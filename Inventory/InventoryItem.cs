using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryItem : MonoBehaviour
{
    [Header("Item Data")]
    public ItemSO itemScriptableObject;

    [Header("Stack Information")]
    public int stackCurrent = 1;
    public int stackMax;

    [Header("Item Properties")]
    public float totalWeight;
    public float timeSinceLastUse;
    public float durability;
    public bool isEquipped;

    [Header("UI Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text stackText;

    // Durability system
    private List<int> durabilityList = new List<int>();

    // Cached values for performance
    private bool hasValidStackMax;
    private bool shouldShowStackText;

    // Properties
    public List<int> DurabilityList { get => durabilityList; set => durabilityList = value ?? new List<int>(); }
    public Image IconImage { get => iconImage; set => iconImage = value; }
    public Text StackText { get => stackText; set => stackText = value; }

    private void Start()
    {
        InitializeItem();
    }

    private void Update()
    {
        UpdateUI();
    }

    // Initialize item with cached values for performance
    private void InitializeItem()
    {
        if (itemScriptableObject != null)
        {
            stackMax = itemScriptableObject.StackMax;
            hasValidStackMax = stackMax >= 1;
            UpdateTotalWeight();
        }
        else
        {
            Debug.LogWarning($"ItemSO is null on {gameObject.name}");
        }
    }

    // Optimized UI update with caching
    private void UpdateUI()
    {
        UpdateIcon();
        UpdateStackDisplay();
    }

    private void UpdateIcon()
    {
        if (iconImage != null && itemScriptableObject?.Icon != null)
        {
            // Only update if the sprite has changed
            if (iconImage.sprite != itemScriptableObject.Icon)
                iconImage.sprite = itemScriptableObject.Icon;
        }
    }

    private void UpdateStackDisplay()
    {
        if (stackText != null && hasValidStackMax)
        {
            bool shouldShow = stackMax > 1;

            // Only update text if visibility changed or stack count changed
            if (shouldShow != shouldShowStackText || shouldShow)
            {
                stackText.text = shouldShow ? stackCurrent.ToString() : "";
                shouldShowStackText = shouldShow;
            }
        }
    }

    // Stack management
    public bool CanStackWith(InventoryItem other)
    {
        if (other == null || itemScriptableObject == null || other.itemScriptableObject == null)
            return false;

        return itemScriptableObject == other.itemScriptableObject &&
               stackCurrent < stackMax;
    }

    public int GetAvailableStackSpace()
    {
        return Mathf.Max(0, stackMax - stackCurrent);
    }

    public bool AddToStack(int amount)
    {
        if (amount <= 0) return false;

        int availableSpace = GetAvailableStackSpace();
        if (availableSpace <= 0) return false;

        int amountToAdd = Mathf.Min(amount, availableSpace);
        stackCurrent += amountToAdd;
        UpdateTotalWeight();

        return amountToAdd == amount; // Returns true if all items were added
    }

    public bool RemoveFromStack(int amount)
    {
        if (amount <= 0 || amount > stackCurrent) return false;

        stackCurrent -= amount;
        UpdateTotalWeight();

        return true;
    }

    // Durability management
    public void AddDurability(int durabilityValue)
    {
        durabilityList?.Add(durabilityValue);
    }

    public void AddDurabilityRange(IEnumerable<int> durabilityValues)
    {
        durabilityList?.AddRange(durabilityValues);
    }

    public bool ConsumeDurability()
    {
        if (durabilityList == null || durabilityList.Count == 0) return false;

        int lastIndex = durabilityList.Count - 1;
        durabilityList.RemoveAt(lastIndex);

        // Update current durability if there are more items
        if (durabilityList.Count > 0)
            durability = durabilityList[durabilityList.Count - 1];
        else
            durability = 0;

        return true;
    }

    public int GetRemainingDurability()
    {
        return durabilityList?.Count ?? 0;
    }

    // Weight management
    public void UpdateTotalWeight()
    {
        if (itemScriptableObject != null)
            totalWeight = itemScriptableObject.Weight * stackCurrent;
    }

    // Equipment status
    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
    }

    // Cooldown management
    public bool IsOnCooldown()
    {
        return Time.time < timeSinceLastUse;
    }

    public void SetCooldown(float cooldownDuration)
    {
        timeSinceLastUse = Time.time + cooldownDuration;
    }

    public float GetRemainingCooldown()
    {
        return Mathf.Max(0, timeSinceLastUse - Time.time);
    }

    // Utility methods
    public bool IsEmpty()
    {
        return stackCurrent <= 0;
    }

    public bool IsFull()
    {
        return stackCurrent >= stackMax;
    }

    public float GetWeightPerItem()
    {
        return itemScriptableObject?.Weight ?? 0f;
    }

    // Item validation
    public bool IsValid()
    {
        return itemScriptableObject != null && stackCurrent > 0 && stackCurrent <= stackMax;
    }

    // Split item functionality
    public InventoryItem CreateSplitItem(int amountToSplit, GameObject itemPrefab)
    {
        if (amountToSplit <= 0 || amountToSplit >= stackCurrent) return null;

        // Create new item
        GameObject newItemObject = Instantiate(itemPrefab);
        InventoryItem newItem = newItemObject.GetComponent<InventoryItem>();

        if (newItem == null) return null;

        // Setup new item
        newItem.itemScriptableObject = itemScriptableObject;
        newItem.stackCurrent = amountToSplit;
        newItem.stackMax = stackMax;
        newItem.isEquipped = false;

        // Transfer durability
        TransferDurabilityToItem(newItem, amountToSplit);

        // Update this item
        RemoveFromStack(amountToSplit);

        return newItem;
    }

    private void TransferDurabilityToItem(InventoryItem targetItem, int amount)
    {
        if (durabilityList == null || targetItem == null) return;

        var transferredDurability = new List<int>();
        int transferCount = Mathf.Min(amount, durabilityList.Count);

        for (int i = 0; i < transferCount; i++)
        {
            int lastIndex = durabilityList.Count - 1;
            transferredDurability.Add(durabilityList[lastIndex]);
            durabilityList.RemoveAt(lastIndex);
        }

        targetItem.DurabilityList = transferredDurability;

        // Update durability values
        if (targetItem.DurabilityList.Count > 0)
            targetItem.durability = targetItem.DurabilityList[targetItem.DurabilityList.Count - 1];

        if (durabilityList.Count > 0)
            durability = durabilityList[durabilityList.Count - 1];
    }

    // Debug information
    public string GetDebugInfo()
    {
        return $"Item: {itemScriptableObject?.Name ?? "NULL"}, Stack: {stackCurrent}/{stackMax}, " +
               $"Weight: {totalWeight:F2}, Durability: {durability}, Equipped: {isEquipped}";
    }

    // Cleanup
    private void OnDestroy()
    {
        durabilityList?.Clear();
    }
}