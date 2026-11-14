#!/usr/bin/env python3
"""Image model trial (docs/PLANS.md "Image model trial (2026-11-14)"): one entry per model, each with the function that
builds its ComfyUI API graph. Talks to the local ComfyUI over the API (default http://127.0.0.1:8188), never the browser.

  python scripts/model_trial.py list                      # the models and which files each needs
  python scripts/model_trial.py smoke                     # one campfire prop per model -> art/tests/models/<model>/
  python scripts/model_trial.py smoke krea2 --runs 2      # some models; run 2 = a warm second image, for timing
  python scripts/model_trial.py smoke --ref some.png      # also run the reference / edit graph with that picture

Models: qwen21 (baseline, what the game art is made with), krea2 ("everything else"), moss ("uncensored").
Each entry: `t2i(job)` and `ref(job)` (None if the model has no such graph) return a graph {node id: node}; `job` is a
dict with prompt, seed, width, height, steps, negative, refs (names already uploaded to ComfyUI). `wrap` dresses the
prompt for what the model needs for a clean background (Qwen has native transparency, the others get a plain white one).
Smoke results (seconds, peak VRAM) are appended to art/tests/models/smoke.json. The next step adds the test set on top:
call `generate(model, kind, job)` from a script of its own.
"""
import io
import json
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from qwen_batch import CLIP as QWEN_CLIP, RGBA_CLOSE, RGBA_OPEN, UNET as QWEN_UNET, VAE as QWEN_VAE  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "art" / "tests" / "models"
HOST = "http://127.0.0.1:8188"

# --- the smoke prompt: the campfire prop (a thing Qwen already draws well), in the game's flat cut-out look ---
SMOKE_PROMPT = (
    "A small campfire: a ring of grey stones, three crossed logs and bright orange and yellow flames. Drawn in a flat "
    "cut-out cartoon style: clean thin dark outlines, flat colours with hard-edged cel shadows, matte surfaces with no "
    "glossy shine, simple readable shapes and minimal detail. Seen from the side at eye level, the whole object "
    "centered with empty margin around it. Nothing else in the picture: no ground, no shadow, no characters, no text.")
PLAIN_BG = " On a plain flat white background."


# ---------------------------------------------------------------- ComfyUI client
def _post(path: str, payload: dict) -> dict:
    req = urllib.request.Request(HOST + path, json.dumps(payload).encode(), {"Content-Type": "application/json"})
    try:
        body = urllib.request.urlopen(req).read()
        return json.loads(body) if body.strip() else {}
    except urllib.error.HTTPError as e:
        raise RuntimeError(e.read().decode()[:2000]) from e


def check() -> None:
    try:
        urllib.request.urlopen(f"{HOST}/system_stats", timeout=5)
    except OSError:
        sys.exit(f"ComfyUI is not answering at {HOST}. Start it first (see docs/PLANS.md, Image model trial).")


def upload(name: str, data: bytes) -> str:
    b = uuid.uuid4().hex
    body = (f'--{b}\r\nContent-Disposition: form-data; name="image"; filename="{name}"\r\nContent-Type: image/png\r\n\r\n'
            ).encode() + data + (f'\r\n--{b}\r\nContent-Disposition: form-data; name="overwrite"\r\n\r\ntrue\r\n--{b}--\r\n').encode()
    req = urllib.request.Request(f"{HOST}/upload/image", body, {"Content-Type": f"multipart/form-data; boundary={b}"})
    return json.load(urllib.request.urlopen(req))["name"]


def free_models() -> None:
    """Unload everything so the next model starts from the same empty VRAM (and its timing includes its own load)."""
    _post("/free", {"unload_models": True, "free_memory": True})


def run_graph(graph: dict) -> bytes:
    pid = _post("/prompt", {"prompt": graph})["prompt_id"]
    while True:
        hist = json.load(urllib.request.urlopen(f"{HOST}/history/{pid}"))
        if pid in hist:
            st = hist[pid].get("status", {})
            if st.get("status_str") == "error":
                raise RuntimeError(json.dumps(st.get("messages", []))[-2000:])
            imgs = [i for o in hist[pid]["outputs"].values() for i in o.get("images", [])]
            if imgs:
                q = urllib.parse.urlencode({k: imgs[0][k] for k in ("filename", "subfolder", "type")})
                return urllib.request.urlopen(f"{HOST}/view?{q}").read()
        time.sleep(1)


