// Sprite shader for the effects in Fx (src/ui/fx.ts): unlit, no LightMode tag (URP's 2D renderer draws it as
// SRPDefaultUnlit), premultiplied output so one shader does normal alpha (Blend One OneMinusSrcAlpha) and additive glows
// (Blend One One). `_Fill` 1 paints the sprite one solid color (the renderer color) through its alpha: hit flashes and
// ghost silhouettes. Lives in Resources so builds always include it; Fx loads it with Resources.Load("Fx/FxSprite").
// A figure's pictures bend on its soft bones (Bend.cginc, set per renderer by World/Bend.cs); others are left as they are.
Shader "Drakepg/FxSprite"
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
        // Soft-bone bend (Bend.cginc), set per renderer by World/Bend.cs. Declared so a renderer without a bend draws with
        // these zeros: undeclared, the last bent figure's values leaked into the next sprites drawn and warped them.
        [PerRendererData] _BendXf ("Bend object to figure", Vector) = (0,0,0,0)
        [PerRendererData] _BendJ ("Bend joints", Vector) = (0,0,0,0)
        [PerRendererData] _BendA0 ("Bend legs", Vector) = (1,0,0,1)
        [PerRendererData] _BendA1 ("Bend body", Vector) = (1,0,0,1)
        [PerRendererData] _BendA2 ("Bend head", Vector) = (1,0,0,1)
        [PerRendererData] _BendT01 ("Bend t01", Vector) = (0,0,0,0)
        [PerRendererData] _BendT2 ("Bend t2", Vector) = (0,0,0,0)
        [PerRendererData] _BendWave ("Bend wave", Vector) = (0,0,0,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
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
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
        CGPROGRAM
            #pragma vertex BendSpriteVert
            #pragma fragment FxFrag
            #pragma target 2.0
            #include "UnitySprites.cginc"
            #include "Bend.cginc"

            v2f BendSpriteVert(appdata_t IN)
            {
                IN.vertex = BendVertex(IN.vertex);
                return SpriteVert(IN);
            }

            float _Fill;

            fixed4 FxFrag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                fixed a = tex.a * IN.color.a;
                fixed3 rgb = lerp(tex.rgb * IN.color.rgb, IN.color.rgb, _Fill);
                return fixed4(rgb * a, a);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
