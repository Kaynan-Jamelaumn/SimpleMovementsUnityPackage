using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// UI manager for displaying armor set information and bonuses
public class ArmorSetUIManager : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("The player's set bonus manager (found on the player when empty).")]
    [SerializeField] private ArmorSetManager armorSetManager;
    [Tooltip("The player's inventory (found on the player when empty).")]
    [SerializeField] private InventoryManager inventoryManager;
    [Tooltip("Canvas of this UI (found in the parents when empty).")]
    [SerializeField] private Canvas armorSetCanvas;
    [Tooltip("The window that is shown and hidden (Show / Hide / Toggle). Empty = the whole canvas (old behaviour: hides everything on it).")]
    [SerializeField] private GameObject windowPanel;

    [Header("UI Panels")]
    [Tooltip("Details of the selected set (name, icon, progress, pieces).")]
    [SerializeField] private GameObject setInfoPanel;
    [Tooltip("List of the sets the player is wearing pieces of.")]
    [SerializeField] private GameObject setListPanel;
    [Tooltip("Active and next bonuses of the selected set.")]
    [SerializeField] private GameObject setEffectPanel;

    [Header("Set Info Display")]
    [SerializeField, Tooltip("Name of the selected set.")] private TextMeshProUGUI setNameText;
    [SerializeField, Tooltip("Description and bonus tiers of the selected set.")] private TextMeshProUGUI setDescriptionText;
    [SerializeField, Tooltip("Icon of the selected set.")] private Image setIconImage;
    [SerializeField, Tooltip("Worn pieces / total pieces.")] private Slider setProgressSlider;
    [SerializeField, Tooltip("'2/4' text next to the progress bar.")] private TextMeshProUGUI setProgressText;

    [Header("Set Effects Display")]
    [SerializeField, Tooltip("Where the active bonuses are listed.")] private Transform activeEffectsContainer;
    [SerializeField, Tooltip("Where the bonuses not reached yet are listed.")] private Transform availableEffectsContainer;
    [SerializeField, Tooltip("One bonus entry: an Image with children 'EffectName', 'EffectDescription', 'PiecesRequired' (TextMeshPro).")] private GameObject setEffectPrefab;

    [Header("Set List Display")]
    [SerializeField, Tooltip("Where the set entries are listed.")] private Transform setListContainer;
    [SerializeField, Tooltip("One set entry: a Button + Image with children 'SetName', 'Progress', 'Status' (TextMeshPro).")] private GameObject setListItemPrefab;

    [Header("Equipment Slots")]
    [SerializeField, Tooltip("Where the set's piece slots are shown.")] private Transform equipmentSlotsContainer;
    [SerializeField, Tooltip("One piece slot: an Image with children 'Icon' (Image) and 'Name' (TextMeshPro).")] private GameObject equipmentSlotPrefab;

    [Header("Audio")]
    [SerializeField, Tooltip("Plays the sounds below (found on this object when empty).")] private AudioSource uiAudioSource;
    [SerializeField, Tooltip("Played when a set becomes complete.")] private AudioClip setCompleteSound;
    [SerializeField, Tooltip("Played when a set bonus activates.")] private AudioClip effectActivatedSound;

    [Header("Settings")]
    [SerializeField, Tooltip("Open the window on the set that was just completed.")] private bool showUIOnSetCompletion = true;
    [SerializeField, Tooltip("Seconds before a window opened by a completion closes again (0 = stays open).")] private float autoHideDelay = 5f;
    [SerializeField, Tooltip("Log set completions and bonus activations to the Console.")] private bool enableSetNotifications = true;

    // Current state
    private ArmorSet currentlyDisplayedSet;
    private List<GameObject> activeEffectItems = new List<GameObject>();
    private List<GameObject> setListItems = new List<GameObject>();
    private Dictionary<ArmorSlotType, GameObject> equipmentSlots = new Dictionary<ArmorSlotType, GameObject>();

    // Events
    public System.Action<ArmorSet> OnSetSelected;
    public System.Action OnUIOpened;
    public System.Action OnUIClosed;

    private void Awake()
    {
        ValidateComponents();
        InitializeUI();
    }

    private void Start()
    {
        SubscribeToEvents();
        CreateEquipmentSlots();
        RefreshSetList();

        if (setInfoPanel != null)
            setInfoPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void ValidateComponents()
    {
        // The player this UI belongs to first (the UI usually sits on the player's canvas), the scene last.
        if (armorSetManager == null)
            armorSetManager = GetComponentInParent<ArmorSetManager>(true);
        if (armorSetManager == null && GetComponentInParent<PlayerStatusController>(true) is PlayerStatusController ps)
            armorSetManager = ps.GetComponentInChildren<ArmorSetManager>(true);
        if (armorSetManager == null)
            armorSetManager = InventoryUtils.OnlyInstance<ArmorSetManager>(); // never another player's

        if (inventoryManager == null)
            inventoryManager = GetComponentInParent<InventoryManager>(true);
        if (inventoryManager == null && transform.root.GetComponentInChildren<InventoryManager>(true) is InventoryManager im)
            inventoryManager = im;
        if (inventoryManager == null)
            inventoryManager = InventoryUtils.OnlyInstance<InventoryManager>();

        if (uiAudioSource == null)
            uiAudioSource = GetComponent<AudioSource>();

        if (armorSetCanvas == null)
            armorSetCanvas = GetComponentInParent<Canvas>();
    }

    private void InitializeUI()
    {
        // Initialize UI panels (the window opens with the Sets button / key, or when a set is completed)
        if (windowPanel != null) windowPanel.SetActive(false);
        if (setInfoPanel != null) setInfoPanel.SetActive(false);
        if (setListPanel != null) setListPanel.SetActive(true);
        if (setEffectPanel != null) setEffectPanel.SetActive(false);

        // Initialize progress slider
        if (setProgressSlider != null)
        {
            setProgressSlider.minValue = 0f;
            setProgressSlider.maxValue = 1f;
            setProgressSlider.value = 0f;
        }
    }

    private void SubscribeToEvents()
    {
        if (armorSetManager != null)
        {
            armorSetManager.OnSetPiecesChanged += HandleSetPiecesChanged;
            armorSetManager.OnSetCompleted += HandleSetCompleted;
            armorSetManager.OnSetBroken += HandleSetBroken;
            armorSetManager.OnSetEffectActivated += HandleSetEffectActivated;
            armorSetManager.OnSetEffectDeactivated += HandleSetEffectDeactivated;
            armorSetManager.SetsChanged += HandleSetsChanged;
        }
    }

    private void HandleSetsChanged()
    {
        if (IsVisible)
            RefreshAll();
    }

    private void UnsubscribeFromEvents()
    {
        if (armorSetManager != null)
        {
            armorSetManager.OnSetPiecesChanged -= HandleSetPiecesChanged;
            armorSetManager.OnSetCompleted -= HandleSetCompleted;
            armorSetManager.OnSetBroken -= HandleSetBroken;
            armorSetManager.OnSetEffectActivated -= HandleSetEffectActivated;
            armorSetManager.OnSetEffectDeactivated -= HandleSetEffectDeactivated;
            armorSetManager.SetsChanged -= HandleSetsChanged;
        }
    }

    // Event handlers - Fixed signatures
    private void HandleSetPiecesChanged(ArmorSet armorSet, int newCount)
    {
        RefreshSetList();

        if (currentlyDisplayedSet == armorSet)
        {
            UpdateSetInfo();
            UpdateSetEffects();
        }

        UpdateEquipmentSlots();
    }

    private void HandleSetCompleted(ArmorSet armorSet, bool isComplete)
    {
        if (isComplete)
        {
            if (enableSetNotifications)
            {
                ShowSetCompletionNotification(armorSet);
            }

            if (showUIOnSetCompletion)
            {
                bool wasVisible = IsVisible;
                currentlyDisplayedSet = armorSet;
                ShowUI();
                if (!wasVisible)
                    AutoHideUI();
            }

            PlaySound(setCompleteSound);
        }
        else
        {
            // Handle when set is no longer complete
            RefreshSetList();

            if (currentlyDisplayedSet == armorSet)
            {
                UpdateSetInfo();
                UpdateSetEffects();
            }
        }
    }

    private void HandleSetBroken(ArmorSet armorSet)
    {
        RefreshSetList();

        if (currentlyDisplayedSet == armorSet)
        {
            UpdateSetInfo();
            UpdateSetEffects();
        }
    }

    private void HandleSetEffectActivated(ArmorSetEffect effect)
    {
        PlaySound(effectActivatedSound);

        if (enableSetNotifications)
        {
            ShowEffectActivationNotification(effect);
        }
    }

    private void HandleSetEffectDeactivated(ArmorSetEffect effect)
    {
        // Update UI if necessary
        if (currentlyDisplayedSet != null)
        {
            UpdateSetEffects();
        }
    }

    // Public API
    /// <summary>True while the window is shown.</summary>
    public bool IsVisible => windowPanel != null ? windowPanel.activeInHierarchy : armorSetCanvas != null && armorSetCanvas.gameObject.activeInHierarchy;

    public void ShowUI()
    {
        CancelInvoke(nameof(HideUI));
        if (windowPanel != null)
            windowPanel.SetActive(true);
        else if (armorSetCanvas != null)
            armorSetCanvas.gameObject.SetActive(true);

        // Show the first worn set when nothing is selected yet.
        RefreshSetList();
        if (currentlyDisplayedSet == null && armorSetManager != null)
        {
            var sets = armorSetManager.GetActiveSets();
            if (sets.Count > 0) currentlyDisplayedSet = sets[0];
        }
        if (currentlyDisplayedSet != null)
            DisplaySet(currentlyDisplayedSet);
        else if (setInfoPanel != null)
            setInfoPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None; // the window is clicked with the mouse
        Cursor.visible = true;
        OnUIOpened?.Invoke();
    }

    public void HideUI()
    {
        CancelInvoke(nameof(HideUI));
        if (windowPanel != null)
            windowPanel.SetActive(false);
        else if (armorSetCanvas != null)
            armorSetCanvas.gameObject.SetActive(false);

        if (inventoryManager == null || !inventoryManager.IsInventoryOpened)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        OnUIClosed?.Invoke();
    }

    public void ToggleUI()
    {
        if (IsVisible) HideUI();
        else ShowUI();
    }

    public void DisplaySet(ArmorSet armorSet)
    {
        currentlyDisplayedSet = armorSet;

        if (setInfoPanel != null)
            setInfoPanel.SetActive(true);
        if (setEffectPanel != null)
            setEffectPanel.SetActive(true); // hidden at start; it was never shown again

        UpdateSetInfo();
        UpdateSetEffects();
        UpdateEquipmentSlots();

        OnSetSelected?.Invoke(armorSet);
    }

    public void RefreshAll()
    {
        RefreshSetList();
        UpdateSetInfo();
        UpdateSetEffects();
        UpdateEquipmentSlots();
    }

    // UI Update Methods
    private void UpdateSetInfo()
    {
        if (currentlyDisplayedSet == null) return;

        int equippedCount = armorSetManager?.GetEquippedPiecesCount(currentlyDisplayedSet) ?? 0;
        int totalPieces = currentlyDisplayedSet.SetPieces.Count;

        // Update text elements
        if (setNameText != null)
            setNameText.text = currentlyDisplayedSet.SetName;

        if (setDescriptionText != null)
            setDescriptionText.text = currentlyDisplayedSet.GetFormattedSetInfo(equippedCount);

        if (setIconImage != null && currentlyDisplayedSet.SetIcon != null)
            setIconImage.sprite = currentlyDisplayedSet.SetIcon;

        // Update progress
        if (setProgressSlider != null)
        {
            float progress = totalPieces > 0 ? (float)equippedCount / totalPieces : 0f;
            setProgressSlider.value = progress;
        }

        if (setProgressText != null)
            setProgressText.text = $"{equippedCount}/{totalPieces}";
    }

    private void UpdateSetEffects()
    {
        ClearEffectContainers();

        if (currentlyDisplayedSet == null) return;

        int equippedCount = armorSetManager?.GetEquippedPiecesCount(currentlyDisplayedSet) ?? 0;
        var activeEffects = currentlyDisplayedSet.GetActiveEffects(equippedCount);
        var allEffects = currentlyDisplayedSet.SetEffects;

        // Display active effects
        foreach (var effect in activeEffects)
        {
            CreateEffectItem(effect, activeEffectsContainer, true);
        }

        // Display available effects
        var availableEffects = allEffects.Where(e => !activeEffects.Contains(e));
        foreach (var effect in availableEffects)
        {
            CreateEffectItem(effect, availableEffectsContainer, false);
        }
    }

    private void RefreshSetList()
    {
        ClearSetListItems();

        if (armorSetManager == null) return;

        var activeSets = armorSetManager.GetActiveSets();

        foreach (var armorSet in activeSets)
        {
            CreateSetListItem(armorSet);
        }
    }

    private void UpdateEquipmentSlots()
    {
        if (currentlyDisplayedSet == null) return;

        var equippedArmor = ArmorSetUtils.GetEquippedArmor(inventoryManager);
        var setArmor = equippedArmor.Where(armor => armor.BelongsToSet == currentlyDisplayedSet).ToList();

        foreach (var kvp in equipmentSlots)
        {
            var slotType = kvp.Key;
            var slotObj = kvp.Value;

            // Only the slots this set has pieces for.
            bool used = currentlyDisplayedSet.SetPieces.Any(p => p != null && p.ArmorSlotType == slotType);
            if (slotObj != null && slotObj.activeSelf != used)
                slotObj.SetActive(used);
            var equippedPiece = setArmor.FirstOrDefault(armor => armor.ArmorSlotType == slotType);
            UpdateEquipmentSlot(slotObj, equippedPiece);
        }
    }

    // UI Creation Methods
    private void CreateEquipmentSlots()
    {
        if (equipmentSlotsContainer == null || equipmentSlotPrefab == null) return;

        equipmentSlots.Clear();

        foreach (ArmorSlotType slotType in System.Enum.GetValues(typeof(ArmorSlotType)))
        {
            var slotObj = Instantiate(equipmentSlotPrefab, equipmentSlotsContainer);
            equipmentSlots[slotType] = slotObj;

            // Setup slot
            var slotText = slotObj.GetComponentInChildren<TextMeshProUGUI>();
            if (slotText != null)
                slotText.text = slotType.ToString();
        }
    }

    private void CreateEffectItem(ArmorSetEffect effect, Transform container, bool isActive)
    {
        if (container == null || setEffectPrefab == null) return;

        var effectObj = Instantiate(setEffectPrefab, container);
        activeEffectItems.Add(effectObj);

        // Setup effect display
        var nameText = effectObj.transform.Find("EffectName")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = effect.effectName;

        var descText = effectObj.transform.Find("EffectDescription")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
            descText.text = effect.GetFormattedDescription();

        var piecesText = effectObj.transform.Find("PiecesRequired")?.GetComponent<TextMeshProUGUI>();
        if (piecesText != null)
            piecesText.text = $"Requires {effect.piecesRequired} pieces";

        // Visual state
        var image = effectObj.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? new Color(0.3f, 0.75f, 0.4f, 0.35f) : new Color(1f, 1f, 1f, 0.06f);
        }
    }

    private void CreateSetListItem(ArmorSet armorSet)
    {
        if (setListContainer == null || setListItemPrefab == null) return;

        var listItemObj = Instantiate(setListItemPrefab, setListContainer);
        setListItems.Add(listItemObj);

        int equippedCount = armorSetManager.GetEquippedPiecesCount(armorSet);
        int totalPieces = armorSet.SetPieces.Count;
        bool isComplete = armorSetManager.IsSetComplete(armorSet);

        // Setup list item
        var nameText = listItemObj.transform.Find("SetName")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = armorSet.SetName;

        var progressText = listItemObj.transform.Find("Progress")?.GetComponent<TextMeshProUGUI>();
        if (progressText != null)
            progressText.text = $"{equippedCount}/{totalPieces}";

        var statusText = listItemObj.transform.Find("Status")?.GetComponent<TextMeshProUGUI>();
        if (statusText != null)
            statusText.text = isComplete ? "Complete" : "Incomplete";

        // Add button functionality
        var button = listItemObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => DisplaySet(armorSet));
        }

        // Visual state
        var image = listItemObj.GetComponent<Image>();
        if (image != null)
        {
            image.color = isComplete ? new Color(0.3f, 0.75f, 0.4f, 0.35f) : new Color(1f, 1f, 1f, 0.1f);
        }
    }

    private void UpdateEquipmentSlot(GameObject slotObj, ArmorSO equippedArmor)
    {
        if (slotObj == null) return;

        var iconImage = slotObj.transform.Find("Icon")?.GetComponent<Image>();
        var nameText = slotObj.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();

        if (equippedArmor != null)
        {
            if (iconImage != null && equippedArmor.Icon != null)
                iconImage.sprite = equippedArmor.Icon;

            if (iconImage != null)
                iconImage.enabled = equippedArmor.Icon != null;
            if (nameText != null)
                nameText.text = string.IsNullOrEmpty(equippedArmor.Name) ? equippedArmor.name : equippedArmor.Name;

            slotObj.GetComponent<Image>().color = new Color(0.3f, 0.75f, 0.4f, 0.5f);
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (nameText != null)
                nameText.text = "Empty";

            slotObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        }
    }

    // Notification Methods
    private void ShowSetCompletionNotification(ArmorSet armorSet)
    {
        // Simple notification - you can enhance this with proper notification UI
        Debug.Log($"Set Completed: {armorSet.SetName}!");

        // You could implement a proper notification system here
        // For example, showing a popup or toast message
    }

    private void ShowEffectActivationNotification(ArmorSetEffect effect)
    {
        Debug.Log($"Set Effect Activated: {effect.effectName}!");
    }

    // Cleanup Methods
    private void ClearEffectContainers()
    {
        foreach (var item in activeEffectItems)
        {
            if (item != null) Destroy(item);
        }
        activeEffectItems.Clear();
    }

    private void ClearSetListItems()
    {
        foreach (var item in setListItems)
        {
            if (item != null) Destroy(item);
        }
        setListItems.Clear();
    }

    // Utility Methods
    private void PlaySound(AudioClip clip)
    {
        if (uiAudioSource != null && clip != null)
        {
            uiAudioSource.PlayOneShot(clip);
        }
    }

    // Auto-hide functionality
    private void AutoHideUI()
    {
        if (autoHideDelay > 0)
        {
            Invoke(nameof(HideUI), autoHideDelay);
        }
    }

    // Debug methods
    [ContextMenu("Force Refresh UI")]
    public void ForceRefreshUI()
    {
        RefreshAll();
    }

    [ContextMenu("Show Test Set")]
    public void ShowTestSet()
    {
        if (armorSetManager != null)
        {
            var activeSets = armorSetManager.GetActiveSets();
            if (activeSets.Count > 0)
            {
                DisplaySet(activeSets[0]);
                ShowUI();
            }
        }
    }
}