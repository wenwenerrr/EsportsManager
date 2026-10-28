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
    /// Meaning of the numbers: design/gdd/match.md (Tuning Knobs), numbers.md 4.14, 4.18. Event types are config keys ("split", "finals").
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
                phases = new PhaseMappingConfig
                {
                    commandDimensionId = dto.phases.command_dimension_id,
                    lanes = dto.phases.lanes.Select(l => new LanePhaseDef
                    {
                        lane = l.lane, early = (string[])l.early.Clone(), mid = (string[])l.mid.Clone(), late = (string[])l.late.Clone(),
                        weightEarly = D(l.weight_early), weightMid = D(l.weight_mid), weightLate = D(l.weight_late),
                    }).ToArray(),
                },
                effective = new EffectiveConfig
                {
                    moodFloor = D(dto.effective.mood_floor), stressFactor = D(dto.effective.stress_factor),
                },
                strength = new PhaseStrengthConfig
                {
                    shortBoardEarly = D(dto.strength.short_board_early), shortBoardMid = D(dto.strength.short_board_mid),
                    shortBoardLate = D(dto.strength.short_board_late), midChemistry = D(dto.strength.mid_chemistry),
                    starFactor = D(dto.strength.star_factor), starThreshold = D(dto.strength.star_threshold), starCap = D(dto.strength.star_cap),
                    phaseWeightEarly = D(dto.strength.phase_weight_early),
                    phaseWeightMid = D(dto.strength.phase_weight_mid), phaseWeightLate = D(dto.strength.phase_weight_late),
                },
                execution = new ExecutionConfig { e0 = D(dto.execution.e0), e1 = D(dto.execution.e1), e2 = D(dto.execution.e2), min = D(dto.execution.min), max = D(dto.execution.max) },
                cycle = new CycleConfig
                {
                    strength = D(dto.cycle.strength), weightMin = D(dto.cycle.weight_min),
                    relations = dto.cycle.relations.Select((r, i) => new CycleRelationDef
                    {
                        beats = ParsePhase(r.beats, "cycle.relations[" + i + "].beats"), weakens = ParsePhase(r.weakens, "cycle.relations[" + i + "].weakens"),
                    }).ToArray(),
                },
                tactic = new TacticConfig
                {
                    fitScale = D(dto.tactic.fit_scale), fitCap = D(dto.tactic.fit_cap), counter = D(dto.tactic.counter), budget = D(dto.tactic.budget),
                    focus = dto.tactic.focus.Select((f, i) => new TacticFocusDef
                    {
                        tactic = Parse(f.tactic, "tactic.focus[" + i + "].tactic"), phase = ParsePhase(f.phase, "tactic.focus[" + i + "].phase"),
                    }).ToArray(),
                    counters = dto.tactic.counters.Select(c => new TacticCounterDef { tactic = Parse(c.tactic, "tactic.counters.tactic"), beats = Parse(c.beats, "tactic.counters.beats") }).ToArray(),
                },
                noise = new NoiseConfig { g0 = D(dto.noise.g0), g1 = D(dto.noise.g1), commandReduction = D(dto.noise.command_reduction) },
                winProbability = new WinProbabilityConfig { k = D(dto.win_probability.k), pFloor = D(dto.win_probability.p_floor) },
                specialty = new SpecialtyConfig
                {
                    phaseFlatCap = D(dto.specialty.phase_flat_cap), starScaleMin = D(dto.specialty.star_scale_min), starScaleMax = D(dto.specialty.star_scale_max),
                    sigmaScaleMin = D(dto.specialty.sigma_scale_min), sigmaScaleMax = D(dto.specialty.sigma_scale_max),
                },
                opponent = new OpponentConfig
                {
                    starterPickRange = dto.opponent.starter_pick_range, rosterExtraMin = dto.opponent.roster_extra_min,
                    rosterExtraMax = dto.opponent.roster_extra_max, familiarityMin = dto.opponent.familiarity_min,
                    familiarityMax = dto.opponent.familiarity_max, clubNames = (string[])dto.opponent.club_names.Clone(),
                    popMin = dto.opponent.pop_min, popMax = dto.opponent.pop_max,
                },
                report = new ReportConfig
                {
                    minReasons = dto.report.min_reasons, maxReasons = dto.report.max_reasons, minContribution = D(dto.report.min_contribution),
                    evenThreshold = D(dto.report.even_threshold), luckThreshold = D(dto.report.luck_threshold),
                    tacticAdviceMin = D(dto.report.tactic_advice_min),
                },
                club = new ClubConfig { defaultName = dto.club.default_name, nameMaxLength = dto.club.name_max_length },
            };
            cfg.Validate();
            return cfg;
        }

        /// <summary>
        /// <see cref="FromJson(string)"/> plus <see cref="MatchConfig.ValidateAgainst"/>: the phase mapping is checked against the
        /// lanes and dimensions of <paramref name="players"/> (missing / unknown / command dimensions, lane ids).
        /// </summary>
        public static MatchConfig FromJson(string json, PlayerConfig players)
        {
            var cfg = FromJson(json);
            cfg.ValidateAgainst(players);
            return cfg;
        }

        // Exact decimal value of a JSON number that was parsed as float (0.22 stays 0.22, not 0.2199999988).
        private static double D(float v) => (double)(decimal)v;

        private static MatchPhase ParsePhase(string key, string field)
        {
            if (!MatchPhases.TryParse(key, out var p))
                throw new InvalidOperationException("MatchConfig: unknown phase '" + key + "' in " + field + " (expected early, mid or late).");
            return p;
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
        [Serializable] private class LanePhaseDto
        {
            public string lane; public string[] early; public string[] mid; public string[] late;
            public float weight_early; public float weight_mid; public float weight_late;
        }
        [Serializable] private class PhasesDto { public string command_dimension_id; public LanePhaseDto[] lanes; }
        [Serializable] private class EffectiveDto { public float mood_floor; public float stress_factor; }
        [Serializable] private class StrengthDto
        {
            public float short_board_early; public float short_board_mid; public float short_board_late; public float mid_chemistry;
            public float star_factor; public float star_threshold; public float star_cap;
            public float phase_weight_early; public float phase_weight_mid; public float phase_weight_late;
        }
        [Serializable] private class ExecutionDto { public float e0; public float e1; public float e2; public float min; public float max; }
        [Serializable] private class RelationDto { public string beats; public string weakens; }
        [Serializable] private class CycleDto { public float strength; public float weight_min; public RelationDto[] relations; }
        [Serializable] private class FocusDto { public string tactic; public string phase; }
        [Serializable] private class CounterDto { public string tactic; public string beats; }
        [Serializable] private class TacticDto { public float fit_scale; public float fit_cap; public float counter; public float budget; public FocusDto[] focus; public CounterDto[] counters; }
        [Serializable] private class NoiseDto { public float g0; public float g1; public float command_reduction; }
        [Serializable] private class WinProbabilityDto { public float k; public float p_floor; }
        [Serializable] private class SpecialtyDto
        {
            public float phase_flat_cap; public float star_scale_min; public float star_scale_max; public float sigma_scale_min; public float sigma_scale_max;
        }
        [Serializable] private class OpponentDto
        {
            public int starter_pick_range;
            public int roster_extra_min; public int roster_extra_max; public float familiarity_min; public float familiarity_max;
            public string[] club_names;
            public float pop_min; public float pop_max;
        }
        [Serializable] private class ReportDto { public int min_reasons; public int max_reasons; public float min_contribution; public float even_threshold; public float luck_threshold; public float tactic_advice_min; }
        [Serializable] private class ClubDto { public string default_name; public int name_max_length; }
        [Serializable] private class ConfigDto
        {
            public SeriesDto series;
            public CalendarDto calendar;
            public FormatDto format;
            public PhasesDto phases;
            public EffectiveDto effective;
            public StrengthDto strength;
            public ExecutionDto execution;
            public CycleDto cycle;
            public TacticDto tactic;
            public NoiseDto noise;
            public WinProbabilityDto win_probability;
            public SpecialtyDto specialty;
            public OpponentDto opponent;
            public ReportDto report;
            public ClubDto club;
        }
    }
}
