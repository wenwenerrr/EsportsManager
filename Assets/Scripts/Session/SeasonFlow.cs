using System;
using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;

namespace EsportsManager.Session
{
    /// <summary>
    /// Event phase of the season (numbers.md 4.18 v3.4). Transition table (anything else returns WrongEventPhase and changes nothing):
    /// <list type="bullet">
    /// <item>None --(segment N resolved, N hosts an event)--> Prepare</item>
    /// <item>Prepare (split) --PlayRegularSeason--> PlayoffPrep</item>
    /// <item>PlayoffPrep --PlayPlayoffs--> Done</item>
    /// <item>Prepare (finals) --PlayFinals--> Done</item>
    /// <item>Done --Acknowledge--> None (after the last segment of the year the year also rolls over)</item>
    /// </list>
    /// Lineup and tactic can change in None (while a segment is open), Prepare and PlayoffPrep; never in Done.
    /// </summary>
    public enum EventPhase
    {
        /// <summary>No event running: segments are planned and resolved.</summary>
        None = 0,
        /// <summary>Before the event: pick lineup + tactic (used for the whole regular season / the finals bracket).</summary>
        Prepare = 1,
        /// <summary>Split regular season played; the one adjustment window before the playoffs.</summary>
        PlayoffPrep = 2,
        /// <summary>Event complete; results shown until acknowledged.</summary>
        Done = 3,
    }

    /// <summary>One event with everything it did to the player club.</summary>
    public sealed class EventRecord
    {
        /// <summary>The tournament data (series, tables, placings, prizes).</summary>
        public EventResult Event;
        /// <summary>Stress / familiarity effects of the regular season (splits only).</summary>
        public List<EventPlayerEffect> RegularSeasonEffects = new List<EventPlayerEffect>();
        /// <summary>Stress / familiarity effects of the playoffs / finals bracket.</summary>
        public List<EventPlayerEffect> BracketEffects = new List<EventPlayerEffect>();
        /// <summary>Mood effects at event end.</summary>
        public List<EventPlayerEffect> MoodEffects = new List<EventPlayerEffect>();
        /// <summary>Prize income paid to the team at event end, yuan.</summary>
        public long PrizeIncome;
        /// <summary>Funds before / after the prize was paid.</summary>
        public long FundsBefore, FundsAfter;
        /// <summary>Team recent win rate (0-100) after the event.</summary>
        public float RecentWinRateAfter;
    }

    /// <summary>Events of one year, for the year-end summary (Story 007) and the UI.</summary>
    public sealed class YearEvents
    {
        /// <summary>1-based year.</summary>
        public int Year;
        /// <summary>Events in calendar order (the running one included).</summary>
        public List<EventRecord> Events = new List<EventRecord>();
        /// <summary>Prize income of the year so far, yuan.</summary>
        public long TotalPrize { get { long t = 0; foreach (var e in Events) t += e.PrizeIncome; return t; } }
    }

    /// <summary>The next event on the calendar.</summary>
    public readonly struct UpcomingEvent
    {
        /// <summary>The calendar entry.</summary>
        public readonly CalendarEventDef Def;
        /// <summary>Segments still to resolve before it starts, counting the open one (0 = running now).</summary>
        public readonly int SegmentsUntil;
        /// <summary>Creates the info.</summary>
        public UpcomingEvent(CalendarEventDef def, int segmentsUntil) { Def = def; SegmentsUntil = segmentsUntil; }
    }

    /// <summary>
    /// The calendar and event state machine (numbers.md 4.18 v3.4; transitions on <see cref="EventPhase"/>). Plain C#; owned by
    /// <see cref="GameSession"/>. Every step returns a <see cref="PlanError"/> (None = done) and changes nothing on error.
    /// </summary>
    public sealed class SeasonFlow
    {
        private readonly MatchConfig _match;
        private readonly SegmentConfig _segment;
        private readonly PlayerConfig _players;
        private readonly Team _team;
        private readonly List<YearEvents> _history = new List<YearEvents>();

