import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from PIL import Image
from jcommon import fit
im = Image.open(sys.argv[1]).convert("RGB")
if len(sys.argv) > 3:
    im = im.crop(tuple(int(v) for v in sys.argv[3].split(",")))
fit(im).save(sys.argv[2]); print(im.size)
