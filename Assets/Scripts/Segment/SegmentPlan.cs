using System;
using System.Collections.Generic;

namespace EsportsManager.Segments
{
    /// <summary>The per-player actions that cost one shared action point (numbers.md 4.16).</summary>
    public enum SegmentAction
    {
        /// <summary>Train one main-lane dimension.</summary>
        Train = 0,
        /// <summary>Livestream.</summary>
        Stream = 1,
        /// <summary>Rest (lowers stress).</summary>
        Rest = 2,
    }

    /// <summary>One action point given to one player.</summary>
    public readonly struct ActionAssignment
    {
        /// <summary>Player.Id of the receiver.</summary>
        public readonly long PlayerId;
        /// <summary>What he does.</summary>
        public readonly SegmentAction Action;
        /// <summary>Main-lane dimension index; only used for <see cref="SegmentAction.Train"/>, 0 otherwise.</summary>
        public readonly int DimensionIndex;

        /// <summary>Creates an assignment.</summary>
        public ActionAssignment(long playerId, SegmentAction action, int dimensionIndex = 0)
        {
            PlayerId = playerId; Action = action; DimensionIndex = dimensionIndex;
        }
    }

    /// <summary>
    /// What the player decided for one segment: every entry costs exactly one shared action point.
    /// Plain data; <see cref="SegmentRunner"/> validates it. Mutable so a UI can edit it while the player decides.
    /// </summary>
    /// <example><code>var plan = new SegmentPlan();
    /// plan.Train(p.Id, 0).Train(p.Id, 0).Stream(q.Id).Sponsor().AcceptOffer(state.PendingOffers[0].Id);</code></example>
    public sealed class SegmentPlan
    {
        private readonly List<ActionAssignment> _assignments = new List<ActionAssignment>();
        private readonly List<int> _acceptedOffers = new List<int>();
        private readonly List<ChoiceAnswer> _choiceAnswers = new List<ChoiceAnswer>();

        /// <summary>The per-player action points, in the order added.</summary>
        public IReadOnlyList<ActionAssignment> Assignments => _assignments;
        /// <summary>Ids of the business offers the player accepts (each costs one point on the offer's player).</summary>
        public IReadOnlyList<int> AcceptedOfferIds => _acceptedOffers;
        /// <summary>Answers to pending choice events (Story 006), one per choice id; unanswered choices take their default option.</summary>
        public IReadOnlyList<ChoiceAnswer> ChoiceAnswers => _choiceAnswers;
        /// <summary>Number of "Sponsor" club-action points (>= 0).</summary>
        public int SponsorPoints { get; set; }

        /// <summary>Total points this plan spends.</summary>
        public int PointsUsed => _assignments.Count + _acceptedOffers.Count + SponsorPoints;

        /// <summary>Adds a training point on a main-lane dimension.</summary>
        public SegmentPlan Train(long playerId, int dimensionIndex) { _assignments.Add(new ActionAssignment(playerId, SegmentAction.Train, dimensionIndex)); return this; }
        /// <summary>Adds a stream point.</summary>
        public SegmentPlan Stream(long playerId) { _assignments.Add(new ActionAssignment(playerId, SegmentAction.Stream)); return this; }
        /// <summary>Adds a rest point.</summary>
        public SegmentPlan Rest(long playerId) { _assignments.Add(new ActionAssignment(playerId, SegmentAction.Rest)); return this; }
        /// <summary>Adds one sponsorship attempt.</summary>
        public SegmentPlan Sponsor() { SponsorPoints++; return this; }
        /// <summary>Accepts a pending business offer by id.</summary>
        public SegmentPlan AcceptOffer(int offerId) { _acceptedOffers.Add(offerId); return this; }
        /// <summary>Answers a pending choice event (replaces an earlier answer to the same choice). Costs no action point.</summary>
        public SegmentPlan AnswerChoice(int choiceId, int option)
        {
            _choiceAnswers.RemoveAll(a => a.ChoiceId == choiceId);
            _choiceAnswers.Add(new ChoiceAnswer(choiceId, option));
            return this;
        }
        /// <summary>Removes the answer to a choice (it falls back to its default option).</summary>
        public SegmentPlan ClearChoiceAnswer(int choiceId) { _choiceAnswers.RemoveAll(a => a.ChoiceId == choiceId); return this; }
        /// <summary>Removes everything.</summary>
        public void Clear() { _assignments.Clear(); _acceptedOffers.Clear(); _choiceAnswers.Clear(); SponsorPoints = 0; }
    }

