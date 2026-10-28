using System;
using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>
    /// One side reduced to what the formulas need from evaluation step 3 on (design/gdd/match.md): the phase ratings
    /// r(lane, k) (F2, already including the decider coefficient in a deciding game), chemistry, the command score (F5),
    /// the stress / resolute averages (F11) and the starters' specialty effects. Lanes are in <see cref="PhaseLayout"/> order.
    /// Tests build it directly for the worked-example fixture.
    /// </summary>
    public sealed class SideInput
    {
        /// <summary>[lane][phase] phase ratings r(i,k), 0-100.</summary>
        public double[][] Ratings;
        /// <summary>Team chemistry 0-100.</summary>
        public double Chemistry;
        /// <summary>Command score cmd 0-100 (F5).</summary>
        public double Command;
        /// <summary>Mean stress of the starters 0-100.</summary>
        public double AverageStress;
        /// <summary>Mean resolute factor of the starters (0.95-1).</summary>
        public double AverageResolute = 1.0;
        /// <summary>Specialty effects of the starters (may be empty).</summary>
        public IReadOnlyList<SpecialtyEffect> Effects = Array.Empty<SpecialtyEffect>();

        /// <summary>Phase rating of a lane.</summary>
        public double Rating(int lane, MatchPhase k) => Ratings[lane][(int)k];
    }

    /// <summary>
    /// Everything a side gets on its own for one tactic (evaluation steps 3-7 and its sigma): phase strengths S_k after the mid
    /// chemistry bonus and phase_flat, execution, sigma, the star scales, and the weakest lane of every phase. This is also the
    /// pre-match estimate the prep screen shows (match.md "赛前预估"; same values as the first non-deciding game).
    /// </summary>
    public sealed class SideEvaluation
    {
        /// <summary>S_E, S_M, S_L (index = (int)MatchPhase).</summary>
        public double[] Strength = new double[MatchPhases.Count];
        /// <summary>Lane-weighted mean WS_k before the short-board penalty.</summary>
        public double[] WeightedMean = new double[MatchPhases.Count];
        /// <summary>Lane index with the lowest r(i,k) per phase (first one on ties).</summary>
        public int[] WeakestLane = new int[MatchPhases.Count];
        /// <summary>Mid chemistry bonus added to S_M (F4).</summary>
        public double MidChemistryBonus;
        /// <summary>Execution 0.3-1 (F5).</summary>
        public double Execution;
        /// <summary>Command score used.</summary>
        public double Command;
        /// <summary>Noise half-width sigma (F11).</summary>
        public double Sigma;
        /// <summary>Aggregated hooks for the tactic.</summary>
        public HookTotals Hooks;
        /// <summary>The tactic this evaluation is for.</summary>
        public MatchTactic Tactic;
        /// <summary>The input it was computed from.</summary>
        public SideInput Input;

        /// <summary>S_k of a phase.</summary>
        public double S(MatchPhase k) => Strength[(int)k];
    }
}
