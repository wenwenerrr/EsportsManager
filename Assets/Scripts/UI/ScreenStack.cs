using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>Identity of a screen on the stack.</summary>
    public enum ScreenId { Main, Summary, Standings, OfferPopup, ChoicePopup, NewClub, EventPrep, RegularResult, Bracket }

    /// <summary>
    /// One entry of the <see cref="ScreenStack"/>: an id, whether it is an overlay (drawn on top of the screen below it)
    /// and the builder that fills the panel root.
    /// </summary>
    public sealed class UiScreen
    {
        /// <summary>Which screen.</summary>
        public ScreenId Id { get; }
        /// <summary>Overlays keep the screen below visible and add themselves on top.</summary>
        public bool IsOverlay { get; }
        private readonly Action<VisualElement> _build;

        /// <summary>Creates a screen.</summary>
        public UiScreen(ScreenId id, bool isOverlay, Action<VisualElement> build)
        {
            Id = id; IsOverlay = isOverlay; _build = build ?? throw new ArgumentNullException(nameof(build));
        }

        /// <summary>Builds the screen into the root.</summary>
        public void Build(VisualElement root) => _build(root);
    }

    /// <summary>
    /// TD-003: a small screen stack / router. The UI is rebuilt from scratch on every change: <see cref="Render"/> clears the
    /// root, builds the topmost full screen and then every overlay above it in order. The bottom screen is never popped.
    /// </summary>
    public sealed class ScreenStack
    {
        private readonly List<UiScreen> _stack = new List<UiScreen>();

        /// <summary>Number of screens.</summary>
        public int Count => _stack.Count;

        /// <summary>The top screen, or null when empty.</summary>
        public UiScreen Top => _stack.Count == 0 ? null : _stack[_stack.Count - 1];

        /// <summary>True when the top screen has this id.</summary>
        public bool IsTop(ScreenId id) => Top != null && Top.Id == id;

        /// <summary>True when any screen on the stack has this id.</summary>
        public bool Contains(ScreenId id)
        {
            foreach (var s in _stack) if (s.Id == id) return true;
            return false;
        }

        /// <summary>Pushes a screen.</summary>
        public void Push(UiScreen screen) => _stack.Add(screen ?? throw new ArgumentNullException(nameof(screen)));

        /// <summary>Pops the top screen; false (no change) when only the bottom screen is left.</summary>
        public bool Pop()
        {
            if (_stack.Count <= 1) return false;
            _stack.RemoveAt(_stack.Count - 1);
            return true;
        }

        /// <summary>Replaces the top screen (pushes when empty).</summary>
        public void Replace(UiScreen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (_stack.Count == 0) _stack.Add(screen); else _stack[_stack.Count - 1] = screen;
        }

        /// <summary>Clears the stack down to a single new bottom screen.</summary>
        public void Reset(UiScreen root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _stack.Clear();
            _stack.Add(root);
        }

        /// <summary>Index of the screen <see cref="Render"/> starts at: the topmost non-overlay (0 if all are overlays).</summary>
        public int BaseIndex()
        {
            for (int i = _stack.Count - 1; i >= 0; i--) if (!_stack[i].IsOverlay) return i;
            return 0;
        }

        /// <summary>Clears the root and builds the visible screens (base + overlays above it).</summary>
        public void Render(VisualElement root)
        {
            root.Clear();
            ResetRootLayout(root);
            for (int i = BaseIndex(); i < _stack.Count; i++) _stack[i].Build(root);
        }

        /// <summary>
        /// Clears the inline layout styles a screen may have put on the shared root, so one screen's layout never leaks
        /// into the next (the new-club screen centres its card; without this reset every later screen was shrink-wrapped
        /// and centred).
        /// </summary>
        public static void ResetRootLayout(VisualElement root)
        {
            root.style.alignItems = StyleKeyword.Null;
            root.style.justifyContent = StyleKeyword.Null;
            root.style.flexDirection = StyleKeyword.Null;
            root.style.flexWrap = StyleKeyword.Null;
        }
    }
}
