using System.IO;
using System.Linq;
using EsportsManager.Growth;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Session;
using NUnit.Framework;

namespace EsportsManager.Tests.Growth
{
    /// <summary>
    /// Story 012 against the SHIPPED configs (Assets/Data): the 16-entry specialty table loads and fits the match phase mapping,
    /// every hook maps to a match effect, and the session wires growth (initial focus, training, three phase strengths).
    /// </summary>
    [Category("Integration")]
    public class GrowthIntegrationTests
    {
        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);

        private static GameSession Session(int seed = GameSession.DefaultSeed) => GameSession.FromJson(Data("player_config.json"),
            Data("team_config.json"), Data("segment_config.json"), Data("match_config.json"), Data("league_config.json"), seed);

        [Test]
        public void ShippedConfig_SixteenSpecialties_FitPhaseMapping_AndMapToHooks()
        {
            var pc = PlayerConfigLoader.FromJson(Data("player_config.json"));
            var mc = MatchConfigLoader.FromJson(Data("match_config.json"));
            var g = new GrowthSystem(pc, new GrowthPhaseMap(new PhaseLayout(mc, pc)));
            Assert.AreEqual(16, pc.specialty.specialties.Length);
            foreach (var lane in pc.lanes)
                for (int k = 0; k < 3; k++)
                    Assert.AreEqual(1, pc.specialty.specialties.Count(s => s.lane == lane.id && SpecialtyTable.PhaseIndex(s.phase) == k), lane.id + " phase " + k);
            Assert.AreEqual(1, pc.specialty.specialties.Count(s => s.IsAnyLane));
            foreach (var s in pc.specialty.specialties)
                foreach (var e in s.effects) Assert.IsNotNull(GrowthSystem.ToEffect(e), s.id);
            Assert.AreEqual(2.3f, g.Find("top_engage").effects[0].value, 1e-6f); // growth.md calibrated table
        }

        [Test]
        public void Session_GeneratedPlayersHaveInitialFocus_PhaseStrengthsShown()
        {
            var s = Session();
            Assert.IsNotNull(s.Growth);
            foreach (var p in s.Team.Roster) Assert.IsTrue(p.HasFocus);
            // Story 013: the roster trained through the warm-up year; a freshly generated player has the talent.md initial focus.
            var fresh = s.World.Talent.Generate(s.World.Keys.Stream("test", 1), "mid", 18, 18, 55f);
            var f = new[] { fresh.GetFocus(0), fresh.GetFocus(1), fresh.GetFocus(2) };
            Assert.AreEqual(0.8f, f.Sum(), 1e-5f, "one phase 0.4, two 0.2");
            var team = s.GetTeamData();
            Assert.IsNotNull(team.PhaseStrength);
            Assert.AreEqual(3, team.PhaseStrength.Length);
            Assert.AreEqual((float)s.Tournament.Simulator.Preview(MatchLineup.FromTeam(s.Team, s.CurrentTactic, s.Tournament.Hooks(s.Team))).Strength[1],
                team.PhaseStrength[1], 1e-4f);
        }

        [Test]
        public void Session_TrainPhaseWithFocus_ResolvesAndUpdatesFocusAndCards()
        {
            var s = Session();
            var p = s.Team.Roster[0];
            int focusDim = s.Growth.Map.PhaseDims(p.MainLane, 0).Last();
            Assert.IsTrue(s.TryAddTrain(p.Id, TrainingTarget.Early, focusDim, out var e), e.Code.ToString());
            Assert.IsFalse(s.TryAddTrain(p.Id, TrainingTarget.Early, s.Growth.Map.CommandIndex(p.MainLane), out e));
            Assert.AreEqual(EsportsManager.Segments.PlanErrorCode.DimensionOutOfRange, e.Code);
            float before = p.GetFocus(0);
            var pv = s.PreviewTrainingPoint(p.Id, TrainingTarget.Early, focusDim);
            Assert.Less(pv.Low[focusDim], pv.High[focusDim]);
            var r = s.Resolve();
            var ps = r.Summary.Players.First(x => x.PlayerId == p.Id);
            Assert.AreEqual(1, ps.Training.Count);
            Assert.That(ps.Training[0].Noise, Is.InRange(0.85f, 1.15f));
            Assert.AreEqual(0.837f * before + 0.163f, p.GetFocus(0), 1e-4f);
            Assert.IsNotNull(r.PhaseStrengthBefore);
            Assert.IsNotNull(r.PhaseStrengthAfter);
            var card = s.ToCard(p);
            Assert.AreEqual(p.GetFocus(0), card.Focus[0], 1e-6f);
            Assert.AreEqual(3, card.SpecialtyProgress.Count(x => x.Phase >= 0));
        }

        [Test]
        public void Session_ScriptedPlan_TrainsPhasesAndStaysDeterministic()
        {
            var a = Session(); var b = Session();
            for (int i = 0; i < 2; i++) { a.ApplyScriptedPlan(); a.ResolveAndAdvance(); b.ApplyScriptedPlan(); b.ResolveAndAdvance(); }
            for (int i = 0; i < a.Team.Roster.Count; i++)
            {
                CollectionAssert.AreEqual(a.Team.Roster[i].GetRadar(a.Team.Roster[i].MainLane), b.Team.Roster[i].GetRadar(b.Team.Roster[i].MainLane));
                Assert.AreEqual(a.Team.Roster[i].GetFocus(0), b.Team.Roster[i].GetFocus(0));
            }
            Assert.IsTrue(a.LastResult.Summary.Players.Any(x => x.TrainPoints > 0));
        }
    }
}
