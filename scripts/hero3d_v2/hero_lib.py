"""Shared Blender helpers for the clean 3D hero (v2): lofted parts, the one-shader toon look, posing, cameras.
Look ingredients are only ones Unity can draw with one simple shader: flat colour (or the one face texture) times a
2-step ramp from one fixed light direction, and an inverted-hull outline (solidify, flipped normals, culled)."""
import math

import bpy
import numpy as np
from mathutils import Matrix, Vector

S = 1.75 / 913.0                     # metres per pixel in the drawn orthographic views (cutB, 1024 px)


def srgb2lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexcol(h):
    return tuple(srgb2lin(int(h[i:i + 2], 16)) for i in (1, 3, 5)) + (1.0,)


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# ---------------------------------------------------------------- geometry
def ring(c, u, v, hu, hv, p=2.0, n=16, hu_neg=None, hv_neg=None, p_front=None):
    """Superellipse ring: centre c, axes u (width) and v (depth), half sizes (+side, -side). p_front: exponent of
    the +v half (a flatter face front)."""
    hu_neg = hu if hu_neg is None else hu_neg
    hv_neg = hv if hv_neg is None else hv_neg
    pts = []
    for k in range(n):
        th = 2 * math.pi * k / n
        cs, sn = math.cos(th), math.sin(th)
        pp = p_front if (p_front is not None and sn > 0) else p
        a = math.copysign(abs(cs) ** (2 / pp), cs)
        b = math.copysign(abs(sn) ** (2 / pp), sn)
        x = a * (hu if a > 0 else hu_neg)
        y = b * (hv if b > 0 else hv_neg)
        pts.append(Vector(c) + Vector(u) * x + Vector(v) * y)
    return pts


def loft(rings, cap0=0.5, cap1=0.5):
    """Rings (lists of Vectors, same count) -> closed mesh arrays. Caps: a pole pushed out by cap * ring radius."""
    n = len(rings[0])
    verts = [p for r in rings for p in r]
    faces = []
    for i in range(len(rings) - 1):
        for k in range(n):
            a, b = i * n + k, i * n + (k + 1) % n
            faces.append((a, b, b + n, a + n))
    for end, cap in ((0, cap0), (len(rings) - 1, cap1)):
        r = rings[end]
        c = sum(r, Vector()) / n
        nb = rings[1] if end == 0 else rings[-2]
        cn = sum(nb, Vector()) / n
        ax = (c - cn).normalized()
        rad = sum((p - c).length for p in r) / n
        pole = c + ax * rad * cap
        pi_ = len(verts)
        verts.append(pole)
        base = end * n
        for k in range(n):
            faces.append((base + k, base + (k + 1) % n, pi_))
    return [tuple(v) for v in verts], faces


def frame(axis, fwd=(0, -1, 0)):
    """u (sideways) and v (forward) perpendicular to axis."""
    a = Vector(axis).normalized()
    f = Vector(fwd)
    f = (f - a * f.dot(a)).normalized()
    u = a.cross(f).normalized()
    return a, u, f


def make_obj(name, verts, faces, subdiv=2):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    # outward normals
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    if subdiv:
        m = ob.modifiers.new("sub", 'SUBSURF')
        m.levels = m.render_levels = subdiv
        dg = bpy.context.evaluated_depsgraph_get()
        me2 = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        ob.modifiers.clear()
        old = ob.data
        ob.data = me2
        bpy.data.meshes.remove(old)
        me2.name = name
    return ob


def mesh_arrays(ob):
    me = ob.data
    co = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    faces = [tuple(p.vertices) for p in me.polygons]
    return co.reshape(-1, 3), faces


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def along(P, a, b):
    a, b = np.array(a), np.array(b)
    d = b - a
    L = np.linalg.norm(d)
    return (P - a) @ (d / L)


# ---------------------------------------------------------------- look
LIGHT_CAM = Vector((0.85, 0.42, 0.32))   # light from the upper right front of the screen (camera space: x right, y up, z toward viewer)
CUT = 0.12                               # N.L above the cut = lit
HI_CUT = 0.72                            # N.L above this = the third, lighter tone (materials that have one)
OUTLINE_M = 0.007                        # inverted-hull width in metres (times each object's outline_scale)


def light_world(cam_rot_matrix=None):
    """The fixed light as a world vector for a camera looking along +Y (x right, z up, -y toward viewer)."""
    l = Vector((LIGHT_CAM.x, -LIGHT_CAM.z, LIGHT_CAM.y)).normalized()
    return l


