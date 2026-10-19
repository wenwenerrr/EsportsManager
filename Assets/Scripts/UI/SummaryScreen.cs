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
        public static void Build(GameUi ui)
        {
            var root = ui.Root;
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
            title.Add(UiKit.Btn(UiStrings.NextSegment, ui.NextSegment, UiKit.Good, true, 15));
            root.Add(title);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1;
            root.Add(body);

            var left = new VisualElement(); left.style.width = 430; left.style.marginRight = 8;
            body.Add(left);
            var team = Section(left, UiStrings.TeamChanges);
            Change(team, UiStrings.Funds, UiKit.Yuan(r.FundsBefore), UiKit.Yuan(r.FundsAfter), UiKit.YuanDelta(r.FundsAfter - r.FundsBefore), r.FundsAfter - r.FundsBefore);
            Change(team, UiStrings.Popularity, r.PopularityBefore.ToString("F1"), r.PopularityAfter.ToString("F1"), UiKit.Delta(r.PopularityAfter - r.PopularityBefore), r.PopularityAfter - r.PopularityBefore);
            Change(team, UiStrings.Chemistry, r.ChemistryBefore.ToString("F1"), r.ChemistryAfter.ToString("F1"), UiKit.Delta(r.ChemistryAfter - r.ChemistryBefore), r.ChemistryAfter - r.ChemistryBefore);
            Change(team, UiStrings.Strength, r.StrengthBefore.ToString("F1"), r.StrengthAfter.ToString("F1"), UiKit.Delta(r.StrengthAfter - r.StrengthBefore), r.StrengthAfter - r.StrengthBefore);

            var money = Section(left, UiStrings.MoneyLines);
            Line(money, UiStrings.LivestreamIncome, UiKit.Yuan(sum.LivestreamIncome), UiKit.Text);
            Line(money, UiStrings.BusinessIncome, UiKit.Yuan(sum.BusinessIncome), UiKit.Text);
            Line(money, UiStrings.SponsorIncome, UiKit.Yuan(sum.SponsorshipIncome), UiKit.Text);
            Line(money, UiStrings.PrizeMoney, UiKit.Yuan(sum.PrizeMoney), UiKit.Text);
            Line(money, UiStrings.Income, UiKit.Yuan(sum.Report.Income), UiKit.Good);
            Line(money, UiStrings.Expenses, UiKit.Yuan(-sum.Report.Expenses), UiKit.Bad);
            Line(money, UiStrings.SalaryShare, UiKit.Yuan(-sum.Report.SalaryShare), UiKit.TextDim);
            Line(money, UiStrings.Net, UiKit.YuanDelta(sum.Report.Net), sum.Report.Net >= 0 ? UiKit.Good : UiKit.Bad);

            var sp = Section(left, UiStrings.SponsorResults);
            if (sum.SponsorAttempts.Count == 0) sp.Add(UiKit.Lbl(UiStrings.NoSponsorAttempt, 13, UiKit.TextDim));
            for (int i = 0; i < sum.SponsorAttempts.Count; i++)
            {
                var a = sum.SponsorAttempts[i];
                sp.Add(UiKit.Lbl(string.Format(UiStrings.SponsorAttemptFormat, i + 1, a.Chance, a.Success ? UiStrings.Success : UiStrings.Failure),
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

            var right = new VisualElement(); right.style.flexGrow = 1;
            body.Add(right);
            var table = Section(right, UiStrings.PlayerChanges);
            table.style.flexGrow = 1;
            table.Add(TableRow(true, UiStrings.ColPlayer, UiStrings.ColActions, UiStrings.ColMood, UiStrings.ColStress,
                UiStrings.ColPopularity, UiStrings.ColTraining, null));
            foreach (var p in sum.Players)
                table.Add(PlayerRow(s, p));
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
            row.Add(UiKit.Lbl(before + " → " + after, 13));
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
                training = string.Join("  ", parts) + "  " + string.Format(UiStrings.TrainingTotalFormat, UiKit.Delta(total));
            }
            var colors = new[] { UiKit.Text, UiKit.Text, UiKit.DeltaColor(p.MoodDelta), UiKit.DeltaColor(p.StressDelta, true),
                UiKit.DeltaColor(p.PopularityDelta), UiKit.Good };
            return TableRow(false, s.PlayerName(p.PlayerId), acts.Count == 0 ? UiStrings.NoActions : string.Join(" ", acts),
                UiKit.Delta(p.MoodDelta), UiKit.Delta(p.StressDelta), UiKit.Delta(p.PopularityDelta), training, colors);
        }
    }
}
