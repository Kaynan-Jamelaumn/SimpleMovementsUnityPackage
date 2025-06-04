using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterCreationUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField characterNameInput;
    [SerializeField] private Transform classListContainer;
    [SerializeField] private GameObject classButtonPrefab;

    [Header("Class Summary Panel")]
    [SerializeField] private GameObject classSummaryPanel;
    [SerializeField] private Image classIcon;
    [SerializeField] private TMP_Text className;
    [SerializeField] private TMP_Text classDescription;
    [SerializeField] private TMP_Text classTraitPoints;
    [SerializeField] private TMP_Text uniqueTraitsText;

    [Header("Trait Selection Panel")]
    [SerializeField] private GameObject traitSelectionPanel;
    [SerializeField] private TMP_Text currentTraitPointsText;
    [SerializeField] private Transform availableTraitsContainer;
    [SerializeField] private Transform selectedTraitsContainer;
    [SerializeField] private GameObject traitButtonPrefab;
    [SerializeField] private GameObject selectedTraitPrefab;

    [Header("Trait Detail Panel")]
    [SerializeField] private GameObject traitDetailPanel;
    [SerializeField] private Image traitDetailIcon;
    [SerializeField] private TMP_Text traitDetailName;
    [SerializeField] private TMP_Text traitDetailDescription;
    [SerializeField] private TMP_Text traitDetailCost;
    [SerializeField] private Button addTraitButton;
    [SerializeField] private Button removeTraitButton;

    [Header("Creation Panel")]
    [SerializeField] private Button createPlayerButton;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Prefab Component References")]
    [SerializeField] private PlayerStatusController prefabStatusController;
    [SerializeField] private PlayerNameComponent prefabNameComponent;
    [SerializeField] private TraitManager prefabTraitManager;

    [Header("Database References")]
    [SerializeField] private List<PlayerClass> availableClasses = new List<PlayerClass>();

    [Header("Audio Settings")]
    [SerializeField] private bool enableAudioFeedback = true;
    [SerializeField] private string buttonClickSoundName = "UI_ButtonClick";
    [SerializeField] private string classSelectSoundName = "UI_ClassSelect";
    [SerializeField] private string traitSelectSoundName = "UI_TraitSelect";
    [SerializeField] private string traitAddSoundName = "UI_TraitAdd";
    [SerializeField] private string traitRemoveSoundName = "UI_TraitRemove";
    [SerializeField] private string characterCreateSoundName = "UI_CharacterCreate";
    [SerializeField] private string errorSoundName = "UI_Error";

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Component managers
    private CharacterCreationValidator validator;
    private ClassSelectionManager classManager;
    private TraitSelectionManager traitManager;
    private PlayerCreationManager playerManager;
    private UIDisplayManager displayManager;

    // Current state
    private PlayerClass selectedClass;
    private Trait selectedTrait;
    private List<Trait> selectedTraits = new List<Trait>();
    private int currentTraitPoints;

    // UI tracking
    private Dictionary<GameObject, PlayerClass> classButtons = new Dictionary<GameObject, PlayerClass>();
    private Dictionary<GameObject, Trait> traitButtons = new Dictionary<GameObject, Trait>();
    private Dictionary<GameObject, Trait> selectedTraitButtons = new Dictionary<GameObject, Trait>();

    // Events
    public System.Action<GameObject> OnPlayerCreated;

    // Properties for component access
    public PlayerClass SelectedClass => selectedClass;
    public Trait SelectedTrait => selectedTrait;
    public List<Trait> SelectedTraits => selectedTraits;
    public int CurrentTraitPoints => currentTraitPoints;
    public bool EnableDebugLogs => enableDebugLogs;

    private void Start()
    {
        InitializeComponents();
        validator.ValidateSetup();
        SetupUI();
        PlayUISound(buttonClickSoundName); // Welcome sound
    }

    private void InitializeComponents()
    {
        validator = new CharacterCreationValidator(this, GetAllUIReferences());
        displayManager = new UIDisplayManager(this, GetDisplayReferences());
        classManager = new ClassSelectionManager(this, GetClassUIReferences(), displayManager, null); // traitManager will be set after creation
        traitManager = new TraitSelectionManager(this, GetTraitUIReferences(), displayManager);
        playerManager = new PlayerCreationManager(this, GetPlayerCreationReferences(), characterNameInput);

        // Set trait manager reference in class manager
        classManager.SetTraitManager(traitManager);
    }

    private void SetupUI()
    {
        SetupButtonListeners();
        displayManager.InitializePanelStates();
        displayManager.InitializeContainers();
        classManager.LoadAvailableClasses(availableClasses);
        displayManager.UpdateCreateButtonState();
    }

    private void SetupButtonListeners()
    {
        if (addTraitButton != null)
            addTraitButton.onClick.AddListener(() => {
                PlayUISound(traitAddSoundName);
                traitManager.AddSelectedTrait();
            });

        if (removeTraitButton != null)
            removeTraitButton.onClick.AddListener(() => {
                PlayUISound(traitRemoveSoundName);
                traitManager.RemoveSelectedTrait();
            });

        if (createPlayerButton != null)
            createPlayerButton.onClick.AddListener(() => {
                PlayUISound(characterCreateSoundName);
                playerManager.CreatePlayer();
            });

        if (characterNameInput != null)
            characterNameInput.onValueChanged.AddListener(OnCharacterNameChanged);
    }

    // Public methods for managers to update state
    public void SetSelectedClass(PlayerClass playerClass)
    {
        selectedClass = playerClass;
        currentTraitPoints = playerClass != null ? playerClass.traitPoints : 0;
        selectedTraits.Clear();

        if (playerClass != null)
            PlayUISound(classSelectSoundName);
    }

    public void SetSelectedTrait(Trait trait)
    {
        selectedTrait = trait;
        if (trait != null)
            PlayUISound(traitSelectSoundName);
    }

    public void ModifyTraitPoints(int amount)
    {
        currentTraitPoints += amount;
    }

    public void AddTraitToSelected(Trait trait)
    {
        if (!selectedTraits.Contains(trait))
            selectedTraits.Add(trait);
    }

    public void RemoveTraitFromSelected(Trait trait)
    {
        selectedTraits.Remove(trait);
    }

    public void ClearSelectedTraits()
    {
        selectedTraits.Clear();
    }

    // Audio system integration
    public void PlayUISound(string soundName)
    {
        if (!enableAudioFeedback || string.IsNullOrEmpty(soundName)) return;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUISound(soundName);
        }
        else
        {
            DebugLogWarning("SoundManager.Instance is null - cannot play UI sound: " + soundName);
        }
    }

    public void PlayErrorSound()
    {
        PlayUISound(errorSoundName);
    }

    public void PlayButtonClickSound()
    {
        PlayUISound(buttonClickSoundName);
    }

    // UI reference getters for managers
    private CharacterCreationValidator.UIReferences GetAllUIReferences()
    {
        return new CharacterCreationValidator.UIReferences
        {
            characterNameInput = characterNameInput,
            classListContainer = classListContainer,
            classButtonPrefab = classButtonPrefab,
            traitSelectionPanel = traitSelectionPanel,
            availableTraitsContainer = availableTraitsContainer,
            selectedTraitsContainer = selectedTraitsContainer,
            createPlayerButton = createPlayerButton,
            playerPrefab = playerPrefab,
            prefabStatusController = prefabStatusController,
            prefabNameComponent = prefabNameComponent,
            prefabTraitManager = prefabTraitManager,
            availableClasses = availableClasses
        };
    }

    private ClassSelectionManager.ClassUIReferences GetClassUIReferences()
    {
        return new ClassSelectionManager.ClassUIReferences
        {
            classListContainer = classListContainer,
            classButtonPrefab = classButtonPrefab,
            classSummaryPanel = classSummaryPanel,
            classIcon = classIcon,
            className = className,
            classDescription = classDescription,
            classTraitPoints = classTraitPoints,
            uniqueTraitsText = uniqueTraitsText,
            traitSelectionPanel = traitSelectionPanel,
            classButtons = classButtons
        };
    }

    private TraitSelectionManager.TraitUIReferences GetTraitUIReferences()
    {
        return new TraitSelectionManager.TraitUIReferences
        {
            availableTraitsContainer = availableTraitsContainer,
            selectedTraitsContainer = selectedTraitsContainer,
            traitButtonPrefab = traitButtonPrefab,
            selectedTraitPrefab = selectedTraitPrefab,
            traitDetailPanel = traitDetailPanel,
            traitDetailIcon = traitDetailIcon,
            traitDetailName = traitDetailName,
            traitDetailDescription = traitDetailDescription,
            traitDetailCost = traitDetailCost,
            addTraitButton = addTraitButton,
            removeTraitButton = removeTraitButton,
            traitButtons = traitButtons,
            selectedTraitButtons = selectedTraitButtons
        };
    }

    private PlayerCreationManager.PlayerCreationReferences GetPlayerCreationReferences()
    {
        return new PlayerCreationManager.PlayerCreationReferences
        {
            playerPrefab = playerPrefab,
            spawnPoint = spawnPoint,
            prefabStatusController = prefabStatusController,
            prefabNameComponent = prefabNameComponent,
            prefabTraitManager = prefabTraitManager
        };
    }

    private UIDisplayManager.DisplayReferences GetDisplayReferences()
    {
        return new UIDisplayManager.DisplayReferences
        {
            characterNameInput = characterNameInput,
            classSummaryPanel = classSummaryPanel,
            traitSelectionPanel = traitSelectionPanel,
            traitDetailPanel = traitDetailPanel,
            createPlayerButton = createPlayerButton,
            currentTraitPointsText = currentTraitPointsText,
            availableTraitsContainer = availableTraitsContainer,
            selectedTraitsContainer = selectedTraitsContainer,
            classListContainer = classListContainer
        };
    }

    // Event handlers
    public void OnCharacterNameChanged(string newName)
    {
        displayManager.UpdateCreateButtonState();
    }

    public void OnPlayerCreatedSuccess(GameObject playerObj)
    {
        OnPlayerCreated?.Invoke(playerObj);
        gameObject.SetActive(false);
    }

    // Handle creation errors with audio feedback
    public void OnCreationError(string errorMessage)
    {
        PlayErrorSound();
        DebugLogError(errorMessage);
    }

    // Public methods for external control and testing
    public void ResetCharacterCreation()
    {
        selectedClass = null;
        selectedTrait = null;
        selectedTraits.Clear();
        currentTraitPoints = 0;

        if (characterNameInput != null)
            characterNameInput.text = "";

        displayManager.ResetAllPanels();
        displayManager.ClearAllContainers();
        displayManager.UpdateTraitPointsDisplay();
        displayManager.UpdateCreateButtonState();

        PlayButtonClickSound();
    }

    public void SetAvailableClasses(List<PlayerClass> classes)
    {
        availableClasses = classes ?? new List<PlayerClass>();
        classManager.ClearClassButtons();
        classManager.LoadAvailableClasses(availableClasses);
    }

    [ContextMenu("Test Select First Class")]
    public void TestSelectFirstClass()
    {
        if (availableClasses != null && availableClasses.Count > 0 && availableClasses[0] != null)
        {
            classManager.SelectClass(availableClasses[0]);
        }
        else
        {
            Debug.LogError("[CharacterCreationUI] No classes available for testing!");
        }
    }

    [ContextMenu("Auto-Assign Prefab References")]
    public void AutoAssignPrefabReferences()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[CharacterCreationUI] Cannot auto-assign - Player Prefab is not assigned!");
            return;
        }

        if (prefabStatusController == null)
        {
            prefabStatusController = playerPrefab.GetComponent<PlayerStatusController>();
        }

        if (prefabNameComponent == null)
        {
            prefabNameComponent = playerPrefab.GetComponent<PlayerNameComponent>();
        }

        if (prefabTraitManager == null)
        {
            prefabTraitManager = playerPrefab.GetComponent<TraitManager>();
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Validate Prefab Setup")]
    public void ValidatePrefabSetupManual()
    {
        validator.ValidatePrefabReferences();
    }

    [ContextMenu("Test Audio Integration")]
    public void TestAudioIntegration()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogError("[CharacterCreationUI] SoundManager.Instance is null! Make sure SoundManager is in the scene.");
            return;
        }

        Debug.Log("[CharacterCreationUI] Testing audio integration...");
        PlayButtonClickSound();
    }

    // Utility methods for managers
    public void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[CharacterCreationUI] {message}");
    }

    public void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[CharacterCreationUI] {message}");
    }

    public void DebugLogError(string message)
    {
        if (enableDebugLogs)
            Debug.LogError($"[CharacterCreationUI] {message}");
    }

    private void OnValidate()
    {
        // Basic validation warnings
        if (characterNameInput == null)
            Debug.LogWarning("Character Name Input Field is not assigned!");
        if (playerPrefab == null)
            Debug.LogWarning("Player Prefab is not assigned!");
        if (availableClasses == null || availableClasses.Count == 0)
            Debug.LogWarning("No available classes assigned!");
    }
}