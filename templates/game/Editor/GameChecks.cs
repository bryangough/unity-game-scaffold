#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The game's level check and replay, registered with <see cref="LevelCheck"/> and <see cref="LevelReplay"/>.
    /// The check plays each level in the rules alone, with no scene: a quick player must win with three stars and a
    /// player who does nothing must lose (a level won by doing nothing does not need its rule). The replay then plays
    /// the real scene with a simulated finger. Put the game's solver in <see cref="CheckLevel"/> and store its moves
    /// as the plan; add a column per thing a level must prove ("without crates: cannot win").
    /// </summary>
    public static class GameChecks
    {
        private const float Dt = 0.1f;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            LevelCheck.Game = Check();
            LevelReplay.Game = Replay();
        }

        public static LevelCheck.Spec Check() => new LevelCheck.Spec
        {
            Count = GameLevels.Count,
            Title = i => GameLevels.Get(i).title,
            Data = i => JsonUtility.ToJson(GameLevels.Get(i)),
            RuleFiles = new[] { "Assets/Game/Scripts/GameRules.cs" },
            Budget = "dt " + Dt,
            Columns = new[] { "Quick player", "Does nothing", "Stars in reach" },
            Check = CheckLevel,
        };

        /// <summary>Plain C#, no Unity calls: it runs on a worker thread.</summary>
        private static LevelCheck.Row CheckLevel(int index)
        {
            GameLevel level = GameLevels.Get(index);
            GameRules quick = Simulate(level, taps: true);
            GameRules idle = Simulate(level, taps: false);
            string[] cells =
            {
                quick.Won ? $"wins in {quick.Elapsed:0.0} s" : "cannot win",
                idle.Lost ? $"loses in {idle.Elapsed:0.0} s" : "does not lose",
                $"{Progress.Count(quick.Stars)} of 3",
            };
            if (!quick.Won) return LevelCheck.Row.Fail("a quick player cannot win", cells);
            if (!idle.Lost) return LevelCheck.Row.Fail("doing nothing does not lose: the level does not need its rule", cells);
            if (Progress.Count(quick.Stars) < 3) return LevelCheck.Row.Fail("three stars are out of reach", cells);
            return LevelCheck.Row.Pass($"tap {level.taps} times", cells);
        }

        private static GameRules Simulate(GameLevel level, bool taps)
        {
            var rules = new GameRules(level);
            for (int step = 0; step < 20000 && !rules.IsOver; step++)
            {
                rules.Tick(Dt);
                if (taps) rules.Tap();
            }
            return rules;
        }

        private static bool fingerDown;

        public static ReplayBot.Spec Replay() => new ReplayBot.Spec
        {
            Count = GameLevels.Count,
            Title = i => GameLevels.Get(i).title,
            Plan = i => LevelCheck.Plan(LevelCheck.Game, i), // from the check's cache only; null = not validated yet
            Play = (level, plan, frame) =>
            {
                GameManager game = GameManager.Instance;
                if (game == null || game.Rules == null) return true; // the scene is still coming up
                if (game.Rules.IsOver) return false;
                // a finger in the middle of the screen: down one frame, up the next, so every tap is a fresh press
                if (!fingerDown) { InputBot.TouchDown(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)); fingerDown = true; }
                else { InputBot.TouchUp(); fingerDown = false; }
                return true;
            },
        };
    }
}
#endif
