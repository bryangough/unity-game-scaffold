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
    /// <c>Assets/Game/Scenes/Main.unity</c> (light, camera, input adapter, the HUD canvas, the game's own objects,
    /// the fun-test kit, the <see cref="Shell"/> with the screens around the game) and puts it in Build Settings.
    /// Nothing in the scene is hand-placed: change this file, rebuild, commit both. Levels are data, never scenes.
    /// The game's part (its HUD and its manager) is one call, <see cref="SampleScene.Add"/>: the only line here that
    /// knows which game this is.
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

            // the HUD canvas (the reference resolution and match come from PrototypeUIHelper)
            var canvasGo = new GameObject("UI_Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            PrototypeUIHelper.SetupCanvas(canvas, canvasGo);

            // the game: its HUD on the canvas and its manager
            SampleScene.Add(cam, canvasGo);

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
