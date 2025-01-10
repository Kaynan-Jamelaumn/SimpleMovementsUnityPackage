using UnityEngine;
/// <summary>
/// The <see cref="TextureGenerator"/> class is responsible for managing texture assignments for terrain rendering.
/// It handles the creation and assignment of texture arrays to materials, including biome textures and splat maps.
/// This class supports different approaches for texture assignment and ensures that the correct shaders and materials are used.
/// </summary>
public class TextureGenerator
{    
    /// <summary>
     /// Assigns four individual textures (for different biome layers) and a splat map to a material on a mesh renderer.
     /// This method is useful for assigning a set of textures to a terrain material using a specific shader.
     /// </summary>
     /// <param name="splatMap">A <see cref="Texture2D"/> representing the splat map used for terrain blending.</param>
     /// <param name="terrainGenerator">An instance of <see cref="TerrainGenerator"/> containing biome definitions and terrain data.</param>
     /// <param name="meshRenderer">The <see cref="MeshRenderer"/> to which the textures will be applied.</param>
    public static void AssignTexture4Textures(Texture2D splatMap, TerrainGenerator terrainGenerator, MeshRenderer meshRenderer)
    {
        Material mat = new Material(Shader.Find("Custom/TerrainSplatMapShader"));
        //mat.SetTexture("_MainTex", defaultTexture);
        mat.SetTexture("_TextureR", terrainGenerator.BiomeDefinitions[0].BiomePrefab.texture);
        mat.SetTexture("_TextureG", terrainGenerator.BiomeDefinitions[1].BiomePrefab.texture);
        mat.SetTexture("_TextureB", terrainGenerator.BiomeDefinitions[2].BiomePrefab.texture);
        mat.SetTexture("_TextureA", terrainGenerator.BiomeDefinitions[3].BiomePrefab.texture);
        mat.SetTexture("_SplatMap", splatMap);
        meshRenderer.sharedMaterial = mat;
    }

    /// <summary>
    /// This method assigns texture arrays to a material, including biome textures and splat maps, to a mesh renderer.
    /// It ensures that the correct shader is used, and creates texture arrays for both the biome textures and splat maps.
    /// </summary>
    /// <param name="splatMaps">An array of <see cref="Texture2D"/> representing the splat maps used for terrain blending.</param>
    /// <param name="terrainGenerator">An instance of <see cref="TerrainGenerator"/> containing biome definitions and terrain data.</param>
    /// <param name="meshRenderer">The <see cref="MeshRenderer"/> to which the textures will be applied.</param>
    public void AssignTexture(Texture2D[] splatMaps, TerrainGenerator terrainGenerator, MeshRenderer meshRenderer, bool shouldUseHDRPShader)
    {
        string shaderToUse = shouldUseHDRPShader ? "MapShaderHDRP" : "MapShaderURP";
        // Find the shader used for terrain splat maps
        Shader cachedShader = Shader.Find("Custom/TerrainSplat" + shaderToUse);
           // "MapShaderHDRP"); ;
        if (cachedShader == null)
        {
            Debug.LogError("Failed to find shader: Custom/TerrainSplatMapShaderHDRP");
            return;
        }

        // Check if the mesh renderer already has the correct material and shader
        Material mat = meshRenderer.sharedMaterial;
        if (mat == null || mat.shader != cachedShader)
        {
            // Create a new material if the existing one is invalid or doesn't use the correct shader
            mat = new Material(cachedShader);
        }

        // Create texture array for biome textures based on terrain generator's biome definitions
        Texture2D[] biomeTextures = new Texture2D[terrainGenerator.BiomeDefinitions.Length];
        for (int i = 0; i < terrainGenerator.BiomeDefinitions.Length; i++)
        {
            // Populate the biomeTextures array with textures from the terrain generator's biome definitions
            biomeTextures[i] = terrainGenerator.BiomeDefinitions[i].BiomePrefab.texture;
        }

        // Create a texture array for the biome textures with specified dimensions and format
        Texture2DArray textureArray = CreateTextureArray(biomeTextures, 1024, 1024, TextureFormat.RGBA32);

        // Create a texture array for the splat maps using the provided splat maps array
        Texture2DArray splatMapArray = CreateTextureArray(splatMaps, splatMaps[0].width, splatMaps[0].height, TextureFormat.RGBA32, false);

        // Assign the created texture arrays to the material
        mat.SetTexture("_TextureArray", textureArray);
        mat.SetTexture("_SplatMaps", splatMapArray);

        // Set the length of the texture array and the count of splat maps as material properties
        mat.SetInt("_TextureArrayLength", terrainGenerator.BiomeDefinitions.Length);
        mat.SetInt("_SplatMapCount", splatMaps.Length);

        // Apply the material to the mesh renderer
        meshRenderer.sharedMaterial = mat;
    }
    /// <summary>
    /// Creates a <see cref="Texture2DArray"/> from an array of textures. This method standardizes the textures and copies them into a texture array.
    /// </summary>
    /// <param name="textures">An array of <see cref="Texture2D"/> objects that will be copied into the texture array.</param>
    /// <param name="width">The width of the texture array (all textures will be resized to this width).</param>
    /// <param name="height">The height of the texture array (all textures will be resized to this height).</param>
    /// <param name="format">The texture format to be used for the texture array.</param>
    /// <param name="mipmaps">Whether mipmaps should be generated for the texture array (default is true).</param>
    /// <returns>A <see cref="Texture2DArray"/> containing the standardized textures.</returns>
    private Texture2DArray CreateTextureArray(Texture2D[] textures, int width, int height, TextureFormat format, bool mipmaps = true)
    {
        // Create a new Texture2DArray with the specified dimensions and format
        Texture2DArray textureArray = new Texture2DArray(width, height, textures.Length, format, mipmaps);

        // Loop through the textures and copy them into the texture array
        for (int i = 0; i < textures.Length; i++)
        {
            // Standardize each texture to the target dimensions and format
            Texture2D standardizedTexture = StandardizeTexture(textures[i], width, height, format);

            // Copy the standardized texture into the texture array at the corresponding index
            Graphics.CopyTexture(standardizedTexture, 0, 0, textureArray, i, 0);
        }

        // Apply the texture array (commits changes to the GPU)
        textureArray.Apply();

        return textureArray;
    }
    /// <summary>
    /// Standardizes a texture to a specified width, height, and format. This method resizes the texture and applies the new format.
    /// </summary>
    /// <param name="sourceTexture">The source <see cref="Texture2D"/> that will be standardized.</param>
    /// <param name="width">The target width of the standardized texture.</param>
    /// <param name="height">The target height of the standardized texture.</param>
    /// <param name="format">The target texture format (e.g., <see cref="TextureFormat.RGBA32"/>).</param>
    /// <returns>A new <see cref="Texture2D"/> that has been resized and standardized.</returns>
    private Texture2D StandardizeTexture(Texture2D sourceTexture, int width, int height, TextureFormat format)
    {
        // Create a temporary RenderTexture to hold the resized texture
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);

        // Blit the source texture into the render texture (resize and copy)
        Graphics.Blit(sourceTexture, renderTexture);

        // Create a new Texture2D to hold the standardized texture
        Texture2D standardizedTexture = new Texture2D(width, height, format, true);

        // Set the RenderTexture as the active texture and read the pixels into the new texture
        RenderTexture.active = renderTexture;
        standardizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        standardizedTexture.Apply();

        // Release the temporary render texture
        RenderTexture.ReleaseTemporary(renderTexture);
        RenderTexture.active = null;

        return standardizedTexture;
    }




}