using System;
using System.Collections.Generic;
using EsportsManager.Events;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;

namespace EsportsManager.Leagues
{
    /// <summary>AI club style (league.md 风格).</summary>
    public enum ClubStyle
    {
        /// <summary>入侵 (early phase).</summary>
        Invade = 0,
        /// <summary>团战 (mid phase).</summary>
        Teamfight = 1,
        /// <summary>后期运营 (late phase).</summary>
        Late = 2,
        /// <summary>明星单核 (one star lane; star phase early or late).</summary>
        Star = 3,
    }

    /// <summary>
    /// AI personality (league.md 性格 + 个性偏差): aggression, frugality, adaptability in [0, 1] and the lifelong offsets. Never
    /// shown in the UI.
    /// </summary>
    public sealed class ClubPersonality
    {
        /// <summary>激进: tolerates stress, trains more.</summary>
        public float Aggression;
        /// <summary>节俭: saves money.</summary>
        public float Frugality;
        /// <summary>适应: willingness to switch style after a bad year.</summary>
        public float Adaptability;
        /// <summary>Rest-threshold offset (+-5).</summary>
        public float OffRest;
        /// <summary>Cash-floor offset in segments of wages (+-1).</summary>
        public float OffCash;
        /// <summary>Renewal threshold offset (+-0.2; used by the transfer window, Story 005).</summary>
        public float OffRenew;
        /// <summary>Bid offset (+-10%; Story 005).</summary>
        public float OffBid;
    }

    /// <summary>One style switch (league news, year review).</summary>
    public sealed class StyleChange
    {
        /// <summary>Year of the switch (0 = warm-up).</summary>
        public int Year;
        /// <summary>Old / new style.</summary>
        public ClubStyle From, To;
    }

    /// <summary>Yearly totals of one club for the wage index (economy.md F3: income excludes transfers, fire sales, loans, capital).</summary>
    public sealed class ClubLedger
    {
        /// <summary>Livestream + business + sponsorship + event income + prizes, yuan.</summary>
        public long Income;
        /// <summary>Wages paid, yuan.</summary>
        public long Wages;
        /// <summary>Event expenses, yuan.</summary>
        public long EventExpenses;
        /// <summary>Prizes (part of <see cref="Income"/>), yuan.</summary>
        public long Prizes;
    }

    /// <summary>A champion-fatigue window (economy.md 冠军疲劳): strength f on global segments [From, To] (year x 12 + index).</summary>
    public sealed class FatigueSpan
    {
        /// <summary>First / last global segment index (inclusive).</summary>
        public int From, To;
        /// <summary>Strength f (finals 1, split 0.5).</summary>
        public float Strength;
    }

    /// <summary>
    /// One club of the league (design/gdd/league.md "同一套模型"): the SAME data and formulas for the player club and the AI clubs
    /// -- a <see cref="Team"/> (roster, lineup, familiarity, popularity, funds), its own <see cref="SegmentRunner"/> (sponsor
    /// contracts, offers, events, training) and keyed random streams. AI-only fields (style, personality, tactic history used
    /// for decisions) exist on every club so a club can change hands (the player takes one over after the warm-up).
    /// </summary>
    public sealed class Club
    {
        /// <summary>Stable club id assigned at generation (= schedule index); never changes.</summary>
        public int Id { get; }
        /// <summary>Display name.</summary>
        public string Name => Team.Name;
        /// <summary>True once the player runs this club.</summary>
        public bool IsPlayer { get; internal set; }
        /// <summary>Roster, lineup, familiarity, popularity, funds.</summary>
        public Team Team { get; }
        /// <summary>Segment actions of this club (same runner type as the player's).</summary>
        public SegmentRunner Runner { get; }
        /// <summary>Random events of this club (null without an event config).</summary>
        public EventEngine Events { get; }

