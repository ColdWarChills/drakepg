"""Shared bits for the hero-in-moments kit: paths, the ComfyUI call (RGBA in, RGBA out) and the prompt texts.

The prompt texts HERO and STYLE are the ones that made pictures the designer said yes to (scripts/hero_layers.py)."""
import io
import json
import sys
import time
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "art/raw"
REDO = ROOT / "art/redo/moments"
TESTS = ROOT / "art/tests/moments"
for _d in (REDO, TESTS):
    _d.mkdir(parents=True, exist_ok=True)
HOST = "http://127.0.0.1:8188"
UNET = "qwen_image_2.1_int8_convrot.safetensors"
CLIP = "qwen3vl_8b_int8_convrot.safetensors"
VAE = "qwen_image_2.1_vae_bf16.safetensors"

# Canvas of every moment: the bare hero standing is HERO_H px tall (soles to hair top) on a CANVAS square.
CANVAS = 1536
HERO_H = 1000
FEET_Y = 1390

RGBA_OPEN = "This is an RGBA format image with transparency."
RGBA_CLOSE = "The image has an alpha channel and a transparent background."
STYLE = ("the same flat cartoon art style with hard-edged cel shadows, thin dark outlines and a warm golden rim light "
         "along his edges")
HERO = ("the same young hero with the same face, messy dark brown hair, red headband with two tails, cream shirt with "
        "sleeves rolled up to the elbows, brown belt with a square buckle, dark teal trousers and brown boots, the same "
        "size and body proportions, the same colours")


def load_rgba(path) -> np.ndarray:
    a = np.array(Image.open(path).convert("RGBA"))
    a[..., 3] = np.where(a[..., 3] < 10, 0, a[..., 3])
    return a


def png_bytes(arr: np.ndarray) -> bytes:
    buf = io.BytesIO()
    Image.fromarray(arr).save(buf, "PNG")
    return buf.getvalue()


class Comfy:
    """One Qwen-Image 2.1 edit. Each reference is loaded with its alpha joined back (LoadImage splits it off), so the
    VAE sees the transparent ground; output is decoded RGBA."""

    def __init__(self, host: str = HOST):
        self.host = host

    def check(self):
        urllib.request.urlopen(f"{self.host}/system_stats", timeout=5)

    def upload(self, name: str, data: bytes) -> str:
        b = uuid.uuid4().hex
        body = (f"--{b}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\nContent-Type: "
                f"image/png\r\n\r\n").encode() + data + (
            f"\r\n--{b}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{b}--\r\n").encode()
        req = urllib.request.Request(f"{self.host}/upload/image", body,
                                     {"Content-Type": f"multipart/form-data; boundary={b}"})
        return json.load(urllib.request.urlopen(req))["name"]

    def generate(self, prompt: str, refs: list, seed: int, steps: int = 30) -> bytes:
        g = {
            "1": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
            "2": {"class_type": "CLIPLoader", "inputs": {"clip_name": CLIP, "type": "qwen_image", "device": "default"}},
            "3": {"class_type": "VAELoader", "inputs": {"vae_name": VAE}},
            "4": {"class_type": "QwenImage21Cache", "inputs": {"model": ["1", 0], "device": "auto", "dtype": "default"}},
        }
        enc = {"clip": ["2", 0], "vae": ["3", 0], "prompt": prompt, "negative_prompt": "", "resolution": 0}
        for i, name in enumerate(refs, 1):
            g[f"{10 + i}"] = {"class_type": "LoadImage", "inputs": {"image": name}}
            g[f"{20 + i}"] = {"class_type": "JoinImageWithAlpha", "inputs": {"image": [f"{10 + i}", 0],
                                                                              "alpha": [f"{10 + i}", 1]}}
            enc[f"images.image_{i}"] = [f"{20 + i}", 0]
        g["5"] = {"class_type": "TextEncodeQwenImage21", "inputs": enc}
        g["6"] = {"class_type": "KSampler", "inputs": {
            "model": ["4", 0], "positive": ["5", 0], "negative": ["5", 1], "latent_image": ["5", 2], "seed": seed,
            "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}}
        g["7"] = {"class_type": "VAEDecode", "inputs": {"samples": ["6", 0], "vae": ["3", 0]}}
        g["8"] = {"class_type": "SaveImage", "inputs": {"images": ["7", 0], "filename_prefix": "moments"}}
        req = urllib.request.Request(f"{self.host}/prompt", json.dumps({"prompt": g}).encode(),
                                     {"Content-Type": "application/json"})
        try:
            pid = json.load(urllib.request.urlopen(req))["prompt_id"]
        except urllib.error.HTTPError as e:
            raise RuntimeError(e.read().decode()[:2000]) from e
        while True:
            hist = json.load(urllib.request.urlopen(f"{self.host}/history/{pid}"))
            if pid in hist:
                st = hist[pid].get("status", {})
                if st.get("status_str") == "error":
                    raise RuntimeError(json.dumps(st.get("messages", []))[:2000])
                imgs = [im for out in hist[pid]["outputs"].values() for im in out.get("images", [])]
                if imgs:
                    q = urllib.parse.urlencode({k: imgs[0][k] for k in ("filename", "subfolder", "type")})
                    return urllib.request.urlopen(f"{self.host}/view?{q}").read()
            time.sleep(1.5)
