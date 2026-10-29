using System;
using EsportsManager.Players;

namespace EsportsManager.Segments
{
    /// <summary>Inputs of the mood formula that the runner derives from the segment (numbers.md 4.6).</summary>
    public struct MoodInputs
    {
        /// <summary>+1 win, -1 loss, 0 no result (segment level, applies to the whole roster).</summary>
        public int Result;
        /// <summary>True for a starter (+1 "play" term), false for the bench (-1).</summary>
        public bool IsStarter;
        /// <summary>(salary - expected) / expected, already bounded by the caller.</summary>
        public float SalarySatisfaction;
        /// <summary>Mean H with the other starters, in [-1, 1].</summary>
        public float Compatibility;
    }

    /// <summary>Stress terms of one player this segment (numbers.md 4.7).</summary>
    public struct StressInputs
    {
        /// <summary>Training points x train_intensity.</summary>
        public float TrainingIntensity;
        /// <summary>Matches played (starters only, decided by the runner).</summary>
        public int Matches;
        /// <summary>Matches lost.</summary>
        public int Losses;
        /// <summary>Accepted business deals.</summary>
        public int Business;
        /// <summary>Rest points (Story 006: effective points = sum of the rest event multipliers, 1 per plain point).</summary>
        public float Rest;
        /// <summary>Stream points.</summary>
        public int Streams;
        /// <summary>Extra raw stress inside the bracket (economy.md champion fatigue: +6 x f for starters), scaled like the other terms.</summary>
        public float Extra;
    }

    /// <summary>
    /// The Story 003 formulas as pure functions (no state, no random source: callers pass the already-rolled
    /// R factor), so they can be tested alone. design/numbers.md 4.2, 4.6, 4.7, 4.11, 4.12. Every number is
    /// read from <see cref="SegmentConfig"/>.
    /// </summary>
    public static class SegmentFormulas
    {
        /// <summary>
        /// Stream heat 0-100 (numbers.md 4.2): streams x trait multiplier (stream_fans) / stream_cap x 100, clamped.
        /// </summary>
        public static float StreamHeat(SegmentConfig cfg, Player player, int streams)
        {
            if (streams <= 0) return 0f;
            return Clamp100(streams * player.GetTraitMultiplier(PlayerEffectKeys.StreamFans) / cfg.stream.cap * 100f);
        }

        /// <summary>Business heat 0-100: accepted deals / heat_cap x 100, clamped.</summary>
        public static float BusinessHeat(SegmentConfig cfg, int accepted)
        {
            return accepted <= 0 ? 0f : Clamp100(accepted / cfg.business.heatCap * 100f);
        }

        /// <summary>
        /// Business trigger probability (numbers.md 4.11): clamp(WS(popularity, main-lane score, looks)/100 x p_max x business_rate, 0, p_max).
        /// </summary>
        public static float BusinessProbability(SegmentConfig cfg, Player p)
        {
            var b = cfg.business;
            float ws = (b.weightPopularity * p.Popularity + b.weightLaneScore * p.GetLaneScore(p.MainLane) + b.weightLooks * p.Looks)
                / (b.weightPopularity + b.weightLaneScore + b.weightLooks);
            float prob = ws / 100f * b.maxProbability * p.GetTraitMultiplier(PlayerEffectKeys.BusinessRate);
            return prob < 0f ? 0f : (prob > b.maxProbability ? b.maxProbability : prob);
        }

        /// <summary>
        /// Business offer amount in whole yuan (numbers.md 4.11): (a0 + a1 x popularity + a2 x looks) x business_amount x R,
        /// rounded away from zero at .5, never below 0. <paramref name="randomFactor"/> is R(r) already rolled.
        /// </summary>
        public static int BusinessAmount(SegmentConfig cfg, Player p, float randomFactor)
        {
            var b = cfg.business;
            double v = (b.baseAmount + (double)b.amountPerPopularity * p.Popularity + (double)b.amountPerLooks * p.Looks)
                * p.GetTraitMultiplier(PlayerEffectKeys.BusinessAmount) * randomFactor;
            return RoundMoney(v);
        }

        /// <summary>The club's share of a business amount, whole yuan (numbers.md 4.10).</summary>
        public static int ClubShare(SegmentConfig cfg, int amount) => RoundMoney(amount * (double)cfg.business.clubShare);

        /// <summary>
        /// Sponsorship success chance (numbers.md 4.12; economy.md F8): clamp(z0 + WS(club popularity, recent win rate)/100 x z_max + noise,
        /// min, max). The shipped config sets the win-rate weight to 0 (economy.md D4: no direct results term).
        /// <paramref name="noise"/> is U(-spread, +spread) already rolled.
        /// </summary>
        public static float SponsorChance(SegmentConfig cfg, float clubPopularity, float recentWinRate, float noise)
        {
            var s = cfg.sponsorship;
            float ws = (s.weightClubPopularity * clubPopularity + s.weightWinRate * recentWinRate) / (s.weightClubPopularity + s.weightWinRate);
            float chance = s.baseChance + ws / 100f * s.maxSuccessRate + noise;
            return chance < s.minChance ? s.minChance : (chance > s.maxChance ? s.maxChance : chance);
        }

