using System;
using EsportsManager.Players;

namespace EsportsManager.Teams
{
    /// <summary>One lane's weight in the team strength average (numbers.md 4.9 step 1).</summary>
    public sealed class LaneWeightDef
    {
        /// <summary>Lane id; must match a lane of the PlayerConfig.</summary>
        public string lane;
        /// <summary>Relative weight, >= 0.</summary>
        public float weight;
    }

    /// <summary>Strength tuning (numbers.md 4.9 step 1).</summary>
    public sealed class StrengthConfig
    {
        /// <summary>lambda: penalty per point of (mean effective - min effective). Range [0,1].</summary>
        public float shortBoardPenalty;
        /// <summary>kappa_s: effective score loses kappa_s x stress/100 x resolute multiplier. Range [0,1].</summary>
        public float stressFactor;
        /// <summary>Mood factor = base + span x mood/100 (numbers.md: 0.7 + 0.3 x mood/100).</summary>
        public float moodFactorBase;
        /// <summary>See <see cref="moodFactorBase"/>. base + span must be in (0,1].</summary>
        public float moodFactorSpan;
    }

    /// <summary>Pair familiarity and chemistry tuning (numbers.md 4.8).</summary>
    public sealed class FamiliarityConfig
    {
        /// <summary>f_seg: F gained per segment when both are starters (times the pair's gentle multiplier).</summary>
        public float perSegment;
        /// <summary>f_win: F gained per won match with both in the lineup.</summary>
        public float perWin;
        /// <summary>F_max: cap of F, in (0,100].</summary>
        public float max;
        /// <summary>f_decay: F lost per segment when the pair are not both starters. Must be > 0 so departed pairs reach 0 and are pruned.</summary>
        public float decay;
        /// <summary>kappa: chemistry C = clamp(F x (1 + kappa x H), 0, 100). Range [0,1].</summary>
        public float compatibilityFactor;
    }

    /// <summary>Player popularity tuning (numbers.md 4.2). Target E = WS of the six inputs (each 0-100).</summary>
    public sealed class PopularityConfig
    {
        /// <summary>alpha: share of the gap to the target closed per segment, in (0,1].</summary>
        public float alpha;
        /// <summary>r of R(r), uniform in [1-r, 1+r]; in [0,1).</summary>
        public float randomRange;
        /// <summary>WS weight of club popularity.</summary>
        public float weightClub;
        /// <summary>WS weight of the main-lane score.</summary>
        public float weightLaneScore;
        /// <summary>WS weight of looks.</summary>
        public float weightLooks;
        /// <summary>WS weight of stream heat (0-100, passed in).</summary>
        public float weightStream;
        /// <summary>WS weight of recent win rate (0-100).</summary>
        public float weightWinRate;
        /// <summary>WS weight of business heat (0-100, passed in).</summary>
        public float weightBusiness;
    }

    /// <summary>Club popularity tuning (numbers.md 4.3). Target E_club = WS of the three inputs (each 0-100).</summary>
    public sealed class ClubPopularityConfig
    {
        /// <summary>Starting club popularity of a new team, 0-100.</summary>
        public float initial;
        /// <summary>beta: share of the gap closed per segment, in (0,1].</summary>
        public float beta;
        /// <summary>r of R(r); in [0,1).</summary>
        public float randomRange;
        /// <summary>WS weight of the mean starter popularity.</summary>
        public float weightStarterPopularity;
        /// <summary>WS weight of recent win rate.</summary>
        public float weightWinRate;
        /// <summary>WS weight of the season rank score (0-100, passed in).</summary>
        public float weightRank;
    }

    /// <summary>Recent results tuning.</summary>
    public sealed class ResultsConfig
    {
        /// <summary>N: only the latest N win/loss records are kept; >= 1.</summary>
        public int window;
        /// <summary>Win rate (0-100) used when there are no records.</summary>
        public float defaultWinRate;
    }

    /// <summary>Livestream income helper tuning (numbers.md 4.10): per stream l_base + l_pop x popularity.</summary>
    public sealed class LivestreamConfig
    {
        /// <summary>l_base: yuan per stream, >= 0.</summary>
        public int baseIncome;
        /// <summary>l_pop: yuan per stream per popularity point, >= 0.</summary>
        public float incomePerPopularity;
        /// <summary>
        /// second_stream_factor (numbers.md 4.10 v3.1): income multiplier for every stream after the first by the same
        /// player in the same segment, in [0, 1].
        /// </summary>
        public float secondStreamFactor;
    }

    /// <summary>
    /// All Story 002 tuning numbers, loaded from Assets/Data/team_config.json by <see cref="TeamConfigLoader"/>.
    /// Meaning of every number: design/numbers.md 4.2, 4.3, 4.8, 4.9 step 1, 4.10. <c>segments_per_year</c>
    /// is deliberately NOT here: the yearly calendar owns it and passes it in at settlement.
    /// </summary>
    public sealed class TeamConfig
    {
        /// <summary>Lane weights of the strength average; exactly the lanes of the PlayerConfig.</summary>
        public LaneWeightDef[] laneWeights;
        /// <summary>Strength tuning.</summary>
        public StrengthConfig strength;
        /// <summary>Familiarity and chemistry tuning.</summary>
        public FamiliarityConfig familiarity;
        /// <summary>Player popularity tuning.</summary>
        public PopularityConfig popularity;
        /// <summary>Club popularity tuning.</summary>
        public ClubPopularityConfig clubPopularity;
        /// <summary>Recent results tuning.</summary>
        public ResultsConfig results;
        /// <summary>Livestream income tuning.</summary>
        public LivestreamConfig livestream;

