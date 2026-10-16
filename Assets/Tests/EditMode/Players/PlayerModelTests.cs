using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Players;
using NUnit.Framework;

namespace EsportsManager.Tests.Players
{
    /// <summary>EditMode tests for the player model basics (Story 001, kept under Story 008). Config is built in memory; no file I/O.</summary>
    public class PlayerModelTests
    {
        private const int FixedSeed = TestConfigFactory.FixedSeed;
        private static readonly string[][] LaneTable = TestConfigFactory.LaneTable;

        private static PlayerConfig MakeConfig(float discount = 0.8f) => TestConfigFactory.Make(discount);

        private static Player MakePlayer(PlayerConfig cfg, int age = 22, string mainLane = "mid", float fill = 50f) =>
            TestConfigFactory.MakePlayer(cfg, age, mainLane, fill);

        [Test]
        public void NewPlayer_HasAllRequiredFieldsAndRadarForEveryLane()
        {
            var cfg = MakeConfig();
            var p = new Player(cfg, 42, "小明", 21, "adc", "diligent", "gentle", 77f, 3, 60f, 40f, 70f, 20f, 3000);

            Assert.AreEqual(42L, p.Id);
            Assert.AreEqual("小明", p.Name);
            Assert.AreEqual(21, p.Age);
            Assert.AreEqual("adc", p.MainLane);
            CollectionAssert.AreEqual(new[] { "diligent", "gentle" }, p.Traits.ToArray());
            Assert.AreEqual(77f, p.Potential);
            Assert.AreEqual(3, p.ContractYearsLeft);
            Assert.AreEqual(60f, p.Looks);
            Assert.AreEqual(40f, p.Popularity);
            Assert.AreEqual(70f, p.Mood);
            Assert.AreEqual(20f, p.Stress);
            Assert.AreEqual(3000, p.Salary);
            foreach (var lane in cfg.lanes)
                Assert.AreEqual(lane.dimensions.Length, p.GetRadar(lane.id).Length);
        }

        [Test]
        public void LaneTable_HasFiveLanesWithSixDimensionsMatchingBrief()
        {
            var cfg = MakeConfig();
            Assert.AreEqual(5, cfg.lanes.Length);
            for (int i = 0; i < LaneTable.Length; i++)
            {
                Assert.AreEqual(6, cfg.lanes[i].dimensions.Length, cfg.lanes[i].id);
                CollectionAssert.AreEqual(LaneTable[i].Skip(1).ToArray(),
                    cfg.lanes[i].dimensions.Select(d => d.displayName).ToArray());
            }
        }

        [Test]
        public void LaneScore_IsWeightedAverageOfLaneDimensions()
        {
            var cfg = MakeConfig();
            var p = MakePlayer(cfg, mainLane: "mid");
            // weights 2,1,1,1,1,1 (sum 7); values 80,50,50,50,50,50 -> (160+250)/7
            p.SetDimension("mid", 0, 80f);
            Assert.AreEqual(410f / 7f, p.GetLaneScore("mid"), 1e-4f);
        }

        [Test]
        public void LaneScore_ChangesWhenWeightsChange()
        {
            var cfg = MakeConfig();
            var p = MakePlayer(cfg, mainLane: "mid");
            p.SetDimension("mid", 0, 100f);
            float before = p.GetLaneScore("mid");
            cfg.lanes.First(l => l.id == "mid").dimensions[0].weight = 10f;
            Assert.Greater(p.GetLaneScore("mid"), before);
        }

        [Test]
        public void OffLaneScore_IsMainLaneStyleScoreTimesDiscount()
        {
            var cfg = MakeConfig(0.75f);
            var p = MakePlayer(cfg, mainLane: "mid", fill: 60f);
            // identical radar on every lane, so only the discount differs
            Assert.AreEqual(60f, p.GetLaneScore("mid"), 1e-4f);
            Assert.AreEqual(45f, p.GetLaneScore("top"), 1e-4f);
        }

