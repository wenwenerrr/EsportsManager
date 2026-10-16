using System;
using System.Collections.Generic;
using EsportsManager.Players;

namespace EsportsManager.Teams
{
    /// <summary>Outcome of one match, used as the "recent results" input of finance (matches do not exist yet).</summary>
    public enum MatchResult
    {
        /// <summary>The team won.</summary>
        Win,
        /// <summary>The team lost.</summary>
        Loss
    }

    /// <summary>Breakdown of one segment settlement.</summary>
    public readonly struct SegmentFinance
    {
        /// <summary>Sponsorship income this segment.</summary>
        public readonly double Sponsorship;
        /// <summary>Livestream income this segment.</summary>
        public readonly double Livestream;
        /// <summary>Salary expense this segment (all rostered players).</summary>
        public readonly double Expenses;
        /// <summary>Sponsorship + livestream.</summary>
        public double Income => Sponsorship + Livestream;
        /// <summary>Income - expenses (may be negative).</summary>
        public double Net => Income - Expenses;

        /// <summary>Creates a breakdown.</summary>
        public SegmentFinance(double sponsorship, double livestream, double expenses)
        {
            Sponsorship = sponsorship; Livestream = livestream; Expenses = expenses;
        }
    }

    /// <summary>
    /// A team: roster, lineup (lane id to starter), and the four triangle numbers: strength, popularity,
    /// funds, chemistry (0-100). Implements Story 002 / design/game-brief.md MVP item 2. Deterministic,
    /// pure C#, config injected. Not thread-safe.
    /// </summary>
    /// <remarks>
    /// Decisions: the lineup has one slot per lane in <see cref="TeamConfig.laneWeights"/>. An empty slot
    /// scores 0 for strength and is ignored for popularity/pairings; chemistry only rises when all slots are
    /// filled. Replacing or removing a starter costs chemistry (per change, scaled by the drop multiplier);
    /// filling an empty slot is free. Funds may go negative; bankruptcy is Story 007.
    /// </remarks>
    public sealed class Team
    {
        /// <summary>Lower bound of popularity and chemistry.</summary>
        public const float StatMin = 0f;
        /// <summary>Upper bound of popularity and chemistry.</summary>
        public const float StatMax = 100f;

        private readonly TeamConfig _config;
        private readonly List<Player> _roster = new List<Player>();
        private readonly Dictionary<string, Player> _lineup = new Dictionary<string, Player>();
        private float _ownPopularity, _chemistry;

        /// <summary>Team name.</summary>
        public string Name { get; }
        /// <summary>Money on hand; can be negative.</summary>
        public double Funds { get; private set; }
        /// <summary>Team-owned accumulated popularity, clamped 0-100.</summary>
        public float OwnPopularity { get => _ownPopularity; set => _ownPopularity = Clamp(value); }
        /// <summary>Chemistry, clamped 0-100.</summary>
        public float Chemistry { get => _chemistry; set => _chemistry = Clamp(value); }
        /// <summary>Everyone under contract (starters and substitutes).</summary>
        public IReadOnlyList<Player> Roster => _roster;
        /// <summary>True when every lane slot has a starter.</summary>
        public bool IsLineupComplete => _lineup.Count == _config.laneWeights.Length;

        /// <summary>Creates a team with initial funds, popularity and chemistry from the config.</summary>
        /// <param name="config">Validated tuning data (injected).</param>
        public Team(TeamConfig config, string name)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Name = name;
            Funds = config.initialFunds;
            OwnPopularity = config.initialOwnPopularity;
            Chemistry = config.initialChemistry;
        }

        /// <summary>Adds a free amount (positive or negative) to funds; used by later stories for action costs.</summary>
        public void AddFunds(double amount) => Funds += amount;

        /// <summary>Signs a player to the roster.</summary>
        public void AddPlayer(Player player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (_roster.Contains(player)) throw new ArgumentException("Player already on roster: " + player.Name, nameof(player));
            _roster.Add(player);
        }

        /// <summary>
        /// Releases a player. If he was a starter his slot becomes empty and the replacement chemistry
        /// penalty is applied. Returns false if he was not on the roster.
        /// </summary>
        public bool RemovePlayer(Player player)
        {
            if (player == null || !_roster.Remove(player)) return false;
            string lane = LaneOf(player);
            if (lane != null)
            {
                _lineup.Remove(lane);
                ApplyReplacementPenalty(1);
            }
            return true;
        }

        /// <summary>Starter of a lane, or null if the slot is empty.</summary>
        public Player GetStarter(string laneId)
        {
            RequireLane(laneId);
            return _lineup.TryGetValue(laneId, out var p) ? p : null;
        }

