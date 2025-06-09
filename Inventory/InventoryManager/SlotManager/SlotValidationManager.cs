using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotValidationManager
{
    [Header("Validation Settings")]
    [SerializeField] private bool enableValidation = true;
    [SerializeField] private bool autoFixMinorIssues = true;

    // Validation statistics
    [SerializeField] private int totalSlotsValidated;
    [SerializeField] private int validationErrors;
    [SerializeField] private int validationWarnings;
    [SerializeField] private int autoFixedIssues;

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

        if (!ValidateSlotPrefab(slotPrefab)) isValid = false;
        if (!ValidateParentTransforms(hotbarSlotsParent, inventorySlotsParent)) isValid = false;
        if (!ValidateGridLayouts(inventoryGridLayout, hotbarGridLayout)) isValid = false;

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
        var requiredComponents = new System.Type[] { typeof(RectTransform), typeof(Image) };

        foreach (var componentType in requiredComponents)
        {
            if (slotPrefab.GetComponent(componentType) == null)
            {
                if (autoFixMinorIssues && componentType == typeof(Image))
                {
                    slotPrefab.AddComponent<Image>();
                    autoFixedIssues++;
                    isValid = true;
                }
                else
                {
                    isValid = false;
                }
            }
        }

        return ValidatePrefabStructure(slotPrefab) && isValid;
    }

    private bool ValidatePrefabStructure(GameObject prefab)
    {
        RectTransform rectTransform = prefab.GetComponent<RectTransform>();
        if (rectTransform != null && rectTransform.sizeDelta == Vector2.zero)
        {
            if (autoFixMinorIssues)
            {
                rectTransform.sizeDelta = new Vector2(80, 80);
                autoFixedIssues++;
            }
        }
        return true;
    }

    private bool ValidateParentTransforms(Transform hotbarParent, Transform inventoryParent)
    {
        bool isValid = true;

        if (hotbarParent == null)
        {
            LogError("Hotbar Slots Parent is not assigned");
            isValid = false;
        }

        if (inventoryParent == null)
        {
            LogError("Inventory Slots Parent is not assigned");
            isValid = false;
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
        else if (!ValidateGridLayoutSettings(inventoryGrid, "Inventory"))
        {
            isValid = false;
        }

        if (hotbarGrid == null)
        {
            LogError("Hotbar Grid Layout is not assigned");
            isValid = false;
        }
        else if (!ValidateGridLayoutSettings(hotbarGrid, "Hotbar"))
        {
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
            if (autoFixMinorIssues)
            {
                gridLayout.cellSize = new Vector2(80, 80);
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
            if (autoFixMinorIssues)
            {
                gridLayout.spacing = new Vector2(Mathf.Max(0, gridLayout.spacing.x), Mathf.Max(0, gridLayout.spacing.y));
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

        return ValidateSlotComponents(slot, slotName);
    }

    private bool ValidateSlotComponents(GameObject slot, string slotName)
    {
        bool isValid = true;

        if (slot.GetComponent<RectTransform>() == null)
        {
            LogError($"Slot {slotName} is missing RectTransform component");
            isValid = false;
        }

        var image = slot.GetComponent<Image>();
        if (image == null)
        {
            if (autoFixMinorIssues)
            {
                slot.AddComponent<Image>();
                autoFixedIssues++;
            }
            else
            {
                isValid = false;
            }
        }
        else if (autoFixMinorIssues && !image.raycastTarget)
        {
            image.raycastTarget = true;
            autoFixedIssues++;
        }

        var inventorySlot = slot.GetComponent<InventorySlot>();
        if (inventorySlot == null && autoFixMinorIssues)
        {
            slot.AddComponent<InventorySlot>();
            autoFixedIssues++;
        }

        return isValid;
    }

    // Layout Validation
    public bool ValidateLayoutData(SlotLayoutCalculator.LayoutData layoutData, int expectedSlotCount)
    {
        if (!enableValidation || layoutData == null) return true;

        bool isValid = true;

        if (layoutData.columns <= 0 || layoutData.rows <= 0)
        {
            LogError($"Invalid grid dimensions: {layoutData.columns}x{layoutData.rows}");
            isValid = false;
        }

        if (layoutData.cellSize.x <= 0 || layoutData.cellSize.y <= 0)
        {
            LogError($"Invalid cell size: {layoutData.cellSize}");
            isValid = false;
        }

        return isValid;
    }

    // Configuration Validation
    public bool ValidateConfiguration(SlotConfigurationManager config)
    {
        if (!enableValidation || config == null) return true;

        bool isValid = true;

        if (config.MinSlotSize <= 0)
        {
            LogError($"Invalid minimum slot size: {config.MinSlotSize}");
            isValid = false;
        }

        if (config.MaxSlotSize <= config.MinSlotSize)
        {
            LogError($"Maximum slot size must be greater than minimum");
            isValid = false;
        }

        return isValid;
    }

    // Logging Methods
    private void LogError(string message)
    {
        validationErrors++;
        Debug.LogError($"[SlotValidation] {message}");
    }

    // Public Utility Methods
    public void SetValidationLevel(bool enableValidation, bool autoFix = true)
    {
        this.enableValidation = enableValidation;
        this.autoFixMinorIssues = autoFix;
    }

    public string GetValidationReport()
    {
        return $"Slot Validation Report:\n" +
               $"Total Validated: {totalSlotsValidated}\n" +
               $"Errors: {validationErrors}\n" +
               $"Warnings: {validationWarnings}\n" +
               $"Auto-Fixed: {autoFixedIssues}\n" +
               $"Success Rate: {ValidationSuccessRate:P1}";
    }

    public void ClearValidationStats()
    {
        ResetValidationStats();
    }
}