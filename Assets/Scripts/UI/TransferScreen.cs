using System.Collections.Generic;
using EsportsManager.Leagues;
using EsportsManager.Players;
using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// The transfer window screen (design/gdd/transfer.md UI Requirements, economy.md UI): title with funds and the funds runway,
    /// the step progress (更替 → 重谈 → 续约 → 挖角 → 三选一 → 拍卖 → 补签), a body for the decision the window waits for
    /// (renegotiation / renewal with the salary breakdown, poach targets, three-choice with the lot preview, the bidding panel,
    /// the re-sign list with empty lanes marked) and the poach pop-up as an overlay. Reads <see cref="TransferWindow"/> only.
    /// </summary>
    public static class TransferScreen
    {
        /// <summary>Builds the screen.</summary>
        public static void Build(GameUi ui, VisualElement root)
        {
            var s = ui.Session;
            var win = s.Window ?? s.World?.LastWindow;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = UiKit.Background;
            UiKit.Pad(root, 8);
            if (win == null) return;
            root.Add(Header(ui, win));
            root.Add(Steps(win));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "transfer-scroll";
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.flexBasis = 0; scroll.style.minHeight = 0;
            root.Add(scroll);
            var body = scroll.contentContainer;
            bool canNext = false;
            switch (win.Waiting)
            {
                case TransferWait.Renegotiation: Renegotiation(ui, win, body); canNext = true; break;
                case TransferWait.Renewal: Renewal(ui, win, body); canNext = true; break;
                case TransferWait.PoachTargets: PoachTargets(ui, win, body); break;
                case TransferWait.PoachResponse: PoachTargets(ui, win, body); break;
                case TransferWait.ThreeChoice: ThreeChoice(ui, win, body); break;
                case TransferWait.AuctionBid: Auction(ui, win, body); break;
                case TransferWait.Resign: Resign(ui, win, body); break;
                case TransferWait.AuctionReview: AuctionReview(ui, win, body); canNext = true; break;
                default: body.Add(UiKit.Lbl(UiStrings.XferWatching, 14, UiKit.TextDim)); break;
            }
            root.Add(BottomBar(ui, win, canNext));
            if (win.Waiting == TransferWait.PoachResponse && win.PendingIncoming != null) root.Add(PoachPopup(ui, win.PendingIncoming));
        }

        // ------------------------------------------------------------ auction results (playtest 2026-10-30)

        /// <summary>The auction is over: its result for the player club (confirm with "下一步").</summary>
        private static void AuctionReview(GameUi ui, TransferWindow win, VisualElement body)
        {
            var box = Section(body, UiStrings.StepAuctionDone, UiStrings.StepAuctionNext);
            box.name = "auction-review";
            var lines = TransferSummary.Lines(ui.Session, win);
            if (lines.Count == 0) lines.Add(UiStrings.StepNoDecision);
            foreach (var t in lines) { var r = Line(28); r.Add(Dim(t, 14)); box.Add(r); }
        }

        // ------------------------------------------------------------ frame

        private static VisualElement Header(GameUi ui, TransferWindow win)
        {
            var s = ui.Session;
            var bar = UiKit.Row(40);
            bar.style.flexShrink = 0;
            bar.Add(UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.XferTitleFormat, win.Year), 21, UiKit.Text, true)));
            bar.Add(UiKit.Spacer());
            int runway = s.FundsRunway;
            string funds = UiKit.Yuan(s.Team.Funds);
            var r = UiKit.Lbl(runway == int.MaxValue ? string.Format(UiStrings.XferRunwayInfinite, funds) : string.Format(UiStrings.XferRunwayFormat, funds, runway),
                15, s.Team.Funds < 0 ? UiKit.Bad : runway < 6 ? UiKit.Warn : UiKit.Good, true);
            r.name = "funds-runway"; r.style.flexShrink = 0;
            bar.Add(r);
            return bar;
        }

        private static VisualElement Steps(TransferWindow win)
        {
            var row = UiKit.Row(34);
            row.name = "transfer-steps";
            row.style.flexShrink = 0; row.style.marginBottom = 6;
            int cur = (int)win.Step;
            for (int i = 0; i < UiStrings.XferSteps.Length; i++)
            {
                bool done = i < cur, now = i == cur;
                var chip = UiKit.Block(now ? UiKit.Accent : done ? UiKit.PanelLight : UiKit.Panel, 4);
                chip.style.height = 28; chip.style.flexGrow = 1; chip.style.flexBasis = 0; chip.style.minWidth = 0;
                chip.style.justifyContent = Justify.Center; chip.style.alignItems = Align.Center; chip.style.marginRight = 4;
                var l = UiKit.Ellipsis(UiKit.Lbl(string.Format(UiStrings.XferStepFormat, i + 1, UiStrings.XferSteps[i]), 14, now ? UiKit.Text : done ? UiKit.Good : UiKit.TextDim, now));
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                chip.Add(l);
                row.Add(chip);
            }
            return row;
        }

        private static VisualElement BottomBar(GameUi ui, TransferWindow win, bool canNext)
        {
            var bar = UiKit.Block(UiKit.Panel, 6);
            bar.style.flexDirection = FlexDirection.Row; bar.style.alignItems = Align.Center; bar.style.flexShrink = 0;
            bar.style.height = 48; bar.style.marginTop = 6; UiKit.Pad(bar, 8);
            string info = string.Format(UiStrings.XferRolloverFormat, win.Retired.Count, win.NewPlayers.Count,
                win.WageIndexBefore.ToString("F3"), win.WageIndexAfter.ToString("F3"));
            var msg = UiKit.Ellipsis(UiKit.Lbl(ui.MessageIsError ? ui.Message : info, 13, ui.MessageIsError ? UiKit.Bad : UiKit.TextDim));
            msg.style.flexGrow = 1;
            bar.Add(msg);
            if (canNext)
            {
                var b = UiKit.Btn(UiStrings.XferContinue, ui.TransferNext, UiKit.Accent, true, 15);
                b.name = "transfer-next"; b.style.height = 34; b.style.flexShrink = 0;
                bar.Add(b);
            }
            return bar;
        }

        private static VisualElement Section(VisualElement parent, string title, string hint)
        {
            var box = UiKit.Block(UiKit.Panel, 6);
            UiKit.Pad(box, 10); box.style.marginBottom = 6;
            box.Add(UiKit.Ellipsis(UiKit.Lbl(title, 17, UiKit.Accent, true)));
            if (!string.IsNullOrEmpty(hint))
            {
                var h = UiKit.Lbl(hint, 12, UiKit.TextDim); h.style.whiteSpace = WhiteSpace.Normal; h.style.marginTop = 2; h.style.marginBottom = 4;
                box.Add(h);
            }
            parent.Add(box);
            return box;
        }

        private static VisualElement Line(float h = 30)
        {
            var r = UiKit.Row(h);
            r.style.borderBottomWidth = 1; r.style.borderBottomColor = UiKit.PanelLight;
            return r;
        }

        private static string LaneName(GameSession s, string lane) => s.World.Players.FindLane(lane)?.displayName ?? lane;

        private static string Pot(Player p)
        {
            var e = p.GetPotentialEstimate();
            return string.Format(UiStrings.PotentialRangeFormat, e.Low, e.High);
        }

        private static string Who(GameSession s, Player p) => p.Name + UiStrings.TraitSeparator + LaneName(s, p.MainLane) + UiStrings.TraitSeparator + string.Format(UiStrings.AgeFormat, p.Age);

        /// <summary>"基础 a + 行情 b + 荣誉 c + 绝活 d + 年限 e = total" (economy.md UI: five parts sum to the salary).</summary>
        public static string BreakdownText(SalaryBreakdown b)
            => string.Format(UiStrings.BreakdownFormat, UiKit.Yuan(b.Base), UiKit.Yuan(b.WageIndex), UiKit.Yuan(b.Honor), UiKit.Yuan(b.Specialty), UiKit.Yuan(b.Years), UiKit.Yuan(b.Total));

        private static Label Name(string text, float width)
        {
            var l = UiKit.Ellipsis(UiKit.Lbl(text, 14, UiKit.Text, true));
            l.style.width = width; l.style.flexShrink = 0;
            return l;
        }

        private static Label Dim(string text, int size = 12)
        {
            var l = UiKit.Ellipsis(UiKit.Lbl(text, size, UiKit.TextDim));
            l.style.flexGrow = 1;
            return l;
        }

        // ------------------------------------------------------------ 2 renegotiation

        private static void Renegotiation(GameUi ui, TransferWindow win, VisualElement body)
        {
            var s = ui.Session;
            var box = Section(body, UiStrings.RenegTitle, UiStrings.RenegHint);
            box.name = "reneg-list";
            foreach (var r in win.Renegotiations)
            {
                if (r.ClubId != s.PlayerClub.Id) continue;
                var p = r.Player;
                var top = Line(30);
                top.Add(Name(Who(s, p), 230));
                top.Add(Dim(string.Format(UiStrings.RenegLineFormat, UiKit.Yuan(r.Current), UiKit.Yuan(r.Market), p.ContractYearsLeft), 13));
                if (r.Agreed == null)
                {
                    top.Add(UiKit.Btn(UiStrings.Agree, () => ui.AnswerRenegotiation(p.Id, true), UiKit.Good));
                    top.Add(UiKit.Btn(UiStrings.Refuse, () => ui.AnswerRenegotiation(p.Id, false), UiKit.Bad));
                }
                else top.Add(UiKit.Lbl(r.Agreed.Value ? UiStrings.Agreed : UiStrings.Refused, 13, r.Agreed.Value ? UiKit.Good : UiKit.Warn, true));
                box.Add(top);
                var b = Line(22);
                var bl = Dim(BreakdownText(r.Breakdown), 12); bl.name = "salary-breakdown";
                b.Add(bl);
                box.Add(b);
            }
        }

        // ------------------------------------------------------------ 3 renewals

        private static void Renewal(GameUi ui, TransferWindow win, VisualElement body)
        {
            var s = ui.Session;
            var box = Section(body, UiStrings.RenewTitle, UiStrings.RenewHint);
            box.name = "renew-list";
            foreach (var r in win.Renewals)
            {
                if (r.ClubId != s.PlayerClub.Id) continue;
                var p = r.Player;
                var top = Line(32);
                top.Add(Name(Who(s, p), 230));
                top.Add(Dim(string.Format(UiStrings.RenewOldFormat, UiKit.Yuan(r.OldSalary)) + UiStrings.PartSeparator + string.Format(UiStrings.PotentialShortFormat, Pot(p)), 12));
                if (r.Years == null)
                {
                    for (int y = 1; y <= 3; y++)
                    {
                        int yy = y;
                        top.Add(UiKit.Btn(string.Format(UiStrings.RenewYearsFormat, y, UiKit.Yuan(r.Ask[y - 1])), () => ui.Renew(p.Id, yy), UiKit.Good, true, 12));
                    }
                    top.Add(UiKit.Btn(UiStrings.ReleaseBtn, () => ui.ReleasePlayer(p.Id), UiKit.Bad, true, 12));
                }
                else top.Add(UiKit.Lbl(r.Years > 0 ? string.Format(UiStrings.RenewedFormat, r.Years, UiKit.Yuan(r.Ask[r.Years.Value - 1])) : UiStrings.Released, 13,
                    r.Years > 0 ? UiKit.Good : UiKit.Warn, true));
                box.Add(top);
                for (int y = 1; y <= 3; y++)
                {
                    var b = Line(20);
                    var bl = Dim(string.Format(UiStrings.ContractYearsFormat, y) + UiStrings.PartSeparator + BreakdownText(r.Breakdown[y - 1]), 12);
                    bl.name = "salary-breakdown";
                    b.Add(bl);
                    box.Add(b);
                }
            }
        }

        // ------------------------------------------------------------ 4 poaching

        private static void PoachTargets(GameUi ui, TransferWindow win, VisualElement body)
        {
            var s = ui.Session;
            int max = s.World.Config.transfer.poachOffersPerClub;
            var box = Section(body, UiStrings.PoachTitle, string.Format(UiStrings.PoachHintFormat, max, win.PlayerPoachQueue.Count));
            box.name = "poach-list";
            bool open = win.Waiting == TransferWait.PoachTargets;
            foreach (long id in win.PlayerPoachQueue)
            {
                Player p = null; Club owner = null;
                foreach (var c in s.World.Clubs) { var q = c.Find(id); if (q != null) { p = q; owner = c; } }
                if (p == null) continue;
                var row = Line(30);
                row.Add(Name(Who(s, p), 230));
                row.Add(Dim(owner.Name + UiStrings.PartSeparator + string.Format(UiStrings.PoachFeeFormat, UiKit.Yuan(win.PoachFee(s.PlayerClub, p))), 13));
                if (open) row.Add(UiKit.Btn(UiStrings.PoachUnqueue, () => ui.UnqueuePoach(id), UiKit.Warn));
                box.Add(row);
            }
            var list = open ? win.PlayerPoachTargets() : new List<PoachOffer>();
            bool full = s.Team.Roster.Count >= s.World.Config.ai.maxRoster;
            if (open && list.Count == 0) box.Add(Dim(full ? UiStrings.PoachRosterFull : UiStrings.PoachNone, 13));
            int n = 0;
            foreach (var o in list)
            {
                if (n++ >= 14) break;
                var p = o.Player;
                var row = Line(30);
                row.Add(Name(Who(s, p), 230));
                row.Add(Dim(string.Format(UiStrings.PoachLineFormat, s.World.Clubs[o.SellerId].Name, UiKit.Int(p.GetLaneScore(p.MainLane)),
                    UiKit.Int(s.World.Economy.Honor(p)), UiKit.Yuan(o.Fee), UiKit.Yuan(o.NewSalary)), 12));
                row.Add(UiKit.Btn(UiStrings.PoachQueue, () => ui.QueuePoach(p.Id), UiKit.Accent, win.PlayerPoachQueue.Count < max));
                box.Add(row);
            }
            if (open)
            {
                var go = UiKit.Btn(UiStrings.PoachStart, ui.TransferNext, UiKit.Good, true, 14);
                go.name = "poach-go"; go.style.height = 32; go.style.marginTop = 6; go.style.alignSelf = Align.FlexStart;
                box.Add(go);
            }
        }

        private static VisualElement PoachPopup(GameUi ui, PoachOffer o)
        {
            var s = ui.Session;
            var overlay = new VisualElement();
            overlay.name = "poach-popup";
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            overlay.style.alignItems = Align.Center; overlay.style.justifyContent = Justify.Center;
            var box = UiKit.Block(UiKit.Panel, 8);
            box.style.width = 600; box.style.maxWidth = Length.Percent(92); UiKit.Pad(box, 18);
            box.style.borderTopWidth = 4; box.style.borderTopColor = UiKit.Warn;
            box.Add(UiKit.Lbl(UiStrings.PoachPopupTitle, 20, UiKit.Warn, true));
            var p = o.Player;
            Label L(string t, Color c, bool bold = false) { var l = UiKit.Lbl(t, 15, c, bold); l.style.whiteSpace = WhiteSpace.Normal; l.style.marginTop = 6; return l; }
            box.Add(L(string.Format(UiStrings.PoachPopupBodyFormat, s.World.Clubs[o.BuyerId].Name, p.Name, LaneName(s, p.MainLane)), UiKit.Text));
            box.Add(L(string.Format(UiStrings.PoachFeeFormat, UiKit.Yuan(o.Fee)), UiKit.Good, true));
            box.Add(L(string.Format(UiStrings.PoachRetainFormat, UiKit.Yuan(p.Salary), UiKit.Yuan(o.RetainSalary)), UiKit.Text));
            string sat = o.Satisfaction < 0f ? UiStrings.SatisfactionLow : o.Satisfaction > 0f ? UiStrings.SatisfactionHigh : UiStrings.SatisfactionOk;
            box.Add(L(string.Format(UiStrings.PoachValueFormat, UiKit.Yuan(o.Value), sat), UiKit.TextDim));
            if (!o.CanRetain) box.Add(L(UiStrings.PoachRetainUsed, UiKit.Bad));
            int runway = s.FundsRunway;
            box.Add(L(runway == int.MaxValue ? string.Format(UiStrings.XferRunwayInfinite, UiKit.Yuan(s.Team.Funds))
                : string.Format(UiStrings.XferRunwayFormat, UiKit.Yuan(s.Team.Funds), runway), UiKit.TextDim));
            var buttons = UiKit.Row(34);
            buttons.style.marginTop = 14;
            var a = UiKit.Btn(UiStrings.PoachAccept, () => ui.RespondPoach(true), UiKit.Good, true, 15); a.name = "poach-accept"; a.style.height = 32;
            var r = UiKit.Btn(UiStrings.PoachRetain, () => ui.RespondPoach(false), UiKit.Accent, o.CanRetain, 15); r.name = "poach-retain"; r.style.height = 32;
            buttons.Add(a); buttons.Add(r);
            box.Add(buttons);
            overlay.Add(box);
            return overlay;
        }

        // ------------------------------------------------------------ 5 three-choice

        private static string LotKindText(GameSession s, AuctionLot l)
        {
            if (l.Kind == LotKind.Youth) return UiStrings.LotKindYouth;
            if (l.Kind == LotKind.Free) return UiStrings.LotKindFree;
            return string.Format(l.Forced ? UiStrings.LotKindForcedFormat : UiStrings.LotKindListedFormat, s.World.Clubs[l.SellerId].Name);
        }

        private static string LotLine(GameSession s, AuctionLot l)
        {
            var p = l.Player;
            return string.Format(UiStrings.LotLineFormat, LotKindText(s, l), LaneName(s, p.MainLane), p.Age, Pot(p),
                UiKit.Yuan((long)s.World.Economy.Value(p)), UiKit.Yuan(l.StartPrice));
        }

        private static void ThreeChoice(GameUi ui, TransferWindow win, VisualElement body)
        {
            var s = ui.Session;
            var me = s.PlayerClub;
            var top = Section(body, UiStrings.XferChoiceTitle, null);
            top.name = "three-choice";
            var cols = UiKit.Row();
            cols.style.alignItems = Align.FlexStart; cols.style.marginTop = 8;
            top.Add(cols);
            VisualElement Col(string title, string hint, string name)
            {
                var c = UiKit.Block(UiKit.PanelLight, 6);
                c.name = name;
                c.style.flexGrow = 1; c.style.flexBasis = 0; c.style.minWidth = 0; c.style.marginRight = 6; UiKit.Pad(c, 8);
                c.Add(UiKit.Ellipsis(UiKit.Lbl(title, 16, UiKit.Text, true)));
                var h = UiKit.Lbl(hint, 12, UiKit.TextDim); h.style.whiteSpace = WhiteSpace.Normal; h.style.marginBottom = 4; c.Add(h);
                cols.Add(c);
                return c;
            }
            // auction + lot preview
            var a = Col(UiStrings.ChoiceAuction, UiStrings.ChoiceAuctionHint, "choice-auction");
            a.Add(UiKit.Lbl(UiStrings.LotsPreview, 12, UiKit.Accent, true));
            foreach (var l in win.Lots)
            {
                var nm = UiKit.Ellipsis(UiKit.Lbl(l.Player.Name, 13, UiKit.Text, true)); nm.style.marginTop = 4; a.Add(nm);
                var ln = UiKit.Lbl(LotLine(s, l), 11, UiKit.TextDim); ln.style.whiteSpace = WhiteSpace.Normal; a.Add(ln);
            }
            var ab = UiKit.Btn(UiStrings.ChoiceAuction, ui.ChooseAuction, UiKit.Good, true, 14); ab.style.marginTop = 6; ab.style.height = 30; ab.name = "choose-auction";
            a.Add(ab);
            // scouting per lane
            var sc = Col(UiStrings.ChoiceScout, string.Format(UiStrings.ChoiceScoutHintFormat, UiKit.Yuan((long)s.World.Config.talent.scoutCost)), "choice-scout");
            foreach (var lane in s.World.Players.lanes)
            {
                var r = UiKit.Row(28);
                bool need = s.World.ScoutChance(me, lane.id) > s.World.Config.talent.scoutT0;
                var nl = UiKit.Ellipsis(UiKit.Lbl(lane.displayName, 14, UiKit.Text, true)); nl.style.width = 60; r.Add(nl);
                r.Add(Dim(need ? UiStrings.ScoutNeedHigh : UiStrings.ScoutNeedLow, 12));
                string id = lane.id;
                r.Add(UiKit.Btn(UiStrings.ChoiceScout, () => ui.ChooseScout(id), UiKit.Accent, win.PlayerCanScout, 12));
                sc.Add(r);
            }
            // selling
            var se = Col(UiStrings.ChoiceSell, UiStrings.ChoiceSellHint, "choice-sell");
            foreach (var p in me.Team.Roster)
            {
                var r = UiKit.Row(26);
                bool ok = win.CanList(me, p, 0);
                bool sel = ui.SellSelection.Contains(p.Id);
                var nl = UiKit.Ellipsis(UiKit.Lbl(p.Name + UiStrings.TraitSeparator + LaneName(s, p.MainLane), 13, ok ? UiKit.Text : UiKit.TextDim, sel));
                nl.style.flexGrow = 1; r.Add(nl);
                var t = UiKit.Btn(sel ? UiStrings.SellPicked : UiStrings.SellPick, () => ui.ToggleSell(p.Id), sel ? UiKit.Warn : UiKit.Panel, ok || sel, 12);
                t.style.width = 52; r.Add(t);
                se.Add(r);
            }
            int nsel = ui.SellSelection.Count;
            var sb = UiKit.Btn(string.Format(UiStrings.ChoiceSellConfirmFormat, nsel), ui.ConfirmSell, UiKit.Warn, nsel >= 1 && nsel <= s.World.Config.transfer.maxListed, 13);
            sb.style.marginTop = 6; sb.style.height = 28; se.Add(sb);
            var skip = UiKit.Btn(UiStrings.ChoiceSkip, ui.ChooseSkip, UiKit.PanelLight, true, 14);
            skip.name = "choose-skip"; skip.style.height = 30; skip.style.alignSelf = Align.FlexStart; skip.style.marginTop = 6;
            top.Add(skip);
        }

        // ------------------------------------------------------------ 6 auction

        private static void Auction(GameUi ui, TransferWindow win, VisualElement body)
        {
            var s = ui.Session;
            var lot = win.CurrentLot;
            var row = UiKit.Row();
            row.style.alignItems = Align.FlexStart;
            body.Add(row);
            // lot list
            var list = UiKit.Block(UiKit.Panel, 6);
            list.name = "lot-list";
            list.style.flexGrow = 1; list.style.flexBasis = 0; list.style.minWidth = 0; list.style.marginRight = 6; UiKit.Pad(list, 10);
            list.Add(UiKit.Lbl(UiStrings.AuctionTitle, 17, UiKit.Accent, true));
            foreach (var l in win.Lots)
            {
                var r = Line(40);
                r.style.flexDirection = FlexDirection.Column; r.style.alignItems = Align.Stretch; r.style.justifyContent = Justify.Center;
                string status = l.Finished ? (l.Sold ? string.Format(UiStrings.AuctionSoldFormat, s.World.Clubs[l.TopBidder].Name, UiKit.Yuan(l.Price)) : UiStrings.AuctionUnsold)
                    : l == lot ? UiStrings.AuctionLive : UiStrings.AuctionPending;
                var n = UiKit.Ellipsis(UiKit.Lbl(l.Player.Name + UiStrings.TraitSeparator + status, 13, l == lot ? UiKit.Warn : UiKit.Text, l == lot));
                r.Add(n);
                r.Add(UiKit.Ellipsis(UiKit.Lbl(LotLine(s, l), 11, UiKit.TextDim)));
                list.Add(r);
            }
            row.Add(list);
            if (lot == null) return;
            // bidding panel
            var panel = UiKit.Block(UiKit.Panel, 6);
            panel.name = "bid-panel";
            panel.style.width = 470; panel.style.flexShrink = 0; UiKit.Pad(panel, 12);
            panel.style.borderTopWidth = 3; panel.style.borderTopColor = UiKit.Warn;
            var p = lot.Player;
            panel.Add(UiKit.Ellipsis(UiKit.Lbl(Who(s, p), 18, UiKit.Text, true)));
            var d = UiKit.Lbl(LotLine(s, lot), 12, UiKit.TextDim); d.style.whiteSpace = WhiteSpace.Normal; panel.Add(d);
            long next = win.NextBid(lot);
            var price = UiKit.Lbl(string.Format(UiStrings.AuctionPriceFormat, UiKit.Yuan(lot.Price), UiKit.Yuan(next)), 17, UiKit.Good, true);
            price.style.marginTop = 8; price.name = "bid-price"; panel.Add(price);
            panel.Add(UiKit.Ellipsis(UiKit.Lbl(lot.TopBidder >= 0 ? string.Format(UiStrings.AuctionTopFormat, s.World.Clubs[lot.TopBidder].Name) : UiStrings.AuctionNobody, 14, UiKit.Text)));
            var names = new List<string>();
            foreach (int id in lot.InClubs) names.Add(s.World.Clubs[id].Name);
            var inl = UiKit.Lbl(string.Format(UiStrings.AuctionInFormat, string.Join(UiStrings.ListSeparator, names)), 13, UiKit.TextDim);
            inl.style.whiteSpace = WhiteSpace.Normal; panel.Add(inl);
            bool can = win.PlayerCanBid(lot);
            if (!can) { var w = UiKit.Lbl(UiStrings.AuctionCantBid, 12, UiKit.Bad); w.style.whiteSpace = WhiteSpace.Normal; panel.Add(w); }
            var b1 = UiKit.Row(34); b1.style.marginTop = 8;
            var raise = UiKit.Btn(string.Format(UiStrings.AuctionRaiseFormat, UiKit.Yuan(next)), ui.BidRaise, UiKit.Good, can, 14); raise.name = "bid-raise"; raise.style.height = 30;
            var exit = UiKit.Btn(UiStrings.AuctionExit, ui.BidExit, UiKit.Bad, true, 14); exit.name = "bid-exit"; exit.style.height = 30;
            b1.Add(raise); b1.Add(exit);
            panel.Add(b1);
            var b2 = UiKit.Row(34); b2.style.marginTop = 6;
            var lbl = UiKit.Lbl(UiStrings.AuctionAutoLabel, 12, UiKit.TextDim); lbl.style.marginRight = 6; b2.Add(lbl);
            if (string.IsNullOrEmpty(ui.AutoBidText)) ui.AutoBidText = ((long)s.World.Economy.Value(p)).ToString();
            var field = new TextField { value = ui.AutoBidText };
            field.name = "bid-limit";
            field.style.width = 130; field.style.height = 28; field.style.fontSize = 14; field.style.marginRight = 6;
            field.Query<VisualElement>().ForEach(e =>
            {
                if (e == field) return;
                e.style.backgroundColor = UiKit.PanelLight; e.style.color = UiKit.Text; e.style.unityTextAlign = TextAnchor.MiddleLeft;
                e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 0;
            });
            field.RegisterValueChangedCallback(e => ui.AutoBidText = e.newValue);
            b2.Add(field);
            var auto = UiKit.Btn(UiStrings.AuctionAuto, ui.BidAuto, UiKit.Accent, can, 13); auto.name = "bid-auto"; auto.style.height = 28;
            b2.Add(auto);
            panel.Add(b2);
            row.Add(panel);
        }

        // ------------------------------------------------------------ 7 re-sign

        private static void Resign(GameUi ui, TransferWindow win, VisualElement body)
        {
            var s = ui.Session;
            var me = s.PlayerClub;
            var missing = s.World.MissingLanes(me);
            var miss = new List<string>();
            foreach (var l in missing) miss.Add(LaneName(s, l));
            var row = UiKit.Row();
            row.style.alignItems = Align.FlexStart;
            body.Add(row);
            var box = UiKit.Block(UiKit.Panel, 6);
            box.name = "resign-list";
            box.style.flexGrow = 1; box.style.flexBasis = 0; box.style.minWidth = 0; box.style.marginRight = 6; UiKit.Pad(box, 10);
            box.Add(UiKit.Ellipsis(UiKit.Lbl(UiStrings.ResignTitle, 17, UiKit.Accent, true)));
            var hint = UiKit.Lbl(string.Format(UiStrings.ResignHintFormat, me.Team.Roster.Count, miss.Count == 0 ? UiStrings.ResignNoMissing : string.Join(UiStrings.ListSeparator, miss)), 13,
                miss.Count > 0 ? UiKit.Bad : UiKit.TextDim, miss.Count > 0);
            hint.name = "resign-missing"; hint.style.marginTop = 4; hint.style.marginBottom = 4; box.Add(hint);
            var pool = win.SignablePool();
            pool.Sort((x, y) =>
            {
                int a = missing.Contains(x.MainLane) ? 0 : 1, b = missing.Contains(y.MainLane) ? 0 : 1;
                return a != b ? a.CompareTo(b) : y.GetLaneScore(y.MainLane).CompareTo(x.GetLaneScore(x.MainLane));
            });
            bool full = me.Team.Roster.Count >= s.World.Config.ai.maxRoster;
            int n = 0;
            foreach (var p in pool)
            {
                if (n++ >= 16) break;
                bool need = missing.Contains(p.MainLane);
                var r = Line(30);
                if (need) { var tag = UiKit.Lbl(UiStrings.ResignMissingTag, 11, UiKit.Bad, true); tag.style.width = 48; tag.style.flexShrink = 0; r.Add(tag); }
                var ask = win.ResignAsk(p);
                r.Add(Dim(string.Format(UiStrings.ResignLineFormat, p.Name, LaneName(s, p.MainLane), p.Age, UiKit.Int(p.GetLaneScore(p.MainLane)), Pot(p), UiKit.Yuan(ask.Total)), 13));
                long id = p.Id;
                r.Add(UiKit.Btn(UiStrings.ResignSign, () => ui.SignFree(id), need ? UiKit.Warn : UiKit.Accent, !full, 12));
                box.Add(r);
            }
            row.Add(box);
            // my roster + finish
            var side = UiKit.Block(UiKit.Panel, 6);
            side.name = "resign-roster";
            side.style.width = 330; side.style.flexShrink = 0; UiKit.Pad(side, 10);
            side.Add(UiKit.Lbl(UiStrings.RosterTitle, 16, UiKit.Accent, true));
            foreach (var lane in s.World.Players.lanes)
            {
                var r = UiKit.Row(24);
                var ln = UiKit.Lbl(lane.displayName, 13, missing.Contains(lane.id) ? UiKit.Bad : UiKit.TextDim, true); ln.style.width = 48; ln.style.flexShrink = 0; r.Add(ln);
                var names = new List<string>();
                foreach (var p in me.Team.Roster) if (p.MainLane == lane.id) names.Add(p.Name);
                r.Add(UiKit.Ellipsis(UiKit.Lbl(names.Count == 0 ? UiStrings.EmptyLane : string.Join(UiStrings.ListSeparator, names), 13, UiKit.Text)));
                side.Add(r);
            }
            bool ok = win.CanFinish() == TransferError.None;
            if (!ok) { var w = UiKit.Lbl(UiStrings.ResignBlocked, 12, UiKit.Bad); w.style.whiteSpace = WhiteSpace.Normal; w.style.marginTop = 6; side.Add(w); }
            var fin = UiKit.Btn(UiStrings.ResignFinish, ui.FinishWindow, UiKit.Good, ok, 15);
            fin.name = "finish-window"; fin.style.height = 34; fin.style.marginTop = 8;
            side.Add(fin);
            row.Add(side);
        }
    }
}
