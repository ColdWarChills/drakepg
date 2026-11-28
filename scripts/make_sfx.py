"""Make battle sound effects with MOSS-SoundEffect v2.0 (open text-to-audio model, Apache-2.0).

Run with the model's own Python (everything lives in C:\\Users\\Epic\\moss-sfx\\, delete that folder to remove it):
    C:\\Users\\Epic\\moss-sfx\\.venv\\Scripts\\python.exe scripts\\make_sfx.py                  # every sound in sfx_sounds.json
    C:\\Users\\Epic\\moss-sfx\\.venv\\Scripts\\python.exe scripts\\make_sfx.py --only hit      # redo one sound
    ... --takes 5 --out .sim\\sfx_try                                                        # more takes, other folder
    ... --page-only                                                                           # just rebuild listen.html
    ... --install slash:1 [hit:2 ...]    # copy <out>\\slash_1.wav to unity\\Assets\\Resources\\Sfx\\<game name>.wav (the game plays it instead of the synth)
    ... --install crit_blade:1 crit_ring:3  # several picks for one game name: crit.wav, crit_2.wav ... in the order given
                                         # (the game plays one of them at random); that name's older extra takes are removed
    ... --uninstall slash [hit ...]      # remove every take of that game name and their .meta (and the Sfx folder once it is empty)
--page-only, --install and --uninstall don't load the model. The game name is the row's "game" field, else its "name".
A file that is overwritten keeps its .meta (same GUID and import settings). At most 9 takes per game name (MaxTakes in SfxSynth.cs).

Model:   C:\\Users\\Epic\\moss-sfx\\weights   (Hugging Face OpenMOSS-Team/MOSS-SoundEffect-v2.0, ~11 GB)
Code:    C:\\Users\\Epic\\moss-sfx\\MOSS-TTS\\moss_soundeffect_v2  (github.com/OpenMOSS/MOSS-TTS)
Windows: no Triton, so torch.compile is switched off (TORCHDYNAMO_DISABLE=1, set below before torch loads);
         the models are loaded straight onto the GPU by load_pipeline() (DiT in bf16, ~8 GB VRAM), because
         loading them all on the CPU first ran out of Windows commit space ("paging file is too small").

Each take (seed = take number) is post-processed: mono, leading silence trimmed (starts within 10 ms,
1.5 ms fade-in), tail cut where it has decayed (short fade-out), peaked to -1 dBFS, 48 kHz 16-bit mono WAV.
One line per take: length, peak/RMS dBFS, clipped samples, leading silence, raw model peak (before the gain).
"""
import os
import sys

HOME = os.environ.get("MOSS_SFX_HOME", r"C:\Users\Epic\moss-sfx")
os.environ.setdefault("TORCHDYNAMO_DISABLE", "1")
os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("HF_HUB_DISABLE_IMPLICIT_TOKEN", "1")
os.environ.setdefault("HF_HOME", os.path.join(HOME, "hf-home"))

import argparse
import base64
import html
import json
import re
import shutil
import time
from pathlib import Path

import numpy as np

SR = 48000
REPO = Path(__file__).resolve().parent.parent
TABLE = Path(__file__).resolve().parent / "sfx_sounds.json"
DEFAULT_OUT = REPO / ".sim" / "sfx_2026-11-28"
GAME_SFX = REPO / "unity" / "Assets" / "Resources" / "Sfx"


def game_name(table, name):
    """The game's sound name for a table row, found by its name or its game name."""
    for s in table:
        if name in (s["name"], s.get("game", s["name"])):
            return s.get("game", s["name"])
    sys.exit(f"no sound named {name!r} in {TABLE}")


MAX_TAKES = 9  # MaxTakes in unity/Assets/Scripts/Game/Fx/SfxSynth.cs


def take_file(game, n):
    """The game's file name of a game name's n-th take (1 = <game>.wav, then <game>_2.wav ...)."""
    return f"{game}.wav" if n == 1 else f"{game}_{n}.wav"


def installed_takes(game):
    """Files in Resources/Sfx that belong to a game name: <game>.wav, <game>_N.wav and their .meta (orphans too)."""
    if not GAME_SFX.is_dir():
        return []
    pattern = re.compile(rf"^{re.escape(game)}(_\d+)?\.wav(\.meta)?$")
    return sorted(p for p in GAME_SFX.iterdir() if p.is_file() and pattern.match(p.name))


