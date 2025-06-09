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

        // Progress bar for completeness
        Rect progressRect = EditorGUILayout.GetControlRect();
        EditorGUI.ProgressBar(progressRect, completeness, $"Setup Progress: {completeness:P0}");

        EditorGUILayout.Space(5);

        // Component status overview
        DrawComponentStatusOverview();

        // Validation buttons
        EditorGUILayout.Space(5);
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

    private void DrawComponentStatusOverview()
    {
        EditorGUILayout.LabelField("📋 Component Status", EditorStyles.boldLabel);

        // Core components
        DrawStatusLine("Item Prefab", utils.IsSerializedPropertyConfigured(utils.ItemPrefabProp));
        DrawStatusLine("Hand Parent", utils.IsSerializedPropertyConfigured(utils.HandParentProp));
        DrawStatusLine("Player", utils.IsSerializedPropertyConfigured(utils.PlayerProp));
        DrawStatusLine("Camera", utils.IsSerializedPropertyConfigured(utils.CamProp));

        // Management components
        DrawStatusLine("Slot Manager", utils.SlotManagerProp != null);
        DrawStatusLine("UI Layout Manager", utils.UILayoutManagerProp != null);

        // UI components
        DrawStatusLine("Inventory Panel", utils.IsSerializedPropertyConfigured(utils.InventoryParentProp));
        DrawStatusLine("Equipment Panel", utils.IsSerializedPropertyConfigured(utils.EquippableInventoryProp));

        // Controllers
        DrawStatusLine("Player Status Controller", utils.IsSerializedPropertyConfigured(utils.PlayerStatusControllerProp));
        DrawStatusLine("Weapon Controller", utils.IsSerializedPropertyConfigured(utils.WeaponControllerProp));
    }

    private void DrawStatusLine(string componentName, bool isConfigured)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(componentName, GUILayout.Width(150));

        GUI.color = isConfigured ? Color.green : Color.red;
        EditorGUILayout.LabelField(isConfigured ? "✓ OK" : "✗ Missing", GUILayout.Width(60));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    public float CalculateSetupCompleteness()
    {
        // Use the utility method that properly checks all components
        return utils.CalculateCompleteness();
    }

    public bool ValidateInventorySetup()
    {
        List<string> issues = new List<string>();
        List<string> warnings = new List<string>();

        // Check critical components using helper method
        if (!utils.IsSerializedPropertyConfigured(utils.ItemPrefabProp))
            issues.Add("Item Prefab not assigned");
        if (!utils.IsSerializedPropertyConfigured(utils.HandParentProp))
            issues.Add("Hand Parent not assigned");
        if (!utils.IsSerializedPropertyConfigured(utils.PlayerProp))
            issues.Add("Player not assigned");
        if (!utils.IsSerializedPropertyConfigured(utils.CamProp))
            issues.Add("Camera not assigned");

        // Check management components
        if (utils.SlotManagerProp == null)
            issues.Add("Slot Manager not properly configured");
        if (utils.UILayoutManagerProp == null)
            warnings.Add("UI Layout Manager not properly configured");

        // Check UI panels
        if (!utils.IsSerializedPropertyConfigured(utils.InventoryParentProp))
            warnings.Add("Inventory Panel not assigned");

        // Check controllers
        if (!utils.IsSerializedPropertyConfigured(utils.PlayerStatusControllerProp))
            warnings.Add("Player Status Controller not assigned");
        if (!utils.IsSerializedPropertyConfigured(utils.WeaponControllerProp))
            warnings.Add("Weapon Controller not assigned");

        // Report results
        if (issues.Count == 0 && warnings.Count == 0)
        {
            Debug.Log("✅ Inventory setup validation passed!");
            return true;
        }
        else
        {
            if (issues.Count > 0)
            {
                Debug.LogError($"❌ Validation found {issues.Count} critical issues:\n• " + string.Join("\n• ", issues));
            }
            if (warnings.Count > 0)
            {
                Debug.LogWarning($"⚠️ Validation found {warnings.Count} warnings:\n• " + string.Join("\n• ", warnings));
            }
            return issues.Count == 0; // Return true if only warnings, false if there are errors
        }
    }

    private void RunFullValidation()
    {
        Debug.Log("🔍 Running full validation...");
        bool isValid = ValidateInventorySetup();

        // Additional validation checks
        ValidateSerializationIntegrity();
        ValidateComponentReferences();

        if (Application.isPlaying)
        {
            inventoryManager.LogLayoutInfo();
            var slotManager = utils.GetSlotManager();
            slotManager?.LogValidationReport();
        }

        string status = isValid ? "✅ PASSED" : "❌ FAILED";
        Debug.Log($"🔍 Full validation {status}");
    }

    private void ValidateSerializationIntegrity()
    {
        Debug.Log("🔍 Validating serialization integrity...");

        // Check if serializable classes are properly initialized
        if (utils.SlotManagerProp != null)
        {
            Debug.Log("✅ SlotManager serialization OK");
        }
        else
        {
            Debug.LogWarning("⚠️ SlotManager serialization issue detected");
        }

        if (utils.UILayoutManagerProp != null)
        {
            Debug.Log("✅ UILayoutManager serialization OK");
        }
        else
        {
            Debug.LogWarning("⚠️ UILayoutManager serialization issue detected");
        }
    }

    private void ValidateComponentReferences()
    {
        Debug.Log("🔍 Validating component references...");

        // Check for broken references in object references only
        var objectRefProperties = new SerializedProperty[]
        {
            utils.ItemPrefabProp, utils.HandParentProp, utils.PlayerProp,
            utils.PlayerStatusControllerProp, utils.WeaponControllerProp, utils.CamProp,
            utils.InventoryParentProp, utils.EquippableInventoryProp, utils.StorageParentProp,
            utils.ItemInfoProp
        };

        int brokenRefs = 0;
        foreach (var prop in objectRefProperties)
        {
            if (prop != null && prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
            {
                Debug.LogWarning($"Broken reference detected in {prop.displayName}");
                brokenRefs++;
            }
        }

        if (brokenRefs == 0)
        {
            Debug.Log("✅ No broken references found");
        }
        else
        {
            Debug.LogWarning($"⚠️ Found {brokenRefs} broken references");
        }
    }

    private void CheckForIssues()
    {
        List<string> issues = new List<string>();

        // Check object references for common issues
        if (utils.ItemPrefabProp?.objectReferenceValue == null)
            issues.Add("Item Prefab not assigned");

        if (utils.HandParentProp?.objectReferenceValue == null)
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
        if (utils.PlayerProp?.objectReferenceValue != null)
        {
            var player = utils.PlayerProp.objectReferenceValue as GameObject;
            if (player != null)
            {
                if (player.GetComponent<PlayerStatusController>() == null &&
                    utils.PlayerStatusControllerProp?.objectReferenceValue == null)
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

        // Force serialized object update
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(inventoryManager);

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