using EsportsManager.Events;
using System;
using System.Collections.Generic;
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
    }

    /// <summary>Header numbers of the main screen.</summary>
    public sealed class TeamData
    {
        /// <summary>Team name.</summary>
        public string Name;
        /// <summary>Team strength (the triangle's first corner).</summary>
        public float Strength;
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
        /// <summary>Team strength before / after.</summary>
        public float StrengthBefore, StrengthAfter;
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

        /// <summary>The team.</summary>
        public Team Team { get; }
        /// <summary>The runner (state, validation, preview, resolve).</summary>
        public SegmentRunner Runner { get; }
        /// <summary>The draft plan the UI edits.</summary>
        public SegmentPlan Plan { get; private set; } = new SegmentPlan();
        /// <summary>The seed this session was built with.</summary>
        public int Seed { get; }
        /// <summary>Players of the generated pool who are NOT on the roster (free agents), for later stories.</summary>
        public IReadOnlyList<Player> FreeAgents { get; }

        /// <summary>Builds a session from the three config JSON texts WITHOUT a league (no matches; Story 003/009 behaviour).</summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson,
            int seed = DefaultSeed, long startingFunds = DefaultFunds)
        {
            return new GameSession(PlayerConfigLoader.FromJson(playerJson), TeamConfigLoader.FromJson(teamJson),
                SegmentConfigLoader.FromJson(segmentJson), seed, startingFunds);
        }

        /// <summary>Builds a session WITH the league (Story 004) from the four config JSON texts.</summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson, string matchJson,
            int seed = DefaultSeed, long startingFunds = DefaultFunds)
        {
            return new GameSession(PlayerConfigLoader.FromJson(playerJson), TeamConfigLoader.FromJson(teamJson),
                SegmentConfigLoader.FromJson(segmentJson), seed, startingFunds,
                matchJson == null ? null : MatchConfigLoader.FromJson(matchJson));
        }

        /// <summary>
        /// Builds a session with the league AND random events (Story 006). <paramref name="eventJson"/> null = no events.
        /// </summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson, string matchJson, string eventJson,
            int seed = DefaultSeed, long startingFunds = DefaultFunds)
        {
            var players = PlayerConfigLoader.FromJson(playerJson);
            return new GameSession(players, TeamConfigLoader.FromJson(teamJson), SegmentConfigLoader.FromJson(segmentJson), seed, startingFunds,
                matchJson == null ? null : MatchConfigLoader.FromJson(matchJson),
                eventJson == null ? null : EventConfigLoader.FromJson(eventJson, players));
        }

        /// <summary>Event text table for the UI (set by the bootstrap; null in tests that do not need texts).</summary>
        public EventTextTable EventTexts { get; set; }

        /// <summary>The random event engine (Story 006), or null when the session has no event config.</summary>
        public EventEngine Events { get; }

        /// <summary>The tournament runner (league, opponents, simulator), or null for a session without matches.</summary>
        public TournamentRunner Tournament { get; }
        /// <summary>Calendar + event state machine (numbers.md 4.18), or null for a session without matches.</summary>
        public SeasonFlow Season { get; }
        /// <summary>The match config, or null.</summary>
        public MatchConfig MatchConfig => Tournament?.Config;

        /// <summary>
        /// Generates the pool, takes the best main-lane player of every lane as starter plus the best remaining player
        /// as bench, and opens the first segment. With <paramref name="match"/> it also builds the league (Story 004) from the
        /// SAME generator (ids never collide) with seed + 3, and plays events on a dedicated source seeded seed + 4, so the
        /// team (seed + 1) and runner (seed + 2) streams draw exactly as before.
        /// </summary>
        public GameSession(PlayerConfig players, TeamConfig team, SegmentConfig segment, int seed = DefaultSeed,
            long startingFunds = DefaultFunds, MatchConfig match = null, EventConfig events = null)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _segment = segment ?? throw new ArgumentNullException(nameof(segment));
            Seed = seed;
            var generator = new PlayerGenerator(players, new SeededRandomSource(seed));
            var pool = generator.GeneratePool();
            Team = new Team(team, players, new SeededRandomSource(seed + 1), startingFunds, match != null ? match.club.defaultName : "Demo");
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
            var free = new List<Player>();
            foreach (var p in pool) if (!used.Contains(p.Id)) free.Add(p);
            FreeAgents = free;
            if (match != null)
            {
                var league = LeagueFactory.Build(match, team, players, generator, seed + 3, Team.Name);
                Tournament = new TournamentRunner(league, match, team, players, new SeededRandomSource(seed + 4));
                Season = new SeasonFlow(match, segment, players, Tournament, Team);
            }
            // Story 006: events draw on their own source (seed + 5) so the other streams are unchanged.
            if (events != null) Events = new EventEngine(events, players, new SeededRandomSource(seed + 5));
            Runner = new SegmentRunner(segment, players, Team, new SeededRandomSource(seed + 2), Events);
            Runner.BeginSegment();
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
            => FromJsonWithOffer(playerJson, teamJson, segmentJson, null, seed, maxTries);

        /// <summary>Same as the overload above, with the league when <paramref name="matchJson"/> is not null.</summary>
        public static GameSession FromJsonWithOffer(string playerJson, string teamJson, string segmentJson, string matchJson, int seed, int maxTries = 500)
            => FromJsonWithOffer(playerJson, teamJson, segmentJson, matchJson, null, seed, maxTries);

        /// <summary>Same, with random events when <paramref name="eventJson"/> is not null.</summary>
        public static GameSession FromJsonWithOffer(string playerJson, string teamJson, string segmentJson, string matchJson, string eventJson,
            int seed, int maxTries = 500)
        {
            GameSession s = null;
            for (int i = 0; i < maxTries; i++)
            {
                s = FromJson(playerJson, teamJson, segmentJson, matchJson, eventJson, seed + i * 7);
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
                ChemistryBefore = Team.Chemistry(), StrengthBefore = Team.Strength(),
            };
            r.Summary = Runner.Resolve(Plan, BuildContext());
            r.FundsAfter = Team.Funds; r.PopularityAfter = Team.ClubPopularity;
            r.ChemistryAfter = Team.Chemistry(); r.StrengthAfter = Team.Strength();
            _segmentsPlayed++;
            Season?.OnSegmentResolved();
            Plan = new SegmentPlan();
            _declined.Clear();
            AwaitingNext = true;
            LastResult = r;
            return r;
        }

        /// <summary>Opens the next segment (rolls its business offers). Throws while an event is not Done and acknowledged.</summary>
        public void BeginNext()
        {
            if (!AwaitingNext) throw new InvalidOperationException("Resolve the open segment first.");
            if (Season != null && Season.Phase != EventPhase.None) throw new InvalidOperationException("Finish and acknowledge the event first.");
            AwaitingNext = false;
            Runner.BeginSegment();
        }

        /// <summary>BeginNext without throwing: false + EventPending / SegmentNotOpen.</summary>
        public bool TryBeginNext(out PlanError error)
        {
            error = default;
            if (Season != null && Season.Phase != EventPhase.None) { error = new PlanError { Code = PlanErrorCode.EventPending }; return false; }
            if (!AwaitingNext) { error = new PlanError { Code = PlanErrorCode.SegmentNotOpen }; return false; }
            BeginNext();
            return true;
        }

        /// <summary>Resolve, play a pending event with the current lineup (<see cref="SeasonFlow.AutoPlay"/>), then BeginNext (scripted auto-play).</summary>
        public SegmentResult ResolveAndAdvance()
        {
            var r = Resolve();
            if (Season != null && Season.Phase != EventPhase.None) Season.AutoPlay();
            BeginNext();
            return r;
        }

        // ------------------------------------------------------------ plan editing (every change is validated)

        /// <summary>Adds one training point on a main-lane dimension; refused (false + error) when the plan would be invalid.</summary>
        public bool TryAddTrain(long playerId, int dimensionIndex, out PlanError error) => TryChange(p => p.Train(playerId, dimensionIndex), out error);
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
            var ev = Tournament.Simulator.Preview(MatchLineup.FromTeam(Team, CurrentTactic));
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

        /// <summary>The club name rules (null without a league).</summary>
        public ClubConfig ClubRules => Tournament?.Config.club;

        /// <summary>
        /// Names the player club (new game): trimmed, cut to club.name_max_length, empty -> club.default_name. Updates the team and the
        /// league table. Returns the name actually used. Without a league the raw text is only trimmed.
        /// </summary>
        public string SetClubName(string raw)
        {
            string name = ClubRules != null ? ClubRules.NormalizeName(raw) : (raw ?? "").Trim();
            Team.Rename(name);
            Tournament?.League.RenamePlayerClub(name);
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
        /// popular player, then round-robin: rest when stress >= 60, otherwise train the lowest main-lane dimension,
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
                    bool ok = p.Stress >= 60f ? TryAddRest(p.Id, out _) : TryAddTrain(p.Id, LowestDimension(p), out _);
                    added |= ok;
                }
            }
        }

        private static int LowestDimension(Player p)
        {
            var radar = p.GetRadar(p.MainLane);
            int best = 0;
            for (int i = 1; i < radar.Length; i++) if (radar[i] < radar[best]) best = i;
            return best;
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
                    case SegmentAction.Train: c.Train(a.PlayerId, a.DimensionIndex); break;
                    case SegmentAction.Stream: c.Stream(a.PlayerId); break;
                    default: c.Rest(a.PlayerId); break;
                }
            }
            for (int i = 0; i < src.AcceptedOfferIds.Count; i++) if (i != skipOffer) c.AcceptOffer(src.AcceptedOfferIds[i]);
            c.SponsorPoints = src.SponsorPoints;
            foreach (var a in src.ChoiceAnswers) c.AnswerChoice(a.ChoiceId, a.Option);
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
                Strength = Team.Strength(),
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
            return new PlayerCardData
            {
                Id = p.Id, Name = p.Name, Age = p.Age, LaneId = p.MainLane, LaneName = lane.displayName,
                TraitNames = traits.ToArray(), Mood = p.Mood, Stress = p.Stress, Looks = p.Looks,
                Popularity = p.Popularity, Salary = p.Salary, ContractYearsLeft = p.ContractYearsLeft,
                DimensionNames = names, DimensionValues = values, PotentialLow = est.Low, PotentialHigh = est.High,
                IsStarter = Team.LaneOf(p) != null,
                Rating = p.GetLaneScore(p.MainLane),
                HighStress = p.Stress > _segment.mood.highStressThreshold,
            };
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
