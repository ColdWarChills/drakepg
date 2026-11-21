"""Repaint the 3D hero from the drawn views: every texel takes its colour from the drawn view that sees it best
(front, back, right side, left side = the right side mirrored), with a depth test so a texel hidden behind an arm in a
view does not take the arm's colour. Texels no view sees keep a filled-in colour from their neighbours.

blender -b <mesh dir>/hero_mesh.blend --python project_paint.py -- <views dir (front/right/back.png, views.json)>
        <out mesh dir> [--res 2048] [--sharp 4]
Views are the cut_views.py squares: 1024 px, the body's height = 1024/1.12 px, feet on one line, each view centred on
its own silhouette box. The mesh (prep_mesh.py) is 1.75 m tall, feet on z 0, centred on its box, facing -Y.
"""
import math
import os
import shutil
import sys

import bpy
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
VIEWS, OUT = a[0], a[1]
RES = int(a[a.index("--res") + 1]) if "--res" in a else 2048
SHARP = float(a[a.index("--sharp") + 1]) if "--sharp" in a else 4.0
EDGE = int(a[a.index("--edge") + 1]) if "--edge" in a else 9     # view pixels dropped along the silhouette edge
os.makedirs(OUT, exist_ok=True)
SIZE, PAD, HM = 1024, 1.12, 1.75
S = (SIZE / PAD) / HM                       # pixels per metre
FEET = SIZE - (SIZE - SIZE / PAD) / 2       # feet row

hero = bpy.data.objects["Hero"]
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.samples = 1
sc.cycles.device = 'CPU'
mat = hero.data.materials[0]
nt = mat.node_tree
old_img = next(n.image for n in nt.nodes if n.type == 'TEX_IMAGE')


def bake(kind):
    """Bake object-space position or normal into a float image on the mesh's UVs; returns (RES, RES, 3)."""
    img = bpy.data.images.new(f"bake_{kind}", RES, RES, float_buffer=True, alpha=False)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    em = nt.nodes.new("ShaderNodeEmission")
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    prev = out.inputs["Surface"].links[0].from_socket
    nt.links.new(tc.outputs["Object" if kind == "pos" else "Normal"], em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = img
    nt.nodes.active = tn
    for m in hero.modifiers:
        m.show_render = False
    bpy.context.view_layer.objects.active = hero
    hero.select_set(True)
    sc.render.bake.margin = 0
    bpy.ops.object.bake(type='EMIT')
    arr = np.array(img.pixels[:], dtype=np.float32).reshape(RES, RES, 4)[..., :3]
    nt.links.new(prev, out.inputs["Surface"])
    for n in (tc, em, tn):
        nt.nodes.remove(n)
    for m in hero.modifiers:
        m.show_render = True
    return arr


P = bake("pos")
N = bake("norm")
covered = np.linalg.norm(N, axis=2) > 0.5          # texels inside UV islands
print("TEXELS", int(covered.sum()))
Pc, Nc = P[covered], N[covered]
Nc /= np.linalg.norm(Nc, axis=1, keepdims=True)


def load(name, flip=False):
    im = bpy.data.images.load(os.path.join(VIEWS, f"{name}.png"))
    arr = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], im.channels)[::-1, :, :3]
    return arr[:, ::-1] if flip else arr


# each view: image, how a point maps to its pixels (u right, v down), depth toward the camera, facing weight
xs, ys = Pc[:, 0], Pc[:, 1]
vrow = FEET - Pc[:, 2] * S
views = {
    "front": (load("front"), SIZE / 2 + xs * S, -ys, np.clip(-Nc[:, 1], 0, None)),
    "back": (load("back"), SIZE / 2 - xs * S, ys, np.clip(Nc[:, 1], 0, None)),
    # the side view only paints the head: its arms hang by the body, the mesh's arms are out (A-pose)
    "right": (load("right"), SIZE / 2 - ys * S, -xs, np.clip(-Nc[:, 0], 0, None) * (Pc[:, 2] > 1.36)),
    "left": (load("right"), SIZE / 2 - ys * S, xs, np.clip(Nc[:, 0], 0, None) * (Pc[:, 2] > 1.36)),
}
acc = np.zeros((len(Pc), 3), np.float32)
wsum = np.zeros(len(Pc), np.float32)
for name, (img, u, depth, face) in views.items():
    ui = np.clip(u.astype(int), 0, SIZE - 1)
    vi = np.clip(vrow.astype(int), 0, SIZE - 1)
    # z-buffer from the surface samples themselves: nearest depth per view pixel (depth = toward camera, bigger = nearer)
    zb = np.full((SIZE, SIZE), -np.inf, np.float32)
    np.maximum.at(zb, (vi, ui), depth)
    # close small holes in the z-buffer (3x3 max)
    zp = np.pad(zb, 1, constant_values=-np.inf)
    zb = np.max(np.stack([zp[dy:dy + SIZE, dx:dx + SIZE] for dy in range(3) for dx in range(3)]), 0)
    vis = depth >= zb[vi, ui] - 0.012
    # grazing texels (seen almost edge-on) take the drawn outline and rim colours as stripes: leave them to the
    # generated texture instead
    w = np.where(face > 0.35, face ** (SHARP if name in ("right", "left") else 1.0), 0.0) * vis
    col = img[vi, ui]
    # inside the drawn silhouette, minus a band along its edge: the drawn hero has a light rim line there, which
    # would paint stripes down the sides of the 3D legs and arms
    inside = img.sum(2) >= 0.05
    for _ in range(EDGE):
        inside = inside & np.roll(inside, 1, 0) & np.roll(inside, -1, 0) & np.roll(inside, 1, 1) & np.roll(inside, -1, 1)
    dark_bg = ~inside[vi, ui]
    w = np.where(dark_bg, 0, w)
    acc += col * w[:, None]
    wsum += w
    print("VIEW", name, "visible", round(float(vis.mean()), 3), "used", round(float((w > 0.05).mean()), 3))
seen = wsum > 1e-3
old = np.array(old_img.pixels[:], dtype=np.float32).reshape(old_img.size[1], old_img.size[0], old_img.channels)[..., :3]
if old.shape[0] != RES:
    # nearest resample of the old texture to RES
    idx = (np.arange(RES) * old.shape[0] / RES).astype(int)
    old = old[idx][:, idx]
newc = old[covered].copy()
newc[seen] = acc[seen] / wsum[seen, None]
print("UNSEEN texels", int((~seen).sum()), "of", len(seen))
tex = old.copy()
tex[covered] = newc
# grow the islands a few texels so seams don't pull in background
m = covered.copy()
for _ in range(6):
    grown = m.copy()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        sh = np.roll(np.roll(m, dy, 0), dx, 1)
        src = np.roll(np.roll(tex, dy, 0), dx, 1)
        add = sh & ~grown
        tex[add] = src[add]
        grown |= add
    m = grown
img = bpy.data.images.new("ProjectedAlbedo", RES, RES, alpha=False)
img.pixels[:] = np.dstack([tex, np.ones((RES, RES), np.float32)]).ravel()
img.filepath_raw = os.path.join(OUT, "albedo_proj.png")
img.file_format = 'PNG'
img.save()
img.pack()
for n in nt.nodes:
    if n.type == 'TEX_IMAGE':
        n.image = img
        n.interpolation = 'Linear'
sc.render.engine = 'BLENDER_EEVEE'
for f in ("joints.json", "verts.npz"):
    src = os.path.join(os.path.dirname(bpy.data.filepath), f)
    if os.path.exists(src):
        shutil.copy(src, os.path.join(OUT, f))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "hero_mesh.blend"))
print("DONE")
