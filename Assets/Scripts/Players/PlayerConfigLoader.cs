using System;
using UnityEngine;

namespace EsportsManager.Players
{
    /// <summary>
    /// Parses and validates <see cref="PlayerConfig"/> JSON (Assets/Data/player_config.json).
    /// The file uses snake_case keys (.claude/rules/data-files.md) and is read through private
    /// wire-format DTOs that map once into the real config types. Every key is required: a missing
    /// or misspelled key is an error, never a silent 0.
    /// </summary>
    /// <remarks>
    /// JsonUtility cannot report missing keys, it leaves a field at its initializer value. Each DTO
    /// field therefore starts at a sentinel (float.NaN, int.MinValue, null) and the mapping step
    /// rejects any field still holding it. Unknown extra keys cannot be detected (JsonUtility ignores
    /// them), but because every real key is required, a misspelled key always shows up as a missing one.
    /// Verified JsonUtility behavior (Unity 6000.6): missing scalar/string/array keys keep their initializer
    /// (so sentinels work); a missing nested object is NOT null, it is auto-created with its own initializers
    /// (so the sentinels inside it still trigger); an explicit JSON null on a number becomes 0 (so null
    /// literals are rejected separately). It also coerces wrong value types silently ("3" into a float
    /// gives 3, true/"abc"/[1]/{} give 0, 2.7 into an int gives 2, a scalar into an array gives []) and
    /// accepts Infinity. So <see cref="JsonShapeChecker"/> first checks the raw text strictly against the DTO
    /// types (types, missing/unknown/duplicate keys, null, non-finite numbers); the sentinels remain as a
    /// second line of defence.
    /// Numbers are documented where the DTO fields are declared; their meaning is in design/numbers.md.
    /// </remarks>
    public static class PlayerConfigLoader
    {
        /// <summary>
        /// Parses JSON text into a validated config. Every "bad config" case (null/empty text,
        /// malformed JSON, missing key, failed validation) surfaces as <see cref="InvalidOperationException"/>;
        /// a parser exception is kept as InnerException. A missing key message names the key path.
        /// </summary>
        /// <example><code>var cfg = PlayerConfigLoader.FromJson(textAsset.text);</code></example>
        public static PlayerConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("PlayerConfig: JSON text is empty.");

            // Strict pass first: JsonUtility silently coerces wrong types and ignores unknown keys.
            JsonShapeChecker.Check(json, typeof(ConfigDto), "PlayerConfig");

            ConfigDto dto;
            try
            {
                dto = JsonUtility.FromJson<ConfigDto>(json);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("PlayerConfig: JSON could not be parsed: " + e.Message, e);
            }
            if (dto == null) throw new InvalidOperationException("PlayerConfig: JSON could not be parsed.");

            var config = Map(dto);
            config.Validate();
            return config;
        }

        // ---------------------------------------------------------------------------------
        // Wire format. Field names ARE the JSON keys (snake_case). Sentinel initializers mark
        // "key absent". Do not give any field a real default.
        // ---------------------------------------------------------------------------------

        private const float NoFloat = float.NaN;
        private const int NoInt = int.MinValue;

        [Serializable] private class DimensionDto
        {
            public string id = null;
            public string display_name = null;
            /// <summary>Weight in the lane score (relative, >= 0).</summary>
            public float weight = NoFloat;
        }

        [Serializable] private class LaneDto
        {
            public string id = null;
            public string display_name = null;
            public DimensionDto[] dimensions = null;
        }

        [Serializable] private class TraitDto
        {
            public string id = null;
            public string display_name = null;
        }

        [Serializable] private class GroupDto
        {
            public string id = null;
            public TraitDto[] traits = null;
        }

        [Serializable] private class EffectDto
        {
            public string trait = null;
            public string key = null;
            /// <summary>Multiplier applied to the effect key (> 0, 1 = no change).</summary>
            public float multiplier = NoFloat;
        }

        [Serializable] private class PairDto
        {
            public string a = null;
            public string b = null;
            /// <summary>Compatibility h in [-1, +1].</summary>
            public float value = NoFloat;
        }

        [Serializable] private class AgeCoefficientDto
        {
            public int min_age = NoInt;
            public int max_age = NoInt;
            /// <summary>Training gain multiplier for the age range.</summary>
            public float coefficient = NoFloat;
        }

