using System.Collections.Generic;
using System.Linq;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;
using EsportsManager.Tests.Teams;

namespace EsportsManager.Tests.Matches
{
    /// <summary>In-memory fixtures for Story 004 tests: numbers.md initial values, neutral players. No file I/O.</summary>
    internal static class MatchTestFactory
    {
        public static readonly string[] Lanes = TeamTestFactory.Lanes;

        /// <summary>Player config of the team tests plus a dimension "command" at index 5 of every lane and the match-related trait effects.</summary>
        public static PlayerConfig MakePlayerConfig()
        {
            var cfg = TeamTestFactory.MakePlayerConfig();
            foreach (var lane in cfg.lanes) lane.dimensions[5].id = "command";
            cfg.traitEffects = cfg.traitEffects.Concat(new[]
            {
                TestConfigFactory.Effect("assertive", PlayerEffectKeys.Command, 1.05f),
                TestConfigFactory.Effect("decisive", PlayerEffectKeys.DeciderPerformance, 1.03f),
                TestConfigFactory.Effect("hesitant", PlayerEffectKeys.DeciderPerformance, 0.97f),
            }).ToArray();
            return cfg;
        }

        public static MatchConfig MakeConfig() => new MatchConfig
        {
            series = new SeriesConfig { opponentCount = 7 },
            calendar = new CalendarConfig
            {
                segmentsPerYear = 12,
                events = new[]
                {
                    new CalendarEventDef { type = EventType.Split, nameKey = "spring", afterSegment = 3 },
                    new CalendarEventDef { type = EventType.Split, nameKey = "summer", afterSegment = 6 },
                    new CalendarEventDef { type = EventType.Finals, nameKey = "finals", afterSegment = 12 },
                },
            },
            format = new FormatConfig
            {
                regularBestOf = 3, playoffTeams = 4, playoffBestOf = 5, splitFinalBestOf = 5,
                finalsTeams = 4, finalsSemiBestOf = 5, finalsFinalBestOf = 7,
                finalsPoints = new[] { 10, 7, 5, 5, 3, 2, 1, 0 },
                splitPrizes = new long[] { 80000, 50000, 30000, 30000, 10000, 10000, 10000, 10000 },
                finalsPrizes = new long[] { 200000, 120000, 60000, 60000 },
            },
            power = new PowerConfig
            {
                weightSkill = 3f, weightChemistry = 1f, weightStyle = 1f, styleWeightCommand = 1f, styleWeightLaneMatch = 1f,
                commandDimensionId = "command",
            },
            noise = new NoiseConfig { sigma0 = 0.03f, sigma1 = 0.12f },
            winProbability = new WinProbabilityConfig { k = 8f, pMin = 0.1f, pMax = 0.9f },
            tactic = new TacticConfig
            {
                counterBonus = 4f,
                counters = new[]
                {
                    new TacticCounterDef { tactic = MatchTactic.Invade, beats = MatchTactic.Develop },
                    new TacticCounterDef { tactic = MatchTactic.Develop, beats = MatchTactic.Teamfight },
                    new TacticCounterDef { tactic = MatchTactic.Teamfight, beats = MatchTactic.Invade },
                },
            },
            opponent = new OpponentConfig
            {
                fixedMood = 50f, fixedStress = 50f, starterPickRange = 3, rosterExtraMin = 0, rosterExtraMax = 2,
                familiarityMin = 5f, familiarityMax = 25f,
                clubNames = new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
                formDown = 4f, formUpBase = 4f, formUpPop = 8f, formStep = 2f, popMin = 20f, popMax = 80f,
            },
            report = new ReportConfig { minReasons = 2, maxReasons = 3, minContribution = 0.5f, evenThreshold = 0.05f },
            club = new ClubConfig { defaultName = "Rookie Stars", nameMaxLength = 16 },
        };

        /// <summary>One lineup of five neutral players (ids idBase+1..+5, one per lane, uniform radar = fill).</summary>
        public static Player[] MakePlayers(PlayerConfig pc, long idBase, float fill = 50f, float mood = 50f, float stress = 0f,
            string traitA = TeamTestFactory.NeutralA, string traitB = TeamTestFactory.NeutralB)
        {
            return Lanes.Select((l, i) =>
            {
                var p = TeamTestFactory.MakePlayer(pc, idBase + i + 1, l, fill, traitA, traitB, stress: stress);
                p.Mood = mood;
                return p;
            }).ToArray();
        }

        public static MatchLineup MakeLineup(Player[] players, string club = "X", float chemistry = 0f, MatchTactic tactic = MatchTactic.Develop,
            bool fixedState = false, float fixedMood = 50f, float fixedStress = 50f)
        {
            var map = new Dictionary<string, Player>();
            for (int i = 0; i < Lanes.Length; i++) map[Lanes[i]] = players[i];
            return new MatchLineup(club, map, chemistry, tactic, fixedState, fixedMood, fixedStress);
        }

        public static MatchSimulator MakeSimulator(MatchConfig m = null, PlayerConfig pc = null, TeamConfig tc = null)
        {
            pc = pc ?? MakePlayerConfig();
            return new MatchSimulator(m ?? MakeConfig(), tc ?? TeamTestFactory.MakeTeamConfig(), pc);
        }
    }
}
