#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Phone and web exports: <c>Game > Build > …</c> iOS (an Xcode project; sign and run it on a Mac), Android (an
    /// .apk to sideload; <see cref="StoreBuilds"/> makes the signed .aab) and WebGL (a release build, Brotli with the
    /// decompression fallback, so it plays from any static host). The "(dev)" items make a development build for the
    /// profiler. Scenes come from Build Settings (the scene builder puts the main scene there). Output goes to
    /// <c>Builds/&lt;platform&gt;/</c> (git-ignored). Every export bumps the iOS build number or the Android version
    /// code and appends a line to <c>Builds/build_log.txt</c>; a web build also logs its download against the budget
    /// and writes <c>Builds/Checks/size_&lt;target&gt;.md</c>, the packed assets by kind and the 25 heaviest.
    /// <c>Serve Web Build on the LAN</c> starts a small web server on this PC so a phone on the same Wi-Fi can play it.
    /// </summary>
    public static class PhoneBuilds
    {
        private const string Root = "Builds";
        public static string BuildRoot => Root;
        private const int WebPort = 8000;
        private static string LogPath => Path.Combine(Root, "build_log.txt");

        /// <summary>The product name as a file name: letters and digits only.</summary>
        public static string SafeName
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in PlayerSettings.productName) if (char.IsLetterOrDigit(c)) sb.Append(c);
                return sb.Length > 0 ? sb.ToString() : "Game";
            }
        }
        public static string ApkName => SafeName + ".apk";
        public static string BundleName => SafeName + ".aab";

        /// <summary>The web build should stay under this for a quick load on a phone (the shipped games held 10–15 MB).</summary>
        public const float WebBudgetMB = 25f;

        [MenuItem(Menus.Root + "Build/WebGL (play on a phone's browser)", false, 100)]
        public static void WebGL() => Run(BuildTarget.WebGL);

        [MenuItem(Menus.Root + "Build/Android (apk)", false, 101)]
        public static void Android() => Run(BuildTarget.Android);

        [MenuItem(Menus.Root + "Build/iOS (Xcode project)", false, 102)]
        public static void IOS() => Run(BuildTarget.iOS);

        [MenuItem(Menus.Root + "Build/Android (dev, profiling)", false, 111)]
        public static void AndroidDev() => Run(BuildTarget.Android, true);

        [MenuItem(Menus.Root + "Build/iOS (dev, profiling)", false, 112)]
        public static void IOSDev() => Run(BuildTarget.iOS, true);

        [MenuItem(Menus.Root + "Build/All (WebGL, Android, iOS)", false, 120)]
        public static void All()
        {
            foreach (BuildTarget t in new[] { BuildTarget.WebGL, BuildTarget.Android, BuildTarget.iOS })
                if (!Run(t)) break;
        }

        /// <summary>For a batch-mode Unity (<c>tools/unity_batch.py build webgl</c>): the target from <c>-buildTarget</c>, then quit.</summary>
        public static void Batch()
        {
            bool ok = Run(EditorUserBuildSettings.activeBuildTarget);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <param name="bundle">Android only: an .aab for Google Play (<see cref="StoreBuilds"/> signs it) instead of the sideload .apk.</param>
        public static bool Run(BuildTarget target, bool dev = false, bool bundle = false)
        {
            if (EditorApplication.isPlaying) { Log(target, "FAILED: stop Play mode first"); return false; }
            Directory.CreateDirectory(Root);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Log(target, "FAILED: no scenes in Build Settings (run the scene builder)"); return false; }

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            NamedBuildTarget named = NamedBuildTarget.FromBuildTargetGroup(group);
            string path;
            switch (target)
            {
                case BuildTarget.iOS:
                    PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
                    PlayerSettings.iOS.buildNumber = (ParseInt(PlayerSettings.iOS.buildNumber) + 1).ToString();
                    path = Path.Combine(Root, "iOS");
                    break;
                case BuildTarget.Android:
                    PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    PlayerSettings.Android.bundleVersionCode++;
                    EditorUserBuildSettings.buildAppBundle = bundle;
                    path = Path.Combine(Root, "Android", bundle ? BundleName : ApkName);
                    break;
                case BuildTarget.WebGL:
                    WebSize.ApplyWebPlayer(); // Brotli with the fallback, wasm built for size, the page template
                    PlayerSettings.SetManagedStrippingLevel(named, ManagedStrippingLevel.High);
                    PlayerSettings.SetIl2CppCodeGeneration(named, Il2CppCodeGeneration.OptimizeSize);
                    path = Path.Combine(Root, "WebGL");
                    break;
                default:
                    path = Path.Combine(Root, target.ToString());
                    break;
            }
            WebSize.ApplyTextures();        // our own art's caps, every platform
            TextureCaps.ApplyFor(target);   // third-party textures the build reaches, for this platform
            MeshCaps.Apply();               // and their meshes
            AssetDatabase.SaveAssets();
            // the music, packed per platform into StreamingAssets: bundles are built for one target at a time
            if (!MusicBundles.Build(target)) { Log(target, "FAILED: the music bundles did not build"); return false; }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                targetGroup = group,
                locationPathName = path,
                // WebGL is always release: a development one skips compression and ships a large debug wasm
                options = dev && target != BuildTarget.WebGL ? BuildOptions.Development : BuildOptions.None,
            };
            Log(target, $"started {((options.options & BuildOptions.Development) != 0 ? "dev" : "release")} " +
                        $"(version {PlayerSettings.bundleVersion}, iOS build {PlayerSettings.iOS.buildNumber}, Android code {PlayerSettings.Android.bundleVersionCode})");

            DateTime t0 = DateTime.Now;
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            catch (Exception e) { Log(target, "FAILED: " + e.Message); return false; }

            BuildSummary sum = report.summary;
            string size = sum.totalSize > 0 ? $"{sum.totalSize / 1e6:0.0} MB" : "";
            Log(target, $"{sum.result} in {(DateTime.Now - t0).TotalMinutes:0.0} min, {sum.totalErrors} errors, {sum.totalWarnings} warnings, {size} -> {Path.GetFullPath(path)}");
            if (sum.result == BuildResult.Succeeded)
            {
                Breakdown(target, report);
                if (target == BuildTarget.WebGL) CheckWebBudget(path);
                if (target == BuildTarget.Android && !bundle) OfferApk(path);
                if (target == BuildTarget.Android && bundle && File.Exists(path)) Log(target, $"aab {new FileInfo(path).Length / 1e6:0.0} MB");
            }
            else
            {
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) Log(target, "  " + m.content.Split('\n')[0]);
            }
            return sum.result == BuildResult.Succeeded;
        }

        /// <summary>
        /// Logs the .apk's real size (the report's total counts far more) and puts a copy beside the web build, so the
        /// LAN server offers it too: open <c>http://&lt;this PC&gt;:8000/&lt;Product&gt;.apk</c> on the phone to install it
        /// (Android asks once to allow installs from the browser).
        /// </summary>
        private static void OfferApk(string apk)
        {
            if (!File.Exists(apk)) return;
            Log(BuildTarget.Android, $"apk {new FileInfo(apk).Length / 1e6:0.0} MB");
            string web = Path.Combine(Root, "WebGL");
            if (!Directory.Exists(web)) return;
            File.Copy(apk, Path.Combine(web, ApkName), true);
            Log(BuildTarget.Android, $"copied beside the web build: the LAN server offers it at /{ApkName}");
        }

        // ---------------------------------------------------------------- the LAN web server

        private static System.Diagnostics.Process server;

        /// <summary>
        /// Serves <c>Builds/WebGL</c> with Python's built-in web server on port 8000 and logs the address a phone on
        /// the same Wi-Fi opens. Windows may ask once to let Python through the firewall: allow private networks.
        /// </summary>
        [MenuItem(Menus.Root + "Build/Serve Web Build on the LAN", false, 130)]
        public static void Serve()
        {
            string folder = Path.GetFullPath(Path.Combine(Root, "WebGL"));
            if (!File.Exists(Path.Combine(folder, "index.html"))) { Debug.LogError("[PhoneBuilds] No web build yet: Build > WebGL first."); return; }
            StopServer();
            try
            {
                server = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m http.server {WebPort} --bind 0.0.0.0",
                    WorkingDirectory = folder,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch (Exception e) { Debug.LogError("[PhoneBuilds] Could not start Python's web server (is Python on the PATH?): " + e.Message); return; }
            string urls = string.Join("  or  ", LanAddresses().Select(a => $"http://{a}:{WebPort}"));
            Log(BuildTarget.WebGL, $"serving {folder} at {(urls.Length > 0 ? urls : $"http://localhost:{WebPort}")} (Build > Stop Web Server to stop)");
        }

        [MenuItem(Menus.Root + "Build/Stop Web Server", false, 131)]
        public static void StopServer()
        {
            if (server == null) return;
            try { if (!server.HasExited) server.Kill(); } catch { }
            server = null;
            Debug.Log("[PhoneBuilds] Web server stopped.");
        }

        /// <summary>This PC's IPv4 addresses on its live networks (what a phone on the same Wi-Fi can reach).</summary>
        public static List<string> LanAddresses()
        {
            var list = new List<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.GetIPProperties().GatewayAddresses.Count == 0) continue; // virtual adapters have no gateway
                foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                    if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)) list.Add(a.Address.ToString());
            }
            return list;
        }

        // ---------------------------------------------------------------- the log

        /// <summary>Logs what a player downloads (the Build folder, as served) against the budget.</summary>
        private static void CheckWebBudget(string folder)
        {
            long bytes = 0;
            string build = Path.Combine(folder, "Build");
            if (Directory.Exists(build)) foreach (string f in Directory.GetFiles(build)) bytes += new FileInfo(f).Length;
            float mb = bytes / 1e6f;
            Log(BuildTarget.WebGL, $"download {mb:0.0} MB of the {WebBudgetMB:0} MB budget{(mb > WebBudgetMB ? "  ** OVER BUDGET: see Builds/Checks/size_webgl.md for the heaviest assets **" : "")}");
        }

        /// <summary>What the build's assets weigh once packed: the game's own folder against everything else (the vendor packs).</summary>
        private static void Breakdown(BuildTarget target, BuildReport report)
        {
            ulong total = 0, mine = 0;
            foreach (PackedAssets pa in report.packedAssets)
                foreach (PackedAssetInfo c in pa.contents)
                {
                    total += c.packedSize;
                    if (c.sourceAssetPath != null && c.sourceAssetPath.StartsWith("Assets/Game/")) mine += c.packedSize;
                }
            Log(target, $"packed assets {total / 1e6:0.0} MB: the game's own {mine / 1e6:0.00} MB, vendor and built-in {(total - mine) / 1e6:0.00} MB");
            WriteSizeReport(target, report, total);
        }

        /// <summary>
        /// <c>Builds/Checks/size_&lt;target&gt;.md</c>: the build's packed assets by kind (textures, audio, meshes...) and
        /// the 25 heaviest, so the next cut is picked by size, not guessed.
        /// </summary>
        private static void WriteSizeReport(BuildTarget target, BuildReport report, ulong total)
        {
            var bySource = new Dictionary<string, ulong>();
            var byType = new Dictionary<string, ulong>();
            foreach (PackedAssets pa in report.packedAssets)
                foreach (PackedAssetInfo c in pa.contents)
                {
                    string src = string.IsNullOrEmpty(c.sourceAssetPath) ? "(built in)" : c.sourceAssetPath;
                    bySource.TryGetValue(src, out ulong s); bySource[src] = s + c.packedSize;
                    string type = c.type != null ? c.type.Name : "?";
                    byType.TryGetValue(type, out ulong t); byType[type] = t + c.packedSize;
                }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {target} build size, {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"Packed assets {total / 1e6:0.0} MB (before the download's compression).");
            sb.AppendLine();
            sb.AppendLine("| Kind | MB |");
            sb.AppendLine("|---|---|");
            var types = new List<KeyValuePair<string, ulong>>(byType);
            types.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in types) if (kv.Value > 20000) sb.AppendLine($"| {kv.Key} | {kv.Value / 1e6:0.00} |");
            sb.AppendLine();
            sb.AppendLine("| Heaviest assets | MB |");
            sb.AppendLine("|---|---|");
            var srcs = new List<KeyValuePair<string, ulong>>(bySource);
            srcs.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < srcs.Count && i < 25; i++) sb.AppendLine($"| `{srcs[i].Key}` | {srcs[i].Value / 1e6:0.00} |");
            try
            {
                Directory.CreateDirectory("Builds/Checks");
                File.WriteAllText($"Builds/Checks/size_{target.ToString().ToLowerInvariant()}.md", sb.ToString());
            }
            catch { }
        }

        private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;

        private static void Log(BuildTarget target, string line)
        {
            string msg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {target}: {line}";
            Debug.Log("[PhoneBuilds] " + msg);
            try { File.AppendAllText(LogPath, msg + Environment.NewLine); } catch { }
        }
    }
}
#endif
