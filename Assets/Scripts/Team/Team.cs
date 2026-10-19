using System;
using System.Collections.Generic;
using EsportsManager.Players;

namespace EsportsManager.Teams
{
    /// <summary>Stream and business heat of one player this segment, each 0-100 (numbers.md 4.2).</summary>
    public struct PlayerHeat
    {
        /// <summary>Stream heat 0-100.</summary>
        public float Stream;
        /// <summary>Business heat 0-100.</summary>
        public float Business;
    }

    /// <summary>
    /// Everything the segment update needs from the caller (Story 003 actions, Story 004 matches, Story 005 calendar).
    /// All money is integer yuan. Unset fields are 0 / null, except <see cref="SegmentsPerYear"/> which must be set (> 0).
    /// </summary>
    public struct SegmentInput
    {
        /// <summary>Segments in a year (numbers.md 1, owned by the calendar), must be > 0.</summary>
        public int SegmentsPerYear;
        /// <summary>0-based index of this segment inside the year, in [0, SegmentsPerYear). Decides who gets the salary remainder.</summary>
        public int SegmentIndexInYear;
        /// <summary>Results of the matches played this segment (true = win). May be null.</summary>
        public IReadOnlyList<bool> MatchResults;
        /// <summary>Season rank score 0-100 (numbers.md 4.3).</summary>
        public float SeasonRankScore;
        /// <summary>Per-player heat by Player.Id; missing = 0 / 0. May be null.</summary>
        public IReadOnlyDictionary<long, PlayerHeat> Heat;
        /// <summary>Match prize money, >= 0.</summary>
        public int PrizeMoney;
        /// <summary>Livestream income (see <see cref="Team.LivestreamIncome"/>), >= 0.</summary>
        public int LivestreamIncome;
        /// <summary>Club share of business deals, >= 0.</summary>
        public int BusinessIncome;
        /// <summary>Sponsorship paid this segment, >= 0.</summary>
        public int Sponsorship;
        /// <summary>Event income, >= 0.</summary>
        public int EventIncome;
        /// <summary>Transfer fees paid this segment, >= 0.</summary>
        public int TransferFees;
        /// <summary>Event expenses, >= 0.</summary>
        public int EventExpenses;
    }

    /// <summary>What <see cref="Team.SettleSegment"/> did.</summary>
    public struct SegmentReport
    {
        /// <summary>Total income this segment.</summary>
        public long Income;
        /// <summary>Total expenses this segment.</summary>
        public long Expenses;
        /// <summary>The salary part of the expenses (see <see cref="Team.SalaryShare"/>).</summary>
        public long SalaryShare;
        /// <summary>Income - Expenses.</summary>
        public long Net;
        /// <summary>Funds after settlement (may be negative).</summary>
        public long Funds;
        /// <summary>Recent win rate (0-100) after this segment's results were recorded.</summary>
        public float RecentWinRate;
        /// <summary>Club popularity after settlement.</summary>
        public float ClubPopularity;
    }

    /// <summary>
    /// A team: roster, 5-lane lineup, pair familiarity, popularity, recent results and funds
    /// (Story 002; design/numbers.md 4.2, 4.3, 4.8, 4.9 step 1, 4.10). Pure C#, not thread-safe.
    /// Lanes come from the injected PlayerConfig; all numbers from the injected <see cref="TeamConfig"/>.
    /// Mood/stress changes, training, matches and calendar belong to later stories.
    /// </summary>
    /// <example><code>var team = new Team(teamCfg, playerCfg, rng, 1000000);
    /// team.AddPlayer(p); team.SetStarter("mid", p);
    /// var report = team.SettleSegment(new SegmentInput { SegmentsPerYear = 12, MatchResults = new[] { true } });</code></example>
    public sealed class Team
    {
        private readonly TeamConfig _config;
        private readonly PlayerConfig _players;
        private readonly IRandomSource _rng;
        private readonly List<Player> _roster = new List<Player>();
        private readonly Dictionary<string, Player> _starters = new Dictionary<string, Player>();
        private readonly Dictionary<PairKey, float> _familiarity = new Dictionary<PairKey, float>();
        private readonly Queue<bool> _results = new Queue<bool>();
        private float _clubPopularity;

