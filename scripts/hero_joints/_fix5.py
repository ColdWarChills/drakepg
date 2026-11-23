import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
s = s.replace("""    # purple left in soft or enclosed edge pixels (the arm/body gap): off
    purple = (B > G + 25) & (R > G + 10)
    px[purple, 3] = 0
    print("purple px taken off", int(purple.sum()))
""", """    # soft edge pixels take the colour of the nearest solid pixel (no purple fringe; the alpha stays soft)
    solid = px[..., 3] > 245
    px[..., :3] = np.where(solid[..., None], px[..., :3], cutlib.fill_rgb(px, solid))
""")
s = s.replace("""    near = fig & (corridor | disc) & ~(skin & (u > 200))
    near_px = px.copy()
    near_px[~near] = 0
""", """    # the sleeve's inner (torso-side, v < 0) edge on its free part; above the armpit the drawing runs on into the
    # torso, so there the piece is cut on that edge's straight line (with a drawn line on it)
    inner = []
    for uu in np.arange(130, 230, 5.0):
        c = S + d * uu
        l0, l1, _ = cutlib.section(fig & ~skin, c, d)
        inner.append(-l0 if n @ np.array([-d[1], d[0]]) > 0 else l1)
    v_in = -float(np.median(np.abs(inner)))
    P["v_inner"] = v_in
    near = fig & (corridor | disc) & ~(skin & (u > 200)) & (v > v_in - 1.5)
    near_px = px.copy()
    near_px[~near] = 0
    near_px[..., 3] *= np.clip((v - v_in + 1.5) / 1.5, 0, 1)
    cutline = ((v < v_in + 3) & (u < 125)).astype(np.float32)
    near_px = cutlib.line_on(near_px, cutline, width=2.0)
""")
p.write_text(s)
