using System;
using System.Linq;
using EsportsManager.Players;
using EsportsManager.Teams;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static EsportsManager.Tests.Teams.TeamTestFactory;

namespace EsportsManager.Tests.Teams
{
    /// <summary>Story 002 config tests (AC10): validation, strict loader, the shipped file.</summary>
    public class TeamConfigTests
    {
        // ' stands for " to keep the literal readable.
        private static readonly string ValidJson = (
            "{'lane_weights':[{'lane':'top','weight':1},{'lane':'jungle','weight':1},{'lane':'mid','weight':1},{'lane':'adc','weight':1},{'lane':'support','weight':1}],"
            + "'strength':{'short_board_penalty':0.3,'stress_factor':0.2,'mood_factor_base':0.7,'mood_factor_span':0.3},"
            + "'familiarity':{'per_segment':5,'per_win':2,'max':100,'decay':1,'compatibility_factor':0.5},"
            + "'popularity':{'alpha':0.25,'random_range':0.15,'weight_club':1,'weight_lane_score':1,'weight_looks':0.8,'weight_stream':1.5,'weight_win_rate':1,'weight_business':0.8},"
            + "'club_popularity':{'initial':30,'beta':0.2,'random_range':0.1,'weight_starter_popularity':1,'weight_win_rate':1,'weight_rank':0.5},"
            + "'results':{'window':10,'default_win_rate':50},"
            + "'livestream':{'base_income':500,'income_per_popularity':20,'second_stream_factor':0.5}}").Replace('\'', '"');

        private static string Json(string from, string to)
        {
            string f = from.Replace('\'', '"');
            Assert.IsTrue(ValidJson.Contains(f), "test bug: '" + from + "' not in ValidJson");
            return ValidJson.Replace(f, to.Replace('\'', '"'));
        }

