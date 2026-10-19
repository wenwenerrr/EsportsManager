using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// Simulates one best-of-N series (numbers.md 4.9). Pure C#, no state besides the injected configs.
    /// </summary>
    /// <remarks>
    /// FIXED RANDOM DRAW ORDER (every game draws exactly 3 floats, even when sigma is 0, so the order never shifts):
    /// per game, in play order: (1) epsilon of side A = NextFloat(-sigmaA, +sigmaA); (2) epsilon of side B = NextFloat(-sigmaB, +sigmaB);
    /// (3) the game roll u = NextFloat(0, 1); side A wins the game when u &lt; p, p = <see cref="MatchFormulas.WinProbability"/> of the
    /// two noisy powers. The series stops as soon as a side has bestOf / 2 + 1 games, so a series draws 3 x games played.
    /// The decider (both sides one game from winning, also the single game of a best-of-1) multiplies each side's power by its team
    /// decider coefficient (mean of the starters' "decider_performance": decisive 1.03, hesitant 0.97).
    /// Same configs, lineups and random sequence give an identical result.
    /// </remarks>
    /// <example><code>var sim = new MatchSimulator(matchCfg, teamCfg, playerCfg);
    /// MatchResult r = sim.Simulate(MatchLineup.FromTeam(team, MatchTactic.Invade), MatchLineup.FromOpponent(club, matchCfg), rng);</code></example>
    public sealed class MatchSimulator
    {
        private readonly MatchConfig _match;
        private readonly TeamConfig _team;
        private readonly PlayerConfig _players;

        /// <summary>Creates a simulator; validates that the match config fits the player config (command dimension).</summary>
        public MatchSimulator(MatchConfig match, TeamConfig team, PlayerConfig players)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            match.Validate();
            match.ValidateAgainst(players);
        }

        /// <summary>
        /// Plays the series. Throws <see cref="InvalidOperationException"/> BEFORE drawing anything when a lineup lacks a starter
        /// in some lane or the same player starts for both sides.
        /// </summary>
        public MatchResult Simulate(MatchLineup a, MatchLineup b, IRandomSource rng) => Simulate(a, b, rng, _match.format.regularBestOf);

        /// <summary>
        /// Plays a best-of-<paramref name="bestOf"/> series (numbers.md 4.18: BO3 regular season, BO5 / BO7 playoffs).
        /// Throws <see cref="ArgumentOutOfRangeException"/> for an even or non-positive length, before any draw.
        /// </summary>
        public MatchResult Simulate(MatchLineup a, MatchLineup b, IRandomSource rng, int bestOf)
        {
            if (bestOf < 1 || bestOf % 2 == 0) throw new ArgumentOutOfRangeException(nameof(bestOf), "must be odd and >= 1, got " + bestOf + ".");
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            MatchFormulas.RequireFullLineup(_players, a);
            MatchFormulas.RequireFullLineup(_players, b);
            foreach (var lane in _players.lanes)
                foreach (var other in _players.lanes)
                    if (a.StartersByLane[lane.id].Id == b.StartersByLane[other.id].Id)
                        throw new InvalidOperationException("Player " + a.StartersByLane[lane.id].Id + " starts for both sides.");

            var evalA = MatchFormulas.Evaluate(_match, _team, _players, a, b.Tactic);
            var evalB = MatchFormulas.Evaluate(_match, _team, _players, b, a.Tactic);
            int need = MatchConfig.GamesToWinOf(bestOf);
            int winsA = 0, winsB = 0;
            var games = new List<GameResult>();
            while (winsA < need && winsB < need)
            {
                bool decider = winsA == need - 1 && winsB == need - 1;
                float epsA = rng.NextFloat(-evalA.Sigma, evalA.Sigma);
                float epsB = rng.NextFloat(-evalB.Sigma, evalB.Sigma);
                float roll = rng.NextFloat(0f, 1f);
                float pa = MatchFormulas.GamePower(evalA.Power, epsA, evalA.DeciderFactor, decider);
                float pb = MatchFormulas.GamePower(evalB.Power, epsB, evalB.DeciderFactor, decider);
                float p = MatchFormulas.WinProbability(_match, pa, pb);
                bool aWins = roll < p;
                if (aWins) winsA++; else winsB++;
                games.Add(new GameResult(aWins, p, decider));
            }
            return new MatchResult(Snapshot(a, evalA), Snapshot(b, evalB), games);
        }

        private MatchSideSnapshot Snapshot(MatchLineup lineup, SideEvaluation eval)
        {
            var list = new List<MatchParticipant>();
            foreach (var lane in _players.lanes)
            {
                var p = lineup.StartersByLane[lane.id];
                float stress = lineup.UsesFixedState ? lineup.FixedStress : p.Stress;
                list.Add(new MatchParticipant(lane.id, p.Id, MatchFormulas.EffectiveRating(_team, lineup, p, lane.id), stress));
            }
            return new MatchSideSnapshot(lineup.ClubName, lineup.Tactic, list, eval);
        }
    }
}