class VramWatch:
    """Samples nvidia-smi while a job runs: the peak of memory used on the GPU (everything on it, not just ComfyUI)."""

    def __enter__(self):
        self.peak = self.base = self._used()
        self._stop = False
        self._t = threading.Thread(target=self._loop, daemon=True)
        self._t.start()
        return self

    def __exit__(self, *a):
        self._stop = True
        self._t.join()

    @staticmethod
    def _used() -> int:
        try:
            out = subprocess.check_output(["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"], text=True)
            return int(out.split()[0])
        except (OSError, subprocess.CalledProcessError, ValueError):
            return 0

    def _loop(self) -> None:
        while not self._stop:
            self.peak = max(self.peak, self._used())
            time.sleep(0.4)


def numbered(g: dict) -> dict:
    """ComfyUI's usual numeric node ids, so the graph also opens in its window (names -> 1, 2, 3...)."""
    ids = {name: str(i) for i, name in enumerate(g, 1)}
    fix = lambda v: [ids[v[0]], v[1]] if isinstance(v, list) and len(v) == 2 and v[0] in ids else v  # noqa: E731
    return {ids[n]: {**node, "inputs": {k: fix(v) for k, v in node["inputs"].items()}, "_meta": {"title": n}} for n, node in g.items()}


def save_node(g: dict, image_from: list) -> None:
    g["save"] = {"class_type": "SaveImage", "inputs": {"images": image_from, "filename_prefix": "trial"}}


