using System;
using System.Linq;
using EsportsManager.Players;
using NUnit.Framework;

namespace EsportsManager.Tests.Players
{
    /// <summary>EditMode tests for the two-trait personality system (Story 008, numbers.md section 3).</summary>
    public class PersonalityTests
    {
        private static PlayerConfig Cfg(float discount = 0.8f) => TestConfigFactory.Make(discount);

        private static Player P(PlayerConfig cfg, string a, string b) =>
            TestConfigFactory.MakePlayer(cfg, traitA: a, traitB: b);

        private static void AssertInvalid(PlayerConfig cfg, string expectedFragment)
        {
            var ex = Assert.Throws<InvalidOperationException>(cfg.Validate);
            StringAssert.Contains(expectedFragment, ex.Message);
        }

        // ---- two traits from different groups ----

        [Test]
        public void NewPlayer_HasExactlyTwoTraits()
        {
            var p = P(Cfg(), "diligent", "gentle");
            Assert.AreEqual(2, p.Traits.Count);
        }

        [Test]
        public void Constructor_TwoTraitsFromSameGroup_Throws()
        {
            var cfg = Cfg();
            var ex = Assert.Throws<ArgumentException>(() => P(cfg, "diligent", "lazy"));
            StringAssert.Contains("same group", ex.Message);
            Assert.Throws<ArgumentException>(() => P(cfg, "diligent", "diligent"));
        }

        [Test]
        public void Constructor_UnknownTrait_Throws()
        {
            var cfg = Cfg();
            Assert.Throws<ArgumentException>(() => P(cfg, "diligent", "nonexistent"));
            Assert.Throws<ArgumentException>(() => P(cfg, "nonexistent", "gentle"));
        }

        [Test]
        public void Pool_NeverHasTwoTraitsFromTheSameGroup_AcrossSeeds()
        {
            var cfg = Cfg();
            cfg.generation.playersPerLane = 40; // 200 players per seed
            for (int seed = 1; seed <= 5; seed++)
            {
                var pool = new PlayerGenerator(cfg, new SeededRandomSource(seed)).GeneratePool();
                foreach (var p in pool)
                {
                    Assert.AreEqual(2, p.Traits.Count);
                    Assert.AreNotEqual(cfg.FindTraitGroup(p.Traits[0]), cfg.FindTraitGroup(p.Traits[1]),
                        p.Name + " has " + p.Traits[0] + " + " + p.Traits[1]);
                    Assert.GreaterOrEqual(cfg.FindTraitGroup(p.Traits[0]), 0);
                }
            }
        }

        [Test]
        public void Pool_UsesManyDifferentTraits()
        {
            var cfg = Cfg();
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            int distinct = pool.SelectMany(p => p.Traits).Distinct().Count();
            Assert.Greater(distinct, 10, "traits should be spread across the ten groups");
        }

        // ---- effect multipliers ----

        [Test]
        public void TraitMultiplier_IsProductOfBothTraits()
        {
            var cfg = Cfg();
            var p = P(cfg, "diligent", "clever"); // 1.2 x 1.1
            Assert.AreEqual(1.2f * 1.1f, p.GetTraitMultiplier("training_gain"), 1e-5f);
        }

        [Test]
        public void TraitMultiplier_OnlyOneTraitListed_UsesThatTrait()
        {
            var p = P(Cfg(), "lazy", "gentle");
            Assert.AreEqual(0.8f, p.GetTraitMultiplier("training_gain"), 1e-5f);
        }

        [Test]
        public void TraitMultiplier_KeyNotListedForEitherTrait_IsOne()
        {
            var p = P(Cfg(), "gentle", "optimistic");
            Assert.AreEqual(1f, p.GetTraitMultiplier("training_gain"));
        }

        [Test]
        public void TraitMultiplier_UnknownKey_IsOne()
        {
            var p = P(Cfg(), "diligent", "clever");
            Assert.AreEqual(1f, p.GetTraitMultiplier("no_such_effect"));
        }

