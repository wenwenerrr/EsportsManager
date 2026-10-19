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
    /// Maps a structured <see cref="MatchReason"/> to one plain Chinese sentence that needs no esports knowledge. Pure text
    /// selection: every judgement (sign, <see cref="MatchReason.IsEven"/>, <see cref="MatchReason.SubjectAhead"/>) comes from
    /// the logic layer.
    /// </summary>
    public static class ReasonText
    {
        /// <summary>The sentence for one reason.</summary>
        /// <param name="reason">The reason (viewer = player club).</param>
        /// <param name="subject">Display data of the subject player (ignored when the reason has none).</param>
        /// <param name="ourTactic">Player club tactic in this series.</param>
        /// <param name="theirTactic">Opponent tactic in this series.</param>
        public static string For(MatchReason reason, ReasonSubject subject, MatchTactic ourTactic, MatchTactic theirTactic)
        {
            bool even = reason.IsEven;
            bool us = reason.Value > 0f;
            bool hasSubject = reason.HasSubject && subject.IsSet;
            bool ahead = reason.SubjectAhead;
            string who = hasSubject ? string.Format(UiStrings.SubjectFormat, subject.Name, subject.Lane) : "";
            switch (reason.Factor)
            {
                case MatchFactor.Skill:
                    if (even) return UiStrings.ReasonSkillEven;
                    if (us)
                        return !hasSubject ? UiStrings.ReasonSkillUs
                            : string.Format(ahead ? UiStrings.ReasonSkillUsSubjectAhead : UiStrings.ReasonSkillUsSubjectBehind, who);
                    return !hasSubject ? UiStrings.ReasonSkillThem
                        : string.Format(ahead ? UiStrings.ReasonSkillThemSubjectAhead : UiStrings.ReasonSkillThemSubjectBehind, who);
                case MatchFactor.Chemistry:
                    return even ? UiStrings.ReasonChemistryEven : us ? UiStrings.ReasonChemistryUs : UiStrings.ReasonChemistryThem;
                case MatchFactor.Style:
                    return even ? UiStrings.ReasonStyleEven : us ? UiStrings.ReasonStyleUs : UiStrings.ReasonStyleThem;
                case MatchFactor.Tactic:
                    if (even) return UiStrings.ReasonTacticEven;
                    return string.Format(us ? UiStrings.ReasonTacticUs : UiStrings.ReasonTacticThem,
                        UiStrings.TacticName(ourTactic), UiStrings.TacticName(theirTactic));
                case MatchFactor.StressVolatility:
                    if (even) return UiStrings.ReasonStressEven;
                    if (us) return UiStrings.ReasonStressUs;
                    return hasSubject ? string.Format(UiStrings.ReasonStressThemSubject, who) : UiStrings.ReasonStressThem;
                case MatchFactor.OpponentForm:
                    // Negative = the opponent's form helped them (hot); positive = they were in a slump.
                    return even ? UiStrings.ReasonFormEven : us ? UiStrings.ReasonFormSlump : UiStrings.ReasonFormHot;
                default:
                    return UiStrings.ReasonSkillEven;
            }
        }
    }
}
