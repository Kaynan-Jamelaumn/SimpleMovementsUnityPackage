using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class RegenerateCollectableItem : MonoBehaviour
{
    [SerializeField] private float regenerationTime;
    [SerializeField] private GameObject regeneratedItemObjectPrefab;
    [SerializeField] private GameObject collectedItemObjectPrefab;
    
    private bool isRegenerating = false;
    private void Update()
    {
        if (isRegenerating == false)
            if (HasCollectableItemObject() == false)
                StartCoroutine(RegenerateItem());
    }

    private bool HasCollectableItemObject()
    {
        // Check if there is any child corresponding to the collectedItemObjectPrefab
        foreach (Transform child in transform)
            if (child.gameObject == collectedItemObjectPrefab)
                return true;
        return false;
    }

    private IEnumerator RegenerateItem()
    {
        isRegenerating = true;

        // Instantiate regeneration object
        GameObject collectedItemObject = Instantiate(collectedItemObjectPrefab, transform.position, Quaternion.identity);

        // Wait for the regeneration time
        yield return new WaitForSeconds(regenerationTime);

        // Destroy the regeneration object
        Destroy(collectedItemObject);

        // Instantiate the collectable object
        Instantiate(regeneratedItemObjectPrefab, transform.position, Quaternion.identity);

        isRegenerating = false;
    }
}

