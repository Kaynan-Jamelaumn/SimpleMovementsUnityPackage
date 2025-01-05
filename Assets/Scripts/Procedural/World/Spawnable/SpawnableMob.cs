using System.Collections.Generic;
using UnityEngine;

// <summary>
/// Class representing a spawnable mob that can be managed by a spawner.
/// </summary>
[System.Serializable]
public class SpawnableMob : ISpawbleBySpawner
{
    [Tooltip("Prefab of the mob to spawn.")]
    public GameObject mobPrefab;

    [Tooltip("List of allowed biomes where this mob can spawn.")]
    public List<Biome> allowedBiomes;

    [Tooltip("Maximum number of mob instances allowed.")]
    public int maxInstances;

    [Tooltip("Weight for random spawning of the mob. Higher values mean more frequent spawning.")]
    public float spawnWeight = 1;

    [Tooltip("Fixed spawn interval for the mob.")]
    public float spawnTime;

    [Tooltip("Minimum spawn time for random spawn time variation.")]
    public float minSpawnTime;

    [Tooltip("Maximum spawn time for random spawn time variation.")]
    public float maxSpawnTime;

    [Tooltip("Should the spawn time be randomized?")]
    public bool shouldHaveRandomSpawnTime;

    [HideInInspector]
    [Tooltip("Current number of mob instances that have been spawned.")]
    public int currentInstances;

    public int MaxInstances { get => maxInstances; set => maxInstances = value; }
    public int CurrentInstances { get => currentInstances; set => currentInstances = value; }
}
