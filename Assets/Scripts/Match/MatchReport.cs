using System;
using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>
    /// The eight report components (design/gdd/match.md "战报数据" rule 1), each independent. Per game, Early + Mid + Late +
    /// Star + Cycle = Base, and Base + Tactic + Luck = G; Specialty is the separate "没有绝活会怎样" difference.
    /// </summary>
    public enum MatchFactor
    {
        /// <summary>Early phase strength gap: B_E x dS_E / sum B (no star, before the cycle).</summary>
        Early = 0,
        /// <summary>Mid phase strength gap (teamfights, includes chemistry).</summary>
        Mid = 1,
        /// <summary>Late phase strength gap.</summary>
        Late = 2,
        /// <summary>Star terms of early and late: sum B_k x eta x (star_A - star_B) / sum B.</summary>
        Star = 3,
        /// <summary>Three-phase cycle: the contribution lost to f_k &lt; 1, sum B_k x (f_k - 1) x Delta_k / sum B.</summary>
        Cycle = 4,
        /// <summary>Tactics: Fit_A - Fit_B + Counter.</summary>
        Tactic = 5,
        /// <summary>Specialties: (G0 - G0 with A's hooks neutral) + (G0 - G0 with B's hooks neutral).</summary>
        Specialty = 6,
        /// <summary>Form swing epsilon_A - epsilon_B (also the factor of a "luck" reason).</summary>
        Luck = 7,
    }

    /// <summary>
    /// One game reduced to what the reason rules need, from side A's perspective. Built from a <see cref="GameResult"/> or
    /// directly by tests (AC 40 fixture).
    /// </summary>
    public sealed class GameComponents
    {
        /// <summary>The eight component values (index = (int)MatchFactor), positive favours A.</summary>
        public double[] Values = new double[MatchReport.FactorCount];
        /// <summary>p0 of side A.</summary>
        public double P0 = 0.5;
        /// <summary>True when A won the game.</summary>
        public bool AWon;
        /// <summary>Cycle factors f_E, f_M, f_L.</summary>
        public double[] Cycle = { 1.0, 1.0, 1.0 };
        /// <summary>For each weakened phase Y: true when side A led the beating phase X (A benefited). Unknown = false.</summary>
        public bool[] CycleByA = new bool[MatchPhases.Count];

        /// <summary>Components of a played game.</summary>
        public static GameComponents Of(GameResult game, MatchConfig config)
        {
            var c = new GameComponents { P0 = game.P0, AWon = game.WinnerIsA };
            var e = game.Evaluation;
            if (e == null) return c;
            var st = config.strength;
            double sumB = e.WeightSum;
            foreach (var k in MatchPhases.All)
            {
                int ki = (int)k;
                c.Values[ki] = st.PhaseWeight(k) * e.DeltaStrength[ki] / sumB;
                c.Values[(int)MatchFactor.Star] += st.PhaseWeight(k) * st.starFactor * (e.StarA[ki] - e.StarB[ki]) / sumB;
                c.Values[(int)MatchFactor.Cycle] += st.PhaseWeight(k) * (e.Cycle[ki] - 1.0) * e.Delta[ki] / sumB;
                c.Cycle[ki] = e.Cycle[ki];
            }
            foreach (var rel in config.cycle.relations) c.CycleByA[(int)rel.weakens] = e.DeltaStrength[(int)rel.beats] > 0.0;
            c.Values[(int)MatchFactor.Tactic] = e.FitA - e.FitB + e.Counter;
            c.Values[(int)MatchFactor.Specialty] = (e.G0 - game.G0NoHooksA) + (e.G0 - game.G0NoHooksB);
            c.Values[(int)MatchFactor.Luck] = game.EpsilonA - game.EpsilonB;
            return c;
        }
    }

    /// <summary>
    /// One reason of a report: a factor summed over the series' games from the viewing side's perspective (positive helped
    /// the viewer), or a luck reason about one game. Text is the UI's job.
    /// </summary>
    public readonly struct MatchReason
    {
        /// <summary>Which factor.</summary>
        public readonly MatchFactor Factor;
        /// <summary>Signed sum in points (viewer minus other side). For a luck reason: that game's Luck component.</summary>
        public readonly float Value;
        /// <summary>True when <see cref="SubjectPlayerId"/> / <see cref="SubjectLaneId"/> are meaningful.</summary>
        public readonly bool HasSubject;
        /// <summary>The viewer's player the reason points at.</summary>
        public readonly long SubjectPlayerId;
        /// <summary>True when the subject's lane was ahead of the opposing lane in that phase.</summary>
        public readonly bool SubjectAhead;
        /// <summary>Lane id of the subject.</summary>
        public readonly string SubjectLaneId;
        /// <summary>True for a rule-3 luck reason about game <see cref="GameIndex"/>.</summary>
        public readonly bool IsLuckGame;
        /// <summary>0-based game index of a luck reason (-1 otherwise).</summary>
        public readonly int GameIndex;
        /// <summary>Luck reason: the loser's own p0.</summary>
        public readonly float LoserP0;
        /// <summary>Luck reason: true when the viewer was the unlucky favourite.</summary>
        public readonly bool ViewerUnlucky;

        /// <summary>Creates a factor reason.</summary>
        public MatchReason(MatchFactor factor, float value, bool hasSubject = false, long subjectPlayerId = 0, bool subjectAhead = false, string subjectLaneId = null)
        {
            Factor = factor; Value = value; HasSubject = hasSubject; SubjectPlayerId = subjectPlayerId; SubjectAhead = subjectAhead; SubjectLaneId = subjectLaneId;
            IsLuckGame = false; GameIndex = -1; LoserP0 = 0f; ViewerUnlucky = false;
        }

        /// <summary>Creates a luck reason (rule 3).</summary>
        public static MatchReason Luck(int gameIndex, float loserP0, bool viewerUnlucky, float value)
            => new MatchReason(gameIndex, loserP0, viewerUnlucky, value);

        private MatchReason(int gameIndex, float loserP0, bool viewerUnlucky, float value)
        {
            Factor = MatchFactor.Luck; Value = value; HasSubject = false; SubjectPlayerId = 0; SubjectAhead = false; SubjectLaneId = null;
            IsLuckGame = true; GameIndex = gameIndex; LoserP0 = loserP0; ViewerUnlucky = viewerUnlucky;
        }
    }

    /// <summary>Tactic counterfactual of one of side A's tactics: mean over the games of G0(t) - G0(actual) and p0(t) - p0(actual).</summary>
    public readonly struct TacticAlternative
    {
        /// <summary>The tactic.</summary>
        public readonly MatchTactic Tactic;
        /// <summary>Mean G0 gain in points (0 for the actual tactic).</summary>
        public readonly float GainPoints;
        /// <summary>Mean p0 gain (0.02 = 2 percentage points).</summary>
        public readonly float GainProbability;

        /// <summary>Creates an entry.</summary>
        public TacticAlternative(MatchTactic tactic, float gainPoints, float gainProbability) { Tactic = tactic; GainPoints = gainPoints; GainProbability = gainProbability; }
    }

    /// <summary>
    /// The structured "why" of a series (design/gdd/match.md "战报数据" rules 1-6): component sums, 2-3 reasons, the even flag,
    /// the cycle / early-end flag and the tactic advice. Contains NO text.
    /// </summary>
    /// <example><code>var report = MatchReport.Build(result, matchCfg, viewerIsA: true);
    /// foreach (var r in report.Reasons) ui.Show(r.Factor, r.Value);</code></example>
    public sealed class MatchReport
    {
        /// <summary>Number of components (8).</summary>
        public const int FactorCount = 8;

        /// <summary>True when the report is written from side A's point of view.</summary>
        public bool ViewerIsA { get; }
        /// <summary>Sum over the games of every component, viewer perspective, indexed by (int)MatchFactor.</summary>
        public IReadOnlyList<float> Totals { get; }
        /// <summary>The chosen reasons: a luck reason first when rule 3 applies, then factors (rules 1-2).</summary>
        public IReadOnlyList<MatchReason> Reasons { get; }
        /// <summary>Rule 4: the mean p0 of A was within 0.5 +- even_threshold.</summary>
        public bool IsEven { get; }
        /// <summary>Mean p0 of the viewer over the games.</summary>
        public float MeanP0 { get; }
        /// <summary>Rule 5: some game had a cycle factor below 1.</summary>
        public bool CycleTriggered { get; }
        /// <summary>When triggered: the weakened phase of the first such game (lowest f).</summary>
        public MatchPhase CyclePhase { get; }
        /// <summary>When triggered: true when the viewer led the beating phase (the viewer's lead weakened the other side).</summary>
        public bool CycleByViewer { get; }
        /// <summary>Counterfactual of side A's three tactics (empty when the viewer is B), enum order.</summary>
        public IReadOnlyList<TacticAlternative> Alternatives { get; }
        /// <summary>Rule 6: the advised tactic when the best tactic's mean G0 beats the actual one by >= tactic_advice_min, else null.</summary>
        public TacticAlternative? Advice { get; }

        private MatchReport(bool viewerIsA, float[] totals, List<MatchReason> reasons, bool even, float meanP0, bool cycle, MatchPhase cyclePhase,
            bool cycleViewer, List<TacticAlternative> alts, TacticAlternative? advice)
        {
            ViewerIsA = viewerIsA; Totals = totals; Reasons = reasons; IsEven = even; MeanP0 = meanP0; CycleTriggered = cycle;
            CyclePhase = cyclePhase; CycleByViewer = cycleViewer; Alternatives = alts; Advice = advice;
        }

        /// <summary>The per-game G parts recorded for AC 38 (side A): the three phase contributions, Fit_A, -Fit_B, Counter, epsilon_A, -epsilon_B.</summary>
        public static double[] GParts(GameResult game)
        {
            var e = game.Evaluation;
            return new[]
            {
                e.PhaseContribution[0], e.PhaseContribution[1], e.PhaseContribution[2], e.FitA, -e.FitB, e.Counter, game.EpsilonA, -game.EpsilonB,
            };
        }

        /// <summary>Builds the report of a played series (rules 1-6).</summary>
        public static MatchReport Build(MatchResult result, MatchConfig config, bool viewerIsA)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var games = new List<GameComponents>();
            foreach (var g in result.Games) games.Add(GameComponents.Of(g, config));
            var report = Select(games, result.WinnerIsA, viewerIsA, config.report, out var sums);

            var alts = new List<TacticAlternative>();
            TacticAlternative? advice = null;
            if (viewerIsA)
            {
                foreach (var t in MatchTactics.All)
                {
                    double dg = 0.0, dp = 0.0; int n = 0;
                    foreach (var g in result.Games)
                    {
                        if (g.Evaluation == null || g.TacticG0.Count <= (int)t) continue;
                        dg += g.TacticG0[(int)t] - g.Evaluation.G0;
                        dp += MatchFormulas.WinProbability(config.winProbability, g.TacticG0[(int)t]) - g.Evaluation.P0;
                        n++;
                    }
                    alts.Add(new TacticAlternative(t, n > 0 ? (float)(dg / n) : 0f, n > 0 ? (float)(dp / n) : 0f));
                }
                foreach (var a in alts)
                    if (a.GainPoints >= config.report.tacticAdviceMin && (advice == null || a.GainPoints > advice.Value.GainPoints)) advice = a;
            }
            var reasons = new List<MatchReason>();
            foreach (var r in report.Reasons)
                reasons.Add(r.IsLuckGame ? r : WithSubject(result, r, viewerIsA));
            return new MatchReport(viewerIsA, report.totals, reasons, report.even, report.meanP0, report.cycle, report.cyclePhase,
                report.cycleViewer, alts, advice);
        }

        /// <summary>
        /// Rules 1-5 on game components (pure; the AC 40 fixture calls it directly). Returns a report without subjects, tactic
        /// counterfactual or advice.
        /// </summary>
        public static MatchReport Select(IReadOnlyList<GameComponents> games, bool aWonSeries, bool viewerIsA, ReportConfig rc)
        {
            var r = Select(games, aWonSeries, viewerIsA, rc, out _);
            return new MatchReport(viewerIsA, r.totals, r.Reasons, r.even, r.meanP0, r.cycle, r.cyclePhase, r.cycleViewer,
                new List<TacticAlternative>(), null);
        }

        private sealed class Selection
        {
            public float[] totals; public List<MatchReason> Reasons; public bool even; public float meanP0;
            public bool cycle; public MatchPhase cyclePhase; public bool cycleViewer;
        }

        private static Selection Select(IReadOnlyList<GameComponents> games, bool aWonSeries, bool viewerIsA, ReportConfig rc, out double[] sums)
        {
            if (games == null) throw new ArgumentNullException(nameof(games));
            if (rc == null) throw new ArgumentNullException(nameof(rc));
            double sign = viewerIsA ? 1.0 : -1.0;
            sums = new double[FactorCount];
            double p0Sum = 0.0;
            int luckGame = -1; double luckP0 = -1.0;
            var sel = new Selection();
            for (int gi = 0; gi < games.Count; gi++)
            {
                var g = games[gi];
                for (int f = 0; f < FactorCount; f++) sums[f] += sign * g.Values[f];
                p0Sum += g.P0;
                // Rule 3: the loser's own p0 (A: p0, B: 1 - p0) >= luck_threshold. The largest one is reported (first on ties).
                double loserP0 = g.AWon ? 1.0 - g.P0 : g.P0;
                if (loserP0 >= rc.luckThreshold - 1e-12 && loserP0 > luckP0) { luckGame = gi; luckP0 = loserP0; }
                // Rule 5: first game with some f_k < 1; its weakest factor's phase.
                if (!sel.cycle)
                {
                    int low = -1;
                    for (int k = 0; k < MatchPhases.Count; k++) if (g.Cycle[k] < 1.0 && (low < 0 || g.Cycle[k] < g.Cycle[low])) low = k;
                    if (low >= 0) { sel.cycle = true; sel.cyclePhase = (MatchPhase)low; sel.cycleViewer = g.CycleByA[low] == viewerIsA; }
                }
            }
            double meanA = games.Count > 0 ? p0Sum / games.Count : 0.5;
            sel.meanP0 = (float)(viewerIsA ? meanA : 1.0 - meanA);
            sel.even = Math.Abs(meanA - 0.5) <= rc.evenThreshold + 1e-12; // rule 4
            sel.totals = new float[FactorCount];
            for (int f = 0; f < FactorCount; f++) sel.totals[f] = (float)sums[f];

            var reasons = new List<MatchReason>();
            if (luckGame >= 0)
            {
                bool viewerLost = games[luckGame].AWon != viewerIsA;
                reasons.Add(MatchReason.Luck(luckGame, (float)luckP0, viewerLost, (float)(sign * games[luckGame].Values[(int)MatchFactor.Luck])));
            }
            // Rules 1-2: factors in the series winner's favour with |sum| >= min_contribution, largest first (ties: enum order).
            var order = new List<int>();
            for (int f = 0; f < FactorCount; f++)
                if (!(luckGame >= 0 && f == (int)MatchFactor.Luck)) order.Add(f);
            var s = sums;
            for (int i = 1; i < order.Count; i++)
            {
                int x = order[i], j = i - 1;
                while (j >= 0 && Math.Abs(s[order[j]]) < Math.Abs(s[x])) { order[j + 1] = order[j]; j--; }
                order[j + 1] = x;
            }
            bool viewerWon = aWonSeries == viewerIsA;
            var used = new HashSet<int>();
            foreach (int f in order)
            {
                if (reasons.Count >= rc.maxReasons) break;
                bool forWinner = viewerWon ? s[f] > 0.0 : s[f] < 0.0;
                if (forWinner && Math.Abs(s[f]) >= rc.minContribution - 1e-12) { reasons.Add(new MatchReason((MatchFactor)f, (float)s[f])); used.Add(f); }
            }
            foreach (int f in order)
            {
                if (reasons.Count >= rc.minReasons) break;
                if (used.Add(f)) reasons.Add(new MatchReason((MatchFactor)f, (float)s[f]));
            }
            sel.Reasons = reasons;
            return sel;
        }

        // Subject of a factor reason from the first non-deciding game's evaluation.
        private static MatchReason WithSubject(MatchResult result, MatchReason reason, bool viewerIsA)
        {
            GameEvaluation e = null;
            foreach (var g in result.Games) if (g.Evaluation != null && !g.WasDecider) { e = g.Evaluation; break; }
            if (e == null && result.Games.Count > 0) e = result.Games[0].Evaluation;
            var own = viewerIsA ? result.SideA : result.SideB;
            var factor = reason.Factor; float value = reason.Value;
            if (e == null || own == null) return reason;
            double sign = viewerIsA ? 1.0 : -1.0;
            bool favours = value > 0f;
            switch (factor)
            {
                case MatchFactor.Early:
                case MatchFactor.Mid:
                case MatchFactor.Late:
                {
                    int k = (int)factor;
                    int n = e.A.Input.Ratings.Length;
                    int best = -1; double bestScore = 0.0;
                    for (int i = 0; i < n; i++)
                    {
                        double lead = sign * (e.A.Input.Ratings[i][k] - e.B.Input.Ratings[i][k]);
                        double score = favours ? lead : -lead;
                        if (best < 0 || score > bestScore) { best = i; bestScore = score; }
                    }
                    double subjectLead = favours ? bestScore : -bestScore;
                    return new MatchReason(factor, value, true, own.PlayerAt(best), subjectLead > 0.0, own.LaneOf(best));
                }
                case MatchFactor.Star:
                {
                    int E = (int)MatchPhase.Early, L = (int)MatchPhase.Late;
                    double vE = viewerIsA ? e.StarA[E] : e.StarB[E], vL = viewerIsA ? e.StarA[L] : e.StarB[L];
                    double oE = viewerIsA ? e.StarB[E] : e.StarA[E], oL = viewerIsA ? e.StarB[L] : e.StarA[L];
                    int lane;
                    if (favours) lane = vE >= vL ? (viewerIsA ? e.StarLaneA[E] : e.StarLaneB[E]) : (viewerIsA ? e.StarLaneA[L] : e.StarLaneB[L]);
                    else lane = oE >= oL ? (viewerIsA ? e.StarLaneB[E] : e.StarLaneA[E]) : (viewerIsA ? e.StarLaneB[L] : e.StarLaneA[L]);
                    return new MatchReason(factor, value, true, own.PlayerAt(lane), favours, own.LaneOf(lane));
                }
                default:
                    return reason;
            }
        }
    }
}
