Shader "MR/PassthroughMask"
{
    Properties
    {
        // This shader is just to reserve z buffer for invisible occluders (scene mesh)
    }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "Queue"="Geometry-1500"
        }
        LOD 100
        ColorMask 0

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return 1; // Doesn't matter because ColorMask is 0
            }
            ENDCG
        }
    }
}
