using System.Globalization;
using EsportsManager.Growth;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Training pop-up (Story 012, design/gdd/growth.md "UI Requirements"): choose a phase (前期 / 团战 / 后期, showing its
    /// dimensions) or command, an optional focus dimension inside the phase, see the +-noise range of one point and how far the
    /// phase's specialty is ("离绝活还差"). Never shows the potential. Display only: every change goes through <see cref="GameUi"/>.
    /// </summary>
    public static class TrainingPopup
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Builds the overlay into the root.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            if (ui.TrainingPlayer == null) return;
            var s = ui.Session;
            var c = ui.FindCard(ui.TrainingPlayer.Value);
            if (c == null) return;
            var target = ui.TrainingTarget;

            var overlay = new VisualElement();
            overlay.name = "training-popup";
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            overlay.style.alignItems = Align.Center; overlay.style.justifyContent = Justify.Center;

            var box = UiKit.Block(UiKit.Panel, 8);
            box.style.width = 620; UiKit.Pad(box, 16);
            box.style.borderTopWidth = 4; box.style.borderTopColor = UiKit.Warn;

            var title = UiKit.Row(30);
            title.Add(UiKit.Lbl(string.Format(UiStrings.TrainTitleFormat, c.Name, c.LaneName), 20, UiKit.Warn, true));
            title.Add(UiKit.Spacer());
            int used = 0;
            foreach (var pp in s.Preview().Players) if (pp.PlayerId == c.Id) used = pp.PointsUsed;
            title.Add(UiKit.Lbl(string.Format(UiStrings.TrainPointsFormat, used, s.State.MaxPerPlayer), 12, UiKit.TextDim));
            box.Add(title);

            // 1. target
            box.Add(Section(UiStrings.TrainChooseTarget));
            var targets = UiKit.Row(52);
            for (int t = 0; t < 4; t++)
            {
                var tt = (TrainingTarget)t;
                bool sel = tt == target;
                var b = UiKit.Block(sel ? UiKit.Accent : UiKit.PanelLight, 4);
                b.name = "train-target-" + t;
                b.style.flexGrow = 1; b.style.flexBasis = 0; b.style.height = 50; b.style.marginRight = 4; UiKit.Pad(b, 5);
                if (sel) { b.style.borderBottomWidth = 3; b.style.borderBottomColor = UiKit.Text; }
                b.Add(UiKit.Lbl(UiStrings.TargetName(tt), 15, UiKit.Text, true));
                b.Add(UiKit.Ellipsis(UiKit.Lbl(DimList(c, tt), 10, sel ? UiKit.Text : UiKit.TextDim)));
                b.AddManipulator(new Clickable(() => ui.SelectTrainingTarget(tt)));
                targets.Add(b);
            }
            box.Add(targets);

            // 2. focus dimension
            box.Add(Section(UiStrings.TrainChooseFocus));
            var focusRow = UiKit.Row(28);
            int[] dims = TargetDims(c, target);
            if (target == TrainingTarget.Command) focusRow.Add(UiKit.Lbl(UiStrings.TrainCommandNote, 12, UiKit.TextDim));
            else if (dims.Length <= 1) focusRow.Add(UiKit.Lbl(UiStrings.TrainFocusSingle, 12, UiKit.TextDim));
            else
            {
                focusRow.Add(FocusButton(ui, UiStrings.TrainNoFocus, -1, ui.TrainingFocus == -1));
                foreach (int d in dims) focusRow.Add(FocusButton(ui, c.DimensionNames[d], d, ui.TrainingFocus == d));
            }
            box.Add(focusRow);

            // 3. range preview of ONE point (+-noise; event modifiers unknown)
            box.Add(Section(UiStrings.TrainPreviewTitle));
            var pv = s.PreviewTrainingPoint(c.Id, target, ui.TrainingFocus);
            var grid = new VisualElement();
            grid.name = "train-range";
            grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap;
            if (pv != null)
            {
                foreach (int d in pv.TargetDimensions)
                {
                    bool near = c.NearCap.Length > d && c.NearCap[d];
                    var cell = UiKit.Block(UiKit.PanelLight, 3);
                    cell.style.flexDirection = FlexDirection.Row; cell.style.alignItems = Align.Center;
                    cell.style.height = 24; cell.style.marginRight = 6; cell.style.marginBottom = 4; cell.style.paddingLeft = cell.style.paddingRight = 6;
                    cell.Add(UiKit.Lbl(string.Format(UiStrings.TrainRangeFormat, c.DimensionNames[d], pv.Low[d].ToString("0.0", Inv), pv.High[d].ToString("0.0", Inv)),
                        13, UiKit.Good, true));
                    if (near) { var n = UiKit.Lbl(UiStrings.NearCapTag, 10, UiKit.Warn, true); n.style.marginLeft = 4; cell.Add(n); }
                    grid.Add(cell);
                }
                float lo = float.MaxValue, hi = 0f;
                for (int d = 0; d < pv.Low.Length; d++)
                {
                    if (System.Array.IndexOf(pv.TargetDimensions, d) >= 0) continue;
                    lo = Mathf.Min(lo, pv.Low[d]); hi = Mathf.Max(hi, pv.High[d]);
                }
                box.Add(grid);
                if (lo <= hi) box.Add(UiKit.Lbl(string.Format(UiStrings.TrainOtherFormat, lo.ToString("0.0", Inv), hi.ToString("0.0", Inv)), 12, UiKit.TextDim));
            }
            else box.Add(grid);

            // 4. specialty distance of this phase
            foreach (var sp in c.SpecialtyProgress)
            {
                bool relevant = target == TrainingTarget.Command ? sp.Phase < 0 : sp.Phase == (int)target;
                if (!relevant) continue;
                string text = sp.Owned
                    ? sp.Name + " · " + UiStrings.SpecialtyUnlocked
                    : UiStrings.SpecialtyGap(sp.Name, sp.DimensionName, sp.DimensionGap, sp.Phase, sp.FocusGap);
                var l = UiKit.Lbl(text, 13, sp.Owned ? UiKit.Good : UiKit.Accent, true);
                l.name = "train-specialty-gap";
                l.style.marginTop = 4;
                box.Add(l);
            }
            var note = UiKit.Lbl(UiStrings.TrainStateNote, 11, UiKit.TextDim); note.style.marginTop = 6; note.style.whiteSpace = WhiteSpace.Normal;
            box.Add(note);

            var buttons = UiKit.Row(32);
            buttons.style.marginTop = 12;
            var ok = UiKit.Btn(UiStrings.TrainConfirm, ui.ConfirmTraining, UiKit.Good, true, 14);
            ok.name = "train-confirm";
            buttons.Add(ok);
            buttons.Add(UiKit.Spacer());
            buttons.Add(UiKit.Btn(UiStrings.TrainClose, ui.CloseTraining, UiKit.PanelLight, true, 14));
            box.Add(buttons);
            if (ui.MessageIsError)
            {
                var err = UiKit.Lbl(ui.Message, 12, UiKit.Bad); err.style.marginTop = 6; box.Add(err);
            }
            overlay.Add(box);
            root.Add(overlay);
        }

        private static int[] TargetDims(PlayerCardData c, TrainingTarget t)
            => t == TrainingTarget.Command ? new[] { c.CommandIndex } : c.PhaseDimensions[(int)t];

        private static string DimList(PlayerCardData c, TrainingTarget t)
        {
            var dims = TargetDims(c, t);
            var names = new string[dims.Length];
            for (int i = 0; i < dims.Length; i++) names[i] = dims[i] >= 0 && dims[i] < c.DimensionNames.Length ? c.DimensionNames[dims[i]] : "?";
            return string.Join(UiStrings.TraitSeparator, names);
        }

        private static Label Section(string text)
        {
            var l = UiKit.Lbl(text, 12, UiKit.TextDim);
            l.style.marginTop = 8; l.style.marginBottom = 3;
            return l;
        }

        private static Button FocusButton(GameUi ui, string text, int dim, bool selected)
        {
            var b = UiKit.Btn(text, () => ui.SelectTrainingFocus(dim), selected ? UiKit.Accent : UiKit.PanelLight, true, 12);
            b.name = "train-focus-" + dim;
            b.style.height = 24;
            if (selected) { b.style.borderBottomWidth = 2; b.style.borderBottomColor = UiKit.Text; b.style.unityFontStyleAndWeight = FontStyle.Bold; }
            return b;
        }
    }
}
