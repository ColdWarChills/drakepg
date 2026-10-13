# Art queue: 75 images

Everything the game references or can use that has no art yet, in priority order. Each prompt is complete: copy the block as one piece.

**How:** Higgsfield, model **Nano Banana**, **Unlimited ON**, count 1. Set the aspect ratio listed. Attach the style reference named in each section (click **+**).
Save each result as its **file name** (the key, any extension) in `art/raw/`, then run `python3 scripts/prepare_art.py <key>`. It removes the flat magenta/green background, trims and resizes; the game picks the file up by name.
Missing art never breaks anything: the game draws stand-in shapes until a file exists. Tick the boxes as images land.

## Checklist

**1. NPCs** (4)
- [ ] `npc_hulda` (the smith, Brindlemoor)
- [ ] `npc_captain_hale` (Reedwatch captain, quest giver)
- [ ] `npc_wren` (Reedwatch quartermaster (shop))
- [ ] `npc_ottilie` (Reedwatch healer (inn))

**2. Monsters** (8)
- [ ] `mon_barkshell_beetle` (Ch1, tough)
- [ ] `mon_mire_toad` (Ch2)
- [ ] `mon_spark_wisp` (Ch2)
- [ ] `mon_bog_lurker` (Ch2)
- [ ] `mon_sunken_revenant` (Ch2)
- [ ] `mon_stormscale_drakeling` (Ch2, tough)
- [ ] `mon_bogmother` (Ch2 mini-boss)
- [ ] `mon_stormcoil` (Ch2 boss)

**3. Backgrounds** (5)
- [ ] `bg_fen_road` (Ch2 monster area)
- [ ] `bg_reedwatch` (Ch2 outpost town)
- [ ] `bg_sunken_causeway` (Ch2 monster area)
- [ ] `bg_bog_hollow` (mini-boss arena)
- [ ] `bg_drowned_shrine` (final boss arena)

**4. Weapons** (14)
- [ ] `wpn_heartwood_staff` (Thornheart drop)
- [ ] `wpn_briar_fang` (Thornheart drop)
- [ ] `wpn_steel_broadsword` (Ch2)
- [ ] `wpn_yew_staff` (Ch2)
- [ ] `wpn_fenwatch_stiletto` (Ch2)
- [ ] `wpn_tidebreaker` (Ch2)
- [ ] `wpn_stormcleaver` (Ch2)
- [ ] `wpn_tidecaller_rod` (Ch2)
- [ ] `wpn_sparkglass_wand` (Ch2)
- [ ] `wpn_brinefang` (Ch2)
- [ ] `wpn_sparkfang` (Ch2)
- [ ] `wpn_thunderhead_maul` (Ch2)
- [ ] `wpn_stormcoil_scepter` (Ch2)
- [ ] `wpn_coilfang` (Ch2)

**5. Item icons** (26)
- [ ] `icon_travel_garb`
- [ ] `icon_novice_robe`
- [ ] `icon_leather_armor`
- [ ] `icon_apprentice_robe`
- [ ] `icon_scale_mail`
- [ ] `icon_reedweave_robe`
- [ ] `icon_marshhide_vest`
- [ ] `icon_stormweave_robe`
- [ ] `icon_health_potion`
- [ ] `icon_mana_potion`
- [ ] `icon_greater_health_potion`
- [ ] `icon_greater_mana_potion`
- [ ] `icon_scrap`
- [ ] `icon_dust`
- [ ] `icon_moss_clump`
- [ ] `icon_boar_tusk`
- [ ] `icon_fen_hide`
- [ ] `icon_storm_scale`
- [ ] `icon_essence_fire`
- [ ] `icon_essence_water`
- [ ] `icon_essence_ice`
- [ ] `icon_essence_wind`
- [ ] `icon_essence_earth`
- [ ] `icon_essence_energy`
- [ ] `icon_essence_light`
- [ ] `icon_essence_dark`

**6. Armor outfits (lowest priority)** (18)
- [ ] `char_warrior_leather_armor`
- [ ] `char_warrior_apprentice_robe`
- [ ] `char_warrior_scale_mail`
- [ ] `char_warrior_reedweave_robe`
- [ ] `char_warrior_marshhide_vest`
- [ ] `char_warrior_stormweave_robe`
- [ ] `char_mage_leather_armor`
- [ ] `char_mage_apprentice_robe`
- [ ] `char_mage_scale_mail`
- [ ] `char_mage_reedweave_robe`
- [ ] `char_mage_marshhide_vest`
- [ ] `char_mage_stormweave_robe`
- [ ] `char_rogue_leather_armor`
- [ ] `char_rogue_apprentice_robe`
- [ ] `char_rogue_scale_mail`
- [ ] `char_rogue_reedweave_robe`
- [ ] `char_rogue_marshhide_vest`
- [ ] `char_rogue_stormweave_robe`

