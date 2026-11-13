using System;

namespace GameScaffold
{
    /// <summary>
    /// What the screens around the game need to know about its levels: how many there are and what each is called.
    /// The game hands one to <see cref="Shell.Catalog"/>; stars, bests and the level last played come from
    /// <see cref="Progress"/>, so the catalogue stays this small. Titles are shown, never used as save keys.
    /// </summary>
    public interface ILevelCatalog
    {
        int Count { get; }
        string Title(int index);
    }

    /// <summary>The level select's paging, plain C# and tested: which levels are on which page.</summary>
    public static class LevelPages
    {
        public static int PageCount(int levels, int perPage) => levels <= 0 ? 1 : (levels + perPage - 1) / perPage;

        /// <summary>The page a level is on (the level select opens on the current level's page).</summary>
        public static int PageOf(int level, int perPage) => Math.Max(0, level) / perPage;

        public static int Clamp(int page, int levels, int perPage) => Math.Min(Math.Max(0, page), PageCount(levels, perPage) - 1);

        /// <summary>The first level on a page and how many it holds (the last page may be short).</summary>
        public static (int first, int count) Range(int page, int levels, int perPage)
        {
            int first = Clamp(page, levels, perPage) * perPage;
            return (first, Math.Max(0, Math.Min(perPage, levels - first)));
        }
    }
}
