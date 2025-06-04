#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// Unity Editor helper for PlayerStatusController
// Provides custom inspector features, setup wizards, and editor-only utilities
[CustomEditor(typeof(PlayerStatusController))]
public class PlayerStatusControllerEditor : Editor
{
    private PlayerStatusController statusController;
    private PlayerStatusSetupHelper setupHelper;
    private bool showSetupTools = true;
    private bool showValidationResults = false;
    private bool showStatusValues = false;
    private bool showDebugTools = false;
    private SetupValidationResult lastValidationResult;

    public override void OnInspectorGUI()
    {
        statusController = (PlayerStatusController)target;
        setupHelper = new PlayerStatusSetupHelper(statusController);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Player Status Setup Tools", EditorStyles.boldLabel);

        DrawSetupTools();
        DrawValidationSection();
        DrawStatusValueEditor();
        DrawDebugTools();
    }

    // Draw main setup tools section
    private void DrawSetupTools()
    {
        showSetupTools = EditorGUILayout.Foldout(showSetupTools, "Setup Tools", true);
        if (!showSetupTools) return;

        EditorGUILayout.BeginVertical("box");

        // Quick setup buttons
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-Assign All Components", GUILayout.Height(25)))
        {
            AutoAssignAllComponents();
        }
        if (GUILayout.Button("Create Missing Components", GUILayout.Height(25)))
        {
            CreateMissingComponents();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Complete Setup Wizard", GUILayout.Height(25)))
        {
            RunCompleteSetupWizard();
        }
        if (GUILayout.Button("Reset All References", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Reset References",
                "This will clear all component references. Are you sure?", "Yes", "Cancel"))
            {
                ResetAllReferences();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Individual component setup
        EditorGUILayout.LabelField("Individual Components", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("Setup Status Managers Only"))
        {
            SetupStatusManagersOnly();
        }

        if (GUILayout.Button("Setup System Managers Only"))
        {
            SetupSystemManagersOnly();
        }

        if (GUILayout.Button("Setup Movement Models Only"))
        {
            SetupMovementModelsOnly();
        }

        EditorGUILayout.EndVertical();
    }

    // Draw validation section
    private void DrawValidationSection()
    {
        showValidationResults = EditorGUILayout.Foldout(showValidationResults, "Validation & Diagnostics", true);
        if (!showValidationResults) return;

        EditorGUILayout.BeginVertical("box");

        if (GUILayout.Button("Validate Setup", GUILayout.Height(25)))
        {
            lastValidationResult = setupHelper.ValidateSetup();
        }

        if (GUILayout.Button("Validate Status Values", GUILayout.Height(25)))
        {
            ValidateStatusValues();
        }

        // Display validation results
        if (lastValidationResult != null)
        {
            EditorGUILayout.Space(5);
            DrawValidationResults(lastValidationResult);
        }

        EditorGUILayout.EndVertical();
    }

    // Draw status value editor
    private void DrawStatusValueEditor()
    {
        showStatusValues = EditorGUILayout.Foldout(showStatusValues, "Status Values Editor", true);
        if (!showStatusValues) return;

        EditorGUILayout.BeginVertical("box");

        if (statusController.CurrentPlayerClass == null)
        {
            EditorGUILayout.HelpBox("No PlayerClass assigned. Assign a PlayerClass to edit status values.", MessageType.Warning);
        }
        else
        {
            DrawPlayerClassEditor();
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Reasonable Defaults"))
        {
            setupHelper.SetupDefaultValues();
            EditorUtility.SetDirty(statusController.CurrentPlayerClass);
        }

        if (GUILayout.Button("Reset to Zero"))
        {
            if (EditorUtility.DisplayDialog("Reset Values",
                "This will reset all status values to zero. Are you sure?", "Yes", "Cancel"))
            {
                ResetStatusValuesToZero();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // Draw debug tools section
    private void DrawDebugTools()
    {
        showDebugTools = EditorGUILayout.Foldout(showDebugTools, "Debug Tools", true);
        if (!showDebugTools) return;

        EditorGUILayout.BeginVertical("box");

        if (Application.isPlaying)
        {
            DrawRuntimeDebugTools();
        }
        else
        {
            EditorGUILayout.HelpBox("Debug tools are available during Play mode.", MessageType.Info);
        }

        // Editor-time debug tools
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Editor Tools", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("Log Component Hierarchy"))
        {
            LogComponentHierarchy();
        }

        if (GUILayout.Button("Find All Status Controllers in Scene"))
        {
            FindAllStatusControllersInScene();
        }

        EditorGUILayout.EndVertical();
    }

    // Draw player class editor
    private void DrawPlayerClassEditor()
    {
        var playerClass = statusController.CurrentPlayerClass;

        EditorGUILayout.LabelField($"Editing: {playerClass.className}", EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();

        // Core stats
        EditorGUILayout.LabelField("Core Stats", EditorStyles.boldLabel);
        playerClass.health = EditorGUILayout.FloatField("Health", playerClass.health);
        playerClass.stamina = EditorGUILayout.FloatField("Stamina", playerClass.stamina);
        playerClass.mana = EditorGUILayout.FloatField("Mana", playerClass.mana);
        playerClass.speed = EditorGUILayout.FloatField("Speed", playerClass.speed);

        EditorGUILayout.Space(3);

        // Survival stats
        EditorGUILayout.LabelField("Survival Stats", EditorStyles.boldLabel);
        playerClass.hunger = EditorGUILayout.FloatField("Hunger", playerClass.hunger);
        playerClass.thirst = EditorGUILayout.FloatField("Thirst", playerClass.thirst);
        playerClass.weight = EditorGUILayout.FloatField("Weight", playerClass.weight);
        playerClass.sleep = EditorGUILayout.FloatField("Sleep", playerClass.sleep);

        EditorGUILayout.Space(3);

        // Environmental stats
        EditorGUILayout.LabelField("Environmental Stats", EditorStyles.boldLabel);
        playerClass.sanity = EditorGUILayout.FloatField("Sanity", playerClass.sanity);
        playerClass.bodyHeat = EditorGUILayout.FloatField("Body Heat", playerClass.bodyHeat);
        playerClass.oxygen = EditorGUILayout.FloatField("Oxygen", playerClass.oxygen);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(playerClass);
        }
    }

    // Draw runtime debug tools
    private void DrawRuntimeDebugTools()
    {
        EditorGUILayout.LabelField("Runtime Debug", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Update UI"))
        {
            statusController.ForceUpdateConditionalUIs();
        }
        if (GUILayout.Button("Stop All Effects"))
        {
            statusController.StopAllEffects(false); // Stop debuffs
            statusController.StopAllEffects(true);  // Stop buffs
        }
        EditorGUILayout.EndHorizontal();

        // Environmental controls
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Environmental Controls", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Sleeping"))
        {
            statusController.StartSleeping();
        }
        if (GUILayout.Button("Stop Sleeping"))
        {
            statusController.StopSleeping();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Underwater"))
        {
            statusController.SetUnderwater(true);
        }
        if (GUILayout.Button("Set Above Water"))
        {
            statusController.SetUnderwater(false);
        }
        EditorGUILayout.EndHorizontal();

        // Status level display
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Current Status Levels", EditorStyles.miniBoldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Sleep Level", statusController.GetSleepinessLevel().ToString());
        EditorGUILayout.TextField("Sanity Level", statusController.GetSanityLevel().ToString());
        EditorGUILayout.TextField("Temperature Level", statusController.GetTemperatureLevel().ToString());
        EditorGUILayout.TextField("Oxygen Environment", statusController.GetOxygenEnvironment().ToString());
        EditorGUI.EndDisabledGroup();
    }

    // Draw validation results
    private void DrawValidationResults(SetupValidationResult result)
    {
        EditorGUILayout.LabelField("Validation Results", EditorStyles.miniBoldLabel);

        // Summary
        EditorGUILayout.LabelField($"✓ {result.SuccessCount} | ⚠ {result.WarningCount} | ✗ {result.ErrorCount}");

        // Errors
        if (result.ErrorCount > 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Errors:", EditorStyles.boldLabel);
            foreach (var error in result.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        // Warnings
        if (result.WarningCount > 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Warnings:", EditorStyles.boldLabel);
            foreach (var warning in result.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }

        // Successes (collapsed by default)
        if (result.SuccessCount > 0)
        {
            EditorGUILayout.Space(2);
            if (EditorGUILayout.Foldout(false, $"Successes ({result.SuccessCount})"))
            {
                foreach (var success in result.Successes)
                {
                    EditorGUILayout.LabelField($"✓ {success}");
                }
            }
        }
    }

    // Setup tool implementations
    private void AutoAssignAllComponents()
    {
        var result = setupHelper.AutoAssignComponents();
        Debug.Log($"[PlayerStatusEditor] Auto-assigned {result.AssignedCount} components");

        if (result.MissingCount > 0)
        {
            EditorUtility.DisplayDialog("Auto-Assignment Complete",
                $"Assigned {result.AssignedCount} components.\n{result.MissingCount} components are still missing and may need to be created.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Auto-Assignment Complete",
                $"Successfully assigned all {result.AssignedCount} components!", "OK");
        }

        EditorUtility.SetDirty(statusController);
    }

    private void CreateMissingComponents()
    {
        var result = setupHelper.CreateMissingComponents();
        Debug.Log($"[PlayerStatusEditor] Created {result.CreatedCount} components");

        if (result.CreatedCount > 0)
        {
            EditorUtility.DisplayDialog("Component Creation Complete",
                $"Created {result.CreatedCount} missing components.\nRun 'Auto-Assign All Components' to link them.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Component Creation Complete",
                "No missing components found - all components already exist!", "OK");
        }
    }

    private void RunCompleteSetupWizard()
    {
        Debug.Log("[PlayerStatusEditor] Running complete setup wizard...");

        // Step 1: Create missing components
        var creationResult = setupHelper.CreateMissingComponents();

        // Step 2: Auto-assign all components
        var assignmentResult = setupHelper.AutoAssignComponents();

        // Step 3: Validate setup
        var validationResult = setupHelper.ValidateSetup();

        // Step 4: Setup default values if needed
        if (statusController.CurrentPlayerClass != null)
        {
            setupHelper.SetupDefaultValues();
        }

        string message = $"Setup Wizard Complete!\n\n" +
                        $"Created: {creationResult.CreatedCount} components\n" +
                        $"Assigned: {assignmentResult.AssignedCount} components\n" +
                        $"Validation: {validationResult.SuccessCount} successes, {validationResult.WarningCount} warnings, {validationResult.ErrorCount} errors";

        if (validationResult.ErrorCount > 0)
        {
            message += "\n\nPlease check the validation results for remaining issues.";
        }

        EditorUtility.DisplayDialog("Setup Wizard", message, "OK");
        EditorUtility.SetDirty(statusController);
    }

    private void ResetAllReferences()
    {
        // Use reflection to reset all serialized fields to null
        var fields = typeof(PlayerStatusController).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.FieldType.IsSubclassOf(typeof(Component)))
            {
                field.SetValue(statusController, null);
            }
        }

        EditorUtility.SetDirty(statusController);
        Debug.Log("[PlayerStatusEditor] Reset all component references");
    }

    private void SetupStatusManagersOnly()
    {
        var result = setupHelper.AutoAssignComponents();
        var statusManagerCount = result.Assigned.Count(a => a.Contains("Manager") && !a.Contains("Experience") && !a.Contains("Trait"));

        Debug.Log($"[PlayerStatusEditor] Setup {statusManagerCount} status managers");
        EditorUtility.SetDirty(statusController);
    }

    private void SetupSystemManagersOnly()
    {
        var result = setupHelper.AutoAssignComponents();
        var systemManagerCount = result.Assigned.Count(a => a.Contains("ExperienceManager") || a.Contains("TraitManager"));

        Debug.Log($"[PlayerStatusEditor] Setup {systemManagerCount} system managers");
        EditorUtility.SetDirty(statusController);
    }

    private void SetupMovementModelsOnly()
    {
        var result = setupHelper.AutoAssignComponents();
        var modelCount = result.Assigned.Count(a => a.Contains("Model"));

        Debug.Log($"[PlayerStatusEditor] Setup {modelCount} movement models");
        EditorUtility.SetDirty(statusController);
    }

    private void ValidateStatusValues()
    {
        var result = setupHelper.ValidateStatusValues();
        string message = $"Status Value Validation:\n\n" +
                        $"Valid: {result.ValidCount}\n" +
                        $"Warnings: {result.WarningCount}\n" +
                        $"Errors: {result.ErrorCount}";

        if (result.ErrorCount > 0)
        {
            message += "\n\nErrors found - check console for details.";
            foreach (var error in result.Errors)
            {
                Debug.LogError($"[Status Values] {error}");
            }
        }

        EditorUtility.DisplayDialog("Status Value Validation", message, "OK");
    }

    private void ResetStatusValuesToZero()
    {
        if (statusController.CurrentPlayerClass == null) return;

        var playerClass = statusController.CurrentPlayerClass;
        playerClass.health = 0;
        playerClass.stamina = 0;
        playerClass.mana = 0;
        playerClass.speed = 0;
        playerClass.hunger = 0;
        playerClass.thirst = 0;
        playerClass.weight = 0;
        playerClass.sleep = 0;
        playerClass.sanity = 0;
        playerClass.bodyHeat = 0;
        playerClass.oxygen = 0;

        EditorUtility.SetDirty(playerClass);
        Debug.Log("[PlayerStatusEditor] Reset all status values to zero");
    }

    private void LogComponentHierarchy()
    {
        Debug.Log("[PlayerStatusEditor] Component hierarchy for " + statusController.name);
        var components = statusController.GetComponentsInChildren<Component>();

        foreach (var component in components)
        {
            Debug.Log($"  {component.GetType().Name} on {component.name}");
        }
    }

    private void FindAllStatusControllersInScene()
    {
        var controllers = UnityEngine.Object.FindObjectsByType<PlayerStatusController>(FindObjectsSortMode.None);
        Debug.Log($"[PlayerStatusEditor] Found {controllers.Length} PlayerStatusController(s) in scene:");

        foreach (var controller in controllers)
        {
            Debug.Log($"  {controller.name} - Class: {(controller.CurrentPlayerClass?.className ?? "None")}");
        }
    }
}

// Menu items for quick access
public class PlayerStatusEditorMenu
{
    [MenuItem("Tools/Player Status/Validate All Controllers")]
    static void ValidateAllControllers()
    {
        var controllers = UnityEngine.Object.FindObjectsByType<PlayerStatusController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            var helper = new PlayerStatusSetupHelper(controller);
            var result = helper.ValidateSetup();
            Debug.Log($"[{controller.name}] Validation: {result.SuccessCount} successes, {result.WarningCount} warnings, {result.ErrorCount} errors");
        }
    }

    [MenuItem("Tools/Player Status/Auto-Setup All Controllers")]
    static void AutoSetupAllControllers()
    {
        var controllers = UnityEngine.Object.FindObjectsByType<PlayerStatusController>(FindObjectsSortMode.None);
        int setupCount = 0;

        foreach (var controller in controllers)
        {
            var helper = new PlayerStatusSetupHelper(controller);
            helper.CreateMissingComponents();
            helper.AutoAssignComponents();
            setupCount++;
        }

        Debug.Log($"[PlayerStatusEditorMenu] Auto-setup completed for {setupCount} controllers");
        EditorUtility.DisplayDialog("Auto-Setup Complete", $"Completed auto-setup for {setupCount} PlayerStatusController(s)", "OK");
    }

    [MenuItem("Assets/Create/Player Status/Player Class Template")]
    static void CreatePlayerClassTemplate()
    {
        var template = ScriptableObject.CreateInstance<PlayerClass>();
        template.className = "New Player Class";
        template.health = 100f;
        template.stamina = 100f;
        template.mana = 100f;
        template.speed = 5f;
        template.hunger = 100f;
        template.thirst = 100f;
        template.weight = 50f;
        template.sleep = 100f;
        template.sanity = 100f;
        template.bodyHeat = 100f;
        template.oxygen = 100f;
        template.traitPoints = 10;

        AssetDatabase.CreateAsset(template, "Assets/New Player Class.asset");
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = template;
    }
}
#endif