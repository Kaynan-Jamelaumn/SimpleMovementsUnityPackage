//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;
//using UnityEngine.Events;

//[System.Serializable]
//public class CharacterCreatedEvent : UnityEvent<GameObject> { }

//public class CharacterCreationUI : MonoBehaviour
//{
//    [Header("UI Panel References")]
//    [SerializeField] private GameObject characterCreationPanel;
//    [SerializeField] private GameObject namePanel;
//    [SerializeField] private GameObject classPanel;
//    [SerializeField] private GameObject traitPanel;
//    [SerializeField] private GameObject summaryPanel;

//    [Header("Name Selection")]
//    [SerializeField] private TMP_InputField nameInputField;
//    [SerializeField] private Button nameNextButton;
//    [SerializeField] private TextMeshProUGUI nameErrorText;

//    [Header("Class Selection")]
//    [SerializeField] private Transform classButtonParent;
//    [SerializeField] private Button classButtonPrefab;
//    [SerializeField] private Button classNextButton;
//    [SerializeField] private Button classBackButton;

//    [Header("Class Preview")]
//    [SerializeField] private Image classPreviewIcon;
//    [SerializeField] private TextMeshProUGUI classPreviewName;
//    [SerializeField] private TextMeshProUGUI classPreviewDescription;
//    [SerializeField] private Transform classStatsParent;
//    [SerializeField] private TextMeshProUGUI classStatTextPrefab;
//    [SerializeField] private Transform classTraitsParent;
//    [SerializeField] private GameObject classTraitDisplayPrefab;

//    [Header("Trait Selection")]
//    [SerializeField] private Transform availableTraitsParent;
//    [SerializeField] private Transform selectedTraitsParent;
//    [SerializeField] private Button traitButtonPrefab;
//    [SerializeField] private Button addTraitButton;
//    [SerializeField] private Button removeTraitButton;
//    [SerializeField] private Button traitsNextButton;
//    [SerializeField] private Button traitsBackButton;
//    [SerializeField] private ScrollRect availableTraitsScroll;
//    [SerializeField] private ScrollRect selectedTraitsScroll;

//    [Header("Trait Preview")]
//    [SerializeField] private GameObject traitPreviewPanel;
//    [SerializeField] private Image traitPreviewIcon;
//    [SerializeField] private TextMeshProUGUI traitPreviewName;
//    [SerializeField] private TextMeshProUGUI traitPreviewDescription;
//    [SerializeField] private TextMeshProUGUI traitPreviewCost;
//    [SerializeField] private Transform traitEffectsParent;
//    [SerializeField] private TextMeshProUGUI traitEffectTextPrefab;

//    [Header("Trait Points")]
//    [SerializeField] private TextMeshProUGUI availablePointsText;
//    [SerializeField] private TextMeshProUGUI usedPointsText;
//    [SerializeField] private Image pointsProgressBar;

//    [Header("Character Summary")]
//    [SerializeField] private TextMeshProUGUI summaryNameText;
//    [SerializeField] private TextMeshProUGUI summaryClassText;
//    [SerializeField] private Transform summaryStatsParent;
//    [SerializeField] private Transform summaryTraitsParent;
//    [SerializeField] private TextMeshProUGUI summaryStatTextPrefab;
//    [SerializeField] private GameObject summaryTraitDisplayPrefab;
//    [SerializeField] private Button createCharacterButton;
//    [SerializeField] private Button summaryBackButton;

//    [Header("Available Classes")]
//    [SerializeField] public List<PlayerClass> availableClasses = new List<PlayerClass>();

//    [Header("Player Prefab")]
//    [SerializeField] public GameObject playerPrefab;
//    [SerializeField] private Transform spawnPoint;

//    [Header("Trait Database")]
//    [SerializeField] private TraitDatabase traitDatabase;

//    [Header("Audio")]
//    [SerializeField] private AudioSource audioSource;
//    [SerializeField] private AudioClip buttonClickSound;
//    [SerializeField] private AudioClip errorSound;
//    [SerializeField] private AudioClip successSound;

//    [Header("Events")]
//    public CharacterCreatedEvent OnCharacterCreated;

//    // Current character data
//    private string characterName = "";
//    private PlayerClass selectedClass;
//    private List<Trait> selectedTraits = new List<Trait>();
//    private int availableTraitPoints = 0;
//    private int maxTraitPoints = 0;

