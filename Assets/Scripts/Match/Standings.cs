using System;
using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>One club's line of the standings.</summary>
    public readonly struct StandingsEntry
    {
        /// <summary>Club index (0 = the player club).</summary>
        public readonly int ClubIndex;
        /// <summary>Series won.</summary>
        public readonly int SeriesWins;
        /// <summary>Series lost.</summary>
        public readonly int SeriesLosses;
        /// <summary>Games won over all series ("small points").</summary>
        public readonly int GamesWon;
        /// <summary>Games lost over all series.</summary>
        public readonly int GamesLost;
        /// <summary>GamesWon - GamesLost.</summary>
        public int GameDiff => GamesWon - GamesLost;
        /// <summary>1-based rank (see <see cref="Standings"/> for the tie-break).</summary>
        public readonly int Rank;

        /// <summary>Creates an entry.</summary>
        public StandingsEntry(int clubIndex, int seriesWins, int seriesLosses, int gamesWon, int gamesLost, int rank)
        {
            ClubIndex = clubIndex; SeriesWins = seriesWins; SeriesLosses = seriesLosses; GamesWon = gamesWon; GamesLost = gamesLost; Rank = rank;
        }
    }

    /// <summary>
    /// League table: series wins / losses, game differential and rank per club, accumulated across segments
    /// (season reset is Story 005). Ranking order, strict and stable: 1) more series wins, 2) larger game differential,
    /// 3) more games won, 4) lower club index (the player club is index 0, so it wins a full tie).
    /// </summary>
    /// <example><code>standings.Record(0, 3, 2, 1); // club 0 beat club 3 by 2-1
    /// int rank = standings.RankOf(0);</code></example>
    public sealed class Standings
    {
        private readonly int[] _sw, _sl, _gw, _gl;

        /// <summary>Number of clubs.</summary>
        public int ClubCount { get; }

        /// <summary>Creates an empty table for <paramref name="clubCount"/> clubs (>= 2).</summary>
        public Standings(int clubCount)
        {
            if (clubCount < 2) throw new ArgumentOutOfRangeException(nameof(clubCount), "needs at least 2 clubs.");
            ClubCount = clubCount;
            _sw = new int[clubCount]; _sl = new int[clubCount]; _gw = new int[clubCount]; _gl = new int[clubCount];
        }

        /// <summary>
        /// Records a series. The side with more games wins it. Throws when the clubs are equal / out of range, a score is negative,
        /// or the scores are equal.
        /// </summary>
        public void Record(int clubA, int clubB, int gamesA, int gamesB)
        {
            Check(clubA); Check(clubB);
            if (clubA == clubB) throw new ArgumentException("A club cannot play itself.");
            if (gamesA < 0 || gamesB < 0) throw new ArgumentOutOfRangeException(nameof(gamesA), "game scores must be >= 0.");
            if (gamesA == gamesB) throw new ArgumentException("A series has a winner: scores must differ.");
            _gw[clubA] += gamesA; _gl[clubA] += gamesB;
            _gw[clubB] += gamesB; _gl[clubB] += gamesA;
            if (gamesA > gamesB) { _sw[clubA]++; _sl[clubB]++; } else { _sw[clubB]++; _sl[clubA]++; }
        }

        /// <summary>Records a played series; side A is club <paramref name="clubA"/>.</summary>
        public void Record(int clubA, int clubB, MatchResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            Record(clubA, clubB, result.ScoreA, result.ScoreB);
        }

        /// <summary>All entries sorted by rank (rank 1 first).</summary>
        public IReadOnlyList<StandingsEntry> Ranking()
        {
            var order = new List<int>();
            for (int i = 0; i < ClubCount; i++) order.Add(i);
            order.Sort(Compare);
            var list = new List<StandingsEntry>();
            for (int r = 0; r < order.Count; r++)
            {
                int c = order[r];
                list.Add(new StandingsEntry(c, _sw[c], _sl[c], _gw[c], _gl[c], r + 1));
            }
            return list;
        }

        /// <summary>1-based rank of a club.</summary>
        public int RankOf(int club)
        {
            Check(club);
            foreach (var e in Ranking()) if (e.ClubIndex == club) return e.Rank;
            throw new InvalidOperationException("unreachable");
        }

        /// <summary>The standings line of one club.</summary>
        public StandingsEntry GetEntry(int club)
        {
            Check(club);
            foreach (var e in Ranking()) if (e.ClubIndex == club) return e;
            throw new InvalidOperationException("unreachable");
        }

        private int Compare(int a, int b)
        {
            int c = _sw[b].CompareTo(_sw[a]);
            if (c != 0) return c;
            c = (_gw[b] - _gl[b]).CompareTo(_gw[a] - _gl[a]);
            if (c != 0) return c;
            c = _gw[b].CompareTo(_gw[a]);
            if (c != 0) return c;
            return a.CompareTo(b);
        }

        private void Check(int club)
        {
            if (club < 0 || club >= ClubCount) throw new ArgumentOutOfRangeException(nameof(club), "club index out of range: " + club);
        }
    }
}
