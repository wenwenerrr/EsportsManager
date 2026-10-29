using System;
using System.Collections.Generic;

namespace EsportsManager.Players
{
    /// <summary>One scored dimension of a lane (id, display name, weight). Data from config.</summary>
    public sealed class DimensionDef
    {
        /// <summary>Stable identifier.</summary>
        public string id;
        /// <summary>Name shown to the player.</summary>
        public string displayName;
        /// <summary>Weight in the lane score; must be >= 0, and the lane's weights must sum to more than 0.</summary>
        public float weight;
    }

    /// <summary>A lane and its ordered dimensions. Data from config.</summary>
    public sealed class LaneDef
    {
        /// <summary>Stable identifier.</summary>
        public string id;
        /// <summary>Name shown to the player.</summary>
        public string displayName;
        /// <summary>Ordered dimensions of this lane's radar.</summary>
        public DimensionDef[] dimensions;
    }

    /// <summary>One personality word (numbers.md 3.1). Data from config.</summary>
    public sealed class TraitDef
    {
        /// <summary>Stable English identifier, unique across all groups (e.g. "diligent").</summary>
        public string id;
        /// <summary>Chinese word shown to the player (e.g. "勤奋").</summary>
        public string displayName;
    }

    /// <summary>One opposed personality group: exactly two traits (numbers.md 3.1). Data from config.</summary>
    public sealed class PersonalityGroupDef
    {
        /// <summary>Stable identifier, unique across groups.</summary>
        public string id;
        /// <summary>The two opposed traits.</summary>
        public TraitDef[] traits;
    }

    /// <summary>
    /// One entry of the personality effect table (numbers.md 3.2): trait <see cref="traitId"/>
    /// multiplies effect <see cref="effectKey"/> by <see cref="multiplier"/>. Unlisted pairs mean x1.
    /// </summary>
    public sealed class TraitEffectDef
    {
        /// <summary>Id of a trait defined in a group.</summary>
        public string traitId;
        /// <summary>Effect key; must be one of <see cref="PlayerEffectKeys.All"/>.</summary>
        public string effectKey;
        /// <summary>Multiplier, must be > 0.</summary>
        public float multiplier;
    }

    /// <summary>One entry of the compatibility table (numbers.md 3.3). Unordered: (a,b) equals (b,a).</summary>
    public sealed class TraitPairDef
    {
        /// <summary>First trait id.</summary>
        public string traitA;
        /// <summary>Second trait id (may equal <see cref="traitA"/> for "same word" pairs).</summary>
        public string traitB;
        /// <summary>Compatibility h, within [-1, +1].</summary>
        public float value;
    }

    /// <summary>Training age coefficient for an inclusive age range (numbers.md 4.1).</summary>
    public sealed class AgeCoefficientBand
    {
        /// <summary>Lowest age (inclusive).</summary>
        public int minAge;
        /// <summary>Highest age (inclusive).</summary>
        public int maxAge;
        /// <summary>Multiplier on training gain, >= 0.</summary>
        public float coefficient;
    }

    /// <summary>Yearly decline of every dimension for an inclusive age range (numbers.md 4.1).</summary>
    public sealed class DeclineBand
    {
        /// <summary>Lowest age (inclusive).</summary>
        public int minAge;
        /// <summary>Highest age (inclusive).</summary>
        public int maxAge;
        /// <summary>Points subtracted from every dimension per year, >= 0.</summary>
        public float amount;
    }

    /// <summary>
    /// Training and specialization knobs (design/gdd/growth.md rev 3, F1-F3; age bands from numbers.md 4.1). Replaces the
    /// numbers.md 4.1 "six dims + target x1.5" model and its per-dimension soft cap.
    /// </summary>
    public sealed class TrainingConfig
    {
        /// <summary>T_base (2): base amount b of one training point with every factor at 1. Must be >= 0.</summary>
        public float baseGain;
        /// <summary>Age coefficient bands (age_f). Ages not covered by any band use coefficient 1.</summary>
        public AgeCoefficientBand[] ageBands;
        /// <summary>cap_margin (10): every dimension's ceiling is min(100, potential + cap_margin). In [0, 100].</summary>
        public float capMargin;
        /// <summary>span (25): head_d = clamp((cap - d) / span, 0, 1). Must be > 0.</summary>
        public float span;
        /// <summary>bonus (0.5): share the focus dimension takes from its phase mates (phase mean unchanged). In [0, 1].</summary>
        public float focusBonus;
        /// <summary>s_other (0.1): weight of every dimension outside the trained phase. In [0, 1].</summary>
        public float otherShare;
        /// <summary>mood_t at mood 0 (0.8); mood_t = min + (max - min) x mood/100. >= 0.</summary>
        public float moodFactorMin;
        /// <summary>mood_t at mood 100 (1.2), >= <see cref="moodFactorMin"/>.</summary>
        public float moodFactorMax;
        /// <summary>Stress at or below this does not slow training (50). In [0, 100].</summary>
        public float stressThreshold;
        /// <summary>k_th (2.3): stress_t = max(0, 1 - k_th x max(0, stress - threshold)/100). >= 0.</summary>
        public float stressSlope;
        /// <summary>mu (0.163): specialization step per training point (F3). In (0, 1).</summary>
        public float specializationRate;
        /// <summary>phi (0.6): spec = 1 + phi x focus of the trained phase. >= 0.</summary>
        public float specializationGain;
        /// <summary>Training noise half-width (0.15): each point's b is multiplied by U(1 - noise, 1 + noise). In [0, 1).</summary>
        public float noise;
        /// <summary>UI "接近上限" marker when cap - d is below this (5). >= 0. Display only.</summary>
        public float nearCapMargin;
        /// <summary>talent.md initial focus of the phase with the highest phase mean (0.4). In [0, 1].</summary>
        public float initialFocusHigh;
        /// <summary>talent.md initial focus of the other two phases (0.2). In [0, 1].</summary>
        public float initialFocusLow;
    }

