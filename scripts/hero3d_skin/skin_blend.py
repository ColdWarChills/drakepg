"""Drawn skin on the clean 3D hero: the Blender side.
blender -b <blend> --python skin_blend.py -- <mode> ...
  prep <out work.blend> <atlas res>         UV atlas over all body parts (one 2048 image), bakes the old flat look as
                                            fallback.png, saves work.blend (copy; the round2 blend is never written)
  view <out dir> <pose> <yaw> <pitch> <w>x<h> <ppm> <zc>
                                            one projection view: bakes texel world position/normal/object id in that
                                            pose (npy, top-down), renders the camera's position + object-id passes
                                            (npy) and the flat old look (png), and the world->pixel map (proj.json)
  show <atlas.png> <out dir> <list of name:pose:yaw:pitch:wxh:ppm:zc[:sword]> [--magenta] [--mail]
                                            the textured hero, unlit + hull outline; --magenta: texels with alpha 0 drawn
                                            magenta; --mail: scale-mail shell on, Shirt torso hidden
  mail <atlas res>                          adds the scale-mail shell objects to work.blend (saved in place)
Body = Head Hair Headband Shirt Arms Hands Belt Trousers Boots (+ Mail, Pauldrons when made)."""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "hero3d_v2"))
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import hero_lib as L  # noqa: E402
import poses as PZ  # noqa: E402
import skin_poses as SP  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
MODE = argv[0]
ARM = bpy.data.objects["HeroRig"]
PZ.init(ARM)
BODY = ["Head", "Hair", "Headband", "Shirt", "Arms", "Hands", "Belt", "Trousers", "Boots"]
MAILP = ["Mail", "Pauldrons"]
ID = {n: i + 1 for i, n in enumerate(BODY + MAILP)}
OFF = 10.0                                   # positions are rendered/baked + OFF (keeps emission positive)


def body_objs():
    return [bpy.data.objects[n] for n in BODY + MAILP if bpy.data.objects.get(n)]


def outline_mods(ob, on):
    for m in ob.modifiers:
        if m.type == 'SOLIDIFY' and m.name == "Outline":
            m.show_render = on
            m.show_viewport = on


def emission_mat(name, build):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    build(nt, em)
    return m


def pos_mat():
    def b(nt, em):
        g = nt.nodes.new("ShaderNodeNewGeometry")
        add = nt.nodes.new("ShaderNodeVectorMath")
        add.operation = 'ADD'
        add.inputs[1].default_value = (OFF, OFF, OFF)
        nt.links.new(g.outputs["Position"], add.inputs[0])
        nt.links.new(add.outputs[0], em.inputs["Color"])
    return emission_mat("_pos", b)


def nrm_mat():
    def b(nt, em):
        g = nt.nodes.new("ShaderNodeNewGeometry")
        mad = nt.nodes.new("ShaderNodeVectorMath")
        mad.operation = 'MULTIPLY_ADD'
        mad.inputs[1].default_value = (0.5, 0.5, 0.5)
        mad.inputs[2].default_value = (0.5, 0.5, 0.5)
        nt.links.new(g.outputs["Normal"], mad.inputs[0])
        nt.links.new(mad.outputs[0], em.inputs["Color"])
    return emission_mat("_nrm", b)


def id_mat(k):
    def b(nt, em):
        em.inputs["Color"].default_value = (k / 16.0, 0, 0, 1)
    return emission_mat(f"_id{k}", b)


def swap_materials(mat_for):
    """Replace every slot of every body object (sword hidden) with mat_for(ob); returns a restore list."""
    keep = []
    for ob in body_objs():
        keep.append((ob, [s.material for s in ob.material_slots]))
        m = mat_for(ob)
        if m is None:                      # keep the object's own materials (the fallback bake of the old look)
            continue
        for s in ob.material_slots:
            s.material = m
    return keep


def restore(keep):
    for ob, mats in keep:
        for s, m in zip(ob.material_slots, mats):
            s.material = m


def cycles(samples=1):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    try:
        sc.cycles.device = 'GPU'
    except Exception:
        pass
    sc.cycles.use_denoising = False
    sc.cycles.pixel_filter_type = 'BOX'
    sc.cycles.filter_width = 0.01
    sc.view_settings.view_transform = 'Standard'
    sc.render.film_transparent = True
    return sc


