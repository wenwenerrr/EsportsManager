using System;
using System.Collections.Generic;
using EsportsManager.Players;

namespace EsportsManager.Leagues
{
    /// <summary>Steps of the transfer window in their fixed order (transfer.md Overview 1-8).</summary>
    public enum TransferStep
    {
        /// <summary>1 yearly rollover (automatic).</summary>
        Rollover = 0,
        /// <summary>2 renegotiation (economy F6).</summary>
        Renegotiation = 1,
        /// <summary>3 renewals of expired contracts.</summary>
        Renewal = 2,
        /// <summary>4 poaching (two rounds in club id order).</summary>
        Poaching = 3,
        /// <summary>5 three-choice (auction / scouting / selling / skip).</summary>
        ThreeChoice = 4,
        /// <summary>6 multi-round open auction.</summary>
        Auction = 5,
        /// <summary>7 re-signing from the free pool.</summary>
        Resign = 6,
        /// <summary>8 the window is over (new year).</summary>
        Done = 7,
    }

    /// <summary>What the window waits for from the player (None = it can run on).</summary>
    public enum TransferWait
    {
        /// <summary>Nothing.</summary>
        None = 0,
        /// <summary>Answer every renegotiation request.</summary>
        Renegotiation,
        /// <summary>Renew (1-3 years) or release every expired player.</summary>
        Renewal,
        /// <summary>Optionally queue poach offers, then continue.</summary>
        PoachTargets,
        /// <summary>Accept or retain an incoming offer.</summary>
        PoachResponse,
        /// <summary>Pick auction / scouting / selling / skip.</summary>
        ThreeChoice,
        /// <summary>Raise / exit / auto-bid on the running lot.</summary>
        AuctionBid,
        /// <summary>Sign free agents, then finish the window.</summary>
        Resign,
    }

    /// <summary>The three-choice options (transfer.md step 5).</summary>
    public enum WindowChoice
    {
        /// <summary>Not chosen yet.</summary>
        None = 0,
        /// <summary>Take part in the auction.</summary>
        Auction,
        /// <summary>National scouting (one new player for a lane).</summary>
        Scout,
        /// <summary>List 1-2 own players.</summary>
        Sell,
        /// <summary>Skip.</summary>
        Skip,
    }

    /// <summary>Why a player action was refused.</summary>
    public enum TransferError
    {
        /// <summary>Done.</summary>
        None = 0,
        /// <summary>The window does not wait for this action now.</summary>
        WrongStep,
        /// <summary>Unknown player / request / lot.</summary>
        Unknown,
        /// <summary>Contract years outside 1..3.</summary>
        BadYears,
        /// <summary>Not every request / renewal has been answered.</summary>
        Unanswered,
        /// <summary>Not enough funds.</summary>
        Funds,
        /// <summary>Roster would exceed 8 (or the club is full).</summary>
        RosterFull,
        /// <summary>The roster would end below 5, or a lane would lose its only main-lane starter.</summary>
        RosterGuard,
        /// <summary>The roster has fewer than 5 players or a lane without a main-lane player.</summary>
        IncompleteRoster,
        /// <summary>Not allowed for this player (signed this year, already offered, retained before, locked lot ...).</summary>
        NotAllowed,
        /// <summary>No more poach offers this year.</summary>
        OfferLimit,
    }

    /// <summary>A renegotiation request (economy F6).</summary>
    public sealed class RenegotiationRequest
    {
        /// <summary>Club and player.</summary>
        public int ClubId; public Player Player;
        /// <summary>Current salary and the market salary he asks (F1, no random, year factor 1).</summary>
        public long Current, Market;
        /// <summary>Breakdown of the market salary.</summary>
        public SalaryBreakdown Breakdown;
        /// <summary>null = open, true agreed, false refused.</summary>
        public bool? Agreed;
    }

    /// <summary>An expired contract (step 3).</summary>
    public sealed class RenewalCase
    {
        /// <summary>Club and player.</summary>
        public int ClubId; public Player Player;
        /// <summary>Salary before the renewal.</summary>
        public long OldSalary;
        /// <summary>The salary asked for 1 / 2 / 3 years (index 0..2) and their breakdowns (same R for every length).</summary>
        public long[] Ask = new long[3];
        /// <summary>Breakdowns of <see cref="Ask"/>.</summary>
        public SalaryBreakdown[] Breakdown = new SalaryBreakdown[3];
        /// <summary>null = open, 0 = released, 1..3 = renewed years.</summary>
        public int? Years;
    }

    /// <summary>A poach offer (step 4).</summary>
    public sealed class PoachOffer
    {
        /// <summary>Round (1-based), buyer, seller and the player.</summary>
        public int Round, BuyerId, SellerId; public Player Player;
        /// <summary>Fee = value x U(1.0, 1.3).</summary>
        public long Fee;
        /// <summary>His salary at the buyer (signing salary, 2 years) and the retain salary: max(current salary, buyer salary x 1.1) (user decision 2026-10-29).</summary>
        public long NewSalary, RetainSalary;
        /// <summary>Value and salary satisfaction at the time of the offer (pop-up).</summary>
        public long Value; public float Satisfaction;
        /// <summary>False when he was retained before in this contract (only accepting is possible).</summary>
        public bool CanRetain;
        /// <summary>null = open, true accepted (he moved), false retained.</summary>
        public bool? Accepted;
    }

    /// <summary>Kinds of auction lots.</summary>
    public enum LotKind
    {
        /// <summary>A club's listed player.</summary>
        Listed = 0,
        /// <summary>One of this year's youth players (league lot).</summary>
        Youth = 1,
        /// <summary>A mature free agent (league lot).</summary>
        Free = 2,
    }

    /// <summary>One auction lot and its bidding state (step 6).</summary>
    public sealed class AuctionLot
    {
        /// <summary>The player.</summary>
        public Player Player;
        /// <summary>Kind and seller (-1 = the league).</summary>
        public LotKind Kind; public int SellerId = -1;
        /// <summary>True when the listing was forced (refused renegotiation / debt).</summary>
        public bool Forced;
        /// <summary>F2 start price and the current price (rounded to 100).</summary>
        public long StartPrice, Price;
        /// <summary>Top bidder club id, -1 none.</summary>
        public int TopBidder = -1;
        /// <summary>Clubs still in, in bidding order (popularity, then id).</summary>
        public List<int> InClubs = new List<int>();
        /// <summary>Clubs that bid at least once.</summary>
        public HashSet<int> Bidders = new HashSet<int>();
        /// <summary>True once finished; <see cref="TopBidder"/> &gt;= 0 = sold.</summary>
        public bool Finished;
        /// <summary>Index into <see cref="InClubs"/> of the next club to ask in the running round, and whether anyone raised this round.</summary>
        internal int Cursor; internal bool RaisedThisRound, Opened;
        /// <summary>AI limits (F4) per club id.</summary>
        internal Dictionary<int, double> Limits = new Dictionary<int, double>();
        /// <summary>True when sold.</summary>
        public bool Sold => Finished && TopBidder >= 0;
    }

