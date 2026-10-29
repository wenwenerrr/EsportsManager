using System;
using System.Linq;
using EsportsManager.Growth;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Tests.Matches;
using NUnit.Framework;
using static EsportsManager.Tests.Growth.GrowthTestFactory;

namespace EsportsManager.Tests.Growth
{
    /// <summary>
    /// Story 012: design/gdd/growth.md rev 3 Acceptance Criteria 1-17 (noise fixed by passing the factor; tolerance +-0.005
    /// unless noted) plus the hook aggregation and talent.md initial focus. Pure in-memory fixtures (see GrowthTestFactory).
    /// </summary>
    public class GrowthTests
    {
        private const float Tol = 0.005f;
        private PlayerConfig _cfg;
        private GrowthSystem _g;

        [SetUp]
        public void SetUp()
        {
            _cfg = MakeConfig();
            _g = MakeGrowth(_cfg);
        }

        private int D(string lane, string id) => Dim(_cfg, lane, id);

        // ---------------- AC1 / AC2 / AC17: F1 base amount

        [Test]
        public void BaseAmount_Age21Mood60Stress30Focus05_Is2704_Stress70Is1460()
        {
            var p = MakePlayer(_cfg, focus: new[] { 0.5f, 0.2f, 0.2f });
            Assert.AreEqual(2.704f, _g.BaseAmount(p, TrainingTarget.Early, 1f, 1f), Tol);
            p.Stress = 70f;
            Assert.AreEqual(1.460f, _g.BaseAmount(p, TrainingTarget.Early, 1f, 1f), Tol);
        }

        [Test]
        public void MoodAndStressFactors_MatchGrowthTable()
        {
            Assert.AreEqual(0.8f, _g.MoodFactor(0f), 1e-6f);
            Assert.AreEqual(1.2f, _g.MoodFactor(100f), 1e-6f);
            Assert.AreEqual(1.0f, _g.StressFactor(50f), 1e-6f);
            Assert.AreEqual(0.54f, _g.StressFactor(70f), 1e-5f);
            Assert.AreEqual(0f, _g.StressFactor(100f), 1e-6f);
            Assert.AreEqual(0f, _g.StressFactor(93.5f), 1e-5f);
            Assert.AreEqual(1.0f, _g.StressFactor(0f), 1e-6f);
        }

        [Test]
        public void BaseAmount_InjectedNoise085And115_ScalesB()
        {
            var p = MakePlayer(_cfg, focus: new[] { 0.5f, 0.2f, 0.2f });
            Assert.AreEqual(2.704f * 0.85f, _g.BaseAmount(p, TrainingTarget.Early, 1f, 0.85f), Tol);
            Assert.AreEqual(2.704f * 1.15f, _g.BaseAmount(p, TrainingTarget.Early, 1f, 1.15f), Tol);
            Assert.AreEqual(0.85f, _g.NoiseLow, 1e-6f);
            Assert.AreEqual(1.15f, _g.NoiseHigh, 1e-6f);
        }

        // ---------------- AC3 / AC5 / AC15: ceiling

        [Test]
        public void Head_Potential65_Dims50_70_75_80_Give1_02_0_0()
        {
            var p = MakePlayer(_cfg, potential: 65f);
            int d = D("jungle", "gold_per_min");
            float[] values = { 50f, 70f, 75f, 80f };
            float[] heads = { 1f, 0.2f, 0f, 0f };
            for (int i = 0; i < values.Length; i++)
            {
                p.SetDimension("jungle", d, values[i]);
                Assert.AreEqual(heads[i], _g.Head(p, d), 1e-5f, "d = " + values[i]);
            }
            Assert.AreEqual(75f, _g.Cap(p), 1e-6f);
        }

        [Test]
        public void ApplyGains_Dim745Cap75Gain3_EndsAt75_AboveCapUnchanged()
        {
            var p = MakePlayer(_cfg, potential: 65f);
            int d = D("jungle", "gold_per_min"), other = D("jungle", "kda");
            p.SetDimension("jungle", d, 74.5f);
            p.SetDimension("jungle", other, 80f); // above the ceiling (potential lowered): never lowered
            var gains = new float[6]; gains[d] = 3f; gains[other] = 3f;
            _g.ApplyGains(p, gains);
            Assert.AreEqual(75f, p.GetDimension("jungle", d), 1e-5f);
            Assert.AreEqual(80f, p.GetDimension("jungle", other), 1e-5f);
        }

