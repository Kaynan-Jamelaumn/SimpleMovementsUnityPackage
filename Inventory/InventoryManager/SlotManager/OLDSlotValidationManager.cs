using UnityEngine;
using UnityEngine.UI;
using System.Text;

[System.Serializable]
public class OLDSlotValidationManager
{
    [Header("Validation Settings")]
    [SerializeField, Tooltip("Enable comprehensive slot validation")]
    private bool enableValidation = true;

    [SerializeField, Tooltip("Log validation warnings to console")]
    private bool logValidationWarnings = true;

    [SerializeField, Tooltip("Log validation info messages")]
    private bool logValidationInfo = false;

    [SerializeField, Tooltip("Auto-fix minor validation issues")]
    private bool autoFixMinorIssues = true;

    [SerializeField, Tooltip("Validation check frequency (in seconds)")]
    private float validationInterval = 5f;

    // Validation statistics
    [SerializeField] private int totalSlotsValidated;
    [SerializeField] private int validationErrors;
    [SerializeField] private int validationWarnings;
    [SerializeField] private int autoFixedIssues;

    // Runtime tracking (non-serialized)
    [System.NonSerialized] private float lastValidationTime;
    [System.NonSerialized] private System.Collections.Generic.List<string> validationLog;

    // Properties
    public bool EnableValidation => enableValidation;
    public int TotalSlotsValidated => totalSlotsValidated;
    public int ValidationErrors => validationErrors;
    public int ValidationWarnings => validationWarnings;
    public int AutoFixedIssues => autoFixedIssues;
    public float ValidationSuccessRate => totalSlotsValidated > 0 ? (float)(totalSlotsValidated - validationErrors) / totalSlotsValidated : 1f;

    public void Initialize()
    {
        ResetValidationStats();
        validationLog = new System.Collections.Generic.List<string>();
        lastValidationTime = Time.realtimeSinceStartup;

        if (enableValidation && logValidationInfo)
        {
            Debug.Log("SlotValidationManager initialized");
        }
    }

    public void Update()
    {
        if (!enableValidation) return;

        // Periodic validation check
        if (Time.realtimeSinceStartup - lastValidationTime > validationInterval)
        {
            PerformPeriodicValidation();
            lastValidationTime = Time.realtimeSinceStartup;
        }
    }

    private void PerformPeriodicValidation()
    {
        // This could be expanded to perform automatic validation checks
        if (logValidationInfo)
        {
            LogInfo("Performing periodic validation check");
        }
    }

    private void ResetValidationStats()
    {
        totalSlotsValidated = 0;
        validationErrors = 0;
        validationWarnings = 0;
        autoFixedIssues = 0;
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
        LogInfo("Starting SlotManager component validation");

        // Validate slot prefab
        if (!ValidateSlotPrefab(slotPrefab))
            isValid = false;

        // Validate parent transforms  Check  slot parents, not UI panels
        if (!ValidateParentTransforms(hotbarSlotsParent, inventorySlotsParent))
            isValid = false;

        // Validate grid layout components
        if (!ValidateGridLayouts(inventoryGridLayout, hotbarGridLayout))
            isValid = false;

        LogValidationSummary(isValid, "SlotManager Components");
        return isValid;
    }

    private bool ValidateSlotPrefab(GameObject slotPrefab)
    {
        if (slotPrefab == null)
        {
            LogError("Slot Prefab is not assigned");
            return false;
        }

        bool isValid = true;

        // Check for required components
        var requiredComponents = new System.Type[]
        {
            typeof(RectTransform),
            typeof(Image)
        };

        foreach (var componentType in requiredComponents)
        {
            if (slotPrefab.GetComponent(componentType) == null)
            {
                LogWarning($"Slot Prefab is missing {componentType.Name} component");
                isValid = false;

                // Auto-fix if enabled
                if (autoFixMinorIssues && componentType == typeof(Image))
                {
                    var addedComponent = slotPrefab.AddComponent<Image>();
                    if (addedComponent != null)
                    {
                        LogInfo($"Auto-fixed: Added {componentType.Name} component to slot prefab");
                        autoFixedIssues++;
                        isValid = true;
                    }
                }
            }
        }

        // Check for InventorySlot component (can be added dynamically)
        if (slotPrefab.GetComponent<InventorySlot>() == null)
        {
            LogInfo("Slot Prefab doesn't have InventorySlot component (will be added dynamically)");
        }

        // Validate prefab structure
        if (!ValidatePrefabStructure(slotPrefab))
        {
            isValid = false;
        }

        return isValid;
    }

