using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using EsportsManager.Events;
using EsportsManager.Growth;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Session;
using EsportsManager.Teams;

namespace EsportsManager.Leagues
{
    /// <summary>Kinds of league news (league.md 联赛新闻).</summary>
    public enum NewsKind
    {
        /// <summary>An AI club switched style.</summary>
        StyleSwitch = 0,
        /// <summary>A club signed a player (emergency / yearly fill; transfers are Story 005).</summary>
        Signing = 1,
        /// <summary>A club sold a player to the league in self-rescue.</summary>
        FireSale = 2,
        /// <summary>An AI club borrowed from the league.</summary>
        Loan = 3,
        /// <summary>A starter gained a specialty.</summary>
        Specialty = 4,
        /// <summary>Event champion.</summary>
        Champion = 5,
        /// <summary>A poach transfer (club = buyer, other = seller, amount = fee).</summary>
        Poach = 6,
        /// <summary>An auction purchase (club = buyer, other = seller or -1 for a league lot, amount = price).</summary>
        AuctionBuy = 7,
        /// <summary>A forced listing (refused renegotiation or debt over the line).</summary>
        ForcedListing = 8,
        /// <summary>A player kept with a retain raise (club = seller, other = bidder).</summary>
        Retain = 9,
        /// <summary>A listed player nobody bought, sold to the league at the fire-sale price (club = seller, amount = price).</summary>
        UnsoldSale = 10,
    }

    /// <summary>Kinds of notices for the player club (shown as a pop-up on the next settlement / main screen).</summary>
    public enum NoticeKind
    {
        /// <summary>Self-rescue sold a player to the league (economy F7).</summary>
        FireSale = 0,
        /// <summary>A financial crisis started (amount = deficit, segments = grace).</summary>
        CrisisStart = 1,
        /// <summary>A player put his own business earnings into the club.</summary>
        ChipIn = 2,
        /// <summary>The crisis is over (funds back at 0 or above).</summary>
        CrisisOver = 3,
    }

    /// <summary>One notice for the player club (data only; the UI formats it).</summary>
    public sealed class ClubNotice
    {
        /// <summary>Kind.</summary>
        public NoticeKind Kind;
        /// <summary>Player name (fire sale, chip-in), else empty.</summary>
        public string PlayerName = "";
        /// <summary>Money (sale price, chip-in, deficit), yuan.</summary>
        public long Amount;
        /// <summary>Crisis segments left (crisis start).</summary>
        public int SegmentsLeft;
    }

    /// <summary>One news line (data only; the UI formats it).</summary>
    public sealed class NewsItem
    {
        /// <summary>Year (0 = warm-up).</summary>
        public int Year;
        /// <summary>1-based segment, or 0 at the year end / transfer step.</summary>
        public int Segment;
        /// <summary>Kind.</summary>
        public NewsKind Kind;
        /// <summary>Club id and name (snapshot).</summary>
        public int ClubId;
        /// <summary>Club name when the news happened.</summary>
        public string ClubName;
        /// <summary>Player name (signing, sale, specialty), else empty.</summary>
        public string PlayerName = "";
        /// <summary>Style switch: old / new.</summary>
        public ClubStyle From, To;
        /// <summary>Event name key (champion) or specialty id.</summary>
        public string Key = "";
        /// <summary>Money involved (loan, sale), yuan.</summary>
        public long Amount;
        /// <summary>The other club of a transfer (seller / bidder), -1 = the league.</summary>
        public int OtherClubId = -1;
        /// <summary>Name of the other club (snapshot), empty for the league.</summary>
        public string OtherClubName = "";
    }

    /// <summary>Offer tier (league.md 三份聘书).</summary>
    public enum OfferTier
    {
        /// <summary>Strong club (strength rank 3-4).</summary>
        Strong = 0,
        /// <summary>Mid club (4-5).</summary>
        Mid = 1,
        /// <summary>Weak club (6-7).</summary>
        Weak = 2,
    }

    /// <summary>A job offer after the warm-up.</summary>
    public sealed class JobOffer
    {
        /// <summary>Tier.</summary>
        public OfferTier Tier;
        /// <summary>Club id.</summary>
        public int ClubId;
        /// <summary>Strength rank at the end of the warm-up (1 = strongest).</summary>
        public int StrengthRank;
        /// <summary>Overall place of the warm-up year (finals placing, then the qualification order).</summary>
        public int LastYearRank;
    }

    /// <summary>
    /// The whole league (design/gdd/league.md rev 3, talent.md rev 3): 8 clubs in ONE model, the free-agent pool, the schedule,
    /// the season flow, the keyed random numbers, the AI, news and the warm-up with three job offers. Every club runs the same
    /// segment pipeline (<see cref="SegmentRunner"/>) and event effects; the AI only decides.
    /// </summary>
    /// <remarks>
    /// SEGMENT ORDER (league.md 正常): every club plans (the player's plan comes from the session) -> settles (club id order) ->
    /// event (all clubs' lineups) -> prizes -> self-rescue -> next segment. Year end (after the last segment / the finals):
    /// style evaluation, then the talent rollover (age + 1, retirements, youth + free agents, pool cap) and the AI's yearly fill
    /// (stand-in for the transfer window until Story 005). Shared-pool operations run in club id order.
    /// </remarks>
    public sealed class LeagueWorld
    {
        /// <summary>Player config.</summary>
        public PlayerConfig Players { get; }
        /// <summary>Team config.</summary>
        public TeamConfig TeamConfig { get; }
        /// <summary>Segment config.</summary>
        public SegmentConfig SegmentConfig { get; }
        /// <summary>Match config.</summary>
        public MatchConfig Match { get; }
        /// <summary>Event config (null = no random events).</summary>
        public EventConfig EventConfig { get; }
        /// <summary>League config.</summary>
        public LeagueConfig Config { get; }
        /// <summary>Keyed random numbers.</summary>
        public KeyedRandom Keys { get; }
        /// <summary>Growth system (shared, stateless).</summary>
        public GrowthSystem Growth { get; }
        /// <summary>Talent system.</summary>
        public TalentSystem Talent { get; }
        /// <summary>Economy formulas.</summary>
        public EconomyRules Economy { get; }
        /// <summary>The AI.</summary>
        public LeagueAi Ai { get; }
        /// <summary>Schedule and names.</summary>
        public League Schedule { get; }
        /// <summary>Tournament runner.</summary>
        public TournamentRunner Tournament { get; }
        /// <summary>Calendar / event state machine.</summary>
        public SeasonFlow Season { get; }
        /// <summary>Clubs by id.</summary>
        public List<Club> Clubs { get; } = new List<Club>();
        /// <summary>Free agents (shared pool).</summary>
        public List<Player> FreeAgents { get; } = new List<Player>();
        /// <summary>News, newest last (at most news.max_items).</summary>
        public List<NewsItem> News { get; } = new List<NewsItem>();
        /// <summary>The three offers (after the warm-up), else empty.</summary>
        public List<JobOffer> Offers { get; } = new List<JobOffer>();
        /// <summary>The open transfer window (null outside it).</summary>
        public TransferWindow Window { get; private set; }
        /// <summary>The last finished transfer window (year review, UI), or null.</summary>
        public TransferWindow LastWindow { get; private set; }
        /// <summary>Wage index of the previous years (index = year of the window that set it), for the year review.</summary>
        public List<double> WageIndexHistory { get; } = new List<double>();
        /// <summary>Funds injected at the takeover (not league income).</summary>
        public long Injected { get; private set; }
        /// <summary>Notices for the player club not yet acknowledged (fire sales, crisis, chip-ins), oldest first.</summary>
        public List<ClubNotice> Notices { get; } = new List<ClubNotice>();
        /// <summary>Times the player manager was fired (year review).</summary>
        public int Dismissals { get; private set; }
        /// <summary>True between a dismissal and taking one of the new offers.</summary>
        public bool DismissalPending { get; private set; }
        /// <summary>The club the manager was last fired from (-1 = never) and its deficit at that moment.</summary>
        public int FiredFromClubId { get; private set; } = -1;
        /// <summary>Deficit (positive yuan) of the club at the last dismissal.</summary>
        public long FiredDeficit { get; private set; }

