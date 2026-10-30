using System.Linq;
using EsportsManager.Leagues;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Session;
using NUnit.Framework;
using static EsportsManager.Tests.Leagues.LeagueTestFactory;
using static EsportsManager.Tests.Transfer.TransferTestKit;

namespace EsportsManager.Tests.Economy
{
    /// <summary>
    /// Playtest changes 2026-10-30 (design/gdd/economy.md 媒体邀约 and 财务危机与解雇): fire-sale notices, media invitations
    /// (probability, accept / decline effects, club point, AI rule), the financial crisis (grace, chip-ins), the dismissal and the
    /// new offers from strength ranks 5-8.
    /// </summary>
    public class CrisisAndMediaTests
    {
        // ------------------------------------------------------------ helpers

        private static LeagueWorld PlayerWorld(LeagueConfig cfg = null, int clubId = 1)
        {
            var w = World(Seed, cfg, false);
            w.TakeOver(new JobOffer { ClubId = clubId, Tier = OfferTier.Mid }, w.Clubs[clubId].Name);
            return w;
        }

        private static void StripBench(LeagueWorld w, Club c)
        {
            foreach (var p in c.Team.Roster.Where(p => c.Team.LaneOf(p) == null).ToList()) { c.Team.RemovePlayer(p); w.FreeAgents.Add(p); }
        }

        private static void Bond(Club c)
        {
            var st = c.Team.Starters();
            for (int i = 0; i < st.Count; i++) for (int j = i + 1; j < st.Count; j++) c.Team.SetFamiliarity(st[i].Id, st[j].Id, 100f);
        }

        // ------------------------------------------------------------ 1 fire-sale notice

        [Test]
        public void FireSale_PlayerClub_NoticeListsWhoAndPrice_AiClubOnlyNews()
        {
            var w = PlayerWorld();
            var me = w.PlayerClub;
            StripBench(w, me);
            var x = AddPlayer(w, me, "top", 19, 45f);
            long price = w.Economy.FireSalePrice(x);
            me.Team.Funds = -10;
            w.FundsCheck(me, 3);
            Assert.AreEqual(1, w.Notices.Count);
            var n = w.Notices[0];
            Assert.AreEqual(NoticeKind.FireSale, n.Kind);
            Assert.AreEqual(x.Name, n.PlayerName);
            Assert.AreEqual(price, n.Amount);
            Assert.Contains(x, w.FreeAgents);

            var ai = w.Clubs[0];
            StripBench(w, ai);
            AddPlayer(w, ai, "top", 19, 45f);
            ai.Team.Funds = -10;
            w.FundsCheck(ai, 3);
            Assert.AreEqual(1, w.Notices.Count, "AI fire sales are news only");
            Assert.IsTrue(w.News.Any(k => k.Kind == NewsKind.FireSale && k.ClubId == ai.Id));
        }

        [Test]
        public void Session_NoticesExposed_AcknowledgeClearsThem()
        {
            var s = MakeSession();
            s.World.Notices.Add(new ClubNotice { Kind = NoticeKind.FireSale, PlayerName = "A", Amount = 1 });
            Assert.AreEqual(1, s.Notices.Count);
            s.AcknowledgeNotices();
            Assert.AreEqual(0, s.Notices.Count);
        }

        // ------------------------------------------------------------ 4 media invitations

        [Test]
        public void MediaChance_ClampedLinearInPopularity_IncomeFromLivestreamBase()
        {
            var w = World(Seed, null, false);
            Assert.AreEqual(0.1f, w.MediaChance(0f), 1e-6f);
            Assert.AreEqual(0.35f, w.MediaChance(50f), 1e-6f);
            Assert.AreEqual(0.6f, w.MediaChance(100f), 1e-6f);
            Assert.AreEqual(0.6f, w.MediaChance(150f), 1e-6f, "clamped at chance_max");
            Assert.AreEqual(1.5 * w.TeamConfig.livestream.baseIncome + 100 * 40, w.MediaIncome(40f), 0.5);
        }

        [Test]
        public void MediaRoll_AtMostTwoPerClub_KeyedSameSeedSameInvitations()
        {
            var a = World(Seed, null, false); var b = World(Seed, null, false);
            int total = 0;
            for (int seg = 0; seg < 6; seg++)
            {
                a.BeginSegment(); b.BeginSegment();
                foreach (var c in a.Clubs)
                {
                    var ia = c.Runner.MediaInvitations; var ib = b.Clubs[c.Id].Runner.MediaInvitations;
                    Assert.LessOrEqual(ia.Count, 2);
                    CollectionAssert.AreEqual(ia.Select(m => m.Income).ToList(), ib.Select(m => m.Income).ToList());
                    total += ia.Count;
                }
                a.ResolveSegment(null); b.ResolveSegment(null);
                if (a.Season.Phase != EventPhase.None) { a.Season.AutoPlay(); b.Season.AutoPlay(); }
            }
            Assert.Greater(total, 0, "some invitations in 6 segments x 8 clubs");
        }

