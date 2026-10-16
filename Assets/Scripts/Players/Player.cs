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
    /// player.AdvanceYear();</code>
    /// </example>
    public sealed class Player
    {
        /// <summary>Lower clamp bound for all 0-100 stats.</summary>
        public const float StatMin = 0f;
        /// <summary>Upper clamp bound for all 0-100 stats.</summary>
        public const float StatMax = 100f;

        private readonly PlayerConfig _config;
        private readonly Dictionary<string, float[]> _radar = new Dictionary<string, float[]>();
        private float _looks, _popularity, _mood, _stress;

        /// <summary>Display name.</summary>
        public string Name { get; }
        /// <summary>Age in years.</summary>
        public int Age { get; private set; }
        /// <summary>Main lane id.</summary>
        public string MainLane { get; }
        /// <summary>Personality id (from config).</summary>
        public string Personality { get; }
        /// <summary>Salary (currency units per year).</summary>
        public int Salary { get; set; }

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
        /// <param name="radar">Optional lane id to dimension values (length must equal the lane's dimension count).</param>
        public Player(PlayerConfig config, string name, int age, string mainLane, string personality,
            float looks, float popularity, float mood, float stress, int salary,
            IDictionary<string, float[]> radar = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.FindLane(mainLane) == null) throw new ArgumentException("Unknown lane: " + mainLane, nameof(mainLane));
            Name = name;
            Age = age;
            MainLane = mainLane;
            Personality = personality;
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
        /// the config's off-lane discount when <paramref name="laneId"/> is not the main lane.
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
            return laneId == MainLane ? score : score * _config.offLaneDiscount;
        }

        /// <summary>
        /// Ages the player one year: adds the age band's delta to every dimension of every lane
        /// (clamped), then increments Age. The band is chosen from the age before incrementing.
        /// </summary>
        public void AdvanceYear()
        {
            float delta = _config.GetGrowthDelta(Age);
            foreach (var values in _radar.Values)
                for (int i = 0; i < values.Length; i++)
                    values[i] = Clamp(values[i] + delta);
            Age++;
        }

        private float[] RadarFor(string laneId)
        {
            if (!_radar.TryGetValue(laneId, out var v)) throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
            return v;
        }

        private static float Clamp(float v) => v < StatMin ? StatMin : (v > StatMax ? StatMax : v);
    }
}
