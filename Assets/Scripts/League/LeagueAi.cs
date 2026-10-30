using System;
using System.Collections.Generic;
using EsportsManager.Events;
using EsportsManager.Growth;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;

namespace EsportsManager.Leagues
{
    /// <summary>
    /// The league AI (design/gdd/league.md rev 3 "AI 每赛段的决策", "AI 战术", F1-F5; Python reference tools/sim/sim5.py plan_ai,
    /// pick_lineup, ai_stage_tactic, decide_choice). It only DECIDES: the plan it builds goes through the same
    /// <see cref="SegmentRunner"/> and formulas as the player's. Random draws are keyed (purpose "aidec" per club / year / segment,
    /// "tac" per club / event / stage).
    /// </summary>
    public sealed class LeagueAi
    {
        private readonly LeagueWorld _w;

        /// <summary>Creates the AI for a world.</summary>
        public LeagueAi(LeagueWorld world) { _w = world ?? throw new ArgumentNullException(nameof(world)); }

        private LeagueConfig Cfg => _w.Config;
        private LeagueAiConfig A => _w.Config.ai;

        // ------------------------------------------------------------ ratings

        /// <summary>Raw phase rating r(i,k): mean of the main-lane dimensions of phase k (0 early, 1 mid, 2 late).</summary>
        public float PhaseRating(Player p, int phase)
        {
            var dims = _w.Growth.Map.PhaseDims(p.MainLane, phase);
            float s = 0f;
            foreach (int d in dims) s += p.GetDimension(p.MainLane, d);
            return s / dims.Length;
        }

        /// <summary>
        /// Effective rating for lineup choice (league.md rule 1): mean of the three phase ratings x mood factor x stress factor
        /// (match.md effective-value factors, resolute trait included).
        /// </summary>
        public float EffectiveRating(Player p)
        {
            var e = _w.Match.effective;
            double m = MatchFormulas.MoodFactor(e, p.Mood);
            double s = MatchFormulas.StressFactor(e, p.Stress, p.GetTraitMultiplier(PlayerEffectKeys.StressVarianceImpact));
            return (float)((PhaseRating(p, 0) + PhaseRating(p, 1) + PhaseRating(p, 2)) / 3.0 * m * s);
        }

        /// <summary>The player's weakest phase by raw rating (ties early > mid > late).</summary>
        public int WeakestPhase(Player p)
        {
            int best = 0;
            for (int k = 1; k < 3; k++) if (PhaseRating(p, k) < PhaseRating(p, best)) best = k;
            return best;
        }

        // ------------------------------------------------------------ F2

        /// <summary>F2 rest threshold: 55 + 15 x aggression + offset; the segment before an event: minus 15 x (1 - aggression).</summary>
        public float RestThreshold(Club c, bool preEvent)
        {
            float th = A.restBase + A.restAggression * c.Personality.Aggression + c.Personality.OffRest;
            if (preEvent) th -= A.restPreEvent * (1f - c.Personality.Aggression);
            return th;
        }

        /// <summary>AI media rule (economy.md 媒体邀约): accept every invitation when funds &lt; floor or the starters' mean stress &lt; ai_stress_below.</summary>
        public bool AcceptsMedia(Club c)
        {
            if (c.Team.Funds < Floor(c)) return true;
            var st = c.Team.Starters();
            if (st.Count == 0) return false;
            float sum = 0f;
            foreach (var p in st) sum += p.Stress;
            return sum / st.Count < _w.Config.media.aiStressBelow;
        }

        /// <summary>F2 cash floor = (3 + 3 x frugality + offset) x W.</summary>
        public double Floor(Club c) => (A.floorBase + A.floorFrugal * c.Personality.Frugality + c.Personality.OffCash) * c.WagesPerSegment(_w.SegmentsPerYear);

        // ------------------------------------------------------------ rule 1: lineup