        /// <summary>The tournament runner.</summary>
        public TournamentRunner Tournament { get; }
        /// <summary>1-based year.</summary>
        public int Year { get; private set; } = 1;
        /// <summary>Segments resolved in this year.</summary>
        public int SegmentsDoneInYear { get; private set; }
        /// <summary>Segments per year (calendar config).</summary>
        public int SegmentsPerYear => _match.calendar.segmentsPerYear;
        /// <summary>0-based index of the open (next) segment inside the year.</summary>
        public int SegmentIndexInYear => SegmentsDoneInYear % SegmentsPerYear;
        /// <summary>Current phase.</summary>
        public EventPhase Phase { get; private set; } = EventPhase.None;
        /// <summary>The running (or just finished, until acknowledged) event; null in None.</summary>
        public EventRecord Current { get; private set; }
        /// <summary>The last completed event, or null.</summary>
        public EventRecord LastCompleted { get; private set; }
        /// <summary>Tactic used for the next event stage (default Invade).</summary>
        public MatchTactic Tactic { get; private set; } = MatchTactic.Invade;
        /// <summary>All years so far (the current one last).</summary>
        public IReadOnlyList<YearEvents> History => _history;
        /// <summary>The current year's events.</summary>
        public YearEvents ThisYear => _history[_history.Count - 1];

        /// <summary>Creates the flow at year 1, segment 1, phase None.</summary>
        public SeasonFlow(MatchConfig match, SegmentConfig segment, PlayerConfig players, TournamentRunner tournament, Team team)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _segment = segment ?? throw new ArgumentNullException(nameof(segment));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            Tournament = tournament ?? throw new ArgumentNullException(nameof(tournament));
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _history.Add(new YearEvents { Year = 1 });
        }

        /// <summary>
        /// Called after every segment resolve: when the calendar has an event
        /// after this segment, open it (phase Prepare); otherwise roll the year over after its last segment.
        /// </summary>
        public void OnSegmentResolved()
        {
            if (Phase != EventPhase.None) throw new InvalidOperationException("An event is in progress.");
            SegmentsDoneInYear++;
            var def = _match.calendar.EventAfter(SegmentsDoneInYear);
            if (def != null)
            {
                var splits = new List<EventResult>();
                foreach (var r in ThisYear.Events) splits.Add(r.Event);
                Current = new EventRecord { Event = Tournament.CreateEvent(def, Year, splits) };
                ThisYear.Events.Add(Current);
                Phase = EventPhase.Prepare;
            }
            else if (SegmentsDoneInYear >= SegmentsPerYear) RollYear();
        }

        /// <summary>None when lineup / tactic may change now (<paramref name="segmentOpen"/>: a segment is open for planning).</summary>
        public PlanError CanEditLineup(bool segmentOpen)
        {
            if (Phase == EventPhase.Prepare || Phase == EventPhase.PlayoffPrep) return default;
            if (Phase == EventPhase.None && segmentOpen) return default;
            return new PlanError { Code = Phase == EventPhase.None ? PlanErrorCode.SegmentNotOpen : PlanErrorCode.LineupLocked };
        }

        /// <summary>Sets the tactic (same lock rules as the lineup).</summary>
        public PlanError SetTactic(MatchTactic tactic, bool segmentOpen)
        {
            if (!Enum.IsDefined(typeof(MatchTactic), tactic)) return new PlanError { Code = PlanErrorCode.UnknownTactic };
            var e = CanEditLineup(segmentOpen);
            if (e.IsNone) Tactic = tactic;
            return e;
        }

        /// <summary>Prepare (split) -> PlayoffPrep: plays the whole regular season with the current lineup and tactic.</summary>
        public PlanError PlayRegularSeason()
        {
            if (Phase != EventPhase.Prepare || Current.Event.Type != EventType.Split) return Err(PlanErrorCode.WrongEventPhase);
            var le = LineupError();
            if (!le.IsNone) return le;
            Tournament.PlayRegularSeason(Current.Event, MatchLineup.FromTeam(_team, Tactic));
            Current.RegularSeasonEffects = EventEffects.ApplyStage(_segment, _team, Current.Event.RegularSeries, Tournament.Random);
            Phase = EventPhase.PlayoffPrep;
            return default;
        }

        /// <summary>PlayoffPrep -> Done: plays the split playoffs (the player club only plays when seeded).</summary>
        public PlanError PlayPlayoffs()
        {
            if (Phase != EventPhase.PlayoffPrep) return Err(PlanErrorCode.WrongEventPhase);
            return PlayBracket();
        }

        /// <summary>Prepare (finals) -> Done: plays the finals bracket (runs for the AI too when the player did not qualify).</summary>
        public PlanError PlayFinals()
        {
            if (Phase != EventPhase.Prepare || Current.Event.Type != EventType.Finals) return Err(PlanErrorCode.WrongEventPhase);
            return PlayBracket();
        }

        /// <summary>Plays whichever stage is next (regular season, playoffs or finals).</summary>
        public PlanError PlayNextStage()
        {
            if (Phase == EventPhase.Prepare) return Current.Event.Type == EventType.Split ? PlayRegularSeason() : PlayFinals();
            if (Phase == EventPhase.PlayoffPrep) return PlayPlayoffs();
            return Err(PlanErrorCode.WrongEventPhase);
        }

