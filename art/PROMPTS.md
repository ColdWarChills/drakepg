# Art prompts

Painted cartoon style, original designs. 14 images (4 backgrounds, 10 characters), plus optional extras (class heroes, deep forest).

## How to make them

1. On higgsfield.ai pick **Nano Banana** with **Unlimited ON**. Set the aspect ratio listed for each image. Generating 4 at a time and keeping the best is fine.
2. **Make `char_player` first.** For every other image, click **+** to attach your chosen player image as a reference, and add this line to the end of the prompt:
   > Match the art style of the reference image exactly. Only the style, not the character.
3. Download each pick and rename it to its **file name** below (any extension: `.png`, `.jpg`, `.webp`).
4. On GitHub open the `art/raw/` folder → **Add file → Upload files** → drop them in → **Commit changes**.
5. Or just paste them into the Claude chat and say which is which.
6. Tell Claude. It removes the backgrounds, trims and resizes everything, and wires it into the game.

Renaming is optional: if it's a hassle, upload as-is and say so. Same if a character faces the wrong way.

## Shared style text

Each prompt below already ends with one of these, so you can copy each prompt as one piece.

- **[CHAR-M]** = `2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.`
- **[CHAR-G]** = the same, but with `Plain flat solid bright green (#00FF00) background`. Used for red or purple characters so the background can be cut out cleanly.
- **[BG]** = `2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.`

---

## Characters

### 1. `char_player` · 3:4
```
A young adventurer hero, about 18, short messy brown hair, determined friendly face, blue tunic over a white shirt, brown leather belt with small pouches, brown boots, a simple steel short sword held at his side. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 2. `npc_elder_maren` · 3:4
```
An old, wise village elder woman, long white hair in a braid, kind wrinkled face, long grey-blue robe with a dark green shawl, leaning on a gnarled wooden walking staff. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 3. `npc_tobin` · 3:4
```
A cheerful, stout middle-aged merchant, round belly, bushy brown beard, orange vest over a cream shirt, brown apron, a coin purse on his belt, holding a small ledger book. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 4. `npc_ilsa` · 3:4
```
A friendly innkeeper woman in her thirties, red hair tied up in a bun, rosy cheeks, red dress with a white apron, holding a wooden tankard. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 5. `npc_gate_guard` · 3:4
```
A tall, sturdy town guard, steel helmet and chainmail, dark blue tabard, holding a long spear upright, stern but honest face, short black beard. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

## Monsters

### 5b. `npc_hulda` · 3:4 (the smith)
```
A broad-shouldered blacksmith woman in her forties, braided auburn hair, soot on her cheeks, rolled-up sleeves showing strong forearms, thick leather apron over a brown shirt, heavy gloves, a smithing hammer resting on her shoulder. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 6. `mon_mossling` · 1:1
```
A small, cute but mischievous moss creature monster: a round blob of green moss and lichen with two big glowing yellow eyes, tiny mushrooms and leaves growing on top, stubby root-like feet. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 7. `mon_thornback_boar` · 4:3
```
A large, aggressive wild boar monster seen from the side, dark brown bristly fur, long curved ivory tusks, glowing red eyes, a ridge of sharp wooden thorns and brambles growing along its spine, snorting. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 8. `mon_ash_imp` · 1:1
```
A small winged fire imp demon, charcoal-grey skin with glowing orange ember cracks, small curved horns, bat-like wings, sharp toothy grin, long tail with a flame at the tip. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 9. `mon_bramble_bandit` · 3:4
```
A sneaky forest bandit, dark purple hooded cloak, face half-covered by a cloth mask, leather armor with thorny bramble vines wrapped around the arms, holding a curved dagger in a crouched, ready stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 9a. `mon_barkshell_beetle` · 4:3 (tough)
```
A big armored forest beetle, its domed shell made of thick cracked dark bark with moss in the cracks, dry and brittle-looking, strong curved mandibles, six stubby legs, small glowing amber eyes, sturdy low stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 9b. `mon_thornheart` · 1:1 (Chapter 1 boss)
```
A huge ancient forest guardian boss monster gone wild: a hulking four-legged beast made of twisted dark wood and bark, a glowing sickly green heart visible through a crack in its chest, thick thorny bramble vines wrapped around its body, moss and small mushrooms on its back, antler-like branches on its head, glowing green eyes, roots trailing from its feet. Menacing, powerful stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### Class heroes (optional) · 3:4
The game uses these per class; any missing one falls back to `char_player`. Same [CHAR-M] style text and reference rule as above.

