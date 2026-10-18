using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemDrop
{
    [Tooltip("Pickable prefab (with an ItemPickable) dropped.")]
    [SerializeField] public GameObject item;
    [Tooltip("Chance (0-1) that it drops.")]
    [SerializeField, Range(0f, 1f)] public float spawnChance = 1;
    [Tooltip("When it may drop: only by day or at night, between some hours, only in some weathers (at the drop's spot) " +
             "or never in some. Default: any time, any weather.")]
    public SpawnConditions conditions = new SpawnConditions();
}

/// <summary>
/// Drops pickable items (loot) at a position: mobs on death, collectables when harvested. Loot is rolled and created
/// where the rules run (offline, the server); in a network session each drop goes into this object's scene and is
/// network-spawned (see <see cref="WorldItems"/>), so every player there sees it and the server decides who picks it up.
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Tooltip("Items with chances, quantities and tiers (Create ▸ SimpleMovements ▸ Items ▸ Item Drop Table), rolled through " +
             "the Item Drop Manager. Optional: the prefab list below still works (both are used).")]
    [SerializeField] private ItemDropTable dropTable;
    [Tooltip("Tier of the table roll (-1 = from the dungeon placement, elites and bosses add the manager's bonus).")]
    [SerializeField] private int tableTier = -1;
    [SerializeField] private List<ItemDrop> itemDrops = new List<ItemDrop>();

    public List<ItemDrop> ItemDrops => itemDrops;
    public ItemDropTable DropTable { get => dropTable; set => dropTable = value; }

    /// <summary>Does it drop anything (a table or prefabs)?</summary>
    public bool HasDrops => dropTable != null || (itemDrops != null && itemDrops.Count > 0);

    public void SpawnItem(Vector3 position)
    {
        if (!GameNet.NetSpawn.ShouldSpawnHere)
            return; // a client: the server's loot arrives over the network
        if (dropTable != null)
        {
            CombatEntity self = CombatEntity.Resolve(gameObject);
            ItemDropOnDeath.Describe(self, out int t, out bool elite, out bool boss);
            ItemDropManager m = ItemDropManager.Instance;
            int tier = tableTier >= 0 ? tableTier : t + (boss ? m.bossTierBonus : elite ? m.eliteTierBonus : 0);
            ItemDropManager.Drop(dropTable, tier, position, gameObject, elite || boss ? m.eliteChanceMultiplier : 1f);
        }
        foreach (ItemDrop drop in itemDrops)
        {
            if (drop == null || drop.item == null || Random.value > drop.spawnChance)
                continue;
            if (drop.conditions != null && !drop.conditions.Allows(position))
                continue; // not its time of day or its weather

            GameObject newItem = WorldItems.InstantiatePickable(drop.item, position + GetSpawnOffset(), Quaternion.identity, out bool carrier);
            ItemPickable itemPickable = newItem != null ? newItem.GetComponent<ItemPickable>() : null;
            if (itemPickable == null || itemPickable.itemScriptableObject == null)
            {
                Debug.LogWarning($"[ItemSpawner] '{drop.item.name}' has no ItemPickable with an item assigned; it cannot be picked up.", this);
                if (newItem != null)
                    WorldItems.Publish(newItem, gameObject); // still shown to everyone as it was before
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

            // The carrier has no physics to fall with: it shows the item's model and is put on the ground at once.
            WorldItems.Prepare(newItem);
            if (carrier)
                ItemHandler.RestOnGround(newItem, gameObject);
            WorldItems.Publish(newItem, gameObject);
        }
    }

    private static Vector3 GetSpawnOffset()
    {
        return new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
    }
}
