#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Checks > Replay Levels (Play mode)</c>: every level's stored plan played through the real game by
    /// <see cref="ReplayBot"/>, a report in <c>Builds/Checks/replay.md</c>. Run it before a build and after anything
    /// that touches input, the views or the frame loop. The save is locked for the run and read back at the end.
    /// Register the game's spec next to its level check: <c>LevelReplay.Game = new ReplayBot.Spec { … }</c>, with
    /// <c>Plan = i => LevelCheck.Plan(LevelCheck.Game, i)</c> so a plan only ever comes from the check's cache
    /// (a replay that solved levels at Play start once ran for minutes and crashed the Editor).
    /// It needs the real Editor: a batch Unity's Play mode barely ticks.
    /// </summary>
    public static class LevelReplay
    {
        public const string ReportPath = "Builds/Checks/replay.md";

        /// <summary>The game's replay; set from an <c>[InitializeOnLoadMethod]</c> in the game's editor code.</summary>
        public static ReplayBot.Spec Game;

        [MenuItem(Menus.Root + "Checks/Replay Levels (Play mode)", false, 60)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Replay] enter Play mode first"); return; }
            if (Game == null || Game.Play == null) { Debug.LogError("[Replay] no replay registered: set LevelReplay.Game from an [InitializeOnLoadMethod] in the game's editor code (see SampleChecks)"); return; }
            if (ReplayBot.Running) { Debug.LogWarning("[Replay] already running"); return; }
            if (File.Exists(ReportPath)) File.Delete(ReportPath); // a stale report must not pass for a new one
            ReplayBot.Run(Game, (markdown, failed) =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, markdown);
                if (failed == 0) Debug.Log("[Replay] every level won: " + ReportPath);
                else Debug.LogError($"[Replay] {failed} level(s) did not win: {ReportPath}");
            });
        }
    }
}
#endif
