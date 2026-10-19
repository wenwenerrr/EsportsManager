using System;
using System.Collections.Generic;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>The factors a report compares between the two sides.</summary>
    public enum MatchFactor
    {
        /// <summary>Skill score gap (effective ratings, short board).</summary>
        Skill = 0,
        /// <summary>Team chemistry gap.</summary>
        Chemistry = 1,
        /// <summary>Style score gap (commander, lane match).</summary>
        Style = 2,
        /// <summary>Tactic counter: positive when our tactic counters theirs, negative when theirs counters ours.</summary>
        Tactic = 3,
        /// <summary>Stress volatility: the gap in per-game noise half-width, in power points. Negative when we swing more than they do.</summary>
        StressVolatility = 4,
        /// <summary>
        /// Form gap (numbers.md 4.14 v3.3), split out of the skill gap: weightSkill/sum x (viewer form - other form). With the
        /// player as viewer (form 0) a NEGATIVE value means the opponent was in hot form, a positive one that it was in a slump.
        /// Listed only when |Value| >= report.min_contribution (it never fills the minimum count).
        /// </summary>
        OpponentForm = 5,
    }

    /// <summary>
    /// One reason of a report. <see cref="Value"/> is the contribution to the power gap from the viewing side's
    /// perspective, in overall-power points: positive helped the viewing side, negative hurt it. Text is the UI's job.
    /// </summary>
    public readonly struct MatchReason
    {
        /// <summary>Which factor.</summary>
        public readonly MatchFactor Factor;
        /// <summary>Signed contribution in power points (viewing side minus other side).</summary>
        public readonly float Value;
        /// <summary>True when <see cref="SubjectPlayerId"/> is meaningful (a viewing-side player the factor points at).</summary>
        public readonly bool HasSubject;
        /// <summary>The viewing side's player this reason is about (the lane with the biggest rating gap for Skill, the most stressed starter for StressVolatility).</summary>
        public readonly long SubjectPlayerId;
        /// <summary>Skill only: the subject's effective rating beat the opposing player of the SAME lane (matched by lane id).</summary>
        public readonly bool SubjectAhead;
        /// <summary>|Value| is below report.even_threshold: neither side gained from this factor.</summary>
        public readonly bool IsEven;

        /// <summary>Creates a reason.</summary>
        public MatchReason(MatchFactor factor, float value, bool hasSubject, long subjectPlayerId, bool subjectAhead = false, bool isEven = false)
        {
            Factor = factor; Value = value; HasSubject = hasSubject; SubjectPlayerId = subjectPlayerId;
            SubjectAhead = subjectAhead; IsEven = isEven;
        }
    }

    /// <summary>
    /// The structured "why" of a series result (Story 004): the 2-3 factors (count from config) with the largest
    /// absolute contribution to the pre-game power gap, for either a win or a loss. Contains NO text.
    /// </summary>
    /// <example><code>var report = MatchReport.Build(result, teamCfg, matchCfg, viewerIsA: true);
    /// foreach (var r in report.Reasons) ui.Show(r.Factor, r.Value, r.SubjectPlayerId);</code></example>
    public sealed class MatchReport
    {
        /// <summary>True when the report is written from side A's point of view.</summary>
        public bool ViewerIsA { get; }
        /// <summary>Overall power of the viewing side minus the other side (before noise); equals the sum of the Skill, Chemistry, Style, Tactic and OpponentForm values (StressVolatility is spread, not mean power, so it is not part of the gap).</summary>
        public float PowerGap { get; }
        /// <summary>The chosen reasons, largest |Value| first (ties by enum order).</summary>
        public IReadOnlyList<MatchReason> Reasons { get; }

        private MatchReport(bool viewerIsA, float gap, IReadOnlyList<MatchReason> reasons)
        {
            ViewerIsA = viewerIsA; PowerGap = gap; Reasons = reasons;
        }

        /// <summary>Compares the sides and picks the reasons; always returns between min_reasons and max_reasons of them.</summary>
        public static MatchReport Build(MatchResult result, TeamConfig team, MatchConfig config, bool viewerIsA)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (team == null) throw new ArgumentNullException(nameof(team));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var own = viewerIsA ? result.SideA : result.SideB;
            var opp = viewerIsA ? result.SideB : result.SideA;
            var e = own.Evaluation;
            var o = opp.Evaluation;
            var w = config.power;
            float sum = w.weightSkill + w.weightChemistry + w.weightStyle;

            // Skill subject: the lane whose weighted rating gap is largest in absolute value.
            // Lanes are matched by lane id (not by list position).
            bool hasSkillSubject = false, skillAhead = false; long skillSubject = 0; float bestGap = -1f;
            float laneWeightSum = 0f;
            foreach (var p in own.Participants) laneWeightSum += team.GetLaneWeight(p.LaneId);
            foreach (var mine in own.Participants)
            {
                bool found = false; MatchParticipant theirs = default;
                foreach (var q in opp.Participants) if (q.LaneId == mine.LaneId) { theirs = q; found = true; break; }
                if (!found) continue;
                float signed = mine.EffectiveRating - theirs.EffectiveRating;
                float gap = Math.Abs(signed) * team.GetLaneWeight(mine.LaneId) / (laneWeightSum > 0f ? laneWeightSum : 1f);
                if (gap > bestGap) { bestGap = gap; skillSubject = mine.PlayerId; hasSkillSubject = true; skillAhead = signed > 0f; }
            }

            float volatility = (o.Sigma - e.Sigma) * 0.5f * (e.Power + o.Power);
            bool hasStressSubject = false; long stressSubject = 0;
            if (volatility < 0f)
            {
                float top = float.MinValue;
                foreach (var p in own.Participants)
                    if (p.Stress > top) { top = p.Stress; stressSubject = p.PlayerId; hasStressSubject = true; }
            }

            float form = w.weightSkill / sum * (e.Form - o.Form);
            float even = config.report.evenThreshold;
            float skill = w.weightSkill / sum * (e.Skill - o.Skill) - form;
            float chem = w.weightChemistry / sum * (e.Chemistry - o.Chemistry);
            float style = w.weightStyle / sum * (e.Style - o.Style);
            float tac = e.TacticBonus - o.TacticBonus;
            var all = new List<MatchReason>
            {
                new MatchReason(MatchFactor.Skill, skill, hasSkillSubject, skillSubject, skillAhead, Math.Abs(skill) < even),
                new MatchReason(MatchFactor.Chemistry, chem, false, 0, false, Math.Abs(chem) < even),
                new MatchReason(MatchFactor.Style, style, false, 0, false, Math.Abs(style) < even),
                new MatchReason(MatchFactor.Tactic, tac, false, 0, false, Math.Abs(tac) < even),
                new MatchReason(MatchFactor.StressVolatility, volatility, hasStressSubject, stressSubject, false, Math.Abs(volatility) < even),
            };
            if (Math.Abs(form) >= config.report.minContribution && form != 0f)
                all.Add(new MatchReason(MatchFactor.OpponentForm, form, false, 0, false, Math.Abs(form) < even));
            // Stable insertion sort by |Value| descending (List.Sort is unstable).
            for (int i = 1; i < all.Count; i++)
            {
                var x = all[i]; int j = i - 1;
                while (j >= 0 && Math.Abs(all[j].Value) < Math.Abs(x.Value)) { all[j + 1] = all[j]; j--; }
                all[j + 1] = x;
            }
            var chosen = new List<MatchReason>();
            for (int i = 0; i < all.Count && chosen.Count < config.report.maxReasons; i++)
            {
                if (chosen.Count >= config.report.minReasons && Math.Abs(all[i].Value) < config.report.minContribution) break;
                chosen.Add(all[i]);
            }
            return new MatchReport(viewerIsA, e.Power - o.Power, chosen);
        }
    }
}
