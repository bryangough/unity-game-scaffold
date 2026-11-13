#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Portrait captures of the live game with its overlay canvases (the HUD and the menus) for the checks in
    /// <c>Builds/Checks/views/</c>, so an assistant (or a commit) can look at what the player sees. Overlay canvases are
    /// drawn by the screen, not a camera, so each is switched to the main camera for one render and back. Edit or Play
    /// mode; the <c>Checks > Capture …</c> items shoot the phone, tall-phone and tablet shapes. <see cref="Sequence"/>
    /// runs steps a few frames apart in Play mode (load a level, open a menu, shoot); the store screenshots and a
    /// smoke run are built from these two calls.
    /// </summary>
    public static class Captures
    {
        public const string ViewsDir = "Builds/Checks/views";

        /// <summary>Writes <c>&lt;dir&gt;/&lt;name&gt;.png</c> (<see cref="ViewsDir"/> by default); false when there is no main camera.</summary>
        public static bool Shot(string name, int w = 720, int h = 1280, string dir = ViewsDir)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            var rt = new RenderTexture(w, h, 24);
            RenderTexture prev = cam.targetTexture;
            var overlays = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>())
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.enabled)
                {
                    overlays.Add(c);
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f; // not the near plane: at 0.15 m TMP's SDF scale overflowed half precision on Android and text drew as blocks
                    Rematch(c, w, h);
                }
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            foreach (Canvas c in overlays) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; Rematch(c, Screen.width, Screen.height); }
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Debug.Log($"[Captures] {dir}/{name}.png ({w}x{h})");
            return true;
        }

        /// <summary>The same width / height match the game uses at this aspect (<see cref="AspectMatch"/>), so an iPad shot lays out like an iPad.</summary>
        private static void Rematch(Canvas c, int w, int h)
        {
            var scaler = c.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
            scaler.matchWidthOrHeight = AspectMatch.MatchFor(w, h);
            scaler.enabled = false; scaler.enabled = true; // re-scale now
        }

        // The layouts at the shapes that matter, Edit or Play mode: a phone, a tall phone, an iPad. Judge sizes on the tall one.
        [MenuItem(Menus.Root + "Checks/Capture Phone 9:16", false, 70)] private static void Shot916() => Shot("phone_9x16", 720, 1280);
        [MenuItem(Menus.Root + "Checks/Capture Tall Phone 9:20", false, 71)] private static void Shot920() => Shot("phone_9x20", 1080, 2400);
        [MenuItem(Menus.Root + "Checks/Capture Tablet 3:4", false, 72)] private static void Shot34() => Shot("tablet_3x4", 1536, 2048);

        private static readonly Queue<System.Action> steps = new Queue<System.Action>();
        private static int frames;

        /// <summary>Runs the steps one every <paramref name="gap"/> frames (a level load, a menu, a shot...).</summary>
        public static void Sequence(int gap, params System.Action[] actions)
        {
            steps.Clear();
            foreach (System.Action a in actions) steps.Enqueue(a);
            frames = 0;
            gapFrames = gap;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static int gapFrames = 30;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || steps.Count == 0) { EditorApplication.update -= Tick; return; }
            if (++frames < gapFrames) return;
            frames = 0;
            steps.Dequeue()();
        }
    }
}
#endif