    /// <summary>
    /// The transfer window (design/gdd/transfer.md rev 3; economy.md rev 3 F3/F6/F7; talent.md rev 3; Python reference
    /// tools/sim/sim5.py transfer_window with fixG + the approved changes: up to 2 poach offers per AI in two id-order rounds,
    /// 8 youth a year). Every club follows the same rules; the AI decides by the documented rules and the player club (unless
    /// <c>playerClubId</c> is -1 = autopilot / warm-up) answers through the public methods. <see cref="Continue"/> runs until the
    /// next player decision (<see cref="Waiting"/>) or the end. Shared-pool operations run in club id order; every random draw is
    /// keyed (league.md 随机数), so a player's choice never changes an AI draw.
    /// </summary>
    public sealed class TransferWindow
    {
        private readonly LeagueWorld _w;
        private readonly int _player;
        private readonly HashSet<long> _youth = new HashSet<long>();
        private readonly List<long> _playerPoachQueue = new List<long>();
        private int _poachRound = 1, _poachIndex;
        private int _lotIndex;
        private int _resignIndex;
        private PoachOffer _incoming;
        private bool _poachStarted, _choicesDone;

        /// <summary>The window's year (the year that just ended; keys use it).</summary>
        public int Year { get; }
        /// <summary>Current step.</summary>
        public TransferStep Step { get; private set; } = TransferStep.Rollover;
        /// <summary>What the player has to do now.</summary>
        public TransferWait Waiting { get; private set; }
        /// <summary>The player club id (-1 = autopilot / all AI).</summary>
        public int PlayerClubId => _player;
        /// <summary>Wage index before and after this window's update.</summary>
        public double WageIndexBefore, WageIndexAfter;
        /// <summary>League income / (wages + event expenses) of the ended year (F3 R).</summary>
        public double LeagueRatio;
        /// <summary>Players who retired this window.</summary>
        public List<Player> Retired { get; } = new List<Player>();
        /// <summary>Generated youth and free agents.</summary>
        public List<Player> NewPlayers { get; } = new List<Player>();
        /// <summary>Renegotiation requests of every club.</summary>
        public List<RenegotiationRequest> Renegotiations { get; } = new List<RenegotiationRequest>();
        /// <summary>Expired contracts of every club.</summary>
        public List<RenewalCase> Renewals { get; } = new List<RenewalCase>();
        /// <summary>Every poach offer made.</summary>
        public List<PoachOffer> Offers { get; } = new List<PoachOffer>();
        /// <summary>The three-choice of every club (scout lane in <see cref="ScoutLanes"/>).</summary>
        public Dictionary<int, WindowChoice> Choices { get; } = new Dictionary<int, WindowChoice>();
        /// <summary>Scouting lane per club and the scouted player.</summary>
        public Dictionary<int, string> ScoutLanes { get; } = new Dictionary<int, string>();
        /// <summary>Scouted players per club.</summary>
        public Dictionary<int, Player> Scouted { get; } = new Dictionary<int, Player>();
        /// <summary>Auction lots in auction order (league lots exist from the start of step 5; listings are added after the choices).</summary>
        public List<AuctionLot> Lots { get; } = new List<AuctionLot>();
        /// <summary>Re-signings (club id, player) in order.</summary>
        public List<(int ClubId, Player Player)> Resigned { get; } = new List<(int, Player)>();

        private LeagueConfig Cfg => _w.Config;
        private TransferConfig X => _w.Config.transfer;
        private EconomyRules Eco => _w.Economy;

        /// <summary>Creates the window for <paramref name="year"/>; nothing runs until <see cref="Continue"/>.</summary>
        public TransferWindow(LeagueWorld world, int year, int playerClubId)
        {
            _w = world ?? throw new ArgumentNullException(nameof(world));
            Year = year;
            _player = playerClubId;
        }

        private bool _handedOver;
        private bool Active => _player >= 0 && !_handedOver;
        private bool IsPlayer(Club c) => Active && c.Id == _player;
        private Club Player => _player >= 0 ? _w.Clubs[_player] : null;

        /// <summary>
        /// Hands the player club's remaining decisions to the AI rules (scripted auto-play, tests) and runs the window to the end.
        /// </summary>
        public void HandOverToAi()
        {
            if (Step == TransferStep.Done) return;
            var me = Player;
            _handedOver = true;
            if (me != null)
            {
                foreach (var r in Renegotiations) if (r.ClubId == me.Id && r.Agreed == null) Answer(r, me.Team.Funds >= _w.Ai.Floor(me) && me.Team.LaneOf(r.Player) != null);
                foreach (var r in Renewals) if (r.ClubId == me.Id && r.Years == null) DecideAiRenewal(me, r);
                if (PendingIncoming != null) Resolve(_incoming, AiPoachResponse(me, _incoming));
            }
            Waiting = TransferWait.None;
            Continue();
        }

        // ================================================================== driver

        /// <summary>
        /// Runs the window until the player has to decide (<see cref="Waiting"/> != None) or it is done (then the world rolls the
        /// year). Returns <see cref="TransferError.Unanswered"/> when the current wait is not satisfied yet.
        /// </summary>
        public TransferError Continue()
        {
            while (true)
            {
                switch (Step)
                {
                    case TransferStep.Rollover:
                        Rollover();
                        Step = TransferStep.Renegotiation;
                        OpenRenegotiation();
                        break;
                    case TransferStep.Renegotiation:
                        if (Open(Renegotiations, r => r.Agreed == null)) return Wait(TransferWait.Renegotiation);
                        Step = TransferStep.Renewal;
                        OpenRenewals();
                        break;
                    case TransferStep.Renewal:
                        if (Open(Renewals, r => r.Years == null)) return Wait(TransferWait.Renewal);
                        FinishRenewals();
                        Step = TransferStep.Poaching;
                        if (Active) { Waiting = TransferWait.PoachTargets; return TransferError.None; }
                        break;
                    case TransferStep.Poaching:
                        if (Waiting == TransferWait.PoachTargets) Waiting = TransferWait.None;
                        if (_incoming != null && _incoming.Accepted == null) return Wait(TransferWait.PoachResponse);
                        _poachStarted = true;
                        if (!RunPoachRounds()) return Wait(TransferWait.PoachResponse);
                        Step = TransferStep.ThreeChoice;
                        BuildLeagueLots();
                        if (Active && Choices.Count == 0) { Waiting = TransferWait.ThreeChoice; return TransferError.None; }
                        break;
                    case TransferStep.ThreeChoice:
                        if (Active && !Choices.ContainsKey(_player)) return Wait(TransferWait.ThreeChoice);
                        Waiting = TransferWait.None;
                        if (!_choicesDone) { RunChoices(); _choicesDone = true; }
                        Step = TransferStep.Auction;
                        break;
                    case TransferStep.Auction:
                        if (!RunAuction()) return Wait(TransferWait.AuctionBid);
                        Step = TransferStep.Resign;
                        break;
                    case TransferStep.Resign:
                        if (!RunResign()) return Wait(TransferWait.Resign);
                        FinishWindow();
                        Step = TransferStep.Done;
                        Waiting = TransferWait.None;
                        _w.CloseWindow();
                        return TransferError.None;
                    default:
                        return TransferError.None;
                }
            }
        }

        private TransferError Wait(TransferWait w) { Waiting = w; return TransferError.None; }

        private bool Open<T>(List<T> list, Func<T, bool> open) where T : class
        {
            foreach (var x in list) if (open(x)) return true;
            return false;
        }

        // ================================================================== 1. rollover

