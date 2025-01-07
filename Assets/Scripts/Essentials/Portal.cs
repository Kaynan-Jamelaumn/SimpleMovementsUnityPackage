using UnityEngine.SceneManagement;
using UnityEngine;

using System.Collections;

public class Portal : MonoBehaviour
{
    [Tooltip("Name of the scene to load when the player enters the portal.")]
    [SerializeField] private string sceneToLoad = null;

    [Tooltip("Position to set the player if no scene is loaded.")]
    [SerializeField] private Vector3 position;

    [Header("Dungeon Configuration")]
    [Tooltip("The prefab of the dungeon to instantiate.")]
    [SerializeField] private GameObject dungeonObject;

    [Tooltip("Fixed time for the portal to despawn.")]
    [SerializeField] private float despawnTime;

    [Tooltip("Minimum random time for the portal to despawn.")]
    [SerializeField] private float minDespawnTime;

    [Tooltip("Maximum random time for the portal to despawn.")]
    [SerializeField] private float maxDespawnTime;

    [Tooltip("Should the portal's despawn time be randomized?")]
    [SerializeField] private bool shouldHaveRandomDespawnTime;

    [Tooltip("Should the dungeon be instantiated instead of loading a scene?")]
    [SerializeField] private bool shouldInstantiateDungeon = true;

    // Delegate and event to notify listeners when the portal is destroyed
    public delegate void PortalDestroyedHandler();
    public event PortalDestroyedHandler OnPortalDestroyed;

    private void Awake()
    {
        // Start the coroutine to handle portal activation and destruction
        StartCoroutine(ActivatePortal());
    }

    /// <summary>
    /// Handles the portal's lifespan by waiting for a specified time and then destroying it.
    /// </summary>
    private IEnumerator ActivatePortal()
    {
        // Calculate wait time based on the randomization flag
        float waitTime = shouldHaveRandomDespawnTime
            ? Random.Range(minDespawnTime, maxDespawnTime)
            : despawnTime;

        yield return new WaitForSeconds(waitTime);
        Debug.Log($"Portal destroyed after {waitTime} seconds.");

        // Destroy the portal object
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to the player
        if (other.CompareTag("Player"))
        {
            PlayerMovementController movementController = other.GetComponent<PlayerMovementController>();
            CharacterController characterController = other.GetComponent<CharacterController>();

            // Disable player controls during the transition
            movementController.enabled = false;
            characterController.enabled = false;

            // Persist player object across scene loads
            DontDestroyOnLoad(other.gameObject);

            if (shouldInstantiateDungeon)
            {
                // Handle dungeon instantiation
                HandleDungeonInstantiation(other, movementController, characterController);
            }
            else
            {
                // Handle scene loading
                if (string.IsNullOrEmpty(sceneToLoad))
                {
                    Debug.LogWarning("Invalid scene name. Resetting player position.");
                    other.gameObject.transform.position = position;
                    return;
                }

                // Load the specified scene
                SceneManager.LoadScene(sceneToLoad);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }
    }

    /// <summary>
    /// Instantiates the dungeon and handles player positioning.
    /// </summary>
    private void HandleDungeonInstantiation(Collider player, PlayerMovementController movementController, CharacterController characterController)
    {
        // Destroy any existing dungeon generator
        DungeonGenerator existingDungeonGenerator = FindObjectOfType<DungeonGenerator>();
        if (existingDungeonGenerator != null)
        {
            Destroy(existingDungeonGenerator.gameObject);
            Debug.Log("Existing DungeonGenerator destroyed.");
        }

        // Instantiate the new dungeon
        GameObject instantiatedDungeon = Instantiate(dungeonObject, new Vector3(0, -10000, 0), Quaternion.identity);
        DungeonGenerator newDungeonGenerator = instantiatedDungeon.GetComponent<DungeonGenerator>();

        if (newDungeonGenerator != null)
        {
            // Subscribe to the dungeon generation event
            newDungeonGenerator.OnDungeonGenerated += () =>
            {
                SetPlayerPosition(player, newDungeonGenerator, characterController, movementController);
            };
        }
        else
        {
            Debug.LogWarning("DungeonGenerator component not found on the instantiated dungeon object.");
        }
    }

    /// <summary>
    /// Called when the scene is loaded to position the player.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        DungeonGenerator dungeonGenerator = FindObjectOfType<DungeonGenerator>();

        if (player != null && dungeonGenerator != null)
        {
            dungeonGenerator.OnDungeonGenerated += () =>
            {
                SetPlayerPosition(player.GetComponent<Collider>(), dungeonGenerator, player.GetComponent<CharacterController>(), player.GetComponent<PlayerMovementController>());
            };
        }
    }

    /// <summary>
    /// Sets the player's position based on the dungeon generator's spawn position.
    /// </summary>
    private void SetPlayerPosition(Collider player, DungeonGenerator dungeonGenerator, CharacterController characterController, PlayerMovementController movementController)
    {
        if (dungeonGenerator != null)
        {
            player.transform.position = dungeonGenerator.SpawnPosition;
            Debug.Log($"Player spawned at: {player.transform.position}");

            // Re-enable player controls
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
        // Notify listeners about the portal's destruction
        OnPortalDestroyed?.Invoke();
    }
}