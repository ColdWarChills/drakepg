import pathlib
p = pathlib.Path("cut.py"); s = p.read_text()
s = s.replace("""    px[rim, 3] = 0
""", """    px[rim, 3] = 0
    # purple left in soft or enclosed edge pixels (the arm/body gap): off
    purple = (B > G + 25) & (R > G + 10)
    px[purple, 3] = 0
    print("purple px taken off", int(purple.sum()))
""")
p.write_text(s)
p = pathlib.Path("rig.py"); s = p.read_text()
s = s.replace("window=(-150, 220, 1600, 580)", "window=(-150, 190, 1800, 700)")
p.write_text(s)
