namespace EsportsManager.Players
{
    /// <summary>
    /// Injectable source of randomness. Gameplay code must never call System.Random or
    /// UnityEngine.Random directly so tests can supply a deterministic implementation.
    /// Not thread-safe.
    /// </summary>
    /// <example>
    /// <code>IRandomSource rng = new SeededRandomSource(42);
    /// int roll = rng.NextInt(0, 6); // 0..5</code>
    /// </example>
    public interface IRandomSource
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive). Returns minInclusive if the range is empty.</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns a float in [minInclusive, maxInclusive] (inclusive up to float rounding).</summary>
        float NextFloat(float minInclusive, float maxInclusive);
    }
}
