using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EsportsManager.Leagues;
using EsportsManager.Players;
using EsportsManager.Session;
using NUnit.Framework;
using static EsportsManager.Tests.Leagues.LeagueTestFactory;
using static EsportsManager.Tests.Transfer.TransferTestKit;

namespace EsportsManager.Tests.Transfer
{
    /// <summary>
    /// Story 005: design/gdd/transfer.md rev 3 acceptance criteria 1-17 (AC15 = screenshots in production/qa/evidence) and the
    /// warm-up -> year 1 -> window -> year 2 integration run.
    /// </summary>
    public class TransferTests
    {
        private static LeagueWorld W(long seed = Seed) => World(seed, null, false);

        private static Player StarterWithMate(LeagueWorld w, Club c, out Player mate)
        {
            // a main-lane starter who is NOT his lane's only main-lane player (a bench mate of the same lane is added)
            var p = c.Team.Starters().First(s => c.Team.LaneOf(s) == s.MainLane);
            mate = AddPlayer(w, c, p.MainLane, 22, 40f);
            return p;
        }

        // ------------------------------------------------------------ AC1 rollover

        [Test]
        public void Rollover_AgesDeclineByPreIncrementBand_ContractToZeroEntersRenewal()
        {
            var w = W();
            var c = w.Clubs[0];
            string lane = w.Players.lanes[0].id;
            int[] ages = { 26, 27, 29, 31 };
            float[] decline = { 0, 1, 2, 4 };
            var ps = ages.Select((a, i) => AddPlayer(w, c, lane, a, 60f, contract: i == 0 ? 1 : 2)).ToList();
            var win = new TransferWindow(w, 0, c.Id);
            win.Continue();
            Assert.That(win.Step, Is.EqualTo(TransferStep.Renegotiation).Or.EqualTo(TransferStep.Renewal));
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(ages[i] + 1, ps[i].Age);
                Assert.AreEqual(60f - decline[i], ps[i].GetDimension(lane, 0), 1e-4f, "decline by the age band before + 1");
            }
            Assert.AreEqual(0, ps[0].ContractYearsLeft);
            TransferTestKit.AdvanceTo(win, w, TransferWait.Renewal);
            Assert.IsTrue(win.Renewals.Any(r => r.Player == ps[0] && r.ClubId == c.Id), "contract 1 -> 0 enters the renewal step");
        }

        // ------------------------------------------------------------ AC2 AI renewals

        private static RenewalCase Case(Club c, Player p, long ask) => new RenewalCase { ClubId = c.Id, Player = p, OldSalary = p.Salary, Ask = new[] { ask, ask, ask } };

        [Test]
        public void AiRenewal_StarterAtFundsZero_BenchByValueRatio_Age29NeedsBenchTest()
        {
            var w = W();
            var c = w.Clubs[0];
            c.Personality.OffRenew = 0f;
            var win = new TransferWindow(w, 0, -1);
            double wi = w.Economy.WageIndex;
            // starter, funds >= 0 -> renews, years by age
            var st = StarterWithMate(w, c, out var mate);
            c.Team.Funds = 0;
            var rc = Case(c, st, 50000);
            win.DecideAiRenewal(c, rc);
            Assert.AreEqual(win.AiRenewYears(st.Age), rc.Years);
            // starter at funds < 0 -> released
            var st2 = c.Team.Starters().First(s => s != st);
            c.Team.Funds = -1;
            var rc2 = Case(c, st2, 50000);
            win.DecideAiRenewal(c, rc2);
            Assert.AreEqual(0, rc2.Years);
            Assert.Contains(st2, w.FreeAgents);
            // bench: value / (salary / WI) 1.6 -> renew, 1.4 -> release
            c.Team.Funds = 50_000_000;
            double v = w.Economy.Value(mate);
            var b1 = Case(c, mate, (long)Math.Round(v * wi / 1.6));
            win.DecideAiRenewal(c, b1);
            Assert.AreEqual(win.AiRenewYears(mate.Age), b1.Years);
            var mate2 = AddPlayer(w, c, mate.MainLane, 22, 40f);
            double v2 = w.Economy.Value(mate2);
            var b2 = Case(c, mate2, (long)Math.Round(v2 * wi / 1.4));
            win.DecideAiRenewal(c, b2);
            Assert.AreEqual(0, b2.Years);
            // 29-year-old starter who is not his lane's only main-lane player: needs the bench test
            var old = AddPlayer(w, c, st.MainLane, 29, 70f);
            c.Team.SetStarter(st.MainLane, old);
            double vo = w.Economy.Value(old);
            var o1 = Case(c, old, (long)Math.Round(vo * wi / 1.4));
            win.DecideAiRenewal(c, o1);
            Assert.AreEqual(0, o1.Years, "29 + not sole: 1.4 fails the bench test");
            var old2 = AddPlayer(w, c, st.MainLane, 29, 70f);
            c.Team.SetStarter(st.MainLane, old2);
            var o2 = Case(c, old2, (long)Math.Round(w.Economy.Value(old2) * wi / 1.6));
            win.DecideAiRenewal(c, o2);
            Assert.AreEqual(1, o2.Years, ">= 28 renews 1 year");
        }

