#!/usr/bin/env python3
"""Image model trial (docs/PLANS.md "Image model trial (2026-11-14)"): one entry per model, each with the function that
builds its ComfyUI API graph. Talks to the local ComfyUI over the API (default http://127.0.0.1:8188), never the browser.

  python scripts/model_trial.py list                      # the models and which files each needs
  python scripts/model_trial.py smoke                     # one campfire prop per model -> art/tests/models/<model>/
  python scripts/model_trial.py smoke krea2 --runs 2      # some models; run 2 = a warm second image, for timing
  python scripts/model_trial.py smoke --ref some.png      # also run the reference / edit graph with that picture
  python scripts/model_trial.py trial [--dry]             # step 2: the whole test set, unattended; skips files that exist
  python scripts/model_trial.py sheets                    # step 2: cut-outs + comparison sheets (art/tests/models/_sheets)
  python scripts/model_trial.py stats                     # seconds per image per model (art/tests/models/trial.json)

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


# ---------------------------------------------------------------- step 2: the test set on every model (`trial`)
# The Qwen batch tries (seeds 7100, 7101) and the style sheets exist only in the unity-port worktree (git-ignored there).
WT_REDO = ROOT / ".claude" / "worktrees" / "unity-port" / "art" / "redo"
STYLE = WT_REDO / "_style"
GAME = ROOT / "src" / "assets" / "art"
CUT = OUT / "_cutout"
SHEETS = OUT / "_sheets"
SEEDS = (7100, 7101)
MAGENTA_BG = " On a plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow."
# Words-only stand-ins for what Qwen got from a reference picture (the game's sprites, described by eye).
WORDS_STYLE = ("Drawn in a flat cut-out cartoon style for a 2D game: clean thin dark outlines, flat colours with "
               "hard-edged cel shadows, matte surfaces with no glossy shine, a warm golden rim light, simple readable "
               "shapes and minimal detail.")
HERO_WORDS = ("a young hero with messy dark brown hair and a red headband with tails, a cream shirt with rolled sleeves, "
              "a brown belt, dark teal trousers and brown boots, empty hands")
HULDA_WORDS = ("a broad-shouldered blacksmith woman in her forties: long auburn hair in a thick braid over one shoulder, "
               "soot on her cheek, a brown shirt with rolled-up sleeves, a long dark brown leather apron, heavy dark "
               "leather gloves, a smithing hammer resting on her shoulder")
MOSSLING_WORDS = ("a small round moss creature: a fluffy ball of green moss and leaves with two big round yellow eyes, "
                  "two small orange mushrooms on top and stubby mossy feet")
THORNWOOD_ROOM = ("A narrow glen deep in the Thornwood forest: huge dark old trees with gnarled roots on both sides, "
                  "their trunks wrapped in black thorny briars, a fallen mossy log and ferns on the left, bramble bushes "
                  "with red berries, pale shafts of light falling through the canopy. The bottom 30% of the image is "
                  "flat, open walkable ground running across the full width. No characters, no creatures.")
BLOOD = ("A slain cartoon wild boar lying on its side, its eyes shut as two small crosses and its tongue hanging out, in "
         "a pool of bright red blood spreading from under it, a few red blood drops around it. " + WORDS_STYLE +
         " Seen from the side, the whole scene centered with empty margin around it. No other creatures, no people, "
         "no text.")
CUT_SUBJECTS = ["prop_cart_wheel", "wpn_galecutter", "fx_heal"]


def trial_set() -> dict:
    """{subject: kind ('sprite' | 'fx' | 'bg'), size, words (the prompt without its background line), ref (Krea's
    style reference: the picture Qwen used for this kind of subject), today (the game's art, or None), qwen (the
    existing Qwen tries by seed, or a job to make them), edit (Moss edit: source + prompt)}. Prompts come from
    scripts/asset_batch.py so the comparison with the Qwen batch is fair."""
    import asset_batch as A
    import redo_art as R
    no_bg = lambda s: s.replace("no background, ", "")  # noqa: E731  (Krea and Moss get a plain background line)
    tries = lambda k: {s: WT_REDO / k / f"{k}_s{s}.png" for s in SEEDS}  # noqa: E731
    S: dict = {}
    for k in ("prop_campfire_b", "prop_cart_wheel", "prop_ice_block"):
        S[k] = dict(kind="sprite", size=(1024, 1024), words=f"{A.PROPS[k]} {WORDS_STYLE} {no_bg(A.PROP_FRAME)}",
                    ref=STYLE / "monsters.png", today=None, qwen=tries(k))
    wpn = A.entries("wpn")
    for k in ("wpn_galecutter", "wpn_stormcleaver"):
        S[k] = dict(kind="sprite", size=(864, 1536), words=f"{wpn[k][3]} {WORDS_STYLE} {R.WPN_FRAME}",
                    ref=STYLE / "monsters.png", today=GAME / f"{k}@2x.webp", qwen=tries(k))
    for k in ("fx_heal", "fx_stab", "fx_fire_burst"):
        # Qwen made effects with no reference; Krea's style run gets the icon sheet, the nearest kind of picture.
        S[k] = dict(kind="fx", size=(1024, 1024), words=f"{A.VFX[k]} {no_bg(A.VFX_STYLE)}", ref=STYLE / "icons.png",
                    today=None, qwen=tries(k))
    S["portrait_hulda"] = dict(
        kind="sprite", size=(1024, 1024), ref=GAME / "npc_hulda@2x.webp", today=GAME / "npc_hulda@2x.webp",
        qwen=tries("portrait_hulda"),
        words=(f"A close-up head-and-shoulders portrait of {HULDA_WORDS}. {WORDS_STYLE} Three-quarter view facing right, "
               "a calm friendly expression, the head and shoulders filling most of the square, cut off cleanly below "
               "the chest. Nothing else: no frame, no text."))
    S["pose_cheer"] = dict(
        kind="sprite", size=(1024, 1536), ref=GAME / "char_hero_bare@2x.webp", today=GAME / "char_hero_bare@2x.webp",
        qwen=tries("pose_cheer"),
        words=(f"Full body, {HERO_WORDS}, in {A.HERO_POSES['pose_cheer']}. {WORDS_STYLE} Three-quarter view facing "
               "right, the whole figure visible with empty margin around it. No weapon, no ground, no shadow, no text."),
        edit=(GAME / "char_hero_bare@2x.webp",
              f"Change only his pose: the hero is now in {A.HERO_POSES['pose_cheer']}. "
              + A.KEEP_HERO + " Keep the plain white background."))
    S["mon_mossling_strike"] = dict(
        kind="sprite", size=(1024, 1024), ref=GAME / "mon_mossling@2x.webp", today=GAME / "mon_mossling@2x.webp",
        qwen=tries("mon_mossling_strike"),
        words=(f"Full body, {MOSSLING_WORDS}, {A.MON_POSES['strike']}. {WORDS_STYLE} Side view facing right, the whole "
               "figure visible with empty margin around it. No ground, no shadow, no text."),
        edit=(GAME / "mon_mossling@2x.webp",
              f"Change only the pose of the creature: it is {A.MON_POSES['strike']}. "
              + A.KEEP_FIGURE.format(what="creature").replace("no background, ", "") + " Keep the plain white background."))
    bg_words = f"A wide 16:9 background for a 2D side-view fantasy RPG. {THORNWOOD_ROOM} {R.BG_STYLE}"
    S["bg_thornwood_glen"] = dict(
        kind="bg", size=(2048, 1152), words=bg_words, ref=STYLE / "bg.png", today=GAME / "bg_thornwood@2x.webp",
        qwen=dict(prompt=f"{bg_words} {R.BG_REF}", refs=[STYLE / "bg.png"]))
    return S


def trial_jobs() -> list[dict]:
    """Every image of step 2 in run order (one model at a time: Krea 2, Moss, Qwen, then the BiRefNet cut-outs)."""
    S = trial_set()
    jobs: list[dict] = []

    def add(model, subject, variant, seed, kind, prompt=None, size=(1024, 1024), refs=(), edit=False):
        name = f"{subject}__{variant}_s{seed}.png"
        out = (CUT / name) if model == "birefnet" else (OUT / model / name)
        jobs.append(dict(model=model, subject=subject, variant=variant, seed=seed, kind=kind, prompt=prompt,
                         size=size, refs=list(refs), edit=edit, out=out))

    bg_line = lambda s: "" if S[s]["kind"] == "bg" else PLAIN_BG  # noqa: E731  (a room fills the picture)
    for s, d in S.items():
        for seed in SEEDS:
            add("krea2", s, "words", seed, "t2i", d["words"] + bg_line(s), d["size"])
            add("krea2", s, "style", seed, "ref", d["words"] + bg_line(s), d["size"], [d["ref"]])
    for seed in SEEDS:
        add("krea2", "blood_boar", "words", seed, "t2i", BLOOD + PLAIN_BG)
    for s in CUT_SUBJECTS:
        add("krea2", s, "magenta", SEEDS[0], "t2i", S[s]["words"] + MAGENTA_BG, S[s]["size"])
    for s, d in S.items():
        for seed in SEEDS:
            add("moss", s, "words", seed, "t2i", d["words"] + bg_line(s), d["size"])
    for s, d in S.items():
        if d.get("edit"):
            for seed in SEEDS:
                add("moss", s, "edit", seed, "ref", d["edit"][1], refs=[d["edit"][0]], edit=True)
    for seed in SEEDS:
        add("moss", "blood_boar", "words", seed, "t2i", BLOOD + PLAIN_BG)
    for s in CUT_SUBJECTS:
        add("moss", s, "magenta", SEEDS[0], "t2i", S[s]["words"] + MAGENTA_BG, S[s]["size"])
    for s, d in S.items():
        if "prompt" in d["qwen"]:
            for seed in SEEDS:
                add("qwen21", s, "style", seed, "ref", d["qwen"]["prompt"], d["size"], d["qwen"]["refs"])
    for seed in SEEDS:
        add("qwen21", "blood_boar", "words", seed, "t2i", f"{RGBA_OPEN} {BLOOD} {RGBA_CLOSE}")
    thing = {"prop_cart_wheel": "broken cart wheel and grain sack", "wpn_galecutter": "dagger", "fx_heal": "green effect"}
    for src in ("krea2", "moss"):
        for s in CUT_SUBJECTS:
            source = OUT / src / f"{s}__words_s{SEEDS[0]}.png"
            text = (f"{RGBA_OPEN} Edit <image1>. Remove only the white background: keep the {thing[s]} exactly the "
                    "same, with the same shapes, colours, outlines and size, in the same place. Everything that was "
                    f"white background is now fully transparent. {RGBA_CLOSE}")
            add("qwen21", s, f"cut_{src}", SEEDS[0], "ref", text, refs=[source], edit=True)
    for src in ("krea2", "moss"):
        for s in CUT_SUBJECTS:
            add("birefnet", s, f"mask_{src}", SEEDS[0], "mask", refs=[OUT / src / f"{s}__words_s{SEEDS[0]}.png"])
    return jobs


def birefnet_graph(job: dict) -> dict:
    """ComfyUI's built-in background removal (BiRefNet, already in models/background_removal): the mask as a picture."""
    g = {
        "src": {"class_type": "LoadImage", "inputs": {"image": job["refs"][0]}},
        "model": {"class_type": "LoadBackgroundRemovalModel", "inputs": {"bg_removal_name": "birefnet.safetensors"}},
        "remove": {"class_type": "RemoveBackground", "inputs": {"bg_removal_model": ["model", 0], "image": ["src", 0]}},
        "img": {"class_type": "MaskToImage", "inputs": {"mask": ["remove", 0]}},
    }
    save_node(g, ["img", 0])
    return numbered(g)


