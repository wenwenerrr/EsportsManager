using System;
using System.Collections.Generic;
using EsportsManager.Players;

namespace EsportsManager.Events
{
    /// <summary>Kind of random event (Story 006, design/numbers.md 4.17).</summary>
    public enum EventKind
    {
        /// <summary>Rolled per action point at resolve.</summary>
        Action,
        /// <summary>Rolled after mood and stress when its conditions hold.</summary>
        State,
        /// <summary>Two (or more) options; generated at segment start and answered before resolve.</summary>
        Choice,
    }

    /// <summary>Good / bad tone (choice events are neutral).</summary>
    public enum EventTone { Good, Bad, Neutral }

    /// <summary>The action an action event hangs on (None for state / choice events).</summary>
    public enum EventAction { None, Train, Stream, Rest, Business, Sponsor }

    /// <summary>Who the event is about.</summary>
    public enum EventSubject
    {
        /// <summary>One rostered player (for action events: the player who spent the point).</summary>
        Player,
        /// <summary>One starter.</summary>
        Starter,
        /// <summary>A pair of starters (pair conditions).</summary>
        Pair,
        /// <summary>The club as a whole (sponsor action events, team-wide state / choice events).</summary>
        Team,
    }

    /// <summary>Structured condition (all conditions of an event must hold).</summary>
    public enum ConditionType
    {
        /// <summary>Subject player stress &gt; value.</summary>
        StressAbove,
        /// <summary>Subject player stress &lt; value.</summary>
        StressBelow,
        /// <summary>Subject player mood &gt; value.</summary>
        MoodAbove,
        /// <summary>Subject player mood &lt; value.</summary>
        MoodBelow,
        /// <summary>Subject player has the trait id in <c>trait</c>.</summary>
        TraitHas,
        /// <summary>Subject player age &gt; value.</summary>
        AgeAbove,
        /// <summary>Club popularity (0-100) &gt;= value.</summary>
        ClubPopularityAtLeast,
        /// <summary>Subject player age &lt; value.</summary>
        AgeBelow,
        /// <summary>Team chemistry (0-100) &lt; value.</summary>
        ChemistryBelow,
        /// <summary>Team chemistry (0-100) &gt; value.</summary>
        ChemistryAbove,
        /// <summary>Subject pair compatibility H(i,j) &lt;= value (value is negative, e.g. -0.3).</summary>
        PairBadCompat,
        /// <summary>The last <c>value</c> recorded series results are all losses.</summary>
        RecentLossStreak,
        /// <summary>Recent win rate (0-100) &gt; value.</summary>
        RecentWinRateAbove,
        /// <summary>Recent win rate (0-100) &lt; value.</summary>
        RecentWinRateBelow,
        /// <summary>Club funds (yuan) &lt; value.</summary>
        FundsBelow,
    }

    /// <summary>Structured effect (numbers.md 4.17 "效果种类").</summary>
    public enum EffectType
    {
        /// <summary>Mood points (negative values x negative_mood_change trait multiplier).</summary>
        Mood,
        /// <summary>Stress points (positive values x stress_gain trait multiplier).</summary>
        Stress,
        /// <summary>Player popularity points.</summary>
        PlayerPopularity,
        /// <summary>Club popularity points.</summary>
        ClubPopularity,
        /// <summary>Pair familiarity points (numbers.md 4.8), which moves chemistry.</summary>
        Familiarity,
        /// <summary>True potential points (numbers.md 4.15 breakthrough / slump) via Player.AdjustPotential.</summary>
        Potential,
        /// <summary>Club funds, yuan (+ = event income, - = event expense, numbers.md 4.10).</summary>
        Funds,
        /// <summary>Multiplier on this training point's gains.</summary>
        TrainMultiplier,
        /// <summary>Extra points on this training point's target dimension.</summary>
        TrainAdd,
        /// <summary>Multiplier on this stream point's income.</summary>
        StreamMultiplier,
        /// <summary>Extra yuan on this stream point's income.</summary>
        StreamAdd,
        /// <summary>Multiplier on this rest point's stress relief.</summary>
        RestMultiplier,
        /// <summary>Multiplier on this accepted deal's club share.</summary>
        BusinessMultiplier,
        /// <summary>Added to this sponsorship attempt's success chance (then clamped to [min_chance, max_chance]).</summary>
        SponsorChanceAdd,
        /// <summary>This sponsorship attempt does not happen (the point is wasted).</summary>
        VoidAction,
        /// <summary>Choice option: the subject player is on leave and may receive no action point this segment.</summary>
        OnLeave,
    }