def install(table, out, picks):
    # Check every pick before touching anything, so a bad one can't leave half an install.
    groups = {}
    for pick in picks:
        name, sep, take = pick.partition(":")
        if not sep or not take.isdigit():
            sys.exit(f"--install wants name:take (like slash:1), got {pick!r}")
        src = out / f"{name}_{take}.wav"
        if not src.exists():
            sys.exit(f"no take {src}")
        groups.setdefault(game_name(table, name), []).append(src)
    for game, srcs in groups.items():
        if len(srcs) > MAX_TAKES:
            sys.exit(f"{len(srcs)} picks for {game}, the game plays at most {MAX_TAKES}")

    GAME_SFX.mkdir(parents=True, exist_ok=True)
    for game, srcs in groups.items():
        keep = {take_file(game, n) for n in range(1, len(srcs) + 1)}
        keep |= {k + ".meta" for k in keep}
        for p in installed_takes(game):
            if p.name not in keep:
                p.unlink()
                print(f"removed the old take {p.name}")
        for n, src in enumerate(srcs, 1):
            dst = GAME_SFX / take_file(game, n)
            shutil.copyfile(src, dst)  # an existing .meta stays as it is
            print(f"installed {src.name} -> {dst}")


def uninstall(table, names):
    for name in names:
        game = game_name(table, name)
        gone = installed_takes(game)
        for p in gone:
            p.unlink()
        print(f"removed {', '.join(p.name for p in gone)}" if gone else f"nothing installed for {game} in {GAME_SFX}")
    if GAME_SFX.is_dir() and not any(GAME_SFX.iterdir()):
        GAME_SFX.rmdir()
        folder_meta = GAME_SFX.with_name(GAME_SFX.name + ".meta")
        if folder_meta.exists():
            folder_meta.unlink()
        print(f"removed the empty folder {GAME_SFX}")


def db(x):
    return 20 * np.log10(max(float(x), 1e-12))


def load_pipeline(model_dir):
    """Low-memory twin of MossSoundEffectPipeline.from_pretrained for a PC with little free commit
    (Windows "paging file is too small" when all three models sat in RAM at once). Each model goes to
    the GPU as soon as it is read; the DiT is filled tensor by tensor on the GPU and kept in bf16
    (it runs under bf16 autocast anyway): ~8 GB of VRAM in all, no CPU offload needed."""
    import torch
    from safetensors import safe_open
    from moss_soundeffect_v2 import MossSoundEffectPipeline
    from moss_soundeffect_v2.diffsynth.pipelines.wan_audio import (
        WanAudioPipeline, WanAudioModel, Qwen3TextEncoder, WanPrompter, DAC, _convert_hf_dit_state_dict)

    d = Path(model_dir)
    index = json.loads((d / "model_index.json").read_text())
    sched = json.loads((d / "scheduler" / "scheduler_config.json").read_text())
    cfg = json.loads((d / "transformer" / "config.json").read_text())
    dev, bf16 = "cuda", torch.bfloat16

    with torch.device(dev):
        dit = WanAudioModel(
            in_dim=cfg["in_dim"], out_dim=cfg["out_dim"], text_dim=cfg["text_dim"], freq_dim=cfg["freq_dim"],
            eps=cfg["eps"], patch_size=tuple(cfg["patch_size"]), has_image_input=cfg["has_image_input"],
            dim=cfg["dim"], ffn_dim=cfg["ffn_dim"], num_heads=cfg["num_heads"], num_layers=cfg["num_layers"],
            vae_type=cfg.get("vae_type", "dac"))
    own = dit.state_dict()
    filled = set()
    with safe_open(str(d / "transformer" / "diffusion_pytorch_model.safetensors"), framework="pt", device="cpu") as f:
        for k in f.keys():
            ((name, t),) = _convert_hf_dit_state_dict({k: f.get_tensor(k)}).items()
            own[name].copy_(t)
            filled.add(name)
    missing = set(own) - filled
    if missing:
        raise RuntimeError(f"DiT weights missing {len(missing)} keys, e.g. {sorted(missing)[:3]}")
    for p in dit.parameters():  # params only: Module.to(dtype) would also squash the complex RoPE buffers
        p.data = p.data.to(bf16)
    dit.eval()
    del own
    torch.cuda.empty_cache()

    text_encoder = Qwen3TextEncoder(str(d / "text_encoder"), torch_dtype=bf16).to(dev)
    prompter = WanPrompter(tokenizer_path=str(d / "tokenizer"))
    prompter.fetch_models(text_encoder)
    vae = DAC.load(str(d / "vae" / "vae_128d_48k.pth")).to(dev).eval()

    engine = WanAudioPipeline(device=dev, torch_dtype=bf16, flow_shift=sched.get("shift", 5.0))
    engine.text_encoder, engine.prompter, engine.vae, engine.dit = text_encoder, prompter, vae, dit
    engine.audio_latent_dim = cfg["in_dim"]
    engine.num_samples_division_factor = vae.hop_length
    engine.dit_variant = index.get("dit_variant")
    return MossSoundEffectPipeline(engine=engine, sample_rate=int(index.get("sample_rate", SR)),
                                   max_inference_seconds=int(index.get("max_inference_seconds", 30)))


