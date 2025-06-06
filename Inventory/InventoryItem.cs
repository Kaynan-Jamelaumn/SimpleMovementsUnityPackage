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

    // Cached UI state for performance
    private UICache uiCache;

    // Properties
    public List<int> DurabilityList { get => durabilityList; set => durabilityList = value ?? new List<int>(); }
    public Image IconImage { get => iconImage; set => iconImage = value; }
    public Text StackText { get => stackText; set => stackText = value; }

    private void Awake()
    {
        uiCache = new UICache();
    }

    private void Start()
    {
        if (itemScriptableObject != null)
        {
            InitializeFromScriptableObject();
        }
    }

    private void Update()
    {
        UpdateUI();
    }

    // Initialize item from ItemPickable data
    public void Initialize(ItemPickable pickedItem)
    {
        if (pickedItem?.itemScriptableObject == null) return;

        itemScriptableObject = pickedItem.itemScriptableObject;
        stackCurrent = pickedItem.quantity;
        stackMax = itemScriptableObject.StackMax;
        DurabilityList = pickedItem.DurabilityList ?? new List<int>();

        if (DurabilityList.Count > 0)
            durability = DurabilityList[DurabilityList.Count - 1];

        UpdateTotalWeight();
        uiCache.MarkForUpdate();
    }

    // Initialize from ScriptableObject (for existing items)
    private void InitializeFromScriptableObject()
    {
        stackMax = itemScriptableObject.StackMax;
        UpdateTotalWeight();
        uiCache.MarkForUpdate();
    }

    // Optimized UI update with caching
    private void UpdateUI()
    {
        if (!uiCache.NeedsUpdate()) return;

        UpdateIcon();
        UpdateStackDisplay();
        uiCache.ClearUpdateFlag();
    }

    private void UpdateIcon()
    {
        if (iconImage == null || itemScriptableObject?.Icon == null) return;

        if (uiCache.LastSprite != itemScriptableObject.Icon)
        {
            iconImage.sprite = itemScriptableObject.Icon;
            uiCache.LastSprite = itemScriptableObject.Icon;
        }
    }

    private void UpdateStackDisplay()
    {
        if (stackText == null) return;

        bool shouldShow = stackMax > 1;
        string currentText = shouldShow ? stackCurrent.ToString() : "";

        if (uiCache.LastStackText != currentText)
        {
            stackText.text = currentText;
            uiCache.LastStackText = currentText;
        }
    }

    // Stack management
    public bool CanStackWith(InventoryItem other)
    {
        return other != null &&
               itemScriptableObject != null &&
               other.itemScriptableObject != null &&
               itemScriptableObject == other.itemScriptableObject &&
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
        uiCache.MarkForUpdate();

        return amountToAdd == amount;
    }

    public bool RemoveFromStack(int amount)
    {
        if (amount <= 0 || amount > stackCurrent) return false;

        stackCurrent -= amount;
        UpdateTotalWeight();
        uiCache.MarkForUpdate();

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
    public bool IsEmpty() => stackCurrent <= 0;
    public bool IsFull() => stackCurrent >= stackMax;
    public float GetWeightPerItem() => itemScriptableObject?.Weight ?? 0f;
    public bool IsValid() => itemScriptableObject != null && stackCurrent > 0 && stackCurrent <= stackMax;

    // Split item functionality
    public InventoryItem CreateSplitItem(int amountToSplit, GameObject itemPrefab)
    {
        if (amountToSplit <= 0 || amountToSplit >= stackCurrent) return null;

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

    private void OnDestroy()
    {
        durabilityList?.Clear();
    }

    // UI caching helper class
    private class UICache
    {
        public Sprite LastSprite { get; set; }
        public string LastStackText { get; set; } = "";
        private bool needsUpdate = true;

        public bool NeedsUpdate() => needsUpdate;
        public void MarkForUpdate() => needsUpdate = true;
        public void ClearUpdateFlag() => needsUpdate = false;
    }
}