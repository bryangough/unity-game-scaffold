using System.Collections.Generic;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// Sound effects and music. <c>Sfx.Play(Sfx.Id.Click)</c> from anywhere plays a random variant of that sound from
    /// the <see cref="SoundBank"/> in <c>Resources/SoundBank</c> (the picks of <c>Editor/SoundPicks.cs</c>); an id with
    /// no entry is silent, so the game runs without any sound imported. Add the game's sounds to <see cref="Id"/>
    /// (the bank matches by name). A small pool of voices survives scene reloads.
    /// <para><see cref="Loop(Id, float)"/> every frame drives a looping sound's volume by a 0..1 level (an engine by
    /// speed, a heartbeat by danger); it fades out by itself when the calls stop.</para>
    /// <para>Music: a track is stems of the same length (calm to full), played in lockstep and mixed by
    /// <see cref="MusicIntensity"/>; two voices per stem alternate so a track with a reverb tail loops seamlessly.
    /// Tracks are packed per platform into AssetBundles (<c>Editor/MusicBundles.cs</c>) and loaded when first
    /// wanted, so they are not in the first web download and never sit on a server as loose audio files. On the web a
    /// bundle holds small MP3s as data instead (<c>MusicBundles</c>), decoded here by the browser
    /// (<c>Plugins/WebGL/WebMusic.jslib</c>) and timed by the source's exact length.</para>
    /// The SOUND setting (<see cref="Progress.Sound"/>) is the master switch through <see cref="AudioListener.volume"/>;
    /// MUSIC (<see cref="Progress.Music"/>) mutes the stems; <see cref="Ducked"/> drops the music under a menu.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        /// <summary>The game's sounds. Keep these; add your own below them (one folder each under Audio/SFX/).</summary>
        public enum Id
        {
            Click, Back,        // UI
            Win, Lose,          // the result
            Star,               // a star landing on the result card
            Step, Hit,          // the sample game: a tap on a lit tile, a miss
        }

        /// <summary>Where the music stems live in the project (one folder per track, <c>Music1/Music1_0.wav</c>...).</summary>
        public const string MusicFolder = "Assets/Game/Audio/Music";

        private const int Voices = 8;
        private static Sfx instance;
        private SoundBank bank;
        private SoundBank.Entry[] byId;
        private AudioSource[] pool;
        private int next;
        private float[] lastPlayed;

        /// <summary>True while a menu is open: the music keeps its place but drops to <see cref="DuckLevel"/>.</summary>
        public static bool Ducked;
        public const float DuckLevel = 0.45f;

        private float duckLevel = 1f, duckUntil;

        /// <summary>
        /// Dip the music to <paramref name="level"/> for <paramref name="seconds"/> (real time) so a moment reads over it:
        /// a toast, a big hit. The deepest duck wins while several overlap; it eases back over about half a second.
        /// </summary>
        public static void Duck(float level, float seconds)
        {
            Sfx s = Get();
            float now = Time.realtimeSinceStartup;
            s.duckLevel = now >= s.duckUntil ? level : Mathf.Min(s.duckLevel, level);
            s.duckUntil = Mathf.Max(s.duckUntil, now + seconds);
        }

        // ---------------------------------------------------------------- music
        private const int MaxStems = 3;
        private readonly AudioSource[] music = new AudioSource[MaxStems * 2];
        private readonly float[] stemWeight = new float[MaxStems];
        private SoundBank.Entry musicEntry;
        private int stems = 1, musicCur;
        private float intensity, musicFade, musicFadeTarget, musicFadeRate = 1f, musicDuck = 1f;
        private double nextLoopDsp, scheduleOtherAt;
        private bool otherScheduled;

        /// <summary>
        /// Starts a track from the bank ("Music1"), fading in; the same track already playing keeps going, so the
        /// music runs on through the levels of a chapter. Its clips are stems, calm to full.
        /// </summary>
        public static void PlayMusic(string id, float fadeIn = 1.2f)
        {
            Sfx s = Get();
            s.wantedMusic = id;
            SoundBank.Entry e = s.bank != null ? s.bank.FindMusic(id) : null;
            if (e == null) { StopMusic(); return; }
            if (e.streamed && s.streamedReady.TryGetValue(id, out SoundBank.Entry ready)) e = ready;
            else if (e.streamed)
            {
                // packed in its own AssetBundle: load it, then play, if this is still the track wanted
                if (s.musicEntry != null && s.musicEntry.id != id) StopMusic();
                if (!s.loading.Contains(id)) s.StartCoroutine(s.LoadStreamed(e, fadeIn));
                return;
            }
            if (e.clips == null || e.clips.Length == 0) { StopMusic(); return; }
            if (s.musicEntry == e && s.musicFadeTarget > 0f) return;
            foreach (AudioSource m in s.music) m.Stop();
            s.musicEntry = e;
            s.stems = Mathf.Min(MaxStems, e.clips.Length);
            double start = AudioSettings.dspTime + 0.1;
            s.musicCur = 0;
            s.nextLoopDsp = start + e.Period;
            for (int k = 0; k < s.stems; k++)
            {
                s.music[k * 2].clip = e.clips[k];
                s.music[k * 2].PlayScheduled(start);
                s.music[k * 2 + 1].clip = e.clips[k];
                s.music[k * 2 + 1].PlayScheduled(s.nextLoopDsp);
            }
            s.otherScheduled = true;
            s.musicFade = 0f;
            s.musicFadeTarget = 1f;
            s.musicFadeRate = 1f / Mathf.Max(0.05f, fadeIn);
            s.intensity = 0f;
            for (int k = 0; k < MaxStems; k++) s.stemWeight[k] = k == 0 ? 1f : 0f;
        }

        // streamed music: each track is an AssetBundle in StreamingAssets/Music
        private string wantedMusic;
        private readonly HashSet<string> loading = new HashSet<string>();
        // loaded tracks, as runtime copies of their bank entries (the bank asset itself is never given the clips: in
        // the Editor that would write them into Resources/SoundBank.asset and back into the build's data)
        private readonly Dictionary<string, SoundBank.Entry> streamedReady = new Dictionary<string, SoundBank.Entry>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern string GSMusicBlobUrl(byte[] data, int length);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void GSRevokeMusicBlobUrl(string url);
