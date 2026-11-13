using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>The screens around the game: what Back does on each, where a launch and a reload land, and the level select's pages.</summary>
    public class ShellTests
    {
        [Test]
        public void Back_InPlay_OpensThePause_AndBackAgainClosesIt()
        {
            AppFlow f = Shell.NewFlow(canQuit: false);
            f.Enter(Shell.Play);
            Assert.AreEqual(BackAction.OpenOverlay, f.Back(out string target));
            Assert.AreEqual(Shell.Pause, target);
            Assert.AreEqual(Shell.Pause, f.Top);
            Assert.AreEqual(BackAction.CloseOverlay, f.Back());
            Assert.IsFalse(f.HasOverlay);
        }

        [Test]
        public void Back_ClosesSettingsBeforeThePauseUnderIt()
        {
            AppFlow f = Shell.NewFlow(canQuit: false);
            f.Enter(Shell.Play);
            f.Push(Shell.Pause);
            f.Push(Shell.Settings);
            f.Back();
            Assert.AreEqual(Shell.Pause, f.Top, "settings opened from the pause goes back to the pause");
        }

        [Test]
        public void Back_WalksResultToLevelsToTitle()
        {
            AppFlow f = Shell.NewFlow(canQuit: false);
            f.Enter(Shell.Result);
            Assert.AreEqual(BackAction.GoTo, f.Back(out string target));
            Assert.AreEqual(Shell.Levels, target);
            f.Enter(target);
            Assert.AreEqual(BackAction.GoTo, f.Back(out target));
            Assert.AreEqual(Shell.Title, target);
        }

        [Test]
        public void Back_OnTheTitle_AsksAboutQuitting_OnlyWhereThereIsSomewhereToQuitTo()
        {
            AppFlow web = Shell.NewFlow(canQuit: false);
            web.Enter(Shell.Title);
            Assert.AreEqual(BackAction.None, web.Back(), "a web page or an iPhone has no quit");

            AppFlow android = Shell.NewFlow(canQuit: true);
            android.Enter(Shell.Title);
            Assert.AreEqual(BackAction.OpenOverlay, android.Back(out string target));
            Assert.AreEqual(Shell.Confirm, target);
        }

        [Test]
        public void ANewPlayer_SkipsTheStartScreen()
        {
            Assert.AreEqual(Shell.Play, Shell.FirstScreen(newPlayer: true));
            Assert.AreEqual(Shell.Title, Shell.FirstScreen(newPlayer: false));
        }

        [Test]
        public void AReload_LandsInPlayAfterAResult_AndStaysPutOtherwise()
        {
            Assert.AreEqual(Shell.Play, Shell.ScreenAfterReload(Shell.Result), "RETRY and NEXT reload into the level");
            Assert.AreEqual(Shell.Play, Shell.ScreenAfterReload(Shell.Play), "R in a level restarts it");
            Assert.AreEqual(Shell.Title, Shell.ScreenAfterReload(Shell.Title), "R on the start screen stays there");
            Assert.AreEqual(Shell.Levels, Shell.ScreenAfterReload(Shell.Levels));
        }

        [TestCase(0, 20, 1)]
        [TestCase(1, 20, 1)]
        [TestCase(20, 20, 1)]
        [TestCase(21, 20, 2)]
        [TestCase(100, 20, 5)]
        public void PageCount(int levels, int perPage, int pages)
        {
            Assert.AreEqual(pages, LevelPages.PageCount(levels, perPage));
        }

        [Test]
        public void TheLastPage_MayBeShort_AndAPagePastTheEndIsTheLast()
        {
            Assert.AreEqual((0, 20), LevelPages.Range(0, 45, 20));
            Assert.AreEqual((40, 5), LevelPages.Range(2, 45, 20));
            Assert.AreEqual((40, 5), LevelPages.Range(9, 45, 20), "clamped");
            Assert.AreEqual((0, 3), LevelPages.Range(0, 3, 20), "the sample's three levels");
            Assert.AreEqual((0, 0), LevelPages.Range(0, 0, 20));
        }

        [Test]
        public void TheLevelSelect_OpensOnTheCurrentLevelsPage()
        {
            Assert.AreEqual(0, LevelPages.PageOf(19, 20));
            Assert.AreEqual(1, LevelPages.PageOf(20, 20));
            Assert.AreEqual(2, LevelPages.PageOf(44, 20));
        }
    }
}
