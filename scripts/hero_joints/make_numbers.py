"""numbers.md: Qwen runs, PC minutes, hand-set numbers, the check numbers."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import rig
import swing
from jcommon import REDO, TESTS

runs = [json.loads(l) for l in (REDO / "runs.jsonl").read_text().splitlines() if l.strip()]
P = json.loads((REDO / "parts.json").read_text())
E = json.loads((TESTS / "_elbow_numbers.json").read_text())
S = json.loads((TESTS / "swing_numbers.json").read_text())
render_min = float(sys.argv[1]) if len(sys.argv) > 1 else 0.0
L = []
L.append("# Jointed hero step 1: numbers\n")
L.append(f"## Qwen\n- runs: {len(runs)} (cap 30), {sum(r['seconds'] for r in runs) / 60:.1f} PC minutes on ComfyUI")
for r in runs:
    L.append(f"  - {r['name']}_s{r['seed']}: {r['seconds']} s")
L.append(f"- rendering the three clips (Python, CPU): about {render_min:.0f} minutes\n")
L.append("## Picked drawing: cutj3_s123 (prompt `cutj3` in qwen_joints.py)\n")
L.append("## Hand-set numbers (drawing px, cutj3_s123 canvas 1536)")
for k in ("S", "a_src", "b_src", "UE", "L", "sleeve_w", "u_cuff_end", "v_inner", "far_shoulder", "neck", "waist",
          "collar_line", "torso_left", "far_poly", "belt_top", "torso_bottom", "hips_top"):
    L.append(f"- {k}: {P[k]}")
L.append(f"- elbow at u = {rig.UE:.1f} of {rig.L:.1f} (0.52); mesh half width {rig.HALF:.0f}; kind B radius {rig.R_B:.1f} "
         f"(0.6 sleeve width); kind C band {rig.BAND_C:.1f} (half the arm)")
L.append(f"- stretch (B, C only, mid sleeve u {rig.STRETCH_LO:.0f}..{rig.STRETCH_HI:.0f}): {swing.STRETCH}")
L.append(f"- rim {rig.RIM_W} px, colour {list((rig.RIM_RGB * 255).round().astype(int))}; arm shadow on body offset "
         f"{rig.SHADOW_OFF}, darkness {rig.SHADOW_DARK}; fist scale {rig.FIST_SCALE}, sword scale {rig.SWORD_SCALE}")
L.append("- fists (A3, drawn grip turn): ready 44.5, wind 96.8, strike 21.5; swaps at turn 70.65 and 33.0; "
         "the gate: turn 20..120 and within 28 of the fist's drawn turn")
L.append("- timeline keys (swing frame: a1, bend, turn, lean, head, far arm, shoulder dx, dy):")
for k in swing.K:
    L.append(f"  - {k}")
L.append("\n## Check numbers")
L.append(f"- elbow widths (alpha across the centre line, against the straight sleeve {E['straight_w']:.0f} px):")
for k, v in E.items():
    if k != "straight_w":
        L.append(f"  - {k}: {v}")
for k in "ABC":
    if k in S:
        sm = S[k]["summary"]
        fr = S[k]["frames"]
        L.append(f"- kind {k}: gate fails {sum(not r['gate'] for r in fr)} of {len(fr)}, flipped triangles "
                 f"{sum(r['flipped'] for r in fr)}, max wrist step {sm['max_wrist_step']:.0f} px/frame, max elbow step "
                 f"{sm['max_elbow_step']:.0f}, elbow steps through the cut {sm['elbow_steps_cut'][:12]}")
(TESTS / "numbers.md").write_text("\n".join(L) + "\n")
print("\n".join(L[:6]))