        /// <summary>Weight of a lane in the strength average. Throws ArgumentException for an unknown lane.</summary>
        public float GetLaneWeight(string laneId)
        {
            if (laneWeights != null)
                foreach (var w in laneWeights)
                    if (w.lane == laneId) return w.weight;
            throw new ArgumentException("No lane weight for lane: " + laneId, nameof(laneId));
        }

        /// <summary>
        /// Checks every range. Throws <see cref="InvalidOperationException"/> naming the field.
        /// </summary>
        public void Validate()
        {
            if (laneWeights == null || laneWeights.Length == 0) throw Fail("laneWeights must not be empty.");
            float sum = 0f;
            for (int i = 0; i < laneWeights.Length; i++)
            {
                var w = laneWeights[i] ?? throw Fail("laneWeights[" + i + "] is null.");
                if (string.IsNullOrEmpty(w.lane)) throw Fail("laneWeights[" + i + "].lane must not be empty.");
                for (int j = 0; j < i; j++)
                    if (laneWeights[j].lane == w.lane) throw Fail("duplicate lane in laneWeights: " + w.lane + ".");
                NonNegative("laneWeights[" + i + "].weight", w.weight);
                sum += w.weight;
            }
            if (!(sum > 0f)) throw Fail("laneWeights must have a positive total.");

            if (strength == null || familiarity == null || popularity == null || clubPopularity == null || results == null || livestream == null)
                throw Fail("a config section is missing.");

            Range("strength.shortBoardPenalty", strength.shortBoardPenalty, 0f, 1f);
            Range("strength.stressFactor", strength.stressFactor, 0f, 1f);
            Range("strength.moodFactorBase", strength.moodFactorBase, 0f, 1f);
            NonNegative("strength.moodFactorSpan", strength.moodFactorSpan);
            float top = strength.moodFactorBase + strength.moodFactorSpan;
            if (!(top > 0f && top <= 1f)) throw Fail("strength.moodFactorBase + moodFactorSpan must be in (0,1], got " + top + ".");

            NonNegative("familiarity.perSegment", familiarity.perSegment);
            NonNegative("familiarity.perWin", familiarity.perWin);
            NonNegative("familiarity.decay", familiarity.decay);
            if (familiarity.decay <= 0f) throw Fail("familiarity.decay must be > 0 (otherwise departed pairs are never pruned).");
            Range("familiarity.max", familiarity.max, 0f, 100f);
            if (familiarity.max <= 0f) throw Fail("familiarity.max must be > 0.");
            Range("familiarity.compatibilityFactor", familiarity.compatibilityFactor, 0f, 1f);

            var p = popularity;
            Range("popularity.alpha", p.alpha, 0f, 1f);
            if (p.alpha <= 0f) throw Fail("popularity.alpha must be > 0.");
            RandomRange("popularity.randomRange", p.randomRange);
            NonNegative("popularity.weightClub", p.weightClub);
            NonNegative("popularity.weightLaneScore", p.weightLaneScore);
            NonNegative("popularity.weightLooks", p.weightLooks);
            NonNegative("popularity.weightStream", p.weightStream);
            NonNegative("popularity.weightWinRate", p.weightWinRate);
            NonNegative("popularity.weightBusiness", p.weightBusiness);
            if (!(p.weightClub + p.weightLaneScore + p.weightLooks + p.weightStream + p.weightWinRate + p.weightBusiness > 0f))
                throw Fail("popularity weights must have a positive total.");

            var c = clubPopularity;
            Range("clubPopularity.initial", c.initial, 0f, 100f);
            Range("clubPopularity.beta", c.beta, 0f, 1f);
            if (c.beta <= 0f) throw Fail("clubPopularity.beta must be > 0.");
            RandomRange("clubPopularity.randomRange", c.randomRange);
            NonNegative("clubPopularity.weightStarterPopularity", c.weightStarterPopularity);
            NonNegative("clubPopularity.weightWinRate", c.weightWinRate);
            NonNegative("clubPopularity.weightRank", c.weightRank);
            if (!(c.weightStarterPopularity + c.weightWinRate + c.weightRank > 0f))
                throw Fail("clubPopularity weights must have a positive total.");

            if (results.window < 1) throw Fail("results.window must be >= 1, got " + results.window + ".");
            Range("results.defaultWinRate", results.defaultWinRate, 0f, 100f);

            if (livestream.baseIncome < 0) throw Fail("livestream.baseIncome must be >= 0, got " + livestream.baseIncome + ".");
            NonNegative("livestream.incomePerPopularity", livestream.incomePerPopularity);
            Range("livestream.secondStreamFactor", livestream.secondStreamFactor, 0f, 1f);
        }

        /// <summary>Checks that the lane weights cover exactly the lanes of <paramref name="players"/>.</summary>
        public void ValidateAgainst(PlayerConfig players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            foreach (var lane in players.lanes) GetLaneWeight(lane.id); // throws ArgumentException when missing
            foreach (var w in laneWeights)
                if (players.FindLane(w.lane) == null)
                    throw Fail("laneWeights names unknown lane '" + w.lane + "' (not in the PlayerConfig).");
        }

        private static InvalidOperationException Fail(string msg) => new InvalidOperationException("TeamConfig: " + msg);

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
