#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Saved Data</c>: start as a new player, open every level for testing, or print the save. The save
    /// in the Editor is the owner's real save when they play in the same Editor: ask before clearing it.
    /// </summary>
    public static class SavedDataMenu
    {
        [MenuItem(Menus.Root + "Saved Data/Clear All (New Player)", false, 200)]
        public static void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("Clear the save?", "Deletes every level's record and the stars, as if the game had just been installed. The settings (music, sound, vibration) are kept.", "Clear", "Cancel")) return;
            Progress.ClearAll();
            Debug.Log("[Save] Cleared: the next Play starts as a new player in level 1.");
        }

        private const string UnlockItem = Menus.Root + "Saved Data/Unlock Everything (Testing)";

        [MenuItem(UnlockItem, false, 201)]
        public static void UnlockEverything()
        {
            Progress.Current.UnlockAll = !Progress.Current.UnlockAll;
            Debug.Log($"[Save] Unlock everything: {(Progress.Current.UnlockAll ? "on" : "off")} (stars and wins are kept).");
        }

        [MenuItem(UnlockItem, true)]
        public static bool UnlockCheck() { Menu.SetChecked(UnlockItem, Progress.Current.UnlockAll); return true; }

        [MenuItem(Menus.Root + "Saved Data/Show in Console", false, 202)]
        public static void Show()
        {
            Progress p = Progress.Current;
            Debug.Log($"[Save] last level {p.LastLevel + 1}, {p.SolvedCount} solved, {p.TotalStars} stars, unlock all {p.UnlockAll}\n{p.ToJson()}");
        }
    }
}
#endif
