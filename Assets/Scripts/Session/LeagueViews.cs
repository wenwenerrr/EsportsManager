using System;
using System.Collections.Generic;
using EsportsManager.Leagues;
using EsportsManager.Matches;
using EsportsManager.Players;

namespace EsportsManager.Session
{
    /// <summary>One player as other clubs see him: name, lane, age, potential RANGE, phase shape on the absolute 0-100 scale.</summary>
    public sealed class ScoutedPlayerData
    {
        /// <summary>Player.Id.</summary>
        public long Id;
        /// <summary>Name.</summary>
        public string Name;
        /// <summary>Main lane display name.</summary>
        public string LaneName;
        /// <summary>Age.</summary>
        public int Age;
        /// <summary>Potential estimate range (never the true potential).</summary>
        public int PotentialLow, PotentialHigh;
        /// <summary>Raw phase ratings E / M / L, 0-100 (drawn as a shape; the UI shows no numbers).</summary>
        public float[] Phases = new float[3];
        /// <summary>True when he starts.</summary>
        public bool IsStarter;
        /// <summary>True when his contract ends after this year.</summary>
        public bool Expiring;
    }

    /// <summary>One job offer card (league.md 聘书画面). No AI threshold or personality number.</summary>
    public sealed class JobOfferData
    {
        /// <summary>Index to pass to <see cref="GameSession.TakeOffer"/>.</summary>
        public int Index;
        /// <summary>Tier.</summary>
        public OfferTier Tier;
        /// <summary>Club name.</summary>
        public string ClubName;
        /// <summary>Funds, yuan.</summary>
        public long Funds;
        /// <summary>Last year's overall place.</summary>
        public int LastYearRank;
        /// <summary>Club style (AI style).</summary>
        public ClubStyle Style;
        /// <summary>Players whose contract ends after this year.</summary>
        public int ExpiringContracts;
        /// <summary>Roster, starters first.</summary>
        public List<ScoutedPlayerData> Players = new List<ScoutedPlayerData>();
    }

    /// <summary>Pre-match information about one opponent (league.md UI; match.md 赛前准备).</summary>
    public sealed class OpponentInfoData
    {
        /// <summary>Club index.</summary>
        public int ClubIndex;
        /// <summary>Name.</summary>
        public string ClubName;
        /// <summary>Style label (null: incomplete lineup).</summary>
        public ClubStyle? StyleTag;
        /// <summary>Roster, starters first.</summary>
        public List<ScoutedPlayerData> Players = new List<ScoutedPlayerData>();
        /// <summary>Tactics of its last (up to) 5 stages, oldest first.</summary>
        public List<MatchTactic> RecentTactics = new List<MatchTactic>();
        /// <summary>Last meeting with the player club: our S_k - their S_k (from the report), or null.</summary>
        public float[] LastMeetingGap;
        /// <summary>True when we won the last meeting.</summary>
        public bool LastMeetingWon;
    }

    /// <summary>View data of the league for the UI (Story 013). Reads only; never shows AI thresholds or personality numbers.</summary>
    public static class LeagueViews
    {
        /// <summary>The three job offers (empty unless the session is choosing).</summary>
        public static List<JobOfferData> JobOffers(GameSession s)
        {
            var list = new List<JobOfferData>();
            if (!s.ChoosingOffer) return list;
            var w = s.World;
            for (int i = 0; i < w.Offers.Count; i++)
            {
                var o = w.Offers[i];
                var c = w.Clubs[o.ClubId];
                var d = new JobOfferData { Index = i, Tier = o.Tier, ClubName = c.Name, Funds = c.Team.Funds, LastYearRank = o.LastYearRank, Style = c.Style };
                d.Players = Scout(s, c);
                foreach (var p in d.Players) if (p.Expiring) d.ExpiringContracts++;
                list.Add(d);
            }
            return list;
        }

