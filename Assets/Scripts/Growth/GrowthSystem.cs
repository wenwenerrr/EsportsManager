using System;
using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Players;

namespace EsportsManager.Growth
{
    /// <summary>What one training point trains (design/gdd/growth.md "训练"): a phase of the main lane, or command.</summary>
    public enum TrainingTarget
    {
        /// <summary>前期 (early phase dimensions of the main lane).</summary>
        Early = 0,
        /// <summary>团战 (mid phase).</summary>
        Mid = 1,
        /// <summary>后期 (late phase).</summary>
        Late = 2,
        /// <summary>指挥 (the command dimension; trains no phase, decays every focus).</summary>
        Command = 3,
    }

    /// <summary>
    /// The match phase mapping (design/gdd/match.md "阶段与维度", owned by match_config) as the growth system reads it: for every
    /// lane the main-lane dimension indexes of each phase and the command index. Built from the match <see cref="PhaseLayout"/>
    /// in the game; tests may build it directly.
    /// </summary>
    public sealed class GrowthPhaseMap
    {
        private readonly Dictionary<string, int[][]> _dims = new Dictionary<string, int[][]>();
        private readonly Dictionary<string, int> _command = new Dictionary<string, int>();

        /// <summary>From the match layout (lanes in PlayerConfig order).</summary>
        public GrowthPhaseMap(PhaseLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            for (int i = 0; i < layout.LaneCount; i++)
            {
                _dims[layout.LaneIds[i]] = layout.DimIndexes[i];
                _command[layout.LaneIds[i]] = layout.CommandIndex[i];
            }
        }

        /// <summary>Direct construction: lane id -> [phase][] dimension indexes, lane id -> command index.</summary>
        public GrowthPhaseMap(IDictionary<string, int[][]> phaseDims, IDictionary<string, int> commandIndex)
        {
            if (phaseDims == null) throw new ArgumentNullException(nameof(phaseDims));
            if (commandIndex == null) throw new ArgumentNullException(nameof(commandIndex));
            foreach (var kv in phaseDims) _dims[kv.Key] = kv.Value;
            foreach (var kv in commandIndex) _command[kv.Key] = kv.Value;
        }

        /// <summary>True when the lane is mapped.</summary>
        public bool HasLane(string laneId) => laneId != null && _dims.ContainsKey(laneId);

        /// <summary>Dimension indexes of a phase (0 early, 1 mid, 2 late) of a lane.</summary>
        public int[] PhaseDims(string laneId, int phase) => Lane(laneId)[phase];

        /// <summary>Index of the command dimension of a lane.</summary>
        public int CommandIndex(string laneId)
        {
            if (!_command.TryGetValue(laneId ?? "", out int c)) throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
            return c;
        }

        /// <summary>Phase of a dimension (0-2), or -1 for command / unmapped.</summary>
        public int PhaseOf(string laneId, int dimIndex)
        {
            var d = Lane(laneId);
            for (int k = 0; k < d.Length; k++) if (Array.IndexOf(d[k], dimIndex) >= 0) return k;
            return -1;
        }

        private int[][] Lane(string laneId)
        {
            if (!_dims.TryGetValue(laneId ?? "", out var d)) throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
            return d;
        }
    }

    /// <summary>Specialties gained and lost by one F4 check (ids, config order).</summary>
    public sealed class SpecialtyChanges
    {
        /// <summary>Gained this check.</summary>
        public readonly List<string> Gained = new List<string>();
        /// <summary>Lost this check.</summary>
        public readonly List<string> Lost = new List<string>();
        /// <summary>True when nothing changed.</summary>
        public bool IsEmpty => Gained.Count == 0 && Lost.Count == 0;
    }

    /// <summary>How far a player is from one specialty of his main lane (growth.md UI "离绝活还差"). Never contains potential.</summary>
    public sealed class SpecialtyProgress
    {
        /// <summary>The specialty.</summary>
        public SpecialtyDef Def;
        /// <summary>Main-lane index of its dimension.</summary>
        public int DimensionIndex;
        /// <summary>Its phase (0-2), or -1 for an any-lane specialty (no focus needed).</summary>
        public int Phase;
        /// <summary>max(0, unlock_value - d).</summary>
        public float DimensionGap;
        /// <summary>max(0, unlock_focus - focus); 0 for an any-lane specialty.</summary>
        public float FocusGap;
        /// <summary>True when owned ("已解锁").</summary>
        public bool Owned;
    }

