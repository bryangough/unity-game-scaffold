#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The store builds. <c>Game > Build > Store</c>:
    /// <list type="bullet">
    /// <item><b>Make Upload Key</b>: a Google Play upload keystore, outside the repo, in <see cref="SigningDir"/>
    /// (<c>upload.keystore</c> and <c>signing.txt</c> with its passwords). Back that folder up: Play signs the app
    /// with its own key (Play App Signing), and a lost upload key means asking Play support for a reset.</item>
    /// <item><b>Google Play bundle (aab, signed)</b>: <c>Builds/Android/&lt;Product&gt;.aab</c>, signed with that key.
    /// The key settings are put back afterwards, so ProjectSettings never holds the keystore path or a password.</item>
    /// <item><b>Zip Web Build for itch.io</b>: <c>Builds/Store/&lt;Product&gt;_web_&lt;version&gt;.zip</c>, the web build
    /// with <c>index.html</c> at the top (what itch.io wants), without the sideload apk the LAN server offers.</item>
    /// </list>
    /// </summary>
    public static class StoreBuilds
    {
        /// <summary>Outside the repo, per user: <c>%USERPROFILE%\.keys\&lt;game id&gt;</c>.</summary>
        public static string SigningDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".keys", GameInfo.Id);
        private const string KeystoreFile = "upload.keystore";
        private const string SigningFile = "signing.txt";
        private const string Alias = "upload";
        public static string StoreDir => Path.Combine(PhoneBuilds.BuildRoot, "Store");

        [MenuItem(Menus.Root + "Build/Store/Make Upload Key (once)", false, 150)]
        public static void MakeUploadKeyMenu() => MakeUploadKey(SigningDir);

        [MenuItem(Menus.Root + "Build/Store/Google Play bundle (aab, signed)", false, 151)]
        public static void PlayBundleMenu() => PlayBundle(SigningDir);

        [MenuItem(Menus.Root + "Build/Store/Zip Web Build for itch.io", false, 152)]
        public static void ZipWebMenu() => ZipWeb();

        /// <summary>Creates the upload keystore with a random password; refuses when one is already there.</summary>
        public static bool MakeUploadKey(string dir)
        {
            string keystore = Path.Combine(dir, KeystoreFile);
            if (File.Exists(keystore)) { Debug.LogWarning($"[Store] An upload key already exists at {keystore}; not replacing it."); return false; }
            string keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool.exe");
            if (!File.Exists(keytool)) { Debug.LogError("[Store] keytool not found: install the Android module's OpenJDK."); return false; }
            Directory.CreateDirectory(dir);
            string pass = RandomPassword(24);
            var psi = new System.Diagnostics.ProcessStartInfo(keytool,
                $"-genkeypair -keystore \"{keystore}\" -alias {Alias} -keyalg RSA -keysize 2048 -validity 10000 " +
                $"-storepass {pass} -keypass {pass} -dname \"CN={PlayerSettings.companyName.Replace(',', ' ')}, O={PlayerSettings.companyName.Replace(',', ' ')}\"")
            { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0 || !File.Exists(keystore)) { Debug.LogError("[Store] keytool failed: " + err); return false; }
            }
            File.WriteAllText(Path.Combine(dir, SigningFile),
                $"# {PlayerSettings.productName}: Google Play upload key. Keep this folder backed up and out of the repo.\n" +
                $"keystore={KeystoreFile}\nalias={Alias}\nstorepass={pass}\nkeypass={pass}\n");
            Debug.Log($"[Store] Upload key made in {dir}. Back this folder up (a password manager or a USB stick).");
            return true;
        }

        /// <summary>Builds the signed .aab with the key in <paramref name="dir"/>.</summary>
        public static bool PlayBundle(string dir)
        {
            Dictionary<string, string> cfg = ReadSigning(dir);
            if (cfg == null) { Debug.LogError($"[Store] No upload key in {dir}: run Game > Build > Store > Make Upload Key first."); return false; }
            bool oldCustom = PlayerSettings.Android.useCustomKeystore;
            string oldName = PlayerSettings.Android.keystoreName, oldAlias = PlayerSettings.Android.keyaliasName;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(Path.Combine(dir, cfg["keystore"]));
                PlayerSettings.Android.keystorePass = cfg["storepass"];
                PlayerSettings.Android.keyaliasName = cfg["alias"];
                PlayerSettings.Android.keyaliasPass = cfg["keypass"];
                return PhoneBuilds.Run(BuildTarget.Android, false, true);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = oldCustom;
                PlayerSettings.Android.keystoreName = oldName;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Zips <c>Builds/WebGL</c> for itch.io; returns the zip's path, or null.</summary>
        public static string ZipWeb()
        {
            string web = Path.Combine(PhoneBuilds.BuildRoot, "WebGL");
            if (!File.Exists(Path.Combine(web, "index.html"))) { Debug.LogError("[Store] No web build yet: Game > Build > WebGL first."); return null; }
            Directory.CreateDirectory(StoreDir);
            string zip = Path.Combine(StoreDir, $"{PhoneBuilds.SafeName}_web_{PlayerSettings.bundleVersion}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            int files = 0;
            using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                foreach (string f in Directory.GetFiles(web, "*", SearchOption.AllDirectories))
                {
                    if (f.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) continue; // the LAN server's sideload copy
                    string rel = f.Substring(web.Length + 1).Replace('\\', '/');
                    archive.CreateEntryFromFile(f, rel, System.IO.Compression.CompressionLevel.Optimal);
                    files++;
                }
            Debug.Log($"[Store] {files} files, {new FileInfo(zip).Length / 1e6:0.0} MB -> {Path.GetFullPath(zip)} " +
                      "(itch.io: kind HTML, 'This file will be played in the browser', viewport 450 x 800, mobile friendly, portrait)");
            return zip;
        }

        private static Dictionary<string, string> ReadSigning(string dir)
        {
            string file = Path.Combine(dir, SigningFile);
            if (!File.Exists(file)) return null;
            var cfg = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq > 0) cfg[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            foreach (string k in new[] { "keystore", "alias", "storepass", "keypass" })
                if (!cfg.ContainsKey(k)) { Debug.LogError($"[Store] {file} has no '{k}='"); return null; }
            return cfg;
        }

        private static string RandomPassword(int n)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var bytes = new byte[n];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new System.Text.StringBuilder(n);
            foreach (byte b in bytes) sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }
    }
}
#endif