        [Serializable] private class DeclineDto
        {
            public int min_age = NoInt;
            public int max_age = NoInt;
            /// <summary>Dimension points lost per year.</summary>
            public float amount = NoFloat;
        }

        [Serializable] private class TrainingDto
        {
            /// <summary>T_base, dimension points per training at intensity 1.</summary>
            public float base_gain = NoFloat;
            /// <summary>k_s, stress penalty factor in [0, 1].</summary>
            public float stress_factor = NoFloat;
            /// <summary>rho, the soft-cap floor of the headroom factor max(1 - dim/potential, rho), in [0, 1].</summary>
            public float soft_cap_floor = NoFloat;
            public AgeCoefficientDto[] age_bands = null;
        }

        [Serializable] private class AgingDto
        {
            /// <summary>age_decline_start, first age that declines each year.</summary>
            public int decline_start_age = NoInt;
            public DeclineDto[] decline_bands = null;
        }

        [Serializable] private class PotentialDto
        {
            /// <summary>Potential at or above this is a talent player (0-100).</summary>
            public float talent_threshold = NoFloat;
            /// <summary>b: scouting bias rolled per player in U(-b, +b) at generation (numbers.md 4.15).</summary>
            public float scouting_bias = NoFloat;
            /// <summary>u0: estimate half-width at observation 0.</summary>
            public float estimate_half_width = NoFloat;
            /// <summary>u_min: the estimate half-width never drops below this.</summary>
            public float estimate_min_half_width = NoFloat;
            /// <summary>o_seg: observation gained per segment on the team (read by Story 003).</summary>
            public float observation_per_segment = NoFloat;
            /// <summary>o_train: observation gained per training (read by Story 003).</summary>
            public float observation_per_training = NoFloat;
        }

        [Serializable] private class SalaryDto
        {
            /// <summary>S_min, yuan per year.</summary>
            public int min_salary = NoInt;
            /// <summary>S_max, yuan per year.</summary>
            public int max_salary = NoInt;
            /// <summary>WS weights of main-lane score, popularity, potential.</summary>
            public float weight_lane_score = NoFloat;
            public float weight_popularity = NoFloat;
            public float weight_potential = NoFloat;
            /// <summary>r of R(r), random factor in [1-r, 1+r].</summary>
            public float random_range = NoFloat;
        }

        [Serializable] private class GenerationDto
        {
            public int players_per_lane = NoInt;
            public int min_age = NoInt;
            public int max_age = NoInt;
            /// <summary>Main-lane dimension = potential x U(min, max) (numbers.md 4.13), fractions in [0, 1].</summary>
            public float main_lane_dim_fraction_min = NoFloat;
            public float main_lane_dim_fraction_max = NoFloat;
            /// <summary>Off-lane dimension = potential x U(min, max), fractions in [0, 1].</summary>
            public float off_lane_dim_fraction_min = NoFloat;
            public float off_lane_dim_fraction_max = NoFloat;
            public float looks_min = NoFloat;
            public float looks_max = NoFloat;
            public float popularity_min = NoFloat;
            public float popularity_max = NoFloat;
            public float mood_min = NoFloat;
            public float mood_max = NoFloat;
            public float stress_min = NoFloat;
            public float stress_max = NoFloat;
            public float normal_potential_min = NoFloat;
            public float normal_potential_max = NoFloat;
            public float talent_potential_min = NoFloat;
            public float talent_potential_max = NoFloat;
            public float talent_chance = NoFloat;
            public int contract_years_min = NoInt;
            public int contract_years_max = NoInt;
            public string[] surnames = null;
            public string[] given_names = null;
        }

        [Serializable] private class ConfigDto
        {
            /// <summary>Lane score multiplier off the main lane, in (0, 1).</summary>
            public float off_lane_discount = NoFloat;
            public LaneDto[] lanes = null;
            public GroupDto[] personality_groups = null;
            public EffectDto[] trait_effects = null;
            public PairDto[] trait_pairs = null;
            public TrainingDto training = null;
            public AgingDto aging = null;
            public PotentialDto potential = null;
            public SalaryDto salary = null;
            public GenerationDto generation = null;
        }