        /// <summary>
        /// Rule 1: per lane the main-lane player with the highest effective rating; the current starter stays unless beaten by MORE
        /// than lineup_hysteresis (3). A lane without a main-lane player takes the best remaining player by that lane's score and
        /// is remembered in <see cref="Club.NeedLanes"/>. Rosters below min_roster are first topped up by emergency signings.
        /// </summary>
        public void PickLineup(Club c)
        {
            while (c.Team.Roster.Count < A.minRoster) _w.EmergencySign(c);
            var team = c.Team;
            var chosen = new Dictionary<string, Player>();
            var used = new HashSet<long>();
            foreach (var lane in _w.Players.lanes)
            {
                Player best = null; float bestR = float.NegativeInfinity;
                foreach (var p in team.Roster)
                {
                    if (p.MainLane != lane.id) continue;
                    float r = EffectiveRating(p);
                    if (r > bestR || (r == bestR && best != null && p.Id < best.Id)) { best = p; bestR = r; }
                }
                if (best == null) continue;
                var cur = team.GetStarter(lane.id);
                if (cur != null && cur.Id != best.Id && cur.MainLane == lane.id && bestR - EffectiveRating(cur) <= A.lineupHysteresis) best = cur;
                chosen[lane.id] = best;
                used.Add(best.Id);
            }
            c.NeedLanes.Clear();
            foreach (var lane in _w.Players.lanes)
            {
                if (chosen.ContainsKey(lane.id)) continue;
                Player best = null;
                foreach (var p in team.Roster)
                    if (!used.Contains(p.Id) && (best == null || p.GetLaneScore(lane.id) > best.GetLaneScore(lane.id))) best = p;
                if (best == null) continue;
                chosen[lane.id] = best;
                used.Add(best.Id);
                c.NeedLanes.Add(lane.id);
            }
            foreach (var lane in _w.Players.lanes) team.ClearStarter(lane.id);
            foreach (var kv in chosen) team.SetStarter(kv.Key, kv.Value);
        }

        // ------------------------------------------------------------ F5

        /// <summary>F5 answer of the AI to a two-choice event (option index).</summary>
        public int ChooseOption(Club c, PendingChoice ch, bool preEvent)
        {
            string kind = Cfg.ChoiceKind(ch.EventId);
            var p = ch.PlayerId != null ? c.Find(ch.PlayerId.Value) : null;
            switch (kind)
            {
                case "leave": return p != null && p.Stress > RestThreshold(c, false) && !preEvent ? 0 : 1;
                case "money": return c.Team.Funds < Floor(c) && p != null && p.Stress < A.moneyChoiceStress ? 0 : 1;
                case "spend":
                {
                    double cost = 0;
                    foreach (var fx in ch.Options[0].Effects) if (fx.Type == EffectType.Funds && fx.Value < 0) cost -= fx.Value;
                    return c.Team.Funds - cost >= Floor(c) ? 0 : 1;
                }
                case "conflict": return 0;
                default: return ch.DefaultOption;
            }
        }

        // ------------------------------------------------------------ per-segment plan (steps 0-8)

        private sealed class Budget
        {
            public int Left;
            public readonly int Max;
            public readonly Dictionary<long, int> Used = new Dictionary<long, int>();
            public readonly HashSet<long> Blocked = new HashSet<long>();
            public Budget(int total, int max) { Left = total; Max = max; }
            public bool Can(Player p) => Left > 0 && !Blocked.Contains(p.Id) && N(p) < Max;
            public int N(Player p) => Used.TryGetValue(p.Id, out int n) ? n : 0;
            public void Take(Player p) { Used[p.Id] = N(p) + 1; Left--; }
        }

