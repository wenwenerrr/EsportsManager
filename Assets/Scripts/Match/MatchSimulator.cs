using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// Simulates one best-of-N series with the v4 match model (design/gdd/match.md). Pure C#: the inputs (two locked lineups,
    /// configs, the random source) are never modified.
    /// </summary>
    /// <remarks>
    /// EVALUATION ORDER per game (match.md "求值顺序"): F1 effective values, F14 decider factor (deciding games only), F2 phase
    /// ratings (command excluded from F14), F3 phase strengths (per-phase short board), F4 mid chemistry bonus, phase_flat, F5 execution, F6 phase gaps with stars, F7/F8 early end and
    /// base (three-phase cycle, fixed denominator), F9/F10 fit and counter, F11 noise, F12/F13 G and p.
    /// FIXED RANDOM DRAW ORDER: every game draws exactly 3 values u = NextFloat(0, 1), even when sigma is 0: (1) u_A, epsilon_A =
    /// sigma_A x (2u_A - 1); (2) u_B, epsilon_B likewise; (3) the roll; side A wins when roll &lt; p (roll = p is a loss). The
    /// series stops as soon as a side has bestOf / 2 + 1 games, so a series draws 3 x games played. The deciding game is the one
    /// where both sides are one game from winning (the single game of a BO1 too). Same configs, lineups and random sequence give
    /// an identical result (same build, same machine).
    /// </remarks>
    /// <example><code>var sim = new MatchSimulator(matchCfg, teamCfg, playerCfg);
    /// MatchResult r = sim.Simulate(MatchLineup.FromTeam(team, MatchTactic.Invade), MatchLineup.FromTeam(otherTeam, MatchTactic.Develop), rng, 3);</code></example>
    public sealed class MatchSimulator
    {
        private readonly MatchConfig _match;
        private readonly PlayerConfig _players;

        /// <summary>The resolved phase mapping.</summary>
        public PhaseLayout Layout { get; }
        /// <summary>The match config.</summary>
        public MatchConfig Config => _match;

        /// <summary>Creates a simulator; validates the match config and that it fits the player config.</summary>
        /// <param name="team">Unused by the v4 model (kept so callers need not change); may be null.</param>
        public MatchSimulator(MatchConfig match, TeamConfig team, PlayerConfig players)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            match.Validate();
            Layout = new PhaseLayout(match, players);
        }

        /// <summary>A regular-season series (format.regular_best_of).</summary>
        public MatchResult Simulate(MatchLineup a, MatchLineup b, IRandomSource rng) => Simulate(a, b, rng, _match.format.regularBestOf);

        /// <summary>
        /// Throws <see cref="InvalidLineupException"/> when a lineup lacks a starter, starts a player twice, or a player starts
        /// for both sides; <see cref="ArgumentOutOfRangeException"/> when bestOf is not an odd number in 1..9. Nothing is drawn then.
        /// </summary>
        public void Validate(MatchLineup a, MatchLineup b, int bestOf)
        {
            if (!MatchConfig.IsLegalBestOf(bestOf))
                throw new ArgumentOutOfRangeException(nameof(bestOf), "must be odd and in [1," + MatchConfig.MaxBestOf + "], got " + bestOf + ".");
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            MatchFormulas.RequireFullLineup(Layout, a);
            MatchFormulas.RequireFullLineup(Layout, b);
            foreach (var s in a.Starters.Values)
                if (b.Starts(s.PlayerId)) throw new InvalidLineupException("Player " + s.PlayerId + " starts for both sides.", s.LaneId, s.PlayerId);
        }

        /// <summary>
        /// Plays a best-of-<paramref name="bestOf"/> series. Validates first (see <see cref="Validate"/>); an invalid request
        /// throws before any draw.
        /// </summary>
        public MatchResult Simulate(MatchLineup a, MatchLineup b, IRandomSource rng, int bestOf) => Simulate(a, b, rng, bestOf, true);

        /// <summary>
        /// Same as above; <paramref name="counterfactuals"/> false skips the report-only tactic / specialty counterfactuals (league
        /// series without the player club, Story 013 performance): the result and every draw are identical, only
        /// <see cref="GameResult.TacticG0"/> and the no-hook G0 values are left at the actual G0.
        /// </summary>
        public MatchResult Simulate(MatchLineup a, MatchLineup b, IRandomSource rng, int bestOf, bool counterfactuals)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            Validate(a, b, bestOf);

            var inA = MatchFormulas.BuildSide(_match, Layout, a, false);
            var inB = MatchFormulas.BuildSide(_match, Layout, b, false);
            SideInput decA = null, decB = null;
            var evalA = MatchFormulas.EvaluateSide(_match, Layout, inA, a.Tactic);
            var evalB = MatchFormulas.EvaluateSide(_match, Layout, inB, b.Tactic);

            int need = MatchConfig.GamesToWinOf(bestOf);
            int winsA = 0, winsB = 0;
            var games = new List<GameResult>();
            while (winsA < need && winsB < need)
            {
                bool decider = winsA == need - 1 && winsB == need - 1;
                SideInput ia = inA, ib = inB;
                SideEvaluation ea = evalA, eb = evalB;
                if (decider)
                {
                    decA = decA ?? MatchFormulas.BuildSide(_match, Layout, a, true);
                    decB = decB ?? MatchFormulas.BuildSide(_match, Layout, b, true);
                    ia = decA; ib = decB;
                    ea = MatchFormulas.EvaluateSide(_match, Layout, ia, a.Tactic);
                    eb = MatchFormulas.EvaluateSide(_match, Layout, ib, b.Tactic);
                }
                var game = MatchFormulas.EvaluateGame(_match, ea, eb);

                // Tactic counterfactual (match.md "战术对照"): A's three tactics, B unchanged, recomputed from step 9.
                var cf = new double[MatchTactics.All.Length];
                foreach (var t in MatchTactics.All)
                    cf[(int)t] = t == a.Tactic || !counterfactuals ? game.G0
                        : MatchFormulas.EvaluateGame(_match, MatchFormulas.EvaluateSide(_match, Layout, ia, t), eb).G0;
                // Specialty counterfactual (match.md "绝活对照"): each side's hooks set to neutral, the other side unchanged.
                double noHooksA = a.Effects.Count == 0 || !counterfactuals ? game.G0
                    : MatchFormulas.EvaluateGame(_match, MatchFormulas.EvaluateSide(_match, Layout, ia, a.Tactic, true), eb).G0;
                double noHooksB = b.Effects.Count == 0 || !counterfactuals ? game.G0
                    : MatchFormulas.EvaluateGame(_match, ea, MatchFormulas.EvaluateSide(_match, Layout, ib, b.Tactic, true)).G0;

                double uA = rng.NextFloat(0f, 1f);
                double uB = rng.NextFloat(0f, 1f);
                double roll = rng.NextFloat(0f, 1f);
                double epsA = MatchFormulas.Epsilon(ea.Sigma, uA);
                double epsB = MatchFormulas.Epsilon(eb.Sigma, uB);
                double g = game.G0 + epsA - epsB;
                double p = MatchFormulas.WinProbability(_match.winProbability, g);
                bool aWins = roll < p;
                if (aWins) winsA++; else winsB++;
                games.Add(new GameResult(aWins, p, decider, game, uA, uB, roll, epsA, epsB, g, cf, noHooksA, noHooksB));
            }
            return new MatchResult(Snapshot(a, evalA), Snapshot(b, evalB), games, bestOf);
        }

        /// <summary>
        /// Pre-match estimate of ONE side (match.md "赛前预估"): S_E / S_M / S_L, execution and the weakest lane of every phase for
        /// <paramref name="lineup"/> with its own tactic, exactly as the first non-deciding game uses them. No opponent, no
        /// win probability. Throws when the lineup is incomplete.
        /// </summary>
        public SideEvaluation Preview(MatchLineup lineup)
        {
            MatchFormulas.RequireFullLineup(Layout, lineup);
            return MatchFormulas.EvaluateSide(_match, Layout, MatchFormulas.BuildSide(_match, Layout, lineup, false), lineup.Tactic);
        }

        private MatchSideSnapshot Snapshot(MatchLineup lineup, SideEvaluation eval)
        {
            var list = new List<MatchParticipant>();
            foreach (var lane in Layout.LaneIds)
            {
                var s = lineup.Starters[lane];
                list.Add(new MatchParticipant(lane, s.PlayerId, s.Stress));
            }
            return new MatchSideSnapshot(lineup.ClubName, lineup.Tactic, list, eval);
        }
    }
}
