Shader "Phanto/PhantoLight"
{
    Properties
    {

        _SourcePos("Source", Vector) = (0.0, 0.0, 0.0, 0.0)
        _Radius("1/Radius", Float) = 1.0
        _Strength("Strength", Float) = 1.0
        _Falloff("Falloff", Float) = 0.1
        _Rot("Rot", Float) = 0.0
        _Highlight("Highlight", Color) = (1.0, 0.0, 0.0, 1.0)
        _Midlight("Midlight", Color) = (0.0, 1.0, 0.0, 1.0)
        _Lowlight("Lowlight", Color) = (0.0, 0.0, 1.0, 1.0)

        [MainTexture][NoScaleOffset] _FXtex("FX Texture", CUBE) = "white" {}

    }
    SubShader
    {
        Tags { "Queue" = "Geometry-1"  "RenderType"="Transparent" }
        ZWrite on
        Blend One OneMinusSrcAlpha
        Cull Back
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile SPOT DONUT
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

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
                float4 wPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }


            float spot(float3 pos, float radius, float strength, float falloff)
            {
                float3 d = abs(pos) * radius;
                float a = dot(d,d);
                return strength * pow(saturate(1-a), falloff);
            }

            float parabola( float x, float k )
            {
                return pow( 4.0*x*(1.0-x), k );
            }

            samplerCUBE _FXtex;
            float _Radius, _Strength, _Falloff, _Rot;
            float4 _SourcePos;
            float4x4 _TextureRotation;
            float _Blend;
            float4 _Lowlight, _Midlight, _Highlight;

            fixed4 frag (v2f i) : SV_Target
            {
                // NOTE: If you're debugging this shader set _SourcePos.w to 1.0f to see the effect.
                float s = _Strength * _SourcePos.w;

                float3 v = i.wPos - _SourcePos;
                float d = spot(v, _Radius, s, _Falloff);

                float f1 = texCUBE(_FXtex, normalize(v)).r;
                v = mul(_TextureRotation, v);
                float f2 = texCUBE(_FXtex, normalize(v)).r;

                float f = f1 * _Blend + f2 * (1-_Blend);


                float w = d * f;
                float4 col = _Highlight * w;

                return float4(col.r, col.g, col.b,w);
            }
            ENDCG
        }
    }

}
