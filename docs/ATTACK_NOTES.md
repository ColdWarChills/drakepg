# Attack notes: a toolbox for the hero on bones

Written 2026-11-17 (overnight run, burst 1: research). For the workers who build the hero on bones and its attacks.
Not one style and not a checklist: ideas and numbers to explore from. Different weapons move differently; try
several takes and keep the strongest.

How to read the numbers:
- Time is given as ms at x1, then frames at 60 fps (1 frame = 16.7 ms). "120 ms / 7f".
- Numbers with a source number "(source N)" come from that source (section 6). Every other number is a starting
  value to tune by eye.
- At x2 every Anim tween runs twice as fast (see 4.2). A pose that must be seen (wind-up peak, contact) must last
  **at least 2 frames at x2**, so at least 67 ms at x1 (4f). What counts: for the coil peak, the hold plus the last
  40% of the coil (an eased-out coil barely moves there); for contact, the freeze. Mid-combo hits are exempt (the
  rhythm carries them); the last hit is not. Light everyday pokes and stabs (dagger, wand jab, staff thrust/sweep)
  are also exempt and may freeze 40-60 ms, like today's quick hit (40 ms, 4.4): the mark and spark carry the
  contact. Anything heavier keeps the 67 ms floor.
- Directions: the hero faces right, the monster stands on the right. "Forward" = toward the monster.
- Angles (blade, torso, limbs) as in the rig code: degrees **clockwise on screen, 0 = pointing forward, 90 = down,
  -90 = up, 180 = back**. So a blade at 200 deg points back and a little up; at 5 deg it is level forward; at -60
  it points up-forward.
- "Near" arm/leg = the one closer to the viewer (drawn on top), "far" = behind the body.
- "Needs rig code" on a card: the move needs a field the test rig doesn't have yet (list in 4.6). It costs no
  picture, but it is work.

---

## 1. What makes a blow feel powerful, tasty, playful, epic

A blow is a short story in beats: **ready -> coil (anticipation) -> strike -> contact -> follow-through -> settle
-> back home**. Every beat has a job. The numbers below are where to start; judge by eye at x1 and at x2.

### 1.1 The beats, with numbers

| Beat | Job | Light (dagger, wand) | Medium (sword, staff) | Heavy (axe, hammer, skill) |
|---|---|---|---|---|
| Coil | say "a blow is coming, from there" | 60-100 ms / 4-6f | 100-160 ms / 6-10f | 180-350 ms / 11-21f |
| Hold at the coil peak | let the eye read the pose | 20-40 ms / 1-2f | 40-60 ms / 3-4f | 60-120 ms / 4-7f |
| Strike (fast part) | the weapon crosses its path | 30-50 ms / 2-3f | 40-70 ms / 3-4f | 50-90 ms / 3-5f |
| Freeze at contact | weight | 40-60 ms / 2-4f | 60-90 ms / 4-5f | 90-150 ms / 5-9f |
| Follow-through | show how heavy it was | 60-100 ms / 4-6f | 80-150 ms / 5-9f | 150-300 ms / 9-18f |
| Settle / back home | calm, ready again | 180-250 ms | 250-320 ms | 300-400 ms |

Where the numbers come from: anticipation light 80-120 / medium 120-200 / heavy 200-350 ms (source 17);
hit stop light 2-3f / medium 4-6f / heavy 8-12f / killing blow 12-20f (source 3); one action game uses 4f for a
sword and 5f for heavy weapons (source 4); fighting games use about 8/12/16f light/medium/heavy (sources 1, 2), long
because both fighters must react. Ours is a show, not a reaction test, so the shorter action-game numbers fit
everyday blows and the fighting-game ones fit crits, finishers and bosses.

**At x2** every tween and the freeze run twice as fast (4.2). Rules:
- The **coil peak and the contact pose must last at least 2 frames at x2** = at least 67 ms at x1 (hold + freeze
  together count). Below that, x2 skips the pose and the blow turns into a blur. (Mid-combo hits and light
  pokes are exempt from the contact part; see the legend.)
- The **strike itself may be 2-3 frames at x1** (1-2 at x2) only if a trail or smear draws its path (1.6).
  Without a trail, give it at least 4 frames at x1.
- Shake and kick are not sped up at x2 (4.1). Keep everyday shakes at 90-140 ms so they don't drag at x2.
- Idea for the builder (code change, not made here): a floor on the freeze at x2, e.g. never under 35 ms real time.

### 1.2 Anticipation: pull back, then hold

