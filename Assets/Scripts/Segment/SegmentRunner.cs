using System;
using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Events;
using EsportsManager.Growth;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Segments
{
    /// <summary>
    /// Runs one segment of player actions on a <see cref="Team"/> (Story 003; design/numbers.md 4.1, 4.2, 4.6, 4.7,
    /// 4.10, 4.11, 4.12, 4.16). Pure C#, not thread-safe, all numbers from <see cref="SegmentConfig"/>.
    /// <para>FLOW (two calls so the player can look at the offers before deciding):</para>
    /// <para>TRAINING (design/gdd/growth.md rev 3, Story 012, via <see cref="GrowthSystem"/>): every point trains one phase of
    /// the main lane (or command) with an optional focus dimension; its base amount b (F1, x the point's noise, then the F5 event
    /// modifiers) and its gains (F2) come from the SEGMENT-START values; a player's gains are summed and applied once (clamped to
    /// the ceiling), so the order of points never matters. Then each trained player's focus is updated from his point counts
    /// (F3). The specialty check (F4) runs for every rostered player at the very END of Resolve (after events and settlement).
    /// Noise: one U(1 - noise, 1 + noise) per point from the injected training-noise source (players in roster order, points
    /// in plan order); without that source the noise is 1 (tests). Without a growth system no training point is accepted
    /// (TrainingUnavailable).</para>
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
    /// <para>RANDOM EVENTS (Story 006, numbers.md 4.17), only with an <see cref="EventEngine"/> (own random source, so the
    /// draws above are unchanged): BeginSegment rolls choice events after the offers; Resolve, right after validation, rolls the
    /// action events for every point (order: assignments in plan order, accepted offers in plan order, sponsor points) and
    /// feeds their modifiers into training / sponsorship / business / stress (rest) / livestream income; after mood and stress
    /// it applies choice answers (unanswered = the default option), then the action events' mood / stress / potential, then
    /// rolls and applies state events; funds effects go into SegmentInput as event income / expenses; popularity, club
    /// popularity and familiarity effects are applied after Team.SettleSegment.</para>
    /// <para>DECISIONS: sponsorship contracts live here, not in Team (Team has no place for them); a contract won this
    /// segment pays from this segment on (N segments including this one); legacy context MatchResults (tests only) stress the
    /// starters and give the team's win/loss sign to every rostered player (unchanged Story 003 behaviour).
    /// Accepting a business offer costs one action point on that player (numbers.md 4.11 v3 / 4.16); his remaining
    /// points can still be used for training etc. Stress (4.7 v3): s_recover is subtracted every segment for every rostered player.</para>
    /// </summary>
    /// <example><code>var runner = new SegmentRunner(cfg, playerCfg, team, rng, events, growth, noiseRng);
    /// runner.BeginSegment();
    /// var plan = new SegmentPlan().Train(p.Id, TrainingTarget.Early).Stream(q.Id);
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
        private readonly EventEngine _events;
        private readonly List<PendingChoice> _choices = new List<PendingChoice>();
        private int _nextChoiceId;
        private readonly GrowthSystem _growth;
        private readonly IRandomSource _noise;

        /// <summary>
        /// Salary satisfaction source for the mood m_salary term (economy.md rev 3: market salary with a dead zone, set by the
        /// league). Null = numbers.md 4.6 (expected = Player.CalculateSalaryWithoutRandom, bounded to [-1, 1]).
        /// </summary>
        public Func<Player, float> SalarySatisfactionSource { get; set; }

        /// <summary>
        /// Media invitations of the open segment (economy.md 媒体邀约). Cleared by <see cref="BeginSegment"/>, then filled by the
        /// league; a plan may accept each once (one club action point each). Their effects are applied by the league after
        /// <see cref="Resolve"/> (the list stays readable until the next BeginSegment).
        /// </summary>
        public List<MediaInvitation> MediaInvitations { get; } = new List<MediaInvitation>();

        /// <summary>Creates a runner. Validates the config; all dependencies are injected.</summary>
        /// <param name="rng">Random source for offers, sponsorship and the R factors (the team keeps its own).</param>
        /// <param name="events">Optional random-event engine (Story 006); null = no events (Story 003 behaviour).</param>
        /// <param name="growth">Growth system (Story 012); null = training points are refused (TrainingUnavailable).</param>
        /// <param name="trainingNoise">Own source for the training noise (so the other draws are unchanged); null = noise 1.</param>
        public SegmentRunner(SegmentConfig config, PlayerConfig players, Team team, IRandomSource rng, EventEngine events = null,
            GrowthSystem growth = null, IRandomSource trainingNoise = null)
        {
            _events = events;
            _growth = growth;
            _noise = trainingNoise;
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
                PendingChoices = _open ? _choices.ToArray() : new PendingChoice[0],
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
            MediaInvitations.Clear();
            var b = _cfg.business;
            foreach (var p in _team.Roster)
            {
                float trigger = _rng.NextFloat(0f, 1f);
                float prob = SegmentFormulas.BusinessProbability(_cfg, p);
                if (!(trigger < prob)) continue;
                float r = _rng.NextFloat(1f - b.randomRange, 1f + b.randomRange);
                int amount = SegmentFormulas.BusinessAmount(_cfg, p, r);
                _offers.Add(new BusinessOffer(_nextOfferId++, p.Id, amount, SegmentFormulas.ClubShare(_cfg, amount)));
            }
            _choices.Clear();
            if (_events != null) _choices.AddRange(_events.RollChoices(_team, ref _nextChoiceId));
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
                    throw new ArgumentException("Focus dimension " + e.DimensionIndex + " is not in the trained phase for player " + e.PlayerId + ".", nameof(plan));
                case PlanErrorCode.UnknownTrainingTarget: throw new ArgumentException("Unknown training target for player " + e.PlayerId + ".", nameof(plan));
                case PlanErrorCode.TrainingUnavailable: throw new InvalidOperationException("Training needs the growth system (no match phase mapping).");
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
                case PlanErrorCode.UnknownChoice: throw new ArgumentException("Unknown choice id " + e.ChoiceId + ".", nameof(plan));
                case PlanErrorCode.ChoiceOptionOutOfRange:
                    throw new ArgumentException("Choice " + e.ChoiceId + " has options 0.." + (e.Limit - 1) + ".", nameof(plan));
                case PlanErrorCode.PlayerOnLeave:
                    throw new InvalidOperationException("Player " + e.PlayerId + " is on leave (choice " + e.ChoiceId + ") and may get no action point.");
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
                    var te = CheckTraining(player, a);
                    if (!te.IsNone) return te;
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

            var seenMedia = new HashSet<int>();
            foreach (int id in plan.AcceptedMediaIds)
            {
                if (!MediaInvitations.Exists(m => m.Id == id)) return new PlanError { Code = PlanErrorCode.UnknownOffer, OfferId = id };
                if (!seenMedia.Add(id)) return new PlanError { Code = PlanErrorCode.DuplicateOffer, OfferId = id };
            }

            foreach (var ans in plan.ChoiceAnswers)
            {
                var ch = FindChoice(ans.ChoiceId);
                if (ch == null) return new PlanError { Code = PlanErrorCode.UnknownChoice, ChoiceId = ans.ChoiceId };
                if (ans.Option < 0 || ans.Option >= ch.Options.Count)
                    return new PlanError { Code = PlanErrorCode.ChoiceOptionOutOfRange, ChoiceId = ans.ChoiceId, Limit = ch.Options.Count };
            }
            foreach (var ch in _choices)
                if (ch.PlayerId != null && ch.Options[EffectiveOption(plan, ch, out _)].PutsOnLeave && perPlayer.ContainsKey(ch.PlayerId.Value))
                    return new PlanError { Code = PlanErrorCode.PlayerOnLeave, PlayerId = ch.PlayerId, ChoiceId = ch.Id };

            int max = _cfg.actionPoints.maxPerPlayer;
            foreach (long id in order)
                if (perPlayer[id] > max) return new PlanError { Code = PlanErrorCode.PlayerOverLimit, PlayerId = id, Used = perPlayer[id], Limit = max };
            if (plan.PointsUsed > _cfg.actionPoints.perSegment)
                return new PlanError { Code = PlanErrorCode.OverTotalPoints, Used = plan.PointsUsed, Limit = _cfg.actionPoints.perSegment };
            return default;
        }

        private static PlanError Err(PlanErrorCode code) => new PlanError { Code = code };

        private PlanError CheckTraining(Player player, ActionAssignment a)
        {
            if (_growth == null) return new PlanError { Code = PlanErrorCode.TrainingUnavailable, PlayerId = a.PlayerId };
            if (!Enum.IsDefined(typeof(TrainingTarget), a.Target)) return new PlanError { Code = PlanErrorCode.UnknownTrainingTarget, PlayerId = a.PlayerId };
            if (!_growth.IsValidFocus(player.MainLane, a.Target, a.FocusDimension))
                return new PlanError { Code = PlanErrorCode.DimensionOutOfRange, PlayerId = a.PlayerId, DimensionIndex = a.FocusDimension };
            return default;
        }

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
        /// Training gains equal what Resolve applies for the same plan at noise 1 without event modifiers (before clamping); the
        /// low / high arrays are the +-noise range.
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
                    TrainingGains = PreviewGains(plan, p, 1f),
                    TrainingGainsLow = PreviewGains(plan, p, _growth != null ? _growth.NoiseLow : 1f),
                    TrainingGainsHigh = PreviewGains(plan, p, _growth != null ? _growth.NoiseHigh : 1f),
                    FocusAfter = PreviewFocus(plan, p),
                    StressDelta = SegmentFormulas.StressDelta(_cfg, p, StressFor(ps, starter ? matches : 0, starter ? losses : 0, starter), 1f),
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
        /// The one entry method for a segment; see the class remarks for the fixed order (training first, specialty check last). Any validation failure
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

            // ---- 0. random events (Story 006): roll the action events for every point; nothing changes yet
            int offerBase = plan.Assignments.Count, sponsorBase = offerBase + plan.AcceptedOfferIds.Count;
            var points = new List<ActionPointRef>();
            foreach (var a in plan.Assignments) points.Add(new ActionPointRef(ToEventAction(a.Action), a.PlayerId, (int)a.Target));
            foreach (int id in plan.AcceptedOfferIds) points.Add(new ActionPointRef(EventAction.Business, FindOffer(id).Value.PlayerId));
            for (int i = 0; i < plan.SponsorPoints; i++) points.Add(new ActionPointRef(EventAction.Sponsor, 0));
            var actionEvents = _events != null ? _events.RollActionEvents(_team, points) : new List<EventOccurrence>();
            var mods = new PointMods[points.Count];
            for (int i = 0; i < mods.Length; i++) mods[i] = new PointMods();
            foreach (var ev in actionEvents)
                foreach (var fx in ev.Effects) mods[ev.PointIndex].Add(fx);

            // ---- 1. execute actions: tally, then training (growth.md; order independent, see class remarks)
            TallyAssignments(plan, byId, skipInvalid: false);
            float o = _players.potential.observationPerTraining;
            foreach (var p in roster)
            {
                var ps = byId[p.Id];
                if (ps.TrainPoints == 0) continue;
                _growth.EnsureFocus(p);
                var acc = new float[DimensionCount(p)];
                var counts = new int[4];
                for (int i = 0; i < plan.Assignments.Count; i++)
                {
                    var a = plan.Assignments[i];
                    if (a.PlayerId != p.Id || a.Action != SegmentAction.Train) continue;
                    var m = mods[i];
                    float noise = _noise != null ? _noise.NextFloat(_growth.NoiseLow, _growth.NoiseHigh) : 1f;
                    float b0 = _growth.BaseAmount(p, a.Target, _cfg.training.intensity, noise);
                    float b = GrowthSystem.WithEventModifiers(b0, m.TrainMult, m.TrainAdd);
                    foreach (var fx in m.Fx)
                    {
                        if (fx.Type == EffectType.TrainMultiplier) fx.Applied = b0 * (fx.Value - 1f);
                        else if (fx.Type == EffectType.TrainAdd) fx.Applied = fx.Value;
                    }
                    var gains = _growth.PointGains(p, a.Target, a.FocusDimension, b);
                    for (int d = 0; d < acc.Length; d++) acc[d] += gains[d];
                    ps.Training.Add(new TrainingResult { Target = a.Target, FocusDimension = a.FocusDimension, Noise = noise, Base = b, Gains = gains });
                    counts[(int)a.Target]++;
                }
                ps.ValuesBefore = p.GetRadar(p.MainLane);
                ps.FocusBefore = Focus(p);
                _growth.ApplyGains(p, acc);
                _growth.UpdateFocus(p, counts);
                ps.ValuesAfter = p.GetRadar(p.MainLane);
                p.AddObservation(o * ps.TrainPoints);
            }

            // ---- 2. no matches here: the calendar plays them in events between segments (numbers.md 4.18 v3.4)
            IReadOnlyList<bool> results = ctx.MatchResults;

            // ---- 3. sponsorship attempts (the chance reads the recent win rate = the latest event's, numbers.md 4.18)
            float postMatchWinRate = _team.RecentWinRateWith(results);
            var sp = _cfg.sponsorship;
            for (int i = 0; i < plan.SponsorPoints; i++)
            {
                var m = mods[sponsorBase + i];
                if (m.Void) { summary.SponsorAttempts.Add(new SponsorAttempt { Voided = true }); continue; }
                float noise = _rng.NextFloat(-sp.randomSpread, sp.randomSpread);
                float chance = SegmentFormulas.SponsorChance(_cfg, _team.ClubPopularity, postMatchWinRate, noise);
                if (m.SponsorAdd != 0f)
                {
                    float chanceBefore = chance;
                    chance = Math.Max(sp.minChance, Math.Min(sp.maxChance, chance + m.SponsorAdd));
                    foreach (var fx in m.Fx) if (fx.Type == EffectType.SponsorChanceAdd) fx.Applied = chance - chanceBefore;
                }
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
                var bm = mods[offerBase + IndexOf(plan.AcceptedOfferIds, offer.Id)];
                long extra = (long)Math.Round(offer.ClubAmount * (double)(bm.BizMult - 1f), MidpointRounding.AwayFromZero);
                foreach (var fx in bm.Fx) if (fx.Type == EffectType.BusinessMultiplier) fx.Applied = extra;
                businessIncome += Math.Max(0L, offer.ClubAmount + extra);
                var earner = Find(offer.PlayerId);
                if (earner != null) earner.PersonalBusinessEarnings += Math.Max(0, offer.Amount - offer.ClubAmount);
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
                float rest = 0f;
                for (int k = 0; k < plan.Assignments.Count; k++)
                {
                    var a = plan.Assignments[k];
                    if (a.PlayerId != p.Id || a.Action != SegmentAction.Rest) continue;
                    rest += mods[k].RestMult;
                    foreach (var fx in mods[k].Fx) if (fx.Type == EffectType.RestMultiplier) fx.Applied = (fx.Value - 1f) * _cfg.stress.rest;
                }
                var si = StressFor(ps, pm, pl, starter); si.Rest = rest;
                float stressDelta = SegmentFormulas.StressDelta(_cfg, p, si, stressR);
                float moodDelta = SegmentFormulas.MoodDelta(_cfg, p, MoodFor(p, starter, pr, starters), moodR);
                p.Stress += stressDelta;
                p.Mood += moodDelta;
                p.AddObservation(_players.potential.observationPerSegment);
            }

            // ---- 5b. random events (Story 006): choices, action events' personal effects, then state events; funds are booked
            var occurrences = new List<EventOccurrence>();
            foreach (var ch in _choices)
            {
                if ((ch.PlayerId != null && Find(ch.PlayerId.Value) == null) || (ch.PartnerId != null && Find(ch.PartnerId.Value) == null)) continue;
                int opt = EffectiveOption(plan, ch, out bool answered);
                occurrences.Add(_events.ResolveChoice(ch, opt, !answered));
            }
            occurrences.AddRange(actionEvents);
            foreach (var ev in occurrences) EventApplier.ApplyPersonal(ev, _team);
            if (_events != null)
            {
                var states = _events.RollStateEvents(_team);
                foreach (var ev in states) EventApplier.ApplyPersonal(ev, _team);
                occurrences.AddRange(states);
            }
            long eventIn = 0, eventOut = 0;
            foreach (var ev in occurrences)
            {
                long f = EventApplier.Funds(ev);
                if (f > 0) eventIn += f; else eventOut -= f;
            }
            summary.Events = occurrences;
            summary.EventIncome = ClampInt(eventIn);
            summary.EventExpenses = ClampInt(eventOut);

            // ---- 6. build SegmentInput (livestream income BEFORE settling, popularity still unchanged)
            var heat = new Dictionary<long, PlayerHeat>();
            long livestream = 0;
            foreach (var p in roster)
            {
                var ps = byId[p.Id];
                ps.StreamHeat = SegmentFormulas.StreamHeat(_cfg, p, ps.StreamPoints);
                ps.BusinessHeat = SegmentFormulas.BusinessHeat(_cfg, ps.BusinessPoints);
                ps.LivestreamIncome = _team.LivestreamIncome(p, ps.StreamPoints);
                // Story 006: stream events scale the MARGINAL income of their point (k-th stream = L(k+1) - L(k))
                long streamExtra = 0; int nth = 0;
                for (int k = 0; k < plan.Assignments.Count; k++)
                {
                    var a = plan.Assignments[k];
                    if (a.PlayerId != p.Id || a.Action != SegmentAction.Stream) continue;
                    var m = mods[k];
                    if (m.Fx.Count > 0)
                    {
                        int marginal = _team.LivestreamIncome(p, nth + 1) - _team.LivestreamIncome(p, nth);
                        long extra = (long)Math.Round(marginal * (double)(m.StreamMult - 1f) + m.StreamAdd, MidpointRounding.AwayFromZero);
                        foreach (var fx in m.Fx)
                            if (fx.Type == EffectType.StreamMultiplier) fx.Applied = (float)Math.Round(marginal * (double)(fx.Value - 1f));
                            else if (fx.Type == EffectType.StreamAdd) fx.Applied = fx.Value;
                        streamExtra += extra;
                    }
                    nth++;
                }
                ps.LivestreamIncome = ClampInt(Math.Max(0L, ps.LivestreamIncome + streamExtra));
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
                EventIncome = summary.EventIncome, EventExpenses = summary.EventExpenses,
            };

            // ---- 7. settle, then expire contracts and close
            summary.Report = _team.SettleSegment(input);
            foreach (var c in _contracts) c.SegmentsRemaining--;
            _contracts.RemoveAll(c => c.SegmentsRemaining <= 0);
            foreach (var ev in occurrences) EventApplier.ApplyAfterSettle(ev, _team);

            // ---- 8. growth.md F4: specialty check of every rostered player at segment end (after training, focus and events)
            if (_growth != null)
                foreach (var p in roster)
                {
                    var ps = byId[p.Id];
                    if (ps.FocusBefore == null) ps.FocusBefore = Focus(p);
                    var ch = _growth.CheckSpecialties(p);
                    ps.SpecialtiesGained.AddRange(ch.Gained);
                    ps.SpecialtiesLost.AddRange(ch.Lost);
                    ps.FocusAfter = Focus(p);
                }
            _offers.Clear();
            _choices.Clear();
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

        // Counts points per player. With skipInvalid, assignments that validation would reject are ignored (Preview of a draft);
        // otherwise the plan is already valid.
        private void TallyAssignments(SegmentPlan plan, Dictionary<long, PlayerSegmentSummary> byId, bool skipInvalid)
        {
            foreach (var a in plan.Assignments)
            {
                if (!byId.TryGetValue(a.PlayerId, out var ps)) continue;
                switch (a.Action)
                {
                    case SegmentAction.Train:
                        if (skipInvalid && !CheckTraining(Find(a.PlayerId), a).IsNone) continue;
                        ps.TrainPoints++;
                        break;
                    case SegmentAction.Stream: ps.StreamPoints++; break;
                    case SegmentAction.Rest: ps.RestPoints++; break;
                }
            }
        }

        // Preview: summed gains of the player's valid training points at a fixed noise, no event modifiers. Changes nothing.
        private float[] PreviewGains(SegmentPlan plan, Player p, float noise)
        {
            var total = new float[DimensionCount(p)];
            if (_growth == null) return total;
            foreach (var a in plan.Assignments)
            {
                if (a.PlayerId != p.Id || a.Action != SegmentAction.Train || !CheckTraining(p, a).IsNone) continue;
                float b = _growth.BaseAmount(p, a.Target, _cfg.training.intensity, noise);
                var g = _growth.PointGains(p, a.Target, a.FocusDimension, b);
                for (int d = 0; d < total.Length; d++) total[d] += g[d];
            }
            return total;
        }

        // Preview: focus after this segment's training (F3 on a copy of the current values).
        private float[] PreviewFocus(SegmentPlan plan, Player p)
        {
            var f = Focus(p);
            if (_growth == null) return f;
            var counts = new int[4];
            int n = 0;
            foreach (var a in plan.Assignments)
                if (a.PlayerId == p.Id && a.Action == SegmentAction.Train && CheckTraining(p, a).IsNone) { counts[(int)a.Target]++; n++; }
            if (n == 0) return f;
            float keep = (float)Math.Pow(1.0 - _players.training.specializationRate, n);
            for (int k = 0; k < f.Length; k++) f[k] = keep * f[k] + (1f - keep) * counts[k] / n;
            return f;
        }

        private static float[] Focus(Player p)
        {
            var f = new float[Player.FocusPhases];
            for (int k = 0; k < f.Length; k++) f[k] = p.GetFocus(k);
            return f;
        }

        private StressInputs StressFor(PlayerSegmentSummary ps, int matches, int losses, bool starter) => new StressInputs
        {
            TrainingIntensity = ps.TrainPoints * _cfg.training.intensity,
            Matches = matches, Losses = losses,
            Business = ps.BusinessPoints, Rest = ps.RestPoints, Streams = ps.StreamPoints,
        };

        private MoodInputs MoodFor(Player p, bool starter, int result, List<Player> starters) => new MoodInputs
        {
            Result = result, IsStarter = starter,
            SalarySatisfaction = SalarySatisfactionSource != null ? SalarySatisfactionSource(p) : SalarySatisfaction(p), Compatibility = MeanCompatibility(p, starters),
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

        private PendingChoice FindChoice(int id)
        {
            foreach (var c in _choices) if (c.Id == id) return c;
            return null;
        }

        // The answered option, else the choice's default.
        private static int EffectiveOption(SegmentPlan plan, PendingChoice ch, out bool answered)
        {
            foreach (var a in plan.ChoiceAnswers)
                if (a.ChoiceId == ch.Id) { answered = true; return a.Option; }
            answered = false;
            return ch.DefaultOption;
        }

        private static int IndexOf(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return i;
            return -1;
        }

        private static EventAction ToEventAction(SegmentAction a) =>
            a == SegmentAction.Train ? EventAction.Train : (a == SegmentAction.Stream ? EventAction.Stream : EventAction.Rest);

        // Modifiers one action event put on one action point (Story 006); defaults = no change.
        private sealed class PointMods
        {
            public float TrainMult = 1f, TrainAdd, StreamMult = 1f, StreamAdd, RestMult = 1f, BizMult = 1f, SponsorAdd;
            public bool Void;
            public readonly List<EventEffectResult> Fx = new List<EventEffectResult>();

            public void Add(EventEffectResult fx)
            {
                switch (fx.Type)
                {
                    case EffectType.TrainMultiplier: TrainMult *= fx.Value; break;
                    case EffectType.TrainAdd: TrainAdd += fx.Value; break;
                    case EffectType.StreamMultiplier: StreamMult *= fx.Value; break;
                    case EffectType.StreamAdd: StreamAdd += fx.Value; break;
                    case EffectType.RestMultiplier: RestMult *= fx.Value; break;
                    case EffectType.BusinessMultiplier: BizMult *= fx.Value; break;
                    case EffectType.SponsorChanceAdd: SponsorAdd += fx.Value; break;
                    case EffectType.VoidAction: Void = true; break;
                    default: return;
                }
                Fx.Add(fx);
            }
        }

        private static int ClampInt(long v) => v > int.MaxValue ? int.MaxValue : (int)v;
    }
}
