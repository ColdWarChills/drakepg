import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
a = s.index("    # the elbow: the drawn sleeve steps out on its outer edge")
b = s.index("    # pixels of the near piece that lie on the torso side")
s = s[:a] + """    # A clean tube: the drawn sleeve steps out on its outer edge at the elbow (a notch once straightened or bent).
    # Its two edges are measured along u (sections across the centre line), smoothed (the sleeve narrows smoothly
    # toward the cuff), and the piece is re-cut between them with the drawn colours; the edge line goes back on.
    us_m = np.arange(40.0, P["u_cuff_end"] - 4, 4.0)
    vo, vi = [], []
    for uu in us_m:
        dd = d1 if uu <= UE else d2
        c = (S + d1 * uu) if uu <= UE else (E + d2 * (uu - UE))
        l0, l1, _ = cutlib.section(fig & ~skin, c, dd)
        vo.append(l1)
        vi.append(l0)
    vo, vi = np.array(vo, float), np.array(vi, float)
    vi = np.maximum(vi, v_in)  # the straight cut above the armpit
    # a straight line fit per edge (least squares) is the plainest tube; the drawn sleeve narrows ~8 px to the cuff
    co = np.polyfit(us_m, vo, 1)
    ci = np.polyfit(us_m[us_m > 110], vi[us_m > 110], 1)
    P["edge_fit_outer"], P["edge_fit_inner"] = [float(c) for c in co], [float(c) for c in ci]
    P["edge_drawn_max_dev"] = [float(np.abs(np.polyval(co, us_m) - vo).max()),
                               float(np.abs(np.polyval(ci, us_m) - vi)[us_m > 110].max())]
    vout = np.polyval(co, u)
    vinn = np.maximum(np.polyval(ci, u), v_in)
    tube = (u > 30) & (u < P["u_cuff_end"])
    a_t = np.clip(vout - v + 0.5, 0, 1) * np.clip(v - vinn + 0.5, 0, 1)
    keep_top = near & (u <= 30)  # the drawn shoulder stays as drawn
    new_a = np.where(tube, a_t * 255, np.where(keep_top, near_px[..., 3], 0))
    # the tube's cuff end stays as drawn (its own outline)
    cuffz = (u > P["u_cuff_end"] - 14) & near
    new_a = np.where(cuffz, np.minimum(new_a, px[..., 3]), new_a)
    solid_n = near & (px[..., 3] > 245) & (np.abs(v - (vout + vinn) / 2) < (vout - vinn) / 2 - 4)
    rgbn = cutlib.fill_rgb(px, solid_n)
    near_px = np.concatenate([np.where(solid_n[..., None], px[..., :3], rgbn), new_a[..., None]], -1)
    lit0 = near & (u > 60) & (u < 240) & (np.abs(v) < w / 4)
    cloth = np.median(rgb[lit0], 0)
    near_px = cutlib.flatten(near_px, E, 30.0, 85.0, cloth)
    near_px = cutlib.line_on(near_px, ((u > 0) & (u < P["u_cuff_end"] - 10)).astype(np.float32), width=1.8, dark=0.72)
    near = near_px[..., 3] > 128
""" + s[b:]
p.write_text(s)
