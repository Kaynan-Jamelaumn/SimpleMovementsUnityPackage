using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// UI manager for displaying armor set information and bonuses
public class ArmorSetUIManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private ArmorSetManager armorSetManager;
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private Canvas armorSetCanvas;

    [Header("UI Panels")]
    [SerializeField] private GameObject setInfoPanel;
    [SerializeField] private GameObject setListPanel;
    [SerializeField] private GameObject setEffectPanel;

    [Header("Set Info Display")]
    [SerializeField] private TextMeshProUGUI setNameText;
    [SerializeField] private TextMeshProUGUI setDescriptionText;
    [SerializeField] private Image setIconImage;
    [SerializeField] private Slider setProgressSlider;
    [SerializeField] private TextMeshProUGUI setProgressText;

    [Header("Set Effects Display")]
    [SerializeField] private Transform activeEffectsContainer;
    [SerializeField] private Transform availableEffectsContainer;
    [SerializeField] private GameObject setEffectPrefab;

    [Header("Set List Display")]
    [SerializeField] private Transform setListContainer;
    [SerializeField] private GameObject setListItemPrefab;

    [Header("Equipment Slots")]
    [SerializeField] private Transform equipmentSlotsContainer;
    [SerializeField] private GameObject equipmentSlotPrefab;

    [Header("Audio")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip setCompleteSound;
    [SerializeField] private AudioClip effectActivatedSound;

    [Header("Settings")]
    [SerializeField] private bool showUIOnSetCompletion = true;
    [SerializeField] private float autoHideDelay = 5f;
    [SerializeField] private bool enableSetNotifications = true;

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
        if (armorSetManager == null)
            armorSetManager = Object.FindFirstObjectByType<ArmorSetManager>();

        if (inventoryManager == null)
            inventoryManager = Object.FindFirstObjectByType<InventoryManager>();

        if (uiAudioSource == null)
            uiAudioSource = GetComponent<AudioSource>();

        if (armorSetCanvas == null)
            armorSetCanvas = GetComponentInParent<Canvas>();
    }

    private void InitializeUI()
    {
        // Initialize UI panels
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
            armorSetManager.OnSetPiecesChanged += OnSetPiecesChanged;
            armorSetManager.OnSetCompleted += OnSetCompleted;
            armorSetManager.OnSetBroken += OnSetBroken;
            armorSetManager.OnSetEffectActivated += OnSetEffectActivated;
            armorSetManager.OnSetEffectDeactivated += OnSetEffectDeactivated;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (armorSetManager != null)
        {
            armorSetManager.OnSetPiecesChanged -= OnSetPiecesChanged;
            armorSetManager.OnSetCompleted -= OnSetCompleted;
            armorSetManager.OnSetBroken -= OnSetBroken;
            armorSetManager.OnSetEffectActivated -= OnSetEffectActivated;
            armorSetManager.OnSetEffectDeactivated -= OnSetEffectDeactivated;
        }
    }

    // Event handlers
    private void OnSetPiecesChanged(ArmorSet armorSet, int newCount)
    {
        RefreshSetList();

        if (currentlyDisplayedSet == armorSet)
        {
            UpdateSetInfo();
            UpdateSetEffects();
        }

        UpdateEquipmentSlots();
    }

    private void OnSetCompleted(ArmorSet armorSet)
    {
        if (enableSetNotifications)
        {
            ShowSetCompletionNotification(armorSet);
        }

        if (showUIOnSetCompletion)
        {
            DisplaySet(armorSet);
            ShowUI();
        }

        PlaySound(setCompleteSound);
    }

    private void OnSetBroken(ArmorSet armorSet)
    {
        RefreshSetList();

        if (currentlyDisplayedSet == armorSet)
        {
            UpdateSetInfo();
            UpdateSetEffects();
        }
    }

    private void OnSetEffectActivated(ArmorSetEffect effect)
    {
        PlaySound(effectActivatedSound);

        if (enableSetNotifications)
        {
            ShowEffectActivationNotification(effect);
        }
    }

    private void OnSetEffectDeactivated(ArmorSetEffect effect)
    {
        // Update UI if necessary
        if (currentlyDisplayedSet != null)
        {
            UpdateSetEffects();
        }
    }

    // Public API
    public void ShowUI()
    {
        if (armorSetCanvas != null)
            armorSetCanvas.gameObject.SetActive(true);

        OnUIOpened?.Invoke();
    }

    public void HideUI()
    {
        if (armorSetCanvas != null)
            armorSetCanvas.gameObject.SetActive(false);

        OnUIClosed?.Invoke();
    }

    public void ToggleUI()
    {
        if (armorSetCanvas != null)
        {
            bool isActive = armorSetCanvas.gameObject.activeSelf;
            if (isActive)
                HideUI();
            else
                ShowUI();
        }
    }

    public void DisplaySet(ArmorSet armorSet)
    {
        currentlyDisplayedSet = armorSet;

        if (setInfoPanel != null)
            setInfoPanel.SetActive(true);

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
            image.color = isActive ? Color.green : Color.gray;
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
            image.color = isComplete ? Color.green : Color.yellow;
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

            if (nameText != null)
                nameText.text = equippedArmor.name;

            slotObj.GetComponent<Image>().color = Color.green;
        }
        else
        {
            if (iconImage != null)
                iconImage.sprite = null;

            if (nameText != null)
                nameText.text = "Empty";

            slotObj.GetComponent<Image>().color = Color.gray;
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