        [Test]
        public void LongTraining_Potential60_NoDimensionAbove70()
        {
            var p = MakePlayer(_cfg, potential: 60f, fill: 40f, stress: 0f, mood: 100f);
            for (int seg = 0; seg < 200; seg++)
            {
                var t = (TrainingTarget)(seg % 4);
                var acc = new float[6];
                for (int i = 0; i < 2; i++)
                {
                    var g = _g.PointGains(p, t, -1, _g.BaseAmount(p, t, 1f, 1.15f) * 3f + 5f); // event-sized boosts too
                    for (int d = 0; d < 6; d++) acc[d] += g[d];
                }
                _g.ApplyGains(p, acc);
                var counts = new int[4]; counts[(int)t] = 2;
                _g.UpdateFocus(p, counts);
            }
            foreach (float v in p.GetRadar("jungle")) Assert.LessOrEqual(v, 70f + 1e-4f);
            Assert.Greater(p.GetRadar("jungle").Max(), 69f, "long training reaches the ceiling");
        }

        // ---------------- AC4: weights and focus dimension

        [Test]
        public void PointGains_JungleEarlyFocusGoldPerMin_RedistributesInsidePhase()
        {
            var p = MakePlayer(_cfg, potential: 65f);
            const float b = 2.298f;
            var g = _g.PointGains(p, TrainingTarget.Early, D("jungle", "gold_per_min"), b);
            Assert.AreEqual(3.064f, g[D("jungle", "gold_per_min")], Tol);
            Assert.AreEqual(1.915f, g[D("jungle", "kda")], Tol);
            Assert.AreEqual(1.915f, g[D("jungle", "kill_contribution")], Tol);
            Assert.AreEqual(0.230f, g[D("jungle", "gold_conversion")], Tol);
            Assert.AreEqual(0.230f, g[D("jungle", "teamfight_participation")], Tol);
            Assert.AreEqual(0.230f, g[D("jungle", "command")], Tol);
            float mean = (g[0] + g[3] + g[4]) / 3f;
            Assert.AreEqual(b, mean, Tol, "phase mean unchanged");

            var none = _g.PointGains(p, TrainingTarget.Early, -1, b);
            foreach (var id in new[] { "gold_per_min", "kda", "kill_contribution" }) Assert.AreEqual(b, none[D("jungle", id)], Tol);
        }

        [Test]
        public void PointGains_SingleDimensionPhaseFocus_HasNoExtraEffect()
        {
            var p = MakePlayer(_cfg, potential: 65f);
            var with = _g.PointGains(p, TrainingTarget.Mid, D("jungle", "teamfight_participation"), 2f);
            var without = _g.PointGains(p, TrainingTarget.Mid, -1, 2f);
            CollectionAssert.AreEqual(without, with);
        }

        [Test]
        public void IsValidFocus_OnlyDimensionsOfTheTargetPhase()
        {
            Assert.IsTrue(_g.IsValidFocus("jungle", TrainingTarget.Early, D("jungle", "kda")));
            Assert.IsTrue(_g.IsValidFocus("jungle", TrainingTarget.Early, -1));
            Assert.IsFalse(_g.IsValidFocus("jungle", TrainingTarget.Early, D("jungle", "gold_conversion")));
            Assert.IsFalse(_g.IsValidFocus("jungle", TrainingTarget.Command, D("jungle", "command")));
            Assert.IsFalse(_g.IsValidFocus("jungle", TrainingTarget.Early, -2));
        }

        // ---------------- AC6: command

