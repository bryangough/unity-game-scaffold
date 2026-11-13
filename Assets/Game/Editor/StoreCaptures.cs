#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Store > Capture Screenshots (Play mode)</c>: poses each of <see cref="Shots"/> and writes it at every
    /// store size in <see cref="Sizes"/>, to <c>Store/Screenshots/&lt;store&gt;_&lt;w&gt;x&lt;h&gt;/&lt;name&gt;.png</c>, with the
    /// HUD and menus in (<see cref="Captures.Shot"/>). The save is suspended for the run and read back from PlayerPrefs at
    /// the end, so demo stars seeded for a level-select shot never reach the player's progress.
    /// <para>The game fills in <see cref="Shots"/>: find each scene by name, not number (renumbering levels then never
    /// breaks a shot), pose it (a few moves of the level's solution, a meter almost full) and hide one-time hints.
    /// Google Play takes up to 8, the App Store up to 10; the first two or three are the ones people see. The sizes
    /// are portrait: a landscape game swaps them. From the stealth game.</para>
    /// </summary>
    public static class StoreCaptures
    {
        public const string Root = "Store/Screenshots";

        /// <summary>The store sizes (see <c>Store/listing.md</c>). Add the iPad's when the build keeps iPad.</summary>
        public static readonly (string store, int w, int h)[] Sizes =
        {
            ("Play", 1080, 1920),          // Google Play and itch.io: 9:16, inside Play's 2:1 limit
            ("AppStore", 1320, 2868),      // iPhone 6.9", scaled down by Apple for the smaller iPhones
            // ("iPad", 2064, 2752),       // iPad 13", only if Player Settings keep iPad
        };

        /// <summary>A shot: its file name and what to do before it is taken (load the level, pose it, open a menu).</summary>
        public struct Pose
        {
            public string name;
            public Action setup;
            public Pose(string name, Action setup) { this.name = name; this.setup = setup; }
        }

        /// <summary>The game's shots, in store order. Until the game fills it in, one shot of whatever is on screen.
        /// A pose is a level loaded by name and set up, for example:
        /// <c>new Pose("01_crate_cone", () => { MyGame.LoadLevel(MyLevels.IndexOf("Vault")); MyGame.PlayMoves(5); })</c>.</summary>
        public static readonly List<Pose> Shots = new List<Pose>
        {
            new Pose("01_current_view", null),
        };

        /// <summary>Frames between steps: long enough for a level load, a fade and the UI's first layout.</summary>
        public const int Gap = 30;

        [MenuItem(Menus.Root + "Store/Capture Screenshots (Play mode)", false, 50)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[StoreCaptures] enter Play mode first"); return; }
            Progress.Suspended = true; // nothing the poses do is written to the save

            var steps = new List<Action>();
            foreach (Pose p in Shots)
            {
                Pose pose = p;
                steps.Add(() => pose.setup?.Invoke());
                steps.Add(() =>
                {
                    foreach (var s in Sizes) Captures.Shot(pose.name, s.w, s.h, $"{Root}/{s.store}_{s.w}x{s.h}");
                });
            }
            steps.Add(() =>
            {
                Progress.Reload();          // drop the in-memory demo progress; PlayerPrefs was never written
                Progress.Suspended = false; // the game reloads its level here if its poses moved it
                Debug.Log($"[StoreCaptures] {Shots.Count} shots at {Sizes.Length} sizes in {Root}/");
            });
            Captures.Sequence(Gap, steps.ToArray());
        }
    }
}
#endif
