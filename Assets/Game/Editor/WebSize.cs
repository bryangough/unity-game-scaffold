#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameScaffold.Editor
{
    /// <summary>
    /// The web build's diet, applied by <see cref="PhoneBuilds"/> before every build (each step does nothing when
    /// already set), so a cap changed by hand in an importer is put back: change it here instead.
    /// <list type="bullet">
    /// <item><see cref="Caps"/>: texture paths and the largest size they need. A bought pack's 2048 palette map on a
    /// low-poly model looks the same at 512 and costs a quarter; compare a capture before and after (the shipped game
    /// measured 0.08 % of pixels moving by more than 16 of 255). Every platform, so phones save the memory too.</item>
    /// <item><see cref="CrunchOnWeb"/>: flat UI pictures (how-to cards, a logo, a backdrop) crunched at quality 75 on
    /// the web only; phones keep ASTC. Block compression needs sides that are multiples of 4: pad a PNG that is not.</item>
    /// <item><see cref="ApplyWebPlayer"/>: Brotli with the decompression fallback, and the wasm built for size with
    /// link-time optimisation (a cold web build takes ~5 min; the engine code dropped 7.9 → 5.5 MB).</item>
    /// </list>
    /// The size report (<c>Builds/Checks/size_webgl.md</c>) says what to cut next. A 10 MB download is a good bar.
    /// </summary>
    public static class WebSize
    {
        /// <summary>Texture path → max size, every platform. Fill in as the art arrives.</summary>
        public static readonly Dictionary<string, int> Caps = new Dictionary<string, int>
        {
            // { "Assets/Vendor/Pack/Textures/Colour Map 01.psd", 512 },
        };

        /// <summary>Texture paths crunched on the web (lossy, fine for flat UI art), with the web max size (0 = keep).</summary>
        public static readonly Dictionary<string, int> CrunchOnWeb = new Dictionary<string, int>
        {
            // { "Assets/Game/Resources/MenuBackdrop.png", 1024 },
        };
        public const int CrunchQuality = 75;

        [MenuItem(Menus.Root + "Build/Apply Web Size Settings", false, 140)]
        public static void ApplyMenu() { ApplyTextures(); ApplyWebPlayer(); AssetDatabase.SaveAssets(); }

        /// <summary>Caps and crunches; returns how many importers changed (and reimported).</summary>
        public static int ApplyTextures()
        {
            int changed = 0;
            foreach (KeyValuePair<string, int> kv in Caps)
            {
                if (!(AssetImporter.GetAtPath(kv.Key) is TextureImporter ti)) { Debug.LogWarning("[WebSize] missing " + kv.Key); continue; }
                if (ti.maxTextureSize == kv.Value) continue;
                ti.maxTextureSize = kv.Value;
                ti.SaveAndReimport();
                changed++;
            }
            foreach (KeyValuePair<string, int> kv in CrunchOnWeb)
            {
                if (!(AssetImporter.GetAtPath(kv.Key) is TextureImporter ti)) { Debug.LogWarning("[WebSize] missing " + kv.Key); continue; }
                TextureImporterPlatformSettings w = ti.GetPlatformTextureSettings("WebGL");
                int max = kv.Value > 0 ? kv.Value : ti.maxTextureSize;
                if (w.overridden && w.crunchedCompression && w.compressionQuality == CrunchQuality && w.maxTextureSize == max) continue;
                w.overridden = true;
                w.maxTextureSize = max;
                w.format = TextureImporterFormat.Automatic;
                w.textureCompression = TextureImporterCompression.Compressed;
                w.crunchedCompression = true;
                w.compressionQuality = CrunchQuality;
                ti.SetPlatformTextureSettings(w);
                ti.SaveAndReimport();
                changed++;
            }
            if (changed > 0) Debug.Log($"[WebSize] {changed} textures capped");
            return changed;
        }

        /// <summary>Brotli (15–20 % smaller than gzip; the fallback keeps any host and plain http working), the wasm
        /// built for size, and the page (<c>Assets/WebGLTemplates/Game</c>: fills its frame, no footer, 2x sharpness cap).</summary>
        public static void ApplyWebPlayer()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
            PlayerSettings.WebGL.template = "PROJECT:Game";
            // only the size of an itch.io embed: the page itself fills whatever frame it is given
            PlayerSettings.defaultWebScreenWidth = 450;
            PlayerSettings.defaultWebScreenHeight = 800;
        }

        /// <summary>Pads a PNG with clear pixels to sides that are multiples of 4, so it block-compresses. Returns true when it changed the file.</summary>
        public static bool PadToFour(string assetPath)
        {
            if (!System.IO.File.Exists(assetPath)) return false;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(System.IO.File.ReadAllBytes(assetPath))) { Object.DestroyImmediate(src); return false; }
            int w = (src.width + 3) / 4 * 4, h = (src.height + 3) / 4 * 4;
            if (w == src.width && h == src.height) { Object.DestroyImmediate(src); return false; }
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels32(new Color32[w * h]);
            dst.SetPixels32((w - src.width) / 2, (h - src.height) / 2, src.width, src.height, src.GetPixels32());
            dst.Apply();
            System.IO.File.WriteAllBytes(assetPath, dst.EncodeToPNG());
            Debug.Log($"[WebSize] {assetPath} padded {src.width}x{src.height} -> {w}x{h}");
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return true;
        }
    }
}
#endif
