#!/usr/bin/env python3
"""A long, unattended Qwen run for the asset update (2026-11), built from the game's data plus the lists below. `all` is
what the game needs now (about 155 prompts); the groups under LATER wait for a decision and run by name. Uses the redo_art.py setup (local ComfyUI, the round-15 style references) and writes tries to
art/redo/<key>/<key>_s<seed>.png like redo_art.py, so `redo_art.py sheet` and `redo_art.py pick key:seed` work on them.
Files that exist are skipped, so a stopped run picks up where it left off.

  python scripts/asset_batch.py all                 # every group, 2 seeds each (hours)
  python scripts/asset_batch.py prop wpn --seeds 1  # some groups
  python scripts/asset_batch.py --list              # write the whole list to art/BATCH_LIST.md
  python scripts/asset_batch.py --dry vfx           # print the prompts
  python scripts/asset_batch.py hero_outfits        # stage 2: outfit poses, after base poses exist (not in `all`)

Now: prop (things drawn in code today), vfx (effect sprites), hero (new base-hero poses), npc (a talk pose and a
portrait each), wpn (the six weapons that read badly), mon (wind-up, strike and hurt for five Chapter 1 monsters).
Later: wpn_all, mon_all, npc_idle, blunt (maces and hammers), flask (poison flask, oils), bg (rooms for a bigger map),
helm (helms on the hero), unique (boss item icons).
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import redo_art as R  # noqa: E402
from art_tests import write_notes  # noqa: E402
from hero_edits import on_white  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = R.ROOT
DATA = json.loads((ROOT / "unity" / "Assets" / "Resources" / "Data" / "game.json").read_text(encoding="utf-8"))
FIRST_SEED = 7100  # apart from the redo's 4242, so old tries are neither skipped nor overwritten
# `all` is only what the game needs now (rated 2026-11-10). The rest waits for a decision and runs by name.
ALL = ["prop", "vfx", "hero", "npc", "wpn", "mon"]
LATER = ["wpn_all", "mon_all", "npc_idle", "blunt", "flask", "bg", "helm", "unique"]
# The 40 weapons were looked at on one sheet: 34 read fine. These six don't, and why.
WEAK_WEAPONS = {
    "iron_sword": "It must not look like a training sword: a short, broad, leaf-shaped iron blade with a round pommel.",
    "steel_broadsword": "A wide heavy blade with a deep central groove and a broad brass crossguard: clearly a broadsword.",
    "tempered_longsword": "A long slender two-handed blade with a blue-tempered sheen, a long grip and a ring guard.",
    "stormcleaver": "Clearly an axe, not a sword: a long haft with one broad crescent axe blade at the top.",
    "galecutter": "A legendary curved dagger: a sweeping crescent blade like a gust of wind, with a feathered guard.",
    "larchwood_staff": "A fine staff: pale larch wood ending in a carved spiral crook that cradles a clear crystal.",
}
# Monster poses are unproven: a first set from Chapter 1 to judge before the other 26 monsters.
FIRST_MONSTERS = ["mossling", "thornback_boar", "ash_imp", "bramble_bandit", "thornheart"]

OBJECT_STYLE = (
    "Drawn in exactly the same art style as the creatures in <image1>: clean thin dark outlines, flat colours with "
    "hard-edged cel shadows, matte surfaces with no glossy shine, a warm golden rim light, simple readable shapes and "
    "minimal detail. Use <image1> only as an example of the art style: draw none of the creatures or objects shown in "
    "it, only the subject described here.")
PROP_FRAME = ("Seen from the side at eye level, as an object standing on the ground in a side-view 2D game. The whole "
              "object visible, centered, with empty margin around it. Nothing else in the picture: no ground, no shadow, "
              "no background, no characters, no text.")
ICON_STYLE = (
    "Drawn in a clean 2D cartoon game-icon style: clean thin dark outlines, flat colours with hard-edged cel shadows, "
    "matte surfaces with no glossy shine, a warm golden rim light, simple readable shapes and minimal detail, muted "
    "warm colours.")
VFX_STYLE = (
    "A 2D game visual effect sprite in a bold flat cartoon style: clean hard-edged shapes in two or three flat tones "
    "with a thin dark outline, like cut paper. No soft glow, no blur, no gradients, no smoke haze, no realistic "
    "rendering. Centered, the whole effect visible with empty margin around it. Nothing else in the picture: no "
    "character, no creature, no ground, no background, no text.")
KEEP_HERO = (
    "Keep everything else exactly the same: the same young hero with messy dark brown hair and the red headband with "
    "tails, the same face, the same clothes and colours, the same body size and proportions, the same flat art style "
    "with hard-edged shadows, thin dark lines and a warm golden rim light. The whole figure inside the picture. No "
    "weapon, nothing in his hands unless said. No ground, no shadow, no text.")
KEEP_FIGURE = (
    "Keep exactly the same {what}: the same design, face, clothing, colours and markings, the same size and "
    "proportions, facing the same way, the same flat art style with hard-edged shadows, thin dark lines and a warm "
    "golden rim light. The whole figure stays inside the picture. No ground, no shadow, no background, no text.")

# --- prop: what the rooms draw in code today (campfire triangle, bones, the thorns bar, the snow blob, arrows) ---
PROPS = {
    "prop_campfire": "A small campfire: a ring of grey stones, three crossed logs and bright orange and yellow flames.",
    "prop_campfire_b": "The same kind of small campfire a moment later: a ring of grey stones, three crossed logs, the "
                       "orange and yellow flames leaning the other way with two sparks above.",
    "prop_campfire_out": "A burnt-out campfire: a ring of grey stones around charred black logs and pale ash, one thin "
                         "wisp of smoke.",
    "prop_bones": "A small pile of old bleached animal bones with one antlered deer skull on top.",
    "prop_thorn_wall": ("3:2", "A tall impassable wall of tangled black thorny briars with a few sickly green glowing "
                        "buds, as wide as a gate."),
    "prop_snowdrift": ("3:2", "A big heaped snowdrift blocking a path, taller than a person, with a few dark rocks and a "
                       "broken fence post sticking out."),
    "prop_reed_wall": ("3:2", "A dense tall wall of marsh reeds and cattails tied with rope into a barrier."),
    "prop_rockfall": ("3:2", "A heap of fallen grey boulders and rubble blocking a mountain path."),
    "prop_fallen_log": ("3:2", "A huge fallen mossy tree trunk lying across a path, with mushrooms on its bark."),
    "prop_signpost_right": "A weathered wooden signpost with one arrow-shaped board pointing to the right, blank board.",
    "prop_signpost_left": "A weathered wooden signpost with one arrow-shaped board pointing to the left, blank board.",
    "prop_signpost_up": "A weathered wooden signpost with one arrow-shaped board pointing up and away, blank board.",
    "prop_trapdoor": "A wooden trapdoor with an iron ring set in a low frame of stones, a rope ladder going down.",
    "prop_chest": "A sturdy wooden treasure chest with iron bands and a brass lock, closed.",
    "prop_chest_open": "A sturdy wooden treasure chest with iron bands, its lid thrown open, gold coins heaped inside.",
    "prop_mimic_chest": "A wooden treasure chest that is secretly a monster: closed, but with a row of small teeth "
                        "along the lid's edge and one sly eye peeking through the lock.",
    "prop_herb_patch": "A patch of wild healing herbs with small white flowers growing from a tuft of grass.",
    "prop_ore_vein": "A grey boulder split open to show a vein of glittering copper ore.",
    "prop_mushroom_ring": "A ring of big red-capped spotted mushrooms on a mossy mound.",
    "prop_lichen_rock": "A frosted boulder covered in pale blue-green lichen tufts.",
    "prop_bog_lilies": "A cluster of pale pink bog lilies on broad green pads.",
    "prop_notice_board": "A wooden notice board on two posts with a small shingle roof, a few pinned paper notices.",
    "prop_crates": "A stack of three wooden crates and a small barrel tied with rope.",
    "prop_tally_board": "A rough plank board nailed to a post, scratched with rows of tally marks.",
    "prop_note_stake": "A wooden stake in the ground with a folded paper note pinned to it by a knife.",
    "prop_shrine_stone": "A waist-high weathered standing stone carved with a simple spiral, a few offerings at its foot.",
    "prop_offering_bowl": "A shallow stone offering bowl holding coins, shells and a candle stub.",
    "prop_rune_pillar": "A tall narrow ice-rimmed stone pillar carved with faintly glowing blue runes.",
    "prop_footprints": "A short trail of large clawed footprints pressed into snow, seen at a low angle.",
    "prop_sprouting_stump": "An old tree stump with a single bright green sapling sprouting from its middle.",
    "prop_cart_wheel": "A broken wooden cart wheel leaning against a spilled sack of grain.",
    "prop_steam_vent": "A cracked rocky vent in the ground with a curling plume of white steam.",
    "prop_ice_block": "A big block of clear blue ice with a dim dark shape frozen inside.",
    "prop_tent": "A small patched canvas hunter's tent with a bedroll and a drying rack of pelts beside it.",
}

# --- wpn: every weapon again, each with its own silhouette; the tier and element set how it is dressed ---
TIER_WORDS = {1: "plain, worn and humble", 2: "well-made and sturdy, with one clear feature",
              3: "fine and master-crafted, with one bold ornament", 4: "legendary, with an unmistakable shape"}
ELEMENT_WORDS = {
    None: "Plain steel, wood and leather, no magic.",
    "fire": "Ember-orange and charred-black accents and a faintly glowing hot edge.",
    "water": "Deep blue and sea-green accents with wave shapes.",
    "ice": "Pale blue and white accents with frost crystals.",
    "wind": "Light grey-green accents with feather and swirl shapes.",
    "earth": "Moss-green and brown accents with stone, bark and root shapes.",
    "energy": "Yellow and violet accents with jagged lightning shapes.",
    "light": "White and gold accents with a small sunburst.",
    "dark": "Deep purple and black accents with thorn shapes.",
}
KIND_WORDS = {"sword": "sword", "axe": "battle axe", "hammer": "two-handed war hammer", "dagger": "dagger",
              "staff": "tall wizard's staff", "wand": "short magic wand"}
BLUNT = {
    "wpn_cobble_mace": "A plain, humble one-handed mace: a rounded river cobble bound to an oak handle with leather "
                       "straps and iron nails.",
    "wpn_millwrights_maul": "A sturdy two-handed maul with a square iron-banded wooden head, like a millwright's "
                            "mallet grown huge.",
    "wpn_bog_iron_mace": "A well-made flanged mace of dark bog iron, six blunt flanges, a reed-wrapped grip, deep blue "
                         "and sea-green accents.",
    "wpn_tidebreaker": "A fine two-handed war hammer with a head shaped like a breaking wave in blue-grey steel and a "
                       "long ash haft bound with rope.",
    "wpn_frostknuckle_mace": "A fine one-handed mace whose head is a fist-sized lump of blue glacier ice in a steel "
                             "cage, frost on the haft.",
    "wpn_avalanche_maul": "A legendary two-handed maul: a great wedge of snow-capped grey mountain rock on a long "
                          "dark haft with fur wrapping.",
}

# --- flask: gear answers anyone can carry (docs/BALANCE_SPEC.md rule 8) ---
OIL_WORDS = {"fire": "glowing orange oil with a tiny flame on the cork", "water": "clear blue oil with a wave label",
             "ice": "pale frosty blue oil, the glass rimed with frost", "wind": "swirling pale grey-green oil",
             "earth": "thick moss-green oil with a leaf label", "energy": "crackling yellow oil with a lightning label",
             "light": "bright white-gold oil that shines softly", "dark": "inky purple-black oil"}
FLASKS = {"icon_poison_flask": "A round glass throwing flask of bubbling sickly green poison, a cork stopper tied "
                               "with string and a small skull tag."}
FLASKS.update({f"icon_oil_{e}": f"A small squat bottle of weapon oil: {w}, a cork stopper and a cloth rag tied to its "
                               "neck." for e, w in OIL_WORDS.items()})

# --- vfx: effect sprites (the game's effects are shapes generated in code today) ---
VFX = {
    "fx_slash_a": "A single wide crescent sword-slash arc in white with a pale yellow edge, sweeping left to right.",
    "fx_slash_b": "A long thin diagonal sword-slash streak in white with pale blue edges.",
    "fx_slash_heavy": "A thick heavy overhead chop arc in white and orange, with three small chips flying off.",
    "fx_stab": "A narrow straight thrust streak ending in a small four-point star, white and pale yellow.",
    "fx_claw": "Three parallel curved claw-rake marks in white with red edges.",
    "fx_bite": "Two rows of jagged white fangs snapping shut, upper and lower, with a red burst between.",
    "fx_impact": "A spiky comic-book impact burst: a white many-pointed star with a yellow rim.",
    "fx_impact_blunt": "A blunt crushing impact: a white ring crack with chunky fragments flying outward, grey and "
                       "yellow.",
    "fx_crit": "A big dramatic comic impact star in white, yellow and red with long sharp points.",
    "fx_block": "A round blue shield-shaped flash with a white rim and two small sparks bouncing off.",
    "fx_miss": "A light grey curved whoosh line with a small puff at its end.",
    "fx_fire_burst": "A burst of flames: several pointed tongues of fire in red, orange and yellow spreading from a "
                     "centre.",
    "fx_fire_bolt": "A fireball flying to the right: a round orange and yellow core with a trailing pointed tail.",
    "fx_water_burst": "A splash of water: a crown of blue droplets and curled waves in deep blue and white.",
    "fx_water_bolt": "A ball of water flying to the right with a trailing ribbon of droplets, deep blue and white.",
    "fx_ice_burst": "A burst of sharp ice shards in pale blue and white radiating from a centre.",
    "fx_ice_bolt": "A long pointed icicle lance flying to the right, pale blue with a white streak.",
    "fx_wind_burst": "A swirl of wind: three curled grey-green ribbons spiralling around a centre with small leaves.",
    "fx_wind_bolt": "A crescent blade of wind flying to the right, pale grey-green with a white edge.",
    "fx_earth_burst": "A burst of rocks and clods: angular brown and grey stones flying up from a centre with green "
                      "leaf bits.",
    "fx_earth_bolt": "A jagged brown boulder flying to the right with a short trail of pebbles.",
    "fx_energy_burst": "A burst of lightning: jagged yellow and violet bolts forking out from a white centre.",
    "fx_energy_bolt": "A single jagged lightning bolt running left to right, yellow with a violet edge.",
    "fx_light_burst": "A sunburst of holy light: a white disc with long gold rays of two lengths.",
    "fx_light_bolt": "A slim beam of white-gold light flying to the right with a small star at its tip.",
    "fx_dark_burst": "A burst of dark wisps: curling purple-black tendrils around a small violet core.",
    "fx_dark_bolt": "A purple-black orb flying to the right with a ragged trailing tail.",
    "fx_poison_cloud": "A puff of sickly green poison gas made of three rounded lobes with small bubbles.",
    "fx_spores": "A loose cloud of round yellow-green spores of different sizes drifting.",
    "fx_blood_splat": "A liquid splat seen from the side in pure white: a main blob with teardrop drops flying off "
                      "to one side. Pure white only, so it can be tinted.",
    "fx_blood_pool": "A flat puddle seen at a low angle in pure white with an irregular rounded edge. Pure white "
                     "only, so it can be tinted.",
    "fx_dust_puff": "A small low puff of dust made of four rounded lobes, pale tan.",
    "fx_smoke_puff": "A rounded puff of smoke made of five lobes, light grey with a darker underside.",
    "fx_sparkle": "A single four-pointed sparkle star, white with a pale yellow centre.",
    "fx_stars_stun": "Three small yellow five-point stars in an arc, as if circling a dazed head.",
    "fx_heal": "A rising cluster of green plus signs and two small leaves, bright green and white.",
    "fx_shield": "A translucent pale blue dome shield seen from the side with a white hex-pattern rim.",
    "fx_buff_up": "Three stacked upward-pointing chevron arrows, orange and yellow.",
    "fx_debuff_down": "Three stacked downward-pointing chevron arrows, purple and blue.",
    "fx_shockwave": "A flat ground shockwave ring seen at a low angle, white with a yellow inner edge.",
    "fx_levelup": "A tall column of golden light with rising stars and a ring at its base.",
    "fx_coin": "A single gold coin seen at a slight angle with a simple leaf emblem.",
    "fx_confetti": "A scatter of small paper confetti pieces in red, yellow, blue, green and white.",
    "fx_snowflake": "A single six-armed snowflake, white with pale blue edges.",
    "fx_leaf": "A single falling leaf, green turning yellow at the tip.",
    "fx_ember": "A single glowing ember spark, orange with a yellow centre and a tiny tail.",
    "fx_freeze": "A jagged block of ice big enough to encase a creature, pale blue with white cracks.",
    "fx_chain": "A dark iron chain of six links stretched in a line with a small padlock.",
    "fx_charge_ring": "A ring of small pointed light wedges all aimed inward at an empty centre, white and gold.",
}

# --- hero: new poses of the base hero (edits of art/raw/char_hero_bare.png); outfits follow in `hero_outfits` ---
HERO_POSES = {
    "pose_idle": "a relaxed standing pose: weight on one leg, both arms hanging loosely at his sides with open hands, "
                 "shoulders down, a calm friendly look",
    "pose_idle_b": "a relaxed standing pose: one hand on his hip, the other arm hanging loosely, head turned a little "
                   "toward the viewer",
    "pose_walk_a": "a walking stride to the right: the leg nearer the viewer forward, the other leg back, arms "
                   "swinging naturally in the opposite way",
    "pose_walk_b": "a walking stride to the right: the leg farther from the viewer forward, the other leg back, arms "
                   "swinging naturally in the opposite way",
    "pose_talk": "a friendly talking pose: standing upright, one open hand raised at chest height in a small gesture, "
                 "mouth slightly open as if speaking",
    "pose_cheer": "a victory pose: one fist punched high into the air, the other fist at his hip, a wide grin",
    "pose_hurt": "a hurt pose: down on one knee, one hand pressed to his side, head lowered, teeth gritted",
    "pose_down": "a defeated pose: collapsed and lying on the ground on his side, eyes closed, one arm stretched out",
    "pose_sit": "a resting pose: sitting on the ground cross-legged, leaning forward with both hands held out as if "
                "warming them at a fire",
    "pose_drink": "a drinking pose: standing with his head tipped back, drinking from a small round red potion bottle "
                  "held in one hand",
    "pose_read": "a reading pose: standing and holding an open book in both hands at chest height, looking down at it",
    "pose_gather": "a gathering pose: crouching low on one knee and reaching down toward the ground with one hand",
    "pose_wave": "a greeting pose: standing upright with one hand raised high in a wave, smiling",
    "pose_point": "a pointing pose: standing and pointing forward to the right with a straight arm, a determined look",
    "pose_guard": "a guarding pose: feet planted wide, both forearms crossed in front of his chest and face, bracing "
                  "against a blow",
}
OUTFIT_POSES = ["pose_idle", "pose_walk_a", "pose_walk_b", "pose_talk", "pose_cheer", "pose_hurt", "pose_sit"]

NPC_POSES = {
    "talk": "now talking: one open hand raised at chest height in a small gesture, mouth slightly open as if speaking",
}
NPC_LATER = {
    "idle2": "in a second idle moment: weight shifted to the other leg, head turned a little toward the viewer, eyes "
             "closed in a blink",
}
MON_POSES = {
    "wind": "rearing back and coiled tight, body tensed and drawn away, about to strike",
    "strike": "lunging forward at full stretch in the middle of its attack, jaws, claws or weapon thrust out ahead",
    "hurt": "recoiling from a heavy blow: flinching backward, body bent away, eyes squeezed shut",
}

# --- bg: rooms for the bigger map (docs/PLANS.md, "A more intricate map"), plus the same pattern for the other regions ---
FLOOR = "The bottom 30% of the image is flat, open walkable ground running across the full width."
BGS = {
    "bg_moss_glade": "A sunny round forest glade ringed by old oaks, the ground soft with thick green moss and dotted "
                     "with mushrooms and small burrow mounds, shafts of light through the leaves.",
    "bg_mill_stream": "A narrow forest path beside a fast stream with a ruined water mill, its broken wheel half in "
                      "the water, mossy stones and ferns, dimmer and wilder than a road.",
    "bg_imp_den": "A scorched hollow in the forest: blackened tree stumps, ash on the ground, small smouldering "
                  "cracks glowing orange, a dark cave mouth in a burnt bank at the back.",
    "bg_root_tunnel": "A secret tunnel under the forest: the walls and ceiling are huge twisted tree roots, glowing "
                      "pale mushrooms light the way, a little daylight far ahead.",
    "bg_boar_run": "A trampled forest track churned to mud, rooted-up earth, snapped saplings and thorn bushes on both "
                   "sides, tusk-scarred tree trunks.",
    "bg_lily_shallows": "A wide still shallows in a marsh under a grey sky, carpets of lily pads and pink bog lilies, "
                        "low wooden walkway planks, reeds at the edges.",
    "bg_toad_wallow": "A muddy marsh wallow with fat bubbling mud pools, toadstools, drooping willows and half-sunk "
                      "logs in drizzle.",
    "bg_wreck_den": "The broken hull of an old river barge stranded in the marsh, ribs showing, a dark opening in its "
                    "side, ropes and barnacles, storm clouds above.",
    "bg_reed_maze": "A narrow hidden path winding between walls of reeds twice a person's height, lantern posts with "
                    "faded cloth strips, mist.",
    "bg_wolf_run": "A snowy mountain meadow crossed by many paw tracks, wind-bent dark pines, a frozen brook, grey "
                   "peaks behind. " + R.WINTER,
    "bg_ram_ledges": "Broad stepped rock ledges on a snowy mountainside with tufts of frosted grass, a sheer drop and "
                     "far white peaks behind. " + R.WINTER,
    "bg_ice_cellar": "A cave of blue ice under a glacier: smooth ice walls, hanging icicles, a frozen pool, old "
                     "wooden props and a dark passage at the back. " + R.WINTER,
    "bg_frozen_falls": "A hidden frozen waterfall in a mountain cleft, a curtain of pale blue ice columns with a "
                       "narrow gap behind it, snow-laden dark pines. " + R.WINTER,
}

# --- helm: the nine helms on the hero's head (edits of the base hero), a first step to gear you can see ---
# --- unique: icons for one named piece per boss (docs/PLANS.md review, item 5; the items themselves are not designed yet) ---
UNIQUES = {
    "icon_unique_heartwood_charm": "A pendant: a knot of dark heartwood with a small green glowing seed at its centre "
                                   "on a braided cord.",
    "icon_unique_millstone_ring": "A chunky ring carved from grey millstone with a tiny iron crab claw as its setting.",
    "icon_unique_bogmother_veil": "A hood of dark green woven marsh weed hung with small pale shells and one glowing "
                                  "blue bead.",
    "icon_unique_stormcoil_band": "An armband of coiled copper wire shaped like a serpent biting its tail, small "
                                  "sparks along it.",
    "icon_unique_snowmaw_mantle": "A heavy white fur mantle with a clasp made of two crossed ice-blue fangs.",
    "icon_unique_frostbinder_circlet": "A thin circlet of clear ice with three upright frost crystals and a pale blue "
                                       "gem.",
    "icon_unique_bandit_gloves": "A pair of patched dark leather gloves with brass knuckle studs and a red wrist "
                                 "wrap.",
    "icon_unique_impling_boots": "A pair of small pointed golden boots with tiny wings at the heels and a coin "
                                 "buckle.",
}


def size_of(aspect: str, long_side: int = 1536) -> tuple[int, int]:
    a, b = (int(x) for x in aspect.split(":"))
    w, h = (long_side, long_side * b / a) if a >= b else (long_side * a / b, long_side)
    return round(w / 32) * 32, round(h / 32) * 32


def raw(key: str) -> Path:
    return R.RAW / f"{key}.png"


def entries(group: str) -> dict[str, tuple[str, tuple[int, int] | None, list[str], str]]:
    """{key: (prompt, size or None for an edit, refs, one-line subject)}. A ref is "@name" (a style picture in
    art/redo/_style) or a path to a picture."""
    out: dict = {}
    if group == "prop":
        for key, v in PROPS.items():
            aspect, subject = v if isinstance(v, tuple) else ("1:1", v)
            size = (1024, 1024) if aspect == "1:1" else size_of(aspect)
            out[key] = (f"{RGBA_OPEN} {subject} {OBJECT_STYLE} {PROP_FRAME} {RGBA_CLOSE}", size, ["@monsters.png"], subject)
    elif group in ("wpn", "wpn_all"):
        for it in DATA["items"]:
            if it.get("type") != "weapon" or (group == "wpn" and it["id"] not in WEAK_WEAPONS):
                continue
            kind = KIND_WORDS.get(it.get("kind"), it.get("kind") or "weapon")
            subject = (f"A single {kind} for a fantasy RPG, named \"{it['name']}\": {it.get('description', '')} It is "
                       f"{TIER_WORDS.get(it.get('tier', 1), TIER_WORDS[1])}. {ELEMENT_WORDS[it.get('element')]} Give it "
                       f"a strong, distinctive silhouette that reads at a glance and cannot be mistaken for another {kind}. "
                       f"{WEAK_WEAPONS.get(it['id'], '')}".strip())
            out[it["art"]] = (f"{RGBA_OPEN} {subject} {OBJECT_STYLE} {R.WPN_FRAME} {RGBA_CLOSE}", (864, 1536),
                              ["@monsters.png"], subject)
    elif group == "blunt":
        for key, subject in BLUNT.items():
            out[key] = (f"{RGBA_OPEN} {subject} {OBJECT_STYLE} {R.WPN_FRAME} {RGBA_CLOSE}", (864, 1536), ["@monsters.png"], subject)
    elif group in ("flask", "unique"):
        for key, subject in (FLASKS if group == "flask" else UNIQUES).items():
            out[key] = (f"{RGBA_OPEN} {subject} {ICON_STYLE} {R.ICON_FRAME} {RGBA_CLOSE}", (1024, 1024), [], subject)
    elif group == "vfx":
        for key, subject in VFX.items():
            out[key] = (f"{RGBA_OPEN} {subject} {VFX_STYLE} {RGBA_CLOSE}", (1024, 1024), [], subject)
    elif group == "hero":
        for key, pose in HERO_POSES.items():
            out[key] = (f"{RGBA_OPEN} Edit <image1>. Change only his pose: the hero is now in {pose}. {KEEP_HERO} {RGBA_CLOSE}",
                        None, [str(raw("char_hero_bare"))], pose)
    elif group == "hero_outfits":
        for outfit in R.OUTFITS:
            for pose in OUTFIT_POSES:
                base = R.REDO / pose / f"{pose}_s{FIRST_SEED}.png"
                dressed = raw(f"char_hero_{outfit}")
                if not base.exists() or not dressed.exists():
                    continue
                text = (f"{RGBA_OPEN} Edit <image1>. Change only the clothing: the hero now wears exactly the outfit shown "
                        f"in <image2> ({R.OUTFITS[outfit]}). Keep the pose, the body, the face, the hair and the red "
                        "headband of <image1> exactly the same, in the same place and at the same size, the same flat art "
                        f"style. No weapon. No ground, no shadow, no text. {RGBA_CLOSE}")
                out[f"{pose}_{outfit}"] = (text, None, [str(base), str(dressed)], f"{pose} in {outfit}")
    elif group in ("npc", "npc_idle"):
        arts = sorted({n["look"]["art"] for room in DATA["rooms"] for n in (room.get("npcs") or []) if n.get("look", {}).get("art")})
        for art in arts:
            if not raw(art).exists():
                continue
            keep = KEEP_FIGURE.format(what="character")
            for name, pose in (NPC_POSES if group == "npc" else NPC_LATER).items():
                out[f"{art}_{name}"] = (f"{RGBA_OPEN} Edit <image1>. Change only the pose: the character is {pose}. {keep} {RGBA_CLOSE}",
                                        None, [str(raw(art))], f"{art}: {name}")
            if group == "npc_idle":
                continue
            text = (f"{RGBA_OPEN} A close-up head-and-shoulders portrait of exactly the same character as in <image1>: the "
                    "same face, hair, clothing and colours, the same flat art style with hard-edged shadows, thin dark "
                    "lines and a warm golden rim light. Three-quarter view facing right, a calm friendly expression, the "
                    "head and shoulders filling most of the square, cut off cleanly below the chest. Nothing else: no "
                    f"background, no frame, no text. {RGBA_CLOSE}")
            out[f"portrait_{art[4:]}"] = (text, (1024, 1024), [str(raw(art))], f"{art}: portrait")
    elif group in ("mon", "mon_all"):
        for m in DATA["monsters"]:
            art = m.get("look", {}).get("art")
            if not art or not raw(art).exists() or (group == "mon" and m["id"] not in FIRST_MONSTERS):
                continue
            keep = KEEP_FIGURE.format(what="creature")
            for name, pose in MON_POSES.items():
                out[f"{art}_{name}"] = (f"{RGBA_OPEN} Edit <image1>. Change only the pose of the creature: it is {pose}. {keep} {RGBA_CLOSE}",
                                        None, [str(raw(art))], f"{m['name']}: {name}")
    elif group == "bg":
        for key, subject in BGS.items():
            R.NEW[key] = ("16:9", f"{subject} {FLOOR} No characters, no creatures.")
            text, size, refs = R.build(key, {})
            out[key] = (text, size, ["@" + r for r in refs], subject)
    elif group == "helm":
        for it in DATA["items"]:
            if it.get("slot") != "helm":
                continue
            subject = f"{it['name']}: {it.get('description', '')}"
            text = (f"{RGBA_OPEN} Edit <image1>. Change only his head: the hero now wears a helm, the {subject} His face "
                    f"stays visible. {KEEP_HERO} {RGBA_CLOSE}")
            out[f"helm_{it['id']}"] = (text, None, [str(raw("char_hero_bare"))], subject)
    else:
        sys.exit(f"Unknown group: {group}. Groups: {', '.join(ALL + LATER)}, hero_outfits")
    return out


def write_list() -> None:
    lines = ["# Asset batch (scripts/asset_batch.py)", "",
             "Every prompt of the long Qwen run, by group. Tries land in `art/redo/<key>/`; review with "
             "`python scripts/redo_art.py sheet <keys>` and take one with `python scripts/redo_art.py pick <key>:<seed>`.", ""]
    total = 0
    for group in ALL + LATER + ["hero_outfits"]:
        e = entries(group)
        total += len(e)
        lines += [f"## {group} ({len(e)}){'' if group in ALL else ' - later, not in `all`'}", ""]
        lines += [f"- `{key}`: {subject}" for key, (_, _, _, subject) in e.items()]
        lines.append("")
    lines.insert(3, f"{total} prompts listed (`hero_outfits` only lists poses whose base pose exists).")
    (ROOT / "art" / "BATCH_LIST.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"art/BATCH_LIST.md: {total} prompts")


def main() -> None:
    args = sys.argv[1:]
    if "--list" in args:
        write_list()
        return
    seeds = 2
    if "--seeds" in args:
        i = args.index("--seeds")
        seeds = int(args[i + 1])
        del args[i:i + 2]
    dry = "--dry" in args
    groups = [a for a in args if not a.startswith("--")]
    if not groups:
        sys.exit(__doc__)
    if "all" in groups:
        groups = ALL
    todo: dict = {}
    for g in groups:
        todo.update(entries(g))
    if dry:
        for key, (text, size, refs, _) in todo.items():
            print(f"\n{key} {size} {[Path(r).name for r in refs]}\n{text}")
        print(f"\n{len(todo)} prompts")
        return
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    uploaded: dict[str, str] = {}

    def ref(r: str) -> str:
        if r not in uploaded:
            path = R.STYLE / r[1:] if r.startswith("@") else Path(r)
            uploaded[r] = comfy.upload(f"drakepg_batch_{path.stem}.png", on_white(path))
        return uploaded[r]

    done = 0
    print(f"{len(todo)} prompts x {seeds} seeds", flush=True)
    for key, (text, size, refs, _) in todo.items():
        for i in range(seeds):
            seed = FIRST_SEED + i
            out = R.REDO / key / f"{key}_s{seed}.png"
            if out.exists():
                continue
            out.parent.mkdir(parents=True, exist_ok=True)
            try:
                out.write_bytes(comfy.generate(text, [ref(r) for r in refs], size, seed, 30))
            except Exception as e:  # keep going: one bad prompt or a hiccup shouldn't end a night's run
                print(f"  {key} s{seed}: {e}", flush=True)
                continue
            write_notes(out.with_suffix(".txt"), comfy.last_graph)
            done += 1
            print(f"{done} {out.relative_to(ROOT)}", flush=True)
    print(f"done: {done} new images", flush=True)


if __name__ == "__main__":
    main()
