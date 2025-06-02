using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TraitSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject traitSelectionPanel;
    [SerializeField] private Transform availableTraitsParent;
    [SerializeField] private Transform activeTraitsParent;
    [SerializeField] private Button traitButtonPrefab;

    [Header("Trait Details Panel")]
    [SerializeField] private GameObject traitDetailsPanel;
    [SerializeField] private Image traitIcon;
    [SerializeField] private TextMeshProUGUI traitName;
    [SerializeField] private TextMeshProUGUI traitDescription;
    [SerializeField] private TextMeshProUGUI traitCost;
    [SerializeField] private TextMeshProUGUI traitRarity;
    [SerializeField] private Transform traitEffectsParent;
    [SerializeField] private TextMeshProUGUI traitEffectPrefab;
    [SerializeField] private Image traitTypeIcon;
    [SerializeField] private GameObject restrictionsPanel;
    [SerializeField] private TextMeshProUGUI restrictionsText;

    [Header("Filters and Search")]
    [SerializeField] private TMP_Dropdown typeFilterDropdown;
    [SerializeField] private TMP_Dropdown rarityFilterDropdown;
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private Toggle showPositiveToggle;
    [SerializeField] private Toggle showNegativeToggle;
    [SerializeField] private Toggle showAffordableOnlyToggle;
    [SerializeField] private Toggle showAvailableOnlyToggle;
    [SerializeField] private Toggle showClassExclusiveToggle;

    [Header("Points Display")]
    [SerializeField] private TextMeshProUGUI availablePointsText;
    [SerializeField] private TextMeshProUGUI totalCostText;
    [SerializeField] private Image pointsProgressBar;

    [Header("Class Information")]
    [SerializeField] private GameObject classInfoPanel;
    [SerializeField] private Image classIcon;
    [SerializeField] private TextMeshProUGUI className;
    [SerializeField] private TextMeshProUGUI classTraitBonusText;

    [Header("Control Buttons")]
    [SerializeField] private Button addTraitButton;
    [SerializeField] private Button removeTraitButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button previewBuildButton;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip traitSelectSound;
    [SerializeField] private AudioClip traitAddSound;
    [SerializeField] private AudioClip traitRemoveSound;
    [SerializeField] private AudioClip errorSound;

    [Header("Player Reference")]
    [SerializeField] private TraitManager traitManager;
    [SerializeField] private PlayerStatusController playerController;

    [Header("Scroll Views")]
    [SerializeField] private ScrollRect availableTraitsScroll;
    [SerializeField] private ScrollRect activeTraitsScroll;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private GameObject noTraitsMessage;
    [SerializeField] private Color positiveTraitColor = Color.green;
    [SerializeField] private Color negativeTraitColor = Color.red;
    [SerializeField] private Color neutralTraitColor = Color.white;
    [SerializeField] private Color exclusiveTraitColor = Color.gold;

    // State
    private List<Button> availableTraitButtons = new List<Button>();
    private List<Button> activeTraitButtons = new List<Button>();
    private List<TextMeshProUGUI> effectDisplays = new List<TextMeshProUGUI>();
    private Trait selectedTrait;
    private TraitType currentTypeFilter = TraitType.Combat;
    private TraitRarity currentRarityFilter = TraitRarity.Common;
    private List<Trait> originalActiveTraits = new List<Trait>();
    private Dictionary<TraitType, Sprite> traitTypeIcons = new Dictionary<TraitType, Sprite>();

    private void Start()
    {
        SetupUI();
        HideTraitSelection();
        HideTraitDetails();
    }

    private void SetupUI()
    {
        SetupButtons();
        SetupFilters();
        SetupAudio();
        UpdateButtonStates();
    }

    private void SetupButtons()
    {
        if (addTraitButton != null)
            addTraitButton.onClick.AddListener(AddSelectedTrait);

        if (removeTraitButton != null)
            removeTraitButton.onClick.AddListener(RemoveSelectedTrait);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmTraitSelection);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelTraitSelection);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetTraits);

        if (previewBuildButton != null)
            previewBuildButton.onClick.AddListener(PreviewBuild);
    }

    private void SetupFilters()
    {
        SetupTypeFilter();
        SetupRarityFilter();
        SetupSearchFilter();
        SetupToggles();
    }

    private void SetupTypeFilter()
    {
        if (typeFilterDropdown != null)
        {
            typeFilterDropdown.ClearOptions();
            var options = System.Enum.GetNames(typeof(TraitType)).ToList();
            options.Insert(0, "All Types");
            typeFilterDropdown.AddOptions(options);
            typeFilterDropdown.onValueChanged.AddListener(OnTypeFilterChanged);
        }
    }

    private void SetupRarityFilter()
    {
        if (rarityFilterDropdown != null)
        {
            rarityFilterDropdown.ClearOptions();
            var options = System.Enum.GetNames(typeof(TraitRarity)).ToList();
            options.Insert(0, "All Rarities");
            rarityFilterDropdown.AddOptions(options);
            rarityFilterDropdown.onValueChanged.AddListener(OnRarityFilterChanged);
        }
    }

    private void SetupSearchFilter()
    {
        if (searchField != null)
        {
            searchField.onValueChanged.AddListener(OnSearchChanged);
        }
    }

    private void SetupToggles()
    {
        if (showPositiveToggle != null)
        {
            showPositiveToggle.isOn = true;
            showPositiveToggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }

        if (showNegativeToggle != null)
        {
            showNegativeToggle.isOn = true;
            showNegativeToggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }

        if (showAffordableOnlyToggle != null)
        {
            showAffordableOnlyToggle.isOn = false;
            showAffordableOnlyToggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }

        if (showAvailableOnlyToggle != null)
        {
            showAvailableOnlyToggle.isOn = true;
            showAvailableOnlyToggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }

        if (showClassExclusiveToggle != null)
        {
            showClassExclusiveToggle.isOn = false;
            showClassExclusiveToggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }
    }

    private void SetupAudio()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void ShowTraitSelection()
    {
        if (traitManager == null)
        {
            Debug.LogError("TraitManager not assigned!");
            return;
        }

        // Store original traits for cancel functionality
        originalActiveTraits = new List<Trait>(traitManager.ActiveTraits);

        RefreshTraitLists();
        UpdatePointsDisplay();
        UpdateClassInfo();

        if (traitSelectionPanel != null)
            traitSelectionPanel.SetActive(true);
    }

    public void HideTraitSelection()
    {
        if (traitSelectionPanel != null)
            traitSelectionPanel.SetActive(false);

        HideTraitDetails();
    }

    private void HideTraitDetails()
    {
        if (traitDetailsPanel != null)
            traitDetailsPanel.SetActive(false);
    }

    private void RefreshTraitLists()
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);

        RefreshAvailableTraits();
        RefreshActiveTraits();

        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);
    }

    private void RefreshAvailableTraits()
    {
        ClearAvailableTraitButtons();

        if (traitButtonPrefab == null || availableTraitsParent == null || traitManager?.Database == null)
            return;

        var filteredTraits = GetFilteredAvailableTraits();

        if (filteredTraits.Count == 0)
        {
            if (noTraitsMessage != null)
                noTraitsMessage.SetActive(true);
            return;
        }

        if (noTraitsMessage != null)
            noTraitsMessage.SetActive(false);

        foreach (var trait in filteredTraits)
        {
            Button button = Instantiate(traitButtonPrefab, availableTraitsParent);
            SetupTraitButton(button, trait, true);
            availableTraitButtons.Add(button);
        }
    }

    private void RefreshActiveTraits()
    {
        ClearActiveTraitButtons();

        if (traitButtonPrefab == null || activeTraitsParent == null || traitManager == null)
            return;

        var activeTraits = traitManager.ActiveTraits;

        foreach (var trait in activeTraits)
        {
            Button button = Instantiate(traitButtonPrefab, activeTraitsParent);
            SetupTraitButton(button, trait, false);
            activeTraitButtons.Add(button);
        }
    }

    private void SetupTraitButton(Button button, Trait trait, bool isAvailable)
    {
        // Set button text with enhanced info
        var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            int actualCost = GetActualTraitCost(trait);
            string costText = actualCost > 0 ? $"({actualCost})" : actualCost < 0 ? $"(+{-actualCost})" : "";

            string rarityText = trait.rarity != TraitRarity.Common ? $"[{trait.rarity}]" : "";
            buttonText.text = $"{trait.Name} {costText} {rarityText}";
        }

        // Set button color based on trait properties
        SetTraitButtonColor(button, trait, isAvailable);

        // Set button click
        button.onClick.AddListener(() => SelectTrait(trait));

        // Add tooltip or additional info
        var tooltip = button.gameObject.AddComponent<TraitButtonTooltip>();
        tooltip?.Initialize(trait, GetActualTraitCost(trait));
    }

    private void SetTraitButtonColor(Button button, Trait trait, bool isAvailable)
    {
        var colors = button.colors;

        // Base color based on trait type
        if (IsClassExclusiveTrait(trait))
            colors.normalColor = exclusiveTraitColor;
        else if (trait.IsPositive)
            colors.normalColor = positiveTraitColor;
        else if (trait.IsNegative)
            colors.normalColor = negativeTraitColor;
        else
            colors.normalColor = neutralTraitColor;

        // Dim if not available/affordable
        if (isAvailable && !traitManager.CanAddTrait(trait))
        {
            colors.normalColor = Color.gray;
        }

        // Adjust alpha based on rarity
        float alpha = trait.rarity switch
        {
            TraitRarity.Common => 0.8f,
            TraitRarity.Uncommon => 0.85f,
            TraitRarity.Rare => 0.9f,
            TraitRarity.Epic => 0.95f,
            TraitRarity.Legendary => 1.0f,
            _ => 0.8f
        };

        var color = colors.normalColor;
        color.a = alpha;
        colors.normalColor = color;

        button.colors = colors;
    }

    private List<Trait> GetFilteredAvailableTraits()
    {
        if (traitManager?.Database == null) return new List<Trait>();

        var playerClass = playerController?.CurrentPlayerClass;
        if (playerClass == null) return new List<Trait>();

        // Start with class-appropriate traits
        var allTraits = traitManager.Database.GetTraitsForClass(playerClass as PlayerClass);
        var filtered = allTraits.Where(t => t != null && !traitManager.HasTrait(t));

        // Apply type filter
        if (typeFilterDropdown != null && typeFilterDropdown.value > 0)
        {
            var selectedType = (TraitType)(typeFilterDropdown.value - 1);
            filtered = filtered.Where(t => t.type == selectedType);
        }

        // Apply rarity filter
        if (rarityFilterDropdown != null && rarityFilterDropdown.value > 0)
        {
            var selectedRarity = (TraitRarity)(rarityFilterDropdown.value - 1);
            filtered = filtered.Where(t => t.rarity == selectedRarity);
        }

        // Apply search filter
        if (searchField != null && !string.IsNullOrEmpty(searchField.text))
        {
            string search = searchField.text.ToLower();
            filtered = filtered.Where(t =>
                t.Name.ToLower().Contains(search) ||
                t.description.ToLower().Contains(search) ||
                t.effects.Any(e => e.targetStat.ToLower().Contains(search))
            );
        }

        // Apply toggle filters
        if (showPositiveToggle != null && !showPositiveToggle.isOn)
            filtered = filtered.Where(t => !t.IsPositive);

        if (showNegativeToggle != null && !showNegativeToggle.isOn)
            filtered = filtered.Where(t => !t.IsNegative);

        if (showAffordableOnlyToggle != null && showAffordableOnlyToggle.isOn)
            filtered = filtered.Where(t => traitManager.CanAddTrait(t));

        if (showAvailableOnlyToggle != null && showAvailableOnlyToggle.isOn)
            filtered = filtered.Where(t => traitManager.CanAddTrait(t));

        if (showClassExclusiveToggle != null && showClassExclusiveToggle.isOn)
            filtered = filtered.Where(t => IsClassExclusiveTrait(t));

        return filtered.OrderBy(t => t.rarity)
                      .ThenBy(t => GetActualTraitCost(t))
                      .ThenBy(t => t.Name)
                      .ToList();
    }

    private bool IsClassExclusiveTrait(Trait trait)
    {
        var playerClass = playerController?.CurrentPlayerClass as PlayerClass;
        if (playerClass == null) return false;

        return playerClass.GetExclusiveTraits().Contains(trait);
    }

    private int GetActualTraitCost(Trait trait)
    {
        var playerClass = playerController?.CurrentPlayerClass as PlayerClass;
        return playerClass?.GetTraitCost(trait) ?? trait.cost;
    }

    private void SelectTrait(Trait trait)
    {
        selectedTrait = trait;
        UpdateTraitDetails(trait);
        UpdateButtonStates();
        PlaySound(traitSelectSound);

        if (traitDetailsPanel != null)
            traitDetailsPanel.SetActive(true);
    }

    private void UpdateTraitDetails(Trait trait)
    {
        if (trait == null) return;

        // Basic info
        if (traitIcon != null)
            traitIcon.sprite = trait.icon;

        if (traitName != null)
            traitName.text = trait.Name;

        if (traitDescription != null)
            traitDescription.text = trait.GetFormattedDescription();

        if (traitCost != null)
        {
            int actualCost = GetActualTraitCost(trait);
            string costText = actualCost > 0 ? $"Cost: {actualCost}" :
                             actualCost < 0 ? $"Gives: {-actualCost}" : "Free";

            // Show original cost if different
            if (actualCost != trait.cost)
            {
                costText += $" (Base: {trait.cost})";
            }

            traitCost.text = costText;
        }

        if (traitRarity != null)
        {
            traitRarity.text = $"Rarity: {trait.rarity}";
            traitRarity.color = GetRarityColor(trait.rarity);
        }

        // Type icon
        if (traitTypeIcon != null && traitTypeIcons.ContainsKey(trait.type))
            traitTypeIcon.sprite = traitTypeIcons[trait.type];

        // Restrictions
        UpdateRestrictionsDisplay(trait);

        // Effects
        UpdateEffectsDisplay(trait);
    }

    private void UpdateRestrictionsDisplay(Trait trait)
    {
        if (restrictionsPanel == null || restrictionsText == null) return;

        var restrictions = new List<string>();

        // Class restrictions
        if (trait.restrictions.allowedClasses.Count > 0)
        {
            restrictions.Add($"Classes: {string.Join(", ", trait.restrictions.allowedClasses.Select(c => c.GetClassName()))}");
        }

        // Level requirements
        if (trait.restrictions.minimumLevel > 1)
        {
            restrictions.Add($"Minimum Level: {trait.restrictions.minimumLevel}");
        }

        // Stat requirements
        foreach (var req in trait.restrictions.statRequirements)
        {
            restrictions.Add($"{req.statName} {GetComparisonSymbol(req.comparisonType)} {req.minimumValue}");
        }

        bool hasRestrictions = restrictions.Count > 0;
        restrictionsPanel.SetActive(hasRestrictions);

        if (hasRestrictions)
        {
            restrictionsText.text = "Requirements:\n" + string.Join("\n", restrictions);
        }
    }

    private string GetComparisonSymbol(ComparisonType type)
    {
        return type switch
        {
            ComparisonType.GreaterThan => ">",
            ComparisonType.GreaterThanOrEqual => "≥",
            ComparisonType.LessThan => "<",
            ComparisonType.LessThanOrEqual => "≤",
            ComparisonType.Equal => "=",
            _ => "?"
        };
    }

    private void UpdateEffectsDisplay(Trait trait)
    {
        ClearEffectsDisplay();

        if (traitEffectPrefab == null || traitEffectsParent == null) return;

        foreach (var effect in trait.effects)
        {
            var effectDisplay = Instantiate(traitEffectPrefab, traitEffectsParent);
            string effectText = GetEffectDescription(effect);
            effectDisplay.text = effectText;
            effectDisplays.Add(effectDisplay);
        }
    }

    private void ClearEffectsDisplay()
    {
        foreach (var display in effectDisplays)
        {
            if (display != null)
                DestroyImmediate(display.gameObject);
        }
        effectDisplays.Clear();
    }

    private string GetEffectDescription(TraitEffect effect)
    {
        if (!string.IsNullOrEmpty(effect.effectDescription))
            return effect.effectDescription;

        string typeText = effect.effectType switch
        {
            TraitEffectType.StatMultiplier => $"{effect.value * 100:F0}% {effect.targetStat}",
            TraitEffectType.StatAddition => $"{(effect.value > 0 ? "+" : "")}{effect.value:F1} {effect.targetStat}",
            TraitEffectType.RegenerationRate => $"{effect.value * 100:F0}% {effect.targetStat} regen",
            TraitEffectType.ConsumptionRate => $"{effect.value * 100:F0}% {effect.targetStat} consumption",
            TraitEffectType.ResistanceBonus => $"{effect.value:F1} {effect.targetStat} resistance",
            _ => $"{effect.effectType}: {effect.value:F1}"
        };

        return typeText;
    }

    private Color GetRarityColor(TraitRarity rarity)
    {
        return rarity switch
        {
            TraitRarity.Common => Color.white,
            TraitRarity.Uncommon => Color.green,
            TraitRarity.Rare => Color.blue,
            TraitRarity.Epic => Color.magenta,
            TraitRarity.Legendary => Color.yellow,
            _ => Color.white
        };
    }

    private void AddSelectedTrait()
    {
        if (selectedTrait == null || traitManager == null) return;

        if (traitManager.AddTrait(selectedTrait))
        {
            RefreshTraitLists();
            UpdatePointsDisplay();
            PlaySound(traitAddSound);

            // Clear selection if trait was added
            selectedTrait = null;
            HideTraitDetails();
        }
        else
        {
            Debug.Log($"Cannot add trait: {selectedTrait.Name}");
            PlaySound(errorSound);
        }

        UpdateButtonStates();
    }

    private void RemoveSelectedTrait()
    {
        if (selectedTrait == null || traitManager == null) return;

        if (traitManager.RemoveTrait(selectedTrait))
        {
            RefreshTraitLists();
            UpdatePointsDisplay();
            PlaySound(traitRemoveSound);

            // Clear selection if trait was removed
            selectedTrait = null;
            HideTraitDetails();
        }
        else
        {
            Debug.Log($"Cannot remove trait: {selectedTrait.Name}");
            PlaySound(errorSound);
        }

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = selectedTrait != null;
        bool canAdd = hasSelection && traitManager != null && traitManager.CanAddTrait(selectedTrait);
        bool canRemove = hasSelection && traitManager != null && traitManager.HasTrait(selectedTrait);

        if (addTraitButton != null)
            addTraitButton.interactable = canAdd;

        if (removeTraitButton != null)
            removeTraitButton.interactable = canRemove;
    }

    private void UpdatePointsDisplay()
    {
        if (traitManager == null) return;

        if (availablePointsText != null)
            availablePointsText.text = $"Available Points: {traitManager.AvailableTraitPoints}";

        if (totalCostText != null)
        {
            int totalCost = traitManager.GetTotalTraitCost();
            totalCostText.text = $"Total Cost: {totalCost}";
        }

        // Update progress bar
        if (pointsProgressBar != null)
        {
            var playerClass = playerController?.CurrentPlayerClass as PlayerClass;
            int maxPoints = playerClass?.traitPoints ?? 10;
            int usedPoints = traitManager.GetTotalTraitCost();
            pointsProgressBar.fillAmount = maxPoints > 0 ? (float)usedPoints / maxPoints : 0f;
        }
    }

    private void UpdateClassInfo()
    {
        var playerClass = playerController?.CurrentPlayerClass as PlayerClass;
        if (playerClass == null || classInfoPanel == null) return;

        classInfoPanel.SetActive(true);

        if (classIcon != null)
            classIcon.sprite = playerClass.classIcon;

        if (className != null)
            className.text = playerClass.GetClassName();

        if (classTraitBonusText != null)
        {
            var bonuses = new List<string>();

            if (playerClass.traitRestrictions.preferredTraitTypes.Count > 0)
            {
                bonuses.Add($"Preferred: {string.Join(", ", playerClass.traitRestrictions.preferredTraitTypes)} (-{(1f - playerClass.traitRestrictions.preferredCostMultiplier) * 100:F0}% cost)");
            }

            if (playerClass.traitRestrictions.difficultTraitTypes.Count > 0)
            {
                bonuses.Add($"Difficult: {string.Join(", ", playerClass.traitRestrictions.difficultTraitTypes)} (+{(playerClass.traitRestrictions.difficultCostMultiplier - 1f) * 100:F0}% cost)");
            }

            classTraitBonusText.text = bonuses.Count > 0 ? string.Join("\n", bonuses) : "No trait bonuses";
        }
    }

    private void PreviewBuild()
    {
        // Show a preview of the character build with all selected traits
        var previewText = "Current Build:\n\n";

        var activeTraits = traitManager.ActiveTraits;
        if (activeTraits.Count > 0)
        {
            foreach (var trait in activeTraits.OrderBy(t => t.type))
            {
                previewText += $"• {trait.Name} ({trait.type})\n";
            }
        }
        else
        {
            previewText += "No traits selected.";
        }

        Debug.Log(previewText);
        // Could show this in a popup or dedicated preview panel
    }

    private void ConfirmTraitSelection()
    {
        Debug.Log("Trait selection confirmed!");
        HideTraitSelection();
    }

    private void CancelTraitSelection()
    {
        // Restore original traits
        if (traitManager != null)
        {
            traitManager.ClearAllTraits();
            foreach (var trait in originalActiveTraits)
            {
                traitManager.AddTrait(trait, true);
            }
        }

        HideTraitSelection();
    }

    private void ResetTraits()
    {
        if (traitManager != null)
        {
            traitManager.ClearAllTraits();
            RefreshTraitLists();
            UpdatePointsDisplay();
        }
    }

    // Filter event handlers
    private void OnTypeFilterChanged(int index)
    {
        RefreshAvailableTraits();
    }

    private void OnRarityFilterChanged(int index)
    {
        RefreshAvailableTraits();
    }

    private void OnSearchChanged(string searchText)
    {
        RefreshAvailableTraits();
    }

    private void OnFilterToggleChanged(bool value)
    {
        RefreshAvailableTraits();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Cleanup methods
    private void ClearAvailableTraitButtons()
    {
        foreach (var button in availableTraitButtons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        availableTraitButtons.Clear();
    }

    private void ClearActiveTraitButtons()
    {
        foreach (var button in activeTraitButtons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        activeTraitButtons.Clear();
    }

    // Debug methods
    [ContextMenu("Show Trait Selection (Debug)")]
    private void DebugShowTraitSelection()
    {
        ShowTraitSelection();
    }

    [ContextMenu("Add Random Trait (Debug)")]
    private void DebugAddRandomTrait()
    {
        if (traitManager?.Database != null)
        {
            var availableTraits = traitManager.GetAvailableTraits();
            if (availableTraits.Count > 0)
            {
                var randomTrait = availableTraits[Random.Range(0, availableTraits.Count)];
                traitManager.AddTrait(randomTrait);
                RefreshTraitLists();
                UpdatePointsDisplay();
            }
        }
    }
}

// Helper component for trait button tooltips
public class TraitButtonTooltip : MonoBehaviour
{
    private Trait trait;
    private int cost;

    public void Initialize(Trait trait, int cost)
    {
        this.trait = trait;
        this.cost = cost;
    }

    // This could be expanded to show tooltips on hover
}