using System;
using System.Collections.Generic;

namespace EsportsManager.Players
{
    /// <summary>
    /// Generates the fictional player pool from config ranges using an injected random source.
    /// The same config and same random sequence give an identical pool.
    /// </summary>
    /// <example>
    /// <code>var pool = new PlayerGenerator(config, new SeededRandomSource(1)).GeneratePool();</code>
    /// </example>
    public sealed class PlayerGenerator
    {
        private readonly PlayerConfig _config;
        private readonly IRandomSource _rng;

        /// <summary>Creates a generator (both dependencies injected).</summary>
        public PlayerGenerator(PlayerConfig config, IRandomSource rng)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
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
            string personality = _config.personalities[_rng.NextInt(0, _config.personalities.Length)];
            float looks = _rng.NextFloat(g.looksMin, g.looksMax);
            float popularity = _rng.NextFloat(g.popularityMin, g.popularityMax);
            float mood = _rng.NextFloat(g.moodMin, g.moodMax);
            float stress = _rng.NextFloat(g.stressMin, g.stressMax);
            int salary = _rng.NextInt(g.salaryMin, g.salaryMax + 1);

            var radar = new Dictionary<string, float[]>();
            foreach (var lane in _config.lanes)
            {
                bool main = lane.id == mainLane;
                float lo = main ? g.mainLaneDimMin : g.offLaneDimMin;
                float hi = main ? g.mainLaneDimMax : g.offLaneDimMax;
                var values = new float[lane.dimensions.Length];
                for (int i = 0; i < values.Length; i++) values[i] = _rng.NextFloat(lo, hi);
                radar[lane.id] = values;
            }
            return new Player(_config, name, age, mainLane, personality, looks, popularity, mood, stress, salary, radar);
        }

        private string UniqueName(HashSet<string> used)
        {
            var g = _config.generation;
            string name = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                name = g.surnames[_rng.NextInt(0, g.surnames.Length)] + g.givenNames[_rng.NextInt(0, g.givenNames.Length)];
                if (used.Add(name)) return name;
            }
            // Fall back to a numeric suffix so names stay unique even if pools are small.
            int suffix = 2;
            string candidate;
            do { candidate = name + suffix++; } while (!used.Add(candidate));
            return candidate;
        }
    }
}