        private void Rollover()
        {
            var t = Cfg.talent;
            var all = new List<Player>();
            foreach (var c in _w.Clubs) all.AddRange(c.Team.Roster);
            all.AddRange(_w.FreeAgents);
            foreach (var p in all) _w.Growth.AdvanceYear(p);
            foreach (var c in _w.Clubs)
                foreach (var p in new List<Player>(c.Team.Roster))
                {
                    long pid = p.Id;
                    if (_w.Talent.RetiresSigned(p, () => _w.Keys.U01("ret", Year, pid))) { c.Team.RemovePlayer(p); Retired.Add(p); }
                }
            foreach (var p in new List<Player>(_w.FreeAgents))
            {
                long pid = p.Id;
                if (_w.Talent.RetiresFree(p, () => _w.Keys.U01("ret", Year, pid))) { _w.FreeAgents.Remove(p); Retired.Add(p); }
            }
            float mu = _w.Talent.SupplyMean(_w.LeaguePopularity());
            var rng = _w.Keys.Stream("supply", Year);
            var lanes = _w.Players.lanes;
            for (int i = 0; i < t.youthCount; i++)
            {
                var p = _w.Talent.Generate(rng, lanes[rng.NextInt(0, lanes.Length)].id, t.youthAgeMin, t.youthAgeMax, mu);
                _youth.Add(p.Id); _w.Market(p).YouthYear = Year; _w.FreeAgents.Add(p); NewPlayers.Add(p);
            }
            for (int i = 0; i < t.freeCount; i++)
            {
                var p = _w.Talent.Generate(rng, lanes[rng.NextInt(0, lanes.Length)].id, t.freeAgeMin, t.freeAgeMax, mu);
                _w.FreeAgents.Add(p); NewPlayers.Add(p);
            }
            _w.Talent.ApplyPoolCap(_w.FreeAgents, p => _youth.Contains(p.Id));
            // F3 wage index from the ended year (income without transfers / fire sales / loans / capital)
            long inc = 0, exp = 0;
            foreach (var c in _w.Clubs) { inc += c.Ledger.Income; exp += c.Ledger.Wages + c.Ledger.EventExpenses; }
            LeagueRatio = inc / (double)Math.Max(1L, exp);
            WageIndexBefore = Eco.WageIndex;
            Eco.WageIndex = Eco.NextWageIndex(Eco.WageIndex, LeagueRatio);
            WageIndexAfter = Eco.WageIndex;
            _w.WageIndexHistory.Add(WageIndexAfter);
            foreach (var c in _w.Clubs) Lineup(c);
            _w.Invalidate();
        }

        // The AI picks its lineup by rule 1; the player club keeps its starters and only fills empty lanes.
        private void Lineup(Club c)
        {
            if (!IsPlayer(c)) { if (c.Team.Roster.Count > 0) _w.Ai.PickLineup(c); return; }
            var team = c.Team;
            foreach (var lane in _w.Players.lanes)
            {
                if (team.GetStarter(lane.id) != null) continue;
                Player best = null;
                foreach (var p in team.Roster)
                {
                    if (team.LaneOf(p) != null) continue;
                    bool main = p.MainLane == lane.id, bmain = best != null && best.MainLane == lane.id;
                    if (best == null || (main && !bmain) || (main == bmain && p.GetLaneScore(lane.id) > best.GetLaneScore(lane.id))) best = p;
                }
                if (best != null) team.SetStarter(lane.id, best);
            }
        }

        // ================================================================== 2. renegotiation (economy F6)

        private void OpenRenegotiation()
        {
            foreach (var c in _w.Clubs)
                foreach (var p in c.Team.Roster)
                {
                    var m = _w.Market(p);
                    if (m.RenegBlockedYear == Year || !Eco.WantsRenegotiation(p)) continue;
                    var r = new RenegotiationRequest
                    {
                        ClubId = c.Id, Player = p, Current = p.Salary, Market = (long)Math.Round(Eco.MarketSalary(p)),
                        Breakdown = Eco.Breakdown(p, 1, 1f),
                    };
                    Renegotiations.Add(r);
                    if (!IsPlayer(c)) Answer(r, c.Team.Funds >= _w.Ai.Floor(c) && c.Team.LaneOf(p) != null);
                }
        }

        private void Answer(RenegotiationRequest r, bool agree)
        {
            r.Agreed = agree;
            var p = r.Player;
            if (agree) { p.Salary = (int)r.Market; return; }
            var m = _w.Market(p);
            p.Mood -= Cfg.economy.renegMoodDrop;
            m.WantsTransfer = true;
            m.RenegBlockedYear = Year + 1;
            m.AutoListYear = Year;
        }

        /// <summary>The player agrees to (true) or refuses (false) a renegotiation request of his club.</summary>
        public TransferError AnswerRenegotiation(long playerId, bool agree)
        {
            if (Step != TransferStep.Renegotiation) return TransferError.WrongStep;
            foreach (var r in Renegotiations)
                if (r.ClubId == _player && r.Player.Id == playerId && r.Agreed == null) { Answer(r, agree); return TransferError.None; }
            return TransferError.Unknown;
        }

        // ================================================================== 3. renewals

        /// <summary>AI renewal length: age &lt;= 23 -> 3 years, &lt;= 27 -> 2, else 1.</summary>
        public int AiRenewYears(int age) => age <= X.renewYoungMaxAge ? 3 : age <= X.renewMidMaxAge ? 2 : 1;

        private void OpenRenewals()
        {
            foreach (var c in _w.Clubs)
            {
                var list = new List<Player>();
                foreach (var p in c.Team.Roster) if (p.ContractYearsLeft <= 0) list.Add(p);
                list.Sort((a, b) =>
                {
                    int sa = c.Team.LaneOf(a) != null ? 0 : 1, sb = c.Team.LaneOf(b) != null ? 0 : 1;
                    if (sa != sb) return sa.CompareTo(sb);
                    int k = b.GetLaneScore(b.MainLane).CompareTo(a.GetLaneScore(a.MainLane));
                    return k != 0 ? k : a.Id.CompareTo(b.Id);
                });
                foreach (var p in list)
                {
                    float r = Eco.DrawSalaryRandom(_w.SalaryStream(p, LeagueWorld.SalaryKind.Renew));
                    var rc = new RenewalCase { ClubId = c.Id, Player = p, OldSalary = p.Salary };
                    for (int y = 1; y <= 3; y++) { rc.Breakdown[y - 1] = Eco.Breakdown(p, y, r, true); rc.Ask[y - 1] = rc.Breakdown[y - 1].Total; }
                    Renewals.Add(rc);
                    if (!IsPlayer(c)) DecideAiRenewal(c, rc);
                }
            }
        }

        /// <summary>
        /// AI renewal rule (transfer.md 3): starters and a lane's only main-lane player renew at funds >= 0 (older than 28 and not
        /// the lane's only main-lane player: also the bench test); bench players need value / (salary / WI) >= 1.5 + offset and
        /// funds >= floor with the new wage bill. Applies the decision.
        /// </summary>
        internal void DecideAiRenewal(Club c, RenewalCase rc)
        {
            var p = rc.Player;
            int years = AiRenewYears(p.Age);
            long ask = rc.Ask[years - 1];
            bool sole = SoleMain(c, p);
            bool benchTest = Eco.Value(p) / (ask / Eco.WageIndex) >= X.benchRenew + c.Personality.OffRenew;
            bool ok;
            if (c.Team.LaneOf(p) != null || sole)
            {
                ok = c.Team.Funds >= 0;
                if (ok && p.Age > X.renewAgeTestAbove && !sole) ok = benchTest;
            }
            else
            {
                long bill = WageBill(c) - p.Salary + ask;
                ok = benchTest && c.Team.Funds >= FloorWith(c, bill);
            }
            ApplyRenewal(c, rc, ok ? years : 0);
        }

