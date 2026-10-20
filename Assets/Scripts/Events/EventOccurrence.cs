using System.Collections.Generic;

namespace EsportsManager.Events
{
    /// <summary>
    /// One resolved effect of an occurrence (Story 006). <see cref="Value"/> is the rolled value (points, multiplier, or yuan for
    /// funds). <see cref="Applied"/> is what it actually did, after clamping:
    /// mood / stress / popularity / potential / familiarity = summed actual change over the affected players (pairs);
    /// club popularity = actual change; funds = signed yuan (+ income, - expense);
    /// train_multiplier = extra (or lost) gain on the target dimension, before clamping; train_add = the value;
    /// stream_multiplier / stream_add = extra (or lost) livestream yuan; rest_multiplier = extra relief in stress points before
    /// personality and R ((m - 1) x s_rest); business_multiplier = extra (or lost) club-share yuan;
    /// sponsor_chance_add = chance after - chance before; void_action / on_leave = 0.
    /// </summary>
    public sealed class EventEffectResult
    {
        /// <summary>Effect type.</summary>
        public EffectType Type;
        /// <summary>Target.</summary>
        public EffectTarget Target;
        /// <summary>Rolled value.</summary>
        public float Value;
        /// <summary>Actual result (see class remarks); 0 until applied.</summary>
        public float Applied;
        /// <summary>Players the effect touched (empty for club effects and action modifiers).</summary>
        public List<long> PlayerIds = new List<long>();

        internal EventEffectResult Clone() => new EventEffectResult { Type = Type, Target = Target, Value = Value };
    }

    /// <summary>
    /// One event that happened in a segment (for the settlement cards). Texts are in the event text table under
    /// <see cref="TitleKey"/> / <see cref="TextKey"/> ({player} = <see cref="PlayerId"/>, {partner} = <see cref="PartnerId"/>).
    /// Never carries true potential: potential effects report only the change.
    /// </summary>
    public sealed class EventOccurrence
    {
        /// <summary>Event id (event_config.json).</summary>
        public string EventId;
        /// <summary>Kind.</summary>
        public EventKind Kind;
        /// <summary>Good / bad / neutral (choice).</summary>
        public EventTone Tone;
        /// <summary>Trigger action (action events), None otherwise.</summary>
        public EventAction Action;
        /// <summary>Title text key.</summary>
        public string TitleKey;
        /// <summary>Body text key.</summary>
        public string TextKey;
        /// <summary>Subject player (null for club events).</summary>
        public long? PlayerId;
        /// <summary>Second player of a pair subject, else null.</summary>
        public long? PartnerId;
        /// <summary>Action events: index of the action point in the fixed draw order (assignments, accepted offers, sponsor points); -1 otherwise.</summary>
        public int PointIndex = -1;
        /// <summary>Choice events: the choice id.</summary>
        public int? ChoiceId;
        /// <summary>Choice events: option taken.</summary>
        public int? OptionTaken;
        /// <summary>Choice events: true when the player did not answer and the default option was applied.</summary>
        public bool OptionWasDefault;
        /// <summary>Choice events: label key of the option taken.</summary>
        public string OptionLabelKey;
        /// <summary>Resolved effects.</summary>
        public List<EventEffectResult> Effects = new List<EventEffectResult>();

        /// <summary>Signed yuan of all funds effects (after application).</summary>
        public long FundsDelta
        {
            get { long t = 0; foreach (var e in Effects) if (e.Type == EffectType.Funds) t += (long)e.Applied; return t; }
        }
    }

    /// <summary>One option of a pending choice, with its effect values already rolled (shown to the player).</summary>
    public sealed class ChoiceOptionView
    {
        /// <summary>Button label key.</summary>
        public string LabelKey;
        /// <summary>Effects with <see cref="EventEffectResult.Value"/> filled (Applied = 0).</summary>
        public List<EventEffectResult> Effects = new List<EventEffectResult>();
        /// <summary>True when this option puts the subject player on leave (he may get no action point).</summary>
        public bool PutsOnLeave
        {
            get { foreach (var e in Effects) if (e.Type == EffectType.OnLeave) return true; return false; }
        }
    }

    /// <summary>A choice event waiting for an answer in the open segment (answer through the plan before resolve).</summary>
    public sealed class PendingChoice
    {
        /// <summary>Unique for the runner's lifetime, never reused.</summary>
        public int Id;
        /// <summary>Event id.</summary>
        public string EventId;
        /// <summary>Title key.</summary>
        public string TitleKey;
        /// <summary>Body key.</summary>
        public string TextKey;
        /// <summary>Subject player, or null (team).</summary>
        public long? PlayerId;
        /// <summary>Second player of a pair subject, or null.</summary>
        public long? PartnerId;
        /// <summary>Option applied when unanswered.</summary>
        public int DefaultOption;
        /// <summary>Options in config order.</summary>
        public List<ChoiceOptionView> Options = new List<ChoiceOptionView>();
    }
}
