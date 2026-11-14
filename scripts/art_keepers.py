"""Keepers pages for the 2026-11 asset batch: one picked try per item (rated 2026-11-14).

The picks live here (key:seed digit, 0 = seed 7100, 1 = 7101). Usage: python scripts/art_keepers.py [repo]
writes art/redo/_keepers_<n>_<group>.png. Not picked = thrown out (see docs/PLANS.md, "Asset update batch")."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent)
REDO = ROOT / "art" / "redo"
ART = ROOT / "src" / "assets" / "art"
PAPER, DARK, MOSS = (214, 205, 182), (40, 38, 46), (74, 88, 66)
FONT = ImageFont.truetype("arialbd.ttf", 13)

def P(s):
    return {k: int(v) for k, v in (x.split(":") for x in s.split())}

PROP = P("bog_lilies:1 bones:0 campfire_b:0 campfire_out:1 chest:1 chest_open:1 mimic_chest:1 crates:0 fallen_log:1 "
         "herb_patch:0 lichen_rock:0 mushroom_ring:1 note_stake:0 notice_board:0 offering_bowl:0 reed_wall:1 rockfall:1 "
         "rune_pillar:0 shrine_stone:0 signpost_left:0 signpost_right:0 signpost_up:0 snowdrift:0 sprouting_stump:1 "
         "steam_vent:0 tent:0 thorn_wall:1")
WPN = P("galecutter:0 iron_sword:1 larchwood_staff:0 steel_broadsword:1 stormcleaver:0 tempered_longsword:0")
FX = P("bite:0 block:1 blood_pool:1 blood_splat:0 buff_up:0 chain:0 charge_ring:0 claw:1 coin:0 confetti:0 crit:0 dark_bolt:0 "
       "dark_burst:1 debuff_down:1 dust_puff:0 earth_bolt:0 earth_burst:1 ember:0 energy_bolt:1 energy_burst:0 fire_bolt:0 "
       "fire_burst:0 freeze:0 ice_bolt:1 ice_burst:0 impact:0 impact_blunt:1 leaf:1 levelup:1 light_bolt:0 light_burst:0 miss:0 "
       "poison_cloud:0 shield:1 shockwave:0 slash_a:0 slash_b:1 slash_heavy:0 smoke_puff:0 snowflake:1 sparkle:0 spores:1 "
       "stars_stun:1 water_bolt:1 water_burst:1 wind_bolt:0 wind_burst:1")
POSE = P("idle:1 idle_b:1 walk_a:0 walk_b:0 talk:1 point:0 wave:0 cheer:1 guard:0 hurt:0 down:0 drink:1 read:0 gather:1 sit:0")
MON = P("ash_imp_wind:1 ash_imp_strike:0 ash_imp_hurt:0 bramble_bandit_wind:1 bramble_bandit_strike:1 bramble_bandit_hurt:0 "
        "mossling_wind:0 mossling_strike:0 mossling_hurt:1 thornback_boar_wind:0 thornback_boar_strike:0 thornback_boar_hurt:1 "
        "thornheart_wind:0 thornheart_strike:1 thornheart_hurt:1")
TALK1 = {"guild_tamsin", "tobin", "hulda"}

def game(key):
    return next(iter(sorted(ART.glob(f"{key}@*x.webp"))), ART / f"{key}.webp")

def tri(key, seed):
    return REDO / key / f"{key}_s{7100 + seed}.png"

def fig(path, trim):
    img = Image.open(path).convert("RGBA")
    if trim:
        box = img.getchannel("A").point(lambda v: 0 if v < 10 else v).getbbox()
        if box:
            img = img.crop(box)
    return img

def sheet(name, cells, cols, cw, ch, bg, trim=True):
    label, pad = 16, 4
    rows = (len(cells) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cw + pad) + pad, rows * (ch + label + pad) + pad), (24, 22, 28))
    d = ImageDraw.Draw(img)
    for i, (text, path, now) in enumerate(cells):
        x, y = pad + (i % cols) * (cw + pad), pad + (i // cols) * (ch + label + pad)
        d.text((x + 2, y), text, fill=(255, 214, 120) if now else (225, 220, 205), font=FONT)
        tile = Image.new("RGB", (cw, ch), (150, 150, 160) if now else bg)
        if path and Path(path).exists():
            f = fig(path, trim)
            s = min((cw - 6) / f.width, (ch - 6) / f.height)
            f = f.resize((max(1, round(f.width * s)), max(1, round(f.height * s))), Image.LANCZOS)
            tile.paste(f, ((cw - f.width) // 2, ch - 3 - f.height), f)
        else:
            print("MISSING", text, path)
        img.paste(tile, (x, y + label))
    out = REDO / f"_keepers_{name}.png"
    img.save(out)
    print(out, img.size)

sheet("1_props", [(k, tri(f"prop_{k}", s), False) for k, s in PROP.items()], 9, 168, 168, PAPER)
cells = []
for k, s in WPN.items():
    cells += [(k[:16] + " NOW", game(f"wpn_{k}"), True), (k[:18] + " new", tri(f"wpn_{k}", s), False)]
sheet("2_weapons", cells, 12, 124, 300, MOSS)
sheet("3_effects", [(k, tri(f"fx_{k}", s), False) for k, s in FX.items()], 10, 150, 150, DARK)
cells = [("hero NOW", game("char_hero_bare"), True)] + [(k, tri(f"pose_{k}", s), False) for k, s in POSE.items()]
sheet("4_hero_poses", cells, 8, 190, 285, PAPER, trim=False)
cells = []
for m in ("ash_imp", "bramble_bandit", "mossling", "thornback_boar", "thornheart"):
    cells.append((m + " NOW", game(f"mon_{m}"), True))
    cells += [(p, tri(f"mon_{m}_{p}", MON[f"{m}_{p}"]), False) for p in ("wind", "strike", "hurt")]
sheet("5_monster_poses", cells, 8, 190, 190, PAPER, trim=False)
names = sorted(p.name[4:-5] for p in REDO.iterdir() if p.is_dir() and p.name.startswith("npc_") and p.name.endswith("_talk"))
cells = []
for n in names:
    cells += [(n[:14] + " NOW", game(f"npc_{n}"), True), ("talk", tri(f"npc_{n}_talk", 1 if n in TALK1 else 0), False),
              ("portrait", tri(f"portrait_{n}", 0), False)]
sheet("6_npcs", cells, 12, 126, 180, PAPER, trim=False)