    /// <summary>Who an effect applies to.</summary>
    public enum EffectTarget
    {
        /// <summary>The subject (player, or both players of a pair; for action modifiers: the point itself).</summary>
        Subject,
        /// <summary>Every rostered player.</summary>
        Roster,
        /// <summary>Every starter (for familiarity: every starter pair).</summary>
        Starters,
        /// <summary>The club (funds, club popularity).</summary>
        Club,
        /// <summary>Familiarity of the subject player with every other starter.</summary>
        SubjectWithStarters,
    }

    /// <summary>One condition.</summary>
    public sealed class EventConditionDef
    {
        /// <summary>Condition type.</summary>
        public ConditionType type;
        /// <summary>Threshold (meaning per type).</summary>
        public float value;
        /// <summary>Trait id for TraitHas, empty otherwise.</summary>
        public string trait = "";
    }

    /// <summary>One effect; the value is U(min, max) (no draw when min == max).</summary>
    public sealed class EventEffectDef
    {
        /// <summary>Effect type.</summary>
        public EffectType type;
        /// <summary>Target.</summary>
        public EffectTarget target;
        /// <summary>Lower bound of the rolled value.</summary>
        public float min;
        /// <summary>Upper bound of the rolled value (&gt;= min).</summary>
        public float max;
    }

    /// <summary>One option of a choice event.</summary>
    public sealed class ChoiceOptionDef
    {
        /// <summary>Text-table key of the button label.</summary>
        public string labelKey;
        /// <summary>Effects when this option is taken (may be empty).</summary>
        public EventEffectDef[] effects = new EventEffectDef[0];
    }

    /// <summary>One event definition (texts live in the text table under the keys).</summary>
    public sealed class EventDef
    {
        /// <summary>Unique id.</summary>
        public string id;
        /// <summary>Kind.</summary>
        public EventKind kind;
        /// <summary>Tone (Neutral only for choice events).</summary>
        public EventTone tone;
        /// <summary>Trigger action (action events only).</summary>
        public EventAction action;
        /// <summary>Subject.</summary>
        public EventSubject subject;
        /// <summary>Relative weight inside its pool, &gt; 0.</summary>
        public float weight;
        /// <summary>State events: chance the drawn candidate actually happens, (0, 1]; 1 for the other kinds.</summary>
        public float chance;
        /// <summary>Title text key.</summary>
        public string titleKey;
        /// <summary>Body text key ({player} / {partner} placeholders).</summary>
        public string textKey;
        /// <summary>All must hold.</summary>
        public EventConditionDef[] conditions = new EventConditionDef[0];
        /// <summary>Effects (action / state events; empty for choice events).</summary>
        public EventEffectDef[] effects = new EventEffectDef[0];
        /// <summary>Options (choice events only, &gt;= 2).</summary>
        public ChoiceOptionDef[] options = new ChoiceOptionDef[0];
        /// <summary>Option applied when the player does not answer (choice events only).</summary>
        public int defaultOption;
    }

    /// <summary>A personality modifier on P(good) (numbers.md 4.17: within +-max_trait_modifier per trait).</summary>
    public sealed class TraitModifierDef
    {
        /// <summary>Trait id (player_config).</summary>
        public string trait;
        /// <summary>Added to P(good).</summary>
        public float value;
    }