        // ------------------------------------------------------------ AC3 retain raise

        [Test]
        public void Retain_WhenBuyerOfferTimes1_1IsBelowCurrentSalary_KeepsCurrentSalary()
        {
            // Arrange: a player already paid far above what the buyer would pay him
            var w = W();
            var me = w.Clubs[0]; var buyer = w.Clubs[1];
            var win = new TransferWindow(w, 0, me.Id);
            var p = me.Team.Starters()[0];
            p.Salary = 5000000;
            // Act
            var o = win.MakeOffer(1, buyer, me, p, 123400);
            // Assert: retaining never lowers the salary (user decision 2026-10-29)
            Assert.Less(o.NewSalary * 1.1, 5000000.0);
            Assert.AreEqual(5000000L, o.RetainSalary);
        }

        [Test]
        public void PlayerRetain_SalaryIsBuyerSigningTimes1_1_ContractPlusOne_SecondOfferOnlyAccept()
        {
            var w = W();
            var me = w.Clubs[0]; var buyer = w.Clubs[1];
            var win = new TransferWindow(w, 0, me.Id);
            var p = me.Team.Starters()[0];
            w.Market(p).Honor = 80f;
            var o = win.MakeOffer(1, buyer, me, p, 123400);
            float r = w.Economy.DrawSalaryRandom(w.SalaryStream(p, LeagueWorld.SalaryKind.Poach));
            Assert.AreEqual(w.Economy.Breakdown(p, 2, r).Total, o.NewSalary, "buyer's signing salary (2 years, own honor)");
            long withR1 = w.Economy.Breakdown(p, 2, 1f).Total;
            Assert.AreEqual(Math.Round(w.Economy.MarketSalary(p) * w.Economy.YearFactor(p.Age, 2)), withR1, 1.0, "R injected 1 = F1 x F5");
            Assert.AreEqual(Math.Max((long)p.Salary, (long)Math.Round(o.NewSalary * 1.1)), o.RetainSalary);
            int contract = p.ContractYearsLeft;
            win.SetIncoming(o);
            Assert.AreEqual(TransferError.None, win.RespondPoach(false));
            Assert.AreEqual(o.RetainSalary, p.Salary);
            Assert.AreEqual(contract + 1, p.ContractYearsLeft);
            Assert.IsNotNull(me.Find(p.Id));
            var o2 = win.MakeOffer(2, buyer, me, p, 123400);
            Assert.IsFalse(o2.CanRetain);
            win.SetIncoming(o2);
            Assert.AreEqual(TransferError.NotAllowed, win.RespondPoach(false));
            long before = me.Team.Funds;
            Assert.AreEqual(TransferError.None, win.RespondPoach(true));
            Assert.IsNotNull(buyer.Find(p.Id));
            Assert.AreEqual(before + 123400, me.Team.Funds, "fee to the seller");
            Assert.AreEqual(80f, w.Market(p).Honor, "honor travels with the player (economy AC2)");
            Assert.AreEqual(2, p.ContractYearsLeft);
        }

        // ------------------------------------------------------------ AC4 target guards

