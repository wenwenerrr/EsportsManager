using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Teams.TeamTestFactory;

namespace EsportsManager.Tests.Teams
{
    /// <summary>Story 002 logic tests (AC 1-9): lineup, strength, familiarity, chemistry, popularity, finance, results.</summary>
    public class TeamTests
    {
        private const float Tol = 1e-3f;

        // ------------------------------------------------------------------ AC1 lineup

        [Test]
        public void SetStarter_RosteredPlayer_FillsSlot()
        {
            var f = new Fixture(lineup: false);
            f.Team.AddPlayer(f.Starters[0]);
            f.Team.SetStarter("top", f.Starters[0]);
            Assert.AreSame(f.Starters[0], f.Team.GetStarter("top"));
            Assert.IsNull(f.Team.GetStarter("mid"));
            Assert.AreEqual("top", f.Team.LaneOf(f.Starters[0]));
        }

        [Test]
        public void SetStarter_PlayerNotOnRoster_Throws()
        {
            var f = new Fixture(lineup: false);
            var ex = Assert.Throws<InvalidOperationException>(() => f.Team.SetStarter("top", f.Starters[0]));
            StringAssert.Contains("not on the roster", ex.Message);
        }

        [Test]
        public void SetStarter_PlayerAlreadyStartingInOtherLane_Throws()
        {
            var f = new Fixture();
            var ex = Assert.Throws<InvalidOperationException>(() => f.Team.SetStarter("mid", f.Starters[0]));
            StringAssert.Contains("already starts in lane 'top'", ex.Message);
            Assert.AreSame(f.Starters[2], f.Team.GetStarter("mid"));
        }

        [Test]
        public void SetStarter_OccupiedLane_ReplacesOccupantAndBenchesHim()
        {
            var f = new Fixture();
            var sub = f.AddBench(10);
            f.Team.SetStarter("mid", sub);
            Assert.AreSame(sub, f.Team.GetStarter("mid"));
            Assert.IsNull(f.Team.LaneOf(f.Starters[2]));
            Assert.IsTrue(f.Team.IsRostered(f.Starters[2]));
        }

        [Test]
        public void SetStarter_UnknownLane_Throws()
        {
            var f = new Fixture();
            Assert.Throws<ArgumentException>(() => f.Team.SetStarter("coach", f.Starters[0]));
            Assert.Throws<ArgumentException>(() => f.Team.GetStarter("coach"));
            Assert.Throws<ArgumentException>(() => f.Team.ClearStarter("coach"));
        }

        [Test]
        public void SwapStarters_TwoLanes_ExchangesOccupants()
        {
            var f = new Fixture();
            f.Team.SwapStarters("top", "adc");
            Assert.AreSame(f.Starters[3], f.Team.GetStarter("top"));
            Assert.AreSame(f.Starters[0], f.Team.GetStarter("adc"));
        }

        [Test]
        public void SwapStarters_WithEmptyLane_MovesPlayer()
        {
            var f = new Fixture();
            f.Team.ClearStarter("adc");
            f.Team.SwapStarters("top", "adc");
            Assert.IsNull(f.Team.GetStarter("top"));
            Assert.AreSame(f.Starters[0], f.Team.GetStarter("adc"));
        }

        [Test]
        public void SwapStarters_SameLaneOrUnknown_Throws()
        {
            var f = new Fixture();
            Assert.Throws<ArgumentException>(() => f.Team.SwapStarters("top", "top"));
            Assert.Throws<ArgumentException>(() => f.Team.SwapStarters("top", "coach"));
        }

        [Test]
        public void RemovePlayer_Starter_ClearsHisSlot()
        {
            var f = new Fixture();
            f.Team.RemovePlayer(f.Starters[1]);
            Assert.IsNull(f.Team.GetStarter("jungle"));
            Assert.IsFalse(f.Team.IsRostered(f.Starters[1]));
            Assert.AreEqual(4, f.Team.Roster.Count);
        }

        [Test]
        public void RemovePlayer_NotOnRoster_Throws()
        {
            var f = new Fixture(lineup: false);
            Assert.Throws<InvalidOperationException>(() => f.Team.RemovePlayer(f.Starters[0]));
        }

