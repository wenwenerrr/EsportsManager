using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EsportsManager.Events;
using EsportsManager.Growth;
using EsportsManager.Leagues;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using NUnit.Framework;
using static EsportsManager.Tests.Leagues.LeagueTestFactory;

namespace EsportsManager.Tests.Leagues
{
    /// <summary>design/gdd/league.md rev 3 Acceptance Criteria 1-13 (Story 013).</summary>
    public sealed class LeagueTests
    {
        // ------------------------------------------------------------ helpers

        private static Player StarterOfSoleLane(LeagueWorld w, Club c)
        {
            foreach (var p in c.Team.Starters())
                if (p.MainLane == c.Team.LaneOf(p) && c.Team.Roster.Count(q => q.MainLane == p.MainLane) == 1) return p;
            return null;
        }

        private static void ShiftPhase(LeagueWorld w, Player p, int phase, float delta)
        {
            foreach (int d in w.Growth.Map.PhaseDims(p.MainLane, phase)) p.SetDimension(p.MainLane, d, p.GetDimension(p.MainLane, d) + delta);
        }

        // ------------------------------------------------------------ AC1 / AC2 determinism

        [Test]
        public void SameSeed_GenerateAndWarmUp_SameLeagueHash()
        {
            var a = World(); var b = World(); var c = World(Seed + 1);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "generation");
            a.RunWarmUp(); b.RunWarmUp(); c.RunWarmUp();
            Assert.AreEqual(a.StateHash(), b.StateHash(), "after the warm-up");
            Assert.AreNotEqual(a.StateHash(), c.StateHash(), "another seed differs");
            Assert.AreEqual(0, a.Season.History[0].Year);
            Assert.AreEqual(1, a.Year, "the warm-up is year 0; year 1 waits for the player");
        }

        [Test]
        public void PlayerStrategyChange_EveryDrawKeyInBothRuns_HasTheSameValue()
        {
            Dictionary<(ulong, long), double> Run(bool scripted)
            {
                var s = MakeSession();
                var log = new Dictionary<(ulong, long), double>();
                s.World.Keys.Recorder = (k, n, u) => log[(k, n)] = u;
                for (int i = 0; i < 12; i++)
                {
                    if (scripted) s.ApplyScriptedPlan();
                    s.ResolveAndAdvance();
                }
                return log;
            }
            var a = Run(true);
            var b = Run(false);
            int common = 0;
            foreach (var kv in a)
                if (b.TryGetValue(kv.Key, out double u)) { common++; Assert.AreEqual(kv.Value, u, 0.0, "key " + kv.Key); }
            Assert.Greater(common, 1000, "the AI and the matches drew under shared keys");
        }

        // ------------------------------------------------------------ AC3 generation

        [Test]
        public void Generation_FourStylesTwoEach_RostersDraftedWithFiveLanes()
        {
            var w = World();
            foreach (ClubStyle st in Enum.GetValues(typeof(ClubStyle))) Assert.AreEqual(2, w.Clubs.Count(c => c.Style == st), st.ToString());
            Assert.AreEqual(4, w.Clubs.Count(c => c.Team.Roster.Count == 7));
            Assert.AreEqual(4, w.Clubs.Count(c => c.Team.Roster.Count == 6));
            var ids = new HashSet<long>();
            foreach (var c in w.Clubs)
            {
                Assert.AreEqual(5, c.Team.Roster.Select(p => p.MainLane).Distinct().Count(), c.Name + " has every lane");
                Assert.AreEqual(5, c.Team.Starters().Count);
                Assert.AreEqual(2, c.Runner.GetState().ActiveContracts.Count, "two opening sponsors");
                Assert.That(c.Team.Funds, Is.InRange(400000L, 1000000L));
                Assert.Greater(c.Team.Chemistry(), 0f);
                foreach (var p in c.Team.Roster) { Assert.IsTrue(ids.Add(p.Id)); Assert.That(p.ContractYearsLeft, Is.InRange(1, 3)); Assert.Greater(p.Salary, 0); }
                if (c.Style == ClubStyle.Star) { Assert.IsNotNull(c.Find(c.StarPlayerId)); Assert.That(c.StarPhase, Is.EqualTo(0).Or.EqualTo(2)); }
            }
            Assert.AreEqual(80 - 52, w.FreeAgents.Count, "about 25 free agents");
        }

