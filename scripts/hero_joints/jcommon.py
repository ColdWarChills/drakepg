"""Shared paths and helpers for the jointed-hero test (step 1)."""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]  # the worktree
RAW = ROOT / "art" / "raw"
REDO = ROOT / "art" / "redo" / "hero_joints"
TESTS = ROOT / "art" / "tests" / "hero_joints"
A3 = ROOT / "art" / "redo" / "moments" / "layers_A3"
REDO.mkdir(parents=True, exist_ok=True)
TESTS.mkdir(parents=True, exist_ok=True)
FFMPEG = ("C:/Users/Epic/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/"
          "ffmpeg-9.0-full_build/bin/ffmpeg.exe")
PURPLE = (163, 55, 211)


def load(p) -> Image.Image:
    return Image.open(p).convert("RGBA")


def a3_moment(m: str) -> Image.Image:
    """An A3 moment stacked: weapon (back/front), body, fist copy (1536 canvas, sword may be clipped)."""
    man = json.loads((A3 / "manifest.json").read_text())
    d = man["moments"][m]
    sys.path.insert(0, str(ROOT / "scripts" / "momentclip"))
    from layers import Weapon  # noqa
    w = d["weapon"]
    wp = Weapon(w["art"]).placed(w["grip_xy"], w["angle"], w["scale_px_per_art_px"], 1536, 0)
    img = Image.new("RGBA", (1536, 1536), (0, 0, 0, 0))
    if d["weapon_order"] == "back":
        img.alpha_composite(wp)
    img.alpha_composite(load(A3 / m / "body.png"))
    img.alpha_composite(load(A3 / m / "shirt.png"))
    img.alpha_composite(load(A3 / m / "hair.png"))
    if d["weapon_order"] != "back":
        img.alpha_composite(wp)
    img.alpha_composite(load(A3 / m / "fist.png"))
    return img


def on_bg(im: Image.Image, col=(235, 235, 235)) -> Image.Image:
    bg = Image.new("RGBA", im.size, col + (255,))
    bg.alpha_composite(im)
    return bg.convert("RGB")


def fit(im: Image.Image, max_side=1500, max_mp=1.05) -> Image.Image:
    w, h = im.size
    k = min(1.0, max_side / max(w, h), math.sqrt(max_mp * 1e6 / (w * h)))
    if k < 1:
        im = im.resize((max(1, round(w * k)), max(1, round(h * k))), Image.LANCZOS)
    return im
