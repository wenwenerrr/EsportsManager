using System;
using System.Linq;
using EsportsManager.Players;
using NUnit.Framework;

namespace EsportsManager.Tests.Players
{
    /// <summary>
    /// EditMode tests for potential, contract years, training gain, yearly decline and salary
    /// (Story 008, numbers.md sections 2, 4.1, 4.4). Config is built in memory; no file I/O.
    /// </summary>
    public class TrainingAndAgingTests
    {
        private static PlayerConfig Cfg() => TestConfigFactory.Make();

        // Neutral traits: neither has any entry in the fixture's training_gain / ask_price tables.
        private const string NeutralA = "gentle", NeutralB = "optimistic";

        private static Player Neutral(PlayerConfig cfg, int age = 22, float fill = 40f, float potential = 80f,
            float stress = 0f, float popularity = 50f, int contractYears = 2) =>
            TestConfigFactory.MakePlayer(cfg, age: age, fill: fill, potential: potential, stress: stress,
                popularity: popularity, contractYears: contractYears, traitA: NeutralA, traitB: NeutralB);

        private static void AssertInvalid(PlayerConfig cfg, string expectedFragment)
        {
            var ex = Assert.Throws<InvalidOperationException>(cfg.Validate);
            StringAssert.Contains(expectedFragment, ex.Message);
        }

        // ---- potential / talent ----

        [Test]
        public void Potential_IsStoredAndClampedToZeroHundred()
        {
            var cfg = Cfg();
            Assert.AreEqual(63f, Neutral(cfg, potential: 63f).Potential);
            Assert.AreEqual(100f, Neutral(cfg, potential: 250f).Potential);
            Assert.AreEqual(0f, Neutral(cfg, potential: -5f).Potential);
        }

        [Test]
        public void IsTalent_TrueAtOrAboveThreshold_FalseBelow()
        {
            var cfg = Cfg(); // threshold 85
            Assert.IsTrue(Neutral(cfg, potential: 85f).IsTalent);
            Assert.IsTrue(Neutral(cfg, potential: 99f).IsTalent);
            Assert.IsFalse(Neutral(cfg, potential: 84.9f).IsTalent);
        }

        [Test]
        public void IsTalent_FollowsConfiguredThreshold()
        {
            var cfg = Cfg();
            var p = Neutral(cfg, potential: 70f);
            Assert.IsFalse(p.IsTalent);
            cfg.potential.talentThreshold = 60f;
            Assert.IsTrue(p.IsTalent);
        }

        [Test]
        public void Pool_DimensionsAreFractionsOfPotential_AndPotentialMatchesTalentRanges()
        {
            var cfg = Cfg();
            var g = cfg.generation;
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            const float eps = 1e-3f;
            foreach (var p in pool)
            {
                foreach (var lane in cfg.lanes)
                {
                    bool main = lane.id == p.MainLane;
                    float lo = p.Potential * (main ? g.mainLaneDimFractionMin : g.offLaneDimFractionMin);
                    float hi = p.Potential * (main ? g.mainLaneDimFractionMax : g.offLaneDimFractionMax);
                    foreach (float v in p.GetRadar(lane.id))
                        Assert.That(v, Is.InRange(lo - eps, hi + eps), p.Name + " " + lane.id);
                }
                if (p.IsTalent)
                    Assert.That(p.Potential, Is.InRange(g.talentPotentialMin, g.talentPotentialMax));
                else
                    Assert.That(p.Potential, Is.InRange(g.normalPotentialMin, g.normalPotentialMax));
            }
        }

        [Test]
        public void Pool_RadarIsUneven_MainLaneAboveOffLaneOnAverage()
        {
            var cfg = Cfg();
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            foreach (var p in pool)
            {
                float main = p.GetRadar(p.MainLane).Average();
                float off = cfg.lanes.Where(l => l.id != p.MainLane).SelectMany(l => p.GetRadar(l.id)).Average();
                Assert.Greater(main, off, p.Name);
            }
        }

        // ---- potential estimate / observation / adjustment (numbers.md 4.15) ----

        [Test]
        public void PotentialEstimate_ObservationZero_UsesFullBiasAndHalfWidth()
        {
            var p = TestConfigFactory.MakePlayer(Cfg(), potential: 70f, scoutingBias: 6f);
            var est = p.GetPotentialEstimate(); // center 76, u = max(3, 20) = 20
            Assert.AreEqual(56, est.Low);
            Assert.AreEqual(96, est.High);
        }

