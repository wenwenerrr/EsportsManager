using EsportsManager.Players;
using EsportsManager.Segments;
using NUnit.Framework;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Segments
{
    /// <summary>Story 003 numeric formulas as pure functions: mood (4.6), stress (4.7), business (4.11), sponsorship (4.12), heat (4.2).</summary>
    public class SegmentFormulaTests
    {
        private const float Tol = 1e-4f;
        private Fixture _fx;
        private SegmentConfig _cfg;

        [SetUp] public void SetUp() { _fx = new Fixture(); _cfg = _fx.Cfg; }

        private Player P(string a = NeutralA, string b = NeutralB, float stress = 0f, float mood = 50f)
        {
            var p = _fx.MakePlayer(50, "mid", a, b, stress);
            p.Mood = mood;
            return p;
        }

        private float Mood(Player p, int result = 0, bool starter = true, float sat = 0f, float compat = 0f, float r = 1f) =>
            SegmentFormulas.MoodDelta(_cfg, p, new MoodInputs { Result = result, IsStarter = starter, SalarySatisfaction = sat, Compatibility = compat }, r);

        // ---------------- mood (AC5)

        [Test] public void MoodDelta_StarterNoResult_GainsPlayTerm() => Assert.AreEqual(4f, Mood(P()), Tol);
        [Test] public void MoodDelta_Bench_LosesPlayTerm() => Assert.AreEqual(-4f, Mood(P(), starter: false), Tol);
        [Test] public void MoodDelta_Win_AddsResultTerm() => Assert.AreEqual(12f, Mood(P(), result: 1), Tol);
        [Test] public void MoodDelta_Loss_SubtractsResultTerm() => Assert.AreEqual(-4f, Mood(P(), result: -1), Tol);
        [Test] public void MoodDelta_PositiveSalarySatisfaction_AddsSalaryTerm() => Assert.AreEqual(9f, Mood(P(), sat: 0.5f), Tol);
        [Test] public void MoodDelta_PositiveCompatibility_AddsChemistryTerm() => Assert.AreEqual(6.5f, Mood(P(), compat: 0.5f), Tol);
        [Test] public void MoodDelta_NegativeCompatibility_SubtractsChemistryTerm() => Assert.AreEqual(-1f, Mood(P(), compat: -1f), Tol);
        [Test] public void MoodDelta_StressAboveThreshold_SubtractsStressTerm() => Assert.AreEqual(-2f, Mood(P(stress: 80f)), Tol);
        [Test] public void MoodDelta_StressExactlyAtThreshold_NoStressTerm() => Assert.AreEqual(4f, Mood(P(stress: 70f)), Tol);
        [Test] public void MoodDelta_MoodAboveNeutral_RegressesDown() => Assert.AreEqual(4f - 1.5f, Mood(P(mood: 80f)), Tol);
        [Test] public void MoodDelta_MoodBelowNeutral_RegressesUp() => Assert.AreEqual(4f + 1.5f, Mood(P(mood: 20f)), Tol);
        [Test] public void MoodDelta_RandomFactor_ScalesBracketNotRegression() => Assert.AreEqual(4f * 1.15f + 0.05f * 20f, Mood(P(mood: 30f), r: 1.15f), Tol);

        [Test] public void MoodDelta_PessimistDoublesNegativeTerms() => Assert.AreEqual(-8f, Mood(P("pessimistic"), starter: false), Tol);
        [Test] public void MoodDelta_FragileScalesLossTerm() => Assert.AreEqual(-12f, Mood(P("fragile"), result: -1), Tol);
        [Test] public void MoodDelta_DecisiveScalesWinTerm() => Assert.AreEqual(20f, Mood(P("decisive"), result: 1), Tol);
        [Test] public void MoodDelta_SelfishScalesBenchTerm() => Assert.AreEqual(-12f, Mood(P("selfish"), starter: false), Tol);
        [Test] public void MoodDelta_PragmaticScalesSalaryTerm() => Assert.AreEqual(14f, Mood(P("pragmatic", "reserved"), sat: 0.5f), Tol);
        [Test] public void MoodDelta_HonestScalesWholeBracket() => Assert.AreEqual(2f, Mood(P("honest")), Tol);

        [Test]
        public void MoodDelta_AppliedToPlayer_ClampsToRange()
        {
            var p = P(stress: 80f, mood: 1f);
            p.Mood += Mood(p, result: -1, starter: false);
            Assert.AreEqual(0f, p.Mood);
            var q = P(mood: 100f);
            q.Mood += Mood(q, result: 1);
            Assert.AreEqual(100f, q.Mood);
        }

        // ---------------- stress (AC4)

        private float Stress(Player p, StressInputs t, float r = 1f) => SegmentFormulas.StressDelta(_cfg, p, t, r);

        [Test] public void StressDelta_Training_AddsPerIntensity() => Assert.AreEqual(12f, Stress(P(), new StressInputs { TrainingIntensity = 2f }), Tol);
        [Test] public void StressDelta_MatchesAndLosses_AddUp() => Assert.AreEqual(2f * 3 + 3f * 2, Stress(P(), new StressInputs { Matches = 3, Losses = 2 }), Tol);
        [Test] public void StressDelta_Business_AddsTerm() => Assert.AreEqual(4f, Stress(P(), new StressInputs { Business = 1 }), Tol);
        [Test] public void StressDelta_Rest_ReducesStrongly() => Assert.AreEqual(-15f, Stress(P(), new StressInputs { Rest = 1 }), Tol);
        [Test] public void StressDelta_Stream_ReducesSlightly() => Assert.AreEqual(-3f, Stress(P(), new StressInputs { Streams = 1 }), Tol);
        [Test] public void StressDelta_TraitMultiplier_Scales() => Assert.AreEqual(3f, Stress(P("lazy"), new StressInputs { TrainingIntensity = 1f }), Tol);
        [Test] public void StressDelta_RandomFactor_Scales() => Assert.AreEqual(6.6f, Stress(P(), new StressInputs { TrainingIntensity = 1f }, 1.1f), Tol);

        [Test]
        public void StressDelta_Recovery_SubtractedOutsideRandomBracket()
        {
            _cfg.stress.recovery = 4f;
            Assert.AreEqual(6f * 1.1f - 4f, Stress(P(), new StressInputs { TrainingIntensity = 1f }, 1.1f), Tol);
            Assert.AreEqual(-4f, Stress(P("lazy"), new StressInputs(), 1.1f), Tol); // not scaled by personality or R
        }

        [Test]
        public void StressDelta_AppliedToPlayer_ClampsToRange()
        {
            var p = P(stress: 10f);
            p.Stress += Stress(p, new StressInputs { Rest = 2 });
            Assert.AreEqual(0f, p.Stress);
            var q = P(stress: 95f);
            q.Stress += Stress(q, new StressInputs { TrainingIntensity = 2f });
            Assert.AreEqual(100f, q.Stress);
        }

        // ---------------- stream heat (AC3)

        [Test] public void StreamHeat_NoStreams_IsZero() => Assert.AreEqual(0f, SegmentFormulas.StreamHeat(_cfg, P(), 0));
        [Test] public void StreamHeat_HalfCap_IsFifty() => Assert.AreEqual(50f, SegmentFormulas.StreamHeat(_cfg, P(), 1), Tol);
        [Test] public void StreamHeat_AtCap_IsHundred() => Assert.AreEqual(100f, SegmentFormulas.StreamHeat(_cfg, P(), 2), Tol);
        [Test] public void StreamHeat_AboveCap_ClampsToHundred() => Assert.AreEqual(100f, SegmentFormulas.StreamHeat(_cfg, P(), 5), Tol);
        [Test] public void StreamHeat_TraitMultiplierAppliedBeforeCap() => Assert.AreEqual(65f, SegmentFormulas.StreamHeat(_cfg, P("lively"), 1), Tol);

        // ---------------- business (AC6)

        [Test] public void BusinessProbability_AveragePlayer_IsFiftyPercentOfMax() => Assert.AreEqual(0.2f, SegmentFormulas.BusinessProbability(_cfg, P()), Tol);
        [Test] public void BusinessProbability_TraitRateScales() => Assert.AreEqual(0.1f, SegmentFormulas.BusinessProbability(_cfg, P("earnest")), Tol);
        [Test] public void BusinessProbability_CappedAtMax() => Assert.AreEqual(0.4f, SegmentFormulas.BusinessProbability(_cfg, P("lively")), Tol);

        [Test]
        public void BusinessProbability_ZeroStats_IsZero()
        {
            var p = _fx.MakePlayer(60, "mid", fill: 0f);
            p.Popularity = 0f; p.Looks = 0f;
            Assert.AreEqual(0f, SegmentFormulas.BusinessProbability(_cfg, p));
        }

        [Test] public void BusinessAmount_UsesPopularityAndLooks() => Assert.AreEqual(5000 + 100 * 50 + 80 * 50, SegmentFormulas.BusinessAmount(_cfg, P(), 1f));
        [Test] public void BusinessAmount_RandomFactorScales() => Assert.AreEqual(11200, SegmentFormulas.BusinessAmount(_cfg, P(), 0.8f));
        [Test] public void BusinessAmount_TraitMultiplierScales() => Assert.AreEqual(21000, SegmentFormulas.BusinessAmount(_cfg, P("slick"), 1f));
        [Test] public void ClubShare_IsRoundedShareOfAmount() => Assert.AreEqual(9800, SegmentFormulas.ClubShare(_cfg, 14000));
        [Test] public void BusinessHeat_OneDealAtCapOne_IsHundred() => Assert.AreEqual(100f, SegmentFormulas.BusinessHeat(_cfg, 1));
        [Test] public void BusinessHeat_NoDeal_IsZero() => Assert.AreEqual(0f, SegmentFormulas.BusinessHeat(_cfg, 0));

        // ---------------- sponsorship (AC7)

        [Test] public void SponsorChance_UsesWeightedScoreAndZMax() => Assert.AreEqual(0.32f, SegmentFormulas.SponsorChance(_cfg, 30f, 50f, 0f), Tol);
        [Test] public void SponsorChance_NoiseAdded() => Assert.AreEqual(0.42f, SegmentFormulas.SponsorChance(_cfg, 30f, 50f, 0.1f), Tol);
        [Test] public void SponsorChance_ClampedAtMinimum() => Assert.AreEqual(0.05f, SegmentFormulas.SponsorChance(_cfg, 0f, 0f, -0.1f), Tol);

        [Test]
        public void SponsorChance_ClampedAtMaximum()
        {
            _cfg.sponsorship.maxChance = 0.85f;
            Assert.AreEqual(0.85f, SegmentFormulas.SponsorChance(_cfg, 100f, 100f, 0.1f), Tol);
        }

        [Test] public void ContractAmount_IsBasePlusPerPopularity() => Assert.AreEqual(9000, SegmentFormulas.ContractAmount(_cfg, 30f));
    }
}
