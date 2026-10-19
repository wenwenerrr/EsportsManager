using System.IO;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace EsportsManager.UI
{
    /// <summary>
    /// Chinese font setup. The default UI Toolkit font has no CJK glyphs. Font.CreateDynamicFontFromOSFont does NOT work
    /// with UI Toolkit text (it logs "Unable to load font face"), so a font FILE of the OS is opened by path and wrapped in
    /// a dynamic SDF FontAsset (glyphs are added to the atlas on demand).
    /// NOT SOURCEABLE: this approach is not covered by docs/engine-reference/unity/ or the Unity 6.6 docs we checked;
    /// it is verified only by running the build (Story 009 screenshots). Before shipping, bundle an open CJK font
    /// (e.g. Source Han Sans) in the project instead of relying on OS font files (tech debt TD-002).
    /// </summary>
    public static class UiFont
    {
        /// <summary>CJK font files shipped with the OS, in preference order (macOS, Windows, Linux).</summary>
        public static readonly string[] Candidates =
        {
            "/System/Library/Fonts/Hiragino Sans GB.ttc",
            "/System/Library/Fonts/STHeiti Medium.ttc",
            "/System/Library/Fonts/STHeiti Light.ttc",
            "/System/Library/Fonts/Supplemental/Songti.ttc",
            "/System/Library/Fonts/Songti.ttc",
            "C:/Windows/Fonts/msyh.ttc",
            "C:/Windows/Fonts/simhei.ttf",
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
        };

        /// <summary>The font asset in use, or null when none could be created.</summary>
        public static FontAsset Current { get; private set; }

        /// <summary>Applies the Chinese-capable font to <paramref name="root"/> (children inherit it).</summary>
        public static void Apply(VisualElement root)
        {
            foreach (var path in Candidates)
            {
                if (!File.Exists(path)) continue;
                var font = new Font(path);
                var asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (asset == null) { Debug.LogWarning("UiFont: could not create a FontAsset from " + path); continue; }
                asset.name = Path.GetFileNameWithoutExtension(path);
                Current = asset;
                Debug.Log("UiFont: using " + path + " hasChinese=" + asset.HasCharacter('中', false, true));
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(asset));
                return;
            }
            Debug.LogError("UiFont: no CJK font file found; Chinese text will render as boxes.");
        }
    }
}
