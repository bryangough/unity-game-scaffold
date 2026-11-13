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
    /// <para><b>The web build packs small MP3s instead of clips, when an encoder is installed (optional).</b> Unity re-encodes all web audio to ~128 kbps AAC
    /// whatever the import settings say, so for WebGL each stem is encoded by <c>tools/web_music.py</c> (ffmpeg or the
    /// Python soundfile package) as a 40 kbps mono MP3 into <see cref="WebDir"/> (git-ignored, re-encoded only when the
    /// source is newer), packed as .bytes data with a <c>length</c> file holding the source's exact length: a decoded
    /// MP3's length is padded by the encoder, and a loop timed from it drifts. <see cref="Sfx"/> decodes them in the
    /// browser. The jelly game's web download went 24.8 → 12.0 MB. <b>Neither ffmpeg nor soundfile is required:</b> with
    /// no python, no encoder, a missing script or a stem that fails, the web build packs Unity's clips as the phones do
    /// (bigger, nothing else changes) and logs one warning saying why; it never fails the build. Not yet tried end to end
    /// on a web build with real music.</para>
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
            if (target == BuildTarget.WebGL)
            {
                string why;
                try { why = EncodeForWeb(builds); }
                catch (System.Exception e) { why = e.Message; } // never fails the build: the clips are the fallback
                if (why != null) Debug.LogWarning($"[Music] web music not encoded ({why}): the web build packs Unity's clips instead, " +
                    "~128 kbps AAC, about three times the size. Optional: put ffmpeg on PATH or pip install soundfile.");
            }

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

        public const string EncoderScript = "tools/web_music.py";
        private const int EncodeTimeoutMs = 120000;

        /// <summary>Swaps each build's clips for encoded MP3 .bytes and a length file. Null on success; otherwise why
        /// not, with <paramref name="builds"/> untouched (no python, no encoder, a stem that failed).</summary>
        private static string EncodeForWeb(List<AssetBundleBuild> builds)
        {
            if (!File.Exists(EncoderScript)) return EncoderScript + " is missing";
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
                        if (File.Exists(dst)) File.Delete(dst); // never pack a half-written or stale stem
                        string why = Python(EncoderScript, Path.GetFullPath(src), Path.GetFullPath(dst));
                        if (why != null) return why;
                        if (!File.Exists(dst) || new FileInfo(dst).Length == 0) return $"{Path.GetFileName(src)} came out empty";
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
            return null;
        }

        /// <summary>Runs a script with python (or python3): null when it exits 0, otherwise why not.</summary>
        private static string Python(string script, params string[] args)
        {
            string line = "\"" + script + "\" " + string.Join(" ", System.Array.ConvertAll(args, a => "\"" + a + "\""));
            string last = "no python on PATH";
            foreach (string exe in new[] { "python", "python3" })
            {
                System.Diagnostics.Process p;
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(exe, line)
                    {
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                        WorkingDirectory = Path.GetFullPath("."),
                    };
                    p = System.Diagnostics.Process.Start(psi);
                }
                catch (System.Exception) { continue; } // not on PATH: try the next name
                if (p == null) continue;
                using (p)
                {
                    var err = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(EncodeTimeoutMs)) { try { p.Kill(); } catch (System.Exception) { } return "the encoder took over 2 minutes on one stem"; }
                    if (p.ExitCode == 0) return null;
                    string msg = err.Wait(1000) ? err.Result.Trim() : "";
                    // 2 is web_music.py's "no ffmpeg and no soundfile"; 9009 is Windows' python alias with no Python installed
                    if (p.ExitCode == 9009) { last = "no python installed"; continue; }
                    if (p.ExitCode == 2 && msg.StartsWith("no encoder")) return "no ffmpeg on PATH and no soundfile package";
                    int nl = msg.LastIndexOf('\n');
                    return $"{script} exited {p.ExitCode}: {(nl >= 0 ? msg.Substring(nl + 1) : msg)}";
                }
            }
            return last;
        }
    }
}
#endif
