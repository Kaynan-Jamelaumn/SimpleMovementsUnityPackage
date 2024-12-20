//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using static EndlessTerrain;
//using UnityEngine;

//namespace Assets.Scripts.Procedural.Terrain
//{
//    internal class FuncationBackUp
//    {
//        void UpdateVisibleChunksOld()
//        {
//            // Hide all chunks from the last update.
//            for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
//            {
//                terrainChunksVisibleLastUpdate[i].SetVisible(false);
//            }
//            terrainChunksVisibleLastUpdate.Clear();

//            // Calculate the viewer's current chunk coordinates in the chunk grid.
//            // Each chunk is chunkSize units wide, so we divide the viewer's position by chunkSize
//            // and round to the nearest integer to get the chunk's grid coordinates.
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            // Iterate over all potential chunks within the viewing distance.
//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    // Calculate the coordinates of the chunk being considered.
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    // Limit the number of chunks if maximum chunks per side is enabled.
//                    if (shouldHaveMaxChunkPerSide)
//                    {
//                        // This condition ensures that the number of chunks generated or rendered does not exceed the maximum allowed per side.
//                        // It checks if the absolute value of either the x or y coordinate of the chunk exceeds the limit set by 'maxChunksPerSide'.
//                        // If either coordinate is outside the allowed range (greater than 'maxChunksPerSide'), the chunk is skipped to optimize performance,
//                        // preventing the generation or rendering of chunks that are too far from the viewer's position.
//                        // For example, if 'maxChunksPerSide' is 3, the system will only generate chunks within a 7x7 grid centered on the viewer (from -3 to 3 on both axes).
//                        if (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide)
//                        {
//                            continue;
//                        }
//                    }
//                    // Update existing chunk or create a new one if it doesn't exist.
//                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
//                    {
//                        // Update the chunk and check if it's visible.
//                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
//                        if (terrainChunkDictionary[viewedChunkCoord].IsVisible())
//                        {
//                            terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
//                        }

//                    }
//                    else
//                    {
//                        count++;
//                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);

//                    }
//                }
//            }


//        }
//        second:
//void UpdateVisibleChunks()
//        {
//            // Calculate the viewer's current chunk coordinates in the chunk grid.
//            // Each chunk is chunkSize units wide, so we divide the viewer's position by chunkSize
//            // and round to the nearest integer to get the chunk's grid coordinates.
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            // Keep track of currently visible chunks
//            HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();
//            // Iterate over all potential chunks within the viewing distance.
//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    // Calculate the coordinates of the chunk being considered.
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    // Skip chunks outside the maximum allowed range (if enabled)
//                    if (shouldHaveMaxChunkPerSide &&

//                        // This condition ensures that the number of chunks generated or rendered does not exceed the maximum allowed per side.
//                        // It checks if the absolute value of either the x or y coordinate of the chunk exceeds the limit set by 'maxChunksPerSide'.
//                        // If either coordinate is outside the allowed range (greater than 'maxChunksPerSide'), the chunk is skipped to optimize performance,
//                        // preventing the generation or rendering of chunks that are too far from the viewer's position.
//                        // For example, if 'maxChunksPerSide' is 3, the system will only generate chunks within a 7x7 grid centered on the viewer (from -
//                        (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
//                    {
//                        continue;
//                    }

//                    chunksToBeVisible.Add(viewedChunkCoord);

//                    // Update or create chunk
//                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
//                    {
//                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
//                    }
//                    else
//                    {
//                        count++;
//                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
//                    }
//                }
//            }

//            // Hide chunks that are no longer within view
//            for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
//            {
//                TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
//                if (!chunksToBeVisible.Contains(chunk.Position / chunkSize)) // Check if chunk is still in the visible set
//                {
//                    chunk.SetVisible(false);
//                    terrainChunksVisibleLastUpdate.RemoveAt(i);
//                }
//            }

//            // Update list of currently visible chunks
//            terrainChunksVisibleLastUpdate.Clear();
//            foreach (Vector2 coord in chunksToBeVisible)
//            {
//                if (terrainChunkDictionary[coord].IsVisible())
//                {
//                    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[coord]);
//                }
//            }
//        }

//        third:
//    void UpdateVisibleChunksNew()
//        {
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();

