using System;
using EsportsManager.Players;

namespace EsportsManager.Matches
{
    /// <summary>States of one series (design/gdd/match.md "States and Transitions").</summary>
    public enum SeriesState
    {
        /// <summary>Scheduled; lineups and tactics not locked.</summary>
        Pending = 0,
        /// <summary>Both lineups passed the checks and are snapshots.</summary>
        Locked = 1,
        /// <summary>Games are being simulated.</summary>
        Playing = 2,
        /// <summary>A side has bestOf / 2 + 1 games; <see cref="MatchSeries.Result"/> is set.</summary>
        Complete = 3,
    }

    /// <summary>
    /// The series flow around the pure simulator: Pending -> Locked (<see cref="TryLock"/>, rejects illegal lineups and stays
    /// Pending) -> Playing -> Complete (<see cref="Play"/>). Locking takes the lineups as snapshots; later roster or mood
    /// changes do not affect the series.
    /// </summary>
    /// <example><code>var s = new MatchSeries(sim, 5);
    /// if (s.TryLock(MatchLineup.FromTeam(team, tactic), MatchLineup.FromOpponent(club), out var err)) s.Play(rng);</code></example>
    public sealed class MatchSeries
    {
        private readonly MatchSimulator _sim;
        private MatchLineup _a, _b;

        /// <summary>Series length.</summary>
        public int BestOf { get; }
        /// <summary>Current state.</summary>
        public SeriesState State { get; private set; } = SeriesState.Pending;
        /// <summary>The result once Complete, else null.</summary>
        public MatchResult Result { get; private set; }

        /// <summary>Creates a pending series. The length is checked when locking.</summary>
        public MatchSeries(MatchSimulator simulator, int bestOf)
        {
            _sim = simulator ?? throw new ArgumentNullException(nameof(simulator));
            BestOf = bestOf;
        }

        /// <summary>
        /// Pending -> Locked when both lineups and the length are legal; otherwise false with the reason, state stays Pending.
        /// </summary>
        public bool TryLock(MatchLineup a, MatchLineup b, out string error)
        {
            error = null;
            if (State != SeriesState.Pending) { error = "The series is not pending."; return false; }
            try { _sim.Validate(a, b, BestOf); }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                error = e.Message;
                return false;
            }
            _a = a; _b = b;
            State = SeriesState.Locked;
            return true;
        }

        /// <summary>Locked -> Playing -> Complete. Throws <see cref="InvalidOperationException"/> when not Locked.</summary>
        public MatchResult Play(IRandomSource rng)
        {
            if (State != SeriesState.Locked) throw new InvalidOperationException("Only a locked series can be played (state " + State + ").");
            State = SeriesState.Playing;
            Result = _sim.Simulate(_a, _b, rng, BestOf);
            State = SeriesState.Complete;
            return Result;
        }
    }
}
