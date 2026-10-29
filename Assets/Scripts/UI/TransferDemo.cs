using System.Collections.Generic;
using System.Linq;
using EsportsManager.Leagues;
using EsportsManager.Players;
using EsportsManager.Session;
using UnityEngine;

namespace EsportsManager.UI
{
    /// <summary>
    /// Story 005 debug demos (screenshots only, never normal play): play year 1 with the scripted plan, walk the finals through the
    /// UI into the transfer window, then answer with defaults until the requested step. A few DEBUG set-ups make sure the step
    /// has something to show for the fixed seed: two players underpaid (renegotiation), two contracts ending (renewal), the AI
    /// clubs given money (an incoming poach offer), one lane emptied at re-sign (empty-lane highlight).
    /// </summary>
    public static class TransferDemo
    {
        /// <summary>Runs the demo set-up and leaves the UI on the requested step.</summary>
        public static void Run(GameUi ui, GameSession s, DemoState demo)
        {
            for (int i = 0; i < s.SegmentsPerYear - 1; i++) { s.ApplyScriptedPlan(); s.ResolveAndAdvance(); }
            var me = s.PlayerClub;
            var w = s.World;
            if (demo == DemoState.XferReneg)
                foreach (var p in me.Team.Roster.Where(p => p.ContractYearsLeft >= 2).Take(2)) p.Salary = p.Salary / 3;
            if (demo == DemoState.XferRenew)
                foreach (var p in me.Team.Roster.Take(2)) p.ContractYearsLeft = 1;
            if (demo == DemoState.XferOffer)
            {
                foreach (var c in w.Clubs) if (c != me) c.Team.Funds += 20_000_000;
                foreach (var st in me.Team.Starters().Take(2))
                {
                    var mate = w.BestFree(new[] { st.MainLane });
                    if (mate != null && me.Team.Roster.Count < 8) w.Sign(me, mate, 2);
                }
            }
            if (demo == DemoState.XferPoach) me.Team.Funds += 2_000_000;
            if ((demo == DemoState.XferAuction || demo == DemoState.XferChoice || demo == DemoState.XferPoach) && me.Team.Roster.Count >= 7)
                foreach (var p in me.Team.Roster.Where(p => me.Team.LaneOf(p) == null).OrderBy(p => p.GetLaneScore(p.MainLane)).Take(me.Team.Roster.Count - 6).ToList())
                { me.Team.RemovePlayer(p); w.FreeAgents.Add(p); }
            s.ApplyScriptedPlan();
            ui.Resolve();
            ui.NextSegment();
            ui.StartEventStage();
            ui.ContinueAfterEvent();
            var win = s.Window;
            if (win == null) { Debug.LogWarning("TransferDemo: the window did not open."); return; }
            var target = demo == DemoState.XferReneg ? TransferWait.Renegotiation : demo == DemoState.XferRenew ? TransferWait.Renewal
                : demo == DemoState.XferPoach ? TransferWait.PoachTargets : demo == DemoState.XferOffer ? TransferWait.PoachResponse
                : demo == DemoState.XferChoice ? TransferWait.ThreeChoice : demo == DemoState.XferAuction ? TransferWait.AuctionBid : TransferWait.Resign;
            for (int guard = 0; guard < 300 && s.InTransferWindow; guard++)
            {
                // the offer demo shows an offer that can still be retained (skips offers to players retained before)
                if (win.Waiting == target && !(target == TransferWait.PoachResponse && !win.PendingIncoming.CanRetain)) break;
                switch (win.Waiting)
                {
                    case TransferWait.Renegotiation:
                        foreach (var r in win.Renegotiations.Where(r => r.ClubId == me.Id && r.Agreed == null).ToList()) win.AnswerRenegotiation(r.Player.Id, true);
                        break;
                    case TransferWait.Renewal:
                        foreach (var r in win.Renewals.Where(r => r.ClubId == me.Id && r.Years == null).ToList()) win.Renew(r.Player.Id, 2);
                        break;
                    case TransferWait.PoachTargets:
                        break;
                    case TransferWait.PoachResponse: win.RespondPoach(!win.PendingIncoming.CanRetain); break;
                    case TransferWait.ThreeChoice: if (demo == DemoState.XferAuction) win.ChooseAuction(); else win.ChooseSkip(); break;
                    case TransferWait.AuctionBid: win.BidExit(); break;
                }
                win.Continue();
            }
            if (win.Waiting != target) Debug.LogWarning("TransferDemo: step " + target + " was not reached (now " + win.Waiting + ").");
            if (demo == DemoState.XferResign && s.InTransferWindow)
            {
                string lane = w.Players.lanes[3].id;
                foreach (var p in me.Team.Roster.Where(p => p.MainLane == lane).ToList()) { me.Team.RemovePlayer(p); w.FreeAgents.Add(p); }
            }
            if (demo == DemoState.XferAuction && win.CurrentLot != null) ui.AutoBidText = ((long)w.Economy.Value(win.CurrentLot.Player)).ToString();
            ui.ShowTransfer();
        }

        /// <summary>Fatigue demo: the player club is treated as the reigning champion for the open segment (media points reserved).</summary>
        public static void Fatigue(GameUi ui, GameSession s)
        {
            var c = s.PlayerClub;
            int g = s.World.GlobalSegment(s.Season.Year, s.Season.SegmentIndexInYear);
            c.Fatigue.Add(new FatigueSpan { From = g, To = g + s.World.Config.economy.fatigueSegments - 1, Strength = 1f });
            s.ApplyScriptedPlan();
            s.ResolveAndAdvance();
            s.ApplyScriptedPlan();
            ui.Refresh();
        }
    }
}
