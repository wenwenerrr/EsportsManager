using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Growth;
using EsportsManager.Leagues;
using EsportsManager.Players;
using NUnit.Framework;
using static EsportsManager.Tests.Leagues.LeagueTestFactory;

namespace EsportsManager.Tests.Talent
{
    /// <summary>design/gdd/talent.md rev 3 Acceptance Criteria 1-10 (Story 013; AC11 = job-offer / card screenshot).</summary>
    public sealed class TalentTests
    {
        private static TalentSystem Make(LeagueWorld w = null, float? talentChance = null)
        {
            w = w ?? World(events: false);
            var cfg = League().talent;
            if (talentChance != null) cfg.talentChance = talentChance.Value;
            return new TalentSystem(w.Players, cfg, w.Growth, 100000);
        }

        private static List<float> Potentials(TalentSystem t, float leaguePop, int n, long seed)
        {
            var rng = new KeyedRandom(seed).Stream("pot");
            float mu = t.SupplyMean(leaguePop);
            var list = new List<float>();
            for (int i = 0; i < n; i++) list.Add(t.DrawPotential(rng, mu, 0f));
            return list;
        }

        [Test]
        public void Potential_NoTalent_LeaguePop40_MeanIn552To572_Clamped40To84()
        {
            var v = Potentials(Make(), 40f, 2000, 7);
            Assert.That(v.Average(), Is.InRange(55.2, 57.2));
            Assert.GreaterOrEqual(v.Min(), 40f);
            Assert.LessOrEqual(v.Max(), 84f);
        }

        [Test]
        public void Potential_LeaguePop30Vs70_MeanDifference569()
        {
            var t = Make();
            double d = Potentials(t, 70f, 2000, 11).Average() - Potentials(t, 30f, 2000, 12).Average();
            Assert.AreEqual(5.69, d, 0.5);
        }

        [Test]
        public void Generate_Potential80_MainDims48To76_OtherDims28To60()
        {
            var t = Make();
            var rng = new KeyedRandom(3).Stream("g");
            for (int i = 0; i < 50; i++)
            {
                var p = t.Generate(rng, "mid", 20, 20, 55f, potential: 80f);
                Assert.AreEqual(80f, p.Potential, 1e-4f);
                foreach (var lane in t_lanes(p))
                    foreach (var v in p.GetRadar(lane))
                        if (lane == "mid") Assert.That(v, Is.InRange(48f - 1e-3f, 76f + 1e-3f));
                        else Assert.That(v, Is.InRange(28f - 1e-3f, 60f + 1e-3f));
            }
        }

        private static IEnumerable<string> t_lanes(Player p) => new[] { "top", "jungle", "mid", "adc", "support" };

        [TestCase(0, 0.4f, 0.2f, 0.2f)]
        [TestCase(1, 0.2f, 0.4f, 0.2f)]
        [TestCase(2, 0.2f, 0.2f, 0.4f)]
        [TestCase(-1, 0.4f, 0.2f, 0.2f)]
        public void InitialFocus_HighestPhaseMean_TiesEarlyFirst(int best, float e, float m, float l)
        {
            var w = World(events: false);
            var p = Make(w).Generate(new KeyedRandom(1).Stream("f"), "mid", 20, 20, 55f, potential: 70f);
            for (int d = 0; d < 6; d++) p.SetDimension("mid", d, 50f);
            if (best >= 0) foreach (int d in w.Growth.Map.PhaseDims("mid", best)) p.SetDimension("mid", d, 60f);
            else { foreach (int d in w.Growth.Map.PhaseDims("mid", 0)) p.SetDimension("mid", d, 60f); foreach (int d in w.Growth.Map.PhaseDims("mid", 1)) p.SetDimension("mid", d, 60f); }
            w.Growth.InitializeFocus(p);
            Assert.AreEqual(e, p.GetFocus(0), 1e-6f); Assert.AreEqual(m, p.GetFocus(1), 1e-6f); Assert.AreEqual(l, p.GetFocus(2), 1e-6f);
        }

        [Test]
        public void TalentWithCommand76_GetsTacticalBrainAtGeneration()
        {
            var w = World(events: false);
            var t = Make(w);
            var rng = new KeyedRandom(5).Stream("b");
            Player found = null;
            for (int i = 0; i < 400 && found == null; i++)
            {
                var p = t.Generate(rng, "support", 20, 20, 55f, potential: 95f);
                if (p.GetDimension("support", w.Growth.Map.CommandIndex("support")) >= 76f) found = p;
            }
            Assert.IsNotNull(found, "a 95-potential player reaches command 76 often enough");
            Assert.IsTrue(found.HasSpecialty("tactical_brain"));
            Assert.Less(found.GetFocus(0) + found.GetFocus(1) + found.GetFocus(2), 0.81f, "focus is the initial 0.4/0.2/0.2: no phase specialty");
        }

        [Test]
        public void ScoutChance_WeakLane010_EmptyLane010_SoleOkLane0075_TwoOkLane005()
        {
            var t = Make();
            Assert.AreEqual(0.10f, t.ScoutTalentChance(2, 60f, 66f), 1e-6f);
            Assert.AreEqual(0.10f, t.ScoutTalentChance(0, 0f, 66f), 1e-6f);
            Assert.AreEqual(0.075f, t.ScoutTalentChance(1, 64f, 66f), 1e-6f);
            Assert.AreEqual(0.05f, t.ScoutTalentChance(2, 64f, 66f), 1e-6f);
        }

