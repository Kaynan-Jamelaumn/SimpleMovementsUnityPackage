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

    [Header("Races (optional)")]
    [Tooltip("Races the player can pick (Character Archetype assets). Empty = no race step (classes only).")]
    [SerializeField] private List<CharacterArchetype> availableRaces = new List<CharacterArchetype>();
    [Tooltip("A race must be picked before the character can be created.")]
    [SerializeField] private bool raceRequired = true;
    [Tooltip("Where the race buttons go (the Class Button Prefab is used for them).")]
    [SerializeField] private Transform raceListContainer;
    [SerializeField] private TMP_Text raceDescription;
    [Tooltip("Optional height slider (limited to the race's height range).")]
    [SerializeField] private Slider heightSlider;
    [SerializeField] private TMP_Text heightText;
    [Tooltip("Every trait, for races / classes without their own trait list (the class's list is used when it has one).")]
    [SerializeField] private List<Trait> allTraits = new List<Trait>();
    [Tooltip("Active traits (an ability on its own key) the player may pick. Movement traits (double jump, wall climb, glide) do not count.")]
    [SerializeField, Min(0)] private int maxActiveTraits = 1;

    [Header("Audio Settings")]
    [SerializeField] private bool enableAudioFeedback = true;
    [SerializeField] private string buttonClickSoundName = "UI_ButtonClick";
    [SerializeField] private string classSelectSoundName = "UI_ClassSelect";
    [SerializeField] private string traitSelectSoundName = "UI_TraitSelect";
    [SerializeField] private string traitAddSoundName = "UI_TraitAdd";
    [SerializeField] private string traitRemoveSoundName = "UI_TraitRemove";
    [SerializeField] private string characterCreateSoundName = "UI_CharacterCreate";
    [SerializeField] private string errorSoundName = "UI_Error";

    [Header("Cursor & Flow")]
    [Tooltip("Show the mouse cursor while this screen is open, and lock it for the game once the character is created.")]
    [SerializeField] private bool manageCursor = true;
    [Tooltip("Objects shown only while choosing (e.g. a preview camera); turned off when the character is created.")]
    [SerializeField] private List<GameObject> hideOnCreate = new List<GameObject>();

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Component managers
    private CharacterCreationValidator validator;
    private ClassSelectionManager classManager;
    private TraitSelectionManager traitManager;
    private PlayerCreationManager playerManager;
    private UIDisplayManager displayManager;
    private RaceSelectionManager raceManager;

    // Current state
    private PlayerClass selectedClass;
    private CharacterArchetype selectedRace;
    private float selectedHeight;
    private readonly List<CharacterArchetype> archetypes = new List<CharacterArchetype>();
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
    public CharacterArchetype SelectedRace => selectedRace;
    /// <summary>Picked height in metres (0 = the race's default).</summary>
    public float SelectedHeight => selectedHeight;
    public bool HasRaces => availableRaces != null && availableRaces.Exists(r => r != null);
    public bool RaceRequired => raceRequired && HasRaces;
    public List<Trait> AllTraits => allTraits;
    public int MaxActiveTraits => maxActiveTraits;

    /// <summary>The archetypes the character will have: the race and the class's archetype.</summary>
    public IList<CharacterArchetype> Archetypes
    {
        get
        {
            archetypes.Clear();
            if (selectedRace != null) archetypes.Add(selectedRace);
            if (selectedClass != null && selectedClass.archetype != null && !archetypes.Contains(selectedClass.archetype))
                archetypes.Add(selectedClass.archetype);
            return archetypes;
        }
    }

    /// <summary>What a trait costs for the current class and race.</summary>
    public int TraitCost(Trait trait) => TraitRules.Cost(trait, selectedClass, Archetypes);

    /// <summary>Trait points before any trait is picked: the class's points plus the race / class archetype bonuses.</summary>
    public int StartingTraitPoints => (selectedClass != null ? selectedClass.traitPoints : 0) + TraitRules.BonusPoints(Archetypes);

    private void Start()
    {
        if (manageCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        PauseMenuManager.Instance?.SetMainMenuState(true); // no pause menu while creating the character
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
        raceManager = new RaceSelectionManager(this, new RaceSelectionManager.RaceUIReferences
        {
            raceListContainer = raceListContainer,
            raceButtonPrefab = classButtonPrefab,
            raceDescription = raceDescription,
            heightSlider = heightSlider,
            heightText = heightText,
        });

        // Set trait manager reference in class manager
        classManager.SetTraitManager(traitManager);
    }

    private void SetupUI()
    {
        SetupButtonListeners();
        displayManager.InitializePanelStates();
        displayManager.InitializeContainers();
        classManager.LoadAvailableClasses(availableClasses);
        raceManager.LoadRaces(availableRaces);
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
        currentTraitPoints = StartingTraitPoints;
        selectedTraits.Clear();

        if (playerClass != null)
            PlayUISound(classSelectSoundName);
    }

    /// <summary>
    /// Picks a race. Refused (false) when the race cannot be combined with the chosen class. The chosen traits are reset,
    /// because what is allowed and what it costs depend on the race.
    /// </summary>
    public bool TrySelectRace(CharacterArchetype race)
    {
        if (race != null && selectedClass != null && selectedClass.archetype != null && !race.IsCompatibleWith(selectedClass.archetype))
        {
            OnCreationError($"{race.Name} cannot be a {selectedClass.GetClassName()}.");
            return false;
        }
        selectedRace = race;
        selectedHeight = race != null && race.setsHeight ? race.ClampHeight(selectedHeight > 0f ? selectedHeight : race.defaultHeight) : 0f;
        PlayUISound(classSelectSoundName);
        if (selectedClass != null)
            traitManager.ClearSelectedTraits(); // resets the points with the new race and reloads the list
        displayManager.UpdateTraitPointsDisplay();
        displayManager.UpdateCreateButtonState();
        DebugLog($"Selected race: {(race != null ? race.Name : "none")}");
        return true;
    }

    public void SetSelectedHeight(float metres)
    {
        selectedHeight = selectedRace != null ? selectedRace.ClampHeight(metres) : Mathf.Max(0f, metres);
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
        PauseMenuManager.Instance?.SetMainMenuState(false);
        foreach (GameObject go in hideOnCreate)
            if (go != null) go.SetActive(false);
        if (manageCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
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
        selectedRace = null;
        selectedHeight = 0f;
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
        if (classManager == null)
        {
            Debug.LogWarning("[CharacterCreationUI] Test First Class works in Play Mode (the screen is set up when Play starts).");
            return;
        }
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
        // Works in edit mode too: the validator is otherwise only created when Play starts.
        if (validator == null)
            validator = new CharacterCreationValidator(this, GetAllUIReferences());
        validator.ValidateSetup();
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