        /// <summary>
        /// Puts a rostered player into a lane slot. Replacing a different starter costs chemistry; filling an
        /// empty slot or re-setting the same starter is free. A player already starting in another lane is
        /// rejected (use <see cref="SwapStarters"/>).
        /// </summary>
        public void SetStarter(string laneId, Player player)
        {
            RequireLane(laneId);
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_roster.Contains(player)) throw new ArgumentException("Player not on roster: " + player.Name, nameof(player));
            string current = LaneOf(player);
            if (current == laneId) return;
            if (current != null) throw new ArgumentException(player.Name + " already starts in lane '" + current + "'.", nameof(player));
            bool replacing = _lineup.ContainsKey(laneId);
            _lineup[laneId] = player;
            if (replacing) ApplyReplacementPenalty(1);
        }

        /// <summary>Swaps the starters of two filled lanes; counts as two replacements.</summary>
        public void SwapStarters(string laneA, string laneB)
        {
            RequireLane(laneA); RequireLane(laneB);
            if (laneA == laneB) return;
            if (!_lineup.TryGetValue(laneA, out var pa) || !_lineup.TryGetValue(laneB, out var pb))
                throw new InvalidOperationException("Both lanes need a starter to swap.");
            _lineup[laneA] = pb;
            _lineup[laneB] = pa;
            ApplyReplacementPenalty(2);
        }

        /// <summary>
        /// Team strength, 0-100. Each lane's starter contributes his <see cref="Player.GetLaneScore"/> for that
        /// lane (empty slot = 0). weighted_average: sum(w*score)/sum(w); weighted_sum: sum(w*score), clamped.
        /// </summary>
        public float Strength
        {
            get
            {
                double sum = 0, weights = 0;
                foreach (var lw in _config.laneWeights)
                {
                    weights += lw.weight;
                    if (_lineup.TryGetValue(lw.lane, out var p)) sum += lw.weight * p.GetLaneScore(lw.lane);
                }
                double value = _config.strengthAggregation == TeamConfig.AggregationWeightedSum ? sum : sum / weights;
                return Clamp((float)value);
            }
        }

        /// <summary>
        /// Team popularity, 0-100: starterShare * average starter popularity + (1 - starterShare) * own popularity.
        /// With no starters the starter part is 0.
        /// </summary>
        public float Popularity
        {
            get
            {
                float avg = 0f;
                if (_lineup.Count > 0)
                {
                    foreach (var p in _lineup.Values) avg += p.Popularity;
                    avg /= _lineup.Count;
                }
                float share = _config.starterPopularityShare;
                return Clamp(share * avg + (1f - share) * _ownPopularity);
            }
        }

        /// <summary>
        /// Average pair modifier over all pairs of current starters (compatible positive, conflict negative,
        /// unlisted 0). 0 when fewer than two starters.
        /// </summary>
        public float AveragePairModifier
        {
            get
            {
                var list = new List<Player>(_lineup.Values);
                int pairs = 0;
                float sum = 0f;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        sum += _config.GetPairModifier(list[i].Personality, list[j].Personality);
                        pairs++;
                    }
                return pairs == 0 ? 0f : sum / pairs;
            }
        }

        /// <summary>Scales chemistry rise: max(0, 1 + AveragePairModifier).</summary>
        public float RiseMultiplier => Math.Max(0f, 1f + AveragePairModifier);
        /// <summary>Scales chemistry drops: max(0, 1 - AveragePairModifier).</summary>
        public float DropMultiplier => Math.Max(0f, 1f - AveragePairModifier);

        /// <summary>
        /// Settles one segment: updates funds and chemistry and returns the finance breakdown.
        /// Sponsorship = base + perPopularity * Popularity + perWinRate * winRate, where winRate is the share of
        /// Wins among the last recentResultsWindow results (noResultsWinRate if none). Livestream = base +
        /// perPopularity * Popularity. Expenses = sum of all rostered players' yearly Salary / segmentsPerYear.
        /// Chemistry rises by risePerSegment * RiseMultiplier when the lineup is complete.
        /// </summary>
        /// <param name="recentResults">Oldest to newest; null means none.</param>
        public SegmentFinance SettleSegment(IReadOnlyList<MatchResult> recentResults)
        {
            float pop = Popularity;
            double winRate = WinRate(recentResults);
            double sponsorship = _config.sponsorshipBase + _config.sponsorshipPerPopularity * pop + _config.sponsorshipPerWinRate * winRate;
            double livestream = _config.livestreamBase + _config.livestreamPerPopularity * pop;
            double salaries = 0;
            foreach (var p in _roster) salaries += p.Salary;
            var finance = new SegmentFinance(sponsorship, livestream, salaries / _config.segmentsPerYear);
            Funds += finance.Net;

            if (IsLineupComplete) Chemistry += _config.chemistryRisePerSegment * RiseMultiplier;
            return finance;
        }

        private double WinRate(IReadOnlyList<MatchResult> results)
        {
            if (results == null || results.Count == 0) return _config.noResultsWinRate;
            int n = Math.Min(results.Count, _config.recentResultsWindow);
            int wins = 0;
            for (int i = results.Count - n; i < results.Count; i++)
                if (results[i] == MatchResult.Win) wins++;
            return (double)wins / n;
        }

        private void ApplyReplacementPenalty(int changes)
        {
            Chemistry -= _config.chemistryDropPerReplacement * changes * DropMultiplier;
        }

        private string LaneOf(Player player)
        {
            foreach (var kv in _lineup) if (kv.Value == player) return kv.Key;
            return null;
        }

        private void RequireLane(string laneId)
        {
            if (!HasLane(laneId))
                throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
        }

        private bool HasLane(string laneId)
        {
            foreach (var lw in _config.laneWeights) if (lw.lane == laneId) return true;
            return false;
        }

        private static float Clamp(float v) => v < StatMin ? StatMin : (v > StatMax ? StatMax : v);
    }
}
