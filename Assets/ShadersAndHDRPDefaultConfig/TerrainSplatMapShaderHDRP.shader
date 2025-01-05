Shader "Custom/TerrainSplatMapShaderHDRP"
{
    // Properties section defines the input textures used by the shader
    Properties
    {
        // _TextureArray is a 2D texture array containing the biome textures
        _TextureArray("Texture Array", 2DArray) = "" {}

        // _SplatMaps is a 2D texture array containing the splat maps for terrain blending
        _SplatMaps("Splat Maps", 2DArray) = "" {}
    }

    // HLSL code block that includes necessary libraries and defines shader parameters
    HLSLINCLUDE

    // Include necessary libraries for the HDRP pipeline and shader functionality
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

    // Declare texture arrays and samplers for accessing textures in the shader
    TEXTURE2D_ARRAY(_TextureArray);
    SAMPLER(sampler_TextureArray);

    TEXTURE2D_ARRAY(_SplatMaps);
    SAMPLER(sampler_SplatMaps);

    // Constant buffer for material properties
    CBUFFER_START(UnityPerMaterial)
        // _TextureArrayLength is the number of textures in the texture array
        int _TextureArrayLength;
        // _SplatMapCount is the number of splat maps used for terrain blending
        int _SplatMapCount;
    CBUFFER_END

    // Define input and output structures for the vertex and fragment shaders
    struct Attributes
    {
        float3 positionOS : POSITION; // Object-space position
        float2 uv : TEXCOORD0; // Texture coordinates
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION; // Clip-space position
        float2 uv : TEXCOORD0; // Texture coordinates
    };

    // Vertex shader to transform object-space position to clip-space
    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS); // Transform to clip space
        output.uv = input.uv; // Pass through texture coordinates
        return output;
    }

    // Fragment shader to calculate the final color based on the splat maps and texture array
    float4 Frag(Varyings input) : SV_Target
    {
        // Initialize color to black (transparent)
        float4 color = float4(0, 0, 0, 0);

        // Loop through each splat map
        for (int i = 0; i < _SplatMapCount; i++)
        {
            // Sample the splat control values from the splat map at the given UV coordinates
            float4 splatControl = SAMPLE_TEXTURE2D_ARRAY(_SplatMaps, sampler_SplatMaps, input.uv, i);

            // Loop through each texture channel (R, G, B, A) for the current splat map
            for (int j = 0; j < 4; j++)
            {
                // Calculate the texture index for the current texture
                int textureIndex = i * 4 + j;

                // Break if the texture index exceeds the available texture array length
                if (textureIndex >= _TextureArrayLength) break;

                // Accumulate the color by multiplying the splat control with the texture sample
                color += splatControl[j] * SAMPLE_TEXTURE2D_ARRAY(_TextureArray, sampler_TextureArray, input.uv, textureIndex);
            }
        }

        // Ensure the color is clamped between 0 and 1
        return saturate(color);
    }

    ENDHLSL

    // SubShader section defines how the shader is rendered in the pipeline
    SubShader
    {
        // Tag for HDRP rendering pipeline
        Tags { "RenderPipeline"="HDRenderPipeline" }

        // Define the rendering pass
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="Forward" }

            // HLSL code block for the shader pass
            HLSLPROGRAM
            #pragma vertex Vert // Use the vertex shader defined above
            #pragma fragment Frag // Use the fragment shader defined above
            ENDHLSL
        }
    }

    // Fallback shader if HDRP is not available
    Fallback "HDRenderPipeline/Unlit"
}