//    // UI State
//    private enum CreationStep { Name, Class, Traits, Summary }
//    private CreationStep currentStep = CreationStep.Name;

//    // UI Collections
//    private List<Button> classButtons = new List<Button>();
//    private List<Button> availableTraitButtons = new List<Button>();
//    private List<Button> selectedTraitButtons = new List<Button>();
//    private List<TextMeshProUGUI> classStatDisplays = new List<TextMeshProUGUI>();
//    private List<GameObject> classTraitDisplays = new List<GameObject>();
//    private List<TextMeshProUGUI> traitEffectDisplays = new List<TextMeshProUGUI>();

//    // Selected UI elements
//    private Trait currentSelectedTrait = null;
//    private Button currentSelectedClassButton = null;
//    private Button currentSelectedAvailableTraitButton = null;
//    private Button currentSelectedSelectedTraitButton = null;

//    private void Start()
//    {
//        InitializeUI();
//        ShowNamePanel();
//    }

//    private void InitializeUI()
//    {
//        // Setup button listeners
//        if (nameNextButton != null)
//            nameNextButton.onClick.AddListener(OnNameNext);

//        if (classNextButton != null)
//            classNextButton.onClick.AddListener(OnClassNext);
//        if (classBackButton != null)
//            classBackButton.onClick.AddListener(OnClassBack);

//        if (addTraitButton != null)
//            addTraitButton.onClick.AddListener(OnAddTrait);
//        if (removeTraitButton != null)
//            removeTraitButton.onClick.AddListener(OnRemoveTrait);
//        if (traitsNextButton != null)
//            traitsNextButton.onClick.AddListener(OnTraitsNext);
//        if (traitsBackButton != null)
//            traitsBackButton.onClick.AddListener(OnTraitsBack);

//        if (createCharacterButton != null)
//            createCharacterButton.onClick.AddListener(OnCreateCharacter);
//        if (summaryBackButton != null)
//            summaryBackButton.onClick.AddListener(OnSummaryBack);

//        // Setup input field listener
//        if (nameInputField != null)
//            nameInputField.onValueChanged.AddListener(OnNameChanged);

//        // Get trait database if not assigned
//        if (traitDatabase == null)
//            traitDatabase = TraitDatabase.Instance;

//        // Hide all panels initially
//        HideAllPanels();

//        // Initialize UI state
//        UpdateAddRemoveButtons();
//        HideTraitPreview();
//    }

//    #region Navigation Methods

//    private void ShowNamePanel()
//    {
//        currentStep = CreationStep.Name;
//        HideAllPanels();
//        namePanel?.SetActive(true);
//        characterCreationPanel?.SetActive(true);

//        // Focus on name input
//        if (nameInputField != null)
//        {
//            nameInputField.text = characterName;
//            nameInputField.Select();
//        }

//        UpdateNameUI();
//    }

//    private void ShowClassPanel()
//    {
//        currentStep = CreationStep.Class;
//        HideAllPanels();
//        classPanel?.SetActive(true);
//        characterCreationPanel?.SetActive(true);

//        CreateClassButtons();
//        UpdateClassUI();
//    }

//    private void ShowTraitPanel()
//    {
//        currentStep = CreationStep.Traits;
//        HideAllPanels();
//        traitPanel?.SetActive(true);
//        characterCreationPanel?.SetActive(true);

//        InitializeTraitSelection();
//        CreateTraitButtons();
//        UpdateTraitUI();
//    }

//    private void ShowSummaryPanel()
//    {
//        currentStep = CreationStep.Summary;
//        HideAllPanels();
//        summaryPanel?.SetActive(true);
//        characterCreationPanel?.SetActive(true);

//        UpdateSummaryDisplay();
//    }

//    private void HideAllPanels()
//    {
//        namePanel?.SetActive(false);
//        classPanel?.SetActive(false);
//        traitPanel?.SetActive(false);
//        summaryPanel?.SetActive(false);
//    }

//    public void HideCharacterCreation()
//    {
//        characterCreationPanel?.SetActive(false);
//    }

//    public void ShowCharacterCreation()
//    {
//        characterCreationPanel?.SetActive(true);
//        ShowNamePanel();
//    }

//    #endregion

//    #region Name Selection

//    private void OnNameChanged(string newName)
//    {
//        characterName = newName.Trim();
//        UpdateNameUI();
//    }

