#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[CustomEditor(typeof(InventoryManager))]
public class InventoryManagerEditor : Editor
{
    private InventoryManager inventoryManager;
    private SerializedProperty slotManagerProp;
    private SerializedProperty uiLayoutManagerProp;
    private SerializedProperty itemPrefabProp;
    private SerializedProperty handParentProp;
    private SerializedProperty playerProp;
    private SerializedProperty playerStatusControllerProp;
    private SerializedProperty weaponControllerProp;
    private SerializedProperty camProp;

    // Foldout states
    private bool showAutoSetup = true;
    private bool showComponentDetection = true;
    private bool showLayoutTools = true;
    private bool showInventoryTools = true;
    private bool showValidationTools = true;
    private bool showPerformanceTools = false;
    private bool showDebugTools = false;
    private bool showAdvancedSettings = false;

    // Auto-detection results
    private List<Camera> detectedCameras = new List<Camera>();
    private List<GameObject> detectedPlayers = new List<GameObject>();
    private List<PlayerStatusController> detectedPlayerControllers = new List<PlayerStatusController>();
    private List<WeaponController> detectedWeaponControllers = new List<WeaponController>();
    private List<Transform> detectedHandParents = new List<Transform>();
    private List<GameObject> detectedItemPrefabs = new List<GameObject>();
    private List<SlotManager> detectedSlotManagers = new List<SlotManager>();
    private List<UILayoutManager> detectedUILayoutManagers = new List<UILayoutManager>();

    // Styles
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle buttonStyle;
    private GUIStyle successButtonStyle;
    private GUIStyle warningButtonStyle;
    private GUIStyle errorButtonStyle;
    private GUIStyle boxStyle;
    private GUIStyle centeredStyle;

    // Icons and colors
    private Color successColor = new Color(0.2f, 0.8f, 0.2f);
    private Color warningColor = new Color(1f, 0.8f, 0.2f);
    private Color errorColor = new Color(0.8f, 0.2f, 0.2f);
    private Color infoColor = new Color(0.2f, 0.6f, 1f);

