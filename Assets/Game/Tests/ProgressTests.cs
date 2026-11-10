using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>The save: stars, the best score, the level order, and the JSON round trip.</summary>
    public class ProgressTests
    {
        [Test]
        public void RecordWin_KeepsTheBestScore_AndEveryStarEverEarned()
        {
            var p = new Progress();
            int fresh = p.RecordWin(0, 120, Progress.Star1 | Progress.Star2);
            Assert.AreEqual(Progress.Star1 | Progress.Star2, fresh);
            fresh = p.RecordWin(0, 90, Progress.Star1 | Progress.Star3);
            Assert.AreEqual(Progress.Star3, fresh, "only the third star is new");
            Assert.AreEqual(7, p.Level(0).stars, "stars from different runs add up");
            Assert.AreEqual(120, p.Level(0).best, "higher is better by default");
            Assert.AreEqual(3, p.TotalStars);
            Assert.AreEqual(1, p.SolvedCount);
        }

        [Test]
        public void RecordWin_LowerIsBetter_ForMovesOrTimes()
        {
            var p = new Progress();
            p.RecordWin(2, 14, Progress.Star1, higherIsBetter: false);
            p.RecordWin(2, 20, Progress.Star1, higherIsBetter: false);
            Assert.AreEqual(14, p.Level(2).best, "a worse run never replaces the best");
            p.RecordWin(2, 10, Progress.Star1, higherIsBetter: false);
            Assert.AreEqual(10, p.Level(2).best);
        }

        [Test]
        public void Count_CountsStarBits()
        {
            Assert.AreEqual(3, Progress.Count(7));
            Assert.AreEqual(1, Progress.Count(Progress.Star1));
            Assert.AreEqual(2, Progress.Count(Progress.Star1 | Progress.Star3));
        }

        [Test]
        public void EveryLevelIsOpen_AndPlayGoesOnFromTheLastLevelPlayed()
        {
            var p = new Progress();
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsTrue(p.IsUnlocked(29), "levels are open by default (Progress.LockLevels is false)");
            Assert.AreEqual(0, p.NextUnsolved(30));
            p.RecordWin(0, 1, Progress.Star1);
            Assert.AreEqual(1, p.NextUnsolved(30));
            // stuck on level 2 (index 1), the player skips to level 5 and wins it: PLAY goes on to 6, not back to 2
            p.LastLevel = 4;
            p.RecordWin(4, 1, Progress.Star1);
            Assert.AreEqual(5, p.NextUnsolved(30));
            // past the end it wraps round to the level still unsolved
            for (int i = 5; i < 30; i++) p.RecordWin(i, 1, Progress.Star1);
            p.LastLevel = 29;
            Assert.AreEqual(1, p.NextUnsolved(30));
        }

        [Test]
        public void FirstTime_IsTrueOnce()
        {
            var p = new Progress();
            Assert.IsTrue(p.FirstTime(1));
            Assert.IsFalse(p.FirstTime(1));
            Assert.IsTrue(p.FirstTime(2), "another flag is its own first time");
            Assert.AreEqual(3, p.Flags);
        }

        [Test]
        public void Json_RoundTrips_AndABrokenOrNewerSaveStartsFresh()
        {
            var p = new Progress();
            p.RecordWin(3, 14, 7);
            p.LastLevel = 4;
            p.Music = false;
            Progress q = Progress.FromJson(p.ToJson());
            Assert.IsTrue(q.Level(3).solved);
            Assert.AreEqual(14, q.Level(3).best);
            Assert.AreEqual(7, q.Level(3).stars);
            Assert.AreEqual(4, q.LastLevel);
            Assert.IsFalse(q.Music);

            Assert.AreEqual(0, Progress.FromJson("not json {").TotalStars);
            Assert.AreEqual(0, Progress.FromJson("").LastLevel);
            string newer = p.ToJson().Replace("\"version\":" + Progress.Version, "\"version\":" + (Progress.Version + 1));
            Assert.AreEqual(0, Progress.FromJson(newer).TotalStars, "a save from a newer version is not trusted");
        }
    }
}
