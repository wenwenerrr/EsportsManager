using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;
using EventType = EsportsManager.Matches.EventType;

namespace EsportsManager.UI
{
    /// <summary>Shared layout pieces of the event screens.</summary>
    internal static class EventLayout
    {
        /// <summary>Column root (background, padding).</summary>
        public static void Root(VisualElement root)
        {
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            UiKit.Pad(root, 8);
        }

        /// <summary>Title row: big title, dim subtitle, spacer; the caller adds the buttons.</summary>
        public static VisualElement Title(VisualElement root, string title, string sub)
        {
            var row = UiKit.Row(44);
            row.style.flexShrink = 0;
            var t = UiKit.Ellipsis(UiKit.Lbl(title, 22, UiKit.Text, true));
            row.Add(t);
            if (!string.IsNullOrEmpty(sub)) { var s = UiKit.Lbl(sub, 14, UiKit.TextDim); s.style.marginLeft = 14; s.style.flexShrink = 0; row.Add(s); }
            row.Add(UiKit.Spacer());
            root.Add(row);
            return row;
        }

        /// <summary>A vertical ScrollView filling the rest of the screen; returns its content container.</summary>
        public static VisualElement Scroll(VisualElement root, string name, out ScrollView scroll)
        {
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = name;
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.flexBasis = 0; scroll.style.minHeight = 0; scroll.style.overflow = Overflow.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);
            return scroll.contentContainer;
        }

        /// <summary>
        /// Stops vertical squeezing inside a ScrollView: every element whose parent lays out as a column gets flexShrink 0
        /// (rows keep their shrink so ellipsis labels still work). Call after the content is built.
        /// </summary>
        public static void NoVerticalShrink(VisualElement content)
        {
            content.Query<VisualElement>().ForEach(e =>
            {
                if (e == content) return;
                var p = e.hierarchy.parent;
                if (p == null) return;
                var fd = p.style.flexDirection;
                bool row = fd.keyword == StyleKeyword.Undefined && fd.value == FlexDirection.Row;
                if (!row) e.style.flexShrink = 0;
            });
        }

        /// <summary>A panel with an accent title.</summary>
        public static VisualElement Section(VisualElement parent, string title)
        {
            var sec = UiKit.Block(UiKit.Panel, 6);
            UiKit.Pad(sec, 10); sec.style.marginBottom = 8;
            var t = UiKit.Ellipsis(UiKit.Lbl(title, 15, UiKit.Accent, true)); t.style.marginBottom = 4;
            sec.Add(t);
            parent.Add(sec);
            return sec;
        }

        /// <summary>A wrapping label.</summary>
        public static Label Wrap(string text, int size, Color color, bool bold = false)
        {
            var l = UiKit.Lbl(text, size, color, bold);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        /// <summary>Bottom message line (refusals in the error colour).</summary>
        public static void MessageBar(GameUi ui, VisualElement root)
        {
            var bar = UiKit.Row(32);
            bar.name = "message-bar"; bar.style.flexShrink = 0;
            bar.style.backgroundColor = UiKit.Panel; bar.style.paddingLeft = 8;
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = 4;
            var msg = UiKit.Ellipsis(UiKit.Lbl(ui.Message, 13, ui.MessageIsError ? UiKit.Bad : UiKit.TextDim, ui.MessageIsError));
            msg.name = "message"; msg.style.flexGrow = 1;
            bar.Add(msg);
            root.Add(bar);
        }
    }

