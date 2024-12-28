//using System.Collections.Generic;
//using UnityEngine;
//using System.Collections;
//using UnityEngine.AI;

//public class MobSpawner : SpawnerBase<SpawnableMob, SpawnableMob>
//{
//    protected override IEnumerator SpawnRoutine()
//    {
//        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
//        {
//            SpawnableMob chosenMob = ChooseWeightedMob();

//            if (chosenMob != null)
//            {
//                SpawnInstance(chosenMob);

//                float waitTime = chosenMob.shouldHaveRandomSpawnTime
//                    ? Random.Range(chosenMob.minSpawnTime, chosenMob.maxSpawnTime)
//                    : chosenMob.spawnTime;

//                yield return new WaitForSeconds(waitTime);
//            }
//            else
//            {
//                // No valid mob to spawn, wait for a short period before retrying
//                yield return new WaitForSeconds(retryingSpawnTime);
//            }
//        }
//    }


//    private SpawnableMob ChooseWeightedMob()
//    {
//        float totalWeight = 0f;

//        foreach (var mob in spawnablePrefabs)
//        {
//            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
//            continue;

//            if (mob.CurrentInstances < mob.MaxInstances)
//                totalWeight += mob.spawnWeight;
             
//        }

//        if (totalWeight <= 0)

//            return null;
        

//        float randomValue = Random.Range(0, totalWeight);

//        foreach (var mob in spawnablePrefabs)
//        {
//            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
//                continue;

//            if (mob.CurrentInstances < mob.MaxInstances)
//            {
//                if (randomValue <= mob.spawnWeight)
//                    return mob;
                
//                randomValue -= mob.spawnWeight;
//            }
//        }

//        Debug.LogWarning("Failed to select a mob. This should not happen if totalWeight > 0.");
//        return null;
//    }


//    protected override Vector3 GetRandomSpawnPosition()
//    {
//        float xOffset = Random.Range(0, chunkSize);
//        float zOffset = Random.Range(0, chunkSize);

//        float height = heightMap[(int)xOffset, (int)zOffset];
//        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
//    }

//    protected override bool IsValidSpawnPosition(Vector3 position)
//    {
//        NavMeshHit hit;
//        return NavMesh.SamplePosition(position, out hit, 1.0f, NavMesh.AllAreas) &&
//               Vector3.Distance(position, hit.position) < 1.0f;
//    }

//    protected override GameObject GetPrefab(SpawnableMob data)
//    {
//        return data.mobPrefab;
//    }
//    public override void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent)
//    {
//        base.InitializeSpawner(chunkPosition, heightMap, chunkSize, parent);

//        // Filter out null prefabs to prevent runtime issues.
//        spawnablePrefabs = spawnablePrefabs?.FindAll(mob => mob != null) ?? new List<SpawnableMob>();
//    }
//}
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;


//public class PortalSpawner : SpawnerBase<PortalPrefab, PortalPrefab>
//{
//    protected override IEnumerator SpawnRoutine()
//    {
//        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
//        {
//            PortalPrefab chosenPortal = spawnablePrefabs[Random.Range(0, spawnablePrefabs.Count)];
//            if (chosenPortal != null)
//            {
//                SpawnInstance(chosenPortal);

//                float waitTime = chosenPortal.shouldHaveRandomSpawnTime
//                    ? Random.Range(chosenPortal.minSpawnTime, chosenPortal.maxSpawnTime)
//                    : chosenPortal.spawnTime;

//                yield return new WaitForSeconds(waitTime);
//            }
//            else
//            {
//                yield return new WaitForSeconds(retryingSpawnTime);
//            }
//        }
//    }

//    protected override Vector3 GetRandomSpawnPosition()
//    {
//        float xOffset = Random.Range(0, chunkSize);
//        float zOffset = Random.Range(0, chunkSize);

//        float height = heightMap[(int)xOffset, (int)zOffset];
//        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
//    }

//    protected override bool IsValidSpawnPosition(Vector3 position)
//    {
//        return true; // Implement your own validation logic for portals
//    }

//    protected override GameObject GetPrefab(PortalPrefab data)
//    {
//        return data.prefab;
//    }


//} 
//using System.Collections;
//using UnityEngine;
//using UnityEngine.SceneManagement;

//public class Portal : MonoBehaviour
//{
//    [SerializeField] private string sceneToLoad = null; // name of the scene to load
//    [SerializeField] private Vector3 position;

