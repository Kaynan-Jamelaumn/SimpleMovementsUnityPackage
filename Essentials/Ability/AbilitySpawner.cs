using System.Collections.Generic;
using UnityEngine;

/// <summary>An ability that can drop from an <see cref="AbilitySpawner"/>.</summary>
[System.Serializable]
public class GetableAbility
{
    [Tooltip("Ability to drop.")]
    public AbilityDefinition ability;
    [Tooltip("Old-style ability asset, converted automatically. Used only when Ability is empty.")]
    public AbilityEffectSO legacyAbility;
    [Tooltip("Changes applied to the dropped copy (e.g. a weaker version).")]
    public AbilityModifierSet modifiers = new AbilityModifierSet();
    [Tooltip("Roll the absorbed variant (weaker / same / stronger / altered) like a mob kill, on top of the modifiers.")]
    public bool rollVariant = false;
    [Tooltip("Chance (0-1) of this ability dropping.")]
    [Range(0f, 1f)] public float spawnChance = 1f;
    [Tooltip("Pickup visual. Empty = the default glowing orb.")]
    public GameObject particleEffect;
    [Tooltip("Seconds after dropping before it can be picked up.")]
    [Min(0f)] public float waitingTimeToAbilityBecomeAvailable = 0.6f;
    [Tooltip("Seconds it stays on the ground before vanishing (0 = forever).")]
    [Min(0f)] public float abilityLifeSpan = 25f;
}

/// <summary>
/// Drops ability pickups: when this character dies (Spawn On Death) or when <see cref="SpawnAbility"/> is called
/// (chests, quest rewards, shrines). Mobs do not need it for absorption - that is automatic from their abilities'
/// Absorption settings - but it can add guaranteed or extra drops.
/// </summary>
public class AbilitySpawner : MonoBehaviour
{
    [Tooltip("Abilities that can drop, each with its own chance.")]
    [SerializeField] private List<GetableAbility> spawnedAbilities = new List<GetableAbility>();
    [Tooltip("Drop automatically when this character dies.")]
    [SerializeField] private bool spawnOnDeath = true;
    [Tooltip("Maximum pickups per drop (0 = no limit).")]
    [SerializeField, Min(0)] private int maxDrops = 0;

    private CombatEntity entity;

    public List<GetableAbility> SpawnedAbilities => spawnedAbilities;

    private void OnEnable()
    {
        if (!spawnOnDeath)
            return;
        entity = CombatEntity.Resolve(gameObject);
        if (entity != null)
            entity.Died += OnDied;
    }

    private void OnDisable()
    {
        if (entity != null)
            entity.Died -= OnDied;
        entity = null;
    }

    private void OnDied(CombatEntity killer) => SpawnAbility();

    /// <summary>Rolls every entry and drops the winners around this object.</summary>
    public void SpawnAbility()
    {
        if (spawnedAbilities == null)
            return;
        int dropped = 0;
        for (int i = 0; i < spawnedAbilities.Count; i++)
        {
            if (maxDrops > 0 && dropped >= maxDrops)
                break;
            GetableAbility entry = spawnedAbilities[i];
            if (entry == null || Random.value > entry.spawnChance)
                continue;
            AbilityDefinition def = entry.ability != null ? entry.ability : LegacyAbilityConverter.Convert(entry.legacyAbility, false);
            if (def == null)
                continue;

            AbilityGrant grant;
            if (entry.rollVariant)
            {
                grant = AbilityAbsorption.CreateGrant(def);
                grant.modifiers = grant.modifiers.CombinedWith(entry.modifiers);
            }
            else
            {
                grant = new AbilityGrant(def, entry.modifiers, def.DisplayName);
            }

            Vector3 pos = transform.position + GetSpawnOffset();
            AbilityPickup.Spawn(grant, pos, entry.particleEffect, entry.waitingTimeToAbilityBecomeAvailable, entry.abilityLifeSpan);
            dropped++;
        }
    }

    private static Vector3 GetSpawnOffset() => new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
}