        /// <summary>Current year (0 = warm-up).</summary>
        public int Year => Season.Year;
        /// <summary>Segments per year.</summary>
        public int SegmentsPerYear => Match.calendar.segmentsPerYear;
        /// <summary>The player club id, or -1.</summary>
        public int PlayerClubId => Schedule.PlayerClub;
        /// <summary>The player club, or null during the warm-up.</summary>
        public Club PlayerClub => PlayerClubId >= 0 ? Clubs[PlayerClubId] : null;

        private readonly Dictionary<int, float[]> _strengthCache = new Dictionary<int, float[]>();
        private readonly Dictionary<long, PlayerMarketState> _market = new Dictionary<long, PlayerMarketState>();

        /// <summary>The market state of a player (created on first use).</summary>
        public PlayerMarketState Market(Player p)
        {
            if (!_market.TryGetValue(p.Id, out var m)) { m = new PlayerMarketState(); _market[p.Id] = m; }
            return m;
        }

        /// <summary>Global segment index year x segments per year + index.</summary>
        public int GlobalSegment(int year, int index) => year * SegmentsPerYear + index;

        /// <summary>Current wage index WI (economy F3).</summary>
        public double WageIndex => Economy.WageIndex;
        private bool _cacheValid;

        /// <summary>Generates the league (year 0, segment 1 not yet open). Deterministic for the seed.</summary>
        public LeagueWorld(PlayerConfig players, TeamConfig team, SegmentConfig segment, MatchConfig match, EventConfig events, LeagueConfig league, long seed)
        {
            Players = players ?? throw new ArgumentNullException(nameof(players));
            TeamConfig = team ?? throw new ArgumentNullException(nameof(team));
            SegmentConfig = segment ?? throw new ArgumentNullException(nameof(segment));
            Match = match ?? throw new ArgumentNullException(nameof(match));
            EventConfig = events;
            Config = league ?? throw new ArgumentNullException(nameof(league));
            match.Validate();
            match.ValidateAgainst(players);
            league.Validate();
            Keys = new KeyedRandom(seed);
            var sim = new MatchSimulator(match, team, players);
            Growth = new GrowthSystem(players, new GrowthPhaseMap(sim.Layout));
            Talent = new TalentSystem(players, league.talent, Growth);
            Economy = new EconomyRules(players, league.economy);
            Economy.HonorOf = p => _market.TryGetValue(p.Id, out var m) ? m.Honor : 0f;
            Ai = new LeagueAi(this);

            var gen = Keys.Stream("gen", 0);
            var names = new List<string>(league.clubNames);
            Shuffle(names, gen);
            names.RemoveRange(league.clubCount, names.Count - league.clubCount);
            int slots = league.clubCount - 1;
            var order = new List<int>();
            for (int i = 1; i <= slots; i++) order.Add(i);
            Shuffle(order, gen);
            Schedule = new League(names, order);
            Tournament = new TournamentRunner(Schedule, match, team, players,
                (y, e, st, slot) => Keys.Stream("match", y, e, st, slot)) { HookBuilder = s => Growth.BuildHooks(s) };
            Generate(gen, names);
            Season = new SeasonFlow(this, 0);
        }

        // ------------------------------------------------------------ generation (league.md 联赛构成与开局, F1)

        private void Generate(KeyedStream gen, List<string> names)
        {
            var g = Config.generation;
            int n = Config.clubCount;
            var deck = new List<ClubStyle>();
            for (int i = 0; i < n; i++) deck.Add((ClubStyle)(i % 4));
            Shuffle(deck, gen);
            for (int i = 0; i < n; i++)
            {
                var segRng = Keys.Rekeyable("seg", i);
                var teamRng = Keys.Rekeyable("pop", i);
                var evRng = Keys.Rekeyable("ev", i);
                var noise = Keys.Rekeyable("noise", i);
                var t = new Team(TeamConfig, Players, teamRng, 0, names[i]);
                var ev = EventConfig != null ? new EventEngine(EventConfig, Players, evRng) : null;
                var runner = new SegmentRunner(SegmentConfig, Players, t, segRng, ev, Growth, noise) { SalarySatisfactionSource = Economy.Satisfaction };
                var c = new Club(i, t, runner, ev, segRng, teamRng, evRng, noise) { Style = deck[i] };
                if (c.Style == ClubStyle.Star) c.StarLane = Players.lanes[gen.NextInt(0, Players.lanes.Length)].id;
                var pe = c.Personality;
                pe.Aggression = (float)gen.NextDouble(); pe.Frugality = (float)gen.NextDouble(); pe.Adaptability = (float)gen.NextDouble();
                pe.OffRest = gen.NextFloat(-g.offsetRest, g.offsetRest);
                pe.OffCash = gen.NextFloat(-g.offsetCash, g.offsetCash);
                pe.OffRenew = gen.NextFloat(-g.offsetRenew, g.offsetRenew);
                pe.OffBid = gen.NextFloat(-g.offsetBid, g.offsetBid);
                t.Funds = (long)Math.Round(gen.NextFloat(g.fundsMin, g.fundsMax));
                t.ClubPopularity = gen.NextFloat(g.popularityMin, g.popularityMax);
                for (int s = 0; s < g.openingSponsors; s++)
                    runner.AddContract(new SponsorContract(SegmentFormulas.ContractAmount(SegmentConfig, t.ClubPopularity),
                        gen.NextInt(g.sponsorSegmentsMin, g.sponsorSegmentsMax + 1)));
                Clubs.Add(c);
            }
            // talent pool (talent.md 开局联赛: mu with league popularity 50)
            float mu = Talent.SupplyMean(g.openingLeaguePopularity);
            var trng = Keys.Stream("talent", 0, -1);
            var pool = new List<Player>();
            foreach (var lane in Players.lanes)
                for (int k = 0; k < g.poolPerLane; k++) pool.Add(Talent.Generate(trng, lane.id, g.poolAgeMin, g.poolAgeMax, mu));
            // snake draft (F1)
            var draftOrder = new List<int>();
            for (int i = 0; i < n; i++) draftOrder.Add(i);
            Shuffle(draftOrder, gen);
            var extra = new List<int>(draftOrder);
            Shuffle(extra, gen);
            var sevens = new HashSet<int>(extra.GetRange(0, g.rosterExtraClubs));
            for (int rnd = 0; rnd <= g.rosterBase; rnd++)
            {
                var seq = new List<int>(draftOrder);
                if (rnd % 2 == 1) seq.Reverse();
                foreach (int ci in seq)
                {
                    if (rnd == g.rosterBase && !sevens.Contains(ci)) continue;
                    var c = Clubs[ci];
                    var have = new HashSet<string>();
                    foreach (var p in c.Team.Roster) have.Add(p.MainLane);
                    Player best = null; float bestS = float.NegativeInfinity;
                    foreach (var p in pool)
                    {
                        if (have.Count < Players.lanes.Length && have.Contains(p.MainLane)) continue;
                        float s = DraftScore(c, p, have);
                        if (s > bestS || (s == bestS && best != null && p.Id < best.Id)) { best = p; bestS = s; }
                    }
                    pool.Remove(best);
                    int years = gen.NextInt(g.contractYearsMin, g.contractYearsMax + 1);
                    best.ContractYearsLeft = years;
                    best.Salary = Economy.SigningSalary(best, years, gen, g.openingSalaryFactor);
                    c.Team.AddPlayer(best);
                }
            }
            FreeAgents.AddRange(pool);
            foreach (var c in Clubs)
            {
                Ai.PickLineup(c);
                if (c.Style == ClubStyle.Star) SetStar(c, c.StarLane);
                var st = c.Team.Starters();
                for (int i = 0; i < st.Count; i++)
                    for (int j = i + 1; j < st.Count; j++) c.Team.SetFamiliarity(st[i].Id, st[j].Id, gen.NextFloat(g.familiarityMin, g.familiarityMax));
            }
        }