#endif

        /// <summary>The web bundle's MP3 stems (TextAssets, sorted by name) decoded into clips; the loop length from its
        /// <c>length</c> file. Empty when the bundle holds clips instead (no encoder at build time).</summary>
        private System.Collections.IEnumerator DecodeWebStems(AssetBundle b, List<AudioClip> into, float[] loopSeconds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AssetBundleRequest all = b.LoadAllAssetsAsync<TextAsset>();
            yield return all;
            var stems = new List<TextAsset>();
            foreach (Object o in all.allAssets)
            {
                if (!(o is TextAsset t)) continue;
                if (t.name == "length") float.TryParse(t.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out loopSeconds[0]);
                else stems.Add(t);
            }
            stems.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            foreach (TextAsset t in stems)
            {
                byte[] data = t.bytes;
                string url = GSMusicBlobUrl(data, data.Length);
                using (var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        AudioClip c = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
                        if (c != null) { c.name = t.name; into.Add(c); }
                    }
                    else Debug.LogWarning($"[Sfx] stem {t.name} did not decode ({req.error})");
                }
                GSRevokeMusicBlobUrl(url);
            }
#else
            yield break;
#endif
        }

        private System.Collections.IEnumerator LoadStreamed(SoundBank.Entry e, float fadeIn)
        {
            loading.Add(e.id);
            AudioClip[] clips = null;
            var loop = new float[1];
#if UNITY_EDITOR
            // in the Editor the stems play straight from the project: no bundles to build first
            var found = new List<AudioClip>();
            string folder = MusicFolder + "/" + e.id;
            if (UnityEditor.AssetDatabase.IsValidFolder(folder))
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (c != null) found.Add(c);
                }
            clips = found.ToArray();
            yield return null;
