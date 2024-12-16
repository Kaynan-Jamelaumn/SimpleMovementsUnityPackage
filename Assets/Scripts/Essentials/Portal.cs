using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = null; // name of the scene to load
    [SerializeField] private Vector3 position;

    [Header("Dungeon Config")]
    [SerializeField] private GameObject dungeonObject;
    [SerializeField] private float despawnTime; // Fixed time for activation
    [SerializeField] private float minDespawnTime; // Minimum random time
    [SerializeField] private float maxDespawnTime; // Maximum random time
    [SerializeField] private bool shouldHaveRandomDespawnTime; // Flag for random time activation

    public delegate void PortalDestroyedHandler();
    public event PortalDestroyedHandler OnPortalDestroyed;
    private void Awake()
    {
        StartCoroutine(ActivatePortal());
    }

    private IEnumerator ActivatePortal()
    {
        float waitTime = shouldHaveRandomDespawnTime ? Random.Range(minDespawnTime, maxDespawnTime) : despawnTime;
        yield return new WaitForSeconds(waitTime);
        Debug.Log("Portal destroyed after " + waitTime + " seconds.");

        // Destroy the portal GameObject
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogWarning("Bad Scene");
                other.gameObject.transform.position = position;
                return;
            }
            PlayerMovementController movementController = other.GetComponent<PlayerMovementController>();
            CharacterController characterController = other.GetComponent<CharacterController>();

            movementController.enabled = false;
            characterController.enabled = false;
            // Preserve the player during the transition
            DontDestroyOnLoad(other.gameObject);

            // Load the new scene
            SceneManager.LoadScene(sceneToLoad);

            // Set up the callback for when the scene is loaded
            SceneManager.sceneLoaded += OnSceneLoaded;

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == sceneToLoad)
                {
                    // Move the player to the correct scene
                    SceneManager.MoveGameObjectToScene(other.gameObject, scene);

                    // Find the dungeon generator and set the spawn position
                    Instantiate(dungeonObject);
                    DungeonGenerator dungeonGenerator = dungeonObject.GetComponent<DungeonGenerator>();

                    if (dungeonGenerator != null)
                    {
                        dungeonGenerator.OnDungeonGenerated += () => SetPlayerPosition(other, dungeonGenerator, characterController, movementController);
                    }
                    else
                    {
                        Debug.LogWarning("DungeonGenerator not found in the loaded scene.");
                    }

                    // Remove the scene loaded event to avoid repeated calls
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                }
            }
        }
    }

    private void SetPlayerPosition(Collider player, DungeonGenerator dungeonGenerator, CharacterController characterController, PlayerMovementController movementController)
    {


        if (dungeonGenerator != null)
        {
            player.transform.position = dungeonGenerator.SpawnPosition;
            Debug.Log("Player spawned at: " + player.transform.position);
            movementController.enabled = true;
            characterController.enabled = true;
        }
        else
        {
            Debug.LogWarning("DungeonGenerator not found when setting player position.");
        }

    }

    private void OnDestroy()
    {
        OnPortalDestroyed?.Invoke();
    }
}

