using System;
using System.Collections.Generic;
using System.Linq;
using EsportsManager.Players;
using EsportsManager.Teams;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EsportsManager.Tests.Teams
{
    /// <summary>EditMode tests for Story 002 (team triangle and chemistry). Configs are built in memory.</summary>
    public class TeamTriangleTests
    {
        private static readonly string[] Lanes = { "top", "jungle", "mid", "adc", "support" };
        private static readonly string[] Personalities = { "p1", "p2", "p3" };

        // One dimension per lane, weight 1: a player's lane score equals his fill value (x discount off-lane).
        private static PlayerConfig MakePlayerConfig() => new PlayerConfig
        {
            offLaneDiscount = 0.5f,
            personalities = Personalities,
            lanes = Lanes.Select(l => new LaneDef
            {
                id = l, displayName = l,
                dimensions = new[] { new DimensionDef { id = "d", displayName = "d", weight = 1f } }
            }).ToArray(),
        };

        private static TeamConfig MakeConfig() => new TeamConfig
        {
            strengthAggregation = TeamConfig.AggregationWeightedAverage,
            laneWeights = Lanes.Select(l => new LaneWeight { lane = l, weight = 1f }).ToArray(),
            starterPopularityShare = 0.5f,
            initialFunds = 1000, initialOwnPopularity = 20, initialChemistry = 50,
            segmentsPerYear = 4,
            sponsorshipBase = 100, sponsorshipPerPopularity = 10, sponsorshipPerWinRate = 200,
            recentResultsWindow = 4, noResultsWinRate = 0.5f,
            livestreamBase = 50, livestreamPerPopularity = 5,
            chemistryRisePerSegment = 10, chemistryDropPerReplacement = 20,
            compatibleModifier = 0.5f, conflictModifier = -0.5f,
            pairings = new[]
            {
                new PersonalityPairing { a = "p1", b = "p2", relation = TeamConfig.RelationCompatible },
                new PersonalityPairing { a = "p1", b = "p3", relation = TeamConfig.RelationConflict },
            },
        };

        private static void Validate(TeamConfig c) => c.Validate(Lanes, Personalities);

        private static Player MakePlayer(PlayerConfig pc, string lane, float score = 60f, float pop = 40f,
            string personality = "p2", int salary = 4000)
        {
            var radar = pc.lanes.ToDictionary(l => l.id, l => new[] { score });
            return new Player(pc, "n_" + lane, 22, lane, personality, 50f, pop, 50f, 50f, salary, radar);
        }

        private static List<Player> FullFive(PlayerConfig pc, float score = 60f, float pop = 40f,
            string personality = "p2", int salary = 4000) =>
            Lanes.Select(l => MakePlayer(pc, l, score, pop, personality, salary)).ToList();

        private static Team MakeTeam(TeamConfig cfg, PlayerConfig pc, IList<Player> five, IList<Player> subs = null)
        {
            var t = new Team(cfg, "T");
            foreach (var p in five) t.AddPlayer(p);
            if (subs != null) foreach (var p in subs) t.AddPlayer(p);
            foreach (var p in five) t.SetStarter(p.MainLane, p);
            return t;
        }

        // AC1: four numbers
        [Test]
        public void NewTeam_HasInitialFundsPopularityChemistryFromConfig()
        {
            var t = new Team(MakeConfig(), "T");
            Assert.AreEqual(1000d, t.Funds);
            Assert.AreEqual(20f, t.OwnPopularity);
            Assert.AreEqual(50f, t.Chemistry);
            Assert.AreEqual(0f, t.Strength);
        }

        [Test]
        public void Chemistry_IsClampedTo0To100()
        {
            var t = new Team(MakeConfig(), "T");
            t.Chemistry = 150f; Assert.AreEqual(100f, t.Chemistry);
            t.Chemistry = -5f; Assert.AreEqual(0f, t.Chemistry);
        }

        // AC2: strength
        [Test]
        public void Strength_WeightedAverage_UsesLaneScoresAndWeights()
        {
            var pc = MakePlayerConfig(); var cfg = MakeConfig();
            cfg.laneWeights.First(w => w.lane == "mid").weight = 3f; // weights 1,1,3,1,1 (sum 7)
            var five = Lanes.Select((l, i) => MakePlayer(pc, l, score: 10f * (i + 1))).ToList(); // 10..50
            var t = MakeTeam(cfg, pc, five);
            // (10+20+3*30+40+50)/7 = 210/7
            Assert.AreEqual(30f, t.Strength, 1e-4f);
        }

        [Test]
        public void Strength_WeightedSum_IsConfigurable()
        {
            var pc = MakePlayerConfig(); var cfg = MakeConfig();
            cfg.strengthAggregation = TeamConfig.AggregationWeightedSum;
            foreach (var w in cfg.laneWeights) w.weight = 0.2f;
            var t = MakeTeam(cfg, pc, FullFive(pc, score: 50f));
            Assert.AreEqual(50f, t.Strength, 1e-3f);
        }

        [Test]
        public void Strength_StarterOffLane_UsesDiscountedLaneScore()
        {
            var pc = MakePlayerConfig(); var cfg = MakeConfig();
            var five = FullFive(pc, score: 80f);
            var t = MakeTeam(cfg, pc, five);
            float before = t.Strength; // 80
            // a mid main-laner moves to top: 80 * 0.5 = 40 in that slot
            var offLane = MakePlayer(pc, "mid", score: 80f);
            t.AddPlayer(offLane);
            t.SetStarter("top", offLane);
            Assert.AreEqual(80f, before, 1e-4f);
            Assert.AreEqual((40f + 80f * 4) / 5f, t.Strength, 1e-4f);
        }

        [Test]
        public void Strength_EmptySlotScoresZero()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, score: 50f).Take(4).ToList());
            Assert.IsFalse(t.IsLineupComplete);
            Assert.AreEqual(40f, t.Strength, 1e-4f);
        }

        // AC3: popularity
        [Test]
        public void Popularity_BlendsStarterAverageAndOwn()
        {
            var pc = MakePlayerConfig(); var cfg = MakeConfig(); // share 0.5, own 20
            var t = MakeTeam(cfg, pc, FullFive(pc, pop: 60f));
            Assert.AreEqual(0.5f * 60f + 0.5f * 20f, t.Popularity, 1e-4f);
            t.OwnPopularity = 80f;
            Assert.AreEqual(70f, t.Popularity, 1e-4f);
        }

        [Test]
        public void Popularity_SubstitutesDoNotCount()
        {
            var pc = MakePlayerConfig();
            var sub = MakePlayer(pc, "mid", pop: 100f);
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, pop: 40f), new[] { sub });
            Assert.AreEqual(30f, t.Popularity, 1e-4f);
        }

        // AC4: finance
        [Test]
        public void Settle_NoResults_UsesNoResultsWinRateAndFormula()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, pop: 40f)); // popularity 30
            var f = t.SettleSegment(null);
            Assert.AreEqual(100 + 10 * 30 + 200 * 0.5, f.Sponsorship, 1e-6);   // 500
            Assert.AreEqual(50 + 5 * 30, f.Livestream, 1e-6);                  // 200
            Assert.AreEqual(5 * 4000 / 4.0, f.Expenses, 1e-6);                 // 5000
            Assert.AreEqual(700 - 5000, f.Net, 1e-6);
            Assert.AreEqual(1000 + 700 - 5000, t.Funds, 1e-6);
        }

        [Test]
        public void Settle_ExpensesIncludeSubstitutes()
        {
            var pc = MakePlayerConfig();
            var sub = MakePlayer(pc, "mid", salary: 8000);
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc), new[] { sub });
            Assert.AreEqual((5 * 4000 + 8000) / 4.0, t.SettleSegment(null).Expenses, 1e-6);
        }

        [Test]
        public void Settle_SponsorshipRisesWithPopularity()
        {
            var pc = MakePlayerConfig();
            var low = MakeTeam(MakeConfig(), pc, FullFive(pc, pop: 10f)).SettleSegment(null).Sponsorship;
            var high = MakeTeam(MakeConfig(), pc, FullFive(pc, pop: 90f)).SettleSegment(null).Sponsorship;
            Assert.Greater(high, low);
        }

        [Test]
        public void Settle_SponsorshipRisesWithRecentResults_AndOnlyWindowCounts()
        {
            var pc = MakePlayerConfig();
            double Spons(params MatchResult[] r) =>
                MakeTeam(MakeConfig(), pc, FullFive(pc)).SettleSegment(r).Sponsorship;
            const MatchResult W = MatchResult.Win, L = MatchResult.Loss;
            Assert.Greater(Spons(W, W, W, W), Spons(L, L, L, L));
            // window is 4: only the last four count, so old losses are ignored
            Assert.AreEqual(Spons(W, W, W, W), Spons(L, L, W, W, W, W), 1e-6);
            Assert.AreEqual(Spons(W, L, W, L), Spons(W, W, W, L, W, L), 1e-6);
        }

        [Test]
        public void Settle_FundsCanGoNegative()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, salary: 1000000));
            t.SettleSegment(null);
            t.SettleSegment(null);
            Assert.Less(t.Funds, 0d);
        }

        // AC5: chemistry
        [Test]
        public void Settle_SameFive_ChemistryRisesByConfiguredAmount()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, personality: "p3")); // p3+p3 unlisted: neutral
            t.SettleSegment(null);
            Assert.AreEqual(60f, t.Chemistry, 1e-4f);
            t.SettleSegment(null);
            Assert.AreEqual(70f, t.Chemistry, 1e-4f);
        }

        [Test]
        public void Settle_ChemistryCapsAt100()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, personality: "p3"));
            for (int i = 0; i < 20; i++) t.SettleSegment(null);
            Assert.AreEqual(100f, t.Chemistry);
        }

        [Test]
        public void Settle_IncompleteLineup_ChemistryDoesNotRise()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, personality: "p3").Take(4).ToList());
            t.SettleSegment(null);
            Assert.AreEqual(50f, t.Chemistry);
        }

        [Test]
        public void SetStarter_ReplacingStarter_DropsChemistryByConfiguredAmount()
        {
            var pc = MakePlayerConfig();
            var sub = MakePlayer(pc, "mid", personality: "p3");
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, personality: "p3"), new[] { sub });
            t.SetStarter("mid", sub);
            Assert.AreEqual(30f, t.Chemistry, 1e-4f);
        }

        [Test]
        public void SetStarter_FillingEmptySlotOrSamePlayer_IsFree()
        {
            var pc = MakePlayerConfig();
            var five = FullFive(pc);
            var t = MakeTeam(MakeConfig(), pc, five);
            t.SetStarter("mid", five[2]);
            Assert.AreEqual(50f, t.Chemistry);
        }

        [Test]
        public void ChemistryDrop_ClampsAtZero()
        {
            var pc = MakePlayerConfig();
            var subs = new[] { MakePlayer(pc, "mid", personality: "p3"), MakePlayer(pc, "top", personality: "p3") };
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, personality: "p3"), subs);
            t.Chemistry = 25f;
            t.SetStarter("mid", subs[0]);
            t.SetStarter("top", subs[1]); // 25 - 20 - 20 would be negative
            Assert.AreEqual(0f, t.Chemistry);
        }

        [Test]
        public void RemovePlayer_Starter_DropsChemistryAndEmptiesSlot()
        {
            var pc = MakePlayerConfig();
            var five = FullFive(pc, personality: "p3");
            var t = MakeTeam(MakeConfig(), pc, five);
            Assert.IsTrue(t.RemovePlayer(five[0]));
            Assert.IsNull(t.GetStarter("top"));
            Assert.AreEqual(30f, t.Chemistry, 1e-4f);
        }

        [Test]
        public void SwapStarters_CountsAsTwoReplacements()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc, personality: "p3"));
            t.SwapStarters("top", "mid");
            Assert.AreEqual(10f, t.Chemistry, 1e-4f);
        }

        [Test]
        public void SetStarter_PlayerAlreadyStartingElsewhere_Throws()
        {
            var pc = MakePlayerConfig();
            var five = FullFive(pc);
            var t = MakeTeam(MakeConfig(), pc, five);
            Assert.Throws<ArgumentException>(() => t.SetStarter("top", five[1]));
        }

        [Test]
        public void SetStarter_NotOnRosterOrUnknownLane_Throws()
        {
            var pc = MakePlayerConfig();
            var t = MakeTeam(MakeConfig(), pc, FullFive(pc));
            Assert.Throws<ArgumentException>(() => t.SetStarter("top", MakePlayer(pc, "top")));
            Assert.Throws<ArgumentException>(() => t.GetStarter("nope"));
        }

        // AC6: personality pairing
        [Test]
        public void Pairing_CompatibleFive_RisesFasterThanNeutral()
        {
            var pc = MakePlayerConfig();
            var five = new List<Player>
            {
                MakePlayer(pc, "top", personality: "p1"), MakePlayer(pc, "jungle", personality: "p2"),
                MakePlayer(pc, "mid", personality: "p2"), MakePlayer(pc, "adc", personality: "p2"),
                MakePlayer(pc, "support", personality: "p2"),
            };
            var t = MakeTeam(MakeConfig(), pc, five);
            // pairs: 10 total; p1-p2 compatible x4 (+0.5), p2-p2 unlisted x6 (0) -> avg 0.2
            Assert.AreEqual(0.2f, t.AveragePairModifier, 1e-5f);
            t.SettleSegment(null);
            Assert.AreEqual(50f + 10f * 1.2f, t.Chemistry, 1e-4f);
        }

        [Test]
        public void Pairing_ConflictFive_RisesSlowerAndDropsHarder()
        {
            var pc = MakePlayerConfig();
            var five = new List<Player>
            {
                MakePlayer(pc, "top", personality: "p1"), MakePlayer(pc, "jungle", personality: "p3"),
                MakePlayer(pc, "mid", personality: "p3"), MakePlayer(pc, "adc", personality: "p3"),
                MakePlayer(pc, "support", personality: "p3"),
            };
            var sub = MakePlayer(pc, "support", personality: "p3");
            var t = MakeTeam(MakeConfig(), pc, five, new[] { sub });
            Assert.AreEqual(-0.2f, t.AveragePairModifier, 1e-5f);
            t.SettleSegment(null);
            Assert.AreEqual(50f + 10f * 0.8f, t.Chemistry, 1e-4f);
            Assert.AreEqual(1.2f, t.DropMultiplier, 1e-5f);
            t.Chemistry = 50f;
            t.SetStarter("support", sub); // p3 for p3: modifier unchanged, drop 20 * 1.2
            Assert.AreEqual(50f - 24f, t.Chemistry, 1e-4f);
        }

        [Test]
        public void Pairing_IsUnordered()
        {
            var cfg = MakeConfig();
            Assert.AreEqual(cfg.GetPairModifier("p1", "p2"), cfg.GetPairModifier("p2", "p1"));
            Assert.AreEqual(0.5f, cfg.GetPairModifier("p2", "p1"));
            Assert.AreEqual(0f, cfg.GetPairModifier("p2", "p3"));
        }

        // AC7: no bankruptcy logic - covered by Settle_FundsCanGoNegative (no exception, no state flag).

        // Validation
        [Test]
        public void Validate_ValidConfig_DoesNotThrow() => Assert.DoesNotThrow(() => Validate(MakeConfig()));

        [Test]
        public void Validate_UnknownPersonalityInPairing_ThrowsNamingId()
        {
            var c = MakeConfig(); c.pairings[0].b = "ghost";
            var e = Assert.Throws<InvalidOperationException>(() => Validate(c));
            StringAssert.Contains("ghost", e.Message);
        }

        [Test]
        public void Validate_EmptyPairings_Throws()
        {
            var c = MakeConfig(); c.pairings = new PersonalityPairing[0];
            var e = Assert.Throws<InvalidOperationException>(() => Validate(c));
            StringAssert.Contains("pairings", e.Message);
        }

        [Test]
        public void Validate_DuplicateUnorderedPair_Throws()
        {
            var c = MakeConfig();
            c.pairings = c.pairings.Concat(new[] { new PersonalityPairing { a = "p2", b = "p1", relation = TeamConfig.RelationConflict } }).ToArray();
            Assert.Throws<InvalidOperationException>(() => Validate(c));
        }

        [Test]
        public void Validate_UnknownRelation_Throws()
        {
            var c = MakeConfig(); c.pairings[0].relation = "friends";
            Assert.Throws<InvalidOperationException>(() => Validate(c));
        }

        [Test]
        public void Validate_NegativeRate_ThrowsNamingField()
        {
            var c = MakeConfig(); c.sponsorshipPerPopularity = -1f;
            var e = Assert.Throws<InvalidOperationException>(() => Validate(c));
            StringAssert.Contains("sponsorship_per_popularity", e.Message);
        }

        [Test]
        public void Validate_NegativeChemistryDrop_Throws()
        {
            var c = MakeConfig(); c.chemistryDropPerReplacement = -1f;
            var e = Assert.Throws<InvalidOperationException>(() => Validate(c));
            StringAssert.Contains("drop_per_replacement", e.Message);
        }

        [Test]
        public void Validate_WrongModifierSigns_Throw()
        {
            var c = MakeConfig(); c.conflictModifier = 0.1f;
            Assert.Throws<InvalidOperationException>(() => Validate(c));
            c = MakeConfig(); c.compatibleModifier = -0.1f;
            Assert.Throws<InvalidOperationException>(() => Validate(c));
        }

        [Test]
        public void Validate_LaneWeights_EmptyUnknownMissingOrAllZero_Throw()
        {
            var c = MakeConfig(); c.laneWeights = new LaneWeight[0];
            Assert.Throws<InvalidOperationException>(() => Validate(c));
            c = MakeConfig(); c.laneWeights[0].lane = "bot";
            Assert.Throws<InvalidOperationException>(() => Validate(c));
            c = MakeConfig(); c.laneWeights = c.laneWeights.Take(4).ToArray();
            Assert.Throws<InvalidOperationException>(() => Validate(c));
            c = MakeConfig(); foreach (var w in c.laneWeights) w.weight = 0f;
            Assert.Throws<InvalidOperationException>(() => Validate(c));
        }

        [Test]
        public void Validate_BadAggregation_StarterShare_SegmentsPerYear_Throw()
        {
            var c = MakeConfig(); c.strengthAggregation = "median";
            Assert.Throws<InvalidOperationException>(() => Validate(c));
            c = MakeConfig(); c.starterPopularityShare = 1.5f;
            Assert.Throws<InvalidOperationException>(() => Validate(c));
            c = MakeConfig(); c.segmentsPerYear = 0;
            Assert.Throws<InvalidOperationException>(() => Validate(c));
        }

        [Test]
        public void Loader_EmptyOrMalformedJson_ThrowsInvalidOperation()
        {
            var pc = MakePlayerConfig();
            Assert.Throws<InvalidOperationException>(() => TeamConfigLoader.FromJson("", pc));
            Assert.Throws<InvalidOperationException>(() => TeamConfigLoader.FromJson("{ not json", pc));
            Assert.Throws<InvalidOperationException>(() => TeamConfigLoader.FromJson("{}", pc));
        }

        [Test, Category("Integration")]
        public void ShippedConfig_LoadsAndValidatesAgainstShippedPlayerConfig()
        {
            var playerAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/PlayerConfig.json");
            var teamAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/team_config.json");
            Assert.IsNotNull(playerAsset, "missing PlayerConfig.json");
            Assert.IsNotNull(teamAsset, "missing team_config.json");

            var pc = PlayerConfigLoader.FromJson(playerAsset.text);
            var cfg = TeamConfigLoader.FromJson(teamAsset.text, pc);

            Assert.AreEqual(pc.lanes.Length, cfg.laneWeights.Length);
            Assert.Greater(cfg.pairings.Length, 0);
            Assert.Greater(cfg.segmentsPerYear, 0);
        }
    }
}