        [Test]
        public void PoachTarget_SellerWithFivePlayersOrSoleMainStarter_IsNotATarget()
        {
            var w = W();
            var win = new TransferWindow(w, 0, -1);
            var c = w.Clubs[2];
            var sole = c.Team.Starters().FirstOrDefault(s => c.Team.LaneOf(s) == s.MainLane && win.SoleMain(c, s));
            if (sole == null)
            {
                sole = AddPlayer(w, c, w.Players.lanes[0].id, 24, 50f);
                foreach (var q in c.Team.Roster.Where(q => q != sole && q.MainLane == sole.MainLane).ToList()) c.Team.RemovePlayer(q);
                c.Team.SetStarter(sole.MainLane, sole);
            }
            while (c.Team.Roster.Count < 7) AddPlayer(w, c, sole.MainLane == "top" ? "mid" : "top", 22, 40f);
            Assert.IsFalse(win.CanBeTarget(c, sole), "a lane's only main-lane starter");
            var st = StarterWithMate(w, c, out _);
            Assert.IsTrue(win.CanBeTarget(c, st));
            while (c.Team.Roster.Count > 5) c.Team.RemovePlayer(c.Team.Roster.Last(q => q != st && c.Team.LaneOf(q) == null));
            Assert.IsFalse(win.CanBeTarget(c, st), "seller with 5 players");
        }

        // ------------------------------------------------------------ AC5 F4 / F3

        [Test]
        public void MaxBid_F4Example_616k_ExitAt590kRaiseAt580k()
        {
            var w = W();
            var win = new TransferWindow(w, 0, -1);
            double max = win.MaxBid(400000, 1f, 0.5f, 0f, 1.0);
            Assert.AreEqual(616000, max, 0.5);
            Assert.AreEqual(29500, win.Step3(590000));
            Assert.AreEqual(29000, win.Step3(580000));
            Assert.AreEqual(15000, win.Step3(300000));
            Assert.AreEqual(5000, win.Step3(60000));
            Assert.IsFalse(win.AiRaises(max, 590000, 10_000_000, 0, 6));
            Assert.IsTrue(win.AiRaises(max, 580000, 10_000_000, 0, 6));
            Assert.IsFalse(win.AiRaises(max, 580000, 600000, 2000, 6), "keeps the 2-segment reserve");
            Assert.IsFalse(win.AiRaises(max, 580000, 10_000_000, 0, 8), "roster would exceed 8");
        }

        // ------------------------------------------------------------ AC6 / AC7 / AC8 / AC9 auction

        private static (LeagueWorld w, TransferWindow win, Club a, Club b) Auction(int player)
        {
            var w = W();
            var win = new TransferWindow(w, 0, player);
            var a = w.Clubs[1]; var b = w.Clubs[2];
            a.Team.ClubPopularity = 90f; b.Team.ClubPopularity = 10f;
            a.Team.Funds = b.Team.Funds = 10_000_000;
            win.Choices[a.Id] = WindowChoice.Auction; win.Choices[b.Id] = WindowChoice.Auction;
            return (w, win, a, b);
        }

        [Test]
        public void Auction_TwoAis_HigherLimitWinsBetween286kAnd315k()
        {
            var (w, win, a, b) = Auction(-1);
            var p = w.FreeAgents[0];
            var lot = win.LeagueLot(p, LotKind.Free);
            lot.StartPrice = 200000;
            win.OpenLot(lot);
            CollectionAssert.AreEqual(new[] { a.Id, b.Id }, lot.InClubs, "popularity order");
            lot.Limits[a.Id] = 400000; lot.Limits[b.Id] = 300000;
            long fundsA = a.Team.Funds;
            Assert.IsTrue(win.RunLot(lot));
            Assert.AreEqual(a.Id, lot.TopBidder);
            Assert.That(lot.Price, Is.InRange(286000L, 315000L));
            Assert.AreEqual(0, lot.Price % 100);
            Assert.IsNotNull(a.Find(p.Id));
            Assert.AreEqual(fundsA - lot.Price, a.Team.Funds, "paid at once; a league lot's money goes to the league");
            Assert.AreEqual(2, p.ContractYearsLeft, "mature free agent: 2 years");
        }

        [Test]
        public void Auction_PlayerAutoBidTo350k_ExitsWhenNextBidAboveLimit_AiWins()
        {
            var (w, win, me, b) = Auction(1);
            var p = w.FreeAgents[0];
            var lot = win.LeagueLot(p, LotKind.Free);
            lot.StartPrice = 200000;
            win.OpenLot(lot);
            lot.Limits[b.Id] = 400000;
            win.SetAutoBid(p.Id, 350000);
            Assert.IsTrue(win.RunLot(lot));
            Assert.AreEqual(b.Id, lot.TopBidder);
            Assert.IsTrue(lot.Bidders.Contains(me.Id), "the player bid automatically");
            Assert.Greater(lot.Price + win.Step3(lot.Price), 350000, "the player left because the next bid passed X");
            Assert.LessOrEqual(lot.Price, 400000);
        }