`char_warrior`
```
A young warrior hero, about 18, short messy brown hair, determined face, blue tunic with a dented steel breastplate and shoulder pad, brown leather belt, brown boots, a steel sword held ready at his side. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

`char_mage`
```
A young mage hero, about 18, shoulder-length dark hair, curious clever face, deep purple hooded robe with gold trim, a leather satchel of scrolls, holding a wooden staff topped with a softly glowing orange crystal. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

`char_rogue`
```
A young rogue hero, about 18, messy auburn hair, sly confident grin, dark green hooded short cloak, brown leather vest and bracers, many small belt pouches, a dagger in each hand in a light, ready stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

## Backgrounds

### 10. `bg_title` · 16:9
```
Title screen scene: a huge dragon silhouette flying across a full moon above misty mountains, a small cozy medieval village with glowing windows in the valley below, deep purple and blue night sky with stars. Keep the upper-middle area calm and uncluttered for a game logo. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no text, no letters, no UI.
```

### 11. `bg_brindlemoor` · 16:9
```
A cozy medieval village street in daytime. From left to right: a small stone cottage with a round wooden door, a merchant's shop with a striped awning and crates of goods, a two-story timber-framed inn with a blank hanging sign, and at the far right a stone town wall with an open wooden gate. The bottom 20% of the image is a wide, empty, flat cobblestone street running across the full width. Blue sky with soft clouds. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 12. `bg_thornwood` · 16:9
```
A dark enchanted forest clearing: tall twisted trees with thorny brambles, hanging vines, mossy rocks, dappled light beams through the canopy, green and teal palette. The bottom 30% of the image is flat, open, mossy dirt ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 13. `bg_thornwood_deep` · 16:9 (optional)
Darker variant for the second forest room. Without it, the game reuses `bg_thornwood`, darkened.
```
The deepest, darkest part of an enchanted forest at dusk: enormous ancient twisted trees, thick thorny bramble walls, glowing blue-green mushrooms, faint purple mist, fireflies. The bottom 30% of the image is flat, open, mossy dirt ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 14. `bg_thornheart_hollow` · 16:9
The boss arena. Without it, the game reuses the forest backgrounds, darkened.
```
A hidden hollow at the heart of a cursed forest: a circle of giant ancient trees whose roots and thorny brambles twist together into walls, a huge gnarled dead tree in the background with a faint sickly green glow pulsing from a crack in its trunk, drifting green spores, dim moonlight from above. The bottom 30% of the image is flat, open, root-covered dirt ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

## Weapons (drawn in the hero's hand)

Model **Nano Banana**, Unlimited on, aspect **9:16**, with any finished character as the style reference. File name `wpn_<item id>`. Fill in the description:

```
A single <weapon description>, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

After processing, set the weapon's `art`, `grip` (where the handle's middle is, 0 = top, 1 = bottom) and `size` in `src/data/items.ts`.

## Empty-handed heroes (`char_<class>_bare`)

Attach the class's hero image as the reference (aspect 3:4) and ask for an edit:

```
Edit this image (image 1). Remove the <weapon> completely. Keep everything else exactly the same: the same <class description>, colors, pose, bold outline style, proportions and the plain flat <magenta / bright green> background. The hand that held it is now an empty clenched fist in the same place, as if gripping an invisible handle. No weapon anywhere in the image. No text.
```

Then re-measure the fist positions in `look.bare.hands` (src/data/classes.ts).

---

# Chapter 2: Rising Waters

27 images: 3 NPCs, 7 monsters, 5 backgrounds, 12 weapons. Same rules as above (Nano Banana, Unlimited ON, a finished character as the style reference). The game already runs without them: missing art falls back to drawn shapes and the procedural marsh. Paste them in any order and say which is which.

## Characters

### 15. `npc_captain_hale` · 3:4
```
A weathered middle-aged outpost captain, short grey beard, a scar over one eyebrow, dark green padded coat under a steel breastplate, high muddy boots, a brass spyglass on his belt, one hand resting on his sword hilt, steady confident stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 16. `npc_wren` · 3:4
```
A young, wiry quartermaster woman, short brown hair under a flat cap, freckles, rolled-up sleeves, a brown leather apron full of tools, a coil of rope over one shoulder, holding a clipboard. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 17. `npc_ottilie` · 3:4
```
A kind elderly healer, round gentle face, long white and pale blue robes, a sea-blue headscarf, a pouch of herbs and a small lantern at her belt, holding a wooden bowl of steaming broth. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```


## Monsters

