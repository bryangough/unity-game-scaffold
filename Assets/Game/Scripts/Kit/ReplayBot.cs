#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace GameScaffold
{
    /// <summary>
    /// Plays every level through the real game, with no hand on it: the input path, the views and the frame loop are
    /// tested, not just the rules. A level the solver wins but the game loses points at the views or the input.
    /// The game supplies the plan for a level (read from the level check's cache, never solved here) and a
    /// <see cref="Spec.Play"/> that presses the next input through <see cref="InputBot"/>; this owns the rest: opening
    /// each level and waiting for it to load, a call at the end of every frame, the result (from
    /// <see cref="FunTestKit.OnResult"/>), a time limit per level, the report, and putting the save, the input
    /// settings and the scene back. Started in Play mode by <c>Checks > Replay Levels</c> (<c>LevelReplay</c>);
    /// it lives across the scene loads it causes.
    /// <para>For a turn-based game pin the frame step (<see cref="Spec.Tick"/>) to the game's tick so one tick runs a
    /// frame however fast the frames come: then the replay and the solver agree exactly.</para>
    /// </summary>
    public sealed class ReplayBot : MonoBehaviour
    {
        public sealed class Spec
        {
            public int Count;
            public Func<int, string> Title = i => "";
            /// <summary>The stored plan for a level; null means "not checked yet" and the level is reported, not played.</summary>
            public Func<int, string> Plan = i => "";
            /// <summary>Opens a level in play. The shell's by default.</summary>
            public Action<int> Open = Shell.OpenLevel;
            /// <summary>
            /// Called at the end of every frame while the level is undecided, with the frames since it opened: press
            /// (or lift) this frame's input. Return false when the plan has nothing left; the bot then waits for the result.
            /// </summary>
            public Func<int, string, int, bool> Play;
            /// <summary><c>Time.captureDeltaTime</c> for the run; 0 leaves the clock alone (a real-time game).</summary>
            public float Tick;
            /// <summary>Real seconds a level may take before it is reported as having no result.</summary>
            public float Timeout = 30f;
            /// <summary>The range to play (0-based, inclusive): a changed chapter, not the whole game.</summary>
            public int First, Last = int.MaxValue;
        }

        private static ReplayBot host;
        public static bool Running => host != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { host = null; }

        /// <summary>Starts the replay; <paramref name="done"/> gets the report (Markdown) and how many levels did not win.</summary>
        public static void Run(Spec spec, Action<string, int> done)
        {
            if (!Application.isPlaying || host != null || spec == null || spec.Play == null) return;
            var go = new GameObject("ReplayBot");
            DontDestroyOnLoad(go);
            host = go.AddComponent<ReplayBot>();
            host.StartCoroutine(host.Replay(spec, done));
        }

        private void OnDestroy()
        {
            if (host != this) return;
            host = null;
            InputBot.End(); // Play mode stopped mid-run: the save and the input settings still go back
        }

        private IEnumerator Replay(Spec spec, Action<string, int> done)
        {
            var sb = new StringBuilder("# Replay\n\n");
            var rows = new StringBuilder("| # | Level | Result | Seconds |\n|---|---|---|---|\n");
            int failed = 0, played = 0, wins = 0, loads = 0;
            UnityAction<Scene, LoadSceneMode> onLoad = (s, m) => loads++;
            SceneManager.sceneLoaded += onLoad;
            InputBot.Begin(spec.Tick);

            int last = Mathf.Min(spec.Last, spec.Count - 1);
            for (int level = Mathf.Max(0, spec.First); level <= last; level++)
            {
                string title = spec.Title != null ? spec.Title(level) : "";
                string plan = spec.Plan != null ? spec.Plan(level) : "";
                if (plan == null)
                {
                    failed++;
                    rows.Append($"| {level + 1} | {title} | **NO PLAN**: run Validate Levels first | |\n");
                    continue;
                }

                int before = loads;
                spec.Open(level);
                float t0 = Time.realtimeSinceStartup;
                while ((loads == before || ScreenFade.IsFading) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
                if (loads == before)
                {
                    failed++;
                    rows.Append($"| {level + 1} | {title} | **DID NOT LOAD** | |\n");
                    continue;
                }
                yield return null; // every Start has run

                bool? result = null;
                FunTestKit.OnResult += won => result = won; // the kit drops its listeners with the scene
                int frame = 0;
                bool more = true;
                t0 = Time.realtimeSinceStartup;
                while (result == null && Time.realtimeSinceStartup - t0 < spec.Timeout)
                {
                    yield return new WaitForEndOfFrame(); // an input queued now is read by the game next frame
                    if (result != null) break;
                    if (!more) continue;
                    more = spec.Play(level, plan, frame++);
                    if (!more) Release();
                }
                Release();
                played++;
                float seconds = Time.realtimeSinceStartup - t0;
                if (result == true) wins++; else failed++;
                string outcome = result == true ? "won" : result == false ? "**LOST**" : "**NO RESULT**" + (more ? " (timed out mid-plan)" : " (the plan ran out)");
                rows.Append($"| {level + 1} | {title} | {outcome} | {seconds:0.0} |\n");
            }

            SceneManager.sceneLoaded -= onLoad;
            InputBot.End(); // the save is read back from disk: the bot's wins were never written
            sb.Append($"{DateTime.Now:yyyy-MM-dd HH:mm}: {played} played, {wins} won, {failed} not\n\n").Append(rows);
            host = null;
            Destroy(gameObject);
            FunTestKit.Restart(); // the scene goes back to the level the save says
            done?.Invoke(sb.ToString(), failed);
        }

        private static void Release()
        {
            InputBot.ReleaseKeys();
            InputBot.TouchUp();
        }
    }
}
#endif
