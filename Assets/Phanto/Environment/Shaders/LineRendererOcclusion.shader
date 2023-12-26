Shader "Phanto/LineRendererOcclusion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DepthBias("Depth Bias", float) = 0.06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 100

        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "UnityCG.cginc"
            #include "Packages/com.meta.xr.depthapi/Runtime/BiRP/EnvironmentOcclusionBiRP.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color: COLOR;

                META_DEPTH_VERTEX_OUTPUT(1)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO // required for stereo
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _DepthBias;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(o, v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(i, col, _DepthBias);

                return col;
            }
            ENDCG
        }
    }
}
