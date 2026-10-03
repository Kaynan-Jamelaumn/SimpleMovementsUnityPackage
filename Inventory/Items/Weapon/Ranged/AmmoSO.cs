using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ammunition (arrows, bolts, bullets, darts, stones). A ranged weapon fires the ammo whose Ammo Type matches its own;
/// the ammo can change the shot: damage, element, extra on-hit effects, the projectile's model. Stacks in the inventory
/// like any item and is counted, spent and recovered automatically.
/// </summary>
[CreateAssetMenu(fileName = "Ammo", menuName = "SimpleMovements/Items/Ammo", order = 3)]
public class AmmoSO : ItemSO
{
    [Header("Ammo")]
    [Tooltip("Which weapons fire it: their ranged mechanic's Ammo Type must be the same word (Arrow, Bolt, Bullet, Shell, Dart, Stone...).")]
    [SerializeField] private string ammoType = "Arrow";
    [Tooltip("Multiplies the weapon's damage (1.2 = broadhead arrows).")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [Tooltip("Damage added to each hit (before defenses).")]
    [SerializeField] private float bonusDamage = 0f;
    [Tooltip("Element of the shot (None = the weapon's element).")]
    [SerializeField] private ElementType element = ElementType.None;
    [Tooltip("Multiplies the projectile's speed (heavy bolts fly slower).")]
    [SerializeField, Min(0.1f)] private float velocityMultiplier = 1f;
    [Tooltip("Projectile model for this ammo (empty = the weapon's projectile).")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Extra effects on the character hit: burning, poison, slow...")]
    [SerializeReference, SubclassSelector] private List<AbilityEffect> onHitEffects = new List<AbilityEffect>();
    [Tooltip("Can be picked up again where it lands (the weapon's Recover Chance decides how often).")]
    [SerializeField] private bool recoverable = true;

    public string AmmoType => ammoType;
    public float DamageMultiplier => damageMultiplier;
    public float BonusDamage => bonusDamage;
    public ElementType Element => element;
    public float VelocityMultiplier => velocityMultiplier;
    public GameObject ProjectilePrefab => projectilePrefab;
    public List<AbilityEffect> OnHitEffects => onHitEffects ?? (onHitEffects = new List<AbilityEffect>());
    public bool Recoverable => recoverable;

    public AmmoSO()
    {
        itemType = ItemType.Ammo;
        stackMax = 50;
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add($"Ammo: {ammoType}");
        if (!Mathf.Approximately(damageMultiplier, 1f))
            lines.Add($"×{damageMultiplier:0.##} damage");
        if (!Mathf.Approximately(bonusDamage, 0f))
            lines.Add($"{bonusDamage:+0.#;-0.#} damage per hit");
        if (element != ElementType.None)
            lines.Add($"{element} shots");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(ammoType))
            errors.Add("Ammo Type is empty: no weapon can fire it.");
        if (stackMax <= 1)
            warnings.Add("Ammo usually stacks (Stack Max 1 means one shot per slot).");
    }

    protected override void OnValidate()
    {
        if (itemType != ItemType.Ammo)
            itemType = ItemType.Ammo;
        base.OnValidate();
    }
}
