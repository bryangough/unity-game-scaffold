using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// The game's sounds: one entry per <see cref="Sfx.Id"/> name, each with its variants. Built by
    /// <c>Game > Audio > Import Sound Picks</c> from the picks in <c>Editor/SoundPicks.cs</c>; never edited by hand.
    /// Lives in <c>Resources/</c> so <see cref="Sfx"/> finds it without scene wiring.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch either way, so repeats don't sound copied (0.06 = +-6 %).")]
            public float pitchJitter = 0.06f;
            [Tooltip("A loop (an engine, a heartbeat): played on its own voice through Sfx.Loop, never as a one-shot.")]
            public bool loop;
            [Tooltip("Music only: seconds of reverb tail at the end. The next pass starts this long before the end, so the tail rings over it.")]
            public float tail;
            [Tooltip("Music only: the stems are not in the build's data; they are packed in the AssetBundle StreamingAssets/Music/<id in lower case>, loaded when the track is first played.")]
            public bool streamed;
        }

        public Entry[] entries;

        /// <summary>A music entry, with or without its clips (a streamed track has none until its bundle is loaded).</summary>
        public Entry FindMusic(string id)
        {
            if (entries == null) return null;
            foreach (Entry e in entries) if (e != null && e.id == id) return e;
            return null;
        }

        public Entry Find(string id)
        {
            if (entries == null) return null;
            foreach (Entry e in entries)
                if (e != null && e.id == id && e.clips != null && e.clips.Length > 0) return e;
            return null;
        }
    }
}
