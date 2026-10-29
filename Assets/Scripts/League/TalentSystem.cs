using System;
using System.Collections.Generic;
using EsportsManager.Growth;
using EsportsManager.Players;

namespace EsportsManager.Leagues
{
    /// <summary>
    /// The talent system (design/gdd/talent.md rev 3): player generation (potential first, dimensions as fractions of it, initial
    /// focus and the generation specialty check through <see cref="GrowthSystem"/>), the supply mean (F1), the national scouting
    /// talent chance (F2), retirement and the free-agent pool cap. Ids are assigned here and never reused; names are unique
    /// across the league (a number is appended once the name table is used up).
    /// </summary>
    /// <remarks>
    /// DRAWS (on the caller's keyed source, in this order per player): potential (talent U01, then U(85,95) or one Gaussian =
    /// two draws), main-lane then other lanes' dimensions in config order, age, two trait groups + two words, looks, popularity,
    /// mood, stress, scouting bias, name.
    /// </remarks>
    public sealed class TalentSystem
    {
        private readonly PlayerConfig _players;
        private readonly TalentConfig _t;
        private readonly GrowthSystem _growth;
        private readonly HashSet<string> _usedNames = new HashSet<string>();

        /// <summary>Id of the next generated player.</summary>
        public long NextId { get; private set; }

        /// <summary>The talent tuning.</summary>
        public TalentConfig Config => _t;

        /// <summary>Creates the system; <paramref name="growth"/> may be null (tests: no focus / specialty initialisation).</summary>
        public TalentSystem(PlayerConfig players, TalentConfig talent, GrowthSystem growth, long firstId = 1)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _t = talent ?? throw new ArgumentNullException(nameof(talent));
            _growth = growth;
            NextId = firstId;
        }

        /// <summary>F1: mu_pot = y0 + y1 x leaguePopularity / 100.</summary>
        public float SupplyMean(float leaguePopularity) => _t.meanBase + _t.meanPerPopularity * leaguePopularity / 100f;

        /// <summary>Potential draw: talent with <paramref name="talentChance"/> (U(85,95)), else clamp(N(mu, sigma), 40, 84).</summary>
        public float DrawPotential(KeyedStream rng, float mu, float talentChance)
        {
            if (rng.NextDouble() < talentChance) return rng.NextFloat(_t.talentPotentialMin, _t.talentPotentialMax);
            double v = mu + _t.sigma * rng.NextGaussian();
            return (float)Math.Max(_t.potentialMin, Math.Min(_t.potentialMax, v));
        }

        /// <summary>
        /// Generates one player of <paramref name="lane"/> aged U{ageMin..ageMax}. <paramref name="potential"/> (tests) skips the
        /// potential draw. Contract 0, salary 0 (set when signed).
        /// </summary>
        public Player Generate(KeyedStream rng, string lane, int ageMin, int ageMax, float mu, float? talentChance = null, float? potential = null)
        {
            if (_players.FindLane(lane) == null) throw new ArgumentException("Unknown lane: " + lane, nameof(lane));
            float pot = potential ?? DrawPotential(rng, mu, talentChance ?? _t.talentChance);
            var g = _players.generation;
            var radar = new Dictionary<string, float[]>();
            foreach (var l in _players.lanes)
            {
                bool main = l.id == lane;
                float lo = main ? g.mainLaneDimFractionMin : g.offLaneDimFractionMin;
                float hi = main ? g.mainLaneDimFractionMax : g.offLaneDimFractionMax;
                var v = new float[l.dimensions.Length];
                for (int i = 0; i < v.Length; i++) v[i] = pot * rng.NextFloat(lo, hi);
                radar[l.id] = v;
            }
            int age = rng.NextInt(ageMin, ageMax + 1);
            int groups = _players.personalityGroups.Length;
            int first = rng.NextInt(0, groups);
            int second = rng.NextInt(0, groups - 1);
            if (second >= first) second++;
            string ta = _players.personalityGroups[first].traits[rng.NextInt(0, 2)].id;
            string tb = _players.personalityGroups[second].traits[rng.NextInt(0, 2)].id;
            float looks = rng.NextFloat(g.looksMin, g.looksMax);
            float pop = rng.NextFloat(g.popularityMin, g.popularityMax);
            float mood = rng.NextFloat(g.moodMin, g.moodMax);
            float stress = rng.NextFloat(g.stressMin, g.stressMax);
            float bias = rng.NextFloat(-_players.potential.scoutingBias, _players.potential.scoutingBias);
            string name = UniqueName(rng);
            var p = new Player(_players, NextId++, name, age, lane, ta, tb, pot, 0, looks, pop, mood, stress, 0, radar, bias);
            if (_growth != null) _growth.InitializeNewPlayer(p);
            return p;
        }