        // ---------------------------------------------------------------------------------
        // Mapping. Each Req* call rejects a sentinel and names the offending key path.
        // ---------------------------------------------------------------------------------

        private static InvalidOperationException Missing(string path)
        {
            return new InvalidOperationException("PlayerConfig: missing or misspelled key '" + path + "'.");
        }

        private static float ReqF(float v, string path)
        {
            if (float.IsNaN(v)) throw Missing(path);
            if (float.IsInfinity(v)) throw new InvalidOperationException("PlayerConfig: key '" + path + "' is not a finite number.");
            return v;
        }
        private static int ReqI(int v, string path) { if (v == NoInt) throw Missing(path); return v; }
        private static string ReqS(string v, string path) { if (v == null) throw Missing(path); return v; }
        private static T ReqObj<T>(T v, string path) where T : class { if (v == null) throw Missing(path); return v; }

        private static TOut[] MapArray<TIn, TOut>(TIn[] src, string path, Func<TIn, string, TOut> map) where TIn : class
        {
            ReqObj(src, path);
            var result = new TOut[src.Length];
            for (int i = 0; i < src.Length; i++)
                result[i] = map(ReqObj(src[i], path + "[" + i + "]"), path + "[" + i + "]");
            return result;
        }

        private static string[] ReqStrings(string[] v, string path)
        {
            ReqObj(v, path);
            for (int i = 0; i < v.Length; i++) ReqS(v[i], path + "[" + i + "]");
            return v;
        }

        private static PlayerConfig Map(ConfigDto d)
        {
            return new PlayerConfig
            {
                offLaneDiscount = ReqF(d.off_lane_discount, "off_lane_discount"),
                lanes = MapArray(d.lanes, "lanes", MapLane),
                personalityGroups = MapArray(d.personality_groups, "personality_groups", MapGroup),
                traitEffects = MapArray(d.trait_effects, "trait_effects", (e, p) => new TraitEffectDef
                {
                    traitId = ReqS(e.trait, p + ".trait"),
                    effectKey = ReqS(e.key, p + ".key"),
                    multiplier = ReqF(e.multiplier, p + ".multiplier"),
                }),
                traitPairs = MapArray(d.trait_pairs, "trait_pairs", (e, p) => new TraitPairDef
                {
                    traitA = ReqS(e.a, p + ".a"),
                    traitB = ReqS(e.b, p + ".b"),
                    value = ReqF(e.value, p + ".value"),
                }),
                training = MapTraining(ReqObj(d.training, "training")),
                aging = MapAging(ReqObj(d.aging, "aging")),
                potential = MapPotential(ReqObj(d.potential, "potential")),
                salary = MapSalary(ReqObj(d.salary, "salary")),
                generation = MapGeneration(ReqObj(d.generation, "generation")),
            };
        }

        private static LaneDef MapLane(LaneDto l, string p)
        {
            return new LaneDef
            {
                id = ReqS(l.id, p + ".id"),
                displayName = ReqS(l.display_name, p + ".display_name"),
                dimensions = MapArray(l.dimensions, p + ".dimensions", (x, q) => new DimensionDef
                {
                    id = ReqS(x.id, q + ".id"),
                    displayName = ReqS(x.display_name, q + ".display_name"),
                    weight = ReqF(x.weight, q + ".weight"),
                }),
            };
        }

        private static PersonalityGroupDef MapGroup(GroupDto g, string p)
        {
            return new PersonalityGroupDef
            {
                id = ReqS(g.id, p + ".id"),
                traits = MapArray(g.traits, p + ".traits", (t, q) => new TraitDef
                {
                    id = ReqS(t.id, q + ".id"),
                    displayName = ReqS(t.display_name, q + ".display_name"),
                }),
            };
        }

        private static PotentialConfig MapPotential(PotentialDto p)
        {
            return new PotentialConfig
            {
                talentThreshold = ReqF(p.talent_threshold, "potential.talent_threshold"),
                scoutingBias = ReqF(p.scouting_bias, "potential.scouting_bias"),
                estimateHalfWidth = ReqF(p.estimate_half_width, "potential.estimate_half_width"),
                estimateMinHalfWidth = ReqF(p.estimate_min_half_width, "potential.estimate_min_half_width"),
                observationPerSegment = ReqF(p.observation_per_segment, "potential.observation_per_segment"),
                observationPerTraining = ReqF(p.observation_per_training, "potential.observation_per_training"),
            };
        }