    /// <summary>One match hook of a specialty (growth.md specialty table; hook ids from match.md "绝活挂钩点").</summary>
    public sealed class SpecialtyEffectDef
    {
        /// <summary>"phase_flat", "star_scale", "exec_add" or "sigma_scale".</summary>
        public string hook;
        /// <summary>"early" / "mid" / "late" for phase_flat (star_scale: early or late only); "none" for exec_add and sigma_scale.</summary>
        public string phase;
        /// <summary>Added value (phase_flat, exec_add) or multiplier &gt; 0 (star_scale, sigma_scale).</summary>
        public float value;
    }

    /// <summary>One specialty (绝活) of the growth.md table. Data from config.</summary>
    public sealed class SpecialtyDef
    {
        /// <summary>Stable id, unique.</summary>
        public string id;
        /// <summary>Chinese name shown to the player.</summary>
        public string displayName;
        /// <summary>Lane id, or <see cref="SpecialtyTable.AnyLane"/> for a specialty every lane can learn (tactical brain).</summary>
        public string lane;
        /// <summary>"early" / "mid" / "late"; <see cref="SpecialtyTable.NoPhase"/> for an any-lane specialty (it ignores focus).</summary>
        public string phase;
        /// <summary>Dimension id checked against the unlock / lose values (on the player's main lane).</summary>
        public string dimension;
        /// <summary>Match hooks while the owner starts on his main lane (at least one).</summary>
        public SpecialtyEffectDef[] effects;

        /// <summary>True for an any-lane specialty.</summary>
        public bool IsAnyLane => lane == SpecialtyTable.AnyLane;
    }

    /// <summary>Specialty rules and table (growth.md F4 and "绝活表").</summary>
    public sealed class SpecialtyTable
    {
        /// <summary>Lane value of a specialty every lane can learn.</summary>
        public const string AnyLane = "any";
        /// <summary>Phase value of an any-lane specialty and of hooks without a phase.</summary>
        public const string NoPhase = "none";
        /// <summary>Phase ids in index order (index = MatchPhase value).</summary>
        public static readonly string[] PhaseIds = { "early", "mid", "late" };
        /// <summary>Known hook ids (match.md).</summary>
        public static readonly string[] HookIds = { "phase_flat", "star_scale", "exec_add", "sigma_scale" };

        /// <summary>Gain when the dimension is at least this (76).</summary>
        public float unlockValue;
        /// <summary>Lose when the dimension is below this (73).</summary>
        public float loseValue;
        /// <summary>Gain needs the phase focus at least this (0.55).</summary>
        public float unlockFocus;
        /// <summary>Lose when the phase focus is below this (0.30).</summary>
        public float loseFocus;
        /// <summary>The table, config order.</summary>
        public SpecialtyDef[] specialties;

        /// <summary>Index of a phase id in <see cref="PhaseIds"/>, or -1.</summary>
        public static int PhaseIndex(string phase) => Array.IndexOf(PhaseIds, phase);

        /// <summary>Finds a specialty by id, or null.</summary>
        public SpecialtyDef Find(string id)
        {
            if (specialties == null) return null;
            foreach (var s in specialties) if (s != null && s.id == id) return s;
            return null;
        }
    }

    /// <summary>Yearly decline knobs (numbers.md 4.1).</summary>
    public sealed class AgingConfig
    {
        /// <summary>age_decline_start: players this age or older decline in AdvanceYear. Must be >= 0.</summary>
        public int declineStartAge;
        /// <summary>Decline bands. Ages at or above the start but outside every band decline by 0.</summary>
        public DeclineBand[] declineBands;
    }

    /// <summary>Potential knobs (numbers.md 2, 4.13, 4.15).</summary>
    public sealed class PotentialConfig
    {
        /// <summary>True potential at or above this value marks a talent player. Within [0, 100].</summary>
        public float talentThreshold;
        /// <summary>b: each generated player's scouting bias is rolled in U(-b, +b). Within [0, 100].</summary>
        public float scoutingBias;
        /// <summary>u0: estimate half-width at observation 0. >= 0.</summary>
        public float estimateHalfWidth;
        /// <summary>u_min: the estimate half-width never shrinks below this. >= 0.</summary>
        public float estimateMinHalfWidth;
        /// <summary>o_seg: observation per segment on the team. Stored for Story 003. >= 0.</summary>
        public float observationPerSegment;
        /// <summary>o_train: observation per training. Stored for Story 003. >= 0.</summary>
        public float observationPerTraining;
    }

