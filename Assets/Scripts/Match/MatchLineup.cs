using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// One side of a series as the simulator sees it: club name, the starter of every lane, team chemistry, tactic,
    /// and whether mood and stress are the players' own (player club) or fixed (opponent clubs, numbers.md 4.14).
    /// A lineup is a value snapshot: it copies the lane map at creation, so later roster edits do not change it.
    /// </summary>
    /// <example><code>var mine = MatchLineup.FromTeam(team, MatchTactic.Invade);
    /// var theirs = MatchLineup.FromOpponent(club, matchConfig);</code></example>
    public sealed class MatchLineup
    {
        private readonly Dictionary<string, Player> _starters;

        /// <summary>Club name (display).</summary>
        public string ClubName { get; }
        /// <summary>Lane id to starter (a copy).</summary>
        public IReadOnlyDictionary<string, Player> StartersByLane => _starters;
        /// <summary>Team chemistry 0-100 (numbers.md 4.8), computed from the club's pair familiarity when the lineup was made.</summary>
        public float Chemistry { get; }
        /// <summary>Chosen tactic.</summary>
        public MatchTactic Tactic { get; }
        /// <summary>True when every starter is evaluated at <see cref="FixedMood"/> / <see cref="FixedStress"/>.</summary>
        public bool UsesFixedState { get; }
        /// <summary>Mood used when <see cref="UsesFixedState"/>.</summary>
        public float FixedMood { get; }
        /// <summary>Stress used when <see cref="UsesFixedState"/>.</summary>
        public float FixedStress { get; }
        /// <summary>Form offset added to every starter's effective rating (opponents, numbers.md 4.14 v3.3); 0 for the player club.</summary>
        public float Form { get; }

        /// <summary>Creates a lineup from explicit values.</summary>
        public MatchLineup(string clubName, IReadOnlyDictionary<string, Player> startersByLane, float chemistry, MatchTactic tactic,
            bool usesFixedState = false, float fixedMood = 50f, float fixedStress = 50f, float form = 0f)
        {
            if (startersByLane == null) throw new ArgumentNullException(nameof(startersByLane));
            ClubName = clubName ?? "";
            _starters = new Dictionary<string, Player>(startersByLane);
            Chemistry = chemistry;
            Tactic = tactic;
            UsesFixedState = usesFixedState;
            FixedMood = fixedMood;
            FixedStress = fixedStress;
            Form = form;
        }

        /// <summary>The player club: current starters, current chemistry, the players' own mood and stress.</summary>
        public static MatchLineup FromTeam(Team team, MatchTactic tactic)
        {
            if (team == null) throw new ArgumentNullException(nameof(team));
            return new MatchLineup(team.Name, team.StartersByLane(), team.Chemistry(), tactic);
        }

        /// <summary>An opponent club: its starters and chemistry, fixed mood / stress from the config, its fixed tactic and current form.</summary>
        public static MatchLineup FromOpponent(OpponentClub club, MatchConfig config)
        {
            if (club == null) throw new ArgumentNullException(nameof(club));
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new MatchLineup(club.Name, club.Team.StartersByLane(), club.Team.Chemistry(), club.Tactic,
                true, config.opponent.fixedMood, config.opponent.fixedStress, club.Form);
        }
    }
}
