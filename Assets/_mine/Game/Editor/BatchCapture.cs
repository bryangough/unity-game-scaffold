#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// A headless look at the game: <c>tools/unity_batch.py capture</c> opens the main scene, enters Play mode, writes
    /// <c>Builds/Checks/views/batch.png</c> (720 x 1280, HUD included) and quits. The cheapest way for an assistant to
    /// see the opening frame after a change, without an Editor window. <b>Only the opening frame:</b> with no Game
    /// view the player loop barely ticks (frame 4 after 120 editor ticks), so Update-driven state never advances; a
    /// game that has to run for a while needs the real Editor (<see cref="SmokeRun"/>). The log carries the camera,
    /// every renderer and the picture's mean brightness, so the result can be read without opening the picture.
    /// </summary>
    public static class BatchCapture
    {
        private static int frames;
        private static bool pending;

        public static void Batch()
        {
            // a batch Editor opens with an empty scene: load the game's first
            string scene = EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : SceneBuilder.ScenePath;
            if (!File.Exists(scene)) { Debug.LogError($"[BatchCapture] no scene at {scene}: run Setup first"); EditorApplication.Exit(1); return; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
            ShaderUtil.allowAsyncCompilation = false; // or the first frames draw the Editor's placeholder shader
            pending = true;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !pending) return;
            pending = false;
            frames = 0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (++frames < 120) return; // the scene is up and the first callout shows
            EditorApplication.update -= Tick;
            Debug.Log($"[BatchCapture] after {frames} editor ticks the game is at frame {Time.frameCount}, t = {Time.time:0.00} s");

            // Objects made at run time in these first frames are not yet lit by the sun in a headless render (they
            // drew floor-grey until the light was touched); re-enabling the lights brings their lighting up to date.
            foreach (Light l in Object.FindObjectsByType<Light>()) { l.enabled = false; l.enabled = true; }
            // one render to a texture carries the change; the real shot comes after it (a bare Camera.Render() with no
            // Game view crashes a batch Editor)
            Captures.Shot("batch_warm");
            try { File.Delete("Builds/Checks/views/batch_warm.png"); } catch { }

            var sb = new System.Text.StringBuilder();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>())
                sb.AppendLine($"  {r.name}: {r.sharedMaterial?.shader?.name} {r.sharedMaterial?.color} bounds {r.bounds.center} {r.bounds.size}");
            Debug.Log($"[BatchCapture] camera {Camera.main?.transform.position} size {Camera.main?.orthographicSize}, {Object.FindObjectsByType<Light>().Length} light(s)\n{sb}");

            bool ok = Captures.Shot("batch");
            string path = Path.GetFullPath("Builds/Checks/views/batch.png");
            if (ok)
            {
                // a number to read without the picture: the mean brightness (0..1); under 0.05 means an unlit or empty view
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(path));
                Color32[] px = tex.GetPixels32();
                double sum = 0;
                foreach (Color32 c in px) sum += (c.r + c.g + c.b) / (3.0 * 255.0);
                Debug.Log($"[BatchCapture] wrote {path} ({tex.width}x{tex.height}, mean brightness {sum / px.Length:0.000})");
                Object.DestroyImmediate(tex);
            }
            else Debug.LogError("[BatchCapture] no main camera in the scene: nothing captured");
            EditorApplication.ExitPlaymode();
            EditorApplication.Exit(ok && File.Exists(path) ? 0 : 1);
        }
    }
}
#endif