    /// <summary>Annual salary formula knobs (numbers.md 4.4). Money is integer yuan.</summary>
    public sealed class SalaryConfig
    {
        /// <summary>S_min, yuan per year. Result is never below this.</summary>
        public int minSalary;
        /// <summary>S_max, yuan per year. Result is never above this. Must be >= S_min.</summary>
        public int maxSalary;
        /// <summary>WS weight of the main-lane score.</summary>
        public float weightLaneScore;
        /// <summary>WS weight of popularity.</summary>
        public float weightPopularity;
        /// <summary>WS weight of potential.</summary>
        public float weightPotential;
        /// <summary>r of R(r): random factor is uniform in [1-r, 1+r]. Within [0, 1).</summary>
        public float randomRange;
    }

    /// <summary>Ranges used to generate the fictional player pool. Every min must be &lt;= its max.</summary>
    public sealed class GenerationConfig
    {
        /// <summary>Players generated for each lane as their main lane. Must be > 0.</summary>
        public int playersPerLane;
        /// <summary>Age range (inclusive).</summary>
        public int minAge, maxAge;
        /// <summary>Main-lane dimension = potential x U(min, max) (numbers.md 4.13). Fractions within [0, 1].</summary>
        public float mainLaneDimFractionMin, mainLaneDimFractionMax;
        /// <summary>Off-lane dimension = potential x U(min, max). Fractions within [0, 1].</summary>
        public float offLaneDimFractionMin, offLaneDimFractionMax;
        /// <summary>Looks range.</summary>
        public float looksMin, looksMax;
        /// <summary>Popularity range.</summary>
        public float popularityMin, popularityMax;
        /// <summary>Mood range.</summary>
        public float moodMin, moodMax;
        /// <summary>Stress range.</summary>
        public float stressMin, stressMax;
        /// <summary>Potential range of ordinary players (numbers.md 4.13: 40-75).</summary>
        public float normalPotentialMin, normalPotentialMax;
        /// <summary>Potential range of talent players. Its min must be >= the talent threshold.</summary>
        public float talentPotentialMin, talentPotentialMax;
        /// <summary>Probability in [0, 1] that a generated player is a talent.</summary>
        public float talentChance;
        /// <summary>Initial contract years range (inclusive), min >= 0.</summary>
        public int contractYearsMin, contractYearsMax;
        /// <summary>Fictional esports IDs (the player's whole name), unique; a number is appended only when the pool runs out.</summary>
        public string[] playerIds;
    }

    /// <summary>
    /// Root of all player-model tuning data. Plain data built by <see cref="PlayerConfigLoader"/>
    /// from JSON (or in memory by tests); no gameplay number lives in code and fields have no
    /// code-side defaults. Lookups are linear scans over tiny tables so edits to the arrays
    /// are always seen and nothing allocates.
    /// </summary>
    public sealed class PlayerConfig
    {
        /// <summary>Multiplier in (0,1) applied to the lane score off the main lane.</summary>
        public float offLaneDiscount;
        /// <summary>All lanes with their dimensions.</summary>
        public LaneDef[] lanes;
        /// <summary>Opposed personality groups (numbers.md 3.1).</summary>
        public PersonalityGroupDef[] personalityGroups;
        /// <summary>Personality effect table (numbers.md 3.2).</summary>
        public TraitEffectDef[] traitEffects;
        /// <summary>Personality compatibility table (numbers.md 3.3).</summary>
        public TraitPairDef[] traitPairs;
        /// <summary>Training formula knobs (growth.md).</summary>
        public TrainingConfig training;
        /// <summary>Specialty rules and table (growth.md).</summary>
        public SpecialtyTable specialty;
        /// <summary>Age decline knobs.</summary>
        public AgingConfig aging;
        /// <summary>Potential knobs.</summary>
        public PotentialConfig potential;
        /// <summary>Salary formula knobs.</summary>
        public SalaryConfig salary;
        /// <summary>Pool generation ranges.</summary>
        public GenerationConfig generation;

        /// <summary>Finds a lane by id, or null.</summary>
        public LaneDef FindLane(string laneId)
        {
            if (lanes == null) return null;
            for (int i = 0; i < lanes.Length; i++)
                if (lanes[i].id == laneId) return lanes[i];
            return null;
        }

        /// <summary>Index into <see cref="personalityGroups"/> of the group containing the trait, or -1.</summary>
        /// <example><code>bool known = config.FindTraitGroup("diligent") >= 0;</code></example>
        public int FindTraitGroup(string traitId)
        {
            if (personalityGroups == null || traitId == null) return -1;
            for (int g = 0; g < personalityGroups.Length; g++)
            {
                var traits = personalityGroups[g].traits;
                for (int t = 0; t < traits.Length; t++)
                    if (traits[t].id == traitId) return g;
            }
            return -1;
        }

        /// <summary>Multiplier the trait applies to an effect key; 1 when the table has no entry.</summary>
        /// <example><code>float m = config.GetEffectMultiplier("diligent", "training_gain"); // 1.2</code></example>
        public float GetEffectMultiplier(string traitId, string effectKey)
        {
            if (traitEffects == null) return 1f;
            for (int i = 0; i < traitEffects.Length; i++)
                if (traitEffects[i].traitId == traitId && traitEffects[i].effectKey == effectKey)
                    return traitEffects[i].multiplier;
            return 1f;
        }

