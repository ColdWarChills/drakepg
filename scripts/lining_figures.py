"""Make a font's digits lining figures (all as tall as capitals) by default.

Alegreya and Alegreya Sans draw old-style figures by default (3, 4, 5, 7, 9 dip below the line, 1 and 0 are short),
which looks uneven in HP bars. Browsers could switch with `font-variant-numeric: lining-nums`, but Unity's text
can't turn OpenType features on, so this points the cmap's 0-9 straight at the `zero.lf`..`nine.lf` glyphs the
fonts already carry. The fonts are OFL with no Reserved Font Name, so the modified files may keep their names.

No dependencies (fontTools isn't installed here): reads the post names, rebuilds the cmap, fixes the checksums.

    python scripts/lining_figures.py unity/Assets/Resources/Fonts/Alegreya*.ttf
"""
import struct
import sys

DIGITS = ['zero', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine']


def read_tables(b):
    n = struct.unpack('>H', b[4:6])[0]
    t = {}
    for i in range(n):
        tag, _, off, ln = struct.unpack('>4sIII', b[12 + 16 * i:28 + 16 * i])
        t[tag.decode('latin1')] = b[off:off + ln]
    return b[:4], t


def glyph_names(post):
    if struct.unpack('>I', post[:4])[0] != 0x20000:
        raise SystemExit('post table has no glyph names')
    ng = struct.unpack('>H', post[32:34])[0]
    idx = struct.unpack('>%dH' % ng, post[34:34 + 2 * ng])
    q, names = 34 + 2 * ng, []
    while q < len(post):
        n = post[q]
        names.append(post[q + 1:q + 1 + n].decode('latin1'))
        q += 1 + n
    return [None if i < 258 else names[i - 258] for i in idx]


def read_cmap(cmap):
    """{(platform, encoding): (format, mapping or raw bytes)}"""
    n = struct.unpack('>H', cmap[2:4])[0]
    out = {}
    for i in range(n):
        pid, eid, off = struct.unpack('>HHI', cmap[4 + 8 * i:12 + 8 * i])
        fmt = struct.unpack('>H', cmap[off:off + 2])[0]
        if fmt == 4:
            seg2 = struct.unpack('>H', cmap[off + 6:off + 8])[0]
            seg = seg2 // 2
            ends = struct.unpack('>%dH' % seg, cmap[off + 14:off + 14 + seg2])
            p = off + 16 + seg2
            starts = struct.unpack('>%dH' % seg, cmap[p:p + seg2])
            deltas = struct.unpack('>%dh' % seg, cmap[p + seg2:p + 2 * seg2])
            ro_at = p + 2 * seg2
            ranges = struct.unpack('>%dH' % seg, cmap[ro_at:ro_at + seg2])
            m = {}
            for s in range(seg):
                for c in range(starts[s], ends[s] + 1):
                    if c == 0xFFFF:
                        continue
                    if ranges[s] == 0:
                        g = (c + deltas[s]) & 0xFFFF
                    else:
                        a = ro_at + 2 * s + ranges[s] + 2 * (c - starts[s])
                        g = struct.unpack('>H', cmap[a:a + 2])[0]
                        if g:
                            g = (g + deltas[s]) & 0xFFFF
                    if g:
                        m[c] = g
            out[(pid, eid)] = (4, m)
        elif fmt == 12:
            ng = struct.unpack('>I', cmap[off + 12:off + 16])[0]
            m = {}
            for k in range(ng):
                sc, ec, sg = struct.unpack('>III', cmap[off + 16 + 12 * k:off + 28 + 12 * k])
                for c in range(sc, ec + 1):
                    m[c] = sg + c - sc
            out[(pid, eid)] = (12, m)
        else:
            ln = struct.unpack('>I', cmap[off + 2:off + 6])[0] if fmt in (8, 10, 12, 13, 14) else struct.unpack('>H', cmap[off + 2:off + 4])[0]
            out[(pid, eid)] = (fmt, cmap[off:off + ln])
    return out


def runs(m):
    """Consecutive codes whose glyphs are consecutive too."""
    cs = sorted(m)
    out, i = [], 0
    while i < len(cs):
        j = i
        while j + 1 < len(cs) and cs[j + 1] == cs[j] + 1 and m[cs[j + 1]] == m[cs[j]] + 1:
            j += 1
        out.append((cs[i], cs[j], m[cs[i]]))
        i = j + 1
    return out


def build4(m):
    segs = [(s, e, (g - s) & 0xFFFF) for s, e, g in runs({c: g for c, g in m.items() if c < 0xFFFF})] + [(0xFFFF, 0xFFFF, 1)]
    n = len(segs)
    sr = 2
    while sr * 2 <= n * 2:
        sr *= 2
    es = sr.bit_length() - 2
    body = struct.pack('>%dH' % n, *[e for _, e, _ in segs]) + b'\0\0'
    body += struct.pack('>%dH' % n, *[s for s, _, _ in segs])
    body += struct.pack('>%dH' % n, *[d for _, _, d in segs])
    body += struct.pack('>%dH' % n, *([0] * n))
    head = struct.pack('>HHHHHHH', 4, 14 + len(body), 0, 2 * n, sr, es, 2 * n - sr)
    return head + body


def build12(m):
    groups = runs(m)
    body = b''.join(struct.pack('>III', s, e, g) for s, e, g in groups)
    return struct.pack('>HHIII', 12, 0, 16 + len(body), 0, len(groups)) + body


def write_cmap(subs):
    keys = sorted(subs)
    blobs, offsets, data = {}, {}, b''
    start = 4 + 8 * len(keys)
    for k in keys:
        fmt, v = subs[k]
        blob = build4(v) if fmt == 4 else build12(v) if fmt == 12 else v
        if blob not in blobs:
            blobs[blob] = start + len(data)
            data += blob
            while len(data) % 4:
                data += b'\0'
        offsets[k] = blobs[blob]
    head = struct.pack('>HH', 0, len(keys)) + b''.join(struct.pack('>HHI', p, e, offsets[(p, e)]) for p, e in keys)
    return head + data


def checksum(b):
    b += b'\0' * (-len(b) % 4)
    return sum(struct.unpack('>%dI' % (len(b) // 4), b)) & 0xFFFFFFFF


def write_font(version, tables):
    tags = sorted(tables)
    n = len(tags)
    sr = 16
    while sr * 2 <= n * 16:
        sr *= 2
    es = (sr // 16).bit_length() - 1
    head = struct.pack('>4sHHHH', version, n, sr, es, n * 16 - sr)
    tables = dict(tables)
    tables['head'] = tables['head'][:8] + b'\0\0\0\0' + tables['head'][12:]
    off = 12 + 16 * n
    recs, data = b'', b''
    for t in tags:
        d = tables[t]
        recs += struct.pack('>4sIII', t.encode('latin1'), checksum(d), off + len(data), len(d))
        data += d + b'\0' * (-len(d) % 4)
    font = bytearray(head + recs + data)
    adj = (0xB1B0AFBA - checksum(bytes(font))) & 0xFFFFFFFF
    h = font.index(b'head', 12)
    hoff = struct.unpack('>I', font[h + 8:h + 12])[0]
    font[hoff + 8:hoff + 12] = struct.pack('>I', adj)
    return bytes(font)


def main(paths):
    for p in paths:
        b = open(p, 'rb').read()
        version, tables = read_tables(b)
        names = glyph_names(tables['post'])
        lf = [names.index(d + '.lf') if d + '.lf' in names else None for d in DIGITS]
        if None in lf:
            print(f'{p}: no .lf digits, skipped')
            continue
        subs = read_cmap(tables['cmap'])
        for fmt, m in subs.values():
            if fmt in (4, 12):
                for i, g in enumerate(lf):
                    m[0x30 + i] = g
        tables['cmap'] = write_cmap(subs)
        open(p, 'wb').write(write_font(version, tables))
        print(f'{p}: digits -> lining')


if __name__ == '__main__':
    main(sys.argv[1:])
