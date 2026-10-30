using EsportsManager.Events;
using System;
using System.Collections.Generic;
using EsportsManager.Growth;
using EsportsManager.Leagues;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;

namespace EsportsManager.Session
{
    /// <summary>Everything the main screen shows about one player. Deliberately has NO true-potential field.</summary>
    public sealed class PlayerCardData
    {
        /// <summary>Player.Id.</summary>
        public long Id;
        /// <summary>Display name.</summary>
        public string Name;
        /// <summary>Age in years.</summary>
        public int Age;
        /// <summary>Main lane id.</summary>
        public string LaneId;
        /// <summary>Main lane display name (from config).</summary>
        public string LaneName;
        /// <summary>The two personality words (display names from config).</summary>
        public string[] TraitNames;
        /// <summary>Mood 0-100.</summary>
        public float Mood;
        /// <summary>Stress 0-100.</summary>
        public float Stress;
        /// <summary>Looks 0-100.</summary>
        public float Looks;
        /// <summary>Popularity 0-100.</summary>
        public float Popularity;
        /// <summary>Salary, yuan per year.</summary>
        public int Salary;
        /// <summary>Contract years left.</summary>
        public int ContractYearsLeft;
        /// <summary>Main-lane dimension display names, in lane order.</summary>
        public string[] DimensionNames;
        /// <summary>Main-lane dimension values, in lane order.</summary>
        public float[] DimensionValues;
        /// <summary>Lower end of the player-facing potential estimate.</summary>
        public int PotentialLow;
        /// <summary>Upper end of the player-facing potential estimate.</summary>
        public int PotentialHigh;
        /// <summary>True when he is a starter in his lane.</summary>
        public bool IsStarter;
        /// <summary>Current main-lane rating (lane score from the current dimensions; never the hidden potential).</summary>
        public float Rating;
        /// <summary>True when his stress is above mood.high_stress_threshold (the same test the mood formula uses).</summary>
        public bool HighStress;
        /// <summary>Phase focus early / mid / late, 0-1 (growth.md 专精条).</summary>
        public float[] Focus = new float[3];
        /// <summary>Main-lane dimension indexes of each phase ([phase][]); empty arrays without the growth system.</summary>
        public int[][] PhaseDimensions = { new int[0], new int[0], new int[0] };
        /// <summary>Main-lane index of the command dimension (-1 without the growth system).</summary>
        public int CommandIndex = -1;
        /// <summary>Per main-lane dimension: "接近上限" (ceiling - value below near_cap_margin). The potential is never exposed.</summary>
        public bool[] NearCap = new bool[0];
        /// <summary>Display names of the owned specialties.</summary>
        public string[] SpecialtyNames = new string[0];
        /// <summary>Distance to every specialty of his main lane ("离绝活还差"), config order.</summary>
        public List<SpecialtyProgressData> SpecialtyProgress = new List<SpecialtyProgressData>();
    }

    /// <summary>One "离绝活还差" line of a player card (growth.md UI): gaps only, never the potential.</summary>
    public sealed class SpecialtyProgressData
    {
        /// <summary>Specialty id.</summary>
        public string Id;
        /// <summary>Specialty display name.</summary>
        public string Name;
        /// <summary>Phase 0-2, or -1 for an any-lane specialty (focus not needed).</summary>
        public int Phase;
        /// <summary>Main-lane dimension index and its display name.</summary>
        public int DimensionIndex;
        /// <summary>Dimension display name.</summary>
        public string DimensionName;
        /// <summary>max(0, 76 - d).</summary>
        public float DimensionGap;
        /// <summary>max(0, 0.55 - focus) (0 for an any-lane specialty).</summary>
        public float FocusGap;
        /// <summary>True when owned ("已解锁").</summary>
        public bool Owned;
    }

    /// <summary>Training pop-up estimate of ONE more point (growth.md "训练预估": a +-noise range, no event modifiers).</summary>
    public sealed class TrainingPointPreview
    {
        /// <summary>Gain per main-lane dimension at the lowest / highest noise.</summary>
        public float[] Low, High;
        /// <summary>Dimension indexes of the target.</summary>
        public int[] TargetDimensions;
    }

    /// <summary>Header numbers of the main screen.</summary>
    public sealed class TeamData
    {
        /// <summary>Team name.</summary>
        public string Name;
        /// <summary>
        /// Phase strengths S_E / S_M / S_L of the current lineup, tactic and specialty hooks (the match's pre-match estimate;
        /// user decision 2026-10-28 replaces the old single TeamStrength number). Null without a league or with an incomplete lineup.
        /// </summary>
        public float[] PhaseStrength;
        /// <summary>Club popularity 0-100 (second corner).</summary>
        public float Popularity;
        /// <summary>Funds, yuan; may be negative (third corner).</summary>
        public long Funds;
        /// <summary>Team chemistry.</summary>
        public float Chemistry;
        /// <summary>Action points per segment.</summary>
        public int ActionPoints;
        /// <summary>Action points not yet used by the draft plan.</summary>
        public int PointsRemaining;
        /// <summary>Per-player action point cap.</summary>
        public int MaxPerPlayer;
        /// <summary>1-based number of the open segment.</summary>
        public int SegmentNumber;
        /// <summary>Segments per year (calendar placeholder until Story 005).</summary>
        public int SegmentsPerYear;
    }

    /// <summary>One resolved segment plus the team numbers before and after it (for the settlement panel).</summary>
    public sealed class SegmentResult
    {
        /// <summary>1-based number of the resolved segment.</summary>
        public int SegmentNumber;
        /// <summary>The runner's summary.</summary>
        public SegmentSummary Summary;
        /// <summary>Funds before / after, yuan.</summary>
        public long FundsBefore, FundsAfter;
        /// <summary>Club popularity before / after.</summary>
        public float PopularityBefore, PopularityAfter;
        /// <summary>Team chemistry before / after.</summary>
        public float ChemistryBefore, ChemistryAfter;
        /// <summary>Phase strengths (pre-match estimate) before / after; null when unavailable (no league, incomplete lineup).</summary>
        public float[] PhaseStrengthBefore, PhaseStrengthAfter;
    }

