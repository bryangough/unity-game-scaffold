#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Checks > Smoke Run (Play mode)</c>: the smallest bot. In Play mode it locks the save, lets the game
    /// run on its own for a few seconds, captures the screen to <c>Builds/Checks/views/smoke.png</c>, then presses R
    /// through the real input path and checks the scene came back. An assistant driving the Editor runs this after a
    /// change to see that the game opens, draws and restarts without a hand on it; <c>Logs/FunTest.txt</c> holds any
    /// result the kit wrote meanwhile. Once the game has plans to play, <see cref="LevelReplay"/> plays every level.
    /// </summary>
    public static class SmokeRun
    {
        public const string ReportPath = "Builds/Checks/smoke.md";

        [MenuItem(Menus.Root + "Checks/Smoke Run (Play mode)", false, 61)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Smoke] enter Play mode first"); return; }
            InputBot.Begin();
            int frame0 = Time.frameCount;
            Captures.Sequence(60,
                () => Captures.Shot("smoke"),
                () => InputBot.PressKey(UnityEngine.InputSystem.Key.R),
                () => InputBot.ReleaseKeys(),
                () =>
                {
                    bool kit = Object.FindAnyObjectByType<FunTestKit>() != null;
                    bool cam = Camera.main != null;
                    InputBot.End();
                    string report = $"# Smoke run\n\n{System.DateTime.Now:yyyy-MM-dd HH:mm}: {Time.frameCount - frame0} frames, camera {(cam ? "ok" : "MISSING")}, kit after restart {(kit ? "ok" : "MISSING")}, capture Builds/Checks/views/smoke.png\n";
                    Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                    File.WriteAllText(ReportPath, report);
                    if (kit && cam) Debug.Log("[Smoke] ok: " + ReportPath); else Debug.LogError("[Smoke] FAILED: " + report);
                });
        }
    }
}
#endif
