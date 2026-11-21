"""Put rendered RGBA frames on a plain backdrop with a drawn-style outline taken from the alpha (the outer line
heavier than the thin inner lines the 3D hull gives). python comp.py <frames dir> <out dir> [--line 3] [--bg r,g,b]
[--keep-alpha]  (--keep-alpha writes RGBA with the outline, no backdrop)"""
import sys
from pathlib import Path

from PIL import Image, ImageFilter

a = sys.argv[1:]
src, out = Path(a[0]), Path(a[1])
line = int(a[a.index("--line") + 1]) if "--line" in a else 3
bg = tuple(int(x) for x in a[a.index("--bg") + 1].split(",")) if "--bg" in a else (225, 228, 222)
out.mkdir(parents=True, exist_ok=True)
for p in sorted(src.glob("f*.png")):
    im = Image.open(p).convert("RGBA")
    al = im.getchannel("A").point(lambda v: 255 if v > 40 else 0)
    ring = al.filter(ImageFilter.MaxFilter(2 * line + 1)).filter(ImageFilter.GaussianBlur(0.6))
    ink = Image.new("RGBA", im.size, (23, 15, 13, 255))
    ink.putalpha(ring)
    fig = Image.alpha_composite(ink, im)
    if "--keep-alpha" in a:
        fig.save(out / p.name)
    else:
        base = Image.new("RGBA", im.size, bg + (255,))
        Image.alpha_composite(base, fig).convert("RGB").save(out / p.name)
print("frames", len(list(out.glob("f*.png"))))