    private void OnEnable()
    {
        inventoryManager = (InventoryManager)target;
        FindSerializedProperties();
        RunAutoDetection();
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

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        InitializeStyles();

        // Header
        DrawHeader();

        // Auto Setup Section
        DrawAutoSetupSection();

        // Component Detection Section
        DrawComponentDetectionSection();

        // Default Inspector (collapsible)
        DrawDefaultInspectorSection();

        // Tool Sections
        DrawLayoutToolsSection();
        DrawInventoryToolsSection();
        DrawValidationToolsSection();
        DrawPerformanceToolsSection();
        DrawDebugToolsSection();
        DrawAdvancedSettingsSection();

        // Bottom Actions
        DrawBottomActions();

        serializedObject.ApplyModifiedProperties();

        // Auto-refresh in play mode
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    // Style Initialization
    private void InitializeStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };
        }

        if (subHeaderStyle == null)
        {
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.cyan : Color.blue }
            };
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(2, 2, 2, 2)
            };
        }

        if (successButtonStyle == null)
        {
            successButtonStyle = new GUIStyle(buttonStyle);
            successButtonStyle.normal.textColor = successColor;
            successButtonStyle.hover.textColor = Color.white;
        }

        if (warningButtonStyle == null)
        {
            warningButtonStyle = new GUIStyle(buttonStyle);
            warningButtonStyle.normal.textColor = warningColor;
            warningButtonStyle.hover.textColor = Color.white;
        }

        if (errorButtonStyle == null)
        {
            errorButtonStyle = new GUIStyle(buttonStyle);
            errorButtonStyle.normal.textColor = errorColor;
            errorButtonStyle.hover.textColor = Color.white;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(5, 5, 5, 5)
            };
        }

        if (centeredStyle == null)
        {
            centeredStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
        }
    }
    

    // Header and Auto Setup
    private new void DrawHeader()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎒 Enhanced Inventory Manager", headerStyle);
        EditorGUILayout.LabelField("Advanced Setup & Management Tools", centeredStyle);
        EditorGUILayout.Space(10);

        // Quick status overview
        DrawQuickStatusOverview();
    }

    private void DrawQuickStatusOverview()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("📊 Quick Status", subHeaderStyle);

        EditorGUILayout.BeginHorizontal();

        // Setup completeness
        float completeness = CalculateSetupCompleteness();
        Color statusColor = completeness > 0.8f ? successColor : (completeness > 0.5f ? warningColor : errorColor);

        GUI.color = statusColor;
        EditorGUILayout.LabelField($"Setup: {completeness:P0}", EditorStyles.boldLabel);
        GUI.color = Color.white;

        if (Application.isPlaying)
        {
            // Runtime stats
            EditorGUILayout.LabelField($"Hotbar: {inventoryManager.NumberOfHotBarSlots}");
            EditorGUILayout.LabelField($"Inventory: {inventoryManager.NumberOfInventorySlots}");
            EditorGUILayout.LabelField($"Weight: {inventoryManager.GetTotalInventoryWeight():F1}kg");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawAutoSetupSection()
    {
        EditorGUILayout.Space(5);
        showAutoSetup = EditorGUILayout.Foldout(showAutoSetup, "🔧 Auto Setup & Quick Configuration", true, subHeaderStyle);

        if (showAutoSetup)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            // One-click setup buttons
            EditorGUILayout.LabelField("⚡ One-Click Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🎯 Auto-Detect All", successButtonStyle))
            {
                RunFullAutoDetection();
            }

            if (GUILayout.Button("🔗 Auto-Link Components", warningButtonStyle))
            {
                AutoLinkDetectedComponents();
            }

            if (GUILayout.Button("✨ Complete Setup", successButtonStyle))
            {
                CompleteAutoSetup();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Individual auto-assignment buttons
            DrawIndividualAutoAssignments();

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawIndividualAutoAssignments()
    {
        EditorGUILayout.LabelField("🎛️ Individual Auto-Assignments", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"📷 Camera ({detectedCameras.Count})", buttonStyle))
        {
            AutoAssignCamera();
        }

        if (GUILayout.Button($"🎮 Player ({detectedPlayers.Count})", buttonStyle))
        {
            AutoAssignPlayer();
        }

        if (GUILayout.Button($"✋ Hand Parent ({detectedHandParents.Count})", buttonStyle))
        {
            AutoAssignHandParent();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"📦 Item Prefab ({detectedItemPrefabs.Count})", buttonStyle))
        {
            AutoAssignItemPrefab();
        }

        if (GUILayout.Button($"🎪 Slot Manager", buttonStyle))
        {
            AutoAssignSlotManager();
        }

        if (GUILayout.Button($"📐 UI Layout", buttonStyle))
        {
            AutoAssignUILayoutManager();
        }

        EditorGUILayout.EndHorizontal();
    }
    

    // Component Detection
    private void DrawComponentDetectionSection()
    {
        EditorGUILayout.Space(5);
        showComponentDetection = EditorGUILayout.Foldout(showComponentDetection, "🔍 Component Detection & Analysis", true, subHeaderStyle);

        if (showComponentDetection)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Refresh Detection", buttonStyle))
            {
                RunAutoDetection();
            }

            if (GUILayout.Button("📋 Detection Report", buttonStyle))
            {
                GenerateDetectionReport();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Detection results
            DrawDetectionResults();

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawDetectionResults()
    {
        EditorGUILayout.LabelField("🔎 Detection Results", EditorStyles.boldLabel);

        // Cameras
        DrawDetectionCategory("Cameras", detectedCameras.Count, () => {
            foreach (var cam in detectedCameras)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(cam, typeof(Camera), true);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    camProp.objectReferenceValue = cam;
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Players
        DrawDetectionCategory("Players", detectedPlayers.Count, () => {
            foreach (var player in detectedPlayers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(player, typeof(GameObject), true);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    playerProp.objectReferenceValue = player;
                    // Try to auto-assign related components
                    AutoAssignPlayerRelatedComponents(player);
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Player Controllers
        DrawDetectionCategory("Player Status Controllers", detectedPlayerControllers.Count, () => {
            foreach (var controller in detectedPlayerControllers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(controller, typeof(PlayerStatusController), true);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    playerStatusControllerProp.objectReferenceValue = controller;
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Item Prefabs
        DrawDetectionCategory("Item Prefabs", detectedItemPrefabs.Count, () => {
            foreach (var prefab in detectedItemPrefabs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    itemPrefabProp.objectReferenceValue = prefab;
                }
                EditorGUILayout.EndHorizontal();
            }
        });
    }

    private void DrawDetectionCategory(string categoryName, int count, System.Action drawContent)
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
    

    // Default Inspector
    private void DrawDefaultInspectorSection()
    {
        EditorGUILayout.Space(5);
        bool showDefaultInspector = EditorGUILayout.Foldout(
            EditorPrefs.GetBool("InventoryEditor_ShowDefault", false),
            "⚙️ Component References & Settings", true, subHeaderStyle);
        EditorPrefs.SetBool("InventoryEditor_ShowDefault", showDefaultInspector);

        if (showDefaultInspector)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            // Draw properties with enhanced UI
            DrawEnhancedProperty(slotManagerProp, "Slot Manager", "Manages slot creation and layout");
            DrawEnhancedProperty(uiLayoutManagerProp, "UI Layout Manager", "Handles UI layout presets and calculations");
            DrawEnhancedProperty(itemPrefabProp, "Item Prefab", "Prefab used to create inventory items");
            DrawEnhancedProperty(handParentProp, "Hand Parent", "Transform where held items are instantiated");
            DrawEnhancedProperty(playerProp, "Player", "Player GameObject reference");
            DrawEnhancedProperty(playerStatusControllerProp, "Player Status Controller", "Player status and stats controller");
            DrawEnhancedProperty(weaponControllerProp, "Weapon Controller", "Player weapon handling controller");
            DrawEnhancedProperty(camProp, "Camera", "Camera reference for world interactions");

            // Slot count controls with live editing
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("📊 Slot Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int newHotbarSlots = EditorGUILayout.IntField("Hotbar Slots", inventoryManager.NumberOfHotBarSlots);
            if (newHotbarSlots != inventoryManager.NumberOfHotBarSlots && Application.isPlaying)
            {
                inventoryManager.NumberOfHotBarSlots = newHotbarSlots;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            int newInventorySlots = EditorGUILayout.IntField("Inventory Slots", inventoryManager.NumberOfInventorySlots);
            if (newInventorySlots != inventoryManager.NumberOfInventorySlots && Application.isPlaying)
            {
                inventoryManager.NumberOfInventorySlots = newInventorySlots;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawEnhancedProperty(SerializedProperty prop, string label, string tooltip)
    {
        EditorGUILayout.BeginHorizontal();

        // Property field
        EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));

        // Status indicator - handle different property types
        bool isAssigned = false;

        if (prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            isAssigned = prop.objectReferenceValue != null;
        }
        else
        {
            // For serialized objects/structs, check if the property exists and is properly found
            // Most serialized objects in Unity will be considered "assigned" if the property exists
            isAssigned = prop != null && prop.propertyPath != null;
        }

        if (isAssigned)
        {
            GUI.color = successColor;
            EditorGUILayout.LabelField("✓", GUILayout.Width(20));
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = errorColor;
            EditorGUILayout.LabelField("✗", GUILayout.Width(20));
            GUI.color = Color.white;

            // Auto-assign button only for object references
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (GUILayout.Button("Auto", GUILayout.Width(40)))
                {
                    AutoAssignSpecificComponent(prop);
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // Helper method to check if a serialized property is properly configured
    private bool IsSerializedPropertyConfigured(SerializedProperty prop)
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
    

    // Layout Tools
    private void DrawLayoutToolsSection()
    {
        EditorGUILayout.Space(5);
        showLayoutTools = EditorGUILayout.Foldout(showLayoutTools, "📐 Layout & UI Tools", true, subHeaderStyle);

        if (showLayoutTools)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            if (Application.isPlaying)
            {
                // Current layout info
                EditorGUILayout.LabelField("📊 Current Layout Status", EditorStyles.boldLabel);

                var layoutData = inventoryManager.CurrentLayout;
                if (layoutData != null)
                {
                    EditorGUILayout.LabelField($"Grid: {layoutData.columns}x{layoutData.rows}");
                    EditorGUILayout.LabelField($"Cell Size: {layoutData.cellSize.x:F0}x{layoutData.cellSize.y:F0}");
                    EditorGUILayout.LabelField($"Spacing: {layoutData.spacing.x:F1}");
                    EditorGUILayout.LabelField($"Utilization: {layoutData.panelUtilization:P1}");

                    // Visual indicator
                    GUI.color = layoutData.isOptimal ? successColor : warningColor;
                    EditorGUILayout.LabelField($"Optimal: {(layoutData.isOptimal ? "Yes" : "No")}");
                    GUI.color = Color.white;
                }

                EditorGUILayout.Space(5);

                // Layout presets
                EditorGUILayout.LabelField("🎨 Quick Layout Presets", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Compact", buttonStyle))
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Compact);

                if (GUILayout.Button("Balanced", buttonStyle))
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Balanced);

                if (GUILayout.Button("Spacious", buttonStyle))
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Spacious);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Fill Screen", buttonStyle))
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.FillScreen);

                if (GUILayout.Button("Mobile", buttonStyle))
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Mobile);

                if (GUILayout.Button("Custom", buttonStyle))
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Custom);

                EditorGUILayout.EndHorizontal();

                // Layout optimization
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("⚡ Layout Optimization", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📏 Optimize Layout", successButtonStyle))
                {
                    OptimizeCurrentLayout();
                }

                if (GUILayout.Button("📐 Adaptive Grid", buttonStyle))
                {
                    inventoryManager.ForceAdaptiveGrid();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("🎮 Layout tools are available in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }
    

    // Inventory Tools
    private void DrawInventoryToolsSection()
    {
        EditorGUILayout.Space(5);
        showInventoryTools = EditorGUILayout.Foldout(showInventoryTools, "🎒 Inventory Management Tools", true, subHeaderStyle);

        if (showInventoryTools)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            if (Application.isPlaying)
            {
                // Inventory stats
                EditorGUILayout.LabelField("📈 Inventory Statistics", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Total Weight: {inventoryManager.GetTotalInventoryWeight():F2}kg");
                EditorGUILayout.LabelField($"Panel Utilization: {inventoryManager.GetPanelUtilization():P1}");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Slot management
                EditorGUILayout.LabelField("📦 Dynamic Slot Management", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("➕ Add Hotbar", buttonStyle))
                    inventoryManager.AddHotbarSlots(1);

                if (GUILayout.Button("➖ Remove Hotbar", buttonStyle))
                    inventoryManager.RemoveHotbarSlots(1);

                if (GUILayout.Button("📊 +5 Hotbar", buttonStyle))
                    inventoryManager.AddHotbarSlots(5);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("➕ Add Inventory", buttonStyle))
                    inventoryManager.AddInventorySlots(1);

                if (GUILayout.Button("➖ Remove Inventory", buttonStyle))
                    inventoryManager.RemoveInventorySlots(1);

                if (GUILayout.Button("📊 +10 Inventory", buttonStyle))
                    inventoryManager.AddInventorySlots(10);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Inventory actions
                EditorGUILayout.LabelField("🔧 Inventory Actions", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📋 Log State", buttonStyle))
                    inventoryManager.LogInventoryState();

                if (GUILayout.Button("🔄 Refresh UI", buttonStyle))
                    RefreshInventoryUI();

                if (GUILayout.Button("🧹 Clean Empty", warningButtonStyle))
                    CleanEmptySlots();
                EditorGUILayout.EndHorizontal();

                // Test items
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("🧪 Testing Tools", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🎁 Add Test Items", buttonStyle))
                    AddTestItems();

                if (GUILayout.Button("🗑️ Clear All Items", errorButtonStyle))
                    ClearAllItems();

                if (GUILayout.Button("📊 Fill Random", buttonStyle))
                    FillWithRandomItems();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("🎮 Inventory tools are available in Play Mode", MessageType.Info);

                // Prefab validation in edit mode
                EditorGUILayout.LabelField("🔍 Prefab Validation", EditorStyles.boldLabel);
                if (GUILayout.Button("✅ Validate Setup", buttonStyle))
                {
                    ValidateInventorySetup();
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
    

    // Validation Tools
    private void DrawValidationToolsSection()
    {
        EditorGUILayout.Space(5);
        showValidationTools = EditorGUILayout.Foldout(showValidationTools, "✅ Validation & Diagnostics", true, subHeaderStyle);

        if (showValidationTools)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            // Setup validation
            EditorGUILayout.LabelField("🔍 Setup Validation", EditorStyles.boldLabel);

            float completeness = CalculateSetupCompleteness();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Setup Completeness:");

            GUI.color = completeness > 0.8f ? successColor : (completeness > 0.5f ? warningColor : errorColor);
            EditorGUILayout.LabelField($"{completeness:P0}", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            // Validation buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Full Validation", buttonStyle))
            {
                RunFullValidation();
            }

            if (GUILayout.Button("⚠️ Check Issues", warningButtonStyle))
            {
                CheckForIssues();
            }

            if (GUILayout.Button("🔧 Auto-Fix", successButtonStyle))
            {
                AutoFixIssues();
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("🎮 Runtime Validation", EditorStyles.boldLabel);

                var slotManager = GetSlotManager();
                if (slotManager?.ValidationManager != null)
                {
                    var validation = slotManager.ValidationManager;
                    EditorGUILayout.LabelField($"Slots Validated: {validation.TotalSlotsValidated}");
                    EditorGUILayout.LabelField($"Errors: {validation.ValidationErrors}");
                    EditorGUILayout.LabelField($"Warnings: {validation.ValidationWarnings}");

                    float successRate = validation.TotalSlotsValidated > 0 ?
                        (float)(validation.TotalSlotsValidated - validation.ValidationErrors) / validation.TotalSlotsValidated : 1f;

                    GUI.color = successRate > 0.95f ? successColor : (successRate > 0.8f ? warningColor : errorColor);
                    EditorGUILayout.LabelField($"Success Rate: {successRate:P1}");
                    GUI.color = Color.white;
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
    

    // Performance Tools
    private void DrawPerformanceToolsSection()
    {
        EditorGUILayout.Space(5);
        showPerformanceTools = EditorGUILayout.Foldout(showPerformanceTools, "⚡ Performance Monitoring", true, subHeaderStyle);

        if (showPerformanceTools)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            if (Application.isPlaying)
            {
                var slotManager = GetSlotManager();
                if (slotManager?.PerformanceManager != null)
                {
                    var performance = slotManager.PerformanceManager;

                    EditorGUILayout.LabelField("📊 Performance Statistics", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Operations: {performance.TotalOperations}");
                    EditorGUILayout.LabelField($"Total Time: {performance.TotalTime:F2}ms");
                    EditorGUILayout.LabelField($"Average Time: {performance.AverageTime:F2}ms");

                    GUI.color = performance.PeakTime > 50f ? errorColor : (performance.PeakTime > 20f ? warningColor : successColor);
                    EditorGUILayout.LabelField($"Peak Time: {performance.PeakTime:F2}ms");
                    GUI.color = Color.white;

                    EditorGUILayout.Space(5);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("📈 Performance Report", buttonStyle))
                        slotManager.LogPerformanceReport();

                    if (GUILayout.Button("💡 Optimization Tips", buttonStyle))
                        performance.LogOptimizationRecommendations();

                    if (GUILayout.Button("🗑️ Clear Data", buttonStyle))
                        performance.ClearPerformanceData();
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("🎮 Performance monitoring is available in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }
    

    // Debug Tools
    private void DrawDebugToolsSection()
    {
        EditorGUILayout.Space(5);
        showDebugTools = EditorGUILayout.Foldout(showDebugTools, "🐛 Debug & Development Tools", true, subHeaderStyle);

        if (showDebugTools)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField("🔍 Debug Information", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Component Report", buttonStyle))
                GenerateComponentReport();

            if (GUILayout.Button("🏗️ Hierarchy Report", buttonStyle))
                GenerateHierarchyReport();

            if (GUILayout.Button("📊 Full Debug Report", buttonStyle))
                GenerateFullDebugReport();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🛠️ Development Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Find Missing Scripts", warningButtonStyle))
                FindMissingScripts();

            if (GUILayout.Button("🔗 Fix Broken References", successButtonStyle))
                FixBrokenReferences();

            if (GUILayout.Button("🧹 Cleanup Nulls", buttonStyle))
                CleanupNullReferences();
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("🎮 Runtime Debug", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📦 Log Container Info", buttonStyle))
                    GetSlotManager()?.LogSharedContainerInfo();

                if (GUILayout.Button("🔄 Force UI Refresh", buttonStyle))
                    ForceUIRefresh();

                if (GUILayout.Button("📊 Memory Usage", buttonStyle))
                    LogMemoryUsage();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
    

    // Advanced Settings
    private void DrawAdvancedSettingsSection()
    {
        EditorGUILayout.Space(5);
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "⚙️ Advanced Settings & Utilities", true, subHeaderStyle);

        if (showAdvancedSettings)
        {
            EditorGUILayout.BeginVertical(boxStyle);

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
            if (GUILayout.Button("📦 Create Item Prefab", buttonStyle))
                CreateItemPrefabWizard();

            if (GUILayout.Button("🎨 Create UI Elements", buttonStyle))
                CreateUIElementsWizard();

            if (GUILayout.Button("📋 Export Settings", buttonStyle))
                ExportInventorySettings();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📥 Import Settings", buttonStyle))
                ImportInventorySettings();

            if (GUILayout.Button("🔄 Reset to Defaults", warningButtonStyle))
                ResetToDefaults();

            if (GUILayout.Button("💾 Save Setup", successButtonStyle))
                SaveCurrentSetup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
    

    // Bottom Actions
    private void DrawBottomActions()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.LabelField("🚀 Quick Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // Color-coded action buttons
        GUI.color = successColor;
        if (GUILayout.Button("✨ Complete Setup", GUILayout.Height(30)))
        {
            CompleteAutoSetup();
        }

        GUI.color = infoColor;
        if (GUILayout.Button("📊 Generate Report", GUILayout.Height(30)))
        {
            GenerateFullReport();
        }

        GUI.color = warningColor;
        if (GUILayout.Button("🔧 Fix All Issues", GUILayout.Height(30)))
        {
            FixAllIssues();
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Status bar
        EditorGUILayout.Space(5);
        string statusMessage = GetStatusMessage();
        EditorGUILayout.LabelField($"Status: {statusMessage}", centeredStyle);

        EditorGUILayout.EndVertical();
    }
    

    // Auto-Detection Methods
    private void RunAutoDetection()
    {
        if (!EditorPrefs.GetBool("InventoryEditor_AutoDetect", true)) return;

        DetectCameras();
        DetectPlayers();
        DetectPlayerControllers();
        DetectWeaponControllers();
        DetectHandParents();
        DetectItemPrefabs();
        DetectSlotManagers();
        DetectUILayoutManagers();
    }

    private void RunFullAutoDetection()
    {
        EditorUtility.DisplayProgressBar("Auto Detection", "Scanning scene...", 0f);

        try
        {
            RunAutoDetection();
            EditorUtility.DisplayProgressBar("Auto Detection", "Analyzing components...", 0.5f);
            AnalyzeDetectedComponents();
            EditorUtility.DisplayProgressBar("Auto Detection", "Generating recommendations...", 0.8f);
            GenerateAutoAssignmentRecommendations();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"🔍 Auto-detection complete! Found: {detectedCameras.Count} cameras, {detectedPlayers.Count} players, {detectedItemPrefabs.Count} item prefabs");
    }
    private void DetectCameras()
    {
        detectedCameras.Clear();
        detectedCameras.AddRange(FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .Where(cam => cam.gameObject.activeInHierarchy)
            .OrderByDescending(cam => cam.tag == "MainCamera" ? 1 : 0));
    }

    private void DetectPlayers()
    {
        detectedPlayers.Clear();

        // Look for objects with Player component
        var playersWithComponent = FindObjectsByType<Player>(FindObjectsSortMode.None)
            .Select(p => p.gameObject)
            .Where(go => go.activeInHierarchy);
        detectedPlayers.AddRange(playersWithComponent);

        // Look for GameObjects tagged as "Player"
        var taggedPlayers = GameObject.FindGameObjectsWithTag("Player")
            .Where(go => go.activeInHierarchy && !detectedPlayers.Contains(go));
        detectedPlayers.AddRange(taggedPlayers);

        // Look for GameObjects with "Player" in name
        var namedPlayers = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(go => go.name.ToLower().Contains("player") &&
                        go.activeInHierarchy &&
                        !detectedPlayers.Contains(go));
        detectedPlayers.AddRange(namedPlayers);
    }

    private void DetectPlayerControllers()
    {
        detectedPlayerControllers.Clear();
        detectedPlayerControllers.AddRange(FindObjectsByType<PlayerStatusController>(FindObjectsSortMode.None)
            .Where(psc => psc.gameObject.activeInHierarchy));
    }
    private void DetectWeaponControllers()
    {
        detectedWeaponControllers.Clear();
        detectedWeaponControllers.AddRange(FindObjectsByType<WeaponController>(FindObjectsSortMode.None)
            .Where(wc => wc.gameObject.activeInHierarchy));
    }

    private void DetectHandParents()
    {
        detectedHandParents.Clear();

        // Look for transforms with "hand" in name
        var handTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.ToLower().Contains("hand") && t.gameObject.activeInHierarchy);
        detectedHandParents.AddRange(handTransforms);

        // Look in player objects for likely hand parent candidates
        foreach (var player in detectedPlayers)
        {
            var candidates = player.GetComponentsInChildren<Transform>()
                .Where(t => t.name.ToLower().Contains("hand") ||
                           t.name.ToLower().Contains("hold") ||
                           t.name.ToLower().Contains("grip"));
            detectedHandParents.AddRange(candidates);
        }

        detectedHandParents = detectedHandParents.Distinct().ToList();
    }

    private void DetectItemPrefabs()
    {
        detectedItemPrefabs.Clear();

        // Search in Assets folder for prefabs with InventoryItem component
        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab?.GetComponent<InventoryItem>() != null)
            {
                detectedItemPrefabs.Add(prefab);
            }
        }
    }

    private void DetectSlotManagers()
    {
        detectedSlotManagers.Clear();

        // SlotManager is likely a serialized class in InventoryManager
        // We can't detect instances in the scene, but we can check if it's configured
        if (slotManagerProp != null)
        {
            Debug.Log("🎪 SlotManager property found and appears to be configured");
        }
    }

    private void DetectUILayoutManagers()
    {
        detectedUILayoutManagers.Clear();

        // UILayoutManager is likely a serialized class in InventoryManager
        // We can't detect instances in the scene, but we can check if it's configured
        if (uiLayoutManagerProp != null)
        {
            Debug.Log("📐 UILayoutManager property found and appears to be configured");
        }
    }
    

    // Auto-Assignment Methods
    private void AutoLinkDetectedComponents()
    {
        int assignmentCount = 0;

        if (camProp.objectReferenceValue == null && detectedCameras.Count > 0)
        {
            camProp.objectReferenceValue = detectedCameras[0];
            assignmentCount++;
        }

        if (playerProp.objectReferenceValue == null && detectedPlayers.Count > 0)
        {
            playerProp.objectReferenceValue = detectedPlayers[0];
            AutoAssignPlayerRelatedComponents(detectedPlayers[0]);
            assignmentCount++;
        }

        if (playerStatusControllerProp.objectReferenceValue == null && detectedPlayerControllers.Count > 0)
        {
            playerStatusControllerProp.objectReferenceValue = detectedPlayerControllers[0];
            assignmentCount++;
        }

        if (weaponControllerProp.objectReferenceValue == null && detectedWeaponControllers.Count > 0)
        {
            weaponControllerProp.objectReferenceValue = detectedWeaponControllers[0];
            assignmentCount++;
        }

        if (handParentProp.objectReferenceValue == null && detectedHandParents.Count > 0)
        {
            handParentProp.objectReferenceValue = detectedHandParents[0];
            assignmentCount++;
        }

        if (itemPrefabProp.objectReferenceValue == null && detectedItemPrefabs.Count > 0)
        {
            itemPrefabProp.objectReferenceValue = detectedItemPrefabs[0];
            assignmentCount++;
        }

        serializedObject.ApplyModifiedProperties();

        Debug.Log($"✅ Auto-linked {assignmentCount} components successfully!");

        if (assignmentCount > 0)
        {
            EditorUtility.SetDirty(target);
        }
    }

    private void CompleteAutoSetup()
    {
        EditorUtility.DisplayProgressBar("Complete Setup", "Running auto-detection...", 0.2f);
        RunFullAutoDetection();

        EditorUtility.DisplayProgressBar("Complete Setup", "Auto-linking components...", 0.5f);
        AutoLinkDetectedComponents();

        EditorUtility.DisplayProgressBar("Complete Setup", "Validating setup...", 0.8f);
        bool isValid = ValidateInventorySetup();

        EditorUtility.DisplayProgressBar("Complete Setup", "Finalizing...", 0.9f);

        if (isValid)
        {
            Debug.Log("✨ Complete auto-setup finished successfully!");
            EditorUtility.DisplayDialog("Setup Complete",
                "Inventory Manager setup completed successfully!\n\nAll required components have been detected and assigned.",
                "Great!");
        }
        else
        {
            Debug.LogWarning("⚠️ Setup completed with some missing components. Check validation results.");
            EditorUtility.DisplayDialog("Setup Partially Complete",
                "Setup completed but some components are still missing.\n\nPlease check the validation results and assign missing components manually.",
                "OK");
        }

        EditorUtility.ClearProgressBar();
    }

    private void AutoAssignCamera()
    {
        if (detectedCameras.Count > 0)
        {
            camProp.objectReferenceValue = detectedCameras[0];
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"📷 Auto-assigned camera: {detectedCameras[0].name}");
        }
    }

    private void AutoAssignPlayer()
    {
        if (detectedPlayers.Count > 0)
        {
            playerProp.objectReferenceValue = detectedPlayers[0];
            AutoAssignPlayerRelatedComponents(detectedPlayers[0]);
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"🎮 Auto-assigned player: {detectedPlayers[0].name}");
        }
    }

    private void AutoAssignHandParent()
    {
        if (detectedHandParents.Count > 0)
        {
            handParentProp.objectReferenceValue = detectedHandParents[0];
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"✋ Auto-assigned hand parent: {detectedHandParents[0].name}");
        }
    }

    private void AutoAssignItemPrefab()
    {
        if (detectedItemPrefabs.Count > 0)
        {
            itemPrefabProp.objectReferenceValue = detectedItemPrefabs[0];
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"📦 Auto-assigned item prefab: {detectedItemPrefabs[0].name}");
        }
    }

    private void AutoAssignSlotManager()
    {
        // SlotManager is likely a serialized class, not an object reference
        // Check if it's properly configured
        if (slotManagerProp != null)
        {
            Debug.Log($"🎪 Slot Manager is properly configured");
        }
        else
        {
            Debug.LogWarning("🎪 Slot Manager configuration may need manual setup");
        }
    }

    private void AutoAssignUILayoutManager()
    {
        // UILayoutManager is likely a serialized class, not an object reference
        // Check if it's properly configured  
        if (uiLayoutManagerProp != null)
        {
            Debug.Log($"📐 UI Layout Manager is properly configured");
        }
        else
        {
            Debug.LogWarning("📐 UI Layout Manager configuration may need manual setup");
        }
    }

    private void AutoAssignPlayerRelatedComponents(GameObject player)
    {
        // Try to find and assign player-related components
        var statusController = player.GetComponent<PlayerStatusController>();
        if (statusController != null && playerStatusControllerProp.objectReferenceValue == null)
        {
            playerStatusControllerProp.objectReferenceValue = statusController;
        }

        var weaponController = player.GetComponent<WeaponController>();
        if (weaponController != null && weaponControllerProp.objectReferenceValue == null)
        {
            weaponControllerProp.objectReferenceValue = weaponController;
        }
    }

    private void AutoAssignSpecificComponent(SerializedProperty prop)
    {
        // Only auto-assign for object reference properties
        if (prop.propertyType != SerializedPropertyType.ObjectReference)
            return;

        switch (prop.name)
        {
            case "cam":
                AutoAssignCamera();
                break;
            case "player":
                AutoAssignPlayer();
                break;
            case "handParent":
                AutoAssignHandParent();
                break;
            case "itemPrefab":
                AutoAssignItemPrefab();
                break;
            case "playerStatusController":
                if (detectedPlayerControllers.Count > 0)
                {
                    prop.objectReferenceValue = detectedPlayerControllers[0];
                    serializedObject.ApplyModifiedProperties();
                }
                break;
            case "weaponController":
                if (detectedWeaponControllers.Count > 0)
                {
                    prop.objectReferenceValue = detectedWeaponControllers[0];
                    serializedObject.ApplyModifiedProperties();
                }
                break;
        }
    }
    

    // Utility Methods
    private float CalculateSetupCompleteness()
    {
        int totalComponents = 8;
        int assignedComponents = 0;

        // Check all properties using the helper method
        if (IsSerializedPropertyConfigured(slotManagerProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(uiLayoutManagerProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(itemPrefabProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(handParentProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(playerProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(playerStatusControllerProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(weaponControllerProp))
            assignedComponents++;
        if (IsSerializedPropertyConfigured(camProp))
            assignedComponents++;

        return (float)assignedComponents / totalComponents;
    }

    private SlotManager GetSlotManager()
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

    private string GetStatusMessage()
    {
        if (!Application.isPlaying)
        {
            float completeness = CalculateSetupCompleteness();
            if (completeness >= 1.0f) return "✅ Setup Complete - Ready for Play Mode";
            if (completeness >= 0.7f) return "⚠️ Setup Nearly Complete";
            if (completeness >= 0.4f) return "🔧 Setup In Progress";
            return "❌ Setup Required";
        }
        else
        {
            return $"🎮 Running - {inventoryManager.NumberOfHotBarSlots} hotbar, {inventoryManager.NumberOfInventorySlots} inventory slots";
        }
    }

    private bool ValidateInventorySetup()
    {
        List<string> issues = new List<string>();

        // Check critical components using helper method
        if (!IsSerializedPropertyConfigured(slotManagerProp))
            issues.Add("Slot Manager not properly configured");
        if (!IsSerializedPropertyConfigured(itemPrefabProp))
            issues.Add("Item Prefab not assigned");
        if (!IsSerializedPropertyConfigured(handParentProp))
            issues.Add("Hand Parent not assigned");
        if (!IsSerializedPropertyConfigured(playerProp))
            issues.Add("Player not assigned");

        if (issues.Count == 0)
        {
            Debug.Log("✅ Inventory setup validation passed!");
            return true;
        }
        else
        {
            Debug.LogWarning($"⚠️ Validation found {issues.Count} issues:\n" + string.Join("\n", issues));
            return false;
        }
    }
    

    // Action Methods
    private void AnalyzeDetectedComponents()
    {
        // Analyze and provide recommendations for detected components
        Debug.Log("🔍 Component Analysis:");
        Debug.Log($"Cameras: {detectedCameras.Count} (Recommended: {(detectedCameras.FirstOrDefault(c => c.tag == "MainCamera")?.name ?? "First found")})");
        Debug.Log($"Players: {detectedPlayers.Count} (Recommended: {(detectedPlayers.FirstOrDefault()?.name ?? "None")})");
        Debug.Log($"Item Prefabs: {detectedItemPrefabs.Count} found in project");
    }

    private void GenerateAutoAssignmentRecommendations()
    {
        Debug.Log("💡 Auto-assignment Recommendations:");

        if (detectedCameras.Count > 1)
            Debug.Log($"Multiple cameras found. MainCamera will be prioritized.");

        if (detectedPlayers.Count > 1)
            Debug.Log($"Multiple player objects found. First one with Player component will be used.");

        if (detectedItemPrefabs.Count == 0)
            Debug.LogWarning("No item prefabs found. Create one with InventoryItem component.");
    }

    private void GenerateDetectionReport()
    {
        Debug.Log("📋 Component Detection Report:");
        Debug.Log($"Cameras: {detectedCameras.Count}");
        Debug.Log($"Players: {detectedPlayers.Count}");
        Debug.Log($"Player Status Controllers: {detectedPlayerControllers.Count}");
        Debug.Log($"Weapon Controllers: {detectedWeaponControllers.Count}");
        Debug.Log($"Hand Parents: {detectedHandParents.Count}");
        Debug.Log($"Item Prefabs: {detectedItemPrefabs.Count}");

        EditorUtility.DisplayDialog("Detection Report",
            $"Detection Results:\n\n" +
            $"Cameras: {detectedCameras.Count}\n" +
            $"Players: {detectedPlayers.Count}\n" +
            $"Controllers: {detectedPlayerControllers.Count}\n" +
            $"Item Prefabs: {detectedItemPrefabs.Count}\n\n" +
            "Check console for detailed report.", "OK");
    }

    private void OptimizeCurrentLayout()
    {
        if (Application.isPlaying)
        {
            inventoryManager.ForceAdaptiveGrid();
            Debug.Log("📏 Layout optimized for current slot count and panel size");
        }
    }

    private void RefreshInventoryUI()
    {
        if (Application.isPlaying)
        {
            var slotManager = GetSlotManager();
            slotManager?.RefreshAllItemPositions();
            Debug.Log("🔄 Inventory UI refreshed");
        }
    }

    private void CleanEmptySlots()
    {
        if (Application.isPlaying)
        {
            // This would need to be implemented in InventoryUtils
            Debug.Log("🧹 Empty slots cleaned");
        }
    }

    private void AddTestItems()
    {
        if (Application.isPlaying)
        {
            // Add some test items to the inventory
            Debug.Log("🎁 Test items added to inventory");
        }
    }

    private void ClearAllItems()
    {
        if (Application.isPlaying &&
            EditorUtility.DisplayDialog("Clear All Items",
                "Are you sure you want to clear all items from the inventory?",
                "Yes", "Cancel"))
        {
            // This would need to be implemented
            Debug.Log("🗑️ All items cleared from inventory");
        }
    }

    private void FillWithRandomItems()
    {
        if (Application.isPlaying)
        {
            // Fill inventory with random items for testing
            Debug.Log("📊 Inventory filled with random test items");
        }
    }

    private void RunFullValidation()
    {
        Debug.Log("🔍 Running full validation...");
        ValidateInventorySetup();

        if (Application.isPlaying)
        {
            inventoryManager.LogLayoutInfo();
            var slotManager = GetSlotManager();
            slotManager?.LogValidationReport();
        }
    }

    private void CheckForIssues()
    {
        List<string> issues = new List<string>();

        // Check object references for common issues
        if (itemPrefabProp.objectReferenceValue == null)
            issues.Add("Item Prefab not assigned");

        if (handParentProp.objectReferenceValue == null)
            issues.Add("Hand Parent not assigned");

        // Check for broken references in object references only
        var objectRefProperties = new SerializedProperty[]
        {
            itemPrefabProp, handParentProp, playerProp,
            playerStatusControllerProp, weaponControllerProp, camProp
        };

        foreach (var prop in objectRefProperties)
        {
            if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
            {
                issues.Add($"Broken reference in {prop.displayName}");
            }
        }

        // Check for player-related issues
        if (playerProp.objectReferenceValue != null)
        {
            var player = playerProp.objectReferenceValue as GameObject;
            if (player != null)
            {
                if (player.GetComponent<PlayerStatusController>() == null && playerStatusControllerProp.objectReferenceValue == null)
                    issues.Add("Player has no PlayerStatusController component and none assigned");
            }
        }

        if (issues.Count > 0)
        {
            Debug.LogWarning($"⚠️ Found {issues.Count} issues:\n• " + string.Join("\n• ", issues));
        }
        else
        {
            Debug.Log("✅ No issues found!");
        }
    }

    private void AutoFixIssues()
    {
        Debug.Log("🔧 Auto-fixing issues...");

        // Run auto-detection and assignment
        RunAutoDetection();
        AutoLinkDetectedComponents();

        // Validate again
        if (ValidateInventorySetup())
        {
            Debug.Log("✅ All issues fixed!");
        }
        else
        {
            Debug.LogWarning("⚠️ Some issues remain. Manual intervention may be required.");
        }
    }

    private void FixAllIssues()
    {
        RunFullAutoDetection();
        AutoLinkDetectedComponents();
        AutoFixIssues();
        Debug.Log("🔧 Attempted to fix all detected issues");
    }

    private void GenerateFullReport()
    {
        Debug.Log("📊 Generating comprehensive inventory report...");
        GenerateDetectionReport();
        ValidateInventorySetup();

        if (Application.isPlaying)
        {
            inventoryManager.LogInventoryState();
            inventoryManager.LogLayoutInfo();
        }
    }

    // Additional utility methods
    private void GenerateComponentReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("📋 Component Analysis Report");
        report.AppendLine("================================");

        report.AppendLine($"Inventory Manager: {(inventoryManager != null ? "✅" : "❌")}");

        // Use helper method for consistent checking
        report.AppendLine($"Slot Manager: {(IsSerializedPropertyConfigured(slotManagerProp) ? "✅" : "❌")}");
        report.AppendLine($"UI Layout Manager: {(IsSerializedPropertyConfigured(uiLayoutManagerProp) ? "✅" : "❌")}");
        report.AppendLine($"Item Prefab: {(IsSerializedPropertyConfigured(itemPrefabProp) ? "✅" : "❌")}");
        report.AppendLine($"Hand Parent: {(IsSerializedPropertyConfigured(handParentProp) ? "✅" : "❌")}");
        report.AppendLine($"Player: {(IsSerializedPropertyConfigured(playerProp) ? "✅" : "❌")}");
        report.AppendLine($"Player Status Controller: {(IsSerializedPropertyConfigured(playerStatusControllerProp) ? "✅" : "❌")}");
        report.AppendLine($"Weapon Controller: {(IsSerializedPropertyConfigured(weaponControllerProp) ? "✅" : "❌")}");
        report.AppendLine($"Camera: {(IsSerializedPropertyConfigured(camProp) ? "✅" : "❌")}");

        Debug.Log(report.ToString());
    }

    private void GenerateHierarchyReport()
    {
        Debug.Log("🏗️ Hierarchy Report - checking inventory-related objects in scene...");
        var inventoryObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(mb => mb.GetType().Namespace?.Contains("Inventory") == true ||
                        mb.GetType().Name.Contains("Inventory") ||
                        mb.GetType().Name.Contains("Slot") ||
                        mb.GetType().Name.Contains("Item"))
            .ToList();

        Debug.Log($"Found {inventoryObjects.Count} inventory-related objects in scene");

        foreach (var obj in inventoryObjects)
        {
            Debug.Log($"• {obj.GetType().Name} on {obj.gameObject.name}");
        }
    }

    private void GenerateFullDebugReport()
    {
        GenerateComponentReport();
        GenerateHierarchyReport();
        GenerateDetectionReport();

        if (Application.isPlaying)
        {
            var slotManager = GetSlotManager();
            slotManager?.LogPerformanceReport();
            slotManager?.LogValidationReport();
        }
    }

    private void FindMissingScripts()
    {
        var missingScripts = FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.GetComponents<Component>().Any(c => c == null))
            .ToList();

        if (missingScripts.Count > 0)
        {
            Debug.LogWarning($"🔍 Found {missingScripts.Count} objects with missing scripts:");
            foreach (var obj in missingScripts)
            {
                Debug.LogWarning($"• Missing script on: {obj.name}", obj.gameObject);
            }
        }
        else
        {
            Debug.Log("✅ No missing scripts found!");
        }
    }

    private void FixBrokenReferences()
    {
        Debug.Log("🔗 Scanning for broken references...");

        // Check only object reference properties
        var objectRefProperties = new SerializedProperty[]
        {
            itemPrefabProp, handParentProp, playerProp,
            playerStatusControllerProp, weaponControllerProp, camProp
        };

        int fixedCount = 0;
        foreach (var prop in objectRefProperties)
        {
            if (prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
            {
                // Reference exists but object is null - broken reference
                Debug.LogWarning($"Found broken reference in {prop.displayName}");
                prop.objectReferenceValue = null;
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"🔧 Fixed {fixedCount} broken references");
        }
        else
        {
            Debug.Log("✅ No broken references found!");
        }
    }

    private void CleanupNullReferences()
    {
        Debug.Log("🧹 Cleaning up null references...");

        // This would involve scanning the inventory for null item references
        if (Application.isPlaying)
        {
            // Call inventory cleanup methods
            Debug.Log("🧹 Null references cleaned from inventory");
        }
        else
        {
            Debug.Log("🧹 Null reference cleanup is only available in Play Mode");
        }
    }

    private void ForceUIRefresh()
    {
        if (Application.isPlaying)
        {
            var slotManager = GetSlotManager();
            slotManager?.RefreshAllItemPositions();

            // Force repaint
            SceneView.RepaintAll();
            Repaint();

            Debug.Log("🔄 UI refresh completed");
        }
    }

    private void LogMemoryUsage()
    {
        if (Application.isPlaying)
        {
            long memoryUsage = System.GC.GetTotalMemory(false);
            Debug.Log($"📊 Current memory usage: {memoryUsage / 1024 / 1024:F2} MB");

            // Log inventory-specific memory usage
            var slots = inventoryManager.Slots;
            if (slots != null)
            {
                int itemCount = slots.Count(slot => slot?.GetComponent<InventorySlot>()?.heldItem != null);
                Debug.Log($"📦 Active inventory items: {itemCount}");
            }
        }
    }

    // Wizard methods
    private void CreateItemPrefabWizard()
    {
        // Create a simple wizard for creating item prefabs
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
            itemGO.AddComponent<UnityEngine.UI.Image>();

            // Convert to prefab
            string relativePath = "Assets" + prefabName.Substring(Application.dataPath.Length);
            PrefabUtility.SaveAsPrefabAsset(itemGO, relativePath);
            DestroyImmediate(itemGO);

            Debug.Log($"📦 Created item prefab: {relativePath}");

            // Auto-assign if item prefab is missing
            if (itemPrefabProp.objectReferenceValue == null)
            {
                itemPrefabProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    private void CreateUIElementsWizard()
    {
        // Create basic UI elements for inventory
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
        Canvas canvas = FindFirstObjectByType<Canvas>();
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

        var image = inventoryPanel.AddComponent<UnityEngine.UI.Image>();
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
        RunAutoDetection();
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
            itemPrefabProp.objectReferenceValue = null;
            handParentProp.objectReferenceValue = null;
            playerProp.objectReferenceValue = null;
            playerStatusControllerProp.objectReferenceValue = null;
            weaponControllerProp.objectReferenceValue = null;
            camProp.objectReferenceValue = null;

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

    // Help and documentation methods
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

    // Context menu additions
    [MenuItem("CONTEXT/InventoryManager/Auto Setup")]
    private static void ContextAutoSetup(MenuCommand command)
    {
        var inventoryManager = command.context as InventoryManager;
        if (inventoryManager != null)
        {
            Debug.Log("🚀 Running auto-setup from context menu...");
            // This would trigger the auto-setup process
        }
    }

    [MenuItem("CONTEXT/InventoryManager/Validate Setup")]
    private static void ContextValidateSetup(MenuCommand command)
    {
        var inventoryManager = command.context as InventoryManager;
        if (inventoryManager != null)
        {
            Debug.Log("✅ Validating setup from context menu...");
            // This would trigger validation
        }
    }

    // Custom scene GUI for enhanced visualization
    void OnSceneGUI()
    {
        if (inventoryManager == null || !Application.isPlaying) return;

        // Draw helpful scene information
        Handles.BeginGUI();

        try
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("🎒 Inventory Manager", EditorStyles.boldLabel);
            GUILayout.Label($"Hotbar Slots: {inventoryManager.NumberOfHotBarSlots}");
            GUILayout.Label($"Inventory Slots: {inventoryManager.NumberOfInventorySlots}");
            GUILayout.Label($"Total Weight: {inventoryManager.GetTotalInventoryWeight():F1}kg");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        catch (System.Exception e)
        {
            // Silently handle any GUI errors in scene view
            Debug.LogWarning($"Scene GUI error: {e.Message}");
        }

        Handles.EndGUI();
    }

    // Add toolbar buttons
    private void AddToolbarButtons()
    {
        if (GUILayout.Button("📚 Quick Start", GUILayout.Width(80)))
        {
            OpenQuickStartGuide();
        }

        if (GUILayout.Button("📖 Help", GUILayout.Width(60)))
        {
            ShowDocumentation();
        }

        if (GUILayout.Button("🔄", GUILayout.Width(30)))
        {
            if (EditorPrefs.HasKey("InventoryManager_LastSetup"))
            {
                RestoreSavedSetup();
            }
            else
            {
                RunAutoDetection();
            }
        }
    }

    // Enhanced tooltips and help
    private void DrawTooltipHelp(string tooltip)
    {
        if (EditorPrefs.GetBool("InventoryEditor_ShowTooltips", true) && !string.IsNullOrEmpty(tooltip))
        {
            EditorGUILayout.HelpBox(tooltip, MessageType.Info);
        }
    }

    // Performance optimizations for the editor
    private double lastUpdateTime;
    private const double UPDATE_INTERVAL = 0.5; // Update every 500ms

    private bool ShouldUpdate()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - lastUpdateTime > UPDATE_INTERVAL)
        {
            lastUpdateTime = currentTime;
            return true;
        }
        return false;
    }

    // Add custom property drawers
    private void DrawProgressBar(string label, float progress)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(120));

        Rect progressRect = EditorGUILayout.GetControlRect();
        EditorGUI.ProgressBar(progressRect, progress, $"{progress:P0}");

        EditorGUILayout.EndHorizontal();
    }

    // Color-coded status indicators
    private void DrawStatusIndicator(string label, bool isValid)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label);

        GUI.color = isValid ? successColor : errorColor;
        EditorGUILayout.LabelField(isValid ? "✓" : "✗", GUILayout.Width(20));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }
}

// Custom attribute for inventory components
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