using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// Phones stay portrait; tablets may rotate (from the slope game, where the owner said "let's try rotate" for
    /// iPads). With <see cref="TabletsRotate"/> on, the build must allow every orientation in Player Settings (so iOS
    /// lists them all and an iPad can turn), and at start-up a phone-shaped screen (long side over 1.6x the short one:
    /// every phone is 16:9 or longer) is locked back to portrait. The UI follows through <see cref="AspectMatch"/> and
    /// the safe area; an orthographic camera simply sees wider (<see cref="FunTestKit.DesignAspect"/> keeps the width).
    /// Off by default: portrait everywhere, as the Player Settings say.
    /// </summary>
    public static class Orientation
    {
        public const bool TabletsRotate = false;
        public const float PhoneRatio = 1.6f;

        public static bool IsTablet
        {
            get
            {
                float a = Screen.width, b = Screen.height;
                if (a <= 0f || b <= 0f) return false;
                return Mathf.Max(a, b) / Mathf.Min(a, b) < PhoneRatio;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.isEditor || !TabletsRotate) return; // the Editor's Game view decides its own shape
            if (IsTablet)
            {
                Screen.autorotateToPortrait = true;
                Screen.autorotateToPortraitUpsideDown = true;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            else Screen.orientation = Shell.Wide ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait; // a phone keeps the game's own shape
        }
    }
}
