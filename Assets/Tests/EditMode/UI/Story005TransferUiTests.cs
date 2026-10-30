using System.IO;
using System.Linq;
using EsportsManager.Leagues;
using EsportsManager.Session;
using EsportsManager.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace EsportsManager.Tests.UI
{
    /// <summary>Story 005: the transfer window screen driven through <see cref="GameUi"/> (finals -> window -> new year).</summary>
    public class Story005TransferUiTests
    {
        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);

        private static (GameSession s, GameUi ui, VisualElement root) ToWindow(int seed)
        {
            var s = GameSession.FromJson(Data("player_config.json"), Data("team_config.json"), Data("segment_config.json"),
                Data("match_config.json"), Data("event_config.json"), Data("league_config.json"), seed);
            for (int i = 0; i < s.SegmentsPerYear - 1; i++) { s.ApplyScriptedPlan(); s.ResolveAndAdvance(); }
            var root = new VisualElement();
            var ui = new GameUi(root, s, () => 0.0) { FlowGuardSeconds = 0 };
            ui.ClosePopups();
            s.ApplyScriptedPlan();
            ui.Resolve();
            ui.NextSegment();
            ui.StartEventStage();
            ui.ContinueAfterEvent();
            return (s, ui, root);
        }

        [Test]
        public void FinalsContinue_OpensTheTransferScreen_WithStepsAndRunway()
        {
            var (s, ui, root) = ToWindow(20261101);
            Assert.IsTrue(s.InTransferWindow);
            Assert.IsTrue(ui.TransferOpen);
            Assert.IsNotNull(root.Q("transfer-steps"));
            Assert.AreEqual(7, root.Q("transfer-steps").childCount, "更替 → 重谈 → 续约 → 挖角 → 三选一 → 拍卖 → 补签");
            Assert.IsNotNull(root.Q("funds-runway"));
        }

        [Test]
        public void WalkingTheWindowThroughTheUi_EndsInYearTwoMainScreenWithBanner()
        {
            var (s, ui, root) = ToWindow(20261102);
            var win = s.Window;
            var me = s.PlayerClub;
            for (int guard = 0; guard < 300 && s.InTransferWindow; guard++)
            {
                switch (win.Waiting)
                {
                    case TransferWait.Renegotiation:
                        Assert.IsNotNull(root.Q("reneg-list"));
                        Assert.IsNotNull(root.Q("salary-breakdown"));
                        foreach (var r in win.Renegotiations.Where(r => r.ClubId == me.Id && r.Agreed == null).ToList()) ui.AnswerRenegotiation(r.Player.Id, true);
                        ui.TransferNext();
                        break;
                    case TransferWait.Renewal:
                        Assert.IsNotNull(root.Q("renew-list"));
                        foreach (var r in win.Renewals.Where(r => r.ClubId == me.Id && r.Years == null).ToList()) ui.Renew(r.Player.Id, 1);
                        ui.TransferNext();
                        break;
                    case TransferWait.PoachTargets: Assert.IsNotNull(root.Q("poach-list")); ui.TransferNext(); break;
                    case TransferWait.PoachResponse: Assert.IsNotNull(root.Q("poach-popup")); ui.RespondPoach(true); break;
                    case TransferWait.ThreeChoice: Assert.IsNotNull(root.Q("three-choice")); ui.ChooseAuction(); break;
                    case TransferWait.AuctionBid: Assert.IsNotNull(root.Q("bid-panel")); ui.BidExit(); break;
                    case TransferWait.AuctionReview: Assert.IsNotNull(root.Q("auction-review")); ui.TransferNext(); break;
                    case TransferWait.Resign:
                        Assert.IsNotNull(root.Q("resign-list")); Assert.IsNotNull(root.Q("finish-window"));
                        while (win.CanFinish() != TransferError.None)
                        {
                            var miss = s.World.MissingLanes(me);
                            ui.SignFree(win.SignablePool().First(p => miss.Count == 0 || miss.Contains(p.MainLane)).Id);
                        }
                        ui.FinishWindow();
                        break;
                    default: Assert.Fail("unexpected wait " + win.Waiting); break;
                }
            }
            Assert.IsFalse(s.InTransferWindow);
            Assert.AreEqual(2, s.Season.Year);
            Assert.IsFalse(ui.TransferOpen);
            StringAssert.Contains("2", ui.Banner);
        }

        [Test]
        public void TransferNews_HaveTexts()
        {
            var s = GameSession.FromJson(Data("player_config.json"), Data("team_config.json"), Data("segment_config.json"),
                Data("match_config.json"), Data("event_config.json"), Data("league_config.json"), 20261103);
            foreach (var k in new[] { NewsKind.Poach, NewsKind.AuctionBuy, NewsKind.ForcedListing, NewsKind.Retain })
            {
                var t = LeagueWidgets.NewsText(s, new NewsItem { Kind = k, ClubName = "A", OtherClubName = "B", PlayerName = "P", Amount = 1000 });
                StringAssert.Contains("P", t, k.ToString());
                StringAssert.DoesNotContain("{", t);
            }
            Assert.AreEqual(DemoState.XferAuction, DebugArgs.Parse(new[] { "x", "--demo", "xfer-auction" }).Demo);
        }

        [Test]
        public void SalaryBreakdownText_ShowsFivePartsAndTotal()
        {
            var t = TransferScreen.BreakdownText(new SalaryBreakdown { Base = 100, WageIndex = 10, Honor = 20, Specialty = 5, Years = 6, Total = 141 });
            foreach (var part in new[] { "100", "10", "20", "5", "6", "141" }) StringAssert.Contains(part, t);
        }
    }
}
