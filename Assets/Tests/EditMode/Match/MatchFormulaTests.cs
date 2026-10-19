using System;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;
using EsportsManager.Tests.Teams;
using NUnit.Framework;
using static EsportsManager.Tests.Matches.MatchTestFactory;

namespace EsportsManager.Tests.Matches
{
    /// <summary>Story 004: every formula of numbers.md 4.9 on hand-computed numbers.</summary>
    public class MatchFormulaTests
    {
        private PlayerConfig _pc;
        private TeamConfig _tc;
        private MatchConfig _mc;

        [SetUp]
        public void SetUp()
        {
            _pc = MakePlayerConfig();
            _tc = TeamTestFactory.MakeTeamConfig();
            _mc = MakeConfig();
        }

        // ------------------------------------------------------------ effective rating
        [Test]
        public void EffectiveRating_FullMoodNoStress_EqualsLaneScore()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, mood: 100f, stress: 0f);
            Assert.AreEqual(50f, MatchFormulas.EffectiveRating(_tc, MakeLineup(ps), ps[0], "top"), 1e-4f);
        }

        [Test]
        public void EffectiveRating_ZeroMood_Is70Percent()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, mood: 0f, stress: 0f);
            Assert.AreEqual(35f, MatchFormulas.EffectiveRating(_tc, MakeLineup(ps), ps[0], "top"), 1e-4f);
        }

        [Test]
        public void EffectiveRating_FullStress_LosesKappaS()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, mood: 100f, stress: 100f);
            Assert.AreEqual(40f, MatchFormulas.EffectiveRating(_tc, MakeLineup(ps), ps[0], "top"), 1e-4f); // 50 x (1 - 0.2)
        }

        [Test]
        public void EffectiveRating_ResoluteHalvesStressLoss()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, mood: 100f, stress: 100f, traitA: "resolute"); // test multiplier 0.5
            Assert.AreEqual(45f, MatchFormulas.EffectiveRating(_tc, MakeLineup(ps), ps[0], "top"), 1e-4f);
        }

        [Test]
        public void EffectiveRating_FixedStateLineup_IgnoresPlayerMoodAndStress()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, mood: 100f, stress: 0f);
            var lineup = MakeLineup(ps, fixedState: true, fixedMood: 50f, fixedStress: 50f);
            Assert.AreEqual(50f * 0.85f * 0.9f, MatchFormulas.EffectiveRating(_tc, lineup, ps[0], "top"), 1e-4f);
        }

        // ------------------------------------------------------------ skill score
        [Test]
        public void SkillScore_UniformTeam_IsWeightedMeanWithoutPenalty()
        {
            var lineup = MakeLineup(MakePlayers(_pc, 0, fill: 50f, mood: 50f, stress: 0f));
            Assert.AreEqual(42.5f, MatchFormulas.SkillScore(_tc, _pc, lineup), 1e-4f);
        }

        [Test]
        public void SkillScore_WeakStarter_AppliesShortBoardPenalty()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, mood: 50f, stress: 0f);
            for (int i = 0; i < 6; i++) ps[4].SetDimension("support", i, 20f);
            var lineup = MakeLineup(ps);
            // effective 42.5 x4 and 17: W = 37.4, strength = 37.4 - 0.3 x (37.4 - 17) = 31.28
            Assert.AreEqual(31.28f, MatchFormulas.SkillScore(_tc, _pc, lineup), 1e-3f);
        }

        // ------------------------------------------------------------ style
        [Test]
        public void CommanderScore_IsHighestCommandAmongStarters()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f);
            ps[2].SetDimension("mid", 5, 72f);
            Assert.AreEqual(72f, MatchFormulas.CommanderScore(_mc, _pc, MakeLineup(ps)), 1e-4f);
        }

        [Test]
        public void CommanderScore_AssertiveMultipliesAndIsCappedAt100()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f);
            ps[0] = TeamTestFactory.MakePlayer(_pc, 1, "top", 50f, "assertive", "idealistic");
            ps[0].SetDimension("top", 5, 60f);
            Assert.AreEqual(63f, MatchFormulas.CommanderScore(_mc, _pc, MakeLineup(ps)), 1e-3f);
            ps[0].SetDimension("top", 5, 100f);
            Assert.AreEqual(100f, MatchFormulas.CommanderScore(_mc, _pc, MakeLineup(ps)), 1e-4f);
        }

        [Test]
        public void LaneMatchScore_CountsMainLaneStarters()
        {
            var ps = MakePlayers(_pc, 0);
            Assert.AreEqual(100f, MatchFormulas.LaneMatchScore(_pc, MakeLineup(ps)), 1e-4f);
            (ps[0], ps[2]) = (ps[2], ps[0]); // top <-> mid: two players off their main lane
            Assert.AreEqual(60f, MatchFormulas.LaneMatchScore(_pc, MakeLineup(ps)), 1e-4f);
        }

        [Test]
        public void StyleScore_IsWeightedMeanOfCommanderAndLaneMatch()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f);
            _mc.power.styleWeightCommand = 3f; _mc.power.styleWeightLaneMatch = 1f;
            // commander 50, lane match 100: (3 x 50 + 100) / 4
            Assert.AreEqual(62.5f, MatchFormulas.StyleScore(_mc, _pc, MakeLineup(ps)), 1e-4f);
        }

        // ------------------------------------------------------------ tactic, power, noise, probability
        [Test]
        public void TacticBonus_CounterGetsBonusOtherSideNothing()
        {
            Assert.AreEqual(4f, MatchFormulas.TacticBonus(_mc, MatchTactic.Invade, MatchTactic.Develop));
            Assert.AreEqual(0f, MatchFormulas.TacticBonus(_mc, MatchTactic.Develop, MatchTactic.Invade));
        }

        [Test]
        public void TacticBonus_CyclicTableWorksBothWays()
        {
            foreach (var a in MatchTactics.All)
                foreach (var b in MatchTactics.All)
                {
                    float ab = MatchFormulas.TacticBonus(_mc, a, b), ba = MatchFormulas.TacticBonus(_mc, b, a);
                    if (a == b) Assert.AreEqual(0f, ab);
                    else Assert.AreEqual(4f, ab + ba, "exactly one of the two counters the other: " + a + " vs " + b);
                }
        }

        [Test]
        public void OverallPower_IsWeightedMeanPlusBonus()
        {
            Assert.AreEqual(34f, MatchFormulas.OverallPower(_mc, 40f, 10f, 20f, 4f), 1e-4f); // (120 + 10 + 20) / 5 + 4
        }

        [Test]
        public void Sigma_UsesStressAndResoluteFactor()
        {
            Assert.AreEqual(0.03f, MatchFormulas.Sigma(_mc, 0f, 1f), 1e-6f);
            Assert.AreEqual(0.15f, MatchFormulas.Sigma(_mc, 100f, 1f), 1e-6f);
            Assert.AreEqual(0.06f, MatchFormulas.Sigma(_mc, 50f, 0.5f), 1e-6f);
        }

        [Test]
        public void GamePower_AppliesNoiseAndDeciderOnlyInDecider()
        {
            Assert.AreEqual(55f, MatchFormulas.GamePower(50f, 0.1f, 1.03f, false), 1e-4f);
            Assert.AreEqual(50f * 1.03f, MatchFormulas.GamePower(50f, 0f, 1.03f, true), 1e-4f);
        }

        [Test]
        public void WinProbability_EqualPowerIsHalf_OneKIsLogistic()
        {
            Assert.AreEqual(0.5f, MatchFormulas.WinProbability(_mc, 40f, 40f), 1e-6f);
            Assert.AreEqual(0.7310586f, MatchFormulas.WinProbability(_mc, 48f, 40f), 1e-5f);
            Assert.AreEqual(1f - MatchFormulas.WinProbability(_mc, 48f, 40f), MatchFormulas.WinProbability(_mc, 40f, 48f), 1e-5f);
        }

        [Test]
        public void WinProbability_IsClampedToPMinPMax()
        {
            Assert.AreEqual(0.9f, MatchFormulas.WinProbability(_mc, 100f, 0f), 1e-6f);
            Assert.AreEqual(0.1f, MatchFormulas.WinProbability(_mc, 0f, 100f), 1e-6f);
            Assert.AreEqual(0.9f, MatchFormulas.WinProbability(_mc, 1e9f, 0f), 1e-6f);
        }

        // ------------------------------------------------------------ evaluate and the fixed-state overload
        [Test]
        public void Evaluate_NeutralSides_MatchesHandComputedPower()
        {
            var a = MakeLineup(MakePlayers(_pc, 0, fill: 50f, mood: 50f, stress: 0f), chemistry: 10f, tactic: MatchTactic.Invade);
            var e = MatchFormulas.Evaluate(_mc, _tc, _pc, a, MatchTactic.Develop);
            Assert.AreEqual(42.5f, e.Skill, 1e-4f);
            Assert.AreEqual(75f, e.Style, 1e-4f);
            Assert.AreEqual(4f, e.TacticBonus);
            Assert.AreEqual((3f * 42.5f + 10f + 75f) / 5f + 4f, e.Power, 1e-4f);
            Assert.AreEqual(0.03f, e.Sigma, 1e-6f);
        }

        [Test]
        public void Evaluate_TeamFactorsAreAveragesOfStarters()
        {
            var ps = MakePlayers(_pc, 0, fill: 50f, stress: 40f);
            ps[0] = TeamTestFactory.MakePlayer(_pc, 1, "top", 50f, "resolute", "idealistic", stress: 40f); // resolute 0.5 in the test config
            ps[1] = TeamTestFactory.MakePlayer(_pc, 2, "jungle", 50f, "decisive", "idealistic", stress: 40f);
            var e = MatchFormulas.Evaluate(_mc, _tc, _pc, MakeLineup(ps), MatchTactic.Develop);
            Assert.AreEqual((0.5f + 4f) / 5f, e.ResoluteFactor, 1e-5f);
            Assert.AreEqual((1.03f + 4f) / 5f, e.DeciderFactor, 1e-5f);
            Assert.AreEqual(40f, e.AverageStress, 1e-4f);
        }

        [Test]
        public void SkillScore_FixedOverload_EqualsPlayersSetToFixedValues()
        {
            var ps = MakePlayers(_pc, 0, fill: 60f, mood: 90f, stress: 5f);
            ps[3].SetDimension("adc", 0, 20f);
            var fixedLineup = MakeLineup(ps, fixedState: true, fixedMood: 50f, fixedStress: 50f);
            float viaOverload = MatchFormulas.SkillScore(_tc, _pc, fixedLineup);
            Assert.AreEqual(90f, ps[0].Mood, "evaluation must not change the player");
            foreach (var p in ps) { p.Mood = 50f; p.Stress = 50f; }
            float viaPlayers = MatchFormulas.SkillScore(_tc, _pc, MakeLineup(ps));
            Assert.AreEqual(viaPlayers, viaOverload, 1e-5f);
        }

        [Test]
        public void Evaluate_FixedOverload_EqualsPlayersSetToFixedValues_IncludingSigma()
        {
            var ps = MakePlayers(_pc, 0, fill: 60f, mood: 90f, stress: 5f);
            var viaOverload = MatchFormulas.Evaluate(_mc, _tc, _pc, MakeLineup(ps, chemistry: 12f, fixedState: true), MatchTactic.Develop);
            foreach (var p in ps) { p.Mood = 50f; p.Stress = 50f; }
            var viaPlayers = MatchFormulas.Evaluate(_mc, _tc, _pc, MakeLineup(ps, chemistry: 12f), MatchTactic.Develop);
            Assert.AreEqual(viaPlayers.Power, viaOverload.Power, 1e-5f);
            Assert.AreEqual(viaPlayers.Sigma, viaOverload.Sigma, 1e-6f);
            Assert.AreEqual(viaPlayers.AverageStress, viaOverload.AverageStress, 1e-5f);
        }

        [Test]
        public void TeamStrength_ExistingOverloadsUnchanged()
        {
            var ps = MakePlayers(_pc, 0, fill: 60f, mood: 70f, stress: 30f);
            var map = new System.Collections.Generic.Dictionary<string, Player>();
            for (int i = 0; i < 5; i++) map[Lanes[i]] = ps[i];
            float plain = TeamStrength.Compute(_tc, _pc, map);
            float fixedAtOwn = TeamStrength.Compute(_tc, _pc, map, 70f, 30f);
            Assert.AreEqual(plain, fixedAtOwn, 1e-5f);
            Assert.AreEqual(TeamStrength.EffectiveScore(_tc, ps[0], "top"), TeamStrength.EffectiveScore(_tc, ps[0], "top", 70f, 30f), 1e-6f);
            Assert.AreEqual(0f, TeamStrength.EffectiveScore(_tc, null, "top"));
        }

        [Test]
        public void Evaluate_MissingStarter_Throws()
        {
            var ps = MakePlayers(_pc, 0);
            var lineup = MakeLineup(ps);
            var map = new System.Collections.Generic.Dictionary<string, Player>(lineup.StartersByLane);
            map.Remove("mid");
            var broken = new MatchLineup("X", map, 0f, MatchTactic.Develop);
            var ex = Assert.Throws<InvalidOperationException>(() => MatchFormulas.Evaluate(_mc, _tc, _pc, broken, MatchTactic.Develop));
            StringAssert.Contains("mid", ex.Message);
        }
    }
}
