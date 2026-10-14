using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The storage window (a chest's slots next to the inventory). Opening a <see cref="Storage"/> fills the window's slots
/// from the storage's items; closing saves the slots back into that storage. A network client opens a VIEW instead
/// (<see cref="OpenView"/>): the window with the server's slot count, filled by the server's snapshot, and closing it
/// saves nothing (the server's copy of the storage is the one that changes).
/// </summary>
public class StorageManager
{
    private GameObject storageParent;
    private GameObject itemPrefab;
    private Storage lastStorage;
    private bool isStorageOpened;
    private GameObject[] slotObjects;

    public bool IsStorageOpened => isStorageOpened;

    /// <summary>The storage shown in the window (null: none, or a view of a storage open on the server).</summary>
    public Storage Current => lastStorage;

    /// <summary>Slots shown in the window while it is open.</summary>
    public int ViewSize { get; private set; }

    public StorageManager(GameObject storageParent, GameObject itemPrefab)
    {
        this.storageParent = storageParent;
        this.itemPrefab = itemPrefab;
    }

    /// <summary>Every slot object of the window (shown or not), in order; empty when the window has no slots.</summary>
    public GameObject[] SlotObjects
    {
        get
        {
            Transform parent = SlotsParent;
            int n = parent != null ? parent.childCount : 0;
            if (slotObjects == null || slotObjects.Length != n)
                slotObjects = new GameObject[n];
            for (int i = 0; i < n; i++)
                slotObjects[i] = parent.GetChild(i).gameObject;
            return slotObjects;
        }
    }

    private Transform SlotsParent => storageParent != null && storageParent.transform.childCount > 1 ? storageParent.transform.GetChild(1) : null;

    public void OpenStorage(Storage storage)
    {
        if (storage == null)
            return;
        if (isStorageOpened)
            CloseCurrentStorage(); // the storage shown before is saved first (it used to be overwritten by the next one)
        lastStorage = storage;
        isStorageOpened = true;
        ConfigureStorageUI(storage.size);
        PopulateStorageItems(storage);
    }

    /// <summary>
    /// Opens the window with <paramref name="size"/> slots and no storage behind it: a network client showing a storage
    /// open on the server (the items arrive with the server's snapshot).
    /// </summary>
    public void OpenView(int size)
    {
        if (isStorageOpened && lastStorage != null)
            CloseCurrentStorage();
        lastStorage = null;
        isStorageOpened = true;
        ConfigureStorageUI(size);
    }

    /// <summary>Closes a view opened with <see cref="OpenView"/>: its items are only removed from the window.</summary>
    public void CloseView()
    {
        if (lastStorage != null)
        {
            CloseCurrentStorage();
            return;
        }
        ClearExistingStorageItems();
        isStorageOpened = false;
        ViewSize = 0;
    }

    /// <summary>
    /// Closes the window, saving its slots into the storage that is shown (whatever <paramref name="storage"/> is: saving
    /// one storage's window into another one duplicated items).
    /// </summary>
    public void CloseStorage(Storage storage)
    {
        if (lastStorage != null)
            SaveStorageItems(lastStorage);
        else if (isStorageOpened)
            ClearExistingStorageItems(); // a view: nothing to save
        lastStorage = null;
        isStorageOpened = false;
        ViewSize = 0;
    }

    public void CloseCurrentStorage()
    {
        if (lastStorage != null) CloseStorage(lastStorage);
        else if (isStorageOpened) CloseView();
        isStorageOpened = false;
    }

    private void ConfigureStorageUI(int size)
    {
        ViewSize = Mathf.Max(0, size);
        SetupStorageSlots(ViewSize);
        SetupStorageBackground(ViewSize);
    }

    private void SetupStorageSlots(int size)
    {
        Transform slotsParent = SlotsParent;
        if (slotsParent == null)
            return;

        // Deactivate all slots
        for (int i = 0; i < slotsParent.childCount; i++)
            slotsParent.GetChild(i).gameObject.SetActive(false);

        // Activate required slots
        for (int i = 0; i < size && i < slotsParent.childCount; i++)
            slotsParent.GetChild(i).gameObject.SetActive(true);
    }

    private void SetupStorageBackground(int size)
    {
        if (storageParent == null || storageParent.transform.childCount == 0)
            return;
        var backgroundTransform = storageParent.transform.GetChild(0);
        float sizeY = (float)Mathf.CeilToInt(size / 4f) / 4;
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
        // Items live inside the storage slots (child 1 of the storage panel).
        Transform slotsParent = SlotsParent;
        if (slotsParent == null)
            return;
        for (int i = 0; i < slotsParent.childCount; i++)
        {
            var slot = slotsParent.GetChild(i).GetComponent<InventorySlot>();
            if (slot != null && slot.heldItem != null)
            {
                Object.Destroy(slot.heldItem);
                slot.heldItem = null;
            }
        }
    }

    private void CreateStorageItem(StorageItem storageItem, int index)
    {
        Transform slotsParent = SlotsParent;
        if (slotsParent == null || index >= slotsParent.childCount || itemPrefab == null)
            return;
        var slot = slotsParent.GetChild(index).GetComponent<InventorySlot>();
        if (slot == null)
            return;
        var newItem = Object.Instantiate(itemPrefab);
        var itemComponent = newItem.GetComponent<InventoryItem>();
        itemComponent.Initialize(storageItem.itemScriptableObject, storageItem.currentStack, storageItem.durabilities);
        // What the item had when it was stored (uses left, rounds loaded) comes back with it.
        if (storageItem.durability > 0f)
            itemComponent.durability = storageItem.durability;
        itemComponent.loadedRounds = storageItem.loadedRounds;
        slot.SetHeldItem(newItem);
    }

    private void SaveStorageItems(Storage storage)
    {
        Transform slotsParent = SlotsParent;
        if (slotsParent == null || storage == null)
            return;
        storage.items.Clear();

        for (int i = 0; i < slotsParent.childCount; i++)
        {
            var slot = slotsParent.GetChild(i);
            var slotComponent = slot.GetComponent<InventorySlot>();

            // The slots the window shows (not "active in the hierarchy": on a server the panel of another player's
            // inventory may be hidden, and every item would have been saved as empty).
            if (i < ViewSize && slotComponent.Live()?.heldItem != null)
            {
                var inventoryItem = slotComponent.heldItem.GetComponent<InventoryItem>();
                storage.items.Add(new StorageItem(inventoryItem.stackCurrent, inventoryItem.itemScriptableObject)
                {
                    durabilities = new List<int>(inventoryItem.DurabilityList),
                    durability = inventoryItem.durability,
                    loadedRounds = inventoryItem.loadedRounds,
                });
            }
            else
            {
                storage.items.Add(new StorageItem(0, null));
            }
        }
    }
}
