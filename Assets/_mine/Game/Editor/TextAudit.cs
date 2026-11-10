#if UNITY_EDITOR
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Checks > Text Fit Audit</c>: every TextMesh Pro label in the open scene (and in prefabs under
    /// <c>Assets/_mine</c>) checked against the readability floors a phone test set, scaled to the 720-wide canvas:
    /// body text ≥ 24 px, captions ≥ 20, button labels ≥ 28; and no label whose text overflows its box. Writes
    /// <c>Builds/Checks/text.md</c>. From the duel game's iPhone test ("too small to read at arm's length").
    /// </summary>
    public static class TextAudit
    {
        public const float BodyMin = 24f, CaptionMin = 20f, ButtonMin = 28f;
        public const string ReportPath = "Builds/Checks/text.md";

        [MenuItem(Menus.Root + "Checks/Text Fit Audit", false, 62)]
        public static void Run()
        {
            var sb = new StringBuilder();
            int issues = 0, labels = 0;
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)) { labels++; issues += Check(t, "scene", sb); }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_mine" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try { foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) { labels++; issues += Check(t, Path.GetFileNameWithoutExtension(path), sb); } }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, $"# Text fit audit\n\n{System.DateTime.Now:yyyy-MM-dd HH:mm}: {labels} labels, {issues} issues. Floors: body {BodyMin}, captions {CaptionMin}, button labels {ButtonMin} (720-wide canvas).\n\n{sb}");
            if (issues == 0) Debug.Log($"[TextAudit] {labels} labels, no issues ({ReportPath})"); else Debug.LogWarning($"[TextAudit] {labels} labels, {issues} issues:\n{sb}");
        }

        private static int Check(TMP_Text t, string where, StringBuilder sb)
        {
            int n = 0;
            bool onButton = t.GetComponentInParent<Button>() != null;
            float floor = onButton ? ButtonMin : t.fontSize < BodyMin ? CaptionMin : BodyMin;
            float size = t.enableAutoSizing ? t.fontSizeMin : t.fontSize;
            string name = $"{where}: {HierarchyPath(t.transform)}";
            if (size < floor) { sb.AppendLine($"- {name}: {(t.enableAutoSizing ? "fontSizeMin" : "size")} {size} px under the {floor} px floor"); n++; }
            if (!string.IsNullOrEmpty(t.text) && t.rectTransform.rect.width > 0f)
            {
                t.ForceMeshUpdate(true, true);
                if (t.isTextOverflowing && t.overflowMode != TextOverflowModes.Overflow) { sb.AppendLine($"- {name}: text overflows its box (\"{Short(t.text)}\")"); n++; }
            }
            return n;
        }

        private static string Short(string s) => s.Length > 40 ? s.Substring(0, 40).Replace("\n", " ") + "…" : s.Replace("\n", " ");

        private static string HierarchyPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
#endif
