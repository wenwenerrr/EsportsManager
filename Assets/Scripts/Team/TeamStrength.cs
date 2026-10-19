using System;
using System.Collections.Generic;
using EsportsManager.Players;

namespace EsportsManager.Teams
{
    /// <summary>
    /// Pure functions for team strength (实力, numbers.md 4.9 step 1). No state, no randomness; reused by Story 004.
    /// </summary>
    /// <example><code>float s = TeamStrength.Compute(teamCfg, playerCfg, team.StartersByLane());</code></example>
    public static class TeamStrength
    {
        /// <summary>
        /// Effective score of one starter in one lane:
        /// <c>laneScore x moodFactor x stressFactor</c> where moodFactor = base + span x mood/100
        /// (0.7 + 0.3 x mood/100) and stressFactor = 1 - kappa_s x stress/100 x resolute multiplier
        /// (the player's "stress_variance_impact" trait multiplier, <see cref="PlayerEffectKeys.StressVarianceImpact"/>).
        /// The stress factor is floored at 0. A null player (empty slot) scores 0.
        /// </summary>
        public static float EffectiveScore(TeamConfig config, Player player, string laneId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (player == null) return 0f;
            var s = config.strength;
            float mood = s.moodFactorBase + s.moodFactorSpan * player.Mood / 100f;
            float stress = 1f - s.stressFactor * player.Stress / 100f * player.GetTraitMultiplier(PlayerEffectKeys.StressVarianceImpact);
            if (stress < 0f) stress = 0f;
            return player.GetLaneScore(laneId) * mood * stress;
        }

        /// <summary>
        /// Strength = W - lambda x max(0, W - min effective), where W is the lane-weighted average of the effective
        /// scores over all lanes of <paramref name="players"/> (empty slot = 0). The short-board penalty uses the SAME
        /// weighted mean W (user decision 2026-10-16), and the minimum also includes empty slots as 0. Result is
        /// floored at 0 so a pathological weight table cannot give a negative strength.
        /// </summary>
        /// <param name="config">Team tuning.</param>
        /// <param name="players">Player config; its lanes define the slots.</param>
        /// <param name="startersByLane">Lane id to starter; a missing key or null value is an empty slot. May be null.</param>
        public static float Compute(TeamConfig config, PlayerConfig players, IReadOnlyDictionary<string, Player> startersByLane)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (players == null) throw new ArgumentNullException(nameof(players));
            double weighted = 0.0, weights = 0.0, min = double.MaxValue;
            int n = 0;
            foreach (var lane in players.lanes)
            {
                Player p = null;
                if (startersByLane != null) startersByLane.TryGetValue(lane.id, out p);
                float eff = EffectiveScore(config, p, lane.id);
                float w = config.GetLaneWeight(lane.id);
                weighted += w * eff; weights += w; n++;
                if (eff < min) min = eff;
            }
            if (n == 0 || weights <= 0.0) return 0f;
            double mean = weighted / weights;
            double result = mean - config.strength.shortBoardPenalty * Math.Max(0.0, mean - min);
            return result < 0.0 ? 0f : (float)result;
        }
    }
}