- Move **opposite** to the blow: down-and-back before forward-and-down. Weight to the back foot, hips low.
- **Pull further back than feels natural, hold it relatively long, then go through the strike keys in 1 frame
  each** (source 15: a game director's notes on attack animation; "too much is just right").
- The coil pose must show **where** the blow will land: the head and eyes point at the target; the blade points
  along the line it will travel.
- Ease **into** the coil with a slow-out curve (CubicOut / QuadOut), so it arrives and "sits".
- A **strain** in the hold reads as power: 1-2 px shake on the body, a gritted face, a glint at the tip.
- Asymmetric coils: weight on one leg, body twisted. A symmetric coil looks static.

### 1.3 The strike: few frames, accelerating

- Ease **in** (QuadIn / CubicIn): slow at the start, fastest at contact. Never ease out into contact: it lands soft.
- **Successive breaking of joints**: the hips start first, the torso 1 frame later, the shoulder/upper arm 1 frame
  after that, then forearm, hand, and the blade last, whipping. Each about 17 ms behind the one before.
  This is the cheapest way to make flat parts look alive (and the first fix for "parts only turn, stiff").
- The swing should **start before the body arrives** and end right at contact, so the freeze holds the blow at its
  extreme (today's code already does this: `SwingAt`, BattleScreen.cs:2068).
- Exaggerate reach on the strike frame: forearm and blade a little longer (1.05-1.10x) for 1-2 frames (sources 9,
  11: "breaking the body").

### 1.4 The contact frame

- The frame the freeze holds is the one the player remembers: make it the **strongest, clearest pose** of the move
  (source 15). Check it as a still: one line of action from the back foot through hips, torso, arm and blade.
- Fire everything that says "contact" **on the same frame**: freeze, flash on the target, sparks, impact sound.
  Things that say "what it cost" (number, HP bar, knockback travel) come **after** the freeze (source 3).
- **Overshoot**: one frame past the final strike pose, then back (sources 9, 11). On bones: blade 8-15 deg past,
  then settle back over 60-100 ms. The `BackOut` ease gives about 10% overshoot.

### 1.5 Follow-through, settle, back home

- After contact the motion **keeps going on its own path**, not the strike played backwards (source 15). A sword
  ends low in front; a hammer stays in the ground; a staff bounces.
- **Hold a pose clearly unlike the ready stance** for the follow-through (source 15), then go home quickly.
  Avoid a slow, even blend from follow-through to ready (source 14: the in-between reads as mush).
- Heavy weapons: the hero is pulled by the weapon (hips drop, a half step forward), then **lifts it back with
  effort** (source 18).
- Landing: a small squash (0.92-0.95 tall) for 80-120 ms, then a tiny overshoot up (1.02) and settle.

### 1.5b Arcs, line of action, silhouette, eases

- **Arcs**: the weapon tip, the hand and the head travel on curves, never straight lines, except a thrust (a
  straight line is what makes a thrust read). The sword hand circles the **shoulder**; the body travels on a low
  arc (the hop). A heavy head swings the long way round.
- **Line of action**: one clear line through the pose. Coil = a bow bent away from the monster (back foot, hips,
  back, head, weapon laid back). Strike = a straight or forward-bent line from the back foot through the hips,
  torso, arm and blade into the target. Check it on a still of each key.
- **Silhouette**: at the coil peak and at contact, the weapon and the weapon arm stand clear of the body against
  the background; the free hand doesn't cover them; the head isn't hidden. Fill the key in black: you should still
  read the move.
- **Eases per beat** (names from `Anim`, Core/Anim.cs:8): into the coil `CubicOut`; the hold `Linear`; the strike
  `QuadIn` or `CubicIn`; the overshoot back `BackOut`; follow-through `QuadOut`; going home `SineInOut` with a
  hop; playful settles `ElasticOut` or `BounceOut` (sparingly). Avoid `Linear` between keys except the hold.

### 1.6 Trails, smears, afterimages

- A trail shows the path the tip just took. Thick and bright at the tip, thin at the hand, white edge leading,
  weapon or element colour in the middle, see-through tail (sources 7, 33). It must join the coil pose to the
  contact pose, or the blade seems to jump (source 33).
- Only during the strike. **No smear on the coil or the return** (source 12): it muddies the read.
- Short: visible 1-3 frames, fading in 60-120 ms. A trail that lingers turns into an object.
- How to build it on bones: record the blade tip and the hand point each frame during the strike; draw a strip
  between the two polylines (a crescent). Cheaper option: ghost copies of the blade sprite each frame (today's
  `SkeletonSwing.Trail`, 120 ms fade), but at x2 the gaps between ghosts open up; the strip doesn't.
- Afterimages of the whole body (3 ghosts, alpha 0.35 down to 0.2) sell a dash. Already in the game (4.3).

### 1.7 Squash, stretch and lag on flat cut-outs

- Squash and stretch the **whole puppet** (its root scale), not single limbs: limbs that change length look rubbery
  (source 10). Stretch 1.1-1.2 along a dash; squash 0.85-0.9 on a landing or contact; 0.94-0.97 on a coil.
- Keep the area roughly the same: x1.15 wide goes with about x0.87 tall.
- **Lag (drag)**: things on the end of a chain arrive later. Head 2-3 frames behind the torso on a fast move;
  free hand 1-2 frames; hair, cape, sash and robe hem 3-6 frames with a spring that overshoots once.
- **Weapon lag**: on a fast hand turn, let the blade angle trail the hand by 1-2 frames, then whip past (it reads as
  weight and flex).

### 1.8 Camera, flash, sound

- **Kick** the camera along the blow, 4-12 px, spring back in 100-170 ms (the game's `Kick`). Heavy: add 4-9 px down.
- **Shake**: 3-11 px for 90-180 ms, fading. Best felt **inside the freeze** (source 4). Shake strength that grows
  with the square of a "trauma" amount feels right; smooth noise feels better than per-frame random jitter, and
  in 2D a little roll (0.5-1.5 deg) with the slide helps (source 6). Today's shake is random per frame
  (GameRoot.cs:297): a smoother one is a possible upgrade, not tonight's job.
- **Zoom punch**: 1.03-1.05 in 60 ms, back in 180 ms (crit, today). Showpieces: 1.08-1.2.
- **Screen flash** only for finishers and showpieces. Everyday blows flash the target, not the screen.
- **Sound timing**: the whoosh starts with the strike (2-4 frames before contact); the hit sound on the contact frame
  exactly (a sound 3 frames late reads as another event, source 3); a low thud for heavy blows; for an epic hit,
  **silence for 150-300 ms before contact** (dim the music or cut the ambient), then the loudest hit of the fight.

### 1.9 The monster's side of the blow

- The first frame of the hurt is an **extreme, lopsided pain pose**, reached in a few frames, not a snap (source 15).
- Knockback after the freeze; tilt away about 1 deg per px pushed (one game uses 1.3, source 4).
- White flash 1-2 frames, then the element tint (today 100-140 ms, 4.4).
- Sparks fly **along the blow's direction**; a thrust sprays forward, an overhead blow sprays down and out.
- Today's numbers for all of this are in 4.4; reuse them.

### 1.10 Playful versus epic

**Playful** (tasty, cheeky, light):
- Overshoot and settle on everything: 1-2 small bounces (`BackOut`, `ElasticOut`), weapon bobbing after the blow.
- Uneven rhythm: "ta-ta-TAK" (two light, one heavy), a little pause and a wink of extra motion.
- Squash on the hop home; a skip in the step; the hero twirls the weapon or tosses it and catches it.
- Fast in, relaxed out: sharp strike, loose follow-through with the free hand flung wide.
- Small surprises: the staff tip boings back up, the dagger flips in the air between stabs.

**Epic** (powerful, big):
- A **build-up you can watch grow**: dim the backdrop, light gathers at the hands, ground glow, a rising sound,
  dust lifting. 300-900 ms.
- **Hang time**: the hero hangs at the top of a leap or holds the raised weapon 120-250 ms, slowed.
- **Camera**: push in during the build (1.08-1.15), hold, snap back or shake on contact.
- **Silence before the hit** (1.8), then the biggest freeze of the fight (150-250 ms), flash, ground ring, slow
  motion after (0.3x for ~0.5-1 s, the `Grand` path).
- **Delayed burst**: the hero passes through and lands in a still pose; a beat of nothing (200-300 ms); then all the
  hit marks burst at once. Cheap and very strong.
- **Scale**: effects bigger than the hero; shockwaves crossing the floor; the monster lifted off its feet.

### 1.11 Keeping the everyday blow fresh

- **2-3 alternates per weapon**, cycled in order (not random repeats): e.g. down-cut, rising cut, thrust. The game
  already flips the slash mark each hit (`twinFlip`, BattleScreen.cs:2728); pair the body move with it.
- Small random spread on every blow: timing +-5-10%, coil depth +-10%, blade angle +-5 deg, step length +-10 px.
- **Crit version**: same move, deeper coil (+40 ms), a shout face, a bigger overshoot, the long freeze, a zoom punch.
- **Finishing blow**: a stronger cousin of the same move, plus the existing finisher show (4.5).
- **Aim at the monster's size**: the contact height follows the target (small slime low, big ogre high).

## 2. How each weapon kind moves

How to read a move card:
- **Kind**: everyday / skill / showpiece.
- **Beats**: name, ms at x1 (frames at 60 fps), what leads, where the weapon tip goes. "Freeze" is the game's hit stop
  on contact (70 ms on trash today, longer on bosses); it is listed but not counted in the total unless it says so.
- **Total**: the sum of the beats, from leaving home to standing home again.
- **Swaps**: which shared swap pictures (3.4) the move uses: F-shout, F-hurt (faces, already drawn), F-grit, T-away,
  T-toward, H-open-n / H-open-f, A-fore. **Cost** = new pictures bare / with every gear piece (3.4 formula).
  "0 / 0" means the move works with what exists.
- **Chest turn (orchestrator's correction, 2026-11-17):** a forehand blow with the near hand opens the chest to the
  viewer on the coil (T-toward) and turns it away, back showing, on the strike (T-away). A backhand (D3) is the
  other way round. The rule and how it was checked are in 3.3.
- Footwork words: **planted** (feet stay), **step** (one foot moves), **hop** (both leave the ground a little),
  **leap**, **spin**, **slide**.
- All tip paths in screen terms, the hero facing right: "up-back" = above and behind the head.

### 2.1 Sword (10 weapons; the Warrior's start)

**How it moves.** Medium weight, balanced near the hand, so it turns fast around the wrist. One hand: the near
(right) fist on the grip, the blade coming out of the thumb side. The **free hand stays on its own side**: a fist
guard at the chest, or flung out behind for balance on big swings. It never crosses in front of the sword arm at a
read frame (that is what made the sword seem to switch hands, PLANS 4c). Power comes from the hips and the step,
not the arm (real longsword cuts: the body pivots, the step lands behind the cut, source 19). Recovery is quick and
upright. **Feel: sweeping, confident, clean.**

Paths to vary between: high diagonal down (the classic), rising from low back, level thrust, flat horizontal at
chest height.

**S1. Diagonal cut** (everyday)
- Coil 120 ms (7f): hips drop 25 px and turn back, torso leans back 14 deg, sword hand goes up-back over the near
  shoulder, tip behind the head (blade about 200 deg). Free fist comes to the chest. Weight on the back foot.
- Hold 50 ms (3f): strain 1 px, tip glints on crits.
- Dash + cut 110 ms (7f): body dashes in (QuadIn), front foot reaches far forward. In the last 45 ms the cut goes:
  hips, then torso, then arm, then blade, 1 frame apart. Tip draws a big arc from up-back, over the top, down to
  low-front. Trail on.
- Contact: freeze (70 trash / 90-130 boss). Blade at about 5 deg (level), shout face, slash mark.
- Follow 100 ms (6f): blade carries on to point down-forward (40-60 deg), body pitches in, back heel lifts.
- Held 60 ms (4f).
- Home 300 ms (18f): hop back on a low arc, blade swings up to ready, land squash.
- **Total 740 ms.** Camera: kick 6-10 px forward on contact. Swaps: F-shout (optional T-toward on the coil, T-away
  on contact). Cost 0 / 0 (or 2 / 24 with the two torsos).

**S2. Rising cut** (everyday alternate)
- Coil 110 ms (7f): drop low, sword hand back by the back hip, tip trailing low behind (blade about 150 deg),
  head forward and down.
- Hold 40 ms (2-3f).
- Dash + cut 110 ms (7f): the body rises as it arrives; the tip sweeps from low-back, under, up through the target
  to high-front. Ends with the blade up-forward (-60 deg), on the toes, a 10 px lift.
- Contact: freeze. Slash mark tilted the other way (pairs with `twinFlip`).
- Follow 110 ms (7f): tip overshoots past vertical by 10 deg, then drops back; free arm flung back.
- Held 50 ms (3f).
- Home 300 ms (18f).
- **Total 720 ms.** Leads: legs (rising), then arm. Camera: kick 4 px forward and 3 px up. Cost 0 / 0.

**S3. Lunge thrust** (everyday alternate)
- Coil 100 ms (6f): sword hand pulled back past the hip, elbow bent behind the body, blade level pointing at the
  monster, front knee up a little (cocked).
- Hold 50 ms (3f).
- Dash 120 ms (7f): long low lunge (front foot reaches 40 px further than S1), body almost horizontal at the end;
  the arm shoots straight in the last 40 ms; tip travels in a **straight line** (no arc: that is what makes it read
  as a thrust).
- Contact: freeze; stab mark; arm 1.08x long for 2 frames.
- Follow 80 ms (5f): small push further, then the arm snaps back 20 px (a recoil).
- Held 60 ms (4f).
- Home 300 ms (18f).
- **Total 710 ms.** Camera: kick 8 px straight forward, no down. Cost 0 / 0.

**S4. Crit flourish** (everyday, crit version of S1)
- As S1, with: coil 160 (+40), hold 70, F-grit in the hold, glint at the tip.
- After the follow: a 60 ms flick back up across (a second small mark, no damage) and a wrist twirl (blade spins
  360 deg around the grip in 180 ms) before the hop home.
- **Total about 1.0 s.** Cost 0 / 0 (F-grit 1 / 10 if made).

**S5. Three-cut chain** (skill, multi-hit)
- First: S1 up to contact, 350 ms (coil 120, hold 50, dash 110, freeze 70).
- Second, 200 ms: step back 30 px in 50 ms, rising cut in 50 ms, contact (freeze 40), follow 60.
  (This is today's combo rhythm, AttackAnim 2488-2517.)
- Third, 280 ms: blade pulled back level (60), thrust (50), contact (freeze 90 + kick 10 px), arm overshoot (80).
- Held 120 ms, then home 300 ms.
- **Total 1250 ms, freezes included.** Rhythm "ta-ta-TAK". Marks alternate in tilt. Cost 0 / 0.

**S6. Leaping overhead** (showpiece)
- Crouch 200 ms (12f): deep squash (0.85 tall), both feet planted, sword low behind; backdrop dims 0.35; camera
  leans in 1.08.
- Jump 180 ms (11f): stretched tall (1.15), rising 150 px on a parabola; blade swings up over the head.
- Hang 160 ms (10f) at the top, slowed: blade held straight up-back over the head, F-shout, glint at the tip,
  silence.
- Drop 80 ms (5f): plunge on a steep line, blade sweeping down in front (trail on, thick).
- Contact: freeze 150 (boss 200), screen flash 0.35, ground shockwave, shake 160 ms 9 px, kick 9 px down.
- Follow 220 ms (13f): blade buried low in front, knees deep, dust ring; held 120 ms.
- Home 340 ms (20f).
- **Total about 1.3 s + freeze.** Leads: legs (jump), then the whole body; the arm only at the drop.
  Swaps: F-shout (have), T-toward for the hang (optional). Cost 0 / 0 (1 / 12 with T-toward).

**S7. Turnaround cut** (skill, spin; risky; needs rig code: root scale)
- Coil 140 ms: wind the whole body back (torso -20 deg, sword low behind). Hold 20 ms.
- Spin 180 ms: the puppet squeezes wide-to-thin (root scale x 1 -> 0.2 -> 1, never below 0, so the sword
  never shows in the other hand), a full-circle trail ring drawn round the body, the blade passing through the
  monster at the end.
- Contact, follow 100 ms, home 300 ms. **Total 740 ms + freeze.**
- Risk: the squeeze may read as a paper card turning. Judge it in a clip before keeping. Cost 0 / 0.

### 2.2 Dagger (14 weapons; the Rogue's start)

**How it moves.** Very light, short. The body does the travelling; the arm only finishes. Two grips:
**forward** (blade out of the thumb side, pointing forward: stabs and slashes) and **reverse** (blade out of the
little-finger side, lying along the forearm: downward stabs and backhand slashes). Often two daggers (the "dual"
hold). Low crouch, weight on the balls of the feet, quick hops, gets **close** (today's dagger stops 34 px closer).
Recovery is a springy hop back. **Feel: quick, sly, snappy.**

Reverse grip on our rig: the weapon sits between forearm and fist (4.6), so the same fist picture can hold the blade
pointing out of either end. Try turning the weapon 180 deg about its grip point before drawing a new hand.

**D1. Quick stab** (everyday)
- Coil 70 ms (4f): drop 15 px, elbow back, dagger level at the hip, far hand forward as a guard.
- Hold 40 ms (2-3f).
- Dash 90 ms (5f): low and fast, almost no hop (6 px), body leaning in.
- Stab 40 ms (2-3f): the arm shoots out straight, tip in a straight line at the monster's middle.
- Contact: freeze 50-60 ms; stab mark; spark forward.
- Pull 60 ms (4f): arm snaps back, blade flicked up.
- Home 230 ms (14f): a springy hop back with a small squash.
- **Total 530 ms.** Camera: kick 4 px. Cost 0 / 0. Needs rig code: the dagger in the hand (Build hardcodes the
  sword).

**D2. Double stab** (everyday alternate; with two daggers, near then far hand)
- Coil 70 ms, hold 40 ms, dash 90 ms.
- Stab one 40 ms (near hand), freeze 40, pull 50 ms while the far hand stabs 40 ms, freeze 50.
- Pull 60 ms, home 230 ms.
- **Total 620 ms + freezes.** Rhythm "ta-TAK". With one dagger: the same hand twice, the second higher.
  The far hand stabs with `FarFront` on (4.6) so it crosses in front for that one frame. Cost 0 / 0. Needs rig
  code: far weapon, `FarFront` per key.

**D3. Reverse-grip backhand** (everyday alternate; needs rig code: reverse grip flag)
- Coil 90 ms (5f): reverse grip, dagger fist pulled across to the far shoulder (the near arm crosses the chest, in
  front of the torso, which it already is in draw order), torso turned away.
- Hold 40 ms.
- Dash + slash 100 ms (6f): the torso unwinds toward the viewer, the arm sweeps out flat at chest height, tip in a
  wide flat arc ending behind (arm fully out back).
- Contact: freeze 60; flat slash mark.
- Follow 80 ms: arm extended back, body twisted open, free hand forward.
- Home 230 ms.
- **Total 540 ms.** Swaps: T-toward helps the unwind. Cost 0 / 0 (1 / 12 with T-toward).

**D4. Flurry** (skill, 5 hits)
- Coil 90 ms, hold 40 ms, dash 90 ms.
- Five stabs, each 70 ms (stab 30, pull 40), freeze 25 ms each, alternating hands (or alternating high/low with one
  dagger); marks scattered +-20 px; the body bounces 4 px per stab on its toes.
- Last stab: freeze 90, push 20 px, kick 8 px.
- Follow 80, home 250.
- **Total about 1.1 s, freezes included.** Leads: shoulders alternating. Cost 0 / 0. Needs rig code as D2.

**D5. Vanish and reappear behind** (skill / showpiece, cheap; needs rig code: root scale and mirror)
- Coil 100 ms: crouch, dagger reverse grip. Hold 30 ms.
- Vanish 60 ms: the puppet thins to a line (root scale x -> 0.05) and a puff of smoke.
- Appear behind the monster, facing left (whole puppet mirrored: x scale -1; the dagger stays in the near hand, so it
  doesn't read as a switch), already high: 60 ms.
- Downward stab 50 ms, contact freeze 90, monster flashes.
- Vanish again 60 ms, appear at home facing right 60 ms, settle 150 ms.
- **Total 570 ms + freeze.** Camera: no kick on the vanish, kick on the stab. Cost 0 / 0.

**D6. Pass-through, delayed burst** (showpiece)
- Coil 160 ms with F-grit, dim 0.3. Hold 40 ms.
- Dash through 120 ms: the hero passes **through** the monster to its far side, 3-4 trails crossing it (no hit
  yet, no sound but a whoosh).
- Landing pose 80 ms: crouched low, back to the monster, dagger held out reverse grip, the other hand flat.
- Silence 250 ms: nothing moves but cloth and dust.
- Burst: all marks at once on the monster, freeze 150, flash, sparks, the biggest sound.
- Dash home 300 ms.
- **Total about 1.1 s, freeze included.** Swaps: H-open-f for the flat hand (optional). Cost 0 / 0 (1 / 10 with
  H-open-f).

### 2.3 Staff (11 weapons; the Mage's start): casting and bonking

**How it moves.** Long, held in **both hands** (near hand nearer the bottom, far hand up the shaft, `On`/`OnT` in
4.6). Long reach, so the hero needs to come less close. Power from turning the hips and shoulders together, little
from the arms (source 20). The hands can **slide** along the shaft: together for a fast swing (short lever), apart
for control, the far hand sliding to the end to reach. Real thrusts drive the staff along its own length, like a cue
(source 21). A mage also casts: plant, gather, point, release.
**Feel (bonk): sweeping, grounded, bouncy. Feel (cast): gathered, precise, released.**

Rig note for every staff card: needs rig code (the staff in the hand, and `On` / `OnT` per key; 4.6).

**St1. Overhead bonk** (everyday)
- Coil 150 ms (9f): both hands lift the staff up and back over the head, top end behind; torso leans back 10 deg;
  the far hand slides down toward the near hand (`OnT` shorter) to "grip for power".
- Hold 40 ms (2-3f).
- Hop in 110 ms (7f): a small hop (16 px); the staff comes over the top, top end leading, far hand sliding back up
  the shaft as it falls (`OnT` longer: the reach grows).
- Contact: freeze 70; impact mark on the monster's head; "bonk".
- Follow 80 ms (5f): the staff **bounces** back up 15-20 deg (`BackOut`), the body bobs.
- Held 60 ms.
- Home 280 ms.
- **Total 720 ms.** Playful. Cost 0 / 0.

**St2. Cue thrust** (everyday alternate)
- Coil 110 ms: staff level, both hands slide back along the shaft (the staff end pulled back past the hip).
- Hold 40 ms.
- Step 110 ms: front foot steps, the staff drives straight forward along its own line, through the far hand (the far
  hand stays still while the shaft slides through it, `OnT` changing).
- Contact: freeze 60; stab-like impact at chest height.
- Follow 80 ms: staff pulled back 30 px, body upright.
- Home 280 ms.
- **Total 620 ms.** Cost 0 / 0.

**St3. Low sweep** (everyday alternate)
- Coil 120 ms: crouch low, staff swung back behind at knee height. Hold 20 ms.
- Dash 110 ms, sweep 50 ms: staff end swings flat at shin height; monster's feet knocked (monster tilts back more
  than usual, a small lift).
- Contact: freeze 60.
- Follow 100 ms: the staff carries on round to the front-up; the hero rises.
- Home 280 ms.
- **Total 680 ms.** Cost 0 / 0.

**St4. Point and release** (everyday cast, the bolt)
- Gather 300 ms (today's bolt charge): plant the staff butt, the top end tilts up, both hands stay on the shaft;
  light gathers at the top end; the hero rises on the toes 8 px, robe hem lifts.
- Point 70 ms: both arms push the staff forward, the top end aimed at the monster (straight line).
- Release: the bolt leaves the top end; recoil: body leans back 6 deg, staff kicks up 10 deg (`BackOut`), a 3 px
  camera kick backwards.
- Projectile 420 ms (today), contact on the monster.
- Settle 240 ms.
- **Total about 1.0 s** (most of it is the bolt flying; the hero's part is 610 ms). Cost 0 / 0.

**St5. Twirl and plant** (skill cast)
- Twirl 300 ms: the far hand lets go (H-open-f), the near hand spins the staff in front, 1.5 turns around the grip,
  with a circular trail ring.
- Catch 60 ms: the far hand grabs back on.
- Plant 90 ms: the butt slams the ground; ground ring in the element colour, dust, kick 6 px down.
- Release: the spell rises from the ring under the monster (or flies from the top end).
- Settle 280 ms.
- **Total about 730 ms + the spell.** Cost 0 / 0 (1 / 10 if the far open hand is drawn).

**St6. Raised staff, beam from above** (showpiece cast)
- Raise 250 ms: the staff lifted overhead in both hands, top end straight up; F-shout.
- Float 400 ms: the hero rises 30 px off the ground, robe hanging, backdrop dims 0.45, camera pushes in 1.12,
  particles stream up into the top end; sound rises, then cuts.
- Silence 200 ms.
- Strike: a beam or bolt lands on the monster; freeze 170, screen flash 0.45, shockwave, shake 200 ms 10 px.
- Land 200 ms, settle 300 ms.
- **Total about 1.5 s, freeze included.** Cost 0 / 0.

**St7. Vault kick** (showpiece melee; risky)
- Plant the staff ahead of the hero 120 ms, vault up and over on it 300 ms (the body swings round the staff, legs
  leading, the hands as the pivot), both feet kick the monster at the top, freeze 120, drop and pull the staff out
  200 ms, home 300 ms. **Total about 1.0 s.** Needs a hands-fixed / body-moving setup (IK with the hands as the
  anchor). Try only after the others work.

### 2.4 Axe (2 weapons)

**How it moves.** Heavy head at the end of a handle: slow to start, unstoppable once moving. Two hands at the end
of the haft for a full chop (the far hand slides down to the near hand during the swing), or one hand for a
short hack. Weight stays over the feet (a heavy weapon held out far tips you over, source 22). The head travels the
long way round; the hero follows it. Ends stuck low in front, then wrenched back. **Feel: heavy, committed, chopping.**

**A1. Overhead chop** (everyday)
- Coil 180 ms (11f): staff-like lift overhead, axe head far behind; hips drop; torso leads the lift (the body turns
  first, then the axe comes up: source 18).
- Hold 60 ms (4f).
- Hop in 120 ms (7f), chop 50 ms: the head comes straight down over the top.
- Contact: freeze 90-130 (today's axe already kicks 6 px down).
- Follow 120 ms: the head digs in low in front; held 100 ms; hips sink.
- Wrench back 80 ms, home 320 ms.
- **Total about 1.0 s.** Over the 750 ms budget: fine for a rare, heavy kind, or trim the coil to 150 and the
  hold-down to 60 (total about 960). Cost: the axe needs its rig weapon picture (1, no gear multiplier).

**A2. Woodcutter's side swing** (everyday alternate)
- Coil 160 ms: axe back at hip height behind, both hands at the end of the haft, hips wound away. Hold 50 ms.
- Dash 120 ms, swing 50 ms: the head sweeps flat at waist height through the target, hips first.
- Contact: freeze 90; near-flat cleave mark.
- Follow 140 ms: the weight carries the hero round half a step, front heel pivoting; held 60 ms.
- Home 300 ms.
- **Total 880 ms + freeze.** Over the 750 ms budget: allowed for a rare kind (2 axes); trim the held 60 first.
  Cost: the axe rig picture (shared with A1).

**A3. Toss and catch** (skill, playful)
- Coil 140 ms: axe cocked behind the head, one hand. Hold 50 ms.
- Throw 60 ms: the arm whips forward, the hand opens (H-open-n, or simply let go with the fist).
- Flight out 260 ms: a spinning axe sprite with a ring trail; hit (freeze 90, cleave mark).
- Flight back 300 ms on an arc above; the hero hops once, waiting.
- Catch 80 ms: squash 0.92, a small grin bob. Settle 150 ms. (No go-in: thrown from home.)
- **Total about 1.1 s, freeze included.** Cost 0 / 0 (1 / 10 with H-open-n).

**A4. Two-turn spin** (showpiece; needs rig code: root scale)
- Coil 200 ms with F-grit, dim 0.3. Hold 80 ms.
- Turn one 200 ms moving forward 80 px (root squeeze x 1 -> 0.2 -> 1 with a ring trail), hit one (freeze 60).
- Turn two 200 ms, hit two lifts the monster 40 px (freeze 150, shake 160 ms 9 px).
- Follow 200 ms: the axe head drags on the ground, sparks; the hero staggers a step. Home 340 ms.
- **Total about 1.4 s, freezes included.** Cost 0 / 0.

### 2.5 Hammer (1 weapon)

**How it moves.** The heaviest. Lifted with effort, kept balanced over the feet, then dropped and **driven** with
the whole body (source 22). A small **rebound** after it hits the ground (source 12). Long recovery: the hero
heaves it back up. **Feel: heavy, slamming, earth-shaking.**

**H1. Overhead drop** (everyday)
- Lift 220 ms: hands slide apart up the handle, the head goes up and back over the shoulder; knees bend.
- Hold 90 ms: strain, F-grit.
- Jump in 160 ms (34 px hop, as today), slam 50 ms.
- Contact: freeze 120 (boss 150), shockwave across the floor, kick 9 px down, thud.
- Rebound 60 ms: the head bounces up 10 deg and drops back.
- Held 140 ms, heave up 120 ms, home 360 ms.
- **Total about 1.2 s.** Rare weapon (1 of 40), so the length is allowed; trim the held and heave first if needed.

**H2. Handle poke, then drop** (everyday alternate, playful)
- Coil 120 ms: hammer held across the body, handle end forward. Hold 50 ms.
- Dash 120 ms, poke 40 ms: the handle end jabs the monster (freeze 40, small mark: no damage number yet).
- Lift 140 ms: the hammer flips up over the shoulder in one turn; drop 50 ms on the head (freeze 110, thud).
- Rebound 60 ms, home 340 ms.
- **Total about 1.1 s, freezes included.** Rhythm "tik... BOOM". Over the budget like H1: allowed for the one
  hammer; trim the lift and home first.

**H3. Uppercut swing** (skill)
- Coil 220 ms: the hammer hangs low behind the back foot, body crouched and wound. Hold 80 ms.
- Dash 140 ms, swing 60 ms: up from under, through the monster.
- Contact: freeze 130; the monster is lifted 40-60 px and falls back (lift tween 300 ms, lands with dust).
- Follow 160 ms: the hammer ends high, the hero on tiptoe, then drops back on the heels. Home 360 ms.
- **Total about 1.15 s, freeze included** (the monster's lift runs during the follow).

**H4. High jump drop** (showpiece)
- As S6, with: crouch 240, jump 200 to 180 px, the hammer turning one full circle in the air during a 180 ms hang,
  drop 80, freeze 170 (boss 220), floor crack and shockwave, shake 250 ms 11 px, rebound 60, held 160, heave 140,
  home 360.
- **Total about 1.6 s, freeze included.**

### 2.6 Wand (2 weapons)

**How it moves.** Tiny and light: the wrist and fingers do the work. One hand; the free hand often open, shaping
the spell. Casts are quick flicks or traced shapes; melee is a jab. **Feel: nimble, crisp, sparkly.**

**W1. Flick bolt** (everyday): wand cocked back by the ear 100 ms (4.5's 20 px step back happens here), hold
30 ms, flick forward 40 ms (the wrist snaps, tip on a short arc), the bolt leaves the tip, the arm overshoots
10 deg and settles 80 ms, free hand flung out; back home 240 ms (today's bolt recovery). Hero part 490 ms plus the
bolt's flight.

**W2. Wand jab** (everyday melee)
- Coil 80 ms: wand hand pulled back by the hip, chest opened to the viewer a little. Hold 40 ms.
- Dash 100 ms (stops 20 px closer, as today), jab 40 ms: a straight poke at the monster's face, tip sparkles.
- Contact: freeze 50; stab mark plus a few sparks.
- Follow 60 ms: the wrist flicks the wand up. Home 240 ms.
- **Total 560 ms.** Cost 0 / 0.

**W3. Trace and push** (skill)
- Trace 300 ms: the tip draws a circle in the air in front, leaving a glowing line.
- Push 70 ms: the free hand (H-open-f) pushes through the middle of the circle; the circle flies at the monster
  (projectile 360 ms), hits.
- Settle 240 ms.
- **Total about 970 ms.** Cost 1 / 10.

**W4. Conductor** (skill, playful)
- Raise 120 ms: wand up, chin up. Hold 40 ms.
- Three flicks, up, down, up, each 60 ms with 60 ms between; a small spark lands on the monster on each (freeze
  30 each; the third 80 with a bigger burst).
- Bow 200 ms: a little bow with the wand across the chest. Settle 200 ms.
- **Total about 1.0 s, freezes included.** Cost 0 / 0.

## 3. How good cut-out puppets are built

### 3.1 Joints that never open

- **Round ends that overlap**: every part ends in a circle around its joint, and the two neighbours overlap there.
  A round end turning inside another always covers the joint; flat ends that just touch open a notch when bent
  (sources 23, 24, 25).
- **The pivot is the centre of that circle**, at the real joint: the middle of the shoulder, elbow, wrist, hip,
  knee, ankle, the base of the neck. An off-centre pivot makes the part slide away from its neighbour.
- **Draw a little extra**: limbs slightly longer under the overlap, so extreme bends don't show a gap (source 24).
- **Build for the most extreme pose** the moves need (the deep lunge, the arm over the head), not the rest pose:
  that is where rigs break (source 24).
- **Wiggle test**: swing every joint back and forth through its full range at once and look for gaps and sliding
  (source 23). Do it before animating anything, and again after any new part picture.
- Shoulders: the upper arm's top tucks **under** the torso's shoulder edge for the far arm and **over** the torso
  for the near arm, with a round cap. The small shoulder seams in the skeleton clip (PLANS 4c) are this.

### 3.2 Draw order

- Far arm and far leg behind the torso and hips, near arm and near leg in front (our rig: 4.6).
- The hand sits over its forearm, so the round wrist hides the joint (source 23); the weapon between forearm and
  fist so the fingers close over the grip (our rig already does this).
- **Order swaps mid-move** are normal tools: the far arm comes in front of the body for a cross-body move
  (`FarFront`), the near arm goes behind the torso when the sword is laid back behind the back. Cut the swap on a
  frame where the parts don't overlap, or under a fast motion / trail, or it pops. Prefer a key that swaps at a
  chosen time over a swap at the halfway point of a blend (today's `Mix` swaps at 50%).
- A **weapon** can need its own order: in front of the head when raised in front, behind the body when laid back.

### 3.3 Faking depth and twist with flat parts

> **Which way the chest turns (orchestrator's correction, 2026-11-17).** The first version of these notes had it the
> wrong way round for a forehand blow. Checked against the drawn wind-up and strike
> (`art/tests/sheets/bones/baseline/warrior_sword/key_006_sword_wind.webp`, `key_024_sword_strike.webp`): on a
> **forehand** blow with the near hand (the weapon starts on the near side: S1, S6, the overhead blows, a jab pulled
> back at the hip) the **coil pulls the near shoulder back and opens the chest to the viewer**; the **strike drives
> the near shoulder forward and turns the chest away**, back and near side showing, the body bent over the cut. A
> **backhand** (D3: the fist is pulled across to the far shoulder first) is the other way round. The swap pictures
> are named for what they show (T-toward = chest open to the viewer, T-away = back showing more); each move says
> which one it shows when.

None of these need a new picture. Try them before drawing swaps:
- **Torso squeeze**: scale the torso x to 0.85-0.92 when it turns away or toward the viewer; back to 1 when side-on.
  (Orchestrator's note: the base torso is drawn 3/4 front, so by plain geometry it is widest opened to the viewer and
  narrowest side-on. Try the squeeze both ways and keep what reads.)
- **Shoulder slide**: move the shoulder joints when the chest turns: opening to the viewer (forehand coil), the near
  shoulder slides back 10-25 rig px and the far shoulder slides forward and shows more; turning away (forehand
  strike), the near shoulder comes forward and the far shoulder hides more behind the torso. The arms are attached
  there, so they follow.
- **Hips against shoulders**: in the coil the hips turn back less than the shoulders (a wound spring); in the strike
  the hips arrive first and the shoulders whip after. The visible angle between them is the twist.
- **Head lag and counter-turn**: the head arrives 2-3 frames after the torso and tilts the other way a little,
  keeping the eyes on the target.
- **Depth by size**: a hand or weapon coming toward the viewer can grow 5-10% for 2-3 frames (foreshortening);
  going away, shrink 5%.
- **Depth by brightness**: a part turned away gets 5-10% darker (slot tint), a part coming toward 5% lighter.

When these aren't enough, a **swap picture** is the answer (the drawn twist the designer liked): professionals
switch whole drawings for a body turn rather than bending one picture (source 26), and cover the switch with
motion so the eye doesn't catch it.

### 3.4 Swap pictures: the shared set and what they cost

Rule: a swap picture replaces one part's picture for some frames (a "slot" showing another "attachment").
**Cost of one swap picture of a part = 1 bare picture + 1 per gear piece that covers that part.**
Gear on parts today (rig.json `slots`, 4.6) and counts (4.7): chest pieces (11 with an outfit) cover torso, upper
arms, forearms; helms (9) the head; legs (9) hips to feet; boots (9) shins and feet; gloves (9) will cover the hands.

The shared set every move should draw from (don't invent per-move swaps):

| Code | Part | What it shows | Used for | Cost bare / all gear |
|---|---|---|---|---|
| F-shout | head (face) | shouting | strikes, crits | **already drawn** (2 x 10 with hurt) |
| F-hurt | head (face) | pain | being hit | **already drawn** |
| F-grit | head (face) | gritted teeth, eyes narrowed | coils, charges (the calm wind-up face was a complaint) | 1 / 10 |
| T-away | torso | chest turned away, back showing more | forehand strikes and follow-throughs; backhand coils | 1 / 12 |
| T-toward | torso | chest open to the viewer | forehand coils and wind-ups; backhand strikes | 1 / 12 |
| H-open-n / H-open-f | hand | open flat hand | casting, pushing, balance | 1 / 10 each |
| A-fore | forearm + fist | forearm pointing at the viewer, fist big | punches or reaches toward the camera; rare in side view | 2 / 22 (forearm 1+11, fist 1+9) |

So the two torsos together cost 2 bare / 24 with every chest piece; the grit face 1 / 10. The rest of the set is
for later. The faces show the formula already works (shout and hurt: 1 bare + 9 helms each).

Make swaps **pop-proof**: same pivot and same joint circles as the base picture; same outline thickness; swap on a
fast frame or under a trail; a ghost of the old picture (the game's `SwapPose` ghost, 110 ms) hides the cut.

### 3.5 Arms, legs, two hands, the weapon in the hand

- **Legs on IK** with feet planted (ours does this, 4.6): the hips move, the feet stay. Lift a foot only on purpose
  (a step key moves the ankle target on an arc, 20-30 px up at mid-step).
- **Arms on IK** for holding and reaching (the hand must reach a point: a grip on a shaft, the guard at the chest).
  **Arms on turns (FK) for swings**: a swing reads best as arcs from the shoulder (source 24). Our sword arm uses
  IK with the hand moved on an arc (`SkeletonSwing.Arc`); the arc's centre should follow the shoulder (today it is
  a fixed point, 4.6).
- **Elbow bend side** must not flip during a blend (that pops the elbow inside out). Flip it only on a fast frame,
  or blend the target so the arm passes straight there.
- **Two hands on one weapon**: the near hand holds the grip (the weapon's parent); the far hand is placed by IK on a
  point along the shaft (ours: `On`, `OnT`). Animate `OnT` to slide the hand along the shaft (staff, axe,
  hammer). If the far arm can't reach (the point is further than the arm), the hand lets go (open hand) rather than
  stretching.
- **The weapon is a child of the hand bone** with a fixed grip point and angle; it turns with the hand. Never move
  the weapon on its own; turn the hand. The grip angle per hand is in rig.json `grips`.
- **The free hand needs a home**: a guard fist at the chest, a balance hand out behind, or on the weapon. Give it a
  key in every pose so it doesn't drift or float (the floating far fist in the clip).
- **Stretch**: arms and the blade may stretch up to 1.08-1.10 for 1-2 frames at the extreme of a thrust or strike.
  More reads as rubber.

### 3.6 Common faults and their fixes

| Fault | Looks like | Fix |
|---|---|---|
| Gap at a joint | a notch at the elbow, knee or shoulder when bent | rounder, bigger overlap; pivot at the circle's centre; wiggle test |
| Sliding part | the forearm drifts off the elbow | pivot not on the joint; re-place the pivot |
| Floating piece | a hand or foot hanging in space | give it a key in every pose; IK target for hands that hold; feet planted |
| Paper-doll stiffness | parts turn flat and all start and stop together | successive breaking (1.3), lag (1.7), shoulder slide and torso squeeze (3.3), arcs |
| Pop on a swap | a jump when a picture or draw order changes | swap on a fast frame or under a trail, same pivots, a ghost; key the swap time, don't swap at 50% |
| Elbow flips | the arm snaps inside out | keep the bend side through the move; change it only when the arm is straight |
| Rubber limbs | arms and legs that stretch and squash | squash the whole puppet, limbs only 1.05-1.10 for 1-2 frames |
| Weapon changes hands | the free fist crosses in front, then back | keep the free hand on its own side; if it must cross, `FarFront` for that frame |
| Smearing meshes | bent pictures go blurry or stretched | no mesh bending on body parts; turn rigid parts and swap pictures (PLANS lesson) |
| Mush between poses | slow even blends look slow and unclear | short blends into strong held poses (source 14) |
| Too long at x2 | at x2 still waiting | everyday move total about 750 ms at x1 including go-in and return |

### 3.7 How proven skeleton tools shape their data (to shape our own move files alike)

Ideas only (from a well-known 2D skeleton tool's documentation, sources 27-29, and the engine's own 2D package,
source 30). Our own format, our own names.
- **Bones**: a tree; each has a turn, a position, a scale. Children follow parents.
- **Slots**: a named place on a bone that shows **one picture at a time** (or none). The **draw order is the list of
  slots**, not the bone tree, so two pictures on one bone can sit above and below a third.
- **Attachments**: the pictures a slot can show (a face slot: calm, shout, hurt, grit).
- **Skins**: a map from (slot, attachment name) to the actual picture. Same names, different pictures per skin:
  that is gear. A gear piece lists only the slots it covers; anything missing falls back to bare. Our engine's own
  2D package has the same idea as categories and labels with a resolver per renderer (source 30).
- **IK constraints**: two bones and a target; a "mix" from 0 (plain turns) to 1 (full IK); a bend direction.
  The mix can be keyed, so an arm can go from IK (holding) to turns (swinging) inside one move.
- **Animations are timelines**: per bone (turn, position, scale), per slot (which picture, tint), the draw order,
  the IK mix and bend, and **events**. Each key has a time and a **curve to the next key** (stepped, linear, or a
  bezier with two control points).
- **Events**: named marks on the timeline with a few values (a number, a word). Ours would be: `contact` (fire
  Impact), `sound:whoosh`, `trail:on` / `trail:off`, `freeze:90`, `shake:120,8`, `kick:8,0`, `zoom:1.08,200`,
  `dim:0.35`, `face:shout`, `torso:away`, `order:far-front`.

A move file for us could be: name, weapon kind, everyday/skill/showpiece, a list of keys (time ms at x1, pose
values: root, hips, torso, head, hand targets, blade angle, foot targets, bend sides), each with its curve, plus a
list of events. The battle plays it on `Anim` time so x2 and the freeze reach it.

## 4. What the game already has (read from code)

All in `unity/Assets/Scripts/Game/`. Line numbers as of 2026-11-17 on `claude/unity-port`.

### 4.1 Camera and freeze tools

- **`GameRoot.Kick(dx, dy)`** (Core/GameRoot.cs:265): knocks the camera dx, dy design px the way the blow travels; a
  stiff spring brings it back (GameRoot.cs:304: omega 40, damping 0.6, stepped at 1/240 s). Back in about 0.1-0.17 s
  with one small bounce. Kicks add up.
- **`GameRoot.Shake(seconds, pixels)`** (GameRoot.cs:277): random jitter of `pixels` design px that fades out
  quadratically over `seconds` (GameRoot.cs:274). A weaker shake never cuts a stronger one short.
- **Both run on raw `Time.deltaTime`** (GameRoot.cs:286): x2 speed does **not** shorten them and the hit stop does
  **not** freeze them. So the camera keeps shaking through a freeze (good), and at x2 a shake lasts as long in real
  time as at x1 (keep shakes short: 90-180 ms).
- **`Fx.HitStop(ms)`** (Fx/Fx.cs:279): sets `Anim.TimeScale = 0.02` (near-frozen, not zero) and restores it after
  `ms / 1000 / Anim.Speed` real seconds (FxRunner.cs:40). So **at x2 the freeze is half as long**. Particles freeze
  with it (`ParticlesFollowHitStop`, FxParticles.cs:14).
- **`Fx.SlowMo(scale, ms)`** (Fx/FxBlood.cs:282): game time at `scale` for `ms / Anim.Speed` real ms; a hit stop
  inside it still freezes.
- **`GameRoot.Flash(seconds, color)`** (GameRoot.cs:236): full-screen colour flash that fades.
- **`ViewIn(x, y, zoom, seconds)`** (Screens/BattleScreen.cs:2176) / `ViewOut` (2182): camera zoom toward a point,
  clamped so no black edge shows.
- **`Dim(alpha, seconds)`** (BattleScreen.cs:2416): darkens the backdrop only (not fighters or effects).

### 4.2 Time: Anim, x2, and the test rig's own clock

- **`Anim`** (Core/Anim.cs): tweens with eases `Linear, QuadIn/Out, CubicIn/Out/InOut, BackIn/Out, SineInOut,
  BounceOut, ElasticOut` (Anim.cs:8, formulas at 167-199). `Anim.Wait(s)` (111) is game time.
- `Anim.Tick` multiplies dt by `TimeScale * Speed` (Anim.cs:127). `Speed` is the x2 battle option, set from
  `Saves.BattleSpeed` (BattleScreen.cs:303, 1121); `TimeScale` is what hit stop and slow motion lower.
- **`SkeletonSwing` (Test/SkeletonSwing.cs) runs on its own `clock += Time.deltaTime` (line 196).** It ignores x2
  and ignores hit stop. A move made on it must be moved onto Anim time (or read `Anim.TimeScale * Anim.Speed`) before
  it goes into the battle, or the freeze and x2 won't touch it.

### 4.3 Today's drawn sword blow (BattleScreen.cs `WeaponBlow`, 2094-2150)

The beats, with the `Blows["sword"]` numbers (BattleScreen.cs:2074):

| Beat | What happens | Time |
|---|---|---|
| Anticipate (2342) | swap to drawn `sword_wind`, ease back 20+18*power px, lean back, squash 1.06/0.94, CubicOut | 140 ms / 8f |
| Hold | held coil, body strains (shakes) | 40 ms / 2-3f |
| Lunge (2365) | swap to `sword_step` at 15%, QuadIn dash to reach, hop 14 px, stretch 1.2/0.86, 3 afterimages, speed lines, whoosh | 110 ms / 7f |
| Contact | swap to `sword_strike`, weapon turns -16 deg in 50 ms; dust, "slash"; `Impact` runs (hit stop, shake, kick) | 0 + freeze |
| FollowThrough (2384) | squash 0.9/1.08, carry on 22 px, QuadOut | 90 ms / 5f |
| Held strike | `posed && chain.Last` wait | 80 ms / 5f |
| Recover (2395) | back to standing, SineInOut hop of 16 px home, land squash 1.06/0.94 | 300 ms / 18f |

**Sum: 140 + 40 + 110 + 90 + 80 + 300 = 760 ms**, plus the freeze at contact (70 ms on trash, see below): about
**0.83 s at x1** and about **0.42 s at x2**. This is the budget the everyday blow on bones should match.

Other kinds in the same table (Antic / Hold / Lunge / Follow px / FollowHold / Recover, seconds):
dagger 0.08 / 0.02 / 0.09 / 12 / 0 / 0.22 (stops 34 px closer); axe 0.20 / 0.06 / 0.12 / 14 / 0.12 / 0.32 (ground
shockwave + Kick(2, 6)); hammer 0.26 / 0.09 / 0.16 / 8 / 0.18 / 0.36 (hop 34, "leap" pose, Kick(2, 9)); staff
0.18 / 0.05 / 0.12 / 16 / 0.06 / 0.30; wand 0.10 / 0.03 / 0.10 / 12 / 0 / 0.24.

Pose swaps leave a white ghost of the old pose (alpha 0.28, 110 ms; `SwapPose` 2168, `Ghost` 2440). The lunge drops
3 afterimages (alpha 0.35 / 0.28 / 0.21, each fading 220 ms; `Afterimages` 2456).

### 4.4 The hit on the target (`Impact`, BattleScreen.cs:2694-2822)

- `weight` = damage / max HP, clamped 0.05-0.6 (2703). `big` = crit, weak spot, heavy skill or big style (2719).
  `quick` = a hit in the middle of a combo (2721).
- Mark by weapon: sword crescent slash (alternating tilt), axe near-upright cleave, dagger/wand stab, hammer/staff
  impact burst (2724-2733); sparks and the element burst on top (2743-2744).
- **Hit stop** (2766-2767): `quick` 40 ms; crit 150; big 130; else `min(150, 70 + weight*120)`. **Capped at 70 ms
  unless the monster is a boss**, so trash fights keep moving.
- **Shake** (2768): 90 ms (140 crit), size `(0.002 + weight * 0.011) * 1280` px: about 3 px for a scratch to 11 px
  for a 60% hit (halved weight factor for combo hits).
- **Kick** (2769-2770): `4 + weight*14 (+4 crit)` px along the blow (x0.5 in combos), 1-2 px down.
- Crit: camera zoom to 1.03 in 60 ms and back in 180 ms (2749).
- Target: white flash 100 ms (140 big) (2763); knocked back `18 + weight*70 (+20 crit)` px in 60 ms CubicOut, tilted
  `6 + weight*12` deg, lifted `10 + weight*30` px on big hits, then slides home in 300 ms after 50 ms (2779-2798);
  squash to `0.86 - weight*0.12` wide / `1.08 + weight*0.08` tall over 260 ms (2801); rocked back, head snapping after
  (2803); its HUD jolts (2804); blood amount by weight (2809).

### 4.5 Finishers, Grand, combos, skills

- **`Chain`** (BattleScreen.cs:72): `First`, `Last`, `Finisher` (the blow that ends the fight), `Grand` (a finisher
  on a boss, or on the hero: the full show).
- **Grand wind-up** (WeaponBlow 2101-2115): backdrop dims to 0.35 in 180 ms, camera leans in to 1.08 in 200 ms,
  anticipation +60 ms and hold +60 ms, power 1, a glint at the blade tip with a "glint" sound.
- **`FinishingBlow`** (2829-2846): hit stop 170 ms (Grand) / 120; Grand adds slow motion 0.3x for 950 ms, undim over
  600 ms, zoom to 1.2 in 140 ms on the one going down; screen flash 100 ms (alpha 0.55 / 0.35); a second slash, crit
  flare, ground ring, "FINISH!".
- Enemy death: hit stop 260 ms boss / 90 trash (1962).
- **Combo hits after the first** (AttackAnim 2488-2517): step back 32 px in 50 ms, in again in 50 ms (QuadIn), contact,
  follow 12 px in 60 ms, hit stop 40 ms. That's a ~150 ms hit: the rhythm for multi-hits.
- **`heavy` skills** (2553-2609): anticipate 180 + 80 ms, leap 340 ms on a parabola 150 px high (90 posed), squash
  1.22/0.8 on landing, Shake(0.12, 6), Kick(0, 6), held 160 ms (300 posed), recover 340 ms with a 22 px hop.
- **`bolt` spells** (2610-2683): drawn up tall, 20 px back in 120 ms, charge 300 ms (520 big), staff spins / wand
  cocks, cast, projectile 420 ms (260 in combos, 520 big), recoil Kick(-3, 0), recover 240 ms.
- **`SkillCharge`** (2303-2332): dim 0.25, crouch and strain, cast circle, power gathers to the hand 380 ms (520
  heavy), glint at the tip.
- Other shakes for scale: boss intro 300 ms / 9 px (347); a body landing 160 ms / 7 px (2190); a stone monster
  shattering 180 ms / 8 px (2253); a big spell 180 ms / 8 px (2674); hero defeated 250 ms / 8 px (2012).

### 4.6 The bones today (Test/LayeredHero.cs, SkeletonSwing.cs, rig.json)

- **Parts and draw order** (`unity/Bones/hero/rig.json`, the tracked copy of `art/redo/layers/parts/rig.json`; `z` x 10 = sorting order, LayeredHero.cs:279):
  far upper arm 10, far forearm 8, far hand 9.4, far weapon 9.2; far thigh 22, far shin 20, far foot 21; hips 30;
  near thigh 42, near shin 40, near foot 41; torso 50; head 60; near forearm 70, **near weapon 71**, near hand 72,
  near upper arm 74. The weapon sits between the near forearm and the near fist, so the fist closes over the grip.
- **Order swap exists:** `Pose.FarFront` (LayeredHero.cs:40, 248) lifts the far forearm to 76, far hand to 78, far
  weapon to 77: the far arm comes in front of the body (use it when the free hand crosses in front, or a two-hander
  is held in front). In `Mix` it snaps at the halfway point (180).
- **Two hands on one weapon exists:** `Pose.On / OnT / OnBend` (LayeredHero.cs:40, 231-244): the far fist is placed
  on the near weapon's shaft, `OnT` px along it, by IK. The staff poses use it.
- **Legs** are IK from hip to ankle every frame, feet planted (LayeredHero.cs:218-228). `SkeletonSwing` adds IK
  arms: the sword hand goes where the key says, `Blade` sets the weapon angle (SkeletonSwing.cs:114-126).
- **Gear slots on parts** (rig.json `slots`): chest = torso + both upper arms + both forearms; legs = hips, thighs,
  shins, feet; boots = shins + feet; helm = head. Hands are bare (no gloves set yet). Faces `shout` and `hurt` exist
  bare and once per helm (fight.json `faces`: 2 x 10 pictures).
- **Weapons on the rig:** `iron_sword`, `oak_staff`, `rusty_dagger` (rig.json `weapons`), with grip points
  (`grips.n`, `grips.f`).
- **Poses on file** (poses.json): rest; sword, staff, dagger each ready / wind / strike; sword_left_ready.
- **SkeletonSwing's blow** (SkeletonSwing.cs:168-181): ready -> wind 300 ms, coil 60, strike 80, follow 80, held 280,
  back 360 = **1.16 s**, too long for the everyday blow (the drawn one is 0.76 s). Blade ghosts every frame from
  coil to just after the follow, each fading in 120 ms.
- **Two hands today:** the staff poses use `On` (far fist on the shaft, `t` -220 / -200 / -160 for ready / wind /
  strike) with `far_front` on; the dagger poses hold two daggers; the sword poses one (poses.json).
- **What the test rig can't do yet** (so cards marked "needs rig code" need these first):
  - `SkeletonSwing.Build` always puts `iron_sword` in the near hand (SkeletonSwing.cs:117); no far weapon.
  - `SkeletonSwing.Key` has no `FarFront`, `On`, `OnT`, `OnBend` (they exist only on `LayeredHero.Pose`), so
    two-handed holds (staff, axe, hammer), the far hand stabbing in front, and hand slides need these added.
  - No reverse grip: the hand angle is worked out from `Blade` and the grip angle (SkeletonSwing.cs:120-122); a
    reverse grip needs a per-key "grip flipped 180" flag.
  - `LayeredHero.Put` sets position, turn and order, never scale (LayeredHero.cs:275-280), and `Places` uses the
    fixed joints from rig.json (263-273): torso squeeze, shoulder slide, part stretch and the whole-puppet squeeze
    and mirror all need a scale/offset per part (or on the root) added.
  - No events (contact, sound, trail on/off, freeze, camera) and no curves per key beyond the three eases in
    SkeletonSwing.cs:163-165.
- **Known pop sources in the test code:** `SkeletonSwing.Mix` snaps `NBend`/`FBend` (elbow bend side) to the next
  key at the start of a segment, and swaps the face at the halfway point (line 135); `Arc` swings the sword hand round
  a fixed point (-40, -200) from the waist (line 140) that ignores the torso's turn and the root's move, so in a big
  lunge the hand's path drifts off the shoulder. `LayeredHero.Mix` snaps `FarFront` at halfway.

### 4.7 Gear counts (for the cost of a swap picture)

From `src/data`: body armor 12 (items.ts; PLANS: 11 have an outfit, Travel Garb shows the plain shirt), helm 9,
gloves 9, legs 9, boots 9 (equipment.ts). Weapons: 14 daggers, 11 staffs, 10 swords, 2 axes, 2 wands, 1 hammer.
On the rig today only two chest pieces are cut into per-part pictures (`chest_leather`, `chest_scale` in
rig.json `sets`); the others exist as whole outfit pictures. So "with every gear piece" costs assume the other
chest pieces get cut into parts too.

## 5. Try first

Ranked for tonight. Moves that need **no new picture** come first.

1. **Sword S1 diagonal cut, on Anim time, 740 ms**, with successive breaking (1.3) and the free fist kept as a guard
   at the chest. Fixes the two loudest complaints (stiff, "switches hands") with no art. Judge it against today's
   drawn blow (0.76 s, 4.3).
2. **Fake twist on the bones** (3.3: torso squeeze, shoulder slide, hips against shoulders, head lag) added to S1.
   Decides whether the T-away / T-toward pictures are even needed before anyone draws them.
3. **Sword S2 rising cut and S3 lunge thrust**: the two alternates; with S1 they give three paths that cycle.
4. **Sword S6 leaping overhead**: the first showpiece; uses only what exists (shout face, dim, zoom, freeze, flash).
5. **Dagger D1 quick stab and D2 double stab** (0 pictures; check the reverse grip by turning the weapon 180 deg).
6. **Staff St1 overhead bonk** with the hand sliding on the shaft (`OnT`) and the bounce: playful, 0 pictures.
7. **Staff St4 point and release**: the everyday cast on bones, reusing today's bolt.
8. **Dagger D5 vanish and reappear** or **D6 delayed burst**: cheap, very showy.

Note: items 5-8 need the rig code listed in 4.6 (weapon per move, `On`/`OnT`/`FarFront` per key, root scale).
That code is one small step that unlocks all of them; do it once, before the first non-sword move.
9. **If 2 did not reach the drawn twist**: draw T-away and T-toward bare (2 pictures) and compare S1 with and
   without them, side by side. Gear versions (22 more) only after a yes.
10. **F-grit** face (1 bare, 10 with helms) for the coils, if the calm wind-up face still shows.
11. **Axe A1, hammer H1, wand W1**: each needs its rig weapon picture first (rig.json has only the iron sword, oak
    staff and rusty dagger), then they reuse the sword's and staff's machinery.

## 6. Sources

Found with web search tonight (2026-11-17). Numbers marked *(tune)* in the text are mine, not from these.

1. https://game.capcom.com/cfn/sfv/column/131545?lang=en : hit stop is "for the visual performance"; example
   light/medium/heavy 8/12/16f; shorter per hit on multi-hit moves.
2. https://feelframes.dev/mechanics/hit-stop : measured hit stop in several games (9/11/13f light/medium/heavy in
   one fighting game; about 110 ms on a light melee hit in a 2D action game).
3. https://gamedesignerx.com/blog/hit-feedback-in-frames-designing-impact-with-hit-stop-shake-and-knockback :
   layered hit feedback table (what fires on frame 0, what after the freeze), starting hit stop by attack class,
   separate attacker/target freeze.
4. https://cobomakesgames.com/blog/how-to-make-combat-feel-good : one game's real numbers: 16 px pull, 4f sword
   freeze, 5f heavy, 8f target flash, shake inside the freeze, tilt 1.3 deg per px pushed.
5. https://www.dustloop.com/w/Using_Frame_Data : startup / active / recovery as frames at 60 fps.
6. https://media.gdcvault.com/gdc2016/Presentations/Eiserloh_Squirrel_JuicingYourCameras.pdf : camera shake =
   trauma squared, smooth noise instead of random, translation plus rotation in 2D.
7. https://theses.fh-hagenberg.at/system/files/pdf/Lendenfeld18.pdf : smear frames and motion trails in games
   (trail brighter than the weapon, follows the path).
8. https://www.gdquest.com/library/juicy_attack : anticipation, a smear only on the forward swing, easing.
9. https://media.gdcvault.com/gdcchina14/presentations/833784_MarielCartwright_PowerfulAndEffective_EN.pdf :
   even one frame of anticipation, overshoot by one frame, smears, "breaking the body", followthrough fills gaps.
10. https://tdgamestudio.com/blog/frame-by-frame-principles-of-game-animation : squash and stretch only on impact
    frames and landings; too much exposes rig deformation.
11. https://media.gdcvault.com/GDC2014/Presentations/Cartwright_Muriel_Animation_Bootcamp_Fluid.pdf : favour the
    keys, followthrough, smears, overshoot, a frame of delay gives strength.
12. https://www.slynyrd.com/blog/2025/5/23/pixelblog-56-top-down-character-attack-animation : phases
    (anticipation, smear, rebound, follow-through, recover), per-weapon ms timings, no smear on anticipation or
    recovery, a rebound frame for the hammer.
13. https://www.gameanim.com/2019/05/15/the-12-principles-of-animation-in-video-games/ : anticipation versus
    responsiveness; follow-through holds strong poses; player can act before the follow-through ends.
14. https://www.rivalslib.com/workshop_guide/art/anticipation_action_recovery : reach poses fast and hold them;
    direction shown in the anticipation; recovery stays clear, not a slow blend to idle.
15. https://github.com/flossbud/sakurai-game-design/blob/main/study-guide/06-animation.md : notes from a game
    director's talks: contact frame = strongest pose, "too much is just right", pull the windup back and hold, keys
    in 1 frame, follow-through on its own path, the first hurt frame an extreme pose.
16. https://www.jasondeheras.com/gamedesign/2021/4/23/how-do-3rd-person-melee-combat-games-communicate-game-and-hit-feel :
    hit frames, hit pause tiers, slowdown, camera zoom and shake as feedback.
17. https://github.com/claude-dev-suite/claude-dev-suite/blob/main/skills/gamedev/2d-art/animation-frames/quick-ref/attack-anticipation.md :
    anticipation length by attack weight (80-120 / 120-200 / 200-350 ms / charged 400 ms-1 s).
18. https://animlog.net/weightsword : heavy weapon motion: the torso twist leads and pulls the weapon up, lift
    slow and swing fast, long stiffness after the hit, key poses for a heavy chop.
19. https://scholarvictoria.com/2016/02/25/back-to-basics-2-guards-and-cuts/ ,
    https://www.hema101.com/post/liechtenauer-s-longsword-101-part-3-guards and
    http://dawnduellists.blogspot.com/2014/09/longsword-class-notes-five-meisterhaus.html : real sword cuts: guards
    as start, through and end positions; the rear hand turns the blade, the front hand is the centre; the step
    lands out and behind the cut at full extension.
20. https://hroarr.com/article/basic-meyer-quarterstaff-series/ : staff power from hip and shoulder rotation,
    little from the arms.
21. https://weaponsofchoice.com/the-textbook-of-stage-combat/foundations-of-staged-violence/quarterstaff-stage-combat :
    staff thrust travels along its own length, like a cue, with both hands.
22. https://www.perlego.com/book/2061486/timing-for-animation-40th-anniversary-edition-pdf : a heavy hammer is kept
    balanced over the feet until it falls (classic timing book, excerpt).
23. https://animperia.com/learn/rig-a-2d-character : pivots at joint centres, round overlapping ends, draw order,
    the wiggle test.
24. https://mediascaleup.com/post/a-practical-guide-to-2d-character-rigging-motion : overdraw every joint, rigs
    break at extreme poses, IK for planted hands, FK for swings.
25. https://docs.godotengine.org/en/stable/tutorials/animation/cutout_animation.html : cut-out rig in an engine,
    pivots, moving a part behind its parent mid-animation, IK chains.
26. https://lostmarble.net/forum/viewtopic.php?t=34969 : professional cut-out animators switch drawings for body
    turns and hide the switch in the motion.
27. http://en.esotericsoftware.com/spine-basic-concepts : bones, slots, attachments; draw order is a list of slots.
28. https://en.esotericsoftware.com/spine-json-format : what a skeleton file holds: slots, IK (mix, bend), skins,
    events, timelines with stepped / linear / bezier curves, draw order keys.
29. http://en.esotericsoftware.com/spine-slots : one visible attachment per slot; a weapon slot holding many weapons.
30. https://docs.unity3d.com/Packages/com.unity.2d.animation@7.0/manual/ex-sprite-swap.html (and
    https://learn.unity.com/article/how-to-animate-2d-characters) : the engine's own sprite swap: libraries of
    categories and labels, a resolver per renderer, full-skin swap by changing the library; limb IK solvers.
31. https://anim.works/strong-silhouette-combat-animation-moss : two-handed weapon holds, hands sliding to absorb
    weight then to the end for reach, weapon and body as one silhouette, recovery proves the cost.
32. https://aztez.com/blog/2014/01/06/anatomy-of-a-successful-attack : the swing itself lasts no more than about
    6 frames; layered hit effects; both fighters freeze on contact.

33. https://cursogame.dev/blog/pixel-art-animacao-ataque : the smear draws the path, not the weapon; it must join
    the coil pose to the contact pose; one short frame; bright leading edge, see-through tail; a trail after it
    fading over 2-3 frames.

From memory (not looked up tonight): "successive breaking of joints", "line of action", arcs and silhouette
from classic animation teaching; the trail-strip technique in 1.6; the faking-depth tricks in 3.3 (torso squeeze,
shoulder slide, depth by size and brightness); the silence (150-300 ms) and hang-time (120-250 ms) numbers in 1.8
and 1.10; all move cards in section 2 are my own designs built on the sources' principles.
