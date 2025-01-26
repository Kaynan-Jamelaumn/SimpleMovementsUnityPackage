using UnityEngine;
public enum ItemType
{
    Potion,
    Food,
    Weapon,
    Helmet,
    Armor,
    Boots,

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
    [SerializeField] protected int durabilityReductionPerUse;
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

    //-16.8 56.58 0.067 -0.04 0.659 
    public virtual void ApplyEquippedStats(bool shouldApply = false, PlayerStatusController statusController = null)
    {

    }

    public virtual void UseItem(GameObject player, PlayerStatusController statusController)
    {
        if (durability <= 0) return;
        durability -= durabilityReductionPerUse;
        // Play animation if available
        InteractionEffects.ApplyEffects(prefab, useAnimation, useAudioClip, useParticles);

    }

    public virtual void UseItem(GameObject player, PlayerStatusController statusController, WeaponController weaponController, AttackType attackType = AttackType.Normal)
    {
        if (durability <= 0) return;
        durability -= durabilityReductionPerUse;
        // Play animation if available
        InteractionEffects.ApplyEffects(prefab, useAnimation, useAudioClip, useParticles);


    }

}
