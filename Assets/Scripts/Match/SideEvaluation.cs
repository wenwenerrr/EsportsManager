namespace EsportsManager.Matches
{
    /// <summary>
    /// Everything numbers.md 4.9 computes for one side before the first game (no randomness).
    /// <see cref="Power"/> is the overall power P including the tactic counter bonus; the per-game noise is applied by the simulator.
    /// </summary>
    public readonly struct SideEvaluation
    {
        /// <summary>Skill score (step 1): weighted effective rating minus short-board penalty.</summary>
        public readonly float Skill;
        /// <summary>Team chemistry 0-100.</summary>
        public readonly float Chemistry;
        /// <summary>Style score 0-100 (step 2).</summary>
        public readonly float Style;
        /// <summary>Power points gained by countering the opponent's tactic (step 3), 0 when not countering.</summary>
        public readonly float TacticBonus;
        /// <summary>P = WS(skill, chemistry, style) + tactic bonus.</summary>
        public readonly float Power;
        /// <summary>Mean stress of the five starters (the fixed value for opponents).</summary>
        public readonly float AverageStress;
        /// <summary>Team resolute factor: mean of the starters' "stress_variance_impact" multipliers (decision 2026-10-19).</summary>
        public readonly float ResoluteFactor;
        /// <summary>Team decider coefficient: mean of the starters' "decider_performance" multipliers.</summary>
        public readonly float DeciderFactor;
        /// <summary>Per-game noise half-width sigma (step 4).</summary>
        public readonly float Sigma;
        /// <summary>Form offset included in <see cref="Skill"/> (opponents, numbers.md 4.14 v3.3; 0 for the player club).</summary>
        public readonly float Form;

        /// <summary>Creates an evaluation.</summary>
        public SideEvaluation(float skill, float chemistry, float style, float tacticBonus, float power,
            float averageStress, float resoluteFactor, float deciderFactor, float sigma, float form = 0f)
        {
            Form = form;
            Skill = skill; Chemistry = chemistry; Style = style; TacticBonus = tacticBonus; Power = power;
            AverageStress = averageStress; ResoluteFactor = resoluteFactor; DeciderFactor = deciderFactor; Sigma = sigma;
        }
    }
}
