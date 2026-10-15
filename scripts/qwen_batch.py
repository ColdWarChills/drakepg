#!/usr/bin/env python3
"""Generate art/QUEUE.md images on a local ComfyUI running Qwen-Image 2.1 (setup: docs/PLANS.md, Art).

The queue's prompts were written for Higgsfield (flat magenta/green backgrounds, one style reference). This script
rewrites them for Qwen: sprites ask for a real transparent background, and every image gets the game's own art as
style references (see `refs_for`). Results land in art/raw/<key>.png, ready for scripts/prepare_art.py.

Usage:
  python3 scripts/qwen_batch.py mon_volt_eel bg_frozen_roost   # these keys
  python3 scripts/qwen_batch.py --pending                       # every unticked key in the checklist
  python3 scripts/qwen_batch.py npc_gunnar --variants 4         # 4 seeds into art/review/npc_gunnar/ to pick from
Options:
  --seed N      first seed (default: random, printed)       --steps N   sampler steps (default 30)
  --size PX     long side (default 1536; backgrounds 2048)  --force     overwrite an existing art/raw file
  --prepare     run prepare_art.py on what was written       --dry       print the rewritten prompts, generate nothing
  --refs a,b    style references to use instead (art keys, or `none`)
  --host URL    ComfyUI address (default http://127.0.0.1:8188)

Armor outfits (`char_<class>_<armor>`) are edits of the class's bare art at its exact canvas size, so
prepare_art.py can crop them like the bare art and the weapon anchors stay valid.
Needs Pillow and numpy, plus ComfyUI with the Qwen-Image 2.1 int8 files (models listed in UNET/CLIP/VAE).
"""
import io
import json
import random
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
QUEUE = ROOT / "art" / "QUEUE.md"
RAW = ROOT / "art" / "raw"
REVIEW = ROOT / "art" / "review"
ART = ROOT / "src" / "assets" / "art"
WORLD = ROOT / "src" / "data" / "world.ts"

UNET = "qwen_image_2.1_int8_convrot.safetensors"
CLIP = "qwen3vl_8b_int8_convrot.safetensors"
VAE = "qwen_image_2.1_vae_bf16.safetensors"

# Style references by key prefix: finished art that defines the look (first batch, before any queue work).
# Single objects get none: Qwen copies a reference object outright (an icon test with an armor, a potion or even a
# ring as reference came back as that object), and the prompt's own style text is close enough for them.
SPRITE_REFS = {
    "npc_": ["npc_elder_maren", "npc_hulda"],
    "mon_": ["mon_thornback_boar", "mon_mossling"],
    "icon_": [],
    "wpn_": [],
}
# A room with no area of its own: a scene with no buildings, so none get copied in.
DEFAULT_BG_REFS = ["bg_thornwood"]

RGBA_OPEN = "This is an RGBA format image with transparency."
RGBA_CLOSE = "The image has an alpha channel and a transparent background."


def parse_queue() -> tuple[dict[str, tuple[str, str]], list[str]]:
    """{key: (aspect, prompt)} for every prompt block, and the unticked keys in checklist order."""
    text = QUEUE.read_text(encoding="utf-8")
    items = {
        m.group(1): (m.group(2), m.group(3).strip())
        for m in re.finditer(r"^### `([a-z0-9_]+)` · (\d+:\d+)[^\n]*\n```\n(.*?)\n```", text, re.S | re.M)
    }
    pending = re.findall(r"^- \[ \] `([a-z0-9_]+)`", text, re.M)
    return items, pending


def art_file(key: str) -> Path:
    """A key's processed file: `<key>@2x.webp` once it's redone at 2x (scripts/prepare_art.py), else `<key>.webp`."""
    return next(iter(sorted(ART.glob(f"{key}@*x.webp"), reverse=True)), ART / f"{key}.webp")


def bg_refs(key: str) -> list[str]:
    """A room's own fallback backgrounds (src/data/world.ts) that have art: the area it belongs to."""
    for m in re.finditer(r"background:\s*\[([^\]]*)\]", WORLD.read_text(encoding="utf-8")):
        keys = re.findall(r"'([a-z0-9_]+)'", m.group(1))
        if keys and keys[0] == key:
            found = [k for k in keys[1:] if art_file(k).exists()]
            if found:
                return found[:2]
    return DEFAULT_BG_REFS


def refs_for(key: str) -> list[str]:
    if key.startswith("bg_"):
        return bg_refs(key)
    if key.startswith("char_"):
        return [f"char_{key.split('_')[1]}_bare"]
    return next((refs for prefix, refs in SPRITE_REFS.items() if key.startswith(prefix)), SPRITE_REFS["mon_"])


