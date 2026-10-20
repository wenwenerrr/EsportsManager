using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Events
{
    /// <summary>One action point in the fixed event draw order (Story 006).</summary>
    public readonly struct ActionPointRef
    {
        /// <summary>Action of the point.</summary>
        public readonly EventAction Action;
        /// <summary>Player who spent it (0 for a sponsor point).</summary>
        public readonly long PlayerId;
        /// <summary>Training target (train points), else 0.</summary>
        public readonly int DimensionIndex;

        /// <summary>Creates a point.</summary>
        public ActionPointRef(EventAction action, long playerId, int dimensionIndex = 0)
        {
            Action = action; PlayerId = playerId; DimensionIndex = dimensionIndex;
        }
    }

    /// <summary>
    /// Rolls random events (Story 006, design/numbers.md 4.17) on its OWN random source, so the segment runner, team and
    /// tournament streams draw exactly as without events. Pure C#, not thread-safe. Applying effects is
    /// <see cref="EventApplier"/>'s job; the segment runner applies the action modifiers inside its own formulas.
    /// <para>DRAW ORDER (fixed):</para>
    /// <list type="bullet">
    /// <item><see cref="RollChoices"/> (segment start): per choice slot one U(0,1) trigger; when it triggers and a candidate exists:
    /// one U(0, total weight) pick, one NextInt subject pick (not for team subjects), then every option's effects in order, one
    /// U(min,max) per effect with min &lt; max.</item>
    /// <item><see cref="RollActionEvents"/> (resolve, before any action): for each point in order while fewer than
    /// max_action_events happened: one U(0,1) trigger; when it triggers: one U(0,1) good/bad draw; when the pool for
    /// (action, tone) has an eligible event: one U(0, total weight) pick, then one U(min,max) per effect with min &lt; max.
    /// An empty pool produces nothing and does not count toward the cap. Points past the cap draw nothing.</item>
    /// <item><see cref="RollStateEvents"/> (resolve, after mood and stress): per state slot: FIRST one U(0,1) against
    /// state_attempt_chance (always drawn, even with no candidate; a miss leaves the slot empty); then candidates = state events not yet
    /// tried this segment whose conditions hold for at least one subject; none = stop. Weighted draw WITHOUT replacement
    /// (2026-10-20 user decision): one U(0, total weight of the remaining candidates) pick, one U(0,1) against that event's
    /// chance; on failure the candidate is removed and the next pick follows, until one passes or none remain (then the slot
    /// is empty). On success: one NextInt subject pick (not for team) and the effect draws. Failed candidates are not
    /// retried in later slots.</item>
    /// </list>
    /// </summary>
    public sealed class EventEngine
    {
        private readonly EventConfig _cfg;
        private readonly IRandomSource _rng;
        private readonly Dictionary<string, float> _traitMods = new Dictionary<string, float>();

        /// <summary>Creates an engine; validates the config (trait ids against <paramref name="players"/>).</summary>
        public EventEngine(EventConfig config, PlayerConfig players, IRandomSource rng)
        {
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            config.Validate(players);
            foreach (var m in config.traitModifiers) _traitMods[m.trait] = m.value;
        }

        /// <summary>The config.</summary>
        public EventConfig Config => _cfg;

        /// <summary>
        /// P(good) (numbers.md 4.17): clamp(p_good_base + g_mood x (mood - 50) - g_stress x (stress - 50) + trait modifier,
        /// p_good_min, p_good_max).
        /// </summary>
        public static float GoodChance(EventConfig c, float mood, float stress, float traitModifier)
        {
            float p = c.goodBase + c.goodMoodWeight * (mood - c.goodNeutral) - c.goodStressWeight * (stress - c.goodNeutral) + traitModifier;
            return p < c.goodMin ? c.goodMin : (p > c.goodMax ? c.goodMax : p);
        }

        /// <summary>Sum of the configured modifiers of the player's traits (0 when none listed).</summary>
        public float TraitModifier(Player p)
        {
            float t = 0f;
            foreach (var trait in p.Traits) if (_traitMods.TryGetValue(trait, out float v)) t += v;
            return t;
        }

        /// <summary>P(good) for a player's action point.</summary>
        public float GoodChanceFor(Player p) => GoodChance(_cfg, p.Mood, p.Stress, TraitModifier(p));

        /// <summary>P(good) for a club action (sponsorship): average starter mood / stress, no trait modifier.</summary>
        public float GoodChanceForClub(Team team) => GoodChance(_cfg, team.AverageStarterMood(), team.AverageStarterStress(), 0f);

        // ------------------------------------------------------------ rolling

        /// <summary>Rolls this segment's choice events (segment start). Ids continue from <paramref name="nextId"/>.</summary>
        public List<PendingChoice> RollChoices(Team team, ref int nextId)
        {
            var result = new List<PendingChoice>();
            var used = new HashSet<string>();
            for (int slot = 0; slot < _cfg.maxChoiceEvents; slot++)
            {
                if (!(_rng.NextFloat(0f, 1f) < _cfg.choiceEventChance)) continue;
                var cands = new List<EventDef>();
                var subjects = new List<List<(Player, Player)>>();
                foreach (var e in _cfg.events)
                {
                    if (e.kind != EventKind.Choice || used.Contains(e.id)) continue;
                    var s = Subjects(e, team, null);
                    if (s.Count == 0) continue;
                    cands.Add(e); subjects.Add(s);
                }
                if (cands.Count == 0) continue;
                int k = PickWeighted(cands);
                var ev = cands[k];
                used.Add(ev.id);
                var (a, b) = PickSubject(ev, subjects[k]);
                var pc = new PendingChoice
                {
                    Id = nextId++, EventId = ev.id, TitleKey = ev.titleKey, TextKey = ev.textKey,
                    PlayerId = a?.Id, PartnerId = b?.Id, DefaultOption = ev.defaultOption,
                };
                foreach (var opt in ev.options)
                {
                    var view = new ChoiceOptionView { LabelKey = opt.labelKey };
                    foreach (var x in opt.effects) view.Effects.Add(RollEffect(x));
                    pc.Options.Add(view);
                }
                result.Add(pc);
            }
            return result;
        }

        /// <summary>Rolls action events for the points in the fixed order (see class remarks). Changes nothing.</summary>
        public List<EventOccurrence> RollActionEvents(Team team, IReadOnlyList<ActionPointRef> points)
        {
            var result = new List<EventOccurrence>();
            for (int i = 0; i < points.Count && result.Count < _cfg.maxActionEvents; i++)
            {
                var pt = points[i];
                if (!(_rng.NextFloat(0f, 1f) < _cfg.actionEventChance)) continue;
                Player actor = pt.Action == EventAction.Sponsor ? null : Find(team, pt.PlayerId);
                if (pt.Action != EventAction.Sponsor && actor == null) continue;
                float pGood = actor == null ? GoodChanceForClub(team) : GoodChanceFor(actor);
                var tone = _rng.NextFloat(0f, 1f) < pGood ? EventTone.Good : EventTone.Bad;
                var cands = new List<EventDef>();
                foreach (var e in _cfg.events)
                    if (e.kind == EventKind.Action && e.action == pt.Action && e.tone == tone && Holds(e, team, actor, null)) cands.Add(e);
                if (cands.Count == 0) continue;
                var ev = cands[PickWeighted(cands)];
                var o = NewOccurrence(ev, actor, null);
                o.PointIndex = i;
                foreach (var x in ev.effects) o.Effects.Add(RollEffect(x));
                result.Add(o);
            }
            return result;
        }

        /// <summary>Rolls state events (after mood and stress). Changes nothing.</summary>
        public List<EventOccurrence> RollStateEvents(Team team)
        {
            var result = new List<EventOccurrence>();
            var used = new HashSet<string>();
            for (int slot = 0; slot < _cfg.maxStateEvents; slot++)
            {
                if (!(_rng.NextFloat(0f, 1f) < _cfg.stateAttemptChance)) continue;
                var cands = new List<EventDef>();
                var subjects = new List<List<(Player, Player)>>();
                foreach (var e in _cfg.events)
                {
                    if (e.kind != EventKind.State || used.Contains(e.id)) continue;
                    var s = Subjects(e, team, null);
                    if (s.Count == 0) continue;
                    cands.Add(e); subjects.Add(s);
                }
                if (cands.Count == 0) break;
                while (cands.Count > 0)
                {
                    int k = PickWeighted(cands);
                    var ev = cands[k];
                    var subj = subjects[k];
                    used.Add(ev.id);
                    cands.RemoveAt(k); subjects.RemoveAt(k);
                    if (!(_rng.NextFloat(0f, 1f) < ev.chance)) continue;
                    var (a, b) = PickSubject(ev, subj);
                    var o = NewOccurrence(ev, a, b);
                    foreach (var x in ev.effects) o.Effects.Add(RollEffect(x));
                    result.Add(o);
                    break;
                }
            }
            return result;
        }

        /// <summary>Builds the occurrence of an answered (or defaulted) choice from its pre-rolled values.</summary>
        public EventOccurrence ResolveChoice(PendingChoice choice, int option, bool wasDefault)
        {
            var ev = _cfg.Find(choice.EventId);
            var o = new EventOccurrence
            {
                EventId = choice.EventId, Kind = EventKind.Choice, Tone = EventTone.Neutral, Action = EventAction.None,
                TitleKey = choice.TitleKey, TextKey = choice.TextKey, PlayerId = choice.PlayerId, PartnerId = choice.PartnerId,
                ChoiceId = choice.Id, OptionTaken = option, OptionWasDefault = wasDefault,
                OptionLabelKey = choice.Options[option].LabelKey,
            };
            foreach (var x in choice.Options[option].Effects) o.Effects.Add(x.Clone());
            _ = ev;
            return o;
        }

        // ------------------------------------------------------------ conditions

        /// <summary>True when every condition of <paramref name="e"/> holds for the subject (a, b) on this team.</summary>
        public bool Holds(EventDef e, Team team, Player a, Player b)
        {
            foreach (var c in e.conditions) if (!Holds(c, team, a, b)) return false;
            return true;
        }

        private static bool Holds(EventConditionDef c, Team team, Player a, Player b)
        {
            float v = c.value;
            switch (c.type)
            {
                case ConditionType.StressAbove: return a != null && a.Stress > v;
                case ConditionType.StressBelow: return a != null && a.Stress < v;
                case ConditionType.MoodAbove: return a != null && a.Mood > v;
                case ConditionType.MoodBelow: return a != null && a.Mood < v;
                case ConditionType.AgeAbove: return a != null && a.Age > v;
                case ConditionType.AgeBelow: return a != null && a.Age < v;
                case ConditionType.TraitHas:
                    if (a == null) return false;
                    foreach (var t in a.Traits) if (t == c.trait) return true;
                    return false;
                case ConditionType.ChemistryBelow: return team.Chemistry() < v;
                case ConditionType.ChemistryAbove: return team.Chemistry() > v;
                case ConditionType.PairBadCompat: return a != null && b != null && a.CompatibilityWith(b) <= v;
                case ConditionType.RecentWinRateAbove: return team.RecentResults.Count > 0 && team.RecentWinRate() > v;
                case ConditionType.RecentWinRateBelow: return team.RecentResults.Count > 0 && team.RecentWinRate() < v;
                case ConditionType.FundsBelow: return team.Funds < v;
                case ConditionType.ClubPopularityAtLeast: return team.ClubPopularity >= v;
                case ConditionType.RecentLossStreak:
                {
                    var list = new List<bool>(team.RecentResults);
                    int n = (int)v;
                    if (list.Count < n) return false;
                    for (int i = list.Count - n; i < list.Count; i++) if (list[i]) return false;
                    return true;
                }
                default: return false;
            }
        }

        // Every subject the event could be about (team conditions included), in roster / starter order.
        private List<(Player, Player)> Subjects(EventDef e, Team team, Player only)
        {
            var r = new List<(Player, Player)>();
            switch (e.subject)
            {
                case EventSubject.Team:
                    if (Holds(e, team, null, null)) r.Add((null, null));
                    break;
                case EventSubject.Player:
                    foreach (var p in team.Roster) if (Holds(e, team, p, null)) r.Add((p, null));
                    break;
                case EventSubject.Starter:
                    foreach (var p in team.Starters()) if (Holds(e, team, p, null)) r.Add((p, null));
                    break;
                case EventSubject.Pair:
                    var s = team.Starters();
                    for (int i = 0; i < s.Count; i++)
                        for (int j = i + 1; j < s.Count; j++)
                            if (Holds(e, team, s[i], s[j])) r.Add((s[i], s[j]));
                    break;
            }
            return r;
        }

        private (Player, Player) PickSubject(EventDef e, List<(Player, Player)> subjects)
        {
            if (e.subject == EventSubject.Team) return (null, null);
            return subjects[_rng.NextInt(0, subjects.Count)];
        }

        private int PickWeighted(List<EventDef> cands)
        {
            float total = 0f;
            foreach (var e in cands) total += e.weight;
            float r = _rng.NextFloat(0f, total);
            float acc = 0f;
            for (int i = 0; i < cands.Count; i++)
            {
                acc += cands[i].weight;
                if (r < acc) return i;
            }
            return cands.Count - 1;
        }

        private EventEffectResult RollEffect(EventEffectDef x)
        {
            float v = x.min < x.max ? _rng.NextFloat(x.min, x.max) : x.min;
            if (x.type == EffectType.Funds) v = (float)Math.Round(v, MidpointRounding.AwayFromZero);
            return new EventEffectResult { Type = x.type, Target = x.target, Value = v };
        }

        private static EventOccurrence NewOccurrence(EventDef e, Player a, Player b) => new EventOccurrence
        {
            EventId = e.id, Kind = e.kind, Tone = e.tone, Action = e.action, TitleKey = e.titleKey, TextKey = e.textKey,
            PlayerId = a?.Id, PartnerId = b?.Id,
        };

        private static Player Find(Team team, long id)
        {
            foreach (var p in team.Roster) if (p.Id == id) return p;
            return null;
        }
    }

    /// <summary>
    /// Applies the attribute effects of occurrences through the existing setters (Player.Mood / Stress / Popularity clamp to
    /// 0-100, Player.AdjustPotential clamps 0-100, Team.ClubPopularity clamps, Team.SetFamiliarity clamps to [0, F_max]).
    /// Personality (numbers.md 3.2): a negative mood value x negative_mood_change, a positive stress value x stress_gain.
    /// Players who left the roster are skipped. Fills <see cref="EventEffectResult.Applied"/> and PlayerIds.
    /// </summary>
    public static class EventApplier
    {
        /// <summary>Before settlement: mood, stress, potential.</summary>
        public static void ApplyPersonal(EventOccurrence o, Team team)
        {
            foreach (var x in o.Effects)
            {
                if (x.Type != EffectType.Mood && x.Type != EffectType.Stress && x.Type != EffectType.Potential) continue;
                foreach (var p in Players(o, x.Target, team))
                {
                    float before, after;
                    switch (x.Type)
                    {
                        case EffectType.Mood:
                            float m = x.Value < 0f ? x.Value * p.GetTraitMultiplier(PlayerEffectKeys.NegativeMoodChange) : x.Value;
                            before = p.Mood; p.Mood += m; after = p.Mood; break;
                        case EffectType.Stress:
                            float s = x.Value > 0f ? x.Value * p.GetTraitMultiplier(PlayerEffectKeys.StressGain) : x.Value;
                            before = p.Stress; p.Stress += s; after = p.Stress; break;
                        default:
                            before = p.Potential; p.AdjustPotential(x.Value); after = p.Potential; break;
                    }
                    x.Applied += after - before;
                    x.PlayerIds.Add(p.Id);
                }
            }
        }

        /// <summary>Signed yuan of the funds effects (also fills Applied). The caller books them as event income / expenses.</summary>
        public static long Funds(EventOccurrence o)
        {
            long t = 0;
            foreach (var x in o.Effects)
                if (x.Type == EffectType.Funds) { x.Applied = x.Value; t += (long)x.Value; }
            return t;
        }

        /// <summary>After settlement: player popularity, club popularity, familiarity.</summary>
        public static void ApplyAfterSettle(EventOccurrence o, Team team)
        {
            foreach (var x in o.Effects)
            {
                switch (x.Type)
                {
                    case EffectType.PlayerPopularity:
                        foreach (var p in Players(o, x.Target, team))
                        {
                            float b = p.Popularity; p.Popularity += x.Value;
                            x.Applied += p.Popularity - b; x.PlayerIds.Add(p.Id);
                        }
                        break;
                    case EffectType.ClubPopularity:
                    {
                        float b = team.ClubPopularity; team.ClubPopularity += x.Value; x.Applied = team.ClubPopularity - b;
                        break;
                    }
                    case EffectType.Familiarity:
                        foreach (var (a, c) in Pairs(o, x.Target, team))
                        {
                            float b = team.GetFamiliarity(a.Id, c.Id);
                            team.SetFamiliarity(a.Id, c.Id, b + x.Value);
                            x.Applied += team.GetFamiliarity(a.Id, c.Id) - b;
                            if (!x.PlayerIds.Contains(a.Id)) x.PlayerIds.Add(a.Id);
                            if (!x.PlayerIds.Contains(c.Id)) x.PlayerIds.Add(c.Id);
                        }
                        break;
                }
            }
        }

        private static List<Player> Players(EventOccurrence o, EffectTarget target, Team team)
        {
            var r = new List<Player>();
            switch (target)
            {
                case EffectTarget.Subject:
                    AddIf(r, team, o.PlayerId); AddIf(r, team, o.PartnerId); break;
                case EffectTarget.Roster: r.AddRange(team.Roster); break;
                case EffectTarget.Starters: r.AddRange(team.Starters()); break;
            }
            return r;
        }

        private static List<(Player, Player)> Pairs(EventOccurrence o, EffectTarget target, Team team)
        {
            var r = new List<(Player, Player)>();
            var s = team.Starters();
            if (target == EffectTarget.Subject)
            {
                var a = Find(team, o.PlayerId); var b = Find(team, o.PartnerId);
                if (a != null && b != null) r.Add((a, b));
            }
            else if (target == EffectTarget.SubjectWithStarters)
            {
                var a = Find(team, o.PlayerId);
                if (a != null) foreach (var p in s) if (p.Id != a.Id) r.Add((a, p));
            }
            else if (target == EffectTarget.Starters)
            {
                for (int i = 0; i < s.Count; i++) for (int j = i + 1; j < s.Count; j++) r.Add((s[i], s[j]));
            }
            return r;
        }

        private static void AddIf(List<Player> r, Team team, long? id)
        {
            var p = Find(team, id);
            if (p != null) r.Add(p);
        }

        private static Player Find(Team team, long? id)
        {
            if (id == null) return null;
            foreach (var p in team.Roster) if (p.Id == id.Value) return p;
            return null;
        }
    }
}
