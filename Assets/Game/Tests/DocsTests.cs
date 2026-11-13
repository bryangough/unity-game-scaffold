using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace GameScaffold.Tests
{
    /// <summary>
    /// The assistant's briefing stays true: every script in <c>Scripts/Kit/</c> and <c>Editor/</c> is named in
    /// <c>CLAUDE.md</c> (and, in the scaffold itself, in <c>templates/CLAUDE.md</c>, which becomes a new game's
    /// briefing). An assistant briefed from a stale list rebuilds what is already there; this fails the moment a tool
    /// is added without a line about it.
    /// </summary>
    public class DocsTests
    {
        private static string Root => Path.GetDirectoryName(Application.dataPath);

        private static IEnumerable<string> Briefings()
        {
            yield return "CLAUDE.md";
            if (File.Exists(Path.Combine(Root, "templates", "CLAUDE.md"))) yield return "templates/CLAUDE.md";
        }

        private static IEnumerable<string> Scripts()
        {
            foreach (string folder in new[] { "Assets/Game/Scripts/Kit", "Assets/Game/Editor" })
            {
                string dir = Path.Combine(Root, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    yield return Path.GetFileNameWithoutExtension(file);
            }
        }

        [Test]
        public void Briefing_NamesEveryKitAndEditorScript()
        {
            foreach (string briefing in Briefings())
            {
                string path = Path.Combine(Root, briefing);
                Assert.IsTrue(File.Exists(path), $"{briefing} is missing");
                string text = File.ReadAllText(path);
                var missing = new List<string>();
                foreach (string name in Scripts())
                    if (!Regex.IsMatch(text, $@"\b{Regex.Escape(name)}\b")) missing.Add(name);
                Assert.IsEmpty(missing, $"{briefing} does not mention: {string.Join(", ", missing)}. Add each to its layout table.");
            }
        }

        [Test]
        public void Briefing_PointsAtTheLessons()
        {
            // the game's briefing is the template here, and CLAUDE.md in a game made from the scaffold
            string briefing = File.Exists(Path.Combine(Root, "templates", "CLAUDE.md")) ? "templates/CLAUDE.md" : "CLAUDE.md";
            StringAssert.Contains("docs/lessons.md", File.ReadAllText(Path.Combine(Root, briefing)));
            Assert.IsTrue(File.Exists(Path.Combine(Root, "docs", "lessons.md")), "docs/lessons.md should travel with the project");
        }
    }
}
