using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>Where the missions keep their state: PlayerPrefs in the game, a dictionary in the tests.</summary>
    public interface IMissionStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void DeleteKey(string key);
    }

    public class PrefsMissionStore : IMissionStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
        public void DeleteKey(string key) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// Missions: a few up at a time from a pool, a reason to play again when the best score is out of reach. The game
    /// defines <typeparamref name="TReport"/> (what a finished run did) and a pool of missions that measure it; each
    /// one done is a star and makes room for the next in the pool; one can be skipped once it has been up for 24 h.
    /// Plain C#, tested; two of the shipped games had this. <b>Add new missions at the end of the pool</b>: saved
    /// places in the pool point at them by index.
    /// </summary>
    public class MissionBook<TReport>
    {
        public sealed class Mission
        {
            public readonly string id, text;
            public readonly int target;
            /// <summary>Progress adds up over runs (else it has to happen in one run).</summary>
            public readonly bool acrossRuns;
            public readonly Func<TReport, int> measure;
            public Mission(string id, string text, int target, bool acrossRuns, Func<TReport, int> measure)
            { this.id = id; this.text = text; this.target = target; this.acrossRuns = acrossRuns; this.measure = measure; }
        }

        public readonly Mission[] Pool;
        public readonly int Slots;
        public const long SkipAfterSeconds = 24 * 60 * 60;
        private readonly string K;
        private readonly IMissionStore store;

        public MissionBook(Mission[] pool, IMissionStore store = null, int slots = 3, string prefix = null)
        {
            Pool = pool;
            Slots = Mathf.Clamp(slots, 1, Mathf.Max(1, pool.Length));
            this.store = store ?? new PrefsMissionStore();
            K = (prefix ?? GameInfo.Id) + ".mission.";
        }

        /// <summary>Missions done so far: each is a star toward a ladder.</summary>
        public int Stars => store.GetInt(K + "stars", 0);

        private int SlotIndex(int slot) => store.GetInt(K + "slot" + slot, -1);

        /// <summary>The missions up now (the first ones of the pool on a new game; their 24 h starts then).</summary>
        public Mission[] Active(long now = -1)
        {
            var a = new Mission[Slots];
            for (int i = 0; i < Slots; i++)
            {
                int ix = SlotIndex(i);
                if (ix < 0 || ix >= Pool.Length)
                {
                    ix = i % Pool.Length;
                    store.SetInt(K + "slot" + i, ix);
                    store.SetInt(K + "since" + i, Clamp(now >= 0 ? now : Now()));
                }
                a[i] = Pool[ix];
            }
            return a;
        }

        /// <summary>Progress toward a mission that adds up over runs (0 for the one-run kind).</summary>
        public int Progress(Mission m) => m.acrossRuns ? store.GetInt(K + "progress." + m.id, 0) : 0;

        /// <summary>Count a finished run. Returns the missions it completed (each replaced by the next in the pool).</summary>
        public List<Mission> Submit(TReport r, long now)
        {
            var done = new List<Mission>();
            Mission[] active = Active(now);
            for (int i = 0; i < Slots; i++)
            {
                Mission m = active[i];
                int value = Mathf.Max(0, m.measure(r));
                bool complete;
                if (m.acrossRuns)
                {
                    int p = Progress(m) + value;
                    store.SetInt(K + "progress." + m.id, Mathf.Min(p, m.target));
                    complete = p >= m.target;
                }
                else complete = value >= m.target;
                if (!complete) continue;
                done.Add(m);
                store.SetInt(K + "stars", Stars + 1);
                store.SetInt(K + "progress." + m.id, 0); // fresh if it comes round again
                Replace(i, now);
            }
            return done;
        }

        /// <summary>A mission can be skipped after 24 h up (a save from before the clock existed starts it now).</summary>
        public bool CanSkip(int slot, long now)
        {
            int since = store.GetInt(K + "since" + slot, -1);
            if (since < 0) { store.SetInt(K + "since" + slot, Clamp(now)); return false; }
            return now - since >= SkipAfterSeconds;
        }

        /// <summary>Swap a mission that has been up 24 h for the next one. False if it is too soon.</summary>
        public bool Skip(int slot, long now)
        {
            if (slot < 0 || slot >= Slots) return false;
            Active(now);
            if (!CanSkip(slot, now)) return false;
            Replace(slot, now);
            return true;
        }

        /// <summary>The next mission in the pool that is not up in another slot.</summary>
        private void Replace(int slot, long now)
        {
            int next = store.GetInt(K + "next", Slots);
            for (int tries = 0; tries < Pool.Length; tries++)
            {
                int ix = next % Pool.Length;
                next++;
                bool taken = false;
                for (int j = 0; j < Slots; j++) if (j != slot && SlotIndex(j) == ix) taken = true;
                if (taken || ix == SlotIndex(slot)) continue;
                store.SetInt(K + "slot" + slot, ix);
                break;
            }
            store.SetInt(K + "next", next % Pool.Length);
            store.SetInt(K + "since" + slot, Clamp(now));
        }

        /// <summary>Back to a new game: no stars, no progress, the first missions up again.</summary>
        public void Clear()
        {
            store.DeleteKey(K + "stars");
            store.DeleteKey(K + "next");
            for (int i = 0; i < Slots; i++) { store.DeleteKey(K + "slot" + i); store.DeleteKey(K + "since" + i); }
            foreach (Mission m in Pool) store.DeleteKey(K + "progress." + m.id);
        }

        /// <summary>Seconds since 1970, for the 24 h skip.</summary>
        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static int Clamp(long seconds) => (int)Math.Min(Math.Max(seconds, 0), int.MaxValue);
    }
}