        /// <summary>F1 draft score: R_main + style_w x r(style phase) - age_pen x max(0, age - 26); Star: star_w x max(r_E, r_L) for the first star-lane pick.</summary>
        public float DraftScore(Club c, Player p, ICollection<string> haveLanes)
        {
            var g = Config.generation;
            float bonus = 0f;
            if (c.Style != ClubStyle.Star) bonus = g.styleWeight * Ai.PhaseRating(p, (int)c.Style);
            else if (p.MainLane == c.StarLane && !haveLanes.Contains(p.MainLane)) bonus = g.starWeight * Math.Max(Ai.PhaseRating(p, 0), Ai.PhaseRating(p, 2));
            return p.GetLaneScore(p.MainLane) + bonus - g.agePenalty * Math.Max(0, p.Age - g.agePenaltyFrom);
        }

        private void SetStar(Club c, string lane)
        {
            var sp = c.Team.GetStarter(lane);
            c.StarLane = lane;
            c.StarPlayerId = sp != null ? sp.Id : -1;
            c.StarPhase = sp != null && Ai.PhaseRating(sp, 0) < Ai.PhaseRating(sp, 2) ? 2 : 0;
        }

        // ------------------------------------------------------------ queries

        /// <summary>True when an event follows segment <paramref name="segmentIndex"/> (0-based) of the year.</summary>
        public bool IsPreEventSegment(int segmentIndex) => Match.calendar.EventAfter(segmentIndex + 1) != null;

        /// <summary>
        /// S_E / S_M / S_L of a club's current lineup (match preview; the tactic does not change S), or null when the lineup is
        /// incomplete. Cached until the next state change (<see cref="Invalidate"/>).
        /// </summary>
        public float[] PhaseStrength(Club c)
        {
            if (!_cacheValid) { _strengthCache.Clear(); _cacheValid = true; }
            if (_strengthCache.TryGetValue(c.Id, out var s)) return s;
            s = null;
            if (c.Team.Starters().Count == Players.lanes.Length)
            {
                var ev = Tournament.Simulator.Preview(Tournament.LineupOf(c.Team, MatchTactic.Invade));
                s = new[] { (float)ev.Strength[0], (float)ev.Strength[1], (float)ev.Strength[2] };
            }
            _strengthCache[c.Id] = s;
            return s;
        }

        /// <summary>Drops cached strengths (call after any roster / lineup / player change).</summary>
        public void Invalidate() => _cacheValid = false;

        /// <summary>League popularity = mean club popularity (talent.md).</summary>
        public float LeaguePopularity()
        {
            float s = 0f;
            foreach (var c in Clubs) s += c.Team.ClubPopularity;
            return s / Clubs.Count;
        }

        /// <summary>
        /// Star-lane lead: max over lanes of (the club's main-lane starter's rating - median of every club's starter rating in
        /// that lane); lane null when the club has no main-lane starter.
        /// </summary>
        public float StarLaneLead(Club c, out string lane)
        {
            lane = null; float best = float.NegativeInfinity;
            foreach (var l in Players.lanes)
            {
                var p = c.Team.GetStarter(l.id);
                if (p == null || p.MainLane != l.id) continue;
                var vals = new List<float>();
                foreach (var o in Clubs) { var q = o.Team.GetStarter(l.id); if (q != null) vals.Add(q.GetLaneScore(l.id)); }
                float d = p.GetLaneScore(l.id) - LeagueAi.Median(vals);
                if (d > best) { best = d; lane = l.id; }
            }
            return best;
        }

        /// <summary>
        /// Club style label (league.md 俱乐部风格标签, every club incl. the player's): Star when the star-lane lead >= 15, else the
        /// phase with the largest lead over the league median of S. Null when the lineup is incomplete.
        /// </summary>
        public ClubStyle? StyleLabel(Club c)
        {
            var me = PhaseStrength(c);
            if (me == null) return null;
            if (StarLaneLead(c, out _) >= Config.style.starLabelLead) return ClubStyle.Star;
            return (ClubStyle)LeagueAi.ArgMax(GapToMedian(c, me, false));
        }

        private float[] GapToMedian(Club c, float[] me, bool radarOthers)
        {
            var gap = new float[3];
            for (int k = 0; k < 3; k++)
            {
                var vals = new List<float>();
                foreach (var o in Clubs)
                {
                    var s = o.Id == c.Id ? me : radarOthers ? Ai.Radar(PhaseStrength(o)) : PhaseStrength(o);
                    if (s != null) vals.Add(s[k]);
                }
                gap[k] = me[k] - LeagueAi.Median(vals);
            }
            return gap;
        }

        /// <summary>
        /// 当前榜首: the club with the most finals points from this year's completed splits; before the first event of the year the
        /// previous finals champion; null on a tie or when nothing is known yet.
        /// </summary>
        public Club CurrentLeader()
        {
            var year = Season.ThisYear;
            var splits = new List<EventResult>();
            foreach (var r in year.Events) if (r.Event.IsComplete && r.Event.Type == EventType.Split) splits.Add(r.Event);
            if (splits.Count == 0)
            {
                foreach (var c in Clubs) if (c.LastYearFinalsPlace == 1) return c;
                return null;
            }
            var pts = new int[Clubs.Count];
            foreach (var e in splits) for (int c = 0; c < pts.Length; c++) pts[c] += e.Points[c];
            int best = 0; bool tie = false;
            for (int c = 1; c < pts.Length; c++)
            {
                if (pts[c] > pts[best]) { best = c; tie = false; }
                else if (pts[c] == pts[best]) tie = true;
            }
            return tie ? null : Clubs[best];
        }

        // ------------------------------------------------------------ segment pipeline

