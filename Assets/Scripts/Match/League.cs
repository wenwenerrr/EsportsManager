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
    /// The league (numbers.md 4.14, v3.3): the player club (index 0) and <c>opponent_count</c> opponents (indexes 1..N),
    /// and the round-robin schedule. Per-event standings live in <see cref="EventResult"/> (numbers.md 4.18).
    /// </summary>
    /// <remarks>
    /// SCHEDULE: circle method over all clubs. Club 0 stays fixed, the others rotate by one position per round, in an order that
    /// the factory shuffled once with the seed. Every round every club plays exactly one series (with an odd club count one club
    /// has a bye per round). Rounds 0 .. RoundsPerCycle-1 are one single round robin: every club meets every other exactly once
    /// (a split's regular season, numbers.md 4.18). The schedule depends only on the club count and the stored order.
    /// </remarks>
    public sealed class League
    {
        private readonly List<OpponentClub> _opponents;
        private readonly int[] _rotation; // permutation of the non-fixed slots (club indexes, or ClubCount for a bye)
        private readonly MatchConfig _config;

        /// <summary>Player club index (always 0).</summary>
        public const int PlayerClub = 0;
        /// <summary>The player club's name.</summary>
        public string PlayerClubName { get; private set; }

        /// <summary>Renames the player club (shown in the standings).</summary>
        public void RenamePlayerClub(string name) => PlayerClubName = name ?? "";
        /// <summary>Opponent clubs; club index = list index + 1.</summary>
        public IReadOnlyList<OpponentClub> Opponents => _opponents;
        /// <summary>Total clubs: opponents + the player.</summary>
        public int ClubCount => _opponents.Count + 1;
        /// <summary>Rounds before the schedule repeats (every club meets every other once).</summary>
        public int RoundsPerCycle => _rotation.Length;

        /// <summary>
        /// Creates a league. <paramref name="rotationOrder"/> is a permutation of club indexes 1..ClubCount-1 (the order of
        /// the rotating slots); pass null for the natural order.
        /// </summary>
        public League(MatchConfig config, string playerClubName, IReadOnlyList<OpponentClub> opponents, IReadOnlyList<int> rotationOrder = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (opponents == null) throw new ArgumentNullException(nameof(opponents));
            if (opponents.Count < 1) throw new ArgumentException("A league needs at least one opponent.", nameof(opponents));
            PlayerClubName = playerClubName ?? "";
            _opponents = new List<OpponentClub>(opponents);
            int n = ClubCount;
            int slots = n % 2 == 0 ? n - 1 : n; // odd club count: add one bye slot with index n
            _rotation = new int[slots];
            var seen = new HashSet<int>();
            for (int i = 0; i < slots; i++)
            {
                int club = rotationOrder != null && i < n - 1 ? rotationOrder[i] : i + 1;
                if (club < 1 || club > (n % 2 == 0 ? n - 1 : n) || !seen.Add(club))
                    throw new ArgumentException("rotationOrder must be a permutation of 1.." + (slots) + ".", nameof(rotationOrder));
                _rotation[i] = club;
            }
        }

        /// <summary>Name of a club by index (0 = player).</summary>
        public string ClubName(int club)
        {
            if (club == PlayerClub) return PlayerClubName;
            if (club < 1 || club > _opponents.Count) throw new ArgumentOutOfRangeException(nameof(club));
            return _opponents[club - 1].Name;
        }

        /// <summary>The opponent club with the given club index (1..N).</summary>
        public OpponentClub OpponentAt(int club)
        {
            if (club < 1 || club > _opponents.Count) throw new ArgumentOutOfRangeException(nameof(club), "not an opponent index: " + club);
            return _opponents[club - 1];
        }

        /// <summary>All pairings of a round (byes omitted). Club 0's pairing is included.</summary>
        public IReadOnlyList<LeaguePairing> PairingsOfRound(int round)
        {
            if (round < 0) throw new ArgumentOutOfRangeException(nameof(round));
            int slots = _rotation.Length;
            int size = slots + 1; // arrangement size, even
            var arr = new int[size];
            arr[0] = PlayerClub;
            for (int i = 1; i < size; i++) arr[i] = _rotation[(i - 1 + round) % slots];
            var list = new List<LeaguePairing>();
            for (int i = 0; i < size / 2; i++)
            {
                int a = arr[i], b = arr[size - 1 - i];
                if (a >= ClubCount || b >= ClubCount) continue; // bye
                list.Add(a < b ? new LeaguePairing(a, b) : new LeaguePairing(b, a));
            }
            return list;
        }

        /// <summary>The club the player club meets in a round, or -1 when it has a bye (only with an odd club count).</summary>
        public int PlayerOpponentOfRound(int round)
        {
            foreach (var p in PairingsOfRound(round))
                if (p.ClubA == PlayerClub) return p.ClubB;
            return -1;
        }
    }
}
