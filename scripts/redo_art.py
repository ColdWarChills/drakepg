#!/usr/bin/env python3
"""Redo the game's art in the round-15 look (docs/ART_PLAN.md): layered paper-cut backgrounds and clean cartoon
characters (thin dark outlines, hard cel shadows, warm rim light), with the round-15 pictures as style references.

The subject of each image comes from its old prompt (art/QUEUE.md, art/PROMPTS.md) with the old style text cut off,
or from NEW below for art that never had a prompt. Tries go to art/redo/<key>/<key>_s<seed>.png (+ .txt with the
settings); a pick is copied to art/raw/<key>.png and run through prepare_art.py.

  python scripts/redo_art.py bg_thornwood mon_mossling --seeds 2   # tries
  python scripts/redo_art.py --all-bg / --all-mon / --all-npc      # every key of a kind
  python scripts/redo_art.py sheet mon_mossling mon_ash_imp        # review sheet of the tries -> art/redo/_sheet.png
  python scripts/redo_art.py pick mon_mossling:4242 bg_thornwood   # copy picks to art/raw and prepare them
  python scripts/redo_art.py --dry bg_thornwood                    # print the prompt only
"""
import re
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from art_tests import write_notes  # noqa: E402
from hero_edits import on_white  # noqa: E402
from qwen_batch import RGBA_CLOSE, RGBA_OPEN, Comfy  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
REDO = ROOT / "art" / "redo"
RAW = ROOT / "art" / "raw"
PARTS = ROOT / "art" / "tests" / "themes" / "parts"
# Round-15 pictures that define the look (art/tests is git-ignored, so the picks are copied into art/redo/_style).
STYLE = REDO / "_style"
STYLE_SRC = {"hero.png": PARTS / "pick_hero.png", "boar.png": PARTS / "pick_boar.png", "bg.png": PARTS / "pick_bg.png",
             "hero_full.png": PARTS / "_raw" / "hero_s4243.png",
             # A redone monster in the look: monsters get two creatures as references (with the hero as one, humanoid
             # monsters came back with his head).
             "wolf.png": REDO / "mon_drift_wolf" / "mon_drift_wolf_s4243.png"}
# The picked base hero: armor outfits are edits of it at its exact canvas size, so prepare_art.py crops them with the
# same box and the fists stay where `look.bare.hands` says (the picture itself: art/raw/char_hero_bare.png).
HERO_BASE = RAW / "char_hero_bare.png"
FIRST_SEED = 4242

BG_STYLE = (
    "Made as a layered paper-cut diorama: every hill, tree, rock, building and cloud is cut from matte coloured card "
    "paper and stacked in flat layers, with soft shadows between the layers for gentle depth. Flat colours with a fine "
    "paper grain, clean cut edges, no outlines, big simple shapes with few small details. A soft, muted palette "
    "and gentle light, no vignette. The scene fills the whole picture edge to edge: a softly "
    "coloured sky with paper clouds across the top, the ground reaching the bottom edge, no white or blank paper, no "
    "border. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI."
)
BG_REF = ("Use <image1> only as an example of the paper-cut style, the palette and the light: none of its trees, "
          "hills or layout appear in this scene.")
SPRITE_STYLE = (
    "Drawn in exactly the same art style as <image1> and <image2>: clean thin dark outlines, flat colours with "
    "hard-edged cel shadows, a warm golden rim light, simple readable shapes and minimal interior detail, the same "
    "line weight and colouring, as if by the same artist for the same game. Use <image1> and <image2> "
    "only as examples of the art style: draw none of the characters or creatures shown in them."
)
# Monsters: one style reference, a sheet of four finished monsters (art/redo/_style/monsters.png). With two single
# characters as references, monsters came back with the hero's head or a wolf's body.
MONSTER_STYLE = (
    "Drawn in exactly the same art style as the creatures in <image1>: clean thin dark outlines, flat colours with "
    "hard-edged cel shadows, a warm golden rim light, simple readable shapes and minimal interior detail, the same line "
    "weight and colouring, as if by the same artist for the same game. Use <image1> only as an example of the art "
    "style: draw none of the creatures shown in it, only the subject described here."
)
PLAIN_STYLE = (
    "Drawn in a clean 2D cartoon style: clean thin dark outlines, flat colours with hard-edged cel shadows, a warm "
    "golden rim light, simple readable shapes and minimal interior detail."
)
MONSTER_REFS = "sheet"  # or "none" (--monster-refs)
ICON_REFS = "sheet"  # or "none" (--icon-refs)
ITEM_STYLE = (
    "Drawn in exactly the same art style as <image1> and <image2>: clean thin dark outlines, flat colours with "
    "hard-edged cel shadows, matte surfaces with no glossy shine, a warm golden rim light, simple readable shapes and "
    "minimal detail, as if by the same artist for the same game. Use <image1> and <image2> only as examples of the art "
    "style: draw none of the characters or creatures shown in them, only this one item."
)
WPN_FRAME = ("The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole "
             "weapon visible with empty margin around it. No hands, no character, no stand, no text.")
ICON_FRAME = ("Centered, seen from the front, the whole item visible and filling most of the square with a small empty "
              "margin. No hands, no character, no stand, no text.")

# Art with no old prompt (designs from docs/ART_PLAN.md and the game data). key: (aspect, subject, pose)
ROAD_FLOOR = "The bottom 30% of the image is a flat, open dirt road running across the full width."
# The Spire's battle halls: interiors need the "no sky" wording or the style's sky and hills leak in (the caves taught it).
SPIRE_HALL = ("The whole picture is the inside of the hall: grey stone walls on the left and right, a dark vaulted stone "
              "ceiling across the top; the outside shows only through the arched windows, there is no open sky above the "
              "hall. The bottom 30% of the image is a flat, open flagstone floor running across the full width, with "
              "nothing standing on it; the middle of the picture stays calm and open.")
