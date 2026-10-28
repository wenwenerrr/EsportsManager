using System;
using EsportsManager.Players;

namespace EsportsManager.Matches
{
    /// <summary>League size (numbers.md 4.14).</summary>
    public sealed class SeriesConfig
    {
        /// <summary>opponent_count: fictional opponent clubs, >= 1 (initial 7, so 8 clubs with the player).</summary>
        public int opponentCount;
    }

    /// <summary>Kind of a calendar event (numbers.md 4.18).</summary>
    public enum EventType
    {
        /// <summary>Spring / summer split: round-robin regular season + playoffs.</summary>
        Split = 0,
        /// <summary>Annual finals: top clubs by split points, single elimination.</summary>
        Finals = 1,
    }

    /// <summary>One event of the yearly calendar (numbers.md 4.18): it is played after segment <see cref="afterSegment"/> is resolved.</summary>
    public sealed class CalendarEventDef
    {
        /// <summary>split or finals.</summary>
        public EventType type;
        /// <summary>name_key: stable id of the event ("spring", "summer", "finals"); the UI maps it to a display name.</summary>
        public string nameKey;
        /// <summary>after_segment: 1-based segment number of the year after whose resolve the event is played.</summary>
        public int afterSegment;
    }

    /// <summary>The yearly calendar (numbers.md 4.18 v3.4).</summary>
    public sealed class CalendarConfig
    {
        /// <summary>segments_per_year (initial 12), >= 1.</summary>
        public int segmentsPerYear;
        /// <summary>Events in calendar order (after_segment strictly increasing).</summary>
        public CalendarEventDef[] events;

        /// <summary>The event played after the 1-based segment <paramref name="segmentNumber"/>, or null when there is none.</summary>
        public CalendarEventDef EventAfter(int segmentNumber)
        {
            foreach (var e in events) if (e.afterSegment == segmentNumber) return e;
            return null;
        }

        /// <summary>The first event whose after_segment is >= <paramref name="segmentNumber"/> (1-based), or null.</summary>
        public CalendarEventDef NextEventFrom(int segmentNumber)
        {
            foreach (var e in events) if (e.afterSegment >= segmentNumber) return e;
            return null;
        }
    }

    /// <summary>
    /// Tournament formats, points and prizes (numbers.md 4.18 v3.4). Tables are indexed by FINAL POSITION (0-based place in the
    /// final order: champion, runner-up, the two semi-final losers, then the rest), so tied places (3-4) must carry equal values.
    /// </summary>
    public sealed class FormatConfig
    {
        /// <summary>regular_best_of: regular-season series length, odd >= 1 (initial 3).</summary>
        public int regularBestOf;
        /// <summary>playoff_teams: clubs in a split's playoffs, 2 or 4 (initial 4: semis 1v4, 2v3).</summary>
        public int playoffTeams;
        /// <summary>playoff_best_of: split semi-final length, odd (initial 5).</summary>
        public int playoffBestOf;
        /// <summary>split_final_best_of: split final length, odd (initial 5).</summary>
        public int splitFinalBestOf;
        /// <summary>finals_teams: clubs in the annual finals, 2 or 4 (initial 4).</summary>
        public int finalsTeams;
        /// <summary>finals_semi_best_of (initial 5).</summary>
        public int finalsSemiBestOf;
        /// <summary>finals_final_best_of (initial 7).</summary>
        public int finalsFinalBestOf;
        /// <summary>finals_points: qualification points per split final position, one per club (initial 10,7,5,5,3,2,1,0).</summary>
        public int[] finalsPoints;
        /// <summary>split_prizes: yuan per split final position, one per club (initial 80000,50000,30000,30000,10000 x4).</summary>
        public long[] splitPrizes;
        /// <summary>finals_prizes: yuan per finals final position, one per finals team (initial 200000,120000,60000,60000).</summary>
        public long[] finalsPrizes;
    }

    /// <summary>The three phases of a game (design/gdd/match.md "阶段与维度").</summary>
    public enum MatchPhase
    {
        /// <summary>Early game (laning), "early" in the config.</summary>
        Early = 0,
        /// <summary>Mid game (teamfights), "mid" in the config.</summary>
        Mid = 1,
        /// <summary>Late game (carries), "late" in the config.</summary>
        Late = 2,
    }

    /// <summary>Config-key mapping of <see cref="MatchPhase"/>.</summary>
    public static class MatchPhases
    {
        /// <summary>Number of phases (3).</summary>
        public const int Count = 3;
        /// <summary>All phases in order.</summary>
        public static readonly MatchPhase[] All = { MatchPhase.Early, MatchPhase.Mid, MatchPhase.Late };

        /// <summary>The config key ("early", "mid", "late").</summary>
        public static string Key(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Early: return "early";
                case MatchPhase.Mid: return "mid";
                case MatchPhase.Late: return "late";
                default: throw new ArgumentOutOfRangeException(nameof(phase), phase, "unknown phase.");
            }
        }

