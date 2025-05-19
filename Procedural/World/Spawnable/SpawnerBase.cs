using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for spawners in Unity, responsible for handling the spawning and despawning of prefab instances based on provided data.
/// </summary>
/// <typeparam name="TPrefab">The type of prefab to be spawned.</typeparam>
/// <typeparam name="TData">The type of data that defines the properties of the prefab to be spawned.</typeparam>
public abstract class SpawnerBase<TPrefab, TData> : MonoBehaviour where TData : class
{
    /// <summary>
    /// List of prefabs that can be spawned by this spawner.
    /// </summary>
    [Tooltip("List of prefabs that can be spawned by this spawner.")]
    public List<TData> spawnablePrefabs;

    /// <summary>
    /// Maximum number of instances allowed globally.
    /// </summary>
    [Tooltip("Maximum number of instances allowed globally.")]
    public int globalMaxInstances;

    /// <summary>
    /// Whether spawning should wait until initialization is complete.
    /// </summary>
    [Tooltip("Whether spawning should wait until initialization is complete.")]
    public bool shouldWaitToStartSpawning;

    /// <summary>
    /// Time to wait before starting the spawn process.
    /// </summary>
    [Tooltip("Time to wait before starting the spawn process.")]
    public float waitingTime;

    /// <summary>
    /// Minimum time to wait if random waiting time is enabled.
    /// </summary>
    [Tooltip("Minimum time to wait if random waiting time is enabled.")]
    public float minWaitingTime;

    /// <summary>
    /// Maximum time to wait if random waiting time is enabled.
    /// </summary>
    [Tooltip("Maximum time to wait if random waiting time is enabled.")]
    public float maxWaitingTime;

    /// <summary>
    /// Whether the waiting time before spawning should be randomized.
    /// </summary>
    [Tooltip("Whether the waiting time before spawning should be randomized.")]
    public bool shouldHaveRandomWaitingTime;

    /// <summary>
    /// Time to wait before retrying to spawn after a failed attempt.
    /// </summary>
    [Tooltip("Time to wait before retrying to spawn after a failed attempt.")]
    public float retryingSpawnTime = 2f;

    /// <summary>
    /// Dictionary holding active instances of spawned objects, mapped by their associated data.
    /// </summary>
    [HideInInspector] // Hide this field in the Unity inspector as it is internal to the class
    protected Dictionary<TData, List<GameObject>> activeInstances = new Dictionary<TData, List<GameObject>>();

    /// <summary>
    /// Coroutine responsible for handling the spawn logic.
    /// </summary>
    [HideInInspector]
    protected Coroutine spawnRoutine;

    /// <summary>
    /// Coroutine responsible for waiting until initialization is complete before spawning.
    /// </summary>
    [HideInInspector]
    protected Coroutine waitForInitRoutine;

    /// <summary>
    /// Total count of active instances currently spawned.
    /// </summary>
    [HideInInspector]
    protected int totalActiveInstances;

    /// <summary>
    /// Heightmap used to define spawn position in the Y.
    /// </summary>
    [HideInInspector]
    protected float[,] heightMap;

    /// <summary>
    /// Biome used to define the biome positions.
    /// </summary>
    [HideInInspector]
    protected Biome[,] biomeMap;

    /// <summary>
    /// Position of the chunk being processed.
    /// </summary>
    [HideInInspector]
    protected Vector2 chunkPosition;

    /// <summary>
    /// Size of the chunk being processed.
    /// </summary>
    [HideInInspector]
    protected int chunkSize;

    /// <summary>
    /// Parent transform for all spawned instances, typically a container for the spawned objects.
    /// </summary>
    [HideInInspector]
    protected Transform chunkParent;

