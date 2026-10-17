# Unity port: handoff plan

Started 2026-10-17 (designer: "start making a plan to hand off to Unity: what libraries, assets etc. we need"). Builds on the proposal in `docs/PLANS.md` ("Decisions to keep", Engine). Nothing is built yet.

**Goal:** the same game in Unity 6, built for the browser (WebGL, itch.io) and Windows. Content is written once and shared. Unity becomes the main game once it plays start to finish; until then the Phaser version stays live on GitHub Pages.

## What we have today (2026-10-17)

| Part | Size | Ports how |
|---|---|---|
| Game rules `src/game/` (battle, loot, items, forge, guild, quests, travel, saves) | 3.7k lines, 21 files, no Phaser | 1:1 to plain C# classes |
| Content `src/data/` (+ `BALANCE` in `config.ts`) | 5.1k lines, 18 files, plain data | exported to JSON, read by both versions |
| Screens `src/scenes/` (Title, Create, Room, Battle, Result) | 4.1k lines | rebuilt as Unity scenes |
| UI `src/ui/` (HUD, inventory, shop, forge, guild, map, menus) | 4.9k lines | rebuilt (the UI redo happens here, see below) |
| Effects `src/ui/fx.ts` | 34 effects, 10 particle textures | Particle System prefabs + tweens |
| Sounds `src/ui/sfx.ts` | 39 sounds, synthesized in code | rendered to WAV once, imported |
| Art | 225 images (PNG masters in `art/raw/`, 342 MB) | trimmed 2x PNGs, see Assets |
| Balance tools `npm run sim` / `pace` | 1.1k lines | Unity EditMode tests that print the same tables |

The rules already use a seeded random generator (Mulberry32 in `src/game/rng.ts`). Ported exactly, a seeded fight gives the same result in both versions, so the C# port can be checked number for number.

## 1. Install

| What | Notes |
|---|---|
| **Unity 6 LTS** (newest 6000.x LTS in Unity Hub) | This PC has 6000.0.57f1 (no WebGL module), 6000.2.2f1 (has WebGL), 6000.3.0b10 (a beta). Install the newest LTS with the modules below; 6000.2.2f1 works as a fallback today. |
| Modules: **Web Build Support**, Windows Build Support | Android later, if ever. |
| Template: **Universal 2D** (URP with the 2D Renderer) | |
| IDE: Rider or Visual Studio | Both IDE packages are already in the other Unity projects on this PC. |
| **Unity MCP** (`com.coplaydev.unity-mcp`) | Already used in GachaFront; lets Claude create scripts, scenes and prefabs, run tests and build from a PC chat. |

## 2. Packages

From Unity (free, in the Package Manager):

| Package | Why |
|---|---|
| Universal RP (2D Renderer) | Sorting by feet y (depth), additive glows, 2D lights later. Bloom (URP Volume) for Sigil and element glows works on WebGL 2. |
| Input System | Keyboard, mouse and touch from one setup; rebindable keys. |
| UI Toolkit (built in) | Menus and HUD. Its USS styling is close to CSS, so the hand-made UI mocks (`art/ui/*.html`) carry over nearly as written. |
| TextMeshPro (in uGUI 2.0) | Floating text in the scene: damage numbers, names over NPCs, "Weak!" pops. |
| 2D Sprite, 2D Animation | Sprites now; cut-out rigs (bones on part sheets, decided in PLANS Art) later. |
| Newtonsoft Json (`com.unity.nuget.newtonsoft-json`) | Reads the exported data (optional fields, dictionaries, unions), which `JsonUtility` can't. Also saves. |
| Test Framework | EditMode tests: the C# rules against the TypeScript results. |
| Addressables (later) | Load each chapter's backgrounds when it starts; the art is 23 MB now. Not needed for the first builds. |

Free third-party, one package:

| Package | Why |
|---|---|
| **PrimeTween** (MIT, Asset Store / OpenUPM) | The game uses ~90 tweens (moves, fades, shakes, hit-stops). Fast, no garbage, awaitable. DOTween's free version is the fallback. |

Not needed: Cinemachine (fixed single-screen rooms; shakes and zooms are code), VFX Graph (doesn't run in WebGL builds; use the Particle System), UniTask (Unity 6's built-in `Awaitable` covers the async scene code).

## 3. Assets