        /// <summary>Current style.</summary>
        public ClubStyle Style { get; internal set; }
        /// <summary>The style before the current one (no-return rule), or null.</summary>
        public ClubStyle? PreviousStyle { get; internal set; }
        /// <summary>Star lane id (Star style only).</summary>
        public string StarLane { get; internal set; }
        /// <summary>Star player id (Star style only), -1 otherwise.</summary>
        public long StarPlayerId { get; internal set; } = -1;
        /// <summary>Star phase 0 (early) or 2 (late) (Star style only).</summary>
        public int StarPhase { get; internal set; }
        /// <summary>Personality (AI decisions; hidden).</summary>
        public ClubPersonality Personality { get; } = new ClubPersonality();
        /// <summary>Events played since the last style switch (lock: 3).</summary>
        public int EventsSinceSwitch { get; internal set; } = 99;
        /// <summary>All style switches.</summary>
        public List<StyleChange> StyleHistory { get; } = new List<StyleChange>();
        /// <summary>Tactics of the last stages played (newest last, at most 5) -- shown as "最近 5 场战术".</summary>
        public List<MatchTactic> TacticHistory { get; } = new List<MatchTactic>();
        /// <summary>Lanes without a main-lane player (filled by off-lane bench players; next window fills them first).</summary>
        public HashSet<string> NeedLanes { get; } = new HashSet<string>();
        /// <summary>League loan still owed (AI only; economy F7).</summary>
        public long Debt { get; internal set; }
        /// <summary>Total borrowed so far.</summary>
        public long LoanTaken { get; internal set; }
        /// <summary>True after a self-rescue left the player club below 0 with 5 players (bankruptcy; game over is Story 007).</summary>
        public bool Bankrupt { get; internal set; }
        /// <summary>
        /// Champion-fatigue media obligation: action points to stream this segment (economy.md; Story 005 sets it). 0 until then.
        /// </summary>
        public int MediaObligationPoints { get; set; }
        /// <summary>This year's totals for the wage index.</summary>
        public ClubLedger Ledger { get; internal set; } = new ClubLedger();
        /// <summary>Last year's totals (year review).</summary>
        public ClubLedger LastLedger { get; internal set; }
        /// <summary>Champion-fatigue windows (economy.md).</summary>
        public List<FatigueSpan> Fatigue { get; } = new List<FatigueSpan>();

        /// <summary>Fatigue strength f on a global segment (overlaps take the largest), 0 when none.</summary>
        public float FatigueAt(int globalSegment)
        {
            float f = 0f;
            foreach (var s in Fatigue) if (s.From <= globalSegment && globalSegment <= s.To && s.Strength > f) f = s.Strength;
            return f;
        }

        /// <summary>Segments left (from <paramref name="globalSegment"/> on, inclusive) of the strongest running fatigue window, 0 when none.</summary>
        public int FatigueSegmentsLeft(int globalSegment)
        {
            int left = 0;
            foreach (var s in Fatigue) if (s.From <= globalSegment && globalSegment <= s.To) left = System.Math.Max(left, s.To - globalSegment + 1);
            return left;
        }

        /// <summary>Placings of this year's events (calendar order).</summary>
        public List<int> YearPlacings { get; } = new List<int>();
        /// <summary>Finals place of the previous year (0 = none yet).</summary>
        public int LastYearFinalsPlace { get; internal set; }

        internal KeyedStream SegmentRng, TeamRng, EventRng, NoiseRng;

        internal Club(int id, Team team, SegmentRunner runner, EventEngine events, KeyedStream seg, KeyedStream teamRng, KeyedStream ev, KeyedStream noise)
        {
            Id = id;
            Team = team ?? throw new ArgumentNullException(nameof(team));
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Events = events;
            SegmentRng = seg; TeamRng = teamRng; EventRng = ev; NoiseRng = noise;
        }

        /// <summary>Remembers a stage tactic (keeps the last 5).</summary>
        public void RecordTactic(MatchTactic t)
        {
            TacticHistory.Add(t);
            while (TacticHistory.Count > 5) TacticHistory.RemoveAt(0);
        }

        /// <summary>Wages per segment W = sum of salaries / segments per year.</summary>
        public double WagesPerSegment(int segmentsPerYear)
        {
            long s = 0;
            foreach (var p in Team.Roster) s += p.Salary;
            return s / (double)segmentsPerYear;
        }

        /// <summary>The rostered player with this id, or null.</summary>
        public Player Find(long id)
        {
            foreach (var p in Team.Roster) if (p.Id == id) return p;
            return null;
        }
    }
}
