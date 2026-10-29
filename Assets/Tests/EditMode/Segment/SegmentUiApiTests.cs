using System;
using System.Collections.Generic;
using EsportsManager.Growth;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Tests.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Segments
{
    /// <summary>Story 003 API for the Story 009 UI: state (serial, last summary, offer ids, voided offers), TryValidate, Preview.</summary>
    public class SegmentUiApiTests
    {
        private const float Tol = 1e-3f;

        private static SegmentPlan WithAction(long playerId, SegmentAction action)
        {
            var plan = new SegmentPlan();
            var list = (List<ActionAssignment>)typeof(SegmentPlan)
                .GetField("_assignments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(plan);
            list.Add(new ActionAssignment(playerId, action));
            return plan;
        }

        // Player 1 gets an offer worth 14000 (club 9800), nobody else.
        private static Fixture OfferFor1(out ScriptedRandomSource rng)
        {
            rng = new ScriptedRandomSource(0.5f, 0.0f, 0.5f);
            return new Fixture(rng);
        }

        // ---------------- state

        [Test]
        public void State_SerialAndLastSummary_TrackSegments()
        {
            var fx = new Fixture();
            Assert.AreEqual(0, fx.Runner.GetState().Serial);
            Assert.IsNull(fx.Runner.GetState().LastSummary);
            fx.Runner.BeginSegment();
            Assert.AreEqual(1, fx.Runner.GetState().Serial);
            var s1 = fx.Runner.Resolve(new SegmentPlan(), Ctx());
            Assert.AreSame(s1, fx.Runner.GetState().LastSummary);
            var s2 = fx.Run(new SegmentPlan());
            Assert.AreEqual(2, fx.Runner.GetState().Serial);
            Assert.AreSame(s2, fx.Runner.GetState().LastSummary);
        }

        [Test]
        public void OfferIds_AreMonotonicAcrossSegments()
        {
            var fx = new Fixture(new StubRandomSource(0f)); // every player gets an offer
            fx.Runner.BeginSegment();
            var first = fx.Runner.GetState().PendingOffers;
            fx.Runner.Resolve(new SegmentPlan(), Ctx());
            fx.Runner.BeginSegment();
            var second = fx.Runner.GetState().PendingOffers;
            Assert.AreEqual(new[] { 0, 1, 2, 3, 4 }, Ids(first));
            Assert.AreEqual(new[] { 5, 6, 7, 8, 9 }, Ids(second));
            Assert.Throws<ArgumentException>(() => fx.Runner.Resolve(new SegmentPlan().AcceptOffer(0), Ctx()), "old ids are not valid");
        }

        private static int[] Ids(IReadOnlyList<BusinessOffer> offers)
        {
            var ids = new int[offers.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = offers[i].Id;
            return ids;
        }

        [Test]
        public void Offer_CarriesClubAmount()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            var offer = fx.Runner.GetState().PendingOffers[0];
            Assert.AreEqual(14000, offer.Amount);
            Assert.AreEqual(9800, offer.ClubAmount);
        }

        [Test]
        public void OfferOfDepartedPlayer_HiddenFromStateAndVoidedNotDeclined()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            fx.Team.RemovePlayer(fx.Starters[0]);
            Assert.AreEqual(0, fx.Runner.GetState().PendingOffers.Count);
            var s = fx.Runner.Resolve(new SegmentPlan(), Ctx());
            Assert.AreEqual(1, s.OffersVoided.Count);
            Assert.AreEqual(0, s.OffersDeclined.Count);
            Assert.AreEqual(0, s.OffersAccepted.Count);
        }

        // ---------------- TryValidate

        private static PlanError Error(Fixture fx, SegmentPlan plan, SegmentContext? ctx = null)
        {
            bool ok = fx.Runner.TryValidate(plan, ctx ?? Ctx(), out var e);
            Assert.AreEqual(e.IsNone, ok);
            return e;
        }

        [Test]
        public void TryValidate_ValidPlan_ReturnsTrueAndNone()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            Assert.AreEqual(PlanErrorCode.None, Error(fx, new SegmentPlan().Train(1, TrainingTarget.Early).Sponsor()).Code);
        }

        [Test]
        public void TryValidate_NullPlanAndClosedSegment()
        {
            var fx = new Fixture();
            Assert.AreEqual(PlanErrorCode.SegmentNotOpen, Error(fx, new SegmentPlan()).Code);
            fx.Runner.BeginSegment();
            Assert.AreEqual(PlanErrorCode.PlanNull, Error(fx, null).Code);
        }

        [Test]
        public void TryValidate_ContextCodes()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            var plan = new SegmentPlan();
            Assert.AreEqual(PlanErrorCode.BadSegmentsPerYear, Error(fx, plan, new SegmentContext()).Code);
            Assert.AreEqual(PlanErrorCode.BadSegmentIndex, Error(fx, plan, new SegmentContext { SegmentsPerYear = 12, SegmentIndexInYear = 12 }).Code);
            Assert.AreEqual(PlanErrorCode.BadSeasonRankScore, Error(fx, plan, new SegmentContext { SegmentsPerYear = 12, SeasonRankScore = -1f }).Code);
            Assert.AreEqual(PlanErrorCode.NegativePrizeMoney, Error(fx, plan, new SegmentContext { SegmentsPerYear = 12, PrizeMoney = -5 }).Code);
        }

        [Test]
        public void TryValidate_NegativeSalaryAndSponsorPoints()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            Assert.AreEqual(PlanErrorCode.NegativeSponsorPoints, Error(fx, new SegmentPlan { SponsorPoints = -1 }).Code);
            fx.Starters[2].Salary = -1;
            var e = Error(fx, new SegmentPlan());
            Assert.AreEqual(PlanErrorCode.NegativeSalary, e.Code);
            Assert.AreEqual(3L, e.PlayerId);
        }

        [Test]
        public void TryValidate_AssignmentCodes()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            var e = Error(fx, new SegmentPlan().Train(999, TrainingTarget.Early));
            Assert.AreEqual(PlanErrorCode.UnknownPlayer, e.Code); Assert.AreEqual(999L, e.PlayerId);
            e = Error(fx, WithAction(2, (SegmentAction)42));
            Assert.AreEqual(PlanErrorCode.UnknownAction, e.Code); Assert.AreEqual(2L, e.PlayerId);
            e = Error(fx, new SegmentPlan().Train(1, TrainingTarget.Early, 6)); // focus dimension outside the early phase
            Assert.AreEqual(PlanErrorCode.DimensionOutOfRange, e.Code);
            Assert.AreEqual(1L, e.PlayerId); Assert.AreEqual(6, e.DimensionIndex);
        }

        [Test]
        public void TryValidate_OfferCodes()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            var e = Error(fx, new SegmentPlan().AcceptOffer(7));
            Assert.AreEqual(PlanErrorCode.UnknownOffer, e.Code); Assert.AreEqual(7, e.OfferId);
            e = Error(fx, new SegmentPlan().AcceptOffer(0).AcceptOffer(0));
            Assert.AreEqual(PlanErrorCode.DuplicateOffer, e.Code); Assert.AreEqual(0, e.OfferId);
            fx.Team.RemovePlayer(fx.Starters[0]);
            e = Error(fx, new SegmentPlan().AcceptOffer(0));
            Assert.AreEqual(PlanErrorCode.OfferPlayerLeft, e.Code); Assert.AreEqual(0, e.OfferId); Assert.AreEqual(1L, e.PlayerId);
        }

        [Test]
        public void TryValidate_LimitCodes()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            var e = Error(fx, new SegmentPlan().Train(1, TrainingTarget.Early).Stream(1).Rest(1));
            Assert.AreEqual(PlanErrorCode.PlayerOverLimit, e.Code);
            Assert.AreEqual(1L, e.PlayerId); Assert.AreEqual(3, e.Used); Assert.AreEqual(2, e.Limit);
            e = Error(fx, new SegmentPlan().Train(1, TrainingTarget.Early).Train(1, TrainingTarget.Early).Train(2, TrainingTarget.Early).Train(2, TrainingTarget.Early).Train(3, TrainingTarget.Early).Train(3, TrainingTarget.Early).Sponsor());
            Assert.AreEqual(PlanErrorCode.OverTotalPoints, e.Code);
            Assert.AreEqual(7, e.Used); Assert.AreEqual(6, e.Limit);
        }

        [Test]
        public void TryValidate_ChangesNothing()
        {
            var rng = new ScriptedRandomSource(0.5f);
            var fx = new Fixture(rng);
            fx.Runner.BeginSegment();
            int draws = rng.Draws;
            fx.Runner.TryValidate(new SegmentPlan().Train(999, TrainingTarget.Early), Ctx(), out _);
            Assert.AreEqual(draws, rng.Draws);
            Assert.IsTrue(fx.Runner.GetState().IsOpen);
        }

        // ---------------- Preview

        [Test]
        public void Preview_NoRandomDrawsAndNoStateChange()
        {
            var rng = new ScriptedRandomSource(0.5f);
            var fx = new Fixture(rng, funds: 1000);
            fx.Runner.BeginSegment();
            int draws = rng.Draws;
            var radar = fx.Starters[0].GetRadar("top");
            float stress = fx.Starters[0].Stress, mood = fx.Starters[0].Mood, pop = fx.Starters[0].Popularity, obs = fx.Starters[0].Observation;
            fx.Runner.Preview(new SegmentPlan().Train(1, TrainingTarget.Early).Train(1, TrainingTarget.Early).Stream(2).Rest(3).Sponsor(), Ctx(true));
            Assert.AreEqual(draws, rng.Draws);
            CollectionAssert.AreEqual(radar, fx.Starters[0].GetRadar("top"));
            Assert.AreEqual(stress, fx.Starters[0].Stress);
            Assert.AreEqual(mood, fx.Starters[0].Mood);
            Assert.AreEqual(pop, fx.Starters[0].Popularity);
            Assert.AreEqual(obs, fx.Starters[0].Observation);
            Assert.AreEqual(1000, fx.Team.Funds);
            Assert.AreEqual(0, fx.Team.RecentResults.Count);
            Assert.AreEqual(0, fx.Runner.GetState().ActiveContracts.Count);
            Assert.IsTrue(fx.Runner.GetState().IsOpen);
        }

        [Test]
        public void Preview_TrainingGainsEqualResolve()
        {
            var fx = new Fixture();
            fx.Starters[0].SetDimension("top", 0, 70f);
            fx.Runner.BeginSegment();
            var plan = new SegmentPlan().Train(1, TrainingTarget.Early).Train(1, TrainingTarget.Early).Train(2, TrainingTarget.Early).Train(2, TrainingTarget.Early);
            var pv = fx.Runner.Preview(plan, Ctx());
            var before1 = fx.Starters[0].GetRadar("top");
            var before2 = fx.Starters[1].GetRadar("jungle");
            fx.Runner.Resolve(plan, Ctx());
            var after1 = fx.Starters[0].GetRadar("top");
            var after2 = fx.Starters[1].GetRadar("jungle");
            for (int d = 0; d < 6; d++)
            {
                Assert.AreEqual(after1[d] - before1[d], pv.Players[0].TrainingGains[d], Tol, "player 1 dim " + d);
                Assert.AreEqual(after2[d] - before2[d], pv.Players[1].TrainingGains[d], Tol, "player 2 dim " + d);
            }
        }

        [Test]
        public void Preview_StressMoodAtRandomFactorOne()
        {
            var fx = new Fixture(new StubRandomSource(1f)); // the runner's R would be 1.1 / 1.15; preview must ignore it
            fx.Runner.BeginSegment();
            var pv = fx.Runner.Preview(new SegmentPlan().Train(1, TrainingTarget.Early), Ctx());
            Assert.AreEqual(6f, pv.Players[0].StressDelta, Tol);
            Assert.AreEqual(4f, pv.Players[0].MoodDelta, Tol);
            Assert.AreEqual(0f, pv.Players[1].StressDelta, Tol);
        }

        [Test]
        public void Preview_PointsPerPlayerAndTotal()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            var pv = fx.Runner.Preview(new SegmentPlan().Train(1, TrainingTarget.Early).Stream(1).Rest(1).Rest(2), Ctx());
            Assert.AreEqual(PlanErrorCode.PlayerOverLimit, pv.Error.Code);
            Assert.AreEqual(4, pv.PointsUsed);
            Assert.AreEqual(2, pv.PointsRemaining);
            Assert.AreEqual(3, pv.Players[0].PointsUsed);
            Assert.AreEqual(-1, pv.Players[0].PointsRemaining);
            Assert.AreEqual(1, pv.Players[1].PointsUsed);
            Assert.AreEqual(1, pv.Players[1].PointsRemaining);
        }

        [Test]
        public void Preview_SponsorChanceBeforeNoiseAndOfferClubShare()
        {
            var fx = OfferFor1(out _);
            fx.Runner.BeginSegment();
            var pv = fx.Runner.Preview(new SegmentPlan().Sponsor().AcceptOffer(0).Stream(2), Ctx());
            Assert.AreEqual(PlanErrorCode.None, pv.Error.Code);
            Assert.AreEqual(0.32f, pv.SponsorChanceBeforeNoise, Tol); // WS(30, 50)/100 x 0.8
            Assert.AreEqual(1, pv.AcceptedOffers.Count);
            Assert.AreEqual(9800, pv.AcceptedOffers[0].ClubAmount);
            Assert.AreEqual(9800, pv.BusinessIncome);
            Assert.AreEqual(1, pv.Players[0].BusinessPoints);
            Assert.AreEqual(1500, pv.LivestreamIncome); // 500 + 20 x 50
        }

        [Test]
        public void Preview_InvalidDraft_SkipsUnknownEntries()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            var pv = fx.Runner.Preview(new SegmentPlan().Train(999, TrainingTarget.Early).Train(1, TrainingTarget.Early, 9).AcceptOffer(3), Ctx());
            Assert.AreEqual(PlanErrorCode.UnknownPlayer, pv.Error.Code);
            Assert.AreEqual(0, pv.Players[0].TrainPoints);
            Assert.AreEqual(0, pv.AcceptedOffers.Count);
            Assert.Throws<ArgumentNullException>(() => fx.Runner.Preview(null, Ctx()));
        }

        // ---------------- sponsorship caps (numbers.md 4.12 v3.2)

        [Test]
        public void SponsorAttempts_OverPerSegmentCap_FailsValidation()
        {
            var fx = new Fixture();
            fx.Runner.BeginSegment();
            var e = Error(fx, new SegmentPlan().Sponsor().Sponsor().Sponsor().Sponsor());
            Assert.AreEqual(PlanErrorCode.SponsorAttemptsOverLimit, e.Code);
            Assert.AreEqual(4, e.Used); Assert.AreEqual(3, e.Limit);
            Assert.Throws<InvalidOperationException>(() => fx.Runner.Resolve(new SegmentPlan().Sponsor().Sponsor().Sponsor().Sponsor(), Ctx()));
        }

        [Test]
        public void SponsorAttempts_FailedAttemptsCountTowardTheCap()
        {
            var fx = new Fixture(new StubRandomSource(1f)); // every attempt fails (outcome draw 1.0)
            var s = fx.Run(new SegmentPlan().Sponsor().Sponsor().Sponsor());
            Assert.AreEqual(3, s.SponsorAttempts.Count);
            Assert.AreEqual(0, s.NewContracts.Count);
        }

        [Test]
        public void SponsorAttempts_ContractsFull_FailsValidation()
        {
            var fx = new Fixture();
            for (int i = 0; i < 3; i++) fx.Runner.AddContract(new SponsorContract(1000, 4));
            fx.Runner.BeginSegment();
            var st = fx.Runner.GetState();
            Assert.AreEqual(0, st.FreeContractSlots);
            Assert.AreEqual(0, st.SponsorAttemptsRemaining);
            var e = Error(fx, new SegmentPlan().Sponsor());
            Assert.AreEqual(PlanErrorCode.ContractsFull, e.Code);
            Assert.AreEqual(3, e.Used); Assert.AreEqual(3, e.Limit);
            Assert.Throws<InvalidOperationException>(() => fx.Runner.Resolve(new SegmentPlan().Sponsor(), Ctx()));
            Assert.AreEqual(PlanErrorCode.None, Error(fx, new SegmentPlan().Train(1, TrainingTarget.Early)).Code, "other actions still allowed");
        }

        [Test]
        public void SponsorAttempts_FewerFreeSlotsThanCap_AllowedAttemptsAreFreeSlots()
        {
            var fx = new Fixture();
            fx.Runner.AddContract(new SponsorContract(1000, 4));
            fx.Runner.AddContract(new SponsorContract(1000, 4));
            fx.Runner.BeginSegment();
            Assert.AreEqual(1, fx.Runner.GetState().FreeContractSlots);
            Assert.AreEqual(1, fx.Runner.GetState().SponsorAttemptsRemaining);
            var e = Error(fx, new SegmentPlan().Sponsor().Sponsor());
            Assert.AreEqual(PlanErrorCode.SponsorAttemptsOverLimit, e.Code);
            Assert.AreEqual(2, e.Used); Assert.AreEqual(1, e.Limit);
            Assert.AreEqual(PlanErrorCode.None, Error(fx, new SegmentPlan().Sponsor()).Code);
        }

        [Test]
        public void SponsorAttempts_SuccessesFillSlots_NextSegmentHasNone()
        {
            var fx = new Fixture(new StubRandomSource(0f)); // noise -0.1, outcome 0 -> every attempt succeeds
            var s = fx.Run(new SegmentPlan().Sponsor().Sponsor().Sponsor());
            Assert.AreEqual(3, s.NewContracts.Count);
            Assert.AreEqual(0, fx.Runner.GetState().FreeContractSlots);
            Assert.AreEqual(0, fx.Runner.GetState().SponsorAttemptsRemaining);
        }

        [Test]
        public void AddContract_WhenFull_Throws()
        {
            var fx = new Fixture();
            for (int i = 0; i < 3; i++) fx.Runner.AddContract(new SponsorContract(1000, 4));
            Assert.Throws<InvalidOperationException>(() => fx.Runner.AddContract(new SponsorContract(1000, 4)));
        }

        [Test]
        public void AddContract_SameInstanceTwice_Throws()
        {
            var fx = new Fixture();
            var c = new SponsorContract(1000, 4);
            fx.Runner.AddContract(c);
            Assert.Throws<ArgumentException>(() => fx.Runner.AddContract(c));
            Assert.AreEqual(1, fx.Runner.GetState().ActiveContracts.Count);
        }

        [Test]
        public void Preview_ExposesSponsorAttemptsRemainingAndFreeSlots()
        {
            var fx = new Fixture();
            fx.Runner.AddContract(new SponsorContract(1000, 4));
            fx.Runner.BeginSegment();
            var pv = fx.Runner.Preview(new SegmentPlan().Sponsor(), Ctx());
            Assert.AreEqual(2, pv.FreeContractSlots);
            Assert.AreEqual(1, pv.SponsorAttemptsRemaining);
            var over = fx.Runner.Preview(new SegmentPlan().Sponsor().Sponsor().Sponsor(), Ctx());
            Assert.AreEqual(-1, over.SponsorAttemptsRemaining);
            Assert.AreEqual(PlanErrorCode.SponsorAttemptsOverLimit, over.Error.Code);
        }
    }
}
