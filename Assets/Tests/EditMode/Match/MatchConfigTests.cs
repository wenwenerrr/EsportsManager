using System;
using System.IO;
using EsportsManager.Matches;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 004: match_config.json loader strictness, range validation, the shipped file.</summary>
    public class MatchConfigTests
    {
        // ' stands for " to keep the literal readable.
        private static readonly string ValidJson = (
            "{'series':{'opponent_count':7},"
            + "'calendar':{'segments_per_year':12,'events':[{'type':'split','name_key':'spring','after_segment':3},{'type':'split','name_key':'summer','after_segment':6},{'type':'finals','name_key':'finals','after_segment':12}]},"
            + "'format':{'regular_best_of':3,'playoff_teams':4,'playoff_best_of':5,'split_final_best_of':5,'finals_teams':4,'finals_semi_best_of':5,'finals_final_best_of':7,"
            + "'finals_points':[10,7,5,5,3,2,1,0],'split_prizes':[80000,50000,30000,30000,10000,10000,10000,10000],'finals_prizes':[200000,120000,60000,60000]},"
            + "'power':{'weight_skill':3,'weight_chemistry':1,'weight_style':1,'style_weight_command':1,'style_weight_lane_match':1,'command_dimension_id':'command'},"
            + "'noise':{'sigma0':0.03,'sigma1':0.12},"
            + "'win_probability':{'k':8,'p_min':0.1,'p_max':0.9},"
            + "'tactic':{'counter_bonus':4,'counters':[{'tactic':'invade','beats':'develop'},{'tactic':'develop','beats':'teamfight'},{'tactic':'teamfight','beats':'invade'}]},"
            + "'opponent':{'fixed_mood':50,'fixed_stress':50,'starter_pick_range':3,'roster_extra_min':0,'roster_extra_max':2,'familiarity_min':5,'familiarity_max':25,'club_names':['A','B','C','D','E','F','G'],'form_down':4,'form_up_base':4,'form_up_pop':8,'form_step':2,'pop_min':20,'pop_max':80},"
            + "'report':{'min_reasons':2,'max_reasons':3,'min_contribution':0.5,'even_threshold':0.05},'club':{'default_name':'Rookie Stars','name_max_length':16}}").Replace('\'', '"');

        private static string Json(string from, string to)
        {
            string f = from.Replace('\'', '"');
            Assert.IsTrue(ValidJson.Contains(f), "test bug: '" + from + "' not in ValidJson");
            return ValidJson.Replace(f, to.Replace('\'', '"'));
        }

        private static void AssertLoadFails(string json, string contains)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MatchConfigLoader.FromJson(json));
            StringAssert.Contains(contains, ex.Message);
        }

        [Test]
        public void FromJson_ValidText_MapsEveryKey()
        {
            var c = MatchConfigLoader.FromJson(ValidJson);
            Assert.AreEqual(7, c.series.opponentCount);
            Assert.AreEqual(12, c.calendar.segmentsPerYear);
            Assert.AreEqual(3, c.calendar.events.Length);
            Assert.AreEqual(EventType.Finals, c.calendar.events[2].type);
            Assert.AreEqual("summer", c.calendar.events[1].nameKey);
            Assert.AreEqual(6, c.calendar.events[1].afterSegment);
            Assert.AreEqual(3, c.format.regularBestOf);
            Assert.AreEqual(4, c.format.playoffTeams);
            Assert.AreEqual(7, c.format.finalsFinalBestOf);
            CollectionAssert.AreEqual(new[] { 10, 7, 5, 5, 3, 2, 1, 0 }, c.format.finalsPoints);
            Assert.AreEqual(80000L, c.format.splitPrizes[0]);
            Assert.AreEqual(60000L, c.format.finalsPrizes[3]);
            Assert.AreEqual(3f, c.power.weightSkill);
            Assert.AreEqual("command", c.power.commandDimensionId);
            Assert.AreEqual(0.03f, c.noise.sigma0);
            Assert.AreEqual(0.12f, c.noise.sigma1);
            Assert.AreEqual(8f, c.winProbability.k);
            Assert.AreEqual(0.1f, c.winProbability.pMin);
            Assert.AreEqual(0.9f, c.winProbability.pMax);
            Assert.AreEqual(4f, c.tactic.counterBonus);
            Assert.IsTrue(c.Counters(MatchTactic.Invade, MatchTactic.Develop));
            Assert.IsTrue(c.Counters(MatchTactic.Teamfight, MatchTactic.Invade));
            Assert.IsFalse(c.Counters(MatchTactic.Develop, MatchTactic.Invade));
            Assert.AreEqual(7, c.opponent.clubNames.Length);
            Assert.AreEqual(2, c.report.minReasons);
            Assert.AreEqual(2, c.GamesToWin);
        }

        [Test] public void FromJson_EmptyText_Throws() => AssertLoadFails(" ", "empty");
        [Test] public void FromJson_MissingKey_ThrowsNamingKey() => AssertLoadFails(Json("'k':8,", ""), "win_probability.k");
        [Test] public void FromJson_MissingSection_ThrowsNamingKey() => AssertLoadFails(Json("'noise':{'sigma0':0.03,'sigma1':0.12},", ""), "noise");
        [Test] public void FromJson_UnknownKey_Throws() => AssertLoadFails(Json("'k':8,", "'k':8,'extra':1,"), "unknown key 'win_probability.extra'");
        [Test] public void FromJson_WrongTypeString_Throws() => AssertLoadFails(Json("'regular_best_of':3", "'regular_best_of':'3'"), "must be a number");
        [Test] public void FromJson_FractionWhereIntExpected_Throws() => AssertLoadFails(Json("'regular_best_of':3", "'regular_best_of':3.5"), "whole number");
        [Test] public void FromJson_RemovedKeyWinPrize_Throws() => AssertLoadFails(Json("'opponent_count':7}", "'opponent_count':7,'win_prize':10000}"), "unknown key 'series.win_prize'");
        [Test] public void FromJson_UnknownEventType_Throws() => AssertLoadFails(Json("'type':'finals'", "'type':'cup'"), "unknown event type 'cup'");
        [Test] public void FromJson_MissingCalendar_Throws() => AssertLoadFails(Json("'segments_per_year':12,", ""), "calendar.segments_per_year");
        [Test] public void FromJson_NonFiniteNumber_Throws() => AssertLoadFails(Json("'k':8", "'k':1e999"), "not a finite");
        [Test] public void FromJson_NullValue_Throws() => AssertLoadFails(Json("'k':8", "'k':null"), "null");
        [Test] public void FromJson_ArrayWrongType_Throws() => AssertLoadFails(Json("'club_names':['A','B','C','D','E','F','G']", "'club_names':'A'"), "must be an array");
        [Test] public void FromJson_UnknownTacticName_Throws() => AssertLoadFails(Json("'beats':'develop'", "'beats':'rush'"), "unknown tactic 'rush'");
        [Test] public void FromJson_OutOfRangeValue_FailsValidation() => AssertLoadFails(Json("'k':8", "'k':0"), "winProbability.k");

        private static void AssertInvalid(Action<MatchConfig> mutate, string contains)
        {
            var c = MakeConfig();
            mutate(c);
            var ex = Assert.Throws<InvalidOperationException>(() => c.Validate());
            StringAssert.Contains(contains, ex.Message);
        }

        [Test] public void Validate_TestConfig_Passes() => MakeConfig().Validate();
        [Test] public void Validate_MissingSection_Throws() => AssertInvalid(c => c.noise = null, "section is missing");
        [Test] public void Validate_ZeroOpponents_Throws() => AssertInvalid(c => c.series.opponentCount = 0, "series.opponentCount");
        [Test] public void Validate_EvenBestOf_Throws() => AssertInvalid(c => c.format.regularBestOf = 4, "format.regularBestOf");
        [Test] public void Validate_EvenFinalsFinal_Throws() => AssertInvalid(c => c.format.finalsFinalBestOf = 6, "format.finalsFinalBestOf");
        [Test] public void Validate_PlayoffTeams3_Throws() => AssertInvalid(c => c.format.playoffTeams = 3, "format.playoffTeams");
        [Test] public void Validate_FinalsTeams8_Throws() => AssertInvalid(c => c.format.finalsTeams = 8, "format.finalsTeams");
        [Test] public void Validate_PrizeTableWrongLength_Throws() => AssertInvalid(c => c.format.splitPrizes = new long[] { 1, 0 }, "format.splitPrizes");
        [Test] public void Validate_NegativePrize_Throws() => AssertInvalid(c => c.format.finalsPrizes[3] = -1, "format.finalsPrizes");
        [Test] public void Validate_IncreasingPoints_Throws() => AssertInvalid(c => c.format.finalsPoints[7] = 5, "non-increasing");
        [Test] public void Validate_SemiLoserPrizesDiffer_Throws() => AssertInvalid(c => c.format.splitPrizes[3] = 20000, "share place 3");
        [Test] public void Validate_ZeroSegmentsPerYear_Throws() => AssertInvalid(c => c.calendar.segmentsPerYear = 0, "calendar.segmentsPerYear");
        [Test] public void Validate_EventAfterYearEnd_Throws() => AssertInvalid(c => c.calendar.events[2].afterSegment = 13, "afterSegment must be in");
        [Test] public void Validate_EventsOutOfOrder_Throws() => AssertInvalid(c => c.calendar.events[1].afterSegment = 3, "greater than the previous");
        [Test] public void Validate_DuplicateEventName_Throws() => AssertInvalid(c => c.calendar.events[1].nameKey = "spring", "used twice");
        [Test] public void Validate_FinalsWithoutSplit_Throws() => AssertInvalid(c => c.calendar.events = new[] { new CalendarEventDef { type = EventType.Finals, nameKey = "f", afterSegment = 12 } }, "at least one split");
        [Test] public void Validate_TwoFinals_Throws() => AssertInvalid(c => c.calendar.events[1].type = EventType.Finals, "at most one finals");

        [Test]
        public void Calendar_EventAfter_OnlySegments3_6_12()
        {
            var c = MakeConfig();
            for (int seg = 1; seg <= 12; seg++)
            {
                var e = c.calendar.EventAfter(seg);
                if (seg == 3 || seg == 6 || seg == 12) Assert.IsNotNull(e, "segment " + seg); else Assert.IsNull(e, "segment " + seg);
            }
            Assert.AreEqual("summer", c.calendar.NextEventFrom(4).nameKey);
            Assert.AreEqual("finals", c.calendar.NextEventFrom(7).nameKey);
        }
        [Test] public void Validate_AllPowerWeightsZero_Throws() => AssertInvalid(c => { c.power.weightSkill = 0; c.power.weightChemistry = 0; c.power.weightStyle = 0; }, "power weights");
        [Test] public void Validate_NaNWeight_Throws() => AssertInvalid(c => c.power.weightStyle = float.NaN, "power.weightStyle");
        [Test] public void Validate_EmptyCommandDimension_Throws() => AssertInvalid(c => c.power.commandDimensionId = "", "power.commandDimensionId");
        [Test] public void Validate_SigmaSumAtOne_Throws() => AssertInvalid(c => { c.noise.sigma0 = 0.5f; c.noise.sigma1 = 0.5f; }, "noise.sigma0 + noise.sigma1");
        [Test] public void Validate_PMinNotBelowPMax_Throws() => AssertInvalid(c => c.winProbability.pMin = 0.95f, "winProbability.pMin");
        [Test] public void Validate_NegativeCounterBonus_Throws() => AssertInvalid(c => c.tactic.counterBonus = -1f, "tactic.counterBonus");
        [Test] public void Validate_SelfCounter_Throws() => AssertInvalid(c => c.tactic.counters[0].beats = c.tactic.counters[0].tactic, "counters itself");
        [Test] public void Validate_CounterBothDirections_Throws() => AssertInvalid(c => c.tactic.counters[1] = new TacticCounterDef { tactic = MatchTactic.Develop, beats = MatchTactic.Invade }, "contradicts");
        [Test] public void Validate_DuplicateCounter_Throws() => AssertInvalid(c => c.tactic.counters[1] = c.tactic.counters[0], "duplicates");
        [Test] public void Validate_FixedStressAbove100_Throws() => AssertInvalid(c => c.opponent.fixedStress = 101f, "opponent.fixedStress");
        [Test] public void Validate_ZeroPickRange_Throws() => AssertInvalid(c => c.opponent.starterPickRange = 0, "opponent.starterPickRange");
        [Test] public void Validate_RosterExtraTooLarge_Throws() => AssertInvalid(c => c.opponent.rosterExtraMax = 3, "opponent.rosterExtraMax");
        [Test] public void Validate_FamiliarityBoundsSwapped_Throws() => AssertInvalid(c => c.opponent.familiarityMin = 30f, "familiarityMin");
        [Test] public void Validate_TooFewClubNames_Throws() => AssertInvalid(c => c.opponent.clubNames = new[] { "A" }, "opponent.clubNames");
        [Test] public void Validate_DuplicateClubName_Throws() => AssertInvalid(c => c.opponent.clubNames[1] = "A", "duplicate");
        [Test] public void Validate_EmptyClubName_Throws() => AssertInvalid(c => c.opponent.clubNames[1] = " ", "opponent.clubNames[1]");
        [Test] public void Validate_MaxReasonsBelowMin_Throws() => AssertInvalid(c => c.report.maxReasons = 1, "report.maxReasons");
        [Test] public void Validate_NegativeMinContribution_Throws() => AssertInvalid(c => c.report.minContribution = -1f, "report.minContribution");

        [Test]
        public void ValidateAgainst_MissingCommandDimension_Throws()
        {
            var pc = MakePlayerConfig();
            pc.lanes[2].dimensions[5].id = "other";
            var ex = Assert.Throws<InvalidOperationException>(() => MakeConfig().ValidateAgainst(pc));
            StringAssert.Contains("mid", ex.Message);
        }

        [Test] public void ValidateAgainst_ConfigWithCommand_Passes() => MakeConfig().ValidateAgainst(MakePlayerConfig());

        [Test]
        public void ShippedFile_LoadsAndHasInitialValues()
        {
            var c = MatchConfigLoader.FromJson(File.ReadAllText("Assets/Data/match_config.json"));
            Assert.AreEqual(7, c.series.opponentCount);
            Assert.AreEqual(12, c.calendar.segmentsPerYear);
            CollectionAssert.AreEqual(new[] { 3, 6, 12 }, System.Linq.Enumerable.Select(c.calendar.events, e => e.afterSegment));
            Assert.AreEqual(3, c.format.regularBestOf);
            Assert.AreEqual(5, c.format.playoffBestOf);
            Assert.AreEqual(5, c.format.finalsSemiBestOf);
            Assert.AreEqual(7, c.format.finalsFinalBestOf);
            CollectionAssert.AreEqual(new long[] { 80000, 50000, 30000, 30000, 10000, 10000, 10000, 10000 }, c.format.splitPrizes);
            CollectionAssert.AreEqual(new long[] { 200000, 120000, 60000, 60000 }, c.format.finalsPrizes);
            CollectionAssert.AreEqual(new[] { 10, 7, 5, 5, 3, 2, 1, 0 }, c.format.finalsPoints);
            Assert.AreEqual(0.1f, c.winProbability.pMin);
            Assert.AreEqual(0.9f, c.winProbability.pMax);
            Assert.GreaterOrEqual(c.opponent.clubNames.Length, c.series.opponentCount);
            // numbers.md 4.9 / 4.14 v3.3 user decision 2026-10-19: mature opponents, skill-weighted power
            Assert.AreEqual(4f, c.power.weightSkill);
            Assert.AreEqual(0.7f, c.power.weightChemistry);
            Assert.AreEqual(1f, c.power.weightStyle);
            Assert.AreEqual(80f, c.opponent.fixedMood);
            Assert.AreEqual(50f, c.opponent.fixedStress);
            Assert.AreEqual(60f, c.opponent.familiarityMin);
            Assert.AreEqual(80f, c.opponent.familiarityMax);
            Assert.AreEqual(8f, c.winProbability.k);
        }

        [Test]
        public void Tactics_KeyRoundTrip()
        {
            foreach (var t in MatchTactics.All)
            {
                Assert.IsTrue(MatchTactics.TryParse(MatchTactics.Key(t), out var back));
                Assert.AreEqual(t, back);
            }
            Assert.IsFalse(MatchTactics.TryParse("rush", out _));
        }
    }
}