        [Test]
        public void AddPlayer_DuplicateIdOrNull_Throws()
        {
            var f = new Fixture();
            Assert.Throws<InvalidOperationException>(() => f.Team.AddPlayer(f.Starters[0]));
            Assert.Throws<ArgumentNullException>(() => f.Team.AddPlayer(null));
        }

        [Test]
        public void Constructor_ConfigMissingALane_Throws()
        {
            var pc = MakePlayerConfig();
            var tc = MakeTeamConfig();
            tc.laneWeights = tc.laneWeights.Take(4).ToArray();
            Assert.Throws<ArgumentException>(() => new Team(tc, pc, new StubRandomSource(0.5f)));
        }

        // ------------------------------------------------------------------ AC2 strength

        [Test]
        public void Strength_UniformStarters_EqualsEffectiveScore()
        {
            var f = new Fixture();
            // lane score 50 x mood factor (0.7 + 0.3 x 0.5 = 0.85) x stress factor 1
            Assert.AreEqual(42.5f, f.Team.Strength(), Tol);
        }

        [Test]
        public void EffectiveScore_StressLowersByKappa()
        {
            var f = new Fixture();
            f.Starters[0].Stress = 50f; // 1 - 0.2 x 0.5 = 0.9
            Assert.AreEqual(50f * 0.85f * 0.9f, TeamStrength.EffectiveScore(f.Config, f.Starters[0], "top"), Tol);
        }

        [Test]
        public void EffectiveScore_ResoluteTraitHalvesStressImpact()
        {
            var f = new Fixture();
            var p = MakePlayer(f.Players, 20, "top", traitA: "resolute", traitB: NeutralB, stress: 50f); // 1 - 0.2 x 0.5 x 0.5
            Assert.AreEqual(50f * 0.85f * 0.95f, TeamStrength.EffectiveScore(f.Config, p, "top"), Tol);
        }

        [Test]
        public void EffectiveScore_MoodScalesFromBaseToFull()
        {
            var f = new Fixture();
            f.Starters[0].Mood = 0f;
            Assert.AreEqual(50f * 0.7f, TeamStrength.EffectiveScore(f.Config, f.Starters[0], "top"), Tol);
            f.Starters[0].Mood = 100f;
            Assert.AreEqual(50f, TeamStrength.EffectiveScore(f.Config, f.Starters[0], "top"), Tol);
        }

        [Test]
        public void EffectiveScore_EmptySlot_IsZero()
        {
            Assert.AreEqual(0f, TeamStrength.EffectiveScore(MakeTeamConfig(), null, "top"));
        }

        [Test]
        public void Strength_OneWeakStarter_ShortBoardPenaltyApplies()
        {
            var f = new Fixture();
            var weak = MakePlayer(f.Players, 30, "adc", fill: 20f);
            f.Team.AddPlayer(weak);
            f.Team.RemovePlayer(f.Starters[3]);
            f.Team.SetStarter("adc", weak);
            // effective: 4 x 42.5 and 17; avg 37.4, mean 37.4, min 17 -> 37.4 - 0.3 x 20.4
            Assert.AreEqual(37.4f - 0.3f * 20.4f, f.Team.Strength(), Tol);
        }

        [Test]
        public void Strength_EmptySlot_CountsAsZeroEffective()
        {
            var f = new Fixture();
            f.Team.ClearStarter("support");
            // effective: 4 x 42.5 and 0; avg = mean = 34, min 0 -> 34 - 0.3 x 34
            Assert.AreEqual(34f - 0.3f * 34f, f.Team.Strength(), Tol);
        }

        [Test]
        public void Strength_NoStarters_IsZero()
        {
            var f = new Fixture(lineup: false);
            Assert.AreEqual(0f, f.Team.Strength());
        }

        [Test]
        public void Strength_UnequalLaneWeights_WeightedAverage()
        {
            var f = new Fixture();
            f.Config.laneWeights.First(w => w.lane == "mid").weight = 3f;
            var star = MakePlayer(f.Players, 31, "mid", fill: 100f);
            f.Team.AddPlayer(star);
            f.Team.RemovePlayer(f.Starters[2]);
            f.Team.SetStarter("mid", star);
            // effective: 4 x 42.5 and 85; weighted mean W = (170 + 3 x 85)/7; min 42.5 -> W - 0.3 x (W - 42.5)
            float w = (170f + 255f) / 7f;
            Assert.AreEqual(w - 0.3f * (w - 42.5f), f.Team.Strength(), Tol);
        }

