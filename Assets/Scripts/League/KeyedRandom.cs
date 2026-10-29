using System;
using EsportsManager.Players;

namespace EsportsManager.Leagues
{
    /// <summary>
    /// Keyed random numbers (design/gdd/league.md "随机数（决定论）"): every draw is
    /// <c>u = U01(SplitMix64(H(seed, purpose, year, segment-or-step, club id, subject id, kind, k)))</c>. A draw depends only on its
    /// key, never on how many draws happened before under OTHER keys, so a player who changes his own choices never changes any AI
    /// draw (the AI may react differently, but it does not "get another set of luck"). The warm-up year is year 0.
    /// </summary>
    /// <remarks>
    /// <see cref="Stream"/> returns an <see cref="IRandomSource"/> whose n-th draw uses k = n under a fixed key prefix, so existing
    /// systems that take a sequential source (segment runner, team, event engine, simulator) can draw keyed values unchanged.
    /// Purposes are strings hashed with FNV-1a (stable across platforms). <see cref="Recorder"/> (tests) sees every draw.
    /// </remarks>
    public sealed class KeyedRandom
    {
        /// <summary>Master seed.</summary>
        public long Seed { get; }
        /// <summary>Optional observer of every draw: (key prefix hash, k, u). Tests use it for league.md AC2.</summary>
        public Action<ulong, long, double> Recorder { get; set; }

        /// <summary>Creates the root from the master seed.</summary>
        public KeyedRandom(long seed) { Seed = seed; }

        /// <summary>SplitMix64 finaliser.</summary>
        public static ulong SplitMix64(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>FNV-1a 64 of a purpose string.</summary>
        public static ulong HashString(string s)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (char c in s ?? "") { h ^= c; h *= 1099511628211UL; }
                return h;
            }
        }

        /// <summary>Folds one more key part into a hash.</summary>
        public static ulong Mix(ulong h, long part) => SplitMix64(h ^ unchecked((ulong)part * 0xD6E8FEB86659FD93UL));

        /// <summary>The key prefix H(seed, purpose, parts...).</summary>
        public ulong Key(string purpose, params long[] parts)
        {
            ulong h = Mix(HashString(purpose), Seed);
            if (parts != null) foreach (long p in parts) h = Mix(h, p);
            return h;
        }

        /// <summary>The k-th uniform [0, 1) under a key prefix (reported to <see cref="Recorder"/>).</summary>
        public double U01(ulong key, long k)
        {
            double u = (SplitMix64(Mix(key, k)) >> 11) * (1.0 / 9007199254740992.0);
            Recorder?.Invoke(key, k, u);
            return u;
        }

        /// <summary>One draw under purpose + parts with k = 0.</summary>
        public double U01(string purpose, params long[] parts) => U01(Key(purpose, parts), 0);

        /// <summary>A sequential source under a fixed key prefix (draw n uses k = n).</summary>
        public KeyedStream Stream(string purpose, params long[] parts) => new KeyedStream(this, Key(purpose, parts));

        /// <summary>A re-keyable stream: <see cref="KeyedStream.Rekey"/> sets the remaining key parts (year, segment ...).</summary>
        public KeyedStream Rekeyable(string purpose, long clubId) => new KeyedStream(this, purpose, clubId);
    }

    /// <summary>
    /// Sequential view of keyed draws: the n-th call uses k = n under the current key prefix. A re-keyable stream (purpose + club
    /// id fixed) is re-keyed per segment / event with <see cref="Rekey"/>, which also resets k.
    /// </summary>
    public sealed class KeyedStream : IRandomSource
    {
        private readonly KeyedRandom _root;
        private readonly string _purpose;
        private readonly long _club;
        private ulong _key;
        private long _k;

        internal KeyedStream(KeyedRandom root, ulong key) { _root = root; _key = key; }

        internal KeyedStream(KeyedRandom root, string purpose, long club)
        {
            _root = root; _purpose = purpose; _club = club;
            _key = root.Key(purpose, -1, -1, club);
        }

        /// <summary>Draws made under the current key.</summary>
        public long Count => _k;

        /// <summary>New key (purpose, year, step, club, extra...) and k = 0. Only for streams made by <see cref="KeyedRandom.Rekeyable"/>.</summary>
        public void Rekey(long year, long step, params long[] extra)
        {
            if (_purpose == null) throw new InvalidOperationException("This stream has a fixed key.");
            var parts = new long[3 + (extra?.Length ?? 0)];
            parts[0] = year; parts[1] = step; parts[2] = _club;
            if (extra != null) Array.Copy(extra, 0, parts, 3, extra.Length);
            _key = _root.Key(_purpose, parts);
            _k = 0;
        }

        /// <summary>Next uniform [0, 1).</summary>
        public double NextDouble() => _root.U01(_key, _k++);

        /// <inheritdoc/>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            long range = (long)maxExclusive - minInclusive;
            long v = (long)(NextDouble() * range);
            if (v >= range) v = range - 1;
            return (int)(minInclusive + v);
        }

        /// <inheritdoc/>
        public float NextFloat(float minInclusive, float maxInclusive) => (float)(minInclusive + NextDouble() * (maxInclusive - minInclusive));

        /// <summary>Standard normal by Box-Muller (two draws).</summary>
        public double NextGaussian()
        {
            double u1 = 1.0 - NextDouble(), u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
