using System.Collections.Generic;

namespace EsportsManager.Segments
{
    /// <summary>Why a plan is invalid (no localized text: the UI maps codes to strings).</summary>
    public enum PlanErrorCode
    {
        /// <summary>The plan is valid.</summary>
        None = 0,
        /// <summary>The plan is null.</summary>
        PlanNull,
        /// <summary>No open segment (call BeginSegment).</summary>
        SegmentNotOpen,
        /// <summary>Context SegmentsPerYear is not > 0.</summary>
        BadSegmentsPerYear,
        /// <summary>Context SegmentIndexInYear outside [0, SegmentsPerYear).</summary>
        BadSegmentIndex,
        /// <summary>Context SeasonRankScore outside [0, 100].</summary>
        BadSeasonRankScore,
        /// <summary>Context PrizeMoney negative.</summary>
        NegativePrizeMoney,
        /// <summary>A rostered player has a negative salary (PlayerId set).</summary>
        NegativeSalary,
        /// <summary>SponsorPoints negative.</summary>
        NegativeSponsorPoints,
        /// <summary>An assignment names a player who is not rostered (PlayerId set).</summary>
        UnknownPlayer,
        /// <summary>An assignment has an undefined action (PlayerId set).</summary>
        UnknownAction,
        /// <summary>A training focus dimension is not in the trained phase (PlayerId, DimensionIndex = the focus dimension).</summary>
        DimensionOutOfRange,
        /// <summary>An accepted offer id is not pending (OfferId set).</summary>
        UnknownOffer,
        /// <summary>An offer is accepted twice (OfferId set).</summary>
        DuplicateOffer,
        /// <summary>An accepted offer's player left the roster (OfferId, PlayerId set).</summary>
        OfferPlayerLeft,
        /// <summary>A player gets more than max_per_player points (PlayerId, Used, Limit).</summary>
        PlayerOverLimit,
        /// <summary>The plan spends more than AP points (Used, Limit).</summary>
        OverTotalPoints,
        /// <summary>
        /// More Sponsor points than allowed this segment (Used = sponsor points, Limit = min(max_attempts_per_segment,
        /// free contract slots)).
        /// </summary>
        SponsorAttemptsOverLimit,
        /// <summary>Sponsor points while every contract slot is taken (Used = active contracts, Limit = max_active_contracts).</summary>
        ContractsFull,
        /// <summary>Story 004: an event stage needs one starter in every lane (Used = starters, Limit = lanes).</summary>
        IncompleteLineup,
        /// <summary>Story 004: the tactic is not a defined <c>MatchTactic</c>.</summary>
        UnknownTactic,
        /// <summary>Story 004 (numbers.md 4.18): an event is in progress; finish and acknowledge it before the next segment.</summary>
        EventPending,
        /// <summary>Story 004: this event step is not possible in the current event phase.</summary>
        WrongEventPhase,
        /// <summary>Story 004: lineup / tactic can only change while planning a segment or before an event stage.</summary>
        LineupLocked,
        /// <summary>Story 006: a choice answer names a choice that is not pending (ChoiceId set).</summary>
        UnknownChoice,
        /// <summary>Story 006: a choice answer's option is out of range (ChoiceId, Limit = option count).</summary>
        ChoiceOptionOutOfRange,
        /// <summary>Story 006: the player is on leave by a choice answer (or default) and may get no action point (PlayerId, ChoiceId).</summary>
        PlayerOnLeave,
        /// <summary>Story 012: the training target is not a defined phase / command (PlayerId).</summary>
        UnknownTrainingTarget,
        /// <summary>Story 012: training needs the growth system, which needs the match phase mapping (session without a match config).</summary>
        TrainingUnavailable,
    }

