using System;
using System.IO;
using System.Threading;
using GameScaffold.Editor;
using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>The level check's cache: a level is solved once, again only when it or a rule changes, and a plan is only ever read.</summary>
    public class LevelCheckTests
    {
        private string dir, rulesFile;
        private string[] levels;
        private int calls;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "LevelCheckTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            rulesFile = Path.Combine(dir, "Rules.cs.txt");
            File.WriteAllText(rulesFile, "rule one");
            levels = new[] { "aaa", "bbb", "ccc", "ddd" };
            calls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        private string Cache => Path.Combine(dir, "cache");

        /// <summary>A stand-in game: a level "wins" unless its data starts with x, and its plan is its data backwards.</summary>
        private LevelCheck.Spec Spec() => new LevelCheck.Spec
        {
            Count = levels.Length,
            Title = i => "Level " + levels[i],
            Data = i => levels[i],
            RuleFiles = new[] { rulesFile },
            Budget = "depth 10",
            Columns = new[] { "Length" },
            Check = i =>
            {
                Interlocked.Increment(ref calls);
                string data = levels[i];
                if (data.StartsWith("x")) return LevelCheck.Row.Fail("starts with x", data.Length.ToString());
                char[] plan = data.ToCharArray();
                Array.Reverse(plan);
                return LevelCheck.Row.Pass(new string(plan), data.Length.ToString());
            },
        };

        [Test]
        public void EveryLevel_IsCheckedOnce_ThenComesFromTheCache()
        {
            LevelCheck.Report first = LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(4, first.Checked);
            Assert.AreEqual(0, first.Cached);
            Assert.AreEqual(0, first.Failed);
            Assert.AreEqual(4, calls);

            LevelCheck.Report second = LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(0, second.Checked);
            Assert.AreEqual(4, second.Cached);
            Assert.AreEqual(4, calls, "nothing is solved twice");
            Assert.AreEqual("aaa".Length.ToString(), second.Rows[0].cells[0], "the cells come back from the cache");
        }

        [Test]
        public void ChangingALevel_RechecksOnlyThatLevel()
        {
            LevelCheck.Run(Spec(), Cache);
            levels[2] = "cccc";
            LevelCheck.Report r = LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(1, r.Checked);
            Assert.AreEqual(3, r.Cached);
            Assert.AreEqual(5, calls);
        }

        [Test]
        public void ChangingARule_RechecksEveryLevel()
        {
            LevelCheck.Run(Spec(), Cache);
            File.WriteAllText(rulesFile, "rule one, changed");
            LevelCheck.Report r = LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(4, r.Checked, "an answer under the old rules is not an answer");
        }

        [Test]
        public void ARulesFile_ChecksOutTheSame_WithEitherLineEnding()
        {
            File.WriteAllText(rulesFile, "one\ntwo\n");
            string lf = LevelCheck.Key(Spec(), 0);
            File.WriteAllText(rulesFile, "one\r\ntwo\r\n");
            Assert.AreEqual(lf, LevelCheck.Key(Spec(), 0));
        }

        [Test]
        public void RenamingALevel_RechecksNothing()
        {
            LevelCheck.Run(Spec(), Cache);
            LevelCheck.Spec renamed = Spec();
            renamed.Title = i => "A new name " + i;
            Assert.AreEqual(0, LevelCheck.Run(renamed, Cache).Checked);
        }

        [Test]
        public void AFailingLevel_IsCounted_AndNamedInTheReport()
        {
            levels[1] = "xbb";
            LevelCheck.Report r = LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(1, r.Failed);
            Assert.IsFalse(r.Rows[1].ok);
            StringAssert.Contains("| 2 | Level xbb | 3 | **FAIL**: starts with x |", r.Markdown);
            StringAssert.Contains("| 1 | Level aaa | 3 | ok |", r.Markdown);
        }

        [Test]
        public void ACheckThatThrows_Fails_AndRunsAgainNextTime()
        {
            LevelCheck.Spec spec = Spec();
            spec.Check = i => { Interlocked.Increment(ref calls); throw new InvalidOperationException("boom"); };
            LevelCheck.Report r = LevelCheck.Run(spec, Cache);
            Assert.AreEqual(4, r.Failed);
            StringAssert.Contains("boom", r.Rows[0].note);
            LevelCheck.Run(spec, Cache);
            Assert.AreEqual(8, calls, "a crash is not cached as an answer");
        }

        [Test]
        public void ThePlan_IsReadFromTheCache_AndNeverSolved()
        {
            Assert.IsNull(LevelCheck.Plan(Spec(), 0, Cache), "not checked yet: no plan");
            Assert.AreEqual(0, calls, "asking for a plan solves nothing");
            LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual("aaa", LevelCheck.Plan(Spec(), 0, Cache));
            Assert.AreEqual("ddd", LevelCheck.Plan(Spec(), 3, Cache));
            levels[3] = "dxy";
            Assert.IsNull(LevelCheck.Plan(Spec(), 3, Cache), "the level changed: its old plan is not offered");
            Assert.AreEqual(4, calls);
        }

        [Test]
        public void AFailedLevel_HasNoPlan()
        {
            levels[0] = "xaa";
            LevelCheck.Run(Spec(), Cache);
            Assert.IsNull(LevelCheck.Plan(Spec(), 0, Cache));
        }

        [Test]
        public void OldAnswers_AreDropped_WhenNoLevelPointsAtThem()
        {
            LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(4, Directory.GetFiles(Cache, "*.json").Length);
            levels[0] = "a2";
            LevelCheck.Run(Spec(), Cache);
            Assert.AreEqual(4, Directory.GetFiles(Cache, "*.json").Length, "one new entry, the stale one pruned");
        }

        [Test]
        public void OneAfterAnother_GivesTheSameAnswers()
        {
            LevelCheck.Spec serial = Spec();
            serial.Parallel = false;
            LevelCheck.Report r = LevelCheck.Run(serial, Cache);
            Assert.AreEqual(4, r.Checked);
            Assert.AreEqual(0, r.Failed);
        }
    }
}
