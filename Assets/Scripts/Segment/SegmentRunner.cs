using System;
using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Segments
{
    /// <summary>
    /// Runs one segment of player actions on a <see cref="Team"/> (Story 003; design/numbers.md 4.1, 4.2, 4.6, 4.7,
    /// 4.10, 4.11, 4.12, 4.16). Pure C#, not thread-safe, all numbers from <see cref="SegmentConfig"/>.
    /// <para>FLOW (two calls so the player can look at the offers before deciding):</para>
    /// <para>TRAINING (numbers.md 4.1 v3): one pass per distinct target dimension; every pass computes all six gains from
    /// the SEGMENT-START values, the gains are summed per dimension and applied once (clamped), so the order of training
    /// points never matters. Same target twice = one pass at intensity 2.</para>
    /// <list type="number">
    /// <item><see cref="BeginSegment"/> rolls the business offers ONCE and opens the segment; read <see cref="GetState"/>.</item>
    /// <item><see cref="Resolve"/> is the one entry method. Order: validate (throws, changes NOTHING) -> actions: training ->
    /// sponsorship attempts -> resolve business -> update mood and stress -> build the <see cref="SegmentInput"/> (livestream income
    /// BEFORE settling) -> <see cref="Team.SettleSegment"/> -> expire contracts -> return the summary. NO matches are played in a
    /// segment (numbers.md 4.18 v3.4): events run between segments in the session (EventEffects applies their match terms).</item>
    /// </list>
    /// <para>RANDOM DRAW ORDER (fixed): Begin: for each rostered player in roster order, one U(0,1) trigger draw, and when
    /// it triggers, one R(business.randomRange) amount draw. Resolve: for each sponsor point in order, one
    /// U(-spread,+spread) noise draw then one U(0,1) outcome draw; then for each rostered player in roster order one
    /// R(stress.randomRange) then one R(mood.randomRange); then Team.SettleSegment draws from the TEAM's own random
    /// source (club first, then every player in roster order).</para>
    /// <para>DECISIONS: sponsorship contracts live here, not in Team (Team has no place for them); a contract won this
    /// segment pays from this segment on (N segments including this one); legacy context MatchResults (tests only) stress the
    /// starters and give the team's win/loss sign to every rostered player (unchanged Story 003 behaviour).
    /// Accepting a business offer costs one action point on that player (numbers.md 4.11 v3 / 4.16); his remaining
    /// points can still be used for training etc. Training (4.1 v3): every point raises all six main-lane dimensions,
    /// the target dimension x target_bonus. Stress (4.7 v3): s_recover is subtracted every segment for every rostered player.</para>
    /// </summary>
    /// <example><code>var runner = new SegmentRunner(cfg, playerCfg, team, rng);
    /// runner.BeginSegment();
    /// var plan = new SegmentPlan().Train(p.Id, 0).Stream(q.Id);
    /// var summary = runner.Resolve(plan, new SegmentContext { SegmentsPerYear = 12 });</code></example>
    public sealed class SegmentRunner
    {
        private readonly SegmentConfig _cfg;
        private readonly PlayerConfig _players;
        private readonly Team _team;
        private readonly IRandomSource _rng;
        private readonly List<BusinessOffer> _offers = new List<BusinessOffer>();
        private readonly List<SponsorContract> _contracts = new List<SponsorContract>();
        private bool _open;
        private int _nextOfferId;
        private int _serial;
        private SegmentSummary _lastSummary;

        /// <summary>Creates a runner. Validates the config; all dependencies are injected.</summary>
        /// <param name="rng">Random source for offers, sponsorship and the R factors (the team keeps its own).</param>
        public SegmentRunner(SegmentConfig config, PlayerConfig players, Team team, IRandomSource rng)
        {
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            config.Validate();
        }

        /// <summary>
        /// Adds an already running contract (loading a save, tests). Throws <see cref="InvalidOperationException"/> when all
        /// max_active_contracts slots are taken and <see cref="ArgumentException"/> when this instance is already active.
        /// </summary>
        public void AddContract(SponsorContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (_contracts.Contains(contract)) throw new ArgumentException("This contract is already active.", nameof(contract));
            if (FreeContractSlots() <= 0)
                throw new InvalidOperationException("All " + _cfg.sponsorship.maxActiveContracts + " contract slots are taken.");
            _contracts.Add(contract);
        }

        private int FreeContractSlots() => Math.Max(0, _cfg.sponsorship.maxActiveContracts - _contracts.Count);

        // Sponsorship rule (numbers.md 4.12 v3.2): Sponsor points allowed this segment = min(max_attempts_per_segment,
        // free contract slots at segment start), so every success always gets a slot and nothing is "won but unsigned".
        private int AllowedSponsorAttempts() => Math.Min(_cfg.sponsorship.maxAttemptsPerSegment, FreeContractSlots());

        /// <summary>
        /// Snapshot for the UI: points, pending offers (only those whose player is still rostered), active contracts,
        /// segment serial and the last summary.
        /// </summary>
        public SegmentState GetState()
        {
            var visible = new List<BusinessOffer>();
            foreach (var o in _offers) if (Find(o.PlayerId) != null) visible.Add(o);
            return new SegmentState
            {
                IsOpen = _open,
                ActionPoints = _cfg.actionPoints.perSegment,
                MaxPerPlayer = _cfg.actionPoints.maxPerPlayer,
                PendingOffers = visible.ToArray(),
                ActiveContracts = _contracts.ToArray(),
                FreeContractSlots = FreeContractSlots(),
                SponsorAttemptsRemaining = AllowedSponsorAttempts(),
                Serial = _serial,
                LastSummary = _lastSummary,
            };
        }

        /// <summary>Points still free if <paramref name="plan"/> were played (may be negative for an invalid plan).</summary>
        public int PointsRemaining(SegmentPlan plan)
        {
            return _cfg.actionPoints.perSegment - (plan == null ? 0 : plan.PointsUsed);
        }

        /// <summary>
        /// Opens a segment (serial + 1) and rolls this segment's business offers (numbers.md 4.11) for every rostered
        /// player. Offer ids continue from the previous segment and are never reused.
        /// Throws <see cref="InvalidOperationException"/> when a segment is already open (no re-rolling).
        /// </summary>
        public void BeginSegment()
        {
            if (_open) throw new InvalidOperationException("A segment is already open; call Resolve first.");
            _offers.Clear();
            var b = _cfg.business;
            foreach (var p in _team.Roster)
            {
                float trigger = _rng.NextFloat(0f, 1f);
                if (!(trigger < SegmentFormulas.BusinessProbability(_cfg, p))) continue;
                float r = _rng.NextFloat(1f - b.randomRange, 1f + b.randomRange);
                int amount = SegmentFormulas.BusinessAmount(_cfg, p, r);
                _offers.Add(new BusinessOffer(_nextOfferId++, p.Id, amount, SegmentFormulas.ClubShare(_cfg, amount)));
            }
            _serial++;
            _open = true;
        }

        // ------------------------------------------------------------ validation

        /// <summary>
        /// Checks a plan and context without changing anything. Returns false and the FIRST problem found
        /// (fixed check order: plan, open segment, context, salaries, sponsor points, assignments in order, offers in
        /// order, per-player limits, total points).
        /// </summary>
        public bool TryValidate(SegmentPlan plan, in SegmentContext ctx, out PlanError error)
        {
            error = Check(plan, ctx);
            return error.IsNone;
        }

        /// <summary>
        /// Same checks as <see cref="TryValidate"/>, throwing: <see cref="ArgumentNullException"/> (null plan),
        /// <see cref="ArgumentOutOfRangeException"/> (bad context / negative sponsor points), <see cref="ArgumentException"/>
        /// (unknown player / action / offer, bad dimension, duplicate offer, offer of a departed player) and
        /// <see cref="InvalidOperationException"/> (closed segment, negative salary, per-player or total limit).
        /// </summary>
        public void Validate(SegmentPlan plan, in SegmentContext ctx)
        {
            var e = Check(plan, ctx);
            switch (e.Code)
            {
                case PlanErrorCode.None: return;
                case PlanErrorCode.PlanNull: throw new ArgumentNullException(nameof(plan));
                case PlanErrorCode.SegmentNotOpen: throw new InvalidOperationException("No open segment; call BeginSegment first.");
                case PlanErrorCode.BadSegmentsPerYear: throw new ArgumentOutOfRangeException(nameof(ctx), "SegmentsPerYear must be > 0.");
                case PlanErrorCode.BadSegmentIndex: throw new ArgumentOutOfRangeException(nameof(ctx), "SegmentIndexInYear must be in [0, SegmentsPerYear).");
                case PlanErrorCode.BadSeasonRankScore: throw new ArgumentOutOfRangeException(nameof(ctx), "SeasonRankScore must be in [0,100].");
                case PlanErrorCode.NegativePrizeMoney: throw new ArgumentOutOfRangeException(nameof(ctx), "PrizeMoney must be >= 0.");
                case PlanErrorCode.NegativeSalary: throw new InvalidOperationException("Player " + e.PlayerId + " has a negative salary.");
                case PlanErrorCode.NegativeSponsorPoints: throw new ArgumentOutOfRangeException(nameof(plan), "SponsorPoints must be >= 0.");
                case PlanErrorCode.UnknownPlayer: throw new ArgumentException("Unknown player id " + e.PlayerId + " (not on the roster).", nameof(plan));
                case PlanErrorCode.UnknownAction: throw new ArgumentException("Unknown action for player " + e.PlayerId + ".", nameof(plan));
                case PlanErrorCode.DimensionOutOfRange:
                    throw new ArgumentException("Dimension index " + e.DimensionIndex + " is outside 0.." + (e.Limit - 1) + " for player " + e.PlayerId + ".", nameof(plan));
                case PlanErrorCode.UnknownOffer: throw new ArgumentException("Unknown business offer id " + e.OfferId + ".", nameof(plan));
                case PlanErrorCode.DuplicateOffer: throw new ArgumentException("Business offer " + e.OfferId + " is accepted twice.", nameof(plan));
                case PlanErrorCode.OfferPlayerLeft:
                    throw new ArgumentException("Offer " + e.OfferId + " is for player " + e.PlayerId + " who left the roster.", nameof(plan));
                case PlanErrorCode.PlayerOverLimit:
                    throw new InvalidOperationException("Player " + e.PlayerId + " gets " + e.Used + " action points, the limit is " + e.Limit + ".");
                case PlanErrorCode.SponsorAttemptsOverLimit:
                    throw new InvalidOperationException("Plan has " + e.Used + " sponsorship attempts, only " + e.Limit + " are allowed this segment.");
                case PlanErrorCode.ContractsFull:
                    throw new InvalidOperationException("All " + e.Limit + " sponsorship contract slots are taken; no attempt is possible.");
                case PlanErrorCode.IncompleteLineup:
                    throw new InvalidOperationException("The lineup has " + e.Used + " starters; all " + e.Limit + " lanes need one to play.");
                case PlanErrorCode.UnknownTactic: throw new ArgumentException("Unknown tactic.", nameof(plan));
                case PlanErrorCode.EventPending: throw new InvalidOperationException("An event is in progress.");
                case PlanErrorCode.OverTotalPoints:
                    throw new InvalidOperationException("Plan spends " + e.Used + " action points, only " + e.Limit + " are available.");
                default: throw new InvalidOperationException("Invalid plan: " + e.Code + ".");
            }
        }

        private PlanError Check(SegmentPlan plan, in SegmentContext ctx)
        {
            if (plan == null) return Err(PlanErrorCode.PlanNull);
            if (!_open) return Err(PlanErrorCode.SegmentNotOpen);

            // Context and salary checks MIRROR Team.ValidateInput (Team.cs, SettleSegment step 1): if Team gains a check,
            // add it here too, or Resolve could half-apply (players changed, then SettleSegment throws).
            if (ctx.SegmentsPerYear <= 0) return Err(PlanErrorCode.BadSegmentsPerYear);
            if (ctx.SegmentIndexInYear < 0 || ctx.SegmentIndexInYear >= ctx.SegmentsPerYear) return Err(PlanErrorCode.BadSegmentIndex);
            if (!(ctx.SeasonRankScore >= 0f && ctx.SeasonRankScore <= 100f)) return Err(PlanErrorCode.BadSeasonRankScore);
            if (ctx.PrizeMoney < 0) return Err(PlanErrorCode.NegativePrizeMoney);
            foreach (var p in _team.Roster)
                if (p.Salary < 0) return new PlanError { Code = PlanErrorCode.NegativeSalary, PlayerId = p.Id };
            if (plan.SponsorPoints < 0) return Err(PlanErrorCode.NegativeSponsorPoints);
            if (plan.SponsorPoints > 0 && FreeContractSlots() == 0)
                return new PlanError { Code = PlanErrorCode.ContractsFull, Used = _contracts.Count, Limit = _cfg.sponsorship.maxActiveContracts };
            if (plan.SponsorPoints > AllowedSponsorAttempts())
                return new PlanError { Code = PlanErrorCode.SponsorAttemptsOverLimit, Used = plan.SponsorPoints, Limit = AllowedSponsorAttempts() };

            var perPlayer = new Dictionary<long, int>();
            var order = new List<long>();
            foreach (var a in plan.Assignments)
            {
                var player = Find(a.PlayerId);
                if (player == null) return new PlanError { Code = PlanErrorCode.UnknownPlayer, PlayerId = a.PlayerId };
                if (!Enum.IsDefined(typeof(SegmentAction), a.Action)) return new PlanError { Code = PlanErrorCode.UnknownAction, PlayerId = a.PlayerId };
                if (a.Action == SegmentAction.Train)
                {
                    int dims = DimensionCount(player);
                    if (a.DimensionIndex < 0 || a.DimensionIndex >= dims)
                        return new PlanError { Code = PlanErrorCode.DimensionOutOfRange, PlayerId = a.PlayerId, DimensionIndex = a.DimensionIndex, Limit = dims };
                }
                Count(perPlayer, order, a.PlayerId);
            }
            var seen = new HashSet<int>();
            foreach (int id in plan.AcceptedOfferIds)
            {
                var offer = FindOffer(id);
                if (offer == null) return new PlanError { Code = PlanErrorCode.UnknownOffer, OfferId = id };
                if (!seen.Add(id)) return new PlanError { Code = PlanErrorCode.DuplicateOffer, OfferId = id };
                if (Find(offer.Value.PlayerId) == null)
                    return new PlanError { Code = PlanErrorCode.OfferPlayerLeft, OfferId = id, PlayerId = offer.Value.PlayerId };
                Count(perPlayer, order, offer.Value.PlayerId);
            }

            int max = _cfg.actionPoints.maxPerPlayer;
            foreach (long id in order)
                if (perPlayer[id] > max) return new PlanError { Code = PlanErrorCode.PlayerOverLimit, PlayerId = id, Used = perPlayer[id], Limit = max };
            if (plan.PointsUsed > _cfg.actionPoints.perSegment)
                return new PlanError { Code = PlanErrorCode.OverTotalPoints, Used = plan.PointsUsed, Limit = _cfg.actionPoints.perSegment };
            return default;
        }

        private static PlanError Err(PlanErrorCode code) => new PlanError { Code = code };

        private static void Count(Dictionary<long, int> map, List<long> order, long id)
        {
            if (!map.TryGetValue(id, out int n)) order.Add(id);
            map[id] = n + 1;
        }

        // ------------------------------------------------------------ preview

        /// <summary>
        /// What <paramref name="plan"/> would do, for a UI drafting a plan. Deterministic: NO random draws (all R = 1,
        /// sponsorship chance before noise) and NO state change. Works for invalid drafts too: <see cref="SegmentPreview.Error"/>
        /// carries the first problem, assignments naming unknown players or bad dimensions and unknown offers are skipped.
        /// Training gains equal what Resolve applies for the same plan (before clamping).
        /// </summary>
        public SegmentPreview Preview(SegmentPlan plan, SegmentContext ctx)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var pv = new SegmentPreview
            {
                Error = Check(plan, ctx), PointsUsed = plan.PointsUsed, PointsRemaining = PointsRemaining(plan),
                SponsorPoints = plan.SponsorPoints,
                FreeContractSlots = FreeContractSlots(),
                SponsorAttemptsRemaining = AllowedSponsorAttempts() - Math.Max(0, plan.SponsorPoints),
                SponsorChanceBeforeNoise = SegmentFormulas.SponsorChance(_cfg, _team.ClubPopularity, _team.RecentWinRateWith(ctx.MatchResults), 0f),
            };
            var roster = _team.Roster;
            var tallies = new Dictionary<long, PlayerSegmentSummary>();
            foreach (var p in roster) tallies[p.Id] = new PlayerSegmentSummary { PlayerId = p.Id };
            TallyAssignments(plan, tallies, skipInvalid: true);
            var seen = new HashSet<int>();
            foreach (int id in plan.AcceptedOfferIds)
            {
                var offer = FindOffer(id);
                if (offer == null || !seen.Add(id) || !tallies.TryGetValue(offer.Value.PlayerId, out var ps)) continue;
                ps.BusinessPoints++;
                pv.AcceptedOffers.Add(offer.Value);
                pv.BusinessIncome += offer.Value.ClubAmount;
            }

            var starters = _team.Starters();
            MatchCounts(ctx, out int matches, out int losses, out int result);
            long live = 0;
            foreach (var p in roster)
            {
                var ps = tallies[p.Id];
                bool starter = IsIn(starters, p);
                int used = ps.TrainPoints + ps.StreamPoints + ps.RestPoints + ps.BusinessPoints;
                var pp = new PlayerPreview
                {
                    PlayerId = p.Id, PointsUsed = used, PointsRemaining = _cfg.actionPoints.maxPerPlayer - used,
                    TrainPoints = ps.TrainPoints, StreamPoints = ps.StreamPoints, RestPoints = ps.RestPoints, BusinessPoints = ps.BusinessPoints,
                    TrainingGains = ComputeTraining(p, ps.Training),
                    StressDelta = SegmentFormulas.StressDelta(_cfg, p, StressFor(ps, starter ? matches : 0, starter ? losses : 0), 1f),
                    MoodDelta = SegmentFormulas.MoodDelta(_cfg, p, MoodFor(p, starter, result, starters), 1f),
                    LivestreamIncome = _team.LivestreamIncome(p, ps.StreamPoints),
                };
                live += pp.LivestreamIncome;
                pv.Players.Add(pp);
            }
            pv.LivestreamIncome = ClampInt(live);
            return pv;
        }

        // ------------------------------------------------------------ resolve

        /// <summary>
        /// The one entry method for a segment; see the class remarks for the fixed order. Any validation failure
        /// throws BEFORE any state (players, team, contracts, offers, random source) changes and leaves the segment
        /// open so the plan can be corrected. On success the segment closes, unused points are lost, and the summary
        /// becomes <see cref="SegmentState.LastSummary"/>. Offers whose player left the roster are reported as voided.
        /// </summary>
        public SegmentSummary Resolve(SegmentPlan plan, SegmentContext ctx)
        {
            Validate(plan, ctx);

            var roster = _team.Roster;
            var summary = new SegmentSummary { PointsUsed = plan.PointsUsed, PrizeMoney = ctx.PrizeMoney };
            summary.PointsUnused = _cfg.actionPoints.perSegment - summary.PointsUsed;
            var byId = new Dictionary<long, PlayerSegmentSummary>();
            var before = new float[roster.Count, 4]; // mood, stress, popularity, observation
            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                var ps = new PlayerSegmentSummary { PlayerId = p.Id };
                summary.Players.Add(ps);
                byId[p.Id] = ps;
                before[i, 0] = p.Mood; before[i, 1] = p.Stress; before[i, 2] = p.Popularity; before[i, 3] = p.Observation;
            }

            // ---- 1. execute actions: tally, then training (order independent, see class remarks)
            TallyAssignments(plan, byId, skipInvalid: false);
            float o = _players.potential.observationPerTraining;
            foreach (var p in roster)
            {
                var ps = byId[p.Id];
                if (ps.TrainPoints == 0) continue;
                float[] total = ComputeTraining(p, ps.Training);
                var finals = new float[total.Length];
                for (int d = 0; d < total.Length; d++)
                {
                    p.SetDimension(p.MainLane, d, p.GetDimension(p.MainLane, d) + total[d]);
                    finals[d] = p.GetDimension(p.MainLane, d);
                }
                for (int k = 0; k < ps.Training.Count; k++)
                {
                    var t = ps.Training[k];
                    t.NewValues = (float[])finals.Clone();
                    t.NewValue = finals[t.DimensionIndex];
                    ps.Training[k] = t;
                }
                p.AddObservation(o * ps.TrainPoints);
            }

            // ---- 2. no matches here: the calendar plays them in events between segments (numbers.md 4.18 v3.4)
            IReadOnlyList<bool> results = ctx.MatchResults;

            // ---- 3. sponsorship attempts (the chance reads the recent win rate = the latest event's, numbers.md 4.18)
            float postMatchWinRate = _team.RecentWinRateWith(results);
            var sp = _cfg.sponsorship;
            for (int i = 0; i < plan.SponsorPoints; i++)
            {
                float noise = _rng.NextFloat(-sp.randomSpread, sp.randomSpread);
                float chance = SegmentFormulas.SponsorChance(_cfg, _team.ClubPopularity, postMatchWinRate, noise);
                bool ok = _rng.NextFloat(0f, 1f) < chance;
                summary.SponsorAttempts.Add(new SponsorAttempt { Chance = chance, Success = ok });
                if (!ok) continue;
                var contract = new SponsorContract(SegmentFormulas.ContractAmount(_cfg, _team.ClubPopularity), sp.durationSegments);
                _contracts.Add(contract);
                summary.NewContracts.Add(contract);
            }

            // ---- 4. resolve business
            long businessIncome = 0;
            var accepted = new HashSet<int>(plan.AcceptedOfferIds);
            foreach (var offer in _offers)
            {
                if (!byId.ContainsKey(offer.PlayerId)) { summary.OffersVoided.Add(offer); continue; }
                if (!accepted.Contains(offer.Id)) { summary.OffersDeclined.Add(offer); continue; }
                summary.OffersAccepted.Add(offer);
                byId[offer.PlayerId].BusinessPoints++;
                businessIncome += offer.ClubAmount;
            }

            // ---- 5. mood and stress (both deltas from segment-start values, then applied)
            var starters = _team.Starters();
            MatchCounts(ctx, out int matches, out int losses, out int result);
            float rs = _cfg.stress.randomRange, rm = _cfg.mood.randomRange;
            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                var ps = byId[p.Id];
                bool starter = IsIn(starters, p);
                int pm = starter ? matches : 0, pl = starter ? losses : 0, pr = result;
                ps.MatchesPlayed = pm; ps.MatchesLost = pl;
                float stressR = _rng.NextFloat(1f - rs, 1f + rs);
                float moodR = _rng.NextFloat(1f - rm, 1f + rm);
                float stressDelta = SegmentFormulas.StressDelta(_cfg, p, StressFor(ps, pm, pl), stressR);
                float moodDelta = SegmentFormulas.MoodDelta(_cfg, p, MoodFor(p, starter, pr, starters), moodR);
                p.Stress += stressDelta;
                p.Mood += moodDelta;
                p.AddObservation(_players.potential.observationPerSegment);
            }

            // ---- 6. build SegmentInput (livestream income BEFORE settling, popularity still unchanged)
            var heat = new Dictionary<long, PlayerHeat>();
            long livestream = 0;
            foreach (var p in roster)
            {
                var ps = byId[p.Id];
                ps.StreamHeat = SegmentFormulas.StreamHeat(_cfg, p, ps.StreamPoints);
                ps.BusinessHeat = SegmentFormulas.BusinessHeat(_cfg, ps.BusinessPoints);
                ps.LivestreamIncome = _team.LivestreamIncome(p, ps.StreamPoints);
                livestream += ps.LivestreamIncome;
                heat[p.Id] = new PlayerHeat { Stream = ps.StreamHeat, Business = ps.BusinessHeat };
            }
            long sponsorship = 0;
            foreach (var c in _contracts) sponsorship += c.AmountPerSegment;
            summary.LivestreamIncome = ClampInt(livestream);
            summary.BusinessIncome = ClampInt(businessIncome);
            summary.SponsorshipIncome = ClampInt(sponsorship);
            var input = new SegmentInput
            {
                SegmentsPerYear = ctx.SegmentsPerYear, SegmentIndexInYear = ctx.SegmentIndexInYear,
                MatchResults = results, SeasonRankScore = ctx.SeasonRankScore, Heat = heat,
                PrizeMoney = summary.PrizeMoney, LivestreamIncome = summary.LivestreamIncome,
                BusinessIncome = summary.BusinessIncome, Sponsorship = summary.SponsorshipIncome,
            };

            // ---- 7. settle, then expire contracts and close
            summary.Report = _team.SettleSegment(input);
            foreach (var c in _contracts) c.SegmentsRemaining--;
            _contracts.RemoveAll(c => c.SegmentsRemaining <= 0);
            _offers.Clear();
            _open = false;

            for (int i = 0; i < roster.Count; i++)
            {
                var p = roster[i];
                var ps = summary.Players[i];
                ps.MoodDelta = p.Mood - before[i, 0];
                ps.StressDelta = p.Stress - before[i, 1];
                ps.PopularityDelta = p.Popularity - before[i, 2];
                ps.ObservationDelta = p.Observation - before[i, 3];
            }
            _lastSummary = summary;
            return summary;
        }

        // ------------------------------------------------------------ shared helpers (Preview and Resolve)

        // Counts points per player and groups training points by target (first-appearance order). With skipInvalid,
        // assignments that validation would reject are ignored (Preview of a draft); otherwise the plan is already valid.
        private void TallyAssignments(SegmentPlan plan, Dictionary<long, PlayerSegmentSummary> byId, bool skipInvalid)
        {
            foreach (var a in plan.Assignments)
            {
                if (!byId.TryGetValue(a.PlayerId, out var ps)) continue;
                switch (a.Action)
                {
                    case SegmentAction.Train:
                        if (skipInvalid && (a.DimensionIndex < 0 || a.DimensionIndex >= DimensionCount(Find(a.PlayerId)))) continue;
                        ps.TrainPoints++;
                        int idx = ps.Training.FindIndex(t => t.DimensionIndex == a.DimensionIndex);
                        if (idx < 0) ps.Training.Add(new TrainingResult { DimensionIndex = a.DimensionIndex, Points = 1 });
                        else { var t = ps.Training[idx]; t.Points++; ps.Training[idx] = t; }
                        break;
                    case SegmentAction.Stream: ps.StreamPoints++; break;
                    case SegmentAction.Rest: ps.RestPoints++; break;
                }
            }
        }

        // Fills Gains / Gain of every pass from the player's CURRENT (segment-start) values and returns the per-dimension
        // sum over all passes. Changes nothing on the player.
        private float[] ComputeTraining(Player p, List<TrainingResult> passes)
        {
            int dims = DimensionCount(p);
            var total = new float[dims];
            for (int k = 0; k < passes.Count; k++)
            {
                var t = passes[k];
                float intensity = t.Points * _cfg.training.intensity;
                t.Gains = new float[dims];
                for (int d = 0; d < dims; d++)
                {
                    float g = p.TrainingGain(p.MainLane, d, intensity);
                    t.Gains[d] = d == t.DimensionIndex ? g * _cfg.training.targetBonus : g;
                    total[d] += t.Gains[d];
                }
                t.Gain = t.Gains[t.DimensionIndex];
                passes[k] = t;
            }
            return total;
        }

        private StressInputs StressFor(PlayerSegmentSummary ps, int matches, int losses) => new StressInputs
        {
            TrainingIntensity = ps.TrainPoints * _cfg.training.intensity,
            Matches = matches, Losses = losses,
            Business = ps.BusinessPoints, Rest = ps.RestPoints, Streams = ps.StreamPoints,
        };

        private static MoodInputs MoodFor(Player p, bool starter, int result, List<Player> starters) => new MoodInputs
        {
            Result = result, IsStarter = starter,
            SalarySatisfaction = SalarySatisfaction(p), Compatibility = MeanCompatibility(p, starters),
        };

        private static void MatchCounts(in SegmentContext ctx, out int matches, out int losses, out int result)
        {
            matches = ctx.MatchResults == null ? 0 : ctx.MatchResults.Count;
            int wins = 0;
            if (ctx.MatchResults != null) foreach (bool w in ctx.MatchResults) if (w) wins++;
            losses = matches - wins;
            result = wins > losses ? 1 : (wins < losses ? -1 : 0);
        }

        private static bool IsIn(List<Player> list, Player p)
        {
            foreach (var x in list) if (x.Id == p.Id) return true;
            return false;
        }

        private int DimensionCount(Player p) => _players.FindLane(p.MainLane).dimensions.Length;

        private Player Find(long id)
        {
            foreach (var p in _team.Roster) if (p.Id == id) return p;
            return null;
        }

        private BusinessOffer? FindOffer(int id)
        {
            foreach (var o in _offers) if (o.Id == id) return o;
            return null;
        }

        // (salary - expected) / expected with expected = Player.CalculateSalaryWithoutRandom (numbers.md 4.6),
        // bounded to [-1, 1] (decision: keeps the term in a range comparable to the other +-1 terms).
        private static float SalarySatisfaction(Player p)
        {
            int expected = p.CalculateSalaryWithoutRandom();
            if (expected <= 0) return 0f;
            float s = (float)(p.Salary - expected) / expected;
            return s < -1f ? -1f : (s > 1f ? 1f : s);
        }

        // Mean H with the OTHER starters (the player himself is skipped); 0 when there are none.
        private static float MeanCompatibility(Player p, List<Player> starters)
        {
            double sum = 0.0; int n = 0;
            foreach (var o in starters)
            {
                if (o.Id == p.Id) continue;
                sum += p.CompatibilityWith(o); n++;
            }
            return n == 0 ? 0f : (float)(sum / n);
        }

        private static int ClampInt(long v) => v > int.MaxValue ? int.MaxValue : (int)v;
    }
}
