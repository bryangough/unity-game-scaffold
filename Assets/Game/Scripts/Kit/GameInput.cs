using UnityEngine;
using UnityEngine.InputSystem;

namespace GameScaffold
{
    /// <summary>
    /// Every input the game reads, as Input System actions: one map built in code and enabled on first use, so scripts
    /// ask "was Up pressed?" instead of "was W pressed?", and a gamepad works for free. Add a game's own actions here
    /// (one <see cref="Button"/> line each); never read <c>Keyboard.current</c> or <c>UnityEngine.Input</c> elsewhere.
    /// <list type="bullet">
    /// <item>Point / Press: the touch or mouse pointer, read by <see cref="TouchInputAdapter"/> for taps and swipes.</item>
    /// <item>Up / Down / Left / Right: W A S D, arrows, d-pad, left stick. Four buttons rather than one 2D value, so each
    /// press is exactly one step.</item>
    /// <item>Restart: R, gamepad select. Next: N / Enter / Space / gamepad south (after a win).</item>
    /// <item>Menus on keys or a pad: Pause (Esc, Start), Back (Esc, Backspace, B), Submit (Enter, Space, A), Help (H, Y),
    /// PagePrev / PageNext (Q / E, LB / RB). Arrows / d-pad move the focus through the UI module's own Navigate action.</item>
    /// <item>Slot(i): the dev keys 1-9 (a level picker in the Editor and development builds).</item>
    /// </list>
    /// </summary>
    public static class GameInput
    {
        public const int SlotKeys = 9;

        private static InputActionMap map;
        private static InputAction point, press, up, down, left, right, restart, next;
        private static InputAction pause, back, submit, help, pagePrev, pageNext, devMenu;
        private static InputAction[] slots;

        public static InputAction Point { get { Init(); return point; } }
        public static InputAction Press { get { Init(); return press; } }
        public static InputAction Up { get { Init(); return up; } }
        public static InputAction Down { get { Init(); return down; } }
        public static InputAction Left { get { Init(); return left; } }
        public static InputAction Right { get { Init(); return right; } }
        public static InputAction Restart { get { Init(); return restart; } }
        public static InputAction Next { get { Init(); return next; } }
        public static InputAction Pause { get { Init(); return pause; } }
        public static InputAction Back { get { Init(); return back; } }
        public static InputAction Submit { get { Init(); return submit; } }
        public static InputAction Help { get { Init(); return help; } }
        public static InputAction PagePrev { get { Init(); return pagePrev; } }
        public static InputAction PageNext { get { Init(); return pageNext; } }
        /// <summary>F1: the dev menu (Editor, development builds, or a release build once unlocked).</summary>
        public static InputAction DevMenu { get { Init(); return devMenu; } }

        /// <summary>The dev key for slot i (0-based, keys 1-9), or null.</summary>
        public static InputAction Slot(int i) { Init(); return i >= 0 && i < slots.Length ? slots[i] : null; }

        /// <summary>The direction pressed this frame (x right, y up), or zero.</summary>
        public static Vector2Int DirectionPressedThisFrame()
        {
            if (Up.WasPressedThisFrame()) return Vector2Int.up;
            if (Down.WasPressedThisFrame()) return Vector2Int.down;
            if (Right.WasPressedThisFrame()) return Vector2Int.right;
            if (Left.WasPressedThisFrame()) return Vector2Int.left;
            return Vector2Int.zero;
        }

        /// <summary>The direction held down now (for hold-to-repeat), or zero.</summary>
        public static Vector2Int DirectionHeld()
        {
            if (Up.IsPressed()) return Vector2Int.up;
            if (Down.IsPressed()) return Vector2Int.down;
            if (Right.IsPressed()) return Vector2Int.right;
            if (Left.IsPressed()) return Vector2Int.left;
            return Vector2Int.zero;
        }

        // Domain reload is off (Enter Play Mode options), so statics are reset by hand
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (map != null) { map.Disable(); map.Dispose(); }
            map = null;
        }

        private static void Init()
        {
            if (map != null) return;
            map = new InputActionMap("Game");

            point = map.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Touchscreen>/primaryTouch/position");
            point.AddBinding("<Mouse>/position");

            press = map.AddAction("Press", InputActionType.PassThrough, expectedControlLayout: "Button");
            press.AddBinding("<Touchscreen>/primaryTouch/press");
            press.AddBinding("<Mouse>/leftButton");

            up = Button("Up", "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            down = Button("Down", "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            left = Button("Left", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            right = Button("Right", "<Keyboard>/d", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");

            restart = Button("Restart", "<Keyboard>/r", "<Gamepad>/select");
            next = Button("Next", "<Keyboard>/n", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");

            pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            back = Button("Back", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            submit = Button("Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            help = Button("Help", "<Keyboard>/h", "<Gamepad>/buttonNorth");
            pagePrev = Button("PagePrev", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            pageNext = Button("PageNext", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            devMenu = Button("DevMenu", "<Keyboard>/f1");

            slots = new InputAction[SlotKeys];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Button("Slot" + (i + 1), "<Keyboard>/" + (i + 1), "<Keyboard>/numpad" + (i + 1));

            map.Enable();
        }

        private static InputAction Button(string name, params string[] paths)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (string p in paths) a.AddBinding(p);
            return a;
        }
    }
}