def rewrite(key: str, prompt: str, n_refs: int) -> str:
    """Turn a Higgsfield queue prompt into a Qwen one."""
    names = " and ".join(f"<image{i}>" for i in range(1, n_refs + 1))
    if key.startswith("char_"):
        body = re.sub(r"the plain flat (?:bright green|magenta) background", "a transparent background", prompt)
        return f"{RGBA_OPEN} {body} {RGBA_CLOSE}"
    body = re.sub(r"\s*Plain flat solid [^.]*?no border\.", "", prompt)
    body = re.sub(r"\s*Match the art style of the reference image exactly\.(?: Only the style, not the character\.)?", "", body)
    if n_refs == 0:
        return body if key.startswith("bg_") else f"{RGBA_OPEN} {body} {RGBA_CLOSE}"
    if key.startswith("bg_"):
        return (f"{body} Paint it in exactly the same art style as {names}, as if by the same artist for the same game. "
                f"Use {names} only as examples of the painting style: none of their buildings, objects, plants or layout "
                "appear in this scene.")
    style = (f"Draw it in exactly the same art style as {names}: the same bold dark outlines, the same cel shading with "
             "soft painted highlights, the same colors, line weight and proportions, as if by the same artist for the "
             f"same game. Use {names} only as examples of the art style: draw none of the creatures, characters or "
             "objects shown in them, only the subject described here.")
    return f"{RGBA_OPEN} {body} {style} {RGBA_CLOSE}"


def canvas(aspect: str, long_side: int) -> tuple[int, int]:
    a, b = (int(x) for x in aspect.split(":"))
    w, h = (long_side, long_side * b / a) if a >= b else (long_side * a / b, long_side)
    return round(w / 32) * 32, round(h / 32) * 32


