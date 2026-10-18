// A hero's fist drawn again over the weapon it holds (Figure.Fist): the body sprite's own pixels inside a soft circle,
// so the grip disappears into the fingers along the fist's round outline (a rectangle left a straight cut where the
// weapon crossed the wrist). `_Circle` is the circle in the sprite's texture UVs (center xy, radii zw), set per
// renderer. Unlit and premultiplied like FxSprite; lives in Resources so builds include it.
Shader "Drakepg/FistSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _Circle ("Circle (uv center xy, uv radii zw)", Vector) = (0.5,0.5,0.5,0.5)
        _Feather ("Soft edge (fraction of the radius)", Float) = 0.09
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
            #pragma fragment FistFrag
            #pragma target 2.0
            #include "UnitySprites.cginc"

            float4 _Circle;
            float _Feather;

            fixed4 FistFrag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                float d = length((IN.texcoord - _Circle.xy) / _Circle.zw);
                fixed a = tex.a * IN.color.a * saturate((1 - d) / _Feather);
                return fixed4(tex.rgb * IN.color.rgb * a, a);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
