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
    /// <summary>Story 004: opponent form, familiarity / sponsorship feedback, lineup toggling (shipped configs for sessions).</summary>
    public class MatchIntegrationTests
    {
        private const float Tol = 1e-3f;
        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);
        private static GameSession Make(int seed) => GameSession.FromJson(Data("player_config.json"), Data("team_config.json"),
            Data("segment_config.json"), Data("match_config.json"), seed);

        // ------------------------------------------------------------ form (numbers.md 4.14 v3.3)

        [Test]
        public void Form_RandomWalk_StaysInBounds_CeilingGrowsWithPopularity()
        {
            var mc = MatchTestFactory.MakeConfig();
            var pc = MatchTestFactory.MakePlayerConfig();
            var tc = TeamTestFactory.MakeTeamConfig();
            var lo = new OpponentClub("Lo", new Team(tc, pc, new StubRandomSource(0.5f)), MatchTactic.Invade, 0f);
            var hi = new OpponentClub("Hi", new Team(tc, pc, new StubRandomSource(0.5f)), MatchTactic.Invade, 100f);
            var league = new League(mc, "P", new[] { lo, hi });
            var rng = new SeededRandomSource(7);
            float loMax = float.MinValue, hiMax = float.MinValue, min = float.MaxValue;
            for (int i = 0; i < 2000; i++)
            {
                league.AdvanceForm(rng);
                Assert.That(lo.Form, Is.InRange(-4f, 4f));
                Assert.That(hi.Form, Is.InRange(-4f, 12f));
                loMax = Math.Max(loMax, lo.Form); hiMax = Math.Max(hiMax, hi.Form); min = Math.Min(min, Math.Min(lo.Form, hi.Form));
            }
            Assert.Greater(hiMax, 10f, "a popular club reaches a higher form");
            Assert.LessOrEqual(loMax, 4f);
            Assert.Less(min, -3f);
            Assert.AreEqual(4f + 8f * 0.5f, mc.opponent.FormUpper(50f), Tol);
        }

        [Test]
        public void Form_StartsAtZero_PopularityInConfigRange_FactoryDeterministic()
        {
            var pc = PlayerConfigLoader.FromJson(Data("player_config.json"));
            var mc = MatchConfigLoader.FromJson(Data("match_config.json"));
            var tc = TeamConfigLoader.FromJson(Data("team_config.json"));
            League Build() => LeagueFactory.Build(mc, tc, pc, new PlayerGenerator(pc, new SeededRandomSource(5), 1000), 5, "P");
            var a = Build(); var b = Build();
            for (int i = 0; i < a.Opponents.Count; i++)
            {
                Assert.AreEqual(0f, a.Opponents[i].Form);
                Assert.That(a.Opponents[i].Popularity, Is.InRange(mc.opponent.popMin, mc.opponent.popMax));
                Assert.AreEqual(a.Opponents[i].Popularity, b.Opponents[i].Popularity);
            }
            Assert.Greater(a.Opponents.Select(o => o.Popularity).Distinct().Count(), 1);
        }

        [Test]
        public void Form_AddsToOpponentSkill_AndReportListsOpponentForm()
        {
            var pc = MatchTestFactory.MakePlayerConfig();
            var tc = TeamTestFactory.MakeTeamConfig();
            var mc = MatchTestFactory.MakeConfig();
            var mine = MatchTestFactory.MakeLineup(MatchTestFactory.MakePlayers(pc, 0));
            var theirsPlayers = MatchTestFactory.MakePlayers(pc, 100);
            var neutral = MatchTestFactory.MakeLineup(theirsPlayers);
            var hot = new MatchLineup("X", neutral.StartersByLane, 0f, MatchTactic.Develop, false, 50f, 50f, 8f);
            float baseSkill = MatchFormulas.SkillScore(tc, pc, neutral);
            Assert.AreEqual(baseSkill + 8f, MatchFormulas.SkillScore(tc, pc, hot), Tol);

            var r = MatchTestFactory.MakeSimulator(mc, pc, tc).Simulate(mine, hot, new SeededRandomSource(1));
            Assert.AreEqual(baseSkill + 8f, r.SideB.Evaluation.Skill, Tol);
            Assert.AreEqual(8f, r.SideB.Evaluation.Form, Tol);
            var rep = MatchReport.Build(r, tc, mc, true);
            var form = rep.Reasons.First(x => x.Factor == MatchFactor.OpponentForm);
            Assert.AreEqual(-3f / 5f * 8f, form.Value, Tol); // opponent hot -> negative for the viewer
            Assert.AreEqual(-4.8f, rep.PowerGap, Tol);
            // no form -> no OpponentForm reason
            var r0 = MatchTestFactory.MakeSimulator(mc, pc, tc).Simulate(mine, neutral, new SeededRandomSource(1));
            Assert.IsFalse(MatchReport.Build(r0, tc, mc, true).Reasons.Any(x => x.Factor == MatchFactor.OpponentForm));
        }

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
