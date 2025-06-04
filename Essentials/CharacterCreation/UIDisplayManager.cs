using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIDisplayManager
{
    public struct DisplayReferences
    {
        public TMP_InputField characterNameInput;
        public GameObject classSummaryPanel;
        public GameObject traitSelectionPanel;
        public GameObject traitDetailPanel;
        public Button createPlayerButton;
        public TMP_Text currentTraitPointsText;
        public Transform availableTraitsContainer;
        public Transform selectedTraitsContainer;
        public Transform classListContainer;
    }

    private CharacterCreationUI mainUI;
    private DisplayReferences references;

    public UIDisplayManager(CharacterCreationUI ui, DisplayReferences refs)
    {
        mainUI = ui;
        references = refs;
    }

    public void InitializePanelStates()
    {
        // Initially hide panels
        if (references.classSummaryPanel != null)
            references.classSummaryPanel.SetActive(false);
        if (references.traitSelectionPanel != null)
            references.traitSelectionPanel.SetActive(false);
        if (references.traitDetailPanel != null)
            references.traitDetailPanel.SetActive(false);
        if (references.createPlayerButton != null)
            references.createPlayerButton.interactable = false;
    }

    public void InitializeContainers()
    {
        // Clear containers
        ClearContainer(references.classListContainer);
        ClearContainer(references.availableTraitsContainer);
        ClearContainer(references.selectedTraitsContainer);

        UpdateTraitPointsDisplay();
    }

    public void UpdateTraitPointsDisplay()
    {
        if (references.currentTraitPointsText != null)
        {
            references.currentTraitPointsText.text = $"Available Points: {mainUI.CurrentTraitPoints}";
        }
    }

    public void UpdateCreateButtonState()
    {
        bool canCreate = mainUI.SelectedClass != null &&
                        references.characterNameInput != null &&
                        !string.IsNullOrEmpty(references.characterNameInput.text.Trim());

        if (references.createPlayerButton != null)
            references.createPlayerButton.interactable = canCreate;
    }

    public void ResetAllPanels()
    {
        if (references.classSummaryPanel != null)
            references.classSummaryPanel.SetActive(false);
        if (references.traitSelectionPanel != null)
            references.traitSelectionPanel.SetActive(false);
        if (references.traitDetailPanel != null)
            references.traitDetailPanel.SetActive(false);
    }

    public void ClearAllContainers()
    {
        ClearContainer(references.availableTraitsContainer);
        ClearContainer(references.selectedTraitsContainer);
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(container.GetChild(i).gameObject);
        }
    }
}