//    private void UpdateNameUI()
//    {
//        bool isValidName = !string.IsNullOrEmpty(characterName) && characterName.Length >= 2;

//        if (nameNextButton != null)
//            nameNextButton.interactable = isValidName;

//        if (nameErrorText != null)
//        {
//            if (string.IsNullOrEmpty(characterName))
//            {
//                nameErrorText.text = "";
//            }
//            else if (characterName.Length < 2)
//            {
//                nameErrorText.text = "Name must be at least 2 characters";
//            }
//            else
//            {
//                nameErrorText.text = "";
//            }
//        }
//    }

//    private void OnNameNext()
//    {
//        if (string.IsNullOrEmpty(characterName) || characterName.Length < 2)
//        {
//            PlaySound(errorSound);
//            return;
//        }

//        PlaySound(buttonClickSound);
//        ShowClassPanel();
//    }

//    #endregion

//    #region Class Selection

//    private void CreateClassButtons()
//    {
//        ClearClassButtons();

//        if (classButtonPrefab == null || classButtonParent == null) return;

//        foreach (var playerClass in availableClasses)
//        {
//            if (playerClass == null) continue;

//            Button button = Instantiate(classButtonPrefab, classButtonParent);
//            SetupClassButton(button, playerClass);
//            classButtons.Add(button);
//        }
//    }

//    private void SetupClassButton(Button button, PlayerClass playerClass)
//    {
//        // Set button text
//        var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
//        if (buttonText != null)
//            buttonText.text = playerClass.GetClassName();

//        // Set button icon if available
//        var buttonImage = button.GetComponent<Image>();
//        if (buttonImage != null && playerClass.classIcon != null)
//            buttonImage.sprite = playerClass.classIcon;

//        // Set button click
//        button.onClick.AddListener(() => SelectClass(playerClass, button));

//        // Set initial color
//        var colors = button.colors;
//        colors.normalColor = playerClass.classColor != Color.white ? playerClass.classColor : Color.white;
//        button.colors = colors;
//    }

//    private void SelectClass(PlayerClass playerClass, Button button)
//    {
//        selectedClass = playerClass;
//        currentSelectedClassButton = button;

//        // Update button visuals
//        foreach (var btn in classButtons)
//        {
//            var colors = btn.colors;
//            colors.normalColor = Color.white;
//            btn.colors = colors;
//        }

//        // Highlight selected button
//        var selectedColors = button.colors;
//        selectedColors.normalColor = Color.green;
//        button.colors = selectedColors;

//        // Update class preview
//        UpdateClassPreview(playerClass);
//        UpdateClassUI();

//        PlaySound(buttonClickSound);
//    }

//    private void UpdateClassPreview(PlayerClass playerClass)
//    {
//        if (playerClass == null) return;

//        // Basic info
//        if (classPreviewIcon != null)
//            classPreviewIcon.sprite = playerClass.classIcon;

//        if (classPreviewName != null)
//            classPreviewName.text = playerClass.GetClassName();

//        if (classPreviewDescription != null)
//            classPreviewDescription.text = playerClass.GetFormattedDescription();

//        // Stats display
//        UpdateClassStatsDisplay(playerClass);

//        // Starting traits display
//        UpdateClassTraitsDisplay(playerClass);
//    }

//    private void UpdateClassStatsDisplay(PlayerClass playerClass)
//    {
//        ClearClassStatsDisplay();

//        if (classStatTextPrefab == null || classStatsParent == null) return;

//        var stats = new Dictionary<string, float>
//        {
//            {"Health", playerClass.health},
//            {"Stamina", playerClass.stamina},
//            {"Mana", playerClass.mana},
//            {"Speed", playerClass.speed},
//            {"Strength", playerClass.strength},
//            {"Agility", playerClass.agility},
//            {"Intelligence", playerClass.intelligence},
//            {"Endurance", playerClass.endurance}
//        };

//        foreach (var stat in stats)
//        {
//            var statDisplay = Instantiate(classStatTextPrefab, classStatsParent);
//            statDisplay.text = $"{stat.Key}: {stat.Value:F1}";
//            classStatDisplays.Add(statDisplay);
//        }
//    }

//    private void UpdateClassTraitsDisplay(PlayerClass playerClass)
//    {
//        ClearClassTraitsDisplay();

//        if (classTraitDisplayPrefab == null || classTraitsParent == null) return;

