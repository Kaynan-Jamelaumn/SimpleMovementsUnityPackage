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
    [System.NonSerialized] private RectOffset hotbarPadding;

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
        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        // Ensure minimum constraints
        minSlotSize = Mathf.Max(10f, minSlotSize);
        maxSlotSize = Mathf.Max(minSlotSize + 1f, maxSlotSize);
        minSpacing = Mathf.Max(0f, minSpacing);
        maxSpacing = Mathf.Max(minSpacing, maxSpacing);
        preferredSpacing = Mathf.Clamp(preferredSpacing, minSpacing, maxSpacing);

        // Validate grid constraints
        maxColumns = Mathf.Max(1, maxColumns);
        maxRows = Mathf.Max(1, maxRows);
        forcedColumns = Mathf.Max(1, forcedColumns);
        forcedRows = Mathf.Max(1, forcedRows);

        // Validate padding
        paddingLeft = Mathf.Max(0, paddingLeft);
        paddingRight = Mathf.Max(0, paddingRight);
        paddingTop = Mathf.Max(0, paddingTop);
        paddingBottom = Mathf.Max(0, paddingBottom);
        paddingPercentage = Mathf.Clamp(paddingPercentage, 0f, 20f);

        // Validate aspect ratio
        if (slotAspectRatio.x <= 0) slotAspectRatio.x = 1f;
        if (slotAspectRatio.y <= 0) slotAspectRatio.y = 1f;

        // Validate multipliers
        oversizeMultiplier = Mathf.Clamp(oversizeMultiplier, 1f, 3f);
        minPanelUtilization = Mathf.Clamp(minPanelUtilization, 0.1f, 1f);
    }

    // Configuration Methods
    public void SetLayoutMode(LayoutMode mode)
    {
        layoutMode = mode;
        ApplyLayoutModeDefaults(mode);
    }

    private void ApplyLayoutModeDefaults(LayoutMode mode)
    {
        switch (mode)
        {
            case LayoutMode.Compact:
                SetSpacingRange(2f, 8f, 4f);
                spaceDistribution = SpaceDistribution.LargerSlots;
                break;
            case LayoutMode.Spacious:
                SetSpacingRange(8f, 20f, 12f);
                spaceDistribution = SpaceDistribution.EvenSpacing;
                break;
            case LayoutMode.Adaptive:
                SetSpacingRange(5f, 15f, 8f);
                spaceDistribution = SpaceDistribution.Balanced;
                break;
            case LayoutMode.FillPanel:
                spaceDistribution = SpaceDistribution.Balanced;
                break;
            case LayoutMode.Custom:
                // Don't change existing values
                break;
        }
    }

    public void SetSpacingRange(float min, float max, float preferred)
    {
        minSpacing = Mathf.Max(0f, min);
        maxSpacing = Mathf.Max(minSpacing, max);
        preferredSpacing = Mathf.Clamp(preferred, minSpacing, maxSpacing);
    }

    public void SetSlotSizeConstraints(float minSize, float maxSize)
    {
        minSlotSize = Mathf.Max(10f, minSize);
        maxSlotSize = Mathf.Max(minSlotSize + 1f, maxSize);
    }

    public void SetCustomSpacing(Vector2 spacing)
    {
        customSpacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));
        useUniformSpacing = false;
    }

    public void SetCustomPadding(RectOffset padding)
    {
        paddingLeft = Mathf.Max(0, padding.left);
        paddingRight = Mathf.Max(0, padding.right);
        paddingTop = Mathf.Max(0, padding.top);
        paddingBottom = Mathf.Max(0, padding.bottom);
        useCustomPadding = true;
    }

    public void SetGridConstraints(GridConstraintMode mode, int constraintValue = 5)
    {
        constraintMode = mode;
        constraintValue = Mathf.Max(1, constraintValue);

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
        hotbarSlotSize = new Vector2(Mathf.Max(20f, size.x), Mathf.Max(20f, size.y));
    }

    public void SetHotbarSpacing(Vector2 spacing)
    {
        hotbarSlotSpacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));
    }

    public void SetHotbarPadding(RectOffset padding)
    {
        hotbarPadding = new RectOffset(
            Mathf.Max(0, padding.left),
            Mathf.Max(0, padding.right),
            Mathf.Max(0, padding.top),
            Mathf.Max(0, padding.bottom)
        );
    }

    // Advanced configuration methods
    public void SetAspectRatio(Vector2 ratio)
    {
        slotAspectRatio = new Vector2(Mathf.Max(0.1f, ratio.x), Mathf.Max(0.1f, ratio.y));
    }

    public void SetPanelUtilizationTarget(float target)
    {
        minPanelUtilization = Mathf.Clamp(target, 0.1f, 1f);
    }

    public void SetOversizeSettings(bool allowOversize, float multiplier)
    {
        allowOversizedSlots = allowOversize;
        oversizeMultiplier = Mathf.Clamp(multiplier, 1f, 3f);
    }

    public void SetDynamicResize(bool enabled)
    {
        dynamicResize = enabled;
    }

    public void EnableSquareSlots(bool enabled)
    {
        maintainSquareSlots = enabled;
        if (enabled)
        {
            slotAspectRatio = Vector2.one;
        }
    }

    public void SetPreserveAspectRatio(bool preserve)
    {
        preserveAspectRatio = preserve;
    }

    public void SetAdaptivePadding(bool adaptive, float percentage = 5f)
    {
        adaptivePadding = adaptive;
        paddingPercentage = Mathf.Clamp(percentage, 0f, 20f);
    }

    // Preset application methods
    public void ApplyCompactPreset()
    {
        SetLayoutMode(LayoutMode.Compact);
        SetSlotSizeConstraints(50f, 80f);
        SetSpacingRange(2f, 8f, 4f);
        SetSpaceDistribution(SpaceDistribution.LargerSlots);
        SetContentAlignment(ContentAlignment.TopLeft);
        maintainSquareSlots = true;
    }

    public void ApplyBalancedPreset()
    {
        SetLayoutMode(LayoutMode.Adaptive);
        SetSlotSizeConstraints(60f, 120f);
        SetSpacingRange(5f, 15f, 8f);
        SetSpaceDistribution(SpaceDistribution.Balanced);
        SetContentAlignment(ContentAlignment.Center);
        maintainSquareSlots = true;
    }

    public void ApplySpeciousPreset()
    {
        SetLayoutMode(LayoutMode.Spacious);
        SetSlotSizeConstraints(80f, 150f);
        SetSpacingRange(10f, 25f, 15f);
        SetSpaceDistribution(SpaceDistribution.EvenSpacing);
        SetContentAlignment(ContentAlignment.Center);
        maintainSquareSlots = true;
    }

    public void ApplyMobilePreset()
    {
        SetLayoutMode(LayoutMode.Adaptive);
        SetSlotSizeConstraints(70f, 140f);
        SetSpacingRange(8f, 20f, 12f);
        SetSpaceDistribution(SpaceDistribution.EvenSpacing);
        SetContentAlignment(ContentAlignment.Center);
        maintainSquareSlots = true;
        SetGridConstraints(GridConstraintMode.FixedColumns, 3);
    }

    public void ApplyFillScreenPreset()
    {
        SetLayoutMode(LayoutMode.FillPanel);
        SetSlotSizeConstraints(60f, 200f);
        SetSpacingRange(5f, 20f, 10f);
        SetSpaceDistribution(SpaceDistribution.Balanced);
        SetContentAlignment(ContentAlignment.Center);
        allowOversizedSlots = true;
        oversizeMultiplier = 1.5f;
    }

    // Utility methods
    public bool IsValidConfiguration()
    {
        return minSlotSize > 0 && maxSlotSize > minSlotSize &&
               minSpacing >= 0 && maxSpacing >= minSpacing &&
               preferredSpacing >= minSpacing && preferredSpacing <= maxSpacing &&
               maxColumns > 0 && maxRows > 0;
    }

    public void ResetToDefaults()
    {
        minSlotSize = 60f;
        maxSlotSize = 120f;
        maintainSquareSlots = true;
        slotAspectRatio = Vector2.one;

        minSpacing = 2f;
        maxSpacing = 15f;
        preferredSpacing = 5f;
        useUniformSpacing = true;
        customSpacing = new Vector2(5f, 5f);

        useCustomPadding = false;
        paddingLeft = paddingRight = paddingTop = paddingBottom = 10;
        paddingPercentage = 5f;
        adaptivePadding = true;

        layoutMode = LayoutMode.Adaptive;
        spaceDistribution = SpaceDistribution.EvenSpacing;
        minPanelUtilization = 0.7f;
        preserveAspectRatio = true;

        constraintMode = GridConstraintMode.Adaptive;
        forcedColumns = 5;
        forcedRows = 4;
        maxColumns = 10;
        maxRows = 8;

        dynamicResize = true;
        contentAlignment = ContentAlignment.Center;
        allowOversizedSlots = false;
        oversizeMultiplier = 1.5f;

        hotbarSlotSize = new Vector2(80f, 80f);
        hotbarSlotSpacing = new Vector2(5f, 5f);
    }

    public string GetConfigurationSummary()
    {
        return $"Layout Mode: {layoutMode} | " +
               $"Slot Size: {minSlotSize:F0}-{maxSlotSize:F0} | " +
               $"Spacing: {minSpacing:F1}-{maxSpacing:F1} (pref: {preferredSpacing:F1}) | " +
               $"Grid: {constraintMode} | " +
               $"Alignment: {contentAlignment} | " +
               $"Square Slots: {maintainSquareSlots}";
    }

    // Debug methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogConfiguration()
    {
        Debug.Log($"SlotConfiguration Summary:\n{GetConfigurationSummary()}");
        Debug.Log($"Panel Utilization Target: {minPanelUtilization:P1}");
        Debug.Log($"Dynamic Resize: {dynamicResize}");
        Debug.Log($"Oversize Allowed: {allowOversizedSlots} (x{oversizeMultiplier:F1})");
    }
}