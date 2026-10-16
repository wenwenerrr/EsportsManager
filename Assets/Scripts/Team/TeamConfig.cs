using System;
using System.Collections.Generic;

namespace EsportsManager.Teams
{
    /// <summary>Weight of one lane in the team strength aggregation. Data from config.</summary>
    [Serializable]
    public sealed class LaneWeight
    {
        /// <summary>Lane id (must exist in the player config).</summary>
        public string lane;
        /// <summary>Weight, must be >= 0.</summary>
        public float weight = 1f;
    }

    /// <summary>One entry of the personality pairing table. Pairs are unordered; unlisted pairs are neutral.</summary>
    [Serializable]
    public sealed class PersonalityPairing
    {
        /// <summary>First personality id.</summary>
        public string a;
        /// <summary>Second personality id (may equal <see cref="a"/>).</summary>
        public string b;
        /// <summary>Either <see cref="TeamConfig.RelationCompatible"/> or <see cref="TeamConfig.RelationConflict"/>.</summary>
        public string relation;
    }

    /// <summary>
    /// All Story 002 tuning data (design/game-brief.md MVP item 2). Plain data, loaded from
    /// team_config.json by <see cref="TeamConfigLoader"/>. No gameplay number lives in code.
    /// </summary>
    /// <remarks>
    /// Decisions (Story 002 left them open): Salary is per YEAR; a segment pays salary / segmentsPerYear.
    /// Strength aggregates the starters' lane scores with per-lane weights. Popularity blends starters'
    /// average with team-owned popularity by <see cref="starterPopularityShare"/>.
    /// </remarks>
    [Serializable]
    public sealed class TeamConfig
    {
        /// <summary>Strength = sum(w*score)/sum(w).</summary>
        public const string AggregationWeightedAverage = "weighted_average";
        /// <summary>Strength = sum(w*score), clamped to 0-100 (weights should then sum to about 1).</summary>
        public const string AggregationWeightedSum = "weighted_sum";
        /// <summary>Pairing relation: personalities get along.</summary>
        public const string RelationCompatible = "compatible";
        /// <summary>Pairing relation: personalities clash.</summary>
        public const string RelationConflict = "conflict";

        // strength
        public string strengthAggregation = AggregationWeightedAverage;
        public LaneWeight[] laneWeights;
        // popularity
        /// <summary>Share (0..1) of team popularity coming from starters; the rest is team-owned.</summary>
        public float starterPopularityShare = 0.5f;
        // initial state
        public double initialFunds;
        public float initialOwnPopularity, initialChemistry;
        // finance
        /// <summary>Segments in a year; a segment's salary expense is yearly salary / this.</summary>
        public int segmentsPerYear = 4;
        public float sponsorshipBase, sponsorshipPerPopularity, sponsorshipPerWinRate;
        /// <summary>How many of the most recent results count toward win rate.</summary>
        public int recentResultsWindow = 10;
        /// <summary>Win rate (0..1) assumed when there are no results yet.</summary>
        public float noResultsWinRate = 0.5f;
        public float livestreamBase, livestreamPerPopularity;
        // chemistry
        public float chemistryRisePerSegment, chemistryDropPerReplacement;
        /// <summary>Pair modifier for compatible pairs, >= 0.</summary>
        public float compatibleModifier;
        /// <summary>Pair modifier for conflicting pairs, &lt;= 0.</summary>
        public float conflictModifier;
        public PersonalityPairing[] pairings;

        /// <summary>Weight of a lane, or 0 if the lane is not configured.</summary>
        public float GetLaneWeight(string laneId)
        {
            if (laneWeights != null)
                foreach (var lw in laneWeights)
                    if (lw.lane == laneId) return lw.weight;
            return 0f;
        }

        /// <summary>
        /// Modifier of an unordered personality pair: compatibleModifier, conflictModifier, or 0 when unlisted.
        /// </summary>
        public float GetPairModifier(string personalityA, string personalityB)
        {
            if (pairings == null) return 0f;
            foreach (var p in pairings)
            {
                bool match = (p.a == personalityA && p.b == personalityB) || (p.a == personalityB && p.b == personalityA);
                if (match) return p.relation == RelationCompatible ? compatibleModifier : conflictModifier;
            }
            return 0f;
        }

