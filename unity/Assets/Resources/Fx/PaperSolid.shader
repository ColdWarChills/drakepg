// The paper fold on a solver (World/Paper.Solid.cs, docs/PLANS.md "Step 1b, the fold redone"). The dying picture is a
// mesh of paper that the solver bends along its creases; it is drawn into its own render texture (with depth, MSAA and
// a little perspective made on the CPU), which a quad in the figure's place shows.
// Passes: 1 the paper into the texture (the picture on the front, the paper's back behind, each face toned by its
// angle to the light, crisp lines at the folds); 2 the folds' shadows on the paper below them; 0 the texture shown in
// the scene (premultiplied, tinted by `_Color.rgb`, faded by `_Color.a`).
Shader "Drakepg/PaperSolid"
{
    Properties
    {
        _MainTex ("Canvas / folded paper", 2D) = "white" {}
        _Color ("Tint and fade", Color) = (1,1,1,1)
        _Back ("The paper's back", Color) = (0.94,0.9,0.82,0.1)
        _Ink ("Fold line colour", Color) = (0.16,0.1,0.08,0.75)
        _Light ("Light (canvas space, toward it)", Vector) = (-0.5,0.7,0.5,0)
        _Tone ("Tone: x dark, y light, z steps, w line px", Vector) = (0.55,1.12,3,1.1)
        _Print ("Print: x toward the paper, y less colour", Vector) = (0,0,0,0)
        _PrintTint ("A tint on the printed side only", Color) = (1,1,1,1)
        _Crinkle ("Small creases: x steepness, y size px, w lit side gain; z the white back along cut edges, px", Vector) = (0,0,0,0)
        _Shade ("Shadow strength (x), blur mip (y), front faces flip (z)", Vector) = (0.38,2.5,-1,0)
        _Outline ("A dark line round the outline in pass 0, texels (0 none)", Float) = 0
        _EdgeInk ("A line along every cut edge of the paper in pass 1, times the fold line's width (0 none)", Float) = 0
        _CloseTex ("The outline's closing: what it adds round the picture (premultiplied)", 2D) = "black" {}
        _Close ("How much of the closing shows (0-1)", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _CloseTex;
    float4 _Color, _Back, _Ink, _Light, _Tone, _Shade, _Print, _PrintTint, _Crinkle, _MainTex_TexelSize;

    // Small creases (`_Crinkle`): one short ridge or valley in each cell of `_Crinkle.y` canvas px of the flat sheet
    // (fixed to the paper), a little roof: a crease down its middle and along both sides, tapering to points at its
    // ends, so its two sides take the light differently. Returns the paper's slope (height per px, steepness
    // `_Crinkle.x` applied by the caller) at `px`, canvas px on the sheet.
    float3 CrinkleHash(float2 p)
    {
        float3 q = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
        q += dot(q, q.yxz + 33.33);
        return frac((q.xxy + q.yzz) * q.zyx);
    }
    float2 CrinkleSlope(float2 px)
    {
        float T = _Crinkle.y;
        float2 cell = floor(px / T);
        float2 g = 0;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                float2 c = cell + float2(i, j);
                float3 h = CrinkleHash(c * 1.37 + 0.5);
                float3 h2 = CrinkleHash(c * 2.11 + 3.1);
                float2 at = (c + h.xy) * T;
                float ang = h.z * 3.14159265;
                float2 n = float2(cos(ang), sin(ang)), t = float2(-n.y, n.x);
                float2 d = px - at;
                float across = dot(d, n), along = dot(d, t);
                float L = T * (0.55 + 0.45 * h2.x), D = T * (0.22 + 0.16 * h2.y);
                float inside = saturate((D - abs(across)) / 0.75) * saturate((L - abs(along)) / 0.75);
                float taper = 1 - abs(along) / L;
                float a = (h2.z < 0.5 ? -1 : 1) * (0.6 + 0.8 * frac(h2.z * 7.3));
                g += inside * a * (-clamp(across / 0.75, -1, 1) * taper * n - (D - abs(across)) * sign(along) / L * t);
            }
        return g;
    }
    float _Outline, _EdgeInk, _Close;

    // The canvas with the outline's closing under it (made as the fight starts, so the fight's burns and tears on the
    // canvas stay open): where the picture is clear, the closing's colour; its cover, whichever is more.
    float4 Closed(float4 p, float4 c)
    {
        c *= _Close;
        if (p.a < 0.5 && c.a > 1e-3) p.rgb = c.rgb / c.a;
        p.a = max(p.a, c.a);
        return p;
    }
    float4 Pic(float2 uv) { return Closed(tex2D(_MainTex, uv), tex2D(_CloseTex, uv)); }
    float4 PicLod(float2 uv, float lod) { return Closed(tex2Dlod(_MainTex, float4(uv, 0, lod)), tex2Dlod(_CloseTex, float4(uv, 0, lod))); }

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;     // smooth within a panel of paper, split at the creases
        float2 uv : TEXCOORD0;      // where on the flat canvas
        float3 world : TEXCOORD1;   // the solver's 3D point (canvas px), for the face's normal
        float3 bary : TEXCOORD2;    // barycentric corner
        float3 lines : TEXCOORD3;   // how strongly each edge is drawn as a fold (the edge opposite corner 0, 1, 2)
    };
    ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off

        // 0: the folded paper shown in the scene (the texture is premultiplied: MSAA resolves it so).
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                if (_Outline > 0)
                {
                    // A dark line just outside the paper's outline: the most cover among the texels around, minus its own.
                    float2 d = _MainTex_TexelSize.xy * _Outline;
                    float n = tex2D(_MainTex, i.uv + float2(d.x, 0)).a;
                    n = max(n, tex2D(_MainTex, i.uv - float2(d.x, 0)).a);
                    n = max(n, tex2D(_MainTex, i.uv + float2(0, d.y)).a);
                    n = max(n, tex2D(_MainTex, i.uv - float2(0, d.y)).a);
                    n = max(n, tex2D(_MainTex, i.uv + 0.7 * d).a);
                    n = max(n, tex2D(_MainTex, i.uv - 0.7 * d).a);
                    n = max(n, tex2D(_MainTex, i.uv + 0.7 * float2(d.x, -d.y)).a);
                    n = max(n, tex2D(_MainTex, i.uv - 0.7 * float2(d.x, -d.y)).a);
                    float o = saturate(n - c.a);
                    c.rgb += _Ink.rgb * o;
                    c.a += o;
                }
                return fixed4(c.rgb * _Color.rgb * _Color.a, c.a * _Color.a);
            }
        ENDCG
        }
        // 1: the paper into its render texture (MSAA; alpha to coverage gives soft edges with a depth buffer).
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            AlphaToMask On
            Stencil { Ref 0 Comp Always Pass Zero }
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 bary : TEXCOORD2;
                float3 lines : TEXCOORD3;
                float3 normal : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = v.normal;
                o.world = v.world;
                o.bary = v.bary;
                o.lines = v.lines;
                return o;
            }

            float4 frag(v2f i, float face : VFACE) : SV_Target
            {
                float4 pic = Pic(i.uv);
                bool front = face * _Shade.z > 0;
                // The paper's normal here (smooth across a panel, split at creases), turned to the side we see.
                float3 n = normalize(i.normal);
                if (n.z < 0) n = -n;
                float lit = dot(n, normalize(_Light.xyz));
                // Small creases: the face's normal tipped by the creases' slope, along the sheet's own directions here
                // (from how the flat sheet and the bent paper change across the pixel).
                float crinkle = 0;
                if (_Crinkle.x > 0)
                {
                    float2 q = i.uv * _MainTex_TexelSize.zw;
                    float2 qx = ddx(q), qy = ddy(q);
                    float3 px = ddx(i.world), py = ddy(i.world);
                    float det = qx.x * qy.y - qy.x * qx.y;
                    if (abs(det) > 1e-6)
                    {
                        float3 tu = (px * qy.y - py * qx.y) / det;
                        float3 tv = (py * qx.x - px * qy.x) / det;
                        float2 g = _Crinkle.x * CrinkleSlope(q);
                        float3 n2 = normalize(n - g.x * tu - g.y * tv);
                        crinkle = (dot(n2, normalize(_Light.xyz)) - lit) * 1.6;
                    }
                }
                float rest = normalize(_Light.xyz).z;
                // Facing us as it lay: tone 1 (the picture as drawn); turned to the light: lighter; away: darker. A few
                // steps, the flat cut-out look.
                // Stepped around the flat tone (0 stays 0 however the normal wobbles).
                float t = clamp((lit - rest) * 1.6, -1, 1);
                t = _Tone.z > 0.5 ? round(t * _Tone.z) / _Tone.z : t;
                float tone = t < 0 ? lerp(1, _Tone.x, -t) : lerp(1, _Tone.y, t);
                if (_Crinkle.x > 0) tone *= clamp(1 + crinkle * (crinkle < 0 ? 1 - _Tone.x : _Crinkle.w), 0.4, 1.4);
                float3 rgb;
                if (front)
                {
                    // Printed on matte paper (`_Print`): lifted toward the paper's colour, with less of its own.
                    float3 p = pic.rgb * _PrintTint.rgb;
                    p = lerp(p, dot(p, float3(0.3, 0.59, 0.11)).xxx, _Print.y);
                    p = lerp(p, _Back.rgb, _Print.x);
                    rgb = p * tone;
                    if (_Crinkle.z > 0)
                    {
                        // The paper's white back showing along its cut edge (turned up where the edge curls): a band
                        // about `_Crinkle.z` canvas px in from it, from the outline blurred to that size.
                        float blurred = PicLod(i.uv, log2(max(1.0, _Crinkle.z))).a;
                        rgb = lerp(rgb, _Back.rgb * tone, 0.9 * (1 - smoothstep(0.6, 0.97, blurred)));
                    }
                }
                else
                {
                    // The paper's back: its own colour, the picture faintly through it, and its cut edge drawn.
                    float edge = 1 - smoothstep(0.35, 0.95, PicLod(i.uv, 1.2).a);
                    rgb = lerp(_Back.rgb, pic.rgb, _Back.a) * tone * (1 - 0.45 * edge);
                }
                // Crisp lines at the folds, a constant width on screen.
                float3 w = fwidth(i.bary);
                float3 near = 1 - smoothstep(_Tone.w * 0.5, _Tone.w * 0.5 + 1, i.bary / max(w, 1e-5));
                float ink = saturate(max(max(near.x * i.lines.x, near.y * i.lines.y), near.z * i.lines.z));
                if (_EdgeInk > 0)
                {
                    // The paper's own cut edge (its alpha's half line), a line just inside it: where one layer's edge
                    // lies over another, the layers read apart.
                    float edgePx = (pic.a - 0.5) / max(fwidth(pic.a), 1e-4);
                    float lw = _Tone.w * 0.5 * _EdgeInk;
                    ink = max(ink, 1 - smoothstep(lw, lw + 1, edgePx));
                }
                rgb = lerp(rgb, _Ink.rgb, ink * _Ink.a);
                // Alpha to coverage: sharpen so the edge spans about a pixel.
                float a = pic.a;
                float aw = max(fwidth(a), 1e-4);
                a = saturate((a - 0.5) / aw + 0.5);
                clip(pic.a - 0.02);
                return float4(rgb, a);
            }
        ENDCG
        }

        // 2: each lifted face's shadow on the paper under it (the mesh again, moved along the light by how high each
        // point is, a little behind its caster: it darkens only paper lying below). Once per pixel (stencil).
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend DstColor Zero, Zero One
            Stencil { Ref 1 Comp NotEqual Pass Replace }
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float a = PicLod(i.uv, _Shade.y).a;
                clip(a - 0.05);
                return float4((1 - _Shade.x * a).xxx, 1);
            }
        ENDCG
        }

    }
}
