using System;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Tests.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Segments
{
    /// <summary>Story 003 AC6 (business pop-ups) and AC7 (sponsorship).</summary>
    public class SegmentBusinessSponsorTests
    {
        private const float Tol = 1e-3f;

        private PlayerSegmentSummary Of(SegmentSummary s, long id) => s.Players.Find(x => x.PlayerId == id);

        // Begin draws per player: trigger, then (only when triggered) the amount factor.
        // Script: player 1 gets an offer (trigger 0.0 < 0.2, amount fraction 0.5 = R 1.0), everybody else none (0.5).
        private static Fixture OfferFor1(out ScriptedRandomSource rng)
        {
            rng = new ScriptedRandomSource(0.5f, 0.0f, 0.5f);
            return new Fixture(rng);
        }

        // ---------------- offers

        [Test]
        public void Begin_RollsOffersOncePerRosterPlayerWithTrigger()
        {
            var fx = OfferFor1(out var rng);
            fx.Runner.BeginSegment();
            var st = fx.Runner.GetState();
            Assert.IsTrue(st.IsOpen);
            Assert.AreEqual(1, st.PendingOffers.Count);
            Assert.AreEqual(1, st.PendingOffers[0].PlayerId);
            Assert.AreEqual(14000, st.PendingOffers[0].Amount); // (5000 + 100 x 50 + 80 x 50) x R(1)
            Assert.AreEqual(6, rng.Draws); // 5 trigger draws + 1 amount draw
            Assert.AreEqual(1, fx.Runner.GetState().PendingOffers.Count, "reading the state must not roll again");
        }

        [Test]
        public void Begin_LowTriggerDraw_OffersEveryoneWithRandomFactorAmount()
        {
            var fx = new Fixture(new StubRandomSource(0f)); // trigger 0, R(0.2) = 0.8
            fx.Runner.BeginSegment();
            var offers = fx.Runner.GetState().PendingOffers;
            Assert.AreEqual(5, offers.Count);
            Assert.AreEqual(11200, offers[0].Amount);
            for (int i = 0; i < offers.Count; i++) Assert.AreEqual(i, offers[i].Id);
        }

        [Test]
        public void Begin_HighTriggerDraw_NoOffers()
        {
            var fx = new Fixture(new StubRandomSource(1f));
            fx.Runner.BeginSegment();
            Assert.AreEqual(0, fx.Runner.GetState().PendingOffers.Count);
        }

        // ---------------- accept / decline

        [Test]
        public void Accept_ConsumesOnePointOnThatPlayer()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            // player 1 already has 2 points after accepting + 1 train; a third point on him is over the limit
            var over = new SegmentPlan().Train(1, 0).Train(1, 0).AcceptOffer(0);
            Assert.Throws<InvalidOperationException>(() => fx.Runner.Resolve(over, Ctx()));
            var ok = new SegmentPlan().Train(1, 0).AcceptOffer(0);
            var s = fx.Runner.Resolve(ok, Ctx());
            Assert.AreEqual(2, s.PointsUsed);
            Assert.AreEqual(1, Of(s, 1).BusinessPoints);
            Assert.AreEqual(1, Of(s, 1).TrainPoints);
        }

        [Test]
        public void Accept_WithoutFreePoint_Throws()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            var full = new SegmentPlan().Train(2, 0).Train(2, 0).Train(3, 0).Train(3, 0).Train(4, 0).Train(4, 0).AcceptOffer(0);
            var ex = Assert.Throws<InvalidOperationException>(() => fx.Runner.Resolve(full, Ctx()));
            StringAssert.Contains("7", ex.Message);
            // a plan that leaves one point free accepts fine
            var fits = new SegmentPlan().Train(2, 0).Train(2, 0).Train(3, 0).Train(3, 0).Train(4, 0).AcceptOffer(0);
            Assert.DoesNotThrow(() => fx.Runner.Resolve(fits, Ctx()));
        }

        [Test]
        public void Accept_AddsHeatStressAndClubShareIncome()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            var s = fx.Runner.Resolve(new SegmentPlan().AcceptOffer(0), Ctx());
            Assert.AreEqual(100f, Of(s, 1).BusinessHeat, Tol);
            Assert.AreEqual(4f, fx.Starters[0].Stress, Tol); // s_biz x 1
            Assert.AreEqual(9800, s.BusinessIncome); // 70% of 14000
            Assert.AreEqual(9800, s.Report.Income);
            Assert.AreEqual(1, s.OffersAccepted.Count);
            Assert.AreEqual(0, s.OffersDeclined.Count);
        }

        [Test]
        public void Accept_RaisesPopularityMoreThanDeclining()
        {
            // two identical offers, only one accepted
            var fx = new Fixture(new StubRandomSource(0f));
            fx.Runner.BeginSegment();
            var s = fx.Runner.Resolve(new SegmentPlan().AcceptOffer(0), Ctx());
            Assert.Greater(Of(s, 1).PopularityDelta, Of(s, 2).PopularityDelta);
        }

        [Test]
        public void Decline_ChangesNothing()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            var s = fx.Runner.Resolve(new SegmentPlan(), Ctx());
            Assert.AreEqual(1, s.OffersDeclined.Count);
            Assert.AreEqual(0, s.OffersAccepted.Count);
            Assert.AreEqual(0, s.BusinessIncome);
            Assert.AreEqual(0f, Of(s, 1).BusinessHeat);
            Assert.AreEqual(0f, fx.Starters[0].Stress);
            Assert.AreEqual(Of(s, 2).PopularityDelta, Of(s, 1).PopularityDelta, Tol, "a declined offer must not move popularity");
            Assert.AreEqual(Of(s, 2).MoodDelta, Of(s, 1).MoodDelta, Tol);
            Assert.AreEqual(0, s.PointsUsed);
        }

        [Test]
        public void Accept_UnknownOrDuplicateOfferId_ThrowsArgument()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            Assert.Throws<ArgumentException>(() => fx.Runner.Resolve(new SegmentPlan().AcceptOffer(7), Ctx()));
            Assert.Throws<ArgumentException>(() => fx.Runner.Resolve(new SegmentPlan().AcceptOffer(0).AcceptOffer(0), Ctx()));
        }

        [Test]
        public void Accept_OfferOfPlayerWhoLeft_ThrowsArgument()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            fx.Team.RemovePlayer(fx.Starters[0]);
            Assert.Throws<ArgumentException>(() => fx.Runner.Resolve(new SegmentPlan().AcceptOffer(0), Ctx()));
        }

        [Test]
        public void Offers_DoNotCarryOverToTheNextSegment()
        {
            var fx = OfferFor1(out _);
            fx.Run(new SegmentPlan());
            Assert.IsFalse(fx.Runner.GetState().IsOpen);
            Assert.AreEqual(0, fx.Runner.GetState().PendingOffers.Count);
        }

        // ---------------- sponsorship

        // Script: 5 trigger draws (0.5, no offers), then noise fraction 0.5 (= 0), then the outcome draw.
        private static Fixture Sponsoring(float outcome)
        {
            return new Fixture(new ScriptedRandomSource(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, outcome));
        }

        [Test]
        public void Sponsor_ConsumesOnePoint()
        {
            var fx = Sponsoring(0f);
            var s = fx.Run(new SegmentPlan().Sponsor());
            Assert.AreEqual(1, s.PointsUsed);
            Assert.AreEqual(1, s.SponsorAttempts.Count);
        }

        [Test]
        public void Sponsor_Success_AddsContractThatPaysThisSegment()
        {
            var fx = Sponsoring(0f); // outcome 0.0 < chance 0.32
            var s = fx.Run(new SegmentPlan().Sponsor());
            Assert.AreEqual(0.32f, s.SponsorAttempts[0].Chance, Tol); // WS(30, 50)/100 x 0.8 + noise 0
            Assert.IsTrue(s.SponsorAttempts[0].Success);
            Assert.AreEqual(1, s.NewContracts.Count);
            Assert.AreEqual(9000, s.NewContracts[0].AmountPerSegment); // c0 6000 + c1 100 x club popularity 30
            Assert.AreEqual(9000, s.SponsorshipIncome);
            Assert.AreEqual(9000, s.Report.Income);
            var st = fx.Runner.GetState();
            Assert.AreEqual(1, st.ActiveContracts.Count);
            Assert.AreEqual(3, st.ActiveContracts[0].SegmentsRemaining);
        }

        [Test]
        public void Sponsor_Failure_AddsNoContractAndNoIncome()
        {
            var fx = Sponsoring(0.99f);
            var s = fx.Run(new SegmentPlan().Sponsor());
            Assert.IsFalse(s.SponsorAttempts[0].Success);
            Assert.AreEqual(0, s.NewContracts.Count);
            Assert.AreEqual(0, s.SponsorshipIncome);
            Assert.AreEqual(0, fx.Runner.GetState().ActiveContracts.Count);
        }

        [Test]
        public void Sponsor_ContractPaysEverySegmentThenExpires()
        {
            var fx = Sponsoring(0f);
            var first = fx.Run(new SegmentPlan().Sponsor());
            Assert.AreEqual(9000, first.SponsorshipIncome);
            for (int i = 0; i < 3; i++)
            {
                var s = fx.Run(new SegmentPlan());
                Assert.AreEqual(9000, s.SponsorshipIncome, "segment " + (i + 2));
                Assert.AreEqual(9000, s.Report.Income);
            }
            Assert.AreEqual(0, fx.Runner.GetState().ActiveContracts.Count, "N = 4 segments, then removed");
            Assert.AreEqual(0, fx.Run(new SegmentPlan()).SponsorshipIncome);
        }

        [Test]
        public void Sponsor_ContractAmountIsFixedAtSigning()
        {
            var fx = Sponsoring(0f);
            fx.Run(new SegmentPlan().Sponsor());
            fx.Team.ClubPopularity = 90f;
            var s = fx.Run(new SegmentPlan());
            Assert.AreEqual(9000, s.SponsorshipIncome);
        }

        [Test]
        public void Sponsor_ContractsStack()
        {
            var fx = new Fixture(new StubRandomSource(0f)); // noise -0.1, outcome 0 -> success
            fx.Runner.AddContract(new SponsorContract(1000, 2));
            fx.Runner.BeginSegment();
            var s = fx.Runner.Resolve(new SegmentPlan().Sponsor(), Ctx());
            Assert.AreEqual(1000 + 9000, s.SponsorshipIncome);
            Assert.AreEqual(2, fx.Runner.GetState().ActiveContracts.Count);
        }

        [Test]
        public void SponsorContract_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SponsorContract(-1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SponsorContract(100, 0));
        }
    }
}