def trial(dry: bool) -> None:
    jobs = trial_jobs()
    if dry:
        for j in jobs:
            print(f"{j['model']:8} {j['out'].name:44} {j['kind']:4} {j['size']} refs={[Path(r).name for r in j['refs']]}\n   {j['prompt']}")
        todo = [j for j in jobs if not j["out"].exists()]
        print(f"\n{len(jobs)} jobs, {len(todo)} still to make")
        return
    check()
    log = OUT / "trial.json"
    results = json.loads(log.read_text(encoding="utf-8")) if log.exists() else []
    uploaded: dict[str, str] = {}
    from hero_edits import on_white  # gotcha 1: references and edit sources go in flattened on white

    def ref(p: Path) -> str:
        if str(p) not in uploaded:
            uploaded[str(p)] = upload(f"trial_{p.parent.name}_{p.stem.replace('@', '_')}.png", on_white(p))
        return uploaded[str(p)]

    model = None
    todo = [j for j in jobs if not j["out"].exists()]
    print(f"{len(todo)} of {len(jobs)} images to make", flush=True)
    for n, j in enumerate(todo, 1):
        if j["model"] != model:
            free_models()
            model, cold = j["model"], True
        missing = [r for r in j["refs"] if not Path(r).exists()]
        if missing:
            print(f"{n}/{len(todo)} {j['out'].name}: SKIPPED, source missing {missing}", flush=True)
            continue
        w, h = j["size"]
        job = {"prompt": j["prompt"], "seed": j["seed"], "width": w, "height": h, "edit": j["edit"]}
        try:
            job["refs"] = [ref(Path(r)) for r in j["refs"]]
            if j["model"] == "birefnet":
                with VramWatch() as v:
                    t = time.time()
                    data = run_graph(birefnet_graph(job))
                    secs, peak = time.time() - t, v.peak
            else:
                data, secs, peak = generate(j["model"], j["kind"] if j["kind"] != "mask" else "t2i", job)
        except Exception as e:  # one failure must not end the run
            print(f"{n}/{len(todo)} {j['out'].name}: FAILED {str(e)[:300]}", flush=True)
            results.append({"model": j["model"], "file": j["out"].name, "error": str(e)[:500]})
            log.write_text(json.dumps(results, indent=1), encoding="utf-8")
            cold = True
            continue
        j["out"].parent.mkdir(parents=True, exist_ok=True)
        j["out"].write_bytes(data)
        print(f"{n}/{len(todo)} {j['model']}/{j['out'].name}  {secs:.0f} s{' (cold)' if cold else ''}  peak {peak} MiB", flush=True)
        results.append({"model": j["model"], "subject": j["subject"], "variant": j["variant"], "seed": j["seed"],
                        "seconds": round(secs, 1), "cold": cold, "peak_vram_mib": peak, "size": list(j["size"]),
                        "file": str(j["out"].relative_to(ROOT))})
        log.write_text(json.dumps(results, indent=1), encoding="utf-8")
        cold = False
    free_models()
    stats()


