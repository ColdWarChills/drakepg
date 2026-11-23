import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from PIL import Image
from jcommon import fit, TESTS
d = pathlib.Path(sys.argv[1]); names = sys.argv[3:]
ims = [Image.open(d / f"f{int(n):04d}.png") for n in names]
W = sum(i.width for i in ims); sh = Image.new("RGB", (W, ims[0].height))
x = 0
for i in ims: sh.paste(i, (x, 0)); x += i.width
fit(sh).save(TESTS / sys.argv[2]); print(sh.size)