        /// <summary>Opens the next segment for every club (re-keys their streams, rolls offers and choices).</summary>
        public void BeginSegment()
        {
            if (Window != null) throw new InvalidOperationException("Finish the transfer window first.");
            int seg = Season.SegmentIndexInYear;
            foreach (var c in Clubs)
            {
                c.SegmentRng.Rekey(Year, seg); c.TeamRng.Rekey(Year, seg); c.EventRng.Rekey(Year, seg); c.NoiseRng.Rekey(Year, seg);
                c.Runner.BeginSegment();
                RollMedia(c, seg);
            }
            Invalidate();
        }

        /// <summary>The calendar context of the open segment.</summary>
        public SegmentContext Context() => new SegmentContext
        {
            SegmentsPerYear = SegmentsPerYear, SegmentIndexInYear = Season.SegmentIndexInYear, MatchResults = null, SeasonRankScore = 50f, PrizeMoney = 0,
        };

        /// <summary>
        /// Resolves the open segment for every club in id order: the player club with <paramref name="playerPlan"/> (validated FIRST,
        /// so an invalid plan throws before any club changes), the AI clubs with their own plans. Then the season flow opens an
        /// event or (no event) runs the self-rescue check. Returns the player's summary (null during the warm-up).
        /// </summary>
        public SegmentSummary ResolveSegment(SegmentPlan playerPlan)
        {
            var ctx = Context();
            int seg = ctx.SegmentIndexInYear;
            var player = PlayerClub;
            if (player != null) player.Runner.Validate(playerPlan ?? throw new ArgumentNullException(nameof(playerPlan)), ctx);
            SegmentSummary mine = null;
            foreach (var c in Clubs)
            {
                SegmentSummary sum;
                long wages = c.Team.SalaryShare(ctx.SegmentsPerYear, seg);
                var plan = c == player ? playerPlan : Ai.Plan(c, seg);
                sum = c.Runner.Resolve(plan, ctx);
                if (c == player) mine = sum;
                ApplyMedia(c, plan, sum);
                c.Ledger.Income += sum.MediaIncome;
                c.Ledger.Wages += wages;
                c.Ledger.Income += (long)sum.LivestreamIncome + sum.BusinessIncome + sum.SponsorshipIncome + sum.EventIncome;
                c.Ledger.EventExpenses += sum.EventExpenses;
                foreach (var ps in sum.Players)
                {
                    if (ps.SpecialtiesGained.Count == 0) continue;
                    var p = c.Find(ps.PlayerId);
                    if (p != null && c.Team.LaneOf(p) != null)
                        foreach (var id in ps.SpecialtiesGained) AddNews(new NewsItem { Kind = NewsKind.Specialty, ClubId = c.Id, PlayerName = p.Name, Key = id, Segment = seg + 1 });
                }
            }
            Invalidate();
            Season.OnSegmentResolved();
            if (Season.Phase == EventPhase.None) FundsCheckAll(seg + 1);
            return mine;
        }

        /// <summary>Runs the remaining warm-up year (all AI): segments and events, then the year rollover; then makes the offers.</summary>
        public void RunWarmUp()
        {
            if (PlayerClubId >= 0) throw new InvalidOperationException("The league already has a player club.");
            while (Year == 0)
            {
                BeginSegment();
                ResolveSegment(null);
                if (Season.Phase != EventPhase.None) Season.AutoPlay();
            }
            MakeOffers();
        }

        /// <summary>Plays one full all-AI year (tests, performance): segments, events, rollover.</summary>
        public void RunAiYear()
        {
            int y = Year;
            while (Year == y)
            {
                BeginSegment();
                if (PlayerClub != null) throw new InvalidOperationException("RunAiYear needs an all-AI league.");
                ResolveSegment(null);
                if (Season.Phase != EventPhase.None) Season.AutoPlay();
            }
        }

        // ------------------------------------------------------------ events (called by SeasonFlow)

        /// <summary>The lineup of an AI club for a stage: lineup rule 1, then the stage tactic; the tactic is recorded.</summary>
        internal MatchLineup AiLineup(int clubId, EventResult ev, bool bracket)
        {
            var c = Clubs[clubId];
            MatchTactic t;
            if (!bracket) t = Ai.RegularTactic(c, ev.EventIndex);
            else { int opp = ev.FirstOpponent(clubId); t = Ai.PlayoffTactic(c, opp >= 0 ? Clubs[opp] : null); }
            c.RecordTactic(t);
            return Tournament.LineupOf(c.Team, t);
        }

        /// <summary>AI lineups (rule 1) of all AI clubs before a stage (so the tactic medians see the final lineups).</summary>
        internal void PrepareAiLineups(IEnumerable<int> clubs)
        {
            foreach (int id in clubs) if (id != PlayerClubId) Ai.PickLineup(Clubs[id]);
            Invalidate();
        }

        /// <summary>Stage effects (stress, win familiarity) for every club; returns the player's list (empty without one).</summary>
        internal List<EventPlayerEffect> ApplyStage(EventResult ev, List<EventSeries> series, int stageNo)
        {
            List<EventPlayerEffect> mine = new List<EventPlayerEffect>();
            foreach (var c in Clubs)
            {
                var l = EventEffects.ApplyStage(SegmentConfig, c.Team, c.Id, series, Keys.Stream("stage", ev.Year, ev.EventIndex, stageNo, c.Id));
                if (c.Id == PlayerClubId) mine = l;
            }
            Invalidate();
            return mine;
        }

        /// <summary>Event end for every club: mood effects, recent results, prizes (AI in debt repay 50%), placings, champion news, self-rescue.</summary>
        internal List<EventPlayerEffect> FinishEvent(EventResult ev)
        {
            List<EventPlayerEffect> mine = new List<EventPlayerEffect>();
            foreach (var c in Clubs)
            {
                var l = EventEffects.ApplyEventEnd(SegmentConfig, c.Team, c.Id, ev.SeriesOf(c.Id), Keys.Stream("eventend", ev.Year, ev.EventIndex, c.Id));
                if (c.Id == PlayerClubId) mine = l;
                c.YearPlacings.Add(ev.Placing[c.Id]);
                if (c.Id != PlayerClubId) PayIn(c, ev.Prize[c.Id]);
                c.Ledger.Income += ev.Prize[c.Id]; c.Ledger.Prizes += ev.Prize[c.Id];
                float honor = Economy.HonorForPlace(ev.Placing[c.Id], ev.Type == EventType.Finals);
                foreach (var p in c.Team.Roster) { var m = Market(p); m.HonorThisYear = Math.Max(m.HonorThisYear, honor); }
            }
            int champ = ev.FinalOrder[0];
            AddNews(new NewsItem { Kind = NewsKind.Champion, ClubId = champ, Key = ev.Def.nameKey, Segment = Season.SegmentsDoneInYear });
            Invalidate();
            return mine;
        }

        /// <summary>Self-rescue check after the event's prizes (called by SeasonFlow when the event is done).</summary>
        internal void AfterEvent() => FundsCheckAll(Season.SegmentsDoneInYear);

        /// <summary>Income a club receives (prize, sale, fee); an AI club in debt repays repay share of it first (economy F7).</summary>
        internal void PayIn(Club c, long amount)
        {
            if (amount <= 0) return;
            if (c.Debt > 0 && !c.IsPlayer)
            {
                long rep = Math.Min(c.Debt, (long)Math.Round(amount * (double)Economy.RepayShare));
                c.Debt -= rep;
                c.Team.Funds += amount - rep;
            }
            else c.Team.ReceivePrize(amount);
        }

