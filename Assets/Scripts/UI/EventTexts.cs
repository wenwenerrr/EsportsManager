using System;
using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Segments;
using EsportsManager.Session;
using EventType = EsportsManager.Matches.EventType;

namespace EsportsManager.UI
{
    /// <summary>Per-player totals of one event for the result screen (actual deltas recorded by the logic layer).</summary>
    public sealed class PlayerEventTotals
    {
        /// <summary>Player.Id.</summary>
        public long PlayerId;
        /// <summary>Series played / won / lost over the whole event.</summary>
        public int Played, Won, Lost;
        /// <summary>Stress change summed over the event's stages.</summary>
        public float StressDelta;
        /// <summary>Mood change at event end.</summary>
        public float MoodDelta;
    }

    /// <summary>
    /// Pure text / lookup helpers of the calendar and event screens (Story 004 stage C2). No UnityEngine types: every
    /// judgement comes from the logic layer; these only choose and format <see cref="UiStrings"/>.
    /// </summary>
    public static class EventTexts
    {
        /// <summary>"距离 春季赛 还有 2 个赛段" / "本赛段结算后开始 春季赛" / running / none left.</summary>
        public static string Countdown(UpcomingEvent? next)
        {
            if (next == null) return UiStrings.NoEventLeft;
            string name = UiStrings.EventName(next.Value.Def.nameKey);
            int n = next.Value.SegmentsUntil;
            if (n <= 0) return UiStrings.F(UiStrings.EventRunningFormat, name);
            if (n == 1) return UiStrings.F(UiStrings.NextEventAfterThisFormat, name);
            return UiStrings.F(UiStrings.NextEventFormat, name, n);
        }

        /// <summary>"第 1 年 · 第 2/12 赛段" (segment 1-based within the year).</summary>
        public static string YearSegment(int year, int segmentOfYear, int segmentsPerYear)
            => UiStrings.F(UiStrings.YearSegmentFormat, year, segmentOfYear, segmentsPerYear);

        /// <summary>Format line for the prep screen of an event in a phase (config numbers only).</summary>
        /// <param name="type">Split or finals.</param>
        /// <param name="phase">Prepare (regular season / finals) or PlayoffPrep.</param>
        /// <param name="regularSeries">Series per club in the regular season (clubs - 1).</param>
        /// <param name="f">The format config.</param>
        public static string FormatSummary(EventType type, EventPhase phase, int regularSeries, FormatConfig f)
        {
            if (type == EventType.Finals)
                return f.finalsTeams == 4
                    ? UiStrings.F(UiStrings.FormatFinalsFormat, f.finalsTeams, f.finalsSemiBestOf, f.finalsFinalBestOf)
                    : UiStrings.F(UiStrings.FormatFinalsNoSemiFormat, f.finalsTeams, f.finalsFinalBestOf);
            if (phase == EventPhase.PlayoffPrep)
                return f.playoffTeams == 4
                    ? UiStrings.F(UiStrings.FormatPlayoffFormat, f.playoffBestOf, f.splitFinalBestOf)
                    : UiStrings.F(UiStrings.FormatPlayoffNoSemiFormat, f.splitFinalBestOf);
            return f.playoffTeams == 4
                ? UiStrings.F(UiStrings.FormatSplitFormat, regularSeries, f.regularBestOf, f.playoffTeams, f.playoffBestOf, f.splitFinalBestOf)
                : UiStrings.F(UiStrings.FormatSplitNoSemiFormat, regularSeries, f.regularBestOf, f.playoffTeams, f.splitFinalBestOf);
        }

        /// <summary>The short note under the format line.</summary>
        public static string PrepNote(EventType type, EventPhase phase)
            => type == EventType.Finals ? UiStrings.PrepNoteFinals
                : phase == EventPhase.PlayoffPrep ? UiStrings.PrepNotePlayoff : UiStrings.PrepNoteRegular;

        /// <summary>Chinese name of a stage.</summary>
        public static string StageName(EventStage stage)
            => stage == EventStage.Final ? UiStrings.StageFinal : stage == EventStage.Semifinal ? UiStrings.StageSemifinal : UiStrings.StageRegular;