        [Test]
        public void Command_TrainsCommandFully_OthersTimesOtherShare_SpecOne_FocusDecays()
        {
            var p = MakePlayer(_cfg, potential: 65f, focus: new[] { 0.5f, 0.3f, 0.2f });
            Assert.AreEqual(1f, _g.SpecFactor(p, TrainingTarget.Command));
            float b = _g.BaseAmount(p, TrainingTarget.Command, 1f, 1f);
            Assert.AreEqual(2f * 1.04f, b, Tol);
            var g = _g.PointGains(p, TrainingTarget.Command, -1, b);
            int cmd = D("jungle", "command");
            Assert.AreEqual(b, g[cmd], Tol);
            for (int d = 0; d < 6; d++) if (d != cmd) Assert.AreEqual(0.1f * b, g[d], Tol);
            _g.UpdateFocus(p, new[] { 0, 0, 0, 1 });
            Assert.AreEqual(0.5f * 0.837f, p.GetFocus(0), Tol);
            Assert.AreEqual(0.3f * 0.837f, p.GetFocus(1), Tol);
            Assert.AreEqual(0.2f * 0.837f, p.GetFocus(2), Tol);
        }

        // ---------------- AC7 / AC8: focus update from counts (F3)

        [Test]
        public void TwoPointsEarlyAndMid_SegmentStartValues_OrderFree_Focus029_029_014()
        {
            var a = MakePlayer(_cfg);
            var b = MakePlayer(_cfg);
            // both points computed from the same start values; summing in either order is identical
            var e = _g.PointGains(a, TrainingTarget.Early, -1, _g.BaseAmount(a, TrainingTarget.Early, 1f));
            var m = _g.PointGains(a, TrainingTarget.Mid, -1, _g.BaseAmount(a, TrainingTarget.Mid, 1f));
            _g.ApplyGains(a, e.Zip(m, (x, y) => x + y).ToArray());
            _g.ApplyGains(b, m.Zip(e, (x, y) => x + y).ToArray());
            CollectionAssert.AreEqual(a.GetRadar("jungle"), b.GetRadar("jungle"));
            _g.UpdateFocus(a, new[] { 1, 1, 0, 0 });
            Assert.AreEqual(0.290f, a.GetFocus(0), Tol);
            Assert.AreEqual(0.290f, a.GetFocus(1), Tol);
            Assert.AreEqual(0.140f, a.GetFocus(2), Tol);
        }

        [Test]
        public void TwoPointsEarly_Focus044_014_014_ThenSegmentsMatchExample()
        {
            var p = MakePlayer(_cfg);
            _g.UpdateFocus(p, new[] { 2, 0, 0, 0 });
            Assert.AreEqual(0.440f, p.GetFocus(0), Tol);
            Assert.AreEqual(0.140f, p.GetFocus(1), Tol);
            Assert.AreEqual(0.140f, p.GetFocus(2), Tol);
            _g.UpdateFocus(p, new[] { 2, 0, 0, 0 });
            Assert.AreEqual(0.607f, p.GetFocus(0), Tol);
            _g.UpdateFocus(p, new[] { 2, 0, 0, 0 });
            Assert.AreEqual(0.725f, p.GetFocus(0), Tol);
            _g.UpdateFocus(p, new[] { 0, 2, 0, 0 });
            Assert.AreEqual(0.508f, p.GetFocus(0), Tol);
        }

        [Test]
        public void UpdateFocus_NoPoints_LeavesFocusUnchanged()
        {
            var p = MakePlayer(_cfg, focus: new[] { 0.4f, 0.2f, 0.2f });
            _g.UpdateFocus(p, new[] { 0, 0, 0, 0 });
            Assert.AreEqual(0.4f, p.GetFocus(0), 1e-6f);
            Assert.Throws<ArgumentException>(() => _g.UpdateFocus(p, new[] { 1, 0, 0 }));
        }

        // ---------------- AC9 - AC12: specialty gain / loss (F4)

        [Test]
        public void Gain_Dim76Focus055_Unlocks_Dim759OrFocus0549_DoesNot()
        {
            int d = D("jungle", "gold_per_min");
            var p = MakePlayer(_cfg, potential: 80f, focus: new[] { 0.55f, 0.2f, 0.2f });
            p.SetDimension("jungle", d, 76f);
            CollectionAssert.AreEqual(new[] { "jungle_king" }, _g.CheckSpecialties(p).Gained);

            var q = MakePlayer(_cfg, potential: 80f, focus: new[] { 0.55f, 0.2f, 0.2f });
            q.SetDimension("jungle", d, 75.9f);
            Assert.IsTrue(_g.CheckSpecialties(q).IsEmpty);

            var r = MakePlayer(_cfg, potential: 80f, focus: new[] { 0.549f, 0.2f, 0.2f });
            r.SetDimension("jungle", d, 76f);
            Assert.IsTrue(_g.CheckSpecialties(r).IsEmpty);
        }

