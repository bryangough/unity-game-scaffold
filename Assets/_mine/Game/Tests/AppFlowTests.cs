using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>Back behaves on every screen: overlays pop first, then the screen's own rule.</summary>
    public class AppFlowTests
    {
        private static AppFlow Flow() => new AppFlow()
            .OnBack("Title", BackAction.OpenOverlay, "QuitConfirm")
            .OnBack("Levels", BackAction.GoTo, "Title")
            .OnBack("Play", BackAction.OpenOverlay, "Pause")
            .OnBack("Results", BackAction.Custom, "continue");

        [Test]
        public void Back_PerScreen()
        {
            AppFlow f = Flow();
            f.Enter("Levels");
            Assert.AreEqual(BackAction.GoTo, f.Back(out string target));
            Assert.AreEqual("Title", target);
            f.Enter("Results");
            Assert.AreEqual(BackAction.Custom, f.Back(out target));
            Assert.AreEqual("continue", target);
            f.Enter("Boot");
            Assert.AreEqual(BackAction.None, f.Back(), "a screen with no rule");
        }

        [Test]
        public void Back_OnTitle_AsksBeforeQuitting_AndBackAgainCancels()
        {
            AppFlow f = Flow();
            f.Enter("Title");
            Assert.AreEqual(BackAction.OpenOverlay, f.Back(out string target));
            Assert.AreEqual("QuitConfirm", target);
            Assert.AreEqual("QuitConfirm", f.Top);
            Assert.AreEqual(BackAction.CloseOverlay, f.Back());
            Assert.IsFalse(f.HasOverlay);
        }

        [Test]
        public void Back_InPlay_OpensPause_ThenOverlaysPopInOrder()
        {
            AppFlow f = Flow();
            f.Enter("Play");
            Assert.AreEqual(BackAction.OpenOverlay, f.Back());
            Assert.AreEqual("Pause", f.Top);
            f.Push("Settings"); // Settings from the pause menu
            Assert.AreEqual(BackAction.CloseOverlay, f.Back());
            Assert.AreEqual("Pause", f.Top, "back from settings returns to pause");
            Assert.AreEqual(BackAction.CloseOverlay, f.Back());
            Assert.IsFalse(f.HasOverlay);
            Assert.AreEqual("Play", f.State);
        }

        [Test]
        public void Enter_ClosesOverlays_AndPushIgnoresDuplicateTop()
        {
            AppFlow f = Flow();
            f.Enter("Levels");
            f.Push("Settings");
            f.Push("Settings");
            Assert.AreEqual(1, f.Overlays.Count);
            Assert.IsTrue(f.IsOpen("Settings"));
            f.Enter("Play");
            Assert.IsFalse(f.HasOverlay);
            Assert.IsFalse(f.Pop(), "nothing left to pop");
        }
    }
}