//        foreach (var trait in playerClass.startingTraits)
//        {
//            if (trait == null) continue;

//            var traitDisplay = Instantiate(classTraitDisplayPrefab, classTraitsParent);

//            var traitText = traitDisplay.GetComponentInChildren<TextMeshProUGUI>();
//            if (traitText != null)
//                traitText.text = trait.Name;

//            var traitIcon = traitDisplay.GetComponentInChildren<Image>();
//            if (traitIcon != null && trait.icon != null)
//                traitIcon.sprite = trait.icon;

//            classTraitDisplays.Add(traitDisplay);
//        }
//    }

//    private void UpdateClassUI()
//    {
//        bool hasClassSelected = selectedClass != null;

//        if (classNextButton != null)
//            classNextButton.interactable = hasClassSelected;
//    }

//    private void OnClassNext()
//    {
//        if (selectedClass == null)
//        {
//            PlaySound(errorSound);
//            return;
//        }

//        PlaySound(buttonClickSound);
//        ShowTraitPanel();
//    }

//    private void OnClassBack()
//    {
//        PlaySound(buttonClickSound);
//        ShowNamePanel();
//    }

//    private void ClearClassButtons()
//    {
//        foreach (var button in classButtons)
//        {
//            if (button != null)
//                DestroyImmediate(button.gameObject);
//        }
//        classButtons.Clear();
//    }

//    private void ClearClassStatsDisplay()
//    {
//        foreach (var display in classStatDisplays)
//        {
//            if (display != null)
//                DestroyImmediate(display.gameObject);
//        }
//        classStatDisplays.Clear();
//    }

//    private void ClearClassTraitsDisplay()
//    {
//        foreach (var display in classTraitDisplays)
//        {
//            if (display != null)
//                DestroyImmediate(display);
//        }
//        classTraitDisplays.Clear();
//    }

//    #endregion

//    #region Trait Selection

//    private void InitializeTraitSelection()
//    {
//        if (selectedClass == null) return;

//        // Initialize trait points
//        maxTraitPoints = selectedClass.traitPoints;
//        availableTraitPoints = maxTraitPoints;

//        // Clear previous selections
//        selectedTraits.Clear();

//        // Add starting traits automatically
//        foreach (var trait in selectedClass.startingTraits)
//        {
//            if (trait != null && !selectedTraits.Contains(trait))
//            {
//                selectedTraits.Add(trait);
//                // Starting traits don't cost points
//            }
//        }
//    }

//    private void CreateTraitButtons()
//    {
//        CreateAvailableTraitButtons();
//        CreateSelectedTraitButtons();
//    }

//    private void CreateAvailableTraitButtons()
//    {
//        ClearAvailableTraitButtons();

//        if (traitButtonPrefab == null || availableTraitsParent == null || traitDatabase == null) return;

//        var availableTraits = GetAvailableTraitsForClass();

//        foreach (var trait in availableTraits)
//        {
//            if (trait == null || selectedTraits.Contains(trait)) continue;

//            Button button = Instantiate(traitButtonPrefab, availableTraitsParent);
//            SetupTraitButton(button, trait, true);
//            availableTraitButtons.Add(button);
//        }
//    }

//    private void CreateSelectedTraitButtons()
//    {
//        ClearSelectedTraitButtons();

//        if (traitButtonPrefab == null || selectedTraitsParent == null) return;

//        foreach (var trait in selectedTraits)
//        {
//            if (trait == null) continue;

//            Button button = Instantiate(traitButtonPrefab, selectedTraitsParent);
//            SetupTraitButton(button, trait, false);
//            selectedTraitButtons.Add(button);
//        }
//    }

//    private void SetupTraitButton(Button button, Trait trait, bool isAvailable)
//    {
//        // Set button text
//        var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
//        if (buttonText != null)
//        {
//            int cost = GetTraitCost(trait);
//            bool isStartingTrait = selectedClass.startingTraits.Contains(trait);

//            string costText = isStartingTrait ? "(Starting)" :
//                             cost > 0 ? $"({cost})" :
//                             cost < 0 ? $"(+{-cost})" : "(Free)";

//            buttonText.text = $"{trait.Name}\n{costText}";
//        }

//        // Set button color
//        SetTraitButtonColor(button, trait, isAvailable);

