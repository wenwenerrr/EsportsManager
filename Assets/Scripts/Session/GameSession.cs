using System;
using System.Collections.Generic;
using EsportsManager.Players;
using EsportsManager.Segments;
using EsportsManager.Teams;

namespace EsportsManager.Session
{
    /// <summary>Everything the main screen shows about one player. Deliberately has NO true-potential field.</summary>
    public sealed class PlayerCardData
    {
        /// <summary>Player.Id.</summary>
        public long Id;
        /// <summary>Display name.</summary>
        public string Name;
        /// <summary>Age in years.</summary>
        public int Age;
        /// <summary>Main lane id.</summary>
        public string LaneId;
        /// <summary>Main lane display name (from config).</summary>
        public string LaneName;
        /// <summary>The two personality words (display names from config).</summary>
        public string[] TraitNames;
        /// <summary>Mood 0-100.</summary>
        public float Mood;
        /// <summary>Stress 0-100.</summary>
        public float Stress;
        /// <summary>Looks 0-100.</summary>
        public float Looks;
        /// <summary>Popularity 0-100.</summary>
        public float Popularity;
        /// <summary>Salary, yuan per year.</summary>
        public int Salary;
        /// <summary>Contract years left.</summary>
        public int ContractYearsLeft;
        /// <summary>Main-lane dimension display names, in lane order.</summary>
        public string[] DimensionNames;
        /// <summary>Main-lane dimension values, in lane order.</summary>
        public float[] DimensionValues;
        /// <summary>Lower end of the player-facing potential estimate.</summary>
        public int PotentialLow;
        /// <summary>Upper end of the player-facing potential estimate.</summary>
        public int PotentialHigh;
        /// <summary>True when he is a starter in his lane.</summary>
        public bool IsStarter;
    }

    /// <summary>Header numbers of the main screen.</summary>
    public sealed class TeamData
    {
        /// <summary>Team name.</summary>
        public string Name;
        /// <summary>Team strength (the triangle's first corner).</summary>
        public float Strength;
        /// <summary>Club popularity 0-100 (second corner).</summary>
        public float Popularity;
        /// <summary>Funds, yuan; may be negative (third corner).</summary>
        public long Funds;
        /// <summary>Team chemistry.</summary>
        public float Chemistry;
        /// <summary>Action points per segment.</summary>
        public int ActionPoints;
        /// <summary>Action points not yet used by the draft plan.</summary>
        public int PointsRemaining;
        /// <summary>Per-player action point cap.</summary>
        public int MaxPerPlayer;
        /// <summary>1-based number of the open segment.</summary>
        public int SegmentNumber;
        /// <summary>Segments per year (calendar placeholder until Story 005).</summary>
        public int SegmentsPerYear;
    }

    /// <summary>One resolved segment plus the team numbers before and after it (for the settlement panel).</summary>
    public sealed class SegmentResult
    {
        /// <summary>1-based number of the resolved segment.</summary>
        public int SegmentNumber;
        /// <summary>The runner's summary.</summary>
        public SegmentSummary Summary;
        /// <summary>Funds before / after, yuan.</summary>
        public long FundsBefore, FundsAfter;
        /// <summary>Club popularity before / after.</summary>
        public float PopularityBefore, PopularityAfter;
        /// <summary>Team chemistry before / after.</summary>
        public float ChemistryBefore, ChemistryAfter;
        /// <summary>Team strength before / after.</summary>
        public float StrengthBefore, StrengthAfter;
    }

    /// <summary>
    /// Plain-C# game state for the text UI (Story 009; no UnityEngine dependency): wraps generator, team and
    /// <see cref="SegmentRunner"/>, owns the draft <see cref="SegmentPlan"/> and exposes view data. It contains no
    /// formulas; it forwards to the runner. Deterministic for a fixed seed.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Default demo seed.</summary>
        public const int DefaultSeed = 20260101;
        /// <summary>Default starting funds, yuan.</summary>
        public const long DefaultFunds = 800000;
        /// <summary>Calendar placeholder until Story 005: segments per year.</summary>
        public const int SegmentsPerYear = 12;

        private readonly PlayerConfig _players;
        private readonly SegmentConfig _segment;
        private int _segmentsPlayed;
        private readonly HashSet<int> _declined = new HashSet<int>();

        /// <summary>The team.</summary>
        public Team Team { get; }
        /// <summary>The runner (state, validation, preview, resolve).</summary>
        public SegmentRunner Runner { get; }
        /// <summary>The draft plan the UI edits.</summary>
        public SegmentPlan Plan { get; private set; } = new SegmentPlan();
        /// <summary>The seed this session was built with.</summary>
        public int Seed { get; }
        /// <summary>Players of the generated pool who are NOT on the roster (free agents), for later stories.</summary>
        public IReadOnlyList<Player> FreeAgents { get; }

