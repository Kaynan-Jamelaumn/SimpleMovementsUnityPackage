using UnityEngine;

/// <summary>
/// Settings shared by the world spawners (portals and mobs), edited on the <see cref="EndlessTerrain"/> component.
/// </summary>
[System.Serializable]
public abstract class BaseSettings
{
    [Header("Start Delay")]
    [Tooltip("Wait before a chunk's spawner creates anything once the chunk is ready (its objects and NavMesh exist). Off = it starts right away.")]
    public bool shouldWaitToStartSpawning;

    [Tooltip("Seconds to wait (with Should Wait To Start Spawning, when the waiting time isn't random). Recommended 0-5.")]
    [Min(0f)] public float waitingTime;

    [Tooltip("Shortest random wait in seconds (with Should Have Random Waiting Time).")]
    [Min(0f)] public float minWaitingTime;

    [Tooltip("Longest random wait in seconds (with Should Have Random Waiting Time). Must be at least Min Waiting Time.")]
    [Min(0f)] public float maxWaitingTime;

    [Tooltip("Pick the wait between Min and Max Waiting Time per chunk (so neighbouring chunks don't all start at once) instead of using Waiting Time.")]
    public bool shouldHaveRandomWaitingTime;

    [Tooltip("Seconds before trying again after an attempt found no valid spot (too steep, water, objects, no NavMesh...). Recommended 2-10.")]
    [Min(0.1f)] public float retryingSpawnTime = 3f;

    [Header("Players")]
    [Tooltip("Tag of the player object(s). Distances to the player (spawn limits, activation) are measured from objects with this tag; when none exists, EndlessTerrain's Viewer is used.")]
    public string playerTag = "Player";

    [Header("Debugging")]
    [Tooltip("Log every spawn, rejection and despawn to the Console (noisy - for tracking down why something doesn't appear).")]
    public bool enableDetailedLogging = false;

    [Tooltip("Draw gizmos in the Scene view: spawned things, planned portal sites and their search areas.")]
    public bool enableVisualDebug = false;

    /// <summary>Seconds a chunk's spawner waits before starting (<paramref name="random01"/> picks the random wait).</summary>
    public float StartDelay(float random01)
    {
        if (!shouldWaitToStartSpawning)
            return 0f;
        return shouldHaveRandomWaitingTime ? Mathf.Lerp(minWaitingTime, Mathf.Max(minWaitingTime, maxWaitingTime), random01) : waitingTime;
    }

    /// <summary>Clamps the shared values to safe ranges.</summary>
    protected void ValidateBaseSettings()
    {
        waitingTime = Mathf.Max(0f, waitingTime);
        minWaitingTime = Mathf.Max(0f, minWaitingTime);
        maxWaitingTime = Mathf.Max(minWaitingTime, maxWaitingTime);
        retryingSpawnTime = Mathf.Max(0.1f, retryingSpawnTime);
        if (string.IsNullOrEmpty(playerTag))
            playerTag = "Player";
    }
}