        /// <summary>Compatibility h of an unordered trait pair; 0 when the table has no entry.</summary>
        /// <example><code>float h = config.GetPairCompatibility("assertive", "gentle"); // 0.5</code></example>
        public float GetPairCompatibility(string traitA, string traitB)
        {
            if (traitPairs == null) return 0f;
            for (int i = 0; i < traitPairs.Length; i++)
            {
                var p = traitPairs[i];
                if ((p.traitA == traitA && p.traitB == traitB) || (p.traitA == traitB && p.traitB == traitA))
                    return p.value;
            }
            return 0f;
        }

        /// <summary>Training age coefficient for an age; 1 when no band covers it.</summary>
        public float GetAgeCoefficient(int age)
        {
            var bands = training?.ageBands;
            if (bands == null) return 1f;
            for (int i = 0; i < bands.Length; i++)
                if (age >= bands[i].minAge && age <= bands[i].maxAge) return bands[i].coefficient;
            return 1f;
        }

        /// <summary>
        /// Points every dimension loses when a player of this age passes a year: 0 below
        /// <see cref="AgingConfig.declineStartAge"/> or when no band covers the age.
        /// </summary>
        public float GetDeclineAmount(int age)
        {
            if (aging == null || age < aging.declineStartAge || aging.declineBands == null) return 0f;
            for (int i = 0; i < aging.declineBands.Length; i++)
                if (age >= aging.declineBands[i].minAge && age <= aging.declineBands[i].maxAge)
                    return aging.declineBands[i].amount;
            return 0f;
        }

        /// <summary>
        /// Checks the config is usable. Throws <see cref="InvalidOperationException"/> naming the field.
        /// Rules: lanes (at least one, unique ids, dimensions with unique ids, non-negative weights, weight
        /// sum &gt; 0); offLaneDiscount in (0,1); at least two personality groups with unique ids, exactly two
        /// non-empty trait ids each, no trait id in two groups; effect table entries reference known traits,
        /// have a key, a multiplier &gt; 0 and are unique; pair table entries reference known traits, have
        /// a value in [-1,1] and are unique as unordered pairs; training (growth.md knobs in their ranges, age bands valid
        /// and non-overlapping); specialty (thresholds, the two growth.md interval rules, every specialty's lane / phase /
        /// dimension / hooks known — errors name the specialty id); aging (start &gt;= 0, decline bands valid, non-overlapping,
        /// amount &gt;= 0); talentThreshold in [0,100]; salary (min &lt;= max, weights &gt;= 0 with sum &gt; 0,
        /// randomRange in [0,1)); generation (playersPerLane &gt; 0, every min &lt;= max, 0-100 ranges inside
        /// 0-100, talent potential min &gt;= threshold, chance in [0,1], contract years min &gt;= 0, non-empty name pools).
        /// Every float must be finite (no NaN / Infinity); effect keys must be in <see cref="PlayerEffectKeys.All"/>;
        /// potential estimate knobs &gt;= 0; dimension fractions in [0,1].
        /// Gaps between bands are allowed.
        /// </summary>
        /// <example><code>config.Validate(); // throws InvalidOperationException if unusable</code></example>
        public void Validate()
        {
            ValidateFinite();
            ValidateLanes();
            if (!(offLaneDiscount > 0f && offLaneDiscount < 1f))
                throw Fail("offLaneDiscount must be in (0,1), got " + offLaneDiscount + ".");
            ValidatePersonality();
            ValidateTraining();
            ValidateSpecialty();
            ValidateAging();
            ValidatePotential();
            ValidateSalary();
            ValidateGeneration();
        }

        private void ValidateLanes()
        {
            if (lanes == null || lanes.Length == 0) throw Fail("no lanes.");
            var laneIds = new HashSet<string>();
            foreach (var lane in lanes)
            {
                if (lane == null || string.IsNullOrEmpty(lane.id)) throw Fail("lane without id.");
                if (!laneIds.Add(lane.id)) throw Fail("duplicate lane id '" + lane.id + "'.");
                if (lane.dimensions == null || lane.dimensions.Length == 0)
                    throw Fail("lane '" + lane.id + "' has no dimensions.");
                var dimIds = new HashSet<string>();
                float weightSum = 0f;
                foreach (var d in lane.dimensions)
                {
                    if (d == null || string.IsNullOrEmpty(d.id)) throw Fail("lane '" + lane.id + "' has a dimension without id.");
                    if (!dimIds.Add(d.id)) throw Fail("lane '" + lane.id + "' has duplicate dimension id '" + d.id + "'.");
                    if (!(d.weight >= 0f)) // also rejects NaN
                        throw Fail("lane '" + lane.id + "' dimension '" + d.id + "' weight must be >= 0.");
                    weightSum += d.weight;
                }
                if (!(weightSum > 0f))
                    throw Fail("lane '" + lane.id + "' weights sum to 0; lane score would divide by zero.");
            }
        }