        [Test]
        public void TacticalBrain_Command76_FocusZero_Unlocks()
        {
            var p = MakePlayer(_cfg, lane: "support", potential: 80f, focus: new[] { 0f, 0f, 0f });
            p.SetDimension("support", D("support", "command"), 76f);
            CollectionAssert.AreEqual(new[] { "tactical_brain" }, _g.CheckSpecialties(p).Gained);
        }

        [Test]
        public void Owned_Dim74Focus031_Kept_Dim729OrFocus0299_Lost()
        {
            int d = D("jungle", "gold_per_min");
            Player Owned(float dim, float focus)
            {
                var p = MakePlayer(_cfg, potential: 80f, focus: new[] { focus, 0.2f, 0.2f });
                p.SetDimension("jungle", d, dim);
                p.AddSpecialty("jungle_king");
                return p;
            }
            var keep = Owned(74f, 0.31f);
            Assert.IsTrue(_g.CheckSpecialties(keep).IsEmpty);
            Assert.IsTrue(keep.HasSpecialty("jungle_king"));
            var lowDim = Owned(72.9f, 0.31f);
            CollectionAssert.AreEqual(new[] { "jungle_king" }, _g.CheckSpecialties(lowDim).Lost);
            var lowFocus = Owned(74f, 0.299f);
            CollectionAssert.AreEqual(new[] { "jungle_king" }, _g.CheckSpecialties(lowFocus).Lost);
            Assert.IsFalse(lowFocus.HasSpecialty("jungle_king"));
        }

        [Test]
        public void Lost_Focus029_OnePointOnPhase_Focus0406_NotReunlocked()
        {
            var p = MakePlayer(_cfg, potential: 80f, focus: new[] { 0.29f, 0.2f, 0.2f });
            p.SetDimension("jungle", D("jungle", "gold_per_min"), 80f);
            _g.UpdateFocus(p, new[] { 1, 0, 0, 0 });
            Assert.AreEqual(0.406f, p.GetFocus(0), Tol);
            Assert.IsTrue(_g.CheckSpecialties(p).IsEmpty);
            Assert.IsFalse(p.HasSpecialty("jungle_king"));
        }

        [Test]
        public void JustUnlocked_Focus055_OnePointElsewhere_Focus0460_Kept()
        {
            var p = MakePlayer(_cfg, potential: 80f, focus: new[] { 0.55f, 0.2f, 0.2f });
            p.SetDimension("jungle", D("jungle", "gold_per_min"), 80f);
            Assert.AreEqual(1, _g.CheckSpecialties(p).Gained.Count);
            _g.UpdateFocus(p, new[] { 0, 1, 0, 0 });
            Assert.AreEqual(0.460f, p.GetFocus(0), Tol);
            Assert.IsTrue(_g.CheckSpecialties(p).IsEmpty);
            Assert.IsTrue(p.HasSpecialty("jungle_king"));
        }

        [Test]
        public void AdvanceYear_DeclineBelowLoseValue_LosesSpecialty()
        {
            var p = MakePlayer(_cfg, age: 31, potential: 80f, focus: new[] { 0.6f, 0.2f, 0.2f });
            p.SetDimension("jungle", D("jungle", "gold_per_min"), 76f);
            _g.CheckSpecialties(p);
            Assert.IsTrue(p.HasSpecialty("jungle_king"));
            var ch = _g.AdvanceYear(p); // age 31: -4
            CollectionAssert.AreEqual(new[] { "jungle_king" }, ch.Lost);
        }

        [Test]
        public void Progress_ShowsDimensionAndFocusGaps_NoPotential()
        {
            var p = MakePlayer(_cfg, potential: 80f, focus: new[] { 0.4f, 0.2f, 0.2f });
            p.SetDimension("jungle", D("jungle", "gold_per_min"), 70f);
            var jk = _g.Progress(p).Single(x => x.Def.id == "jungle_king");
            Assert.AreEqual(6f, jk.DimensionGap, 1e-4f);
            Assert.AreEqual(0.15f, jk.FocusGap, 1e-4f);
            Assert.IsFalse(jk.Owned);
            var brain = _g.Progress(p).Single(x => x.Def.id == "tactical_brain");
            Assert.AreEqual(-1, brain.Phase);
            Assert.AreEqual(0f, brain.FocusGap);
        }

