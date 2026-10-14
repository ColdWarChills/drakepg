# Art queue: 131 images

Everything the game references or can use that has no art yet, in priority order. Each prompt is complete: copy the block as one piece.

**How:** Higgsfield, model **Nano Banana**, **Unlimited ON**, count 1. Set the aspect ratio listed. Attach the style reference named in each section (click **+**).
Save each result as its **file name** (the key, any extension) in `art/raw/`, then run `python3 scripts/prepare_art.py <key>`. It removes the flat magenta/green background, trims and resizes; the game picks the file up by name.
Missing art never breaks anything: the game draws stand-in shapes until a file exists. Tick the boxes as images land.

## Checklist

**1. NPCs** (7)
- [x] `npc_hulda` (the smith, Brindlemoor)
- [x] `npc_captain_hale` (Reedwatch captain, quest giver)
- [x] `npc_wren` (Reedwatch quartermaster (shop))
- [ ] `npc_ottilie` (Reedwatch healer (inn))
- [ ] `npc_ranger_sabine` (Ch3, Hearthold quest giver)
- [ ] `npc_gunnar` (Ch3, Hearthold trader (shop))
- [ ] `npc_hearthkeeper_moll` (Ch3, Hearthold innkeeper)

**2. Monsters** (18)
- [ ] `mon_barkshell_beetle` (Ch1, tough)
- [ ] `mon_mire_toad` (Ch2)
- [ ] `mon_spark_wisp` (Ch2)
- [ ] `mon_bog_lurker` (Ch2)
- [ ] `mon_sunken_revenant` (Ch2)
- [ ] `mon_stormscale_drakeling` (Ch2, tough)
- [ ] `mon_volt_eel` (Ch2, tough)
- [ ] `mon_bogmother` (Ch2 mini-boss)
- [ ] `mon_stormcoil` (Ch2 boss)
- [ ] `mon_drift_wolf` (Ch3)
- [ ] `mon_crag_ram` (Ch3)
- [ ] `mon_gust_sprite` (Ch3)
- [ ] `mon_rime_acolyte` (Ch3)
- [ ] `mon_glacier_golem` (Ch3, tough)
- [ ] `mon_ember_salamander` (Ch3)
- [ ] `mon_vent_wyrmling` (Ch3, tough)
- [ ] `mon_old_snowmaw` (Ch3 mini-boss)
- [ ] `mon_frostbinder` (Ch3 boss)

**3. Backgrounds** (11)
- [ ] `bg_fen_road` (Ch2 monster area)
- [ ] `bg_reedwatch` (Ch2 outpost town)
- [ ] `bg_sunken_causeway` (Ch2 monster area)
- [ ] `bg_bog_hollow` (mini-boss arena)
- [ ] `bg_drowned_shrine` (final boss arena)
- [ ] `bg_frostfang_pass` (Ch3 monster area)
- [ ] `bg_hearthold` (Ch3 village)
- [ ] `bg_windscour_ridge` (Ch3 monster area; keep the cave mouth in the middle)
- [ ] `bg_steamvent_caves` (Ch3 side cave)
- [ ] `bg_snowmaw_den` (Ch3 mini-boss arena)
- [ ] `bg_frozen_roost` (Ch3 boss arena)

**4. Weapons** (29)
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
- [ ] `wpn_tempered_longsword` (Ch3 shop)
- [ ] `wpn_larchwood_staff` (Ch3 shop)
- [ ] `wpn_ridgerunner_kris` (Ch3 shop)
- [ ] `wpn_frostbite` (Ch3 Ice)
- [ ] `wpn_hoarfrost_staff` (Ch3 Ice)
- [ ] `wpn_icicle_kris` (Ch3 Ice)
- [ ] `wpn_hearthfire_blade` (Ch3 Fire)
- [ ] `wpn_kindling_rod` (Ch3 Fire)
- [ ] `wpn_salamander_kris` (Ch3 Fire)
- [ ] `wpn_snowmaw_cleaver` (Snowmaw drop)
- [ ] `wpn_mawtooth_staff` (Snowmaw drop)
- [ ] `wpn_snowmaw_claw` (Snowmaw drop)
- [ ] `wpn_whiteout` (Frostbinder drop)
- [ ] `wpn_squall_stave` (Frostbinder drop)
- [ ] `wpn_galecutter` (Frostbinder drop)

