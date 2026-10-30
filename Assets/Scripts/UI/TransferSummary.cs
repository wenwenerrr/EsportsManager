using System.Collections.Generic;
using EsportsManager.Leagues;
using EsportsManager.Session;

namespace EsportsManager.UI
{
    /// <summary>
    /// Text of the transfer step confirmation (playtest 2026-10-30): the player club's decisions of the step the window waits in
    /// (renegotiation answers, renewals, queued poach offers, the auction result, re-signings). Reads <see cref="TransferWindow"/> only.
    /// </summary>
    public static class TransferSummary
    {
        /// <summary>True while the player club still has an unanswered request in a renegotiation / renewal wait.</summary>
        public static bool HasOpen(TransferWindow win, TransferWait w)
        {
            if (w == TransferWait.Renegotiation) { foreach (var r in win.Renegotiations) if (r.ClubId == win.PlayerClubId && r.Agreed == null) return true; }
            else if (w == TransferWait.Renewal) { foreach (var r in win.Renewals) if (r.ClubId == win.PlayerClubId && r.Years == null) return true; }
            return false;
        }

        /// <summary>The decision lines of the current wait (empty = nothing decided).</summary>
        public static List<string> Lines(GameSession s, TransferWindow win)
        {
            var lines = new List<string>();
            int me = win.PlayerClubId;
            switch (win.Waiting)
            {
                case TransferWait.Renegotiation:
                    foreach (var r in win.Renegotiations)
                        if (r.ClubId == me && r.Agreed != null)
                            lines.Add(r.Agreed.Value ? string.Format(UiStrings.StepAgreeFormat, r.Player.Name, UiKit.N0(r.Market)) : string.Format(UiStrings.StepRefuseFormat, r.Player.Name));
                    break;
                case TransferWait.Renewal:
                    foreach (var r in win.Renewals)
                        if (r.ClubId == me && r.Years != null)
                            lines.Add(r.Years.Value > 0 ? string.Format(UiStrings.StepRenewFormat, r.Player.Name, r.Years.Value, UiKit.N0(r.Ask[r.Years.Value - 1]))
                                : string.Format(UiStrings.StepReleaseFormat, r.Player.Name));
                    break;
                case TransferWait.PoachTargets:
                    foreach (long id in win.PlayerPoachQueue)
                        foreach (var c in s.World.Clubs)
                        {
                            var p = c.Find(id);
                            if (p != null) lines.Add(string.Format(UiStrings.StepPoachFormat, p.Name, c.Name, UiKit.N0(win.PoachFee(s.PlayerClub, p))));
                        }
                    break;
                case TransferWait.AuctionReview:
                    foreach (var l in win.Lots)
                    {
                        if (l.Sold && l.TopBidder == me) lines.Add(string.Format(UiStrings.StepBoughtFormat, l.Player.Name, UiKit.N0(l.Price)));
                        else if (l.Sold && l.SellerId == me) lines.Add(string.Format(UiStrings.StepSoldFormat, l.Player.Name, s.World.Clubs[l.TopBidder].Name, UiKit.N0(l.Price)));
                    }
                    foreach (var u in win.UnsoldSales) if (u.ClubId == me) lines.Add(string.Format(UiStrings.StepUnsoldSaleFormat, u.Player.Name, UiKit.N0(u.Price)));
                    foreach (var u in win.UnsoldKept) if (u.ClubId == me) lines.Add(string.Format(UiStrings.StepUnsoldKeptFormat, u.Player.Name));
                    break;
                case TransferWait.Resign:
                    foreach (var r in win.Resigned) if (r.ClubId == me) lines.Add(string.Format(UiStrings.StepSignedFormat, r.Player.Name, UiKit.N0(r.Player.Salary)));
                    break;
            }
            return lines;
        }
    }
}
