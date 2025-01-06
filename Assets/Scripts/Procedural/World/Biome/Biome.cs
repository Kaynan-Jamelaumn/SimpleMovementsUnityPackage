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

    /// <summary>Texture associated with this biome, used for rendering its appearance.</summary>
    public Texture2D texture;

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


}
