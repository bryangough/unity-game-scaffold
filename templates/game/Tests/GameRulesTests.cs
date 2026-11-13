using GameScaffold.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameScaffold.Tests
{
    /// <summary>
    /// The rules, played without a scene: the shape every rule test takes (build the rules, tick, act, assert; a plan
    /// played to a win; each way of losing on its own). A rule change goes in <see cref="GameRules"/> and gets a test here.
    /// </summary>
    public class GameRulesTests
    {
        private static GameLevel Level(int taps = 3, float seconds = 5f) => new GameLevel { title = "test", taps = taps, seconds = seconds };

        [Test]
        public void EnoughTaps_Win()
        {
            var r = new GameRules(Level(taps: 3));
            r.Tap(); r.Tap();
            Assert.IsFalse(r.IsOver);
            r.Tap();
            Assert.IsTrue(r.Won);
            Assert.IsFalse(r.Tap(), "a decided level takes no more taps");
            Assert.AreEqual(3, r.Taps);
        }

        [Test]
        public void TheClockRunningOut_Loses()
        {
            var r = new GameRules(Level(seconds: 2f));
            r.Tick(1.9f);
            Assert.IsFalse(r.IsOver);
            r.Tick(0.2f);
            Assert.IsTrue(r.Lost);
        }

        [Test]
        public void AFastWin_EarnsEveryStar_AndASlowOneOnlyTheFirst()
        {
            var fast = new GameRules(Level(taps: 1, seconds: 10f));
            fast.Tick(1f); fast.Tap();
            Assert.AreEqual(3, Progress.Count(fast.Stars));

            var slow = new GameRules(Level(taps: 1, seconds: 10f));
            slow.Tick(9f); slow.Tap();
            Assert.AreEqual(1, Progress.Count(slow.Stars));
        }

        [Test]
        public void EveryLevel_PassesTheLevelCheck()
        {
            // the same check the Validate Levels menu runs, against a throwaway cache
            string cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GameLevelCheck_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                LevelCheck.Report r = LevelCheck.Run(GameChecks.Check(), cache);
                Assert.AreEqual(GameLevels.Count, r.Rows.Length);
                Assert.AreEqual(0, r.Failed, r.Markdown);
            }
            finally { if (System.IO.Directory.Exists(cache)) System.IO.Directory.Delete(cache, true); }
        }
    }

    /// <summary>The colours a player must tell apart: every pair, under every kind of colour vision. Add each new one here.</summary>
    public class GameColourTests
    {
        [Test]
        public void Palette_EveryPairReadsApart_InEveryVisionMode()
        {
            var close = ColourCheck.ClosePairs(new[] { "idle", "done" }, new[] { GameManager.Idle, GameManager.Done });
            Assert.IsEmpty(close, "Too close:\n" + string.Join("\n", close));
        }
    }
}