        [Test]
        public void TraitMultiplier_KeyOnlyStoredForLaterStories_IsReadable()
        {
            var p = P(Cfg(), "lively", "gentle");
            Assert.AreEqual(1.3f, p.GetTraitMultiplier("stream_fans"), 1e-5f);
        }

        [Test]
        public void TraitMultiplier_ConfigEdit_ChangesResult()
        {
            var cfg = Cfg();
            var p = P(cfg, "diligent", "gentle");
            cfg.traitEffects.First(e => e.traitId == "diligent" && e.effectKey == "training_gain").multiplier = 2f;
            Assert.AreEqual(2f, p.GetTraitMultiplier("training_gain"), 1e-5f);
        }

        [Test]
        public void OffLaneScore_CleverRaisesDiscountFromPointEightToPointNine()
        {
            var cfg = Cfg(0.8f);
            var clever = TestConfigFactory.MakePlayer(cfg, mainLane: "mid", fill: 60f, traitA: "clever", traitB: "gentle");
            var plain = TestConfigFactory.MakePlayer(cfg, mainLane: "mid", fill: 60f, traitA: "diligent", traitB: "gentle");
            Assert.AreEqual(60f * 0.9f, clever.GetLaneScore("top"), 1e-3f);
            Assert.AreEqual(60f * 0.8f, plain.GetLaneScore("top"), 1e-3f);
            Assert.AreEqual(60f, clever.GetLaneScore("mid"), 1e-4f, "main lane is never discounted");
        }

        [Test]
        public void OffLaneScore_CleverDiscountIsCappedAtOne()
        {
            var cfg = Cfg(0.95f); // 0.95 x 1.125 > 1
            var clever = TestConfigFactory.MakePlayer(cfg, mainLane: "mid", fill: 60f, traitA: "clever", traitB: "gentle");
            Assert.AreEqual(60f, clever.GetLaneScore("top"), 1e-4f);
        }

        // ---- compatibility H(i,j) ----

        [Test]
        public void Compatibility_SumsAllFourCombinations()
        {
            var cfg = Cfg();
            // assertive-gentle 0.5, assertive-optimistic 0, diligent-gentle 0, diligent-optimistic 0
            var a = P(cfg, "assertive", "diligent");
            var b = P(cfg, "gentle", "optimistic");
            Assert.AreEqual(0.5f, a.CompatibilityWith(b), 1e-5f);

            // loyal-loyal 0.5, loyal-fragile 0, assertive-loyal 0, assertive-fragile 0.5
            var c = P(cfg, "assertive", "loyal");
            var d = P(cfg, "loyal", "fragile");
            Assert.AreEqual(1f, c.CompatibilityWith(d), 1e-5f);

            // two negative and one positive combination: -0.5 (diligent-lazy) + 0.3 (optimistic-pessimistic)
            var e = P(cfg, "diligent", "optimistic");
            var f = P(cfg, "lazy", "pessimistic");
            Assert.AreEqual(-0.2f, e.CompatibilityWith(f), 1e-5f);
        }

        [Test]
        public void Compatibility_IsSymmetricAndPairTableIsUnordered()
        {
            var cfg = Cfg();
            var a = P(cfg, "assertive", "diligent");
            var b = P(cfg, "gentle", "optimistic");
            Assert.AreEqual(a.CompatibilityWith(b), b.CompatibilityWith(a));
            // the table lists (assertive, gentle); the reverse lookup must find it too
            Assert.AreEqual(0.5f, cfg.GetPairCompatibility("gentle", "assertive"));
            Assert.AreEqual(0.5f, cfg.GetPairCompatibility("assertive", "gentle"));
        }

        [Test]
        public void Compatibility_UnlistedCombinationsAreZero()
        {
            var cfg = Cfg();
            var a = P(cfg, "earnest", "hesitant");
            var b = P(cfg, "honest", "decisive");
            Assert.AreEqual(0f, a.CompatibilityWith(b));
        }