        private void ApplyRenewal(Club c, RenewalCase rc, int years)
        {
            rc.Years = years;
            var p = rc.Player;
            if (years > 0)
            {
                p.Salary = (int)rc.Ask[years - 1];
                p.ContractYearsLeft = years;
                var m = _w.Market(p);
                m.Retained = false;
            }
            else _w.Release(c, p);
        }

        /// <summary>The player renews an expired contract for 1-3 years at the asked salary (funds may go negative).</summary>
        public TransferError Renew(long playerId, int years)
        {
            if (Step != TransferStep.Renewal) return TransferError.WrongStep;
            if (years < 1 || years > 3) return TransferError.BadYears;
            var rc = PlayerRenewal(playerId);
            if (rc == null) return TransferError.Unknown;
            ApplyRenewal(_w.Clubs[_player], rc, years);
            return TransferError.None;
        }

        /// <summary>The player lets an expired player go (to the free pool).</summary>
        public TransferError Release(long playerId)
        {
            if (Step != TransferStep.Renewal) return TransferError.WrongStep;
            var rc = PlayerRenewal(playerId);
            if (rc == null) return TransferError.Unknown;
            ApplyRenewal(_w.Clubs[_player], rc, 0);
            return TransferError.None;
        }

        private RenewalCase PlayerRenewal(long id)
        {
            foreach (var r in Renewals) if (r.ClubId == _player && r.Player.Id == id && r.Years == null) return r;
            return null;
        }

        private void FinishRenewals()
        {
            foreach (var c in _w.Clubs)
            {
                if (IsPlayer(c)) { Lineup(c); continue; }
                // keep the AI invariants for the poach step (sim: resign_fill when below 5)
                if (c.Team.Roster.Count < Cfg.ai.minRoster) AiResign(c);
                _w.Ai.PickLineup(c);
            }
            _w.Invalidate();
        }

        // ================================================================== 4. poaching

        /// <summary>Fee of a poach: value x U(fee_min, fee_max), keyed ("fee", year, buyer, player).</summary>
        public long PoachFee(Club buyer, Player p)
            => (long)Math.Round(Eco.Value(p) * (X.feeMin + (X.feeMax - X.feeMin) * _w.Keys.U01("fee", Year, buyer.Id, p.Id)));

        /// <summary>True when <paramref name="p"/> of <paramref name="seller"/> may be a poach target at all (transfer.md guards).</summary>
        public bool CanBeTarget(Club seller, Player p)
        {
            if (seller.Team.Roster.Count <= Cfg.ai.minRoster) return false;
            string lane = seller.Team.LaneOf(p);
            if (lane == null || lane != p.MainLane) return false;
            if (SoleMain(seller, p)) return false;
            return _w.Market(p).OfferedYear != Year;
        }

        /// <summary>F1 poach score: R_main x 1.5 (buyer's weakest lane) x 2 (wants a transfer) x (1 + honor/100).</summary>
        public double PoachScore(Club buyer, Player p, string weakestLane)
        {
            var m = _w.Market(p);
            return p.GetLaneScore(p.MainLane) * (p.MainLane == weakestLane ? X.poachWeakLaneBonus : 1f)
                * (m.WantsTransfer ? X.poachWantsBonus : 1f) * (1.0 + m.Honor / 100.0);
        }

        /// <summary>
        /// AI target (transfer.md 4): roster &lt; 8; fee &lt;= funds - floor; the best-scoring eligible starter of the reigning finals
        /// champion if one is affordable, else of the whole league. Null = no offer.
        /// </summary>
        public Player AiPoachTarget(Club c, out Club seller, out long fee)
        {
            seller = null; fee = 0;
            if (c.Team.Roster.Count >= Cfg.ai.maxRoster) return null;
            double cap = c.Team.Funds - _w.Ai.Floor(c);
            string wl = _w.WeakestLane(c);
            Player best = null, bestCh = null; double bs = 0, bc = 0; Club bo = null, bco = null; long bf = 0, bcf = 0;
            foreach (var o in _w.Clubs)
            {
                if (o == c) continue;
                bool champ = o.LastYearFinalsPlace == 1;
                foreach (var p in o.Team.Starters())
                {
                    if (!CanBeTarget(o, p)) continue;
                    long f = PoachFee(c, p);
                    if (f > cap) continue;
                    double sc = PoachScore(c, p, wl);
                    if (best == null || sc > bs) { best = p; bs = sc; bo = o; bf = f; }
                    if (champ && (bestCh == null || sc > bc)) { bestCh = p; bc = sc; bco = o; bcf = f; }
                }
            }
            if (bestCh != null) { seller = bco; fee = bcf; return bestCh; }
            seller = bo; fee = bf;
            return best;
        }

        /// <summary>Starters of other clubs the player club may make an offer for now (guards + funds >= fee + roster &lt; 8).</summary>
        public List<PoachOffer> PlayerPoachTargets()
        {
            var list = new List<PoachOffer>();
            var me = Player;
            if (me == null || me.Team.Roster.Count >= Cfg.ai.maxRoster) return list;
            foreach (var o in _w.Clubs)
            {
                if (o == me) continue;
                foreach (var p in o.Team.Starters())
                {
                    if (!CanBeTarget(o, p) || _playerPoachQueue.Contains(p.Id)) continue;
                    long fee = PoachFee(me, p);
                    if (fee > me.Team.Funds) continue;
                    list.Add(MakeOffer(0, me, o, p, fee));
                }
            }
            list.Sort((a, b) => b.Player.GetLaneScore(b.Player.MainLane).CompareTo(a.Player.GetLaneScore(a.Player.MainLane)));
            return list;
        }

        /// <summary>Player ids the player club queued as poach targets (round 1, round 2).</summary>
        public IReadOnlyList<long> PlayerPoachQueue => _playerPoachQueue;

        /// <summary>Queues a poach offer for the player club (at most poach_offers_per_club; made in its id slot of each round).</summary>
        public TransferError QueuePoach(long playerId)
        {
            if (Step != TransferStep.Poaching || Waiting != TransferWait.PoachTargets) return TransferError.WrongStep;
            if (_playerPoachQueue.Count >= X.poachOffersPerClub) return TransferError.OfferLimit;
            foreach (var o in PlayerPoachTargets()) if (o.Player.Id == playerId) { _playerPoachQueue.Add(playerId); return TransferError.None; }
            return TransferError.NotAllowed;
        }

        /// <summary>Removes a queued poach target.</summary>
        public TransferError UnqueuePoach(long playerId)
        {
            if (Step != TransferStep.Poaching || Waiting != TransferWait.PoachTargets) return TransferError.WrongStep;
            return _playerPoachQueue.Remove(playerId) ? TransferError.None : TransferError.Unknown;
        }

        /// <summary>Puts an offer in front of the player (tests; normally made by the rounds).</summary>
        internal void SetIncoming(PoachOffer o) { _incoming = o; Waiting = TransferWait.PoachResponse; }

        /// <summary>The incoming offer the player has to answer, or null.</summary>
        public PoachOffer PendingIncoming => _incoming != null && _incoming.Accepted == null ? _incoming : null;

        /// <summary>The player answers the pending incoming offer: accept the fee, or retain with the raise (once per contract).</summary>
        public TransferError RespondPoach(bool accept)
        {
            var o = PendingIncoming;
            if (o == null) return TransferError.WrongStep;
            if (!accept && !o.CanRetain) return TransferError.NotAllowed;
            Resolve(o, accept);
            return TransferError.None;
        }

