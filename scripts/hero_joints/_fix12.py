import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
s = s.replace('''    torso_side = ~poly((H, Wd), [(0, 0)] + P["torso_left"] + [(0, 705)])  # True right of the torso's left edge''',
'''    torso_side = ~poly((H, Wd), [(0, 0)] + P["torso_left"] + [(682, 790), (0, 790)])  # right of the torso's left edge''')
s = s.replace('''    hips = fig & (yy >= P["hips_top"]) & ~near & ~farm & ~corridor & ~(skin & (xx < 660) & (yy < 900))''',
'''    drawn_fist = poly((H, Wd), [(548, 768), (652, 768), (652, 888), (548, 888)])  # the drawing's own near fist
    hips = fig & (yy >= P["hips_top"]) & ~near & ~farm & ~corridor & ~drawn_fist''')
s = s.replace('''    lw = 1.8''', '''    lw = 2.2''')
s = s.replace('''    near_px[..., :3] = near_px[..., :3] * (1 - 0.28 * band[..., None])''', '''    near_px[..., :3] = near_px[..., :3] * (1 - 0.42 * band[..., None])''')
p.write_text(s)
p = pathlib.Path("rig.py"); s = p.read_text()
s = s.replace('''        frame[:] = body + frame * (1 - body[..., 3:4])
        if swl is not None:''', '''        frame[:] = body + frame * (1 - body[..., 3:4])
        # the arm's hard cel shadow on the body (light from his upper back): the sleeve and fist shifted, on body only
        arm_a = np.zeros(frame.shape[:2], np.float32)
        for lay in [fist_lay] + sleeve_layers:
            arm_a = lay[..., 3] + arm_a * (1 - lay[..., 3])
        sh = np.zeros_like(arm_a)
        dx, dy = SHADOW_OFF
        sh[dy:, dx:] = arm_a[:-dy, :-dx]
        sh = sh * body[..., 3] * (1 - arm_a)
        frame[..., :3] *= 1 - SHADOW_DARK * sh[..., None]
        if swl is not None:''')
s = s.replace('''KINDS = {''', '''SHADOW_OFF = (5, 7)     # px: the arm's shadow on the body (hard edged, like the drawing's cel shadows)
SHADOW_DARK = 0.2
KINDS = {''')
p.write_text(s)