        [Test]
        public void Compatibility_IsClampedToPlusMinusOne()
        {
            var cfg = Cfg();
            cfg.traitPairs.First(p => p.traitA == "assertive" && p.traitB == "gentle").value = 1f;
            cfg.traitPairs.First(p => p.traitA == "assertive" && p.traitB == "fragile").value = 1f;
            var up = P(cfg, "assertive", "optimistic");
            var upOther = P(cfg, "gentle", "fragile"); // 1 + 1 = 2 -> 1
            Assert.AreEqual(1f, up.CompatibilityWith(upOther));

            cfg.traitPairs.First(p => p.traitA == "assertive" && p.traitB == "assertive").value = -1f;
            cfg.traitPairs.First(p => p.traitA == "selfish" && p.traitB == "selfish").value = -1f;
            var down = P(cfg, "assertive", "selfish");
            var downOther = P(cfg, "assertive", "selfish"); // -1 + -1 = -2 -> -1
            Assert.AreEqual(-1f, down.CompatibilityWith(downOther));
        }

        [Test]
        public void Compatibility_SameWordPair_CountsOncePerCombination()
        {
            var cfg = Cfg();
            // loyal+loyal listed 0.5; both players have loyal once -> exactly one combination matches
            var a = P(cfg, "loyal", "gentle");
            var b = P(cfg, "loyal", "optimistic");
            Assert.AreEqual(0.5f, a.CompatibilityWith(b), 1e-5f);
        }

