using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a spawnable mob that can be managed by a spawner.
///  with natural behavior patterns, pack dynamics, environmental preferences,
/// and sophisticated spawn configuration options.
/// </summary>
[System.Serializable]
public class SpawnableMob : ISpawbleBySpawner
{
    [Header("Mob Prefab")]
    /// <summary>
    /// Prefab of the mob to spawn.
    /// </summary>
    [Tooltip("Prefab of the mob to spawn.")]
    public GameObject mobPrefab;

    [Header("Instance Limits")]
    /// <summary>
    /// Maximum number of mob instances allowed.
    /// </summary>
    [Tooltip("Maximum number of mob instances allowed.")]
    public int maxInstances;

    /// <summary>
    /// Current number of mob instances that have been spawned.
    /// Hidden in the inspector to avoid unintended modifications.
    /// </summary>
    [HideInInspector]
    [Tooltip("Current number of mob instances that have been spawned.")]
    public int currentInstances;

    [Header("Biome and Environmental Preferences")]
    /// <summary>
    /// List of allowed biomes where this mob can spawn.
    /// </summary>
    [Tooltip("List of allowed biomes where this mob can spawn.")]
    public List<Biome> allowedBiomes;

    // UNIMPLEMENTED: Preferred biomes not used in spawn weight calculations
    /// <summary>
    /// Preferred biomes where this mob spawns more frequently.
    /// </summary>
    [Tooltip("Preferred biomes where this mob spawns more frequently.")]
    public List<Biome> preferredBiomes;

    // UNIMPLEMENTED: Avoided biomes not checked in spawn logic
    /// <summary>
    /// Biomes where this mob actively avoids spawning.
    /// </summary>
    [Tooltip("Biomes where this mob actively avoids spawning.")]
    public List<Biome> avoidedBiomes;

    [Header("Height Preferences")]
    /// <summary>
    /// Minimum preferred height for spawning.
    /// </summary>
    [Tooltip("Minimum preferred height for spawning.")]
    public float minPreferredHeight = 0f;

    /// <summary>
    /// Maximum preferred height for spawning.
    /// </summary>
    [Tooltip("Maximum preferred height for spawning.")]
    public float maxPreferredHeight = 100f;

    // UNIMPLEMENTED: Optimal height not used (only basic min/max checked)
    /// <summary>
    /// Optimal height where this mob prefers to spawn.
    /// </summary>
    [Tooltip("Optimal height where this mob prefers to spawn.")]
    public float optimalHeight = 50f;

    // UNIMPLEMENTED: Height preference strength not applied in spawn logic
    /// <summary>
    /// How strictly to enforce height preferences (0 = loose, 1 = strict).
    /// </summary>
    [Tooltip("How strictly to enforce height preferences. 0 = loose, 1 = strict.")]
    [Range(0f, 1f)]
    public float heightPreferenceStrength = 0.7f;

    [Header("Spawn Weighting and Rarity")]
    /// <summary>
    /// Weight for random spawning of the mob. Higher values mean more frequent spawning.
    /// </summary>
    [Tooltip("Weight for random spawning. Higher values = more frequent spawning.")]
    public float spawnWeight = 1f;

    /// <summary>
    /// Rarity modifier affecting overall spawn frequency.
    /// </summary>
    [Tooltip("Rarity level. Higher values = rarer mobs.")]
    [Range(1, 10)]
    public int rarityLevel = 5;

    // UNIMPLEMENTED: Seasonal modifier not applied in spawn calculations
    /// <summary>
    /// Seasonal spawn modifier (future expansion for day/night or weather systems).
    /// </summary>
    [Tooltip("Modifier for seasonal or time-based spawning.")]
    [Range(0.1f, 2f)]
    public float seasonalModifier = 1f;

    [Header("Spawn Timing")]
    /// <summary>
    /// Fixed spawn interval for the mob.
    /// </summary>
    [Tooltip("Fixed spawn interval for the mob.")]
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

