using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  configuration settings for managing spawnable mobs with advanced pack behavior,
/// environmental awareness, territorial dynamics, and natural distribution patterns.
/// </summary>
[System.Serializable]
public class MobSettings : BaseSettings
{
    [Header("Mob Prefab Configuration")]
    /// <summary>
    /// List of spawnable mob prefabs with their individual configurations.
    /// </summary>
    [Tooltip("List of spawnable mob prefabs with individual configurations.")]
    public List<SpawnableMob> prefabs;

    /// <summary>
    /// The maximum number of mobs allowed to spawn simultaneously.
    /// </summary>
    [Tooltip("Maximum number of mobs allowed to spawn simultaneously.")]
    public int maxNumberOfMobs;

    [Header("Pack Behavior and Social Dynamics")]
    /// <summary>
    /// Enable pack spawning behavior for social mobs.
    /// </summary>
    [Tooltip("Enable pack spawning behavior for social mobs.")]
    public bool enablePackSpawning = true;

    /// <summary>
    /// Global chance modifier for pack spawning (multiplied with individual mob settings).
    /// </summary>
    [Tooltip("Global pack spawning chance modifier.")]
    [Range(0f, 2f)]
    public float globalPackSpawnChanceModifier = 1f;

    /// <summary>
    /// Maximum distance for pack spawning - mobs of same type spawn within this range.
    /// </summary>
    [Tooltip("Maximum distance for pack member spawning.")]
    [Range(5f, 50f)]
    public float packSpawnRadius = 15f;

    /// <summary>
    /// Minimum pack size when pack spawning occurs.
    /// </summary>
    [Tooltip("Minimum pack size when pack spawning occurs.")]
    [Range(2, 8)]
    public int minPackSize = 2;

    /// <summary>
    /// Maximum pack size when pack spawning occurs.
    /// </summary>
    [Tooltip("Maximum pack size when pack spawning occurs.")]
    [Range(2, 12)]
    public int maxPackSize = 4;

    // UNIMPLEMENTED: Mixed-species pack spawning not implemented in MobSpawner
    /// <summary>
    /// Allow mixed-species packs for compatible mob types.
    /// </summary>
    [Tooltip("Allow mixed-species packs for compatible mob types.")]
    public bool allowMixedSpeciesPacks = false;

    [Header("Territorial and Distance Management")]
    /// <summary>
    /// Minimum distance between individual mob spawns to prevent overcrowding.
    /// </summary>
    [Tooltip("Minimum distance between individual mob spawns.")]
    [Range(2f, 25f)]
    public float minDistanceBetweenMobs = 5f;

    // UNIMPLEMENTED: Territorial behavior system not implemented in MobSpawner
    /// <summary>
    /// Enable territorial behavior calculations for applicable mobs.
    /// </summary>
    [Tooltip("Enable territorial behavior calculations.")]
    public bool enableTerritorialBehavior = true;

    // UNIMPLEMENTED: Territorial distance modifier not applied in spawn logic
    /// <summary>
    /// Global territorial distance modifier (multiplied with individual mob settings).
    /// </summary>
    [Tooltip("Global territorial distance modifier.")]
    [Range(0.5f, 2f)]
    public float territorialDistanceModifier = 1f;

    // UNIMPLEMENTED: Inter-species conflicts not implemented
    /// <summary>
    /// Enable inter-species territorial conflicts (some species avoid others).
    /// </summary>
    [Tooltip("Enable inter-species territorial conflicts.")]
    public bool enableInterSpeciesConflicts = true;

    [Header("Environmental and Biome Awareness")]
    /// <summary>
    /// Enable biome-specific spawn rate modifiers.
    /// </summary>
    [Tooltip("Enable biome-specific spawn rate modifiers.")]
    public bool useBiomeSpawnModifiers = true;

    /// <summary>
    /// Global spawn rate multiplier for preferred biomes.
    /// </summary>
    [Tooltip("Global spawn rate multiplier for preferred biomes.")]
    [Range(1f, 3f)]
    public float preferredBiomeMultiplier = 1.5f;

    /// <summary>
    /// Global spawn rate multiplier for non-preferred biomes.
    /// </summary>
    [Tooltip("Global spawn rate multiplier for non-preferred biomes.")]
    [Range(0.1f, 1f)]
    public float nonPreferredBiomeMultiplier = 0.5f;

    /// <summary>
    /// Global biomes where no mobs should spawn (overrides individual mob settings).
    /// </summary>
    [Tooltip("Global biomes where no mobs should spawn.")]
    public List<Biome> globalForbiddenBiomes;

