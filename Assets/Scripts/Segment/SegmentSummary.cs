using System.Collections.Generic;
using EsportsManager.Teams;

namespace EsportsManager.Segments
{
    /// <summary>
    /// One training pass (numbers.md 4.1 v3): all points a player put on the same TARGET dimension. The pass raises
    /// every main-lane dimension; the target's gain is multiplied by target_bonus.
    /// </summary>
    public struct TrainingResult
    {
        /// <summary>The chosen target dimension index (main lane).</summary>
        public int DimensionIndex;
        /// <summary>Training points on this target (intensity = Points x train_intensity).</summary>
        public int Points;
        /// <summary>Gain of the target dimension (Player.TrainingGain x target_bonus).</summary>
        public float Gain;
        /// <summary>Target dimension value after all of this player's training this segment (clamped 0-100).</summary>
        public float NewValue;
        /// <summary>Gain of every main-lane dimension in this pass, by index (target included, bonus applied), from SEGMENT-START values.</summary>
        public float[] Gains;
        /// <summary>Every main-lane dimension value after ALL of this player's passes were summed and applied, by index.</summary>
        public float[] NewValues;
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
        /// <summary>Per dimension training results.</summary>
        public List<TrainingResult> Training = new List<TrainingResult>();
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
        /// <summary>The team's settlement report (income, expenses, salary share, net, funds, win rate, club popularity).</summary>
        public SegmentReport Report;
    }
}
