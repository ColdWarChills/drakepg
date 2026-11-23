"""Round 3: cut the near vest cap from the Qwen edit r3_cap_s302 (same 1536 canvas as r2_vest_teal_s202).
<venv python> -I -B cap_r3.py -> art/redo/hero_joints/parts_r3/cap.png (RGBA), cap_seam.png (L: the seam's line band)
                                and parts_r3.json (hinge, seam, tone factors, counts)
The cap = the vest left of a raglan seam (collar -> the drawn fold -> the hem's inner end), teal only (cream sleeve and
purple dropped by R-G), tone-matched to s202 on an unchanged chest box."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

from jcommon import REDO

OUT = REDO / "parts_r3"
OUT.mkdir(exist_ok=True)
SEAM = [(702.0, 484.0), (701.0, 557.0), (673.0, 611.0)]   # raglan seam: collar end, top of the drawn fold, hem end
POLY = SEAM + [(668.0, 630.0), (588.0, 602.0), (583.0, 468.0), (703.0, 468.0)]
HINGE = (702.0, 486.0)
OUTLINE = (17.0, 33.0, 32.0)   # s202's own vest outline colour (sampled on its left contour)


def main():
    s0 = np.asarray(Image.open(REDO / "r2_vest_teal_s202.png").convert("RGB")).astype(np.float32)
    s1 = np.asarray(Image.open(REDO / "r3_cap_s302.png").convert("RGB")).astype(np.float32)
    box = (slice(560, 650), slice(760, 840))            # unchanged chest
    tone = s0[box].reshape(-1, 3).mean(0) / s1[box].reshape(-1, 3).mean(0)
    H, W = s1.shape[:2]
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon(POLY, fill=255)
    region = np.asarray(m).astype(np.float32) / 255
    rg = s1[..., 0] - s1[..., 1]
    teal = np.clip((15.0 - rg) / 30.0, 0, 1)            # teal R-G ~ -22, outline ~ -15, cream +22, purple +93
    a = region * teal
    a = cv2.GaussianBlur(a, (0, 0), 0.6) * (a > 0) + 0 * a  # soften the 1 px stair, keep the inside solid
    a = np.clip(np.maximum(a, region * (teal > 0.95)), 0, 1)
    rgb = np.clip(s1 * tone, 0, 255)
    # the seam's line band (inside the cap, along the seam only): drawn per frame where the cap borders no vest
    sm = Image.new("L", (W, H), 0)
    ImageDraw.Draw(sm).line(SEAM, fill=255, width=5)
    seam = (np.asarray(sm).astype(np.float32) / 255) * (a > 0.5)
    seam = cv2.GaussianBlur(seam, (0, 0), 0.7)
    out = np.concatenate([rgb, a[..., None] * 255], -1)
    Image.fromarray((out + 0.5).astype(np.uint8), "RGBA").save(OUT / "cap.png")
    Image.fromarray((np.clip(seam, 0, 1) * 255 + 0.5).astype(np.uint8), "L").save(OUT / "cap_seam.png")
    warm = int(((a > 0.5) & (s1[..., 0] > s1[..., 1])).sum())
    info = {"src": "r3_cap_s302", "hinge": HINGE, "seam": SEAM, "poly": POLY, "outline": OUTLINE,
            "tone": np.round(tone, 4).tolist(), "cap_px": int((a > 0.5).sum()), "warm_px_in_cap": warm}
    (REDO / "parts_r3.json").write_text(json.dumps(info, indent=1))
    print(info)


if __name__ == "__main__":
    main()