        private void ValidatePersonality()
        {
            if (personalityGroups == null || personalityGroups.Length < 2)
                throw Fail("personalityGroups needs at least 2 groups (a player takes one trait from each of two groups).");
            var groupIds = new HashSet<string>();
            var traitToGroup = new Dictionary<string, string>();
            foreach (var group in personalityGroups)
            {
                if (group == null || string.IsNullOrEmpty(group.id)) throw Fail("personality group without id.");
                if (!groupIds.Add(group.id)) throw Fail("duplicate personality group id '" + group.id + "'.");
                if (group.traits == null || group.traits.Length != 2)
                    throw Fail("personality group '" + group.id + "' must have exactly 2 traits.");
                foreach (var trait in group.traits)
                {
                    if (trait == null || string.IsNullOrEmpty(trait.id))
                        throw Fail("personality group '" + group.id + "' has a trait without id.");
                    if (traitToGroup.TryGetValue(trait.id, out var other))
                        throw Fail("trait '" + trait.id + "' is in more than one group ('" + other + "' and '" + group.id + "').");
                    traitToGroup[trait.id] = group.id;
                }
            }

            if (traitEffects == null) throw Fail("missing traitEffects.");
            var effectKeys = new HashSet<string>();
            for (int i = 0; i < traitEffects.Length; i++)
            {
                var e = traitEffects[i];
                if (e == null) throw Fail("traitEffects[" + i + "] is null.");
                if (!traitToGroup.ContainsKey(e.traitId ?? ""))
                    throw Fail("traitEffects[" + i + "].traitId references unknown trait '" + e.traitId + "'.");
                if (string.IsNullOrEmpty(e.effectKey)) throw Fail("traitEffects[" + i + "].effectKey is empty.");
                if (!PlayerEffectKeys.IsKnown(e.effectKey))
                    throw Fail("traitEffects[" + i + "].effectKey '" + e.effectKey + "' is not a known effect key (see PlayerEffectKeys).");
                if (!(e.multiplier > 0f)) // also rejects NaN
                    throw Fail("traitEffects[" + i + "].multiplier must be > 0, got " + e.multiplier + ".");
                if (!effectKeys.Add(e.traitId + "\n" + e.effectKey))
                    throw Fail("traitEffects[" + i + "] duplicates trait '" + e.traitId + "' effect '" + e.effectKey + "'.");
            }

            if (traitPairs == null) throw Fail("missing traitPairs.");
            var pairKeys = new HashSet<string>();
            for (int i = 0; i < traitPairs.Length; i++)
            {
                var p = traitPairs[i];
                if (p == null) throw Fail("traitPairs[" + i + "] is null.");
                if (!traitToGroup.ContainsKey(p.traitA ?? ""))
                    throw Fail("traitPairs[" + i + "].traitA references unknown trait '" + p.traitA + "'.");
                if (!traitToGroup.ContainsKey(p.traitB ?? ""))
                    throw Fail("traitPairs[" + i + "].traitB references unknown trait '" + p.traitB + "'.");
                if (!(p.value >= -1f && p.value <= 1f)) // also rejects NaN
                    throw Fail("traitPairs[" + i + "].value must be in [-1,1], got " + p.value + ".");
                string key = string.CompareOrdinal(p.traitA, p.traitB) <= 0 ? p.traitA + "\n" + p.traitB : p.traitB + "\n" + p.traitA;
                if (!pairKeys.Add(key))
                    throw Fail("traitPairs[" + i + "] duplicates pair '" + p.traitA + "' + '" + p.traitB + "'.");
            }
        }

        private void ValidateTraining()
        {
            var t = training ?? throw Fail("missing training.");
            if (!(t.baseGain >= 0f)) throw Fail("training.baseGain must be >= 0, got " + t.baseGain + ".");
            if (!(t.capMargin >= 0f && t.capMargin <= 100f)) throw Fail("training.capMargin must be in [0,100], got " + t.capMargin + ".");
            if (!(t.span > 0f)) throw Fail("training.span must be > 0, got " + t.span + ".");
            if (!(t.focusBonus >= 0f && t.focusBonus <= 1f)) throw Fail("training.focusBonus must be in [0,1], got " + t.focusBonus + ".");
            if (!(t.otherShare >= 0f && t.otherShare <= 1f)) throw Fail("training.otherShare must be in [0,1], got " + t.otherShare + ".");
            if (!(t.moodFactorMin >= 0f && t.moodFactorMax >= t.moodFactorMin))
                throw Fail("training.moodFactorMin/Max must satisfy 0 <= min <= max, got " + t.moodFactorMin + ".." + t.moodFactorMax + ".");
            if (!(t.stressThreshold >= 0f && t.stressThreshold <= 100f))
                throw Fail("training.stressThreshold must be in [0,100], got " + t.stressThreshold + ".");
            if (!(t.stressSlope >= 0f)) throw Fail("training.stressSlope must be >= 0, got " + t.stressSlope + ".");
            if (!(t.specializationRate > 0f && t.specializationRate < 1f))
                throw Fail("training.specializationRate must be in (0,1), got " + t.specializationRate + ".");
            if (!(t.specializationGain >= 0f)) throw Fail("training.specializationGain must be >= 0, got " + t.specializationGain + ".");
            if (!(t.noise >= 0f && t.noise < 1f)) throw Fail("training.noise must be in [0,1), got " + t.noise + ".");
            if (!(t.nearCapMargin >= 0f)) throw Fail("training.nearCapMargin must be >= 0, got " + t.nearCapMargin + ".");
            if (!(t.initialFocusHigh >= 0f && t.initialFocusHigh <= 1f)) throw Fail("training.initialFocusHigh must be in [0,1].");
            if (!(t.initialFocusLow >= 0f && t.initialFocusLow <= 1f)) throw Fail("training.initialFocusLow must be in [0,1].");
            if (t.ageBands == null || t.ageBands.Length == 0) throw Fail("training.ageBands is empty.");
            var spans = new List<int[]>();
            for (int i = 0; i < t.ageBands.Length; i++)
            {
                var b = t.ageBands[i] ?? throw Fail("training.ageBands[" + i + "] is null.");
                if (!(b.coefficient >= 0f))
                    throw Fail("training.ageBands[" + i + "].coefficient must be >= 0, got " + b.coefficient + ".");
                CheckBand("training.ageBands", i, b.minAge, b.maxAge, spans);
            }
        }

