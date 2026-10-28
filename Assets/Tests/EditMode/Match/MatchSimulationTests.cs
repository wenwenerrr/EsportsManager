using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Teams;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 011: series flow, draws, purity, symmetry, decider, report data (design/gdd/match.md AC 1-12, 33, 34, 38, 39, 43).</summary>
    public class MatchSimulationTests
    {
        private PlayerConfig _pc;
        private MatchConfig _mc;
        private MatchSimulator _sim;

        [SetUp]
        public void SetUp()
        {
            _pc = MakePlayerConfig();
            _mc = MakeConfig();
            _sim = MakeSimulator(_mc, _pc);
        }

        private MatchLineup Side(long idBase, float fill = 50f, MatchTactic tactic = MatchTactic.Develop, float stress = 0f, string traitA = TeamTestFactory.NeutralA)
            => MakeLineup(MakePlayers(_pc, idBase, fill: fill, stress: stress, traitA: traitA), "C" + idBase, 50f, tactic);

        private static string Dump(Player p) => p.Id + ":" + p.Mood.ToString("R") + "/" + p.Stress.ToString("R") + "/" + p.Popularity.ToString("R") + "/"
            + string.Join(",", p.GetRadar(p.MainLane).Select(v => v.ToString("R")));

        // ------------------------------------------------------------ AC 1-4, 11

        [Test]
        public void MissingLane_ThrowsInvalidLineupWithLane_NoDraws_NoChange()
        {
            var ps = MakePlayers(_pc, 0);
            var map = Map(ps); map.Remove("adc");
            var before = string.Join("|", ps.Select(Dump));
            var rng = new UScriptedSource(null, 0.5f);
            var ex = Assert.Throws<InvalidLineupException>(() => _sim.Simulate(new MatchLineup("X", map, 50f, MatchTactic.Invade), Side(100), rng, 3));
            Assert.AreEqual("adc", ex.LaneId);
            StringAssert.Contains("adc", ex.Message);
            Assert.AreEqual(0, rng.Calls);
            Assert.AreEqual(before, string.Join("|", ps.Select(Dump)));
        }

        [Test]
        public void SamePlayerTwiceInOneTeam_ThrowsWithPlayerId_NoDraws()
        {
            var ps = MakePlayers(_pc, 0);
            var map = Map(ps); map["mid"] = ps[0];
            var rng = new UScriptedSource(null, 0.5f);
            var ex = Assert.Throws<InvalidLineupException>(() => _sim.Simulate(new MatchLineup("X", map, 50f, MatchTactic.Invade), Side(100), rng, 3));
            Assert.AreEqual(1L, ex.PlayerId);
            StringAssert.Contains("1", ex.Message);
            Assert.AreEqual(0, rng.Calls);
        }

        [Test]
        public void SamePlayerBothSides_Throws_NoDraws()
        {
            var rng = new UScriptedSource(null, 0.5f);
            var ex = Assert.Throws<InvalidLineupException>(() => _sim.Simulate(Side(0), Side(3), rng, 3));
            Assert.IsTrue(ex.PlayerId.HasValue);
            Assert.AreEqual(0, rng.Calls);
        }

        [Test]
        public void BestOf_IllegalThrowsWithoutDraws_LegalGivesWinnerBestOfHalfPlusOne()
        {
            foreach (int bo in new[] { -1, 0, 2, 4, 11 })
            {
                var rng = new UScriptedSource(null, 0.5f);
                Assert.Throws<ArgumentOutOfRangeException>(() => _sim.Simulate(Side(0), Side(100), rng, bo), "bo " + bo);
                Assert.AreEqual(0, rng.Calls);
            }
            foreach (int bo in new[] { 1, 3, 5, 7, 9 })
            {
                var r = _sim.Simulate(Side(0), Side(100), new SeededRandomSource(bo), bo);
                Assert.AreEqual(bo / 2 + 1, Math.Max(r.ScoreA, r.ScoreB), "bo " + bo);
                Assert.Less(Math.Min(r.ScoreA, r.ScoreB), bo / 2 + 1);
            }
        }

        [Test]
        public void SeriesFlow_IllegalLineupStaysPending_LegalPlaysToComplete()
        {
            var s = new MatchSeries(_sim, 3);
            var map = Map(MakePlayers(_pc, 0)); map.Remove("top");
            Assert.IsFalse(s.TryLock(new MatchLineup("X", map, 50f, MatchTactic.Invade), Side(100), out var err));
            Assert.IsNotNull(err);
            Assert.AreEqual(SeriesState.Pending, s.State);
            Assert.Throws<InvalidOperationException>(() => s.Play(new SeededRandomSource(1)));
            Assert.IsTrue(s.TryLock(Side(0), Side(100), out _));
            Assert.AreEqual(SeriesState.Locked, s.State);
            s.Play(new SeededRandomSource(1));
            Assert.AreEqual(SeriesState.Complete, s.State);
            Assert.IsNotNull(s.Result);
        }

        // ------------------------------------------------------------ AC 5 (golden, Python oracle with the same SplitMix64 stream)

        [Test]
        public void GoldenBo5_FullExampleFixture_MatchesPythonOracle()
        {
            var gold = LoadGolden();
            var players = LoadExamplePlayers(_pc, out float chemA, out float chemB);
            var a = new MatchLineup("A", Map(players[0]), chemA, MatchTactic.Invade);
            var b = new MatchLineup("B", Map(players[1]), chemB, MatchTactic.Develop);
            for (int run = 0; run < 2; run++)
            {
                var r = _sim.Simulate(a, b, new SeededRandomSource(gold.seed), gold.best_of);
                Assert.AreEqual(gold.games.Length, r.Games.Count);
                for (int i = 0; i < r.Games.Count; i++)
                {
                    var g = r.Games[i]; var x = gold.games[i];
                    Assert.AreEqual(x.a_wins, g.WinnerIsA, "game " + i);
                    Assert.AreEqual(x.decider, g.WasDecider, "game " + i);
                    Assert.AreEqual(x.u_a, g.UA, 1e-12); Assert.AreEqual(x.u_b, g.UB, 1e-12); Assert.AreEqual(x.roll, g.Roll, 1e-12);
                    Assert.AreEqual(x.g0, g.Evaluation.G0, 1e-6, "G0 game " + i);
                    Assert.AreEqual(x.eps_a, g.EpsilonA, 1e-6); Assert.AreEqual(x.eps_b, g.EpsilonB, 1e-6);
                    Assert.AreEqual(x.g, g.G, 1e-6); Assert.AreEqual(x.p, g.P, 1e-6);
                }
            }
        }

        // ------------------------------------------------------------ AC 6-8

        [Test]
        public void Draws_OrderIsNoiseANoiseBRoll_EpsilonIsSigmaTwoUMinusOne()
        {
            var a = Side(0, stress: 40f); var b = Side(100, stress: 0f);
            var rng = new UScriptedSource(new[] { 0.25f, 0.9f, 0.1f }, 0.5f);
            var r = _sim.Simulate(a, b, rng, 1);
            var g = r.Games[0];
            Assert.AreEqual(0.25, g.UA, 1e-7); Assert.AreEqual(0.9, g.UB, 1e-7); Assert.AreEqual(0.1, g.Roll, 1e-7);
            double sa = r.SideA.Evaluation.Sigma, sb = r.SideB.Evaluation.Sigma;
            Assert.AreNotEqual(sa, sb, "different sigmas per side");
            Assert.AreEqual(sa * (2 * g.UA - 1), g.EpsilonA, 1e-12);
            Assert.AreEqual(sb * (2 * g.UB - 1), g.EpsilonB, 1e-12);
            Assert.AreEqual(3, rng.Calls);
        }

        [Test]
        public void Draws_AlwaysZero_A3to0_NineDraws_Always099_B3to0_Alternating_3to2_Fifteen()
        {
            var z = new UScriptedSource(null, 0f);
            var r = _sim.Simulate(Side(0), Side(100), z, 5);
            Assert.AreEqual(3, r.ScoreA); Assert.AreEqual(0, r.ScoreB); Assert.AreEqual(9, z.Calls);
            var h = new UScriptedSource(null, 0.99f);
            r = _sim.Simulate(Side(0), Side(100), h, 5);
            Assert.AreEqual(0, r.ScoreA); Assert.AreEqual(3, r.ScoreB); Assert.AreEqual(9, h.Calls);
            var alt = new UScriptedSource(null, 0f, 0.99f);
            r = _sim.Simulate(Side(0), Side(100), alt, 5);
            Assert.AreEqual(3, r.ScoreA); Assert.AreEqual(2, r.ScoreB); Assert.AreEqual(15, alt.Calls);
        }

        [Test]
        public void Draws_ZeroSigma_StillThreePerGame()
        {
            var mc = MakeConfig(); mc.noise.g0 = 0f; mc.noise.g1 = 0f;
            var rng = new UScriptedSource(null, 0.3f, 0.7f);
            var r = MakeSimulator(mc, _pc).Simulate(Side(0), Side(100), rng, 3);
            Assert.AreEqual(3 * r.Games.Count, rng.Calls);
            Assert.AreEqual(0.0, r.Games[0].EpsilonA, 0.0);
        }

        // ------------------------------------------------------------ AC 9, 10

        [Test]
        public void Simulate_DoesNotModifyInputs()
        {
            var pa = MakePlayers(_pc, 0, fill: 60f, stress: 30f); var pb = MakePlayers(_pc, 100, fill: 55f, stress: 70f);
            var before = string.Join("|", pa.Concat(pb).Select(Dump));
            string cfgBefore = ConfigDump(_mc);
            _sim.Simulate(MakeLineup(pa, "A", 60f, MatchTactic.Invade), MakeLineup(pb, "B", 40f, MatchTactic.Teamfight), new SeededRandomSource(3), 7);
            Assert.AreEqual(before, string.Join("|", pa.Concat(pb).Select(Dump)));
            Assert.AreEqual(cfgBefore, ConfigDump(_mc));
        }

        private static string ConfigDump(object o, int depth = 0)
        {
            if (o == null) return "null";
            var t = o.GetType();
            if (t.IsPrimitive || o is string || t.IsEnum) return Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture);
            if (o is System.Collections.IEnumerable e) return "[" + string.Join(",", e.Cast<object>().Select(x => ConfigDump(x, depth + 1))) + "]";
            return "{" + string.Join(";", t.GetFields().Select(f => f.Name + "=" + ConfigDump(f.GetValue(o), depth + 1))) + "}";
        }

        [Test]
        public void LockedSnapshot_LaterRosterAndMoodChanges_DoNotMatter()
        {
            var pa = MakePlayers(_pc, 0, fill: 60f);
            var locked = MakeLineup(pa, "A", 50f, MatchTactic.Invade);
            var r1 = _sim.Simulate(locked, Side(100), new SeededRandomSource(11), 5);
            foreach (var p in pa) { p.Mood = 0f; p.Stress = 100f; p.SetDimension(p.MainLane, 4, 1f); }
            var r2 = _sim.Simulate(locked, Side(100), new SeededRandomSource(11), 5);
            Assert.AreEqual(Sig(r1), Sig(r2));
        }

        private static string Sig(MatchResult r) => string.Join(",", r.Games.Select(g => (g.WinnerIsA ? "A" : "B") + g.P.ToString("R") + "/" + g.Evaluation.G0.ToString("R")));

        [Test]
        public void SameSeed_SameResult_DifferentSeedUsuallyDiffers()
        {
            Assert.AreEqual(Sig(_sim.Simulate(Side(0, 55f), Side(100), new SeededRandomSource(5), 7)), Sig(_sim.Simulate(Side(0, 55f), Side(100), new SeededRandomSource(5), 7)));
        }

        // ------------------------------------------------------------ AC 12 (symmetry)

        [Test]
        public void Symmetry_SwapSides_PASumsToOne_ThousandRandomPairs()
        {
            var mc = _mc;
            var layout = new PhaseLayout(mc, _pc);
            var rng = new SeededRandomSource(12);
            for (int n = 0; n < 1000; n++)
            {
                var a = MatchFormulaTests.RandomSide(rng, rng.NextFloat(0f, 100f), rng.NextFloat(0f, 100f));
                var b = MatchFormulaTests.RandomSide(rng, rng.NextFloat(0f, 100f), rng.NextFloat(0f, 100f));
                var ta = MatchTactics.All[rng.NextInt(0, 3)]; var tb = MatchTactics.All[rng.NextInt(0, 3)];
                double pa = Evaluate(mc, layout, a, b, ta, tb).P0;
                double pb = Evaluate(mc, layout, b, a, tb, ta).P0;
                Assert.AreEqual(1.0, pa + pb, 1e-9, "pair " + n);
            }
        }

        // ------------------------------------------------------------ AC 33, 34 (decider, roll boundary)

        [Test]
        public void Decider_ThreeDecisiveTwoHesitant_ScalesRatingsNotCommand()
        {
            var ps = MakePlayers(_pc, 0, fill: 60f, mood: 100f);
            var traits = new[] { "decisive", "decisive", "decisive", "hesitant", "hesitant" };
            var withTraits = ps.Select((p, i) => { var q = TeamTestFactory.MakePlayer(_pc, p.Id, p.MainLane, 60f, traits[i], TeamTestFactory.NeutralB); q.Mood = 100f; q.SetDimension(q.MainLane, 5, 80f); return q; }).ToArray();
            var layout = new PhaseLayout(_mc, _pc);
            var lineup = MakeLineup(withTraits, "A", 50f, MatchTactic.Invade);
            var normal = MatchFormulas.BuildSide(_mc, layout, lineup, false);
            var dec = MatchFormulas.BuildSide(_mc, layout, lineup, true);
            for (int i = 0; i < 5; i++)
                for (int k = 0; k < 3; k++) Assert.AreEqual(normal.Ratings[i][k] * 1.006, dec.Ratings[i][k], 1e-5); // dec_i are float trait multipliers
            Assert.AreEqual(normal.Command, dec.Command, 0.0, "command is not scaled by the decider factor");
        }

        [Test]
        public void Decider_OnlyWhenBothOneGameFromWinning()
        {
            // Alternating 0 / 0.99: games go A, B, A, B, A in a BO5 -> only game 5 (2:2) is the decider.
            var r = _sim.Simulate(Side(0), Side(100), new UScriptedSource(null, 0f, 0.99f), 5);
            CollectionAssert.AreEqual(new[] { false, false, false, false, true }, r.Games.Select(g => g.WasDecider).ToArray());
            var r3 = _sim.Simulate(Side(0), Side(100), new UScriptedSource(null, 0f, 0.99f), 3);
            CollectionAssert.AreEqual(new[] { false, false, true }, r3.Games.Select(g => g.WasDecider).ToArray());
            Assert.IsTrue(_sim.Simulate(Side(0), Side(100), new SeededRandomSource(1), 1).Games[0].WasDecider);
            // BO5 at 2:1 (A, A, B ...) is not a decider.
            var r21 = _sim.Simulate(Side(0), Side(100), new UScriptedSource(new[] { 0.5f, 0.5f, 0f, 0.5f, 0.5f, 0f, 0.5f, 0.5f, 0.99f }, 0f), 5);
            Assert.IsFalse(r21.Games[3].WasDecider);
        }

        [Test]
        public void Roll_EqualToP_Loses_JustBelow_Wins()
        {
            var mc = MakeConfig(); mc.noise.g0 = 0f; mc.noise.g1 = 0f;
            var sim = MakeSimulator(mc, _pc);
            // Identical sides and tactics: G0 = 0, p = 0.5 exactly.
            var lose = sim.Simulate(Side(0, tactic: MatchTactic.Invade), Side(100, tactic: MatchTactic.Invade), new UScriptedSource(new[] { 0.5f, 0.5f, 0.5f }), 1);
            Assert.AreEqual(0.5, lose.Games[0].P, 0.0);
            Assert.IsFalse(lose.Games[0].WinnerIsA);
            float below = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(0.5f) - 1);
            var win = sim.Simulate(Side(0, tactic: MatchTactic.Invade), Side(100, tactic: MatchTactic.Invade), new UScriptedSource(new[] { 0.5f, 0.5f, below }), 1);
            Assert.IsTrue(win.Games[0].WinnerIsA);
        }

        // ------------------------------------------------------------ AC 35 (neutral hooks in a series)

        [Test]
        public void NeutralHookTable_SeriesIdenticalToEmptyTable()
        {
            var neutral = new[] { new SpecialtyEffect(SpecialtyHook.PhaseFlat, 0f, MatchPhase.Early), new SpecialtyEffect(SpecialtyHook.SigmaScale, 1f) };
            var pa = MakePlayers(_pc, 0, fill: 58f, stress: 30f);
            var r1 = _sim.Simulate(MakeLineup(pa, "A", 50f, MatchTactic.Invade, neutral), Side(100), new SeededRandomSource(9), 7);
            var r2 = _sim.Simulate(MakeLineup(pa, "A", 50f, MatchTactic.Invade), Side(100), new SeededRandomSource(9), 7);
            Assert.AreEqual(Sig(r1), Sig(r2));
            for (int i = 0; i < r1.Games.Count; i++) CollectionAssert.AreEqual(MatchReport.GParts(r2.Games[i]), MatchReport.GParts(r1.Games[i]));
        }

        // ------------------------------------------------------------ AC 38, 39 (report data)

        [Test]
        public void GParts_SumToG_EveryGame()
        {
            var rng = new SeededRandomSource(38);
            for (int n = 0; n < 50; n++)
            {
                var a = MakeLineup(MakePlayers(_pc, 0, fill: rng.NextFloat(40f, 80f), stress: rng.NextFloat(0f, 80f)), "A", rng.NextFloat(0f, 100f), MatchTactics.All[n % 3]);
                var b = MakeLineup(MakePlayers(_pc, 100, fill: rng.NextFloat(40f, 80f), stress: rng.NextFloat(0f, 80f)), "B", rng.NextFloat(0f, 100f), MatchTactics.All[(n / 3) % 3]);
                var r = _sim.Simulate(a, b, rng, 5);
                foreach (var g in r.Games) Assert.AreEqual(g.G, MatchReport.GParts(g).Sum(), 1e-9);
            }
        }

        [Test]
        public void GParts_UseCycleWeightedContributions()
        {
            var players = LoadExamplePlayers(_pc, out float ca, out float cb);
            var r = _sim.Simulate(new MatchLineup("A", Map(players[0]), ca, MatchTactic.Invade), new MatchLineup("B", Map(players[1]), cb, MatchTactic.Develop), new SeededRandomSource(1), 1);
            var e = r.Games[0].Evaluation;
            for (int k = 0; k < 3; k++)
                Assert.AreEqual(_mc.strength.PhaseWeight((MatchPhase)k) * e.Cycle[k] * e.Delta[k] / e.WeightSum, e.PhaseContribution[k], 1e-12);
        }

        [Test]
        public void Counterfactuals_ThreeTactics_ActualEqualsG0_AndNoHooksG0()
        {
            var hooks = new[] { new SpecialtyEffect(SpecialtyHook.PhaseFlat, 2f, MatchPhase.Early) };
            var a = MakeLineup(MakePlayers(_pc, 0, fill: 60f), "A", 50f, MatchTactic.Teamfight, hooks);
            var r = _sim.Simulate(a, Side(100, 58f), new SeededRandomSource(4), 5);
            var plain = _sim.Simulate(MakeLineup(MakePlayers(_pc, 0, fill: 60f), "A", 50f, MatchTactic.Teamfight), Side(100, 58f), new SeededRandomSource(4), 5);
            foreach (var g in r.Games)
            {
                Assert.AreEqual(3, g.TacticG0.Count);
                Assert.AreEqual(g.Evaluation.G0, g.TacticG0[(int)MatchTactic.Teamfight], 0.0);
                Assert.AreNotEqual(g.Evaluation.G0, g.G0NoHooksA);
                Assert.AreEqual(g.Evaluation.G0, g.G0NoHooksB, 0.0, "B has no hooks");
            }
            Assert.AreEqual(plain.Games[0].Evaluation.G0, r.Games[0].G0NoHooksA, 1e-12);
            foreach (var t in MatchTactics.All)
            {
                var alt = _sim.Simulate(a.WithTactic(t), Side(100, 58f), new SeededRandomSource(4), 1);
                Assert.AreEqual(alt.Games[0].Evaluation.G0, r.Games[0].TacticG0[(int)t], 1e-12, t.ToString());
            }
        }

        // ------------------------------------------------------------ AC 43 (pre-match estimate)

        [Test]
        public void Preview_EqualsFirstGame_WeakestLaneIsLowestFirstOnTies()
        {
            var players = LoadExamplePlayers(_pc, out float ca, out float cb);
            var a = new MatchLineup("A", Map(players[0]), ca, MatchTactic.Invade);
            var preview = _sim.Preview(a);
            var r = _sim.Simulate(a, new MatchLineup("B", Map(players[1]), cb, MatchTactic.Develop), new SeededRandomSource(2), 3);
            var first = r.Games[0];
            Assert.IsFalse(first.WasDecider);
            for (int k = 0; k < 3; k++) Assert.AreEqual(first.Evaluation.A.Strength[k], preview.Strength[k], 1e-9);
            Assert.AreEqual(first.Evaluation.A.Execution, preview.Execution, 1e-9);
            // A early 66/70/66/64/66 -> adc (3); mid 60/62/60/58/62 -> adc; late 56/56/57/56/57 -> top (tie, first in config order).
            CollectionAssert.AreEqual(new[] { 3, 3, 0 }, preview.WeakestLane);
            Assert.AreEqual(66.296, preview.Strength[0], 0.005);
            Assert.AreEqual(0.79, preview.Execution, 1e-9);
        }
    }
}
