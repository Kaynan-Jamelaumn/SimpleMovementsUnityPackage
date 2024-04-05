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
    [SerializeField] protected new string name;
    [SerializeField] protected Sprite icon;
    [Tooltip("the item prefab")][SerializeField] protected GameObject prefab;
    [Tooltip("how mauch the same item can be contained in one slot")][SerializeField] protected int stackMax;
    [SerializeField] protected float weight;
    [SerializeField] protected float price;
    [SerializeField] protected int maxDurability;
    [SerializeField] protected int durability = 1;
    [SerializeField] protected int durabilityReductionPerUse;
    [SerializeField] protected bool shouldBeDestroyedOn0UsesLeft = true;
    [SerializeField] protected ItemType itemType;
    [SerializeField] protected string description; 
    [Tooltip("coolDown for the same item to be used again")][SerializeField] protected float cooldown = 0;
    [Header("Item Hand Position")]
    [Header("Position")]
    [SerializeField] protected Vector3 position;

    [Header("Rotation")]
    [SerializeField] protected Vector3 rotation = new Vector3(80f,-20f,0);

    [Header("Scale")]
    [SerializeField] protected Vector3 scale = new Vector3(1, 1, 1);
    [Header("Animation")]
    [SerializeField] protected AnimationClip useAnimation;

    [Header("Audio")]
    [SerializeField] protected AudioClip useAudioClip;
    [Header("Particles")]
    [SerializeField] protected ParticleSystem useParticles;
    [SerializeField] protected float pickUpTime;
    public float PickUpTime
    {
        get => pickUpTime;
        set => pickUpTime = value;
    } 
    public string Name
    {
        get => name;
        set => name = value; 
    }    
    public string Description
    {
        get => description;
    }
    public ItemType ItemType
    {
        get => itemType;
    }

    public Sprite Icon
    {
        get => icon; 
    }

    public GameObject Prefab
    {
        get => prefab; 
    }
    public int StackMax
    {
        get => stackMax; 
    }

    public float Weight
    {
        get => weight;
        set => weight = value; 
    }
    
    public float Price
    {
        get => price;
        set => price = value; 
    }

    
    public int MaxDurability
    {
        get => maxDurability; 
    }

    public int Durability
    {
        get => durability;
    }

    
    public int DurabilityReductionPerUse
    {
        get => durabilityReductionPerUse; 
    }

    
    public bool ShouldBeDestroyedOn0UsesLeft
    {
        get => shouldBeDestroyedOn0UsesLeft;
    }

    public float Cooldown
    {
        get => cooldown;
    }

    //-16.8 56.58 0.067 -0.04 0.659 

    public Vector3 Position
    {
        get => position;
        set => position = value;
    }
    
    public Vector3 Rotation
    {
        get => rotation;
        set => rotation = value;
    }
    
    public Vector3 Scale
    {
        get => scale;
        set => scale = value;
    }

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

}
