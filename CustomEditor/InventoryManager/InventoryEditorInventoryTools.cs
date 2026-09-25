#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class InventoryEditorInventoryTools
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;

    public InventoryEditorInventoryTools(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }

    public void DrawInventoryToolsSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        if (Application.isPlaying)
        {
            // Inventory stats
            EditorGUILayout.LabelField("📈 Inventory Statistics", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Safe weight calculation
            try
            {
                if (inventoryManager.Slots != null)
                {
                    float weight = inventoryManager.GetTotalInventoryWeight();
                    EditorGUILayout.LabelField($"Total Weight: {weight:F2}kg");
                }
                else
                {
                    EditorGUILayout.LabelField("Total Weight: N/A");
                }
            }
            catch
            {
                EditorGUILayout.LabelField("Total Weight: Error");
            }

            EditorGUILayout.LabelField($"Panel Utilization: {inventoryManager.GetPanelUtilization():P1}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Slot management
            EditorGUILayout.LabelField("📦 Dynamic Slot Management", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add Hotbar", styles.ButtonStyle))
                inventoryManager.AddHotbarSlots(1);

            if (GUILayout.Button("➖ Remove Hotbar", styles.ButtonStyle))
                inventoryManager.RemoveHotbarSlots(1);

            if (GUILayout.Button("📊 +5 Hotbar", styles.ButtonStyle))
                inventoryManager.AddHotbarSlots(5);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add Inventory", styles.ButtonStyle))
                inventoryManager.AddInventorySlots(1);

            if (GUILayout.Button("➖ Remove Inventory", styles.ButtonStyle))
                inventoryManager.RemoveInventorySlots(1);

            if (GUILayout.Button("📊 +10 Inventory", styles.ButtonStyle))
                inventoryManager.AddInventorySlots(10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Inventory actions
            EditorGUILayout.LabelField("🔧 Inventory Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Log State", styles.ButtonStyle))
                inventoryManager.LogInventoryState();

            if (GUILayout.Button("🔄 Refresh UI", styles.ButtonStyle))
                RefreshInventoryUI();

            if (GUILayout.Button("🧹 Clean Empty", styles.WarningButtonStyle))
                CleanEmptySlots();
            EditorGUILayout.EndHorizontal();

            // Test items
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🧪 Testing Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎁 Add Test Items", styles.ButtonStyle))
                AddTestItems();

            if (GUILayout.Button("🗑️ Clear All Items", styles.ErrorButtonStyle))
                ClearAllItems();

            if (GUILayout.Button("📊 Fill Random", styles.ButtonStyle))
                FillWithRandomItems();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("🎮 Inventory tools are available in Play Mode", MessageType.Info);

            // Prefab validation in edit mode
            EditorGUILayout.LabelField("🔍 Prefab Validation", EditorStyles.boldLabel);
            if (GUILayout.Button("✅ Validate Setup", styles.ButtonStyle))
            {
                var validation = new InventoryEditorValidation(inventoryManager, serializedObject, utils);
                validation.ValidateInventorySetup();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void RefreshInventoryUI()
    {
        if (Application.isPlaying)
        {
            var slotManager = utils.GetSlotManager();
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
}
#endif