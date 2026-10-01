using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemDrop
{
    [Tooltip("Pickable prefab (with an ItemPickable) dropped.")]
    [SerializeField] public GameObject item;
    [Tooltip("Chance (0-1) that it drops.")]
    [SerializeField, Range(0f, 1f)] public float spawnChance = 1;
}

/// <summary>Drops pickable items (loot) at a position: mobs on death, collectables when harvested.</summary>
public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private List<ItemDrop> itemDrops = new List<ItemDrop>();

    public List<ItemDrop> ItemDrops => itemDrops;

    public void SpawnItem(Vector3 position)
    {
        foreach (ItemDrop drop in itemDrops)
        {
            if (drop == null || drop.item == null || Random.value > drop.spawnChance)
                continue;

            GameObject newItem = Instantiate(drop.item, position + GetSpawnOffset(), Quaternion.identity);
            ItemPickable itemPickable = newItem.GetComponent<ItemPickable>();
            if (itemPickable == null || itemPickable.itemScriptableObject == null)
            {
                Debug.LogWarning($"[ItemSpawner] '{drop.item.name}' has no ItemPickable with an item assigned; it cannot be picked up.", this);
                continue;
            }

            int maxDurability = Mathf.Max(1, itemPickable.itemScriptableObject.MaxDurability);
            int stackMax = Mathf.Max(1, itemPickable.itemScriptableObject.StackMax);

            // Between 1 and a full stack (the upper bound used to be excluded, and the quantity was never set).
            int quantityToDrop = Random.Range(1, stackMax + 1);
            itemPickable.quantity = quantityToDrop;
            itemPickable.DurabilityList = new List<int>(quantityToDrop);
            for (int i = 0; i < quantityToDrop; i++)
                itemPickable.DurabilityList.Add(Random.Range(1, maxDurability + 1)); // never spawns already broken
        }
    }

    private static Vector3 GetSpawnOffset()
    {
        return new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
    }
}
