using System;
using System.IO;
using System.Linq;
using EsportsManager.Players;
using EsportsManager.Matches;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 004 / 011: match_config.json loader strictness, range validation (match.md AC 13), the shipped file (AC 14).</summary>
    public class MatchConfigTests
    {
        private static readonly string ValidJson = File.ReadAllText("Assets/Data/match_config.json");

        // Replaces one exact snippet of the shipped file (' stands for ").
        private static string Json(string from, string to)
        {
            string f = from.Replace('\'', '"');
            Assert.IsTrue(ValidJson.Contains(f), "test bug: '" + from + "' not in the shipped file");
            return ValidJson.Replace(f, to.Replace('\'', '"'));
        }

        private static void AssertLoadFails(string json, string contains)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MatchConfigLoader.FromJson(json, MakePlayerConfig()));
            StringAssert.Contains(contains, ex.Message);
        }

        private static void AssertLoads(string json) => Assert.DoesNotThrow(() => MatchConfigLoader.FromJson(json, MakePlayerConfig()));

        [Test] public void FromJson_EmptyText_Throws() => AssertLoadFails(" ", "empty");
        [Test] public void FromJson_MissingKey_ThrowsNamingKey() => AssertLoadFails(Json("'k': 8, ", ""), "win_probability.k");
        [Test] public void FromJson_UnknownKey_Throws() => AssertLoadFails(Json("'k': 8,", "'k': 8, 'extra': 1,"), "win_probability.extra");
        [Test] public void FromJson_NonNumber_Throws() => AssertLoadFails(Json("'k': 8,", "'k': 'eight',"), "must be a number");
        [Test] public void FromJson_FractionWhereIntExpected_Throws() => AssertLoadFails(Json("'regular_best_of': 3", "'regular_best_of': 3.5"), "whole number");
        [Test] public void FromJson_NullValue_Throws() => AssertLoadFails(Json("'k': 8,", "'k': null,"), "null");
        [Test] public void FromJson_UnknownTacticName_Throws() => AssertLoadFails(Json("'beats': 'develop'", "'beats': 'rush'"), "unknown tactic 'rush'");
        [Test] public void FromJson_UnknownPhase_Throws() => AssertLoadFails(Json("'beats': 'early'", "'beats': 'dawn'"), "unknown phase 'dawn'");
        [Test] public void FromJson_RemovedV3Key_Throws() => AssertLoadFails(Json("'k': 8,", "'k': 8, 'p_min': 0.1,"), "win_probability.p_min");

        // ---- AC 13: mapping (4 kinds), with lane and dimension ids in the message
        [Test] public void Mapping_PhaseWithoutDimension_FailsNamingLane() => AssertLoadFails(Json("'late': ['healing']", "'late': []"), "support");
        [Test] public void Mapping_DimensionInTwoPhases_FailsNamingDimension() => AssertLoadFails(Json("'late': ['healing']", "'late': ['healing', 'engage']"), "engage");
        [Test] public void Mapping_UnmappedDimension_FailsNamingDimension() => AssertLoadFails(Json("'late': ['farming', 'gold_to_damage', 'survival']", "'late': ['farming', 'gold_to_damage']"), "survival");
        [Test] public void Mapping_CommandMapped_FailsNamingCommand() => AssertLoadFails(Json("'late': ['healing']", "'late': ['healing', 'command']"), "command");

        // ---- AC 13: ranges and constraints, with the key in the message; boundary values pass
        [Test] public void PFloor_BelowRange_Fails() => AssertLoadFails(Json("'p_floor': 0.15", "'p_floor': 0.049"), "pFloor");
        [Test] public void PFloor_AboveRange_Fails() => AssertLoadFails(Json("'p_floor': 0.15", "'p_floor': 0.251"), "pFloor");
        [Test] public void PFloor_Boundaries_Pass() { AssertLoads(Json("'p_floor': 0.15", "'p_floor': 0.05")); AssertLoads(Json("'p_floor': 0.15", "'p_floor': 0.25")); }
        [Test] public void WMin_Zero_Fails() => AssertLoadFails(Json("'weight_min': 0.4", "'weight_min': 0"), "weightMin");
        [Test] public void WMin_AboveOne_Fails() => AssertLoadFails(Json("'weight_min': 0.4", "'weight_min': 1.2"), "weightMin");
        [Test] public void LambdaMid_AboveHalf_Fails() => AssertLoadFails(Json("'short_board_mid': 0.5", "'short_board_mid': 0.6"), "shortBoardMid");
        [Test] public void ExecutionSum_1_0011_Fails() => AssertLoadFails(Json("'e2': 0.3,", "'e2': 0.3011,"), "e0 + e1 + e2");
        [Test] public void ExecutionSum_1_0009_Passes() => AssertLoads(Json("'e2': 0.3,", "'e2': 0.3009,"));
        [Test] public void LaneWeight_Zero_Fails() => AssertLoadFails(Json("'weight_late': 1.6", "'weight_late': 0"), "weight_late");
        [Test] public void TacticBudget_Exceeded_Fails() => AssertLoadFails(Json("'counter': 0.5,", "'counter': 0.6,"), "budget");
        [Test] public void Monotonicity_Violated_Fails() => AssertLoadFails(Json("'star_scale_max': 2,", "'star_scale_max': 2.6,"), "monotonicity");

        // ---- AC 14: the shipped file equals match.md
        [Test]
        public void ShippedFile_MappingAndKnobs_EqualMatchMd()
        {
            var c = MatchConfigLoader.FromJson(ValidJson, MakePlayerConfig());
            Assert.AreEqual("command", c.phases.commandDimensionId);
            for (int i = 0; i < Lanes.Length; i++)
            {
                var l = c.phases.Find(Lanes[i]);
                Assert.IsNotNull(l, Lanes[i]);
                CollectionAssert.AreEqual(PhaseDims[i][0], l.early, Lanes[i] + " early");
                CollectionAssert.AreEqual(PhaseDims[i][1], l.mid, Lanes[i] + " mid");
                CollectionAssert.AreEqual(PhaseDims[i][2], l.late, Lanes[i] + " late");
                Assert.AreEqual(LaneWeights[0][i], l.weightEarly); Assert.AreEqual(LaneWeights[1][i], l.weightMid); Assert.AreEqual(LaneWeights[2][i], l.weightLate);
            }
            Assert.AreEqual(0.95, c.effective.moodFloor); Assert.AreEqual(0.025, c.effective.stressFactor);
            Assert.AreEqual(0.15, c.strength.shortBoardEarly); Assert.AreEqual(0.5, c.strength.shortBoardMid); Assert.AreEqual(0.15, c.strength.shortBoardLate);
            Assert.AreEqual(1, c.strength.midChemistry); Assert.AreEqual(0.3, c.strength.starFactor);
            Assert.AreEqual(4, c.strength.starThreshold); Assert.AreEqual(15, c.strength.starCap);
            Assert.AreEqual(1, c.strength.phaseWeightEarly); Assert.AreEqual(1.1, c.strength.phaseWeightMid); Assert.AreEqual(1, c.strength.phaseWeightLate);
            Assert.AreEqual(0.3, c.execution.e0); Assert.AreEqual(0.4, c.execution.e1); Assert.AreEqual(0.3, c.execution.e2);
            Assert.AreEqual(0.3, c.execution.min); Assert.AreEqual(1, c.execution.max);
            Assert.AreEqual(0.22, c.cycle.strength); Assert.AreEqual(0.4, c.cycle.weightMin);
            CollectionAssert.AreEquivalent(new[] { "early>late", "mid>early", "late>mid" },
                c.cycle.relations.Select(r => MatchPhases.Key(r.beats) + ">" + MatchPhases.Key(r.weakens)));
            Assert.AreEqual(0.1, c.tactic.fitScale); Assert.AreEqual(1, c.tactic.fitCap); Assert.AreEqual(0.5, c.tactic.counter); Assert.AreEqual(3, c.tactic.budget);
            Assert.AreEqual(MatchPhase.Early, c.tactic.FocusOf(MatchTactic.Invade));
            Assert.AreEqual(MatchPhase.Mid, c.tactic.FocusOf(MatchTactic.Teamfight));
            Assert.AreEqual(MatchPhase.Late, c.tactic.FocusOf(MatchTactic.Develop));
            Assert.IsTrue(c.Counters(MatchTactic.Invade, MatchTactic.Develop));
            Assert.IsTrue(c.Counters(MatchTactic.Develop, MatchTactic.Teamfight));
            Assert.IsTrue(c.Counters(MatchTactic.Teamfight, MatchTactic.Invade));
            Assert.AreEqual(2, c.noise.g0); Assert.AreEqual(6, c.noise.g1); Assert.AreEqual(0.3, c.noise.commandReduction);
            Assert.AreEqual(8, c.winProbability.k); Assert.AreEqual(0.15, c.winProbability.pFloor);
            Assert.AreEqual(5, c.specialty.phaseFlatCap); Assert.AreEqual(0.5, c.specialty.starScaleMin); Assert.AreEqual(2, c.specialty.starScaleMax);
            Assert.AreEqual(0.7, c.specialty.sigmaScaleMin); Assert.AreEqual(1.3, c.specialty.sigmaScaleMax);
            Assert.AreEqual(0.5, c.report.minContribution); Assert.AreEqual(2, c.report.minReasons); Assert.AreEqual(3, c.report.maxReasons);
            Assert.AreEqual(0.05, c.report.evenThreshold); Assert.AreEqual(0.6, c.report.luckThreshold); Assert.AreEqual(0.5, c.report.tacticAdviceMin);
            // Calendar, formats and prizes (numbers.md 4.18) unchanged.
            Assert.AreEqual(12, c.calendar.segmentsPerYear);
            CollectionAssert.AreEqual(new[] { 3, 6, 12 }, c.calendar.events.Select(e => e.afterSegment));
            Assert.AreEqual(3, c.format.regularBestOf); Assert.AreEqual(5, c.format.playoffBestOf); Assert.AreEqual(7, c.format.finalsFinalBestOf);
            CollectionAssert.AreEqual(new long[] { 80000, 50000, 30000, 30000, 10000, 10000, 10000, 10000 }, c.format.splitPrizes);
            CollectionAssert.AreEqual(new long[] { 200000, 120000, 60000, 60000 }, c.format.finalsPrizes);
            Assert.AreEqual(60f, c.opponent.familiarityMin); Assert.AreEqual(80f, c.opponent.familiarityMax);
        }

        [Test] public void ShippedFile_FitsShippedPlayerConfig() => Assert.DoesNotThrow(() => MatchConfigLoader.FromJson(ValidJson, PlayerConfigLoader.FromJson(File.ReadAllText("Assets/Data/player_config.json"))));

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
        [Test] public void Validate_BestOfAboveNine_Throws() => AssertInvalid(c => c.format.finalsFinalBestOf = 11, "format.finalsFinalBestOf");
        [Test] public void Validate_TestConfig_IsMatchMdDefaults() => Assert.DoesNotThrow(() => { var c = MakeConfig(); c.Validate(); c.ValidateAgainst(MakePlayerConfig()); });
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
        [Test] public void Validate_SelfCounter_Throws() => AssertInvalid(c => c.tactic.counters[0].beats = c.tactic.counters[0].tactic, "counters itself");
        [Test] public void Validate_CounterBothDirections_Throws() => AssertInvalid(c => c.tactic.counters[1] = new TacticCounterDef { tactic = MatchTactic.Develop, beats = MatchTactic.Invade }, "contradicts");
        [Test] public void Validate_DuplicateCounter_Throws() => AssertInvalid(c => c.tactic.counters[1] = c.tactic.counters[0], "duplicates");
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
            StringAssert.Contains("command", ex.Message);
        }

        [Test] public void ValidateAgainst_ConfigWithCommand_Passes() => MakeConfig().ValidateAgainst(MakePlayerConfig());

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
