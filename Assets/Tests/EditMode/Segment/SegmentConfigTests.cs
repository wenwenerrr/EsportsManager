using System;
using EsportsManager.Segments;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Segments
{
    /// <summary>Story 003 AC10: segment_config.json loader strictness, range validation, the shipped file.</summary>
    public class SegmentConfigTests
    {
        // ' stands for " to keep the literal readable.
        private static readonly string ValidJson = (
            "{'action_points':{'per_segment':6,'max_per_player':2},"
            + "'training':{'intensity':1},"
            + "'stream':{'cap':2},"
            + "'business':{'weight_popularity':1.5,'weight_lane_score':0.5,'weight_looks':1,'max_probability':0.4,'base_amount':5000,'amount_per_popularity':100,'amount_per_looks':80,'random_range':0.2,'club_share':0.7,'heat_cap':1},"
            + "'sponsorship':{'base_chance':0,'weight_club_popularity':1,'weight_win_rate':1,'max_success_rate':0.8,'random_spread':0.1,'min_chance':0.05,'max_chance':0.9,'base_amount':6000,'amount_per_club_popularity':100,'duration_segments':4,'max_attempts_per_segment':3,'max_active_contracts':3},"
            + "'mood':{'result':8,'play':4,'salary':10,'chemistry':5,'stress':6,'regression':0.05,'neutral':50,'high_stress_threshold':70,'random_range':0.15},"
            + "'stress':{'training':6,'match':2,'loss':3,'business':4,'rest':15,'stream':3,'recovery':4,'random_range':0.1}}").Replace('\'', '"');

        private static string Json(string from, string to)
        {
            string f = from.Replace('\'', '"');
            Assert.IsTrue(ValidJson.Contains(f), "test bug: '" + from + "' not in ValidJson");
            return ValidJson.Replace(f, to.Replace('\'', '"'));
        }

        private static void AssertLoadFails(string json, string contains)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => SegmentConfigLoader.FromJson(json));
            StringAssert.Contains(contains, ex.Message);
        }

        [Test]
        public void FromJson_ValidText_MapsEveryKey()
        {
            var c = SegmentConfigLoader.FromJson(ValidJson);
            Assert.AreEqual(6, c.actionPoints.perSegment);
            Assert.AreEqual(2, c.actionPoints.maxPerPlayer);
            Assert.AreEqual(1f, c.training.intensity);
            Assert.AreEqual(4f, c.stress.recovery);
            Assert.AreEqual(2f, c.stream.cap);
            Assert.AreEqual(1.5f, c.business.weightPopularity);
            Assert.AreEqual(0.5f, c.business.weightLaneScore);
            Assert.AreEqual(1f, c.business.weightLooks);
            Assert.AreEqual(0.4f, c.business.maxProbability);
            Assert.AreEqual(5000, c.business.baseAmount);
            Assert.AreEqual(100f, c.business.amountPerPopularity);
            Assert.AreEqual(80f, c.business.amountPerLooks);
            Assert.AreEqual(0.2f, c.business.randomRange);
            Assert.AreEqual(0.7f, c.business.clubShare);
            Assert.AreEqual(1f, c.business.heatCap);
            Assert.AreEqual(1f, c.sponsorship.weightClubPopularity);
            Assert.AreEqual(1f, c.sponsorship.weightWinRate);
            Assert.AreEqual(0.8f, c.sponsorship.maxSuccessRate);
            Assert.AreEqual(0.1f, c.sponsorship.randomSpread);
            Assert.AreEqual(0.05f, c.sponsorship.minChance);
            Assert.AreEqual(0.9f, c.sponsorship.maxChance);
            Assert.AreEqual(6000, c.sponsorship.baseAmount);
            Assert.AreEqual(100f, c.sponsorship.amountPerClubPopularity);
            Assert.AreEqual(4, c.sponsorship.durationSegments);
            Assert.AreEqual(3, c.sponsorship.maxAttemptsPerSegment);
            Assert.AreEqual(3, c.sponsorship.maxActiveContracts);
            Assert.AreEqual(8f, c.mood.result);
            Assert.AreEqual(4f, c.mood.play);
            Assert.AreEqual(10f, c.mood.salary);
            Assert.AreEqual(5f, c.mood.chemistry);
            Assert.AreEqual(6f, c.mood.stress);
            Assert.AreEqual(0.05f, c.mood.regression);
            Assert.AreEqual(50f, c.mood.neutral);
            Assert.AreEqual(70f, c.mood.highStressThreshold);
            Assert.AreEqual(0.15f, c.mood.randomRange);
            Assert.AreEqual(6f, c.stress.training);
            Assert.AreEqual(2f, c.stress.match);
            Assert.AreEqual(3f, c.stress.loss);
            Assert.AreEqual(4f, c.stress.business);
            Assert.AreEqual(15f, c.stress.rest);
            Assert.AreEqual(3f, c.stress.stream);
            Assert.AreEqual(0.1f, c.stress.randomRange);
        }

        [Test] public void FromJson_EmptyText_Throws() => AssertLoadFails(" ", "empty");
        [Test] public void FromJson_MissingKey_ThrowsNamingKey() => AssertLoadFails(Json("'cap':2", ""), "stream");
        [Test] public void FromJson_MissingNestedKey_ThrowsNamingKey() => AssertLoadFails(Json("'per_segment':6,", ""), "action_points.per_segment");
        [Test] public void FromJson_MisspelledKey_Throws() => AssertLoadFails(Json("'club_share'", "'clubshare'"), "business.club_share");
        [Test] public void FromJson_ExtraKey_Throws() => AssertLoadFails(Json("'intensity':1}", "'intensity':1,'bonus':1}"), "unknown key 'training.bonus'");
        [Test] public void FromJson_MissingRecovery_Throws() => AssertLoadFails(Json("'recovery':4,", ""), "stress.recovery");
        [Test] public void FromJson_ObsoleteTargetBonus_IsUnknownKey() => AssertLoadFails(Json("'intensity':1", "'intensity':1,'target_bonus':1.5"), "target_bonus");
        [Test] public void FromJson_DuplicateKey_Throws() => AssertLoadFails(Json("'intensity':1}", "'intensity':1,'intensity':2}"), "duplicate key");
        [Test] public void FromJson_StringWhereNumberExpected_Throws() => AssertLoadFails(Json("'cap':2", "'cap':'2'"), "must be a number");
        [Test] public void FromJson_FractionWhereIntExpected_Throws() => AssertLoadFails(Json("'per_segment':6", "'per_segment':6.5"), "whole number");
        [Test] public void FromJson_NullValue_Throws() => AssertLoadFails(Json("'play':4", "'play':null"), "null");
        [Test] public void FromJson_NonFiniteNumber_Throws() => AssertLoadFails(Json("'rest':15", "'rest':1e999"), "not a finite");
        [Test] public void FromJson_InfinityLiteral_Throws() => Assert.Throws<InvalidOperationException>(() => SegmentConfigLoader.FromJson(Json("'rest':15", "'rest':Infinity")));
        [Test] public void FromJson_SectionWrongType_Throws() => AssertLoadFails(Json("'stream':{'cap':2}", "'stream':[]"), "stream");
        [Test] public void FromJson_OutOfRangeValue_FailsValidation() => AssertLoadFails(Json("'per_segment':6", "'per_segment':0"), "actionPoints.perSegment");

        private static void AssertInvalid(Action<SegmentConfig> mutate, string contains)
        {
            var c = MakeConfig();
            mutate(c);
            var ex = Assert.Throws<InvalidOperationException>(() => c.Validate());
            StringAssert.Contains(contains, ex.Message);
        }

        [Test] public void Validate_TestConfig_Passes() => MakeConfig().Validate();
        [Test] public void Validate_MissingSection_Throws() => AssertInvalid(c => c.mood = null, "section is missing");
        [Test] public void Validate_ZeroMaxPerPlayer_Throws() => AssertInvalid(c => c.actionPoints.maxPerPlayer = 0, "maxPerPlayer");
        [Test] public void Validate_ZeroIntensity_Throws() => AssertInvalid(c => c.training.intensity = 0f, "training.intensity");
        [Test] public void Validate_ZeroStreamCap_Throws() => AssertInvalid(c => c.stream.cap = 0f, "stream.cap");
        [Test] public void Validate_BusinessProbabilityAboveOne_Throws() => AssertInvalid(c => c.business.maxProbability = 1.5f, "business.maxProbability");
        [Test] public void Validate_BusinessWeightsAllZero_Throws() => AssertInvalid(c => { c.business.weightLooks = 0f; c.business.weightLaneScore = 0f; c.business.weightPopularity = 0f; }, "positive total");
        [Test] public void Validate_ClubShareAboveOne_Throws() => AssertInvalid(c => c.business.clubShare = 1.1f, "business.clubShare");
        [Test] public void Validate_BusinessRandomRangeOne_Throws() => AssertInvalid(c => c.business.randomRange = 1f, "business.randomRange");
        [Test] public void Validate_NegativeBusinessBase_Throws() => AssertInvalid(c => c.business.baseAmount = -1, "business.baseAmount");
        [Test] public void Validate_SponsorChanceBoundsSwapped_Throws() => AssertInvalid(c => { c.sponsorship.minChance = 0.95f; }, "minChance");
        [Test] public void Validate_ZeroContractDuration_Throws() => AssertInvalid(c => c.sponsorship.durationSegments = 0, "sponsorship.durationSegments");
        [Test] public void Validate_ZeroMaxAttempts_Throws() => AssertInvalid(c => c.sponsorship.maxAttemptsPerSegment = 0, "sponsorship.maxAttemptsPerSegment");
        [Test] public void Validate_ZeroMaxActiveContracts_Throws() => AssertInvalid(c => c.sponsorship.maxActiveContracts = 0, "sponsorship.maxActiveContracts");
        [Test] public void FromJson_MissingMaxActiveContracts_Throws() => AssertLoadFails(Json(",'max_active_contracts':3", ""), "sponsorship.max_active_contracts");
        [Test] public void FromJson_FractionalMaxAttempts_Throws() => AssertLoadFails(Json("'max_attempts_per_segment':3", "'max_attempts_per_segment':2.5"), "whole number");
        [Test] public void Validate_ZMaxZero_Throws() => AssertInvalid(c => c.sponsorship.maxSuccessRate = 0f, "sponsorship.maxSuccessRate");
        [Test] public void Validate_RegressionAboveOne_Throws() => AssertInvalid(c => c.mood.regression = 1.5f, "mood.regression");
        [Test] public void Validate_NeutralOutOfRange_Throws() => AssertInvalid(c => c.mood.neutral = 120f, "mood.neutral");
        [Test] public void Validate_NegativeMoodConstant_Throws() => AssertInvalid(c => c.mood.play = -1f, "mood.play");
        [Test] public void Validate_StressRandomRangeOne_Throws() => AssertInvalid(c => c.stress.randomRange = 1f, "stress.randomRange");
        [Test] public void Validate_NegativeRecovery_Throws() => AssertInvalid(c => c.stress.recovery = -1f, "stress.recovery");
        [Test] public void Validate_NegativeRest_Throws() => AssertInvalid(c => c.stress.rest = -1f, "stress.rest");
        [Test] public void Validate_NaNValue_Throws() => AssertInvalid(c => c.stress.stream = float.NaN, "stress.stream");

        [Test]
        [Category("Integration")]
        public void ShippedSegmentConfig_LoadsValidatesAndHasDesignValues()
        {
            const string path = "Assets/Data/segment_config.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, "missing " + path);
            var c = SegmentConfigLoader.FromJson(asset.text); // runs Validate()
            // numbers.md initial values
            Assert.AreEqual(8, c.actionPoints.perSegment);
            Assert.AreEqual(2, c.actionPoints.maxPerPlayer);
            Assert.AreEqual(1f, c.training.intensity);
            Assert.AreEqual(11000, c.sponsorship.baseAmount);  // economy.md rev 3 F8 c0
            Assert.AreEqual(5, c.sponsorship.durationSegments); // F8: 5 segments
            Assert.AreEqual(0.25f, c.sponsorship.baseChance);   // F8 z0
            Assert.AreEqual(0f, c.sponsorship.weightWinRate);   // D4: no results term
            Assert.AreEqual(10000, c.business.baseAmount);      // F8: business x2
            Assert.AreEqual(3, c.sponsorship.maxAttemptsPerSegment); // v3.2
            Assert.AreEqual(3, c.sponsorship.maxActiveContracts);    // v3.2
            Assert.AreEqual(4f, c.stress.recovery);           // v3
            Assert.AreEqual(8f, c.mood.result);
            Assert.AreEqual(4f, c.mood.play);
            Assert.AreEqual(10f, c.mood.salary);
            Assert.AreEqual(5f, c.mood.chemistry);
            Assert.AreEqual(6f, c.mood.stress);
            Assert.AreEqual(0.05f, c.mood.regression);
            Assert.AreEqual(4f, c.stress.training); // v3
            Assert.AreEqual(2f, c.stress.match);
            Assert.AreEqual(3f, c.stress.loss);
            Assert.AreEqual(4f, c.stress.business);
            Assert.AreEqual(15f, c.stress.rest);
            Assert.AreEqual(0f, c.stress.stream); // v3.1: streaming no longer relieves stress
            Assert.AreEqual(0.4f, c.business.maxProbability);
            Assert.AreEqual(0.7f, c.business.clubShare);
            Assert.AreEqual(0.5f, c.sponsorship.maxSuccessRate); // F8 z1
        }
    }
}
