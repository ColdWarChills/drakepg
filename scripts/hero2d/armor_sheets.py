"""The armored hero in the ready stance, and the candidate frame with both heroes.
<venv python> -I -B armor_sheets.py [armor kit json, default art/redo/hero2d/r2/scale_mail.json]
Writes art/tests/hero2d/r2/armor_stance_2x.png (Fen Road 2560x1440, the armored hero 480 px tall at the bare hero's
spot), armor_judge_3x.png (beside the game's drawn Scale Mail stance, with crops) and candidate_2x.png (bare hero at his spot, the
armored hero about 380 px to his right, both 480 px, no labels)."""
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rig import KIT, TESTS, Kit  # noqa: E402
from stance import judge, on_road, stance_pose  # noqa: E402

bare = Kit(KIT / "pieces.json")
mail = Kit(Path(sys.argv[1]) if len(sys.argv) > 1 else KIT / "scale_mail.json")
pb, pm = stance_pose(bare), stance_pose(mail)
on_road([(pm, mail, 674)]).convert("RGB").save(TESTS / "armor_stance_2x.png")
judge(mail, pm, TESTS / "armor_judge_3x.png", "char_hero_scale_mail_sword_ready.png")   # beside the game's Scale Mail
on_road([(pb, bare, 674), (pm, mail, 674 + 380)]).convert("RGB").save(TESTS / "candidate_2x.png")
print("ok")
