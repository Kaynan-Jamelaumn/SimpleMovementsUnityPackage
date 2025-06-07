using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotLayoutCalculator
{
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

    private SlotConfigurationManager config;
    private LayoutData cachedLayoutData;

    // Events
    public System.Action<LayoutData> OnLayoutChanged;

    // Properties
    public LayoutData CurrentLayout => cachedLayoutData;

    public void Initialize(SlotConfigurationManager configuration)
    {
        config = configuration;
        InitializeLayoutData();
    }

    private void InitializeLayoutData()
    {
        cachedLayoutData = new LayoutData();
    }

    // Layout Calculation Main Entry Point
    public void CalculateOptimalLayout(int slotCount, Transform inventorySlotsParent)
    {
        RectTransform panelRect = inventorySlotsParent.GetComponent<RectTransform>();
        if (panelRect == null) return;

        slotCount = Mathf.Max(1, slotCount);
        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;

        var gridDimensions = CalculateGridDimensions(slotCount, panelWidth, panelHeight);
        var sizeAndSpacing = CalculateOptimalSizeAndSpacing(gridDimensions.x, gridDimensions.y, panelWidth, panelHeight);
        var padding = CalculatePadding(panelWidth, panelHeight);

        UpdateLayoutData(sizeAndSpacing.cellSize, sizeAndSpacing.spacing, padding, gridDimensions.x, gridDimensions.y, panelWidth, panelHeight);
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
            SlotConfigurationManager.GridConstraintMode.Adaptive or _ => CalculateAdaptiveGrid(slotCount, panelWidth, panelHeight)
        };
    }

    private Vector2Int CalculateFixedColumns(int slotCount)
    {
        int columns = Mathf.Clamp(config.ForcedColumns, 1, config.MaxColumns);
        int rows = Mathf.CeilToInt((float)slotCount / columns);
        return new Vector2Int(columns, rows);
    }

    private Vector2Int CalculateFixedRows(int slotCount)
    {
        int rows = Mathf.Clamp(config.ForcedRows, 1, config.MaxRows);
        int columns = Mathf.CeilToInt((float)slotCount / rows);
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

    public Vector2Int CalculateAdaptiveGrid(int slotCount, float panelWidth, float panelHeight)
    {
        float aspectRatio = panelWidth / panelHeight;
        int bestColumns = 1;
        int bestRows = slotCount;
        float bestScore = float.MaxValue;

        for (int cols = 1; cols <= Mathf.Min(slotCount, config.MaxColumns); cols++)
        {
            int rows = Mathf.CeilToInt((float)slotCount / cols);
            if (rows > config.MaxRows) continue;

            float cellAspectRatio = (panelWidth / cols) / (panelHeight / rows);
            float aspectDifference = Mathf.Abs(cellAspectRatio - 1f);
            float utilization = (float)(cols * rows) / slotCount;
            float overUtilization = Mathf.Max(0, utilization - 1f);
            float score = aspectDifference + overUtilization * 2f;

            if (score < bestScore)
            {
                bestScore = score;
                bestColumns = cols;
                bestRows = rows;
            }
        }

        return new Vector2Int(bestColumns, bestRows);
    }

    // Size and Spacing Calculations
    private (Vector2 cellSize, Vector2 spacing) CalculateOptimalSizeAndSpacing(int columns, int rows, float panelWidth, float panelHeight)
    {
        Vector2 spacing = CalculateSpacing();
        RectOffset padding = CalculatePadding(panelWidth, panelHeight);

        float availableWidth = panelWidth - padding.horizontal - (spacing.x * (columns - 1));
        float availableHeight = panelHeight - padding.vertical - (spacing.y * (rows - 1));

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
            SlotConfigurationManager.LayoutMode.Spacious => ApplySpeciousMode(cellSize),
            SlotConfigurationManager.LayoutMode.FillPanel => ApplyFillPanelMode(columns, rows, panelWidth, panelHeight, padding, spacing),
            SlotConfigurationManager.LayoutMode.Custom => ApplyCustomMode(cellWidth, cellHeight),
            SlotConfigurationManager.LayoutMode.Adaptive or _ => cellSize
        };
    }

    private Vector2 ApplyCompactMode(Vector2 cellSize)
    {
        return Vector2.one * Mathf.Min(cellSize.x, cellSize.y, config.MaxSlotSize);
    }

    private Vector2 ApplySpeciousMode(Vector2 cellSize)
    {
        // Spacious mode handled by spacing calculation
        return cellSize;
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

        return cellSize;
    }

    private Vector2 ApplyCustomMode(float cellWidth, float cellHeight)
    {
        return new Vector2(
            Mathf.Clamp(cellWidth, config.MinSlotSize, config.MaxSlotSize),
            Mathf.Clamp(cellHeight, config.MinSlotSize, config.MaxSlotSize)
        );
    }

    private Vector2 CalculateSpacing()
    {
        if (!config.UseUniformSpacing)
            return config.CustomSpacing;

        float spacing = config.CurrentSpaceDistribution switch
        {
            SlotConfigurationManager.SpaceDistribution.EvenSpacing => Mathf.Clamp(config.PreferredSpacing, config.MinSpacing, config.MaxSpacing),
            SlotConfigurationManager.SpaceDistribution.LargerSlots => config.MinSpacing,
            SlotConfigurationManager.SpaceDistribution.ExtraPadding or SlotConfigurationManager.SpaceDistribution.Balanced => config.PreferredSpacing,
            _ => config.PreferredSpacing
        };

        return Vector2.one * spacing;
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
            size = Mathf.Clamp(size, config.MinSlotSize, config.AllowOversizedSlots ? config.MaxSlotSize * config.OversizeMultiplier : config.MaxSlotSize);
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

        float maxSize = config.AllowOversizedSlots ? config.MaxSlotSize * config.OversizeMultiplier : config.MaxSlotSize;
        return new Vector2(
            Mathf.Clamp(cellWidth, config.MinSlotSize, maxSize),
            Mathf.Clamp(cellHeight, config.MinSlotSize, maxSize)
        );
    }

    private void UpdateLayoutData(Vector2 cellSize, Vector2 spacing, RectOffset padding, int columns, int rows, float panelWidth, float panelHeight)
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
        cachedLayoutData.isOptimal = cachedLayoutData.panelUtilization >= config.MinPanelUtilization;
    }

    // Utility Methods
    public bool IsLayoutOptimal() => cachedLayoutData?.isOptimal ?? false;

    public float GetPanelUtilization() => cachedLayoutData?.panelUtilization ?? 0f;

    public Vector2 GetRecommendedPanelSize(int slotCount)
    {
        var gridDims = CalculateAdaptiveGrid(slotCount, 800f, 600f);

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
}