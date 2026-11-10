using UnityEngine;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// A small fps / frame-ms / worst-frame readout at the bottom left, so a phone test gives numbers. Development
    /// builds only, plus any build with the <c>DEV_FPS</c> define. Never in the Editor. Creates itself; nothing in the scene.
    /// </summary>
    public class DevFps : MonoBehaviour
    {
        private TMPro.TextMeshProUGUI text;
        private float acc, worst, nextShow;
        private int frames;

        // In a development build Unity pops its console up over the game on any logged error. Logs still reach Xcode,
        // logcat and the browser console.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void NoConsole() { Debug.developerConsoleEnabled = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isEditor) return;
#if !DEV_FPS
            if (!Debug.isDebugBuild) return;
#endif
            var go = new GameObject("DevFps");
            DontDestroyOnLoad(go);
            go.AddComponent<DevFps>();
        }

        private void Awake()
        {
            var canvasGo = new GameObject("DevFpsCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            PrototypeUIHelper.SetupScaler(canvasGo.AddComponent<CanvasScaler>());
            text = UIText.Make(canvasGo.transform, "Fps", 22f, Color.white, TMPro.TextAlignmentOptions.BottomLeft);
            RectTransform r = text.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(12f, 6f);
            r.sizeDelta = new Vector2(500f, 32f);
            UIText.Outline(text);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            acc += dt;
            frames++;
            worst = Mathf.Max(worst, dt);
            if (Time.unscaledTime < nextShow) return;
            float avg = acc / Mathf.Max(1, frames);
            text.text = $"{1f / Mathf.Max(0.0001f, avg):0} fps  {avg * 1000f:0.0} ms  worst {worst * 1000f:0} ms";
            text.color = avg > 1f / 29f ? new Color(1f, 0.35f, 0.3f) : avg > 1f / 50f ? new Color(1f, 0.8f, 0.3f) : new Color(0.5f, 1f, 0.5f);
            acc = 0f;
            frames = 0;
            worst = 0f;
            nextShow = Time.unscaledTime + 0.5f;
        }
    }
}
