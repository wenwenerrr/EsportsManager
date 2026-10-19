using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// Builds a <see cref="League"/> deterministically (numbers.md 4.14). Opponent strength: each opponent gets its own fresh
    /// player pool from the SHARED <see cref="PlayerGenerator"/> (same config, same quality as the player's pool, ids from the
    /// shared counter so they never collide with the player's or each other's). For every lane the opponent takes a random one of
    /// the best <c>opponent.starter_pick_range</c> main-lane players by lane score (the player's start takes the single best, so with
    /// the initial range 3 opponents are on average slightly weaker and spread around the player's level), then
    /// <c>roster_extra_min..max</c> bench players (best remaining by main-lane score). Starters get one pair familiarity per club,
    /// uniform in [familiarity_min, familiarity_max], so their chemistry is non-zero from the start.
    /// </summary>
    /// <remarks>
    /// DRAW ORDER of the factory's own source (seeded with <c>seed</c>): (1) the shuffle of the rotation order, Fisher-Yates from the
    /// last index down: for i = last..1, j = NextInt(0, i + 1); (2) per opponent in index order: tactic NextInt(0, 3),
    /// familiarity NextFloat(min, max), then for each lane in PlayerConfig order NextInt(0, min(range, candidates)), then the bench
    /// size NextInt(extraMin, extraMax + 1); (3) per opponent in index order: club popularity NextFloat(pop_min, pop_max).
    /// Form starts at 0 for every club. The player pool draws come from the generator's own source, one pool per opponent.
    /// </remarks>
    /// <example><code>var league = LeagueFactory.Build(matchCfg, teamCfg, playerCfg, generator, seed, "Demo");</code></example>
    public static class LeagueFactory
    {
        /// <summary>Builds the league. Throws <see cref="InvalidOperationException"/> when a pool cannot fill a lane.</summary>
        /// <param name="generator">The shared generator (its NextId advances); its player config must be <paramref name="players"/>.</param>
        public static League Build(MatchConfig match, TeamConfig team, PlayerConfig players, PlayerGenerator generator, int seed, string playerClubName)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (team == null) throw new ArgumentNullException(nameof(team));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            match.Validate();
            match.ValidateAgainst(players);
            var rng = new SeededRandomSource(seed);

            int count = match.series.opponentCount;
            int clubs = count + 1;
            int slots = clubs % 2 == 0 ? clubs - 1 : clubs;
            var order = new List<int>();
            for (int i = 1; i <= slots; i++) order.Add(i);
            for (int i = order.Count - 1; i >= 1; i--)
            {
                int j = rng.NextInt(0, i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            // The factory only shuffles real clubs; for an odd club count the bye slot (index clubs) stays last.
            if (clubs % 2 != 0)
            {
                order.Remove(clubs);
                order.Add(clubs);
            }

            var opponents = new List<OpponentClub>();
            for (int c = 0; c < count; c++) opponents.Add(BuildOpponent(match, team, players, generator, rng, c, seed));
            // (3) v3.3: club popularity, AFTER all opponents so the earlier draws (and A1 rosters) are unchanged.
            foreach (var club in opponents) club.Popularity = rng.NextFloat(match.opponent.popMin, match.opponent.popMax);
            return new League(match, playerClubName, opponents, order);
        }

        private static OpponentClub BuildOpponent(MatchConfig match, TeamConfig teamCfg, PlayerConfig players, PlayerGenerator generator,
            IRandomSource rng, int index, int seed)
        {
            var oc = match.opponent;
            var tactic = MatchTactics.All[rng.NextInt(0, MatchTactics.All.Length)];
            float familiarity = rng.NextFloat(oc.familiarityMin, oc.familiarityMax);
            var pool = generator.GeneratePool();
            var team = new Team(teamCfg, players, new SeededRandomSource(seed + 1000 + index), 0L, oc.clubNames[index]);
            var used = new HashSet<long>();
            var starters = new List<Player>();
            foreach (var lane in players.lanes)
            {
                var candidates = pool.FindAll(p => p.MainLane == lane.id);
                string laneId = lane.id;
                candidates.Sort((x, y) =>
                {
                    int c = y.GetLaneScore(laneId).CompareTo(x.GetLaneScore(laneId));
                    return c != 0 ? c : x.Id.CompareTo(y.Id);
                });
                if (candidates.Count == 0) throw new InvalidOperationException("The generated pool has no player for lane '" + lane.id + "'.");
                var pick = candidates[rng.NextInt(0, Math.Min(oc.starterPickRange, candidates.Count))];
                team.AddPlayer(pick);
                team.SetStarter(lane.id, pick);
                used.Add(pick.Id);
                starters.Add(pick);
            }
            for (int i = 0; i < starters.Count; i++)
                for (int j = i + 1; j < starters.Count; j++)
                    team.SetFamiliarity(starters[i].Id, starters[j].Id, familiarity);

            int extras = rng.NextInt(oc.rosterExtraMin, oc.rosterExtraMax + 1);
            var rest = pool.FindAll(p => !used.Contains(p.Id));
            rest.Sort((x, y) =>
            {
                int c = y.GetLaneScore(y.MainLane).CompareTo(x.GetLaneScore(x.MainLane));
                return c != 0 ? c : x.Id.CompareTo(y.Id);
            });
            for (int i = 0; i < extras && i < rest.Count; i++) team.AddPlayer(rest[i]);
            return new OpponentClub(oc.clubNames[index], team, tactic);
        }
    }
}