        /// <summary>The player club's result line of a complete event ("夺得冠军！" / "最终第 3 名" / not in the finals).</summary>
        /// <param name="placing">Player placing (0 = did not take part).</param>
        /// <param name="prizeText">Formatted prize.</param>
        public static string PlacingText(int placing, string prizeText)
        {
            if (placing <= 0) return UiStrings.NotInFinals;
            return placing == 1 ? UiStrings.F(UiStrings.PlacingChampionFormat, prizeText) : UiStrings.F(UiStrings.PlacingFormat, placing, prizeText);
        }

        /// <summary>After the regular season: rank and the first playoff opponent, or the miss line.</summary>
        public static string RegularOutcome(EventResult ev)
        {
            int rank = 0;
            foreach (var r in ev.StandingsRows()) if (r.IsPlayer) rank = r.Rank;
            int seeds = ev.Seeds?.Length ?? 0;
            int opp = ev.PlayerFirstOpponent;
            if (opp < 0) return UiStrings.F(UiStrings.MissedPlayoffsFormat, rank, seeds);
            return UiStrings.F(UiStrings.MadePlayoffsFormat, rank, ev.ClubNames[opp]);
        }

        /// <summary>"前 4 名进入年度总决赛 · ...：10/7/5/5/3/2/1/0 · ..." from the format config.</summary>
        public static string QualificationNote(FormatConfig f)
        {
            var pts = new List<string>();
            if (f.finalsPoints != null) foreach (int p in f.finalsPoints) pts.Add(p.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return UiStrings.F(UiStrings.QualificationNoteFormat, f.finalsTeams, string.Join(UiStrings.PointsJoin, pts));
        }

        /// <summary>High-stress warning for the given names, or null when the list is empty.</summary>
        public static string HighStressWarning(IReadOnlyList<string> names)
            => names == null || names.Count == 0 ? null : UiStrings.F(UiStrings.HighStressFormat, string.Join(UiStrings.NameJoin, names));

        /// <summary>
        /// Sums the event's recorded effects per player: series counts and stress over the stages (regular season + bracket), mood
        /// from the event-end entry. Order: first appearance (regular season first).
        /// </summary>
        public static List<PlayerEventTotals> Totals(EventRecord rec)
        {
            var list = new List<PlayerEventTotals>();
            if (rec == null) return list;
            var map = new Dictionary<long, PlayerEventTotals>();
            PlayerEventTotals Get(long id)
            {
                if (!map.TryGetValue(id, out var t)) { t = new PlayerEventTotals { PlayerId = id }; map[id] = t; list.Add(t); }
                return t;
            }
            foreach (var stage in new[] { rec.RegularSeasonEffects, rec.BracketEffects })
                if (stage != null)
                    foreach (var e in stage)
                    {
                        var t = Get(e.PlayerId);
                        t.Played += e.SeriesPlayed; t.Won += e.SeriesWon; t.Lost += e.SeriesLost; t.StressDelta += e.StressDelta;
                    }
            if (rec.MoodEffects != null) foreach (var e in rec.MoodEffects) Get(e.PlayerId).MoodDelta += e.MoodDelta;
            return list;
        }

        /// <summary>The latest split whose regular season was played (running one included), searching back over all years; null if none.</summary>
        public static EventResult LatestSplit(SeasonFlow season)
        {
            if (season == null) return null;
            var h = season.History;
            for (int y = h.Count - 1; y >= 0; y--)
                for (int i = h[y].Events.Count - 1; i >= 0; i--)
                {
                    var ev = h[y].Events[i].Event;
                    if (ev.Type == EventType.Split && ev.RegularDone) return ev;
                }
            return null;
        }

        /// <summary>Display names of the split events in calendar order (columns of the qualification table).</summary>
        public static List<string> SplitNames(MatchConfig config)
        {
            var names = new List<string>();
            if (config?.calendar?.events != null)
                foreach (var e in config.calendar.events) if (e.type == EventType.Split) names.Add(UiStrings.EventName(e.nameKey));
            return names;
        }
    }
}