        // growth.md F4 and the specialty table. Every per-specialty error names the specialty id. Phase membership of the
        // dimension is checked later against the match phase mapping (GrowthSystem), which this config does not own.
        private void ValidateSpecialty()
        {
            var s = specialty ?? throw Fail("missing specialty.");
            if (!(s.unlockValue >= 0f && s.unlockValue <= 100f)) throw Fail("specialty.unlockValue must be in [0,100], got " + s.unlockValue + ".");
            if (!(s.loseValue >= 0f && s.loseValue <= s.unlockValue))
                throw Fail("specialty.loseValue must be in [0, unlockValue], got " + s.loseValue + ".");
            if (!(s.loseFocus >= 0f && s.unlockFocus > s.loseFocus && s.unlockFocus <= 1f))
                throw Fail("specialty focus thresholds must satisfy 0 <= loseFocus < unlockFocus <= 1, got " + s.loseFocus + " / " + s.unlockFocus + ".");
            float mu = training.specializationRate;
            // One training point must not re-activate a lost specialty; one idle point must not drop a fresh one (growth.md F4).
            if (!(s.unlockFocus > mu + (1f - mu) * s.loseFocus))
                throw Fail("specialty interval rule violated: unlockFocus " + s.unlockFocus + " must be > mu + (1 - mu) x loseFocus = " + (mu + (1f - mu) * s.loseFocus) + ".");
            if (!((1f - mu) * s.unlockFocus >= s.loseFocus))
                throw Fail("specialty interval rule violated: (1 - mu) x unlockFocus = " + ((1f - mu) * s.unlockFocus) + " must be >= loseFocus " + s.loseFocus + ".");
            if (s.specialties == null) throw Fail("missing specialty.specialties.");
            var ids = new HashSet<string>();
            for (int i = 0; i < s.specialties.Length; i++)
            {
                var d = s.specialties[i] ?? throw Fail("specialty.specialties[" + i + "] is null.");
                string who = "specialty '" + d.id + "'";
                if (string.IsNullOrEmpty(d.id)) throw Fail("specialty.specialties[" + i + "] has no id.");
                if (!ids.Add(d.id)) throw Fail("duplicate " + who + ".");
                if (d.IsAnyLane)
                {
                    if (d.phase != SpecialtyTable.NoPhase) throw Fail(who + ": an any-lane specialty must have phase '" + SpecialtyTable.NoPhase + "', got '" + d.phase + "'.");
                    foreach (var lane in lanes)
                        if (DimIndex(lane, d.dimension) < 0) throw Fail(who + ": unknown dimension '" + d.dimension + "' in lane '" + lane.id + "'.");
                }
                else
                {
                    var lane = FindLane(d.lane) ?? throw Fail(who + ": unknown lane '" + d.lane + "'.");
                    if (SpecialtyTable.PhaseIndex(d.phase) < 0) throw Fail(who + ": unknown phase '" + d.phase + "'.");
                    if (DimIndex(lane, d.dimension) < 0) throw Fail(who + ": unknown dimension '" + d.dimension + "' in lane '" + d.lane + "'.");
                }
                if (d.effects == null || d.effects.Length == 0) throw Fail(who + " has no effects.");
                foreach (var e in d.effects)
                {
                    if (e == null) throw Fail(who + " has a null effect.");
                    if (Array.IndexOf(SpecialtyTable.HookIds, e.hook) < 0) throw Fail(who + ": unknown hook '" + e.hook + "'.");
                    if (float.IsNaN(e.value) || float.IsInfinity(e.value)) throw Fail(who + ": hook '" + e.hook + "' value must be finite.");
                    int ph = SpecialtyTable.PhaseIndex(e.phase);
                    switch (e.hook)
                    {
                        case "phase_flat":
                            if (ph < 0) throw Fail(who + ": phase_flat needs phase early/mid/late, got '" + e.phase + "'.");
                            break;
                        case "star_scale":
                            if (ph != 0 && ph != 2) throw Fail(who + ": star_scale needs phase early or late, got '" + e.phase + "'.");
                            if (!(e.value > 0f)) throw Fail(who + ": star_scale must be > 0.");
                            break;
                        default:
                            if (e.phase != SpecialtyTable.NoPhase) throw Fail(who + ": " + e.hook + " takes phase '" + SpecialtyTable.NoPhase + "', got '" + e.phase + "'.");
                            if (e.hook == "sigma_scale" && !(e.value > 0f)) throw Fail(who + ": sigma_scale must be > 0.");
                            break;
                    }
                }
            }
        }

        /// <summary>Index of a dimension id in a lane, or -1.</summary>
        public static int DimIndex(LaneDef lane, string dimensionId)
        {
            if (lane?.dimensions == null) return -1;
            for (int i = 0; i < lane.dimensions.Length; i++) if (lane.dimensions[i].id == dimensionId) return i;
            return -1;
        }

