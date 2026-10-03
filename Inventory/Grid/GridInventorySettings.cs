using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What happens to items that do not fit when the inventory switches mode (slots ↔ grid).</summary>
public enum GridOverflowPolicy
{
    /// <summary>The switch is refused when something would not fit (nothing moves).</summary>
    Refuse,
    /// <summary>What does not fit goes to free hotbar slots, then is dropped next to the player (logged).</summary>
    HotbarThenDrop,
}

/// <summary>How an item's icon fills the cells it takes in the grid.</summary>
public enum GridIconFit
{
    /// <summary>The icon covers all the item's cells (a 1×3 sword's icon is 1×3), stretched if its picture is another shape.</summary>
    FillCells,
    /// <summary>As big as fits in the item's cells, keeping the picture's proportions (never stretched).</summary>
    KeepProportions,
}

/// <summary>Settings of the grid inventory (Inventory Manager ▸ Grid Inventory).</summary>
[Serializable]
public class GridInventorySettings
{
    [Header("Grid")]
    [Tooltip("Cells across.")]
    [Range(2, 30)] public int columns = 10;
    [Tooltip("Cells down.")]
    [Range(2, 30)] public int rows = 6;
    [Tooltip("Size of a cell (pixels). 0 = fit the cells to the inventory panel.")]
    [Min(0f)] public float cellSize = 56f;
    [Tooltip("Gap between cells (pixels).")]
    [Min(0f)] public float spacing = 2f;
    [Tooltip("Margin around the grid (pixels).")]
    [Min(0f)] public float padding = 8f;
    [Tooltip("Resize the inventory panel to fit the grid (with a fixed Cell Size).")]
    public bool resizePanelToFit = true;

    [Header("Icons")]
    [Tooltip("Keep Proportions (recommended): as big as fits without stretching. Fill Cells: stretched over every cell. Either " +
             "way, an item's Grid Icon (drawn in its shape) fills its cells, and Grid Icon Angle stands diagonal icons up.")]
    public GridIconFit iconFitMode = GridIconFit.KeepProportions;
    [Tooltip("Gap between the icon and the edge of its cells (pixels).")]
    [Min(0f)] public float iconPadding = 4f;

    [Header("Rotation")]
    [Tooltip("Actions (by name, in the player's input actions) that rotate the dragged or hovered item.")]
    public string[] rotateActionNames = { "RotateItem", "Rotate" };
    [Tooltip("Key that rotates when the input actions have no rotate action.")]
    public Key rotateFallbackKey = Key.R;
    [Tooltip("The right mouse button rotates the item being dragged.")]
    public bool rightClickRotatesWhileDragging = true;
    [Tooltip("Items being added (pickups, rewards) may be turned sideways to fit.")]
    public bool autoRotateToFit = true;
    [Tooltip("A line above the grid with the move / rotate controls of the device in use. Off by default.")]
    public bool showRotateHint = false;

    [Header("Mode switching")]
    [Tooltip("When switching between slots and grid at runtime, what to do with items that would not fit.")]
    public GridOverflowPolicy overflow = GridOverflowPolicy.Refuse;

    [Header("Colors")]
    public Color cellColor = new Color(1f, 1f, 1f, 0.07f);
    [Tooltip("Behind items in the grid (shows the cells they take).")]
    public Color itemBackdropColor = new Color(0.35f, 0.55f, 0.85f, 0.22f);
    public Color hoverColor = new Color(1f, 1f, 1f, 0.18f);
    [Tooltip("Placement preview: the item fits there.")]
    public Color validColor = new Color(0.3f, 1f, 0.45f, 0.35f);
    [Tooltip("Placement preview: it stacks onto / swaps with the item there.")]
    public Color mergeColor = new Color(1f, 0.85f, 0.3f, 0.35f);
    [Tooltip("Placement preview: it does not fit there.")]
    public Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.4f);
    [Tooltip("Where the dragged item came from.")]
    public Color originColor = new Color(1f, 1f, 1f, 0.1f);

    public int CellCount => Mathf.Max(1, columns) * Mathf.Max(1, rows);

    public void Clamp()
    {
        columns = Mathf.Clamp(columns, 2, 30);
        rows = Mathf.Clamp(rows, 2, 30);
        cellSize = Mathf.Max(0f, cellSize);
        spacing = Mathf.Max(0f, spacing);
        padding = Mathf.Max(0f, padding);
    }
}
