using System;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// A fictional opponent club (numbers.md 4.14): a name, a <see cref="Team"/> holding its 5-7 generated players, starters
    /// and pair familiarity, and one fixed tactic (league AI tactic choice: Story 013). In matches its starters use their own mood and stress
    /// (design/gdd/match.md; the v3 fixed values and the form random walk are gone).
    /// Built by <see cref="LeagueFactory"/>.
    /// </summary>
    public sealed class OpponentClub
    {
        /// <summary>Club name (from the config list).</summary>
        public string Name { get; }
        /// <summary>Roster, starters and familiarity. Its funds stay 0 (opponent funding is Story 005).</summary>
        public Team Team { get; }
        /// <summary>The tactic this club always plays.</summary>
        public MatchTactic Tactic { get; }
        /// <summary>Club popularity 0-100 (numbers.md 4.14 v3.3), fixed at generation (not used by the match).</summary>
        public float Popularity { get; internal set; }
        /// <summary>Creates an opponent from an already built team.</summary>
        public OpponentClub(string name, Team team, MatchTactic tactic, float popularity = 0f)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Team = team ?? throw new ArgumentNullException(nameof(team));
            Tactic = tactic;
            if (!(popularity >= 0f && popularity <= 100f)) throw new ArgumentOutOfRangeException(nameof(popularity), "must be in [0,100].");
            Popularity = popularity;
        }
    }
}
