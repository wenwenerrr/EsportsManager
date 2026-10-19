using System;
using System.Linq;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Matches
{
    /// <summary>
    /// Parses and validates <see cref="MatchConfig"/> JSON (Assets/Data/match_config.json): snake_case keys, private
    /// wire-format DTOs (.claude/rules/data-files.md). The text is first checked strictly by
    /// <see cref="JsonShapeChecker"/> (missing / misspelled / extra / duplicate keys, wrong types, nulls, non-finite
    /// numbers all throw). Tactic names are config keys ("invade", "develop", "teamfight"); an unknown one throws.
    /// Meaning of the numbers: design/numbers.md 4.9, 4.10, 4.14, 4.18. Event types are config keys ("split", "finals").
    /// </summary>
    /// <remarks>JsonShapeChecker has no 64-bit type, so the prize tables are int on the wire and long in <see cref="FormatConfig"/>.</remarks>
    public static class MatchConfigLoader
    {
        /// <summary>Parses JSON text into a validated config; every bad-config case is an <see cref="InvalidOperationException"/>.</summary>
        /// <example><code>var cfg = MatchConfigLoader.FromJson(textAsset.text);</code></example>
        public static MatchConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("MatchConfig: JSON text is empty.");
            JsonShapeChecker.Check(json, typeof(ConfigDto), "MatchConfig");
            ConfigDto dto;
            try { dto = JsonUtility.FromJson<ConfigDto>(json); }
            catch (Exception e) { throw new InvalidOperationException("MatchConfig: JSON could not be parsed: " + e.Message, e); }
            if (dto == null) throw new InvalidOperationException("MatchConfig: JSON could not be parsed.");

            var cfg = new MatchConfig
            {
                series = new SeriesConfig { opponentCount = dto.series.opponent_count },
                calendar = new CalendarConfig
                {
                    segmentsPerYear = dto.calendar.segments_per_year,
                    events = dto.calendar.events.Select((e, i) => new CalendarEventDef
                    {
                        type = ParseEventType(e.type, "calendar.events[" + i + "].type"), nameKey = e.name_key, afterSegment = e.after_segment,
                    }).ToArray(),
                },
                format = new FormatConfig
                {
                    regularBestOf = dto.format.regular_best_of, playoffTeams = dto.format.playoff_teams,
                    playoffBestOf = dto.format.playoff_best_of, splitFinalBestOf = dto.format.split_final_best_of,
                    finalsTeams = dto.format.finals_teams, finalsSemiBestOf = dto.format.finals_semi_best_of,
                    finalsFinalBestOf = dto.format.finals_final_best_of,
                    finalsPoints = (int[])dto.format.finals_points.Clone(),
                    splitPrizes = Array.ConvertAll(dto.format.split_prizes, v => (long)v),
                    finalsPrizes = Array.ConvertAll(dto.format.finals_prizes, v => (long)v),
                },
                power = new PowerConfig
                {
                    weightSkill = dto.power.weight_skill, weightChemistry = dto.power.weight_chemistry,
                    weightStyle = dto.power.weight_style, styleWeightCommand = dto.power.style_weight_command,
                    styleWeightLaneMatch = dto.power.style_weight_lane_match, commandDimensionId = dto.power.command_dimension_id,
                },
                noise = new NoiseConfig { sigma0 = dto.noise.sigma0, sigma1 = dto.noise.sigma1 },
                winProbability = new WinProbabilityConfig { k = dto.win_probability.k, pMin = dto.win_probability.p_min, pMax = dto.win_probability.p_max },
                tactic = new TacticConfig
                {
                    counterBonus = dto.tactic.counter_bonus,
                    counters = dto.tactic.counters.Select(c => new TacticCounterDef { tactic = Parse(c.tactic, "tactic.counters.tactic"), beats = Parse(c.beats, "tactic.counters.beats") }).ToArray(),
                },
                opponent = new OpponentConfig
                {
                    fixedMood = dto.opponent.fixed_mood, fixedStress = dto.opponent.fixed_stress,
                    starterPickRange = dto.opponent.starter_pick_range, rosterExtraMin = dto.opponent.roster_extra_min,
                    rosterExtraMax = dto.opponent.roster_extra_max, familiarityMin = dto.opponent.familiarity_min,
                    familiarityMax = dto.opponent.familiarity_max, clubNames = (string[])dto.opponent.club_names.Clone(),
                    formDown = dto.opponent.form_down, formUpBase = dto.opponent.form_up_base, formUpPop = dto.opponent.form_up_pop,
                    formStep = dto.opponent.form_step, popMin = dto.opponent.pop_min, popMax = dto.opponent.pop_max,
                },
                report = new ReportConfig { minReasons = dto.report.min_reasons, maxReasons = dto.report.max_reasons, minContribution = dto.report.min_contribution, evenThreshold = dto.report.even_threshold },
                club = new ClubConfig { defaultName = dto.club.default_name, nameMaxLength = dto.club.name_max_length },
            };
            cfg.Validate();
            return cfg;
        }

        private static EventType ParseEventType(string key, string field)
        {
            switch (key)
            {
                case "split": return EventType.Split;
                case "finals": return EventType.Finals;
                default: throw new InvalidOperationException("MatchConfig: unknown event type '" + key + "' in " + field + " (expected split or finals).");
            }
        }

        private static MatchTactic Parse(string key, string field)
        {
            if (!MatchTactics.TryParse(key, out var t))
                throw new InvalidOperationException("MatchConfig: unknown tactic '" + key + "' in " + field + " (expected invade, develop or teamfight).");
            return t;
        }

        // Wire format: field names ARE the JSON keys.
        [Serializable] private class SeriesDto { public int opponent_count; }
        [Serializable] private class EventDto { public string type; public string name_key; public int after_segment; }
        [Serializable] private class CalendarDto { public int segments_per_year; public EventDto[] events; }
        [Serializable] private class FormatDto
        {
            public int regular_best_of; public int playoff_teams; public int playoff_best_of; public int split_final_best_of;
            public int finals_teams; public int finals_semi_best_of; public int finals_final_best_of;
            public int[] finals_points; public int[] split_prizes; public int[] finals_prizes;
        }
        [Serializable] private class PowerDto
        {
            public float weight_skill; public float weight_chemistry; public float weight_style;
            public float style_weight_command; public float style_weight_lane_match; public string command_dimension_id;
        }
        [Serializable] private class NoiseDto { public float sigma0; public float sigma1; }
        [Serializable] private class WinProbabilityDto { public float k; public float p_min; public float p_max; }
        [Serializable] private class CounterDto { public string tactic; public string beats; }
        [Serializable] private class TacticDto { public float counter_bonus; public CounterDto[] counters; }
        [Serializable] private class OpponentDto
        {
            public float fixed_mood; public float fixed_stress; public int starter_pick_range;
            public int roster_extra_min; public int roster_extra_max; public float familiarity_min; public float familiarity_max;
            public string[] club_names;
            public float form_down; public float form_up_base; public float form_up_pop; public float form_step;
            public float pop_min; public float pop_max;
        }
        [Serializable] private class ReportDto { public int min_reasons; public int max_reasons; public float min_contribution; public float even_threshold; }
        [Serializable] private class ClubDto { public string default_name; public int name_max_length; }
        [Serializable] private class ConfigDto
        {
            public SeriesDto series;
            public CalendarDto calendar;
            public FormatDto format;
            public PowerDto power;
            public NoiseDto noise;
            public WinProbabilityDto win_probability;
            public TacticDto tactic;
            public OpponentDto opponent;
            public ReportDto report;
            public ClubDto club;
        }
    }
}
