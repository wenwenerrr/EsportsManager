using EsportsManager.Segments;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// UI controller: owns only presentation state (message line, pop-up open, close-up mode) and rebuilds the screen
    /// from <see cref="GameSession"/> after every change. Clicks are forwarded to the session; no game rules here.
    /// </summary>
    public sealed class GameUi
    {
        /// <summary>The session being shown.</summary>
        public GameSession Session { get; }
        /// <summary>Panel root.</summary>
        public VisualElement Root { get; }
        /// <summary>Message line text.</summary>
        public string Message = UiStrings.Hint;
        /// <summary>True when the message is a refusal / error.</summary>
        public bool MessageIsError;
        /// <summary>Business pop-up visible (opens automatically when a segment begins with offers).</summary>
        public bool OfferPopupOpen;
        /// <summary>Close-up mode: only cards, at most this many.</summary>
        public bool CardsOnly;
        /// <summary>Max cards in close-up mode.</summary>
        public int MaxCards = int.MaxValue;

        /// <summary>Creates the controller.</summary>
        public GameUi(VisualElement root, GameSession session)
        {
            Root = root; Session = session;
            OfferPopupOpen = session.UnansweredOffers().Count > 0;
        }

        /// <summary>Rebuilds everything.</summary>
        public void Refresh()
        {
            Root.Clear();
            if (Session.AwaitingNext && Session.LastResult != null) { SummaryScreen.Build(this); return; }
            MainScreen.Build(this);
            if (OfferPopupOpen && !CardsOnly && Session.UnansweredOffers().Count > 0) BuildOfferPopup();
        }

        /// <summary>Shows a message (error = refusal colour) and rebuilds.</summary>
        public void Say(string text, bool error) { Message = text; MessageIsError = error; Refresh(); }

        /// <summary>Text for a refused change.</summary>
        public string ErrorText(PlanError e) => UiStrings.PlanError(e, Session.PlayerName);

        // ---- actions forwarded from the screens
        public void Train(long id, int dim)
        {
            if (Session.TryAddTrain(id, dim, out var e))
                Say(string.Format(UiStrings.Added, Session.PlayerName(id), string.Format(UiStrings.TrainDimFormat, Session.DimensionNames(id)[dim])), false);
            else Say(ErrorText(e), true);
        }
        public void Stream(long id)
        {
            if (Session.TryAddStream(id, out var e)) Say(string.Format(UiStrings.Added, Session.PlayerName(id), UiStrings.Stream), false);
            else Say(ErrorText(e), true);
        }
        public void Rest(long id)
        {
            if (Session.TryAddRest(id, out var e)) Say(string.Format(UiStrings.Added, Session.PlayerName(id), UiStrings.Rest), false);
            else Say(ErrorText(e), true);
        }
        public void Undo(long id)
        {
            bool ok = Session.UndoLast(id);
            Say(string.Format(ok ? UiStrings.Undone : UiStrings.NothingToUndo, Session.PlayerName(id)), !ok);
        }
        public void Sponsor()
        {
            if (Session.TryAddSponsor(out var e)) Say(string.Format(UiStrings.Added, "", UiStrings.Sponsor), false);
            else Say(ErrorText(e), true);
        }
        public void UndoSponsor() { Session.UndoSponsor(); Say(UiStrings.Hint, false); }
        public void ClearPlan() { Session.ClearPlan(); Say(UiStrings.Cleared, false); }
        public void Resolve()
        {
            if (!Session.TryValidate(out var e)) { Say(ErrorText(e), true); return; }
            Session.Resolve();
            Refresh();
        }
        public void NextSegment()
        {
            Session.BeginNext();
            OfferPopupOpen = Session.UnansweredOffers().Count > 0;
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
        }
        public void AcceptOffer(BusinessOffer o)
        {
            if (Session.TryAcceptOffer(o.Id, out var e)) Say(string.Format(UiStrings.OfferAccepted, Session.PlayerName(o.PlayerId)), false);
            else Say(ErrorText(e), true);
        }
        public void DeclineOffer(BusinessOffer o)
        {
            Session.DeclineOffer(o.Id);
            Say(string.Format(UiStrings.OfferDeclined, Session.PlayerName(o.PlayerId)), false);
        }
        public void OpenOffers() { OfferPopupOpen = true; Refresh(); }
        public void LaterOffers() { OfferPopupOpen = false; Refresh(); }

        private void BuildOfferPopup()
        {
            var offers = Session.UnansweredOffers();
            var o = offers[0];
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            overlay.style.alignItems = Align.Center; overlay.style.justifyContent = Justify.Center;

            var box = UiKit.Block(UiKit.Panel, 8);
            box.style.width = 560; UiKit.Pad(box, 18);
            box.style.borderTopWidth = 4; box.style.borderTopColor = UiKit.Warn;
            var title = UiKit.Row(30);
            title.Add(UiKit.Lbl(UiStrings.OfferTitle, 20, UiKit.Warn, true));
            title.Add(UiKit.Lbl(string.Format(UiStrings.OfferCountFormat, 1, offers.Count), 14, UiKit.TextDim));
            box.Add(title);
            var body = UiKit.Lbl(string.Format(UiStrings.OfferBodyFormat, Session.PlayerName(o.PlayerId)), 16);
            body.style.marginTop = 10; box.Add(body);
            var amt = UiKit.Lbl(string.Format(UiStrings.OfferAmountFormat, UiKit.Yuan(o.Amount), UiKit.Yuan(o.ClubAmount)), 16, UiKit.Good, true);
            amt.style.marginTop = 6; box.Add(amt);

            var pv = Session.Preview();
            int used = 0;
            foreach (var pp in pv.Players) if (pp.PlayerId == o.PlayerId) used = pp.PointsUsed;
            var st = Session.State;
            var cost = UiKit.Lbl(string.Format(UiStrings.OfferCostFormat, used, st.MaxPerPlayer, pv.PointsRemaining), 13, UiKit.TextDim);
            cost.style.marginTop = 6; box.Add(cost);

            bool can = Session.CanAcceptOffer(o.Id, out var err);
            if (!can)
            {
                var why = UiKit.Lbl(ErrorText(err), 13, UiKit.Bad);
                why.style.marginTop = 6; box.Add(why);
            }
            var buttons = UiKit.Row(30);
            buttons.style.marginTop = 14;
            buttons.Add(UiKit.Btn(UiStrings.Accept, () => AcceptOffer(o), UiKit.Good, can, 14));
            buttons.Add(UiKit.Btn(UiStrings.Decline, () => DeclineOffer(o), UiKit.Bad, true, 14));
            buttons.Add(UiKit.Spacer());
            buttons.Add(UiKit.Btn(UiStrings.Later, LaterOffers, UiKit.PanelLight, true, 14));
            box.Add(buttons);
            overlay.Add(box);
            Root.Add(overlay);
        }
    }
}
