using System.Collections.Generic;
using EsportsManager.Matches;
using EsportsManager.Segments;
using EsportsManager.Session;
using EsportsManager.UI;
using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using EventType = EsportsManager.Matches.EventType;

namespace EsportsManager.Tests.UI
{
    /// <summary>Story 004 stage C2: pure helpers of the calendar / event screens and the new debug demos.</summary>
    public class Story004EventUiTests
    {
        private static CalendarEventDef Def(string key, EventType type = EventType.Split) => new CalendarEventDef { nameKey = key, type = type };

        [Test]
        public void Countdown_NoneRunningNextAndLater()
        {
            Assert.AreEqual(UiStrings.NoEventLeft, EventTexts.Countdown(null));
            Assert.AreEqual(string.Format(UiStrings.EventRunningFormat, UiStrings.EventName("spring")),
                EventTexts.Countdown(new UpcomingEvent(Def("spring"), 0)));
            Assert.AreEqual(string.Format(UiStrings.NextEventAfterThisFormat, UiStrings.EventName("summer")),
                EventTexts.Countdown(new UpcomingEvent(Def("summer"), 1)));
            string later = EventTexts.Countdown(new UpcomingEvent(Def("finals", EventType.Finals), 4));
            Assert.AreEqual(string.Format(UiStrings.NextEventFormat, UiStrings.EventName("finals"), 4), later);
            StringAssert.Contains(UiStrings.EventName("finals"), later);
        }

        [Test]
        public void YearSegment_Formats()
        {
            Assert.AreEqual(string.Format(UiStrings.YearSegmentFormat, 2, 5, 12), EventTexts.YearSegment(2, 5, 12));
        }

        [Test]
        public void FormatSummary_UsesConfigNumbersPerStage()
        {
            var f = Matches.MatchTestFactory.MakeConfig().format;
            string reg = EventTexts.FormatSummary(EventType.Split, EventPhase.Prepare, 7, f);
            StringAssert.Contains("7", reg);
            StringAssert.Contains("BO" + f.regularBestOf, reg);
            StringAssert.Contains("BO" + f.splitFinalBestOf, EventTexts.FormatSummary(EventType.Split, EventPhase.PlayoffPrep, 7, f));
            string fin = EventTexts.FormatSummary(EventType.Finals, EventPhase.Prepare, 7, f);
            StringAssert.Contains("BO" + f.finalsFinalBestOf, fin);
            Assert.AreNotEqual(reg, fin);
            Assert.IsFalse(reg.Contains("{") || fin.Contains("{"));
        }

        [Test]
        public void PrepNote_ThreeBranches()
        {
            Assert.AreEqual(UiStrings.PrepNoteRegular, EventTexts.PrepNote(EventType.Split, EventPhase.Prepare));
            Assert.AreEqual(UiStrings.PrepNotePlayoff, EventTexts.PrepNote(EventType.Split, EventPhase.PlayoffPrep));
            Assert.AreEqual(UiStrings.PrepNoteFinals, EventTexts.PrepNote(EventType.Finals, EventPhase.Prepare));
        }

        [Test]
        public void PlacingText_ChampionPlaceAndAbsent()
        {
            Assert.AreEqual(UiStrings.NotInFinals, EventTexts.PlacingText(0, "x"));
            Assert.AreEqual(string.Format(UiStrings.PlacingChampionFormat, "¥1"), EventTexts.PlacingText(1, "¥1"));
            Assert.AreEqual(string.Format(UiStrings.PlacingFormat, 3, "¥1"), EventTexts.PlacingText(3, "¥1"));
        }

        [Test]
        public void StageName_EveryStage()
        {
            Assert.AreEqual(UiStrings.StageFinal, EventTexts.StageName(EventStage.Final));
            Assert.AreEqual(UiStrings.StageSemifinal, EventTexts.StageName(EventStage.Semifinal));
            Assert.AreEqual(UiStrings.StageRegular, EventTexts.StageName(EventStage.RegularSeason));
        }

        private static EventResult EmptySplit(int[] seeds)
        {
            var names = new string[8];
            for (int i = 0; i < names.Length; i++) names[i] = "Club" + i;
            return new EventResult { Def = Def("spring"), ClubNames = names, Regular = new Standings(8), RegularDone = true, Seeds = seeds };
        }

