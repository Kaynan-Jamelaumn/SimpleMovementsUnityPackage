// Helper class for drag operations
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine;

public class DragHandler
{
    private GameObject draggedObject;
    private GameObject lastItemSlotObject;
    private bool isDragging;
    private Mouse mouse;

    public GameObject DraggedObject => draggedObject;
    public GameObject LastItemSlotObject => lastItemSlotObject;
    public bool IsDragging => isDragging;

    public DragHandler(Mouse mouse)
    {
        this.mouse = mouse;
    }

    public void StartDragging(InventorySlot slot, GameObject clickedObject)
    {
        isDragging = true;
        draggedObject = slot.heldItem;
        slot.heldItem = null;
        lastItemSlotObject = clickedObject;
    }

    public bool ValidateDragOperation(PointerEventData eventData)
    {
        if (draggedObject == null || eventData.pointerCurrentRaycast.gameObject == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            isDragging = false;
            return false;
        }
        return true;
    }

    public void UpdateDraggedObjectPosition()
    {
        if (draggedObject != null && mouse != null)
        {
            draggedObject.transform.position = mouse.position.ReadValue();
        }
    }

    public void CleanupDragging()
    {
        draggedObject = null;
        isDragging = false;
    }
}

