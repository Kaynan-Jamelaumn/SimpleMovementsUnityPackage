using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotLayoutCalculator
{
    [System.Serializable]
    public class LayoutData
    {
        [SerializeField] public Vector2 cellSize;
        [SerializeField] public Vector2 spacing;
        [SerializeField] public RectOffset padding;
        [SerializeField] public int columns;
        [SerializeField] public int rows;
        [SerializeField] public float panelUtilization;
        [SerializeField] public Vector2 totalContentSize;
        [SerializeField] public bool isOptimal;

        // Additional calculated properties
        [SerializeField] public float aspectRatio;
        [SerializeField] public float contentDensity;
        [SerializeField] public Vector2 availableSpace;
        [SerializeField] public Vector2 unusedSpace;

        public LayoutData()
        {
            cellSize = Vector2.zero;
            spacing = Vector2.zero;
            padding = new RectOffset();
            columns = 0;
            rows = 0;
            panelUtilization = 0f;
            totalContentSize = Vector2.zero;
            isOptimal = false;
            aspectRatio = 1f;
            contentDensity = 0f;
            availableSpace = Vector2.zero;
            unusedSpace = Vector2.zero;
        }

        public void CalculateAdditionalProperties(Vector2 panelSize)
        {
            aspectRatio = columns > 0 && rows > 0 ? (float)columns / rows : 1f;
            contentDensity = panelSize.x * panelSize.y > 0 ? (totalContentSize.x * totalContentSize.y) / (panelSize.x * panelSize.y) : 0f;
            availableSpace = panelSize;
            unusedSpace = new Vector2(
                Mathf.Max(0, panelSize.x - totalContentSize.x),
                Mathf.Max(0, panelSize.y - totalContentSize.y)
            );
        }
    }

    [SerializeField] private SlotConfigurationManager config;
    [SerializeField] private LayoutData cachedLayoutData = new LayoutData();

    // Layout calculation settings
    [SerializeField, Tooltip("Maximum iterations for optimization algorithms")]
    private int maxOptimizationIterations = 50;

    [SerializeField, Tooltip("Tolerance for layout optimization")]
    private float optimizationTolerance = 0.01f;

    [SerializeField, Tooltip("Enable advanced layout metrics calculation")]
    private bool enableAdvancedMetrics = true;

    // Events
    [System.NonSerialized] public System.Action<LayoutData> OnLayoutChanged;

    // Properties
    public LayoutData CurrentLayout => cachedLayoutData;
    public SlotConfigurationManager Configuration => config;

    public void Initialize(SlotConfigurationManager configuration)
    {
        config = configuration;
        InitializeLayoutData();
    }

    private void InitializeLayoutData()
    {
        if (cachedLayoutData == null)
            cachedLayoutData = new LayoutData();
    }

    // Layout Calculation Main Entry Point
    public void CalculateOptimalLayout(int slotCount, Transform inventorySlotsParent)
    {
        if (config == null)
        {
            Debug.LogError("SlotConfigurationManager is null in SlotLayoutCalculator");
            return;
        }

        RectTransform panelRect = inventorySlotsParent?.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            Debug.LogError("InventorySlotsParent is missing RectTransform component");
            return;
        }

        slotCount = Mathf.Max(1, slotCount);
        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;

        // Handle zero-sized panels
        if (panelWidth <= 0 || panelHeight <= 0)
        {
            Debug.LogWarning($"Panel has zero or negative size: {panelWidth}x{panelHeight}. Using default size.");
            panelWidth = 400f;
            panelHeight = 300f;
        }

        var gridDimensions = CalculateGridDimensions(slotCount, panelWidth, panelHeight);
        var sizeAndSpacing = CalculateOptimalSizeAndSpacing(gridDimensions.x, gridDimensions.y, panelWidth, panelHeight);
        var padding = CalculatePadding(panelWidth, panelHeight);

        UpdateLayoutData(sizeAndSpacing.cellSize, sizeAndSpacing.spacing, padding, gridDimensions.x, gridDimensions.y, panelWidth, panelHeight, slotCount);
        OnLayoutChanged?.Invoke(cachedLayoutData);
    }

    // Grid Dimension Calculations
    private Vector2Int CalculateGridDimensions(int slotCount, float panelWidth, float panelHeight)
    {
        return config.ConstraintMode switch
        {
            SlotConfigurationManager.GridConstraintMode.FixedColumns => CalculateFixedColumns(slotCount),
            SlotConfigurationManager.GridConstraintMode.FixedRows => CalculateFixedRows(slotCount),
            SlotConfigurationManager.GridConstraintMode.AspectRatio => CalculateAspectRatioGrid(slotCount, panelWidth, panelHeight),
            SlotConfigurationManager.GridConstraintMode.Custom => CalculateCustomGrid(slotCount, panelWidth, panelHeight),
            SlotConfigurationManager.GridConstraintMode.Adaptive or _ => CalculateAdaptiveGrid(slotCount, panelWidth, panelHeight)
        };
    }

    private Vector2Int CalculateFixedColumns(int slotCount)
    {
        int columns = Mathf.Clamp(config.ForcedColumns, 1, config.MaxColumns);
        int rows = Mathf.CeilToInt((float)slotCount / columns);
        rows = Mathf.Clamp(rows, 1, config.MaxRows);
        return new Vector2Int(columns, rows);
    }

    private Vector2Int CalculateFixedRows(int slotCount)
    {
        int rows = Mathf.Clamp(config.ForcedRows, 1, config.MaxRows);
        int columns = Mathf.CeilToInt((float)slotCount / rows);
        columns = Mathf.Clamp(columns, 1, config.MaxColumns);
        return new Vector2Int(columns, rows);
    }

    private Vector2Int CalculateAspectRatioGrid(int slotCount, float panelWidth, float panelHeight)
    {
        float aspectRatio = panelWidth / panelHeight;
        int optimalColumns = Mathf.CeilToInt(Mathf.Sqrt(slotCount * aspectRatio));
        int optimalRows = Mathf.CeilToInt((float)slotCount / optimalColumns);

        return new Vector2Int(
            Mathf.Clamp(optimalColumns, 1, config.MaxColumns),
            Mathf.Clamp(optimalRows, 1, config.MaxRows)
        );
    }

    private Vector2Int CalculateCustomGrid(int slotCount, float panelWidth, float panelHeight)
    {
        // For custom mode, use a balanced approach similar to adaptive but with more flexibility
        return CalculateAdaptiveGrid(slotCount, panelWidth, panelHeight);
    }

    public Vector2Int CalculateAdaptiveGrid(int slotCount, float panelWidth, float panelHeight)
    {
        float aspectRatio = panelWidth / panelHeight;
        int bestColumns = 1;
        int bestRows = slotCount;
        float bestScore = float.MaxValue;

        // Try different grid configurations and score them
        for (int cols = 1; cols <= Mathf.Min(slotCount, config.MaxColumns); cols++)
        {
            int rows = Mathf.CeilToInt((float)slotCount / cols);
            if (rows > config.MaxRows) continue;

            float score = CalculateGridScore(cols, rows, slotCount, aspectRatio, panelWidth, panelHeight);

            if (score < bestScore)
            {
                bestScore = score;
                bestColumns = cols;
                bestRows = rows;
            }
        }

        return new Vector2Int(bestColumns, bestRows);
    }

    private float CalculateGridScore(int columns, int rows, int slotCount, float panelAspectRatio, float panelWidth, float panelHeight)
    {
        // Calculate various factors for scoring
        float gridAspectRatio = (float)columns / rows;
        float aspectDifference = Mathf.Abs(gridAspectRatio - panelAspectRatio);

        int totalSlots = columns * rows;
        float utilization = (float)slotCount / totalSlots;
        float overUtilization = Mathf.Max(0, 1f - utilization);

        // Calculate cell size for this configuration
        float cellWidth = (panelWidth - config.PreferredSpacing * (columns - 1)) / columns;
        float cellHeight = (panelHeight - config.PreferredSpacing * (rows - 1)) / rows;
        float cellSize = Mathf.Min(cellWidth, cellHeight);

        // Size score - prefer sizes close to the preferred range
        float sizeScore = 0f;
        if (cellSize < config.MinSlotSize)
            sizeScore = (config.MinSlotSize - cellSize) / config.MinSlotSize;
        else if (cellSize > config.MaxSlotSize && !config.AllowOversizedSlots)
            sizeScore = (cellSize - config.MaxSlotSize) / config.MaxSlotSize;

        // Combine scores with weights
        float totalScore = aspectDifference * 1.0f +       // Aspect ratio weight
                          overUtilization * 2.0f +         // Utilization weight
                          sizeScore * 1.5f;                // Size weight

        return totalScore;
    }

    // Size and Spacing Calculations
    private (Vector2 cellSize, Vector2 spacing) CalculateOptimalSizeAndSpacing(int columns, int rows, float panelWidth, float panelHeight)
    {
        Vector2 spacing = CalculateSpacing(columns, rows, panelWidth, panelHeight);
        RectOffset padding = CalculatePadding(panelWidth, panelHeight);

        float availableWidth = panelWidth - padding.horizontal - (spacing.x * (columns - 1));
        float availableHeight = panelHeight - padding.vertical - (spacing.y * (rows - 1));

        // Ensure minimum available space
        availableWidth = Mathf.Max(config.MinSlotSize * columns, availableWidth);
        availableHeight = Mathf.Max(config.MinSlotSize * rows, availableHeight);

        float cellWidth = availableWidth / columns;
        float cellHeight = availableHeight / rows;

        Vector2 cellSize = ApplyLayoutMode(cellWidth, cellHeight, spacing, columns, rows, panelWidth, panelHeight, padding);

        return (cellSize, spacing);
    }

    private Vector2 ApplyLayoutMode(float cellWidth, float cellHeight, Vector2 spacing, int columns, int rows, float panelWidth, float panelHeight, RectOffset padding)
    {
        Vector2 cellSize = CalculateCellSize(cellWidth, cellHeight);

        return config.CurrentLayoutMode switch
        {
            SlotConfigurationManager.LayoutMode.Compact => ApplyCompactMode(cellSize),
            SlotConfigurationManager.LayoutMode.Spacious => ApplySpeciousMode(cellSize, columns, rows, panelWidth, panelHeight, padding),
            SlotConfigurationManager.LayoutMode.FillPanel => ApplyFillPanelMode(columns, rows, panelWidth, panelHeight, padding, spacing),
            SlotConfigurationManager.LayoutMode.Custom => ApplyCustomMode(cellWidth, cellHeight),
            SlotConfigurationManager.LayoutMode.Adaptive or _ => cellSize
        };
    }

    private Vector2 ApplyCompactMode(Vector2 cellSize)
    {
        float size = Mathf.Min(cellSize.x, cellSize.y, config.MaxSlotSize);
        size = Mathf.Max(size, config.MinSlotSize);
        return Vector2.one * size;
    }

    private Vector2 ApplySpeciousMode(Vector2 cellSize, int columns, int rows, float panelWidth, float panelHeight, RectOffset padding)
    {
        // In spacious mode, prefer larger spacing and moderate cell sizes
        float maxAllowedSize = Mathf.Min(config.MaxSlotSize, Mathf.Max(cellSize.x, cellSize.y));
        float size = Mathf.Clamp(maxAllowedSize, config.MinSlotSize, config.MaxSlotSize);

        if (config.MaintainSquareSlots)
            return Vector2.one * size;

        return new Vector2(
            Mathf.Clamp(cellSize.x, config.MinSlotSize, config.MaxSlotSize),
            Mathf.Clamp(cellSize.y, config.MinSlotSize, config.MaxSlotSize)
        );
    }

    private Vector2 ApplyFillPanelMode(int columns, int rows, float panelWidth, float panelHeight, RectOffset padding, Vector2 spacing)
    {
        float availableWidth = panelWidth - padding.horizontal;
        float availableHeight = panelHeight - padding.vertical;

        float totalSpacingWidth = spacing.x * (columns - 1);
        float totalSpacingHeight = spacing.y * (rows - 1);

        Vector2 cellSize = new Vector2(
            (availableWidth - totalSpacingWidth) / columns,
            (availableHeight - totalSpacingHeight) / rows
        );

        if (config.MaintainSquareSlots)
        {
            float size = Mathf.Min(cellSize.x, cellSize.y);
            cellSize = Vector2.one * size;
        }

        // Apply oversize limits
        float maxAllowedSizeForFill = config.AllowOversizedSlots ? config.MaxSlotSize * config.OversizeMultiplier : config.MaxSlotSize;
        cellSize.x = Mathf.Clamp(cellSize.x, config.MinSlotSize, maxAllowedSizeForFill);
        cellSize.y = Mathf.Clamp(cellSize.y, config.MinSlotSize, maxAllowedSizeForFill);

        return cellSize;
    }

    private Vector2 ApplyCustomMode(float cellWidth, float cellHeight)
    {
        return new Vector2(
            Mathf.Clamp(cellWidth, config.MinSlotSize, config.MaxSlotSize),
            Mathf.Clamp(cellHeight, config.MinSlotSize, config.MaxSlotSize)
        );
    }

    private Vector2 CalculateSpacing(int columns, int rows, float panelWidth, float panelHeight)
    {
        if (!config.UseUniformSpacing)
            return config.CustomSpacing;

        float spacing = config.CurrentSpaceDistribution switch
        {
            SlotConfigurationManager.SpaceDistribution.EvenSpacing => CalculateEvenSpacing(columns, rows, panelWidth, panelHeight),
            SlotConfigurationManager.SpaceDistribution.LargerSlots => config.MinSpacing,
            SlotConfigurationManager.SpaceDistribution.ExtraPadding => config.PreferredSpacing * 0.8f,
            SlotConfigurationManager.SpaceDistribution.Balanced or _ => config.PreferredSpacing
        };

        return Vector2.one * Mathf.Clamp(spacing, config.MinSpacing, config.MaxSpacing);
    }

    private float CalculateEvenSpacing(int columns, int rows, float panelWidth, float panelHeight)
    {
        // Calculate spacing that would evenly distribute remaining space
        RectOffset padding = CalculatePadding(panelWidth, panelHeight);

        float availableWidth = panelWidth - padding.horizontal - (columns * config.MinSlotSize);
        float spacingWidth = availableWidth / (columns - 1);

        float availableHeight = panelHeight - padding.vertical - (rows * config.MinSlotSize);
        float spacingHeight = availableHeight / (rows - 1);

        return Mathf.Min(spacingWidth, spacingHeight);
    }

    private RectOffset CalculatePadding(float panelWidth, float panelHeight)
    {
        if (config.UseCustomPadding)
            return new RectOffset(config.PaddingLeft, config.PaddingRight, config.PaddingTop, config.PaddingBottom);

        if (config.AdaptivePadding)
        {
            float avgDimension = (panelWidth + panelHeight) * 0.5f;
            int padding = Mathf.RoundToInt(avgDimension * config.PaddingPercentage * 0.01f);
            return new RectOffset(padding, padding, padding, padding);
        }

        int defaultPadding = Mathf.RoundToInt(config.PreferredSpacing);
        return new RectOffset(defaultPadding, defaultPadding, defaultPadding, defaultPadding);
    }

    private Vector2 CalculateCellSize(float cellWidth, float cellHeight)
    {
        if (config.MaintainSquareSlots)
        {
            float size = Mathf.Min(cellWidth, cellHeight);
            float maxAllowedCellSize = config.AllowOversizedSlots ? config.MaxSlotSize * config.OversizeMultiplier : config.MaxSlotSize;
            size = Mathf.Clamp(size, config.MinSlotSize, maxAllowedCellSize);
            return Vector2.one * size;
        }

        if (config.PreserveAspectRatio)
        {
            float aspectRatio = config.SlotAspectRatio.x / config.SlotAspectRatio.y;
            if (cellWidth / cellHeight > aspectRatio)
                cellWidth = cellHeight * aspectRatio;
            else
                cellHeight = cellWidth / aspectRatio;
        }

        float maxCellSizeLimit = config.AllowOversizedSlots ? config.MaxSlotSize * config.OversizeMultiplier : config.MaxSlotSize;
        return new Vector2(
            Mathf.Clamp(cellWidth, config.MinSlotSize, maxCellSizeLimit),
            Mathf.Clamp(cellHeight, config.MinSlotSize, maxCellSizeLimit)
        );
    }

    private void UpdateLayoutData(Vector2 cellSize, Vector2 spacing, RectOffset padding, int columns, int rows, float panelWidth, float panelHeight, int slotCount)
    {
        cachedLayoutData.cellSize = cellSize;
        cachedLayoutData.spacing = spacing;
        cachedLayoutData.padding = padding;
        cachedLayoutData.columns = columns;
        cachedLayoutData.rows = rows;

        float totalContentWidth = (cellSize.x * columns) + (spacing.x * (columns - 1)) + padding.horizontal;
        float totalContentHeight = (cellSize.y * rows) + (spacing.y * (rows - 1)) + padding.vertical;

        cachedLayoutData.totalContentSize = new Vector2(totalContentWidth, totalContentHeight);
        cachedLayoutData.panelUtilization = (totalContentWidth * totalContentHeight) / (panelWidth * panelHeight);
        cachedLayoutData.isOptimal = EvaluateLayoutOptimality(slotCount);

        if (enableAdvancedMetrics)
        {
            cachedLayoutData.CalculateAdditionalProperties(new Vector2(panelWidth, panelHeight));
        }
    }

    private bool EvaluateLayoutOptimality(int slotCount)
    {
        // A layout is considered optimal if:
        // 1. Panel utilization is above minimum threshold
        // 2. Cell size is within acceptable range
        // 3. Space usage is efficient

        bool utilizationOK = cachedLayoutData.panelUtilization >= config.MinPanelUtilization;
        bool sizeOK = cachedLayoutData.cellSize.x >= config.MinSlotSize && cachedLayoutData.cellSize.x <= config.MaxSlotSize;
        bool gridOK = (cachedLayoutData.columns * cachedLayoutData.rows) >= slotCount;

        return utilizationOK && sizeOK && gridOK;
    }

    // Utility Methods
    public bool IsLayoutOptimal() => cachedLayoutData?.isOptimal ?? false;

    public float GetPanelUtilization() => cachedLayoutData?.panelUtilization ?? 0f;

    public Vector2 GetRecommendedPanelSize(int slotCount)
    {
        if (config == null) return Vector2.zero;

        var gridDims = CalculateAdaptiveGrid(slotCount, 800f, 600f); // Use reference size

        float width = (config.MaxSlotSize * gridDims.x) + (config.PreferredSpacing * (gridDims.x - 1)) + (config.PreferredSpacing * 2);
        float height = (config.MaxSlotSize * gridDims.y) + (config.PreferredSpacing * (gridDims.y - 1)) + (config.PreferredSpacing * 2);

        return new Vector2(width, height);
    }

    public TextAnchor GetTextAnchorFromAlignment(SlotConfigurationManager.ContentAlignment alignment)
    {
        return alignment switch
        {
            SlotConfigurationManager.ContentAlignment.TopLeft => TextAnchor.UpperLeft,
            SlotConfigurationManager.ContentAlignment.TopCenter => TextAnchor.UpperCenter,
            SlotConfigurationManager.ContentAlignment.TopRight => TextAnchor.UpperRight,
            SlotConfigurationManager.ContentAlignment.MiddleLeft => TextAnchor.MiddleLeft,
            SlotConfigurationManager.ContentAlignment.Center => TextAnchor.MiddleCenter,
            SlotConfigurationManager.ContentAlignment.MiddleRight => TextAnchor.MiddleRight,
            SlotConfigurationManager.ContentAlignment.BottomLeft => TextAnchor.LowerLeft,
            SlotConfigurationManager.ContentAlignment.BottomCenter => TextAnchor.LowerCenter,
            SlotConfigurationManager.ContentAlignment.BottomRight => TextAnchor.LowerRight,
            _ => TextAnchor.UpperLeft
        };
    }

    // Advanced layout analysis
    public float CalculateSpaceEfficiency()
    {
        if (cachedLayoutData == null) return 0f;

        float usedSpace = cachedLayoutData.cellSize.x * cachedLayoutData.cellSize.y * cachedLayoutData.columns * cachedLayoutData.rows;
        float totalSpace = cachedLayoutData.totalContentSize.x * cachedLayoutData.totalContentSize.y;

        return totalSpace > 0 ? usedSpace / totalSpace : 0f;
    }

    public Vector2Int GetOptimalGridForTargetSize(Vector2 targetCellSize, int slotCount)
    {
        int bestColumns = 1;
        int bestRows = slotCount;
        float bestSizeDifference = float.MaxValue;

        for (int cols = 1; cols <= Mathf.Min(slotCount, config.MaxColumns); cols++)
        {
            int rows = Mathf.CeilToInt((float)slotCount / cols);
            if (rows > config.MaxRows) continue;

            // Calculate what cell size this configuration would produce
            float testCellSize = Mathf.Min(targetCellSize.x, targetCellSize.y);
            float sizeDifference = Mathf.Abs(testCellSize - config.PreferredSpacing);

            if (sizeDifference < bestSizeDifference)
            {
                bestSizeDifference = sizeDifference;
                bestColumns = cols;
                bestRows = rows;
            }
        }

        return new Vector2Int(bestColumns, bestRows);
    }

    // Debug and analysis methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogLayoutAnalysis()
    {
        if (cachedLayoutData == null) return;

        Debug.Log("=== Layout Analysis ===");
        Debug.Log($"Grid: {cachedLayoutData.columns}x{cachedLayoutData.rows}");
        Debug.Log($"Cell Size: {cachedLayoutData.cellSize.x:F1}x{cachedLayoutData.cellSize.y:F1}");
        Debug.Log($"Spacing: {cachedLayoutData.spacing.x:F1}x{cachedLayoutData.spacing.y:F1}");
        Debug.Log($"Panel Utilization: {cachedLayoutData.panelUtilization:P2}");
        Debug.Log($"Space Efficiency: {CalculateSpaceEfficiency():P2}");
        Debug.Log($"Optimal: {cachedLayoutData.isOptimal}");

        if (enableAdvancedMetrics)
        {
            Debug.Log($"Content Density: {cachedLayoutData.contentDensity:P2}");
            Debug.Log($"Grid Aspect Ratio: {cachedLayoutData.aspectRatio:F2}");
            Debug.Log($"Unused Space: {cachedLayoutData.unusedSpace.x:F1}x{cachedLayoutData.unusedSpace.y:F1}");
        }
    }
}