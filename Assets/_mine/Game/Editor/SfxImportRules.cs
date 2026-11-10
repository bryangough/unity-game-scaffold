#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Mobile import settings for every clip under <c>Assets/_mine/Game/Audio/SFX/</c>: mono, 22 050 Hz, short
    /// one-shots Decompress On Load + ADPCM, loops and long clips Compressed In Memory + Vorbis 50 %, preloaded. Music
    /// under <c>Audio/Music/</c>: mono, 22 050 Hz, Streaming, Vorbis 30 % (~5 KB/s, so a 160 s stem is under 1 MB).
    /// </summary>
    public class SfxImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/_mine/Game/Audio/SFX/";
        private const string MusicRoot = "Assets/_mine/Game/Audio/Music/";
        private const long LongBytes = 360_000; // ~4 s of 44.1 kHz mono 16-bit

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith(MusicRoot)) { Music((AudioImporter)assetImporter); return; }
            if (!path.StartsWith(Root)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            ai.loadInBackground = false;
            ai.ambisonic = false;
            bool loopOrLong = Path.GetFileName(assetPath).ToLowerInvariant().Contains("loop") || new FileInfo(assetPath).Length > LongBytes;
            var s = ai.defaultSampleSettings;
            s.loadType = loopOrLong ? UnityEngine.AudioClipLoadType.CompressedInMemory : UnityEngine.AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loopOrLong ? UnityEngine.AudioCompressionFormat.Vorbis : UnityEngine.AudioCompressionFormat.ADPCM;
            s.quality = 0.5f;
            s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            s.sampleRateOverride = 22050;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
            ai.ClearSampleSettingOverride("Android");
            ai.ClearSampleSettingOverride("iOS");
            ai.ClearSampleSettingOverride("WebGL");
        }

        /// <summary>Music: mono, 22 050 Hz, streamed, Vorbis 30 % (~5 KB/s).</summary>
        private static void Music(AudioImporter ai)
        {
            ai.forceToMono = true;
            ai.loadInBackground = true;
            ai.ambisonic = false;
            var s = ai.defaultSampleSettings;
            s.loadType = UnityEngine.AudioClipLoadType.Streaming;
            s.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
            s.quality = 0.3f;
            s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            s.sampleRateOverride = 22050;
            s.preloadAudioData = false;
            ai.defaultSampleSettings = s;
            ai.ClearSampleSettingOverride("Android");
            ai.ClearSampleSettingOverride("iOS");
            ai.ClearSampleSettingOverride("WebGL");
        }
    }
}
#endif