        [Test]
        public void NearCap_WithinMarginOfCeiling()
        {
            var p = MakePlayer(_cfg, potential: 65f);
            int d = D("jungle", "kda");
            p.SetDimension("jungle", d, 70.5f);
            Assert.IsTrue(_g.IsNearCap(p, d));
            p.SetDimension("jungle", d, 69.9f);
            Assert.IsFalse(_g.IsNearCap(p, d));
        }

        // ---------------- AC13: hook table (filter + de-duplicate) and aggregation

        [Test]
        public void Hooks_TwoTacticalBrainStarters_CountOnce_PhaseFlatMidPlusOne()
        {
            var a = MakePlayer(_cfg, lane: "top", id: 1); a.AddSpecialty("tactical_brain");
            var b = MakePlayer(_cfg, lane: "mid", id: 2); b.AddSpecialty("tactical_brain");
            var hooks = _g.BuildHooks(Lineup(a, b));
            var totals = HookTotals.Aggregate(hooks, MatchTestFactory.MakeConfig().specialty);
            Assert.AreEqual(1.0, totals.FlatMid, 1e-6);
            Assert.AreEqual(0.15, totals.ExecAdd, 1e-6);
            Assert.AreEqual(0.85, totals.SigmaScale, 1e-6);
            Assert.AreEqual(3, hooks.Count);
        }

        [Test]
        public void Hooks_OffMainLaneOrBench_NotCounted()
        {
            var off = MakePlayer(_cfg, lane: "jungle", id: 1); off.AddSpecialty("jungle_king");
            var lineup = new System.Collections.Generic.Dictionary<string, Player> { { "top", off } }; // plays top, main lane jungle
            Assert.AreEqual(0, _g.BuildHooks(lineup).Count);
            var bench = MakePlayer(_cfg, lane: "mid", id: 2); bench.AddSpecialty("tempo_mid"); // not in the lineup at all
            var starter = MakePlayer(_cfg, lane: "top", id: 3);
            Assert.AreEqual(0, _g.BuildHooks(Lineup(starter)).Count);
            Assert.IsTrue(HookTotals.Aggregate(_g.BuildHooks(Lineup(starter)), MatchTestFactory.MakeConfig().specialty).IsNeutral);
        }

        [Test]
        public void Hooks_DifferentSpecialties_SumAndMultiply()
        {
            var top = MakePlayer(_cfg, lane: "top", id: 1); top.AddSpecialty("solo_king");
            var jg = MakePlayer(_cfg, lane: "jungle", id: 2); jg.AddSpecialty("jungle_king");
            var mid = MakePlayer(_cfg, lane: "mid", id: 3); mid.AddSpecialty("tempo_mid");
            var sup = MakePlayer(_cfg, lane: "support", id: 4); sup.AddSpecialty("iron_wall"); sup.AddSpecialty("tactical_brain");
            var t = HookTotals.Aggregate(_g.BuildHooks(Lineup(top, jg, mid, sup)), MatchTestFactory.MakeConfig().specialty);
            Assert.AreEqual(1.5 + 1.8, t.FlatEarly, 1e-5);
            Assert.AreEqual(1.5 + 1.5 + 1.0, t.FlatMid, 1e-5);
            Assert.AreEqual(1.5, t.StarScaleEarly, 1e-5);
            Assert.AreEqual(0.1 + 0.15, t.ExecAdd, 1e-5);
            Assert.AreEqual(0.9 * 0.85, t.SigmaScale, 1e-5);
        }

        // ---------------- AC14: config validation names the specialty

