using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Session;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;
using EsportsManager.Tests.Segments;
using EsportsManager.Tests.Teams;
using NUnit.Framework;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 004 stage C1: calendar, split / finals formats, event phases and effects (numbers.md 4.18 v3.4).</summary>
    public class TournamentTests
    {
        private const float Tol = 1e-3f;
        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);
        private static GameSession Make(int seed) => GameSession.FromJson(Data("player_config.json"), Data("team_config.json"),
            Data("segment_config.json"), Data("match_config.json"), Data("league_config.json"), seed);

        // Resolves segments (scripted plan) until an event is pending; returns the segments resolved.
        private static int ToNextEvent(GameSession s)
        {
            for (int n = 1; ; n++)
            {
                s.ApplyScriptedPlan();
                s.Resolve();
                if (s.EventPhase != EventPhase.None) return n;
                s.BeginNext();
            }
        }

        // ------------------------------------------------------------ calendar

        [Test]
        public void Events_OnlyAfterSegments3_6_12_NoMatchTermsInSegments()
        {
            var s = Make(31);
            var eventSegments = new List<int>();
            var keys = new List<string>();
            for (int seg = 1; seg <= 12; seg++)
            {
                s.ApplyScriptedPlan();
                var r = s.Resolve();
                foreach (var p in r.Summary.Players) { Assert.AreEqual(0, p.MatchesPlayed); Assert.AreEqual(0, p.MatchesLost); }
                if (s.EventPhase != EventPhase.None)
                {
                    eventSegments.Add(seg);
                    keys.Add(s.CurrentEvent.Event.Def.nameKey);
                    Assert.AreEqual(EventPhase.Prepare, s.EventPhase);
                    s.Season.AutoPlay();
                }
                else Assert.AreEqual(EventPhase.None, s.EventPhase);
                s.AutoCompleteTransferWindow(); // Story 005: the year rolls when the window closes
                s.BeginNext();
            }
            CollectionAssert.AreEqual(new[] { 3, 6, 12 }, eventSegments);
            CollectionAssert.AreEqual(new[] { "spring", "summer", "finals" }, keys);
            Assert.AreEqual(2, s.Season.Year);
            Assert.AreEqual(0, s.Season.SegmentsDoneInYear);
        }

        [Test]
        public void NextEvent_CountsDownSegments()
        {
            var s = Make(32);
            var n = s.NextEvent().Value;
            Assert.AreEqual("spring", n.Def.nameKey);
            Assert.AreEqual(3, n.SegmentsUntil);
            s.ApplyScriptedPlan(); s.ResolveAndAdvance();
            Assert.AreEqual(2, s.NextEvent().Value.SegmentsUntil);
            s.ApplyScriptedPlan(); s.ResolveAndAdvance();
            s.ApplyScriptedPlan(); s.ResolveAndAdvance(); // spring auto-played
            Assert.AreEqual("summer", s.NextEvent().Value.Def.nameKey);
            Assert.AreEqual(3, s.NextEvent().Value.SegmentsUntil);
        }

        // ------------------------------------------------------------ split format

        [Test]
        public void RegularSeason_SingleRoundRobin_SevenSeriesEach_28Total_BO3()
        {
            var s = Make(33);
            ToNextEvent(s);
            Assert.IsTrue(s.TryPlayRegularSeason(out var e), e.Code.ToString());
            var ev = s.CurrentEvent.Event;
            Assert.AreEqual(28, ev.RegularSeries.Count);
            var perClub = new int[8];
            var pairs = new HashSet<(int, int)>();
            foreach (var x in ev.RegularSeries)
            {
                perClub[x.ClubA]++; perClub[x.ClubB]++;
                Assert.IsTrue(pairs.Add((Math.Min(x.ClubA, x.ClubB), Math.Max(x.ClubA, x.ClubB))), "pair twice");
                Assert.AreEqual(3, x.BestOf);
                Assert.AreEqual(2, Math.Max(x.Result.ScoreA, x.Result.ScoreB));
                Assert.AreEqual(x.ClubB != s.Tournament.League.PlayerClub, true, "player is always side A");
            }
            CollectionAssert.AreEqual(Enumerable.Repeat(7, 8), perClub);
            Assert.AreEqual(7, ev.PlayerSeries().Count);
            Assert.AreEqual(28, ev.StandingsRows().Sum(r => r.SeriesWins));
            Assert.AreEqual(EventPhase.PlayoffPrep, s.EventPhase);
        }

        [Test]
        public void Playoffs_Seeding1v4_2v3_PlacingsPrizesPoints()
        {
            var s = Make(34);
            ToNextEvent(s);
            Assert.IsTrue(s.TryPlayRegularSeason(out _));
            var ev = s.CurrentEvent.Event;
            var rank = ev.Regular.Ranking();
            CollectionAssert.AreEqual(rank.Take(4).Select(r => r.ClubIndex), ev.Seeds);
            long funds = s.Team.Funds;
            Assert.IsTrue(s.TryPlayPlayoffs(out var e), e.Code.ToString());
            Assert.AreEqual(EventPhase.Done, s.EventPhase);
            Assert.AreEqual(3, ev.PlayoffSeries.Count);
            var semi1 = ev.PlayoffSeries[0]; var semi2 = ev.PlayoffSeries[1]; var fin = ev.PlayoffSeries[2];
            CollectionAssert.AreEquivalent(new[] { ev.Seeds[0], ev.Seeds[3] }, new[] { semi1.ClubA, semi1.ClubB });
            CollectionAssert.AreEquivalent(new[] { ev.Seeds[1], ev.Seeds[2] }, new[] { semi2.ClubA, semi2.ClubB });
            CollectionAssert.AreEquivalent(new[] { semi1.Winner, semi2.Winner }, new[] { fin.ClubA, fin.ClubB });
            Assert.AreEqual(5, semi1.BestOf); Assert.AreEqual(5, semi2.BestOf); Assert.AreEqual(5, fin.BestOf);
            Assert.AreEqual(3, Math.Max(fin.Result.ScoreA, fin.Result.ScoreB));

            var cfg = s.MatchConfig.format;
            Assert.AreEqual(1, ev.Placing[fin.Winner]);
            Assert.AreEqual(2, ev.Placing[fin.Loser]);
            Assert.AreEqual(3, ev.Placing[semi1.Loser]);
            Assert.AreEqual(3, ev.Placing[semi2.Loser]);
            foreach (var r in rank.Skip(4)) Assert.AreEqual(r.Rank, ev.Placing[r.ClubIndex], "5-8 by regular-season rank");
            long[] prizeByPlace = { 0, 80000, 50000, 30000, 30000, 10000, 10000, 10000, 10000 };
            int[] pointsByPlace = { 0, 10, 7, 5, 5, 3, 2, 1, 0 };
            for (int c = 0; c < 8; c++)
            {
                Assert.AreEqual(prizeByPlace[ev.Placing[c]], ev.Prize[c], "club " + c);
                Assert.AreEqual(pointsByPlace[ev.Placing[c]], ev.Points[c], "club " + c);
            }
            Assert.AreEqual(cfg.finalsPoints.Sum(), ev.Points.Sum());
            Assert.AreEqual(ev.PlayerPrize, s.CurrentEvent.PrizeIncome);
            Assert.AreEqual(funds + ev.PlayerPrize, s.Team.Funds);
            Assert.AreEqual(s.CurrentEvent.PrizeIncome, s.Season.ThisYear.TotalPrize);
            Assert.AreEqual(ev.PlayerInBracket, ev.PlayoffSeries.Any(x => x.PlayerInvolved));
        }

        // ------------------------------------------------------------ phases, lineup lock

        [Test]
        public void LineupLockedInDone_OneAdjustmentBeforePlayoffs_TacticPerStage()
        {
            GameSession s = null;
            for (int seed = 40; seed < 90; seed++)
            {
                s = Make(seed);
                ToNextEvent(s);
                Assert.IsTrue(s.SetTactic(MatchTactic.Teamfight, out _));
                Assert.IsTrue(s.TryPlayRegularSeason(out _));
                if (s.CurrentEvent.Event.PlayerInBracket) break;
            }
            Assert.IsTrue(s.CurrentEvent.Event.PlayerInBracket, "no seed put the player into the playoffs");
            foreach (var x in s.CurrentEvent.Event.PlayerSeries()) Assert.AreEqual(MatchTactic.Teamfight, x.Result.SideA.Tactic);

            // the one adjustment window
            Assert.AreEqual(EventPhase.PlayoffPrep, s.EventPhase);
            var bench = s.Team.Roster.First(p => s.Team.LaneOf(p) == null);
            Assert.IsTrue(s.ToggleStarter(bench.Id, out var te), te.Code.ToString());
            Assert.IsTrue(s.SetTactic(MatchTactic.Develop, out _));
            Assert.IsTrue(s.TryPlayPlayoffs(out _));
            var po = s.CurrentEvent.Event.PlayoffSeries.Where(x => x.PlayerInvolved).ToList();
            Assert.Greater(po.Count, 0);
            foreach (var x in po) { Assert.AreEqual(MatchTactic.Develop, x.Result.SideA.Tactic); Assert.IsTrue(x.Result.SideA.Played(bench.Id)); }

            // Done: locked, next segment blocked until acknowledged
            Assert.AreEqual(EventPhase.Done, s.EventPhase);
            Assert.IsFalse(s.ToggleStarter(bench.Id, out var le));
            Assert.AreEqual(PlanErrorCode.LineupLocked, le.Code);
            Assert.IsFalse(s.SetTactic(MatchTactic.Invade, out le));
            Assert.AreEqual(PlanErrorCode.LineupLocked, le.Code);
            Assert.AreEqual(MatchTactic.Develop, s.CurrentTactic);
            Assert.Throws<InvalidOperationException>(() => s.BeginNext());
            Assert.IsFalse(s.TryBeginNext(out var be));
            Assert.AreEqual(PlanErrorCode.EventPending, be.Code);
            Assert.Throws<InvalidOperationException>(() => s.Resolve());
            Assert.IsFalse(s.TryPlayPlayoffs(out var we));
            Assert.AreEqual(PlanErrorCode.WrongEventPhase, we.Code);
            Assert.IsTrue(s.TryAcknowledgeEvent(out _));
            Assert.AreEqual(EventPhase.None, s.EventPhase);
            Assert.IsFalse(s.TryAcknowledgeEvent(out we));
            Assert.AreEqual(PlanErrorCode.WrongEventPhase, we.Code);
            Assert.IsTrue(s.TryBeginNext(out _));
            Assert.IsTrue(s.ToggleStarter(bench.Id, out _), "editable again while a segment is open");
        }

        [Test]
        public void WrongPhaseAndIncompleteLineup_ChangeNothing()
        {
            var s = Make(35);
            Assert.IsFalse(s.TryPlayRegularSeason(out var e0));
            Assert.AreEqual(PlanErrorCode.WrongEventPhase, e0.Code);
            ToNextEvent(s);
            var ev = s.CurrentEvent.Event;
            Assert.IsFalse(s.TryPlayPlayoffs(out var e1)); Assert.AreEqual(PlanErrorCode.WrongEventPhase, e1.Code);
            Assert.IsFalse(s.TryPlayFinals(out var e2)); Assert.AreEqual(PlanErrorCode.WrongEventPhase, e2.Code);
            Assert.IsFalse(s.TryAcknowledgeEvent(out var e3)); Assert.AreEqual(PlanErrorCode.WrongEventPhase, e3.Code);
            var starter = s.Team.Starters()[0];
            Assert.IsTrue(s.ToggleStarter(starter.Id, out _));
            var state = string.Join("|", s.Team.Roster.Select(p => p.Mood + "/" + p.Stress)) + s.Team.Funds;
            Assert.IsFalse(s.TryPlayRegularSeason(out var e4));
            Assert.AreEqual(PlanErrorCode.IncompleteLineup, e4.Code);
            Assert.AreEqual(0, ev.RegularSeries.Count);
            Assert.AreEqual(EventPhase.Prepare, s.EventPhase);
            Assert.AreEqual(state, string.Join("|", s.Team.Roster.Select(p => p.Mood + "/" + p.Stress)) + s.Team.Funds);
            Assert.IsTrue(s.ToggleStarter(starter.Id, out _));
            Assert.IsTrue(s.TryPlayRegularSeason(out _));
        }

        // ------------------------------------------------------------ finals

        [Test]
        public void Qualification_PointsThenRegularWinsThenGameDiffThenIndex()
        {
            var pc = PlayerConfigLoader.FromJson(Data("player_config.json"));
            var mc = MatchConfigLoader.FromJson(Data("match_config.json"));
            var tc = TeamConfigLoader.FromJson(Data("team_config.json"));
            var league = new League(Enumerable.Range(0, 8).Select(i => "C" + i).ToList());
            var runner = new TournamentRunner(league, mc, tc, pc, (y, e, st, slot) => new SeededRandomSource(1));
            EventResult Split(int[] points)
            {
                return new EventResult
                {
                    Def = new CalendarEventDef { type = EventType.Split, nameKey = "x", afterSegment = 3 },
                    Regular = new Standings(8), Points = points, IsComplete = true,
                };
            }
            var spring = Split(new[] { 10, 7, 5, 5, 3, 2, 1, 0 });
            var summer = Split(new[] { 0, 1, 2, 3, 5, 5, 7, 10 });
            // totals: c0 10, c1 8, c2 7, c3 8, c4 8, c5 7, c6 8, c7 10
            spring.Regular.Record(7, 0, 2, 1); // c7 above c0 on series wins
            spring.Regular.Record(4, 1, 2, 0);
            summer.Regular.Record(4, 2, 2, 0); // c4: 2 wins
            summer.Regular.Record(6, 5, 2, 0); // c6: 1 win, +2
            spring.Regular.Record(3, 5, 2, 1); // c3: 1 win, +1
            var unfinished = Split(new[] { 10, 7, 5, 5, 3, 2, 1, 0 }); unfinished.IsComplete = false; // ignored
            var rows = runner.Qualification(new[] { spring, summer, unfinished }, Enumerable.Range(0, 8).Select(i => "C" + i).ToArray());
            CollectionAssert.AreEqual(new[] { 7, 0, 4, 6, 3, 1, 2, 5 }, rows.Select(r => r.ClubIndex));
            CollectionAssert.AreEqual(new[] { 10, 10, 8, 8, 8, 8, 7, 7 }, rows.Select(r => r.Points));
            CollectionAssert.AreEqual(new[] { true, true, true, true, false, false, false, false }, rows.Select(r => r.Qualified));
            CollectionAssert.AreEqual(new[] { 10, 0 }, rows[1].SplitPoints);
            Assert.AreEqual(2, rows[2].RegularSeriesWins);
            Assert.AreEqual(4, rows[2].RegularGameDiff);
        }

        [Test]
        public void Finals_TopFourByPoints_SemisBO5_FinalBO7_PrizesAndYearHistory()
        {
            var s = Make(36);
            ToNextEvent(s); s.Season.AutoPlay(); s.BeginNext();
            ToNextEvent(s); s.Season.AutoPlay(); s.BeginNext();
            int n = ToNextEvent(s);
            Assert.AreEqual(6, n);
            var rec = s.CurrentEvent;
            var ev = rec.Event;
            Assert.AreEqual(EventType.Finals, ev.Type);
            var q = s.GetQualification();
            Assert.AreEqual(66, q.Sum(r => r.Points));
            CollectionAssert.AreEqual(q.Take(4).Select(r => r.ClubIndex), ev.Seeds);
            Assert.IsFalse(s.TryPlayRegularSeason(out var we)); Assert.AreEqual(PlanErrorCode.WrongEventPhase, we.Code);
            long funds = s.Team.Funds;
            Assert.IsTrue(s.TryPlayFinals(out var e), e.Code.ToString());
            Assert.AreEqual(3, ev.PlayoffSeries.Count);
            Assert.AreEqual(5, ev.PlayoffSeries[0].BestOf);
            Assert.AreEqual(5, ev.PlayoffSeries[1].BestOf);
            Assert.AreEqual(7, ev.PlayoffSeries[2].BestOf);
            Assert.AreEqual(4, Math.Max(ev.PlayoffSeries[2].Result.ScoreA, ev.PlayoffSeries[2].Result.ScoreB));
            long[] prize = { 300000, 180000, 90000, 90000 }; // economy.md F8: finals x1.5
            for (int pos = 0; pos < 4; pos++) Assert.AreEqual(prize[pos], ev.Prize[ev.FinalOrder[pos]]);
            for (int c = 0; c < 8; c++)
                if (!ev.Seeds.Contains(c)) { Assert.AreEqual(0, ev.Placing[c]); Assert.AreEqual(0, ev.Prize[c]); }
            Assert.AreEqual(ev.PlayerInBracket, ev.PlayerSeries().Count > 0);
            Assert.AreEqual(funds + ev.PlayerPrize, s.Team.Funds);
            var year = s.Season.ThisYear;
            Assert.AreEqual(3, year.Events.Count);
            Assert.AreEqual(year.Events.Sum(x => x.PrizeIncome), year.TotalPrize);
            Assert.IsTrue(s.TryAcknowledgeEvent(out _));
            Assert.IsTrue(s.InTransferWindow, "Story 005: the finals open the transfer window");
            Assert.AreEqual(1, s.Season.Year);
            s.AutoCompleteTransferWindow();
            Assert.AreEqual(2, s.Season.Year);
            Assert.AreEqual(3, s.Season.History.Count, "warm-up year 0, year 1, year 2");
            Assert.AreEqual(0, s.BuildContext().SegmentIndexInYear);
        }

        // ------------------------------------------------------------ effects

        [Test]
        public void EventEffects_StressPerSeriesPlayed_FWinPerWonSeries_MoodSign_RecentResults()
        {
            var pc = MatchTestFactory.MakePlayerConfig();
            var tc = TeamTestFactory.MakeTeamConfig();
            var cfg = SegmentTestFactory.MakeConfig();
            var team = new Team(tc, pc, new StubRandomSource(0.5f));
            var mine = MatchTestFactory.MakePlayers(pc, 0, fill: 70f);
            for (int i = 0; i < mine.Length; i++) { team.AddPlayer(mine[i]); team.SetStarter(MatchTestFactory.Lanes[i], mine[i]); }
            var bench = TeamTestFactory.MakePlayer(pc, 50, "mid", 50f, TeamTestFactory.NeutralA, TeamTestFactory.NeutralB);
            team.AddPlayer(bench);
            var sim = MatchTestFactory.MakeSimulator(null, pc, tc);
            var theirs = MatchTestFactory.MakeLineup(MatchTestFactory.MakePlayers(pc, 100, fill: 30f), "O");
            MatchResult win = null, loss = null;
            for (int seed = 0; seed < 500 && (win == null || loss == null); seed++)
            {
                var r = sim.Simulate(MatchLineup.FromTeam(team, MatchTactic.Develop), theirs, new SeededRandomSource(seed));
                if (r.WinnerIsA) win = win ?? r; else loss = loss ?? r;
            }
            Assert.IsNotNull(win); Assert.IsNotNull(loss);
            var series = new List<EventSeries>
            {
                new EventSeries { ClubA = 0, ClubB = 1, BestOf = 3, Result = win },
                new EventSeries { ClubA = 0, ClubB = 2, BestOf = 3, Result = win },
                new EventSeries { ClubA = 0, ClubB = 3, BestOf = 3, Result = loss },
                new EventSeries { ClubA = 2, ClubB = 3, BestOf = 3, Result = loss }, // AI series: ignored
            };
            var expectStress = mine.Select(p => p.Stress + SegmentFormulas.MatchStressDelta(cfg, p, 3, 1, 1f)).ToArray();
            var stage = EventEffects.ApplyStage(cfg, team, 0, series, new StubRandomSource(0.5f));
            Assert.AreEqual(5, stage.Count, "only the five who played");
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(Math.Min(100f, expectStress[i]), mine[i].Stress, Tol);
                Assert.AreEqual(3, stage[i].SeriesPlayed); Assert.AreEqual(1, stage[i].SeriesLost);
            }
            Assert.AreEqual(0f, bench.Stress, Tol, "bench did not play");
            Assert.AreEqual(2 * tc.familiarity.perWin, team.GetFamiliarity(mine[0].Id, mine[1].Id), Tol);
            Assert.AreEqual(0f, team.GetFamiliarity(mine[0].Id, bench.Id), Tol);

            var expectMood = mine.Select(p => p.Mood + SegmentFormulas.MatchMoodDelta(cfg, p, 1, 1f)).ToArray();
            float benchMood = bench.Mood;
            var end = EventEffects.ApplyEventEnd(cfg, team, 0, series, new StubRandomSource(0.5f));
            Assert.AreEqual(5, end.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual(Math.Min(100f, expectMood[i]), mine[i].Mood, Tol);
            Assert.AreEqual(benchMood, bench.Mood, Tol);
            Assert.AreEqual(200f / 3f, team.RecentWinRate(), Tol, "2 of 3 player series won");
            Assert.Greater(SegmentFormulas.MatchMoodDelta(cfg, mine[0], 1, 1f), 0f);
            Assert.Less(SegmentFormulas.MatchMoodDelta(cfg, mine[0], -1, 1f), 0f);
            Assert.AreEqual(0f, SegmentFormulas.MatchMoodDelta(cfg, mine[0], 0, 1f), Tol);
        }

        [Test]
        public void SessionEvent_RecentWinRateIsEventSeriesRate_StressRisesForPlayers()
        {
            var s = Make(37);
            ToNextEvent(s);
            var stressBefore = s.Team.Roster.ToDictionary(p => p.Id, p => p.Stress);
            var rec = s.Season.AutoPlay();
            var ps = rec.Event.PlayerSeries();
            Assert.That(ps.Count, Is.InRange(7, 9));
            Assert.AreEqual(100f * ps.Count(x => x.Result.WinnerIsA) / ps.Count, s.Team.RecentWinRate(), Tol);
            Assert.AreEqual(rec.RecentWinRateAfter, s.Team.RecentWinRate(), Tol);
            foreach (var eff in rec.RegularSeasonEffects)
            {
                Assert.AreEqual(7, eff.SeriesPlayed);
                Assert.Greater(eff.StressDelta, 0f);
            }
            Assert.AreEqual(5, rec.RegularSeasonEffects.Count);
            var benchId = s.Team.Roster.First(p => !rec.RegularSeasonEffects.Any(x => x.PlayerId == p.Id)).Id;
            Assert.AreEqual(stressBefore[benchId], s.Team.Roster.First(p => p.Id == benchId).Stress, Tol);
        }

        // ------------------------------------------------------------ determinism, balance, economy

        private static string RunYearLog(int seed)
        {
            var s = Make(seed);
            var log = new List<string>();
            for (int i = 0; i < 12; i++)
            {
                s.ApplyScriptedPlan();
                var r = s.Resolve();
                log.Add(r.FundsAfter.ToString());
                if (s.EventPhase != EventPhase.None)
                {
                    var rec = s.Season.AutoPlay();
                    log.Add(string.Join(",", rec.Event.RegularSeries.Concat(rec.Event.PlayoffSeries).Select(x => x.ClubA + "v" + x.ClubB + ":" + x.Result.ScoreA + "-" + x.Result.ScoreB)));
                    log.Add(string.Join(",", rec.Event.Placing) + " " + rec.PrizeIncome + " " + rec.FundsAfter);
                }
                s.AutoCompleteTransferWindow(); // Story 005: the year rolls when the window closes
                s.BeginNext();
            }
            log.Add(string.Join("|", s.Team.Roster.Select(p => p.Mood.ToString("F4") + "/" + p.Stress.ToString("F4"))));
            return string.Join("\n", log);
        }

        [Test]
        public void SameSeed_FullYear_Identical()
        {
            Assert.AreEqual(RunYearLog(16), RunYearLog(16));
            Assert.AreNotEqual(RunYearLog(16), RunYearLog(17));
        }

        private sealed class YearStats { public int SplitPlacingSum; public int FinalsPlacing; public long Prize; public long Salary; }

        private static YearStats PlayYear(GameSession s, bool train)
        {
            var st = new YearStats();
            for (int i = 0; i < 12; i++)
            {
                if (train) s.ApplyScriptedPlan();
                var r = s.Resolve();
                st.Salary += r.Summary.Report.SalaryShare;
                if (s.EventPhase != EventPhase.None)
                {
                    var rec = s.Season.AutoPlay();
                    st.Prize += rec.PrizeIncome;
                    if (rec.Event.Type == EventType.Split) st.SplitPlacingSum += rec.Event.PlayerPlacing;
                    else st.FinalsPlacing = rec.Event.PlayerPlacing;
                }
                s.AutoCompleteTransferWindow(); // Story 005: the year rolls when the window closes
                s.BeginNext();
            }
            return st;
        }

        [Test]
        public void FullYear_TrainedTeamPlacesBetterAndEarnsMorePrize_TenSeeds()
        {
            int splitT = 0, splitI = 0, finalsT = 0, finalsI = 0;
            long prizeT = 0, prizeI = 0, salaryT = 0;
            int[] seeds = { 101, 202, 303, 404, 505, 606, 707, 808, 909, 1010 };
            foreach (int seed in seeds)
            {
                var t = PlayYear(Make(seed), true);
                var i = PlayYear(Make(seed), false);
                TestContext.Out.WriteLine("SEED " + seed + ": trained split places sum " + t.SplitPlacingSum + " finals " + t.FinalsPlacing
                    + " prize " + t.Prize + " salary " + t.Salary + " | idle split places sum " + i.SplitPlacingSum + " finals " + i.FinalsPlacing
                    + " prize " + i.Prize);
                splitT += t.SplitPlacingSum; splitI += i.SplitPlacingSum;
                finalsT += t.FinalsPlacing > 0 ? 1 : 0; finalsI += i.FinalsPlacing > 0 ? 1 : 0;
                prizeT += t.Prize; prizeI += i.Prize; salaryT += t.Salary;
            }
            double n = seeds.Length;
            TestContext.Out.WriteLine("TOTAL avg split place trained " + (splitT / (2 * n)).ToString("F2") + " idle " + (splitI / (2 * n)).ToString("F2")
                + "; finals qualifications trained " + finalsT + " idle " + finalsI
                + "; avg prize/year trained " + (prizeT / n) + " idle " + (prizeI / n));
            TestContext.Out.WriteLine("ECON scripted plan: avg prize/year " + (prizeT / n) + ", avg salary/year " + (salaryT / n)
                + ", prize share " + (100.0 * prizeT / salaryT).ToString("F1") + "%");
            // Observed 2026-10-19 (numbers.md v3.4 balance, unchanged): split-place sums trained 89 vs idle 94 over 20 splits
            // (avg place 4.45 vs 4.70), prize 1,410,000 vs 1,160,000 over 10 years; 7 of 10 seeds identical (paired random
            // numbers, small per-game probability shifts). Thresholds are about half the observed gap.
            Assert.LessOrEqual(splitT, splitI - 2, "trained must place better on average: " + splitT + " vs " + splitI);
            // Story 005 (2026-10-29): the warm-up now runs the full transfer window and the economy rev 3 incomes, so these ten
            // seeds see different leagues; observed trained 750,000 vs idle 730,000 (split places 100 vs 108). Training still
            // has to win on both measures; the prize margin is no longer pinned to the v3.4 observation.
            Assert.Greater(prizeT, prizeI, "trained must earn more prize money: " + prizeT + " vs " + prizeI);
        }
    }
}
