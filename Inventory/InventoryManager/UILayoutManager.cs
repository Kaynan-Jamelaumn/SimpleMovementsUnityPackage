using UnityEngine;

[System.Serializable]
public class UILayoutManager
{
    [Header("Layout Preset Configuration")]
    [SerializeField, Tooltip("Use predefined layout presets instead of manual configuration")]
    private bool useLayoutPresets = true;

    [SerializeField, Tooltip("Current active layout preset")]
    private LayoutPreset currentPreset = LayoutPreset.Balanced;

    [SerializeField, Tooltip("Automatically adjust layout based on screen size and resolution")]
    private bool autoAdjustForScreenSize = true;

    [SerializeField, Tooltip("Show layout information in console for debugging")]
    private bool debugLayoutInfo = false;

    // Layout presets enumeration
    public enum LayoutPreset
    {
        Compact,        // Minimal spacing, maximum slots visible
        Balanced,       // Good balance of size and spacing  
        Spacious,       // Large slots with generous spacing
        FillScreen,     // Use entire available space
        Mobile,         // Optimized for touch interfaces
        Custom          // Use manual SlotManager configuration
    }

    // Private state (runtime only)
    [System.NonSerialized] private SlotManager slotManager;
    [System.NonSerialized] private LayoutPreset previousPreset;
    [System.NonSerialized] private Vector2 lastScreenSize;

    // Properties
    public LayoutPreset CurrentPreset => currentPreset;
    public bool UseLayoutPresets => useLayoutPresets;
    public bool AutoAdjustForScreenSize => autoAdjustForScreenSize;

    // Events (runtime only)
    [System.NonSerialized] public System.Action<LayoutPreset> OnPresetChanged;

    // Initialization and Updates
    public void Initialize(SlotManager manager)
    {
        slotManager = manager;
        previousPreset = currentPreset;
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        if (useLayoutPresets)
        {
            ApplyLayoutPreset(currentPreset);
        }
    }

    public void Update()
    {
        CheckForPresetChange();
        CheckForScreenSizeChange();
    }

    private void CheckForPresetChange()
    {
        if (currentPreset != previousPreset)
        {
            ApplyLayoutPreset(currentPreset);
            previousPreset = currentPreset;
            OnPresetChanged?.Invoke(currentPreset);
        }
    }

    private void CheckForScreenSizeChange()
    {
        if (!autoAdjustForScreenSize) return;

        Vector2 currentScreenSize = new Vector2(Screen.width, Screen.height);
        if (currentScreenSize != lastScreenSize)
        {
            AdjustForScreenSize();
            lastScreenSize = currentScreenSize;
        }
    }

    // Layout Preset Management
    public void SetLayoutPreset(LayoutPreset preset)
    {
        currentPreset = preset;
        useLayoutPresets = true;
        ApplyLayoutPreset(preset);
        OnPresetChanged?.Invoke(preset);
    }

    private void ApplyLayoutPreset(LayoutPreset preset)
    {
        if (!useLayoutPresets || slotManager == null) return;

        switch (preset)
        {
            case LayoutPreset.Compact:
                ApplyCompactLayout();
                break;
            case LayoutPreset.Balanced:
                ApplyBalancedLayout();
                break;
            case LayoutPreset.Spacious:
                ApplySpeciousLayout();
                break;
            case LayoutPreset.FillScreen:
                ApplyFillScreenLayout();
                break;
            case LayoutPreset.Mobile:
                ApplyMobileLayout();
                break;
            case LayoutPreset.Custom:
                // Don't override custom settings
                break;
        }

        if (debugLayoutInfo)
        {
            LogLayoutInfo();
        }
    }

