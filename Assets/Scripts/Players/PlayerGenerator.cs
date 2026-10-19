using System;
using System.Collections.Generic;

namespace EsportsManager.Players
{
    /// <summary>
    /// Generates the fictional player pool from config ranges using an injected random source.
    /// Each player gets a unique sequential id, two traits from different groups, a potential (talent with
    /// probability generation.talentChance), a scouting bias U(-b,+b), dimensions as fractions of potential,
    /// a contract length, and an initial salary from the salary formula (numbers.md 4.4).
    /// The same config and same random sequence give an identical pool.
    /// </summary>
    /// <example>
    /// <code>var pool = new PlayerGenerator(config, new SeededRandomSource(1)).GeneratePool();</code>
    /// </example>
    public sealed class PlayerGenerator
    {
        private readonly PlayerConfig _config;
        private readonly IRandomSource _rng;

        /// <summary>
        /// The id the next generated player receives. Ids increase by 1 per player and are never reused by
        /// this generator, across any number of <see cref="GeneratePool"/> calls. Persist it with a save and
        /// pass it back as <c>firstId</c> to keep ids unique across sessions.
        /// </summary>
        public long NextId { get; private set; }

        /// <summary>Creates a generator (both dependencies injected).</summary>
        /// <param name="firstId">Id of the first generated player (default 1).</param>
        public PlayerGenerator(PlayerConfig config, IRandomSource rng, long firstId = 1)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            NextId = firstId;
        }

        /// <summary>Generates generation.playersPerLane players for each lane, with unique names.</summary>
        public List<Player> GeneratePool()
        {
            var g = _config.generation;
            var pool = new List<Player>();
            var usedNames = new HashSet<string>();
            foreach (var lane in _config.lanes)
                for (int n = 0; n < g.playersPerLane; n++)
                    pool.Add(Create(lane.id, usedNames));
            return pool;
        }

        private Player Create(string mainLane, HashSet<string> usedNames)
        {
            var g = _config.generation;
            string name = UniqueName(usedNames);
            int age = _rng.NextInt(g.minAge, g.maxAge + 1);

            // Two different groups, then one of the two opposed words in each.
            int groupCount = _config.personalityGroups.Length;
            int first = _rng.NextInt(0, groupCount);
            int second = _rng.NextInt(0, groupCount - 1);
            if (second >= first) second++;
            string traitA = _config.personalityGroups[first].traits[_rng.NextInt(0, 2)].id;
            string traitB = _config.personalityGroups[second].traits[_rng.NextInt(0, 2)].id;

            float looks = _rng.NextFloat(g.looksMin, g.looksMax);
            float popularity = _rng.NextFloat(g.popularityMin, g.popularityMax);
            float mood = _rng.NextFloat(g.moodMin, g.moodMax);
            float stress = _rng.NextFloat(g.stressMin, g.stressMax);

            bool talent = _rng.NextFloat(0f, 1f) < g.talentChance;
            float potential = talent
                ? _rng.NextFloat(g.talentPotentialMin, g.talentPotentialMax)
                : _rng.NextFloat(g.normalPotentialMin, g.normalPotentialMax);
            int contractYears = _rng.NextInt(g.contractYearsMin, g.contractYearsMax + 1);
            float bias = _rng.NextFloat(-_config.potential.scoutingBias, _config.potential.scoutingBias);

            // Dimensions are a fraction of potential (numbers.md 4.13): uneven radars with room to grow.
            var radar = new Dictionary<string, float[]>();
            foreach (var lane in _config.lanes)
            {
                bool main = lane.id == mainLane;
                float lo = main ? g.mainLaneDimFractionMin : g.offLaneDimFractionMin;
                float hi = main ? g.mainLaneDimFractionMax : g.offLaneDimFractionMax;
                var values = new float[lane.dimensions.Length];
                for (int i = 0; i < values.Length; i++) values[i] = potential * _rng.NextFloat(lo, hi);
                radar[lane.id] = values;
            }
            var player = new Player(_config, NextId++, name, age, mainLane, traitA, traitB, potential, contractYears,
                looks, popularity, mood, stress, 0, radar, bias);
            player.Salary = player.CalculateSalary(_rng); // numbers.md 4.4, random factor from the injected source
            return player;
        }

        // Picks an unused ID uniformly (one draw); only when every ID is taken, a random ID gets a number appended.
        private string UniqueName(HashSet<string> used)
        {
            var ids = _config.generation.playerIds;
            var free = new List<string>();
            foreach (var id in ids) if (!used.Contains(id)) free.Add(id);
            if (free.Count > 0)
            {
                string pick = free[_rng.NextInt(0, free.Count)];
                used.Add(pick);
                return pick;
            }
            string baseId = ids[_rng.NextInt(0, ids.Length)];
            int suffix = 2;
            string candidate;
            do { candidate = baseId + suffix++; } while (!used.Add(candidate));
            return candidate;
        }
    }
}
