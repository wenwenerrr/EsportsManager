using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Teams;

namespace EsportsManager.Segments
{
    /// <summary>What an event stage / event end did to one rostered player (actual deltas, after clamping).</summary>
    public sealed class EventPlayerEffect
    {
        /// <summary>Player.Id.</summary>
        public long PlayerId;
        /// <summary>Series he played (participant snapshots) in the stage / whole event.</summary>
        public int SeriesPlayed;
        /// <summary>Of those, won.</summary>
        public int SeriesWon;
        /// <summary>Of those, lost.</summary>
        public int SeriesLost;
        /// <summary>Stress after - before (stage effects only).</summary>
        public float StressDelta;
        /// <summary>Mood after - before (event-end effects only).</summary>
        public float MoodDelta;
    }

    /// <summary>
    /// Applies the match terms of an event to the player club (numbers.md 4.18 "对其他公式的影响", 4.6, 4.7, 4.8). Uses the SAME
    /// formula code as the segment runner (<see cref="SegmentFormulas.MatchStressDelta"/> / <see cref="SegmentFormulas.MatchMoodDelta"/>
    /// share the bracket terms with StressDelta / MoodDelta). Every count comes from the participant snapshots; players who left
    /// the roster are skipped; players who played no series get nothing (and draw nothing).
    /// </summary>
    /// <remarks>
    /// WHEN (decision): stress and familiarity at the end of EACH stage (regular season; playoffs / finals bracket), so a heavy
    /// regular season raises stress before the playoffs; mood ONCE at event end with the sign of (series won - series lost) over
    /// the whole event; the recent results are replaced at event end. UNIT: s_match x SERIES played, s_loss x SERIES lost (same
    /// unit as Story 004 stage A2). DRAWS (on the caller's source, the tournament's): stage: for each rostered player in roster
    /// order who played, one R(stress.randomRange); event end: for each rostered player in roster order who played, one
    /// R(mood.randomRange).
    /// </remarks>
    public static class EventEffects
    {
        /// <summary>Stage effects: stress match terms and + f_win per won series to the pairs who played it. Returns one entry per player who played.</summary>
        public static List<EventPlayerEffect> ApplyStage(SegmentConfig cfg, Team team, IReadOnlyList<EventSeries> playerSeries, IRandomSource rng)
        {
            var counts = Count(playerSeries);
            var list = new List<EventPlayerEffect>();
            float rs = cfg.stress.randomRange;
            foreach (var p in team.Roster)
            {
                if (!counts.TryGetValue(p.Id, out var c)) continue;
                float r = rng.NextFloat(1f - rs, 1f + rs);
                float before = p.Stress;
                p.Stress += SegmentFormulas.MatchStressDelta(cfg, p, c.SeriesPlayed, c.SeriesLost, r);
                c.StressDelta = p.Stress - before;
                list.Add(c);
            }
            var winning = new List<IReadOnlyList<long>>();
            foreach (var s in playerSeries)
            {
                if (!s.PlayerInvolved || !s.Result.WinnerIsA) continue;
                var ids = new List<long>();
                foreach (var part in s.Result.SideA.Participants) ids.Add(part.PlayerId);
                winning.Add(ids);
            }
            if (winning.Count > 0) team.AddWinFamiliarity(winning);
            return list;
        }

        /// <summary>
        /// Event-end effects: mood win/loss term for every player who played, then the team's recent results = this event's series
        /// results (unchanged when the player club played none). Returns one entry per player who played.
        /// </summary>
        public static List<EventPlayerEffect> ApplyEventEnd(SegmentConfig cfg, Team team, IReadOnlyList<EventSeries> playerSeries, IRandomSource rng)
        {
            var counts = Count(playerSeries);
            var list = new List<EventPlayerEffect>();
            float rm = cfg.mood.randomRange;
            foreach (var p in team.Roster)
            {
                if (!counts.TryGetValue(p.Id, out var c)) continue;
                float r = rng.NextFloat(1f - rm, 1f + rm);
                int sign = c.SeriesWon > c.SeriesLost ? 1 : (c.SeriesWon < c.SeriesLost ? -1 : 0);
                float before = p.Mood;
                p.Mood += SegmentFormulas.MatchMoodDelta(cfg, p, sign, r);
                c.MoodDelta = p.Mood - before;
                list.Add(c);
            }
            var results = new List<bool>();
            foreach (var s in playerSeries) if (s.PlayerInvolved) results.Add(s.Result.WinnerIsA);
            team.SetRecentResults(results);
            return list;
        }

        private static Dictionary<long, EventPlayerEffect> Count(IReadOnlyList<EventSeries> series)
        {
            var map = new Dictionary<long, EventPlayerEffect>();
            foreach (var s in series)
            {
                if (!s.PlayerInvolved) continue;
                foreach (var part in s.Result.SideA.Participants)
                {
                    if (!map.TryGetValue(part.PlayerId, out var c)) map[part.PlayerId] = c = new EventPlayerEffect { PlayerId = part.PlayerId };
                    c.SeriesPlayed++;
                    if (s.Result.WinnerIsA) c.SeriesWon++; else c.SeriesLost++;
                }
            }
            return map;
        }
    }
}
