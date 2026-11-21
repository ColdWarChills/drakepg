"""Import an image-to-3D GLB of the hero, clean it, stand it up (feet on z 0, facing -Y, height 1.75 m),
decimate, and save hero_mesh.blend. Also writes a front and side check render and the vertex cloud (npz) for
measuring joints.

blender -b --factory-startup --python prep_mesh.py -- <in.glb> <out_dir> [--faces 60000] [--rotz deg]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import h3d_lib as L  # noqa: E402

a = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = a[0], a[1]
FACES = int(a[a.index("--faces") + 1]) if "--faces" in a else 60000
ROTZ = float(a[a.index("--rotz") + 1]) if "--rotz" in a else 0.0
os.makedirs(OUT, exist_ok=True)

L.clear()
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in bpy.data.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
hero = bpy.context.view_layer.objects.active
hero.name = "Hero"
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in bpy.data.objects if o != hero]:
    bpy.data.objects.remove(o)

if ROTZ:
    hero.data.transform(Matrix.Rotation(math.radians(ROTZ), 4, 'Z'))
V = np.array([v.co[:] for v in hero.data.vertices])
lo, hi = V.min(0), V.max(0)
s = 1.75 / (hi[2] - lo[2])
c = (lo + hi) / 2
hero.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-c[0], -c[1], -lo[2])))
hero.data.update()

nf = len(hero.data.polygons)
if nf > FACES:
    mod = hero.modifiers.new("Dec", 'DECIMATE')
    mod.ratio = FACES / nf
    bpy.context.view_layer.objects.active = hero
    bpy.ops.object.modifier_apply(modifier="Dec")
print("FACES", nf, "->", len(hero.data.polygons))

# toon material from the generated texture
img = None
for m in hero.data.materials:
    if m and m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image:
                img = n.image
                break
    if img:
        break
hero.data.materials.clear()
hero.data.materials.append(L.toon_material("HeroToon", image=img))
L.add_outline(hero, 0.006)

V = np.array([v.co[:] for v in hero.data.vertices])
np.savez(os.path.join(OUT, "verts.npz"), V=V)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "hero_mesh.blend"))

sc = L.render_setup((768, 768), transparent=False)
sc.world = bpy.data.worlds.new("W")
sc.world.color = (0.85, 0.87, 0.82)
for name, yaw in (("front", -90.0), ("side", 0.0), ("battle", -35.0)):
    for o in [o for o in bpy.data.objects if o.type == 'CAMERA']:
        bpy.data.objects.remove(o)
    L.camera(target=(0, 0, 0.875), yaw=yaw, pitch=0.0 if name != "battle" else 6.0, ortho=1.95)
    sc.render.filepath = os.path.join(OUT, f"check_{name}.png")
    bpy.ops.render.render(write_still=True)
print("DONE", OUT)