        /// <summary>Builds a session from the three config JSON texts.</summary>
        public static GameSession FromJson(string playerJson, string teamJson, string segmentJson,
            int seed = DefaultSeed, long startingFunds = DefaultFunds)
        {
            return new GameSession(PlayerConfigLoader.FromJson(playerJson), TeamConfigLoader.FromJson(teamJson),
                SegmentConfigLoader.FromJson(segmentJson), seed, startingFunds);
        }

        /// <summary>
        /// Generates the pool, takes the best main-lane player of every lane as starter plus the best remaining player
        /// as bench, and opens the first segment.
        /// </summary>
        public GameSession(PlayerConfig players, TeamConfig team, SegmentConfig segment, int seed = DefaultSeed,
            long startingFunds = DefaultFunds)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _segment = segment ?? throw new ArgumentNullException(nameof(segment));
            Seed = seed;
            var pool = new PlayerGenerator(players, new SeededRandomSource(seed)).GeneratePool();
            Team = new Team(team, players, new SeededRandomSource(seed + 1), startingFunds, "Demo");
            var used = new HashSet<long>();
            foreach (var lane in players.lanes)
            {
                Player best = null;
                foreach (var p in pool)
                    if (p.MainLane == lane.id && (best == null || p.GetLaneScore(lane.id) > best.GetLaneScore(lane.id))) best = p;
                if (best == null) continue;
                Team.AddPlayer(best);
                Team.SetStarter(lane.id, best);
                used.Add(best.Id);
            }
            Player bench = null;
            foreach (var p in pool)
                if (!used.Contains(p.Id) && (bench == null || p.GetLaneScore(p.MainLane) > bench.GetLaneScore(bench.MainLane))) bench = p;
            if (bench != null) { Team.AddPlayer(bench); used.Add(bench.Id); }
            var free = new List<Player>();
            foreach (var p in pool) if (!used.Contains(p.Id)) free.Add(p);
            FreeAgents = free;
            Runner = new SegmentRunner(segment, players, Team, new SeededRandomSource(seed + 2));
            Runner.BeginSegment();
        }

        /// <summary>Current runner snapshot (offers, contracts, last summary).</summary>
        public SegmentState State => Runner.GetState();

        /// <summary>The calendar/match inputs of the open segment (placeholders until Stories 004 and 005).</summary>
        public SegmentContext BuildContext() => new SegmentContext
        {
            SegmentsPerYear = SegmentsPerYear,
            SegmentIndexInYear = _segmentsPlayed % SegmentsPerYear,
            MatchResults = null,
            SeasonRankScore = 50f,
            PrizeMoney = 0,
        };

        /// <summary>
        /// Builds a session whose FIRST segment has at least one business offer, scanning seeds upwards from
        /// <paramref name="seed"/> (debug --force-offer / --demo offer). Returns the last tried session if none has one.
        /// </summary>
        public static GameSession FromJsonWithOffer(string playerJson, string teamJson, string segmentJson, int seed, int maxTries = 500)
        {
            GameSession s = null;
            for (int i = 0; i < maxTries; i++)
            {
                s = FromJson(playerJson, teamJson, segmentJson, seed + i * 7);
                if (s.Runner.GetState().PendingOffers.Count > 0) return s;
            }
            return s;
        }

        /// <summary>Validates the draft plan; false plus the first error.</summary>
        public bool TryValidate(out PlanError error) => Runner.TryValidate(Plan, BuildContext(), out error);

        /// <summary>Live preview of the draft plan.</summary>
        public SegmentPreview Preview() => Runner.Preview(Plan, BuildContext());

        /// <summary>True between <see cref="Resolve"/> and <see cref="BeginNext"/> (the settlement panel is up).</summary>
        public bool AwaitingNext { get; private set; }
        /// <summary>The last resolved segment, or null.</summary>
        public SegmentResult LastResult { get; private set; }

        /// <summary>Resolves the draft plan (validated by the runner; throws on an invalid plan and changes nothing).</summary>
        public SegmentResult Resolve()
        {
            var r = new SegmentResult
            {
                SegmentNumber = _segmentsPlayed + 1, FundsBefore = Team.Funds, PopularityBefore = Team.ClubPopularity,
                ChemistryBefore = Team.Chemistry(), StrengthBefore = Team.Strength(),
            };
            r.Summary = Runner.Resolve(Plan, BuildContext());
            r.FundsAfter = Team.Funds; r.PopularityAfter = Team.ClubPopularity;
            r.ChemistryAfter = Team.Chemistry(); r.StrengthAfter = Team.Strength();
            _segmentsPlayed++;
            Plan = new SegmentPlan();
            _declined.Clear();
            AwaitingNext = true;
            LastResult = r;
            return r;
        }

        /// <summary>Opens the next segment (rolls its business offers).</summary>
        public void BeginNext()
        {
            if (!AwaitingNext) throw new InvalidOperationException("Resolve the open segment first.");
            AwaitingNext = false;
            Runner.BeginSegment();
        }

        /// <summary>Resolve then BeginNext (scripted auto-play).</summary>
        public SegmentResult ResolveAndAdvance()
        {
            var r = Resolve();
            BeginNext();
            return r;
        }

        // ------------------------------------------------------------ plan editing (every change is validated)

        /// <summary>Adds one training point on a main-lane dimension; refused (false + error) when the plan would be invalid.</summary>
        public bool TryAddTrain(long playerId, int dimensionIndex, out PlanError error) => TryChange(p => p.Train(playerId, dimensionIndex), out error);
        /// <summary>Adds one stream point.</summary>
        public bool TryAddStream(long playerId, out PlanError error) => TryChange(p => p.Stream(playerId), out error);
        /// <summary>Adds one rest point.</summary>
        public bool TryAddRest(long playerId, out PlanError error) => TryChange(p => p.Rest(playerId), out error);
        /// <summary>Adds one sponsorship attempt.</summary>
        public bool TryAddSponsor(out PlanError error) => TryChange(p => p.Sponsor(), out error);
        /// <summary>Accepts a pending business offer (costs one point on that player).</summary>
        public bool TryAcceptOffer(int offerId, out PlanError error)
        {
            if (!TryChange(p => p.AcceptOffer(offerId), out error)) return false;
            _declined.Remove(offerId);
            return true;
        }
        /// <summary>Whether accepting the offer would be valid right now (nothing changes).</summary>
        public bool CanAcceptOffer(int offerId, out PlanError error)
        {
            if (IsAccepted(offerId)) { error = new PlanError { Code = PlanErrorCode.DuplicateOffer, OfferId = offerId }; return false; }
            var c = Copy(Plan, -1, -1); c.AcceptOffer(offerId);
            return Runner.TryValidate(c, BuildContext(), out error);
        }
        /// <summary>Declines (dismisses) a pending offer for this segment.</summary>
        public void DeclineOffer(int offerId) => _declined.Add(offerId);
        /// <summary>True when the draft plan accepts the offer.</summary>
        public bool IsAccepted(int offerId) { foreach (int id in Plan.AcceptedOfferIds) if (id == offerId) return true; return false; }
        /// <summary>Pending offers neither accepted nor declined yet (these trigger the pop-up).</summary>
        public List<BusinessOffer> UnansweredOffers()
        {
            var list = new List<BusinessOffer>();
            if (AwaitingNext) return list;
            foreach (var o in Runner.GetState().PendingOffers)
                if (!_declined.Contains(o.Id) && !IsAccepted(o.Id)) list.Add(o);
            return list;
        }

        /// <summary>Removes the player's last assignment, else his last accepted offer. False when he has neither.</summary>
        public bool UndoLast(long playerId)
        {
            for (int i = Plan.Assignments.Count - 1; i >= 0; i--)
                if (Plan.Assignments[i].PlayerId == playerId) { Plan = Copy(Plan, i, -1); return true; }
            var pending = Runner.GetState().PendingOffers;
            for (int i = Plan.AcceptedOfferIds.Count - 1; i >= 0; i--)
                foreach (var o in pending)
                    if (o.Id == Plan.AcceptedOfferIds[i] && o.PlayerId == playerId) { Plan = Copy(Plan, -1, i); return true; }
            return false;
        }

        /// <summary>Removes one sponsorship attempt. False when there is none.</summary>
        public bool UndoSponsor()
        {
            if (Plan.SponsorPoints <= 0) return false;
            Plan.SponsorPoints--;
            return true;
        }

        /// <summary>Empties the draft plan (declined offers stay declined).</summary>
        public void ClearPlan() => Plan = new SegmentPlan();

        /// <summary>
        /// Demo / auto-play plan: accept every acceptable offer, one sponsorship attempt, one stream for the most
        /// popular player, then round-robin: rest when stress >= 60, otherwise train the lowest main-lane dimension,
        /// until no point can be added. Every step goes through the validated Try* methods.
        /// </summary>
        public void ApplyScriptedPlan()
        {
            foreach (var o in UnansweredOffers()) TryAcceptOffer(o.Id, out _);
            TryAddSponsor(out _);
            Player star = null;
            foreach (var p in Team.Roster) if (star == null || p.Popularity > star.Popularity) star = p;
            if (star != null) TryAddStream(star.Id, out _);
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var p in Team.Roster)
                {
                    bool ok = p.Stress >= 60f ? TryAddRest(p.Id, out _) : TryAddTrain(p.Id, LowestDimension(p), out _);
                    added |= ok;
                }
            }
        }

        private static int LowestDimension(Player p)
        {
            var radar = p.GetRadar(p.MainLane);
            int best = 0;
            for (int i = 1; i < radar.Length; i++) if (radar[i] < radar[best]) best = i;
            return best;
        }

        private bool TryChange(Action<SegmentPlan> change, out PlanError error)
        {
            var c = Copy(Plan, -1, -1);
            change(c);
            if (!Runner.TryValidate(c, BuildContext(), out error)) return false;
            Plan = c;
            return true;
        }

        private static SegmentPlan Copy(SegmentPlan src, int skipAssignment, int skipOffer)
        {
            var c = new SegmentPlan();
            for (int i = 0; i < src.Assignments.Count; i++)
            {
                if (i == skipAssignment) continue;
                var a = src.Assignments[i];
                switch (a.Action)
                {
                    case SegmentAction.Train: c.Train(a.PlayerId, a.DimensionIndex); break;
                    case SegmentAction.Stream: c.Stream(a.PlayerId); break;
                    default: c.Rest(a.PlayerId); break;
                }
            }
            for (int i = 0; i < src.AcceptedOfferIds.Count; i++) if (i != skipOffer) c.AcceptOffer(src.AcceptedOfferIds[i]);
            c.SponsorPoints = src.SponsorPoints;
            return c;
        }

        /// <summary>Main-lane dimension names of a rostered player (empty when unknown).</summary>
        public string[] DimensionNames(long playerId)
        {
            foreach (var p in Team.Roster) if (p.Id == playerId) return ToCard(p).DimensionNames;
            return new string[0];
        }

        /// <summary>Header numbers.</summary>
        public TeamData GetTeamData()
        {
            var st = Runner.GetState();
            return new TeamData
            {
                Name = Team.Name,
                Strength = Team.Strength(),
                Popularity = Team.ClubPopularity,
                Funds = Team.Funds,
                Chemistry = Team.Chemistry(),
                ActionPoints = st.ActionPoints,
                PointsRemaining = Runner.PointsRemaining(Plan),
                MaxPerPlayer = st.MaxPerPlayer,
                SegmentNumber = AwaitingNext ? _segmentsPlayed : _segmentsPlayed + 1,
                SegmentsPerYear = SegmentsPerYear,
            };
        }

        /// <summary>One card per rostered player, roster order. Never contains the true potential.</summary>
        public List<PlayerCardData> GetRosterData()
        {
            var list = new List<PlayerCardData>();
            foreach (var p in Team.Roster) list.Add(ToCard(p));
            return list;
        }

        /// <summary>Card for one player.</summary>
        public PlayerCardData ToCard(Player p)
        {
            var lane = _players.FindLane(p.MainLane);
            var est = p.GetPotentialEstimate();
            var radar = p.GetRadar(p.MainLane);
            var names = new string[lane.dimensions.Length];
            var values = new float[lane.dimensions.Length];
            for (int i = 0; i < names.Length; i++) { names[i] = lane.dimensions[i].displayName; values[i] = radar[i]; }
            var traits = new List<string>();
            foreach (var t in p.Traits) traits.Add(TraitName(t));
            return new PlayerCardData
            {
                Id = p.Id, Name = p.Name, Age = p.Age, LaneId = p.MainLane, LaneName = lane.displayName,
                TraitNames = traits.ToArray(), Mood = p.Mood, Stress = p.Stress, Looks = p.Looks,
                Popularity = p.Popularity, Salary = p.Salary, ContractYearsLeft = p.ContractYearsLeft,
                DimensionNames = names, DimensionValues = values, PotentialLow = est.Low, PotentialHigh = est.High,
                IsStarter = Team.LaneOf(p) != null,
            };
        }

        /// <summary>Chinese display name of a trait id (falls back to the id).</summary>
        public string TraitName(string traitId)
        {
            foreach (var g in _players.personalityGroups)
                foreach (var t in g.traits) if (t.id == traitId) return t.displayName;
            return traitId;
        }

        /// <summary>Display name of a player by id, or an empty string.</summary>
        public string PlayerName(long id)
        {
            foreach (var p in Team.Roster) if (p.Id == id) return p.Name;
            return "";
        }
    }
}
