using System;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Segments
{
    /// <summary>
    /// Parses and validates <see cref="SegmentConfig"/> JSON (Assets/Data/segment_config.json): snake_case keys,
    /// private wire-format DTOs (.claude/rules/data-files.md). The text is first checked strictly by
    /// <see cref="JsonShapeChecker"/> (missing / misspelled / extra / duplicate keys, wrong types, nulls,
    /// non-finite numbers all throw). Meaning of the numbers: design/numbers.md 4.6, 4.7, 4.11, 4.12, 4.16.
    /// </summary>
    public static class SegmentConfigLoader
    {
        /// <summary>Parses JSON text into a validated config; every bad-config case is an <see cref="InvalidOperationException"/>.</summary>
        /// <example><code>var cfg = SegmentConfigLoader.FromJson(textAsset.text);</code></example>
        public static SegmentConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("SegmentConfig: JSON text is empty.");
            JsonShapeChecker.Check(json, typeof(ConfigDto), "SegmentConfig");
            ConfigDto dto;
            try { dto = JsonUtility.FromJson<ConfigDto>(json); }
            catch (Exception e) { throw new InvalidOperationException("SegmentConfig: JSON could not be parsed: " + e.Message, e); }
            if (dto == null) throw new InvalidOperationException("SegmentConfig: JSON could not be parsed.");

            var cfg = new SegmentConfig
            {
                actionPoints = new ActionPointConfig { perSegment = dto.action_points.per_segment, maxPerPlayer = dto.action_points.max_per_player },
                training = new SegmentTrainingConfig { intensity = dto.training.intensity },
                stream = new StreamConfig { cap = dto.stream.cap },
                business = new BusinessConfig
                {
                    weightPopularity = dto.business.weight_popularity, weightLaneScore = dto.business.weight_lane_score,
                    weightLooks = dto.business.weight_looks, maxProbability = dto.business.max_probability,
                    baseAmount = dto.business.base_amount, amountPerPopularity = dto.business.amount_per_popularity,
                    amountPerLooks = dto.business.amount_per_looks, randomRange = dto.business.random_range,
                    clubShare = dto.business.club_share, heatCap = dto.business.heat_cap,
                },
                sponsorship = new SponsorshipConfig
                {
                    weightClubPopularity = dto.sponsorship.weight_club_popularity, weightWinRate = dto.sponsorship.weight_win_rate,
                    maxSuccessRate = dto.sponsorship.max_success_rate, randomSpread = dto.sponsorship.random_spread,
                    minChance = dto.sponsorship.min_chance, maxChance = dto.sponsorship.max_chance,
                    baseAmount = dto.sponsorship.base_amount, amountPerClubPopularity = dto.sponsorship.amount_per_club_popularity,
                    durationSegments = dto.sponsorship.duration_segments,
                    maxAttemptsPerSegment = dto.sponsorship.max_attempts_per_segment, maxActiveContracts = dto.sponsorship.max_active_contracts,
                },
                mood = new MoodConfig
                {
                    result = dto.mood.result, play = dto.mood.play, salary = dto.mood.salary, chemistry = dto.mood.chemistry,
                    stress = dto.mood.stress, regression = dto.mood.regression, neutral = dto.mood.neutral,
                    highStressThreshold = dto.mood.high_stress_threshold, randomRange = dto.mood.random_range,
                },
                stress = new StressConfig
                {
                    training = dto.stress.training, match = dto.stress.match, loss = dto.stress.loss, business = dto.stress.business,
                    rest = dto.stress.rest, stream = dto.stress.stream, recovery = dto.stress.recovery, randomRange = dto.stress.random_range,
                },
            };
            cfg.Validate();
            return cfg;
        }

        // Wire format: field names ARE the JSON keys.
        [Serializable] private class ActionPointsDto { public int per_segment; public int max_per_player; }
        [Serializable] private class TrainingDto { public float intensity; }
        [Serializable] private class StreamDto { public float cap; }
        [Serializable] private class BusinessDto
        {
            public float weight_popularity; public float weight_lane_score; public float weight_looks; public float max_probability;
            public int base_amount; public float amount_per_popularity; public float amount_per_looks; public float random_range;
            public float club_share; public float heat_cap;
        }
        [Serializable] private class SponsorshipDto
        {
            public float weight_club_popularity; public float weight_win_rate; public float max_success_rate; public float random_spread;
            public float min_chance; public float max_chance; public int base_amount; public float amount_per_club_popularity;
            public int duration_segments; public int max_attempts_per_segment; public int max_active_contracts;
        }
        [Serializable] private class MoodDto
        {
            public float result; public float play; public float salary; public float chemistry; public float stress;
            public float regression; public float neutral; public float high_stress_threshold; public float random_range;
        }
        [Serializable] private class StressDto
        {
            public float training; public float match; public float loss; public float business; public float rest;
            public float stream; public float recovery; public float random_range;
        }
        [Serializable] private class ConfigDto
        {
            public ActionPointsDto action_points;
            public TrainingDto training;
            public StreamDto stream;
            public BusinessDto business;
            public SponsorshipDto sponsorship;
            public MoodDto mood;
            public StressDto stress;
        }
    }
}
