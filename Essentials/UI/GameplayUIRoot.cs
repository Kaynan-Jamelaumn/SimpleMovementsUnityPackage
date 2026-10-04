using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Finds the canvas that a player's gameplay windows go on (interaction prompt, NPC dialogue, shops): the canvas of the
/// player's inventory UI, so everything a player sees stays together, else one overlay canvas created at runtime.
/// </summary>
public static class GameplayUIRoot
{
    private static Canvas generated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => generated = null;

    /// <summary>The canvas for UI of the player owning <paramref name="anyOnPlayer"/> (never null).</summary>
    public static Canvas CanvasFor(Component anyOnPlayer)
    {
        Canvas c = InventoryCanvas(anyOnPlayer != null ? InventoryManager.For(anyOnPlayer) : null);
        return c != null ? c : Generated();
    }

    private static Canvas InventoryCanvas(InventoryManager inventory)
    {
        if (inventory == null)
            return null;
        Canvas c = inventory.GetComponentInParent<Canvas>();
        if (c == null && inventory.HotbarSlots != null)
            foreach (GameObject slot in inventory.HotbarSlots)
                if (slot != null && (c = slot.GetComponentInParent<Canvas>()) != null)
                    break;
        if (c == null && inventory.Slots != null)
            foreach (GameObject slot in inventory.Slots)
                if (slot != null && (c = slot.GetComponentInParent<Canvas>()) != null)
                    break;
        return c != null ? c.rootCanvas : null;
    }

    /// <summary>An overlay canvas (1920×1080 reference) with a raycaster, created once.</summary>
    public static Canvas Generated()
    {
        if (generated != null)
            return generated;
        var go = new GameObject("Gameplay UI (generated)", typeof(RectTransform));
        generated = go.AddComponent<Canvas>();
        generated.renderMode = RenderMode.ScreenSpaceOverlay;
        generated.sortingOrder = 50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        InventoryEventSystems.EnsureSingle();
        return generated;
    }
}
