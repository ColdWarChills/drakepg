import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np, cv2
from PIL import Image
from jcommon import PURPLE, REDO, RAW
for p in sys.argv[1:]:
    im = np.asarray(Image.open(p).convert("RGBA")).astype(np.float32)
    rgb = im[..., :3]
    fig = (np.abs(rgb - np.array(PURPLE)).max(-1) > 70) & (im[..., 3] > 128)
    px = rgb[fig][::7]
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 1.0)
    _, lab, cen = cv2.kmeans(px.astype(np.float32), 10, None, crit, 3, cv2.KMEANS_PP_CENTERS)
    cnt = np.bincount(lab.ravel())
    print(p.split('/')[-1], sorted([(int(c), [int(v) for v in cen[i]]) for i, c in enumerate(cnt)], reverse=True))
