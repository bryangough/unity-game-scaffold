namespace GameScaffold
{
    /// <summary>
    /// The one place the game's identity lives in code. <c>tools/new_game.py</c> rewrites these when it makes a new
    /// project from the scaffold; everything else (save keys, the signing folder, log tags) reads them from here.
    /// </summary>
    public static class GameInfo
    {
        /// <summary>Short lower-case id: PlayerPrefs keys (<c>id.progress</c>), the signing folder, the itch zip name.</summary>
        public const string Id = "gamescaffold";

        /// <summary>The name shown to people (menus, the credits).</summary>
        public const string Title = "Game Scaffold";

        /// <summary>The credits line and the link on the credits card. Empty = no link.</summary>
        public const string Credit = "A game by Your Studio";
        public const string Website = "";
    }
}
