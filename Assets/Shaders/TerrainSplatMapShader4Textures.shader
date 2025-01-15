Shader "Custom/TerrainSplatMapShader4Textures" {
    Properties {
        _MainTex ("Base Map", 2D) = "white" {}
        _TextureR ("Red Texture", 2D) = "white" {}
        _TextureG ("Green Texture", 2D) = "white" {}
        _TextureB ("Blue Texture", 2D) = "white" {}
        _TextureA ("Alpha Texture", 2D) = "white" {}
        _SplatMap ("Splat Map", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        // Omitted for brevity: pass definitions and other shader structures
        CGPROGRAM
        // User-specified pragmas, includes, and defines
        #pragma surface surf Standard fullforwardshadows

        // Texture samplers
        sampler2D _MainTex;
        sampler2D _TextureR;
        sampler2D _TextureG;
        sampler2D _TextureB;
        sampler2D _TextureA;
        sampler2D _SplatMap;

        struct Input {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Sample the splat map
            fixed4 splatControl = tex2D(_SplatMap, IN.uv_MainTex);

            // Sample each texture map
            fixed4 texR = tex2D(_TextureR, IN.uv_MainTex);
            fixed4 texG = tex2D(_TextureG, IN.uv_MainTex);
            fixed4 texB = tex2D(_TextureB, IN.uv_MainTex);
            fixed4 texA = tex2D(_TextureA, IN.uv_MainTex);

            // Combine the textures based on the splat map
            fixed4 combinedTexture = texR * splatControl.r + texG * splatControl.g + texB * splatControl.b + texA * splatControl.a;

            // Set the combined texture color as the albedo color
            o.Albedo = combinedTexture.rgb;
            // Metallic and smoothness values would be set here as well
        }
        ENDCG
    }
    // Fallback to a simpler shader if necessary
    Fallback "Diffuse"
}




// Shader "Custom/TerrainSxplatMapShader"
// {
//     Properties
//     {
//         _Color ("Color", Color) = (1,1,1,1)
//         _MainTex ("Albedo (RGB)", 2D) = "white" {}
//         _Glossiness ("Smoothness", Range(0,1)) = 0.5
//         _Metallic ("Metallic", Range(0,1)) = 0.0
//     }
//     SubShader
//     {
//         Tags { "RenderType"="Opaque" }
//         LOD 200

//         CGPROGRAM
//         // Physically based Standard lighting model, and enable shadows on all light types
//         #pragma surface surf Standard fullforwardshadows

//         // Use shader model 3.0 target, to get nicer looking lighting
//         #pragma target 3.0

//         sampler2D _MainTex;

//         struct Input
//         {
//             float2 uv_MainTex;
//         };

//         half _Glossiness;
//         half _Metallic;
//         fixed4 _Color;

//         // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
//         // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
//         // #pragma instancing_options assumeuniformscaling
//         UNITY_INSTANCING_BUFFER_START(Props)
//             // put more per-instance properties here
//         UNITY_INSTANCING_BUFFER_END(Props)

//         void surf (Input IN, inout SurfaceOutputStandard o)
//         {
//             // Albedo comes from a texture tinted by color
//             fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
//             o.Albedo = c.rgb;
//             // Metallic and smoothness come from slider variables
//             o.Metallic = _Metallic;
//             o.Smoothness = _Glossiness;
//             o.Alpha = c.a;
//         }
//         ENDCG
//     }
//     FallBack "Diffuse"
// }
