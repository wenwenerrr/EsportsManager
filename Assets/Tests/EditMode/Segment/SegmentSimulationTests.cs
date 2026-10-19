using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EsportsManager.Tests.Segments
{
    /// <summary>
    /// Story 003 AC9 (tension simulation) and the shipped-config integration run. These read the SHIPPED json files
    /// through AssetDatabase, so they are [Category("Integration")]; everything is seeded and deterministic.
    /// </summary>
    public class SegmentSimulationTests
    {
        private const int Seed = 20261016;

        private sealed class World
        {
            public PlayerConfig Players; public TeamConfig TeamCfg; public SegmentConfig Cfg;
            public Team Team; public SegmentRunner Runner; public List<Player> Starters;
        }

        private static string Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, "missing " + path);
            return asset.text;
        }

        /// <summary>Same seed = same pool = same team, so the two groups differ ONLY in what they do.</summary>
        private static World Build(long funds = 1000000)
        {
            var w = new World
            {
                Players = PlayerConfigLoader.FromJson(Load("Assets/Data/player_config.json")),
                TeamCfg = TeamConfigLoader.FromJson(Load("Assets/Data/team_config.json")),
                Cfg = SegmentConfigLoader.FromJson(Load("Assets/Data/segment_config.json")),
            };
            var pool = new PlayerGenerator(w.Players, new SeededRandomSource(Seed)).GeneratePool();
            w.Team = new Team(w.TeamCfg, w.Players, new SeededRandomSource(Seed + 1), funds);
            w.Starters = new List<Player>();
            foreach (var lane in w.Players.lanes)
            {
                var best = pool.Where(p => p.MainLane == lane.id).OrderByDescending(p => p.GetLaneScore(lane.id)).First();
                w.Team.AddPlayer(best);
                w.Team.SetStarter(lane.id, best);
                w.Starters.Add(best);
            }
            var bench = pool.Where(p => !w.Team.IsRostered(p)).OrderByDescending(p => p.GetLaneScore(p.MainLane)).First();
            w.Team.AddPlayer(bench);
            w.Runner = new SegmentRunner(w.Cfg, w.Players, w.Team, new SeededRandomSource(Seed + 2));
            return w;
        }

        private static int LowestMainDimension(Player p, PlayerConfig pc)
        {
            int dims = pc.FindLane(p.MainLane).dimensions.Length, best = 0;
            for (int i = 1; i < dims; i++) if (p.GetDimension(p.MainLane, i) < p.GetDimension(p.MainLane, best)) best = i;
            return best;
        }

        /// <summary>All points on training: starters in lane order get 2 points each (max_per_player) on their weakest dimension.</summary>
        private static SegmentPlan TrainPlan(World w)
        {
            var plan = new SegmentPlan();
            int ap = w.Cfg.actionPoints.perSegment, per = w.Cfg.actionPoints.maxPerPlayer;
            foreach (var p in w.Starters)
                for (int k = 0; k < per && plan.PointsUsed < ap; k++) plan.Train(p.Id, LowestMainDimension(p, w.Players));
            return plan;
        }

        /// <summary>Accept every pending business offer that fits, then spend the rest on streams (starters first).</summary>
        private static SegmentPlan StreamBusinessPlan(World w)
        {
            var plan = new SegmentPlan();
            int ap = w.Cfg.actionPoints.perSegment, per = w.Cfg.actionPoints.maxPerPlayer;
            var used = new Dictionary<long, int>();
            foreach (var offer in w.Runner.GetState().PendingOffers)
            {
                used.TryGetValue(offer.PlayerId, out int n);
                if (plan.PointsUsed >= ap || n >= per) continue;
                plan.AcceptOffer(offer.Id); used[offer.PlayerId] = n + 1;
            }
            foreach (var p in w.Starters)
            {
                used.TryGetValue(p.Id, out int n);
                for (; n < per && plan.PointsUsed < ap; n++) plan.Stream(p.Id);
                used[p.Id] = n;
            }
            return plan;
        }

        /// <summary>Mixed management: accept at most one offer, 1 sponsorship, then 2 stream / 2 train / 2 rest on free starters.</summary>
        private static SegmentPlan MixedPlan(World w)
        {
            var plan = new SegmentPlan();
            var used = new Dictionary<long, int>();
            foreach (var offer in w.Runner.GetState().PendingOffers) { plan.AcceptOffer(offer.Id); used[offer.PlayerId] = 1; break; }
            if (w.Runner.GetState().SponsorAttemptsRemaining > 0) plan.Sponsor(); // v3.2: contract slots may be full
            Player Free() => w.Starters.First(p => !used.ContainsKey(p.Id));
            var a = Free(); used[a.Id] = 2; plan.Stream(a.Id).Stream(a.Id);
            var b = Free(); used[b.Id] = 2; int dim = LowestMainDimension(b, w.Players); plan.Train(b.Id, dim).Train(b.Id, dim);
            var c = Free(); used[c.Id] = 2; plan.Rest(c.Id).Rest(c.Id);
            return plan;
        }

        /// <summary>Streams only (no business), starters first.</summary>
        private static SegmentPlan StreamPlan(World w)
        {
            var plan = new SegmentPlan();
            foreach (var p in w.Starters)
                for (int k = 0; k < w.Cfg.actionPoints.maxPerPlayer && plan.PointsUsed < w.Cfg.actionPoints.perSegment; k++) plan.Stream(p.Id);
            return plan;
        }

        private sealed class Outcome
        {
            public string Kind; public int Segments; public long Funds; public float Strength; public float Skill; public float Stress; public float Mood;
            public long Income, Salary, Livestream, Business, Sponsorship;
            private string Pct(long v) => (Salary == 0 ? 0 : 100.0 * v / Salary).ToString("F1") + "%";
            public override string ToString() =>
                Kind + " segs=" + Segments + " funds=" + Funds + " strength=" + Strength.ToString("F2") + " skill=" + Skill.ToString("F2")
                + " stress=" + Stress.ToString("F1") + " mood=" + Mood.ToString("F1")
                + " | per-seg salary=" + Salary / Segments + " live=" + Livestream / Segments + " (" + Pct(Livestream) + ") biz=" + Business / Segments
                + " (" + Pct(Business) + ") spon=" + Sponsorship / Segments + " (" + Pct(Sponsorship) + ") income=" + Income / Segments + " (" + Pct(Income) + ")";
        }

        private static Outcome RunGroup(string kind, int segments)
        {
            var w = Build();
            var o = new Outcome { Kind = kind, Segments = segments };
            for (int i = 0; i < segments; i++)
            {
                w.Runner.BeginSegment();
                var plan = kind == "train" ? TrainPlan(w) : kind == "mixed" ? MixedPlan(w) : kind == "stream" ? StreamPlan(w) : StreamBusinessPlan(w);
                var s = w.Runner.Resolve(plan, new SegmentContext { SegmentsPerYear = 12, SegmentIndexInYear = i % 12 });
                o.Income += s.Report.Income; o.Salary += s.Report.SalaryShare;
                o.Livestream += s.LivestreamIncome; o.Business += s.BusinessIncome; o.Sponsorship += s.SponsorshipIncome;
            }
            o.Funds = w.Team.Funds; o.Strength = w.Team.Strength(); o.Stress = w.Team.AverageStarterStress(); o.Mood = w.Team.AverageStarterMood();
            o.Skill = w.Starters.Average(p => p.GetLaneScore(p.MainLane));
            return o;
        }

        /// <summary>AC9 (numbers.md v3 values): money vs strength tension after 4 segments, seed 20261016.</summary>
        [Test]
        [Category("Integration")]
        public void TensionSimulation_FourSegments_TrainingHasLessMoneyButMoreStrength()
        {
            var train = RunGroup("train", 4);
            var live = RunGroup("live", 4);
            Debug.Log("SIM4 train: " + train);
            Debug.Log("SIM4 live : " + live);
            Assert.Less(train.Funds, live.Funds, "training group must have less money");
            Assert.Greater(train.Strength, live.Strength, "training group must be stronger");
            Assert.Greater(train.Skill, live.Skill);
        }

        /// <summary>
        /// Design intent "rest is valuable but not forced" (numbers.md 4.7 v3): over 12 segments the mixed plan (with rest)
        /// ends stronger than all-train-no-rest. Observation only (not asserted): all-train starter stress levels off near 60.
        /// </summary>
        [Test]
        [Category("Integration")]
        public void MixedWithRest_TwelveSegments_StrongerThanAllTrainNoRest()
        {
            var train = RunGroup("train", 12);
            var mixed = RunGroup("mixed", 12);
            Debug.Log("SIM12 train: " + train);
            Debug.Log("SIM12 mixed: " + mixed);
            Assert.Greater(mixed.Strength, train.Strength);
        }

        /// <summary>Rough economy check (numbers.md 4.10 target 40/25/20/15): logs 12-segment averages per income line.</summary>
        [Test]
        [Category("Integration")]
        public void EconomyCheck_TwelveSegments_IncomeOrdering()
        {
            var train = RunGroup("train", 12); var mixed = RunGroup("mixed", 12); var live = RunGroup("live", 12); var stream = RunGroup("stream", 12);
            foreach (var o in new[] { train, mixed, live, stream }) Debug.Log("ECON " + o);
            Assert.AreEqual(0, train.Income);
            Assert.Greater(mixed.Income, 0);
            Assert.Greater(live.Livestream, mixed.Livestream);
        }

        [Test]
        [Category("Integration")]
        public void ShippedConfigs_RunSeveralMixedSegments_StaysInRange()
        {
            var w = Build();
            for (int i = 0; i < 3; i++)
            {
                w.Runner.BeginSegment();
                var plan = new SegmentPlan().Train(w.Starters[0].Id, 0).Stream(w.Starters[1].Id).Rest(w.Starters[2].Id).Sponsor();
                var s = w.Runner.Resolve(plan, new SegmentContext { SegmentsPerYear = 12, SegmentIndexInYear = i, MatchResults = new[] { true, false } });
                Assert.AreEqual(4, s.PointsUsed);
                Assert.AreEqual(w.Cfg.actionPoints.perSegment - 4, s.PointsUnused);
                Assert.Greater(s.Report.SalaryShare, 0);
            }
            foreach (var p in w.Team.Roster)
            {
                Assert.That(p.Mood, Is.InRange(0f, 100f));
                Assert.That(p.Stress, Is.InRange(0f, 100f));
                Assert.That(p.Popularity, Is.InRange(0f, 100f));
            }
        }
    }
}
