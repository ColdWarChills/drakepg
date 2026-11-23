import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
s = s.replace('''    near_px = cutlib.line_on(near_px, cutline, width=2.0)
''', '''    near_px = cutlib.line_on(near_px, cutline, width=2.0)
    # the elbow: the drawn sleeve steps out on its outer edge there and its edge shading starts there; straightened
    # or bent that reads as a notch. Round the outline and fade the colours to the flat cloth near the elbow, then put
    # the edge line back on the rounded outline.
    lit0 = near & (u > 60) & (u < 240) & (np.abs(v) < w / 4)
    cloth = np.median(rgb[lit0], 0)
    near_px = cutlib.smooth_outline(near_px, E, 70.0, sigma=6.0, fade=0.4)
    near_px = cutlib.flatten(near_px, E, 30.0, 85.0, cloth)
    ring = (np.hypot(xx - E[0], yy - E[1]) < 95).astype(np.float32)
    near_px = cutlib.line_on(near_px, ring * ((u > 40) & (u < 240)), width=1.6, dark=0.78)
''')
p.write_text(s)
