# Spire assets: what the tower needs, how each piece moves, and the rules that keep it in the pipeline

For the Spire (`docs/TOWER_SPEC.md`). Written 2026-10-27 (designer: "make a documentation of needed assets and generate them; take notes of how it should be animated so the assets fit that pipeline"). One list of every asset the tower shows, what makes it, its status, where it is drawn and how it moves; then the pipeline rules each kind must follow, so later art (or animation) drops in without redoing it.

## The list

| Key | Kind | Made by | Status | Shown | How it moves |
| --- | --- | --- | --- | --- | --- |
| `bg_spire_foot` | Room background | Qwen, `scripts/redo_art.py` (NEW) | Done (seed 4243) | Spire Foot | Still picture; `dust` motes drift (room `ambient`). Later: the two door lanterns flicker (glow anchors below). |
| `npc_spire_warden` | NPC sprite | Qwen, `redo_art.py` (NEW) | Done (seed 4242) | Spire Foot, x 830, beside the door | Stands; hops when you talk to him (`NpcHop`). Built to take the bend rig later (rules below). Later: his staff lantern glows. |
| `bg_spire_thornwood` | Battle background | Qwen, `redo_art.py` (NEW) | Done (seed 4243): roots through the stone, green lanterns | Floors 1-10 (`FlightDef.background`) | Still picture, raised 90 px under the battle HUD. Fighters bend and move in front of it. |
| `bg_spire_fen` | Battle background | Qwen | Done (seed 4242): a flooded hall; fighters stand ankle-deep at the pool's edge | Floors 11-20 | Same. |
| `bg_spire_frostfang` | Battle background | Qwen edit of `bg_spire_thornwood` (`scripts/spire_frost.py edit`) | Done (edit seed 4242) | Floors 21-30 | Same. |
| `mark` | UI icon | Hand SVG, `scripts/ui_icons.py` | Done | Spire header (vendor prices and the result screen can use it next) | Still; the result screen counts the marks up. |
| `spire` | UI icon | Hand SVG | Done | Not placed yet: the map's Spire badge or a floor-list tab, later | Still. |
| `cache` | UI icon | Hand SVG | Done | Vendor: Sigil cache rows | Still. |
| `twist_unshakable`, `twist_short_fuse`, `twist_no_potions`, `twist_veil`, `twist_shell`, `twist_ambush` | UI icons | Hand SVG | Done | Floor cards, the battle panel's twist lines (a Shell twist shows as the game's shell line instead) | Still. |
| `twist_hulking`, `twist_frenzied`, `twist_warded`, `twist_venomous`, `twist_ironhide` | UI icons (the Elite affixes as twists) | Hand SVG | Done | Same as above (Elite lines in battle can use them later) | Still. |
| Fury, Hardened, Regrowth | Twists that start a status | Existing `status_war_cry`, `status_iron_skin`, `status_regen` | Reused | Same as above, plus the status chip in battle | Still. |
| Twist effects in battle | Particles and glows | Code (`Fx`), no art | Notes only (below) | Battle | See "Twist effects". |

The ice hall took three rounds (designer: "the ice biome... needs rework, try to maintain the style"). Text alone first grew green hills inside the hall, then gave a rendered stone hall (smooth gradients, realistic blocks) that didn't read as paper. What worked: a **Qwen edit of the picked Thornwood hall** ("turn this hall into the frozen top hall... keep everything else exactly the same: the same layered paper-cut style"), so the paper layers, light and layout carry over; the frozen trees and icicles read as cut white paper. A new drawing with two finished cutout pictures as style references (`spire_frost.py refs`) also worked once in two tries. A style LoRA wasn't needed; it stays the fallback if a new biome can't be reached by editing a finished picture. **Rule for new backgrounds of a set: edit a finished, liked picture of the set rather than prompting from scratch.**

Review sheets of the tries are made with `redo_art.py sheet` (art/redo/_sheet.png) and `ui_icons.py sheet`; the first icon draft of Veil, Shell and Venomous read as a "C", a car and a pendant at small size and were redrawn (a ring with a bright gap, a shield with three charges, two fangs with a drop).

