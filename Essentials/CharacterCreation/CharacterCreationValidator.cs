using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterCreationValidator
{
    public struct UIReferences
    {
        public TMP_InputField characterNameInput;
        public Transform classListContainer;
        public GameObject classButtonPrefab;
        public GameObject traitSelectionPanel;
        public Transform availableTraitsContainer;
        public Transform selectedTraitsContainer;
        public Button createPlayerButton;
        public GameObject playerPrefab;
        public PlayerStatusController prefabStatusController;
        public PlayerNameComponent prefabNameComponent;
        public TraitManager prefabTraitManager;
        public List<PlayerClass> availableClasses;
    }

    private CharacterCreationUI mainUI;
    private UIReferences references;

    public CharacterCreationValidator(CharacterCreationUI ui, UIReferences refs)
    {
        mainUI = ui;
        references = refs;
    }

    public void ValidateSetup()
    {
        ValidateUISetup();
        ValidateReferences();
        ValidatePrefabReferences();
    }

    private void ValidateUISetup()
    {
        var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            mainUI.DebugLogError("NO EVENTSYSTEM FOUND! Please add an EventSystem to your scene");
        }

        var canvas = mainUI.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            mainUI.DebugLogError("NO CANVAS FOUND! This UI must be a child of a Canvas!");
        }
        else
        {
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                mainUI.DebugLogError("Canvas is missing GraphicRaycaster component!");
            }
        }
    }

    private void ValidateReferences()
    {
        if (references.characterNameInput == null)
            mainUI.DebugLogError("Character Name Input is not assigned!");
        if (references.classListContainer == null)
            mainUI.DebugLogError("Class List Container is not assigned!");
        if (references.classButtonPrefab == null)
            mainUI.DebugLogError("Class Button Prefab is not assigned!");
        if (references.traitSelectionPanel == null)
            mainUI.DebugLogError("Trait Selection Panel is not assigned!");
        if (references.availableTraitsContainer == null)
            mainUI.DebugLogError("Available Traits Container is not assigned!");
        if (references.selectedTraitsContainer == null)
            mainUI.DebugLogError("Selected Traits Container is not assigned!");
        if (references.createPlayerButton == null)
            mainUI.DebugLogError("Create Player Button is not assigned!");
        if (references.playerPrefab == null)
            mainUI.DebugLogError("Player Prefab is not assigned!");

        if (references.availableClasses == null || references.availableClasses.Count == 0)
        {
            mainUI.DebugLogError("No available classes assigned!");
        }
        else
        {
            for (int i = 0; i < references.availableClasses.Count; i++)
            {
                if (references.availableClasses[i] == null)
                {
                    mainUI.DebugLogError($"PlayerClass at index {i} is null!");
                }
            }
        }
    }

    public void ValidatePrefabReferences()
    {
        if (references.playerPrefab == null)
        {
            mainUI.DebugLogError("Player Prefab is not assigned!");
            return;
        }

        bool hasValidReferences = true;

        if (references.prefabStatusController == null)
        {
            mainUI.DebugLogWarning("Prefab StatusController reference not assigned - will use GetComponent fallback");
            hasValidReferences = false;
        }

        if (references.prefabNameComponent == null)
        {
            mainUI.DebugLogWarning("Prefab NameComponent reference not assigned - will use GetComponent fallback");
            hasValidReferences = false;
        }

        if (references.prefabTraitManager == null)
        {
            mainUI.DebugLogWarning("Prefab TraitManager reference not assigned - will use GetComponent fallback");
            hasValidReferences = false;
        }

        if (hasValidReferences)
        {
            ValidateComponentsExistOnPrefab();
        }
    }

    private void ValidateComponentsExistOnPrefab()
    {
        // Check PlayerStatusController
        if (references.prefabStatusController != null)
        {
            var prefabStatusControllers = references.playerPrefab.GetComponentsInChildren<PlayerStatusController>(true);
            if (prefabStatusControllers.Length == 0)
            {
                mainUI.DebugLogError("Assigned StatusController reference doesn't match any component on the prefab!");
            }
            else if (!System.Array.Exists(prefabStatusControllers, x => x == references.prefabStatusController))
            {
                mainUI.DebugLogWarning("Assigned StatusController reference might be from a different prefab instance");
            }
        }

        // Check PlayerNameComponent
        if (references.prefabNameComponent != null)
        {
            var prefabNameComponents = references.playerPrefab.GetComponentsInChildren<PlayerNameComponent>(true);
            if (prefabNameComponents.Length == 0)
            {
                mainUI.DebugLogError("Assigned NameComponent reference doesn't match any component on the prefab!");
            }
            else if (!System.Array.Exists(prefabNameComponents, x => x == references.prefabNameComponent))
            {
                mainUI.DebugLogWarning("Assigned NameComponent reference might be from a different prefab instance");
            }
        }

        // Check TraitManager
        if (references.prefabTraitManager != null)
        {
            var prefabTraitManagers = references.playerPrefab.GetComponentsInChildren<TraitManager>(true);
            if (prefabTraitManagers.Length == 0)
            {
                mainUI.DebugLogError("Assigned TraitManager reference doesn't match any component on the prefab!");
            }
            else if (!System.Array.Exists(prefabTraitManagers, x => x == references.prefabTraitManager))
            {
                mainUI.DebugLogWarning("Assigned TraitManager reference might be from a different prefab instance");
            }
        }
    }

    public static string GetClassNameSafe(PlayerClass playerClass)
    {
        if (playerClass == null) return "Unknown Class";

        try
        {
            return playerClass.GetClassName() ?? "Unnamed Class";
        }
        catch
        {
            return playerClass.className ?? "Unnamed Class";
        }
    }

    public static string GetClassDescriptionSafe(PlayerClass playerClass)
    {
        if (playerClass == null) return "No description available";

        try
        {
            // Get basic description without exclusive traits since they're displayed separately
            return GetBasicClassDescription(playerClass) ?? "No description available";
        }
        catch
        {
            return playerClass.classDescription ?? "No description available";
        }
    }

    private static string GetBasicClassDescription(PlayerClass playerClass)
    {
        // Try to get a clean description without exclusive traits
        // First try the basic classDescription field
        if (!string.IsNullOrEmpty(playerClass.classDescription))
        {
            return playerClass.classDescription;
        }

        // If the class has a GetFormattedDescription method that includes exclusive traits,
        // we might need to use an alternative method or field
        // For now, fallback to the basic description
        try
        {
            // Check if there's a method to get description without traits
            var descMethod = playerClass.GetType().GetMethod("GetBasicDescription");
            if (descMethod != null)
            {
                return descMethod.Invoke(playerClass, null) as string;
            }

            // Otherwise use the regular formatted description
            var formattedDesc = playerClass.GetFormattedDescription();

            // If it contains exclusive traits info, try to clean it up
            if (!string.IsNullOrEmpty(formattedDesc))
            {
                // Remove common exclusive traits patterns from description
                var cleanedDesc = formattedDesc;

                // Remove lines that start with exclusive traits indicators
                var lines = cleanedDesc.Split('\n');
                var filteredLines = new System.Collections.Generic.List<string>();

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    // Skip lines that look like exclusive traits listings
                    if (trimmedLine.StartsWith("Exclusive Traits:") ||
                        trimmedLine.StartsWith("Unique Traits:") ||
                        trimmedLine.StartsWith("• ") ||
                        trimmedLine.StartsWith("- ") ||
                        (trimmedLine.Contains("Exclusive") && trimmedLine.Contains("Trait")))
                    {
                        continue;
                    }
                    filteredLines.Add(line);
                }

                return string.Join("\n", filteredLines).Trim();
            }
        }
        catch
        {
            // If anything fails, just return the basic description
        }

        return playerClass.classDescription;
    }

    public static string GetTraitDescriptionSafe(Trait trait)
    {
        if (trait == null) return "No description available";

        try
        {
            return trait.GetFormattedDescription() ?? "No description available";
        }
        catch
        {
            return trait.description ?? "No description available";
        }
    }
}