        /// <summary>Contract amount per segment in whole yuan (numbers.md 4.12): c0 + c1 x club popularity.</summary>
        public static int ContractAmount(SegmentConfig cfg, float clubPopularity)
        {
            return RoundMoney(cfg.sponsorship.baseAmount + (double)cfg.sponsorship.amountPerClubPopularity * clubPopularity);
        }

        /// <summary>
        /// Stress change (numbers.md 4.7): [s_train x intensity + s_match x matches + s_loss x losses + s_biz x business
        /// - s_rest x rest - s_live x streams] x stress_gain trait multiplier x R - s_recover (numbers.md 4.7 v3: recovery is
        /// outside the bracket, applies every segment, not scaled by personality or R). The caller clamps the result of adding it.
        /// </summary>
        public static float StressDelta(SegmentConfig cfg, Player p, in StressInputs t, float randomFactor)
        {
            var s = cfg.stress;
            float raw = s.training * t.TrainingIntensity + MatchStressRaw(cfg, t.Matches, t.Losses) + s.business * t.Business
                - s.rest * t.Rest - s.stream * t.Streams + t.Extra;
            return raw * p.GetTraitMultiplier(PlayerEffectKeys.StressGain) * randomFactor - s.recovery;
        }

        /// <summary>The match part of the 4.7 bracket: s_match x series played + s_loss x series lost.</summary>
        public static float MatchStressRaw(SegmentConfig cfg, int matches, int losses) => cfg.stress.match * matches + cfg.stress.loss * losses;

        /// <summary>
        /// Stress from an event stage (numbers.md 4.18 / 4.7): ONLY the match terms of the 4.7 bracket, x stress_gain trait
        /// multiplier x R; no s_recover (that is a per-segment term). Same code path as <see cref="StressDelta"/>.
        /// </summary>
        public static float MatchStressDelta(SegmentConfig cfg, Player p, int matches, int losses, float randomFactor)
            => MatchStressRaw(cfg, matches, losses) * p.GetTraitMultiplier(PlayerEffectKeys.StressGain) * randomFactor;

        /// <summary>
        /// Mood from an event (numbers.md 4.18 / 4.6): ONLY the win/loss term of the 4.6 bracket (result = sign of series won -
        /// lost), x mood_change_magnitude x R; no regression, play, salary, chemistry or stress terms (those are per segment).
        /// </summary>
        public static float MatchMoodDelta(SegmentConfig cfg, Player p, int result, float randomFactor)
            => ResultTerm(cfg, p, result) * p.GetTraitMultiplier(PlayerEffectKeys.MoodChangeMagnitude) * randomFactor;

        private static float ResultTerm(SegmentConfig cfg, Player p, int resultSign)
        {
            float result = cfg.mood.result * resultSign;
            if (resultSign > 0) result *= p.GetTraitMultiplier(PlayerEffectKeys.WinMoodGain);
            else if (resultSign < 0) result *= p.GetTraitMultiplier(PlayerEffectKeys.LossMoodDrop) * p.GetTraitMultiplier(PlayerEffectKeys.NegativeMoodChange);
            return result;
        }

        /// <summary>
        /// Mood change (numbers.md 4.6): [m_result x result + m_play x play + m_salary x salary + m_chem x chemistry - m_stress x highStress]
        /// x personality x R + gamma x (neutral - mood). High stress = the player's CURRENT stress above the threshold.
        /// Personality (decision, numbers.md 3.2): win_mood_gain scales a winning result term, loss_mood_drop a losing one,
        /// bench_mood_drop the bench term, salary_satisfaction_weight the salary term; negative_mood_change scales every
        /// NEGATIVE term; mood_change_magnitude scales the whole bracket (not the regression part).
        /// </summary>
        public static float MoodDelta(SegmentConfig cfg, Player p, in MoodInputs t, float randomFactor)
        {
            var m = cfg.mood;
            float neg = p.GetTraitMultiplier(PlayerEffectKeys.NegativeMoodChange);

            float result = ResultTerm(cfg, p, t.Result);

            float play = t.IsStarter ? m.play : -m.play * p.GetTraitMultiplier(PlayerEffectKeys.BenchMoodDrop) * neg;

            float salary = m.salary * t.SalarySatisfaction * p.GetTraitMultiplier(PlayerEffectKeys.SalarySatisfactionWeight);
            if (salary < 0f) salary *= neg;

            float chem = m.chemistry * t.Compatibility;
            if (chem < 0f) chem *= neg;

            float stress = p.Stress > m.highStressThreshold ? -m.stress * neg : 0f;

            float bracket = (result + play + salary + chem + stress) * p.GetTraitMultiplier(PlayerEffectKeys.MoodChangeMagnitude);
            return bracket * randomFactor + m.regression * (m.neutral - p.Mood);
        }

        private static float Clamp100(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);

        private static int RoundMoney(double v)
        {
            if (v < 0.0) v = 0.0;
            if (v > int.MaxValue) v = int.MaxValue; // overflow guard only
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }
    }
}
