using System;
using EsportsManager.Players;

namespace EsportsManager.Matches
{
    /// <summary>
    /// The phase mapping of a <see cref="MatchConfig"/> resolved against a <see cref="PlayerConfig"/>: lanes in PlayerConfig order,
    /// for every lane the dimension INDEXES of each phase, the command dimension index and the lane weights Lw[k][lane].
    /// Built once per simulator (validates the config against the player config).
    /// </summary>
    public sealed class PhaseLayout
    {
        /// <summary>Lane ids, PlayerConfig order.</summary>
        public string[] LaneIds { get; }
        /// <summary>[lane][phase] -> dimension indexes of that lane's radar.</summary>
        public int[][][] DimIndexes { get; }
        /// <summary>[lane] -> index of the command dimension.</summary>
        public int[] CommandIndex { get; }
        /// <summary>[phase][lane] -> Lw.</summary>
        public double[][] LaneWeights { get; }

        /// <summary>PlayerConfig.off_lane_discount (lane_f off the main lane, before the clever multiplier).</summary>
        public double OffLaneDiscount { get; }

        /// <summary>Number of lanes.</summary>
        public int LaneCount => LaneIds.Length;

        /// <summary>Resolves the mapping; throws <see cref="InvalidOperationException"/> when the configs do not fit together.</summary>
        public PhaseLayout(MatchConfig config, PlayerConfig players)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (players == null) throw new ArgumentNullException(nameof(players));
            config.ValidateAgainst(players);
            int n = players.lanes.Length;
            OffLaneDiscount = players.offLaneDiscount;
            LaneIds = new string[n];
            DimIndexes = new int[n][][];
            CommandIndex = new int[n];
            LaneWeights = new double[MatchPhases.Count][];
            for (int k = 0; k < MatchPhases.Count; k++) LaneWeights[k] = new double[n];
            for (int i = 0; i < n; i++)
            {
                var lane = players.lanes[i];
                var map = config.phases.Find(lane.id);
                LaneIds[i] = lane.id;
                DimIndexes[i] = new int[MatchPhases.Count][];
                foreach (var k in MatchPhases.All)
                {
                    var ids = map.Dims(k);
                    var idx = new int[ids.Length];
                    for (int j = 0; j < ids.Length; j++) idx[j] = IndexOf(lane, ids[j]);
                    DimIndexes[i][(int)k] = idx;
                    LaneWeights[(int)k][i] = map.Weight(k);
                }
                CommandIndex[i] = IndexOf(lane, config.phases.commandDimensionId);
            }
        }

        /// <summary>Index of a lane id, or -1.</summary>
        public int LaneIndex(string laneId) => Array.IndexOf(LaneIds, laneId);

        private static int IndexOf(LaneDef lane, string id)
        {
            for (int i = 0; i < lane.dimensions.Length; i++) if (lane.dimensions[i].id == id) return i;
            throw new InvalidOperationException("MatchConfig: dimension '" + id + "' is not in lane '" + lane.id + "'.");
        }
    }
}
