using System;

namespace GameScaffold
{
    /// <summary>
    /// One level of the sample game, as data. The convention every shipped game kept: levels are entries in a list,
    /// never scenes, so a new level is a few lines and the one scene rebuilds itself for whichever is current.
    /// Replace this with your game's level type (a text map, a seed, a route list...).
    /// </summary>
    [Serializable]
    public class SampleLevel
    {
        public string title;
        /// <summary>One line shown as the level opens, until the first tap.</summary>
        public string callout;
        public int grid = 3;
        /// <summary>How long a tile stays lit before it counts as missed.</summary>
        public float litSeconds = 1.2f;
        public int hitsToWin = 10;
        public int missesAllowed = 2;
        /// <summary>The level clock, 0 = none (the sample uses one; the shipped puzzle game dropped its clock).</summary>
        public float seconds = 60f;
        /// <summary>The third star: average reaction at or under this.</summary>
        public float fastSeconds = 0.6f;
    }

    /// <summary>The sample's three levels. Keys 1-3 (and the Slot actions) jump to any of them in the Editor.</summary>
    public static class SampleLevels
    {
        public static readonly SampleLevel[] All =
        {
            new SampleLevel { title = "Warm Up", callout = "Tap the lit tile before it goes dark.", grid = 2, litSeconds = 1.6f, hitsToWin = 6, missesAllowed = 3, fastSeconds = 0.8f },
            new SampleLevel { title = "Nine Tiles", callout = "Bigger board, less time.", grid = 3, litSeconds = 1.1f, hitsToWin = 10, missesAllowed = 2, fastSeconds = 0.6f },
            new SampleLevel { title = "Quick Hands", callout = "Two misses and you are out.", grid = 4, litSeconds = 0.8f, hitsToWin = 14, missesAllowed = 1, fastSeconds = 0.5f },
        };

        public static int Count => All.Length;

        public static SampleLevel Get(int index) => All[Math.Clamp(index, 0, All.Length - 1)];
    }
}
