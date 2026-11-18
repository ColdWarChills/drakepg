# The hero on bones: how to use the core

The core (`unity/Assets/Scripts/Game/Bones/`, namespace `Drakepg.Bones`, editor only for now) plays the hero from
data files in this folder. Nothing in the game calls it yet; the fight stage (`Drakepg.Test.BoneStage`) is where moves
are judged. All numbers are rig canvas px (y down) and degrees clockwise on screen (0 = forward, 90 = down).

## Files

| File | Who writes it | What it holds |
|---|---|---|
| `hero/rig.json` | rig work | joints, parts (parent, joint, z), picture sets (bare, gear), slots, weapons and grips, weapon z, far-front order |
| `hero/fight.json` | rig work | faces (`shout`, ... and `<face>_<helm>`), other fight pictures |
| `hero/swaps.json` | picture work | extra sets (`sets.bare_v2`), swap pictures (`swaps.<name>`: part, file, x, y, own joints, a hand's own grip), extra faces. Optional; a missing or half-written file only warns |
| `hero/moves/<name>.json` | move work | one move each (below) |

Pictures are read through the source (`BoneDiskSource` in the test: the files on disk). A redrawn picture shows on the
next look (cached by full path and file time); an edited move plays on the next `Play` without recompiling.

## Parts, sets and gear

- A part's picture comes from the first set that has it: a worn gear slot (boots > legs > chest > helm), then the base
  sets (`hero.BaseSets`, e.g. `"bare_v2"`), then `bare`.
- `hero.Wear("chest", "chest_scale")` (slot `chest`/`body`, `legs`, `helm`, `boots`, as in rig.json `slots`; gear =
  a set name in rig.json `sets` such as `legs_plate`, `ridgeplate_helm`, or null to take it off). `hero.Hold("iron_sword", "rusty_dagger")` = near and far hand weapons (null = empty hand).
- A swap picture replaces one part for a while (`torso:<swap>`, `hand:<swap>` events). Its own joints win over the rig's;
  a swap hand's own grip wins too. A move can bring a stand-in in its own `"swaps"` (same fields as swaps.json), for
  testing before the real picture exists; mark it `STAND-IN` in its note.

## A move file

```json
{
  "name": "slash", "note": "what it is", "weapon": "sword", "kind": "attack",
  "weapons": { "n": "iron_sword" },          // optional: weapons held for this move (else the hero's own)
  "reach": 170,                               // stop this far before the target (travel)
  "arc": { "n": true, "f": false },           // hand targets swing round the shoulder (true) or go straight
  "lag": { "torso": 40, "head": 120, "n": 80, "n.blade": 110 },  // ms a channel trails its keys
  "orders": { "lunge": { "f_upper": 76 } },   // named draw orders for order:<name>
  "keys": [
    { "t": 0, "name": "ready", "ease": "SineInOut", "n.hand": [70, -40], "n.blade": -45 },
    { "t": 300, "name": "wind", "ease": "QuadOut", "n.hand": [-120, -220], "n.sweep": -1, "n.blade": -155 },
    { "t": 300, "hold": 60, "n.blade": -155 }  // hold: keep the value 60 ms
  ],
  "events": [ [370, "trail:on"], [440, "contact"], [440, "freeze:60"], [440, "kick:8,1"], [560, "trail:off"] ]
  // to step in: key "travel": 0 at the start, 1 at the blow (the hero stands `reach` px before the target), 0 again
}
```

A key names only the channels it changes; each channel is eased from its previous key with the ease of the key it
arrives at. Unknown fields, channels, eases or events give a warning naming the file and key (`BoneStage.Play` returns
them). The move's length = its last key, hold or event, plus the largest lag.

### Channels

| Channel | Value | What |
|---|---|---|
| `root` | [x, y] | the body (waist) from its rest spot |
| `travel` | 0..1 | 0 = home, 1 = at the target spot (target given at play time, minus `reach`) |
| `rootTurn` | deg | the whole puppet's tilt |
| `squash` | [sx, sy] | the whole puppet's squash (1 = none) |
| `facing` | 1 / -1 | turn round |
| `hips`, `torso`, `head` | deg | turns (absolute on screen, like rig.json) |
| `scale.<part>` | [sx, sy] | one part's scale (its children follow) |
| `slide.<joint>` | [x, y] | move a joint off its part (shoulders on a squeezed torso) |
| `shade.<part>` | -1..1 | 0.3 = 30% darker (turned away); below 0 lightens |
| `n.hand`, `f.hand` | [x, y] | the hand (with a blade: the grip) target, from the waist |
| `n.sweep`, `f.sweep` | 1 / -1 / 0 | which way round the shoulder this key's hand target is reached (key only) |
| `n.aim`, `n.reach` | deg, px | the same target as angle and distance from the shoulder (what `hand` compiles to) |
| `n.ik` | 0..1 | 1 = arm by target, 0 = by angles (`n.upper`, `n.fore`) |
| `n.bend` | -1..1 | elbow bend side; passing 0 bends smoothly, never snaps |
| `n.blade` | deg | the weapon's angle on screen (the hand turns to hold it) |
| `n.wrist` | deg | the hand's own turn when no blade is keyed |
| `n.inhand` | deg | the weapon turned in the hand (180 = reverse grip) |
| `f.on`, `f.onT` | 0..1, px | the far hand on the near weapon's shaft, at `onT` px along it (two hands; slide it) |
| `n.foot`, `f.foot` | [x, y] | the planted foot from its rest spot |
| `plant` | 0..1 | 1 = feet stay on the ground when the body moves; 0 = feet go with the body (a leap) |
| `n.knee`, `f.knee` | -1..1 | knee bend side |
| `n.toe`, `f.toe` | deg | foot turn |

`n.` is the near side, `f.` the far side; every `n.` channel has an `f.` twin.

### Events

`[ms, "name:arg"]`, sent through the caller's handler (`IBoneEvents`; the game's tools in `BoneGameEvents`):

