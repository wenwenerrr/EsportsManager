using System.Collections.Generic;
using System.Linq;
using EsportsManager.Leagues;
using EsportsManager.Players;

namespace EsportsManager.Tests.Transfer
{
    /// <summary>Story 005 fixtures: hand-made players and a scripted way through a window's player decisions.</summary>
    internal static class TransferTestKit
    {
        private static long _next = 900000;

        /// <summary>
        /// A player whose main-lane dimensions all equal <paramref name="rating"/> (so R_main = rating), potential estimate centre
        /// = <paramref name="potential"/> + <paramref name="bias"/> (observation 0), traits without ask-price modifiers.
        /// </summary>
        public static Player MakePlayer(LeagueWorld w, string lane, int age, float rating, float potential = 75f, float popularity = 60f,
            float bias = 0f, int contract = 2, int salary = 100000)
        {
            var radar = new Dictionary<string, float[]>();
            foreach (var l in w.Players.lanes) radar[l.id] = Enumerable.Repeat(l.id == lane ? rating : 30f, l.dimensions.Length).ToArray();
            long id = _next++;
            return new Player(w.Players, id, "T" + id, age, lane, "reserved", "diligent", potential, contract, 50f, popularity, 60f, 20f, salary, radar, bias);
        }

        /// <summary>Adds a hand-made player to a club (bench).</summary>
        public static Player AddPlayer(LeagueWorld w, Club c, string lane, int age, float rating, int contract = 3, int salary = 100000)
        {
            var p = MakePlayer(w, lane, age, rating, contract: contract, salary: salary);
            c.Team.AddPlayer(p);
            w.Invalidate();
            return p;
        }

        /// <summary>
        /// Answers the window's player decisions with simple defaults until it waits for <paramref name="target"/> (or ends):
        /// agree to renegotiations, renew 1 year, no poach offers, retain when possible, skip the three-choice, exit lots,
        /// finish re-signing (signing the best free agents first when the roster is incomplete).
        /// </summary>
        public static void AdvanceTo(TransferWindow win, LeagueWorld w, TransferWait target)
        {
            for (int guard = 0; guard < 500 && win.Step != TransferStep.Done; guard++)
            {
                if (win.Waiting == target) return;
                var me = w.Clubs[win.PlayerClubId];
                switch (win.Waiting)
                {
                    case TransferWait.Renegotiation:
                        foreach (var r in win.Renegotiations.Where(r => r.ClubId == me.Id && r.Agreed == null).ToList()) win.AnswerRenegotiation(r.Player.Id, true);
                        break;
                    case TransferWait.Renewal:
                        foreach (var r in win.Renewals.Where(r => r.ClubId == me.Id && r.Years == null).ToList()) win.Renew(r.Player.Id, 1);
                        break;
                    case TransferWait.PoachResponse:
                        win.RespondPoach(!win.PendingIncoming.CanRetain);
                        break;
                    case TransferWait.ThreeChoice: win.ChooseSkip(); break;
                    case TransferWait.AuctionBid: win.BidExit(); break;
                    case TransferWait.AuctionReview: win.ConfirmAuction(); break;
                    case TransferWait.Resign:
                        while (win.CanFinish() != TransferError.None)
                        {
                            var miss = w.MissingLanes(me);
                            var pick = win.SignablePool().FirstOrDefault(p => miss.Count == 0 || miss.Contains(p.MainLane));
                            if (pick == null || win.SignFree(pick.Id) != TransferError.None) break;
                        }
                        win.FinishResign();
                        continue;
                }
                win.Continue();
            }
        }
    }
}
