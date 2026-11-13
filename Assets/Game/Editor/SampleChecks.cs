#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The sample game's level check and replay, registered with <see cref="LevelCheck"/> and <see cref="LevelReplay"/>:
    /// the pattern for a game's own, and deleted with the sample. The check plays each level in the rules alone, with
    /// no scene: a perfect player must win inside the clock with three stars, and a player who never taps must lose
    /// (a level that is won by doing nothing does not need its rule). The replay then plays the real scene with a
    /// simulated finger. A puzzle game's check would run its solver here and store the moves as the plan.
    /// </summary>
    public static class SampleChecks
    {
        private const float Dt = 0.05f;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            LevelCheck.Game = Check();
            LevelReplay.Game = Replay();
        }

        public static LevelCheck.Spec Check() => new LevelCheck.Spec
        {
            Count = SampleLevels.Count,
            Title = i => SampleLevels.Get(i).title,
            Data = i => JsonUtility.ToJson(SampleLevels.Get(i)),    // every number of the level; a new title re-checks it too, which is cheap here
            RuleFiles = new[] { "Assets/Game/Scripts/Sample/SampleRules.cs" },
            Budget = "dt " + Dt,
            Columns = new[] { "Perfect player", "Never taps", "Stars in reach" },
            Check = CheckLevel,
        };

        /// <summary>Plain C#, no Unity calls: it runs on a worker thread.</summary>
        private static LevelCheck.Row CheckLevel(int index)
        {
            SampleLevel level = SampleLevels.Get(index);
            SampleRules perfect = Simulate(level, index, taps: true);
            SampleRules idle = Simulate(level, index, taps: false);
            string[] cells =
            {
                perfect.Won ? $"wins in {perfect.Elapsed:0.0} s" : "cannot win",
                idle.Lost ? $"loses in {idle.Elapsed:0.0} s" : "does not lose",
                $"{Progress.Count(perfect.Stars)} of 3",
            };
            if (!perfect.Won) return LevelCheck.Row.Fail("a perfect player cannot win", cells);
            if (level.seconds > 0f && perfect.Elapsed > level.seconds) return LevelCheck.Row.Fail("a perfect player runs out of clock", cells);
            if (!idle.Lost) return LevelCheck.Row.Fail("doing nothing does not lose: the level does not need its rule", cells);
            if (Progress.Count(perfect.Stars) < 3) return LevelCheck.Row.Fail("three stars are out of reach", cells);
            return LevelCheck.Row.Pass($"tap the lit tile {level.hitsToWin} times", cells);
        }

        private static SampleRules Simulate(SampleLevel level, int index, bool taps)
        {
            var rules = new SampleRules(level, seed: index * 7919 + 1); // the seed the game uses
            for (int step = 0; step < 20000 && !rules.IsOver; step++)
            {
                rules.Tick(Dt);
                if (taps && rules.Lit >= 0) rules.Tap(rules.Lit);
            }
            return rules;
        }

        private static bool fingerDown;

        public static ReplayBot.Spec Replay() => new ReplayBot.Spec
        {
            Count = SampleLevels.Count,
            Title = i => SampleLevels.Get(i).title,
            Plan = i => LevelCheck.Plan(LevelCheck.Game, i),         // from the check's cache only; null = not validated yet
            Timeout = 60f,
            Play = (level, plan, frame) =>
            {
                SampleGame game = SampleGame.Instance;
                if (game == null || game.Rules == null) return true;  // the scene is still coming up
                if (game.Rules.IsOver) return false;
                // a finger: down on the lit tile one frame, up the next, so every tap is a fresh press
                if (!fingerDown && game.Rules.Lit >= 0) { InputBot.TouchDown(game.TileScreenPoint(game.Rules.Lit)); fingerDown = true; }
                else { InputBot.TouchUp(); fingerDown = false; }
                return true;
            },
        };
    }
}
#endif
