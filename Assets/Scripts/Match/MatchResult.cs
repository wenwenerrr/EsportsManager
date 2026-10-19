using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>One player of a side as he actually played the series (snapshot, later stages never re-read the roster).</summary>
    public readonly struct MatchParticipant
    {
        /// <summary>Lane he played in.</summary>
        public readonly string LaneId;
        /// <summary>His <c>Player.Id</c>.</summary>
        public readonly long PlayerId;
        /// <summary>Effective rating in that lane (4.9 step 1) with the mood / stress used for the series.</summary>
        public readonly float EffectiveRating;
        /// <summary>The stress value used (the fixed value for opponents).</summary>
        public readonly float Stress;

        /// <summary>Creates a participant record.</summary>
        public MatchParticipant(string laneId, long playerId, float effectiveRating, float stress)
        {
            LaneId = laneId; PlayerId = playerId; EffectiveRating = effectiveRating; Stress = stress;
        }
    }

    /// <summary>One side of a played series: who played, the tactic and the pre-game evaluation.</summary>
    public sealed class MatchSideSnapshot
    {
        /// <summary>Club name.</summary>
        public string ClubName { get; }
        /// <summary>Tactic used.</summary>
        public MatchTactic Tactic { get; }
        /// <summary>The five players in lane order of the PlayerConfig.</summary>
        public IReadOnlyList<MatchParticipant> Participants { get; }
        /// <summary>Skill, chemistry, style, tactic bonus, power, sigma.</summary>
        public SideEvaluation Evaluation { get; }

        /// <summary>Creates a snapshot.</summary>
        public MatchSideSnapshot(string clubName, MatchTactic tactic, IReadOnlyList<MatchParticipant> participants, SideEvaluation evaluation)
        {
            ClubName = clubName; Tactic = tactic; Participants = participants; Evaluation = evaluation;
        }

        /// <summary>True when the player id played this series for this side.</summary>
        public bool Played(long playerId)
        {
            foreach (var p in Participants) if (p.PlayerId == playerId) return true;
            return false;
        }
    }

    /// <summary>One game of a series.</summary>
    public readonly struct GameResult
    {
        /// <summary>True when side A won this game.</summary>
        public readonly bool WinnerIsA;
        /// <summary>Probability that side A won, after the per-game noise and the clamp (what the roll was compared with).</summary>
        public readonly float WinProbabilityA;
        /// <summary>True when this was a deciding game (both sides one win from the series), where the decider coefficients applied.</summary>
        public readonly bool WasDecider;

        /// <summary>Creates a game result.</summary>
        public GameResult(bool winnerIsA, float winProbabilityA, bool wasDecider)
        {
            WinnerIsA = winnerIsA; WinProbabilityA = winProbabilityA; WasDecider = wasDecider;
        }
    }

    /// <summary>
    /// Result of one best-of-N series between side A and side B (Story 004). Immutable. Everything later stages need
    /// (who played, game by game) is in here; build the reasons with <see cref="MatchReport.Build"/>.
    /// </summary>
    public sealed class MatchResult
    {
        /// <summary>Side A (the first lineup passed to the simulator).</summary>
        public MatchSideSnapshot SideA { get; }
        /// <summary>Side B.</summary>
        public MatchSideSnapshot SideB { get; }
        /// <summary>Games in play order.</summary>
        public IReadOnlyList<GameResult> Games { get; }
        /// <summary>Games won by A.</summary>
        public int ScoreA { get; }
        /// <summary>Games won by B.</summary>
        public int ScoreB { get; }
        /// <summary>True when A won the series.</summary>
        public bool WinnerIsA => ScoreA > ScoreB;

        /// <summary>Creates a result; the series winner is the side with more games.</summary>
        public MatchResult(MatchSideSnapshot sideA, MatchSideSnapshot sideB, IReadOnlyList<GameResult> games)
        {
            SideA = sideA; SideB = sideB; Games = games;
            foreach (var g in games) { if (g.WinnerIsA) ScoreA++; else ScoreB++; }
        }
    }
}
