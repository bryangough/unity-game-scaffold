#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The game never goes online (nothing collected, no ads, no sign-in; <c>Store/privacy.md</c>), but Unity adds the
    /// INTERNET permission to every Android build. After Unity writes the Gradle project this drops it from Unity's
    /// library manifest and tells the manifest merger to remove it from anything else, so the app asks for vibration
    /// only. The music still loads: on Android it is read from inside the app (<c>jar:file://</c>), and a credits link
    /// opens the phone's browser, which has its own permission. Delete this file if the game needs the network.
    /// </summary>
    public class AndroidNoInternet : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        private const string Internet = "android.permission.INTERNET";

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string library = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            if (File.Exists(library))
            {
                var kept = new List<string>();
                foreach (string line in File.ReadAllLines(library))
                    if (!line.Contains("\"" + Internet + "\"")) kept.Add(line);
                File.WriteAllLines(library, kept);
            }

            string launcher = Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml");
            if (File.Exists(launcher))
            {
                string text = File.ReadAllText(launcher);
                if (!text.Contains(Internet))
                {
                    if (!text.Contains("xmlns:tools"))
                        text = text.Replace("<manifest ", "<manifest xmlns:tools=\"http://schemas.android.com/tools\" ");
                    int at = text.IndexOf("<application");
                    if (at >= 0) text = text.Insert(at, "<uses-permission android:name=\"" + Internet + "\" tools:node=\"remove\" />\n  ");
                    File.WriteAllText(launcher, text);
                }
            }
            Debug.Log("[AndroidNoInternet] removed the INTERNET permission from the Android build");
        }
    }
}
#endif
