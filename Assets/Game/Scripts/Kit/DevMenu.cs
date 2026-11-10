using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// A dev menu drawn with IMGUI, so it needs no prefab or scene: a DEV tab on the left edge (or F1) opens a panel
    /// of buttons the game registers (<see cref="Add"/>) and live readouts (<see cref="AddLine"/>): jump to a level,
    /// win now, add currency, clear the save. It exists in the Editor, in development builds, and on a release build
    /// once <see cref="DevUnlocked"/> is set (seven quick taps on the version line in Settings: call <see cref="Tap"/>
    /// from that label). The kit registers the save tools itself. While it is open the pointer is the menu's
    /// (<see cref="TouchInputAdapter"/> sees no presses). From the duel game.
    /// </summary>
    public class DevMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private static readonly List<(string section, string label, Action action)> items = new List<(string, string, Action)>();
        private static readonly List<(string section, Func<string> text)> lines = new List<(string, Func<string>)>();
        private static DevMenu instance;
        private static readonly string UnlockKey = GameInfo.Id + ".dev";

        /// <summary>A release build with the dev menu switched on (seven taps on the version line).</summary>
        public static bool DevUnlocked
        {
            get => PlayerPrefs.GetInt(UnlockKey, 0) == 1;
            set { PlayerPrefs.SetInt(UnlockKey, value ? 1 : 0); PlayerPrefs.Save(); if (value) Ensure(); }
        }

        public static bool Available => Application.isEditor || Debug.isDebugBuild || DevUnlocked;

        /// <summary>A button in the panel. Sections group the buttons; the same label replaces an earlier one.</summary>
        public static void Add(string section, string label, Action action)
        {
            items.RemoveAll(i => i.section == section && i.label == label);
            items.Add((section, label, action));
        }

        /// <summary>A line of text in a section, read every frame the panel is open.</summary>
        public static void AddLine(string section, Func<string> text) => lines.Add((section, text));

        private static int taps;
        private static float lastTap;

        /// <summary>From a version label's click: seven taps inside three seconds unlock the menu on a release build.</summary>
        public static void Tap()
        {
            if (Time.unscaledTime - lastTap > 3f) taps = 0;
            lastTap = Time.unscaledTime;
            if (++taps < 7) return;
            taps = 0;
            if (!DevUnlocked) { DevUnlocked = true; Debug.Log("[DevMenu] unlocked"); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false; instance = null; taps = 0;
            items.Clear(); lines.Clear();
            AddLine("Save", () => $"level {Progress.Current.LastLevel + 1}, {Progress.Current.SolvedCount} solved, {Progress.Current.TotalStars} stars{(Progress.Current.UnlockAll ? ", unlock all" : "")}");
            Add("Save", "Clear save (new player)", () => { Progress.ClearAll(); FunTestKit.Restart(); });
            Add("Save", "Toggle unlock all", () => Progress.Current.UnlockAll = !Progress.Current.UnlockAll);
            Add("Scene", "Reload", FunTestKit.Restart);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create() { if (Available) Ensure(); }

        public static void Ensure()
        {
            if (instance != null || !Application.isPlaying) return;
            var go = new GameObject("DevMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevMenu>();
        }

        public static void Close() { IsOpen = false; }

        private const float RefWidth = 720f;
        private Vector2 scroll;
        private GUIStyle label, button, header, tab;
        private static Texture2D backdrop;

        private static Texture2D Backdrop
        {
            get
            {
                if (backdrop != null) return backdrop;
                backdrop = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                backdrop.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.07f, 0.95f));
                backdrop.Apply();
                return backdrop;
            }
        }

        private void Update()
        {
            if (GameInput.DevMenu.WasPressedThisFrame()) IsOpen = !IsOpen;
        }

        private void OnDisable() => IsOpen = false;

        private void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            header = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 24, fixedHeight = 64f };
            tab = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            float scale = Screen.width / RefWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = RefWidth, h = Screen.height / scale;
            EnsureStyles();
            Rect sa = Screen.safeArea;
            float insetL = sa.x / scale, insetR = (Screen.width - sa.xMax) / scale, insetT = (Screen.height - sa.yMax) / scale, insetB = sa.y / scale;

            if (!IsOpen)
            {
                // a small tab on the left edge, inside the safe area; out of the way of a thumb on the board
                float tabY = Mathf.Clamp(h * 0.42f, insetT, h - insetB - 64f);
                if (GUI.Button(new Rect(insetL, tabY, 64f, 64f), "DEV", tab)) IsOpen = true;
                return;
            }

            GUI.DrawTexture(new Rect(0f, 0f, w, h), Backdrop);
            const float margin = 24f, headerH = 72f, closeW = 180f;
            var panel = new Rect(insetL + margin, insetT + margin, w - insetL - insetR - 2f * margin, h - insetT - insetB - 2f * margin);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && !panel.Contains(e.mousePosition)) { IsOpen = false; e.Use(); return; }

            GUI.Label(new Rect(panel.x, panel.y, panel.width - closeW - 12f, headerH), $"DEV  ·  {GameInfo.Title} v{Application.version}", header);
            if (GUI.Button(new Rect(panel.xMax - closeW, panel.y, closeW, headerH), "CLOSE", button)) { IsOpen = false; return; }

            GUILayout.BeginArea(new Rect(panel.x, panel.y + headerH + 12f, panel.width, panel.height - headerH - 12f));
            scroll = GUILayout.BeginScrollView(scroll);
            var sections = new List<string>();
            foreach ((string section, string _, Action _) in items) if (!sections.Contains(section)) sections.Add(section);
            foreach ((string section, Func<string> _) in lines) if (!sections.Contains(section)) sections.Add(section);
            foreach (string section in sections)
            {
                GUILayout.Space(10f);
                GUILayout.Label(section, header);
                foreach ((string s, Func<string> text) in lines)
                    if (s == section) { string t; try { t = text(); } catch (Exception ex) { t = ex.Message; } GUILayout.Label(t, label); }
                int inRow = 0;
                GUILayout.BeginHorizontal();
                foreach ((string s, string lab, Action action) in items)
                {
                    if (s != section) continue;
                    if (inRow == 2) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); inRow = 0; }
                    if (GUILayout.Button(lab, button)) { try { action(); } catch (Exception ex) { Debug.LogException(ex); } }
                    inRow++;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
