using TMPro;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// Every piece of UI text is TextMesh Pro, made through here so anything can make a label without wiring: the HUD,
    /// the kit's callout, the popups, the menus. One font for the game: TMP's default until <see cref="Use"/> is given
    /// a <see cref="UISkin"/>'s font. (TMP's essential resources must be imported once: the Setup menu does it.)
    /// </summary>
    public static class UIText
    {
        private static TMP_FontAsset font, chosen;

        public static TMP_FontAsset Font
        {
            get
            {
                if (chosen != null) return chosen;
                if (font == null)
                {
                    if (TMP_Settings.instance == null)
                    {
                        // no TMP Settings asset: the essentials are not imported (Game > Setup Project does it)
                        Debug.LogError("[UIText] TextMesh Pro's essential resources are missing: run the Setup menu (or Window > TextMeshPro > Import TMP Essential Resources). Using a stand-in font.");
                        font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                    }
                    else font = TMP_Settings.defaultFontAsset;
                }
                return font;
            }
        }

        /// <summary>The skin's font takes over from the default for everything made after this.</summary>
        public static void Use(TMP_FontAsset skinFont) { chosen = skinFont; }

        /// <summary>A label: wrapped, rich text on, no raycasts, centred unless told otherwise.</summary>
        public static TextMeshProUGUI Make(Transform parent, string name, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, TMP_FontAsset withFont = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = withFont != null ? withFont : Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A thin dark outline round the letters, for text over the game (the callout, popups, the HUD status).</summary>
        public static void Outline(TMP_Text t, float width = 0.1f)
        {
            t.outlineWidth = width;
            t.outlineColor = new Color32(0, 0, 0, 220);
        }

        /// <summary>
        /// A star rating as rich text, earned ones filled in <paramref name="earned"/> colour and the rest hollow and dim:
        /// "● ● ○". Circles, not ★ ☆: TMP's default font (LiberationSans) has no star, and a missing glyph draws as an
        /// empty box (TMP logs "The character with Unicode value … was not found"). A game with its own font may set
        /// <see cref="StarOn"/> / <see cref="StarOff"/> to ★ ☆ once that font has them.
        /// </summary>
        /// <param name="size">Relative to the line's text: the rating is the card's reward, so it is drawn larger.</param>
        public static string Stars(int count, int max, Color earned, float size = 1.8f)
        {
            var sb = new System.Text.StringBuilder($"<size={Mathf.RoundToInt(size * 100)}%>");
            string on = ColorUtility.ToHtmlStringRGB(earned);
            for (int i = 0; i < max; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i < count ? $"<color=#{on}>{StarOn}</color>" : $"<color=#FFFFFF66>{StarOff}</color>");
            }
            return sb.Append("</size>").ToString();
        }

        public static string StarOn = "●", StarOff = "○"; // ● ○, both in LiberationSans

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStars() { StarOn = "●"; StarOff = "○"; }
    }
}
