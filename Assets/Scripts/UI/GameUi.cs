using System.Collections.Generic;
using EsportsManager.Events;
using EsportsManager.Growth;
using EsportsManager.Matches;
using EsportsManager.Segments;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// UI controller: owns only presentation state (message line, screen stack, close-up mode) and rebuilds the visible
    /// screens from <see cref="GameSession"/> after every change. Clicks are forwarded to the session; no game rules here.
    /// Screens live on a <see cref="ScreenStack"/> (TD-003): Main at the bottom; the business pop-up is an overlay on Main;
    /// resolving pushes Summary; "next segment" resets to Main. After an event segment the summary's "next step" REPLACES the
    /// top with EventPrep; "开赛" replaces it with RegularResult (split) or Bracket (playoffs / finals); RegularResult leads to
    /// EventPrep again (playoff adjustment) or straight to Bracket; Bracket's "continue" acknowledges the event and resets to
    /// Main. Standings is pushed from Main and popped by "back". Events are never auto-played here.
    /// </summary>
    public sealed partial class GameUi
    {
        /// <summary>The session being shown.</summary>
        public GameSession Session { get; }
        /// <summary>Panel root.</summary>
        public VisualElement Root { get; }
        /// <summary>The screen stack.</summary>
        public ScreenStack Stack { get; } = new ScreenStack();
        /// <summary>Message line text.</summary>
        public string Message = UiStrings.Hint;
        /// <summary>True when the message is a refusal / error.</summary>
        public bool MessageIsError;
        /// <summary>Business pop-up visible.</summary>
        public bool OfferPopupOpen => Stack.Contains(ScreenId.OfferPopup);
        /// <summary>Close-up mode: only cards, at most this many.</summary>
        public bool CardsOnly;
        /// <summary>Max cards in close-up mode.</summary>
        public int MaxCards = int.MaxValue;
        /// <summary>One-off banner on the main screen (e.g. "第 2 年开始"); cleared when the next segment is resolved.</summary>
        public string Banner;
        /// <summary>Debug ("report" demo): the event result screen shows only its detailed series reports.</summary>
        public bool ScrollToReports;

        /// <summary>
        /// Double-click guard (seconds): flow-advancing actions (resolve, next step, start, to playoffs, continue) are ignored
        /// this long after the last screen change, because every flow button sits at the same spot and the UI rebuilds
        /// synchronously. 0 disables it (scripted debug demos).
        /// </summary>
        public double FlowGuardSeconds = 0.35;
        private readonly System.Func<double> _clock;
        private double _lastScreenChange = double.NegativeInfinity;

        /// <summary>Creates the controller; the business pop-up opens automatically when the segment begins with offers.</summary>
        /// <param name="root">Panel root.</param>
        /// <param name="session">The session.</param>
        /// <param name="clock">Unscaled seconds (injectable for tests); default Time.unscaledTimeAsDouble.</param>
        public GameUi(VisualElement root, GameSession session, System.Func<double> clock = null)
        {
            Root = root; Session = session;
            _clock = clock ?? (() => Time.unscaledTimeAsDouble);
            if (session.ChoosingOffer) { Stack.Reset(JobOffersUiScreen()); return; }
            if (session.InTransferWindow) { Stack.Reset(TransferUiScreen()); return; }
            Stack.Reset(MainUiScreen());
            PushStartPopups();
        }

        // ---- Story 013: job offers, opponent info
        /// <summary>Offer chosen on the job-offer screen (-1 = none yet).</summary>
        public int SelectedOffer = -1;
        /// <summary>Name typed on the job-offer screen (null = the club's name).</summary>
        public string OfferName;
        /// <summary>Club shown in the opponent panel (-1 = the default opponent).</summary>
        public int OpponentIndex = -1;

        private UiScreen JobOffersUiScreen() => new UiScreen(ScreenId.JobOffers, false, r => JobOfferScreen.Build(this, r));

        /// <summary>New game: the three job offers (pick + name).</summary>
        public void ShowJobOffers()
        {
            Stack.Reset(JobOffersUiScreen());
            Refresh();
        }

        /// <summary>Selects an offer (the name field takes the club's name).</summary>
        public void SelectOffer(int index)
        {
            SelectedOffer = index;
            OfferName = null;
            Refresh();
        }

        /// <summary>Takes the selected offer with the typed name and opens the main screen.</summary>
        public void ConfirmOffer(string name)
        {
            if (!Session.ChoosingOffer || SelectedOffer < 0) { Say(UiStrings.OfferPickFirst, true); return; }
            var c = Session.TakeOffer(SelectedOffer, name);
            Stack.Reset(MainUiScreen());
            PushStartPopups();
            Message = string.Format(UiStrings.OfferTakenFormat, c.Name); MessageIsError = false;
            Refresh();
        }

        /// <summary>Shows another club in the opponent panel.</summary>
        public void ShowOpponent(int club) { OpponentIndex = club; Refresh(); }

        /// <summary>True while flow-advancing actions are ignored (just after a screen change).</summary>
        public bool FlowLocked => _clock() - _lastScreenChange < FlowGuardSeconds;

        private void MarkScreenChange() => _lastScreenChange = _clock();

        /// <summary>
        /// A newly shown flow button starts disabled while <see cref="FlowLocked"/> and enables itself when the guard window ends,
        /// so a double click cannot land on it.
        /// </summary>
        public void GuardFlowButton(Button b, bool enabled)
        {
            if (!enabled || !FlowLocked) return;
            b.SetEnabled(false);
            double remain = FlowGuardSeconds - (_clock() - _lastScreenChange);
            b.schedule.Execute(() => b.SetEnabled(true)).StartingIn((long)(remain * 1000.0) + 1);
        }

        /// <summary>Rebuilds the visible screens.</summary>
        public void Refresh()
        {
            // Pop-ups close themselves once there is nothing left to show (the choice pop-up closes after the last answer,
            // see AnswerChoice; it can be reopened to change an answer).
            bool popped = true;
            while (popped)
            {
                popped = false;
                if (Stack.IsTop(ScreenId.ChoicePopup) && (CardsOnly || Session.PendingChoices.Count == 0)) popped = Stack.Pop();
                else if (Stack.IsTop(ScreenId.OfferPopup) && (CardsOnly || Session.UnansweredOffers().Count == 0)) popped = Stack.Pop();
                else if (Stack.IsTop(ScreenId.TrainingPopup) && (TrainingPlayer == null || Session.AwaitingNext)) popped = Stack.Pop();
            }
            Stack.Render(Root);
        }

        /// <summary>Shows a message (error = refusal colour) and rebuilds.</summary>
        public void Say(string text, bool error) { Message = text; MessageIsError = error; Refresh(); }

        /// <summary>Text for a refused change.</summary>
        public string ErrorText(PlanError e) => UiStrings.PlanError(e, Session.PlayerName);

        private UiScreen MainUiScreen() => new UiScreen(ScreenId.Main, false, r => MainScreen.Build(this, r));
        private UiScreen OfferUiScreen() => new UiScreen(ScreenId.OfferPopup, true, BuildOfferPopup);
        private UiScreen ChoiceUiScreen() => new UiScreen(ScreenId.ChoicePopup, true, r => ChoicePopup.Build(this, r));

        /// <summary>
        /// Segment start: the business pop-up, then the choice pop-up ON TOP of it (choices are shown first; the offer
        /// pop-up only draws while no choice pop-up is above it, so the two never overlap).
        /// </summary>
        private void PushStartPopups()
        {
            ChoiceError = null;
            if (Session.UnansweredOffers().Count > 0) Stack.Push(OfferUiScreen());
            if (UnansweredChoices().Count > 0) Stack.Push(ChoiceUiScreen());
        }

        // ---- choice events (Story 006)
        /// <summary>Refusal text of the last failed answer (shown in the choice pop-up), else null.</summary>
        public string ChoiceError;
        /// <summary>Choice pop-up visible.</summary>
        public bool ChoicePopupOpen => Stack.Contains(ScreenId.ChoicePopup);

        /// <summary>Pending choices the draft plan does not answer yet, in order.</summary>
        public List<PendingChoice> UnansweredChoices()
        {
            var list = new List<PendingChoice>();
            foreach (var c in Session.PendingChoices) if (!Session.IsChoiceAnswered(c.Id)) list.Add(c);
            return list;
        }

        /// <summary>The choice the pop-up shows: the first unanswered one, else the first one (reviewing an answer).</summary>
        public PendingChoice ShownChoice()
        {
            var open = UnansweredChoices();
            if (open.Count > 0) return open[0];
            return Session.PendingChoices.Count > 0 ? Session.PendingChoices[0] : null;
        }

        /// <summary>True when the option that will apply (answer, else default) puts the player on leave this segment.</summary>
        public bool IsOnLeave(long playerId)
        {
            foreach (var c in Session.PendingChoices)
            {
                if (c.PlayerId != playerId) continue;
                int o = Session.EffectiveChoiceOption(c.Id);
                if (o >= 0 && o < c.Options.Count && c.Options[o].PutsOnLeave) return true;
            }
            return false;
        }

        /// <summary>An event text from the session's text table with player names filled in.</summary>
        public string EventText(string key, long? player, long? partner)
        {
            string t = Session.EventTexts != null ? Session.EventTexts.Get(key) : "[" + key + "]";
            // Display only: no-break spaces, because UI Toolkit otherwise breaks the line at the space after a leading player
            // name and leaves the name alone on the first line (observed in the 1512x950 capture).
            return EventEffectText.Fill(t, player.HasValue ? Session.PlayerName(player.Value) : "", partner.HasValue ? Session.PlayerName(partner.Value) : "")
                .Replace(' ', '\u00A0');
        }

        /// <summary>Answers a choice through the session; a refusal (e.g. leave vs. planned actions) stays in the pop-up.</summary>
        public void AnswerChoice(PendingChoice c, int option)
        {
            if (!Session.TryAnswerChoice(c.Id, option, out var e)) { ChoiceError = ErrorText(e); Say(ChoiceError, true); return; }
            ChoiceError = null;
            if (UnansweredChoices().Count == 0 && Stack.IsTop(ScreenId.ChoicePopup)) Stack.Pop();
            Say(string.Format(UiStrings.ChoiceAnsweredFormat, EventText(c.Options[option].LabelKey, c.PlayerId, c.PartnerId)), false);
        }

        /// <summary>Closes every pop-up (debug demos that show the bare main screen).</summary>
        public void ClosePopups()
        {
            while (Stack.IsTop(ScreenId.OfferPopup) || Stack.IsTop(ScreenId.ChoicePopup) || Stack.IsTop(ScreenId.TrainingPopup)) Stack.Pop();
            Refresh();
        }

        /// <summary>Opens the choice pop-up (badge).</summary>
        public void OpenChoices() { ChoiceError = null; if (!ChoicePopupOpen && Session.PendingChoices.Count > 0) Stack.Push(ChoiceUiScreen()); Refresh(); }
        /// <summary>Closes the choice pop-up; unanswered choices keep their default.</summary>
        public void LaterChoices() { ChoiceError = null; if (Stack.IsTop(ScreenId.ChoicePopup)) Stack.Pop(); Refresh(); }
        private UiScreen PrepUiScreen() => new UiScreen(ScreenId.EventPrep, false, r => EventPrepScreen.Build(this, r));
        private UiScreen RegularUiScreen() => new UiScreen(ScreenId.RegularResult, false, r => RegularResultScreen.Build(this, r));
        private UiScreen BracketUiScreen() => new UiScreen(ScreenId.Bracket, false, r => BracketScreen.Build(this, r));
        private UiScreen SummaryUiScreen() => new UiScreen(ScreenId.Summary, false, r => SummaryScreen.Build(this, r));
        private UiScreen StandingsUiScreen() => new UiScreen(ScreenId.Standings, false, r => StandingsScreen.Build(this, r));

        // ---- training pop-up (Story 012): pick a phase (or command) and an optional focus dimension, see the range, confirm
        /// <summary>Player whose training pop-up is open, or null.</summary>
        public long? TrainingPlayer;
        /// <summary>Target selected in the pop-up.</summary>
        public TrainingTarget TrainingTarget;
        /// <summary>Focus dimension selected in the pop-up (-1 = none).</summary>
        public int TrainingFocus = -1;
        /// <summary>Training pop-up visible.</summary>
        public bool TrainingPopupOpen => Stack.Contains(ScreenId.TrainingPopup);

        /// <summary>Opens the training pop-up for a player, preselecting his most specialized phase without a focus dimension.</summary>
        public void OpenTraining(long id)
        {
            var card = FindCard(id);
            if (card == null) return;
            int k = 0;
            for (int i = 1; i < 3; i++) if (card.Focus[i] > card.Focus[k]) k = i;
            TrainingPlayer = id; TrainingTarget = (TrainingTarget)k; TrainingFocus = -1;
            if (!TrainingPopupOpen) Stack.Push(new UiScreen(ScreenId.TrainingPopup, true, r => TrainingPopup.Build(this, r)));
            Refresh();
        }

        /// <summary>Selects the target in the pop-up (clears the focus dimension).</summary>
        public void SelectTrainingTarget(TrainingTarget t) { TrainingTarget = t; TrainingFocus = -1; Refresh(); }
        /// <summary>Selects the focus dimension (-1 = none).</summary>
        public void SelectTrainingFocus(int dim) { TrainingFocus = dim; Refresh(); }
        /// <summary>Closes the pop-up without adding.</summary>
        public void CloseTraining() { TrainingPlayer = null; if (Stack.IsTop(ScreenId.TrainingPopup)) Stack.Pop(); Refresh(); }
        /// <summary>Adds the selected point; the pop-up closes on success and stays (with the refusal) otherwise.</summary>
        public void ConfirmTraining()
        {
            if (TrainingPlayer == null) return;
            long id = TrainingPlayer.Value;
            if (Train(id, TrainingTarget, TrainingFocus)) { TrainingPlayer = null; if (Stack.IsTop(ScreenId.TrainingPopup)) Stack.Pop(); Refresh(); }
        }

        /// <summary>The card of a rostered player, or null.</summary>
        public PlayerCardData FindCard(long id)
        {
            foreach (var c in Session.GetRosterData()) if (c.Id == id) return c;
            return null;
        }

        // ---- actions forwarded from the screens
        /// <summary>Adds one training point (phase / command + optional focus dimension); true when added.</summary>
        public bool Train(long id, TrainingTarget target, int focus)
        {
            if (Session.TryAddTrain(id, target, focus, out var e))
            {
                string what = focus >= 0
                    ? string.Format(UiStrings.TrainAddedFocusFormat, UiStrings.TargetName(target), Session.DimensionNames(id)[focus])
                    : string.Format(UiStrings.TrainAddedFormat, UiStrings.TargetName(target));
                Say(string.Format(UiStrings.Added, Session.PlayerName(id), what), false);
                return true;
            }
            Say(ErrorText(e), true);
            return false;
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
            if (FlowLocked) return;
            if (!Session.TryValidate(out var e)) { Say(ErrorText(e), true); return; }
            Session.Resolve();
            Banner = null;
            MarkScreenChange();
            // Segments never play matches (numbers.md 4.18); an event after this segment starts from the summary's "next step".
            Stack.Push(SummaryUiScreen());
            Refresh();
        }

        /// <summary>
        /// From the settlement summary: an event waiting for preparation opens the prep screen; otherwise the next segment opens
        /// (Main). Never plays an event.
        /// </summary>
        public void NextSegment()
        {
            if (FlowLocked) return;
            OpenNext();
        }

        private void OpenNext()
        {
            var phase = Session.EventPhase;
            if (phase == EventPhase.Prepare || phase == EventPhase.PlayoffPrep) { ShowEventScreen(PrepUiScreen(), UiStrings.PrepHint); return; }
            if (phase == EventPhase.Done) { ShowEventScreen(BracketUiScreen(), ""); return; }
            if (!Session.TryBeginNext(out var e)) { Say(ErrorText(e), true); return; }
            MarkScreenChange();
            Stack.Reset(MainUiScreen());
            PushStartPopups();
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
        }

        /// <summary>"开赛" / "观看总决赛": plays the next stage of the running event with the current lineup and tactic.</summary>
        public void StartEventStage()
        {
            if (FlowLocked) return;
            PlayStage();
        }

        // The session decides which stage runs; the resulting phase decides the screen.
        private void PlayStage()
        {
            if (!Session.TryPlayNextEventStage(out var err)) { Say(ErrorText(err), true); return; }
            ShowEventScreen(Session.EventPhase == EventPhase.PlayoffPrep ? RegularUiScreen() : BracketUiScreen(), "");
        }

        /// <summary>From the regular-season result: the playoff adjustment when the club is seeded, else the AI playoffs are played.</summary>
        public void GoToPlayoffs()
        {
            if (FlowLocked) return;
            var ev = Session.CurrentEvent?.Event;
            if (Session.EventPhase != EventPhase.PlayoffPrep || ev == null) { Say(ErrorText(new PlanError { Code = PlanErrorCode.WrongEventPhase }), true); return; }
            if (ev.PlayerInBracket) ShowEventScreen(PrepUiScreen(), UiStrings.PrepHint);
            else PlayStage();
        }

        /// <summary>"继续" on the event result: acknowledges the event, opens the next segment; a new year shows its banner.</summary>
        public void ContinueAfterEvent()
        {
            if (FlowLocked) return;
            int year = Session.Season?.Year ?? 1;
            if (!Session.TryAcknowledgeEvent(out var e)) { Say(ErrorText(e), true); return; }
            ScrollToReports = false;
            if (Session.InTransferWindow) { ShowTransfer(); return; } // Story 005: the finals open the transfer window
            if (!Session.TryBeginNext(out var be))
            {
                // Never strand the player on a finished event: go home and show why.
                MarkScreenChange();
                Stack.Reset(MainUiScreen());
                Say(ErrorText(be), true);
                return;
            }
            MarkScreenChange();
            Stack.Reset(MainUiScreen());
            PushStartPopups();
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
            int now = Session.Season?.Year ?? 1;
            if (now > year) { Banner = string.Format(UiStrings.NewYearFormat, now); Refresh(); }
        }

        private void ShowEventScreen(UiScreen screen, string message)
        {
            MarkScreenChange();
            Stack.Replace(screen);
            Message = message; MessageIsError = false;
            Refresh();
        }

        /// <summary>New game: shows only the club naming screen.</summary>
        public void ShowNewClub()
        {
            Stack.Reset(new UiScreen(ScreenId.NewClub, false, r => NewClubScreen.Build(this, r)));
            Refresh();
        }
        /// <summary>Applies the entered club name (normalized by the session) and opens the main screen.</summary>
        public void ConfirmClubName(string raw)
        {
            string name = Session.SetClubName(raw);
            Stack.Reset(MainUiScreen());
            PushStartPopups();
            Message = string.Format(UiStrings.ClubNamedFormat, name); MessageIsError = false;
            Refresh();
        }
        /// <summary>Opens the standings screen (pops back with <see cref="Back"/>).</summary>
        public void OpenStandings() { Stack.Push(StandingsUiScreen()); Refresh(); }
        /// <summary>Pops the top screen.</summary>
        public void Back() { Stack.Pop(); Refresh(); }
        /// <summary>Sets the tactic for the open segment.</summary>
        public void SetTactic(MatchTactic t)
        {
            if (!Session.SetTactic(t, out var e)) { Say(ErrorText(e), true); return; }
            Say(string.Format(UiStrings.TacticSet, UiStrings.TacticName(t)), false);
        }
        /// <summary>Moves a player between lineup and bench.</summary>
        public void ToggleStarter(long id)
        {
            if (!Session.ToggleStarter(id, out var e)) { Say(ErrorText(e), true); return; }
            string lane = null;
            foreach (var slot in Session.GetLineup().Slots) if (slot.PlayerId == id) lane = slot.LaneName;
            Say(lane != null ? string.Format(UiStrings.BenchToggled, Session.PlayerName(id), lane)
                : string.Format(UiStrings.StarterToggled, Session.PlayerName(id)), false);
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
        public void OpenOffers() { if (!OfferPopupOpen && Session.UnansweredOffers().Count > 0) Stack.Push(OfferUiScreen()); Refresh(); }
        public void LaterOffers() { if (Stack.IsTop(ScreenId.OfferPopup)) Stack.Pop(); Refresh(); }

        private void BuildOfferPopup(VisualElement root)
        {
            if (ChoicePopupOpen) return; // a choice pop-up above it is shown first
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
            root.Add(overlay);
        }
    }
}
