using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// The game around the levels, the same in every game: a start screen (PLAY goes to the next unsolved level), a
    /// level select with stars, a result card (stars landing, NEXT / RETRY / LEVELS), a pause, settings (music, sound,
    /// vibration, reset) and what Back does on each (<see cref="AppFlow"/>; Esc, Android Back, the pad's B and Start).
    /// It builds its own canvas, so the scene builder drops one GameObject with this component. Flat boxes until a
    /// <see cref="UISkin"/> is wired in; keys and a pad work on every screen and a focus ring shows only to them.
    /// <para>What the game does: hand over its levels (<see cref="Catalog"/>, in its manager's Awake), build the level
    /// in <see cref="CurrentLevel"/> on load, tick only while <see cref="Playing"/>, and end a level with
    /// <see cref="ShowResult"/>. Levels open through <see cref="OpenLevel"/>: the level goes into the save and the
    /// scene reloads around it, so R, a reload and quitting all come back to it.</para>
    /// <para>The screen it is on survives the scene reload (a static), which is how RETRY and NEXT land in play and R
    /// on the title stays on the title. A new player skips the start screen. The game's own screens (a collection, a
    /// how-to card) are made from <see cref="ShellUI"/> and pushed on <see cref="Flow"/> as overlays.</para>
    /// </summary>
    [DefaultExecutionOrder(-400)] // after FunTestKit (-500), which makes the EventSystem
    public class Shell : MonoBehaviour
    {
        // screens
        public const string Title = "Title", Levels = "Levels", Play = "Play", Result = "Result";
        // overlays
        public const string Pause = "Pause", Settings = "Settings", Confirm = "Confirm";

        public const int Columns = 4, Rows = 5, PerPage = Columns * Rows;
        private const float Tile = 144f, Gap = 16f;

        public static Shell Instance { get; private set; }
        /// <summary>Which screen is on and which overlays are over it. Lives across scene reloads.</summary>
        public static AppFlow Flow { get; private set; }
        /// <summary>The game's levels; set in the game manager's Awake. Without one the shell treats the game as one level.</summary>
        public static ILevelCatalog Catalog;
        /// <summary>A check that presses every key (<c>InputCheck</c>) sets this so the menus do not react meanwhile.</summary>
        public static bool IgnoreInput;

        private static int resumeFrame;

        /// <summary>The game ticks and reads input only while this is true: in play, nothing over it. Always true in a
        /// scene with no shell. False for the frame a menu closes, so the press that closed it is not also a move.</summary>
        public static bool Playing => Instance == null || (Flow.State == Play && !Flow.HasOverlay && Time.frameCount > resumeFrame);

        public static int LevelCount => Catalog != null ? Mathf.Max(1, Catalog.Count) : 1;
        /// <summary>The level to build: the save's last level, kept inside the catalogue.</summary>
        public static int CurrentLevel => Mathf.Clamp(Progress.Current.LastLevel, 0, LevelCount - 1);

        [Tooltip("Optional: the look of the screens. Flat boxes without one.")]
        public UISkin skin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; Flow = null; Catalog = null; IgnoreInput = false; resumeFrame = 0; Changed = null; }

        // ---------------------------------------------------------------- the rules (plain, tested by ShellTests)

        /// <summary>What Back does on each screen. Only a platform with somewhere to quit to asks about quitting.</summary>
        public static AppFlow NewFlow(bool canQuit)
        {
            AppFlow f = new AppFlow()
                .OnBack(Levels, BackAction.GoTo, Title)
                .OnBack(Play, BackAction.OpenOverlay, Pause)
                .OnBack(Result, BackAction.GoTo, Levels);
            if (canQuit) f.OnBack(Title, BackAction.OpenOverlay, Confirm);
            return f;
        }

        /// <summary>A new player goes straight into level 1; everyone else sees the start screen.</summary>
        public static string FirstScreen(bool newPlayer) => newPlayer ? Play : Title;

        /// <summary>The screen a scene reload lands on: a finished level reloads into play, the rest stay where they were.</summary>
        public static string ScreenAfterReload(string state) => state == Result || string.IsNullOrEmpty(state) ? Play : state;

        private static bool CanQuit =>
            Application.platform == RuntimePlatform.Android ||
            (!Application.isMobilePlatform && !Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer);

        // ---------------------------------------------------------------- what the game calls

        /// <summary>Opens a level in play: the save remembers it and the scene reloads around it (behind a fade).</summary>
        public static void OpenLevel(int index)
        {
            Progress.Current.LastLevel = Mathf.Clamp(index, 0, LevelCount - 1);
            Flow?.Enter(Play);
            if (Instance != null) ScreenFade.Transition(FunTestKit.Restart);
            else FunTestKit.Restart();
        }

        /// <summary>
        /// The level is over: the result card, with <paramref name="stars"/> (the bits earned this run, as
        /// <see cref="Progress.RecordWin"/> takes them) landing one by one. Record the win first. Without a shell in
        /// the scene the kit's own banner shows instead.
        /// </summary>
        public static void ShowResult(bool won, int stars, string line)
        {
            FunTestKit.ShowResult(won, line, showPanel: Instance == null);
            if (Instance == null) return;
            Flow.Enter(Result);
            Instance.FillResult(won, Progress.Count(stars), line);
            Instance.Apply();
        }

        /// <summary>Opens one of the game's own overlays (or Pause / Settings) over the current screen.</summary>
        public static void Open(string overlay)
        {
            if (Instance == null) return;
            Flow.Push(overlay);
            Instance.Apply();
        }

        /// <summary>Shows a screen by name (for a check or a store pose): <c>Shell.Go(Shell.Levels)</c>.</summary>
        public static void Go(string screen)
        {
            if (Instance == null) return;
            if (screen == Levels) Instance.page = LevelPages.PageOf(CurrentLevel, PerPage);
            Flow.Enter(screen);
            Instance.Apply();
        }

        /// <summary>Raised after every change of screen or overlay, so a game's own overlay can show and hide itself.</summary>
        public static event Action Changed;

        // ---------------------------------------------------------------- the screens

        private GameObject hud, titleScreen, levelsScreen, resultScreen, pausePanel, settingsPanel, confirmPanel;
        private CanvasGroup titleGroup, levelsGroup, resultGroup;
        private Button playButton, levelsButton, resumeButton, backButton, pagePrev, pageNext;
        private Button resultPrimary, resultRetry, resultLevels, musicButton, soundButton, hapticsButton, confirmNo;
        private TextMeshProUGUI titleStars, pageLabel, resultCaption, resultTitle, resultLine, pauseCaption, confirmText;
        private readonly Button[] tiles = new Button[PerPage];
        private readonly Vector2[] tileHome = new Vector2[PerPage];
        private readonly TextMeshProUGUI[] tileStars = new TextMeshProUGUI[PerPage];
        private readonly TextMeshProUGUI[] starLabels = new TextMeshProUGUI[3];
        private Action resultAction, confirmAction;
        private Coroutine landing;
        private int page;
        private bool wasPlaying;

        private Color TitleCol => skin != null ? skin.title : ShellUI.TitleColor;
        private Color TextCol => skin != null ? skin.text : Color.white;
        private Color SoftCol => skin != null ? skin.soft : ShellUI.Soft;
        private Color StarCol => skin != null ? skin.starOn : ShellUI.TitleColor;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            bool newPlayer = Progress.Current != null && Progress.NewPlayer;
            if (Flow == null) { Flow = NewFlow(CanQuit); Flow.Enter(FirstScreen(newPlayer)); }
            else Flow.Enter(ScreenAfterReload(Flow.State)); // a reload closes every overlay
            if (skin != null && skin.tmpFont != null) UIText.Use(skin.tmpFont);
            Build();
            InputMode.Changed += OnInputMode;
        }

        private void Start()
        {
            FunTestKit.ShowRestart(false); // the pause has RESTART
            page = LevelPages.PageOf(CurrentLevel, PerPage);
            Apply();
        }

        private void OnDestroy()
        {
            InputMode.Changed -= OnInputMode;
            if (Instance == this) { Instance = null; Changed = null; }
        }

        private void OnInputMode(InputMode.Mode m) => Focus();

        private void Build()
        {
            var canvasGo = new GameObject("Shell_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // over the HUD and the kit (100), under the fade (500)
            PrototypeUIHelper.SetupScaler(canvasGo.AddComponent<CanvasScaler>());
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform root = canvasGo.transform;

            BuildHud(root);
            BuildTitle(root);
            BuildLevels(root);
            BuildResult(root);
            BuildPause(root);
            BuildSettings(root);
            BuildConfirm(root);
            SafeAreaPanel.Wrap(canvas); // buttons clear of a notch and the home bar; the dims bleed past it
        }

        /// <summary>A full screen. Its backdrop is solid by default: the level's HUD and callout would read through a dim.</summary>
        private GameObject MakeScreen(Transform root, string name, out CanvasGroup group, float dimAlpha = 1f)
        {
            RectTransform r = ShellUI.Fill(root, name);
            group = r.gameObject.AddComponent<CanvasGroup>();
            ShellUI.Dim(r, skin, dimAlpha);
            return r.gameObject;
        }

        private GameObject MakePanel(Transform root, string name, Vector2 size, string heading, out Transform card)
        {
            RectTransform r = ShellUI.Fill(root, name);
            ShellUI.Dim(r, skin);
            card = ShellUI.Card(r, skin, size).transform;
            if (heading != null) Heading(card, heading, 52f, new Vector2(0f, size.y * 0.5f - 70f), size.x - 60f);
            return r.gameObject;
        }

        private TextMeshProUGUI Heading(Transform parent, string text, float size, Vector2 position, float width)
        {
            TextMeshProUGUI t = ShellUI.Label(parent, "Text_Heading", text, size, TitleCol, ShellUI.Centre, position, new Vector2(width, size * 1.4f), skin);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        private Button Btn(Transform parent, string name, string label, UISkin.Role role, Vector2 position, Vector2 size, Action onClick, float labelSize = ShellUI.ButtonLabelSize) =>
            ShellUI.MakeButton(parent, name, label, role, ShellUI.Centre, position, size, onClick, skin, labelSize);

        private void BuildHud(Transform root)
        {
            hud = ShellUI.Fill(root, "HUD").gameObject;
            Button pause = ShellUI.MakeButton(hud.transform, "Button_Pause", "II", UISkin.Role.Plain, Vector2.one,
                new Vector2(-62f, -62f), new Vector2(88f, 88f), () => Open(Pause), skin);
            pause.navigation = new Navigation { mode = Navigation.Mode.None }; // never takes the focus from the game
        }

        private void BuildTitle(Transform root)
        {
            titleScreen = MakeScreen(root, "Screen_Title", out titleGroup);
            Transform t = titleScreen.transform;
            if (skin != null && skin.titleLogo != null)
            {
                RectTransform lr = ShellUI.Box(t, "Logo", ShellUI.Centre, new Vector2(0f, 330f), new Vector2(560f, 280f));
                var img = lr.gameObject.AddComponent<Image>();
                img.sprite = skin.titleLogo;
                img.preserveAspect = true;
                img.raycastTarget = false;
                lr.gameObject.AddComponent<Breathe>();
            }
            else
            {
                TextMeshProUGUI title = ShellUI.Label(t, "Text_Title", GameInfo.Title, 84f, TitleCol, ShellUI.Centre, new Vector2(0f, 330f), new Vector2(640f, 230f), skin);
                title.fontStyle = FontStyles.Bold;
                title.enableAutoSizing = true;
                title.fontSizeMin = 44f;
                title.fontSizeMax = 84f;
                title.gameObject.AddComponent<Breathe>();
            }
            titleStars = ShellUI.Label(t, "Text_Stars", "", 30f, SoftCol, ShellUI.Centre, new Vector2(0f, 168f), new Vector2(600f, 50f), skin);
            playButton = Btn(t, "Button_Play", "PLAY", UISkin.Role.Primary, new Vector2(0f, 20f), new Vector2(420f, 120f), PlayNext, 48f);
            levelsButton = Btn(t, "Button_Levels", "LEVELS", UISkin.Role.Plain, new Vector2(0f, -130f), new Vector2(420f, 96f), () => Go(Levels));
            Btn(t, "Button_Settings", "SETTINGS", UISkin.Role.Plain, new Vector2(0f, -246f), new Vector2(420f, 96f), () => Open(Settings));

            // the credit and version line: seven quick taps unlock the dev menu on a release build
            RectTransform vr = ShellUI.Box(t, "Button_Version", new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(640f, 64f));
            var hit = vr.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var vb = vr.gameObject.AddComponent<Button>();
            vb.transition = Selectable.Transition.None;
            vb.navigation = new Navigation { mode = Navigation.Mode.None };
            vb.onClick.AddListener(DevMenu.Tap);
            ShellUI.Label(vr, "Text_Version", $"{GameInfo.Credit}  ·  v{Application.version}", ShellUI.CaptionSize, SoftCol, ShellUI.Centre, Vector2.zero, new Vector2(640f, 64f), skin);
        }

        private void BuildLevels(Transform root)
        {
            levelsScreen = MakeScreen(root, "Screen_Levels", out levelsGroup);
            Transform t = levelsScreen.transform;
            TextMeshProUGUI head = ShellUI.Label(t, "Text_Heading", "LEVELS", 56f, TitleCol, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(600f, 90f), skin);
            head.fontStyle = FontStyles.Bold;

            float gridW = Columns * Tile + (Columns - 1) * Gap, gridH = Rows * Tile + (Rows - 1) * Gap;
            RectTransform grid = ShellUI.Box(t, "Grid", ShellUI.Centre, new Vector2(0f, 42f), new Vector2(gridW, gridH));
            for (int i = 0; i < PerPage; i++)
            {
                int slot = i;
                float x = -gridW * 0.5f + Tile * 0.5f + (i % Columns) * (Tile + Gap);
                float y = gridH * 0.5f - Tile * 0.5f - (i / Columns) * (Tile + Gap);
                Button b = ShellUI.MakeButton(grid, "Tile_" + i, "", UISkin.Role.Plain, ShellUI.Centre, new Vector2(x, y), new Vector2(Tile, Tile), () => PickTile(slot), skin, 46f);
                Image face = ShellUI.Face(b);
                if (skin != null && skin.levelTile != null) { face.sprite = skin.levelTile; face.type = Image.Type.Sliced; }
                ShellUI.LabelOf(b).rectTransform.offsetMin = new Vector2(10f, 40f); // the number sits over the stars
                if (skin != null) ShellUI.LabelOf(b).color = skin.levelText;
                tileStars[i] = ShellUI.Label(face.transform, "Text_Stars", "", 24f, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(Tile, 36f), skin);
                tiles[i] = b;
                tileHome[i] = new Vector2(x, y);
            }

            Vector2 bottom = new Vector2(0.5f, 0f);
            pagePrev = ShellUI.MakeButton(t, "Button_PagePrev", "<", UISkin.Role.Plain, bottom, new Vector2(-200f, 200f), new Vector2(96f, 88f), () => TurnPage(-1), skin, 40f);
            pageNext = ShellUI.MakeButton(t, "Button_PageNext", ">", UISkin.Role.Plain, bottom, new Vector2(200f, 200f), new Vector2(96f, 88f), () => TurnPage(1), skin, 40f);
            pageLabel = ShellUI.Label(t, "Text_Page", "", 30f, SoftCol, bottom, new Vector2(0f, 200f), new Vector2(260f, 60f), skin);
            backButton = ShellUI.MakeButton(t, "Button_Back", "BACK", UISkin.Role.Plain, bottom, new Vector2(0f, 90f), new Vector2(420f, 96f), DoBack, skin);
        }

        private void BuildResult(Transform root)
        {
            resultScreen = MakeScreen(root, "Screen_Result", out resultGroup, 0.6f); // the finished board still shows
            Transform card = ShellUI.Card(resultScreen.transform, skin, new Vector2(600f, 640f)).transform;
            resultCaption = ShellUI.Label(card, "Text_Level", "", 26f, SoftCol, ShellUI.Centre, new Vector2(0f, 264f), new Vector2(540f, 44f), skin);
            resultTitle = Heading(card, "", 64f, new Vector2(0f, 196f), 540f);
            for (int i = 0; i < 3; i++)
                starLabels[i] = ShellUI.Label(card, "Star_" + i, UIText.StarOff, 88f, Color.white, ShellUI.Centre, new Vector2((i - 1) * 120f, 96f), new Vector2(120f, 110f), skin);
            resultLine = ShellUI.Label(card, "Text_Line", "", ShellUI.BodySize, TextCol, ShellUI.Centre, new Vector2(0f, -8f), new Vector2(520f, 96f), skin);
            resultLine.enableAutoSizing = true;
            resultLine.fontSizeMin = 24f;
            resultLine.fontSizeMax = ShellUI.BodySize;
            resultPrimary = Btn(card, "Button_Primary", "NEXT", UISkin.Role.Primary, new Vector2(0f, -122f), new Vector2(440f, 108f), () => resultAction?.Invoke(), 42f);
            resultRetry = Btn(card, "Button_Retry", "RETRY", UISkin.Role.Plain, new Vector2(-115f, -244f), new Vector2(210f, 92f), Retry);
            resultLevels = Btn(card, "Button_Levels", "LEVELS", UISkin.Role.Plain, new Vector2(115f, -244f), new Vector2(210f, 92f), () => Go(Levels));
        }

        private void BuildPause(Transform root)
        {
            pausePanel = MakePanel(root, "Panel_Pause", new Vector2(520f, 640f), "PAUSED", out Transform card);
            pauseCaption = ShellUI.Label(card, "Text_Level", "", 26f, SoftCol, ShellUI.Centre, new Vector2(0f, 192f), new Vector2(460f, 44f), skin);
            resumeButton = Btn(card, "Button_Resume", "RESUME", UISkin.Role.Primary, new Vector2(0f, 96f), new Vector2(400f, 104f), Resume, 40f);
            Btn(card, "Button_Restart", "RESTART", UISkin.Role.Plain, new Vector2(0f, -18f), new Vector2(400f, 92f), Retry);
            Btn(card, "Button_Levels", "LEVELS", UISkin.Role.Plain, new Vector2(0f, -120f), new Vector2(400f, 92f), () => Go(Levels));
            Btn(card, "Button_Settings", "SETTINGS", UISkin.Role.Plain, new Vector2(0f, -222f), new Vector2(400f, 92f), () => Open(Settings));
        }

        private void BuildSettings(Transform root)
        {
            settingsPanel = MakePanel(root, "Panel_Settings", new Vector2(560f, 780f), "SETTINGS", out Transform card);
            Vector2 size = new Vector2(440f, 88f);
            musicButton = Btn(card, "Button_Music", "", UISkin.Role.Plain, new Vector2(0f, 210f), size, () => { Progress.Current.Music = !Progress.Current.Music; FillSettings(); });
            soundButton = Btn(card, "Button_Sound", "", UISkin.Role.Plain, new Vector2(0f, 110f), size, () => { Progress.Current.Sound = !Progress.Current.Sound; FillSettings(); });
            hapticsButton = Btn(card, "Button_Vibration", "", UISkin.Role.Plain, new Vector2(0f, 10f), size, () =>
            {
                Progress.Current.Haptics = !Progress.Current.Haptics;
                Haptics.Play(Haptics.Strength.Light); // felt when it comes on, silent when it goes off
                FillSettings();
            });
            Btn(card, "Button_Reset", "RESET PROGRESS", UISkin.Role.Danger, new Vector2(0f, -110f), size, () =>
                Ask("Reset all progress?\nStars and bests are cleared; the settings stay.", () =>
                {
                    Progress.ClearAll();
                    Flow.Enter(Title);
                    FunTestKit.Restart();
                }));
            Btn(card, "Button_Back", "BACK", UISkin.Role.Plain, new Vector2(0f, -220f), size, Resume);

            string credit = GameInfo.Credit;
            if (!string.IsNullOrEmpty(GameInfo.Website))
                credit += $"\n<link=\"https://{GameInfo.Website}\"><u><color=#{ColorUtility.ToHtmlStringRGB(TitleCol)}>{GameInfo.Website}</color></u></link>";
            TextMeshProUGUI credits = ShellUI.Label(card, "Text_Credits", credit, ShellUI.CaptionSize, SoftCol, ShellUI.Centre, new Vector2(0f, -326f), new Vector2(480f, 72f), skin);
            credits.raycastTarget = true; // the link is tapped
            credits.gameObject.AddComponent<TextLinkOpener>();
        }

        private void BuildConfirm(Transform root)
        {
            confirmPanel = MakePanel(root, "Panel_Confirm", new Vector2(580f, 380f), null, out Transform card);
            confirmText = ShellUI.Label(card, "Text_Question", "", 30f, TextCol, ShellUI.Centre, new Vector2(0f, 70f), new Vector2(500f, 170f), skin);
            Btn(card, "Button_Yes", "YES", UISkin.Role.Danger, new Vector2(-125f, -110f), new Vector2(210f, 96f), () =>
            {
                Action a = confirmAction;
                Flow.Pop();
                Apply();
                a?.Invoke();
            });
            confirmNo = Btn(card, "Button_No", "NO", UISkin.Role.Plain, new Vector2(125f, -110f), new Vector2(210f, 96f), Resume);
        }

        // ---------------------------------------------------------------- showing them

        /// <summary>Shows what <see cref="Flow"/> says: the screen, the top overlay, the pause button, the focus.</summary>
        public void Apply()
        {
            string state = Flow.State, top = Flow.Top;
            bool playing = state == Play && top == null;
            if (playing && !wasPlaying) resumeFrame = Time.frameCount;
            wasPlaying = playing;

            titleScreen.SetActive(state == Title);
            levelsScreen.SetActive(state == Levels);
            resultScreen.SetActive(state == Result);
            titleGroup.interactable = levelsGroup.interactable = resultGroup.interactable = top == null; // no focus under an overlay
            pausePanel.SetActive(top == Pause);
            settingsPanel.SetActive(top == Settings);
            confirmPanel.SetActive(top == Confirm);
            hud.SetActive(playing && !FunTestKit.IsOver);

            FunTestKit.Hold = !playing; // the level clock waits under a menu
            Sfx.Ducked = !playing;

            if (state == Title)
            {
                titleStars.text = Catalog != null ? $"<color=#{ColorUtility.ToHtmlStringRGB(StarCol)}>{UIText.StarOn}</color> {Progress.Current.TotalStars} / {LevelCount * 3}" : "";
                levelsButton.gameObject.SetActive(LevelCount > 1);
            }
            if (state == Levels) FillLevels();
            if (top == Pause) pauseCaption.text = Caption(CurrentLevel);
            if (top == Settings) FillSettings();
            Focus();
            Changed?.Invoke();
        }

        private static string Caption(int level)
        {
            string title = Catalog != null ? Catalog.Title(level) : null;
            return string.IsNullOrEmpty(title) ? $"Level {level + 1}" : $"Level {level + 1}: {title}";
        }

        private void FillLevels()
        {
            int n = LevelCount;
            page = LevelPages.Clamp(page, n, PerPage);
            (int first, int count) = LevelPages.Range(page, n, PerPage);
            Progress p = Progress.Current;
            float shift = count < Columns ? (Columns - count) * (Tile + Gap) * 0.5f : 0f; // a short single row sits in the middle
            for (int i = 0; i < PerPage; i++)
            {
                bool on = i < count;
                tiles[i].gameObject.SetActive(on);
                if (!on) continue;
                ((RectTransform)tiles[i].transform).anchoredPosition = tileHome[i] + new Vector2(shift, 0f);
                int level = first + i;
                LevelRecord r = p.Level(level);
                bool open = p.IsUnlocked(level);
                ShellUI.SetLabel(tiles[i], (level + 1).ToString());
                tileStars[i].text = r.solved ? UIText.Stars(Progress.Count(r.stars), 3, StarCol, 1f) : "";
                tiles[i].interactable = open;
                ShellUI.Face(tiles[i]).color = TileColour(open, level == CurrentLevel, r.solved);
            }
            int pages = LevelPages.PageCount(n, PerPage);
            pagePrev.gameObject.SetActive(pages > 1);
            pageNext.gameObject.SetActive(pages > 1);
            pageLabel.gameObject.SetActive(pages > 1);
            pageLabel.text = $"{page + 1} / {pages}";
            pagePrev.interactable = page > 0;
            pageNext.interactable = page < pages - 1;
        }

        private Color TileColour(bool open, bool current, bool solved)
        {
            if (skin != null && skin.levelTile != null)
                return !open ? skin.levelLockedTint : current ? skin.levelCurrentTint : solved ? skin.levelSolvedTint : skin.levelOpenTint;
            return !open ? new Color(0.3f, 0.3f, 0.34f, 0.8f) : current ? new Color(0.55f, 0.43f, 0.14f) : solved ? new Color(0.16f, 0.4f, 0.3f) : ShellUI.Plain;
        }

        private void FillSettings()
        {
            Progress p = Progress.Current;
            ShellUI.SetLabel(musicButton, "MUSIC: " + (p.Music ? "ON" : "OFF"));
            ShellUI.SetLabel(soundButton, "SOUND: " + (p.Sound ? "ON" : "OFF"));
            ShellUI.SetLabel(hapticsButton, "VIBRATION: " + (p.Haptics ? "ON" : "OFF"));
        }

        private void FillResult(bool won, int starCount, string line)
        {
            int level = CurrentLevel;
            bool hasNext = level < LevelCount - 1;
            resultCaption.text = Caption(level);
            resultTitle.text = won ? "YOU WIN" : "YOU LOSE";
            resultTitle.color = won ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
            resultLine.text = line ?? "";
            foreach (TextMeshProUGUI s in starLabels)
            {
                s.gameObject.SetActive(won);
                s.text = UIText.StarOff;
                s.color = new Color(1f, 1f, 1f, 0.4f);
                s.rectTransform.localScale = Vector3.one;
            }

            // one main button: NEXT after a win, RETRY after a loss, LEVELS when the last level is won
            Vector2 left = new Vector2(-115f, -244f), right = new Vector2(115f, -244f), middle = new Vector2(0f, -244f);
            Vector2 half = new Vector2(210f, 92f), wide = new Vector2(440f, 92f);
            if (won && hasNext)
            {
                ShellUI.SetLabel(resultPrimary, "NEXT");
                resultAction = () => { int next = CurrentLevel + 1; GameServices.Current.BetweenLevels(next, () => OpenLevel(next)); };
                Place(resultRetry, true, left, half);
                Place(resultLevels, true, right, half);
            }
            else if (won)
            {
                ShellUI.SetLabel(resultPrimary, "LEVELS");
                resultAction = () => Go(Levels);
                Place(resultRetry, true, middle, wide);
                Place(resultLevels, false, right, half);
            }
            else
            {
                ShellUI.SetLabel(resultPrimary, "RETRY");
                resultAction = Retry;
                Place(resultRetry, false, left, half);
                Place(resultLevels, true, middle, wide);
            }

            if (landing != null) StopCoroutine(landing);
            landing = won ? StartCoroutine(LandStars(starCount)) : null;
        }

        private static void Place(Button b, bool on, Vector2 position, Vector2 size)
        {
            b.gameObject.SetActive(on);
            var r = (RectTransform)b.transform;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        private IEnumerator LandStars(int count)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            for (int i = 0; i < count && i < starLabels.Length; i++)
            {
                TextMeshProUGUI s = starLabels[i];
                s.text = UIText.StarOn;
                s.color = StarCol;
                Sfx.Play(Sfx.Id.Star);
                for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
                {
                    s.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t / 0.18f);
                    yield return null;
                }
                s.rectTransform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.15f);
            }
            landing = null;
        }

        /// <summary>A key or pad player gets a focused button on every screen; a finger or a mouse gets none.</summary>
        private void Focus()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject want = null;
            if (InputMode.UsingKeysOrPad && !IgnoreInput)
            {
                string state = Flow.State, top = Flow.Top;
                if (top == Confirm) want = confirmNo.gameObject;
                else if (top == Settings) want = musicButton.gameObject;
                else if (top == Pause) want = resumeButton.gameObject;
                else if (top != null) return; // a game's own overlay looks after its focus
                else if (state == Title) want = playButton.gameObject;
                else if (state == Result) want = resultPrimary.gameObject;
                else if (state == Levels)
                {
                    int slot = CurrentLevel - page * PerPage;
                    want = slot >= 0 && slot < PerPage && tiles[slot].gameObject.activeSelf && tiles[slot].interactable ? tiles[slot].gameObject : backButton.gameObject;
                }
            }
            if (es.currentSelectedGameObject != want) es.SetSelectedGameObject(want);
        }

        // ---------------------------------------------------------------- what the buttons and keys do

        private void Update()
        {
            EventSystem es = EventSystem.current;
            if (IgnoreInput)
            {
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                return;
            }
            if (ScreenFade.IsFading || DevMenu.IsOpen) return;

            bool inPlay = Flow.State == Play && !Flow.HasOverlay;
            bool back = GameInput.Back.WasPressedThisFrame(), pause = GameInput.Pause.WasPressedThisFrame();
            if (pause && !back && Flow.Top == Pause) Resume();            // Start closes the pause it opened
            else if (back || (pause && inPlay)) DoBack();                 // Esc is both; Android Back arrives as Esc

            if (Flow.HasOverlay) { KeepFocus(es); return; }
            if (Flow.State == Levels)
            {
                if (GameInput.PagePrev.WasPressedThisFrame()) TurnPage(-1);
                if (GameInput.PageNext.WasPressedThisFrame()) TurnPage(1);
            }
            // N, Enter, Space or the pad's A with nothing focused (a mouse player): the screen's main button.
            // With a button focused the UI module presses it, so this stays out of the way.
            if (GameInput.Next.WasPressedThisFrame() && (es == null || es.currentSelectedGameObject == null))
            {
                if (Flow.State == Result) resultAction?.Invoke();
                else if (Flow.State == Title) PlayNext();
            }
            KeepFocus(es);
        }

        /// <summary>A click on empty space drops the selection; a key player gets it back.</summary>
        private void KeepFocus(EventSystem es)
        {
            if (es == null || !InputMode.UsingKeysOrPad || (Flow.State == Play && !Flow.HasOverlay)) return;
            GameObject sel = es.currentSelectedGameObject;
            if (sel == null || !sel.activeInHierarchy) Focus();
        }

        private void DoBack()
        {
            BackAction action = Flow.Back(out string target);
            if (action == BackAction.None) return;
            if (action == BackAction.GoTo)
            {
                if (target == Levels) page = LevelPages.PageOf(CurrentLevel, PerPage);
                Flow.Enter(target);
            }
            else if (action == BackAction.OpenOverlay && target == Confirm)
            {
                confirmText.text = "Quit the game?";
                confirmAction = Application.Quit;
            }
            Sfx.Play(Sfx.Id.Back);
            Apply();
        }

        private void Resume()
        {
            Flow.Pop();
            Apply();
        }

        private void Ask(string question, Action yes)
        {
            confirmText.text = question;
            confirmAction = yes;
            Open(Confirm);
        }

        private void Retry() => OpenLevel(CurrentLevel);

        /// <summary>PLAY: the next unsolved level from where the player was. The level already behind the start screen just starts.</summary>
        private void PlayNext()
        {
            int target = Catalog != null ? Progress.Current.NextUnsolved(LevelCount) : CurrentLevel;
            if (target == CurrentLevel && !FunTestKit.IsOver) Go(Play);
            else OpenLevel(target);
        }

        private void PickTile(int slot) => OpenLevel(page * PerPage + slot);

        private void TurnPage(int by)
        {
            int to = LevelPages.Clamp(page + by, LevelCount, PerPage);
            if (to == page) return;
            page = to;
            Apply();
        }
    }
}
