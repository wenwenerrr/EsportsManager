using UnityEngine;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>Small element factories and the colour palette (plain colour blocks + text, no art).</summary>
    public static class UiKit
    {
        public static readonly Color Background = new Color32(18, 22, 30, 255);
        public static readonly Color Panel = new Color32(32, 38, 52, 255);
        public static readonly Color PanelLight = new Color32(46, 54, 74, 255);
        public static readonly Color Text = new Color32(230, 234, 242, 255);
        public static readonly Color TextDim = new Color32(150, 160, 180, 255);
        public static readonly Color Accent = new Color32(90, 160, 240, 255);
        public static readonly Color Good = new Color32(90, 200, 130, 255);
        public static readonly Color Warn = new Color32(240, 190, 80, 255);
        public static readonly Color Bad = new Color32(235, 90, 90, 255);

        /// <summary>A single-line label.</summary>
        public static Label Lbl(string text, int size = 13, Color? color = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color ?? Text;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.overflow = Overflow.Hidden;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        /// <summary>A flex row.</summary>
        public static VisualElement Row(float height = 0)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            if (height > 0) r.style.height = height;
            return r;
        }

        /// <summary>A coloured block with rounded corners.</summary>
        public static VisualElement Block(Color bg, float radius = 4)
        {
            var b = new VisualElement();
            b.style.backgroundColor = bg;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
                b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = radius;
            return b;
        }

        /// <summary>Sets the same padding on all sides.</summary>
        public static void Pad(VisualElement e, float p)
        {
            e.style.paddingLeft = e.style.paddingRight = e.style.paddingTop = e.style.paddingBottom = p;
        }

        /// <summary>Sets the same margin on all sides.</summary>
        public static void Margin(VisualElement e, float m)
        {
            e.style.marginLeft = e.style.marginRight = e.style.marginTop = e.style.marginBottom = m;
        }

        /// <summary>Formats a yuan amount with thousands separators and a sign for negatives.</summary>
        public static string Yuan(long v) => (v < 0 ? "-" : "") + UiStrings.Yuan + System.Math.Abs(v).ToString("N0");

        /// <summary>A styled button (no theme needed). Disabled buttons are dimmed and ignore clicks.</summary>
        public static Button Btn(string text, System.Action onClick, Color bg, bool enabled = true, int size = 13)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = enabled ? bg : PanelLight;
            b.style.color = enabled ? Text : TextDim;
            b.style.fontSize = size;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 4;
            b.style.paddingLeft = b.style.paddingRight = 10;
            b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.height = 26; b.style.marginLeft = 0; b.style.marginRight = 6; b.style.marginTop = b.style.marginBottom = 0;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.whiteSpace = WhiteSpace.NoWrap;
            b.SetEnabled(enabled);
            return b;
        }

        /// <summary>Signed one-decimal delta, e.g. "+1.2" / "-0.5" / "0.0".</summary>
        public static string Delta(float v) => v.ToString("+0.0;-0.0;0.0");

        /// <summary>Signed yuan delta.</summary>
        public static string YuanDelta(long v) => (v > 0 ? "+" : "") + Yuan(v);

        /// <summary>Colour of a delta (positive good unless <paramref name="highBad"/>).</summary>
        public static Color DeltaColor(float v, bool highBad = false)
        {
            if (System.Math.Abs(v) < 0.05f) return TextDim;
            return (v > 0) != highBad ? Good : Bad;
        }

        /// <summary>A flexible spacer.</summary>
        public static VisualElement Spacer() { var s = new VisualElement(); s.style.flexGrow = 1; return s; }

        /// <summary>Colour for a 0-100 value where high is good.</summary>
        public static Color Scale(float v) => v >= 66f ? Good : v >= 40f ? Warn : Bad;
    }
}