# ---------------------------------------------------------------- graphs: Qwen-Image 2.1 (baseline)
def qwen_graph(job: dict) -> dict:
    """Text to image, or an edit / style-reference run when job['refs'] has uploaded names (up to 10; <image1> in the
    prompt). Same graph the game's art scripts use (scripts/qwen_batch.py)."""
    g = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": QWEN_UNET, "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": QWEN_CLIP, "type": "qwen_image", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": QWEN_VAE}},
        "cache": {"class_type": "QwenImage21Cache", "inputs": {"model": ["unet", 0], "device": "auto", "dtype": "default"}},
    }
    refs = job.get("refs", [])
    edit = bool(refs) and job.get("edit", False)  # an edit keeps the reference's own size
    enc = {"clip": ["clip", 0], "vae": ["vae", 0], "prompt": job["prompt"], "negative_prompt": job.get("negative", ""),
           "resolution": 0 if edit else 1024}
    for i, name in enumerate(refs, 1):
        g[f"ref{i}"] = {"class_type": "LoadImage", "inputs": {"image": name}}
        enc[f"images.image_{i}"] = [f"ref{i}", 0]
    g["enc"] = {"class_type": "TextEncodeQwenImage21", "inputs": enc}
    if edit:
        latent = ["enc", 2]
    else:
        g["latent"] = {"class_type": "EmptyLatentImage", "inputs": {"width": job["width"], "height": job["height"], "batch_size": 1}}
        latent = ["latent", 0]
    g["sampler"] = {"class_type": "KSampler", "inputs": {
        "model": ["cache", 0], "positive": ["enc", 0], "negative": ["enc", 1], "latent_image": latent, "seed": job["seed"],
        "steps": job.get("steps", 30), "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}}
    g["decode"] = {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}}
    save_node(g, ["decode", 0])
    return numbered(g)


# ---------------------------------------------------------------- graphs: Krea 2 Turbo
KREA_UNET = "krea2_turbo_fp8_scaled.safetensors"
KREA_CLIP = "qwen3vl_4b_fp8_scaled.safetensors"
KREA_VAE = "qwen_image_vae.safetensors"
KREA_STYLE_LORA = "krea2_style_reference.safetensors"


def krea_t2i(job: dict) -> dict:
    """The native ComfyUI Krea 2 Turbo graph: 8 steps, cfg 1 (so no real negative: the empty one is the positive zeroed out)."""
    g = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": KREA_UNET, "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": KREA_CLIP, "type": "krea2", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": KREA_VAE}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": job["prompt"]}},
        "neg": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["pos", 0]}},
        "latent": {"class_type": "EmptyLatentImage", "inputs": {"width": job["width"], "height": job["height"], "batch_size": 1}},
        "sampler": {"class_type": "KSampler", "inputs": {
            "model": ["unet", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["latent", 0],
            "seed": job["seed"], "steps": job.get("steps", 8), "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}},
    }
    save_node(g, ["decode", 0])
    return numbered(g)


def krea_ref(job: dict) -> dict:
    """Krea 2's reference: a STYLE reference only (ostris's style LoRA + up to 3 pictures; no edits of a given picture).
    Same graph as ComfyUI's image_krea2_turbo style-reference template."""
    g = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": KREA_UNET, "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": KREA_CLIP, "type": "krea2", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": KREA_VAE}},
        "lora": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["unet", 0], "lora_name": KREA_STYLE_LORA, "strength_model": 1.0}},
        "flux": {"class_type": "ModelSamplingFlux", "inputs": {"model": ["lora", 0], "max_shift": 1.15, "base_shift": 0.5,
                                                               "width": job["width"], "height": job["height"]}},
        "enc": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {"clip": ["clip", 0], "vae": ["vae", 0], "prompt": job["prompt"]}},
        "pos": {"class_type": "FluxKontextMultiReferenceLatentMethod", "inputs": {"conditioning": ["enc", 0], "reference_latents_method": "index_timestep_zero"}},
        "neg": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["pos", 0]}},
        "guider": {"class_type": "CFGGuider", "inputs": {"model": ["flux", 0], "positive": ["pos", 0], "negative": ["neg", 0], "cfg": 1.0}},
        "ksel": {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "euler"}},
        "sigmas": {"class_type": "BasicScheduler", "inputs": {"model": ["flux", 0], "scheduler": "simple", "steps": job.get("steps", 8), "denoise": 1.0}},
        "noise": {"class_type": "RandomNoise", "inputs": {"noise_seed": job["seed"]}},
        "latent": {"class_type": "EmptyLatentImage", "inputs": {"width": job["width"], "height": job["height"], "batch_size": 1}},
        "sampler": {"class_type": "SamplerCustomAdvanced", "inputs": {"noise": ["noise", 0], "guider": ["guider", 0], "sampler": ["ksel", 0],
                                                                       "sigmas": ["sigmas", 0], "latent_image": ["latent", 0]}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}},
    }
    for i, name in enumerate(job["refs"][:3], 1):
        g[f"ref{i}"] = {"class_type": "LoadImage", "inputs": {"image": name}}
        g["enc"]["inputs"][f"image{i}"] = [f"ref{i}", 0]
    save_node(g, ["decode", 0])
    return numbered(g)


# ---------------------------------------------------------------- graphs: MOSS-Image (SFT)
MOSS_UNET = "moss_image_sft_int8_convrot.safetensors"
MOSS_CLIP = "qwen3.5_9b_w4a8_gptq.safetensors"
MOSS_VAE = "flux2-vae_small.safetensors"
MOSS_EDIT_LORA = "moss_image_edit_lora_rank_64_bf16.safetensors"
MOSS_NEG = "nsfw, worst quality, bad anatomy, watermark, jpeg artifact"  # ComfyUI's template negative


def moss_t2i(job: dict) -> dict:
    """The native ComfyUI MOSS-Image (SFT) graph: 25 steps, cfg 2.5, er_sde. The prompt enhancer (an LLM pass) is left off."""
    g = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": MOSS_UNET, "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": MOSS_CLIP, "type": "moss_image", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": MOSS_VAE}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": job["prompt"]}},
        "neg": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": job.get("negative", MOSS_NEG)}},
        "latent": {"class_type": "EmptyFlux2LatentImage", "inputs": {"width": job["width"], "height": job["height"], "batch_size": 1}},
        "sampler": {"class_type": "KSampler", "inputs": {
            "model": ["unet", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["latent", 0],
            "seed": job["seed"], "steps": job.get("steps", 25), "cfg": 2.5, "sampler_name": "er_sde", "scheduler": "simple", "denoise": 1.0}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}},
    }
    save_node(g, ["decode", 0])
    return numbered(g)


