using UnityEngine;

/// <summary>
/// Damage caused by a status (starvation, dehydration, freezing, a mental breakdown): goes through the character's
/// <see cref="CombatEntity"/> as True, unblockable damage over time - so it can knock the character out
/// (<see cref="Unconsciousness"/>) or kill it like any other damage - or straight to the Health Manager when the
/// character has no combat entity.
/// </summary>
public static class StatusDamage
{
    /// <summary>Deals <paramref name="amount"/> to the character of <paramref name="anyPart"/>. Returns the health removed.</summary>
    public static float Apply(Component anyPart, float amount, ElementType element = ElementType.None)
    {
        if (anyPart == null || amount <= 0f)
            return 0f;
        CombatEntity e = CombatEntity.Resolve(anyPart.gameObject);
        if (e == null)
            e = anyPart.GetComponentInParent<CombatEntity>();
        if (e != null)
        {
            if (e.IsReplica || e.IsDead)
                return 0f;
            return e.ApplyDamage(new DamageInfo
            {
                amount = amount,
                source = null,
                target = e,
                point = e.Center,
                direction = Vector3.down,
                isPeriodic = true,
                unblockable = true,
                type = DamageType.True,
                element = element,
                delivery = DamageDelivery.Periodic,
                threatMultiplier = 0f,
            });
        }
        HealthManager hp = anyPart.GetComponentInParent<HealthManager>();
        if (hp == null)
            hp = anyPart.GetComponentInChildren<HealthManager>();
        if (hp == null || hp.IsReplica)
            return 0f;
        float before = hp.CurrentValue;
        hp.ConsumeHP(amount, true);
        return Mathf.Max(0f, before - hp.CurrentValue);
    }

    /// <summary>Is the character of <paramref name="anyPart"/> dead or unconscious (needs pause, no penalties)?</summary>
    public static bool IsDown(Component anyPart)
    {
        if (anyPart == null)
            return false;
        CombatEntity e = anyPart.GetComponentInParent<CombatEntity>();
        if (e == null)
            e = anyPart.GetComponentInChildren<CombatEntity>();
        return e != null && (e.IsDead || e.IsUnconscious);
    }
}
