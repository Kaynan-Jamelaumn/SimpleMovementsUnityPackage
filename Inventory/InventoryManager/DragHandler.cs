using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The item being dragged in the inventory. While dragged it follows the pointer, draws above everything and does not
/// block raycasts, so the slot under the pointer receives the drop (items used to hide the slot they were dropped on).
/// </summary>
public class DragHandler
{
    private GameObject draggedObject;
    private GameObject lastItemSlotObject;
    private bool isDragging;
    private Mouse mouse;
    private CanvasGroup draggedGroup;
    private bool previousBlocksRaycasts;

    public GameObject DraggedObject => draggedObject;
    public GameObject LastItemSlotObject => lastItemSlotObject;
    public bool IsDragging => isDragging;

    public DragHandler(Mouse mouse)
    {
        this.mouse = mouse;
    }

    public void StartDragging(InventorySlot slot, GameObject slotObject)
    {
        if (slot == null || slot.heldItem == null)
            return;
        isDragging = true;
        draggedObject = slot.heldItem;
        slot.heldItem = null;
        lastItemSlotObject = slotObject != null ? slotObject : slot.gameObject;

        draggedGroup = draggedObject.GetComponent<CanvasGroup>();
        if (draggedGroup == null)
            draggedGroup = draggedObject.AddComponent<CanvasGroup>();
        previousBlocksRaycasts = draggedGroup.blocksRaycasts;
        draggedGroup.blocksRaycasts = false;

        // Items live inside their slot: while dragged, move to the top of the canvas so the item is drawn above every
        // panel and slot, keeping its on-screen size (the slot it returns to stretches it again).
        if (draggedObject.transform is RectTransform rt)
        {
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Vector2 size = rt.rect.size;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            if (canvas != null)
                rt.SetParent(canvas.rootCanvas.transform, true);
        }
        draggedObject.transform.SetAsLastSibling();
    }

    public bool ValidateDragOperation(PointerEventData eventData)
    {
        return draggedObject != null && eventData != null &&
               eventData.pointerCurrentRaycast.gameObject != null &&
               eventData.button == PointerEventData.InputButton.Left;
    }

    public void UpdateDraggedObjectPosition()
    {
        if (draggedObject == null)
            return;
        if (mouse == null)
            mouse = Mouse.current;
        if (mouse != null)
            draggedObject.transform.position = mouse.position.ReadValue();
    }

    public void CleanupDragging()
    {
        if (draggedGroup != null)
            draggedGroup.blocksRaycasts = previousBlocksRaycasts;
        draggedGroup = null;
        draggedObject = null;
        isDragging = false;
    }
}