    /// <summary>
    /// The growth system (design/gdd/growth.md rev 3): training of one point (F1 base amount, F2 gains with ceiling and focus
    /// dimension redistribution, F5 event modifiers), focus update per segment (F3), specialty gain / loss (F4), talent.md initial
    /// focus, and the match hook table built from the starters. Pure C#: no state of its own, no random source (callers pass the
    /// noise factor), every number from <see cref="PlayerConfig"/>. The same functions serve the player club and the AI clubs.
    /// </summary>
    /// <example><code>var g = new GrowthSystem(playerCfg, new GrowthPhaseMap(simulator.Layout));
    /// float b = g.BaseAmount(p, TrainingTarget.Early, 1f, noise);
    /// var acc = g.PointGains(p, TrainingTarget.Early, focusDim, b);
    /// g.ApplyGains(p, acc); g.UpdateFocus(p, new[] { 1, 0, 0, 0 }); var changes = g.CheckSpecialties(p);</code></example>
    public sealed class GrowthSystem
    {
        private readonly PlayerConfig _cfg;
        private readonly Dictionary<string, List<LaneSpecialty>> _byLane = new Dictionary<string, List<LaneSpecialty>>();

        private struct LaneSpecialty
        {
            public SpecialtyDef Def;
            public int Dim;
            public int Phase;
        }

        /// <summary>The phase mapping.</summary>
        public GrowthPhaseMap Map { get; }
        /// <summary>The player config (training and specialty sections).</summary>
        public PlayerConfig Config => _cfg;

        /// <summary>
        /// Creates the system and checks the specialty table against the phase mapping: a lane specialty's dimension must belong
        /// to its phase in its lane. Throws <see cref="InvalidOperationException"/> naming the specialty id.
        /// </summary>
        public GrowthSystem(PlayerConfig config, GrowthPhaseMap map)
        {
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (config.training == null || config.specialty?.specialties == null) throw new InvalidOperationException("GrowthSystem: config has no training / specialty section.");
            foreach (var lane in config.lanes)
            {
                if (!map.HasLane(lane.id)) throw new InvalidOperationException("GrowthSystem: lane '" + lane.id + "' has no phase mapping.");
                _byLane[lane.id] = new List<LaneSpecialty>();
            }
            foreach (var d in config.specialty.specialties)
            {
                if (d.IsAnyLane)
                {
                    foreach (var lane in config.lanes)
                        _byLane[lane.id].Add(new LaneSpecialty { Def = d, Dim = PlayerConfig.DimIndex(lane, d.dimension), Phase = -1 });
                    continue;
                }
                var l = config.FindLane(d.lane) ?? throw new InvalidOperationException("GrowthSystem: specialty '" + d.id + "' has unknown lane '" + d.lane + "'.");
                int dim = PlayerConfig.DimIndex(l, d.dimension);
                int phase = SpecialtyTable.PhaseIndex(d.phase);
                if (dim < 0 || phase < 0 || Array.IndexOf(map.PhaseDims(l.id, phase), dim) < 0)
                    throw new InvalidOperationException("GrowthSystem: specialty '" + d.id + "': dimension '" + d.dimension + "' is not in phase '" + d.phase + "' of lane '" + d.lane + "'.");
                _byLane[l.id].Add(new LaneSpecialty { Def = d, Dim = dim, Phase = phase });
            }
        }

        private TrainingConfig T => _cfg.training;
        private SpecialtyTable S => _cfg.specialty;

        // ------------------------------------------------------------ F1

        /// <summary>mood_t = min + (max - min) x mood/100 (growth.md D1; 0.8 at 0, 1.2 at 100).</summary>
        public float MoodFactor(float mood) => T.moodFactorMin + (T.moodFactorMax - T.moodFactorMin) * mood / 100f;

