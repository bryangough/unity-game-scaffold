#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Sets the Game view to a fixed resolution so the layouts can be checked (and played) at phone, tall phone, iPad
    /// and landscape shapes. Unity has no public API for this; it reaches into the internal GameViewSizes, so it may
    /// need a touch after a Unity upgrade (it fails with a warning, never an error). From the slope game.
    /// </summary>
    public static class GameViewSize
    {
        [MenuItem(Menus.Root + "Dev/Game View/Phone 9:16 (720x1280)", false, 80)] private static void P916() => Set(720, 1280);
        [MenuItem(Menus.Root + "Dev/Game View/Tall phone 9:19.5 (1170x2532)", false, 81)] private static void P195() => Set(1170, 2532);
        [MenuItem(Menus.Root + "Dev/Game View/iPad portrait 3:4 (1536x2048)", false, 82)] private static void T34() => Set(1536, 2048);
        [MenuItem(Menus.Root + "Dev/Game View/iPad landscape 4:3 (2048x1536)", false, 83)] private static void T43() => Set(2048, 1536);
        [MenuItem(Menus.Root + "Dev/Game View/Desktop 16:9 (1920x1080)", false, 84)] private static void D169() => Set(1920, 1080);

        /// <summary>Selects (adding if needed) a fixed-resolution size in the Game view's current group.</summary>
        public static bool Set(int width, int height)
        {
            try
            {
                Assembly asm = typeof(UnityEditor.Editor).Assembly;
                Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
                Type single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = single.GetProperty("instance").GetValue(null);
                object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
                Type groupType = group.GetType();

                int index = -1;
                MethodInfo getTotal = groupType.GetMethod("GetTotalCount");
                MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
                int total = (int)getTotal.Invoke(group, null);
                for (int i = 0; i < total; i++)
                {
                    object s = getSize.Invoke(group, new object[] { i });
                    Type st = s.GetType();
                    if ((int)st.GetProperty("width").GetValue(s) == width && (int)st.GetProperty("height").GetValue(s) == height) { index = i; break; }
                }
                if (index < 0)
                {
                    Type sizeType = asm.GetType("UnityEditor.GameViewSize");
                    Type kind = asm.GetType("UnityEditor.GameViewSizeType");
                    object fixedRes = Enum.Parse(kind, "FixedResolution");
                    object size = Activator.CreateInstance(sizeType, fixedRes, width, height, $"{width}x{height}");
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = (int)getTotal.Invoke(group, null) - 1;
                }

                Type gameViewType = asm.GetType("UnityEditor.GameView");
                EditorWindow gv = EditorWindow.GetWindow(gameViewType, false, null, false);
                PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                selected.SetValue(gv, index);
                gv.Repaint();
                Debug.Log($"[GameViewSize] {width}x{height}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameViewSize] could not set the Game view size (Unity's internals moved?): " + e.Message);
                return false;
            }
        }
    }
}
#endif