def post_process(audio, told_seconds):
    """Raw model output (C, T) float -> (clean mono float32, stats)."""
    x = np.asarray(audio, dtype=np.float64)
    if x.ndim > 1:
        x = x.mean(axis=0)
    x = x - x.mean()
    raw_peak = float(np.abs(x).max()) if x.size else 0.0
    stats = {"raw_peak_db": round(db(raw_peak), 1), "decayed_s": None, "capped": False}
    if raw_peak < 1e-6:
        return np.zeros(int(0.1 * SR), dtype=np.float32), stats

    # Leading silence: start 2 ms before the first sample above -40 dB of the peak.
    above = np.nonzero(np.abs(x) > raw_peak * 10 ** (-40 / 20))[0]
    start = max(0, int(above[0]) - int(0.002 * SR))
    x = x[start:]

    # Tail: last 10 ms frame whose RMS is within 45 dB of the loudest frame.
    hop = int(0.010 * SR)
    n = len(x) // hop
    env = np.sqrt((x[: n * hop].reshape(n, hop) ** 2).mean(axis=1)) if n else np.array([raw_peak])
    loud = np.nonzero(env > env.max() * 10 ** (-45 / 20))[0]
    end = (int(loud[-1]) + 1) * hop if loud.size else len(x)
    cap = int(told_seconds * 1.5 * SR)
    capped = end > cap
    end = max(min(end, cap, len(x)), int(0.05 * SR))
    x = x[:end].copy()
    stats["decayed_s"] = round((int(loud[-1]) + 1) * hop / SR, 3) if loud.size else None
    stats["capped"] = bool(capped)

    # Fades: 1.5 ms in (the cut lands on a non-zero sample), 30 ms out (80 ms if the cap cut it short).
    fi = min(int(0.0015 * SR), len(x))
    x[:fi] *= np.linspace(0.0, 1.0, fi)
    fo = min(int((0.080 if capped else 0.030) * SR), len(x))
    x[-fo:] *= 0.5 * (1 + np.cos(np.linspace(0, np.pi, fo)))

    # Peak to -1 dBFS.
    x = x * (10 ** (-1 / 20) / np.abs(x).max())
    return x.astype(np.float32), stats


def measure(path):
    import soundfile as sf
    y, sr = sf.read(str(path), dtype="int16")
    f = y.astype(np.float64) / 32768.0
    peak = np.abs(f).max()
    lead = np.nonzero(np.abs(f) > peak * 10 ** (-40 / 20))[0]
    return {
        "seconds": round(len(f) / sr, 3),
        "sr": sr,
        "peak_db": round(db(peak), 1),
        "rms_db": round(db(np.sqrt((f ** 2).mean())), 1),
        "clipped": int((np.abs(y.astype(np.int32)) >= 32767).sum()),
        "lead_ms": round(1000 * int(lead[0]) / sr, 1) if lead.size else None,
    }


def build_page(out, sounds):
    rows = []
    count = 0
    for s in sounds:
        cells = []
        for take in range(1, 100):
            wav = out / f"{s['name']}_{take}.wav"
            if not wav.exists():
                break
            data = base64.b64encode(wav.read_bytes()).decode("ascii")
            cells.append(
                f'<button type="button" data-a="{s["name"]}_{take}">{take}</button>'
                f'<audio id="{s["name"]}_{take}" preload="auto" src="data:audio/wav;base64,{data}"></audio>'
            )
            count += 1
        if cells:
            rows.append(f'<div class="row"><span class="name">{html.escape(s.get("label", s["name"]))}</span>{"".join(cells)}</div>')
    page = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Battle sounds</title>
