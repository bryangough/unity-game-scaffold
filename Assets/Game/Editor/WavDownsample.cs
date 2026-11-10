#if UNITY_EDITOR
using System;
using System.IO;

namespace GameScaffold.Editor
{
    /// <summary>
    /// Shrinks a PCM WAV on its way into the project: any channel count and 16 / 24 / 32-bit integer samples in,
    /// 16-bit mono at <see cref="Rate"/> out (channels averaged, linear resampling), so a 30 MB music stem (48 kHz,
    /// 24-bit stereo) lands in git at about 4.6 MB. The game plays music mono at 22 kHz anyway
    /// (<see cref="SfxImportRules"/>), so nothing is lost in the build.
    /// </summary>
    public static class WavDownsample
    {
        public const int Rate = 22050;

        /// <summary>Converts <paramref name="src"/> to <paramref name="dst"/>; false (and a plain copy) when the source is not PCM.</summary>
        public static bool Convert(string src, string dst)
        {
            byte[] b = File.ReadAllBytes(src);
            if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') { File.Copy(src, dst, true); return false; }
            int fmt = -1, data = -1, dataLen = 0;
            for (int i = 12; i + 8 <= b.Length;)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, i, 4);
                int len = BitConverter.ToInt32(b, i + 4);
                if (id == "fmt ") fmt = i + 8;
                else if (id == "data") { data = i + 8; dataLen = Math.Min(len, b.Length - data); break; }
                i += 8 + len + (len & 1);
            }
            if (fmt < 0 || data < 0) { File.Copy(src, dst, true); return false; }
            int format = BitConverter.ToInt16(b, fmt);
            int channels = BitConverter.ToInt16(b, fmt + 2);
            int rate = BitConverter.ToInt32(b, fmt + 4);
            int bits = BitConverter.ToInt16(b, fmt + 14);
            if ((format != 1 && format != 0xFFFE) || (bits != 16 && bits != 24 && bits != 32) || channels < 1) { File.Copy(src, dst, true); return false; }

            int bytesPer = bits / 8;
            int frames = dataLen / (bytesPer * channels);
            float[] mono = new float[frames];
            float scale = 1f / (bits == 16 ? 32768f : bits == 24 ? 8388608f : 2147483648f);
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                int at = data + f * bytesPer * channels;
                for (int c = 0; c < channels; c++, at += bytesPer)
                {
                    int v = bits == 16 ? BitConverter.ToInt16(b, at)
                          : bits == 24 ? (b[at] | (b[at + 1] << 8) | (b[at + 2] << 16)) << 8 >> 8
                          : BitConverter.ToInt32(b, at);
                    sum += v * scale;
                }
                mono[f] = sum / channels;
            }

            int outFrames = (int)((long)frames * Rate / rate);
            short[] pcm = new short[outFrames];
            double step = (double)rate / Rate;
            for (int o = 0; o < outFrames; o++)
            {
                double pos = o * step;
                int i0 = (int)pos;
                int i1 = Math.Min(frames - 1, i0 + 1);
                float t = (float)(pos - i0);
                float s = mono[i0] * (1f - t) + mono[i1] * t;
                pcm[o] = (short)Math.Max(-32768, Math.Min(32767, Math.Round(s * 32767f)));
            }

            using (var w = new BinaryWriter(File.Create(dst)))
            {
                int dataBytes = outFrames * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (short s in pcm) w.Write(s);
            }
            return true;
        }
    }
}
#endif
