#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Checks > Validate Levels</c>: runs the game's check on every level and writes
    /// <c>Builds/Checks/levels.md</c>, one row a level. The game supplies what only it knows (<see cref="Spec"/>: its
    /// levels as text, and a <see cref="Spec.Check"/> that solves one and says whether it is winnable, needs its rule,
    /// starts safe...); this supplies what every game needed around that:
    /// <list type="bullet">
    /// <item><b>A cache per level</b>, keyed by a hash of everything the answer depends on: the level's data, the
    /// budget and the <b>full source of the rules files</b>. Change a level and only it is checked again; change a
    /// rule and every one is. Titles and art stay out of the key. Entries live in <c>Library/LevelCheck/</c>.</item>
    /// <item><b>The misses in parallel</b>, capped at cores − 1 and at most 4 (one big search can hold a few hundred
    /// thousand boards). So <see cref="Spec.Check"/> must be plain C# with no Unity calls and no shared state.</item>
    /// <item><b>The plan kept with the result</b> (<see cref="Row.plan"/>: the solution, as the game writes it), so a
    /// replay or a hint reads it with <see cref="Plan"/> and nothing ever solves a level at Play start.</item>
    /// <item><b>A headless run</b>: <c>tools/unity_batch.py levels</c> exits 1 when a level fails.</item>
    /// </list>
    /// Register the game's spec from an <c>[InitializeOnLoadMethod]</c>: <c>LevelCheck.Game = new LevelCheck.Spec { … }</c>
    /// (the sample's is <c>SampleChecks</c>). From the stealth game, whose check took minutes before it was cached.
    /// </summary>
    public static class LevelCheck
    {
        public const string ReportPath = "Builds/Checks/levels.md";
        public const string CacheDir = "Library/LevelCheck";

        /// <summary>One level's result: a cell per column of the report, and the plan that wins it.</summary>
        [Serializable]
        public sealed class Row
        {
            public bool ok;
            public string[] cells = new string[0];
            /// <summary>The solution, in whatever form the game replays ("RRULD", a list of taps). Empty when it failed.</summary>
            public string plan = "";
            /// <summary>Why it failed, in a few words.</summary>
            public string note = "";

            public static Row Pass(string plan, params string[] cells) => new Row { ok = true, plan = plan ?? "", cells = cells };
            public static Row Fail(string note, params string[] cells) => new Row { ok = false, note = note ?? "", cells = cells };
        }

        /// <summary>What the game tells the check about itself.</summary>
        public sealed class Spec
        {
            public int Count;
            /// <summary>For the report only; never part of the cache key, so a rename re-checks nothing.</summary>
            public Func<int, string> Title = i => "";
            /// <summary>Everything about level <c>i</c> the answer depends on, as text (its map, its numbers).</summary>
            public Func<int, string> Data = i => "";
            /// <summary>The rules' source files (project-relative paths): their full text is part of every key.</summary>
            public string[] RuleFiles = new string[0];
            /// <summary>Anything else the answer depends on: the solver's budget, the check's own version.</summary>
            public string Budget = "";
            /// <summary>The report's columns, matching <see cref="Row.cells"/> ("Par", "Without crates", "Starts safe").</summary>
            public string[] Columns = new string[0];
            /// <summary>Checks one level. Plain C#, no Unity calls, no shared state: misses run on worker threads.</summary>
            public Func<int, Row> Check;
            /// <summary>False runs the misses one after another (a check that is not thread-safe yet).</summary>
            public bool Parallel = true;
        }

        public sealed class Report
        {
            public Row[] Rows;
            public int Failed, Checked, Cached;
            public double Seconds;
            public string Markdown;
        }

        /// <summary>The game's check; set from an <c>[InitializeOnLoadMethod]</c> in the game's editor code.</summary>
        public static Spec Game;

        [MenuItem(Menus.Root + "Checks/Validate Levels", false, 60)]
        public static void Run()
        {
            Report r = RunGame();
            if (r == null) return;
            if (r.Failed == 0) Debug.Log($"[LevelCheck] {r.Rows.Length} levels pass ({r.Cached} from the cache, {r.Checked} checked, {r.Seconds:0.0} s): {ReportPath}");
            else Debug.LogError($"[LevelCheck] {r.Failed} of {r.Rows.Length} levels FAIL: {ReportPath}");
        }

        /// <summary>For a batch-mode Unity (<c>tools/unity_batch.py levels</c>): the check, then quit with 0, or 1 when a level fails.</summary>
        public static void Batch()
        {
            Report r = RunGame();
            EditorApplication.Exit(r != null && r.Failed == 0 ? 0 : 1);
        }

        private static Report RunGame()
        {
            if (Game == null || Game.Check == null)
            {
                Debug.LogError("[LevelCheck] no check registered: set LevelCheck.Game from an [InitializeOnLoadMethod] in the game's editor code (see SampleChecks)");
                return null;
            }
            Report r = Run(Game);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, r.Markdown);
            return r;
        }

        /// <summary>Checks every level of <paramref name="spec"/>, reading and filling the cache in <paramref name="cacheDir"/>.</summary>
        public static Report Run(Spec spec, string cacheDir = CacheDir)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string rules = RulesText(spec);
            int n = Math.Max(0, spec.Count);
            var keys = new string[n];
            var rows = new Row[n];
            var misses = new List<int>();
            Directory.CreateDirectory(cacheDir);
            for (int i = 0; i < n; i++)
            {
                keys[i] = Key(spec, i, rules);
                rows[i] = Load(cacheDir, keys[i]);
                if (rows[i] == null) misses.Add(i);
            }

            var threw = new bool[n];
            void Solve(int i)
            {
                try { rows[i] = spec.Check(i) ?? Row.Fail("the check returned nothing"); }
                catch (Exception e) { rows[i] = Row.Fail("the check threw: " + e.Message); threw[i] = true; }
            }
            if (spec.Parallel && misses.Count > 1)
                Parallel.ForEach(misses, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1)) }, Solve);
            else foreach (int i in misses) Solve(i);

            foreach (int i in misses) if (!threw[i]) Save(cacheDir, keys[i], rows[i]); // a crash is not an answer: it runs again next time
            Prune(cacheDir, keys);

            var report = new Report { Rows = rows, Checked = misses.Count, Cached = n - misses.Count, Failed = rows.Count(r => !r.ok), Seconds = watch.Elapsed.TotalSeconds };
            report.Markdown = Markdown(spec, report);
            return report;
        }

        /// <summary>
        /// The stored plan for a level, or null when the level has not passed the check as it stands (run Validate
        /// Levels). A read of the cache only: nothing is solved here, so it is safe from Play mode and from a replay.
        /// </summary>
        public static string Plan(Spec spec, int level, string cacheDir = CacheDir)
        {
            if (spec == null || level < 0 || level >= spec.Count) return null;
            Row r = Load(cacheDir, Key(spec, level, RulesText(spec)));
            return r != null && r.ok ? r.plan : null;
        }

        /// <summary>The cache key of a level: a SHA1 of its data, the budget, the columns and the rules' source.</summary>
        public static string Key(Spec spec, int level) => Key(spec, level, RulesText(spec));

        private static string Key(Spec spec, int level, string rules)
        {
            string text = spec.Budget + "\n" + string.Join("|", spec.Columns ?? new string[0]) + "\n" + rules + "\n" + (spec.Data != null ? spec.Data(level) : "");
            using (SHA1 sha = SHA1.Create())
            {
                var sb = new StringBuilder(40);
                foreach (byte b in sha.ComputeHash(Encoding.UTF8.GetBytes(text))) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string RulesText(Spec spec)
        {
            var sb = new StringBuilder();
            foreach (string path in spec.RuleFiles ?? new string[0])
            {
                if (!File.Exists(path)) throw new FileNotFoundException($"[LevelCheck] rules file not found: {path} (Spec.RuleFiles are project-relative paths)");
                sb.Append(File.ReadAllText(path).Replace("\r\n", "\n")).Append('\n'); // the same key on every checkout
            }
            return sb.ToString();
        }

        private static Row Load(string dir, string key)
        {
            string path = Path.Combine(dir, key + ".json");
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<Row>(File.ReadAllText(path)); }
            catch (Exception) { return null; }
        }

        private static void Save(string dir, string key, Row row) => File.WriteAllText(Path.Combine(dir, key + ".json"), JsonUtility.ToJson(row));

        /// <summary>Entries no level points at any more (an edited level's old answer) are dropped.</summary>
        private static void Prune(string dir, string[] keys)
        {
            var keep = new HashSet<string>(keys);
            foreach (string file in Directory.GetFiles(dir, "*.json"))
                if (!keep.Contains(Path.GetFileNameWithoutExtension(file))) File.Delete(file);
        }

        private static string Markdown(Spec spec, Report r)
        {
            string[] columns = spec.Columns ?? new string[0];
            var sb = new StringBuilder("# Level check\n\n");
            int n = r.Rows.Length;
            sb.Append($"{DateTime.Now:yyyy-MM-dd HH:mm}: {n} levels, {n - r.Failed} pass, {r.Failed} fail ({r.Cached} from the cache, {r.Checked} checked, {r.Seconds:0.0} s)\n\n");
            sb.Append("| # | Level |");
            foreach (string c in columns) sb.Append(' ').Append(c).Append(" |");
            sb.Append(" Result |\n|---|---|");
            foreach (string _ in columns) sb.Append("---|");
            sb.Append("---|\n");
            for (int i = 0; i < n; i++)
            {
                Row row = r.Rows[i];
                sb.Append($"| {i + 1} | {(spec.Title != null ? spec.Title(i) : "")} |");
                for (int c = 0; c < columns.Length; c++) sb.Append(' ').Append(row.cells != null && c < row.cells.Length ? row.cells[c] : "").Append(" |");
                sb.Append(row.ok ? " ok |\n" : $" **FAIL**: {row.note} |\n");
            }
            return sb.ToString();
        }
    }
}
#endif