        [Test]
        public void RegularOutcome_SeededShowsFirstOpponent_ElseMiss()
        {
            // Empty table ranks by club index, so the player club (0) is first and seed 1 meets seed 4.
            var made = EventTexts.RegularOutcome(EmptySplit(new[] { 0, 1, 2, 3 }));
            Assert.AreEqual(string.Format(UiStrings.MadePlayoffsFormat, 1, "Club3"), made);
            var missed = EventTexts.RegularOutcome(EmptySplit(new[] { 1, 2, 3, 4 }));
            Assert.AreEqual(string.Format(UiStrings.MissedPlayoffsFormat, 1, 4), missed);
        }

        [Test]
        public void Totals_SumsStagesAndTakesMoodFromEventEnd()
        {
            var rec = new EventRecord
            {
                RegularSeasonEffects = new List<EventPlayerEffect>
                {
                    new EventPlayerEffect { PlayerId = 1, SeriesPlayed = 7, SeriesWon = 4, SeriesLost = 3, StressDelta = 10f },
                    new EventPlayerEffect { PlayerId = 2, SeriesPlayed = 3, SeriesWon = 1, SeriesLost = 2, StressDelta = 5f },
                },
                BracketEffects = new List<EventPlayerEffect> { new EventPlayerEffect { PlayerId = 1, SeriesPlayed = 2, SeriesWon = 1, SeriesLost = 1, StressDelta = 3f } },
                MoodEffects = new List<EventPlayerEffect> { new EventPlayerEffect { PlayerId = 1, MoodDelta = 4f }, new EventPlayerEffect { PlayerId = 2, MoodDelta = -2f } },
            };
            var t = EventTexts.Totals(rec);
            Assert.AreEqual(2, t.Count);
            Assert.AreEqual(1, t[0].PlayerId);
            Assert.AreEqual(9, t[0].Played); Assert.AreEqual(5, t[0].Won); Assert.AreEqual(4, t[0].Lost);
            Assert.AreEqual(13f, t[0].StressDelta, 1e-4f);
            Assert.AreEqual(4f, t[0].MoodDelta, 1e-4f);
            Assert.AreEqual(-2f, t[1].MoodDelta, 1e-4f);
            Assert.AreEqual(0, EventTexts.Totals(null).Count);
        }

        [Test]
        public void HighStressWarning_NullWhenNobody_JoinsNames()
        {
            Assert.IsNull(EventTexts.HighStressWarning(new List<string>()));
            Assert.IsNull(EventTexts.HighStressWarning(null));
            string w = EventTexts.HighStressWarning(new[] { "Ace", "Bo" });
            StringAssert.Contains("Ace" + UiStrings.NameJoin + "Bo", w);
        }

        [Test]
        public void QualificationNote_ListsPointsAndTeams()
        {
            var f = Matches.MatchTestFactory.MakeConfig().format;
            string n = EventTexts.QualificationNote(f);
            StringAssert.Contains(string.Join(UiStrings.PointsJoin, System.Array.ConvertAll(f.finalsPoints, p => p.ToString(System.Globalization.CultureInfo.InvariantCulture))), n);
            StringAssert.Contains(f.finalsTeams.ToString(System.Globalization.CultureInfo.InvariantCulture), n);
        }

        [Test]
        public void SplitNames_OnlySplitsInCalendarOrder()
        {
            var cfg = Matches.MatchTestFactory.MakeConfig();
            int splits = 0;
            foreach (var e in cfg.calendar.events) if (e.type == EventType.Split) splits++;
            var names = EventTexts.SplitNames(cfg);
            Assert.AreEqual(splits, names.Count);
            Assert.AreEqual(0, EventTexts.SplitNames(null).Count);
        }

        private static string Data(string f) => File.ReadAllText("Assets/Data/" + f);
        private static GameSession MakeSession() => GameSession.FromJson(Data("player_config.json"), Data("team_config.json"),
            Data("segment_config.json"), Data("match_config.json"), GameSession.DefaultSeed);

