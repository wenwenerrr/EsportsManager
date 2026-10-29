namespace EsportsManager.Leagues
{
    /// <summary>
    /// The economy / transfer state of one player that is not part of the player model (design/gdd/economy.md F2, F6;
    /// transfer.md): honor (it travels with the player), the "wants a transfer" flag, the renegotiation block, the once-per-contract
    /// retain raise, and the years used by the listing / auction rules. Kept by <see cref="LeagueWorld"/> per player id.
    /// </summary>
    public sealed class PlayerMarketState
    {
        /// <summary>Not set marker for the year fields.</summary>
        public const int Never = -99;
        /// <summary>F2 honor 0-100 (best place score of last year, at the club he played for; 0 for free agents).</summary>
        public float Honor;
        /// <summary>Best place score of the running year (becomes <see cref="Honor"/> at the year end).</summary>
        public float HonorThisYear;
        /// <summary>"想转会": set by a refused renegotiation; poach score x2, AI accepts any offer. Cleared by a move or a renewal.</summary>
        public bool WantsTransfer;
        /// <summary>Window year in which he may not ask for a renegotiation again (the year after a refusal).</summary>
        public int RenegBlockedYear = Never;
        /// <summary>True after a retain raise in the current contract (a second offer can only be accepted).</summary>
        public bool Retained;
        /// <summary>Year he joined his club (season or window year); a player signed this year cannot be listed.</summary>
        public int JoinedYear = Never;
        /// <summary>Window year of a refused renegotiation: listed automatically in that window.</summary>
        public int AutoListYear = Never;
        /// <summary>Window year in which he received a poach offer (one per year).</summary>
        public int OfferedYear = Never;
        /// <summary>Window year in which he was generated as a youth player (league lots, pool-cap exemption).</summary>
        public int YouthYear = Never;
        /// <summary>Window year in which he went unsold as a league lot (not signable that window, not counted by the pool cap).</summary>
        public int LockedYear = Never;
    }
}