def moss_edit(job: dict) -> dict:
    """MOSS-Image edit: the SFT model + the edit LoRA; job['refs'][0] is the picture, job['prompt'] says what to change
    ("Change only X ... Keep Y unchanged"). The picture is scaled to about 1 megapixel and the result has that size."""
    g = {
        "src": {"class_type": "LoadImage", "inputs": {"image": job["refs"][0]}},
        "scale": {"class_type": "ImageScaleToTotalPixels", "inputs": {"image": ["src", 0], "upscale_method": "lanczos", "megapixels": 1.0, "resolution_steps": 16}},
        "size": {"class_type": "GetImageSize", "inputs": {"image": ["scale", 0]}},
        "latent": {"class_type": "EmptyFlux2LatentImage", "inputs": {"width": ["size", 0], "height": ["size", 1], "batch_size": 1}},
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": MOSS_UNET, "weight_dtype": "default"}},
        "lora": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["unet", 0], "lora_name": MOSS_EDIT_LORA, "strength_model": 1.0}},
        "slg": {"class_type": "SkipLayerGuidanceDiTSimple", "inputs": {"model": ["lora", 0], "double_layers": "", "single_layers": "22", "start_percent": 0.0, "end_percent": 1.0}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": MOSS_CLIP, "type": "moss_image", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": MOSS_VAE}},
    }
    for name, text in (("pos", job["prompt"]), ("neg", job.get("negative", MOSS_NEG))):
        g[name] = {"class_type": "TextEncodeMOSSImageEdit", "inputs": {
            "clip": ["clip", 0], "prompt": text, "edit_format": "Moss-Image-Edit", "image_strength": 1.0,
            "vae": ["vae", 0], "images.image_0": ["scale", 0]}}
    g["sampler"] = {"class_type": "KSampler", "inputs": {
        "model": ["slg", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["latent", 0], "seed": job["seed"],
        "steps": job.get("steps", 30), "cfg": 4.0, "sampler_name": "euler", "scheduler": "normal", "denoise": 1.0}}
    g["decode"] = {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}}
    save_node(g, ["decode", 0])
    return numbered(g)


# ---------------------------------------------------------------- the models
MODELS = {
    "qwen21": {
        "label": "Qwen-Image 2.1 (baseline: text to image, edit, style references, transparent output)",
        "files": [QWEN_UNET, QWEN_CLIP, QWEN_VAE],
        "t2i": qwen_graph, "ref": qwen_graph, "ref_is": "edit and style references (up to 10 pictures, <image1> in the prompt)",
        "wrap": lambda p: f"{RGBA_OPEN} {p} {RGBA_CLOSE}",
    },
    "krea2": {
        "label": "Krea 2 Turbo, fp8 (text to image in 8 steps; style references)",
        "files": [KREA_UNET, KREA_CLIP, KREA_VAE, KREA_STYLE_LORA],
        "t2i": krea_t2i, "ref": krea_ref, "ref_is": "style reference only (1-3 pictures + the style LoRA), no edits",
        "wrap": lambda p: p + PLAIN_BG,
    },
    "moss": {
        "label": "MOSS-Image SFT, int8 (text to image in 25 steps; edit with the edit LoRA)",
        "files": [MOSS_UNET, MOSS_CLIP, MOSS_VAE, MOSS_EDIT_LORA],
        "t2i": moss_t2i, "ref": moss_edit, "ref_is": "edit of one picture (edit LoRA), no style reference",
        "wrap": lambda p: p + PLAIN_BG,
    },
}


