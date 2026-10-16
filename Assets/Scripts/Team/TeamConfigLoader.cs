using System;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Teams
{
    /// <summary>
    /// Parses team_config.json (snake_case keys) into a validated <see cref="TeamConfig"/>.
    /// JsonUtility binds by exact field name, so the file is read through private wire-format DTOs
    /// that are mapped once into the real type (see .claude/rules/data-files.md).
    /// </summary>
    public static class TeamConfigLoader
    {
        // ---- wire format: field names ARE the JSON keys ----
        [Serializable] private sealed class WireRoot
        {
            public WireStrength strength;
            public WirePopularity popularity;
            public WireInitial initial;
            public WireFinance finance;
            public WireChemistry chemistry;
        }
        [Serializable] private sealed class WireLaneWeight { public string lane; public float weight; }
        [Serializable] private sealed class WireStrength { public string aggregation; public WireLaneWeight[] lane_weights; }
        [Serializable] private sealed class WirePopularity { public float starter_share; }
        [Serializable] private sealed class WireInitial { public double funds; public float own_popularity; public float chemistry; }
        [Serializable] private sealed class WireFinance
        {
            public int segments_per_year;
            public float sponsorship_base, sponsorship_per_popularity, sponsorship_per_win_rate;
            public int recent_results_window;
            public float no_results_win_rate;
            public float livestream_base, livestream_per_popularity;
        }
        [Serializable] private sealed class WirePairing { public string a; public string b; public string relation; }
        [Serializable] private sealed class WireChemistry
        {
            public float rise_per_segment, drop_per_replacement, compatible_modifier, conflict_modifier;
            public WirePairing[] pairings;
        }

        /// <summary>
        /// Parses JSON text and validates it against the player config (lane and personality ids are read
        /// from it). Any bad-config case surfaces as <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <example><code>var cfg = TeamConfigLoader.FromJson(textAsset.text, playerConfig);</code></example>
        public static TeamConfig FromJson(string json, PlayerConfig playerConfig)
        {
            if (playerConfig == null) throw new ArgumentNullException(nameof(playerConfig));
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("TeamConfig: JSON text is empty.");

            WireRoot wire;
            try { wire = JsonUtility.FromJson<WireRoot>(json); }
            catch (Exception e) { throw new InvalidOperationException("TeamConfig: JSON could not be parsed: " + e.Message, e); }
            if (wire == null) throw new InvalidOperationException("TeamConfig: JSON could not be parsed.");
            if (wire.strength == null) throw new InvalidOperationException("TeamConfig: missing section 'strength'.");
            if (wire.popularity == null) throw new InvalidOperationException("TeamConfig: missing section 'popularity'.");
            if (wire.initial == null) throw new InvalidOperationException("TeamConfig: missing section 'initial'.");
            if (wire.finance == null) throw new InvalidOperationException("TeamConfig: missing section 'finance'.");
            if (wire.chemistry == null) throw new InvalidOperationException("TeamConfig: missing section 'chemistry'.");

            var config = Map(wire);
            var laneIds = new string[playerConfig.lanes.Length];
            for (int i = 0; i < laneIds.Length; i++) laneIds[i] = playerConfig.lanes[i].id;
            config.Validate(laneIds, playerConfig.personalities);
            return config;
        }

        private static TeamConfig Map(WireRoot w)
        {
            var c = new TeamConfig
            {
                strengthAggregation = w.strength.aggregation,
                starterPopularityShare = w.popularity.starter_share,
                initialFunds = w.initial.funds,
                initialOwnPopularity = w.initial.own_popularity,
                initialChemistry = w.initial.chemistry,
                segmentsPerYear = w.finance.segments_per_year,
                sponsorshipBase = w.finance.sponsorship_base,
                sponsorshipPerPopularity = w.finance.sponsorship_per_popularity,
                sponsorshipPerWinRate = w.finance.sponsorship_per_win_rate,
                recentResultsWindow = w.finance.recent_results_window,
                noResultsWinRate = w.finance.no_results_win_rate,
                livestreamBase = w.finance.livestream_base,
                livestreamPerPopularity = w.finance.livestream_per_popularity,
                chemistryRisePerSegment = w.chemistry.rise_per_segment,
                chemistryDropPerReplacement = w.chemistry.drop_per_replacement,
                compatibleModifier = w.chemistry.compatible_modifier,
                conflictModifier = w.chemistry.conflict_modifier,
            };
            if (w.strength.lane_weights != null)
            {
                c.laneWeights = new LaneWeight[w.strength.lane_weights.Length];
                for (int i = 0; i < c.laneWeights.Length; i++)
                    c.laneWeights[i] = new LaneWeight { lane = w.strength.lane_weights[i].lane, weight = w.strength.lane_weights[i].weight };
            }
            if (w.chemistry.pairings != null)
            {
                c.pairings = new PersonalityPairing[w.chemistry.pairings.Length];
                for (int i = 0; i < c.pairings.Length; i++)
                    c.pairings[i] = new PersonalityPairing { a = w.chemistry.pairings[i].a, b = w.chemistry.pairings[i].b, relation = w.chemistry.pairings[i].relation };
            }
            return c;
        }
    }
}