        /// <summary>stress_t = max(0, 1 - k_th x max(0, stress - threshold)/100) (1 at <= 50, 0.54 at 70, 0 from 93.5).</summary>
        public float StressFactor(float stress)
        {
            float v = 1f - T.stressSlope * Math.Max(0f, stress - T.stressThreshold) / 100f;
            return v < 0f ? 0f : v;
        }

        /// <summary>spec = 1 + phi x focus of the trained phase; 1 for command (F3).</summary>
        public float SpecFactor(Player p, TrainingTarget target)
            => target == TrainingTarget.Command ? 1f : 1f + T.specializationGain * p.GetFocus((int)target);

        /// <summary>
        /// F1 base amount of one point from the player's CURRENT (segment-start) values:
        /// <c>T_base x I x age_f x trait_f x mood_t x stress_t x spec x noise</c>. <paramref name="noise"/> is the already-drawn
        /// U(1 - noise, 1 + noise) factor (1 = no noise).
        /// </summary>
        public float BaseAmount(Player p, TrainingTarget target, float intensity, float noise = 1f)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return T.baseGain * intensity * _cfg.GetAgeCoefficient(p.Age) * p.GetTraitMultiplier(PlayerEffectKeys.TrainingGain)
                * MoodFactor(p.Mood) * StressFactor(p.Stress) * SpecFactor(p, target) * noise;
        }

        /// <summary>F5: event modifiers on one point's base amount: b x train_multiplier + train_add (the add is still x head_d).</summary>
        public static float WithEventModifiers(float b, float multiplier, float add) => b * multiplier + add;

        /// <summary>Lowest / highest noise factor (1 - noise, 1 + noise) for the UI range preview.</summary>
        public float NoiseLow => 1f - T.noise;
        /// <summary>See <see cref="NoiseLow"/>.</summary>
        public float NoiseHigh => 1f + T.noise;
        /// <summary>The configured noise half-width (0.15).</summary>
        public float Noise => T.noise;

        // ------------------------------------------------------------ F2

        /// <summary>Ceiling of every dimension: min(100, potential + cap_margin).</summary>
        public float Cap(Player p) => Math.Min(Player.StatMax, p.Potential + T.capMargin);

        /// <summary>head_d = clamp((cap - d) / span, 0, 1) of a main-lane dimension.</summary>
        public float Head(Player p, int dim) => HeadOf(Cap(p), p.GetDimension(p.MainLane, dim));

        private float HeadOf(float cap, float d)
        {
            float h = (cap - d) / T.span;
            return h < 0f ? 0f : (h > 1f ? 1f : h);
        }

        /// <summary>UI "接近上限": cap - d &lt; near_cap_margin (the potential itself is never shown).</summary>
        public bool IsNearCap(Player p, int dim) => Cap(p) - p.GetDimension(p.MainLane, dim) < T.nearCapMargin;

        /// <summary>Main-lane dimension indexes the target trains (the phase's dimensions, or the command dimension).</summary>
        public int[] TargetDims(string laneId, TrainingTarget target)
            => target == TrainingTarget.Command ? new[] { Map.CommandIndex(laneId) } : Map.PhaseDims(laneId, (int)target);

        /// <summary>True when <paramref name="focusDim"/> is -1 (none) or a dimension of the target phase (command: none only).</summary>
        public bool IsValidFocus(string laneId, TrainingTarget target, int focusDim)
        {
            if (focusDim < 0) return focusDim == -1;
            return target != TrainingTarget.Command && Array.IndexOf(Map.PhaseDims(laneId, (int)target), focusDim) >= 0;
        }

        /// <summary>
        /// F2 weights w_d of one point on the main lane: target phase dimensions 1 (with a focus dimension: focus
        /// 1 + bonus x (n-1)/n, its phase mates 1 - bonus/n), every other dimension (command included) s_other; target command:
        /// command 1, all others s_other.
        /// </summary>
        public float[] Weights(string laneId, TrainingTarget target, int focusDim)
        {
            var lane = _cfg.FindLane(laneId) ?? throw new ArgumentException("Unknown lane: " + laneId, nameof(laneId));
            var w = new float[lane.dimensions.Length];
            for (int d = 0; d < w.Length; d++) w[d] = T.otherShare;
            var dims = TargetDims(laneId, target);
            int n = dims.Length;
            bool focus = target != TrainingTarget.Command && focusDim >= 0 && Array.IndexOf(dims, focusDim) >= 0;
            foreach (int d in dims)
                w[d] = !focus ? 1f : d == focusDim ? 1f + T.focusBonus * (n - 1) / n : 1f - T.focusBonus / n;
            return w;
        }

