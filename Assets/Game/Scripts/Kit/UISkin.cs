using UnityEngine;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// The look of the screens around the game: the font, the 9-sliced panel and button sprites, the star and the
    /// icons, and the text colours. Menus draw flat coloured boxes with no skin, and dress themselves through
    /// <see cref="Dress"/> / <see cref="DressPanel"/> when one is set. Make the asset with an editor script that reads
    /// your UI pack (the shipped games built theirs from a bought GUI pack with a <c>Build Skin</c> menu: a few sprites
    /// and one font, never the whole pack copied in). No pack ships with the scaffold.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/UI Skin", fileName = "UISkin")]
    public class UISkin : ScriptableObject
    {
        [Header("Font")]
        [Tooltip("The source font (kept for reference; the UI draws with tmpFont).")]
        public Font font;
        [Tooltip("The TextMesh Pro font every label uses.")]
        public TMPro.TMP_FontAsset tmpFont;
        [Tooltip("Scales every font size (a wide font wants a little less).")]
        public float fontScale = 1f;

        [Header("Sprites (9-sliced)")]
        [Tooltip("The card behind a screen's content, over the dim.")]
        public Sprite panel;
        public Color panelTint = Color.white;
        [Tooltip("A second 9-sliced layer over the card's edge (GUI Pro's black border); none = no border.")]
        public Sprite panelBorder;
        public Color panelBorderTint = Color.black;
        [Tooltip("A plain button (LEVELS, BACK, the settings rows).")]
        public Sprite button;
        public Color buttonTint = Color.white;
        [Tooltip("The main button of a screen (PLAY, NEXT, RESUME).")]
        public Sprite buttonPrimary;
        public Color buttonPrimaryTint = Color.white;
        [Tooltip("Reset progress.")]
        public Sprite buttonDanger;
        public Color buttonDangerTint = Color.white;
        [Tooltip("A 9-sliced inner layer laid inside every button (GUI Pro's inner border / light); none = flat buttons. Tinted per role below.")]
        public Sprite buttonTrim;
        public Color buttonTrimTint = Color.white;
        public Color buttonPrimaryTrimTint = Color.white;
        public Color buttonDangerTrimTint = Color.white;
        [Tooltip("How far the trim sits inside the button's edge, in canvas pixels.")]
        public float buttonTrimInset = 5f;
        [Tooltip("A level's tile on the level select; tinted by its state.")]
        public Sprite levelTile;
        public Color levelOpenTint = Color.white;
        public Color levelSolvedTint = new Color(0.7f, 1f, 0.75f);
        public Color levelCurrentTint = new Color(1f, 0.85f, 0.45f);
        public Color levelLockedTint = new Color(0.45f, 0.45f, 0.5f, 0.8f);
        [Tooltip("The level number on its tile.")]
        public Color levelText = Color.white;

        [Header("Icons")]
        [Tooltip("The game's title logo on the start screen; when null, the plain title text shows.")]
        public Sprite titleLogo;
        public Sprite star;
        public Color starOn = new Color(1f, 0.82f, 0.2f);
        public Color starOff = new Color(0.3f, 0.3f, 0.35f);
        public Sprite lockIcon;
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite pause;
        public Sprite gear;

        [Header("Text")]
        public Color title = new Color(0.95f, 0.85f, 0.45f);
        public Color text = Color.white;
        public Color soft = new Color(0.75f, 0.8f, 0.9f);
        [Tooltip("Text on a plain button.")]
        public Color buttonText = Color.white;
        [Tooltip("Text on the main button.")]
        public Color buttonPrimaryText = Color.white;
        [Tooltip("The dim over the game behind a screen.")]
        public Color dim = new Color(0.04f, 0.05f, 0.08f, 0.85f);

        /// <summary>Which sprite and tints a button gets.</summary>
        public enum Role { Plain, Primary, Danger }

        /// <summary>
        /// Dresses a button's face in the skin: the role's sprite and tint, and the trim layer inside it when the
        /// skin has one (made once; a second call retints). Returns the role's text colour.
        /// </summary>
        public Color Dress(Image face, Role role)
        {
            if (face == null || button == null) return buttonText;
            face.sprite = role == Role.Primary && buttonPrimary != null ? buttonPrimary : role == Role.Danger && buttonDanger != null ? buttonDanger : button;
            face.type = Image.Type.Sliced;
            face.color = role == Role.Primary ? buttonPrimaryTint : role == Role.Danger ? buttonDangerTint : buttonTint;
            if (buttonTrim != null)
            {
                Transform t = face.transform.Find("Trim");
                Image trim;
                if (t == null)
                {
                    var go = new GameObject("Trim", typeof(RectTransform), typeof(Image));
                    var r = (RectTransform)go.transform;
                    r.SetParent(face.transform, false);
                    r.SetAsFirstSibling(); // under the label
                    r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                    r.offsetMin = new Vector2(buttonTrimInset, buttonTrimInset);
                    r.offsetMax = new Vector2(-buttonTrimInset, -buttonTrimInset);
                    trim = go.GetComponent<Image>();
                    trim.raycastTarget = false;
                }
                else trim = t.GetComponent<Image>();
                trim.sprite = buttonTrim;
                trim.type = Image.Type.Sliced;
                trim.color = role == Role.Primary ? buttonPrimaryTrimTint : role == Role.Danger ? buttonDangerTrimTint : buttonTrimTint;
            }
            return role == Role.Primary ? buttonPrimaryText : buttonText;
        }

        /// <summary>Dresses a card: the panel sprite and tint, plus the border layer when the skin has one.</summary>
        public void DressPanel(Image card)
        {
            if (card == null || panel == null) return;
            card.sprite = panel;
            card.type = Image.Type.Sliced;
            card.color = panelTint;
            if (panelBorder == null || card.transform.Find("Border") != null) return;
            var go = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var r = (RectTransform)go.transform;
            r.SetParent(card.transform, false);
            r.SetAsFirstSibling();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(-2f, -2f); r.offsetMax = new Vector2(2f, 2f);
            var img = go.GetComponent<Image>();
            img.sprite = panelBorder;
            img.type = Image.Type.Sliced;
            img.color = panelBorderTint;
            img.raycastTarget = false;
        }
    }
}