    /// <summary>
    /// Random event knobs and the event table (Story 006, design/numbers.md 4.17, 4.15, 4.10). Pure data; built by
    /// <see cref="EventConfigLoader"/> or in code (tests). <see cref="Validate"/> names the offending field.
    /// </summary>
    public sealed class EventConfig
    {
        /// <summary>p_event: chance one action point triggers an event, [0, 1].</summary>
        public float actionEventChance;
        /// <summary>max_action_events per segment, &gt;= 0 (points after the cap are not rolled).</summary>
        public int maxActionEvents;
        /// <summary>p_good_base.</summary>
        public float goodBase;
        /// <summary>g_mood (per mood point above neutral).</summary>
        public float goodMoodWeight;
        /// <summary>g_stress (per stress point above neutral).</summary>
        public float goodStressWeight;
        /// <summary>The 50 in (mood - 50) and (stress - 50).</summary>
        public float goodNeutral;
        /// <summary>p_good_min.</summary>
        public float goodMin;
        /// <summary>p_good_max.</summary>
        public float goodMax;
        /// <summary>Largest |trait modifier| allowed (0.05 per numbers.md 3.2 / 4.17).</summary>
        public float maxTraitModifier;
        /// <summary>Per-trait P(good) modifiers.</summary>
        public TraitModifierDef[] traitModifiers = new TraitModifierDef[0];
        /// <summary>max_state_events per segment, &gt;= 0.</summary>
        public int maxStateEvents;
        /// <summary>state_attempt_chance: per state slot, chance the slot is tried at all (one draw before candidate selection), [0, 1].</summary>
        public float stateAttemptChance = 1f;
        /// <summary>Chance per choice slot at segment start, [0, 1].</summary>
        public float choiceEventChance;
        /// <summary>Choice slots per segment, &gt;= 0.</summary>
        public int maxChoiceEvents;
        /// <summary>The event table.</summary>
        public EventDef[] events = new EventDef[0];

        /// <summary>Finds an event by id, or null.</summary>
        public EventDef Find(string id)
        {
            foreach (var e in events) if (e.id == id) return e;
            return null;
        }

        /// <summary>
        /// Checks every rule; throws <see cref="InvalidOperationException"/> naming the field. With <paramref name="players"/>
        /// the trait ids (modifiers and TraitHas conditions) are checked against the personality table too.
        /// </summary>
        public void Validate(PlayerConfig players = null)
        {
            Unit(actionEventChance, "action_events.p_event");
            if (maxActionEvents < 0) Fail("action_events.max_per_segment must be >= 0.");
            Finite(goodBase, "good_chance.base"); Finite(goodMoodWeight, "good_chance.mood_weight");
            Finite(goodStressWeight, "good_chance.stress_weight"); Finite(goodNeutral, "good_chance.neutral");
            Unit(goodMin, "good_chance.min"); Unit(goodMax, "good_chance.max");
            if (goodMin > goodMax) Fail("good_chance.min must be <= good_chance.max.");
            if (!(maxTraitModifier >= 0f && maxTraitModifier <= 1f)) Fail("max_trait_modifier must be in [0, 1].");
            if (traitModifiers == null) Fail("trait_modifiers is missing.");
            var traits = new HashSet<string>();
            for (int i = 0; i < traitModifiers.Length; i++)
            {
                var t = traitModifiers[i];
                string f = "trait_modifiers[" + i + "]";
                if (t == null || string.IsNullOrEmpty(t.trait)) Fail(f + ".trait is empty.");
                if (!traits.Add(t.trait)) Fail(f + ".trait '" + t.trait + "' is listed twice.");
                Finite(t.value, f + ".value");
                if (Math.Abs(t.value) > maxTraitModifier + 1e-6f) Fail(f + ".value must be within +-max_trait_modifier (" + maxTraitModifier + ").");
                if (players != null && players.FindTraitGroup(t.trait) < 0) Fail(f + ".trait '" + t.trait + "' is not a known trait.");
            }
            if (maxStateEvents < 0) Fail("state_events.max_per_segment must be >= 0.");
            Unit(stateAttemptChance, "state_attempt_chance");
            Unit(choiceEventChance, "choice_events.p_choice");
            if (maxChoiceEvents < 0) Fail("choice_events.max_per_segment must be >= 0.");
            if (events == null) Fail("events is missing.");
            var ids = new HashSet<string>();
            for (int i = 0; i < events.Length; i++) ValidateEvent(events[i], "events[" + i + "]", ids, players);
        }

