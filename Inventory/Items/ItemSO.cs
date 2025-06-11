using UnityEngine;

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
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item/ItemSO")]
public abstract class ItemSO : ScriptableObject
{
    // Basic Item Information
    [Header("Basic Information")]
    [SerializeField] protected new string name;
    [SerializeField] protected string description;
    [SerializeField] protected ItemType itemType;
    [SerializeField] protected Sprite icon;
    [Tooltip("The item prefab")][SerializeField] protected GameObject prefab;

    // Stack and Weight
    [Header("Stack and Weight")]
    [Tooltip("How much of the same item can be contained in one slot")]
    [SerializeField] protected int stackMax;
    [SerializeField] protected float weight;
    [SerializeField] protected float price;

    // Durability
    [Header("Durability")]
    [SerializeField] protected int maxDurability;
    [SerializeField] protected int durability = 1;
    [SerializeField] protected int durabilityReductionPerUse = 1;
    [SerializeField] protected bool shouldBeDestroyedOn0UsesLeft = true;

    // Cooldown
    [Header("Cooldown")]
    [Tooltip("Cooldown for the same item to be used again")]
    [SerializeField] protected float cooldown = 0;

    // Hand Position, Rotation, and Scale
    [Header("Item Hand Position")]
    [Header("Position")]
    [SerializeField] protected Vector3 position;
    [Header("Rotation")]
    [SerializeField] protected Vector3 rotation = new Vector3(80f, -20f, 0);
    [Header("Scale")]
    [SerializeField] protected Vector3 scale = new Vector3(1, 1, 1);

    // Animation and Audio
    [Header("Animation")]
    [SerializeField] protected AnimationClip useAnimation;
    [Header("Audio")]
    [SerializeField] protected AudioClip useAudioClip;

    // Particles
    [Header("Particles")]
    [SerializeField] protected ParticleSystem useParticles;

    // Pickup Time
    [Header("Pickup")]
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

    public virtual void ApplyEquippedStats(bool shouldApply = false, PlayerStatusController statusController = null) { }

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
        // Ensure name is not empty
        if (string.IsNullOrEmpty(name))
            name = this.name;

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