        /// <summary>Builds an offer (fee, salary at the buyer, retain salary, value, satisfaction); nothing changes yet.</summary>
        internal PoachOffer MakeOffer(int round, Club buyer, Club seller, Player p, long fee)
        {
            float r = Eco.DrawSalaryRandom(_w.SalaryStream(p, LeagueWorld.SalaryKind.Poach));
            long sal = Eco.Breakdown(p, X.poachContractYears, r).Total;
            var m = _w.Market(p);
            return new PoachOffer
            {
                Round = round, BuyerId = buyer.Id, SellerId = seller.Id, Player = p, Fee = fee, NewSalary = sal,
                RetainSalary = Math.Max((long)p.Salary, (long)Math.Round(sal * (double)X.retainMult)), Value = (long)Math.Round(Eco.Value(p)),
                Satisfaction = Eco.Satisfaction(p), CanRetain = !m.Retained,
            };
        }

        // Runs the rounds from the saved cursor; false = paused for the player's answer.
        private bool RunPoachRounds()
        {
            while (_poachRound <= X.poachOffersPerClub)
            {
                while (_poachIndex < _w.Clubs.Count)
                {
                    var c = _w.Clubs[_poachIndex++];
                    PoachOffer offer = null;
                    if (IsPlayer(c))
                    {
                        if (_playerPoachQueue.Count >= _poachRound && c.Team.Roster.Count < Cfg.ai.maxRoster)
                        {
                            long id = _playerPoachQueue[_poachRound - 1];
                            Club seller = null; Player p = null;
                            foreach (var o in _w.Clubs) { var q = o.Find(id); if (q != null) { seller = o; p = q; } }
                            if (p != null && seller != c && CanBeTarget(seller, p))
                            {
                                long fee = PoachFee(c, p);
                                if (fee <= c.Team.Funds) offer = MakeOffer(_poachRound, c, seller, p, fee);
                            }
                        }
                    }
                    else
                    {
                        var p = AiPoachTarget(c, out var seller, out long fee);
                        if (p != null) offer = MakeOffer(_poachRound, c, seller, p, fee);
                    }
                    if (offer == null) continue;
                    _w.Market(offer.Player).OfferedYear = Year;
                    Offers.Add(offer);
                    var s = _w.Clubs[offer.SellerId];
                    if (IsPlayer(s)) { _incoming = offer; return false; }
                    Resolve(offer, AiPoachResponse(s, offer));
                }
                _poachRound++;
                _poachIndex = 0;
            }
            return true;
        }

        /// <summary>
        /// AI answer (transfer.md 4): wants a transfer -> accept; funds &lt; floor and fee >= 1.2 x value -> accept; else retain when
        /// not retained before, the retain salary &lt;= value / 1.5 and funds >= floor with the new bill; else accept. True = accept.
        /// </summary>
        public bool AiPoachResponse(Club seller, PoachOffer o)
        {
            var p = o.Player;
            if (_w.Market(p).WantsTransfer) return true;
            double value = Eco.Value(p);
            if (seller.Team.Funds < _w.Ai.Floor(seller) && o.Fee >= X.aiAcceptFee * value) return true;
            if (o.CanRetain && o.RetainSalary <= value / X.retainValueRatio
                && seller.Team.Funds >= FloorWith(seller, WageBill(seller) - p.Salary + o.RetainSalary)) return false;
            return true;
        }

        /// <summary>Applies an answer: accept (he moves, fee paid) or retain (raise, contract + 1, once per contract).</summary>
        internal void Resolve(PoachOffer o, bool accept)
        {
            o.Accepted = accept;
            var buyer = _w.Clubs[o.BuyerId];
            var seller = _w.Clubs[o.SellerId];
            var p = o.Player;
            var m = _w.Market(p);
            if (accept)
            {
                seller.Team.RemovePlayer(p);
                _w.PayIn(seller, o.Fee);
                buyer.Team.Funds -= o.Fee;
                p.Salary = (int)o.NewSalary;
                p.ContractYearsLeft = X.poachContractYears;
                p.ResetObservation();
                m.Retained = false; m.WantsTransfer = false; m.JoinedYear = Year;
                buyer.Team.AddPlayer(p);
                Lineup(seller); Lineup(buyer);
                _w.AddNews(new NewsItem { Kind = NewsKind.Poach, ClubId = buyer.Id, OtherClubId = seller.Id, OtherClubName = seller.Name, PlayerName = p.Name, Amount = o.Fee });
            }
            else
            {
                p.Salary = (int)o.RetainSalary;
                p.ContractYearsLeft += 1;
                m.Retained = true;
                _w.AddNews(new NewsItem { Kind = NewsKind.Retain, ClubId = seller.Id, OtherClubId = buyer.Id, OtherClubName = buyer.Name, PlayerName = p.Name, Amount = o.RetainSalary });
            }
            _w.Invalidate();
        }

        // ================================================================== 5. three-choice

        private void BuildLeagueLots()
        {
            var youth = new List<Player>(); var mature = new List<Player>();
            foreach (var p in _w.FreeAgents) (_youth.Contains(p.Id) ? youth : mature).Add(p);
            youth.Sort((a, b) => { int k = Eco.PotentialEstimate(b).CompareTo(Eco.PotentialEstimate(a)); return k != 0 ? k : a.Id.CompareTo(b.Id); });
            mature.Sort((a, b) => { int k = b.GetLaneScore(b.MainLane).CompareTo(a.GetLaneScore(a.MainLane)); return k != 0 ? k : a.Id.CompareTo(b.Id); });
            for (int i = 0; i < X.lotsYouth && i < youth.Count; i++) Lots.Add(LeagueLot(youth[i], LotKind.Youth));
            for (int i = 0; i < X.lotsFree && i < mature.Count; i++) Lots.Add(LeagueLot(mature[i], LotKind.Free));
        }

        /// <summary>A league lot with its F2 start price (value_est x 0.4).</summary>
        internal AuctionLot LeagueLot(Player p, LotKind k)
        {
            long start = Round100(Eco.Value(p) * X.leagueStartFactor);
            return new AuctionLot { Player = p, Kind = k, StartPrice = start, Price = start };
        }

        /// <summary>F2 start price of a listed player: value x U(0.5, 0.8) (key "lot", year, player) x (0.85 + 0.1 x contract years).</summary>
        public long ListedStartPrice(Player p)
        {
            double u = X.listedStartMin + (X.listedStartMax - X.listedStartMin) * _w.Keys.U01("lot", Year, p.Id);
            return Round100(Eco.Value(p) * u * (X.listedContractBase + X.listedContractPerYear * Math.Max(0, p.ContractYearsLeft)));
        }

        /// <summary>F3 bid step: round100(max(step_min, step_pct x price)).</summary>
        public long Step3(long price) => Round100(Math.Max(X.stepMin, X.stepPct * (double)price));

        /// <summary>Rounds to the price unit (100 yuan), halves up.</summary>
        public long Round100(double v) => (long)(Math.Floor(v / X.priceRound + 0.5) * X.priceRound);

        /// <summary>The cheapest league lot's first bid (start + step); long.MaxValue without lots.</summary>
        public long MinLotFirstBid()
        {
            long m = long.MaxValue;
            foreach (var l in Lots) if (l.SellerId < 0) m = Math.Min(m, l.StartPrice + Step3(l.StartPrice));
            return m;
        }

        /// <summary>
        /// Listing guards (transfer.md 5): after listing the roster keeps >= 5 players; never a lane's only main-lane starter; not
        /// a player who joined this year. <paramref name="alreadyListed"/> = players of this club listed before.
        /// </summary>
        public bool CanList(Club c, Player p, int alreadyListed)
        {
            if (c.Team.Roster.Count - alreadyListed - 1 < Cfg.ai.minRoster) return false;
            if (c.Team.LaneOf(p) != null && SoleMain(c, p)) return false;
            return _w.Market(p).JoinedYear != Year;
        }

