using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// Pure formulas of numbers.md 4.9 (match outcome). No state, no randomness. Skill and chemistry reuse
    /// <see cref="TeamStrength"/> / <see cref="Team.Chemistry"/>; this class adds style, power, noise and win probability.
    /// </summary>
    public static class MatchFormulas
    {
        /// <summary>
        /// Effective rating of one starter (4.9 step 1): laneScore x (0.7 + 0.3 x mood/100) x (1 - kappa_s x stress/100 x resolute).
        /// Uses the lineup's fixed mood / stress for opponents, the player's own otherwise; then adds the lineup's form
        /// offset (numbers.md 4.14 v3.3, opponents only), floored at 0.
        /// </summary>
        public static float EffectiveRating(TeamConfig team, MatchLineup lineup, Player player, string laneId)
        {
            if (lineup == null) throw new ArgumentNullException(nameof(lineup));
            float r = lineup.UsesFixedState
                ? TeamStrength.EffectiveScore(team, player, laneId, lineup.FixedMood, lineup.FixedStress)
                : TeamStrength.EffectiveScore(team, player, laneId);
            r += lineup.Form;
            return r < 0f ? 0f : r;
        }

        /// <summary>
        /// Skill score (4.9 step 1): weighted mean of effective ratings minus the weighted short-board penalty, plus the
        /// lineup's form. Adding the same offset f to every effective rating shifts both the weighted mean and the minimum by f,
        /// so the short-board term is unchanged and skill = TeamStrength + f exactly (floored at 0).
        /// </summary>
        public static float SkillScore(TeamConfig team, PlayerConfig players, MatchLineup lineup)
        {
            if (lineup == null) throw new ArgumentNullException(nameof(lineup));
            float s = lineup.UsesFixedState
                ? TeamStrength.Compute(team, players, lineup.StartersByLane, lineup.FixedMood, lineup.FixedStress)
                : TeamStrength.Compute(team, players, lineup.StartersByLane);
            s += lineup.Form;
            return s < 0f ? 0f : s;
        }

        /// <summary>
        /// Commander score (4.9 step 2): the highest command dimension among the starters, each read in the lane he plays and
        /// multiplied by his "command" trait multiplier (assertive), capped at 100.
        /// </summary>
        public static float CommanderScore(MatchConfig config, PlayerConfig players, MatchLineup lineup)
        {
            float best = 0f;
            foreach (var lane in players.lanes)
            {
                if (!lineup.StartersByLane.TryGetValue(lane.id, out var p) || p == null) continue;
                int idx = config.CommandIndex(lane);
                if (idx < 0) throw new InvalidOperationException("MatchConfig: lane '" + lane.id + "' has no dimension '" + config.power.commandDimensionId + "'.");
                float v = Math.Min(Player.StatMax, p.GetDimension(lane.id, idx) * p.GetTraitMultiplier(PlayerEffectKeys.Command));
                if (v > best) best = v;
            }
            return best;
        }

        /// <summary>Lane-match score (4.9 step 2): starters on their main lane / number of lanes x 100.</summary>
        public static float LaneMatchScore(PlayerConfig players, MatchLineup lineup)
        {
            int n = players.lanes.Length;
            if (n == 0) return 0f;
            int onMain = 0;
            foreach (var lane in players.lanes)
                if (lineup.StartersByLane.TryGetValue(lane.id, out var p) && p != null && p.MainLane == lane.id) onMain++;
            return 100f * onMain / n;
        }

        /// <summary>Style score (4.9 step 2): WS(commander score, lane-match score) with the configured style weights.</summary>
        public static float StyleScore(MatchConfig config, PlayerConfig players, MatchLineup lineup)
        {
            var w = config.power;
            return (w.styleWeightCommand * CommanderScore(config, players, lineup) + w.styleWeightLaneMatch * LaneMatchScore(players, lineup))
                / (w.styleWeightCommand + w.styleWeightLaneMatch);
        }

        /// <summary>Power points a side gains from the tactic matchup: counterBonus when <paramref name="own"/> counters <paramref name="opponent"/>, else 0.</summary>
        public static float TacticBonus(MatchConfig config, MatchTactic own, MatchTactic opponent)
            => config.Counters(own, opponent) ? config.tactic.counterBonus : 0f;

        /// <summary>Overall power (4.9 step 3): WS(skill, chemistry, style) + tactic bonus; WS is the weight-normalised mean.</summary>
        public static float OverallPower(MatchConfig config, float skill, float chemistry, float style, float tacticBonus)
        {
            var w = config.power;
            return (w.weightSkill * skill + w.weightChemistry * chemistry + w.weightStyle * style)
                / (w.weightSkill + w.weightChemistry + w.weightStyle) + tacticBonus;
        }

        /// <summary>Noise half-width (4.9 step 4): sigma0 + sigma1 x averageStress/100 x resoluteFactor.</summary>
        public static float Sigma(MatchConfig config, float averageStress, float resoluteFactor)
            => config.noise.sigma0 + config.noise.sigma1 * averageStress / 100f * resoluteFactor;

        /// <summary>Power in one game (4.9 step 4): P x (1 + epsilon), times the decider coefficient in a deciding game (1 otherwise).</summary>
        public static float GamePower(float power, float epsilon, float deciderFactor, bool isDecider)
            => power * (1f + epsilon) * (isDecider ? deciderFactor : 1f);

        /// <summary>Win probability of side A (4.9 step 5): clamp(1 / (1 + e^-((pA - pB)/k)), p_min, p_max).</summary>
        public static float WinProbability(MatchConfig config, float powerA, float powerB)
        {
            double p = 1.0 / (1.0 + Math.Exp(-(powerA - powerB) / config.winProbability.k));
            if (p < config.winProbability.pMin) p = config.winProbability.pMin;
            if (p > config.winProbability.pMax) p = config.winProbability.pMax;
            return (float)p;
        }

        /// <summary>
        /// Evaluates one side (everything in 4.9 except the per-game noise draw). The lineup must have a starter in every lane.
        /// </summary>
        /// <param name="opponentTactic">The other side's tactic, for the counter bonus.</param>
        public static SideEvaluation Evaluate(MatchConfig config, TeamConfig team, PlayerConfig players, MatchLineup lineup, MatchTactic opponentTactic)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (lineup == null) throw new ArgumentNullException(nameof(lineup));
            RequireFullLineup(players, lineup);
            float skill = SkillScore(team, players, lineup);
            float style = StyleScore(config, players, lineup);
            float bonus = TacticBonus(config, lineup.Tactic, opponentTactic);
            float power = OverallPower(config, skill, lineup.Chemistry, style, bonus);

            double stress = 0.0, resolute = 0.0, decider = 0.0;
            int n = 0;
            foreach (var lane in players.lanes)
            {
                var p = lineup.StartersByLane[lane.id];
                stress += lineup.UsesFixedState ? lineup.FixedStress : p.Stress;
                resolute += p.GetTraitMultiplier(PlayerEffectKeys.StressVarianceImpact);
                decider += p.GetTraitMultiplier(PlayerEffectKeys.DeciderPerformance);
                n++;
            }
            float avgStress = (float)(stress / n), avgResolute = (float)(resolute / n), avgDecider = (float)(decider / n);
            return new SideEvaluation(skill, lineup.Chemistry, style, bonus, power, avgStress, avgResolute, avgDecider,
                Sigma(config, avgStress, avgResolute), lineup.Form);
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> unless every lane has a starter and no player starts twice
        /// (a team needs five starters to play, numbers.md 6).
        /// </summary>
        public static void RequireFullLineup(PlayerConfig players, MatchLineup lineup)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            var seen = new HashSet<long>();
            foreach (var lane in players.lanes)
            {
                if (!lineup.StartersByLane.TryGetValue(lane.id, out var p) || p == null)
                    throw new InvalidOperationException("Lineup of '" + lineup.ClubName + "' has no starter in lane '" + lane.id + "'.");
                if (!seen.Add(p.Id))
                    throw new InvalidOperationException("Lineup of '" + lineup.ClubName + "' starts player " + p.Id + " twice.");
            }
        }
    }
}
