Shader "Phanto/CrystalPortal Occlusion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _speed ("rotational speed", float) = 1
        _distort("distorsion", float) = 1
        _color ("color",  Color) = (1,1,1,1)
        _EnvironmentDepthBias("Depth Bias", float) = 0.06
    }
    SubShader
    {
 		Tags { "Queue"="Transparent"  "RenderType"="Transparent"}
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Cull back
        LOD 100

        Pass
        {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "UnityCG.cginc"
            #include "Packages/com.meta.xr.depthapi/Runtime/BiRP/EnvironmentOcclusionBiRP.cginc"

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

                META_DEPTH_VERTEX_OUTPUT(1)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO // required for stereo
            };

            static const float PI = 3.14159265f;
            static const float uBound = 0.47f;
            static const float vBound = 0.47f;
            static const float roundness = 0.05f;

            sampler2D _MainTex;
            float4 _MainTex_ST, _color;
            float _speed, _distort, _EnvironmentDepthBias;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(o, v.vertex);
                return o;
            }

            // box-shaped signed distance field
            float sdRoundBox( float2 p, float2 b, float4 r )
            {
                r.xy = (p.x>0.0)?r.xy : r.zw;
                r.x  = (p.y>0.0)?r.x  : r.y;
                float2 q = abs(p)-b+r.x;
                return min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // center the coordinate system
                float2 p = i.uv - 0.5f;

                // create a radial matte
                float rad = 2*(atan2(p.x, p.y) + PI) / (2*PI);
                rad = 1-saturate((rad + _Time.x*_speed) %1);

                // create a distortion field by indexing into the noise texture
                float4 noise = tex2D(_MainTex, float2(0.5,rad));
                noise *= _distort;

                // create a box-shaped signed distance field
                float2 si = float2(uBound,vBound);
                p += p*noise;
                float d = -sdRoundBox(p,si,roundness);
	            d = 1.0 - exp(-6.0*(d));

                // tail off the thickness as you move around the periphery
                float thickness = lerp(0.01, 0.1, rad);
                d = 1-smoothstep(0.0,thickness,abs(d));

                // composite the result
                float x = rad*d;
                float4 compC = float4(x,x,x,x) * _color;

                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(i, compC, _EnvironmentDepthBias);

                return compC;
            }
            ENDCG
        }
    }
}