        /// <summary>
        /// Builds the AI plan of the open segment (league.md steps 0-8; step 8 (choices) is decided first because a leave blocks
        /// the player's points). Draws on the club's keyed "aidec" stream for this year / segment.
        /// </summary>
        public SegmentPlan Plan(Club c, int segmentIndex)
        {
            var rd = _w.Keys.Stream("aidec", _w.Year, segmentIndex, c.Id);
            var state = c.Runner.GetState();
            var plan = new SegmentPlan();
            var b = new Budget(state.ActionPoints, state.MaxPerPlayer);
            bool pre = _w.IsPreEventSegment(segmentIndex);

            // 8. choices (F5) -- decided first: a leave blocks the player's points this segment
            foreach (var ch in state.PendingChoices)
            {
                int opt = ChooseOption(c, ch, pre);
                plan.AnswerChoice(ch.Id, opt);
                if (ch.PlayerId != null && ch.Options[opt].PutsOnLeave) b.Blocked.Add(ch.PlayerId.Value);
            }
            // 1. lineup
            PickLineup(c);
            var team = c.Team;
            var starters = team.Starters();
            // 0. media invitations (economy.md 媒体邀约): one club point each, accepted when short of money or the starters are calm
            if (AcceptsMedia(c))
                foreach (var inv in c.Runner.MediaInvitations)
                    if (b.Left > 0) { plan.AcceptMedia(inv.Id); b.Left--; }
            // 2. rest
            float th = RestThreshold(c, pre);
            var resting = new HashSet<long>();
            foreach (var p in Sorted(starters, (x, y) => y.Stress.CompareTo(x.Stress)))
            {
                int n = p.Stress > th + A.restDoubleMargin ? 2 : p.Stress > th ? 1 : 0;
                for (int i = 0; i < n && b.Can(p); i++) { plan.Rest(p.Id); b.Take(p); resting.Add(p.Id); }
            }
            // 3. sponsorship: every free slot, one attempt each
            int sp = Math.Min(state.SponsorAttemptsRemaining, b.Left);
            plan.SponsorPoints = Math.Max(0, sp);
            b.Left -= plan.SponsorPoints;
            // 4 / 5. cash mode (F3)
            bool emergency = team.Funds < Floor(c);
            var starterIds = new HashSet<long>();
            foreach (var p in starters) starterIds.Add(p.Id);
            if (emergency)
            {
                int k = 0;
                foreach (var p in Sorted(team.Roster, (x, y) => y.Popularity.CompareTo(x.Popularity)))
                {
                    if (k >= A.streamCount) break;
                    if (resting.Contains(p.Id) || !b.Can(p)) continue;
                    plan.Stream(p.Id); b.Take(p); k++;
                }
                foreach (var o in state.PendingOffers)
                {
                    var p = c.Find(o.PlayerId);
                    if (p != null && p.Stress < A.emergencyBusinessStress && b.Can(p)) { plan.AcceptOffer(o.Id); b.Take(p); }
                }
            }
            else
            {
                foreach (var o in state.PendingOffers)
                {
                    double u = rd.NextDouble();
                    var p = c.Find(o.PlayerId);
                    if (p != null && p.Stress < A.normalBusinessStress && b.Can(p) && u < c.Personality.Frugality) { plan.AcceptOffer(o.Id); b.Take(p); }
                }
                int k = 0;
                foreach (var p in Sorted(team.Roster, (x, y) => y.Popularity.CompareTo(x.Popularity)))
                {
                    if (k >= A.streamCount) break;
                    if (starterIds.Contains(p.Id) || !b.Can(p)) continue;
                    plan.Stream(p.Id); b.Take(p); k++;
                }
            }
            // 7. prospect point (reserved before the starters' training)
            Player prospect = null;
            if (resting.Count == 0 && b.Left >= 1)
                foreach (var p in team.Roster)
                {
                    if (starterIds.Contains(p.Id) || p.Age > A.prospectMaxAge || !b.Can(p)) continue;
                    if (prospect == null || _w.Economy.PotentialEstimate(p) > _w.Economy.PotentialEstimate(prospect)) prospect = p;
                }
            int reserve = prospect != null ? 1 : 0;
            // command rule
            Player cmd = null;
            if (segmentIndex % A.commandEvery == 0 && starters.Count > 0)
            {
                float maxCmd = float.NegativeInfinity;
                foreach (var p in starters)
                {
                    float v = p.GetDimension(p.MainLane, _w.Growth.Map.CommandIndex(p.MainLane));
                    if (v > maxCmd) { maxCmd = v; cmd = p; }
                }
                if (maxCmd >= A.commandThreshold) cmd = null;
            }
            // 6. training by expected gain age_f x head
            var order = new List<(float g, Player p, TrainingTarget t, int f)>();
            foreach (var p in starters)
            {
                if (resting.Contains(p.Id) || b.Blocked.Contains(p.Id)) continue;
                var t = TrainTarget(c, p, rd, out int focus);
                order.Add((_w.Players.GetAgeCoefficient(p.Age) * MeanHead(p, t), p, t, focus));
            }
            order.Sort((x, y) => { int k = y.g.CompareTo(x.g); return k != 0 ? k : x.p.Id.CompareTo(y.p.Id); });
            foreach (var (g, p, t, f) in order)
                while (b.Can(p) && b.Left > reserve)
                {
                    if (cmd != null && p.Id == cmd.Id) { plan.Train(p.Id, TrainingTarget.Command); cmd = null; }
                    else plan.Train(p.Id, t, f);
                    b.Take(p);
                }
            if (prospect != null && b.Can(prospect))
            {
                var t = TrainTarget(c, prospect, rd, out int focus);
                plan.Train(prospect.Id, t, focus);
                b.Take(prospect);
            }
            return plan;
        }

