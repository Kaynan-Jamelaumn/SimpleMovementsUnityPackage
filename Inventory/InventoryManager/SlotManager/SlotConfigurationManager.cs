using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotConfigurationManager
{
    [Header("Slot Size Configuration")]
    [SerializeField, Tooltip("Minimum size for inventory slots in pixels")]
    private float minSlotSize = 60f;

    [SerializeField, Tooltip("Maximum size for inventory slots in pixels")]
    private float maxSlotSize = 120f;

    [SerializeField, Tooltip("Keep slots as perfect squares")]
    private bool maintainSquareSlots = true;

    [SerializeField, Tooltip("Aspect ratio for non-square slots (width:height)")]
    private Vector2 slotAspectRatio = Vector2.one;

    [Header("Spacing Configuration")]
    [SerializeField, Tooltip("Minimum spacing between slots in pixels")]
    private float minSpacing = 2f;

    [SerializeField, Tooltip("Maximum spacing between slots in pixels")]
    private float maxSpacing = 15f;

    [SerializeField, Tooltip("Preferred spacing when space allows")]
    private float preferredSpacing = 5f;

    [SerializeField, Tooltip("Use same spacing for X and Y axes")]
    private bool useUniformSpacing = true;

    [SerializeField, Tooltip("Custom spacing values when uniform spacing is disabled")]
    private Vector2 customSpacing = new Vector2(5f, 5f);

    [Header("Padding Configuration")]
    [SerializeField, Tooltip("Use custom padding values instead of calculated")]
    private bool useCustomPadding = false;

    [SerializeField, Tooltip("Left padding in pixels")]
    private int paddingLeft = 10;

    [SerializeField, Tooltip("Right padding in pixels")]
    private int paddingRight = 10;

    [SerializeField, Tooltip("Top padding in pixels")]
    private int paddingTop = 10;

    [SerializeField, Tooltip("Bottom padding in pixels")]
    private int paddingBottom = 10;

    [SerializeField, Tooltip("Padding as percentage of panel size when custom padding is disabled"), Range(0f, 20f)]
    private float paddingPercentage = 5f;

    [SerializeField, Tooltip("Automatically adjust padding based on panel size")]
    private bool adaptivePadding = true;

    [Header("Layout Behavior")]
    [SerializeField, Tooltip("How slots should be arranged and sized")]
    private LayoutMode layoutMode = LayoutMode.Adaptive;

    [SerializeField, Tooltip("How extra space should be distributed")]
    private SpaceDistribution spaceDistribution = SpaceDistribution.EvenSpacing;

    [SerializeField, Tooltip("Minimum percentage of panel that should be utilized"), Range(0.5f, 1f)]
    private float minPanelUtilization = 0.7f;

    [SerializeField, Tooltip("Maintain aspect ratio when resizing slots")]
    private bool preserveAspectRatio = true;

    [Header("Grid Constraints")]
    [SerializeField, Tooltip("How grid dimensions should be determined")]
    private GridConstraintMode constraintMode = GridConstraintMode.Adaptive;

    [SerializeField, Tooltip("Fixed number of columns when using FixedColumns mode")]
    private int forcedColumns = 5;

    [SerializeField, Tooltip("Fixed number of rows when using FixedRows mode")]
    private int forcedRows = 4;

    [SerializeField, Tooltip("Maximum columns allowed in adaptive mode")]
    private int maxColumns = 10;

    [SerializeField, Tooltip("Maximum rows allowed in adaptive mode")]
    private int maxRows = 8;

    [Header("Advanced Options")]
    [SerializeField, Tooltip("Automatically resize when slot count changes")]
    private bool dynamicResize = true;

    [SerializeField, Tooltip("How content should be aligned within the panel")]
    private ContentAlignment contentAlignment = ContentAlignment.Center;

    [SerializeField, Tooltip("Allow slots to exceed maxSlotSize if space is available")]
    private bool allowOversizedSlots = false;

    [SerializeField, Tooltip("Multiplier for oversize when allowOversizedSlots is true"), Range(1f, 3f)]
    private float oversizeMultiplier = 1.5f;

    [Header("Hotbar Configuration")]
    [SerializeField, Tooltip("Hotbar slot size")]
    private Vector2 hotbarSlotSize = new Vector2(80f, 80f);

    [SerializeField, Tooltip("Hotbar slot spacing")]
    private Vector2 hotbarSlotSpacing = new Vector2(5f, 5f);

    // Runtime hotbar padding - initialized in Initialize()
    private RectOffset hotbarPadding;

    // Enums
    public enum LayoutMode
    {
        Compact,        // Minimize spacing, maximize slot count
        Spacious,       // Maximize spacing within limits
        Adaptive,       // Balance between size and spacing
        FillPanel,      // Use entire panel space
        Custom          // Use exact custom values
    }

    public enum SpaceDistribution
    {
        EvenSpacing,    // Distribute extra space as spacing
        LargerSlots,    // Make slots larger with extra space
        ExtraPadding,   // Add extra space as padding
        Balanced        // Balance between spacing and slot size
    }

    public enum GridConstraintMode
    {
        Adaptive,       // Calculate optimal grid automatically
        FixedColumns,   // Force specific column count
        FixedRows,      // Force specific row count
        AspectRatio,    // Maintain specific grid aspect ratio
        Custom          // Manual constraint setting
    }

    public enum ContentAlignment
    {
        TopLeft, TopCenter, TopRight,
        MiddleLeft, Center, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }

    // Properties
    public float MinSlotSize => minSlotSize;
    public float MaxSlotSize => maxSlotSize;
    public bool MaintainSquareSlots => maintainSquareSlots;
    public Vector2 SlotAspectRatio => slotAspectRatio;
    public float MinSpacing => minSpacing;
    public float MaxSpacing => maxSpacing;
    public float PreferredSpacing => preferredSpacing;
    public bool UseUniformSpacing => useUniformSpacing;
    public Vector2 CustomSpacing => customSpacing;
    public bool UseCustomPadding => useCustomPadding;
    public int PaddingLeft => paddingLeft;
    public int PaddingRight => paddingRight;
    public int PaddingTop => paddingTop;
    public int PaddingBottom => paddingBottom;
    public float PaddingPercentage => paddingPercentage;
    public bool AdaptivePadding => adaptivePadding;
    public LayoutMode CurrentLayoutMode => layoutMode;
    public SpaceDistribution CurrentSpaceDistribution => spaceDistribution;
    public float MinPanelUtilization => minPanelUtilization;
    public bool PreserveAspectRatio => preserveAspectRatio;
    public GridConstraintMode ConstraintMode => constraintMode;
    public int ForcedColumns => forcedColumns;
    public int ForcedRows => forcedRows;
    public int MaxColumns => maxColumns;
    public int MaxRows => maxRows;
    public bool DynamicResize => dynamicResize;
    public ContentAlignment CurrentContentAlignment => contentAlignment;
    public bool AllowOversizedSlots => allowOversizedSlots;
    public float OversizeMultiplier => oversizeMultiplier;
    public Vector2 HotbarSlotSize => hotbarSlotSize;
    public Vector2 HotbarSlotSpacing => hotbarSlotSpacing;
    public RectOffset HotbarPadding => hotbarPadding;

    // Initialization
    public void Initialize()
    {
        // Initialize RectOffset during runtime to avoid serialization issues
        hotbarPadding = new RectOffset(10, 10, 10, 10);
    }

    // Configuration Methods
    public void SetLayoutMode(LayoutMode mode)
    {
        layoutMode = mode;
    }

    public void SetSpacingRange(float min, float max, float preferred)
    {
        minSpacing = min;
        maxSpacing = max;
        preferredSpacing = preferred;
    }

    public void SetSlotSizeConstraints(float minSize, float maxSize)
    {
        minSlotSize = minSize;
        maxSlotSize = maxSize;
    }

    public void SetCustomSpacing(Vector2 spacing)
    {
        customSpacing = spacing;
        useUniformSpacing = false;
    }

    public void SetCustomPadding(RectOffset padding)
    {
        paddingLeft = padding.left;
        paddingRight = padding.right;
        paddingTop = padding.top;
        paddingBottom = padding.bottom;
        useCustomPadding = true;
    }

    public void SetGridConstraints(GridConstraintMode mode, int constraintValue = 5)
    {
        constraintMode = mode;
        switch (mode)
        {
            case GridConstraintMode.FixedColumns:
                forcedColumns = constraintValue;
                break;
            case GridConstraintMode.FixedRows:
                forcedRows = constraintValue;
                break;
        }
    }

    public void SetContentAlignment(ContentAlignment alignment)
    {
        contentAlignment = alignment;
    }

    public void SetSpaceDistribution(SpaceDistribution distribution)
    {
        spaceDistribution = distribution;
    }

    public void SetHotbarSlotSize(Vector2 size)
    {
        hotbarSlotSize = size;
    }

    public void SetHotbarSpacing(Vector2 spacing)
    {
        hotbarSlotSpacing = spacing;
    }

    public void SetHotbarPadding(RectOffset padding)
    {
        hotbarPadding = padding;
    }
}