        [Test]
        public void PotentialEstimate_HalfObservation_HalvesBiasAndWidth()
        {
            var p = TestConfigFactory.MakePlayer(Cfg(), potential: 70f, scoutingBias: 6f);
            p.AddObservation(50f); // center 73, u = max(3, 10) = 10
            var est = p.GetPotentialEstimate();
            Assert.AreEqual(63, est.Low);
            Assert.AreEqual(83, est.High);
        }

        [Test]
        public void PotentialEstimate_FullObservation_CentersOnTruthButNeverNarrowerThanUMin()
        {
            var p = TestConfigFactory.MakePlayer(Cfg(), potential: 70f, scoutingBias: -8f);
            p.AddObservation(100f); // center 70, u = max(3, 0) = 3
            var est = p.GetPotentialEstimate();
            Assert.AreEqual(67, est.Low);
            Assert.AreEqual(73, est.High);
            Assert.Greater(est.High, est.Low, "the estimate is never a single number");
        }

        [Test]
        public void PotentialEstimate_IsRoundedAndClampedToZeroHundred()
        {
            var cfg = Cfg();
            var high = TestConfigFactory.MakePlayer(cfg, potential: 95f, scoutingBias: 10f);
            Assert.AreEqual(100, high.GetPotentialEstimate().High);
            Assert.AreEqual(85, high.GetPotentialEstimate().Low); // 105 - 20
            var low = TestConfigFactory.MakePlayer(cfg, potential: 5f, scoutingBias: -4f);
            Assert.AreEqual(0, low.GetPotentialEstimate().Low);
            var frac = TestConfigFactory.MakePlayer(cfg, potential: 70.4f, scoutingBias: 0f);
            frac.AddObservation(100f); // 70.4 +- 3 -> 67.4 / 73.4 -> 67 / 73
            Assert.AreEqual(67, frac.GetPotentialEstimate().Low);
            Assert.AreEqual(73, frac.GetPotentialEstimate().High);
        }

        [Test]
        public void PotentialEstimate_KnobsComeFromConfig()
        {
            var cfg = Cfg();
            var p = TestConfigFactory.MakePlayer(cfg, potential: 50f, scoutingBias: 0f);
            cfg.potential.estimateHalfWidth = 10f;
            Assert.AreEqual(40, p.GetPotentialEstimate().Low);
            p.AddObservation(100f);
            cfg.potential.estimateMinHalfWidth = 5f;
            Assert.AreEqual(45, p.GetPotentialEstimate().Low);
        }

        [Test]
        public void Observation_AddsClampsAndResets()
        {
            var p = Neutral(Cfg());
            Assert.AreEqual(0f, p.Observation);
            p.AddObservation(5f);
            p.AddObservation(3f);
            Assert.AreEqual(8f, p.Observation);
            p.AddObservation(500f);
            Assert.AreEqual(100f, p.Observation);
            p.ResetObservation();
            Assert.AreEqual(0f, p.Observation);
            Assert.Throws<ArgumentOutOfRangeException>(() => p.AddObservation(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.AddObservation(float.PositiveInfinity));
        }

        [Test]
        public void AdjustPotential_ChangesTruePotentialClampedAndAffectsTalent()
        {
            var p = Neutral(Cfg(), potential: 80f);
            Assert.IsFalse(p.IsTalent);
            p.AdjustPotential(10f);
            Assert.AreEqual(90f, p.Potential);
            Assert.IsTrue(p.IsTalent, "IsTalent uses the true potential");
            p.AdjustPotential(50f);
            Assert.AreEqual(100f, p.Potential);
            p.AdjustPotential(-250f);
            Assert.AreEqual(0f, p.Potential);
            Assert.Throws<ArgumentOutOfRangeException>(() => p.AdjustPotential(float.NaN));
        }

        [Test]
        public void AdjustPotential_MovesTheEstimate()
        {
            var p = TestConfigFactory.MakePlayer(Cfg(), potential: 60f, scoutingBias: 0f);
            int before = p.GetPotentialEstimate().Low;
            p.AdjustPotential(10f);
            Assert.AreEqual(before + 10, p.GetPotentialEstimate().Low);
        }

        [Test]
        public void Pool_ScoutingBiasWithinPlusMinusB_AndVaries()
        {
            var cfg = Cfg();
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            Assert.IsTrue(pool.All(p => Math.Abs(p.ScoutingBias) <= cfg.potential.scoutingBias));
            Assert.Greater(pool.Select(p => p.ScoutingBias).Distinct().Count(), 1);
            Assert.IsTrue(pool.All(p => p.Observation == 0f));
        }

        // ---- stable unique ids ----

        [Test]
        public void Pool_IdsAreUniqueSequentialAndDeterministic()
        {
            var cfg = Cfg();
            var gen = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed));
            var pool = gen.GeneratePool();
            CollectionAssert.AreEqual(Enumerable.Range(1, pool.Count).Select(i => (long)i).ToArray(), pool.Select(p => p.Id).ToArray());
            Assert.AreEqual(pool.Count + 1, gen.NextId);

