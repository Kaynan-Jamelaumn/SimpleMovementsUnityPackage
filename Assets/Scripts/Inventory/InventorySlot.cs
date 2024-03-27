using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SlotType
{
    Common,
    Potion,
    Food,
    Helmet,
    Armor,
    Boots,

}
public class InventorySlot : MonoBehaviour
{
    [Tooltip("the inventory slot item that is being clicked,dragged,split, dropped and etc or just the item in the slot")] public GameObject heldItem;
    [Tooltip("the slot type item the slot can support")][SerializeField] private SlotType slotType = SlotType.Common;

    public SlotType SlotType
    {
        get => slotType;
        set => slotType = value;
    }

    public void SetHeldItem(GameObject item)
    {
        heldItem = item;
        heldItem.transform.position = transform.position;
    }
}
