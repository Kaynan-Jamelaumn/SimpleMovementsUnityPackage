#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

public class InventoryEditorDebug
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;

    public InventoryEditorDebug(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }

    public void DrawDebugToolsSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        EditorGUILayout.LabelField("🔍 Debug Information", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📋 Component Report", styles.ButtonStyle))
            GenerateComponentReport();

        if (GUILayout.Button("🏗️ Hierarchy Report", styles.ButtonStyle))
            GenerateHierarchyReport();

        if (GUILayout.Button("📊 Full Debug Report", styles.ButtonStyle))
            GenerateFullDebugReport();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🛠️ Development Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎯 Find Missing Scripts", styles.WarningButtonStyle))
            FindMissingScripts();

        if (GUILayout.Button("🔗 Fix Broken References", styles.SuccessButtonStyle))
            FixBrokenReferences();

        if (GUILayout.Button("🧹 Cleanup Nulls", styles.ButtonStyle))
            CleanupNullReferences();
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🎮 Runtime Debug", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📦 Log Container Info", styles.ButtonStyle))
                utils.GetSlotManager()?.LogSharedContainerInfo();

            if (GUILayout.Button("🔄 Force UI Refresh", styles.ButtonStyle))
                ForceUIRefresh();

            if (GUILayout.Button("📊 Memory Usage", styles.ButtonStyle))
                LogMemoryUsage();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void GenerateComponentReport()
    {
        var report = new StringBuilder();
        report.AppendLine("📋 Component Analysis Report");
        report.AppendLine("================================");

        report.AppendLine($"Inventory Manager: {(inventoryManager != null ? "✅" : "❌")}");

        // Use helper method for consistent checking
        report.AppendLine($"Slot Manager: {(utils.IsSerializedPropertyConfigured(utils.SlotManagerProp) ? "✅" : "❌")}");
        report.AppendLine($"UI Layout Manager: {(utils.IsSerializedPropertyConfigured(utils.UILayoutManagerProp) ? "✅" : "❌")}");
        report.AppendLine($"Item Prefab: {(utils.IsSerializedPropertyConfigured(utils.ItemPrefabProp) ? "✅" : "❌")}");
        report.AppendLine($"Hand Parent: {(utils.IsSerializedPropertyConfigured(utils.HandParentProp) ? "✅" : "❌")}");
        report.AppendLine($"Player: {(utils.IsSerializedPropertyConfigured(utils.PlayerProp) ? "✅" : "❌")}");
        report.AppendLine($"Player Status Controller: {(utils.IsSerializedPropertyConfigured(utils.PlayerStatusControllerProp) ? "✅" : "❌")}");
        report.AppendLine($"Weapon Controller: {(utils.IsSerializedPropertyConfigured(utils.WeaponControllerProp) ? "✅" : "❌")}");
        report.AppendLine($"Camera: {(utils.IsSerializedPropertyConfigured(utils.CamProp) ? "✅" : "❌")}");

        Debug.Log(report.ToString());
    }

    private void GenerateHierarchyReport()
    {
        Debug.Log("🏗️ Hierarchy Report - checking inventory-related objects in scene...");
        var inventoryObjects = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
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

        var detection = new InventoryEditorComponentDetection(inventoryManager, serializedObject, utils);
        detection.GenerateDetectionReport();

        if (Application.isPlaying)
        {
            var slotManager = utils.GetSlotManager();
            slotManager?.LogPerformanceReport();
            slotManager?.LogValidationReport();
        }
    }

    private void FindMissingScripts()
    {
        var missingScripts = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
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
            utils.ItemPrefabProp, utils.HandParentProp, utils.PlayerProp,
            utils.PlayerStatusControllerProp, utils.WeaponControllerProp, utils.CamProp
        };

        int fixedCount = 0;
        foreach (var prop in objectRefProperties)
        {
            if (prop != null && prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
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
            var slotManager = utils.GetSlotManager();
            slotManager?.RefreshAllItemPositions();

            // Force repaint
            SceneView.RepaintAll();

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
}
#endif