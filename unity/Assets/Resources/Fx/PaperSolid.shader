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
        _Shade ("Shadow strength (x), blur mip (y), front faces flip (z)", Vector) = (0.38,2.5,-1,0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _Color, _Back, _Ink, _Light, _Tone, _Shade;

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
                float4 pic = tex2D(_MainTex, i.uv);
                bool front = face * _Shade.z > 0;
                // The paper's normal here (smooth across a panel, split at creases), turned to the side we see.
                float3 n = normalize(i.normal);
                if (n.z < 0) n = -n;
                float lit = dot(n, normalize(_Light.xyz));
                float rest = normalize(_Light.xyz).z;
                // Facing us as it lay: tone 1 (the picture as drawn); turned to the light: lighter; away: darker. A few
                // steps, the flat cut-out look.
                // Stepped around the flat tone (0 stays 0 however the normal wobbles).
                float t = clamp((lit - rest) * 1.6, -1, 1);
                t = _Tone.z > 0.5 ? round(t * _Tone.z) / _Tone.z : t;
                float tone = t < 0 ? lerp(1, _Tone.x, -t) : lerp(1, _Tone.y, t);
                float3 rgb;
                if (front)
                    rgb = pic.rgb * tone;
                else
                {
                    // The paper's back: its own colour, the picture faintly through it, and its cut edge drawn.
                    float edge = 1 - smoothstep(0.35, 0.95, tex2Dlod(_MainTex, float4(i.uv, 0, 1.2)).a);
                    rgb = lerp(_Back.rgb, pic.rgb, _Back.a) * tone * (1 - 0.45 * edge);
                }
                // Crisp lines at the folds, a constant width on screen.
                float3 w = fwidth(i.bary);
                float3 near = 1 - smoothstep(_Tone.w * 0.5, _Tone.w * 0.5 + 1, i.bary / max(w, 1e-5));
                float ink = saturate(max(max(near.x * i.lines.x, near.y * i.lines.y), near.z * i.lines.z));
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
                float a = tex2Dlod(_MainTex, float4(i.uv, 0, _Shade.y)).a;
                clip(a - 0.05);
                return float4((1 - _Shade.x * a).xxx, 1);
            }
        ENDCG
        }

    }
}
