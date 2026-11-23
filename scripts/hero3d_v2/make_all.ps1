# Rebuild the clean 3D hero (round 2) and every judging picture (PowerShell). Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File scripts/hero3d_v2/make_all.ps1
# Needs Blender 5.2 (Steam) and a Python with numpy + Pillow + scipy (the Animation Workflow venv).
# Writes art/redo/hero3d_v2/ (model) and art/tests/hero3d_v2/ (pictures), then copies both into their round2\ folders.
# Never writes into a round1\ folder (the archive), round1_clip\ (step A of round 2, clip_round1.py) or u2\ / unity\.
$ErrorActionPreference = "Stop"
$W = (Resolve-Path "$PSScriptRoot\..\..").Path
$B = "C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
$P = "C:\Users\Epic\Desktop\Animation Workflow\.venv\Scripts\python.exe"
$S = "$W\scripts\hero3d_v2"; $R = "$W\art\redo\hero3d_v2"; $O = "$W\art\tests\hero3d_v2"; $T = "$R\tmp\final"
function Check($what) { if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE); READY.txt not written" } }
New-Item -ItemType Directory -Force $R, $O, $T, "$R\round2", "$O\round2" | Out-Null
Remove-Item "$R\READY.txt", "$R\round2\READY.txt" -ErrorAction SilentlyContinue
Get-ChildItem $T -File | Remove-Item                      # no stale frames can reach the sheets
& $P -I "$S\make_face.py" "$W\art\redo\animway\cutB\front.png" "$R\face.png"; Check "make_face"
& $B -b --factory-startup --python-exit-code 1 --python "$S\build_hero.py" -- $R "$R\face.png"; Check "build_hero"   # hero.fbx (+ Stance), .glb, .blend, _look.json
foreach ($m in "look", "turnaround", "bends", "face_turn", "overlay") {
  & $B -b "$R\hero.blend" --python-exit-code 1 --python "$S\render.py" -- $m $T; Check "render $m" }   # look asserts the sword hand is the near hand
& $P -I "$S\sheets.py" look $T "$O\look.png" $W; Check "sheets look"   # look_gamesize, look_3x, silhouette, sword_compare
& $P -I "$S\sheets.py" turnaround $T "$O\turnaround.png" $W; Check "sheets turnaround"
& $P -I "$S\sheets.py" bends $T "$O\bends.png" $W; Check "sheets bends"   # + bends_zoom.png
& $P -I "$S\sheets.py" face_turn $T "$O\face_turn.png" $W; Check "sheets face_turn"
& $P -I "$S\sheets.py" overlay $T "$O\overlay.png" $W; Check "sheets overlay"   # overlay_front.png / overlay_right.png
& $B -b --factory-startup --python-exit-code 1 --python "$S\check_fbx.py" -- "$R\hero.fbx"; Check "check_fbx"
foreach ($f in "look_gamesize", "look_3x", "silhouette", "sword_compare", "turnaround", "bends", "bends_zoom", "face_turn") {
  Copy-Item "$O\$f.png" "$O\round2\$f.png" -Force }
foreach ($f in "hero.fbx", "hero.glb", "hero.blend", "hero_look.json", "face.png") { Copy-Item "$R\$f" "$R\round2\$f" -Force }
$ready = "Round 2 hero: hero.fbx (action Stance, 24 fps, frames 1-24, sword object on socket_hand_R), hero.glb, " +
         "hero.blend (with the outline hull), hero_look.json (look v2: materials, light, shade mask, outline, rim, " +
         "face mapping, parts, gear, clips, game view, stats), face.png. Written " + (Get-Date -Format "yyyy-MM-dd HH:mm") + "."
Set-Content "$R\READY.txt" $ready -Encoding utf8
Set-Content "$R\round2\READY.txt" $ready -Encoding utf8
"DONE"
