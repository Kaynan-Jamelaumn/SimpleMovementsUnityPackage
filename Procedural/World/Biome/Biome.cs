//[System.Serializable]
using System.Collections.Generic;

//[System.Serializable]
using UnityEngine;

/// <summary>
/// Represents a biome within the terrain. A biome defines its characteristics, 
/// including its height range, texture, terrain variation, and associated objects.
/// </summary>
//[System.Serializable]
[CreateAssetMenu(menuName = "Scriptable Objects/Biome", fileName = "NewBiome")]
public class Biome : ScriptableObject
{
    /// <summary>Name of the biome-RECOMMENDED TO BE UNIQUE.</summary>
    public new string name;

    /// <summary>Minimum height value for the biome. Used to define its elevation range.</summary>
    public float minHeight;

    /// <summary>Maximum height value for the biome. Used to define its elevation range.</summary>
    public float maxHeight;

    /// <summary>Primary texture associated with this biome, used for rendering its appearance.</summary>
    public Texture2D texture;

    /// <summary>
    /// Optional: Additional texture variations for this biome to reduce repetition. 
    /// Leave empty to use only the primary texture (original behavior).
    /// Only used when EnableTextureVariations is enabled in TerrainGenerator.
    /// </summary>
    [Tooltip("Optional: Additional texture variations for this biome. Leave empty for original behavior.")]
    public Texture2D[] textureVariations;

    /// <summary>Amplitude of height variations within the biome.</summary>
    public float amplitude;

    /// <summary>Frequency of height details within the biome. Higher values result in more details.</summary>
    public float frequency;

    /// <summary>How Likely is this Biome supposed to be chosen compared to other Biomes</summary>
    public float weight = 1;

    /// <summary>
    /// Persistence controls the detail added or removed at each noise octave. 
    /// Ranges from 0 to 1, where higher values retain more detail.
    /// </summary>
    [Range(0, 1)]
    public float persistence = 1;

    /// <summary>
    /// Gets a random texture variation for this biome, including the primary texture.
    /// Returns the primary texture if no variations are defined or texture variations are disabled.
    /// Only used internally when texture variations are enabled.
    /// </summary>
    /// <param name="seed">Seed for consistent random selection per chunk.</param>
    /// <returns>A texture variation for this biome.</returns>
    public Texture2D GetRandomTextureVariation(int seed = 0)
    {
        // If no variations array exists or is empty, return primary texture (original behavior)
        if (textureVariations == null || textureVariations.Length == 0)
        {
            return texture;
        }

        // Create a list including the primary texture and all variations
        List<Texture2D> allTextures = new List<Texture2D> { texture };
        foreach (var variation in textureVariations)
        {
            if (variation != null)
            {
                allTextures.Add(variation);
            }
        }

        // If only primary texture exists, return it
        if (allTextures.Count == 1)
        {
            return texture;
        }

        // Use seed for consistent random selection
        System.Random random = new System.Random(seed);
        int randomIndex = random.Next(0, allTextures.Count);
        return allTextures[randomIndex];
    }

    /// <summary>
    /// Gets the total number of texture variations including the primary texture.
    /// Returns 1 if no variations are defined (original behavior).
    /// </summary>
    /// <returns>Total number of available textures for this biome.</returns>
    public int GetTextureVariationCount()
    {
        int count = 1; // Primary texture always counts

        if (textureVariations != null)
        {
            foreach (var variation in textureVariations)
            {
                if (variation != null) count++;
            }
        }

        return count;
    }
}