        [Test]
        public void Auction_NobodyBids_UnsoldLotLockedThisWindow_BackInPoolNextYear()
        {
            var (w, win, a, b) = Auction(-1);
            var p = w.FreeAgents[0];
            var lot = win.LeagueLot(p, LotKind.Youth);
            win.OpenLot(lot);
            lot.Limits[a.Id] = 0; lot.Limits[b.Id] = 0;
            Assert.IsTrue(win.RunLot(lot));
            Assert.IsFalse(lot.Sold);
            Assert.AreEqual(-1, lot.TopBidder);
            Assert.Contains(p, w.FreeAgents);
            CollectionAssert.DoesNotContain(win.SignablePool(), p, "not in the re-sign list this window");
            var next = new TransferWindow(w, 1, -1);
            CollectionAssert.Contains(next.SignablePool(), p, "back in the pool next year");
        }

        [Test]
        public void Auction_ListedSold_MoneyToSellerContractKept_YouthLotThreeYears()
        {
            var (w, win, a, b) = Auction(-1);
            var seller = w.Clubs[3];
            var q = AddPlayer(w, seller, "mid", 23, 55f, contract: 2, salary: 77000);
            var lot = win.ListedLot(seller, q, false);
            win.OpenLot(lot);
            lot.Limits[a.Id] = 10_000_000; lot.Limits[b.Id] = 0;
            long sf = seller.Team.Funds;
            Assert.IsTrue(win.RunLot(lot));
            Assert.AreEqual(a.Id, lot.TopBidder);
            Assert.AreEqual(sf + lot.Price, seller.Team.Funds, "money to the seller");
            Assert.AreEqual(2, q.ContractYearsLeft); Assert.AreEqual(77000, q.Salary);
            Assert.IsNotNull(a.Find(q.Id)); Assert.IsNull(seller.Find(q.Id));
            var y = w.FreeAgents[0];
            var ylot = win.LeagueLot(y, LotKind.Youth);
            win.OpenLot(ylot);
            ylot.Limits[a.Id] = 10_000_000; ylot.Limits[b.Id] = 0;
            long total = w.Clubs.Sum(c => c.Team.Funds);
            win.RunLot(ylot);
            Assert.AreEqual(3, y.ContractYearsLeft, "youth lot: 3 years");
            Assert.AreEqual(total - ylot.Price, w.Clubs.Sum(c => c.Team.Funds), "league lot money leaves the clubs");
        }

        // ------------------------------------------------------------ playtest 2026-10-30: unsold listed players go to the league

        private static Club Seller(LeagueWorld w)
        {
            var seller = w.Clubs[3];
            foreach (var p in seller.Team.Roster.Where(p => seller.Team.LaneOf(p) == null).ToList()) { seller.Team.RemovePlayer(p); w.FreeAgents.Add(p); }
            return seller;
        }

        private static AuctionLot RunUnsold(TransferWindow win, Club a, Club b, AuctionLot lot)
        {
            win.OpenLot(lot);
            lot.Limits[a.Id] = 0; lot.Limits[b.Id] = 0;
            Assert.IsTrue(win.RunLot(lot));
            Assert.IsFalse(lot.Sold);
            return lot;
        }

        [Test]
        public void Auction_ListedUnsold_SoldToLeagueAtFireSalePrice_IntoFreePool_WithNews()
        {
            var (w, win, a, b) = Auction(-1);
            var seller = Seller(w);
            var q = AddPlayer(w, seller, "mid", 23, 55f);
            long funds = seller.Team.Funds, price = w.Economy.FireSalePrice(q);
            RunUnsold(win, a, b, win.ListedLot(seller, q, false));
            Assert.IsNull(seller.Find(q.Id));
            Assert.Contains(q, w.FreeAgents);
            Assert.AreEqual(funds + price, seller.Team.Funds, "value x 0.6");
            Assert.AreEqual(0, q.ContractYearsLeft);
            Assert.IsTrue(win.UnsoldSales.Any(u => u.Player == q && u.Price == price));
            var n = w.News.Last();
            Assert.AreEqual(NewsKind.UnsoldSale, n.Kind); Assert.AreEqual(q.Name, n.PlayerName); Assert.AreEqual(price, n.Amount);
        }