//        // Set button click
//        if (isAvailable)
//        {
//            button.onClick.AddListener(() => SelectAvailableTrait(trait, button));
//        }
//        else
//        {
//            button.onClick.AddListener(() => SelectSelectedTrait(trait, button));
//        }
//    }

//    private void SetTraitButtonColor(Button button, Trait trait, bool isAvailable)
//    {
//        var colors = button.colors;

//        if (trait.IsPositive)
//            colors.normalColor = Color.green;
//        else if (trait.IsNegative)
//            colors.normalColor = Color.red;
//        else
//            colors.normalColor = Color.white;

//        // Dim if can't afford
//        if (isAvailable && !CanAffordTrait(trait))
//        {
//            colors.normalColor = Color.gray;
//        }

//        // Mark starting traits
//        if (selectedClass.startingTraits.Contains(trait))
//        {
//            colors.normalColor = Color.yellow;
//        }

//        button.colors = colors;
//    }

//    private List<Trait> GetAvailableTraitsForClass()
//    {
//        if (selectedClass == null || traitDatabase == null) return new List<Trait>();

//        return traitDatabase.GetTraitsForClass(selectedClass)
//            .OrderBy(t => GetTraitCost(t))
//            .ThenBy(t => t.Name)
//            .ToList();
//    }

//    private int GetTraitCost(Trait trait)
//    {
//        if (selectedClass == null) return trait.cost;
//        return selectedClass.GetTraitCost(trait);
//    }

//    private bool CanAffordTrait(Trait trait)
//    {
//        int cost = GetTraitCost(trait);
//        return cost <= availableTraitPoints;
//    }

//    private void SelectAvailableTrait(Trait trait, Button button)
//    {
//        currentSelectedTrait = trait;
//        currentSelectedAvailableTraitButton = button;
//        currentSelectedSelectedTraitButton = null;

//        // Update button visuals
//        UpdateAvailableTraitButtonVisuals(button);

//        // Show trait preview
//        ShowTraitPreview(trait);
//        UpdateAddRemoveButtons();

//        PlaySound(buttonClickSound);
//    }

//    private void SelectSelectedTrait(Trait trait, Button button)
//    {
//        currentSelectedTrait = trait;
//        currentSelectedSelectedTraitButton = button;
//        currentSelectedAvailableTraitButton = null;

//        // Update button visuals
//        UpdateSelectedTraitButtonVisuals(button);

//        // Show trait preview
//        ShowTraitPreview(trait);
//        UpdateAddRemoveButtons();

//        PlaySound(buttonClickSound);
//    }

//    private void UpdateAvailableTraitButtonVisuals(Button selectedButton)
//    {
//        foreach (var btn in availableTraitButtons)
//        {
//            var colors = btn.colors;
//            colors.selectedColor = Color.white;
//            btn.colors = colors;
//        }

//        var selectedColors = selectedButton.colors;
//        selectedColors.selectedColor = Color.cyan;
//        selectedButton.colors = selectedColors;
//    }

//    private void UpdateSelectedTraitButtonVisuals(Button selectedButton)
//    {
//        foreach (var btn in selectedTraitButtons)
//        {
//            var colors = btn.colors;
//            colors.selectedColor = Color.white;
//            btn.colors = colors;
//        }

//        var selectedColors = selectedButton.colors;
//        selectedColors.selectedColor = Color.cyan;
//        selectedButton.colors = selectedColors;
//    }

//    private void ShowTraitPreview(Trait trait)
//    {
//        if (trait == null || traitPreviewPanel == null) return;

//        traitPreviewPanel.SetActive(true);

//        // Basic info
//        if (traitPreviewIcon != null)
//            traitPreviewIcon.sprite = trait.icon;

//        if (traitPreviewName != null)
//            traitPreviewName.text = trait.Name;

//        if (traitPreviewDescription != null)
//            traitPreviewDescription.text = trait.GetFormattedDescription();

//        if (traitPreviewCost != null)
//        {
//            int cost = GetTraitCost(trait);
//            bool isStartingTrait = selectedClass.startingTraits.Contains(trait);

//            if (isStartingTrait)
//                traitPreviewCost.text = "Starting Trait";
//            else if (cost > 0)
//                traitPreviewCost.text = $"Cost: {cost} points";
//            else if (cost < 0)
//                traitPreviewCost.text = $"Gives: {-cost} points";
//            else
//                traitPreviewCost.text = "Free";
//        }

