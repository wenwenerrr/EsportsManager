using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsportsManager.Events;
using EsportsManager.Growth;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Session;
using EsportsManager.Tests.Segments;
using NUnit.Framework;
using static EsportsManager.Tests.Segments.SegmentTestFactory;

namespace EsportsManager.Tests.Events
{
    /// <summary>Story 006 stage A: event config, engine, effects, choice events, segment integration.</summary>
    public class EventTests
    {
        private const float Tol = 1e-3f;
        private static string Json => File.ReadAllText("Assets/Data/event_config.json");

        // ------------------------------------------------------------ fixtures

        private static EventConfig Cfg(params EventDef[] evs) => new EventConfig
        {
            actionEventChance = 1f, maxActionEvents = 3, goodBase = 0.6f, goodMoodWeight = 0.003f, goodStressWeight = 0.004f,
            goodNeutral = 50f, goodMin = 1f, goodMax = 1f, maxTraitModifier = 0.05f, maxStateEvents = 1, stateAttemptChance = 1f,
            choiceEventChance = 1f, maxChoiceEvents = 1, events = evs,
        };

        private static EventEffectDef Fx(EffectType t, float v, EffectTarget target = EffectTarget.Subject) =>
            new EventEffectDef { type = t, target = target, min = v, max = v };

        private static EventDef Act(string id, EventAction a, params EventEffectDef[] fx) => new EventDef
        {
            id = id, kind = EventKind.Action, tone = EventTone.Good, action = a,
            subject = a == EventAction.Sponsor ? EventSubject.Team : EventSubject.Player,
            weight = 1f, chance = 1f, titleKey = "t", textKey = "x", effects = fx,
        };

        private static EventDef State(string id, params EventEffectDef[] fx) => new EventDef
        {
            id = id, kind = EventKind.State, tone = EventTone.Good, subject = EventSubject.Player,
            weight = 1f, chance = 1f, titleKey = "t", textKey = "x", effects = fx,
        };

        private static EventDef Leave() => new EventDef
        {
            id = "leave", kind = EventKind.Choice, tone = EventTone.Neutral, subject = EventSubject.Player, weight = 1f, chance = 1f,
            titleKey = "t", textKey = "x", defaultOption = 1,
            options = new[]
            {
                new ChoiceOptionDef { labelKey = "a", effects = new[] { Fx(EffectType.OnLeave, 0f), Fx(EffectType.Mood, 10f) } },
                new ChoiceOptionDef { labelKey = "b", effects = new[] { Fx(EffectType.Mood, -5f) } },
            },
        };

        private static Fixture With(EventConfig cfg, out ScriptedRandomSource eventRng, IRandomSource runnerRng = null)
        {
            var f = new Fixture(runnerRng);
            eventRng = new ScriptedRandomSource(0.5f);
            f.Runner = new SegmentRunner(f.Cfg, f.Players, f.Team, f.Rng, new EventEngine(cfg, f.Players, eventRng), f.Growth);
            return f;
        }

        private static SegmentSummary Run(Fixture f, SegmentPlan plan)
        {
            f.Runner.BeginSegment();
            return f.Runner.Resolve(plan, Ctx());
        }

        // ------------------------------------------------------------ config