def stats() -> None:
    """Average seconds per image per model from art/tests/models/trial.json (all runs, and warm runs only)."""
    log = OUT / "trial.json"
    results = [r for r in json.loads(log.read_text(encoding="utf-8")) if "seconds" in r] if log.exists() else []
    for m in ("krea2", "moss", "qwen21", "birefnet"):
        rs = [r for r in results if r["model"] == m]
        if rs:
            warm = [r["seconds"] for r in rs if not r["cold"]]
            print(f"{m:8} {len(rs):3} images  avg {sum(r['seconds'] for r in rs) / len(rs):5.1f} s  "
                  f"warm avg {sum(warm) / max(len(warm), 1):5.1f} s  max peak {max(r['peak_vram_mib'] for r in rs)} MiB")
    errors = [r for r in json.loads(log.read_text(encoding="utf-8")) if "error" in r] if log.exists() else []
    for e in errors:
        print(f"error: {e['model']} {e['file']}: {e['error'][:200]}")


# ---------------------------------------------------------------- cut-outs and sheets (`sheets`)
PAPER, DARK, LIGHT = (214, 205, 182), (40, 38, 46), (240, 238, 232)


def key_out(img, key: tuple[int, int, int] | None, tol: float, enclosed: bool = False):
    """Cut a flat background colour out: pixels near `key` and connected to the border become transparent; a 3 px band
    of the subject next to them gets colour-to-alpha against `key` (soft edges without a coloured fringe)."""
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    a = np.asarray(img.convert("RGB")).astype(np.float32)
    if key is None:  # the colour the model really drew (Krea's "magenta" is a muted pink with paper grain)
        edge = np.concatenate([a[:4].reshape(-1, 3), a[-4:].reshape(-1, 3), a[:, :4].reshape(-1, 3), a[:, -4:].reshape(-1, 3)])
        key = tuple(np.median(edge, axis=0))
    k = np.array(key, np.float32)
    near = np.sqrt(((a - k) ** 2).sum(-1)) < tol
    m = Image.fromarray(np.pad(near, 1, constant_values=True).astype(np.uint8) * 255, "L").copy()  # writable
    ImageDraw.floodfill(m, (0, 0), 128)
    bg = np.asarray(m)[1:-1, 1:-1] == 128
    if enclosed:  # also holes inside the subject (a colour the subject never uses, like magenta)
        bg = bg | near
    band = (np.asarray(Image.fromarray(bg.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(7))) > 0) & ~bg
    up = np.where(a > k, (a - k) / np.maximum(255 - k, 1), 0)
    down = np.where(a < k, (k - a) / np.maximum(k, 1), 0)
    soft = np.maximum(up, down).max(-1).clip(0, 1)
    alpha = np.where(bg, 0.0, np.where(band, soft, 1.0))
    unmixed = (a - k) / np.maximum(alpha[..., None], 1e-3) + k
    rgb = np.where((band & (alpha > 0.01))[..., None], unmixed, a).clip(0, 255)
    return Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA")