//        // Effects
//        UpdateTraitEffectsDisplay(trait);
//    }

//    private void HideTraitPreview()
//    {
//        traitPreviewPanel?.SetActive(false);
//    }

//    private void UpdateTraitEffectsDisplay(Trait trait)
//    {
//        ClearTraitEffectsDisplay();

//        if (traitEffectTextPrefab == null || traitEffectsParent == null) return;

//        foreach (var effect in trait.effects)
//        {
//            var effectDisplay = Instantiate(traitEffectTextPrefab, traitEffectsParent);
//            effectDisplay.text = GetEffectDescription(effect);
//            traitEffectDisplays.Add(effectDisplay);
//        }
//    }

//    private void ClearTraitEffectsDisplay()
//    {
//        foreach (var display in traitEffectDisplays)
//        {
//            if (display != null)
//                DestroyImmediate(display.gameObject);
//        }
//        traitEffectDisplays.Clear();
//    }

//    private string GetEffectDescription(TraitEffect effect)
//    {
//        if (!string.IsNullOrEmpty(effect.effectDescription))
//            return effect.effectDescription;

//        return effect.effectType switch
//        {
//            TraitEffectType.StatMultiplier => $"{effect.value * 100:F0}% {effect.targetStat}",
//            TraitEffectType.StatAddition => $"{(effect.value > 0 ? "+" : "")}{effect.value:F1} {effect.targetStat}",
//            TraitEffectType.RegenerationRate => $"{effect.value * 100:F0}% {effect.targetStat} regen",
//            TraitEffectType.ConsumptionRate => $"{effect.value * 100:F0}% {effect.targetStat} consumption",
//            TraitEffectType.ResistanceBonus => $"{effect.value:F1} {effect.targetStat} resistance",
//            _ => $"{effect.effectType}: {effect.value:F1}"
//        };
//    }

//    private void UpdateAddRemoveButtons()
//    {
//        bool canAdd = currentSelectedTrait != null &&
//                     currentSelectedAvailableTraitButton != null &&
//                     !selectedTraits.Contains(currentSelectedTrait) &&
//                     CanAffordTrait(currentSelectedTrait);

//        bool canRemove = currentSelectedTrait != null &&
//                        currentSelectedSelectedTraitButton != null &&
//                        selectedTraits.Contains(currentSelectedTrait) &&
//                        !selectedClass.startingTraits.Contains(currentSelectedTrait); // Can't remove starting traits

//        if (addTraitButton != null)
//            addTraitButton.interactable = canAdd;

//        if (removeTraitButton != null)
//            removeTraitButton.interactable = canRemove;
//    }

//    private void OnAddTrait()
//    {
//        if (currentSelectedTrait == null || !CanAffordTrait(currentSelectedTrait))
//        {
//            PlaySound(errorSound);
//            return;
//        }

//        // Add trait
//        selectedTraits.Add(currentSelectedTrait);

//        // Spend points
//        int cost = GetTraitCost(currentSelectedTrait);
//        availableTraitPoints -= cost;

//        // Refresh UI
//        CreateTraitButtons();
//        UpdateTraitUI();
//        HideTraitPreview();

//        // Clear selection
//        currentSelectedTrait = null;
//        currentSelectedAvailableTraitButton = null;
//        UpdateAddRemoveButtons();

//        PlaySound(buttonClickSound);
//    }

//    private void OnRemoveTrait()
//    {
//        if (currentSelectedTrait == null ||
//            !selectedTraits.Contains(currentSelectedTrait) ||
//            selectedClass.startingTraits.Contains(currentSelectedTrait))
//        {
//            PlaySound(errorSound);
//            return;
//        }

//        // Remove trait
//        selectedTraits.Remove(currentSelectedTrait);

//        // Refund points
//        int cost = GetTraitCost(currentSelectedTrait);
//        availableTraitPoints += cost;

//        // Refresh UI
//        CreateTraitButtons();
//        UpdateTraitUI();
//        HideTraitPreview();

//        // Clear selection
//        currentSelectedTrait = null;
//        currentSelectedSelectedTraitButton = null;
//        UpdateAddRemoveButtons();

//        PlaySound(buttonClickSound);
//    }

//    private void UpdateTraitUI()
//    {
//        // Update points display
//        if (availablePointsText != null)
//            availablePointsText.text = $"Available: {availableTraitPoints}";