        private static GameSession MediaSession(bool accept, out long income)
        {
            var s = MakeSession();
            var inv = s.Runner.MediaInvitations;
            inv.Clear();
            income = s.World.MediaIncome(s.Team.ClubPopularity);
            inv.Add(new MediaInvitation { Id = 1, Income = income });
            inv.Add(new MediaInvitation { Id = 2, Income = income });
            foreach (var o in s.UnansweredOffers()) s.DeclineOffer(o.Id);
            if (accept) { Assert.IsTrue(s.TryAcceptMedia(1, out _)); Assert.IsTrue(s.TryAcceptMedia(2, out _)); }
            else { s.DeclineMedia(1); s.DeclineMedia(2); }
            return s;
        }

        [Test]
        public void MediaAccept_IncomePopularityPlus2AndStarterStressPlus2_DeclineMinus1_5()
        {
            var acc = MediaSession(true, out long income);
            var dec = MediaSession(false, out _);
            Assert.AreEqual(2, acc.Preview().PointsUsed, "one club point each");
            var starters = acc.Team.Starters().Select(p => p.Id).ToList();
            var ra = acc.Resolve(); var rd = dec.Resolve();
            Assert.AreEqual(2 * income, ra.Summary.MediaIncome);
            Assert.AreEqual(2, ra.Summary.MediaAccepted); Assert.AreEqual(2, rd.Summary.MediaDeclined);
            Assert.AreEqual(rd.FundsAfter + 2 * income, ra.FundsAfter, "income booked into funds");
            Assert.AreEqual(dec.Team.ClubPopularity + 2 * 2f + 2 * 1.5f, acc.Team.ClubPopularity, 1e-3f);
            foreach (long id in starters)
                Assert.AreEqual(dec.Team.Roster.First(p => p.Id == id).Stress + 4f, acc.Team.Roster.First(p => p.Id == id).Stress, 1e-3f);
        }

        [Test]
        public void MediaAccept_UsesClubPointNotPlayers_RefusedWhenNoPointLeft()
        {
            var s = MakeSession();
            s.Runner.MediaInvitations.Clear();
            s.Runner.MediaInvitations.Add(new MediaInvitation { Id = 1, Income = 1000 });
            var st = s.Team.Starters();
            int total = s.State.ActionPoints, max = s.State.MaxPerPlayer;
            int used = 0;
            foreach (var p in st) for (int i = 0; i < max && used < total - 1; i++) { Assert.IsTrue(s.TryAddRest(p.Id, out _)); used++; }
            Assert.IsTrue(s.TryAcceptMedia(1, out var e1), e1.Code.ToString());
            s.Plan.UnacceptMedia(1);
            Assert.IsTrue(s.TryAddSponsor(out _) || s.TryAddRest(s.Team.Roster.Last().Id, out _));
            Assert.IsFalse(s.TryAcceptMedia(1, out var e2));
            Assert.AreEqual(PlanErrorCode.OverTotalPoints, e2.Code);
            Assert.IsFalse(s.TryAcceptMedia(99, out var e3) && e3.IsNone, "unknown invitation");
        }

        [Test]
        public void MediaAi_AcceptsWhenBelowFloorOrStartersCalm_ElseDeclines()
        {
            var w = World(Seed, null, false);
            var c = w.Clubs[0];
            c.Team.Funds = 100_000_000;
            foreach (var p in c.Team.Starters()) p.Stress = 70f;
            Assert.IsFalse(w.Ai.AcceptsMedia(c));
            foreach (var p in c.Team.Starters()) p.Stress = 40f;
            Assert.IsTrue(w.Ai.AcceptsMedia(c), "mean starter stress < 55");
            foreach (var p in c.Team.Starters()) p.Stress = 70f;
            c.Team.Funds = 0;
            Assert.IsTrue(w.Ai.AcceptsMedia(c), "funds < floor");
            w.BeginSegment();
            c.Runner.MediaInvitations.Clear();
            c.Runner.MediaInvitations.Add(new MediaInvitation { Id = 1, Income = 1 });
            var plan = w.Ai.Plan(c, 0);
            CollectionAssert.AreEqual(new[] { 1 }, plan.AcceptedMediaIds.ToArray());
        }

