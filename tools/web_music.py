"""
Encodes one music stem as a small mono MP3 for the web build (called by Editor/MusicBundles.cs; runnable by hand).

Unity re-encodes all web audio to ~128 kbps AAC whatever the import settings say, so a web build's music is made here
instead: mono, 32 kHz, 40 kbps (about 0.5 MB per 100 s). The jelly game's web download went 24.8 -> 12.0 MB this way.
The MP3 is saved as .bytes data and packed into the track's AssetBundle, so it never sits on a web server as a loose
music file; Sfx decodes it in the browser.

Uses ffmpeg if it is on PATH, otherwise the Python `soundfile` package (pip install soundfile; libsndfile 1.1+ writes
MP3). Exit code 2 when neither is there: the build then packs Unity's own clips and says so.

    python tools/web_music.py <source.wav|.ogg|.mp3> <out.bytes> [kbps]
"""
import shutil
import subprocess
import sys

KBPS = 40


def encode(src, dst, kbps):
    if shutil.which("ffmpeg"):
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, "-ac", "1", "-ar", "32000",
                        "-b:a", f"{kbps}k", "-f", "mp3", dst], check=True)
        return
    try:
        import soundfile as sf
    except ImportError:
        print("no encoder: put ffmpeg on PATH or pip install soundfile", file=sys.stderr)
        sys.exit(2)
    info = sf.info(src)
    # soundfile cannot resample, so the source rate is kept; its MP3 quality is a 0..1 compression level, not a
    # bitrate (0.95 lands near 40 kbps mono at 32 kHz, a little over at 44.1 / 48 kHz)
    with sf.SoundFile(dst, "w", samplerate=info.samplerate, channels=1, format="MP3",
                      subtype="MPEG_LAYER_III", bitrate_mode="CONSTANT", compression_level=0.95) as out:
        for block in sf.blocks(src, blocksize=8192, always_2d=True):
            out.write(block.mean(axis=1))  # stereo to mono


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    encode(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else KBPS)
    return 0


if __name__ == "__main__":
    sys.exit(main())