    /// <summary>One lineup slot for the lineup screen (no text besides config names).</summary>
    public sealed class LineupSlotData
    {
        /// <summary>Lane id.</summary>
        public string LaneId;
        /// <summary>Lane display name (config).</summary>
        public string LaneName;
        /// <summary>Starter's Player.Id, or null when the slot is empty.</summary>
        public long? PlayerId;
        /// <summary>Starter's name, or empty.</summary>
        public string PlayerName;
    }

    /// <summary>
    /// Pre-match estimate of the player club (design/gdd/match.md "赛前预估", Story 011): exact phase strengths, execution and the
    /// weakest lane of every phase for the current lineup and tactic. No opponent data and no win probability.
    /// </summary>
    public sealed class MatchPreviewData
    {
        /// <summary>S_E, S_M, S_L (index = (int)MatchPhase).</summary>
        public float[] Strength = new float[3];
        /// <summary>Execution 0.3-1.</summary>
        public float Execution;
        /// <summary>Lane display name of the weakest lane per phase.</summary>
        public string[] WeakestLaneName = new string[3];
        /// <summary>Starter name in that lane.</summary>
        public string[] WeakestPlayerName = new string[3];
        /// <summary>His phase rating r(i,k).</summary>
        public float[] WeakestRating = new float[3];
    }

    /// <summary>Current lineup and tactic (Story 004).</summary>
    public sealed class LineupData
    {
        /// <summary>One slot per lane, config lane order.</summary>
        public List<LineupSlotData> Slots = new List<LineupSlotData>();
        /// <summary>Rostered players who do not start, roster order.</summary>
        public List<long> Bench = new List<long>();
        /// <summary>The tactic the next resolve will use.</summary>
        public MatchTactic Tactic;
        /// <summary>True when every lane has a starter.</summary>
        public bool IsComplete;
    }

    /// <summary>
    /// Plain-C# game state for the text UI (Story 009; no UnityEngine dependency): wraps generator, team and
    /// <see cref="SegmentRunner"/>, owns the draft <see cref="SegmentPlan"/> and exposes view data. It contains no
    /// formulas; it forwards to the runner. Deterministic for a fixed seed.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Default demo seed.</summary>
        public const int DefaultSeed = 20260101;
        /// <summary>Default starting funds, yuan.</summary>
        public const long DefaultFunds = 800000;
        /// <summary>Segments per year of a session WITHOUT a match config (with one, the calendar config decides).</summary>
        public const int DefaultSegmentsPerYear = 12;

        /// <summary>Segments per year: calendar.segments_per_year, or <see cref="DefaultSegmentsPerYear"/> without a match config.</summary>
        public int SegmentsPerYear => Season != null ? Season.SegmentsPerYear : DefaultSegmentsPerYear;

        private readonly PlayerConfig _players;
        private readonly SegmentConfig _segment;
        private int _segmentsPlayed;
        private readonly HashSet<int> _declined = new HashSet<int>();

        /// <summary>The player's team (null while the job offers are pending).</summary>
        public Team Team { get; private set; }
        /// <summary>The player club's runner (state, validation, preview, resolve); null while the job offers are pending.</summary>
        public SegmentRunner Runner { get; private set; }
        /// <summary>The whole league (Story 013), or null for a session without a match config.</summary>
        public LeagueWorld World { get; }
        /// <summary>The player club (null without a league or while the offers are pending).</summary>
        public Club PlayerClub => World?.PlayerClub;
        /// <summary>True after the warm-up while the player has not picked one of the three offers yet.</summary>
        public bool ChoosingOffer => World != null && World.PlayerClubId < 0;
        /// <summary>The draft plan the UI edits.</summary>
        public SegmentPlan Plan { get; private set; } = new SegmentPlan();
        /// <summary>The seed this session was built with.</summary>
        public int Seed { get; }
        /// <summary>Free agents (the league's pool with a league; otherwise the generated pool minus the roster).</summary>
        public IReadOnlyList<Player> FreeAgents => World != null ? (IReadOnlyList<Player>)World.FreeAgents : _free;
        private readonly List<Player> _free = new List<Player>();

