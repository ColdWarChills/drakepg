"""Shared paths and the battle stage numbers (read from the Unity battle code, BattleScreen.cs / Figure.cs)."""
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]  # the worktree
RAW = ROOT / "art" / "raw"
TESTS = ROOT / "art" / "tests" / "momentclip"
SNAPSHOT = TESTS / "layers_snapshot"   # the default layers folder (read only)
OUT = TESTS / "v2"                     # where every picture, clip and timing.md is written (clip.py --outdir)
FONTS = ROOT / "unity" / "Assets" / "Resources" / "Fonts"
FFMPEG = ("C:/Users/Epic/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/"
          "ffmpeg-9.0-full_build/bin/ffmpeg.exe")

FPS = 60
DESIGN_W, DESIGN_H = 1280, 720
PX = 2  # output px per design px (the game draws the 1280x720 design at 2x on 1440p)

# BattleScreen.cs
GROUND_Y = 500          # GroundY: the fighters' feet
PLAYER_X = 330          # PlayerHome
ENEMY_X = 950           # EnemyHome
BG_RAISE = 90           # BgRaise: the painted background is lifted, the strip under it filled dark
BG_STRIP = (0x14, 0x10, 0x1c)
# Figure.cs: a figure's art is scaled to 240 design px tall (at most 300 wide), then by its look's scale
ART_H, ART_MAX_W = 240, 300
SHADOW_ALPHA, SHADOW_H = 0.45, 26  # AddShadow: Art.Soft at 0.7 x drawn width, 26 px high, black 45%

# The weapon kind data (src/data/items.ts training_sword): grip point at 0.79 of the art's height, centred.
WEAPON_GRIP = {"wpn_training_sword": 0.79}

# Fen Road's monster: zone `fen` encounters (mire_toad weight 5, spark_wisp 3); look scale 0.9, art mon_mire_toad.
MONSTER = {"id": "mire_toad", "art": "mon_mire_toad", "scale": 0.9}

MIXES = {
    "bare": [],
    "mix1": ["scale_mail", "leather_cap", "scale_gauntlets", "leather_leggings", "scale_boots"],
    "mix2": ["leather_armor", "scale_coif", "leather_gloves", "scale_leggings", "leather_boots"],
}
NAMES = {
    "scale_mail": "Scale Mail", "leather_cap": "Leather Cap", "scale_gauntlets": "Scale Gauntlets",
    "leather_leggings": "Leather Leggings", "scale_boots": "Scale Boots", "leather_armor": "Leather Armor",
    "scale_coif": "Scale Coif", "leather_gloves": "Leather Gloves", "scale_leggings": "Scale Leggings",
    "leather_boots": "Leather Boots",
}


def caption(pieces):
    return "Bare" if not pieces else ", ".join(NAMES.get(p, p.replace("_", " ").title()) for p in pieces)