        // ------------------------------------------------------------ 5 crisis, chip-ins, dismissal

        [Test]
        public void Crisis_StartsAfterSelfRescue_GraceTwoChecks_EndsWhenFundsBackToZero()
        {
            var w = PlayerWorld();
            var me = w.PlayerClub;
            StripBench(w, me);
            me.Team.Funds = -1000;
            w.FundsCheck(me, 1);
            Assert.IsTrue(me.InCrisis);
            Assert.AreEqual(2, me.CrisisSegmentsLeft);
            Assert.AreEqual(NoticeKind.CrisisStart, w.Notices.Last().Kind);
            Assert.AreEqual(1000, w.Notices.Last().Amount);
            w.FundsCheck(me, 2);
            Assert.AreEqual(1, me.CrisisSegmentsLeft);
            me.Team.Funds = 0;
            w.FundsCheck(me, 3);
            Assert.IsFalse(me.InCrisis);
            Assert.AreEqual(NoticeKind.CrisisOver, w.Notices.Last().Kind);
            Assert.IsTrue(me.IsPlayer);
            Assert.AreEqual(0, w.Dismissals);
        }

        [Test]
        public void ChipIn_CappedAtDeficitAndHisEarnings_NeedsChemistry60AndMood70()
        {
            var cfg = League(); cfg.crisis.chipinChance = 1f;
            var w = PlayerWorld(cfg);
            var me = w.PlayerClub;
            StripBench(w, me);
            Bond(me);
            Assert.GreaterOrEqual(me.Team.Chemistry(), 60f, "precondition");
            var st = me.Team.Roster.ToList();
            foreach (var p in st) { p.Mood = 90f; p.PersonalBusinessEarnings = 0; }
            st[0].PersonalBusinessEarnings = 50_000;
            st[1].PersonalBusinessEarnings = 5_000; st[1].Mood = 69f; // mood too low
            me.Team.Funds = -30_000;
            w.FundsCheck(me, 1); // crisis starts, no chip-in yet
            Assert.AreEqual(-30_000, me.Team.Funds);
            w.FundsCheck(me, 2);
            Assert.AreEqual(0, me.Team.Funds, "min(deficit, earnings)");
            Assert.AreEqual(20_000, st[0].PersonalBusinessEarnings);
            Assert.AreEqual(5_000, st[1].PersonalBusinessEarnings, "mood < 70 does not chip in");
            var chip = w.Notices.Single(n => n.Kind == NoticeKind.ChipIn);
            Assert.AreEqual(st[0].Name, chip.PlayerName); Assert.AreEqual(30_000, chip.Amount);
            Assert.IsFalse(me.InCrisis, "funds back at 0 ends the crisis");

            // low chemistry: nobody chips in
            var w2 = PlayerWorld(cfg);
            var me2 = w2.PlayerClub;
            StripBench(w2, me2);
            var s2 = me2.Team.Starters();
            for (int i = 0; i < s2.Count; i++) for (int j = i + 1; j < s2.Count; j++) me2.Team.SetFamiliarity(s2[i].Id, s2[j].Id, 0f);
            Assert.Less(me2.Team.Chemistry(), 60f, "precondition");
            foreach (var p in me2.Team.Roster) { p.Mood = 90f; p.PersonalBusinessEarnings = 50_000; }
            me2.Team.Funds = -30_000;
            w2.FundsCheck(me2, 1); w2.FundsCheck(me2, 2);
            Assert.AreEqual(-30_000, me2.Team.Funds);
        }

        [Test]
        public void ChipIn_Chance30Percent_KeyedPerPlayerAndSegment()
        {
            var w = PlayerWorld();
            var me = w.PlayerClub;
            Bond(me);
            int hits = 0, n = 0;
            foreach (var p in me.Team.Roster) { p.Mood = 90f; }
            for (int seg = 0; seg < 60; seg++)
                foreach (var p in me.Team.Roster)
                {
                    n++;
                    if (w.Keys.U01("chipin", w.Year, seg, me.Id, p.Id) < 0.3) hits++;
                }
            Assert.That(hits / (double)n, Is.InRange(0.2, 0.4));
            me.Team.Funds = -1;
            foreach (var p in me.Team.Roster) p.PersonalBusinessEarnings = 1;
            long got = w.ChipIns(me, 7);
            bool expect = me.Team.Roster.Any(p => w.Keys.U01("chipin", w.Year, 7, me.Id, p.Id) < 0.3);
            Assert.AreEqual(expect ? 1 : 0, got);
        }