        // ------------------------------------------------------------ economy F7

        private void FundsCheckAll(int segment)
        {
            foreach (var c in Clubs) FundsCheck(c, segment);
        }

        /// <summary>
        /// economy.md F7 self-rescue (every club): while funds &lt; 0 and the roster has more than 5 players, sell the lowest-rated
        /// non-starter to the league at value x 0.6 (into the free pool; the player club gets a <see cref="NoticeKind.FireSale"/>
        /// notice). Still &lt; 0: an AI club borrows up to 0; the player club goes through the financial crisis
        /// (<see cref="CrisisCheck"/>). Called once per segment end (after the event's prizes when there is one).
        /// </summary>
        public void FundsCheck(Club c, int segment)
        {
            if (c.Team.Funds < 0)
                while (c.Team.Funds < 0 && c.Team.Roster.Count > Config.ai.minRoster)
                {
                    Player worst = null;
                    foreach (var p in c.Team.Roster)
                        if (c.Team.LaneOf(p) == null && (worst == null || p.GetLaneScore(p.MainLane) < worst.GetLaneScore(worst.MainLane))) worst = p;
                    if (worst == null) break;
                    long price = Economy.FireSalePrice(worst);
                    PayIn(c, price);
                    Release(c, worst);
                    AddNews(new NewsItem { Kind = NewsKind.FireSale, ClubId = c.Id, PlayerName = worst.Name, Amount = price, Segment = segment });
                    if (c.IsPlayer) Notices.Add(new ClubNotice { Kind = NoticeKind.FireSale, PlayerName = worst.Name, Amount = price });
                }
            if (c.IsPlayer) { CrisisCheck(c, segment); return; }
            if (c.Team.Funds >= 0) return;
            long loan = -c.Team.Funds;
            c.Debt += loan; c.LoanTaken += loan;
            c.Team.Funds = 0;
            AddNews(new NewsItem { Kind = NewsKind.Loan, ClubId = c.Id, Amount = loan, Segment = segment });
        }

        /// <summary>
        /// economy.md 财务危机与解雇 (player club, after the self-rescue): funds &gt;= 0 ends a running crisis. Funds &lt; 0 without a
        /// crisis starts one with crisis.grace_segments checks left. In a crisis each check first rolls the chip-ins
        /// (<see cref="ChipIns"/>); still &lt; 0 uses up one check, and the last one fires the manager (<see cref="Fire"/>).
        /// </summary>
        internal void CrisisCheck(Club c, int segment)
        {
            if (c.Team.Funds >= 0)
            {
                if (c.InCrisis) { c.CrisisSegmentsLeft = 0; Notices.Add(new ClubNotice { Kind = NoticeKind.CrisisOver }); }
                return;
            }
            if (!c.InCrisis)
            {
                c.CrisisSegmentsLeft = Config.crisis.graceSegments;
                Notices.Add(new ClubNotice { Kind = NoticeKind.CrisisStart, Amount = -c.Team.Funds, SegmentsLeft = c.CrisisSegmentsLeft });
                return;
            }
            ChipIns(c, segment);
            if (c.Team.Funds >= 0) { c.CrisisSegmentsLeft = 0; Notices.Add(new ClubNotice { Kind = NoticeKind.CrisisOver }); return; }
            c.CrisisSegmentsLeft--;
            if (c.CrisisSegmentsLeft <= 0) Fire(c, segment);
        }

        /// <summary>
        /// Chip-ins of one crisis segment: when team chemistry &gt;= chipin_chemistry, every rostered player with mood &gt;=
        /// chipin_mood puts in min(remaining deficit, his personal business earnings) with chance chipin_chance (key "chipin", year,
        /// segment, club, player), in roster order until the deficit is gone. Not league income. Returns the total.
        /// </summary>
        public long ChipIns(Club c, int segment)
        {
            var k = Config.crisis;
            long total = 0;
            if (c.Team.Chemistry() < k.chipinChemistry) return 0;
            foreach (var p in new List<Player>(c.Team.Roster))
            {
                if (c.Team.Funds >= 0) break;
                if (p.Mood < k.chipinMood || p.PersonalBusinessEarnings <= 0) continue;
                if (Keys.U01("chipin", Year, segment, c.Id, p.Id) >= k.chipinChance) continue;
                long give = Math.Min(-c.Team.Funds, p.PersonalBusinessEarnings);
                p.PersonalBusinessEarnings -= give;
                c.Team.Funds += give;
                total += give;
                if (c.IsPlayer) Notices.Add(new ClubNotice { Kind = NoticeKind.ChipIn, PlayerName = p.Name, Amount = give });
            }
            return total;
        }

        /// <summary>
        /// The player manager is fired (economy.md 财务危机与解雇): the club becomes AI-run and its deficit an AI loan (F7), the
        /// dismissal is counted and three job offers from clubs ranked crisis.dismissal_ranks by strength (old club excluded) wait
        /// for <see cref="TakeOver"/> (weak-offer capital top-up). The season goes on.
        /// </summary>
        internal void Fire(Club c, int segment)
        {
            long deficit = Math.Max(0, -c.Team.Funds);
            c.CrisisSegmentsLeft = 0;
            c.IsPlayer = false;
            c.Bankrupt = true;
            Schedule.PlayerClub = -1;
            if (deficit > 0)
            {
                c.Debt += deficit; c.LoanTaken += deficit;
                c.Team.Funds = 0;
                AddNews(new NewsItem { Kind = NewsKind.Loan, ClubId = c.Id, Amount = deficit, Segment = segment });
            }
            Dismissals++;
            DismissalPending = true;
            FiredFromClubId = c.Id;
            FiredDeficit = deficit;
            Notices.Clear();
            Ai.PickLineup(c);
            Invalidate();
            MakeDismissalOffers(c.Id);
        }

        /// <summary>The job offers after a dismissal: the first dismissal_offers clubs of strength ranks [lo, hi] other than the old club.</summary>
        public void MakeDismissalOffers(int excludeClubId)
        {
            Offers.Clear();
            var rank = StrengthRanking();
            var k = Config.crisis;
            for (int r = k.dismissalRanks[0]; r <= rank.Count && Offers.Count < k.dismissalOffers; r++)
            {
                var c = rank[r - 1];
                if (c.Id == excludeClubId) continue; // past hi only when the old club sat inside a range of exactly dismissal_offers
                Offers.Add(new JobOffer { Tier = OfferTier.Weak, ClubId = c.Id, StrengthRank = r, LastYearRank = YearRank(Year - 1, c.Id) });
            }
        }

        /// <summary>
        /// End of the auction (transfer.md 6): a listed player nobody bought is sold to the league at the fire-sale price into the
        /// free pool -- unless the club would drop below ai.min_roster or he is the last main-lane player of his lane (then he
        /// stays). True when sold.
        /// </summary>
        public bool SellUnsoldToLeague(Club c, Player p)
        {
            if (c.Find(p.Id) == null || c.Team.Roster.Count - 1 < Config.ai.minRoster) return false;
            int lane = 0;
            foreach (var q in c.Team.Roster) if (q.MainLane == p.MainLane) lane++;
            if (lane <= 1) return false;
            long price = Economy.FireSalePrice(p);
            PayIn(c, price);
            Release(c, p);
            AddNews(new NewsItem { Kind = NewsKind.UnsoldSale, ClubId = c.Id, PlayerName = p.Name, Amount = price });
            return true;
        }

