using UnityEngine;

/// <summary>
/// Base class for defining common settings for spawnable entities (Portals, Mobs).
/// Includes configurable options for waiting times and retries.
/// </summary>
[System.Serializable]
public abstract class BaseSettings
{
    /// <summary>
    /// Determines if the system should wait before starting the spawning process.
    /// </summary>
    [Tooltip("Should the system wait before starting the spawning process?")]
    public bool shouldWaitToStartSpawning;

    /// <summary>
    /// The fixed time to wait before starting the spawning process.
    /// </summary>
    [Tooltip("The time to wait before starting the spawn process.")]
    public float waitingTime;

    /// <summary>
    /// The minimum randomized waiting time before spawning.
    /// </summary>
    [Tooltip("Minimum waiting time before spawning.")]
    public float minWaitingTime;

    /// <summary>
    /// The maximum randomized waiting time before spawning.
    /// </summary>
    [Tooltip("Maximum waiting time before spawning.")]
    public float maxWaitingTime;

    /// <summary>
    /// Determines if the waiting time between spawns should be randomized.
    /// </summary>
    [Tooltip("Should there be a random waiting time between spawns?")]
    public bool shouldHaveRandomWaitingTime;

    /// <summary>
    /// The time interval between retries for failed spawns.
    /// </summary>
    [Tooltip("Time interval between retries for spawning.")]
    public float retryingSpawnTime;
}
