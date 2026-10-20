using System;
using System.Collections.Generic;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Events
{
    /// <summary>
    /// Parses <see cref="EventConfig"/> JSON (Assets/Data/event_config.json): snake_case keys, private wire DTOs, strict shape
    /// check first (<see cref="JsonShapeChecker"/>: every key required, no extra / duplicate keys, no type coercion), then the
    /// enum strings are mapped (unknown value = error naming the field) and <see cref="EventConfig.Validate"/> runs.
    /// </summary>
    public static class EventConfigLoader
    {
        /// <summary>Parses and validates; every bad-config case is an <see cref="InvalidOperationException"/>.</summary>
        /// <param name="players">Optional: also checks trait ids against the personality table.</param>
        /// <example><code>var cfg = EventConfigLoader.FromJson(textAsset.text, playerConfig);</code></example>
        public static EventConfig FromJson(string json, PlayerConfig players = null)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("EventConfig: JSON text is empty.");
            JsonShapeChecker.Check(json, typeof(ConfigDto), "EventConfig");
            ConfigDto d;
            try { d = JsonUtility.FromJson<ConfigDto>(json); }
            catch (Exception e) { throw new InvalidOperationException("EventConfig: JSON could not be parsed: " + e.Message, e); }
            if (d == null) throw new InvalidOperationException("EventConfig: JSON could not be parsed.");

            var cfg = new EventConfig
            {
                actionEventChance = d.action_events.p_event, maxActionEvents = d.action_events.max_per_segment,
                goodBase = d.good_chance.@base, goodMoodWeight = d.good_chance.mood_weight, goodStressWeight = d.good_chance.stress_weight,
                goodNeutral = d.good_chance.neutral, goodMin = d.good_chance.min, goodMax = d.good_chance.max,
                maxTraitModifier = d.max_trait_modifier,
                maxStateEvents = d.state_events.max_per_segment, stateAttemptChance = d.state_attempt_chance,
                choiceEventChance = d.choice_events.p_choice, maxChoiceEvents = d.choice_events.max_per_segment,
            };
            var mods = new List<TraitModifierDef>();
            foreach (var m in d.trait_modifiers) mods.Add(new TraitModifierDef { trait = m.trait, value = m.value });
            cfg.traitModifiers = mods.ToArray();
            var list = new List<EventDef>();
            for (int i = 0; i < d.events.Length; i++)
            {
                var x = d.events[i];
                string f = "events[" + i + "] (" + x.id + ")";
                var e = new EventDef
                {
                    id = x.id, kind = Map(Kinds, x.kind, f + ".kind"), tone = Map(Tones, x.tone, f + ".tone"),
                    action = Map(Actions, x.action, f + ".action"), subject = Map(Subjects, x.subject, f + ".subject"),
                    weight = x.weight, chance = x.chance, titleKey = x.title_key, textKey = x.text_key,
                    defaultOption = x.default_option,
                };
                var conds = new List<EventConditionDef>();
                for (int c = 0; c < x.conditions.Length; c++)
                    conds.Add(new EventConditionDef
                    {
                        type = Map(Conditions, x.conditions[c].type, f + ".conditions[" + c + "].type"),
                        value = x.conditions[c].value, trait = x.conditions[c].trait,
                    });
                e.conditions = conds.ToArray();
                e.effects = Effects(x.effects, f + ".effects");
                var opts = new List<ChoiceOptionDef>();
                for (int o = 0; o < x.options.Length; o++)
                    opts.Add(new ChoiceOptionDef { labelKey = x.options[o].label_key, effects = Effects(x.options[o].effects, f + ".options[" + o + "].effects") });
                e.options = opts.ToArray();
                list.Add(e);
            }
            cfg.events = list.ToArray();
            cfg.Validate(players);
            return cfg;
        }

        private static EventEffectDef[] Effects(EffectDto[] src, string f)
        {
            var r = new EventEffectDef[src.Length];
            for (int k = 0; k < src.Length; k++)
                r[k] = new EventEffectDef
                {
                    type = Map(EffectTypes, src[k].type, f + "[" + k + "].type"),
                    target = Map(Targets, src[k].target, f + "[" + k + "].target"),
                    min = src[k].min, max = src[k].max,
                };
            return r;
        }

        private static T Map<T>(Dictionary<string, T> map, string key, string field)
        {
            if (key != null && map.TryGetValue(key, out var v)) return v;
            throw new InvalidOperationException("EventConfig: " + field + " has unknown value '" + key + "' (allowed: " + string.Join(", ", map.Keys) + ").");
        }

        private static readonly Dictionary<string, EventKind> Kinds = new Dictionary<string, EventKind>
        { ["action"] = EventKind.Action, ["state"] = EventKind.State, ["choice"] = EventKind.Choice };
        private static readonly Dictionary<string, EventTone> Tones = new Dictionary<string, EventTone>
        { ["good"] = EventTone.Good, ["bad"] = EventTone.Bad, ["neutral"] = EventTone.Neutral };
        private static readonly Dictionary<string, EventAction> Actions = new Dictionary<string, EventAction>
        {
            [""] = EventAction.None, ["train"] = EventAction.Train, ["stream"] = EventAction.Stream, ["rest"] = EventAction.Rest,
            ["business"] = EventAction.Business, ["sponsor"] = EventAction.Sponsor,
        };
        private static readonly Dictionary<string, EventSubject> Subjects = new Dictionary<string, EventSubject>
        { ["player"] = EventSubject.Player, ["starter"] = EventSubject.Starter, ["pair"] = EventSubject.Pair, ["team"] = EventSubject.Team };
        private static readonly Dictionary<string, ConditionType> Conditions = new Dictionary<string, ConditionType>
        {
            ["stress_above"] = ConditionType.StressAbove, ["stress_below"] = ConditionType.StressBelow,
            ["mood_above"] = ConditionType.MoodAbove, ["mood_below"] = ConditionType.MoodBelow, ["trait_has"] = ConditionType.TraitHas,
            ["age_above"] = ConditionType.AgeAbove, ["age_below"] = ConditionType.AgeBelow,
            ["chemistry_below"] = ConditionType.ChemistryBelow, ["chemistry_above"] = ConditionType.ChemistryAbove,
            ["pair_bad_compat"] = ConditionType.PairBadCompat, ["recent_loss_streak"] = ConditionType.RecentLossStreak,
            ["recent_win_rate_above"] = ConditionType.RecentWinRateAbove, ["recent_win_rate_below"] = ConditionType.RecentWinRateBelow,
            ["funds_below"] = ConditionType.FundsBelow, ["club_popularity_at_least"] = ConditionType.ClubPopularityAtLeast,
        };
        private static readonly Dictionary<string, EffectType> EffectTypes = new Dictionary<string, EffectType>
        {
            ["mood"] = EffectType.Mood, ["stress"] = EffectType.Stress, ["player_popularity"] = EffectType.PlayerPopularity,
            ["club_popularity"] = EffectType.ClubPopularity, ["familiarity"] = EffectType.Familiarity, ["potential"] = EffectType.Potential,
            ["funds"] = EffectType.Funds, ["train_multiplier"] = EffectType.TrainMultiplier, ["train_add"] = EffectType.TrainAdd,
            ["stream_multiplier"] = EffectType.StreamMultiplier, ["stream_add"] = EffectType.StreamAdd,
            ["rest_multiplier"] = EffectType.RestMultiplier, ["business_multiplier"] = EffectType.BusinessMultiplier,
            ["sponsor_chance_add"] = EffectType.SponsorChanceAdd, ["void_action"] = EffectType.VoidAction, ["on_leave"] = EffectType.OnLeave,
        };
        private static readonly Dictionary<string, EffectTarget> Targets = new Dictionary<string, EffectTarget>
        {
            ["subject"] = EffectTarget.Subject, ["roster"] = EffectTarget.Roster, ["starters"] = EffectTarget.Starters,
            ["club"] = EffectTarget.Club, ["subject_with_starters"] = EffectTarget.SubjectWithStarters,
        };

        // Wire format: field names ARE the JSON keys.
        [Serializable] private class ActionEventsDto { public float p_event; public int max_per_segment; }
        [Serializable] private class GoodChanceDto { public float @base; public float mood_weight; public float stress_weight; public float neutral; public float min; public float max; }
        [Serializable] private class TraitModDto { public string trait; public float value; }
        [Serializable] private class StateEventsDto { public int max_per_segment; }
        [Serializable] private class ChoiceEventsDto { public float p_choice; public int max_per_segment; }
        [Serializable] private class ConditionDto { public string type; public float value; public string trait; }
        [Serializable] private class EffectDto { public string type; public string target; public float min; public float max; }
        [Serializable] private class OptionDto { public string label_key; public EffectDto[] effects; }
        [Serializable] private class EventDto
        {
            public string id; public string kind; public string tone; public string action; public string subject;
            public float weight; public float chance; public string title_key; public string text_key;
            public ConditionDto[] conditions; public EffectDto[] effects; public OptionDto[] options; public int default_option;
        }
        [Serializable] private class ConfigDto
        {
            public ActionEventsDto action_events;
            public GoodChanceDto good_chance;
            public float max_trait_modifier;
            public TraitModDto[] trait_modifiers;
            public StateEventsDto state_events;
            public float state_attempt_chance;
            public ChoiceEventsDto choice_events;
            public EventDto[] events;
        }
    }
}
