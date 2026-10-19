using System;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;
using EsportsManager.Tests.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Segments
{
    /// <summary>Story 003 AC1-AC4: action points, validation, training, streaming, resting.</summary>
    public class SegmentActionTests
    {
        private const float Tol = 1e-3f;
        private Fixture _fx;

        [SetUp] public void SetUp() { _fx = new Fixture(); }

        private Player S(int i) => _fx.Starters[i];
        private PlayerSegmentSummary Of(SegmentSummary s, Player p) => s.Players.Find(x => x.PlayerId == p.Id);

        // ---------------- AC1 action points and validation

        [Test]
        public void Plan_UsesExactlyAvailablePoints_Resolves()
        {
            var plan = new SegmentPlan().Train(1, 0).Train(1, 0).Stream(2).Stream(2).Rest(3).Rest(3);
            var s = _fx.Run(plan);
            Assert.AreEqual(6, s.PointsUsed);
            Assert.AreEqual(0, s.PointsUnused);
        }

        [Test]
        public void Plan_OverTotalPoints_ThrowsInvalidOperation()
        {
            var plan = new SegmentPlan().Train(1, 0).Train(1, 0).Train(2, 0).Train(2, 0).Train(3, 0).Train(3, 0).Train(4, 0);
            _fx.Runner.BeginSegment();
            var ex = Assert.Throws<InvalidOperationException>(() => _fx.Runner.Resolve(plan, Ctx()));
            StringAssert.Contains("7", ex.Message);
        }

        [Test]
        public void Plan_PlayerOverPerPlayerLimit_ThrowsInvalidOperation()
        {
            var plan = new SegmentPlan().Train(1, 0).Stream(1).Rest(1);
            _fx.Runner.BeginSegment();
            var ex = Assert.Throws<InvalidOperationException>(() => _fx.Runner.Resolve(plan, Ctx()));
            StringAssert.Contains("Player 1", ex.Message);
        }

        [Test]
        public void Plan_UnknownPlayer_ThrowsArgument()
        {
            _fx.Runner.BeginSegment();
            Assert.Throws<ArgumentException>(() => _fx.Runner.Resolve(new SegmentPlan().Train(999, 0), Ctx()));
        }

        [Test]
        public void Plan_DimensionIndexOutOfRange_ThrowsArgument()
        {
            _fx.Runner.BeginSegment();
            Assert.Throws<ArgumentException>(() => _fx.Runner.Resolve(new SegmentPlan().Train(1, 6), Ctx()));
            Assert.Throws<ArgumentException>(() => _fx.Runner.Resolve(new SegmentPlan().Train(1, -1), Ctx()));
        }

        [Test]
        public void Plan_UnknownAction_ThrowsArgument()
        {
            _fx.Runner.BeginSegment();
            Assert.Throws<ArgumentException>(() => _fx.Runner.Resolve(new BadPlan().Make(), Ctx()));
        }

        // A plan carrying an undefined enum value (cast), built through the public API only.
        private sealed class BadPlan
        {
            public SegmentPlan Make()
            {
                var plan = new SegmentPlan();
                var list = (System.Collections.Generic.List<ActionAssignment>)typeof(SegmentPlan)
                    .GetField("_assignments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(plan);
                list.Add(new ActionAssignment(1, (SegmentAction)99));
                return plan;
            }
        }

        [Test]
        public void Resolve_WithoutBegin_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(() => _fx.Runner.Resolve(new SegmentPlan(), Ctx()));
        }

        [Test]
        public void Begin_WhileOpen_ThrowsInvalidOperation()
        {
            _fx.Runner.BeginSegment();
            Assert.Throws<InvalidOperationException>(() => _fx.Runner.BeginSegment());
        }

        [Test]
        public void Resolve_NullPlan_ThrowsArgumentNull()
        {
            _fx.Runner.BeginSegment();
            Assert.Throws<ArgumentNullException>(() => _fx.Runner.Resolve(null, Ctx()));
        }

        [Test]
        public void Resolve_BadContext_ThrowsArgumentOutOfRange()
        {
            _fx.Runner.BeginSegment();
            Assert.Throws<ArgumentOutOfRangeException>(() => _fx.Runner.Resolve(new SegmentPlan(), new SegmentContext())); // SegmentsPerYear 0
            Assert.Throws<ArgumentOutOfRangeException>(() => _fx.Runner.Resolve(new SegmentPlan(), new SegmentContext { SegmentsPerYear = 12, SegmentIndexInYear = 12 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _fx.Runner.Resolve(new SegmentPlan(), new SegmentContext { SegmentsPerYear = 12, SeasonRankScore = 101f }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _fx.Runner.Resolve(new SegmentPlan(), new SegmentContext { SegmentsPerYear = 12, PrizeMoney = -1 }));
        }

        [Test]
        public void Validate_FailureChangesNoState_AndSegmentStaysOpen()
        {
            var rng = new ScriptedRandomSource(0.5f);
            var fx = new Fixture(rng, funds: 1000);
            fx.AddBench(100);
            var snapshot = Snapshot(fx);
            fx.Runner.BeginSegment();
            int drawsAfterBegin = rng.Draws;
            // valid first half, invalid second half (unknown player): must not train player 1
            var bad = new SegmentPlan().Train(1, 0).Stream(2).Sponsor().Train(999, 0);
            Assert.Throws<ArgumentException>(() => fx.Runner.Resolve(bad, Ctx(true)));
            var tooMany = new SegmentPlan().Train(1, 0).Train(1, 0).Train(2, 0).Train(2, 0).Train(3, 0).Train(3, 0).Sponsor();
            Assert.Throws<InvalidOperationException>(() => fx.Runner.Resolve(tooMany, Ctx(true)));
            Assert.AreEqual(snapshot, Snapshot(fx));
            Assert.AreEqual(drawsAfterBegin, rng.Draws, "a failed validation must not consume random numbers");
            Assert.IsTrue(fx.Runner.GetState().IsOpen);
            Assert.AreEqual(0, fx.Runner.GetState().ActiveContracts.Count);
            Assert.AreEqual(0, fx.Team.RecentResults.Count);
            // the corrected plan still works
            Assert.DoesNotThrow(() => fx.Runner.Resolve(new SegmentPlan().Train(1, 0), Ctx(true)));
        }

        private static string Snapshot(Fixture fx)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(fx.Team.Funds).Append('|').Append(fx.Team.ClubPopularity).Append('|').Append(fx.Team.RecentResults.Count);
            foreach (var p in fx.Team.Roster)
                sb.Append('|').Append(p.Mood).Append(',').Append(p.Stress).Append(',').Append(p.Popularity).Append(',')
                  .Append(p.Observation).Append(',').Append(string.Join(";", p.GetRadar(p.MainLane)));
            return sb.ToString();
        }

        [Test]
        public void UnusedPoints_AreLostAndDoNotCarryOver()
        {
            var s1 = _fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(5, s1.PointsUnused);
            Assert.AreEqual(6, _fx.Runner.GetState().ActionPoints);
            Assert.AreEqual(6, _fx.Runner.PointsRemaining(new SegmentPlan()));
            _fx.Runner.BeginSegment();
            var seven = new SegmentPlan().Train(1, 0).Train(1, 0).Train(2, 0).Train(2, 0).Train(3, 0).Train(3, 0).Train(4, 0);
            Assert.Throws<InvalidOperationException>(() => _fx.Runner.Resolve(seven, Ctx()));
        }

        [Test]
        public void PointsRemaining_CountsAllPointKinds()
        {
            var plan = new SegmentPlan().Train(1, 0).Stream(2).Rest(3).Sponsor().AcceptOffer(0);
            Assert.AreEqual(5, plan.PointsUsed);
            Assert.AreEqual(1, _fx.Runner.PointsRemaining(plan));
        }

        // ---------------- AC2 training

        [Test]
        public void Train_OnePoint_RaisesAllSixDimensions_TargetTimesBonus()
        {
            var p = S(0);
            float baseGain = p.TrainingGain("top", 2, 1f);
            Assert.AreEqual(0.75f, baseGain, Tol); // 2 x 1 x (1 - 50/80) x 1 x 1.0 x 1
            var s = _fx.Run(new SegmentPlan().Train(1, 2));
            Assert.AreEqual(50f + baseGain * 1.5f, p.GetDimension("top", 2), Tol);
            for (int d = 0; d < 6; d++)
                if (d != 2) Assert.AreEqual(50f + baseGain, p.GetDimension("top", d), Tol, "dimension " + d);
            Assert.AreEqual(50f, p.GetDimension("mid", 0), Tol, "off lanes are not trained");
            var t = Of(s, p).Training[0];
            Assert.AreEqual(2, t.DimensionIndex);
            Assert.AreEqual(baseGain * 1.5f, t.Gain, Tol);
            Assert.AreEqual(p.GetDimension("top", 2), t.NewValue, Tol);
            Assert.AreEqual(6, t.Gains.Length);
            Assert.AreEqual(baseGain, t.Gains[0], Tol);
        }

        [Test]
        public void Train_TwoPointsSameDimension_IsIntensityTwo()
        {
            var p = S(0);
            float expected = p.TrainingGain("top", 0, 2f);
            Assert.AreEqual(1.5f, expected, Tol);
            var s = _fx.Run(new SegmentPlan().Train(1, 0).Train(1, 0));
            Assert.AreEqual(50f + expected * 1.5f, p.GetDimension("top", 0), Tol);
            Assert.AreEqual(50f + expected, p.GetDimension("top", 3), Tol);
            Assert.AreEqual(1, Of(s, p).Training.Count);
            Assert.AreEqual(2, Of(s, p).Training[0].Points);
        }

        [Test]
        public void Train_TwoPointsDifferentTargets_BothPassesUseSegmentStartValues()
        {
            // gain(x) = T_base 2 x max(1 - x/80, 0.15) for this neutral 22-year-old with stress 0
            float G = 2f * (1f - 50f / 80f);
            var p = S(0);
            _fx.Run(new SegmentPlan().Train(1, 0).Train(1, 1));
            Assert.AreEqual(50f + G * 1.5f + G, p.GetDimension("top", 0), Tol);
            Assert.AreEqual(50f + G + G * 1.5f, p.GetDimension("top", 1), Tol);
            Assert.AreEqual(50f + 2f * G, p.GetDimension("top", 2), Tol);
        }

        [Test]
        public void Train_DifferentTargets_OrderDoesNotMatter()
        {
            var a = new Fixture(); var b = new Fixture();
            a.Starters[0].SetDimension("top", 0, 70f); b.Starters[0].SetDimension("top", 0, 70f);
            a.Run(new SegmentPlan().Train(1, 0).Train(1, 3));
            b.Run(new SegmentPlan().Train(1, 3).Train(1, 0));
            CollectionAssert.AreEqual(a.Starters[0].GetRadar("top"), b.Starters[0].GetRadar("top"));
        }

        [Test]
        public void Train_NearHundred_ClampsAtHundred()
        {
            var p = S(0);
            p.SetDimension("top", 0, 99.9f);
            var s = _fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(100f, p.GetDimension("top", 0));
            Assert.AreEqual(0.45f, Of(s, p).Training[0].Gain, Tol); // soft cap floor 0.15 x T_base 2 x target_bonus 1.5
        }

        [Test]
        public void Train_RaisesStressAndObservation()
        {
            var p = S(0);
            var s = _fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(6f, p.Stress, Tol); // s_train x intensity 1 x R(1)
            Assert.AreEqual(3f + 5f, p.Observation, Tol); // o_train + o_seg
            Assert.AreEqual(6f, Of(s, p).StressDelta, Tol);
        }

        [Test]
        public void Train_TwoPoints_DoublesStress()
        {
            _fx.Run(new SegmentPlan().Train(1, 0).Train(1, 0));
            Assert.AreEqual(12f, S(0).Stress, Tol);
            Assert.AreEqual(6f + 5f, S(0).Observation, Tol);
        }

        [Test]
        public void Train_UntrainedRosterPlayer_GetsOnlySegmentObservation()
        {
            _fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(5f, S(1).Observation, Tol);
            Assert.AreEqual(0f, S(1).Stress, Tol);
        }

        [Test]
        public void Train_UsesStressBeforeThisSegmentsStressUpdate()
        {
            var fx = new Fixture();
            var p = fx.Starters[0];
            p.Stress = 40f;
            float expected = p.TrainingGain("top", 0, 1f) * 1.5f; // (1 - 0.5 x 0.4) factor, target bonus
            fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(50f + expected, p.GetDimension("top", 0), Tol);
        }

        [Test]
        public void Train_UsesIntensityFromConfig()
        {
            var fx = new Fixture();
            fx.Cfg.training.intensity = 2f;
            fx.Runner = new SegmentRunner(fx.Cfg, fx.Players, fx.Team, fx.Rng);
            var p = fx.Starters[0];
            float expected = p.TrainingGain("top", 0, 2f) * 1.5f;
            fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(50f + expected, p.GetDimension("top", 0), Tol);
            Assert.AreEqual(12f, p.Stress, Tol); // stress follows the intensity too
        }

        // ---------------- AC3 stream

        [Test]
        public void Stream_OneAndTwoPoints_GiveHeatFiftyAndHundred()
        {
            var s = _fx.Run(new SegmentPlan().Stream(1).Stream(2).Stream(2));
            Assert.AreEqual(50f, Of(s, S(0)).StreamHeat, Tol);
            Assert.AreEqual(100f, Of(s, S(1)).StreamHeat, Tol);
            Assert.AreEqual(0f, Of(s, S(2)).StreamHeat, Tol);
        }

        [Test]
        public void Stream_TraitMultiplierAppliedBeforeCap()
        {
            var lively = _fx.AddBench(100, "mid", "lively", NeutralB);
            var s = _fx.Run(new SegmentPlan().Stream(100));
            Assert.AreEqual(65f, Of(s, lively).StreamHeat, Tol);
        }

        [Test]
        public void Stream_IncomeIsTeamLivestreamIncomeWithPreSettlePopularity()
        {
            int expected = _fx.Team.LivestreamIncome(S(0), 2) + _fx.Team.LivestreamIncome(S(1), 1);
            Assert.AreEqual(2 * (500 + 20 * 50) + (500 + 20 * 50), expected);
            var s = _fx.Run(new SegmentPlan().Stream(1).Stream(1).Stream(2));
            Assert.AreEqual(expected, s.LivestreamIncome);
            Assert.AreEqual(expected, Of(s, S(0)).LivestreamIncome + Of(s, S(1)).LivestreamIncome);
            Assert.AreEqual(expected, s.Report.Income);
        }

        [Test]
        public void Stream_SecondStreamOfSamePlayer_EarnsSecondStreamFactor()
        {
            _fx.TeamCfg.livestream.secondStreamFactor = 0.5f;
            var s = _fx.Run(new SegmentPlan().Stream(1).Stream(1).Stream(2));
            Assert.AreEqual(1500 + 750, Of(s, S(0)).LivestreamIncome); // (500 + 20 x 50) x (1 + 0.5)
            Assert.AreEqual(1500, Of(s, S(1)).LivestreamIncome);
            Assert.AreEqual(3750, s.LivestreamIncome);
            Assert.AreEqual(100f, Of(s, S(0)).StreamHeat, 1e-3f, "heat still counts both streams");
        }

        [Test]
        public void Stream_WithZeroStreamStressTerm_LeavesStressUnchanged()
        {
            var fx = new Fixture();
            fx.Cfg.stress.stream = 0f;
            fx.Runner = new SegmentRunner(fx.Cfg, fx.Players, fx.Team, fx.Rng);
            fx.Starters[0].Stress = 20f;
            fx.Run(new SegmentPlan().Stream(1).Stream(1));
            Assert.AreEqual(20f, fx.Starters[0].Stress, 1e-3f);
        }

        [Test]
        public void Stream_LowersStressSlightly()
        {
            var fx = new Fixture();
            fx.Starters[0].Stress = 20f;
            fx.Run(new SegmentPlan().Stream(1));
            Assert.AreEqual(17f, fx.Starters[0].Stress, Tol);
        }

        [Test]
        public void Stream_HeatRaisesPopularityMoreThanNoStream()
        {
            var a = _fx.AddBench(100); var b = _fx.AddBench(101);
            var s = _fx.Run(new SegmentPlan().Stream(100).Stream(100));
            Assert.Greater(Of(s, a).PopularityDelta, Of(s, b).PopularityDelta);
        }

        // ---------------- AC4 rest and stress

        [Test]
        public void Rest_OnePoint_LowersStressByRestTerm()
        {
            var fx = new Fixture();
            fx.Starters[0].Stress = 30f;
            fx.Run(new SegmentPlan().Rest(1));
            Assert.AreEqual(15f, fx.Starters[0].Stress, Tol);
        }

        [Test]
        public void Rest_TwoPoints_StackAndClampAtZero()
        {
            var fx = new Fixture();
            fx.Starters[0].Stress = 20f;
            fx.Run(new SegmentPlan().Rest(1).Rest(1));
            Assert.AreEqual(0f, fx.Starters[0].Stress);
        }

        [Test]
        public void Stress_MixedActions_SumTerms()
        {
            // train +6, stream -3 on top of 30
            var fx = new Fixture();
            fx.Starters[0].Stress = 30f;
            fx.Run(new SegmentPlan().Train(1, 0).Stream(1));
            Assert.AreEqual(30f + 6f - 3f, fx.Starters[0].Stress, Tol);
        }

        [Test]
        public void Stress_MatchesAndLosses_OnlyStartersFeelThem()
        {
            var bench = _fx.AddBench(100);
            _fx.Run(new SegmentPlan(), Ctx(true, false, false));
            Assert.AreEqual(2f * 3 + 3f * 2, S(0).Stress, Tol);
            Assert.AreEqual(0f, bench.Stress, Tol);
        }

        [Test]
        public void Stress_PersonalityMultiplierScalesChange()
        {
            var fx = new Fixture();
            var lazy = fx.AddBench(100, "mid", "lazy", NeutralB);
            fx.Run(new SegmentPlan().Train(100, 0));
            Assert.AreEqual(3f, lazy.Stress, Tol);
        }

        [Test]
        public void Stress_RandomFactorFromRandomSource_IsApplied()
        {
            var fx = new Fixture(new StubRandomSource(1f)); // R(0.1) = 1.1, R(0.15) = 1.15
            fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(6.6f, fx.Starters[0].Stress, Tol);
        }

        [Test]
        public void Stress_Recovery_AppliesToEveryRosteredPlayerEverySegment()
        {
            var fx = new Fixture();
            fx.Cfg.stress.recovery = 4f;
            fx.Runner = new SegmentRunner(fx.Cfg, fx.Players, fx.Team, fx.Rng);
            var bench = fx.AddBench(100, stress: 10f);
            fx.Starters[1].Stress = 2f;
            fx.Run(new SegmentPlan().Train(1, 0));
            Assert.AreEqual(6f - 4f, fx.Starters[0].Stress, Tol);
            Assert.AreEqual(6f, bench.Stress, Tol);
            Assert.AreEqual(0f, fx.Starters[1].Stress, "clamped at 0");
        }

        [Test]
        public void Stress_ClampedToRangeAfterSegment()
        {
            var fx = new Fixture();
            fx.Starters[0].Stress = 99f;
            fx.Run(new SegmentPlan().Train(1, 0).Train(1, 0));
            Assert.AreEqual(100f, fx.Starters[0].Stress);
        }

        // ---------------- AC5 mood through the runner

        [Test]
        public void Mood_StarterAndBench_UseLineup()
        {
            var bench = _fx.AddBench(100);
            _fx.Run(new SegmentPlan());
            Assert.AreEqual(54f, S(0).Mood, Tol);
            Assert.AreEqual(46f, bench.Mood, Tol);
        }

        [Test]
        public void Mood_WinAndLossFromContext()
        {
            var fx = new Fixture();
            fx.Run(new SegmentPlan(), Ctx(true, true, false)); // more wins than losses: +1
            Assert.AreEqual(62f, fx.Starters[0].Mood, Tol);
            var fx2 = new Fixture();
            fx2.Run(new SegmentPlan(), Ctx(false)); // loss: -8 + 4
            Assert.AreEqual(46f, fx2.Starters[0].Mood, Tol);
            var fx3 = new Fixture();
            fx3.Run(new SegmentPlan(), Ctx(true, false)); // equal: 0
            Assert.AreEqual(54f, fx3.Starters[0].Mood, Tol);
        }

        [Test]
        public void Mood_SalarySatisfaction_UsesExpectedSalaryWithoutRandom()
        {
            var p = S(0);
            int expected = p.CalculateSalaryWithoutRandom();
            p.Salary = expected + expected / 2;
            _fx.Run(new SegmentPlan());
            Assert.AreEqual(54f + 5f, p.Mood, 0.01f);
        }

        [Test]
        public void Mood_IndividualCompatibility_IsMeanOverOtherStarters()
        {
            var fx = new Fixture(lineup: false);
            // replace starters 1 and 2 by an assertive + gentle pair (H = +0.5)
            var a = fx.MakePlayer(11, "top", "assertive", NeutralB);
            var b = fx.MakePlayer(12, "jungle", "gentle", NeutralB);
            fx.Team.AddPlayer(a); fx.Team.AddPlayer(b);
            fx.Team.SetStarter("top", a); fx.Team.SetStarter("jungle", b);
            fx.Team.SetStarter("mid", fx.Starters[2]); fx.Team.SetStarter("adc", fx.Starters[3]); fx.Team.SetStarter("support", fx.Starters[4]);
            fx.Run(new SegmentPlan());
            Assert.AreEqual(50f + 4f + 5f * (0.5f / 4f), a.Mood, Tol);
        }

        [Test]
        public void Mood_ClampedToRange()
        {
            var fx = new Fixture();
            fx.Starters[0].Mood = 100f;
            fx.Starters[1].Mood = 0f;
            fx.Starters[1].Stress = 100f;
            fx.Run(new SegmentPlan(), Ctx(true));
            Assert.LessOrEqual(fx.Starters[0].Mood, 100f);
            Assert.GreaterOrEqual(fx.Starters[1].Mood, 0f);
        }

        [Test]
        public void Mood_UsesRandomFactor()
        {
            var fx = new Fixture(new StubRandomSource(1f)); // R(0.15) = 1.15
            fx.Run(new SegmentPlan());
            Assert.AreEqual(50f + 4f * 1.15f, fx.Starters[0].Mood, Tol);
        }
    }
}