        [Test]
        public void Strength_HeavyWeightOnWeakLane_PenaltyUsesWeightedMean()
        {
            var f = new Fixture();
            f.Config.laneWeights.First(w => w.lane == "adc").weight = 4f;
            var weak = MakePlayer(f.Players, 30, "adc", fill: 20f);
            f.Team.AddPlayer(weak);
            f.Team.RemovePlayer(f.Starters[3]);
            f.Team.SetStarter("adc", weak);
            // effective: 4 x 42.5 and 17 (weight 4); W = (170 + 68)/8 = 29.75; unweighted mean would be 37.4
            float w = (170f + 4f * 17f) / 8f;
            Assert.AreEqual(w - 0.3f * (w - 17f), f.Team.Strength(), Tol);
        }

        [Test]
        public void Strength_PureFunction_MatchesTeamAndIsRepeatable()
        {
            var f = new Fixture();
            f.Starters[1].Stress = 30f;
            float a = TeamStrength.Compute(f.Config, f.Players, f.Team.StartersByLane());
            float b = TeamStrength.Compute(f.Config, f.Players, f.Team.StartersByLane());
            Assert.AreEqual(a, b);
            Assert.AreEqual(a, f.Team.Strength());
            Assert.AreEqual(0f, TeamStrength.Compute(f.Config, f.Players, null));
        }

        // ------------------------------------------------------------------ AC3 familiarity

        [Test]
        public void Settle_BothStarters_AddFSegForEveryPair()
        {
            var f = new Fixture();
            f.Team.SettleSegment(Input());
            for (int i = 0; i < 5; i++)
                for (int j = i + 1; j < 5; j++)
                    Assert.AreEqual(5f, f.Team.GetFamiliarity(f.Starters[i].Id, f.Starters[j].Id), Tol);
        }

        [Test]
        public void Settle_GentlePlayerInPair_MultipliesFSeg()
        {
            var f = new Fixture();
            var gentle = MakePlayer(f.Players, 40, "top", traitA: "gentle", traitB: NeutralB);
            f.Team.AddPlayer(gentle);
            f.Team.RemovePlayer(f.Starters[0]);
            f.Team.SetStarter("top", gentle);
            f.Team.SettleSegment(Input());
            Assert.AreEqual(5f * 1.1f, f.Team.GetFamiliarity(40, 2), Tol);
            Assert.AreEqual(5f, f.Team.GetFamiliarity(2, 3), Tol);
        }

        [Test]
        public void Settle_EachWin_AddsFWinPerPairLossesDoNot()
        {
            var f = new Fixture();
            f.Team.SettleSegment(Input(true, true, false));
            Assert.AreEqual(5f + 2f * 2f, f.Team.GetFamiliarity(1, 2), Tol);
        }

        [Test]
        public void Settle_PairNotBothStarters_DecaysAndStopsAtZero()
        {
            var f = new Fixture();
            var bench = f.AddBench(10);
            f.Team.SetFamiliarity(1, 10, 1.5f);
            f.Team.SettleSegment(Input());
            Assert.AreEqual(0.5f, f.Team.GetFamiliarity(1, 10), Tol);
            f.Team.SettleSegment(Input());
            Assert.AreEqual(0f, f.Team.GetFamiliarity(1, 10), Tol);
        }

        [Test]
        public void Settle_Familiarity_CappedAtFMax()
        {
            var f = new Fixture();
            f.Team.SetFamiliarity(1, 2, 98f);
            f.Team.SettleSegment(Input(true, true));
            Assert.AreEqual(100f, f.Team.GetFamiliarity(1, 2), Tol);
        }

        [Test]
        public void Familiarity_KeyedByIdUnordered_AndKeptAfterPlayerLeaves()
        {
            var f = new Fixture();
            f.Team.SetFamiliarity(2, 1, 30f);
            Assert.AreEqual(30f, f.Team.GetFamiliarity(1, 2));
            f.Team.RemovePlayer(f.Starters[0]);
            f.Team.SettleSegment(Input());
            Assert.AreEqual(29f, f.Team.GetFamiliarity(1, 2), Tol); // decays, not erased
        }

