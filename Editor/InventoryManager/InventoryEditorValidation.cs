#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class InventoryEditorValidation
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;
    private InventoryEditorComponentDetection detection;
    private InventoryEditorAutoSetup autoSetup;

    public InventoryEditorValidation(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
        detection = new InventoryEditorComponentDetection(manager, serialized, utilities);
        autoSetup = new InventoryEditorAutoSetup(manager, serialized, utilities);
        autoSetup.Initialize();
    }

    public void DrawValidationToolsSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        // Setup validation
        EditorGUILayout.LabelField("🔍 Setup Validation", EditorStyles.boldLabel);

        float completeness = CalculateSetupCompleteness();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Setup Completeness:");

        GUI.color = completeness > 0.8f ? styles.SuccessColor : (completeness > 0.5f ? styles.WarningColor : styles.ErrorColor);
        EditorGUILayout.LabelField($"{completeness:P0}", EditorStyles.boldLabel);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Validation buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 Full Validation", styles.ButtonStyle))
        {
            RunFullValidation();
        }

        if (GUILayout.Button("⚠️ Check Issues", styles.WarningButtonStyle))
        {
            CheckForIssues();
        }

        if (GUILayout.Button("🔧 Auto-Fix", styles.SuccessButtonStyle))
        {
            AutoFixIssues();
        }
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🎮 Runtime Validation", EditorStyles.boldLabel);

            var slotManager = utils.GetSlotManager();
            if (slotManager?.ValidationManager != null)
            {
                var validation = slotManager.ValidationManager;
                EditorGUILayout.LabelField($"Slots Validated: {validation.TotalSlotsValidated}");
                EditorGUILayout.LabelField($"Errors: {validation.ValidationErrors}");
                EditorGUILayout.LabelField($"Warnings: {validation.ValidationWarnings}");

                float successRate = validation.TotalSlotsValidated > 0 ?
                    (float)(validation.TotalSlotsValidated - validation.ValidationErrors) / validation.TotalSlotsValidated : 1f;

                GUI.color = successRate > 0.95f ? styles.SuccessColor : (successRate > 0.8f ? styles.WarningColor : styles.ErrorColor);
                EditorGUILayout.LabelField($"Success Rate: {successRate:P1}");
                GUI.color = Color.white;
            }
        }

        EditorGUILayout.EndVertical();
    }

    public float CalculateSetupCompleteness()
    {
        int totalComponents = 8;
        int assignedComponents = 0;

        // Check all properties using the helper method
        if (utils.IsSerializedPropertyConfigured(utils.SlotManagerProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.UILayoutManagerProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.ItemPrefabProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.HandParentProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.PlayerProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.PlayerStatusControllerProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.WeaponControllerProp))
            assignedComponents++;
        if (utils.IsSerializedPropertyConfigured(utils.CamProp))
            assignedComponents++;

        return (float)assignedComponents / totalComponents;
    }

    public bool ValidateInventorySetup()
    {
        List<string> issues = new List<string>();

        // Check critical components using helper method
        if (!utils.IsSerializedPropertyConfigured(utils.SlotManagerProp))
            issues.Add("Slot Manager not properly configured");
        if (!utils.IsSerializedPropertyConfigured(utils.ItemPrefabProp))
            issues.Add("Item Prefab not assigned");
        if (!utils.IsSerializedPropertyConfigured(utils.HandParentProp))
            issues.Add("Hand Parent not assigned");
        if (!utils.IsSerializedPropertyConfigured(utils.PlayerProp))
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

    private void RunFullValidation()
    {
        Debug.Log("🔍 Running full validation...");
        ValidateInventorySetup();

        if (Application.isPlaying)
        {
            inventoryManager.LogLayoutInfo();
            var slotManager = utils.GetSlotManager();
            slotManager?.LogValidationReport();
        }
    }

    private void CheckForIssues()
    {
        List<string> issues = new List<string>();

        // Check object references for common issues
        if (utils.ItemPrefabProp.objectReferenceValue == null)
            issues.Add("Item Prefab not assigned");

        if (utils.HandParentProp.objectReferenceValue == null)
            issues.Add("Hand Parent not assigned");

        // Check for broken references in object references only
        var objectRefProperties = new SerializedProperty[]
        {
            utils.ItemPrefabProp, utils.HandParentProp, utils.PlayerProp,
            utils.PlayerStatusControllerProp, utils.WeaponControllerProp, utils.CamProp
        };

        foreach (var prop in objectRefProperties)
        {
            if (prop != null && prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
            {
                issues.Add($"Broken reference in {prop.displayName}");
            }
        }

        // Check for player-related issues
        if (utils.PlayerProp != null && utils.PlayerProp.objectReferenceValue != null)
        {
            var player = utils.PlayerProp.objectReferenceValue as GameObject;
            if (player != null)
            {
                if (player.GetComponent<PlayerStatusController>() == null &&
                    utils.PlayerStatusControllerProp.objectReferenceValue == null)
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
        detection.RunAutoDetection();
        autoSetup.AutoLinkDetectedComponents();

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

    public void FixAllIssues()
    {
        detection.RunAutoDetection();
        autoSetup.AutoLinkDetectedComponents();
        AutoFixIssues();
        Debug.Log("🔧 Attempted to fix all detected issues");
    }
}
#endif