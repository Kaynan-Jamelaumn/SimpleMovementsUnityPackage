using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
[System.Serializable]
public class ItemDrop
{
    [SerializeField] public GameObject item;
    [SerializeField, Range(0f, 1f)] public float spawnChance = 1;
}

public class ItemSpawnner : MonoBehaviour
{
    [SerializeField] private List<ItemDrop> itemDrops = new List<ItemDrop>();

    public void SpawnItem(Vector3 position)
    {
        foreach (ItemDrop item in itemDrops)
        {
            if (item.item != null && Random.value <= item.spawnChance)
            {
                Vector3 spawnOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
                Vector3 spawnPosition = position + spawnOffset;

                GameObject newItem = Instantiate(item.item, spawnPosition, Quaternion.identity);
                ItemPickable itemPickable = newItem.GetComponent<ItemPickable>();

                int maxDurability = itemPickable.itemScriptableObject.MaxDurability;
                int stackMax = itemPickable.itemScriptableObject.StackMax;

                int quantityToDrop = Random.Range(1, stackMax);

                for (int i = 0; i < quantityToDrop; i++)
                {
                    int durability = Random.Range(0, maxDurability);
                    itemPickable.DurabilityList.Add(durability);
                }
            }
        }
    }
}