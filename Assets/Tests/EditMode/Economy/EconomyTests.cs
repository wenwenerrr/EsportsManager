using System;
using System.Linq;
using EsportsManager.Leagues;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Session;
using NUnit.Framework;
using static EsportsManager.Tests.Leagues.LeagueTestFactory;
using static EsportsManager.Tests.Transfer.TransferTestKit;

namespace EsportsManager.Tests.Economy
{
    /// <summary>Story 005: design/gdd/economy.md rev 3 acceptance criteria 1-13 (AC12 also by screenshot).</summary>
    public class EconomyTests
    {
        private static LeagueWorld W(long seed = Seed) => World(seed, null, false);

        // ------------------------------------------------------------ AC1 F1

        [Test]
        public void MarketSalary_F1Example_28_82Wan_And_14_04WanWithoutBonuses()
        {
            var w = W();
            var p = MakePlayer(w, "mid", 24, 70f, potential: 75f, popularity: 60f);
            Assert.AreEqual(75f, w.Economy.PotentialEstimate(p), 1e-4f);
            Assert.AreEqual(140400, w.Economy.MarketSalary(p, 0f), 100, "WS 66.90 -> 14.04 wan");
            p.AddSpecialty("any_specialty");
            w.Economy.WageIndex = 1.2;
            Assert.AreEqual(288200, w.Economy.MarketSalary(p, 80f), 100, "x 1.71 x 1.2 -> 28.82 wan");
        }

        // ------------------------------------------------------------ AC2 F2 honor

        [Test]
        public void Honor_FinalsRunnerUpPlusSpringChampion_Is80_NewPlayersZero()
        {
            var w = W();
            var e = w.Economy;
            Assert.AreEqual(80f, Math.Max(e.HonorForPlace(2, true), e.HonorForPlace(1, false)));
            Assert.AreEqual(100f, e.HonorForPlace(1, true));
            Assert.AreEqual(30f, e.HonorForPlace(4, false));
            Assert.AreEqual(0f, e.HonorForPlace(5, true));
            Assert.AreEqual(0f, e.HonorForPlace(0, false));
            var fresh = w.Talent.Generate(w.Keys.Stream("t", 1), "top", 18, 18, 55f);
            Assert.AreEqual(0f, e.Honor(fresh));
        }

        [Test]
        public void Honor_AfterWarmUp_RosteredPlayersCarryTheirClubsBestPlace_FreeAgentsZero()
        {
            var w = W();
            w.RunWarmUp();
            var champ = w.Clubs.Single(c => c.LastYearFinalsPlace == 1);
            // the champion's players who were there all year have honor 100 unless they moved in the window
            Assert.IsTrue(champ.Team.Roster.Any(p => w.Economy.Honor(p) == 100f));
            Assert.IsTrue(w.FreeAgents.All(p => w.Economy.Honor(p) == 0f));
        }

        // ------------------------------------------------------------ AC3 F3

        [Test]
        public void WageIndex_NegativeFeedbackAroundRref()
        {
            var e = W().Economy;
            double[] r = { 1.06, 2.0, 0.3, 1.0, 3.0, 0.1, 0.942 };
            double[] exp = { 1.031, 1.281, 0.830, 1.015, 1.3, 0.8, 1.0 };
            for (int i = 0; i < r.Length; i++) Assert.AreEqual(exp[i], e.NextWageIndex(1.0, r[i]), 0.0006, "R " + r[i]);
        }

        // ------------------------------------------------------------ AC4 F4

        [Test]
        public void Value_F4Example_49_95Wan_UsesTheEstimateNotTheTruePotential()
        {
            var w = W();
            var p = MakePlayer(w, "jungle", 21, 75f, potential: 80f);
            p.AddSpecialty("any_specialty");
            Assert.AreEqual(499500, w.Economy.Value(p), 50);
            var q = MakePlayer(w, "jungle", 21, 75f, potential: 70f, bias: 10f);
            q.AddSpecialty("any_specialty");
            Assert.AreEqual(w.Economy.Value(p), w.Economy.Value(q), 1e-6, "same estimate, different true potential");
        }

        // ------------------------------------------------------------ AC5 F5, AC12 breakdown

