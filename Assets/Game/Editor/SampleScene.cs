#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The sample game's part of the scene, called by <see cref="SceneBuilder"/>: its HUD on the shared canvas and its
    /// manager, wired. The pattern for a game's own (a status line, the manager with its references set through a
    /// <see cref="SerializedObject"/>), and deleted with the sample.
    /// </summary>
    public static class SampleScene
    {
        public static void Add(Camera cam, GameObject canvasGo)
        {
            TextMeshProUGUI status = UIText.Make(canvasGo.transform, "Text_Status", 26f, Color.white, TextAlignmentOptions.TopLeft);
            status.text = "Level 1\nHits 0 / 10   Misses 0 / 2";
            UIText.Outline(status);
            RectTransform sr = status.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(18f, -18f);
            sr.sizeDelta = new Vector2(480f, 80f);

            PrototypeUIHelper.CreateBottomDescriptionPanel(canvasGo, "Tap the lit tile before it goes dark. Arrows move, Enter taps. Esc pauses, R restarts; keys 1-3 pick a level.");

            SampleGame gm = new GameObject("SampleGame").AddComponent<SampleGame>();
            var so = new SerializedObject(gm);
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