        /// <summary>
        /// AI three-choice (transfer.md 5 rules 1-6). Returns the choice; <paramref name="sell"/> the players to list,
        /// <paramref name="lane"/> the scouting lane.
        /// </summary>
        public WindowChoice AiChoice(Club c, long minLot, out List<Player> sell, out string lane)
        {
            sell = new List<Player>(); lane = null;
            double W = c.WagesPerSegment(_w.SegmentsPerYear);
            double floor = _w.Ai.Floor(c);
            var team = c.Team;
            if (c.Debt > Cfg.economy.debtLine * WageBill(c))
            {
                var st = new List<Player>(team.Starters());
                st.Sort((a, b) => { int k = b.Salary.CompareTo(a.Salary); return k != 0 ? k : a.Id.CompareTo(b.Id); });
                foreach (var p in st) if (CanList(c, p, 0)) { sell.Add(p); return WindowChoice.Sell; }
                return WindowChoice.Skip;
            }
            if (team.Funds < floor)
            {
                var bench = new List<Player>();
                foreach (var p in team.Roster) if (team.LaneOf(p) == null) bench.Add(p);
                bench.Sort((a, b) => { int k = (Eco.Value(a) / Math.Max(1, a.Salary)).CompareTo(Eco.Value(b) / Math.Max(1, b.Salary)); return k != 0 ? k : a.Id.CompareTo(b.Id); });
                int max = team.Roster.Count > Cfg.ai.fillRoster ? X.maxListed : 1;
                foreach (var p in bench)
                {
                    if (sell.Count >= max) break;
                    if (CanList(c, p, sell.Count)) sell.Add(p);
                }
                return sell.Count > 0 ? WindowChoice.Sell : WindowChoice.Skip;
            }
            var missing = _w.MissingLanes(c);
            double reserve = X.reserveSegments * W;
            bool affordLot = minLot != long.MaxValue && team.Funds - minLot >= reserve;
            int n = team.Roster.Count;
            if (n < Cfg.ai.fillRoster || missing.Count > 0)
            {
                if (affordLot) return WindowChoice.Auction;
                if (n < Cfg.ai.maxRoster) { lane = missing.Count > 0 ? missing[0] : _w.WeakestLane(c); return WindowChoice.Scout; }
                return WindowChoice.Skip;
            }
            if (affordLot && n < Cfg.ai.maxRoster) return WindowChoice.Auction;
            if (n <= Cfg.ai.maxRoster - 1) { lane = _w.WeakestLane(c); return WindowChoice.Scout; }
            return WindowChoice.Skip;
        }

        /// <summary>The player chooses the auction.</summary>
        public TransferError ChooseAuction() => Choose(WindowChoice.Auction, null, null);

        /// <summary>The player skips the three-choice.</summary>
        public TransferError ChooseSkip() => Choose(WindowChoice.Skip, null, null);

        /// <summary>The player scouts a lane (needs roster &lt; 8; costs scout_cost now).</summary>
        public TransferError ChooseScout(string lane)
        {
            if (_w.Players.FindLane(lane) == null) return TransferError.Unknown;
            return Choose(WindowChoice.Scout, null, lane);
        }

        /// <summary>The player lists 1-2 of his players (listing guards apply).</summary>
        public TransferError ChooseSell(IList<long> playerIds)
        {
            if (playerIds == null || playerIds.Count < 1 || playerIds.Count > X.maxListed) return TransferError.NotAllowed;
            return Choose(WindowChoice.Sell, playerIds, null);
        }

        private TransferError Choose(WindowChoice ch, IList<long> sell, string lane)
        {
            if (Step != TransferStep.ThreeChoice || !Active || Choices.ContainsKey(_player)) return TransferError.WrongStep;
            var c = Player;
            if (ch == WindowChoice.Scout && c.Team.Roster.Count >= Cfg.ai.maxRoster) return TransferError.RosterFull;
            var listed = new List<Player>();
            if (ch == WindowChoice.Sell)
                foreach (long id in sell)
                {
                    var p = c.Find(id);
                    if (p == null || listed.Contains(p)) return TransferError.Unknown;
                    if (!CanList(c, p, listed.Count)) return TransferError.RosterGuard;
                    listed.Add(p);
                }
            Choices[c.Id] = ch;
            if (ch == WindowChoice.Scout) DoScout(c, lane);
            foreach (var p in listed) Lots.Add(ListedLot(c, p, false));
            Waiting = TransferWait.None;
            return TransferError.None;
        }

        /// <summary>True when the player club could choose scouting now (roster &lt; 8).</summary>
        public bool PlayerCanScout => Player != null && Player.Team.Roster.Count < Cfg.ai.maxRoster;

        /// <summary>A listed lot with its F2 start price.</summary>
        internal AuctionLot ListedLot(Club c, Player p, bool forced)
        {
            long s = ListedStartPrice(p);
            if (forced) _w.AddNews(new NewsItem { Kind = NewsKind.ForcedListing, ClubId = c.Id, PlayerName = p.Name });
            return new AuctionLot { Player = p, Kind = LotKind.Listed, SellerId = c.Id, StartPrice = s, Price = s, Forced = forced };
        }

        private void DoScout(Club c, string lane)
        {
            ScoutLanes[c.Id] = lane;
            Scouted[c.Id] = _w.Scout(c, lane);
            if (!IsPlayer(c)) _w.Ai.PickLineup(c);
        }

        private void RunChoices()
        {
            long minLot = MinLotFirstBid();
            foreach (var c in _w.Clubs)
            {
                if (IsPlayer(c)) continue;
                var ch = AiChoice(c, minLot, out var sell, out var lane);
                bool debtSale = ch == WindowChoice.Sell && c.Debt > Cfg.economy.debtLine * WageBill(c);
                Choices[c.Id] = ch;
                if (ch == WindowChoice.Scout) DoScout(c, lane);
                foreach (var p in sell) Lots.Add(ListedLot(c, p, debtSale));
            }
            // forced listings of refused renegotiations (whatever the club chose), club id order
            foreach (var c in _w.Clubs)
            {
                int n = 0;
                foreach (var l in Lots) if (l.SellerId == c.Id) n++;
                foreach (var p in new List<Player>(c.Team.Roster))
                {
                    if (_w.Market(p).AutoListYear != Year || Lots.Exists(l => l.Player == p) || !CanList(c, p, n)) continue;
                    Lots.Add(ListedLot(c, p, true));
                    n++;
                }
            }
            // order: listings, then youth, then free agents; each by start price (high first), ties player id
            var order = new List<AuctionLot>(Lots);
            order.Sort((a, b) =>
            {
                int k = ((int)a.Kind).CompareTo((int)b.Kind);
                if (k != 0) return k;
                k = b.StartPrice.CompareTo(a.StartPrice);
                return k != 0 ? k : a.Player.Id.CompareTo(b.Player.Id);
            });
            Lots.Clear(); Lots.AddRange(order);
        }

        // ================================================================== 6. auction

        /// <summary>F4 AI max bid: value x (0.9 + 0.5 need) x (1 + 0.2 aggression) x (1 + offset) x U(0.9, 1.1) (key "bid", year, club, player).</summary>
        public double AiMaxBid(Club c, Player p)
        {
            double u = 1.0 - X.bidNoise + 2.0 * X.bidNoise * _w.Keys.U01("bid", Year, c.Id, p.Id);
            return MaxBid(Eco.Value(p), Need(c, p.MainLane), c.Personality.Aggression, c.Personality.OffBid, u);
        }