//        if (usedPointsText != null)
//        {
//            int usedPoints = maxTraitPoints - availableTraitPoints;
//            usedPointsText.text = $"Used: {usedPoints} / {maxTraitPoints}";
//        }

//        if (pointsProgressBar != null)
//        {
//            float progress = maxTraitPoints > 0 ? (float)(maxTraitPoints - availableTraitPoints) / maxTraitPoints : 0f;
//            pointsProgressBar.fillAmount = progress;
//        }

//        // Update next button
//        if (traitsNextButton != null)
//            traitsNextButton.interactable = true; // Can always proceed (even with 0 traits)
//    }

//    private void OnTraitsNext()
//    {
//        PlaySound(buttonClickSound);
//        ShowSummaryPanel();
//    }

//    private void OnTraitsBack()
//    {
//        PlaySound(buttonClickSound);
//        ShowClassPanel();
//    }

//    private void ClearAvailableTraitButtons()
//    {
//        foreach (var button in availableTraitButtons)
//        {
//            if (button != null)
//                DestroyImmediate(button.gameObject);
//        }
//        availableTraitButtons.Clear();
//    }

//    private void ClearSelectedTraitButtons()
//    {
//        foreach (var button in selectedTraitButtons)
//        {
//            if (button != null)
//                DestroyImmediate(button.gameObject);
//        }
//        selectedTraitButtons.Clear();
//    }

//    #endregion

//    #region Summary

//    private void UpdateSummaryDisplay()
//    {
//        // Character name and class
//        if (summaryNameText != null)
//            summaryNameText.text = $"Name: {characterName}";

//        if (summaryClassText != null)
//            summaryClassText.text = $"Class: {selectedClass?.GetClassName() ?? "None"}";

//        // Final stats (class + trait modifications)
//        UpdateSummaryStats();

//        // Selected traits
//        UpdateSummaryTraits();
//    }

//    private void UpdateSummaryStats()
//    {
//        ClearSummaryStats();

//        if (selectedClass == null || summaryStatTextPrefab == null || summaryStatsParent == null) return;

//        // Calculate final stats (this is a preview, actual application happens in character creation)
//        var finalStats = CalculateFinalStats();

//        foreach (var stat in finalStats)
//        {
//            var statDisplay = Instantiate(summaryStatTextPrefab, summaryStatsParent);
//            statDisplay.text = $"{stat.Key}: {stat.Value:F1}";
//        }
//    }

//    private Dictionary<string, float> CalculateFinalStats()
//    {
//        var stats = new Dictionary<string, float>
//        {
//            {"Health", selectedClass.health},
//            {"Stamina", selectedClass.stamina},
//            {"Mana", selectedClass.mana},
//            {"Speed", selectedClass.speed},
//            {"Strength", selectedClass.strength},
//            {"Agility", selectedClass.agility},
//            {"Intelligence", selectedClass.intelligence},
//            {"Endurance", selectedClass.endurance}
//        };

//        // Apply trait modifications (simplified preview)
//        foreach (var trait in selectedTraits)
//        {
//            foreach (var effect in trait.effects)
//            {
//                if (stats.ContainsKey(effect.targetStat))
//                {
//                    switch (effect.effectType)
//                    {
//                        case TraitEffectType.StatAddition:
//                            stats[effect.targetStat] += effect.value;
//                            break;
//                        case TraitEffectType.StatMultiplier:
//                            stats[effect.targetStat] *= effect.value;
//                            break;
//                    }
//                }
//            }
//        }

//        return stats;
//    }

//    private void UpdateSummaryTraits()
//    {
//        ClearSummaryTraits();

//        if (summaryTraitDisplayPrefab == null || summaryTraitsParent == null) return;

//        foreach (var trait in selectedTraits)
//        {
//            if (trait == null) continue;

//            var traitDisplay = Instantiate(summaryTraitDisplayPrefab, summaryTraitsParent);

//            var traitText = traitDisplay.GetComponentInChildren<TextMeshProUGUI>();
//            if (traitText != null)
//            {
//                bool isStartingTrait = selectedClass.startingTraits.Contains(trait);
//                string prefix = isStartingTrait ? "[Starting] " : "";
//                traitText.text = $"{prefix}{trait.Name}";
//            }

//            var traitIcon = traitDisplay.GetComponentInChildren<Image>();
//            if (traitIcon != null && trait.icon != null)
//                traitIcon.sprite = trait.icon;
//        }
//    }