        // ------------------------------------------------------------------ AC4 chemistry

        private static Player Pair(Fixture f, long id, string a, string b) => MakePlayer(f.Players, id, "top", traitA: a, traitB: b);

        [Test]
        public void PairChemistry_PositiveCompatibility_RaisesAboveF()
        {
            var f = new Fixture();
            var a = Pair(f, 50, "assertive", NeutralA);
            var b = Pair(f, 51, "gentle", NeutralB); // H = +0.5
            f.Team.SetFamiliarity(50, 51, 40f);
            Assert.AreEqual(40f * (1f + 0.5f * 0.5f), f.Team.PairChemistry(a, b), Tol);
        }

        [Test]
        public void PairChemistry_NegativeCompatibility_LowersBelowF()
        {
            var f = new Fixture();
            var a = Pair(f, 50, "assertive", NeutralA);
            var b = Pair(f, 51, "assertive", NeutralB); // H = -0.5
            f.Team.SetFamiliarity(50, 51, 40f);
            Assert.AreEqual(40f * (1f - 0.25f), f.Team.PairChemistry(a, b), Tol);
        }

        [Test]
        public void PairChemistry_ClampedTo100()
        {
            var f = new Fixture();
            var a = Pair(f, 50, "assertive", "loyal");
            var b = Pair(f, 51, "gentle", "loyal"); // H = +1
            f.Team.SetFamiliarity(50, 51, 100f);
            Assert.AreEqual(100f, f.Team.PairChemistry(a, b), Tol);
        }

        [Test]
        public void PairChemistry_NoFamiliarity_IsZero()
        {
            var f = new Fixture();
            Assert.AreEqual(0f, f.Team.PairChemistry(f.Starters[0], f.Starters[1]));
        }

        [Test]
        public void Chemistry_IsMeanOfTenStarterPairs()
        {
            var f = new Fixture();
            f.SetAllStarterPairsF(20f);
            Assert.AreEqual(20f, f.Team.Chemistry(), Tol);
            f.Team.SetFamiliarity(1, 2, 30f);
            Assert.AreEqual((9f * 20f + 30f) / 10f, f.Team.Chemistry(), Tol);
        }

        [Test]
        public void Chemistry_EmptySlot_PairsCountAsZero()
        {
            var f = new Fixture();
            f.SetAllStarterPairsF(20f);
            f.Team.ClearStarter("support");
            Assert.AreEqual(6f * 20f / 10f, f.Team.Chemistry(), Tol);
        }

        // ------------------------------------------------------------------ AC5 substitution

        [Test]
        public void Chemistry_NewStarter_DropsOnlyBecauseHisFamiliarityIsZero()
        {
            var f = new Fixture();
            f.SetAllStarterPairsF(20f);
            var sub = f.AddBench(10, "support");
            f.Team.SetStarter("support", sub); // replaces Starters[4]
            Assert.AreEqual(6f * 20f / 10f, f.Team.Chemistry(), Tol); // 4 new pairs have F = 0
        }

        [Test]
        public void SwapAndSwapBack_IsNotDoublePenalisedBeyondFamiliarityDecay()
        {
            var f = new Fixture();
            f.SetAllStarterPairsF(20f);
            var sub = f.AddBench(10, "support");
            var original = f.Starters[4];

            f.Team.SetStarter("support", sub);
            f.Team.SettleSegment(Input());   // ABCD together: 20 + 5; pairs with the original: 20 - 1
            f.Team.SetStarter("support", original);

            // Back to the original five: nothing but F decides chemistry.
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(19f, f.Team.GetFamiliarity(f.Starters[i].Id, original.Id), Tol, "pair with the swapped-out player lost exactly f_decay");
                for (int j = i + 1; j < 4; j++) Assert.AreEqual(25f, f.Team.GetFamiliarity(f.Starters[i].Id, f.Starters[j].Id), Tol);
            }
            Assert.AreEqual((6f * 25f + 4f * 19f) / 10f, f.Team.Chemistry(), Tol);

            // Swapping lanes around and back changes nothing at all.
            float before = f.Team.Chemistry();
            f.Team.SwapStarters("top", "mid");
            f.Team.SwapStarters("top", "mid");
            Assert.AreEqual(before, f.Team.Chemistry(), 0f);
        }

