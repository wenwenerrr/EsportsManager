using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>
    /// Value snapshot of one starter as the match reads him (design/gdd/match.md "Locked" state): the radar of the lane he plays,
    /// mood, stress and the trait multipliers. Later changes to the <see cref="Player"/> never reach a snapshot.
    /// </summary>
    public sealed class StarterSnapshot
    {
        /// <summary>Lane he plays.</summary>
        public readonly string LaneId;
        /// <summary>Player.Id.</summary>
        public readonly long PlayerId;
        /// <summary>True when <see cref="LaneId"/> is his main lane (lane_f = 1).</summary>
        public readonly bool OnMainLane;
        /// <summary>The six dimension values of <see cref="LaneId"/> (a copy).</summary>
        public readonly float[] Dimensions;
        /// <summary>Mood 0-100.</summary>
        public readonly float Mood;
        /// <summary>Stress 0-100.</summary>
        public readonly float Stress;
        /// <summary>res: "stress_variance_impact" multiplier (resolute 0.95).</summary>
        public readonly float Resolute;
        /// <summary>asr: "command" multiplier (assertive 1.05).</summary>
        public readonly float Assertive;
        /// <summary>dec_i: "decider_performance" multiplier (decisive 1.03, hesitant 0.97).</summary>
        public readonly float Decider;
        /// <summary>"off_lane_discount" multiplier (clever 1.03).</summary>
        public readonly float OffLaneMultiplier;

        /// <summary>Copies the values of <paramref name="player"/> playing <paramref name="laneId"/>.</summary>
        public StarterSnapshot(Player player, string laneId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            LaneId = laneId;
            PlayerId = player.Id;
            OnMainLane = player.MainLane == laneId;
            Dimensions = player.GetRadar(laneId);
            Mood = player.Mood;
            Stress = player.Stress;
            Resolute = player.GetTraitMultiplier(PlayerEffectKeys.StressVarianceImpact);
            Assertive = player.GetTraitMultiplier(PlayerEffectKeys.Command);
            Decider = player.GetTraitMultiplier(PlayerEffectKeys.DeciderPerformance);
            OffLaneMultiplier = player.GetTraitMultiplier(PlayerEffectKeys.OffLaneDiscount);
        }
    }

    /// <summary>
    /// One side of a series as the simulator sees it (design/gdd/match.md): club name, a value snapshot of the starter of every
    /// lane, team chemistry, the tactic and the side's specialty hook table. Creating a lineup IS the lock: it copies everything,
    /// so later roster, mood or stress changes do not change it. Both the player club and opponents use their players' own mood
    /// and stress (no fixed values, no form).
    /// </summary>
    /// <example><code>var mine = MatchLineup.FromTeam(team, MatchTactic.Invade);
    /// var theirs = MatchLineup.FromTeam(otherTeam, MatchTactic.Develop);</code></example>
    public sealed class MatchLineup
    {
        private readonly Dictionary<string, StarterSnapshot> _starters = new Dictionary<string, StarterSnapshot>();
        private readonly List<SpecialtyEffect> _effects = new List<SpecialtyEffect>();

        /// <summary>Club name (display).</summary>
        public string ClubName { get; }
        /// <summary>Lane id to starter snapshot.</summary>
        public IReadOnlyDictionary<string, StarterSnapshot> Starters => _starters;
        /// <summary>Team chemistry 0-100 (numbers.md 4.8) when the lineup was locked.</summary>
        public float Chemistry { get; }
        /// <summary>Chosen tactic.</summary>
        public MatchTactic Tactic { get; }
        /// <summary>The side's hook table (already filtered by the growth system).</summary>
        public IReadOnlyList<SpecialtyEffect> Effects => _effects;

        /// <summary>
        /// Locks a lineup. A null player is skipped (the lane counts as empty and the simulator rejects the lineup).
        /// <paramref name="hooks"/> is the side's already-filtered hook table (Story 012); null = no specialties.
        /// </summary>
        public MatchLineup(string clubName, IReadOnlyDictionary<string, Player> startersByLane, float chemistry, MatchTactic tactic,
            IEnumerable<SpecialtyEffect> hooks = null)
        {
            if (startersByLane == null) throw new ArgumentNullException(nameof(startersByLane));
            ClubName = clubName ?? "";
            Chemistry = chemistry;
            Tactic = tactic;
            foreach (var kv in startersByLane)
                if (kv.Value != null) _starters[kv.Key] = new StarterSnapshot(kv.Value, kv.Key);
            if (hooks != null) foreach (var e in hooks) if (e != null) _effects.Add(e);
        }

        /// <summary>The same lineup with another tactic (snapshots shared; they are immutable).</summary>
        public MatchLineup WithTactic(MatchTactic tactic) => new MatchLineup(this, tactic);

        private MatchLineup(MatchLineup src, MatchTactic tactic)
        {
            ClubName = src.ClubName; Chemistry = src.Chemistry; Tactic = tactic;
            foreach (var kv in src._starters) _starters[kv.Key] = kv.Value;
            _effects.AddRange(src._effects);
        }

        /// <summary>True when <paramref name="playerId"/> starts in this lineup.</summary>
        public bool Starts(long playerId)
        {
            foreach (var s in _starters.Values) if (s.PlayerId == playerId) return true;
            return false;
        }

        /// <summary>The player club: current starters, current chemistry, the players' own mood and stress.</summary>
        public static MatchLineup FromTeam(Team team, MatchTactic tactic, IEnumerable<SpecialtyEffect> hooks = null)
        {
            if (team == null) throw new ArgumentNullException(nameof(team));
            return new MatchLineup(team.Name, team.StartersByLane(), team.Chemistry(), tactic, hooks);
        }
    }
}