NEW = {
    # Title screen: no dragon (docs/ART_PLAN.md); the upper middle stays calm for the logo.
    "bg_title": ("16:9", "Title screen scene at dusk: an old road winding through layered green hills toward a small "
                 "walled market town with glowing windows and red roofs in the valley, misty mountains behind, the first "
                 "stars and a big pale moon in a warm violet and amber sky, a lone signpost by the road. Keep the "
                 "upper-middle area calm and uncluttered for a game logo. No characters, no creatures."),
    "bg_old_road": ("16:9", "A quiet old country road between tall green hedgerows and a few crooked trees in the "
                    "morning, wildflowers along the verge, gentle meadows and rolling hills far behind. " + ROAD_FLOOR),
    "bg_road_cart": ("16:9", "A country road between hedgerows in the morning: on the right side of the road a "
                     "wooden farm cart lies tipped over on its side with one broken wheel, spilled sacks and a crate, "
                     "deep scratch marks in the hedge behind it; meadows and hills far behind. " + ROAD_FLOOR),
    "bg_road_mill": ("16:9", "An old country road past a disused stone watermill set back on the left, its big "
                     "wooden wheel still, a narrow stream under a little stone bridge, hedgerows and a low dry-stone "
                     "wall, tall grass and wildflowers. " + ROAD_FLOOR),
    "bg_road_gate": ("16:9", "The east gate of a small walled market town on the left side: a stout stone gatehouse "
                     "with a closed heavy wooden gate, a few faded banners, red tiled rooftops and a chimney peeking "
                     "over the town wall; on the right the country road arrives between hedges and a lone tree. "
                     + ROAD_FLOOR),

    "npc_odo": ("3:4", "A weathered middle-aged carter, stocky, sun-browned face with a grey stubble beard and a "
                "patient tired smile, a floppy brown felt hat, a faded tan shirt with rolled sleeves under a brown "
                "leather vest, patched trousers, a coil of rope over one shoulder, holding a wooden wagon spoke."),
    "npc_pell": ("3:4", "A young, nervous town gate watchman, lanky, freckles, short sandy hair under a dented "
                 "round steel cap, a dark blue tabard with a small yellow tower badge over a padded jacket, holding "
                 "a long spear a little too tightly, a horn on his belt."),
    "npc_guild_tamsin": ("3:4", "A brisk young Adventurers' Guild clerk woman, neat dark hair in a short bob, round "
                         "glasses, a dark green waistcoat with brass buttons over a cream blouse, ink-stained "
                         "fingers, a quill tucked behind her ear, holding a clipboard of notices."),
    "npc_guild_ivo": ("3:4", "A calm, lanky Adventurers' Guild clerk man from a marsh outpost, thin face, long "
                      "black hair tied back, a dark green waistcoat with brass buttons over a grey shirt, tall "
                      "waders, holding a rolled-up notice and a pencil."),
    "npc_guild_edda": ("3:4", "A sturdy older Adventurers' Guild clerk woman from a mountain village, silver hair in "
                       "two thick braids, red cheeks, a dark green waistcoat with brass buttons over a thick cream "
                       "wool sweater, fur-lined boots, holding a heavy ledger."),
    # The tower (docs/TOWER_SPEC.md): its room behind Brindlemoor and its Warden.
    "bg_spire_foot": ("16:9", "The foot of a tall, ancient grey stone tower beside a small market town: the round "
                      "tower base fills the middle of the picture and rises out of the top edge, a big arched wooden "
                      "door with iron bands at its foot, two iron lanterns either side of it, worn stone steps up to "
                      "the door, faded blue banners with a simple gold star hanging from the stones, ivy climbing the "
                      "walls; a low town wall and a few red rooftops on the left, green hills and pines behind. "
                      "The bottom 30% of the image is a flat, open cobbled square running across the full width."),
    # Battle halls inside the Spire, one per flight (docs/SPIRE_ASSETS.md): open floor, calm middle.
    "bg_spire_thornwood": ("16:9", "Inside the lowest hall of an ancient round stone tower: thick tree roots and moss "
                           "break through the grey stone walls and the cracks of the floor, two tall arched windows "
                           "let in green forest light, a few hanging lanterns of green glass, worn stone steps "
                           "spiralling up along the back wall. " + SPIRE_HALL),
    "bg_spire_fen": ("16:9", "Inside a middle hall of an ancient round stone tower, half flooded: a thin sheet of still "
                     "water lies over the flagstones, reeds and lily pads grow from cracks along the walls, damp moss "
                     "on the stones, blue-green light from tall arched windows, hanging lanterns mirrored in the water, "
                     "worn stone steps spiralling up along the back wall. " + SPIRE_HALL),
    # Winter needs the "no green" line (Frostfang rooms, 2026-10-19): the first tries grew green hills inside the hall.
    "bg_spire_frostfang": ("16:9", "Inside the top hall of an ancient round stone tower high in snowy mountains: "
                           "frost and long icicles on the grey stone walls and pillars, drifts of snow along the floor "
                           "edges under tall arched windows that show white peaks and a pale winter sky, cold blue "
                           "light, two iron braziers with small orange flames by the walls, worn stone steps rising "
                           "along the back wall. Only snow, ice, frosted grey stone and iron inside the hall: no trees, "
                           "no plants, no grass and no hills anywhere inside it. " + SPIRE_HALL),
    "npc_spire_warden": ("3:4", "A stern, tall old tower warden, short trimmed grey beard, a long slate-grey coat "
                         "with gold trim over dark chain mail, a faded blue sash with a small gold star, a ring of "
                         "heavy iron keys on his belt, holding a tall iron-shod staff topped with a small brass "
                         "lantern."),
    # Weapons from the first batch had one shared prompt template; their subjects, from the item text.
    "wpn_training_sword": ("9:16", "A single plain, slightly blunt short training sword with a simple iron crossguard "
                           "and a leather-wrapped grip, shown alone as a game item."),
    "wpn_worn_staff": ("9:16", "A single worn wooden apprentice staff, a simple straight shaft with a knotted top and "
                       "a small cloth wrap, shown alone as a game item."),
    "wpn_rusty_dagger": ("9:16", "A single short dagger with a slightly rusty blade, a plain crossguard and a cord-"
                         "wrapped grip, shown alone as a game item."),
    "wpn_iron_sword": ("9:16", "A single sturdy iron sword with a straight blade, a squared crossguard and a leather "
                       "grip, shown alone as a game item."),
    "wpn_oak_staff": ("9:16", "A single oak staff with a curled top holding a small glowing blue stone, shown alone as a game item."),
    "wpn_tusk_dagger": ("9:16", "A single dagger whose curved blade is carved from an ivory boar tusk, on a leather-"
                        "wrapped hilt, shown alone as a game item."),
    "wpn_hunting_knife": ("9:16", "A single hunting knife with a broad single-edged steel blade and an antler handle, "
                          "shown alone as a game item."),
    "wpn_cinder_fang": ("9:16", "A single dagger with a blade of black volcanic glass glowing faintly orange along its "
                        "edge, on a dark hilt, shown alone as a game item."),
    "wpn_emberbrand": ("9:16", "A single steel sword whose edge glows ember-orange, small sparks drifting off it, a "
                       "blackened crossguard, shown alone as a game item."),
    "wpn_ember_wand": ("9:16", "A single short charred wooden wand with a glowing red-orange coal set in its tip, shown alone as a game item."),
    "wpn_thornheart_blade": ("9:16", "A single sword carved from dark living wood, thorns along the back of the blade "
                             "and a small green glowing heartwood gem in the crossguard, shown alone as a game item."),
    "icon_recall_scroll": ("1:1", "A single rolled parchment scroll tied with a red ribbon and a small wax seal shaped like a "
                           "little house, a faint golden glow around it, shown alone as a game inventory icon."),
    "icon_stone_of_might": ("1:1", "A single smooth river stone with a glowing red-orange rune of a sword carved into it, "
                            "shown alone as a game inventory icon."),
    "icon_stone_of_precision": ("1:1", "A single smooth river stone with a glowing yellow rune of a target eye carved into "
                                "it, shown alone as a game inventory icon."),
    "icon_stone_of_warding": ("1:1", "A single smooth river stone with a glowing blue rune of a shield carved into it, "
                              "shown alone as a game inventory icon."),
    "icon_stone_of_vigor": ("1:1", "A single smooth river stone with a glowing green rune of a heart carved into it, "
                            "shown alone as a game inventory icon."),
    "icon_briarberry": ("1:1", "A small cluster of dark purple-red berries on a thorny bramble twig with two leaves, shown "
                        "alone as a game inventory icon."),
    "icon_scorched_twig": ("1:1", "A single short, thick forked branch, charred black and cracked like charcoal, glowing "
                           "orange embers in the cracks and at its broken end, a thin wisp of smoke, lying diagonally "
                           "and filling most of the square, shown alone as a game inventory icon."),
    "icon_stolen_trinket": ("1:1", "A single small gold locket on a broken chain, its lid open a crack, shown alone as a "
                            "game inventory icon."),
    "icon_bog_lily": ("1:1", "A single pale white-green lily flower with a long dripping stem and a lily pad leaf, shown "
                      "alone as a game inventory icon."),
    "icon_drowned_coin": ("1:1", "A single old round coin crusted green with age, a worn face stamped on it, a few "
                          "water drops, shown alone as a game inventory icon."),
    "icon_storm_glass_shard": ("1:1", "A single jagged shard of pale blue glass with a frozen lightning streak inside it, "
                               "shown alone as a game inventory icon."),
    "icon_lichen_tuft": ("1:1", "A single tuft of frosty grey-green lichen torn from a rock, tiny ice crystals on it, "
                         "shown alone as a game inventory icon."),
    "icon_binding_rune": ("1:1", "A single chip of clear blue ice carved with one glowing pale blue rune, shown alone as "
                          "a game inventory icon."),
    "icon_cinder_cap": ("1:1", "A single small mushroom with a glowing orange-red cap speckled like embers on a dark "
                        "stem, shown alone as a game inventory icon."),
    "icon_frost_seal": ("1:1", "A single round white wax seal stamped with a snowflake sigil, a torn scrap of ribbon on "
                        "it, shown alone as a game inventory icon."),
    "mon_mimic": ("1:1", "A treasure chest monster: a sturdy wooden chest with iron bands that has sprung open into a "
                  "wide mouth of sharp teeth, a long purple tongue lolling out, two small glinting eyes under the "
                  "lid, a few gold coins spilling out, standing on stubby little wooden legs.", "menacing stance"),
    "mon_burrow_rat": ("4:3", "A small, plump field rat monster with grubby brown fur, oversized front teeth, big "
                       "digging claws caked in dirt, a bald pink tail and a twitchy nose, a clump of soil on its back.",
                       "side view"),
    "mon_dew_slug": ("4:3", "A small, soft garden slug monster, glossy pale green body with darker green spots, "
                     "two long eye stalks with sleepy eyes, a few dew drops beaded on its back and a shiny slime "
                     "trail at its tail.", "side view"),
    "mon_hedge_pixie": ("1:1", "A tiny mischievous hedge pixie, a thumb-sized sprite with a leafy green tunic, "
                        "pointed ears, a spiky hairdo of twigs and leaves, dragonfly wings, a cheeky grin, holding a "
                        "thorn like a little spear.", "hovering in the air"),
    "mon_stormcrest_heron": ("3:4", "A big marsh heron monster, long legs and a long sharp beak, slate-blue and "
                             "white feathers, a tall crest of feathers crackling with small bright blue lightning "
                             "sparks, wings half open, glaring yellow eyes.", "menacing stance, side view"),
    "mon_vent_tortoise": ("4:3", "A big tortoise monster from volcanic caves: a heavy domed shell of cracked black "
                          "lava rock with glowing orange seams and small puffs of steam from its vents, thick grey "
                          "scaly legs, a blunt beaked head with ember-orange eyes.", "menacing stance, side view"),
}