        // ------------------------------------------------------------------ AC6 player popularity

        private static float ExpectedPlayerE(float club, float lane, float looks, float stream, float win, float biz) =>
            (1f * club + 1f * lane + 0.8f * looks + 1.5f * stream + 1f * win + 0.8f * biz) / 6.1f;

        [Test]
        public void Popularity_NoStreamNoBusiness_MovesTowardLowerTarget()
        {
            var f = new Fixture();
            f.Team.SettleSegment(Input());
            float e = ExpectedPlayerE(30f, 50f, 50f, 0f, 50f, 0f); // default win rate 50
            Assert.AreEqual(50f + 0.25f * (e - 50f), f.Starters[0].Popularity, Tol);
            Assert.Less(f.Starters[0].Popularity, 50f);
        }

        [Test]
        public void Popularity_HeatFromCaller_RaisesTarget()
        {
            var f = new Fixture();
            var input = Input();
            input.Heat = new Dictionary<long, PlayerHeat> { { 1, new PlayerHeat { Stream = 100f, Business = 100f } } };
            f.Team.SettleSegment(input);
            float e = ExpectedPlayerE(30f, 50f, 50f, 100f, 50f, 100f);
            Assert.AreEqual(50f + 0.25f * (e - 50f), f.Starters[0].Popularity, Tol);
            Assert.Greater(f.Starters[0].Popularity, f.Starters[1].Popularity); // player 2 got no heat
        }

        [Test]
        public void Popularity_RecentWinRate_EntersTarget()
        {
            var f = new Fixture();
            f.Team.SettleSegment(Input(true, true, true, true)); // 100 %
            float e = ExpectedPlayerE(30f, 50f, 50f, 0f, 100f, 0f);
            Assert.AreEqual(50f + 0.25f * (e - 50f), f.Starters[0].Popularity, Tol);
        }

        [Test]
        public void Popularity_RandomFactorScalesDeltaWithinRange()
        {
            var low = new Fixture(new StubRandomSource(0f));    // R = 0.85
            var high = new Fixture(new StubRandomSource(1f));   // R = 1.15
            low.Team.SettleSegment(Input());
            high.Team.SettleSegment(Input());
            float dLow = 50f - low.Starters[0].Popularity, dHigh = 50f - high.Starters[0].Popularity;
            Assert.AreEqual(0.85f / 1.15f, dLow / dHigh, Tol);
        }

        [Test]
        public void Popularity_ClampedAt100()
        {
            var f = new Fixture();
            foreach (var p in f.Starters) { p.Popularity = 100f; p.Looks = 100f; for (int i = 0; i < 6; i++) p.SetDimension(p.MainLane, i, 100f); }
            f.Team.ClubPopularity = 100f;
            var input = Input(true);
            input.SeasonRankScore = 100f;
            input.Heat = f.Starters.ToDictionary(p => p.Id, p => new PlayerHeat { Stream = 100f, Business = 100f });
            for (int i = 0; i < 5; i++) f.Team.SettleSegment(input);
            foreach (var p in f.Starters) Assert.AreEqual(100f, p.Popularity, Tol);
            Assert.AreEqual(100f, f.Team.ClubPopularity, Tol);
        }

        [Test]
        public void Popularity_WithoutExposure_DriftsDownButStaysAboveZero()
        {
            var f = new Fixture();
            f.Starters[0].Popularity = 90f;
            float last = 90f;
            for (int i = 0; i < 30; i++)
            {
                f.Team.SettleSegment(Input());
                Assert.LessOrEqual(f.Starters[0].Popularity, last + 1e-4f);
                last = f.Starters[0].Popularity;
            }
            Assert.Less(last, 40f);
            Assert.GreaterOrEqual(last, 0f);
        }

        [Test]
        public void Popularity_SameSeed_SameResult()
        {
            float Run()
            {
                var f = new Fixture(new SeededRandomSource(TestConfigFactory.FixedSeed));
                for (int i = 0; i < 6; i++) f.Team.SettleSegment(Input(true, false));
                return f.Starters[0].Popularity + f.Team.ClubPopularity;
            }
            Assert.AreEqual(Run(), Run());
        }

        // ------------------------------------------------------------------ AC7 club popularity

