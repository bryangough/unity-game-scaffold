using UnityEngine;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// The canvas setup every screen shares (portrait, 720 x 1280 matched on width so tall phones get extra height) and
    /// the bottom hint panel of the fun tests. Same name as the test bed's helper, so a graduated prototype's scene
    /// builder compiles as it is.
    /// </summary>
    public static class PrototypeUIHelper
    {
        /// <summary>Portrait by default. For a landscape game set 1280 x 720 and match on height (1f).</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(720f, 1280f);
        public static float MatchWidthOrHeight = 0f; // width: the sides never clip

        public static void SetupScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
        }

        public static void SetupCanvas(Canvas canvas, GameObject canvasGo)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>() ?? canvasGo.AddComponent<CanvasScaler>();
            SetupScaler(scaler);
            if (canvasGo.GetComponent<GraphicRaycaster>() == null) canvasGo.AddComponent<GraphicRaycaster>();
        }

        /// <summary>The "three things to know" strip along the bottom of a fun-test scene.</summary>
        public static GameObject CreateBottomDescriptionPanel(GameObject canvasGo, string instructionText, float height = 110f, float bottomOffset = 16f)
        {
            height = Mathf.Max(height, 110f); // two readable lines
            bottomOffset = Mathf.Min(bottomOffset, 16f);
            var panelGo = new GameObject("Panel_BottomHint");
            panelGo.transform.SetParent(canvasGo.transform, false);

            Image panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.07f, 0.12f, 0.88f);

            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.03f, 0f);
            panelRect.anchorMax = new Vector2(0.97f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.offsetMin = new Vector2(0f, bottomOffset);
            panelRect.offsetMax = new Vector2(0f, bottomOffset + height);

            Outline outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.5f, 0.9f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            TMPro.TextMeshProUGUI text = UIText.Make(panelGo.transform, "Text_Hint", 24f, new Color(0.95f, 0.98f, 1f, 0.95f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 23f;
            text.fontSizeMax = 24f;
            text.text = instructionText;

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.02f, 0.05f);
            textRect.anchorMax = new Vector2(0.98f, 0.95f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return panelGo;
        }
    }
}
