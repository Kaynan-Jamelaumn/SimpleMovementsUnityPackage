#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class InventoryEditorAdvanced
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;

    public InventoryEditorAdvanced(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }

    public void DrawAdvancedSettingsSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        EditorGUILayout.LabelField("🔧 Editor Preferences", EditorStyles.boldLabel);

        bool autoDetectOnEnable = EditorGUILayout.Toggle("Auto-detect on Enable",
            EditorPrefs.GetBool("InventoryEditor_AutoDetect", true));
        EditorPrefs.SetBool("InventoryEditor_AutoDetect", autoDetectOnEnable);

        bool showHelpTooltips = EditorGUILayout.Toggle("Show Help Tooltips",
            EditorPrefs.GetBool("InventoryEditor_ShowTooltips", true));
        EditorPrefs.SetBool("InventoryEditor_ShowTooltips", showHelpTooltips);

        bool enablePerformanceTracking = EditorGUILayout.Toggle("Enable Performance Tracking",
            EditorPrefs.GetBool("InventoryEditor_PerformanceTracking", false));
        EditorPrefs.SetBool("InventoryEditor_PerformanceTracking", enablePerformanceTracking);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("📁 Project Utilities", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📦 Create Item Prefab", styles.ButtonStyle))
            CreateItemPrefabWizard();

        if (GUILayout.Button("🎨 Create UI Elements", styles.ButtonStyle))
            CreateUIElementsWizard();

        if (GUILayout.Button("📋 Export Settings", styles.ButtonStyle))
            ExportInventorySettings();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📥 Import Settings", styles.ButtonStyle))
            ImportInventorySettings();

        if (GUILayout.Button("🔄 Reset to Defaults", styles.WarningButtonStyle))
            ResetToDefaults();

        if (GUILayout.Button("💾 Save Setup", styles.SuccessButtonStyle))
            SaveCurrentSetup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📚 Quick Start", styles.ButtonStyle))
            OpenQuickStartGuide();

        if (GUILayout.Button("📖 Documentation", styles.ButtonStyle))
            ShowDocumentation();

        if (GUILayout.Button("💾 Restore Saved", styles.ButtonStyle))
            RestoreSavedSetup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void CreateItemPrefabWizard()
    {
        string prefabName = EditorUtility.SaveFilePanel(
            "Create Item Prefab",
            "Assets/Prefabs",
            "NewInventoryItem",
            "prefab");

        if (!string.IsNullOrEmpty(prefabName))
        {
            // Create a basic item prefab
            GameObject itemGO = new GameObject("InventoryItem");
            itemGO.AddComponent<InventoryItem>();
            itemGO.AddComponent<Image>();

            // Convert to prefab
            string relativePath = "Assets" + prefabName.Substring(Application.dataPath.Length);
            PrefabUtility.SaveAsPrefabAsset(itemGO, relativePath);
            GameObject.DestroyImmediate(itemGO);

            Debug.Log($"📦 Created item prefab: {relativePath}");

            // Auto-assign if item prefab is missing
            if (utils.ItemPrefabProp.objectReferenceValue == null)
            {
                utils.ItemPrefabProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    private void CreateUIElementsWizard()
    {
        if (EditorUtility.DisplayDialog("Create UI Elements",
            "This will create basic UI elements for the inventory system.\n\nContinue?",
            "Yes", "Cancel"))
        {
            CreateBasicInventoryUI();
        }
    }

    private void CreateBasicInventoryUI()
    {
        // Find or create Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create main inventory panel
        GameObject inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvas.transform, false);

        var rectTransform = inventoryPanel.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.2f, 0.2f);
        rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        var image = inventoryPanel.AddComponent<Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Create grid layout for slots
        GameObject slotsParent = new GameObject("SlotsParent");
        slotsParent.transform.SetParent(inventoryPanel.transform, false);

        var slotsRect = slotsParent.AddComponent<RectTransform>();
        slotsRect.anchorMin = Vector2.zero;
        slotsRect.anchorMax = Vector2.one;
        slotsRect.offsetMin = Vector2.zero;
        slotsRect.offsetMax = Vector2.zero;

        var gridLayout = slotsParent.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(80, 80);
        gridLayout.spacing = new Vector2(5, 5);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 5;

        Debug.Log("🎨 Basic inventory UI elements created!");

        // Try to auto-assign references
        var detection = new InventoryEditorComponentDetection(inventoryManager, serializedObject, utils);
        detection.RunAutoDetection();
    }

    private void ExportInventorySettings()
    {
        string settingsJson = JsonUtility.ToJson(inventoryManager, true);
        string path = EditorUtility.SaveFilePanel(
            "Export Inventory Settings",
            Application.dataPath,
            "InventorySettings",
            "json");

        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, settingsJson);
            Debug.Log($"📋 Inventory settings exported to: {path}");
        }
    }

    private void ImportInventorySettings()
    {
        string path = EditorUtility.OpenFilePanel(
            "Import Inventory Settings",
            Application.dataPath,
            "json");

        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
        {
            try
            {
                string json = System.IO.File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(json, inventoryManager);
                serializedObject.Update();
                Debug.Log($"📥 Inventory settings imported from: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to import settings: {e.Message}");
            }
        }
    }

    private void ResetToDefaults()
    {
        if (EditorUtility.DisplayDialog("Reset to Defaults",
            "This will reset all inventory settings to default values.\n\nContinue?",
            "Yes", "Cancel"))
        {
            // Reset only object reference properties to null
            utils.ItemPrefabProp.objectReferenceValue = null;
            utils.HandParentProp.objectReferenceValue = null;
            utils.PlayerProp.objectReferenceValue = null;
            utils.PlayerStatusControllerProp.objectReferenceValue = null;
            utils.WeaponControllerProp.objectReferenceValue = null;
            utils.CamProp.objectReferenceValue = null;

            // Reset slot counts to defaults
            inventoryManager.NumberOfHotBarSlots = 4;
            inventoryManager.NumberOfInventorySlots = 20;

            serializedObject.ApplyModifiedProperties();
            Debug.Log("🔄 Inventory Manager reset to default values");
        }
    }

    private void SaveCurrentSetup()
    {
        // Save current setup as editor prefs for quick restoration
        EditorPrefs.SetString("InventoryManager_LastSetup", JsonUtility.ToJson(inventoryManager));
        EditorPrefs.SetInt("InventoryManager_HotbarSlots", inventoryManager.NumberOfHotBarSlots);
        EditorPrefs.SetInt("InventoryManager_InventorySlots", inventoryManager.NumberOfInventorySlots);

        Debug.Log("💾 Current setup saved to editor preferences");

        EditorUtility.DisplayDialog("Setup Saved",
            "Current inventory setup has been saved to editor preferences.\n\nYou can restore this setup later using the Advanced Settings.",
            "OK");
    }

    private void RestoreSavedSetup()
    {
        if (EditorPrefs.HasKey("InventoryManager_LastSetup"))
        {
            if (EditorUtility.DisplayDialog("Restore Setup",
                "Restore previously saved inventory setup?",
                "Yes", "Cancel"))
            {
                try
                {
                    string setupJson = EditorPrefs.GetString("InventoryManager_LastSetup");
                    JsonUtility.FromJsonOverwrite(setupJson, inventoryManager);

                    inventoryManager.NumberOfHotBarSlots = EditorPrefs.GetInt("InventoryManager_HotbarSlots", 4);
                    inventoryManager.NumberOfInventorySlots = EditorPrefs.GetInt("InventoryManager_InventorySlots", 20);

                    serializedObject.Update();
                    Debug.Log("✅ Setup restored from saved preferences");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to restore setup: {e.Message}");
                }
            }
        }
        else
        {
            EditorUtility.DisplayDialog("No Saved Setup",
                "No previously saved setup found.",
                "OK");
        }
    }

    private void ShowDocumentation()
    {
        EditorUtility.DisplayDialog("Inventory Manager Documentation",
            "Inventory Manager Setup Guide:\n\n" +
            "1. Use 'Auto-Detect All' to find components\n" +
            "2. Use 'Auto-Link Components' to assign them\n" +
            "3. Use 'Complete Setup' for full automation\n" +
            "4. Validate setup before entering Play Mode\n" +
            "5. Use Layout Tools to optimize UI in Play Mode\n\n" +
            "For detailed documentation, check the script comments.",
            "OK");
    }

    private void OpenQuickStartGuide()
    {
        var quickStartText = @"
🚀 Quick Start Guide

1. AUTO-SETUP (Recommended):
   • Click 'Complete Setup' button
   • This will detect and assign all components automatically

2. MANUAL SETUP:
   • Click 'Auto-Detect All' to scan for components
   • Review detected components in the Component Detection section
   • Click 'Auto-Link Components' to assign them
   • Or manually assign components in the Component References section

3. VALIDATION:
   • Use 'Full Validation' to check for issues
   • Green indicators mean components are properly assigned
   • Red indicators show missing components

4. TESTING:
   • Enter Play Mode to test functionality
   • Use Layout Tools to optimize UI
   • Use Inventory Tools to test item management

5. TROUBLESHOOTING:
   • Use 'Fix All Issues' if problems occur
   • Check Debug Tools for detailed diagnostics
   • Use 'Generate Report' for comprehensive analysis
";

        EditorUtility.DisplayDialog("Quick Start Guide", quickStartText, "Got it!");
    }
}

