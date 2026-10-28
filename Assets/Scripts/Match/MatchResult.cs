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
        /// <summary>The stress value used.</summary>
        public readonly float Stress;

        /// <summary>Creates a participant record.</summary>
        public MatchParticipant(string laneId, long playerId, float stress)
        {
            LaneId = laneId; PlayerId = playerId; Stress = stress;
        }
    }

    /// <summary>One side of a played series: who played, the tactic and its own (non-deciding) evaluation.</summary>
    public sealed class MatchSideSnapshot
    {
        /// <summary>Club name.</summary>
        public string ClubName { get; }
        /// <summary>Tactic used.</summary>
        public MatchTactic Tactic { get; }
        /// <summary>The five players in lane order of the PlayerConfig.</summary>
        public IReadOnlyList<MatchParticipant> Participants { get; }
        /// <summary>S_E / S_M / S_L, execution, sigma of a non-deciding game with the actual tactic.</summary>
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

        /// <summary>Lane id of a lane index (PhaseLayout order = participant order).</summary>
        public string LaneOf(int laneIndex) => laneIndex >= 0 && laneIndex < Participants.Count ? Participants[laneIndex].LaneId : null;

        /// <summary>Player id of a lane index, or -1.</summary>
        public long PlayerAt(int laneIndex) => laneIndex >= 0 && laneIndex < Participants.Count ? Participants[laneIndex].PlayerId : -1;
    }

    /// <summary>
    /// One game of a series with its full breakdown (design/gdd/match.md "战报数据"): the noise-free evaluation (phases, stars,
    /// f_early, fit, counter, G0, p0), both noise draws, G, p, the roll, and the tactic counterfactual.
    /// </summary>
    public sealed class GameResult
    {
        /// <summary>True when side A won this game.</summary>
        public bool WinnerIsA { get; }
        /// <summary>p of side A after the noise and the clamp (what the roll was compared with).</summary>
        public float WinProbabilityA { get; }
        /// <summary>True when this was a deciding game (both sides one win from the series; F14 applied).</summary>
        public bool WasDecider { get; }
        /// <summary>The noise-free evaluation of this game (null only for hand-built results).</summary>
        public GameEvaluation Evaluation { get; }
        /// <summary>First random value of the game (u of side A's noise).</summary>
        public double UA { get; }
        /// <summary>Second random value (u of side B's noise).</summary>
        public double UB { get; }
        /// <summary>Third random value: the roll; A wins when Roll &lt; p.</summary>
        public double Roll { get; }
        /// <summary>epsilon_A (F11).</summary>
        public double EpsilonA { get; }
        /// <summary>epsilon_B (F11).</summary>
        public double EpsilonB { get; }
        /// <summary>G = G0 + epsilon_A - epsilon_B (F12).</summary>
        public double G { get; }
        /// <summary>G0 of A's three tactics with B's tactic unchanged, indexed by (int)MatchTactic.</summary>
        public IReadOnlyList<double> TacticG0 { get; }

        /// <summary>Creates a game without a breakdown (tests, hand-built data).</summary>
        public GameResult(bool winnerIsA, float winProbabilityA, bool wasDecider)
        {
            WinnerIsA = winnerIsA; WinProbabilityA = winProbabilityA; WasDecider = wasDecider;
            TacticG0 = new double[0];
            P = winProbabilityA;
            G0NoHooksA = G0NoHooksB = 0.0;
        }

        /// <summary>Creates a game with its full breakdown.</summary>
        public GameResult(bool winnerIsA, double p, bool wasDecider, GameEvaluation evaluation, double uA, double uB, double roll,
            double epsilonA, double epsilonB, double g, IReadOnlyList<double> tacticG0, double g0NoHooksA, double g0NoHooksB)
        {
            G0NoHooksA = g0NoHooksA; G0NoHooksB = g0NoHooksB;
            WinnerIsA = winnerIsA; WinProbabilityA = (float)p; WasDecider = wasDecider; Evaluation = evaluation;
            UA = uA; UB = uB; Roll = roll; EpsilonA = epsilonA; EpsilonB = epsilonB; G = g; TacticG0 = tacticG0;
            P = p;
        }

        /// <summary>G0 with side A's hook table neutral (B unchanged): the specialty counterfactual of A.</summary>
        public double G0NoHooksA { get; }
        /// <summary>G0 with side B's hook table neutral (A unchanged).</summary>
        public double G0NoHooksB { get; }

        /// <summary>p in double precision (same value as <see cref="WinProbabilityA"/>).</summary>
        public double P { get; }

        /// <summary>p0 of side A (noise-free).</summary>
        public double P0 => Evaluation?.P0 ?? WinProbabilityA;
    }

    /// <summary>
    /// Result of one best-of-N series between side A and side B. Immutable. Everything later stages need (who played, game by
    /// game with the breakdown) is in here; build the reasons with <see cref="MatchReport.Build"/>.
    /// </summary>
    public sealed class MatchResult
    {
        /// <summary>Side A (the first lineup passed to the simulator).</summary>
        public MatchSideSnapshot SideA { get; }
        /// <summary>Side B.</summary>
        public MatchSideSnapshot SideB { get; }
        /// <summary>Games in play order.</summary>
        public IReadOnlyList<GameResult> Games { get; }
        /// <summary>Series length.</summary>
        public int BestOf { get; }
        /// <summary>Games won by A.</summary>
        public int ScoreA { get; }
        /// <summary>Games won by B.</summary>
        public int ScoreB { get; }
        /// <summary>True when A won the series.</summary>
        public bool WinnerIsA => ScoreA > ScoreB;

        /// <summary>Creates a result; the series winner is the side with more games.</summary>
        public MatchResult(MatchSideSnapshot sideA, MatchSideSnapshot sideB, IReadOnlyList<GameResult> games, int bestOf = 0)
        {
            SideA = sideA; SideB = sideB; Games = games; BestOf = bestOf;
            foreach (var g in games) { if (g.WinnerIsA) ScoreA++; else ScoreB++; }
        }
    }
}
