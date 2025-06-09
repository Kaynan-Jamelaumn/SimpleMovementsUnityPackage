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
    private SerializedProperty inventoryParentProp;
    private SerializedProperty equippableInventoryProp;
    private SerializedProperty storageParentProp;
    private SerializedProperty itemInfoProp;

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
    public SerializedProperty InventoryParentProp => inventoryParentProp;
    public SerializedProperty EquippableInventoryProp => equippableInventoryProp;
    public SerializedProperty StorageParentProp => storageParentProp;
    public SerializedProperty ItemInfoProp => itemInfoProp;

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
        inventoryParentProp = serializedObject.FindProperty("inventoryParent");
        equippableInventoryProp = serializedObject.FindProperty("equippableInventory");
        storageParentProp = serializedObject.FindProperty("storageParent");
        itemInfoProp = serializedObject.FindProperty("itemInfo");

        // Debug missing properties with more detail
        LogPropertyStatus("slotManager", slotManagerProp);
        LogPropertyStatus("uiLayoutManager", uiLayoutManagerProp);
        LogPropertyStatus("itemPrefab", itemPrefabProp);
        LogPropertyStatus("handParent", handParentProp);
        LogPropertyStatus("player", playerProp);
        LogPropertyStatus("playerStatusController", playerStatusControllerProp);
        LogPropertyStatus("weaponController", weaponControllerProp);
        LogPropertyStatus("cam", camProp);
        LogPropertyStatus("inventoryParent", inventoryParentProp);
        LogPropertyStatus("equippableInventory", equippableInventoryProp);
        LogPropertyStatus("storageParent", storageParentProp);
        LogPropertyStatus("itemInfo", itemInfoProp);
    }

    private void LogPropertyStatus(string propertyName, SerializedProperty property)
    {
        //if (property == null)
        //{
        //    Debug.LogWarning($"Could not find '{propertyName}' property in InventoryManager");
        //}
        //else
        //{
        //    Debug.Log($"Found '{propertyName}' property: {property.propertyType}");
        //}
    }

    public void DrawEnhancedProperties(InventoryEditorStyles styles)
    {
        EditorGUILayout.LabelField("📦 Core Configuration", EditorStyles.boldLabel);
        DrawEnhancedProperty(itemPrefabProp, "Item Prefab", "Prefab used to create inventory items");

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🔧 Management Components", EditorStyles.boldLabel);
        DrawSerializableClassProperty(slotManagerProp, "Slot Manager", "Manages slot creation and layout calculations");
        DrawSerializableClassProperty(uiLayoutManagerProp, "UI Layout Manager", "Handles UI layout presets and calculations");

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🎨 UI Panel References", EditorStyles.boldLabel);
        DrawEnhancedProperty(inventoryParentProp, "Inventory Parent", "Main inventory panel container");
        DrawEnhancedProperty(equippableInventoryProp, "Equipment Panel", "Equipment/gear panel container");
        DrawEnhancedProperty(storageParentProp, "Storage Panel", "Storage interaction panel container");
        DrawEnhancedProperty(handParentProp, "Hand Parent", "Transform where held items are instantiated");
        DrawEnhancedProperty(itemInfoProp, "Item Info", "UI component for displaying item information");
        DrawEnhancedProperty(camProp, "Camera", "Camera reference for world interactions");

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("👤 Player References", EditorStyles.boldLabel);
        DrawEnhancedProperty(playerProp, "Player", "Player GameObject reference");
        DrawEnhancedProperty(playerStatusControllerProp, "Player Status Controller", "Player status and stats controller");
        DrawEnhancedProperty(weaponControllerProp, "Weapon Controller", "Player weapon handling controller");
    }

    public void DrawEnhancedProperty(SerializedProperty prop, string label, string tooltip)
    {
        if (prop == null)
        {
            EditorGUILayout.HelpBox($"Property '{label}' not found!", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginHorizontal();

        // Property field
        try
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));
        }
        catch (System.Exception e)
        {
            EditorGUILayout.LabelField($"{label}: Error - {e.Message}");
        }

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
                    Debug.Log($"Auto-assignment requested for {label}");
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    public void DrawSerializableClassProperty(SerializedProperty prop, string label, string tooltip)
    {
        if (prop == null)
        {
            EditorGUILayout.HelpBox($"Property '{label}' not found!", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Header with foldout
        bool isExpanded = EditorPrefs.GetBool($"InventoryEditor_{prop.name}_Expanded", false);
        EditorGUILayout.BeginHorizontal();

        isExpanded = EditorGUILayout.Foldout(isExpanded, new GUIContent(label, tooltip), true);
        EditorPrefs.SetBool($"InventoryEditor_{prop.name}_Expanded", isExpanded);

        // Status indicator
        bool isConfigured = IsSerializedPropertyConfigured(prop);
        GUI.color = isConfigured ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f);
        EditorGUILayout.LabelField(isConfigured ? "✓" : "○", GUILayout.Width(20));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        // Draw nested properties if expanded
        if (isExpanded)
        {
            EditorGUI.indentLevel++;
            try
            {
                EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
            }
            catch (System.Exception e)
            {
                EditorGUILayout.LabelField($"Error drawing property: {e.Message}");
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    public bool IsSerializedPropertyConfigured(SerializedProperty prop)
    {
        if (prop == null) return false;

        // For object references, check if the value is not null
        if (prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            return prop.objectReferenceValue != null;
        }

        // For other types (like serialized classes), check if property exists and has meaningful data
        if (prop.propertyType == SerializedPropertyType.Generic)
        {
            // For serializable classes, consider them configured if the property exists and has a valid path
            return !string.IsNullOrEmpty(prop.propertyPath) && prop.serializedObject != null;
        }

        // For basic types, check if they have valid values
        return !string.IsNullOrEmpty(prop.propertyPath);
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

    // property drawing for specific types
    public void DrawPropertySection(string sectionName, System.Action drawProperties)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField(sectionName, EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        drawProperties?.Invoke();
        EditorGUILayout.EndVertical();
    }

    // Utility method to check if all required properties are assigned
    public float CalculateCompleteness()
    {
        var requiredProps = new SerializedProperty[]
        {
            itemPrefabProp, handParentProp, playerProp,
            playerStatusControllerProp, camProp
        };

        int assignedCount = 0;
        int totalCount = requiredProps.Length;

        foreach (var prop in requiredProps)
        {
            if (IsSerializedPropertyConfigured(prop))
                assignedCount++;
        }

        // Add points for serializable classes being present (not null)
        if (slotManagerProp != null)
        {
            assignedCount++;
            totalCount++;
        }
        if (uiLayoutManagerProp != null)
        {
            assignedCount++;
            totalCount++;
        }

        return totalCount > 0 ? (float)assignedCount / totalCount : 0f;
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