    /// <summary>
    /// Event prep (before the regular season / the finals, and the one playoff adjustment): event name, format, card-lite lineup
    /// rows with starter / bench toggles, tactic selector with the counter hint, a high-stress warning and "开赛" (disabled with
    /// the reason while the lineup is incomplete). Finals without the player club: the qualification table and "观看总决赛".
    /// </summary>
    public static class EventPrepScreen
    {
        /// <summary>Builds the screen for the running event.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            EventLayout.Root(root);
            var rec = s.CurrentEvent;
            if (rec == null) { EventLayout.Title(root, UiStrings.NoEventLeft, null); return; }
            var ev = rec.Event;
            var phase = s.EventPhase;
            bool finals = ev.Type == EventType.Finals;
            bool plays = finals ? ev.PlayerInBracket : phase == EventPhase.Prepare || ev.PlayerInBracket;
            string name = UiStrings.EventName(ev.Def.nameKey);
            string titleFmt = !plays ? UiStrings.FinalsWatchTitleFormat
                : phase == EventPhase.PlayoffPrep ? UiStrings.PlayoffPrepTitleFormat : UiStrings.PrepTitleFormat;
            var title = EventLayout.Title(root, string.Format(titleFmt, name), string.Format(UiStrings.YearFormat, ev.Year));

            var lineupErr = s.Season.LineupError();
            if (plays)
            {
                var b = UiKit.Btn(UiStrings.StartEvent, ui.StartEventStage, UiKit.Good, lineupErr.IsNone, 16);
                b.name = "start-event"; ui.GuardFlowButton(b, lineupErr.IsNone); b.style.height = 34; b.style.marginRight = 0; b.style.paddingLeft = b.style.paddingRight = 24;
                title.Add(b);
            }
            else
            {
                var b = UiKit.Btn(UiStrings.WatchFinals, ui.StartEventStage, UiKit.Accent, true, 16);
                b.name = "watch-finals"; ui.GuardFlowButton(b, true); b.style.height = 34; b.style.marginRight = 0;
                title.Add(b);
            }

            var cfg = s.MatchConfig;
            int regular = s.Tournament.League.ClubCount - 1;
            var fmt = UiKit.Ellipsis(UiKit.Lbl(EventTexts.FormatSummary(ev.Type, phase, regular, cfg.format), 15, UiKit.Text, true));
            fmt.name = "format"; fmt.style.flexShrink = 0; root.Add(fmt);
            var note = UiKit.Lbl(plays ? EventTexts.PrepNote(ev.Type, phase) : UiStrings.NotQualifiedFinals, 13, plays ? UiKit.TextDim : UiKit.Warn);
            note.style.marginTop = 8; note.style.marginBottom = 8; note.style.flexShrink = 0; root.Add(note);

            var body = EventLayout.Scroll(root, "prep-scroll", out _);
            if (!plays)
            {
                var q = EventLayout.Section(body, UiStrings.QualificationTitle);
                q.style.width = 760; q.style.alignSelf = Align.Center;
                q.Add(QualificationView.Build(ev.Qualification, EventTexts.SplitNames(cfg), 36, 16));
                var qn = EventLayout.Wrap(EventTexts.QualificationNote(cfg.format), 12, UiKit.TextDim); qn.style.marginTop = 8; q.Add(qn);
                EventLayout.NoVerticalShrink(body);
                EventLayout.MessageBar(ui, root);
                return;
            }
            body.style.flexDirection = FlexDirection.Row; body.style.alignItems = Align.FlexStart;

            var left = new VisualElement(); left.style.flexGrow = 1; left.style.flexBasis = 0; left.style.minWidth = 0; left.style.marginRight = 8;
            body.Add(left);
            var lineup = EventLayout.Section(left, UiStrings.LineupTitle);
            var high = new List<string>();
            foreach (var c in OrderedRoster(s))
            {
                lineup.Add(PlayerRow(ui, c));
                if (c.IsStarter && c.HighStress) high.Add(c.Name);
            }
            if (!lineupErr.IsNone)
            {
                var why = EventLayout.Wrap(string.Format(UiStrings.PrepLineupIncompleteFormat, lineupErr.Used ?? 0, lineupErr.Limit ?? 0), 14, UiKit.Bad, true);
                why.name = "lineup-error"; why.style.marginTop = 6; lineup.Add(why);
            }

