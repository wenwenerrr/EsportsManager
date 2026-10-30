using System.Collections.Generic;
using EsportsManager.Leagues;

namespace EsportsManager.UI
{
    /// <summary>
    /// Story 005: the transfer-window flow of the UI controller. After the finals' "继续" the window screen replaces Main; every
    /// click forwards to <see cref="TransferWindow"/> and then lets the window run on (<see cref="TransferWindow.Continue"/>) to
    /// the next decision. When the player ends re-signing the window closes, the next year opens and Main returns with a banner.
    /// No rules here.
    /// </summary>
    public sealed partial class GameUi
    {
        /// <summary>Players picked for listing on the three-choice screen.</summary>
        public HashSet<long> SellSelection { get; } = new HashSet<long>();
        /// <summary>Text of the auto-bid limit field (yuan).</summary>
        public string AutoBidText;
        /// <summary>True while the transfer screen is shown.</summary>
        public bool TransferOpen => Stack.Contains(ScreenId.Transfer);

        private TransferWindow Win => Session.Window;

        private UiScreen TransferUiScreen() => new UiScreen(ScreenId.Transfer, false, r => TransferScreen.Build(this, r));

        /// <summary>Shows the transfer window screen.</summary>
        public void ShowTransfer()
        {
            MarkScreenChange();
            Stack.Reset(TransferUiScreen());
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
        }

        private void After(TransferError e, bool continueWindow = true)
        {
            if (e != TransferError.None) { Say(UiStrings.XferError(e), true); return; }
            if (continueWindow && Win != null) Win.Continue();
            if (!Session.InTransferWindow) { EnterNewYear(); return; }
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
        }

        private void EnterNewYear()
        {
            if (!Session.TryBeginNext(out var be)) { Stack.Reset(MainUiScreen()); Say(ErrorText(be), true); return; }
            MarkScreenChange();
            Stack.Reset(MainUiScreen());
            PushStartPopups();
            Banner = string.Format(UiStrings.NewYearFormat, Session.Season?.Year ?? 1);
            Message = UiStrings.Hint; MessageIsError = false;
            Refresh();
        }

        /// <summary>"下一步": runs the window on (refused while the current list is not fully answered).</summary>
        public void TransferNext()
        {
            if (Win == null) return;
            var w = Win.Waiting;
            if ((w == TransferWait.Renegotiation || w == TransferWait.Renewal) && TransferSummary.HasOpen(Win, w)) { Say(UiStrings.XferUnanswered, true); return; }
            if (w == TransferWait.AuctionReview) { After(Win.ConfirmAuction(), false); return; }
            Win.Continue();
            After(TransferError.None, false);
        }

        /// <summary>Answers a renegotiation request.</summary>
        public void AnswerRenegotiation(long id, bool agree) => After(Win?.AnswerRenegotiation(id, agree) ?? TransferError.WrongStep, false);
        /// <summary>Renews an expired contract.</summary>
        public void Renew(long id, int years) => After(Win?.Renew(id, years) ?? TransferError.WrongStep, false);
        /// <summary>Lets an expired player go.</summary>
        public void ReleasePlayer(long id) => After(Win?.Release(id) ?? TransferError.WrongStep, false);
        /// <summary>Queues a poach offer.</summary>
        public void QueuePoach(long id) => After(Win?.QueuePoach(id) ?? TransferError.WrongStep, false);
        /// <summary>Withdraws a queued poach offer.</summary>
        public void UnqueuePoach(long id) => After(Win?.UnqueuePoach(id) ?? TransferError.WrongStep, false);
        /// <summary>Accepts (true) or retains (false) on the incoming offer.</summary>
        public void RespondPoach(bool accept) => After(Win?.RespondPoach(accept) ?? TransferError.WrongStep);
        /// <summary>Chooses the auction.</summary>
        public void ChooseAuction() => After(Win?.ChooseAuction() ?? TransferError.WrongStep);
        /// <summary>Chooses national scouting for a lane.</summary>
        public void ChooseScout(string lane) => After(Win?.ChooseScout(lane) ?? TransferError.WrongStep);
        /// <summary>Skips the three-choice.</summary>
        public void ChooseSkip() => After(Win?.ChooseSkip() ?? TransferError.WrongStep);
        /// <summary>Toggles a player for listing.</summary>
        public void ToggleSell(long id) { if (!SellSelection.Remove(id)) SellSelection.Add(id); Refresh(); }
        /// <summary>Lists the selected players.</summary>
        public void ConfirmSell()
        {
            var e = Win?.ChooseSell(new List<long>(SellSelection)) ?? TransferError.WrongStep;
            if (e == TransferError.None) SellSelection.Clear();
            After(e);
        }
        /// <summary>Raises once on the running lot.</summary>
        public void BidRaise() => After(Win?.BidRaise() ?? TransferError.WrongStep);
        /// <summary>Leaves the running lot.</summary>
        public void BidExit() => After(Win?.BidExit() ?? TransferError.WrongStep);
        /// <summary>Auto-bids up to the limit typed in the field (yuan).</summary>
        public void BidAuto()
        {
            if (!long.TryParse(AutoBidText, out long x) || x <= 0) { Say(UiStrings.XferError(TransferError.Unknown), true); return; }
            After(Win?.BidAuto(x) ?? TransferError.WrongStep);
        }
        /// <summary>Signs a free agent.</summary>
        public void SignFree(long id) => After(Win?.SignFree(id) ?? TransferError.WrongStep, false);
        /// <summary>"结束转会期".</summary>
        public void FinishWindow()
        {
            if (Win == null) return;
            var e = Win.Waiting != TransferWait.Resign ? TransferError.WrongStep : Win.CanFinish();
            if (e != TransferError.None) { Say(UiStrings.XferError(e), true); return; }
            After(Win?.FinishResign() ?? TransferError.WrongStep, false);
        }
    }
}
