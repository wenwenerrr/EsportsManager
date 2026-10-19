using System;

namespace EsportsManager.Matches
{
    /// <summary>
    /// A team's tactical lean for a series (numbers.md 4.9 step 3, Story 004). The counter relation between
    /// tactics is data (match_config.json "tactic.counters"), not code.
    /// </summary>
    public enum MatchTactic
    {
        /// <summary>Invade (config key "invade").</summary>
        Invade = 0,
        /// <summary>Develop (config key "develop").</summary>
        Develop = 1,
        /// <summary>Teamfight (config key "teamfight").</summary>
        Teamfight = 2,
    }

    /// <summary>Config-key mapping of <see cref="MatchTactic"/>.</summary>
    public static class MatchTactics
    {
        /// <summary>The config key of a tactic ("invade", "develop", "teamfight").</summary>
        public static string Key(MatchTactic tactic)
        {
            switch (tactic)
            {
                case MatchTactic.Invade: return "invade";
                case MatchTactic.Develop: return "develop";
                case MatchTactic.Teamfight: return "teamfight";
                default: throw new ArgumentOutOfRangeException(nameof(tactic), tactic, "unknown tactic.");
            }
        }

        /// <summary>Parses a config key; returns false for an unknown key.</summary>
        public static bool TryParse(string key, out MatchTactic tactic)
        {
            foreach (MatchTactic t in All)
                if (Key(t) == key) { tactic = t; return true; }
            tactic = MatchTactic.Invade;
            return false;
        }

        /// <summary>All tactics in enum order.</summary>
        public static readonly MatchTactic[] All = { MatchTactic.Invade, MatchTactic.Develop, MatchTactic.Teamfight };
    }
}