        [Test]
        public void ClubPopularity_MovesTowardTargetFromStartersWinRateAndRank()
        {
            var f = new Fixture();
            var input = Input(true, true);
            input.SeasonRankScore = 80f;
            f.Team.SettleSegment(input);
            float e = (1f * 50f + 1f * 100f + 0.5f * 80f) / 2.5f; // 76, uses starter popularity BEFORE this segment's update
            Assert.AreEqual(30f + 0.2f * (e - 30f), f.Team.ClubPopularity, Tol);
        }

        [Test]
        public void ClubPopularity_RandomFactorUsesTenPercentRange()
        {
            var low = new Fixture(new StubRandomSource(0f));  // R = 0.9
            var high = new Fixture(new StubRandomSource(1f)); // R = 1.1
            low.Team.SettleSegment(Input());
            high.Team.SettleSegment(Input());
            float dl = low.Team.ClubPopularity - 30f, dh = high.Team.ClubPopularity - 30f;
            Assert.AreEqual(0.9f / 1.1f, dl / dh, Tol);
        }

        // ------------------------------------------------------------------ AC8 finance

        [Test]
        public void Settle_Finance_IncomeMinusExpenses_BenchSalariesIncluded()
        {
            var f = new Fixture(funds: 100000);
            f.AddBench(10); // 6 players x 120000 / 12 = 60000 per segment
            var input = Input();
            input.PrizeMoney = 10000; input.LivestreamIncome = 2000; input.BusinessIncome = 3000; input.Sponsorship = 4000; input.EventIncome = 500;
            input.TransferFees = 7000; input.EventExpenses = 1000;
            var r = f.Team.SettleSegment(input);
            Assert.AreEqual(19500L, r.Income);
            Assert.AreEqual(60000L + 7000L + 1000L, r.Expenses);
            Assert.AreEqual(60000L, r.SalaryShare);
            Assert.AreEqual(-48500L, r.Net);
            Assert.AreEqual(51500L, f.Team.Funds);
            Assert.AreEqual(51500L, r.Funds);
        }

        [Test]
        public void Settle_FundsMayGoNegative_NoBankruptcyLogic()
        {
            var f = new Fixture(funds: 0);
            f.Team.SettleSegment(Input());
            Assert.Less(f.Team.Funds, 0L);
        }

        [Test]
        public void SalaryShare_SumsExactlyToAnnualTotalOverSegmentsPerYear()
        {
            var f = new Fixture(lineup: false);
            var p = f.AddBench(10, salary: 100);
            long sum = 0;
            for (int i = 0; i < 12; i++) sum += f.Team.SalaryShare(12, i);
            Assert.AreEqual(100L, sum); // 100/12 = 8 remainder 4: first four segments pay 9
            Assert.AreEqual(9L, f.Team.SalaryShare(12, 3));
            Assert.AreEqual(8L, f.Team.SalaryShare(12, 4));
        }

        [Test]
        public void SalaryShare_RemovedPlayerNoLongerCounts()
        {
            var f = new Fixture();
            long before = f.Team.SalaryShare(12, 0);
            f.Team.RemovePlayer(f.Starters[0]);
            Assert.AreEqual(before - 10000L, f.Team.SalaryShare(12, 0));
        }