        // ------------------------------------------------------------ media invitations (economy.md 媒体邀约)

        /// <summary>Chance of one invitation: clamp(chance_base + chance_per_popularity x popularity / 100, 0, chance_max).</summary>
        public float MediaChance(float clubPopularity)
        {
            var m = Config.media;
            return Math.Max(0f, Math.Min(m.chanceMax, m.chanceBase + m.chancePerPopularity * clubPopularity / 100f));
        }

        /// <summary>Income of an accepted invitation: income_livestream_mult x livestream base + income_per_popularity x popularity.</summary>
        public long MediaIncome(float clubPopularity)
        {
            var m = Config.media;
            return (long)Math.Round(m.incomeLivestreamMult * (double)TeamConfig.livestream.baseIncome + m.incomePerPopularity * (double)clubPopularity);
        }

        /// <summary>Rolls the club's invitations of the open segment (max_per_segment draws, key "media", year, segment, club).</summary>
        internal void RollMedia(Club c, int seg)
        {
            var m = Config.media;
            var rng = Keys.Stream("media", Year, seg, c.Id);
            float chance = MediaChance(c.Team.ClubPopularity);
            for (int i = 0; i < m.maxPerSegment; i++)
                if (rng.NextDouble() < chance) c.Runner.MediaInvitations.Add(new MediaInvitation { Id = i + 1, Income = MediaIncome(c.Team.ClubPopularity) });
        }

        /// <summary>
        /// After the club's segment resolved: each accepted invitation pays its income (funds), club popularity + accept_popularity
        /// and every starter's stress + accept_starter_stress; each one not accepted costs decline_popularity club popularity.
        /// </summary>
        internal void ApplyMedia(Club c, SegmentPlan plan, SegmentSummary sum)
        {
            var list = c.Runner.MediaInvitations;
            if (list.Count == 0) return;
            var m = Config.media;
            var acc = new HashSet<int>(plan.AcceptedMediaIds);
            long income = 0;
            foreach (var inv in list)
            {
                if (acc.Contains(inv.Id))
                {
                    income += inv.Income;
                    c.Team.ClubPopularity += m.acceptPopularity;
                    foreach (var p in c.Team.Starters()) p.Stress += m.acceptStarterStress;
                    sum.MediaAccepted++;
                }
                else { c.Team.ClubPopularity -= m.declinePopularity; sum.MediaDeclined++; }
            }
            c.Team.Funds += income;
            sum.MediaIncome = (int)income;
        }

        /// <summary>Moves a rostered player to the free pool (contract 0, honor 0 and no transfer wish: economy F2).</summary>
        internal void Release(Club c, Player p)
        {
            c.Team.RemovePlayer(p);
            p.ContractYearsLeft = 0;
            var m = Market(p);
            m.Honor = 0f; m.HonorThisYear = 0f; m.WantsTransfer = false; m.Retained = false;
            FreeAgents.Add(p);
            Invalidate();
        }

        // ------------------------------------------------------------ signings

        /// <summary>Signs a free agent (removed from the pool) for <paramref name="years"/> at the signing salary (key "sal", year, player).</summary>
        public void Sign(Club c, Player p, int years) => Sign(c, p, years, SalaryKind.Sign);

        /// <summary>Salary draw kinds (part of the "sal" key: each signing kind has its own R).</summary>
        public enum SalaryKind
        {
            /// <summary>Re-sign / emergency / takeover.</summary>
            Sign = 0,
            /// <summary>Renewal.</summary>
            Renew = 1,
            /// <summary>Poach.</summary>
            Poach = 2,
            /// <summary>Auction purchase of a league lot.</summary>
            Auction = 3,
        }

        /// <summary>The keyed R stream of a salary ("sal", year, player, kind).</summary>
        public KeyedStream SalaryStream(Player p, SalaryKind kind) => Keys.Stream("sal", Year, p.Id, (long)kind);

        /// <summary>Signs a free agent with the salary draw of <paramref name="kind"/>.</summary>
        public void Sign(Club c, Player p, int years, SalaryKind kind)
        {
            FreeAgents.Remove(p);
            p.ContractYearsLeft = years;
            p.Salary = Economy.SigningSalary(p, years, kind == SalaryKind.Sign ? Keys.Stream("sal", Year, p.Id) : SalaryStream(p, kind));
            p.ResetObservation();
            var m = Market(p);
            m.Retained = false; m.WantsTransfer = false; m.JoinedYear = Year;
            c.Team.AddPlayer(p);
            AddNews(new NewsItem { Kind = NewsKind.Signing, ClubId = c.Id, PlayerName = p.Name, Amount = p.Salary, Segment = Season.SegmentsDoneInYear });
            Invalidate();
        }

        /// <summary>
        /// Emergency signing (league.md Edge Cases, talent.md empty pool): the best free agent of a missing lane (else the best
        /// one); an empty pool first generates one normal player aged 23 for the first missing lane.
        /// </summary>
        public void EmergencySign(Club c)
        {
            var missing = MissingLanes(c);
            if (FreeAgents.Count == 0)
            {
                string lane = missing.Count > 0 ? missing[0] : Players.lanes[0].id;
                var rng = Keys.Stream("emg", Year, Season.SegmentsDoneInYear, c.Id, Talent.NextId);
                int age = Config.talent.emergencyAge;
                FreeAgents.Add(Talent.Generate(rng, lane, age, age, Talent.SupplyMean(LeaguePopularity()), 0f));
            }
            int y = Year;
            Func<Player, bool> open = p => Market(p).LockedYear != y;
            var best = BestFree(missing, open) ?? BestFree(null, open) ?? BestFree(null);
            if (best == null)
            {
                string lane = missing.Count > 0 ? missing[0] : Players.lanes[0].id;
                var rng = Keys.Stream("emg", Year, Season.SegmentsDoneInYear, c.Id, Talent.NextId);
                best = Talent.Generate(rng, lane, Config.talent.emergencyAge, Config.talent.emergencyAge, Talent.SupplyMean(LeaguePopularity()), 0f);
                FreeAgents.Add(best);
            }
            Sign(c, best, Config.economy.signYears);
        }

        /// <summary>
        /// National scouting (talent.md 全国选拔; called by the transfer window in Story 005): pays scout_cost, generates one player
        /// aged 17-20 for <paramref name="lane"/> with the F2 talent chance (need vs the league median of every club's best
        /// main-lane player), signs him for scout_contract years. Key ("scout", year, club). Throws for an unknown lane.
        /// </summary>
        public Player Scout(Club c, string lane)
        {
            if (Players.FindLane(lane) == null) throw new ArgumentException("Unknown lane: " + lane, nameof(lane));
            var t = Config.talent;
            float chance = ScoutChance(c, lane);
            var rng = Keys.Stream("scout", Year, c.Id);
            var p = Talent.Generate(rng, lane, t.scoutAgeMin, t.scoutAgeMax, Talent.SupplyMean(LeaguePopularity()), chance);
            c.Team.Funds -= (long)Math.Round(t.scoutCost);
            Market(p).JoinedYear = Year;
            p.ContractYearsLeft = t.scoutContract;
            p.Salary = Economy.SigningSalary(p, t.scoutContract, Keys.Stream("sal", Year, p.Id));
            c.Team.AddPlayer(p);
            Invalidate();
            return p;
        }