        [Test]
        public void Auction_ListedUnsold_SoldToLeague_NotSignableThisWindow()
        {
            // Arrange
            var (w, win, a, b) = Auction(-1);
            var seller = Seller(w);
            var q = AddPlayer(w, seller, "mid", 23, 55f);
            // Act
            RunUnsold(win, a, b, win.ListedLot(seller, q, false));
            // Assert: listing then re-signing him free would pocket value x 0.6 for nothing
            Assert.Contains(q, w.FreeAgents);
            Assert.IsFalse(win.SignablePool().Contains(q));
        }

        [Test]
        public void Auction_ListedUnsold_RosterWouldDropBelow5_Stays()
        {
            var (w, win, a, b) = Auction(-1);
            var seller = Seller(w);
            seller.Team.RemovePlayer(seller.Team.Starters().First(p => p.MainLane != "mid"));
            var q = AddPlayer(w, seller, "mid", 23, 55f);
            Assert.AreEqual(5, seller.Team.Roster.Count);
            RunUnsold(win, a, b, win.ListedLot(seller, q, false));
            Assert.IsNotNull(seller.Find(q.Id), "roster >= 5 guard");
            Assert.IsTrue(win.UnsoldKept.Any(u => u.Player == q));
            Assert.IsFalse(w.News.Any(x => x.Kind == NewsKind.UnsoldSale));
        }

        [Test]
        public void Auction_ListedUnsold_LastMainLanePlayer_Stays()
        {
            var (w, win, a, b) = Auction(-1);
            var seller = Seller(w);
            AddPlayer(w, seller, "top", 23, 55f);
            var mid = seller.Team.Roster.Single(p => p.MainLane == "mid");
            RunUnsold(win, a, b, win.ListedLot(seller, mid, false));
            Assert.IsNotNull(seller.Find(mid.Id), "last-in-lane guard");
            Assert.IsTrue(win.UnsoldKept.Any(u => u.Player == mid));
        }

        // ------------------------------------------------------------ AC10 / AC11 / AC12 / AC16 player window

        private static (LeagueWorld w, TransferWindow win, Club me) PlayerWindow(long seed = Seed)
        {
            var w = W(seed);
            var me = w.Clubs[0];
            me.Team.Funds = 5_000_000;
            return (w, new TransferWindow(w, 0, me.Id), me);
        }

        [Test]
        public void Scout_SignsYouthForLane_CannotBid_NotAllowedAtEight()
        {
            var (w, win, me) = PlayerWindow();
            win.Continue();
            TransferTestKit.AdvanceTo(win, w, TransferWait.ThreeChoice);
            Assert.AreEqual(TransferWait.ThreeChoice, win.Waiting);
            long funds = me.Team.Funds;
            Assert.AreEqual(TransferError.None, win.ChooseScout("jungle"));
            var s = win.Scouted[me.Id];
            Assert.AreEqual("jungle", s.MainLane); Assert.AreEqual(3, s.ContractYearsLeft);
            Assert.That(s.Age, Is.InRange(17, 20));
            Assert.AreEqual(funds - 50000, me.Team.Funds);
            win.Continue();
            Assert.IsFalse(win.Lots.Any(l => l.Bidders.Contains(me.Id) || l.InClubs.Contains(me.Id)), "scouting clubs do not bid");

            var (w2, win2, me2) = PlayerWindow();
            win2.Continue();
            TransferTestKit.AdvanceTo(win2, w2, TransferWait.ThreeChoice);
            while (me2.Team.Roster.Count < 8) AddPlayer(w2, me2, "support", 22, 40f);
            Assert.IsFalse(win2.PlayerCanScout);
            Assert.AreEqual(TransferError.RosterFull, win2.ChooseScout("mid"));
        }

        [Test]
        public void Listing_PlayerSignedThisYear_CannotBeListed()
        {
            var (w, win, me) = PlayerWindow();
            while (me.Team.Roster.Count < 8) AddPlayer(w, me, "support", 22, 40f);
            var q = me.Team.Roster.First(p => me.Team.LaneOf(p) == null);
            Assert.IsTrue(win.CanList(me, q, 0));
            w.Market(q).JoinedYear = win.Year;
            Assert.IsFalse(win.CanList(me, q, 0));
        }

