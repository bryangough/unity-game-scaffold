namespace GameScaffold
{
    /// <summary>
    /// The rules, in plain C# with EditMode tests (<c>GameRulesTests</c>); the manager only presents them, and a rule
    /// is never decided with a physics ray. This is the smallest game that can be won and lost, standing where the
    /// real rules go: tap <see cref="GameLevel.taps"/> times before <see cref="GameLevel.seconds"/> run out.
    /// Deterministic, so a test, the level check and a replay bot can all play it.
    /// </summary>
    public class GameRules
    {
        public readonly GameLevel Level;
        public int Taps { get; private set; }
        public float Elapsed { get; private set; }
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool IsOver => Won || Lost;
        /// <summary>0 at the start, 1 when won.</summary>
        public float Progress01 => Level.taps > 0 ? (float)Taps / Level.taps : 1f;

        public GameRules(GameLevel level) { Level = level; }

        /// <summary>Advances the clock; out of time is the loss.</summary>
        public void Tick(float dt)
        {
            if (IsOver) return;
            Elapsed += dt;
            if (Elapsed >= Level.seconds) Lost = true;
        }

        /// <summary>The player's one verb. False when the level is already decided.</summary>
        public bool Tap()
        {
            if (IsOver) return false;
            Taps++;
            if (Taps >= Level.taps) Won = true;
            return true;
        }

        /// <summary>The stars a win earns: cleared, with a quarter of the clock left, with half of it left.</summary>
        public int Stars
        {
            get
            {
                int s = Progress.Star1;
                if (Elapsed <= Level.seconds * 0.75f) s |= Progress.Star2;
                if (Elapsed <= Level.seconds * 0.5f) s |= Progress.Star3;
                return s;
            }
        }
    }
}