            var right = new VisualElement(); right.style.flexBasis = Length.Percent(38); right.style.flexGrow = 0; right.style.flexShrink = 0; right.style.minWidth = 380;
            body.Add(right);
            var tac = EventLayout.Section(right, UiStrings.TacticTitle);
            var tr = UiKit.Row(34);
            foreach (var t in MatchTactics.All)
            {
                var tt = t;
                bool sel = t == s.CurrentTactic;
                var b = UiKit.Btn(UiStrings.TacticName(t), () => ui.SetTactic(tt), sel ? UiKit.Accent : UiKit.PanelLight, true, 15);
                b.name = "tactic-" + MatchTactics.Key(t); b.style.height = 30; b.style.flexGrow = 1;
                if (sel) { b.style.unityFontStyleAndWeight = FontStyle.Bold; b.style.borderBottomWidth = 2; b.style.borderBottomColor = UiKit.Text; }
                tr.Add(b);
            }
            tac.Add(tr);
            var hint = EventLayout.Wrap(MainScreen.CounterHint(cfg), 13, UiKit.TextDim); hint.style.marginTop = 6; tac.Add(hint);
            var focus = EventLayout.Wrap(FocusHint(cfg), 13, UiKit.TextDim); focus.name = "focus-hint"; focus.style.marginTop = 2; tac.Add(focus);
            OurStrength(s, right);
            if (s.World != null) LeagueWidgets.OpponentPanel(ui, right);

            string warn = EventTexts.HighStressWarning(high);
            if (warn != null)
            {
                var w = UiKit.Block(UiKit.Panel, 6);
                UiKit.Pad(w, 10); w.style.marginBottom = 8; w.style.borderLeftWidth = 4; w.style.borderLeftColor = UiKit.Warn;
                var wl = EventLayout.Wrap(warn, 14, UiKit.Warn, true); wl.name = "stress-warning"; w.Add(wl);
                right.Add(w);
            }
            if (phase == EventPhase.PlayoffPrep)
            {
                var r = EventLayout.Section(right, UiStrings.RegularStandingsTitle);
                r.Add(EventLayout.Wrap(EventTexts.RegularOutcome(ev), 14, UiKit.Good, true));
            }
            if (finals)
            {
                var q = EventLayout.Section(right, UiStrings.QualificationTitle);
                q.Add(QualificationView.Build(ev.Qualification, EventTexts.SplitNames(cfg), 26, 13));
            }
            EventLayout.NoVerticalShrink(body);
            EventLayout.MessageBar(ui, root);
        }

        /// <summary>"战术专注：入侵→前期 · 团战→中期 · 发育→后期" from the config focus table.</summary>
        public static string FocusHint(MatchConfig cfg)
        {
            var parts = new List<string>();
            if (cfg?.tactic?.focus != null)
                foreach (var f in cfg.tactic.focus)
                    parts.Add(string.Format(UiStrings.FocusFormat, UiStrings.TacticName(f.tactic), UiStrings.PhaseName(f.phase)));
            return string.Format(UiStrings.FocusHintFormat, string.Join(UiStrings.CounterJoin, parts));
        }

