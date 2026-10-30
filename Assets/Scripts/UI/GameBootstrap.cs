using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Scene entry point. Holds the four config TextAssets (assigned by the editor script, no Resources.Load),
    /// builds the <see cref="GameSession"/>, creates the runtime PanelSettings + UIDocument and shows the screen.
    /// Debug launch arguments are parsed by <see cref="DebugArgs"/>.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset playerConfig;
        [SerializeField] private TextAsset teamConfig;
        [SerializeField] private TextAsset segmentConfig;
        [SerializeField] private TextAsset matchConfig;
        [SerializeField] private TextAsset eventConfig;
        [SerializeField] private TextAsset eventText;
        [SerializeField] private TextAsset leagueConfig;

        private GameSession _session;
        private GameUi _ui;

        /// <summary>Assigns the config assets (used by the editor setup script).</summary>
        public void SetConfigs(TextAsset player, TextAsset team, TextAsset segment, TextAsset match, TextAsset events, TextAsset texts, TextAsset league)
        {
            playerConfig = player; teamConfig = team; segmentConfig = segment; matchConfig = match; eventConfig = events; eventText = texts;
            leagueConfig = league;
        }

        // Unattended screenshot runs launch the player without focus; macOS pauses an unfocused player unless this is set.
        // Only for debug runs: a normal game keeps the platform default (pauses in the background).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void KeepRunningUnfocused()
        {
            if (DebugArgs.IsDebugRun(System.Environment.GetCommandLineArgs())) Application.runInBackground = true;
        }

        private void Start()
        {
            var args = DebugArgs.Parse();
            // Normal play gets a fresh seed every launch (a fixed seed made every new game identical);
            // debug / screenshot runs stay on the fixed demo seed so captures are reproducible.
            bool debugRun = DebugArgs.IsDebugRun(System.Environment.GetCommandLineArgs());
            int seed = args.Seed ?? (debugRun ? GameSession.DefaultSeed : new System.Random().Next(1, int.MaxValue));
            string match = matchConfig != null ? matchConfig.text : null;
            if (match == null) Debug.LogWarning("GameBootstrap: no match_config assigned; re-run SceneSetup. Playing without matches.");
            string events = eventConfig != null ? eventConfig.text : null;
            if (events == null) Debug.LogWarning("GameBootstrap: no event_config assigned; re-run SceneSetup. Playing without random events.");
            string league = leagueConfig != null ? leagueConfig.text : null;
            if (match != null && league == null) { Debug.LogWarning("GameBootstrap: no league_config assigned; re-run SceneSetup. Playing without matches."); match = null; }
            // A real new game leaves the three job offers pending (offer screen); every demo takes the mid offer.
            bool pickOffer = !args.DemoGiven || args.Demo == DemoState.Name || args.Demo == DemoState.JobOffers;
            System.Func<int, GameSession> make = sd => GameSession.FromJson(playerConfig.text, teamConfig.text, segmentConfig.text, match, events, league, sd, GameSession.DefaultFunds, !pickOffer);
            // Story 006 demos: deterministic seed search (from --seed, else the documented demo seed, upwards).
            if (args.Demo == DemoState.Choice) seed = DemoSeeds.FindChoiceSeed(make, args.Seed ?? DemoSeeds.ChoiceDemoSeed);
            else if (args.Demo == DemoState.Events) seed = DemoSeeds.FindEventsSeed(make, args.Seed ?? DemoSeeds.EventsDemoSeed);
            else if (args.Demo == DemoState.Specialty) seed = DemoSeeds.FindSpecialtySeed(make, args.Seed ?? DemoSeeds.SpecialtyDemoSeed);
            _session = args.ForceOffer || args.Demo == DemoState.Offer
                ? GameSession.FromJsonWithOffer(playerConfig.text, teamConfig.text, segmentConfig.text, match, events, league, seed)
                : make(seed);
            if (eventText != null)
            {
                _session.EventTexts = EsportsManager.Events.EventTextTable.FromJson(eventText.text);
                if (_session.Events != null)
                {
                    var missing = _session.EventTexts.MissingKeys(_session.Events.Config);
                    if (missing.Count > 0) Debug.LogWarning("GameBootstrap: event texts missing: " + string.Join(", ", missing));
                }
            }
            // Debug only: scripted segments (their events auto-play). Event demos keep the last segment for the UI below.
            int segments = args.EffectiveSegments;
            int scripted = args.IsEventDemo ? System.Math.Max(0, segments - 1) : segments;
            for (int i = 0; i < scripted; i++)
            {
                _session.ApplyScriptedPlan();
                _session.ResolveAndAdvance();
            }
            Debug.Log("GameBootstrap: seed " + _session.Seed + ", auto-played " + scripted + " segment(s), demo " + args.Demo);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = args.Demo == DemoState.Card ? new Vector2Int(800, 450) : new Vector2Int(1280, 720);
            // Expand: scale = min(w/refW, h/refH), so the panel is never smaller than the reference size in either
            // dimension (observed: Shrink uses the larger ratio, i.e. a 3440x1440 window gave a 1280x536 panel and cropped
            // the height; MatchWidthOrHeight 0.5 shrank the width on 16:10 Retina). Wide windows get a wider panel; the content
            // content fills the safe area (SafeAreaFitter).
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            var go = new GameObject("UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            go.SetActive(true);

            var panelRoot = doc.rootVisualElement;
            UiFont.Apply(panelRoot);
            // Content lives in a safe-area container (notch, menu bar) and fills it on any screen shape.
            var root = SafeAreaFitter.Attach(go, panelRoot, args.SafeAreaTop).Content;

            _ui = new GameUi(root, _session);
            // Scripted demo steps run back to back; the double-click guard is for players only.
            double guard = _ui.FlowGuardSeconds;
            _ui.FlowGuardSeconds = 0;
            // Story 013: a real new game (no --demo), --demo name and --demo job-offers start at the three job offers (pick + name).
            if (pickOffer)
            {
                _ui.FlowGuardSeconds = guard;
                _ui.ShowJobOffers();
                if (args.Demo == DemoState.JobOffers) _ui.SelectOffer(1);
                return;
            }
            // Every other demo took the mid offer under the club's own name (GameSession autoTakeOffer).
            _ui.Refresh();
            // Pop-ups stay only for the demos that show them (the offer demo shows offers, the choice demo the choice).
            if (args.Demo == DemoState.Offer) _ui.LaterChoices();
            else if (args.Demo != DemoState.Choice) _ui.ClosePopups();
            switch (args.Demo)
            {
                case DemoState.Card: _ui.CardsOnly = true; _ui.MaxCards = 2; break;
                case DemoState.Main: break;
                case DemoState.Assign:
                    foreach (var o in _session.UnansweredOffers()) _session.DeclineOffer(o.Id);
                    _session.ApplyScriptedPlan();
                    // Show a refusal too: one more point than the team has.
                    if (!_session.TryAddRest(_session.Team.Roster[0].Id, out var err)) { _ui.Message = _ui.ErrorText(err); _ui.MessageIsError = true; }
                    break;
                case DemoState.Offer: _ui.OpenOffers(); break;
                case DemoState.Summary: _session.ApplyScriptedPlan(); _ui.Resolve(); break;
                case DemoState.Choice:
                    if (!_ui.ChoicePopupOpen) Debug.LogWarning("GameBootstrap: no choice event in this segment (seed " + _session.Seed + ").");
                    break;
                case DemoState.Events: _session.ApplyScriptedPlan(); _ui.Resolve(); break;
                case DemoState.Specialty: DemoSeeds.ApplySpecialtyPlan(_session); _ui.Resolve(); break;
                case DemoState.Training:
                {
                    foreach (var o in _session.UnansweredOffers()) _session.DeclineOffer(o.Id);
                    long id = -1; float bestScore = float.MaxValue; Session.SpecialtyProgressData sp = null;
                    foreach (var c in _session.GetRosterData())
                        foreach (var x in c.SpecialtyProgress)
                            if (!x.Owned && x.Phase >= 0 && c.PhaseDimensions[x.Phase].Length > 1 && x.DimensionGap + x.FocusGap * 100f < bestScore) { bestScore = x.DimensionGap + x.FocusGap * 100f; id = c.Id; sp = x; }
                    if (sp != null)
                    {
                        _ui.Train(id, (Growth.TrainingTarget)sp.Phase, sp.DimensionIndex);
                        _ui.OpenTraining(id);
                        _ui.SelectTrainingTarget((Growth.TrainingTarget)sp.Phase);
                        _ui.SelectTrainingFocus(sp.DimensionIndex);
                    }
                    break;
                }
                case DemoState.EventPrep:
                case DemoState.FinalsQual:
                case DemoState.Regular:
                case DemoState.Bracket:
                case DemoState.Report:
                    RunEventDemo(args.Demo);
                    break;
                case DemoState.Standings: _ui.OpenStandings(); break;
                case DemoState.News: _ui.OpenStandings(); break;
                case DemoState.Opponent: RunEventDemo(DemoState.EventPrep); break;
                case DemoState.XferReneg:
                case DemoState.XferRenew:
                case DemoState.XferPoach:
                case DemoState.XferOffer:
                case DemoState.XferChoice:
                case DemoState.XferAuction:
                case DemoState.XferResign: TransferDemo.Run(_ui, _session, args.Demo); break;
                case DemoState.Media: TransferDemo.Media(_ui, _session); break;
                case DemoState.FireSale: TransferDemo.FireSale(_ui, _session); break;
                case DemoState.Crisis: TransferDemo.Crisis(_ui, _session); break;
                case DemoState.Fired: TransferDemo.Fired(_ui, _session); break;
                case DemoState.Lineup:
                    foreach (var o in _session.UnansweredOffers()) _session.DeclineOffer(o.Id);
                    _ui.SetTactic(Matches.MatchTactic.Teamfight);
                    var starters = _session.Team.Starters();
                    if (starters.Count > 0) _ui.ToggleStarter(starters[0].Id);
                    break;
            }
            // --clicks: scripted train clicks, only while a segment is open for planning.
            if (_session.EventPhase == EventPhase.None && !_session.AwaitingNext)
            {
                var roster = _session.GetRosterData();
                for (int i = 0; i < args.Clicks && roster.Count > 0; i++) _ui.Train(roster[i % roster.Count].Id, Growth.TrainingTarget.Early, -1);
            }
            _ui.FlowGuardSeconds = guard;
            _ui.Refresh();
        }

        // Resolves the open segment through the UI (the summary), then walks the event screens exactly as a player would.
        private void RunEventDemo(DemoState demo)
        {
            _session.ApplyScriptedPlan();
            _ui.Resolve();
            if (_session.EventPhase == EventPhase.None) { Debug.LogWarning("GameBootstrap: this segment hosts no event; showing the summary."); return; }
            _ui.NextSegment(); // -> prep
            if (demo == DemoState.EventPrep || demo == DemoState.FinalsQual) return;
            _ui.StartEventStage(); // regular season (split) or finals -> result screen
            if (demo == DemoState.Regular || _session.EventPhase != EventPhase.PlayoffPrep) { _ui.ScrollToReports = demo == DemoState.Report; return; }
            _ui.GoToPlayoffs(); // playoff prep when seeded, else the AI playoffs are played
            if (_session.EventPhase == EventPhase.PlayoffPrep) _ui.StartEventStage();
            _ui.ScrollToReports = demo == DemoState.Report;
        }
    }
}