        private static void ValidateEvent(EventDef e, string f, HashSet<string> ids, PlayerConfig players)
        {
            if (e == null) Fail(f + " is null.");
            if (string.IsNullOrEmpty(e.id)) Fail(f + ".id is empty.");
            f = f + " (" + e.id + ")";
            if (!ids.Add(e.id)) Fail(f + ".id is not unique.");
            if (!(e.weight > 0f) || float.IsInfinity(e.weight)) Fail(f + ".weight must be > 0.");
            if (!(e.chance > 0f && e.chance <= 1f)) Fail(f + ".chance must be in (0, 1].");
            if (string.IsNullOrEmpty(e.titleKey)) Fail(f + ".title_key is empty.");
            if (string.IsNullOrEmpty(e.textKey)) Fail(f + ".text_key is empty.");
            if (e.conditions == null || e.effects == null || e.options == null) Fail(f + " has a missing list.");
            switch (e.kind)
            {
                case EventKind.Action:
                    if (e.action == EventAction.None) Fail(f + ".action is required for action events.");
                    if (e.tone == EventTone.Neutral) Fail(f + ".tone must be good or bad.");
                    if (e.action == EventAction.Sponsor ? e.subject != EventSubject.Team : e.subject != EventSubject.Player)
                        Fail(f + ".subject must be 'team' for sponsor events and 'player' for the other actions.");
                    if (e.options.Length > 0) Fail(f + ".options must be empty (not a choice event).");
                    if (e.chance != 1f) Fail(f + ".chance must be 1 for action events.");
                    break;
                case EventKind.State:
                    if (e.action != EventAction.None) Fail(f + ".action must be empty for state events.");
                    if (e.tone == EventTone.Neutral) Fail(f + ".tone must be good or bad.");
                    if (e.options.Length > 0) Fail(f + ".options must be empty (not a choice event).");
                    break;
                case EventKind.Choice:
                    if (e.action != EventAction.None) Fail(f + ".action must be empty for choice events.");
                    if (e.tone != EventTone.Neutral) Fail(f + ".tone must be neutral for choice events.");
                    if (e.effects.Length > 0) Fail(f + ".effects must be empty (choice effects live in options).");
                    if (e.options.Length < 2) Fail(f + ".options needs at least 2 entries.");
                    if (e.defaultOption < 0 || e.defaultOption >= e.options.Length) Fail(f + ".default_option is out of range.");
                    if (e.chance != 1f) Fail(f + ".chance must be 1 for choice events.");
                    break;
            }
            for (int c = 0; c < e.conditions.Length; c++) ValidateCondition(e, e.conditions[c], f + ".conditions[" + c + "]", players);
            for (int k = 0; k < e.effects.Length; k++) ValidateEffect(e, e.effects[k], f + ".effects[" + k + "]", false);
            for (int o = 0; o < e.options.Length; o++)
            {
                var opt = e.options[o];
                string of = f + ".options[" + o + "]";
                if (opt == null || string.IsNullOrEmpty(opt.labelKey)) Fail(of + ".label_key is empty.");
                if (opt.effects == null) Fail(of + ".effects is missing.");
                for (int k = 0; k < opt.effects.Length; k++) ValidateEffect(e, opt.effects[k], of + ".effects[" + k + "]", true);
            }
        }

        private static bool IsPlayerCondition(ConditionType t) => t == ConditionType.StressAbove || t == ConditionType.StressBelow
            || t == ConditionType.MoodAbove || t == ConditionType.MoodBelow || t == ConditionType.TraitHas
            || t == ConditionType.AgeAbove || t == ConditionType.AgeBelow;

