Shader "MR/PassthroughShadowCaster"
{
    Properties
    {
        // This shader is just used to cast shadows for invisible meshes (controllers, etc.)
        [ToggleOff] _ZWrite ("ZWrite", Int) = 1.0
    }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "Queue"="Geometry-1500"
        }
        LOD 200
        ColorMask 0
        ZWrite [_ZWrite]

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Nothing fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
            float2 uv_MainTex;
        };

        half4 LightingNothing (SurfaceOutput s, half3 lightDir, half atten) {
              half4 c;
              c.rgb = s.Albedo;
              c.a = s.Alpha;
              return c;
        }

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Doesn't matter because ColorMask is 0
            o.Albedo = 1;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