    [Header("Social Behavior and Pack Dynamics")]
    /// <summary>
    /// Whether this mob type exhibits pack behavior.
    /// </summary>
    [Tooltip("Does this mob exhibit pack/group behavior?")]
    public bool isPackAnimal = false;

    /// <summary>
    /// Preferred pack size when spawning as a group.
    /// </summary>
    [Tooltip("Preferred pack size when spawning as a group.")]
    [Range(2, 10)]
    public int preferredPackSize = 3;

    /// <summary>
    /// Chance for pack spawning when this mob is selected.
    /// </summary>
    [Tooltip("Chance for pack spawning when this mob is selected.")]
    [Range(0f, 1f)]
    public float packSpawnChance = 0.4f;

    /// <summary>
    /// Maximum distance pack members can be from pack center.
    /// </summary>
    [Tooltip("Maximum distance pack members can be from pack center.")]
    public float packSpreadRadius = 12f;

    // UNIMPLEMENTED: Territorial behavior not implemented in spawner
    /// <summary>
    /// Whether this mob is territorial and avoids spawning near other mobs.
    /// </summary>
    [Tooltip("Is this mob territorial and avoids other mobs?")]
    public bool isTerritorial = false;

    // UNIMPLEMENTED: Territorial distance not enforced in spawn logic
    /// <summary>
    /// Minimum distance to maintain from other mobs if territorial.
    /// </summary>
    [Tooltip("Minimum distance to maintain from other mobs if territorial.")]
    public float territorialDistance = 25f;

    [Header("Activity Patterns")]
    // UNIMPLEMENTED: Diurnal activity not used in spawn timing
    /// <summary>
    /// Whether this mob is primarily active during day time.
    /// </summary>
    [Tooltip("Is this mob primarily active during day time?")]
    public bool isDiurnal = true;

    // UNIMPLEMENTED: Nocturnal activity not used in spawn timing
    /// <summary>
    /// Whether this mob is primarily active during night time.
    /// </summary>
    [Tooltip("Is this mob primarily active during night time?")]
    public bool isNocturnal = false;

    // UNIMPLEMENTED: Time-based activity modifier not applied
    /// <summary>
    /// Activity level modifier based on time of day (future expansion).
    /// </summary>
    [Tooltip("Activity level modifier based on time of day.")]
    [Range(0.1f, 2f)]
    public float timeBasedActivityModifier = 1f;

    [Header("Environmental Interactions")]
    // UNIMPLEMENTED: Water preference not implemented
    /// <summary>
    /// Preferred distance from water sources (negative = seeks water, positive = avoids water).
    /// </summary>
    [Tooltip("Preferred distance from water. Negative = seeks water, positive = avoids.")]
    public float waterPreference = 0f;

    // UNIMPLEMENTED: Biome center preference not used (BiomeObject has similar but different)
    /// <summary>
    /// How much this mob prefers edge vs center areas of biomes.
    /// </summary>
    [Tooltip("Preference for biome edges vs centers. 0 = edge, 1 = center.")]
    [Range(0f, 1f)]
    public float biomeCenterPreference = 0.5f;

    // UNIMPLEMENTED: Max spawn slope not enforced in position validation
    /// <summary>
    /// Maximum slope this mob can spawn on (in degrees).
    /// </summary>
    [Tooltip("Maximum slope this mob can spawn on (in degrees).")]
    [Range(0f, 90f)]
    public float maxSpawnSlope = 45f;

    [Header("Spawn Restrictions")]
    /// <summary>
    /// Minimum distance to maintain from player spawn locations.
    /// </summary>
    [Tooltip("Minimum distance to maintain from player spawn locations.")]
    public float minDistanceFromPlayer = 50f;

    // UNIMPLEMENTED: Structure spawning check not implemented
    /// <summary>
    /// Whether this mob can spawn near structures or settlements.
    /// </summary>
    [Tooltip("Can this mob spawn near structures or settlements?")]
    public bool canSpawnNearStructures = true;