//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    if (shouldHaveMaxChunkPerSide &&
//                        (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
//                    {
//                        continue;
//                    }

//                    chunksToBeVisible.Add(viewedChunkCoord);

//                    if (!terrainChunkDictionary.ContainsKey(viewedChunkCoord))
//                    {
//                        StartCoroutine(LoadChunk(viewedChunkCoord));
//                    }
//                    else
//                    {
//                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
//                    }
//                }
//            }

//            // Hide chunks no longer visible
//            for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
//            {
//                TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
//                if (!chunksToBeVisible.Contains(chunk.Position / chunkSize))
//                {
//                    chunk.SetVisible(false);
//                    terrainChunksVisibleLastUpdate.RemoveAt(i);
//                }
//            }

//            terrainChunksVisibleLastUpdate.Clear();
//            foreach (Vector2 coord in chunksToBeVisible)
//            {
//                if (terrainChunkDictionary[coord].IsVisible())
//                {
//                    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[coord]);
//                }
//            }
//        }

//        IEnumerator LoadChunk(Vector2 coord)
//        {
//            if (!terrainChunkDictionary.ContainsKey(coord))
//            {
//                count++;
//                TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//                terrainChunkDictionary.Add(coord, newChunk);
//                yield return null; // Pause for a frame
//            }
//        }
//        forth:

//    void UpdateVisibleChunks()
//        {
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();

//            // Iterate through chunks in view distance
//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    // Check max chunk boundary
//                    if (shouldHaveMaxChunkPerSide &&
//                        (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
//                    {
//                        continue;
//                    }

//                    chunksToBeVisible.Add(viewedChunkCoord);

//                    // Check if chunk exists, otherwise create it
//                    if (!terrainChunkDictionary.TryGetValue(viewedChunkCoord, out TerrainChunk chunk))
//                    {
//                        chunk = CreateNewChunk(viewedChunkCoord);
//                    }

//                    chunk.UpdateTerrainChunk();
//                }
//            }

//            // Update visibility for chunks not in chunksToBeVisible
//            UpdateChunkVisibility(chunksToBeVisible);
//        }

//        // Helper to create a new chunk
//        TerrainChunk CreateNewChunk(Vector2 coord)
//        {
//            count++;
//            TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//            terrainChunkDictionary.Add(coord, newChunk);
//            return newChunk;
//        }

//        // Helper to update visibility for old and new chunks
//        void UpdateChunkVisibility(HashSet<Vector2> chunksToBeVisible)
//        {
//            for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
//            {
//                TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
//                Vector2 chunkCoord = chunk.Position / chunkSize;

//                if (!chunksToBeVisible.Contains(chunkCoord))
//                {
//                    chunk.SetVisible(false);
//                    terrainChunksVisibleLastUpdate.RemoveAt(i);
//                }
//            }

//            foreach (Vector2 coord in chunksToBeVisible)
//            {
//                TerrainChunk chunk = terrainChunkDictionary[coord];
//                if (chunk.IsVisible() && !terrainChunksVisibleLastUpdate.Contains(chunk))
//                {
//                    terrainChunksVisibleLastUpdate.Add(chunk);
//                }
//            }
//        }
//    }
//}



//public class PortalSpawner : MonoBehaviour
//{

//    public List<PortalPrefab> portalPrefabs;
//    public int globalMaxPortals;

//    private Dictionary<PortalPrefab, List<GameObject>> activePortals = new Dictionary<PortalPrefab, List<GameObject>>();
//    public Coroutine portalRoutine;
//    [SerializeField] private int totalActivePortals;

//    // Assigned by the TerrainChunk
//    Transform chunk;
//    private Vector2 chunkPosition;
//    private float[,] heightMap;
//    private int chunkSize;

//    public void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform chunk)
//    {
//        this.chunkPosition = chunkPosition;
//        this.heightMap = heightMap;
//        this.chunkSize = chunkSize;
//        this.chunk = chunk;
//        if (portalPrefabs != null)
//        {
//            foreach (var portalPrefab in portalPrefabs)
//            {
//                if (!activePortals.ContainsKey(portalPrefab))
//                {
//                    activePortals[portalPrefab] = new List<GameObject>();
//                }
//            }
//        }
//        else
//        {
//            Debug.LogError("PortalPrefabs is null or not assigned!");
//        }

//    }