def generate(model: str, kind: str, job: dict) -> tuple[bytes, float, int]:
    """One image: (png bytes, seconds, peak VRAM in MiB while it ran). kind is 't2i' or 'ref'. job: prompt, seed, width,
    height, optional steps / negative / refs (uploaded names) / edit (Qwen: keep the reference's size). The prompt is
    used as given; `MODELS[model]['wrap'](prompt)` dresses it for a clean background."""
    graph = MODELS[model][kind](job)
    with VramWatch() as v:
        t = time.time()
        data = run_graph(graph)
        secs = time.time() - t
    return data, secs, v.peak


def smoke(models: list[str], runs: int, ref: Path | None) -> None:
    check()
    log = OUT / "smoke.json"
    results = json.loads(log.read_text(encoding="utf-8")) if log.exists() else []
    for m in models:
        entry = MODELS[m]
        free_models()
        for i in range(runs):
            seed = 1 + i
            job = {"prompt": entry["wrap"](SMOKE_PROMPT), "seed": seed, "width": 1024, "height": 1024}
            out = OUT / m / f"smoke_campfire_s{seed}.png"
            out.parent.mkdir(parents=True, exist_ok=True)
            try:
                data, secs, peak = generate(m, "t2i", job)
            except Exception as e:  # one model failing must not stop the others
                print(f"{m}: FAILED {e}")
                results.append({"model": m, "kind": "t2i", "seed": seed, "error": str(e)[:500]})
                break
            out.write_bytes(data)
            from PIL import Image
            mode = Image.open(io.BytesIO(data)).mode
            note = ("cold: includes loading the model" if i == 0 else "warm")
            print(f"{m}: {out.relative_to(ROOT)}  {secs:.0f} s ({note})  peak VRAM {peak} MiB  mode {mode}")
            results.append({"model": m, "kind": "t2i", "seed": seed, "seconds": round(secs, 1), "run": note,
                            "peak_vram_mib": peak, "mode": mode, "file": str(out.relative_to(ROOT))})
        if ref and entry.get("ref"):
            name = upload("trial_ref.png", ref.read_bytes())
            job = {"prompt": "A small campfire." if m != "moss" else "Change only the flames to blue. Keep everything else unchanged.",
                   "seed": 1, "width": 1024, "height": 1024, "refs": [name], "edit": True}
            out = OUT / m / "smoke_ref_s1.png"
            try:
                data, secs, peak = generate(m, "ref", job)
                out.write_bytes(data)
                print(f"{m}: reference graph ({entry['ref_is']}): {out.relative_to(ROOT)}  {secs:.0f} s  peak VRAM {peak} MiB")
                results.append({"model": m, "kind": "ref", "seconds": round(secs, 1), "peak_vram_mib": peak, "file": str(out.relative_to(ROOT))})
            except Exception as e:
                print(f"{m}: reference graph FAILED {e}")
                results.append({"model": m, "kind": "ref", "error": str(e)[:500]})
    free_models()
    OUT.mkdir(parents=True, exist_ok=True)
    log.write_text(json.dumps(results, indent=1), encoding="utf-8")


def main() -> None:
    args = sys.argv[1:]
    if not args or args[0] not in ("list", "smoke"):
        sys.exit(__doc__)
    if args[0] == "list":
        models_dir = Path.home() / "ComfyUI_windows_portable" / "ComfyUI" / "models"
        for k, m in MODELS.items():
            have = [f for f in m["files"] if any(models_dir.rglob(f))]
            print(f"{k}: {m['label']}\n   reference: {m['ref_is']}\n   files: {len(have)}/{len(m['files'])} found under {models_dir}")
        return
    runs, ref = 1, None
    if "--runs" in args:
        i = args.index("--runs")
        runs = int(args[i + 1])
        del args[i:i + 2]
    if "--ref" in args:
        i = args.index("--ref")
        ref = Path(args[i + 1])
        del args[i:i + 2]
    names = [a for a in args[1:] if not a.startswith("--")] or list(MODELS)
    bad = [n for n in names if n not in MODELS]
    if bad:
        sys.exit(f"unknown model {bad}; have {list(MODELS)}")
    smoke(names, runs, ref)


if __name__ == "__main__":
    main()
