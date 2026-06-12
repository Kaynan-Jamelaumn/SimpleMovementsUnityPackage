using UnityEngine;
using System.Collections.Generic;

/// <summary>Which shader draws the terrain (see <see cref="TerrainGenerator"/> > Terrain Material).</summary>
public enum TerrainShaderMode
{
    /// <summary>The package's "SimpleMovements/Terrain" shader (URP and Built-in): tri-planar on steep ground, wetness near water.</summary>
    PackageTriplanar,
    /// <summary>The project's "Custom/TerrainSplatMapShaderURP" (or HDRP) shader.</summary>
    ProjectShader,
    /// <summary>A copy of the generator's Custom Terrain Material (its shader reads the same properties).</summary>
    CustomMaterial,
}

/// <summary>
/// The <see cref="TextureGenerator"/> class is responsible for managing texture assignments for terrain rendering.
/// It handles the creation and assignment of texture arrays to materials, including biome textures and splat maps.
///
/// Memory: the biome textures are copied into one texture array that every chunk shares (built the first time
/// a chunk needs it); each chunk only owns its material, its splat maps and its wetness map, which
/// <see cref="ReleaseChunkMaterial"/> frees when the chunk is unloaded.
/// </summary>
public class TextureGenerator
{
    public const string PackageShaderName = "SimpleMovements/Terrain";

    // Biome texture arrays shared by every chunk, by the textures they hold and their resolution.
    private static readonly List<KeyValuePair<Texture2D[], Texture2DArray>> SharedBiomeArrays = new List<KeyValuePair<Texture2D[], Texture2DArray>>();
    private static readonly List<int> SharedBiomeArrayResolutions = new List<int>();

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
    /// Supports texture variations when enabled in TerrainGenerator.
    /// </summary>
    /// <param name="splatMaps">An array of <see cref="Texture2D"/> representing the splat maps used for terrain blending.</param>
    /// <param name="terrainGenerator">An instance of <see cref="TerrainGenerator"/> containing biome definitions and terrain data.</param>
    /// <param name="meshRenderer">The <see cref="MeshRenderer"/> to which the textures will be applied.</param>
    /// <param name="shouldUseHDRPShader">Whether to use HDRP or URP shader.</param>
    public void AssignTexture(Texture2D[] splatMaps, TerrainGenerator terrainGenerator, MeshRenderer meshRenderer, bool shouldUseHDRPShader)
    {
        if (splatMaps == null || splatMaps.Length == 0)
            return;

        var pixels = new Color32[splatMaps.Length][];
        for (int i = 0; i < splatMaps.Length; i++)
            pixels[i] = splatMaps[i].GetPixels32();

        Material material = CreateChunkMaterial(pixels, splatMaps[0].width, terrainGenerator, shouldUseHDRPShader);
        if (material != null)
            meshRenderer.sharedMaterial = material;
    }

