import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
a = s.index("    # ---- near sleeve: corridor along S->W")
b = s.index("    # pixels of the near piece that lie on the torso side")
s = s[:a] + """    # ---- near sleeve: the drawn sleeve's own centre line (_axis.py: section middles) is two straight parts with a
    # gentle drawn bend: from S at a_src, bending by b_src at u = UE, to the wrist W at u = L
    S = np.array(P["S"])
    a_src, b_src, UE, L = P["a_src"], P["b_src"], P["UE"], P["L"]
    d1 = np.array([np.cos(np.radians(a_src)), np.sin(np.radians(a_src))])
    d2 = np.array([np.cos(np.radians(a_src - b_src)), np.sin(np.radians(a_src - b_src))])
    E = S + d1 * UE
    P["E"] = E.tolist()
    P["W"] = (E + d2 * (L - UE)).tolist()
    bis = (d1 + d2) / np.linalg.norm(d1 + d2)
    lower_side = ((xx - E[0]) * bis[0] + (yy - E[1]) * bis[1]) > 0
    u_up = (xx - S[0]) * d1[0] + (yy - S[1]) * d1[1]
    v_up = (xx - S[0]) * -d1[1] + (yy - S[1]) * d1[0]
    u_lo = UE + (xx - E[0]) * d2[0] + (yy - E[1]) * d2[1]
    v_lo = (xx - E[0]) * -d2[1] + (yy - E[1]) * d2[0]
    u = np.where(lower_side, u_lo, u_up)
    v = np.where(lower_side, v_lo, v_up)
    w = P["sleeve_w"]
    corridor = (np.abs(v) < w / 2 + 8) & (u > 0) & (u < P["u_cuff_end"])
    disc = np.hypot(xx - S[0], yy - S[1]) < w / 2 + 4
    torso_side = ~poly((H, Wd), [(0, 0)] + P["torso_left"] + [(0, 705)])  # True right of the torso's left edge
    # the inner (torso-side, v < 0) edge: above the armpit the drawing runs on into the torso, so there the piece is
    # cut on the free part's inner edge line, with a drawn line on it
    ins = []
    for uu in np.arange(110, 230, 5.0):
        dd = d1 if uu <= UE else d2
        c = (S + d1 * uu) if uu <= UE else (E + d2 * (uu - UE))
        l0, l1, _ = cutlib.section(fig & ~skin, c, dd)
        ins.append(l0)  # extent along -n (v < 0 side), negative
    v_in = float(np.median(ins))
    P["v_inner"] = v_in
    near = fig & (corridor | disc) & ~(skin & (u > 200)) & (v > v_in - 1.5)
    near_px = px.copy()
    near_px[~near] = 0
    near_px[..., 3] *= np.clip((v - v_in + 1.5) / 1.5, 0, 1)
    cutline = ((v < v_in + 3) & (u < 105)).astype(np.float32)
    near_px = cutlib.line_on(near_px, cutline, width=2.0)
""" + s[b:]
s = s.replace('''    "S": (652.0, 540.0), "W": (601.0, 795.0), "sleeve_w": 72.0, "u_cuff_end": 268.0,''',
 '''    "S": (660.0, 543.0), "a_src": 110.8, "b_src": 13.0, "UE": 134.0, "L": 258.5, "sleeve_w": 72.0, "u_cuff_end": 269.0,''')
s = s.replace('''    P["L"] = float(np.linalg.norm(Wr - S))
''', "")
s = s.replace('''    print({k: int((v[..., 3] > 128).sum()) for k, v in parts.items()}, "L", round(P["L"], 1), "cloth", P["cloth_rgb"])''',
 '''    print({k: int((v[..., 3] > 128).sum()) for k, v in parts.items()}, "v_inner", v_in, "cloth", P["cloth_rgb"])''')
p.write_text(s)
