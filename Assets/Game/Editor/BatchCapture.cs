#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// A headless look at the game: <c>tools/unity_batch.py capture</c> opens the main scene, enters Play mode, writes
    /// <c>Builds/Checks/views/batch.png</c> (the canvas's reference size, HUD included) and quits. The cheapest way for an assistant to
    /// see the opening frame after a change, without an Editor window. <b>Only the opening frame:</b> with no Game
    /// view the player loop barely ticks (frame 4 after 120 editor ticks), so Update-driven state never advances; a
    /// game that has to run for a while needs the real Editor (<see cref="SmokeRun"/>). The log carries the camera,
    /// every renderer and the picture's mean brightness, so the result can be read without opening the picture.
    /// <para><c>capture &lt;screen&gt;</c> (<c>-shellScreen</c>) poses one of the <see cref="Shell"/>'s screens first
    /// (Title, Levels, Play, Result, Pause, Settings, Confirm) and writes <c>batch_&lt;screen&gt;.png</c>: showing a
    /// screen needs no frames, so every menu can be looked at headless. The save is not written.</para>
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
            string screen = Arg("-shellScreen");
            if (screen != null) Pose(screen);
            Captures.Shot("batch_warm");
            try { File.Delete("Builds/Checks/views/batch_warm.png"); } catch { }

            var sb = new System.Text.StringBuilder();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>())
                sb.AppendLine($"  {r.name}: {r.sharedMaterial?.shader?.name} {r.sharedMaterial?.color} bounds {r.bounds.center} {r.bounds.size}");
            Debug.Log($"[BatchCapture] camera {Camera.main?.transform.position} size {Camera.main?.orthographicSize}, {Object.FindObjectsByType<Light>().Length} light(s)\n{sb}");

            string shot = screen != null ? "batch_" + screen : "batch";
            bool ok = Captures.Shot(shot);
            string path = Path.GetFullPath($"Builds/Checks/views/{shot}.png");
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

        private static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>Puts one of the shell's screens up; a result is a sample win with two stars.</summary>
        private static void Pose(string screen)
        {
            if (Shell.Instance == null) { Debug.LogWarning("[BatchCapture] no Shell in the scene: capturing the opening frame"); return; }
            Progress.Suspended = true;
            if (screen == Shell.Result) Shell.ShowResult(true, Progress.Star1 | Progress.Star2, "A sample result line");
            else if (screen == Shell.Pause || screen == Shell.Settings || screen == Shell.Confirm) { Shell.Go(Shell.Play); Shell.Open(screen); }
            else Shell.Go(screen);
        }
    }
}
#endif
