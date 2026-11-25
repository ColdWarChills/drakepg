"""Paper damage proof (2026-11-25): an mp4 from recorded battle frames (Drive.Record's f000.jpg...).

<venv python> -I -B clip.py <out.mp4> <segment> [<segment> ...]
A segment is "dir|first|last" or "dir|first|last|slow" (frame numbers inclusive; slow 2 = half speed). Frames were
recorded at 20 fps. Needs ffmpeg on PATH.
"""
import subprocess
import sys
import tempfile
from pathlib import Path

FPS = 20


def main():
    out = Path(sys.argv[1])
    lines = []
    last = None
    for seg in sys.argv[2:]:
        bits = seg.split("|")
        d, first, last_n = bits[0], int(bits[1]), int(bits[2])
        slow = int(bits[3]) if len(bits) > 3 else 1
        for i in range(first, last_n + 1):
            f = Path(d) / f"f{i:03d}.jpg"
            if not f.exists():
                continue
            lines.append(f"file '{f.resolve().as_posix()}'\nduration {slow / FPS:.4f}")
            last = f
    if last is None:
        sys.exit("no frames")
    # The concat demuxer ignores the last duration unless the last file is listed again.
    lines.append(f"file '{last.resolve().as_posix()}'")
    with tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False) as t:
        t.write("\n".join(lines))
        listing = t.name
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listing,
                    "-vf", f"fps={FPS},format=yuv420p", "-c:v", "libx264", "-crf", "18", "-movflags", "+faststart", str(out)],
                   check=True)
    print(out)


if __name__ == "__main__":
    main()