---

## 1. NPCs

Aspect **3:4**. Style reference: any finished character (e.g. `npc_elder_maren`).

### `npc_hulda` · 3:4 · the smith, Brindlemoor
```
A broad-shouldered blacksmith woman in her forties, braided auburn hair, soot on her cheeks, rolled-up sleeves showing strong forearms, thick leather apron over a brown shirt, heavy gloves, a smithing hammer resting on her shoulder. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `npc_captain_hale` · 3:4 · Reedwatch captain, quest giver
```
A weathered middle-aged outpost captain, short grey beard, a scar over one eyebrow, dark green padded coat under a steel breastplate, high muddy boots, a brass spyglass on his belt, one hand resting on his sword hilt, steady confident stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `npc_wren` · 3:4 · Reedwatch quartermaster (shop)
```
A young, wiry quartermaster woman, short brown hair under a flat cap, freckles, rolled-up sleeves, a brown leather apron full of tools, a coil of rope over one shoulder, holding a clipboard. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `npc_ottilie` · 3:4 · Reedwatch healer (inn)
```
A kind elderly healer, round gentle face, long white and pale blue robes, a sea-blue headscarf, a pouch of herbs and a small lantern at her belt, holding a wooden bowl of steaming broth. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

---

## 2. Monsters

Aspect as listed. Style reference: any finished monster (e.g. `mon_thornback_boar`). Monsters face right.

### `mon_barkshell_beetle` · 4:3 · Ch1, tough
```
A big armored forest beetle, its domed shell made of thick cracked dark bark with moss in the cracks, dry and brittle-looking, strong curved mandibles, six stubby legs, small glowing amber eyes, sturdy low stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_mire_toad` · 1:1 · Ch2
```
A fat, warty swamp toad monster the size of a pony, mottled dark green and brown skin, glowing yellow eyes, a long sticky pink tongue curling out of its wide mouth, small water lilies and algae growing on its back. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_spark_wisp` · 1:1 · Ch2
```
A floating ball of living lightning: a crackling bright yellow-white core with a small mischievous face, violet sparks and electric tendrils trailing below it like a tail, tiny arcs of lightning around it. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_bog_lurker` · 4:3 · Ch2
```
A hulking swamp beast on four legs, part crocodile and part bear, matted hide dripping dark green moss and mud, glowing teal eyes, broad clawed paws, reeds stuck in its fur, a heavy lumbering stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_sunken_revenant` · 3:4 · Ch2
```
A drowned undead knight: rusted, barnacle-crusted armor, a tattered dark cloak dripping water, pale blue-grey skin, glowing violet eyes under a broken helmet, seaweed tangled on the shoulders, holding a rusted short sword. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_stormscale_drakeling` · 4:3 · Ch2, tough
```
A young dragon-like lizard the size of a large dog, deep blue scales with bright yellow lightning-stripe markings, a crest of yellow spines, small folded wings, sparks crackling at its jaws, alert crouching pose. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_bogmother` · 1:1 · Ch2 mini-boss
```
A huge, grotesque swamp matriarch boss monster: a massive bloated toad-like body of mud and moss rising out of a puddle, many small glowing yellow eyes, a wide drooping mouth with hanging roots, tiny frogs and leeches clinging to her sides, a crown of reeds and lily pads, menacing pose. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### `mon_stormcoil` · 1:1 · Ch2 boss
```
A colossal sea serpent boss monster coiled upright, deep blue scales with glowing yellow lightning patterns, a fin-like crest crackling with electricity, glowing white eyes, lightning arcing between its coils, a dull grey giant scale tangled in its coils, menacing pose. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

---

## 3. Backgrounds

Aspect **16:9**. Style reference: `bg_brindlemoor` or `bg_thornwood`.

