using System;
using System.Collections.Generic;

namespace EsportsManager.Players
{
    /// <summary>
    /// A fictional player. Pure data and logic. Mood, stress, looks, popularity and every radar
    /// dimension are clamped to [0, 100]. Not thread-safe.
    /// </summary>
    /// <example>
    /// <code>float score = player.GetLaneScore("mid"); // discounted if mid is not the main lane
    /// // training, focus and specialties: EsportsManager.Growth.GrowthSystem (design/gdd/growth.md)
    /// player.AdvanceYear(); // decline only, age + 1, contract - 1</code>
    /// </example>
    public sealed class Player
    {
        /// <summary>Lower clamp bound for all 0-100 stats.</summary>
        public const float StatMin = 0f;
        /// <summary>Upper clamp bound for all 0-100 stats.</summary>
        public const float StatMax = 100f;

        private readonly PlayerConfig _config;
        private readonly Dictionary<string, float[]> _radar = new Dictionary<string, float[]>();
        private readonly string[] _traits = new string[2];
        private float _looks, _popularity, _mood, _stress;
        private int _contractYearsLeft;
        private float _potential, _observation;
        private readonly float[] _focus = new float[FocusPhases];
        private readonly List<string> _specialties = new List<string>();

        /// <summary>Number of phase focus values (early, mid, late; index = MatchPhase value).</summary>
        public const int FocusPhases = 3;

        /// <summary>Stable unique id (assigned by <see cref="PlayerGenerator"/>); key for pair data and saves.</summary>
        public long Id { get; }
        /// <summary>Display name.</summary>
        public string Name { get; }
        /// <summary>Age in years.</summary>
        public int Age { get; private set; }
        /// <summary>Main lane id.</summary>
        public string MainLane { get; }
        /// <summary>The two personality trait ids (config), always from two different groups.</summary>
        public IReadOnlyList<string> Traits => _traits;
        /// <summary>
        /// TRUE potential 0-100, the soft cap of training (numbers.md 4.1, 4.15). Hidden from the player:
        /// UI must show <see cref="GetPotentialEstimate"/> instead. Changed only by <see cref="AdjustPotential"/>.
        /// </summary>
        public float Potential => _potential;
        /// <summary>Scouting error rolled at generation (numbers.md 4.15); shrinks with observation in the estimate.</summary>
        public float ScoutingBias { get; }
        /// <summary>How well the club knows this player, 0-100. Narrows the potential estimate.</summary>
        public float Observation => _observation;
        /// <summary>True when the true <see cref="Potential"/> is at or above the config's talent threshold.</summary>
        public bool IsTalent => Potential >= _config.potential.talentThreshold;
        /// <summary>Salary (integer yuan per year).</summary>
        public int Salary { get; set; }

