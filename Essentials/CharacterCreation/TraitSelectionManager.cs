using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TraitSelectionManager
{
    public struct TraitUIReferences
    {
        public Transform availableTraitsContainer;
        public Transform selectedTraitsContainer;
        public GameObject traitButtonPrefab;
        public GameObject selectedTraitPrefab;
        public GameObject traitDetailPanel;
        public Image traitDetailIcon;
        public TMP_Text traitDetailName;
        public TMP_Text traitDetailDescription;
        public TMP_Text traitDetailCost;
        public Button addTraitButton;
        public Button removeTraitButton;
        public Dictionary<GameObject, Trait> traitButtons;
        public Dictionary<GameObject, Trait> selectedTraitButtons;
    }

    private CharacterCreationUI mainUI;
    private TraitUIReferences references;
    private UIDisplayManager displayManager;

    public TraitSelectionManager(CharacterCreationUI ui, TraitUIReferences refs, UIDisplayManager display)
    {
        mainUI = ui;
        references = refs;
        displayManager = display;
    }

    public void LoadAvailableTraits()
    {
        if (references.availableTraitsContainer == null)
        {
            mainUI.DebugLogError("availableTraitsContainer is null!");
            return;
        }

        ClearContainer(references.availableTraitsContainer);
        references.traitButtons.Clear();

        var selectedClass = mainUI.SelectedClass;
        if (selectedClass == null)
        {
            mainUI.DebugLogError("Cannot load traits - no class selected!");
            return;
        }

        List<Trait> availableTraits = selectedClass.GetSelectableTraits();

        if (availableTraits.Count == 0)
        {
            mainUI.DebugLogWarning("No traits available for this class! Check your PlayerClass trait setup.");
        }

        foreach (var trait in availableTraits)
        {
            if (trait == null) continue;

            // Skip if already selected
            if (mainUI.SelectedTraits.Contains(trait)) continue;

            CreateTraitButton(trait, references.availableTraitsContainer, false);
        }

        // Force layout rebuild for ScrollView
        Canvas.ForceUpdateCanvases();
        var layoutGroup = references.availableTraitsContainer.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(references.availableTraitsContainer.GetComponent<RectTransform>());
        }
    }

    private void CreateTraitButton(Trait trait, Transform container, bool isSelected)
    {
        if (trait == null || container == null) return;

        GameObject prefabToUse = isSelected ? references.selectedTraitPrefab : references.traitButtonPrefab;
        if (prefabToUse == null)
        {
            mainUI.DebugLogError($"Trait button prefab is null! (isSelected: {isSelected})");
            return;
        }

        GameObject buttonObj = Object.Instantiate(prefabToUse, container);
        Button button = buttonObj.GetComponent<Button>();

        if (button == null)
        {
            mainUI.DebugLogError("Trait button prefab doesn't have Button component!");
            return;
        }

        // Setup button display
        var buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            var selectedClass = mainUI.SelectedClass;
            int cost = selectedClass != null ? selectedClass.GetTraitCost(trait) : trait.cost;
            buttonText.text = $"{trait.Name} ({cost})";
        }

        // Capture trait reference for closure
        var traitReference = trait;

        // Store reference and setup listener
        if (isSelected)
        {
            references.selectedTraitButtons[buttonObj] = trait;
            button.onClick.AddListener(() => {
                SelectTraitForRemoval(traitReference);
            });
        }
        else
        {
            references.traitButtons[buttonObj] = trait;
            button.onClick.AddListener(() => {
                SelectTraitForAddition(traitReference);
            });
        }

        // Visual feedback for affordability
        if (!isSelected && mainUI.SelectedClass != null)
        {
            int cost = mainUI.SelectedClass.GetTraitCost(trait);
            button.interactable = mainUI.CurrentTraitPoints >= cost;

            if (mainUI.CurrentTraitPoints < cost)
            {
                var colors = button.colors;
                colors.normalColor = Color.gray;
                button.colors = colors;
            }
        }
    }

    private void SelectTraitForAddition(Trait trait)
    {
        mainUI.SetSelectedTrait(trait);
        ShowTraitDetails(trait, true);
    }

    private void SelectTraitForRemoval(Trait trait)
    {
        mainUI.SetSelectedTrait(trait);
        ShowTraitDetails(trait, false);
    }

    private void ShowTraitDetails(Trait trait, bool canAdd)
    {
        if (trait == null) return;

        if (references.traitDetailPanel != null)
        {
            references.traitDetailPanel.SetActive(true);
        }

        // Update trait details
        if (references.traitDetailIcon != null && trait.icon != null)
            references.traitDetailIcon.sprite = trait.icon;

        if (references.traitDetailName != null)
            references.traitDetailName.text = trait.Name;

        if (references.traitDetailDescription != null)
            references.traitDetailDescription.text = CharacterCreationValidator.GetTraitDescriptionSafe(trait);

        if (references.traitDetailCost != null)
        {
            var selectedClass = mainUI.SelectedClass;
            int cost = selectedClass != null ? selectedClass.GetTraitCost(trait) : trait.cost;
            references.traitDetailCost.text = $"Cost: {cost}";
        }

        // Update button states
        if (references.addTraitButton != null)
        {
            references.addTraitButton.gameObject.SetActive(canAdd);
            if (canAdd && mainUI.SelectedClass != null)
            {
                int cost = mainUI.SelectedClass.GetTraitCost(trait);
                references.addTraitButton.interactable = mainUI.CurrentTraitPoints >= cost && !mainUI.SelectedTraits.Contains(trait);
            }
        }

        if (references.removeTraitButton != null)
        {
            references.removeTraitButton.gameObject.SetActive(!canAdd);
            if (!canAdd)
                references.removeTraitButton.interactable = mainUI.SelectedTraits.Contains(trait);
        }
    }

    public void AddSelectedTrait()
    {
        var selectedTrait = mainUI.SelectedTrait;
        var selectedClass = mainUI.SelectedClass;

        if (selectedTrait == null || selectedClass == null) return;

        int cost = selectedClass.GetTraitCost(selectedTrait);

        if (mainUI.CurrentTraitPoints >= cost && !mainUI.SelectedTraits.Contains(selectedTrait))
        {
            mainUI.AddTraitToSelected(selectedTrait);
            mainUI.ModifyTraitPoints(-cost);

            RefreshTraitDisplays();
            NotifyUIUpdate();

            if (references.traitDetailPanel != null)
                references.traitDetailPanel.SetActive(false);
        }
        else
        {
            mainUI.DebugLogWarning($"Cannot add trait: insufficient points ({mainUI.CurrentTraitPoints} < {cost}) or already selected");
        }
    }

    public void RemoveSelectedTrait()
    {
        var selectedTrait = mainUI.SelectedTrait;
        var selectedClass = mainUI.SelectedClass;

        if (selectedTrait == null || selectedClass == null) return;

        if (mainUI.SelectedTraits.Contains(selectedTrait))
        {
            mainUI.RemoveTraitFromSelected(selectedTrait);
            int cost = selectedClass.GetTraitCost(selectedTrait);
            mainUI.ModifyTraitPoints(cost);

            RefreshTraitDisplays();
            NotifyUIUpdate();

            if (references.traitDetailPanel != null)
                references.traitDetailPanel.SetActive(false);
        }
    }

    private void RefreshTraitDisplays()
    {
        // Refresh available traits
        LoadAvailableTraits();

        // Refresh selected traits
        if (references.selectedTraitsContainer != null)
        {
            ClearContainer(references.selectedTraitsContainer);
            references.selectedTraitButtons.Clear();

            foreach (var trait in mainUI.SelectedTraits)
            {
                CreateTraitButton(trait, references.selectedTraitsContainer, true);
            }

            // Force layout rebuild for ScrollView
            Canvas.ForceUpdateCanvases();
            var layoutGroup = references.selectedTraitsContainer.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(references.selectedTraitsContainer.GetComponent<RectTransform>());
            }
        }
    }

    public void ClearSelectedTraits()
    {
        mainUI.ClearSelectedTraits();

        if (references.selectedTraitsContainer != null)
        {
            ClearContainer(references.selectedTraitsContainer);
            references.selectedTraitButtons.Clear();
        }

        if (mainUI.SelectedClass != null)
        {
            mainUI.SetSelectedClass(mainUI.SelectedClass); // This resets trait points
        }

        LoadAvailableTraits();
        NotifyUIUpdate();
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(container.GetChild(i).gameObject);
        }
    }

    private void NotifyUIUpdate()
    {
        if (displayManager != null)
        {
            displayManager.UpdateTraitPointsDisplay();
            displayManager.UpdateCreateButtonState();
        }
    }
}