using UnityEngine;

// Simple UI state manager for basic show/hide functionality
[System.Serializable]
public class UIStateManager
{
    [Header("UI Panel References")]
    [SerializeField, Tooltip("Main inventory panel to show/hide")]
    private GameObject inventoryPanel;

    [SerializeField, Tooltip("Equipment panel to show/hide")]
    private GameObject equipmentPanel;

    [SerializeField, Tooltip("Storage panel to show/hide")]
    private GameObject storagePanel;

    // State tracking
    private bool isInventoryOpened;
    private bool isStorageOpened;
    private bool lastInventoryState;
    private bool lastStorageState;

    // Properties
    public bool IsInventoryOpened => isInventoryOpened;
    public bool IsStorageOpened => isStorageOpened;
    public bool IsAnyPanelOpen => isInventoryOpened || isStorageOpened;

    // Initialization

    public UIStateManager(GameObject inventoryParent, GameObject equippableInventory, GameObject storageParent)
    {
        this.inventoryPanel = inventoryParent;
        this.equipmentPanel = equippableInventory;
        this.storagePanel = storageParent;

        InitializeUI();
    }

    private void InitializeUI()
    {
        // Set all panels to closed state initially
        SetUIState(storagePanel, false);
        SetUIState(inventoryPanel, false);
        SetUIState(equipmentPanel, false);

        // Initialize state tracking
        lastStorageState = isStorageOpened;
        lastInventoryState = isInventoryOpened;
    }

    

    // State Management

    public void UpdateUI()
    {
        // Only update panels when state actually changes for performance
        if (lastStorageState != isStorageOpened)
        {
            SetUIState(storagePanel, isStorageOpened);
            lastStorageState = isStorageOpened;
        }

        if (lastInventoryState != isInventoryOpened)
        {
            SetUIState(inventoryPanel, isInventoryOpened);
            SetUIState(equipmentPanel, isInventoryOpened);
            lastInventoryState = isInventoryOpened;
        }
    }

    public void SetInventoryOpened(bool opened)
    {
        isInventoryOpened = opened;
    }

    public void SetStorageOpened(bool opened)
    {
        isStorageOpened = opened;
    }

    public void CloseAllPanels()
    {
        SetInventoryOpened(false);
        SetStorageOpened(false);
    }

    public void ToggleInventory()
    {
        SetInventoryOpened(!isInventoryOpened);
    }

    public void ToggleStorage()
    {
        SetStorageOpened(!isStorageOpened);
    }

    

    // Utility Methods

    private void SetUIState(GameObject uiElement, bool active)
    {
        if (uiElement != null && uiElement.activeSelf != active)
        {
            uiElement.SetActive(active);
        }
    }

    public void ForceRefresh()
    {
        // Force update all panels regardless of state tracking
        SetUIState(inventoryPanel, isInventoryOpened);
        SetUIState(equipmentPanel, isInventoryOpened);
        SetUIState(storagePanel, isStorageOpened);

        lastInventoryState = isInventoryOpened;
        lastStorageState = isStorageOpened;
    }

    public void ValidatePanelReferences()
    {
        if (inventoryPanel == null)
            Debug.LogWarning("Inventory panel reference is null in UIStateManager");
        if (equipmentPanel == null)
            Debug.LogWarning("Equipment panel reference is null in UIStateManager");
        if (storagePanel == null)
            Debug.LogWarning("Storage panel reference is null in UIStateManager");
    }

    

    // Debug Methods

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogCurrentState()
    {
        Debug.Log($"UI State - Inventory: {isInventoryOpened}, Storage: {isStorageOpened}");
    }

    
}