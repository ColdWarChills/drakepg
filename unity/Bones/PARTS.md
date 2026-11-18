# Part pictures: how to add and check them

Everything here is run with `python scripts/bones_parts.py <command>` from the repo root (Python 3.12, PIL, numpy).
Pictures sit on the 1536 canvas of the rig picture: a part's `x, y` is the top-left of its picture on that canvas,
and its joints are canvas points in the rest layout (the hero 3/4 front, facing right; `hero/rig.json` `joints`).

`hero/swaps.json` lists what this adds on top of `rig.json` (rig.json itself is never edited here):

- `sets`: a recut set of parts, e.g. `bare_v2` (only the parts that changed; the rest come from `rig.json`).
- `radius`: joint radius overrides (none needed so far).
- `swaps`: a picture that replaces one part for some moments. It carries its own joints: the children hang from
  these, not from the rig's (`part`, `file`, `x`, `y`, `joints`, optional `grip` for a hand, `note`).
- `faces`: faces laid over the head (z 61, turning with the head), like `fight.json`'s shout and hurt.
- `cost`: how many gear pictures each new picture asks for (counted, not drawn).

## Add or fix a part (the cut rules)
`bare_v2` is cut from the rig picture by rules, so the same rules cut any gear picture of the rig pose:
- **Round caps** (`CAPS`): an upper arm or thigh owns a round cap round its shoulder or hip; what is behind the joint
  line and outside the cap goes to the parent. The parent keeps the child's pixels only inside `keep` px. Use a
  small `keep` behind a far limb so no sleeve stub stands up when the limb turns away. A shoulder cap paints the
  torso's skin it takes (the neck, the collar line) over with the shirt next to it, so a raised arm shows no skin
  fleck (`cap_cloth`).
- **Hips without the belt**: the torso brings the belt. Under it the hips get trousers carried up from just below,
  rounded about the waist and kept inside the trouser sides carried on up along their slant, so no square corner
  stands out past the belt end when the torso tilts (`hips_fill`).
- **Torso neck and collar**: a round neck under the jaw and the collar's back, so a turning head opens no notch.

