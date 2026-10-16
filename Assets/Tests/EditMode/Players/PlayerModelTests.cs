using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EsportsManager.Tests.Players
{
    /// <summary>EditMode tests for Story 001 (player model). Config is built in memory; no file I/O.</summary>
    public class PlayerModelTests
    {
        private const int FixedSeed = 12345;

        // Fixture: lane id -> six dimension display names, copied from design/game-brief.md.
        private static readonly string[][] LaneTable =
        {
            new[] { "top", "每死承伤", "伤害占比", "开团能力", "参团率", "KDA", "指挥能力" },
            new[] { "jungle", "分均经济", "经济转化", "参团率", "KDA", "击杀贡献", "指挥能力" },
            new[] { "mid", "伤害占比", "参团率", "经济转化", "KDA", "控制时长", "指挥能力" },
            new[] { "adc", "发育能力", "经济转伤害", "KDA", "参团率", "生存能力", "指挥能力" },
            new[] { "support", "参团率", "每死承伤", "控制时长", "开团能力", "治疗量", "指挥能力" },
        };

        private static PlayerConfig MakeConfig(float discount = 0.8f)
        {
            var lanes = LaneTable.Select(row => new LaneDef
            {
                id = row[0],
                displayName = row[0],
                dimensions = row.Skip(1).Select((n, i) => new DimensionDef
                {
                    id = row[0] + "_" + i,
                    displayName = n,
                    weight = i == 0 ? 2f : 1f // first dimension weighs double
                }).ToArray()
            }).ToArray();

            return new PlayerConfig
            {
                offLaneDiscount = discount,
                personalities = new[] { "a", "b", "c", "d" },
                lanes = lanes,
                growthBands = new[]
                {
                    new GrowthBand { minAge = 0, maxAge = 19, deltaPerYear = 5f },
                    new GrowthBand { minAge = 20, maxAge = 27, deltaPerYear = 0f },
                    new GrowthBand { minAge = 28, maxAge = 200, deltaPerYear = -4f },
                },
                generation = new GenerationConfig
                {
                    playersPerLane = 5,
                    minAge = 17, maxAge = 30,
                    mainLaneDimMin = 55, mainLaneDimMax = 90,
                    offLaneDimMin = 30, offLaneDimMax = 70,
                    looksMin = 30, looksMax = 90,
                    popularityMin = 5, popularityMax = 60,
                    moodMin = 50, moodMax = 80,
                    stressMin = 10, stressMax = 40,
                    salaryMin = 1000, salaryMax = 5000,
                    surnames = new[] { "S1", "S2", "S3" },
                    givenNames = new[] { "G1", "G2", "G3" },
                }
            };
        }

        private static Player MakePlayer(PlayerConfig cfg, int age = 22, string mainLane = "mid", float fill = 50f)
        {
            var radar = cfg.lanes.ToDictionary(l => l.id, l => Enumerable.Repeat(fill, l.dimensions.Length).ToArray());
            return new Player(cfg, "Test", age, mainLane, "a", 50f, 50f, 50f, 50f, 1000, radar);
        }

        [Test]
        public void NewPlayer_HasAllRequiredFieldsAndRadarForEveryLane()
        {
            var cfg = MakeConfig();
            var p = new Player(cfg, "小明", 21, "adc", "b", 60f, 40f, 70f, 20f, 3000);

            Assert.AreEqual("小明", p.Name);
            Assert.AreEqual(21, p.Age);
            Assert.AreEqual("adc", p.MainLane);
            Assert.AreEqual("b", p.Personality);
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
            var q = new Player(cfg, "Q", 20, "top", "a", 500f, -9f, 500f, 500f, 1, radar);
            Assert.AreEqual(100f, q.GetDimension("mid", 3));
            Assert.AreEqual(100f, q.Looks);
            Assert.AreEqual(0f, q.Popularity);
        }

        [Test]
        public void ConfigPersonalities_AreFiniteSetOfFourToSixAndGeneratedPlayersUseOnlyThem()
        {
            var cfg = MakeConfig();
            Assert.That(cfg.personalities.Length, Is.InRange(4, 6));
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(FixedSeed)).GeneratePool();
            Assert.IsTrue(pool.All(p => cfg.personalities.Contains(p.Personality)));
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
                Assert.That(p.Salary, Is.InRange(g.salaryMin, g.salaryMax));
                Assert.That(p.Mood, Is.InRange(g.moodMin, g.moodMax));
                foreach (float v in p.GetRadar(p.MainLane))
                    Assert.That(v, Is.InRange(g.mainLaneDimMin, g.mainLaneDimMax));
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
                Assert.AreEqual(a[i].Personality, b[i].Personality);
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

        [Test]
        public void AdvanceYear_YoungPlayerImproves()
        {
            var cfg = MakeConfig();
            var p = MakePlayer(cfg, age: 18, fill: 50f);
            p.AdvanceYear();
            Assert.AreEqual(19, p.Age);
            Assert.AreEqual(55f, p.GetDimension("mid", 2), 1e-4f);
            Assert.AreEqual(55f, p.GetDimension("top", 5), 1e-4f);
        }

        [Test]
        public void AdvanceYear_VeteranDeclines()
        {
            var cfg = MakeConfig();
            var p = MakePlayer(cfg, age: 30, fill: 50f);
            p.AdvanceYear();
            Assert.AreEqual(31, p.Age);
            Assert.AreEqual(46f, p.GetDimension("mid", 2), 1e-4f);
        }

        [Test]
        public void AdvanceYear_PeakBandIsFlat_AndResultsStayClamped()
        {
            var cfg = MakeConfig();
            var peak = MakePlayer(cfg, age: 24, fill: 50f);
            peak.AdvanceYear();
            Assert.AreEqual(50f, peak.GetDimension("mid", 0), 1e-4f);

            var young = MakePlayer(cfg, age: 18, fill: 98f);
            young.AdvanceYear();
            Assert.AreEqual(100f, young.GetDimension("mid", 0));
            var old = MakePlayer(cfg, age: 35, fill: 2f);
            old.AdvanceYear();
            Assert.AreEqual(0f, old.GetDimension("mid", 0));
        }

        [Test]
        public void Loader_ParsesJsonAndRejectsInvalidDiscount()
        {
            const string valid = "{\"offLaneDiscount\":0.5,\"personalities\":[\"a\"],\"lanes\":[{\"id\":\"x\",\"displayName\":\"X\",\"dimensions\":[{\"id\":\"d\",\"displayName\":\"D\",\"weight\":1}]}],\"growthBands\":[{\"minAge\":0,\"maxAge\":99,\"deltaPerYear\":0}],\"generation\":{\"playersPerLane\":1,\"surnames\":[\"s\"],\"givenNames\":[\"g\"]}}";
            var cfg = PlayerConfigLoader.FromJson(valid);
            Assert.AreEqual(0.5f, cfg.offLaneDiscount);
            Assert.AreEqual("x", cfg.lanes[0].id);
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(valid.Replace("0.5", "1.5")));
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
        public void Validate_EmptyGrowthBands_Throws()
        {
            var cfg = MakeConfig();
            cfg.growthBands = new GrowthBand[0];
            AssertInvalid(cfg, "growthBands");
        }

        [Test]
        public void Validate_GrowthBandMinAboveMax_Throws()
        {
            var cfg = MakeConfig();
            cfg.growthBands[1].minAge = 30; // 30 > maxAge 27
            AssertInvalid(cfg, "minAge");
        }

        [Test]
        public void Validate_OverlappingGrowthBands_Throws()
        {
            var cfg = MakeConfig();
            cfg.growthBands[1].minAge = 19; // band 0 ends at 19
            AssertInvalid(cfg, "overlaps");
        }

        [Test]
        public void Validate_GapBetweenGrowthBands_IsAllowedAndMeansNoChange()
        {
            var cfg = MakeConfig();
            cfg.growthBands[1].minAge = 21; // age 20 is now uncovered
            Assert.DoesNotThrow(cfg.Validate);
            Assert.AreEqual(0f, cfg.GetGrowthDelta(20));
        }

        [Test]
        public void Validate_GenerationRangeMinAboveMax_Throws()
        {
            var setters = new Action<GenerationConfig>[]
            {
                g => g.minAge = g.maxAge + 1,
                g => g.mainLaneDimMin = g.mainLaneDimMax + 1,
                g => g.offLaneDimMin = g.offLaneDimMax + 1,
                g => g.looksMin = g.looksMax + 1,
                g => g.popularityMin = g.popularityMax + 1,
                g => g.moodMin = g.moodMax + 1,
                g => g.stressMin = g.stressMax + 1,
                g => g.salaryMin = g.salaryMax + 1,
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

        [Test]
        public void Loader_MalformedJson_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson("{ \"lanes\": [ {"));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson("not json at all"));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(""));
        }

        // Integration-style: reads the shipped config asset from disk via AssetDatabase.
        [Test]
        [Category("Integration")]
        public void ShippedConfig_LoadsAndValidatesAndMatchesBriefLaneTable()
        {
            const string path = "Assets/Data/PlayerConfig.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, "missing " + path);

            var cfg = PlayerConfigLoader.FromJson(asset.text);

            Assert.AreEqual(LaneTable.Length, cfg.lanes.Length);
            for (int i = 0; i < LaneTable.Length; i++)
                CollectionAssert.AreEqual(LaneTable[i].Skip(1).ToArray(),
                    cfg.lanes[i].dimensions.Select(d => d.displayName).ToArray(), cfg.lanes[i].id);
            Assert.That(cfg.personalities.Length, Is.InRange(4, 6));
            Assert.GreaterOrEqual(cfg.generation.playersPerLane, 5);
        }
    }
}
