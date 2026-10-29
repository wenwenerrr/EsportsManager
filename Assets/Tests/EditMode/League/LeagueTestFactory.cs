using System.IO;
using EsportsManager.Events;
using EsportsManager.Leagues;
using EsportsManager.Matches;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Session;
using EsportsManager.Teams;

namespace EsportsManager.Tests.Leagues
{
    /// <summary>Story 013 fixtures: the shipped configs (read once) and league / session builders.</summary>
    internal static class LeagueTestFactory
    {
        public const int Seed = 20261028;

        public static string Data(string f) => File.ReadAllText("Assets/Data/" + f);

        private static PlayerConfig _p; private static TeamConfig _t; private static SegmentConfig _s; private static MatchConfig _m;
        private static EventConfig _e;

        public static PlayerConfig Players => _p ?? (_p = PlayerConfigLoader.FromJson(Data("player_config.json")));
        public static TeamConfig Team => _t ?? (_t = TeamConfigLoader.FromJson(Data("team_config.json")));
        public static SegmentConfig Segment => _s ?? (_s = SegmentConfigLoader.FromJson(Data("segment_config.json")));
        public static MatchConfig Match => _m ?? (_m = MatchConfigLoader.FromJson(Data("match_config.json")));
        public static EventConfig Events => _e ?? (_e = EventConfigLoader.FromJson(Data("event_config.json"), Players));
        /// <summary>A fresh league config (tests may change it).</summary>
        public static LeagueConfig League() => LeagueConfigLoader.FromJson(Data("league_config.json"));

        /// <summary>A generated league (year 0, nothing played).</summary>
        public static LeagueWorld World(long seed = Seed, LeagueConfig cfg = null, bool events = true)
            => new LeagueWorld(Players, Team, Segment, Match, events ? Events : null, cfg ?? League(), seed);

        /// <summary>A session with the league; offers pending unless <paramref name="take"/>.</summary>
        public static GameSession MakeSession(int seed = Seed, bool take = true)
            => GameSession.FromJson(Data("player_config.json"), Data("team_config.json"), Data("segment_config.json"), Data("match_config.json"),
                Data("event_config.json"), Data("league_config.json"), seed, GameSession.DefaultFunds, take);
    }
}
