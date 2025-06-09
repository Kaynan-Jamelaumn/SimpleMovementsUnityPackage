using UnityEngine;

// Enhanced UI state manager with better error checking and debugging
[System.Serializable]
public class UIStateManager
{
    [Header("UI Panel References")]
    [SerializeField] private GameObject inventoryPanel;

    [SerializeField, Tooltip("Equipment panel to show/hide")]
    private GameObject equipmentPanel;

    [SerializeField, Tooltip("Storage panel to show/hide")]
    private GameObject storagePanel;

    [Header("Debug Settings")]
    [SerializeField, Tooltip("Enable debug logging for UI state changes")]
    private bool enableDebugLogging = false;

    // State tracking
    private bool isInventoryOpened;
    private bool isStorageOpened;
    private bool lastInventoryState;
    private bool lastStorageState;

    // Initialization tracking
    private bool isInitialized = false;

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

        ValidateReferences();
        InitializeUI();
        isInitialized = true;
    }

    private void ValidateReferences()
    {
        if (inventoryPanel == null)
        {
            Debug.LogError("UIStateManager: inventoryPanel reference is null! Make sure to assign the inventory panel in the InventoryManager inspector.");
        }

        if (equipmentPanel == null)
        {
            Debug.LogWarning("UIStateManager: equipmentPanel reference is null. Equipment functionality will not work.");
        }

        if (storagePanel == null)
        {
            Debug.LogWarning("UIStateManager: storagePanel reference is null. Storage functionality will not work.");
        }

        if (enableDebugLogging)
        {
            Debug.Log($"UIStateManager references - Inventory: {(inventoryPanel != null ? inventoryPanel.name : "NULL")}, Equipment: {(equipmentPanel != null ? equipmentPanel.name : "NULL")}, Storage: {(storagePanel != null ? storagePanel.name : "NULL")}");
        }
    }

    private void InitializeUI()
    {
        if (enableDebugLogging)
        {
            Debug.Log("UIStateManager: Initializing UI - setting all panels to closed state");
        }

        // Set all panels to closed state initially
        SetUIState(storagePanel, false);
        SetUIState(inventoryPanel, false);
        SetUIState(equipmentPanel, false);

        // Initialize state tracking
        lastStorageState = isStorageOpened;
        lastInventoryState = isInventoryOpened;

        if (enableDebugLogging)
        {
            Debug.Log("UIStateManager: UI initialization complete");
        }
    }

    // State Management
    public void UpdateUI()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("UIStateManager: UpdateUI called before initialization!");
            return;
        }

        // Only update panels when state actually changes for performance
        if (lastStorageState != isStorageOpened)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"UIStateManager: Storage state changed from {lastStorageState} to {isStorageOpened}");
            }

            SetUIState(storagePanel, isStorageOpened);
            lastStorageState = isStorageOpened;
        }

        if (lastInventoryState != isInventoryOpened)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"UIStateManager: Inventory state changed from {lastInventoryState} to {isInventoryOpened}");
            }

            SetUIState(inventoryPanel, isInventoryOpened);
            SetUIState(equipmentPanel, isInventoryOpened);
            lastInventoryState = isInventoryOpened;
        }
    }

    public void SetInventoryOpened(bool opened)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"UIStateManager: SetInventoryOpened called with value: {opened}");
        }

        isInventoryOpened = opened;
    }

    public void SetStorageOpened(bool opened)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"UIStateManager: SetStorageOpened called with value: {opened}");
        }

        isStorageOpened = opened;
    }

    public void CloseAllPanels()
    {
        if (enableDebugLogging)
        {
            Debug.Log("UIStateManager: Closing all panels");
        }

        SetInventoryOpened(false);
        SetStorageOpened(false);
    }

    public void ToggleInventory()
    {
        bool newState = !isInventoryOpened;
        if (enableDebugLogging)
        {
            Debug.Log($"UIStateManager: Toggling inventory from {isInventoryOpened} to {newState}");
        }

        SetInventoryOpened(newState);
    }

    public void ToggleStorage()
    {
        bool newState = !isStorageOpened;
        if (enableDebugLogging)
        {
            Debug.Log($"UIStateManager: Toggling storage from {isStorageOpened} to {newState}");
        }

        SetStorageOpened(newState);
    }

    // Utility Methods
    private void SetUIState(GameObject uiElement, bool active)
    {
        if (uiElement == null)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning("UIStateManager: Attempted to set state on null UI element");
            }
            return;
        }

        if (uiElement.activeSelf != active)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"UIStateManager: Setting {uiElement.name} active state to {active}");
            }

            try
            {
                uiElement.SetActive(active);

                if (enableDebugLogging)
                {
                    Debug.Log($"UIStateManager: Successfully set {uiElement.name} to {active}. Current state: {uiElement.activeSelf}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"UIStateManager: Error setting {uiElement.name} active state: {e.Message}");
            }
        }
        else if (enableDebugLogging)
        {
            Debug.Log($"UIStateManager: {uiElement.name} already in state {active}, skipping");
        }
    }

    public void ForceRefresh()
    {
        if (enableDebugLogging)
        {
            Debug.Log("UIStateManager: Force refreshing all panels");
        }

        // Force update all panels regardless of state tracking
        SetUIState(inventoryPanel, isInventoryOpened);
        SetUIState(equipmentPanel, isInventoryOpened);
        SetUIState(storagePanel, isStorageOpened);

        lastInventoryState = isInventoryOpened;
        lastStorageState = isStorageOpened;
    }

    public void ValidatePanelReferences()
    {
        bool hasErrors = false;

        if (inventoryPanel == null)
        {
            Debug.LogError("UIStateManager: Inventory panel reference is null");
            hasErrors = true;
        }
        if (equipmentPanel == null)
        {
            Debug.LogWarning("UIStateManager: Equipment panel reference is null");
        }
        if (storagePanel == null)
        {
            Debug.LogWarning("UIStateManager: Storage panel reference is null");
        }

        if (!hasErrors)
        {
            Debug.Log("UIStateManager: All critical panel references are valid");
        }
    }

    // Debug Methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogCurrentState()
    {
        Debug.Log($"UIStateManager State - Inventory: {isInventoryOpened} (last: {lastInventoryState}), Storage: {isStorageOpened} (last: {lastStorageState})");

        if (inventoryPanel != null)
        {
            Debug.Log($"Inventory Panel '{inventoryPanel.name}' - Active: {inventoryPanel.activeSelf}, ActiveInHierarchy: {inventoryPanel.activeInHierarchy}");
        }

        if (equipmentPanel != null)
        {
            Debug.Log($"Equipment Panel '{equipmentPanel.name}' - Active: {equipmentPanel.activeSelf}, ActiveInHierarchy: {equipmentPanel.activeInHierarchy}");
        }

        if (storagePanel != null)
        {
            Debug.Log($"Storage Panel '{storagePanel.name}' - Active: {storagePanel.activeSelf}, ActiveInHierarchy: {storagePanel.activeInHierarchy}");
        }
    }

    // Public methods to enable/disable debug logging
    public void SetDebugLogging(bool enabled)
    {
        enableDebugLogging = enabled;
        if (enabled)
        {
            Debug.Log("UIStateManager: Debug logging enabled");
        }
    }

    // Method to manually set panel references (useful for dynamic setup)
    public void SetPanelReferences(GameObject inventory, GameObject equipment, GameObject storage)
    {
        inventoryPanel = inventory;
        equipmentPanel = equipment;
        storagePanel = storage;

        ValidateReferences();

        if (enableDebugLogging)
        {
            Debug.Log("UIStateManager: Panel references updated");
        }
    }

    // Method to check if UI system is working properly
    public bool PerformUITest()
    {
        if (inventoryPanel == null)
        {
            Debug.LogError("UIStateManager: Cannot perform UI test - inventory panel is null");
            return false;
        }

        Debug.Log("UIStateManager: Performing UI test...");

        // Test opening inventory
        bool originalState = inventoryPanel.activeSelf;

        SetInventoryOpened(true);
        UpdateUI();

        if (!inventoryPanel.activeSelf)
        {
            Debug.LogError("UIStateManager: UI test failed - inventory panel did not activate");
            return false;
        }

        SetInventoryOpened(false);
        UpdateUI();

        if (inventoryPanel.activeSelf)
        {
            Debug.LogError("UIStateManager: UI test failed - inventory panel did not deactivate");
            return false;
        }

        // Restore original state
        inventoryPanel.SetActive(originalState);

        Debug.Log("UIStateManager: UI test passed!");
        return true;
    }
}