    /// <summary>
    /// A chunk's terrain material: the biome textures (a texture array shared by every chunk), the chunk's splat
    /// maps built straight from <paramref name="splatPixels"/> (one RGBA array per splat map, 4 biomes each, as
    /// computed on a worker thread by <see cref="SplatMapGenerator.GenerateSplatPixels"/>), and the texturing
    /// settings. Null if no usable shader was found. Free it with <see cref="ReleaseChunkMaterial"/>.
    /// </summary>
    public static Material CreateChunkMaterial(Color32[][] splatPixels, int splatSize, TerrainGenerator terrainGenerator, bool shouldUseHDRPShader)
    {
        if (splatPixels == null || splatPixels.Length == 0)
            return null;

        Material mat = CreateMaterial(terrainGenerator, shouldUseHDRPShader, out bool packageShader);
        if (mat == null)
            return null;

        List<Texture2D> biomeTextures = terrainGenerator.EnableTextureVariations
            ? CreateBiomeTextureListWithVariations(terrainGenerator)
            : CreateBiomeTextureListOriginal(terrainGenerator);
        int textureResolution = GetOptimalTextureResolution(terrainGenerator);
        Texture2DArray textureArray = GetSharedBiomeArray(biomeTextures, textureResolution);

        // The package shader reads the weights as plain numbers (linear); project shaders get the sRGB array
        // they always had, so they look exactly as before.
        Texture2DArray splatMapArray = CreateSplatArray(splatPixels, splatSize, packageShader);

        mat.SetTexture("_TextureArray", textureArray);
        mat.SetTexture("_SplatMaps", splatMapArray);
        mat.SetInt("_TextureArrayLength", biomeTextures.Count);
        mat.SetInt("_SplatMapCount", splatPixels.Length);
        mat.SetInt("_BiomeCount", terrainGenerator.BiomeDefinitions.Length);

        // Set shader enhancement properties only if enabled
        if (terrainGenerator.EnableShaderEnhancements)
        {
            mat.SetFloat("_UVRotationStrength", terrainGenerator.ShaderUVRotationStrength);
            mat.SetFloat("_UVScaleVariation", terrainGenerator.ShaderUVScaleVariation);
            mat.SetFloat("_TextureBlendSharpness", terrainGenerator.ShaderTextureBlendSharpness);
        }
        else
        {
            // Set default values for shader properties when enhancements are disabled
            mat.SetFloat("_UVRotationStrength", 0f);
            mat.SetFloat("_UVScaleVariation", 1f);
            mat.SetFloat("_TextureBlendSharpness", 1f);
        }

        // Wire the UV noise properties - previously these had NO material property at all,
        // so the shader's GenerateUVVariation ran at full, hardcoded strength unconditionally,
        // completely ignoring EnableTextureVariations/EnableUVNoise. Strength is now properly
        // 0 (fully disabled, no offset applied at all) when those toggles are off.
        mat.SetFloat("_UVNoiseStrength", terrainGenerator.EnableUVNoise ? terrainGenerator.UVNoiseStrength : 0f);
        mat.SetFloat("_UVNoiseScale", Mathf.Max(0.0001f, terrainGenerator.UVNoiseScale));
        // Seeds the shader's noise field so texture variation (when enabled) differs between
        // worlds/seeds instead of always producing the exact same pattern at the same world position.
        mat.SetFloat("_NoiseSeedOffset", terrainGenerator.VoronoiSeed * 0.6180339887f);

        // The package shader's own settings (other shaders ignore properties they don't have).
        mat.SetFloat("_TextureTiling", 1f / terrainGenerator.TerrainTextureWorldSize);
        mat.SetFloat("_TriplanarStrength", terrainGenerator.TriplanarStrength);
        mat.SetFloat("_TriplanarSharpness", terrainGenerator.TriplanarSharpness);
        mat.SetFloat("_TriplanarSlopeStart", terrainGenerator.TriplanarSlopeStart);
        mat.SetFloat("_TriplanarSlopeEnd", terrainGenerator.TriplanarSlopeEnd);
        mat.SetFloat("_Smoothness", terrainGenerator.TerrainSmoothness);
        mat.SetFloat("_WetnessDarkening", terrainGenerator.WetnessDarkening);
        mat.SetFloat("_WetnessSmoothness", terrainGenerator.WetnessSmoothness);
        return mat;
    }

