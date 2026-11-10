using System;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// The slot for ads and data. The game calls these at its moments; the default <see cref="NullServices"/> does
    /// nothing, no SDK is installed, and nothing leaves the device (the store forms can say "Data Not Collected").
    /// Turning a provider on later is one class behind this interface plus the store forms and a privacy policy;
    /// no game code changes. Rename the events to the game's own moments.
    /// </summary>
    public interface IGameServices
    {
        // ---- events (analytics would listen here)
        void LevelStarted(int level);
        void LevelWon(int level, int score, int stars, float seconds);
        void LevelLost(int level, string cause);
        void HintUsed(int level);
        void ChapterFinished(int chapter);

        // ---- ad points
        /// <summary>After a result card, never mid-level, never before level 3, at most one in N levels (the provider's
        /// call). Must call <paramref name="done"/> when the player may go on.</summary>
        void BetweenLevels(int nextLevel, Action done);
        /// <summary>A rewarded ad (a hint is the natural reward); calls <paramref name="granted"/> if earned.</summary>
        void Rewarded(string reason, Action granted);
    }

    /// <summary>The default: nothing is collected, no ad is shown, a reward is granted at once.</summary>
    public class NullServices : IGameServices
    {
        public void LevelStarted(int level) { }
        public void LevelWon(int level, int score, int stars, float seconds) { }
        public void LevelLost(int level, string cause) { }
        public void HintUsed(int level) { }
        public void ChapterFinished(int chapter) { }
        public void BetweenLevels(int nextLevel, Action done) => done?.Invoke();
        public void Rewarded(string reason, Action granted) => granted?.Invoke();
    }

    /// <summary>A test provider: logs every call to the console. <c>Game > Services > Log Calls</c>.</summary>
    public class LogServices : IGameServices
    {
        private static void Log(string s) => Debug.Log("[Services] " + s);
        public void LevelStarted(int level) => Log($"level started {level + 1}");
        public void LevelWon(int level, int score, int stars, float seconds) => Log($"level won {level + 1}: score {score}, {stars} stars, {seconds:0.0} s");
        public void LevelLost(int level, string cause) => Log($"level lost {level + 1}: {cause}");
        public void HintUsed(int level) => Log($"hint used in level {level + 1}");
        public void ChapterFinished(int chapter) => Log($"chapter {chapter + 1} finished");
        public void BetweenLevels(int nextLevel, Action done) { Log($"ad point before level {nextLevel + 1} (none shown)"); done?.Invoke(); }
        public void Rewarded(string reason, Action granted) { Log($"rewarded ad for '{reason}' (granted at once)"); granted?.Invoke(); }
    }

    /// <summary>Where the game reaches the slot.</summary>
    public static class GameServices
    {
        public static readonly string LogPref = GameInfo.Id + ".services.log"; // set by the editor menu

        private static IGameServices current;
        public static IGameServices Current
        {
            get
            {
                if (current == null) current = Application.isEditor && PlayerPrefs.GetInt(LogPref, 0) == 1 ? new LogServices() : (IGameServices)new NullServices();
                return current;
            }
            set => current = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;
    }
}
