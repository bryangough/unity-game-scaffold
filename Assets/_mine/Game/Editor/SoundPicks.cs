#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The sound pipeline. Sound libraries live OUTSIDE the project under one root (set per machine with
    /// <c>Game > Audio > Set Sound Library Folder</c>; pick paths are relative to it). Only the picks below are copied
    /// in, to <c>Assets/_mine/Game/Audio/SFX/&lt;Id&gt;/</c>, where <see cref="SfxImportRules"/> gives them mobile
    /// import settings; <c>Import Sound Picks</c> copies what is missing, removes files no longer picked and rebuilds
    /// <c>Resources/SoundBank.asset</c>. A machine without the library keeps working from the copies in git. To
    /// change a sound: edit the list, import, commit. Give every id two to four variants, so the owner hears the
    /// candidates in play and strikes the ones they dislike.
    /// <para>Licences: a sound that ships must be licensed for games (an Asset Store EULA, CC0, a royalty-free pack);
    /// write each source and its licence in <c>Store/licences.md</c>, and never redistribute a library's raw files
    /// (the repo stays private while bought packs are in it; the scaffold itself ships none).</para>
    /// </summary>
    public static class SoundPicks
    {
        public const string SfxRoot = "Assets/_mine/Game/Audio/SFX";
        public static string MusicRoot => Sfx.MusicFolder;
        public const string BankPath = "Assets/_mine/Game/Resources/SoundBank.asset";
        private static readonly string LibraryPref = GameInfo.Id + ".soundRoot";
        private const string DefaultLibrary = "D:/GameDev/Sound";

        private struct Pick
        {
            public string id; public float volume; public string[] files; public bool loop; public float tail;
            public Pick(string id, float volume, params string[] files) { this.id = id; this.volume = volume; this.files = files; loop = false; tail = 0f; }
            public Pick Loop() { loop = true; return this; }
            public Pick Tail(float seconds) { tail = seconds; return this; }
        }

        /// <summary>Music files are renamed on the way in (nothing that ships carries a source name): Music1_0.wav, Music1_1.wav.</summary>
        private static string DestName(Pick p, string root, int index, string rel) =>
            root == MusicRoot ? $"{p.id}_{index}{Path.GetExtension(rel)}" : Path.GetFileName(rel);

        private static string[] Range(string pattern, int from, int to)
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++) list.Add(string.Format(pattern, i));
            return list.ToArray();
        }

        private static string[] P(string prefix, params string[] files)
        {
            var list = new string[files.Length];
            for (int i = 0; i < files.Length; i++) list[i] = prefix + files[i];
            return list;
        }

        // Libraries, relative to the sound root. Examples from the shipped games (not included here):
        //   private const string USFX = "_SoundLibraries/Universal Sound FX/";    // Imphenzia, Asset Store EULA
        //   private const string KENNEY = "../Art/Kenney Game Assets All-in-1 3.8.0/Audio/Interface Sounds/Audio/"; // CC0

        // ---------------------------------------------------------------- the picks (ids match Sfx.Id)
        private static readonly Pick[] Picks =
        {
            // Add one line per Sfx.Id, two to four variants each. Examples:
            // new Pick("Click", 0.6f, P(KENNEY, "click_001.ogg", "click_002.ogg", "click_003.ogg")),
            // new Pick("Back", 0.6f, P(KENNEY, "back_001.ogg", "back_002.ogg")),
            // new Pick("Win", 0.8f, P(USFX, "PUZZLES/PUZZLE_Success_Harp_Three_Note_Bright_Wet_stereo.wav")),
            // new Pick("Step", 0.5f, P(USFX, Range("HUMAN/Footsteps/_Metal_Footsteps/FOOTSTEP_Metal_Sneak_01_RR{0:00}_mono.wav", 1, 4))),
            // new Pick("Engine", 0.4f, P(USFX, "VEHICLES/ENGINE_Idle_loop_mono.wav")).Loop(),   // driven by Sfx.Loop
        };

        // ---------------------------------------------------------------- music (Audio/Music/<Id>/)
        // Two or three stems of the same length per track, calm to full, mixed by Sfx.MusicIntensity. Tail = the
        // reverb tail in seconds (so the loop point rings over). Tracks are packed into AssetBundles by MusicBundles.
        private static readonly Pick[] MusicPicks =
        {
            // new Pick("Music1", 0.4f, P("ovanisound/Some Pack/Track (RT 4.1)/", "Track Intensity 1.wav", "Track Main.wav")).Tail(4.1f),
        };

        // ---------------------------------------------------------------- menu

        public static string LibraryFolder
        {
            get => EditorPrefs.GetString(LibraryPref, DefaultLibrary);
            set => EditorPrefs.SetString(LibraryPref, value);
        }

        [MenuItem(Menus.Root + "Audio/Set Sound Library Folder...", false, 40)]
        private static void SetLibrary()
        {
            string picked = EditorUtility.OpenFolderPanel("Sound root (the folder holding your sound libraries)", LibraryFolder, "");
            if (!string.IsNullOrEmpty(picked)) { LibraryFolder = picked; Debug.Log("[SoundPicks] library: " + picked); }
        }

        [MenuItem(Menus.Root + "Audio/Import Sound Picks", false, 41)]
        public static void Import()
        {
            string lib = LibraryFolder;
            bool haveLib = Directory.Exists(lib);
            if (!haveLib && (Picks.Length > 0 || MusicPicks.Length > 0))
                Debug.LogWarning($"[SoundPicks] library not found at {lib}: keeping the copies already in {SfxRoot} (Audio > Set Sound Library Folder to fix)");
            var log = new StringBuilder();
            int copied = 0, kept = 0, missing = 0, removed = 0;
            var wanted = new HashSet<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach ((Pick p, string root) in All())
                {
                    string dir = $"{root}/{p.id}";
                    Directory.CreateDirectory(dir);
                    for (int k = 0; k < p.files.Length; k++)
                    {
                        string rel = p.files[k];
                        string dst = $"{dir}/{DestName(p, root, k, rel)}";
                        wanted.Add(dst);
                        if (File.Exists(dst)) { kept++; continue; }
                        string src = Path.GetFullPath(Path.Combine(lib, rel));
                        if (haveLib && File.Exists(src))
                        {
                            // music is shrunk on the way in (22 kHz mono 16-bit), so git holds megabytes, not hundreds
                            if (root == MusicRoot && dst.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)) WavDownsample.Convert(src, dst);
                            else File.Copy(src, dst);
                            copied++;
                        }
                        else { missing++; log.AppendLine($"  MISSING {p.id}: {rel}"); }
                    }
                }
                foreach (string root in new[] { SfxRoot, MusicRoot })
                    foreach (string f in AudioFiles(root))
                    {
                        string a = f.Replace('\\', '/');
                        if (wanted.Contains(a)) continue;
                        AssetDatabase.DeleteAsset(a);
                        removed++;
                        log.AppendLine($"  removed {a}");
                    }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            BuildBank();
            Debug.Log($"[SoundPicks] {copied} copied, {kept} already in, {removed} removed, {missing} missing\n{log}");
            Report();
        }

        private static IEnumerable<(Pick, string)> All()
        {
            foreach (Pick p in Picks) yield return (p, SfxRoot);
            foreach (Pick p in MusicPicks) yield return (p, MusicRoot);
        }

        private static IEnumerable<string> AudioFiles(string root)
        {
            if (!Directory.Exists(root)) yield break;
            foreach (string f in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string e = Path.GetExtension(f).ToLowerInvariant();
                if (e == ".wav" || e == ".mp3" || e == ".ogg" || e == ".aif" || e == ".aiff" || e == ".flac") yield return f;
            }
        }

        private static void BuildBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            var entries = new List<SoundBank.Entry>();
            foreach ((Pick p, string root) in All())
            {
                var clips = new List<AudioClip>();
                for (int k = 0; k < p.files.Length; k++)
                {
                    var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{root}/{p.id}/{DestName(p, root, k, p.files[k])}");
                    if (c != null) clips.Add(c);
                }
                // Music is not referenced from the bank (so it stays out of the build's data): MusicBundles packs each
                // track into an AssetBundle under StreamingAssets, and Sfx loads it when the track first plays
                bool streamed = root == MusicRoot;
                entries.Add(new SoundBank.Entry { id = p.id, clips = streamed ? new AudioClip[0] : clips.ToArray(), volume = p.volume, pitchJitter = p.loop || streamed ? 0f : 0.06f, loop = p.loop, tail = p.tail, streamed = streamed });
            }
            bank.entries = entries.ToArray();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
        }

        /// <summary>What the sounds cost: source size in git, and an estimate of the phone build.</summary>
        [MenuItem(Menus.Root + "Audio/Sound Report", false, 42)]
        public static void Report()
        {
            long source = 0; double seconds = 0, build = 0; int n = 0;
            var sb = new StringBuilder();
            foreach (string root in new[] { SfxRoot, MusicRoot })
                foreach (string f in AudioFiles(root))
                {
                    string a = f.Replace('\\', '/');
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(a);
                    var imp = AssetImporter.GetAtPath(a) as AudioImporter;
                    if (clip == null || imp == null) continue;
                    n++;
                    source += new FileInfo(f).Length;
                    seconds += clip.length;
                    var s = imp.defaultSampleSettings;
                    double kbps = root == MusicRoot ? 5 : s.compressionFormat == AudioCompressionFormat.Vorbis ? 6 : s.compressionFormat == AudioCompressionFormat.ADPCM ? 11 : 44;
                    build += clip.length * kbps;
                    if (clip.length > 4f || root == MusicRoot) sb.AppendLine($"  long: {a} {clip.length:0.0} s ({s.loadType}, {s.compressionFormat})");
                }
            string report = $"[SoundPicks] {n} clips, {seconds:0} s of sound, {source / 1048576.0:0.0} MB of source audio in git, ~{build / 1024.0:0.0} MB in a phone build\n{sb}";
            Debug.Log(report);
            Directory.CreateDirectory("Builds/Checks");
            File.WriteAllText("Builds/Checks/sound.md", "# Sound report\n\n" + report);
        }
    }
}
#endif
