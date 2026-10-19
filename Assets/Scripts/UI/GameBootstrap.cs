using EsportsManager.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Scene entry point. Holds the three config TextAssets (assigned by the editor script, no Resources.Load),
    /// builds the <see cref="GameSession"/>, creates the runtime PanelSettings + UIDocument and shows the screen.
    /// Debug launch arguments are parsed by <see cref="DebugArgs"/>.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset playerConfig;
        [SerializeField] private TextAsset teamConfig;
        [SerializeField] private TextAsset segmentConfig;

        private GameSession _session;
        private GameUi _ui;

        /// <summary>Assigns the config assets (used by the editor setup script).</summary>
        public void SetConfigs(TextAsset player, TextAsset team, TextAsset segment)
        {
            playerConfig = player; teamConfig = team; segmentConfig = segment;
        }

        // Unattended screenshot runs launch the player without focus; macOS pauses an unfocused player unless this is set.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void KeepRunningUnfocused() => Application.runInBackground = true;

        private void Start()
        {
            var args = DebugArgs.Parse();
            int seed = args.Seed ?? GameSession.DefaultSeed;
            _session = args.ForceOffer || args.Demo == DemoState.Offer
                ? GameSession.FromJsonWithOffer(playerConfig.text, teamConfig.text, segmentConfig.text, seed)
                : GameSession.FromJson(playerConfig.text, teamConfig.text, segmentConfig.text, seed);
            for (int i = 0; i < args.Segments; i++)
            {
                _session.ApplyScriptedPlan();
                _session.ResolveAndAdvance();
            }
            Debug.Log("GameBootstrap: seed " + _session.Seed + ", auto-played " + args.Segments + " segment(s), demo " + args.Demo);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = args.Demo == DemoState.Card ? new Vector2Int(800, 450) : new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            var go = new GameObject("UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            go.SetActive(true);

            var root = doc.rootVisualElement;
            root.style.flexGrow = 1;
            UiFont.Apply(root);

            _ui = new GameUi(root, _session);
            switch (args.Demo)
            {
                case DemoState.Card: _ui.CardsOnly = true; _ui.MaxCards = 2; _ui.OfferPopupOpen = false; break;
                case DemoState.Main: _ui.OfferPopupOpen = false; break;
                case DemoState.Assign:
                    _ui.OfferPopupOpen = false;
                    foreach (var o in _session.UnansweredOffers()) _session.DeclineOffer(o.Id);
                    _session.ApplyScriptedPlan();
                    // Show a refusal too: one more point than the team has.
                    if (!_session.TryAddRest(_session.Team.Roster[0].Id, out var err)) { _ui.Message = _ui.ErrorText(err); _ui.MessageIsError = true; }
                    break;
                case DemoState.Offer: _ui.OfferPopupOpen = true; break;
                case DemoState.Summary: _session.ApplyScriptedPlan(); _session.Resolve(); break;
            }
            _ui.Refresh();
        }
    }
}