        // Story 011: our exact phase strengths, execution and the weakest lane of each phase (no opponent numbers, no win chance).
        private static void OurStrength(GameSession s, VisualElement parent)
        {
            var sec = EventLayout.Section(parent, UiStrings.OurStrengthTitle);
            sec.name = "our-strength";
            var d = s.GetMatchPreview();
            if (d == null) { sec.Add(EventLayout.Wrap(UiStrings.PreviewUnavailable, 13, UiKit.TextDim)); return; }
            foreach (var k in MatchPhases.All)
            {
                int i = (int)k;
                var row = UiKit.Row(30);
                row.name = "phase-" + MatchPhases.Key(k);
                var pn = UiKit.Lbl(UiStrings.PhaseName(k), 15, UiKit.Text, true); pn.style.width = 44; pn.style.flexShrink = 0; row.Add(pn);
                var v = UiKit.Lbl(string.Format(UiStrings.PhaseStrengthFormat, UiKit.F1(d.Strength[i])), 18, UiKit.Accent, true);
                v.style.width = 64; v.style.flexShrink = 0; row.Add(v);
                var w = UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.WeakestFormat, d.WeakestLaneName[i], d.WeakestPlayerName[i], UiKit.F1(d.WeakestRating[i])), 13, UiKit.Warn));
                w.style.flexGrow = 1; w.style.flexShrink = 1; w.style.minWidth = 0; row.Add(w);
                sec.Add(row);
            }
            var er = UiKit.Row(30);
            er.name = "execution";
            var el = UiKit.Lbl(UiStrings.ExecutionLabel, 15, UiKit.Text, true); el.style.width = 64; el.style.flexShrink = 0; er.Add(el);
            var ev = UiKit.Lbl(d.Execution.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 18, UiKit.Accent, true);
            ev.style.width = 52; ev.style.flexShrink = 0; er.Add(ev);
            var en = UiKit.Ellipsis(UiKit.Lbl(UiStrings.ExecutionNote, 12, UiKit.TextDim)); en.style.flexGrow = 1; en.style.flexShrink = 1; en.style.minWidth = 0; er.Add(en);
            sec.Add(er);
        }

        // Starters in lane order first, then the bench in roster order.
        private static List<PlayerCardData> OrderedRoster(GameSession s)
        {
            var cards = s.GetRosterData();
            var byId = new Dictionary<long, PlayerCardData>();
            foreach (var c in cards) byId[c.Id] = c;
            var list = new List<PlayerCardData>();
            foreach (var slot in s.GetLineup().Slots) if (slot.PlayerId.HasValue && byId.TryGetValue(slot.PlayerId.Value, out var c)) list.Add(c);
            foreach (var c in cards) if (!c.IsStarter) list.Add(c);
            return list;
        }

        private static VisualElement PlayerRow(GameUi ui, PlayerCardData c)
        {
            var row = UiKit.Row(40);
            row.name = "prep-player-" + c.Id;
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = UiKit.PanelLight;
            row.style.paddingLeft = 6;
            row.style.borderLeftWidth = 3; row.style.borderLeftColor = c.IsStarter ? UiKit.Accent : UiKit.PanelLight;
            var n = UiKit.Ellipsis(UiKit.Lbl(c.Name, 16, c.IsStarter ? UiKit.Text : UiKit.TextDim, true));
            n.style.flexBasis = 0; n.style.flexGrow = 2; n.style.minWidth = 60; row.Add(n);
            var lane = UiKit.Lbl(c.LaneName, 14, UiKit.TextDim); lane.style.flexBasis = 0; lane.style.flexGrow = 1; lane.style.minWidth = 46; lane.style.flexShrink = 0; row.Add(lane);
            row.Add(Cell(string.Format(UiStrings.RatingFormat, UiKit.Int(c.Rating)), UiKit.Text, false));
            row.Add(Cell(string.Format(UiStrings.MoodFormat, UiKit.Int(c.Mood)), UiKit.Scale(c.Mood), false));
            row.Add(Cell(string.Format(UiStrings.StressFormat, UiKit.Int(c.Stress)), c.HighStress ? UiKit.Bad : UiKit.Scale(100f - c.Stress), true));
            long id = c.Id;
            var tog = UiKit.Btn(c.IsStarter ? UiStrings.StarterToggleOff : UiStrings.BenchToggleOn, () => ui.ToggleStarter(id),
                c.IsStarter ? UiKit.Accent : UiKit.Warn, true, 13);
            tog.name = "starter-toggle-" + c.Id; tog.style.height = 28; tog.style.flexShrink = 0;
            if (!c.IsStarter) tog.style.color = UiKit.Background;
            row.Add(tog);
            return row;
        }

        private static Label Cell(string text, Color color, bool bold)
        {
            var l = UiKit.Lbl(text, 14, color, bold);
            l.style.flexBasis = 0; l.style.flexGrow = 1; l.style.minWidth = 62; l.style.flexShrink = 0;
            return l;
        }
    }

    /// <summary>
    /// Regular season result: the player's series (round, opponent, score, win / loss and the single most important reason), the
    /// final table (player highlighted) and whether the club made the playoffs; the button leads to the playoff prep or plays the
    /// playoffs for the AI.
    /// </summary>
    public static class RegularResultScreen
    {
        /// <summary>Builds the screen for the running split.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            EventLayout.Root(root);
            var rec = s.CurrentEvent;
            if (rec == null || rec.Event.Regular == null) { EventLayout.Title(root, UiStrings.NoEventLeft, null); return; }
            var ev = rec.Event;
            var title = EventLayout.Title(root, string.Format(UiStrings.RegularTitleFormat, UiStrings.EventName(ev.Def.nameKey)),
                string.Format(UiStrings.YearFormat, ev.Year));
            bool can = s.EventPhase == EventPhase.PlayoffPrep;
            var go = ev.PlayerInBracket
                ? UiKit.Btn(UiStrings.ToPlayoffPrep, ui.GoToPlayoffs, UiKit.Good, can, 15)
                : UiKit.Btn(UiStrings.WatchPlayoffs, ui.GoToPlayoffs, UiKit.Accent, can, 15);
            go.name = "to-playoffs"; go.style.height = 34; go.style.marginRight = 0; ui.GuardFlowButton(go, can);
            title.Add(go);

            var outcome = UiKit.Ellipsis(UiKit.Lbl(EventTexts.RegularOutcome(ev), 18, ev.PlayerInBracket ? UiKit.Good : UiKit.Warn, true));
            outcome.name = "regular-outcome"; outcome.style.flexShrink = 0; outcome.style.marginBottom = 6;
            root.Add(outcome);

            var body = EventLayout.Scroll(root, "regular-scroll", out _);
            body.style.flexDirection = FlexDirection.Row; body.style.alignItems = Align.FlexStart;
            var left = new VisualElement(); left.style.flexGrow = 1; left.style.flexBasis = 0; left.style.minWidth = 0; left.style.marginRight = 8;
            body.Add(left);
            var mine = new List<EventSeries>();
            foreach (var se in ev.RegularSeries) if (se.PlayerInvolved) mine.Add(se);
            var list = EventLayout.Section(left, string.Format(UiStrings.YourSeriesTitleFormat, mine.Count));
            var lanes = ReportScreen.LaneNames(ui);
            foreach (var se in mine) list.Add(SeriesLine(ui, ev, se, lanes));

            var right = new VisualElement(); right.style.flexBasis = Length.Percent(44); right.style.flexGrow = 0; right.style.flexShrink = 0; right.style.minWidth = 400;
            body.Add(right);
            var table = EventLayout.Section(right, UiStrings.RegularStandingsTitle);
            var rows = ev.StandingsRows();
            table.Add(StandingsView.Build(rows, 30, 14));
            var note = EventLayout.Wrap(string.Format(UiStrings.StandingsNoteFormat, rows.Count), 12, UiKit.TextDim);
            note.style.marginTop = 6; table.Add(note);
            EventLayout.NoVerticalShrink(body);
            EventLayout.MessageBar(ui, root);
        }

        private static VisualElement SeriesLine(GameUi ui, EventResult ev, EventSeries se, Dictionary<string, string> lanes)
        {
            var res = se.Result;
            bool won = res.WinnerIsA;
            var item = new VisualElement();
            item.name = "regular-series-" + se.Round;
            item.style.borderLeftWidth = 4; item.style.borderLeftColor = won ? UiKit.Good : UiKit.Bad;
            item.style.paddingLeft = 8; item.style.marginBottom = 3;
            var l1 = UiKit.Row(22);
            var round = UiKit.Lbl(string.Format(UiStrings.RoundFormat, se.Round + 1), 13, UiKit.TextDim); round.style.width = 56; round.style.flexShrink = 0;
            l1.Add(round);
            var opp = UiKit.Ellipsis(UiKit.Lbl(ev.ClubNames[se.ClubB], 15, UiKit.Text, true)); opp.style.flexGrow = 1;
            l1.Add(opp);
            var score = UiKit.Lbl(string.Format(UiStrings.SeriesScoreFormat, res.ScoreA, res.ScoreB), 16, won ? UiKit.Good : UiKit.Bad, true);
            score.style.width = 56; score.style.flexShrink = 0; l1.Add(score);
            var wl = UiKit.Lbl(won ? UiStrings.SeriesWon : UiStrings.SeriesLost, 15, won ? UiKit.Good : UiKit.Bad, true);
            wl.style.width = 22; wl.style.flexShrink = 0; l1.Add(wl);
            item.Add(l1);
            var reasons = se.Report?.Reasons;
            if (reasons != null && reasons.Count > 0)
            {
                // One short line under the series; a long reason ends in an ellipsis instead of growing the list.
                var r = UiKit.Ellipsis(UiKit.Lbl(UiStrings.ReasonBullet + ReportScreen.Reason(ui, reasons[0], res, lanes), 12, reasons[0].Value >= 0f ? UiKit.TextDim : UiKit.Warn));
                r.style.marginLeft = 56; r.style.height = 16; item.Add(r);
            }
            return item;
        }
    }

    /// <summary>
    /// Event result: the bracket (semis + final, winner bold, player highlighted), final placings with prizes, the player club's
    /// placing / prize / funds, the players' stress and mood changes, then the detailed reports of the player's bracket series.
    /// "继续" acknowledges the event.
    /// </summary>
    public static class BracketScreen
    {
        /// <summary>Builds the screen for the finished (Done) event.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            EventLayout.Root(root);
            var rec = s.CurrentEvent ?? s.LastEvent;
            if (rec == null || !rec.Event.IsComplete) { EventLayout.Title(root, UiStrings.NoEventLeft, null); return; }
            var ev = rec.Event;
            var title = EventLayout.Title(root, string.Format(UiStrings.BracketTitleFormat, UiStrings.EventName(ev.Def.nameKey)),
                string.Format(UiStrings.YearFormat, ev.Year));
            bool done = s.EventPhase == EventPhase.Done;
            var cont = UiKit.Btn(UiStrings.Continue, ui.ContinueAfterEvent, UiKit.Good, done, 16);
            cont.name = "event-continue"; ui.GuardFlowButton(cont, done); cont.style.height = 34; cont.style.marginRight = 0; cont.style.paddingLeft = cont.style.paddingRight = 24;
            title.Add(cont);

            var body = EventLayout.Scroll(root, "bracket-scroll", out _);
            // Debug "report" demo: the same screen without the overview sections, so the detailed reports are on screen.
            if (!ui.ScrollToReports) BuildOverview(s, rec, body);
            BuildReports(ui, ev, body);
            EventLayout.NoVerticalShrink(body);
            EventLayout.MessageBar(ui, root);
        }

        private static void BuildOverview(GameSession s, EventRecord rec, VisualElement body)
        {
            var ev = rec.Event;
            var top = UiKit.Row(); top.style.alignItems = Align.Stretch; body.Add(top);
            var bracket = EventLayout.Section(top, UiStrings.BracketTitle);
            bracket.style.flexGrow = 1; bracket.style.marginRight = 8;
            bracket.Add(Bracket(ev));
            var placings = EventLayout.Section(top, UiStrings.PlacingsTitle);
            placings.style.width = 420; placings.style.flexShrink = 0;
            foreach (int c in ev.FinalOrder) placings.Add(PlacingRow(ev, c));

            var mid = UiKit.Row(); mid.style.alignItems = Align.Stretch; body.Add(mid);
            var you = EventLayout.Section(mid, UiStrings.YourResultTitle);
            you.style.width = 420; you.style.flexShrink = 0; you.style.marginRight = 8;
            var pl = EventLayout.Wrap(EventTexts.PlacingText(ev.PlayerPlacing, UiKit.Yuan(rec.PrizeIncome)), 18,
                ev.PlayerPlacing > 0 ? UiKit.Good : UiKit.TextDim, true);
            pl.name = "player-placing"; you.Add(pl);
            var funds = UiKit.Lbl(string.Format(UiStrings.FundsChangeFormat, UiKit.Yuan(rec.FundsBefore), UiKit.Yuan(rec.FundsAfter)), 14, UiKit.Text);
            funds.style.marginTop = 6; you.Add(funds);
            var wr = UiKit.Lbl(string.Format(UiStrings.RecentWinRateFormat, UiKit.Int(rec.RecentWinRateAfter)), 13, UiKit.TextDim);
            wr.style.marginTop = 4; you.Add(wr);
            var eff = EventLayout.Section(mid, UiStrings.PlayerEffectsTitle);
            eff.style.flexGrow = 1; eff.style.flexBasis = 0;
            BuildEffects(s, rec, eff);
        }

        private static void BuildReports(GameUi ui, EventResult ev, VisualElement body)
        {

            var reportsTitle = UiKit.Lbl(UiStrings.ReportsTitle, 18, UiKit.Accent, true);
            reportsTitle.name = "reports"; reportsTitle.style.marginTop = 4; reportsTitle.style.alignSelf = Align.Center;
            body.Add(reportsTitle);
            var lanes = ReportScreen.LaneNames(ui);
            int i = 0;
            foreach (var se in ev.PlayoffSeries)
                if (se.PlayerInvolved) body.Add(ReportScreen.SeriesPanel(ui, se, ev.ClubNames[se.ClubB], i++, lanes));
            if (i == 0) { var none = UiKit.Lbl(UiStrings.NoBracketSeries, 14, UiKit.TextDim); none.style.alignSelf = Align.Center; body.Add(none); }
        }

        private static VisualElement Bracket(EventResult ev)
        {
            var row = UiKit.Row();
            row.name = "bracket"; row.style.alignItems = Align.Center;
            var series = ev.PlayoffSeries;
            if (series.Count > 1)
            {
                var semis = new VisualElement(); semis.style.width = 250; semis.style.marginRight = 16;
                for (int i = 0; i < series.Count - 1; i++) semis.Add(MatchBox(ev, series[i]));
                row.Add(semis);
            }
            var final = series[series.Count - 1];
            var fin = new VisualElement(); fin.style.width = 250;
            fin.Add(MatchBox(ev, final));
            var champ = UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.ChampionFormat, ev.ClubNames[final.Winner]), 17,
                final.Winner == ev.PlayerClub ? UiKit.Accent : UiKit.Warn, true));
            champ.name = "champion"; fin.Add(champ);
            row.Add(fin);
            return row;
        }

        private static VisualElement MatchBox(EventResult ev, EventSeries se)
        {
            var box = UiKit.Block(UiKit.PanelLight, 4);
            UiKit.Pad(box, 6); box.style.marginBottom = 8;
            box.Add(UiKit.Lbl(EventTexts.StageName(se.Stage), 12, UiKit.TextDim));
            box.Add(Side(ev, se.ClubA, se.Result.ScoreA, se.Winner == se.ClubA));
            box.Add(Side(ev, se.ClubB, se.Result.ScoreB, se.Winner == se.ClubB));
            return box;
        }

        private static VisualElement Side(EventResult ev, int club, int score, bool winner)
        {
            var r = UiKit.Row(26);
            r.style.paddingLeft = 4;
            if (club == ev.PlayerClub)
            {
                r.name = "bracket-player";
                r.style.backgroundColor = StandingsView.PlayerRowColor;
                r.style.borderLeftWidth = 3; r.style.borderLeftColor = UiKit.Accent;
            }
            int seed = System.Array.IndexOf(ev.Seeds, club) + 1;
            var sd = UiKit.Lbl(string.Format(UiStrings.SeedFormat, seed), 12, UiKit.TextDim); sd.style.width = 38; sd.style.flexShrink = 0;
            r.Add(sd);
            var n = UiKit.Ellipsis(UiKit.Lbl(ev.ClubNames[club], 15, winner ? UiKit.Text : UiKit.TextDim, winner)); n.style.flexGrow = 1;
            r.Add(n);
            var sc = UiKit.Lbl(score.ToString(System.Globalization.CultureInfo.InvariantCulture), 16, winner ? UiKit.Good : UiKit.TextDim, winner);
            sc.style.width = 24; sc.style.flexShrink = 0; sc.style.unityTextAlign = TextAnchor.MiddleRight; sc.style.marginRight = 4;
            r.Add(sc);
            return r;
        }

        private static VisualElement PlacingRow(EventResult ev, int club)
        {
            bool player = club == ev.PlayerClub;
            var r = UiKit.Row(26);
            r.style.paddingLeft = 4; r.style.borderBottomWidth = 1; r.style.borderBottomColor = UiKit.PanelLight;
            if (player)
            {
                r.name = "placing-player";
                r.style.backgroundColor = StandingsView.PlayerRowColor;
                r.style.borderLeftWidth = 3; r.style.borderLeftColor = UiKit.Accent;
            }
            var p = UiKit.Lbl(string.Format(UiStrings.RankFormat, ev.Placing[club]), 14, player ? UiKit.Text : UiKit.TextDim, player);
            p.style.width = 72; p.style.flexShrink = 0; r.Add(p);
            var n = UiKit.Ellipsis(UiKit.Lbl(ev.ClubNames[club], 14, UiKit.Text, player)); n.style.flexGrow = 1; r.Add(n);
            var z = UiKit.Lbl(UiKit.Yuan(ev.Prize[club]), 14, ev.Prize[club] > 0 ? UiKit.Good : UiKit.TextDim, player);
            z.style.width = 110; z.style.flexShrink = 0; z.style.unityTextAlign = TextAnchor.MiddleRight; z.style.marginRight = 6; r.Add(z);
            return r;
        }

        private static void BuildEffects(GameSession s, EventRecord rec, VisualElement parent)
        {
            var totals = EventTexts.Totals(rec);
            if (totals.Count == 0) { parent.Add(UiKit.Lbl(UiStrings.NoEffects, 13, UiKit.TextDim)); return; }
            parent.Add(EffectRow(true, UiStrings.ColPlayer, UiStrings.ColPlayed, UiStrings.ColStress, UiStrings.ColMood, UiKit.TextDim, UiKit.TextDim, UiKit.TextDim));
            foreach (var t in totals)
            {
                string name = s.PlayerName(t.PlayerId);
                if (string.IsNullOrEmpty(name)) continue;
                parent.Add(EffectRow(false, name, string.Format(UiStrings.PlayedFormat, t.Played, t.Won, t.Lost), UiKit.Delta(t.StressDelta),
                    UiKit.Delta(t.MoodDelta), UiKit.Text, UiKit.DeltaColor(t.StressDelta, true), UiKit.DeltaColor(t.MoodDelta)));
            }
        }

        private static VisualElement EffectRow(bool header, string a, string b, string c, string d, Color ca, Color cc, Color cd)
        {
            var r = UiKit.Row(header ? 22 : 26);
            r.style.borderBottomWidth = 1; r.style.borderBottomColor = UiKit.PanelLight;
            int size = header ? 12 : 14;
            var la = UiKit.Ellipsis(UiKit.Lbl(a, size, ca, !header)); la.style.flexGrow = 1; la.style.flexBasis = 0; r.Add(la);
            var lb = UiKit.Lbl(b, size, header ? UiKit.TextDim : UiKit.Text); lb.style.width = 130; lb.style.flexShrink = 0; r.Add(lb);
            var lc = UiKit.Lbl(c, size, cc, !header); lc.style.width = 80; lc.style.flexShrink = 0; r.Add(lc);
            var ld = UiKit.Lbl(d, size, cd, !header); ld.style.width = 80; ld.style.flexShrink = 0; r.Add(ld);
            return r;
        }
    }
}
