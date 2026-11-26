// A paper figure folding up as it dies (World/Paper.cs). The picture so far lies on a canvas with room around it
// (`_MainTex`, straight alpha, `_Canvas` px). One fold at a time: the side of `_FoldLine` its normal points to turns
// over the line, squashed toward it by `_Fold.x` (1 flat, 0 edge-on, -1 folded over). A valley fold (`_Fold.y` 1)
// comes toward the viewer and lies on top showing the paper's back (`_Back`, a little of the picture showing through:
// `_Back.a`) with a soft shadow under it; a mountain fold (0) goes behind. Passes: 0 draws it on the figure's quad
// (premultiplied, tinted by `_Color.rgb`, faded by `_Color.a`); 1 bakes a finished fold into the next canvas (Graphics.Blit); 2 lays the
// living picture with its creases (Paper.cginc) onto the first canvas, from the art's own texture.
Shader "Drakepg/PaperFold"
{
    Properties
    {
        _MainTex ("Canvas", 2D) = "white" {}
        _Color ("Tint and fade", Color) = (1,1,1,1)
        _Canvas ("Canvas px", Vector) = (1,1,0,0)
        _FoldLine ("Fold line", Vector) = (0,0,0,1)
        _Fold ("Fold", Vector) = (1,1,0,1)
        _Back ("Back", Color) = (0.94,0.9,0.82,0.12)
        _Shadow ("Shadow", Vector) = (0,0,0,0)
        _Pad ("Canvas margin and picture size", Vector) = (0,0,1,1)
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Paper.cginc"

    sampler2D _MainTex;
    float4 _Color, _Canvas, _FoldLine, _Fold, _Back, _Shadow, _Pad;

    float4 Over(float4 top, float4 under)
    {
        float a = top.a + under.a * (1 - top.a);
        float3 rgb = (top.rgb * top.a + under.rgb * under.a * (1 - top.a)) / max(a, 1e-4);
        return float4(rgb, a);
    }

    float4 Canvas(float2 px)
    {
        return tex2Dlod(_MainTex, float4(px / _Canvas.xy, 0, 0));
    }

    // Where the folding side's paper that now lies at canvas px `p` came from (squash s); false off the flap.
    bool FlapSource(float2 p, float s, out float2 src, out float d)
    {
        d = dot(p - _FoldLine.xy, _FoldLine.zw);
        src = p;
        if (abs(s) <= 0.02) return false;
        float ds = d / s;
        if (ds <= 0) return false;
        src = p + _FoldLine.zw * (ds - d);
        return true;
    }

    float4 FoldAt(float2 uv)
    {
        float2 p = uv * _Canvas.xy;
        float s = _Fold.x;
        bool valley = _Fold.y > 0.5;
        float2 src;
        float d;
        bool onFlap = FlapSource(p, s, src, d);
        // The side that stays, cut softly along the line.
        float4 still = Canvas(p);
        still.a *= saturate(0.5 - d);
        float4 flap = 0;
        if (onFlap)
        {
            float4 f = Canvas(src);
            // The back turns a little away from the light toward its far edge (paper doesn't lie quite flat).
            float far = saturate(dot(src - _FoldLine.xy, _FoldLine.zw) / max(_Shadow.w, 1));
            float3 c = s < 0 ? lerp(_Back.rgb, f.rgb, _Back.a) * (1 - 0.16 * far) : f.rgb;
            float k = 1 - _Fold.z - (abs(d) < _Fold.w ? 0.25 : 0);
            flap = float4(c * k, f.a * saturate(dot(src - _FoldLine.xy, _FoldLine.zw) + 0.5));
        }
        if (valley && s < 0)
        {
            // The lifted flap's shadow on what it lies over.
            float2 q;
            float dq;
            if (FlapSource(p - _Shadow.xy, s, q, dq)) still.rgb *= 1 - _Shadow.z * Canvas(q).a;
            return Over(flap, still);
        }
        return s < 0 ? Over(still, flap) : Over(flap, still);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Lighting Off

        // 0: shown on the figure's quad.
        Pass
        {
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
                float4 c = FoldAt(i.uv);
                float a = c.a * _Color.a;
                return fixed4(c.rgb * _Color.rgb * a, a);
            }
        ENDCG
        }

        // 1: a finished fold baked into the next canvas.
        Pass
        {
            ZTest Always
            Blend Off
        CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            fixed4 frag(v2f_img i) : SV_Target { return FoldAt(i.uv); }
        ENDCG
        }

        // 2: the living picture with its creases onto the first canvas (`_MainTex` = the art's texture here).
        Pass
        {
            ZTest Always
            Blend Off
        CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 local = (i.uv * _Canvas.xy - _Pad.xy) / _Pad.zw;
                float2 uv = _PaperRect.xy + saturate(local) * _PaperRect.zw;
                float4 c = tex2D(_MainTex, uv);
                float inside = local.x >= 0 && local.y >= 0 && local.x <= 1 && local.y <= 1 ? 1 : 0;
                float2 p = float2(local.x * _PaperInfo.x, local.y);
                float px = 1 / _Pad.w;
                c.rgb = PaperCreased(c.rgb, p, px);
                // Water, ice and energy as the fight left them (step 1d), so the crumple keeps them.
                c = PaperWet(c, p, px);
                if (_PaperFrost.x > 0)
                {
                    float w2 = px * 3;
                    float4 edge;
                    float2 ups = float2(_PaperRect.z / _PaperInfo.x, _PaperRect.w);
                    PAPER_EDGE(_MainTex, uv, log2(max(1, w2 * _PaperMore.w)), w2, ups, edge)
                    float ww = max(0.035, px * 5);
                    float wide = tex2Dlod(_MainTex, float4(uv, 0, log2(max(1, ww * _PaperMore.w)))).a;
                    c = PaperFrost(c, p, px, edge, wide);
                }
                c = PaperSnapped(PaperRipped(PaperDamaged(c, p, px), p, px), p, px);
                return c * inside;
            }
        ENDCG
        }
    }
}
