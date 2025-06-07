using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotValidationManager
{
    [Header("Validation Settings")]
    [SerializeField, Tooltip("Enable comprehensive slot validation")]
    private bool enableValidation = true;

    [SerializeField, Tooltip("Log validation warnings to console")]
    private bool logValidationWarnings = true;

    [SerializeField, Tooltip("Log validation info messages")]
    private bool logValidationInfo = false;

    // Validation statistics
    private int totalSlotsValidated;
    private int validationErrors;
    private int validationWarnings;

    // Properties
    public bool EnableValidation => enableValidation;
    public int TotalSlotsValidated => totalSlotsValidated;
    public int ValidationErrors => validationErrors;
    public int ValidationWarnings => validationWarnings;

    public void Initialize()
    {
        ResetValidationStats();
    }

    private void ResetValidationStats()
    {
        totalSlotsValidated = 0;
        validationErrors = 0;
        validationWarnings = 0;
    }

    // Component Validation
    public bool ValidateSlotManagerComponents(
        GameObject slotPrefab,
        Transform hotbarSlotsParent,
        Transform inventorySlotsParent,
        GridLayoutGroup inventoryGridLayout,
        GridLayoutGroup hotbarGridLayout)
    {
        if (!enableValidation) return true;

        bool isValid = true;

        // Validate slot prefab
        if (!ValidateSlotPrefab(slotPrefab))
            isValid = false;

        // Validate parent transforms
        if (!ValidateParentTransforms(hotbarSlotsParent, inventorySlotsParent))
            isValid = false;

        // Validate grid layout components
        if (!ValidateGridLayouts(inventoryGridLayout, hotbarGridLayout))
            isValid = false;

        LogValidationSummary(isValid);
        return isValid;
    }

    private bool ValidateSlotPrefab(GameObject slotPrefab)
    {
        if (slotPrefab == null)
        {
            LogError("Slot Prefab is not assigned");
            return false;
        }

        // Check for required components
        var requiredComponents = new System.Type[]
        {
            typeof(RectTransform),
            typeof(Image)
        };

        bool hasAllComponents = true;
        foreach (var componentType in requiredComponents)
        {
            if (slotPrefab.GetComponent(componentType) == null)
            {
                LogWarning($"Slot Prefab is missing {componentType.Name} component");
                hasAllComponents = false;
            }
        }

        // Check for InventorySlot component (can be added dynamically)
        if (slotPrefab.GetComponent<InventorySlot>() == null)
        {
            LogInfo("Slot Prefab doesn't have InventorySlot component (will be added dynamically)");
        }

        return hasAllComponents;
    }

    private bool ValidateParentTransforms(Transform hotbarParent, Transform inventoryParent)
    {
        bool isValid = true;

        if (hotbarParent == null)
        {
            LogError("Hotbar Slots Parent is not assigned");
            isValid = false;
        }
        else
        {
            ValidateTransformHierarchy(hotbarParent, "Hotbar");
        }

        if (inventoryParent == null)
        {
            LogError("Inventory Slots Parent is not assigned");
            isValid = false;
        }
        else
        {
            ValidateTransformHierarchy(inventoryParent, "Inventory");
        }

        return isValid;
    }

    private void ValidateTransformHierarchy(Transform parent, string parentType)
    {
        // Check if parent has RectTransform (required for UI)
        if (parent.GetComponent<RectTransform>() == null)
        {
            LogWarning($"{parentType} parent is missing RectTransform component");
        }

        // Check if parent is properly set up for UI
        Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            LogWarning($"{parentType} parent is not under a Canvas");
        }

        // Check for potential layout conflicts
        if (parent.GetComponent<LayoutGroup>() != null)
        {
            LogInfo($"{parentType} parent has a LayoutGroup component - ensure it's compatible with dynamic slot creation");
        }
    }

    private bool ValidateGridLayouts(GridLayoutGroup inventoryGrid, GridLayoutGroup hotbarGrid)
    {
        bool isValid = true;

        if (inventoryGrid == null)
        {
            LogError("Inventory Grid Layout is not assigned");
            isValid = false;
        }
        else
        {
            ValidateGridLayoutSettings(inventoryGrid, "Inventory");
        }

        if (hotbarGrid == null)
        {
            LogError("Hotbar Grid Layout is not assigned");
            isValid = false;
        }
        else
        {
            ValidateGridLayoutSettings(hotbarGrid, "Hotbar");
        }

        return isValid;
    }

    private void ValidateGridLayoutSettings(GridLayoutGroup gridLayout, string gridType)
    {
        // Check for reasonable cell size
        if (gridLayout.cellSize.x <= 0 || gridLayout.cellSize.y <= 0)
        {
            LogWarning($"{gridType} Grid Layout has invalid cell size: {gridLayout.cellSize}");
        }

        // Check for negative spacing
        if (gridLayout.spacing.x < 0 || gridLayout.spacing.y < 0)
        {
            LogWarning($"{gridType} Grid Layout has negative spacing: {gridLayout.spacing}");
        }

        // Check constraint settings
        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount && gridLayout.constraintCount <= 0)
        {
            LogWarning($"{gridType} Grid Layout has invalid constraint count: {gridLayout.constraintCount}");
        }

        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount && gridLayout.constraintCount <= 0)
        {
            LogWarning($"{gridType} Grid Layout has invalid constraint count: {gridLayout.constraintCount}");
        }
    }

    // Slot Validation
    public bool ValidateSlot(GameObject slot, string slotName, bool isHotbarSlot = false)
    {
        if (!enableValidation) return true;

        totalSlotsValidated++;

        if (slot == null)
        {
            LogError($"Slot {slotName} is null");
            return false;
        }

        bool isValid = true;

        // Validate slot components
        if (!ValidateSlotComponents(slot, slotName))
            isValid = false;

        // Validate slot setup
        if (!ValidateSlotSetup(slot, slotName, isHotbarSlot))
            isValid = false;

        return isValid;
    }

    private bool ValidateSlotComponents(GameObject slot, string slotName)
    {
        bool isValid = true;

        // Check for RectTransform
        if (slot.GetComponent<RectTransform>() == null)
        {
            LogError($"Slot {slotName} is missing RectTransform component");
            isValid = false;
        }

        // Check for Image component
        var image = slot.GetComponent<Image>();
        if (image == null)
        {
            LogWarning($"Slot {slotName} is missing Image component");
            isValid = false;
        }
        else
        {
            // Validate Image settings
            if (!image.raycastTarget)
            {
                LogInfo($"Slot {slotName} Image component has raycastTarget disabled - this may affect interaction");
            }
        }

        // Check for InventorySlot component
        var inventorySlot = slot.GetComponent<InventorySlot>();
        if (inventorySlot == null)
        {
            LogWarning($"Slot {slotName} is missing InventorySlot component");
            isValid = false;
        }
        else
        {
            // Validate InventorySlot setup
            if (!inventorySlot.ValidateSlotSetup())
            {
                LogWarning($"Slot {slotName} failed InventorySlot validation");
            }
        }

        return isValid;
    }

    private bool ValidateSlotSetup(GameObject slot, string slotName, bool isHotbarSlot)
    {
        bool isValid = true;

        // Check slot naming convention
        if (isHotbarSlot && !slotName.Contains("Hotbar"))
        {
            LogInfo($"Hotbar slot {slotName} doesn't follow naming convention (should contain 'Hotbar')");
        }

        if (!isHotbarSlot && slotName.Contains("Hotbar"))
        {
            LogWarning($"Non-hotbar slot {slotName} has 'Hotbar' in name but is not marked as hotbar slot");
        }

        // Check parent hierarchy
        Transform parent = slot.transform.parent;
        if (parent == null)
        {
            LogError($"Slot {slotName} has no parent transform");
            isValid = false;
        }

        return isValid;
    }

    // Layout Validation
    public bool ValidateLayoutData(SlotLayoutCalculator.LayoutData layoutData, int expectedSlotCount)
    {
        if (!enableValidation || layoutData == null) return true;

        bool isValid = true;

        // Validate grid dimensions
        if (layoutData.columns <= 0 || layoutData.rows <= 0)
        {
            LogError($"Invalid grid dimensions: {layoutData.columns}x{layoutData.rows}");
            isValid = false;
        }

        // Validate cell size
        if (layoutData.cellSize.x <= 0 || layoutData.cellSize.y <= 0)
        {
            LogError($"Invalid cell size: {layoutData.cellSize}");
            isValid = false;
        }

        // Validate slot capacity
        int totalSlots = layoutData.columns * layoutData.rows;
        if (totalSlots < expectedSlotCount)
        {
            LogWarning($"Grid capacity ({totalSlots}) is less than expected slot count ({expectedSlotCount})");
        }

        // Validate panel utilization
        if (layoutData.panelUtilization < 0.1f)
        {
            LogWarning($"Very low panel utilization: {layoutData.panelUtilization:P1}");
        }

        if (layoutData.panelUtilization > 1.5f)
        {
            LogWarning($"Panel utilization exceeds panel bounds: {layoutData.panelUtilization:P1}");
        }

        // Validate spacing
        if (layoutData.spacing.x < 0 || layoutData.spacing.y < 0)
        {
            LogWarning($"Negative spacing detected: {layoutData.spacing}");
        }

        return isValid;
    }

    // Configuration Validation
    public bool ValidateConfiguration(SlotConfigurationManager config)
    {
        if (!enableValidation || config == null) return true;

        bool isValid = true;

        // Validate size constraints
        if (config.MinSlotSize <= 0)
        {
            LogError($"Invalid minimum slot size: {config.MinSlotSize}");
            isValid = false;
        }

        if (config.MaxSlotSize <= config.MinSlotSize)
        {
            LogError($"Maximum slot size ({config.MaxSlotSize}) must be greater than minimum ({config.MinSlotSize})");
            isValid = false;
        }

        // Validate spacing constraints
        if (config.MinSpacing < 0)
        {
            LogWarning($"Negative minimum spacing: {config.MinSpacing}");
        }

        if (config.MaxSpacing < config.MinSpacing)
        {
            LogError($"Maximum spacing ({config.MaxSpacing}) must be greater than minimum ({config.MinSpacing})");
            isValid = false;
        }

        if (config.PreferredSpacing < config.MinSpacing || config.PreferredSpacing > config.MaxSpacing)
        {
            LogWarning($"Preferred spacing ({config.PreferredSpacing}) is outside min/max range ({config.MinSpacing}-{config.MaxSpacing})");
        }

        // Validate grid constraints
        if (config.MaxColumns <= 0 || config.MaxRows <= 0)
        {
            LogError($"Invalid max grid dimensions: {config.MaxColumns}x{config.MaxRows}");
            isValid = false;
        }

        if (config.ForcedColumns <= 0 || config.ForcedRows <= 0)
        {
            LogWarning($"Invalid forced grid dimensions: {config.ForcedColumns}x{config.ForcedRows}");
        }

        return isValid;
    }

    // Logging Methods
    private void LogError(string message)
    {
        validationErrors++;
        Debug.LogError($"[SlotValidation ERROR] {message}");
    }

    private void LogWarning(string message)
    {
        validationWarnings++;
        if (logValidationWarnings)
        {
            Debug.LogWarning($"[SlotValidation WARNING] {message}");
        }
    }

    private void LogInfo(string message)
    {
        if (logValidationInfo)
        {
            Debug.Log($"[SlotValidation INFO] {message}");
        }
    }

    private void LogValidationSummary(bool isValid)
    {
        if (logValidationInfo)
        {
            string status = isValid ? "PASSED" : "FAILED";
            Debug.Log($"[SlotValidation] Component validation {status} - Errors: {validationErrors}, Warnings: {validationWarnings}");
        }
    }

    // Public Utility Methods
    public void SetValidationLevel(bool enableValidation, bool logWarnings = true, bool logInfo = false)
    {
        this.enableValidation = enableValidation;
        this.logValidationWarnings = logWarnings;
        this.logValidationInfo = logInfo;
    }

    public string GetValidationReport()
    {
        return $"Validation Report:\n" +
               $"- Total Slots Validated: {totalSlotsValidated}\n" +
               $"- Validation Errors: {validationErrors}\n" +
               $"- Validation Warnings: {validationWarnings}\n" +
               $"- Success Rate: {(totalSlotsValidated > 0 ? (float)(totalSlotsValidated - validationErrors) / totalSlotsValidated * 100f : 0f):F1}%";
    }

    public void ClearValidationStats()
    {
        ResetValidationStats();
    }
}