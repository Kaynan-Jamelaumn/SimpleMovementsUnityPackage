using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  configuration settings for managing spawnable portals with advanced natural distribution,
/// environmental awareness, and performance optimization features.
/// </summary>
[System.Serializable]
public class PortalSettings : BaseSettings
{
    [Header("Portal Prefab Configuration")]
    /// <summary>
    /// List of spawnable portal prefabs with their individual configurations.
    /// </summary>
    [Tooltip("List of spawnable portal prefabs with individual configurations.")]
    public List<SpawnablePortal> prefabs;

    /// <summary>
    /// The maximum number of portals allowed to spawn simultaneously.
    /// </summary>
    [Tooltip("Maximum number of portals allowed to spawn simultaneously.")]
    public int maxNumberOfPortals;

    [Header("Natural Distribution Settings")]
    /// <summary>
    /// Minimum distance between portal spawns to prevent overcrowding.
    /// </summary>
    [Tooltip("Minimum distance between portal spawns to prevent overcrowding.")]
    [Range(5f, 100f)]
    public float minDistanceBetweenPortals = 25f;

    /// <summary>
    /// Maximum attempts to find a valid spawn position before giving up.
    /// </summary>
    [Tooltip("Maximum attempts to find a valid spawn position before giving up.")]
    [Range(5, 50)]
    public int maxSpawnAttempts = 10;

    /// <summary>
    /// Prefer spawning portals in center areas of chunks rather than edges.
    /// </summary>
    [Tooltip("Prefer spawning portals in center areas of chunks rather than edges.")]
    public bool preferCenterSpawning = true;

    /// <summary>
    /// Distance from chunk edges to avoid when center spawning is enabled.
    /// </summary>
    [Tooltip("Distance from chunk edges to avoid when center spawning is enabled.")]
    [Range(5f, 50f)]
    public float edgeAvoidanceDistance = 15f;

    [Header("Environmental Restrictions")]
    /// <summary>
    /// Enable height-based portal placement restrictions.
    /// </summary>
    [Tooltip("Enable height-based portal placement restrictions.")]
    public bool useHeightRestrictions = true;

    /// <summary>
    /// Minimum height for portal spawning when height restrictions are enabled.
    /// </summary>
    [Tooltip("Minimum height for portal spawning when height restrictions are enabled.")]
    public float minSpawnHeight = 0f;

    /// <summary>
    /// Maximum height for portal spawning when height restrictions are enabled.
    /// </summary>
    [Tooltip("Maximum height for portal spawning when height restrictions are enabled.")]
    public float maxSpawnHeight = 100f;

    /// <summary>
    /// Enable biome-based portal spawning restrictions and preferences.
    /// </summary>
    [Tooltip("Enable biome-based portal spawning restrictions and preferences.")]
    public bool useBiomeRestrictions = true;

    /// <summary>
    /// Global biomes where no portals should spawn (overrides individual portal settings).
    /// </summary>
    [Tooltip("Global biomes where no portals should spawn (overrides individual settings).")]
    public List<Biome> globalForbiddenBiomes;

    [Header("Spawn Selection Algorithm")]
    /// <summary>
    /// Enable weighted selection based on portal rarity and environmental factors.
    /// </summary>
    [Tooltip("Enable weighted selection based on rarity and environmental factors.")]
    public bool useWeightedSelection = false;

    // UNIMPLEMENTED: Environmental factors not calculated in selection algorithm
    /// <summary>
    /// Enable environmental factor calculations for more realistic spawning.
    /// </summary>
    [Tooltip("Enable environmental factor calculations for more realistic spawning.")]
    public bool useEnvironmentalFactors = true;

    // UNIMPLEMENTED: Rarity favor bias not applied in spawn selection
    /// <summary>
    /// How much to favor rare portals in spawn selection (higher = more rare portal spawns).
    /// </summary>
    [Tooltip("How much to favor rare portals in selection. Higher = more rare spawns.")]
    [Range(0.1f, 3f)]
    public float rarityFavorBias = 1f;

    [Header("Performance and Error Handling")]
    /// <summary>
    /// Maximum number of consecutive spawn failures before temporarily disabling portal spawning.
    /// </summary>
    [Tooltip("Max consecutive failures before temporarily disabling portal spawning.")]
    [Range(3, 20)]
    public int maxConsecutiveFailures = 8;

    /// <summary>
    /// Time to wait after max failures before re-enabling portal spawn attempts.
    /// </summary>
    [Tooltip("Time to wait after max failures before re-enabling spawn attempts.")]
    [Range(10f, 120f)]
    public float failureRecoveryTime = 45f;

    /// <summary>
    /// Enable performance monitoring and detailed logging for portal spawning operations.
    /// </summary>
    [Tooltip("Enable performance monitoring and detailed logging.")]
    public bool enablePerformanceLogging = false;

