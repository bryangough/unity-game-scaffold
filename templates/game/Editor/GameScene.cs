#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The game's part of the scene, called by <see cref="SceneBuilder"/>: its HUD on the shared canvas and its
    /// manager, wired. Everything the game puts in the scene is added here, never by hand: change this, run
    /// Build Main Scene, commit both.
    /// </summary>
    public static class GameScene
    {
        public static void Add(Camera cam, GameObject canvasGo)
        {
            TextMeshProUGUI status = UIText.Make(canvasGo.transform, "Text_Status", 26f, Color.white, TextAlignmentOptions.TopLeft);
            status.text = "Level 1\nTaps 0 / 5";
            UIText.Outline(status);
            RectTransform sr = status.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(18f, -18f);
            sr.sizeDelta = new Vector2(480f, 80f);

            PrototypeUIHelper.CreateBottomDescriptionPanel(canvasGo, "Tap anywhere (or press Enter) until the block is gold. Esc pauses, R restarts; keys 1-3 pick a level.");

            GameManager gm = new GameObject("GameManager").AddComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
