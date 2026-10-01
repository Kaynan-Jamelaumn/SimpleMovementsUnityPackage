using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What kind of item it is. Decides which equipment slot it goes into (see <see cref="SlotTypeHelper"/>).
/// Serialized as numbers in assets: only add new values at the END.
/// </summary>
public enum ItemType
{
    Potion,
    Food,
    Weapon,
    Helmet,
    Armor,
    Boots,
    Gloves,
    Shield,
    Trinket,
    Cloak,
    Belt,
    Shoulders,
    Bracers,
    Ring,
    Leggings,
    Amulet
}
/// <summary>
/// Base of every item asset: name, icon, stack size, weight, durability, cooldown, how it sits in the hand and how it
/// looks and sounds when used. Consumables, equippables (armor, trinkets) and weapons derive from it.
/// </summary>
public abstract class ItemSO : ScriptableObject
{
    // Basic Item Information
    [Header("Basic Information")]
    [Tooltip("Name shown in the inventory and tooltips. Empty = the asset's name.")]
    [SerializeField] protected new string name;
    [Tooltip("Flavour / explanation text shown in the item panel.")]
    [TextArea(2, 5)]
    [SerializeField] protected string description;
    [Tooltip("Kind of item. Decides the equipment slot it goes into (Helmet, Armor, Boots, Ring...). Weapons, potions and food use common slots.")]
    [SerializeField] protected ItemType itemType;
    [Tooltip("Picture shown in the slots and the item panel. 'Auto-Fill' finds a sprite with the item's name; 'Icon From Prefab' renders one.")]
    [SerializeField] protected Sprite icon;
    [Tooltip("3D model: held in the hand and spawned in the world when the item is dropped (a collider and an ItemPickable are added when missing).")]
    [SerializeField] protected GameObject prefab;

    // Stack and Weight
    [Header("Stack and Weight")]
    [Tooltip("How many of this item fit in one slot. 1 for weapons and armor; e.g. 10 for potions, 64 for materials.")]
    [SerializeField] protected int stackMax;
    [Tooltip("Weight of ONE item (the stack weighs this times the amount). Counts toward the player's carry weight.")]
    [SerializeField] protected float weight;
    [Tooltip("Value of one item (shops, loot). Shown in the item panel.")]
    [SerializeField] protected float price;

    // Durability
    [Header("Durability")]
    [Tooltip("Uses before the item breaks (0 = never breaks). Weapons lose durability per attack.")]
    [SerializeField] protected int maxDurability;
    [Tooltip("Durability a new item starts with (usually the same as Max Durability).")]
    [SerializeField] protected int durability = 1;
    [Tooltip("Durability lost each time the item is used / each attack.")]
    [SerializeField] protected int durabilityReductionPerUse = 1;
    [Tooltip("Remove the item when its durability reaches 0 (off = it stays, unusable, e.g. to be repaired).")]
    [SerializeField] protected bool shouldBeDestroyedOn0UsesLeft = true;

    // Cooldown
    [Header("Cooldown")]
    [Tooltip("Seconds before the item can be used again (potions, throwables...).")]
    [SerializeField] protected float cooldown = 0;

    // Hand Position, Rotation, and Scale
    [Header("Item Hand Position")]
    [Tooltip("Offset of the model from the player's hand. Tune it in Play Mode: changes to the asset are kept.")]
    [SerializeField] protected Vector3 position;
    [Tooltip("Rotation (degrees) of the model in the hand.")]
    [SerializeField] protected Vector3 rotation = new Vector3(80f, -20f, 0);
    [Tooltip("Scale of the model in the hand.")]
    [SerializeField] protected Vector3 scale = new Vector3(1, 1, 1);

    // Animation and Audio
    [Header("Use Feedback")]
    [Tooltip("Animation played when the item is used (drink, eat...). Weapons use their attacks' animations instead.")]
    [SerializeField] protected AnimationClip useAnimation;
    [Tooltip("Sound played when the item is used (drinking, eating, reading...). Weapons do not use it: their attacks play the Attack Sound.")]
    [SerializeField] protected AudioClip useAudioClip;
    [Tooltip("Particles played when the item is used. Weapons do not use it: their attacks have their own particles and trail.")]
    [SerializeField] protected ParticleSystem useParticles;

    // Pickup Time
    [Header("Pickup")]
    [Tooltip("Seconds the interact key must be held to pick the item up from the world (0 = instant).")]
    [SerializeField] protected float pickUpTime;

    // Properties
    public float PickUpTime => pickUpTime;
    public string Name => name;
    public string Description => description;
    public ItemType ItemType => itemType;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public int StackMax => stackMax;
    public float Weight => weight;
    public float Price => price;
    public int MaxDurability => maxDurability;
    public int Durability => durability;
    public int DurabilityReductionPerUse => durabilityReductionPerUse;
    public bool ShouldBeDestroyedOn0UsesLeft => shouldBeDestroyedOn0UsesLeft;
    public float Cooldown => cooldown;
    public Vector3 Position => position;
    public Vector3 Rotation => rotation;
    public Vector3 Scale => scale;
    public AnimationClip UseAnimation => useAnimation;
    public AudioClip UseAudioClip => useAudioClip;
    public ParticleSystem UseParticles => useParticles;