**5. Item icons** (36)
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
- [ ] `icon_superior_health_potion`
- [ ] `icon_superior_mana_potion`
- [ ] `icon_ridgeplate`
- [ ] `icon_fleecewool_robe`
- [ ] `icon_frostpelt_vest`
- [ ] `icon_galeweave_robe`
- [ ] `icon_frost_pelt`
- [ ] `icon_ram_horn`
- [ ] `icon_rime_shard`
- [ ] `icon_salamander_scale`
- [ ] `icon_essence_fire`
- [ ] `icon_essence_water`
- [ ] `icon_essence_ice`
- [ ] `icon_essence_wind`
- [ ] `icon_essence_earth`
- [ ] `icon_essence_energy`
- [ ] `icon_essence_light`
- [ ] `icon_essence_dark`

**6. Armor outfits (lowest priority)** (30)
- [ ] `char_warrior_leather_armor`
- [ ] `char_warrior_apprentice_robe`
- [ ] `char_warrior_scale_mail`
- [ ] `char_warrior_reedweave_robe`
- [ ] `char_warrior_marshhide_vest`
- [ ] `char_warrior_stormweave_robe`
- [ ] `char_warrior_ridgeplate`
- [ ] `char_warrior_fleecewool_robe`
- [ ] `char_warrior_frostpelt_vest`
- [ ] `char_warrior_galeweave_robe`
- [ ] `char_mage_leather_armor`
- [ ] `char_mage_apprentice_robe`
- [ ] `char_mage_scale_mail`
- [ ] `char_mage_reedweave_robe`
- [ ] `char_mage_marshhide_vest`
- [ ] `char_mage_stormweave_robe`
- [ ] `char_mage_ridgeplate`
- [ ] `char_mage_fleecewool_robe`
- [ ] `char_mage_frostpelt_vest`
- [ ] `char_mage_galeweave_robe`
- [ ] `char_rogue_leather_armor`
- [ ] `char_rogue_apprentice_robe`
- [ ] `char_rogue_scale_mail`
- [ ] `char_rogue_reedweave_robe`
- [ ] `char_rogue_marshhide_vest`
- [ ] `char_rogue_stormweave_robe`
- [ ] `char_rogue_ridgeplate`
- [ ] `char_rogue_fleecewool_robe`
- [ ] `char_rogue_frostpelt_vest`
- [ ] `char_rogue_galeweave_robe`

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

