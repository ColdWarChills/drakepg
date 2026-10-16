# Plans (not built yet)

The one list every chat works from. **Decided** = the designer agreed. **Proposed** = suggested, not approved yet. **Open** = needs an answer. Built work moves to `docs/IMPROVEMENTS_2026-10.md`. Nothing here is in the code yet unless it says so.

Combined on 2026-10-14 from three chats ("Progress review and planning", "Drakepg design pass" and the combat design chat).

## Next up

- **Discuss first (designer, 2026-10-15):** the designer wants more control over style and direction ("I just get what you spit out"). The Adventurers' Guild was built on Claude's defaults; review it together (ranks, hunt kinds, numbers, clerks, how the board looks) before building more. From now on, for new systems and UI: show a few short options or a mockup and let the designer pick, then build.

Build order (all built, see "Done" at the bottom):
1. ~~Recall Scroll~~
2. ~~Room features: quiet rooms, campfire, down exit, saving visited rooms~~
3. ~~Map~~
4. ~~Chapter 1-3 room layouts plus drafted story lines for review~~
5. ~~Sim and pacing check, art queue entries~~

- **Review:** every new line (first-visit scenes, notes, signs) is a draft, listed in `docs/ROOM_LINES.md`. Mark what to change; the next chat edits `src/data/world.ts` and deletes that file once approved. Includes the Hunters' Camp lines.
- **Timed run:** the designer times one run per chapter; compare with the pace bot (see "Longer chapters").
- **Playtest by hand** (only the sim, the pace bot and screenshots have checked these so far):
  - New rooms: one timed run per chapter (the pace bot's minutes are guesses). Do the quiet rooms feel good or slow? Are campfires before every boss too generous?
  - Map, Recall Scroll: is the map readable now that pages have 7-9 rooms in a row?
  - Prologue road (3 rooms since 2026-10-15: 1 pest, 2 pests, gate guard): short enough now? Does each class's guard teach "look at Weak:, use that"? Does the Skills button's "Weak!" tag read? Is a level-1 Rogue with only Gas Shot fun (Twin Cut comes at level 2, right after the guard)?
  - Does the "Next:" intent line read at a glance? Does enrage (turn 15, bosses 30) feel fair?
  - Is the drop reveal too slow when farming normal monsters?
  - Is the 5-slot skill swap in the Character panel clear?
  - 8 equipment slots, sets, Lucky Impling.
  - Ch2-3 bosses (retuned 2026-10-14): is a big wind-up every 3rd move fair, or too samey across bosses?
  - Battle effects (built 2026-10-14): too much or too little? Do farming fights feel slower? Easy to tone down per effect in `src/ui/fx.ts`.

## Longer chapters (Ch1-3)

- **Decided:** make chapters much longer than now (estimate before the new rooms: Ch1 15-20 min, Ch2 30-40, Ch3 30-40; not playtested).
- **Decided:** early chapters level up a bit faster, so players feel powerful early, but fights stay risky.
- **Decided:** add rooms to Chapters 1-3 (not only from Chapter 4 on).
- **Decided:** not every room has monsters. Some rooms only move the plot along and build anticipation.
- **Decided:** the early game is easy for every class: kill simple monsters, farm, grind, collect gear.
- **Built (2026-10-14) as proposed:** 8/9/10 rooms (+ town) for Chapters 1/2/3; about half combat (2-3 monsters each), the rest quiet or side rooms; 2-3 combat rooms, then a quiet room; a quiet room with a campfire right before every boss. Same quests and XP curve. New rooms reuse their area's backgrounds; 15 background prompts are in `art/QUEUE.md`.
  - Ch1: Thornwood Path (+ Hunters' Camp below), Fallen Oak, Woodcutters' Clearing (quiet), Deep Thornwood (+ Bramble Hideout below: bandits), Briar Wall (quiet, campfire, the thorn wall), Thornheart Hollow.
  - Ch2: Fen Road, Reedwatch, Sunken Causeway (+ Eel Pools below: Tough eels and a sure chest), Whispering Reeds, Stilt Village (quiet), Bog Hollow, Stormlit Flats, Shrine Steps (quiet), Drowned Shrine.
  - Ch3: Frostfang Pass, Hearthold, Windscour Ridge (+ Steamvent Caves, now with a sure chest, + Hot Springs past them: quiet), The High Trail, Ranger's Lookout (quiet), Snowmaw's Den, Whiteout Ledge (the acolytes), Roost Stair (quiet), The Frozen Roost.
  - Quest gates moved to the quiet room before each boss (thorns, reeds, snowdrifts), so you see the pre-boss room before the quest.
- **Pace bot (`npm run pace`, guessed seconds per action; it fights everything it walks past):** old layout -> new: Ch1 28 -> 32 min, Ch2 33 -> 41, Ch3 46 -> 49. Level at each boss barely moved (Thornheart L5 at hero 5-7, Bogmother L8 at 8-10, Stormcoil L10 at 9-12, Snowmaw L12 at 13-18, Frostbinder L14 at 14-19). Most of the time is quest farming, which more rooms don't change.
- **Decided (2026-10-14):** no side quests now. Build the base first (rooms, systems); quests and side quests come with the story pass. The length gap to the targets waits until then.
- **Changed (2026-10-15):** the errand quests became Adventurers' Guild hunts (see below); only the story quests are left.
- **Proposed:** target lengths about 35 / 55 / 70 min (rough estimates).
- **Open:** keep campfires after a playtest? (Built, easy to remove per room.)
- **Open:** sure chests in side rooms (`room.chest`, first fill each session, never a mimic): keep? Only the Eel Pools and Steamvent Caves have one; Chapter 1 has none because chests unlock after it.

## Adventurers' Guild (built 2026-10-15)

- **Decided (designer, 2026-10-15):** remove most quests. Instead, ranked adventurers: everything is done at an Adventurers' Guild person (just an NPC for now). Early on you can only take the lowest hunts, which fit what you can reach. Hunts are "kill X monsters" or "bring X items from one place" (any monster there can drop them). You can take hunts up to 2 ranks above your own. Hunts match the player's level and strength. S rank is late game; now and then a big S-rank mission.
- **Built with these defaults** (all easy to change, see `docs/IMPROVEMENTS_2026-10.md`):
  - Ranks F, E, D, C, B, A, S. 3/4/5/6/8/10 rank points to the next one. A hunt gives 1 point at your rank, +1 per rank above, none below.
  - A Guild Clerk in each town (Tamsin, Ivo, Edda), one shared board: 5 notices, hold up to 3.
  - Hunt kinds: kill 5 (3 of a Tough monster), gather 4 (50% per kill in that area), Elite bounty (after Chapter 1).
  - Hunt rank from monster level: F 1-2, E 3-5, D 6-8, C 9-11, B 12-14, A 15-19, S 20+; Tough or Elite one rank higher. Pays the XP and gold of the kills it takes.
  - Kept 7 story quests (each chapter's opener and bosses). Removed the 8 errands. Boss quests need a rank: Thornheart E, Bogmother D, Old Snowmaw C. Promotions give the armor the errands used to give.
- **Pace bot** (it takes the 3 hunts with the most points each trip): Ch1 32 -> 23 min (3 hunts, Rank E), Ch2 41 -> 52 min (3 hunts, Rank D), Ch3 49 -> 62 min (4-5 hunts, Rank C), with the shorter gate-guard road. Boss levels barely moved: Thornheart at hero L4-6 (was 5-7), Bogmother 8-11, Stormcoil 9-12, Snowmaw 14-21, Frostbinder 15-21.
- **Not built:** S-rank hunts (no monster is level 20+ yet) and the "now and then a big S-rank mission".
- **Open:** what is a big S mission (a Heroic boss? a named monster? a timed event?), and how often?
- **Open:** a rank-up test (a fight at the Guild) on top of the points?
- **Open:** one clerk in Brindlemoor only, or one per town (built: one per town)?
- **Open:** Chapter 1 is shorter now (a D-rank Ash Imp hunt gives 3 points, so Rank E can take one trip). Raise F -> E to 4-5 points?
- **Open:** a paid "new notices" button, so a board you don't like can be rerolled before a turn-in?
- To do: art for the 3 clerks (`look` shapes now) and icons for the 10 hunt items (drawn icons now). Not in `art/QUEUE.md` yet.
- Playtest: is 50% per kill for gather items right? Are Tough hunts (3 Ash Imps at Rank F) too risky that early?

## UI

- **Decided (designer, 2026-10-15):** the UI needs an overhaul. Do it after the Qwen (ComfyUI) art setup (set up the same day, see Art). The designer is still deciding on the style: discuss options before building.

## Map (built 2026-10-14)

- Built as proposed: **M** or the Map button; one area per page (Old Road, Brindlemoor and the Thornwood, Mirewater Fen, Frostfang Mountains); rooms as dots, exits as lines; "?" for unvisited neighbours; icons for town, boss, campfire; "You" marker; quest marks (! pick up, ? turn in, gold ring = an active quest's monster lives here); dashed red = blocked; arrows to other pages.
- **Changed:** M used to mute the sound. Sound is now only in the Menu.
- **Open:** sealed back exits (like the mountain gate before its quest) stay hidden on the map, same as in the room. OK?

## Recall Scroll (built 2026-10-14)

- Built with the proposed defaults: one way to the last town you walked into, not in battle, 20g in every town shop, new characters start with 5 (`BALANCE.startingRecallScrolls`). Read it from the Inventory or the Map; it asks first.
- **Open:** confirm price and one-way after a playtest.
- Old saves (before v12) also got 5 scrolls, so existing characters can try it.

## Prologue road (built as a placeholder)

- **Decided:** start with the class weapon only, one-way into Brindlemoor (the shop is found on arrival). Claude drafted the story hook.
- **Built (2026-10-15, designer: "too long to get to the city, uneventful", then "first with 1 monster, second with 2, then the boss before the city"):** 3 rooms instead of 6: Overturned Cart (1 pest), Old Mill Road (2 pests), Brindlemoor Gate (Pell and a gate guard per class), each weak to that class's first skill so it teaches weak spots: Warrior Millstone Crab (Blunt: Heavy Strike), Mage Dusk Wraith (Light: Light Bolt), Rogue Hedge Troll (Poison: Gas Shot). See `docs/IMPROVEMENTS_2026-10.md`.
- To do: real monsters (names, stats, art) for `burrow_rat`, `dew_slug` and `hedge_pixie`. They are shape stand-ins now.
- To do: background art for the 3 `road_*` rooms (procedural forest now), and art for Odo and Pell. Not in `art/QUEUE.md` yet. The 3 gate guards are queued (`mon_millstone_crab`, `mon_dusk_wraith`, `mon_hedge_troll`).
- To do: final lines for Odo and Pell (redrafted for the gate guard, listed in `docs/ROOM_LINES.md`).
- **Open:** the designer called the Rogue "ranger" when asking for Gas Shot. Rename the class, or keep Rogue?
- **Proposed:** more Blunt and Poison weak spots later (skeletons, golems and shelled things weak to Blunt; big beasts weak to Poison; slimes could resist Blunt). Only the gate guards use them now.
- **Open:** the hero looks armored with no armor on, because the base class art includes armor. Keep it, or get bare art?

## Story and theme

- **Decided:** the world becomes an original, IP-free magic world with set rules (like Harry Potter, but its own).
- **Decided:** ancient-god dungeons (Greek, Baltic, Egyptian...) come later as DLC/liveops.
- **Decided:** dragons must be super rare, not a common enemy or a boss every chapter.
- **Decided:** the hero is a wanderer. Keep Warrior, Mage and Rogue.
- **Decided:** no rework of the Ch1-3 story for now.
- **Decided:** rooms stay stationary single screens, with vertical (up/down) exits allowed.
- **Proposed:** story rooms always show their short lines, even with story text off.
- **Open:** turn story text on by default? It's off now (`storyText: false` in `src/game/settings.ts`), so players see only each NPC's first line.
- **Open:** give each quest offer a short in-character line too (offers still use the plain reminder line)?
- **Open:** magic-world story spec (rules of magic, who the wanderer is, how it connects to Ch1-3). Not started.

## Dragon clean-up

- **Decided:** dragons become rare (see above).
- **Open:** remove or replace the common dragons: Stormscale Drakeling (Ch2), Vent Wyrmling (Ch3), the dragon story thread, the dragon on the title art. Waiting for go.
- **Open:** `docs/CHAPTER3_SPEC.md` still asks for the dragon's name ("wakes in Chapter 4?"); revisit with the story spec.

## Skills, weapons and builds (discussed 2026-10-15)

Facts from the code at the time: Warrior and Rogue skills take the equipped weapon's element and damage type (daggers are pierce, so Rogue skills scale on DEX); only 11 skills, mostly the Mage's, have a fixed element. Stormcoil (Energy -> Water) and the Frostbinder (-> Wind) change element mid-fight. Gear has no "+element damage" line, only a resist per element. Stat points can't be refunded (only the cheat menu can). Monster weaknesses are -50 to -75, resists up to +75.

- **Built (2026-10-15):** mid-fight weapon swap (Spare slot, Swap costs the turn), see `docs/IMPROVEMENTS_2026-10.md`. Why: players won't own the best weapon of every element, so it keeps pushing upgrades; a weaker backup in the right element still beats a strong weapon in the wrong one.
- **Open:** the spare slot unlocks after Chapter 1 (with Elites and chests), so the Stormcoil is the first boss it matters for. Keep, or from the start?
- **Playtest:** is the Swap button clear? Does a swap feel worth a turn outside the two-form bosses?
- **Proposed:** skills come from drops and town vendors, not only from levels, even some early ones. Not all of them strong. The strongest ones come from known sources (a boss's first kill, Heroic rematches, a Lucky Impling, a side room's sure chest), not random trash drops (bad-luck protection rarely kicks in).
- **Proposed:** element skill families: the same skill in different elements (beam of fire, of water, of light...), so a player can build e.g. a fire Rogue. The shape is per class (e.g. Warrior strike, Rogue knives, Mage beam), the elements are shared. Damage type follows the class's weapon, so a Rogue's element skills scale on DEX, a Mage's on INT.
- **Proposed:** a spellbook that shows every skill: unknown ones as dark pages with a hint where to find them, tabs per element (HoMM3-like).
- **Proposed:** weapons require a minimum of their class's main stat to equip. Investing in another stat opens another class's weapons (hybrids, used through the swap).
- **Open:** which skills move to drops/vendors, and do the level unlocks stay for the rest? Books for your own class only, or cross-class?
- **Open:** a hybrid needs a payoff: a sword for a Rogue is weak unless STR is invested. Weapons that scale on two stats? (Not strictly better than a pure build.)
- **Open:** a paid respec at a trainer, so a failed hybrid isn't permanent?
- **Open:** a "+element damage" gear line or element sets, so an element build is more than weapon + skills.
- **Open:** weight weapon drops toward the player's class, so stat-locked drops don't pile up?
- Dropped: replacing levels with gear-only power. Levels stay.
- **Built (2026-10-15):** first skills per class: Warrior Heavy Strike is now Blunt (and Shield Bash), Mage starts with Light Bolt (Light; Staff Bolt moved to level 2), Rogue starts with Gas Shot (Poison; Twin Cut moved to level 2). Weak spots besides elements: `weakTo` Blunt and Poison, shown on the "Weak:" line; skill buttons say "Weak!" when a skill lands on one.

## Combat and balance

- **Decided:** monster rebalances are fine and expected often. Keep numbers small (no thousands early), especially weapons.
- **Decided (2026-10-14):** no fix for the Mage vs Thornheart at level 4 with starter gear (3%). Rushing the boss without gear may be hard; with shop gear it's 75%, at level 5 97%.
- **Built (2026-10-14):** Ch2-3 bosses wind up their big hit every 3rd move, like the Thornheart, and hit harder (their damage had fallen behind hero HP: normal hits took 7-13% of max HP vs the Thornheart's 13-18%). Sim, shop gear at the intended level: smart AI wins 83-100%, the random-button bot 52-76% (was 75-98%), close to the Thornheart's 47-71%. Old Snowmaw's test stays Fire (with it: 89-100% smart).
- **Proposed (built as the default, 2026-10-14):** first-kill bosses are tuned for shop gear; a full set + jewelry makes them easy (random bot ~95-100%) as the reward for farming. Trash was left as is (early game easy).
- **Open:** Heroic rematches are still easy with a full set (random bot 94-100%). A bigger Heroic damage multiplier hurts players without a set long before it touches set-geared ones, so it stays x1.25. Needs new Heroic moves (beats, a second phase), or a higher tier later.
- **Open:** Lucky Impling catch rates in the sim: Warrior ~9-85%, Mage ~18-87%, Rogue ~39-100% (worst in late areas). Tune later.
- **Open:** the longer chapters need one timed run by hand (the pace bot is done, see "Longer chapters").
- **Open:** no roll stone for the Power line of rings and necklaces.
- Watch: Old Snowmaw without Fire is a 0-4% win for every class (intended: the quest before her collects Fire Essence). With Fire she's 89-100% for the smart AI. The sim's Mage now carries Meteor and Ember Bolt for her (it had no Fire spell in its default 5, which made her look much harder for Mages).
- Watch: stacked-affix Elites on Tough monsters are harsh (Frenzied Ash Imp at level 3: 6-72%; Ironhide Drakeling: 15-77%). They can be fled.
- Watch: in normal fights the random bot does about as well as the smart AI. Accepted: the tension is in attrition between fights.

## Art

- **Decided (2026-10-15, art Q&A): run art direction tests before redoing the graphics.**
  - Why it looks "480p": the game draws at 1280x720 and the canvas is stretched to the screen; backgrounds are stored at 1280x720 and sprites at 480px tall. Target is the designer's **1440p monitor**: render at the screen's real size and keep art at **2x** (2560x1440 backgrounds, ~960px sprites). Frame drops also need finding. Runs in parallel with the art tests.
  - **4 directions to test:** Flash cartoon, Painterly, HD pixel art, Storybook ink.
  - **Same test set per direction:** battle screen mock (hero vs monster on a room background, HUD on top), the 3 class heroes (bare), a monster trio (cute trash, tough, boss), one exploration room and 4 item icons, plus a cut-out rig test (one hero split into parts, short idle + attack).
  - **Judge by what reads as AI to the designer:** too much detail, inconsistent style between images, nonsense details (melted straps, broken hands, armor that doesn't connect). Glossy is fine.
  - **Style source:** Civitai style LoRAs on the local Qwen for the tests; after the pick, train our own LoRA on the best ~20 images so all later art matches. Any license is fine for tests; the final style must be sell-safe (our own LoRA covers that).
  - **Animation:** cut-out rigs (parts that swing/bend, old-Flash-RPG style). Favors Flash cartoon and Painterly; pixel art suffers when parts rotate. Rigged art needs parts, so the redo makes sprites as part sheets.
  - **Cinematics:** more in-engine scenes (like first-visit: letterbox, pans, characters moving) and boss intros (close-up splash, name, sound hit). No AI video clips, no full-screen story panels for now.
  - **Open:** which direction wins; then the redo order. The 13 paused armor outfits wait for this (a new hero look redoes all of them anyway).
  - **Decided (2026-10-16, after the direction tests): none of the four as tested.** Backgrounds: the designer likes all four. Characters look bad in every direction: Flash reads as Russian animation (not wanted), Painterly is nice but clearly AI, Pixel is fun but not the way to go, Ink is alright but has a lot of detail. The cut-out rig GIFs looked horrible. Too many images per round: rethink the character look first, then run small tests, starting without a reference or LoRA to see what Qwen gives. Comparison page: https://claude.ai/artifact/AQS37cnLwyTYf9z9WXAB2N (copy in `art/tests/compare/`).
  - **Decided (2026-10-16, round 2): characters go "Chunky indie".** Six low-detail looks on the Warrior (plain Qwen, no LoRA, no reference; `art_tests.py` looks `cartoon`, `cel`, `gouache`, `chunky`, `storylight`, `papercut`; gouache and paper cut-out didn't take without a LoRA). Picked: Chunky indie (bold angular shapes, thick outlines, flat colours with hard shadows, minimal detail). Mage, Rogue and the Boar followed in the same look (`art/tests/chunky_set.png`, mock on the Painterly forest `art/tests/chunky_mock.png`). Animation: decide after the look.
  - **Research (2026-10-16, asked by the designer: which style sells best with AI art):** no style sells because of AI; the disclosure itself costs. A controlled look at 2025 Steam releases found AI-disclosed games got ~53% fewer reviews (a sales proxy) than similar games, more so for strong games; median positive 84.6% vs 88.3% ([Game Oracle](https://www.game-oracle.com/blog/ai-part2)). Still, 45 AI-disclosed games grossed $1M+, half of them using AI for in-game art, mostly small parts: icons, backgrounds, store images ([Totally Human](https://www.totallyhuman.io/blog/games-with-ai-disclosures-have-grossed-an-estimated-660m-on-steam)). Players punish the generic glossy/painterly look and characters that don't match; a deliberate, consistent style rarely gets called out. Top indie looks overall: pixel art, bold stylized cartoon with strong silhouettes (Hades), low-poly 3D (Schedule I). Takeaway: AI backgrounds/icons are the low-risk use; characters need one consistent, low-detail look (our own LoRA) and hand fixes.
  - **Round 3 (2026-10-16):** Chunky turned out "very edgy". The designer asked for something like Supercell's look, or a new style. Six rounder looks on the Warrior (`art_tests.py` `mobile3d`, `mobile2d`, `clay`, `toy`, `plush`, `softstory`; sheet `art/tests/looks_round3.png`). The two mobile looks needed "about three and a half heads tall, huge hands" spelled out before they got stubby. No studio or game is named in prompts.
  - **Decided (2026-10-16): no 3D-looking characters, the game is 2D.** Out: Mobile cartoon 3D, Clay, Vinyl toy, Felt plush. Still in: Mobile cartoon 2D and Soft storybook.
  - **Shortlist (2026-10-16):** Mobile cartoon 2D, Soft storybook, and the designer's mix (Mobile 2D shapes with Soft storybook colour and line, `art_tests.py` look `mix`), each with Warrior, Mage, Rogue and Boar; sheet `art/tests/shortlist.png`, battle mocks on the Ink room `art/tests/shortlist_mocks.png` (`python scripts/art_compare.py grid mobile2d softstory mix`). Monster prompts swap "huge hands" for "big chunky feet" (the boar got boots and hands otherwise).
  - **Round 4 (2026-10-16):** nothing in the shortlist stood out. The designer asked about looks inspired by JoJo's Bizarre Adventure and Solo Leveling (prompts describe traits only, `art_tests.py` looks `manga`, `webtoon`; sheet `art/tests/looks_round4.png`). Plain Qwen gave generic 90s anime for the first (no bizarre poses or heavy hatching; that would need a LoRA) and a sleek, polished webtoon look for the second. Suggested to the designer: park the redo, keep the current art, re-check it once the sharpness fix lands; characters stand out through design (silhouette, one striking trait), not rendering style.
  - **Round 5 (2026-10-16):** the designer liked both the manga and the webtoon take and asked for variations: manga heavy ink, retro anime cel, pop colours; webtoon warm, shadow, flat (`art_tests.py` `VARIATIONS`; sheet `art/tests/variations.png` via `python scripts/art_compare.py variations`).
  - **Round 6 (2026-10-16):** the designer's mixes, the dark and the warm webtoon palettes painted flat (`webtoon_darkflat`, `webtoon_warmflat`; sheet `art/tests/flat_mixes.png` with the glossy versions under them, `python scripts/art_compare.py flatmix`).
  - **Round 7 (2026-10-16):** the designer shared a flat graphic poster they liked (`art/tests/refs/poster_ref.png`, source unknown, likely AI: tests only). "Graphic poster" look (`art_tests.py` `poster`: limited warm palette of burnt orange, amber, cream and maroon, flat hard-edged shapes, no outlines, bold silhouettes, warm backlight with a bright rim, minimal detail). From the description alone it matched better than with the picture as a style reference (`poster_ref` drifted teal). First look where hero, monster and room share one palette and light. Sheet with battle mock: `art/tests/poster_test.png`.
  - **Open:** graphic poster vs the flat webtoons (plain, dark, warm); then the full cast (Mage, Rogue, Mossling, Thornheart, icons) and how other areas (marsh, snow) keep the look with their own palettes; animation.
  - **Test round notes (2026-10-15/16, branch `claude/art-direction-tests-02b582`):** test LoRAs (Qwen 2.1, from Civitai, in `ComfyUI\models\loras\art_tests\`; downloads need a Civitai login): Flash = [Soyuzmultfilm](https://civitai.com/models/2817513) (selling images OK), Painterly = [Disco Elysium](https://civitai.com/models/2487755) (OK), Ink = [Sidney Sime](https://civitai.com/models/2986328) (OK); alternates [Cinematic Painterly](https://civitai.com/models/2967890) (OK, credit required) and [Dream Loom](https://civitai.com/models/2605989) (OK) lost to them. No Qwen 2.1 pixel LoRA exists: pixel art is plain Qwen + a snap to a real pixel grid (`art_tests.py`, 4x, 64 colours). Done: pixel set + rig, Flash 9 of 11 subjects. Left: Flash ring/cap + rig, Ink and Painterly sets + rigs, self-check, `python scripts/art_compare.py`, show the page. Scripts: `scripts/art_tests.py` (subjects/directions), `scripts/rig_test.py` (cut-out rig: base + no-arms edit, then build), `scripts/art_compare.py` (battle mock + page). Full-size images stay local in `art/tests/` (git-ignored except the page).
- **Was decided:** keep generating with Higgsfield for now; redo the art later with Qwen at home if the polished look reads as AI. Files swap by key, so nothing in code changes. (Superseded by the direction tests above.)
- **Set up (2026-10-15):** ComfyUI 0.40 portable + Qwen-Image 2.1 int8 on the designer's PC (`C:\Users\Epic\ComfyUI_windows_portable`, start with `run_nvidia_gpu.bat`, UI at http://127.0.0.1:8188). RTX 5070 Ti: ~20-50 s per image (Higgsfield ~8 min). It writes a real alpha channel (no magenta/green keying) and takes up to 10 reference images; with `mon_thornback_boar` + `mon_mossling` as style refs, a test Mire Toad matched the game's look. Generate with `scripts/qwen_batch.py` (in this repo).
- **Built (2026-10-15):** `scripts/qwen_batch.py` runs `art/QUEUE.md` prompts on the local ComfyUI with the game's art as style references, and `prepare_art.py` takes transparent PNGs. See `docs/IMPROVEMENTS_2026-10.md`.
- **Open:** what to redo with Qwen (the 13 paused outfits, the road art, anything that reads as AI), and whether new art should only come from it from now on.
- **Open:** 20 images left in `art/QUEUE.md`: the 3 gate guards (new), 13 armor outfits (paused, see below), 3 waiting for the dragon clean-up (Stormscale Drakeling, Vent Wyrmling, `bg_frozen_roost` with its sleeping dragon) and the dropped Volt Eel. Road art isn't queued yet (see Prologue road).
- **Open:** armor outfits (paused 2026-10-15 at 17 of 30: Warrior 9, Mage 8, Rogue 0). Each is an edit of the class's bare art, so a new hero look means redoing all of them. Only body armor changes the picture, but each outfit paints its own gloves and boots (the Mage's Ridgeplate has steel gauntlets and boots), so the picture can disagree with the gloves/boots actually worn. Every new body armor costs 3 images. Options: torso-only outfits, layered pieces per slot, or keep the class look and show armor only in the UI.
- **Open:** look-alike icons: Rimeglass Ring vs Frostward Ring, Tidepearl Ring vs Moonstone Ring. Regenerate one of each (~3 credits)?
- Dropped: Volt Eel art (the eel was only a sample). Its prompt `mon_volt_eel` is still in the queue.

## Parked

- **#7 Class choice before seeing combat:** not important now. Options: a free class swap until the end of Chapter 1, or a one-fight trial per class on the Create screen.
- **#8 Bad-luck protection kicks in too late:** agreed, not urgent. `pityAfter` is 80 dry kills per area; a chapter is roughly 40-50 normal kills, so it may never trigger. Options: lower to 25-30, count across all areas, or cut it.
- **#9 Endgame:** no goal yet, on purpose. Heroic boss tier is built. Open: more tiers later (new moves each, never more HP)?
- **#10 System overlap audit:** agreed in principle. First candidates: hone vs reinforce, and normal mods vs Sigil mods.

## Decisions to keep

- Engine (2026-10-15): stay on Phaser in the browser for now. A Unity port was floated (the designer finds testing easier there) and set aside: it's a full rewrite, and the cloud chats can't run Unity. The game is made for the designer's own enjoyment; selling a few copies (itch.io, or Steam as a wrapped web build) is optional, and disclosing the AI art on a store is fine. Re-asked 2026-10-15: **Unity, but later**: finish the content in Phaser, port once the design stops changing. Make new art large enough (2x masters, rig parts) to carry over.
- Testing tools (2026-10-15): no pause/play/inspector (dropped as bloat). Built instead: Rates x1-x10 in the testing menu, to see drops and level fast.
- Skills stay simple (Silkroad style): skills just do damage. No interrupts, silences or similar for now.
- Enrage, not a hard turn limit. Heroic adds no HP. Only specific monsters absorb (resist 150).
- Element names stay Earth and Energy.
- Elemental weapons are part of the puzzle: carrying the wrong element should hurt. Absorbing monsters (Volt Eel, Ember Salamander) are the first case. Open: add the example weapons from the discussion ("leaf blade" as Earth, "lightning dagger" as Energy)?
- Drop reveal: keep the full result screen; reveal timing scales with how good the drop is.

## Smaller open items

- Economy review was skipped: the forge's gold costs and late-game gold flow weren't checked.
- `docs/CHAPTER2_SPEC.md` "Later": player marketplace (needs a backend), Alchemy, mobile touch controls, itch.io release.
- `docs/CHAPTER3_SPEC.md` "Open questions": does Hearthold get its own smith?

## Done since these notes were written

- 2026-10-14: #7 combat pass, #8 prologue road, roll stones + tier caps, and the 8 slots / sets / Lucky Impling branch are all merged and live. `LUCKY_ON_NEW_GAME` is off.
- 2026-10-14: build steps 1-3: Recall Scroll, quiet-room features (first-visit scene, inspect, campfire, down exits), saved visited rooms and last town (save v12), and the map. Placeholder side room: Hunters' Camp.
- 2026-10-14: battle effects pass (wind-ups, element impacts, crits, combos, statuses, deaths), see `docs/IMPROVEMENTS_2026-10.md`.
- 2026-10-14: build steps 4-5: Chapter 1-3 room layouts (8/9/10 rooms), drafted lines (`docs/ROOM_LINES.md`), sure chests in side rooms, `npm run pace`, 15 background prompts in `art/QUEUE.md`.
- 2026-10-15: spare weapon and mid-fight Swap (save v13), see `docs/IMPROVEMENTS_2026-10.md`.
- 2026-10-15: testing Rates x1/x2/x5/x10 (XP, gold, drop chances) in the testing menu.
- 2026-10-15: Adventurers' Guild: ranks F-S, hunts from a Guild Clerk in each town, 8 errand quests removed (save v15). Was parked as #6 Hunter contracts.
- 2026-10-15: Higgsfield art run: 3 Hearthold NPCs, 16 monsters, 25 backgrounds, 24 item icons and 17 armor outfits, see `docs/IMPROVEMENTS_2026-10.md`.
- 2026-10-15: shorter prologue road with a gate guard per class, Blunt/Poison weak spots, Light Bolt and Gas Shot (save v14), see `docs/IMPROVEMENTS_2026-10.md`.
