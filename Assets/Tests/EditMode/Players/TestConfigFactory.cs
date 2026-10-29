using System.Linq;
using EsportsManager.Players;

namespace EsportsManager.Tests.Players
{
    /// <summary>Test fixtures: an in-memory config with round numbers and a scripted random source. No file I/O.</summary>
    internal static class TestConfigFactory
    {
        public const int FixedSeed = 12345;

        // Fixture: lane id -> six dimension display names, copied from design/game-brief.md.
        public static readonly string[][] LaneTable =
        {
            new[] { "top", "每死承伤", "伤害占比", "开团能力", "参团率", "KDA", "指挥能力" },
            new[] { "jungle", "分均经济", "经济转化", "参团率", "KDA", "击杀贡献", "指挥能力" },
            new[] { "mid", "伤害占比", "参团率", "经济转化", "KDA", "控制时长", "指挥能力" },
            new[] { "adc", "发育能力", "经济转伤害", "KDA", "参团率", "生存能力", "指挥能力" },
            new[] { "support", "参团率", "每死承伤", "控制时长", "开团能力", "治疗量", "指挥能力" },
        };

        // Ten opposed groups: group id, trait one, trait two (numbers.md 3.1).
        public static readonly string[][] GroupTable =
        {
            new[] { "loyalty", "loyal", "selfish" },
            new[] { "temperament", "reserved", "lively" },
            new[] { "sincerity", "earnest", "slick" },
            new[] { "diligence", "diligent", "lazy" },
            new[] { "dominance", "assertive", "gentle" },
            new[] { "outlook", "optimistic", "pessimistic" },
            new[] { "resilience", "resolute", "fragile" },
            new[] { "worldview", "idealistic", "pragmatic" },
            new[] { "decisiveness", "decisive", "hesitant" },
            new[] { "wit", "clever", "honest" },
        };

