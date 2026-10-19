using System.Collections.Generic;
using System.Globalization;
using EsportsManager.Matches;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>The league table element (rank, club, series W-L, games W-L, game diff), player club highlighted. Shared by the summary and the standings screen.</summary>
    public static class StandingsView
    {
        // Rank column fits "第 10 名" (>= 10 clubs); widths scale with the font size.
        // Proportional columns: {flex weight, minimum width at 13 px}; the club column (min 0) takes the slack and ellipsizes.
        private static readonly float[][] Cols =
        {
            new[] { 1.1f, 62f }, new[] { 3f, 0f }, new[] { 1.5f, 78f }, new[] { 1.3f, 68f }, new[] { 1f, 46f },
        };

        /// <summary>Highlight colour of the player row.</summary>
        public static readonly Color PlayerRowColor = new Color(90f / 255f, 160f / 255f, 240f / 255f, 0.28f);

        /// <summary>Builds the table.</summary>
        public static VisualElement Build(IReadOnlyList<StandingsRow> rows, float rowHeight, int fontSize)
        {
            var table = new VisualElement();
            table.name = "standings-table";
            table.Add(Row(true, rowHeight, fontSize, false, UiStrings.ColRank, UiStrings.ColClub, UiStrings.ColSeries, UiStrings.ColGames, UiStrings.ColGameDiff));
            foreach (var r in rows)
                table.Add(Row(false, rowHeight, fontSize, r.IsPlayer,
                    string.Format(UiStrings.RankFormat, r.Rank), r.ClubName,
                    string.Format(UiStrings.WinLossFormat, r.SeriesWins, r.SeriesLosses),
                    string.Format(UiStrings.WinLossFormat, r.GamesWon, r.GamesLost),
                    r.GameDiff.ToString(UiStrings.SignedIntFormat, CultureInfo.InvariantCulture)));
            return table;
        }

        /// <summary>"名次 第 5 → 第 3 名（上升 2）"; a smaller rank number is better.</summary>
        public static string RankChange(int before, int after)
        {
            string d = after < before ? string.Format(UiStrings.RankUpFormat, before - after)
                : after > before ? string.Format(UiStrings.RankDownFormat, after - before) : UiStrings.RankSame;
            return string.Format(UiStrings.RankChangeFormat, before, after, d);
        }

        /// <summary>Colour of a rank change (up = good).</summary>
        public static Color RankChangeColor(int before, int after) => after < before ? UiKit.Good : after > before ? UiKit.Bad : UiKit.TextDim;

        private static VisualElement Row(bool header, float h, int size, bool player, params string[] cells)
        {
            var row = UiKit.Row(header ? h - 2 : h);
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = UiKit.PanelLight;
            row.style.paddingLeft = 6; row.style.paddingRight = 6;
            if (player)
            {
                row.name = "standings-player-row";
                row.style.backgroundColor = PlayerRowColor;
                row.style.borderLeftWidth = 3; row.style.borderLeftColor = UiKit.Accent;
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var l = UiKit.Lbl(cells[i], header ? size - 1 : size, header ? UiKit.TextDim : player ? UiKit.Text : (i == 1 ? UiKit.Text : UiKit.TextDim), player);
                l.style.flexBasis = 0; l.style.flexGrow = Cols[i][0]; l.style.flexShrink = 1;
                l.style.minWidth = Cols[i][1] * size / 13f;
                if (i == 1) UiKit.Ellipsis(l);
                else if (i == cells.Length - 1) l.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(l);
            }
            return row;
        }
    }

    /// <summary>Annual finals qualification table (rank, club, points per split, total, qualified), player club highlighted.</summary>
    public static class QualificationView
    {
        /// <summary>Builds the table; <paramref name="splitNames"/> are the column headers (calendar order).</summary>
        public static VisualElement Build(IReadOnlyList<QualificationRow> rows, IReadOnlyList<string> splitNames, float rowHeight, int fontSize)
        {
            var table = new VisualElement();
            table.name = "qualification-table";
            var head = new List<string> { UiStrings.ColRank, UiStrings.ColClub };
            head.AddRange(splitNames);
            head.Add(UiStrings.ColTotalPoints); head.Add(UiStrings.ColQualified);
            table.Add(Row(true, rowHeight, fontSize, false, false, head));
            foreach (var r in rows)
            {
                var cells = new List<string> { string.Format(UiStrings.RankFormat, r.Rank), r.ClubName };
                for (int i = 0; i < splitNames.Count; i++)
                    cells.Add(i < r.SplitPoints.Count ? r.SplitPoints[i].ToString(CultureInfo.InvariantCulture) : UiStrings.NoValue);
                cells.Add(r.Points.ToString(CultureInfo.InvariantCulture));
                // Nothing is decided before a split is finished; until every split is done the top places are only provisional.
                cells.Add(!r.HasPlayed || !r.Qualified ? UiStrings.NoValue
                    : r.SplitPoints.Count < splitNames.Count ? UiStrings.QualifiedProvisional : UiStrings.Qualified);
                table.Add(Row(false, rowHeight, fontSize, r.IsPlayer, r.HasPlayed && r.Qualified, cells));
            }
            return table;
        }

        private static VisualElement Row(bool header, float h, int size, bool player, bool qualified, List<string> cells)
        {
            var row = UiKit.Row(header ? h - 2 : h);
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = UiKit.PanelLight;
            row.style.paddingLeft = 6;
            if (player)
            {
                row.name = "qualification-player-row";
                row.style.backgroundColor = StandingsView.PlayerRowColor;
                row.style.borderLeftWidth = 3; row.style.borderLeftColor = UiKit.Accent;
            }
            int last = cells.Count - 1;
            for (int i = 0; i < cells.Count; i++)
            {
                Color c = header ? UiKit.TextDim : i == last && qualified ? UiKit.Good : player || i == 1 || i == last - 1 ? UiKit.Text : UiKit.TextDim;
                var l = UiKit.Lbl(cells[i], header ? size - 1 : size, c, player || (!header && i == last - 1));
                l.style.flexBasis = 0; l.style.flexShrink = 1;
                if (i == 1) { l.style.flexGrow = 3; UiKit.Ellipsis(l); }
                else { l.style.flexGrow = i == 0 ? 1.1f : 1f; l.style.minWidth = (i == 0 ? 56 : 44) * size / 13f; }
                row.Add(l);
            }
            return row;
        }
    }

    /// <summary>
    /// Full-screen standings, reachable from the main screen; "back" pops it. Left: the latest split's regular-season table;
    /// right: this year's finals qualification table.
    /// </summary>
    public static class StandingsScreen
    {
        /// <summary>Builds the screen.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            UiKit.Pad(root, 8);
            var title = UiKit.Row(44);
            title.style.flexShrink = 0;
            title.Add(UiKit.Lbl(UiStrings.StandingsTitle, 22, UiKit.Text, true));
            var countdown = UiKit.Ellipsis(UiKit.Lbl(EventTexts.Countdown(s.NextEvent()), 14, UiKit.TextDim));
            countdown.style.marginLeft = 16; title.Add(countdown);
            title.Add(UiKit.Spacer());
            title.Add(UiKit.Btn(UiStrings.Back, ui.Back, UiKit.PanelLight, true, 15));
            root.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);
            var body = scroll.contentContainer;
            body.style.flexDirection = FlexDirection.Row; body.style.alignItems = Align.FlexStart;

            var left = UiKit.Block(UiKit.Panel, 6);
            UiKit.Pad(left, 12); left.style.flexGrow = 1; left.style.flexBasis = 0; left.style.marginRight = 8;
            var split = EventTexts.LatestSplit(s.Season);
            var sub = split != null ? string.Format(UiStrings.StandingsOfFormat, split.Year, UiStrings.EventName(split.Def.nameKey)) : UiStrings.NoSplitYet;
            var st = UiKit.Ellipsis(UiKit.Lbl(sub, 15, UiKit.Accent, true)); st.style.marginBottom = 6; left.Add(st);
            var rows = split != null ? (IReadOnlyList<StandingsRow>)split.StandingsRows() : s.GetStandings();
            left.Add(StandingsView.Build(rows, 34, 15));
            var note = UiKit.Lbl(string.Format(UiStrings.StandingsNoteFormat, rows.Count), 12, UiKit.TextDim);
            note.style.marginTop = 8; note.style.whiteSpace = WhiteSpace.Normal; left.Add(note);
            body.Add(left);

            var right = UiKit.Block(UiKit.Panel, 6);
            UiKit.Pad(right, 12); right.style.flexGrow = 1; right.style.flexBasis = 0;
            var qt = UiKit.Lbl(string.Format(UiStrings.YearTitleFormat, s.Season?.Year ?? 1, UiStrings.QualificationTitle), 15, UiKit.Accent, true);
            qt.style.marginBottom = 6; right.Add(qt);
            right.Add(QualificationView.Build(s.GetQualification(), EventTexts.SplitNames(s.MatchConfig), 34, 15));
            if (s.MatchConfig != null)
            {
                var qn = UiKit.Lbl(EventTexts.QualificationNote(s.MatchConfig.format), 12, UiKit.TextDim);
                qn.style.marginTop = 8; qn.style.whiteSpace = WhiteSpace.Normal; right.Add(qn);
            }
            body.Add(right);
            EventLayout.NoVerticalShrink(body);
        }
    }
}
