# Hero animation brief (2026-11-23)

**Read `docs/HERO_ANIMATION_LESSONS.md` first (2026-11-25).** It says why the tries below failed, why the Dragon Crashers knight works, and the rules for any new try.

Read this before any work on the hero's body, poses, gear-on-hero or attack animation. It replaces the open hero threads in `docs/PLANS.md` ("Layered hero", "Hero on bones", "Overnight runs"). The designer handed the technical decisions to Claude; they are made below and are not reopened unless a step fails its tests twice (see "Stop rule").

The goal, in the designer's words: unique, powerful, fluent; every equipped item visible on the hero.

## 1. Decision

- **Tool: Unity's 2D Animation package** (`com.unity.2d.animation`, free, from Package Manager). Bones are Transforms; parts are SpriteRenderers on them; gear and swap drawings go through **Sprite Library + Sprite Resolver**; arms and legs use **Limb Solver 2D** (IK); moves are **Animation clips** (data, editable in the Animation window).
- **No custom rig code.** `LayeredHero.cs`, `LayeredFight.cs` and `SkeletonSwing.cs` (1,581 lines in `unity/Assets/Scripts/Game/Test/`) stay as reference only. Do not extend them and do not write a new rig system.
- **Why this tool:** it covers what Spine would give this game (per-part gear swap = Spine's "mix and match" skins), it is already inside the engine, and it is documented with sample projects. Each session writing its own rig is part of why the last weeks looped (section 2).
- **Not Spine:** not owned; not needed for the above. (Essential is $69 on sale and has no meshes; Professional $369.)
- **Not Blender, for now:** Blender can do Spine-style cutout rigs with the COA Tools add-on (COA Tools 2 supports Blender 3.4+, version 3.0.0 for Blender 4.x). The original COA Tools exports JSON and ships a Godot importer; I found no Unity importer, so using it means writing one, which is custom rig code again. Rendering Blender animation to sprite sheets breaks per-part gear: a sprite sheet doesn't know where a hat sits, so every gear combination would need its own sheet (Blightbound's finding, see References).
- **Plan B, only if the 2D base fails its tests twice:** a real-time 3D, cel-shaded hero (as in Darkest Dungeon II and Guilty Gear Xrd). Gear is then meshes on one skeleton. Not started; needs the designer's yes because it changes the look.

## 2. Why the last attempts failed (checked in the repo at commit 30ba9ff, Nov 17)

The designer's version from Nov 22 (a 3D try) is not pushed, so it is not covered here.

1. **The elbow flips sides at the start of moves.** In `SkeletonSwing.cs` the elbow side is a sign, `NBend` (+1/-1). `Mix()` (line 135) sets `NBend = b.NBend`, so the moment a blend starts, the elbow jumps to the next key's side. Ready is +1, Wind and Coil are -1, Strike is +1: the elbow pops to the other side of the arm at the start of the wind-up and again at the strike.
2. **The hand is one picture, rotated.** The hand angle follows the blade (`hand = Norm(Blade - grip angle)`, line 120). Blended the short way, the blade turns about 110° into the wind-up (-45° to 205°) and 160° into the strike (205° to 5°), and the one fist picture turns with it; no other hand picture is ever swapped in. A real wrist can't turn a fist that far without showing the palm or the back, so this fits the designer's "hands are inverted". Likely cause, not confirmed: the grip angle is in `rig.json`, which is not in the repo.
3. **Parts were never designed for rotation.** They were cut out of whole AI pictures with fixed masks, so there are no round overlapping joints (seams at the shoulders were noted in PLANS).
4. **A new rig system per session.** The 2D Animation package was proposed on Nov 17 but never installed (`unity/Packages/manifest.json` has no `com.unity.2d.*` package); each attempt hand-wrote its own IK, blending and part loader.
5. **Judged on stills.** Several steps were judged from still sheets (the hands step had no clip; the designer judged the bones test on stills of the wind-up and strike). Rotation problems only show in motion.

## 3. Building the base (rules)

