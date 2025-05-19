using UnityEngine;
using UnityEngine.InputSystem;

public static class HotbarHandler
{
    private static int selectedHotbarSlot = 0;
    private static Keyboard keyboard;

    public static int SelectedHotbarSlot { get => selectedHotbarSlot; }

    // Initialize the keyboard reference
    static HotbarHandler()
    {
        keyboard = Keyboard.current;
    }

    public static void CheckForHotbarInput(GameObject[] hotbarSlots, Transform handParent)
    {
        if (keyboard == null) return; // Safety check

        if (keyboard.digit1Key.wasPressedThisFrame)
            selectedHotbarSlot = 0;
        else if (keyboard.digit2Key.wasPressedThisFrame)
            selectedHotbarSlot = 1;
        else if (keyboard.digit3Key.wasPressedThisFrame)
            selectedHotbarSlot = 2;
        else if (keyboard.digit4Key.wasPressedThisFrame)
            selectedHotbarSlot = 3;

        HotbarItemChanged(hotbarSlots, handParent);
    }

    public static void HotbarItemChanged(GameObject[] hotbarSlots, Transform handParent)
    {
        foreach (GameObject slot in hotbarSlots)
        {
            Vector3 scale;

            if (slot == hotbarSlots[SelectedHotbarSlot])
            {
                scale = new Vector3(1.25f, 1.25f, 1.25f);

                // Delete every child inside handparent
                for (int i = 0; i < handParent.childCount; i++)
                {
                    Object.Destroy(handParent.GetChild(i).gameObject);
                }

                if (slot.GetComponent<InventorySlot>().heldItem != null)
                {
                    // Instantiate the prefab of the item in the hand
                    GameObject newItem = Object.Instantiate(
                        hotbarSlots[SelectedHotbarSlot].GetComponent<InventorySlot>().heldItem
                            .GetComponent<InventoryItem>().itemScriptableObject.Prefab);

                    Rigidbody rb = newItem.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Object.Destroy(rb);
                    }

                    // Remove ItemPickable component
                    ItemPickable itemPickable = newItem.GetComponent<ItemPickable>();
                    if (itemPickable != null)
                    {
                        Object.Destroy(itemPickable);
                    }

                    newItem.transform.parent = handParent;
                    newItem.transform.localPosition = hotbarSlots[SelectedHotbarSlot].GetComponent<InventorySlot>()
                        .heldItem.GetComponent<InventoryItem>().itemScriptableObject.Position;
                    newItem.transform.localRotation = Quaternion.Euler(hotbarSlots[SelectedHotbarSlot]
                        .GetComponent<InventorySlot>().heldItem.GetComponent<InventoryItem>()
                        .itemScriptableObject.Rotation);
                    newItem.transform.localScale = hotbarSlots[SelectedHotbarSlot].GetComponent<InventorySlot>()
                        .heldItem.GetComponent<InventoryItem>().itemScriptableObject.Scale;
                }
            }
            else
            {
                scale = new Vector3(1f, 1f, 1f);
            }

            slot.transform.localScale = scale;
        }
    }
}