#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary><c>Game > Services > Log Calls</c>: swap the do-nothing services for one that logs every call, to see
    /// where analytics and ads would hook in. Editor only; builds always get the do-nothing one.</summary>
    public static class ServicesMenu
    {
        private const string Item = Menus.Root + "Services/Log Calls (Editor)";

        [MenuItem(Item, false, 210)]
        public static void Toggle()
        {
            bool on = PlayerPrefs.GetInt(GameServices.LogPref, 0) == 0;
            PlayerPrefs.SetInt(GameServices.LogPref, on ? 1 : 0);
            PlayerPrefs.Save();
            GameServices.Current = null; // re-pick on next use
            Debug.Log($"[Services] Logging calls: {(on ? "on" : "off")}");
        }

        [MenuItem(Item, true)]
        public static bool ToggleCheck()
        {
            Menu.SetChecked(Item, PlayerPrefs.GetInt(GameServices.LogPref, 0) == 1);
            return true;
        }
    }
}
#endif