    // UNIMPLEMENTED: Special conditions not implemented
    /// <summary>
    /// Special conditions required for spawning (future expansion).
    /// </summary>
    [Tooltip("Special conditions required for spawning.")]
    public bool requiresSpecialConditions = false;

    /// <summary>
    /// Gets or sets the maximum number of mob instances allowed.
    /// </summary>
    public int MaxInstances
    {
        get => maxInstances;
        set => maxInstances = value;
    }

    /// <summary>
    /// Gets or sets the current number of mob instances that have been spawned.
    /// </summary>
    public int CurrentInstances
    {
        get => currentInstances;
        set => currentInstances = value;
    }

    /// <summary>
    /// Calculates the effective spawn weight based on environmental factors.
    /// </summary>
    /// <param name="biome">Current biome being evaluated.</param>
    /// <param name="height">Current height being evaluated.</param>
    /// <param name="isDay">Whether it's currently day time.</param>
    /// <returns>Effective spawn weight for the given conditions.</returns>
    public float CalculateEffectiveSpawnWeight(Biome biome, float height, bool isDay = true)
    {
        float effectiveWeight = spawnWeight;

        // Apply rarity modifier
        effectiveWeight *= (11f - rarityLevel) / 10f;

        // Apply biome preference modifiers
        effectiveWeight *= CalculateBiomePreferenceFactor(biome);

        // Apply height preference modifier
        effectiveWeight *= CalculateHeightPreferenceFactor(height);

        // Apply time-based activity modifier
        effectiveWeight *= CalculateTimeBasedModifier(isDay);

        // Apply seasonal modifier
        effectiveWeight *= seasonalModifier;

        return Mathf.Max(0.01f, effectiveWeight);
    }

    /// <summary>
    /// Calculates the biome preference factor for spawn weighting.
    /// </summary>
    /// <param name="biome">Biome to evaluate.</param>
    /// <returns>Biome preference factor.</returns>
    private float CalculateBiomePreferenceFactor(Biome biome)
    {
        // Check if biome is in avoided list
        if (avoidedBiomes != null && avoidedBiomes.Contains(biome))
        {
            return 0.1f; // Heavily penalize avoided biomes
        }

        // Check if biome is not in allowed list
        if (allowedBiomes != null && allowedBiomes.Count > 0 && !allowedBiomes.Contains(biome))
        {
            return 0f; // Cannot spawn in disallowed biomes
        }

        // Check if biome is in preferred list
        if (preferredBiomes != null && preferredBiomes.Contains(biome))
        {
            return 1.5f; // Bonus for preferred biomes
        }

        // Default factor for allowed but not preferred biomes
        return 1f;
    }

    /// <summary>
    /// Calculates how well the given height matches this mob's preferences.
    /// </summary>
    /// <param name="height">Height to evaluate.</param>
    /// <returns>Height preference factor.</returns>
    private float CalculateHeightPreferenceFactor(float height)
    {
        if (height < minPreferredHeight || height > maxPreferredHeight)
        {
            // Outside preferred range
            float penalty = 1f - heightPreferenceStrength;
            return Mathf.Max(0.1f, penalty);
        }
        else
        {
            // Within preferred range - calculate bonus based on proximity to optimal height
            float distanceFromOptimal = Mathf.Abs(height - optimalHeight);
            float range = maxPreferredHeight - minPreferredHeight;

            if (range > 0)
            {
                float normalizedDistance = distanceFromOptimal / (range * 0.5f);
                float optimalBonus = 1f + (1f - Mathf.Min(1f, normalizedDistance)) * heightPreferenceStrength;
                return optimalBonus;
            }

            return 1f + heightPreferenceStrength;
        }
    }

    /// <summary>
    /// Calculates time-based activity modifier.
    /// </summary>
    /// <param name="isDay">Whether it's currently day time.</param>
    /// <returns>Time-based modifier.</returns>
    private float CalculateTimeBasedModifier(bool isDay)
    {
        float modifier = 1f;

        if (isDiurnal && isDay)
        {
            modifier *= timeBasedActivityModifier;
        }
        else if (isNocturnal && !isDay)
        {
            modifier *= timeBasedActivityModifier;
        }
        else if ((isDiurnal && !isDay) || (isNocturnal && isDay))
        {
            modifier *= (2f - timeBasedActivityModifier); // Reduced activity during non-preferred time
        }

        return modifier;
    }

