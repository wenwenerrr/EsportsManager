namespace EsportsManager.Players
{
    /// <summary>
    /// Deterministic SplitMix64-based <see cref="IRandomSource"/>. The same seed yields the same
    /// sequence on every platform and runtime version (unlike System.Random). Not thread-safe.
    /// </summary>
    /// <example>
    /// <code>var a = new SeededRandomSource(7);
    /// var b = new SeededRandomSource(7);
    /// // a.NextInt(0, 100) == b.NextInt(0, 100)</code>
    /// </example>
    public sealed class SeededRandomSource : IRandomSource
    {
        private ulong _state;

        /// <summary>Creates a source from a seed.</summary>
        public SeededRandomSource(int seed)
        {
            _state = (ulong)(long)seed;
        }

        private ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <inheritdoc/>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + (long)(NextUInt64() % range));
        }

        /// <inheritdoc/>
        public float NextFloat(float minInclusive, float maxInclusive)
        {
            double t = (NextUInt64() >> 11) * (1.0 / 9007199254740992.0); // [0,1)
            return (float)(minInclusive + t * (maxInclusive - minInclusive));
        }
    }
}
