// Helper class for UI state management
using UnityEngine;

public class UIStateManager
{
    private GameObject inventoryParent;
    private GameObject equippableInventory;
    private GameObject storageParent;

    private bool isInventoryOpened;
    private bool isStorageOpened;
    private bool lastInventoryState;
    private bool lastStorageState;

    public bool IsInventoryOpened => isInventoryOpened;
    public bool IsStorageOpened => isStorageOpened;

    public UIStateManager(GameObject inventoryParent, GameObject equippableInventory, GameObject storageParent)
    {
        this.inventoryParent = inventoryParent;
        this.equippableInventory = equippableInventory;
        this.storageParent = storageParent;

        InitializeUI();
    }

    private void InitializeUI()
    {
        SetUIState(storageParent, false);
        SetUIState(inventoryParent, false);
        SetUIState(equippableInventory, false);
    }

    public void UpdateUI()
    {
        if (lastStorageState != isStorageOpened)
        {
            SetUIState(storageParent, isStorageOpened);
            lastStorageState = isStorageOpened;
        }

        if (lastInventoryState != isInventoryOpened)
        {
            SetUIState(inventoryParent, isInventoryOpened);
            SetUIState(equippableInventory, isInventoryOpened);
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

    private void SetUIState(GameObject uiElement, bool active)
    {
        if (uiElement != null) uiElement.SetActive(active);
    }
}
