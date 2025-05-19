using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Represents a spawnable mob that can be managed by a spawner.
/// Contains configuration options for spawn restrictions, weights, and timing.
/// </summary>
[System.Serializable]
public class SpawnableMob : ISpawbleBySpawner
{
    /// <summary>
    /// Prefab of the mob to spawn.
    /// </summary>
    [Tooltip("Prefab of the mob to spawn.")]
    public GameObject mobPrefab;

    /// <summary>
    /// List of allowed biomes where this mob can spawn.
    /// </summary>
    [Tooltip("List of allowed biomes where this mob can spawn.")]
    public List<Biome> allowedBiomes;

    /// <summary>
    /// Maximum number of mob instances allowed.
    /// </summary>
    [Tooltip("Maximum number of mob instances allowed.")]
    public int maxInstances;

    /// <summary>
    /// Weight for random spawning of the mob. Higher values mean more frequent spawning.
    /// </summary>
    [Tooltip("Weight for random spawning of the mob. Higher values mean more frequent spawning.")]
    public float spawnWeight = 1;

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

    /// <summary>
    /// Current number of mob instances that have been spawned.
    /// Hidden in the inspector to avoid unintended modifications.
    /// </summary>
    [HideInInspector]
    [Tooltip("Current number of mob instances that have been spawned.")]
    public int currentInstances;

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
}
