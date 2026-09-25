#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InventoryManager))]
public class SlotManagerEditor : Editor
{
    private InventoryManager inventoryManager;
    private SlotManager slotManager;
    private bool showPerformanceSection = false;
    private bool showValidationSection = false;
    private bool showLayoutSection = true;
    private bool showDebugSection = false;

    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle boxStyle;

    public override void OnInspectorGUI()
    {
        inventoryManager = (InventoryManager)target;

        // Initialize styles
        InitializeStyles();

        // Draw default inspector
        DrawDefaultInspector();

        if (inventoryManager == null) return;

        // Get slot manager reference
        slotManager = GetSlotManager();
        if (slotManager == null)
        {
            EditorGUILayout.HelpBox("SlotManager not found or not assigned", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(10);

        // Custom sections
        DrawLayoutSection();
        DrawValidationSection();
        DrawPerformanceSection();
        DrawDebugSection();

        // Repaint if playing to show real-time updates
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void InitializeStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 5, 5)
            };
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
        }
    }

    private SlotManager GetSlotManager()
    {
        // Use reflection to get the private slotManager field
        var field = typeof(InventoryManager).GetField("slotManager",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(inventoryManager) as SlotManager;
    }

    private void DrawLayoutSection()
    {
        EditorGUILayout.Space(5);
        showLayoutSection = EditorGUILayout.Foldout(showLayoutSection, "Layout Management", true, headerStyle);

        if (showLayoutSection)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            // Current layout info
            if (Application.isPlaying && slotManager.CurrentLayout != null)
            {
                var layout = slotManager.CurrentLayout;
                EditorGUILayout.LabelField("Current Layout", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Grid: {layout.columns}x{layout.rows}");
                EditorGUILayout.LabelField($"Cell Size: {layout.cellSize.x:F0} x {layout.cellSize.y:F0}");
                EditorGUILayout.LabelField($"Spacing: {layout.spacing.x:F1}");
                EditorGUILayout.LabelField($"Utilization: {layout.panelUtilization:P1}");
                EditorGUILayout.LabelField($"Optimal: {(layout.isOptimal ? "Yes" : "No")}");
                EditorGUILayout.Space(5);
            }

            // Layout presets
            EditorGUILayout.LabelField("Quick Layout Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Compact", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Compact);
            }

            if (GUILayout.Button("Balanced", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Balanced);
            }

            if (GUILayout.Button("Spacious", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Spacious);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Fill Screen", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.FillScreen);
            }

            if (GUILayout.Button("Mobile", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Mobile);
            }

            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Layout presets only work in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawValidationSection()
    {
        EditorGUILayout.Space(5);
        showValidationSection = EditorGUILayout.Foldout(showValidationSection, "Validation & Diagnostics", true, headerStyle);

        if (showValidationSection)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            if (Application.isPlaying && slotManager.ValidationManager != null)
            {
                var validation = slotManager.ValidationManager;

                // Validation stats
                EditorGUILayout.LabelField("Validation Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Slots Validated: {validation.TotalSlotsValidated}");
                EditorGUILayout.LabelField($"Errors: {validation.ValidationErrors}");
                EditorGUILayout.LabelField($"Warnings: {validation.ValidationWarnings}");

                // Success rate with color
                float successRate = validation.TotalSlotsValidated > 0 ?
                    (float)(validation.TotalSlotsValidated - validation.ValidationErrors) / validation.TotalSlotsValidated * 100f : 100f;

                Color originalColor = GUI.color;
                GUI.color = successRate > 95f ? Color.green : (successRate > 80f ? Color.yellow : Color.red);
                EditorGUILayout.LabelField($"Success Rate: {successRate:F1}%");
                GUI.color = originalColor;

                EditorGUILayout.Space(5);
            }

            // Validation buttons
            EditorGUILayout.LabelField("Validation Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Full Validation", buttonStyle))
            {
                if (Application.isPlaying)
                {
                    // Trigger a full re-validation
                    inventoryManager.LogLayoutInfo();
                    Debug.Log(slotManager.GetValidationReport());
                }
                else
                {
                    Debug.Log("Validation only works in Play Mode");
                }
            }

            if (GUILayout.Button("Clear Validation Stats", buttonStyle))
            {
                if (Application.isPlaying)
                {
                    slotManager.ValidationManager?.ClearValidationStats();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Validation features only work in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawPerformanceSection()
    {
        EditorGUILayout.Space(5);
        showPerformanceSection = EditorGUILayout.Foldout(showPerformanceSection, "Performance Monitoring", true, headerStyle);

        if (showPerformanceSection)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            if (Application.isPlaying && slotManager.PerformanceManager != null)
            {
                var performance = slotManager.PerformanceManager;

                // Performance stats
                EditorGUILayout.LabelField("Performance Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Operations: {performance.TotalOperations}");
                EditorGUILayout.LabelField($"Total Time: {performance.TotalTime:F2}ms");
                EditorGUILayout.LabelField($"Average Time: {performance.AverageTime:F2}ms");

                // Peak time with color coding
                Color originalColor = GUI.color;
                GUI.color = performance.PeakTime > 50f ? Color.red : (performance.PeakTime > 20f ? Color.yellow : Color.green);
                EditorGUILayout.LabelField($"Peak Time: {performance.PeakTime:F2}ms ({performance.PeakOperation})");
                GUI.color = originalColor;

                EditorGUILayout.Space(5);
            }

            // Performance buttons
            EditorGUILayout.LabelField("Performance Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Performance Report", buttonStyle))
            {
                if (Application.isPlaying)
                {
                    slotManager.LogPerformanceReport();
                }
                else
                {
                    Debug.Log("Performance monitoring only works in Play Mode");
                }
            }

            if (GUILayout.Button("Optimization Tips", buttonStyle))
            {
                if (Application.isPlaying)
                {
                    slotManager.PerformanceManager?.LogOptimizationRecommendations();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Clear Performance Data", buttonStyle))
            {
                if (Application.isPlaying)
                {
                    slotManager.PerformanceManager?.ClearPerformanceData();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Performance monitoring only works in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawDebugSection()
    {
        EditorGUILayout.Space(5);
        showDebugSection = EditorGUILayout.Foldout(showDebugSection, "Debug Tools", true, headerStyle);

        if (showDebugSection)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            // Slot count controls
            EditorGUILayout.LabelField("Runtime Slot Management", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Hotbar Slot", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.AddHotbarSlots(1);
            }

            if (GUILayout.Button("Remove Hotbar Slot", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.RemoveHotbarSlots(1);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Inventory Slot", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.AddInventorySlots(1);
            }

            if (GUILayout.Button("Remove Inventory Slot", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.RemoveInventorySlots(1);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Debug info
            EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);

            if (GUILayout.Button("Log Inventory State", buttonStyle))
            {
                if (Application.isPlaying)
                    inventoryManager.LogInventoryState();
            }

            if (GUILayout.Button("Log Container Info", buttonStyle))
            {
                if (Application.isPlaying)
                    slotManager.LogSharedContainerInfo();
            }

            if (GUILayout.Button("Log All Debug Info", buttonStyle))
            {
                if (Application.isPlaying)
                {
                    inventoryManager.LogInventoryState();
                    inventoryManager.LogLayoutInfo();
                    slotManager.LogSharedContainerInfo();
                    slotManager.LogPerformanceReport();
                    slotManager.LogValidationReport();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Debug tools only work in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif