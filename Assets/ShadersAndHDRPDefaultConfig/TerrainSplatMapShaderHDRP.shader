Shader "Custom/TerrainSplatMapShaderHDRP"
{
    Properties
    {
        _TextureArray("Texture Array", 2DArray) = "" {}
        _SplatMap("Splat Map", 2D) = "white" {}
    }
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

    TEXTURE2D_ARRAY(_TextureArray);
    SAMPLER(sampler_TextureArray);

    TEXTURE2D(_SplatMap);
    SAMPLER(sampler_SplatMap);

    struct Attributes
    {
        float3 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS);
        output.uv = input.uv;
        return output;
    }

    float4 Frag(Varyings input) : SV_Target
    {
        // Sample the splat map
        float4 splatControl = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, input.uv);

        // Initialize the blended color
        float4 color = float4(0, 0, 0, 0);

        // Blend textures from the texture array based on splat map channels
        [unroll(4)] // Unroll the loop for performance optimization
        for (int i = 0; i < 4; i++)
        {
            float3 uvArray = float3(input.uv, i);
            color += splatControl[i] * _TextureArray.Sample(sampler_TextureArray, uvArray);
        }

        return color;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="Forward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback "HDRenderPipeline/Unlit"
}
