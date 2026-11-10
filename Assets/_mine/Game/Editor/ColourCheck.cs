#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Colour-blindness check: <c>Game > Checks > Colour Check (Current View)</c> captures the main camera and writes
    /// <c>Builds/Checks/colour_normal / deutan / protan / tritan / grey.png</c> plus a five-column strip, so what the
    /// player must tell apart (safe from danger, lit from dark, team from team) can be looked at under each kind of
    /// vision. The matrices are also used by the EditMode tests (<c>ColourBlindTests</c>) to check the game's own
    /// colour constants automatically; put every colour the player must read in a static field and test it.
    /// </summary>
    public static class ColourCheck
    {
        private const string Dir = "Builds/Checks";

        // Machado, Oliveira and Fernandes (2009) 3x3 simulation matrices at severity 1.0
        public static readonly float[] Deutan = { 0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f };
        public static readonly float[] Protan = { 0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f };
        public static readonly float[] Tritan = { 0.950000f, 0.050000f, 0.000000f, 0.000000f, 0.433333f, 0.566667f, 0.000000f, 0.475000f, 0.525000f };

        public static readonly System.Func<Color, Color>[] Filters =
        {
            c => c,
            c => Mul(Deutan, c),
            c => Mul(Protan, c),
            c => Mul(Tritan, c),
            Grey,
        };
        public static readonly string[] FilterNames = { "normal", "deutan", "protan", "tritan", "grey" };

        [MenuItem(Menus.Root + "Checks/Colour Check (Current View)", false, 60)]
        public static void Run()
        {
            Camera cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Colour Check] no main camera"); return; }
            int w = 540, h = 960;
            var rt = new RenderTexture(w, h, 24);
            RenderTexture prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevRt;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            Color[] px = tex.GetPixels();
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            Directory.CreateDirectory(Dir);
            for (int i = 0; i < Filters.Length; i++)
            {
                var dst = new Color[px.Length];
                for (int k = 0; k < px.Length; k++) dst[k] = Filters[i](px[k]);
                tex.SetPixels(dst);
                tex.Apply();
                File.WriteAllBytes($"{Dir}/colour_{FilterNames[i]}.png", tex.EncodeToPNG());
            }
            Object.DestroyImmediate(tex);
            SaveStrip(px, w, h, $"{Dir}/colour_strip.png");
            Debug.Log($"[Colour Check] wrote {Dir}/colour_*.png and colour_strip.png");
        }

        private static void SaveStrip(Color[] src, int w, int h, string path)
        {
            int cols = Filters.Length;
            var sheet = new Texture2D(cols * w, h, TextureFormat.RGB24, false);
            var dst = new Color[cols * w * h];
            for (int col = 0; col < cols; col++)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        dst[y * cols * w + col * w + x] = Filters[col](src[y * w + x]);
            sheet.SetPixels(dst);
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        public static Color Grey(Color c)
        {
            float g = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            return new Color(g, g, g, c.a);
        }

        public static Color Mul(float[] m, Color c) => new Color(
            Mathf.Clamp01(m[0] * c.r + m[1] * c.g + m[2] * c.b),
            Mathf.Clamp01(m[3] * c.r + m[4] * c.g + m[5] * c.b),
            Mathf.Clamp01(m[6] * c.r + m[7] * c.g + m[8] * c.b),
            c.a);

        /// <summary>Euclidean RGB distance, 0..1.73; above ~0.4 two colours read apart at a glance.</summary>
        public static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        public static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
#endif
