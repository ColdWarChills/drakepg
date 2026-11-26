// Paper damage on a figure's picture (World/Paper.cs): the creases its blows left, the tears it gets when badly hurt
// and the holes fire burns in it, drawn on the picture by the shader (Fx/PaperSprite.shader while it lives,
// Fx/PaperFold.shader's sheet pass when it dies). "Sheet space": the picture with its height 1 and y up (x runs 0 to its
// width / height). Set per renderer through a MaterialPropertyBlock.

#define PAPER_MAX 16
#define PAPER_TEARS 2
#define PAPER_HOLES 4
#define PAPER_RIPS 3
#define PAPER_SNAPS 2

float4 _PaperRect;                  // the picture's rect in its texture's UV: min xy, size zw
float4 _PaperInfo;                  // x: width / height, y: how many creases, z: tears, w: holes
float4 _PaperCreases[PAPER_MAX];    // per crease: a point on it (xy) and its unit normal (zw), sheet space
float4 _PaperTears[PAPER_TEARS];    // per tear: where it starts at the edge (xy) and its tip (zw)
float4 _PaperHoles[PAPER_HOLES];    // per burn hole: its middle (xy), radius (z), how hot its edge still glows 0-1 (w)
// Water, ice and energy on the paper (step 1d). All 0: none.
float4 _PaperMore;                  // x: rips, y: snapped pieces, z: keep only snapped piece z - 1 (the falling piece; 0: off), w: texels a sheet unit
float4 _PaperWet;                   // x: how far down it has soaked 0-1, y: how dark the wet is 0-1, z: ripples left as it dried 0-1, w: seed
float4 _PaperFrost;                 // x: frost 0-1, y: seed
float4 _PaperRips[PAPER_RIPS];      // per rip: its two ends (xy, zw), sheet space
float4 _PaperRipInfo[PAPER_RIPS];   // per rip: x how open 0-1, y seed, z the side whose flap lifts (+1 / -1), w half its widest (sheet units)
float4 _PaperSnaps[PAPER_SNAPS];    // per snapped piece: a point on the break (xy), the unit normal toward the piece (zw)
float4 _PaperSnapInfo[PAPER_SNAPS]; // per snapped piece: x 1 once it has broken off, y seed
float4 _PaperWetAt;                 // where the water landed (xy, sheet space): the middle of the wet splash
float _PaperWetDebug;               // testing: the wet area painted flat magenta
float _PaperWetStyle;               // global (Paper.WetBlotches): 0 the first try (a front from the top), 1 a splash where the water landed, with runs below it

float2 PaperSheet(float2 uv)
{
    float2 s = (uv - _PaperRect.xy) / _PaperRect.zw;
    return float2(s.x * _PaperInfo.x, s.y);
}

float PaperHash(float n)
{
    return frac(sin(n) * 43758.5453);
}

// Smooth 1D value noise, 0-1.
float PaperNoise(float x)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3 - 2 * f);
    return lerp(PaperHash(i), PaperHash(i + 1), f);
}

// A valley crease: the side its normal points away from tilts to the light (lighter), the other away (darker),
// strongest at the fold and fading over a few px; a thin dark line in the bottom of the fold. `px`: one screen pixel
// in sheet units, for a smooth line.
float3 PaperCreased(float3 rgb, float2 p, float px)
{
    const float spread = 7.0 / 360.0;
    const float core = 0.8 / 360.0;
    int count = (int)_PaperInfo.y;
    for (int i = 0; i < PAPER_MAX; i++)
    {
        if (i >= count) break;
        float4 c = _PaperCreases[i];
        float d = dot(p - c.xy, c.zw);
        float ad = abs(d);
        float fall = exp(-ad / spread);
        float lit = d < 0 ? 0.16 * fall : 0;
        float shade = d >= 0 ? 0.2 * fall + 0.04 : 0;
        float ink = saturate((core - ad) / max(px, 1e-5) + 0.5) * 0.3;
        rgb *= 1 - shade - ink;
        rgb += (1 - rgb) * lit;
        // Frost gathers in the fold (like rime along a leaf's veins): a white line a couple of px wide.
        if (_PaperFrost.x > 0) rgb += (1 - rgb) * _PaperFrost.x * 0.45 * saturate(1 - ad / max(px * 1.2, core * 1.5));
    }
    return rgb;
}