# The base hero (one for every class, docs/PLANS.md) redrawn from the round-15 hero with empty fists, so the game
# draws the weapon in his hands (`look.bare.hands`). key: pose
HERO_KEEP = ("exactly the same young hero as in <image1>: the same face, messy dark brown hair, red headband with "
             "tails, cream shirt with rolled sleeves, brown belt, dark teal trousers and brown boots, the same colours "
             "and art style (flat shapes with hard-edged shadows, thin dark lines, a warm golden rim light)")
HERO = {
    "hero_fists": ("standing in a relaxed ready stance, three-quarter view facing right, feet apart, both arms "
                   "hanging a little away from his body, both hands closed into empty fists at hip height with clear "
                   "space between each fist and the body, nothing in his hands"),
    "hero_raised": ("standing in a ready stance, three-quarter view facing right, feet apart; the arm on the right "
                    "side of the picture bent with its hand raised to chest height in front of him, closed into an "
                    "empty upright fist as if gripping a tall staff; the other arm hanging at his side with an empty "
                    "fist; nothing in his hands"),
}

# Body armor outfits on the base hero, `char_hero_<armor id>` (Traveler's Garb is the base clothes: no outfit).
OUTFITS = {
    "novice_robe": "a plain dusty-purple cloth robe down to the shins with a few pale protective stitches along the "
                   "hem, tied with a rope belt",
    "leather_armor": "a brown leather cuirass with stitched panels, leather shoulder guards and brass buckles over his "
                     "shirt, with leather bracers",
    "apprentice_robe": "a deep red apprentice robe with orange warding runes along the sleeves and hem, tied with a "
                       "cloth sash",
    "scale_mail": "a shirt of overlapping polished steel scales over a padded tunic, with steel shoulder guards and a "
                  "wide leather belt",
    "reedweave_robe": "a robe woven from dried green-gold marsh reeds in a basket-weave pattern with faint blue runes",
    "marshhide_vest": "a dark green oiled hide vest with bone toggles over a grey shirt, with hide bracers",
    "stormweave_robe": "a pale silver-blue silk robe with small yellow lightning zigzags stitched along the seams",
    "ridgeplate": "thick steel plate armor on his chest and shoulders with a white fur lining showing at the collar "
                  "and edges",
    "fleecewool_robe": "a thick cream wool robe with blue stitched warding runes and a fur collar",
    "frostpelt_vest": "a vest of white wolf pelt over boiled leather, with leather bracers",
    "galeweave_robe": "a light pale green robe with long flowing sleeves and a sash, its hem rippling as if in a breeze",
}


