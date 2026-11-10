using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// Phone fit: 60 frames a second (release builds on phones default to 30), the screen kept awake while playing,
    /// every overlay canvas's contents kept inside <see cref="Screen.safeArea"/> (clear of a notch, a punch-hole camera
    /// and the home bar), and a flag for the game to pause while the app is in the background. The game manager calls
    /// <see cref="Apply"/> when a level opens and forwards <c>OnApplicationPause</c> / <c>OnApplicationFocus</c> to
    /// <see cref="SetBackground"/>.
    /// </summary>
    public static class PhoneFit
    {
        /// <summary>True while the app is in the background on a phone: the game stops ticking.</summary>
        public static bool Paused { get; private set; }

        public static void Apply()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (c.GetComponentInChildren<SafeAreaPanel>(true) != null) continue;
                SafeAreaPanel.Wrap(c);
            }
        }

        /// <summary>Only phones pause: on a desktop, clicking another window leaves the game running.</summary>
        public static void SetBackground(bool inBackground)
        {
            if (!Application.isMobilePlatform) { Paused = false; return; }
            Paused = inBackground;
        }
    }

    /// <summary>A full-canvas panel resized to the screen's safe area; the canvas's other children move inside it.</summary>
    public class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform rect;
        private Rect applied;
        private Vector2Int screen;

        public static void Wrap(Canvas canvas)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(canvas.transform, false);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var kids = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in canvas.transform) if (t != r) kids.Add(t);
            foreach (Transform t in kids) t.SetParent(r, false);
            go.AddComponent<SafeAreaPanel>().Fit();
        }

        private void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Fit();
        }

        private void Fit()
        {
            if (rect == null) rect = (RectTransform)transform;
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rect.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rect.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Covers the whole screen from inside the safe area: a menu's dim or a backdrop reaches the real screen edges, past
    /// a notch and the home bar, while the buttons beside it stay in the safe area.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BleedToScreen : MonoBehaviour
    {
        private RectTransform rect, root;

        private void OnEnable() => Fit();
        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (root == null)
            {
                Canvas c = GetComponentInParent<Canvas>(true);
                if (c == null) return;
                root = (RectTransform)c.rootCanvas.transform;
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Vector3 centre = root.TransformPoint(root.rect.center);
            if (rect.position != centre) rect.position = centre;
            Vector2 size = root.rect.size; // the panels between this and the canvas are unscaled
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }
    }
}