    /// <summary>
    /// Frees what a chunk's terrain material owns - the material, its splat maps and its wetness map - leaving the
    /// shared biome texture array alone. Call when the chunk is unloaded.
    /// </summary>
    public static void ReleaseChunkMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_SplatMaps"))
            DestroyObject(material.GetTexture("_SplatMaps"));
        if (material.HasProperty("_WetnessMap"))
            DestroyObject(material.GetTexture("_WetnessMap"));
        DestroyObject(material);
    }

    /// <summary>Destroys the shared biome texture arrays (when the terrain is destroyed or its biomes change).</summary>
    public static void ReleaseSharedTextures()
    {
        foreach (KeyValuePair<Texture2D[], Texture2DArray> entry in SharedBiomeArrays)
            DestroyObject(entry.Value);
        SharedBiomeArrays.Clear();
        SharedBiomeArrayResolutions.Clear();
    }

    /// <summary>
    /// A material for the chosen terrain shader (see <see cref="TerrainShaderMode"/>), falling back to the other
    /// shader when the chosen one isn't available (the package shader under HDRP, or a missing project shader).
    /// </summary>
    private static Material CreateMaterial(TerrainGenerator terrainGenerator, bool shouldUseHDRPShader, out bool packageShader)
    {
        packageShader = false;
        TerrainShaderMode mode = terrainGenerator.TerrainShader;
        if (mode == TerrainShaderMode.CustomMaterial && terrainGenerator.CustomTerrainMaterial != null)
        {
            Material template = terrainGenerator.CustomTerrainMaterial;
            packageShader = template.shader != null && template.shader.name == PackageShaderName;
            return new Material(template) { name = "Terrain (" + template.name + ")" };
        }

        Shader package = FindPackageShader(shouldUseHDRPShader);
        string projectShaderName = "Custom/TerrainSplat" + (shouldUseHDRPShader ? "MapShaderHDRP" : "MapShaderURP");
        Shader project = Shader.Find(projectShaderName);

        Shader shader = mode == TerrainShaderMode.PackageTriplanar ? (package != null ? package : project) : (project != null ? project : package);
        if (shader == null)
        {
            Debug.LogError("TextureGenerator: no terrain shader found - neither '" + PackageShaderName + "' (URP / Built-in) nor '" + projectShaderName + "'.");
            return null;
        }

        packageShader = shader == package;
        return new Material(shader) { name = "Terrain" };
    }

    /// <summary>The package's terrain shader, or null under HDRP (not supported) or if it is missing.</summary>
    private static Shader FindPackageShader(bool shouldUseHDRPShader)
    {
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (shouldUseHDRPShader || (pipeline != null && !pipeline.GetType().Name.Contains("Universal")))
            return null;
        Shader shader = Shader.Find(PackageShaderName);
        return shader != null && shader.isSupported ? shader : null;
    }

    /// <summary>The biome texture array for these textures - built once, then shared by every chunk.</summary>
    private static Texture2DArray GetSharedBiomeArray(List<Texture2D> textures, int resolution)
    {
        for (int i = 0; i < SharedBiomeArrays.Count; i++)
        {
            KeyValuePair<Texture2D[], Texture2DArray> entry = SharedBiomeArrays[i];
            if (entry.Value != null && SharedBiomeArrayResolutions[i] == resolution && SameTextures(entry.Key, textures))
                return entry.Value;
        }

        Texture2DArray array = CreateTextureArray(textures.ToArray(), resolution, resolution, TextureFormat.RGBA32);
        array.name = "Biome Textures (shared)";
        SharedBiomeArrays.Add(new KeyValuePair<Texture2D[], Texture2DArray>(textures.ToArray(), array));
        SharedBiomeArrayResolutions.Add(resolution);
        return array;
    }

    private static bool SameTextures(Texture2D[] a, List<Texture2D> b)
    {
        if (a.Length != b.Count)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (!ReferenceEquals(a[i], b[i]))
                return false;
        return true;
    }

    /// <summary>A chunk's splat maps as a texture array, written directly from their pixels.</summary>
    private static Texture2DArray CreateSplatArray(Color32[][] pixels, int size, bool linear)
    {
        var array = new Texture2DArray(size, size, pixels.Length, TextureFormat.RGBA32, false, linear)
        {
            name = "Splat Maps",
            // Clamp, not Repeat: the splat map is sampled with a dedicated 0-1 UV that should never
            // wrap within a chunk. This is a defensive guard against float rounding at uv=1.0 only -
            // the real fix is MeshGenerator's separate, untiled splat UV channel (mesh.uv2).
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int i = 0; i < pixels.Length; i++)
            array.SetPixels32(pixels[i], i);
        // Upload, then drop the CPU copy - nothing reads it back.
        array.Apply(false, true);
        return array;
    }

    /// <summary>
    /// Creates the original biome texture list (primary textures only).
    /// Used when texture variations are disabled.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing biome definitions.</param>
    /// <returns>List of primary biome textures.</returns>
    private static List<Texture2D> CreateBiomeTextureListOriginal(TerrainGenerator terrainGenerator)
    {
        List<Texture2D> textures = new List<Texture2D>();

        foreach (var biomeDefinition in terrainGenerator.BiomeDefinitions)
        {
            textures.Add(biomeDefinition.BiomePrefab.texture);
        }

        return textures;
    }

    /// <summary>
    /// Creates a comprehensive list of all biome textures including variations.
    /// Used when texture variations are enabled.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing biome definitions.</param>
    /// <returns>List of all textures including variations.</returns>
    private static List<Texture2D> CreateBiomeTextureListWithVariations(TerrainGenerator terrainGenerator)
    {
        List<Texture2D> allTextures = new List<Texture2D>();

        // First pass: Add primary textures to maintain base biome mapping
        foreach (var biomeDefinition in terrainGenerator.BiomeDefinitions)
        {
            Biome biome = biomeDefinition.BiomePrefab;
            allTextures.Add(biome.texture);
        }

        // Second pass: Add texture variations if they exist
        foreach (var biomeDefinition in terrainGenerator.BiomeDefinitions)
        {
            Biome biome = biomeDefinition.BiomePrefab;
            if (biome.textureVariations != null)
            {
                foreach (var variation in biome.textureVariations)
                {
                    if (variation != null)
                    {
                        allTextures.Add(variation);
                    }
                }
            }
        }

        return allTextures;
    }

    /// <summary>
    /// Calculates the optimal texture resolution based on terrain size to prevent stretching.
    /// Larger terrains get higher resolution textures to maintain visual quality.
    /// </summary>
    /// <param name="terrainGenerator">The terrain generator containing size information.</param>
    /// <returns>Optimal texture resolution for the current terrain size.</returns>
    private static int GetOptimalTextureResolution(TerrainGenerator terrainGenerator)
    {
        // Base resolution for the largest terrain size
        int baseResolution = 1024;

        // Scale resolution based on terrain size
        // Larger terrains get higher resolution to prevent stretching
        float sizeRatio = (float)terrainGenerator.ChunkSize / (float)TerrainGenerator.MaxChunkSize;

        // Calculate resolution (minimum 512, maximum 2048)
        int calculatedResolution = Mathf.RoundToInt(baseResolution * Mathf.Sqrt(sizeRatio));
        return Mathf.Clamp(calculatedResolution, 512, 2048);
    }

    /// <summary>
    /// Creates a <see cref="Texture2DArray"/> from an array of textures. This method standardizes the textures and copies them into a texture array.
    /// The resized copies are destroyed once copied, and the array's CPU copy is dropped after uploading.
    /// </summary>
    /// <param name="textures">An array of <see cref="Texture2D"/> objects that will be copied into the texture array.</param>
    /// <param name="width">The width of the texture array (all textures will be resized to this width).</param>
    /// <param name="height">The height of the texture array (all textures will be resized to this height).</param>
    /// <param name="format">The texture format to be used for the texture array.</param>
    /// <param name="mipmaps">Whether mipmaps should be generated for the texture array (default is true).</param>
    /// <returns>A <see cref="Texture2DArray"/> containing the standardized textures.</returns>
    private static Texture2DArray CreateTextureArray(Texture2D[] textures, int width, int height, TextureFormat format, bool mipmaps = true)
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

            // The resized copy was only needed for this.
            DestroyObject(standardizedTexture);
        }

        // Apply the texture array (commits changes to the GPU, builds the mipmaps) and free its CPU copy.
        textureArray.Apply(mipmaps, true);

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
    private static Texture2D StandardizeTexture(Texture2D sourceTexture, int width, int height, TextureFormat format)
    {
        // Create a temporary RenderTexture to hold the resized texture
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);

        // Blit the source texture into the render texture (resize and copy)
        Graphics.Blit(sourceTexture, renderTexture);

        // Create a new Texture2D to hold the standardized texture
        Texture2D standardizedTexture = new Texture2D(width, height, format, true);

        // Set the RenderTexture as the active texture and read the pixels into the new texture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        standardizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        standardizedTexture.Apply();

        // Release the temporary render texture
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);

        return standardizedTexture;
    }

    private static void DestroyObject(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}
