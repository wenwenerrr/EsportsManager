using System;

namespace EsportsManager.Segments
{
    /// <summary>Shared action points (design/numbers.md 4.16).</summary>
    public sealed class ActionPointConfig
    {
        /// <summary>AP: shared action points per segment, >= 1.</summary>
        public int perSegment;
        /// <summary>max_per_player: most points one player may receive per segment, >= 1.</summary>
        public int maxPerPlayer;
    }

    /// <summary>
    /// Training knobs of the segment (numbers.md 4.16). The growth formulas and their numbers live in PlayerConfig.training
    /// (design/gdd/growth.md); the old target_bonus was removed with the "six dims + target x1.5" model.
    /// </summary>
    public sealed class SegmentTrainingConfig
    {
        /// <summary>I of growth.md F1 and the stress formula's training intensity per point, > 0. Observation values come from PlayerConfig.potential.</summary>
        public float intensity;
    }

    /// <summary>Livestream knobs (numbers.md 4.2).</summary>
    public sealed class StreamConfig
    {
        /// <summary>stream_cap: streams x trait multiplier at or above this give heat 100. > 0.</summary>
        public float cap;
    }

    /// <summary>Business pop-up knobs (numbers.md 4.11, 4.10).</summary>
    public sealed class BusinessConfig
    {
        /// <summary>WS weight of popularity (>= 0).</summary>
        public float weightPopularity;
        /// <summary>WS weight of the main-lane score (>= 0).</summary>
        public float weightLaneScore;
        /// <summary>WS weight of looks (>= 0).</summary>
        public float weightLooks;
        /// <summary>p_max: trigger probability cap, in (0, 1].</summary>
        public float maxProbability;
        /// <summary>a0: base offer amount, yuan, >= 0.</summary>
        public int baseAmount;
        /// <summary>a1: yuan per popularity point, >= 0.</summary>
        public float amountPerPopularity;
        /// <summary>a2: yuan per looks point, >= 0.</summary>
        public float amountPerLooks;
        /// <summary>R(r) range of the amount, in [0, 1).</summary>
        public float randomRange;
        /// <summary>Share of an accepted deal the club keeps, in [0, 1].</summary>
        public float clubShare;
        /// <summary>Accepted deals that give business heat 100 (numbers.md 4.2), > 0.</summary>
        public float heatCap;
    }

    /// <summary>Sponsorship knobs (numbers.md 4.12).</summary>
    public sealed class SponsorshipConfig
    {
        /// <summary>WS weight of club popularity (>= 0).</summary>
        public float weightClubPopularity;
        /// <summary>WS weight of the recent win rate (>= 0).</summary>
        public float weightWinRate;
        /// <summary>z_max, in (0, 1].</summary>
        public float maxSuccessRate;
        /// <summary>Half-width of the uniform noise U(-s, +s) added to the chance, in [0, 1].</summary>
        public float randomSpread;
        /// <summary>Lower clamp of the chance, in [0, 1].</summary>
        public float minChance;
        /// <summary>Upper clamp of the chance, in [minChance, 1].</summary>
        public float maxChance;
        /// <summary>c0: base contract amount per segment, yuan, >= 0.</summary>
        public int baseAmount;
        /// <summary>c1: yuan per club popularity point, >= 0.</summary>
        public float amountPerClubPopularity;
        /// <summary>N: segments a contract pays, >= 1.</summary>
        public int durationSegments;
        /// <summary>Most Sponsor points per segment, failed attempts included (numbers.md 4.12 v3.2), >= 1.</summary>
        public int maxAttemptsPerSegment;
        /// <summary>Most contracts paying at the same time (numbers.md 4.12 v3.2), >= 1.</summary>
        public int maxActiveContracts;
    }

    /// <summary>Mood formula constants (numbers.md 4.6).</summary>
    public sealed class MoodConfig
    {
        /// <summary>m_result.</summary>
        public float result;
        /// <summary>m_play.</summary>
        public float play;
        /// <summary>m_salary.</summary>
        public float salary;
        /// <summary>m_chem.</summary>
        public float chemistry;
        /// <summary>m_stress.</summary>
        public float stress;
        /// <summary>gamma, in [0, 1].</summary>
        public float regression;
        /// <summary>The value mood regresses to, in [0, 100] (50 in the design).</summary>
        public float neutral;
        /// <summary>Stress above this counts as high stress, in [0, 100] (70 in the design).</summary>
        public float highStressThreshold;
        /// <summary>R(r) range, in [0, 1).</summary>
        public float randomRange;
    }

    /// <summary>Stress formula constants (numbers.md 4.7).</summary>
    public sealed class StressConfig
    {
        /// <summary>s_train per unit of training intensity.</summary>
        public float training;
        /// <summary>s_match per match played (starters).</summary>
        public float match;
        /// <summary>s_loss per match lost (starters).</summary>
        public float loss;
        /// <summary>s_biz per accepted business deal.</summary>
        public float business;
        /// <summary>s_rest per rest point (reduces stress).</summary>
        public float rest;
        /// <summary>s_live per stream (reduces stress).</summary>
        public float stream;
        /// <summary>s_recover (numbers.md 4.7 v3): natural recovery subtracted every segment for every rostered player, outside R. >= 0.</summary>
        public float recovery;
        /// <summary>R(r) range, in [0, 1).</summary>
        public float randomRange;
    }

