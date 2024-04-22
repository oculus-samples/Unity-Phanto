Shader "XPD/Crystals"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0,6)) = 3
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _EmissionMap ("Emission Map", 2D) = "black" {} // Added emission map property
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _EmissionMap; // Added emission map sampler

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float2 uv_BumpMap;
            float2 uv_EmissionMap; // Added UV for emission map
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _RimColor;
        half _RimPower;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;

            // Add rim lighting
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower);

            // Apply normal map
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));

            // Apply emission map
            fixed3 emissionColor = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb; // Added emission map application
            o.Emission += emissionColor; // Added emission color to output emission
        }
        ENDCG
    }
    FallBack "Diffuse"
}