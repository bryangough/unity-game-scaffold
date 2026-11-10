#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The music, packed. Each track's stems (<c>Audio/Music/MusicN/</c>) go into one AssetBundle,
    /// <c>Assets/StreamingAssets/Music/musicN</c>, LZ4 compressed so the clips stream from it. The bank never references
    /// the stems, so they are out of the build's data (a track is ~3 MB of a web download, fetched only when first
    /// played), and <see cref="Sfx"/> loads a bundle when its track is first wanted. A bundle is Unity's packed format,
    /// not a playable audio file, so licensed tracks never sit on a web server as loose music. Bundles are built per
    /// platform, so <see cref="PhoneBuilds"/> rebuilds them before every player build. The folder is git-ignored.
    /// </summary>
    public static class MusicBundles
    {
        public const string OutDir = "Assets/StreamingAssets/Music";

        [MenuItem(Menus.Root + "Audio/Build Music Bundles (current platform)", false, 62)]
        public static void BuildForActive() => Build(EditorUserBuildSettings.activeBuildTarget);

        /// <summary>Packs every track under <see cref="Sfx.MusicFolder"/> into its own bundle for <paramref name="target"/>.</summary>
        public static bool Build(BuildTarget target)
        {
            var builds = new List<AssetBundleBuild>();
            if (Directory.Exists(Sfx.MusicFolder))
                foreach (string dir in Directory.GetDirectories(Sfx.MusicFolder))
                {
                    var files = new List<string>();
                    foreach (string f in Directory.GetFiles(dir))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".wav" || ext == ".ogg" || ext == ".mp3") files.Add(f.Replace('\\', '/'));
                    }
                    if (files.Count == 0) continue;
                    files.Sort(System.StringComparer.Ordinal);
                    builds.Add(new AssetBundleBuild { assetBundleName = Path.GetFileName(dir).ToLowerInvariant(), assetNames = files.ToArray() });
                }

            // a clean folder each time: the bundles are for one platform
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            if (File.Exists(OutDir + ".meta")) File.Delete(OutDir + ".meta");
            Directory.CreateDirectory(OutDir);
            if (builds.Count == 0) { Debug.LogWarning("[Music] no tracks to pack"); return false; }

            AssetBundleManifest m = BuildPipeline.BuildAssetBundles(OutDir, builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, target);
            if (m == null) { Debug.LogError($"[Music] bundles failed for {target}"); return false; }

            // only the bundles ship: drop the manifests and the folder's own manifest bundle
            foreach (string f in Directory.GetFiles(OutDir))
            {
                string name = Path.GetFileName(f);
                if (name.EndsWith(".manifest") || name == "Music" || name == "Music.meta" || name.EndsWith(".manifest.meta")) File.Delete(f);
            }
            AssetDatabase.Refresh();
            long bytes = 0;
            foreach (AssetBundleBuild b in builds) { var fi = new FileInfo(Path.Combine(OutDir, b.assetBundleName)); if (fi.Exists) bytes += fi.Length; }
            Debug.Log($"[Music] {builds.Count} tracks packed for {target}: {bytes / 1e6:0.0} MB in {OutDir}");
            return true;
        }
    }
}
#endif