| Event | Does |
|---|---|
| `contact` | the hit lands (the stage plays the game's Impact on the monster) |
| `face:<name>` / `face:none` | face picture (fight.json faces) |
| `torso:<swap>` / `torso:none` | swap the torso picture |
| `hand:<swap>` (its part says which hand), `hand:n_hand=<swap>`, `hand:f_hand=none`, `hand:none` | swap a hand picture |
| `order:<name>` / `order:rest` | draw order (`far_front` from rig.json or the move's `orders`) |
| `weapon:n=<id>`, `weapon:f=<id>`, `=none` | change a held weapon mid-move |
| `trail:on` / `trail:off` | the blade trail (a fan between the tip and mid-blade, 110 ms) |
| `trail:smear` | the blade smear instead: a short tapered crescent near the tip (60 ms, older edges shrink to the tip); for the 2-3 fastest frames of a cut, then `trail:off` |
| `ghost:<alpha>,<ms>[,<at>]` | a pale white copy of the hero fading over `ms` (Anim clock), just behind it; `at` = the move time whose pose it shows (default: now). The pose a key leaves (`at` = the earlier key) or afterimages on a lunge (3 in a row, alpha 0.35/0.28/0.21, 220 ms), like the drawn blow's |
| `lines` | speed lines behind the hero where it stands now (`Fx.SpeedLines`) |
| `sound:<name>` | `Sfx.Play` |
| `freeze:<ms>` | `Fx.HitStop` |
| `shake:<ms>,<px>` | `GameRoot.Shake` |
| `kick:<dx>,<dy>` | `GameRoot.Kick` (dx follows facing) |
| `zoom:<x>,<ms>` | `GameRoot.View`, kept inside the picture |
| `dim:<alpha>,<ms>` | the caller dims the backdrop |
| `mark:<name>` | nothing; a name for the recorder (panels) |
| `grip:<swap>` / `grip:auto` | NewHands with zones: the near grip hand changes to `<swap>` here (see Grip hands) |

### Grip hands (the near sword hand)

`hero.NewHands = true` lets the near hand pick its picture by the wrist bend (the relation: forearm direction minus
blade direction) while it holds a blade and the move names no `hand:` event (an event always wins). The hand is pinned
on the grip, so the grip and the tip stay exactly where the move puts them; the arm is re-aimed at the hand's wrist
(three passes), and the elbow may fold at most `hero.FoldCap` deg (30) more than the move's own. Only the near hand;
the far hand is unchanged.

Zones (second pass, what the kept moves use): when any grip swap has a `zone` [from, to] (deg, going up, may wrap),
only zoned hands are picked, never the rig's fist. Today: `n_hand_hammer` (33..240: ready, wind-up, overhead, back,
rest) and `n_hand_grip2` (-120..24: the swing, the hits, the leap's charge). Between two zones the hand shown stays
(hysteresis), so a pose that hovers near an edge never flickers. The switches are found once per move (`BuildGripPlan`:
the move walked every 5 ms on the old fist's angles, so stills and play agree; `hero.GripPlan` lists them), and the arm
blends between the two hands' wrists `gripBlendMs` (swaps.json, 20) each side of a switch. A move can place a switch
itself with `grip:<swap>` (`grip:auto` clears it): the hand changes there and the zones take over again when the angle
next enters another hand's zone. Put it where the arm starts moving (sword_cut: `[480, "grip:n_hand_hammer"]`, the end
of the hit hold, so the back pose keeps the old arm). Without zones the first pass's rule applies (nearest relation,
blended over `gripBand`). Rest (no move) picks by zone too.

Choosing a hand picture: its wrist-to-grip offset decides where the arm goes, not its look. A hand whose offset equals
the old fist's (40.2 px at 107.9 deg from the blade, `OLD_FIST_OFFSET` in bones_parts.py) leaves the arm exactly where
it was, so give that offset to the picture shown in the held poses (`handcut2 ... derive`). A hand with its own offset
(the in-line grip: 48.5 px along the forearm) moves the arm 12-17 px at 1280; keep it to the moving parts.

## Code

```csharp
var hero = BoneHero.Create(parent, source, x, y, scale);   // source: an IBoneSource
hero.Hold("iron_sword", null); hero.Wear("chest", "chest_scale");
var move = BoneMove.Parse(json, "moves/slash.json", hero.Rig);
hero.Sample(move, 440);                                    // the pose at 440 ms, still
await hero.Play(move, target, events);                     // on the Anim clock (battle speed applies)
```

Named points for the recorder: child objects `n_hand_grip`/`f_hand_grip` (the hand picture's grip point),
`near weapon`/`far weapon` (the weapon's grip), each part by its own name (`j_<joint>` = its joint).

## The stage (play mode, through the MCP's execute_code)

```csharp
Drakepg.Test.BoneStage.Open("iron_sword", "bog_lurker", "fen");  // wait until BoneStage.Ready; parts: bare_v2 (the default)
// a 4th argument picks the base part set: Open(..., "fen", "bare") for the old bare parts (BoneStage.DefaultBaseSet)
Drakepg.Test.BoneStage.Play("sword_swing_old");                  // returns the length and warnings
Drakepg.Test.BoneStage.Hold("sword_swing_old", 440);             // a still pose
Drakepg.Test.BoneStage.Stills("sword_leap@560=charge sword_leap@860=top sword_leap@1030=hit sword_chain@540", DIR);  // still poses as frames
// (f000.jpg..., track.json with each label as its event; wait until BoneStage.Busy is false): key poses before a take
Drakepg.Test.BoneStage.Hands(true, 30);                          // grip hands on (fold cap 30); Hands(false) = the old fist
Drakepg.Test.BoneStage.Close("sword_cut@300=hit ...", DIR, 12, 480);  // the same poses zoomed 12x on the near grip
// (f000.jpg..., labels.txt with the hand shown, the relation and the wrist gap; wait until Busy is false)
Drakepg.Test.BoneStage.Path2("sword_cut", 5);                    // TSV every 5 ms: grip, tip, hand, elbow, hand picture, relation, gap
Drakepg.Test.BoneStage.Speed(2);                                 // battle speed x2 (stage only)
Drakepg.Test.BoneStage.Close();                                  // back to the title screen
```

Record and judge (see `scripts/bones_sheets.py` help):

```csharp
var h = Drakepg.Test.BoneStage.Hero.gameObject;
Drakepg.EditorTools.BonesCapture.Reset(); Drakepg.EditorTools.BonesCapture.BoneCore(h);
Drakepg.Test.BoneStage.Play("slash");
Drakepg.EditorTools.BonesCapture.Take(DIR + "/main", 96, 60, 1280);
// wait until BonesCapture.Busy is false, then the same with .Clean(DIR + "/clean", h, 96, 60, 1280)
```

```
python scripts/bones_sheets.py all DIR/main --clean DIR/clean --rig unity/Bones/hero/rig.json --boxes hero --out art/tests/sheets/bones/<name>
python scripts/bones_sheets.py panels "DIR/main|@mark:leap|caption" ... --out x.webp
```

The checks (`check.txt`): (a) the hero in one piece, (b) every joint covered, (c) the weapon in the hand.
The old swing beside the core: `BoneStage.PortKeys(dir)` (frames + `diff.txt`, joints compared at the old keys).
