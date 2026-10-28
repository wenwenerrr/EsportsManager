using System;
using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>The four specialty hook points of the match (design/gdd/match.md "绝活挂钩点"). Version 1 only changes the OWN side.</summary>
    public enum SpecialtyHook
    {
        /// <summary>phase_flat[k]: added to the own S_k at evaluation step 6. Summed, clamped per phase.</summary>
        PhaseFlat = 0,
        /// <summary>star_scale[k], k in {Early, Late}: multiplies the own star term at step 8 (before the star cap). Multiplied, clamped.</summary>
        StarScale = 1,
        /// <summary>exec_add: added to the own execution at step 7 before its clamp. Summed.</summary>
        ExecAdd = 2,
        /// <summary>sigma_scale: multiplies the own noise half-width at step 11. Multiplied, clamped.</summary>
        SigmaScale = 3,
    }

    /// <summary>
    /// One entry of a side's hook table. Hooks are unconditional (no tactic condition, match.md 2026-10-23). The growth system
    /// (Story 012) builds the table and does all filtering (starters only, main lane only, no duplicate specialty); the match
    /// only sums / multiplies and clamps.
    /// </summary>
    public sealed class SpecialtyEffect
    {
        /// <summary>Hook point.</summary>
        public readonly SpecialtyHook Hook;
        /// <summary>Phase (PhaseFlat: any; StarScale: Early or Late; ignored otherwise).</summary>
        public readonly MatchPhase Phase;
        /// <summary>Added value (PhaseFlat, ExecAdd) or multiplier (StarScale, SigmaScale).</summary>
        public readonly float Value;

        /// <summary>Creates an entry. Throws for a StarScale on the mid phase or a non-finite value.</summary>
        public SpecialtyEffect(SpecialtyHook hook, float value, MatchPhase phase = MatchPhase.Early)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "must be finite.");
            if (hook == SpecialtyHook.StarScale && phase == MatchPhase.Mid) throw new ArgumentException("star_scale exists only for early and late.", nameof(phase));
            Hook = hook; Value = value; Phase = phase;
        }
    }

    /// <summary>The aggregated hook values of one side (neutral = 0 / 1).</summary>
    public readonly struct HookTotals
    {
        /// <summary>phase_flat of early / mid / late, clamped.</summary>
        public readonly double FlatEarly, FlatMid, FlatLate;
        /// <summary>star_scale for early / late, clamped.</summary>
        public readonly double StarScaleEarly, StarScaleLate;
        /// <summary>exec_add (unclamped; the execution itself is clamped).</summary>
        public readonly double ExecAdd;
        /// <summary>sigma_scale, clamped.</summary>
        public readonly double SigmaScale;

        /// <summary>Creates totals.</summary>
        public HookTotals(double flatE, double flatM, double flatL, double starE, double starL, double execAdd, double sigmaScale)
        {
            FlatEarly = flatE; FlatMid = flatM; FlatLate = flatL; StarScaleEarly = starE; StarScaleLate = starL; ExecAdd = execAdd; SigmaScale = sigmaScale;
        }

        /// <summary>All hooks neutral.</summary>
        public static HookTotals Neutral => new HookTotals(0, 0, 0, 1, 1, 0, 1);

        /// <summary>True when every hook is neutral.</summary>
        public bool IsNeutral => FlatEarly == 0 && FlatMid == 0 && FlatLate == 0 && StarScaleEarly == 1 && StarScaleLate == 1 && ExecAdd == 0 && SigmaScale == 1;

        /// <summary>phase_flat of a phase.</summary>
        public double Flat(MatchPhase k) => k == MatchPhase.Early ? FlatEarly : k == MatchPhase.Mid ? FlatMid : FlatLate;

        /// <summary>star_scale of a phase (1 for mid).</summary>
        public double StarScale(MatchPhase k) => k == MatchPhase.Early ? StarScaleEarly : k == MatchPhase.Late ? StarScaleLate : 1.0;

        /// <summary>
        /// Aggregates a hook table (match.md: additive hooks add, multipliers multiply, then the aggregate clamps of
        /// <see cref="SpecialtyConfig"/>). Null or empty = neutral.
        /// </summary>
        public static HookTotals Aggregate(IEnumerable<SpecialtyEffect> effects, SpecialtyConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            double fe = 0, fm = 0, fl = 0, se = 1, sl = 1, ex = 0, sg = 1;
            if (effects != null)
                foreach (var e in effects)
                {
                    if (e == null) continue;
                    switch (e.Hook)
                    {
                        case SpecialtyHook.PhaseFlat:
                            if (e.Phase == MatchPhase.Early) fe += e.Value; else if (e.Phase == MatchPhase.Mid) fm += e.Value; else fl += e.Value;
                            break;
                        case SpecialtyHook.StarScale:
                            if (e.Phase == MatchPhase.Early) se *= e.Value; else sl *= e.Value;
                            break;
                        case SpecialtyHook.ExecAdd: ex += e.Value; break;
                        case SpecialtyHook.SigmaScale: sg *= e.Value; break;
                    }
                }
            double cap = cfg.phaseFlatCap;
            return new HookTotals(Clamp(fe, -cap, cap), Clamp(fm, -cap, cap), Clamp(fl, -cap, cap),
                Clamp(se, cfg.starScaleMin, cfg.starScaleMax), Clamp(sl, cfg.starScaleMin, cfg.starScaleMax), ex,
                Clamp(sg, cfg.sigmaScaleMin, cfg.sigmaScaleMax));
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