//    private void OnSummaryBack()
//    {
//        PlaySound(buttonClickSound);
//        ShowTraitPanel();
//    }

//    private void ClearSummaryStats()
//    {
//        foreach (Transform child in summaryStatsParent)
//        {
//            DestroyImmediate(child.gameObject);
//        }
//    }

//    private void ClearSummaryTraits()
//    {
//        foreach (Transform child in summaryTraitsParent)
//        {
//            DestroyImmediate(child.gameObject);
//        }
//    }

//    #endregion

//    #region Character Creation

//    private void OnCreateCharacter()
//    {
//        if (string.IsNullOrEmpty(characterName) || selectedClass == null)
//        {
//            PlaySound(errorSound);
//            Debug.LogError("Cannot create character: missing name or class");
//            return;
//        }

//        // Instantiate player
//        GameObject player = CreatePlayer();

//        if (player != null)
//        {
//            // Configure the player
//            ConfigurePlayer(player);

//            // Fire event
//            OnCharacterCreated?.Invoke(player);

//            // Play success sound
//            PlaySound(successSound);

//            // Hide UI
//            HideCharacterCreation();

//            Debug.Log($"Character '{characterName}' created successfully!");
//        }
//        else
//        {
//            PlaySound(errorSound);
//            Debug.LogError("Failed to create player GameObject");
//        }
//    }

//    private GameObject CreatePlayer()
//    {
//        if (playerPrefab == null)
//        {
//            Debug.LogError("Player prefab not assigned!");
//            return null;
//        }

//        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
//        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

//        return Instantiate(playerPrefab, spawnPosition, spawnRotation);
//    }

//    private void ConfigurePlayer(GameObject player)
//    {
//        // Get PlayerStatusController
//        var statusController = player.GetComponent<PlayerStatusController>();
//        if (statusController == null)
//        {
//            Debug.LogError("Player prefab doesn't have PlayerStatusController!");
//            return;
//        }

//        // Set player name (you might need to add a name component)
//        player.name = $"Player_{characterName}";

//        // Set player class (this will automatically apply class stats and starting traits)
//        statusController.SetPlayerClass(selectedClass);

//        // Apply additional selected traits (non-starting traits)
//        var traitManager = statusController.TraitManager;
//        if (traitManager != null)
//        {
//            // Set available trait points
//            traitManager.SetTraitPoints(maxTraitPoints);

//            // Add selected traits
//            foreach (var trait in selectedTraits)
//            {
//                if (!selectedClass.startingTraits.Contains(trait))
//                {
//                    traitManager.AddTrait(trait, false); // Don't skip cost for non-starting traits
//                }
//            }
//        }

//        Debug.Log($"Configured player with class: {selectedClass.GetClassName()}, traits: {selectedTraits.Count}");
//    }

//    #endregion

//    #region Utility Methods

//    private void PlaySound(AudioClip clip)
//    {
//        if (audioSource != null && clip != null)
//        {
//            audioSource.PlayOneShot(clip);
//        }
//    }

//    public void ResetCharacterCreation()
//    {
//        characterName = "";
//        selectedClass = null;
//        selectedTraits.Clear();
//        availableTraitPoints = 0;
//        maxTraitPoints = 0;
//        currentSelectedTrait = null;
//        currentSelectedClassButton = null;
//        currentSelectedAvailableTraitButton = null;
//        currentSelectedSelectedTraitButton = null;

//        ShowNamePanel();
//    }

//    // Public methods for external control
//    public void SetAvailableClasses(List<PlayerClass> classes)
//    {
//        availableClasses = new List<PlayerClass>(classes);
//    }

//    public void SetTraitDatabase(TraitDatabase database)
//    {
//        traitDatabase = database;
//    }

//    public void SetPlayerPrefab(GameObject prefab)
//    {
//        playerPrefab = prefab;
//    }

//    public void SetSpawnPoint(Transform spawn)
//    {
//        spawnPoint = spawn;
//    }

//    #endregion

//    #region Debug Methods

//    [ContextMenu("Show Character Creation")]
//    private void DebugShowCharacterCreation()
//    {
//        ShowCharacterCreation();
//    }

//    [ContextMenu("Reset Character Creation")]
//    private void DebugResetCharacterCreation()
//    {
//        ResetCharacterCreation();
//    }

//    #endregion
//}