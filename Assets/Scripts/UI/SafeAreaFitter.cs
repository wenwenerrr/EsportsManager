using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Keeps the UI content inside <see cref="Screen.safeArea"/> (macOS notch / menu bar, phone cutouts); the content always
    /// fills the whole safe area, whatever the screen shape (user decision 2026-10-19: no width cap, no side bars). Insets are converted from screen pixels to panel units by the ratio
    /// panel size / screen size, which holds for every PanelScaleMode. Re-applied only when the safe area, resolution or the
    /// panel size changes.
    /// API sourcing: Screen.safeArea is NOT SOURCEABLE in docs/engine-reference/unity/ (no mention); it is a long-standing
    /// UnityEngine.Screen property (Rect, bottom-left origin, screen pixels) used here from general knowledge.
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private VisualElement _outer;
        private VisualElement _content;
        private float _debugTopPx;
        private Rect _lastSafe;
        private Vector2 _lastScreen, _lastPanel;

        /// <summary>The content container the screens are built into.</summary>
        public VisualElement Content => _content;

        /// <summary>Creates the outer/inner containers under the panel root and starts tracking.</summary>
        public static SafeAreaFitter Attach(GameObject host, VisualElement panelRoot, float debugTopPx)
        {
            var f = host.AddComponent<SafeAreaFitter>();
            f._debugTopPx = debugTopPx;
            panelRoot.style.flexGrow = 1;
            panelRoot.style.backgroundColor = UiKit.Background;
            f._outer = new VisualElement { name = "safe-outer" };
            f._outer.style.flexGrow = 1;
            f._outer.style.flexDirection = FlexDirection.Column;
            f._outer.style.alignItems = Align.Stretch;
            f._content = new VisualElement { name = "safe-content" };
            // Column + stretch + grow: the content always fills the safe area on every rebuild and resize
            // (a percent width inside a centred row collapsed to the intrinsic width after a fullscreen resize).
            f._content.style.flexGrow = 1;
            f._content.style.flexShrink = 1;
            f._content.style.minHeight = 0;
            f._content.style.minWidth = 0;
            f._outer.Add(f._content);
            panelRoot.Add(f._outer);
            f._outer.RegisterCallback<GeometryChangedEvent>(_ => f.Apply());
            return f;
        }

        private void Update() => Apply();

        /// <summary>Computes the panel-unit insets for a safe rect (pure; unit-testable).</summary>
        public static (float left, float top, float right, float bottom) Insets(Rect safe, Vector2 screen, Vector2 panel)
        {
            if (screen.x <= 0 || screen.y <= 0) return (0, 0, 0, 0);
            float sx = panel.x / screen.x, sy = panel.y / screen.y;
            return (Mathf.Max(0, safe.xMin) * sx, Mathf.Max(0, screen.y - safe.yMax) * sy,
                    Mathf.Max(0, screen.x - safe.xMax) * sx, Mathf.Max(0, safe.yMin) * sy);
        }

        private void Apply()
        {
            if (_outer == null) return;
            var screen = new Vector2(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (_debugTopPx > 0) safe = new Rect(safe.xMin, safe.yMin, safe.width, Mathf.Max(0, safe.height - _debugTopPx));
            var panel = _outer.panel != null ? _outer.panel.visualTree.layout.size : Vector2.zero;
            if (panel.x <= 0 || panel.y <= 0) return;
            if (safe == _lastSafe && screen == _lastScreen && panel == _lastPanel) return;
            _lastSafe = safe; _lastScreen = screen; _lastPanel = panel;
            Debug.Log("SafeAreaFitter: screen " + screen + " safe " + safe + " panel " + panel + " content " + _content.layout.size);
            var (l, t, r, b) = Insets(safe, screen, panel);
            _outer.style.paddingLeft = l; _outer.style.paddingTop = t;
            _outer.style.paddingRight = r; _outer.style.paddingBottom = b;
        }
    }
}