        /// <summary>Done -> None (rolls the year over after its last segment).</summary>
        public PlanError Acknowledge()
        {
            if (Phase != EventPhase.Done) return Err(PlanErrorCode.WrongEventPhase);
            Phase = EventPhase.None;
            Current = null;
            if (SegmentsDoneInYear >= SegmentsPerYear) RollYear();
            return default;
        }

        /// <summary>Scripted play: every remaining stage with the current lineup, then acknowledge. Throws when a stage is refused.</summary>
        public EventRecord AutoPlay()
        {
            var rec = Current;
            while (Phase == EventPhase.Prepare || Phase == EventPhase.PlayoffPrep)
            {
                var e = PlayNextStage();
                if (!e.IsNone) throw new InvalidOperationException("Event stage refused: " + e.Code + ".");
            }
            if (Phase == EventPhase.Done) Acknowledge();
            return rec;
        }

        /// <summary>The next event (the running one with SegmentsUntil 0), or null when the calendar has none left this year.</summary>
        public UpcomingEvent? NextEvent()
        {
            if (Current != null) return new UpcomingEvent(Current.Event.Def, 0);
            var def = _match.calendar.NextEventFrom(SegmentsDoneInYear + 1);
            return def == null ? (UpcomingEvent?)null : new UpcomingEvent(def, def.afterSegment - SegmentsDoneInYear);
        }

        /// <summary>
        /// Regular-season table to show: the running split's, else the latest split's of all years, else an empty table of
        /// all clubs (rank by club index).
        /// </summary>
        public List<StandingsRow> CurrentStandings()
        {
            if (Current != null && Current.Event.Type == EventType.Split) return Current.Event.StandingsRows();
            for (int y = _history.Count - 1; y >= 0; y--)
                for (int i = _history[y].Events.Count - 1; i >= 0; i--)
                    if (_history[y].Events[i].Event.Type == EventType.Split) return _history[y].Events[i].Event.StandingsRows();
            var empty = new EventResult { ClubNames = Names(), Regular = new Standings(Tournament.League.ClubCount) };
            return empty.StandingsRows();
        }

        /// <summary>Finals qualification table of the current year (complete splits so far).</summary>
        public List<QualificationRow> Qualification()
        {
            var splits = new List<EventResult>();
            foreach (var r in ThisYear.Events) splits.Add(r.Event);
            return Tournament.Qualification(splits, Names());
        }

        /// <summary>IncompleteLineup when the player club needs to play the next stage and a lane is empty, else None.</summary>
        public PlanError LineupError()
        {
            bool plays = Phase == EventPhase.Prepare
                ? (Current.Event.Type == EventType.Split || Current.Event.PlayerInBracket)
                : Phase == EventPhase.PlayoffPrep && Current.Event.PlayerInBracket;
            int n = _team.Starters().Count;
            if (!plays || n == _players.lanes.Length) return default;
            return new PlanError { Code = PlanErrorCode.IncompleteLineup, Used = n, Limit = _players.lanes.Length };
        }

        private PlanError PlayBracket()
        {
            var le = LineupError();
            if (!le.IsNone) return le;
            var ev = Current.Event;
            Tournament.PlayBracket(ev, ev.PlayerInBracket ? MatchLineup.FromTeam(_team, Tactic) : null);
            Current.BracketEffects = EventEffects.ApplyStage(_segment, _team, ev.PlayoffSeries, Tournament.Random);
            Current.MoodEffects = EventEffects.ApplyEventEnd(_segment, _team, ev.PlayerSeries(), Tournament.Random);
            Current.FundsBefore = _team.Funds;
            Current.PrizeIncome = ev.PlayerPrize;
            _team.ReceivePrize(ev.PlayerPrize);
            Current.FundsAfter = _team.Funds;
            Current.RecentWinRateAfter = _team.RecentWinRate();
            LastCompleted = Current;
            Phase = EventPhase.Done;
            return default;
        }

        private void RollYear()
        {
            Year++;
            SegmentsDoneInYear = 0;
            _history.Add(new YearEvents { Year = Year });
        }

        private string[] Names()
        {
            var names = new string[Tournament.League.ClubCount];
            for (int i = 0; i < names.Length; i++) names[i] = Tournament.League.ClubName(i);
            return names;
        }

        private static PlanError Err(PlanErrorCode c) => new PlanError { Code = c };
    }
}