        /// <summary>Remaining contract years, always >= 0. Setting a negative value throws.</summary>
        public int ContractYearsLeft
        {
            get => _contractYearsLeft;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "ContractYearsLeft must be >= 0.");
                _contractYearsLeft = value;
            }
        }

        /// <summary>True when no contract years are left (the player must be renewed or leaves).</summary>
        public bool IsContractExpired => _contractYearsLeft <= 0;

        /// <summary>Looks, clamped 0-100.</summary>
        public float Looks { get => _looks; set => _looks = Clamp(value); }
        /// <summary>Popularity, clamped 0-100.</summary>
        public float Popularity { get => _popularity; set => _popularity = Clamp(value); }
        /// <summary>Mood, clamped 0-100.</summary>
        public float Mood { get => _mood; set => _mood = Clamp(value); }
        /// <summary>Stress, clamped 0-100.</summary>
        public float Stress { get => _stress; set => _stress = Clamp(value); }

        /// <summary>
        /// Creates a player. Every lane in the config gets a radar, initialised to 0 unless
        /// <paramref name="radar"/> supplies values (clamped to 0-100).
        /// </summary>
        /// <param name="config">Tuning data (injected).</param>
        /// <param name="id">Stable unique id.</param>
        /// <param name="traitA">First personality trait id (must exist in the config).</param>
        /// <param name="traitB">Second trait id; must belong to a different group than <paramref name="traitA"/>.</param>
        /// <param name="potential">Potential, clamped to 0-100.</param>
        /// <param name="contractYearsLeft">Remaining contract years, must be >= 0.</param>
        /// <param name="radar">Optional lane id to dimension values (length must equal the lane's dimension count).</param>
        /// <param name="scoutingBias">Scouting error of the potential estimate (0 = unbiased); must be finite.</param>
        /// <exception cref="ArgumentException">Unknown lane, unknown trait, or both traits in the same group.</exception>
        /// <example><code>var p = new Player(cfg, 1, "Name", 20, "mid", "diligent", "gentle", 80f, 3, 50f, 10f, 50f, 50f, 30000);</code></example>
        public Player(PlayerConfig config, long id, string name, int age, string mainLane, string traitA, string traitB,
            float potential, int contractYearsLeft,
            float looks, float popularity, float mood, float stress, int salary,
            IDictionary<string, float[]> radar = null, float scoutingBias = 0f)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.FindLane(mainLane) == null) throw new ArgumentException("Unknown lane: " + mainLane, nameof(mainLane));
            int groupA = config.FindTraitGroup(traitA);
            int groupB = config.FindTraitGroup(traitB);
            if (groupA < 0) throw new ArgumentException("Unknown trait: " + traitA, nameof(traitA));
            if (groupB < 0) throw new ArgumentException("Unknown trait: " + traitB, nameof(traitB));
            if (groupA == groupB)
                throw new ArgumentException("Traits '" + traitA + "' and '" + traitB + "' are in the same group.", nameof(traitB));
            if (contractYearsLeft < 0) throw new ArgumentOutOfRangeException(nameof(contractYearsLeft), "must be >= 0.");
            if (float.IsNaN(scoutingBias) || float.IsInfinity(scoutingBias))
                throw new ArgumentOutOfRangeException(nameof(scoutingBias), "must be finite.");
            Id = id;
            Name = name;
            Age = age;
            MainLane = mainLane;
            _traits[0] = traitA;
            _traits[1] = traitB;
            _potential = Clamp(potential);
            ScoutingBias = scoutingBias;
            _contractYearsLeft = contractYearsLeft;
            Salary = salary;
            Looks = looks; Popularity = popularity; Mood = mood; Stress = stress;

            foreach (var lane in config.lanes)
            {
                var values = new float[lane.dimensions.Length];
                if (radar != null && radar.TryGetValue(lane.id, out var src))
                {
                    if (src.Length != values.Length)
                        throw new ArgumentException("Radar for lane '" + lane.id + "' needs " + values.Length + " values.", nameof(radar));
                    for (int i = 0; i < values.Length; i++) values[i] = Clamp(src[i]);
                }
                _radar[lane.id] = values;
            }
        }

        /// <summary>Returns a copy of the six dimension values for a lane.</summary>
        public float[] GetRadar(string laneId)
        {
            return (float[])RadarFor(laneId).Clone();
        }

        /// <summary>Gets one dimension value of a lane.</summary>
        public float GetDimension(string laneId, int index) => RadarFor(laneId)[index];

        /// <summary>Sets one dimension value of a lane (clamped 0-100).</summary>
        public void SetDimension(string laneId, int index, float value) => RadarFor(laneId)[index] = Clamp(value);

        /// <summary>
        /// Weighted average (config weights) of the lane's dimensions, range 0-100. Multiplied by
        /// the config's off-lane discount when <paramref name="laneId"/> is not the main lane; the
        /// discount is itself multiplied by the traits' "off_lane_discount" effect (clever x1.03: 0.8 to about 0.82)
        /// and capped at 1.
        /// </summary>
        public float GetLaneScore(string laneId)
        {
            var lane = _config.FindLane(laneId) ?? throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
            var values = _radar[laneId];
            float sum = 0f, weights = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                float w = lane.dimensions[i].weight;
                sum += values[i] * w;
                weights += w;
            }
            float score = sum / weights;
            if (laneId == MainLane) return score;
            float discount = _config.offLaneDiscount * GetTraitMultiplier(PlayerEffectKeys.OffLaneDiscount);
            return score * (discount > 1f ? 1f : discount);
        }

        /// <summary>
        /// Product of both traits' multipliers for an effect key (numbers.md 3.2); 1 for keys the
        /// effect table does not list.
        /// </summary>
        /// <example><code>float m = player.GetTraitMultiplier("training_gain"); // diligent x clever = 1.05 * 1.03 (each trait within +-5%, numbers.md 3.2 v2)</code></example>
        public float GetTraitMultiplier(string effectKey)
        {
            return _config.GetEffectMultiplier(_traits[0], effectKey) * _config.GetEffectMultiplier(_traits[1], effectKey);
        }

        /// <summary>
        /// Personality compatibility H(i,j) with another player (numbers.md 3.3 v2): the sum of the table
        /// value h of all 4 trait combinations (this player's 2 x the other's 2), clamped to [-1, +1].
        /// Unlisted combinations count 0. When either player has a "negative_compat_impact" effect
        /// (slick), every negative h is multiplied by it before summing. Symmetric.
        /// </summary>
        /// <example><code>float h = a.CompatibilityWith(b); // -1..1</code></example>
        public float CompatibilityWith(Player other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            // Traits never change, so the pair value is memoised per partner (hot path: mood every segment for every club).
            if (_compat != null && _compat.TryGetValue(other, out float cached)) return cached;
            float v = ComputeCompatibility(other);
            (_compat ?? (_compat = new Dictionary<Player, float>())).Add(other, v);
            return v;
        }

        private Dictionary<Player, float> _compat;

        private float ComputeCompatibility(Player other)
        {
            float negativeFactor = PairEffect(other, PlayerEffectKeys.NegativeCompatImpact);
            float sum = 0f;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    float h = _config.GetPairCompatibility(_traits[i], other._traits[j]);
                    sum += h < 0f ? h * negativeFactor : h;
                }
            return sum < -1f ? -1f : (sum > 1f ? 1f : sum);
        }

        /// <summary>
        /// Familiarity-growth multiplier for this pair (numbers.md 3.3 / 4.8, used by Story 002): the
        /// "familiarity_gain" effect (gentle) when either player has it, otherwise 1. Symmetric.
        /// </summary>
        /// <example><code>f += fSeg * a.PairFamiliarityMultiplier(b);</code></example>
        public float PairFamiliarityMultiplier(Player other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            return PairEffect(other, PlayerEffectKeys.FamiliarityGain);
        }

        // "If either player has it" rule for pair effects: the multiplier of the player whose value is
        // further from 1, applied once (both players having the trait does not stack).
        private float PairEffect(Player other, string key)
        {
            float mine = GetTraitMultiplier(key);
            float theirs = other.GetTraitMultiplier(key);
            return Math.Abs(theirs - 1f) > Math.Abs(mine - 1f) ? theirs : mine;
        }

        /// <summary>
        /// Player-facing potential estimate (numbers.md 4.15): center = potential + bias x (1 - obs/100),
        /// half-width u = max(u_min, u0 x (1 - obs/100)); returns [center - u, center + u] rounded to whole
        /// numbers and clamped to 0-100. Deterministic.
        /// </summary>
        /// <example><code>var est = player.GetPotentialEstimate(); label.text = est.Low + "-" + est.High;</code></example>
        public PotentialEstimate GetPotentialEstimate()
        {
            var p = _config.potential;
            float unknown = 1f - _observation / 100f;
            float center = _potential + ScoutingBias * unknown;
            float u = Math.Max(p.estimateMinHalfWidth, p.estimateHalfWidth * unknown);
            return new PotentialEstimate(ClampInt((int)Math.Round(center - u)), ClampInt((int)Math.Round(center + u)));
        }

        /// <summary>Adds observation (clamped to 100). <paramref name="amount"/> must be >= 0.</summary>
        /// <example><code>player.AddObservation(config.potential.observationPerTraining);</code></example>
        public void AddObservation(float amount)
        {
            if (!(amount >= 0f) || float.IsInfinity(amount)) throw new ArgumentOutOfRangeException(nameof(amount), "must be finite and >= 0.");
            _observation = Clamp(_observation + amount);
        }

        /// <summary>Resets observation to 0 (the player moved to another club).</summary>
        public void ResetObservation() => _observation = 0f;

        /// <summary>Changes the true potential by <paramref name="delta"/> (breakthrough / slump events, Story 006), clamped to 0-100.</summary>
        /// <example><code>player.AdjustPotential(+8f);</code></example>
        public void AdjustPotential(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta), "must be finite.");
            _potential = Clamp(_potential + delta);
        }

        /// <summary>
        /// True once the phase focus was set (by the growth system: talent.md initial focus for a new player, or a backfill for
        /// a player created without it). A player without it trains as if every focus were 0 until the growth system fills it.
        /// </summary>
        public bool HasFocus { get; private set; }

        /// <summary>Focus of a phase (0 early, 1 mid, 2 late), 0-1 (growth.md "专精").</summary>
        public float GetFocus(int phase) => _focus[phase];

        /// <summary>Sets the focus of a phase (clamped 0-1) and marks the focus as set. Written by the growth system only.</summary>
        public void SetFocus(int phase, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "must be finite.");
            _focus[phase] = value < 0f ? 0f : (value > 1f ? 1f : value);
            HasFocus = true;
        }

        /// <summary>Ids of the specialties he owns, in the order they were gained. They move with him on a transfer.</summary>
        public IReadOnlyList<string> Specialties => _specialties;

        /// <summary>True when he owns the specialty.</summary>
        public bool HasSpecialty(string id) => _specialties.Contains(id);

        /// <summary>Adds a specialty (no-op when owned). Called by the growth system's F4 check.</summary>
        public void AddSpecialty(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Specialty id is empty.", nameof(id));
            if (!_specialties.Contains(id)) _specialties.Add(id);
        }

        /// <summary>Removes a specialty; false when he did not own it.</summary>
        public bool RemoveSpecialty(string id) => _specialties.Remove(id);

        /// <summary>
        /// Salary for signing or renewing (numbers.md 4.4):
        /// <c>S_min + (S_max - S_min) x WS(main lane score, popularity, potential)/100 x ask-price multiplier x R(r)</c>,
        /// R(r) drawn from <paramref name="rng"/> in [1-r, 1+r], using the TRUE potential. No salary cap
        /// (numbers.md 4.4 v2): the result may exceed S_max; it is rounded to whole yuan and never below 0.
        /// Does not change <see cref="Salary"/>.
        /// </summary>
        /// <example><code>player.Salary = player.CalculateSalary(rng);</code></example>
        public int CalculateSalary(IRandomSource rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            float r = _config.salary.randomRange;
            return SalaryWithFactor(rng.NextFloat(1f - r, 1f + r));
        }

        /// <summary>The same salary formula with R fixed at 1 (the "expected salary", no randomness).</summary>
        public int CalculateSalaryWithoutRandom() => SalaryWithFactor(1f);

        private int SalaryWithFactor(float randomFactor)
        {
            var s = _config.salary;
            float ws = (GetLaneScore(MainLane) * s.weightLaneScore + _popularity * s.weightPopularity + Potential * s.weightPotential)
                / (s.weightLaneScore + s.weightPopularity + s.weightPotential);
            double raw = s.minSalary + (double)(s.maxSalary - s.minSalary) * ws / 100.0
                * GetTraitMultiplier(PlayerEffectKeys.AskPrice) * randomFactor;
            if (raw < 0.0) raw = 0.0;
            if (raw > int.MaxValue) raw = int.MaxValue; // overflow guard only, not a gameplay cap
            return (int)Math.Round(raw);
        }

        /// <summary>
        /// Passes one year (numbers.md 4.1): when Age is at or above the config's decline start age,
        /// every dimension of every lane drops by the age's decline amount (floored at 0); then Age + 1
        /// and ContractYearsLeft - 1 (floored at 0). There is no automatic growth; growth comes from training.
        /// The decline band is chosen from the age before incrementing.
        /// </summary>
        public void AdvanceYear()
        {
            float decline = _config.GetDeclineAmount(Age);
            if (decline > 0f)
                foreach (var values in _radar.Values)
                    for (int i = 0; i < values.Length; i++)
                        values[i] = Clamp(values[i] - decline);
            Age++;
            if (_contractYearsLeft > 0) _contractYearsLeft--;
        }

        private float[] RadarFor(string laneId)
        {
            if (!_radar.TryGetValue(laneId, out var v)) throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
            return v;
        }

        private static int ClampInt(int v) => v < 0 ? 0 : (v > 100 ? 100 : v);

        private static float Clamp(float v) => v < StatMin ? StatMin : (v > StatMax ? StatMax : v);
    }

    /// <summary>Player-facing potential range, whole numbers within 0-100 (numbers.md 4.15).</summary>
    public readonly struct PotentialEstimate
    {
        /// <summary>Lower bound (inclusive).</summary>
        public readonly int Low;
        /// <summary>Upper bound (inclusive).</summary>
        public readonly int High;

        /// <summary>Creates an estimate.</summary>
        public PotentialEstimate(int low, int high) { Low = low; High = high; }
    }
}