### `bg_fen_road` · 16:9 · Ch2 monster area
```
A misty road at the edge of a fen: a muddy dirt road with old wooden planks laid over puddles, tall reeds and cattails, a few gnarled dead trees, still pools reflecting a grey-green overcast sky. The bottom 30% of the image is flat, open, muddy ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### `bg_reedwatch` · 16:9 · Ch2 outpost town
```
A small wooden outpost on stilts in a marsh. From left to right: a watch hut with a green banner, a supply hut with crates and barrels on its porch, a healer's hut with hanging herbs and a lantern, and at the far right a tall wooden watchtower. Reeds and mist, grey-green sky. The bottom 20% of the image is a wide, empty, flat wooden boardwalk running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### `bg_sunken_causeway` · 16:9 · Ch2 monster area
```
An ancient stone causeway half sunk into a stormy fen: broken stone pillars and arches, dark water on both sides, reeds, slanting rain, distant lightning in a dark grey sky. The bottom 30% of the image is flat, wet, cracked stone causeway running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### `bg_bog_hollow` · 16:9 · mini-boss arena
```
A gloomy hollow deep in a swamp: a large murky green pool at the back, twisted mangrove roots, hanging moss, bubbling mud, drifting pale will-o-wisp lights, dim greenish light. The bottom 30% of the image is flat, muddy ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### `bg_drowned_shrine` · 16:9 · final boss arena
```
A drowned ancient shrine in a storm: tilted stone columns and a broken dome rising from dark water, a huge dull grey dragon scale set into the altar at the back, crackling lightning in purple-black clouds, rain. The bottom 30% of the image is flat, wet stone floor running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

---

## 4. Weapons

Aspect **9:16**. Style reference: any finished weapon or character. Upright, handle at the bottom. After processing, the coding session sets `grip` and `size` on the item.

### `wpn_heartwood_staff` · 9:16 · Thornheart drop
```
A single tall living staff of pale golden heartwood, its top branching into small leafy twigs that cradle a softly glowing green seed-shaped gem, thin green vines spiraling down the shaft, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_briar_fang` · 9:16 · Thornheart drop
```
A single dagger grown from one huge curved dark-brown thorn, the glossy thorn point is the blade, the grip wrapped in green vine with two small leaves at the guard, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_steel_broadsword` · 9:16 · Ch2
```
A single steel broadsword with a wide polished blade, a simple brass crossguard and a leather-wrapped grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_yew_staff` · 9:16 · Ch2
```
A single tall dark yew wood staff, smooth and slightly twisted, the top curling around a pale blue glass orb, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_fenwatch_stiletto` · 9:16 · Ch2
```
A single slim steel stiletto dagger with a needle-thin blade, a small round guard and a dark green wrapped grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_tidebreaker` · 9:16 · Ch2
```
A single sea-green steel sword with a wavy edge like a breaking wave, a coral-shaped crossguard and droplets of water floating around the blade, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_stormcleaver` · 9:16 · Ch2
```
A single heavy one-handed cleaver sword with a dark steel blade etched with glowing yellow lightning runes, small sparks jumping off the edge, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_tidecaller_rod` · 9:16 · Ch2
```
A single short driftwood rod wrapped in seaweed, a large white pearl at the top inside a swirl of water, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_sparkglass_wand` · 9:16 · Ch2
```
A single slender wand of fused yellow glass with jagged lightning shapes trapped inside, a copper handle, crackling at the tip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_brinefang` · 9:16 · Ch2
```
A single wavy-bladed kris dagger, blue-green steel crusted with white sea salt, a shell-shaped guard, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_sparkfang` · 9:16 · Ch2
```
A single dagger made from a curved blue-and-yellow drake fang on a copper hilt, tiny sparks at the tip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_thunderhead_maul` · 9:16 · Ch2
```
A single huge war maul with a block-shaped head of storm-grey metal set with a glowing yellow scale, lightning crackling around the head, a long leather-wrapped handle, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_stormcoil_scepter` · 9:16 · Ch2
```
A single silver scepter whose top is a cage of coiled silver serpents holding a ball of crackling lightning, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_coilfang` · 9:16 · Ch2
```
A single long curved serpent-fang dagger, deep blue with glowing yellow veins, a hilt wrapped in blue scaled leather, sparks at the tip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

---

## 5. Item icons

Aspect **1:1**. Style reference: any finished weapon (e.g. `wpn_iron_sword`). The game shows `icon_<item id>` art in the inventory, shop and forge when it exists; until then it draws simple shapes.

