#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Setup Project (once)</c>: everything a fresh clone needs before the first Play: TextMesh Pro's
    /// essential resources (the default font; Unity's, imported from the package, never committed), the sound bank
    /// (empty until picks are imported), the main scene built and registered. Safe to run again. <see cref="Batch"/>
    /// is the same for <c>Unity.exe -batchmode -executeMethod GameScaffold.Editor.ProjectSetup.Batch</c>
    /// (<c>tools/unity_batch.py setup</c>): the package import is asynchronous, so the setup waits for it on the
    /// editor's update tick and quits itself when done.
    /// </summary>
    public static class ProjectSetup
    {
        private const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const double ImportTimeout = 180.0;

        private static bool batch;
        private static double startedAt;

        [MenuItem(Menus.Root + "Setup Project (once)", false, 0)]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Setup] stop Play mode first"); return; }
            Start(false);
        }

        /// <summary>For a batch-mode Unity: setup, then quit with 0 (1 if the TMP import never finished).</summary>
        public static void Batch() => Start(true);

        private static void Start(bool isBatch)
        {
            batch = isBatch;
            if (File.Exists(TmpSettings)) { Finish(); return; }
            Debug.Log("[Setup] importing TextMesh Pro's essential resources...");
            AssetDatabase.importPackageCompleted += OnImported;
            AssetDatabase.importPackageFailed += OnImportFailed;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Poll;
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        private static void OnImported(string package) => Poll();

        private static void OnImportFailed(string package, string error)
        {
            Unhook();
            Debug.LogError($"[Setup] TMP essentials failed to import: {error}. Window > TextMeshPro > Import TMP Essential Resources, then run Setup again.");
            if (batch) EditorApplication.Exit(1);
        }

        private static void Poll()
        {
            if (File.Exists(TmpSettings))
            {
                Unhook();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[Setup] TextMesh Pro essentials imported");
                Finish();
            }
            else if (EditorApplication.timeSinceStartup - startedAt > ImportTimeout)
            {
                Unhook();
                Debug.LogError("[Setup] TMP essentials did not arrive in time. Window > TextMeshPro > Import TMP Essential Resources, then run Setup again.");
                if (batch) EditorApplication.Exit(1);
            }
        }

        private static void Unhook()
        {
            EditorApplication.update -= Poll;
            AssetDatabase.importPackageCompleted -= OnImported;
            AssetDatabase.importPackageFailed -= OnImportFailed;
        }

        private static void Finish()
        {
            SoundPicks.Import();
            SceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] done: TMP essentials in, sound bank built, Main scene built. Press Play.");
            if (batch) EditorApplication.Exit(0);
        }
    }
}
#endif