        /// <summary>
        /// F2 gains of one point on every main-lane dimension: b x head_d x w_d, head from the CURRENT (segment-start) values.
        /// Changes nothing; sum the points of a segment and apply once with <see cref="ApplyGains"/>.
        /// </summary>
        public float[] PointGains(Player p, TrainingTarget target, int focusDim, float b)
        {
            var w = Weights(p.MainLane, target, focusDim);
            float cap = Cap(p);
            var values = p.GetRadar(p.MainLane);
            var g = new float[w.Length];
            for (int d = 0; d < g.Length; d++) g[d] = b * HeadOf(cap, values[d]) * w[d];
            return g;
        }

        /// <summary>
        /// Applies summed gains: a dimension below the ceiling rises by its gain, clamped to the ceiling; a dimension at or above
        /// the ceiling (e.g. potential lowered by an event) does not change. Never lowers a dimension.
        /// </summary>
        public void ApplyGains(Player p, float[] gains)
        {
            float cap = Cap(p);
            for (int d = 0; d < gains.Length; d++)
            {
                float v = p.GetDimension(p.MainLane, d);
                if (gains[d] > 0f && v < cap) p.SetDimension(p.MainLane, d, Math.Min(cap, v + gains[d]));
            }
        }

        // ------------------------------------------------------------ F3

