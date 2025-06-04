using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClassSelectionManager
{
    public struct ClassUIReferences
    {
        public Transform classListContainer;
        public GameObject classButtonPrefab;
        public GameObject classSummaryPanel;
        public Image classIcon;
        public TMP_Text className;
        public TMP_Text classDescription;
        public TMP_Text classTraitPoints;
        public TMP_Text uniqueTraitsText;
        public GameObject traitSelectionPanel;
        public Dictionary<GameObject, PlayerClass> classButtons;
    }

    private CharacterCreationUI mainUI;
    private ClassUIReferences references;
    private UIDisplayManager displayManager;
    private TraitSelectionManager traitManager;

    public ClassSelectionManager(CharacterCreationUI ui, ClassUIReferences refs, UIDisplayManager display, TraitSelectionManager traits)
    {
        mainUI = ui;
        references = refs;
        displayManager = display;
        traitManager = traits;
    }

    public void SetTraitManager(TraitSelectionManager traits)
    {
        traitManager = traits;
    }

    public void LoadAvailableClasses(List<PlayerClass> availableClasses)
    {
        if (availableClasses == null || availableClasses.Count == 0)
        {
            mainUI.DebugLogError("No classes to load!");
            return;
        }

        int loadedCount = 0;
        foreach (var playerClass in availableClasses)
        {
            if (playerClass != null)
            {
                CreateClassButton(playerClass);
                loadedCount++;
            }
        }

        // Force layout rebuild for ScrollView
        if (references.classListContainer != null)
        {
            Canvas.ForceUpdateCanvases();
            var layoutGroup = references.classListContainer.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(references.classListContainer.GetComponent<RectTransform>());
            }
        }
    }

    private void CreateClassButton(PlayerClass playerClass)
    {
        if (playerClass == null || references.classButtonPrefab == null || references.classListContainer == null)
        {
            mainUI.DebugLogError("Cannot create button - missing references!");
            return;
        }

        GameObject buttonObj = Object.Instantiate(references.classButtonPrefab, references.classListContainer);
        Button button = buttonObj.GetComponent<Button>();

        if (button == null)
        {
            mainUI.DebugLogError("Class button prefab doesn't have a Button component!");
            Object.DestroyImmediate(buttonObj);
            return;
        }

        button.interactable = true;
        button.enabled = true;
        buttonObj.SetActive(true);

        // Setup button text
        var buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            string className = CharacterCreationValidator.GetClassNameSafe(playerClass);
            buttonText.text = className;
        }

        // Store reference
        references.classButtons[buttonObj] = playerClass;

        // Setup click listener
        var classReference = playerClass;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => {
            SelectClass(classReference);
        });
    }

    public void SelectClass(PlayerClass playerClass)
    {
        if (playerClass == null)
        {
            mainUI.DebugLogError("Cannot select null PlayerClass!");
            return;
        }

        mainUI.SetSelectedClass(playerClass);
        UpdateClassSummary();
        ShowTraitSelectionPanel();

        // Notify other managers to update
        if (traitManager != null)
        {
            traitManager.LoadAvailableTraits();
            traitManager.ClearSelectedTraits();
        }

        if (displayManager != null)
        {
            displayManager.UpdateTraitPointsDisplay();
            displayManager.UpdateCreateButtonState();
        }
    }

    private void UpdateClassSummary()
    {
        var selectedClass = mainUI.SelectedClass;
        if (selectedClass == null)
        {
            mainUI.DebugLogError("Cannot update class summary - selectedClass is null!");
            return;
        }

        // Show the class summary panel
        if (references.classSummaryPanel != null)
        {
            references.classSummaryPanel.SetActive(true);
        }

        // Update class info
        if (references.classIcon != null && selectedClass.classIcon != null)
        {
            references.classIcon.sprite = selectedClass.classIcon;
        }

        if (references.className != null)
        {
            string name = CharacterCreationValidator.GetClassNameSafe(selectedClass);
            references.className.text = name;
        }

        if (references.classDescription != null)
        {
            string description = CharacterCreationValidator.GetClassDescriptionSafe(selectedClass);
            references.classDescription.text = description;
        }

        if (references.classTraitPoints != null)
        {
            references.classTraitPoints.text = $"Trait Points: {selectedClass.traitPoints}";
        }

        DisplayUniqueTraits();
    }

    private void DisplayUniqueTraits()
    {
        if (references.uniqueTraitsText == null)
        {
            return;
        }

        var selectedClass = mainUI.SelectedClass;
        try
        {
            var exclusiveTraits = selectedClass.GetExclusiveTraits();
            if (exclusiveTraits == null || exclusiveTraits.Count == 0)
            {
                references.uniqueTraitsText.text = "No unique traits";
                return;
            }

            string traitsText = "Unique Traits:\n";
            foreach (var trait in exclusiveTraits)
            {
                if (trait != null)
                    traitsText += $"• {trait.Name}\n";
            }

            references.uniqueTraitsText.text = traitsText.TrimEnd();
        }
        catch (System.Exception e)
        {
            mainUI.DebugLogError($"Error displaying unique traits: {e.Message}");
            references.uniqueTraitsText.text = "Error loading unique traits";
        }
    }

    private void ShowTraitSelectionPanel()
    {
        if (references.traitSelectionPanel != null)
        {
            references.traitSelectionPanel.SetActive(true);
        }
    }

    public void ClearClassButtons()
    {
        if (references.classListContainer != null)
        {
            for (int i = references.classListContainer.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(references.classListContainer.GetChild(i).gameObject);
            }
        }
        references.classButtons.Clear();
    }
}