Commands:
- `trycut [gear id] [--close]`: recut into `art/redo/bones_tries/` and show it beside the kept cut (a gear id cuts
  that gear's rig picture). Look at the close-ups before keeping anything.
- `writeset [--force]`: write `hero/bare_v2/` and its `sets` entry. It refuses if the folder exists; `--force`
  only before it is committed.
- Gear recut: the hips rule means the legs-slot sets need the same recut. The torso, shoulder and neck rules mean
  the chest sets need it too. The head is unchanged, so every helm still fits.

## Add a swap picture (a torso turned, a hand, ...)
1. Make the picture where its pose already exists: edit a drawn stance (`qwen <name> <seeds>`, prompts in `QWEN`)
   with the arms moved out of the body so the torso is whole. Tries go to `art/redo/bones_tries/qwen/`.
2. Bring it into the rest layout: `align()` turns it by the stance's `lean` about its `pivot`
   (`fight.json` moments) and moves that pivot to the rig's waist, so the belt lies level on the hips.
3. Measure its joints on the aligned picture (`measure_sheet()`, a 2x crop with a 20 px grid), then put them in
   `TORSO_SRC`, with the sleeve seams and the belt's lower edge.
4. `torso_cut()` keeps the shirt only. It drops what is above the neck line and not shirt-coloured (skin, hair,
   headband), drops the arms past the seams (keeping the near shoulder ball), cuts at the belt's lower edge, and
   drops the dark-green trousers under the belt (the hips bring them). It also drops near-white rim-light flecks
   (and cream rim light on the outline in the belt's last rows), keeps only the piece joined to the belt, and adds
   the round neck disc. About 30 s per torso.
5. `writeswaps [--force]` writes `hero/bare/<name>.png`, the grit face, and swaps.json's `swaps`, `faces` and `cost`.
   New names only; `--force` only for this run's own uncommitted files. Pictures are written atomically and only
   when their pixels change, so a rerun leaves the others byte-identical. The grit face gets `FACE_FIX` after its
   cut (its far eye: the hole filled from the edit, the grey smear under it painted as cheek).
6. Look at the proof sheets (below). Keep a swap only if it clearly beats the base part in the poses.

## The sword hand (grip hands)
The near hand that holds the sword in line with the forearm (`n_hand_grip`, swaps.json): the back of the fist, the
forearm going into it, the blade out of the curled fingers. It is a grip hand: `relation` (forearm direction minus
blade direction it is drawn for, deg), its own `grip` and `n_wrist`, and `weaponZ` 69 (the handle under the forearm
and fist). The old fist (`bare/n_hand.png`, relation 112.4) stays for the cocked poses; `BoneHero.NewHands` picks.
1. `handqwen closed <seeds>`: Qwen edit of the drawn strike stance's near hand (`HAND_SRC`, `HAND_BOX`, x3 on white):
   the green rod removed, the fist kept closed. Tries in `art/redo/hand/qwen/`.
2. `handcut <seed> [kind]`: keeps the hand beyond the wrist line plus a round wrist stub, scales it so the wrist is
   44 px wide (the rig forearm), puts its wrist on the rig's `n_wrist`, and sets the grip `HAND_GUARD` (44 px) behind
   the front of the fingers along the rod. Writes `art/redo/hand/cut/n_hand_grip_s<seed>.png` and prints the entry;
   copy the picture to `hero/bare/` under a new name and the entry into swaps.json.
3. Kept: seed 8102 (one picture). Glove versions: the same edit from `art/redo/layers/<glove>_strike_s6300.png`, cut
   with the same numbers. 9 gloves today (`cost.n_hand_grip`). Not made yet.

Second pass (what is used now, zones; see HOWTO "Grip hands"): two pictures.
- `n_hand_hammer`: the hammer hold, the back of the fist with the handle crossing it (relation 121.7, zone 33..240,
  weaponZ 71 as the old fist). `handqwen hammer --src wind 8200` (the drawn wind-up stance's near fist, `HAND_SRCS`,
  rod removed), then `handcut2 8200 hammer_wind n_hand_hammer`: the grip is set from the guard (the fist's front edge
  on the blade side along the rod, `HAND_GUARD` back in), and the wrist from the grip by the old fist's offset
  (`OLD_FIST_OFFSET`), so the arm is exactly the old one wherever it shows (the drawn wrist is 12.6 px away, inside the
  forearm). The ready-stance source (`--src ready`, seeds 8210-8211) gave a side-on fist; not used.
- `n_hand_grip2`: `n_hand_grip` recut (`handcut2 8102 closed n_hand_grip2`, same numbers) without the pale rim light
  (`rim_off`: edge pixels brighter than the skin's median take the median; the seam at ready was drawn rim light, not
  alpha fringe). Zone -120..24, weaponZ 69, and `weaponZBent` [25, 71]: past 25 deg off its relation (the hits) the
  sword draws over the forearm, so the handle runs on from the fist to the pommel instead of the pommel showing loose.
- `handcut2` writes new names only and prints the swaps.json entry (add `zone`, `weaponZ`, `note` by hand).
- Cost: 2 pictures per glove piece (hammer + in-line), 18 for the 9 gloves (`cost` in swaps.json). The first pass's
  `n_hand_grip` stays on disk but is no longer picked (no zone).

## Gear versions of a swap
Never drawn by hand. A gear version is the same edit made from the gear's own rig picture, or from its drawn
stance. It is cut with the same `TORSO_SRC` joints and seams, so it hangs from the same points. (Untested so far: no
gear version has been made. Check that the edit kept the body where the bare one is.)
Count before making any: a torso swap needs one picture per body armor with an outfit (11 today). A face needs one
cut per helm (9), masked from the one edit, as shout and hurt were. `cost` in swaps.json holds the counts.

## Checks
- `check`: swaps.json loads, every file exists, and each swap composes in rest, sword wind-up and sword strike in
  one piece.
- `sheets1`: joints proof (wiggle every joint, close-ups, test poses), kept parts beside `bare_v2`.
- `sheets234`: the swap torsos alone and in poses, their joint close-ups, the grip clock and the faces.
- `wiggle [--set X] [--try]`: the wiggle test for any set.
Sheets go to `art/tests/sheets/bones/parts/` (webp, at most 2000 px wide).