    /// <summary>
    /// Checks if this mob can spawn in the given biome.
    /// </summary>
    /// <param name="biome">Biome to check.</param>
    /// <returns>True if mob can spawn in this biome.</returns>
    public bool CanSpawnInBiome(Biome biome)
    {
        // Check avoided biomes first
        if (avoidedBiomes != null && avoidedBiomes.Contains(biome))
            return false;

        // If allowed biomes is specified, must be in the list
        if (allowedBiomes != null && allowedBiomes.Count > 0)
            return allowedBiomes.Contains(biome);

        // If no specific allowed biomes, can spawn anywhere not avoided
        return true;
    }

    /// <summary>
    /// Checks if this mob meets height requirements for spawning.
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
            float tolerance = (maxPreferredHeight - minPreferredHeight) * (1f - heightPreferenceStrength) * 0.3f;
            return height >= (minPreferredHeight - tolerance) && height <= (maxPreferredHeight + tolerance);
        }
    }

    /// <summary>
    /// Gets the effective spawn time considering randomization and pack behavior.
    /// </summary>
    /// <param name="isPackSpawn">Whether this is part of a pack spawn.</param>
    /// <returns>Calculated spawn time.</returns>
    public float GetEffectiveSpawnTime(bool isPackSpawn = false)
    {
        float baseTime;

        if (shouldHaveRandomSpawnTime)
        {
            baseTime = Random.Range(minSpawnTime, maxSpawnTime);
        }
        else
        {
            baseTime = spawnTime;
        }

        // Pack spawns might have different timing
        if (isPackSpawn && isPackAnimal)
        {
            baseTime *= 1.5f; // Slightly longer cooldown after pack spawns
        }

        return baseTime;
    }

    /// <summary>
    /// Determines if this mob should spawn as a pack based on its configuration and random chance.
    /// </summary>
    /// <returns>True if should spawn as pack.</returns>
    public bool ShouldSpawnAsPack()
    {
        return isPackAnimal && Random.value < packSpawnChance;
    }

    /// <summary>
    /// Gets the actual pack size for spawning, considering available instances.
    /// </summary>
    /// <param name="availableSlots">Number of available spawn slots.</param>
    /// <returns>Actual pack size to spawn.</returns>
    public int GetActualPackSize(int availableSlots)
    {
        if (!isPackAnimal) return 1;

        int desiredPackSize = Random.Range(2, preferredPackSize + 1);
        return Mathf.Min(desiredPackSize, availableSlots, maxInstances - currentInstances);
    }

    /// <summary>
    /// Validates the mob configuration and logs warnings for invalid settings.
    /// </summary>
    public void ValidateConfiguration()
    {
        if (mobPrefab == null)
        {
            Debug.LogWarning("SpawnableMob has null mobPrefab!");
        }

        if (maxInstances <= 0)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} has invalid maxInstances: {maxInstances}");
        }

        if (allowedBiomes != null && allowedBiomes.Count == 0)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} has empty allowedBiomes list - will not spawn!");
        }

        if (shouldHaveRandomSpawnTime && minSpawnTime > maxSpawnTime)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} has minSpawnTime > maxSpawnTime");
        }

        if (minPreferredHeight > maxPreferredHeight)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} has minPreferredHeight > maxPreferredHeight");
        }

        if (spawnWeight <= 0)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} has invalid spawnWeight: {spawnWeight}");
        }

        if (isPackAnimal && preferredPackSize < 2)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} is pack animal but preferredPackSize < 2");
        }

        if (isDiurnal && isNocturnal)
        {
            Debug.LogWarning($"SpawnableMob {(mobPrefab ? mobPrefab.name : "Unknown")} is both diurnal and nocturnal - using diurnal");
            isNocturnal = false;
        }
    }
}