        [Test]
        public void FinishWindow_FourPlayersOrEmptyLane_Refused_ThenAllowedAfterSigning()
        {
            var (w, win, me) = PlayerWindow();
            win.Continue();
            TransferTestKit.AdvanceTo(win, w, TransferWait.Resign);
            Assert.AreEqual(TransferWait.Resign, win.Waiting);
            string lane = "adc";
            foreach (var p in me.Team.Roster.Where(p => p.MainLane == lane).ToList()) me.Team.RemovePlayer(p);
            while (me.Team.Roster.Count < 6) AddPlayer(w, me, "top", 22, 40f);
            CollectionAssert.Contains(w.MissingLanes(me), lane, "the re-sign list marks the empty lane");
            Assert.AreEqual(TransferError.IncompleteRoster, win.FinishResign());
            while (me.Team.Roster.Count > 4) me.Team.RemovePlayer(me.Team.Roster.Last());
            Assert.AreEqual(TransferError.IncompleteRoster, win.FinishResign());
            while (win.CanFinish() != TransferError.None)
            {
                var miss = w.MissingLanes(me);
                var pick = win.SignablePool().First(p => miss.Count == 0 || miss.Contains(p.MainLane));
                Assert.AreEqual(TransferError.None, win.SignFree(pick.Id));
            }
            Assert.AreEqual(TransferError.None, win.FinishResign());
            Assert.AreEqual(TransferStep.Done, win.Step);
        }

        [Test]
        public void RefusedRenegotiation_MoodMinus15_WantsTransfer_AutoListedWhateverTheChoice()
        {
            var (w, win, me) = PlayerWindow();
            while (me.Team.Roster.Count < 7) AddPlayer(w, me, "support", 22, 40f);
            var q = AddPlayer(w, me, "mid", 22, 70f, contract: 3, salary: 1000);
            float mood = q.Mood;
            win.Continue();
            Assert.AreEqual(TransferWait.Renegotiation, win.Waiting);
            var req = win.Renegotiations.Single(r => r.Player == q);
            Assert.GreaterOrEqual(req.Market, 1.2 * 1000);
            Assert.AreEqual(TransferError.None, win.AnswerRenegotiation(q.Id, false));
            Assert.AreEqual(Math.Max(0f, mood - 15f), q.Mood, 1e-4f);
            var m = w.Market(q);
            Assert.IsTrue(m.WantsTransfer);
            Assert.AreEqual(win.Year + 1, m.RenegBlockedYear, "no request next year");
            TransferTestKit.AdvanceTo(win, w, TransferWait.ThreeChoice);
            Assert.AreEqual(TransferError.None, win.ChooseSkip());
            win.Continue();
            var lot = win.Lots.SingleOrDefault(l => l.Player == q);
            Assert.IsNotNull(lot, "auto-listed at step 5 although the club skipped");
            Assert.IsTrue(lot.Forced);
            Assert.AreEqual(me.Id, lot.SellerId);
        }

        // ------------------------------------------------------------ AC13 AI three-choice

