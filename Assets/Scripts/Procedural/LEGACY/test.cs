
//using UnityEngine.SceneManagement;
//using UnityEngine;

//public class Portal : MonoBehaviour
//{
//    [SerializeField] private string sceneToLoad = null; // name of the scene to load
//    [SerializeField] private Vector3 position;


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
//                    DungeonGenerator dungeonGenerator = FindObjectOfType<DungeonGenerator>();
//                    if (dungeonGenerator != null)
//                    {
//                        dungeonGenerator.OnDungeonGenerated += SetPlayerPosition;
//                    }
//                    else
//                    {
//                        Debug.LogWarning("DungeonGenerator not found in the loaded scene.");
//                    }

//                    // Remove the scene loaded event to avoid repeated calls
//                    SceneManager.sceneLoaded -= OnSceneLoaded;
//                }
//            }
//            void SetPlayerPosition()
//            {
//                DungeonGenerator dungeonGenerator = FindObjectOfType<DungeonGenerator>();
//                if (dungeonGenerator != null)
//                {

//                    other.transform.position = dungeonGenerator.SpawnPosition;
//                    Debug.Log("Player spawned at: " + other.transform.position);
//                }
//                else
//                {
//                    Debug.LogWarning("DungeonGenerator not found when setting player position.");
//                }
//            }
//        }
//    }
//    public event Action OnDungeonGenerated;
//    // Start is called before the first frame update
//    void Start()
//    {
//        GenerateDungeon();
//    }


//    void GenerateDungeon()
//    {
//        int numFloors = UnityEngine.Random.Range(minFloors, maxFloors + 1);
//        int maxStairs = UnityEngine.Random.Range(minNumberOfStairsPerFloor, maxNumberOfStairsPerFloor + 1);
//        for (int floor = 0; floor < numFloors; floor++)
//        {
//            MazeGenerator(floor, maxStairs);
//        }
//        mobSpawner.StartSpawningMobs();

//        OnDungeonGenerated?.Invoke(); // Notify that the dungeon generation is complete.
//    }