        [Test]
        public void FlowGuard_RapidSecondClick_DoesNotAdvancePastPhaseOrScreen()
        {
            // Arrange: open segment 3 (the spring split follows it) with a fake clock.
            var s = MakeSession();
            for (int i = 0; i < 2; i++) { s.ApplyScriptedPlan(); s.ResolveAndAdvance(); }
            double now = 100.0;
            var ui = new GameUi(new VisualElement(), s, () => now);
            ui.LaterOffers();
            s.ApplyScriptedPlan();

            // Act + assert: resolve, then an immediate "next step" is ignored.
            ui.Resolve();
            Assert.IsTrue(ui.Stack.IsTop(ScreenId.Summary));
            ui.NextSegment();
            Assert.IsTrue(ui.Stack.IsTop(ScreenId.Summary), "second click within the guard must be ignored");

            now += 1.0; ui.NextSegment();
            Assert.IsTrue(ui.Stack.IsTop(ScreenId.EventPrep));
            ui.StartEventStage();
            Assert.AreEqual(EventPhase.Prepare, s.EventPhase, "the regular season must not start from a double click");

            now += 1.0; ui.StartEventStage();
            Assert.AreEqual(EventPhase.PlayoffPrep, s.EventPhase);
            Assert.IsTrue(ui.Stack.IsTop(ScreenId.RegularResult));
            ui.GoToPlayoffs();
            Assert.IsTrue(ui.Stack.IsTop(ScreenId.RegularResult), "double click must not skip the regular-season result");
            Assert.AreEqual(EventPhase.PlayoffPrep, s.EventPhase);

            now += 0.36; ui.GoToPlayoffs();
            Assert.IsFalse(ui.Stack.IsTop(ScreenId.RegularResult), "after the guard window the click works");
        }

        [Test]
        public void FlowGuard_ZeroSeconds_AllowsBackToBackSteps()
        {
            var s = MakeSession();
            for (int i = 0; i < 2; i++) { s.ApplyScriptedPlan(); s.ResolveAndAdvance(); }
            var ui = new GameUi(new VisualElement(), s, () => 5.0) { FlowGuardSeconds = 0 };
            ui.LaterOffers();
            s.ApplyScriptedPlan();
            ui.Resolve(); ui.NextSegment(); ui.StartEventStage();
            Assert.AreEqual(EventPhase.PlayoffPrep, s.EventPhase);
        }

        [Test]
        public void PlayerCard_HasRatingAndHighStressFlagFromLogic()
        {
            var s = MakeSession();
            var p = s.Team.Roster[0];
            p.Stress = 100f;
            Assert.IsTrue(s.ToCard(p).HighStress);
            p.Stress = 0f;
            Assert.IsFalse(s.ToCard(p).HighStress);
            Assert.AreEqual(p.GetLaneScore(p.MainLane), s.ToCard(p).Rating, 1e-4f);
        }

        [Test]
        public void PlayerFirstOpponent_SeedPairingOrMinusOne()
        {
            Assert.AreEqual(3, EmptySplit(new[] { 0, 1, 2, 3 }).PlayerFirstOpponent);
            Assert.AreEqual(2, EmptySplit(new[] { 4, 2, 0, 1 }).PlayerFirstOpponent, "seed 3 meets seed 2");
            Assert.AreEqual(-1, EmptySplit(new[] { 1, 2, 3, 4 }).PlayerFirstOpponent);
        }

        [Test]
        public void DebugArgs_EventDemos_HyphensAndDefaultSegments()
        {
            var prep = DebugArgs.Parse(new[] { "--demo", "event-prep" });
            Assert.AreEqual(DemoState.EventPrep, prep.Demo);
            Assert.IsTrue(prep.IsEventDemo);
            Assert.AreEqual(3, prep.EffectiveSegments);
            var fq = DebugArgs.Parse(new[] { "--demo", "finals-qual" });
            Assert.AreEqual(DemoState.FinalsQual, fq.Demo);
            Assert.AreEqual(12, fq.EffectiveSegments);
            Assert.AreEqual(DemoState.Regular, DebugArgs.Parse(new[] { "--demo", "regular" }).Demo);
            Assert.AreEqual(DemoState.Bracket, DebugArgs.Parse(new[] { "--demo", "BRACKET" }).Demo);
            Assert.AreEqual(6, DebugArgs.Parse(new[] { "--demo", "bracket", "--segments", "6" }).EffectiveSegments);
            var main = DebugArgs.Parse(new[] { "--demo", "main", "--clicks", "4" });
            Assert.IsFalse(main.IsEventDemo);
            Assert.AreEqual(0, main.EffectiveSegments);
            Assert.AreEqual(4, main.Clicks);
        }
    }
}