1. **Joints:** every limb joint is two overlapping circles with the pivot at the circle's center, so the joint never opens up. Limb parts are drawn straight, ends rounded. Very deep bends show a gap: use a bent-limb swap drawing there.
2. **Swap drawings instead of rotating past what a drawing can show** (what Blightbound did): hands (grip near, grip far, open, palm, back), faces (calm, shout, hurt), torso (front, turned away for wind-ups, turned toward the viewer for strikes). A hand drawing is never rotated more than about 90° from how it was drawn; past that, swap.
3. **Elbow and knee side is fixed for a whole move.** Set Limb Solver 2D's `flip` once per limb. If a move truly needs the other side, change it on a held frame where the arm is hidden or straight, never by blending.
4. **Gear:** one picture per item per body part, drawn on that part's template (same canvas size, same pivot, same joint circles). Sprite Library: category = body part, label = item id. Unity's rule: the skeleton must be identical between sprites that swap.
5. **Art source:** the bare template parts are made exact (vector or hand-drawn). AI may only paint inside a part's locked outline. A part whose outline moves fails the gear check. (Untested: whether Qwen can stay inside a locked outline. Test that on one part before doing a set.)
6. **Mesh bending** (Sprite Skin with bone weights) only where rigid parts look wrong, e.g. a cloth sleeve. Rigid parts first.

## 4. Making moves powerful and fluent

- **Key poses first.** Block the move in a few strong key poses, get the timing right with as few as possible, and only then add in-betweens (how Dead Cells animated, even in 3D).
- **Hold key poses** instead of smoothing everything. Guilty Gear Xrd removed in-between frames on purpose for a more animated feel, and swapped parts instead of scaling or deforming models.
- **Smear on the fastest frame:** a blade smear sprite (or a stretched swap drawing) on the strike frame makes the attack feel faster.
- **Hit stop is already in the game** (four beats, freeze and camera kick per hit, PLANS "Battle animation"). For reference, Capcom's example is 8, 12 and 16 frames of hit stop for light, medium and heavy hits, on both fighters.
- **What flat 2D can't do:** turn the head away or swing a sword horizontally with perspective. Those need swap drawings (Blightbound).
- Explore several takes per weapon kind (designer, Nov 17), but every take has to pass section 5.

## 5. Tests: every step ends with these, recorded as a clip

1. **Joint sweep:** each joint rotated through its full range at slow speed. No gap, no seam, nothing pops.
2. **Bend check:** no elbow or knee changes side during a move.
3. **Hand check:** the thumb is on the correct side in every key, and the weapon stays inside the fist.
4. **Gear check:** each worn piece goes through the same sweep; its outline matches the bare part.
5. **Move check:** the attack at game speed, beside today's hero. Only this one goes to the designer for judgment; 1-4 are Claude's to pass first.

**Stop rule:** if a step fails its tests twice, stop. Report what failed, with the clip, and ask. No third variant.

## 6. Order (one at a time; the next starts after the designer's yes)

1. Install `com.unity.2d.animation` (use the version Package Manager offers for Unity 6000.2; the "IK chain flips 180°" bug was fixed in 9.1.1 and 10.1.2). Import its samples and study "Part Swap" and the "Character" sample (`Fei.psb`). Then import Dragon Crashers into an empty test project and open one of its characters: the designer saw it on Nov 23 and said "look really good, we have a little different style, but might be able to adapt", so its skeleton, IK setup and swap categories are the starting point for step 2 (our own parts on a rig built the same way), not a blank rig. No designer review.
2. Bare base: template parts, rig, IK, joint sweep clip. Designer reviews the clip.
3. One sword attack on the bare base, passing tests 1-3 and 5.
4. One armor set (helm, chest, gloves, legs, boots) on the template, passing test 4.
5. Other weapons, then the rest of the outfits.

## 7. References: study these