def bake_to(img, mat_for):
    """Bake emission of every body object into img (shared atlas)."""
    sc = cycles(1)
    keep = swap_materials(mat_for)
    used = set()
    for ob in body_objs():
        for s in ob.material_slots:
            m = s.material
            if m.name in used:
                continue
            used.add(m.name)
            nt = m.node_tree
            t = nt.nodes.get("_bake") or nt.nodes.new("ShaderNodeTexImage")
            t.name = "_bake"
            t.image = img
            nt.nodes.active = t
    for ob in bpy.data.objects:
        ob.select_set(False)
    objs = body_objs()
    for ob in objs:
        outline_mods(ob, False)
        ob.hide_render = False
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    sc.render.bake.margin = 0
    sc.render.bake.use_clear = True
    bpy.ops.object.bake(type='EMIT')
    for ob in objs:
        outline_mods(ob, True)
    restore(keep)
    for m in bpy.data.materials:
        if m.node_tree and m.node_tree.nodes.get("_bake"):
            m.node_tree.nodes.remove(m.node_tree.nodes["_bake"])
    a = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
    return a[::-1].copy()


def new_img(name, res, flt=True):
    im = bpy.data.images.get(name)
    if im:
        bpy.data.images.remove(im)
    return bpy.data.images.new(name, res, res, float_buffer=flt, alpha=True)


def set_pose(name, yaw):
    PZ.reset()
    if ARM.animation_data:
        ARM.animation_data.action = None
    PZ.apply(SP.POSES[name])
    ARM.rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()


def camera(size, yaw, pitch, ppm, zc):
    p = math.radians(pitch)
    d = Vector((0, math.cos(p), -math.sin(p)))
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    return L.ortho_camera(Vector((0, 0, zc)) - d * 10, d, scale=max(size) / ppm), d


def proj_map(cam):
    """World -> pixel (x right, y down) is affine for an ortho camera: [x, y] = A @ p + b."""
    o = np.array(L.project(cam, (0, 0, 0))[:2])
    A = np.stack([np.array(L.project(cam, e)[:2]) - o for e in ((1, 0, 0), (0, 1, 0), (0, 0, 1))], 1)
    return A.tolist(), o.tolist()


def render_float(path):
    sc = bpy.context.scene
    sc.render.image_settings.file_format = 'OPEN_EXR'
    sc.render.image_settings.color_depth = '32'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    a = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4)[::-1].copy()
    bpy.data.images.remove(im)
    return a


def sword(on):
    s = bpy.data.objects.get("Sword")
    if s:
        s.hide_render = not on


def hand_margin(cam):
    def sp(n):
        M = ARM.matrix_world @ ARM.pose.bones[n].matrix
        return L.project(cam, M.translation)[2]
    return sp("socket_hand_L") - sp("socket_hand_R")


MAIL_ON = "--mail" in argv


def body_objs():                              # noqa: F811  (mail parts only in mail views)
    names = BODY + (MAILP if MAIL_ON or MODE == "prep" else [])
    return [bpy.data.objects[n] for n in names if bpy.data.objects.get(n)]


for _n in MAILP:
    if bpy.data.objects.get(_n):
        bpy.data.objects[_n].hide_render = not (MAIL_ON or MODE == "prep")