        [Test]
        public void OpeningSalary_IsSigningSalaryTimes085()
        {
            var w = World();
            var p = w.Clubs[0].Team.Roster[0];
            int years = p.ContractYearsLeft;
            double market = w.Economy.MarketSalary(p) * w.Economy.YearFactor(p.Age, years);
            Assert.That(p.Salary, Is.InRange(market * 0.85 * 0.9 - 1, market * 0.85 * 1.1 + 1));
        }

        // ------------------------------------------------------------ AC4 rest

        [Test]
        public void RestThreshold_Aggression0And1_55And70_PreEvent40And70_Stress56Rests()
        {
            var w = World(events: false);
            var c = w.Clubs[0];
            c.Personality.OffRest = 0f;
            c.Personality.Aggression = 0f;
            Assert.AreEqual(55f, w.Ai.RestThreshold(c, false), 1e-4f);
            Assert.AreEqual(40f, w.Ai.RestThreshold(c, true), 1e-4f);
            c.Personality.Aggression = 1f;
            Assert.AreEqual(70f, w.Ai.RestThreshold(c, false), 1e-4f);
            Assert.AreEqual(70f, w.Ai.RestThreshold(c, true), 1e-4f);

            c.Personality.Aggression = 0f;
            w.BeginSegment();
            w.Ai.PickLineup(c);
            var who = StarterOfSoleLane(w, c);
            Assert.IsNotNull(who);
            foreach (var p in c.Team.Roster) p.Stress = 10f;
            who.Stress = 56f;
            var plan = w.Ai.Plan(c, 0);
            Assert.AreEqual(1, plan.Assignments.Count(a => a.PlayerId == who.Id && a.Action == SegmentAction.Rest), "56 > 55: one rest point");
            Assert.IsFalse(plan.Assignments.Any(a => a.PlayerId == who.Id && a.Action == SegmentAction.Train), "resting starters do not train");
            who.Stress = 76f;
            plan = w.Ai.Plan(c, 0);
            Assert.AreEqual(2, plan.Assignments.Count(a => a.PlayerId == who.Id && a.Action == SegmentAction.Rest), "> threshold + 20: two");
        }

        // ------------------------------------------------------------ AC5 sponsors and cash modes

        [Test]
        public void Plan_FillsEverySponsorSlot_CashModesStreamAndBusiness()
        {
            var w = World(events: false);
            w.BeginSegment();
            foreach (var c in w.Clubs)
            {
                var st = c.Runner.GetState();
                c.Team.Funds = 1000000000L;
                var rich = w.Ai.Plan(c, 0);
                Assert.AreEqual(st.FreeContractSlots, rich.SponsorPoints, "every free slot, whatever the funds");
                var starters = new HashSet<long>(c.Team.Starters().Select(p => p.Id));
                var resting = new HashSet<long>(rich.Assignments.Where(a => a.Action == SegmentAction.Rest).Select(a => a.PlayerId));
                var expectBench = c.Team.Roster.Where(p => !starters.Contains(p.Id)).OrderByDescending(p => p.Popularity).ThenBy(p => p.Id).Take(2).Select(p => p.Id);
                CollectionAssert.AreEquivalent(expectBench, rich.Assignments.Where(a => a.Action == SegmentAction.Stream).Select(a => a.PlayerId), "funds >= floor: the 2 most popular bench players stream");

                c.Team.Funds = -1;
                var poor = w.Ai.Plan(c, 0);
                Assert.AreEqual(st.FreeContractSlots, poor.SponsorPoints);
                resting = new HashSet<long>(poor.Assignments.Where(a => a.Action == SegmentAction.Rest).Select(a => a.PlayerId));
                var expect = c.Team.Roster.Where(p => !resting.Contains(p.Id)).OrderByDescending(p => p.Popularity).ThenBy(p => p.Id).Take(2).Select(p => p.Id);
                CollectionAssert.AreEquivalent(expect, poor.Assignments.Where(a => a.Action == SegmentAction.Stream).Select(a => a.PlayerId), "funds < floor: the 2 most popular players stream");
                foreach (var o in st.PendingOffers)
                {
                    var p = c.Find(o.PlayerId);
                    int used = poor.Assignments.Count(a => a.PlayerId == p.Id) + poor.AcceptedOfferIds.Count(id => id == o.Id);
                    bool accepted = poor.AcceptedOfferIds.Contains(o.Id);
                    if (p.Stress >= 70f) Assert.IsFalse(accepted);
                    else if (used < 2) Assert.IsTrue(accepted, "emergency: every offer of a player under 70 stress");
                }
                Assert.IsTrue(c.Runner.TryValidate(poor, w.Context(), out var e), e.Code.ToString());
                Assert.IsTrue(c.Runner.TryValidate(rich, w.Context(), out e), e.Code.ToString());
            }
        }

