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
        }
        else
        {
            Debug.LogError("PortalPrefabs is null or not assigned!");
        }

    }

    private void Start()
    {
        foreach (var portalPrefab in portalPrefabs)
        {
            activePortals[portalPrefab] = new List<GameObject>();
        }
    }

    public void InstantiatePortal(PortalPrefab randomPortalPrefab)
    {

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

            GameObject portal = Instantiate(randomPortalPrefab.prefab, spawnPosition, Quaternion.identity);
            portal.transform.parent = chunk;
            activePortals[randomPortalPrefab].Add(portal);
            totalActivePortals++;
        }
    }


    private IEnumerator SpawnPortalRoutine(Vector2 chunkPosition, Transform parent, float[,] heightMap, int chunkSize)
    {
        while (parent != null && parent.gameObject.activeInHierarchy)
        {
            PortalPrefab randomPortalPrefab = portalPrefabs[UnityEngine.Random.Range(0, portalPrefabs.Count)];

            InstantiatePortal(randomPortalPrefab);

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

    private void OnEnable()
    {
        StartPortalRoutine();
    }

    private void OnDisable()
    {
        StopPortalRoutine();
        DespawnPortalsInChunk(transform);
        StopAllCoroutines();
    }

    private void StartPortalRoutine()
    {
       // Debug.Log("PortalManager enabled for chunk: " + transform.name);
        if (heightMap != null && chunkSize > 0)
        {
            if (portalRoutine == null)
            {
                portalRoutine = StartCoroutine(SpawnPortalRoutine(chunkPosition, transform, heightMap, chunkSize));
            }
        }
        else
        {
            //Debug.Log("Dados de Height map/ chunkSize/chunkPosition/chunk null em" + transform.name);
            StartCoroutine(WaitForInitialization());
        }

    }

    private void StopPortalRoutine()
    {
        if (portalRoutine != null)
        {
            StopCoroutine(portalRoutine);
            portalRoutine = null;
        }
    }

    private IEnumerator WaitForInitialization()
    {
        // Wait until both globalOffset and heightMap are not null

        while (chunk == null || heightMap == null || chunkPosition == null || chunkSize == 0)
        {
            yield return new WaitForSeconds(3); // Wait for the next frame
        }


        // Once both are not null, initialize the portal manager
        StartPortalRoutine();
    }

}