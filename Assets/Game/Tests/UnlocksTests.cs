using System;
using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>Cosmetic unlocks: what opens when, the fallback to the free one, the "new" dot, and the save's side.</summary>
    public class UnlocksTests
    {
        private static UnlockTable Table() => new UnlockTable(
            new Unlock("skin.plain", "Plain", "skin", 0),
            new Unlock("track.one", "One", "track", 0),
            new Unlock("skin.gold", "Gold", "skin", 30),   // listed out of order on purpose
            new Unlock("skin.red", "Red", "skin", 5),
            new Unlock("track.two", "Two", "track", 12));

        [Test]
        public void All_IsInTheOrderTheyAreEarned()
        {
            string[] ids = Array.ConvertAll(Table().All, u => u.Id);
            CollectionAssert.AreEqual(new[] { "skin.plain", "track.one", "skin.red", "track.two", "skin.gold" }, ids);
        }

        [Test]
        public void Next_And_ToNext_CountDownToTheNextUnlock()
        {
            var t = Table();
            Assert.AreEqual("skin.red", t.Next(0).Id);
            Assert.AreEqual(5, t.ToNext(0));
            Assert.AreEqual("track.two", t.Next(5).Id, "exactly the count needed opens it");
            Assert.IsNull(t.Next(30));
            Assert.AreEqual(0, t.ToNext(99));
        }

        [Test]
        public void NewlyOpen_ListsWhatACountJumpOpened()
        {
            var opened = Table().NewlyOpen(4, 12);
            Assert.AreEqual(2, opened.Count);
            Assert.AreEqual("skin.red", opened[0].Id);
            Assert.AreEqual("track.two", opened[1].Id);
            Assert.IsEmpty(Table().NewlyOpen(12, 12));
        }

        [Test]
        public void Resolve_FallsBackToTheFreeOne_WhenTheChoiceIsNotEarnedOrWrongKind()
        {
            var t = Table();
            Assert.AreEqual("skin.red", t.Resolve("skin", "skin.red", 5));
            Assert.AreEqual("skin.plain", t.Resolve("skin", "skin.gold", 5), "not earned yet (a cleared save)");
            Assert.AreEqual("skin.plain", t.Resolve("skin", "track.two", 99), "another kind's id");
            Assert.AreEqual("skin.plain", t.Resolve("skin", null, 99));
            Assert.AreEqual("skin.plain", t.Resolve("skin", "skin.removed", 99), "an id the table dropped");
        }

        [Test]
        public void NewCount_CountsEarnedUnseen_AndNeverTheFreeOnes()
        {
            var t = Table();
            var p = new Progress();
            Assert.AreEqual(0, t.NewCount(0, p.Seen), "free ones are never news");
            Assert.AreEqual(2, t.NewCount(12, p.Seen));
            Assert.IsTrue(t.IsNew("skin.red", 12, p.Seen));
            p.MarkSeen(t.OpenIds(12));
            Assert.AreEqual(0, t.NewCount(12, p.Seen));
            Assert.AreEqual(1, t.NewCount(30, p.Seen), "gold is new");
        }

        [Test]
        public void Table_RefusesDuplicateIds_AndAKindWithNoFreeUnlock()
        {
            Assert.Throws<ArgumentException>(() => new UnlockTable(new Unlock("a", "A", "skin", 0), new Unlock("a", "A", "skin", 3)));
            Assert.Throws<ArgumentException>(() => new UnlockTable(new Unlock("a", "A", "skin", 0), new Unlock("b", "B", "track", 3)));
        }

        [Test]
        public void Progress_KeepsChoicesAndSeen_ThroughTheJson()
        {
            var p = new Progress();
            p.Choose("skin", "skin.red");
            p.Choose("track", "track.two");
            p.Choose("skin", "skin.gold"); // replaces the earlier skin
            p.MarkSeen(new[] { "skin.red" });
            var back = Progress.FromJson(p.ToJson());
            Assert.AreEqual("skin.gold", back.Chosen("skin"));
            Assert.AreEqual("track.two", back.Chosen("track"));
            Assert.IsNull(back.Chosen("backdrop"));
            Assert.IsTrue(back.Seen("skin.red"));
        }

        [Test]
        public void Progress_ReadsASaveFromBeforeUnlocks()
        {
            var old = Progress.FromJson("{\"version\":1,\"lastLevel\":2,\"levels\":[],\"music\":true}");
            Assert.IsNull(old.Chosen("skin"));
            Assert.IsFalse(old.Seen("skin.red"));
            old.Choose("skin", "skin.red"); // and can write to it
            Assert.AreEqual("skin.red", old.Chosen("skin"));
        }
    }
}
