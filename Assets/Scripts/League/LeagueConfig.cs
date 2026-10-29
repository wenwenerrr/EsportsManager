using System;
using System.Collections.Generic;
using System.Reflection;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Leagues
{
    /// <summary>
    /// League, talent and (opening / minimal) economy tuning (Assets/Data/league_config.json). Meaning of every number:
    /// design/gdd/league.md and talent.md "Tuning Knobs", economy.md F1 / F4 / F5 / F7. Loaded by <see cref="LeagueConfigLoader"/>.
    /// </summary>
    [Serializable]
    public sealed class LeagueConfig
    {
        /// <summary>Club names (at least club_count).</summary>
        public string[] clubNames;
        /// <summary>Clubs in the league (player + AI).</summary>
        public int clubCount;
        /// <summary>League generation (league.md 联赛构成与开局, F1).</summary>
        public LeagueGenerationConfig generation;
        /// <summary>AI per-segment rules (league.md AI 每赛段的决策, F2, F3).</summary>
        public LeagueAiConfig ai;
        /// <summary>AI tactics.</summary>
        public LeagueTacticConfig tactics;
        /// <summary>Style switch and labels (F4).</summary>
        public LeagueStyleConfig style;
        /// <summary>Warm-up job offers (预热与三份聘书).</summary>
        public LeagueOfferConfig offers;
        /// <summary>League news.</summary>
        public LeagueNewsConfig news;
        /// <summary>Talent system (talent.md).</summary>
        public TalentConfig talent;
        /// <summary>Economy (economy.md rev 3: F1-F7, honor, wage index, satisfaction, champion fatigue).</summary>
        public LeagueEconomyConfig economy;
        /// <summary>Transfer window (transfer.md rev 3).</summary>
        public TransferConfig transfer;
        /// <summary>Two-choice event id to F5 rule kind (leave / money / spend / conflict).</summary>
        public ChoiceRuleDef[] choiceRules;

        /// <summary>The F5 kind of a choice event id, or null.</summary>
        public string ChoiceKind(string eventId)
        {
            foreach (var r in choiceRules) if (r.eventId == eventId) return r.kind;
            return null;
        }

        /// <summary>Throws <see cref="InvalidOperationException"/> naming the first bad value.</summary>
        public void Validate()
        {
            if (clubCount < 4 || clubCount % 2 != 0) Fail("club_count must be an even number >= 4.");
            if (clubNames == null || clubNames.Length < clubCount) Fail("club_names needs at least club_count names.");
            var g = generation;
            if (g.fundsMax < g.fundsMin || g.popularityMax < g.popularityMin || g.familiarityMax < g.familiarityMin) Fail("generation ranges must be min <= max.");
            if (g.rosterBase < 5 || g.rosterExtraClubs < 0 || g.rosterExtraClubs > clubCount) Fail("generation roster sizes are invalid.");
            if (g.poolPerLane * 5 < clubCount * (g.rosterBase + 1)) Fail("generation.pool_per_lane is too small for the draft.");
            if (g.contractYearsMin < 1 || g.contractYearsMax > 3 || g.contractYearsMax < g.contractYearsMin) Fail("generation contract years must be within 1..3.");
            if (ai.minRoster < 5 || ai.fillRoster < ai.minRoster || ai.maxRoster < ai.fillRoster) Fail("ai roster sizes must be 5 <= min <= fill <= max.");
            if (ai.commandEvery < 1) Fail("ai.command_every must be >= 1.");
            if (tactics.radarStep <= 0) Fail("tactics.radar_step must be > 0.");
            if (style.lockEvents < 0) Fail("style.lock_events must be >= 0.");
            foreach (var r in new[] { offers.strongRanks, offers.midRanks, offers.weakRanks })
                if (r == null || r.Length != 2 || r[0] < 1 || r[1] > clubCount || r[1] < r[0]) Fail("offers rank ranges must be [lo, hi] within 1..club_count.");
            if (news.maxItems < 1) Fail("news.max_items must be >= 1.");
            var t = talent;
            if (t.potentialMin > t.potentialMax || t.sigma < 0f) Fail("talent potential range / sigma invalid.");
            if (t.poolCap < 0 || t.youthCount < 0 || t.freeCount < 0) Fail("talent counts must be >= 0.");
            if (economy.youngYearFactors == null || economy.youngYearFactors.Length != 3 || economy.oldYearFactors == null || economy.oldYearFactors.Length != 3)
                Fail("economy year factors need 3 values (1, 2, 3 years).");
            var e = economy;
            if (e.honorSplit == null || e.honorSplit.Length != clubCount || e.honorFinals == null || e.honorFinals.Length < 4)
                Fail("economy honor tables: honor_split needs club_count values, honor_finals at least 4.");
            if (e.wiMin <= 0f || e.wiMax < e.wiMin || e.wiRef <= 0f || e.wiKappa < 0f) Fail("economy wage index: 0 < wi_min <= wi_max, wi_ref > 0, wi_kappa >= 0.");
            if (e.satisfactionMin > 0f || e.satisfactionMax < 0f || e.satisfactionDeadZone < 0f) Fail("economy satisfaction bounds must bracket 0.");
            if (e.fatigueSegments < 0 || e.renegRatio <= 0f) Fail("economy fatigue_segments >= 0 and reneg_ratio > 0.");
            var x = transfer;
            if (x == null) Fail("transfer is missing.");
            if (x.poachOffersPerClub < 0 || x.lotsYouth < 0 || x.lotsFree < 0 || x.maxListed < 1) Fail("transfer counts must be >= 0 (max_listed >= 1).");
            if (x.stepMin <= 0f || x.stepPct < 0f || x.priceRound <= 0f) Fail("transfer bid step must be positive.");
            if (x.feeMax < x.feeMin || x.listedStartMax < x.listedStartMin) Fail("transfer ranges must be min <= max.");
            if (x.resignTarget < ai.minRoster || x.resignTarget > ai.maxRoster) Fail("transfer.resign_target must be within the roster limits.");
            if (choiceRules == null) Fail("choice_rules is missing.");
            foreach (var r in choiceRules)
                if (r.kind != "leave" && r.kind != "money" && r.kind != "spend" && r.kind != "conflict") Fail("choice_rules kind '" + r.kind + "' is unknown.");
        }

        private static void Fail(string m) => throw new InvalidOperationException("LeagueConfig: " + m);
    }

    /// <summary>See league_config.json "generation".</summary>
    [Serializable]
    public sealed class LeagueGenerationConfig
    {
        public float fundsMin, fundsMax, popularityMin, popularityMax, familiarityMin, familiarityMax;
        public int openingSponsors, sponsorSegmentsMin, sponsorSegmentsMax;
        public int poolPerLane, poolAgeMin, poolAgeMax, rosterBase, rosterExtraClubs;
        public float openingLeaguePopularity;
        public int contractYearsMin, contractYearsMax;
        public float openingSalaryFactor, styleWeight, starWeight, agePenalty;
        public int agePenaltyFrom;
        public float offsetRest, offsetCash, offsetRenew, offsetBid;
    }

    /// <summary>See league_config.json "ai".</summary>
    [Serializable]
    public sealed class LeagueAiConfig
    {
        public float restBase, restAggression, restPreEvent, restDoubleMargin, floorBase, floorFrugal;
        public float coverChance, chaseValue, chaseFocus, commandThreshold;
        public int commandEvery, prospectMaxAge;
        public float lineupHysteresis;
        public int streamCount;
        public float emergencyBusinessStress, normalBusinessStress, moneyChoiceStress;
        public int minRoster, fillRoster, maxRoster;
    }

    /// <summary>See league_config.json "tactics".</summary>
    [Serializable]
    public sealed class LeagueTacticConfig
    {
        public float styleNoise, radarStep;
    }

    /// <summary>See league_config.json "style".</summary>
    [Serializable]
    public sealed class LeagueStyleConfig
    {
        public float switchChance;
        public int lockEvents, missedEvents, topPlaces;
        public float starSwitchLead, starLabelLead;
        public int rareMax;
    }

    /// <summary>See league_config.json "offers".</summary>
    [Serializable]
    public sealed class LeagueOfferConfig
    {
        public int[] strongRanks, midRanks, weakRanks;
        public float injectSegments, weakInjectSegments;
        public int minRoster;
    }

    /// <summary>See league_config.json "news".</summary>
    [Serializable]
    public sealed class LeagueNewsConfig
    {
        public int maxItems;
    }

    /// <summary>talent.md tuning (see league_config.json "talent").</summary>
    [Serializable]
    public sealed class TalentConfig
    {
        public float talentChance, talentPotentialMin, talentPotentialMax, meanBase, meanPerPopularity, sigma, potentialMin, potentialMax;
        public int youthCount, youthAgeMin, youthAgeMax, freeCount, freeAgeMin, freeAgeMax;
        public int youthContract, freeContract, scoutContract, scoutAgeMin, scoutAgeMax;
        public float scoutCost, scoutT0, scoutT1, scoutTMax, scoutWeakMargin;
        public int poolCap;
        public int retireSignedAge;
        public float retireSignedChance;
        public int retireSignedMustAge, retireFreeAge;
        public float retireFreeChance;
        public int retireFreeMustAbove, emergencyAge;
    }

    /// <summary>economy.md pieces (see league_config.json "economy").</summary>
    [Serializable]
    public sealed class LeagueEconomyConfig
    {
        public float honorFactor, specialtyFactor, valueBase, valueSpecialty;
        public int valueYouthAge;
        public float valueYouthPerYear, valueWeightRating, valueWeightPotential, valueWeightYouth;
        public float fireSale, repay, wageIndex;
        public int youngAge, oldAge;
        public float[] youngYearFactors, oldYearFactors;
        public float salaryRandom;
        public int signYears;
        /// <summary>F2 honor by place: splits (one per place) and finals (one per finals place).</summary>
        public float[] honorSplit, honorFinals;
        /// <summary>F3 wage index: kappa, R_ref, clamp.</summary>
        public float wiKappa, wiRef, wiMin, wiMax;
        /// <summary>Salary satisfaction clamp and dead zone (mood m_salary term).</summary>
        public float satisfactionMin, satisfactionMax, satisfactionDeadZone;
        /// <summary>F6 renegotiation trigger ratio and the mood drop on a refusal.</summary>
        public float renegRatio, renegMoodDrop;
        /// <summary>F7 debt line (share of yearly wages) for the forced listing.</summary>
        public float debtLine;
        /// <summary>Champion fatigue: strength of a finals / split title, duration, starter stress per f, business factor per f, media points per f.</summary>
        public float fatigueFinals, fatigueSplit;
        public int fatigueSegments;
        public float fatigueStress, fatigueBusiness, fatigueMediaPoints;
    }

    /// <summary>transfer.md rev 3 tuning (see league_config.json "transfer").</summary>
    [Serializable]
    public sealed class TransferConfig
    {
        /// <summary>Bench renewal threshold value / (salary / WI).</summary>
        public float benchRenew;
        /// <summary>AI renewal years: age &lt;= young -> 3, &lt;= mid -> 2, else 1; starters older than age_test also face the bench test.</summary>
        public int renewYoungMaxAge, renewMidMaxAge, renewAgeTestAbove;
        /// <summary>Poach offers per AI club per year (rounds), fee range, score bonuses, contract years.</summary>
        public int poachOffersPerClub;
        public float feeMin, feeMax, poachWeakLaneBonus, poachWantsBonus;
        public int poachContractYears;
        /// <summary>Retain raise multiplier, AI accept-fee line (x value), AI retain test (new salary &lt;= value / ratio).</summary>
        public float retainMult, aiAcceptFee, retainValueRatio;
        /// <summary>League lots: best-estimate youth and best-rated free agents.</summary>
        public int lotsYouth, lotsFree;
        /// <summary>F2 start prices.</summary>
        public float leagueStartFactor, listedStartMin, listedStartMax, listedContractBase, listedContractPerYear;
        /// <summary>F3 bid step and price rounding.</summary>
        public float stepMin, stepPct, priceRound;
        /// <summary>F4 AI max bid.</summary>
        public float bidBase, bidNeed, bidAggression, bidNoise;
        /// <summary>AI spending reserve in segments of wages (auction bids, three-choice rule 3 / 4).</summary>
        public float reserveSegments;
        /// <summary>Most players one club lists.</summary>
        public int maxListed;
        /// <summary>Contracts of auction buys (youth / free), re-signs and the re-sign target roster.</summary>
        public int auctionYouthContract, auctionFreeContract, resignContract, resignTarget;
    }

    /// <summary>One F5 rule mapping.</summary>
    [Serializable]
    public sealed class ChoiceRuleDef
    {
        public string eventId, kind;
    }

    /// <summary>
    /// Parses league_config.json. WIRE-FORMAT BOUNDARY (.claude/rules/data-files.md): the file uses snake_case keys; it is first
    /// checked strictly by <see cref="JsonShapeChecker"/> against the private snake_case DTO, read by JsonUtility, then copied
    /// into the camelCase <see cref="LeagueConfig"/> by a name mapping (snake_case key "club_names" -> field "clubNames").
    /// </summary>
    public static class LeagueConfigLoader
    {
        /// <summary>Parses and validates; every problem is an <see cref="InvalidOperationException"/>.</summary>
        public static LeagueConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("LeagueConfig: JSON text is empty.");
            JsonShapeChecker.Check(json, typeof(Dto), "LeagueConfig");
            Dto dto;
            try { dto = JsonUtility.FromJson<Dto>(json); }
            catch (Exception e) { throw new InvalidOperationException("LeagueConfig: JSON could not be parsed: " + e.Message, e); }
            var cfg = (LeagueConfig)Map(dto, typeof(LeagueConfig));
            cfg.Validate();
            return cfg;
        }

        // Copies a snake_case DTO graph into the camelCase config types (same shape, names differ only by case style).
        private static object Map(object src, Type targetType)
        {
            if (src == null) return null;
            var srcType = src.GetType();
            if (targetType == typeof(string) || targetType.IsPrimitive) return src;
            if (targetType.IsArray)
            {
                var arr = (Array)src;
                var el = targetType.GetElementType();
                var outArr = Array.CreateInstance(el, arr.Length);
                for (int i = 0; i < arr.Length; i++) outArr.SetValue(Map(arr.GetValue(i), el), i);
                return outArr;
            }
            var target = Activator.CreateInstance(targetType);
            foreach (var f in srcType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var tf = targetType.GetField(Camel(f.Name), BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("LeagueConfig: no field for key '" + f.Name + "' in " + targetType.Name + ".");
                tf.SetValue(target, Map(f.GetValue(src), tf.FieldType));
            }
            return target;
        }

        private static string Camel(string snake)
        {
            var parts = snake.Split('_');
            var s = parts[0];
            for (int i = 1; i < parts.Length; i++) if (parts[i].Length > 0) s += char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return s;
        }

#pragma warning disable 0649
        [Serializable] private sealed class Dto
        {
            public string[] club_names; public int club_count; public GenDto generation; public AiDto ai; public TacDto tactics;
            public StyleDto style; public OfferDto offers; public NewsDto news; public TalentDto talent; public EconDto economy;
            public XferDto transfer; public RuleDto[] choice_rules;
        }
        [Serializable] private sealed class GenDto
        {
            public float funds_min, funds_max, popularity_min, popularity_max, familiarity_min, familiarity_max;
            public int opening_sponsors, sponsor_segments_min, sponsor_segments_max;
            public int pool_per_lane, pool_age_min, pool_age_max, roster_base, roster_extra_clubs;
            public float opening_league_popularity;
            public int contract_years_min, contract_years_max;
            public float opening_salary_factor, style_weight, star_weight, age_penalty;
            public int age_penalty_from;
            public float offset_rest, offset_cash, offset_renew, offset_bid;
        }
        [Serializable] private sealed class AiDto
        {
            public float rest_base, rest_aggression, rest_pre_event, rest_double_margin, floor_base, floor_frugal;
            public float cover_chance, chase_value, chase_focus, command_threshold;
            public int command_every, prospect_max_age;
            public float lineup_hysteresis;
            public int stream_count;
            public float emergency_business_stress, normal_business_stress, money_choice_stress;
            public int min_roster, fill_roster, max_roster;
        }
        [Serializable] private sealed class TacDto { public float style_noise, radar_step; }
        [Serializable] private sealed class StyleDto
        {
            public float switch_chance; public int lock_events, missed_events, top_places; public float star_switch_lead, star_label_lead; public int rare_max;
        }
        [Serializable] private sealed class OfferDto
        {
            public int[] strong_ranks, mid_ranks, weak_ranks; public float inject_segments, weak_inject_segments; public int min_roster;
        }
        [Serializable] private sealed class NewsDto { public int max_items; }
        [Serializable] private sealed class TalentDto
        {
            public float talent_chance, talent_potential_min, talent_potential_max, mean_base, mean_per_popularity, sigma, potential_min, potential_max;
            public int youth_count, youth_age_min, youth_age_max, free_count, free_age_min, free_age_max;
            public int youth_contract, free_contract, scout_contract, scout_age_min, scout_age_max;
            public float scout_cost, scout_t0, scout_t1, scout_t_max, scout_weak_margin;
            public int pool_cap;
            public int retire_signed_age;
            public float retire_signed_chance;
            public int retire_signed_must_age, retire_free_age;
            public float retire_free_chance;
            public int retire_free_must_above, emergency_age;
        }
        [Serializable] private sealed class EconDto
        {
            public float honor_factor, specialty_factor, value_base, value_specialty;
            public int value_youth_age;
            public float value_youth_per_year, value_weight_rating, value_weight_potential, value_weight_youth;
            public float fire_sale, repay, wage_index;
            public int young_age, old_age;
            public float[] young_year_factors, old_year_factors;
            public float salary_random;
            public int sign_years;
            public float[] honor_split, honor_finals;
            public float wi_kappa, wi_ref, wi_min, wi_max;
            public float satisfaction_min, satisfaction_max, satisfaction_dead_zone;
            public float reneg_ratio, reneg_mood_drop, debt_line;
            public float fatigue_finals, fatigue_split;
            public int fatigue_segments;
            public float fatigue_stress, fatigue_business, fatigue_media_points;
        }
        [Serializable] private sealed class XferDto
        {
            public float bench_renew;
            public int renew_young_max_age, renew_mid_max_age, renew_age_test_above;
            public int poach_offers_per_club;
            public float fee_min, fee_max, poach_weak_lane_bonus, poach_wants_bonus;
            public int poach_contract_years;
            public float retain_mult, ai_accept_fee, retain_value_ratio;
            public int lots_youth, lots_free;
            public float league_start_factor, listed_start_min, listed_start_max, listed_contract_base, listed_contract_per_year;
            public float step_min, step_pct, price_round;
            public float bid_base, bid_need, bid_aggression, bid_noise;
            public float reserve_segments;
            public int max_listed;
            public int auction_youth_contract, auction_free_contract, resign_contract, resign_target;
        }
        [Serializable] private sealed class RuleDto { public string event_id, kind; }
#pragma warning restore 0649
    }
}