WINTER = ("Only snow, ice, snow-laden dark pines and bare grey rock: no green leafy trees, no grass, no bushes, no "
          "autumn colours. The bottom 30% of the image is flat packed snow running across the full width.")

# Old prompts whose subject changes in the redo. key: subject (aspect and pose stay).
FIXES = {
    # Traveler's Garb is the base hero's own clothes (no outfit art): the icon shows them.
    "icon_travel_garb": ("A single folded set of plain travel clothes: a cream linen shirt with rolled sleeves on top of "
                         "dark teal trousers and a brown leather belt, shown alone as a game inventory icon."),
    "icon_salamander_scale": ("A single large teardrop-shaped reptile scale, ember orange with a darker red rim and a "
                              "faint glow, no animal, shown alone as a game inventory icon."),
    # The outfit (char_hero_novice_robe) and the item colour are purple.
    "icon_novice_robe": ("A single plain dusty-purple cloth robe with a few pale protective stitches along the hem and a "
                         "rope belt, shown alone as a game inventory icon."),
    "wpn_snowmaw_cleaver": ("A single huge, heavy two-handed cleaver: a very wide, broad curved blade of white ice-hard "
                            "tusk, as wide as a shovel, bound to a thick wooden handle with fur and rope, shown alone as "
                            "a game item."),
    "wpn_sparkfang": "A single curved dagger made from a crackling yellow spur on a copper hilt, tiny sparks at the tip, "
                     "shown alone as a game item.",
    # Monsters whose first tries drifted toward the boar reference, or stood on a patch of ground.
    "mon_bog_lurker": ("A hulking swamp beast on four legs, part crocodile and part bear: a long flat crocodile snout "
                       "lined with jagged teeth, bumpy scaled back, a bear's heavy shoulders and broad clawed paws, matted "
                       "hide dripping dark green moss and mud, glowing teal eyes, reeds stuck to it. Not a pig or a boar: "
                       "no tusks, no pig nose."),
    "mon_bogmother": ("A huge, grotesque swamp matriarch boss monster: a massive bloated toad-like body of mud and moss, "
                      "many small glowing yellow eyes, a wide drooping mouth with hanging roots, tiny frogs and leeches "
                      "clinging to her sides, a crown of reeds and lily pads, menacing pose. She stands alone: no puddle, "
                      "no grass, no ground under her."),
    # Rooms whose first tries put water or lava where the hero walks (the bottom 30%).
    "bg_shrine_steps": ("Ancient worn stone steps at the left leading down into dark floodwater in the background, broken "
                        "statues with a coiled serpent carved on them, small offerings of coins on the top step, "
                        "sideways rain, lightning striking a stone far off. In the foreground the bottom 30% of the image "
                        "is a flat, dry paved stone terrace running across the full width, no water on it."),
    "bg_steamvent_caves": ("Inside a warm volcanic cave deep in a mountain. The whole picture is the cave: dark rock "
                           "walls on the left and right and a low rock ceiling with stalactites across the top. Glowing "
                           "orange hot pools and steam vents set back in the middle distance, a few red crystals. No sky, "
                           "no clouds, no trees, no plants anywhere. In the foreground the bottom 30% of the image is a "
                           "flat floor of solid dark rock running across the full width, no lava or water on it."),
    "icon_storm_scale": ("A single long slate-blue heron crest feather with small yellow lightning sparks crackling along "
                         "it, shown alone as a game inventory icon."),
    # The one old dragon stays in the story (docs/ART_PLAN.md, dragon clean-up): only a dim shape deep in the ice.
    "mon_spark_wisp": ("A floating ball of living lightning with no legs and no arms: a crackling bright yellow-white "
                       "core with a small mischievous face, violet electric tendrils trailing below it like a tail, tiny "
                       "arcs of lightning around it, hovering in the air."),
    "mon_mossling": ("A small, cute but mischievous moss creature monster: a round blob of green moss and lichen with "
                     "two big round yellow eyes with small dark pupils, tiny mushrooms and leaves growing on top, stubby "
                     "root-like feet."),
    # Frostfang rooms whose first tries came out with summer-green trees and meadows (the style reference is a green
    # forest): deep winter spelled out, landmarks where the exits and props are.
    "bg_windscour_ridge": ("A wind-blasted ridge high in the mountains in deep winter: snow streaming off the rocks, bare "
                           "icy boulders, a few snow-laden dark pines, a stormy grey sky, and in the exact middle of the "
                           "image a dark cave mouth in the cliff face with faint orange steam glowing inside, at the back "
                           "edge of the walkable ground. " + WINTER),
    "bg_rangers_lookout": ("A small wooden rangers' lookout hut half buried in snow on a mountain ledge in deep winter, "
                           "its door hanging open with deep claw marks, a cold fire pit, a lookout platform on stilts; "
                           "at the right edge, the dark mouth of a den between snowy cliffs. " + WINTER),
    "bg_snowmaw_den": ("A deep snowy hollow between grey cliffs in deep winter: huge gnawed bones half buried in snow, a "
                       "giant flattened nest of snow and white fur, icicles hanging from the rocks, cold grey light. "
                       + WINTER),
    "bg_roost_stair": ("Ancient wide stone steps cut into a snowy mountainside in deep winter, climbing toward the right, "
                       "tall pillars of blue ice with glowing runes on both sides, a cold blue light at the top right. "
                       + WINTER),
    "bg_hot_springs": ("Inside a dark rock cave deep in a mountain. The whole picture is the cave: dark rock walls on "
                       "the left and right and a low rock ceiling with stalactites across the top. A large steaming hot "
                       "spring pool with turquoise water set back in the middle, smooth warm rocks around it, soft orange "
                       "light from glowing volcanic vents in the back wall, steam drifting up, a few red crystals. No sky, "
                       "no clouds, no trees, no plants anywhere. In the foreground the bottom 30% of the image is a flat "
                       "floor of solid dark rock running across the full width, no water on it."),
    "bg_high_trail": ("A narrow high mountain trail along a cliff in deep winter: snow, wind-blown ice, chunks of broken "
                      "blue ice like shattered statues, a sheer drop with clouds below on the far side, a pale winter "
                      "sky. " + WINTER),
    # The eel was dropped as a sample, but it roams three Fen zones (Tough, absorbs Energy): it needs its picture.
    # Battle sprites stand on nothing, so it rears up out of nothing instead of a puddle.
    "mon_volt_eel": ("A big electric swamp eel monster rearing up in a tall S-curve like a cobra: a long slippery dark "
                     "teal body with a pale yellow belly, a row of glowing yellow spots along its sides, a ragged fin "
                     "along its spine crackling with small yellow lightning sparks, a wide flat head with small glowing "
                     "yellow eyes and an open mouth of needle teeth. No water, no puddle, no ground under it."),
    "bg_frozen_roost": ("A mountaintop roost: a vast frozen plateau under a blizzard sky, tall ice pillars carved with "
                        "faintly glowing blue runes, and behind them a towering wall of thick blue ice with a huge dark "
                        "shape sleeping deep inside it, only a curled horn and a closed eye faintly visible through the "
                        "ice. Only snow, ice and bare grey rock up here: no green trees, no plants. The bottom 30% of the image is "
                        "flat, open, snowy ground running across the full width."),
}


