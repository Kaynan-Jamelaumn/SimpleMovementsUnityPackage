using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

[System.Serializable]
public class ClassSelectedEvent : UnityEvent<PlayerClass> { }

public class ClassSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject classSelectionPanel;
    [SerializeField] private Transform classButtonParent;
    [SerializeField] private Button classButtonPrefab;

    [Header("Class Display")]
    [SerializeField] private Image classIcon;
    [SerializeField] private TextMeshProUGUI className;
    [SerializeField] private TextMeshProUGUI classDescription;
    [SerializeField] private Transform statDisplayParent;
    [SerializeField] private TextMeshProUGUI statDisplayPrefab;

    [Header("Trait Display")]
    [SerializeField] private Transform traitDisplayParent;
    [SerializeField] private GameObject traitDisplayPrefab;

    [Header("Control Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Available Classes")]
    [SerializeField] private List<PlayerClass> availableClasses = new List<PlayerClass>();

    [Header("Player Reference")]
    [SerializeField] private PlayerStatusController playerController;

    [Header("Events")]
    public ClassSelectedEvent OnClassSelected;

    private List<Button> classButtons = new List<Button>();
    private List<TextMeshProUGUI> statDisplays = new List<TextMeshProUGUI>();
    private List<GameObject> traitDisplays = new List<GameObject>();
    private PlayerClass selectedClass;

    private void Start()
    {
        SetupUI();

        // Hide panel initially
        if (classSelectionPanel != null)
            classSelectionPanel.SetActive(false);
    }

    private void SetupUI()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmClassSelection);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelClassSelection);

        UpdateConfirmButton();
    }

    public void ShowClassSelection()
    {
        CreateClassButtons();

        if (classSelectionPanel != null)
            classSelectionPanel.SetActive(true);
    }

    public void HideClassSelection()
    {
        if (classSelectionPanel != null)
            classSelectionPanel.SetActive(false);
    }

    private void CreateClassButtons()
    {
        ClearClassButtons();

        if (classButtonPrefab == null || classButtonParent == null) return;

        foreach (var playerClass in availableClasses)
        {
            if (playerClass == null) continue;

            Button button = Instantiate(classButtonPrefab, classButtonParent);

            // Setup button appearance
            SetupClassButton(button, playerClass);

            // Setup button click
            button.onClick.AddListener(() => SelectClass(playerClass, button));

            classButtons.Add(button);
        }
    }

    private void SetupClassButton(Button button, PlayerClass playerClass)
    {
        // Set button text
        var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.text = playerClass.GetClassName();

        // Set button icon if available
        var buttonImage = button.GetComponent<Image>();
        if (buttonImage != null && playerClass.classIcon != null)
        {
            buttonImage.sprite = playerClass.classIcon;
        }

        // Set button color
        if (playerClass.classColor != Color.white)
        {
            var colors = button.colors;
            colors.normalColor = playerClass.classColor;
            button.colors = colors;
        }
    }

    private void ClearClassButtons()
    {
        foreach (var button in classButtons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        classButtons.Clear();
    }

    private void SelectClass(PlayerClass playerClass, Button button)
    {
        selectedClass = playerClass;

        // Update button visuals
        foreach (var btn in classButtons)
        {
            var colors = btn.colors;
            colors.normalColor = playerClass.classColor != Color.white ? playerClass.classColor : Color.white;
            btn.colors = colors;
        }

        // Highlight selected button
        var selectedColors = button.colors;
        selectedColors.normalColor = Color.green;
        button.colors = selectedColors;

        // Update class display
        UpdateClassDisplay(playerClass);
        UpdateConfirmButton();

        Debug.Log($"Selected class: {playerClass.GetClassName()}");
    }

    private void UpdateClassDisplay(PlayerClass playerClass)
    {
        // Update class info
        if (classIcon != null)
            classIcon.sprite = playerClass.classIcon;

        if (className != null)
            className.text = playerClass.GetClassName();

        if (classDescription != null)
            classDescription.text = playerClass.classDescription;

        // Update stats display
        UpdateStatsDisplay(playerClass);

        // Update traits display
        UpdateTraitsDisplay(playerClass);
    }

    private void UpdateStatsDisplay(PlayerClass playerClass)
    {
        ClearStatsDisplay();

        if (statDisplayPrefab == null || statDisplayParent == null) return;

        // Create stat displays
        var stats = new Dictionary<string, float>
        {
            {"Health", playerClass.health},
            {"Stamina", playerClass.stamina},
            {"Mana", playerClass.mana},
            {"Speed", playerClass.speed},
            {"Strength", playerClass.strength},
            {"Agility", playerClass.agility},
            {"Intelligence", playerClass.intelligence},
            {"Endurance", playerClass.endurance}
        };

        foreach (var stat in stats)
        {
            var statDisplay = Instantiate(statDisplayPrefab, statDisplayParent);
            statDisplay.text = $"{stat.Key}: {stat.Value:F1}";
            statDisplays.Add(statDisplay);
        }
    }

    private void ClearStatsDisplay()
    {
        foreach (var display in statDisplays)
        {
            if (display != null)
                DestroyImmediate(display.gameObject);
        }
        statDisplays.Clear();
    }

    private void UpdateTraitsDisplay(PlayerClass playerClass)
    {
        ClearTraitsDisplay();

        if (traitDisplayPrefab == null || traitDisplayParent == null) return;

        foreach (var trait in playerClass.startingTraits)
        {
            if (trait == null) continue;

            var traitDisplay = Instantiate(traitDisplayPrefab, traitDisplayParent);

            // Setup trait display
            var traitText = traitDisplay.GetComponentInChildren<TextMeshProUGUI>();
            if (traitText != null)
                traitText.text = $"{trait.Name}\n{trait.description}";

            // Setup trait icon if available
            var traitIcon = traitDisplay.GetComponentInChildren<Image>();
            if (traitIcon != null && trait.icon != null)
                traitIcon.sprite = trait.icon;

            traitDisplays.Add(traitDisplay);
        }
    }

    private void ClearTraitsDisplay()
    {
        foreach (var display in traitDisplays)
        {
            if (display != null)
                DestroyImmediate(display);
        }
        traitDisplays.Clear();
    }

    private void ConfirmClassSelection()
    {
        if (selectedClass == null)
        {
            Debug.Log("No class selected!");
            return;
        }

        // Apply class to player
        if (playerController != null)
        {
            playerController.SetPlayerClass(selectedClass);
        }

        // Fire event
        OnClassSelected?.Invoke(selectedClass);

        Debug.Log($"Class confirmed: {selectedClass.GetClassName()}");

        // Hide UI
        HideClassSelection();
    }

    private void CancelClassSelection()
    {
        selectedClass = null;
        HideClassSelection();
    }

    private void UpdateConfirmButton()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = selectedClass != null;
        }
    }

    // Public methods for external control
    public void AddAvailableClass(PlayerClass playerClass)
    {
        if (playerClass != null && !availableClasses.Contains(playerClass))
        {
            availableClasses.Add(playerClass);
        }
    }

    public void RemoveAvailableClass(PlayerClass playerClass)
    {
        availableClasses.Remove(playerClass);
    }

    public void SetAvailableClasses(List<PlayerClass> classes)
    {
        availableClasses = new List<PlayerClass>(classes);
    }

    // Debug methods
    [ContextMenu("Show Class Selection (Debug)")]
    private void DebugShowClassSelection()
    {
        ShowClassSelection();
    }

    [ContextMenu("Add Debug Classes")]
    private void AddDebugClasses()
    {
        // This would need actual class assets to work
        Debug.Log("Add your PlayerClass ScriptableObjects to the availableClasses list");
    }
}