        [Test]
        public void Retirement_Signed33Always_31Half_Free30AfterAgeingAlways_29ThirtyPercent()
        {
            var w = World(events: false);
            var t = Make(w);
            var rng = new KeyedRandom(9).Stream("r");
            Player Aged(int age, int contract)
            {
                var p = t.Generate(rng, "top", age, age, 55f);
                p.ContractYearsLeft = contract;
                return p;
            }
            var a = Aged(32, 1); w.Growth.AdvanceYear(a);
            Assert.AreEqual(33, a.Age); Assert.IsTrue(a.IsContractExpired);
            Assert.IsTrue(t.RetiresSigned(a, () => 0.99));
            var b = Aged(30, 1); w.Growth.AdvanceYear(b);
            Assert.IsTrue(t.RetiresSigned(b, () => 0.49)); Assert.IsFalse(t.RetiresSigned(b, () => 0.51));
            var c = Aged(32, 3); w.Growth.AdvanceYear(c);
            Assert.IsFalse(t.RetiresSigned(c, () => 0.0), "contract not expired");
            var f = Aged(30, 0); w.Growth.AdvanceYear(f);
            Assert.AreEqual(31, f.Age); Assert.IsTrue(t.RetiresFree(f, () => 0.99));
            var g = Aged(29, 0); w.Growth.AdvanceYear(g);
            Assert.IsTrue(t.RetiresFree(g, () => 0.29)); Assert.IsFalse(t.RetiresFree(g, () => 0.31));
            var h = Aged(26, 0); w.Growth.AdvanceYear(h);
            Assert.IsFalse(t.RetiresFree(h, () => 0.0));
        }

        [Test]
        public void PoolCap_40Players_RemovesLowest5_YouthExemptAtWindowStart()
        {
            var t = Make();
            var rng = new KeyedRandom(13).Stream("c");
            var pool = Enumerable.Range(0, 40).Select(i => t.Generate(rng, "adc", 22, 22, 55f)).ToList();
            var lowest = pool.OrderBy(p => p.GetLaneScore("adc")).ThenBy(p => p.Id).Take(5).Select(p => p.Id).ToList();
            var copy = new List<Player>(pool);
            var removed = t.ApplyPoolCap(copy, null);
            Assert.AreEqual(35, copy.Count);
            CollectionAssert.AreEquivalent(lowest, removed.Select(p => p.Id));
            var exempt = new HashSet<long> { lowest[0], lowest[1] };
            var copy2 = new List<Player>(pool);
            var removed2 = t.ApplyPoolCap(copy2, p => exempt.Contains(p.Id));
            Assert.AreEqual(35, copy2.Count);
            Assert.IsFalse(removed2.Any(p => exempt.Contains(p.Id)), "this year's youth stay");
        }

        [Test]
        public void SameSeed_SameGeneration()
        {
            var a = World(); var b = World();
            Assert.AreEqual(a.StateHash(), b.StateHash());
            CollectionAssert.AreEqual(a.FreeAgents.Select(p => p.Name), b.FreeAgents.Select(p => p.Name));
            var names = a.Clubs.SelectMany(c => c.Team.Roster).Concat(a.FreeAgents).Select(p => p.Name).ToList();
            Assert.AreEqual(names.Count, names.Distinct().Count(), "names are unique in the league");
        }

        [Test]
        public void Scout_Costs50k_Age17To20_GivenLane_Contract3()
        {
            var w = World(events: false);
            var c = w.Clubs[0];
            long funds = c.Team.Funds;
            int n = c.Team.Roster.Count;
            var p = w.Scout(c, "support");
            Assert.AreEqual(funds - 50000, c.Team.Funds);
            Assert.That(p.Age, Is.InRange(17, 20));
            Assert.AreEqual("support", p.MainLane);
            Assert.AreEqual(3, p.ContractYearsLeft);
            Assert.AreEqual(n + 1, c.Team.Roster.Count);
            Assert.Throws<ArgumentException>(() => w.Scout(c, "coach"));
        }

        [Test]
        public void YearRollover_AgesPlayers_SuppliesYouth_KeepsPoolCapped()
        {
            var w = World(events: false);
            var ages = w.Clubs.SelectMany(c => c.Team.Roster).ToDictionary(p => p.Id, p => p.Age);
            int next = (int)w.Talent.NextId;
            new TransferWindow(w, 0, -1).Continue(); // all-AI window: step 1 rollover ... step 7 re-sign
            foreach (var c in w.Clubs)
                foreach (var p in c.Team.Roster)
                    if (ages.TryGetValue(p.Id, out int a)) Assert.AreEqual(a + 1, p.Age);
            Assert.GreaterOrEqual((int)w.Talent.NextId, next + 11, "8 youth + 3 free agents (+ scouted players)");
            Assert.LessOrEqual(w.FreeAgents.Count, 35);
            Assert.IsTrue(w.Clubs.All(c => c.Team.Roster.Count >= 6));
        }
    }
}
