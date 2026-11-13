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
    /// A virtual gamepad (<see cref="Pad"/>, removed at <see cref="End"/>) takes <see cref="PressButton"/> and
    /// <see cref="PressControl"/>; <see cref="MoveMouse"/> moves the real mouse device (what switches
    /// <see cref="InputMode"/> back to the pointer). Pointer input goes through <see cref="TouchInputAdapter.SimActive"/>. For a deterministic replay pin
    /// <c>Time.captureDeltaTime</c> to the game's tick so one tick runs per frame however fast the frames come.
    /// </summary>
    public static class InputBot
    {
        private static InputSettings.EditorInputBehaviorInPlayMode oldInput;
        private static InputSettings.BackgroundBehavior oldBackground;
        private static float oldCapture;
        private static bool began;
        private static Gamepad pad;

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
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
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

        /// <summary>Releases every key and every pad button and stick.</summary>
        public static void ReleaseKeys()
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            if (pad != null && pad.added) InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        /// <summary>The bot's own gamepad, added on first use (a real one plugged in is left alone).</summary>
        public static Gamepad Pad
        {
            get
            {
                if (pad == null || !pad.added) pad = InputSystem.AddDevice<Gamepad>("InputBotPad");
                return pad;
            }
        }

        /// <summary>Holds one pad button from the end of this frame; release with <see cref="ReleaseKeys"/>.</summary>
        public static void PressButton(GamepadButton button) => InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(button));

        /// <summary>
        /// Holds any one control by its binding path: <c>&lt;Keyboard&gt;/space</c>, <c>&lt;Gamepad&gt;/dpad/up</c>,
        /// <c>&lt;Gamepad&gt;/leftStick/left</c> (the stick pushed fully that way). False when the path is not a
        /// keyboard or gamepad control this can press.
        /// </summary>
        public static bool PressControl(string bindingPath)
        {
            InputDevice device;
            string rest;
            if (bindingPath.StartsWith("<Keyboard>/")) { device = Keyboard.current; rest = bindingPath.Substring(11); WakeKeyboard(); }
            else if (bindingPath.StartsWith("<Gamepad>/")) { device = Pad; rest = bindingPath.Substring(10); }
            else return false;
            if (device == null) return false;

            if (device is Gamepad g && (rest.StartsWith("leftStick/") || rest.StartsWith("rightStick/")))
            {
                string dir = rest.Substring(rest.IndexOf('/') + 1);
                Vector2 v = dir == "up" ? Vector2.up : dir == "down" ? Vector2.down : dir == "left" ? Vector2.left : dir == "right" ? Vector2.right : Vector2.zero;
                if (v == Vector2.zero) return false;
                var state = new GamepadState();
                if (rest.StartsWith("left")) state.leftStick = v; else state.rightStick = v;
                InputSystem.QueueStateEvent(g, state);
                return true;
            }

            InputControl control = device.TryGetChildControl(rest);
            if (!(control is UnityEngine.InputSystem.Controls.ButtonControl button)) return false;
            using (StateEvent.From(device, out InputEventPtr e))
            {
                button.WriteValueIntoEvent(1f, e);
                InputSystem.QueueEvent(e);
            }
            return true;
        }

        /// <summary>Moves the mouse by <paramref name="delta"/> pixels to <paramref name="position"/> (a few calls past
        /// 40 px of travel switch <see cref="InputMode"/> back to the pointer).</summary>
        public static void MoveMouse(Vector2 position, Vector2 delta)
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.enabled) InputSystem.EnableDevice(Mouse.current);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position, delta = delta });
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
