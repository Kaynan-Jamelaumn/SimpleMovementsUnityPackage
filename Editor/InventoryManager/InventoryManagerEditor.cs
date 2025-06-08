#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(InventoryManager))]
public class InventoryManagerEditor : Editor
{
    private InventoryManager inventoryManager;

    // Sub-editors
    private InventoryEditorStyles styles;
    private InventoryEditorAutoSetup autoSetup;
    private InventoryEditorComponentDetection componentDetection;
    private InventoryEditorLayoutTools layoutTools;
    private InventoryEditorInventoryTools inventoryTools;
    private InventoryEditorValidation validation;
    private InventoryEditorPerformance performance;
    private InventoryEditorDebug debug;
    private InventoryEditorAdvanced advanced;

    // Foldout states
    private bool showAutoSetup = true;
    private bool showComponentDetection = true;
    private bool showLayoutTools = true;
    private bool showInventoryTools = true;
    private bool showValidationTools = true;
    private bool showPerformanceTools = false;
    private bool showDebugTools = false;
    private bool showAdvancedSettings = false;

    private void OnEnable()
    {
        inventoryManager = (InventoryManager)target;
        InitializeSubEditors();
        RunAutoDetection();
    }

    private void InitializeSubEditors()
    {
        styles = new InventoryEditorStyles();

        var utils = new InventoryEditorUtilities(inventoryManager, serializedObject);

        autoSetup = new InventoryEditorAutoSetup(inventoryManager, serializedObject, utils);
        componentDetection = new InventoryEditorComponentDetection(inventoryManager, serializedObject, utils);
        layoutTools = new InventoryEditorLayoutTools(inventoryManager, serializedObject, utils);
        inventoryTools = new InventoryEditorInventoryTools(inventoryManager, serializedObject, utils);
        validation = new InventoryEditorValidation(inventoryManager, serializedObject, utils);
        performance = new InventoryEditorPerformance(inventoryManager, serializedObject, utils);
        debug = new InventoryEditorDebug(inventoryManager, serializedObject, utils);
        advanced = new InventoryEditorAdvanced(inventoryManager, serializedObject, utils);

        // Initialize sub-editors
        autoSetup.Initialize();
        componentDetection.Initialize();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        styles.InitializeStyles();

        // Header
        DrawHeader();

        // Auto Setup Section
        showAutoSetup = EditorGUILayout.Foldout(showAutoSetup, "🔧 Auto Setup & Quick Configuration", true, styles.SubHeaderStyle);
        if (showAutoSetup)
        {
            autoSetup.DrawAutoSetupSection(styles);
        }

        // Component Detection Section
        showComponentDetection = EditorGUILayout.Foldout(showComponentDetection, "🔍 Component Detection & Analysis", true, styles.SubHeaderStyle);
        if (showComponentDetection)
        {
            componentDetection.DrawComponentDetectionSection(styles);
        }

        // Default Inspector (collapsible)
        DrawDefaultInspectorSection();

        // Tool Sections
        showLayoutTools = EditorGUILayout.Foldout(showLayoutTools, "📐 Layout & UI Tools", true, styles.SubHeaderStyle);
        if (showLayoutTools)
        {
            layoutTools.DrawLayoutToolsSection(styles);
        }

        showInventoryTools = EditorGUILayout.Foldout(showInventoryTools, "🎒 Inventory Management Tools", true, styles.SubHeaderStyle);
        if (showInventoryTools)
        {
            inventoryTools.DrawInventoryToolsSection(styles);
        }

        showValidationTools = EditorGUILayout.Foldout(showValidationTools, "✅ Validation & Diagnostics", true, styles.SubHeaderStyle);
        if (showValidationTools)
        {
            validation.DrawValidationToolsSection(styles);
        }

        showPerformanceTools = EditorGUILayout.Foldout(showPerformanceTools, "⚡ Performance Monitoring", true, styles.SubHeaderStyle);
        if (showPerformanceTools)
        {
            performance.DrawPerformanceToolsSection(styles);
        }

        showDebugTools = EditorGUILayout.Foldout(showDebugTools, "🐛 Debug & Development Tools", true, styles.SubHeaderStyle);
        if (showDebugTools)
        {
            debug.DrawDebugToolsSection(styles);
        }

        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "⚙️ Advanced Settings & Utilities", true, styles.SubHeaderStyle);
        if (showAdvancedSettings)
        {
            advanced.DrawAdvancedSettingsSection(styles);
        }

        // Bottom Actions
        DrawBottomActions();

        serializedObject.ApplyModifiedProperties();

        // Auto-refresh in play mode
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎒 Enhanced Inventory Manager", styles.HeaderStyle);
        EditorGUILayout.LabelField("Advanced Setup & Management Tools", styles.CenteredStyle);
        EditorGUILayout.Space(10);