        // ------------------------------------------------------------ AC6 specialty chase

        [Test]
        public void TrainTarget_UnownedSpecialtyDim73Focus05_ChasesEarly_71DoesNot()
        {
            var w = World(events: false);
            var c = w.Clubs.First(x => x.Team.GetStarter("jungle")?.MainLane == "jungle");
            var p = c.Team.GetStarter("jungle");
            var lane = w.Players.FindLane("jungle");
            int gpm = PlayerConfig.DimIndex(lane, "gold_per_min");
            for (int d = 0; d < 6; d++) p.SetDimension("jungle", d, 60f);
            p.RemoveSpecialty("jungle_king");
            p.SetFocus(0, 0.5f); p.SetFocus(1, 0.2f); p.SetFocus(2, 0.2f);
            p.SetDimension("jungle", gpm, 73f);
            var t = w.Ai.TrainTarget(c, p, w.Keys.Stream("t"), out int focus);
            Assert.AreEqual(TrainingTarget.Early, t);
            Assert.AreEqual(gpm, focus);
            p.SetDimension("jungle", gpm, 71f);
            w.Ai.TrainTarget(c, p, w.Keys.Stream("t"), out focus);
            Assert.AreEqual(-1, focus, "71 < 72: no chase");
            p.SetDimension("jungle", gpm, 73f); p.SetFocus(0, 0.4f);
            w.Ai.TrainTarget(c, p, w.Keys.Stream("t"), out focus);
            Assert.AreEqual(-1, focus, "focus 0.4 < 0.45: no chase");
        }

        // ------------------------------------------------------------ AC7 lineup hysteresis

        [TestCase(2f, false)]
        [TestCase(4f, true)]
        public void Lineup_ChallengerBetterBy_SwapsOnlyAbove3(float diff, bool swaps)
        {
            var w = World(events: false);
            var c = w.Clubs[1];
            w.Ai.PickLineup(c);
            var cur = c.Team.GetStarter("mid");
            if (cur.MainLane != "mid") Assert.Inconclusive("mid starter is off-lane");
            var rival = w.FreeAgents.First(p => p.MainLane == "mid");
            w.Sign(c, rival, 2);
            foreach (var p in c.Team.Roster.Where(x => x.MainLane == "mid" && x.Id != cur.Id && x.Id != rival.Id).ToList()) c.Team.RemovePlayer(p);
            for (int d = 0; d < 6; d++) { cur.SetDimension("mid", d, 60f); rival.SetDimension("mid", d, 60f); }
            cur.Mood = rival.Mood = 100f; cur.Stress = rival.Stress = 0f;
            float m = (float)MatchFormulas.MoodFactor(w.Match.effective, 100f);
            for (int d = 0; d < 6; d++) rival.SetDimension("mid", d, 60f + diff / m);
            Assert.AreEqual(diff, w.Ai.EffectiveRating(rival) - w.Ai.EffectiveRating(cur), 1e-3f);
            w.Ai.PickLineup(c);
            Assert.AreEqual(swaps ? rival.Id : cur.Id, c.Team.GetStarter("mid").Id);
        }

        // ------------------------------------------------------------ AC8 tactics

