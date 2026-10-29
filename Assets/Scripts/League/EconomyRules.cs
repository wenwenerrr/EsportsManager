using System;
using EsportsManager.Players;

namespace EsportsManager.Leagues
{
    /// <summary>Salary breakdown for the UI (economy.md UI: base + league index + honor + specialty + years = salary, +-1 yuan).</summary>
    public sealed class SalaryBreakdown
    {
        /// <summary>Base part (F1 base x trait ask x R), yuan.</summary>
        public long Base;
        /// <summary>Wage-index part base x (WI - 1).</summary>
        public long WageIndex;
        /// <summary>Honor part base x WI x h x Honor / 100.</summary>
        public long Honor;
        /// <summary>Specialty part base x WI x s x n_spec.</summary>
        public long Specialty;
        /// <summary>Contract-years part (market x (F5 - 1)).</summary>
        public long Years;
        /// <summary>Total (the salary asked).</summary>
        public long Total;
    }

    /// <summary>
    /// The economy formulas (design/gdd/economy.md rev 3): F1 market salary with honor (F2, per player via <see cref="HonorOf"/>)
    /// and the wage index (F3, <see cref="WageIndex"/>), F5 contract-year factor, signing salary (F1 x F5 x R(0.1)), F4 value, the
    /// salary satisfaction of the mood formula, F6 renegotiation trigger and the F7 fire-sale price. All numbers from config.
    /// </summary>
    public sealed class EconomyRules
    {
        /// <summary>Current wage index WI (F3; starts at economy.wage_index, updated once a year by the transfer window).</summary>
        public double WageIndex { get; set; }

        /// <summary>Honor of a player (F2; the league keeps it per player). Null = 0 for everybody.</summary>
        public Func<Player, float> HonorOf { get; set; }

        /// <summary>The economy tuning.</summary>
        public LeagueEconomyConfig Config => _e;
        private readonly PlayerConfig _players;
        private readonly LeagueEconomyConfig _e;

        /// <summary>Creates the rules.</summary>
        public EconomyRules(PlayerConfig players, LeagueEconomyConfig economy)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _e = economy ?? throw new ArgumentNullException(nameof(economy));
            WageIndex = economy.wageIndex;
        }

        /// <summary>Honor used for a player (0 without <see cref="HonorOf"/>).</summary>
        public float Honor(Player p) => HonorOf != null ? HonorOf(p) : 0f;

        /// <summary>Centre of the player-facing potential estimate (PotEst, talent.md F3).</summary>
        public float PotentialEstimate(Player p)
        {
            var est = p.GetPotentialEstimate();
            return (est.Low + est.High) / 2f;
        }

        /// <summary>
        /// F1: (S_min + (S_max - S_min) x WS(R_main, Pop, PotEst)/100) x (1 + h x Honor/100 + s x n_spec) x trait_ask x WI.
        /// <paramref name="honor"/> null = the player's own honor; <paramref name="renewal"/> also applies the renewal ask multiplier.
        /// </summary>
        public double MarketSalary(Player p, float? honor = null, bool renewal = false)
        {
            float h = honor ?? Honor(p);
            return BaseSalary(p, renewal) * Bonus(p, h) * WageIndex;
        }

        private double BaseSalary(Player p, bool renewal)
        {
            var s = _players.salary;
            double ws = (p.GetLaneScore(p.MainLane) * s.weightLaneScore + p.Popularity * s.weightPopularity + PotentialEstimate(p) * s.weightPotential)
                / (s.weightLaneScore + s.weightPopularity + s.weightPotential);
            double baseSal = s.minSalary + (s.maxSalary - (double)s.minSalary) * ws / 100.0;
            double ask = p.GetTraitMultiplier(PlayerEffectKeys.AskPrice) * (renewal ? p.GetTraitMultiplier(PlayerEffectKeys.RenewalAskPrice) : 1f);
            return baseSal * ask;
        }

        private double Bonus(Player p, float honor) => 1.0 + _e.honorFactor * honor / 100.0 + _e.specialtyFactor * p.Specialties.Count;

        /// <summary>The keyed R(salary_random) factor of a signing (one draw).</summary>
        public float DrawSalaryRandom(IRandomSource rng) => rng.NextFloat(1f - _e.salaryRandom, 1f + _e.salaryRandom);

        /// <summary>
        /// Signing salary with an already-drawn R: F1 x F5 x R, whole yuan, plus its breakdown (base / WI / honor / specialty /
        /// years; the parts sum to the total, rounding put on the years part).
        /// </summary>
        public SalaryBreakdown Breakdown(Player p, int years, float r, bool renewal = false)
        {
            double b = BaseSalary(p, renewal) * r;
            double wi = WageIndex;
            float h = Honor(p);
            double market = b * wi * Bonus(p, h);
            long total = (long)Math.Round(Math.Max(0.0, market * YearFactor(p.Age, years)));
            var x = new SalaryBreakdown
            {
                Base = (long)Math.Round(b),
                WageIndex = (long)Math.Round(b * (wi - 1.0)),
                Honor = (long)Math.Round(b * wi * _e.honorFactor * h / 100.0),
                Specialty = (long)Math.Round(b * wi * _e.specialtyFactor * p.Specialties.Count),
                Total = total,
            };
            x.Years = total - x.Base - x.WageIndex - x.Honor - x.Specialty;
            return x;
        }

        /// <summary>
        /// Salary satisfaction (economy.md 心情中的薪资满意度): clamp(dz((salary - market) / market), min, max) with the dead zone
        /// dz(x) = sign(x) x max(0, |x| - dead); market = F1 without random and year factor.
        /// </summary>
        public float Satisfaction(Player p)
        {
            return SatisfactionFor(p.Salary, MarketSalary(p));
        }

        /// <summary>The satisfaction formula for a salary and a market salary (see <see cref="Satisfaction"/>).</summary>
        public float SatisfactionFor(double salary, double market)
        {
            if (market <= 0) return 0f;
            double x = (salary - market) / market;
            double dz = Math.Sign(x) * Math.Max(0.0, Math.Abs(x) - _e.satisfactionDeadZone);
            return (float)Math.Max(_e.satisfactionMin, Math.Min(_e.satisfactionMax, dz));
        }

        /// <summary>F6: contract left >= 1 and market >= r_ratio x salary.</summary>
        public bool WantsRenegotiation(Player p) => p.ContractYearsLeft >= 1 && MarketSalary(p) >= _e.renegRatio * p.Salary;

        /// <summary>F3: clamp(WI x (1 + kappa x (R / R_ref - 1)), WI_min, WI_max).</summary>
        public double NextWageIndex(double wi, double r)
            => Math.Max(_e.wiMin, Math.Min(_e.wiMax, wi * (1.0 + _e.wiKappa * (r / _e.wiRef - 1.0))));

        /// <summary>F2 honor score of a place (1-based; 0 = not placed) in a split or the finals.</summary>
        public float HonorForPlace(int place, bool finals)
        {
            var t = finals ? _e.honorFinals : _e.honorSplit;
            return place >= 1 && place <= t.Length ? t[place - 1] : 0f;
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
        public int SigningSalary(Player p, int years, IRandomSource rng, float factor = 1f, bool renewal = false)
        {
            float r = DrawSalaryRandom(rng);
            double v = MarketSalary(p, null, renewal) * YearFactor(p.Age, years) * r * factor;
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