    // UNIMPLEMENTED: Spawn debug visualization not implemented in PortalSpawner
    /// <summary>
    /// Enable debug visualization of portal spawn attempts and valid positions.
    /// </summary>
    [Tooltip("Enable debug visualization of spawn attempts and positions.")]
    public bool enableSpawnDebugVisualization = false;

    [Header("Dynamic Spawn Adjustment")]
    // UNIMPLEMENTED: Dynamic spawn adjustment not implemented in PortalSpawner
    /// <summary>
    /// Dynamically adjust spawn rates based on current portal density in the area.
    /// </summary>
    [Tooltip("Dynamically adjust spawn rates based on current portal density.")]
    public bool useDynamicSpawnAdjustment = true;

    // UNIMPLEMENTED: Target portal density not used in spawn logic
    /// <summary>
    /// Target portal density per chunk area (portals per square unit).
    /// </summary>
    [Tooltip("Target portal density per chunk area (portals per square unit).")]
    [Range(0.0001f, 0.01f)]
    public float targetPortalDensity = 0.002f;

    // UNIMPLEMENTED: Density adjustment speed not applied
    /// <summary>
    /// How quickly to adjust spawn rates when density is off-target.
    /// </summary>
    [Tooltip("How quickly to adjust spawn rates when density is off-target.")]
    [Range(0.1f, 2f)]
    public float densityAdjustmentSpeed = 1f;

    [Header("Special Conditions")]
    // UNIMPLEMENTED: Time-based spawning not implemented
    /// <summary>
    /// Enable time-based spawning variations (future expansion for day/night cycles).
    /// </summary>
    [Tooltip("Enable time-based spawning variations (future expansion).")]
    public bool enableTimeBasedSpawning = false;

    // UNIMPLEMENTED: Weather-based spawning not implemented
    /// <summary>
    /// Enable weather-based spawning modifications (future expansion).
    /// </summary>
    [Tooltip("Enable weather-based spawning modifications (future expansion).")]
    public bool enableWeatherBasedSpawning = false;

    /// <summary>
    /// Enable player proximity influence on portal spawning.
    /// </summary>
    [Tooltip("Enable player proximity influence on portal spawning.")]
    public bool enablePlayerProximityInfluence = true;

    /// <summary>
    /// Minimum distance from player before portals can spawn.
    /// </summary>
    [Tooltip("Minimum distance from player before portals can spawn.")]
    [Range(20f, 200f)]
    public float minDistanceFromPlayer = 50f;

    /// <summary>
    /// Maximum distance from player where portals will spawn (0 = unlimited).
    /// </summary>
    [Tooltip("Maximum distance from player for spawning (0 = unlimited).")]
    [Range(0f, 500f)]
    public float maxDistanceFromPlayer = 300f;

    /// <summary>
    /// Validates all portal settings and logs warnings for invalid configurations.
    /// </summary>
    public void ValidateSettings()
    {
        // Validate basic settings
        maxNumberOfPortals = Mathf.Max(0, maxNumberOfPortals);
        minDistanceBetweenPortals = Mathf.Max(1f, minDistanceBetweenPortals);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
        edgeAvoidanceDistance = Mathf.Max(0f, edgeAvoidanceDistance);

        // Validate height restrictions
        if (useHeightRestrictions && minSpawnHeight > maxSpawnHeight)
        {
            Debug.LogWarning("PortalSettings: minSpawnHeight > maxSpawnHeight, swapping values");
            float temp = minSpawnHeight;
            minSpawnHeight = maxSpawnHeight;
            maxSpawnHeight = temp;
        }

        // Validate player distance settings
        if (enablePlayerProximityInfluence && maxDistanceFromPlayer > 0 && minDistanceFromPlayer > maxDistanceFromPlayer)
        {
            Debug.LogWarning("PortalSettings: minDistanceFromPlayer > maxDistanceFromPlayer, adjusting");
            maxDistanceFromPlayer = minDistanceFromPlayer + 50f;
        }

        // Validate density settings
        targetPortalDensity = Mathf.Max(0.0001f, targetPortalDensity);
        densityAdjustmentSpeed = Mathf.Max(0.1f, densityAdjustmentSpeed);

        // Validate individual portal prefabs
        if (prefabs != null)
        {
            for (int i = prefabs.Count - 1; i >= 0; i--)
            {
                if (prefabs[i] == null)
                {
                    Debug.LogWarning($"PortalSettings: Removing null portal prefab at index {i}");
                    prefabs.RemoveAt(i);
                }
                else
                {
                    prefabs[i].ValidateConfiguration();
                }
            }

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("PortalSettings: No valid portal prefabs configured!");
            }
        }

