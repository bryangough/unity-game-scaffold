using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScaffold
{
    /// <summary>
    /// Shared "juice": camera shake, hit-stop, primitive particle bursts,
    /// floating score popups, scale punches and colour flashes. No assets needed.
    /// Everything lives on a host object created on first use; it dies with the scene, so R-restart cleans up.
    /// Call from gameplay code, e.g. GameFeel.Shake(0.3f); GameFeel.Burst(pos, Color.red); GameFeel.Popup(pos, "+100", Color.yellow);
    /// </summary>
    public static class GameFeel
    {
        /// <summary>Shake the main camera. amount = world units of offset at the start, fades out over duration (unscaled).</summary>
        public static void Shake(float amount = 0.25f, float duration = 0.2f)
        {
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.AddShake(amount, duration);
        }

        /// <summary>Freeze time for a few frames (0.03–0.08 s reads as impact). Safe to call repeatedly; the longest wins.</summary>
        public static void HitStop(float seconds = 0.05f)
        {
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.AddHitStop(seconds);
        }

        /// <summary>Spray of small unlit cubes from a point, with gravity. Works in 2D (XY) and 3D scenes.</summary>
        public static void Burst(Vector3 position, Color color, int count = 12, float speed = 5f, float size = 0.18f, float life = 0.6f)
        {
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.SpawnBurst(position, color, count, speed, size, life);
        }

        /// <summary>Paper confetti that pops up, tumbles and flutters down, carried along at <paramref name="carry"/> (a win).</summary>
        public static void Confetti(Vector3 position, Vector3 carry = default, int count = 60, float speed = 9f, float life = 3f)
        {
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.SpawnConfetti(position, carry, count, speed, life);
        }

        /// <summary>Floating text that rises and fades at a world position (screen-space, always readable).</summary>
        public static void Popup(Vector3 worldPosition, string text, Color color, float scale = 1f)
        {
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.SpawnPopup(worldPosition, text, color, scale);
        }

        /// <summary>Quick scale punch: grows by 'amount' and settles back over duration.</summary>
        public static void Punch(Transform target, float amount = 0.25f, float duration = 0.18f)
        {
            if (target == null) return;
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.AddPunch(target, amount, duration);
        }

        /// <summary>Tint a renderer's (instanced) material for a moment, then restore its colour.</summary>
        public static void Flash(Renderer renderer, Color color, float duration = 0.1f)
        {
            if (renderer == null) return;
            GameFeelHost h = GameFeelHost.Get();
            if (h != null) h.AddFlash(renderer, color, duration);
        }
    }

    [DefaultExecutionOrder(1000)]
    public class GameFeelHost : MonoBehaviour
    {
        private static GameFeelHost instance;

        public static GameFeelHost Get()
        {
            if (!Application.isPlaying) return null;
            if (instance == null)
            {
                var go = new GameObject("GameFeel");
                instance = go.AddComponent<GameFeelHost>();
            }
            return instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        // ---------------------------------------------------------------- shake
        private float shakeAmount, shakeDuration, shakeTime;
        private Camera shakenCam;
        private Vector3 appliedOffset;

        public void AddShake(float amount, float duration)
        {
            if (amount >= shakeAmount * (1f - shakeTime / Mathf.Max(shakeDuration, 0.0001f)))
            {
                shakeAmount = amount;
                shakeDuration = Mathf.Max(0.01f, duration);
                shakeTime = 0f;
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCam;
            RenderPipelineManager.endCameraRendering += EndCam;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCam;
            RenderPipelineManager.endCameraRendering -= EndCam;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            RemoveOffset();
            if (stopping) { Time.timeScale = 1f; stopping = false; }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (stopping) Time.timeScale = 1f;
        }

        private void OnSceneUnloaded(Scene s)
        {
            if (stopping) { Time.timeScale = 1f; stopping = false; }
        }

        private void BeginCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != Camera.main || shakeTime >= shakeDuration || shakeAmount <= 0f) return;
            float k = 1f - shakeTime / shakeDuration;
            appliedOffset = (Vector3)(Random.insideUnitCircle * shakeAmount * k * k);
            appliedOffset = cam.transform.right * appliedOffset.x + cam.transform.up * appliedOffset.y;
            cam.transform.position += appliedOffset;
            shakenCam = cam;
        }

        private void EndCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == shakenCam) RemoveOffset();
        }

        private void RemoveOffset()
        {
            if (shakenCam != null) shakenCam.transform.position -= appliedOffset;
            shakenCam = null;
            appliedOffset = Vector3.zero;
        }

        // ---------------------------------------------------------------- hit-stop
        private bool stopping;
        private float stopUntil;
        private float timeScaleBefore = 1f;

        public void AddHitStop(float seconds)
        {
            if (FunTestKit.IsOver) return;
            if (!stopping)
            {
                timeScaleBefore = Time.timeScale <= 0f ? 1f : Time.timeScale;
                Time.timeScale = 0f;
                stopping = true;
                stopUntil = Time.unscaledTime + seconds;
            }
            else stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + seconds);
        }

        // ---------------------------------------------------------------- bursts
        private class Bit { public Transform t; public Renderer r; public Vector3 v, carry, spin; public float age, life, size; public bool alive, paper; }
        private readonly List<Bit> bits = new List<Bit>();
        private Material bitMat;
        private readonly Dictionary<Color, Material> bitMats = new Dictionary<Color, Material>();

        private Material MatFor(Color c)
        {
            if (bitMats.TryGetValue(c, out Material m)) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            bitMats[c] = m;
            return m;
        }

        public void SpawnBurst(Vector3 pos, Color color, int count, float speed, float size, float life)
        {
            Material m = MatFor(color);
            bool flat = Camera.main != null && Camera.main.orthographic && Mathf.Abs(Camera.main.transform.forward.z) > 0.9f;
            for (int i = 0; i < count; i++)
            {
                Bit b = FreeBit();
                b.alive = true;
                b.paper = false;
                b.t.gameObject.SetActive(true);
                b.r.sharedMaterial = m;
                b.t.position = pos;
                b.t.rotation = Random.rotation;
                Vector3 dir = flat ? (Vector3)Random.insideUnitCircle.normalized : Random.onUnitSphere;
                if (!flat) dir.y = Mathf.Abs(dir.y) + 0.3f;
                b.v = dir.normalized * speed * Random.Range(0.5f, 1.1f);
                b.size = size * Random.Range(0.7f, 1.3f);
                b.life = life * Random.Range(0.7f, 1.2f);
                b.age = 0f;
                b.t.localScale = Vector3.one * b.size;
            }
        }

        private Bit FreeBit()
        {
            Bit b = bits.Find(x => !x.alive);
            if (b != null) return b;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = "FeelBit";
            go.transform.SetParent(transform, false);
            b = new Bit { t = go.transform, r = go.GetComponent<Renderer>() };
            b.r.shadowCastingMode = ShadowCastingMode.Off;
            bits.Add(b);
            return b;
        }

        private static readonly Color[] Party = { new Color(1f, 0.82f, 0.29f), new Color(1f, 0.33f, 0.28f), new Color(0.25f, 0.62f, 1f), new Color(0.3f, 0.85f, 0.45f), Color.white, new Color(1f, 0.5f, 0.85f) };

        /// <summary>Flat bits in party colours that pop up, tumble and flutter down slowly, carried along at <paramref name="carry"/> so they stay in view.</summary>
        public void SpawnConfetti(Vector3 pos, Vector3 carry, int count, float speed, float life)
        {
            for (int i = 0; i < count; i++)
            {
                Bit b = FreeBit();
                b.alive = true;
                b.paper = true;
                b.t.gameObject.SetActive(true);
                b.r.sharedMaterial = MatFor(Party[i % Party.Length]);
                b.t.position = pos + Random.insideUnitSphere * 0.6f;
                b.t.rotation = Random.rotation;
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.8f;
                b.v = dir.normalized * speed * Random.Range(0.6f, 1.1f);
                b.carry = carry;
                b.spin = Random.insideUnitSphere * 720f;
                b.size = Random.Range(0.16f, 0.26f);
                b.life = life * Random.Range(0.75f, 1.2f);
                b.age = 0f;
                b.t.localScale = new Vector3(b.size, b.size * 0.6f, b.size * 0.05f);
            }
        }

        // ---------------------------------------------------------------- popups
        private class Pop { public TMPro.TextMeshProUGUI text; public Vector3 world; public float age; public float scale; public bool alive; }
        private readonly List<Pop> pops = new List<Pop>();
        private Canvas canvas;

        public void SpawnPopup(Vector3 world, string s, Color c, float scale)
        {
            if (canvas == null)
            {
                var cgo = new GameObject("GameFeel_Canvas");
                cgo.transform.SetParent(transform, false);
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;
                var scaler = cgo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            Pop p = pops.Find(x => !x.alive);
            if (p == null)
            {
                TMPro.TextMeshProUGUI t = UIText.Make(canvas.transform, "Popup", 40f, Color.white);
                t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                UIText.Outline(t, 0.14f);
                t.rectTransform.sizeDelta = new Vector2(600f, 80f);
                p = new Pop { text = t };
                pops.Add(p);
            }
            p.alive = true;
            p.text.gameObject.SetActive(true);
            p.text.text = s;
            p.text.color = c;
            p.text.fontSize = 34f * scale;
            p.world = world;
            p.age = 0f;
            p.scale = scale;
            PlacePopup(p);
        }

        private void PlacePopup(Pop p)
        {
            Camera cam = Camera.main;
            if (cam == null || canvas == null) return;
            Vector3 sp = cam.WorldToScreenPoint(p.world);
            if (sp.z < 0f) { p.text.enabled = false; return; }
            p.text.enabled = true;
            float k = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            float rise = 90f * Mathf.Sqrt(Mathf.Clamp01(p.age / 0.9f));
            p.text.rectTransform.position = new Vector3(sp.x, sp.y + rise * k, 0f);
            float pop = p.age < 0.08f ? Mathf.Lerp(0.6f, 1.25f, p.age / 0.08f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((p.age - 0.08f) / 0.12f));
            p.text.rectTransform.localScale = Vector3.one * pop;
        }

        // ---------------------------------------------------------------- punches & flashes
        private class PunchJob { public Transform t; public Vector3 baseScale; public float amount, duration, age; }
        private readonly List<PunchJob> punches = new List<PunchJob>();
        private class FlashJob { public Renderer r; public Color baseColor; public float until; }
        private readonly List<FlashJob> flashes = new List<FlashJob>();

        public void AddPunch(Transform t, float amount, float duration)
        {
            PunchJob j = punches.Find(x => x.t == t);
            if (j == null) { j = new PunchJob { t = t, baseScale = t.localScale }; punches.Add(j); }
            j.amount = amount; j.duration = Mathf.Max(0.01f, duration); j.age = 0f;
        }

        public void AddFlash(Renderer r, Color c, float duration)
        {
            FlashJob j = flashes.Find(x => x.r == r);
            if (j == null)
            {
                j = new FlashJob { r = r, baseColor = r.material.color };
                flashes.Add(j);
            }
            r.material.color = c;
            j.until = Time.unscaledTime + duration;
        }

        // ---------------------------------------------------------------- loop
        private void Update()
        {
            float udt = Time.unscaledDeltaTime;
            float dt = Time.deltaTime;

            if (stopping && Time.unscaledTime >= stopUntil)
            {
                stopping = false;
                if (Time.timeScale == 0f) Time.timeScale = timeScaleBefore;
            }

            shakeTime += udt;

            bool flat = Camera.main != null && Camera.main.orthographic && Mathf.Abs(Camera.main.transform.forward.z) > 0.9f;
            Vector3 g = flat ? new Vector3(0f, -14f, 0f) : new Vector3(0f, -18f, 0f);
            foreach (Bit b in bits)
            {
                if (!b.alive) continue;
                b.age += dt;
                if (b.age >= b.life) { b.alive = false; b.t.gameObject.SetActive(false); continue; }
                if (b.paper)
                {
                    // paper: light gravity, a lot of air drag, a side-to-side flutter, a tumble; full size until the end
                    b.v += g * 0.3f * dt;
                    b.v *= Mathf.Max(0f, 1f - 2.4f * dt);
                    b.carry *= Mathf.Max(0f, 1f - 0.5f * dt);
                    var flutter = new Vector3(Mathf.Sin(b.age * 7f + b.size * 40f), 0f, Mathf.Cos(b.age * 5f + b.size * 30f)) * 0.9f;
                    b.t.position += (b.v + b.carry + flutter) * dt;
                    b.t.Rotate(b.spin * dt, Space.Self);
                    float left = (b.life - b.age) / (b.life * 0.25f);
                    b.t.localScale = new Vector3(b.size, b.size * 0.6f, b.size * 0.05f) * Mathf.Clamp01(left);
                    continue;
                }
                b.v += g * dt;
                b.t.position += b.v * dt;
                b.t.localScale = Vector3.one * b.size * (1f - b.age / b.life);
            }

            foreach (Pop p in pops)
            {
                if (!p.alive) continue;
                p.age += udt;
                if (p.age > 0.9f) { p.alive = false; p.text.gameObject.SetActive(false); continue; }
                Color c = p.text.color;
                c.a = p.age < 0.6f ? 1f : 1f - (p.age - 0.6f) / 0.3f;
                p.text.color = c;
                PlacePopup(p);
            }

            for (int i = punches.Count - 1; i >= 0; i--)
            {
                PunchJob j = punches[i];
                if (j.t == null) { punches.RemoveAt(i); continue; }
                j.age += udt;
                float k = Mathf.Clamp01(j.age / j.duration);
                j.t.localScale = j.baseScale * (1f + j.amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                if (k >= 1f) { j.t.localScale = j.baseScale; punches.RemoveAt(i); }
            }

            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                FlashJob j = flashes[i];
                if (j.r == null) { flashes.RemoveAt(i); continue; }
                if (Time.unscaledTime >= j.until) { j.r.material.color = j.baseColor; flashes.RemoveAt(i); }
            }
        }
    }
}