        /// <summary>
        /// Training target (rule 6): specialty chase (an unowned phase specialty whose dimension >= 72 with that phase's focus >=
        /// 0.45 -> that phase, the dimension as focus; highest dimension first); else with cover_p the player's weakest phase; else
        /// the style phase (Star: the star player trains the star phase, the others the team's weakest phase).
        /// </summary>
        public TrainingTarget TrainTarget(Club c, Player p, KeyedStream rd, out int focusDim)
        {
            focusDim = -1;
            float bestD = float.NegativeInfinity; int bestPhase = -1;
            foreach (var sp in _w.Growth.Progress(p))
            {
                if (sp.Phase < 0 || sp.Owned) continue;
                float d = p.GetDimension(p.MainLane, sp.DimensionIndex);
                if (d >= A.chaseValue && p.GetFocus(sp.Phase) >= A.chaseFocus && d > bestD) { bestD = d; bestPhase = sp.Phase; focusDim = sp.DimensionIndex; }
            }
            if (bestPhase >= 0) return (TrainingTarget)bestPhase;
            if (rd.NextDouble() < A.coverChance) return (TrainingTarget)WeakestPhase(p);
            if (c.Style == ClubStyle.Star)
            {
                if (p.MainLane == c.StarLane) return (TrainingTarget)c.StarPhase;
                var s = _w.PhaseStrength(c);
                int w = 0;
                if (s != null) for (int k = 1; k < 3; k++) if (s[k] < s[w]) w = k;
                return (TrainingTarget)w;
            }
            return (TrainingTarget)(int)c.Style;
        }

        private float MeanHead(Player p, TrainingTarget t)
        {
            var dims = _w.Growth.TargetDims(p.MainLane, t);
            float s = 0f;
            foreach (int d in dims) s += _w.Growth.Head(p, d);
            return s / dims.Length;
        }

        // ------------------------------------------------------------ tactics

        /// <summary>The tactic whose focus is phase <paramref name="phase"/>.</summary>
        public MatchTactic TacticOfPhase(int phase)
        {
            foreach (var t in MatchTactics.All) if ((int)_w.Match.tactic.FocusOf(t) == phase) return t;
            return MatchTactic.Invade;
        }

        /// <summary>The style's tactic (Star: the star phase's).</summary>
        public MatchTactic StyleTactic(Club c) => TacticOfPhase(c.Style == ClubStyle.Star ? c.StarPhase : (int)c.Style);

        /// <summary>Phase strengths rounded to the radar step (what every club sees of an opponent).</summary>
        public float[] Radar(float[] s)
        {
            if (s == null) return null;
            float step = Cfg.tactics.radarStep;
            var r = new float[3];
            for (int k = 0; k < 3; k++) r[k] = step * (float)Math.Round(s[k] / step, MidpointRounding.AwayFromZero);
            return r;
        }

        /// <summary>
        /// Regular-season tactic: with p_style_noise the style tactic, else the phase with the largest lead over the league median
        /// (8 clubs incl. itself; itself exact, the others on the radar scale). Draw key ("tac", year, event, 0, club).
        /// </summary>
        public MatchTactic RegularTactic(Club c, int eventIndex)
        {
            double u = _w.Keys.U01("tac", _w.Year, eventIndex, 0, c.Id);
            if (u < Cfg.tactics.styleNoise) return StyleTactic(c);
            var me = _w.PhaseStrength(c);
            if (me == null) return StyleTactic(c);
            var gap = new float[3];
            for (int k = 0; k < 3; k++)
            {
                var vals = new List<float> { me[k] };
                foreach (var o in _w.Clubs)
                {
                    if (o.Id == c.Id) continue;
                    var r = Radar(_w.PhaseStrength(o));
                    if (r != null) vals.Add(r[k]);
                }
                gap[k] = me[k] - Median(vals);
            }
            return TacticOfPhase(ArgMax(gap));
        }

        /// <summary>Playoff / finals tactic: the phase with the largest lead over the opponent's radar-scale strengths.</summary>
        public MatchTactic PlayoffTactic(Club c, Club opponent)
        {
            var me = _w.PhaseStrength(c);
            var op = Radar(opponent != null ? _w.PhaseStrength(opponent) : null);
            if (me == null || op == null) return StyleTactic(c);
            var gap = new float[3];
            for (int k = 0; k < 3; k++) gap[k] = me[k] - op[k];
            return TacticOfPhase(ArgMax(gap));
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Index of the largest value (ties: the first).</summary>
        public static int ArgMax(float[] v)
        {
            int b = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[b]) b = i;
            return b;
        }

        /// <summary>Median of a list (mean of the two middle values for an even count).</summary>
        public static float Median(List<float> v)
        {
            if (v.Count == 0) return 0f;
            var s = new List<float>(v);
            s.Sort();
            int n = s.Count;
            return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2f;
        }

        private static List<Player> Sorted(IReadOnlyList<Player> src, Comparison<Player> cmp)
        {
            var l = new List<Player>(src);
            l.Sort((x, y) => { int k = cmp(x, y); return k != 0 ? k : x.Id.CompareTo(y.Id); });
            return l;
        }
    }
}
