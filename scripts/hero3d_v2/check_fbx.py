"""Re-import the exported FBX (and GLB) into an empty Blender and print what Unity will get: height, bones, materials,
actions, the largest number of bone influences per vertex, and the "shade" colour layer (R = forced shade, G = outline
factor; both must be exactly 0 or 1). Asserts the rules the Unity side relies on.
blender -b --factory-startup --python check_fbx.py -- <hero.fbx>"""
import sys

import bpy

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.context.view_layer.update()
zs = [(o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices]
print(f"FBX objects: {len(arms)} armature, {len(meshes)} meshes: {sorted(o.name for o in meshes)}")
print(f"FBX height {max(zs) - min(zs):.3f} m (min z {min(zs):.3f}); armature scale {tuple(round(s, 3) for s in arms[0].scale)}")
bones = {b.name for b in arms[0].data.bones}
print("FBX bones", len(bones), sorted(bones))
print("FBX actions", [a.name for a in bpy.data.actions])
print("FBX materials", sorted({m.name for o in meshes for m in o.data.materials if m}))
mx = 0
for o in meshes:
    gi = {g.index for g in o.vertex_groups if g.name in bones}
    for v in o.data.vertices:
        mx = max(mx, sum(1 for g in v.groups if g.group in gi and g.weight > 1e-4))
    assert o.parent in arms or 'ARMATURE' in [m.type for m in o.modifiers], o.name
    lay = o.data.color_attributes.get("shade") or (o.data.color_attributes[0] if len(o.data.color_attributes) else None)
    assert lay is not None, f"{o.name}: no shade colour layer"
    vals = set()
    for d in lay.data:
        c = d.color_srgb if hasattr(d, "color_srgb") else d.color
        vals.add((round(c[0], 3), round(c[1], 3)))
    bad = {v for v in vals if not all(x in (0.0, 1.0) for x in v)}
    print(f"  {o.name}: {len(o.data.vertices)} verts, layer '{lay.name}' ({lay.domain}), R/G values {sorted(vals)}")
    assert not bad, f"{o.name}: shade layer values not 0/1: {sorted(bad)[:5]}"
print("FBX max influences per vertex", mx)
assert mx <= 3, "more than 3 bone influences"
print("FBX uv layers on Head:", [l.name for l in bpy.data.objects["Head"].data.uv_layers])


def mat_info(m):
    col = tuple(round(c, 2) for c in m.diffuse_color[:3])
    imgs = [n.image.name for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image] if m.node_tree else []
    return f"{m.name}: colour {col} images {imgs}"


print("FBX material colours:", "; ".join(mat_info(m) for m in bpy.data.materials))
glb = path[:-4] + ".glb"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
print(f"GLB: {len(arms)} armature, {len(arms[0].data.bones) if arms else 0} bones, "
      f"{sum(1 for o in bpy.data.objects if o.type == 'MESH')} meshes, actions {[a.name for a in bpy.data.actions]}")
print("CHECK OK")