        [Test]
        public void YearFactor_ThreeYears_Young112_Old092_BreakdownSumsToSalary()
        {
            var w = W();
            var e = w.Economy;
            Assert.AreEqual(1.12f, e.YearFactor(22, 3), 1e-6f);
            Assert.AreEqual(0.92f, e.YearFactor(30, 3), 1e-6f);
            Assert.AreEqual(1f, e.YearFactor(25, 3), 1e-6f);
            e.WageIndex = 1.13;
            var p = MakePlayer(w, "adc", 22, 66f);
            p.AddSpecialty("x");
            w.Market(p).Honor = 60f;
            for (int y = 1; y <= 3; y++)
            {
                var b = e.Breakdown(p, y, 1.04f);
                Assert.AreEqual(b.Total, b.Base + b.WageIndex + b.Honor + b.Specialty + b.Years, 1, "five parts = salary (+-1)");
                Assert.AreEqual(Math.Round(e.MarketSalary(p) * e.YearFactor(p.Age, y) * 1.04f), b.Total, 1.0);
            }
        }

        // ------------------------------------------------------------ AC6 satisfaction

        [Test]
        public void Satisfaction_DeadZoneAndClamp()
        {
            var e = W().Economy;
            Assert.AreEqual(0f, e.SatisfactionFor(100000, 105000), 1e-6f);
            Assert.AreEqual(-0.131f, e.SatisfactionFor(100000, 130000), 0.001f);
            Assert.AreEqual(-0.5f, e.SatisfactionFor(100000, 250000), 1e-6f);
            Assert.AreEqual(0.2f, e.SatisfactionFor(300000, 100000), 1e-6f);
        }

        [Test]
        public void Satisfaction_FeedsTheMoodSalaryTerm_ThroughTheRunner()
        {
            var w = W();
            var c = w.Clubs[0];
            Assert.IsNotNull(c.Runner.SalarySatisfactionSource);
            var p = c.Team.Roster[0];
            Assert.AreEqual(w.Economy.Satisfaction(p), c.Runner.SalarySatisfactionSource(p));
        }

        // ------------------------------------------------------------ AC7 renegotiation trigger

        [Test]
        public void Renegotiation_TriggerAt1_2TimesSalary_WithContractLeft()
        {
            var w = W();
            var p = MakePlayer(w, "top", 24, 70f, contract: 1);
            double m = w.Economy.MarketSalary(p);
            p.Salary = (int)Math.Floor(m / 1.2);
            Assert.IsTrue(w.Economy.WantsRenegotiation(p));
            p.Salary = (int)Math.Ceiling(m / 1.2) + 10;
            Assert.IsFalse(w.Economy.WantsRenegotiation(p));
            p.Salary = 1000; p.ContractYearsLeft = 0;
            Assert.IsFalse(w.Economy.WantsRenegotiation(p), "expired contracts go to renewal instead");
        }

        // ------------------------------------------------------------ AC8 / AC9 self-rescue, loans

        [Test]
        public void FireSale_LowestRatedBenchFirst_At0_6OfValue_UntilFundsNonNegative()
        {
            var w = W();
            var c = w.Clubs[0];
            while (c.Team.Roster.Count > 5) c.Team.RemovePlayer(c.Team.Roster.Last(p => c.Team.LaneOf(p) == null));
            var x = AddPlayer(w, c, "top", 24, 40f);
            var y = AddPlayer(w, c, "top", 24, 55f);
            long px = w.Economy.FireSalePrice(x), py = w.Economy.FireSalePrice(y);
            Assert.AreEqual(Math.Round(w.Economy.Value(x) * 0.6), px, 1.0);
            c.Team.Funds = -(px + 1);
            w.FundsCheck(c, 1);
            Assert.Contains(x, w.FreeAgents); Assert.Contains(y, w.FreeAgents);
            Assert.AreEqual(-(px + 1) + px + py, c.Team.Funds);
            Assert.AreEqual(5, c.Team.Roster.Count);
            Assert.IsTrue(w.News.Any(n => n.Kind == NewsKind.FireSale && n.PlayerName == x.Name));
        }