        [Test]
        public void RegularTactic_InvadeStyleLeadingInLate_Develop_UnlessStyleNoise()
        {
            var w = World(events: false);
            var c = w.Clubs.First(x => x.Style != ClubStyle.Star);
            c.Style = ClubStyle.Invade;
            foreach (var p in c.Team.Starters()) { ShiftPhase(w, p, 0, -10f); ShiftPhase(w, p, 1, -10f); ShiftPhase(w, p, 2, 15f); }
            w.Invalidate();
            int noise = 0, plain = 0;
            for (int e = 0; e < 30; e++)
            {
                double u = w.Keys.U01("tac", w.Year, e, 0, c.Id);
                var t = w.Ai.RegularTactic(c, e);
                if (u < 0.2) { noise++; Assert.AreEqual(MatchTactic.Invade, t, "style noise -> the style tactic"); }
                else { plain++; Assert.AreEqual(MatchTactic.Develop, t, "lead over the median is late"); }
            }
            Assert.Greater(noise, 0); Assert.Greater(plain, 0);
        }

        [Test]
        public void PlayoffTactic_LargestLeadOverOpponentRadar()
        {
            var w = World(events: false);
            var a = w.Clubs[0]; var b = w.Clubs[1];
            foreach (var p in a.Team.Starters()) ShiftPhase(w, p, 1, 20f);
            w.Invalidate();
            var sa = w.PhaseStrength(a); var rb = w.Ai.Radar(w.PhaseStrength(b));
            int k = LeagueAi.ArgMax(new[] { sa[0] - rb[0], sa[1] - rb[1], sa[2] - rb[2] });
            Assert.AreEqual(w.Ai.TacticOfPhase(k), w.Ai.PlayoffTactic(a, b));
            foreach (float v in rb) Assert.AreEqual(0f, v % 5f, 1e-4f, "opponents are seen rounded to 5");
        }

        // ------------------------------------------------------------ AC9 style switch

        [Test]
        public void StyleSwitch_TwoMissedEvents_Adapt1_Switches_LockedThenNoReturn()
        {
            var w = World(events: false);
            var c = w.Clubs.First(x => x.Style != ClubStyle.Star);
            var original = c.Style;
            c.Personality.Adaptability = 1f;
            int Year(int from) { for (int y = from; ; y++) if (w.Keys.U01("style", y, c.Id) < 0.5) return y; }
            int y1 = Year(0);
            c.YearPlacings.Clear(); c.YearPlacings.AddRange(new[] { 5, 6, 1 });
            c.EventsSinceSwitch = 0;
            w.StyleEvaluation(y1);
            Assert.AreNotEqual(original, c.Style, "switched");
            Assert.AreEqual(original, c.PreviousStyle);
            Assert.AreEqual(NewsKind.StyleSwitch, w.News.Last().Kind);
            Assert.AreEqual(1, c.StyleHistory.Count);
            var second = c.Style;
            c.YearPlacings.Clear(); c.YearPlacings.AddRange(new[] { 6, 7 });
            w.StyleEvaluation(Year(y1 + 1));
            Assert.AreEqual(second, c.Style, "locked: only 2 events since the switch");
            c.YearPlacings.Clear(); c.YearPlacings.AddRange(new[] { 6, 7, 8 });
            w.StyleEvaluation(Year(y1 + 2));
            Assert.AreNotEqual(second, c.Style);
            Assert.AreNotEqual(original, c.Style, "never back to the previous style");
        }

        [Test]
        public void StyleSwitch_OneMissedEvent_NoSwitch()
        {
            var w = World(events: false);
            var c = w.Clubs[0];
            var st = c.Style;
            c.Personality.Adaptability = 1f;
            c.YearPlacings.AddRange(new[] { 6, 1, 2 });
            for (int y = 0; y < 10; y++) w.StyleEvaluation(y);
            Assert.AreEqual(st, c.Style);
        }

        [Test]
        public void StyleLabel_StarWhenStarLaneLeadAtLeast15()
        {
            var w = World(events: false);
            var c = w.Clubs[2];
            var p = c.Team.GetStarter("adc");
            for (int d = 0; d < 6; d++) p.SetDimension(p.MainLane, d, 99f);
            w.Invalidate();
            float lead = w.StarLaneLead(c, out _);
            Assert.GreaterOrEqual(lead, 15f, "a 99 adc leads the median by 15+");
            Assert.AreEqual(ClubStyle.Star, w.StyleLabel(c));
        }

