using System;
using EsportsManager.Players;

namespace EsportsManager.Leagues
{
    /// <summary>
    /// The economy formulas Story 013 needs (design/gdd/economy.md rev 3): F1 market salary, F5 contract-year factor, signing
    /// salary (F1 x F5 x R(0.1)), F4 value and the F7 fire-sale price. Honor (F2) and the wage index (F3) belong to Story 005:
    /// honor is a parameter (0 until then) and the wage index comes from config (1.0). Pure functions; all numbers from config.
    /// </summary>
    public sealed class EconomyRules
    {
        private readonly PlayerConfig _players;
        private readonly LeagueEconomyConfig _e;

        /// <summary>Creates the rules.</summary>
        public EconomyRules(PlayerConfig players, LeagueEconomyConfig economy)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _e = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        /// <summary>Centre of the player-facing potential estimate (PotEst, talent.md F3).</summary>
        public float PotentialEstimate(Player p)
        {
            var est = p.GetPotentialEstimate();
            return (est.Low + est.High) / 2f;
        }

        /// <summary>
        /// F1: (S_min + (S_max - S_min) x WS(R_main, Pop, PotEst)/100) x (1 + h x Honor/100 + s x n_spec) x trait_ask x WI.
        /// <paramref name="renewal"/> also applies the renewal ask multiplier.
        /// </summary>
        public double MarketSalary(Player p, float honor = 0f, bool renewal = false)
        {
            var s = _players.salary;
            double ws = (p.GetLaneScore(p.MainLane) * s.weightLaneScore + p.Popularity * s.weightPopularity + PotentialEstimate(p) * s.weightPotential)
                / (s.weightLaneScore + s.weightPopularity + s.weightPotential);
            double baseSal = s.minSalary + (s.maxSalary - (double)s.minSalary) * ws / 100.0;
            double bonus = 1.0 + _e.honorFactor * honor / 100.0 + _e.specialtyFactor * p.Specialties.Count;
            double ask = p.GetTraitMultiplier(PlayerEffectKeys.AskPrice) * (renewal ? p.GetTraitMultiplier("renewal_ask_price") : 1f);
            return baseSal * bonus * ask * _e.wageIndex;
        }

        /// <summary>F5 contract-year factor (1..3 years).</summary>
        public float YearFactor(int age, int years)
        {
            if (years < 1 || years > 3) throw new ArgumentOutOfRangeException(nameof(years), "1..3 years.");
            if (age <= _e.youngAge) return _e.youngYearFactors[years - 1];
            if (age >= _e.oldAge) return _e.oldYearFactors[years - 1];
            return 1f;
        }

        /// <summary>Signing salary = F1 x F5 x R(salary_random) x <paramref name="factor"/> (opening league 0.85), whole yuan.</summary>
        public int SigningSalary(Player p, int years, IRandomSource rng, float factor = 1f)
        {
            float r = rng.NextFloat(1f - _e.salaryRandom, 1f + _e.salaryRandom);
            double v = MarketSalary(p) * YearFactor(p.Age, years) * r * factor;
            return (int)Math.Round(Math.Max(0.0, Math.Min(int.MaxValue, v)));
        }

        /// <summary>F4 value = V_base x WS(R_main, PotEst, Youth)/100 x (1 + v_s x n_spec), Youth = clamp((32 - age) x 8, 0, 100).</summary>
        public double Value(Player p)
        {
            double youth = Math.Max(0.0, Math.Min(100.0, (_e.valueYouthAge - p.Age) * (double)_e.valueYouthPerYear));
            double ws = (p.GetLaneScore(p.MainLane) * _e.valueWeightRating + PotentialEstimate(p) * _e.valueWeightPotential + youth * _e.valueWeightYouth)
                / (_e.valueWeightRating + _e.valueWeightPotential + _e.valueWeightYouth);
            return _e.valueBase * ws / 100.0 * (1.0 + _e.valueSpecialty * p.Specialties.Count);
        }

        /// <summary>F7: the league pays value x fire_sale for a player sold in self-rescue (whole yuan).</summary>
        public long FireSalePrice(Player p) => (long)Math.Round(Value(p) * _e.fireSale);

        /// <summary>F7 repayment share of AI prize / sale income while in debt.</summary>
        public float RepayShare => _e.repay;
    }
}