| Asset | Today | For Unity | Work |
|---|---|---|---|
| 225 images (backgrounds, monsters, NPCs, hero + outfits, weapons, icons) | game files are WebP, which Unity can't import | trimmed **2x PNGs** with the same crop as the WebP files, so hand anchors and weapon grips stay valid | add `--png <dir>` to `scripts/prepare_art.py` |
| Hand anchors, weapon grip and size | in the data | exported with the data; C# places the weapon in the fist like `figures.ts` | none extra |
| Sprite import settings | | figures: pivot bottom-center; Pixels Per Unit 200 for 2x files (1280x720 design space = orthographic camera size 3.6) | an import preset |
| 10 particle textures (soft, spark, star, leaf, coin, ring, rock, plus, chevron, streak) | drawn at boot | PNGs | save them once from the running game |
| 39 sounds | synthesized at runtime | WAV files | a small page that runs `sfx.ts` through an OfflineAudioContext and downloads each sound |
| ~12 props drawn as shapes (campfire, chest, note, sign, marks, bones, 4 blockers, exit arrows) | code shapes | sprites | generate in the round-15 look with `scripts/redo_art.py` |
| Font | Georgia (a system font; can't be shipped) | a free OFL font as a font asset | pick it with the UI kit (the mocks tried Fredoka, Almendra + Kalam) |
| UI kit (panels, buttons, bars, slots, skill and status icons) | not made yet | made for Unity directly | the UI redo, see below |
| Content | TypeScript data | JSON, one file per table (`unity/Assets/Data/`) | `npm run export-data` |

## 4. How the code maps

| Phaser | Unity |
|---|---|
| `src/game/*` rules | `Drakepg.Rules` assembly: plain C# classes, no MonoBehaviours, so tests run without a scene. RNG ported bit for bit. |
| `src/data/*` + `BALANCE` | JSON + C# data classes mirroring `src/data/types.ts`; a `Db` that loads them once. |
| Scenes (`goTo`) | Unity scenes: Title, Create, Room, Battle, Result; one persistent `GameSession` object. |
| `drawFigure`, hand anchors | a Figure prefab: body sprite, weapon sprite placed by anchor and grip, shadow. |
| `fx.ts` (34 effects) | Particle System prefabs + PrimeTween sequences; per-element bursts as one prefab with element colours. |
| `sfx.ts` | a SoundBank of AudioClips; same names. |
| `view.ts` (2x canvas, `enterScene`) | not needed: Unity renders at screen size; UI Toolkit scales from a 1280x720 reference. |
| `SaveSystem` (localStorage, save v15) | JSON file in `Application.persistentDataPath` (IndexedDB in WebGL). Web saves move over with an Export button in Phaser and Import in Unity. |
| `settings.ts` | PlayerPrefs. |
| Cheat menu | a debug panel in development builds only. |
| `npm run sim` / `pace` | EditMode tests and an editor menu that print the same tables. |

## 5. Order

Each step ends with a check.

1. **Project.** A Unity project in this repo at `unity/` (Library/Temp ignored), so data and art have one source and every chat sees both. **Done when** it opens, builds an empty WebGL page, and the Unity MCP connects.
2. **Data export.** `npm run export-data` writes every table to JSON. **Done when** C# loads every table and the counts match TypeScript.
3. **Rules port** (`src/game` to C#). **Done when** golden tests pass: thousands of seeded fights per class and monster, exported from TypeScript, give the same winner, turns and HP in C#, and the sim tables match.
4. **Assets in.** PNG export, sprites with pivots, WAVs, particle textures, font. **Done when** every key in the data has its sprite or a fallback.
5. **Battle screen**, the core. **Done when** a full fight plays: skills, statuses, intents, weak spots, effects, sounds, drops.
6. **Room screen.** Walking with feet-y depth, exits, NPCs, roaming monsters, chests and mimics, campfires, first-visit scenes.
7. **Menus.** Inventory and shop, character and skills, map, guild, forge, title and create, result.
8. **Saves and the WebGL build.** **Done when** a new hero plays from the road to the Frozen Roost in a browser, and a web save imports.
9. **Switch.** Unity is the main game; itch.io (private page first). The Phaser version is frozen as a reference.

While both versions exist, a rule changed in TypeScript has to be changed twice. So: finish content changes before step 3, or port a system and from then on change it only in Unity.

## 6. Constraints

- Cloud chats can't run Unity: Unity work happens in PC chats (with the Unity MCP). Data export and rule changes can still happen anywhere.
- The browser build needs WebGL 2 (all current browsers). No VFX Graph, no threads.
- The PNG masters in `art/raw/` stay the source; Unity gets trimmed copies (the masters are 342 MB).

## Open (designer)

- **Build the UI redo in Unity, not Phaser** (Claude's recommendation): the UI is the next big job and would otherwise be built twice. Then the port starts before the UI, and the UI kit is made straight in UI Toolkit.
- Same repo (`unity/` folder) or its own repo? Default: same repo.