//    private void Start()
//    {
//        foreach (var portalPrefab in portalPrefabs)
//        {
//            activePortals[portalPrefab] = new List<GameObject>();
//        }
//    }

//    public void InstantiatePortal(PortalPrefab randomPortalPrefab)
//    {

//        if (activePortals[randomPortalPrefab].Count < randomPortalPrefab.maxInstances && totalActivePortals < globalMaxPortals)
//        {
//            float xOffset = UnityEngine.Random.Range(0, chunkSize);
//            float zOffset = UnityEngine.Random.Range(0, chunkSize);

//            float height = heightMap[(int)xOffset, (int)zOffset];
//            Vector3 spawnPosition = new Vector3(
//                chunkPosition.x + xOffset,
//                height,
//                chunkPosition.y + zOffset
//            );

//            GameObject portal = Instantiate(randomPortalPrefab.prefab, spawnPosition, Quaternion.identity);
//            portal.transform.parent = chunk;
//            activePortals[randomPortalPrefab].Add(portal);
//            totalActivePortals++;
//        }
//    }


//    private IEnumerator SpawnPortalRoutine(Vector2 chunkPosition, Transform parent, float[,] heightMap, int chunkSize)
//    {
//        while (parent != null && parent.gameObject.activeInHierarchy)
//        {
//            PortalPrefab randomPortalPrefab = portalPrefabs[UnityEngine.Random.Range(0, portalPrefabs.Count)];

//            InstantiatePortal(randomPortalPrefab);

//            float waitTime = randomPortalPrefab.shouldHaveRandomSpawnTime
//                ? UnityEngine.Random.Range(randomPortalPrefab.minSpawnTime, randomPortalPrefab.maxSpawnTime)
//                : randomPortalPrefab.spawnTime;

//            yield return new WaitForSeconds(waitTime);
//        }
//    }

//    public void DespawnPortalsInChunk(Transform chunkParent)
//    {
//        foreach (Transform child in chunkParent)
//        {
//            if (child.CompareTag("Portal"))
//            {
//                Destroy(child.gameObject);
//                totalActivePortals--;
//            }
//        }
//    }

//    private void OnEnable()
//    {
//        StartPortalRoutine();
//    }

//    private void OnDisable()
//    {
//        StopPortalRoutine();
//        DespawnPortalsInChunk(transform);
//        StopAllCoroutines();
//    }

//    private void StartPortalRoutine()
//    {
//       // Debug.Log("PortalManager enabled for chunk: " + transform.name);
//        if (heightMap != null && chunkSize > 0)
//        {
//            if (portalRoutine == null)
//            {
//                portalRoutine = StartCoroutine(SpawnPortalRoutine(chunkPosition, transform, heightMap, chunkSize));
//            }
//        }
//        else
//        {
//            //Debug.Log("Dados de Height map/ chunkSize/chunkPosition/chunk null em" + transform.name);
//            StartCoroutine(WaitForInitialization());
//        }

//    }

//    private void StopPortalRoutine()
//    {
//        if (portalRoutine != null)
//        {
//            StopCoroutine(portalRoutine);
//            portalRoutine = null;
//        }
//    }

//    private IEnumerator WaitForInitialization()
//    {
//        // Wait until both globalOffset and heightMap are not null

//        while (chunk == null || heightMap == null || chunkPosition == null || chunkSize == 0)
//        {
//            yield return new WaitForSeconds(3); // Wait for the next frame
//        }


//        // Once both are not null, initialize the portal manager
//        StartPortalRoutine();
//    }

//}




//public class MobSpawner : MonoBehaviour
//{

//    public List<SpawnableMob> mobPrefabs;
//    public int globalMaxMobs = 50;  // Global limit for mobs

//    private Dictionary<SpawnableMob, List<GameObject>> activeMobs = new Dictionary<SpawnableMob, List<GameObject>>();
//    private Coroutine spawnRoutine;
//    private Coroutine waitForInitRoutine;
//    private int totalActiveMobs;

//    private float[,] heightMap;
//    private Vector2 chunkPosition;
//    private int chunkSize;
//    private Transform chunkParent;

//    public void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent)
//    {
//        this.chunkPosition = chunkPosition;
//        this.heightMap = heightMap;
//        this.chunkSize = chunkSize;
//        this.chunkParent = parent;