        private readonly struct PairKey : IEquatable<PairKey>
        {
            public readonly long Lo, Hi;
            public PairKey(long a, long b) { Lo = a < b ? a : b; Hi = a < b ? b : a; }
            public bool Equals(PairKey o) => Lo == o.Lo && Hi == o.Hi;
            public override bool Equals(object obj) => obj is PairKey k && Equals(k);
            public override int GetHashCode() => unchecked((int)(Lo * 397) ^ (int)(Hi ^ (Hi >> 32)));
        }

        /// <summary>Team name (display only).</summary>
        public string Name { get; }
        /// <summary>Money in integer yuan; may go negative (bankruptcy is Story 007).</summary>
        public long Funds { get; set; }
        /// <summary>Club popularity 0-100.</summary>
        public float ClubPopularity { get => _clubPopularity; set => _clubPopularity = Clamp(value); }
        /// <summary>All rostered players, in the order they were added.</summary>
        public IReadOnlyList<Player> Roster => _roster;

        /// <summary>Creates an empty team. Validates that the team config covers the player config's lanes.</summary>
        /// <param name="config">Team tuning (validated).</param>
        /// <param name="players">Player config; its lanes are the lineup slots.</param>
        /// <param name="rng">Random source for R(r) in popularity updates.</param>
        /// <param name="startingFunds">Initial funds in yuan.</param>
        /// <param name="name">Display name.</param>
        public Team(TeamConfig config, PlayerConfig players, IRandomSource rng, long startingFunds = 0, string name = "Team")
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            config.Validate();
            config.ValidateAgainst(players);
            Funds = startingFunds;
            Name = name;
            _clubPopularity = config.clubPopularity.initial;
        }

        // ---------------------------------------------------------------- roster and lineup

        /// <summary>Adds a player to the roster. Throws on null or a player (same Id) already on the roster.</summary>
        public void AddPlayer(Player player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (IsRostered(player)) throw new InvalidOperationException("Player " + player.Id + " is already on the roster.");
            _roster.Add(player);
        }

        /// <summary>
        /// Removes a player from the roster; if he is a starter his slot becomes empty.
        /// Pair familiarity is kept (and decays). Throws when he is not on the roster.
        /// </summary>
        public void RemovePlayer(Player player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            int idx = _roster.FindIndex(p => p.Id == player.Id);
            if (idx < 0) throw new InvalidOperationException("Player " + player.Id + " is not on the roster.");
            string lane = LaneOf(player);
            if (lane != null) _starters.Remove(lane);
            _roster.RemoveAt(idx);
        }

        /// <summary>True when a player with the same Id is on the roster.</summary>
        public bool IsRostered(Player player) => player != null && _roster.Exists(p => p.Id == player.Id);

        /// <summary>The starter of a lane, or null when the slot is empty. Throws for an unknown lane.</summary>
        public Player GetStarter(string laneId)
        {
            RequireLane(laneId);
            _starters.TryGetValue(laneId, out var p);
            return p;
        }

        /// <summary>The lane a player starts in, or null when he does not start.</summary>
        public string LaneOf(Player player)
        {
            if (player == null) return null;
            foreach (var kv in _starters) if (kv.Value.Id == player.Id) return kv.Key;
            return null;
        }