        /// <summary>Roster view of a club, starters in lane order first, then the bench.</summary>
        public static List<ScoutedPlayerData> Scout(GameSession s, Club c)
        {
            var list = new List<ScoutedPlayerData>();
            var w = s.World;
            var starters = c.Team.Starters();
            var order = new List<Player>(starters);
            foreach (var p in c.Team.Roster) if (!starters.Contains(p)) order.Add(p);
            foreach (var p in order)
            {
                var est = p.GetPotentialEstimate();
                var d = new ScoutedPlayerData
                {
                    Id = p.Id, Name = p.Name, LaneName = w.Players.FindLane(p.MainLane).displayName, Age = p.Age,
                    PotentialLow = est.Low, PotentialHigh = est.High, IsStarter = starters.Contains(p), Expiring = p.ContractYearsLeft <= 1,
                };
                for (int k = 0; k < 3; k++) d.Phases[k] = w.Ai.PhaseRating(p, k);
                list.Add(d);
            }
            return list;
        }

        /// <summary>Style tag of a club for the standings (null without a league / incomplete lineup).</summary>
        public static ClubStyle? StyleTag(GameSession s, int clubIndex)
            => s.World == null || clubIndex < 0 || clubIndex >= s.World.Clubs.Count ? null : s.World.StyleLabel(s.World.Clubs[clubIndex]);

        /// <summary>
        /// The opponent to show before a stage: the bracket opponent when the player club is seeded, else the first-round
        /// opponent of the running / next split; -1 without a league.
        /// </summary>
        public static int DefaultOpponent(GameSession s)
        {
            var w = s.World;
            if (w == null || w.PlayerClubId < 0) return -1;
            var ev = s.CurrentEvent?.Event;
            if (ev != null && ev.PlayerInBracket && (ev.Type == EventType.Finals || ev.RegularDone)) return ev.PlayerFirstOpponent;
            return w.Schedule.PlayerOpponentOfRound(0);
        }

        /// <summary>Pre-match info about club <paramref name="clubIndex"/>.</summary>
        public static OpponentInfoData OpponentInfo(GameSession s, int clubIndex)
        {
            var w = s.World;
            if (w == null || clubIndex < 0 || clubIndex >= w.Clubs.Count) return null;
            var c = w.Clubs[clubIndex];
            var d = new OpponentInfoData { ClubIndex = clubIndex, ClubName = c.Name, StyleTag = w.StyleLabel(c), Players = Scout(s, c) };
            d.RecentTactics.AddRange(c.TacticHistory);
            int me = w.PlayerClubId;
            if (me < 0) return d;
            var hist = w.Season.History;
            for (int y = hist.Count - 1; y >= 0 && d.LastMeetingGap == null; y--)
                for (int e = hist[y].Events.Count - 1; e >= 0 && d.LastMeetingGap == null; e--)
                {
                    var ev = hist[y].Events[e].Event;
                    var all = new List<EventSeries>(ev.RegularSeries);
                    all.AddRange(ev.PlayoffSeries);
                    for (int i = all.Count - 1; i >= 0; i--)
                    {
                        var se = all[i];
                        if (!se.Involves(me) || !se.Involves(clubIndex)) continue;
                        var mine = se.SideOf(me).Evaluation; var theirs = se.SideOf(clubIndex).Evaluation;
                        if (mine == null || theirs == null) continue;
                        d.LastMeetingGap = new float[3];
                        for (int k = 0; k < 3; k++) d.LastMeetingGap[k] = (float)(mine.Strength[k] - theirs.Strength[k]);
                        d.LastMeetingWon = se.Won(me);
                        break;
                    }
                }
            return d;
        }

        /// <summary>League news, newest first (at most news.max_items).</summary>
        public static List<NewsItem> News(GameSession s)
        {
            var list = s.World == null ? new List<NewsItem>() : new List<NewsItem>(s.World.News);
            list.Reverse();
            return list;
        }
    }
}
