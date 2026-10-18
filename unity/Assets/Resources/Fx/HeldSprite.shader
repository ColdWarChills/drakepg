// A held weapon and the fist around it (Figure.cs). Weapons lean away from the viewer, so each is drawn twice: whole
// behind the body (its blade goes behind the head), and its grip end again in front: `_Cut` keeps texels below a line
// across the weapon (x: the line in texture V, y: a soft edge in V; y <= 0 keeps everything). Then the fist is drawn
// again over the grip through a soft circle: `_Circle` is the circle in texture UVs (center xy, radii zw; zw <= 0 keeps
// everything). Both are set per renderer. Unlit and premultiplied like FxSprite; lives in Resources so builds include it.
Shader "Drakepg/HeldSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _Circle ("Circle (uv center xy, uv radii zw)", Vector) = (0.5,0.5,0,0)
        [PerRendererData] _Cut ("Cut (v line, v soft edge)", Vector) = (0,0,0,0)
        _Feather ("Circle's soft edge (fraction of the radius)", Float) = 0.09
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
            #pragma vertex SpriteVert
            #pragma fragment HeldFrag
            #pragma target 2.0
            #include "UnitySprites.cginc"

            float4 _Circle;
            float4 _Cut;
            float _Feather;

            fixed4 HeldFrag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                fixed a = tex.a * IN.color.a;
                if (_Circle.z > 0)
                {
                    float d = length((IN.texcoord - _Circle.xy) / _Circle.zw);
                    a *= saturate((1 - d) / _Feather);
                }
                if (_Cut.y > 0) a *= saturate((_Cut.x - IN.texcoord.y) / _Cut.y);
                return fixed4(tex.rgb * IN.color.rgb * a, a);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
