"""Print what the Unity importer needs to know about a hero .blend (read-only).
blender -b <hero.blend> --python scripts/hero3d_unity/probe_blend.py"""
import bpy

for ob in bpy.data.objects:
    line = f"{ob.name:18s} {ob.type:9s} parent={ob.parent.name if ob.parent else '-'} hide_render={ob.hide_render}"
    if ob.type == 'MESH':
        me = ob.data
        sharp = 0
        if "sharp_edge" in me.attributes:
            sharp = sum(1 for v in me.attributes["sharp_edge"].data if v.value)
        smooth = sum(1 for p in me.polygons if p.use_smooth)
        mods = [f"{m.type}:{m.name}" for m in ob.modifiers]
        line += (f" verts={len(me.vertices)} polys={len(me.polygons)} smooth={smooth} sharp_edges={sharp}"
                 f" uv={[u.name for u in me.uv_layers]} col={[c.name for c in me.color_attributes]}"
                 f" mats={[m.name if m else None for m in me.materials]} mods={mods}")
    print("PROBE", line)
arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
if arm:
    print("PROBE bones", [b.name for b in arm.data.bones])
    print("PROBE arm loc", tuple(arm.location), "rot", tuple(arm.rotation_euler), "scale", tuple(arm.scale))
print("PROBE scene keys", list(bpy.context.scene.keys()))
