using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  base class for spawners in Unity, responsible for handling the spawning and despawning 
/// of prefab instances with improved error handling, validation, and configurable parameters.
/// </summary>
/// <typeparam name="TPrefab">The type of prefab to be spawned.</typeparam>
/// <typeparam name="TData">The type of data that defines the properties of the prefab to be spawned.</typeparam>
public abstract class SpawnerBase<TPrefab, TData> : MonoBehaviour where TData : class
{
    [Header("Spawner Configuration")]
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

    [Header("Initialization Settings")]
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

    [Header("Retry and Error Handling")]
    /// <summary>
    /// Time to wait before retrying to spawn after a failed attempt.
    /// </summary>
    [Tooltip("Time to wait before retrying to spawn after a failed attempt.")]
    public float retryingSpawnTime = 2f;

    /// <summary>
    /// Maximum number of consecutive spawn failures before temporarily disabling spawning.
    /// </summary>
    [Tooltip("Maximum consecutive spawn failures before temporarily disabling spawning.")]
    [SerializeField] protected int maxConsecutiveFailures = 10;

    /// <summary>
    /// Time to wait after max failures before re-enabling spawning attempts.
    /// </summary>
    [Tooltip("Time to wait after max failures before re-enabling spawning attempts.")]
    [SerializeField] protected float failureRecoveryTime = 30f;

    [Header("Performance Settings")]
    /// <summary>
    /// Enable performance monitoring and logging for spawn operations.
    /// </summary>
    [Tooltip("Enable performance monitoring and logging for spawn operations.")]
    [SerializeField] protected bool enablePerformanceLogging = false;

    /// <summary>
    /// Maximum time allowed for a single spawn operation before considering it slow.
    /// </summary>
    [Tooltip("Maximum time allowed for a single spawn operation before considering it slow.")]
    [SerializeField] protected float maxSpawnTime = 0.1f;

    /// <summary>
    /// Dictionary holding active instances of spawned objects, mapped by their associated data.
    /// </summary>
    [HideInInspector]
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

    //  error tracking
    private int consecutiveFailures = 0;
    private bool isTemporarilyDisabled = false;
    private float lastFailureTime = 0f;

    // Performance tracking
    private float lastSpawnStartTime = 0f;
    private int successfulSpawns = 0;
    private int failedSpawns = 0;

