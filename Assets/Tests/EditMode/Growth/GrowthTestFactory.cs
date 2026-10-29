using System.Collections.Generic;
using System.Linq;
using EsportsManager.Growth;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Tests.Matches;
using EsportsManager.Tests.Players;

namespace EsportsManager.Tests.Growth
{
    /// <summary>
    /// In-memory fixtures for the Story 012 growth tests (design/gdd/growth.md rev 3): the real dimension ids and the match.md
    /// phase mapping (via <see cref="MatchTestFactory"/>), the growth.md knobs, and a subset of the specialty table. No file I/O.
    /// </summary>
    internal static class GrowthTestFactory
    {
        public const string Neutral1 = "reserved";
        public const string Neutral2 = "idealistic";

        public static SpecialtyDef Spec(string id, string lane, string phase, string dim, params SpecialtyEffectDef[] effects)
            => new SpecialtyDef { id = id, displayName = id, lane = lane, phase = phase, dimension = dim, effects = effects };

        public static SpecialtyEffectDef Fx(string hook, string phase, float value)
            => new SpecialtyEffectDef { hook = hook, phase = phase, value = value };

        /// <summary>growth.md rows used by the tests (values as in the table).</summary>
        public static SpecialtyDef[] Table() => new[]
        {
            Spec("solo_king", "top", "early", "kda", Fx("phase_flat", "early", 1.5f), Fx("star_scale", "early", 1.5f)),
            Spec("jungle_king", "jungle", "early", "gold_per_min", Fx("phase_flat", "early", 1.8f)),
            Spec("tempo_mid", "mid", "mid", "teamfight_participation", Fx("phase_flat", "mid", 1.5f), Fx("exec_add", "none", 0.1f)),
            Spec("iron_wall", "support", "mid", "damage_taken_per_death", Fx("phase_flat", "mid", 1.5f), Fx("sigma_scale", "none", 0.9f)),
            Spec("tactical_brain", "any", "none", "command", Fx("phase_flat", "mid", 1.0f), Fx("exec_add", "none", 0.15f), Fx("sigma_scale", "none", 0.85f)),
        };

        public static PlayerConfig MakeConfig()
        {
            var cfg = MatchTestFactory.MakePlayerConfig();
            cfg.specialty.specialties = Table();
            return cfg;
        }

        public static GrowthSystem MakeGrowth(PlayerConfig cfg = null)
        {
            cfg = cfg ?? MakeConfig();
            cfg.Validate();
            return new GrowthSystem(cfg, new GrowthPhaseMap(new PhaseLayout(MatchTestFactory.MakeConfig(), cfg)));
        }

        /// <summary>A neutral player (no training trait), all dimensions = fill, focus 0.2 / 0.2 / 0.2 unless given.</summary>
        public static Player MakePlayer(PlayerConfig cfg, string lane = "jungle", int age = 21, float potential = 65f, float fill = 50f,
            float mood = 60f, float stress = 30f, long id = 1, float[] focus = null)
        {
            var p = TestConfigFactory.MakePlayer(cfg, age: age, mainLane: lane, fill: fill, traitA: Neutral1, traitB: Neutral2,
                potential: potential, stress: stress, id: id);
            p.Mood = mood;
            focus = focus ?? new[] { 0.2f, 0.2f, 0.2f };
            for (int k = 0; k < 3; k++) p.SetFocus(k, focus[k]);
            return p;
        }

        /// <summary>Main-lane index of a dimension id.</summary>
        public static int Dim(PlayerConfig cfg, string lane, string id) => PlayerConfig.DimIndex(cfg.FindLane(lane), id);

        public static Dictionary<string, Player> Lineup(params Player[] starters) => starters.ToDictionary(p => p.MainLane, p => p);
    }
}
