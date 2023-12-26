Shader "MR/OcclusionScanner"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DepthScale("Depth Scale", Range(-2.0, 2.0)) = 1
        _RightEye("Right Eye", int) = 0 // zero or one only
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Packages/com.meta.xr.depthapi/Runtime/BiRP/EnvironmentOcclusionBiRP.cginc"

            // DepthAPI Environment Occlusion
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            // source: https://gamedev.stackexchange.com/a/59808
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _DepthScale;
            float _RightEye;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // This is an effect displayed on a 2D object.
                // Toggle whether we want to see left or right depth camera output
                unity_StereoEyeIndex.x = _RightEye;

                #if defined(HARD_OCCLUSION) || defined(SOFT_OCCLUSION)
                float envDepth = SampleEnvironmentDepth(i.uv) * _DepthScale;
                #else
                float envDepth = 0.5 * _DepthScale;
                #endif

                fixed4 col = fixed4(hsv2rgb(float3(envDepth, 1, 1)), 1);
                return col;
            }
            ENDCG
        }
    }
}