// Context menu additions
public static class InventoryManagerContextMenu
{
    [MenuItem("CONTEXT/InventoryManager/Auto Setup")]
    private static void ContextAutoSetup(MenuCommand command)
    {
        var inventoryManager = command.context as InventoryManager;
        if (inventoryManager != null)
        {
            Debug.Log("🚀 Running auto-setup from context menu...");
            var serializedObject = new SerializedObject(inventoryManager);
            var utils = new InventoryEditorUtilities(inventoryManager, serializedObject);
            var autoSetup = new InventoryEditorAutoSetup(inventoryManager, serializedObject, utils);
            autoSetup.Initialize();
            autoSetup.CompleteAutoSetup();
        }
    }

    [MenuItem("CONTEXT/InventoryManager/Validate Setup")]
    private static void ContextValidateSetup(MenuCommand command)
    {
        var inventoryManager = command.context as InventoryManager;
        if (inventoryManager != null)
        {
            Debug.Log("✅ Validating setup from context menu...");
            var serializedObject = new SerializedObject(inventoryManager);
            var utils = new InventoryEditorUtilities(inventoryManager, serializedObject);
            var validation = new InventoryEditorValidation(inventoryManager, serializedObject, utils);
            validation.ValidateInventorySetup();
        }
    }
}
#endif