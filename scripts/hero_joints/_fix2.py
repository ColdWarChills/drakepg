import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
s = s.replace("""    a = cutlib.key_purple(src)
""", """    # alpha from the distance to the purple field (soft edge); no hole filling: the gap between the near arm and the
    # body is enclosed and must stay background
    dpu = np.abs(src.astype(np.float32) - np.array(cutlib.PURPLE)).max(-1)
    a = np.clip((dpu - 40) / 50, 0, 1)
    nlab, lab, st, _ = cv2.connectedComponentsWithStats((a > 0.5).astype(np.uint8), 8)
    big = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
    a = a * (cv2.dilate(big.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0)
""")
p.write_text(s)
p = pathlib.Path("fistcheck.py"); s = p.read_text()
s = s.replace("""    im = rig.to_image(px, BG)
    g = info["W"]
    c = 70
    box = (int(g[0] - c), int(g[1] - c), int(g[0] + c), int(g[1] + c))
    crop = im.crop(box).resize((4 * 2 * c, 4 * 2 * c), Image.NEAREST)""", """    im = rig.to_image(px, BG)
    g = info["W"]
    # the same zoom as the approved sheet: its hero is ~319 px tall (ours 1028), then both 4x
    k = 319 / 1028
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    c = 70
    gx, gy = g[0] * k, g[1] * k
    box = (int(gx - c), int(gy - c), int(gx + c), int(gy + c))
    crop = im.crop(box).resize((4 * 2 * c, 4 * 2 * c), Image.NEAREST)""")
s = s.replace("""r1 = ref.crop((290, 740, 400, 850)).resize((560, 560), Image.NEAREST)
r2 = ref.crop((410, 1160, 520, 1270)).resize((560, 560), Image.NEAREST)""", """r1 = ref.crop((303, 718, 443, 858)).resize((560, 560), Image.NEAREST)
r2 = ref.crop((403, 1166, 543, 1306)).resize((560, 560), Image.NEAREST)""")
p.write_text(s)