        [Test]
        public void AiThreeChoice_FollowsRules1To6()
        {
            var w = W();
            var win = new TransferWindow(w, 0, -1);
            var c = w.Clubs[4];
            while (c.Team.Roster.Count < 7) AddPlayer(w, c, "support", 22, 40f, salary: 30000);
            long bill = TransferWindow.WageBill(c);
            double W12 = bill / 12.0;
            // 1 debt over the line -> sell the highest-paid listable starter
            c.Debt = (long)(0.3 * bill);
            Assert.AreEqual(WindowChoice.Sell, win.AiChoice(c, 1000, out var sell, out _));
            var listable = c.Team.Starters().Where(p => win.CanList(c, p, 0)).OrderByDescending(p => p.Salary).First();
            Assert.AreEqual(listable, sell.Single());
            c.Debt = 0;
            // 2 funds below floor -> sell bench (lowest value / salary), 2 when roster > 6
            c.Team.Funds = (long)w.Ai.Floor(c) - 1;
            Assert.AreEqual(WindowChoice.Sell, win.AiChoice(c, 1000, out sell, out _));
            Assert.AreEqual(2, sell.Count);
            Assert.IsTrue(sell.All(p => c.Team.LaneOf(p) == null));
            // 4 affordable lot and roster < 8 -> auction
            c.Team.Funds = (long)w.Ai.Floor(c) + 10_000_000;
            Assert.AreEqual(WindowChoice.Auction, win.AiChoice(c, 1000, out _, out _));
            // 5 not affordable, roster <= 7 -> scout the weakest lane
            Assert.AreEqual(WindowChoice.Scout, win.AiChoice(c, c.Team.Funds, out _, out var lane));
            Assert.AreEqual(w.WeakestLane(c), lane);
            // 6 roster 8, not affordable -> skip
            AddPlayer(w, c, "support", 22, 40f, salary: 30000);
            Assert.AreEqual(WindowChoice.Skip, win.AiChoice(c, c.Team.Funds, out _, out _));
            // 3 roster < 6 -> auction when affordable, else scout the missing lane
            while (c.Team.Roster.Count > 5) c.Team.RemovePlayer(c.Team.Roster.Last(p => c.Team.LaneOf(p) == null || c.Team.Roster.Count(q => q.MainLane == p.MainLane) > 1));
            c.Team.Funds = (long)w.Ai.Floor(c) + 10_000_000;
            Assert.AreEqual(WindowChoice.Auction, win.AiChoice(c, 1000, out _, out _));
            Assert.AreEqual(WindowChoice.Scout, win.AiChoice(c, c.Team.Funds, out _, out lane));
            var missing = w.MissingLanes(c);
            Assert.AreEqual(missing.Count > 0 ? missing[0] : w.WeakestLane(c), lane);
            Assert.Greater(W12, 0);
        }

        // ------------------------------------------------------------ AC17 poaching

        [Test]
        public void AiPoach_TargetsTheChampionsBestStarter_AtMostTwoOffersPerClub()
        {
            var w = W();
            var win = new TransferWindow(w, 0, -1);
            var buyer = w.Clubs[0];
            buyer.Team.Funds = 50_000_000;
            foreach (var c in w.Clubs) c.LastYearFinalsPlace = 0;
            var champ = w.Clubs[5];
            champ.LastYearFinalsPlace = 1;
            StarterWithMate(w, champ, out _);
            string wl = w.WeakestLane(buyer);
            var expected = champ.Team.Starters().Where(p => win.CanBeTarget(champ, p)).OrderByDescending(p => win.PoachScore(buyer, p, wl)).First();
            var t = win.AiPoachTarget(buyer, out var seller, out long fee);
            Assert.AreEqual(champ, seller);
            Assert.AreEqual(expected, t);
            Assert.LessOrEqual(fee, buyer.Team.Funds - w.Ai.Floor(buyer));
            Assert.That(fee, Is.InRange(w.Economy.Value(t) * 1.0 - 1, w.Economy.Value(t) * 1.3 + 1));
            // whole all-AI window: <= 2 offers per buyer, one offer per player
            var w2 = W(Seed + 1);
            foreach (var c in w2.Clubs) c.Team.Funds += 3_000_000;
            var win2 = new TransferWindow(w2, 0, -1);
            win2.Continue();
            Assert.AreEqual(TransferStep.Done, win2.Step);
            Assert.Greater(win2.Offers.Count, 0);
            foreach (var g in win2.Offers.GroupBy(o => o.BuyerId)) Assert.LessOrEqual(g.Count(), 2);
            Assert.AreEqual(win2.Offers.Count, win2.Offers.Select(o => o.Player.Id).Distinct().Count());
            Assert.IsTrue(win2.Offers.All(o => o.Round == 1 || o.Round == 2));
        }

        // ------------------------------------------------------------ whole window, integration, determinism (AC14)

        [Test]
        public void AllAiWindow_EveryClubEndsWithFiveToEightAndAllLanes_PoolCapped()
        {
            var w = W();
            var win = new TransferWindow(w, 0, -1);
            win.Continue();
            Assert.AreEqual(TransferStep.Done, win.Step);
            foreach (var c in w.Clubs)
            {
                Assert.That(c.Team.Roster.Count, Is.InRange(5, 8), c.Name);
                Assert.IsEmpty(w.MissingLanes(c), c.Name);
                Assert.AreEqual(5, c.Team.Starters().Count);
            }
            Assert.LessOrEqual(w.FreeAgents.Count(p => w.Market(p).LockedYear != 0), 35, "talent AC8: cap at the end, unsold lots excluded");
            Assert.AreEqual(8, win.NewPlayers.Count(p => w.Market(p).YouthYear == 0), "8 youth a year");
            Assert.That(win.Lots.Count(l => l.SellerId < 0), Is.EqualTo(3), "2 youth + 1 free agent");
        }

