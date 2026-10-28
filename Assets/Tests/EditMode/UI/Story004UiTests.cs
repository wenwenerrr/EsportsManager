using System;
using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Segments;
using EsportsManager.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace EsportsManager.Tests.UI
{
    /// <summary>Story 004 stage B: pure UI helpers (screen stack, reason text, debug args, error text, counter hint).</summary>
    public class Story004UiTests
    {
        private static UiScreen Fake(ScreenId id, bool overlay, List<ScreenId> log) => new UiScreen(id, overlay, r => log.Add(id));

        [Test]
        public void ScreenStack_PushPopReplace_RendersBaseAndOverlays()
        {
            var log = new List<ScreenId>();
            var st = new ScreenStack();
            st.Reset(Fake(ScreenId.Main, false, log));
            st.Push(Fake(ScreenId.OfferPopup, true, log));
            st.Render(new VisualElement());
            CollectionAssert.AreEqual(new[] { ScreenId.Main, ScreenId.OfferPopup }, log);

            Assert.IsTrue(st.Pop());
            st.Push(Fake(ScreenId.Bracket, false, log));
            log.Clear(); st.Render(new VisualElement());
            CollectionAssert.AreEqual(new[] { ScreenId.Bracket }, log, "a full screen hides the one below");

            st.Replace(Fake(ScreenId.Summary, false, log));
            Assert.IsTrue(st.IsTop(ScreenId.Summary));
            Assert.AreEqual(2, st.Count);
            Assert.IsFalse(st.Contains(ScreenId.Bracket));
            Assert.IsTrue(st.Pop());
            Assert.IsFalse(st.Pop(), "the bottom screen is never popped");
            Assert.IsTrue(st.IsTop(ScreenId.Main));
        }

        [Test]
        public void ScreenStack_Render_ResetsRootLayoutLeftByPreviousScreen()
        {
            // Regression (user playtest 2026-10-19): the new-club screen centred the shared root and every later
            // screen was shrink-wrapped and centred, so the prep screen's roster and tactic panel overlapped.
            var root = new VisualElement();
            var st = new ScreenStack();
            st.Reset(new UiScreen(ScreenId.Main, false, r => { r.style.alignItems = Align.Center; r.style.justifyContent = Justify.Center; }));
            st.Render(root);
            Assert.AreEqual(Align.Center, root.style.alignItems.value);

            StyleEnum<Align> seenAlign = default; StyleEnum<Justify> seenJustify = default;
            st.Replace(new UiScreen(ScreenId.Summary, false, r => { seenAlign = r.style.alignItems; seenJustify = r.style.justifyContent; }));
            st.Render(root);
            Assert.AreEqual(StyleKeyword.Null, seenAlign.keyword, "alignItems must be reset before the next screen builds");
            Assert.AreEqual(StyleKeyword.Null, seenJustify.keyword, "justifyContent must be reset before the next screen builds");
        }

        [Test]
        public void ScreenStack_Render_ClearsRootFirst()
        {
            var root = new VisualElement();
            root.Add(new VisualElement());
            var st = new ScreenStack();
            st.Reset(new UiScreen(ScreenId.Main, false, r => r.Add(new Label("x"))));
            st.Render(root);
            Assert.AreEqual(1, root.childCount);
        }

        private static readonly MatchFactor[] Factors = (MatchFactor[])Enum.GetValues(typeof(MatchFactor));

        [Test]
        public void ReasonText_EveryFactorAndSign_IsNonEmptyAndSignsDiffer()
        {
            var subj = new ReasonSubject("Ace", "Mid");
            foreach (var f in Factors)
                foreach (bool has in new[] { false, true })
                {
                    string pos = ReasonText.For(new MatchReason(f, 3f, has, 1, true), subj, MatchTactic.Invade, MatchTactic.Develop);
                    string neg = ReasonText.For(new MatchReason(f, -3f, has, 1, false), subj, MatchTactic.Invade, MatchTactic.Develop);
                    string even = ReasonText.For(new MatchReason(f, 0.01f, has, 1), subj, MatchTactic.Invade, MatchTactic.Develop);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(even), f + " even");
                    Assert.AreNotEqual(pos, even, f + " even must differ from a clear advantage");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(pos), f + " +");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(neg), f + " -");
                    Assert.AreNotEqual(pos, neg, f + " sign must change the sentence");
                    Assert.IsFalse(pos.Contains("{") || neg.Contains("{"), f + " unformatted placeholder");
                }
        }

        [Test]
        public void ReasonText_Subject_NameAndLaneAppear_TacticNamesAppear()
        {
            var subj = new ReasonSubject("Ace", "Mid");
            string early = ReasonText.For(new MatchReason(MatchFactor.Early, -2f, true, 7, false, "mid"), subj, MatchTactic.Invade, MatchTactic.Invade);
            StringAssert.Contains("Ace", early); StringAssert.Contains("Mid", early);
            string tactic = ReasonText.For(new MatchReason(MatchFactor.Tactic, -1.5f), default, MatchTactic.Invade, MatchTactic.Teamfight);
            StringAssert.Contains(UiStrings.TacticInvade, tactic); StringAssert.Contains(UiStrings.TacticTeamfight, tactic);
        }

        [Test]
        public void ReasonText_LuckReason_NamesGameAndChance()
        {
            string t = ReasonText.For(MatchReason.Luck(1, 0.62f, true, -3.5f), default, MatchTactic.Invade, MatchTactic.Develop);
            Assert.AreEqual(string.Format(UiStrings.LuckUnluckyFormat, 2, "62"), t);
            string l = ReasonText.For(MatchReason.Luck(0, 0.7f, false, 2f), default, MatchTactic.Invade, MatchTactic.Develop);
            Assert.AreEqual(string.Format(UiStrings.LuckLuckyFormat, 1, "70"), l);
        }

        [Test]
        public void ReasonText_WithValue_AppendsSignedPoints()
        {
            StringAssert.EndsWith(string.Format(UiStrings.ReasonValueFormat, "+2.3"), ReasonText.WithValue("x", 2.31f));
            StringAssert.EndsWith(string.Format(UiStrings.ReasonValueFormat, "-1.0"), ReasonText.WithValue("x", -1f));
        }

        [Test]
        public void ClubName_Normalize_TrimsCutsAndDefaults()
        {
            var club = Matches.MatchTestFactory.MakeConfig().club;
            Assert.AreEqual(club.defaultName, club.NormalizeName("   "));
            Assert.AreEqual(club.defaultName, club.NormalizeName(null));
            Assert.AreEqual("Tigers", club.NormalizeName("  Tigers "));
            Assert.AreEqual(club.nameMaxLength, club.NormalizeName(new string('x', 40)).Length);
        }

        [Test]
        public void DebugArgs_ParsesNewDemos()
        {
            Assert.AreEqual(DemoState.Lineup, DebugArgs.Parse(new[] { "x", "--demo", "lineup" }).Demo);
            Assert.AreEqual(DemoState.Report, DebugArgs.Parse(new[] { "--demo", "report" }).Demo);
            var d = DebugArgs.Parse(new[] { "--demo", "standings", "--segments", "8" });
            Assert.AreEqual(DemoState.Standings, d.Demo);
            Assert.AreEqual(8, d.Segments);
            Assert.AreEqual(DemoState.Summary, DebugArgs.Parse(new[] { "--demo", "summary" }).Demo);
            Assert.AreEqual(DemoState.Main, DebugArgs.Parse(new[] { "--demo", "nonsense" }).Demo);
            Assert.AreEqual(DemoState.Name, DebugArgs.Parse(new[] { "--demo", "name" }).Demo);
            Assert.IsTrue(DebugArgs.Parse(new[] { "--demo", "main" }).DemoGiven);
            Assert.IsFalse(DebugArgs.Parse(new[] { "app" }).DemoGiven);
            Assert.IsTrue(DebugArgs.IsDebugRun(new[] { "app", "--screenshot", "/x.png" }));
            Assert.IsTrue(DebugArgs.IsDebugRun(new[] { "app", "--demo", "main" }));
            Assert.IsFalse(DebugArgs.IsDebugRun(new[] { "app", "-screen-width", "1280" }));
        }

        [Test]
        public void PlanError_EveryCode_HasSpecificChineseText_NoEnumNameOnScreen()
        {
            string generic = UiStrings.ErrGeneric;
            foreach (PlanErrorCode c in Enum.GetValues(typeof(PlanErrorCode)))
            {
                if (c == PlanErrorCode.None) continue;
                string t = UiStrings.PlanError(new PlanError { Code = c, Used = 4, Limit = 5, PlayerId = 1 }, id => "Ace");
                Assert.AreNotEqual(generic, t, c + " falls back to the generic text");
                StringAssert.DoesNotContain(c.ToString(), t, c + ": enum name shown to the player");
            }
            StringAssert.Contains("4/5", UiStrings.PlanError(new PlanError { Code = PlanErrorCode.IncompleteLineup, Used = 4, Limit = 5 }, id => ""));
        }

        [Test]
        public void CounterHint_FactoryConfig_ListsThreeCounters()
        {
            var mc = Matches.MatchTestFactory.MakeConfig();
            string h = MainScreen.CounterHint(mc);
            Assert.AreEqual(3, h.Split(new[] { UiStrings.CounterJoin }, StringSplitOptions.None).Length);
            StringAssert.Contains(string.Format(UiStrings.CounterFormat, UiStrings.TacticInvade, UiStrings.TacticDevelop), h);
        }

        [Test]
        public void RankChange_UpDownSame()
        {
            StringAssert.Contains(string.Format(UiStrings.RankUpFormat, 2), StandingsView.RankChange(5, 3));
            StringAssert.Contains(string.Format(UiStrings.RankDownFormat, 1), StandingsView.RankChange(3, 4));
            StringAssert.Contains(UiStrings.RankSame, StandingsView.RankChange(2, 2));
        }
    }
}