    /// <summary>
    /// Enable height-based spawn restrictions and preferences.
    /// </summary>
    [Tooltip("Enable height-based spawn restrictions and preferences.")]
    public bool useHeightBasedSpawning = true;

    /// <summary>
    /// Global height tolerance for mob spawning preferences.
    /// </summary>
    [Tooltip("Global height tolerance for mob spawning preferences.")]
    [Range(0f, 50f)]
    public float globalHeightTolerance = 10f;

    [Header("Spawn Distribution and Positioning")]
    /// <summary>
    /// Maximum attempts to find a valid spawn position before giving up.
    /// </summary>
    [Tooltip("Maximum attempts to find a valid spawn position.")]
    [Range(5, 50)]
    public int maxSpawnAttempts = 15;

    /// <summary>
    /// Prefer spawning in areas away from chunk edges.
    /// </summary>
    [Tooltip("Prefer spawning away from chunk edges.")]
    public bool avoidChunkEdges = true;

    /// <summary>
    /// Distance from chunk edges to avoid when edge avoidance is enabled.
    /// </summary>
    [Tooltip("Distance from chunk edges to avoid.")]
    [Range(0f, 30f)]
    public float edgeAvoidanceDistance = 10f;

    /// <summary>
    /// NavMesh sample distance for position validation.
    /// </summary>
    [Tooltip("NavMesh sample distance for position validation.")]
    [Range(0.1f, 10f)]
    public float navMeshSampleDistance = 2f;

    // UNIMPLEMENTED: Natural clustering patterns not implemented in spawn distribution
    /// <summary>
    /// Enable natural clustering patterns for mob distribution.
    /// </summary>
    [Tooltip("Enable natural clustering patterns for mob distribution.")]
    public bool enableNaturalClustering = true;

    // UNIMPLEMENTED: Clustering strength not applied
    /// <summary>
    /// Strength of clustering behavior (higher = more clustered).
    /// </summary>
    [Tooltip("Strength of clustering behavior.")]
    [Range(0.1f, 2f)]
    public float clusteringStrength = 1f;

    [Header("Time-Based and Activity Patterns")]
    // UNIMPLEMENTED: Day/night cycle not implemented in MobSpawner
    /// <summary>
    /// Enable day/night cycle influence on mob spawning.
    /// </summary>
    [Tooltip("Enable day/night cycle influence on spawning.")]
    public bool enableTimeBasedSpawning = false;

    // UNIMPLEMENTED: Time of day not used in spawn calculations
    /// <summary>
    /// Current time of day for spawning calculations (0-24).
    /// </summary>
    [Tooltip("Current time of day for spawning calculations (0-24).")]
    [Range(0f, 24f)]
    public float currentTimeOfDay = 12f;

    // UNIMPLEMENTED: Time influence not applied to spawn rates
    /// <summary>
    /// How strongly time affects spawn rates (higher = more effect).
    /// </summary>
    [Tooltip("How strongly time affects spawn rates.")]
    [Range(0.1f, 2f)]
    public float timeInfluenceStrength = 1f;

    [Header("Performance and Error Handling")]
    /// <summary>
    /// Maximum number of consecutive spawn failures before temporarily disabling mob spawning.
    /// </summary>
    [Tooltip("Max consecutive failures before temporarily disabling spawning.")]
    [Range(5, 30)]
    public int maxConsecutiveFailures = 12;

    /// <summary>
    /// Time to wait after max failures before re-enabling mob spawn attempts.
    /// </summary>
    [Tooltip("Time to wait after max failures before re-enabling spawning.")]
    [Range(15f, 180f)]
    public float failureRecoveryTime = 60f;

    /// <summary>
    /// Enable performance monitoring and detailed logging for mob spawning operations.
    /// </summary>
    [Tooltip("Enable performance monitoring and detailed logging.")]
    public bool enablePerformanceLogging = false;

    // UNIMPLEMENTED: Max spawn processing time not enforced in spawner
    /// <summary>
    /// Maximum processing time allowed for a single spawn cycle (in seconds).
    /// </summary>
    [Tooltip("Maximum processing time for a single spawn cycle.")]
    [Range(0.01f, 1f)]
    public float maxSpawnProcessingTime = 0.1f;

    [Header("Dynamic Population Management")]
    // UNIMPLEMENTED: Dynamic population control not implemented in MobSpawner
    /// <summary>
    /// Dynamically adjust spawn rates based on current mob density in the area.
    /// </summary>
    [Tooltip("Dynamically adjust spawn rates based on current density.")]
    public bool useDynamicPopulationControl = true;