        [TestCase("lane", "nowhere", "unknown lane")]
        [TestCase("dimension", "farming", "unknown dimension")]
        [TestCase("phase", "midgame", "unknown phase")]
        [TestCase("hook", "phase_bonus", "unknown hook")]
        public void Validate_BadSpecialty_FailsNamingId(string field, string value, string fragment)
        {
            var cfg = MakeConfig();
            var s = cfg.specialty.specialties[1]; // jungle_king
            if (field == "lane") s.lane = value;
            else if (field == "dimension") s.dimension = value;
            else if (field == "phase") s.phase = value;
            else s.effects[0].hook = value;
            var ex = Assert.Throws<InvalidOperationException>(cfg.Validate);
            StringAssert.Contains("jungle_king", ex.Message);
            StringAssert.Contains(fragment, ex.Message);
        }

        [Test]
        public void Validate_IntervalRules_Violations_Fail()
        {
            var cfg = MakeConfig();
            cfg.specialty.unlockFocus = 0.41f; // must be > 0.163 + 0.837 x 0.30 = 0.414
            StringAssert.Contains("interval rule", Assert.Throws<InvalidOperationException>(cfg.Validate).Message);
            cfg = MakeConfig();
            cfg.specialty.loseFocus = 0.47f; cfg.specialty.unlockFocus = 0.56f; // 0.837 x 0.56 = 0.469 < 0.47
            StringAssert.Contains("interval rule", Assert.Throws<InvalidOperationException>(cfg.Validate).Message);
            cfg = MakeConfig();
            Assert.DoesNotThrow(cfg.Validate);
        }

        [Test]
        public void Validate_StarScaleOnMidPhase_Fails()
        {
            var cfg = MakeConfig();
            cfg.specialty.specialties[0].effects[1].phase = "mid";
            StringAssert.Contains("solo_king", Assert.Throws<InvalidOperationException>(cfg.Validate).Message);
        }

        [Test]
        public void GrowthSystem_DimensionNotInItsPhase_FailsNamingId()
        {
            var cfg = MakeConfig();
            cfg.specialty.specialties[1].phase = "late"; // gold_per_min is early for jungle
            cfg.Validate();
            var ex = Assert.Throws<InvalidOperationException>(() => MakeGrowth(cfg));
            StringAssert.Contains("jungle_king", ex.Message);
        }

        // ---------------- AC16: event modifiers (F5)

        [Test]
        public void EventModifiers_MultiplierDoublesB_AddIsThenTimesHead()
        {
            var p = MakePlayer(_cfg, potential: 65f);
            int d = D("jungle", "gold_per_min");
            p.SetDimension("jungle", d, 70f); // head 0.2
            float b = _g.BaseAmount(p, TrainingTarget.Early, 1f);
            Assert.AreEqual(2f * b, GrowthSystem.WithEventModifiers(b, 2f, 0f), 1e-6f);
            float b2 = GrowthSystem.WithEventModifiers(b, 1f, 2f);
            Assert.AreEqual(b + 2f, b2, 1e-6f);
            Assert.AreEqual((b + 2f) * 0.2f, _g.PointGains(p, TrainingTarget.Early, -1, b2)[d], Tol);
        }

        // ---------------- talent.md initial focus

        [Test]
        public void InitialFocus_HighestPhaseMean_TiesEarlyFirst()
        {
            var p = MakePlayer(_cfg, lane: "mid", fill: 50f);
            _g.InitializeFocus(p);
            CollectionAssert.AreEqual(new[] { 0.4f, 0.2f, 0.2f }, new[] { p.GetFocus(0), p.GetFocus(1), p.GetFocus(2) });
            p.SetDimension("mid", D("mid", "damage_share"), 70f); // late mean (70 + 50)/2 = 60
            _g.InitializeFocus(p);
            CollectionAssert.AreEqual(new[] { 0.2f, 0.2f, 0.4f }, new[] { p.GetFocus(0), p.GetFocus(1), p.GetFocus(2) });
        }

        [Test]
        public void EnsureFocus_PlayerWithoutFocus_GetsTalentInitialValues()
        {
            var p = TestConfigFactoryPlayer();
            Assert.IsFalse(p.HasFocus);
            _g.EnsureFocus(p);
            Assert.IsTrue(p.HasFocus);
            Assert.AreEqual(0.4f, p.GetFocus(0), 1e-6f);
        }

        private Player TestConfigFactoryPlayer()
            => EsportsManager.Tests.Players.TestConfigFactory.MakePlayer(_cfg, mainLane: "jungle", traitA: Neutral1, traitB: Neutral2);
    }
}
