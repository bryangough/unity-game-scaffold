using System.Collections.Generic;
using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>Missions: a few up at a time, progress, completion once, the next one up, the 24 h skip.</summary>
    public class MissionBookTests
    {
        private struct Run { public int hits, misses; public bool won; public float seconds; }

        private class Store : IMissionStore
        {
            private readonly Dictionary<string, int> d = new Dictionary<string, int>();
            public int GetInt(string key, int fallback) => d.TryGetValue(key, out int v) ? v : fallback;
            public void SetInt(string key, int value) => d[key] = value;
            public void DeleteKey(string key) => d.Remove(key);
        }

        private static readonly MissionBook<Run>.Mission[] Pool =
        {
            new MissionBook<Run>.Mission("hits30", "Hit 30 tiles", 30, true, r => r.hits),
            new MissionBook<Run>.Mission("clean", "Win without a miss", 1, false, r => r.won && r.misses == 0 ? 1 : 0),
            new MissionBook<Run>.Mission("fast", "Win in under 20 s", 1, false, r => r.won && r.seconds < 20f ? 1 : 0),
            new MissionBook<Run>.Mission("hits12", "12 hits in one run", 12, false, r => r.hits),
            new MissionBook<Run>.Mission("wins3", "Win 3 times", 3, true, r => r.won ? 1 : 0),
        };

        private static MissionBook<Run> Book() => new MissionBook<Run>(Pool, new Store(), 3, "test");

        [Test]
        public void NewGameShowsTheFirstThree()
        {
            MissionBook<Run>.Mission[] a = Book().Active(0);
            Assert.AreEqual("hits30", a[0].id);
            Assert.AreEqual("clean", a[1].id);
            Assert.AreEqual("fast", a[2].id);
        }

        [Test]
        public void ProgressAddsUpAcrossRuns_ForThatKind()
        {
            MissionBook<Run> b = Book();
            Assert.AreEqual(0, b.Submit(new Run { hits = 10 }, 0).Count);
            Assert.AreEqual(0, b.Submit(new Run { hits = 15 }, 0).Count);
            Assert.AreEqual(25, b.Progress(Pool[0]));
            List<MissionBook<Run>.Mission> done = b.Submit(new Run { hits = 5 }, 0);
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual("hits30", done[0].id);
        }

        [Test]
        public void OneRunMissionsDoNotAddUp()
        {
            MissionBook<Run> b = Book();
            b.Submit(new Run { won = true, misses = 1 }, 0);
            Assert.AreEqual(0, b.Submit(new Run { won = true, misses = 2 }, 0).Count);
            Assert.AreEqual(1, b.Submit(new Run { won = true, misses = 0, seconds = 30f }, 0).Count, "clean");
        }

        [Test]
        public void AMissionCompletesOnce_AndTheNextComesUp()
        {
            MissionBook<Run> b = Book();
            b.Submit(new Run { won = true, misses = 0, seconds = 30f }, 0);
            Assert.AreEqual(1, b.Stars);
            Assert.AreEqual("hits12", b.Active(0)[1].id, "the next unused one took the slot");
            Assert.AreEqual(0, b.Submit(new Run { won = true, misses = 0, seconds = 30f }, 0).Count, "clean is no longer up");
            Assert.AreEqual(1, b.Stars);
        }

        [Test]
        public void SeveralInOneRun()
        {
            MissionBook<Run> b = Book();
            List<MissionBook<Run>.Mission> done = b.Submit(new Run { hits = 30, won = true, misses = 0, seconds = 10f }, 0);
            Assert.AreEqual(3, done.Count);
            Assert.AreEqual(3, b.Stars);
            MissionBook<Run>.Mission[] a = b.Active(0);
            Assert.AreEqual("hits12", a[0].id);
            Assert.AreEqual("wins3", a[1].id);
            Assert.AreEqual("hits30", a[2].id, "round the pool again once every other is up");
        }

        [Test]
        public void SkipNeeds24Hours()
        {
            MissionBook<Run> b = Book();
            b.Active(1000);
            Assert.IsFalse(b.CanSkip(0, 1000 + 3600));
            Assert.IsFalse(b.Skip(0, 1000 + 3600));
            Assert.IsTrue(b.CanSkip(0, 1000 + MissionBook<Run>.SkipAfterSeconds));
            Assert.IsTrue(b.Skip(0, 1000 + MissionBook<Run>.SkipAfterSeconds));
            Assert.AreEqual("hits12", b.Active(0)[0].id);
            Assert.IsFalse(b.CanSkip(0, 1000 + MissionBook<Run>.SkipAfterSeconds + 10), "the new one has its own clock");
        }

        [Test]
        public void ClearStartsOver()
        {
            MissionBook<Run> b = Book();
            b.Submit(new Run { hits = 30, won = true, misses = 0, seconds = 10f }, 0);
            b.Clear();
            Assert.AreEqual(0, b.Stars);
            Assert.AreEqual("hits30", b.Active(0)[0].id);
            Assert.AreEqual(0, b.Progress(Pool[0]));
        }
    }
}