    /// <summary>
    /// Initializes the spawner with chunk data and  validation.
    /// </summary>
    /// <param name="chunkPosition">Position of the chunk.</param>
    /// <param name="heightMap">Height map used for determining spawn positions.</param>
    /// <param name="chunkSize">Size of the chunk.</param>
    /// <param name="parent">Parent transform for the spawned objects.</param>
    /// <param name="biomeMap">Biome map for biome-aware spawning.</param>
    public virtual void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent, Biome[,] biomeMap)
    {
        this.chunkPosition = chunkPosition;
        this.heightMap = heightMap;
        this.chunkSize = chunkSize;
        this.chunkParent = parent;
        this.biomeMap = biomeMap;

        // ed validation
        if (chunkParent == null)
        {
            Debug.LogError($"ChunkParent is null in InitializeSpawner for {gameObject.name}!");
            return;
        }

        if (heightMap == null)
        {
            Debug.LogError($"HeightMap is null in InitializeSpawner for {gameObject.name}!");
            return;
        }

        if (biomeMap == null)
        {
            Debug.LogError($"BiomeMap is null in InitializeSpawner for {gameObject.name}!");
            return;
        }

        // Initialize active instances list for each spawnable prefab with null checking
        if (spawnablePrefabs != null)
        {
            foreach (var prefab in spawnablePrefabs)
            {
                if (prefab != null)
                {
                    activeInstances[prefab] = new List<GameObject>();
                }
            }
        }
        else
        {
            Debug.LogWarning($"SpawnablePrefabs list is null for {gameObject.name}!");
            spawnablePrefabs = new List<TData>();
        }

        // Validate configuration parameters
        ValidateSpawnerParameters();

        // Log initialization if performance logging is enabled
        if (enablePerformanceLogging)
        {
            Debug.Log($"Spawner {gameObject.name} initialized at chunk {chunkPosition} with {spawnablePrefabs.Count} prefab types");
        }
    }

    /// <summary>
    /// Validates and clamps spawner parameters to safe ranges.
    /// </summary>
    private void ValidateSpawnerParameters()
    {
        globalMaxInstances = Mathf.Max(0, globalMaxInstances);
        waitingTime = Mathf.Max(0f, waitingTime);
        minWaitingTime = Mathf.Max(0f, minWaitingTime);
        maxWaitingTime = Mathf.Max(minWaitingTime, maxWaitingTime);
        retryingSpawnTime = Mathf.Max(0.1f, retryingSpawnTime);
        maxConsecutiveFailures = Mathf.Max(1, maxConsecutiveFailures);
        failureRecoveryTime = Mathf.Max(1f, failureRecoveryTime);
        maxSpawnTime = Mathf.Max(0.01f, maxSpawnTime);
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
    ///  coroutine that waits for initialization with better error handling.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected virtual IEnumerator WaitForInitialization()
    {
        float initStartTime = Time.time;
        const float maxInitWaitTime = 60f; // Maximum time to wait for initialization

        // Wait until all initialization conditions are met with timeout
        while ((heightMap == null || chunkParent == null || chunkSize == 0 || chunkPosition == Vector2.zero)
               && (Time.time - initStartTime) < maxInitWaitTime)
        {
            yield return new WaitForSeconds(1f);
        }

        // Check if initialization timed out
        if (Time.time - initStartTime >= maxInitWaitTime)
        {
            Debug.LogError($"Spawner {gameObject.name} initialization timed out after {maxInitWaitTime} seconds!");
            yield break;
        }

        // Final validation before proceeding
        if (!IsProperlyInitialized())
        {
            Debug.LogError($"Spawner {gameObject.name} failed final initialization validation!");
            yield break;
        }

        // If spawning should wait, introduce a delay before starting the spawn routine
        if (shouldWaitToStartSpawning)
        {
            float waitTime = shouldHaveRandomWaitingTime
                ? Random.Range(minWaitingTime, maxWaitingTime)
                : waitingTime;

            if (enablePerformanceLogging)
            {
                Debug.Log($"Spawner {gameObject.name} waiting {waitTime} seconds before starting spawn routine");
            }

            yield return new WaitForSeconds(waitTime);
        }

        // Start the spawn routine once initialization is done
        StartSpawnRoutine();
    }

    /// <summary>
    /// Checks if the spawner is properly initialized.
    /// </summary>
    /// <returns>True if properly initialized.</returns>
    private bool IsProperlyInitialized()
    {
        return heightMap != null &&
               chunkParent != null &&
               chunkSize > 0 &&
               chunkPosition != Vector2.zero &&
               biomeMap != null &&
               spawnablePrefabs != null;
    }

    /// <summary>
    /// Starts the spawn routine if it's not already running and conditions are met.
    /// </summary>
    protected void StartSpawnRoutine()
    {
        // Don't start if temporarily disabled due to failures
        if (isTemporarilyDisabled)
        {
            if (Time.time - lastFailureTime > failureRecoveryTime)
            {
                isTemporarilyDisabled = false;
                consecutiveFailures = 0;

                if (enablePerformanceLogging)
                {
                    Debug.Log($"Spawner {gameObject.name} recovered from failure state");
                }
            }
            else
            {
                return;
            }
        }

        if (spawnRoutine == null && IsProperlyInitialized())
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
    ///  spawning method with error tracking and performance monitoring.
    /// </summary>
    /// <param name="data">Data representing the prefab to spawn.</param>
    protected virtual void SpawnInstance(TData data)
    {
        if (enablePerformanceLogging)
        {
            lastSpawnStartTime = Time.time;
        }

        try
        {
            // Prevent spawning if max instances have been reached
            if (totalActiveInstances >= globalMaxInstances)
            {
                RecordSpawnFailure("Global max instances reached");
                return;
            }

            // Prevent spawning if the prefab's max instances have been reached
            if (data is ISpawbleBySpawner spawnableData && spawnableData.CurrentInstances >= spawnableData.MaxInstances)
            {
                RecordSpawnFailure("Prefab max instances reached");
                return;
            }

            // Validate data
            if (data == null)
            {
                RecordSpawnFailure("Spawn data is null");
                return;
            }

            GameObject prefab = GetPrefab(data);
            if (prefab == null)
            {
                RecordSpawnFailure("Prefab is null");
                return;
            }

            // Determine the spawn position
            Vector3 spawnPosition = GetRandomSpawnPosition(data);

            // Only spawn if the position is valid
            if (IsValidSpawnPosition(spawnPosition) && spawnPosition != Vector3.negativeInfinity)
            {
                // Instantiate the prefab and parent it to the chunk's parent transform
                GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);

                if (instance == null)
                {
                    RecordSpawnFailure("Failed to instantiate prefab");
                    return;
                }

                instance.transform.parent = chunkParent;

                // Track the instance
                if (!activeInstances.ContainsKey(data))
                {
                    activeInstances[data] = new List<GameObject>();
                }

                activeInstances[data].Add(instance);
                totalActiveInstances++;

                // Update the prefab's instance count if it implements ISpawbleBySpawner
                if (data is ISpawbleBySpawner spawnablePrefab)
                {
                    spawnablePrefab.CurrentInstances++;
                }

                // Subscribe to the instance's destruction event, if it has one
                RegisterDestructionCallbacks(instance);

                RecordSpawnSuccess();

                if (enablePerformanceLogging)
                {
                    float spawnTime = Time.time - lastSpawnStartTime;
                    if (spawnTime > maxSpawnTime)
                    {
                        Debug.LogWarning($"Slow spawn detected for {gameObject.name}: {spawnTime:F3}s (max: {maxSpawnTime:F3}s)");
                    }
                }
            }
            else
            {
                RecordSpawnFailure("Invalid spawn position");
            }
        }
        catch (System.Exception ex)
        {
            RecordSpawnFailure($"Exception during spawn: {ex.Message}");
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// Registers destruction callbacks for spawned instances.
    /// </summary>
    /// <param name="instance">The spawned instance.</param>
    private void RegisterDestructionCallbacks(GameObject instance)
    {
        if (instance.TryGetComponent(out Portal portal))
        {
            portal.OnPortalDestroyed += () => DecrementInstanceCount(instance);
        }
        else if (instance.TryGetComponent(out Mob mob))
        {
            mob.OnMobDestroyed += () => DecrementInstanceCount(instance);
        }
    }

    /// <summary>
    /// Records a successful spawn for performance tracking.
    /// </summary>
    private void RecordSpawnSuccess()
    {
        successfulSpawns++;
        consecutiveFailures = 0;

        if (enablePerformanceLogging && successfulSpawns % 10 == 0)
        {
            Debug.Log($"Spawner {gameObject.name} stats - Success: {successfulSpawns}, Failed: {failedSpawns}, Active: {totalActiveInstances}");
        }
    }

    /// <summary>
    /// Records a spawn failure and handles consecutive failure logic.
    /// </summary>
    /// <param name="reason">Reason for the failure.</param>
    private void RecordSpawnFailure(string reason)
    {
        failedSpawns++;
        consecutiveFailures++;
        lastFailureTime = Time.time;

        if (enablePerformanceLogging)
        {
            Debug.LogWarning($"Spawn failure for {gameObject.name}: {reason} (consecutive: {consecutiveFailures})");
        }

        // Temporarily disable spawning if too many consecutive failures
        if (consecutiveFailures >= maxConsecutiveFailures)
        {
            isTemporarilyDisabled = true;
            StopSpawnRoutine();

            Debug.LogWarning($"Spawner {gameObject.name} temporarily disabled due to {consecutiveFailures} consecutive failures. Will retry in {failureRecoveryTime} seconds.");
        }
    }

    /// <summary>
    /// Abstract method that returns the prefab associated with the provided data.
    /// </summary>
    /// <param name="data">Data representing the prefab to retrieve.</param>
    /// <returns>The prefab GameObject associated with the data.</returns>
    protected abstract GameObject GetPrefab(TData data);

    /// <summary>
    /// Destroys all currently active instances and resets counters with  cleanup.
    /// </summary>
    protected void DespawnAllInstances()
    {
        int destroyedCount = 0;

        foreach (var kvp in activeInstances)
        {
            for (int i = kvp.Value.Count - 1; i >= 0; i--)
            {
                GameObject instance = kvp.Value[i];
                if (instance != null)
                {
                    Destroy(instance);
                    destroyedCount++;
                }
            }
            kvp.Value.Clear();

            // Reset instance count for spawnable data
            if (kvp.Key is ISpawbleBySpawner spawnableData)
            {
                spawnableData.CurrentInstances = 0;
            }
        }

        totalActiveInstances = 0;

        if (enablePerformanceLogging && destroyedCount > 0)
        {
            Debug.Log($"Spawner {gameObject.name} despawned {destroyedCount} instances");
        }
    }

    /// <summary>
    /// 
    /// instance count decrementing with better error handling.
    /// </summary>
    /// <param name="instance">The instance to decrement.</param>
    public virtual void DecrementInstanceCount(GameObject instance)
    {
        if (instance == null) return;

        bool instanceFound = false;

        // Loop through the active instances dictionary to find and remove the destroyed instance
        foreach (var kvp in activeInstances)
        {
            if (kvp.Value != null && kvp.Value.Remove(instance))
            {
                totalActiveInstances = Mathf.Max(0, totalActiveInstances - 1);

                // Update the prefab's instance count if it implements ISpawbleBySpawner
                if (kvp.Key is ISpawbleBySpawner spawnablePrefab)
                {
                    spawnablePrefab.CurrentInstances = Mathf.Max(0, spawnablePrefab.CurrentInstances - 1);
                }

                instanceFound = true;
                break;
            }
        }

        if (!instanceFound && enablePerformanceLogging)
        {
            Debug.LogWarning($"Attempted to decrement instance count for {instance.name}, but instance was not found in active instances");
        }

        // Clean up the instance
        if (instance != null)
        {
            Destroy(instance);
        }

        // Restart the spawn routine if there are still spots available and spawner is not disabled
        if (spawnRoutine == null && !isTemporarilyDisabled && totalActiveInstances < globalMaxInstances)
        {
            StartSpawnRoutine();
        }
    }

    /// <summary>
    /// Gets current spawn statistics for debugging and monitoring.
    /// </summary>
    /// <returns>A formatted string with spawn statistics.</returns>
    public virtual string GetSpawnStatistics()
    {
        return $"Spawner {gameObject.name}: Active: {totalActiveInstances}/{globalMaxInstances}, " +
               $"Success: {successfulSpawns}, Failed: {failedSpawns}, " +
               $"Consecutive Failures: {consecutiveFailures}, " +
               $"Disabled: {isTemporarilyDisabled}";
    }

    /// <summary>
    /// Manual method to reset failure state and re-enable spawning.
    /// </summary>
    [ContextMenu("Reset Failure State")]
    public void ResetFailureState()
    {
        consecutiveFailures = 0;
        isTemporarilyDisabled = false;

        if (enablePerformanceLogging)
        {
            Debug.Log($"Manually reset failure state for spawner {gameObject.name}");
        }

        StartSpawnRoutine();
    }

    /// <summary>
    /// Gets the current number of active instances.
    /// </summary>
    /// <returns>Number of currently active instances.</returns>
    public int GetActiveInstanceCount()
    {
        return totalActiveInstances;
    }

    /// <summary>
    /// Checks if the spawner is currently active and able to spawn.
    /// </summary>
    /// <returns>True if spawner can currently spawn instances.</returns>
    public bool IsSpawnerActive()
    {
        return !isTemporarilyDisabled &&
               IsProperlyInitialized() &&
               totalActiveInstances < globalMaxInstances &&
               gameObject.activeInHierarchy;
    }
}