            var again = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            CollectionAssert.AreEqual(pool.Select(p => p.Id).ToArray(), again.Select(p => p.Id).ToArray());
        }

        [Test]
        public void Pool_IdsStayUniqueAcrossSeveralGeneratePoolCalls()
        {
            var gen = new PlayerGenerator(Cfg(), new SeededRandomSource(TestConfigFactory.FixedSeed));
            var all = gen.GeneratePool().Concat(gen.GeneratePool()).Concat(gen.GeneratePool()).ToList();
            Assert.AreEqual(all.Count, all.Select(p => p.Id).Distinct().Count());
        }

        [Test]
        public void Generator_FirstIdIsRespected()
        {
            var gen = new PlayerGenerator(Cfg(), new SeededRandomSource(1), firstId: 1000);
            var pool = gen.GeneratePool();
            Assert.AreEqual(1000L, pool[0].Id);
            Assert.AreEqual(1000L + pool.Count, gen.NextId);
        }

        [Test]
        public void Pool_ContainsBothTalentAndOrdinaryPlayers_WithFixedSeed()
        {
            var pool = new PlayerGenerator(Cfg(), new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            Assert.IsTrue(pool.Any(p => p.IsTalent), "no talent generated");
            Assert.IsTrue(pool.Any(p => !p.IsTalent), "no ordinary player generated");
        }

        [Test]
        public void Pool_TalentChanceZero_NeverGeneratesTalent()
        {
            var cfg = Cfg();
            cfg.generation.talentChance = 0f;
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            Assert.IsFalse(pool.Any(p => p.IsTalent));
        }

        // ---- contract years ----

        [Test]
        public void ContractYears_DropByOnePerYear_AndExpireAtZero()
        {
            var p = Neutral(Cfg(), contractYears: 2);
            Assert.AreEqual(2, p.ContractYearsLeft);
            Assert.IsFalse(p.IsContractExpired);
            p.AdvanceYear();
            Assert.AreEqual(1, p.ContractYearsLeft);
            Assert.IsFalse(p.IsContractExpired);
            p.AdvanceYear();
            Assert.AreEqual(0, p.ContractYearsLeft);
            Assert.IsTrue(p.IsContractExpired);
        }

        [Test]
        public void ContractYears_NeverGoBelowZero()
        {
            var p = Neutral(Cfg(), contractYears: 0);
            Assert.IsTrue(p.IsContractExpired);
            p.AdvanceYear();
            Assert.AreEqual(0, p.ContractYearsLeft);
        }

        [Test]
        public void ContractYears_NegativeInConstructorOrSetter_Throws()
        {
            var cfg = Cfg();
            Assert.Throws<ArgumentOutOfRangeException>(() => Neutral(cfg, contractYears: -1));
            var p = Neutral(cfg);
            Assert.Throws<ArgumentOutOfRangeException>(() => p.ContractYearsLeft = -1);
            p.ContractYearsLeft = 4;
            Assert.AreEqual(4, p.ContractYearsLeft);
        }

        [Test]
        public void Pool_ContractYearsInsideConfiguredRange()
        {
            var cfg = Cfg();
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            foreach (var p in pool)
                Assert.That(p.ContractYearsLeft, Is.InRange(cfg.generation.contractYearsMin, cfg.generation.contractYearsMax));
        }

        // ---- training gain (numbers.md 4.1) ----

        [Test]
        public void TrainingGain_MatchesFormula()
        {
            var cfg = Cfg();
            // T_base 2, intensity 1.5, dim 40 of potential 80, age 22 (coef 1.0), stress 40, k_s 0.5, no trait effect
            var p = Neutral(cfg, age: 22, fill: 40f, potential: 80f, stress: 40f);
            float expected = 2f * 1.5f * (1f - 40f / 80f) * 1f * 1.0f * (1f - 0.5f * 40f / 100f);
            Assert.AreEqual(expected, p.TrainingGain("mid", 0, 1.5f), 1e-5f);
            Assert.AreEqual(1.2f, p.TrainingGain("mid", 0, 1.5f), 1e-5f); // 2 x 1.5 x 0.5 x 0.8
        }

        [Test]
        public void TrainingGain_AtOrAbovePotential_StaysAtSoftCapFloorNotZero()
        {
            var cfg = Cfg();
            // headroom = max(1 - dim/P, rho) = rho 0.15 -> 2 x 1 x 0.15 x 1 x 1.0 x 1
            float expected = 2f * 0.15f;
            Assert.AreEqual(expected, Neutral(cfg, fill: 80f, potential: 80f).TrainingGain("mid", 0, 1f), 1e-5f);
            Assert.AreEqual(expected, Neutral(cfg, fill: 95f, potential: 80f).TrainingGain("mid", 0, 1f), 1e-5f);
            Assert.AreEqual(expected, Neutral(cfg, fill: 75f, potential: 80f).TrainingGain("mid", 0, 1f), 1e-5f,
                "1 - 75/80 = 0.0625 is below rho, so the floor applies before reaching potential too");
        }

        [Test]
        public void TrainingGain_SoftCapFloorComesFromConfig()
        {
            var cfg = Cfg();
            var p = Neutral(cfg, fill: 90f, potential: 80f);
            cfg.training.softCapFloor = 0.3f;
            Assert.AreEqual(2f * 0.3f, p.TrainingGain("mid", 0, 1f), 1e-5f);
        }

        [Test]
        public void TrainingGain_PastPotential_RepeatedTrainingKeepsRaisingDimensionUpTo100()
        {
            var p = Neutral(Cfg(), fill: 80f, potential: 80f);
            for (int i = 0; i < 500; i++)
                p.SetDimension("mid", 0, p.GetDimension("mid", 0) + p.TrainingGain("mid", 0, 1f));
            Assert.AreEqual(100f, p.GetDimension("mid", 0));
        }

        [Test]
        public void TrainingGain_ZeroPotential_UsesFloorNotNaN()
        {
            var gain = Neutral(Cfg(), fill: 0f, potential: 0f).TrainingGain("mid", 0, 1f);
            Assert.AreEqual(2f * 0.15f, gain, 1e-5f);
        }

        [Test]
        public void TrainingGain_ShrinksAsDimensionApproachesPotential()
        {
            var cfg = Cfg();
            var p = Neutral(cfg, fill: 10f, potential: 80f);
            float previous = float.MaxValue;
            foreach (float dim in new[] { 10f, 30f, 50f, 65f }) // 1 - 65/80 = 0.1875, still above rho
            {
                p.SetDimension("mid", 0, dim);
                float gain = p.TrainingGain("mid", 0, 1f);
                Assert.Less(gain, previous, "dim " + dim);
                Assert.Greater(gain, 0f);
                previous = gain;
            }
        }

        [Test]
        public void TrainingGain_OlderPlayersGainLess()
        {
            var cfg = Cfg();
            float previous = float.MaxValue;
            foreach (int age in new[] { 18, 21, 24, 27, 30 }) // one age from each band
            {
                float gain = Neutral(cfg, age: age).TrainingGain("mid", 0, 1f);
                Assert.Less(gain, previous, "age " + age);
                previous = gain;
            }
        }

        [Test]
        public void TrainingGain_AgeCoefficientsComeFromConfigBands()
        {
            var cfg = Cfg();
            float at19 = Neutral(cfg, age: 19).TrainingGain("mid", 0, 1f);
            float at20 = Neutral(cfg, age: 20).TrainingGain("mid", 0, 1f);
            Assert.AreEqual(1.2f / 1.0f, at19 / at20, 1e-5f);
            Assert.AreEqual(0.3f / 1.0f, Neutral(cfg, age: 40).TrainingGain("mid", 0, 1f) / at20, 1e-5f);
        }

        [Test]
        public void TrainingGain_AgeNotCoveredByAnyBand_UsesCoefficientOne()
        {
            var cfg = Cfg();
            cfg.training.ageBands = cfg.training.ageBands.Where(b => b.minAge != 20).ToArray(); // gap at 20-22
            Assert.DoesNotThrow(cfg.Validate);
            Assert.AreEqual(1f, cfg.GetAgeCoefficient(21));
        }

        [Test]
        public void TrainingGain_StressLowersGain()
        {
            var cfg = Cfg();
            float calm = Neutral(cfg, stress: 0f).TrainingGain("mid", 0, 1f);
            float tense = Neutral(cfg, stress: 100f).TrainingGain("mid", 0, 1f);
            Assert.AreEqual(1f - 0.5f, tense / calm, 1e-5f);
        }

        [Test]
        public void TrainingGain_ScalesLinearlyWithIntensity_AndZeroIntensityGivesZero()
        {
            var p = Neutral(Cfg());
            float one = p.TrainingGain("mid", 0, 1f);
            Assert.AreEqual(2f * one, p.TrainingGain("mid", 0, 2f), 1e-5f);
            Assert.AreEqual(0f, p.TrainingGain("mid", 0, 0f));
        }

        [Test]
        public void TrainingGain_NegativeIntensity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Neutral(Cfg()).TrainingGain("mid", 0, -1f));
        }

        [Test]
        public void TrainingGain_PersonalityTrainingMultiplierApplies()
        {
            var cfg = Cfg();
            float neutral = Neutral(cfg).TrainingGain("mid", 0, 1f);
            var diligent = TestConfigFactory.MakePlayer(cfg, fill: 40f, traitA: "diligent", traitB: NeutralA);
            var lazy = TestConfigFactory.MakePlayer(cfg, fill: 40f, traitA: "lazy", traitB: NeutralA);
            Assert.AreEqual(neutral * 1.2f, diligent.TrainingGain("mid", 0, 1f), 1e-5f);
            Assert.AreEqual(neutral * 0.8f, lazy.TrainingGain("mid", 0, 1f), 1e-5f);
        }

        [Test]
        public void TrainingGain_IsDeterministicAndDoesNotChangeThePlayer()
        {
            var p = Neutral(Cfg(), stress: 30f);
            float first = p.TrainingGain("mid", 2, 1f);
            for (int i = 0; i < 5; i++) Assert.AreEqual(first, p.TrainingGain("mid", 2, 1f));
            Assert.AreEqual(40f, p.GetDimension("mid", 2));
            Assert.AreEqual(30f, p.Stress);
        }

        // ---- AdvanceYear (numbers.md 4.1) ----

        [Test]
        public void AdvanceYear_YoungPlayerDoesNotGrowAutomatically()
        {
            var p = Neutral(Cfg(), age: 18, fill: 50f);
            p.AdvanceYear();
            Assert.AreEqual(19, p.Age);
            foreach (var lane in Cfg().lanes)
                foreach (float v in p.GetRadar(lane.id)) Assert.AreEqual(50f, v);
        }

        [Test]
        public void AdvanceYear_BeforeDeclineStartAge_NoChangeToDimensions()
        {
            var p = Neutral(Cfg(), age: 26, fill: 50f); // decline starts at 27
            p.AdvanceYear();
            Assert.AreEqual(27, p.Age);
            Assert.AreEqual(50f, p.GetDimension("mid", 0));
        }

        [Test]
        public void AdvanceYear_DeclinePerAgeBand()
        {
            var cfg = Cfg();
            foreach (var (age, decline) in new[] { (27, 1f), (28, 1f), (29, 2f), (30, 2f), (31, 4f), (45, 4f) })
            {
                var p = Neutral(cfg, age: age, fill: 50f);
                p.AdvanceYear();
                Assert.AreEqual(age + 1, p.Age);
                Assert.AreEqual(50f - decline, p.GetDimension("mid", 0), 1e-5f, "age " + age);
            }
        }

        [Test]
        public void AdvanceYear_DeclinesEveryDimensionOfEveryLane()
        {
            var cfg = Cfg();
            var p = Neutral(cfg, age: 29, fill: 50f);
            p.AdvanceYear();
            foreach (var lane in cfg.lanes)
                foreach (float v in p.GetRadar(lane.id)) Assert.AreEqual(48f, v, 1e-5f);
        }

        [Test]
        public void AdvanceYear_DeclineNeverGoesBelowZero()
        {
            var p = Neutral(Cfg(), age: 35, fill: 2f);
            p.AdvanceYear();
            Assert.AreEqual(0f, p.GetDimension("mid", 0));
        }

        [Test]
        public void AdvanceYear_DeclineStartAgeComesFromConfig()
        {
            var cfg = Cfg();
            cfg.aging.declineStartAge = 30;
            cfg.aging.declineBands = new[] { new DeclineBand { minAge = 20, maxAge = 200, amount = 3f } };
            var p27 = Neutral(cfg, age: 27, fill: 50f);
            p27.AdvanceYear();
            Assert.AreEqual(50f, p27.GetDimension("mid", 0));
            var p30 = Neutral(cfg, age: 30, fill: 50f);
            p30.AdvanceYear();
            Assert.AreEqual(47f, p30.GetDimension("mid", 0));
        }

        [Test]
        public void AdvanceYear_UsesTheAgeBeforeIncrement()
        {
            var p = Neutral(Cfg(), age: 28, fill: 50f); // 28 is band 27-28 (1), not 29-30 (2)
            p.AdvanceYear();
            Assert.AreEqual(49f, p.GetDimension("mid", 0), 1e-5f);
        }

        // ---- salary (numbers.md 4.4) ----

        // Independent re-statement of the formula for expected values.
        private static int ExpectedSalary(PlayerConfig cfg, float laneScore, float popularity, float potential, float ask, float r)
        {
            var s = cfg.salary;
            double ws = (laneScore * s.weightLaneScore + popularity * s.weightPopularity + potential * s.weightPotential)
                / (s.weightLaneScore + s.weightPopularity + s.weightPotential);
            double raw = s.minSalary + (s.maxSalary - s.minSalary) * ws / 100.0 * ask * r;
            return (int)Math.Round(Math.Max(raw, 0.0)); // no cap (numbers.md 4.4 v2)
        }

        [Test]
        public void Salary_MatchesFormula_WithRandomFactorOne()
        {
            var cfg = Cfg();
            var p = Neutral(cfg, fill: 50f, potential: 80f, popularity: 50f);
            int expected = ExpectedSalary(cfg, 50f, 50f, 80f, 1f, 1f);
            Assert.AreEqual(expected, p.CalculateSalaryWithoutRandom());
            Assert.AreEqual(expected, p.CalculateSalary(new StubRandomSource(0.5f))); // R(0.1) midpoint = 1
        }

        [Test]
        public void Salary_RandomFactorSpansPlusMinusTenPercentOfTheVariablePart()
        {
            var cfg = Cfg();
            var p = Neutral(cfg, fill: 50f, potential: 80f, popularity: 50f);
            Assert.AreEqual(ExpectedSalary(cfg, 50f, 50f, 80f, 1f, 0.9f), p.CalculateSalary(new StubRandomSource(0f)));
            Assert.AreEqual(ExpectedSalary(cfg, 50f, 50f, 80f, 1f, 1.1f), p.CalculateSalary(new StubRandomSource(1f)));
            Assert.Less(p.CalculateSalary(new StubRandomSource(0f)), p.CalculateSalary(new StubRandomSource(1f)));
        }

        [Test]
        public void Salary_SameSeed_SameResult()
        {
            var p = Neutral(Cfg());
            Assert.AreEqual(p.CalculateSalary(new SeededRandomSource(9)), p.CalculateSalary(new SeededRandomSource(9)));
        }

        [Test]
        public void Salary_UsesInjectedRandomSource_AndDoesNotChangeSalaryProperty()
        {
            var p = Neutral(Cfg());
            p.Salary = 777;
            p.CalculateSalary(new StubRandomSource(0.3f));
            Assert.AreEqual(777, p.Salary);
            Assert.Throws<ArgumentNullException>(() => p.CalculateSalary(null));
        }

        [Test]
        public void Salary_PersonalityAskPriceMultiplierApplies()
        {
            var cfg = Cfg();
            var neutral = Neutral(cfg);
            var selfish = TestConfigFactory.MakePlayer(cfg, fill: 40f, traitA: "selfish", traitB: NeutralB);
            var honest = TestConfigFactory.MakePlayer(cfg, fill: 40f, traitA: "honest", traitB: NeutralB);
            Assert.AreEqual(ExpectedSalary(cfg, 40f, 50f, 80f, 1.2f, 1f), selfish.CalculateSalaryWithoutRandom());
            Assert.AreEqual(ExpectedSalary(cfg, 40f, 50f, 80f, 0.9f, 1f), honest.CalculateSalaryWithoutRandom());
            Assert.Greater(selfish.CalculateSalaryWithoutRandom(), neutral.CalculateSalaryWithoutRandom());
            Assert.Less(honest.CalculateSalaryWithoutRandom(), neutral.CalculateSalaryWithoutRandom());
        }

        [Test]
        public void Salary_HasNoCap_CanExceedSMaxAfterPersonalityAndRandom()
        {
            var cfg = Cfg();
            var star = TestConfigFactory.MakePlayer(cfg, fill: 100f, potential: 100f, popularity: 100f, traitA: "selfish", traitB: NeutralB);
            int salary = star.CalculateSalary(new StubRandomSource(1f)); // 20000 + 180000 x 1 x 1.2 x 1.1
            Assert.AreEqual(ExpectedSalary(cfg, 100f, 100f, 100f, 1.2f, 1.1f), salary);
            Assert.Greater(salary, cfg.salary.maxSalary);
        }

        [Test]
        public void Salary_WorstPlayer_GetsSMin()
        {
            var cfg = Cfg();
            var nobody = TestConfigFactory.MakePlayer(cfg, fill: 0f, potential: 0f, popularity: 0f, traitA: "honest", traitB: NeutralB);
            Assert.AreEqual(cfg.salary.minSalary, nobody.CalculateSalary(new StubRandomSource(0f)));
        }

        [Test]
        public void Salary_UsesTruePotentialNotTheEstimate()
        {
            var cfg = Cfg();
            var biased = TestConfigFactory.MakePlayer(cfg, potential: 60f, scoutingBias: 10f, traitA: NeutralA, traitB: NeutralB);
            var unbiased = TestConfigFactory.MakePlayer(cfg, potential: 60f, scoutingBias: -10f, traitA: NeutralA, traitB: NeutralB);
            Assert.AreEqual(unbiased.CalculateSalaryWithoutRandom(), biased.CalculateSalaryWithoutRandom());
        }

        [Test]
        public void Salary_RisesWithLaneScorePopularityAndPotential()
        {
            var cfg = Cfg();
            int baseSalary = Neutral(cfg, fill: 40f, popularity: 40f, potential: 60f).CalculateSalaryWithoutRandom();
            Assert.Greater(Neutral(cfg, fill: 60f, popularity: 40f, potential: 60f).CalculateSalaryWithoutRandom(), baseSalary);
            Assert.Greater(Neutral(cfg, fill: 40f, popularity: 60f, potential: 60f).CalculateSalaryWithoutRandom(), baseSalary);
            Assert.Greater(Neutral(cfg, fill: 40f, popularity: 40f, potential: 90f).CalculateSalaryWithoutRandom(), baseSalary);
        }

        [Test]
        public void Salary_ConfigRangeEdit_ChangesResult()
        {
            var cfg = Cfg();
            var p = Neutral(cfg);
            int before = p.CalculateSalaryWithoutRandom();
            cfg.salary.maxSalary = 400000;
            Assert.Greater(p.CalculateSalaryWithoutRandom(), before);
        }

        [Test]
        public void Pool_InitialSalariesUseTheFormula()
        {
            var cfg = Cfg();
            // With the stub, every R(r) is exactly 1, so a pool salary must equal the no-random formula.
            var pool = new PlayerGenerator(cfg, new StubRandomSource(0.5f)).GeneratePool();
            Assert.Greater(pool.Count, 0);
            foreach (var p in pool)
                Assert.AreEqual(p.CalculateSalaryWithoutRandom(), p.Salary, p.Name);
        }

        [Test]
        public void Pool_SalariesAtLeastSMinAndVary_WithRealSeed()
        {
            var cfg = Cfg();
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            Assert.IsTrue(pool.All(p => p.Salary >= cfg.salary.minSalary));
            Assert.Greater(pool.Select(p => p.Salary).Distinct().Count(), 1, "salaries should vary");
        }

        // ---- validation: training / aging / potential / salary / generation additions ----

        [Test]
        public void Validate_TrainingValues_Rejected()
        {
            var cfg = Cfg();
            cfg.training.baseGain = -1f;
            AssertInvalid(cfg, "training.baseGain");
            cfg = Cfg();
            cfg.training.stressFactor = 1.5f;
            AssertInvalid(cfg, "training.stressFactor");
            cfg = Cfg();
            cfg.training.stressFactor = -0.1f;
            AssertInvalid(cfg, "training.stressFactor");
            cfg = Cfg();
            cfg.training.softCapFloor = 1.1f;
            AssertInvalid(cfg, "training.softCapFloor");
            cfg = Cfg();
            cfg.training.softCapFloor = -0.1f;
            AssertInvalid(cfg, "training.softCapFloor");
        }

        [Test]
        public void Validate_TrainingAgeBands_EmptyInvertedOverlappingNegative_Rejected()
        {
            var cfg = Cfg();
            cfg.training.ageBands = new AgeCoefficientBand[0];
            AssertInvalid(cfg, "training.ageBands");

            cfg = Cfg();
            cfg.training.ageBands[1].minAge = 30; // > maxAge 22
            AssertInvalid(cfg, "minAge");

            cfg = Cfg();
            cfg.training.ageBands[1].minAge = 19; // band 0 ends at 19
            AssertInvalid(cfg, "overlaps");

            cfg = Cfg();
            cfg.training.ageBands[0].coefficient = -0.1f;
            AssertInvalid(cfg, "coefficient");
        }

        [Test]
        public void Validate_AgingValues_Rejected()
        {
            var cfg = Cfg();
            cfg.aging.declineStartAge = -1;
            AssertInvalid(cfg, "aging.declineStartAge");

            cfg = Cfg();
            cfg.aging.declineBands = new DeclineBand[0];
            AssertInvalid(cfg, "aging.declineBands");

            cfg = Cfg();
            cfg.aging.declineBands[0].minAge = 40; // > maxAge 28
            AssertInvalid(cfg, "minAge");

            cfg = Cfg();
            cfg.aging.declineBands[1].minAge = 28; // overlaps band 0 (27-28)
            AssertInvalid(cfg, "overlaps");

            cfg = Cfg();
            cfg.aging.declineBands[0].amount = -1f;
            AssertInvalid(cfg, "amount");
        }

        [Test]
        public void Validate_TalentThresholdOutsideZeroHundred_Rejected()
        {
            var cfg = Cfg();
            cfg.potential.talentThreshold = 101f;
            AssertInvalid(cfg, "potential.talentThreshold");
            cfg = Cfg();
            cfg.potential.talentThreshold = -1f;
            AssertInvalid(cfg, "potential.talentThreshold");
        }

        [Test]
        public void Validate_PotentialEstimateKnobs_Rejected()
        {
            var setters = new (Action<PotentialConfig> set, string field)[]
            {
                (p => p.scoutingBias = -1f, "scoutingBias"),
                (p => p.scoutingBias = 101f, "scoutingBias"),
                (p => p.estimateHalfWidth = -1f, "estimateHalfWidth"),
                (p => p.estimateMinHalfWidth = -1f, "estimateMinHalfWidth"),
                (p => p.observationPerSegment = -1f, "observationPerSegment"),
                (p => p.observationPerTraining = -1f, "observationPerTraining"),
            };
            foreach (var (set, field) in setters)
            {
                var cfg = Cfg();
                set(cfg.potential);
                AssertInvalid(cfg, "potential." + field);
            }
        }

        [Test]
        public void Validate_DimensionFractionsOutsideZeroOne_Rejected()
        {
            var cfg = Cfg();
            cfg.generation.mainLaneDimFractionMax = 1.2f;
            AssertInvalid(cfg, "mainLaneDimFraction");
            cfg = Cfg();
            cfg.generation.offLaneDimFractionMin = -0.1f;
            AssertInvalid(cfg, "offLaneDimFraction");
        }

        [Test]
        public void Validate_NonFiniteFloats_Rejected()
        {
            var setters = new (Action<PlayerConfig> set, string field)[]
            {
                (c => c.offLaneDiscount = float.NaN, "offLaneDiscount"),
                (c => c.lanes[0].dimensions[0].weight = float.PositiveInfinity, "weight"),
                (c => c.traitEffects[0].multiplier = float.PositiveInfinity, "traitEffects[0].multiplier"),
                (c => c.traitPairs[0].value = float.NaN, "traitPairs[0].value"),
                (c => c.training.baseGain = float.PositiveInfinity, "training.baseGain"),
                (c => c.training.softCapFloor = float.NaN, "training.softCapFloor"),
                (c => c.training.ageBands[0].coefficient = float.PositiveInfinity, "training.ageBands[0].coefficient"),
                (c => c.aging.declineBands[0].amount = float.PositiveInfinity, "aging.declineBands[0].amount"),
                (c => c.potential.estimateHalfWidth = float.PositiveInfinity, "potential.estimateHalfWidth"),
                (c => c.salary.weightPotential = float.PositiveInfinity, "salary.weightPotential"),
                (c => c.generation.talentChance = float.NaN, "generation.talentChance"),
                (c => c.generation.looksMax = float.NegativeInfinity, "generation.looksMax"),
            };
            foreach (var (set, field) in setters)
            {
                var cfg = Cfg();
                set(cfg);
                var ex = Assert.Throws<InvalidOperationException>(cfg.Validate, field);
                StringAssert.Contains(field, ex.Message);
                StringAssert.Contains("finite", ex.Message);
            }
        }

        [Test]
        public void Validate_SalaryValues_Rejected()
        {
            var cfg = Cfg();
            cfg.salary.minSalary = cfg.salary.maxSalary + 1;
            AssertInvalid(cfg, "salary.minSalary");

            cfg = Cfg();
            cfg.salary.minSalary = -1;
            AssertInvalid(cfg, "salary.minSalary");

            cfg = Cfg();
            cfg.salary.weightLaneScore = cfg.salary.weightPopularity = cfg.salary.weightPotential = 0f;
            AssertInvalid(cfg, "salary weights");

            cfg = Cfg();
            cfg.salary.weightPopularity = -1f;
            AssertInvalid(cfg, "salary.weightPopularity");

            cfg = Cfg();
            cfg.salary.randomRange = 1f;
            AssertInvalid(cfg, "salary.randomRange");
        }

        [Test]
        public void Validate_GenerationAdditions_Rejected()
        {
            var cfg = Cfg();
            cfg.generation.talentPotentialMin = 80f; // below threshold 85
            AssertInvalid(cfg, "talentPotentialMin");

            cfg = Cfg();
            cfg.generation.talentChance = 1.5f;
            AssertInvalid(cfg, "talentChance");

            cfg = Cfg();
            cfg.generation.contractYearsMin = -1;
            AssertInvalid(cfg, "contractYears");

            cfg = Cfg();
            cfg.generation.normalPotentialMax = 120f;
            AssertInvalid(cfg, "normalPotential");
        }
    }
}
