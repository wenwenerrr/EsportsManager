using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Segments;
using EsportsManager.Tests.Teams;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 004: series simulation, report reasons, standings and the league schedule.</summary>
    public class MatchSimulationAndLeagueTests
    {
        private const int Seed = 4242;
        private const int SampleSize = 400;
        private PlayerConfig _pc;
        private TeamConfig _tc;
        private MatchConfig _mc;
        private MatchSimulator _sim;

        [SetUp]
        public void SetUp()
        {
            _pc = MakePlayerConfig();
            _tc = TeamTestFactory.MakeTeamConfig();
            _mc = MakeConfig();
            _sim = new MatchSimulator(_mc, _tc, _pc);
        }

        private MatchLineup Side(long idBase, float fill = 50f, MatchTactic tactic = MatchTactic.Develop, float stress = 0f,
            string traitA = TeamTestFactory.NeutralA)
            => MakeLineup(MakePlayers(_pc, idBase, fill: fill, stress: stress, traitA: traitA), "C" + idBase, 0f, tactic);

        private static string Signature(MatchResult r) =>
            string.Join(",", r.Games.Select(g => (g.WinnerIsA ? "A" : "B") + g.WinProbabilityA.ToString("R")));

        // ------------------------------------------------------------ simulator
        [Test]
        public void Simulate_SameSeed_SameResult()
        {
            var r1 = _sim.Simulate(Side(0, 55f), Side(100, 50f), new SeededRandomSource(Seed));
            var r2 = _sim.Simulate(Side(0, 55f), Side(100, 50f), new SeededRandomSource(Seed));
            Assert.AreEqual(Signature(r1), Signature(r2));
        }

        [Test]
        public void Simulate_SeriesEndsAtGamesToWin_AndScoresMatchGames()
        {
            var rng = new SeededRandomSource(Seed);
            for (int i = 0; i < 50; i++)
            {
                var r = _sim.Simulate(Side(0), Side(100), rng);
                Assert.AreEqual(2, Math.Max(r.ScoreA, r.ScoreB));
                Assert.Less(Math.Min(r.ScoreA, r.ScoreB), 2);
                Assert.AreEqual(r.Games.Count, r.ScoreA + r.ScoreB);
                Assert.AreEqual(r.ScoreA > r.ScoreB, r.WinnerIsA);
            }
        }

        [Test]
        public void Simulate_DrawsThreeFloatsPerGame()
        {
            var rng = new ScriptedRandomSource(0.5f); // eps 0, roll 0.5 vs p 0.5 => B wins every game
            var r = _sim.Simulate(Side(0), Side(100), rng);
            Assert.AreEqual(0, r.ScoreA);
            Assert.AreEqual(2, r.ScoreB);
            Assert.AreEqual(6, rng.Draws);
        }

        [Test]
        public void Simulate_IncompleteLineup_ThrowsBeforeAnyDraw()
        {
            var map = new Dictionary<string, Player>(Side(0).StartersByLane);
            map.Remove("adc");
            var rng = new ScriptedRandomSource(0.5f);
            Assert.Throws<InvalidOperationException>(() => _sim.Simulate(new MatchLineup("X", map, 0f, MatchTactic.Develop), Side(100), rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void Simulate_SamePlayerOnBothSides_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _sim.Simulate(Side(0), Side(3), new ScriptedRandomSource(0.5f)));
        }

        [Test]
        public void Simulate_WinProbabilityAlwaysClamped()
        {
            var rng = new SeededRandomSource(Seed);
            for (int i = 0; i < 100; i++)
            {
                var r = _sim.Simulate(Side(0, 100f), Side(100, 5f), rng);
                foreach (var g in r.Games)
                {
                    Assert.GreaterOrEqual(g.WinProbabilityA, 0.1f - 1e-6f);
                    Assert.LessOrEqual(g.WinProbabilityA, 0.9f + 1e-6f);
                }
            }
        }

        private int SeriesWinsOfA(MatchLineup a, MatchLineup b)
        {
            var rng = new SeededRandomSource(Seed);
            int wins = 0;
            for (int i = 0; i < SampleSize; i++) if (_sim.Simulate(a, b, rng).WinnerIsA) wins++;
            return wins;
        }

        [Test]
        public void Simulate_StrongerTeam_WinsClearlyMore()
        {
            // fill 70 vs 50: power gap ~10, p ~0.78 per game, ~0.87 per series.
            int wins = SeriesWinsOfA(Side(0, 70f), Side(100, 50f));
            Assert.Greater(wins, SampleSize * 0.78, "stronger side won only " + wins + "/" + SampleSize);
        }

        [Test]
        public void Simulate_EqualTeams_AboutHalf()
        {
            int wins = SeriesWinsOfA(Side(0), Side(100));
            Assert.That(wins, Is.InRange((int)(SampleSize * 0.42), (int)(SampleSize * 0.58)));
        }

        [Test]
        public void Simulate_TacticCounter_HelpsWhicheverSideCounters()
        {
            int aCounters = SeriesWinsOfA(Side(0, tactic: MatchTactic.Invade), Side(100, tactic: MatchTactic.Develop));
            int bCounters = SeriesWinsOfA(Side(0, tactic: MatchTactic.Develop), Side(100, tactic: MatchTactic.Invade));
            Assert.Greater(aCounters, SampleSize * 0.58, "A countering should win more");
            Assert.Less(bCounters, SampleSize * 0.42, "B countering should win more");
        }

        [Test]
        public void Simulate_Decider_AppliesDecisiveAndHesitant()
        {
            var a = Side(0, traitA: "decisive");
            var b = Side(100, traitA: "hesitant");
            // game 1: A wins (roll 0), game 2: B wins (roll 1), game 3 decider: roll 0.5
            var rng = new ScriptedRandomSource(0.5f, 0.5f, 0.5f, 0f, 0.5f, 0.5f, 1f);
            var r = _sim.Simulate(a, b, rng);
            Assert.AreEqual(3, r.Games.Count);
            Assert.IsFalse(r.Games[0].WasDecider);
            Assert.IsFalse(r.Games[1].WasDecider);
            Assert.IsTrue(r.Games[2].WasDecider);
            Assert.AreEqual(0.5f, r.Games[0].WinProbabilityA, 1e-6f);
            float pa = r.SideA.Evaluation.Power * 1.03f, pb = r.SideB.Evaluation.Power * 0.97f;
            Assert.AreEqual(MatchFormulas.WinProbability(_mc, pa, pb), r.Games[2].WinProbabilityA, 1e-5f);
            Assert.IsTrue(r.WinnerIsA);
        }

        [Test]
        public void Simulate_SnapshotsParticipants_IndependentOfLaterRosterChanges()
        {
            var ps = MakePlayers(_pc, 0);
            var lineup = MakeLineup(ps);
            var r = _sim.Simulate(lineup, Side(100), new SeededRandomSource(Seed));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5 }, r.SideA.Participants.Select(p => p.PlayerId).ToArray());
            CollectionAssert.AreEqual(Lanes, r.SideA.Participants.Select(p => p.LaneId).ToArray());
            Assert.IsTrue(r.SideB.Played(101));
            Assert.IsFalse(r.SideB.Played(1));
        }

        // ------------------------------------------------------------ report
        [Test]
        public void Report_AlwaysTwoToThreeReasons_WinAndLoss()
        {
            var rng = new SeededRandomSource(Seed);
            for (int i = 0; i < 40; i++)
            {
                var r = _sim.Simulate(Side(0, 45f + i % 10, (MatchTactic)(i % 3), stress: i), Side(100, 50f, MatchTactic.Develop), rng);
                foreach (bool viewerA in new[] { true, false })
                {
                    var rep = MatchReport.Build(r, _tc, _mc, viewerA);
                    Assert.That(rep.Reasons.Count, Is.InRange(2, 3));
                    for (int k = 1; k < rep.Reasons.Count; k++)
                        Assert.GreaterOrEqual(Math.Abs(rep.Reasons[k - 1].Value), Math.Abs(rep.Reasons[k].Value));
                }
            }
        }

        [Test]
        public void Report_IdenticalSides_StillMinReasons()
        {
            var r = _sim.Simulate(Side(0), Side(100), new SeededRandomSource(Seed));
            Assert.AreEqual(2, MatchReport.Build(r, _tc, _mc, true).Reasons.Count);
        }

        [Test]
        public void Report_SkillGap_IsTopReasonWithOppositeSigns()
        {
            var r = _sim.Simulate(Side(0, 70f), Side(100, 50f), new SeededRandomSource(Seed));
            var mine = MatchReport.Build(r, _tc, _mc, true);
            var theirs = MatchReport.Build(r, _tc, _mc, false);
            Assert.AreEqual(MatchFactor.Skill, mine.Reasons[0].Factor);
            Assert.Greater(mine.Reasons[0].Value, 0f);
            Assert.AreEqual(MatchFactor.Skill, theirs.Reasons[0].Factor);
            Assert.AreEqual(-mine.Reasons[0].Value, theirs.Reasons[0].Value, 1e-4f);
            Assert.IsTrue(theirs.Reasons[0].HasSubject);
            Assert.IsTrue(r.SideB.Played(theirs.Reasons[0].SubjectPlayerId));
            Assert.AreEqual(-mine.PowerGap, theirs.PowerGap, 1e-4f);
        }

        [Test]
        public void Report_TacticCounter_ReportedBothWays()
        {
            var r = _sim.Simulate(Side(0, tactic: MatchTactic.Teamfight), Side(100, tactic: MatchTactic.Invade), new SeededRandomSource(Seed));
            var a = MatchReport.Build(r, _tc, _mc, true).Reasons.First(x => x.Factor == MatchFactor.Tactic);
            var b = MatchReport.Build(r, _tc, _mc, false).Reasons.First(x => x.Factor == MatchFactor.Tactic);
            Assert.AreEqual(4f, a.Value, 1e-5f);
            Assert.AreEqual(-4f, b.Value, 1e-5f);
        }

        [Test]
        public void Report_HighStress_NamesMostStressedStarter()
        {
            var ps = MakePlayers(_pc, 0, stress: 0f);
            ps[2].Stress = 100f; // mid
            var r = _sim.Simulate(MakeLineup(ps), Side(100), new SeededRandomSource(Seed));
            var reasons = MatchReport.Build(r, _tc, _mc, true).Reasons;
            var vol = reasons.FirstOrDefault(x => x.Factor == MatchFactor.StressVolatility);
            var skill = reasons.First(x => x.Factor == MatchFactor.Skill);
            Assert.Less(skill.Value, 0f);
            Assert.AreEqual(3L, skill.SubjectPlayerId, "the mid laner has the biggest rating gap");
            if (vol.Factor == MatchFactor.StressVolatility && vol.Value < 0f) Assert.AreEqual(3L, vol.SubjectPlayerId);
        }

        [Test]
        public void Report_SkillSubject_AheadFlagComparesSameLane()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f);
            ps[2] = MakePlayers(_pc, 0, fill: 95f)[2]; // strong mid, id 3
            var r = _sim.Simulate(MakeLineup(ps), Side(100), new SeededRandomSource(Seed));
            var mine = MatchReport.Build(r, _tc, _mc, true).Reasons.First(x => x.Factor == MatchFactor.Skill);
            Assert.AreEqual(3L, mine.SubjectPlayerId);
            Assert.IsTrue(mine.SubjectAhead, "our mid beat their mid");
            var theirs = MatchReport.Build(r, _tc, _mc, false).Reasons.First(x => x.Factor == MatchFactor.Skill);
            Assert.IsTrue(theirs.HasSubject);
            Assert.IsFalse(theirs.SubjectAhead, "their mid lost to ours");
        }

        [Test]
        public void Report_EvenFlag_MatchesConfigThreshold()
        {
            var r = _sim.Simulate(Side(0, tactic: MatchTactic.Develop), Side(100, tactic: MatchTactic.Develop), new SeededRandomSource(Seed));
            foreach (var x in MatchReport.Build(r, _tc, _mc, true).Reasons)
                Assert.AreEqual(System.Math.Abs(x.Value) < _mc.report.evenThreshold, x.IsEven, x.Factor + " " + x.Value);
            var tactic = MatchReport.Build(r, _tc, _mc, true).Reasons.FirstOrDefault(x => x.Factor == MatchFactor.Tactic);
            if (tactic.Factor == MatchFactor.Tactic) Assert.IsTrue(tactic.IsEven, "same tactic: no counter");
        }

        // ------------------------------------------------------------ standings
        [Test]
        public void Standings_OrdersBySeriesWinsThenGameDiffThenGamesWonThenIndex()
        {
            var s = new Standings(4);
            s.Record(1, 2, 2, 0); // 1: 1W +2
            s.Record(3, 0, 2, 1); // 3: 1W +1
            s.Record(0, 2, 2, 1); // 0: 1W1L 0, 2: 0W2L
            var order = s.Ranking().Select(e => e.ClubIndex).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 3, 0, 2 }, order);
            Assert.AreEqual(1, s.RankOf(1));
            Assert.AreEqual(4, s.RankOf(2));
            Assert.AreEqual(-3, s.GetEntry(2).GameDiff);
        }

        [Test]
        public void Standings_FullTie_LowerIndexFirst_AndGamesWonBreaksDiffTie()
        {
            var s = new Standings(4);
            Assert.AreEqual(new[] { 0, 1, 2, 3 }, s.Ranking().Select(e => e.ClubIndex).ToArray());
            s.Record(1, 0, 2, 0); // 1: 1W, +2
            s.Record(3, 1, 2, 1); // 1: 1W1L, won 3 lost 2 (+1); 3: 1W, +1
            s.Record(2, 3, 2, 1); // 2: 1W, won 2 lost 1 (+1); 3: 1W1L, won 3 lost 3 (0)
            // 1 and 2: one series win, +1 each; 1 won more games
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, s.Ranking().Select(e => e.ClubIndex).ToArray());
        }

        [Test]
        public void Standings_AccumulatesAndRejectsBadInput()
        {
            var s = new Standings(2);
            s.Record(0, 1, 2, 1);
            s.Record(0, 1, 0, 2);
            var e = s.GetEntry(0);
            Assert.AreEqual(1, e.SeriesWins); Assert.AreEqual(1, e.SeriesLosses);
            Assert.AreEqual(2, e.GamesWon); Assert.AreEqual(3, e.GamesLost);
            Assert.Throws<ArgumentException>(() => s.Record(0, 0, 2, 1));
            Assert.Throws<ArgumentException>(() => s.Record(0, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Record(0, 5, 2, 1));
        }

        // ------------------------------------------------------------ league (shipped player / team / match configs)
        private static PlayerConfig RealPlayers() => PlayerConfigLoader.FromJson(File.ReadAllText("Assets/Data/player_config.json"));
        private static TeamConfig RealTeam() => TeamConfigLoader.FromJson(File.ReadAllText("Assets/Data/team_config.json"));
        private static MatchConfig RealMatch() => MatchConfigLoader.FromJson(File.ReadAllText("Assets/Data/match_config.json"));

        private static League BuildLeague(int seed, MatchConfig mc = null, PlayerConfig pc = null, long firstId = 1000)
        {
            pc = pc ?? RealPlayers();
            return LeagueFactory.Build(mc ?? RealMatch(), RealTeam(), pc, new PlayerGenerator(pc, new SeededRandomSource(seed), firstId), seed, "Demo");
        }

        [Test]
        public void League_HasOpponentCountClubs_WithDistinctIdsAndRosters5To7()
        {
            var league = BuildLeague(Seed);
            Assert.AreEqual(8, league.ClubCount);
            var ids = new HashSet<long>();
            var names = new HashSet<string>();
            foreach (var club in league.Opponents)
            {
                Assert.IsTrue(names.Add(club.Name));
                Assert.That(club.Team.Roster.Count, Is.InRange(5, 7));
                Assert.AreEqual(5, club.Team.Starters().Count);
                Assert.Greater(club.Team.Chemistry(), 0f);
                foreach (var p in club.Team.Roster) Assert.IsTrue(ids.Add(p.Id), "duplicate id " + p.Id);
            }
        }

        [Test]
        public void League_SameSeed_SameScheduleAndRosters()
        {
            var a = BuildLeague(Seed);
            var b = BuildLeague(Seed);
            for (int r = 0; r < a.RoundsPerCycle; r++) Assert.AreEqual(a.PlayerOpponentOfRound(r), b.PlayerOpponentOfRound(r));
            for (int i = 0; i < a.Opponents.Count; i++)
            {
                Assert.AreEqual(a.Opponents[i].Tactic, b.Opponents[i].Tactic);
                CollectionAssert.AreEqual(a.Opponents[i].Team.Roster.Select(p => p.Id), b.Opponents[i].Team.Roster.Select(p => p.Id));
            }
        }

        [Test]
        public void Schedule_EveryRoundEveryClubOnce_PlayerMeetsEachOpponentOncePerCycle()
        {
            var league = BuildLeague(Seed);
            var met = new int[league.ClubCount];
            for (int r = 0; r < league.RoundsPerCycle; r++)
            {
                var pairs = league.PairingsOfRound(r);
                Assert.AreEqual(4, pairs.Count);
                var seen = new HashSet<int>();
                foreach (var p in pairs) { Assert.IsTrue(seen.Add(p.ClubA)); Assert.IsTrue(seen.Add(p.ClubB)); Assert.Less(p.ClubA, p.ClubB); }
                met[league.PlayerOpponentOfRound(r)]++;
            }
            Assert.AreEqual(7, league.RoundsPerCycle);
            for (int c = 1; c < league.ClubCount; c++) Assert.AreEqual(1, met[c], "opponent " + c);
        }

        [Test]
        public void Schedule_OddClubCount_OneByePerRound()
        {
            var mc = RealMatch();
            mc.series.opponentCount = 6;
            mc.format.finalsPoints = new[] { 10, 7, 5, 5, 3, 2, 1 };
            mc.format.splitPrizes = new long[] { 80000, 50000, 30000, 30000, 10000, 10000, 10000 };
            var league = BuildLeague(Seed, mc);
            Assert.AreEqual(7, league.RoundsPerCycle);
            int byes = 0;
            var met = new int[league.ClubCount];
            for (int r = 0; r < 7; r++)
            {
                Assert.AreEqual(3, league.PairingsOfRound(r).Count);
                int o = league.PlayerOpponentOfRound(r);
                if (o < 0) byes++; else met[o]++;
            }
            Assert.AreEqual(1, byes);
            for (int c = 1; c < league.ClubCount; c++) Assert.AreEqual(1, met[c]);
        }

        [Test]
        public void Opponents_AreComparableToPlayerStartingTeam()
        {
            // Player start as GameSession builds it: best main-lane player per lane of one pool, own mood / stress.
            var pc = RealPlayers();
            var tc = RealTeam();
            var mc = RealMatch();
            double playerSkill = 0, oppSkill = 0;
            const int seeds = 10;
            for (int s = 0; s < seeds; s++)
            {
                var pool = new PlayerGenerator(pc, new SeededRandomSource(s)).GeneratePool();
                var map = new Dictionary<string, Player>();
                foreach (var lane in pc.lanes)
                    map[lane.id] = pool.Where(p => p.MainLane == lane.id).OrderByDescending(p => p.GetLaneScore(lane.id)).First();
                playerSkill += MatchFormulas.SkillScore(tc, pc, new MatchLineup("P", map, 0f, MatchTactic.Develop));
                var league = BuildLeague(s, mc, pc);
                foreach (var club in league.Opponents) oppSkill += MatchFormulas.SkillScore(tc, pc, MatchLineup.FromOpponent(club, mc)) / league.Opponents.Count;
            }
            double ratio = oppSkill / playerSkill;
            TestContext.WriteLine("player skill " + playerSkill / seeds + ", opponent skill " + oppSkill / seeds + ", ratio " + ratio);
            Assert.That(ratio, Is.InRange(0.75, 1.05));
        }
    }
}
