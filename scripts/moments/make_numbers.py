"""art/tests/moments/numbers.md from the run log (art/redo/moments/runs.jsonl) and the checks (checks.json).

<venv python> -I -B make_numbers.py
"""
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO, TESTS  # noqa: E402


def main():
    from datetime import datetime
    runs = [json.loads(l) for l in (REDO / "runs.jsonl").read_text().splitlines() if l.strip()]
    # "sec" is the wait for the result, queue included (two batches shared ComfyUI); the PC time of a run is at most
    # the gap since the run before it finished
    runs.sort(key=lambda r: r["utc"])
    prev = None
    for r in runs:
        t = datetime.fromisoformat(r["utc"]).timestamp()
        if prev is not None:
            r["sec"] = min(r["sec"], t - prev)
        prev = t
    checks = json.loads((REDO / "checks.json").read_text())
    picks = json.loads((REDO / "moments.json").read_text())
    kept = set(picks["raw"].values()) | {picks.get("sleeve_short")}
    for by_m in picks.get("pieces", {}).values():
        kept |= {v.replace("sheet:", "") for v in by_m.values()}

    def group(f):
        f = f[:-4]
        if f.startswith("bare_") or f.startswith("plain_") or f.startswith("sleeve_"):
            return "A: moments (bare poses, sleeve)"
        if "@sheet" in f or f.endswith(("s602", "s612")) or f in ("scale_boots@strike_s601", "scale_gauntlets@strike_s611"):
            return "B: place tests (strike 2 pieces x 2 seeds, one 2x2 sheet)"
        return "C: armor pieces on the four moments"

    groups = {}
    for r in runs:
        g = groups.setdefault(group(r["file"]), {"runs": 0, "sec": 0, "kept": 0})
        g["runs"] += 1
        g["sec"] += r["sec"]
        g["kept"] += r["file"][:-4] in kept
    total = sum(g["sec"] for g in groups.values())
    piece_runs = [r for r in runs if group(r["file"]).startswith("C")]
    pr_kept = sum(r["file"][:-4] in kept for r in piece_runs)
    per = sum(r["sec"] for r in piece_runs) / max(1, len(piece_runs))
    yield_c = pr_kept / max(1, len(piece_runs))
    L = ["# Hero in moments: the numbers", "",
         f"Qwen runs in total: **{len(runs)}**, {total / 60:.0f} minutes of PC time "
         f"(average {total / max(1, len(runs)):.0f} s a run).", "",
         "| part | runs | kept | PC minutes |", "|---|---|---|---|"]
    for k in sorted(groups):
        g = groups[k]
        L.append(f"| {k} | {g['runs']} | {g['kept']} | {g['sec'] / 60:.1f} |")
    L += ["", f"First-try yield of the armor edits (kept / runs): {pr_kept}/{len(piece_runs)} = {yield_c:.0%}.", "",
          "## Cost of more", "",
          f"- One more armor piece on these four moments: 4 edits, at this yield {4 / max(yield_c, 1e-6):.1f} runs = "
          f"{4 / max(yield_c, 1e-6) * per / 60:.0f} minutes of PC time, then `build.py` (3-5 minutes for all pieces). "
          f"One look at the four edits (does it match its ready design?) and at the sheets.",
          f"- One more attack (four new moments, then all ten pieces on them): moments took "
          f"{groups.get('A: moments (bare poses, sleeve)', {}).get('runs', 0)} runs here for 4 kept (gate 1 is a human "
          f"look), then 40 piece edits / yield = {40 / max(yield_c, 1e-6):.0f} runs; about "
          f"{(8 + 40 / max(yield_c, 1e-6)) * per / 60:.0f} minutes of PC time if the poses come out in two seeds each. "
          f"Plus checking the cut on the new poses: here the strike pose (tails flying, open mouth) broke the head "
          f"finding and needed three rule fixes; a new attack should be checked with the mask overlays before building.",
          "", "## Place check per kept edit", "",
          "Unchanged zones of the bare moment, after one shift for the whole picture (median of the zones' shifts). "
          "overlap = silhouette IoU inside the zone's box; moved = phase-correlation shift of the zone's drawing in px "
          "(canvas px, hero 1000 px tall). The zone the piece changes is expected to move.", "",
          "| edit | whole-picture shift | head | arms+fists | shirt | trousers | boots | leak outside slot | layer vs edit |",
          "|---|---|---|---|---|---|---|---|---|"]
    for k, c in sorted(checks.items()):
        pc = c["place"]
        z = lambda n: f"{pc[n][0]:.2f} / {pc[n][1]:.1f}" if n in pc else "-"  # noqa: E731
        L.append(f"| {k} ({c['edit']}) | {c['align']['dx']:+.1f}, {c['align']['dy']:+.1f} | "
                 f"{z('head (face, hair, band)')} | {z('skin off the head (arms, fists)')} | {z('shirt')} | "
                 f"{z('trousers')} | {z('boots')} | {c['leak_outside_slot']:.1%} | "
                 f"mean {c['equals_edit']['mean_abs']}, >60: {c['equals_edit']['share_over_60']:.1%} |")
    (TESTS / "numbers.md").write_text("\n".join(L) + "\n")
    print("\n".join(L[:12]))


if __name__ == "__main__":
    main()