        /// <summary>F4 with every input given: value x (0.9 + 0.5 need) x (1 + 0.2 aggression) x (1 + offset) x u.</summary>
        public double MaxBid(double value, float need, float aggression, float offset, double u)
            => value * (X.bidBase + X.bidNeed * need) * (1.0 + X.bidAggression * aggression) * (1.0 + offset) * u;

        /// <summary>
        /// AI bidding test (transfer.md 6): raise when price + step &lt;= max bid, funds - bid >= reserve and the roster stays &lt;= 8.
        /// </summary>
        public bool AiRaises(double maxBid, long price, long funds, double reserve, int roster)
        {
            long bid = price + Step3(price);
            return bid <= maxBid && funds - bid >= reserve && roster + 1 <= Cfg.ai.maxRoster;
        }

        /// <summary>need = 1 (no main-lane player or the club's weakest lane), 0.5 (one main-lane player), else 0.</summary>
        public float Need(Club c, string lane)
        {
            int n = 0;
            foreach (var p in c.Team.Roster) if (p.MainLane == lane) n++;
            if (n == 0 || lane == _w.WeakestLane(c)) return 1f;
            return n == 1 ? 0.5f : 0f;
        }

        /// <summary>The lot being bid on (when <see cref="Waiting"/> is AuctionBid), else null.</summary>
        public AuctionLot CurrentLot => Step == TransferStep.Auction && _lotIndex < Lots.Count ? Lots[_lotIndex] : null;

        /// <summary>The player's auto-bid limit per lot (player id -> X).</summary>
        private readonly Dictionary<long, long> _autoBid = new Dictionary<long, long>();
        private int _playerAction; // 0 none, 1 raise once, 2 exit

        /// <summary>Sets the player's auto-bid limit on a lot directly (tests).</summary>
        internal void SetAutoBid(long playerId, long limit) => _autoBid[playerId] = limit;

        /// <summary>The player raises once on the running lot.</summary>
        public TransferError BidRaise() => BidAction(1, 0);

        /// <summary>The player leaves the running lot.</summary>
        public TransferError BidExit() => BidAction(2, 0);

        /// <summary>The player bids automatically up to <paramref name="limit"/> on the running lot (raise while price + step &lt;= X).</summary>
        public TransferError BidAuto(long limit) => BidAction(3, limit);

        private TransferError BidAction(int a, long limit)
        {
            if (Waiting != TransferWait.AuctionBid) return TransferError.WrongStep;
            var lot = CurrentLot;
            if (a == 1 && !PlayerCanBid(lot)) return Player.Team.Roster.Count + 1 > Cfg.ai.maxRoster ? TransferError.RosterFull : TransferError.Funds;
            if (a == 3) _autoBid[lot.Player.Id] = limit;
            else _playerAction = a;
            Waiting = TransferWait.None;
            return TransferError.None;
        }

        /// <summary>The next bid on a lot (current price + step).</summary>
        public long NextBid(AuctionLot lot) => lot.Price + Step3(lot.Price);

        /// <summary>True when the player club can afford and hold the next bid on <paramref name="lot"/>.</summary>
        public bool PlayerCanBid(AuctionLot lot)
            => lot != null && Player != null && Player.Team.Roster.Count + 1 <= Cfg.ai.maxRoster && Player.Team.Funds >= NextBid(lot);

        /// <summary>Seats the bidders of a lot (auction choosers except the seller, roster &lt; 8; popularity order) and their F4 limits.</summary>
        internal void OpenLot(AuctionLot lot)
        {
            var parts = new List<Club>();
            foreach (var c in _w.Clubs)
                if (Choices.TryGetValue(c.Id, out var ch) && ch == WindowChoice.Auction && c.Id != lot.SellerId && c.Team.Roster.Count < Cfg.ai.maxRoster)
                    parts.Add(c);
            parts.Sort((a, b) => { int k = b.Team.ClubPopularity.CompareTo(a.Team.ClubPopularity); return k != 0 ? k : a.Id.CompareTo(b.Id); });
            foreach (var c in parts)
            {
                lot.InClubs.Add(c.Id);
                if (!IsPlayer(c)) lot.Limits[c.Id] = AiMaxBid(c, lot.Player);
            }
            lot.Cursor = 0; lot.RaisedThisRound = false; lot.Price = lot.StartPrice;
        }

        // Runs lots from the cursor; false = waiting for the player's bid decision.
        private bool RunAuction()
        {
            while (_lotIndex < Lots.Count)
            {
                var lot = Lots[_lotIndex];
                if (!lot.Opened)
                {
                    lot.Opened = true;
                    bool valid = lot.SellerId >= 0 ? _w.Clubs[lot.SellerId].Find(lot.Player.Id) != null : _w.FreeAgents.Contains(lot.Player);
                    if (!valid) { lot.Finished = true; lot.TopBidder = -1; _lotIndex++; continue; }
                    OpenLot(lot);
                }
                if (!RunLot(lot)) return false;
                _lotIndex++;
            }
            return true;
        }

        /// <summary>Runs the open-cry rounds of a lot; false = waiting for the player.</summary>
        internal bool RunLot(AuctionLot lot)
        {
            while (!lot.Finished)
            {
                while (lot.Cursor < lot.InClubs.Count)
                {
                    int id = lot.InClubs[lot.Cursor];
                    var c = _w.Clubs[id];
                    if (id == lot.TopBidder) { lot.Cursor++; continue; }
                    long bid = NextBid(lot);
                    bool ok;
                    if (IsPlayer(c))
                    {
                        if (_autoBid.TryGetValue(lot.Player.Id, out long lim)) ok = bid <= lim && PlayerCanBid(lot);
                        else if (_playerAction == 0) return false; // ask the player
                        else ok = _playerAction == 1 && PlayerCanBid(lot);
                        _playerAction = 0;
                    }
                    else
                    {
                        double reserve = X.reserveSegments * c.WagesPerSegment(_w.SegmentsPerYear);
                        if (!lot.Limits.ContainsKey(id)) lot.Limits[id] = AiMaxBid(c, lot.Player);
                        ok = AiRaises(lot.Limits[id], lot.Price, c.Team.Funds, reserve, c.Team.Roster.Count);
                    }
                    if (ok)
                    {
                        lot.Price = bid; lot.TopBidder = id; lot.RaisedThisRound = true; lot.Bidders.Add(id);
                        lot.Cursor++;
                    }
                    else lot.InClubs.RemoveAt(lot.Cursor);
                }
                if (!lot.RaisedThisRound) { FinishLot(lot); break; }
                lot.Cursor = 0; lot.RaisedThisRound = false;
            }
            return true;
        }

        private void FinishLot(AuctionLot lot)
        {
            lot.Finished = true;
            var p = lot.Player;
            if (lot.TopBidder < 0)
            {
                if (lot.SellerId < 0) _w.Market(p).LockedYear = Year;
                return;
            }
            var w = _w.Clubs[lot.TopBidder];
            w.Team.Funds -= lot.Price;
            if (lot.SellerId < 0)
            {
                _w.Sign(w, p, lot.Kind == LotKind.Youth ? X.auctionYouthContract : X.auctionFreeContract, LeagueWorld.SalaryKind.Auction);
                _w.News.RemoveAt(_w.News.Count - 1); // the generic signing line is replaced by the auction line
            }
            else
            {
                var s = _w.Clubs[lot.SellerId];
                s.Team.RemovePlayer(p);
                _w.PayIn(s, lot.Price);
                p.ResetObservation();
                _w.Market(p).JoinedYear = Year;
                w.Team.AddPlayer(p);
                Lineup(s);
            }
            Lineup(w);
            _w.AddNews(new NewsItem
            {
                Kind = NewsKind.AuctionBuy, ClubId = w.Id, OtherClubId = lot.SellerId,
                OtherClubName = lot.SellerId >= 0 ? _w.Clubs[lot.SellerId].Name : "", PlayerName = p.Name, Amount = lot.Price,
            });
            _w.Invalidate();
        }

