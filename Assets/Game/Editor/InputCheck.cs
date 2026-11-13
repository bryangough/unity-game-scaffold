#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Checks > Keys and Pad (Play mode)</c>: presses every keyboard and gamepad binding of every
    /// <see cref="GameInput"/> action through the real Input System (a virtual pad, <see cref="InputBot"/>), and checks
    /// that the action goes down while held and up when released, and that <see cref="InputMode"/> follows the device
    /// (a key → Keys, a pad button → Pad, mouse travel → Pointer). Writes <c>Builds/Checks/keys.md</c>. The save is
    /// locked for the run. The dev keys (1–9, F1) are left out: they load levels and open the dev menu. Restart (R) does
    /// reload the scene, on the same level. The <see cref="Shell"/>'s menus keep still while the bindings are pressed
    /// (<see cref="Shell.IgnoreInput"/>), then get their own steps: Esc and Start open the pause with RESUME focused,
    /// Esc and B close it.
    /// <para>Grow it with the game's menus in <see cref="MenuSteps"/>, as the stealth game did: Esc in a level opens
    /// the pause with RESUME focused, the arrows move the focus, Esc closes it, the mouse clears the focus, Start and B
    /// do the same on a pad. Each step runs a few frames after the last; call <see cref="Check"/> to record a result.</para>
    /// </summary>
    public static class InputCheck
    {
        public const string ReportPath = "Builds/Checks/keys.md";
        private const int Gap = 6; // editor ticks between steps: a frame or two for the Input System to take the event

        private static readonly HashSet<string> Skip = new HashSet<string> { "Point", "Press", "DevMenu" };

        /// <summary>The game's own menu steps, run after the bindings (see the class summary).</summary>
        public static readonly List<Action> MenuSteps = new List<Action>();

        private static StringBuilder log;
        private static int fails, checks;

        [MenuItem(Menus.Root + "Checks/Keys and Pad (Play mode)", false, 62)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[InputCheck] enter Play mode first"); return; }
            if (Keyboard.current == null) { Debug.LogWarning("[InputCheck] no keyboard device"); return; }
            log = new StringBuilder().AppendLine("# Keys and pad check").AppendLine()
                .AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm}").AppendLine();
            fails = checks = 0;
            InputBot.Begin();
            Shell.IgnoreInput = true; // the bindings are pressed one by one: no menu should open or be clicked meanwhile
            InputMode.Set(InputMode.Mode.Pointer);
            _ = InputBot.Pad; // added now, so its first frame is behind it

            var steps = new List<Action>();
            foreach (InputAction action in GameInput.Map.actions)
            {
                if (Skip.Contains(action.name) || action.name.StartsWith("Slot")) continue;
                foreach (InputBinding b in action.bindings)
                {
                    string path = b.effectivePath;
                    if (!path.StartsWith("<Keyboard>/") && !path.StartsWith("<Gamepad>/")) continue;
                    InputAction a = action;
                    bool onPad = path.StartsWith("<Gamepad>/");
                    steps.Add(() =>
                    {
                        if (!InputBot.PressControl(path)) Check($"{a.name}: {path} can be pressed", false);
                    });
                    steps.Add(() =>
                    {
                        Check($"{a.name}: {path} holds it down", a.IsPressed());
                        Check($"{a.name}: {path} sets the mode to {(onPad ? "Pad" : "Keys")}",
                            InputMode.Current == (onPad ? InputMode.Mode.Pad : InputMode.Mode.Keys));
                        InputBot.ReleaseKeys();
                    });
                    steps.Add(() => Check($"{a.name}: {path} lets go", !a.IsPressed()));
                }
            }

            // the mouse takes the pointer back after a little travel
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                steps.Add(() => InputBot.MoveMouse(new Vector2(300 + 20 * k, 300), new Vector2(20f, 0f)));
            }
            steps.Add(() => Check("mouse travel sets the mode to Pointer", InputMode.Current == InputMode.Mode.Pointer));

            steps.Add(() => Shell.IgnoreInput = false);
            if (Shell.Instance != null) ShellSteps(steps);
            steps.AddRange(MenuSteps);

            steps.Add(() =>
            {
                InputBot.End();
                Shell.IgnoreInput = false;
                InputMode.Set(InputMode.Mode.Pointer);
                log.AppendLine().AppendLine(fails == 0 ? $"**All {checks} passed.**" : $"**{fails} of {checks} failed.**");
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, log.ToString());
                if (fails == 0) Debug.Log($"[InputCheck] all {checks} passed: {ReportPath}");
                else Debug.LogError($"[InputCheck] {fails} of {checks} failed: {ReportPath}");
            });
            Captures.Sequence(Gap, steps.ToArray());
        }

        /// <summary>The shell's pause on keys and a pad, from a level freshly reloaded into play.</summary>
        private static void ShellSteps(List<Action> steps)
        {
            string Focused()
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                return es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "nothing";
            }
            steps.Add(() => { Shell.Go(Shell.Play); FunTestKit.Restart(); });
            steps.Add(() => { });
            steps.Add(() => InputBot.PressControl("<Keyboard>/escape"));
            steps.Add(() =>
            {
                Check("Esc in play opens the pause", Shell.Flow.Top == Shell.Pause);
                Check($"the pause focuses RESUME for a key player (focused: {Focused()})", Focused() == "Button_Resume");
                InputBot.ReleaseKeys();
            });
            steps.Add(() => InputBot.PressControl("<Keyboard>/escape"));
            steps.Add(() => { Check("Esc closes the pause", !Shell.Flow.HasOverlay); InputBot.ReleaseKeys(); });
            steps.Add(() => InputBot.PressControl("<Gamepad>/start"));
            steps.Add(() => { Check("Start on a pad opens the pause", Shell.Flow.Top == Shell.Pause); InputBot.ReleaseKeys(); });
            steps.Add(() => InputBot.PressControl("<Gamepad>/buttonEast"));
            steps.Add(() => { Check("B on a pad closes the pause", !Shell.Flow.HasOverlay); InputBot.ReleaseKeys(); });
        }

        /// <summary>Records one result in the report (only failures are listed one by one, with the count at the end).</summary>
        public static void Check(string what, bool ok)
        {
            checks++;
            if (ok) return;
            fails++;
            log.AppendLine($"- **FAIL**: {what} (mode {InputMode.Current})");
            Debug.LogWarning($"[InputCheck] FAIL: {what} (mode {InputMode.Current})");
        }
    }
}
#endif