        [Test]
        public void Loan_AiBorrowsToZero_RepaysHalfOfIncome_DebtOverLineForcesListing()
        {
            var w = W();
            var c = w.Clubs[0];
            while (c.Team.Roster.Count > 5) c.Team.RemovePlayer(c.Team.Roster.Last(p => c.Team.LaneOf(p) == null));
            c.Team.Funds = -400000;
            w.FundsCheck(c, 1);
            Assert.AreEqual(0, c.Team.Funds);
            Assert.AreEqual(400000, c.Debt);
            w.PayIn(c, 100000);
            Assert.AreEqual(350000, c.Debt, "50% of income repays");
            Assert.AreEqual(50000, c.Team.Funds);
            AddPlayer(w, c, "top", 24, 40f);
            w.Ai.PickLineup(c);
            var win = new TransferWindow(w, 0, -1);
            Assert.Greater(c.Debt, 0.25 * TransferWindow.WageBill(c));
            Assert.AreEqual(WindowChoice.Sell, win.AiChoice(c, 1000, out var sell, out _));
            Assert.IsNotNull(c.Team.LaneOf(sell.Single()), "the highest-paid listable starter");
            var pl = w.Clubs[1];
            w.TakeOver(new JobOffer { ClubId = 1, Tier = OfferTier.Mid }, pl.Name);
            while (pl.Team.Roster.Count > 5) pl.Team.RemovePlayer(pl.Team.Roster.Last(p => pl.Team.LaneOf(p) == null));
            pl.Team.Funds = -1;
            w.FundsCheck(pl, 1);
            Assert.IsTrue(pl.InCrisis); Assert.IsFalse(pl.Bankrupt); Assert.AreEqual(0, pl.Debt);
        }

        // ------------------------------------------------------------ AC10 same rules

        [Test]
        public void SameInputs_PlayerAndAi_SameFormulas()
        {
            var w = W();
            var p = MakePlayer(w, "mid", 23, 66f);
            var a = w.Clubs[0]; var b = w.Clubs[1];
            w.TakeOver(new JobOffer { ClubId = 1, Tier = OfferTier.Mid }, b.Name);
            var win = new TransferWindow(w, 0, b.Id);
            var oa = win.MakeOffer(1, a, w.Clubs[2], p, 1000);
            var ob = win.MakeOffer(1, b, w.Clubs[2], p, 1000);
            Assert.AreEqual(oa.NewSalary, ob.NewSalary, "salary does not depend on who signs");
            Assert.AreEqual(oa.RetainSalary, ob.RetainSalary);
            Assert.AreEqual(win.PoachFee(a, p) > 0, win.PoachFee(b, p) > 0);
        }

        // ------------------------------------------------------------ AC11 prize before the funds check, ledger

        [Test]
        public void FinalsPrize_BookedIntoIncome_BeforeTheFundsCheck()
        {
            var w = W();
            while (!(w.Season.Phase == EventPhase.Prepare && w.Season.Current.Event.Type == EventType.Finals))
            {
                if (w.Season.Phase != EventPhase.None) { w.Season.AutoPlay(); continue; }
                w.BeginSegment(); w.ResolveSegment(null);
                if (w.Season.Phase != EventPhase.None && w.Season.Current.Event.Type != EventType.Finals) w.Season.AutoPlay();
            }
            foreach (var c in w.Clubs) { c.Team.Funds = -1; c.Debt = 0; }
            var counts = w.Clubs.ToDictionary(c => c.Id, c => c.Team.Roster.Count);
            var prizeBefore = w.Clubs.ToDictionary(c => c.Id, c => c.Ledger.Prizes);
            w.Season.PlayFinals();
            var ev = w.Season.Current.Event;
            int champ = ev.FinalOrder[0];
            var ch = w.Clubs[champ];
            Assert.AreEqual(300000 - 1, ch.Team.Funds, "prize booked, then the check: no fire sale");
            Assert.AreEqual(counts[champ], ch.Team.Roster.Count);
            Assert.AreEqual(prizeBefore[champ] + 300000, ch.Ledger.Prizes);
            var outside = w.Clubs.First(c => ev.Placing[c.Id] == 0);
            Assert.IsTrue(outside.Team.Funds >= 0, "non-finalists were rescued");
        }

        // ------------------------------------------------------------ extra stress term (kept after champion fatigue was removed)

        [Test]
        public void ExtraStress_IsInsideTheBracket_ScaledByPersonalityAndRandom()
        {
            var w = W();
            var p = MakePlayer(w, "mid", 22, 60f);
            var none = SegmentFormulas.StressDelta(w.SegmentConfig, p, new StressInputs(), 1.1f);
            var fat = SegmentFormulas.StressDelta(w.SegmentConfig, p, new StressInputs { Extra = 6f }, 1.1f);
            Assert.AreEqual(6f * p.GetTraitMultiplier(PlayerEffectKeys.StressGain) * 1.1f, fat - none, 1e-4f);
        }
    }
}