        /// <summary>
        /// Segment-end focus update from point counts (order independent): n = all training points of the segment (command
        /// included), n_k = points on phase k; <c>focus_k = (1-mu)^n x focus_k + (1 - (1-mu)^n) x n_k / n</c>. No-op when n = 0.
        /// </summary>
        /// <param name="pointsPerTarget">Points per <see cref="TrainingTarget"/> (length 4: early, mid, late, command).</param>
        public void UpdateFocus(Player p, IReadOnlyList<int> pointsPerTarget)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (pointsPerTarget == null || pointsPerTarget.Count != 4) throw new ArgumentException("Need 4 counts (early, mid, late, command).", nameof(pointsPerTarget));
            int n = 0;
            foreach (int c in pointsPerTarget) { if (c < 0) throw new ArgumentOutOfRangeException(nameof(pointsPerTarget)); n += c; }
            if (n == 0) return;
            EnsureFocus(p);
            float keep = (float)Math.Pow(1.0 - T.specializationRate, n);
            for (int k = 0; k < Player.FocusPhases; k++)
                p.SetFocus(k, keep * p.GetFocus(k) + (1f - keep) * pointsPerTarget[k] / n);
        }

        /// <summary>talent.md initial focus: the main-lane phase with the highest phase mean gets the high value, the others the low one (ties E > M > L).</summary>
        public void InitializeFocus(Player p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            int best = 0; float bestMean = float.NegativeInfinity;
            for (int k = 0; k < Player.FocusPhases; k++)
            {
                var dims = Map.PhaseDims(p.MainLane, k);
                float sum = 0f;
                foreach (int d in dims) sum += p.GetDimension(p.MainLane, d);
                float mean = sum / dims.Length;
                if (mean > bestMean) { bestMean = mean; best = k; }
            }
            for (int k = 0; k < Player.FocusPhases; k++) p.SetFocus(k, k == best ? T.initialFocusHigh : T.initialFocusLow);
        }

        /// <summary>Backfill (story 012): a player without focus gets the talent.md initial focus; others are unchanged.</summary>
        public void EnsureFocus(Player p)
        {
            if (!p.HasFocus) InitializeFocus(p);
        }

        /// <summary>A newly generated player (talent.md steps 4-5): initial focus, then the specialty check (tactical brain may apply).</summary>
        public SpecialtyChanges InitializeNewPlayer(Player p)
        {
            InitializeFocus(p);
            return CheckSpecialties(p);
        }

        // ------------------------------------------------------------ F4

        /// <summary>
        /// F4 on the main lane (call at segment end after training and the focus update, and after the yearly decline): owned
        /// specialties are lost when d &lt; lose_value or (lane specialty) focus &lt; lose_focus; others are gained when
        /// d &gt;= unlock_value and (lane specialty) focus &gt;= unlock_focus.
        /// </summary>
        public SpecialtyChanges CheckSpecialties(Player p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            EnsureFocus(p);
            var changes = new SpecialtyChanges();
            foreach (var ls in _byLane[p.MainLane])
            {
                float d = p.GetDimension(p.MainLane, ls.Dim);
                bool needsFocus = ls.Phase >= 0;
                float f = needsFocus ? p.GetFocus(ls.Phase) : 0f;
                if (p.HasSpecialty(ls.Def.id))
                {
                    if (d < S.loseValue || (needsFocus && f < S.loseFocus)) { p.RemoveSpecialty(ls.Def.id); changes.Lost.Add(ls.Def.id); }
                }
                else if (d >= S.unlockValue && (!needsFocus || f >= S.unlockFocus)) { p.AddSpecialty(ls.Def.id); changes.Gained.Add(ls.Def.id); }
            }
            return changes;
        }

        /// <summary>Passes a year (Player.AdvanceYear: decline, age, contract) and runs the F4 check after the decline.</summary>
        public SpecialtyChanges AdvanceYear(Player p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            p.AdvanceYear();
            return CheckSpecialties(p);
        }

        /// <summary>Distance to every specialty of the player's main lane (UI "离绝活还差"), config order.</summary>
        public List<SpecialtyProgress> Progress(Player p)
        {
            var list = new List<SpecialtyProgress>();
            foreach (var ls in _byLane[p.MainLane])
            {
                float d = p.GetDimension(p.MainLane, ls.Dim);
                list.Add(new SpecialtyProgress
                {
                    Def = ls.Def, DimensionIndex = ls.Dim, Phase = ls.Phase,
                    DimensionGap = Math.Max(0f, S.unlockValue - d),
                    FocusGap = ls.Phase >= 0 ? Math.Max(0f, S.unlockFocus - p.GetFocus(ls.Phase)) : 0f,
                    Owned = p.HasSpecialty(ls.Def.id),
                });
            }
            return list;
        }

        /// <summary>The specialty definition of an id, or null.</summary>
        public SpecialtyDef Find(string id) => S.Find(id);

        // ------------------------------------------------------------ hooks for the match

        /// <summary>
        /// The side's hook table (growth.md "挂钩表汇总", match.md): only starters on their MAIN lane count, a specialty owned by
        /// several starters counts once, effects in specialty config order. The match sums / multiplies and clamps.
        /// </summary>
        public List<SpecialtyEffect> BuildHooks(IReadOnlyDictionary<string, Player> startersByLane)
        {
            var owned = new HashSet<string>();
            if (startersByLane != null)
                foreach (var kv in startersByLane)
                    if (kv.Value != null && kv.Value.MainLane == kv.Key)
                        foreach (var id in kv.Value.Specialties) owned.Add(id);
            var list = new List<SpecialtyEffect>();
            foreach (var d in S.specialties)
            {
                if (!owned.Contains(d.id)) continue;
                foreach (var e in d.effects) list.Add(ToEffect(e));
            }
            return list;
        }

        /// <summary>Maps one config hook to the match's effect type.</summary>
        public static SpecialtyEffect ToEffect(SpecialtyEffectDef e)
        {
            int ph = SpecialtyTable.PhaseIndex(e.phase);
            var phase = ph < 0 ? MatchPhase.Early : (MatchPhase)ph;
            switch (e.hook)
            {
                case "phase_flat": return new SpecialtyEffect(SpecialtyHook.PhaseFlat, e.value, phase);
                case "star_scale": return new SpecialtyEffect(SpecialtyHook.StarScale, e.value, phase);
                case "exec_add": return new SpecialtyEffect(SpecialtyHook.ExecAdd, e.value);
                case "sigma_scale": return new SpecialtyEffect(SpecialtyHook.SigmaScale, e.value);
                default: throw new InvalidOperationException("Unknown hook '" + e.hook + "'.");
            }
        }
    }
}
