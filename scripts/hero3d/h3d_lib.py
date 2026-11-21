"""Shared Blender helpers for the 3D hero test: toon look, outline, sword, camera."""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def toon_material(name, image=None, color=(0.8, 0.8, 0.8, 1), shadow=0.72, cut=0.45):
    """Flat cel look: the albedo (texture or colour) as emission, times one shadow tone where the light falls off.
    Works in EEVEE (Shader to RGB)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = 'Linear'
        albedo = tex.outputs["Color"]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = color
        albedo = rgb.outputs[0]
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diff.inputs["Color"].default_value = (1, 1, 1, 1)
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    nt.links.new(diff.outputs[0], s2r.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (shadow, shadow, shadow * 1.04, 1)
    ramp.color_ramp.elements[1].position = cut
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    bw = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(s2r.outputs[0], bw.inputs[0])
    nt.links.new(bw.outputs[0], ramp.inputs[0])
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs[0].default_value = 1.0
    nt.links.new(albedo, mul.inputs[6])
    nt.links.new(ramp.outputs[0], mul.inputs[7])
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(mul.outputs[2], em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    return m


def outline_material(color=(0.09, 0.06, 0.05, 1)):
    m = bpy.data.materials.get("Outline")
    if m:
        return m
    m = bpy.data.materials.new("Outline")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = color
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = True
    return m


def add_outline(obj, thickness=0.008):
    """Inverted hull: a solidify shell with flipped normals in the outline material, back faces culled."""
    mat = outline_material()
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Outline", 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = len(obj.data.materials) - 1
    for m in obj.data.materials:
        if m and m.name != "Outline":
            m.use_backface_culling = True
    return mod


def box(name, size, loc=(0, 0, 0), taper_top=1.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
        if v.co.z > 0 and taper_top != 1.0:
            v.co.x *= taper_top
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    bpy.context.scene.collection.objects.link(ob)
    return ob


def make_sword(length=0.95):
    """A simple arming sword built from boxes along +Z, grip centre at the origin (where the fist holds it).
    Colours from the game's iron sword picture: steel blade, dark iron guard, brown leather grip."""
    steel = toon_material("Steel", color=(0.62, 0.64, 0.66, 1), shadow=0.6)
    edge = toon_material("SteelEdge", color=(0.86, 0.87, 0.88, 1), shadow=0.75)
    iron = toon_material("Iron", color=(0.30, 0.30, 0.32, 1), shadow=0.6)
    leather = toon_material("Leather", color=(0.33, 0.18, 0.10, 1), shadow=0.6)
    grip_len = 0.16
    blade_len = length - grip_len - 0.06
    parts = []
    # blade: a flat diamond section made from a thin box, tapering to a point
    bm = bmesh.new()
    w, t = 0.034, 0.010
    z0, z1, tip = 0.04, 0.04 + blade_len * 0.86, 0.04 + blade_len
    ring = lambda z, s: [bm.verts.new((x * s, y * s, z)) for x, y in ((w, 0), (0, t), (-w, 0), (0, -t))]
    a, b = ring(z0, 1.0), ring(z1, 0.92)
    p = bm.verts.new((0, 0, tip))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((a[i], a[j], b[j], b[i]))
        bm.faces.new((b[i], b[j], p))
    bm.faces.new(list(reversed(a)))
    me = bpy.data.meshes.new("Blade")
    bm.to_mesh(me)
    bm.free()
    blade = bpy.data.objects.new("Blade", me)
    bpy.context.scene.collection.objects.link(blade)
    blade.data.materials.append(steel)
    parts.append(blade)
    fuller = box("Fuller", (0.005, 0.0115, blade_len * 0.38), (0, 0, z0 + blade_len * 0.42))
    fuller.data.materials.append(edge)
    parts.append(fuller)
    guard = box("Guard", (0.085, 0.020, 0.020), (0, 0, 0.025))
    guard.data.materials.append(iron)
    parts.append(guard)
    grip = box("Grip", (0.017, 0.017, grip_len / 2), (0, 0, -grip_len / 2 + 0.012))
    grip.data.materials.append(leather)
    parts.append(grip)
    pommel = box("Pommel", (0.026, 0.026, 0.024), (0, 0, -grip_len - 0.005))
    pommel.data.materials.append(iron)
    parts.append(pommel)
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = blade
    bpy.ops.object.join()
    sword = bpy.context.view_layer.objects.active
    sword.name = "Sword"
    bpy.ops.object.select_all(action='DESELECT')
    add_outline(sword, 0.005)
    return sword


def camera(target=(0, 0, 0.9), yaw=-35.0, pitch=6.0, dist=10.0, ortho=2.4):
    """Orthographic battle camera. The hero faces +X (screen right). yaw = degrees the camera swings from his
    right side toward his front (0 = pure side profile)."""
    sc = bpy.context.scene
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = ortho
    y, p = math.radians(yaw), math.radians(pitch)
    # the hero's right side is -Y when he faces +X; camera at -Y swung toward +X
    d = Vector((math.sin(-y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    cam.location = Vector(target) + d * dist
    look = (Vector(target) - cam.location).normalized()
    cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
    return cam


def render_setup(res=(720, 720), transparent=True, engine="BLENDER_EEVEE"):
    sc = bpy.context.scene
    try:
        sc.render.engine = engine
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = transparent
    sc.view_settings.view_transform = 'Standard'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.fps = 24
    # one sun for the shadow tone: from the upper left front, like the drawn hero's light
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sc.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(40), math.radians(-25), math.radians(-30))
    sun.data.energy = 3.0
    return sc
