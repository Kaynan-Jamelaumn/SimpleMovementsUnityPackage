using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotManager
{
    [Header("Slot Prefab Configuration")]
    [SerializeField, Tooltip("Prefab used to create inventory slots")]
    private GameObject slotPrefab;

    [Header("Grid Layout Components")]
    [SerializeField, Tooltip("Grid layout component for inventory slots")]
    private GridLayoutGroup inventoryGridLayout;

    [SerializeField, Tooltip("Grid layout component for hotbar slots")]
    private GridLayoutGroup hotbarGridLayout;

    [Header("Component Managers")]
    [SerializeField] private SlotConfigurationManager configManager;
    [SerializeField] private SlotCreationManager creationManager;
    [SerializeField] private SlotLayoutCalculator layoutCalculator;
    [SerializeField] private SlotUtilityManager utilityManager;
    [SerializeField] private SlotValidationManager validationManager;
    [SerializeField] private SlotPerformanceManager performanceManager;

    // Cached layout data
    private SlotLayoutCalculator.LayoutData cachedLayoutData;

    // Events
    public System.Action OnSlotsChanged;
    public System.Action<SlotLayoutCalculator.LayoutData> OnLayoutChanged;

    // Properties
    public GameObject[] HotbarSlots => creationManager?.HotbarSlots;
    public GameObject[] InventorySlots => creationManager?.InventorySlots;
    public SlotLayoutCalculator.LayoutData CurrentLayout => layoutCalculator?.CurrentLayout;

    // Legacy properties for compatibility
    public LayoutData LegacyCurrentLayout => ConvertToLegacyLayoutData(CurrentLayout);

    // Legacy LayoutData class for backward compatibility
    [System.Serializable]
    public class LayoutData
    {
        public Vector2 cellSize;
        public Vector2 spacing;
        public RectOffset padding;
        public int columns;
        public int rows;
        public float panelUtilization;
        public Vector2 totalContentSize;
        public bool isOptimal;
    }

    // Enum aliases for backward compatibility
    public enum LayoutMode
    {
        Compact = SlotConfigurationManager.LayoutMode.Compact,
        Spacious = SlotConfigurationManager.LayoutMode.Spacious,
        Adaptive = SlotConfigurationManager.LayoutMode.Adaptive,
        FillPanel = SlotConfigurationManager.LayoutMode.FillPanel,
        Custom = SlotConfigurationManager.LayoutMode.Custom
    }

    public enum SpaceDistribution
    {
        EvenSpacing = SlotConfigurationManager.SpaceDistribution.EvenSpacing,
        LargerSlots = SlotConfigurationManager.SpaceDistribution.LargerSlots,
        ExtraPadding = SlotConfigurationManager.SpaceDistribution.ExtraPadding,
        Balanced = SlotConfigurationManager.SpaceDistribution.Balanced
    }

    public enum GridConstraintMode
    {
        Adaptive = SlotConfigurationManager.GridConstraintMode.Adaptive,
        FixedColumns = SlotConfigurationManager.GridConstraintMode.FixedColumns,
        FixedRows = SlotConfigurationManager.GridConstraintMode.FixedRows,
        AspectRatio = SlotConfigurationManager.GridConstraintMode.AspectRatio,
        Custom = SlotConfigurationManager.GridConstraintMode.Custom
    }

    public enum ContentAlignment
    {
        TopLeft = SlotConfigurationManager.ContentAlignment.TopLeft,
        TopCenter = SlotConfigurationManager.ContentAlignment.TopCenter,
        TopRight = SlotConfigurationManager.ContentAlignment.TopRight,
        MiddleLeft = SlotConfigurationManager.ContentAlignment.MiddleLeft,
        Center = SlotConfigurationManager.ContentAlignment.Center,
        MiddleRight = SlotConfigurationManager.ContentAlignment.MiddleRight,
        BottomLeft = SlotConfigurationManager.ContentAlignment.BottomLeft,
        BottomCenter = SlotConfigurationManager.ContentAlignment.BottomCenter,
        BottomRight = SlotConfigurationManager.ContentAlignment.BottomRight
    }

    public void Initialize(PlayerStatusController playerController, GameObject playerObject)
    {
        ValidateComponents();
        InitializeManagers(playerController, playerObject);
        SetupEventHandlers();
    }

    private void ValidateComponents()
    {
        if (slotPrefab == null)
            Debug.LogError("Slot Prefab is not assigned in SlotManager");
        if (inventoryGridLayout == null)
            Debug.LogError("Inventory Grid Layout is not assigned in SlotManager");
        if (hotbarGridLayout == null)
            Debug.LogError("Hotbar Grid Layout is not assigned in SlotManager");

        // Initialize managers if they're null
        if (configManager == null) configManager = new SlotConfigurationManager();
        if (creationManager == null) creationManager = new SlotCreationManager();
        if (layoutCalculator == null) layoutCalculator = new SlotLayoutCalculator();
        if (utilityManager == null) utilityManager = new SlotUtilityManager();
        if (validationManager == null) validationManager = new SlotValidationManager();
        if (performanceManager == null) performanceManager = new SlotPerformanceManager();
    }

    private void InitializeManagers(PlayerStatusController playerController, GameObject playerObject)
    {
        // Initialize validation and performance managers first
        validationManager.Initialize();
        performanceManager.Initialize();

        // Initialize configuration manager first
        using (performanceManager.MeasureOperation("Config Initialization"))
        {
            configManager.Initialize();
        }

        // Validate configuration
        validationManager.ValidateConfiguration(configManager);

        // Initialize layout calculator with config
        using (performanceManager.MeasureOperation("Layout Calculator Initialization"))
        {
            layoutCalculator.Initialize(configManager);
        }

        // Initialize creation manager
        using (performanceManager.MeasureOperation("Creation Manager Initialization"))
        {
            creationManager.Initialize(playerController, playerObject, slotPrefab);
        }

        // Initialize utility manager
        using (performanceManager.MeasureOperation("Utility Manager Initialization"))
        {
            utilityManager.Initialize(
                creationManager.InventorySlotsParent,
                creationManager.HotbarSlotsParent,
                creationManager.EquipmentSlotsParent
            );
        }

        // Validate all components
        using (performanceManager.MeasureOperation("Component Validation"))
        {
            validationManager.ValidateSlotManagerComponents(
                slotPrefab,
                creationManager.HotbarSlotsParent,
                creationManager.InventorySlotsParent,
                inventoryGridLayout,
                hotbarGridLayout
            );
        }
    }

    private void SetupEventHandlers()
    {
        creationManager.OnSlotsChanged += () => OnSlotsChanged?.Invoke();
        layoutCalculator.OnLayoutChanged += (layoutData) =>
        {
            cachedLayoutData = layoutData;
            OnLayoutChanged?.Invoke(layoutData);
        };
    }

    // Slot Creation and Management
    public void CreateAllSlots(int hotbarSlotCount, int inventorySlotCount)
    {
        using (performanceManager.MeasureOperation("Create All Slots", $"Hotbar: {hotbarSlotCount}, Inventory: {inventorySlotCount}"))
        {
            creationManager.CreateAllSlots(hotbarSlotCount, inventorySlotCount);
            ConfigureHotbarLayout(hotbarSlotCount);
            UpdateGridLayout(inventorySlotCount);

            // Validate created slots
            ValidateCreatedSlots();
        }
    }

    public void UpdateHotbarSlots(int newSlotCount)
    {
        using (performanceManager.MeasureOperation("Update Hotbar Slots", $"Count: {newSlotCount}"))
        {
            creationManager.UpdateHotbarSlots(newSlotCount);
            ConfigureHotbarLayout(newSlotCount);

            // Validate hotbar slots
            ValidateSlotArray(HotbarSlots, "Hotbar", true);
        }
    }

    public void UpdateInventorySlots(int newSlotCount)
    {
        using (performanceManager.MeasureOperation("Update Inventory Slots", $"Count: {newSlotCount}"))
        {
            creationManager.UpdateInventorySlots(newSlotCount);
            UpdateGridLayout(newSlotCount);

            // Validate inventory slots
            ValidateSlotArray(InventorySlots, "Inventory", false);
        }
    }

    private void ConfigureHotbarLayout(int slotCount)
    {
        if (hotbarGridLayout == null)
        {
            Debug.LogError("Hotbar Grid Layout is not assigned in SlotManager");
            return;
        }

        // Configure hotbar grid layout
        hotbarGridLayout.cellSize = configManager.HotbarSlotSize;
        hotbarGridLayout.spacing = configManager.HotbarSlotSpacing;
        hotbarGridLayout.padding = configManager.HotbarPadding;
        hotbarGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        hotbarGridLayout.constraintCount = slotCount; // All slots in one row
        hotbarGridLayout.childAlignment = TextAnchor.MiddleCenter;
        hotbarGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        hotbarGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
    }

    // Layout Calculation
    private void UpdateGridLayout(int slotCount)
    {
        if (inventoryGridLayout == null)
        {
            Debug.LogError("Inventory Grid Layout is not assigned in SlotManager");
            return;
        }

        using (performanceManager.MeasureOperation("Layout Calculation", $"Slots: {slotCount}"))
        {
            layoutCalculator.CalculateOptimalLayout(slotCount, creationManager.InventorySlotsParent);
        }

        using (performanceManager.MeasureOperation("Apply Layout to Grid"))
        {
            ApplyLayoutToGrid();
        }

        // Validate the resulting layout
        validationManager.ValidateLayoutData(layoutCalculator.CurrentLayout, slotCount);
    }

    private void ApplyLayoutToGrid()
    {
        var layout = layoutCalculator.CurrentLayout;
        if (layout == null) return;

        inventoryGridLayout.cellSize = layout.cellSize;
        inventoryGridLayout.spacing = layout.spacing;
        inventoryGridLayout.padding = layout.padding;
        inventoryGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        inventoryGridLayout.constraintCount = layout.columns;
        inventoryGridLayout.childAlignment = layoutCalculator.GetTextAnchorFromAlignment(configManager.CurrentContentAlignment);
        inventoryGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        inventoryGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
    }

    // Public Configuration Methods
    public void SetLayoutMode(LayoutMode mode)
    {
        configManager.SetLayoutMode((SlotConfigurationManager.LayoutMode)mode);
        RefreshLayout();
    }

    public void SetSpacingRange(float min, float max, float preferred)
    {
        configManager.SetSpacingRange(min, max, preferred);
        RefreshLayout();
    }

    public void SetSlotSizeConstraints(float minSize, float maxSize)
    {
        configManager.SetSlotSizeConstraints(minSize, maxSize);
        RefreshLayout();
    }

    public void SetCustomSpacing(Vector2 spacing)
    {
        configManager.SetCustomSpacing(spacing);
        RefreshLayout();
    }

    public void SetCustomPadding(RectOffset padding)
    {
        configManager.SetCustomPadding(padding);
        RefreshLayout();
    }

    public void SetGridConstraints(GridConstraintMode mode, int constraintValue = 5)
    {
        configManager.SetGridConstraints((SlotConfigurationManager.GridConstraintMode)mode, constraintValue);
        RefreshLayout();
    }

    public void SetContentAlignment(ContentAlignment alignment)
    {
        configManager.SetContentAlignment((SlotConfigurationManager.ContentAlignment)alignment);
        if (inventoryGridLayout != null)
            inventoryGridLayout.childAlignment = layoutCalculator.GetTextAnchorFromAlignment(configManager.CurrentContentAlignment);
    }

    public void SetSpaceDistribution(SpaceDistribution distribution)
    {
        configManager.SetSpaceDistribution((SlotConfigurationManager.SpaceDistribution)distribution);
        RefreshLayout();
    }

    // Hotbar configuration methods
    public void SetHotbarSlotSize(Vector2 size)
    {
        configManager.SetHotbarSlotSize(size);
        if (hotbarGridLayout != null)
            hotbarGridLayout.cellSize = size;
    }

    public void SetHotbarSpacing(Vector2 spacing)
    {
        configManager.SetHotbarSpacing(spacing);
        if (hotbarGridLayout != null)
            hotbarGridLayout.spacing = spacing;
    }

    public void SetHotbarPadding(RectOffset padding)
    {
        configManager.SetHotbarPadding(padding);
        if (hotbarGridLayout != null)
            hotbarGridLayout.padding = padding;
    }

    private void RefreshLayout()
    {
        if (InventorySlots != null)
        {
            using (performanceManager.MeasureOperation("Layout Refresh", $"Slots: {InventorySlots.Length}"))
            {
                UpdateGridLayout(InventorySlots.Length);
                RefreshAllItemPositions();
            }
        }
    }

    // Validation Helper Methods
    private void ValidateCreatedSlots()
    {
        ValidateSlotArray(HotbarSlots, "Hotbar", true);
        ValidateSlotArray(InventorySlots, "Inventory", false);
    }

    private void ValidateSlotArray(GameObject[] slots, string slotType, bool isHotbarSlots)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                validationManager.ValidateSlot(slots[i], $"{slotType}Slot_{i}", isHotbarSlots);
            }
        }
    }

    // Utility Methods
    public bool IsLayoutOptimal() => layoutCalculator?.IsLayoutOptimal() ?? false;
    public float GetPanelUtilization() => layoutCalculator?.GetPanelUtilization() ?? 0f;

    public Vector2 GetRecommendedPanelSize(int slotCount)
    {
        return layoutCalculator?.GetRecommendedPanelSize(slotCount) ?? Vector2.zero;
    }

    public void RefreshAllItemPositions()
    {
        using (performanceManager.MeasureOperation("Item Position Refresh"))
        {
            creationManager?.RefreshAllItemPositions();
        }
    }

    public Transform GetSharedContainer(bool isHotbar, SlotType slotType)
    {
        return utilityManager?.GetSharedContainer(isHotbar, slotType);
    }

    public void LogSharedContainerInfo()
    {
        utilityManager?.LogSharedContainerInfo();
    }

    // Performance and Validation Access
    public SlotPerformanceManager PerformanceManager => performanceManager;
    public SlotValidationManager ValidationManager => validationManager;

    public string GetPerformanceReport()
    {
        return performanceManager?.GetPerformanceReport() ?? "Performance monitoring not available";
    }

    public string GetValidationReport()
    {
        return validationManager?.GetValidationReport() ?? "Validation not available";
    }

    public void LogPerformanceReport()
    {
        performanceManager?.LogPerformanceReport();
    }

    public void LogValidationReport()
    {
        Debug.Log(GetValidationReport());
    }

    // Legacy compatibility method
    private LayoutData ConvertToLegacyLayoutData(SlotLayoutCalculator.LayoutData newLayoutData)
    {
        if (newLayoutData == null) return null;

        return new LayoutData
        {
            cellSize = newLayoutData.cellSize,
            spacing = newLayoutData.spacing,
            padding = newLayoutData.padding,
            columns = newLayoutData.columns,
            rows = newLayoutData.rows,
            panelUtilization = newLayoutData.panelUtilization,
            totalContentSize = newLayoutData.totalContentSize,
            isOptimal = newLayoutData.isOptimal
        };
    }
}