    /// <summary>Machine-readable plan error. Fields that do not apply to the code are null.</summary>
    public struct PlanError
    {
        /// <summary>What is wrong (None = valid).</summary>
        public PlanErrorCode Code;
        /// <summary>The player involved, if any.</summary>
        public long? PlayerId;
        /// <summary>The offer involved, if any.</summary>
        public int? OfferId;
        /// <summary>The choice event involved, if any (Story 006).</summary>
        public int? ChoiceId;
        /// <summary>The training focus dimension involved, if any.</summary>
        public int? DimensionIndex;
        /// <summary>Points used (limit errors).</summary>
        public int? Used;
        /// <summary>The limit that was exceeded (points, or dimension count).</summary>
        public int? Limit;

        /// <summary>True when <see cref="Code"/> is None.</summary>
        public bool IsNone => Code == PlanErrorCode.None;
    }

    /// <summary>Deterministic per-player estimate of a draft plan (all random factors at 1).</summary>
    public sealed class PlayerPreview
    {
        /// <summary>Player.Id.</summary>
        public long PlayerId;
        /// <summary>Points the draft gives him (train + stream + rest + accepted offers).</summary>
        public int PointsUsed;
        /// <summary>max_per_player - PointsUsed (negative when over the limit).</summary>
        public int PointsRemaining;
        /// <summary>Training points.</summary>
        public int TrainPoints;
        /// <summary>Stream points.</summary>
        public int StreamPoints;
        /// <summary>Rest points.</summary>
        public int RestPoints;
        /// <summary>Accepted offers.</summary>
        public int BusinessPoints;
        /// <summary>Total training gain per main-lane dimension at noise 1 (before the ceiling); all zero without training.</summary>
        public float[] TrainingGains;
        /// <summary>Same at the lowest noise (x (1 - noise), growth.md "训练预估" range); event modifiers are not known in advance.</summary>
        public float[] TrainingGainsLow;
        /// <summary>Same at the highest noise (x (1 + noise)).</summary>
        public float[] TrainingGainsHigh;
        /// <summary>Phase focus after this segment's training (F3), early / mid / late.</summary>
        public float[] FocusAfter;
        /// <summary>
        /// Stress change at R = 1, before clamping (includes match terms from the context and recovery). With a league: a starter
        /// is counted as playing every scheduled series, losses unknown (0).
        /// </summary>
        public float StressDelta;
        /// <summary>
        /// Mood change at R = 1, before clamping. Approximate: salary satisfaction uses today's lane score, while Resolve
        /// uses the score after this segment's training.
        /// </summary>
        public float MoodDelta;
        /// <summary>Livestream income his streams would earn now (Team.LivestreamIncome).</summary>
        public int LivestreamIncome;
    }

    /// <summary>Result of <see cref="SegmentRunner.Preview"/>: what a draft plan would do, without randomness or side effects.</summary>
    public sealed class SegmentPreview
    {
        /// <summary>First validation problem of the draft (None when it would resolve).</summary>
        public PlanError Error;
        /// <summary>Points the draft spends.</summary>
        public int PointsUsed;
        /// <summary>AP - PointsUsed.</summary>
        public int PointsRemaining;
        /// <summary>One entry per rostered player, roster order. Assignments naming unknown players are skipped.</summary>
        public List<PlayerPreview> Players = new List<PlayerPreview>();
        /// <summary>Sponsorship attempts in the draft.</summary>
        public int SponsorPoints;
        /// <summary>Sponsor points still allowed after this draft (min(max attempts, free slots) - SponsorPoints; negative = over).</summary>
        public int SponsorAttemptsRemaining;
        /// <summary>Contract slots free at segment start (max_active_contracts - active contracts).</summary>
        public int FreeContractSlots;
        /// <summary>
        /// Success chance of each attempt BEFORE the U(-spread, +spread) noise (still clamped). Uses the win rate including the
        /// context's MatchResults; with a league the real chance uses the post-match rate, unknown here, so this shows the current rate.
        /// </summary>
        public float SponsorChanceBeforeNoise;
        /// <summary>Pending offers the draft accepts (each carries ClubAmount).</summary>
        public List<BusinessOffer> AcceptedOffers = new List<BusinessOffer>();
        /// <summary>Sum of ClubAmount of the accepted offers.</summary>
        public int BusinessIncome;
        /// <summary>Sum of the players' livestream income.</summary>
        public int LivestreamIncome;
    }
}
