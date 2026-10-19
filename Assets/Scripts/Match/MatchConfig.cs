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

    /// <summary>Overall power weights (numbers.md 4.9 steps 2 and 3).</summary>
    public sealed class PowerConfig
    {
        /// <summary>WS weight of the skill score (initial 3).</summary>
        public float weightSkill;
        /// <summary>WS weight of chemistry (initial 1).</summary>
        public float weightChemistry;
        /// <summary>WS weight of the style score (initial 1).</summary>
        public float weightStyle;
        /// <summary>WS weight of the commander score inside the style score.</summary>
        public float styleWeightCommand;
        /// <summary>WS weight of the lane-match score inside the style score.</summary>
        public float styleWeightLaneMatch;
        /// <summary>Id of the "command" dimension in every lane of the PlayerConfig.</summary>
        public string commandDimensionId;
    }

    /// <summary>Per-game noise (numbers.md 4.9 step 4): sigma = sigma0 + sigma1 x mean stress/100 x resolute factor.</summary>
    public sealed class NoiseConfig
    {
        /// <summary>sigma0, in [0,1).</summary>
        public float sigma0;
        /// <summary>sigma1, in [0,1).</summary>
        public float sigma1;
    }

    /// <summary>Win probability (numbers.md 4.9 step 5).</summary>
    public sealed class WinProbabilityConfig
    {
        /// <summary>k: logistic scale in power points, > 0 (initial 8).</summary>
        public float k;
        /// <summary>p_min (initial 0.10).</summary>
        public float pMin;
        /// <summary>p_max (initial 0.90).</summary>
        public float pMax;
    }

    /// <summary>One counter relation: <see cref="tactic"/> beats <see cref="beats"/>.</summary>
    public sealed class TacticCounterDef
    {
        /// <summary>The winning tactic.</summary>
        public MatchTactic tactic;
        /// <summary>The countered tactic.</summary>
        public MatchTactic beats;
    }

    /// <summary>Tactic counter table (numbers.md 4.9 step 3).</summary>
    public sealed class TacticConfig
    {
        /// <summary>Power points the countering side gains, >= 0. Only the counter side gets it, the other side is untouched.</summary>
        public float counterBonus;
        /// <summary>Counter relations; a pair may not appear twice or in both directions.</summary>
        public TacticCounterDef[] counters;
    }

    /// <summary>
    /// How opponent clubs are generated and evaluated (numbers.md 4.14). Each opponent is generated from one fresh
    /// player pool with the SAME generator and config as the player's own pool; the player's starting team takes the
    /// best main-lane player of every lane, an opponent takes a random one of the top <see cref="starterPickRange"/>
    /// of every lane. So an opponent is on average a little weaker than the player's best pick and has a spread.
    /// </summary>
    public sealed class OpponentConfig
    {
        /// <summary>Mood used for every opponent starter in matches (numbers.md 4.14: 50), in [0,100].</summary>
        public float fixedMood;
        /// <summary>Stress used for every opponent starter in matches (50), in [0,100].</summary>
        public float fixedStress;
        /// <summary>Opponent picks a random main-lane player among the best N by lane score, N >= 1. 1 = same as the player's start.</summary>
        public int starterPickRange;
        /// <summary>Bench players added to an opponent (roster 5 + this), in [0, 2].</summary>
        public int rosterExtraMin;
        /// <summary>See <see cref="rosterExtraMin"/>; in [rosterExtraMin, 2].</summary>
        public int rosterExtraMax;
        /// <summary>Lower bound of the pair familiarity F of an opponent's starters (all pairs share one value per club), in [0,100].</summary>
        public float familiarityMin;
        /// <summary>Upper bound of the same, in [familiarityMin,100].</summary>
        public float familiarityMax;
        /// <summary>Fictional club names, distinct and non-empty; at least opponent_count of them (the first N are used).</summary>
        public string[] clubNames;
        /// <summary>form_down (numbers.md 4.14 v3.3): lowest form = -formDown, >= 0 (initial 4).</summary>
        public float formDown;
        /// <summary>form_up_base: highest form = formUpBase + formUpPop x pop/100, >= 0 (initial 4).</summary>
        public float formUpBase;
        /// <summary>form_up_pop: extra form ceiling of a club at popularity 100, >= 0 (initial 8).</summary>
        public float formUpPop;
        /// <summary>form_step: per-segment random walk form += U(-formStep, +formStep), >= 0 (initial 2).</summary>
        public float formStep;
        /// <summary>pop_min: lowest generated opponent club popularity, in [0,100].</summary>
        public float popMin;
        /// <summary>pop_max: highest generated opponent club popularity, in [popMin,100].</summary>
        public float popMax;

        /// <summary>Upper form bound of a club with popularity <paramref name="pop"/>: formUpBase + formUpPop x pop/100.</summary>
        public float FormUpper(float pop) => formUpBase + formUpPop * pop / 100f;
    }

    /// <summary>Match report selection (Story 004).</summary>
    public sealed class ReportConfig
    {
        /// <summary>Fewest reasons a report lists, >= 1.</summary>
        public int minReasons;
        /// <summary>Most reasons a report lists, in [minReasons, 5].</summary>
        public int maxReasons;
        /// <summary>Reasons beyond minReasons are only listed when their |contribution| (power points) reaches this, >= 0.</summary>
        public float minContribution;
        /// <summary>even_threshold: a reason whose |contribution| (power points) is below this is "even" (neither side helped), >= 0.</summary>
        public float evenThreshold;
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
    /// All Story 004 tuning numbers, loaded from Assets/Data/match_config.json by <see cref="MatchConfigLoader"/>.
    /// Meaning: design/numbers.md 4.9, 4.10, 4.14. Mood factor, kappa_s, lambda and the lane weights are NOT here:
    /// they already live in <see cref="EsportsManager.Teams.TeamConfig"/> and are reused through TeamStrength so
    /// there is one source of truth; the decisive/hesitant and assertive coefficients live in the PlayerConfig trait
    /// effects ("decider_performance", "command").
    /// </summary>
    public sealed class MatchConfig
    {
        /// <summary>League size.</summary>
        public SeriesConfig series;
        /// <summary>Yearly calendar (numbers.md 4.18).</summary>
        public CalendarConfig calendar;
        /// <summary>Tournament formats, points, prizes (numbers.md 4.18).</summary>
        public FormatConfig format;
        /// <summary>Power weights.</summary>
        public PowerConfig power;
        /// <summary>Per-game noise.</summary>
        public NoiseConfig noise;
        /// <summary>Win probability.</summary>
        public WinProbabilityConfig winProbability;
        /// <summary>Tactic counters.</summary>
        public TacticConfig tactic;
        /// <summary>Opponent generation.</summary>
        public OpponentConfig opponent;
        /// <summary>Report selection.</summary>
        public ReportConfig report;
        /// <summary>Player club name rules.</summary>
        public ClubConfig club;

        /// <summary>Games a side must win a regular-season series: regular_best_of / 2 + 1.</summary>
        public int GamesToWin => GamesToWinOf(format.regularBestOf);

        /// <summary>Games needed to take a best-of-<paramref name="bestOf"/> series: bestOf / 2 + 1.</summary>
        public static int GamesToWinOf(int bestOf) => bestOf / 2 + 1;

        /// <summary>Clubs in the league: opponents + the player club.</summary>
        public int ClubCount => series.opponentCount + 1;

        /// <summary>True when <paramref name="attacker"/> counters <paramref name="defender"/> in the table.</summary>
        public bool Counters(MatchTactic attacker, MatchTactic defender)
        {
            foreach (var c in tactic.counters)
                if (c.tactic == attacker && c.beats == defender) return true;
            return false;
        }

        /// <summary>Checks every range. Throws <see cref="InvalidOperationException"/> naming the field.</summary>
        public void Validate()
        {
            if (series == null || calendar == null || format == null || power == null || noise == null || winProbability == null || tactic == null
                || opponent == null || report == null || club == null)
                throw Fail("a config section is missing.");

            if (series.opponentCount < 1) throw Fail("series.opponentCount must be >= 1, got " + series.opponentCount + ".");
            ValidateCalendar();
            ValidateFormat();

            NonNegative("power.weightSkill", power.weightSkill);
            NonNegative("power.weightChemistry", power.weightChemistry);
            NonNegative("power.weightStyle", power.weightStyle);
            if (!(power.weightSkill + power.weightChemistry + power.weightStyle > 0f)) throw Fail("power weights must have a positive total.");
            NonNegative("power.styleWeightCommand", power.styleWeightCommand);
            NonNegative("power.styleWeightLaneMatch", power.styleWeightLaneMatch);
            if (!(power.styleWeightCommand + power.styleWeightLaneMatch > 0f)) throw Fail("power style weights must have a positive total.");
            if (string.IsNullOrEmpty(power.commandDimensionId)) throw Fail("power.commandDimensionId must not be empty.");

            RandomRange("noise.sigma0", noise.sigma0);
            RandomRange("noise.sigma1", noise.sigma1);
            if (!(noise.sigma0 + noise.sigma1 < 1f)) throw Fail("noise.sigma0 + noise.sigma1 must be < 1 (power must stay positive).");

            if (!(winProbability.k > 0f) || float.IsInfinity(winProbability.k)) throw Fail("winProbability.k must be finite and > 0, got " + winProbability.k + ".");
            Range("winProbability.pMin", winProbability.pMin, 0f, 1f);
            Range("winProbability.pMax", winProbability.pMax, 0f, 1f);
            if (!(winProbability.pMin < winProbability.pMax)) throw Fail("winProbability.pMin must be < pMax.");

            NonNegative("tactic.counterBonus", tactic.counterBonus);
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

            Range("opponent.fixedMood", opponent.fixedMood, 0f, 100f);
            Range("opponent.fixedStress", opponent.fixedStress, 0f, 100f);
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

            NonNegative("opponent.formDown", opponent.formDown);
            NonNegative("opponent.formUpBase", opponent.formUpBase);
            NonNegative("opponent.formUpPop", opponent.formUpPop);
            NonNegative("opponent.formStep", opponent.formStep);
            Range("opponent.popMin", opponent.popMin, 0f, 100f);
            Range("opponent.popMax", opponent.popMax, 0f, 100f);
            if (opponent.popMin > opponent.popMax) throw Fail("opponent.popMin must be <= popMax.");

            if (report.minReasons < 1) throw Fail("report.minReasons must be >= 1, got " + report.minReasons + ".");
            if (report.maxReasons < report.minReasons || report.maxReasons > 5)
                throw Fail("report.maxReasons must be in [minReasons,5], got " + report.maxReasons + ".");
            NonNegative("report.minContribution", report.minContribution);
            NonNegative("report.evenThreshold", report.evenThreshold);
            if (club.nameMaxLength < 1 || club.nameMaxLength > 64) throw Fail("club.nameMaxLength must be in [1,64], got " + club.nameMaxLength + ".");
            if (string.IsNullOrWhiteSpace(club.defaultName) || club.defaultName.Trim() != club.defaultName || club.defaultName.Length > club.nameMaxLength)
                throw Fail("club.defaultName must be non-empty, trimmed and at most nameMaxLength characters.");
        }

        /// <summary>
        /// Checks the parts that depend on the player config: every lane has a dimension with id
        /// <c>power.commandDimensionId</c>. Throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public void ValidateAgainst(PlayerConfig players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            foreach (var lane in players.lanes)
                if (CommandIndex(lane) < 0)
                    throw Fail("power.commandDimensionId '" + power.commandDimensionId + "' is not a dimension of lane '" + lane.id + "'.");
        }

        /// <summary>Index of the command dimension in a lane, or -1.</summary>
        public int CommandIndex(LaneDef lane)
        {
            for (int i = 0; i < lane.dimensions.Length; i++)
                if (lane.dimensions[i].id == power.commandDimensionId) return i;
            return -1;
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
            if (v < 1 || v % 2 == 0) throw Fail(name + " must be odd and >= 1, got " + v + ".");
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

        private static void RandomRange(string name, float v)
        {
            if (!(v >= 0f && v < 1f)) throw Fail(name + " must be in [0,1), got " + v + ".");
        }
    }
}
