// The 3D hero's rim (Hero3DView.Rim): a thin pale edge outside the outline, made in 2D from the hero picture's alpha.
// Blit pass: wherever the picture is not fully solid, the most solid pixel within `_RimTexels` texels (24 directions,
// three rings) decides how much rim shows behind it. Globals: _RimColor, _RimTexels.
Shader "Hidden/Drakepg/Hero3DRim"
{
    Properties { _MainTex ("Picture", 2D) = "clear" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _RimColor;
            float _RimTexels;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv);
                if (src.a >= 0.999) return src;
                float cover = 0;
                const int N = 24;
                for (int k = 0; k < N; k++)
                {
                    float ang = k * 6.2831853 / N;
                    float2 d = float2(cos(ang), sin(ang)) * _MainTex_TexelSize.xy;
                    cover = max(cover, tex2D(_MainTex, i.uv + d * _RimTexels).a);
                    cover = max(cover, tex2D(_MainTex, i.uv + d * _RimTexels * 0.66).a);
                    cover = max(cover, tex2D(_MainTex, i.uv + d * _RimTexels * 0.33).a);
                }
                // The rim lies behind the picture: the picture over the rim colour.
                float a = max(src.a, cover);
                fixed3 rgb = a > 0 ? (src.rgb * src.a + _RimColor.rgb * (a - src.a)) / a : _RimColor.rgb;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