# Map pages (src/data/map.ts): one panorama per area behind the map's route, `bg_map_<area>`. Seen from high above like
# a paper-cut relief map, laid out in the direction the page's rooms run, with each end's landmark at its edge. No
# roads: the game draws the route on top.
MAP_STYLE = (
    "A wide illustrated map of a small region for a 2D fantasy RPG, seen from high above at a steep angle like a "
    "paper-cut relief map: every hill, tree clump, rock, pool and roof is cut from matte coloured card paper and "
    "stacked in flat layers, with soft shadows between the layers. Flat colours with a fine paper grain, clean cut "
    "edges, no outlines, big simple shapes with few small details, a soft muted palette and gentle even light. The "
    "land fills the whole picture edge to edge: no sky, no border, no frame, no compass, no roads, no paths, no "
    "bridges, no characters, no text, no letters, no labels."
)
MAPS = {
    "bg_map_road": ("Gentle green farmland divided by hedgerows into small fields, a few round trees and wildflower "
                    "meadows. At the far left edge the corner of a small walled town with red roofs. In the middle a "
                    "little stream with a stone watermill beside it. Toward the right more hedges and low rolling hills."),
    "bg_map_thornwood": ("A forest region. At the far left edge a small walled market town with red and brown roofs. "
                         "From there to the right a bright, friendly green forest of round trees with a few sunny "
                         "clearings and one giant fallen oak, growing darker, denser and thornier toward the right. "
                         "Near the right edge a wall of black thorny brambles, and at the far right a dark hollow "
                         "around one huge twisted dead tree with a faint green glow."),
    # Landmarks sit where their rooms do on the page (MapPanel lays the rooms out on a grid; fractions from the left).
    "bg_map_fen": ("A misty marsh region of reed beds, shallow green pools and mud islands with a few crooked trees. At "
                   "the far left edge, an old stone shrine half sunk in dark water under stormy violet mist. Around "
                   "two thirds from the left, a short line of broken old stone arches standing in the water. Around "
                   "four fifths from the left, a small village of wooden houses on stilts over the water. The marsh is "
                   "dark and stormy at the left and turns calmer and greener toward the right."),
    "bg_map_frostfang": ("A snowy mountain region seen from above. At the far left edge, a pass between snowy pine "
                         "forests. Around one fifth from the left, in the lower half, a small village of a few "
                         "snow-roofed log cabins with chimney smoke. Around one third from the left, higher up in the "
                         "upper half, a dark cave mouth in a grey cliff with white steam rising beside a small turquoise "
                         "hot spring. The middle and right half are wind-swept snowy ridges and rocky ledges, then ice "
                         "fields with tall blue ice crystals, climbing to the far right edge where a mountaintop of "
                         "glowing blue ice stands. Only snow, ice, pines and grey rock."),
}


