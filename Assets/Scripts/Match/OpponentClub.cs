using System;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// A fictional opponent club (numbers.md 4.14): a name, a <see cref="Team"/> holding its 5-7 generated players, starters
    /// and pair familiarity, and one fixed tactic. In matches its starters are evaluated at the fixed mood / stress of the config.
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
        /// <summary>Club popularity 0-100 (numbers.md 4.14 v3.3), fixed at generation; it widens the form ceiling.</summary>
        public float Popularity { get; internal set; }
        /// <summary>
        /// Form offset (numbers.md 4.14 v3.3) added to every starter's effective rating in matches. Starts at 0;
        /// <see cref="League.AdvanceForm"/> walks it and clamps it to [-form_down, <see cref="MatchConfig"/>.opponent.FormUpper(Popularity)].
        /// Setting it directly is for saves and tests (no clamp here).
        /// </summary>
        public float Form { get; set; }

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
