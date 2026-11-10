using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// The screens are laid out for the portrait reference (720 x 1280) and the CanvasScaler matches width, so a tall
    /// phone gains height. A screen wider than 9:16 (an iPad is 3:4, a desktop window 16:9) would lose height instead
    /// and push the bottom of a layout off the screen, so there the canvas matches height and gains width at the sides.
    /// Added at runtime to every scale-with-screen-size canvas (no scene edits); the captures apply the same rule
    /// (<see cref="MatchFor"/>). From the duel game, where the map's cards ran over the iPad's top bar.
    /// </summary>
    [DefaultExecutionOrder(-100)] // before the CanvasScaler's own Update
    public class AspectMatch : MonoBehaviour
    {
        /// <summary>The reference aspect (width / height) the screens are laid out for.</summary>
        public static float ReferenceAspect => PrototypeUIHelper.ReferenceResolution.x / PrototypeUIHelper.ReferenceResolution.y;

        private CanvasScaler scaler;
        private int lastW, lastH;

        /// <summary>0 = match width (as tall as the reference or taller), 1 = match height (wider than the reference).</summary>
        public static float MatchFor(int width, int height) =>
            height > 0 && (float)width / height > ReferenceAspect + 0.001f ? 1f : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            AttachAll();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => AttachAll();

        /// <summary>Canvases made after the scene loaded (the kit's, a toast's) call this, or get it on the next scene load.</summary>
        public static void AttachAll()
        {
            foreach (CanvasScaler s in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
                if (s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && s.GetComponent<AspectMatch>() == null)
                    s.gameObject.AddComponent<AspectMatch>();
        }

        private void Awake() => scaler = GetComponent<CanvasScaler>();

        private void Update()
        {
            if (scaler == null || (Screen.width == lastW && Screen.height == lastH)) return;
            lastW = Screen.width;
            lastH = Screen.height;
            scaler.matchWidthOrHeight = MatchFor(lastW, lastH);
        }
    }
}
