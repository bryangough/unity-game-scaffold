using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>
    /// The sample's rules, played without a scene: the shape every game's rule tests take (build the rules, tick,
    /// act, assert; a plan played to a win; the lose conditions each on their own).
    /// </summary>
    public class SampleRulesTests
    {
        private static SampleLevel Level(int grid = 3, int hits = 3, int misses = 1, float lit = 1f) =>
            new SampleLevel { title = "test", grid = grid, hitsToWin = hits, missesAllowed = misses, litSeconds = lit, fastSeconds = 0.5f };

        [Test]
        public void ATileLightsAfterTheGap_AndHittingItCounts()
        {
            var r = new SampleRules(Level(), seed: 3);
            Assert.AreEqual(-1, r.Lit, "nothing lit at the start");
            r.Tick(0.5f);
            Assert.GreaterOrEqual(r.Lit, 0, "a tile is lit after the opening gap");
            Assert.AreEqual(SampleRules.Outcome.Hit, r.Tap(r.Lit));
            Assert.AreEqual(1, r.Hits);
            Assert.AreEqual(-1, r.Lit, "dark again until the next one");
        }

        [Test]
        public void TappingTheWrongTile_OrWaitingTooLong_IsAMiss()
        {
            var r = new SampleRules(Level(misses: 5), seed: 3);
            r.Tick(0.5f);
            int lit = r.Lit;
            Assert.AreEqual(SampleRules.Outcome.Miss, r.Tap((lit + 1) % r.Tiles));
            Assert.AreEqual(1, r.Misses);
            r.Tick(SampleRules.GapAfterMiss + 0.01f);
            Assert.GreaterOrEqual(r.Lit, 0);
            r.Tick(1.01f); // longer than litSeconds
            Assert.AreEqual(2, r.Misses, "a tile left lit too long is a miss");
            Assert.AreEqual(SampleRules.Outcome.Nothing, r.Tap(0), "between tiles a tap does nothing");
        }

        [Test]
        public void OneMissOverTheAllowance_Loses()
        {
            var r = new SampleRules(Level(misses: 1), seed: 3);
            r.Tick(0.5f); r.Tick(1.01f);
            Assert.IsFalse(r.Lost, "the allowed miss");
            r.Tick(SampleRules.GapAfterMiss + 0.01f); r.Tick(1.01f);
            Assert.IsTrue(r.Lost, "one over");
            Assert.IsTrue(r.IsOver);
        }

        [Test]
        public void HittingEveryTile_WinsWithThreeStars_WhenFastAndClean()
        {
            var r = new SampleRules(Level(hits: 4), seed: 11);
            for (int i = 0; i < 4; i++)
            {
                r.Tick(0.5f);                 // the gap passes, a tile lights
                r.Tick(0.2f);                 // a quick reaction
                Assert.AreEqual(SampleRules.Outcome.Hit, r.Tap(r.Lit), $"hit {i + 1}");
            }
            Assert.IsTrue(r.Won);
            Assert.AreEqual(0, r.Misses);
            Assert.AreEqual(Progress.Star1 | Progress.Star2 | Progress.Star3, r.Stars);
            Assert.AreEqual(0.2f, r.AverageReaction, 0.01f);
        }

        [Test]
        public void ASlowRun_LosesTheFastStar_AndAMissLosesTheCleanOne()
        {
            var r = new SampleRules(Level(hits: 2, misses: 2), seed: 5);
            r.Tick(0.5f); r.Tick(0.8f); r.Tap(r.Lit);          // slow
            r.Tick(0.5f); r.Tap((r.Lit + 1) % r.Tiles);          // a miss
            r.Tick(SampleRules.GapAfterMiss + 0.01f); r.Tick(0.8f); r.Tap(r.Lit);
            Assert.IsTrue(r.Won);
            Assert.AreEqual(Progress.Star1, r.Stars);
        }

        [Test]
        public void TheSameTileNeverLightsTwiceRunning_AndASeedReplays()
        {
            var a = new SampleRules(Level(grid: 2, hits: 20), seed: 42);
            var b = new SampleRules(Level(grid: 2, hits: 20), seed: 42);
            int last = -1;
            for (int i = 0; i < 20; i++)
            {
                a.Tick(0.5f); b.Tick(0.5f);
                Assert.AreEqual(a.Lit, b.Lit, "the same seed lights the same tiles");
                Assert.AreNotEqual(last, a.Lit);
                last = a.Lit;
                a.Tap(a.Lit); b.Tap(b.Lit);
            }
        }

        [Test]
        public void EveryShippedLevel_IsWinnableByAPerfectPlayer()
        {
            for (int i = 0; i < SampleLevels.Count; i++)
            {
                var r = new SampleRules(SampleLevels.Get(i), seed: i * 7919 + 1);
                float time = 0f;
                while (!r.IsOver && time < 600f)
                {
                    r.Tick(0.1f); time += 0.1f;
                    if (r.Lit >= 0) r.Tap(r.Lit);
                }
                Assert.IsTrue(r.Won, $"level {i + 1} '{SampleLevels.Get(i).title}' wins for a perfect player");
                Assert.Less(time, SampleLevels.Get(i).seconds, $"level {i + 1} fits its clock");
            }
        }
    }
}
