using System;
using System.Linq;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Teams
{
    /// <summary>
    /// Parses and validates <see cref="TeamConfig"/> JSON (Assets/Data/team_config.json), snake_case keys,
    /// private wire-format DTOs (.claude/rules/data-files.md). The raw text is first checked strictly by
    /// <see cref="JsonShapeChecker"/> (missing / misspelled / extra / duplicate keys, wrong types, nulls,
    /// non-finite numbers all throw), so the DTOs only ever see complete, well-typed data and need no
    /// sentinel defaults. Numbers' meaning: design/numbers.md 4.2, 4.3, 4.8, 4.9 step 1, 4.10.
    /// </summary>
    public static class TeamConfigLoader
    {
        /// <summary>
        /// Parses JSON text into a validated config. Every bad-config case surfaces as <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <example><code>var cfg = TeamConfigLoader.FromJson(textAsset.text);</code></example>
        public static TeamConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("TeamConfig: JSON text is empty.");
            JsonShapeChecker.Check(json, typeof(ConfigDto), "TeamConfig");
            ConfigDto dto;
            try { dto = JsonUtility.FromJson<ConfigDto>(json); }
            catch (Exception e) { throw new InvalidOperationException("TeamConfig: JSON could not be parsed: " + e.Message, e); }
            if (dto == null) throw new InvalidOperationException("TeamConfig: JSON could not be parsed.");

            var cfg = new TeamConfig
            {
                laneWeights = dto.lane_weights.Select(w => new LaneWeightDef { lane = w.lane, weight = w.weight }).ToArray(),
                strength = new StrengthConfig
                {
                    shortBoardPenalty = dto.strength.short_board_penalty, stressFactor = dto.strength.stress_factor,
                    moodFactorBase = dto.strength.mood_factor_base, moodFactorSpan = dto.strength.mood_factor_span,
                },
                familiarity = new FamiliarityConfig
                {
                    perSegment = dto.familiarity.per_segment, perWin = dto.familiarity.per_win, max = dto.familiarity.max,
                    decay = dto.familiarity.decay, compatibilityFactor = dto.familiarity.compatibility_factor,
                },
                popularity = new PopularityConfig
                {
                    alpha = dto.popularity.alpha, randomRange = dto.popularity.random_range,
                    weightClub = dto.popularity.weight_club, weightLaneScore = dto.popularity.weight_lane_score,
                    weightLooks = dto.popularity.weight_looks, weightStream = dto.popularity.weight_stream,
                    weightWinRate = dto.popularity.weight_win_rate, weightBusiness = dto.popularity.weight_business,
                },
                clubPopularity = new ClubPopularityConfig
                {
                    initial = dto.club_popularity.initial, beta = dto.club_popularity.beta,
                    randomRange = dto.club_popularity.random_range,
                    weightStarterPopularity = dto.club_popularity.weight_starter_popularity,
                    weightWinRate = dto.club_popularity.weight_win_rate, weightRank = dto.club_popularity.weight_rank,
                },
                results = new ResultsConfig { window = dto.results.window, defaultWinRate = dto.results.default_win_rate },
                livestream = new LivestreamConfig
                {
                    baseIncome = dto.livestream.base_income, incomePerPopularity = dto.livestream.income_per_popularity,
                    secondStreamFactor = dto.livestream.second_stream_factor,
                },
            };
            cfg.Validate();
            return cfg;
        }

        // Wire format: field names ARE the JSON keys.
        [Serializable] private class LaneWeightDto { public string lane; public float weight; }
        [Serializable] private class StrengthDto { public float short_board_penalty; public float stress_factor; public float mood_factor_base; public float mood_factor_span; }
        [Serializable] private class FamiliarityDto { public float per_segment; public float per_win; public float max; public float decay; public float compatibility_factor; }
        [Serializable] private class PopularityDto
        {
            public float alpha; public float random_range; public float weight_club; public float weight_lane_score;
            public float weight_looks; public float weight_stream; public float weight_win_rate; public float weight_business;
        }
        [Serializable] private class ClubPopularityDto
        {
            public float initial; public float beta; public float random_range;
            public float weight_starter_popularity; public float weight_win_rate; public float weight_rank;
        }
        [Serializable] private class ResultsDto { public int window; public float default_win_rate; }
        [Serializable] private class LivestreamDto { public int base_income; public float income_per_popularity; public float second_stream_factor; }
        [Serializable] private class ConfigDto
        {
            public LaneWeightDto[] lane_weights;
            public StrengthDto strength;
            public FamiliarityDto familiarity;
            public PopularityDto popularity;
            public ClubPopularityDto club_popularity;
            public ResultsDto results;
            public LivestreamDto livestream;
        }
    }
}
