#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// <c>Game > Run EditMode Tests</c>: runs the EditMode tests through the Test Runner API and writes the result to
    /// <c>Builds/Checks/tests.md</c> (and the Console). An AI assistant driving the Editor through the Unity MCP cannot
    /// open the Test Runner window and is refused a command that calls the API directly, but it can execute a menu item
    /// and read the report. (<c>tools/unity_batch.py tests</c> runs them headless.)
    /// </summary>
    public static class TestRunnerMenu
    {
        public const string ReportPath = "Builds/Checks/tests.md";

        private class Callbacks : ICallbacks
        {
            private readonly StringBuilder failures = new StringBuilder();
            private int passed, failed;

            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                if (result.TestStatus == TestStatus.Passed) passed++;
                else { failed++; failures.AppendLine($"- FAIL `{result.FullName}`: {result.Message}"); }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, $"# EditMode tests\n\n{System.DateTime.Now:yyyy-MM-dd HH:mm} — {passed} passed, {failed} failed\n\n{failures}");
                if (failed == 0) Debug.Log($"[Tests] {passed} passed, 0 failed");
                else Debug.LogError($"[Tests] {passed} passed, {failed} failed:\n{failures}");
            }
        }

        [MenuItem(Menus.Root + "Run EditMode Tests", false, 52)]
        public static void Run()
        {
            if (File.Exists(ReportPath)) File.Delete(ReportPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            Debug.Log($"[Tests] EditMode tests started; the report goes to {ReportPath}");
        }
    }
}
#endif
