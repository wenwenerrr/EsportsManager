using System;
using System.Collections.Generic;

namespace EsportsManager.Players
{
    /// <summary>One scored dimension of a lane (id, display name, weight). Data from config.</summary>
    [Serializable]
    public sealed class DimensionDef
    {
        /// <summary>Stable identifier.</summary>
        public string id;
        /// <summary>Name shown to the player.</summary>
        public string displayName;
        /// <summary>Weight in the lane score; must be >= 0, and the lane's weights must sum to more than 0.</summary>
        public float weight = 1f;
    }

    /// <summary>A lane and its ordered dimensions. Data from config.</summary>
    [Serializable]
    public sealed class LaneDef
    {
        /// <summary>Stable identifier.</summary>
        public string id;
        /// <summary>Name shown to the player.</summary>
        public string displayName;
        /// <summary>Ordered dimensions of this lane's radar.</summary>
        public DimensionDef[] dimensions;
    }

    /// <summary>Yearly dimension change for an inclusive age range.</summary>
    [Serializable]
    public sealed class GrowthBand
    {
        /// <summary>Lowest age (inclusive) the band applies to.</summary>
        public int minAge;
        /// <summary>Highest age (inclusive) the band applies to.</summary>
        public int maxAge;
        /// <summary>Added to every dimension each year (negative = decline).</summary>
        public float deltaPerYear;
    }

    /// <summary>Ranges used to generate the fictional player pool.</summary>
    [Serializable]
    public sealed class GenerationConfig
    {
        /// <summary>Players generated for each lane as their main lane.</summary>
        public int playersPerLane = 5;
        public int minAge, maxAge;
        /// <summary>Dimension range for the main lane's radar.</summary>
        public float mainLaneDimMin, mainLaneDimMax;
        /// <summary>Dimension range for every non-main lane's radar.</summary>
        public float offLaneDimMin, offLaneDimMax;
        public float looksMin, looksMax;
        public float popularityMin, popularityMax;
        public float moodMin, moodMax;
        public float stressMin, stressMax;
        public int salaryMin, salaryMax;
        /// <summary>Fictional surname pool.</summary>
        public string[] surnames;
        /// <summary>Fictional given-name pool.</summary>
        public string[] givenNames;
    }

    /// <summary>
    /// Root of all player-model tuning data. Plain serializable data (loaded from JSON by
    /// <see cref="PlayerConfigLoader"/>); no gameplay number lives in code.
    /// </summary>
    [Serializable]
    public sealed class PlayerConfig
    {
        /// <summary>Multiplier (0..1 exclusive of 1) applied to the lane score off the main lane.</summary>
        public float offLaneDiscount = 0.8f;
        /// <summary>Finite set of personality ids.</summary>
        public string[] personalities;
        /// <summary>All lanes with their dimensions.</summary>
        public LaneDef[] lanes;
        /// <summary>Age-band growth curve.</summary>
        public GrowthBand[] growthBands;
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

        /// <summary>Yearly delta for an age; 0 when no band matches.</summary>
        public float GetGrowthDelta(int age)
        {
            if (growthBands == null) return 0f;
            for (int i = 0; i < growthBands.Length; i++)
                if (age >= growthBands[i].minAge && age <= growthBands[i].maxAge)
                    return growthBands[i].deltaPerYear;
            return 0f;
        }

        /// <summary>
        /// Checks the config is usable. Throws <see cref="InvalidOperationException"/> naming the problem.
        /// Rules: at least one lane; unique lane ids; each lane has dimensions with unique ids,
        /// non-negative weights and a weight sum greater than 0; offLaneDiscount in (0,1); at least one
        /// personality; at least one growth band, each with minAge &lt;= maxAge and no two overlapping
        /// (gaps between bands are allowed and mean "no change that year", see <see cref="GetGrowthDelta"/>);
        /// generation present with playersPerLane &gt; 0, every min &lt;= max, and non-empty name pools.
        /// </summary>
        /// <example><code>config.Validate(); // throws InvalidOperationException if unusable</code></example>
        public void Validate()
        {
            ValidateLanes();
            if (!(offLaneDiscount > 0f && offLaneDiscount < 1f))
                throw Fail("offLaneDiscount must be in (0,1), got " + offLaneDiscount + ".");
            if (personalities == null || personalities.Length == 0)
                throw Fail("no personalities.");
            ValidateGrowthBands();
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

        private void ValidateGrowthBands()
        {
            if (growthBands == null || growthBands.Length == 0) throw Fail("no growthBands.");
            for (int i = 0; i < growthBands.Length; i++)
            {
                var a = growthBands[i];
                if (a == null) throw Fail("growthBands[" + i + "] is null.");
                if (a.minAge > a.maxAge)
                    throw Fail("growthBands[" + i + "] minAge " + a.minAge + " > maxAge " + a.maxAge + ".");
                for (int j = 0; j < i; j++)
                {
                    var b = growthBands[j];
                    if (a.minAge <= b.maxAge && b.minAge <= a.maxAge)
                        throw Fail("growthBands[" + j + "] (" + b.minAge + "-" + b.maxAge + ") overlaps growthBands[" + i + "] (" + a.minAge + "-" + a.maxAge + ").");
                }
            }
        }

        private void ValidateGeneration()
        {
            var g = generation;
            if (g == null) throw Fail("missing generation.");
            if (g.playersPerLane <= 0) throw Fail("generation.playersPerLane must be > 0, got " + g.playersPerLane + ".");
            CheckRange("age", g.minAge, g.maxAge);
            CheckRange("mainLaneDim", g.mainLaneDimMin, g.mainLaneDimMax);
            CheckRange("offLaneDim", g.offLaneDimMin, g.offLaneDimMax);
            CheckRange("looks", g.looksMin, g.looksMax);
            CheckRange("popularity", g.popularityMin, g.popularityMax);
            CheckRange("mood", g.moodMin, g.moodMax);
            CheckRange("stress", g.stressMin, g.stressMax);
            CheckRange("salary", g.salaryMin, g.salaryMax);
            if (g.surnames == null || g.surnames.Length == 0 || g.givenNames == null || g.givenNames.Length == 0)
                throw Fail("name pools empty.");
        }

        private static void CheckRange(string name, float min, float max)
        {
            if (!(min <= max)) throw Fail("generation." + name + " min " + min + " > max " + max + ".");
        }

        private static InvalidOperationException Fail(string message)
        {
            return new InvalidOperationException("PlayerConfig: " + message);
        }
    }
}