        /// <summary>Parses a config key; false for an unknown key.</summary>
        public static bool TryParse(string key, out MatchPhase phase)
        {
            foreach (var p in All) if (Key(p) == key) { phase = p; return true; }
            phase = MatchPhase.Early;
            return false;
        }
    }

    /// <summary>
    /// One lane of the dimension-to-phase mapping (match.md "阶段与维度", F2, F3): which of the lane's dimension ids feed each
    /// phase, and the lane's weight Lw[k][lane] inside each phase.
    /// </summary>
    public sealed class LanePhaseDef
    {
        /// <summary>Lane id (PlayerConfig).</summary>
        public string lane;
        /// <summary>Dimension ids of the early phase (>= 1).</summary>
        public string[] early;
        /// <summary>Dimension ids of the mid phase (>= 1).</summary>
        public string[] mid;
        /// <summary>Dimension ids of the late phase (>= 1).</summary>
        public string[] late;
        /// <summary>Lw[E][lane] (> 0).</summary>
        public double weightEarly;
        /// <summary>Lw[M][lane] (> 0).</summary>
        public double weightMid;
        /// <summary>Lw[L][lane] (> 0).</summary>
        public double weightLate;

        /// <summary>Dimension ids of a phase.</summary>
        public string[] Dims(MatchPhase phase) => phase == MatchPhase.Early ? early : phase == MatchPhase.Mid ? mid : late;

        /// <summary>Lane weight of a phase.</summary>
        public double Weight(MatchPhase phase) => phase == MatchPhase.Early ? weightEarly : phase == MatchPhase.Mid ? weightMid : weightLate;
    }

    /// <summary>Dimension-to-phase mapping (match.md "阶段与维度").</summary>
    public sealed class PhaseMappingConfig
    {
        /// <summary>command_dimension_id: the command dimension of every lane; it belongs to no phase (F5).</summary>
        public string commandDimensionId;
        /// <summary>One entry per lane.</summary>
        public LanePhaseDef[] lanes;

        /// <summary>The entry of a lane, or null.</summary>
        public LanePhaseDef Find(string laneId)
        {
            if (lanes != null) foreach (var l in lanes) if (l != null && l.lane == laneId) return l;
            return null;
        }
    }

    /// <summary>F1 effective dimension value: mood_f = m0 + (1 - m0) x mood/100; stress_f = 1 - kappa_s x stress/100 x res.</summary>
    public sealed class EffectiveConfig
    {
        /// <summary>mood_floor m0 (0.95), in [0.85, 1]: mood_f at mood 0.</summary>
        public double moodFloor;
        /// <summary>stress_factor kappa_s (0.025), in [0, 0.1].</summary>
        public double stressFactor;
    }

    /// <summary>F3, F4, F6, F8 knobs.</summary>
    public sealed class PhaseStrengthConfig
    {
        /// <summary>short_board_early lambda_E (0.15), in [0, 0.5].</summary>
        public double shortBoardEarly;
        /// <summary>short_board_mid lambda_M (0.5), in [0, 0.5]: teamfights look at the weakest player.</summary>
        public double shortBoardMid;
        /// <summary>short_board_late lambda_L (0.15), in [0, 0.5].</summary>
        public double shortBoardLate;
        /// <summary>mid_chemistry chi (1.0), in [0, 2]: S_M += chi x (chem - 50) / 10.</summary>
        public double midChemistry;
        /// <summary>star_factor eta (0.3), in [0, 0.4].</summary>
        public double starFactor;
        /// <summary>star_threshold st0 (4), in [0, 8].</summary>
        public double starThreshold;
        /// <summary>star_cap (15), in [5, 25].</summary>
        public double starCap;
        /// <summary>phase_weight_early B_E (1), > 0.</summary>
        public double phaseWeightEarly;
        /// <summary>phase_weight_mid B_M (1.1), > 0.</summary>
        public double phaseWeightMid;
        /// <summary>phase_weight_late B_L (1), > 0.</summary>
        public double phaseWeightLate;

        /// <summary>B_k of a phase.</summary>
        public double PhaseWeight(MatchPhase phase) => phase == MatchPhase.Early ? phaseWeightEarly : phase == MatchPhase.Mid ? phaseWeightMid : phaseWeightLate;

        /// <summary>lambda_k of a phase.</summary>
        public double ShortBoard(MatchPhase phase) => phase == MatchPhase.Early ? shortBoardEarly : phase == MatchPhase.Mid ? shortBoardMid : shortBoardLate;
    }

    /// <summary>F5 execution: clamp(e0 + e1 x chem/100 + e2 x cmd/100 + exec_add, min, max).</summary>
    public sealed class ExecutionConfig
    {
        /// <summary>e0 (0.3).</summary>
        public double e0;
        /// <summary>e1 chemistry share (0.4).</summary>
        public double e1;
        /// <summary>e2 command share (0.3). e0 + e1 + e2 must be 1 (+-0.001).</summary>
        public double e2;
        /// <summary>Lower clamp (0.3).</summary>
        public double min;
        /// <summary>Upper clamp (1.0).</summary>
        public double max;
    }

    /// <summary>One relation of the three-phase cycle (F7): leading in <see cref="beats"/> weakens the other side's lead in <see cref="weakens"/>.</summary>
    public sealed class CycleRelationDef
    {
        /// <summary>Phase X.</summary>
        public MatchPhase beats;
        /// <summary>Phase Y.</summary>
        public MatchPhase weakens;
    }

    /// <summary>F7 three-phase cycle: f_Y = max(w_min, 1 - s x |dS_X| / 10) when dS_X x Delta_Y &lt; 0.</summary>
    public sealed class CycleConfig
    {
        /// <summary>strength s (0.22), in [0, 0.9].</summary>
        public double strength;
        /// <summary>weight_min w_min (0.4), in (0, 1].</summary>
        public double weightMin;
        /// <summary>Relations; every phase is weakened by exactly one (E->L, M->E, L->M).</summary>
        public CycleRelationDef[] relations;
    }

    /// <summary>The phase a tactic focuses on (F9).</summary>
    public sealed class TacticFocusDef
    {
        /// <summary>The tactic.</summary>
        public MatchTactic tactic;
        /// <summary>Its phase.</summary>
        public MatchPhase phase;
    }

    /// <summary>One counter relation: <see cref="tactic"/> beats <see cref="beats"/>.</summary>
    public sealed class TacticCounterDef
    {
        /// <summary>The winning tactic.</summary>
        public MatchTactic tactic;
        /// <summary>The countered tactic.</summary>
        public MatchTactic beats;
    }

    /// <summary>F9 tactic fit and F10 counter.</summary>
    public sealed class TacticConfig
    {
        /// <summary>fit_scale a (0.1), >= 0.</summary>
        public double fitScale;
        /// <summary>fit_cap F (1.0) per side, >= 0.</summary>
        public double fitCap;
        /// <summary>counter C (0.5), >= 0: the countering side gets C x its execution.</summary>
        public double counter;
        /// <summary>budget (3.0): validation requires 2F + 2C &lt;= budget (own tactic choice moves G by at most this).</summary>
        public double budget;
        /// <summary>Focus phase of every tactic (exactly one entry per tactic).</summary>
        public TacticFocusDef[] focus;
        /// <summary>Counter relations; a pair may not appear twice or in both directions.</summary>
        public TacticCounterDef[] counters;

        /// <summary>The focus phase of a tactic.</summary>
        public MatchPhase FocusOf(MatchTactic t)
        {
            foreach (var f in focus) if (f.tactic == t) return f.phase;
            throw new InvalidOperationException("MatchConfig: tactic '" + MatchTactics.Key(t) + "' has no focus phase.");
        }
    }

    /// <summary>F11 noise: sigma = (g0 + g1 x avgStress/100 x avgRes) x (1 - command_reduction x cmd/100) x sigma_scale.</summary>
    public sealed class NoiseConfig
    {
        /// <summary>g0 (2 points), >= 0.</summary>
        public double g0;
        /// <summary>g1 (6 points), >= 0.</summary>
        public double g1;
        /// <summary>command_reduction c_cmd (0.3), in [0,1).</summary>
        public double commandReduction;
    }

    /// <summary>F13 win probability.</summary>
    public sealed class WinProbabilityConfig
    {
        /// <summary>k (8), > 0.</summary>
        public double k;
        /// <summary>p_floor (0.15), in [0.05, 0.25]; p is clamped to [p_floor, 1 - p_floor].</summary>
        public double pFloor;
    }

    /// <summary>Aggregate clamps of the specialty hooks (match.md "绝活挂钩点").</summary>
    public sealed class SpecialtyConfig
    {
        /// <summary>phase_flat_cap: the per-phase phase_flat total is clamped to [-cap, +cap] (5).</summary>
        public double phaseFlatCap;
        /// <summary>star_scale_min (0.5).</summary>
        public double starScaleMin;
        /// <summary>star_scale_max (2).</summary>
        public double starScaleMax;
        /// <summary>sigma_scale_min (0.7).</summary>
        public double sigmaScaleMin;
        /// <summary>sigma_scale_max (1.3).</summary>
        public double sigmaScaleMax;
    }

    /// <summary>
    /// How opponent clubs are generated (numbers.md 4.14). Each opponent is generated from one fresh player pool with the SAME
    /// generator and config as the player's own pool; it takes a random one of the top <see cref="starterPickRange"/> main-lane
    /// players of every lane. In matches opponents use their players' own mood and stress (match.md; no fixed values, no form).
    /// </summary>
    public sealed class OpponentConfig
    {
        /// <summary>Opponent picks a random main-lane player among the best N by lane score, N >= 1.</summary>
        public int starterPickRange;
        /// <summary>Bench players added to an opponent (roster 5 + this), in [0, 2].</summary>
        public int rosterExtraMin;
        /// <summary>See <see cref="rosterExtraMin"/>; in [rosterExtraMin, 2].</summary>
        public int rosterExtraMax;
        /// <summary>Lower bound of the pair familiarity F of an opponent's starters, in [0,100].</summary>
        public float familiarityMin;
        /// <summary>Upper bound of the same, in [familiarityMin,100].</summary>
        public float familiarityMax;
        /// <summary>Fictional club names, distinct and non-empty; at least opponent_count of them.</summary>
        public string[] clubNames;
        /// <summary>pop_min: lowest generated opponent club popularity, in [0,100].</summary>
        public float popMin;
        /// <summary>pop_max: highest generated opponent club popularity, in [popMin,100].</summary>
        public float popMax;
    }

    /// <summary>Report reason selection (match.md "战报数据").</summary>
    public sealed class ReportConfig
    {
        /// <summary>min_reasons (2), >= 1.</summary>
        public int minReasons;
        /// <summary>max_reasons (3), in [minReasons, 7].</summary>
        public int maxReasons;
        /// <summary>min_contribution (0.5 points): a factor in the winner's favour is listed only from this |sum| on.</summary>
        public double minContribution;
        /// <summary>even_threshold (0.05): the series is "even" when the mean p0 is within 0.5 +- this.</summary>
        public double evenThreshold;
        /// <summary>luck_threshold (0.6): a game's loser with his own p0 >= this lost by luck.</summary>
        public double luckThreshold;
        /// <summary>tactic_advice_min (0.5 points): show "换成 X 本可 +Y" when the best tactic's mean G0 is this much higher.</summary>
        public double tacticAdviceMin;
    }

    /// <summary>The player club's name rules (Story 004: the player names the club at the start of a new game).</summary>
    public sealed class ClubConfig
    {
        /// <summary>default_name: used when the player enters nothing, and by every debug demo. Non-empty, at most nameMaxLength.</summary>
        public string defaultName;
        /// <summary>name_max_length: longest club name in characters, in [1, 64].</summary>
        public int nameMaxLength;

        /// <summary>The name to use for a raw entry: trimmed, cut to <see cref="nameMaxLength"/>; empty / whitespace -> <see cref="defaultName"/>.</summary>
        public string NormalizeName(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return defaultName;
            if (s.Length > nameMaxLength) s = s.Substring(0, nameMaxLength).TrimEnd();
            return s;
        }
    }

    /// <summary>
    /// All match tuning numbers (design/gdd/match.md Tuning Knobs), loaded from Assets/Data/match_config.json by
    /// <see cref="MatchConfigLoader"/>. Calendar, formats and prizes (numbers.md 4.18) are unchanged by the v4 model.
    /// The decisive / hesitant, assertive, resolute and clever coefficients live in the PlayerConfig trait effects
    /// ("decider_performance", "command", "stress_variance_impact", "off_lane_discount"), the off-lane factor in
    /// PlayerConfig.off_lane_discount, so there is one source of truth for them.
    /// </summary>
    public sealed class MatchConfig
    {
        /// <summary>League size.</summary>
        public SeriesConfig series;
        /// <summary>Yearly calendar (numbers.md 4.18).</summary>
        public CalendarConfig calendar;
        /// <summary>Tournament formats, points, prizes (numbers.md 4.18).</summary>
        public FormatConfig format;
        /// <summary>Dimension-to-phase mapping and lane weights.</summary>
        public PhaseMappingConfig phases;
        /// <summary>F1 mood / stress factors.</summary>
        public EffectiveConfig effective;
        /// <summary>F3 / F4 / F6 / F8 knobs.</summary>
        public PhaseStrengthConfig strength;
        /// <summary>F5 execution.</summary>
        public ExecutionConfig execution;
        /// <summary>F7 three-phase cycle.</summary>
        public CycleConfig cycle;
        /// <summary>F9 / F10 tactics.</summary>
        public TacticConfig tactic;
        /// <summary>F11 noise.</summary>
        public NoiseConfig noise;
        /// <summary>F13 win probability.</summary>
        public WinProbabilityConfig winProbability;
        /// <summary>Specialty hook clamps.</summary>
        public SpecialtyConfig specialty;
        /// <summary>Opponent generation.</summary>
        public OpponentConfig opponent;
        /// <summary>Report selection.</summary>
        public ReportConfig report;
        /// <summary>Player club name rules.</summary>
        public ClubConfig club;

        /// <summary>Longest legal series (match.md: bestOf is an odd number in 1..9).</summary>
        public const int MaxBestOf = 9;

        /// <summary>Games a side must win a regular-season series: regular_best_of / 2 + 1.</summary>
        public int GamesToWin => GamesToWinOf(format.regularBestOf);

        /// <summary>Games needed to take a best-of-<paramref name="bestOf"/> series: bestOf / 2 + 1.</summary>
        public static int GamesToWinOf(int bestOf) => bestOf / 2 + 1;

        /// <summary>True when <paramref name="bestOf"/> is an odd number in [1, 9].</summary>
        public static bool IsLegalBestOf(int bestOf) => bestOf >= 1 && bestOf <= MaxBestOf && bestOf % 2 == 1;

        /// <summary>Clubs in the league: opponents + the player club.</summary>
        public int ClubCount => series.opponentCount + 1;

        /// <summary>True when <paramref name="attacker"/> counters <paramref name="defender"/> in the table.</summary>
        public bool Counters(MatchTactic attacker, MatchTactic defender)
        {
            foreach (var c in tactic.counters)
                if (c.tactic == attacker && c.beats == defender) return true;
            return false;
        }

        /// <summary>Checks every range and the shape of the phase mapping. Throws <see cref="InvalidOperationException"/> naming the key.</summary>
        public void Validate()
        {
            if (series == null || calendar == null || format == null || phases == null || effective == null || strength == null
                || execution == null || cycle == null || tactic == null || noise == null || winProbability == null || specialty == null
                || opponent == null || report == null || club == null)
                throw Fail("a config section is missing.");

            if (series.opponentCount < 1) throw Fail("series.opponentCount must be >= 1, got " + series.opponentCount + ".");
            ValidateCalendar();
            ValidateFormat();
            ValidateMapping();

            Range("effective.moodFloor (m0)", effective.moodFloor, 0.85, 1);
            Range("effective.stressFactor (kappa_s)", effective.stressFactor, 0, 0.1);

            Range("strength.shortBoardEarly (lambda_E)", strength.shortBoardEarly, 0, 0.5);
            Range("strength.shortBoardMid (lambda_M)", strength.shortBoardMid, 0, 0.5);
            Range("strength.shortBoardLate (lambda_L)", strength.shortBoardLate, 0, 0.5);
            Range("strength.midChemistry (chi)", strength.midChemistry, 0, 2);
            Range("strength.starFactor (eta)", strength.starFactor, 0, 0.4);
            Range("strength.starThreshold (st0)", strength.starThreshold, 0, 8);
            Range("strength.starCap", strength.starCap, 5, 25);
            Positive("strength.phaseWeightEarly", strength.phaseWeightEarly);
            Positive("strength.phaseWeightMid", strength.phaseWeightMid);
            Positive("strength.phaseWeightLate", strength.phaseWeightLate);

            Range("execution.e0", execution.e0, 0, 1);
            Range("execution.e1", execution.e1, 0, 1);
            Range("execution.e2", execution.e2, 0, 1);
            double esum = execution.e0 + execution.e1 + execution.e2;
            if (!(Math.Abs(esum - 1.0) <= 0.001 + 1e-9)) throw Fail("execution.e0 + e1 + e2 must be 1 (+-0.001), got " + esum + ".");
            Range("execution.min", execution.min, 0, 1);
            Range("execution.max", execution.max, 0, 1);
            if (!(execution.min <= execution.max)) throw Fail("execution.min must be <= execution.max.");

            Range("cycle.strength (s)", cycle.strength, 0, 0.9);
            if (!(cycle.weightMin > 0f && cycle.weightMin <= 1f))
                throw Fail("cycle.weightMin (w_min) must be in (0,1], got " + cycle.weightMin + ".");
            if (cycle.relations == null) throw Fail("cycle.relations is missing.");
            foreach (var k in MatchPhases.All)
            {
                int n = 0;
                foreach (var r in cycle.relations)
                {
                    if (r == null) throw Fail("cycle.relations has a null entry.");
                    if (r.weakens == k) n++;
                }
                if (n != 1) throw Fail("cycle.relations must weaken phase '" + MatchPhases.Key(k) + "' exactly once, got " + n + ".");
            }
            foreach (var r in cycle.relations)
                if (r.beats == r.weakens) throw Fail("cycle.relations: phase '" + MatchPhases.Key(r.beats) + "' cannot weaken itself.");

            NonNegative("tactic.fitScale", tactic.fitScale);
            NonNegative("tactic.fitCap", tactic.fitCap);
            NonNegative("tactic.counter", tactic.counter);
            NonNegative("tactic.budget", tactic.budget);
            double spend = 2.0 * tactic.fitCap + 2.0 * tactic.counter;
            if (spend > tactic.budget + 1e-9)
                throw Fail("tactic budget: 2 x tactic.fitCap + 2 x tactic.counter must be <= tactic.budget (" + tactic.budget + "), got " + spend + ".");
            ValidateTactics();

            NonNegative("noise.g0", noise.g0);
            NonNegative("noise.g1", noise.g1);
            if (!(noise.commandReduction >= 0f && noise.commandReduction < 1f))
                throw Fail("noise.commandReduction must be in [0,1), got " + noise.commandReduction + ".");

            if (!(winProbability.k > 0f) || double.IsInfinity(winProbability.k)) throw Fail("winProbability.k must be finite and > 0, got " + winProbability.k + ".");
            Range("winProbability.pFloor (p_floor)", winProbability.pFloor, 0.05, 0.25);

            NonNegative("specialty.phaseFlatCap", specialty.phaseFlatCap);
            Positive("specialty.starScaleMin", specialty.starScaleMin);
            if (!(specialty.starScaleMax >= specialty.starScaleMin) || double.IsInfinity(specialty.starScaleMax)) throw Fail("specialty.starScaleMax must be >= starScaleMin.");
            Positive("specialty.sigmaScaleMin", specialty.sigmaScaleMin);
            if (!(specialty.sigmaScaleMax >= specialty.sigmaScaleMin) || double.IsInfinity(specialty.sigmaScaleMax)) throw Fail("specialty.sigmaScaleMax must be >= sigmaScaleMin.");

            ValidateMonotonicity();

            if (opponent.starterPickRange < 1) throw Fail("opponent.starterPickRange must be >= 1, got " + opponent.starterPickRange + ".");
            if (opponent.rosterExtraMin < 0 || opponent.rosterExtraMin > 2) throw Fail("opponent.rosterExtraMin must be in [0,2], got " + opponent.rosterExtraMin + ".");
            if (opponent.rosterExtraMax < opponent.rosterExtraMin || opponent.rosterExtraMax > 2)
                throw Fail("opponent.rosterExtraMax must be in [rosterExtraMin,2], got " + opponent.rosterExtraMax + ".");
            Range("opponent.familiarityMin", opponent.familiarityMin, 0f, 100f);
            Range("opponent.familiarityMax", opponent.familiarityMax, 0f, 100f);
            if (opponent.familiarityMin > opponent.familiarityMax) throw Fail("opponent.familiarityMin must be <= familiarityMax.");
            if (opponent.clubNames == null || opponent.clubNames.Length < series.opponentCount)
                throw Fail("opponent.clubNames needs at least series.opponentCount (" + series.opponentCount + ") names.");
            for (int i = 0; i < opponent.clubNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(opponent.clubNames[i])) throw Fail("opponent.clubNames[" + i + "] is empty.");
                for (int j = 0; j < i; j++)
                    if (opponent.clubNames[j] == opponent.clubNames[i]) throw Fail("opponent.clubNames has a duplicate: " + opponent.clubNames[i] + ".");
            }
            Range("opponent.popMin", opponent.popMin, 0f, 100f);
            Range("opponent.popMax", opponent.popMax, 0f, 100f);
            if (opponent.popMin > opponent.popMax) throw Fail("opponent.popMin must be <= popMax.");

            if (report.minReasons < 1) throw Fail("report.minReasons must be >= 1, got " + report.minReasons + ".");
            if (report.maxReasons < report.minReasons || report.maxReasons > 7)
                throw Fail("report.maxReasons must be in [minReasons,7], got " + report.maxReasons + ".");
            NonNegative("report.minContribution", report.minContribution);
            Range("report.evenThreshold", report.evenThreshold, 0, 0.5);
            Range("report.luckThreshold", report.luckThreshold, 0.5, 1);
            NonNegative("report.tacticAdviceMin", report.tacticAdviceMin);
            if (club.nameMaxLength < 1 || club.nameMaxLength > 64) throw Fail("club.nameMaxLength must be in [1,64], got " + club.nameMaxLength + ".");
            if (string.IsNullOrWhiteSpace(club.defaultName) || club.defaultName.Trim() != club.defaultName || club.defaultName.Length > club.nameMaxLength)
                throw Fail("club.defaultName must be non-empty, trimmed and at most nameMaxLength characters.");
        }

        /// <summary>
        /// Checks the parts that depend on the player config (match.md Edge Cases): the mapping has exactly the PlayerConfig lanes;
        /// every lane has the command dimension; every mapped id is a dimension of its lane; every non-command dimension is mapped.
        /// Errors name the lane id and dimension id. Throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public void ValidateAgainst(PlayerConfig players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            string cmd = phases.commandDimensionId;
            foreach (var lane in players.lanes)
            {
                var map = phases.Find(lane.id) ?? throw Fail("phases.lanes has no entry for lane '" + lane.id + "'.");
                bool hasCmd = false;
                foreach (var d in lane.dimensions) if (d.id == cmd) hasCmd = true;
                if (!hasCmd) throw Fail("phases.commandDimensionId '" + cmd + "' is not a dimension of lane '" + lane.id + "'.");
                foreach (var phase in MatchPhases.All)
                    foreach (var id in map.Dims(phase))
                    {
                        bool known = false;
                        foreach (var d in lane.dimensions) if (d.id == id) known = true;
                        if (!known) throw Fail("phases lane '" + lane.id + "' maps unknown dimension '" + id + "'.");
                    }
                foreach (var d in lane.dimensions)
                {
                    if (d.id == cmd) continue;
                    bool mapped = false;
                    foreach (var phase in MatchPhases.All) if (Array.IndexOf(map.Dims(phase), d.id) >= 0) mapped = true;
                    if (!mapped) throw Fail("phases lane '" + lane.id + "' does not map dimension '" + d.id + "' to any phase.");
                }
            }
            foreach (var l in phases.lanes)
                if (players.FindLane(l.lane) == null) throw Fail("phases.lanes names unknown lane '" + l.lane + "' (not in the PlayerConfig).");
        }

        private void ValidateMapping()
        {
            if (string.IsNullOrEmpty(phases.commandDimensionId)) throw Fail("phases.commandDimensionId must not be empty.");
            if (phases.lanes == null || phases.lanes.Length == 0) throw Fail("phases.lanes must not be empty.");
            for (int i = 0; i < phases.lanes.Length; i++)
            {
                var l = phases.lanes[i] ?? throw Fail("phases.lanes[" + i + "] is null.");
                if (string.IsNullOrEmpty(l.lane)) throw Fail("phases.lanes[" + i + "].lane must not be empty.");
                for (int j = 0; j < i; j++) if (phases.lanes[j].lane == l.lane) throw Fail("phases.lanes lists lane '" + l.lane + "' twice.");
                var seen = new System.Collections.Generic.Dictionary<string, MatchPhase>();
                foreach (var phase in MatchPhases.All)
                {
                    var dims = l.Dims(phase);
                    if (dims == null || dims.Length == 0)
                        throw Fail("phases lane '" + l.lane + "' has no dimension in phase '" + MatchPhases.Key(phase) + "'.");
                    foreach (var id in dims)
                    {
                        if (string.IsNullOrEmpty(id)) throw Fail("phases lane '" + l.lane + "' has an empty dimension id.");
                        if (id == phases.commandDimensionId)
                            throw Fail("phases lane '" + l.lane + "' maps the command dimension '" + id + "'; command belongs to no phase.");
                        if (seen.TryGetValue(id, out var other))
                            throw Fail("phases lane '" + l.lane + "' maps dimension '" + id + "' to two phases ('" + MatchPhases.Key(other)
                                + "' and '" + MatchPhases.Key(phase) + "') or twice.");
                        seen[id] = phase;
                    }
                    Positive("phases lane '" + l.lane + "' weight_" + MatchPhases.Key(phase), l.Weight(phase));
                }
            }
        }

        // match.md "绝活挂钩点": eta x star_scale_max / 5 < min_l(Lw[k][l] / sum Lw[k]) x (1 - lambda_k) for the star phases, so
        // raising any one lane never lowers the own phase gap.
        private void ValidateMonotonicity()
        {
            if (phases?.lanes == null || phases.lanes.Length == 0) return;
            foreach (var k in new[] { MatchPhase.Early, MatchPhase.Late })
            {
                double sum = 0.0, min = double.MaxValue;
                foreach (var l in phases.lanes) { sum += l.Weight(k); if (l.Weight(k) < min) min = l.Weight(k); }
                double rhs = min / sum * (1.0 - strength.ShortBoard(k));
                double lhs = strength.starFactor * specialty.starScaleMax / 5.0;
                if (!(lhs < rhs))
                    throw Fail("monotonicity: strength.starFactor x specialty.starScaleMax / 5 (" + lhs.ToString("0.####")
                        + ") must be < min lane weight share x (1 - lambda) of phase '" + MatchPhases.Key(k) + "' (" + rhs.ToString("0.####") + ").");
            }
        }

        private void ValidateTactics()
        {
            if (tactic.focus == null) throw Fail("tactic.focus is missing.");
            foreach (var t in MatchTactics.All)
            {
                int n = 0;
                foreach (var f in tactic.focus) if (f != null && f.tactic == t) n++;
                if (n != 1) throw Fail("tactic.focus must list tactic '" + MatchTactics.Key(t) + "' exactly once, got " + n + ".");
            }
            if (tactic.focus.Length != MatchTactics.All.Length) throw Fail("tactic.focus must have one entry per tactic.");
            if (tactic.counters == null) throw Fail("tactic.counters is missing.");
            for (int i = 0; i < tactic.counters.Length; i++)
            {
                var c = tactic.counters[i] ?? throw Fail("tactic.counters[" + i + "] is null.");
                if (c.tactic == c.beats) throw Fail("tactic.counters[" + i + "] counters itself.");
                for (int j = 0; j < i; j++)
                {
                    var d = tactic.counters[j];
                    if (d.tactic == c.tactic && d.beats == c.beats) throw Fail("tactic.counters[" + i + "] duplicates entry " + j + ".");
                    if (d.tactic == c.beats && d.beats == c.tactic) throw Fail("tactic.counters[" + i + "] contradicts entry " + j + " (both directions).");
                }
            }
        }

        private void ValidateCalendar()
        {
            if (calendar.segmentsPerYear < 1) throw Fail("calendar.segmentsPerYear must be >= 1, got " + calendar.segmentsPerYear + ".");
            if (calendar.events == null) throw Fail("calendar.events is missing.");
            int last = 0, finals = 0, splits = 0;
            for (int i = 0; i < calendar.events.Length; i++)
            {
                var e = calendar.events[i] ?? throw Fail("calendar.events[" + i + "] is null.");
                if (!Enum.IsDefined(typeof(EventType), e.type)) throw Fail("calendar.events[" + i + "].type is not a known event type.");
                if (string.IsNullOrWhiteSpace(e.nameKey)) throw Fail("calendar.events[" + i + "].nameKey is empty.");
                for (int j = 0; j < i; j++)
                    if (calendar.events[j].nameKey == e.nameKey) throw Fail("calendar.events nameKey '" + e.nameKey + "' is used twice.");
                if (e.afterSegment < 1 || e.afterSegment > calendar.segmentsPerYear)
                    throw Fail("calendar.events[" + i + "].afterSegment must be in [1,segmentsPerYear], got " + e.afterSegment + ".");
                if (e.afterSegment <= last) throw Fail("calendar.events[" + i + "].afterSegment must be greater than the previous event's (" + last + ").");
                last = e.afterSegment;
                if (e.type == EventType.Split) splits++;
                else
                {
                    finals++;
                    if (splits == 0) throw Fail("calendar.events[" + i + "]: the finals need at least one split before them.");
                }
            }
            if (finals > 1) throw Fail("calendar.events may contain at most one finals event, got " + finals + ".");
        }

        private void ValidateFormat()
        {
            int clubs = ClubCount;
            BestOf("format.regularBestOf", format.regularBestOf);
            BestOf("format.playoffBestOf", format.playoffBestOf);
            BestOf("format.splitFinalBestOf", format.splitFinalBestOf);
            BestOf("format.finalsSemiBestOf", format.finalsSemiBestOf);
            BestOf("format.finalsFinalBestOf", format.finalsFinalBestOf);
            if ((format.playoffTeams != 2 && format.playoffTeams != 4) || format.playoffTeams > clubs)
                throw Fail("format.playoffTeams must be 2 or 4 and at most the club count (" + clubs + "), got " + format.playoffTeams + ".");
            if ((format.finalsTeams != 2 && format.finalsTeams != 4) || format.finalsTeams > clubs)
                throw Fail("format.finalsTeams must be 2 or 4 and at most the club count (" + clubs + "), got " + format.finalsTeams + ".");
            Table("format.finalsPoints", format.finalsPoints == null ? null : Array.ConvertAll(format.finalsPoints, v => (long)v), clubs, format.playoffTeams);
            Table("format.splitPrizes", format.splitPrizes, clubs, format.playoffTeams);
            Table("format.finalsPrizes", format.finalsPrizes, format.finalsTeams, format.finalsTeams);
        }

        // A per-position table: exactly `length` entries, each >= 0, non-increasing, and the two semi-final losers' positions
        // (2 and 3, only with a 4-team bracket) equal because they share a place.
        private static void Table(string name, long[] t, int length, int bracket)
        {
            if (t == null) throw Fail(name + " is missing.");
            if (t.Length != length) throw Fail(name + " must have exactly " + length + " entries, got " + t.Length + ".");
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] < 0) throw Fail(name + "[" + i + "] must be >= 0, got " + t[i] + ".");
                if (i > 0 && t[i] > t[i - 1]) throw Fail(name + " must be non-increasing (entry " + i + " is above entry " + (i - 1) + ").");
            }
            if (bracket == 4 && t[2] != t[3]) throw Fail(name + "[2] and [3] must be equal: both semi-final losers share place 3.");
        }

        private static void BestOf(string name, int v)
        {
            if (!IsLegalBestOf(v)) throw Fail(name + " must be odd and in [1," + MaxBestOf + "], got " + v + ".");
        }

        private static InvalidOperationException Fail(string msg) => new InvalidOperationException("MatchConfig: " + msg);

        private static void NonNegative(string name, float v)
        {
            if (!(v >= 0f) || float.IsInfinity(v)) throw Fail(name + " must be finite and >= 0, got " + v + ".");
        }

        private static void Range(string name, float v, float lo, float hi)
        {
            if (!(v >= lo && v <= hi)) throw Fail(name + " must be in [" + lo + "," + hi + "], got " + v + ".");
        }

        private static void Positive(string name, double v)
        {
            if (!(v > 0.0) || double.IsInfinity(v)) throw Fail(name + " must be finite and > 0, got " + v + ".");
        }

        private static void NonNegative(string name, double v)
        {
            if (!(v >= 0.0) || double.IsInfinity(v)) throw Fail(name + " must be finite and >= 0, got " + v + ".");
        }

        private static void Range(string name, double v, double lo, double hi)
        {
            if (!(v >= lo - 1e-12 && v <= hi + 1e-12)) throw Fail(name + " must be in [" + lo + "," + hi + "], got " + v + ".");
        }

        private static void Positive(string name, float v)
        {
            if (!(v > 0f) || float.IsInfinity(v)) throw Fail(name + " must be finite and > 0, got " + v + ".");
        }
    }
}