        // Validate base settings
        ValidateBaseSettings();
    }

    /// <summary>
    /// Gets the effective spawn rate modifier based on current conditions.
    /// </summary>
    /// <param name="currentDensity">Current portal density in the area.</param>
    /// <param name="timeOfDay">Current time of day (0-24).</param>
    /// <returns>Spawn rate modifier (1.0 = normal rate).</returns>
    public float GetEffectiveSpawnRateModifier(float currentDensity, float timeOfDay = 12f)
    {
        float modifier = 1f;

        // Apply dynamic density adjustment
        if (useDynamicSpawnAdjustment)
        {
            float densityRatio = currentDensity / targetPortalDensity;
            if (densityRatio > 1f)
            {
                // Too many portals, reduce spawn rate
                modifier *= Mathf.Lerp(1f, 0.1f, (densityRatio - 1f) * densityAdjustmentSpeed);
            }
            else if (densityRatio < 0.5f)
            {
                // Too few portals, increase spawn rate
                modifier *= Mathf.Lerp(1f, 2f, (0.5f - densityRatio) * densityAdjustmentSpeed);
            }
        }

        // Apply time-based modifications (future expansion)
        if (enableTimeBasedSpawning)
        {
            // Example: Portals might be more active during certain times
            float timeModifier = 1f + 0.3f * Mathf.Sin((timeOfDay / 24f) * 2f * Mathf.PI);
            modifier *= timeModifier;
        }

        return Mathf.Clamp(modifier, 0.1f, 3f);
    }

    /// <summary>
    /// Checks if a portal can spawn in the given biome based on global restrictions.
    /// </summary>
    /// <param name="biome">Biome to check.</param>
    /// <returns>True if biome allows portal spawning.</returns>
    public bool IsBiomeAllowed(Biome biome)
    {
        if (!useBiomeRestrictions) return true;

        if (globalForbiddenBiomes != null && globalForbiddenBiomes.Contains(biome))
            return false;

        return true;
    }

    /// <summary>
    /// Calculates the maximum portals allowed based on chunk size and density settings.
    /// </summary>
    /// <param name="chunkSize">Size of the terrain chunk.</param>
    /// <returns>Maximum portals for the chunk size.</returns>
    public int CalculateMaxPortalsForChunkSize(int chunkSize)
    {
        if (!useDynamicSpawnAdjustment)
            return maxNumberOfPortals;

        float chunkArea = chunkSize * chunkSize;
        int densityBasedMax = Mathf.RoundToInt(chunkArea * targetPortalDensity);

        return Mathf.Min(maxNumberOfPortals, Mathf.Max(1, densityBasedMax));
    }

    /// <summary>
    /// Gets debug information about current portal settings.
    /// </summary>
    /// <returns>Formatted debug string.</returns>
    public string GetDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Portal Settings Debug Info ===");
        sb.AppendLine($"Max Portals: {maxNumberOfPortals}");
        sb.AppendLine($"Prefab Count: {(prefabs != null ? prefabs.Count : 0)}");
        sb.AppendLine($"Min Distance: {minDistanceBetweenPortals}");
        sb.AppendLine($"Max Attempts: {maxSpawnAttempts}");
        sb.AppendLine($"Center Spawning: {preferCenterSpawning}");
        sb.AppendLine($"Height Restrictions: {useHeightRestrictions} ({minSpawnHeight}-{maxSpawnHeight})");
        sb.AppendLine($"Biome Restrictions: {useBiomeRestrictions}");
        sb.AppendLine($"Weighted Selection: {useWeightedSelection}");
        sb.AppendLine($"Dynamic Adjustment: {useDynamicSpawnAdjustment}");
        sb.AppendLine($"Target Density: {targetPortalDensity}");
        sb.AppendLine($"Player Proximity: {enablePlayerProximityInfluence} ({minDistanceFromPlayer}-{maxDistanceFromPlayer})");

        return sb.ToString();
    }

    /// <summary>
    /// Resets all settings to safe default values.
    /// </summary>
    [ContextMenu("Reset to Default Values")]
    public void ResetToDefaults()
    {
        maxNumberOfPortals = 3;
        minDistanceBetweenPortals = 25f;
        maxSpawnAttempts = 10;
        preferCenterSpawning = true;
        edgeAvoidanceDistance = 15f;
        useHeightRestrictions = true;
        minSpawnHeight = 0f;
        maxSpawnHeight = 100f;
        useBiomeRestrictions = true;
        useWeightedSelection = false;
        useEnvironmentalFactors = true;
        rarityFavorBias = 1f;
        maxConsecutiveFailures = 8;
        failureRecoveryTime = 45f;
        enablePerformanceLogging = false;
        enableSpawnDebugVisualization = false;
        useDynamicSpawnAdjustment = true;
        targetPortalDensity = 0.002f;
        densityAdjustmentSpeed = 1f;
        enableTimeBasedSpawning = false;
        enableWeatherBasedSpawning = false;
        enablePlayerProximityInfluence = true;
        minDistanceFromPlayer = 50f;
        maxDistanceFromPlayer = 300f;

        if (globalForbiddenBiomes == null)
            globalForbiddenBiomes = new List<Biome>();
        else
            globalForbiddenBiomes.Clear();
    }
}