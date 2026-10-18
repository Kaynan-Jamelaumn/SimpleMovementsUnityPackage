using UnityEngine;

/// <summary>
/// This character drops items from its own <see cref="ItemDropTable"/> when it dies (a mob, a boss, a training dummy).
/// Without this component mobs use the Item Drop Manager's Mob / Elite / Boss tables (Drop From Every Mob). The tier
/// comes from the dungeon (the placement's tier, elites and bosses get a bonus) or <see cref="tier"/>.
/// Rolled where the game rules run; the items become (networked) world items.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Item Drop On Death")]
public class ItemDropOnDeath : MonoBehaviour
{
    [Tooltip("What it drops. Empty = the Item Drop Manager's Mob / Elite / Boss table.")]
    [SerializeField] private ItemDropTable table;
    [Tooltip("Tier of the drop. -1 = from the dungeon placement (its tier; elites and bosses add the manager's bonuses), else 0.")]
    [SerializeField] private int tier = -1;
    [Tooltip("Every item chance is multiplied by this (on top of the manager's).")]
    [SerializeField, Min(0f)] private float chanceMultiplier = 1f;
    [Tooltip("Only when a player (or a player's summon) killed it.")]
    [SerializeField] private bool onlyPlayerKills = true;
    [Tooltip("Also drop these prefabs (pickups made by hand), every time.")]
    [SerializeField] private GameObject[] extraPrefabs = new GameObject[0];

    private CombatEntity entity;

    public ItemDropTable Table { get => table; set => table = value; }

    private void OnEnable()
    {
        entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>() ?? GetComponentInChildren<CombatEntity>();
        if (entity != null) entity.Died += OnDied;
    }

    private void OnDisable()
    {
        if (entity != null) entity.Died -= OnDied;
    }

    private void OnDied(CombatEntity killer)
    {
        if (entity == null || !GameNet.NetAuthority.RunsRules(entity.gameObject))
            return;
        if (onlyPlayerKills && !IsPlayerKill(killer))
            return;
        ItemDropManager m = ItemDropManager.Instance;
        Describe(entity, out int dungeonTier, out bool elite, out bool boss);
        int t = tier >= 0 ? tier : dungeonTier + (boss ? m.bossTierBonus : elite ? m.eliteTierBonus : 0);
        ItemDropTable use = table != null ? table : m.TableForMob(elite, boss);
        float chance = chanceMultiplier * (elite || boss ? m.eliteChanceMultiplier : 1f);
        if (use != null)
            ItemDropManager.Drop(use, t, entity.Position, entity.gameObject, chance);
        if (extraPrefabs != null && GameNet.NetSpawn.ShouldSpawnHere)
            for (int i = 0; i < extraPrefabs.Length; i++)
            {
                if (extraPrefabs[i] == null) continue;
                GameObject go = WorldItems.InstantiatePickable(extraPrefabs[i], entity.Position + Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f, Quaternion.identity, out bool carrier);
                if (go == null) continue;
                if (carrier) WorldItems.Prepare(go);
                WorldItems.Publish(go, entity.gameObject);
            }
    }

    /// <summary>Killed by a player or something a player summoned?</summary>
    public static bool IsPlayerKill(CombatEntity killer)
    {
        for (int i = 0; killer != null && i < 4; i++)
        {
            if (killer.Kind == CombatEntity.EntityKind.Player) return true;
            killer = killer.Summoner;
        }
        return false;
    }

    /// <summary>Tier, elite and boss of a character from its dungeon placement (0 / false outside dungeons).</summary>
    public static void Describe(CombatEntity e, out int tier, out bool elite, out bool boss)
    {
        tier = 0;
        elite = false;
        boss = false;
        if (e == null) return;
        var tag = e.GetComponentInParent<ProceduralDungeon.DungeonSpawned>();
        if (tag == null) return;
        tier = Mathf.Max(0, tag.tier);
        boss = tag.kind == ProceduralDungeon.PlacementKind.Boss;
        elite = tag.elite || tag.order == ProceduralDungeon.MobOrder.Champion || tag.order == ProceduralDungeon.MobOrder.Roam;
    }
}