        [Test]
        public void ShippedConfig_LoadsWithContentRequirements()
        {
            var players = PlayerConfigLoader.FromJson(File.ReadAllText("Assets/Data/player_config.json"));
            var cfg = EventConfigLoader.FromJson(Json, players);
            Assert.GreaterOrEqual(cfg.events.Length, 16);
            foreach (var a in new[] { EventAction.Train, EventAction.Stream, EventAction.Rest, EventAction.Business, EventAction.Sponsor })
            {
                Assert.IsTrue(cfg.events.Any(e => e.action == a && e.tone == EventTone.Good), a + " good");
                Assert.IsTrue(cfg.events.Any(e => e.action == a && e.tone == EventTone.Bad), a + " bad");
            }
            var states = cfg.events.Where(e => e.kind == EventKind.State).ToList();
            Assert.GreaterOrEqual(states.Count, 4);
            Assert.IsTrue(states.Any(e => e.effects.Any(x => x.type == EffectType.Potential && x.min >= 5f && x.max <= 15f)), "breakthrough");
            Assert.IsTrue(states.Any(e => e.effects.Any(x => x.type == EffectType.Potential && x.max <= -3f && x.min >= -8f)), "slump");
            Assert.GreaterOrEqual(cfg.events.Count(e => e.kind == EventKind.Choice), 3);
            Assert.GreaterOrEqual(cfg.events.Count(e => e.effects.Any(x => x.type == EffectType.Funds && x.max < 0f)), 6, "money-loss events");
            Assert.AreEqual(0.25f, cfg.actionEventChance, Tol);
            Assert.AreEqual(3, cfg.maxActionEvents);
            Assert.AreEqual(1, cfg.maxStateEvents);
            Assert.AreEqual(0.5f, cfg.stateAttemptChance, Tol);
            var texts = EventTextTable.FromJson(File.ReadAllText("Assets/Data/event_text_zh.json"));
            CollectionAssert.IsEmpty(texts.MissingKeys(cfg));
        }

        [TestCase("\"p_event\": 0.25", "\"p_event\": \"0.25\"")]
        [TestCase("\"max_trait_modifier\": 0.05,", "\"max_trait_modifier\": 0.05, \"extra\": 1,")]
        [TestCase("\"max_trait_modifier\": 0.05,", "")]
        public void Loader_StrictShape_Throws(string from, string to)
        {
            Assert.Throws<InvalidOperationException>(() => EventConfigLoader.FromJson(Replace1(Json, from, to)));
        }