    // UNIMPLEMENTED: Target density not used in spawn logic
    /// <summary>
    /// Target mob density per chunk area (mobs per square unit).
    /// </summary>
    [Tooltip("Target mob density per chunk area.")]
    [Range(0.0001f, 0.1f)]
    public float targetMobDensity = 0.01f;

    // UNIMPLEMENTED: Population adjustment not implemented
    /// <summary>
    /// How quickly to adjust spawn rates when population is off-target.
    /// </summary>
    [Tooltip("How quickly to adjust spawn rates when population is off-target.")]
    [Range(0.1f, 3f)]
    public float populationAdjustmentSpeed = 1.5f;

    // UNIMPLEMENTED: Seasonal variations not implemented
    /// <summary>
    /// Enable seasonal population variations (future expansion).
    /// </summary>
    [Tooltip("Enable seasonal population variations (future expansion).")]
    public bool enableSeasonalVariations = false;

    [Header("Player Interaction and Safety")]
    /// <summary>
    /// Enable player proximity influence on mob spawning.
    /// </summary>
    [Tooltip("Enable player proximity influence on spawning.")]
    public bool enablePlayerProximityInfluence = true;

    /// <summary>
    /// Minimum distance from player before mobs can spawn.
    /// </summary>
    [Tooltip("Minimum distance from player before mobs can spawn.")]
    [Range(10f, 200f)]
    public float minDistanceFromPlayer = 30f;

    /// <summary>
    /// Maximum distance from player where mobs will spawn (0 = unlimited).
    /// </summary>
    [Tooltip("Maximum distance from player for spawning (0 = unlimited).")]
    [Range(0f, 1000f)]
    public float maxDistanceFromPlayer = 500f;

    // UNIMPLEMENTED: Safe zones not implemented in MobSpawner
    /// <summary>
    /// Enable safe zone creation around player spawn points and bases.
    /// </summary>
    [Tooltip("Enable safe zones around player spawn points and bases.")]
    public bool enableSafeZones = true;

    // UNIMPLEMENTED: Safe zone radius not checked in spawn logic
    /// <summary>
    /// Radius of safe zones where aggressive mobs won't spawn.
    /// </summary>
    [Tooltip("Radius of safe zones where aggressive mobs won't spawn.")]
    [Range(20f, 200f)]
    public float safeZoneRadius = 100f;

    [Header("Advanced Spawn Algorithms")]
    // UNIMPLEMENTED: Advanced weighted selection not implemented
    /// <summary>
    /// Use advanced weighted selection considering all environmental factors.
    /// </summary>
    [Tooltip("Use advanced weighted selection with environmental factors.")]
    public bool useAdvancedWeightedSelection = true;

    // UNIMPLEMENTED: Spawn prediction not implemented
    /// <summary>
    /// Enable spawn prediction to prevent overcrowding before it happens.
    /// </summary>
    [Tooltip("Enable spawn prediction to prevent overcrowding.")]
    public bool enableSpawnPrediction = true;

    // UNIMPLEMENTED: Ecosystem balance only used in SpawnerManager, not actual spawning
    /// <summary>
    /// Enable ecosystem balance calculations (predator/prey ratios).
    /// </summary>
    [Tooltip("Enable ecosystem balance calculations.")]
    public bool enableEcosystemBalance = false;

    /// <summary>
    /// Validates all mob settings and logs warnings for invalid configurations.
    /// </summary>
    public void ValidateSettings()
    {
        // Validate basic settings
        maxNumberOfMobs = Mathf.Max(0, maxNumberOfMobs);
        minDistanceBetweenMobs = Mathf.Max(1f, minDistanceBetweenMobs);
        packSpawnRadius = Mathf.Max(minDistanceBetweenMobs, packSpawnRadius);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);

        // Validate pack settings
        minPackSize = Mathf.Max(2, minPackSize);
        maxPackSize = Mathf.Max(minPackSize, maxPackSize);
        globalPackSpawnChanceModifier = Mathf.Max(0f, globalPackSpawnChanceModifier);

        // Validate distance settings
        edgeAvoidanceDistance = Mathf.Max(0f, edgeAvoidanceDistance);
        navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        territorialDistanceModifier = Mathf.Max(0.1f, territorialDistanceModifier);

        // Validate biome and environmental settings
        preferredBiomeMultiplier = Mathf.Max(0.1f, preferredBiomeMultiplier);
        nonPreferredBiomeMultiplier = Mathf.Max(0.1f, nonPreferredBiomeMultiplier);
        globalHeightTolerance = Mathf.Max(0f, globalHeightTolerance);

