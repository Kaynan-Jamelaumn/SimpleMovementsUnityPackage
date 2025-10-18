using UnityEngine;

/// <summary>
/// Represents a spawnable portal that can be managed by a spawner.
///  with natural spawning configuration, rarity system, and environmental preferences.
/// </summary>
[System.Serializable]
public class SpawnablePortal : ISpawbleBySpawner
{
    [Header("Portal Prefab")]
    /// <summary>
    /// Prefab of the portal to spawn.
    /// </summary>
    [Tooltip("Prefab of the portal to spawn.")]
    public GameObject prefab;

    [Header("Instance Limits")]
    /// <summary>
    /// Maximum number of portal instances allowed.
    /// </summary>
    [Tooltip("Maximum number of portal instances allowed.")]
    public int maxInstances;

    /// <summary>
    /// Current number of portal instances that have been spawned.
    /// Hidden in the inspector to avoid unintended modifications.
    /// </summary>
    [HideInInspector]
    [Tooltip("Current number of portal instances that have been spawned.")]
    public int currentInstances;

    [Header("Spawn Timing")]
    /// <summary>
    /// Fixed spawn time for the portal.
    /// </summary>
    [Tooltip("Time to spawn the portal.")]
    public float spawnTime;

    /// <summary>
    /// Minimum spawn time for random spawn time variation.
    /// </summary>
    [Tooltip("Minimum spawn time for random spawn time variation.")]
    public float minSpawnTime;

    /// <summary>
    /// Maximum spawn time for random spawn time variation.
    /// </summary>
    [Tooltip("Maximum spawn time for random spawn time variation.")]
    public float maxSpawnTime;

    /// <summary>
    /// Determines whether the spawn time should be randomized.
    /// </summary>
    [Tooltip("Should the spawn time be randomized?")]
    public bool shouldHaveRandomSpawnTime;

    [Header("Portal Rarity and Weighting")]
    /// <summary>
    /// Rarity level of the portal affecting spawn frequency (higher = rarer).
    /// </summary>
    [Tooltip("Rarity level affecting spawn frequency. Higher values = rarer portals.")]
    [Range(1, 10)]
    public int rarityLevel = 5;

    // UNIMPLEMENTED: Base spawn weight has CalculateEffectiveSpawnWeight() method but it's never called
    /// <summary>
    /// Base spawn weight for weighted selection. Higher = more likely to spawn.
    /// </summary>
    [Tooltip("Base spawn weight for selection. Higher values = more likely to spawn.")]
    [Range(0.1f, 10f)]
    public float baseSpawnWeight = 1f;

    [Header("Environmental Preferences")]
    // UNIMPLEMENTED: Preferred biomes not used in PortalSpawner selection logic
    /// <summary>
    /// Preferred biomes for this portal type. Empty list means can spawn in any biome.
    /// </summary>
    [Tooltip("Preferred biomes for this portal. Empty list = can spawn anywhere.")]
    public Biome[] preferredBiomes;

    // UNIMPLEMENTED: Height preferences not used in PortalSpawner
    /// <summary>
    /// Minimum height preference for portal spawning.
    /// </summary>
    [Tooltip("Minimum preferred height for portal spawning.")]
    public float minPreferredHeight = 0f;

    // UNIMPLEMENTED: Height preferences not used in PortalSpawner
    /// <summary>
    /// Maximum height preference for portal spawning.
    /// </summary>
    [Tooltip("Maximum preferred height for portal spawning.")]
    public float maxPreferredHeight = 100f;

    // UNIMPLEMENTED: Height preference strength not applied
    /// <summary>
    /// How strictly to enforce height preferences (0 = loose, 1 = strict).
    /// </summary>
    [Tooltip("How strictly to enforce height preferences. 0 = loose, 1 = strict.")]
    [Range(0f, 1f)]
    public float heightPreferenceStrength = 0.5f;

    [Header("Spacing and Distribution")]
    // UNIMPLEMENTED: Local preferCenterSpawning - PortalSpawner uses its own field
    /// <summary>
    /// Prefer spawning away from chunk edges for more natural placement.
    /// </summary>
    [Tooltip("Prefer spawning away from chunk edges for more natural placement.")]
    public bool preferCenterSpawning = true;

    // UNIMPLEMENTED: Per-portal distance not enforced - global setting used
    /// <summary>
    /// Minimum distance this portal should maintain from other portals.
    /// </summary>
    [Tooltip("Minimum distance to maintain from other portals.")]
    public float minDistanceFromOtherPortals = 30f;

    // UNIMPLEMENTED: Coexistence check not implemented
    /// <summary>
    /// Whether this portal type can coexist with other portal types nearby.
    /// </summary>
    [Tooltip("Can this portal spawn near different portal types?")]
    public bool canCoexistWithOtherTypes = true;

    [Header("Special Behaviors")]
    // UNIMPLEMENTED: Special conditions not checked in spawn logic
    /// <summary>
    /// Portal appears only during certain conditions (future expansion).
    /// </summary>
    [Tooltip("Special conditions required for this portal to spawn.")]
    public bool requiresSpecialConditions = false;

    // UNIMPLEMENTED: Spawn priority not used in selection algorithm
    /// <summary>
    /// Priority level for spawning when multiple portals compete for positions.
    /// </summary>
    [Tooltip("Priority level when multiple portals compete (higher = more priority).")]
    [Range(1, 10)]
    public int spawnPriority = 5;

    /// <summary>
    /// Gets or sets the maximum number of portal instances allowed.
    /// </summary>
    public int MaxInstances
    {
        get => maxInstances;
        set => maxInstances = value;
    }

