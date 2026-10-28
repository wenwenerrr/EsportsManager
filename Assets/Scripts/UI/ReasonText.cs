using System;
using EsportsManager.Matches;

namespace EsportsManager.UI
{
    /// <summary>Display data of a report reason's subject player (name and lane name). Empty name = no subject.</summary>
    public readonly struct ReasonSubject
    {
        /// <summary>Player name.</summary>
        public readonly string Name;
        /// <summary>Lane display name.</summary>
        public readonly string Lane;

        /// <summary>Creates a subject.</summary>
        public ReasonSubject(string name, string lane) { Name = name; Lane = lane; }

        /// <summary>True when there is a usable name.</summary>
        public bool IsSet => !string.IsNullOrEmpty(Name);
    }

    /// <summary>
    /// Maps a structured <see cref="MatchReason"/> / <see cref="MatchReport"/> to plain Chinese sentences that need no esports
    /// knowledge (design/gdd/match.md UI Requirements). Every reason sentence names the factor it comes from.
    /// </summary>
    public static class ReasonText
    {
        /// <summary>|value| below this many points reads as "even" (display rounding: it would print as 0.0).</summary>
        public const float DisplayEpsilon = 0.05f;

        /// <summary>The sentence for one reason.</summary>
        /// <param name="reason">The reason (viewer = player club).</param>
        /// <param name="subject">Display data of the subject player (ignored when the reason has none).</param>
        /// <param name="ourTactic">Player club tactic in this series.</param>
        /// <param name="theirTactic">Opponent tactic in this series.</param>
        public static string For(MatchReason reason, ReasonSubject subject, MatchTactic ourTactic, MatchTactic theirTactic)
        {
            bool even = Math.Abs(reason.Value) < DisplayEpsilon;
            bool us = reason.Value > 0f;
            bool has = reason.HasSubject && subject.IsSet;
            string who = has ? string.Format(UiStrings.SubjectFormat, subject.Name, subject.Lane) : "";
            string ours = UiStrings.TacticName(ourTactic), theirs = UiStrings.TacticName(theirTactic);
            switch (reason.Factor)
            {
                case MatchFactor.Early:
                    return even ? UiStrings.ReasonEarlyEven : Pick(us, has, who, UiStrings.ReasonEarlyUs, UiStrings.ReasonEarlyUsSubject, UiStrings.ReasonEarlyThem, UiStrings.ReasonEarlyThemSubject);
                case MatchFactor.Mid:
                    return even ? UiStrings.ReasonMidEven : Pick(us, has, who, UiStrings.ReasonMidUs, UiStrings.ReasonMidUsSubject, UiStrings.ReasonMidThem, UiStrings.ReasonMidThemSubject);
                case MatchFactor.Late:
                    return even ? UiStrings.ReasonLateEven : Pick(us, has, who, UiStrings.ReasonLateUs, UiStrings.ReasonLateUsSubject, UiStrings.ReasonLateThem, UiStrings.ReasonLateThemSubject);
                case MatchFactor.Star:
                    return even ? UiStrings.ReasonStarEven : Pick(us, has, who, UiStrings.ReasonStarUs, UiStrings.ReasonStarUsSubject, UiStrings.ReasonStarThem, UiStrings.ReasonStarThemSubject);
                case MatchFactor.Cycle:
                    return even ? UiStrings.ReasonCycleEven : us ? UiStrings.ReasonCycleUs : UiStrings.ReasonCycleThem;
                case MatchFactor.Tactic:
                    return even ? UiStrings.ReasonTacticEven : string.Format(us ? UiStrings.ReasonTacticUs : UiStrings.ReasonTacticThem, "", ours, theirs);
                case MatchFactor.Specialty:
                    return even ? UiStrings.ReasonSpecialtyEven : us ? UiStrings.ReasonSpecialtyUs : UiStrings.ReasonSpecialtyThem;
                case MatchFactor.Luck:
                    if (reason.IsLuckGame)
                        return string.Format(reason.ViewerUnlucky ? UiStrings.LuckUnluckyFormat : UiStrings.LuckLuckyFormat, reason.GameIndex + 1,
                            Math.Round(reason.LoserP0 * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                    return even ? UiStrings.ReasonLuckEven : us ? UiStrings.ReasonLuckUs : UiStrings.ReasonLuckThem;
                default:
                    return UiStrings.ReasonEarlyEven;
            }
        }

        /// <summary>The reason sentence with its signed value appended, e.g. "…（+2.3 分）".</summary>
        public static string WithValue(string sentence, float value)
            => sentence + string.Format(UiStrings.ReasonValueFormat, (value >= 0f ? "+" : "") + value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// The report notes in display order: the cycle / early-end note (rule 5), the even flag (rule 4) and the tactic advice
        /// ("换成X本可+Y", rule 6). Luck is a reason (rule 3), not a note.
        /// </summary>
        public static System.Collections.Generic.List<string> Notes(MatchReport report)
        {
            var list = new System.Collections.Generic.List<string>();
            if (report == null) return list;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (report.CycleTriggered)
            {
                bool v = report.CycleByViewer;
                switch (report.CyclePhase)
                {
                    case MatchPhase.Late: list.Add(v ? UiStrings.CycleEarlyEndUs : UiStrings.CycleEarlyEndThem); break;
                    case MatchPhase.Early: list.Add(v ? UiStrings.CycleTeamfightUs : UiStrings.CycleTeamfightThem); break;
                    default: list.Add(v ? UiStrings.CycleLateUs : UiStrings.CycleLateThem); break;
                }
            }
            if (report.IsEven) list.Add(string.Format(UiStrings.EvenFormat, Math.Round(report.MeanP0 * 100f).ToString("0", inv)));
            if (report.Advice.HasValue)
            {
                var a = report.Advice.Value;
                list.Add(string.Format(UiStrings.TacticBetterFormat, UiStrings.TacticName(a.Tactic), a.GainPoints.ToString("0.0", inv),
                    (a.GainProbability * 100f).ToString("0.0", inv)));
            }
            return list;
        }

        private static string Pick(bool us, bool has, string who, string usPlain, string usSubject, string themPlain, string themSubject)
        {
            if (us) return has ? string.Format(usSubject, who) : usPlain;
            return has ? string.Format(themSubject, who) : themPlain;
        }
    }
}
