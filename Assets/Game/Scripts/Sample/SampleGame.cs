using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// The sample game's manager: the one MonoBehaviour that presents <see cref="SampleRules"/>. It shows how the kit
    /// hangs together, and is meant to be deleted once your own game is in: the level that is current lives in the save
    /// (so R, a reload and quitting all come back to it), the scene is built by <c>Game > Build Main Scene</c>, the
    /// tiles are primitives, input comes from <see cref="TouchInputAdapter"/> and <see cref="GameInput"/>, feedback
    /// from <see cref="GameFeel"/>, <see cref="Sfx"/> and <see cref="Haptics"/>, the clock from <see cref="FunTestKit"/>,
    /// the save from <see cref="Progress"/>, and the ads / data slot from <see cref="GameServices"/>. The screens
    /// around it (start, level select, result card, pause, settings) are the <see cref="Shell"/>'s: this class hands
    /// it the levels, ticks only while <see cref="Shell.Playing"/> and ends a level with <see cref="Shell.ShowResult"/>.
    /// </summary>
    [DefaultExecutionOrder(-600)] // before FunTestKit (-500), which reads the camera's ortho size in Awake
    public class SampleGame : MonoBehaviour
    {
        public static SampleGame Instance { get; private set; }

        /// <summary>0-based index of the level being played: the save's last level, so it survives a restart.</summary>
        public static int CurrentLevel
        {
            get => Progress.Current.LastLevel;
            set => Progress.Current.LastLevel = value;
        }

        [SerializeField] private Camera cam;
        [SerializeField] private TextMeshProUGUI statusText;

        public SampleRules Rules { get; private set; }
        private SampleLevel level;
        private readonly List<Renderer> tiles = new List<Renderer>();
        private readonly List<Transform> tileTransforms = new List<Transform>();
        private Transform root;
        private int cursor;          // the tile the keys point at
        private bool moved;          // the first tap ends the story callout
        private float startedAt;

        // The sample's colours, checked by ColourBlindTests: lit and dark must read apart in every vision mode.
        // Material colours are sRGB and the pipeline lights in linear space, so a "dark grey" of 0.2 is nearly black
        // once lit: keep albedos in the 0.35–0.9 range and let the light do the darkening.
        public static readonly Color TileDark = new Color(0.38f, 0.42f, 0.52f);
        public static readonly Color TileLit = new Color(1f, 0.85f, 0.25f);
        public static readonly Color TileCursor = new Color(0.45f, 0.75f, 1f);
        public static readonly Color FloorColor = new Color(0.24f, 0.26f, 0.32f);

        private void Awake()
        {
            Instance = this;
            Shell.Catalog = SampleLevels.Catalog;               // before the shell wakes: it draws the level select from this
            if (cam == null) cam = Camera.main;
            level = SampleLevels.Get(CurrentLevel);
            Rules = new SampleRules(level, seed: CurrentLevel * 7919 + 1);
            BuildBoard();
            Frame();
        }

        private void Start()
        {
            PhoneFit.Apply();                                   // 60 fps, screen awake, HUD in the safe area
            GameServices.Current.LevelStarted(CurrentLevel);
            Sfx.PlayMusic("Music1");                            // silent until a track is imported (Editor/SoundPicks.cs)
            if (level.seconds > 0f) FunTestKit.StartTimer(level.seconds, () => { Rules.OutOfTime(); });
            FunTestKit.Story($"<b>{level.title}</b>\n{level.callout}");
            startedAt = Time.time;
            UpdateStatus();
            // the dev menu (DEV tab or F1): what a tester needs mid-play
            for (int i = 0; i < SampleLevels.Count; i++) { int lv = i; DevMenu.Add("Levels", $"Level {lv + 1}", () => LoadLevel(lv)); }
            DevMenu.Add("Play", "Win now", () => { while (!Rules.IsOver) { Rules.Tick(0.5f); if (Rules.Lit >= 0) Rules.Tap(Rules.Lit); } });
            DevMenu.Add("Play", "Lose now", () => { while (!Rules.IsOver) Rules.Tick(level.litSeconds + 0.7f); });
            DevMenu.AddLine("Play", () => $"hits {Rules.Hits}  misses {Rules.Misses}  lit {Rules.Lit}  t {Rules.Elapsed:0.0}");
        }

        private void OnApplicationPause(bool paused) => PhoneFit.SetBackground(paused);
        private void OnApplicationFocus(bool focused) => PhoneFit.SetBackground(!focused);

        // ---------------------------------------------------------------- the board (primitives; a theme would dress them)

        private void BuildBoard()
        {
            root = new GameObject("Board").transform;
            int n = level.grid;
            float half = (n - 1) * 0.5f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            floor.transform.localScale = new Vector3(n + 0.8f, 0.2f, n + 0.8f);
            floor.GetComponent<Renderer>().material = Mat(FloorColor);
            Destroy(floor.GetComponent<Collider>());
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{x}_{z}";
                    tile.transform.SetParent(root, false);
                    tile.transform.localPosition = new Vector3(x - half, 0f, z - half);
                    tile.transform.localScale = new Vector3(0.86f, 0.4f, 0.86f);
                    var r = tile.GetComponent<Renderer>();
                    r.material = Mat(TileDark);
                    tiles.Add(r);
                    tileTransforms.Add(tile.transform);
                }
        }

        private static Material Mat(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>A 3/4 orthographic view that fits the board in a portrait frame (the kit widens it on wider screens).</summary>
        private void Frame()
        {
            if (cam == null) return;
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.transform.position = new Vector3(0f, 12f, -7f);
            float widthNeeded = (level.grid + 1.2f) * 0.5f / FunTestKit.DesignAspect; // half-height for the board's width in a 9:16 frame
            float heightNeeded = (level.grid + 1.2f) * 0.5f * Mathf.Sin(60f * Mathf.Deg2Rad) + 1.2f;
            cam.orthographicSize = Mathf.Max(widthNeeded, heightNeeded);
        }

        // ---------------------------------------------------------------- the loop

        private void Update()
        {
            if (FunTestKit.IsOver) return;                      // the result card has NEXT / RETRY / LEVELS
            for (int i = 0; i < SampleLevels.Count && i < GameInput.SlotKeys; i++)
                if (GameInput.Slot(i).WasPressedThisFrame() && (Application.isEditor || Debug.isDebugBuild)) { LoadLevel(i); return; }

            if (PhoneFit.Paused || !Shell.Playing) return;      // the app is in the background, or a menu is up
            Rules.Tick(Time.deltaTime);
            Sfx.MusicIntensity(level.missesAllowed > 0 ? (float)Rules.Misses / level.missesAllowed : 0f);

            // a tap: the pointer on a tile, or the keys' cursor with Submit
            TouchInputAdapter touch = TouchInputAdapter.Instance;
            if (touch != null && touch.InteractionStartedThisFrame && !FunTestKit.PointerOverButton(touch.ScreenPosition))
            {
                int hit = TileUnder(touch.ScreenPosition);
                if (hit >= 0) { cursor = hit; Tap(hit); }
            }
            Vector2Int dir = GameInput.DirectionPressedThisFrame();
            if (dir != Vector2Int.zero)
            {
                int n = level.grid;
                int x = Mathf.Clamp(cursor % n + dir.x, 0, n - 1);
                int z = Mathf.Clamp(cursor / n + dir.y, 0, n - 1);
                cursor = z * n + x; // InputMode has already switched to Keys or Pad from the press itself
            }
            if (GameInput.Submit.WasPressedThisFrame() && InputMode.UsingKeysOrPad) Tap(cursor);

            Paint();
            if (Rules.Won) Win();
            else if (Rules.Lost) Lose(FunTestKit.TimeLeft <= 0f && level.seconds > 0f ? "Out of time" : "Too many misses");
            UpdateStatus();
        }

        private int TileUnder(Vector2 screen)
        {
            // a view concern (which tile the finger is on), so a ray is fine here; the rule itself never uses physics
            if (cam == null) return -1;
            Ray ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return -1;
            Vector3 p = ray.GetPoint(enter);
            int n = level.grid;
            float half = (n - 1) * 0.5f;
            int x = Mathf.RoundToInt(p.x + half), z = Mathf.RoundToInt(p.z + half);
            if (x < 0 || z < 0 || x >= n || z >= n) return -1;
            if (Mathf.Abs(p.x + half - x) > 0.48f || Mathf.Abs(p.z + half - z) > 0.48f) return -1;
            return z * n + x;
        }

        /// <summary>Where a tile is on screen: a replay bot's finger goes here (<c>SampleChecks</c>).</summary>
        public Vector2 TileScreenPoint(int index) => cam.WorldToScreenPoint(tileTransforms[index].position);

        private void Tap(int index)
        {
            if (!moved) { moved = true; FunTestKit.EndStory(); }
            SampleRules.Outcome o = Rules.Tap(index);
            Transform t = tileTransforms[index];
            switch (o)
            {
                case SampleRules.Outcome.Hit:
                    GameFeel.Punch(t, 0.25f, 0.15f);
                    GameFeel.Burst(t.position + Vector3.up * 0.3f, TileLit, 10, 3f, 0.12f, 0.4f);
                    GameFeel.Popup(t.position + Vector3.up * 0.6f, $"{Rules.LitFor:0.00}s", Color.white, 0.9f);
                    Sfx.Play(Sfx.Id.Step);
                    Haptics.Play(Haptics.Strength.Light);
                    break;
                case SampleRules.Outcome.Miss:
                    GameFeel.Shake(0.12f, 0.15f);
                    GameFeel.Flash(tiles[index], new Color(1f, 0.25f, 0.2f), 0.2f);
                    GameFeel.Popup(t.position + Vector3.up * 0.6f, "miss", new Color(1f, 0.4f, 0.35f), 1.1f);
                    Sfx.Play(Sfx.Id.Hit);
                    Haptics.Play(Haptics.Strength.Medium);
                    break;
            }
        }

        private void Paint()
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                Color c = i == Rules.Lit ? Color.Lerp(TileLit, TileDark, Rules.LitFor / level.litSeconds * 0.5f) : TileDark;
                if (InputMode.UsingKeysOrPad && i == cursor) c = Color.Lerp(c, TileCursor, 0.5f);
                tiles[i].material.color = c;
            }
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            statusText.text = $"Level {CurrentLevel + 1}: {level.title}\nHits {Rules.Hits} / {level.hitsToWin}   Misses {Rules.Misses} / {level.missesAllowed}";
        }

        // ---------------------------------------------------------------- the ends

        private void Win()
        {
            int stars = Rules.Stars;
            int fresh = Progress.Current.RecordWin(CurrentLevel, Mathf.RoundToInt(Rules.AverageReaction * 1000f), stars, higherIsBetter: false);
            GameServices.Current.LevelWon(CurrentLevel, Rules.Hits, Progress.Count(stars), Time.time - startedAt);
            if (CurrentLevel == SampleLevels.Count - 1) GameServices.Current.ChapterFinished(0);
            Sfx.Play(Sfx.Id.Win);
            Haptics.Play(Haptics.Strength.Medium);
            GameFeel.Shake(0.08f, 0.2f);
            GameFeel.Confetti(root != null ? root.position + Vector3.up : Vector3.up);
            Shell.ShowResult(true, stars, $"Average reaction {Rules.AverageReaction:0.00} s{(fresh != 0 ? "   NEW" : "")}");
        }

        private void Lose(string cause)
        {
            GameServices.Current.LevelLost(CurrentLevel, cause);
            Sfx.Play(Sfx.Id.Lose);
            Haptics.Play(Haptics.Strength.Heavy);
            GameFeel.HitStop(0.07f);
            GameFeel.Shake(0.25f, 0.3f);
            Shell.ShowResult(false, 0, cause);
        }

        /// <summary>Opens a level in play: the save remembers it, and the scene reloads around it.</summary>
        public static void LoadLevel(int index) => Shell.OpenLevel(index);

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
