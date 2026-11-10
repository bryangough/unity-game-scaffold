using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameScaffold
{
    /// <summary>
    /// What the player is using right now: the last device that pressed something or moved. Menus focus a button and
    /// show key prompts only for <see cref="Mode.Keys"/> and <see cref="Mode.Pad"/>; with a mouse or a finger nothing
    /// changes (a player on a touch screen never sees "Press A").
    /// </summary>
    public static class InputMode
    {
        public enum Mode { Pointer, Keys, Pad }

        public static Mode Current { get; private set; } = Mode.Pointer;
        public static bool UsingKeysOrPad => Current != Mode.Pointer;
        /// <summary>Raised when <see cref="Current"/> changes.</summary>
        public static event System.Action<Mode> Changed;

        /// <summary>Mouse travel (pixels, summed) after a key or pad press before the mouse counts again: a nudged desk
        /// or a jittery cursor is not "using the mouse".</summary>
        private const float MouseTravelToSwitch = 40f;
        private static float mouseTravel;
        private const float StickDeadZone = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Mode.Pointer;
            Changed = null;
            mouseTravel = 0f;
            InputSystem.onEvent -= OnEvent;
            InputSystem.onEvent += OnEvent;
        }

        /// <summary>For tests and captures.</summary>
        public static void Set(Mode m)
        {
            if (m == Current) return;
            Current = m;
            mouseTravel = 0f;
            Changed?.Invoke(m);
        }

        private static void OnEvent(InputEventPtr e, InputDevice device)
        {
            if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) return;
            switch (device)
            {
                case Keyboard _:
                    if (AnyPressed(e, device)) Set(Mode.Keys);
                    break;
                case Gamepad pad:
                    if (AnyPressed(e, device) || pad.leftStick.ReadValueFromEvent(e).magnitude > StickDeadZone || pad.dpad.ReadValueFromEvent(e).magnitude > StickDeadZone) Set(Mode.Pad);
                    break;
                case Touchscreen _:
                    if (AnyPressed(e, device)) Set(Mode.Pointer);
                    break;
                case Mouse mouse:
                    if (AnyPressed(e, device)) { Set(Mode.Pointer); break; }
                    if (Current == Mode.Pointer) break;
                    mouseTravel += mouse.delta.ReadValueFromEvent(e).magnitude;
                    if (mouseTravel > MouseTravelToSwitch) Set(Mode.Pointer);
                    break;
            }
        }

        /// <summary>A button on the device went down in this event (not a release, not noise).</summary>
        private static bool AnyPressed(InputEventPtr e, InputDevice device)
        {
            foreach (InputControl c in e.EnumerateChangedControls(device, 0.5f))
                if (c is UnityEngine.InputSystem.Controls.ButtonControl b && !b.synthetic && b.ReadValueFromEvent(e) > 0.5f) return true;
            return false;
        }
    }
}
