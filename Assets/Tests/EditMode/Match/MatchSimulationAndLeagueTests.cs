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

        private static League BuildLeague(int clubs)
        {
            var names = Enumerable.Range(0, clubs).Select(i => "C" + i).ToList();
            var order = Enumerable.Range(1, clubs % 2 == 0 ? clubs - 1 : clubs).Reverse().ToList();
            return new League(names, clubs % 2 == 0 ? order : order.Where(c => c < clubs).ToList()) { PlayerClub = 0 };
        }

        [Test]
        public void Schedule_EveryRoundEveryClubOnce_PlayerMeetsEachOpponentOncePerCycle()
        {
            var league = BuildLeague(8);
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
        public void Schedule_PlayerClubAnyIndex_MeetsEveryOtherClubOnce()
        {
            var league = BuildLeague(8);
            league.PlayerClub = 5;
            var met = new int[8];
            for (int r = 0; r < league.RoundsPerCycle; r++) met[league.PlayerOpponentOfRound(r)]++;
            for (int c = 0; c < 8; c++) Assert.AreEqual(c == 5 ? 0 : 1, met[c], "club " + c);
            league.PlayerClub = League.NoClub;
            Assert.AreEqual(-1, league.PlayerOpponentOfRound(0));
        }

        [Test]
        public void Schedule_OddClubCount_OneByePerRound()
        {
            var league = BuildLeague(7);
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
        public void Schedule_BadRotation_Throws()
        {
            Assert.Throws<ArgumentException>(() => new League(new[] { "a", "b", "c", "d" }, new[] { 1, 1, 2 }));
        }
    }
}