    /// <summary>
    /// Initializes the spawner with chunk data (position, height map, size, and parent).
    /// </summary>
    /// <param name="chunkPosition">Position of the chunk.</param>
    /// <param name="heightMap">Height map used for determining spawn positions.</param>
    /// <param name="chunkSize">Size of the chunk.</param>
    /// <param name="parent">Parent transform for the spawned objects.</param>
    public virtual void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent, Biome[,] biomeMap )
    {
        this.chunkPosition = chunkPosition;
        this.heightMap = heightMap;
        this.chunkSize = chunkSize;
        this.chunkParent = parent;
        this.biomeMap = biomeMap;

        if (chunkParent == null)
        {
            Debug.LogWarning("ChunkParent is null in InitializeSpawner!");
        }

        // Initialize active instances list for each spawnable prefab
        foreach (var prefab in spawnablePrefabs)
        {
            activeInstances[prefab] = new List<GameObject>();
        }
    }

    /// <summary>
    /// Starts waiting for initialization when the object is enabled.
    /// </summary>
    private void OnEnable()
    {
        StartWaitingForInitialization();
    }

    /// <summary>
    /// Stops spawn routine and destroys all spawned instances when the object is disabled. 
    /// </summary>
    private void OnDisable()
    {
        StopSpawnRoutine();
        DespawnAllInstances();
    }

    /// <summary>
    /// Starts a coroutine to wait until initialization is complete before starting to spawn.
    /// </summary>
    protected void StartWaitingForInitialization()
    {
        if (waitForInitRoutine == null)
        {
            waitForInitRoutine = StartCoroutine(WaitForInitialization());
        }
    }

    /// <summary>
    /// Coroutine that waits for initialization conditions to be met (e.g., heightMap, chunkParent, etc.).
    /// If conditions are met and spawning should be delayed, it waits for a specified time.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected virtual IEnumerator WaitForInitialization()
    {
        // Wait until all initialization conditions are met
        while (heightMap == null || chunkParent == null || chunkSize == 0 || chunkPosition == Vector2.zero)
        {
            yield return new WaitForSeconds(1f);
        }

        // If spawning should wait, introduce a delay before starting the spawn routine
        if (shouldWaitToStartSpawning)
        {
            float waitTime = shouldHaveRandomWaitingTime
                ? Random.Range(minWaitingTime, maxWaitingTime) // Randomize waiting time if enabled
                : waitingTime;
            yield return new WaitForSeconds(waitTime);
        }

        // Start the spawn routine once initialization is done
        StartSpawnRoutine();
    }

    /// <summary>
    /// Starts the spawn routine if it's not already running.
    /// </summary>
    protected void StartSpawnRoutine()
    {
        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnRoutine());
        }
    }

    /// <summary>
    /// Stops any active spawn routine and initialization routine.
    /// </summary>
    protected void StopSpawnRoutine()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        if (waitForInitRoutine != null)
        {
            StopCoroutine(waitForInitRoutine);
            waitForInitRoutine = null;
        }
    }

    /// <summary>
    /// Abstract method that defines the actual spawning logic. This method needs to be implemented by derived classes.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected abstract IEnumerator SpawnRoutine();

    /// <summary>
    /// Abstract method that checks if the provided position is a valid spawn location.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <returns>True if the position is valid, otherwise false.</returns>
    protected abstract bool IsValidSpawnPosition(Vector3 position);

    /// <summary>
    /// Abstract method that returns a random spawn position within the spawn area.
    /// </summary>
    /// <returns>A random spawn position.</returns>
    protected abstract Vector3 GetRandomSpawnPosition(TData data = null);

    /// <summary>
    /// Spawns a new instance of the prefab associated with the given data.
    /// </summary>
    /// <param name="data">Data representing the prefab to spawn.</param>
    protected virtual void SpawnInstance(TData data)
    {
        // Prevent spawning if max instances have been reached
        if (totalActiveInstances >= globalMaxInstances) return;

        // Prevent spawning if the prefab's max instances have been reached
        if (data is ISpawbleBySpawner spawnableData && spawnableData.CurrentInstances >= spawnableData.MaxInstances) return;

        // Determine the spawn position
        Vector3 spawnPosition = GetRandomSpawnPosition(data);

        // Only spawn if the position is valid
        if (IsValidSpawnPosition(spawnPosition) && spawnPosition != Vector3.negativeInfinity)
        {
            // Instantiate the prefab and parent it to the chunk's parent transform
            GameObject instance = Instantiate(GetPrefab(data), spawnPosition, Quaternion.identity);
            instance.transform.parent = chunkParent;

            // Track the instance
            activeInstances[data].Add(instance);
            totalActiveInstances++;

            // Update the prefab's instance count if it implements ISpawbleBySpawner
            if (data is ISpawbleBySpawner spawnablePrefab)
            {
                spawnablePrefab.CurrentInstances++;
            }

            // Subscribe to the instance's destruction event, if it has one
            if (instance.TryGetComponent(out Portal portal))
            {
                portal.OnPortalDestroyed += () => DecrementInstanceCount(instance);
            }
            else if (instance.TryGetComponent(out Mob mob))
            {
                mob.OnMobDestroyed += () => DecrementInstanceCount(instance);
            }
        }
    }

    /// <summary>
    /// Abstract method that returns the prefab associated with the provided data.
    /// </summary>
    /// <param name="data">Data representing the prefab to retrieve.</param>
    /// <returns>The prefab GameObject associated with the data.</returns>
    protected abstract GameObject GetPrefab(TData data);

    /// <summary>
    /// Destroys all currently active instances and resets the total active instance count.
    /// </summary>
    protected void DespawnAllInstances()
    {
        foreach (var kvp in activeInstances)
        {
            foreach (GameObject instance in kvp.Value)
            {
                Destroy(instance);
                totalActiveInstances--;
            }
            kvp.Value.Clear();
        }
    }

    /// <summary>
    /// Decreases the instance count when an instance is destroyed, and restarts the spawn routine if needed.
    /// </summary>
    /// <param name="instance">The instance to decrement.</param>
    public virtual void DecrementInstanceCount(GameObject instance)
    {
        // Loop through the active instances dictionary to find and remove the destroyed instance
        foreach (var kvp in activeInstances)
        {
            if (kvp.Value.Remove(instance))
            {
                totalActiveInstances--; // Decrease the active instance count

                // Update the prefab's instance count if it implements ISpawbleBySpawner
                if (kvp.Key is ISpawbleBySpawner spawnablePrefab)
                {
                    spawnablePrefab.CurrentInstances--;
                }

                Destroy(instance); // Destroy the instance

                // Restart the spawn routine if there are still spots available
                if (spawnRoutine == null)
                {
                    StartSpawnRoutine();
                }

                break; // Exit the loop once the instance is found and removed
            }
        }
    }
}
