using System;

namespace GameScaffold
{
    /// <summary>
    /// The sample game's rules in plain C# (the "Duels rule": rules live in a class with EditMode tests, MonoBehaviours
    /// only present them, and a rule is never decided with a physics ray). A tile lights; tapping it is a hit, letting
    /// it go dark or tapping another is a miss; <see cref="SampleLevel.hitsToWin"/> wins, one miss over
    /// <see cref="SampleLevel.missesAllowed"/> loses. Deterministic for a seed, so a test (or a replay bot) can play it.
    /// </summary>
    public class SampleRules
    {
        public enum Outcome { Nothing, Hit, Miss }

        public readonly SampleLevel Level;
        /// <summary>The lit tile (row-major index), or -1 between tiles.</summary>
        public int Lit { get; private set; } = -1;
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool IsOver => Won || Lost;
        public float Elapsed { get; private set; }
        /// <summary>How long the current tile has been lit.</summary>
        public float LitFor { get; private set; }
        public int Tiles => Level.grid * Level.grid;

        private readonly Random rng;
        private float gap = 0.4f;
        private float reactionTotal;
        private int lastLit = -1;

        public const float GapAfterHit = 0.35f, GapAfterMiss = 0.6f;

        public SampleRules(SampleLevel level, int seed = 1)
        {
            Level = level;
            rng = new Random(seed);
        }

        /// <summary>Advances the clock: lights the next tile after a gap, counts a tile left lit too long as a miss.</summary>
        public void Tick(float dt)
        {
            if (IsOver) return;
            Elapsed += dt;
            if (Lit < 0)
            {
                gap -= dt;
                if (gap <= 0f) LightNext();
                return;
            }
            LitFor += dt;
            if (LitFor >= Level.litSeconds) Miss();
        }

        /// <summary>The player tapped tile <paramref name="index"/>.</summary>
        public Outcome Tap(int index)
        {
            if (IsOver || index < 0 || index >= Tiles) return Outcome.Nothing;
            if (Lit < 0) return Outcome.Nothing; // between tiles: nothing to hit, nothing lost
            if (index != Lit) { Miss(); return Outcome.Miss; }
            Hits++;
            reactionTotal += LitFor;
            Lit = -1;
            gap = GapAfterHit;
            if (Hits >= Level.hitsToWin) Won = true;
            return Outcome.Hit;
        }

        /// <summary>The level clock ran out (the kit's timer): a loss unless already decided.</summary>
        public void OutOfTime() { if (!IsOver) Lost = true; }

        public float AverageReaction => Hits > 0 ? reactionTotal / Hits : 0f;

        /// <summary>The stars a win earns: cleared, no misses, fast (average reaction at or under the level's bar).</summary>
        public int Stars
        {
            get
            {
                int s = Progress.Star1;
                if (Misses == 0) s |= Progress.Star2;
                if (Hits > 0 && AverageReaction <= Level.fastSeconds) s |= Progress.Star3;
                return s;
            }
        }

        private void Miss()
        {
            Misses++;
            Lit = -1;
            gap = GapAfterMiss;
            if (Misses > Level.missesAllowed) Lost = true;
        }

        private void LightNext()
        {
            int n = Tiles;
            int next = n == 1 ? 0 : rng.Next(n - 1);
            if (next >= lastLit && lastLit >= 0) next++; // never the same tile twice running
            Lit = next;
            lastLit = next;
            LitFor = 0f;
        }
    }
}
