// A paper figure's picture while it lives (World/Paper.cs): FxSprite (premultiplied output, `_Fill`, the soft-bone
// bend) with the creases of Paper.cginc drawn on it. Lives in Resources so builds include it.
Shader "Drakepg/PaperSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _Fill ("Fill With Color", Float) = 0
        [PerRendererData] _BendXf ("Bend object to figure", Vector) = (0,0,0,0)
        [PerRendererData] _BendJ ("Bend joints", Vector) = (0,0,0,0)
        [PerRendererData] _BendA0 ("Bend legs", Vector) = (1,0,0,1)
        [PerRendererData] _BendA1 ("Bend body", Vector) = (1,0,0,1)
        [PerRendererData] _BendA2 ("Bend head", Vector) = (1,0,0,1)
        [PerRendererData] _BendT01 ("Bend t01", Vector) = (0,0,0,0)
        [PerRendererData] _BendT2 ("Bend t2", Vector) = (0,0,0,0)
        [PerRendererData] _BendWave ("Bend wave", Vector) = (0,0,0,1)
        [PerRendererData] _PaperRect ("Paper rect", Vector) = (0,0,1,1)
        [PerRendererData] _PaperInfo ("Paper info", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex BendSpriteVert
            #pragma fragment PaperFrag
            #pragma target 3.0
            #include "UnitySprites.cginc"
            #include "Bend.cginc"
            #include "Paper.cginc"

            v2f BendSpriteVert(appdata_t IN)
            {
                IN.vertex = BendVertex(IN.vertex);
                return SpriteVert(IN);
            }

            float _Fill;

            fixed4 PaperFrag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                float2 p = PaperSheet(IN.texcoord);
                float px = fwidth(p.y);
                float4 c = PaperDamaged(float4(PaperCreased(tex.rgb, p, px), tex.a), p, px);
                fixed a = c.a * IN.color.a;
                float3 rgb = lerp(c.rgb * IN.color.rgb, IN.color.rgb, _Fill);
                return fixed4(rgb * a, a);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
