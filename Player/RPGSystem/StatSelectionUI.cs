using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Simple UI for stat selection when leveling up
public class StatSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject statSelectionPanel;
    [SerializeField] private Transform statButtonParent;
    [SerializeField] private Button statButtonPrefab;
    [SerializeField] private TextMeshProUGUI levelUpText;
    [SerializeField] private TextMeshProUGUI availablePointsText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Player Reference")]
    [SerializeField] private PlayerStatusController playerController;

    private List<Button> statButtons = new List<Button>();
    private string selectedStat = "";

    private void Start()
    {
        // Subscribe to level up events
        if (playerController?.XPManager != null)
        {
            playerController.XPManager.OnLevelUp += OnPlayerLevelUp;
            playerController.XPManager.OnStatUpgradePointsGained += OnStatPointsGained;
        }

        // Setup UI
        SetupUI();

        // Hide panel initially
        if (statSelectionPanel != null)
            statSelectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (playerController?.XPManager != null)
        {
            playerController.XPManager.OnLevelUp -= OnPlayerLevelUp;
            playerController.XPManager.OnStatUpgradePointsGained -= OnStatPointsGained;
        }
    }

    private void SetupUI()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmStatSelection);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(CloseStatSelection);
    }

    private void OnPlayerLevelUp(int newLevel)
    {
        if (levelUpText != null)
            levelUpText.text = $"Level {newLevel}!";
    }

    private void OnStatPointsGained(int points)
    {
        // Show stat selection UI when player gains stat points
        ShowStatSelection();
    }

    public void ShowStatSelection()
    {
        if (playerController?.XPManager == null) return;

        // Check if player has stat points to spend
        if (playerController.XPManager.StatUpgradePoints <= 0)
        {
            Debug.Log("No stat upgrade points available");
            return;
        }

        // Update UI
        UpdateAvailablePointsText();
        CreateStatButtons();

        // Show panel
        if (statSelectionPanel != null)
            statSelectionPanel.SetActive(true);
    }

    public void CloseStatSelection()
    {
        if (statSelectionPanel != null)
            statSelectionPanel.SetActive(false);

        selectedStat = "";
        UpdateConfirmButton();
    }

    private void CreateStatButtons()
    {
        // Clear existing buttons
        ClearStatButtons();

        if (statButtonPrefab == null || statButtonParent == null) return;

        var upgradeableStats = playerController.GetUpgradeableStats();

        foreach (string stat in upgradeableStats)
        {
            Button button = Instantiate(statButtonPrefab, statButtonParent);

            // Setup button text
            var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                string preview = playerController.GetStatUpgradePreview(stat);
                buttonText.text = $"{FormatStatName(stat)}\n{preview}";
            }

            // Setup button click
            string statName = stat; // Capture for closure
            button.onClick.AddListener(() => SelectStat(statName, button));

            statButtons.Add(button);
        }
    }

    private void ClearStatButtons()
    {
        foreach (var button in statButtons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        statButtons.Clear();
    }

    private void SelectStat(string statName, Button button)
    {
        selectedStat = statName;

        // Update button visuals
        foreach (var btn in statButtons)
        {
            var colors = btn.colors;
            colors.normalColor = Color.white;
            btn.colors = colors;
        }

        // Highlight selected button
        var selectedColors = button.colors;
        selectedColors.normalColor = Color.green;
        button.colors = selectedColors;

        UpdateConfirmButton();

        Debug.Log($"Selected stat: {statName}");
    }

    private void ConfirmStatSelection()
    {
        if (string.IsNullOrEmpty(selectedStat))
        {
            Debug.Log("No stat selected!");
            return;
        }

        if (playerController.UpgradeStat(selectedStat))
        {
            Debug.Log($"Successfully upgraded {selectedStat}!");

            // Update UI
            UpdateAvailablePointsText();

            // Check if player has more points to spend
            if (playerController.XPManager.StatUpgradePoints > 0)
            {
                // Refresh stat buttons for next selection
                CreateStatButtons();
                selectedStat = "";
                UpdateConfirmButton();
            }
            else
            {
                // Close UI if no more points
                CloseStatSelection();
            }
        }
        else
        {
            Debug.Log($"Failed to upgrade {selectedStat}!");
        }
    }

    private void UpdateAvailablePointsText()
    {
        if (availablePointsText != null && playerController?.XPManager != null)
        {
            int points = playerController.XPManager.StatUpgradePoints;
            availablePointsText.text = $"Stat Points: {points}";
        }
    }

    private void UpdateConfirmButton()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = !string.IsNullOrEmpty(selectedStat);
        }
    }

    private string FormatStatName(string statName)
    {
        // Convert "health" to "Health", "bodyHeat" to "Body Heat", etc.
        if (string.IsNullOrEmpty(statName)) return statName;

        // Handle camelCase conversion
        string formatted = System.Text.RegularExpressions.Regex.Replace(
            statName,
            "([a-z])([A-Z])",
            "$1 $2"
        );

        // Capitalize first letter
        return char.ToUpper(formatted[0]) + formatted.Substring(1);
    }

    // Debug methods
    [ContextMenu("Show Stat Selection (Debug)")]
    private void DebugShowStatSelection()
    {
        ShowStatSelection();
    }

    [ContextMenu("Add Stat Points (Debug)")]
    private void DebugAddStatPoints()
    {
        if (playerController?.XPManager != null)
        {
            playerController.XPManager.StatUpgradePoints += 3;
            UpdateAvailablePointsText();
        }
    }
}