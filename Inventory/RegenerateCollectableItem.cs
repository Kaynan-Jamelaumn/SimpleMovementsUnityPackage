using System.Collections;
using UnityEngine;

/// <summary>
/// Grows a collectable back after it was harvested: a placeholder (stump) shows while it regrows, then a new collectable
/// appears. Runs where the rules run (offline, the server); in a network session both prefabs should be networked so
/// every player sees them (they are network-spawned here).
/// </summary>
public class RegenerateCollectableItem : MonoBehaviour
{
    [SerializeField] private float regenerationTime = 5f;
    [SerializeField] private GameObject regeneratedItemObjectPrefab;
    [SerializeField] private GameObject collectedItemObjectPrefab;

    private bool isRegenerating = false;
    private WaitForSeconds regenerationWait;

    private void Awake()
    {
        // Cache the WaitForSeconds to avoid garbage collection
        regenerationWait = new WaitForSeconds(regenerationTime);
    }

    private void Update()
    {
        if (!GameNet.NetSpawn.ShouldSpawnHere)
            return; // a client: the server's regrown objects arrive over the network
        // Only check if we're not already regenerating
        if (!isRegenerating && !HasCollectableItemObject())
        {
            StartCoroutine(RegenerateItem());
        }
    }

    private bool HasCollectableItemObject()
    {
        // Check if there is any child with the same name as the prefab
        // Fixed: Compare names instead of object references since prefab != instance
        if (collectedItemObjectPrefab == null)
            return true;
        string prefabName = collectedItemObjectPrefab.name;

        foreach (Transform child in transform)
        {
            // Remove "(Clone)" suffix for comparison if present
            string childName = child.gameObject.name.Replace("(Clone)", "").Trim();
            if (childName == prefabName)
                return true;
        }
        return false;
    }

    private IEnumerator RegenerateItem()
    {
        isRegenerating = true;

        // Instantiate regeneration object as child
        GameObject collectedItemObject = Instantiate(collectedItemObjectPrefab, transform.position, Quaternion.identity, transform);
        GameNet.NetSpawn.Spawn(collectedItemObject);

        // Wait for the regeneration time using cached WaitForSeconds
        yield return regenerationWait;

        // Destroy the regeneration object
        if (collectedItemObject != null)
            GameNet.NetSpawn.Destroy(collectedItemObject);

        // Instantiate the collectable object as child
        if (regeneratedItemObjectPrefab != null)
            GameNet.NetSpawn.Spawn(Instantiate(regeneratedItemObjectPrefab, transform.position, Quaternion.identity, transform));

        isRegenerating = false;
    }
}