        private void ValidateAging()
        {
            var a = aging ?? throw Fail("missing aging.");
            if (a.declineStartAge < 0) throw Fail("aging.declineStartAge must be >= 0, got " + a.declineStartAge + ".");
            if (a.declineBands == null || a.declineBands.Length == 0) throw Fail("aging.declineBands is empty.");
            var spans = new List<int[]>();
            for (int i = 0; i < a.declineBands.Length; i++)
            {
                var b = a.declineBands[i] ?? throw Fail("aging.declineBands[" + i + "] is null.");
                if (!(b.amount >= 0f))
                    throw Fail("aging.declineBands[" + i + "].amount must be >= 0, got " + b.amount + ".");
                CheckBand("aging.declineBands", i, b.minAge, b.maxAge, spans);
            }
        }

        private static void CheckBand(string name, int index, int minAge, int maxAge, List<int[]> previous)
        {
            if (minAge > maxAge)
                throw Fail(name + "[" + index + "] minAge " + minAge + " > maxAge " + maxAge + ".");
            for (int j = 0; j < previous.Count; j++)
                if (minAge <= previous[j][1] && previous[j][0] <= maxAge)
                    throw Fail(name + "[" + previous[j][2] + "] (" + previous[j][0] + "-" + previous[j][1] + ") overlaps " + name + "[" + index + "] (" + minAge + "-" + maxAge + ").");
            previous.Add(new[] { minAge, maxAge, index });
        }

        private void ValidatePotential()
        {
            var p = potential ?? throw Fail("missing potential.");
            if (!(p.talentThreshold >= 0f && p.talentThreshold <= 100f))
                throw Fail("potential.talentThreshold must be in [0,100], got " + p.talentThreshold + ".");
            if (!(p.scoutingBias >= 0f && p.scoutingBias <= 100f))
                throw Fail("potential.scoutingBias must be in [0,100], got " + p.scoutingBias + ".");
            if (!(p.estimateHalfWidth >= 0f)) throw Fail("potential.estimateHalfWidth must be >= 0.");
            if (!(p.estimateMinHalfWidth >= 0f)) throw Fail("potential.estimateMinHalfWidth must be >= 0.");
            if (!(p.observationPerSegment >= 0f)) throw Fail("potential.observationPerSegment must be >= 0.");
            if (!(p.observationPerTraining >= 0f)) throw Fail("potential.observationPerTraining must be >= 0.");
        }

        private void ValidateSalary()
        {
            var s = salary ?? throw Fail("missing salary.");
            if (s.minSalary < 0) throw Fail("salary.minSalary must be >= 0, got " + s.minSalary + ".");
            if (s.minSalary > s.maxSalary)
                throw Fail("salary.minSalary " + s.minSalary + " > maxSalary " + s.maxSalary + ".");
            if (!(s.weightLaneScore >= 0f)) throw Fail("salary.weightLaneScore must be >= 0.");
            if (!(s.weightPopularity >= 0f)) throw Fail("salary.weightPopularity must be >= 0.");
            if (!(s.weightPotential >= 0f)) throw Fail("salary.weightPotential must be >= 0.");
            if (!(s.weightLaneScore + s.weightPopularity + s.weightPotential > 0f))
                throw Fail("salary weights sum to 0.");
            if (!(s.randomRange >= 0f && s.randomRange < 1f))
                throw Fail("salary.randomRange must be in [0,1), got " + s.randomRange + ".");
        }

        private void ValidateGeneration()
        {
            var g = generation ?? throw Fail("missing generation.");
            if (g.playersPerLane <= 0) throw Fail("generation.playersPerLane must be > 0, got " + g.playersPerLane + ".");
            CheckRange("age", g.minAge, g.maxAge);
            if (g.minAge < 0) throw Fail("generation.age min must be >= 0.");
            CheckFractionRange("mainLaneDimFraction", g.mainLaneDimFractionMin, g.mainLaneDimFractionMax);
            CheckFractionRange("offLaneDimFraction", g.offLaneDimFractionMin, g.offLaneDimFractionMax);
            CheckStatRange("looks", g.looksMin, g.looksMax);
            CheckStatRange("popularity", g.popularityMin, g.popularityMax);
            CheckStatRange("mood", g.moodMin, g.moodMax);
            CheckStatRange("stress", g.stressMin, g.stressMax);
            CheckStatRange("normalPotential", g.normalPotentialMin, g.normalPotentialMax);
            CheckStatRange("talentPotential", g.talentPotentialMin, g.talentPotentialMax);
            if (!(g.talentPotentialMin >= potential.talentThreshold))
                throw Fail("generation.talentPotentialMin " + g.talentPotentialMin + " is below potential.talentThreshold " + potential.talentThreshold + ".");
            if (!(g.talentChance >= 0f && g.talentChance <= 1f))
                throw Fail("generation.talentChance must be in [0,1], got " + g.talentChance + ".");
            CheckRange("contractYears", g.contractYearsMin, g.contractYearsMax);
            if (g.contractYearsMin < 0) throw Fail("generation.contractYears min must be >= 0.");
            if (g.playerIds == null || g.playerIds.Length == 0)
                throw Fail("generation.playerIds is empty.");
            if (new HashSet<string>(g.playerIds).Count != g.playerIds.Length)
                throw Fail("generation.playerIds contains duplicates.");
        }

