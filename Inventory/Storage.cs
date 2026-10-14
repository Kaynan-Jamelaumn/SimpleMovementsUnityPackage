using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A container (chest, crate) whose items are shown in the storage window next to the inventory. One player uses it
/// at a time (<see cref="User"/>): two windows on the same contents would let both players take the same items. In a
/// network session the contents live on the server; a client sees them in the window the server opens for it.
/// </summary>
public class Storage : MonoBehaviour
{
    public new string name;
    public int size;
    public List<StorageItem> items = new List<StorageItem>();

    [SerializeField] RandomSpawningItems itemsToSpawn;
    [SerializeField] bool spawnItems;

    private bool itemsSpawned;

    /// <summary>The inventory that has this storage open right now, or null.</summary>
    public InventoryManager User { get; private set; }

    void Start()
    {
        int itemsToAdd = size - items.Count;
        for (int i = 0; i < itemsToAdd; i++)
        {
            items.Add(new StorageItem(0, null));
        }

        // Random contents are rolled where the rules run (offline, the server): a client never uses its own copy.
        if (spawnItems && !itemsSpawned && GameNet.NetSpawn.ShouldSpawnHere && itemsToSpawn != null && itemsToSpawn.itemsToSpawn.Count > 0)
        {
            itemsSpawned = true;
            int count = Mathf.Min(Random.Range(itemsToSpawn.minCount, itemsToSpawn.maxCount + 1), items.Count);

            for (int i = 0; i < count; i++)
            {
                ItemSO itemToSpawn = itemsToSpawn.itemsToSpawn[Random.Range(0, itemsToSpawn.itemsToSpawn.Count)];
                if (itemToSpawn == null)
                    continue;

                items[i].itemScriptableObject = itemToSpawn;

                if (items[i].itemScriptableObject.StackMax > 1)
                {
                    int countToSpawn = Random.Range(1, items[i].itemScriptableObject.StackMax + 1);
                    items[i].currentStack = countToSpawn;
                }
                else
                {
                    items[i].currentStack = 1;
                }
            }
        }
    }

    /// <summary>Is the storage open in another inventory than <paramref name="inventory"/>?</summary>
    public bool IsUsedByOther(InventoryManager inventory)
    {
        if (User == null)
            return false;
        if (User == inventory)
            return false;
        if (!User.IsStorageOpened || User.OpenedStorage != this)
        {
            User = null; // that inventory closed it (or is gone) without telling
            return false;
        }
        return true;
    }

    /// <summary>The storage is opened by <paramref name="inventory"/>.</summary>
    public void Claim(InventoryManager inventory) => User = inventory;

    /// <summary><paramref name="inventory"/> closed the storage.</summary>
    public void Release(InventoryManager inventory)
    {
        if (User == inventory)
            User = null;
    }
}