        /// <summary>talent.md F2 talent chance of scouting <paramref name="lane"/> for club <paramref name="c"/>.</summary>
        public float ScoutChance(Club c, string lane)
        {
            float Best(Club o) { float b = 0f; foreach (var p in o.Team.Roster) if (p.MainLane == lane) b = Math.Max(b, p.GetLaneScore(lane)); return b; }
            int n = 0;
            foreach (var p in c.Team.Roster) if (p.MainLane == lane) n++;
            var vals = new List<float>();
            foreach (var o in Clubs) vals.Add(Best(o));
            return Talent.ScoutTalentChance(n, Best(c), LeagueAi.Median(vals));
        }

        /// <summary>Lanes without a main-lane player on the roster (config order).</summary>
        public List<string> MissingLanes(Club c)
        {
            var have = new HashSet<string>();
            foreach (var p in c.Team.Roster) have.Add(p.MainLane);
            var list = new List<string>();
            foreach (var l in Players.lanes) if (!have.Contains(l.id)) list.Add(l.id);
            return list;
        }

        /// <summary>The best-rated free agent (optionally of <paramref name="lanes"/> and passing <paramref name="filter"/>), ties lower id.</summary>
        public Player BestFree(ICollection<string> lanes, Func<Player, bool> filter = null)
        {
            Player best = null;
            foreach (var p in FreeAgents)
            {
                if (lanes != null && !lanes.Contains(p.MainLane)) continue;
                if (filter != null && !filter(p)) continue;
                if (best == null || p.GetLaneScore(p.MainLane) > best.GetLaneScore(best.MainLane)
                    || (p.GetLaneScore(p.MainLane) == best.GetLaneScore(best.MainLane) && p.Id < best.Id)) best = p;
            }
            return best;
        }

        // ------------------------------------------------------------ year end

        /// <summary>
        /// Year end (called by SeasonFlow after the last event, before the year number changes): last finals place, honor (this
        /// year's best place score becomes the player's honor; free agents 0), style evaluation, then the transfer window opens
        /// (transfer.md). Without a player club (warm-up) or with <see cref="AutopilotPlayer"/> it runs to the end at once; the
        /// year rolls over when the window closes (<see cref="CloseWindow"/>).
        /// </summary>
        internal void EndYear()
        {
            int year = Year;
            var finals = FinalsOf(year);
            foreach (var c in Clubs) c.LastYearFinalsPlace = finals != null ? finals.Placing[c.Id] : 0;
            foreach (var c in Clubs)
                foreach (var p in c.Team.Roster) { var m = Market(p); m.Honor = m.HonorThisYear; m.HonorThisYear = 0f; }
            foreach (var p in FreeAgents) { var m = Market(p); m.Honor = 0f; m.HonorThisYear = 0f; }
            StyleEvaluation(year);
            foreach (var c in Clubs) c.YearPlacings.Clear();
            Window = new TransferWindow(this, year, PlayerClub == null || AutopilotPlayer ? -1 : PlayerClubId);
            Window.Continue();
        }

        /// <summary>True = the player club's window decisions follow the AI rules (scripted auto-play, tests).</summary>
        public bool AutopilotPlayer { get; set; }

        /// <summary>Called by the window when it is done: keeps it as <see cref="LastWindow"/>, resets the ledgers, rolls the year.</summary>
        internal void CloseWindow()
        {
            LastWindow = Window;
            Window = null;
            foreach (var c in Clubs) { c.LastLedger = c.Ledger; c.Ledger = new ClubLedger(); }
            Invalidate();
            Season.CompleteYear();
        }

        private EventResult FinalsOf(int year)
        {
            foreach (var y in Season.History)
                if (y.Year == year)
                    foreach (var r in y.Events) if (r.Event.Type == EventType.Finals && r.Event.IsComplete) return r.Event;
            return null;
        }

        /// <summary>
        /// 风格调整 (after the finals, AI clubs): forced when a Star club lost its star; else needs >= 2 events outside the top 4,
        /// the 3-event lock passed and U("style", club, year) &lt; adaptability x p_switch. New style per F4 (no return to the
        /// previous style, prefer styles used by at most one other club, Star first when its conditions hold).
        /// </summary>
        public void StyleEvaluation(int year)
        {
            var s = Config.style;
            foreach (var c in Clubs) c.EventsSinceSwitch += c.YearPlacings.Count;
            foreach (var c in Clubs)
            {
                if (c.IsPlayer) continue;
                bool forced = c.Style == ClubStyle.Star && c.Find(c.StarPlayerId) == null;
                if (!forced)
                {
                    int missed = 0;
                    foreach (int pl in c.YearPlacings) if (pl <= 0 || pl > s.topPlaces) missed++;
                    if (missed < s.missedEvents || c.EventsSinceSwitch < s.lockEvents) continue;
                    if (Keys.U01("style", year, c.Id) >= c.Personality.Adaptability * s.switchChance) continue;
                }
                var me = PhaseStrength(c);
                if (me == null) continue;
                SwitchStyle(c, year, NewStyle(c, me, forced));
            }
        }

        /// <summary>F4 choice of the new style (see <see cref="StyleEvaluation"/>).</summary>
        public ClubStyle NewStyle(Club c, float[] me, bool forced)
        {
            var s = Config.style;
            var gap = GapToMedian(c, me, true);
            float lead = StarLaneLead(c, out string lane);
            int nStar = 0;
            foreach (var o in Clubs) if (o.Id != c.Id && o.Style == ClubStyle.Star) nStar++;
            if (lane != null && lead >= s.starSwitchLead && nStar <= s.rareMax && c.Style != ClubStyle.Star && c.PreviousStyle != ClubStyle.Star)
                return ClubStyle.Star;
            int curPhase = c.Style == ClubStyle.Star ? -1 : (int)c.Style;
            var cands = new List<int>();
            for (int k = 0; k < 3; k++) if (k != curPhase) cands.Add(k);
            if (c.PreviousStyle != null && c.PreviousStyle != ClubStyle.Star && cands.Count > 1)
            {
                int prev = (int)c.PreviousStyle.Value;
                if (cands.Contains(prev) && cands.Count > 1) cands.Remove(prev);
            }
            var rare = cands.FindAll(k => { int n = 0; foreach (var o in Clubs) if (o.Id != c.Id && o.Style == (ClubStyle)k) n++; return n <= s.rareMax; });
            if (rare.Count > 0) cands = rare;
            int best = cands[0];
            foreach (int k in cands) if (gap[k] > gap[best]) best = k;
            return (ClubStyle)best;
        }

