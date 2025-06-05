using System.Collections;
using UnityEngine;

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

        // Wait for the regeneration time using cached WaitForSeconds
        yield return regenerationWait;

        // Destroy the regeneration object
        if (collectedItemObject != null)
            Destroy(collectedItemObject);

        // Instantiate the collectable object as child
        Instantiate(regeneratedItemObjectPrefab, transform.position, Quaternion.identity, transform);

        isRegenerating = false;
    }
}