        // Validate time settings
        currentTimeOfDay = Mathf.Clamp(currentTimeOfDay, 0f, 24f);
        timeInfluenceStrength = Mathf.Max(0.1f, timeInfluenceStrength);

        // Validate player proximity settings
        if (enablePlayerProximityInfluence && maxDistanceFromPlayer > 0 && minDistanceFromPlayer > maxDistanceFromPlayer)
        {
            Debug.LogWarning("MobSettings: minDistanceFromPlayer > maxDistanceFromPlayer, adjusting");
            maxDistanceFromPlayer = minDistanceFromPlayer + 100f;
        }

        // Validate population control settings
        targetMobDensity = Mathf.Max(0.0001f, targetMobDensity);
        populationAdjustmentSpeed = Mathf.Max(0.1f, populationAdjustmentSpeed);

        // Validate performance settings
        maxSpawnProcessingTime = Mathf.Max(0.01f, maxSpawnProcessingTime);
        maxConsecutiveFailures = Mathf.Max(1, maxConsecutiveFailures);
        failureRecoveryTime = Mathf.Max(1f, failureRecoveryTime);

        // Validate safe zone settings
        safeZoneRadius = Mathf.Max(minDistanceFromPlayer, safeZoneRadius);

        // Validate individual mob prefabs
        if (prefabs != null)
        {
            for (int i = prefabs.Count - 1; i >= 0; i--)
            {
                if (prefabs[i] == null)
                {
                    Debug.LogWarning($"MobSettings: Removing null mob prefab at index {i}");
                    prefabs.RemoveAt(i);
                }
                else
                {
                    prefabs[i].ValidateConfiguration();
                }
            }

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("MobSettings: No valid mob prefabs configured!");
            }
        }

        // Validate base settings
        ValidateBaseSettings();
    }

    /// <summary>
    /// Gets the effective spawn rate modifier based on current conditions.
    /// </summary>
    /// <param name="currentDensity">Current mob density in the area.</param>
    /// <param name="biome">Current biome being evaluated.</param>
    /// <param name="height">Current height being evaluated.</param>
    /// <returns>Spawn rate modifier (1.0 = normal rate).</returns>
    public float GetEffectiveSpawnRateModifier(float currentDensity, Biome biome = null, float height = 0f)
    {
        float modifier = 1f;

        // Apply dynamic population control
        if (useDynamicPopulationControl)
        {
            float densityRatio = currentDensity / targetMobDensity;
            if (densityRatio > 1f)
            {
                // Too many mobs, reduce spawn rate
                modifier *= Mathf.Lerp(1f, 0.1f, (densityRatio - 1f) * populationAdjustmentSpeed);
            }
            else if (densityRatio < 0.3f)
            {
                // Too few mobs, increase spawn rate
                modifier *= Mathf.Lerp(1f, 2f, (0.3f - densityRatio) * populationAdjustmentSpeed);
            }
        }

        // Apply biome-based modifications
        if (useBiomeSpawnModifiers && biome != null)
        {
            // This is a simplified version - individual mobs have more detailed biome preferences
            modifier *= preferredBiomeMultiplier; // Assume preferred for now
        }

        // Apply time-based modifications
        if (enableTimeBasedSpawning)
        {
            // Simple day/night cycle influence
            float timeModifier = 1f + 0.2f * Mathf.Sin((currentTimeOfDay / 24f) * 2f * Mathf.PI) * timeInfluenceStrength;
            modifier *= timeModifier;
        }

        // Apply seasonal variations (future expansion)
        if (enableSeasonalVariations)
        {
            // Placeholder for seasonal logic
            modifier *= 1f; // No change for now
        }

        return Mathf.Clamp(modifier, 0.05f, 5f);
    }

    /// <summary>
    /// Checks if a mob can spawn in the given biome based on global restrictions.
    /// </summary>
    /// <param name="biome">Biome to check.</param>
    /// <returns>True if biome allows mob spawning.</returns>
    public bool IsBiomeAllowed(Biome biome)
    {
        if (globalForbiddenBiomes != null && globalForbiddenBiomes.Contains(biome))
            return false;

        return true;
    }

    /// <summary>
    /// Calculates the maximum mobs allowed based on chunk size and density settings.
    /// </summary>
    /// <param name="chunkSize">Size of the terrain chunk.</param>
    /// <returns>Maximum mobs for the chunk size.</returns>
    public int CalculateMaxMobsForChunkSize(int chunkSize)
    {
        if (!useDynamicPopulationControl)
            return maxNumberOfMobs;

        float chunkArea = chunkSize * chunkSize;
        int densityBasedMax = Mathf.RoundToInt(chunkArea * targetMobDensity);

        return Mathf.Min(maxNumberOfMobs, Mathf.Max(1, densityBasedMax));
    }

    /// <summary>
    /// Gets the effective pack spawn chance considering global modifiers.
    /// </summary>
    /// <param name="baseMobPackChance">Base pack chance from individual mob settings.</param>
    /// <returns>Effective pack spawn chance.</returns>
    public float GetEffectivePackSpawnChance(float baseMobPackChance)
    {
        if (!enablePackSpawning) return 0f;

        return Mathf.Clamp01(baseMobPackChance * globalPackSpawnChanceModifier);
    }

    /// <summary>
    /// Checks if a position is within a safe zone.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <param name="playerPositions">List of player positions to check against.</param>
    /// <returns>True if position is within a safe zone.</returns>
    public bool IsPositionInSafeZone(Vector3 position, List<Vector3> playerPositions)
    {
        if (!enableSafeZones || playerPositions == null) return false;

        foreach (Vector3 playerPos in playerPositions)
        {
            if (Vector3.Distance(position, playerPos) < safeZoneRadius)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets debug information about current mob settings.
    /// </summary>
    /// <returns>Formatted debug string.</returns>
    public string GetDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Mob Settings Debug Info ===");
        sb.AppendLine($"Max Mobs: {maxNumberOfMobs}");
        sb.AppendLine($"Prefab Count: {(prefabs != null ? prefabs.Count : 0)}");
        sb.AppendLine($"Pack Spawning: {enablePackSpawning} (Size: {minPackSize}-{maxPackSize})");
        sb.AppendLine($"Min Distance: {minDistanceBetweenMobs}");
        sb.AppendLine($"Max Attempts: {maxSpawnAttempts}");
        sb.AppendLine($"Territorial Behavior: {enableTerritorialBehavior}");
        sb.AppendLine($"Biome Modifiers: {useBiomeSpawnModifiers}");
        sb.AppendLine($"Height-Based: {useHeightBasedSpawning}");
        sb.AppendLine($"Time-Based: {enableTimeBasedSpawning} (Current: {currentTimeOfDay:F1}h)");
        sb.AppendLine($"Population Control: {useDynamicPopulationControl} (Target: {targetMobDensity})");
        sb.AppendLine($"Player Proximity: {enablePlayerProximityInfluence} ({minDistanceFromPlayer}-{maxDistanceFromPlayer})");
        sb.AppendLine($"Safe Zones: {enableSafeZones} (Radius: {safeZoneRadius})");

        return sb.ToString();
    }

    /// <summary>
    /// Resets all settings to safe default values.
    /// </summary>
    [ContextMenu("Reset to Default Values")]
    public void ResetToDefaults()
    {
        maxNumberOfMobs = 10;
        enablePackSpawning = true;
        globalPackSpawnChanceModifier = 1f;
        packSpawnRadius = 15f;
        minPackSize = 2;
        maxPackSize = 4;
        allowMixedSpeciesPacks = false;
        minDistanceBetweenMobs = 5f;
        enableTerritorialBehavior = true;
        territorialDistanceModifier = 1f;
        enableInterSpeciesConflicts = true;
        useBiomeSpawnModifiers = true;
        preferredBiomeMultiplier = 1.5f;
        nonPreferredBiomeMultiplier = 0.5f;
        useHeightBasedSpawning = true;
        globalHeightTolerance = 10f;
        maxSpawnAttempts = 15;
        avoidChunkEdges = true;
        edgeAvoidanceDistance = 10f;
        navMeshSampleDistance = 2f;
        enableNaturalClustering = true;
        clusteringStrength = 1f;
        enableTimeBasedSpawning = false;
        currentTimeOfDay = 12f;
        timeInfluenceStrength = 1f;
        maxConsecutiveFailures = 12;
        failureRecoveryTime = 60f;
        enablePerformanceLogging = false;
        maxSpawnProcessingTime = 0.1f;
        useDynamicPopulationControl = true;
        targetMobDensity = 0.01f;
        populationAdjustmentSpeed = 1.5f;
        enableSeasonalVariations = false;
        enablePlayerProximityInfluence = true;
        minDistanceFromPlayer = 30f;
        maxDistanceFromPlayer = 500f;
        enableSafeZones = true;
        safeZoneRadius = 100f;
        useAdvancedWeightedSelection = true;
        enableSpawnPrediction = true;
        enableEcosystemBalance = false;

        if (globalForbiddenBiomes == null)
            globalForbiddenBiomes = new List<Biome>();
        else
            globalForbiddenBiomes.Clear();
    }
}