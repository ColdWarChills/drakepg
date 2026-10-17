# drakepg in Unity

The Unity port of the game (plan and order: `docs/UNITY_PORT.md`). Unity **6000.2.2f1** with the Web Build module, URP with the 2D Renderer.

## Open

Unity Hub > Add > this `unity` folder. The first open unpacks packages and takes a few minutes.

## Layout

| Folder | What |
|---|---|
| `Assets/Scripts/Rules` | `Drakepg.Rules`: the game rules in plain C#, ported from `src/game/`. No Unity references (`noEngineReferences`), like `src/game` has no Phaser. |
| `Assets/Scripts/Game/Core` | `GameRoot` (the one MonoBehaviour: data, camera, UI document, screen switching with fades, camera shake/pan/zoom/flash), `Anim` (tweens owned by the screen), `Art` (sprites by key), `Design` (1280x720 design px to world, `Palette`), `Saves` (PlayerPrefs, web save format), `Nav` (`RoomArrival`, `BattleSetup`, `BattleResult`, `Keys`, `Dice`). |
| `Assets/Scripts/Game/Screens` | One class per Phaser scene: `TitleScreen`, `CreateScreen`, `RoomScreen`, `BattleScreen`, `ResultScreen` (`GameScreen`: sprites under `World`, UI under `Root`). |
| `Assets/Scripts/Game/Ui` | UI Toolkit built in code, in design px: `Ui.cs` (text, buttons, bars, modals, HUD, toasts, floating numbers), `ItemUi`, `InventoryPanel` (inventory and shop), `CharacterPanel`, `MapPanel`, `GuildPanel`, `ForgePanel`, `CheatMenu`, `Dialogue`. |
| `Assets/Scripts/Game/World` | Things in the world: `Figure` (hero/monster art, weapons in the hands), `Backdrop`, `Shapes` (flat drawn shapes, like Phaser Graphics), `Props` (campfire, notes, signs). |
| `Assets/Scripts/Game/Fx` | Effects (`Fx`, pooled sprite particles, an additive shader in `Resources/Fx`) and sounds (`Sfx`, synthesized into AudioClips at first use, no audio files). |
| `Assets/Tests/EditMode` | Tests for the rules: the C# RNG, and golden fights/loot/forge/guild results exported from TypeScript (`npm run golden`). |
| `Assets/Editor/ProjectSetup.cs` | Settings the port relies on, the UI panel settings, the Boot scene, the WebGL build (menu **Drakepg**). |
| `Assets/Editor/ArtImport.cs` | Import rules for `Resources/Art` (pixels per unit from `index.json`, feet pivots for characters). |
| `Assets/Resources/Data/game.json` | All content, from `npm run export-data` (committed). |
| `Assets/Resources/Art` | The game art, made by `python scripts/unity_art.py` from `src/assets/art` (git-ignored: run it after a fresh clone or an art change). |
| `Assets/Scenes/Boot.unity` | The only scene: a camera and the `GameRoot`. |

Design space: 1280x720 px, x right and y down like the web version; 100 px = 1 world unit, camera size 3.6. Sorting order = feet y x 4. UI is UI Toolkit over the camera, so world effects never cover menus.

After changing content or rules in TypeScript: `npm run export-data` (and `npm run golden` + the EditMode tests if rules changed).

## Batch mode (what Claude runs)

```bash
Unity.exe -batchmode -nographics -projectPath unity -executeMethod Drakepg.EditorTools.ProjectSetup.SetupAndQuit
Unity.exe -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults tests.xml
Unity.exe -batchmode -nographics -projectPath unity -executeMethod Drakepg.EditorTools.ProjectSetup.BuildWebGL
```

The WebGL build goes to `unity/Build/WebGL` (git-ignored). Serve the folder with any static server to play it.

## Known problem: packages fail to unpack (EPERM)

On this PC, Unity's package manager often fails to unpack the bigger packages into `Library/PackageCache` ("EPERM: operation not permitted, rename"), so the project can't open. Not the path length, not a file watcher; plain renames from Node work, so the cause is still unknown. Workaround used for the first setup: copy the same package folders (same `name@hash`) from another project that has them, e.g. `C:\Users\Epic\Marble\Library\PackageCache` (also 6000.2.2f1), then open again. That's why 2D Sprite and 2D Animation are left out for now: no other project has them unpacked.
