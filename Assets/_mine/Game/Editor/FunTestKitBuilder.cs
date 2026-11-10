#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>Called by the scene builder just before it saves the scene.</summary>
    public static class FunTestKitBuilder
    {
        public static void AddKit()
        {
            if (Object.FindAnyObjectByType<FunTestKit>() != null) return;
            new GameObject("FunTestKit").AddComponent<FunTestKit>();
        }

        /// <summary>Build Settings = this one scene (restart reloads it by name).</summary>
        public static void RegisterScene(string scenePath)
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        }
    }
}
#endif
