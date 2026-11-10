#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Per-platform texture caps for third-party art. Bought packs ship 2–4K uncompressed maps (metallic, emissive,
    /// normal per colour variant); in the duel game the uncapped packs made 345–387 MB APKs and ran the WebGL build out
    /// of memory. Only the textures a build can reach are touched (the build scenes and our Resources folders, with
    /// their dependencies), only outside <c>Assets/_mine</c>, and UI sprites are left alone. Writes platform overrides
    /// into the packs' import settings (.meta): a deliberate exception to the read-only rule (the files themselves are
    /// not changed). Re-runnable; <see cref="PhoneBuilds"/> runs it for the target before every build. Our own art is
    /// capped by hand in <see cref="WebSize"/>.
    /// </summary>
    public static class TextureCaps
    {
        public const int MaxSize = 1024;
        /// <summary>Metallic, emissive and normal maps on low-poly swatch atlases keep their sheen and glow at 256.</summary>
        public const int SecondaryMax = 256;

        [MenuItem(Menus.Root + "Build/Cap Third-Party Textures for WebGL", false, 141)]
        public static void WebGL() => Apply("WebGL", MaxSize);

        [MenuItem(Menus.Root + "Build/Cap Third-Party Textures for Android", false, 142)]
        public static void Android() => Apply("Android", MaxSize);

        [MenuItem(Menus.Root + "Build/Cap Third-Party Textures for iOS", false, 143)]
        public static void IOS() => Apply("iPhone", MaxSize);

        /// <summary>The platform name Unity's importer uses for a build target.</summary>
        public static string PlatformName(BuildTarget target) => target == BuildTarget.iOS ? "iPhone" : target == BuildTarget.Android ? "Android" : target == BuildTarget.WebGL ? "WebGL" : "Standalone";

        public static int ApplyFor(BuildTarget target) => Apply(PlatformName(target), MaxSize);

        /// <summary>A metallic, emissive or normal map (by import type or the packs' file names); never a mask.</summary>
        public static bool IsSecondaryMap(string path, TextureImporter imp)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (n.Contains("mask")) return false;
            return imp.textureType == TextureImporterType.NormalMap || n.Contains("metallic") || n.Contains("emiss") || n.Contains("emmis") || n.Contains("normal");
        }

        /// <summary>What a build can reach: the build scenes and everything in a Resources folder of ours.</summary>
        public static string[] Roots()
        {
            var roots = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) roots.Add(s.path);
            foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/_mine" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)) roots.Add(p);
            }
            return roots.Distinct().ToArray();
        }

        /// <summary>Every asset a build can reach (see <see cref="Roots"/>).</summary>
        public static string[] BuildDependencies() => AssetDatabase.GetDependencies(Roots(), true);

        /// <summary>Third-party assets the build reaches (outside <c>Assets/_mine</c>, inside <c>Assets</c>).</summary>
        public static List<string> VendorDependencies() =>
            BuildDependencies().Where(p => !p.StartsWith("Assets/_mine/") && p.StartsWith("Assets/")).Distinct().ToList();

        /// <summary>Caps textures for one platform. Returns how many importers changed.</summary>
        public static int Apply(string platform, int maxSize)
        {
            List<string> paths = VendorDependencies().Where(p => AssetImporter.GetAtPath(p) is TextureImporter).ToList();
            int changed = 0, skippedSprites = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < paths.Count; i++)
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
                    if (imp.textureType == TextureImporterType.Sprite) { skippedSprites++; continue; }
                    EditorUtility.DisplayProgressBar($"Texture caps ({platform})", paths[i], (float)i / paths.Count);
                    TextureImporterPlatformSettings s = imp.GetPlatformTextureSettings(platform);
                    int size = Mathf.Min(IsSecondaryMap(paths[i], imp) ? Mathf.Min(maxSize, SecondaryMax) : maxSize, imp.maxTextureSize);
                    if (s.overridden && s.maxTextureSize == size && s.textureCompression == TextureImporterCompression.Compressed && s.format == TextureImporterFormat.Automatic) continue;
                    s.overridden = true;
                    s.maxTextureSize = size;
                    s.format = TextureImporterFormat.Automatic; // the platform's default compressed format (DXT / ASTC / ETC2)
                    s.textureCompression = TextureImporterCompression.Compressed;
                    s.compressionQuality = 50;
                    imp.SetPlatformTextureSettings(s);
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            if (paths.Count > 0) Debug.Log($"[TextureCaps] {platform}, max {maxSize}, secondary maps {Mathf.Min(maxSize, SecondaryMax)}: {changed} changed of {paths.Count} third-party textures in the build ({skippedSprites} UI sprites left alone)");
            return changed;
        }
    }

    /// <summary>
    /// <b>Low</b> mesh compression on the third-party models a build can reach (the same roots as
    /// <see cref="TextureCaps"/>). It only shrinks the files on disk and in the download; meshes are decompressed when
    /// loaded, so memory and looks stay (Low keeps the vertex precision big modular buildings need; Medium risked
    /// seams). Writes the packs' model import settings (.meta), the same agreed exception. Re-runnable.
    /// </summary>
    public static class MeshCaps
    {
        public const ModelImporterMeshCompression Level = ModelImporterMeshCompression.Low;

        [MenuItem(Menus.Root + "Build/Compress Third-Party Meshes", false, 144)]
        public static int Apply()
        {
            List<string> paths = TextureCaps.VendorDependencies().Where(p => AssetImporter.GetAtPath(p) is ModelImporter mi && mi.meshCompression < Level).ToList();
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Mesh compression", paths[i], (float)i / paths.Count);
                    var mi = (ModelImporter)AssetImporter.GetAtPath(paths[i]);
                    mi.meshCompression = Level;
                    mi.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            if (paths.Count > 0) Debug.Log($"[MeshCaps] {Level}: {paths.Count} third-party models changed");
            return paths.Count;
        }
    }
}
#endif
