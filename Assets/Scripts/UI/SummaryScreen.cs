using System.Collections.Generic;
using EsportsManager.Segments;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>Settlement panel after a segment: team changes, money lines, sponsorship, offers, per-player changes.</summary>
    public static class SummaryScreen
    {
        /// <summary>Builds the panel into ui.Root from <see cref="GameSession.LastResult"/>.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            var r = s.LastResult;
            var sum = r.Summary;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            UiKit.Pad(root, 8);

            var title = UiKit.Row(44);
            title.Add(UiKit.Lbl(string.Format(UiStrings.SummaryTitleFormat, r.SegmentNumber), 22, UiKit.Text, true));
            var pts = UiKit.Lbl(string.Format(UiStrings.PointsUnusedFormat, sum.PointsUsed, sum.PointsUnused), 13, UiKit.TextDim);
            pts.style.marginLeft = 16; title.Add(pts);
            title.Add(UiKit.Spacer());
            // After an event segment the next step is the event's prep screen (numbers.md 4.18), not the next segment.
            var ev = s.EventPhase == EventPhase.Prepare ? s.CurrentEvent?.Event : null;
            var next = UiKit.Btn(ev != null ? string.Format(UiStrings.NextStepFormat, UiStrings.EventName(ev.Def.nameKey)) : UiStrings.NextSegment,
                ui.NextSegment, UiKit.Good, true, 15);
            next.name = "next-step"; ui.GuardFlowButton(next, true);
            title.Add(next);
            root.Add(title);

            // Both columns scroll inside the window instead of overflowing it (many events / sponsor lines).
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1; body.style.flexShrink = 1; body.style.flexBasis = 0; body.style.minHeight = 0;
            root.Add(body);

            var leftScroll = new ScrollView(ScrollViewMode.Vertical);
            leftScroll.name = "summary-left";
            leftScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            leftScroll.style.width = 430; leftScroll.style.flexShrink = 0; leftScroll.style.marginRight = 8;
            body.Add(leftScroll);
            var left = leftScroll.contentContainer;
            var team = Section(left, UiStrings.TeamChanges);
            Change(team, UiStrings.Funds, UiKit.Yuan(r.FundsBefore), UiKit.Yuan(r.FundsAfter), UiKit.YuanDelta(r.FundsAfter - r.FundsBefore), r.FundsAfter - r.FundsBefore);
            Change(team, UiStrings.Popularity, UiKit.F1(r.PopularityBefore), UiKit.F1(r.PopularityAfter), UiKit.Delta(r.PopularityAfter - r.PopularityBefore), r.PopularityAfter - r.PopularityBefore);
            Change(team, UiStrings.Chemistry, UiKit.F1(r.ChemistryBefore), UiKit.F1(r.ChemistryAfter), UiKit.Delta(r.ChemistryAfter - r.ChemistryBefore), r.ChemistryAfter - r.ChemistryBefore);
            Change(team, UiStrings.Strength, UiKit.F1(r.StrengthBefore), UiKit.F1(r.StrengthAfter), UiKit.Delta(r.StrengthAfter - r.StrengthBefore), r.StrengthAfter - r.StrengthBefore);

            var money = Section(left, UiStrings.MoneyLines);
            Line(money, UiStrings.LivestreamIncome, UiKit.Yuan(sum.LivestreamIncome), UiKit.Text);
            Line(money, UiStrings.BusinessIncome, UiKit.Yuan(sum.BusinessIncome), UiKit.Text);
            Line(money, UiStrings.SponsorIncome, UiKit.Yuan(sum.SponsorshipIncome), UiKit.Text);
            Line(money, UiStrings.PrizeMoney, UiKit.Yuan(sum.PrizeMoney), UiKit.Text);
            Line(money, UiStrings.EventIncome, UiKit.Yuan(sum.EventIncome), sum.EventIncome > 0 ? UiKit.Good : UiKit.Text);
            Line(money, UiStrings.EventExpenses, UiKit.Yuan(-sum.EventExpenses), sum.EventExpenses > 0 ? UiKit.Bad : UiKit.Text);
            Line(money, UiStrings.Income, UiKit.Yuan(sum.Report.Income), UiKit.Good);
            Line(money, UiStrings.Expenses, UiKit.Yuan(-sum.Report.Expenses), UiKit.Bad);
            Line(money, UiStrings.SalaryShare, UiKit.Yuan(-sum.Report.SalaryShare), UiKit.TextDim);
            Line(money, UiStrings.Net, UiKit.YuanDelta(sum.Report.Net), sum.Report.Net >= 0 ? UiKit.Good : UiKit.Bad);

            var sp = Section(left, UiStrings.SponsorResults);
            if (sum.SponsorAttempts.Count == 0) sp.Add(UiKit.Lbl(UiStrings.NoSponsorAttempt, 13, UiKit.TextDim));
            for (int i = 0; i < sum.SponsorAttempts.Count; i++)
            {
                var a = sum.SponsorAttempts[i];
                if (a.Voided) { sp.Add(UiKit.Lbl(string.Format(UiStrings.SponsorVoidedFormat, i + 1), 13, UiKit.Bad)); continue; }
                sp.Add(UiKit.Lbl(string.Format(UiStrings.SponsorAttemptFormat, i + 1, UiKit.Pct(a.Chance), a.Success ? UiStrings.Success : UiStrings.Failure),
                    13, a.Success ? UiKit.Good : UiKit.Bad));
            }
            foreach (var c in sum.NewContracts)
                sp.Add(UiKit.Lbl(string.Format(UiStrings.NewContractFormat, UiKit.Yuan(c.AmountPerSegment), c.SegmentsRemaining), 13, UiKit.Good));

            var of = Section(left, UiStrings.Offers);
            if (sum.OffersAccepted.Count + sum.OffersDeclined.Count + sum.OffersVoided.Count == 0)
                of.Add(UiKit.Lbl(UiStrings.NoOffers, 13, UiKit.TextDim));
            foreach (var o in sum.OffersAccepted)
                of.Add(UiKit.Lbl(string.Format(UiStrings.OfferAcceptedFormat, s.PlayerName(o.PlayerId), UiKit.Yuan(o.Amount), UiKit.Yuan(o.ClubAmount)), 13, UiKit.Good));
            foreach (var o in sum.OffersDeclined)
                of.Add(UiKit.Lbl(string.Format(UiStrings.OfferDeclinedFormat, s.PlayerName(o.PlayerId), UiKit.Yuan(o.Amount)), 13, UiKit.TextDim));
            foreach (var o in sum.OffersVoided)
                of.Add(UiKit.Lbl(string.Format(UiStrings.OfferVoidedFormat, s.PlayerName(o.PlayerId), UiKit.Yuan(o.Amount)), 13, UiKit.Bad));

            var right = new VisualElement(); right.style.flexGrow = 1; right.style.flexShrink = 1; right.style.minWidth = 0; right.style.minHeight = 0;
            body.Add(right);
            var table = Section(right, UiStrings.PlayerChanges);
            table.style.flexShrink = 0;
            table.Add(TableRow(true, UiStrings.ColPlayer, UiStrings.ColActions, UiStrings.ColMood, UiStrings.ColStress,
                UiStrings.ColPopularity, UiStrings.ColTraining, null));
            foreach (var p in sum.Players)
                table.Add(PlayerRow(s, p));
            right.Add(BuildEvents(ui, sum));

        }

        /// <summary>
        /// Story 006 "本赛段事件": one card per occurrence, in order, in a scrolling wrap grid that takes the rest of the right
        /// column. Potential effects show only their direction (EventEffectText never prints a potential number).
        /// </summary>
        private static VisualElement BuildEvents(GameUi ui, SegmentSummary sum)
        {
            var sec = UiKit.Block(UiKit.Panel, 6);
            sec.name = "events-panel";
            UiKit.Pad(sec, 10); sec.style.marginBottom = 8;
            sec.style.flexGrow = 1; sec.style.flexShrink = 1; sec.style.minHeight = 96;
            var head = UiKit.Row(24);
            head.Add(UiKit.Lbl(UiStrings.EventsTitle, 15, UiKit.Accent, true));
            if (sum.Events.Count > 0)
            {
                var n = UiKit.Lbl(string.Format(UiStrings.EventsCountFormat, sum.Events.Count), 13, UiKit.TextDim);
                n.style.marginLeft = 8; head.Add(n);
            }
            sec.Add(head);
            if (sum.Events.Count == 0) { sec.Add(UiKit.Lbl(UiStrings.NoEvents, 13, UiKit.TextDim)); return sec; }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "events-scroll";
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.flexBasis = 0; scroll.style.minHeight = 0; scroll.style.marginTop = 4;
            var grid = scroll.contentContainer;
            grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap; grid.style.alignItems = Align.FlexStart;
            for (int i = 0; i < sum.Events.Count; i++) grid.Add(EventCard(ui, sum.Events[i], i));
            sec.Add(scroll);
            return sec;
        }

        private static VisualElement EventCard(GameUi ui, EsportsManager.Events.EventOccurrence e, int index)
        {
            var s = ui.Session;
            var card = UiKit.Block(UiKit.PanelLight, 4);
            card.name = "event-card-" + index;
            card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = Length.Percent(45); card.style.minWidth = 280;
            card.style.marginRight = 6; card.style.marginBottom = 6;
            UiKit.Pad(card, 8);
            card.style.borderLeftWidth = 5; card.style.borderLeftColor = ChoicePopup.ToneColor(e.Tone);

            var r1 = UiKit.Row(22);
            var t = UiKit.Ellipsis(UiKit.Lbl(ui.EventText(e.TitleKey, e.PlayerId, e.PartnerId), 15, UiKit.Text, true));
            r1.Add(t);
            string who = e.PlayerId.HasValue
                ? (e.PartnerId.HasValue ? string.Format(UiStrings.PairFormat, s.PlayerName(e.PlayerId.Value), s.PlayerName(e.PartnerId.Value)) : s.PlayerName(e.PlayerId.Value))
                : null;
            if (who != null) { var w = UiKit.Lbl(who, 12, UiKit.Warn); w.style.marginLeft = 8; w.style.flexShrink = 0; r1.Add(w); }
            r1.Add(UiKit.Spacer());
            long funds = e.FundsDelta;
            if (funds != 0) { var f = UiKit.Lbl(UiKit.YuanDelta(funds), 13, funds > 0 ? UiKit.Good : UiKit.Bad, true); f.style.flexShrink = 0; f.style.marginLeft = 8; r1.Add(f); }
            card.Add(r1);

            var text = ChoicePopup.Wrap(UiKit.Lbl(ui.EventText(e.TextKey, e.PlayerId, e.PartnerId), 12, UiKit.TextDim));
            text.style.marginTop = 2;
            card.Add(text);
            if (e.Kind == EsportsManager.Events.EventKind.Choice && e.OptionLabelKey != null)
            {
                string taken = string.Format(UiStrings.EventChoiceTakenFormat, ui.EventText(e.OptionLabelKey, e.PlayerId, e.PartnerId))
                    + (e.OptionWasDefault ? UiStrings.ChoiceDefaultTag : "");
                var c = ChoicePopup.Wrap(UiKit.Lbl(taken, 13, UiKit.Accent, true));
                c.style.marginTop = 4;
                card.Add(c);
            }
            var fx = ChoicePopup.PhraseRow(EventEffectText.DescribeAll(e.Effects, true, e.PartnerId.HasValue), 13);
            fx.style.marginTop = 4;
            card.Add(fx);
            return card;
        }

        private static VisualElement Section(VisualElement parent, string title)
        {
            var sec = UiKit.Block(UiKit.Panel, 6);
            UiKit.Pad(sec, 10); sec.style.marginBottom = 8;
            var t = UiKit.Lbl(title, 15, UiKit.Accent, true); t.style.marginBottom = 4;
            sec.Add(t);
            parent.Add(sec);
            return sec;
        }

        private static void Change(VisualElement parent, string label, string before, string after, string delta, float d)
        {
            var row = UiKit.Row(22);
            var l = UiKit.Lbl(label, 13, UiKit.TextDim); l.style.width = 80; row.Add(l);
            row.Add(UiKit.Lbl(string.Format(UiStrings.BeforeAfterFormat, before, after), 13));
            row.Add(UiKit.Spacer());
            row.Add(UiKit.Lbl(delta, 13, UiKit.DeltaColor(d), true));
            parent.Add(row);
        }

        private static void Line(VisualElement parent, string label, string value, Color color)
        {
            var row = UiKit.Row(20);
            row.Add(UiKit.Lbl(label, 13, UiKit.TextDim));
            row.Add(UiKit.Spacer());
            row.Add(UiKit.Lbl(value, 13, color, true));
            parent.Add(row);
        }

        private static readonly float[] Widths = { 90, 150, 60, 60, 60, 0 };

        private static VisualElement TableRow(bool header, string c0, string c1, string c2, string c3, string c4, string c5, Color[] colors)
        {
            var row = UiKit.Row(header ? 24 : 40);
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = UiKit.PanelLight;
            var cells = new[] { c0, c1, c2, c3, c4, c5 };
            for (int i = 0; i < cells.Length; i++)
            {
                var l = UiKit.Lbl(cells[i], header ? 12 : 13, header ? UiKit.TextDim : (colors != null ? colors[i] : UiKit.Text), i == 0 && !header);
                if (Widths[i] > 0) l.style.width = Widths[i]; else { l.style.flexGrow = 1; l.style.flexShrink = 1; l.style.whiteSpace = WhiteSpace.Normal; }
                row.Add(l);
            }
            return row;
        }

        private static VisualElement PlayerRow(GameSession s, PlayerSegmentSummary p)
        {
            var acts = new List<string>();
            if (p.TrainPoints > 0) acts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Train, p.TrainPoints));
            if (p.StreamPoints > 0) acts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Stream, p.StreamPoints));
            if (p.RestPoints > 0) acts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Rest, p.RestPoints));
            if (p.BusinessPoints > 0) acts.Add(string.Format(UiStrings.ActionCountFormat, UiStrings.Business, p.BusinessPoints));
            string training = UiStrings.NoTraining;
            if (p.Training.Count > 0)
            {
                var names = s.DimensionNames(p.PlayerId);
                var parts = new List<string>();
                float total = 0f;
                var perDim = new float[names.Length];
                foreach (var t in p.Training)
                {
                    parts.Add(string.Format(UiStrings.TrainingTargetFormat, t.DimensionIndex < names.Length ? names[t.DimensionIndex] : "?", UiKit.Delta(t.Gain)));
                    if (t.Gains != null) for (int i = 0; i < t.Gains.Length && i < perDim.Length; i++) perDim[i] += t.Gains[i];
                }
                foreach (var g in perDim) total += g;
                training = string.Join(UiStrings.PartSeparator, parts) + UiStrings.PartSeparator + string.Format(UiStrings.TrainingTotalFormat, UiKit.Delta(total));
            }
            var colors = new[] { UiKit.Text, UiKit.Text, UiKit.DeltaColor(p.MoodDelta), UiKit.DeltaColor(p.StressDelta, true),
                UiKit.DeltaColor(p.PopularityDelta), UiKit.Good };
            return TableRow(false, s.PlayerName(p.PlayerId), acts.Count == 0 ? UiStrings.NoActions : string.Join(UiStrings.ActionSeparator, acts),
                UiKit.Delta(p.MoodDelta), UiKit.Delta(p.StressDelta), UiKit.Delta(p.PopularityDelta), training, colors);
        }
    }
}