        private static void ValidateCondition(EventDef e, EventConditionDef c, string f, PlayerConfig players)
        {
            if (c == null) Fail(f + " is null.");
            Finite(c.value, f + ".value");
            bool playerSubject = e.subject == EventSubject.Player || e.subject == EventSubject.Starter;
            if (IsPlayerCondition(c.type) && !playerSubject) Fail(f + ".type needs subject 'player' or 'starter'.");
            if (c.type == ConditionType.PairBadCompat && e.subject != EventSubject.Pair) Fail(f + ".type pair_bad_compat needs subject 'pair'.");
            if (c.type == ConditionType.TraitHas)
            {
                if (string.IsNullOrEmpty(c.trait)) Fail(f + ".trait is empty.");
                if (players != null && players.FindTraitGroup(c.trait) < 0) Fail(f + ".trait '" + c.trait + "' is not a known trait.");
            }
            else if (!string.IsNullOrEmpty(c.trait)) Fail(f + ".trait must be empty for this type.");
            if (c.type == ConditionType.RecentLossStreak && !(c.value >= 1f && c.value == (float)Math.Floor(c.value)))
                Fail(f + ".value must be a whole number >= 1 for recent_loss_streak.");
        }

        private static void ValidateEffect(EventDef e, EventEffectDef x, string f, bool inOption)
        {
            if (x == null) Fail(f + " is null.");
            Finite(x.min, f + ".min"); Finite(x.max, f + ".max");
            if (x.min > x.max) Fail(f + ".min must be <= max.");
            bool player = e.subject == EventSubject.Player || e.subject == EventSubject.Starter;
            switch (x.type)
            {
                case EffectType.Mood: case EffectType.Stress: case EffectType.PlayerPopularity: case EffectType.Potential:
                    if (x.target == EffectTarget.Subject && e.subject == EventSubject.Team) Fail(f + ".target 'subject' needs a player or pair subject.");
                    if (x.target == EffectTarget.Club || x.target == EffectTarget.SubjectWithStarters) Fail(f + ".target must be subject, roster or starters.");
                    break;
                case EffectType.ClubPopularity: case EffectType.Funds:
                    if (x.target != EffectTarget.Club) Fail(f + ".target must be 'club'.");
                    break;
                case EffectType.Familiarity:
                    if (x.target == EffectTarget.Subject && e.subject != EventSubject.Pair) Fail(f + ".target 'subject' needs subject 'pair'.");
                    if (x.target == EffectTarget.SubjectWithStarters && !player) Fail(f + ".target subject_with_starters needs a player subject.");
                    if (x.target == EffectTarget.Roster || x.target == EffectTarget.Club) Fail(f + ".target must be subject, starters or subject_with_starters.");
                    break;
                case EffectType.OnLeave:
                    if (!inOption) Fail(f + ".type on_leave is only allowed in choice options.");
                    if (!player || x.target != EffectTarget.Subject) Fail(f + ".on_leave needs a player subject and target 'subject'.");
                    break;
                default: // action modifiers
                    EventAction need = ModifierAction(x.type);
                    if (e.kind != EventKind.Action || e.action != need) Fail(f + ".type only works on '" + need.ToString().ToLowerInvariant() + "' action events.");
                    if (x.target != EffectTarget.Subject) Fail(f + ".target must be 'subject' for action modifiers.");
                    if ((x.type == EffectType.TrainMultiplier || x.type == EffectType.StreamMultiplier || x.type == EffectType.RestMultiplier
                         || x.type == EffectType.BusinessMultiplier) && x.min < 0f) Fail(f + ".min must be >= 0 for a multiplier.");
                    break;
            }
        }

        /// <summary>The action an action-modifier effect belongs to (None for attribute effects).</summary>
        public static EventAction ModifierAction(EffectType t)
        {
            switch (t)
            {
                case EffectType.TrainMultiplier: case EffectType.TrainAdd: return EventAction.Train;
                case EffectType.StreamMultiplier: case EffectType.StreamAdd: return EventAction.Stream;
                case EffectType.RestMultiplier: return EventAction.Rest;
                case EffectType.BusinessMultiplier: return EventAction.Business;
                case EffectType.SponsorChanceAdd: case EffectType.VoidAction: return EventAction.Sponsor;
                default: return EventAction.None;
            }
        }

        private static void Unit(float v, string f) { if (!(v >= 0f && v <= 1f)) Fail(f + " must be in [0, 1]."); }
        private static void Finite(float v, string f) { if (float.IsNaN(v) || float.IsInfinity(v)) Fail(f + " must be finite."); }
        private static void Fail(string msg) => throw new InvalidOperationException("EventConfig: " + msg);
    }
}
