using TMPro;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// The game's manager: the one MonoBehaviour that presents <see cref="GameRules"/>. It builds the current level on
    /// load (<see cref="Shell.CurrentLevel"/>: the level lives in the save, so R, a reload and quitting all come back
    /// to it), ticks the rules only while <see cref="Shell.Playing"/>, and ends a level with
    /// <see cref="Shell.ShowResult"/>; the screens around it are the shell's. Input comes from
    /// <see cref="TouchInputAdapter"/> and <see cref="GameInput"/>, feedback from <see cref="GameFeel"/>,
    /// <see cref="Sfx"/> and <see cref="Haptics"/>. Primitives until the art pick.
    /// </summary>
    [DefaultExecutionOrder(-600)] // before FunTestKit (-500) and the Shell (-400), which draws the level list from the catalogue
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private Camera cam;
        [SerializeField] private TextMeshProUGUI statusText;

        public GameRules Rules { get; private set; }
        private GameLevel level;
        private Renderer block;
        private bool moved;
        private float startedAt;

        // The colours a player must tell apart, checked by GameColourTests under every kind of colour vision.
        // Material colours are sRGB and lit in linear space: keep albedos in the 0.35-0.9 range.
        public static readonly Color Idle = new Color(0.38f, 0.42f, 0.52f);
        public static readonly Color Done = new Color(1f, 0.85f, 0.25f);

        private void Awake()
        {
            Instance = this;
            Shell.Catalog = GameLevels.Catalog;
            if (cam == null) cam = Camera.main;
            level = GameLevels.Get(Shell.CurrentLevel);
            Rules = new GameRules(level);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Block";
            go.transform.localScale = Vector3.one * 2.4f;
            Destroy(go.GetComponent<Collider>());
            block = go.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            block.material = new Material(shader) { color = Idle };
        }

        private void Start()
        {
            PhoneFit.Apply(); // 60 fps, screen awake, HUD in the safe area
            GameServices.Current.LevelStarted(Shell.CurrentLevel);
            FunTestKit.StartTimer(level.seconds, null); // shows the clock; the rules decide the loss
            FunTestKit.Story($"<b>{level.title}</b>\nTap {level.taps} times before the clock runs out.");
            startedAt = Time.time;
            for (int i = 0; i < GameLevels.Count; i++) { int lv = i; DevMenu.Add("Levels", $"Level {lv + 1}", () => Shell.OpenLevel(lv)); }
            DevMenu.Add("Play", "Win now", () => { while (!Rules.IsOver) Rules.Tap(); });
            UpdateStatus();
        }

        private void OnApplicationPause(bool paused) => PhoneFit.SetBackground(paused);
        private void OnApplicationFocus(bool focused) => PhoneFit.SetBackground(!focused);

        private void Update()
        {
            if (FunTestKit.IsOver) return;
            for (int i = 0; i < GameLevels.Count && i < GameInput.SlotKeys; i++)
                if (GameInput.Slot(i).WasPressedThisFrame() && (Application.isEditor || Debug.isDebugBuild)) { Shell.OpenLevel(i); return; }
            if (PhoneFit.Paused || !Shell.Playing) return; // the app is in the background, or a menu is up

            Rules.Tick(Time.deltaTime);

            TouchInputAdapter touch = TouchInputAdapter.Instance;
            bool pressed = touch != null && touch.InteractionStartedThisFrame && !FunTestKit.PointerOverButton(touch.ScreenPosition);
            if (pressed || GameInput.Submit.WasPressedThisFrame()) Tap();

            block.material.color = Color.Lerp(Idle, Done, Rules.Progress01);
            if (Rules.Won) Win();
            else if (Rules.Lost) Lose("Out of time");
            UpdateStatus();
        }

        private void Tap()
        {
            if (!moved) { moved = true; FunTestKit.EndStory(); }
            if (!Rules.Tap()) return;
            GameFeel.Punch(block.transform, 0.2f, 0.12f);
            Sfx.Play(Sfx.Id.Step);
            Haptics.Play(Haptics.Strength.Light);
        }

        private void UpdateStatus()
        {
            if (statusText != null) statusText.text = $"Level {Shell.CurrentLevel + 1}: {level.title}\nTaps {Rules.Taps} / {level.taps}";
        }

        private void Win()
        {
            int stars = Rules.Stars;
            int fresh = Progress.Current.RecordWin(Shell.CurrentLevel, Mathf.RoundToInt(Rules.Elapsed * 1000f), stars, higherIsBetter: false);
            GameServices.Current.LevelWon(Shell.CurrentLevel, Rules.Taps, Progress.Count(stars), Time.time - startedAt);
            Sfx.Play(Sfx.Id.Win);
            Haptics.Play(Haptics.Strength.Medium);
            GameFeel.Confetti(Vector3.up);
            Shell.ShowResult(true, stars, $"{Rules.Elapsed:0.0} s{(fresh != 0 ? "   NEW" : "")}");
        }

        private void Lose(string cause)
        {
            GameServices.Current.LevelLost(Shell.CurrentLevel, cause);
            Sfx.Play(Sfx.Id.Lose);
            Haptics.Play(Haptics.Strength.Heavy);
            GameFeel.Shake(0.25f, 0.3f);
            Shell.ShowResult(false, 0, cause);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
