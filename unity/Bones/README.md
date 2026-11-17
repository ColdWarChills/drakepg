# Bones

The hero on bones: part pictures and setup files. Tracked, but outside `Assets`, so Unity doesn't import them and nothing here ships in the game yet.

- `hero/rig.json`, `hero/poses.json`, `hero/fight.json`: the bones, each part's pivot, the old test poses, the faces. Made by `scripts/hero_layers_rig.py` and its neighbours.
- `hero/bare/`: the 15 bare body parts.
- `hero/<gear id>/`: the same part names, drawn for one gear piece.
- `hero/fight/face_*.png`: swap faces (shout, hurt), one per helm.
- `hero/*.png`: weapons.

## Rules
- Never overwrite a kept picture. A new try gets a new name; the old one stays until the designer's yes.
- Saved on 2026-11-17 from `art/redo/layers/parts` (not in git). The drawn stances in its `fight/<moment>/` folders (57 MB) stayed there.