def old_prompts() -> dict[str, tuple[str, str]]:
    """{key: (aspect, full old prompt)} from art/QUEUE.md and art/PROMPTS.md (QUEUE wins)."""
    found: dict[str, tuple[str, str]] = {}
    for name in ("PROMPTS.md", "QUEUE.md"):
        text = (ROOT / "art" / name).read_text(encoding="utf-8")
        for m in re.finditer(r"^### (?:[\w.]+\. )?`([a-z0-9_]+)` · (\d+:\d+)[^\n]*\n(?:(?!###)[^\n]*\n)*?```\n(.*?)\n```",
                             text, re.S | re.M):
            found[m.group(1)] = (m.group(2), m.group(3).strip())
    return found


def subject_of(key: str, old: str) -> tuple[str, str]:
    """The subject sentences of an old prompt, and its pose words for sprites."""
    cut = ("2D side-scrolling game background" if key.startswith("bg_") else
           "2D fantasy RPG item art" if key.startswith(("icon_", "wpn_")) else "2D fantasy RPG character art")
    subject = old.split(cut)[0].strip()
    pose = ""
    m = re.search(r"Full body, ([^.]*?), whole figure", old)
    if m:
        pose = m.group(1)
    return subject, pose


def build(key: str, prompts: dict) -> tuple[str, tuple[int, int], list[str]]:
    """(prompt, size, style refs) for a key."""
    if key.startswith("outfit_"):
        text = (f"{RGBA_OPEN} Edit <image1>. Change only the clothing: the hero now wears {OUTFITS[key[7:]]}. Keep "
                "everything else exactly the same: the same young hero with messy dark brown hair and the red headband, "
                "the same face, body, pose and proportions, both empty fists in exactly the same place, the same flat "
                "art style with hard-edged shadows, thin dark lines and a warm golden rim light. No weapon anywhere. "
                f"No ground, no shadow, no text. {RGBA_CLOSE}")
        return text, None, ["hero_base.png"]
    if key in MAPS:
        ref = ("Use <image1> only as an example of the paper-cut style, the palette and the light: none of its trees, "
               "hills or layout appear in this map.")
        return f"{MAPS[key]} {MAP_STYLE} {ref}", (2048, 768), ["bg.png"]
    if key in HERO:
        text = (f"{RGBA_OPEN} Full body, {HERO_KEEP}, {HERO[key]}. The whole figure visible with empty margin around "
                f"it. No sword, no weapon, no ground, no shadow, no background. {RGBA_CLOSE}")
        return text, (1024, 1536), ["hero_full.png"]
    if key in NEW:
        aspect, subject, *rest = NEW[key]
        pose = rest[0] if rest else "standing idle pose, three-quarter view"
    elif key in FIXES:
        aspect, subject = prompts[key][0], FIXES[key]
        pose = subject_of(key, prompts[key][1])[1]
    else:
        aspect, old = prompts[key]
        subject, pose = subject_of(key, old)
    a, b = (int(x) for x in aspect.split(":"))
    if key.startswith("wpn_"):
        # The monster sheet as reference: the hero holds a sword, and Qwen copies reference objects outright.
        style = ("Drawn in exactly the same art style as the creatures in <image1>: clean thin dark outlines, flat "
                 "colours with hard-edged cel shadows, matte surfaces with no glossy shine, a warm golden rim light, "
                 "simple readable shapes and minimal detail. Use <image1> only as an example of the art style: draw "
                 "none of the creatures or objects shown in it, only this one weapon.")
        return f"{RGBA_OPEN} {subject} {style} {WPN_FRAME} {RGBA_CLOSE}", (864, 1536), ["monsters.png"]
    if key.startswith("icon_"):
        # A sheet of four finished icons as the one reference: with the hero and boar, small items came back as boars.
        style = ("Drawn in exactly the same art style as the items in <image1>: clean thin dark outlines, flat colours "
                 "with hard-edged cel shadows, matte surfaces with no glossy shine, a warm golden rim light, simple "
                 "readable shapes and minimal detail. Use <image1> only as an example of the art style: draw none of the "
                 "objects shown in it, only this one item.")
        if ICON_REFS == "none":
            # No reference: any reference object (a tusk, berries, a boar) leaked into a share of the icons.
            style = ("Drawn in a clean 2D cartoon game-icon style: clean thin dark outlines, flat colours with "
                     "hard-edged cel shadows, matte surfaces with no glossy shine, a warm golden rim light, simple "
                     "readable shapes and minimal detail, muted warm colours.")
            return f"{RGBA_OPEN} {subject} {style} {ICON_FRAME} {RGBA_CLOSE}", (1024, 1024), []
        return f"{RGBA_OPEN} {subject} {style} {ICON_FRAME} {RGBA_CLOSE}", (1024, 1024), ["icons.png"]
    if key.startswith("bg_"):
        size = (2048, 1152)
        return f"A wide 16:9 background for a 2D side-view fantasy RPG. {subject} {BG_STYLE} {BG_REF}", size, ["bg.png"]
    if key == "mon_thornback_boar":
        # The round-15 boar is the accepted design: redraw it at full size, facing right.
        text = (f"{RGBA_OPEN} The big angry wild boar from <image2> alone: exactly the same creature, colours and art "
                "style (dark brown body, mossy green back, curved ivory tusks, flat shapes with hard-edged shadows, a "
                "warm golden rim light). Full body, standing on all four legs, side view facing right, the whole figure "
                f"visible with empty margin around it. No ground, no shadow, no background. {RGBA_CLOSE}")
        return text, (1536, 1024), ["hero.png", "boar.png"]
    long_side = 1536
    w, h = (long_side, long_side * b / a) if a >= b else (long_side * a / b, long_side)
    size = (round(w / 32) * 32, round(h / 32) * 32)
    pose = re.sub(r"\s*facing (right|left)", "", pose or "standing idle pose, three-quarter view")
    style, refs = SPRITE_STYLE, ["hero.png", "boar.png"]
    if key.startswith("mon_"):
        style, refs = (MONSTER_STYLE, ["monsters.png"]) if MONSTER_REFS == "sheet" else (PLAIN_STYLE, [])
    text = (f"{RGBA_OPEN} {subject} {style} Full body, {pose}, facing right, the whole figure visible with "
            f"empty margin around it. Nothing else in the picture: no ground, no shadow, no background, no text. "
            f"{RGBA_CLOSE}")
    return text, size, refs


