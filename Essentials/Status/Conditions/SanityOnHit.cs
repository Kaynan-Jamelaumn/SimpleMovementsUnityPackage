using UnityEngine;

/// <summary>
/// The hits of this character also cost sanity: a ghost's touch, a horror's claws, a banshee's scream. Put it on a mob
/// (or a player with a terrifying weapon). Each hit that does damage removes <see cref="flatPerHit"/> plus
/// <see cref="percentOfDamage"/> of the damage from the target's sanity (reduced by its Sanity Resistance).
/// </summary>
[AddComponentMenu("SimpleMovements/Sanity On Hit")]
public class SanityOnHit : MonoBehaviour
{
    [Tooltip("Sanity removed per hit.")]
    [SerializeField, Min(0f)] private float flatPerHit = 2f;
    [Tooltip("Plus this percent of the damage dealt (50 = a 20-damage hit removes 10 more sanity).")]
    [SerializeField, Min(0f)] private float percentOfDamage = 0f;
    [Tooltip("Chance (0-1) per hit.")]
    [SerializeField, Range(0f, 1f)] private float chance = 1f;
    [Tooltip("Only hits of this element (None = every hit).")]
    [SerializeField] private ElementType onlyElement = ElementType.None;

    private CombatEntity owner;

    private void OnEnable()
    {
        owner = GetComponentInParent<CombatEntity>();
        CombatEvents.Damaged += OnDamaged;
    }

    private void OnDisable() => CombatEvents.Damaged -= OnDamaged;

    private void OnDamaged(DamageInfo info)
    {
        if (owner == null || info.source != owner || info.target == null || info.target == owner || info.amount <= 0f)
            return;
        if (onlyElement != ElementType.None && info.element != onlyElement)
            return;
        if (!GameNet.NetAuthority.RunsRules(info.target.gameObject) || Random.value > chance)
            return;
        SanityManager s = info.target.GetComponentInChildren<SanityManager>();
        if (s == null) return;
        float amount = flatPerHit + info.amount * percentOfDamage / 100f;
        if (amount > 0f) s.ChangeSanity(-amount, owner);
    }
}