        [Test]
        public void Validate_DiscountOfOneOrMore_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => MakeConfig(1f).Validate());
            Assert.Throws<InvalidOperationException>(() => MakeConfig(1.2f).Validate());
            Assert.DoesNotThrow(() => MakeConfig(0.9f).Validate());
        }

        [Test]
        public void StatSetters_ClampToZeroAndHundred()
        {
            var cfg = MakeConfig();
            var p = MakePlayer(cfg);
            p.Mood = 150f; p.Stress = -5f; p.Looks = 101f; p.Popularity = -1f;
            Assert.AreEqual(100f, p.Mood);
            Assert.AreEqual(0f, p.Stress);
            Assert.AreEqual(100f, p.Looks);
            Assert.AreEqual(0f, p.Popularity);
        }

        [Test]
        public void DimensionSetterAndConstructor_ClampToZeroAndHundred()
        {
            var cfg = MakeConfig();
            var p = MakePlayer(cfg);
            p.SetDimension("top", 0, 250f);
            p.SetDimension("top", 1, -30f);
            Assert.AreEqual(100f, p.GetDimension("top", 0));
            Assert.AreEqual(0f, p.GetDimension("top", 1));

            var radar = cfg.lanes.ToDictionary(l => l.id, l => Enumerable.Repeat(500f, l.dimensions.Length).ToArray());
            var q = new Player(cfg, 2, "Q", 20, "top", "diligent", "gentle", 500f, 1, 500f, -9f, 500f, 500f, 1, radar);
            Assert.AreEqual(100f, q.GetDimension("mid", 3));
            Assert.AreEqual(100f, q.Looks);
            Assert.AreEqual(0f, q.Popularity);
        }

        [Test]
        public void Pool_HasAtLeastFivePlayersPerLaneAndTwentyFiveTotal()
        {
            var cfg = MakeConfig();
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(FixedSeed)).GeneratePool();
            Assert.GreaterOrEqual(pool.Count, 25);
            foreach (var lane in cfg.lanes)
                Assert.GreaterOrEqual(pool.Count(p => p.MainLane == lane.id), 5, lane.id);
            Assert.AreEqual(pool.Count, pool.Select(p => p.Name).Distinct().Count(), "names unique");
        }

        [Test]
        public void Pool_StaysInsideConfiguredRanges()
        {
            var cfg = MakeConfig();
            var g = cfg.generation;
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(FixedSeed)).GeneratePool();
            foreach (var p in pool)
            {
                Assert.That(p.Age, Is.InRange(g.minAge, g.maxAge));
                Assert.GreaterOrEqual(p.Salary, cfg.salary.minSalary); // no salary cap since numbers.md v2
                Assert.That(p.Mood, Is.InRange(g.moodMin, g.moodMax));
                foreach (float v in p.GetRadar(p.MainLane))
                    // main-lane dimensions are potential x U(0.6, 0.95) since Story 008
                    Assert.That(v, Is.InRange(p.Potential * g.mainLaneDimFractionMin - 1e-3f, p.Potential * g.mainLaneDimFractionMax + 1e-3f));
            }
        }

        [Test]
        public void Pool_SameSeed_IsIdentical()
        {
            var cfg = MakeConfig();
            var a = new PlayerGenerator(cfg, new SeededRandomSource(FixedSeed)).GeneratePool();
            var b = new PlayerGenerator(cfg, new SeededRandomSource(FixedSeed)).GeneratePool();
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Name, b[i].Name);
                Assert.AreEqual(a[i].Age, b[i].Age);
                CollectionAssert.AreEqual(a[i].Traits.ToArray(), b[i].Traits.ToArray());
                Assert.AreEqual(a[i].Potential, b[i].Potential);
                Assert.AreEqual(a[i].ContractYearsLeft, b[i].ContractYearsLeft);
                Assert.AreEqual(a[i].Salary, b[i].Salary);
                Assert.AreEqual(a[i].Mood, b[i].Mood);
                foreach (var lane in cfg.lanes)
                    CollectionAssert.AreEqual(a[i].GetRadar(lane.id), b[i].GetRadar(lane.id));
            }
        }

        [Test]
        public void Pool_DifferentSeed_Differs()
        {
            var cfg = MakeConfig();
            var a = new PlayerGenerator(cfg, new SeededRandomSource(1)).GeneratePool();
            var b = new PlayerGenerator(cfg, new SeededRandomSource(2)).GeneratePool();
            Assert.IsFalse(a.Select(p => p.GetLaneScore(p.MainLane)).SequenceEqual(b.Select(p => p.GetLaneScore(p.MainLane))));
        }

        // ---- Validate() rules ----

        private static void AssertInvalid(PlayerConfig cfg, string expectedFragment)
        {
            var ex = Assert.Throws<InvalidOperationException>(cfg.Validate);
            StringAssert.Contains(expectedFragment, ex.Message);
        }

        [Test]
        public void Validate_FixtureConfig_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MakeConfig().Validate());
        }

        [Test]
        public void Validate_GenerationRangeMinAboveMax_Throws()
        {
            var setters = new Action<GenerationConfig>[]
            {
                g => g.minAge = g.maxAge + 1,
                g => g.mainLaneDimFractionMin = g.mainLaneDimFractionMax + 0.01f,
                g => g.offLaneDimFractionMin = g.offLaneDimFractionMax + 0.01f,
                g => g.looksMin = g.looksMax + 1,
                g => g.popularityMin = g.popularityMax + 1,
                g => g.moodMin = g.moodMax + 1,
                g => g.stressMin = g.stressMax + 1,
                g => g.normalPotentialMin = g.normalPotentialMax + 1,
                g => g.talentPotentialMin = g.talentPotentialMax + 1,
                g => g.contractYearsMin = g.contractYearsMax + 1,
            };
            for (int i = 0; i < setters.Length; i++)
            {
                var cfg = MakeConfig();
                setters[i](cfg.generation);
                var ex = Assert.Throws<InvalidOperationException>(cfg.Validate, "range #" + i);
                StringAssert.Contains("> max", ex.Message);
            }
        }

        [Test]
        public void Validate_PlayersPerLaneZeroOrNegative_Throws()
        {
            var cfg = MakeConfig();
            cfg.generation.playersPerLane = 0;
            AssertInvalid(cfg, "playersPerLane");
            cfg.generation.playersPerLane = -1;
            AssertInvalid(cfg, "playersPerLane");
        }

        [Test]
        public void Validate_DuplicateLaneIds_Throws()
        {
            var cfg = MakeConfig();
            cfg.lanes[1].id = cfg.lanes[0].id;
            AssertInvalid(cfg, "duplicate lane id");
        }

        [Test]
        public void Validate_DuplicateDimensionIdsWithinLane_Throws()
        {
            var cfg = MakeConfig();
            cfg.lanes[2].dimensions[3].id = cfg.lanes[2].dimensions[0].id;
            AssertInvalid(cfg, "duplicate dimension id");
        }

        [Test]
        public void Validate_SameDimensionIdInDifferentLanes_IsAllowed()
        {
            var cfg = MakeConfig();
            cfg.lanes[1].dimensions[0].id = cfg.lanes[0].dimensions[0].id;
            Assert.DoesNotThrow(cfg.Validate);
        }

        [Test]
        public void Validate_LaneWeightsSumToZero_Throws()
        {
            var cfg = MakeConfig();
            foreach (var d in cfg.lanes[0].dimensions) d.weight = 0f;
            AssertInvalid(cfg, "sum to 0");
        }

        [Test]
        public void Validate_NegativeWeight_Throws()
        {
            var cfg = MakeConfig();
            cfg.lanes[0].dimensions[0].weight = -1f;
            AssertInvalid(cfg, "weight must be >= 0");
        }
    }
}
