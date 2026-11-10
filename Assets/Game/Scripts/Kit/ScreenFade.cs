using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// A full-screen fade between screens: <see cref="Transition"/> fades to black, runs the swap (show a screen,
    /// load a level, even load a scene), waits a frame so the new screen lays out, then fades back. Unscaled time;
    /// blocks input while visible. Builds its own canvas on first use and survives scene loads.
    /// </summary>
    public static class ScreenFade
    {
        public static bool IsFading => Host.instance != null && Host.instance.running != null;

        /// <summary>Black out over <paramref name="outSeconds"/>, call <paramref name="atBlack"/>, fade back in over <paramref name="inSeconds"/>.</summary>
        public static void Transition(Action atBlack, float outSeconds = 0.18f, float inSeconds = 0.25f)
        {
            Host h = Host.Get();
            if (h == null) { atBlack?.Invoke(); return; }
            if (h.running != null) h.StopCoroutine(h.running);
            h.running = h.StartCoroutine(h.Run(atBlack, outSeconds, inSeconds));
        }

        /// <summary>Start black and reveal (the first frame of a scene).</summary>
        public static void FadeIn(float seconds = 0.4f)
        {
            Host h = Host.Get();
            if (h == null) return;
            h.Set(1f);
            if (h.running != null) h.StopCoroutine(h.running);
            h.running = h.StartCoroutine(h.Run(null, 0f, seconds));
        }

        private class Host : MonoBehaviour
        {
            public static Host instance;
            public Coroutine running;
            private CanvasGroup group;

            public static Host Get()
            {
                if (!Application.isPlaying) return null;
                if (instance != null) return instance;
                var go = new GameObject("ScreenFade");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
                black.transform.SetParent(go.transform, false);
                var r = (RectTransform)black.transform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                black.GetComponent<Image>().color = Color.black;
                instance = go.AddComponent<Host>();
                instance.group = go.AddComponent<CanvasGroup>();
                instance.Set(0f);
                return instance;
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetStatics() { instance = null; }

            public void Set(float a)
            {
                group.alpha = a;
                group.blocksRaycasts = a > 0.001f;
            }

            public IEnumerator Run(Action atBlack, float outSeconds, float inSeconds)
            {
                yield return Fade(group.alpha, 1f, outSeconds);
                try { atBlack?.Invoke(); }
                catch (Exception e) { Debug.LogException(e, this); }
                yield return null; // let the new screen lay out before revealing it
                yield return Fade(1f, 0f, inSeconds);
                running = null;
            }

            private IEnumerator Fade(float from, float to, float seconds)
            {
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.unscaledDeltaTime;
                    Set(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                    yield return null;
                }
                Set(to);
            }
        }
    }
}
