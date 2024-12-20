using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SpawnerBase<TPrefab, TData> : MonoBehaviour where TData : class
{
    public List<TData> spawnablePrefabs;
    public int globalMaxInstances;
    public bool shouldWaitToStartSpawning;
    public float waitingTime;
    public float minWaitingTime;
    public float maxWaitingTime;
    public bool shouldHaveRandomWaitingTime;
    public float retryingSpawnTime = 2f;


    protected Dictionary<TData, List<GameObject>> activeInstances = new Dictionary<TData, List<GameObject>>();
    protected Coroutine spawnRoutine;
    protected Coroutine waitForInitRoutine;
    protected int totalActiveInstances;

    protected float[,] heightMap;
    protected Vector2 chunkPosition;
    protected int chunkSize;
    protected Transform chunkParent;

    public virtual void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent)
    {
        this.chunkPosition = chunkPosition;
        this.heightMap = heightMap;
        this.chunkSize = chunkSize;
        this.chunkParent = parent;

        if (chunkParent == null)
        {
            Debug.LogWarning("ChunkParent is null in InitializeSpawner!");
        }


        foreach (var prefab in spawnablePrefabs)
        {
            activeInstances[prefab] = new List<GameObject>();
        }
    }

    private void OnEnable()
    {
        StartWaitingForInitialization();
    }

    private void OnDisable()
    {
        StopSpawnRoutine();
        DespawnAllInstances();
    }

    protected void StartWaitingForInitialization()
    {
        if (waitForInitRoutine == null)
        {
            waitForInitRoutine = StartCoroutine(WaitForInitialization());
        }
    }

    protected virtual IEnumerator WaitForInitialization()
    {
        while (heightMap == null || chunkParent == null || chunkSize == 0 || chunkPosition == Vector2.zero)
        {
            yield return new WaitForSeconds(1f);
        }

        if (shouldWaitToStartSpawning)
        {
            float waitTime = shouldHaveRandomWaitingTime
                ? Random.Range(minWaitingTime, maxWaitingTime)
                : waitingTime;
            yield return new WaitForSeconds(waitTime);
        }

        StartSpawnRoutine();
    }

    protected void StartSpawnRoutine()
    {
        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnRoutine());
        }
    }

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

    protected abstract IEnumerator SpawnRoutine();

    protected abstract bool IsValidSpawnPosition(Vector3 position);

    protected abstract Vector3 GetRandomSpawnPosition();

    protected virtual void SpawnInstance(TData data)
    {
        if (totalActiveInstances >= globalMaxInstances) return;
        if (data is ISpawbleBySpawner spawnableData && spawnableData.CurrentInstances >= spawnableData.MaxInstances) return;


        Vector3 spawnPosition = GetRandomSpawnPosition();

        if (IsValidSpawnPosition(spawnPosition))
        {
            GameObject instance = Instantiate(GetPrefab(data), spawnPosition, Quaternion.identity);
            instance.transform.parent = chunkParent;

            activeInstances[data].Add(instance);
            totalActiveInstances++;
            if (data is ISpawbleBySpawner spawnablePrefab)
            {
                spawnablePrefab.CurrentInstances++;
            }
            // Subscribe to the destruction event if the instance has one
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

    protected abstract GameObject GetPrefab(TData data);

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
    public virtual void DecrementInstanceCount(GameObject instance)
    {
        foreach (var kvp in activeInstances)
        {
            if (kvp.Value.Remove(instance))
            {
                totalActiveInstances--;
                if (kvp.Key is ISpawbleBySpawner spawnablePrefab)
                {
                    spawnablePrefab.CurrentInstances--;
                  //  spawnablePrefab.MaxInstances = Mathf.Max(spawnablePrefab.MaxInstances - 1, 0); // Optional adjustment
                }

                Debug.Log($"Instance destroyed. Total active instances: {totalActiveInstances}");
                Destroy(instance);
                // Restart the spawn routine if necessary
                if (spawnRoutine == null)
                {
                    Debug.Log($"xalabrau");
                    StartSpawnRoutine();
                }
                break;
            }
        }
    }


}
