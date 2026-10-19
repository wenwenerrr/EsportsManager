using System.Linq;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;

namespace EsportsManager.Tests.Teams
{
    /// <summary>In-memory fixtures for Story 002 tests: round-number configs, neutral players, a ready-made team. No file I/O.</summary>
    internal static class TeamTestFactory
    {
        public static readonly string[] Lanes = { "top", "jungle", "mid", "adc", "support" };
        public const string NeutralA = "reserved";
        public const string NeutralB = "idealistic";

        public static PlayerConfig MakePlayerConfig()
        {
            var cfg = TestConfigFactory.Make();
            cfg.traitEffects = cfg.traitEffects.Concat(new[]
            {
                TestConfigFactory.Effect("resolute", PlayerEffectKeys.StressVarianceImpact, 0.5f),
            }).ToArray();
            return cfg;
        }

        public static TeamConfig MakeTeamConfig()
        {
            return new TeamConfig
            {
                laneWeights = Lanes.Select(l => new LaneWeightDef { lane = l, weight = 1f }).ToArray(),
                strength = new StrengthConfig { shortBoardPenalty = 0.3f, stressFactor = 0.2f, moodFactorBase = 0.7f, moodFactorSpan = 0.3f },
                familiarity = new FamiliarityConfig { perSegment = 5f, perWin = 2f, max = 100f, decay = 1f, compatibilityFactor = 0.5f },
                popularity = new PopularityConfig
                {
                    alpha = 0.25f, randomRange = 0.15f, weightClub = 1f, weightLaneScore = 1f, weightLooks = 0.8f,
                    weightStream = 1.5f, weightWinRate = 1f, weightBusiness = 0.8f,
                },
                clubPopularity = new ClubPopularityConfig
                {
                    initial = 30f, beta = 0.2f, randomRange = 0.1f, weightStarterPopularity = 1f, weightWinRate = 1f, weightRank = 0.5f,
                },
                results = new ResultsConfig { window = 5, defaultWinRate = 50f },
                livestream = new LivestreamConfig { baseIncome = 500, incomePerPopularity = 20f, secondStreamFactor = 1f }, // 1 = no reduction; tests that need 0.5 set it
            };
        }

        /// <summary>Player in his main lane with a uniform radar (lane score = fill), mood 50, looks 50, neutral traits by default.</summary>
        public static Player MakePlayer(PlayerConfig pc, long id, string lane, float fill = 50f, string traitA = NeutralA,
            string traitB = NeutralB, float popularity = 50f, float stress = 0f, int salary = 120000)
        {
            return TestConfigFactory.MakePlayer(pc, mainLane: lane, fill: fill, traitA: traitA, traitB: traitB,
                popularity: popularity, stress: stress, salary: salary, id: id);
        }

        /// <summary>A team with five neutral starters (ids 1..5, one per lane, fill 50, popularity 50).</summary>
        public sealed class Fixture
        {
            public PlayerConfig Players = MakePlayerConfig();
            public TeamConfig Config = MakeTeamConfig();
            public Team Team;
            public Player[] Starters;

            public Fixture(IRandomSource rng = null, long funds = 0, bool lineup = true)
            {
                Team = new Team(Config, Players, rng ?? new StubRandomSource(0.5f), funds);
                Starters = Lanes.Select((l, i) => MakePlayer(Players, i + 1, l)).ToArray();
                if (!lineup) return;
                for (int i = 0; i < 5; i++) { Team.AddPlayer(Starters[i]); Team.SetStarter(Lanes[i], Starters[i]); }
            }

            public Player AddBench(long id, string lane = "mid", int salary = 120000)
            {
                var p = MakePlayer(Players, id, lane, salary: salary);
                Team.AddPlayer(p);
                return p;
            }

            public void SetAllStarterPairsF(float f)
            {
                for (int i = 0; i < 5; i++)
                    for (int j = i + 1; j < 5; j++) Team.SetFamiliarity(Starters[i].Id, Starters[j].Id, f);
            }
        }

        public static SegmentInput Input(params bool[] results) =>
            new SegmentInput { SegmentsPerYear = 12, SegmentIndexInYear = 0, MatchResults = results, SeasonRankScore = 0f };
    }
}
