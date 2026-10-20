using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Segments;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// The main screen: header (team triangle, chemistry, action points, segment), one card per player with his
    /// assignment controls and preview, and a bottom bar (message, sponsorship, offers, resolve). Renders
    /// <see cref="GameSession"/> data and <see cref="SegmentRunner.Preview"/> only.
    /// </summary>
    public static class MainScreen
    {
        /// <summary>Builds the screen into ui.Root.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            UiKit.Pad(root, 8);

            var pv = s.Preview();
            var previews = new Dictionary<long, PlayerPreview>();
            foreach (var pp in pv.Players) previews[pp.PlayerId] = pp;
            var team = s.GetTeamData();
            if (!ui.CardsOnly && !string.IsNullOrEmpty(ui.Banner))
            {
                var banner = UiKit.Block(UiKit.Accent, 4);
                banner.name = "year-banner";
                banner.style.height = 34; banner.style.flexShrink = 0; banner.style.marginBottom = 6; banner.style.justifyContent = Justify.Center;
                banner.style.paddingLeft = 12;
                banner.Add(UiKit.Ellipsis(UiKit.Lbl(ui.Banner, 17, UiKit.Text, true)));
                root.Add(banner);
            }
            if (!ui.CardsOnly) root.Add(BuildHeader(ui, team, pv));

            // Cards live in a ScrollView: when the window is shorter than the cards the area scrolls instead of
            // squeezing header / bottom bar (fixed regions never shrink).
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "cards-scroll";
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.flexBasis = 0; scroll.style.minHeight = 0;
            var grid = scroll.contentContainer;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.alignContent = Align.FlexStart;
            root.Add(scroll);
            int n = 0;
            foreach (var card in s.GetRosterData())
            {
                if (n++ >= ui.MaxCards) break;
                previews.TryGetValue(card.Id, out var pp);
                grid.Add(BuildCard(ui, card, pp, team.MaxPerPlayer));
            }
            if (!ui.CardsOnly) root.Add(BuildBottomBar(ui, pv));
        }

        private static VisualElement BuildHeader(GameUi ui, TeamData t, SegmentPreview pv)
        {
            var bar = UiKit.Row(64);
            bar.style.marginBottom = 6; bar.style.flexShrink = 0;
            bar.Add(StatBlock(UiStrings.Strength, UiKit.F1(t.Strength), UiKit.Scale(t.Strength), 84));
            bar.Add(StatBlock(UiStrings.Popularity, UiKit.F1(t.Popularity), UiKit.Scale(t.Popularity), 84));
            bar.Add(StatBlock(UiStrings.Funds, UiKit.Yuan(t.Funds), t.Funds < 0 ? UiKit.Bad : UiKit.Good, 175));
            bar.Add(StatBlock(UiStrings.Chemistry, UiKit.F1(t.Chemistry), UiKit.Scale(t.Chemistry), 84));
            bar.Add(StatBlock(UiStrings.ActionPoints, string.Format(UiStrings.PairFormat, pv.PointsRemaining, t.ActionPoints),
                pv.PointsRemaining > 0 ? UiKit.Accent : UiKit.TextDim, 100));
            var seg = UiKit.Block(UiKit.PanelLight);
            seg.style.flexBasis = 250; seg.style.flexGrow = 1f; seg.style.flexShrink = 1; seg.style.minWidth = 0; seg.style.overflow = Overflow.Hidden; seg.style.height = Length.Percent(100); seg.style.marginRight = 6;
            seg.style.justifyContent = Justify.Center; UiKit.Pad(seg, 8);
            var season = ui.Session.Season;
            string segText = season != null
                ? EventTexts.YearSegment(season.Year, season.SegmentIndexInYear + 1, season.SegmentsPerYear)
                : string.Format(UiStrings.SegmentFormat, t.SegmentNumber);
            var segLabel = UiKit.Ellipsis(UiKit.Lbl(segText, 17, UiKit.Text, true));
            segLabel.name = "year-segment";
            seg.Add(segLabel);
            seg.Add(UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.IncomeLineFormat, UiKit.Yuan(pv.LivestreamIncome), UiKit.Yuan(pv.BusinessIncome)), 12, UiKit.TextDim)));
            bar.Add(seg);
            if (ui.Session.Tournament != null) bar.Add(BuildMatchBlock(ui));
            return bar;
        }

        /// <summary>Countdown to the next event, standings button, tactic selector with the counter cycle (Story 004).</summary>
        private static VisualElement BuildMatchBlock(GameUi ui)
        {
            var s = ui.Session;
            var block = UiKit.Block(UiKit.Panel);
            block.name = "match-block";
            block.style.flexBasis = 430; block.style.flexGrow = 2.5f; block.style.flexShrink = 1; block.style.minWidth = 0; block.style.overflow = Overflow.Hidden; block.style.height = Length.Percent(100);
            block.style.justifyContent = Justify.SpaceAround;
            block.style.paddingLeft = block.style.paddingRight = 8; block.style.paddingTop = block.style.paddingBottom = 4;

            var r1 = UiKit.Row(22);
            var countdown = UiKit.Ellipsis(UiKit.Lbl(EventTexts.Countdown(s.NextEvent()), 14, UiKit.Warn, true));
            countdown.name = "countdown";
            r1.Add(countdown);
            r1.Add(UiKit.Spacer());
            // Rank of the latest split's regular season; just "积分榜" before the first one.
            var split = EventTexts.LatestSplit(s.Season);
            int rank = 0;
            if (split != null) foreach (var row in split.StandingsRows()) if (row.IsPlayer) rank = row.Rank;
            var sb = UiKit.Btn(rank > 0 ? string.Format(UiStrings.StandingsButtonFormat, rank) : UiStrings.StandingsButton, ui.OpenStandings, UiKit.PanelLight, true, 12);
            sb.name = "standings-button";
            sb.style.height = 20; sb.style.marginRight = 0;
            r1.Add(sb);
            block.Add(r1);

            var r2 = UiKit.Row(26);
            var tl = UiKit.Lbl(UiStrings.OurTactic, 12, UiKit.TextDim); tl.style.marginRight = 6; tl.style.flexShrink = 0;
            r2.Add(tl);
            var current = s.CurrentTactic;
            foreach (var t in MatchTactics.All)
            {
                var tt = t;
                bool sel = t == current;
                var b = UiKit.Btn(UiStrings.TacticName(t), () => ui.SetTactic(tt), sel ? UiKit.Accent : UiKit.PanelLight, true, 13);
                b.name = "tactic-" + MatchTactics.Key(t);
                b.style.height = 24;
                if (sel) { b.style.unityFontStyleAndWeight = FontStyle.Bold; b.style.borderBottomWidth = 2; b.style.borderBottomColor = UiKit.Text; }
                r2.Add(b);
            }
            r2.Add(UiKit.Spacer());
            var hint = UiKit.Ellipsis(UiKit.Lbl(CounterHint(s.MatchConfig), 11, UiKit.TextDim));
            r2.Add(hint);
            block.Add(r2);
            return block;
        }

        /// <summary>"入侵克发育 · 发育克团战 · 团战克入侵" built from the config counter table (data, not code).</summary>
        public static string CounterHint(MatchConfig config)
        {
            var parts = new List<string>();
            if (config?.tactic?.counters != null)
                foreach (var c in config.tactic.counters)
                    parts.Add(string.Format(UiStrings.CounterFormat, UiStrings.TacticName(c.tactic), UiStrings.TacticName(c.beats)));
            return string.Join(UiStrings.CounterJoin, parts);
        }

        private static VisualElement StatBlock(string label, string value, Color valueColor, float width)
        {
            var b = UiKit.Block(UiKit.Panel);
            // Flexible: width is only the preferred basis; blocks grow / shrink with the window.
            b.style.flexBasis = width; b.style.flexGrow = width > 150 ? 1.6f : 1; b.style.minWidth = width > 150 ? 150 : 0; b.style.flexShrink = 1; b.style.overflow = Overflow.Hidden;
            b.style.height = Length.Percent(100); b.style.marginRight = 6;
            b.style.justifyContent = Justify.Center; UiKit.Pad(b, 8);
            b.Add(UiKit.Ellipsis(UiKit.Lbl(label, 12, UiKit.TextDim)));
            b.Add(UiKit.Ellipsis(UiKit.Lbl(value, 22, valueColor, true)));
            return b;
        }

        private static VisualElement BuildCard(GameUi ui, PlayerCardData c, PlayerPreview pp, int maxPerPlayer)
        {
            var card = UiKit.Block(UiKit.Panel, 6);
            card.name = "card-" + c.Id;
            card.style.flexGrow = 1; card.style.flexBasis = Length.Percent(ui.CardsOnly ? 45f : 30f);
            card.style.height = 286;
            card.style.flexShrink = 0;
            card.style.marginRight = 6; card.style.marginBottom = 6;
            UiKit.Pad(card, 8);
            card.style.borderLeftWidth = 4;
            card.style.borderLeftColor = c.IsStarter ? UiKit.Accent : UiKit.TextDim;

            var r1 = UiKit.Row(24);
            r1.Add(UiKit.Lbl(c.Name, 17, UiKit.Text, true));
            r1.Add(Tag(c.LaneName, UiKit.PanelLight));
            if (ui.Session.Tournament != null)
            {
                long tid = c.Id;
                var tog = UiKit.Btn(c.IsStarter ? UiStrings.StarterToggleOff : UiStrings.BenchToggleOn, () => ui.ToggleStarter(tid),
                    c.IsStarter ? UiKit.Accent : UiKit.Warn, true, 11);
                tog.name = "starter-toggle-" + c.Id;
                tog.style.height = 20; tog.style.marginLeft = 6; tog.style.paddingLeft = tog.style.paddingRight = 6;
                if (!c.IsStarter) tog.style.color = UiKit.Background;
                r1.Add(tog);
            }
            else r1.Add(Tag(c.IsStarter ? UiStrings.Starter : UiStrings.Bench, c.IsStarter ? UiKit.Accent : UiKit.PanelLight));
            bool onLeave = ui.IsOnLeave(c.Id);
            if (onLeave) { var lt = Tag(UiStrings.OnLeaveTag, UiKit.Warn); lt.name = "on-leave-" + c.Id; r1.Add(lt); }
            r1.Add(UiKit.Spacer());
            r1.Add(UiKit.Lbl(string.Format(UiStrings.AgeFormat, c.Age), 13, UiKit.TextDim));
            card.Add(r1);

            var r2 = UiKit.Row(22);
            r2.Add(UiKit.Lbl(string.Join(UiStrings.TraitSeparator, c.TraitNames), 13, UiKit.Warn));
            r2.Add(UiKit.Spacer());
            r2.Add(Gap(UiKit.Lbl(UiStrings.PotentialEstimate, 12, UiKit.TextDim)));
            r2.Add(UiKit.Lbl(string.Format(UiStrings.PotentialRangeFormat, c.PotentialLow, c.PotentialHigh), 14, UiKit.Accent, true));
            card.Add(r2);

            var r3 = UiKit.Row(26);
            r3.style.marginTop = 2;
            r3.Add(Chip(UiStrings.Mood, c.Mood, false));
            r3.Add(Chip(UiStrings.Stress, c.Stress, true));
            r3.Add(Chip(UiStrings.Looks, c.Looks, false));
            r3.Add(Chip(UiStrings.PopularityShort, c.Popularity, false));
            card.Add(r3);

            var r4 = UiKit.Row(22);
            r4.Add(Gap(UiKit.Lbl(UiStrings.Salary, 12, UiKit.TextDim)));
            r4.Add(UiKit.Lbl(string.Format(UiStrings.SalaryPerYearFormat, UiKit.N0(c.Salary)), 13, UiKit.Text));
            r4.Add(UiKit.Spacer());
            r4.Add(Gap(UiKit.Lbl(UiStrings.ContractLeft, 12, UiKit.TextDim)));
            r4.Add(UiKit.Lbl(string.Format(UiStrings.ContractYearsFormat, c.ContractYearsLeft), 13,
                c.ContractYearsLeft <= 1 ? UiKit.Warn : UiKit.Text));
            card.Add(r4);

            var dims = new VisualElement();
            dims.style.flexDirection = FlexDirection.Row; dims.style.flexWrap = Wrap.Wrap; dims.style.marginTop = 4;
            for (int i = 0; i < c.DimensionNames.Length; i++)
            {
                float gain = pp != null && pp.TrainingGains != null && i < pp.TrainingGains.Length ? pp.TrainingGains[i] : 0f;
                int dim = i; long id = c.Id;
                dims.Add(DimBar(c.DimensionNames[i], c.DimensionValues[i], gain, () => ui.Train(id, dim)));
            }
            card.Add(dims);

            // Assignment area
            var a1 = UiKit.Row(22);
            a1.style.marginTop = 2;
            a1.Add(UiKit.Lbl(ActionsText(pp), 13, pp != null && pp.PointsUsed > 0 ? UiKit.Text : UiKit.TextDim));
            a1.Add(UiKit.Spacer());
            int used = pp != null ? pp.PointsUsed : 0;
            a1.Add(UiKit.Lbl(string.Format(UiStrings.PlayerPointsFormat, used, maxPerPlayer), 13,
                used >= maxPerPlayer ? UiKit.Warn : UiKit.Accent, true));
            card.Add(a1);

            var a2 = UiKit.Row(28);
            long pid = c.Id;
            // On leave (Story 006 choice): no action point may go to him; the buttons are disabled and the reason shown.
            a2.Add(UiKit.Btn(UiStrings.Stream, () => ui.Stream(pid), UiKit.Accent, !onLeave));
            a2.Add(UiKit.Btn(UiStrings.Rest, () => ui.Rest(pid), UiKit.Good, !onLeave));
            a2.Add(UiKit.Btn(UiStrings.Undo, () => ui.Undo(pid), UiKit.PanelLight, used > 0));
            a2.Add(UiKit.Spacer());
            a2.Add(UiKit.Ellipsis(UiKit.Lbl(onLeave ? UiStrings.OnLeaveCardNote : UiStrings.ClickDimToTrain, 11, onLeave ? UiKit.Warn : UiKit.TextDim, onLeave)));
            card.Add(a2);

            if (pp != null)
            {
                var a3 = UiKit.Row(20);
                a3.Add(UiKit.Lbl(string.Format(UiStrings.PreviewFormat, UiKit.Delta(pp.StressDelta), UiKit.Delta(pp.MoodDelta),
                    UiKit.Yuan(pp.LivestreamIncome)), 12, UiKit.TextDim));
                card.Add(a3);
            }
            return card;
        }

        private static string ActionsText(PlayerPreview pp)
        {
            if (pp == null || pp.PointsUsed == 0) return UiStrings.NoActions;
            var parts = new List<string>();
            if (pp.TrainPoints > 0) parts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Train, pp.TrainPoints));
            if (pp.StreamPoints > 0) parts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Stream, pp.StreamPoints));
            if (pp.RestPoints > 0) parts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Rest, pp.RestPoints));
            if (pp.BusinessPoints > 0) parts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Business, pp.BusinessPoints));
            return string.Join("  ", parts);
        }

        private static Label Gap(Label l) { l.style.marginRight = 5; return l; }

        private static VisualElement Tag(string text, Color bg)
        {
            var t = UiKit.Block(bg, 3);
            t.style.marginLeft = 6; t.style.paddingLeft = t.style.paddingRight = 6; t.style.height = 18;
            t.style.justifyContent = Justify.Center;
            t.Add(UiKit.Lbl(text, 11, UiKit.Text));
            return t;
        }

        private static VisualElement Chip(string label, float value, bool highBad)
        {
            var chip = UiKit.Block(UiKit.PanelLight, 3);
            chip.style.flexDirection = FlexDirection.Row; chip.style.alignItems = Align.Center;
            chip.style.flexGrow = 1; chip.style.flexBasis = 0; chip.style.height = 24; chip.style.marginRight = 4;
            chip.style.paddingLeft = chip.style.paddingRight = 6;
            chip.Add(UiKit.Lbl(label, 12, UiKit.TextDim));
            chip.Add(UiKit.Spacer());
            chip.Add(UiKit.Lbl(UiKit.Int(value), 14, UiKit.Scale(highBad ? 100f - value : value), true));
            return chip;
        }

        private static VisualElement DimBar(string name, float value, float gain, System.Action onClick)
        {
            var cell = UiKit.Block(UiKit.PanelLight, 3);
            cell.style.width = Length.Percent(49); cell.style.height = 22; cell.style.marginBottom = 3;
            cell.style.marginRight = Length.Percent(1);
            cell.style.flexDirection = FlexDirection.Row; cell.style.alignItems = Align.Center;
            cell.style.overflow = Overflow.Hidden;
            cell.AddManipulator(new Clickable(onClick));
            var fill = new VisualElement();
            fill.style.position = Position.Absolute; fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = Length.Percent(Mathf.Clamp(value, 0f, 100f));
            var fc = UiKit.Scale(value); fc.a = 0.35f; fill.style.backgroundColor = fc;
            cell.Add(fill);
            var l = UiKit.Lbl(name, 12, UiKit.Text); l.style.marginLeft = 6; cell.Add(l);
            cell.Add(UiKit.Spacer());
            if (gain > 0.005f)
            {
                var g = UiKit.Lbl(UiKit.Delta(gain), 12, UiKit.Good, true);
                g.style.backgroundColor = UiKit.LabelPill; g.style.paddingLeft = g.style.paddingRight = 3;
                g.style.borderTopLeftRadius = g.style.borderTopRightRadius = g.style.borderBottomLeftRadius = g.style.borderBottomRightRadius = 3;
                g.style.marginRight = 6; cell.Add(g);
            }
            var v = UiKit.Lbl(UiKit.Int(value), 13, UiKit.Text, true); v.style.marginRight = 6; cell.Add(v);
            return cell;
        }

        private static VisualElement BuildBottomBar(GameUi ui, SegmentPreview pv)
        {
            var bar = UiKit.Row(44);
            bar.name = "bottom-bar"; bar.style.flexShrink = 0;
            bar.style.backgroundColor = UiKit.Panel; bar.style.paddingLeft = bar.style.paddingRight = 8;
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = 4;
            // A plan that cannot be resolved (e.g. an incomplete lineup) shows why, unless a fresh refusal is on screen.
            bool blocked = !pv.Error.IsNone;
            string text = blocked && !ui.MessageIsError ? ui.ErrorText(pv.Error) : ui.Message;
            bool isError = ui.MessageIsError || blocked;
            var msg = UiKit.Lbl(text, 13, isError ? UiKit.Bad : UiKit.TextDim, isError);
            msg.name = "message";
            msg.style.flexGrow = 1; msg.style.flexShrink = 1; msg.style.minWidth = 0;
            msg.style.whiteSpace = WhiteSpace.NoWrap; msg.style.textOverflow = TextOverflow.Ellipsis;
            bar.Add(msg);

            int unanswered = ui.Session.UnansweredOffers().Count;
            if (unanswered > 0 && !ui.OfferPopupOpen)
                bar.Add(UiKit.Btn(string.Format(UiStrings.OffersBadgeFormat, unanswered), ui.OpenOffers, UiKit.Warn));
            // Choice events: "待决定 N" while any is unanswered, else a quiet button to review / change an answer.
            if (!ui.ChoicePopupOpen && ui.Session.PendingChoices.Count > 0)
            {
                int open = ui.UnansweredChoices().Count;
                var cb = UiKit.Btn(open > 0 ? string.Format(UiStrings.ChoicesBadgeFormat, open) : UiStrings.ChoicesReview, ui.OpenChoices,
                    open > 0 ? UiKit.Warn : UiKit.PanelLight);
                cb.name = "choices-badge";
                bar.Add(cb);
            }

            var info = UiKit.Lbl(string.Format(UiStrings.SponsorInfoFormat, pv.SponsorPoints, pv.SponsorAttemptsRemaining,
                pv.FreeContractSlots, UiKit.Pct(pv.SponsorChanceBeforeNoise)), 12, UiKit.TextDim);
            info.style.marginRight = 8; info.style.flexShrink = 0;
            bar.Add(info);
            bar.Add(UiKit.Btn(UiStrings.Sponsor, ui.Sponsor, UiKit.Accent));
            bar.Add(UiKit.Btn(UiStrings.UndoSponsor, ui.UndoSponsor, UiKit.PanelLight, pv.SponsorPoints > 0));
            bar.Add(UiKit.Btn(UiStrings.ClearPlan, ui.ClearPlan, UiKit.PanelLight, pv.PointsUsed > 0));
            bar.Add(UiKit.Btn(UiStrings.Resolve, ui.Resolve, UiKit.Good, pv.Error.IsNone, 14));
            return bar;
        }
    }
}