<style>
body {{ background: #f4ecd8; color: #2b241c; font: 18px/1.4 Georgia, serif; margin: 0; padding: 24px 16px; }}
h1 {{ font-size: 24px; font-weight: normal; margin: 0 0 20px; }}
.row {{ display: flex; align-items: center; gap: 10px; padding: 10px 0; border-bottom: 1px solid #d8ccb0; max-width: 520px; }}
.name {{ width: 170px; }}
button {{ font: inherit; width: 48px; height: 40px; background: #fbf6ea; color: #2b241c; border: 1px solid #8a7b62; border-radius: 4px; cursor: pointer; }}
button.on {{ background: #2b241c; color: #fbf6ea; }}
</style></head><body>
<h1>Battle sounds</h1>
{chr(10).join(rows)}
<script>
document.querySelectorAll('button[data-a]').forEach(function (b) {{
  b.addEventListener('click', function () {{
    document.querySelectorAll('audio').forEach(function (a) {{ a.pause(); }});
    document.querySelectorAll('button.on').forEach(function (o) {{ o.classList.remove('on'); }});
    var a = document.getElementById(b.dataset.a);
    a.currentTime = 0; a.play(); b.classList.add('on');
    a.onended = function () {{ b.classList.remove('on'); }};
  }});
}});
</script>
</body></html>
"""
    (out / "listen.html").write_text(page, encoding="utf-8")
    return count


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="make just these sounds (names from sfx_sounds.json, comma separated)")
    ap.add_argument("--takes", type=int, help="takes per sound (default: the table's)")
    ap.add_argument("--out", default=str(DEFAULT_OUT), help="output folder")
    ap.add_argument("--model", default=os.path.join(HOME, "weights"), help="model folder")
    ap.add_argument("--steps", type=int, default=100)
    ap.add_argument("--cfg", type=float, default=4.0)
    ap.add_argument("--extra", type=float, default=1.0, help="seconds generated past the told length (found tail, not a hard crop)")
    ap.add_argument("--page-only", action="store_true", help="only rebuild listen.html")
    ap.add_argument("--install", nargs="+", metavar="NAME:TAKE", help="put takes into the game (Resources/Sfx), no model")
    ap.add_argument("--uninstall", nargs="+", metavar="NAME", help="take installed sounds out of the game, no model")
    args = ap.parse_args()

    table = json.loads(TABLE.read_text(encoding="utf-8"))["sounds"]
    if args.install or args.uninstall:
        if args.uninstall:
            uninstall(table, args.uninstall)
        if args.install:
            install(table, Path(args.out), args.install)
        return
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    only = [n.strip() for n in args.only.split(",")] if args.only else None
    todo = [s for s in table if not only or s["name"] in only]
    if only and len(todo) != len(set(only)):
        sys.exit(f"no sound named {sorted(set(only) - {s['name'] for s in todo})} in {TABLE}")

    if not args.page_only:
        import torch
        import soundfile as sf
        try:
            import moss_soundeffect_v2  # noqa: F401
        except ImportError:
            sys.path.insert(0, os.path.join(HOME, "MOSS-TTS"))

        t0 = time.time()
        pipe = load_pipeline(args.model)
        torch.cuda.reset_peak_memory_stats()
        print(f"model loaded in {time.time() - t0:.0f}s", flush=True)

        stats_path = out / "stats.json"
        all_stats = json.loads(stats_path.read_text()) if stats_path.exists() else {}
        for s in todo:
            told = float(s["seconds"])
            for take in range(1, (args.takes or int(s.get("takes", 3))) + 1):
                t = time.time()
                audio = pipe(
                    prompt=f"{s['prompt'].strip()} duration: {told:.1f}s",
                    seconds=min(told + args.extra, pipe.max_inference_seconds),
                    num_inference_steps=args.steps,
                    cfg_scale=args.cfg,
                    seed=take,
                    append_duration_suffix=False,
                    progress_bar_cmd=lambda it: it,
                )
                gen = time.time() - t
                y, st = post_process(audio[0].float().cpu().numpy(), told)
                path = out / f"{s['name']}_{take}.wav"
                sf.write(str(path), y, SR, subtype="PCM_16")
                st.update(measure(path))
                st["gen_s"] = round(gen, 1)
                st["vram_peak_gb"] = round(torch.cuda.max_memory_allocated() / 2 ** 30, 2)
                all_stats[path.name] = st
                flag = "  LOW?" if st["raw_peak_db"] < -30 else ""
                print(
                    f"{path.name:12s} {st['seconds']:6.3f}s (told {told:.1f}) peak {st['peak_db']:5.1f} dBFS "
                    f"rms {st['rms_db']:6.1f} dBFS clipped {st['clipped']} lead {st['lead_ms']} ms "
                    f"raw peak {st['raw_peak_db']:6.1f} dBFS decayed {st['decayed_s']}s{' CAPPED' if st['capped'] else ''} "
                    f"gen {gen:.1f}s vram {st['vram_peak_gb']} GB{flag}",
                    flush=True,
                )
                stats_path.write_text(json.dumps(all_stats, indent=1))

    n = build_page(out, table)
    print(f"listen.html: {n} takes, {(out / 'listen.html').stat().st_size / 1e6:.1f} MB -> {out / 'listen.html'}")


if __name__ == "__main__":
    main()