        public static PlayerConfig Make(float discount = 0.8f)
        {
            var lanes = LaneTable.Select(row => new LaneDef
            {
                id = row[0],
                displayName = row[0],
                dimensions = row.Skip(1).Select((n, i) => new DimensionDef
                {
                    id = row[0] + "_" + i,
                    displayName = n,
                    weight = i == 0 ? 2f : 1f // first dimension weighs double
                }).ToArray()
            }).ToArray();

            return new PlayerConfig
            {
                offLaneDiscount = discount,
                lanes = lanes,
                personalityGroups = GroupTable.Select(r => new PersonalityGroupDef
                {
                    id = r[0],
                    traits = new[] { new TraitDef { id = r[1], displayName = r[1] }, new TraitDef { id = r[2], displayName = r[2] } }
                }).ToArray(),
                traitEffects = new[]
                {
                    Effect("diligent", "training_gain", 1.2f),
                    Effect("lazy", "training_gain", 0.8f),
                    Effect("clever", "training_gain", 1.1f),
                    Effect("clever", "off_lane_discount", 1.125f),
                    Effect("selfish", "ask_price", 1.2f),
                    Effect("honest", "ask_price", 0.9f),
                    Effect("lively", "stream_fans", 1.3f),             // key not read by Story 008 code
                    Effect("slick", "negative_compat_impact", 0.5f),   // halves negative h terms
                    Effect("gentle", "familiarity_gain", 1.1f),
                },
                traitPairs = new[]
                {
                    Pair("assertive", "gentle", 0.5f),
                    Pair("assertive", "fragile", 0.5f),
                    Pair("loyal", "loyal", 0.5f),
                    Pair("optimistic", "pessimistic", 0.3f),
                    Pair("assertive", "assertive", -0.5f),
                    Pair("selfish", "selfish", -0.5f),
                    Pair("diligent", "lazy", -0.5f),
                },
                training = new TrainingConfig
                {
                    baseGain = 2f,
                    capMargin = 10f, span = 25f, focusBonus = 0.5f, otherShare = 0.1f,
                    moodFactorMin = 0.8f, moodFactorMax = 1.2f, stressThreshold = 50f, stressSlope = 2.3f,
                    specializationRate = 0.163f, specializationGain = 0.6f, noise = 0.15f, nearCapMargin = 5f,
                    initialFocusHigh = 0.4f, initialFocusLow = 0.2f,
                    ageBands = new[]
                    {
                        new AgeCoefficientBand { minAge = 0, maxAge = 19, coefficient = 1.2f },
                        new AgeCoefficientBand { minAge = 20, maxAge = 22, coefficient = 1.0f },
                        new AgeCoefficientBand { minAge = 23, maxAge = 25, coefficient = 0.8f },
                        new AgeCoefficientBand { minAge = 26, maxAge = 28, coefficient = 0.5f },
                        new AgeCoefficientBand { minAge = 29, maxAge = 200, coefficient = 0.3f },
                    }
                },
                // growth.md thresholds; the table itself is empty here (Growth tests use the real dimension ids).
                specialty = new SpecialtyTable { unlockValue = 76f, loseValue = 73f, unlockFocus = 0.55f, loseFocus = 0.3f, specialties = new SpecialtyDef[0] },
                aging = new AgingConfig
                {
                    declineStartAge = 27,
                    declineBands = new[]
                    {
                        new DeclineBand { minAge = 27, maxAge = 28, amount = 1f },
                        new DeclineBand { minAge = 29, maxAge = 30, amount = 2f },
                        new DeclineBand { minAge = 31, maxAge = 200, amount = 4f },
                    }
                },
                potential = new PotentialConfig
                {
                    talentThreshold = 85f, scoutingBias = 10f,
                    estimateHalfWidth = 20f, estimateMinHalfWidth = 3f,
                    observationPerSegment = 5f, observationPerTraining = 3f,
                },
                salary = new SalaryConfig
                {
                    minSalary = 20000, maxSalary = 200000,
                    weightLaneScore = 1f, weightPopularity = 0.8f, weightPotential = 0.3f,
                    randomRange = 0.1f,
                },
                generation = new GenerationConfig
                {
                    playersPerLane = 5,
                    minAge = 17, maxAge = 30,
                    mainLaneDimFractionMin = 0.6f, mainLaneDimFractionMax = 0.95f,
                    offLaneDimFractionMin = 0.35f, offLaneDimFractionMax = 0.75f,
                    looksMin = 30, looksMax = 90,
                    popularityMin = 5, popularityMax = 60,
                    moodMin = 50, moodMax = 80,
                    stressMin = 10, stressMax = 40,
                    normalPotentialMin = 40, normalPotentialMax = 75,
                    talentPotentialMin = 85, talentPotentialMax = 95,
                    talentChance = 0.3f, // high so a seeded pool contains both kinds
                    contractYearsMin = 1, contractYearsMax = 3,
                    playerIds = new[] { "S1", "S2", "S3", "G1", "G2", "G3" },
                }
            };
        }

        public static TraitEffectDef Effect(string trait, string key, float m) =>
            new TraitEffectDef { traitId = trait, effectKey = key, multiplier = m };

        public static TraitPairDef Pair(string a, string b, float v) =>
            new TraitPairDef { traitA = a, traitB = b, value = v };

        /// <summary>Radar filled with one value on every lane.</summary>
        public static System.Collections.Generic.Dictionary<string, float[]> FilledRadar(PlayerConfig cfg, float fill) =>
            cfg.lanes.ToDictionary(l => l.id, l => Enumerable.Repeat(fill, l.dimensions.Length).ToArray());

        /// <summary>Builds a player; defaults: age 22, mid, traits diligent + gentle, potential 80, 2 contract years, stress 0.</summary>
        public static Player MakePlayer(PlayerConfig cfg, int age = 22, string mainLane = "mid", float fill = 50f,
            string traitA = "diligent", string traitB = "gentle", float potential = 80f, int contractYears = 2,
            float stress = 0f, float popularity = 50f, int salary = 1000, long id = 1, float scoutingBias = 0f)
        {
            return new Player(cfg, id, "Test", age, mainLane, traitA, traitB, potential, contractYears,
                50f, popularity, 50f, stress, salary, FilledRadar(cfg, fill), scoutingBias);
        }
    }

    /// <summary>Scripted random source: NextFloat returns min + (max-min)*Fraction, NextInt returns min.</summary>
    internal sealed class StubRandomSource : IRandomSource
    {
        public float Fraction;
        public StubRandomSource(float fraction) { Fraction = fraction; }
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat(float minInclusive, float maxInclusive) => minInclusive + (maxInclusive - minInclusive) * Fraction;
    }
}
