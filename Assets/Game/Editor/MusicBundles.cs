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
    /// <para><b>The web build packs small MP3s instead of clips.</b> Unity re-encodes all web audio to ~128 kbps AAC
    /// whatever the import settings say, so for WebGL each stem is encoded by <c>tools/web_music.py</c> (ffmpeg or the
    /// Python soundfile package) as a 40 kbps mono MP3 into <see cref="WebDir"/> (git-ignored, re-encoded only when the
    /// source is newer), packed as .bytes data with a <c>length</c> file holding the source's exact length: a decoded
    /// MP3's length is padded by the encoder, and a loop timed from it drifts. <see cref="Sfx"/> decodes them in the
    /// browser. The jelly game's web download went 24.8 → 12.0 MB. With no encoder installed the clips are packed as
    /// before, with a warning.</para>
    /// </summary>
    public static class MusicBundles
    {
        public const string OutDir = "Assets/StreamingAssets/Music";
        /// <summary>The web build's encoded stems, one folder per track (generated, git-ignored).</summary>
        public const string WebDir = "Assets/Game/Audio/WebMusic";

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
            if (builds.Count == 0) { Debug.Log("[Music] no tracks to pack"); AssetDatabase.Refresh(); return true; } // a game without music yet still builds
            if (target == BuildTarget.WebGL && !EncodeForWeb(builds)) Debug.LogWarning("[Music] no MP3 encoder (ffmpeg, or pip install soundfile): " +
                "the web build packs Unity's clips, ~128 kbps AAC, about three times the size");

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

        /// <summary>Swaps each build's clips for encoded MP3 .bytes and a length file; false (builds untouched) when there is no encoder.</summary>
        private static bool EncodeForWeb(List<AssetBundleBuild> builds)
        {
            var encoded = new List<AssetBundleBuild>();
            foreach (AssetBundleBuild b in builds)
            {
                string track = Path.GetFileName(Path.GetDirectoryName(b.assetNames[0]));
                string dir = $"{WebDir}/{track}";
                Directory.CreateDirectory(dir);
                var names = new List<string>();
                foreach (string src in b.assetNames)
                {
                    string dst = $"{dir}/{Path.GetFileNameWithoutExtension(src)}.bytes";
                    if (!File.Exists(dst) || File.GetLastWriteTimeUtc(dst) < File.GetLastWriteTimeUtc(src))
                    {
                        int code = Python("tools/web_music.py", Path.GetFullPath(src), Path.GetFullPath(dst));
                        if (code == 2) return false;
                        if (code != 0 || !File.Exists(dst)) { Debug.LogError($"[Music] encoding {src} failed ({code})"); return false; }
                    }
                    names.Add(dst);
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(b.assetNames[0]);
                string lengthPath = $"{dir}/length.txt";
                File.WriteAllText(lengthPath, clip != null ? clip.length.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) : "0");
                names.Add(lengthPath);
                encoded.Add(new AssetBundleBuild { assetBundleName = b.assetBundleName, assetNames = names.ToArray() });
            }
            AssetDatabase.Refresh(); // import the .bytes and length files before they are packed
            builds.Clear();
            builds.AddRange(encoded);
            return true;
        }

        /// <summary>Runs a script in tools/ with python (or python3); the exit code, or 2 when there is no python.</summary>
        private static int Python(string script, params string[] args)
        {
            string line = "\"" + script + "\" " + string.Join(" ", System.Array.ConvertAll(args, a => "\"" + a + "\""));
            foreach (string exe in new[] { "python", "python3" })
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(exe, line)
                    {
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                        WorkingDirectory = Path.GetFullPath("."),
                    };
                    using (var p = System.Diagnostics.Process.Start(psi))
                    {
                        string err = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                        if (p.ExitCode != 0 && p.ExitCode != 2) Debug.LogWarning($"[Music] {script}: {err}");
                        return p.ExitCode;
                    }
                }
                catch (System.ComponentModel.Win32Exception) { } // not on PATH: try the next name
            }
            return 2;
        }
    }
}
#endif
