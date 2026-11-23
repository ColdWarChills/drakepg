import pathlib
p = pathlib.Path("rig.py"); s = p.read_text()
s = s.replace("""        fm = pose["fist"]
        f = p.fists[fm]""", """        fm = pose.get("fist")
        if fm is None:  # the sleeve alone (elbow sheets)
            info["flipped"] = self.flipped
            out = np.zeros_like(frame)
            for lay in sleeve_layers:
                out[:] = lay + out * (1 - lay[..., 3:4])
            return out, info
        f = p.fists[fm]""")
s = s.replace("""        info["flipped"] = getattr(render_mesh, "flipped", 0)""", """        info["flipped"] = self.flipped""")
s = s.replace("""        sleeve_layers = []
""", """        sleeve_layers = []
        self.flipped = 0
""")
s = s.replace("""        return render_mesh(limb.src, limb.uv_src(), dst, limb.tris, (self.w, self.h))""", """        out = render_mesh(limb.src, limb.uv_src(), dst, limb.tris, (self.w, self.h))
        self.flipped += render_mesh.flipped
        return out""")
p.write_text(s)
