import pathlib
p = pathlib.Path("swing.py"); s = p.read_text()
old_a = s.index("    (46, -125.0")
old_b = s.index("    (50, 64.0")
s = s[:old_a] + """    (46, -114.0, -36.0, 111.0, -1.6, 1.0, 1.6, -3.0, -8.0),  # the blade lags (turn opens)
    (47, -74.0, -3.0, 104.0, 0.4, 0.0, -0.2, 0.0, -6.0),     # over the top, arm straight
    (48, -32.0, 7.0, 76.0, 2.6, -1.3, -2.0, 5.0, -2.0),
    (49, 12.0, 9.0, 42.0, 4.8, -2.4, -3.8, 10.0, 3.0),
""" + s[old_b:]
p.write_text(s)
