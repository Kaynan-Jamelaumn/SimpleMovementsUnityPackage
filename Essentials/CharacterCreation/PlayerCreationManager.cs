using UnityEngine;

public class PlayerCreationManager
{
    public struct PlayerCreationReferences
    {
        public GameObject playerPrefab;
        public Transform spawnPoint;
        public PlayerStatusController prefabStatusController;
        public PlayerNameComponent prefabNameComponent;
        public TraitManager prefabTraitManager;
    }

    private CharacterCreationUI mainUI;
    private PlayerCreationReferences references;
    private TMPro.TMP_InputField characterNameInput;

    public PlayerCreationManager(CharacterCreationUI ui, PlayerCreationReferences refs, TMPro.TMP_InputField nameInput)
    {
        mainUI = ui;
        references = refs;
        characterNameInput = nameInput;
    }

    public void CreatePlayer()
    {
        var selectedClass = mainUI.SelectedClass;
        if (selectedClass == null)
        {
            mainUI.OnCreationError("Cannot create player: no class selected");
            return;
        }

        if (characterNameInput == null || string.IsNullOrEmpty(characterNameInput.text.Trim()))
        {
            mainUI.OnCreationError("Cannot create player: no name entered");
            return;
        }

        if (references.playerPrefab == null)
        {
            mainUI.OnCreationError("Cannot create player: player prefab is not assigned");
            return;
        }

        try
        {
            // Spawn player prefab
            Vector3 spawnPosition = references.spawnPoint != null ? references.spawnPoint.position : Vector3.zero;
            GameObject playerObj = Object.Instantiate(references.playerPrefab, spawnPosition, Quaternion.identity);

            // Configure player components
            ConfigurePlayerComponents(playerObj, characterNameInput.text.Trim());

            // Notify main UI of successful creation
            mainUI.DebugLog($"Successfully created player: {characterNameInput.text.Trim()}");
            mainUI.OnPlayerCreatedSuccess(playerObj);
        }
        catch (System.Exception e)
        {
            mainUI.OnCreationError($"Error creating player: {e.Message}");
        }
    }

    private void ConfigurePlayerComponents(GameObject playerObj, string characterName)
    {
        var selectedClass = mainUI.SelectedClass;
        var selectedTraits = mainUI.SelectedTraits;

        // Configure player using prefab references (with GetComponent fallback)
        var statusController = GetPlayerComponent<PlayerStatusController>(playerObj, references.prefabStatusController);
        var nameComponent = GetPlayerComponent<PlayerNameComponent>(playerObj, references.prefabNameComponent);
        var traitManager = GetPlayerComponent<TraitManager>(playerObj, references.prefabTraitManager);

        bool configurationSuccessful = true;

        // Configure status controller and class
        if (statusController != null)
        {
            try
            {
                // Set player class
                statusController.SetPlayerClass(selectedClass);
                mainUI.DebugLog($"Set player class: {CharacterCreationValidator.GetClassNameSafe(selectedClass)}");

                // Apply selected traits
                if (traitManager != null)
                {
                    int traitsApplied = 0;
                    foreach (var trait in selectedTraits)
                    {
                        try
                        {
                            traitManager.AddTrait(trait, true);
                            traitsApplied++;
                        }
                        catch (System.Exception e)
                        {
                            mainUI.DebugLogWarning($"Failed to apply trait {trait.Name}: {e.Message}");
                            configurationSuccessful = false;
                        }
                    }
                    mainUI.DebugLog($"Applied {traitsApplied}/{selectedTraits.Count} traits");
                }
                else
                {
                    mainUI.DebugLogWarning("TraitManager not found - traits not applied");
                    configurationSuccessful = false;
                }
            }
            catch (System.Exception e)
            {
                mainUI.DebugLogError($"Error configuring status controller: {e.Message}");
                configurationSuccessful = false;
            }
        }
        else
        {
            mainUI.DebugLogError("PlayerStatusController not found - player class not set");
            configurationSuccessful = false;
        }

        // Set character name
        if (nameComponent != null)
        {
            try
            {
                nameComponent.SetPlayerName(characterName);
                mainUI.DebugLog($"Set player name: {characterName}");
            }
            catch (System.Exception e)
            {
                mainUI.DebugLogWarning($"Error setting player name: {e.Message}");
                // Fallback: just set GameObject name
                playerObj.name = $"Player_{characterName}";
                configurationSuccessful = false;
            }
        }
        else
        {
            // Fallback: just set GameObject name
            playerObj.name = $"Player_{characterName}";
            mainUI.DebugLogWarning("PlayerNameComponent not found - only GameObject name set");
            configurationSuccessful = false;
        }

        // Log configuration result
        if (configurationSuccessful)
        {
            mainUI.DebugLog("Player configuration completed successfully");
        }
        else
        {
            mainUI.DebugLogWarning("Player configuration completed with some issues");
        }
    }

    private T GetPlayerComponent<T>(GameObject playerObj, T prefabReference) where T : Component
    {
        // If we have a prefab reference, try to find the corresponding component on the spawned object
        if (prefabReference != null)
        {
            // Find the component on the spawned object that corresponds to the prefab reference
            var components = playerObj.GetComponentsInChildren<T>(true);

            // If there's only one component of this type, use it
            if (components.Length == 1)
            {
                return components[0];
            }

            // If there are multiple, try to match by component path/hierarchy
            if (components.Length > 1)
            {
                string prefabPath = GetComponentPath(prefabReference);
                foreach (var component in components)
                {
                    string componentPath = GetComponentPath(component);
                    if (componentPath.EndsWith(prefabPath))
                    {
                        return component;
                    }
                }
                // If no exact match, return the first one
                mainUI.DebugLogWarning($"Multiple {typeof(T).Name} components found, using first one");
                return components[0];
            }
        }

        // Fallback: use GetComponent
        var fallbackComponent = playerObj.GetComponent<T>();
        if (fallbackComponent == null)
        {
            fallbackComponent = playerObj.GetComponentInChildren<T>();
        }

        if (fallbackComponent == null)
        {
            mainUI.DebugLogError($"No {typeof(T).Name} component found on player object!");
        }

        return fallbackComponent;
    }

    private string GetComponentPath(Component component)
    {
        if (component == null) return "";

        string path = component.name;
        Transform parent = component.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}