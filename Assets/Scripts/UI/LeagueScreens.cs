using System.Collections.Generic;
using System.Globalization;
using EsportsManager.Leagues;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// A three-axis radar (early / team fight / late) on the ABSOLUTE 0-100 scale, drawn with Painter2D and no numbers (league.md:
    /// the same scale for every club, shape only). Painter2D / generateVisualContent: UI Toolkit vector API (Unity 2022.1+;
    /// verify against the 6.6 docs if it misbehaves).
    /// </summary>
    public sealed class PhaseRadar : VisualElement
    {
        /// <summary>Rating at the radar centre; the rim is 100 (the same fixed scale for every club, so shapes compare).</summary>
        public const float ScaleMin = 30f;
        private readonly float[] _v;
        private readonly Color _fill;

        /// <summary>Creates a radar of <paramref name="size"/> px for the three values (0-100).</summary>
        public PhaseRadar(float[] values, float size, Color fill)
        {
            _v = values; _fill = fill;
            style.width = size; style.height = size; style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        private static Vector2 Axis(int k, float r, Vector2 c)
        {
            float a = Mathf.Deg2Rad * (-90f + 120f * k);
            return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            var c = rect.center;
            float R = Mathf.Min(rect.width, rect.height) * 0.46f;
            var p = ctx.painter2D;
            p.lineWidth = 1f;
            p.strokeColor = new Color(1f, 1f, 1f, 0.18f);
            foreach (float f in new[] { 0.5f, 1f })
            {
                p.BeginPath();
                p.MoveTo(Axis(0, R * f, c)); p.LineTo(Axis(1, R * f, c)); p.LineTo(Axis(2, R * f, c));
                p.ClosePath(); p.Stroke();
            }
            p.fillColor = _fill;
            p.strokeColor = new Color(_fill.r, _fill.g, _fill.b, 1f);
            p.BeginPath();
            for (int k = 0; k < 3; k++)
            {
                var pt = Axis(k, R * Mathf.Clamp01((_v[k] - ScaleMin) / (100f - ScaleMin)), c);
                if (k == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath(); p.Fill(); p.Stroke();
        }
    }

    /// <summary>Shared league widgets: player rows with a radar, news lines, style tags.</summary>
    public static class LeagueWidgets
    {
        /// <summary>Style tag text "名字 · 后期" (name only without a tag).</summary>
        public static string WithTag(string name, ClubStyle? tag) => tag == null ? name : string.Format(UiStrings.StyleTagFormat, name, UiStrings.StyleName(tag.Value));

        /// <summary>One scouted player: radar + name / lane / age / potential range.</summary>
        public static VisualElement PlayerRow(ScoutedPlayerData p, float radar, int font)
        {
            var r = UiKit.Row(radar + 4);
            r.style.marginBottom = 2;
            r.Add(new PhaseRadar(p.Phases, radar, p.IsStarter ? new Color(0.35f, 0.63f, 0.94f, 0.55f) : new Color(0.6f, 0.63f, 0.7f, 0.4f)));
            var col = new VisualElement(); col.style.marginLeft = 6; col.style.flexGrow = 1; col.style.minWidth = 0;
            var top = UiKit.Ellipsis(UiKit.Lbl(p.Name + UiStrings.TraitSeparator + p.LaneName, font, p.IsStarter ? UiKit.Text : UiKit.TextDim, p.IsStarter));
            col.Add(top);
            string sub = string.Format(UiStrings.AgeFormat, p.Age) + UiStrings.PartSeparator + UiStrings.PotentialEstimate + " "
                + string.Format(UiStrings.PotentialRangeFormat, p.PotentialLow, p.PotentialHigh) + (p.IsStarter ? "" : UiStrings.PartSeparator + UiStrings.Bench);
            col.Add(UiKit.Ellipsis(UiKit.Lbl(sub, font - 2, UiKit.TextDim)));
            r.Add(col);
            return r;
        }

        /// <summary>Formats one news line.</summary>
        public static string NewsText(GameSession s, NewsItem n)
        {
            string when = n.Year == 0 ? UiStrings.NewsWarmUp : string.Format(UiStrings.NewsYearFormat, n.Year);
            string body;
            switch (n.Kind)
            {
                case NewsKind.StyleSwitch: body = string.Format(UiStrings.NewsStyleFormat, n.ClubName, UiStrings.StyleName(n.From), UiStrings.StyleName(n.To)); break;
                case NewsKind.Signing: body = string.Format(UiStrings.NewsSigningFormat, n.ClubName, n.PlayerName); break;
                case NewsKind.FireSale: body = string.Format(UiStrings.NewsFireSaleFormat, n.ClubName, n.PlayerName, UiKit.N0(n.Amount)); break;
                case NewsKind.Loan: body = string.Format(UiStrings.NewsLoanFormat, n.ClubName, UiKit.N0(n.Amount)); break;
                case NewsKind.Specialty: body = string.Format(UiStrings.NewsSpecialtyFormat, n.ClubName, n.PlayerName, s.SpecialtyName(n.Key)); break;
                default: body = string.Format(UiStrings.NewsChampionFormat, n.ClubName, UiStrings.EventName(n.Key)); break;
            }
            return when + UiStrings.PartSeparator + body;
        }

        /// <summary>The news panel (newest first).</summary>
        public static VisualElement NewsPanel(GameSession s, int font)
        {
            var box = UiKit.Block(UiKit.Panel, 6);
            box.name = "news-panel";
            UiKit.Pad(box, 12);
            var t = UiKit.Lbl(UiStrings.NewsTitle, font + 2, UiKit.Accent, true); t.style.marginBottom = 6; box.Add(t);
            var news = LeagueViews.News(s);
            if (news.Count == 0) box.Add(UiKit.Lbl(UiStrings.NewsEmpty, font, UiKit.TextDim));
            foreach (var n in news)
            {
                var l = UiKit.Lbl(NewsText(s, n), font, n.ClubId == s.World.PlayerClubId ? UiKit.Text : UiKit.TextDim);
                l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 3;
                box.Add(l);
            }
            return box;
        }

        /// <summary>
        /// Opponent panel for the prep screen: club + style tag, a chip row to switch club, starters / bench with radars, last 5
        /// tactics, last meeting phase gaps.
        /// </summary>
        public static VisualElement OpponentPanel(GameUi ui, VisualElement parent)
        {
            var s = ui.Session;
            int club = ui.OpponentIndex >= 0 ? ui.OpponentIndex : LeagueViews.DefaultOpponent(s);
            var info = LeagueViews.OpponentInfo(s, club);
            var sec = EventLayout.Section(parent, UiStrings.OpponentTitle);
            sec.name = "opponent-panel";
            if (info == null) return sec;
            var head = UiKit.Ellipsis(UiKit.Lbl(WithTag(info.ClubName, info.StyleTag), 16, UiKit.Text, true));
            sec.Add(head);
            var chips = UiKit.Row(28);
            chips.style.flexWrap = Wrap.Wrap; chips.style.height = StyleKeyword.Auto; chips.style.marginTop = 4; chips.style.marginBottom = 4;
            for (int c = 0; c < s.World.Clubs.Count; c++)
            {
                if (c == s.World.PlayerClubId) continue;
                int cc = c;
                var b = UiKit.Btn(s.World.Clubs[c].Name, () => ui.ShowOpponent(cc), c == club ? UiKit.Accent : UiKit.PanelLight, true, 11);
                b.style.height = 22; b.style.marginBottom = 3; b.style.maxWidth = 120;
                chips.Add(b);
            }
            sec.Add(chips);
            var tac = new List<string>();
            foreach (var t in info.RecentTactics) tac.Add(UiStrings.TacticName(t));
            sec.Add(WrapLbl(UiStrings.OpponentRecentTactics + (tac.Count == 0 ? UiStrings.OpponentNoTactics : string.Join(" / ", tac)), 13, UiKit.Text));
            string gap = UiStrings.OpponentNeverMet;
            if (info.LastMeetingGap != null)
            {
                var parts = new List<string>();
                for (int k = 0; k < 3; k++) parts.Add(string.Format(UiStrings.OpponentPhaseFormat, UiStrings.PhaseName(k), info.LastMeetingGap[k].ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)));
                gap = string.Join("  ", parts) + (info.LastMeetingWon ? UiStrings.OpponentWon : UiStrings.OpponentLost);
            }
            sec.Add(WrapLbl(UiStrings.OpponentLastMeeting + gap, 13, UiKit.Text));
            var note = WrapLbl(UiStrings.OpponentRadarNote, 11, UiKit.TextDim); note.style.marginBottom = 4; sec.Add(note);
            // Compact: the five starters in a two-column grid (all visible at 1280x720), the bench as one line.
            var grid = new VisualElement();
            grid.name = "opponent-starters";
            grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            var bench = new List<string>();
            foreach (var p in info.Players)
            {
                if (!p.IsStarter) { bench.Add(p.Name + UiStrings.TraitSeparator + p.LaneName); continue; }
                var cell = PlayerRow(p, 34, 12);
                cell.style.width = Length.Percent(50); cell.style.flexShrink = 0;
                grid.Add(cell);
            }
            sec.Add(grid);
            if (bench.Count > 0) sec.Add(WrapLbl(string.Format(UiStrings.OpponentBenchFormat, string.Join(UiStrings.PartSeparator, bench)), 12, UiKit.TextDim));
            return sec;
        }

        private static Label WrapLbl(string text, int size, Color c)
        {
            var l = UiKit.Lbl(text, size, c);
            l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 3;
            return l;
        }
    }

    /// <summary>
    /// New game (league.md 预热与三份聘书, D3): the three offers side by side (funds, roster radars + potential ranges, last year's
    /// place, style, expiring contracts), pick one, edit the name, confirm. Replaces the old naming-only screen.
    /// </summary>
    public static class JobOfferScreen
    {
        /// <summary>Builds the screen.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            UiKit.Pad(root, 8);
            var title = UiKit.Row(40);
            title.style.flexShrink = 0;
            title.Add(UiKit.Lbl(UiStrings.OffersTitle, 22, UiKit.Text, true));
            var sub = UiKit.Ellipsis(UiKit.Lbl(UiStrings.OffersSubtitle, 14, UiKit.TextDim)); sub.style.marginLeft = 14; title.Add(sub);
            root.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "offers-scroll";
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);
            var row = scroll.contentContainer;
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.FlexStart;
            var offers = LeagueViews.JobOffers(s);
            foreach (var o in offers) row.Add(Card(ui, o));

            var bar = UiKit.Block(UiKit.Panel, 6);
            bar.style.flexDirection = FlexDirection.Row; bar.style.alignItems = Align.Center; bar.style.flexShrink = 0;
            bar.style.height = 56; bar.style.marginTop = 6; UiKit.Pad(bar, 8);
            int max = s.ClubRules != null ? s.ClubRules.nameMaxLength : 16;
            var sel = ui.SelectedOffer >= 0 && ui.SelectedOffer < offers.Count ? offers[ui.SelectedOffer] : null;
            var prompt = UiKit.Lbl(string.Format(UiStrings.OfferNamePrompt, max), 13, UiKit.TextDim);
            prompt.style.marginRight = 8; prompt.style.flexShrink = 0; bar.Add(prompt);
            var field = new TextField { value = ui.OfferName ?? sel?.ClubName ?? "", maxLength = max };
            field.name = "club-name";
            field.style.width = 260; field.style.height = 34; field.style.fontSize = 16; field.style.flexShrink = 0;
            field.Query<VisualElement>().ForEach(e =>
            {
                if (e == field) return;
                e.style.backgroundColor = UiKit.PanelLight; e.style.color = UiKit.Text; e.style.unityTextAlign = TextAnchor.MiddleLeft;
                e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 0;
            });
            field.style.borderTopWidth = field.style.borderBottomWidth = field.style.borderLeftWidth = field.style.borderRightWidth = 2;
            field.style.borderTopColor = field.style.borderBottomColor = field.style.borderLeftColor = field.style.borderRightColor = UiKit.Accent;
            field.SetEnabled(sel != null);
            field.RegisterValueChangedCallback(e => ui.OfferName = e.newValue);
            bar.Add(field);
            var note = UiKit.Ellipsis(UiKit.Lbl(sel == null ? UiStrings.OfferPickFirst : UiStrings.OfferTopUpNote, 12, UiKit.TextDim));
            note.style.marginLeft = 10; note.style.flexShrink = 1; note.style.flexGrow = 1; bar.Add(note);
            var ok = UiKit.Btn(UiStrings.OfferConfirm, () => ui.ConfirmOffer(field.value), UiKit.Good, sel != null, 16);
            ok.name = "offer-confirm"; ok.style.height = 36; ok.style.flexShrink = 0;
            bar.Add(ok);
            root.Add(bar);
        }

        private static VisualElement Card(GameUi ui, JobOfferData o)
        {
            bool sel = ui.SelectedOffer == o.Index;
            var card = UiKit.Block(UiKit.Panel, 8);
            card.name = "offer-" + o.Index;
            card.style.flexGrow = 1; card.style.flexBasis = 0; card.style.minWidth = 0; card.style.marginRight = 8;
            UiKit.Pad(card, 12);
            card.style.borderTopWidth = 4; card.style.borderTopColor = sel ? UiKit.Good : UiKit.Accent;
            if (sel) { card.style.borderLeftWidth = card.style.borderRightWidth = card.style.borderBottomWidth = 2; card.style.borderLeftColor = card.style.borderRightColor = card.style.borderBottomColor = UiKit.Good; }
            var head = UiKit.Row(28);
            head.Add(UiKit.Lbl(UiStrings.TierName(o.Tier), 14, UiKit.Warn, true));
            var n = UiKit.Ellipsis(UiKit.Lbl(o.ClubName, 18, UiKit.Text, true)); n.style.marginLeft = 8; n.style.flexShrink = 1; head.Add(n);
            card.Add(head);
            var funds = UiKit.Lbl(UiStrings.OfferFunds + " " + UiKit.Yuan(o.Funds), 15, o.Funds < 0 ? UiKit.Bad : UiKit.Good, true);
            card.Add(funds);
            string meta = (o.LastYearRank > 0 ? string.Format(UiStrings.OfferLastRankFormat, o.LastYearRank) + UiStrings.PartSeparator : "")
                + string.Format(UiStrings.OfferStyleFormat, UiStrings.StyleName(o.Style));
            card.Add(UiKit.Ellipsis(UiKit.Lbl(meta, 13, UiKit.Text)));
            var exp = UiKit.Lbl(string.Format(UiStrings.OfferExpiringFormat, o.ExpiringContracts), 13, o.ExpiringContracts > 0 ? UiKit.Warn : UiKit.TextDim);
            exp.style.marginBottom = 6; card.Add(exp);
            foreach (var p in o.Players) card.Add(LeagueWidgets.PlayerRow(p, 44, 13));
            var b = UiKit.Btn(sel ? UiStrings.OfferPicked : UiStrings.OfferPick, () => ui.SelectOffer(o.Index), sel ? UiKit.Good : UiKit.Accent, true, 14);
            b.name = "offer-pick-" + o.Index; b.style.marginTop = 8; b.style.height = 30;
            card.Add(b);
            return card;
        }
    }
}
