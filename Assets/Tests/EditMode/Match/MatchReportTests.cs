using System.Linq;
using EsportsManager.Matches;
using EsportsManager.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 011: report reason rules 1-6 (design/gdd/match.md "战报数据", AC 40).</summary>
    public class MatchReportTests
    {
        private static GameComponents Game(bool aWon, double p0, double[] values, double[] cycle = null)
            => new GameComponents { AWon = aWon, P0 = p0, Values = values, Cycle = cycle ?? new[] { 1.0, 1.0, 1.0 } };

        [Test]
        public void ReasonsFixture_Bo3_LuckFirstThenEarlyAndTactic()
        {
            var fx = LoadReportFixture();
            var games = fx.games.Select(g => Game(g.a_won, g.p0, g.values.Select(v => (double)v).ToArray(), g.cycle.Select(v => (double)v).ToArray())).ToList();
            var rep = MatchReport.Select(games, fx.a_won_series, true, MakeConfig().report);

            Assert.AreEqual(3, rep.Reasons.Count);
            Assert.IsTrue(rep.Reasons[0].IsLuckGame);
            Assert.AreEqual(fx.expected_luck_game, rep.Reasons[0].GameIndex);
            Assert.IsTrue(rep.Reasons[0].ViewerUnlucky);
            Assert.AreEqual(MatchFactor.Early, rep.Reasons[1].Factor);
            Assert.AreEqual(6.0f, rep.Reasons[1].Value, 1e-5f);
            Assert.AreEqual(MatchFactor.Tactic, rep.Reasons[2].Factor);
            Assert.AreEqual(1.5f, rep.Reasons[2].Value, 1e-5f);
            Assert.AreEqual(fx.expected_even, rep.IsEven);
            Assert.AreEqual(0.583f, rep.MeanP0, 0.001f);
            Assert.AreEqual(fx.expected_cycle, rep.CycleTriggered);
            Assert.AreEqual(MatchPhase.Late, rep.CyclePhase);
        }

        [Test]
        public void Reasons_FromLoserView_AreTheSameFactorsWithFlippedSigns()
        {
            var fx = LoadReportFixture();
            var games = fx.games.Select(g => Game(g.a_won, g.p0, g.values.Select(v => (double)v).ToArray(), g.cycle.Select(v => (double)v).ToArray())).ToList();
            var rep = MatchReport.Select(games, true, false, MakeConfig().report);
            Assert.IsTrue(rep.Reasons[0].IsLuckGame);
            Assert.IsFalse(rep.Reasons[0].ViewerUnlucky, "B won game 2 as the underdog");
            Assert.AreEqual(-6.0f, rep.Reasons[1].Value, 1e-5f);
            Assert.AreEqual(1f - 0.583f, rep.MeanP0, 0.001f);
        }

        [Test]
        public void Reasons_NoLuck_FillsToMinWithLargestRemaining()
        {
            // A wins 2:0; only one factor favours A by >= 0.5, so the largest other one fills up to min_reasons (2).
            var v1 = new double[] { 0.2, -2.0, 0.1, 0, 0, 0.6, 0, 0.1 };
            var games = new[] { Game(true, 0.52, v1), Game(true, 0.52, v1) }.ToList();
            var rep = MatchReport.Select(games, true, true, MakeConfig().report);
            Assert.AreEqual(2, rep.Reasons.Count);
            Assert.AreEqual(MatchFactor.Tactic, rep.Reasons[0].Factor);
            Assert.AreEqual(MatchFactor.Mid, rep.Reasons[1].Factor, "padding takes the largest |value| even against the winner");
            Assert.IsTrue(rep.IsEven, "mean p0 0.52 is within 0.5 +- 0.05");
            Assert.IsFalse(rep.CycleTriggered);
        }

        [Test]
        public void Reasons_AtMostMaxReasons()
        {
            var v = new double[] { 3, 2, 1.5, 1, 0.9, 0.8, 0.7, 0.6 };
            var rep = MatchReport.Select(new[] { Game(true, 0.7, v) }.ToList(), true, true, MakeConfig().report);
            Assert.AreEqual(3, rep.Reasons.Count);
            CollectionAssert.AreEqual(new[] { MatchFactor.Early, MatchFactor.Mid, MatchFactor.Late }, rep.Reasons.Select(r => r.Factor).ToArray());
        }

        [Test]
        public void Build_PlayedSeries_TotalsSumAndTacticAdvice()
        {
            var pc = MakePlayerConfig();
            var mc = MakeConfig();
            var sim = MakeSimulator(mc, pc);
            var players = LoadExamplePlayers(pc, out float ca, out float cb);
            // A picks develop although it leads early: invade would be ~0.5+ points better (worked example table).
            var r = sim.Simulate(new MatchLineup("A", Map(players[0]), ca, MatchTactic.Develop), new MatchLineup("B", Map(players[1]), cb, MatchTactic.Develop),
                new SeededRandomSource(3), 5);
            var rep = MatchReport.Build(r, mc, true);
            Assert.That(rep.Reasons.Count, Is.InRange(2, 3));
            double g = r.Games.Sum(x => x.G);
            double parts = rep.Totals[(int)MatchFactor.Early] + rep.Totals[(int)MatchFactor.Mid] + rep.Totals[(int)MatchFactor.Late]
                + rep.Totals[(int)MatchFactor.Star] + rep.Totals[(int)MatchFactor.Cycle] + rep.Totals[(int)MatchFactor.Tactic] + rep.Totals[(int)MatchFactor.Luck];
            Assert.AreEqual(g, parts, 1e-4);
            Assert.IsTrue(rep.Advice.HasValue);
            Assert.AreEqual(MatchTactic.Invade, rep.Advice.Value.Tactic);
            Assert.GreaterOrEqual(rep.Advice.Value.GainPoints, 0.5f);
            Assert.IsTrue(rep.CycleTriggered, "the example's early lead weakens B's late lead");
            Assert.AreEqual(MatchPhase.Late, rep.CyclePhase);
            Assert.IsTrue(rep.CycleByViewer);
            var bView = MatchReport.Build(r, mc, false);
            Assert.IsFalse(bView.Advice.HasValue, "the counterfactual is about side A's tactics");
        }

        [Test]
        public void Build_ActualTacticBest_NoAdvice()
        {
            var pc = MakePlayerConfig();
            var mc = MakeConfig();
            var players = LoadExamplePlayers(pc, out float ca, out float cb);
            var r = MakeSimulator(mc, pc).Simulate(new MatchLineup("A", Map(players[0]), ca, MatchTactic.Invade),
                new MatchLineup("B", Map(players[1]), cb, MatchTactic.Develop), new SeededRandomSource(3), 3);
            Assert.IsFalse(MatchReport.Build(r, mc, true).Advice.HasValue);
        }
    }
}
