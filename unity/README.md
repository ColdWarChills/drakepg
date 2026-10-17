# drakepg in Unity

The Unity port of the game (plan and order: `docs/UNITY_PORT.md`). Unity **6000.2.2f1** with the Web Build module, URP with the 2D Renderer.

## Open

Unity Hub > Add > this `unity` folder. The first open unpacks packages and takes a few minutes.

## Layout

| Folder | What |
|---|---|
| `Assets/Scripts/Rules` | `Drakepg.Rules`: the game rules in plain C#, ported from `src/game/`. No Unity references (`noEngineReferences`), like `src/game` has no Phaser. |
| `Assets/Scripts/Game` | `Drakepg.Game`: scenes, figures, effects, UI (MonoBehaviours). |
| `Assets/Tests/EditMode` | Tests for the rules. `RngTests` checks the C# random generator against values printed by `src/game/rng.ts`. |
| `Assets/Editor/ProjectSetup.cs` | Settings the port relies on, the Boot scene, the WebGL build (menu **Drakepg**). |
| `Assets/Art` | Sprites, imported at 200 pixels per unit (the 2x art). |
| `Assets/Scenes/Boot.unity` | First scene. |

Camera: orthographic, size 3.6, so the 1280x720 design space is 12.8 x 7.2 units and a 2560x1440 background at 200 PPU fills it exactly.

## Batch mode (what Claude runs)

```bash
Unity.exe -batchmode -nographics -projectPath unity -executeMethod Drakepg.EditorTools.ProjectSetup.SetupAndQuit
Unity.exe -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults tests.xml
Unity.exe -batchmode -nographics -projectPath unity -executeMethod Drakepg.EditorTools.ProjectSetup.BuildWebGL
```

The WebGL build goes to `unity/Build/WebGL` (git-ignored). Serve the folder with any static server to play it.

## Known problem: packages fail to unpack (EPERM)

On this PC, Unity's package manager often fails to unpack the bigger packages into `Library/PackageCache` ("EPERM: operation not permitted, rename"), so the project can't open. Not the path length, not a file watcher; plain renames from Node work, so the cause is still unknown. Workaround used for the first setup: copy the same package folders (same `name@hash`) from another project that has them, e.g. `C:\Users\Epic\Marble\Library\PackageCache` (also 6000.2.2f1), then open again. That's why 2D Sprite and 2D Animation are left out for now: no other project has them unpacked.
