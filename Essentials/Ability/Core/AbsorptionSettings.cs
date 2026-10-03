using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global rules of ability absorption. Optional: create one with Assets > Create > SimpleMovements > Abilities >
/// Absorption Settings and put it in a Resources folder named exactly "AbsorptionSettings".
/// </summary>
[CreateAssetMenu(fileName = "AbsorptionSettings", menuName = "SimpleMovements/Abilities/Absorption Settings", order = 20)]
public class AbsorptionSettings : ScriptableObject
{
    [Tooltip("Turn the whole absorption mechanic on or off.")]
    public bool enabled = true;

    [Tooltip("Multiplies every ability's absorb chance (difficulty / progression tuning).")]
    [Range(0f, 5f)] public float globalChanceMultiplier = 1f;

    [Tooltip("Maximum abilities one kill can grant.")]
    [Min(1)] public int maxAbilitiesPerKill = 1;

    [Tooltip("Only kills by a player (or a player's summon) can grant abilities.")]
    public bool onlyPlayerKills = true;

    [Tooltip("Pickup: an orb drops where the mob died. Instant: the killer gets it at once.")]
    public AbsorbDelivery delivery = AbsorbDelivery.Pickup;

    [Header("Pickup")]
    [Tooltip("Pickup visual. Empty = Combat Settings' default, or a generated glowing orb.")]
    public GameObject pickupPrefab;
    [Tooltip("Seconds before the pickup can be collected.")]
    [Min(0f)] public float pickupArmDelay = 0.6f;
    [Tooltip("Seconds before an uncollected pickup vanishes (0 = never).")]
    [Min(0f)] public float pickupLifetime = 25f;
    [Tooltip("Distance at which a player collects it (metres).")]
    [Min(0.2f)] public float pickupRadius = 1.4f;
    [Tooltip("Distance at which it starts flying toward the nearest player (0 = off).")]
    [Min(0f)] public float magnetRadius = 4f;

    [Header("Slots")]
    [Tooltip("When the player absorbs an ability it already has.")]
    public DuplicateAbsorbPolicy duplicatePolicy = DuplicateAbsorbPolicy.ReplaceIfStronger;
    [Tooltip("When every ability slot is already used.")]
    public FullSlotsPolicy fullSlotsPolicy = FullSlotsPolicy.ReplaceOldestAbsorbed;

    [Header("Default Variants (used by abilities with 'Use Default Variants')")]
    [Tooltip("Possible absorbed forms and their relative weights. An ability's own Custom Variants are added to these.")]
    public List<AbilityVariant> defaultVariants = CreateDefaultVariants();

    [Header("Debug")]
    public bool logRolls = false;

    public static List<AbilityVariant> CreateDefaultVariants() => new List<AbilityVariant>
    {
        new AbilityVariant
        {
            name = "", tier = AbsorbTier.Weaker, weight = 3f,
            modifiers = new AbilityModifierSet { label = "Weakened", damageMultiplier = 0.7f, cooldownMultiplier = 1.25f, areaMultiplier = 0.85f, controlMultiplier = 0.75f },
        },
        new AbilityVariant { name = "", tier = AbsorbTier.Same, weight = 2f, modifiers = new AbilityModifierSet() },
        new AbilityVariant
        {
            name = "", tier = AbsorbTier.Stronger, weight = 0.6f,
            modifiers = new AbilityModifierSet { label = "Empowered", damageMultiplier = 1.25f, cooldownMultiplier = 0.9f, areaMultiplier = 1.1f },
        },
        new AbilityVariant
        {
            name = "", tier = AbsorbTier.Altered, weight = 1f,
            modifiers = new AbilityModifierSet { label = "Focused", projectileCountOverride = 1, damageMultiplier = 1.4f, areaMultiplier = 0.8f },
        },
    };

    private static AbsorptionSettings instance;

    public static AbsorptionSettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<AbsorptionSettings>("AbsorptionSettings");
                if (instance == null)
                {
                    instance = CreateInstance<AbsorptionSettings>();
                    instance.name = "AbsorptionSettings (defaults)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    public static void Override(AbsorptionSettings settings) => instance = settings;

    private void OnValidate()
    {
        if (defaultVariants == null || defaultVariants.Count == 0)
            defaultVariants = CreateDefaultVariants();
    }
}
