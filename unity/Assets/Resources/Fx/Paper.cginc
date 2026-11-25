// Paper damage on a figure's picture (World/Paper.cs): the creases its blows left, the tears it gets when badly hurt
// and the holes fire burns in it, drawn on the picture by the shader (Fx/PaperSprite.shader while it lives,
// Fx/PaperFold.shader's sheet pass when it dies). "Sheet space": the picture with its height 1 and y up (x runs 0 to its
// width / height). Set per renderer through a MaterialPropertyBlock.

#define PAPER_MAX 16
#define PAPER_TEARS 2
#define PAPER_HOLES 4

float4 _PaperRect;                  // the picture's rect in its texture's UV: min xy, size zw
float4 _PaperInfo;                  // x: width / height, y: how many creases, z: tears, w: holes
float4 _PaperCreases[PAPER_MAX];    // per crease: a point on it (xy) and its unit normal (zw), sheet space
float4 _PaperTears[PAPER_TEARS];    // per tear: where it starts at the edge (xy) and its tip (zw)
float4 _PaperHoles[PAPER_HOLES];    // per burn hole: its middle (xy), radius (z), how hot its edge still glows 0-1 (w)

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
    }
    return rgb;
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
