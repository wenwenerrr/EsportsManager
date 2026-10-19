using System;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Tests.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Segments
{
    /// <summary>Story 003 AC8: segment flow, ordering, determinism, hand-off to Team.SettleSegment.</summary>
    public class SegmentFlowTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void Resolve_SettlesTheTeam_FundsMoveByReportNet()
        {
            var fx = new Fixture(funds: 1000000);
            var s = fx.Run(new SegmentPlan().Stream(1), Ctx(true));
            Assert.AreEqual(1000000 + s.Report.Net, fx.Team.Funds);
            Assert.AreEqual(fx.Team.SalaryShare(12, 0), s.Report.SalaryShare);
            Assert.AreEqual(1, fx.Team.RecentResults.Count, "match results reach Team.SettleSegment");
            Assert.AreEqual(100f, s.Report.RecentWinRate, Tol);
        }

        [Test]
        public void Resolve_PrizeMoneyIsPassedToTheTeam()
        {
            var fx = new Fixture();
            var s = fx.Run(new SegmentPlan(), new SegmentContext { SegmentsPerYear = 12, PrizeMoney = 5000 });
            Assert.AreEqual(5000, s.PrizeMoney);
            Assert.AreEqual(5000, s.Report.Income);
        }

        [Test]
        public void Resolve_PopularityChangesByTeamSettlement_AfterLivestreamIncomeWasComputed()
        {
            var fx = new Fixture();
            float popBefore = fx.Starters[0].Popularity;
            int incomeAtOldPopularity = fx.Team.LivestreamIncome(fx.Starters[0], 2);
            var s = fx.Run(new SegmentPlan().Stream(1).Stream(1));
            Assert.AreNotEqual(popBefore, fx.Starters[0].Popularity, "settlement should have moved popularity");
            Assert.AreEqual(incomeAtOldPopularity, s.LivestreamIncome, "income must use the popularity from before settling");
        }

        [Test]
        public void GetState_ExposesPointsOffersAndContracts()
        {
            var fx = new Fixture(new StubRandomSource(0f));
            var closed = fx.Runner.GetState();
            Assert.IsFalse(closed.IsOpen);
            Assert.AreEqual(6, closed.ActionPoints);
            Assert.AreEqual(2, closed.MaxPerPlayer);
            fx.Runner.BeginSegment();
            Assert.IsTrue(fx.Runner.GetState().IsOpen);
            Assert.AreEqual(5, fx.Runner.GetState().PendingOffers.Count);
        }

        [Test]
        public void Resolve_ClosesTheSegment_SecondResolveThrows()
        {
            var fx = new Fixture();
            fx.Run(new SegmentPlan());
            Assert.Throws<InvalidOperationException>(() => fx.Runner.Resolve(new SegmentPlan(), Ctx()));
            Assert.DoesNotThrow(() => fx.Run(new SegmentPlan()), "a new segment can start");
        }

        [Test]
        public void RandomDrawOrder_BeginOnePerPlayer_ResolveTwoPerPlayer()
        {
            var rng = new ScriptedRandomSource(0.5f);
            var fx = new Fixture(rng);
            fx.Runner.BeginSegment();
            Assert.AreEqual(5, rng.Draws);
            fx.Runner.Resolve(new SegmentPlan(), Ctx());
            Assert.AreEqual(5 + 10, rng.Draws, "stress R then mood R per player; the team uses its own source");
        }

        [Test]
        public void RandomDrawOrder_SponsorDrawsBeforeStressAndMood()
        {
            var rng = new ScriptedRandomSource(0.5f);
            var fx = new Fixture(rng);
            fx.Run(new SegmentPlan().Sponsor().Sponsor());
            Assert.AreEqual(5 + 2 * 2 + 10, rng.Draws);
        }

        [Test]
        public void SameSeed_GivesIdenticalSegments()
        {
            SegmentSummary Play()
            {
                var fx = new Fixture(new SeededRandomSource(7), new SeededRandomSource(8), funds: 500000);
                fx.Runner.BeginSegment();
                var plan = new SegmentPlan().Train(1, 0).Stream(2).Sponsor().Rest(3);
                var offers = fx.Runner.GetState().PendingOffers;
                if (offers.Count > 0) plan.AcceptOffer(offers[0].Id);
                return fx.Runner.Resolve(plan, Ctx(true, false));
            }
            var a = Play(); var b = Play();
            Assert.AreEqual(a.Report.Funds, b.Report.Funds);
            Assert.AreEqual(a.Report.ClubPopularity, b.Report.ClubPopularity);
            Assert.AreEqual(a.SponsorAttempts[0].Success, b.SponsorAttempts[0].Success);
            for (int i = 0; i < a.Players.Count; i++)
            {
                Assert.AreEqual(a.Players[i].MoodDelta, b.Players[i].MoodDelta);
                Assert.AreEqual(a.Players[i].StressDelta, b.Players[i].StressDelta);
                Assert.AreEqual(a.Players[i].PopularityDelta, b.Players[i].PopularityDelta);
            }
        }

        [Test]
        public void Resolve_EmptyRoster_StillSettles()
        {
            var fx = new Fixture(lineup: false);
            foreach (var p in fx.Starters) fx.Team.RemovePlayer(p);
            var s = fx.Run(new SegmentPlan().Sponsor());
            Assert.AreEqual(0, s.Players.Count);
            Assert.AreEqual(1, s.PointsUsed);
        }

        [Test]
        public void Summary_ReportsStatDeltasPerPlayer()
        {
            var fx = new Fixture();
            var s = fx.Run(new SegmentPlan().Train(1, 0));
            var p1 = s.Players.Find(x => x.PlayerId == 1);
            Assert.AreEqual(6f, p1.StressDelta, Tol);
            Assert.AreEqual(4f, p1.MoodDelta, 0.2f); // play term; salary term is ~0: training lifted his expected salary a little
            Assert.AreEqual(8f, p1.ObservationDelta, Tol);
            Assert.AreEqual(fx.Starters[0].Popularity - 50f, p1.PopularityDelta, Tol);
            Assert.AreEqual(5, s.Players.Count);
        }

        [Test]
        public void Constructor_InvalidConfig_Throws()
        {
            var fx = new Fixture();
            var bad = MakeConfig();
            bad.actionPoints.perSegment = 0;
            Assert.Throws<InvalidOperationException>(() => new SegmentRunner(bad, fx.Players, fx.Team, fx.Rng));
            Assert.Throws<ArgumentNullException>(() => new SegmentRunner(null, fx.Players, fx.Team, fx.Rng));
            Assert.Throws<ArgumentNullException>(() => new SegmentRunner(fx.Cfg, null, fx.Team, fx.Rng));
            Assert.Throws<ArgumentNullException>(() => new SegmentRunner(fx.Cfg, fx.Players, null, fx.Rng));
            Assert.Throws<ArgumentNullException>(() => new SegmentRunner(fx.Cfg, fx.Players, fx.Team, null));
        }
    }
}