def keys_of(prefix: str, prompts: dict) -> list[str]:
    art = {re.sub(r"(@\d+x)?\.webp$", "", p.name) for p in (ROOT / "src" / "assets" / "art").glob("*.webp")}
    data = "".join(f.read_text(encoding="utf-8") for f in (ROOT / "src" / "data").glob("*.ts"))
    used = art | set(re.findall(r"'((?:bg|mon|npc)_[a-z0-9_]+)'", data))
    used |= {f"icon_{i}" for i in re.findall(r"id: '([a-z0-9_]+)'", data)}
    used |= set(re.findall(r"art: '(wpn_[a-z0-9_]+)'", data))
    keys = sorted(k for k in set(prompts) | set(NEW) if k.startswith(prefix) and (k in used or k in NEW))
    drop = {"mon_volt_eel", "mon_stormscale_drakeling", "mon_vent_wyrmling", "bg_title"}
    return [k for k in keys if k not in drop]


def generate(keys: list[str], seeds: int, dry: bool) -> None:
    prompts = old_prompts()
    missing = [k for k in keys if k not in prompts and k not in NEW and k not in HERO and k not in MAPS
               and not k.startswith("outfit_")]
    if missing:
        sys.exit(f"No subject for: {', '.join(missing)}")
    if dry:
        for k in keys:
            text, size, refs = build(k, prompts)
            print(f"\n{k} {size} {refs}\n{text}")
        return
    STYLE.mkdir(parents=True, exist_ok=True)
    for name, src in STYLE_SRC.items():
        if not (STYLE / name).exists():
            shutil.copy(src, STYLE / name)
    comfy = Comfy("http://127.0.0.1:8188")
    comfy.check()
    uploaded = {name: comfy.upload(f"drakepg_redo_{name}", on_white(STYLE / name)) for name in STYLE_SRC}
    if (STYLE / "icons.png").exists():
        uploaded["icons.png"] = comfy.upload("drakepg_redo_icons.png", on_white(STYLE / "icons.png"))
    if (STYLE / "monsters.png").exists():
        uploaded["monsters.png"] = comfy.upload("drakepg_redo_monsters.png", on_white(STYLE / "monsters.png"))
    if HERO_BASE.exists():
        uploaded["hero_base.png"] = comfy.upload("drakepg_redo_hero_base.png", on_white(HERO_BASE))
    for key in keys:
        text, size, refs = build(key, prompts)
        for i in range(seeds):
            seed = FIRST_SEED + i
            out = REDO / key / f"{key}_s{seed}.png"
            if out.exists():
                continue
            out.parent.mkdir(parents=True, exist_ok=True)
            print(out.relative_to(ROOT), flush=True)
            try:
                out.write_bytes(comfy.generate(text, [uploaded[r] for r in refs], size, seed, 30))
            except RuntimeError as e:
                print(f"  ComfyUI error: {e}", flush=True)
                continue
            write_notes(out.with_suffix(".txt"), comfy.last_graph)


def trimmed(path: Path) -> Image.Image:
    img = Image.open(path).convert("RGBA")
    a = img.getchannel("A").point(lambda v: 0 if v < 10 else v)
    img.putalpha(a)
    box = a.getbbox()
    return img.crop(box) if box else img