### 18. `mon_mire_toad` · 1:1
```
A fat, warty swamp toad monster the size of a pony, mottled dark green and brown skin, glowing yellow eyes, a long sticky pink tongue curling out of its wide mouth, small water lilies and algae growing on its back. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 19. `mon_spark_wisp` · 1:1
```
A floating ball of living lightning: a crackling bright yellow-white core with a small mischievous face, violet sparks and electric tendrils trailing below it like a tail, tiny arcs of lightning around it. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid bright green (#00FF00) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 20. `mon_bog_lurker` · 4:3
```
A hulking swamp beast on four legs, part crocodile and part bear, matted hide dripping dark green moss and mud, glowing teal eyes, broad clawed paws, reeds stuck in its fur, a heavy lumbering stance. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 21. `mon_sunken_revenant` · 3:4
```
A drowned undead knight: rusted, barnacle-crusted armor, a tattered dark cloak dripping water, pale blue-grey skin, glowing violet eyes under a broken helmet, seaweed tangled on the shoulders, holding a rusted short sword. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 22. `mon_stormscale_drakeling` · 4:3
```
A young dragon-like lizard the size of a large dog, deep blue scales with bright yellow lightning-stripe markings, a crest of yellow spines, small folded wings, sparks crackling at its jaws, alert crouching pose. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, side view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 23. `mon_bogmother` · 1:1 (mini-boss)
```
A huge, grotesque swamp matriarch boss monster: a massive bloated toad-like body of mud and moss rising out of a puddle, many small glowing yellow eyes, a wide drooping mouth with hanging roots, tiny frogs and leeches clinging to her sides, a crown of reeds and lily pads, menacing pose. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```

### 24. `mon_stormcoil` · 1:1 (Chapter 2 boss)
```
A colossal sea serpent boss monster coiled upright, deep blue scales with glowing yellow lightning patterns, a fin-like crest crackling with electricity, glowing white eyes, lightning arcing between its coils, a dull grey giant scale tangled in its coils, menacing pose. 2D fantasy RPG character art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors, slightly chunky stylized proportions. Full body, standing idle pose, three-quarter view facing right, whole figure visible with empty margin around it. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no floor, no shadow, no text, no border.
```


## Backgrounds

### 25. `bg_fen_road` · 16:9
```
A misty road at the edge of a fen: a muddy dirt road with old wooden planks laid over puddles, tall reeds and cattails, a few gnarled dead trees, still pools reflecting a grey-green overcast sky. The bottom 30% of the image is flat, open, muddy ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 26. `bg_reedwatch` · 16:9
```
A small wooden outpost on stilts in a marsh. From left to right: a watch hut with a green banner, a supply hut with crates and barrels on its porch, a healer's hut with hanging herbs and a lantern, and at the far right a tall wooden watchtower. Reeds and mist, grey-green sky. The bottom 20% of the image is a wide, empty, flat wooden boardwalk running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 27. `bg_sunken_causeway` · 16:9
```
An ancient stone causeway half sunk into a stormy fen: broken stone pillars and arches, dark water on both sides, reeds, slanting rain, distant lightning in a dark grey sky. The bottom 30% of the image is flat, wet, cracked stone causeway running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 28. `bg_bog_hollow` · 16:9 (mini-boss arena)
```
A gloomy hollow deep in a swamp: a large murky green pool at the back, twisted mangrove roots, hanging moss, bubbling mud, drifting pale will-o-wisp lights, dim greenish light. The bottom 30% of the image is flat, muddy ground running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```

### 29. `bg_drowned_shrine` · 16:9 (boss arena)
```
A drowned ancient shrine in a storm: tilted stone columns and a broken dome rising from dark water, a huge dull grey dragon scale set into the altar at the back, crackling lightning in purple-black clouds, rain. The bottom 30% of the image is flat, wet stone floor running across the full width. 2D side-scrolling game background, hand-painted cartoon fantasy style like early-2000s Flash adventure games: bold outlines, soft painted shading, rich saturated colors. Flat side-on view, no perspective tilt. No characters, no people, no animals, no text, no letters, no UI.
```


## Weapons

Aspect **9:16**, file name as listed. Each line is the `<weapon description>` for the weapon prompt above; the full prompt is written out so you can copy it in one piece.

### 30. `wpn_steel_broadsword`
```
A single steel broadsword with a wide polished blade, a simple brass crossguard and a leather-wrapped grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 31. `wpn_yew_staff`
```
A single tall dark yew wood staff, smooth and slightly twisted, the top curling around a pale blue glass orb, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 32. `wpn_fenwatch_stiletto`
```
A single slim steel stiletto dagger with a needle-thin blade, a small round guard and a dark green wrapped grip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 33. `wpn_tidebreaker`
```
A single sea-green steel sword with a wavy edge like a breaking wave, a coral-shaped crossguard and droplets of water floating around the blade, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 34. `wpn_stormcleaver`
```
A single heavy one-handed cleaver sword with a dark steel blade etched with glowing yellow lightning runes, small sparks jumping off the edge, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 35. `wpn_tidecaller_rod`
```
A single short driftwood rod wrapped in seaweed, a large white pearl at the top inside a swirl of water, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 36. `wpn_sparkglass_wand`
```
A single slender wand of fused yellow glass with jagged lightning shapes trapped inside, a copper handle, crackling at the tip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 37. `wpn_brinefang`
```
A single wavy-bladed kris dagger, blue-green steel crusted with white sea salt, a shell-shaped guard, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 38. `wpn_sparkfang`
```
A single dagger made from a curved blue-and-yellow drake fang on a copper hilt, tiny sparks at the tip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 39. `wpn_thunderhead_maul`
```
A single huge war maul with a block-shaped head of storm-grey metal set with a glowing yellow scale, lightning crackling around the head, a long leather-wrapped handle, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 40. `wpn_stormcoil_scepter`
```
A single silver scepter whose top is a cage of coiled silver serpents holding a ball of crackling lightning, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

### 41. `wpn_coilfang`
```
A single long curved serpent-fang dagger, deep blue with glowing yellow veins, a hilt wrapped in blue scaled leather, sparks at the tip, shown alone as a game item. 2D fantasy RPG item art in a hand-painted cartoon style like early-2000s Flash adventure games: bold dark outlines, cel shading with soft painted highlights, warm saturated colors. The weapon stands perfectly vertical: handle at the bottom, tip pointing straight up, centered, the whole weapon visible with empty margin around it. No hands, no character, no stand. Plain flat solid magenta (#FF00FF) background filling the whole image, no gradient, no shadow, no text, no border. Match the art style of the reference image exactly. Only the style, not the character.
```

---

# Armor outfits (`char_<class>_<armor id>`)

Wearing an armor, the hero is drawn from `char_<class>_<armor id>` when that image exists (empty-handed, like `char_<class>_bare`; the weapon is still drawn in the fist). Missing ones keep the default look, so make them in any order. Starting armor (Traveler's Garb, Novice Robe) is the default look and needs nothing.

Attach the class's **bare** hero (`char_warrior_bare`, `char_mage_bare` or `char_rogue_bare`) as the reference, aspect **3:4**, and ask for an edit. Background: magenta for the Warrior and Rogue, bright green for the Mage (same as their hero images).

```
Edit this image (image 1). Change only the outfit: the hero now wears <outfit>. Keep everything else exactly the same: the same face, hair, body, pose, proportions, bold outline style, and the plain flat <magenta / bright green> background. Both hands stay empty clenched fists in exactly the same place. No weapon anywhere in the image. No text.
```

| Armor | `<outfit>` | Files (★ = the class most likely to wear it) |
| --- | --- | --- |
| Leather Armor | a fitted brown leather jerkin with stitched shoulder guards, leather bracers, a wide belt with a brass buckle and leather boots | `char_warrior_leather_armor` ★, `char_rogue_leather_armor` ★, `char_mage_leather_armor` |
| Apprentice Robe | a deep blue apprentice robe with silver rune embroidery along the hems, a hood resting on the shoulders and a rope belt | `char_mage_apprentice_robe` ★, `char_warrior_apprentice_robe`, `char_rogue_apprentice_robe` |
| Scale Mail | a shirt of overlapping polished steel scales over a dark red tunic, steel shoulder plates, a short mail skirt and steel-capped boots | `char_warrior_scale_mail` ★, `char_rogue_scale_mail` ★, `char_mage_scale_mail` |
| Reedweave Robe | a robe woven from pale green marsh reeds and cream cloth, a braided reed sash with small knotted charms, sandals | `char_mage_reedweave_robe` ★, `char_warrior_reedweave_robe`, `char_rogue_reedweave_robe` |
| Marshhide Vest | a dark green oiled hide vest with scaly shoulder pads, hide leggings, a belt of pouches and tall swamp boots | `char_warrior_marshhide_vest` ★, `char_rogue_marshhide_vest` ★, `char_mage_marshhide_vest` |
| Stormweave Robe | a violet robe threaded with thin glowing silver lightning lines, a high collar and silver cuffs | `char_mage_stormweave_robe` ★, `char_warrior_stormweave_robe`, `char_rogue_stormweave_robe` |

After processing, the fist positions get measured and set in the armor's `hands` (src/data/items.ts) if they moved.

