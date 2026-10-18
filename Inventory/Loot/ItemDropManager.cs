using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The item drop manager (like Albion Online's loot tables): one place that holds every <see cref="ItemDropTable"/> of
/// the game - the "inventory" of what can drop and from where - the default tables for chests, breakables (urns,
/// barrels, crates), mobs, elites and bosses, global drop-rate and quantity multipliers, and items that never drop.
/// Everything that drops items goes through it (<see cref="Drop"/>): dungeon chests and breakables, dungeon loot / prop
/// entries with a table, and mobs (<see cref="ItemDropOnDeath"/>, or every mob with the Mob table).
/// <para>Create it with Tools ▸ SimpleMovements ▸ Inventory ▸ Item Drops ▸ Create Item Drop Manager (it lives at Resources/SimpleMovements/Items/Drops/ItemDropManager);
/// without one the defaults below are used (no default tables: nothing drops until you assign tables). Drops are rolled
/// where the game rules run (offline, or on the server: the items become networked world items).</para>
/// </summary>
[CreateAssetMenu(fileName = "ItemDropManager", menuName = "SimpleMovements/Items/Item Drop Manager", order = 21)]
public class ItemDropManager : ScriptableObject
{
    public const string ResourcesName = "ItemDropManager";

    [Header("Every drop table of the game")]
    [Tooltip("The registry: every table the game uses (the inspector lists every item they can drop and finds tables in the project).")]
    public List<ItemDropTable> tables = new List<ItemDropTable>();

    [Header("Default tables (used when a source has none of its own)")]
    [Tooltip("Chests without their own table (dungeon chests, built-in chest placeholders).")]
    public ItemDropTable chestTable;
    [Tooltip("Urns, barrels, crates and other breakables without their own table.")]
    public ItemDropTable breakableTable;
    [Tooltip("Ordinary mobs without an Item Drop On Death (only with Drop From Every Mob on).")]
    public ItemDropTable mobTable;
    [Tooltip("Elite mobs (dungeon guardians, champions). Empty = the Mob table with the elite bonuses below.")]
    public ItemDropTable eliteTable;
    [Tooltip("Bosses. Empty = the Elite table, else the Mob table.")]
    public ItemDropTable bossTable;
    [Tooltip("Every mob that dies drops from the Mob / Elite / Boss table (mobs with an Item Drop On Death use theirs).")]
    public bool dropFromEveryMob = true;
    [Tooltip("Mobs only drop when a player (or a player's summon) killed them.")]
    public bool onlyPlayerKills = true;

    [Header("Global multipliers")]
    [Tooltip("Every item chance is multiplied by this (2 = twice as likely, capped at 100%).")]
    [Min(0f)] public float dropChanceMultiplier = 1f;
    [Tooltip("Every quantity is multiplied by this.")]
    [Min(0f)] public float quantityMultiplier = 1f;
    [Tooltip("Tiers added for elites (more rolls, better entries).")]
    [Min(0)] public int eliteTierBonus = 1;
    [Tooltip("Tiers added for bosses.")]
    [Min(0)] public int bossTierBonus = 2;
    [Tooltip("Chance multiplier for elites and bosses.")]
    [Min(0f)] public float eliteChanceMultiplier = 1.5f;

    [Header("World")]
    [Tooltip("Dropped items are spread this far around the source (meters).")]
    [Min(0f)] public float spreadRadius = 0.8f;
    [Tooltip("Height above the source the items appear at (they fall to the ground).")]
    public float dropHeight = 0.6f;

    [Header("Never dropped")]
    [Tooltip("Items no table may drop (quest items, unfinished items).")]
    public List<ItemSO> blockedItems = new List<ItemSO>();

    [Header("Debug")]
    public bool logDrops;

    /// <summary>Raised for every drop (table, items, position, source).</summary>
    public static event Action<ItemDropTable, IReadOnlyList<ItemStack>, Vector3, GameObject> Dropped;

    private static ItemDropManager instance;
    private static readonly System.Random sharedRng = new System.Random();

    /// <summary>The manager (Resources/SimpleMovements/Items/Drops/ItemDropManager), or defaults.</summary>
    public static ItemDropManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = SMAssets.Load<ItemDropManager>(SMAssets.ItemDrops, ResourcesName);
                if (instance == null)
                {
                    instance = CreateInstance<ItemDropManager>();
                    instance.name = "ItemDropManager (defaults)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    /// <summary>Uses another manager (tests, bootstrap).</summary>
    public static void Override(ItemDropManager manager) => instance = manager;

    public bool IsBlocked(ItemSO item) => item == null || (blockedItems != null && blockedItems.Contains(item));

    /// <summary>The table of a mob death: elite / boss tables first, then the Mob table.</summary>
    public ItemDropTable TableForMob(bool elite, bool boss)
    {
        if (boss && bossTable != null) return bossTable;
        if ((boss || elite) && eliteTable != null) return eliteTable;
        return mobTable;
    }

    // ------------------------------------------------------------------ rolling and dropping

    /// <summary>Rolls <paramref name="table"/> for a source of <paramref name="tier"/> (with the global multipliers). Pure: nothing is spawned.</summary>
    public static List<ItemStack> Roll(ItemDropTable table, int tier, float chanceMultiplier = 1f, System.Random rng = null)
    {
        var result = new List<ItemStack>();
        if (table == null)
            return result;
        ItemDropManager m = Instance;
        table.Roll(tier, rng ?? sharedRng, result, m.dropChanceMultiplier * chanceMultiplier, m.quantityMultiplier);
        return result;
    }

    /// <summary>
    /// Rolls <paramref name="table"/> and drops the items in the world around <paramref name="position"/> (pickups that
    /// fall to the ground; networked world items on a server). <paramref name="source"/> is what dropped them (its scene).
    /// Returns the number of pickups created. Does nothing on a network client (the server drops).
    /// </summary>
    public static int Drop(ItemDropTable table, int tier, Vector3 position, GameObject source, float chanceMultiplier = 1f)
    {
        if (table == null || !GameNet.NetSpawn.ShouldSpawnHere)
            return 0;
        List<ItemStack> items = Roll(table, tier, chanceMultiplier);
        return Spawn(table, items, position, source);
    }

    /// <summary>Drops already-rolled <paramref name="items"/> around <paramref name="position"/>.</summary>
    public static int Spawn(ItemDropTable table, IReadOnlyList<ItemStack> items, Vector3 position, GameObject source)
    {
        if (items == null || items.Count == 0 || !GameNet.NetSpawn.ShouldSpawnHere)
            return 0;
        ItemDropManager m = Instance;
        int created = 0, index = 0;
        foreach (ItemStack s in items)
        {
            if (s.item == null || s.quantity <= 0)
                continue;
            int left = s.quantity;
            int stack = Mathf.Max(1, s.item.StackMax);
            while (left > 0)
            {
                int n = Mathf.Min(left, stack);
                left -= n;
                // Spread on a little circle so stacks don't land on each other.
                float a = index++ * 2.39996f;   // golden angle
                float r = m.spreadRadius * Mathf.Sqrt((index % 7 + 1) / 7f);
                Vector3 at = position + new Vector3(Mathf.Cos(a) * r, m.dropHeight, Mathf.Sin(a) * r);
                if (ItemHandler.SpawnWorldItem(s.item, n, null, at, source) != null)
                    created++;
            }
        }
        if (m.logDrops)
            Debug.Log($"[Item Drops] {(table != null ? table.Name : "?")} dropped {string.Join(", ", items)} at {position}.", source);
        Dropped?.Invoke(table, items, position, source);
        return created;
    }

    /// <summary>Puts already-rolled items straight into an inventory (quest rewards, auto-loot). Returns the items that did not fit.</summary>
    public static List<ItemStack> Give(IReadOnlyList<ItemStack> items, InventoryManager inventory)
    {
        var left = new List<ItemStack>();
        if (items == null) return left;
        foreach (ItemStack s in items)
        {
            if (s.item == null || s.quantity <= 0) continue;
            int notAdded = inventory != null ? inventory.AddItem(s.item, s.quantity) : s.quantity;
            if (notAdded > 0) left.Add(new ItemStack(s.item, notAdded));
        }
        return left;
    }

    /// <summary>Every item any registered or default table can drop (the manager's "inventory").</summary>
    public HashSet<ItemSO> AllDroppableItems()
    {
        var set = new HashSet<ItemSO>();
        foreach (ItemDropTable t in AllTables())
            t.CollectItems(set);
        return set;
    }

    /// <summary>The registered tables and the default ones (no duplicates).</summary>
    public List<ItemDropTable> AllTables()
    {
        var list = new List<ItemDropTable>();
        void Add(ItemDropTable t) { if (t != null && !list.Contains(t)) list.Add(t); }
        if (tables != null) foreach (ItemDropTable t in tables) Add(t);
        Add(chestTable); Add(breakableTable); Add(mobTable); Add(eliteTable); Add(bossTable);
        return list;
    }

    // ------------------------------------------------------------------ mobs without their own component

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        CombatEvents.Killed -= OnKilled;
        CombatEvents.Killed += OnKilled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Dropped = null;
    }

    private static void OnKilled(CombatEntity victim, CombatEntity killer)
    {
        if (victim == null || victim.Kind != CombatEntity.EntityKind.Mob || victim.Summoner != null)
            return;   // summons drop nothing
        if (victim.GetComponentInParent<ItemDropOnDeath>() != null || victim.GetComponentInChildren<ItemDropOnDeath>() != null)
            return;   // it drops its own table
        ItemSpawner own = victim.GetComponentInParent<ItemSpawner>() ?? victim.GetComponentInChildren<ItemSpawner>();
        if (own != null && own.HasDrops)
            return;   // its Item Spawner drops its own loot
        ItemDropManager m = Instance;
        if (!m.dropFromEveryMob || !GameNet.NetAuthority.RunsRules(victim.gameObject))
            return;
        if (m.onlyPlayerKills && !ItemDropOnDeath.IsPlayerKill(killer))
            return;
        ItemDropOnDeath.Describe(victim, out int tier, out bool elite, out bool boss);
        ItemDropTable table = m.TableForMob(elite, boss);
        if (table == null)
            return;
        int bonus = boss ? m.bossTierBonus : elite ? m.eliteTierBonus : 0;
        Drop(table, tier + bonus, victim.Position, victim.gameObject, elite || boss ? m.eliteChanceMultiplier : 1f);
    }
}