Each twist names its icon in the data (`TwistDef.icon` in `src/data/tower.ts`); a missing icon falls back to the star, so new twists never block on art.

## Pipeline rules, by kind

### Characters (the Warden; any later tower NPC or monster)

The game draws one picture per character and moves it in code. In battle every fighter bends on three soft bones (`World/Bend.cs`: breathing, weight shift, the head lagging behind the body, a lean into blows, a crouch before a leap, hair and headbands trailing); in rooms NPCs stand still and hop. "Bend in rooms" (NPCs breathing, walking) is planned, so NPC art follows the battle rules too:

- **One full-body picture**, transparent background (`RGBA` prompt wrapper), upright, **feet at the bottom center**, the whole figure inside the canvas with a margin. No ground, no shadow, no base: the game draws the shadow.
- **Faces right** (`Sprites face right`; the game flips it toward the player).
- **Size**: drawn at 1536 px tall, `prepare_art.py` trims it to 960 px tall at @2x (the 1x figure is 480 px tall and scaled by `look.scale`).
- **Joints at fixed heights**: the rig bends at the **hips (50% of the figure's height)** and **shoulders (78%)** with a soft band of 7% around each. The figure's hips should sit near half its height and its shoulders near 78%; nothing important should cross those lines sideways (an arm held straight out shears when the body leans).
- **Props held close and upright** (the Warden's staff): they bend with the body. A prop that should stay rigid (a weapon) is a separate picture placed on hand anchors (`look.bare.hands`), like the hero's weapons.
- **Loose parts** (hair, beard, sash ends, coat tails) trail when the figure moves: keep them on the figure's back side, not crossing the face.
- **Key poses**, if a character ever gets them (the hero does: `scripts/hero_poses.py`): Qwen edits of the base picture at the exact same canvas, same scale and feet, so the swap doesn't jump.
- **Glow anchors**: anything that should glow (the Warden's lantern) is noted as a 1x pixel point on the trimmed picture, so `Props.Glow` can sit on it. Warden: the lantern at the staff's top, about (182, 62) of the 1x picture (to measure once the glow is built).

### Backgrounds

- **2560x1440** at @2x (`prepare_art.py`), the round-15 paper-cut diorama look (`BG_STYLE`), with one finished background as the only style reference ("none of its trees, hills or layout"). No characters, no text.
- **Room backgrounds** (Spire Foot): the walk band is the bottom 30% (design y 590-700): flat, open, no props in it. Exits sit where the art has a door or path: Spire Foot's way back is the bottom edge (a `down` exit), Brindlemoor's door to it is the cottage door at x 247. Lanterns, fires and windows are glow candidates: Spire Foot's two door lanterns sit at about x 552 and 728, y 432 (design px).
- **Battle backgrounds** (the three Spire halls): drawn 90 px higher than the screen (`BgRaise`), so the fighters' feet (design y 500) stand on art y 590. Keep the **floor band flat and empty from art y 500 to the bottom**, and the **middle third calm** (the hero stands near x 330, the monster near x 950, and the "Next:" tag and the monster's panel sit above them). Nothing bright or high-contrast right behind the fighters' heads. Interiors need the "the whole picture is the inside of the hall... no open sky" wording, or the style's sky leaks in (the caves taught that).
- Backgrounds don't move. Life comes from code on top: ambient particles (`dust`, `embers`, `fireflies`, `leaves`, `mist`, `rain`, `snow`), glows and the fighters.

### The monster's sheet in battle (2026-10-27)

Designer: "icons with resistance need overhaul, I see too much info on the screen at once." The sheet now holds two quiet rows instead of a line per fact: the monster's element, its weak spots and its resists as icons (`el_<element>`, `spot_blunt`, `spot_poison`, two short words between them), and a badge per rule it plays by (Elite affix, tower twists, signature move, Heroic move, loot). Hovering or tapping any icon or badge opens a tooltip right by it (`Ui/Tooltip.cs`: a bold title and a line or two, under the thing or above it near the bottom edge), e.g. "Weak to Water: Water hits deal 50% more damage (its Water resist is -50)." The status chips, the shell line and the floor list's twists use the same tooltip. A shell says its hits in words ("Ice Shell: 3 Ice hits to break it · 50% less damage"); the diamond pips were cryptic. Only what counts down during the fight keeps a line (a shell's pips, a Lucky Impling's escape). Twists and traits are still spelled out in the log when the fight starts.

### Tooltip wording (2026-10-27, designer: "too much text, be concise and to the point", settled with the design advisor)

- **A title and one short line**: the title names the thing, the line says the effect in under about 8 words ("Weak to Water" / "Takes 50% more from Water."). Like Slay the Spire's keyword pop-ups (from memory).
- **The effect, not the stat behind it**: "Takes 50% less from Fire", never the resist number.
- **No how-to that holds everywhere** ("any Ice hit counts: a skill or a weapon"): teach it once, not in every tooltip.
- **Parallel wording** so pairs scan together: "Takes 50% more from…" / "Takes 50% less from…".
- **A second sentence only if it changes a decision**: "Regrows at 50% HP." stays; item lists and roll explanations go (loot is one line of odds, plus luck only while it's working).
- **The line says the state, the tooltip says the rule**: "Ice Shell: 3 Ice hits to break" on the panel; "Takes 50% less until 3 more Ice hits. Regrows at 50% HP." on hover.

### UI icons

- **Never AI** (PLANS, Art: "the UI is never AI-generated"). Hand-written SVG in `scripts/ui_icons.py`: a 32x32 viewBox, ink `#2a1d16` outlines 2.4 wide, flat fills from the paper palette, the wobble filter. Rendered by headless Edge to 128 px PNGs: the ink set (`UiIcons/`) for sand and cream felt, the light set (`UiIconsLight/`, `--light`) for navy felt.
- **Reads at 18 px**: one silhouette, two or three colors, no thin detail. Check on a sheet (`python scripts/ui_icons.py sheet <out.png> twist_`).
- Icons don't move by themselves; panels pop them in (`UiMotion`).

## Twist effects (notes for when battle effects are built)

All code-made with `Fx` and its generated textures, no new art. Each must stay readable next to the existing hit, status and element effects, and none may hide the monster's "Next:" tag.

| Twist | Effect | Timing |
| --- | --- | --- |
| Element Veil | A thin ring of the veil element's color around the monster, slowly breathing (alpha 0.25-0.45, about 2 s); a flash of it when a resisted hit lands, a crack of white when the weak element lands. | Whole fight |
| Element Shell | The existing shell pips; each shell hit adds a small burst in the shell element's color; "breaks" with a shard burst; grows back with the existing banner line. | On hits |
| Short Fuse | A spark on a short fuse line under the monster's panel that burns down as the enrage turn nears; red embers rise off the monster once it enrages. | Turns before enrage |
| No Potions | The Items button greyed with a small crossed potion. | Whole fight |
| Unshakable | The existing "Immune" float when a stun or Freeze is shrugged off, plus a short grey ripple. | On stun tries |
| Ambush (Shock, Burn) | At the intro, the status's own burst on the hero as the monster arrives, then the usual chip. | Intro |
| Fury, Hardened, Regrowth | The status's burst on the monster at the intro (War Cry's red flare, Iron Skin's grey sheen, Regen's green motes). | Intro |
| Hulking | The monster drawn 10% bigger (like an Elite's size-up, without the gold glow). | Whole fight |
| Frenzied, Warded, Venomous, Ironhide | No extra effect: their lines and the hits say it. | |

## Making the assets

```
python scripts/redo_art.py bg_spire_thornwood bg_spire_fen bg_spire_frostfang --seeds 2   # tries, art/redo/
python scripts/redo_art.py sheet bg_spire_thornwood bg_spire_fen bg_spire_frostfang         # review sheet
python scripts/redo_art.py pick bg_spire_thornwood:4242 ...                                 # -> art/raw, src/assets/art
python scripts/unity_art.py                                                                  # -> unity/Assets/Resources/Art
python scripts/ui_icons.py mark spire cache twist_...   and again with --light               # the icons
```
