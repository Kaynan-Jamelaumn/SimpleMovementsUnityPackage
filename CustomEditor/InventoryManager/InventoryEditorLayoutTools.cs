#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class InventoryEditorLayoutTools
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;

    public InventoryEditorLayoutTools(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }

    public void DrawLayoutToolsSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

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
                GUI.color = layoutData.isOptimal ? styles.SuccessColor : styles.WarningColor;
                EditorGUILayout.LabelField($"Optimal: {(layoutData.isOptimal ? "Yes" : "No")}");
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(5);

            // Layout presets
            EditorGUILayout.LabelField("🎨 Quick Layout Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Compact", styles.ButtonStyle))
                inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Compact);

            if (GUILayout.Button("Balanced", styles.ButtonStyle))
                inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Balanced);

            if (GUILayout.Button("Spacious", styles.ButtonStyle))
                inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Spacious);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Fill Screen", styles.ButtonStyle))
                inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.FillScreen);

            if (GUILayout.Button("Mobile", styles.ButtonStyle))
                inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Mobile);

            if (GUILayout.Button("Custom", styles.ButtonStyle))
                inventoryManager.SetLayoutPreset(UILayoutManager.LayoutPreset.Custom);

            EditorGUILayout.EndHorizontal();

            // Layout optimization
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("⚡ Layout Optimization", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📏 Optimize Layout", styles.SuccessButtonStyle))
            {
                OptimizeCurrentLayout();
            }

            if (GUILayout.Button("📐 Adaptive Grid", styles.ButtonStyle))
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

    private void OptimizeCurrentLayout()
    {
        if (Application.isPlaying)
        {
            inventoryManager.ForceAdaptiveGrid();
            Debug.Log("📏 Layout optimized for current slot count and panel size");
        }
    }
}
#endif