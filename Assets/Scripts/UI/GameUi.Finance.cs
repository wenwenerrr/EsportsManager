using EsportsManager.Leagues;
using EsportsManager.Segments;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Playtest changes 2026-10-30 (economy.md 媒体邀约 / 财务危机与解雇): the media-invitation pop-up, the notice pop-up (fire
    /// sales, crisis start / end, chip-ins), the crisis banner and the 被解雇 flow. Presentation only: every click forwards to
    /// <see cref="Session.GameSession"/>.
    /// </summary>
    public sealed partial class GameUi
    {
        /// <summary>Media pop-up visible.</summary>
        public bool MediaPopupOpen => Stack.Contains(ScreenId.MediaPopup);
        /// <summary>Notice pop-up visible.</summary>
        public bool NoticeOpen => Stack.Contains(ScreenId.NoticePopup);

        private UiScreen MediaUiScreen() => new UiScreen(ScreenId.MediaPopup, true, BuildMediaPopup);
        private UiScreen NoticeUiScreen() => new UiScreen(ScreenId.NoticePopup, true, BuildNoticePopup);

        /// <summary>Pushes the notice pop-up on top when the session has unseen notices.</summary>
        private void PushNotices()
        {
            if (Session.Notices.Count > 0 && !NoticeOpen) Stack.Push(NoticeUiScreen());
        }

        /// <summary>"知道了": the notices are acknowledged and the pop-up closes.</summary>
        public void AcknowledgeNotices()
        {
            Session.AcknowledgeNotices();
            if (Stack.IsTop(ScreenId.NoticePopup)) Stack.Pop();
            Refresh();
        }

        /// <summary>The manager was fired: the 被解雇 screen with the new offers.</summary>
        public void ShowFired()
        {
            MarkScreenChange();
            SelectedOffer = -1; OfferName = null;
            Stack.Reset(JobOffersUiScreen());
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
        }

        /// <summary>Debug demos: the main screen with the start pop-ups (notices on top).</summary>
        public void ShowMainForDemo()
        {
            Stack.Reset(MainUiScreen());
            PushStartPopups();
            Refresh();
        }

        /// <summary>Opens the media pop-up (badge).</summary>
        public void OpenMedia() { if (!MediaPopupOpen && Session.MediaInvitations.Count > 0) Stack.Push(MediaUiScreen()); Refresh(); }
        /// <summary>Closes the media pop-up; unanswered invitations count as declined at resolve.</summary>
        public void CloseMedia() { if (Stack.IsTop(ScreenId.MediaPopup)) Stack.Pop(); Refresh(); }

        /// <summary>Accepts an invitation (one club point).</summary>
        public void AcceptMedia(MediaInvitation m)
        {
            if (Session.TryAcceptMedia(m.Id, out var e)) Say(UiStrings.MediaAcceptedMsg, false);
            else Say(ErrorText(e), true);
        }

        /// <summary>Declines an invitation.</summary>
        public void DeclineMedia(MediaInvitation m) { Session.DeclineMedia(m.Id); Say(UiStrings.MediaDeclinedMsg, false); }

        private static VisualElement Overlay()
        {
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            overlay.style.alignItems = Align.Center; overlay.style.justifyContent = Justify.Center;
            return overlay;
        }

        private static VisualElement PopupBox(float width, Color accent)
        {
            var box = UiKit.Block(UiKit.Panel, 8);
            box.style.width = width; UiKit.Pad(box, 18);
            box.style.borderTopWidth = 4; box.style.borderTopColor = accent;
            return box;
        }

        private static Label Wrap(Label l) { l.style.whiteSpace = WhiteSpace.Normal; return l; }

        private void BuildMediaPopup(VisualElement root)
        {
            if (OfferPopupOpen || ChoicePopupOpen || NoticeOpen) return; // pop-ups above it are shown first
            var list = Session.MediaInvitations;
            if (list.Count == 0) return;
            var m = Session.World.Config.media;
            var overlay = Overlay();
            var box = PopupBox(620, UiKit.Accent);
            box.name = "media-popup";
            var title = UiKit.Row(30);
            title.Add(UiKit.Lbl(UiStrings.MediaTitle, 20, UiKit.Accent, true));
            var cnt = UiKit.Lbl(string.Format(UiStrings.MediaCountFormat, list.Count), 14, UiKit.TextDim); cnt.style.marginLeft = 10;
            title.Add(cnt);
            box.Add(title);
            foreach (var inv in list)
            {
                var row = UiKit.Row(36);
                row.style.marginTop = 6;
                var l = UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.MediaLineFormat, inv.Id, UiKit.N0(inv.Income), UiKit.F1(m.acceptPopularity), UiKit.F1(m.acceptStarterStress)), 14));
                l.style.flexGrow = 1; l.style.flexShrink = 1; row.Add(l);
                if (Session.IsMediaAccepted(inv.Id))
                {
                    row.Add(UiKit.Lbl(UiStrings.MediaAccepted, 13, UiKit.Good, true));
                    row.Add(UiKit.Btn(UiStrings.Decline, () => DeclineMedia(inv), UiKit.Bad, true, 13));
                }
                else
                {
                    if (Session.UnansweredMedia().Exists(x => x.Id == inv.Id) == false) row.Add(UiKit.Lbl(UiStrings.MediaDeclined, 13, UiKit.Warn, true));
                    row.Add(UiKit.Btn(UiStrings.Accept, () => AcceptMedia(inv), UiKit.Good, true, 13));
                    if (Session.UnansweredMedia().Exists(x => x.Id == inv.Id)) row.Add(UiKit.Btn(UiStrings.Decline, () => DeclineMedia(inv), UiKit.Bad, true, 13));
                }
                box.Add(row);
            }
            var pv = Session.Preview();
            var cost = Wrap(UiKit.Lbl(string.Format(UiStrings.MediaCostFormat, pv.PointsRemaining, UiKit.F1(m.declinePopularity)), 12, UiKit.TextDim));
            cost.style.marginTop = 8; box.Add(cost);
            var buttons = UiKit.Row(30);
            buttons.style.marginTop = 12;
            buttons.Add(UiKit.Spacer());
            buttons.Add(UiKit.Btn(UiStrings.Close, CloseMedia, UiKit.PanelLight, true, 14));
            box.Add(buttons);
            overlay.Add(box);
            root.Add(overlay);
        }

        /// <summary>Text of one notice.</summary>
        public static string NoticeText(ClubNotice n)
        {
            switch (n.Kind)
            {
                case NoticeKind.FireSale: return string.Format(UiStrings.NoticeFireSaleFormat, n.PlayerName, UiKit.N0(n.Amount));
                case NoticeKind.CrisisStart: return string.Format(UiStrings.NoticeCrisisStartFormat, UiKit.N0(n.Amount), n.SegmentsLeft);
                case NoticeKind.ChipIn: return string.Format(UiStrings.NoticeChipInFormat, n.PlayerName, UiKit.N0(n.Amount));
                default: return UiStrings.NoticeCrisisOver;
            }
        }

        private void BuildNoticePopup(VisualElement root)
        {
            var list = Session.Notices;
            if (list.Count == 0) return;
            var overlay = Overlay();
            var box = PopupBox(640, UiKit.Warn);
            box.name = "notice-popup";
            box.Add(UiKit.Lbl(UiStrings.NoticeTitle, 20, UiKit.Warn, true));
            foreach (var n in list)
            {
                var l = Wrap(UiKit.Lbl(NoticeText(n), 15, n.Kind == NoticeKind.ChipIn || n.Kind == NoticeKind.CrisisOver ? UiKit.Good : UiKit.Text));
                l.style.marginTop = 8;
                box.Add(l);
            }
            var buttons = UiKit.Row(30);
            buttons.style.marginTop = 14;
            buttons.Add(UiKit.Spacer());
            var ok = UiKit.Btn(UiStrings.NoticeOk, AcknowledgeNotices, UiKit.Accent, true, 14);
            ok.name = "notice-ok";
            buttons.Add(ok);
            box.Add(buttons);
            overlay.Add(box);
            root.Add(overlay);
        }

        /// <summary>The crisis strip of the main screen (segments left, deficit), or null outside a crisis.</summary>
        public VisualElement CrisisBanner()
        {
            if (!Session.InCrisis) return null;
            var b = UiKit.Block(new Color(0.5f, 0.13f, 0.13f, 1f), 4);
            b.name = "crisis-banner";
            b.style.flexDirection = FlexDirection.Row; b.style.alignItems = Align.Center;
            b.style.height = 30; b.style.flexShrink = 0; b.style.marginBottom = 6; b.style.paddingLeft = 10;
            var t = UiKit.Lbl(UiStrings.CrisisTitle, 15, UiKit.Text, true); t.style.marginRight = 10; t.style.flexShrink = 0; b.Add(t);
            b.Add(UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.CrisisFormat, Session.CrisisSegmentsLeft, UiKit.N0(System.Math.Max(0, -Session.Team.Funds))), 13, UiKit.Text)));
            return b;
        }
    }
}