        /// <summary>
        /// Puts a rostered player into a lane. The previous starter of that lane (if any) goes to the bench.
        /// Throws when the lane is unknown, the player is not rostered, or he already starts in a DIFFERENT lane
        /// (use <see cref="SwapStarters"/> to move people between lanes). No-op when he already starts there.
        /// </summary>
        public void SetStarter(string laneId, Player player)
        {
            RequireLane(laneId);
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!IsRostered(player)) throw new InvalidOperationException("Player " + player.Id + " is not on the roster, cannot start.");
            string current = LaneOf(player);
            if (current == laneId) return;
            if (current != null) throw new InvalidOperationException("Player " + player.Id + " already starts in lane '" + current + "'.");
            _starters[laneId] = player;
        }

        /// <summary>Empties a lane slot (the player stays on the roster). Throws for an unknown lane; no-op when already empty.</summary>
        public void ClearStarter(string laneId)
        {
            RequireLane(laneId);
            _starters.Remove(laneId);
        }

        /// <summary>
        /// Exchanges the occupants of two lanes. If one lane is empty the other occupant simply moves there.
        /// Throws for unknown lanes or when both lanes are the same. Swapping changes nobody's familiarity.
        /// </summary>
        public void SwapStarters(string laneA, string laneB)
        {
            RequireLane(laneA); RequireLane(laneB);
            if (laneA == laneB) throw new ArgumentException("Cannot swap a lane with itself: " + laneA);
            _starters.TryGetValue(laneA, out var a);
            _starters.TryGetValue(laneB, out var b);
            if (b != null) _starters[laneA] = b; else _starters.Remove(laneA);
            if (a != null) _starters[laneB] = a; else _starters.Remove(laneB);
        }

        /// <summary>Snapshot of lane id to starter (empty slots absent), for <see cref="TeamStrength.Compute"/>.</summary>
        public IReadOnlyDictionary<string, Player> StartersByLane() => new Dictionary<string, Player>(_starters);

        /// <summary>The current starters in config lane order (empty slots skipped).</summary>
        public List<Player> Starters()
        {
            var list = new List<Player>();
            foreach (var lane in _players.lanes) if (_starters.TryGetValue(lane.id, out var p)) list.Add(p);
            return list;
        }

        private void RequireLane(string laneId)
        {
            if (_players.FindLane(laneId) == null) throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
        }

        // ---------------------------------------------------------------- derived values

        /// <summary>Mean mood of the actual starters; 0 when there are none.</summary>
        public float AverageStarterMood() => AverageOf(p => p.Mood);

        /// <summary>Mean stress of the actual starters; 0 when there are none.</summary>
        public float AverageStarterStress() => AverageOf(p => p.Stress);

        private float AverageOf(Func<Player, float> f)
        {
            var s = Starters();
            if (s.Count == 0) return 0f;
            double sum = 0.0;
            foreach (var p in s) sum += f(p);
            return (float)(sum / s.Count);
        }

        /// <summary>Team strength (numbers.md 4.9 step 1) of the current lineup; see <see cref="TeamStrength.Compute"/>.</summary>
        public float Strength() => TeamStrength.Compute(_config, _players, _starters);

        /// <summary>
        /// Pair familiarity F of two players by Player.Id, order-independent; 0 when the pair has no record.
        /// The players need not be on the roster (former teammates keep a decaying F).
        /// </summary>
        public float GetFamiliarity(long idA, long idB)
        {
            return _familiarity.TryGetValue(new PairKey(idA, idB), out var f) ? f : 0f;
        }

        /// <summary>Sets F of a pair directly (clamped to [0, F_max]); for loading saves and tests.</summary>
        public void SetFamiliarity(long idA, long idB, float value)
        {
            if (idA == idB) throw new ArgumentException("A pair needs two different players.");
            if (float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), "must not be NaN.");
            StoreF(new PairKey(idA, idB), value);
        }

        /// <summary>
        /// Pair chemistry C = clamp(F x (1 + kappa x H), 0, 100) with H = <see cref="Player.CompatibilityWith"/>
        /// (numbers.md 4.8).
        /// </summary>
        public float PairChemistry(Player a, Player b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            float f = GetFamiliarity(a.Id, b.Id);
            float c = f * (1f + _config.familiarity.compatibilityFactor * a.CompatibilityWith(b));
            return c < 0f ? 0f : (c > 100f ? 100f : c);
        }

        /// <summary>
        /// Team chemistry: mean pair chemistry over all pairs of the lineup slots (10 pairs for 5 lanes).
        /// A pair involving an empty slot counts as 0 (consistent with empty slot = effective score 0).
        /// There is NO swap penalty: a new starter simply has F = 0 with his teammates.
        /// </summary>
        public float Chemistry()
        {
            var slots = new List<Player>();
            foreach (var lane in _players.lanes) { _starters.TryGetValue(lane.id, out var p); slots.Add(p); }
            int pairs = 0; double sum = 0.0;
            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                {
                    pairs++;
                    if (slots[i] != null && slots[j] != null) sum += PairChemistry(slots[i], slots[j]);
                }
            return pairs == 0 ? 0f : (float)(sum / pairs);
        }

        /// <summary>The kept win/loss records, oldest first (true = win). At most results.window entries.</summary>
        public IReadOnlyCollection<bool> RecentResults => _results;

        /// <summary>Recent win rate as 0-100; the config's default when there are no records.</summary>
        public float RecentWinRate()
        {
            if (_results.Count == 0) return _config.results.defaultWinRate;
            int wins = 0;
            foreach (bool w in _results) if (w) wins++;
            return 100f * wins / _results.Count;
        }

        // ---------------------------------------------------------------- finance helpers

        /// <summary>
        /// Livestream income helper (numbers.md 4.10 v3.1): (1 + (streams - 1) x second_stream_factor) x (l_base + l_pop x popularity)
        /// for streams >= 1 (every stream after the player's first this segment pays second_stream_factor), 0 for no streams;
        /// rounded once to whole yuan (away from zero at .5). The caller sums players and passes the total in
        /// <see cref="SegmentInput.LivestreamIncome"/>.
        /// </summary>
        public int LivestreamIncome(Player player, int streams)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (streams < 0) throw new ArgumentOutOfRangeException(nameof(streams), "must be >= 0.");
            if (streams == 0) return 0;
            double weighted = 1.0 + (streams - 1) * (double)_config.livestream.secondStreamFactor;
            double v = weighted * (_config.livestream.baseIncome + (double)_config.livestream.incomePerPopularity * player.Popularity);
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// This segment's share of the yearly salaries of ALL rostered players (starters and bench):
        /// total = sum of Player.Salary (yuan/year); share = total / segmentsPerYear (integer division), and the first
        /// (total % segmentsPerYear) segments of the year each pay 1 yuan more, so with an unchanged roster the share over
        /// segment indexes 0..segmentsPerYear-1 sums EXACTLY to the annual total. Roster changes inside a year can drift
        /// by a few yuan (each segment is computed from the roster at that moment).
        /// </summary>
        public long SalaryShare(int segmentsPerYear, int segmentIndexInYear)
        {
            if (segmentsPerYear <= 0) throw new ArgumentOutOfRangeException(nameof(segmentsPerYear), "must be > 0.");
            if (segmentIndexInYear < 0 || segmentIndexInYear >= segmentsPerYear)
                throw new ArgumentOutOfRangeException(nameof(segmentIndexInYear), "must be in [0, segmentsPerYear).");
            long total = 0;
            foreach (var p in _roster)
            {
                if (p.Salary < 0) throw new InvalidOperationException("Player " + p.Id + " has a negative salary.");
                total += p.Salary;
            }
            long share = total / segmentsPerYear;
            if (segmentIndexInYear < total % segmentsPerYear) share++;
            return share;
        }

        // ---------------------------------------------------------------- the segment update

        /// <summary>
        /// The ONE ordered segment update. Order of operations:
        /// 1. Validate the input AND every rostered player's Salary (>= 0) - all throwing happens here, before any state changes.
        /// 2. Record this segment's match results (window-bounded); recent win rate now includes them.
        /// 3. Pair familiarity (numbers.md 4.8), using the lineup as it is now: every pair of starters gets
        ///    + f_seg x pair multiplier + f_win x wins-this-segment; every other recorded pair decays by f_decay;
        ///    clamp [0, F_max].
        /// 4. Club popularity then player popularity are BOTH computed from the pre-update popularity values, then applied.
        ///    Random draws: club first, then every rostered player in roster order, one R(r) each.
        ///    dClub = beta x (E_club - Club) x R(0.1); dPop = alpha x (E - Pop) x R(0.15); all clamped 0-100.
        /// 5. Finance: income = prize + livestream + business + sponsorship + event; expenses = salary share +
        ///    transfer fees + event expenses; Funds += income - expenses (no bankruptcy check).
        /// Mood, stress, training and matches are NOT touched here.
        /// </summary>
        public SegmentReport SettleSegment(SegmentInput input)
        {
            ValidateInput(input); // includes roster salaries: nothing below may throw
            var fc = _config.familiarity;

            // 2. results
            int wins = 0;
            if (input.MatchResults != null)
                foreach (bool w in input.MatchResults)
                {
                    if (w) wins++;
                    _results.Enqueue(w);
                    while (_results.Count > _config.results.window) _results.Dequeue();
                }
            float winRate = RecentWinRate();

            // 3. familiarity
            var starters = Starters();
            var together = new HashSet<PairKey>();
            for (int i = 0; i < starters.Count; i++)
                for (int j = i + 1; j < starters.Count; j++)
                {
                    var key = new PairKey(starters[i].Id, starters[j].Id);
                    together.Add(key);
                    float gain = fc.perSegment * starters[i].PairFamiliarityMultiplier(starters[j]) + fc.perWin * wins;
                    StoreF(key, GetFamiliarity(key.Lo, key.Hi) + gain);
                }
            foreach (var key in new List<PairKey>(_familiarity.Keys))
                if (!together.Contains(key)) StoreF(key, _familiarity[key] - fc.decay);

            // 4. popularity (all deltas from old values, applied together)
            var cp = _config.clubPopularity;
            double starterPop = 0.0;
            foreach (var p in starters) starterPop += p.Popularity;
            starterPop = starters.Count == 0 ? 0.0 : starterPop / starters.Count;
            double eClub = (cp.weightStarterPopularity * starterPop + cp.weightWinRate * winRate + cp.weightRank * input.SeasonRankScore)
                / (cp.weightStarterPopularity + cp.weightWinRate + cp.weightRank);
            float oldClub = _clubPopularity;
            float clubDelta = (float)(cp.beta * (eClub - oldClub) * Roll(cp.randomRange));

            var pc = _config.popularity;
            float wsum = pc.weightClub + pc.weightLaneScore + pc.weightLooks + pc.weightStream + pc.weightWinRate + pc.weightBusiness;
            var deltas = new float[_roster.Count];
            for (int i = 0; i < _roster.Count; i++)
            {
                var p = _roster[i];
                PlayerHeat heat = default;
                if (input.Heat != null) input.Heat.TryGetValue(p.Id, out heat);
                double e = (pc.weightClub * oldClub + pc.weightLaneScore * p.GetLaneScore(p.MainLane) + pc.weightLooks * p.Looks
                    + pc.weightStream * heat.Stream + pc.weightWinRate * winRate + pc.weightBusiness * heat.Business) / wsum;
                deltas[i] = (float)(pc.alpha * (e - p.Popularity) * Roll(pc.randomRange));
            }
            ClubPopularity = oldClub + clubDelta;
            for (int i = 0; i < _roster.Count; i++) _roster[i].Popularity += deltas[i];

            // 5. finance
            long income = (long)input.PrizeMoney + input.LivestreamIncome + input.BusinessIncome + input.Sponsorship + input.EventIncome;
            long salary = SalaryShare(input.SegmentsPerYear, input.SegmentIndexInYear);
            long expenses = salary + input.TransferFees + input.EventExpenses;
            Funds += income - expenses;

            return new SegmentReport
            {
                Income = income, Expenses = expenses, SalaryShare = salary, Net = income - expenses,
                Funds = Funds, RecentWinRate = winRate, ClubPopularity = _clubPopularity,
            };
        }

        private float Roll(float r) => _rng.NextFloat(1f - r, 1f + r);

        private void StoreF(PairKey key, float value)
        {
            float v = value < 0f ? 0f : (value > _config.familiarity.max ? _config.familiarity.max : value);
            if (v <= 0f) _familiarity.Remove(key); else _familiarity[key] = v;
        }

        private void ValidateInput(SegmentInput i)
        {
            foreach (var p in _roster)
                if (p.Salary < 0) throw new InvalidOperationException("Player " + p.Id + " has a negative salary (" + p.Salary + ").");
            if (i.SegmentsPerYear <= 0) throw new ArgumentOutOfRangeException(nameof(i.SegmentsPerYear), "must be > 0.");
            if (i.SegmentIndexInYear < 0 || i.SegmentIndexInYear >= i.SegmentsPerYear)
                throw new ArgumentOutOfRangeException(nameof(i.SegmentIndexInYear), "must be in [0, SegmentsPerYear).");
            if (!(i.SeasonRankScore >= 0f && i.SeasonRankScore <= 100f))
                throw new ArgumentOutOfRangeException(nameof(i.SeasonRankScore), "must be in [0,100].");
            if (i.PrizeMoney < 0 || i.LivestreamIncome < 0 || i.BusinessIncome < 0 || i.Sponsorship < 0 || i.EventIncome < 0
                || i.TransferFees < 0 || i.EventExpenses < 0)
                throw new ArgumentOutOfRangeException(nameof(i), "money amounts must be >= 0.");
            if (i.Heat != null)
                foreach (var kv in i.Heat)
                    if (!(kv.Value.Stream >= 0f && kv.Value.Stream <= 100f && kv.Value.Business >= 0f && kv.Value.Business <= 100f))
                        throw new ArgumentOutOfRangeException(nameof(i.Heat), "heat of player " + kv.Key + " must be in [0,100].");
        }

        private static float Clamp(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);
    }
}
