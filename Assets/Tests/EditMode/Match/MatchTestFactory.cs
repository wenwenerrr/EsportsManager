using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;
using EsportsManager.Tests.Teams;
using JsonUtility = UnityEngine.JsonUtility;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Random source that returns scripted fractions u for NextFloat(0, 1) (then a default) and counts every call.</summary>
    internal sealed class UScriptedSource : IRandomSource
    {
        private readonly Queue<float> _script;
        private readonly float[] _cycle;
        private int _cycleIndex;
        /// <summary>Number of calls so far.</summary>
        public int Calls;
        /// <summary>Plays <paramref name="script"/> once, then repeats <paramref name="cycle"/> forever.</summary>
        public UScriptedSource(float[] script, params float[] cycle) { _script = new Queue<float>(script ?? new float[0]); _cycle = cycle.Length > 0 ? cycle : new[] { 0.5f }; }
        public int NextInt(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public float NextFloat(float minInclusive, float maxInclusive)
        {
            Calls++;
            float u = _script.Count > 0 ? _script.Dequeue() : _cycle[_cycleIndex++ % _cycle.Length];
            return minInclusive + (maxInclusive - minInclusive) * u;
        }
    }

    /// <summary>In-memory fixtures for the Story 011 match tests (design/gdd/match.md rev 4 initial values). No file I/O except the fixture readers.</summary>
    internal static class MatchTestFactory
    {
        public static readonly string[] Lanes = TeamTestFactory.Lanes;

        /// <summary>Real dimension ids per lane (same order as the shipped player_config.json).</summary>
        public static readonly string[][] DimIds =
        {
            new[] { "damage_taken_per_death", "damage_share", "engage", "teamfight_participation", "kda", "command" },
            new[] { "gold_per_min", "gold_conversion", "teamfight_participation", "kda", "kill_contribution", "command" },
            new[] { "damage_share", "teamfight_participation", "gold_conversion", "kda", "cc_duration", "command" },
            new[] { "farming", "gold_to_damage", "kda", "teamfight_participation", "survival", "command" },
            new[] { "teamfight_participation", "damage_taken_per_death", "cc_duration", "engage", "healing", "command" },
        };

        /// <summary>Phase mapping of match.md (lane order top, jungle, mid, adc, support; phases early, mid, late).</summary>
        public static readonly string[][][] PhaseDims =
        {
            new[] { new[] { "kda" }, new[] { "damage_taken_per_death", "engage", "teamfight_participation" }, new[] { "damage_share" } },
            new[] { new[] { "gold_per_min", "kda", "kill_contribution" }, new[] { "teamfight_participation" }, new[] { "gold_conversion" } },
            new[] { new[] { "kda" }, new[] { "teamfight_participation", "cc_duration" }, new[] { "damage_share", "gold_conversion" } },
            new[] { new[] { "kda" }, new[] { "teamfight_participation" }, new[] { "farming", "gold_to_damage", "survival" } },
            new[] { new[] { "teamfight_participation" }, new[] { "damage_taken_per_death", "cc_duration", "engage" }, new[] { "healing" } },
        };

        /// <summary>Lane weights Lw[k][lane] of match.md.</summary>
        public static readonly double[][] LaneWeights =
        {
            new[] { 1, 1.5, 1.2, 1, 1 }, new[] { 1.2, 1, 1, 1, 1.3 }, new[] { 1, 1, 1.2, 1.6, 1 },
        };

        /// <summary>Test player config with the real dimension ids and the match trait effects (resolute 0.95, assertive 1.05, decisive / hesitant, clever 1.03).</summary>
        public static PlayerConfig MakePlayerConfig()
        {
            var cfg = TestConfigFactory.Make(0.8f);
            for (int l = 0; l < cfg.lanes.Length; l++)
                for (int d = 0; d < 6; d++) cfg.lanes[l].dimensions[d].id = DimIds[l][d];
            cfg.traitEffects = cfg.traitEffects.Where(e => !(e.traitId == "clever" && e.effectKey == PlayerEffectKeys.OffLaneDiscount)).Concat(new[]
            {
                TestConfigFactory.Effect("clever", PlayerEffectKeys.OffLaneDiscount, 1.03f),
                TestConfigFactory.Effect("resolute", PlayerEffectKeys.StressVarianceImpact, 0.95f),
                TestConfigFactory.Effect("assertive", PlayerEffectKeys.Command, 1.05f),
                TestConfigFactory.Effect("decisive", PlayerEffectKeys.DeciderPerformance, 1.03f),
                TestConfigFactory.Effect("hesitant", PlayerEffectKeys.DeciderPerformance, 0.97f),
            }).ToArray();
            return cfg;
        }

        /// <summary>The match.md rev 4 initial values, built in code (the shipped file is compared against match.md separately).</summary>
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
            phases = new PhaseMappingConfig
            {
                commandDimensionId = "command",
                lanes = Lanes.Select((l, i) => new LanePhaseDef
                {
                    lane = l, early = PhaseDims[i][0], mid = PhaseDims[i][1], late = PhaseDims[i][2],
                    weightEarly = LaneWeights[0][i], weightMid = LaneWeights[1][i], weightLate = LaneWeights[2][i],
                }).ToArray(),
            },
            effective = new EffectiveConfig { moodFloor = 0.95, stressFactor = 0.025 },
            strength = new PhaseStrengthConfig
            {
                shortBoardEarly = 0.15, shortBoardMid = 0.5, shortBoardLate = 0.15, midChemistry = 1, starFactor = 0.3,
                starThreshold = 4, starCap = 15, phaseWeightEarly = 1, phaseWeightMid = 1.1, phaseWeightLate = 1,
            },
            execution = new ExecutionConfig { e0 = 0.3, e1 = 0.4, e2 = 0.3, min = 0.3, max = 1 },
            cycle = new CycleConfig
            {
                strength = 0.22, weightMin = 0.4,
                relations = new[]
                {
                    new CycleRelationDef { beats = MatchPhase.Early, weakens = MatchPhase.Late },
                    new CycleRelationDef { beats = MatchPhase.Mid, weakens = MatchPhase.Early },
                    new CycleRelationDef { beats = MatchPhase.Late, weakens = MatchPhase.Mid },
                },
            },
            tactic = new TacticConfig
            {
                fitScale = 0.1, fitCap = 1, counter = 0.5, budget = 3,
                focus = new[]
                {
                    new TacticFocusDef { tactic = MatchTactic.Invade, phase = MatchPhase.Early },
                    new TacticFocusDef { tactic = MatchTactic.Teamfight, phase = MatchPhase.Mid },
                    new TacticFocusDef { tactic = MatchTactic.Develop, phase = MatchPhase.Late },
                },
                counters = new[]
                {
                    new TacticCounterDef { tactic = MatchTactic.Invade, beats = MatchTactic.Develop },
                    new TacticCounterDef { tactic = MatchTactic.Develop, beats = MatchTactic.Teamfight },
                    new TacticCounterDef { tactic = MatchTactic.Teamfight, beats = MatchTactic.Invade },
                },
            },
            noise = new NoiseConfig { g0 = 2, g1 = 6, commandReduction = 0.3 },
            winProbability = new WinProbabilityConfig { k = 8, pFloor = 0.15 },
            specialty = new SpecialtyConfig { phaseFlatCap = 5, starScaleMin = 0.5, starScaleMax = 2, sigmaScaleMin = 0.7, sigmaScaleMax = 1.3 },
            opponent = new OpponentConfig
            {
                starterPickRange = 3, rosterExtraMin = 0, rosterExtraMax = 2, familiarityMin = 5f, familiarityMax = 25f,
                clubNames = new[] { "A", "B", "C", "D", "E", "F", "G", "H" }, popMin = 20f, popMax = 80f,
            },
            report = new ReportConfig { minReasons = 2, maxReasons = 3, minContribution = 0.5, evenThreshold = 0.05, luckThreshold = 0.6, tacticAdviceMin = 0.5 },
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

        public static Dictionary<string, Player> Map(Player[] players)
        {
            var map = new Dictionary<string, Player>();
            for (int i = 0; i < Lanes.Length; i++) map[Lanes[i]] = players[i];
            return map;
        }

        public static MatchLineup MakeLineup(Player[] players, string club = "X", float chemistry = 0f, MatchTactic tactic = MatchTactic.Develop,
            IEnumerable<SpecialtyEffect> hooks = null)
            => new MatchLineup(club, Map(players), chemistry, tactic, hooks);

        public static MatchSimulator MakeSimulator(MatchConfig m = null, PlayerConfig pc = null, TeamConfig tc = null)
        {
            pc = pc ?? MakePlayerConfig();
            return new MatchSimulator(m ?? MakeConfig(), tc ?? TeamTestFactory.MakeTeamConfig(), pc);
        }

        // ------------------------------------------------------------ worked example (match.md "完整示例")

        public static readonly double[][] ExampleA = { new double[] { 66, 70, 66, 64, 66 }, new double[] { 60, 62, 60, 58, 62 }, new double[] { 56, 56, 57, 56, 57 } };
        public static readonly double[][] ExampleB = { new double[] { 58, 60, 58, 56, 58 }, new double[] { 62, 62, 63, 60, 62 }, new double[] { 70, 68, 70, 72, 68 } };

        /// <summary>A phase-rating-level side: ratings[phase][lane], chemistry 70, command 70, no stress.</summary>
        public static SideInput PhaseSide(double[][] byPhase, double chemistry = 70, double command = 70, IReadOnlyList<SpecialtyEffect> hooks = null)
        {
            var r = new double[5][];
            for (int i = 0; i < 5; i++) r[i] = new[] { byPhase[0][i], byPhase[1][i], byPhase[2][i] };
            return new SideInput { Ratings = r, Chemistry = chemistry, Command = command, AverageStress = 0, AverageResolute = 1, Effects = hooks ?? new SpecialtyEffect[0] };
        }

        public static GameEvaluation Evaluate(MatchConfig mc, PhaseLayout layout, SideInput a, SideInput b, MatchTactic ta, MatchTactic tb)
            => MatchFormulas.EvaluateGame(mc, MatchFormulas.EvaluateSide(mc, layout, a, ta), MatchFormulas.EvaluateSide(mc, layout, b, tb));

        // ------------------------------------------------------------ file fixtures

        public const string FixtureDir = "Assets/Tests/EditMode/Match/Fixtures/";

        [Serializable] public class FxPlayer { public long id; public string lane; public string trait_a; public string trait_b; public float[] radar; }
        [Serializable] public class FxSide { public string club; public float chemistry; public FxPlayer[] players; }
        [Serializable] public class FxLineups { public float mood; public float stress; public FxSide[] sides; }

        /// <summary>Fixture 2 (lineup_full_example.json): both sides as players; mood 100 / stress 0 / main lane so v' = v.</summary>
        public static Player[][] LoadExamplePlayers(PlayerConfig pc, out float chemA, out float chemB)
        {
            var fx = JsonUtility.FromJson<FxLineups>(File.ReadAllText(FixtureDir + "lineup_full_example.json"));
            var res = new Player[2][];
            for (int s = 0; s < 2; s++)
                res[s] = fx.sides[s].players.Select(p =>
                {
                    var radar = TestConfigFactory.FilledRadar(pc, 0f);
                    radar[p.lane] = p.radar;
                    return new Player(pc, p.id, fx.sides[s].club + p.lane, 22, p.lane, p.trait_a, p.trait_b, 80f, 2, 50f, 50f, fx.mood, fx.stress, 1000, radar);
                }).ToArray();
            chemA = fx.sides[0].chemistry; chemB = fx.sides[1].chemistry;
            return res;
        }

        [Serializable] public class FxGame { public bool a_wins; public bool decider; public double u_a; public double u_b; public double roll; public double eps_a; public double eps_b; public double g0; public double g; public double p; }
        [Serializable] public class FxGolden { public int seed; public int best_of; public string tactic_a; public string tactic_b; public double sigma_a; public double sigma_b; public FxGame[] games; }

        public static FxGolden LoadGolden() => JsonUtility.FromJson<FxGolden>(File.ReadAllText(FixtureDir + "golden_bo5_full_example.json"));

        [Serializable] public class FxReportGame { public bool a_won; public float p0; public float[] cycle; public float[] values; }
        [Serializable] public class FxReport
        {
            public int best_of; public bool a_won_series; public string[] expected_reasons; public int expected_luck_game; public bool expected_even;
            public bool expected_cycle; public FxReportGame[] games;
        }

        public static FxReport LoadReportFixture() => JsonUtility.FromJson<FxReport>(File.ReadAllText(FixtureDir + "report_reasons_bo3.json"));
    }
}
