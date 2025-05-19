using UnityEngine;

/// <summary>
/// Represents a spawnable portal that can be managed by a spawner.
/// Configures spawn restrictions, timing, and randomized intervals.
/// </summary>
[System.Serializable]
public class SpawnablePortal : ISpawbleBySpawner
{
    /// <summary>
    /// Prefab of the portal to spawn.
    /// </summary>
    [Tooltip("Prefab of the portal to spawn.")]
    public GameObject prefab;

    /// <summary>
    /// Maximum number of portal instances allowed.
    /// </summary>
    [Tooltip("Maximum number of portal instances allowed.")]
    public int maxInstances;

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

    /// <summary>
    /// Current number of portal instances that have been spawned.
    /// Hidden in the inspector to avoid unintended modifications.
    /// </summary>
    [HideInInspector]
    [Tooltip("Current number of portal instances that have been spawned.")]
    public int currentInstances;

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
}
