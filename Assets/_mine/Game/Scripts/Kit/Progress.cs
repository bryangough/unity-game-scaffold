using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>One level's record in the save.</summary>
    [Serializable]
    public class LevelRecord
    {
        public bool solved;
        /// <summary>The best score (or fewest moves) so far; -1 = never won.</summary>
        public int best = -1;
        /// <summary>The stars earned so far, as bits (the game defines what each bit means; up to three is the
        /// convention: cleared, a style star, a mastery star). Each is kept once earned, so they can come from
        /// different runs.</summary>
        public int stars;
    }

    [Serializable]
    public class ProgressData
    {
        public int version = Progress.Version;
        public int lastLevel;
        public List<LevelRecord> levels = new List<LevelRecord>();
        public bool music = true;
        public bool sound = true;
        public bool haptics = true;
        public bool unlockAll;   // Game > Saved Data > Unlock Everything (Testing)
        public int flags;        // one-time teaching lines already shown, as bits
    }

    /// <summary>
    /// The save, plain C# with EditMode tests: per level solved, best and stars, the level last played, the settings
    /// and one-time flags; JSON in PlayerPrefs under <see cref="PrefsKey"/>, with a version number. Three stars per
    /// level is the convention: separate bits, so a daring run and a careful run each earn their own.
    /// <para>Levels are open by default (<see cref="LockLevels"/> false): a stuck player picks any level from the
    /// list, and PLAY goes on to the first unsolved one from where they were.</para>
    /// </summary>
    public class Progress
    {
        public const int Version = 1;
        public static readonly string PrefsKey = GameInfo.Id + ".progress";
        public const int Star1 = 1, Star2 = 2, Star3 = 4;

        /// <summary>True: a level opens once the one before it is solved. False: every level is open from the start.</summary>
        public const bool LockLevels = false;

        private readonly ProgressData data;

        private Progress(ProgressData data)
        {
            this.data = data ?? new ProgressData();
            if (this.data.levels == null) this.data.levels = new List<LevelRecord>();
        }

        public Progress() : this(new ProgressData()) { }

        // ---------------------------------------------------------------- the saved copy

        private static Progress current;

        /// <summary>The player's progress, loaded from PlayerPrefs on first use.</summary>
        public static Progress Current
        {
            get
            {
                if (current == null)
                {
                    if (!checkedLaunch) { checkedLaunch = true; NewPlayer = !PlayerPrefs.HasKey(PrefsKey); }
                    current = FromJson(PlayerPrefs.GetString(PrefsKey, ""));
                }
                return current;
            }
        }

        /// <summary>No save existed when this launch first read it: a new player (skip the start screen, show the first hint).</summary>
        public static bool NewPlayer { get; private set; }
        private static bool checkedLaunch;

        /// <summary>While true (a replay bot, a capture), nothing is written to PlayerPrefs: a tool's wins never land in the save.</summary>
        public static bool Suspended;

        public void Save()
        {
            if (Suspended || this != current) return;
            PlayerPrefs.SetString(PrefsKey, ToJson());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Forget the progress (Game > Saved Data > Clear All, a reset in settings). The settings (music, sound,
        /// vibration) are kept by default: a player resetting their progress did not ask for the music back on.
        /// </summary>
        public static void ClearAll(bool keepSettings = true)
        {
            ProgressData fresh = new ProgressData();
            if (keepSettings && current != null) { fresh.music = current.data.music; fresh.sound = current.data.sound; fresh.haptics = current.data.haptics; }
            PlayerPrefs.DeleteKey(PrefsKey);
            current = new Progress(fresh);
            if (keepSettings) current.Save();
            PlayerPrefs.Save();
        }

        /// <summary>Forget the in-memory copy; the next use reads PlayerPrefs again.</summary>
        public static void Reload() => current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { current = null; Suspended = false; checkedLaunch = false; NewPlayer = false; }

        public string ToJson() => JsonUtility.ToJson(data);

        /// <summary>A save from its JSON; an empty, broken or newer-version save starts fresh.</summary>
        public static Progress FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return new Progress();
            try
            {
                var d = JsonUtility.FromJson<ProgressData>(json);
                if (d == null || d.version > Version) return new Progress();
                d.version = Version; // older saves are read as they are and written back at this version
                return new Progress(d);
            }
            catch (Exception) { return new Progress(); }
        }

        // ---------------------------------------------------------------- levels

        public LevelRecord Level(int index)
        {
            while (data.levels.Count <= index) data.levels.Add(new LevelRecord());
            return data.levels[Mathf.Max(0, index)];
        }

        public int LastLevel
        {
            get => data.lastLevel;
            set
            {
                int v = Mathf.Max(0, value);
                if (v == data.lastLevel) return; // opening the same level writes nothing
                data.lastLevel = v;
                Save();
            }
        }

        public bool UnlockAll { get => data.unlockAll; set { data.unlockAll = value; Save(); } }
        public int Flags { get => data.flags; set { data.flags = value; Save(); } }
        public bool Music { get => data.music; set { data.music = value; Save(); } }
        public bool Sound { get => data.sound; set { data.sound = value; Save(); } }
        public bool Haptics { get => data.haptics; set { data.haptics = value; Save(); } }

        /// <summary>A one-time flag (a hint shown once ever): true the first time, false after.</summary>
        public bool FirstTime(int flag)
        {
            if ((data.flags & flag) != 0) return false;
            Flags = data.flags | flag;
            return true;
        }

        public static int Count(int stars) => (stars & 1) + ((stars >> 1) & 1) + ((stars >> 2) & 1);

        /// <summary>
        /// Records a win; returns the stars that are new for this level (for the result card to land).
        /// <paramref name="higherIsBetter"/> false for a move count or a time.
        /// </summary>
        public int RecordWin(int level, int score, int stars, bool higherIsBetter = true)
        {
            LevelRecord r = Level(level);
            int fresh = stars & ~r.stars;
            r.solved = true;
            r.stars |= stars;
            if (r.best < 0 || (higherIsBetter ? score > r.best : score < r.best)) r.best = score;
            Save();
            return fresh;
        }

        public int TotalStars
        {
            get { int t = 0; foreach (LevelRecord r in data.levels) t += Count(r.stars); return t; }
        }

        public int SolvedCount
        {
            get { int n = 0; foreach (LevelRecord r in data.levels) if (r.solved) n++; return n; }
        }

        public bool IsUnlocked(int level)
        {
            if (!LockLevels || data.unlockAll || level <= 0) return true;
            return Level(level - 1).solved;
        }

        /// <summary>
        /// PLAY's target: the first level not yet solved from the last level played on, wrapping round to the start, so
        /// a player who skipped a stuck level is not sent straight back to it. The last level if every level is solved.
        /// </summary>
        public int NextUnsolved(int levelCount)
        {
            if (levelCount <= 0) return 0;
            int from = Mathf.Clamp(LastLevel, 0, levelCount - 1);
            for (int k = 0; k < levelCount; k++)
            {
                int i = (from + k) % levelCount;
                if (!Level(i).solved && IsUnlocked(i)) return i;
            }
            return levelCount - 1;
        }
    }
}