        /// <summary>
        /// Checks the config is usable; throws <see cref="InvalidOperationException"/> naming the field.
        /// Rules: known aggregation; lane_weights non-empty, unique, known lanes, each >= 0, sum > 0 (every
        /// lane in <paramref name="laneIds"/> must be listed); starter share in [0,1]; initial values finite and
        /// popularity/chemistry in [0,100]; segmentsPerYear and window > 0; all rates finite and >= 0; win
        /// rate in [0,1]; compatible modifier >= 0, conflict modifier &lt;= 0; pairings non-empty, personality
        /// ids known (<paramref name="personalityIds"/>), relation known, no duplicate unordered pair.
        /// </summary>
        /// <param name="laneIds">Lane ids from the player config.</param>
        /// <param name="personalityIds">Personality ids from the player config.</param>
        public void Validate(IEnumerable<string> laneIds, IEnumerable<string> personalityIds)
        {
            var lanes = new HashSet<string>(laneIds ?? throw new ArgumentNullException(nameof(laneIds)));
            var personalities = new HashSet<string>(personalityIds ?? throw new ArgumentNullException(nameof(personalityIds)));

            if (strengthAggregation != AggregationWeightedAverage && strengthAggregation != AggregationWeightedSum)
                throw Fail("strength.aggregation must be '" + AggregationWeightedAverage + "' or '" + AggregationWeightedSum + "', got '" + strengthAggregation + "'.");
            ValidateLaneWeights(lanes);

            if (!(starterPopularityShare >= 0f && starterPopularityShare <= 1f))
                throw Fail("popularity.starter_share must be in [0,1], got " + starterPopularityShare + ".");

            if (!IsFinite(initialFunds)) throw Fail("initial.funds must be a finite number.");
            Range100("initial.own_popularity", initialOwnPopularity);
            Range100("initial.chemistry", initialChemistry);

            if (segmentsPerYear <= 0) throw Fail("finance.segments_per_year must be > 0, got " + segmentsPerYear + ".");
            if (recentResultsWindow <= 0) throw Fail("finance.recent_results_window must be > 0, got " + recentResultsWindow + ".");
            NonNegative("finance.sponsorship_base", sponsorshipBase);
            NonNegative("finance.sponsorship_per_popularity", sponsorshipPerPopularity);
            NonNegative("finance.sponsorship_per_win_rate", sponsorshipPerWinRate);
            NonNegative("finance.livestream_base", livestreamBase);
            NonNegative("finance.livestream_per_popularity", livestreamPerPopularity);
            if (!(noResultsWinRate >= 0f && noResultsWinRate <= 1f))
                throw Fail("finance.no_results_win_rate must be in [0,1], got " + noResultsWinRate + ".");

            NonNegative("chemistry.rise_per_segment", chemistryRisePerSegment);
            NonNegative("chemistry.drop_per_replacement", chemistryDropPerReplacement);
            if (!(compatibleModifier >= 0f) || float.IsInfinity(compatibleModifier))
                throw Fail("chemistry.compatible_modifier must be >= 0, got " + compatibleModifier + ".");
            if (!(conflictModifier <= 0f) || float.IsInfinity(conflictModifier))
                throw Fail("chemistry.conflict_modifier must be <= 0, got " + conflictModifier + ".");
            ValidatePairings(personalities);
        }

        private void ValidateLaneWeights(HashSet<string> lanes)
        {
            if (laneWeights == null || laneWeights.Length == 0) throw Fail("strength.lane_weights is empty.");
            var seen = new HashSet<string>();
            float sum = 0f;
            foreach (var lw in laneWeights)
            {
                if (lw == null || string.IsNullOrEmpty(lw.lane)) throw Fail("strength.lane_weights entry without lane.");
                if (!lanes.Contains(lw.lane)) throw Fail("strength.lane_weights: unknown lane '" + lw.lane + "'.");
                if (!seen.Add(lw.lane)) throw Fail("strength.lane_weights: duplicate lane '" + lw.lane + "'.");
                if (!(lw.weight >= 0f) || float.IsInfinity(lw.weight))
                    throw Fail("strength.lane_weights['" + lw.lane + "'].weight must be >= 0, got " + lw.weight + ".");
                sum += lw.weight;
            }
            foreach (var l in lanes)
                if (!seen.Contains(l)) throw Fail("strength.lane_weights is missing lane '" + l + "'.");
            if (!(sum > 0f)) throw Fail("strength.lane_weights sum to 0.");
        }

        private void ValidatePairings(HashSet<string> personalities)
        {
            if (pairings == null || pairings.Length == 0) throw Fail("chemistry.pairings is empty.");
            var seen = new HashSet<string>();
            foreach (var p in pairings)
            {
                if (p == null) throw Fail("chemistry.pairings contains a null entry.");
                if (!personalities.Contains(p.a)) throw Fail("chemistry.pairings: unknown personality '" + p.a + "'.");
                if (!personalities.Contains(p.b)) throw Fail("chemistry.pairings: unknown personality '" + p.b + "'.");
                if (p.relation != RelationCompatible && p.relation != RelationConflict)
                    throw Fail("chemistry.pairings relation must be '" + RelationCompatible + "' or '" + RelationConflict + "', got '" + p.relation + "'.");
                string key = string.CompareOrdinal(p.a, p.b) <= 0 ? p.a + "|" + p.b : p.b + "|" + p.a;
                if (!seen.Add(key)) throw Fail("chemistry.pairings: duplicate pair '" + p.a + "' + '" + p.b + "'.");
            }
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static void NonNegative(string field, float v)
        {
            if (!(v >= 0f) || float.IsInfinity(v)) throw Fail(field + " must be >= 0, got " + v + ".");
        }

        private static void Range100(string field, float v)
        {
            if (!(v >= 0f && v <= 100f)) throw Fail(field + " must be in [0,100], got " + v + ".");
        }

        private static InvalidOperationException Fail(string message) => new InvalidOperationException("TeamConfig: " + message);
    }
}