        [Test]
        public void SalaryShare_SegmentsPerYearIsCallerSupplied()
        {
            var f = new Fixture();
            Assert.AreEqual(5L * 120000L / 4L, f.Team.SalaryShare(4, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Team.SalaryShare(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Team.SalaryShare(12, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Team.SalaryShare(12, -1));
        }

        [Test]
        public void LivestreamIncome_StreamsAfterTheFirstPaySecondStreamFactor()
        {
            var f = new Fixture();
            f.Config.livestream.secondStreamFactor = 0.5f;
            Assert.AreEqual(500 + 20 * 50, f.Team.LivestreamIncome(f.Starters[0], 1));
            Assert.AreEqual((int)(1.5 * (500 + 20 * 50)), f.Team.LivestreamIncome(f.Starters[0], 2));
            Assert.AreEqual(2 * (500 + 20 * 50), f.Team.LivestreamIncome(f.Starters[0], 3));
            Assert.AreEqual(0, f.Team.LivestreamIncome(f.Starters[0], 0));
        }

        [Test]
        public void LivestreamIncome_BasePlusPopularityPerStream()
        {
            var f = new Fixture();
            Assert.AreEqual(3 * (500 + 20 * 50), f.Team.LivestreamIncome(f.Starters[0], 3));
            Assert.AreEqual(0, f.Team.LivestreamIncome(f.Starters[0], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Team.LivestreamIncome(f.Starters[0], -1));
        }

        [Test]
        public void Settle_NegativeRosterSalary_ThrowsBeforeAnyStateChanges()
        {
            var f = new Fixture(funds: 500);
            f.SetAllStarterPairsF(10f);
            var bench = f.AddBench(10);
            bench.Salary = -1;
            float pop = f.Starters[0].Popularity, club = f.Team.ClubPopularity;
            var input = Input(true, true);
            input.Heat = new Dictionary<long, PlayerHeat> { { 1, new PlayerHeat { Stream = 100f, Business = 100f } } };
            var ex = Assert.Throws<InvalidOperationException>(() => f.Team.SettleSegment(input));
            StringAssert.Contains("negative salary", ex.Message);
            Assert.AreEqual(500L, f.Team.Funds);
            Assert.AreEqual(10f, f.Team.GetFamiliarity(1, 2));
            Assert.AreEqual(0, f.Team.RecentResults.Count);
            Assert.AreEqual(pop, f.Starters[0].Popularity);
            Assert.AreEqual(club, f.Team.ClubPopularity);
        }

        [Test]
        public void Settle_InvalidInput_ThrowsAndChangesNothing()
        {
            var f = new Fixture(funds: 500);
            f.SetAllStarterPairsF(10f);
            var bad = new List<SegmentInput>();
            var i = Input(true); i.SegmentsPerYear = 0; bad.Add(i);
            i = Input(true); i.SegmentIndexInYear = 12; bad.Add(i);
            i = Input(true); i.PrizeMoney = -1; bad.Add(i);
            i = Input(true); i.SeasonRankScore = 101f; bad.Add(i);
            i = Input(true); i.Heat = new Dictionary<long, PlayerHeat> { { 1, new PlayerHeat { Stream = 101f } } }; bad.Add(i);
            foreach (var b in bad) Assert.Throws<ArgumentOutOfRangeException>(() => f.Team.SettleSegment(b));
            Assert.AreEqual(500L, f.Team.Funds);
            Assert.AreEqual(10f, f.Team.GetFamiliarity(1, 2));
            Assert.AreEqual(0, f.Team.RecentResults.Count);
        }

        // ------------------------------------------------------------------ AC9 recent results + averages

        [Test]
        public void RecentWinRate_NoRecords_UsesConfigDefault()
        {
            var f = new Fixture();
            Assert.AreEqual(50f, f.Team.RecentWinRate());
        }

        [Test]
        public void RecentWinRate_KeepsOnlyLastNResults()
        {
            var f = new Fixture(); // window 5
            f.Team.SettleSegment(Input(false, false, false, false, false));
            Assert.AreEqual(0f, f.Team.RecentWinRate());
            f.Team.SettleSegment(Input(true, true, true));
            Assert.AreEqual(5, f.Team.RecentResults.Count);
            Assert.AreEqual(60f, f.Team.RecentWinRate(), Tol); // F F T T T
            CollectionAssert.AreEqual(new[] { false, false, true, true, true }, f.Team.RecentResults.ToArray());
        }

        [Test]
        public void AverageStarterMoodAndStress_AreMeansOverStartersOnly()
        {
            var f = new Fixture();
            var bench = f.AddBench(10);
            bench.Mood = 0f; bench.Stress = 100f;
            for (int i = 0; i < 5; i++) { f.Starters[i].Mood = 10f * (i + 1); f.Starters[i].Stress = 20f; }
            Assert.AreEqual(30f, f.Team.AverageStarterMood(), Tol);
            Assert.AreEqual(20f, f.Team.AverageStarterStress(), Tol);
            f.Team.ClearStarter("top");
            f.Team.ClearStarter("jungle");
            f.Team.ClearStarter("mid");
            f.Team.ClearStarter("adc");
            f.Team.ClearStarter("support");
            Assert.AreEqual(0f, f.Team.AverageStarterMood());
        }
    }
}
