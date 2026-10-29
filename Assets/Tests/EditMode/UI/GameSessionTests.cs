using System.IO;
using System.Linq;
using EsportsManager.Segments;
using EsportsManager.Session;
using NUnit.Framework;

namespace EsportsManager.Tests.UI
{
    /// <summary>Story 009: GameSession built from the shipped configs (fixture = the real JSON files).</summary>
    public class GameSessionTests
    {
        private static GameSession Make(int seed) => GameSession.FromJson(
            File.ReadAllText("Assets/Data/player_config.json"),
            File.ReadAllText("Assets/Data/team_config.json"),
            File.ReadAllText("Assets/Data/segment_config.json"), seed);

        [Test]
        public void FromJson_ShippedConfigs_RosterOfSixWithOneStarterPerLane()
        {
            var s = Make(GameSession.DefaultSeed);
            Assert.AreEqual(6, s.Team.Roster.Count);
            var starters = s.Team.StartersByLane();
            Assert.AreEqual(5, starters.Count);
            foreach (var kv in starters) Assert.AreEqual(kv.Key, kv.Value.MainLane);
            Assert.AreEqual(1, s.GetRosterData().Count(c => !c.IsStarter));
            Assert.IsTrue(s.State.IsOpen);
            Assert.AreEqual(GameSession.DefaultFunds, s.GetTeamData().Funds);
        }

        [Test]
        public void FromJson_SameSeed_SameRosterAndCards()
        {
            var a = Make(42).GetRosterData();
            var b = Make(42).GetRosterData();
            Assert.AreEqual(a.Select(c => c.Name + c.Age + c.Salary + c.PotentialLow + c.PotentialHigh),
                            b.Select(c => c.Name + c.Age + c.Salary + c.PotentialLow + c.PotentialHigh));
        }

        [Test]
        public void PlayerCardData_HasNoTruePotentialMember_AndShowsEstimateRange()
        {
            var members = typeof(PlayerCardData).GetFields().Select(f => f.Name)
                .Concat(typeof(PlayerCardData).GetProperties().Select(p => p.Name)).ToArray();
            Assert.IsFalse(members.Any(m => m == "Potential" || m == "TruePotential"));
            var s = Make(GameSession.DefaultSeed);
            foreach (var p in s.Team.Roster)
            {
                var card = s.ToCard(p);
                var est = p.GetPotentialEstimate();
                Assert.AreEqual(est.Low, card.PotentialLow);
                Assert.AreEqual(est.High, card.PotentialHigh);
                Assert.AreEqual(2, card.TraitNames.Length);
                Assert.AreEqual(6, card.DimensionValues.Length);
            }
        }

        [Test]
        public void TryAdd_OverPerPlayerLimit_IsRefusedAndPlanUnchanged()
        {
            var s = Make(GameSession.DefaultSeed);
            var id = s.Team.Roster[0].Id;
            int max = s.State.MaxPerPlayer;
            for (int i = 0; i < max; i++) Assert.IsTrue(s.TryAddRest(id, out _));
            Assert.IsFalse(s.TryAddStream(id, out var e));
            Assert.AreEqual(PlanErrorCode.PlayerOverLimit, e.Code);
            Assert.AreEqual(max, s.Plan.PointsUsed);
            Assert.IsTrue(s.UndoLast(id));
            Assert.AreEqual(max - 1, s.Plan.PointsUsed);
        }

        [Test]
        public void ScriptedPlan_UsesAllPoints_ThenOverTotalIsRefused()
        {
            var s = Make(GameSession.DefaultSeed);
            s.ApplyScriptedPlan();
            Assert.IsTrue(s.TryValidate(out _));
            Assert.AreEqual(0, s.Preview().PointsRemaining);
            Assert.IsFalse(s.TryAddRest(s.Team.Roster[0].Id, out var e));
            Assert.That(e.Code, Is.EqualTo(PlanErrorCode.OverTotalPoints).Or.EqualTo(PlanErrorCode.PlayerOverLimit));
        }

        [Test]
        public void ResolveThenBeginNext_FourSegments_AdvancesWithoutErrors()
        {
            var s = Make(GameSession.DefaultSeed);
            for (int i = 1; i <= 4; i++)
            {
                s.ApplyScriptedPlan();
                var r = s.Resolve();
                Assert.AreEqual(i, r.SegmentNumber);
                Assert.IsTrue(s.AwaitingNext);
                Assert.AreEqual(r.FundsAfter, s.Team.Funds);
                Assert.AreEqual(0, s.UnansweredOffers().Count);
                s.BeginNext();
                Assert.AreEqual(i + 1, s.GetTeamData().SegmentNumber);
                Assert.AreEqual(0, s.Plan.PointsUsed);
            }
        }

        [Test]
        public void Offers_AcceptAndDecline_UpdateUnansweredList()
        {
            var s = GameSession.FromJsonWithOffer(File.ReadAllText("Assets/Data/player_config.json"),
                File.ReadAllText("Assets/Data/team_config.json"), File.ReadAllText("Assets/Data/segment_config.json"), 1);
            var offers = s.UnansweredOffers();
            Assert.Greater(offers.Count, 0);
            Assert.IsTrue(s.CanAcceptOffer(offers[0].Id, out _));
            Assert.IsTrue(s.TryAcceptOffer(offers[0].Id, out _));
            Assert.IsFalse(s.UnansweredOffers().Exists(o => o.Id == offers[0].Id));
            Assert.AreEqual(1, s.Plan.PointsUsed);
            foreach (var o in s.UnansweredOffers()) s.DeclineOffer(o.Id);
            Assert.AreEqual(0, s.UnansweredOffers().Count);
        }

        [Test]
        public void ShippedConfig_PlayerNamesAreUniqueIds_AndTraitNamesHaveNoSlash()
        {
            var s = Make(GameSession.DefaultSeed);
            var names = s.Team.Roster.Select(p => p.Name).Concat(s.FreeAgents.Select(p => p.Name)).ToList();
            Assert.AreEqual(names.Count, names.Distinct().Count());
            Assert.AreEqual("沉稳", s.TraitName("reserved"));
            Assert.AreEqual("认真", s.TraitName("earnest"));
            Assert.AreEqual("脆弱", s.TraitName("fragile"));
        }
    }
}