    /// <summary>
    /// The effects this item applies while it is equipped (armor in its slot, a weapon in hand). Called each time the
    /// item is equipped; the <see cref="EquipmentManager"/> applies them and removes exactly them when it comes off.
    /// </summary>
    public virtual void CollectEquipEffects(List<EquipmentEffect> into) { }

    /// <summary>Tooltip lines describing what the item does (stats, effects, set membership...).</summary>
    public virtual void AppendTooltip(List<string> lines)
    {
        var effects = new List<EquipmentEffect>();
        CollectEquipEffects(effects);
        EquipmentEffect.DescribeAll(effects, 1f, lines);
    }

    /// <summary>Checks the item's configuration (inspector and validation window).</summary>
    public virtual void ValidateItem(List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(name))
            warnings.Add("The item has no display name.");
        if (icon == null)
            warnings.Add("No icon: the item shows as an empty square in the inventory.");
        if (prefab == null)
            warnings.Add("No prefab: the item cannot be dropped into the world.");
        if (stackMax < 1)
            errors.Add("Stack Max must be at least 1.");
        if (weight < 0f)
            errors.Add("Weight cannot be negative.");
    }

    /// <summary>
    /// Old way of equipping: applies (true) or removes (false) one copy of the item's equipment effects on the
    /// character. Equipment is now applied automatically (items in equipment slots, the weapon in hand); this remains
    /// for older scripts and goes through the <see cref="EquipmentManager"/>, so values never drift.
    /// </summary>
    [System.Obsolete("Equipment is applied automatically by the EquipmentManager (items in equipment slots, the weapon in hand). Use EquipmentManager.Equip/Unequip for custom cases.")]
    public void ApplyEquippedStats(bool shouldApply = false, PlayerStatusController statusController = null)
    {
        if (statusController == null)
        {
            Debug.LogWarning($"[Items] ApplyEquippedStats({this.name}): no PlayerStatusController given.", this);
            return;
        }
        EquipmentManager.For(statusController)?.ApplyLegacy(this, shouldApply);
    }

    // Base UseItem method for non-weapon items
    public virtual void UseItem(GameObject player, PlayerStatusController statusController)
    {
        // Only apply interaction effects for non-weapon items
        if (itemType != ItemType.Weapon)
        {
            ApplyItemEffects(player);
        }
    }

    // Weapon-specific UseItem method (will be overridden by WeaponSO)
    public virtual void UseItem(GameObject player, PlayerStatusController statusController, WeaponController weaponController, AttackType attackType = AttackType.Normal)
    {
        // Default implementation for non-weapon items
        UseItem(player, statusController);
    }

    // Protected method to apply item effects (for non-weapon items)
    protected virtual void ApplyItemEffects(GameObject player)
    {
        // Apply audio effects
        if (useAudioClip != null)
        {
            AudioSource audioSource = player.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                // Try to get from Player component
                var playerComponent = player.GetComponent<Player>();
                audioSource = playerComponent?.PlayerAudioSource;
            }

            if (audioSource != null)
            {
                audioSource.PlayOneShot(useAudioClip);
            }
        }

        // Apply particle effects
        if (useParticles != null)
        {
            var particles = Instantiate(useParticles, player.transform.position, player.transform.rotation);
            particles.Play();
        }

        // For animation, we'll let individual item types handle their own animation logic
        // since weapons use the PlayerAnimationController and other items might use different systems
        ApplyItemAnimation(player);
    }

    // Virtual method for animation handling - can be overridden by subclasses
    protected virtual void ApplyItemAnimation(GameObject player)
    {
        if (useAnimation != null)
        {
            // For non-weapon items, you might want to use a different animation system
            // or trigger specific animations through the PlayerAnimationController
            var animController = player.GetComponent<PlayerAnimationController>();
            if (animController != null)
            {
                // You can create a method in PlayerAnimationController to handle item use animations
                // animController.PlayItemUseAnimation(useAnimation);
                Debug.Log($"Playing item use animation: {useAnimation.name}");
            }
        }
    }


    protected virtual void OnValidate()
    {
        // Empty display name: use the asset's name ('this.name' is the display name field itself).
        if (string.IsNullOrEmpty(name))
            name = base.name;

        // Ensure stack max is at least 1
        if (stackMax < 1)
            stackMax = 1;

        // Ensure durability is valid
        if (maxDurability < 1)
            maxDurability = 1;
        if (durability < 0)
            durability = 0;
        if (durability > maxDurability)
            durability = maxDurability;
    }
}