using System.Collections.Generic;
using System.Linq;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;
using EsportsManager.Tests.Teams;

namespace EsportsManager.Tests.Segments
{
    /// <summary>
    /// Random source for tests: NextFloat returns min + (max-min) x fraction where the fractions come first from a
    /// script, then from a default. Counts every draw. NextInt returns min.
    /// </summary>
    internal sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<float> _script;
        private readonly float _default;
        public int Draws;
        public ScriptedRandomSource(float defaultFraction, params float[] script)
        {
            _default = defaultFraction; _script = new Queue<float>(script);
        }
        public int NextInt(int minInclusive, int maxExclusive) { Draws++; return minInclusive; }
        public float NextFloat(float minInclusive, float maxInclusive)
        {
            Draws++;
            float f = _script.Count > 0 ? _script.Dequeue() : _default;
            return minInclusive + (maxInclusive - minInclusive) * f;
        }
    }

    /// <summary>In-memory fixtures for Story 003 tests: round-number configs, neutral players. No file I/O.</summary>
    internal static class SegmentTestFactory
    {
        public const string NeutralA = "reserved";
        public const string NeutralB = "idealistic";

        /// <summary>Round-number numbers.md initial values; 6 action points.</summary>
        public static SegmentConfig MakeConfig() => new SegmentConfig
        {
            actionPoints = new ActionPointConfig { perSegment = 6, maxPerPlayer = 2 },
            training = new SegmentTrainingConfig { intensity = 1f, targetBonus = 1.5f },
            stream = new StreamConfig { cap = 2f },
            business = new BusinessConfig
            {
                weightPopularity = 1.5f, weightLaneScore = 0.5f, weightLooks = 1f, maxProbability = 0.4f,
                baseAmount = 5000, amountPerPopularity = 100f, amountPerLooks = 80f, randomRange = 0.2f,
                clubShare = 0.7f, heatCap = 1f,
            },
            sponsorship = new SponsorshipConfig
            {
                weightClubPopularity = 1f, weightWinRate = 1f, maxSuccessRate = 0.8f, randomSpread = 0.1f,
                minChance = 0.05f, maxChance = 0.9f, baseAmount = 6000, amountPerClubPopularity = 100f, durationSegments = 4,
                maxAttemptsPerSegment = 3, maxActiveContracts = 3,
            },
            mood = new MoodConfig
            {
                result = 8f, play = 4f, salary = 10f, chemistry = 5f, stress = 6f, regression = 0.05f, neutral = 50f,
                highStressThreshold = 70f, randomRange = 0.15f,
            },
            stress = new StressConfig
            {
                training = 6f, match = 2f, loss = 3f, business = 4f, rest = 15f, stream = 3f, randomRange = 0.1f,
                recovery = 0f, // 0 so single-term tests stay readable; recovery has its own tests

            },
        };

        /// <summary>Player config of Story 002 tests plus the effect keys Story 003 reads (exaggerated so tests see them).</summary>
        public static PlayerConfig MakePlayerConfig()
        {
            var cfg = TeamTestFactory.MakePlayerConfig();
            cfg.traitEffects = cfg.traitEffects.Concat(new[]
            {
                TestConfigFactory.Effect("earnest", PlayerEffectKeys.BusinessRate, 0.5f),
                TestConfigFactory.Effect("lively", PlayerEffectKeys.BusinessRate, 3f),
                TestConfigFactory.Effect("slick", PlayerEffectKeys.BusinessAmount, 1.5f),
                TestConfigFactory.Effect("lazy", PlayerEffectKeys.StressGain, 0.5f),
                TestConfigFactory.Effect("fragile", PlayerEffectKeys.LossMoodDrop, 2f),
                TestConfigFactory.Effect("pessimistic", PlayerEffectKeys.NegativeMoodChange, 2f),
                TestConfigFactory.Effect("decisive", PlayerEffectKeys.WinMoodGain, 2f),
                TestConfigFactory.Effect("selfish", PlayerEffectKeys.BenchMoodDrop, 3f),
                TestConfigFactory.Effect("pragmatic", PlayerEffectKeys.SalarySatisfactionWeight, 2f),
                TestConfigFactory.Effect("honest", PlayerEffectKeys.MoodChangeMagnitude, 0.5f),
            }).ToArray();
            return cfg;
        }

        public static SegmentContext Ctx(params bool[] results) =>
            new SegmentContext { SegmentsPerYear = 12, SegmentIndexInYear = 0, MatchResults = results.Length == 0 ? null : results };

        /// <summary>Team with five neutral starters (ids 1-5, lanes top..support, salary = expected, mood 50, stress 0) and a runner.</summary>
        public sealed class Fixture
        {
            public readonly PlayerConfig Players = MakePlayerConfig();
            public readonly TeamConfig TeamCfg = TeamTestFactory.MakeTeamConfig();
            public readonly SegmentConfig Cfg = MakeConfig();
            public readonly Team Team;
            public readonly Player[] Starters;
            public SegmentRunner Runner;
            public readonly IRandomSource Rng;

            public Fixture(IRandomSource runnerRng = null, IRandomSource teamRng = null, bool lineup = true, long funds = 0)
            {
                Rng = runnerRng ?? new StubRandomSource(0.5f);
                Team = new Team(TeamCfg, Players, teamRng ?? new StubRandomSource(0.5f), funds);
                Starters = TeamTestFactory.Lanes.Select((l, i) => MakePlayer(i + 1, l)).ToArray();
                for (int i = 0; i < 5; i++)
                {
                    Team.AddPlayer(Starters[i]);
                    if (lineup) Team.SetStarter(TeamTestFactory.Lanes[i], Starters[i]);
                }
                Runner = new SegmentRunner(Cfg, Players, Team, Rng);
            }

            /// <summary>Creates a player (salary = expected salary so satisfaction is 0). Not added to the roster.</summary>
            public Player MakePlayer(long id, string lane, string a = NeutralA, string b = NeutralB, float stress = 0f, float fill = 50f)
            {
                var p = TeamTestFactory.MakePlayer(Players, id, lane, fill: fill, traitA: a, traitB: b, stress: stress);
                p.Salary = p.CalculateSalaryWithoutRandom();
                return p;
            }

            /// <summary>Adds a bench player.</summary>
            public Player AddBench(long id, string lane = "mid", string a = NeutralA, string b = NeutralB, float stress = 0f)
            {
                var p = MakePlayer(id, lane, a, b, stress);
                Team.AddPlayer(p);
                return p;
            }

            /// <summary>BeginSegment + Resolve with a default context.</summary>
            public SegmentSummary Run(SegmentPlan plan, SegmentContext? ctx = null)
            {
                Runner.BeginSegment();
                return Runner.Resolve(plan, ctx ?? Ctx());
            }
        }
    }
}