- **Unity 2D Animation samples** (in Package Manager, under the package's Samples): Part Swap, Full Skin Swap, Character (Fei.psb, rigged from a PSB), Single Skinned Sprite. Shows the swap-per-part setup this game needs.
- **Dragon Crashers**, Unity's free official 2D sample (Asset Store): skeletal characters, IK and sprite swap (e.g. mouth drawings as a swap category). Its Asset Store page lists Unity 6000.3.0f1 and 6000.0.32f1 as compatible (checked Nov 23); nobody has opened it on this project's version yet.
- **Blightbound toolchain article** (Ronimo): the closest case to ours. 2D skeletal plus frame-by-frame on parts (hands, faces, chests/torsos swapped), gear swappable at runtime, about 65 MB of textures for every character and enemy across 4,600 parts.
- **Joint construction:** Adobe Community "Types of joints used in digital cutout animation"; Toon Boom Harmony "Fixing the articulations".
- **Spine mix-and-match docs:** the concept of combining per-item skins at runtime (same idea as Sprite Library categories).
- **Power and timing:** Dead Cells 3D-for-2D pipeline (Thomas Vasseur, Game Developer); Guilty Gear Xrd limited animation; Capcom SF seminar on hit stop; GDQuest "Juicing up your game attacks" (anticipation, smear).

## Sources

- Unity 2D Animation, Sprite Swap: https://docs.unity3d.com/Packages/com.unity.2d.animation@7.0/manual/SpriteSwapIntro.html
- Unity 2D Animation, Sprite Swap examples: https://docs.unity3d.com/Packages/com.unity.2d.animation@9.1/manual/ex-sprite-swap.html
- Unity 2D Animation, frame-by-frame with Sprite Resolver: https://docs.unity3d.com/Packages/com.unity.2d.animation@7.0/manual/FFanimation.html
- Unity 2D Animation, PSD Importer character sample: https://docs.unity3d.com/Packages/com.unity.2d.animation@8.0/manual/ex-psd-importer.html
- Unity 2D Animation, preparing artwork (.psb only): https://docs.unity3d.com/Packages/com.unity.2d.animation@9.0/manual/PreparingArtwork.html
- Unity 2D IK, Limb solver flip: https://docs.unity3d.com/Packages/com.unity.2d.ik@1.0/manual
- Unity issue DANB-582 (IK chain flips 180°): https://issuetracker.unity3d.com/issues/rotating-the-ik-bone-chain-in-certain-angles-can-cause-it-to-flip-180o
- Dragon Crashers: https://unity.com/blog/games/get-to-know-dragon-crashers-our-latest-2d-sample-project
- Blightbound toolchain: https://www.gamedeveloper.com/programming/finding-a-suitable-toolchain-for-animating-blightbound-s-2d-characters
- Cutout joints: https://community.adobe.com/questions-540/types-of-joints-used-in-digital-cutout-animation-106544
- Toon Boom articulations: https://docs.toonboom.com/help/harmony/Content/HAR/Stage/011_Character_Building/028_H2_Fixing_the_Articulations.html
- Spine mix and match (Unity): https://en.esotericsoftware.com/spine-unity-mix-and-match
- Spine prices: https://esotericsoftware.com/spine-purchase
- COA Tools 2 (Blender): https://github.com/Aodaruma/coa_tools2
- COA Tools (original, JSON export, Godot importer): https://github.com/linuxtopia/coa_tools
- COA Tools 3.0.0 for Blender 4.x: https://blenderartists.org/t/coa-tools-dead/1479623/
- Dead Cells uses 3D models rendered as 2D sprites: https://80.lv/articles/interview-with-the-developers-of-dead-cells
- Dead Cells keyframe-first animation (Thomas Vasseur's Game Developer article "Art Design Deep Dive: Using a 3D pipeline for 2D animation in Dead Cells"; full text in a Russian translation): https://habr.com/post/416589
- Guilty Gear Xrd removed frames, swapped parts: https://gamingbolt.com/15-video-games-with-the-best-animation/15
- Capcom hit stop: https://game.capcom.com/cfn/sfv/column/131545?lang=en
- GDQuest juicy attacks: https://www.gdquest.com/library/juicy_attack
- Darkest Dungeon II 3D characters, 2D backgrounds: https://tvtropes.org/pmwiki/pmwiki.php/Videogame/DarkestDungeon2