        private void SwitchStyle(Club c, int year, ClubStyle to)
        {
            var from = c.Style;
            if (to == ClubStyle.Star)
            {
                StarLaneLead(c, out string lane);
                c.Style = ClubStyle.Star;
                SetStar(c, lane);
            }
            else { c.Style = to; c.StarLane = null; c.StarPlayerId = -1; }
            if (c.Style == from && from != ClubStyle.Star) return;
            c.PreviousStyle = from;
            c.EventsSinceSwitch = 0;
            c.StyleHistory.Add(new StyleChange { Year = year, From = from, To = c.Style });
            AddNews(new NewsItem { Kind = NewsKind.StyleSwitch, ClubId = c.Id, From = from, To = c.Style });
        }

        /// <summary>
        /// The lane whose starter has the lowest main-lane rating; a lane without a main-lane starter counts as weakest (-1).
        /// Ties: lane config order.
        /// </summary>
        public string WeakestLane(Club c)
        {
            string worst = null; float w = float.PositiveInfinity;
            foreach (var l in Players.lanes)
            {
                var p = c.Team.GetStarter(l.id);
                float v = p == null || p.MainLane != l.id ? -1f : p.GetLaneScore(l.id);
                if (v < w) { w = v; worst = l.id; }
            }
            return worst;
        }

        // ------------------------------------------------------------ offers and takeover

        /// <summary>Clubs by strength (mean of S, descending; ties lower id).</summary>
        public List<Club> StrengthRanking()
        {
            var list = new List<Club>(Clubs);
            float Mean(Club c) { var s = PhaseStrength(c); return s == null ? 0f : (s[0] + s[1] + s[2]) / 3f; }
            list.Sort((a, b) => { int k = Mean(b).CompareTo(Mean(a)); return k != 0 ? k : a.Id.CompareTo(b.Id); });
            return list;
        }

        /// <summary>Overall place of a year: finals placing order for the finalists, then the qualification order.</summary>
        public int YearRank(int year, int clubId)
        {
            var f = FinalsOf(year);
            if (f == null) return 0;
            int i = Array.IndexOf(f.FinalOrder, clubId);
            if (i >= 0) return i + 1;
            int rank = f.FinalOrder.Length;
            foreach (var q in f.Qualification) { if (Array.IndexOf(f.FinalOrder, q.ClubIndex) >= 0) continue; rank++; if (q.ClubIndex == clubId) return rank; }
            return 0;
        }

        /// <summary>The three offers (strong 3-4, mid 4-5, weak 6-7; distinct clubs; mid moves to 5 when both drew 4).</summary>
        public void MakeOffers()
        {
            Offers.Clear();
            var rank = StrengthRanking();
            var o = Config.offers;
            int Pick(int[] r, int k) => r[0] + (int)(Keys.U01("offers", k) * (r[1] - r[0] + 1));
            int strong = Pick(o.strongRanks, 0), mid = Pick(o.midRanks, 1), weak = Pick(o.weakRanks, 2);
            if (mid == strong) mid = Math.Min(o.midRanks[1], strong + 1);
            if (mid == strong) mid = strong + 1;
            if (weak == mid || weak == strong) weak = Math.Max(Math.Max(strong, mid) + 1, weak);
            foreach (var (tier, r) in new[] { (OfferTier.Strong, strong), (OfferTier.Mid, mid), (OfferTier.Weak, weak) })
            {
                var c = rank[Math.Min(r, rank.Count) - 1];
                Offers.Add(new JobOffer { Tier = tier, ClubId = c.Id, StrengthRank = r, LastYearRank = YearRank(Year - 1, c.Id) });
            }
        }

        /// <summary>
        /// Takes over an offered club (league.md 接手整理): clears loans and the rescue state, fills missing lanes and the roster to
        /// 6 from the pool, keeps sponsor contracts, tops funds up to 6 segments of wages (weak: 8; not league income), names it.
        /// </summary>
        public Club TakeOver(JobOffer offer, string name)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            if (PlayerClubId >= 0) throw new InvalidOperationException("A club was already taken over.");
            var c = Clubs[offer.ClubId];
            c.IsPlayer = true;
            Schedule.PlayerClub = c.Id;
            c.Debt = 0; c.Bankrupt = false; c.CrisisSegmentsLeft = 0;
            DismissalPending = false;
            foreach (var lane in MissingLanes(c))
            {
                if (c.Team.Roster.Count >= Config.ai.maxRoster) break;
                var p = BestFree(new[] { lane });
                if (p != null) Sign(c, p, Config.economy.signYears);
            }
            while (c.Team.Roster.Count < Config.offers.minRoster && FreeAgents.Count > 0) Sign(c, BestFree(null), Config.economy.signYears);
            Ai.PickLineup(c);
            float segs = offer.Tier == OfferTier.Weak ? Config.offers.weakInjectSegments : Config.offers.injectSegments;
            long need = (long)Math.Ceiling(segs * c.WagesPerSegment(SegmentsPerYear));
            Injected = Math.Max(0, need - c.Team.Funds);
            c.Team.Funds = Math.Max(c.Team.Funds, need);
            Rename(c, name);
            Offers.Clear();
            Invalidate();
            return c;
        }

        /// <summary>Renames a club (team + schedule).</summary>
        public void Rename(Club c, string name)
        {
            c.Team.Rename(name);
            Schedule.Rename(c.Id, name);
        }

        // ------------------------------------------------------------ news, hash

        /// <summary>Adds a news line (club name snapshot, year), keeping the newest news.max_items.</summary>
        public void AddNews(NewsItem n)
        {
            n.Year = Year;
            n.ClubName = Schedule.ClubName(n.ClubId);
            News.Add(n);
            while (News.Count > Config.news.maxItems) News.RemoveAt(0);
        }

        /// <summary>
        /// League state hash (league.md AC1): every club's funds and roster ids, every player's (rostered and free) main-lane
        /// dimensions, focus, mood, stress and contract, rounded to 1e-6, sorted by id.
        /// </summary>
        public string StateHash()
        {
            var sb = new StringBuilder();
            var all = new List<Player>();
            foreach (var c in Clubs)
            {
                sb.Append('C').Append(c.Id).Append(':').Append(c.Team.Funds).Append(':');
                var ids = new List<long>();
                foreach (var p in c.Team.Roster) { ids.Add(p.Id); all.Add(p); }
                ids.Sort();
                sb.Append(string.Join(",", ids)).Append(';');
            }
            all.AddRange(FreeAgents);
            all.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var p in all)
            {
                sb.Append('P').Append(p.Id);
                foreach (var v in p.GetRadar(p.MainLane)) sb.Append(',').Append(R6(v));
                for (int k = 0; k < 3; k++) sb.Append(',').Append(R6(p.GetFocus(k)));
                sb.Append(',').Append(R6(p.Mood)).Append(',').Append(R6(p.Stress)).Append(',').Append(p.ContractYearsLeft)
                    .Append(',').Append(p.Salary).Append(',').Append(R6(Economy.Honor(p))).Append(';');
            }
            sb.Append("WI").Append(Math.Round(WageIndex, 9).ToString("R", CultureInfo.InvariantCulture));
            ulong h = KeyedRandom.HashString(sb.ToString());
            return h.ToString("x16");
        }

        private static string R6(float v) => Math.Round((double)v, 6).ToString("R", CultureInfo.InvariantCulture);

        private static void Shuffle<T>(List<T> list, KeyedStream rng)
        {
            for (int i = list.Count - 1; i >= 1; i--)
            {
                int j = rng.NextInt(0, i + 1);
                var t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