        // ================================================================== 7. re-sign

        /// <summary>Free agents a club may sign now (unsold league lots of this window excluded).</summary>
        public List<Player> SignablePool()
        {
            var list = new List<Player>();
            foreach (var p in _w.FreeAgents) if (_w.Market(p).LockedYear != Year) list.Add(p);
            list.Sort((a, b) => { int k = b.GetLaneScore(b.MainLane).CompareTo(a.GetLaneScore(a.MainLane)); return k != 0 ? k : a.Id.CompareTo(b.Id); });
            return list;
        }

        private bool Signable(Player p) => _w.Market(p).LockedYear != Year;

        /// <summary>
        /// AI re-sign (transfer.md 7): empty lanes first, then to resign_target (weakest lane first), signing the best-rated
        /// signable player while funds >= 0; then, ignoring funds, until >= 5 players and every lane has a main-lane player.
        /// </summary>
        internal void AiResign(Club c)
        {
            var team = c.Team;
            int max = Cfg.ai.maxRoster;
            foreach (var lane in _w.MissingLanes(c))
            {
                if (team.Roster.Count >= max) break;
                if (team.Funds < 0) break;
                var p = _w.BestFree(new[] { lane }, Signable);
                if (p != null) SignResign(c, p);
            }
            while (team.Roster.Count < X.resignTarget && team.Funds >= 0)
            {
                var p = _w.BestFree(new[] { _w.WeakestLane(c) }, Signable) ?? _w.BestFree(null, Signable);
                if (p == null) break;
                SignResign(c, p);
            }
            while (team.Roster.Count < Cfg.ai.minRoster || (_w.MissingLanes(c).Count > 0 && team.Roster.Count < max))
            {
                var missing = _w.MissingLanes(c);
                var p = missing.Count > 0 ? _w.BestFree(missing, Signable) : _w.BestFree(null, Signable);
                if (p == null)
                {
                    if (team.Roster.Count >= Cfg.ai.minRoster) break;
                    _w.EmergencySign(c);
                    continue;
                }
                SignResign(c, p);
            }
        }

        private void SignResign(Club c, Player p)
        {
            _w.Sign(c, p, X.resignContract);
            Resigned.Add((c.Id, p));
            if (!IsPlayer(c)) _w.Ai.PickLineup(c);
            else Lineup(c);
        }

        // Runs the clubs in id order from the cursor; false = it is the player's turn.
        private bool RunResign()
        {
            while (_resignIndex < _w.Clubs.Count)
            {
                var c = _w.Clubs[_resignIndex];
                if (IsPlayer(c))
                {
                    EnsureLanesAvailable(c);
                    return false;
                }
                AiResign(c);
                _w.Ai.PickLineup(c);
                _resignIndex++;
            }
            return true;
        }

        // talent.md: an empty pool generates an emergency player, so a missing lane always has a candidate for the player.
        private void EnsureLanesAvailable(Club c)
        {
            foreach (var lane in _w.MissingLanes(c))
            {
                if (_w.BestFree(new[] { lane }, Signable) != null) continue;
                var rng = _w.Keys.Stream("emg", Year, -1, c.Id, _w.Talent.NextId);
                int age = Cfg.talent.emergencyAge;
                _w.FreeAgents.Add(_w.Talent.Generate(rng, lane, age, age, _w.Talent.SupplyMean(_w.LeaguePopularity()), 0f));
            }
        }

        /// <summary>The player signs a free agent (2 years, signing salary); up to 8 players; not this window's unsold league lots.</summary>
        public TransferError SignFree(long playerId)
        {
            if (Waiting != TransferWait.Resign) return TransferError.WrongStep;
            var c = Player;
            if (c.Team.Roster.Count >= Cfg.ai.maxRoster) return TransferError.RosterFull;
            Player p = null;
            foreach (var q in _w.FreeAgents) if (q.Id == playerId) p = q;
            if (p == null) return TransferError.Unknown;
            if (!Signable(p)) return TransferError.NotAllowed;
            SignResign(c, p);
            return TransferError.None;
        }

        /// <summary>The salary a free agent would ask from the player club now (2 years; the same keyed R as the signing).</summary>
        public SalaryBreakdown ResignAsk(Player p) => Eco.Breakdown(p, X.resignContract, Eco.DrawSalaryRandom(_w.Keys.Stream("sal", _w.Year, p.Id)));

        /// <summary>None when the player club may end the window (>= 5 players, no lane without a main-lane player).</summary>
        public TransferError CanFinish()
        {
            var c = Player;
            if (c == null) return TransferError.None;
            if (c.Team.Roster.Count < Cfg.ai.minRoster || _w.MissingLanes(c).Count > 0) return TransferError.IncompleteRoster;
            return TransferError.None;
        }

        /// <summary>The player ends his re-sign turn ("结束转会期"); the remaining clubs sign and the window closes.</summary>
        public TransferError FinishResign()
        {
            if (Waiting != TransferWait.Resign) return TransferError.WrongStep;
            var e = CanFinish();
            if (e != TransferError.None) return e;
            Lineup(Player);
            _resignIndex++;
            Waiting = TransferWait.None;
            return Continue();
        }

        private void FinishWindow()
        {
            // pool cap after re-signing: this window's unsold league lots neither count nor go
            var counted = new List<Player>();
            foreach (var p in _w.FreeAgents) if (_w.Market(p).LockedYear != Year) counted.Add(p);
            foreach (var p in _w.Talent.ApplyPoolCap(counted, null)) { _w.FreeAgents.Remove(p); Retired.Add(p); }
            foreach (var c in _w.Clubs) if (!IsPlayer(c)) _w.Ai.PickLineup(c); else Lineup(c);
        }

        // ================================================================== helpers

        /// <summary>True when <paramref name="p"/> is the only main-lane player of his lane in the club.</summary>
        public bool SoleMain(Club c, Player p)
        {
            int n = 0;
            foreach (var q in c.Team.Roster) if (q.MainLane == p.MainLane) n++;
            return n <= 1;
        }

        /// <summary>Sum of the club's salaries (yearly wage bill).</summary>
        public static long WageBill(Club c)
        {
            long s = 0;
            foreach (var p in c.Team.Roster) s += p.Salary;
            return s;
        }

        private double FloorWith(Club c, long bill)
        {
            var a = Cfg.ai;
            return (a.floorBase + a.floorFrugal * c.Personality.Frugality + c.Personality.OffCash) * bill / (double)_w.SegmentsPerYear;
        }

        /// <summary>The player club's funds runway: segments of wages the funds cover ("还能撑几个赛段"); int.MaxValue without wages.</summary>
        public static int Runway(Club c, int segmentsPerYear)
        {
            double w = c.WagesPerSegment(segmentsPerYear);
            if (w <= 0) return int.MaxValue;
            return c.Team.Funds <= 0 ? 0 : (int)Math.Floor(c.Team.Funds / w);
        }
    }
}