### `npc_ranger_sabine` · 3:4 · Ch3, Hearthold quest giver
```
A tough mountain ranger woman in her thirties, a short dark braid, a weathered face with a confident half-smile, a green fur-lined hooded cloak over brown leather, a longbow on her back and snowshoes tied at her belt. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `npc_gunnar` · 3:4 · Ch3, Hearthold trader (shop)
```
A big cheerful mountain trader with a bushy red-blond beard, a thick fur hat and a brown fur coat, a heavy pack of goods on his back, holding a ledger. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `npc_hearthkeeper_moll` · 3:4 · Ch3, Hearthold innkeeper
```
A plump grandmotherly innkeeper with rosy cheeks and grey hair in a bun, a red knitted shawl over a cream dress and apron, holding a steaming mug. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
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

### `mon_drift_wolf` · 4:3 · Ch3
```
A lean white winter wolf with frost-tipped fur and icy blue eyes, breath steaming, snarling in a low hunting stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_crag_ram` · 4:3 · Ch3
```
A sturdy mountain ram with huge curled stone-grey horns and shaggy grey-brown wool with snow on its back, stamping a hoof. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_gust_sprite` · 1:1 · Ch3
```
A small mischievous wind spirit made of swirling pale teal air with a cute grinning face, leaves and snowflakes caught in its spiraling body, floating. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, floating, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_rime_acolyte` · 3:4 · Ch3
```
A cultist in pale blue hooded robes edged with frost, face hidden in shadow except two glowing icy eyes, holding up a glowing crystal of ice. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_glacier_golem` · 3:4 · Ch3, tough
```
A hulking golem made of blocks of translucent blue glacier ice, glowing runes carved into its chest, huge fists, snow piled on its shoulders. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_volt_eel` · 4:3 · Ch2
```
A long teal swamp eel rearing up out of a puddle, yellow lightning crackling along its fins and spine, small glowing yellow eyes, open mouth with needle teeth. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_ember_salamander` · 4:3 · Ch3
```
A fat orange salamander with glowing ember spots along its back, smoke curling from its nostrils, shiny wet skin. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_vent_wyrmling` · 4:3 · Ch3, tough
```
A young fire wyrm with a long body and small wings, red-orange scales cracked with glowing lava lines, steam rising from it, snarling. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_old_snowmaw` · 1:1 · Ch3 mini-boss
```
A huge ancient shaggy mountain beast like a giant snow bear crossed with a yak, white fur matted with ice, a thick crust of packed ice armor over its back and shoulders, small tired eyes, enormous jaws with two tusks. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, menacing stance, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `mon_frostbinder` · 3:4 · Ch3 boss
```
An elegant ice sorceress in flowing white and pale blue robes, long silver hair blowing in the wind, a crown of ice shards, holding a tall stave topped with a swirling blue crystal, a cold calm smile, snowflakes swirling around her. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
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

### `bg_frostfang_pass` · 16:9 · Ch3 monster area
```
A snowy mountain pass: a trail of packed snow winding between tall snow-covered pines, jagged white peaks behind under a pale winter sky. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt; the bottom third is open, flat ground for characters to walk on. No characters, no people, no animals, no text, no letters, no UI. Match the art style of the reference image exactly.
```

### `bg_hearthold` · 16:9 · Ch3 village
```
A small snowy mountain village: three log cabins with snow-covered roofs and warm glowing windows spread left, middle and right (a ranger's lodge, a trading post with furs hanging outside, a cozy inn with a smoking chimney), a packed snow road in front, peaks behind. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt; the bottom third is open, flat ground for characters to walk on. No characters, no people, no animals, no text, no letters, no UI. Match the art style of the reference image exactly.
```

### `bg_windscour_ridge` · 16:9 · Ch3 monster area; keep the cave mouth in the middle
```
A wind-blasted ridge high in the mountains: snow streaming off the rocks, bare icy boulders, stormy grey sky, and in the exact middle of the image a dark cave mouth in the cliff face with faint orange steam glowing inside, at the back edge of the walkable ground. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt; the bottom third is open, flat ground for characters to walk on. No characters, no people, no animals, no text, no letters, no UI. Match the art style of the reference image exactly.
```

### `bg_steamvent_caves` · 16:9 · Ch3 side cave
```
Inside a warm volcanic cave: dark rock walls, glowing orange hot spring pools, steam rising, stalactites and a few red crystals. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt; the bottom third is open, flat ground for characters to walk on. No characters, no people, no animals, no text, no letters, no UI. Match the art style of the reference image exactly.
```

### `bg_snowmaw_den` · 16:9 · Ch3 mini-boss arena
```
A deep snowy hollow between cliffs: huge gnawed bones half buried in snow, a giant flattened nest of snow and fur, icicles hanging from the rocks, grey light. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt; the bottom third is open, flat ground for characters to walk on. No characters, no people, no animals, no text, no letters, no UI. Match the art style of the reference image exactly.
```

### `bg_frozen_roost` · 16:9 · Ch3 boss arena
```
A mountaintop dragon roost: a vast frozen plateau, and in the background an enormous sleeping dragon with dark red scales trapped inside a towering wall of blue ice, ice pillars with glowing runes, blizzard sky. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt; the bottom third is open, flat ground for characters to walk on. No characters, no people, no animals, no text, no letters, no UI. Match the art style of the reference image exactly.
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

### `wpn_tempered_longsword` · 9:16 · Ch3 shop
```
A single long straight steel sword with a polished blade showing a faint blue temper line, a plain iron crossguard and a fur-wrapped grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_larchwood_staff` · 9:16 · Ch3 shop
```
A single tall pale larchwood staff banded with dark iron rings, topped with a small glowing amber stone, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_ridgerunner_kris` · 9:16 · Ch3 shop
```
A single dagger with a wavy steel blade and a bone grip wrapped in leather, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_frostbite` · 9:16 · Ch3 Ice
```
A single sword with a pale icy-blue blade covered in frost crystals, a silver snowflake-shaped crossguard and a white leather grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_hoarfrost_staff` · 9:16 · Ch3 Ice
```
A single staff of frosted white wood with ice crystals growing along it, topped with a cluster of glowing blue ice crystals, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_icicle_kris` · 9:16 · Ch3 Ice
```
A single dagger whose blade is a single sharp translucent icicle, with a silver grip set with a blue gem, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_hearthfire_blade` · 9:16 · Ch3 Fire
```
A single sword with a dark steel blade glowing orange-red along its edge like hot metal, a brass crossguard and a red leather grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_kindling_rod` · 9:16 · Ch3 Fire
```
A single short rod of charred black wood with glowing orange cracks, a burning ember held in iron claws at the top, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_salamander_kris` · 9:16 · Ch3 Fire
```
A single dagger with a wavy orange-red blade, the grip wrapped in shiny salamander scale, a small flame flickering at the guard, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_snowmaw_cleaver` · 9:16 · Snowmaw drop
```
A single huge heavy cleaver made from a curved white ice-hard tusk bound to a thick wooden handle with fur and rope, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_mawtooth_staff` · 9:16 · Snowmaw drop
```
A single gnarled pale staff wrapped in white fur, topped with one huge curved frosty fang, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_snowmaw_claw` · 9:16 · Snowmaw drop
```
A single curved dagger made of a big white beast claw set into a bone grip bound with leather, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_whiteout` · 9:16 · Frostbinder drop
```
A single large greatsword with a pale silver-white blade streaked with swirling wind and snow, a wing-shaped crossguard and a teal grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_squall_stave` · 9:16 · Frostbinder drop
```
A single elegant tall stave of white wood spiraling upward around a swirling orb of pale teal wind, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `wpn_galecutter` · 9:16 · Frostbinder drop
```
A single slim curved dagger with a pale teal blade so thin it looks almost transparent, small feathers tied to the pommel, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
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

### `icon_superior_health_potion` · 1:1
```
A single tall elegant crystal potion bottle with a gold stopper shaped like a heart, filled with glowing crimson liquid, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_superior_mana_potion` · 1:1
```
A single tall elegant crystal potion bottle with a silver stopper shaped like a star, filled with glowing deep blue liquid, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_ridgeplate` · 1:1
```
A single empty set of thick steel plate armor over a white fur lining, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_fleecewool_robe` · 1:1
```
A single empty thick cream wool robe with blue stitched warding runes and a fur collar, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_frostpelt_vest` · 1:1
```
A single empty vest of white wolf pelt over boiled leather, frost on the fur, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_galeweave_robe` · 1:1
```
A single empty pale green robe woven so light it ripples as if in wind, no person inside, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_frost_pelt` · 1:1
```
A single folded thick white wolf fur, stiff with frost, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_ram_horn` · 1:1
```
A single single curled grey-brown ram horn, scarred and ridged, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_rime_shard` · 1:1
```
A single jagged splinter of glowing pale blue glacier ice, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### `icon_salamander_scale` · 1:1
```
A single single orange salamander scale glowing faintly at its edges, shown alone as a game inventory icon. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. Centered, seen from the front, the whole item visible and filling most of the square with a small empty margin. No hands, no character, no stand, no text. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no shadow, no border. Match the art style of the reference image exactly. Only the style, not the character.
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

### `char_warrior_ridgeplate` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears thick steel plate armor with a white fur lining showing at the collar and edges. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_fleecewool_robe` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a thick cream wool robe with blue stitched warding runes and a fur collar. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_frostpelt_vest` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a vest of white wolf pelt over boiled leather with frost on the fur, with leather bracers. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_warrior_galeweave_robe` · 3:4 · reference: `char_warrior_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a pale green robe woven so light it ripples as if in a breeze. Keep everything else exactly the same: the same young warrior hero with short messy brown hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
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

### `char_mage_ridgeplate` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears thick steel plate armor with a white fur lining showing at the collar and edges. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_fleecewool_robe` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a thick cream wool robe with blue stitched warding runes and a fur collar. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_frostpelt_vest` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a vest of white wolf pelt over boiled leather with frost on the fur, with leather bracers. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
```

### `char_mage_galeweave_robe` · 3:4 · reference: `char_mage_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a pale green robe woven so light it ripples as if in a breeze. Keep everything else exactly the same: the same young mage hero with shoulder-length dark hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat bright green background. No weapon anywhere in the image. No text.
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

### `char_rogue_ridgeplate` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears thick steel plate armor with a white fur lining showing at the collar and edges. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_fleecewool_robe` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a thick cream wool robe with blue stitched warding runes and a fur collar. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_frostpelt_vest` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a vest of white wolf pelt over boiled leather with frost on the fur, with leather bracers. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```

### `char_rogue_galeweave_robe` · 3:4 · reference: `char_rogue_bare`
```
Edit this image (image 1). Change only the clothing: the hero now wears a pale green robe woven so light it ripples as if in a breeze. Keep everything else exactly the same: the same young rogue hero with messy auburn hair, the same face, body, pose and proportions, both empty clenched fists in exactly the same place, the bold outline style and the plain flat magenta background. No weapon anywhere in the image. No text.
```
