using System;
using System.Linq;
using EsportsManager.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EsportsManager.Tests.Players
{
    /// <summary>
    /// EditMode tests for reading player_config.json: snake_case keys through the wire-format DTOs,
    /// sentinel-based missing-key detection (TD-001), and JsonUtility behavior the design relies on.
    /// Unit tests parse in-memory strings; only the [Category("Integration")] test touches the asset.
    /// </summary>
    public class PlayerConfigLoadingTests
    {
        // Smallest config that satisfies every rule. ' stands for " to keep the literal readable.
        private static readonly string ValidJson = (
            "{'off_lane_discount':0.5,"
            + "'lanes':[{'id':'x','display_name':'X','dimensions':[{'id':'d','display_name':'D','weight':1}]}],"
            + "'personality_groups':[{'id':'g1','traits':[{'id':'t1','display_name':'T1'},{'id':'t2','display_name':'T2'}]},"
            + "{'id':'g2','traits':[{'id':'t3','display_name':'T3'},{'id':'t4','display_name':'T4'}]}],"
            + "'trait_effects':[{'trait':'t1','key':'training_gain','multiplier':1.2}],"
            + "'trait_pairs':[{'a':'t1','b':'t3','value':0.5}],"
            + "'training':{'base_gain':2,'age_bands':[{'min_age':0,'max_age':99,'coefficient':1}],'cap_margin':10,'span':25,'focus_bonus':0.5,"
            + "'other_share':0.1,'mood_factor_min':0.8,'mood_factor_max':1.2,'stress_threshold':50,'stress_slope':2.3,'specialization_rate':0.163,"
            + "'specialization_gain':0.6,'noise':0.15,'near_cap_margin':5,'initial_focus_high':0.4,'initial_focus_low':0.2},"
            + "'specialty':{'unlock_value':76,'lose_value':73,'unlock_focus':0.55,'lose_focus':0.3,'specialties':[{'id':'sp','display_name':'SP',"
            + "'lane':'x','phase':'early','dimension':'d','effects':[{'hook':'phase_flat','phase':'early','value':1.5}]}]},"
            + "'aging':{'decline_start_age':27,'decline_bands':[{'min_age':27,'max_age':99,'amount':1}]},"
            + "'potential':{'talent_threshold':85,'scouting_bias':10,'estimate_half_width':20,'estimate_min_half_width':3,'observation_per_segment':5,'observation_per_training':3},"
            + "'salary':{'min_salary':20000,'max_salary':200000,'weight_lane_score':1,'weight_popularity':0.8,'weight_potential':0.3,'random_range':0.1},"
            + "'generation':{'players_per_lane':1,'min_age':17,'max_age':30,"
            + "'main_lane_dim_fraction_min':0.6,'main_lane_dim_fraction_max':0.95,'off_lane_dim_fraction_min':0.35,'off_lane_dim_fraction_max':0.75,"
            + "'looks_min':30,'looks_max':90,'popularity_min':5,'popularity_max':60,'mood_min':50,'mood_max':80,'stress_min':10,'stress_max':40,"
            + "'normal_potential_min':40,'normal_potential_max':75,'talent_potential_min':85,'talent_potential_max':95,'talent_chance':0.05,"
            + "'contract_years_min':1,'contract_years_max':3,'player_ids':['s','g']}}").Replace('\'', '"');

        // Every key in ValidJson. Misspelling any one of them must fail loading.
        private static readonly string[] AllKeys =
        {
            "off_lane_discount", "lanes", "id", "display_name", "dimensions", "weight",
            "personality_groups", "traits", "trait_effects", "trait", "key", "multiplier",
            "trait_pairs", "a", "b", "value",
            "training", "base_gain", "age_bands", "min_age", "max_age", "coefficient", "cap_margin", "span", "focus_bonus", "other_share",
            "mood_factor_min", "mood_factor_max", "stress_threshold", "stress_slope", "specialization_rate", "specialization_gain", "noise",
            "near_cap_margin", "initial_focus_high", "initial_focus_low",
            "specialty", "unlock_value", "lose_value", "unlock_focus", "lose_focus", "specialties", "lane", "phase", "dimension", "effects", "hook",
            "aging", "decline_start_age", "decline_bands", "amount",
            "potential", "talent_threshold", "scouting_bias", "estimate_half_width", "estimate_min_half_width",
            "observation_per_segment", "observation_per_training",
            "salary", "min_salary", "max_salary", "weight_lane_score", "weight_popularity", "weight_potential", "random_range",
            "generation", "players_per_lane", "main_lane_dim_fraction_min", "main_lane_dim_fraction_max", "off_lane_dim_fraction_min", "off_lane_dim_fraction_max",
            "looks_min", "looks_max", "popularity_min", "popularity_max", "mood_min", "mood_max", "stress_min", "stress_max",
            "normal_potential_min", "normal_potential_max", "talent_potential_min", "talent_potential_max", "talent_chance",
            "contract_years_min", "contract_years_max", "player_ids",
        };

        // ---- happy path ----

        [Test]
        public void Loader_ValidSnakeCaseJson_MapsEveryField()
        {
            var cfg = PlayerConfigLoader.FromJson(ValidJson);
            Assert.AreEqual(0.5f, cfg.offLaneDiscount);
            Assert.AreEqual("x", cfg.lanes[0].id);
            Assert.AreEqual("D", cfg.lanes[0].dimensions[0].displayName);
            Assert.AreEqual(2, cfg.personalityGroups.Length);
            Assert.AreEqual("T2", cfg.personalityGroups[0].traits[1].displayName);
            Assert.AreEqual("t1", cfg.traitEffects[0].traitId);
            Assert.AreEqual("training_gain", cfg.traitEffects[0].effectKey);
            Assert.AreEqual(1.2f, cfg.traitEffects[0].multiplier);
            Assert.AreEqual(0.5f, cfg.GetPairCompatibility("t3", "t1"));
            Assert.AreEqual(2f, cfg.training.baseGain);
            Assert.AreEqual(25f, cfg.training.span);
            Assert.AreEqual(0.163f, cfg.training.specializationRate);
            Assert.AreEqual("sp", cfg.specialty.specialties[0].id);
            Assert.AreEqual(10f, cfg.potential.scoutingBias);
            Assert.AreEqual(20f, cfg.potential.estimateHalfWidth);
            Assert.AreEqual(3f, cfg.potential.estimateMinHalfWidth);
            Assert.AreEqual(5f, cfg.potential.observationPerSegment);
            Assert.AreEqual(3f, cfg.potential.observationPerTraining);
            Assert.AreEqual(0.6f, cfg.generation.mainLaneDimFractionMin);
            Assert.AreEqual(0.75f, cfg.generation.offLaneDimFractionMax);
            Assert.AreEqual(99, cfg.training.ageBands[0].maxAge);
            Assert.AreEqual(27, cfg.aging.declineStartAge);
            Assert.AreEqual(1f, cfg.aging.declineBands[0].amount);
            Assert.AreEqual(85f, cfg.potential.talentThreshold);
            Assert.AreEqual(20000, cfg.salary.minSalary);
            Assert.AreEqual(200000, cfg.salary.maxSalary);
            Assert.AreEqual(0.8f, cfg.salary.weightPopularity);
            Assert.AreEqual(0.1f, cfg.salary.randomRange);
            Assert.AreEqual(1, cfg.generation.playersPerLane);
            Assert.AreEqual(0.05f, cfg.generation.talentChance);
            Assert.AreEqual(3, cfg.generation.contractYearsMax);
            Assert.AreEqual("g", cfg.generation.playerIds[1]);
        }

        [Test]
        public void Loader_CamelCaseKeys_AreNotAcceptedAnymore()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => PlayerConfigLoader.FromJson(ValidJson.Replace("\"off_lane_discount\"", "\"offLaneDiscount\"")));
            StringAssert.Contains("off_lane_discount", ex.Message);
        }

        // ---- sentinels: every key missing/misspelled is an error ----

        [Test]
        public void ValidJson_Fixture_Loads()
        {
            Assert.DoesNotThrow(() => PlayerConfigLoader.FromJson(ValidJson));
        }

        [Test]
        public void Loader_EveryKeyMisspelled_Throws([ValueSource(nameof(AllKeys))] string key)
        {
            string token = "\"" + key + "\":";
            Assert.IsTrue(ValidJson.Contains(token), "fixture has no key " + key);
            string broken = ValidJson.Replace(token, "\"" + key + "_typo\":");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken), key);
            StringAssert.Contains("missing or misspelled key", ex.Message, key);
            StringAssert.Contains(key, ex.Message);
        }

        [Test]
        public void Loader_MissingFloatKey_IsErrorNotZero()
        {
            // Without the sentinel, JsonUtility would give offLaneDiscount = 0 -> fail only by luck of validation;
            // salary.random_range = 0 would be a LEGAL value, so a silent 0 would load. It must not.
            string broken = ValidJson.Replace("\"random_range\":0.1", "\"random_rang\":0.1");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("salary.random_range", ex.Message);
            Assert.DoesNotThrow(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"random_range\":0.1", "\"random_range\":0")),
                "an explicit 0 is a legal value and must still load");
        }

        [Test]
        public void Loader_MissingIntKey_IsErrorNotZero()
        {
            string broken = ValidJson.Replace("\"min_salary\":20000", "\"min_salry\":20000");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("salary.min_salary", ex.Message);
            Assert.DoesNotThrow(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"min_salary\":20000", "\"min_salary\":0")));
        }

        [Test]
        public void Loader_MissingStringKey_IsError()
        {
            string broken = ValidJson.Replace("\"display_name\":\"X\"", "\"display_nam\":\"X\"");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("lanes[0].display_name", ex.Message);
        }

        [Test]
        public void Loader_MissingArrayKey_IsError()
        {
            string broken = ValidJson.Replace("\"player_ids\":", "\"player_id\":");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("generation.player_ids", ex.Message);
        }

        [Test]
        public void Loader_MissingKeyInsideArrayElement_IsErrorNamingTheElement()
        {
            string broken = ValidJson.Replace("\"multiplier\":1.2", "\"multipler\":1.2");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("trait_effects[0].multiplier", ex.Message);
        }

        [Test]
        public void Loader_MissingWholeSection_IsError()
        {
            string broken = ValidJson.Replace("\"training\":", "\"trainin\":");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("training", ex.Message);
        }

        [Test]
        public void Loader_EmptyObject_IsError()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson("{}"));
        }

        [Test]
        public void Loader_ExplicitNullValue_IsError()
        {
            string broken = ValidJson.Replace("\"base_gain\":2", "\"base_gain\":null");
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(broken));
            StringAssert.Contains("null", ex.Message);
        }

        [Test]
        public void Loader_TheWordNullInsideAString_IsNotMistakenForNull()
        {
            string json = ValidJson.Replace("\"player_ids\":[\"s\",\"g\"]", "\"player_ids\":[\"null\",\"a\\\"null\"]");
            Assert.DoesNotThrow(() => PlayerConfigLoader.FromJson(json));
        }

        [Test]
        public void Loader_ValidationFailureAfterParsing_StillThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"off_lane_discount\":0.5", "\"off_lane_discount\":1.5")));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"value\":0.5", "\"value\":2")));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"min_salary\":20000", "\"min_salary\":300000")));
        }

        [Test]
        public void Loader_MalformedJson_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson("{ \"lanes\": [ {"));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson("not json at all"));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(""));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(null));
        }

        // ---- wrong value types / non-finite / unknown / duplicate keys: all rejected by the strict pre-check ----

        private static void AssertLoadFails(string json, string expectedFragment)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(json));
            StringAssert.Contains(expectedFragment, ex.Message);
        }

        [Test]
        public void Loader_NumberWrittenAsString_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":\"2\""), "training.base_gain' must be a number");
        }

        [Test]
        public void Loader_BooleanInNumberField_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":true"), "must be a number");
        }

        [Test]
        public void Loader_FractionInIntField_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"min_salary\":20000", "\"min_salary\":20000.5"), "salary.min_salary' must be a whole number");
        }

        [Test]
        public void Loader_IntOutOfRange_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"min_salary\":20000", "\"min_salary\":99999999999"), "inside int range");
        }

        [Test]
        public void Loader_ArrayOrObjectInScalarField_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":[2]"), "must be a number");
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":{}"), "must be a number");
        }

        [Test]
        public void Loader_NumberInStringField_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"display_name\":\"X\"", "\"display_name\":3"), "lanes[0].display_name' must be a string");
        }

        [Test]
        public void Loader_ScalarInArrayField_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"player_ids\":[\"s\",\"g\"]", "\"player_ids\":\"s\""), "generation.player_ids' must be an array");
        }

        [Test]
        public void Loader_WrongElementTypeInArray_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"player_ids\":[\"s\",\"g\"]", "\"player_ids\":[\"s\",3]"), "generation.player_ids[1]' must be a string");
        }

        [Test]
        public void Loader_NumberTooBigForFloat_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":1e39"), "finite");
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":1e999"), "finite");
        }

        [Test]
        public void Loader_NaNOrInfinityLiteral_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":NaN")));
            Assert.Throws<InvalidOperationException>(() => PlayerConfigLoader.FromJson(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":Infinity")));
        }

        [Test]
        public void Loader_UnknownExtraKey_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":2,\"extra\":1"), "unknown key 'training.extra'");
        }

        [Test]
        public void Loader_DuplicateKey_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"base_gain\":2", "\"base_gain\":2,\"base_gain\":3"), "duplicate key 'base_gain'");
        }

        [Test]
        public void Loader_UnknownEffectKey_Throws()
        {
            AssertLoadFails(ValidJson.Replace("\"key\":\"training_gain\"", "\"key\":\"trainng_gain\""), "not a known effect key");
        }

        [Test]
        public void Loader_IntegerWrittenInFloatField_IsAccepted()
        {
            Assert.AreEqual(2f, PlayerConfigLoader.FromJson(ValidJson).training.baseGain); // 'base_gain':2
            var cfg = PlayerConfigLoader.FromJson(ValidJson.Replace("\"min_salary\":20000", "\"min_salary\":2e4"));
            Assert.AreEqual(20000, cfg.salary.minSalary, "an integral value in exponent form is a whole number");
        }

        // ---- JsonUtility behavior the loader depends on (pinned so an engine upgrade that changes it fails loudly) ----

        [Serializable] private class ProbeInner { public float f = float.NaN; }
        [Serializable] private class Probe
        {
            public float f = float.NaN;
            public int i = int.MinValue;
            public string s = null;
            public float[] arr = null;
            public ProbeInner[] list = null;
            public ProbeInner inner = null;
        }

        [Test]
        public void JsonUtility_MissingScalarsStringsArrays_KeepTheirInitializer()
        {
            var p = JsonUtility.FromJson<Probe>("{}");
            Assert.IsTrue(float.IsNaN(p.f));
            Assert.AreEqual(int.MinValue, p.i);
            Assert.IsNull(p.s);
            Assert.IsNull(p.arr);
            Assert.IsNull(p.list);
        }

        [Test]
        public void JsonUtility_MissingNestedObject_IsAutoCreatedWithItsOwnInitializers()
        {
            var p = JsonUtility.FromJson<Probe>("{}");
            Assert.IsNotNull(p.inner, "nested [Serializable] fields are never null after FromJson");
            Assert.IsTrue(float.IsNaN(p.inner.f), "so the sentinel inside is what detects a missing section");
        }

        [Test]
        public void JsonUtility_MissingKeyInsideArrayElement_KeepsTheInitializer()
        {
            var p = JsonUtility.FromJson<Probe>("{\"list\":[{\"f\":2},{}]}");
            Assert.AreEqual(2f, p.list[0].f);
            Assert.IsTrue(float.IsNaN(p.list[1].f));
        }

        [Test]
        public void JsonUtility_ExplicitNullOnNumber_BecomesZero_SoLoaderRejectsNullLiterals()
        {
            var p = JsonUtility.FromJson<Probe>("{\"f\":null}");
            Assert.AreEqual(0f, p.f);
        }

        [Test]
        public void JsonUtility_WrongTypes_AreCoercedSilently_WhichIsWhyTheLoaderPreChecks()
        {
            Assert.AreEqual(3f, JsonUtility.FromJson<Probe>("{\"f\":\"3\"}").f, "numeric string into float");
            Assert.AreEqual(0f, JsonUtility.FromJson<Probe>("{\"f\":\"abc\"}").f, "non-numeric string into float");
            Assert.AreEqual(0f, JsonUtility.FromJson<Probe>("{\"f\":true}").f, "bool into float");
            Assert.AreEqual(2, JsonUtility.FromJson<Probe>("{\"i\":2.7}").i, "fraction into int truncates");
            Assert.AreEqual(7, JsonUtility.FromJson<Probe>("{\"i\":\"7\"}").i, "numeric string into int");
            Assert.AreEqual(0f, JsonUtility.FromJson<Probe>("{\"f\":[1]}").f, "array into float");
            Assert.AreEqual(0f, JsonUtility.FromJson<Probe>("{\"f\":{}}").f, "object into float");
            Assert.AreEqual("3", JsonUtility.FromJson<Probe>("{\"s\":3}").s, "number into string");
            Assert.AreEqual(0, JsonUtility.FromJson<Probe>("{\"arr\":3}").arr.Length, "scalar into array gives empty array");
            Assert.AreEqual(-1, JsonUtility.FromJson<Probe>("{\"i\":99999999999}").i, "int overflow wraps");
            Assert.IsTrue(float.IsPositiveInfinity(JsonUtility.FromJson<Probe>("{\"f\":Infinity}").f), "Infinity literal accepted");
            Assert.AreEqual(1f, JsonUtility.FromJson<Probe>("{\"f\":1,\"f\":2}").f, "duplicate key keeps the first");
            Assert.Throws<ArgumentException>(() => JsonUtility.FromJson<Probe>("{\"f\":1e999}"), "only an out-of-double-range number throws");
        }

        [Test]
        public void JsonUtility_UnknownKeys_AreSilentlyIgnored()
        {
            var p = JsonUtility.FromJson<Probe>("{\"f\":3,\"not_a_field\":1}");
            Assert.AreEqual(3f, p.f);
        }

        // ---- shipped file (integration) ----

        [Test]
        [Category("Integration")]
        public void ShippedConfig_LoadsAndValidatesAndMatchesDesign()
        {
            const string path = "Assets/Data/player_config.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.IsNotNull(asset, "missing " + path);

            var cfg = PlayerConfigLoader.FromJson(asset.text); // also runs Validate()

            // brief lane table
            Assert.AreEqual(TestConfigFactory.LaneTable.Length, cfg.lanes.Length);
            for (int i = 0; i < TestConfigFactory.LaneTable.Length; i++)
                CollectionAssert.AreEqual(TestConfigFactory.LaneTable[i].Skip(1).ToArray(),
                    cfg.lanes[i].dimensions.Select(d => d.displayName).ToArray(), cfg.lanes[i].id);

            // numbers.md 3.1: ten groups of two, twenty distinct words
            Assert.AreEqual(10, cfg.personalityGroups.Length);
            Assert.AreEqual(20, cfg.personalityGroups.SelectMany(g => g.traits).Select(t => t.id).Distinct().Count());
            // numbers.md 3.2 / 3.3 sizes: 20 words each have an effect; 16 listed pairs
            Assert.AreEqual(20, cfg.traitEffects.Select(e => e.traitId).Distinct().Count());
            Assert.AreEqual(16, cfg.traitPairs.Length);
            Assert.AreEqual(29, cfg.traitEffects.Length);
            // numbers.md 3.2 v2: every multiplier within +-5%
            foreach (var e in cfg.traitEffects)
                Assert.That(e.multiplier, Is.InRange(0.95f - 1e-6f, 1.05f + 1e-6f), e.traitId + " " + e.effectKey);
            Assert.AreEqual(1.03f, cfg.GetEffectMultiplier("clever", PlayerEffectKeys.OffLaneDiscount), 1e-6f);
            Assert.AreEqual(0.95f, cfg.GetEffectMultiplier("slick", PlayerEffectKeys.NegativeCompatImpact), 1e-6f);
            Assert.AreEqual(1.05f, cfg.GetEffectMultiplier("gentle", PlayerEffectKeys.FamiliarityGain), 1e-6f);

            // initial values named by the story
            Assert.AreEqual(85f, cfg.potential.talentThreshold);
            Assert.AreEqual(27, cfg.aging.declineStartAge);
            Assert.AreEqual(2f, cfg.training.baseGain); // growth.md rev 3 T_base
            Assert.AreEqual(10f, cfg.training.capMargin);
            Assert.AreEqual(25f, cfg.training.span);
            Assert.AreEqual(0.5f, cfg.training.focusBonus);
            Assert.AreEqual(0.1f, cfg.training.otherShare);
            Assert.AreEqual(2.3f, cfg.training.stressSlope);
            Assert.AreEqual(0.163f, cfg.training.specializationRate);
            Assert.AreEqual(0.6f, cfg.training.specializationGain);
            Assert.AreEqual(0.15f, cfg.training.noise);
            Assert.AreEqual(76f, cfg.specialty.unlockValue);
            Assert.AreEqual(73f, cfg.specialty.loseValue);
            Assert.AreEqual(16, cfg.specialty.specialties.Length);
            Assert.AreEqual(10f, cfg.potential.scoutingBias);
            Assert.AreEqual(20f, cfg.potential.estimateHalfWidth);
            Assert.AreEqual(3f, cfg.potential.estimateMinHalfWidth);
            Assert.AreEqual(5f, cfg.potential.observationPerSegment);
            Assert.AreEqual(3f, cfg.potential.observationPerTraining);
            Assert.AreEqual(0.6f, cfg.generation.mainLaneDimFractionMin);
            Assert.AreEqual(0.95f, cfg.generation.mainLaneDimFractionMax);
            Assert.AreEqual(0.35f, cfg.generation.offLaneDimFractionMin);
            Assert.AreEqual(0.75f, cfg.generation.offLaneDimFractionMax);
            Assert.AreEqual(20000, cfg.salary.minSalary);
            Assert.AreEqual(200000, cfg.salary.maxSalary);
            Assert.GreaterOrEqual(cfg.generation.playersPerLane, 5);

            // the shipped pool generates and obeys the rules end to end
            var pool = new PlayerGenerator(cfg, new SeededRandomSource(TestConfigFactory.FixedSeed)).GeneratePool();
            Assert.GreaterOrEqual(pool.Count, 25);
            Assert.IsTrue(pool.All(p => cfg.FindTraitGroup(p.Traits[0]) != cfg.FindTraitGroup(p.Traits[1])));
            Assert.IsTrue(pool.All(p => p.Salary >= cfg.salary.minSalary));
            Assert.AreEqual(pool.Count, pool.Select(p => p.Id).Distinct().Count());
        }

        [Test]
        [Category("Integration")]
        public void ShippedConfig_OldCamelCaseFileIsGone()
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/PlayerConfig.json"));
        }
    }
}
