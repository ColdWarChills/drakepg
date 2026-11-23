import pathlib
p = pathlib.Path("rig.py"); s = p.read_text()
s = s.replace('''S0 = np.array(P["S"])
W0 = np.array(P["W"])
L = float(np.linalg.norm(W0 - S0))
UE = 0.516 * L          # elbow along the sleeve (old hero: upper 0.145 H, forearm 0.136 H)
''', '''S0 = np.array(P["S"])
W0 = np.array(P["W"])
E0 = np.array(P["E"])
L = float(P["L"])
UE = float(P["UE"])     # elbow along the sleeve: 0.52 L (old hero: upper 0.145 H, forearm 0.136 H)
A_SRC, B_SRC = float(P["a_src"]), float(P["b_src"])   # the drawn sleeve: upper part angle, its gentle drawn bend
''')
# kind A source split at the elbow by the bisector; disc about the elbow
s = s.replace('''        d = (W0 - S0) / L
        n = np.array([-d[1], d[0]])
        u = (xx - S0[0]) * d[0] + (yy - S0[1]) * d[1]
        v = (xx - S0[0]) * n[0] + (yy - S0[1]) * n[1]
        a = src[..., 3]''', '''        d1, d2 = vec(A_SRC), vec(A_SRC - B_SRC)
        bis = (d1 + d2) / np.linalg.norm(d1 + d2)
        n = np.array([-bis[1], bis[0]])
        u = UE + (xx - E0[0]) * bis[0] + (yy - E0[1]) * bis[1]
        v = (xx - E0[0]) * n[0] + (yy - E0[1]) * n[1]
        a = src[..., 3]''')
s = s.replace('''        ce = S0 + d * UE + n * vc''', '''        ce = E0 + n * vc''')
s = s.replace('''        ring = np.clip(dd - (r - 2.2), 0, 1) * disc_a''', '''        ring = np.zeros_like(disc_a)  # no drawn ring: inside the sleeve it read as a line across the joint''')
s = s.replace('''        d_src = (W0 - S0) / L
        limb_args = dict(P0=S0, d_src=d_src, half=HALF, u_elbow=UE, u_wrist=L)''', '''        limb_args = dict(P0=S0, d_src=vec(A_SRC), half=HALF, u_elbow=UE, u_wrist=L)''')
s = s.replace('''                limb = Limb(p.A_src[part], u_start=ua, u_end=ub, n_seg=60, **limb_args)''', '''                limb = Limb(p.A_src[part], u_start=ua, u_end=ub, n_seg=60, **limb_args)
                limb.src_c = rigid_two(limb.us, A_SRC, 0.0 if part == "upper" else B_SRC, UE)
                if part == "lower":  # the lower picture: straight along the drawn forearm, from the drawn elbow
                    limb.src_c = (vec(A_SRC, UE)[None] + vec(A_SRC - B_SRC)[None] * (limb.us - UE)[:, None],
                                  np.full(len(limb.us), A_SRC - B_SRC))''')
s = s.replace('''            limb = Limb(p.sleeve, u_start=u0, u_end=u1, n_seg=180, **limb_args)''', '''            limb = Limb(p.sleeve, u_start=u0, u_end=u1, n_seg=180, **limb_args)
            limb.src_c = centreline(limb.us, A_SRC, B_SRC, UE, R_B)''')
s = s.replace('''        out = render_mesh(limb.src, limb.uv_src(), dst, limb.tris, (self.w, self.h))''', '''        sp, sphi = limb.src_c
        snrm = np.stack([np.cos(np.radians(sphi + 90)), np.sin(np.radians(sphi + 90))], 1)
        uv = []
        for c, nv in zip(sp, snrm):
            uv.append(S0 + c - nv * limb.half)
            uv.append(S0 + c + nv * limb.half)
        out = render_mesh(limb.src, np.array(uv), dst, limb.tris, (self.w, self.h))''')
p.write_text(s)