//        // Initialize active mobs tracking
//        foreach (var mob in mobPrefabs)
//        {
//            activeMobs[mob] = new List<GameObject>();
//        }
//    }

//    private void OnEnable()
//    {
//        StartWaitingForInitialization();
//    }

//    private void OnDisable()
//    {
//        StopSpawnRoutine();
//        DespawnAllMobs();
//    }

//    private void StartWaitingForInitialization()
//    {
//        if (waitForInitRoutine == null)
//        {
//            waitForInitRoutine = StartCoroutine(WaitForInitialization());
//        }
//    }

//    private IEnumerator WaitForInitialization()
//    {
//        // Wait until required data is set
//        while (heightMap == null || chunkParent == null || chunkSize == 0 || chunkPosition == Vector2.zero)
//        {
//            yield return new WaitForSeconds(1f);  // Check every second
//        }

//        // Once initialized, start the spawn routine
//        StartSpawnRoutine();
//    }

//    private void StartSpawnRoutine()
//    {
//        if (spawnRoutine == null)
//        {
//            spawnRoutine = StartCoroutine(SpawnMobRoutine());
//        }
//    }

//    private void StopSpawnRoutine()
//    {
//        if (spawnRoutine != null)
//        {
//            StopCoroutine(spawnRoutine);
//            spawnRoutine = null;
//        }
//        if (waitForInitRoutine != null)
//        {
//            StopCoroutine(waitForInitRoutine);
//            waitForInitRoutine = null;
//        }
//    }

//    private IEnumerator SpawnMobRoutine()
//    {
//        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
//        {
//            // Select a mob based on weighted random logic
//            SpawnableMob chosenMob = ChooseWeightedMob();
//            if (chosenMob != null)
//            {
//                SpawnMob(chosenMob);
//            }

//            // Determine spawn wait time
//            float waitTime = chosenMob.shouldHaveRandomSpawnTime
//                ? Random.Range(chosenMob.minSpawnTime, chosenMob.maxSpawnTime)
//                : chosenMob.spawnTime;

//            yield return new WaitForSeconds(waitTime);
//        }
//    }

//    private SpawnableMob ChooseWeightedMob()
//    {
//        float totalWeight = 0f;

//        foreach (var mob in mobPrefabs)
//        {
//            if (activeMobs[mob].Count < mob.maxInstances)  // Consider only mobs under their instance limit
//            {
//                totalWeight += mob.spawnWeight;
//            }
//        }

//        if (totalWeight <= 0) return null;

//        float randomValue = Random.Range(0, totalWeight);

//        foreach (var mob in mobPrefabs)
//        {
//            if (activeMobs[mob].Count < mob.maxInstances)
//            {
//                if (randomValue <= mob.spawnWeight)
//                {
//                    return mob;
//                }
//                randomValue -= mob.spawnWeight;
//            }
//        }

//        return null;
//    }

//    private void SpawnMob(SpawnableMob mobData)
//    {
//        if (totalActiveMobs >= globalMaxMobs) return;  // Enforce global mob limit

//        Vector3 spawnPosition = GetRandomSpawnPosition();

//        if (IsValidSpawnPosition(spawnPosition))
//        {
//            GameObject mobInstance = Instantiate(mobData.mobPrefab, spawnPosition, Quaternion.identity);
//            mobInstance.transform.parent = chunkParent;

//            activeMobs[mobData].Add(mobInstance);
//            totalActiveMobs++;
//        }
//    }

//    private Vector3 GetRandomSpawnPosition()
//    {
//        float xOffset = Random.Range(0, chunkSize);
//        float zOffset = Random.Range(0, chunkSize);

//        float height = heightMap[(int)xOffset, (int)zOffset];
//        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
//    }

//    private bool IsValidSpawnPosition(Vector3 position)
//    {
//        NavMeshHit hit;
//        return NavMesh.SamplePosition(position, out hit, 1.0f, NavMesh.AllAreas) &&
//               Vector3.Distance(position, hit.position) < 1.0f;
//    }

//    private void DespawnAllMobs()
//    {
//        foreach (var kvp in activeMobs)
//        {
//            foreach (GameObject mob in kvp.Value)
//            {
//                Destroy(mob);
//                totalActiveMobs--;
//            }
//            kvp.Value.Clear();
//        }
//    }
//}