    /// <summary>
    /// All Story 003 tuning numbers (Assets/Data/segment_config.json). Plain data, filled by
    /// <see cref="SegmentConfigLoader"/> or built in code for tests; call <see cref="Validate"/> before use.
    /// </summary>
    public sealed class SegmentConfig
    {
        /// <summary>Shared action points.</summary>
        public ActionPointConfig actionPoints;
        /// <summary>Training.</summary>
        public SegmentTrainingConfig training;
        /// <summary>Livestream.</summary>
        public StreamConfig stream;
        /// <summary>Business pop-ups.</summary>
        public BusinessConfig business;
        /// <summary>Sponsorship.</summary>
        public SponsorshipConfig sponsorship;
        /// <summary>Mood.</summary>
        public MoodConfig mood;
        /// <summary>Stress.</summary>
        public StressConfig stress;

        /// <summary>Checks every range. Throws <see cref="InvalidOperationException"/> naming the field.</summary>
        public void Validate()
        {
            if (actionPoints == null || training == null || stream == null || business == null || sponsorship == null
                || mood == null || stress == null)
                throw Fail("a config section is missing.");

            if (actionPoints.perSegment < 1) throw Fail("actionPoints.perSegment must be >= 1, got " + actionPoints.perSegment + ".");
            if (actionPoints.maxPerPlayer < 1) throw Fail("actionPoints.maxPerPlayer must be >= 1, got " + actionPoints.maxPerPlayer + ".");
            Positive("training.intensity", training.intensity);
            Positive("stream.cap", stream.cap);

            var b = business;
            NonNegative("business.weightPopularity", b.weightPopularity);
            NonNegative("business.weightLaneScore", b.weightLaneScore);
            NonNegative("business.weightLooks", b.weightLooks);
            if (!(b.weightPopularity + b.weightLaneScore + b.weightLooks > 0f)) throw Fail("business weights must have a positive total.");
            Range("business.maxProbability", b.maxProbability, 0f, 1f);
            if (b.maxProbability <= 0f) throw Fail("business.maxProbability must be > 0.");
            if (b.baseAmount < 0) throw Fail("business.baseAmount must be >= 0, got " + b.baseAmount + ".");
            NonNegative("business.amountPerPopularity", b.amountPerPopularity);
            NonNegative("business.amountPerLooks", b.amountPerLooks);
            RandomRange("business.randomRange", b.randomRange);
            Range("business.clubShare", b.clubShare, 0f, 1f);
            Positive("business.heatCap", b.heatCap);

            var s = sponsorship;
            NonNegative("sponsorship.weightClubPopularity", s.weightClubPopularity);
            NonNegative("sponsorship.weightWinRate", s.weightWinRate);
            if (!(s.weightClubPopularity + s.weightWinRate > 0f)) throw Fail("sponsorship weights must have a positive total.");
            Range("sponsorship.maxSuccessRate", s.maxSuccessRate, 0f, 1f);
            if (s.maxSuccessRate <= 0f) throw Fail("sponsorship.maxSuccessRate must be > 0.");
            Range("sponsorship.randomSpread", s.randomSpread, 0f, 1f);
            Range("sponsorship.minChance", s.minChance, 0f, 1f);
            Range("sponsorship.maxChance", s.maxChance, 0f, 1f);
            if (s.minChance > s.maxChance) throw Fail("sponsorship.minChance must be <= maxChance.");
            if (s.baseAmount < 0) throw Fail("sponsorship.baseAmount must be >= 0, got " + s.baseAmount + ".");
            NonNegative("sponsorship.amountPerClubPopularity", s.amountPerClubPopularity);
            if (s.durationSegments < 1) throw Fail("sponsorship.durationSegments must be >= 1, got " + s.durationSegments + ".");
            if (s.maxAttemptsPerSegment < 1) throw Fail("sponsorship.maxAttemptsPerSegment must be >= 1, got " + s.maxAttemptsPerSegment + ".");
            if (s.maxActiveContracts < 1) throw Fail("sponsorship.maxActiveContracts must be >= 1, got " + s.maxActiveContracts + ".");

            var m = mood;
            NonNegative("mood.result", m.result);
            NonNegative("mood.play", m.play);
            NonNegative("mood.salary", m.salary);
            NonNegative("mood.chemistry", m.chemistry);
            NonNegative("mood.stress", m.stress);
            Range("mood.regression", m.regression, 0f, 1f);
            Range("mood.neutral", m.neutral, 0f, 100f);
            Range("mood.highStressThreshold", m.highStressThreshold, 0f, 100f);
            RandomRange("mood.randomRange", m.randomRange);

            var t = stress;
            NonNegative("stress.training", t.training);
            NonNegative("stress.match", t.match);
            NonNegative("stress.loss", t.loss);
            NonNegative("stress.business", t.business);
            NonNegative("stress.rest", t.rest);
            NonNegative("stress.stream", t.stream);
            NonNegative("stress.recovery", t.recovery);
            RandomRange("stress.randomRange", t.randomRange);
        }

        private static InvalidOperationException Fail(string msg) => new InvalidOperationException("SegmentConfig: " + msg);

        private static void NonNegative(string name, float v)
        {
            if (!(v >= 0f) || float.IsInfinity(v)) throw Fail(name + " must be finite and >= 0, got " + v + ".");
        }

        private static void Positive(string name, float v)
        {
            if (!(v > 0f) || float.IsInfinity(v)) throw Fail(name + " must be finite and > 0, got " + v + ".");
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