        private static void CheckRange(string name, float min, float max)
        {
            if (!(min <= max)) throw Fail("generation." + name + " min " + min + " > max " + max + ".");
        }

        private static void CheckStatRange(string name, float min, float max)
        {
            CheckRange(name, min, max);
            if (!(min >= 0f && max <= 100f))
                throw Fail("generation." + name + " must stay within [0,100], got " + min + ".." + max + ".");
        }

        private static void CheckFractionRange(string name, float min, float max)
        {
            CheckRange(name, min, max);
            if (!(min >= 0f && max <= 1f))
                throw Fail("generation." + name + " must stay within [0,1], got " + min + ".." + max + ".");
        }

        // Every float field, so NaN / Infinity from code-built configs is caught with the field's name.
        private void ValidateFinite()
        {
            Finite("offLaneDiscount", offLaneDiscount);
            if (lanes != null)
                foreach (var l in lanes)
                    if (l?.dimensions != null)
                        foreach (var d in l.dimensions)
                            if (d != null) Finite("lane '" + l.id + "' dimension '" + d.id + "' weight", d.weight);
            if (traitEffects != null)
                for (int i = 0; i < traitEffects.Length; i++)
                    if (traitEffects[i] != null) Finite("traitEffects[" + i + "].multiplier", traitEffects[i].multiplier);
            if (traitPairs != null)
                for (int i = 0; i < traitPairs.Length; i++)
                    if (traitPairs[i] != null) Finite("traitPairs[" + i + "].value", traitPairs[i].value);
            if (training != null)
            {
                Finite("training.baseGain", training.baseGain);
                Finite("training.capMargin", training.capMargin);
                Finite("training.span", training.span);
                Finite("training.focusBonus", training.focusBonus);
                Finite("training.otherShare", training.otherShare);
                Finite("training.moodFactorMin", training.moodFactorMin);
                Finite("training.moodFactorMax", training.moodFactorMax);
                Finite("training.stressThreshold", training.stressThreshold);
                Finite("training.stressSlope", training.stressSlope);
                Finite("training.specializationRate", training.specializationRate);
                Finite("training.specializationGain", training.specializationGain);
                Finite("training.noise", training.noise);
                Finite("training.nearCapMargin", training.nearCapMargin);
                Finite("training.initialFocusHigh", training.initialFocusHigh);
                Finite("training.initialFocusLow", training.initialFocusLow);
                if (training.ageBands != null)
                    for (int i = 0; i < training.ageBands.Length; i++)
                        if (training.ageBands[i] != null) Finite("training.ageBands[" + i + "].coefficient", training.ageBands[i].coefficient);
            }
            if (aging?.declineBands != null)
                for (int i = 0; i < aging.declineBands.Length; i++)
                    if (aging.declineBands[i] != null) Finite("aging.declineBands[" + i + "].amount", aging.declineBands[i].amount);
            if (specialty != null)
            {
                Finite("specialty.unlockValue", specialty.unlockValue);
                Finite("specialty.loseValue", specialty.loseValue);
                Finite("specialty.unlockFocus", specialty.unlockFocus);
                Finite("specialty.loseFocus", specialty.loseFocus);
            }
            if (potential != null)
            {
                Finite("potential.talentThreshold", potential.talentThreshold);
                Finite("potential.scoutingBias", potential.scoutingBias);
                Finite("potential.estimateHalfWidth", potential.estimateHalfWidth);
                Finite("potential.estimateMinHalfWidth", potential.estimateMinHalfWidth);
                Finite("potential.observationPerSegment", potential.observationPerSegment);
                Finite("potential.observationPerTraining", potential.observationPerTraining);
            }
            if (salary != null)
            {
                Finite("salary.weightLaneScore", salary.weightLaneScore);
                Finite("salary.weightPopularity", salary.weightPopularity);
                Finite("salary.weightPotential", salary.weightPotential);
                Finite("salary.randomRange", salary.randomRange);
            }
            var g = generation;
            if (g != null)
            {
                Finite("generation.mainLaneDimFractionMin", g.mainLaneDimFractionMin);
                Finite("generation.mainLaneDimFractionMax", g.mainLaneDimFractionMax);
                Finite("generation.offLaneDimFractionMin", g.offLaneDimFractionMin);
                Finite("generation.offLaneDimFractionMax", g.offLaneDimFractionMax);
                Finite("generation.looksMin", g.looksMin); Finite("generation.looksMax", g.looksMax);
                Finite("generation.popularityMin", g.popularityMin); Finite("generation.popularityMax", g.popularityMax);
                Finite("generation.moodMin", g.moodMin); Finite("generation.moodMax", g.moodMax);
                Finite("generation.stressMin", g.stressMin); Finite("generation.stressMax", g.stressMax);
                Finite("generation.normalPotentialMin", g.normalPotentialMin); Finite("generation.normalPotentialMax", g.normalPotentialMax);
                Finite("generation.talentPotentialMin", g.talentPotentialMin); Finite("generation.talentPotentialMax", g.talentPotentialMax);
                Finite("generation.talentChance", g.talentChance);
            }
        }

        private static void Finite(string name, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw Fail(name + " must be a finite number, got " + value + ".");
        }

        private static InvalidOperationException Fail(string message)
        {
            return new InvalidOperationException("PlayerConfig: " + message);
        }
    }
}
