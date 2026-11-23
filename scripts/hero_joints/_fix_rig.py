import re, pathlib
p = pathlib.Path("rig.py")
s = p.read_text()
start = s.index("        # body: lean about the waist")
end = s.index("        # near shoulder (drawing canvas -> frame)")
s = s[:start] + """        waist = np.array(P["waist"])
        Mb = rot_about(waist, waist + off, pose["lean"])
        body = self._body(pose, off) if with_body else np.zeros_like(frame)
""" + s[end:]
a = s.index("                pts, phi = rigid_two(limb.us, a1, b, UE)\n")
b_ = s.index("                sleeve_layers.append(self._mesh(limb, pts, phi, S + 0, kind))")
s = s[:a] + """                if part == "upper":
                    pts, phi = rigid_two(limb.us, a1, 0.0, UE)  # rigid: straight past the elbow
                else:
                    el = vec(a1, UE)
                    pts = el[None] + vec(a1 - b)[None] * (limb.us - UE)[:, None]
                    phi = np.full(len(limb.us), a1 - b)
""" + s[b_:]
a = s.index("        # stack: rim (under everything)")
b_ = s.index("        info[\"flipped\"]")
s = s[:a] + """        # stack: rim (under everything), body, sword, fist, sleeve; the rim is found on everything but the sword
        fist_lay = warp_into(np.zeros_like(frame), f["img"], f["origin"], Mfist)
        hero = body.copy()
        for lay in [fist_lay] + sleeve_layers:
            hero[:] = lay + hero * (1 - lay[..., 3:4])
        if rim:
            ring = rim_ring(hero[..., 3])
            frame[..., :3] = RIM_RGB[None, None] * ring[..., None]
            frame[..., 3] = ring
        frame[:] = body + frame * (1 - body[..., 3:4])
        if swl is not None:
            frame[:] = swl + frame * (1 - swl[..., 3:4])
        for lay in [fist_lay] + sleeve_layers:
            frame[:] = lay + frame * (1 - lay[..., 3:4])
""" + s[b_:]
s = s.replace("        hero = np.zeros_like(frame)\n", "")
p.write_text(s)
