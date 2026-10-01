using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards pointer events from the inventory UI to the <see cref="InventoryManager"/> when the manager is not a
/// parent of the UI (for example when it sits on the player and the UI on its own canvas). Put it on the canvas or
/// panel that contains the slots; the UI Builder adds it automatically.
/// </summary>
[DisallowMultipleComponent]
public class InventoryPointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Tooltip("The inventory that receives the events. Empty = the one in the parents, or the only one in the scene.")]
    [SerializeField] private InventoryManager inventoryManager;

    public InventoryManager Target
    {
        get
        {
            if (inventoryManager == null)
            {
                // This player's inventory (each player carries its own UI): parents, then the player's hierarchy.
                // A scene-wide search is only used when there is exactly one inventory (it could pick another player's).
                inventoryManager = GetComponentInParent<InventoryManager>();
                if (inventoryManager == null)
                    inventoryManager = transform.root.GetComponentInChildren<InventoryManager>(true);
                if (inventoryManager == null)
                    inventoryManager = InventoryUtils.OnlyInstance<InventoryManager>();
            }
            return inventoryManager;
        }
        set => inventoryManager = value;
    }

    // When the manager is a parent it receives the events itself (Unity sends them up the hierarchy to the first
    // handler, which is this relay): forward in every case so there is exactly one handler.
    public void OnPointerDown(PointerEventData eventData) => Target?.OnPointerDown(eventData);
    public void OnPointerUp(PointerEventData eventData) => Target?.OnPointerUp(eventData);
    public void OnPointerClick(PointerEventData eventData) => Target?.OnPointerClick(eventData);
}