float PaperHash2(float2 q)
{
    return frac(sin(dot(q, float2(127.1, 311.7))) * 43758.5453);
}

// Smooth 2D value noise, 0-1.
float PaperNoise2(float2 x)
{
    float2 i = floor(x);
    float2 f = frac(x);
    f = f * f * (3 - 2 * f);
    float a = PaperHash2(i), b = PaperHash2(i + float2(1, 0)), c = PaperHash2(i + float2(0, 1)), d = PaperHash2(i + 1);
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Soaked from the top down (straight alpha): darker and deeper in its own colour, patchy, a darker tide line at the wet
// front, a little see-through where it is wet; drying leaves soft light and dark ripples (cockling). `px`: one screen
// pixel in sheet units (thin lines keep a floor in px, so they show on small monsters).
// Where it is wet (try 2), > 0 inside, in sheet units: one irregular splash around where the water landed
// (`_PaperWetAt`), dry paper on every side, made of a few overlapping lobes with a wobbly edge, and short runs down from
// its lower edge. `level` 0-1 sizes it, so drying shrinks it.
float PaperWetArea(float2 p, float level, float seed)
{
    float2 at = _PaperWetAt.xy;
    float R = 0.1 + 0.2 * level;
    // A position warp at about the splash's own size (a few wobbles round its edge), and a finer one.
    float f = 1.6 / R;
    float2 w = float2(PaperNoise2(p * f + seed), PaperNoise2(p * f + seed + 17.7)) - 0.5;
    float2 w2 = float2(PaperNoise2(p * f * 2.7 + seed * 1.3), PaperNoise2(p * f * 2.7 + seed + 5.1)) - 0.5;
    float2 q = p + w * R * 0.55 + w2 * R * 0.16;
    float g = -1;
    for (int i = 0; i < 4; i++)
    {
        float a = PaperHash(seed * 2.7 + i * 1.9) * 6.283;
        float d = (i == 0 ? 0 : 0.45 + 0.25 * PaperHash(seed * 4.1 + i * 3.3)) * R;
        float2 o = at + float2(cos(a), sin(a) * 0.8) * d;
        float r = R * (i == 0 ? 0.75 : 0.45 + 0.2 * PaperHash(seed * 5.3 + i * 2.1));
        g = max(g, r - length(q - o));
    }
    // Runs: water gathering at the lower edge and running down, narrowing to a rounded end (a round bead at the end,
    // with the tide line round it, read as a rivet).
    float runs = saturate((level - 0.3) / 0.5);
    float2 qr = p + w2 * R * 0.06;
    for (int j = 0; j < 3; j++)
    {
        float x = at.x + (PaperHash(seed * 6.1 + j * 4.7) - 0.5) * R * 1.2;
        float top = at.y - R * 0.3;
        float len = R * (0.7 + 0.9 * PaperHash(seed * 8.3 + j * 1.7)) * runs;
        float wid = R * (0.06 + 0.04 * PaperHash(seed * 9.7 + j));
        float yy = clamp(qr.y, top - len, top);
        float t = (top - yy) / max(len, 1e-4);
        g = max(g, lerp(wid, wid * 0.7, t) - length(float2(qr.x - x, qr.y - yy)));
    }
    return g;
}

float4 PaperWet(float4 c, float2 p, float px)
{
    float level = _PaperWet.x, dark = _PaperWet.y, cockle = _PaperWet.z, seed = _PaperWet.w;
    if (_PaperWetStyle > 0.5 && level > 0.001 && dark > 0.001)
    {
        // Try 2: a crisp-edged wet area, deeper and richer in its own colour inside, with a tide line just inside
        // its edge: darker on light paper, a pale rim on dark paper (where darker would not show).
        float g = PaperWetArea(p, level, seed);
        float gp = g / max(fwidth(g), 1e-6);
        float wet = saturate(gp / 1.2 + 0.5);
        // The tide line: a band fading in from the edge, and a crisp line on the edge itself.
        float rimPx = max(3.0, 0.024 / max(px, 1e-6));
        float rim = wet * pow(saturate(1 - gp / rimPx), 1.5);
        float edgeLine = wet * saturate(1 - gp / max(1.5, 0.006 / max(px, 1e-6)));
        // Inside: darker and deeper in its own colour (like wet construction paper), wetter in big soft patches.
        float blotch = smoothstep(0.3, 0.7, PaperNoise2(p * 4.0 + seed * 5.3));
        float k = wet * dark * (0.8 + 0.2 * blotch);
        float lum = dot(c.rgb, float3(0.3, 0.59, 0.11));
        float3 deep = saturate(lum + (c.rgb - lum) * 1.2) * 0.62;
        c.rgb = lerp(c.rgb, deep, k);
        // The tide line darker still.
        c.rgb *= 1 - (0.25 * rim + 0.35 * edgeLine) * dark;
        // Wet paper goes a little see-through: the stage shows through it.
        c.a *= 1 - 0.25 * k;
        if (_PaperWetDebug > 0.5) c.rgb = lerp(c.rgb, float3(1, 0, 1), wet);
    }
    else if (level > 0.001 && dark > 0.001)
    {
        float front = 1.04 - level * 1.14 + (PaperNoise(p.x * 5 + seed) - 0.5) * 0.14 + (PaperNoise(p.x * 17 + seed * 3) - 0.5) * 0.05;
        float d = p.y - front;
        float soft = max(px * 1.5, 0.005);
        float wet = saturate(d / soft + 0.5);
        float patch = 0.75 + 0.25 * PaperNoise2(p * 7 + seed);
        float k = wet * dark * patch;
        float3 deep = pow(saturate(c.rgb), 1.55) * 0.84;
        c.rgb = lerp(c.rgb, deep, k);
        float band = max(px * 2.2, 0.007);
        float tide = saturate(1 - abs(d - band * 0.7) / band) * wet * dark;
        // The tide line: darker on light paper, a pale rim on dark paper (where darker would not show).
        float lumW = dot(c.rgb, float3(0.3, 0.59, 0.11));
        float paleRim = 1 - smoothstep(0.12, 0.3, lumW);
        c.rgb = lerp(c.rgb * (1 - 0.25 * tide), c.rgb + 0.1 * tide, paleRim);
        c.a *= 1 - 0.1 * k;
    }
    if (cockle > 0.001)
    {
        float where = 1;
        if (_PaperWetStyle > 0.5)
        {
            // Only where it was wet (the splash at its biggest), and a faint dried tide ring at that edge, showing as
            // the wet shrinks inside it.
            float g1 = PaperWetArea(p, 1, seed);
            where = saturate(g1 / 0.03 + 0.5);
            float gp1 = g1 / max(fwidth(g1), 1e-6);
            float ring = saturate(1 - abs(gp1 - 1.5) / max(1.5, 0.007 / max(px, 1e-6)));
            c.rgb *= 1 - 0.2 * ring * cockle * (1 - saturate(level * 1.5));
        }
        // Buckles in the sheet (an uneven height field, not regular waves) lit from the top left: the slopes facing
        // the light a little brighter, the others darker.
        float2 q = p * 7 + float2(PaperNoise2(p * 2 + seed), PaperNoise2(p * 2 + seed + 7)) * 1.5;
        float e = 0.08;
        float h0 = PaperNoise2(q + seed * 3);
        float hx = PaperNoise2(q + float2(e, 0) + seed * 3);
        float hy = PaperNoise2(q + float2(0, e) + seed * 3);
        float lit = ((hx - h0) - (hy - h0)) / e;
        c.rgb = saturate(c.rgb * (1 + 0.13 * cockle * where * clamp(lit, -1.5, 1.5)));
    }
    return c;
}

// Frosted (straight alpha): a pale matte bloom, thickest near the outline and creeping in from it in patches, a crust
// of white crystals along the outline with needles standing a few px out past it, and a fine hoar on the face (soft
// dots, sparse in the middle). `edge`: the outline blurred to a few px (x: alpha 0-1) and its outward direction (yz)
// with the blur's slope (w, alpha a sheet unit); `wide`: the same blurred to about a twentieth of the sheet.
float4 PaperFrost(float4 c, float2 p, float px, float4 edge, float wide)
{
    float f = _PaperFrost.x;
    if (f <= 0.001) return c;
    float seed = _PaperFrost.y;
    float3 ice = float3(0.93, 0.96, 1);
    float lum = dot(c.rgb, float3(0.3, 0.59, 0.11));
    float3 pale = lerp(lum.xxx, c.rgb, 0.55) * 0.7 + float3(0.27, 0.3, 0.34);
    // How far the frost has crept in from the outline (patchy), and the crust right at it.
    float creep = saturate((1 - smoothstep(0.55, 0.98, wide)) * (0.75 + 0.5 * PaperNoise2(p * 14 + seed)));
    float crust = 1 - smoothstep(0.3, 0.9, edge.x + (PaperNoise2(p / max(px * 3, 0.006) + seed) - 0.5) * 0.35);
    c.rgb = lerp(c.rgb, pale, f * (0.28 + 0.4 * creep));
    // Hoar: irregular white flecks (thresholded fine noise, no grid), thick near the outline, none in the middle.
    float fine = PaperNoise2(p / max(px * 1.1, 0.0025) + seed * 7) * 0.65 + PaperNoise2(p / max(px * 0.6, 0.0013) + seed * 3) * 0.35;
    float fleck = smoothstep(0.72 - 0.16 * creep, 0.8 - 0.16 * creep, fine) * creep * creep;
    c.rgb = lerp(c.rgb, ice, f * saturate(fleck * 0.8 + crust * 0.9));
    // Needles past the outline: one every few px along it, each its own length, thinning to a point.
    float dOut = (0.5 - edge.x) / max(edge.w, 1e-3) / px;
    float2 n = edge.yz;
    float s = dot(p, float2(-n.y, n.x)) / (px * 3.2);
    float h = PaperHash2(float2(floor(s), seed));
    float len = (2 + 6 * h) * f;
    float lat = abs(frac(s) - 0.5) * 3.2;
    float t = saturate(dOut / max(len, 1e-3));
    float needle = dOut > -1 && dOut < len ? saturate(1 - lat / (0.7 * (1 - t) + 0.2)) * step(0.3, h) : 0;
    c.rgb = lerp(c.rgb, ice, needle * (c.a < 0.5 ? 1 : 0.6));
    c.a = max(c.a, needle * 0.95);
    return c;
}

// Rips where energy struck (straight alpha): a gap that wanders in small rounded jags along the paper's fibres, open
// widest near its middle and closed at both ends, with an uneven white fibre band along both torn edges; on one side
// the lifted flap shows the paper's pale back, edged by its shadow. The stage shows through the gap.
float PaperLum2(float3 c) { return saturate(dot(c, float3(0.3, 0.59, 0.11)) * 2); }

float4 PaperRipped(float4 c, float2 p, float px)
{
    int n = (int)_PaperMore.x;
    for (int i = 0; i < PAPER_RIPS; i++)
    {
        if (i >= n) break;
        float4 r = _PaperRips[i];
        float4 ri = _PaperRipInfo[i];
        float open = ri.x;
        if (open <= 0) continue;
        float2 a = r.xy;
        float2 ab = r.zw - a;
        float len = max(length(ab), 1e-4);
        float2 dir = ab / len;
        float2 nrm = float2(-dir.y, dir.x);
        float u = dot(p - a, dir) / len;
        if (u < -0.06 || u > 1.06) continue;
        float uu = saturate(u);
        float seed = ri.y;
        float off = (PaperNoise(uu * 5 + seed) - 0.5) * 0.03 + (PaperNoise(uu * 21 + seed * 2) - 0.5) * 0.01;
        float v = dot(p - a, nrm) - off;
        float halfW = ri.w * open * pow(sin(3.14159 * uu), 0.8);
        float side = v * ri.z > 0 ? 1 : 0;
        float fringe = (PaperNoise(uu * 90 + seed * 5 + side * 13) - 0.5) * max(px * 1.6, 0.004) * open;
        float gapD = abs(v) - halfW - fringe;
        float ends = saturate((0.5 - abs(u - 0.5)) * len / max(px * 2, 1e-4) + 0.5);
        float gap = saturate(-gapD / px + 0.5) * step(1e-5, halfW) * ends;
        float bandW = max(px * 1.5, 0.0045) * (0.55 + 0.9 * PaperNoise(uu * 37 + seed * 3 + side * 11)) * saturate(open * 3);
        float band = saturate((bandW - gapD) / px + 0.5) * (1 - gap) * ends;
        // Small curls lift along the edges, each its own width, on either side (some none): the paper's pale back,
        // brighter at its lip, with a shadow beyond the fold where it bends up from the sheet.
        float segL = 0.07;
        float sk = uu * len / segL + seed * 3;
        float si = floor(sk);
        float hs = PaperHash(si * 1.7 + side * 5.3 + seed);
        float curl = sin(3.14159 * frac(sk));
        float flapW = (halfW * (0.25 + 0.75 * hs) + px * 2) * step(0.35, hs) * pow(curl, 0.6);
        float flapD = gapD - bandW;
        float lift = saturate(open * 2 - 0.4) * ends;
        float flap = lift * saturate((flapW - flapD) / px + 0.5) * saturate(flapD / px + 0.5) * step(px, flapW);
        float shade = lift * saturate(1 - abs(flapD - flapW - px) / (px * 1.5)) * step(px, flapW);
        float across = saturate(flapD / max(flapW, 1e-5));
        float3 back = float3(0.93, 0.89, 0.8) * (0.78 + 0.22 * (1 - across)) * lerp(0.85, 1, PaperLum2(c.rgb));
        c.rgb = lerp(c.rgb, back, flap);
        c.rgb *= 1 - 0.35 * shade;
        c.rgb = lerp(c.rgb, float3(0.97, 0.95, 0.9), band * 0.92);
        c.a *= 1 - gap;
    }
    return c;
}

// Snapped off while frozen (straight alpha): everything past a brittle break (straight runs between sharp kinks, not
// a torn fringe) is gone, with a clean white edge where it broke. `_PaperMore.z` = i + 1: only piece i is kept (the
// piece's own picture as it falls), with the white edge on its side of the break.
float4 PaperSnapped(float4 c, float2 p, float px)
{
    int n = (int)_PaperMore.y;
    int keep = (int)_PaperMore.z;
    for (int i = 0; i < PAPER_SNAPS; i++)
    {
        if (i >= n) break;
        float4 s = _PaperSnaps[i];
        float seed = _PaperSnapInfo[i].y;
        float2 nrm = s.zw;
        float k = dot(p - s.xy, float2(-nrm.y, nrm.x)) / 0.055 + seed;
        float ki = floor(k);
        float off = lerp(PaperHash(ki + seed) - 0.5, PaperHash(ki + 1 + seed) - 0.5, frac(k)) * 0.04;
        // Small jags on it: short straight runs between kinks a few px apart, of uneven depth.
        float k2 = k * 3.4 + seed * 1.3;
        float kj = floor(k2);
        off += lerp(PaperHash(kj * 1.7 + seed) - 0.5, PaperHash((kj + 1) * 1.7 + seed) - 0.5, frac(k2)) * 0.016;
        float d = dot(p - s.xy, nrm) - off;
        float piece = saturate(d / px + 0.5);
        float bw = max(px * 1.4, 0.004);
        if (keep == i + 1)
        {
            c.rgb = lerp(c.rgb, float3(0.97, 0.98, 1), saturate((bw - d) / px + 0.5) * piece);
            c.a *= piece;
        }
        else if (_PaperSnapInfo[i].x > 0)
        {
            c.rgb = lerp(c.rgb, float3(0.97, 0.98, 1), saturate((d + bw) / px + 0.5) * (1 - piece));
            c.a *= 1 - piece;
        }
    }
    if (keep > 0 && keep > n) c.a = 0;
    return c;
}

// The outline blurred around uv (for the frost): alpha, outward direction in sheet space, slope. `lod`: the mip a few
// screen px wide; `step2`: one blur width in sheet units; `uvPerSheet`: uv a sheet unit (x, y).
#define PAPER_EDGE(tex, uv, lod, step2, uvPerSheet, outEdge) \
{ \
    float a0_ = tex2Dlod(tex, float4(uv, 0, lod)).a; \
    float ax_ = tex2Dlod(tex, float4(uv + float2(step2 * uvPerSheet.x, 0), 0, lod)).a; \
    float ay_ = tex2Dlod(tex, float4(uv + float2(0, step2 * uvPerSheet.y), 0, lod)).a; \
    float2 g_ = float2(ax_ - a0_, ay_ - a0_) / step2; \
    float gl_ = length(g_); \
    outEdge = float4(a0_, gl_ > 1e-4 ? -g_ / gl_ : float2(0, 1), gl_); \
}

// Tears and burn holes on a straight-alpha colour. A tear is a jagged gap from the edge, widest there and closing to its
// tip, with a pale torn fibre rim; a burn hole has a ragged edge, glowing orange while hot, in a ring of scorch.
float4 PaperDamaged(float4 c, float2 p, float px)
{
    px = max(px, 1e-5);
    int tears = (int)_PaperInfo.z;
    for (int i = 0; i < PAPER_TEARS; i++)
    {
        if (i >= tears) break;
        float2 a = _PaperTears[i].xy;
        float2 ab = _PaperTears[i].zw - a;
        float len = max(length(ab), 1e-4);
        float2 dir = ab / len;
        float2 nrm = float2(-dir.y, dir.x);
        float u = dot(p - a, dir) / len;
        if (u < -0.1 || u > 1) continue;
        float uu = saturate(u);
        // Paper rips along its fibres, not straight.
        float seed = i * 17.3 + a.x * 31.7 + a.y * 7.1;
        float off = (PaperNoise(uu * 7 + seed) - 0.5) * 0.05 + (PaperNoise(uu * 29 + seed) - 0.5) * 0.014;
        float v = abs(dot(p - a, nrm) - off);
        float width = 0.045 * pow(1 - uu, 1.2);
        float gap = saturate((width - v) / px + 0.5);
        float rim = saturate((width + 0.009 - v) / px + 0.5) * (1 - gap);
        c.rgb = lerp(c.rgb, float3(0.95, 0.92, 0.84), rim * 0.85);
        c.a *= 1 - gap;
    }
    int holes = (int)_PaperInfo.w;
    for (int j = 0; j < PAPER_HOLES; j++)
    {
        if (j >= holes) break;
        float4 h = _PaperHoles[j];
        if (h.z <= 0) continue;
        float2 d = p - h.xy;
        float r = length(d);
        float ang = atan2(d.y, d.x);
        float seed = j * 2.3 + h.x * 5.1;
        // Ragged all round (whole-number multiples, so the edge closes up).
        float edge = h.z * (1 + 0.16 * sin(ang * 3 + seed) + 0.09 * sin(ang * 7 + seed * 1.7) + 0.05 * sin(ang * 13 + seed * 0.3));
        float hole = saturate((edge - r) / px + 0.5);
        float scorch = saturate((edge + 0.045 - r) / 0.045) * (1 - hole);
        c.rgb = lerp(c.rgb, float3(0.13, 0.08, 0.05), 0.85 * scorch * scorch);
        float glow = saturate((edge + 0.012 - r) / 0.012) * (1 - hole) * h.w;
        c.rgb = saturate(c.rgb + float3(1.0, 0.45, 0.1) * glow);
        c.a *= 1 - hole;
    }
    return c;
}
