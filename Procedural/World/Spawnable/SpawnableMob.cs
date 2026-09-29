using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One kind of world mob the <see cref="MobSpawner"/> can spawn (an entry of <see cref="MobSettings.prefabs"/>):
/// its prefab, how likely and where, and its pack behaviour. Also imported by the dungeon system as encounters
/// (see ProceduralDungeon.SpawnableMobImport).
/// </summary>
[System.Serializable]
public class SpawnableMob
{
    [Header("Mob Prefab")]
    [Tooltip("REQUIRED. The mob prefab. Should have a NavMeshAgent (it is placed on the NavMesh of the agent's type) and a Mob component (so kills are counted and respawned later).")]
    public GameObject mobPrefab;

    [Header("Limits")]
    [Tooltip("At most this many of this type alive in one chunk (0 = no limit besides the chunk's population).")]
    [Min(0)] public int maxInstances = 0;

    [Header("Biomes")]
    [Tooltip("Biomes this mob may spawn in. Empty = any biome (except the global forbidden ones).")]
    public List<Biome> allowedBiomes = new List<Biome>();

    [Tooltip("Biomes where this mob is more common (see Mob Settings > Use Biome Spawn Modifiers). Empty = no preference.")]
    public List<Biome> preferredBiomes = new List<Biome>();

    [Tooltip("Biomes this mob never spawns in, even if allowed.")]
    public List<Biome> avoidedBiomes = new List<Biome>();

    [Header("Height")]
    [Tooltip("Use Min/Max Preferred Height as this mob's height band (with Mob Settings > Use Height Based Spawning; otherwise the biome's band is used).")]
    public bool limitHeight = false;

    [Tooltip("Lowest ground height for this mob (world Y), with Limit Height.")]
    public float minPreferredHeight = 0f;

    [Tooltip("Highest ground height for this mob (world Y), with Limit Height.")]
    public float maxPreferredHeight = 100f;

    [Tooltip("Steepest ground this mob spawns on (degrees); the lower of this and Mob Settings' Max Slope applies.")]
    [Range(0f, 90f)] public float maxSpawnSlope = 45f;

    [Header("Spawn Weighting and Rarity")]
    [Tooltip("Relative chance of this type: 2 = twice as likely as a type with 1.")]
    [Min(0f)] public float spawnWeight = 1f;

    [Tooltip("Rarity level: higher = rarer (5 = average). Level 10 is about 10 times rarer than level 1.")]
    [Range(1, 10)] public int rarityLevel = 5;

    [Header("Spawn Timing")]
    [Tooltip("Seconds before the chunk spawns again after spawning this mob (0 = Mob Settings' Spawn Interval).")]
    [Min(0f)] public float spawnTime;

    [Tooltip("Shortest random wait (seconds), with Should Have Random Spawn Time.")]
    [Min(0f)] public float minSpawnTime;

    [Tooltip("Longest random wait (seconds), with Should Have Random Spawn Time.")]
    [Min(0f)] public float maxSpawnTime;

    [Tooltip("Pick the wait between Min and Max Spawn Time instead of using Spawn Time.")]
    public bool shouldHaveRandomSpawnTime;

    [Header("Social Behavior and Pack Dynamics")]
    [Tooltip("This mob can spawn as a pack (with Mob Settings > Enable Pack Spawning).")]
    public bool isPackAnimal = false;

    [Tooltip("Largest pack of this type (overrides Mob Settings' Max Pack Size).")]
    [Range(2, 10)] public int preferredPackSize = 3;

    [Tooltip("Chance (0-1) that this type spawns as a pack when chosen (times Global Pack Spawn Chance Modifier).")]
    [Range(0f, 1f)] public float packSpawnChance = 0.4f;

    [Tooltip("How far pack members spawn from the pack's centre (world units, 0 = Mob Settings' Pack Spawn Radius).")]
    [Min(0f)] public float packSpreadRadius = 12f;

    [Header("Activity Patterns")]
    [Tooltip("Active by day (with Mob Settings > Enable Time Based Spawning): rarer at night. Leave both off for any time.")]
    public bool isDiurnal = false;

    [Tooltip("Active at night (with Mob Settings > Enable Time Based Spawning): rarer by day.")]
    public bool isNocturnal = false;

    [Header("Spawn Restrictions")]
    [Tooltip("This mob never spawns closer than this to the player (world units); the larger of this and Mob Settings' Min Distance From Player applies.")]
    [Min(0f)] public float minDistanceFromPlayer = 0f;

    /// <summary>True when this type may live in <paramref name="biome"/>.</summary>
    public bool CanSpawnInBiome(Biome biome)
    {
        if (biome != null && avoidedBiomes != null && avoidedBiomes.Contains(biome))
            return false;
        return allowedBiomes == null || allowedBiomes.Count == 0 || (biome != null && allowedBiomes.Contains(biome));
    }

    /// <summary>This type's weight at a spot of <paramref name="biome"/> (0 = can't spawn there).</summary>
    public float WeightAt(Biome biome, MobSettings settings, bool? isDay)
    {
        if (mobPrefab == null || !CanSpawnInBiome(biome))
            return 0f;
        float weight = Mathf.Max(0f, spawnWeight) * (11f - Mathf.Clamp(rarityLevel, 1, 10)) / 10f;
        if (settings != null && settings.useBiomeSpawnModifiers && preferredBiomes != null && preferredBiomes.Count > 0)
            weight *= biome != null && preferredBiomes.Contains(biome) ? settings.preferredBiomeMultiplier : settings.nonPreferredBiomeMultiplier;
        if (settings != null && settings.enableTimeBasedSpawning && isDay.HasValue && (isDiurnal || isNocturnal))
        {
            bool active = isDay.Value ? isDiurnal : isNocturnal;
            if (!active)
                weight *= settings.inactiveTimeWeight;
        }
        return weight;
    }

    /// <summary>Seconds the chunk waits after spawning this type (0 = the settings' Spawn Interval).</summary>
    public float GetEffectiveSpawnTime()
    {
        return shouldHaveRandomSpawnTime ? Random.Range(minSpawnTime, Mathf.Max(minSpawnTime, maxSpawnTime)) : spawnTime;
    }

    /// <summary>Clamps values.</summary>
    public void ValidateConfiguration()
    {
        maxInstances = Mathf.Max(0, maxInstances);
        spawnWeight = Mathf.Max(0f, spawnWeight);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxSpawnTime);
        if (limitHeight && minPreferredHeight > maxPreferredHeight)
        {
            float t = minPreferredHeight;
            minPreferredHeight = maxPreferredHeight;
            maxPreferredHeight = t;
        }
    }
}
