"""Round 2: purple-tinged pixels left on the hero (no background) at the stills' poses: count and where."""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
import rig_r2 as rig
from r2_stills import STILLS, pose_B
parts = rig.Parts(); fr = rig.Frame(parts)
for name, k in STILLS.items():
    px, info = fr.render(pose_B(k), rim=False)
    a = px[..., 3]
    rgb = np.where(a[..., None] > 0.05, px[..., :3] / np.maximum(a[..., None], 1e-3), 0) * 255
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    m = (a > 0.3) & (B > G + 20) & (B > 0.7 * R) & (R > G + 5)
    ys, xs = np.nonzero(m)
    print(name, int(m.sum()), [(int(x + fr.ox), int(y + fr.oy), rgb[y, x].round().tolist()) for y, x in zip(ys[::max(1, len(ys) // 5)], xs[::max(1, len(xs) // 5)])])
for k, (img, org) in parts.layers.items():
    a = img[..., 3]
    rgb = np.where(a[..., None] > 0.05, img[..., :3] / np.maximum(a[..., None], 1e-3), 0) * 255
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    m = (a > 0.3) & (B > G + 20) & (B > 0.7 * R) & (R > G + 5)
    ys, xs = np.nonzero(m)
    print("part", k, int(m.sum()), [(int(x + org[0]), int(y + org[1]), rgb[y, x].round().tolist()) for y, x in zip(ys[::max(1, len(ys) // 6)], xs[::max(1, len(xs) // 6)])])
