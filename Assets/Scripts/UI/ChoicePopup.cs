using System.Collections.Generic;
using EsportsManager.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Story 006: the two-choice event pop-up (overlay on Main, like the business pop-up) and the shared event visuals
    /// (effect phrase rows, tone colours) used by the settlement cards. Renders session data only; answers go through
    /// <see cref="GameUi.AnswerChoice"/>.
    /// </summary>
    public static class ChoicePopup
    {
        /// <summary>Builds the pop-up for <see cref="GameUi.ShownChoice"/> (nothing when there is no choice).</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            if (ui.NoticeOpen) return; // the notice pop-up above it is shown first
            var c = ui.ShownChoice();
            if (c == null) return;
            var s = ui.Session;
            int open = ui.UnansweredChoices().Count;
            bool reviewing = open == 0;
            int effective = s.EffectiveChoiceOption(c.Id);

            var overlay = new VisualElement();
            overlay.name = "choice-popup";
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            overlay.style.alignItems = Align.Center; overlay.style.justifyContent = Justify.Center;

            var box = UiKit.Block(UiKit.Panel, 8);
            box.style.width = 620; box.style.maxWidth = Length.Percent(92); box.style.maxHeight = Length.Percent(94);
            UiKit.Pad(box, 18);
            box.style.borderTopWidth = 4; box.style.borderTopColor = UiKit.Accent;

            var head = UiKit.Row(28);
            head.Add(UiKit.Lbl(UiStrings.ChoiceTitle, 15, UiKit.Accent, true));
            if (!reviewing) head.Add(UiKit.Lbl(string.Format(UiStrings.ChoiceCountFormat, 1, open), 13, UiKit.TextDim));
            box.Add(head);

            var title = UiKit.Lbl(ui.EventText(c.TitleKey, c.PlayerId, c.PartnerId), 20, UiKit.Text, true);
            title.name = "choice-title"; title.style.marginTop = 4;
            box.Add(title);
            var body = Wrap(UiKit.Lbl(ui.EventText(c.TextKey, c.PlayerId, c.PartnerId), 14, UiKit.Text));
            body.style.marginTop = 6; body.style.marginBottom = 8;
            box.Add(body);

            bool pair = c.PartnerId.HasValue;
            for (int i = 0; i < c.Options.Count; i++)
            {
                int option = i;
                var o = c.Options[i];
                bool chosen = i == effective;
                var opt = UiKit.Block(UiKit.PanelLight, 6);
                opt.name = "choice-option-" + i;
                UiKit.Pad(opt, 10); opt.style.marginTop = 6;
                opt.style.borderLeftWidth = 3; opt.style.borderLeftColor = chosen ? UiKit.Warn : UiKit.PanelLight;
                var r = UiKit.Row(30);
                var b = UiKit.Btn(ui.EventText(o.LabelKey, c.PlayerId, c.PartnerId), () => ui.AnswerChoice(c, option), UiKit.Accent, true, 14);
                b.style.height = 28; b.style.flexShrink = 1; b.style.minWidth = 0;
                r.Add(b);
                if (i == c.DefaultOption && !s.IsChoiceAnswered(c.Id)) r.Add(UiKit.Lbl(UiStrings.ChoiceDefaultTag, 13, UiKit.Warn, true));
                else if (chosen && s.IsChoiceAnswered(c.Id)) r.Add(UiKit.Lbl(UiStrings.ChoiceSelectedTag, 13, UiKit.Warn, true));
                opt.Add(r);
                var fx = PhraseRow(EventEffectText.DescribeAll(o.Effects, false, pair), 13);
                fx.style.marginTop = 6;
                opt.Add(fx);
                box.Add(opt);
            }

            var note = UiKit.Lbl(UiStrings.ChoiceDefaultNote, 12, UiKit.TextDim);
            note.style.marginTop = 10; box.Add(note);
            if (!string.IsNullOrEmpty(ui.ChoiceError))
            {
                var err = Wrap(UiKit.Lbl(ui.ChoiceError, 13, UiKit.Bad, true));
                err.name = "choice-error"; err.style.marginTop = 6; box.Add(err);
            }
            var buttons = UiKit.Row(30);
            buttons.style.marginTop = 12;
            buttons.Add(UiKit.Spacer());
            buttons.Add(UiKit.Btn(reviewing ? UiStrings.Back : UiStrings.ChoiceLater, ui.LaterChoices, UiKit.PanelLight, true, 14));
            box.Add(buttons);
            overlay.Add(box);
            root.Add(overlay);
        }

        /// <summary>A wrapping row of coloured effect phrases.</summary>
        public static VisualElement PhraseRow(List<EffectPhrase> phrases, int size)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;
            foreach (var p in phrases)
            {
                var l = UiKit.Lbl(p.Text, size, SentimentColor(p.Sentiment), p.Sentiment != 0);
                l.style.marginRight = 12;
                row.Add(l);
            }
            return row;
        }

        /// <summary>Good green, bad red, neutral dim.</summary>
        public static Color SentimentColor(int sentiment) => sentiment > 0 ? UiKit.Good : sentiment < 0 ? UiKit.Bad : UiKit.TextDim;

        /// <summary>Stripe colour of an event card: good green, bad red, choice / neutral accent.</summary>
        public static Color ToneColor(EventTone tone) => tone == EventTone.Good ? UiKit.Good : tone == EventTone.Bad ? UiKit.Bad : UiKit.Accent;

        /// <summary>Lets a label wrap onto several lines.</summary>
        public static Label Wrap(Label l)
        {
            l.style.whiteSpace = WhiteSpace.Normal; l.style.flexShrink = 1; l.style.minWidth = 0;
            return l;
        }
    }
}
