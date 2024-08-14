// technique based on "Custom Shadow Mapping in Unity"
// https://shahriyarshahrabi.medium.com/custom-shadow-mapping-in-unity-c42a81e1bbf8

Shader "MR/AlphaReceiveShadowMap"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ShadowAlpha ("Shadow Alpha", float) = 0.5
        _ShadowColor ("Shadow Color", Color) = (0.5,0.5,0.5)
        _BaseColor ("Base Color", Color) = (0,0,0)
        _EnvironmentDepthBias("Depth Bias", float) = 0.06
    }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "Queue"="Geometry-1500"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "LightMode" = "ForwardBase"
        }
        LOD 200

        Pass
        {
            // Reserve the z buffer for invisible occluder (scene mesh)
            ZWrite On
            ColorMask 0
        }

        Lighting Off
        ColorMask RGBA
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #define SHADOWS_SCREEN
            #include "AutoLight.cginc"
            #include "UnityCG.cginc"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/BiRP/EnvironmentOcclusionBiRP.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                SHADOW_COORDS(5)
                float4 pos : SV_POSITION;
                META_DEPTH_VERTEX_OUTPUT(3)

                float4 screenPos : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ShadowAlpha;
            float4 _ShadowColor;
            float4 _BaseColor;
            float _EnvironmentDepthBias;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(o, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.screenPos = ComputeScreenPos(o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 col = _BaseColor;

                half e = 1 - unitySampleShadow(i._ShadowCoord);

                col.rgb = lerp(col.xyz, _ShadowColor, e);
                col.a = lerp(0, 1, e) * _ShadowAlpha;

                // only do occlusion test if fragment is visible
                if (col.a > 0.000001)
                {
                    float2 screenUV = i.screenPos.xy / i.screenPos.w;
                    float occlusionValue = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(i.posWorld, _EnvironmentDepthBias);
                    col.a *= occlusionValue;
                }

                return col;
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}
