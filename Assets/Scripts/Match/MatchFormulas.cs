using System;
using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>
    /// The noise-free evaluation of one game between two sides (design/gdd/match.md evaluation steps 3-10 and 12 without the
    /// noise): phase gaps with the star terms, early-end factor, base score, tactic fit, counter, G0 and p0. All phase arrays are
    /// indexed by (int)MatchPhase; positive values favour side A.
    /// </summary>
    public sealed class GameEvaluation
    {
        /// <summary>Side A on its own.</summary>
        public SideEvaluation A;
        /// <summary>Side B on its own.</summary>
        public SideEvaluation B;
        /// <summary>dS_k = S_A,k - S_B,k (no star term; F7 uses it for the cycle).</summary>
        public double[] DeltaStrength = new double[MatchPhases.Count];
        /// <summary>Delta_k = dS_k + eta x (star_A,k - star_B,k) (F6).</summary>
        public double[] Delta = new double[MatchPhases.Count];
        /// <summary>star_A,k after star_scale and the cap (0 for mid).</summary>
        public double[] StarA = new double[MatchPhases.Count];
        /// <summary>star_B,k after star_scale and the cap (0 for mid).</summary>
        public double[] StarB = new double[MatchPhases.Count];
        /// <summary>Lane index of A's biggest lead / B's biggest lead per phase (the star lanes).</summary>
        public int[] StarLaneA = new int[MatchPhases.Count], StarLaneB = new int[MatchPhases.Count];
        /// <summary>Cycle factors f_E, f_M, f_L (F7).</summary>
        public double[] Cycle = { 1.0, 1.0, 1.0 };
        /// <summary>Sum of B_k (the fixed denominator of F8).</summary>
        public double WeightSum;
        /// <summary>B_k x f_k x Delta_k / sum B: each phase's share of the base score (includes its star part).</summary>
        public double[] PhaseContribution = new double[MatchPhases.Count];
        /// <summary>B_k x f_k x eta x (star_A,k - star_B,k) / sum B: the star part inside <see cref="PhaseContribution"/>.</summary>
        public double[] StarContribution = new double[MatchPhases.Count];
        /// <summary>Base score (F8).</summary>
        public double Base;
        /// <summary>Fit_A (F9).</summary>
        public double FitA;
        /// <summary>Fit_B (F9, from B's point of view; G uses -Fit_B).</summary>
        public double FitB;
        /// <summary>Counter term (F10).</summary>
        public double Counter;
        /// <summary>G0 = Base + Fit_A - Fit_B + Counter (F12 without noise).</summary>
        public double G0;
        /// <summary>p0 = F13(G0).</summary>
        public double P0;

        /// <summary>True when some cycle factor is below 1 (report rule 5).</summary>
        public bool CycleTriggered => Cycle[0] < 1.0 || Cycle[1] < 1.0 || Cycle[2] < 1.0;
    }

    /// <summary>
    /// Pure formulas F1-F14 of design/gdd/match.md (rev 4). No state, no randomness. Double precision throughout.
    /// </summary>
    public static class MatchFormulas
    {
        // ------------------------------------------------------------ F1

        /// <summary>mood_f = m0 + (1 - m0) x mood/100 (0.95-1.00).</summary>
        public static double MoodFactor(EffectiveConfig c, double mood) => c.moodFloor + (1.0 - c.moodFloor) * mood / 100.0;

        /// <summary>stress_f = 1 - kappa_s x stress/100 x res (0.975-1.00).</summary>
        public static double StressFactor(EffectiveConfig c, double stress, double resolute) => 1.0 - c.stressFactor * stress / 100.0 * resolute;

        /// <summary>lane_f: 1 on the main lane, else min(1, off_lane_discount x clever multiplier) (0.8 / 0.824).</summary>
        public static double LaneFactor(bool onMainLane, double offLaneDiscount, double offLaneMultiplier)
        {
            if (onMainLane) return 1.0;
            double f = offLaneDiscount * offLaneMultiplier;
            return f > 1.0 ? 1.0 : f;
        }

        /// <summary>F1: v' = max(0, v x mood_f x stress_f x lane_f).</summary>
        public static double EffectiveValue(EffectiveConfig c, double v, double mood, double stress, double resolute, double laneFactor)
        {
            double r = v * MoodFactor(c, mood) * StressFactor(c, stress, resolute) * laneFactor;
            return r < 0.0 ? 0.0 : r;
        }

        // ------------------------------------------------------------ F2 / F3 / F4

        /// <summary>F2: r(i,k) = mean of the phase's effective dimension values (0 for an empty list).</summary>
        public static double PhaseRating(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double s = 0.0;
            for (int i = 0; i < values.Count; i++) s += values[i];
            return s / values.Count;
        }

        /// <summary>WS_k = sum(Lw x r) / sum(Lw).</summary>
        public static double WeightedMean(IReadOnlyList<double> ratings, IReadOnlyList<double> weights)
        {
            double s = 0.0, w = 0.0;
            for (int i = 0; i < ratings.Count; i++) { s += weights[i] * ratings[i]; w += weights[i]; }
            return w > 0.0 ? s / w : 0.0;
        }

        /// <summary>F3: S_k = WS_k - lambda_k x (WS_k - min r).</summary>
        public static double PhaseStrength(IReadOnlyList<double> ratings, IReadOnlyList<double> weights, double lambda)
        {
            double ws = WeightedMean(ratings, weights);
            double min = double.MaxValue;
            for (int i = 0; i < ratings.Count; i++) if (ratings[i] < min) min = ratings[i];
            if (ratings.Count == 0) return 0.0;
            return ws - lambda * (ws - min);
        }

        /// <summary>F4: mid chemistry bonus chi x (chem - 50) / 10 (-5..+5 at chi 1).</summary>
        public static double MidChemistryBonus(PhaseStrengthConfig s, double chemistry) => s.midChemistry * (chemistry - 50.0) / 10.0;

        // ------------------------------------------------------------ F5

        /// <summary>F5: cmd = min(100, max_i(v'_cmd,i x asr_i)); 0 for no starters.</summary>
        public static double CommandScore(IReadOnlyList<double> effectiveCommand, IReadOnlyList<double> assertive)
        {
            double best = 0.0;
            for (int i = 0; i < effectiveCommand.Count; i++)
            {
                double v = effectiveCommand[i] * assertive[i];
                if (v > best) best = v;
            }
            return best > 100.0 ? 100.0 : best;
        }

        /// <summary>F5: exec = clamp(e0 + e1 x chem/100 + e2 x cmd/100 + exec_add, min, max).</summary>
        public static double Execution(ExecutionConfig e, double chemistry, double command, double execAdd)
            => Clamp(e.e0 + e.e1 * chemistry / 100.0 + e.e2 * command / 100.0 + execAdd, e.min, e.max);

        // ------------------------------------------------------------ F6 / F7 / F8

        /// <summary>
        /// F6 star terms from the per-lane leads (A - B), each min(cap, max(0, biggest lead - max(0, mean lead) - threshold) x scale)
        /// (scale first, then the cap). The lanes of the biggest leads are returned too.
        /// </summary>
        public static void Stars(IReadOnlyList<double> leads, double threshold, double cap, double scaleA, double scaleB,
            out double starA, out double starB, out int laneA, out int laneB)
        {
            starA = starB = 0.0; laneA = laneB = 0;
            if (leads == null || leads.Count == 0) return;
            double mean = 0.0, maxA = double.MinValue, maxB = double.MinValue;
            for (int i = 0; i < leads.Count; i++)
            {
                mean += leads[i];
                if (leads[i] > maxA) { maxA = leads[i]; laneA = i; }
                if (-leads[i] > maxB) { maxB = -leads[i]; laneB = i; }
            }
            mean /= leads.Count;
            starA = Math.Min(cap, Math.Max(0.0, maxA - Math.Max(0.0, mean) - threshold) * scaleA);
            starB = Math.Min(cap, Math.Max(0.0, maxB - Math.Max(0.0, -mean) - threshold) * scaleB);
        }

        /// <summary>F7: f_Y = max(w_min, 1 - s x |dS_X| / 10) when dS_X x Delta_Y &lt; 0 (dS_X without star, Delta_Y with), else 1.</summary>
        public static double CycleFactor(CycleConfig c, double deltaStrengthX, double deltaY)
        {
            if (!(deltaStrengthX * deltaY < 0.0)) return 1.0;
            return Math.Max(c.weightMin, 1.0 - c.strength * Math.Abs(deltaStrengthX) / 10.0);
        }

        // ------------------------------------------------------------ F9 / F10 / F11 / F13 / F14

        /// <summary>F9: clamp(a x exec x focusDelta, -F, +F). Pass Delta for A and -Delta for B.</summary>
        public static double Fit(TacticConfig t, double execution, double focusDelta) => Clamp(t.fitScale * execution * focusDelta, -t.fitCap, t.fitCap);

        /// <summary>F10: C x exec_A when A counters B, minus C x exec_B when B counters A, else 0.</summary>
        public static double Counter(MatchConfig c, MatchTactic tacticA, MatchTactic tacticB, double execA, double execB)
        {
            double v = 0.0;
            if (c.Counters(tacticA, tacticB)) v += c.tactic.counter * execA;
            if (c.Counters(tacticB, tacticA)) v -= c.tactic.counter * execB;
            return v;
        }

        /// <summary>F11: sigma = (g0 + g1 x avgStress/100 x avgRes) x (1 - c_cmd x cmd/100) x sigma_scale.</summary>
        public static double Sigma(NoiseConfig n, double averageStress, double averageResolute, double command, double sigmaScale)
            => (n.g0 + n.g1 * averageStress / 100.0 * averageResolute) * (1.0 - n.commandReduction * command / 100.0) * sigmaScale;

        /// <summary>F11: epsilon = sigma x (2u - 1); u = 0 gives -sigma, u = 0.5 gives 0.</summary>
        public static double Epsilon(double sigma, double u) => sigma * (2.0 * u - 1.0);

        /// <summary>F13: clamp(1 / (1 + e^(-G/k)), p_floor, 1 - p_floor). Never NaN.</summary>
        public static double WinProbability(WinProbabilityConfig w, double g)
        {
            double p = 1.0 / (1.0 + Math.Exp(-g / w.k));
            if (double.IsNaN(p)) p = g > 0 ? 1.0 : 0.0;
            return Clamp(p, w.pFloor, 1.0 - w.pFloor);
        }

        /// <summary>F14: dec = mean of the starters' decider multipliers.</summary>
        public static double DeciderFactor(IReadOnlyList<double> multipliers)
        {
            if (multipliers == null || multipliers.Count == 0) return 1.0;
            double s = 0.0;
            foreach (var m in multipliers) s += m;
            return s / multipliers.Count;
        }

        // ------------------------------------------------------------ side and game

        /// <summary>
        /// Steps 1-3 (and the F5 command score, F11 averages) for a locked lineup: every starter's effective dimension values (F1),
        /// times the side's decider coefficient when <paramref name="decider"/> (F14, step 2), averaged per phase (F2). The lineup
        /// must be complete (<see cref="RequireFullLineup"/>).
        /// </summary>
        public static SideInput BuildSide(MatchConfig config, PhaseLayout layout, MatchLineup lineup, bool decider)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (lineup == null) throw new ArgumentNullException(nameof(lineup));
            int n = layout.LaneCount;
            var decs = new double[n];
            for (int i = 0; i < n; i++) decs[i] = lineup.Starters[layout.LaneIds[i]].Decider;
            double dec = decider ? DeciderFactor(decs) : 1.0;

            var input = new SideInput { Ratings = new double[n][], Chemistry = lineup.Chemistry, Effects = lineup.Effects };
            var cmd = new double[n];
            var asr = new double[n];
            double stress = 0.0, res = 0.0;
            for (int i = 0; i < n; i++)
            {
                var s = lineup.Starters[layout.LaneIds[i]];
                double laneF = LaneFactor(s.OnMainLane, layout.OffLaneDiscount, s.OffLaneMultiplier);
                input.Ratings[i] = new double[MatchPhases.Count];
                foreach (var k in MatchPhases.All)
                {
                    var idx = layout.DimIndexes[i][(int)k];
                    var vals = new double[idx.Length];
                    for (int j = 0; j < idx.Length; j++)
                        vals[j] = EffectiveValue(config.effective, s.Dimensions[idx[j]], s.Mood, s.Stress, s.Resolute, laneF) * dec;
                    input.Ratings[i][(int)k] = PhaseRating(vals);
                }
                // F5 / F14: the command value is never multiplied by the decider coefficient.
                cmd[i] = EffectiveValue(config.effective, s.Dimensions[layout.CommandIndex[i]], s.Mood, s.Stress, s.Resolute, laneF);
                asr[i] = s.Assertive;
                stress += s.Stress;
                res += s.Resolute;
            }
            input.Command = CommandScore(cmd, asr);
            input.AverageStress = stress / n;
            input.AverageResolute = res / n;
            return input;
        }

        /// <summary>
        /// Steps 4-7 and the side's sigma: S_k (F3, per-phase short board), mid chemistry (F4), phase_flat (step 6), execution
        /// (F5), sigma (F11), weakest lane per phase. <paramref name="neutralHooks"/> evaluates as if the hook table were empty
        /// (the report's specialty counterfactual).
        /// </summary>
        public static SideEvaluation EvaluateSide(MatchConfig config, PhaseLayout layout, SideInput input, MatchTactic tactic, bool neutralHooks = false)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (input == null) throw new ArgumentNullException(nameof(input));
            var hooks = neutralHooks ? HookTotals.Neutral : HookTotals.Aggregate(input.Effects, config.specialty);
            var ev = new SideEvaluation { Hooks = hooks, Tactic = tactic, Input = input, Command = input.Command };
            int n = input.Ratings.Length;
            ev.MidChemistryBonus = MidChemistryBonus(config.strength, input.Chemistry);
            foreach (var k in MatchPhases.All)
            {
                var r = new double[n];
                int weakest = 0;
                for (int i = 0; i < n; i++)
                {
                    r[i] = input.Ratings[i][(int)k];
                    if (r[i] < r[weakest]) weakest = i;
                }
                var w = layout.LaneWeights[(int)k];
                double s = PhaseStrength(r, w, config.strength.ShortBoard(k));
                if (k == MatchPhase.Mid) s += ev.MidChemistryBonus;
                s += hooks.Flat(k);
                ev.Strength[(int)k] = s;
                ev.WeightedMean[(int)k] = WeightedMean(r, w);
                ev.WeakestLane[(int)k] = weakest;
            }
            ev.Execution = Execution(config.execution, input.Chemistry, input.Command, hooks.ExecAdd);
            ev.Sigma = Sigma(config.noise, input.AverageStress, input.AverageResolute, input.Command, hooks.SigmaScale);
            return ev;
        }

        /// <summary>
        /// Steps 8-10 and the noise-free part of 12: phase gaps with stars (F6), cycle factors (F7), the fixed-denominator base
        /// (F8), fit (F9), counter (F10), G0 and p0 (F13).
        /// </summary>
        public static GameEvaluation EvaluateGame(MatchConfig config, SideEvaluation a, SideEvaluation b)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            var g = new GameEvaluation { A = a, B = b };
            int n = a.Input.Ratings.Length;
            var st = config.strength;
            double eta = st.starFactor;
            foreach (var k in MatchPhases.All)
            {
                int ki = (int)k;
                double ds = a.Strength[ki] - b.Strength[ki];
                g.DeltaStrength[ki] = ds;
                double d = ds;
                if (k != MatchPhase.Mid)
                {
                    var leads = new double[n];
                    for (int i = 0; i < n; i++) leads[i] = a.Input.Ratings[i][ki] - b.Input.Ratings[i][ki];
                    Stars(leads, st.starThreshold, st.starCap, a.Hooks.StarScale(k), b.Hooks.StarScale(k),
                        out double sa, out double sb, out int la, out int lb);
                    g.StarA[ki] = sa; g.StarB[ki] = sb;
                    g.StarLaneA[ki] = la; g.StarLaneB[ki] = lb;
                    d += eta * (sa - sb);
                }
                g.Delta[ki] = d;
            }
            foreach (var rel in config.cycle.relations)
                g.Cycle[(int)rel.weakens] = CycleFactor(config.cycle, g.DeltaStrength[(int)rel.beats], g.Delta[(int)rel.weakens]);
            double sumB = 0.0;
            foreach (var k in MatchPhases.All) sumB += st.PhaseWeight(k);
            g.WeightSum = sumB;
            foreach (var k in MatchPhases.All)
            {
                int ki = (int)k;
                double w = st.PhaseWeight(k) * g.Cycle[ki];
                g.PhaseContribution[ki] = w * g.Delta[ki] / sumB;
                g.StarContribution[ki] = w * eta * (g.StarA[ki] - g.StarB[ki]) / sumB;
                g.Base += g.PhaseContribution[ki];
            }
            g.FitA = Fit(config.tactic, a.Execution, g.Delta[(int)config.tactic.FocusOf(a.Tactic)]);
            g.FitB = Fit(config.tactic, b.Execution, -g.Delta[(int)config.tactic.FocusOf(b.Tactic)]);
            g.Counter = Counter(config, a.Tactic, b.Tactic, a.Execution, b.Execution);
            g.G0 = g.Base + g.FitA - g.FitB + g.Counter;
            g.P0 = WinProbability(config.winProbability, g.G0);
            return g;
        }

        /// <summary>
        /// Throws <see cref="InvalidLineupException"/> unless every lane of the layout has a starter and no player starts twice
        /// (match.md 参赛条件 1).
        /// </summary>
        public static void RequireFullLineup(PhaseLayout layout, MatchLineup lineup)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (lineup == null) throw new ArgumentNullException(nameof(lineup));
            var seen = new HashSet<long>();
            foreach (var lane in layout.LaneIds)
            {
                if (!lineup.Starters.TryGetValue(lane, out var s) || s == null)
                    throw new InvalidLineupException("Lineup of '" + lineup.ClubName + "' has no starter in lane '" + lane + "'.", lane, null);
                if (!seen.Add(s.PlayerId))
                    throw new InvalidLineupException("Lineup of '" + lineup.ClubName + "' starts player " + s.PlayerId + " twice.", lane, s.PlayerId);
            }
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>A lineup the match refuses (match.md 参赛条件 1): a lane without a starter, or a player starting twice / for both sides.</summary>
    public sealed class InvalidLineupException : InvalidOperationException
    {
        /// <summary>The lane concerned, or null.</summary>
        public string LaneId { get; }
        /// <summary>The player concerned, or null.</summary>
        public long? PlayerId { get; }

        /// <summary>Creates the error.</summary>
        public InvalidLineupException(string message, string laneId, long? playerId) : base(message) { LaneId = laneId; PlayerId = playerId; }
    }
}