def ref_png(key: str) -> bytes:
    """Reference image as PNG bytes. Sprites go on white (no key color to copy); an outfit's bare art keeps its raw
    canvas so the edit comes back at the same size and framing."""
    if key.startswith("char_") and key.endswith("_bare"):
        sys.path.insert(0, str(ROOT / "scripts"))
        import prepare_art  # noqa: E402  (only needed for outfits)

        raw = next(p for p in RAW.glob(f"{key}.*") if p.suffix.lower() in prepare_art.EXTS)
        rgb = np.asarray(Image.open(raw).convert("RGB")).astype(np.float32)
        clean, alpha, _, _ = prepare_art._cutout(rgb, key)
        white = clean * alpha[..., None] + 255 * (1 - alpha[..., None])
        img = Image.fromarray(white.clip(0, 255).astype(np.uint8), "RGB")
    else:
        art = Image.open(art_file(key)).convert("RGBA")
        if not key.startswith("bg_"):
            bg = Image.new("RGBA", art.size, (255, 255, 255, 255))
            bg.alpha_composite(art)
            art = bg
        scale = min(1.0, 1024 / max(art.size))
        img = art.convert("RGB").resize((round(art.width * scale), round(art.height * scale)), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


class Comfy:
    def __init__(self, host: str):
        self.host = host.rstrip("/")

    def check(self) -> None:
        try:
            urllib.request.urlopen(f"{self.host}/system_stats", timeout=5)
        except OSError:
            sys.exit(f"ComfyUI is not answering at {self.host}. Start it first (ComfyUI_windows_portable\\run_nvidia_gpu.bat).")

    def upload(self, name: str, data: bytes) -> str:
        boundary = uuid.uuid4().hex
        body = (
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\nContent-Type: image/png\r\n\r\n"
        ).encode() + data + (
            f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{boundary}--\r\n"
        ).encode()
        req = urllib.request.Request(f"{self.host}/upload/image", body, {"Content-Type": f"multipart/form-data; boundary={boundary}"})
        return json.load(urllib.request.urlopen(req))["name"]

    def generate(self, prompt: str, refs: list[str], size: tuple[int, int] | None, seed: int, steps: int) -> bytes:
        """One image. size=None: an edit at image_1's own size (outfits)."""
        g: dict = {
            "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
            "cache": {"class_type": "QwenImage21Cache", "inputs": {"model": ["unet", 0], "device": "auto", "dtype": "default"}},
            "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": CLIP, "type": "qwen_image", "device": "default"}},
            "vae": {"class_type": "VAELoader", "inputs": {"vae_name": VAE}},
        }
        enc = {"clip": ["clip", 0], "vae": ["vae", 0], "prompt": prompt, "negative_prompt": "", "resolution": 0 if size is None else 1024}
        for i, name in enumerate(refs, 1):
            g[f"ref{i}"] = {"class_type": "LoadImage", "inputs": {"image": name}}
            enc[f"images.image_{i}"] = [f"ref{i}", 0]
        g["enc"] = {"class_type": "TextEncodeQwenImage21", "inputs": enc}
        if size is None:
            latent = ["enc", 2]
        else:
            g["latent"] = {"class_type": "EmptyLatentImage", "inputs": {"width": size[0], "height": size[1], "batch_size": 1}}
            latent = ["latent", 0]
        g["sampler"] = {"class_type": "KSampler", "inputs": {
            "model": ["cache", 0], "positive": ["enc", 0], "negative": ["enc", 1], "latent_image": latent,
            "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}}
        g["decode"] = {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}}
        g["save"] = {"class_type": "SaveImage", "inputs": {"images": ["decode", 0], "filename_prefix": "drakepg"}}

        req = urllib.request.Request(f"{self.host}/prompt", json.dumps({"prompt": g}).encode(), {"Content-Type": "application/json"})
        try:
            pid = json.load(urllib.request.urlopen(req))["prompt_id"]
        except urllib.error.HTTPError as e:
            raise RuntimeError(e.read().decode()[:2000]) from e
        while True:
            hist = json.load(urllib.request.urlopen(f"{self.host}/history/{pid}"))
            if pid in hist:
                status = hist[pid].get("status", {})
                if status.get("status_str") == "error":
                    raise RuntimeError(json.dumps(status.get("messages", []))[:2000])
                images = [img for out in hist[pid]["outputs"].values() for img in out.get("images", [])]
                if images:
                    q = urllib.parse.urlencode({k: images[0][k] for k in ("filename", "subfolder", "type")})
                    return urllib.request.urlopen(f"{self.host}/view?{q}").read()
            time.sleep(1.5)


def main() -> None:
    args = sys.argv[1:]

    def opt(name: str, default: str | None = None) -> str | None:
        if name in args:
            i = args.index(name)
            value = args[i + 1]
            del args[i : i + 2]
            return value
        return default

    host = opt("--host", "http://127.0.0.1:8188")
    seed = int(opt("--seed") or random.randrange(1 << 31))
    steps = int(opt("--steps", "30"))
    size_opt = opt("--size")
    variants = int(opt("--variants", "1"))
    refs_opt = opt("--refs")
    flags = {a for a in args if a.startswith("--")}
    keys = [a for a in args if not a.startswith("--")]

    items, pending = parse_queue()
    if "--pending" in flags:
        keys += [k for k in pending if k not in keys]
    unknown = [k for k in keys if k not in items]
    if unknown:
        sys.exit(f"Not in art/QUEUE.md: {', '.join(unknown)}")
    if not keys:
        sys.exit(__doc__)

    comfy = Comfy(host)
    if "--dry" not in flags:
        comfy.check()
    print(f"seed {seed}")
    written: list[str] = []
    for key in keys:
        aspect, queue_prompt = items[key]
        refs = refs_for(key) if refs_opt is None else [r for r in refs_opt.split(",") if r and r != "none"]
        missing = [r for r in refs if not art_file(r).exists()]
        if missing:
            print(f"{key}: skipped, style reference art missing: {', '.join(missing)}")
            continue
        prompt = rewrite(key, queue_prompt, len(refs))
        long_side = int(size_opt or (2048 if key.startswith("bg_") else 1536))
        size = None if key.startswith("char_") else canvas(aspect, long_side)
        if "--dry" in flags:
            print(f"\n{key}  {size or 'bare art size'}  refs {refs}\n{prompt}")
            continue
        if variants == 1:
            targets = [(seed, RAW / f"{key}.png")]
            existing = [p for p in RAW.glob(f"{key}.*")]
            if existing and "--force" not in flags:
                print(f"{key}: skipped, {existing[0].name} already exists (--force overwrites, --variants N to compare)")
                continue
        else:
            targets = [(seed + i, REVIEW / key / f"{key}_s{seed + i}.png") for i in range(variants)]
        names = [comfy.upload(f"drakepg_ref_{r}.png", ref_png(r)) for r in refs]
        for s, path in targets:
            t0 = time.time()
            try:
                data = comfy.generate(prompt, names, size, s, steps)
            except RuntimeError as e:
                print(f"{key}: ComfyUI error: {e}")
                break
            path.parent.mkdir(parents=True, exist_ok=True)
            if variants == 1:
                for old in RAW.glob(f"{key}.*"):
                    old.unlink()
            path.write_bytes(data)
            print(f"{key}: {path.relative_to(ROOT)} (seed {s}, {time.time() - t0:.0f}s)")
        if variants == 1 and targets[0][1].exists():
            written.append(key)

    if written and "--prepare" in flags:
        subprocess.run([sys.executable, str(ROOT / "scripts" / "prepare_art.py"), *written], check=False)
    elif written:
        print(f"next: python3 scripts/prepare_art.py {' '.join(written)}")


if __name__ == "__main__":
    main()
