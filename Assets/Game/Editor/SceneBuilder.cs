#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <b>The source of truth for the scene.</b> <c>Game > Build Main Scene</c> regenerates
    /// <c>Assets/Game/Scenes/Main.unity</c> (light, camera, input adapter, the HUD canvas, the game manager, the
    /// fun-test kit, the <see cref="Shell"/> with the screens around the game) and puts it in Build Settings. Nothing in the scene is hand-placed: change this file, rebuild,
    /// commit both. Levels are data (<see cref="SampleLevels"/>), never scenes.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/Main.unity";

        [MenuItem(Menus.Root + "Build Main Scene", false, 1)]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[SceneBuilder] stop Play mode first"); return; }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // light
            var lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            RenderSettings.sun = light;
            // flat ambient so the shaded sides still read (colours are sRGB, lit in linear: keep this generous)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.66f);

            // camera (the manager frames it per level)
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, -7f), Quaternion.Euler(60f, 0f, 0f));
            camGo.AddComponent<AudioListener>();
            var camData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            camData.renderPostProcessing = false;
            camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

            // input
            new GameObject("TouchInputAdapter").AddComponent<TouchInputAdapter>();

            // HUD (portrait, 720 x 1280 matched on width)
            var canvasGo = new GameObject("UI_Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            PrototypeUIHelper.SetupCanvas(canvas, canvasGo);
            TMP_FontAsset font = UIText.Font;

            TextMeshProUGUI status = UIText.Make(canvasGo.transform, "Text_Status", 26f, Color.white, TextAlignmentOptions.TopLeft, font);
            status.text = "Level 1\nHits 0 / 10   Misses 0 / 2";
            UIText.Outline(status);
            RectTransform sr = status.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(18f, -18f);
            sr.sizeDelta = new Vector2(480f, 80f);

            PrototypeUIHelper.CreateBottomDescriptionPanel(canvasGo, "Tap the lit tile before it goes dark. Arrows move, Enter taps. Esc pauses, R restarts; keys 1-3 pick a level.");

            // the game manager
            var gmGo = new GameObject("SampleGame");
            SampleGame gm = gmGo.AddComponent<SampleGame>();
            var so = new SerializedObject(gm);
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            FunTestKitBuilder.AddKit();
            // the screens around the game: start, level select, result card, pause, settings. A UI skin, once the game
            // has one, is wired here: shell.skin = AssetDatabase.LoadAssetAtPath<UISkin>("Assets/Game/UI/UISkin.asset");
            new GameObject("Shell").AddComponent<Shell>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            FunTestKitBuilder.RegisterScene(ScenePath);
            Debug.Log($"[SceneBuilder] built {ScenePath} and put it in Build Settings");
        }

        public static Button MakeButton(Transform parent, string name, TMP_FontAsset font, string label, float size,
            Vector2 anchor, Vector2 position, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = sizeDelta;
            var img = go.GetComponent<Image>();
            img.color = color;
            var b = go.GetComponent<Button>();
            b.targetGraphic = img;
            TextMeshProUGUI t = UIText.Make(go.transform, "Label", size, Color.white, TextAlignmentOptions.Center, font);
            t.fontStyle = FontStyles.Bold;
            t.text = label;
            RectTransform tr = t.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            return b;
        }
    }
}
#endif
