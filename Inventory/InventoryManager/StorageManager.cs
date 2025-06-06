

using UnityEngine;

public class StorageManager
{
    private GameObject storageParent;
    private GameObject itemPrefab;
    private Storage lastStorage;
    private bool isStorageOpened;

    public bool IsStorageOpened => isStorageOpened;

    public StorageManager(GameObject storageParent, GameObject itemPrefab)
    {
        this.storageParent = storageParent;
        this.itemPrefab = itemPrefab;
    }

    public void OpenStorage(Storage storage)
    {
        lastStorage = storage;
        isStorageOpened = true;
        ConfigureStorageUI(storage);
        PopulateStorageItems(storage);
    }

    public void CloseStorage(Storage storage)
    {
        SaveStorageItems(storage);
        lastStorage = null;
        isStorageOpened = false;
    }

    public void CloseCurrentStorage()
    {
        if (lastStorage != null) CloseStorage(lastStorage);
        isStorageOpened = false;
    }

    private void ConfigureStorageUI(Storage storage)
    {
        SetupStorageSlots(storage);
        SetupStorageBackground(storage);
    }

    private void SetupStorageSlots(Storage storage)
    {
        var slotsParent = storageParent.transform.GetChild(1);

        // Deactivate all slots
        for (int i = 0; i < slotsParent.childCount; i++)
            slotsParent.GetChild(i).gameObject.SetActive(false);

        // Activate required slots
        for (int i = 0; i < storage.size; i++)
            slotsParent.GetChild(i).gameObject.SetActive(true);
    }

    private void SetupStorageBackground(Storage storage)
    {
        var backgroundTransform = storageParent.transform.GetChild(0);
        float sizeY = (float)Mathf.CeilToInt(storage.size / 4f) / 4;
        float posY = (1 - sizeY) * 230;

        backgroundTransform.localScale = new Vector2(1, sizeY);
        backgroundTransform.localPosition = new Vector2(-615, 130 + posY);
    }

    private void PopulateStorageItems(Storage storage)
    {
        ClearExistingStorageItems();

        for (int i = 0; i < storage.items.Count; i++)
        {
            var storageItem = storage.items[i];
            if (storageItem?.itemScriptableObject != null)
                CreateStorageItem(storageItem, i);
        }
    }

    private void ClearExistingStorageItems()
    {
        var itemsParent = storageParent.transform.GetChild(2);
        for (int i = itemsParent.childCount - 1; i >= 0; i--)
            Object.Destroy(itemsParent.GetChild(i).gameObject);
    }

    private void CreateStorageItem(StorageItem storageItem, int index)
    {
        var newItem = Object.Instantiate(itemPrefab);
        var itemComponent = newItem.GetComponent<InventoryItem>();

        itemComponent.itemScriptableObject = storageItem.itemScriptableObject;
        itemComponent.stackCurrent = storageItem.currentStack;

        var slot = storageParent.transform.GetChild(1).GetChild(index);
        newItem.transform.SetParent(slot.parent.parent.GetChild(2));
        newItem.transform.localScale = Vector3.one;

        slot.GetComponent<InventorySlot>().SetHeldItem(newItem);
    }

    private void SaveStorageItems(Storage storage)
    {
        var slotsParent = storageParent.transform.GetChild(1);
        storage.items.Clear();

        for (int i = 0; i < slotsParent.childCount; i++)
        {
            var slot = slotsParent.GetChild(i);
            var slotComponent = slot.GetComponent<InventorySlot>();

            if (slot.gameObject.activeInHierarchy && slotComponent?.heldItem != null)
            {
                var inventoryItem = slotComponent.heldItem.GetComponent<InventoryItem>();
                storage.items.Add(new StorageItem(inventoryItem.stackCurrent, inventoryItem.itemScriptableObject));
            }
            else
            {
                storage.items.Add(new StorageItem(0, null));
            }
        }
    }
}