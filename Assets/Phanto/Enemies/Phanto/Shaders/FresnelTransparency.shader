Shader "Phanto/FresnelTransparency"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Alpha ("Base Alpha", Range(0,1)) = 1.0
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _EmissiveTex("Emission Map", 2D) = "black" {}
        _RimColor("Rimlight Color", Color) = (1,1,1,1)
        _RimTex("Rimlight Texture", 2D) = "white" {}
        _RimIntensity("Rimlight Intensity", Range(0.0, 2.0)) = 0.0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        LOD 200

        Pass
        {
            ZWrite On
            ColorMask 0
        }

        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows alpha

        #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

        #include "UnityCG.cginc"
        #include "Packages/com.meta.xr.depthapi/Runtime/BiRP/EnvironmentOcclusionBiRP.cginc"
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_EmissiveTex;
            float2 uv_RimTex;
            float3 viewDir;

            float3 worldPos;
        };

        fixed4 _Color;
        half _Alpha;
        sampler2D _MainTex;
        half _Glossiness;
        sampler2D _EmissiveTex;
        fixed4 _RimColor;
        sampler2D _RimTex;
        half _RimIntensity;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Alpha = _Alpha * c.a;
            o.Smoothness = _Glossiness;

            o.Normal = float3(0, 0, 1);


            half rimTerm = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = tex2D(_EmissiveTex, IN.uv_EmissiveTex)
                + _RimColor * tex2D(_RimTex, IN.uv_RimTex) * smoothstep(0.0, 1.0, rimTerm * _RimIntensity);

            float occlusionValue = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(IN.worldPos, 0);

            o.Alpha *= occlusionValue;
            o.Emission *= occlusionValue;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