#else
            string url = System.IO.Path.Combine(Application.streamingAssetsPath, "Music", e.id.ToLowerInvariant());
            if (!url.Contains("://")) url = "file://" + url; // iOS and desktop paths are plain files
            using (var req = UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    AssetBundle b = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(req);
                    if (b != null)
                    {
                        AssetBundleRequest all = b.LoadAllAssetsAsync<AudioClip>();
                        yield return all;
                        var list = new List<AudioClip>();
                        foreach (Object o in all.allAssets) if (o is AudioClip c) list.Add(c);
                        if (list.Count == 0) yield return DecodeWebStems(b, list, loop); // the web build's MP3 stems
                        clips = list.ToArray();
                        // the bundle stays loaded: its clips stream from it
                    }
                }
                else Debug.LogWarning($"[Sfx] music {e.id} did not load ({req.error}); playing on without it");
            }
#endif
            loading.Remove(e.id);
            if (clips == null || clips.Length == 0) yield break;
            System.Array.Sort(clips, (a, c) => string.CompareOrdinal(a.name, c.name)); // MusicN_0 (calm), MusicN_1 (full)
            streamedReady[e.id] = new SoundBank.Entry { id = e.id, clips = clips, volume = e.volume, tail = e.tail, pitchJitter = 0f, streamed = true, loopSeconds = loop[0] };
            if (wantedMusic == e.id) PlayMusic(e.id, fadeIn);
        }

        public static void StopMusic(float fadeOut = 0.8f)
        {
            Sfx s = Get();
            s.wantedMusic = null;
            if (s.musicEntry == null) return;
            s.musicFadeTarget = 0f;
            s.musicFadeRate = 1f / Mathf.Max(0.05f, fadeOut);
        }

        /// <summary>How hard the music plays, 0..1: 0 is the calm stem, 1 the full one.</summary>
        public static void MusicIntensity(float t) { Get().intensity = Mathf.Clamp01(t); }

        private void UpdateMusic()
        {
            if (musicEntry == null) return;
            float dt = Time.unscaledDeltaTime;
            musicFade = Mathf.MoveTowards(musicFade, musicFadeTarget, dt * musicFadeRate);
            float duckWant = Ducked ? DuckLevel : 1f;
            if (Time.realtimeSinceStartup < duckUntil) duckWant = Mathf.Min(duckWant, duckLevel);
            musicDuck = Mathf.MoveTowards(musicDuck, duckWant, dt * (duckWant < musicDuck ? 5f : 2f));
            float v = musicEntry.volume * musicFade * musicDuck * (Progress.Current.Music ? 1f : 0f);
            float pos = intensity * (stems - 1);
            for (int k = 0; k < stems; k++)
            {
                float want = stems == 1 ? 1f : Mathf.Clamp01(1f - Mathf.Abs(pos - k));
                stemWeight[k] = Mathf.MoveTowards(stemWeight[k], want, dt * 0.9f);
                music[k * 2].volume = v * stemWeight[k];
                music[k * 2 + 1].volume = v * stemWeight[k];
            }
            if (musicFade <= 0f && musicFadeTarget <= 0f)
            {
                foreach (AudioSource m in music) m.Stop();
                musicEntry = null;
                return;
            }
            double now = AudioSettings.dspTime;
            double period = musicEntry.Period;
            if (otherScheduled && now >= nextLoopDsp)
            {
                musicCur = 1 - musicCur;
                nextLoopDsp += period;
                otherScheduled = false;
                scheduleOtherAt = now + musicEntry.tail + 0.25;
            }
            if (!otherScheduled && now >= scheduleOtherAt)
            {
                for (int k = 0; k < stems; k++) music[k * 2 + 1 - musicCur].PlayScheduled(nextLoopDsp);
                otherScheduled = true;
            }
        }

        // ---------------------------------------------------------------- setup

        private static Sfx Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Sfx>();
            return instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { Get(); }

        private void Awake()
        {
            bank = Resources.Load<SoundBank>("SoundBank");
            string[] names = System.Enum.GetNames(typeof(Id));
            byId = new SoundBank.Entry[names.Length];
            lastPlayed = new float[names.Length];
            if (bank != null) for (int i = 0; i < names.Length; i++) byId[i] = bank.Find(names[i]);
            pool = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) pool[i] = Voice(false);
            for (int i = 0; i < music.Length; i++) music[i] = Voice(false);
        }

        private AudioSource Voice(bool loop)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = 0f;
            a.volume = loop ? 0f : 1f;
            return a;
        }

        // ---------------------------------------------------------------- one-shots

        /// <summary>True when the bank has this sound (the picks are imported).</summary>
        public static bool Has(Id id) { Sfx s = Get(); return s.byId != null && s.byId[(int)id] != null; }

        /// <summary>A one-shot. Repeats of the same sound inside 40 ms are dropped.</summary>
        /// <param name="maxSeconds">Cut a long sound short (an alarm loop), fading over its last 0.3 s; 0 = play it all.</param>
        public static void Play(Id id, float volume = 1f, float pitch = 1f, float maxSeconds = 0f)
        {
            Sfx s = Get();
            if (s.byId == null) return;
            SoundBank.Entry e = s.byId[(int)id];
            if (e == null || e.loop) return;
            float now = Time.realtimeSinceStartup;
            if (now - s.lastPlayed[(int)id] < 0.04f) return;
            s.lastPlayed[(int)id] = now;
            AudioClip clip = e.clips[Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            AudioSource src = s.pool[s.next];
            s.next = (s.next + 1) % s.pool.Length;
            src.pitch = Mathf.Clamp(pitch * (1f + Random.Range(-e.pitchJitter, e.pitchJitter)), 0.3f, 3f);
            src.volume = Mathf.Clamp01(volume * e.volume);
            src.clip = clip;
            src.Play();
            if (maxSeconds > 0f && clip.length > maxSeconds) s.StartCoroutine(s.CutShort(src, clip, maxSeconds));
        }

        private System.Collections.IEnumerator CutShort(AudioSource src, AudioClip clip, float seconds)
        {
            float v0 = src.volume;
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                if (src.clip != clip) yield break;
                float left = end - Time.realtimeSinceStartup;
                if (left < 0.3f) src.volume = v0 * left / 0.3f;
                yield return null;
            }
            if (src.clip == clip) src.Stop();
        }

        // ---------------------------------------------------------------- loops

        private class LoopState { public AudioSource source; public float target, level; public int frame = -10; public float pitchLow = 1f, pitchHigh = 1f; }
        private readonly Dictionary<int, LoopState> loops = new Dictionary<int, LoopState>();

        /// <summary>
        /// A looping sound driven by a level: call every frame with 0..1 (silent at 0, full at 1); the loop fades out on
        /// its own when the calls stop (a menu, a reload). <paramref name="pitchHigh"/> above 1 speeds it up with the level
        /// (a heartbeat quickens, an engine revs).
        /// </summary>
        public static void Loop(Id id, float level, float pitchLow = 1f, float pitchHigh = 1f)
        {
            Sfx s = Get();
            if (!s.loops.TryGetValue((int)id, out LoopState st))
            {
                st = new LoopState { source = s.Voice(true) };
                s.loops[(int)id] = st;
            }
            st.target = Mathf.Clamp01(level);
            st.frame = Time.frameCount;
            st.pitchLow = pitchLow;
            st.pitchHigh = pitchHigh;
        }

        private void UpdateLoops()
        {
            foreach (KeyValuePair<int, LoopState> kv in loops)
            {
                LoopState st = kv.Value;
                SoundBank.Entry e = byId[kv.Key];
                if (e == null) continue;
                if (Time.frameCount - st.frame > 2) st.target = 0f;
                st.level = Mathf.MoveTowards(st.level, st.target, Time.unscaledDeltaTime * (st.target > st.level ? 3f : 1.5f));
                if (st.level <= 0.001f) { if (st.source.isPlaying) st.source.Stop(); continue; }
                if (!st.source.isPlaying) { st.source.clip = e.clips[0]; st.source.Play(); }
                st.source.volume = e.volume * Mathf.Lerp(0.15f, 1f, st.level);
                st.source.pitch = Mathf.Lerp(st.pitchLow, st.pitchHigh, st.level);
            }
        }

        private void Update()
        {
            AudioListener.volume = Progress.Current.Sound ? 1f : 0f;
            UpdateMusic();
            if (byId != null) UpdateLoops();
        }
    }
}
