using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalManager : MonoBehaviour
{
    [System.Serializable]
    public class PortalPrefab
    {
        public GameObject prefab;
        public int maxInstances;
        public float spawnTime;
        public float minSpawnTime;
        public float maxSpawnTime;
        public bool shouldHaveRandomSpawnTime;
    }

    public List<PortalPrefab> portalPrefabs;
    public int globalMaxPortals;

    private Dictionary<PortalPrefab, List<GameObject>> activePortals = new Dictionary<PortalPrefab, List<GameObject>>();
    public Coroutine portalRoutine;
    [SerializeField] private int totalActivePortals;

    // Assigned by the TerrainChunk
    Transform chunk;
    private Vector2 chunkPosition;
    private float[,] heightMap;
    private int chunkSize;

    private void Start()
    {
        foreach (var portalPrefab in portalPrefabs)
        {
            activePortals[portalPrefab] = new List<GameObject>();
        }
    }

    private void OnEnable()
    {
        Debug.Log("PortalManager enabled for chunk: " + transform.name);
        if (heightMap != null && chunkSize > 0)
        {
            Debug.Log("xalabrau");
            portalRoutine = StartCoroutine(SpawnPortalRoutine(chunkPosition, transform, heightMap, chunkSize));
        }
        else
        {
            Debug.Log("majestic");
        }
    }

    private void OnDisable()
    {
        if (portalRoutine != null)
        {
            StopCoroutine(portalRoutine);
            portalRoutine = null;
        }
        DespawnPortalsInChunk(transform);
    }

    public void InitializePortalManager(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform chunk)
    {
        this.chunkPosition = chunkPosition;
        this.heightMap = heightMap;
        this.chunkSize = chunkSize;
        this.chunk = chunk;
        if (portalPrefabs != null)
        {
            foreach (var portalPrefab in portalPrefabs)
            {
                if (!activePortals.ContainsKey(portalPrefab))
                {
                    activePortals[portalPrefab] = new List<GameObject>();
                }
            }
            Debug.Log("PortalPrefabs initialized: " + portalPrefabs.Count);
        }
        else
        {
            Debug.LogError("PortalPrefabs is null or not assigned!");
        }
        if (this.chunkPosition == null) Debug.Log("tst0");
        if (this.heightMap == null) Debug.Log("tst1");
        if (this.chunkSize == null) Debug.Log("tst2");
        if (this.chunk == null) Debug.Log("ts3");

    }

    private IEnumerator SpawnPortalRoutine(Vector2 chunkPosition, Transform parent, float[,] heightMap, int chunkSize)
    {
        while (parent != null && parent.gameObject.activeInHierarchy)
        {
            Debug.Log("talal");
            PortalPrefab randomPortalPrefab = portalPrefabs[UnityEngine.Random.Range(0, portalPrefabs.Count)];

            if (activePortals[randomPortalPrefab].Count < randomPortalPrefab.maxInstances && totalActivePortals < globalMaxPortals)
            {
                float xOffset = UnityEngine.Random.Range(0, chunkSize);
                float zOffset = UnityEngine.Random.Range(0, chunkSize);

                float height = heightMap[(int)xOffset, (int)zOffset];
                Vector3 spawnPosition = new Vector3(
                    chunkPosition.x + xOffset,
                    height,
                    chunkPosition.y + zOffset
                );

                GameObject portal = Instantiate(randomPortalPrefab.prefab, spawnPosition, Quaternion.identity, parent);
                activePortals[randomPortalPrefab].Add(portal);
                totalActivePortals++;
            }

            float waitTime = randomPortalPrefab.shouldHaveRandomSpawnTime
                ? UnityEngine.Random.Range(randomPortalPrefab.minSpawnTime, randomPortalPrefab.maxSpawnTime)
                : randomPortalPrefab.spawnTime;

            yield return new WaitForSeconds(waitTime);
        }
    }

    public void DespawnPortalsInChunk(Transform chunkParent)
    {
        foreach (Transform child in chunkParent)
        {
            if (child.CompareTag("Portal"))
            {
                Destroy(child.gameObject);
                totalActivePortals--;
            }
        }
    }
}