### `icon_travel_garb` · 1:1
```
A single empty plain brown-and-cream traveler's tunic with a rope belt, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_novice_robe` · 1:1
```
A single empty thin grey-blue cloth robe with a few stitched protective runes on the hem, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_leather_armor` · 1:1
```
A single empty brown leather cuirass with stitched panels, shoulder straps and brass buckles, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_apprentice_robe` · 1:1
```
A single empty deep red apprentice robe with glowing orange warding runes along the sleeves and hem, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_scale_mail` · 1:1
```
A single empty shirt of overlapping polished steel scales with leather trim, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_reedweave_robe` · 1:1
```
A single empty robe woven from dried green-gold marsh reeds in a basket-weave pattern with faint glowing blue runes, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_marshhide_vest` · 1:1
```
A single empty dark green oiled hide vest with a glossy wet sheen, bone toggles and water droplets running off it, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_stormweave_robe` · 1:1
```
A single empty pale silver-blue robe of shimmering silk with tiny yellow lightning sparks running along the seams, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_health_potion` · 1:1
```
A single small round glass potion bottle with a cork stopper, filled with bright red liquid, a little heart-shaped paper label, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_mana_potion` · 1:1
```
A single small round glass potion bottle with a cork stopper, filled with glowing blue liquid, a little star-shaped paper label, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_greater_health_potion` · 1:1
```
A single large ornate glass flask with gold filigree and a wax-sealed cork, filled with deep red glowing liquid, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_greater_mana_potion` · 1:1
```
A single large ornate glass flask with silver filigree and a wax-sealed cork, filled with deep blue glowing liquid with tiny sparkles, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_scrap` · 1:1
```
A single small pile of rusty iron scrap: bent nails, a broken buckle and twisted metal bits, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_dust` · 1:1
```
A single small open cloth pouch spilling sparkling pale lilac honing dust, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_moss_clump` · 1:1
```
A single springy clump of bright green moss with a few tiny white flowers, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_boar_tusk` · 1:1
```
A single single curved ivory boar tusk, slightly chipped at the base, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_fen_hide` · 1:1
```
A single rolled piece of slick dark green marsh-creature hide tied with twine, glossy wet sheen, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_storm_scale` · 1:1
```
A single single large blue reptile scale with crackling yellow lightning sparks across it, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_fire` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of flickering orange-red flames, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_water` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of clear blue water with tiny bubbles, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_ice` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of frosty pale blue mist and tiny ice crystals, frost on the glass, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_wind` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of a pale green swirling breeze with tiny leaves, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_earth` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of brown-gold sand and tiny pebbles with a small green sprout, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_energy` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of crackling yellow lightning, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_light` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of a warm white-gold radiant glow with tiny sparkles, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_essence_dark` · 1:1
```
A single small teardrop-shaped glass vial with a silver cap, holding a swirling glowing essence of swirling deep purple shadow smoke with tiny violet sparks, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

---

## 6. Armor outfits (lowest priority)

Aspect **3:4**. **Reference: that class's empty-handed art** `src/assets/art/char_<class>_bare.webp` (an edit of it, not a new character). After processing, the fist positions must be re-measured into the armor's `hands[class]` in `src/data/items.ts`, so tell the coding session which outfits landed.

### `char_warrior_leather_armor` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a brown leather cuirass with stitched panels, leather shoulder guards and brass buckles over a simple shirt, with leather bracers. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_apprentice_robe` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a deep red apprentice robe with glowing orange warding runes along the sleeves and hem, tied with a cloth sash. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_scale_mail` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a shirt of overlapping polished steel scales over a padded tunic, with steel shoulder guards and a wide leather belt. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_reedweave_robe` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a robe woven from dried green-gold marsh reeds in a basket-weave pattern with faint glowing blue runes. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_marshhide_vest` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a dark green oiled hide vest with a glossy wet sheen and bone toggles over a grey shirt, with hide bracers. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_stormweave_robe` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a pale silver-blue robe of shimmering silk with tiny yellow lightning sparks along the seams. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_mage_leather_armor` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a brown leather cuirass with stitched panels, leather shoulder guards and brass buckles over a simple shirt, with leather bracers. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_apprentice_robe` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a deep red apprentice robe with glowing orange warding runes along the sleeves and hem, tied with a cloth sash. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_scale_mail` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a shirt of overlapping polished steel scales over a padded tunic, with steel shoulder guards and a wide leather belt. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_reedweave_robe` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a robe woven from dried green-gold marsh reeds in a basket-weave pattern with faint glowing blue runes. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_marshhide_vest` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a dark green oiled hide vest with a glossy wet sheen and bone toggles over a grey shirt, with hide bracers. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_stormweave_robe` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a pale silver-blue robe of shimmering silk with tiny yellow lightning sparks along the seams. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_rogue_leather_armor` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a brown leather cuirass with stitched panels, leather shoulder guards and brass buckles over a simple shirt, with leather bracers. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_apprentice_robe` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a deep red apprentice robe with glowing orange warding runes along the sleeves and hem, tied with a cloth sash. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_scale_mail` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a shirt of overlapping polished steel scales over a padded tunic, with steel shoulder guards and a wide leather belt. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_reedweave_robe` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a robe woven from dried green-gold marsh reeds in a basket-weave pattern with faint glowing blue runes. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_marshhide_vest` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a dark green oiled hide vest with a glossy wet sheen and bone toggles over a grey shirt, with hide bracers. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_stormweave_robe` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a pale silver-blue robe of shimmering silk with tiny yellow lightning sparks along the seams. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```
