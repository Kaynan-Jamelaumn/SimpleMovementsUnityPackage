Shader "Custom/TerrainSplatMapShaderURP"
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

    // Include necessary libraries for the URP pipeline and shader functionality
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

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
        float4 color = float4(0, 0, 0, 0); // Initialize color to black
        int maxTextures = min(_TextureArrayLength, _SplatMapCount * 4); // Determine max number of textures to sample

        // Flattened loop for better performance
        for (int textureIndex = 0; textureIndex < maxTextures; textureIndex++)
        {
            int splatMapIndex = textureIndex / 4; // Determine the splat map index
            int channelIndex = textureIndex % 4; // Determine the channel index

            // Sample the splat control and texture
            float splatControl = SAMPLE_TEXTURE2D_ARRAY(_SplatMaps, sampler_SplatMaps, input.uv, splatMapIndex)[channelIndex];
            float4 textureSample = SAMPLE_TEXTURE2D_ARRAY(_TextureArray, sampler_TextureArray, input.uv, textureIndex);

            // Accumulate color
            color += splatControl * textureSample;
        }

        return saturate(color); // Ensure the final color is within valid range
    }
    ENDHLSL

    // SubShader section defines how the shader is rendered in the pipeline
    SubShader
    {
        // Tag for URP rendering pipeline
        Tags { "RenderPipeline"="UniversalRenderPipeline" }

        // Define the rendering pass
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            // HLSL code block for the shader pass
            HLSLPROGRAM
            #pragma vertex Vert // Use the vertex shader defined above
            #pragma fragment Frag // Use the fragment shader defined above
            ENDHLSL
        }
    }

    // Fallback shader if URP is not available
    Fallback "UniversalRenderPipeline/Unlit"
}