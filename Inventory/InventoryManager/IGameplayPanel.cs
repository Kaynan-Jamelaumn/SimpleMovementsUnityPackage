/// <summary>
/// A window that takes the mouse while it is open, outside the inventory itself: an NPC's shop or dialogue. Registered on
/// the player's <see cref="InventoryManager"/> (<see cref="InventoryManager.RegisterPanel"/>), it counts as open inventory
/// UI: the cursor stays free, clicks never attack or use the item in hand, inputs that share keys with the UI wait
/// (<see cref="InventoryManager.IsOpenFor"/>), and Escape closes it before the pause menu opens.
/// </summary>
public interface IGameplayPanel
{
    /// <summary>Name for logs ("Shop: Bram's Forge").</summary>
    string PanelName { get; }

    /// <summary>Closes the window (Escape, the pause menu, the inventory going away). Must unregister it.</summary>
    void ClosePanel();
}