    private bool ValidatePrefabStructure(GameObject prefab)
    {
        bool isValid = true;

        // Check RectTransform settings
        RectTransform rectTransform = prefab.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            if (rectTransform.sizeDelta == Vector2.zero)
            {
                LogWarning("Slot Prefab RectTransform has zero size");
                if (autoFixMinorIssues)
                {
                    rectTransform.sizeDelta = new Vector2(80, 80); // Default slot size
                    LogInfo("Auto-fixed: Set default slot size");
                    autoFixedIssues++;
                }
            }
        }

        return isValid;
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
            if (!ValidateTransformHierarchy(hotbarParent, "Hotbar", false)) // Don't check if UI panels are active
                isValid = false;
        }

        if (inventoryParent == null)
        {
            LogError("Inventory Slots Parent is not assigned");
            isValid = false;
        }
        else
        {
            if (!ValidateTransformHierarchy(inventoryParent, "Inventory", false)) // Don't check if UI panels are active
                isValid = false;
        }

        return isValid;
    }

    private bool ValidateTransformHierarchy(Transform parent, string parentType, bool checkActiveState = true)
    {
        bool isValid = true;

        // Check if parent has RectTransform (required for UI)
        if (parent.GetComponent<RectTransform>() == null)
        {
            LogWarning($"{parentType} parent is missing RectTransform component");
            isValid = false;
        }

        // Check if parent is properly set up for UI
        Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            LogWarning($"{parentType} parent is not under a Canvas");
            isValid = false;
        }

        // Check for potential layout conflicts
        LayoutGroup layoutGroup = parent.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            LogInfo($"{parentType} parent has a {layoutGroup.GetType().Name} component - ensure it's compatible with dynamic slot creation");
        }

        // Only check activity state if explicitly requested and the parent is a direct UI panel
        // Slot parents inside UI panels should always be active, but UI panels themselves can start inactive
        if (checkActiveState && !parent.gameObject.activeInHierarchy)
        {
            // Check if this is a slot parent (which should be active) or a UI panel (which can be inactive)
            bool isSlotParent = parent.name.ToLower().Contains("slot") || parent.GetComponent<GridLayoutGroup>() != null;

            if (isSlotParent)
            {
                LogWarning($"{parentType} slots parent GameObject is not active in hierarchy");
                isValid = false;
            }
            else
            {
                LogInfo($"{parentType} UI panel is inactive (this is normal during initialization)");
            }
        }

        return isValid;
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
            if (!ValidateGridLayoutSettings(inventoryGrid, "Inventory"))
                isValid = false;
        }

        if (hotbarGrid == null)
        {
            LogError("Hotbar Grid Layout is not assigned");
            isValid = false;
        }
        else
        {
            if (!ValidateGridLayoutSettings(hotbarGrid, "Hotbar"))
                isValid = false;
        }

        return isValid;
    }

    private bool ValidateGridLayoutSettings(GridLayoutGroup gridLayout, string gridType)
    {
        bool isValid = true;

        // Check for reasonable cell size
        if (gridLayout.cellSize.x <= 0 || gridLayout.cellSize.y <= 0)
        {
            LogWarning($"{gridType} Grid Layout has invalid cell size: {gridLayout.cellSize}");
            if (autoFixMinorIssues)
            {
                gridLayout.cellSize = new Vector2(80, 80); // Default size
                LogInfo($"Auto-fixed: Set default cell size for {gridType} grid");
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
        }

        // Check for negative spacing
        if (gridLayout.spacing.x < 0 || gridLayout.spacing.y < 0)
        {
            LogWarning($"{gridType} Grid Layout has negative spacing: {gridLayout.spacing}");
            if (autoFixMinorIssues)
            {
                gridLayout.spacing = new Vector2(Mathf.Max(0, gridLayout.spacing.x), Mathf.Max(0, gridLayout.spacing.y));
                LogInfo($"Auto-fixed: Corrected negative spacing for {gridType} grid");
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
        }

        // Check constraint settings
        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount && gridLayout.constraintCount <= 0)
        {
            LogWarning($"{gridType} Grid Layout has invalid constraint count: {gridLayout.constraintCount}");
            if (autoFixMinorIssues)
            {
                gridLayout.constraintCount = 5; // Default columns
                LogInfo($"Auto-fixed: Set default constraint count for {gridType} grid");
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
        }

        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount && gridLayout.constraintCount <= 0)
        {
            LogWarning($"{gridType} Grid Layout has invalid constraint count: {gridLayout.constraintCount}");
            if (autoFixMinorIssues)
            {
                gridLayout.constraintCount = 4; // Default rows
                LogInfo($"Auto-fixed: Set default constraint count for {gridType} grid");
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
        }

        return isValid;
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
            if (autoFixMinorIssues)
            {
                slot.AddComponent<Image>();
                LogInfo($"Auto-fixed: Added Image component to {slotName}");
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
        }
        else
        {
            // Validate Image settings
            if (!image.raycastTarget)
            {
                LogInfo($"Slot {slotName} Image component has raycastTarget disabled - this may affect interaction");
                if (autoFixMinorIssues)
                {
                    image.raycastTarget = true;
                    LogInfo($"Auto-fixed: Enabled raycastTarget for {slotName}");
                    autoFixedIssues++;
                }
            }
        }

        // Check for InventorySlot component
        var inventorySlot = slot.GetComponent<InventorySlot>();
        if (inventorySlot == null)
        {
            LogWarning($"Slot {slotName} is missing InventorySlot component");
            if (autoFixMinorIssues)
            {
                slot.AddComponent<InventorySlot>();
                LogInfo($"Auto-fixed: Added InventorySlot component to {slotName}");
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
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

        // Check slot activity - slots should be active even if their UI panel parents are not
        if (!slot.activeInHierarchy)
        {
            LogInfo($"Slot {slotName} is not active in hierarchy");
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
        string fullMessage = $"[SlotValidation ERROR] {message}";
        Debug.LogError(fullMessage);
        validationLog?.Add($"ERROR: {message}");
    }

    private void LogWarning(string message)
    {
        validationWarnings++;
        if (logValidationWarnings)
        {
            string fullMessage = $"[SlotValidation WARNING] {message}";
            Debug.LogWarning(fullMessage);
        }
        validationLog?.Add($"WARNING: {message}");
    }

    private void LogInfo(string message)
    {
        if (logValidationInfo)
        {
            string fullMessage = $"[SlotValidation INFO] {message}";
            Debug.Log(fullMessage);
        }
        validationLog?.Add($"INFO: {message}");
    }

    private void LogValidationSummary(bool isValid, string context)
    {
        if (logValidationInfo)
        {
            string status = isValid ? "PASSED" : "FAILED";
            Debug.Log($"[SlotValidation] {context} validation {status} - Errors: {validationErrors}, Warnings: {validationWarnings}");
        }
    }

    // Public Utility Methods
    public void SetValidationLevel(bool enableValidation, bool logWarnings = true, bool logInfo = false)
    {
        this.enableValidation = enableValidation;
        this.logValidationWarnings = logWarnings;
        this.logValidationInfo = logInfo;
    }

    public void SetAutoFixEnabled(bool enabled)
    {
        autoFixMinorIssues = enabled;
    }

    public void SetValidationInterval(float intervalSeconds)
    {
        validationInterval = Mathf.Max(1f, intervalSeconds);
    }

    public string GetValidationReport()
    {
        var report = new StringBuilder();
        report.AppendLine("📋 Slot Validation Report");
        report.AppendLine("========================");
        report.AppendLine($"Total Slots Validated: {totalSlotsValidated}");
        report.AppendLine($"Validation Errors: {validationErrors}");
        report.AppendLine($"Validation Warnings: {validationWarnings}");
        report.AppendLine($"Auto-Fixed Issues: {autoFixedIssues}");
        report.AppendLine($"Success Rate: {ValidationSuccessRate:P1}");

        if (validationLog != null && validationLog.Count > 0)
        {
            report.AppendLine("\n📝 Recent Validation Log:");
            int startIndex = Mathf.Max(0, validationLog.Count - 10);
            for (int i = startIndex; i < validationLog.Count; i++)
            {
                report.AppendLine($"  {validationLog[i]}");
            }
        }

        return report.ToString();
    }

    public void ClearValidationStats()
    {
        ResetValidationStats();
        validationLog?.Clear();
        LogInfo("Validation statistics cleared");
    }

    public void ClearValidationLog()
    {
        validationLog?.Clear();
        LogInfo("Validation log cleared");
    }

    // Advanced validation methods
    public bool PerformFullSystemValidation(SlotManager slotManager, SlotConfigurationManager config)
    {
        if (!enableValidation) return true;

        LogInfo("Performing full system validation");
        bool isValid = true;

        // Validate configuration
        if (!ValidateConfiguration(config))
            isValid = false;

        // Validate slot arrays
        if (slotManager.HotbarSlots != null)
        {
            for (int i = 0; i < slotManager.HotbarSlots.Length; i++)
            {
                if (!ValidateSlot(slotManager.HotbarSlots[i], $"Hotbar_{i}", true))
                    isValid = false;
            }
        }

        if (slotManager.InventorySlots != null)
        {
            for (int i = 0; i < slotManager.InventorySlots.Length; i++)
            {
                if (!ValidateSlot(slotManager.InventorySlots[i], $"Inventory_{i}", false))
                    isValid = false;
            }
        }

        LogValidationSummary(isValid, "Full System");
        return isValid;
    }
}