        private static TrainingConfig MapTraining(TrainingDto t)
        {
            return new TrainingConfig
            {
                baseGain = ReqF(t.base_gain, "training.base_gain"),
                stressFactor = ReqF(t.stress_factor, "training.stress_factor"),
                softCapFloor = ReqF(t.soft_cap_floor, "training.soft_cap_floor"),
                ageBands = MapArray(t.age_bands, "training.age_bands", (b, p) => new AgeCoefficientBand
                {
                    minAge = ReqI(b.min_age, p + ".min_age"),
                    maxAge = ReqI(b.max_age, p + ".max_age"),
                    coefficient = ReqF(b.coefficient, p + ".coefficient"),
                }),
            };
        }

        private static AgingConfig MapAging(AgingDto a)
        {
            return new AgingConfig
            {
                declineStartAge = ReqI(a.decline_start_age, "aging.decline_start_age"),
                declineBands = MapArray(a.decline_bands, "aging.decline_bands", (b, p) => new DeclineBand
                {
                    minAge = ReqI(b.min_age, p + ".min_age"),
                    maxAge = ReqI(b.max_age, p + ".max_age"),
                    amount = ReqF(b.amount, p + ".amount"),
                }),
            };
        }

        private static SalaryConfig MapSalary(SalaryDto s)
        {
            return new SalaryConfig
            {
                minSalary = ReqI(s.min_salary, "salary.min_salary"),
                maxSalary = ReqI(s.max_salary, "salary.max_salary"),
                weightLaneScore = ReqF(s.weight_lane_score, "salary.weight_lane_score"),
                weightPopularity = ReqF(s.weight_popularity, "salary.weight_popularity"),
                weightPotential = ReqF(s.weight_potential, "salary.weight_potential"),
                randomRange = ReqF(s.random_range, "salary.random_range"),
            };
        }

        private static GenerationConfig MapGeneration(GenerationDto g)
        {
            const string k = "generation.";
            return new GenerationConfig
            {
                playersPerLane = ReqI(g.players_per_lane, k + "players_per_lane"),
                minAge = ReqI(g.min_age, k + "min_age"),
                maxAge = ReqI(g.max_age, k + "max_age"),
                mainLaneDimFractionMin = ReqF(g.main_lane_dim_fraction_min, k + "main_lane_dim_fraction_min"),
                mainLaneDimFractionMax = ReqF(g.main_lane_dim_fraction_max, k + "main_lane_dim_fraction_max"),
                offLaneDimFractionMin = ReqF(g.off_lane_dim_fraction_min, k + "off_lane_dim_fraction_min"),
                offLaneDimFractionMax = ReqF(g.off_lane_dim_fraction_max, k + "off_lane_dim_fraction_max"),
                looksMin = ReqF(g.looks_min, k + "looks_min"),
                looksMax = ReqF(g.looks_max, k + "looks_max"),
                popularityMin = ReqF(g.popularity_min, k + "popularity_min"),
                popularityMax = ReqF(g.popularity_max, k + "popularity_max"),
                moodMin = ReqF(g.mood_min, k + "mood_min"),
                moodMax = ReqF(g.mood_max, k + "mood_max"),
                stressMin = ReqF(g.stress_min, k + "stress_min"),
                stressMax = ReqF(g.stress_max, k + "stress_max"),
                normalPotentialMin = ReqF(g.normal_potential_min, k + "normal_potential_min"),
                normalPotentialMax = ReqF(g.normal_potential_max, k + "normal_potential_max"),
                talentPotentialMin = ReqF(g.talent_potential_min, k + "talent_potential_min"),
                talentPotentialMax = ReqF(g.talent_potential_max, k + "talent_potential_max"),
                talentChance = ReqF(g.talent_chance, k + "talent_chance"),
                contractYearsMin = ReqI(g.contract_years_min, k + "contract_years_min"),
                contractYearsMax = ReqI(g.contract_years_max, k + "contract_years_max"),
                surnames = ReqStrings(g.surnames, k + "surnames"),
                givenNames = ReqStrings(g.given_names, k + "given_names"),
            };
        }
    }
}
