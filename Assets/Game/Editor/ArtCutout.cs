#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Generated art (an AI image, a JPG with no transparency) into game sprites. <c>Game > Art > Cut Out Generated
    /// Art</c> takes every image in <c>Art/Generated/</c> and writes a PNG sprite to <c>Art/UI/</c>: the background is
    /// removed by a flood fill from the image border through pixels close to the border colour (alpha fades with that
    /// distance, so glows and soft edges survive), small leftover specks are cleared, the result is trimmed to its
    /// pixels. A file named <c>portrait_*</c> gets a round mask instead; <c>icon*</c> becomes the app icon (cropped 4 %
    /// in, the phone applies its own mask); <c>splash*</c> and <c>backdrop*</c> are kept whole. Outputs are committed;
    /// re-run after replacing an image. From the slope game, where every badge, portrait and callout came in this way.
    /// </summary>
    public static class ArtCutout
    {
        public const string Src = "Assets/Game/Art/Generated/";
        public const string Out = "Assets/Game/Art/UI/";
        /// <summary>How far from the border colour a pixel may be and still count as background (0–255 per channel).</summary>
        public const float DefaultThreshold = 30f;

        [MenuItem(Menus.Root + "Art/Cut Out Generated Art", false, 30)]
        public static void Run()
        {
            if (!Directory.Exists(Src)) { Debug.LogWarning($"[ArtCutout] put generated images (jpg / png) in {Src}"); return; }
            Directory.CreateDirectory(Out);
            int n = 0;
            foreach (string f in Directory.GetFiles(Src))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") continue;
                string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (name.StartsWith("icon")) { SetAppIcon(f); n++; continue; }
                if (name.StartsWith("splash") || name.StartsWith("backdrop")) { Cut(f, Out + name + ".png", Mode.Whole, 0f, 2048); n++; continue; }
                if (name.StartsWith("portrait")) { Cut(f, Out + name + ".png", Mode.Circle, 0f, 256); n++; continue; }
                Cut(f, Out + name + ".png", Mode.Flood, DefaultThreshold, name.StartsWith("logo") ? 1024 : 512);
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[ArtCutout] {n} images into {Out}");
        }

        public enum Mode { Whole, Flood, Circle }

        /// <summary>
        /// One image. <paramref name="holeShare"/>: background trapped inside the drawing (the paper inside an O) is
        /// cleared too, up to this share of the image per pocket (0 = leave pockets).
        /// </summary>
        public static void Cut(string srcPath, string dstPath, Mode mode, float threshold, int maxSize, float holeShare = 0f)
        {
            if (!File.Exists(srcPath)) { Debug.LogWarning("[ArtCutout] missing " + srcPath); return; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(srcPath));
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);
            if (mode == Mode.Flood) { Flood(px, w, h, threshold, holeShare); Despeckle(px, w, h); tex = Trim(px, w, h, 6); }
            else
            {
                if (mode == Mode.Circle) Circle(px, w, h, 0.455f);
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false); // a loaded JPG is RGB24: no alpha to write into
                tex.SetPixels32(px);
                tex.Apply();
            }
            WriteSprite(tex, dstPath, maxSize);
            Object.DestroyImmediate(tex);
        }

        /// <summary>Flood from the border through "background" pixels; alpha there = distance from the bg colour / threshold.</summary>
        private static void Flood(Color32[] px, int w, int h, float threshold, float holeShare)
        {
            Color32 bg = BorderColour(px, w, h);
            float Dist(Color32 c) => Mathf.Max(Mathf.Abs(c.r - bg.r), Mathf.Max(Mathf.Abs(c.g - bg.g), Mathf.Abs(c.b - bg.b)));
            var seen = new bool[px.Length];
            var stack = new Stack<int>();
            void Push(int i) { if (!seen[i] && Dist(px[i]) < threshold) { seen[i] = true; stack.Push(i); } }
            for (int x = 0; x < w; x++) { Push(x); Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Push(y * w); Push(y * w + w - 1); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                if (x > 0) Push(i - 1);
                if (x < w - 1) Push(i + 1);
                if (y > 0) Push(i - w);
                if (y < h - 1) Push(i + w);
            }
            if (holeShare > 0f)
            {
                int maxHole = Mathf.RoundToInt(holeShare * w * h);
                var inPocket = new bool[px.Length];
                var region = new List<int>();
                for (int start = 0; start < px.Length; start++)
                {
                    if (seen[start] || inPocket[start] || Dist(px[start]) >= threshold) continue;
                    region.Clear();
                    stack.Push(start);
                    inPocket[start] = true;
                    while (stack.Count > 0)
                    {
                        int i = stack.Pop(), x = i % w, y = i / w;
                        region.Add(i);
                        void Grow(int j) { if (!seen[j] && !inPocket[j] && Dist(px[j]) < threshold) { inPocket[j] = true; stack.Push(j); } }
                        if (x > 0) Grow(i - 1);
                        if (x < w - 1) Grow(i + 1);
                        if (y > 0) Grow(i - w);
                        if (y < h - 1) Grow(i + w);
                    }
                    if (region.Count <= maxHole) foreach (int i in region) seen[i] = true;
                }
            }
            for (int i = 0; i < px.Length; i++)
            {
                if (!seen[i]) continue;
                float a = Mathf.Clamp01(Dist(px[i]) / threshold);
                a = a * a; // eases the fade so the old background leaves no haze
                Color32 c = px[i];
                if (a < 0.02f) { px[i] = new Color32(c.r, c.g, c.b, 0); continue; }
                // un-mix the background so a glow keeps its colour, not the grey it was drawn over
                byte Un(byte v, byte b) => (byte)Mathf.Clamp(Mathf.RoundToInt(b + (v - b) / Mathf.Max(a, Mathf.Sqrt(a))), 0, 255);
                px[i] = new Color32(Un(c.r, bg.r), Un(c.g, bg.g), Un(c.b, bg.b), (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        /// <summary>Tiny islands of leftover grain the flood left half-opaque round the drawing; real details are far bigger.</summary>
        private const int SpeckMax = 40;

        private static void Despeckle(Color32[] px, int w, int h)
        {
            var done = new bool[px.Length];
            var stack = new Stack<int>();
            var island = new List<int>();
            for (int start = 0; start < px.Length; start++)
            {
                if (done[start] || px[start].a < 26) continue;
                island.Clear();
                stack.Push(start);
                done[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop(), x = i % w, y = i / w;
                    island.Add(i);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int j = ny * w + nx;
                            if (!done[j] && px[j].a >= 26) { done[j] = true; stack.Push(j); }
                        }
                }
                if (island.Count < SpeckMax) foreach (int i in island) { Color32 c = px[i]; px[i] = new Color32(c.r, c.g, c.b, 0); }
            }
        }

        /// <summary>A round crop with a 2 px soft edge.</summary>
        private static void Circle(Color32[] px, int w, int h, float radius)
        {
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f, r = radius * Mathf.Min(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01((r - d) / 2f + 0.5f) * 255f);
                    int i = y * w + x;
                    px[i] = new Color32(px[i].r, px[i].g, px[i].b, a);
                }
        }

        /// <summary>The median colour of the image's border: what the generator painted behind the subject.</summary>
        private static Color32 BorderColour(Color32[] px, int w, int h)
        {
            var rs = new List<int>(); var gs = new List<int>(); var bs = new List<int>();
            void Add(int i) { rs.Add(px[i].r); gs.Add(px[i].g); bs.Add(px[i].b); }
            for (int x = 0; x < w; x += 4) { Add(x); Add((h - 1) * w + x); }
            for (int y = 0; y < h; y += 4) { Add(y * w); Add(y * w + w - 1); }
            rs.Sort(); gs.Sort(); bs.Sort();
            int m = rs.Count / 2;
            return new Color32((byte)rs[m], (byte)gs[m], (byte)bs[m], 255);
        }

        /// <summary>Crop to the visible pixels plus a margin.</summary>
        private static Texture2D Trim(Color32[] px, int w, int h, int pad)
        {
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 8) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            if (x1 < 0) { x0 = y0 = 0; x1 = w - 1; y1 = h - 1; }
            x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad); x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
            int tw = x1 - x0 + 1, th = y1 - y0 + 1;
            var outPx = new Color32[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++) outPx[y * tw + x] = px[(y + y0) * w + x + x0];
            var t = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            t.SetPixels32(outPx);
            t.Apply();
            return t;
        }

        private static void WriteSprite(Texture2D tex, string path, int maxSize)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }

        /// <summary>
        /// The app icon from a generated square: cropped 4 % in (generators draw their own rounded corners on white; the
        /// phone applies its own mask), scaled to 1024, uncompressed, set as the default icon for every platform.
        /// </summary>
        public static void SetAppIcon(string srcPath)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(srcPath));
            const int size = 1024;
            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            const float inset = 0.04f;
            Graphics.Blit(src, rt, new Vector2(1f - 2f * inset, 1f - 2f * inset), new Vector2(inset, inset));
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var outTex = new Texture2D(size, size, TextureFormat.RGB24, false);
            outTex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            outTex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            string outPath = Out + "app_icon.png";
            File.WriteAllBytes(outPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            Debug.Log($"[ArtCutout] app icon set from {srcPath}");
        }
    }
}
#endif
