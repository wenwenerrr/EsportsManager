using System.Collections.Generic;
using EsportsManager.Teams;

namespace EsportsManager.Segments
{
    /// <summary>
    /// One training point (design/gdd/growth.md rev 3): the base amount b (F1 with its noise and the F5 event modifiers) and the
    /// gains it gave every main-lane dimension (F2, from SEGMENT-START values; all points are summed and applied once).
    /// </summary>
    public struct TrainingResult
    {
        /// <summary>Trained phase or command.</summary>
        public EsportsManager.Growth.TrainingTarget Target;
        /// <summary>Focus dimension (main-lane index), -1 = none.</summary>
        public int FocusDimension;
        /// <summary>The noise factor drawn for this point (1 without a noise source).</summary>
        public float Noise;
        /// <summary>Base amount b after noise and event modifiers.</summary>
        public float Base;
        /// <summary>Gain of every main-lane dimension, by index (before the ceiling clamp).</summary>
        public float[] Gains;
    }

    /// <summary>What happened to one rostered player this segment (all deltas are actual, after clamping).</summary>
    public sealed class PlayerSegmentSummary
    {
        /// <summary>Player.Id.</summary>
        public long PlayerId;
        /// <summary>Training points received.</summary>
        public int TrainPoints;
        /// <summary>Stream points received.</summary>
        public int StreamPoints;
        /// <summary>Rest points received.</summary>
        public int RestPoints;
        /// <summary>Accepted business deals.</summary>
        public int BusinessPoints;
        /// <summary>One entry per training point, plan order.</summary>
        public List<TrainingResult> Training = new List<TrainingResult>();
        /// <summary>Main-lane dimension values before / after this segment's training (null without training).</summary>
        public float[] ValuesBefore, ValuesAfter;
        /// <summary>Phase focus before / after the segment (early, mid, late).</summary>
        public float[] FocusBefore, FocusAfter;
        /// <summary>Specialties gained at the segment-end check (ids).</summary>
        public List<string> SpecialtiesGained = new List<string>();
        /// <summary>Specialties lost at the segment-end check (ids).</summary>
        public List<string> SpecialtiesLost = new List<string>();
        /// <summary>Stream heat 0-100 sent to Team.SettleSegment.</summary>
        public float StreamHeat;
        /// <summary>Business heat 0-100 sent to Team.SettleSegment.</summary>
        public float BusinessHeat;
        /// <summary>Livestream income this player generated, yuan.</summary>
        public int LivestreamIncome;
        /// <summary>Mood after minus before.</summary>
        public float MoodDelta;
        /// <summary>Stress after minus before.</summary>
        public float StressDelta;
        /// <summary>Popularity after minus before (changed by Team.SettleSegment).</summary>
        public float PopularityDelta;
        /// <summary>Observation after minus before.</summary>
        public float ObservationDelta;
        /// <summary>Series he played this segment (legacy context MatchResults, starters only; 0 in the calendar flow).</summary>
        public int MatchesPlayed;
        /// <summary>Of those, series lost.</summary>
        public int MatchesLost;
    }

    /// <summary>One sponsorship attempt.</summary>
    public struct SponsorAttempt
    {
        /// <summary>The success chance that was rolled against (after noise and clamping).</summary>
        public float Chance;
        /// <summary>True when a contract was won.</summary>
        public bool Success;
        /// <summary>Story 006: an event voided the attempt (no draw, no chance; the point was wasted).</summary>
        public bool Voided;
    }

    /// <summary>Everything one segment did: for the UI and for tests.</summary>
    public sealed class SegmentSummary
    {
        /// <summary>Points spent.</summary>
        public int PointsUsed;
        /// <summary>Points left unspent (lost).</summary>
        public int PointsUnused;
        /// <summary>One entry per rostered player, roster order.</summary>
        public List<PlayerSegmentSummary> Players = new List<PlayerSegmentSummary>();
        /// <summary>Offers the player accepted.</summary>
        public List<BusinessOffer> OffersAccepted = new List<BusinessOffer>();
        /// <summary>Offers that were ignored.</summary>
        public List<BusinessOffer> OffersDeclined = new List<BusinessOffer>();
        /// <summary>Offers that expired unanswerable because their player left the roster before Resolve (not "declined").</summary>
        public List<BusinessOffer> OffersVoided = new List<BusinessOffer>();
        /// <summary>Sponsorship attempts in plan order.</summary>
        public List<SponsorAttempt> SponsorAttempts = new List<SponsorAttempt>();
        /// <summary>Contracts won this segment (they already paid once).</summary>
        public List<SponsorContract> NewContracts = new List<SponsorContract>();
        /// <summary>Livestream income, yuan.</summary>
        public int LivestreamIncome;
        /// <summary>Club share of accepted business deals, yuan.</summary>
        public int BusinessIncome;
        /// <summary>Sponsorship paid this segment, yuan.</summary>
        public int SponsorshipIncome;
        /// <summary>Prize money passed in the context, yuan (event prizes are paid at event end, see the session's EventRecord).</summary>
        public int PrizeMoney;
        /// <summary>Story 006: events of this segment in order: choices, action events (point order), state events.</summary>
        public List<EsportsManager.Events.EventOccurrence> Events = new List<EsportsManager.Events.EventOccurrence>();
        /// <summary>Story 006: event income booked this segment (numbers.md 4.10), yuan >= 0.</summary>
        public int EventIncome;
        /// <summary>Story 006: event expenses booked this segment (numbers.md 4.10), yuan >= 0.</summary>
        public int EventExpenses;
        /// <summary>The team's settlement report (income, expenses, salary share, net, funds, win rate, club popularity).</summary>
        public SegmentReport Report;
    }
}