        /// <summary>Marks a name as taken (players created elsewhere).</summary>
        public void ReserveName(string name) { if (name != null) _usedNames.Add(name); }

        private string UniqueName(KeyedStream rng)
        {
            var ids = _players.generation.playerIds;
            var free = new List<string>();
            foreach (var id in ids) if (!_usedNames.Contains(id)) free.Add(id);
            if (free.Count > 0) { string pick = free[rng.NextInt(0, free.Count)]; _usedNames.Add(pick); return pick; }
            string b = ids[rng.NextInt(0, ids.Length)];
            int n = 2; string c;
            do { c = b + n++; } while (!_usedNames.Add(c));
            return c;
        }

        /// <summary>
        /// F2 scouting talent chance: clamp(t0 + t1 x need, t0, t_max). need = 1 when the club has no main-lane player in the lane
        /// or its best one is &lt;= the league median of every club's best in that lane - margin; else 0.5 with exactly one
        /// main-lane player; else 0.
        /// </summary>
        public float ScoutTalentChance(int mainLanePlayers, float clubBest, float leagueMedianBest)
        {
            float need = mainLanePlayers == 0 || clubBest <= leagueMedianBest - _t.scoutWeakMargin ? 1f : mainLanePlayers == 1 ? 0.5f : 0f;
            return Math.Max(_t.scoutT0, Math.Min(_t.scoutTMax, _t.scoutT0 + _t.scoutT1 * need));
        }

        /// <summary>Retirement of a signed player AFTER the yearly age + 1 (contract expired and >= 31: 50%; >= 33: always).</summary>
        public bool RetiresSigned(Player p, Func<double> u)
        {
            if (!p.IsContractExpired || p.Age < _t.retireSignedAge) return false;
            return p.Age >= _t.retireSignedMustAge || u() < _t.retireSignedChance;
        }

        /// <summary>Retirement of a free agent AFTER the yearly age + 1 (> 30: always; >= 28: 30%).</summary>
        public bool RetiresFree(Player p, Func<double> u)
        {
            if (p.Age > _t.retireFreeMustAbove) return true;
            return p.Age >= _t.retireFreeAge && u() < _t.retireFreeChance;
        }

        /// <summary>
        /// Pool cap: when the pool is over pool_cap, removes the lowest-rated players (main-lane score, ties lower id first) among
        /// those not exempt until the cap holds or no candidate is left. Returns the removed players.
        /// </summary>
        public List<Player> ApplyPoolCap(List<Player> pool, Func<Player, bool> exempt)
        {
            var removed = new List<Player>();
            int over = pool.Count - _t.poolCap;
            if (over <= 0) return removed;
            var cands = pool.FindAll(p => exempt == null || !exempt(p));
            cands.Sort((a, b) =>
            {
                int c = a.GetLaneScore(a.MainLane).CompareTo(b.GetLaneScore(b.MainLane));
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            for (int i = 0; i < over && i < cands.Count; i++) { pool.Remove(cands[i]); removed.Add(cands[i]); }
            return removed;
        }
    }
}