    /// <summary>
    /// Gets or sets the current number of portal instances that have been spawned.
    /// </summary>
    public int CurrentInstances
    {
        get => currentInstances;
        set => currentInstances = value;
    }

    /// <summary>
    /// Calculates the effective spawn weight based on environmental factors.
    /// NOTE: This method exists but is NEVER CALLED by PortalSpawner
    /// </summary>
    /// <param name="biome">Current biome being evaluated.</param>
    /// <param name="height">Current height being evaluated.</param>
    /// <returns>Effective spawn weight for the given conditions.</returns>
    public float CalculateEffectiveSpawnWeight(Biome biome, float height)
    {
        float effectiveWeight = baseSpawnWeight;

        // Apply rarity modifier (rarer portals have lower effective weight)
        effectiveWeight *= (11f - rarityLevel) / 10f;

        // Apply biome preference modifier
        if (preferredBiomes != null && preferredBiomes.Length > 0)
        {
            bool isPreferredBiome = System.Array.Exists(preferredBiomes, b => b == biome);
            effectiveWeight *= isPreferredBiome ? 1.5f : 0.3f;
        }

        // Apply height preference modifier
        float heightFactor = CalculateHeightPreferenceFactor(height);
        effectiveWeight *= heightFactor;

        return Mathf.Max(0.01f, effectiveWeight);
    }

    /// <summary>
    /// Calculates how well the given height matches this portal's preferences.
    /// NOTE: This method exists but is NEVER CALLED by PortalSpawner
    /// </summary>
    /// <param name="height">Height to evaluate.</param>
    /// <returns>Height preference factor (0-1).</returns>
    private float CalculateHeightPreferenceFactor(float height)
    {
        if (height < minPreferredHeight || height > maxPreferredHeight)
        {
            // Outside preferred range - apply penalty based on preference strength
            float penalty = 1f - heightPreferenceStrength;
            return Mathf.Max(0.1f, penalty);
        }
        else
        {
            // Within preferred range - bonus based on how centered it is
            float midHeight = (minPreferredHeight + maxPreferredHeight) * 0.5f;
            float range = maxPreferredHeight - minPreferredHeight;

            if (range > 0)
            {
                float distanceFromCenter = Mathf.Abs(height - midHeight);
                float normalizedDistance = distanceFromCenter / (range * 0.5f);
                float centerBonus = 1f + (1f - normalizedDistance) * heightPreferenceStrength * 0.5f;
                return centerBonus;
            }

            return 1f + heightPreferenceStrength * 0.5f;
        }
    }

    /// <summary>
    /// Checks if this portal can spawn in the given biome.
    /// NOTE: This method exists but is NEVER CALLED by PortalSpawner
    /// </summary>
    /// <param name="biome">Biome to check.</param>
    /// <returns>True if portal can spawn in this biome.</returns>
    public bool CanSpawnInBiome(Biome biome)
    {
        // If no preferred biomes specified, can spawn anywhere
        if (preferredBiomes == null || preferredBiomes.Length == 0)
            return true;

        // Check if biome is in preferred list
        return System.Array.Exists(preferredBiomes, b => b == biome);
    }

    /// <summary>
    /// Checks if this portal meets height requirements for spawning.
    /// NOTE: This method exists but is NEVER CALLED by PortalSpawner
    /// </summary>
    /// <param name="height">Height to check.</param>
    /// <param name="strict">Whether to use strict checking.</param>
    /// <returns>True if height is acceptable.</returns>
    public bool IsHeightAcceptable(float height, bool strict = false)
    {
        if (strict)
        {
            return height >= minPreferredHeight && height <= maxPreferredHeight;
        }
        else
        {
            // Allow some tolerance based on preference strength
            float tolerance = (maxPreferredHeight - minPreferredHeight) * (1f - heightPreferenceStrength) * 0.5f;
            return height >= (minPreferredHeight - tolerance) && height <= (maxPreferredHeight + tolerance);
        }
    }

    /// <summary>
    /// Gets the effective spawn time considering randomization settings.
    /// </summary>
    /// <returns>Calculated spawn time.</returns>
    public float GetEffectiveSpawnTime()
    {
        if (shouldHaveRandomSpawnTime)
        {
            return Random.Range(minSpawnTime, maxSpawnTime);
        }
        return spawnTime;
    }

    /// <summary>
    /// Validates the portal configuration and logs warnings for invalid settings.
    /// </summary>
    public void ValidateConfiguration()
    {
        if (prefab == null)
        {
            Debug.LogWarning("SpawnablePortal has null prefab!");
        }

        if (maxInstances <= 0)
        {
            Debug.LogWarning($"SpawnablePortal {(prefab ? prefab.name : "Unknown")} has invalid maxInstances: {maxInstances}");
        }

        if (shouldHaveRandomSpawnTime && minSpawnTime > maxSpawnTime)
        {
            Debug.LogWarning($"SpawnablePortal {(prefab ? prefab.name : "Unknown")} has minSpawnTime > maxSpawnTime");
        }

        if (minPreferredHeight > maxPreferredHeight)
        {
            Debug.LogWarning($"SpawnablePortal {(prefab ? prefab.name : "Unknown")} has minPreferredHeight > maxPreferredHeight");
        }

        if (baseSpawnWeight <= 0)
        {
            Debug.LogWarning($"SpawnablePortal {(prefab ? prefab.name : "Unknown")} has invalid baseSpawnWeight: {baseSpawnWeight}");
        }
    }
}