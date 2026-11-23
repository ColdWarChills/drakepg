"""Round 2 orientation sheet: round 1's look at game size and the picked drawing's upper body."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from PIL import Image
from jcommon import ROOT, TESTS, REDO, fit, on_bg
out = TESTS / "r2" / "_orient.png"
lk = Image.open(TESTS / "look_kinds.png")
s = Image.open(REDO / "cutj3_s123.png").convert("RGBA").crop((450, 300, 1150, 900))
sheet = Image.new("RGB", (1500, 1000), (40, 40, 40))
lk2 = lk.resize((1500, round(520 * 1500 / 2569)), Image.LANCZOS)
sheet.paste(lk2, (0, 0))
s2 = s.convert("RGB").resize((700, 600))
sheet.paste(s2, (0, 310))
sw = Image.open(TESTS / "swaps_4x.png").resize((690, 690)).crop((0, 0, 690, 690))
sheet.paste(sw.crop((0, 0, 690, 690)).resize((690, 690)), (800, 310))
fit(sheet).save(out)
print(out)
