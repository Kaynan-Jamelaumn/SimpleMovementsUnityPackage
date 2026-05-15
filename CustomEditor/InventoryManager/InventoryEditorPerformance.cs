#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class InventoryEditorPerformance
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;
    
    // Performance optimizations for the editor
    private double lastUpdateTime;
    private const double UPDATE_INTERVAL = 0.5; // Update every 500ms
    
    public InventoryEditorPerformance(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }
    
    public void DrawPerformanceToolsSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        
        if (Application.isPlaying)
        {
            var slotManager = utils.GetSlotManager();
            if (slotManager?.PerformanceManager != null)
            {
                var performance = slotManager.PerformanceManager;
                
                EditorGUILayout.LabelField("📊 Performance Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Operations: {performance.TotalOperations}");
                EditorGUILayout.LabelField($"Total Time: {performance.TotalTime:F2}ms");
                EditorGUILayout.LabelField($"Average Time: {performance.AverageTime:F2}ms");
                
                GUI.color = performance.PeakTime > 50f ? styles.ErrorColor : (performance.PeakTime > 20f ? styles.WarningColor : styles.SuccessColor);
                EditorGUILayout.LabelField($"Peak Time: {performance.PeakTime:F2}ms");
                GUI.color = Color.white;
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📈 Performance Report", styles.ButtonStyle))
                    slotManager.LogPerformanceReport();
                
                if (GUILayout.Button("💡 Optimization Tips", styles.ButtonStyle))
                    performance.LogOptimizationRecommendations();
                
                if (GUILayout.Button("🗑️ Clear Data", styles.ButtonStyle))
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
    
    public bool ShouldUpdate()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - lastUpdateTime > UPDATE_INTERVAL)
        {
            lastUpdateTime = currentTime;
            return true;
        }
        return false;
    }
}
#endif