        [Test]
        public void Loader_UnknownEnum_NamesField()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => EventConfigLoader.FromJson(Replace1(Json, "\"kind\": \"action\"", "\"kind\": \"acton\"")));
            StringAssert.Contains(".kind", ex.Message);
        }

        [Test]
        public void Loader_TraitModifierAboveMax_NamesField()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => EventConfigLoader.FromJson(Replace1(Json, "\"value\": 0.05", "\"value\": 0.08")));
            StringAssert.Contains("trait_modifiers[0].value", ex.Message);
        }

        [Test]
        public void Validate_ModifierOnWrongAction_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Cfg(Act("x", EventAction.Train, Fx(EffectType.StreamMultiplier, 2f))).Validate());
            StringAssert.Contains("effects[0]", ex.Message);
        }

        private static string Replace1(string s, string from, string to)
        {
            int i = s.IndexOf(from, StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0, "fixture text not found: " + from);
            return s.Substring(0, i) + to + s.Substring(i + from.Length);
        }

        // ------------------------------------------------------------ formula

        [Test]
        public void GoodChance_NeutralIsBase_ExtremesClamp_TraitAdds()
        {
            var c = EventConfigLoader.FromJson(Json);
            Assert.AreEqual(0.55f, c.goodBase, Tol);
            Assert.AreEqual(c.goodBase, EventEngine.GoodChance(c, 50f, 50f, 0f), Tol);
            Assert.AreEqual(c.goodBase + 0.05f, EventEngine.GoodChance(c, 50f, 50f, 0.05f), Tol);
            Assert.AreEqual(c.goodBase + 0.003f * 20f - 0.004f * 10f, EventEngine.GoodChance(c, 70f, 60f, 0f), Tol);
            Assert.AreEqual(c.goodMax, EventEngine.GoodChance(c, 100f, 0f, 0.05f), Tol);
            Assert.AreEqual(c.goodMin, EventEngine.GoodChance(c, 0f, 100f, -0.05f), Tol);
        }

        // ------------------------------------------------------------ cap, determinism

        [Test]
        public void ActionEvents_CappedAtMax_LaterPointsNotRolled()
        {
            var f = With(Cfg(Act("a", EventAction.Train, Fx(EffectType.Mood, 1f))), out var rng);
            var s = Run(f, new SegmentPlan().Train(1, TrainingTarget.Early).Train(1, TrainingTarget.Early).Train(2, TrainingTarget.Early).Train(2, TrainingTarget.Early).Train(3, TrainingTarget.Early).Train(3, TrainingTarget.Early));
            var action = s.Events.Where(e => e.Kind == EventKind.Action).ToList();
            Assert.AreEqual(3, action.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, action.Select(e => e.PointIndex));
            // choice slot trigger (1) + 3 x (trigger, tone, pick) + state slot attempt (1, then no candidate) = 11 draws
            Assert.AreEqual(11, rng.Draws);
        }

        [Test]
        public void Session_SameSeed_SameEvents()
        {
            Assert.AreEqual(EventTrace(77), EventTrace(77));
        }

        private static string EventTrace(int seed)
        {
            var s = ShippedSession(seed);
            var parts = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                s.ApplyScriptedPlan();
                var r = s.ResolveAndAdvance();
                foreach (var e in r.Summary.Events) parts.Add(e.EventId + ":" + e.PlayerId + ":" + string.Join(",", e.Effects.Select(x => x.Applied.ToString("F3"))));
            }
            return string.Join("|", parts);
        }

        // ------------------------------------------------------------ effects

        [Test]
        public void TrainMultiplier_DoublesGain_TrainAdd_AddsToTarget()
        {
            var lane = "top";
            var baseF = new Fixture(); Run(baseF, new SegmentPlan().Train(1, TrainingTarget.Early));
            float baseGain = baseF.Starters[0].GetDimension(lane, 0) - 50f;
            var f = With(Cfg(Act("a", EventAction.Train, Fx(EffectType.TrainMultiplier, 2f))), out _);
            var s = Run(f, new SegmentPlan().Train(1, TrainingTarget.Early));
            Assert.AreEqual(2f * baseGain, f.Starters[0].GetDimension(lane, 0) - 50f, Tol);
            Assert.AreEqual(baseGain, s.Events[0].Effects[0].Applied, Tol);
            var g = With(Cfg(Act("a", EventAction.Train, Fx(EffectType.TrainAdd, 2f))), out _);
            Run(g, new SegmentPlan().Train(1, TrainingTarget.Early));
            Assert.AreEqual(baseGain + 2f, g.Starters[0].GetDimension(lane, 0) - 50f, Tol);
        }

        [Test]
        public void StreamMultiplier_DoublesThatStreamsIncome()
        {
            var baseS = Run(new Fixture(), new SegmentPlan().Stream(1));
            var f = With(Cfg(Act("a", EventAction.Stream, Fx(EffectType.StreamMultiplier, 2f))), out _);
            var s = Run(f, new SegmentPlan().Stream(1));
            Assert.AreEqual(2 * baseS.LivestreamIncome, s.LivestreamIncome);
            Assert.AreEqual(baseS.LivestreamIncome, s.Events[0].Effects[0].Applied, 0.5f);
        }

        [Test]
        public void RestMultiplierZero_RestRelievesNothing()
        {
            var noRest = new Fixture(); noRest.Starters[0].Stress = 50f; Run(noRest, new SegmentPlan());
            var f = With(Cfg(Act("a", EventAction.Rest, Fx(EffectType.RestMultiplier, 0f))), out _);
            f.Starters[0].Stress = 50f;
            Run(f, new SegmentPlan().Rest(1));
            Assert.AreEqual(noRest.Starters[0].Stress, f.Starters[0].Stress, Tol);
        }

        [Test]
        public void SponsorVoid_NoAttempt_SponsorAdd_ClampedToMax()
        {
            var f = With(Cfg(Act("a", EventAction.Sponsor, Fx(EffectType.VoidAction, 0f))), out _);
            var s = Run(f, new SegmentPlan().Sponsor());
            Assert.IsTrue(s.SponsorAttempts[0].Voided);
            Assert.AreEqual(0, s.NewContracts.Count);
            var g = With(Cfg(Act("a", EventAction.Sponsor, Fx(EffectType.SponsorChanceAdd, 1f))), out _);
            var t = Run(g, new SegmentPlan().Sponsor());
            Assert.AreEqual(g.Cfg.sponsorship.maxChance, t.SponsorAttempts[0].Chance, Tol);
        }

        [Test]
        public void BusinessMultiplier_ScalesClubShare()
        {
            // runner draws: first player's offer trigger 0 (offer), amount R at 0.5 (=1); others 0.5 (no offer)
            var f = With(Cfg(Act("a", EventAction.Business, Fx(EffectType.BusinessMultiplier, 1.5f))), out _, new ScriptedRandomSource(0.5f, 0f));
            f.Runner.BeginSegment();
            var offer = f.Runner.GetState().PendingOffers[0];
            var s = f.Runner.Resolve(new SegmentPlan().AcceptOffer(offer.Id), Ctx());
            Assert.AreEqual((int)Math.Round(offer.ClubAmount * 1.5, MidpointRounding.AwayFromZero), s.BusinessIncome);
        }

        [Test]
        public void MoodStress_ClampedToRange()
        {
            var f = With(Cfg(Act("a", EventAction.Train, Fx(EffectType.Mood, 500f), Fx(EffectType.Stress, -500f))), out _);
            f.Starters[0].Stress = 40f;
            var s = Run(f, new SegmentPlan().Train(1, TrainingTarget.Early));
            Assert.AreEqual(100f, f.Starters[0].Mood, Tol);
            Assert.AreEqual(0f, f.Starters[0].Stress, Tol);
            Assert.LessOrEqual(s.Events[0].Effects[0].Applied, 100f);
        }

        [Test]
        public void Funds_BookedAsEventIncomeAndExpenses()
        {
            var baseS = Run(new Fixture(), new SegmentPlan().Train(1, TrainingTarget.Early).Train(2, TrainingTarget.Early));
            var cfg = Cfg(Act("a", EventAction.Train, Fx(EffectType.Funds, -5000f, EffectTarget.Club)));
            var f = With(cfg, out _);
            var s = Run(f, new SegmentPlan().Train(1, TrainingTarget.Early));
            Assert.AreEqual(5000, s.EventExpenses);
            Assert.AreEqual(0, s.EventIncome);
            Assert.AreEqual(-5000L, s.Events[0].FundsDelta);
            var g = With(Cfg(Act("a", EventAction.Train, Fx(EffectType.Funds, 3000f, EffectTarget.Club))), out _);
            var t = Run(g, new SegmentPlan().Train(1, TrainingTarget.Early));
            Assert.AreEqual(3000, t.EventIncome);
            Assert.AreEqual(baseS.Report.Income + 3000, t.Report.Income);
            Assert.AreEqual(baseS.Report.Expenses + 5000, s.Report.Expenses);
        }

        [Test]
        public void Popularity_ClubPopularity_Familiarity_AppliedAfterSettle()
        {
            var b = new Fixture(); Run(b, new SegmentPlan().Train(1, TrainingTarget.Early));
            var f = With(Cfg(Act("a", EventAction.Train, Fx(EffectType.PlayerPopularity, 10f), Fx(EffectType.ClubPopularity, 5f, EffectTarget.Club),
                Fx(EffectType.Familiarity, 10f, EffectTarget.SubjectWithStarters))), out _);
            Run(f, new SegmentPlan().Train(1, TrainingTarget.Early));
            Assert.AreEqual(b.Starters[0].Popularity + 10f, f.Starters[0].Popularity, Tol);
            Assert.AreEqual(b.Team.ClubPopularity + 5f, f.Team.ClubPopularity, Tol);
            Assert.AreEqual(b.Team.GetFamiliarity(1, 2) + 10f, f.Team.GetFamiliarity(1, 2), Tol);
            Assert.AreEqual(b.Team.GetFamiliarity(2, 3), f.Team.GetFamiliarity(2, 3), Tol);
        }

        [Test]
        public void StateEvent_PotentialChange_AppliedAndClamped()
        {
            var f = With(Cfg(State("up", Fx(EffectType.Potential, 10f))), out _);
            float before = f.Team.Roster[0].Potential;
            var s = Run(f, new SegmentPlan());
            var e = s.Events.Single();
            Assert.AreEqual(EventKind.State, e.Kind);
            Assert.AreEqual(f.Team.Roster[0].Id, e.PlayerId);
            Assert.AreEqual(Math.Min(100f, before + 10f), f.Team.Roster[0].Potential, Tol);
            var g = With(Cfg(State("down", Fx(EffectType.Potential, -500f))), out _);
            Run(g, new SegmentPlan());
            Assert.AreEqual(0f, g.Team.Roster[0].Potential, Tol);
        }

        [Test]
        public void StateEvent_ConditionNotMet_NothingHappens()
        {
            var ev = State("hot", Fx(EffectType.Mood, -10f));
            ev.conditions = new[] { new EventConditionDef { type = ConditionType.StressAbove, value = 80f } };
            var f = With(Cfg(ev), out _);
            Assert.AreEqual(0, Run(f, new SegmentPlan()).Events.Count);
            var g = With(Cfg(ev), out _);
            g.Starters[2].Stress = 95f;
            var s = Run(g, new SegmentPlan());
            Assert.AreEqual(g.Starters[2].Id, s.Events.Single().PlayerId);
        }

        [Test]
        public void StateEvent_FailedCandidate_NextCandidateTried()
        {
            var a = State("a", Fx(EffectType.Mood, 1f)); a.chance = 0.4f;
            var b = State("b", Fx(EffectType.Mood, 2f)); b.chance = 0.6f;
            // event draws: choice slot trigger, state attempt, pick 0.1 (-> a), chance 0.5 (fails 0.4), pick (-> b), chance 0.5 (passes 0.6)
            var f = new Fixture();
            var rng = new ScriptedRandomSource(0.5f, 0.5f, 0.5f, 0.1f, 0.5f, 0.5f, 0.5f);
            f.Runner = new SegmentRunner(f.Cfg, f.Players, f.Team, f.Rng, new EventEngine(Cfg(a, b), f.Players, rng), f.Growth);
            var s = Run(f, new SegmentPlan());
            Assert.AreEqual("b", s.Events.Single().EventId);
        }

        [Test]
        public void StateEvent_AllCandidatesFail_SlotEmpty()
        {
            var a = State("a", Fx(EffectType.Mood, 1f)); a.chance = 0.4f;
            var b = State("b", Fx(EffectType.Mood, 2f)); b.chance = 0.4f;
            var f = new Fixture();
            var rng = new ScriptedRandomSource(0.5f);
            f.Runner = new SegmentRunner(f.Cfg, f.Players, f.Team, f.Rng, new EventEngine(Cfg(a, b), f.Players, rng), f.Growth);
            Assert.AreEqual(0, Run(f, new SegmentPlan()).Events.Count);
            Assert.AreEqual(1 + 1 + 4, rng.Draws); // choice trigger + state attempt + 2 x (pick, chance)
        }

        [Test]
        public void StateEvent_AttemptMissed_NoCandidateSelection()
        {
            var cfg = Cfg(State("a", Fx(EffectType.Mood, 1f)));
            cfg.stateAttemptChance = 0.4f;
            var f = new Fixture();
            var rng = new ScriptedRandomSource(0.5f); // attempt draw 0.5 >= 0.4: slot skipped
            f.Runner = new SegmentRunner(f.Cfg, f.Players, f.Team, f.Rng, new EventEngine(cfg, f.Players, rng), f.Growth);
            Assert.AreEqual(0, Run(f, new SegmentPlan()).Events.Count);
            Assert.AreEqual(1 + 1, rng.Draws); // choice trigger + state attempt only
        }

        [Test]
        public void ClubPopularityCondition_GatesStateEvent()
        {
            var ev = new EventDef
            {
                id = "meetup", kind = EventKind.State, tone = EventTone.Bad, subject = EventSubject.Team, weight = 1f, chance = 1f,
                titleKey = "t", textKey = "x", effects = new[] { Fx(EffectType.Funds, -100f, EffectTarget.Club) },
                conditions = new[] { new EventConditionDef { type = ConditionType.ClubPopularityAtLeast, value = 40f } },
            };
            var low = With(Cfg(ev), out _); low.Team.ClubPopularity = 39f;
            Assert.AreEqual(0, Run(low, new SegmentPlan()).Events.Count);
            var high = With(Cfg(ev), out _); high.Team.ClubPopularity = 40f;
            Assert.AreEqual("meetup", Run(high, new SegmentPlan()).Events.Single().EventId);
        }

        // ------------------------------------------------------------ choices

        [Test]
        public void Choice_RolledAtBegin_UnansweredTakesDefault()
        {
            var f = With(Cfg(Leave()), out _);
            f.Runner.BeginSegment();
            var ch = f.Runner.GetState().PendingChoices.Single();
            Assert.AreEqual(f.Team.Roster[0].Id, ch.PlayerId);
            Assert.IsTrue(ch.Options[0].PutsOnLeave);
            var s = f.Runner.Resolve(new SegmentPlan().Train(ch.PlayerId.Value, TrainingTarget.Early), Ctx());
            var e = s.Events.Single();
            Assert.AreEqual(1, e.OptionTaken);
            Assert.IsTrue(e.OptionWasDefault);
            Assert.AreEqual(0, f.Runner.GetState().PendingChoices.Count);
        }

        [Test]
        public void Choice_LeaveWithActions_FailsAndChangesNothing()
        {
            var f = With(Cfg(Leave()), out var rng);
            f.Runner.BeginSegment();
            var ch = f.Runner.GetState().PendingChoices.Single();
            var p = f.Team.Roster[0];
            float mood = p.Mood; long funds = f.Team.Funds; int draws = rng.Draws;
            var plan = new SegmentPlan().Train(p.Id, TrainingTarget.Early).AnswerChoice(ch.Id, 0);
            Assert.IsFalse(f.Runner.TryValidate(plan, Ctx(), out var err));
            Assert.AreEqual(PlanErrorCode.PlayerOnLeave, err.Code);
            Assert.AreEqual(ch.Id, err.ChoiceId);
            Assert.Throws<InvalidOperationException>(() => f.Runner.Resolve(plan, Ctx()));
            Assert.AreEqual(mood, p.Mood); Assert.AreEqual(funds, f.Team.Funds); Assert.AreEqual(draws, rng.Draws);
            Assert.IsTrue(f.Runner.GetState().IsOpen);
            Assert.AreEqual(1, f.Runner.GetState().PendingChoices.Count);
            // answered "leave" without actions for him resolves and applies option 0
            var s = f.Runner.Resolve(new SegmentPlan().Train(f.Team.Roster[1].Id, TrainingTarget.Early).AnswerChoice(ch.Id, 0), Ctx());
            var e = s.Events.First(x => x.Kind == EventKind.Choice);
            Assert.AreEqual(0, e.OptionTaken);
            Assert.IsFalse(e.OptionWasDefault);
        }

        [Test]
        public void Choice_BadAnswers_Rejected()
        {
            var f = With(Cfg(Leave()), out _);
            f.Runner.BeginSegment();
            var ch = f.Runner.GetState().PendingChoices.Single();
            f.Runner.TryValidate(new SegmentPlan().AnswerChoice(99, 0), Ctx(), out var e1);
            Assert.AreEqual(PlanErrorCode.UnknownChoice, e1.Code);
            f.Runner.TryValidate(new SegmentPlan().AnswerChoice(ch.Id, 2), Ctx(), out var e2);
            Assert.AreEqual(PlanErrorCode.ChoiceOptionOutOfRange, e2.Code);
            Assert.AreEqual(2, e2.Limit);
        }

        [Test]
        public void Session_AnswerChoice_ThroughValidatedApi()
        {
            GameSession s = null;
            for (int seed = 1; seed < 200; seed++)
            {
                s = ShippedSession(seed);
                if (s.PendingChoices.Count > 0) break;
            }
            Assert.IsNotNull(s);
            Assert.Greater(s.PendingChoices.Count, 0, "no seed with a choice event");
            var ch = s.PendingChoices[0];
            Assert.IsFalse(s.TryAnswerChoice(ch.Id, 9, out var err));
            Assert.AreEqual(PlanErrorCode.ChoiceOptionOutOfRange, err.Code);
            Assert.AreEqual(ch.DefaultOption, s.EffectiveChoiceOption(ch.Id));
            int other = ch.DefaultOption == 0 ? 1 : 0;
            Assert.IsTrue(s.TryAnswerChoice(ch.Id, other, out _));
            Assert.AreEqual(other, s.EffectiveChoiceOption(ch.Id));
            var r = s.Resolve();
            var e = r.Summary.Events.First(x => x.Kind == EventKind.Choice);
            Assert.AreEqual(other, e.OptionTaken);
            Assert.IsFalse(e.OptionWasDefault);
        }

        // ------------------------------------------------------------ simulation (prints the scale, loose assertion)

        [Test]
        public void Simulation_TwoYears_PrintsEventMoneyAndCounts()
        {
            foreach (int seed in new[] { GameSession.DefaultSeed, 12345, 777 })
            {
                var s = ShippedSession(seed);
                int perYear = s.SegmentsPerYear, total = 0;
                for (int year = 1; year <= 2; year++)
                {
                    long inc = 0, exp = 0, salary = 0; int good = 0, bad = 0, state = 0, choice = 0;
                    long fundsStart = s.Team.Funds;
                    for (int i = 0; i < perYear; i++)
                    {
                        s.ApplyScriptedPlan();
                        var sum = s.ResolveAndAdvance().Summary;
                        inc += sum.EventIncome; exp += sum.EventExpenses; salary += sum.Report.SalaryShare;
                        foreach (var e in sum.Events)
                        {
                            total++;
                            if (e.Kind == EventKind.Choice) choice++;
                            else if (e.Kind == EventKind.State) state++;
                            if (e.Tone == EventTone.Good) good++; else if (e.Tone == EventTone.Bad) bad++;
                        }
                    }
                    string line = "[Story006 sim] seed " + seed + " year " + year + ": event income " + inc + ", event expenses " + exp
                        + ", net " + (inc - exp) + " (" + (100.0 * (inc - exp) / Math.Max(1L, salary)).ToString("F1") + "% of salaries " + salary + ") | good " + good + ", bad " + bad + " (state " + state + "), choices " + choice
                        + " | funds " + fundsStart + " -> " + s.Team.Funds;
                    TestContext.Progress.WriteLine(line);
                    UnityEngine.Debug.Log(line);
                }
                Assert.Greater(total, 0);
            }
        }

        private static GameSession ShippedSession(int seed) => GameSession.FromJson(
            File.ReadAllText("Assets/Data/player_config.json"), File.ReadAllText("Assets/Data/team_config.json"),
            File.ReadAllText("Assets/Data/segment_config.json"), File.ReadAllText("Assets/Data/match_config.json"), Json, File.ReadAllText("Assets/Data/league_config.json"), seed);
    }
}