        [Test]
        public void Compatibility_NullOther_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => P(Cfg(), "loyal", "gentle").CompatibilityWith(null));
        }

        [Test]
        public void Compatibility_SlickOnEitherSide_ScalesOnlyNegativeTerms()
        {
            var cfg = Cfg(); // slick negative_compat_impact 0.5
            // diligent-lazy -0.5 and optimistic-pessimistic +0.3; slick sits in the other half of each player
            var a = P(cfg, "diligent", "slick");
            var b = P(cfg, "lazy", "pessimistic");
            Assert.AreEqual(-0.25f, a.CompatibilityWith(b), 1e-5f);
            Assert.AreEqual(-0.25f, b.CompatibilityWith(a), 1e-5f, "symmetric: slick on the other side too");

            var c = P(cfg, "optimistic", "slick");
            var d = P(cfg, "pessimistic", "gentle");
            Assert.AreEqual(0.3f, c.CompatibilityWith(d), 1e-5f, "positive terms are untouched");
        }

        [Test]
        public void Compatibility_BothSlick_AppliesFactorOnce()
        {
            var cfg = Cfg();
            // selfish-selfish -0.5; slick-slick unlisted
            var a = P(cfg, "selfish", "slick");
            var b = P(cfg, "selfish", "slick");
            Assert.AreEqual(-0.25f, a.CompatibilityWith(b), 1e-5f);
        }

        [Test]
        public void Compatibility_NoSlick_NegativeTermsUnchanged()
        {
            var cfg = Cfg();
            var a = P(cfg, "diligent", "gentle");
            var b = P(cfg, "lazy", "optimistic");
            Assert.AreEqual(-0.5f, a.CompatibilityWith(b), 1e-5f);
        }

        [Test]
        public void PairFamiliarityMultiplier_GentleOnEitherSide_UsesGentleMultiplier_ElseOne()
        {
            var cfg = Cfg(); // gentle familiarity_gain 1.1
            var gentle = P(cfg, "gentle", "diligent");
            var plain = P(cfg, "lazy", "optimistic");
            var otherPlain = P(cfg, "selfish", "clever");
            Assert.AreEqual(1.1f, gentle.PairFamiliarityMultiplier(plain), 1e-5f);
            Assert.AreEqual(1.1f, plain.PairFamiliarityMultiplier(gentle), 1e-5f);
            Assert.AreEqual(1f, plain.PairFamiliarityMultiplier(otherPlain));
            Assert.AreEqual(1.1f, gentle.PairFamiliarityMultiplier(P(cfg, "gentle", "lazy")), 1e-5f, "both gentle: applied once");
            Assert.Throws<ArgumentNullException>(() => plain.PairFamiliarityMultiplier(null));
        }

        // ---- validation ----

        [Test]
        public void Validate_Fixture_DoesNotThrow()
        {
            Assert.DoesNotThrow(Cfg().Validate);
        }

        [Test]
        public void Validate_EffectKeyNotInWhitelist_Throws()
        {
            var cfg = Cfg();
            cfg.traitEffects[0].effectKey = "trainng_gain"; // typo
            AssertInvalid(cfg, "not a known effect key");
        }

        [Test]
        public void Validate_EveryWhitelistedKey_IsAccepted()
        {
            foreach (var key in PlayerEffectKeys.All)
            {
                var cfg = Cfg();
                cfg.traitEffects = new[] { TestConfigFactory.Effect("resolute", key, 1.01f) };
                Assert.DoesNotThrow(cfg.Validate, key);
            }
            Assert.AreEqual(PlayerEffectKeys.All.Count, PlayerEffectKeys.All.Distinct().Count());
        }

        [Test]
        public void Validate_DuplicateGroupIds_Throws()
        {
            var cfg = Cfg();
            cfg.personalityGroups[1].id = cfg.personalityGroups[0].id;
            AssertInvalid(cfg, "duplicate personality group id");
        }

        [Test]
        public void Validate_TraitInTwoGroups_Throws()
        {
            var cfg = Cfg();
            cfg.personalityGroups[1].traits[0].id = cfg.personalityGroups[0].traits[0].id;
            AssertInvalid(cfg, "more than one group");
        }

        [Test]
        public void Validate_GroupWithoutExactlyTwoTraits_Throws()
        {
            var cfg = Cfg();
            cfg.personalityGroups[0].traits = new[] { cfg.personalityGroups[0].traits[0] };
            AssertInvalid(cfg, "exactly 2 traits");
        }

        [Test]
        public void Validate_FewerThanTwoGroups_Throws()
        {
            var cfg = Cfg();
            cfg.personalityGroups = cfg.personalityGroups.Take(1).ToArray();
            AssertInvalid(cfg, "personalityGroups");
        }

        [Test]
        public void Validate_EffectReferencesUnknownTrait_Throws()
        {
            var cfg = Cfg();
            cfg.traitEffects[0].traitId = "ghost";
            AssertInvalid(cfg, "unknown trait 'ghost'");
        }

        [Test]
        public void Validate_EffectMultiplierZeroOrNegative_Throws()
        {
            var cfg = Cfg();
            cfg.traitEffects[0].multiplier = 0f;
            AssertInvalid(cfg, "multiplier must be > 0");
            cfg.traitEffects[0].multiplier = -1f;
            AssertInvalid(cfg, "multiplier must be > 0");
        }

        [Test]
        public void Validate_DuplicateEffectEntry_Throws()
        {
            var cfg = Cfg();
            cfg.traitEffects = cfg.traitEffects.Concat(new[] { TestConfigFactory.Effect("diligent", "training_gain", 1.5f) }).ToArray();
            AssertInvalid(cfg, "duplicates trait");
        }

        [Test]
        public void Validate_PairReferencesUnknownTrait_Throws()
        {
            var cfg = Cfg();
            cfg.traitPairs[0].traitA = "ghost";
            AssertInvalid(cfg, "unknown trait 'ghost'");
            cfg = Cfg();
            cfg.traitPairs[0].traitB = "ghost";
            AssertInvalid(cfg, "unknown trait 'ghost'");
        }

        [Test]
        public void Validate_DuplicatePair_IncludingReversedOrder_Throws()
        {
            var cfg = Cfg();
            cfg.traitPairs = cfg.traitPairs.Concat(new[] { TestConfigFactory.Pair("gentle", "assertive", 0.1f) }).ToArray();
            AssertInvalid(cfg, "duplicates pair");
        }

        [Test]
        public void Validate_PairValueOutsideMinusOneToOne_Throws()
        {
            var cfg = Cfg();
            cfg.traitPairs[0].value = 1.01f;
            AssertInvalid(cfg, "value must be in [-1,1]");
            cfg.traitPairs[0].value = -1.01f;
            AssertInvalid(cfg, "value must be in [-1,1]");
            cfg.traitPairs[0].value = 1f;
            Assert.DoesNotThrow(cfg.Validate);
            cfg.traitPairs[0].value = -1f;
            Assert.DoesNotThrow(cfg.Validate);
        }
    }
}
