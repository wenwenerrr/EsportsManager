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
    /// <summary>Story 004: standings and the league schedule (series simulation tests: MatchSimulationTests, Story 011).</summary>
    public class MatchSimulationAndLeagueTests
    {
        private const int Seed = 4242;
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
            var sim = new MatchSimulator(mc, tc, pc);
            double Mean(MatchLineup l) { var e = sim.Preview(l); return (e.Strength[0] + e.Strength[1] + e.Strength[2]) / 3.0; }
            for (int s = 0; s < seeds; s++)
            {
                var pool = new PlayerGenerator(pc, new SeededRandomSource(s)).GeneratePool();
                var map = new Dictionary<string, Player>();
                foreach (var lane in pc.lanes)
                    map[lane.id] = pool.Where(p => p.MainLane == lane.id).OrderByDescending(p => p.GetLaneScore(lane.id)).First();
                playerSkill += Mean(new MatchLineup("P", map, 50f, MatchTactic.Develop));
                var league = BuildLeague(s, mc, pc);
                foreach (var club in league.Opponents) oppSkill += Mean(MatchLineup.FromOpponent(club)) / league.Opponents.Count;
            }
            double ratio = oppSkill / playerSkill;
            TestContext.WriteLine("player phase strength " + playerSkill / seeds + ", opponent " + oppSkill / seeds + ", ratio " + ratio);
            Assert.That(ratio, Is.InRange(0.75, 1.05));
        }
    }
}