def make_cutouts() -> None:
    """The four routes on the Krea 2 and Moss pictures -> art/tests/models/_cutout/<subject>__<route>_<model>.png."""
    from PIL import Image
    CUT.mkdir(parents=True, exist_ok=True)
    s0 = SEEDS[0]
    for src in ("krea2", "moss"):
        for s in CUT_SUBJECTS:
            white = OUT / src / f"{s}__words_s{s0}.png"
            if white.exists():
                key_out(Image.open(white), None, 40).save(CUT / f"{s}__white_{src}.png")
            mag = OUT / src / f"{s}__magenta_s{s0}.png"
            if mag.exists():
                key_out(Image.open(mag), None, 90, enclosed=True).save(CUT / f"{s}__magenta_{src}.png")
            mask = CUT / f"{s}__mask_{src}_s{s0}.png"
            if white.exists() and mask.exists():
                rgba = Image.open(white).convert("RGBA")
                m = Image.open(mask).convert("L").resize(rgba.size)
                if sum(m.getpixel(p) for p in ((0, 0), (m.width - 1, 0), (0, m.height - 1))) > 3 * 128:
                    from PIL import ImageOps
                    m = ImageOps.invert(m)  # the mask came back as background = white
                rgba.putalpha(m)
                rgba.save(CUT / f"{s}__birefnet_{src}.png")


