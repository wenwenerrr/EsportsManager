using System;
using System.IO;
using System.Text.RegularExpressions;
using EsportsManager.Events;
using EsportsManager.Session;
using EsportsManager.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace EsportsManager.Tests.UI
{
    /// <summary>Story 006 stage B: effect text mapping, choice / leave UI helpers, demo parsing and demo seeds.</summary>
    public class Story006EventUiTests
    {
        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);

        private static GameSession Make(int seed)
        {
            var s = GameSession.FromJson(Data("player_config.json"), Data("team_config.json"), Data("segment_config.json"),
                Data("match_config.json"), Data("event_config.json"), seed);
            s.EventTexts = EventTextTable.FromJson(Data("event_text_zh.json"));
            return s;
        }

        private static EventEffectResult Fx(EffectType t, float value, float applied = 0f, EffectTarget target = EffectTarget.Subject)
            => new EventEffectResult { Type = t, Value = value, Applied = applied, Target = target };

        [Test]
        public void EveryEffectType_HasASentence_ResolvedAndPending()
        {
            foreach (EffectType t in Enum.GetValues(typeof(EffectType)))
            {
                foreach (bool resolved in new[] { false, true })
                {
                    var p = EventEffectText.Describe(Fx(t, 1.5f, 1.5f), resolved);
                    Assert.IsFalse(string.IsNullOrEmpty(p.Text), t + " has no sentence");
                    Assert.AreNotEqual(UiStrings.EffNone, p.Text, t + " falls back to the generic sentence");
                    StringAssert.DoesNotContain("{", p.Text, t + " left a placeholder");
                }
            }
        }

        [Test]
        public void Signs_AndSentiment_AreRight()
        {
            var mood = EventEffectText.Describe(Fx(EffectType.Mood, 12f), false);
            Assert.AreEqual("心情 +12", mood.Text); Assert.AreEqual(1, mood.Sentiment);
            var stressDown = EventEffectText.Describe(Fx(EffectType.Stress, -10f), false);
            Assert.AreEqual("压力 −10", stressDown.Text); Assert.AreEqual(1, stressDown.Sentiment);
            Assert.AreEqual(-1, EventEffectText.Describe(Fx(EffectType.Stress, 5f), false).Sentiment);
            var cost = EventEffectText.Describe(Fx(EffectType.Funds, -42000f, -42000f), true);
            Assert.AreEqual("资金 −4.2 万", cost.Text); Assert.AreEqual(-1, cost.Sentiment);
            Assert.AreEqual("资金 −2.95 万", EventEffectText.Describe(Fx(EffectType.Funds, -29518f, -29518f), true).Text);
            Assert.AreEqual("资金 +8,000 元", EventEffectText.Describe(Fx(EffectType.Funds, 8000f), false).Text);
            var train = EventEffectText.Describe(Fx(EffectType.TrainMultiplier, 2f), true);
            Assert.AreEqual("训练收益 ×2", train.Text); Assert.AreEqual(1, train.Sentiment);
            Assert.AreEqual(-1, EventEffectText.Describe(Fx(EffectType.StreamMultiplier, 0.5f), true).Sentiment);
            Assert.AreEqual("赞助成功率 +15%", EventEffectText.Describe(Fx(EffectType.SponsorChanceAdd, 0.15f), false).Text);
            Assert.AreEqual(-1, EventEffectText.Describe(Fx(EffectType.VoidAction, 0f), true).Sentiment);
        }

        [Test]
        public void Potential_NeverShowsANumber()
        {
            foreach (float v in new[] { 12.34f, 5f, -3f, -7.8f })
            {
                foreach (bool resolved in new[] { false, true })
                {
                    var p = EventEffectText.Describe(Fx(EffectType.Potential, v, v * 0.7f), resolved);
                    Assert.IsFalse(Regex.IsMatch(p.Text, "[0-9]"), "potential shows a number: " + p.Text);
                    Assert.AreEqual(v > 0 ? UiStrings.EffPotentialUp : UiStrings.EffPotentialDown, p.Text);
                }
            }
        }

        [Test]
        public void Resolved_GroupEffects_ShowPerPlayerValue()
        {
            var e = Fx(EffectType.Mood, -6f, -30f, EffectTarget.Starters);
            for (int i = 0; i < 5; i++) e.PlayerIds.Add(i);
            Assert.AreEqual("首发心情 −6", EventEffectText.Describe(e, true).Text);
            var fam = Fx(EffectType.Familiarity, 5f, 5f);
            fam.PlayerIds.Add(1); fam.PlayerIds.Add(2);
            Assert.AreEqual("两人默契 +5", EventEffectText.Describe(fam, true, true).Text);
        }

        [Test]
        public void Fill_ReplacesPlaceholders_AndEmptyListSaysNone()
        {
            Assert.AreEqual("甲 和 乙", EventEffectText.Fill("{player} 和 {partner}", "甲", "乙"));
            Assert.AreEqual(" 和 ", EventEffectText.Fill("{player} 和 {partner}", null, null));
            Assert.AreEqual(UiStrings.EffNone, EventEffectText.Join(new EventEffectResult[0], false));
        }

        [Test]
        public void DebugArgs_ParsesStory006Demos()
        {
            Assert.AreEqual(DemoState.Choice, DebugArgs.Parse(new[] { "--demo", "choice" }).Demo);
            var ev = DebugArgs.Parse(new[] { "--demo", "events", "--seed", "7" });
            Assert.AreEqual(DemoState.Events, ev.Demo);
            Assert.AreEqual(7, ev.Seed);
            Assert.IsFalse(ev.IsEventDemo);
            Assert.AreEqual(0, ev.EffectiveSegments);
        }

        [Test]
        public void ChoiceDemoSeed_IsTheFirstSeedWithALeaveChoice()
        {
            Assert.AreEqual(DemoSeeds.ChoiceDemoSeed, DemoSeeds.Find(Make, GameSession.DefaultSeed, DemoSeeds.HasLeaveChoice));
        }

        [Test]
        public void EventsDemoSeed_IsTheFirstSeedWithRichEventCards()
        {
            Assert.AreEqual(DemoSeeds.EventsDemoSeed, DemoSeeds.Find(Make, GameSession.DefaultSeed, DemoSeeds.HasEventCards));
        }

        [Test]
        public void ChoicePopup_OpensAtStart_LeaveMarksCard_AndAnswerClosesIt()
        {
            var s = Make(DemoSeeds.ChoiceDemoSeed);
            var ui = new GameUi(new VisualElement(), s, () => 0.0);
            Assert.IsTrue(ui.ChoicePopupOpen);
            ui.Refresh();
            var c = ui.ShownChoice();
            Assert.IsNotNull(c);
            int leave = c.Options.FindIndex(o => o.PutsOnLeave);
            Assert.GreaterOrEqual(leave, 0);
            ui.AnswerChoice(c, leave);
            Assert.IsNull(ui.ChoiceError);
            Assert.IsTrue(ui.IsOnLeave(c.PlayerId.Value));
            Assert.IsFalse(ui.ChoicePopupOpen, "the pop-up closes after the last answer");
            // His actions are refused with the leave reason.
            ui.Rest(c.PlayerId.Value);
            Assert.IsTrue(ui.MessageIsError);
            StringAssert.Contains("请假", ui.Message);
            // Every rendered card / badge builds without throwing.
            ui.OpenChoices();
            Assert.IsTrue(ui.ChoicePopupOpen);
            ui.LaterChoices();
        }

        [Test]
        public void ChoiceAnswer_RefusedWhenPlayerAlreadyHasActions_ShowsReason()
        {
            var s = Make(DemoSeeds.ChoiceDemoSeed);
            var ui = new GameUi(new VisualElement(), s, () => 0.0);
            var c = ui.ShownChoice();
            int leave = c.Options.FindIndex(o => o.PutsOnLeave);
            Assert.IsTrue(s.TryAddRest(c.PlayerId.Value, out _));
            ui.AnswerChoice(c, leave);
            Assert.IsNotNull(ui.ChoiceError);
            StringAssert.Contains(s.PlayerName(c.PlayerId.Value), ui.ChoiceError);
            Assert.IsTrue(ui.ChoicePopupOpen);
        }

        [Test]
        public void EventsSummary_RendersCards()
        {
            var s = Make(DemoSeeds.EventsDemoSeed);
            var root = new VisualElement();
            var ui = new GameUi(root, s, () => 0.0) { FlowGuardSeconds = 0 };
            ui.ClosePopups();
            s.ApplyScriptedPlan();
            ui.Resolve();
            Assert.IsNotNull(root.Q("events-panel"));
            Assert.IsNotNull(root.Q("event-card-0"));
            Assert.IsNotNull(root.Q("event-card-2"));
        }
    }
}
