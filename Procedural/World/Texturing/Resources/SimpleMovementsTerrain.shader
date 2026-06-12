// The terrain package's own terrain shader, for URP and the Built-in render pipeline (under HDRP the terrain uses
// your project's HDRP terrain shader - see the TerrainGenerator's Terrain Material settings).
//
// Biome textures blended by the chunk's splat maps, projected in world space: from above on gentle ground, and
// from the sides too on steep ground (tri-planar mapping), so cliffs, mountain faces and abrupt rises and drops
// don't stretch the textures - and every texture continues seamlessly across chunk borders. Ground near water is
// darker and glossier (the chunk's wetness map). All of it is in SimpleMovementsTerrain.hlsl.
//
// The TerrainGenerator fills every property per chunk (see TextureGenerator.CreateChunkMaterial); the values
// below are only defaults. It is in a Resources folder so it is always included in builds.
Shader "SimpleMovements/Terrain"
{
    Properties
    {
        [NoScaleOffset] _TextureArray ("Biome Textures", 2DArray) = "" {}
        [NoScaleOffset] _SplatMaps ("Splat Maps", 2DArray) = "" {}
        [NoScaleOffset] _WetnessMap ("Wetness", 2D) = "black" {}
        _TextureArrayLength ("Texture Count", Float) = 1
        _SplatMapCount ("Splat Map Count", Float) = 1
        _BiomeCount ("Biome Count", Float) = 1
        _TextureTiling ("Tiling (per world unit)", Float) = 0.083
        _TextureBlendSharpness ("Biome Blend Sharpness", Range(0.25, 8)) = 1
        _UVRotationStrength ("Layer Rotation", Range(0, 1)) = 0
        _UVScaleVariation ("Layer Scale Variation", Range(0.5, 2)) = 1
        _UVNoiseStrength ("UV Noise Strength", Float) = 0
        _UVNoiseScale ("UV Noise Scale", Float) = 0.05
        _NoiseSeedOffset ("Noise Seed", Float) = 0
        _TriplanarStrength ("Tri-Planar Strength", Range(0, 1)) = 1
        _TriplanarSharpness ("Tri-Planar Blend Sharpness", Range(1, 16)) = 6
        _TriplanarSlopeStart ("Tri-Planar From Slope", Range(0, 90)) = 25
        _TriplanarSlopeEnd ("Tri-Planar Full At Slope", Range(0, 90)) = 45
        _Smoothness ("Smoothness", Range(0, 1)) = 0.08
        _WetnessDarkening ("Wetness Darkening", Range(0, 1)) = 0.35
        _WetnessSmoothness ("Wetness Smoothness", Range(0, 1)) = 0.55
    }

    // ------------------------------------------------------------------ URP
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal": "12.0" }
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "SimpleMovementsTerrain.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainVert
            #pragma fragment TerrainFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 splatUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 splatUV : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half3 vertexLight : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings TerrainVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.splatUV = input.splatUV;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.vertexLight = VertexLighting(positions.positionWS, normals.normalWS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 TerrainFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 albedo;
                float smoothness;
                TerrainSurface(input.positionWS, normalWS, input.splatUV, albedo, smoothness);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                inputData.shadowCoord = input.screenPos;
            #else
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = input.vertexLight;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.specular = 0;
                surface.smoothness = smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            // (CommonMaterial first: older URP versions' Shadows.hlsl uses it without including it.)
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half4 DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    // ------------------------------------------------------------------ Built-in render pipeline
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        CGPROGRAM
        #pragma surface TerrainSurf Standard fullforwardshadows vertex:TerrainSurfVert addshadow
        #pragma target 3.5
        #pragma require 2darray

        #include "SimpleMovementsTerrain.hlsl"

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float2 splatUV;
        };

        void TerrainSurfVert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.splatUV = v.texcoord1.xy;
        }

        void TerrainSurf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo;
            float smoothness;
            TerrainSurface(IN.worldPos, IN.worldNormal, IN.splatUV, albedo, smoothness);
            o.Albedo = albedo;
            o.Metallic = 0;
            o.Smoothness = smoothness;
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
