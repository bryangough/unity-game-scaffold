#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameScaffold
{
    /// <summary>
    /// Hands for an Editor bot that plays the game through the real input path (a replay of solver plans, a smoke run,
    /// a store capture), so the views, the input and the frame loop are tested, not just the rules. A bot is a
    /// MonoBehaviour in Play mode: it calls <see cref="Begin"/> once, presses keys with <see cref="PressKey"/> at
    /// the end of a frame (the game reads them next frame through <see cref="GameInput"/>), releases with
    /// <see cref="ReleaseKeys"/>, and calls <see cref="End"/> when done, or from OnDestroy if Play mode stops early.
    /// Pointer input goes through <see cref="TouchInputAdapter.SimActive"/>. For a deterministic replay pin
    /// <c>Time.captureDeltaTime</c> to the game's tick so one tick runs per frame however fast the frames come.
    /// </summary>
    public static class InputBot
    {
        private static InputSettings.EditorInputBehaviorInPlayMode oldInput;
        private static InputSettings.BackgroundBehavior oldBackground;
        private static float oldCapture;
        private static bool began;

        /// <summary>Locks the save, routes every device to the Game view, keeps input alive with the Editor in the
        /// background, and (optionally) pins the frame step.</summary>
        public static void Begin(float captureDeltaTime = 0f)
        {
            if (began) return;
            began = true;
            Progress.Suspended = true; // the bot's wins never reach the player's save
            oldCapture = Time.captureDeltaTime;
            if (captureDeltaTime > 0f) Time.captureDeltaTime = captureDeltaTime;
            oldInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            WakeKeyboard();
        }

        /// <summary>Puts time, input and the save back (the save is re-read from disk, so nothing the bot did stays in memory).</summary>
        public static void End()
        {
            if (!began) return;
            began = false;
            ReleaseKeys();
            Progress.Suspended = false;
            Progress.Reload();
            Time.captureDeltaTime = oldCapture;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldInput;
            InputSystem.settings.backgroundBehavior = oldBackground;
            TouchInputAdapter.SimActive = false;
        }

        /// <summary>Holds one key from the end of this frame; release next frame with <see cref="ReleaseKeys"/>.</summary>
        public static void PressKey(Key key)
        {
            if (Keyboard.current == null) return;
            WakeKeyboard();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        }

        public static void ReleaseKeys()
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        /// <summary>A simulated finger: down at a screen point this frame; call <see cref="TouchUp"/> to lift it.</summary>
        public static void TouchDown(Vector2 screenPos)
        {
            TouchInputAdapter.SimActive = true;
            TouchInputAdapter.SimDown = true;
            TouchInputAdapter.SimPosition = screenPos;
        }

        public static void TouchMove(Vector2 screenPos) => TouchInputAdapter.SimPosition = screenPos;

        public static void TouchUp() => TouchInputAdapter.SimDown = false;

        /// <summary>
        /// The Input System switches the keyboard off while the Game view (or the Editor) does not have focus, and a
        /// device switched off drops every event queued to it: a bot started from a menu would press nothing. Changing
        /// the focus setting does not switch it back on; this does.
        /// </summary>
        public static void WakeKeyboard()
        {
            if (Keyboard.current != null && !Keyboard.current.enabled) InputSystem.EnableDevice(Keyboard.current);
        }
    }
}
#endif