        DrawQuickStatusOverview();
    }

    private void DrawQuickStatusOverview()
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        EditorGUILayout.LabelField("📊 Quick Status", styles.SubHeaderStyle);

        EditorGUILayout.BeginHorizontal();

        // Setup completeness
        float completeness = validation.CalculateSetupCompleteness();
        Color statusColor = completeness > 0.8f ? styles.SuccessColor : (completeness > 0.5f ? styles.WarningColor : styles.ErrorColor);

        GUI.color = statusColor;
        EditorGUILayout.LabelField($"Setup: {completeness:P0}", EditorStyles.boldLabel);
        GUI.color = Color.white;

        if (Application.isPlaying)
        {
            // Runtime stats - Add null checks to prevent errors
            if (inventoryManager != null)
            {
                EditorGUILayout.LabelField($"Hotbar: {inventoryManager.NumberOfHotBarSlots}");
                EditorGUILayout.LabelField($"Inventory: {inventoryManager.NumberOfInventorySlots}");

                // Safe weight calculation with error handling
                try
                {
                    if (inventoryManager.Slots != null)
                    {
                        float weight = inventoryManager.GetTotalInventoryWeight();
                        EditorGUILayout.LabelField($"Weight: {weight:F1}kg");
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Weight: N/A");
                    }
                }
                catch
                {
                    EditorGUILayout.LabelField("Weight: Error");
                }
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawDefaultInspectorSection()
    {
        EditorGUILayout.Space(5);
        bool showDefaultInspector = EditorGUILayout.Foldout(
            EditorPrefs.GetBool("InventoryEditor_ShowDefault", false),
            "⚙️ Component References & Settings", true, styles.SubHeaderStyle);
        EditorPrefs.SetBool("InventoryEditor_ShowDefault", showDefaultInspector);

        if (showDefaultInspector)
        {
            EditorGUILayout.BeginVertical(styles.BoxStyle);

            // Draw properties with enhanced UI
            var props = new InventoryEditorUtilities(inventoryManager, serializedObject);
            props.DrawEnhancedProperties(styles);

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

    private void DrawBottomActions()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        EditorGUILayout.LabelField("🚀 Quick Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // Color-coded action buttons
        GUI.color = styles.SuccessColor;
        if (GUILayout.Button("✨ Complete Setup", GUILayout.Height(30)))
        {
            autoSetup.CompleteAutoSetup();
        }

        GUI.color = styles.InfoColor;
        if (GUILayout.Button("📊 Generate Report", GUILayout.Height(30)))
        {
            GenerateFullReport();
        }

        GUI.color = styles.WarningColor;
        if (GUILayout.Button("🔧 Fix All Issues", GUILayout.Height(30)))
        {
            validation.FixAllIssues();
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Status bar
        EditorGUILayout.Space(5);
        string statusMessage = GetStatusMessage();
        EditorGUILayout.LabelField($"Status: {statusMessage}", styles.CenteredStyle);

        EditorGUILayout.EndVertical();
    }

    private void RunAutoDetection()
    {
        if (!EditorPrefs.GetBool("InventoryEditor_AutoDetect", true)) return;
        componentDetection.RunAutoDetection();
    }

    private string GetStatusMessage()
    {
        if (!Application.isPlaying)
        {
            float completeness = validation.CalculateSetupCompleteness();
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

    private void GenerateFullReport()
    {
        Debug.Log("📊 Generating comprehensive inventory report...");
        componentDetection.GenerateDetectionReport();
        validation.ValidateInventorySetup();

        if (Application.isPlaying)
        {
            inventoryManager.LogInventoryState();
            inventoryManager.LogLayoutInfo();
        }
    }

    // Fixed OnSceneGUI to prevent GUI errors
    void OnSceneGUI()
    {
        if (inventoryManager == null || !Application.isPlaying) return;

        // Wrap in try-catch to prevent GUI errors
        try
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("🎒 Inventory Manager", EditorStyles.boldLabel);
            GUILayout.Label($"Hotbar Slots: {inventoryManager.NumberOfHotBarSlots}");
            GUILayout.Label($"Inventory Slots: {inventoryManager.NumberOfInventorySlots}");

            // Safe weight display
            if (inventoryManager.Slots != null)
            {
                try
                {
                    float weight = inventoryManager.GetTotalInventoryWeight();
                    GUILayout.Label($"Total Weight: {weight:F1}kg");
                }
                catch
                {
                    GUILayout.Label("Total Weight: Error");
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            Handles.EndGUI();
        }
        catch (System.Exception)
        {
            // Silently handle any GUI errors
        }
    }
}
#endif