//    [Header("Dungeon Config")]
//    [SerializeField] private GameObject dungeonObject;
//    [SerializeField] private float despawnTime; // Fixed time for activation
//    [SerializeField] private float minDespawnTime; // Minimum random time
//    [SerializeField] private float maxDespawnTime; // Maximum random time
//    [SerializeField] private bool shouldHaveRandomDespawnTime; // Flag for random time activation


//    public delegate void PortalDestroyedHandler();
//    public event PortalDestroyedHandler OnPortalDestroyed;
//    private void Awake()
//    {
//        StartCoroutine(ActivatePortal());
//    }

//    private IEnumerator ActivatePortal()
//    {
//        float waitTime = shouldHaveRandomDespawnTime ? Random.Range(minDespawnTime, maxDespawnTime) : despawnTime;
//        yield return new WaitForSeconds(waitTime);
//        Debug.Log("Portal destroyed after " + waitTime + " seconds.");

//        // Destroy the portal GameObject
//        Destroy(gameObject);
//    }

//    private void OnTriggerEnter(Collider other)
//    {
//        if (other.CompareTag("Player"))
//        {
//            if (string.IsNullOrEmpty(sceneToLoad))
//            {
//                Debug.LogWarning("Bad Scene");
//                other.gameObject.transform.position = position;
//                return;
//            }
//            PlayerMovementController movementController = other.GetComponent<PlayerMovementController>();
//            CharacterController characterController = other.GetComponent<CharacterController>();

//            movementController.enabled = false;
//            characterController.enabled = false;
//            // Preserve the player during the transition
//            DontDestroyOnLoad(other.gameObject);

//            // Load the new scene
//            SceneManager.LoadScene(sceneToLoad);

//            // Set up the callback for when the scene is loaded
//            SceneManager.sceneLoaded += OnSceneLoaded;

//            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
//            {
//                if (scene.name == sceneToLoad)
//                {
//                    // Move the player to the correct scene
//                    SceneManager.MoveGameObjectToScene(other.gameObject, scene);

//                    // Find the dungeon generator and set the spawn position
//                    Instantiate(dungeonObject);
//                    DungeonGenerator dungeonGenerator = dungeonObject.GetComponent<DungeonGenerator>();

//                    if (dungeonGenerator != null)
//                    {
//                        dungeonGenerator.OnDungeonGenerated += () => SetPlayerPosition(other, dungeonGenerator, characterController, movementController);
//                    }
//                    else
//                    {
//                        Debug.LogWarning("DungeonGenerator not found in the loaded scene.");
//                    }

//                    // Remove the scene loaded event to avoid repeated calls
//                    SceneManager.sceneLoaded -= OnSceneLoaded;
//                }
//            }
//        }
//    }

//    private void SetPlayerPosition(Collider player, DungeonGenerator dungeonGenerator, CharacterController characterController, PlayerMovementController movementController)
//    {


//        if (dungeonGenerator != null)
//        {
//            player.transform.position = dungeonGenerator.SpawnPosition;
//            Debug.Log("Player spawned at: " + player.transform.position);
//            movementController.enabled = true;
//            characterController.enabled = true;
//        }
//        else
//        {
//            Debug.LogWarning("DungeonGenerator not found when setting player position.");
//        }

//    }

//    private void OnDestroy()
//    {
//        // Notify the spawner before invoking event
//        OnPortalDestroyed?.Invoke();
//    }
//}

//[System.Serializable]
//public class PortalPrefab: ISpawbleBySpawner
//{
//    public GameObject prefab;
//    public int maxInstances;
//    public float spawnTime;
//    public float minSpawnTime;
//    public float maxSpawnTime;
//    public bool shouldHaveRandomSpawnTime;
//    [HideInInspector] public int currentInstances;

//    public int MaxInstances { get => maxInstances; set => maxInstances = value; }
//    public int CurrentInstances { get => currentInstances; set => currentInstances = value; }
//}

//[System.Serializable]
//public class SpawnableMob: ISpawbleBySpawner
//{
//    public GameObject mobPrefab;
//    public int maxInstances;        // Maximum instances of this mob
//    public float spawnWeight;       // Weight for random spawning
//    public float spawnTime;         // Fixed spawn interval
//    public float minSpawnTime;      // Min spawn time (for randomized interval)
//    public float maxSpawnTime;      // Max spawn time
//    public bool shouldHaveRandomSpawnTime;  // Use randomized spawn interval?
//    public float weightToSpawnFactor = 1;
//    [HideInInspector] public int currentInstances;

