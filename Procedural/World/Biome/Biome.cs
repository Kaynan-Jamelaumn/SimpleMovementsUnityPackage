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
    [Tooltip("Display name of the biome. Keep it unique: it also seeds a per-biome noise offset, so two biomes with the same name and settings would produce identical terrain shapes.")]
    public new string name;

    /// <summary>Minimum height value for the biome. Used to define its elevation range.</summary>
    [Tooltip("Lowest height (world Y) this biome is associated with. Does NOT shape the terrain. Used for height-based texturing (when Texture Based On Voronoi Points is off) and as the default height band for spawning this biome's objects and mobs.")]
    public float minHeight;

    /// <summary>Maximum height value for the biome. Used to define its elevation range.</summary>
    [Tooltip("Highest height (world Y) this biome is associated with. Does NOT shape the terrain. Used for height-based texturing and as the default height band for spawning objects and mobs (their preferred height is the middle of Min/Max).")]
    public float maxHeight;

    /// <summary>Primary texture associated with this biome, used for rendering its appearance.</summary>
    [Tooltip("Main ground texture for this biome.")]
    public Texture2D texture;

    /// <summary>
    /// Optional: Additional texture variations for this biome to reduce repetition. 
    /// Leave empty to use only the primary texture (original behavior).
    /// Only used when EnableTextureVariations is enabled in TerrainGenerator.
    /// </summary>
    [Tooltip("Optional: Additional texture variations for this biome. Leave empty for original behavior.")]
    public Texture2D[] textureVariations;

    /// <summary>Amplitude of height variations within the biome.</summary>
    [Tooltip("How tall this biome's hills are (world units) for the first noise layer; later layers are scaled down by Persistence. Roughly, peaks reach Base Elevation + amplitude x (1 + p + p^2 + ...). 0 = flat. Large differences between neighboring biomes make wider transitions (see Boundary Max Walkable Slope).")]
    public float amplitude;

    /// <summary>
    /// Vertical offset (world Y units) added to this biome's noise, on top of amplitude. Amplitude alone
    /// only controls how ROUGH a biome is (its noise is otherwise centered on 0) - it can't make one
    /// biome systematically sit higher or lower than another. This is what actually separates, say,
    /// Highlands from a Depression: two biomes can have the same amplitude/roughness and still occupy
    /// very different elevation bands. Terrain is kept above sea level near coasts regardless, and a low
    /// biome never turns into ocean by itself - oceans come from the continent field (see <see cref="OceanGenerator"/>).
    /// </summary>
    [Tooltip("Vertical offset (world Y units) for this biome, on top of its own amplitude/roughness. Lets one biome genuinely sit higher/lower than another (Highlands vs. Depression) instead of every biome only differing in roughness around the same height-0 baseline. A low biome does not become ocean by itself - oceans come from the continent field.")]
    public float baseElevation = 0f;

    /// <summary>
    /// The shape of the ground in this biome (see <see cref="LandformType"/>). Classic keeps the original
    /// terrain. Which setting is actually used also depends on the Terrain Generator's Terrain Shape Mode.
    /// </summary>
    [Tooltip("The shape of the ground in this biome.\n\nClassic: the original layered-noise terrain.\nPlains: broad, low swells and shallow basins.\nHills: rounded, rolling hills with gentle slopes.\nMountains: ranges of connected peaks and ridges of varied height, with valleys between them.\nDunes: wind-aligned sand dunes in fields.\nWetland: flat, low ground with hummocks and hollows.\nPlateau: flat-topped tablelands with cliff steps and canyons.\n\nAmplitude sets the relief height, Frequency the feature size (e.g. peak spacing), Persistence the roughness. Only used when the Terrain Generator's Terrain Shape Mode is not Classic Only.")]
    public LandformType landform = LandformType.Classic;

    /// <summary>Frequency of height details within the biome. Higher values result in more details.</summary>
    [Tooltip("How many hills fit across one chunk width for the first noise layer (the noise repeats about every chunk width / frequency world units). Higher = smaller, more frequent bumps; lower = broad rolling shapes. Changing the Terrain Size also changes this scale.")]
    public float frequency;

    /// <summary>How Likely is this Biome supposed to be chosen compared to other Biomes</summary>
    [Tooltip("How likely this biome is picked compared to others (2 = twice as likely as 1). Only used when Use Weighted Biome is on; combined with climate fit when climate placement is on.")]
    public float weight = 1;

    /// <summary>
    /// Persistence controls the detail added or removed at each noise octave.
    /// Ranges from 0 to 1, where higher values retain more detail.
    /// </summary>
    [Tooltip("How much each extra noise layer keeps of the previous layer's height (0-1). Low (0.3) = smooth hills with little fine detail; high (0.6+) = rough, craggy terrain. 1 = every layer as tall as the first (very noisy).")]
    [Range(0, 1)]
    public float persistence = 1;

    [Header("Climate (Natural Biome Placement)")]
    /// <summary>Ideal temperature for this biome: 0 = coldest, 1 = hottest.</summary>
    [Tooltip("Ideal temperature for this biome: 0 = coldest, 1 = hottest.")]
    [Range(0f, 1f)] public float idealTemperature = 0.5f;

    /// <summary>Ideal moisture/rainfall for this biome: 0 = driest, 1 = wettest.</summary>
    [Tooltip("Ideal moisture/rainfall for this biome: 0 = driest, 1 = wettest.")]
    [Range(0f, 1f)] public float idealMoisture = 0.5f;

    /// <summary>How tolerant this biome is to temperature deviating from its ideal. Smaller values create a narrower, more distinct climate niche.</summary>
    [Tooltip("How tolerant this biome is to temperature deviating from its ideal. Smaller values create a narrower, more distinct climate niche.")]
    [Range(0.05f, 1f)] public float temperatureTolerance = 0.35f;

    /// <summary>How tolerant this biome is to moisture deviating from its ideal.</summary>
    [Tooltip("How tolerant this biome is to moisture deviating from its ideal.")]
    [Range(0.05f, 1f)] public float moistureTolerance = 0.35f;

    [Header("Erosion")]
    /// <summary>Resistance to erosion: 0 = soft/erodes easily (sand, loose soil), 1 = hard rock that barely erodes.</summary>
    [Tooltip("Resistance to erosion: 0 = soft/erodes easily (sand, loose soil), 1 = hard rock that barely erodes.")]
    [Range(0f, 1f)] public float erosionResistance = 0.5f;

    /// <summary>Multiplier applied to rainfall-driven water erosion strength within this biome. Wetter biomes (jungles, swamps) should generally use higher values; arid biomes (deserts) lower.</summary>
    [Tooltip("Multiplier applied to rainfall-driven water erosion strength within this biome. Wetter biomes should generally use higher values; arid biomes lower.")]
    [Range(0f, 3f)] public float rainfallErosionMultiplier = 1f;

    [Header("Water")]
    /// <summary>
    /// Whether lakes, ponds and river springs may originate in this biome. A biome describes an
    /// environment, not a geographic feature, so this never decides water TYPE and never removes water
    /// that belongs to a larger feature: oceans ignore biomes entirely, and a river that started
    /// elsewhere keeps flowing through this biome (a river can't just stop at a biome border).
    /// </summary>
    [Tooltip("Whether lakes, ponds and river springs may originate in this biome. Oceans ignore biomes, and rivers from elsewhere still flow through - a river can't stop at a biome border.")]
    public bool allowsWaterBodies = true;

    /// <summary>Multiplier on how likely a lake is to form here (0 = never, 1 = normal, up to 3).</summary>
    [Tooltip("How likely a lake is to form in this biome (0 = never, 1 = normal, 3 = very common).")]
    [Range(0f, 3f)] public float lakeLikelihood = 1f;

    /// <summary>Multiplier on how likely a pond is to form here - e.g. high for swamps/wetlands, low for deserts.</summary>
    [Tooltip("How likely a pond is to form in this biome (0 = never, 1 = normal, 3 = very common - e.g. swamps).")]
    [Range(0f, 3f)] public float pondLikelihood = 1f;

    /// <summary>Multiplier on how likely a river spring is to appear here - e.g. high for mountains/highlands.</summary>
    [Tooltip("How likely a river spring is to appear in this biome (0 = never, 1 = normal, 3 = very common - e.g. mountains).")]
    [Range(0f, 3f)] public float riverSpringLikelihood = 1f;

    /// <summary>
    /// Scores how well a given climate matches this biome's ideal temperature/moisture niche.
    /// Returns 1 when the climate exactly matches the ideal, falling off toward 0 the further away it is
    /// (relative to the configured tolerances). Used to place biomes in climatically plausible locations.
    /// </summary>
    /// <param name="temperature">Temperature at the position being evaluated, in [0,1].</param>
    /// <param name="moisture">Moisture at the position being evaluated, in [0,1].</param>
    /// <returns>A fitness score in (0,1], higher is a better climate match.</returns>
    public float ClimateFitness(float temperature, float moisture)
    {
        float tempDelta = (temperature - idealTemperature) / Mathf.Max(0.0001f, temperatureTolerance);
        float moistDelta = (moisture - idealMoisture) / Mathf.Max(0.0001f, moistureTolerance);
        float distanceSquared = tempDelta * tempDelta + moistDelta * moistDelta;
        return Mathf.Exp(-distanceSquared);
    }

    /// <summary>
    /// Analytic worst-case magnitude of this biome's fBm noise output (see <c>HeightGenerator.ComputeBiomeNoise</c>):
    /// the sum of every octave's amplitude, assuming (unrealistically, but safely) that every octave
    /// peaks at once. Used by <see cref="VoronoiBiomeGenerator.GetBiomeBlend"/> to size the transition
    /// band between two neighboring biomes wide enough that the elevation change required to go from
    /// "fully this biome" to "fully the other" never exceeds a configured max slope - i.e. so biomes
    /// with very different height ranges (mountains vs. plains) don't produce an unwalkable step at
    /// their shared border. Deliberately an upper bound rather than a tight estimate: erring toward a
    /// wider-than-strictly-needed transition band is the safe direction (still traversable), while
    /// underestimating it would recreate the exact problem this exists to prevent.
    /// </summary>
    /// <param name="octaves">Number of fBm octaves (a global <see cref="TerrainGenerator"/> setting, not per-biome).</param>
    public float EstimateMaxHeightAmplitude(int octaves)
    {
        float amp = amplitude;
        float total = 0f;

        for (int o = 0; o < octaves; o++)
        {
            total += amp;
            amp *= persistence;

            if (amp < 0.001f)
                break;
        }

        return total;
    }

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