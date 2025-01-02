using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnableMob : ISpawbleBySpawner
{
    public GameObject mobPrefab;
    public List<Biome> allowedBiomes;
    public int maxInstances;        // Maximum instances of this mob
    public float spawnWeight = 1;       // Weight for random spawning
    public float spawnTime;         // Fixed spawn interval
    public float minSpawnTime;      // Min spawn time (for randomized interval)
    public float maxSpawnTime;      // Max spawn time
    public bool shouldHaveRandomSpawnTime;  // Use randomized spawn interval?

    [HideInInspector] public int currentInstances;

    public int MaxInstances { get => maxInstances; set => maxInstances = value; }
    public int CurrentInstances { get => currentInstances; set => currentInstances = value; }

}