import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
s = s.replace("    tube = (u > 30) & (u < P[\"u_cuff_end\"])", "    tube = (u > 0) & (u < P[\"u_cuff_end\"])")
s = s.replace("    keep_top = near & (u <= 30)  # the drawn shoulder stays as drawn", "    keep_top = near & (u <= 0)  # the drawn shoulder cap stays as drawn")
s = s.replace('''    near_px = cutlib.line_on(near_px, ((u > 0) & (u < P["u_cuff_end"] - 10)).astype(np.float32), width=1.8, dark=0.72)''',
'''    # the edge line, drawn on the smooth edges themselves (anti-aliased): the local colour darkened, 1.8 px
    lw = 1.8
    band = (np.clip(lw + 0.5 - (vout - v), 0, 1) + np.clip(lw + 0.5 - (v - vinn), 0, 1)).clip(0, 1)
    band *= tube & (u < P["u_cuff_end"] - 10) & ~((v < v_in + 3) & (u < 105))  # the straight cut has its own line
    near_px[..., :3] = near_px[..., :3] * (1 - 0.28 * band[..., None])''')
p.write_text(s)
