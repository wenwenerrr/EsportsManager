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
    /// <summary>Story 004: familiarity / sponsorship feedback, lineup toggling (shipped configs for sessions). Opponent form was removed in Story 011.</summary>
    public class MatchIntegrationTests
    {
        private const float Tol = 1e-3f;
        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);
        private static GameSession Make(int seed) => GameSession.FromJson(Data("player_config.json"), Data("team_config.json"),
            Data("segment_config.json"), Data("match_config.json"), Data("league_config.json"), seed);

        // ------------------------------------------------------------ team feedback

        [Test]
        public void Familiarity_WinningLineups_GiveFWinPerWonSeriesToPairsWhoPlayed()
        {
            var pc = TeamTestFactory.MakePlayerConfig();
            var team = new Team(TeamTestFactory.MakeTeamConfig(), pc, new StubRandomSource(0.5f));
            team.SettleSegment(new SegmentInput
            {
                SegmentsPerYear = 12, MatchResults = new[] { true, true },
                WinningLineups = new List<IReadOnlyList<long>> { new long[] { 1, 2 }, new long[] { 1, 2, 3 } },
            });
            Assert.AreEqual(4f, team.GetFamiliarity(1, 2), Tol); // f_win 2 x two won series together
            Assert.AreEqual(2f, team.GetFamiliarity(2, 3), Tol);
            Assert.Throws<ArgumentException>(() => team.SettleSegment(new SegmentInput { SegmentsPerYear = 12, WinningLineups = new IReadOnlyList<long>[] { null } }));
        }

        [Test]
        public void Sponsorship_ReadsWinRateAfterThisSegmentsMatches()
        {
            var fx = new SegmentTestFactory.Fixture();
            Assert.AreEqual(100f, fx.Team.RecentWinRateWith(new[] { true }), Tol);
            Assert.AreEqual(fx.Team.RecentWinRate(), fx.Team.RecentWinRateWith(null), Tol);
            float pop = fx.Team.ClubPopularity;
            fx.Runner.BeginSegment();
            var s = fx.Runner.Resolve(new SegmentPlan().Sponsor(), SegmentTestFactory.Ctx(true));
            // stub noise 0.5 -> 0 spread; chance uses the post-match rate 100, not the pre-match default
            Assert.AreEqual(SegmentFormulas.SponsorChance(fx.Cfg, pop, 100f, 0f), s.SponsorAttempts[0].Chance, Tol);
        }

        // ------------------------------------------------------------ session (the calendar flow is in TournamentTests)

        [Test]
        public void ToggleStarter_BenchPlayerTakesHisMainLane_PreviousStarterBenched()
        {
            var s = Make(13);
            var bench = s.Team.Roster.First(p => s.Team.LaneOf(p) == null);
            var old = s.Team.GetStarter(bench.MainLane);
            Assert.IsTrue(s.ToggleStarter(bench.Id, out _));
            Assert.AreEqual(bench.Id, s.Team.GetStarter(bench.MainLane).Id);
            Assert.IsNull(s.Team.LaneOf(old));
            Assert.IsTrue(s.GetLineup().IsComplete);
            Assert.IsFalse(s.ToggleStarter(-99, out var err));
            Assert.AreEqual(PlanErrorCode.UnknownPlayer, err.Code);
        }
    }
}