def _font(size: int, bold: bool = False):
    from PIL import ImageFont
    return ImageFont.truetype("arialbd.ttf" if bold else "arial.ttf", size)


def _tile(path, w: int, h: int, bg, note: str = ""):
    from PIL import Image, ImageDraw
    t = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(t)
    if path is None or not Path(path).exists():
        text = note or "MISSING"
        d.text((w // 2, h // 2), text, fill=(150, 140, 120) if note else (200, 40, 40), font=_font(20, True), anchor="mm")
        return t
    im = Image.open(path).convert("RGBA")
    im.thumbnail((w - 12, h - 12), Image.LANCZOS)
    t.paste(im, ((w - im.width) // 2, (h - im.height) // 2), im)
    return t


def _grid(title: str, headers: list[str], rows: list[tuple[str, list, int]], cell_w: int, out_name: str) -> Path:
    """rows: (label, [(path | None, tile colour, note)], row height). Saves PNG in _sheets and webp in art/tests/sheets."""
    from PIL import Image, ImageDraw
    label_w, head_h, title_h = 250, 56, 50
    W = label_w + cell_w * len(headers)
    H = title_h + head_h + sum(r[2] for r in rows)
    sheet = Image.new("RGB", (W, H), (250, 248, 243))
    d = ImageDraw.Draw(sheet)
    d.text((12, title_h // 2), title, fill=(30, 30, 30), font=_font(26, True), anchor="lm")
    for i, h in enumerate(headers):
        d.multiline_text((label_w + i * cell_w + cell_w // 2, title_h + head_h // 2), h, fill=(30, 30, 30),
                         font=_font(17, True), anchor="mm", align="center")
    y = title_h + head_h
    for label, cells, rh in rows:
        d.multiline_text((10, y + rh // 2), label, fill=(30, 30, 30), font=_font(17), anchor="lm")
        for i, (path, bg, note) in enumerate(cells):
            sheet.paste(_tile(path, cell_w - 6, rh - 6, bg, note), (label_w + i * cell_w + 3, y + 3))
        y += rh
    SHEETS.mkdir(parents=True, exist_ok=True)
    png = SHEETS / f"{out_name}.png"
    sheet.save(png)
    webp_dir = ROOT / "art" / "tests" / "sheets"
    webp_dir.mkdir(parents=True, exist_ok=True)
    sheet.save(webp_dir / f"{out_name}.webp", quality=88)
    print(f"{png}  {W}x{H}")
    return png


def sheets() -> None:
    make_cutouts()
    S = trial_set()
    s0, s1 = SEEDS
    tile_of = lambda d: DARK if d["kind"] == "fx" else PAPER  # noqa: E731
    headers = ["In the game\ntoday", f"Qwen 2.1\n{s0}", f"Qwen 2.1\n{s1}", f"Krea 2 words\n{s0}", f"Krea 2 words\n{s1}",
               f"Krea 2 style ref\n{s0}", f"Krea 2 style ref\n{s1}", f"Moss words\n{s0}", f"Moss words\n{s1}"]
    rows = []
    for s, d in S.items():
        bg = tile_of(d)
        qwen = d["qwen"] if "prompt" not in d["qwen"] else {x: OUT / "qwen21" / f"{s}__style_s{x}.png" for x in SEEDS}
        cells = [(d["today"], bg, "" if d["today"] else "not in game")]
        cells += [(qwen[x], bg, "") for x in SEEDS]
        cells += [(OUT / "krea2" / f"{s}__{v}_s{x}.png", bg, "") for v in ("words", "style") for x in SEEDS]
        cells += [(OUT / "moss" / f"{s}__words_s{x}.png", bg, "") for x in SEEDS]
        label = f"{s}\n\nKrea style ref:\n{Path(d['ref']).name}"
        rows.append((label, cells, 250 if d["kind"] == "bg" else 380))
    _grid("Image model trial: the test set (Qwen tries from the asset batch; bg row made new)", headers, rows, 380, "models_main")

    rows = [(f"{s}\n\nsource:\n{d['edit'][0].name}",
             [(d["edit"][0], PAPER, "")] + [(d["qwen"][x], PAPER, "") for x in SEEDS]
             + [(OUT / "moss" / f"{s}__edit_s{x}.png", PAPER, "") for x in SEEDS], 520)
            for s, d in S.items() if d.get("edit")]
    _grid("Image model trial: edits (the character must stay the same; Krea 2 cannot edit)",
          ["Source sprite", f"Qwen edit {s0}", f"Qwen edit {s1}", f"Moss edit {s0}", f"Moss edit {s1}"], rows, 400, "models_edits")

    rows = []
    for src in ("krea2", "moss"):
        for s in CUT_SUBJECTS:
            cells = [(OUT / src / f"{s}__words_s{s0}.png", PAPER, "")]
            for route in ("white", "magenta_raw", "magenta", "birefnet", "qwen"):
                path = {"white": CUT / f"{s}__white_{src}.png", "magenta": CUT / f"{s}__magenta_{src}.png",
                        "birefnet": CUT / f"{s}__birefnet_{src}.png", "qwen": OUT / "qwen21" / f"{s}__cut_{src}_s{s0}.png",
                        "magenta_raw": OUT / src / f"{s}__magenta_s{s0}.png"}[route]
                cells += [(path, PAPER, "")] if route == "magenta_raw" else [(path, DARK, ""), (path, LIGHT, "")]
            rows.append((f"{s}\n{src} {s0}", cells, 330))
    _grid("Image model trial: cut-out routes for white-background pictures (dark and light tile show edge halos)",
          ["Source\n(white bg)", "Key out white\ndark", "Key out white\nlight", "Asked for\nmagenta bg",
           "Key out magenta\ndark", "Key out magenta\nlight", "BiRefNet (built in)\ndark", "BiRefNet (built in)\nlight",
           "Qwen edit\ndark", "Qwen edit\nlight"], rows, 330, "models_cutout")

    rows = [(m, [(OUT / m / f"blood_boar__words_s{x}.png", PAPER, "") for x in SEEDS], 480) for m in ("qwen21", "krea2", "moss")]
    _grid("Image model trial: blood test (same words on all three)", [f"seed {s0}", f"seed {s1}"], rows, 480, "models_blood")


def main() -> None:
    args = sys.argv[1:]
    if args and args[0] == "trial":
        trial("--dry" in args)
        return
    if args and args[0] == "sheets":
        sheets()
        return
    if args and args[0] == "stats":
        stats()
        return
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