//    public int MaxInstances { get => maxInstances; set => maxInstances = value; }
//    public int CurrentInstances { get => currentInstances; set => currentInstances = value; }
//    public float CalculateSpawnWeight(float weightSpawnFactor)
//    {
//        // If any of the factors is 0, we return 0
//        if (weightToSpawnFactor == 0 || weightSpawnFactor == 0)
//            return 0;

//        // Calculate the weight by multiplying the spawn factor by the inverse of weightToSpawnFactor
//        return weightSpawnFactor / weightToSpawnFactor;
//    }

//}


//    public class TerrainChunk
//    {
//        GameObject meshObject;
//        Vector2 position;
//        Bounds bounds;

//        MeshRenderer meshRenderer;
//        MeshFilter meshFilter;
//        MeshCollider meshCollider;

//        NavMeshSurface navMeshSurface;
//        private TerrainGenerator terrainGenerator;

//        private WorldMobSpawner worldMobSpawner;


//        Vector2 globalOffset;
//        int maxMobs;
//        public Vector2 Position { get { return position; } }

//        /// <summary>
//        /// Creates a new terrain chunk at the specified coordinates.
//        /// </summary>
//        /// <param name="coord">The coordinate of the chunk in chunk space.</param>
//        /// <param name="size">The size of the chunk in world units.</param>
//        /// <param name="parent">The parent transform for the chunk.</param>

//        public TerrainChunk(Vector2 coord, int size, Transform parent, PortalSettings portalSettings, MobSettings mobSettings,  int count)
//        {
//            position = coord * size; // The chunk's world position is its grid coordinate multiplied by its size.
//            bounds = new Bounds(position, Vector2.one * size);  // The chunk's bounding box.
//            Vector3 positionV3 = new Vector3(position.x, 0, position.y); // Convert 2D position to 3D for the GameObject.
//            globalOffset = new Vector2(position.x, position.y); // Store the global offset for terrain data.

//            // Create the chunk GameObject and components.
//            meshObject = new GameObject("Terrain Chunk" + count);
//            meshRenderer = meshObject.AddComponent<MeshRenderer>();
//            meshFilter = meshObject.AddComponent<MeshFilter>();
//            meshCollider = meshObject.AddComponent<MeshCollider>();

//            // Add NavMeshSurface component to the terrain chunk
//            navMeshSurface = meshObject.AddComponent<NavMeshSurface>();
//            navMeshSurface.collectObjects = CollectObjects.Children;

//            worldMobSpawner = meshObject.AddComponent<WorldMobSpawner>();
//            worldMobSpawner.Initialize(size, globalOffset, parent, navMeshSurface); //size = chunkSize

//            // PortalManager Initialization
//            PortalSpawner portalSpawner = meshObject.AddComponent<PortalSpawner>();
//            portalSpawner.spawnablePrefabs = portalSettings.prefabs;
//            portalSpawner.globalMaxInstances = portalSettings.maxNumberOfPortals;
//            portalSpawner.shouldWaitToStartSpawning = portalSettings.shouldWaitToStartSpawning;
//            portalSpawner.waitingTime = portalSettings.waitingTime;
//            portalSpawner.minWaitingTime = portalSettings.minWaitingTime;
//            portalSpawner.maxWaitingTime = portalSettings.maxWaitingTime;
//            portalSpawner.shouldHaveRandomWaitingTime = portalSettings.shouldHaveRandomWaitingTime;
//            portalSpawner.retryingSpawnTime = portalSettings.retryingSpawnTime;

//            MobSpawner mobSpawner = meshObject.AddComponent<MobSpawner>();
//            mobSpawner.spawnablePrefabs = mobSettings.prefabs;
//            mobSpawner.globalMaxInstances = mobSettings.maxNumberOfMobs;
//            mobSpawner.shouldWaitToStartSpawning = mobSettings.shouldWaitToStartSpawning;
//            mobSpawner.waitingTime = mobSettings.waitingTime;
//            mobSpawner.minWaitingTime = mobSettings.minWaitingTime;
//            mobSpawner.maxWaitingTime = mobSettings.maxWaitingTime;
//            mobSpawner.shouldHaveRandomWaitingTime = mobSettings.shouldHaveRandomWaitingTime;
//            mobSpawner.retryingSpawnTime = mobSettings.retryingSpawnTime;


//            meshObject.transform.position = positionV3;
//            meshObject.transform.parent = parent;
//            SetVisible(false);


//            // Request map data from the terrain generator.
//            mapGenerator.RequestMapData(OnMapDataReceived, globalOffset);
//        }