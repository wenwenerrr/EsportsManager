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
        /// <summary>True when the player club (index 0) is side A.</summary>
        public bool PlayerInvolved => ClubA == League.PlayerClub;
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
        /// <summary>1-based year.</summary>
        public int Year;
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
        public bool PlayerInBracket => Seeds != null && Array.IndexOf(Seeds, League.PlayerClub) >= 0;
        /// <summary>The player club's first bracket opponent (seed 1 v last seed, 2 v second-to-last ...); -1 when not seeded.</summary>
        public int PlayerFirstOpponent
        {
            get
            {
                if (!PlayerInBracket) return -1;
                int i = Array.IndexOf(Seeds, League.PlayerClub);
                return Seeds[Seeds.Length - 1 - i];
            }
        }
        /// <summary>The player club's place (0 = did not take part / not complete).</summary>
        public int PlayerPlacing => Placing == null ? 0 : Placing[League.PlayerClub];
        /// <summary>The player club's prize.</summary>
        public long PlayerPrize => Prize == null ? 0 : Prize[League.PlayerClub];

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
                    ClubIndex = e.ClubIndex, ClubName = ClubNames[e.ClubIndex], IsPlayer = e.ClubIndex == League.PlayerClub,
                    SeriesWins = e.SeriesWins, SeriesLosses = e.SeriesLosses, GamesWon = e.GamesWon, GamesLost = e.GamesLost,
                    GameDiff = e.GameDiff, Rank = e.Rank,
                });
            return rows;
        }
    }

    /// <summary>
    /// Plays the calendar events (numbers.md 4.18 v3.4) on a DEDICATED random source. Pure C#.
    /// </summary>
    /// <remarks>
    /// FIXED DRAW ORDER on the runner's source: (a) <see cref="AdvanceForm"/>, called by the session once after EVERY segment
    /// resolve (one draw per opponent, club index order) - before the event of that segment starts; (b) regular season: rounds
    /// 0..6 in order, inside a round the pairings in <see cref="League.PairingsOfRound"/> order (the player club's pairing is
    /// always the first); (c) playoffs / finals: semi-final seed 1 v seed 4, semi-final seed 2 v seed 3, then the final. Each
    /// series draws as documented on <see cref="MatchSimulator"/>. Callers (the session's event effects) draw from the same source
    /// right after a stage's series; see <c>EventEffects</c>.
    /// TIE-BREAKS: regular season = <see cref="Standings"/> (series wins, game diff, games won, lower club index).
    /// Finals qualification: points, then regular-season series wins of all splits, then their game diff, then lower club index.
    /// </remarks>
    public sealed class TournamentRunner
    {
        private readonly MatchConfig _match;
        private readonly TeamConfig _team;
        private readonly PlayerConfig _players;

        /// <summary>Opponents, names, schedule, form.</summary>
        public League League { get; }
        /// <summary>The simulator.</summary>
        public MatchSimulator Simulator { get; }
        /// <summary>The match config.</summary>
        public MatchConfig Config => _match;
        /// <summary>The dedicated random source (also used by the session's event effects, see remarks).</summary>
        public IRandomSource Random { get; }

        /// <summary>Creates a runner; every dependency is injected.</summary>
        public TournamentRunner(League league, MatchConfig match, TeamConfig team, PlayerConfig players, IRandomSource rng)
        {
            League = league ?? throw new ArgumentNullException(nameof(league));
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            Random = rng ?? throw new ArgumentNullException(nameof(rng));
            Simulator = new MatchSimulator(match, team, players);
        }

        /// <summary>Opponent form random walk for one segment (numbers.md 4.14).</summary>
        public void AdvanceForm() => League.AdvanceForm(Random);

        /// <summary>
        /// Opens an event. A split gets an empty regular-season table. The finals compute the qualification table from
        /// <paramref name="earlierThisYear"/> (complete splits of the same year) and seed the top finals_teams.
        /// </summary>
        public EventResult CreateEvent(CalendarEventDef def, int year, IReadOnlyList<EventResult> earlierThisYear)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var names = new string[League.ClubCount];
            for (int i = 0; i < names.Length; i++) names[i] = League.ClubName(i);
            var ev = new EventResult { Def = def, Year = year, ClubNames = names };
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
        /// Plays the whole single round robin of a split (every club 7 series, 28 in total with 8 clubs) with
        /// <paramref name="player"/> for every player series, then seeds the playoffs from the table. Throws
        /// <see cref="InvalidOperationException"/> BEFORE any draw when the event is not a split awaiting its regular season or a
        /// lineup is incomplete.
        /// </summary>
        public void PlayRegularSeason(EventResult ev, MatchLineup player)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (ev.Type != EventType.Split || ev.RegularDone) throw new InvalidOperationException("This event has no regular season to play now.");
            RequireOpponents();
            MatchFormulas.RequireFullLineup(_players, player);
            int bo = _match.format.regularBestOf;
            for (int round = 0; round < League.RoundsPerCycle; round++)
                foreach (var p in League.PairingsOfRound(round))
                {
                    var s = Play(p.ClubA, p.ClubB, player, bo, EventStage.RegularSeason);
                    s.Round = round;
                    ev.Regular.Record(s.ClubA, s.ClubB, s.Result);
                    ev.RegularSeries.Add(s);
                }
            ev.RegularDone = true;
            var ranking = ev.Regular.Ranking();
            ev.Seeds = new int[_match.format.playoffTeams];
            for (int i = 0; i < ev.Seeds.Length; i++) ev.Seeds[i] = ranking[i].ClubIndex;
        }

        /// <summary>
        /// Plays the bracket of a split (after its regular season) or of the finals, then sets placings, points and prizes.
        /// <paramref name="player"/> may be null only when the player club is not in the bracket. Throws BEFORE any draw on a
        /// wrong state or an incomplete lineup.
        /// </summary>
        public void PlayBracket(EventResult ev, MatchLineup player)
        {
            if (ev == null) throw new ArgumentNullException(nameof(ev));
            if (!ev.RegularDone || ev.IsComplete || ev.Seeds == null) throw new InvalidOperationException("This event has no bracket to play now.");
            RequireOpponents();
            if (ev.PlayerInBracket)
            {
                if (player == null) throw new ArgumentNullException(nameof(player), "the player club is in the bracket.");
                MatchFormulas.RequireFullLineup(_players, player);
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
                var s1 = Play(seeds[0], seeds[3], player, semiBo, EventStage.Semifinal);
                var s2 = Play(seeds[1], seeds[2], player, semiBo, EventStage.Semifinal);
                ev.PlayoffSeries.Add(s1); ev.PlayoffSeries.Add(s2);
                finA = s1.Winner; finB = s2.Winner;
                // both losers share place 3; listed in seed order
                foreach (int c in seeds) if (c == s1.Loser || c == s2.Loser) semiLosers.Add(c);
            }
            else { finA = seeds[0]; finB = seeds[1]; }
            var fin = Play(finA, finB, player, finalBo, EventStage.Final);
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
                ev.Prize[c] = prizes[pos];
                if (split) ev.Points[c] = f.finalsPoints[pos];
            }
            ev.IsComplete = true;
        }

        // The player club always plays as side A (its report is from side A); otherwise the first club given is side A.
        private EventSeries Play(int a, int b, MatchLineup player, int bestOf, EventStage stage)
        {
            if (b == League.PlayerClub) { int t = a; a = b; b = t; }
            var la = a == League.PlayerClub ? player : MatchLineup.FromOpponent(League.OpponentAt(a), _match);
            var lb = MatchLineup.FromOpponent(League.OpponentAt(b), _match);
            var result = Simulator.Simulate(la, lb, Random, bestOf);
            return new EventSeries
            {
                Stage = stage, ClubA = a, ClubB = b, BestOf = bestOf, Result = result,
                Report = a == League.PlayerClub ? MatchReport.Build(result, _team, _match, true) : null,
            };
        }

        private void RequireOpponents()
        {
            foreach (var o in League.Opponents) MatchFormulas.RequireFullLineup(_players, MatchLineup.FromOpponent(o, _match));
        }
    }
}
