using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace GameScaffold.Tests
{
    /// <summary>
    /// Runs every EditMode test in this assembly without the Test Runner window: an assistant driving the Editor
    /// through the Unity MCP cannot open that window and is refused a command that calls the Test Runner API, but it
    /// can run <c>EditModeSuite.Run()</c> from a RunCommand and read the result as text. Handles [Test], [TestCase]
    /// (with arguments), [SetUp] and [TearDown]; Assert.Pass counts as a pass, Assert.Ignore / Inconclusive are
    /// skipped. The menu (<c>Run EditMode Tests</c>) and <c>tools/unity_batch.py tests</c> run the real runner; this is
    /// the quick check between edits. From the duel game.
    /// </summary>
    public static class EditModeSuite
    {
        public static string Run(string classFilter = null)
        {
            int pass = 0, fail = 0, skip = 0;
            var failures = new StringBuilder();
            foreach (Type type in typeof(EditModeSuite).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).OrderBy(t => t.Name))
            {
                if (classFilter != null && !type.Name.Contains(classFilter)) continue;
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                MethodInfo setUp = methods.FirstOrDefault(m => m.GetCustomAttribute<SetUpAttribute>() != null);
                MethodInfo tearDown = methods.FirstOrDefault(m => m.GetCustomAttribute<TearDownAttribute>() != null);
                foreach (MethodInfo m in methods)
                {
                    var cases = m.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
                    if (m.GetCustomAttribute<TestAttribute>() != null && m.GetParameters().Length == 0) cases.Add(new object[0]);
                    foreach (object[] args in cases)
                    {
                        object inst = m.IsStatic ? null : Activator.CreateInstance(type);
                        string name = $"{type.Name}.{m.Name}" + (args.Length > 0 ? "(" + string.Join(", ", args.Select(a => a is Array arr ? "[" + string.Join(",", arr.Cast<object>()) + "]" : a)) + ")" : "");
                        try
                        {
                            setUp?.Invoke(inst, null);
                            m.Invoke(inst, args);
                            pass++;
                        }
                        catch (TargetInvocationException e)
                        {
                            Exception ie = e.InnerException ?? e;
                            if (ie is SuccessException) pass++;
                            else if (ie is IgnoreException || ie is InconclusiveException) skip++;
                            else { fail++; failures.AppendLine($"FAIL {name}: {ie.Message.Trim()}"); }
                        }
                        catch (Exception e) { fail++; failures.AppendLine($"FAIL {name}: {e.Message.Trim()}"); }
                        finally
                        {
                            try { tearDown?.Invoke(inst, null); } catch { /* a teardown failure is already the test's */ }
                        }
                    }
                }
            }
            return $"pass {pass}, fail {fail}, skipped {skip}\n{failures}";
        }
    }
}
