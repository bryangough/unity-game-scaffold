using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace GameScaffold
{
    /// <summary>
    /// The fun-test kit, kept in the full game: a countdown timer (optional), a WIN / LOSE result banner, a callout
    /// line, restart (a button and R), the EventSystem with Space as Submit, and the aspect fit for an orthographic
    /// camera framed for <see cref="DesignAspect"/>. Builds its own overlay canvas at runtime, so a scene builder
    /// only drops one GameObject with this component (<c>FunTestKitBuilder.AddKit()</c>). Game managers call
    /// <see cref="StartTimer"/> / <see cref="ShowResult"/> and check <see cref="IsOver"/>; in the Editor every result
    /// is appended to <c>Logs/FunTest.txt</c>, which a bot (or the next session) reads back.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class FunTestKit : MonoBehaviour
    {
        public static FunTestKit Instance { get; private set; }

        /// <summary>True once a result has been shown. Gameplay should stop reacting to input.</summary>
        public static bool IsOver => Instance != null && Instance.over;

        /// <summary>Seconds left on the countdown (or 0 if no timer).</summary>
        public static float TimeLeft => Instance != null ? Instance.timeLeft : 0f;

        public static event Action<bool> OnResult;
        /// <summary>Optional override for the R key while the result is up (go to the next level after a win, say).</summary>
        public static Action OnRestartKey;
        /// <summary>While true the countdown waits (a menu is over the game: <see cref="Shell"/> sets it).</summary>
        public static bool Hold;

        private bool over;
        private GameObject restartButton;
        private bool timerRunning;
        private float timeLeft;
        private Action onExpired;

        // Aspect fit: scenes are framed for 16:9. In a narrower Game view (portrait, square) an ortho
        // camera would crop the sides, so widen the view to keep the same world width visible.
        private Camera fitCam;
        private float designSize;
        /// <summary>The aspect the scenes are framed for (portrait 9:16 by default; 16:9 for a landscape game). A view
        /// narrower than this widens an orthographic camera so the designed width stays visible.</summary>
        public static float DesignAspect = 9f / 16f;

        private TextMeshProUGUI timerText;
        private TextMeshProUGUI calloutText;
        private float calloutUntil;
        private bool storyShowing;
        private GameObject resultPanel;
        private TextMeshProUGUI resultTitle;
        private TextMeshProUGUI resultLine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Hold = false;
            Application.runInBackground = true;
            EnsureEventSystem();
            BuildUI();
            fitCam = Camera.main;
            if (fitCam != null && fitCam.orthographic) designSize = fitCam.orthographicSize;
        }

        private void LateUpdate()
        {
            // Space as Enter on the menus: re-applied if anything swaps the UI module or its actions after Awake
            InputSystemUIInputModule ui = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            if (ui != null) SpaceSubmits(ui);
            if (fitCam == null || designSize <= 0f || !fitCam.orthographic) return;
            float aspect = Mathf.Max(0.1f, fitCam.aspect);
            fitCam.orthographicSize = aspect < DesignAspect ? designSize * DesignAspect / aspect : designSize;
        }

        /// <summary>
        /// True only when the pointer is over a clickable UI control (button, toggle...). Plain HUD text
        /// and hint panels do not block gameplay presses (IsPointerOverGameObject counts every Text).
        /// </summary>
        public static bool PointerOverButton(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits)
            {
                if (h.gameObject.GetComponentInParent<Selectable>() != null) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; OnResult = null; OnRestartKey = null; }
        }

        /// <summary>The RESTART button: hidden on a start screen (it has PLAY), shown in play.</summary>
        public static void ShowRestart(bool on)
        {
            if (Instance != null && Instance.restartButton != null && !Instance.over) Instance.restartButton.SetActive(on);
        }

        // ---------------------------------------------------------------- API

        public static void StartTimer(float seconds, Action expired)
        {
            if (Instance == null) return;
            Instance.timeLeft = seconds;
            Instance.onExpired = expired;
            Instance.timerRunning = true;
            Instance.timerText.gameObject.SetActive(true);
        }

        /// <summary>Big centred message near the top for a few seconds (instructions, "+12%!", etc.).</summary>
        public static void Callout(string message, float seconds = 2f)
        {
            if (Instance == null) return;
            Instance.calloutText.text = message;
            Instance.calloutUntil = Time.time + seconds;
            Instance.storyShowing = false;
        }

        /// <summary>
        /// A level's story line: it stays up until the player first moves, then <see cref="EndStory"/> clears it.
        /// Another callout in the meantime replaces it.
        /// </summary>
        public static void Story(string message)
        {
            if (Instance == null) return;
            Instance.calloutText.text = message;
            Instance.calloutUntil = float.PositiveInfinity;
            Instance.storyShowing = true;
        }

        /// <summary>The player has moved: the story goes (a short callout shown over it is left alone).</summary>
        public static void EndStory()
        {
            if (Instance == null || !Instance.storyShowing) return;
            Instance.storyShowing = false;
            Instance.calloutUntil = Time.time + 0.6f; // a beat more, so it doesn't vanish under the thumb
        }

        public static bool StoryShowing => Instance != null && Instance.storyShowing;

        /// <summary>Add (or remove) seconds on a running countdown.</summary>
        public static void AddTime(float seconds)
        {
            if (Instance == null || !Instance.timerRunning) return;
            Instance.timeLeft = Mathf.Max(0f, Instance.timeLeft + seconds);
        }

        public static void StopTimer()
        {
            if (Instance != null) Instance.timerRunning = false;
        }

        /// <summary>Ends the round. <paramref name="showPanel"/> false: the game draws its own result card.</summary>
        public static void ShowResult(bool won, string line, bool showPanel = true)
        {
            if (Instance == null || Instance.over) return;
            Instance.over = true;
            Instance.timerRunning = false;
            Instance.resultPanel.SetActive(showPanel);
            Instance.resultTitle.text = won ? "YOU WIN" : "YOU LOSE";
            Instance.resultTitle.color = won ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
            Instance.resultLine.text = (line ?? "") + "\n\n<size=80%>Press R or RESTART</size>";
            OnResult?.Invoke(won);
            Instance.WriteDevLog((won ? "WIN" : "LOSE") + " | " + line);
        }

        /// <summary>Editor-only: append a line to Logs/FunTest.txt so automated runs can be read back.</summary>
        public static void DevLog(string line)
        {
            if (Instance != null) Instance.WriteDevLog(line);
        }

        private void WriteDevLog(string line)
        {
#if UNITY_EDITOR
            try
            {
                System.IO.File.AppendAllText("Logs/FunTest.txt",
                    $"{SceneManager.GetActiveScene().name} t={Time.timeSinceLevelLoad:F1} {line}" + System.Environment.NewLine);
            }
            catch { }
#endif
        }

        public static void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ---------------------------------------------------------------- loop

        private void Update()
        {
            if (GameInput.Restart.WasPressedThisFrame())
            {
                if (over && OnRestartKey != null) { Action cb = OnRestartKey; OnRestartKey = null; cb(); return; }
                Restart();
                return;
            }

            if (timerRunning && !over && !Hold)
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    timerRunning = false;
                    var cb = onExpired;
                    onExpired = null;
                    cb?.Invoke();
                }
            }

            if (timerText.gameObject.activeSelf)
            {
                timerText.text = Mathf.CeilToInt(timeLeft).ToString();
                timerText.color = timeLeft <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }

            if (calloutText.text.Length > 0 && (Time.time > calloutUntil || over)) calloutText.text = "";

            if (over && resultPanel.activeSelf)
            {
                float s = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
                resultTitle.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        // ---------------------------------------------------------------- setup

        private static void EnsureEventSystem()
        {
            InputSystemUIInputModule module = FindAnyObjectByType<InputSystemUIInputModule>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                module = go.AddComponent<InputSystemUIInputModule>();
            }
            if (module == null) return;
            // arrows / d-pad move the focus, Enter / A press, Esc / B cancel (the menus on keys and pads)
            if (module.move == null || module.submit == null) module.AssignDefaultActions();
            SpaceSubmits(module);
        }

        /// <summary>
        /// Space presses a focused button like Enter. The UI module's default Submit knows only Enter and its asset is
        /// shared, so the module gets its own copy of the asset with Space on UI/Submit (the module finds its actions
        /// again by name in the copy). Once; cheap to check.
        /// </summary>
        private static void SpaceSubmits(InputSystemUIInputModule module)
        {
            InputActionAsset asset = module.actionsAsset;
            InputAction submit = asset != null ? asset.FindAction("UI/Submit") : null;
            if (submit == null) return;
            foreach (InputBinding b in submit.bindings) if (b.path == "<Keyboard>/space") return;
            InputActionAsset copy = Instantiate(asset);
            copy.name = asset.name + " (Space submits)";
            copy.FindAction("UI/Submit").AddBinding("<Keyboard>/space");
            module.actionsAsset = copy;
        }


        private void BuildUI()
        {
            var canvasGo = new GameObject("FunTestKit_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            PrototypeUIHelper.SetupScaler(scaler);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Timer (top centre, its own row in portrait)
            timerText = MakeText(canvasGo.transform, "Timer", 64, TextAlignmentOptions.Top);
            var tr = timerText.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 1f); tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -10f);
            tr.sizeDelta = new Vector2(240f, 80f);
            UIText.Outline(timerText);
            timerText.gameObject.SetActive(false);

            // Callout (upper middle)
            calloutText = MakeText(canvasGo.transform, "Callout", 34, TextAlignmentOptions.Center);
            // the callout fits its box under the HUD row: it shrinks rather than spill
            Stretch(calloutText.rectTransform, new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.915f));
            calloutText.enableAutoSizing = true;
            calloutText.fontSizeMin = 20f;
            calloutText.fontSizeMax = 40f;
            calloutText.color = new Color(1f, 0.92f, 0.3f);
            UIText.Outline(calloutText, 0.12f);
            calloutText.text = "";

            // Result panel (centre)
            resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(canvasGo.transform, false);
            var bg = resultPanel.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.08f, 0.82f);
            bg.raycastTarget = false;
            var pr = bg.rectTransform;
            pr.anchorMin = new Vector2(0.06f, 0.36f); pr.anchorMax = new Vector2(0.94f, 0.62f);
            pr.offsetMin = pr.offsetMax = Vector2.zero;

            resultTitle = MakeText(resultPanel.transform, "Title", 80, TextAlignmentOptions.Center);
            Stretch(resultTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            UIText.Outline(resultTitle, 0.12f);

            resultLine = MakeText(resultPanel.transform, "Line", 30, TextAlignmentOptions.Top);
            Stretch(resultLine.rectTransform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0.5f));
            resultPanel.SetActive(false);

            // Restart button (bottom right)
            var btnGo = new GameObject("RestartButton");
            restartButton = btnGo;
            btnGo.transform.SetParent(canvasGo.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.15f, 0.2f, 0.32f, 0.92f);
            var br = img.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(1f, 0f);
            br.anchoredPosition = new Vector2(-16f, 140f);
            br.sizeDelta = new Vector2(220f, 72f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Restart);
            var bt = MakeText(btnGo.transform, "Label", 30, TextAlignmentOptions.Center);
            bt.text = "RESTART";
            Stretch(bt.rectTransform, Vector2.zero, Vector2.one);
        }

        private TextMeshProUGUI MakeText(Transform parent, string name, float size, TextAlignmentOptions anchor)
        {
            TextMeshProUGUI t = UIText.Make(parent, name, size, Color.white, anchor);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