        private static string RunSession(int seed, out GameSession s)
        {
            s = MakeSession(seed);
            for (int i = 0; i < 12; i++)
            {
                s.ApplyScriptedPlan();
                s.Resolve();
                if (s.EventPhase != EventPhase.None) s.Season.AutoPlay();
                if (i < 11) s.BeginNext();
            }
            Assert.IsTrue(s.InTransferWindow, "the finals open the window for the player");
            var win = s.Window;
            var me = s.PlayerClub;
            // the player's decisions (fixed script): agree, renew 2 years, one poach offer, auction with auto-bid, then finish
            for (int guard = 0; guard < 400 && s.InTransferWindow; guard++)
            {
                switch (win.Waiting)
                {
                    case TransferWait.Renegotiation:
                        foreach (var r in win.Renegotiations.Where(r => r.ClubId == me.Id && r.Agreed == null).ToList()) win.AnswerRenegotiation(r.Player.Id, true);
                        break;
                    case TransferWait.Renewal:
                        foreach (var r in win.Renewals.Where(r => r.ClubId == me.Id && r.Years == null).ToList()) win.Renew(r.Player.Id, 2);
                        break;
                    case TransferWait.PoachTargets:
                        var t = win.PlayerPoachTargets().FirstOrDefault();
                        if (t != null) win.QueuePoach(t.Player.Id);
                        break;
                    case TransferWait.PoachResponse: win.RespondPoach(true); break;
                    case TransferWait.ThreeChoice: win.ChooseAuction(); break;
                    case TransferWait.AuctionBid: win.BidAuto(win.CurrentLot.StartPrice * 2); break;
                    case TransferWait.AuctionReview: win.ConfirmAuction(); continue;
                    case TransferWait.Resign:
                        while (win.CanFinish() != TransferError.None)
                        {
                            var miss = s.World.MissingLanes(me);
                            var pick = win.SignablePool().First(p => miss.Count == 0 || miss.Contains(p.MainLane));
                            win.SignFree(pick.Id);
                        }
                        win.FinishResign();
                        continue;
                }
                if (s.InTransferWindow) win.Continue();
            }
            return s.World.StateHash();
        }

        [Test]
        public void Integration_WarmUpYear1WindowYear2_PlayerDecidesThroughTheApi_SameSeedSameHash()
        {
            string h1 = RunSession(Seed, out var s);
            Assert.IsFalse(s.InTransferWindow);
            Assert.AreEqual(2, s.Season.Year);
            var last = s.World.LastWindow;
            Assert.IsNotNull(last);
            Assert.AreEqual(TransferStep.Done, last.Step);
            Assert.IsTrue(last.Choices.ContainsKey(s.PlayerClub.Id));
            Assert.That(s.Team.Roster.Count, Is.InRange(5, 8));
            Assert.IsEmpty(s.World.MissingLanes(s.PlayerClub));
            Assert.AreEqual(s.World.WageIndex, last.WageIndexAfter);
            Assert.That(s.World.WageIndex, Is.InRange(0.8, 1.3));
            s.BeginNext();
            Assert.AreEqual(0, s.Season.SegmentIndexInYear, "year 2 starts at segment 1");
            s.ApplyScriptedPlan();
            s.Resolve();
            string h2 = RunSession(Seed, out _);
            Assert.AreEqual(h1, h2, "AC14: same seed + same decisions -> same window");
            Assert.AreNotEqual(h1, RunSession(Seed + 3, out _));
        }

        [Test]
        public void Performance_TransferWindow_AllAi_Under5ms()
        {
            new TransferWindow(W(1), 0, -1).Continue(); // JIT
            var times = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var w = W(200 + i);
                var sw = Stopwatch.StartNew();
                new TransferWindow(w, 0, -1).Continue();
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            TestContext.WriteLine("window ms: " + string.Join(", ", times.Select(t => t.ToString("F2"))));
            Assert.LessOrEqual(times[2], 5.0);
        }
    }
}