    /// <summary>The option picked for one pending choice event (Story 006).</summary>
    public readonly struct ChoiceAnswer
    {
        /// <summary>PendingChoice.Id.</summary>
        public readonly int ChoiceId;
        /// <summary>Option index.</summary>
        public readonly int Option;
        /// <summary>Creates an answer.</summary>
        public ChoiceAnswer(int choiceId, int option) { ChoiceId = choiceId; Option = option; }
    }

    /// <summary>The non-gameplay inputs of a segment: calendar (Story 005), matches (Story 004), prize money.</summary>
    public struct SegmentContext
    {
        /// <summary>Segments per year (owned by the calendar), must be > 0.</summary>
        public int SegmentsPerYear;
        /// <summary>0-based index inside the year, in [0, SegmentsPerYear).</summary>
        public int SegmentIndexInYear;
        /// <summary>Legacy / test input: matches played this segment (true = win); null = none (the calendar plays matches in events, numbers.md 4.18, so the session always passes null).</summary>
        public IReadOnlyList<bool> MatchResults;
        /// <summary>Season rank score 0-100.</summary>
        public float SeasonRankScore;
        /// <summary>Prize money, >= 0.</summary>
        public int PrizeMoney;
    }

    /// <summary>A business pop-up rolled at the start of a segment (numbers.md 4.11).</summary>
    public readonly struct BusinessOffer
    {
        /// <summary>Unique for the runner's lifetime: 0, 1, 2 ... in roll order, never reused across segments.</summary>
        public readonly int Id;
        /// <summary>The player the offer is for.</summary>
        public readonly long PlayerId;
        /// <summary>Total deal amount in yuan.</summary>
        public readonly int Amount;
        /// <summary>What the club receives if accepted (Amount x club_share, whole yuan).</summary>
        public readonly int ClubAmount;

        /// <summary>Creates an offer.</summary>
        public BusinessOffer(int id, long playerId, int amount, int clubAmount)
        {
            Id = id; PlayerId = playerId; Amount = amount; ClubAmount = clubAmount;
        }
    }

    /// <summary>A sponsorship contract paying a fixed amount each segment until it runs out (numbers.md 4.12).</summary>
    public sealed class SponsorContract
    {
        /// <summary>Yuan paid each segment.</summary>
        public int AmountPerSegment { get; }
        /// <summary>Segments still to pay, including the next one. The contract is removed at 0.</summary>
        public int SegmentsRemaining { get; internal set; }

        /// <summary>Creates a contract (amount >= 0, segments >= 1).</summary>
        public SponsorContract(int amountPerSegment, int segmentsRemaining)
        {
            if (amountPerSegment < 0) throw new ArgumentOutOfRangeException(nameof(amountPerSegment), "must be >= 0.");
            if (segmentsRemaining < 1) throw new ArgumentOutOfRangeException(nameof(segmentsRemaining), "must be >= 1.");
            AmountPerSegment = amountPerSegment; SegmentsRemaining = segmentsRemaining;
        }
    }

    /// <summary>Read-only snapshot of the segment for a UI to poll.</summary>
    public sealed class SegmentState
    {
        /// <summary>True between BeginSegment and a successful Resolve.</summary>
        public bool IsOpen;
        /// <summary>AP: points available this segment (unused points do not carry over).</summary>
        public int ActionPoints;
        /// <summary>Most points per player.</summary>
        public int MaxPerPlayer;
        /// <summary>Offers rolled at the start of the open segment whose player is still rostered (empty when not open).</summary>
        public IReadOnlyList<BusinessOffer> PendingOffers;
        /// <summary>Serial of the current/last segment: 0 before the first BeginSegment, +1 at every BeginSegment.</summary>
        public int Serial;
        /// <summary>Summary of the last resolved segment; null before the first Resolve.</summary>
        public SegmentSummary LastSummary;
        /// <summary>Contracts currently paying, newest last.</summary>
        public IReadOnlyList<SponsorContract> ActiveContracts;
        /// <summary>Contract slots free (max_active_contracts - active contracts).</summary>
        public int FreeContractSlots;
        /// <summary>Sponsor points a plan may use this segment: min(max_attempts_per_segment, FreeContractSlots).</summary>
        public int SponsorAttemptsRemaining;
        /// <summary>Choice events of the open segment (Story 006), empty when not open or without an event engine.</summary>
        public IReadOnlyList<EsportsManager.Events.PendingChoice> PendingChoices = new EsportsManager.Events.PendingChoice[0];
    }
}
