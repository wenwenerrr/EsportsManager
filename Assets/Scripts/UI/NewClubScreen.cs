using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// New game: the player names the club (Story 004). A text field prefilled with the default name from match_config
    /// ("club.default_name"), limited to "club.name_max_length" characters; trimming / empty -> default is done by the session.
    /// </summary>
    public static class NewClubScreen
    {
        /// <summary>Builds the screen.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var rules = ui.Session.ClubRules;
            string def = rules != null ? rules.defaultName : ui.Session.Team.Name;
            int max = rules != null ? rules.nameMaxLength : 16;

            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            root.style.alignItems = Align.Center; root.style.justifyContent = Justify.Center;
            UiKit.Pad(root, 8);

            var box = UiKit.Block(UiKit.Panel, 8);
            box.name = "new-club";
            box.style.width = 560; UiKit.Pad(box, 28);
            box.style.borderTopWidth = 4; box.style.borderTopColor = UiKit.Accent;
            box.Add(UiKit.Lbl(UiStrings.NewClubTitle, 26, UiKit.Text, true));
            var prompt = UiKit.Lbl(string.Format(UiStrings.NewClubPromptFormat, max), 15, UiKit.TextDim);
            prompt.style.marginTop = 14; box.Add(prompt);

            var field = new TextField { value = def, maxLength = max };
            field.name = "club-name";
            field.style.marginTop = 10; field.style.marginLeft = field.style.marginRight = 0;
            field.style.height = 44; field.style.fontSize = 20; field.style.color = UiKit.Text;
            // No runtime theme is loaded, so the input is styled by hand (background, border, padding, text colour).
            field.Query<VisualElement>().ForEach(e =>
            {
                if (e == field) return;
                e.style.backgroundColor = UiKit.PanelLight;
                e.style.color = UiKit.Text;
                e.style.unityTextAlign = TextAnchor.MiddleLeft;
                e.style.flexGrow = 1;
                e.style.marginTop = e.style.marginBottom = 0;
                e.style.paddingTop = e.style.paddingBottom = 0;
                e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 0;
            });
            field.style.unityTextAlign = TextAnchor.MiddleLeft;
            field.style.backgroundColor = UiKit.PanelLight;
            field.style.borderTopWidth = field.style.borderBottomWidth = field.style.borderLeftWidth = field.style.borderRightWidth = 2;
            field.style.borderTopColor = field.style.borderBottomColor = field.style.borderLeftColor = field.style.borderRightColor = UiKit.Accent;
            field.style.borderTopLeftRadius = field.style.borderTopRightRadius = field.style.borderBottomLeftRadius = field.style.borderBottomRightRadius = 4;
            field.style.paddingLeft = field.style.paddingRight = 8;
            box.Add(field);

            var hint = UiKit.Lbl(string.Format(UiStrings.NewClubDefaultFormat, def), 13, UiKit.TextDim);
            hint.style.marginTop = 8; box.Add(hint);

            var row = UiKit.Row(40);
            row.style.marginTop = 18;
            row.Add(UiKit.Spacer());
            var ok = UiKit.Btn(UiStrings.NewClubConfirm, () => ui.ConfirmClubName(field.value), UiKit.Good, true, 16);
            ok.name = "club-confirm";
            ok.style.height = 34; ok.style.marginRight = 0;
            row.Add(ok);
            box.Add(row);
            root.Add(box);
        }
    }
}
