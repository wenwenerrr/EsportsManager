using System;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Tests.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 011: formulas F1-F14 of design/gdd/match.md rev 4 (AC 15-32, 35-37), noise-free, double precision.</summary>
    public class MatchFormulaTests
    {
        private const double Tol = 0.005;
        private MatchConfig _mc;
        private PlayerConfig _pc;
        private PhaseLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _mc = MakeConfig();
            _pc = MakePlayerConfig();
            _layout = new PhaseLayout(_mc, _pc);
        }

        private GameEvaluation Example(MatchTactic ta = MatchTactic.Invade, MatchTactic tb = MatchTactic.Develop)
            => Evaluate(_mc, _layout, PhaseSide(ExampleA), PhaseSide(ExampleB), ta, tb);

        // ------------------------------------------------------------ F1 (AC 15, 16)

        [Test]
        public void F1_Mood80Stress40_MatchesSpec()
        {
            var e = _mc.effective;
            double main = MatchFormulas.LaneFactor(true, 0.8, 1.0);
            Assert.AreEqual(68.607, MatchFormulas.EffectiveValue(e, 70, 80, 40, 1.0, main), Tol);
            Assert.AreEqual(68.642, MatchFormulas.EffectiveValue(e, 70, 80, 40, 0.95, main), Tol);
            Assert.AreEqual(54.886, MatchFormulas.EffectiveValue(e, 70, 80, 40, 1.0, MatchFormulas.LaneFactor(false, 0.8, 1.0)), Tol);
            Assert.AreEqual(56.532, MatchFormulas.EffectiveValue(e, 70, 80, 40, 1.0, MatchFormulas.LaneFactor(false, 0.8, 1.03)), Tol);
        }

        [Test]
        public void F1_WorstStateOffLane_Is74Point1()
        {
            Assert.AreEqual(74.100, MatchFormulas.EffectiveValue(_mc.effective, 100, 0, 100, 1.0, MatchFormulas.LaneFactor(false, 0.8, 1.0)), Tol);
        }

        [Test]
        public void F1_PlayerSnapshot_UsesTraitsFromPlayerConfig()
        {
            var p = TestConfigFactory.MakePlayer(_pc, mainLane: "jungle", fill: 70f, traitA: "resolute", traitB: "clever", stress: 40f, id: 9);
            p.Mood = 80f;
            var s = new StarterSnapshot(p, "mid");
            Assert.IsFalse(s.OnMainLane);
            Assert.AreEqual(0.95f, s.Resolute, 1e-6f); // trait multipliers stay float (PlayerConfig)
            Assert.AreEqual(1.03f, s.OffLaneMultiplier, 1e-6f);
        }

        // ------------------------------------------------------------ F2, F3 (AC 17-19)

        [Test]
        public void F2_JungleEarly_IsMean_CommandIgnored()
        {
            var players = MakePlayers(_pc, 0, fill: 50f, mood: 100f);
            players[1].SetDimension("jungle", 0, 60f); // gold_per_min
            players[1].SetDimension("jungle", 3, 70f); // kda
            players[1].SetDimension("jungle", 4, 80f); // kill_contribution
            var side = MatchFormulas.BuildSide(_mc, _layout, MakeLineup(players), false);
            Assert.AreEqual(70.0, side.Ratings[1][0], 1e-9);
            players[1].SetDimension("jungle", 5, 99f); // command
            var side2 = MatchFormulas.BuildSide(_mc, _layout, MakeLineup(players), false);
            Assert.AreEqual(70.0, side2.Ratings[1][0], 1e-9);
        }

        [Test]
        public void F3_EarlyExample_LambdaVariants()
        {
            var r = new double[] { 66, 70, 66, 64, 66 };
            var w = _layout.LaneWeights[0];
            Assert.AreEqual(66.296, MatchFormulas.PhaseStrength(r, w, 0.15), Tol);
            Assert.AreEqual(66.702, MatchFormulas.PhaseStrength(r, w, 0.0), Tol);
            Assert.AreEqual(65.351, MatchFormulas.PhaseStrength(r, w, 0.5), Tol);
            var same = new double[] { 61, 61, 61, 61, 61 };
            Assert.AreEqual(MatchFormulas.WeightedMean(same, w), MatchFormulas.PhaseStrength(same, w, 0.5), 1e-9);
        }

        [Test]
        public void F3_AllZero_StrengthZero()
        {
            var zero = new[] { new double[5], new double[5], new double[5] };
            var ev = MatchFormulas.EvaluateSide(_mc, _layout, PhaseSide(zero, chemistry: 50), MatchTactic.Invade);
            foreach (var k in MatchPhases.All) Assert.AreEqual(0.0, ev.S(k), 1e-12);
        }

        // ------------------------------------------------------------ F4, F5 (AC 20-22)

        [Test]
        public void F4_MidChemistry_IsRelativeBonus_OnlyMid()
        {
            Assert.AreEqual(-5.0, MatchFormulas.MidChemistryBonus(_mc.strength, 0), 1e-9);
            Assert.AreEqual(2.0, MatchFormulas.MidChemistryBonus(_mc.strength, 70), 1e-9);
            Assert.AreEqual(5.0, MatchFormulas.MidChemistryBonus(_mc.strength, 100), 1e-9);
            var lo = MatchFormulas.EvaluateSide(_mc, _layout, PhaseSide(ExampleA, chemistry: 0), MatchTactic.Invade);
            var hi = MatchFormulas.EvaluateSide(_mc, _layout, PhaseSide(ExampleA, chemistry: 100), MatchTactic.Invade);
            Assert.AreEqual(lo.S(MatchPhase.Early), hi.S(MatchPhase.Early), 1e-12);
            Assert.AreEqual(lo.S(MatchPhase.Late), hi.S(MatchPhase.Late), 1e-12);
            Assert.AreEqual(10.0, hi.S(MatchPhase.Mid) - lo.S(MatchPhase.Mid), 1e-9);
        }

        [Test]
        public void F5_Execution_ValuesAndClamps()
        {
            var e = _mc.execution;
            Assert.AreEqual(0.79, MatchFormulas.Execution(e, 70, 70, 0), 1e-6);
            Assert.AreEqual(0.3, MatchFormulas.Execution(e, 0, 0, 0), 1e-6);
            Assert.AreEqual(1.0, MatchFormulas.Execution(e, 100, 100, 0), 1e-6);
            Assert.AreEqual(1.0, MatchFormulas.Execution(e, 70, 70, 0.5), 1e-6);
            Assert.AreEqual(0.89, MatchFormulas.Execution(e, 70, 70, 0.1), 1e-6);
        }

        [Test]
        public void F5_CommandScore_AssertiveFirstThenMaxThenCap()
        {
            Assert.AreEqual(81.9, MatchFormulas.CommandScore(new double[] { 80, 78 }, new[] { 1.0, 1.05 }), 1e-9);
            Assert.AreEqual(100.0, MatchFormulas.CommandScore(new double[] { 98 }, new[] { 1.05 }), 1e-9);
        }

        // ------------------------------------------------------------ F6 (AC 23, 24, 36)

        private static void Star(double[] leads, double scaleA, out double a, out double b)
            => MatchFormulas.Stars(leads, 4, 15, scaleA, 1.0, out a, out b, out _, out _);

        [Test]
        public void F6_Stars_ThresholdCapAndScale()
        {
            Star(new double[] { 8, 10, 8, 8, 8 }, 1, out var a, out var b);
            Assert.AreEqual(0.0, a, 1e-9); Assert.AreEqual(0.0, b, 1e-9);
            Star(new double[] { 20, 0, 0, 0, 0 }, 1, out a, out _);
            Assert.AreEqual(12.0, a, 1e-9);
            Star(new double[] { 30, 0, 0, 0, 0 }, 1, out a, out _);
            Assert.AreEqual(15.0, a, 1e-9);
            Star(new double[] { 10, -5, -5, -5, -5 }, 1, out a, out b);
            Assert.AreEqual(6.0, a, 1e-9); Assert.AreEqual(0.0, b, 1e-9);
            Star(new double[] { 20, 0, 0, 0, 0 }, 1.5, out a, out _);
            Assert.AreEqual(15.0, a, 1e-9, "12 x 1.5 = 18, capped after the scale");
        }

        [Test]
        public void F6_WorkedExample_Deltas()
        {
            var g = Example();
            Assert.AreEqual(8.447, g.Delta[0], 0.002);
            Assert.AreEqual(-1.673, g.Delta[1], 0.002);
            Assert.AreEqual(-13.260, g.Delta[2], 0.002);
            Assert.AreEqual(0.0, g.StarA[0] + g.StarB[0] + g.StarA[2] + g.StarB[2], 1e-12);
            Assert.AreEqual(66.296, g.A.S(MatchPhase.Early), Tol); Assert.AreEqual(61.236, g.A.S(MatchPhase.Mid), Tol); Assert.AreEqual(56.322, g.A.S(MatchPhase.Late), Tol);
            Assert.AreEqual(57.849, g.B.S(MatchPhase.Early), Tol); Assert.AreEqual(62.909, g.B.S(MatchPhase.Mid), Tol); Assert.AreEqual(69.583, g.B.S(MatchPhase.Late), Tol);
        }

        // ------------------------------------------------------------ F7, F8 (AC 25, 26)

        [Test]
        public void F7_CycleFactors_WorkedExampleAndBounds()
        {
            var g = Example();
            Assert.AreEqual(0.9632, g.Cycle[0], 1e-4);
            Assert.AreEqual(1.0, g.Cycle[1], 1e-12);
            Assert.AreEqual(0.8142, g.Cycle[2], 1e-4);
            Assert.AreEqual(0.4, MatchFormulas.CycleFactor(_mc.cycle, 100, -10), 1e-12);
            Assert.AreEqual(1.0, MatchFormulas.CycleFactor(_mc.cycle, 5, 3), 1e-12);
            Assert.AreEqual(1.0, MatchFormulas.CycleFactor(_mc.cycle, -5, -3), 1e-12);
        }

        [Test]
        public void F7_StarDoesNotDriveTheCycle()
        {
            var aEarly = new[] { new double[] { 60, 90, 60, 60, 60 }, ExampleA[1], ExampleA[2] };
            var bEarly = new[] { new double[] { 66, 66, 66, 66, 66 }, ExampleB[1], ExampleB[2] };
            var g = Evaluate(_mc, _layout, PhaseSide(aEarly), PhaseSide(bEarly), MatchTactic.Invade, MatchTactic.Develop);
            Assert.AreEqual(0.711, g.DeltaStrength[0], 0.002);
            Assert.AreEqual(4.611, g.Delta[0], 0.002);
            Assert.AreEqual(-13.260, g.Delta[2], 0.002);
            Assert.AreEqual(0.9844, g.Cycle[2], 1e-4, "uses dS_E without the star, not 0.8986");
        }

        [Test]
        public void F8_Base_WorkedExample_AndContinuity()
        {
            Assert.AreEqual(-1.452, Example().Base, 0.002);
            double BaseOf(double dl)
            {
                // Direct Delta inputs: A leads E by 30 and trails M by 10; L crosses zero.
                var a = PhaseSide(new[] { Fill(90), Fill(50), Fill(60 + dl) }, chemistry: 50);
                var b = PhaseSide(new[] { Fill(60), Fill(60), Fill(60) }, chemistry: 50);
                return Evaluate(_mc, _layout, a, b, MatchTactic.Invade, MatchTactic.Invade).Base;
            }
            Assert.Less(Math.Abs(BaseOf(0.01) - BaseOf(-0.01)), 0.01);
        }

        private static double[] Fill(double v) => new[] { v, v, v, v, v };

        // ------------------------------------------------------------ F9, F10, budget (AC 27-29)

        [Test]
        public void F9_Fit_WorkedExample()
        {
            var g = Example();
            Assert.AreEqual(0.79, g.A.Execution, 1e-9);
            Assert.AreEqual(0.667, g.FitA, Tol);
            Assert.AreEqual(1.0, g.FitB, 1e-9);
        }

        [Test]
        public void F10_Counter_SignsAndSameTactic()
        {
            Assert.AreEqual(0.395, MatchFormulas.Counter(_mc, MatchTactic.Invade, MatchTactic.Develop, 0.79, 0.5), 1e-9);
            Assert.AreEqual(-0.25, MatchFormulas.Counter(_mc, MatchTactic.Develop, MatchTactic.Invade, 0.79, 0.5), 1e-9);
            foreach (var t in MatchTactics.All) Assert.AreEqual(0.0, MatchFormulas.Counter(_mc, t, t, 0.79, 0.5), 1e-12);
        }

        [Test]
        public void TacticBudget_OwnTacticsSpreadAtMostThree_ThousandRandomPairs()
        {
            var rng = new SeededRandomSource(29);
            double worst = 0;
            for (int n = 0; n < 1000; n++)
            {
                var a = RandomSide(rng, 100, 100); var b = RandomSide(rng, 100, 100);
                foreach (var tb in MatchTactics.All)
                {
                    double lo = double.MaxValue, hi = double.MinValue;
                    foreach (var ta in MatchTactics.All)
                    {
                        var g = Evaluate(_mc, _layout, a, b, ta, tb);
                        Assert.AreEqual(1.0, g.A.Execution, 1e-9);
                        lo = Math.Min(lo, g.G0); hi = Math.Max(hi, g.G0);
                    }
                    worst = Math.Max(worst, hi - lo);
                }
            }
            Assert.LessOrEqual(worst, 3.0 + 1e-9);
        }

        /// <summary>Random phase ratings U(30, 90) and a random hook table.</summary>
        internal static SideInput RandomSide(IRandomSource rng, double chem, double cmd)
        {
            var r = new double[3][];
            for (int k = 0; k < 3; k++) { r[k] = new double[5]; for (int i = 0; i < 5; i++) r[k][i] = rng.NextFloat(30f, 90f); }
            var hooks = new[]
            {
                new SpecialtyEffect(SpecialtyHook.PhaseFlat, rng.NextFloat(-3f, 3f), (MatchPhase)rng.NextInt(0, 3)),
                new SpecialtyEffect(SpecialtyHook.StarScale, rng.NextFloat(0.7f, 1.5f), rng.NextInt(0, 2) == 0 ? MatchPhase.Early : MatchPhase.Late),
                new SpecialtyEffect(SpecialtyHook.ExecAdd, rng.NextFloat(0f, 0.1f)),
                new SpecialtyEffect(SpecialtyHook.SigmaScale, rng.NextFloat(0.8f, 1.2f)),
            };
            var s = PhaseSide(r, chem, cmd, hooks);
            s.AverageStress = rng.NextFloat(0f, 100f);
            return s;
        }

        // ------------------------------------------------------------ F11, F13 (AC 30, 31, 32)

        [Test]
        public void F11_Sigma_Values()
        {
            var n = _mc.noise;
            Assert.AreEqual(2.0, MatchFormulas.Sigma(n, 0, 1, 0, 1), 1e-9);
            Assert.AreEqual(8.0, MatchFormulas.Sigma(n, 100, 1, 0, 1), 1e-9);
            Assert.AreEqual(8.0 * 0.7, MatchFormulas.Sigma(n, 100, 1, 100, 1), 1e-9);
            Assert.AreEqual(2.781, MatchFormulas.Sigma(n, 40, 1, 70, 0.8), Tol);
            Assert.AreEqual(3.476, MatchFormulas.Sigma(n, 40, 1, 70, 1), Tol);
            Assert.AreEqual(-2.0, MatchFormulas.Epsilon(2.0, 0.0), 1e-12);
            Assert.AreEqual(0.0, MatchFormulas.Epsilon(2.0, 0.5), 1e-12);
        }

        [Test]
        public void F13_WorkedExample_NineTacticPairs()
        {
            var expected = new[,] { { 0.496, 0.459, 0.457 }, { 0.484, 0.447, 0.408 }, { 0.432, 0.432, 0.394 } };
            var order = new[] { MatchTactic.Invade, MatchTactic.Teamfight, MatchTactic.Develop };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.AreEqual(expected[i, j], Example(order[i], order[j]).P0, 0.002, order[i] + " v " + order[j]);
            Assert.AreEqual(-1.389, Example().G0, Tol);
            Assert.AreEqual(0.4567, Example().P0, 1e-4);
        }

        [Test]
        public void F13_WinProbability_ValuesClampAndNoNaN()
        {
            var w = _mc.winProbability;
            Assert.AreEqual(0.5, MatchFormulas.WinProbability(w, 0), 1e-9);
            Assert.AreEqual(0.7311, MatchFormulas.WinProbability(w, 8), 1e-4);
            Assert.AreEqual(0.4694, MatchFormulas.WinProbability(w, -0.979), 1e-4);
            Assert.AreEqual(0.85, MatchFormulas.WinProbability(w, 100), 1e-9);
            Assert.AreEqual(0.15, MatchFormulas.WinProbability(w, -100), 1e-9);
            Assert.AreEqual(0.85, MatchFormulas.WinProbability(w, 1e6), 1e-9);
            Assert.IsFalse(double.IsNaN(MatchFormulas.WinProbability(w, -1e6)));
        }

        [Test]
        public void F14_DeciderFactor_ThreeDecisiveTwoHesitant()
        {
            Assert.AreEqual(1.006, MatchFormulas.DeciderFactor(new[] { 1.03, 1.03, 1.03, 0.97, 0.97 }), 1e-9);
        }

        // ------------------------------------------------------------ hooks (AC 35-37)

        [Test]
        public void Hooks_NeutralValues_EqualEmptyTable()
        {
            var neutral = new[]
            {
                new SpecialtyEffect(SpecialtyHook.PhaseFlat, 0f, MatchPhase.Mid), new SpecialtyEffect(SpecialtyHook.StarScale, 1f, MatchPhase.Late),
                new SpecialtyEffect(SpecialtyHook.ExecAdd, 0f), new SpecialtyEffect(SpecialtyHook.SigmaScale, 1f),
            };
            var g1 = Evaluate(_mc, _layout, PhaseSide(ExampleA, hooks: neutral), PhaseSide(ExampleB), MatchTactic.Invade, MatchTactic.Develop);
            var g2 = Example();
            Assert.AreEqual(g2.G0, g1.G0, 0.0);
            CollectionAssert.AreEqual(g2.Delta, g1.Delta);
            Assert.AreEqual(g2.A.Sigma, g1.A.Sigma, 0.0);
        }

        [Test]
        public void Hooks_PhaseFlatMid_AddedAfterChemistry()
        {
            var hooks = new[] { new SpecialtyEffect(SpecialtyHook.PhaseFlat, 3f, MatchPhase.Mid) };
            var ev = MatchFormulas.EvaluateSide(_mc, _layout, PhaseSide(ExampleA, hooks: hooks), MatchTactic.Invade);
            Assert.AreEqual(64.236, ev.S(MatchPhase.Mid), Tol);
        }

        [Test]
        public void Hooks_StarScaleEarly_CappedAfterScale()
        {
            var a = new[] { new double[] { 80, 60, 60, 60, 60 }, ExampleA[1], ExampleA[2] };
            var b = new[] { new double[] { 60, 60, 60, 60, 60 }, ExampleB[1], ExampleB[2] };
            var hooks = new[] { new SpecialtyEffect(SpecialtyHook.StarScale, 1.5f, MatchPhase.Early) };
            var g = Evaluate(_mc, _layout, PhaseSide(a, hooks: hooks), PhaseSide(b), MatchTactic.Invade, MatchTactic.Develop);
            Assert.AreEqual(15.0, g.StarA[0], 1e-9);
            var g0 = Evaluate(_mc, _layout, PhaseSide(a), PhaseSide(b), MatchTactic.Invade, MatchTactic.Develop);
            Assert.AreEqual(12.0, g0.StarA[0], 1e-9);
        }

        [Test]
        public void Hooks_AggregateClamps()
        {
            var sc = _mc.specialty;
            HookTotals Agg(params SpecialtyEffect[] e) => HookTotals.Aggregate(e, sc);
            var f = SpecialtyHook.PhaseFlat;
            Assert.AreEqual(5.0, Agg(new SpecialtyEffect(f, 4f, MatchPhase.Early), new SpecialtyEffect(f, 5f, MatchPhase.Early)).FlatEarly, 1e-9);
            Assert.AreEqual(-5.0, Agg(new SpecialtyEffect(f, -4f, MatchPhase.Late), new SpecialtyEffect(f, -5f, MatchPhase.Late)).FlatLate, 1e-9);
            var s = SpecialtyHook.StarScale;
            Assert.AreEqual(0.5, Agg(new SpecialtyEffect(s, 0.5f), new SpecialtyEffect(s, 0.5f)).StarScaleEarly, 1e-9);
            Assert.AreEqual(2.0, Agg(new SpecialtyEffect(s, 2f, MatchPhase.Late), new SpecialtyEffect(s, 2f, MatchPhase.Late)).StarScaleLate, 1e-9);
            Assert.AreEqual(0.7, Agg(new SpecialtyEffect(SpecialtyHook.SigmaScale, 0.6f)).SigmaScale, 1e-9);
        }

        [Test]
        public void Hooks_OnlyChangeOwnSide()
        {
            var hooks = new[]
            {
                new SpecialtyEffect(SpecialtyHook.PhaseFlat, 4f, MatchPhase.Early), new SpecialtyEffect(SpecialtyHook.ExecAdd, 0.2f),
                new SpecialtyEffect(SpecialtyHook.SigmaScale, 0.8f), new SpecialtyEffect(SpecialtyHook.StarScale, 2f, MatchPhase.Late),
            };
            var with = Evaluate(_mc, _layout, PhaseSide(ExampleA, hooks: hooks), PhaseSide(ExampleB), MatchTactic.Invade, MatchTactic.Develop);
            var without = Example();
            CollectionAssert.AreEqual(without.B.Strength, with.B.Strength);
            Assert.AreEqual(without.B.Execution, with.B.Execution, 0.0);
            Assert.AreEqual(without.B.Sigma, with.B.Sigma, 0.0);
            CollectionAssert.AreEqual(without.StarB, with.StarB);
        }

        [Test]
        public void Hooks_StarScaleOnMid_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SpecialtyEffect(SpecialtyHook.StarScale, 1.5f, MatchPhase.Mid));
        }
    }
}
