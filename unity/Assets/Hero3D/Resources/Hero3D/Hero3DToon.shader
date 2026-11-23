// The 3D hero's flat cartoon look (Hero3DView.cs): flat colour (material colour x vertex colour, a face texture laid
// over it through its alpha where the mesh has face UVs), one hard shade step from a fixed light direction (a property,
// no scene light: the 2D renderer has none), and an outline from an inverted hull a set number of screen pixels wide.
// Pass 0 "Toon" (LightMode Universal2D) and pass 1 "Outline" (no LightMode, so SRPDefaultUnlit): URP's 2D renderer
// draws both when the mesh is in the scene, and Hero3DView draws them by index into its RenderTexture.
// `_Hero3DPixel` (global, not a material property): clip-space size of one screen pixel (xy), set by Hero3DView.
// Optional (off by default, so the stand-in looks as before): the light in view space (`_LightSpace` 1), vertex colour
// off (`_UseVertexColor` 0), a vertex-colour channel that forces shade (shadow mask), one that scales the outline width.
// Lives in Resources next to its material so player builds include it.
Shader "Drakepg/Hero3DToon"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _MainTex ("Face (alpha laid over the colour where UVs are set)", 2D) = "clear" {}
        _ShadeColor ("Shade (multiplies the colour on the dark side)", Color) = (0.68,0.64,0.82,1)
        _LightDir ("Light direction (world, toward the light)", Vector) = (-0.45,0.75,-0.5,0)
        _ShadeStep ("Where the shade starts (N.L)", Range(-1,1)) = 0.05
        _OutlineColor ("Outline", Color) = (0.13,0.1,0.14,1)
        _OutlinePx ("Outline width (screen px)", Float) = 1.75
        _FlashColor ("Flash colour", Color) = (1,1,1,1)
        _Flash ("Flash amount", Range(0,1)) = 0
        _HasFace ("Lay the face texture over the colour (1) or not (0)", Float) = 1
        _LightSpace ("Light space (0 world, 1 view: x right, y up, z toward the viewer)", Float) = 0
        _UseVertexColor ("Multiply by the vertex colour (1) or not (0)", Float) = 1
        _ShadowMaskChannel ("Shadow mask: vertex colour channel forcing shade (-1 off, 0 r .. 3 a)", Float) = -1
        _ShadowMaskThreshold ("Shadow mask: shade where the channel is above this", Float) = 0.5
        _OutlineVertexChannel ("Outline width x this vertex colour channel (-1 off, 0 r .. 3 a)", Float) = -1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" "IgnoreProjector"="True" }

        CGINCLUDE
        #include "UnityCG.cginc"
        fixed4 _Color, _ShadeColor, _OutlineColor, _FlashColor;
        float4 _LightDir;
        float _ShadeStep, _OutlinePx, _Flash, _HasFace, _LightSpace, _UseVertexColor, _ShadowMaskChannel, _ShadowMaskThreshold, _OutlineVertexChannel;
        // One channel of a colour by index (0 r, 1 g, 2 b, 3 a).
        float Channel(float4 c, float i) { return i < 0.5 ? c.r : i < 1.5 ? c.g : i < 2.5 ? c.b : c.a; }
        sampler2D _MainTex;
        float4 _Hero3DPixel;
        ENDCG

        Pass
        {
            Name "Toon"
            Tags { "LightMode"="Universal2D" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; fixed4 color : COLOR; float2 uv : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                // View space: the light is fixed to the screen (like Blender's camera-space light).
                if (_LightSpace > 0.5) o.n = mul((float3x3)UNITY_MATRIX_V, o.n);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = _Color.rgb * lerp(fixed3(1,1,1), i.color.rgb, _UseVertexColor);
                // Face UVs are inside 0..1; the rest of the body has none (negative).
                if (_HasFace > 0.5 && i.uv.x >= 0)
                {
                    fixed4 face = tex2D(_MainTex, i.uv);
                    c = lerp(c, face.rgb, face.a);
                }
                float ndl = dot(normalize(i.n), normalize(_LightDir.xyz));
                // One hard step; the width of a pixel or so (fwidth) so the step's edge is not stair-stepped.
                float w = max(fwidth(ndl), 1e-4);
                float lit = saturate((ndl - _ShadeStep) / w + 0.5);
                if (_ShadowMaskChannel > -0.5 && Channel(i.color, _ShadowMaskChannel) > _ShadowMaskThreshold) lit = 0;
                c *= lerp(_ShadeColor.rgb, fixed3(1,1,1), lit);
                c = lerp(c, _FlashColor.rgb, _Flash);
                return fixed4(c, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Push the back faces out along the normal as seen on screen, an even number of pixels everywhere.
                float3 wn = UnityObjectToWorldNormal(v.normal);
                float2 sn = mul((float3x3)UNITY_MATRIX_VP, wn).xy;
                float len = length(sn);
                if (len > 1e-5)
                {
                    float2 pixel = _Hero3DPixel.x > 0 ? _Hero3DPixel.xy : 2.0 / _ScreenParams.xy;
                    float width = _OutlinePx * (_OutlineVertexChannel > -0.5 ? Channel(v.color, _OutlineVertexChannel) : 1);
                    o.pos.xy += sn / len * width * pixel * o.pos.w;
                }
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(lerp(_OutlineColor.rgb, _FlashColor.rgb, _Flash), 1);
            }
            ENDCG
        }
    }
}