    // Preset Implementations
    private void ApplyCompactLayout()
    {
        slotManager.SetLayoutMode(SlotManager.LayoutMode.Compact);
        slotManager.SetSlotSizeConstraints(50f, 80f);
        slotManager.SetSpacingRange(2f, 8f, 4f);
        slotManager.SetSpaceDistribution(SlotManager.SpaceDistribution.LargerSlots);
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.Adaptive);
        slotManager.SetContentAlignment(SlotManager.ContentAlignment.TopLeft);
    }

    private void ApplyBalancedLayout()
    {
        slotManager.SetLayoutMode(SlotManager.LayoutMode.Adaptive);
        slotManager.SetSlotSizeConstraints(60f, 120f);
        slotManager.SetSpacingRange(5f, 15f, 8f);
        slotManager.SetSpaceDistribution(SlotManager.SpaceDistribution.Balanced);
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.Adaptive);
        slotManager.SetContentAlignment(SlotManager.ContentAlignment.Center);
    }

    private void ApplySpeciousLayout()
    {
        slotManager.SetLayoutMode(SlotManager.LayoutMode.Spacious);
        slotManager.SetSlotSizeConstraints(80f, 150f);
        slotManager.SetSpacingRange(10f, 25f, 15f);
        slotManager.SetSpaceDistribution(SlotManager.SpaceDistribution.EvenSpacing);
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.Adaptive);
        slotManager.SetContentAlignment(SlotManager.ContentAlignment.Center);
    }

    private void ApplyFillScreenLayout()
    {
        slotManager.SetLayoutMode(SlotManager.LayoutMode.FillPanel);
        slotManager.SetSlotSizeConstraints(60f, 200f);
        slotManager.SetSpacingRange(5f, 20f, 10f);
        slotManager.SetSpaceDistribution(SlotManager.SpaceDistribution.Balanced);
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.Adaptive);
        slotManager.SetContentAlignment(SlotManager.ContentAlignment.Center);
    }

    private void ApplyMobileLayout()
    {
        slotManager.SetLayoutMode(SlotManager.LayoutMode.Adaptive);
        slotManager.SetSlotSizeConstraints(70f, 140f);
        slotManager.SetSpacingRange(8f, 20f, 12f);
        slotManager.SetSpaceDistribution(SlotManager.SpaceDistribution.EvenSpacing);
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.Adaptive);
        slotManager.SetContentAlignment(SlotManager.ContentAlignment.Center);
    }

    // Screen Size Adaptation
    private void AdjustForScreenSize()
    {
        if (slotManager == null || currentPreset == LayoutPreset.Custom) return;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float aspectRatio = screenWidth / screenHeight;

        // Apply screen-specific adjustments
        if (aspectRatio > 2.0f) // Ultra-wide screens
        {
            AdjustForUltraWide();
        }
        else if (aspectRatio < 1.0f) // Portrait orientation (mobile)
        {
            AdjustForPortrait();
        }
        else if (screenWidth < 1024) // Small screens
        {
            AdjustForSmallScreen();
        }
        else if (screenWidth > 2560) // Large/4K screens
        {
            AdjustForLargeScreen();
        }
    }

    private void AdjustForUltraWide()
    {
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.FixedColumns, 8);
    }

    private void AdjustForPortrait()
    {
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.FixedColumns, 3);
        slotManager.SetSlotSizeConstraints(60f, 120f);
    }

    private void AdjustForSmallScreen()
    {
        slotManager.SetSlotSizeConstraints(45f, 90f);
        slotManager.SetSpacingRange(2f, 8f, 5f);
        slotManager.SetGridConstraints(SlotManager.GridConstraintMode.FixedColumns, 4);
    }

    private void AdjustForLargeScreen()
    {
        slotManager.SetSlotSizeConstraints(80f, 160f);
        slotManager.SetSpacingRange(8f, 20f, 12f);
    }

    // Public Configuration Methods (Delegates to SlotManager)
    public void ConfigureCustomLayout(
        SlotManager.LayoutMode layoutMode = SlotManager.LayoutMode.Adaptive,
        float minSlotSize = 60f,
        float maxSlotSize = 120f,
        float minSpacing = 5f,
        float maxSpacing = 15f,
        float preferredSpacing = 8f,
        SlotManager.SpaceDistribution spaceDistribution = SlotManager.SpaceDistribution.Balanced,
        SlotManager.GridConstraintMode constraintMode = SlotManager.GridConstraintMode.Adaptive,
        int constraintValue = 5,
        SlotManager.ContentAlignment alignment = SlotManager.ContentAlignment.Center)
    {
        if (slotManager == null) return;

        currentPreset = LayoutPreset.Custom;
        useLayoutPresets = false;

        slotManager.SetLayoutMode(layoutMode);
        slotManager.SetSlotSizeConstraints(minSlotSize, maxSlotSize);
        slotManager.SetSpacingRange(minSpacing, maxSpacing, preferredSpacing);
        slotManager.SetSpaceDistribution(spaceDistribution);
        slotManager.SetGridConstraints(constraintMode, constraintValue);
        slotManager.SetContentAlignment(alignment);
    }

    public void SetCustomSpacing(Vector2 spacing)
    {
        currentPreset = LayoutPreset.Custom;
        slotManager?.SetCustomSpacing(spacing);
    }

    public void SetCustomPadding(int left, int right, int top, int bottom)
    {
        currentPreset = LayoutPreset.Custom;
        var padding = new RectOffset(left, right, top, bottom);
        slotManager?.SetCustomPadding(padding);
    }

    public void SetGridColumns(int columns)
    {
        slotManager?.SetGridConstraints(SlotManager.GridConstraintMode.FixedColumns, columns);
    }

    public void SetGridRows(int rows)
    {
        slotManager?.SetGridConstraints(SlotManager.GridConstraintMode.FixedRows, rows);
    }

    public void ForceAdaptiveGrid()
    {
        slotManager?.SetGridConstraints(SlotManager.GridConstraintMode.Adaptive);
    }

    // Layout Information Methods (Delegates to SlotManager)
    public bool IsLayoutOptimal()
    {
        return slotManager?.IsLayoutOptimal() ?? false;
    }

    public float GetPanelUtilization()
    {
        return slotManager?.GetPanelUtilization() ?? 0f;
    }

    public Vector2 GetRecommendedPanelSize(int slotCount)
    {
        return slotManager?.GetRecommendedPanelSize(slotCount) ?? Vector2.zero;
    }

    public string GetLayoutSummary()
    {
        var layout = slotManager?.CurrentLayout;
        if (layout == null) return "No layout data available";

        return $"Preset: {currentPreset} | " +
               $"Layout: {layout.columns}x{layout.rows} | " +
               $"Cell Size: {layout.cellSize.x:F0}x{layout.cellSize.y:F0} | " +
               $"Spacing: {layout.spacing.x:F1} | " +
               $"Utilization: {layout.panelUtilization:P1} | " +
               $"Optimal: {(layout.isOptimal ? "Yes" : "No")}";
    }

    // Configuration Properties
    public void SetLayoutPresetsEnabled(bool enabled)
    {
        useLayoutPresets = enabled;
        if (enabled)
        {
            ApplyLayoutPreset(currentPreset);
        }
    }

    public void SetAutoAdjustForScreenSize(bool enabled)
    {
        autoAdjustForScreenSize = enabled;
        if (enabled)
        {
            AdjustForScreenSize();
        }
    }

    public void SetDebugLayoutInfo(bool enabled)
    {
        debugLayoutInfo = enabled;
    }

    // Additional utility methods
    public void RefreshCurrentLayout()
    {
        if (useLayoutPresets)
        {
            ApplyLayoutPreset(currentPreset);
        }
    }

    public void ResetToDefaults()
    {
        currentPreset = LayoutPreset.Balanced;
        useLayoutPresets = true;
        autoAdjustForScreenSize = true;
        debugLayoutInfo = false;

        if (slotManager != null)
        {
            ApplyLayoutPreset(currentPreset);
        }
    }

    public bool IsCustomLayout()
    {
        return currentPreset == LayoutPreset.Custom;
    }

    public void OptimizeForCurrentScreen()
    {
        AdjustForScreenSize();
    }

    // Debug Methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogLayoutInfo()
    {
        if (!debugLayoutInfo) return;

        Debug.Log("=== UI Layout Manager Info ===");
        Debug.Log(GetLayoutSummary());
        Debug.Log($"Auto-adjust: {autoAdjustForScreenSize}");
        Debug.Log($"Screen: {Screen.width}x{Screen.height} ({Screen.width / (float)Screen.height:F2} aspect ratio)");

        var recommendedSize = GetRecommendedPanelSize(20); // Default slot count for testing
        Debug.Log($"Recommended Panel Size: {recommendedSize.x:F0} x {recommendedSize.y:F0}");

        if (slotManager != null)
        {
            Debug.Log($"Layout Optimal: {IsLayoutOptimal()}");
            Debug.Log($"Panel Utilization: {GetPanelUtilization():P1}");
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogDetailedLayoutAnalysis()
    {
        if (slotManager == null)
        {
            Debug.LogWarning("Cannot perform detailed layout analysis: SlotManager is null");
            return;
        }

        Debug.Log("=== Detailed Layout Analysis ===");

        var layout = slotManager.CurrentLayout;
        if (layout != null)
        {
            Debug.Log($"Grid Configuration: {layout.columns}x{layout.rows}");
            Debug.Log($"Cell Size: {layout.cellSize.x:F1} x {layout.cellSize.y:F1}");
            Debug.Log($"Spacing: {layout.spacing.x:F1} x {layout.spacing.y:F1}");
            Debug.Log($"Padding: L:{layout.padding.left} R:{layout.padding.right} T:{layout.padding.top} B:{layout.padding.bottom}");
            Debug.Log($"Total Content Size: {layout.totalContentSize.x:F1} x {layout.totalContentSize.y:F1}");
            Debug.Log($"Panel Utilization: {layout.panelUtilization:P2}");
            Debug.Log($"Layout Optimal: {layout.isOptimal}");
        }

        Debug.Log($"Current Preset: {currentPreset}");
        Debug.Log($"Use Layout Presets: {useLayoutPresets}");
        Debug.Log($"Auto Adjust for Screen: {autoAdjustForScreenSize}");

        float aspectRatio = Screen.width / (float)Screen.height;
        string screenCategory = GetScreenCategory(aspectRatio, Screen.width);
        Debug.Log($"Screen Category: {screenCategory} ({Screen.width}x{Screen.height}, {aspectRatio:F2} ratio)");
    }

    private string GetScreenCategory(float aspectRatio, float screenWidth)
    {
        if (aspectRatio > 2.0f) return "Ultra-wide";
        if (aspectRatio < 1.0f) return "Portrait/Mobile";
        if (screenWidth < 1024) return "Small Screen";
        if (screenWidth > 2560) return "Large/4K Screen";
        return "Standard";
    }
}