        [Test]
        public void BusinessDeal_PlayerKeeps30Percent_ClubGets70()
        {
            var s = GameSession.FromJsonWithOffer(Data("player_config.json"), Data("team_config.json"), Data("segment_config.json"),
                Data("match_config.json"), null, Data("league_config.json"), Seed);
            var o = s.UnansweredOffers()[0];
            var p = s.Team.Roster.First(x => x.Id == o.PlayerId);
            long before = p.PersonalBusinessEarnings;
            Assert.IsTrue(s.TryAcceptOffer(o.Id, out _));
            s.Resolve();
            Assert.AreEqual(before + o.Amount - o.ClubAmount, p.PersonalBusinessEarnings);
            Assert.AreEqual(o.Amount * 0.3, o.Amount - o.ClubAmount, 1.0);
        }

        [Test]
        public void Fired_AfterGrace_OldClubAiWithLoan_ThreeOffersFromRanks5To8()
        {
            var w = PlayerWorld();
            var me = w.PlayerClub;
            StripBench(w, me);
            foreach (var p in me.Team.Roster) p.PersonalBusinessEarnings = 0;
            me.Team.Funds = -500_000;
            w.FundsCheck(me, 1); w.FundsCheck(me, 2);
            Assert.IsFalse(w.DismissalPending);
            w.FundsCheck(me, 3);
            Assert.IsTrue(w.DismissalPending);
            Assert.AreEqual(1, w.Dismissals);
            Assert.IsFalse(me.IsPlayer); Assert.IsTrue(me.Bankrupt);
            Assert.AreEqual(-1, w.PlayerClubId);
            Assert.AreEqual(500_000, me.Debt, "deficit becomes an AI loan");
            Assert.AreEqual(0, me.Team.Funds);
            Assert.AreEqual(3, w.Offers.Count);
            Assert.AreEqual(3, w.Offers.Select(o => o.ClubId).Distinct().Count());
            var rank = w.StrengthRanking();
            foreach (var o in w.Offers)
            {
                Assert.AreNotEqual(me.Id, o.ClubId);
                Assert.That(rank.FindIndex(c => c.Id == o.ClubId) + 1, Is.InRange(5, 8));
                Assert.AreEqual(OfferTier.Weak, o.Tier, "weak-offer capital top-up");
            }
            var target = w.Clubs[w.Offers[0].ClubId];
            var c = w.TakeOver(w.Offers[0], target.Name);
            Assert.IsTrue(c.IsPlayer); Assert.IsFalse(w.DismissalPending);
            Assert.GreaterOrEqual(c.Team.Funds, (long)System.Math.Ceiling(w.Config.offers.weakInjectSegments * c.WagesPerSegment(w.SegmentsPerYear)));
        }

        [Test]
        public void Session_FiredMidSeason_PicksOffer_ContinuesSameSeason()
        {
            var s = MakeSession();
            var w = s.World;
            var old = s.PlayerClub;
            StripBench(w, old);
            int year = s.Season.Year;
            for (int guard = 0; guard < 6 && !s.Dismissed; guard++)
            {
                foreach (var p in old.Team.Roster) p.PersonalBusinessEarnings = 0;
                old.Team.Funds = -50_000_000;
                foreach (var o in s.UnansweredOffers()) s.DeclineOffer(o.Id);
                s.Resolve();
                if (s.EventPhase != EventPhase.None) s.Season.AutoPlay();
                if (!s.Dismissed) { s.TryAcknowledgeEvent(out _); s.BeginNext(); }
            }
            Assert.IsTrue(s.Dismissed);
            Assert.IsTrue(s.ChoosingOffer);
            Assert.AreEqual(1, s.Dismissals);
            Assert.AreEqual(3, s.Offers.Count);
            int newId = s.Offers[1].ClubId;
            s.TakeOffer(1, "");
            s.TryAcknowledgeEvent(out _);
            Assert.IsTrue(s.TryBeginNext(out var e), e.Code.ToString());
            Assert.AreEqual(newId, s.PlayerClub.Id);
            Assert.AreSame(s.PlayerClub.Team, s.Team);
            Assert.AreEqual(year, s.Season.Year, "same season");
            Assert.IsFalse(s.Dismissed);
            var r = s.Resolve();
            Assert.IsNotNull(r.Summary);
        }
    }
}
