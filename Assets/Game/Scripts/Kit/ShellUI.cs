using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// The pieces the <see cref="Shell"/>'s screens are made of, usable by a game's own screens too (a collection, a
    /// how-to card): a dim, a card, a label and a button with a shadow it sinks onto and a focus ring for key and pad
    /// players. Flat coloured boxes until a <see cref="UISkin"/> is passed; sizes are canvas pixels on the 720-wide
    /// reference and keep the readability floors (body 28, captions 23, button labels 28 and up, buttons 88 tall).
    /// </summary>
    public static class ShellUI
    {
        public static readonly Color Plain = new Color(0.17f, 0.22f, 0.34f);
        public static readonly Color Primary = new Color(0.2f, 0.6f, 0.3f);
        public static readonly Color Danger = new Color(0.7f, 0.25f, 0.22f);
        public static readonly Color CardColor = new Color(0.09f, 0.11f, 0.17f, 0.97f);
        public static readonly Color DimColor = new Color(0.04f, 0.05f, 0.08f, 0.85f);
        public static readonly Color TitleColor = new Color(0.95f, 0.85f, 0.45f);
        public static readonly Color Soft = new Color(0.75f, 0.8f, 0.9f);
        public static readonly Color FocusColor = new Color(1f, 0.85f, 0.3f);

        public const float BodySize = 28f, CaptionSize = 23f, ButtonLabelSize = 34f, ButtonLabelMin = 28f;
        private const float Drop = 5f;
        public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        /// <summary>An empty rect that fills its parent.</summary>
        public static RectTransform Fill(Transform parent, string name)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        /// <summary>A rect of a fixed size, placed from an anchor point of its parent (its own pivot is its centre).</summary>
        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = Centre;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        /// <summary>The dim over the game behind a screen: it takes the taps, and reaches past the safe area to the screen's edges.</summary>
        public static Image Dim(Transform parent, UISkin skin, float alpha = -1f)
        {
            RectTransform r = Fill(parent, "Dim");
            var img = r.gameObject.AddComponent<Image>();
            Color c = skin != null ? skin.dim : DimColor;
            if (alpha >= 0f) c.a = alpha;
            img.color = c;
            r.gameObject.AddComponent<BleedToScreen>();
            return img;
        }

        /// <summary>A centred card for a screen's content.</summary>
        public static Image Card(Transform parent, UISkin skin, Vector2 size)
        {
            RectTransform r = Box(parent, "Card", Centre, Vector2.zero, size);
            var img = r.gameObject.AddComponent<Image>();
            img.color = CardColor;
            if (skin != null) skin.DressPanel(img);
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            Vector2 anchor, Vector2 position, Vector2 box, UISkin skin = null)
        {
            TextMeshProUGUI t = UIText.Make(parent, name, size * (skin != null ? skin.fontScale : 1f), color);
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = Centre;
            r.anchoredPosition = position;
            r.sizeDelta = box;
            t.text = text;
            return t;
        }

        /// <summary>
        /// A button: a face over a shadow it sinks onto while held, a label that shrinks to fit (never under the
        /// floor), a click sound, and a ring shown while it has the focus of a key or pad player.
        /// </summary>
        public static Button MakeButton(Transform parent, string name, string label, UISkin.Role role, Vector2 anchor,
            Vector2 position, Vector2 size, Action onClick, UISkin skin = null, float labelSize = ButtonLabelSize)
        {
            RectTransform root = Box(parent, name, anchor, position, size);

            RectTransform ring = Fill(root, "Focus");
            ring.offsetMin = new Vector2(-6f, -6f); ring.offsetMax = new Vector2(6f, 6f);
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.color = FocusColor;
            ringImg.raycastTarget = false;
            ring.gameObject.SetActive(false);

            RectTransform shadow = Fill(root, "Shadow");
            shadow.offsetMin = shadow.offsetMax = new Vector2(Drop, -Drop);
            var shadowImg = shadow.gameObject.AddComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImg.raycastTarget = false;

            RectTransform faceRect = Fill(root, "Face");
            var face = faceRect.gameObject.AddComponent<Image>();
            face.color = role == UISkin.Role.Primary ? Primary : role == UISkin.Role.Danger ? Danger : Plain;
            Color textColor = Color.white;
            if (skin != null && skin.button != null) textColor = skin.Dress(face, role);

            float scale = skin != null ? skin.fontScale : 1f;
            TextMeshProUGUI t = UIText.Make(faceRect, "Label", labelSize * scale, textColor);
            t.fontStyle = FontStyles.Bold;
            t.text = label;
            t.enableAutoSizing = true;
            t.fontSizeMax = labelSize * scale;
            t.fontSizeMin = Mathf.Min(ButtonLabelMin, labelSize) * scale;
            RectTransform tr = t.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10f, 4f); tr.offsetMax = new Vector2(-10f, -4f);

            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            ColorBlock colors = b.colors;
            colors.highlightedColor = new Color(0.9f, 0.95f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.selectedColor = Color.white; // the ring shows the focus; a tint would stick after a mouse click
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = colors;
            b.onClick.AddListener(() => { Sfx.Play(Sfx.Id.Click); onClick?.Invoke(); });

            root.gameObject.AddComponent<ButtonPress>().Init(faceRect, Drop);
            root.gameObject.AddComponent<FocusRing>().ring = ring.gameObject;
            return b;
        }

        public static Image Face(Button b) => b.transform.Find("Face").GetComponent<Image>();

        public static TextMeshProUGUI LabelOf(Button b) => b.transform.Find("Face/Label").GetComponent<TextMeshProUGUI>();

        public static void SetLabel(Button b, string text) => LabelOf(b).text = text;
    }

    /// <summary>The ring round a button while it is selected, shown only to key and pad players (<see cref="InputMode"/>).</summary>
    public class FocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public GameObject ring;

        public void OnSelect(BaseEventData e) { if (ring != null) ring.SetActive(InputMode.UsingKeysOrPad); }
        public void OnDeselect(BaseEventData e) { if (ring != null) ring.SetActive(false); }
        private void OnDisable() { if (ring != null) ring.SetActive(false); }
    }
}
