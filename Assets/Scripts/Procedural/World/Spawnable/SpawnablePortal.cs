using UnityEngine;

/// <summary>
/// Class representing a spawnable portal that can be managed by a spawner.
/// </summary>
[System.Serializable]
public class SpawnablePortal : ISpawbleBySpawner
{
    [Tooltip("Prefab of the portal to spawn.")]
    public GameObject prefab;

    [Tooltip("Maximum number of portal instances allowed.")]
    public int maxInstances;

    [Tooltip("Time to spawn the portal.")]
    public float spawnTime;

    [Tooltip("Minimum spawn time for random spawn time variation.")]
    public float minSpawnTime;

    [Tooltip("Maximum spawn time for random spawn time variation.")]
    public float maxSpawnTime;

    [Tooltip("Should the spawn time be randomized?")]
    public bool shouldHaveRandomSpawnTime;

    [HideInInspector]
    [Tooltip("Current number of portal instances that have been spawned.")]
    public int currentInstances;

    public int MaxInstances { get => maxInstances; set => maxInstances = value; }
    public int CurrentInstances { get => currentInstances; set => currentInstances = value; }
}