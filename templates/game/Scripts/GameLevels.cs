using System;

namespace GameScaffold
{
    /// <summary>
    /// One level, as data: levels are entries in a list, never scenes, so a new level is a few lines and the one scene
    /// rebuilds itself for whichever is current. Replace the fields with the game's own (a text map, a seed, a route).
    /// </summary>
    [Serializable]
    public class GameLevel
    {
        public string title;
        /// <summary>Taps needed to win.</summary>
        public int taps = 10;
        /// <summary>The clock: out of time is the loss.</summary>
        public float seconds = 10f;
    }

    /// <summary>The levels, in order. Keys 1-9 jump to any of them in the Editor and development builds.</summary>
    public static class GameLevels
    {
        public static readonly GameLevel[] All =
        {
            new GameLevel { title = "First Steps", taps = 5, seconds = 10f },
            new GameLevel { title = "Faster", taps = 12, seconds = 8f },
            new GameLevel { title = "Sprint", taps = 20, seconds = 6f },
        };

        public static int Count => All.Length;

        public static GameLevel Get(int index) => All[Math.Clamp(index, 0, All.Length - 1)];

        /// <summary>What the screens around the game see of the levels (<see cref="Shell.Catalog"/>).</summary>
        public static readonly ILevelCatalog Catalog = new LevelList();

        private sealed class LevelList : ILevelCatalog
        {
            public int Count => All.Length;
            public string Title(int index) => Get(index).title;
        }
    }
}