        /// <summary>Builds a session from the three config JSON texts WITHOUT a league (no matches; Story 003/009 behaviour).</summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson,
            int seed = DefaultSeed, long startingFunds = DefaultFunds)
        {
            return new GameSession(PlayerConfigLoader.FromJson(playerJson), TeamConfigLoader.FromJson(teamJson),
                SegmentConfigLoader.FromJson(segmentJson), seed, startingFunds);
        }

        /// <summary>
        /// Builds a session WITH the league (Story 013: warm-up year, then the MID offer is taken automatically under the default
        /// name). <paramref name="matchJson"/> null = no league (then <paramref name="leagueJson"/> is ignored).
        /// </summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson, string matchJson, string leagueJson,
            int seed = DefaultSeed, long startingFunds = DefaultFunds)
            => FromJson(playerJson, teamJson, segmentJson, matchJson, null, leagueJson, seed, startingFunds);

        /// <summary>
        /// Builds a session with the league AND random events (Story 006). <paramref name="eventJson"/> null = no events.
        /// <paramref name="autoTakeOffer"/> false leaves the three job offers pending (the new-game screen); true takes the mid offer.
        /// </summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson, string matchJson, string eventJson,
            string leagueJson, int seed = DefaultSeed, long startingFunds = DefaultFunds, bool autoTakeOffer = true)
        {
            var players = PlayerConfigLoader.FromJson(playerJson);
            return new GameSession(players, TeamConfigLoader.FromJson(teamJson), SegmentConfigLoader.FromJson(segmentJson), seed, startingFunds,
                matchJson == null ? null : MatchConfigLoader.FromJson(matchJson),
                eventJson == null ? null : EventConfigLoader.FromJson(eventJson, players),
                matchJson == null || leagueJson == null ? null : LeagueConfigLoader.FromJson(leagueJson), autoTakeOffer);
        }

        /// <summary>Event text table for the UI (set by the bootstrap; null in tests that do not need texts).</summary>
        public EventTextTable EventTexts { get; set; }

        /// <summary>The random event engine (Story 006), or null when the session has no event config.</summary>
        public EventEngine Events { get; private set; }

        /// <summary>The tournament runner (league, opponents, simulator), or null for a session without matches.</summary>
        public TournamentRunner Tournament { get; private set; }
        /// <summary>Calendar + event state machine (numbers.md 4.18), or null for a session without matches.</summary>
        public SeasonFlow Season { get; private set; }
        /// <summary>The match config, or null.</summary>
        public MatchConfig MatchConfig => Tournament?.Config;
        /// <summary>The growth system (Story 012), or null for a session without a match config (it needs the phase mapping).</summary>
        public GrowthSystem Growth { get; private set; }

        /// <summary>
        /// WITHOUT a match config: generates the pool, takes the best main-lane player of every lane as starter plus the best
        /// remaining player as bench, and opens the first segment (Story 003 / 009 behaviour; no league, no training).
        /// WITH a match config (Story 013): builds the whole league (<see cref="LeagueWorld"/>, keyed random numbers from
        /// <paramref name="seed"/>), runs the warm-up year and makes the three job offers; <paramref name="autoTakeOffer"/> then takes
        /// the mid offer under the default name and opens segment 1 of year 1 (<paramref name="startingFunds"/> is ignored: the
        /// club's own funds plus the takeover top-up apply).
        /// </summary>
        public GameSession(PlayerConfig players, TeamConfig team, SegmentConfig segment, int seed = DefaultSeed,
            long startingFunds = DefaultFunds, MatchConfig match = null, EventConfig events = null, LeagueConfig league = null,
            bool autoTakeOffer = true)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _segment = segment ?? throw new ArgumentNullException(nameof(segment));
            Seed = seed;
            if (match != null)
            {
                if (league == null) throw new ArgumentNullException(nameof(league), "A session with matches needs the league config.");
                World = new LeagueWorld(players, team, segment, match, events, league, seed);
                Tournament = World.Tournament;
                Season = World.Season;
                Growth = World.Growth;
                World.RunWarmUp();
                if (autoTakeOffer) TakeOffer(1, "");
                return;
            }
            var generator = new PlayerGenerator(players, new SeededRandomSource(seed));
            var pool = generator.GeneratePool();
            Team = new Team(team, players, new SeededRandomSource(seed + 1), startingFunds, "Demo");
            var used = new HashSet<long>();
            foreach (var lane in players.lanes)
            {
                Player best = null;
                foreach (var p in pool)
                    if (p.MainLane == lane.id && (best == null || p.GetLaneScore(lane.id) > best.GetLaneScore(lane.id))) best = p;
                if (best == null) continue;
                Team.AddPlayer(best);
                Team.SetStarter(lane.id, best);
                used.Add(best.Id);
            }
            Player bench = null;
            foreach (var p in pool)
                if (!used.Contains(p.Id) && (bench == null || p.GetLaneScore(p.MainLane) > bench.GetLaneScore(bench.MainLane))) bench = p;
            if (bench != null) { Team.AddPlayer(bench); used.Add(bench.Id); }
            foreach (var p in pool) if (!used.Contains(p.Id)) _free.Add(p);
            if (events != null) Events = new EventEngine(events, players, new SeededRandomSource(seed + 5));
            Runner = new SegmentRunner(segment, players, Team, new SeededRandomSource(seed + 2), Events, null, new SeededRandomSource(seed + 6));
            Runner.BeginSegment();
        }

        /// <summary>The three job offers after the warm-up (empty once one was taken or without a league).</summary>
        public IReadOnlyList<JobOffer> Offers => World != null ? (IReadOnlyList<JobOffer>)World.Offers : new JobOffer[0];

        /// <summary>
        /// Takes offer <paramref name="index"/> (0 strong, 1 mid, 2 weak) and names the club (normalised like <see cref="SetClubName"/>;
        /// empty = keep the club's name), then opens segment 1 of year 1. After a dismissal (economy.md 财务危机与解雇) the season
        /// goes on: the settlement is still up (<see cref="AwaitingNext"/>), so the next segment opens with <see cref="BeginNext"/>.
        /// Throws when no offer is pending.
        /// </summary>
        public Club TakeOffer(int index, string name)
        {
            if (!ChoosingOffer) throw new InvalidOperationException("No job offer is pending.");
            if (index < 0 || index >= World.Offers.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var offer = World.Offers[index];
            string old = World.Clubs[offer.ClubId].Name;
            var c = World.TakeOver(offer, old);
            Team = c.Team;
            Runner = c.Runner;
            Events = c.Events;
            if (!string.IsNullOrWhiteSpace(name)) SetClubName(name);
            Plan = new SegmentPlan();
            _declined.Clear(); _mediaDeclined.Clear();
            if (!AwaitingNext) World.BeginSegment();
            return c;
        }

        /// <summary>Current runner snapshot (offers, contracts, last summary).</summary>
        public SegmentState State => Runner.GetState();

        /// <summary>The calendar inputs of the open segment. MatchResults is always null: matches happen in events (numbers.md 4.18).</summary>
        public SegmentContext BuildContext() => new SegmentContext
        {
            SegmentsPerYear = SegmentsPerYear,
            SegmentIndexInYear = Season != null ? Season.SegmentIndexInYear : _segmentsPlayed % SegmentsPerYear,
            MatchResults = null,
            SeasonRankScore = 50f,
            PrizeMoney = 0,
        };

        /// <summary>
        /// Builds a session whose FIRST segment has at least one business offer, scanning seeds upwards from
        /// <paramref name="seed"/> (debug --force-offer / --demo offer). Returns the last tried session if none has one.
        /// </summary>
        public static GameSession FromJsonWithOffer(string playerJson, string teamJson, string segmentJson, int seed, int maxTries = 500)
            => FromJsonWithOffer(playerJson, teamJson, segmentJson, null, null, null, seed, maxTries);

        /// <summary>Same as the overload above, with the league (and random events when <paramref name="eventJson"/> is not null).</summary>
        public static GameSession FromJsonWithOffer(string playerJson, string teamJson, string segmentJson, string matchJson, string eventJson,
            string leagueJson, int seed, int maxTries = 500)
        {
            GameSession s = null;
            for (int i = 0; i < maxTries; i++)
            {
                s = FromJson(playerJson, teamJson, segmentJson, matchJson, eventJson, leagueJson, seed + i * 7);
                if (s.Runner.GetState().PendingOffers.Count > 0) return s;
            }
            return s;
        }

        /// <summary>Validates the draft plan; false plus the first error.</summary>
        public bool TryValidate(out PlanError error) => Runner.TryValidate(Plan, BuildContext(), out error);

        /// <summary>Live preview of the draft plan.</summary>
        public SegmentPreview Preview() => Runner.Preview(Plan, BuildContext());

        /// <summary>True between <see cref="Resolve"/> and <see cref="BeginNext"/> (the settlement panel is up).</summary>
        public bool AwaitingNext { get; private set; }
        /// <summary>The last resolved segment, or null.</summary>
        public SegmentResult LastResult { get; private set; }

        /// <summary>Resolves the draft plan (validated by the runner; throws on an invalid plan and changes nothing).</summary>
        public SegmentResult Resolve()
        {
            if (Season != null && Season.Phase != EventPhase.None) throw new InvalidOperationException("An event is in progress.");
            var r = new SegmentResult
            {
                SegmentNumber = _segmentsPlayed + 1, FundsBefore = Team.Funds, PopularityBefore = Team.ClubPopularity,
                ChemistryBefore = Team.Chemistry(), PhaseStrengthBefore = PhaseStrengths(),
            };
            r.Summary = World != null ? World.ResolveSegment(Plan) : Runner.Resolve(Plan, BuildContext());
            r.FundsAfter = Team.Funds; r.PopularityAfter = Team.ClubPopularity;
            r.ChemistryAfter = Team.Chemistry(); r.PhaseStrengthAfter = PhaseStrengths();
            _segmentsPlayed++;
            Plan = new SegmentPlan();
            _declined.Clear(); _mediaDeclined.Clear();
            AwaitingNext = true;
            LastResult = r;
            return r;
        }

        /// <summary>Opens the next segment (rolls its business offers). Throws while an event is not Done and acknowledged.</summary>
        public void BeginNext()
        {
            if (!AwaitingNext) throw new InvalidOperationException("Resolve the open segment first.");
            if (Season != null && Season.Phase != EventPhase.None) throw new InvalidOperationException("Finish and acknowledge the event first.");
            if (InTransferWindow) throw new InvalidOperationException("Finish the transfer window first.");
            AwaitingNext = false;
            if (World != null) World.BeginSegment(); else Runner.BeginSegment();
        }

        /// <summary>BeginNext without throwing: false + EventPending / SegmentNotOpen.</summary>
        public bool TryBeginNext(out PlanError error)
        {
            error = default;
            if (Season != null && Season.Phase != EventPhase.None || InTransferWindow) { error = new PlanError { Code = PlanErrorCode.EventPending }; return false; }
            if (!AwaitingNext) { error = new PlanError { Code = PlanErrorCode.SegmentNotOpen }; return false; }
            BeginNext();
            return true;
        }

        /// <summary>Resolve, play a pending event with the current lineup (<see cref="SeasonFlow.AutoPlay"/>), then BeginNext (scripted auto-play).</summary>
        public SegmentResult ResolveAndAdvance()
        {
            var r = Resolve();
            if (Season != null && Season.Phase != EventPhase.None) Season.AutoPlay();
            AutoCompleteTransferWindow();
            BeginNext();
            return r;
        }

        // ------------------------------------------------------------ plan editing (every change is validated)

        /// <summary>
        /// Adds one training point on a phase (or command) with an optional focus dimension (-1 = none); refused (false + error)
        /// when the plan would be invalid (TrainingUnavailable without the growth system).
        /// </summary>
        public bool TryAddTrain(long playerId, TrainingTarget target, int focusDimension, out PlanError error)
            => TryChange(p => p.Train(playerId, target, focusDimension), out error);

        /// <summary>Estimate of one more point (+-noise range) for the training pop-up; null without the growth system or for an unknown player.</summary>
        public TrainingPointPreview PreviewTrainingPoint(long playerId, TrainingTarget target, int focusDimension)
        {
            var p = FindRostered(playerId);
            if (Growth == null || p == null || !Growth.IsValidFocus(p.MainLane, target, focusDimension)) return null;
            float i = _segment.training.intensity;
            return new TrainingPointPreview
            {
                Low = Growth.PointGains(p, target, focusDimension, Growth.BaseAmount(p, target, i, Growth.NoiseLow)),
                High = Growth.PointGains(p, target, focusDimension, Growth.BaseAmount(p, target, i, Growth.NoiseHigh)),
                TargetDimensions = Growth.TargetDims(p.MainLane, target),
            };
        }

        /// <summary>Display name of a specialty id (falls back to the id).</summary>
        public string SpecialtyName(string id) => Growth?.Find(id)?.displayName ?? _players.specialty?.Find(id)?.displayName ?? id;

        private Player FindRostered(long id)
        {
            foreach (var p in Team.Roster) if (p.Id == id) return p;
            return null;
        }
        /// <summary>Adds one stream point.</summary>
        public bool TryAddStream(long playerId, out PlanError error) => TryChange(p => p.Stream(playerId), out error);
        /// <summary>Adds one rest point.</summary>
        public bool TryAddRest(long playerId, out PlanError error) => TryChange(p => p.Rest(playerId), out error);
        /// <summary>Adds one sponsorship attempt.</summary>
        public bool TryAddSponsor(out PlanError error) => TryChange(p => p.Sponsor(), out error);
        /// <summary>Accepts a pending business offer (costs one point on that player).</summary>
        public bool TryAcceptOffer(int offerId, out PlanError error)
        {
            if (!TryChange(p => p.AcceptOffer(offerId), out error)) return false;
            _declined.Remove(offerId);
            return true;
        }
        /// <summary>Whether accepting the offer would be valid right now (nothing changes).</summary>
        public bool CanAcceptOffer(int offerId, out PlanError error)
        {
            if (IsAccepted(offerId)) { error = new PlanError { Code = PlanErrorCode.DuplicateOffer, OfferId = offerId }; return false; }
            var c = Copy(Plan, -1, -1); c.AcceptOffer(offerId);
            return Runner.TryValidate(c, BuildContext(), out error) || error.Code == PlanErrorCode.IncompleteLineup;
        }
        /// <summary>Declines (dismisses) a pending offer for this segment.</summary>
        public void DeclineOffer(int offerId) => _declined.Add(offerId);
        /// <summary>True when the draft plan accepts the offer.</summary>
        public bool IsAccepted(int offerId) { foreach (int id in Plan.AcceptedOfferIds) if (id == offerId) return true; return false; }
        /// <summary>Pending offers neither accepted nor declined yet (these trigger the pop-up).</summary>
        public List<BusinessOffer> UnansweredOffers()
        {
            var list = new List<BusinessOffer>();
            if (AwaitingNext) return list;
            foreach (var o in Runner.GetState().PendingOffers)
                if (!_declined.Contains(o.Id) && !IsAccepted(o.Id)) list.Add(o);
            return list;
        }

        // ------------------------------------------------------------ choice events (Story 006)

        /// <summary>Choice events of the open segment (empty while the settlement panel is up or without events).</summary>
        public IReadOnlyList<PendingChoice> PendingChoices => AwaitingNext ? new PendingChoice[0] : Runner.GetState().PendingChoices;

        /// <summary>
        /// Answers a choice in the draft plan (validated: false + UnknownChoice / ChoiceOptionOutOfRange / PlayerOnLeave when the
        /// player already has action points and the option puts him on leave). Re-answering replaces the earlier answer.
        /// </summary>
        public bool TryAnswerChoice(int choiceId, int option, out PlanError error) => TryChange(p => p.AnswerChoice(choiceId, option), out error);

        /// <summary>The option that will apply at resolve: the draft answer, else the default; -1 for an unknown choice.</summary>
        public int EffectiveChoiceOption(int choiceId)
        {
            foreach (var a in Plan.ChoiceAnswers) if (a.ChoiceId == choiceId) return a.Option;
            foreach (var c in PendingChoices) if (c.Id == choiceId) return c.DefaultOption;
            return -1;
        }

        /// <summary>True when the draft plan answers the choice.</summary>
        public bool IsChoiceAnswered(int choiceId)
        {
            foreach (var a in Plan.ChoiceAnswers) if (a.ChoiceId == choiceId) return true;
            return false;
        }

        /// <summary>Removes the player's last assignment, else his last accepted offer. False when he has neither.</summary>
        public bool UndoLast(long playerId)
        {
            for (int i = Plan.Assignments.Count - 1; i >= 0; i--)
                if (Plan.Assignments[i].PlayerId == playerId) { Plan = Copy(Plan, i, -1); return true; }
            var pending = Runner.GetState().PendingOffers;
            for (int i = Plan.AcceptedOfferIds.Count - 1; i >= 0; i--)
                foreach (var o in pending)
                    if (o.Id == Plan.AcceptedOfferIds[i] && o.PlayerId == playerId) { Plan = Copy(Plan, -1, i); return true; }
            return false;
        }

        /// <summary>Removes one sponsorship attempt. False when there is none.</summary>
        public bool UndoSponsor()
        {
            if (Plan.SponsorPoints <= 0) return false;
            Plan.SponsorPoints--;
            return true;
        }

        /// <summary>Empties the draft plan's points (declined offers stay declined, choice answers stay).</summary>
        public void ClearPlan()
        {
            var c = new SegmentPlan();
            foreach (var a in Plan.ChoiceAnswers) c.AnswerChoice(a.ChoiceId, a.Option);
            Plan = c;
        }

        // ------------------------------------------------------------ media invitations, crisis, dismissal (2026-10-30)

        private readonly HashSet<int> _mediaDeclined = new HashSet<int>();

        /// <summary>Media invitations of the open segment (economy.md 媒体邀约); empty while the settlement is up or without a league.</summary>
        public IReadOnlyList<MediaInvitation> MediaInvitations
            => World == null || AwaitingNext || Runner == null ? (IReadOnlyList<MediaInvitation>)new MediaInvitation[0] : Runner.MediaInvitations;

        /// <summary>True when the draft plan accepts the invitation.</summary>
        public bool IsMediaAccepted(int id) { foreach (int x in Plan.AcceptedMediaIds) if (x == id) return true; return false; }

        /// <summary>Accepts an invitation (one club action point; refused when no point is left).</summary>
        public bool TryAcceptMedia(int id, out PlanError error)
        {
            if (IsMediaAccepted(id)) { error = new PlanError { Code = PlanErrorCode.DuplicateOffer, OfferId = id }; return false; }
            if (!TryChange(p => p.AcceptMedia(id), out error)) return false;
            _mediaDeclined.Remove(id);
            return true;
        }

        /// <summary>Declines an invitation (also withdraws an acceptance). Not accepted at resolve = declined (popularity cost).</summary>
        public void DeclineMedia(int id) { Plan.UnacceptMedia(id); _mediaDeclined.Add(id); }

        /// <summary>Invitations neither accepted nor declined yet (these open the pop-up).</summary>
        public List<MediaInvitation> UnansweredMedia()
        {
            var list = new List<MediaInvitation>();
            foreach (var m in MediaInvitations) if (!_mediaDeclined.Contains(m.Id) && !IsMediaAccepted(m.Id)) list.Add(m);
            return list;
        }

        /// <summary>Unacknowledged notices of the player club (fire sales, crisis start / end, chip-ins).</summary>
        public IReadOnlyList<ClubNotice> Notices => World != null ? (IReadOnlyList<ClubNotice>)World.Notices : new ClubNotice[0];

        /// <summary>Marks every notice as seen.</summary>
        public void AcknowledgeNotices() => World?.Notices.Clear();

        /// <summary>True while the player club is in a financial crisis.</summary>
        public bool InCrisis => PlayerClub != null && PlayerClub.InCrisis;

        /// <summary>Crisis checks left before the manager is fired (0 = no crisis).</summary>
        public int CrisisSegmentsLeft => PlayerClub?.CrisisSegmentsLeft ?? 0;

        /// <summary>True between a dismissal and taking a new offer (the 被解雇 screen).</summary>
        public bool Dismissed => World != null && World.DismissalPending;

        /// <summary>Times the manager was fired (year review).</summary>
        public int Dismissals => World?.Dismissals ?? 0;

        /// <summary>The open transfer window (after the finals until the player finishes re-signing), or null.</summary>
        public TransferWindow Window => World?.Window;

        /// <summary>True while the transfer window is open.</summary>
        public bool InTransferWindow => World?.Window != null;

        /// <summary>
        /// Lets the AI rules decide the player club's remaining window decisions and closes the window (scripted auto-play,
        /// tests). No-op outside the window.
        /// </summary>
        public void AutoCompleteTransferWindow() => World?.Window?.HandOverToAi();

        /// <summary>The player club's funds runway in segments of wages ("还能撑几个赛段").</summary>
        public int FundsRunway => PlayerClub == null ? int.MaxValue : TransferWindow.Runway(PlayerClub, SegmentsPerYear);

        // ------------------------------------------------------------ lineup, tactic, league (Story 004)

        /// <summary>The tactic the next event stage uses (Invade without a match config).</summary>
        public MatchTactic CurrentTactic => Season?.Tactic ?? MatchTactic.Invade;

        /// <summary>
        /// Chooses the tactic. Allowed while a segment is open (phase None) and in Prepare / PlayoffPrep; otherwise false +
        /// SegmentNotOpen / LineupLocked (UnknownTactic for an undefined value; NoLeague-like None-op without a match config).
        /// </summary>
        public bool SetTactic(MatchTactic tactic, out PlanError error)
        {
            error = default;
            if (Season == null) return false;
            error = Season.SetTactic(tactic, !AwaitingNext);
            return error.IsNone;
        }

        /// <summary>
        /// Moves a rostered player between bench and lineup: a starter goes to the bench (his lane becomes empty); a bench player
        /// starts in his MAIN lane and that lane's previous starter goes to the bench. The lineup persists to later segments.
        /// False + UnknownPlayer when he is not rostered; false + SegmentNotOpen while the settlement panel is up.
        /// </summary>
        public bool ToggleStarter(long playerId, out PlanError error)
        {
            error = default;
            if (Season != null) { error = Season.CanEditLineup(!AwaitingNext); if (!error.IsNone) return false; }
            else if (AwaitingNext) { error = new PlanError { Code = PlanErrorCode.SegmentNotOpen }; return false; }
            Player p = null;
            foreach (var x in Team.Roster) if (x.Id == playerId) p = x;
            if (p == null) { error = new PlanError { Code = PlanErrorCode.UnknownPlayer, PlayerId = playerId }; return false; }
            string lane = Team.LaneOf(p);
            if (lane != null) Team.ClearStarter(lane);
            else
            {
                if (Team.GetStarter(p.MainLane) != null) Team.ClearStarter(p.MainLane);
                Team.SetStarter(p.MainLane, p);
            }
            return true;
        }

        /// <summary>The lineup and tactic for the lineup screen.</summary>
        public LineupData GetLineup()
        {
            var d = new LineupData { Tactic = CurrentTactic };
            foreach (var lane in _players.lanes)
            {
                var p = Team.GetStarter(lane.id);
                d.Slots.Add(new LineupSlotData { LaneId = lane.id, LaneName = lane.displayName, PlayerId = p?.Id, PlayerName = p?.Name ?? "" });
            }
            foreach (var p in Team.Roster) if (Team.LaneOf(p) == null) d.Bench.Add(p.Id);
            d.IsComplete = Team.Starters().Count == _players.lanes.Length;
            return d;
        }

        /// <summary>IncompleteLineup error when a match config is attached and a lane is empty, otherwise a None error.</summary>
        public PlanError LineupError()
        {
            if (Tournament == null || Team.Starters().Count == _players.lanes.Length) return default;
            return new PlanError { Code = PlanErrorCode.IncompleteLineup, Used = Team.Starters().Count, Limit = _players.lanes.Length };
        }

        /// <summary>
        /// The pre-match estimate of the current lineup and tactic (same numbers as the first non-deciding game of a series);
        /// null without a match config or while the lineup is incomplete.
        /// </summary>
        public MatchPreviewData GetMatchPreview()
        {
            if (Tournament == null || !LineupError().IsNone) return null;
            var ev = Tournament.Simulator.Preview(MatchLineup.FromTeam(Team, CurrentTactic, Tournament.Hooks(Team)));
            var d = new MatchPreviewData { Execution = (float)ev.Execution };
            var layout = Tournament.Simulator.Layout;
            for (int k = 0; k < 3; k++)
            {
                d.Strength[k] = (float)ev.Strength[k];
                int lane = ev.WeakestLane[k];
                string laneId = layout.LaneIds[lane];
                d.WeakestLaneName[k] = _players.FindLane(laneId)?.displayName ?? laneId;
                d.WeakestPlayerName[k] = Team.GetStarter(laneId)?.Name ?? "";
                d.WeakestRating[k] = (float)ev.Input.Ratings[lane][k];
            }
            return d;
        }

        /// <summary>S_E / S_M / S_L of the current lineup, tactic and hooks (the match preview function); null when unavailable.</summary>
        public float[] PhaseStrengths() => GetMatchPreview()?.Strength;

        /// <summary>The club name rules (null without a league).</summary>
        public ClubConfig ClubRules => Tournament?.Config.club;

        /// <summary>
        /// Names the player club (new game): trimmed, cut to club.name_max_length, empty -> club.default_name. Updates the team and the
        /// league table. Returns the name actually used. Without a league the raw text is only trimmed.
        /// </summary>
        public string SetClubName(string raw)
        {
            string name = ClubRules != null ? ClubRules.NormalizeName(raw) : (raw ?? "").Trim();
            if (World != null && World.PlayerClub != null) World.Rename(World.PlayerClub, name); else Team.Rename(name);
            return name;
        }

        /// <summary>Regular-season table to show (running / latest split, else an empty table); empty without a match config.</summary>
        public IReadOnlyList<StandingsRow> GetStandings() => Season == null ? new List<StandingsRow>() : Season.CurrentStandings();

        /// <summary>Finals qualification table of the current year; empty without a match config.</summary>
        public IReadOnlyList<QualificationRow> GetQualification() => Season == null ? new List<QualificationRow>() : Season.Qualification();

        /// <summary>The next event and the segments until it ("距离下一项赛事还有 N 个赛段"); null without a match config / none left.</summary>
        public UpcomingEvent? NextEvent() => Season?.NextEvent();

        /// <summary>Current event phase (None without a match config).</summary>
        public EventPhase EventPhase => Season?.Phase ?? EventPhase.None;
        /// <summary>The running event (until acknowledged), or null.</summary>
        public EventRecord CurrentEvent => Season?.Current;
        /// <summary>The last completed event, or null.</summary>
        public EventRecord LastEvent => Season?.LastCompleted;

        /// <summary>Prepare (split) -> PlayoffPrep. False + WrongEventPhase / IncompleteLineup changes nothing.</summary>
        public bool TryPlayRegularSeason(out PlanError error) => Ok(error = Season?.PlayRegularSeason() ?? NoEvent());
        /// <summary>PlayoffPrep -> Done.</summary>
        public bool TryPlayPlayoffs(out PlanError error) => Ok(error = Season?.PlayPlayoffs() ?? NoEvent());
        /// <summary>Prepare (finals) -> Done.</summary>
        public bool TryPlayFinals(out PlanError error) => Ok(error = Season?.PlayFinals() ?? NoEvent());
        /// <summary>Plays whichever stage is next.</summary>
        public bool TryPlayNextEventStage(out PlanError error) => Ok(error = Season?.PlayNextStage() ?? NoEvent());
        /// <summary>Done -> None; then <see cref="BeginNext"/> may open the next segment.</summary>
        public bool TryAcknowledgeEvent(out PlanError error) => Ok(error = Season?.Acknowledge() ?? NoEvent());

        private static bool Ok(PlanError e) => e.IsNone;
        private static PlanError NoEvent() => new PlanError { Code = PlanErrorCode.WrongEventPhase };

        /// <summary>
        /// Demo / auto-play plan: accept every acceptable offer, one sponsorship attempt, one stream for the most
        /// popular player, then round-robin: rest when stress >= 60, otherwise train his most specialized phase (ties E > M > L)
        /// with that phase's specialty dimension as focus (its lowest dimension when the specialty dimension is near the ceiling),
        /// until no point can be added. Every step goes through the validated Try* methods.
        /// </summary>
        public void ApplyScriptedPlan()
        {
            foreach (var o in UnansweredOffers()) TryAcceptOffer(o.Id, out _);
            TryAddSponsor(out _);
            Player star = null;
            foreach (var p in Team.Roster) if (star == null || p.Popularity > star.Popularity) star = p;
            if (star != null) TryAddStream(star.Id, out _);
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var p in Team.Roster)
                {
                    bool ok;
                    if (p.Stress >= 60f || Growth == null) ok = TryAddRest(p.Id, out _);
                    else { var t = ScriptedTarget(p, out int focus); ok = TryAddTrain(p.Id, t, focus, out _); }
                    added |= ok;
                }
            }
        }

        private TrainingTarget ScriptedTarget(Player p, out int focus)
        {
            int k = 0;
            for (int i = 1; i < Player.FocusPhases; i++) if (p.GetFocus(i) > p.GetFocus(k)) k = i;
            focus = -1;
            foreach (var sp in Growth.Progress(p))
                if (sp.Phase == k && !Growth.IsNearCap(p, sp.DimensionIndex)) { focus = sp.DimensionIndex; break; }
            if (focus < 0)
            {
                var radar = p.GetRadar(p.MainLane);
                foreach (int d in Growth.Map.PhaseDims(p.MainLane, k)) if (focus < 0 || radar[d] < radar[focus]) focus = d;
            }
            return (TrainingTarget)k;
        }

        private bool TryChange(Action<SegmentPlan> change, out PlanError error)
        {
            var c = Copy(Plan, -1, -1);
            change(c);
            // An incomplete lineup only blocks Resolve, not plan editing (it is checked last, so nothing else is wrong).
            if (!Runner.TryValidate(c, BuildContext(), out error) && error.Code != PlanErrorCode.IncompleteLineup) return false;
            Plan = c;
            return true;
        }

        private static SegmentPlan Copy(SegmentPlan src, int skipAssignment, int skipOffer)
        {
            var c = new SegmentPlan();
            for (int i = 0; i < src.Assignments.Count; i++)
            {
                if (i == skipAssignment) continue;
                var a = src.Assignments[i];
                switch (a.Action)
                {
                    case SegmentAction.Train: c.Train(a.PlayerId, a.Target, a.FocusDimension); break;
                    case SegmentAction.Stream: c.Stream(a.PlayerId); break;
                    default: c.Rest(a.PlayerId); break;
                }
            }
            for (int i = 0; i < src.AcceptedOfferIds.Count; i++) if (i != skipOffer) c.AcceptOffer(src.AcceptedOfferIds[i]);
            c.SponsorPoints = src.SponsorPoints;
            foreach (var a in src.ChoiceAnswers) c.AnswerChoice(a.ChoiceId, a.Option);
            foreach (int id in src.AcceptedMediaIds) c.AcceptMedia(id);
            return c;
        }

        /// <summary>Main-lane dimension names of a rostered player (empty when unknown).</summary>
        public string[] DimensionNames(long playerId)
        {
            foreach (var p in Team.Roster) if (p.Id == playerId) return ToCard(p).DimensionNames;
            return new string[0];
        }

        /// <summary>Header numbers.</summary>
        public TeamData GetTeamData()
        {
            var st = Runner.GetState();
            return new TeamData
            {
                Name = Team.Name,
                PhaseStrength = PhaseStrengths(),
                Popularity = Team.ClubPopularity,
                Funds = Team.Funds,
                Chemistry = Team.Chemistry(),
                ActionPoints = st.ActionPoints,
                PointsRemaining = Runner.PointsRemaining(Plan),
                MaxPerPlayer = st.MaxPerPlayer,
                SegmentNumber = AwaitingNext ? _segmentsPlayed : _segmentsPlayed + 1,
                SegmentsPerYear = SegmentsPerYear,
            };
        }

        /// <summary>One card per rostered player, roster order. Never contains the true potential.</summary>
        public List<PlayerCardData> GetRosterData()
        {
            var list = new List<PlayerCardData>();
            foreach (var p in Team.Roster) list.Add(ToCard(p));
            return list;
        }

        /// <summary>Card for one player.</summary>
        public PlayerCardData ToCard(Player p)
        {
            var lane = _players.FindLane(p.MainLane);
            var est = p.GetPotentialEstimate();
            var radar = p.GetRadar(p.MainLane);
            var names = new string[lane.dimensions.Length];
            var values = new float[lane.dimensions.Length];
            for (int i = 0; i < names.Length; i++) { names[i] = lane.dimensions[i].displayName; values[i] = radar[i]; }
            var traits = new List<string>();
            foreach (var t in p.Traits) traits.Add(TraitName(t));
            var card = new PlayerCardData
            {
                Id = p.Id, Name = p.Name, Age = p.Age, LaneId = p.MainLane, LaneName = lane.displayName,
                TraitNames = traits.ToArray(), Mood = p.Mood, Stress = p.Stress, Looks = p.Looks,
                Popularity = p.Popularity, Salary = p.Salary, ContractYearsLeft = p.ContractYearsLeft,
                DimensionNames = names, DimensionValues = values, PotentialLow = est.Low, PotentialHigh = est.High,
                IsStarter = Team.LaneOf(p) != null,
                Rating = p.GetLaneScore(p.MainLane),
                HighStress = p.Stress > _segment.mood.highStressThreshold,
            };
            for (int k = 0; k < Player.FocusPhases; k++) card.Focus[k] = p.GetFocus(k);
            var owned = new List<string>();
            foreach (var id in p.Specialties) owned.Add(SpecialtyName(id));
            card.SpecialtyNames = owned.ToArray();
            if (Growth != null)
            {
                for (int k = 0; k < 3; k++) card.PhaseDimensions[k] = Growth.Map.PhaseDims(p.MainLane, k);
                card.CommandIndex = Growth.Map.CommandIndex(p.MainLane);
                card.NearCap = new bool[names.Length];
                for (int i = 0; i < names.Length; i++) card.NearCap[i] = Growth.IsNearCap(p, i);
                foreach (var sp in Growth.Progress(p))
                    card.SpecialtyProgress.Add(new SpecialtyProgressData
                    {
                        Id = sp.Def.id, Name = sp.Def.displayName, Phase = sp.Phase, DimensionIndex = sp.DimensionIndex,
                        DimensionName = names[sp.DimensionIndex], DimensionGap = sp.DimensionGap, FocusGap = sp.FocusGap, Owned = sp.Owned,
                    });
            }
            return card;
        }

        /// <summary>Chinese display name of a trait id (falls back to the id).</summary>
        public string TraitName(string traitId)
        {
            foreach (var g in _players.personalityGroups)
                foreach (var t in g.traits) if (t.id == traitId) return t.displayName;
            return traitId;
        }

        /// <summary>Display name of a player by id, or an empty string.</summary>
        public string PlayerName(long id)
        {
            foreach (var p in Team.Roster) if (p.Id == id) return p.Name;
            return "";
        }
    }
}