        private static void AssertLoadFails(string json, string contains)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => TeamConfigLoader.FromJson(json));
            StringAssert.Contains(contains, ex.Message);
        }

        // ---- loader

        [Test]
        public void FromJson_ValidText_MapsEveryKey()
        {
            var c = TeamConfigLoader.FromJson(ValidJson);
            Assert.AreEqual(5, c.laneWeights.Length);
            Assert.AreEqual(0.3f, c.strength.shortBoardPenalty);
            Assert.AreEqual(0.2f, c.strength.stressFactor);
            Assert.AreEqual(0.7f, c.strength.moodFactorBase);
            Assert.AreEqual(0.3f, c.strength.moodFactorSpan);
            Assert.AreEqual(5f, c.familiarity.perSegment);
            Assert.AreEqual(2f, c.familiarity.perWin);
            Assert.AreEqual(100f, c.familiarity.max);
            Assert.AreEqual(1f, c.familiarity.decay);
            Assert.AreEqual(0.5f, c.familiarity.compatibilityFactor);
            Assert.AreEqual(0.25f, c.popularity.alpha);
            Assert.AreEqual(0.15f, c.popularity.randomRange);
            Assert.AreEqual(1.5f, c.popularity.weightStream);
            Assert.AreEqual(0.2f, c.clubPopularity.beta);
            Assert.AreEqual(0.1f, c.clubPopularity.randomRange);
            Assert.AreEqual(0.5f, c.clubPopularity.weightRank);
            Assert.AreEqual(10, c.results.window);
            Assert.AreEqual(50f, c.results.defaultWinRate);
            Assert.AreEqual(500, c.livestream.baseIncome);
            Assert.AreEqual(20f, c.livestream.incomePerPopularity);
            Assert.AreEqual(0.5f, c.livestream.secondStreamFactor);
        }

        [Test]
        public void FromJson_EmptyText_Throws() => AssertLoadFails("  ", "empty");

        [Test]
        public void FromJson_MissingKey_ThrowsNamingKey() =>
            AssertLoadFails(Json("'alpha':0.25,", ""), "popularity.alpha");

        [Test]
        public void FromJson_MisspelledKey_Throws() =>
            AssertLoadFails(Json("'per_win'", "'per_wins'"), "familiarity.per_win");

        [Test]
        public void FromJson_ExtraKey_Throws() =>
            AssertLoadFails(Json("'window':10,", "'window':10,'bonus':1,"), "unknown key 'results.bonus'");

        [Test]
        public void FromJson_DuplicateKey_Throws() =>
            AssertLoadFails(Json("'window':10,", "'window':10,'window':9,"), "duplicate key");

        [Test]
        public void FromJson_StringWhereNumberExpected_Throws() =>
            AssertLoadFails(Json("'alpha':0.25", "'alpha':'0.25'"), "must be a number");

        [Test]
        public void FromJson_FractionWhereIntExpected_Throws() =>
            AssertLoadFails(Json("'window':10", "'window':2.5"), "whole number");

        [Test]
        public void FromJson_NullValue_Throws() =>
            AssertLoadFails(Json("'beta':0.2", "'beta':null"), "null");

        [Test]
        public void FromJson_NonFiniteNumber_Throws() =>
            AssertLoadFails(Json("'beta':0.2", "'beta':1e999"), "not a finite");

        [Test]
        public void FromJson_InfinityLiteral_Throws() =>
            Assert.Throws<InvalidOperationException>(() => TeamConfigLoader.FromJson(Json("'beta':0.2", "'beta':Infinity")));

        [Test]
        public void FromJson_WrongContainerType_Throws() =>
            AssertLoadFails(Json("'lane_weights':[{'lane':'top','weight':1},{'lane':'jungle','weight':1},{'lane':'mid','weight':1},{'lane':'adc','weight':1},{'lane':'support','weight':1}]", "'lane_weights':{}"), "lane_weights");

        [Test]
        public void FromJson_OutOfRangeValue_FailsValidation() =>
            AssertLoadFails(Json("'window':10", "'window':0"), "results.window");

        // ---- validation

        private static void AssertInvalid(Action<TeamConfig> mutate, string contains)
        {
            var c = MakeTeamConfig();
            mutate(c);
            var ex = Assert.Throws<InvalidOperationException>(() => c.Validate());
            StringAssert.Contains(contains, ex.Message);
        }

        [Test]
        public void Validate_TestConfig_Passes() => MakeTeamConfig().Validate();

        [Test] public void Validate_ShortBoardPenaltyAboveOne_Throws() => AssertInvalid(c => c.strength.shortBoardPenalty = 1.5f, "strength.shortBoardPenalty");
        [Test] public void Validate_StressFactorNegative_Throws() => AssertInvalid(c => c.strength.stressFactor = -0.1f, "strength.stressFactor");
        [Test] public void Validate_MoodFactorsAboveOne_Throws() => AssertInvalid(c => c.strength.moodFactorSpan = 0.5f, "moodFactorBase + moodFactorSpan");
        [Test] public void Validate_FamiliarityMaxZero_Throws() => AssertInvalid(c => c.familiarity.max = 0f, "familiarity.max");
        [Test] public void Validate_ZeroDecay_Throws() => AssertInvalid(c => c.familiarity.decay = 0f, "familiarity.decay must be > 0");
        [Test] public void Validate_NegativeDecay_Throws() => AssertInvalid(c => c.familiarity.decay = -1f, "familiarity.decay");
        [Test] public void Validate_KappaAboveOne_Throws() => AssertInvalid(c => c.familiarity.compatibilityFactor = 1.1f, "familiarity.compatibilityFactor");
        [Test] public void Validate_AlphaZero_Throws() => AssertInvalid(c => c.popularity.alpha = 0f, "popularity.alpha");
        [Test] public void Validate_PopularityRandomRangeOne_Throws() => AssertInvalid(c => c.popularity.randomRange = 1f, "popularity.randomRange");
        [Test] public void Validate_NegativePopularityWeight_Throws() => AssertInvalid(c => c.popularity.weightLooks = -1f, "popularity.weightLooks");
        [Test] public void Validate_BetaAboveOne_Throws() => AssertInvalid(c => c.clubPopularity.beta = 2f, "clubPopularity.beta");
        [Test] public void Validate_ClubInitialOutOfRange_Throws() => AssertInvalid(c => c.clubPopularity.initial = 101f, "clubPopularity.initial");
        [Test] public void Validate_DefaultWinRateOutOfRange_Throws() => AssertInvalid(c => c.results.defaultWinRate = 101f, "results.defaultWinRate");
        [Test] public void Validate_SecondStreamFactorAboveOne_Throws() => AssertInvalid(c => c.livestream.secondStreamFactor = 1.5f, "livestream.secondStreamFactor");
        [Test] public void Validate_SecondStreamFactorNegative_Throws() => AssertInvalid(c => c.livestream.secondStreamFactor = -0.1f, "livestream.secondStreamFactor");
        [Test] public void FromJson_MissingSecondStreamFactor_Throws() => AssertLoadFails(Json(",'second_stream_factor':0.5", ""), "livestream.second_stream_factor");
        [Test] public void Validate_NegativeStreamBase_Throws() => AssertInvalid(c => c.livestream.baseIncome = -1, "livestream.baseIncome");
        [Test] public void Validate_NaNValue_Throws() => AssertInvalid(c => c.popularity.weightStream = float.NaN, "popularity.weightStream");
        [Test] public void Validate_DuplicateLaneWeight_Throws() => AssertInvalid(c => c.laneWeights[1].lane = "top", "duplicate lane");
        [Test] public void Validate_AllLaneWeightsZero_Throws() => AssertInvalid(c => { foreach (var w in c.laneWeights) w.weight = 0f; }, "positive total");

        [Test]
        public void ValidateAgainst_LaneNotInPlayerConfig_Throws()
        {
            var c = MakeTeamConfig();
            c.laneWeights[0].lane = "coach";
            var ex = Assert.Throws<ArgumentException>(() => c.ValidateAgainst(MakePlayerConfig()));
            StringAssert.Contains("top", ex.Message);
        }

        [Test]
        public void ValidateAgainst_ExtraLane_Throws()
        {
            var c = MakeTeamConfig();
            c.laneWeights = c.laneWeights.Concat(new[] { new LaneWeightDef { lane = "coach", weight = 1f } }).ToArray();
            Assert.Throws<InvalidOperationException>(() => c.ValidateAgainst(MakePlayerConfig()));
        }

        // ---- shipped file (integration)

        [Test]
        [Category("Integration")]
        public void ShippedTeamConfig_LoadsValidatesAndMatchesPlayerConfigLanes()
        {
            const string path = "Assets/Data/team_config.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, "missing " + path);
            var cfg = TeamConfigLoader.FromJson(asset.text); // runs Validate()

            var playerAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/player_config.json");
            Assert.IsNotNull(playerAsset);
            var players = PlayerConfigLoader.FromJson(playerAsset.text);
            Assert.DoesNotThrow(() => cfg.ValidateAgainst(players));
            Assert.DoesNotThrow(() => new Team(cfg, players, new SeededRandomSource(1)));

            // numbers.md initial values
            Assert.AreEqual(0.3f, cfg.strength.shortBoardPenalty);
            Assert.AreEqual(0.2f, cfg.strength.stressFactor);
            Assert.AreEqual(5f, cfg.familiarity.perSegment);
            Assert.AreEqual(2f, cfg.familiarity.perWin);
            Assert.AreEqual(100f, cfg.familiarity.max);
            Assert.AreEqual(1f, cfg.familiarity.decay);
            Assert.AreEqual(0.5f, cfg.familiarity.compatibilityFactor);
            Assert.AreEqual(0.25f, cfg.popularity.alpha);
            Assert.AreEqual(0.15f, cfg.popularity.randomRange);
            Assert.AreEqual(0.2f, cfg.clubPopularity.beta);
            Assert.AreEqual(0.1f, cfg.clubPopularity.randomRange);
            Assert.AreEqual(7500, cfg.livestream.baseIncome);       // economy.md rev 3 F8 l_base
            Assert.AreEqual(60f, cfg.livestream.incomePerPopularity); // numbers.md 4.10 l_pop (v3)
            Assert.AreEqual(0.5f, cfg.livestream.secondStreamFactor); // numbers.md 4.10 (v3.1)
        }
    }
}