if MODE == "mail":
    # scale-mail shell: the Shirt's torso (vertices mostly weighted to the trunk) pushed out along the normals, with the
    # Shirt's own weights; two pauldrons = flattened spheres on the upper arms, 100% to the upper-arm bones
    out = argv[1]
    shirt = bpy.data.objects["Shirt"]
    mail = shirt.copy()
    mail.data = shirt.data.copy()
    mail.name = mail.data.name = "Mail"
    bpy.context.scene.collection.objects.link(mail)
    trunk = {g.index for g in mail.vertex_groups if g.name in ("Hips", "Spine", "Chest", "Neck", "RightShoulder",
                                                               "LeftShoulder")}
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mail.data)
    dl = bm.verts.layers.deform.active
    kill = [v for v in bm.verts if sum(w for gi, w in v[dl].items() if gi in trunk) < 0.6]
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.014
    bm.to_mesh(mail.data)
    bm.free()
    mail["outline_scale"] = 1.0
    sh = bpy.data.objects["Shirt"]
    for side, sx in (("Right", -1), ("Left", 1)):
        b = ARM.data.bones[side + "UpperArm"]
        hd = ARM.matrix_world @ b.head_local
        tl = ARM.matrix_world @ b.tail_local
        c = hd + (tl - hd) * 0.18 + Vector((0, 0, 0.03))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1, location=c)
        p = bpy.context.active_object
        p.name = "Pauldron" + side
        p.scale = (0.085, 0.09, 0.075)
        bpy.ops.object.transform_apply(scale=True)
        vg = p.vertex_groups.new(name=side + "UpperArm")
        vg.add(list(range(len(p.data.vertices))), 1.0, 'REPLACE')
        p.data.materials.append(sh.data.materials[0])
    # join the two pauldrons into one object
    for o in bpy.data.objects:
        o.select_set(o.name in ("PauldronRight", "PauldronLeft"))
    bpy.context.view_layer.objects.active = bpy.data.objects["PauldronRight"]
    bpy.ops.object.join()
    pl = bpy.context.active_object
    pl.name = pl.data.name = "Pauldrons"
    mod = pl.modifiers.new("Armature", 'ARMATURE')
    mod.object = ARM
    pl.parent = ARM
    pl.matrix_parent_inverse = ARM.matrix_world.inverted()
    out_mat = bpy.data.materials.get("Outline")
    if out_mat:
        pl.data.materials.append(out_mat)
        L.add_outline(pl, L.OUTLINE_M)
    print("MAIL verts", len(mail.data.vertices), "pauldrons", len(pl.data.vertices))
    bpy.ops.wm.save_as_mainfile(filepath=out)
elif MODE == "prep":
    out, res = argv[1], int(argv[2])
    # the face texture keeps its own cylindrical UVs: pin its image node to "UVMap" before the atlas becomes active
    for m in bpy.data.materials:
        if m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and not n.inputs["Vector"].is_linked:
                    uv = m.node_tree.nodes.new("ShaderNodeUVMap")
                    uv.uv_map = "UVMap"
                    m.node_tree.links.new(uv.outputs[0], n.inputs["Vector"])
    PZ.reset()
    if ARM.animation_data:
        ARM.animation_data.action = None
    for ob in bpy.data.objects:
        ob.select_set(False)
    objs = body_objs()
    for ob in objs:
        uvs = ob.data.uv_layers
        at = uvs.get("Atlas") or uvs.new(name="Atlas")
        uvs.active = at
        at.active_render = True
        ob.select_set(True)
        print("OBJ", ob.name, [s.material.name if s.material else None for s in ob.material_slots], len(ob.data.vertices))
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.003, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.003, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    set_pose("stance", 40)
    fb = new_img("fallback", res)
    a = bake_to(fb, lambda ob: None)       # the old flat look in the stance, as the last-resort fill
    np.save(os.path.join(os.path.dirname(out), "fallback.npy"), a)
    print("prep: atlas made, fallback coverage", float((a[..., 3] > 0.5).mean()))
    bpy.ops.wm.save_as_mainfile(filepath=out)
elif MODE == "view":
    out, pose, yaw, pitch = argv[1], argv[2], float(argv[3]), float(argv[4])
    size = tuple(int(v) for v in argv[5].split("x"))
    ppm, zc = float(argv[6]), float(argv[7])
    res = bpy.data.images.get("fallback").size[0] if bpy.data.images.get("fallback") else 2048
    os.makedirs(out, exist_ok=True)
    sword(False)
    set_pose(pose, yaw)
    cam, d = camera(size, yaw, pitch, ppm, zc)
    # texels in this pose
    tp = bake_to(new_img("_tp", res), lambda ob: pos_mat())
    tn = bake_to(new_img("_tn", res), lambda ob: nrm_mat())
    ti = bake_to(new_img("_ti", res), lambda ob: id_mat(ID[ob.name]))
    np.save(os.path.join(out, "tex_pos.npy"), tp[..., :3] - OFF)
    np.save(os.path.join(out, "tex_nrm.npy"), tn[..., :3] * 2 - 1)
    np.save(os.path.join(out, "tex_id.npy"), np.rint(ti[..., 0] * 16).astype(np.uint8) * (ti[..., 3] > 0.5))
    # camera passes
    cycles(1)
    keep = swap_materials(lambda ob: pos_mat())
    for ob in body_objs():
        outline_mods(ob, False)
    rp = render_float(os.path.join(out, "_pos.exr"))
    restore(keep)
    keep = swap_materials(lambda ob: id_mat(ID[ob.name]))
    ri = render_float(os.path.join(out, "_id.exr"))
    restore(keep)
    np.save(os.path.join(out, "pix_pos.npy"), np.where(rp[..., 3:] > 0.5, rp[..., :3] - OFF, np.nan))
    np.save(os.path.join(out, "pix_id.npy"), (np.rint(ri[..., 0] * 16) * (ri[..., 3] > 0.5)).astype(np.uint8))
    for ob in body_objs():
        outline_mods(ob, True)
    # the old flat look from this camera (registration reference and Qwen input)
    L.render_setup(size)
    bpy.context.scene.render.filepath = os.path.join(out, "flat.png")
    bpy.context.scene.render.image_settings.file_format = 'PNG'
    bpy.context.scene.render.image_settings.color_depth = '8'
    bpy.ops.render.render(write_still=True)
    A, b = proj_map(cam)
    json.dump({"A": A, "b": b, "view_dir": [-d.x, -d.y, -d.z], "size": size, "ppm": ppm, "pose": pose, "yaw": yaw,
               "pitch": pitch, "zc": zc, "ground": list(L.project(cam, (0, 0, 0))[:2]),
               "hand_margin": hand_margin(cam), "ids": ID}, open(os.path.join(out, "proj.json"), "w"), indent=1)
    print("VIEW", pose, yaw, "hand margin (L depth - R depth, >0 = right hand nearer)", round(hand_margin(cam), 3))
