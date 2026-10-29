using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Matches
{
    /// <summary>One line of a standings table for display (no text: the UI formats it).</summary>
    public sealed class StandingsRow
    {
        /// <summary>Club index (0 = the player club).</summary>
        public int ClubIndex;
        /// <summary>Club name (config data, not localized UI text).</summary>
        public string ClubName;
        /// <summary>True for the player club (highlight it).</summary>
        public bool IsPlayer;
        /// <summary>Series won / lost.</summary>
        public int SeriesWins, SeriesLosses;
        /// <summary>Games won / lost over all series.</summary>
        public int GamesWon, GamesLost;
        /// <summary>GamesWon - GamesLost.</summary>
        public int GameDiff;
        /// <summary>1-based rank.</summary>
        public int Rank;
    }

    /// <summary>Stage of an event a series belongs to.</summary>
    public enum EventStage
    {
        /// <summary>Split regular season (round robin).</summary>
        RegularSeason = 0,
        /// <summary>Semi-final (split playoffs or finals).</summary>
        Semifinal = 1,
        /// <summary>Final (split playoffs or finals).</summary>
        Final = 2,
    }

    /// <summary>One series played in an event. When the player club plays, it is ALWAYS side A.</summary>
    public sealed class EventSeries
    {
        /// <summary>The player club when the series was played (<see cref="League.NoClub"/> during the warm-up).</summary>
        public int PlayerClub = League.NoClub;
        /// <summary>Schedule slot (regular: round x 100 + pairing index; bracket: semi 0 / 1, final 0) - part of the random key.</summary>
        public int Slot;
        /// <summary>Stage.</summary>
        public EventStage Stage;
        /// <summary>Regular-season round (0-based); -1 for playoff series.</summary>
        public int Round = -1;
        /// <summary>Club index of side A / side B.</summary>
        public int ClubA, ClubB;
        /// <summary>Series length.</summary>
        public int BestOf;
        /// <summary>The series (participants are snapshots of who actually played).</summary>
        public MatchResult Result;
        /// <summary>Report from the player club's point of view; null when the player club did not play.</summary>
        public MatchReport Report;
        /// <summary>True when the player club is side A (it always is when it plays).</summary>
        public bool PlayerInvolved => PlayerClub >= 0 && ClubA == PlayerClub;
        /// <summary>True when <paramref name="club"/> played this series.</summary>
        public bool Involves(int club) => ClubA == club || ClubB == club;
        /// <summary>The side snapshot of <paramref name="club"/> (null when it did not play).</summary>
        public MatchSideSnapshot SideOf(int club) => club == ClubA ? Result.SideA : club == ClubB ? Result.SideB : null;
        /// <summary>The other club of the series.</summary>
        public int OpponentOf(int club) => club == ClubA ? ClubB : ClubA;
        /// <summary>True when <paramref name="club"/> won.</summary>
        public bool Won(int club) => Winner == club;
        /// <summary>Club index of the winner / loser.</summary>
        public int Winner => Result.WinnerIsA ? ClubA : ClubB;
        /// <summary>Club index of the loser.</summary>
        public int Loser => Result.WinnerIsA ? ClubB : ClubA;
    }

    /// <summary>One line of the annual finals qualification table (numbers.md 4.18).</summary>
    public sealed class QualificationRow
    {
        /// <summary>Club index.</summary>
        public int ClubIndex;
        /// <summary>Club name.</summary>
        public string ClubName;
        /// <summary>True for the player club.</summary>
        public bool IsPlayer;
        /// <summary>Points per split of the year so far, in calendar order.</summary>
        public List<int> SplitPoints = new List<int>();
        /// <summary>Sum of <see cref="SplitPoints"/>.</summary>
        public int Points;
        /// <summary>Regular-season series wins summed over the splits (first tie-break).</summary>
        public int RegularSeriesWins;
        /// <summary>Regular-season game differential summed over the splits (second tie-break).</summary>
        public int RegularGameDiff;
        /// <summary>1-based rank.</summary>
        public int Rank;
        /// <summary>True when Rank &lt;= finals_teams.</summary>
        public bool Qualified;
        /// <summary>True once at least one split of the year is complete; before that every rank is a tie-break placeholder and nobody is "qualified".</summary>
        public bool HasPlayed => SplitPoints.Count > 0;
    }

    /// <summary>
    /// One event of a year (numbers.md 4.18): a split (regular season + playoffs) or the annual finals. Filled step by step by
    /// <see cref="TournamentRunner"/>; persisted in the year's history. Final positions: the "final order" lists the club indexes from
    /// champion down; the per-club place is 1, 2, 3, 3 (both semi-final losers), then 5.. by regular-season rank (splits only).
    /// </summary>
    public sealed class EventResult
    {
        /// <summary>The calendar entry.</summary>
        public CalendarEventDef Def;
        /// <summary>Index of the entry in the calendar (part of the series random key).</summary>
        public int EventIndex;
        /// <summary>Year (0 = warm-up).</summary>
        public int Year;
        /// <summary>The player club when the event was created (<see cref="League.NoClub"/> during the warm-up).</summary>
        public int PlayerClub = League.NoClub;
        /// <summary>Club names by index (snapshot).</summary>
        public string[] ClubNames;
        /// <summary>Regular-season standings (splits only; null for the finals).</summary>
        public Standings Regular;
        /// <summary>Regular-season series in play order.</summary>
        public List<EventSeries> RegularSeries = new List<EventSeries>();
        /// <summary>Playoff / finals series in play order (semis by seed, then the final).</summary>
        public List<EventSeries> PlayoffSeries = new List<EventSeries>();
        /// <summary>Club indexes in seed order (seed 1 first) of the playoff bracket / finals.</summary>
        public int[] Seeds;
        /// <summary>Finals only: the qualification table the seeds came from.</summary>
        public List<QualificationRow> Qualification;
        /// <summary>True after the regular season was played (always true for the finals).</summary>
        public bool RegularDone;
        /// <summary>True when the event is complete (placings and prizes known).</summary>
        public bool IsComplete;
        /// <summary>Club indexes from champion down (complete events only). Finals: only the qualified clubs.</summary>
        public int[] FinalOrder;
        /// <summary>Place per club index (1,2,3,3,5..); 0 = did not take part (finals non-qualifiers) or not complete.</summary>
        public int[] Placing;
        /// <summary>Prize per club index, yuan.</summary>
        public long[] Prize;
        /// <summary>Finals qualification points per club index (splits only).</summary>
        public int[] Points;

        /// <summary>Event kind.</summary>
        public EventType Type => Def.type;
        /// <summary>True when the player club is in the playoff bracket / finals.</summary>
        public bool PlayerInBracket => PlayerClub >= 0 && Seeds != null && Array.IndexOf(Seeds, PlayerClub) >= 0;
        /// <summary>True when <paramref name="club"/> is in the playoff bracket / finals.</summary>
        public bool InBracket(int club) => Seeds != null && Array.IndexOf(Seeds, club) >= 0;
        /// <summary>The first bracket opponent of a seeded club (seed 1 v last ...), or -1.</summary>
        public int FirstOpponent(int club)
        {
            if (!InBracket(club)) return -1;
            int i = Array.IndexOf(Seeds, club);
            return Seeds[Seeds.Length - 1 - i];
        }
        /// <summary>Every series <paramref name="club"/> played, play order (regular season first).</summary>
        public List<EventSeries> SeriesOf(int club)
        {
            var list = new List<EventSeries>();
            foreach (var s in RegularSeries) if (s.Involves(club)) list.Add(s);
            foreach (var s in PlayoffSeries) if (s.Involves(club)) list.Add(s);
            return list;
        }
        /// <summary>The player club's first bracket opponent (seed 1 v last seed, 2 v second-to-last ...); -1 when not seeded.</summary>
        public int PlayerFirstOpponent
        {
            get { return PlayerInBracket ? FirstOpponent(PlayerClub) : -1; }
        }
        /// <summary>The player club's place (0 = did not take part / not complete).</summary>
        public int PlayerPlacing => Placing == null || PlayerClub < 0 ? 0 : Placing[PlayerClub];
        /// <summary>The player club's prize.</summary>
        public long PlayerPrize => Prize == null || PlayerClub < 0 ? 0 : Prize[PlayerClub];

        /// <summary>Every series the player club played, in play order (regular season first).</summary>
        public List<EventSeries> PlayerSeries()
        {
            var list = new List<EventSeries>();
            foreach (var s in RegularSeries) if (s.PlayerInvolved) list.Add(s);
            foreach (var s in PlayoffSeries) if (s.PlayerInvolved) list.Add(s);
            return list;
        }

        /// <summary>Regular-season table as display rows, rank order (empty for the finals).</summary>
        public List<StandingsRow> StandingsRows()
        {
            var rows = new List<StandingsRow>();
            if (Regular == null) return rows;
            foreach (var e in Regular.Ranking())
                rows.Add(new StandingsRow
                {
                    ClubIndex = e.ClubIndex, ClubName = ClubNames[e.ClubIndex], IsPlayer = e.ClubIndex == PlayerClub,
                    SeriesWins = e.SeriesWins, SeriesLosses = e.SeriesLosses, GamesWon = e.GamesWon, GamesLost = e.GamesLost,
                    GameDiff = e.GameDiff, Rank = e.Rank,
                });
            return rows;
        }
    }

    /// <summary>
    /// Plays the calendar events (numbers.md 4.18 v3.4; league.md rev 3) for ALL clubs with the same rules. Pure C#.
    /// Lineups (starters, chemistry, tactic, hooks) come from the caller per club: the session for the player club, the league
    /// AI for the others. Every series draws on its own KEYED source (league.md 随机数): purpose "match", key (year, calendar
    /// event index, stage, schedule slot), so no series depends on how many draws other series made.
    /// TIE-BREAKS: regular season = <see cref="Standings"/> (series wins, game diff, games won, lower club index).
    /// Finals qualification: points, then regular-season series wins of all splits, then their game diff, then lower club index.
    /// </summary>
    public sealed class TournamentRunner
    {
        private readonly MatchConfig _match;
        private readonly Func<int, int, int, int, IRandomSource> _seriesRng;

        /// <summary>Club names, schedule and the player club id.</summary>
        public League League { get; }
        /// <summary>The simulator.</summary>
        public MatchSimulator Simulator { get; }
        /// <summary>The match config.</summary>
        public MatchConfig Config => _match;

        /// <summary>
        /// Builds a side's specialty hook table from its starters (Story 012: the growth system filters and de-duplicates). Null =
        /// no specialties (neutral hooks).
        /// </summary>
        public Func<IReadOnlyDictionary<string, Player>, IEnumerable<SpecialtyEffect>> HookBuilder { get; set; }

        /// <summary>Hook table of a team's current starters (empty without a <see cref="HookBuilder"/>).</summary>
        public IEnumerable<SpecialtyEffect> Hooks(EsportsManager.Teams.Team team)
            => HookBuilder == null || team == null ? null : HookBuilder(team.StartersByLane());

        /// <summary>A team's lineup with its hook table and the given tactic.</summary>
        public MatchLineup LineupOf(EsportsManager.Teams.Team team, MatchTactic tactic) => MatchLineup.FromTeam(team, tactic, Hooks(team));

        /// <summary>
        /// Creates a runner. <paramref name="seriesRng"/> returns the source of one series from (year, event index, stage, slot).
        /// </summary>
        public TournamentRunner(League league, MatchConfig match, TeamConfig team, PlayerConfig players, Func<int, int, int, int, IRandomSource> seriesRng)
        {
            League = league ?? throw new ArgumentNullException(nameof(league));
            _match = match ?? throw new ArgumentNullException(nameof(match));
            if (team == null) throw new ArgumentNullException(nameof(team));
            if (players == null) throw new ArgumentNullException(nameof(players));
            _seriesRng = seriesRng ?? throw new ArgumentNullException(nameof(seriesRng));
            Simulator = new MatchSimulator(match, team, players);
        }

        /// <summary>Club names snapshot.</summary>
        public string[] Names()
        {
            var names = new string[League.ClubCount];
            for (int i = 0; i < names.Length; i++) names[i] = League.ClubName(i);
            return names;
        }

        /// <summary>
        /// Opens an event. A split gets an empty regular-season table. The finals compute the qualification table from
        /// <paramref name="earlierThisYear"/> (complete splits of the same year) and seed the top finals_teams.
        /// </summary>
        public EventResult CreateEvent(CalendarEventDef def, int year, IReadOnlyList<EventResult> earlierThisYear)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var names = Names();
            var ev = new EventResult
            {
                Def = def, Year = year, ClubNames = names, PlayerClub = League.PlayerClub,
                EventIndex = Array.IndexOf(_match.calendar.events, def),
            };
            if (def.type == EventType.Split) ev.Regular = new Standings(League.ClubCount);
            else
            {
                ev.RegularDone = true;
                ev.Qualification = Qualification(earlierThisYear, names);
                ev.Seeds = new int[_match.format.finalsTeams];
                for (int i = 0; i < ev.Seeds.Length; i++) ev.Seeds[i] = ev.Qualification[i].ClubIndex;
            }
            return ev;
        }

        /// <summary>
        /// Finals qualification table (numbers.md 4.18) from the complete splits in <paramref name="splits"/> (others ignored).
        /// Rank order: points, regular-season series wins, regular-season game diff, lower club index.
        /// </summary>
        public List<QualificationRow> Qualification(IReadOnlyList<EventResult> splits, string[] names)
        {
            int n = League.ClubCount;
            var rows = new List<QualificationRow>();
            for (int c = 0; c < n; c++) rows.Add(new QualificationRow { ClubIndex = c, ClubName = names[c], IsPlayer = c == League.PlayerClub });
            if (splits != null)
                foreach (var s in splits)
                {
                    if (s == null || s.Type != EventType.Split || !s.IsComplete) continue;
                    for (int c = 0; c < n; c++)
                    {
                        var e = s.Regular.GetEntry(c);
                        rows[c].SplitPoints.Add(s.Points[c]);
                        rows[c].Points += s.Points[c];
                        rows[c].RegularSeriesWins += e.SeriesWins;
                        rows[c].RegularGameDiff += e.GameDiff;
                    }
                }
            rows.Sort((a, b) =>
            {
                int k = b.Points.CompareTo(a.Points);
                if (k != 0) return k;
                k = b.RegularSeriesWins.CompareTo(a.RegularSeriesWins);
                if (k != 0) return k;
                k = b.RegularGameDiff.CompareTo(a.RegularGameDiff);
                return k != 0 ? k : a.ClubIndex.CompareTo(b.ClubIndex);
            });
            for (int i = 0; i < rows.Count; i++) { rows[i].Rank = i + 1; rows[i].Qualified = i < _match.format.finalsTeams; }
            return rows;
        }

        /// <summary>
        /// Plays the whole single round robin of a split with <paramref name="lineupOf"/>(club) for every club (asked once per
        /// club, in club order, before any series), then seeds the playoffs. Throws BEFORE any draw when the event is not a split
        /// awaiting its regular season or a lineup is incomplete.
        /// </summary>
        public void PlayRegularSeason(EventResult ev, Func<int, MatchLineup> lineupOf)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            if (lineupOf == null) throw new ArgumentNullException(nameof(lineupOf));
            if (ev.Type != EventType.Split || ev.RegularDone) throw new InvalidOperationException("This event has no regular season to play now.");
            var lineups = new MatchLineup[League.ClubCount];
            for (int c = 0; c < lineups.Length; c++)
            {
                lineups[c] = lineupOf(c) ?? throw new InvalidOperationException("No lineup for club " + c + ".");
                MatchFormulas.RequireFullLineup(Simulator.Layout, lineups[c]);
            }
            int bo = _match.format.regularBestOf;
            for (int round = 0; round < League.RoundsPerCycle; round++)
            {
                var pairs = League.PairingsOfRound(round);
                for (int i = 0; i < pairs.Count; i++)
                {
                    var s = Play(ev, pairs[i].ClubA, pairs[i].ClubB, lineups, bo, EventStage.RegularSeason, round * 100 + i);
                    s.Round = round;
                    ev.Regular.Record(s.ClubA, s.ClubB, s.Result);
                    ev.RegularSeries.Add(s);
                }
            }
            ev.RegularDone = true;
            var ranking = ev.Regular.Ranking();
            ev.Seeds = new int[_match.format.playoffTeams];
            for (int i = 0; i < ev.Seeds.Length; i++) ev.Seeds[i] = ranking[i].ClubIndex;
        }

        /// <summary>
        /// Plays the bracket of a split (after its regular season) or of the finals with <paramref name="lineupOf"/>(club) for every
        /// seeded club (asked once per club in seed order; the final reuses the semi-final lineup and tactic, match.md 战术节奏),
        /// then sets placings, points and prizes. Throws BEFORE any draw on a wrong state or an incomplete lineup.
        /// </summary>
        public void PlayBracket(EventResult ev, Func<int, MatchLineup> lineupOf)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            if (lineupOf == null) throw new ArgumentNullException(nameof(lineupOf));
            if (!ev.RegularDone || ev.IsComplete || ev.Seeds == null) throw new InvalidOperationException("This event has no bracket to play now.");
            var lineups = new MatchLineup[League.ClubCount];
            foreach (int c in ev.Seeds)
            {
                lineups[c] = lineupOf(c) ?? throw new InvalidOperationException("No lineup for club " + c + ".");
                MatchFormulas.RequireFullLineup(Simulator.Layout, lineups[c]);
            }
            var f = _match.format;
            bool split = ev.Type == EventType.Split;
            int semiBo = split ? f.playoffBestOf : f.finalsSemiBestOf;
            int finalBo = split ? f.splitFinalBestOf : f.finalsFinalBestOf;
            var seeds = ev.Seeds;
            var order = new List<int>();
            int finA, finB;
            var semiLosers = new List<int>();
            if (seeds.Length == 4)
            {
                var s1 = Play(ev, seeds[0], seeds[3], lineups, semiBo, EventStage.Semifinal, 0);
                var s2 = Play(ev, seeds[1], seeds[2], lineups, semiBo, EventStage.Semifinal, 1);
                ev.PlayoffSeries.Add(s1); ev.PlayoffSeries.Add(s2);
                finA = s1.Winner; finB = s2.Winner;
                foreach (int c in seeds) if (c == s1.Loser || c == s2.Loser) semiLosers.Add(c);
            }
            else { finA = seeds[0]; finB = seeds[1]; }
            var fin = Play(ev, finA, finB, lineups, finalBo, EventStage.Final, 0);
            ev.PlayoffSeries.Add(fin);
            order.Add(fin.Winner); order.Add(fin.Loser); order.AddRange(semiLosers);
            if (split)
                foreach (var e in ev.Regular.Ranking())
                    if (!order.Contains(e.ClubIndex)) order.Add(e.ClubIndex);

            int n = League.ClubCount;
            ev.FinalOrder = order.ToArray();
            ev.Placing = new int[n];
            ev.Prize = new long[n];
            if (split) ev.Points = new int[n];
            long[] prizes = split ? f.splitPrizes : f.finalsPrizes;
            for (int pos = 0; pos < order.Count; pos++)
            {
                int c = order[pos];
                ev.Placing[c] = seeds.Length == 4 && pos == 3 ? 3 : pos + 1;
                ev.Prize[c] = pos < prizes.Length ? prizes[pos] : 0;
                if (split) ev.Points[c] = f.finalsPoints[pos];
            }
            ev.IsComplete = true;
        }

        // The player club always plays as side A (its report is from side A); otherwise the first club given is side A.
        private EventSeries Play(EventResult ev, int a, int b, MatchLineup[] lineups, int bestOf, EventStage stage, int slot)
        {
            int player = ev.PlayerClub;
            if (player >= 0 && b == player) { int t = a; a = b; b = t; }
            var rng = _seriesRng(ev.Year, ev.EventIndex, (int)stage, slot);
            bool mine = player >= 0 && a == player;
            var result = Simulator.Simulate(lineups[a], lineups[b], rng, bestOf, mine);
            return new EventSeries
            {
                Stage = stage, ClubA = a, ClubB = b, BestOf = bestOf, Result = result, PlayerClub = player, Slot = slot,
                Report = mine ? MatchReport.Build(result, _match, true) : null,
            };
        }
    }
}
