#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Colour-blindness check: <c>Game > Checks > Colour Check (Current View)</c> captures the main camera and writes
    /// <c>Builds/Checks/colour_normal / deutan / protan / tritan / grey.png</c> plus a five-column strip, so what the
    /// player must tell apart (safe from danger, lit from dark, team from team) can be looked at under each kind of
    /// vision. The same filters back the EditMode tests (<c>ColourBlindTests</c>): put every colour the player must
    /// read in a static field and pass them to <see cref="ClosePairs"/>, which compares every pair, so a colour added
    /// later cannot slip through untested. Simulation is Machado, Oliveira and Fernandes (2009) at full severity, done
    /// in linear RGB as the matrices require; differences are CIE76 delta E in L*a*b*, so a threshold means roughly
    /// the same thing for every hue. Pair checking and the linear-space fix are from the jelly game.
    /// </summary>
    public static class ColourCheck
    {
        private const string Dir = "Builds/Checks";

        /// <summary>Delta E below this in any colour mode and two colours are flagged (2.3 is just noticeable side by
        /// side; 15 held up on a phone at arm's length in the jelly game).</summary>
        public const float MinColourDifference = 15f;
        /// <summary>L* below this apart in greyscale (total colour blindness, a dim screen) and two colours are flagged.</summary>
        public const float MinLightnessDifference = 8f;

        // Machado et al. (2009), severity 1.0, applied to linear RGB
        public static readonly float[] Deutan = { 0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f };
        public static readonly float[] Protan = { 0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f };
        public static readonly float[] Tritan = { 1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f };

        /// <summary>Normal, deutan, protan, tritan, grey; the first four are the colour modes, the last is lightness only.</summary>
        public static readonly System.Func<Color, Color>[] Filters =
        {
            c => c,
            c => Simulate(Deutan, c),
            c => Simulate(Protan, c),
            c => Simulate(Tritan, c),
            Grey,
        };
        public static readonly string[] FilterNames = { "normal", "deutan", "protan", "tritan", "grey" };
        public const int GreyFilter = 4;

        [MenuItem(Menus.Root + "Checks/Colour Check (Current View)", false, 60)]
        public static void Run()
        {
            Camera cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[ColourCheck] no main camera"); return; }
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
            Debug.Log($"[ColourCheck] wrote {Dir}/colour_*.png and colour_strip.png");
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

        /// <summary>
        /// Every pair of <paramref name="colours"/> that comes out too close: delta E under
        /// <paramref name="minColour"/> in any colour mode, or L* under <paramref name="minLightness"/> apart in
        /// greyscale. Empty means every colour reads apart from every other for every player. Each line names the pair,
        /// the modes and the values, ready for an assert message or a report.
        /// </summary>
        public static List<string> ClosePairs(string[] names, Color[] colours,
            float minColour = MinColourDifference, float minLightness = MinLightnessDifference)
        {
            var close = new List<string>();
            for (int a = 0; a < colours.Length; a++)
                for (int b = a + 1; b < colours.Length; b++)
                {
                    var why = new List<string>();
                    for (int f = 0; f < GreyFilter; f++)
                    {
                        float d = DeltaE(Filters[f](colours[a]), Filters[f](colours[b]));
                        if (d < minColour) why.Add($"{FilterNames[f]} dE {d:0.0}");
                    }
                    float l = Mathf.Abs(Lightness(colours[a]) - Lightness(colours[b]));
                    if (l < minLightness) why.Add($"grey dL {l:0.0}");
                    if (why.Count > 0) close.Add($"{names[a]} / {names[b]}: {string.Join(", ", why)}");
                }
            return close;
        }

        /// <summary>A colour as a viewer with that deficiency sees it: the matrix applied in linear RGB.</summary>
        public static Color Simulate(float[] m, Color c)
        {
            float r = Mathf.GammaToLinearSpace(c.r), g = Mathf.GammaToLinearSpace(c.g), b = Mathf.GammaToLinearSpace(c.b);
            return new Color(
                Mathf.LinearToGammaSpace(Mathf.Clamp01(m[0] * r + m[1] * g + m[2] * b)),
                Mathf.LinearToGammaSpace(Mathf.Clamp01(m[3] * r + m[4] * g + m[5] * b)),
                Mathf.LinearToGammaSpace(Mathf.Clamp01(m[6] * r + m[7] * g + m[8] * b)),
                c.a);
        }

        /// <summary>Greyscale of the same lightness (relative luminance, Rec. 709, in linear RGB).</summary>
        public static Color Grey(Color c)
        {
            float y = Mathf.LinearToGammaSpace(RelativeLuminance(c));
            return new Color(y, y, y, c.a);
        }

        private static float RelativeLuminance(Color c) =>
            0.2126f * Mathf.GammaToLinearSpace(c.r) + 0.7152f * Mathf.GammaToLinearSpace(c.g) + 0.0722f * Mathf.GammaToLinearSpace(c.b);

        /// <summary>CIE76 delta E between two sRGB colours: about 2 just noticeable, 15+ apart at a glance, 100 black to white.</summary>
        public static float DeltaE(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));

        /// <summary>CIE L*, perceived lightness: 0 black to 100 white.</summary>
        public static float Lightness(Color c) => Lab(c).x;

        /// <summary>sRGB to CIE L*a*b* (D65).</summary>
        public static Vector3 Lab(Color c)
        {
            float r = Mathf.GammaToLinearSpace(c.r), g = Mathf.GammaToLinearSpace(c.g), b = Mathf.GammaToLinearSpace(c.b);
            float x = LabF((0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f);
            float y = LabF(0.2126f * r + 0.7152f * g + 0.0722f * b);
            float z = LabF((0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f);
            return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));
        }

        private static float LabF(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
    }
}
#endif
