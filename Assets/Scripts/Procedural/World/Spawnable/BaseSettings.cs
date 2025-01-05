using UnityEngine;

/// <summary>
/// Base class for defining common settings for spawnable entities (Portals, Mobs).
/// </summary>
[System.Serializable]
public abstract class BaseSettings
{
    [Tooltip("Should the system wait before starting the spawning process?")]
    public bool shouldWaitToStartSpawning;

    [Tooltip("The time to wait before starting the spawn process.")]
    public float waitingTime;

    [Tooltip("Minimum waiting time before spawning.")]
    public float minWaitingTime;

    [Tooltip("Maximum waiting time before spawning.")]
    public float maxWaitingTime;

    [Tooltip("Should there be a random waiting time between spawns?")]
    public bool shouldHaveRandomWaitingTime;

    [Tooltip("Time interval between retries for spawning.")]
    public float retryingSpawnTime;
}
