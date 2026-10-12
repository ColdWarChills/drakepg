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
