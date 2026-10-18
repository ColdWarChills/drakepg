// A held weapon and the hand around it (Figure.cs). Each weapon is drawn twice: whole behind the body (its blade goes
// behind the head), and its grip end again in front, cut by a line across it. A hand with a fingers sprite draws that
// on top; a hand without one draws its own fist again over the grip through a soft circle.
// `_LineA`/`_LineB`: keep texels on one side of a line: xy is the line's normal times the texture size (so a dot with
// the UV gives texels), z the offset, w a soft edge in texels (w <= 0: unused). `_Circle`: the circle in texture UVs
// (center xy, radii zw; zw <= 0: unused). All set per renderer. Unlit and premultiplied like FxSprite; lives in
// Resources so builds include it.
// A figure's pictures bend on its soft bones (Bend.cginc, set per renderer by World/Bend.cs); others are left as they are.
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
        [PerRendererData] _LineA ("Line A (normal xy, offset, soft)", Vector) = (0,0,0,0)
        [PerRendererData] _LineB ("Line B (normal xy, offset, soft)", Vector) = (0,0,0,0)
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
            #pragma vertex BendSpriteVert
            #pragma fragment HeldFrag
            #pragma target 2.0
            #include "UnitySprites.cginc"
            #include "Bend.cginc"

            v2f BendSpriteVert(appdata_t IN)
            {
                IN.vertex = BendVertex(IN.vertex);
                return SpriteVert(IN);
            }

            float4 _Circle;
            float4 _LineA;
            float4 _LineB;
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
                if (_LineA.w > 0) a *= saturate((dot(IN.texcoord, _LineA.xy) - _LineA.z) / _LineA.w + 0.5);
                if (_LineB.w > 0) a *= saturate((dot(IN.texcoord, _LineB.xy) - _LineB.z) / _LineB.w + 0.5);
                return fixed4(tex.rgb * IN.color.rgb * a, a);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
