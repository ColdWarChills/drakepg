"""Round 2 numbers -> art/tests/hero_joints/r2/numbers.md.
<venv python> -I -B make_numbers_r2.py <render minutes>"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))  # noqa: E401,E702
import json

import rig_r2 as rig
import swing_r2 as sw
from jcommon import REDO, TESTS

render_min = sys.argv[1] if len(sys.argv) > 1 else "?"
runs = [json.loads(line) for line in (REDO / "runs_r2.jsonl").read_text().splitlines() if line.strip()]
geo = json.loads((REDO / "parts_r2.json").read_text())
data = json.loads((TESTS / "r2" / "swing_r2.json").read_text())
S, rows = data["summary"], data["frames"]
L = []
L.append("# Jointed hero, round 2: numbers\n")
L.append("## Qwen and PC time")
L.append(f"- runs: {len(runs)} (cap 20), {sum(r['seconds'] for r in runs) / 60:.1f} PC minutes on ComfyUI")
for r in runs:
    L.append(f"  - r2_{r['name']}_s{r['seed']}: {r['seconds']} s")
L.append("- used: r2_vest_teal_s202 (the torso only). Not used: the other three vests (teal s201 is the same; brown "
         "s211/s212 part a little less from the cream sleeve), both fist drawings (s221/s222 moved the figure, "
         "dropped his far arm and drew a heavy sword through the fist; the A3 ready fist was kept)")
L.append(f"- clip render (Python, CPU): about {render_min} minutes; stills and dry runs: a few minutes\n")
L.append("## Hand-set numbers (drawing px, cutj3_s123 canvas; hero 1028 px tall)")
L.append(f"- arm: pivot to wrist L {rig.L} (unchanged: the overlay `r2/_armlen.png` shows the new arm on the old "
         "hero's own arm, same shoulder top and wrist; round 1's 0.84x compared pivot-to-cuff with joint-to-joint), "
         f"elbow at u {rig.UE}")
L.append(f"- sleeve painted by code: {2 * rig.HW0:.0f} px wide at the shoulder to {2 * rig.HW1:.0f} above the cuff; "
         f"cuff {2 * rig.HWC:.0f} wide from u {rig.UC0:.1f} to {rig.UC1:.1f} (bowed {rig.CUFF_CURVE} px); shadow band "
         f"a third of the width on side {rig.SHADE_SIDE:+.0f} (under the forearm at ready, under the arm at strike, "
         "on the back of the upper arm at ready); edge lines x"
         f"{rig.LINE_DARK}; colours cream {(rig.CREAM * 255).round().tolist()} / shadow "
         f"{(rig.CREAM_SH * 255).round().tolist()} (the shirt's own two tones)")
L.append(f"- elbow: kind B, fixed bend radius {rig.R_B}, mesh half width {rig.HALF}; round shoulder radius "
         f"{rig.SHOULDER_R} under the sleeve, turned with the upper arm")
L.append("- fist: the A3 ready fist only (drawn turn 44.5), recoloured to cutj3_s123's skin through 4 cluster pairs, "
         "outline smoothed and cut 1 px in, outer 3 px repainted from inside, a round skin stub r 24 about the wrist "
         "point under the cuff; the cuff's hard shadow on it 6 px deep")
L.append(f"- cast shadow of sleeve, fist and hilt on torso and hips: offset {rig.CAST_OFF}, colour x "
         f"{[round(v, 3) for v in geo['cast_ratio']]} (the vest's own shadow cluster was under 3%, so the trousers' "
         "drawn lit/shadow pair)")
L.append(f"- rim {rig.RIM_W} px, colour {(rig.RIM_RGB * 255).round().tolist()} (round 1's measure)")
L.append(f"- vest torso cut on the belt's own top edge, carried 34 px on under the belt; tails turn about "
         f"{geo['knot']}; edit alignment to s123 (dx, dy): head {[round(v, 2) for v in geo.get('align_head', [0, 0])[:2]]}, "
         f"belt {[round(v, 2) for v in geo.get('align_belt', [0, 0])[:2]]}")
L.append(f"- timeline: {S['frames']} frames at 60 fps (idle {sw.IDLE_A}, swing {sw.SWING_LEN + 1}, idle "
         f"{sw.IDLE_MID}, swing, idle {sw.IDLE_B}); breath period {sw.BREATH_T} frames (2 per clip); stretch "
         f"{sw.STRETCH}; smear bands alpha {sw.SMEAR_ALPHA}, only where the tip moved over {sw.SMEAR_MIN} px")
L.append("- keys (swing frame: a1, b, turn, lean, dy, sq, shoulder sx, sy, far arm angle, fx, fy); the raise (5-26), "
         "the cut (46-51 -> frames 46-54) and the return (87-107) are re-timed along the path so the steps of tip, "
         "wrist and elbow rise and fall:")
for k in sw.K:
    L.append("  - " + ", ".join(f"{v:g}" for v in k))
L.append(f"- cut share of travel per frame: {sw.CUT_W}\n")
L.append("## Check numbers")
L.append(f"- blade tip step: largest inside the cut {S['tip_step_max_in_cut']} px, outside "
         f"{S['tip_step_max_outside_cut']} px (clip frame {S['tip_step_outside_frame']}, the middle of the raise)")
L.append(f"- tip steps through the cut (swing 46-56): {S['tip_steps_cut']}")
cut_rows = [r for r in rows if r['s'] is not None and 46 <= r["s"] <= 56][:11]
L.append("- wrist steps there: " + str([r['wrist_step'] for r in cut_rows]))
L.append("- elbow steps there: " + str([r['elbow_step'] for r in cut_rows]))
L.append(f"- largest wrist / elbow step outside the cut: {S['wrist_step_max_outside_cut']} / "
         f"{S['elbow_step_max_outside_cut']} px")
L.append(f"- grip turn {S['turn_min']}..{S['turn_max']} (one fist, drawn at 44.5: wrist bend "
         f"{S['turn_min'] - 44.5:+.1f}..{S['turn_max'] - 44.5:+.1f}). Holds: wind-up 61-65, strike 20.6-22.5, ready "
         "44-46. Frames under round 1's 20 floor (the overshoot and its settle): "
         + ", ".join(f"clip {f} (swing {s}) {t}" for f, s, t in S['turn_below_20']))
L.append(f"- body: lean {S['lean_range']} deg, head {S['head_range']} deg (counter, half the chest), near shoulder "
         f"moves dx {S['shoulder_dx_range']} / dy {S['shoulder_dy_range']} px (lean + shrug), tails "
         f"{S['tail_range']} deg")
L.append(f"- smear on clip frames {S['smear_frames']}")
L.append(f"- flipped triangles in the sleeve mesh: {S.get('flipped_triangles', '?')}")
(TESTS / "r2" / "numbers.md").write_text("\n".join(L) + "\n")
print("\n".join(L))
