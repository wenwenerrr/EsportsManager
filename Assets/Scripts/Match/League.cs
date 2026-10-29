using System;
using System.Collections.Generic;

namespace EsportsManager.Matches
{
    /// <summary>Two clubs that meet in one round of the schedule; the lower club index is side A.</summary>
    public readonly struct LeaguePairing
    {
        /// <summary>Club index of side A.</summary>
        public readonly int ClubA;
        /// <summary>Club index of side B.</summary>
        public readonly int ClubB;
        /// <summary>Creates a pairing.</summary>
        public LeaguePairing(int a, int b) { ClubA = a; ClubB = b; }
    }

    /// <summary>
    /// The schedule side of the league (design/gdd/league.md; numbers.md 4.18): club names by stable club id (= index) and the
    /// round-robin schedule. The clubs themselves (one model for the player and the AI) live in <c>EsportsManager.Leagues.LeagueWorld</c>.
    /// <see cref="PlayerClub"/> is the club the player runs, or <see cref="NoClub"/> during the warm-up (Story 013: the player club
    /// is no longer assumed to be index 0).
    /// </summary>
    /// <remarks>
    /// SCHEDULE: circle method over all clubs. Club 0 stays fixed, the others rotate by one position per round, in an order the
    /// generator shuffled once. Every round every club plays exactly one series (with an odd club count one club has a bye per
    /// round). Rounds 0 .. RoundsPerCycle-1 are one single round robin. Schedule slots (round, pairing index) key the series
    /// random numbers.
    /// </remarks>
    public sealed class League
    {
        private readonly string[] _names;
        private readonly int[] _rotation; // permutation of the non-fixed slots (club indexes, or ClubCount for a bye)

        /// <summary>"No player club" (warm-up).</summary>
        public const int NoClub = -1;
        /// <summary>The club the player runs, or <see cref="NoClub"/>.</summary>
        public int PlayerClub { get; set; } = NoClub;
        /// <summary>Total clubs.</summary>
        public int ClubCount => _names.Length;
        /// <summary>Rounds before the schedule repeats (every club meets every other once).</summary>
        public int RoundsPerCycle => _rotation.Length;

        /// <summary>
        /// Creates the schedule. <paramref name="rotationOrder"/> is a permutation of club indexes 1..ClubCount-1 (the order of the
        /// rotating slots); null = natural order.
        /// </summary>
        public League(IReadOnlyList<string> clubNames, IReadOnlyList<int> rotationOrder = null)
        {
            if (clubNames == null) throw new ArgumentNullException(nameof(clubNames));
            if (clubNames.Count < 2) throw new ArgumentException("A league needs at least two clubs.", nameof(clubNames));
            _names = new string[clubNames.Count];
            for (int i = 0; i < _names.Length; i++) _names[i] = clubNames[i] ?? "";
            int n = ClubCount;
            int slots = n % 2 == 0 ? n - 1 : n;
            _rotation = new int[slots];
            var seen = new HashSet<int>();
            for (int i = 0; i < slots; i++)
            {
                int club = rotationOrder != null && i < n - 1 ? rotationOrder[i] : i + 1;
                if (club < 1 || club > slots || !seen.Add(club))
                    throw new ArgumentException("rotationOrder must be a permutation of 1.." + slots + ".", nameof(rotationOrder));
                _rotation[i] = club;
            }
        }

        /// <summary>Name of a club by index.</summary>
        public string ClubName(int club)
        {
            if (club < 0 || club >= _names.Length) throw new ArgumentOutOfRangeException(nameof(club));
            return _names[club];
        }

        /// <summary>Renames a club (the player names the club he takes over).</summary>
        public void Rename(int club, string name)
        {
            if (club < 0 || club >= _names.Length) throw new ArgumentOutOfRangeException(nameof(club));
            _names[club] = name ?? "";
        }

        /// <summary>All pairings of a round (byes omitted), in schedule-slot order.</summary>
        public IReadOnlyList<LeaguePairing> PairingsOfRound(int round)
        {
            if (round < 0) throw new ArgumentOutOfRangeException(nameof(round));
            int slots = _rotation.Length;
            int size = slots + 1;
            var arr = new int[size];
            arr[0] = 0;
            for (int i = 1; i < size; i++) arr[i] = _rotation[(i - 1 + round) % slots];
            var list = new List<LeaguePairing>();
            for (int i = 0; i < size / 2; i++)
            {
                int a = arr[i], b = arr[size - 1 - i];
                if (a >= ClubCount || b >= ClubCount) continue;
                list.Add(a < b ? new LeaguePairing(a, b) : new LeaguePairing(b, a));
            }
            return list;
        }

        /// <summary>The club <paramref name="club"/> meets in a round, or -1 (bye).</summary>
        public int OpponentOfRound(int club, int round)
        {
            foreach (var p in PairingsOfRound(round))
            {
                if (p.ClubA == club) return p.ClubB;
                if (p.ClubB == club) return p.ClubA;
            }
            return -1;
        }

        /// <summary>The player club's opponent in a round (-1 when none / bye).</summary>
        public int PlayerOpponentOfRound(int round) => PlayerClub < 0 ? -1 : OpponentOfRound(PlayerClub, round);
    }
}
