using UnityEngine;
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GameScaffold
{
    /// <summary>
    /// Short buzzes on phones: a light tick on a step or a tap, medium on a bigger action or a win, heavy on a loss.
    /// Android uses a one-shot <c>VibrationEffect</c> (API 26+, the project minimum), iOS the system
    /// impact feedback (<c>Assets/Plugins/iOS/Haptics.mm</c>), the web <c>navigator.vibrate</c> (Android browsers;
    /// iPhone Safari has none). Off when the Vibration setting is off; does nothing in the Editor or on desktop.
    /// </summary>
    public static class Haptics
    {
        public enum Strength { Light, Medium, Heavy }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _gsHaptic(int style);
#elif UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GSVibrate(int milliseconds);
#elif UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass effectClass;
        private static bool androidReady;
#endif

        public static void Play(Strength strength)
        {
            if (!Progress.Current.Haptics) return;
#if UNITY_IOS && !UNITY_EDITOR
            _gsHaptic((int)strength);
#elif UNITY_WEBGL && !UNITY_EDITOR
            GSVibrate(strength == Strength.Light ? 10 : strength == Strength.Medium ? 25 : 60);
#elif UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(strength == Strength.Light ? 10 : strength == Strength.Medium ? 25 : 60,
                        strength == Strength.Light ? 80 : strength == Strength.Medium ? 160 : 255);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void PlayAndroid(long ms, int amplitude)
        {
            try
            {
                if (!androidReady)
                {
                    androidReady = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
                if (vibrator == null) return;
                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                    vibrator.Call("vibrate", effect);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] " + e.Message);
                vibrator = null;
            }
        }

        // Never called: a reference to Handheld.Vibrate is what makes Unity add the VIBRATE permission to the manifest.
        private static void KeepVibratePermission() => Handheld.Vibrate();
#endif
    }
}