elif MODE == "show":
    atlas, out = argv[1], argv[2]
    specs = [a for a in argv[3:] if not a.startswith("--")]
    magenta = "--magenta" in argv
    mail = "--mail" in argv
    os.makedirs(out, exist_ok=True)
    img = bpy.data.images.load(atlas)
    img.colorspace_settings.name = 'sRGB'

    def b(nt, em):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = img
        t.interpolation = 'Linear'
        uv = nt.nodes.new("ShaderNodeUVMap")
        uv.uv_map = "Atlas"
        nt.links.new(uv.outputs[0], t.inputs["Vector"])
        if magenta:
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = 'RGBA'
            mix.inputs[6].default_value = (1, 0, 1, 1)
            nt.links.new(t.outputs["Alpha"], mix.inputs[0])
            nt.links.new(t.outputs["Color"], mix.inputs[7])
            nt.links.new(mix.outputs[2], em.inputs["Color"])
        else:
            nt.links.new(t.outputs["Color"], em.inputs["Color"])
    skin = emission_mat("_skin", b)
    skin.use_backface_culling = True
    outline = bpy.data.materials.get("Outline")
    for ob in body_objs():
        for s in ob.material_slots:
            if s.material != outline:
                s.material = skin
    for n in MAILP:
        if bpy.data.objects.get(n):
            bpy.data.objects[n].hide_render = not mail
    meta = {}
    for spec in specs:
        f = spec.split(":")
        name, pose, yaw, pitch = f[0], f[1], float(f[2]), float(f[3])
        size = tuple(int(v) for v in f[4].split("x"))
        ppm, zc = float(f[5]), float(f[6])
        sword(len(f) > 7 and f[7] == "sword")
        set_pose(pose, yaw)
        L.render_setup(size)
        cam, d = camera(size, yaw, pitch, ppm, zc)
        bpy.context.scene.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
        lm = {}
        for k_, (bn, end) in {"head": ("socket_head", "head"), "hand_R": ("socket_hand_R", "head"),
                              "elbow_R": ("RightLowerArm", "head"), "elbow_L": ("LeftLowerArm", "head"),
                              "knee_L": ("LeftLowerLeg", "head"), "knee_R": ("RightLowerLeg", "head"),
                              "foot_L": ("LeftFoot", "head"), "foot_R": ("RightFoot", "head"),
                              "shoulder_R": ("RightUpperArm", "head")}.items():
            pb = ARM.pose.bones[bn]
            lm[k_] = list(L.project(cam, ARM.matrix_world @ (pb.head if end == "head" else pb.tail))[:2])
        meta[name] = {"ground": list(L.project(cam, (0, 0, 0))[:2]), "ppm": ppm, "size": size, "pose": pose,
                      "yaw": yaw, "hand_margin": hand_margin(cam), "lm": lm}
        print("SHOW", name, "hand margin", round(hand_margin(cam), 3))
    mp = os.path.join(out, "meta.json")
    old = json.load(open(mp)) if os.path.exists(mp) else {}
    old.update(meta)
    json.dump(old, open(mp, "w"), indent=1)
print("DONE", MODE)
