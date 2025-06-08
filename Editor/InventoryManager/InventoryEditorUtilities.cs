#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;

public class InventoryEditorUtilities
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;

    // Serialized Properties
    private SerializedProperty slotManagerProp;
    private SerializedProperty uiLayoutManagerProp;
    private SerializedProperty itemPrefabProp;
    private SerializedProperty handParentProp;
    private SerializedProperty playerProp;
    private SerializedProperty playerStatusControllerProp;
    private SerializedProperty weaponControllerProp;
    private SerializedProperty camProp;

    public InventoryManager InventoryManager => inventoryManager;
    public SerializedObject SerializedObject => serializedObject;

    // Property accessors
    public SerializedProperty SlotManagerProp => slotManagerProp;
    public SerializedProperty UILayoutManagerProp => uiLayoutManagerProp;
    public SerializedProperty ItemPrefabProp => itemPrefabProp;
    public SerializedProperty HandParentProp => handParentProp;
    public SerializedProperty PlayerProp => playerProp;
    public SerializedProperty PlayerStatusControllerProp => playerStatusControllerProp;
    public SerializedProperty WeaponControllerProp => weaponControllerProp;
    public SerializedProperty CamProp => camProp;

    public InventoryEditorUtilities(InventoryManager manager, SerializedObject serialized)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        FindSerializedProperties();
    }

    private void FindSerializedProperties()
    {
        slotManagerProp = serializedObject.FindProperty("slotManager");
        uiLayoutManagerProp = serializedObject.FindProperty("uiLayoutManager");
        itemPrefabProp = serializedObject.FindProperty("itemPrefab");
        handParentProp = serializedObject.FindProperty("handParent");
        playerProp = serializedObject.FindProperty("player");
        playerStatusControllerProp = serializedObject.FindProperty("playerStatusController");
        weaponControllerProp = serializedObject.FindProperty("weaponController");
        camProp = serializedObject.FindProperty("cam");

        // Debug missing properties
        if (slotManagerProp == null) Debug.LogWarning("Could not find 'slotManager' property");
        if (uiLayoutManagerProp == null) Debug.LogWarning("Could not find 'uiLayoutManager' property");
        if (itemPrefabProp == null) Debug.LogWarning("Could not find 'itemPrefab' property");
        if (handParentProp == null) Debug.LogWarning("Could not find 'handParent' property");
        if (playerProp == null) Debug.LogWarning("Could not find 'player' property");
        if (playerStatusControllerProp == null) Debug.LogWarning("Could not find 'playerStatusController' property");
        if (weaponControllerProp == null) Debug.LogWarning("Could not find 'weaponController' property");
        if (camProp == null) Debug.LogWarning("Could not find 'cam' property");
    }

    public void DrawEnhancedProperties(InventoryEditorStyles styles)
    {
        DrawEnhancedProperty(slotManagerProp, "Slot Manager", "Manages slot creation and layout");
        DrawEnhancedProperty(uiLayoutManagerProp, "UI Layout Manager", "Handles UI layout presets and calculations");
        DrawEnhancedProperty(itemPrefabProp, "Item Prefab", "Prefab used to create inventory items");
        DrawEnhancedProperty(handParentProp, "Hand Parent", "Transform where held items are instantiated");
        DrawEnhancedProperty(playerProp, "Player", "Player GameObject reference");
        DrawEnhancedProperty(playerStatusControllerProp, "Player Status Controller", "Player status and stats controller");
        DrawEnhancedProperty(weaponControllerProp, "Weapon Controller", "Player weapon handling controller");
        DrawEnhancedProperty(camProp, "Camera", "Camera reference for world interactions");
    }

    public void DrawEnhancedProperty(SerializedProperty prop, string label, string tooltip)
    {
        if (prop == null) return;

        EditorGUILayout.BeginHorizontal();

        // Property field
        EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));

        // Status indicator
        bool isAssigned = IsSerializedPropertyConfigured(prop);

        if (isAssigned)
        {
            GUI.color = new Color(0.2f, 0.8f, 0.2f);
            EditorGUILayout.LabelField("✓", GUILayout.Width(20));
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(0.8f, 0.2f, 0.2f);
            EditorGUILayout.LabelField("✗", GUILayout.Width(20));
            GUI.color = Color.white;

            // Auto-assign button only for object references
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (GUILayout.Button("Auto", GUILayout.Width(40)))
                {
                    // This will be handled by the auto-setup module
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    public bool IsSerializedPropertyConfigured(SerializedProperty prop)
    {
        if (prop == null) return false;

        // For object references, check if the value is not null
        if (prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            return prop.objectReferenceValue != null;
        }

        // For other types (like serialized classes), check if property exists
        return prop.propertyPath != null;
    }

    public SlotManager GetSlotManager()
    {
        try
        {
            // Use reflection to get the private slotManager field
            var field = typeof(InventoryManager).GetField("slotManager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(inventoryManager) as SlotManager;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not access SlotManager via reflection: {e.Message}");
            return null;
        }
    }

    public void DrawDetectionCategory(string categoryName, int count, System.Action drawContent)
    {
        if (count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{categoryName} ({count})", EditorStyles.boldLabel);
            drawContent();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
    }
}

// Helper attribute for inventory components
public class InventoryComponentAttribute : System.Attribute
{
    public string Description { get; }
    public bool Required { get; }

    public InventoryComponentAttribute(string description, bool required = true)
    {
        Description = description;
        Required = required;
    }
}

// Helper class for editor preferences
public static class InventoryEditorPrefs
{
    public const string AUTO_DETECT_KEY = "InventoryEditor_AutoDetect";
    public const string SHOW_TOOLTIPS_KEY = "InventoryEditor_ShowTooltips";
    public const string PERFORMANCE_TRACKING_KEY = "InventoryEditor_PerformanceTracking";
    public const string LAST_SETUP_KEY = "InventoryManager_LastSetup";

    public static bool AutoDetectEnabled
    {
        get => EditorPrefs.GetBool(AUTO_DETECT_KEY, true);
        set => EditorPrefs.SetBool(AUTO_DETECT_KEY, value);
    }

    public static bool ShowTooltips
    {
        get => EditorPrefs.GetBool(SHOW_TOOLTIPS_KEY, true);
        set => EditorPrefs.SetBool(SHOW_TOOLTIPS_KEY, value);
    }

    public static bool PerformanceTrackingEnabled
    {
        get => EditorPrefs.GetBool(PERFORMANCE_TRACKING_KEY, false);
        set => EditorPrefs.SetBool(PERFORMANCE_TRACKING_KEY, value);
    }
}
#endif