        // ------------------------------------------------------------ AC10 offers and takeover

        [TestCase(0, 6f)]
        [TestCase(1, 6f)]
        [TestCase(2, 8f)]
        public void Offers_Ranks34_45_67_Distinct_TakeoverNormalised(int index, float segs)
        {
            var s = MakeSession(take: false);
            Assert.IsTrue(s.ChoosingOffer);
            var w = s.World;
            var offers = w.Offers.ToList();
            Assert.AreEqual(3, offers.Count);
            Assert.That(offers[0].StrengthRank, Is.InRange(3, 4));
            Assert.That(offers[1].StrengthRank, Is.InRange(4, 5));
            Assert.That(offers[2].StrengthRank, Is.InRange(6, 7));
            Assert.AreEqual(3, offers.Select(o => o.ClubId).Distinct().Count());
            var rank = w.StrengthRanking();
            foreach (var o in offers) Assert.AreEqual(o.ClubId, rank[o.StrengthRank - 1].Id);
            var c = s.TakeOffer(index, "新队名");
            Assert.AreEqual("新队名", c.Name);
            Assert.AreEqual("新队名", w.Schedule.ClubName(c.Id));
            Assert.GreaterOrEqual((double)c.Team.Funds, segs * c.WagesPerSegment(12) - 1);
            Assert.GreaterOrEqual(c.Team.Roster.Count, 6);
            Assert.AreEqual(5, c.Team.Roster.Select(p => p.MainLane).Distinct().Count());
            Assert.AreEqual(0, c.Debt); Assert.IsFalse(c.Bankrupt);
            Assert.AreEqual(5, c.Team.Starters().Count);
            Assert.AreEqual(1, s.Season.Year); Assert.AreEqual(0, s.Season.SegmentIndexInYear);
            Assert.IsTrue(c.IsPlayer); Assert.AreEqual(c.Id, w.Schedule.PlayerClub);
        }

        // ------------------------------------------------------------ AC11 F5

        private static PendingChoice Choice(string id, long? player, float option0Funds = 0f)
        {
            var o0 = new ChoiceOptionView();
            if (option0Funds != 0f) o0.Effects.Add(new EventEffectResult { Type = EffectType.Funds, Value = option0Funds });
            return new PendingChoice { Id = 1, EventId = id, PlayerId = player, DefaultOption = 1, Options = new List<ChoiceOptionView> { o0, new ChoiceOptionView() } };
        }

        [Test]
        public void ChoiceRules_F5_LeaveMoneySpendConflict()
        {
            var w = World(events: false);
            var c = w.Clubs[0];
            c.Personality.Aggression = 0f; c.Personality.OffRest = 0f;
            var p = c.Team.Starters()[0];
            p.Stress = 60f;
            Assert.AreEqual(0, w.Ai.ChooseOption(c, Choice("choice_home_leave", p.Id), false), "stress > rest_th, no event next");
            Assert.AreEqual(1, w.Ai.ChooseOption(c, Choice("choice_home_leave", p.Id), true), "event next: refuse");
            p.Stress = 50f;
            Assert.AreEqual(1, w.Ai.ChooseOption(c, Choice("choice_home_leave", p.Id), false));

            double floor = w.Ai.Floor(c);
            c.Team.Funds = (long)floor - 1;
            Assert.AreEqual(0, w.Ai.ChooseOption(c, Choice("choice_extra_stream", p.Id), false), "funds < floor, stress < 60");
            p.Stress = 65f;
            Assert.AreEqual(1, w.Ai.ChooseOption(c, Choice("choice_variety_show", p.Id), false));
            p.Stress = 30f; c.Team.Funds = (long)floor + 1;
            Assert.AreEqual(1, w.Ai.ChooseOption(c, Choice("choice_extra_stream", p.Id), false), "funds >= floor: refuse");

            c.Team.Funds = (long)Math.Ceiling(floor) + 60000;
            Assert.AreEqual(0, w.Ai.ChooseOption(c, Choice("choice_equipment_upgrade", null, -50000f), false), "funds - cost >= floor");
            c.Team.Funds = (long)floor + 40000;
            Assert.AreEqual(1, w.Ai.ChooseOption(c, Choice("choice_equipment_upgrade", null, -50000f), false));
            Assert.AreEqual(0, w.Ai.ChooseOption(c, Choice("choice_teammate_conflict", p.Id), false), "always reconcile");
        }

