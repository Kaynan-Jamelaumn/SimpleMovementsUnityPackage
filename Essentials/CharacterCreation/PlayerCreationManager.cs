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
            mainUI.DebugLogError("Cannot create player: no class selected");
            return;
        }

        if (characterNameInput == null || string.IsNullOrEmpty(characterNameInput.text.Trim()))
        {
            mainUI.DebugLogError("Cannot create player: no name entered");
            return;
        }

        // Spawn player prefab
        Vector3 spawnPosition = references.spawnPoint != null ? references.spawnPoint.position : Vector3.zero;
        GameObject playerObj = Object.Instantiate(references.playerPrefab, spawnPosition, Quaternion.identity);

        // Configure player components
        ConfigurePlayerComponents(playerObj, characterNameInput.text.Trim());

        // Notify main UI of successful creation
        mainUI.OnPlayerCreatedSuccess(playerObj);
    }

    private void ConfigurePlayerComponents(GameObject playerObj, string characterName)
    {
        var selectedClass = mainUI.SelectedClass;
        var selectedTraits = mainUI.SelectedTraits;

        // Configure player using prefab references (with GetComponent fallback)
        var statusController = GetPlayerComponent<PlayerStatusController>(playerObj, references.prefabStatusController);
        var nameComponent = GetPlayerComponent<PlayerNameComponent>(playerObj, references.prefabNameComponent);
        var traitManager = GetPlayerComponent<TraitManager>(playerObj, references.prefabTraitManager);

        if (statusController != null)
        {
            // Set player class
            statusController.SetPlayerClass(selectedClass);

            // Apply selected traits
            if (traitManager != null)
            {
                foreach (var trait in selectedTraits)
                {
                    traitManager.AddTrait(trait, true);
                }
            }
            else
            {
                mainUI.DebugLogWarning("TraitManager not found - traits not applied");
            }
        }
        else
        {
            mainUI.DebugLogError("PlayerStatusController not found - player class not set");
        }

        // Set character name
        if (nameComponent != null)
        {
            nameComponent.SetPlayerName(characterName);
        }
        else
        {
            // Fallback: just set GameObject name
            playerObj.name = $"Player_{characterName}";
            mainUI.DebugLogWarning("PlayerNameComponent not found - only GameObject name set");
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
                return components[0];
            }
        }

        // Fallback: use GetComponent
        var fallbackComponent = playerObj.GetComponent<T>();
        if (fallbackComponent == null)
        {
            fallbackComponent = playerObj.GetComponentInChildren<T>();
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