def sheet(keys: list[str], out: Path) -> None:
    """Every try of each key in a row: sprites on a light and a dark tile, backgrounds as they are."""
    rows = []
    for key in keys:
        tries = sorted((REDO / key).glob(f"{key}_s*.png"))
        if tries:
            rows.append((key, tries))
    cell, pad, label = 360, 10, 26
    width = pad + max(len(t) for _, t in rows) * (cell * (16 / 9 if True else 1) + pad)
    width = int(width)
    img = Image.new("RGB", (width, len(rows) * (cell + label + pad) + pad), (30, 28, 34))
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arialbd.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    y = pad
    for key, tries in rows:
        x = pad
        for path in tries:
            draw.text((x, y + 2), f"{key} s{path.stem.rsplit('_s', 1)[1]}", fill=(235, 228, 210), font=font)
            tw = round(cell * 16 / 9)
            if key.startswith("bg_"):
                im = Image.open(path).convert("RGB").resize((tw, cell), Image.LANCZOS)
                img.paste(im, (x, y + label))
            else:
                tile = Image.new("RGB", (tw, cell), (214, 205, 182))
                ImageDraw.Draw(tile).rectangle((tw // 2, 0, tw, cell), fill=(58, 70, 52))
                fig = trimmed(path)
                s = min((tw // 2 - 12) / fig.width, (cell - 12) / fig.height)
                fig = fig.resize((max(1, round(fig.width * s)), max(1, round(fig.height * s))), Image.LANCZOS)
                for ox in (0, tw // 2):
                    tile.paste(fig, (ox + (tw // 2 - fig.width) // 2, cell - 6 - fig.height), fig)
                img.paste(tile, (x, y + label))
            x += tw + pad
        y += cell + label + pad
    img.save(out)
    print(out)


def find_grip(img: Image.Image) -> float:
    """Where the hand goes on a processed weapon (0 = top, 1 = bottom; `WeaponDef.grip`): the middle of the narrow
    rows in the bottom 40% (a sword's handle between guard and pommel, a staff's shaft near its foot)."""
    import numpy as np

    a = np.asarray(img.getchannel("A")) > 128
    widths = a.sum(axis=1)
    rows = np.nonzero(widths)[0]
    top, bottom = rows.min(), rows.max()
    h = bottom - top + 1
    lower = np.arange(top + int(h * 0.6), bottom - int(h * 0.03))
    lw = widths[lower]
    narrow = lower[lw <= max(1, lw[lw > 0].min()) * 1.5]
    y = float(np.median(narrow)) if len(narrow) else top + h * 0.85
    return round((y - top) / h, 2)


def art_path(key: str) -> Path:
    art = ROOT / "src" / "assets" / "art"
    return next(iter(sorted(art.glob(f"{key}@*x.webp"))), art / f"{key}.webp")


def grips(keys: list[str], write: bool, out: Path) -> None:
    """Measure `grip` for weapons (a sheet with the grip marked goes to `out`); --write puts them in items.ts."""
    items = ROOT / "src" / "data" / "items.ts"
    src = items.read_text(encoding="utf-8")
    keys = keys or sorted(re.findall(r"art: '(wpn_[a-z0-9_]+)'", src))
    found = {}
    cell = 220
    sheet = Image.new("RGB", (len(keys) * (cell // 2 + 10) + 10, cell + 40), (40, 40, 46))
    draw = ImageDraw.Draw(sheet)
    for i, key in enumerate(keys):
        img = Image.open(art_path(key)).convert("RGBA")
        g = find_grip(img)
        found[key] = g
        thumb = img.copy()
        thumb.thumbnail((cell // 2, cell))
        x = 10 + i * (cell // 2 + 10)
        sheet.paste(thumb, (x, 10), thumb)
        gy = 10 + round(g * thumb.height)
        draw.line((x, gy, x + cell // 2, gy), fill=(255, 40, 40), width=2)
        draw.text((x, cell + 18), f"{g:.2f}", fill=(230, 230, 230))
    sheet.save(out)
    print(out)
    for key, g in found.items():
        print(f"{key}: {g}")
        current = re.search(rf"art: '{key}',\s*grip: ([0-9.]+)", src)
        if write and current and float(current.group(1)) < 0.6:
            print(f"  kept {current.group(1)}: held mid-shaft (staffs, rods), a choice rather than a measurement")
        elif write:
            src, n = re.subn(rf"(art: '{key}',\s*grip: )[0-9.]+", rf"\g<1>{g}", src)
            if n != 1:
                print(f"  {key}: grip line not found")
    if write:
        items.write_text(src, encoding="utf-8")


GAME_KEY = {"hero_raised": "char_hero_bare"}


def pick(picks: list[str]) -> None:
    keys = []
    for p in picks:
        key, _, seed = p.partition(":")
        src = REDO / key / f"{key}_s{seed or FIRST_SEED}.png"
        if not src.exists():
            sys.exit(f"missing {src}")
        game_key = GAME_KEY.get(key) or (f"char_hero_{key[7:]}" if key.startswith("outfit_") else key)
        for old in RAW.glob(f"{game_key}.*"):
            old.unlink()
        shutil.copy(src, RAW / f"{game_key}.png")
        keys.append(game_key)
    subprocess.run([sys.executable, str(ROOT / "scripts" / "prepare_art.py"), "--force", *keys], check=False)


def main() -> None:
    args = sys.argv[1:]
    if args and args[0] == "sheet":
        out = REDO / "_sheet.png"
        if "--out" in args:
            i = args.index("--out")
            out = Path(args[i + 1])
            del args[i:i + 2]
        sheet(args[1:], out)
        return
    if args and args[0] == "grips":
        rest = [x for x in args[1:] if not x.startswith("--")]
        grips(rest, "--write" in args, REDO / "_grips.png")
        return
    if args and args[0] == "pick":
        pick(args[1:])
        return
    global FIRST_SEED, MONSTER_REFS, ICON_REFS
    for flag in ("--first-seed", "--monster-refs", "--icon-refs"):
        if flag in args:
            i = args.index(flag)
            if flag == "--first-seed":
                FIRST_SEED = int(args[i + 1])
            elif flag == "--monster-refs":
                MONSTER_REFS = args[i + 1]
            else:
                ICON_REFS = args[i + 1]
            del args[i:i + 2]
    seeds = 2
    if "--seeds" in args:
        i = args.index("--seeds")
        seeds = int(args[i + 1])
        del args[i:i + 2]
    prompts = old_prompts()
    keys = [a for a in args if not a.startswith("--")]
    for flag, prefix in (("--all-bg", "bg_"), ("--all-mon", "mon_"), ("--all-npc", "npc_"), ("--all-icon", "icon_"), ("--all-wpn", "wpn_")):
        if flag in args:
            keys += [k for k in keys_of(prefix, prompts) if k not in keys]
    if "--all-outfits" in args:
        keys += [f"outfit_{a}" for a in OUTFITS if f"outfit_{a}" not in keys]
    if not keys:
        sys.exit(__doc__)
    generate(keys, seeds, "--dry" in args)


if __name__ == "__main__":
    main()