        // ------------------------------------------------------------ AC12 same formulas

        [Test]
        public void PlayerClubWithTheAiPlan_SameResultAsTheAiClub()
        {
            var a = World(); var b = World();
            a.BeginSegment(); b.BeginSegment();
            b.TakeOver(new JobOffer { ClubId = 3, Tier = OfferTier.Mid }, b.Clubs[3].Name);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "the takeover changed nothing here (no top-up needed, full roster)");
            var plan = b.Ai.Plan(b.Clubs[3], 0);
            a.ResolveSegment(null);
            b.ResolveSegment(plan);
            Assert.AreEqual(a.StateHash(), b.StateHash(), "player and AI share every formula and draw");
        }

        // ------------------------------------------------------------ year flow, news, leader

        [Test]
        public void WarmUp_RunsAYear_ThreeEventsPlacings_RolloverKeepsRostersAndPoolCap()
        {
            var w = World();
            w.RunWarmUp();
            var y0 = w.Season.History[0];
            Assert.AreEqual(3, y0.Events.Count);
            Assert.IsTrue(y0.Events.All(e => e.Event.IsComplete));
            Assert.LessOrEqual(w.FreeAgents.Count, 35);
            foreach (var c in w.Clubs)
            {
                Assert.GreaterOrEqual(c.Team.Roster.Count, 6, c.Name);
                Assert.AreEqual(5, c.Team.Starters().Count);
                Assert.That(c.TacticHistory.Count, Is.InRange(2, 5));
                Assert.That(c.LastYearFinalsPlace, Is.InRange(0, 4));
            }
            Assert.LessOrEqual(w.News.Count, 10);
            Assert.IsTrue(w.News.Count > 0);
            Assert.AreEqual(1, w.Clubs.Count(c => c.LastYearFinalsPlace == 1));
            var leader = w.CurrentLeader();
            Assert.IsNotNull(leader, "before the first event: last year's champion");
            Assert.AreEqual(1, leader.LastYearFinalsPlace);
        }

        [Test]
        public void SelfRescue_AiSellsBenchThenBorrows_PlayerFlagsBankrupt()
        {
            var w = World(events: false);
            var ai = w.Clubs[0];
            int before = ai.Team.Roster.Count;
            ai.Team.Funds = -100000000L;
            w.FundsCheck(ai, 1);
            Assert.AreEqual(5, ai.Team.Roster.Count, "bench sold down to 5");
            Assert.AreEqual(0, ai.Team.Funds);
            Assert.Greater(ai.Debt, 0);
            Assert.AreEqual(NewsKind.Loan, w.News.Last().Kind);
            Assert.AreEqual(28 + before - 5, w.FreeAgents.Count, "sold players join the pool");
            var pl = w.Clubs[1];
            w.TakeOver(new JobOffer { ClubId = 1, Tier = OfferTier.Mid }, pl.Name);
            pl.Team.Funds = -100000000L;
            w.FundsCheck(pl, 1);
            Assert.IsTrue(pl.Bankrupt);
            Assert.AreEqual(0, pl.Debt, "no loan for the player");
        }

        // ------------------------------------------------------------ AC13 performance

        [Test]
        public void Performance_WarmUpPlusOneYear_MedianUnder20msPerLeagueYear()
        {
            World(1).RunWarmUp(); // JIT warm-up
            var times = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var w = World(100 + i);
                var sw = Stopwatch.StartNew();
                w.RunWarmUp();
                w.RunAiYear();
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds / 2.0);
            }
            times.Sort();
            TestContext.WriteLine("ms per league-year: " + string.Join(", ", times.Select(t => t.ToString("F1"))));
            Assert.LessOrEqual(times[2], 20.0);
        }
    }
}