def toon_material(name, base_hex, shade_hex, light, cut=None, image=None, hi_hex=None, hi_cut=None):
    """One shader for every part (emission only, so scene lights don't matter):
    d = dot(N, L); lit = d > cut and mask < 0.5 (mask = R of the colour attribute "shade", 0 or 1);
    colour = lit ? (hi and d > hi_cut ? hi : albedo) : albedo * shade/base. albedo = flat colour or the face texture."""
    cut = CUT if cut is None else cut
    hi_cut = HI_CUT if hi_cut is None else hi_cut
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    for n_ in list(nt.nodes):
        nt.nodes.remove(n_)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    base = hexcol(base_hex)
    shade = hexcol(shade_hex)
    tint = tuple(min(1.0, s / max(b, 1e-4)) for s, b in zip(shade[:3], base[:3])) + (1.0,)
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = 'Linear'
        tex.extension = 'EXTEND'
        albedo = tex.outputs["Color"]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = base
        albedo = rgb.outputs[0]
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath")
    dot.operation = 'DOT_PRODUCT'
    dot.inputs[1].default_value = tuple(light)
    nt.links.new(geo.outputs["Normal"], dot.inputs[0])
    gt = nt.nodes.new("ShaderNodeMath")
    gt.operation = 'GREATER_THAN'
    gt.inputs[1].default_value = cut
    nt.links.new(dot.outputs["Value"], gt.inputs[0])
    attr = nt.nodes.new("ShaderNodeAttribute")          # the forced-shade mask (missing attribute = 0 = no force)
    attr.attribute_name = "shade"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs[0])
    unmasked = nt.nodes.new("ShaderNodeMath")
    unmasked.operation = 'LESS_THAN'
    unmasked.inputs[1].default_value = 0.5
    nt.links.new(sep.outputs[0], unmasked.inputs[0])
    lit = nt.nodes.new("ShaderNodeMath")
    lit.operation = 'MULTIPLY'
    nt.links.new(gt.outputs[0], lit.inputs[0])
    nt.links.new(unmasked.outputs[0], lit.inputs[1])
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs[0].default_value = 1.0
    nt.links.new(albedo, mul.inputs[6])
    mul.inputs[7].default_value = tint
    lit_col = albedo
    if hi_hex:                                           # third, lighter tone where the light hits square on
        hg = nt.nodes.new("ShaderNodeMath")
        hg.operation = 'GREATER_THAN'
        hg.inputs[1].default_value = hi_cut
        nt.links.new(dot.outputs["Value"], hg.inputs[0])
        hm = nt.nodes.new("ShaderNodeMix")
        hm.data_type = 'RGBA'
        nt.links.new(hg.outputs[0], hm.inputs[0])
        nt.links.new(albedo, hm.inputs[6])
        hm.inputs[7].default_value = hexcol(hi_hex)
        lit_col = hm.outputs[2]
        m["hi"] = hi_hex
        m["hi_cut"] = hi_cut
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    nt.links.new(lit.outputs[0], mix.inputs[0])
    nt.links.new(mul.outputs[2], mix.inputs[6])
    nt.links.new(lit_col, mix.inputs[7])
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(mix.outputs[2], em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = True
    m["base"] = base_hex
    m["shade"] = shade_hex
    return m


def outline_material(hex_="#2a1a12"):
    m = bpy.data.materials.get("Outline")
    if m:
        return m
    m = bpy.data.materials.new("Outline")
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    for n_ in list(nt.nodes):
        nt.nodes.remove(n_)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = hexcol(hex_)
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = True
    return m


def add_outline(ob, thickness):
    mat = outline_material()
    if mat.name not in [m.name for m in ob.data.materials if m]:
        ob.data.materials.append(mat)
    idx = [m.name for m in ob.data.materials].index(mat.name)
    mod = ob.modifiers.new("Outline", 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = idx
    mod.material_offset_rim = idx
    if ob.vertex_groups.get("outline"):               # per-vertex width factor (0 on inner lines)
        mod.vertex_group = "outline"
        mod.thickness_vertex_group = 0.0
    return mod


def set_outline_thickness(t):
    for ob in bpy.data.objects:
        for m in ob.modifiers:
            if m.type == 'SOLIDIFY' and m.name == "Outline":
                m.thickness = t * ob.get("outline_scale", 1.0)


# ---------------------------------------------------------------- posing
def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)


def rot(arm, bone, axis, deg):
    """Rotate a bone about an axis given in the body (armature rest) frame; the parent's pose carries it along."""
    pb = arm.pose.bones[bone]
    pb.rotation_mode = 'QUATERNION'
    M = arm.data.bones[bone].matrix_local.to_3x3()
    R = Matrix.Rotation(math.radians(deg), 3, Vector(axis).normalized())
    q = (M.inverted() @ R @ M).to_quaternion()
    pb.rotation_quaternion = q @ pb.rotation_quaternion


def move(arm, bone, offset):
    """Translate a bone (the root) by an offset in the armature rest frame."""
    pb = arm.pose.bones[bone]
    M = arm.data.bones[bone].matrix_local.to_3x3()
    pb.location = pb.location + M.inverted() @ Vector(offset)


# ---------------------------------------------------------------- render
def render_setup(res, samples=16):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.view_settings.exposure = 0
    sc.view_settings.gamma = 1
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    return sc


def ortho_camera(loc, look_dir, up=(0, 0, 1), scale=2.0):
    sc = bpy.context.scene
    cam = sc.camera
    if cam is None:
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        sc.collection.objects.link(cam)
        sc.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = scale
    cam.data.clip_start = 0.1
    cam.data.clip_end = 100
    cam.location = Vector(loc)
    cam.rotation_euler = Vector(look_dir).normalized().to_track_quat('-Z', 'Y').to_euler()
    return cam


def project(cam, p):
    """World point -> render pixel (x right, y down)."""
    from bpy_extras.object_utils import world_to_camera_view
    sc = bpy.context.scene
    co = world_to_camera_view(sc, cam, Vector(p))
    return co.x * sc.render.resolution_x, (1 - co.y) * sc.render.resolution_y, co.z
