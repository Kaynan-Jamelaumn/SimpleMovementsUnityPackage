using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Main setup and validation helper for PlayerStatusController
// Handles component validation, auto-assignment, and setup verification
public class PlayerStatusSetupHelper
{
    private PlayerStatusController statusController;
    private bool enableDebugLogs;

    public PlayerStatusSetupHelper(PlayerStatusController controller, bool enableLogs = true)
    {
        statusController = controller;
        enableDebugLogs = enableLogs;
    }

    // Comprehensive setup validation - checks all required components
    public SetupValidationResult ValidateSetup()
    {
        var result = new SetupValidationResult();

        DebugLog("Starting PlayerStatusController setup validation...");

        // Validate core references
        ValidateCoreReferences(result);

        // Validate status managers
        ValidateStatusManagers(result);

        // Validate system managers
        ValidateSystemManagers(result);

        // Validate player class setup
        ValidatePlayerClassSetup(result);

        // Generate summary
        GenerateValidationSummary(result);

        return result;
    }

    // Auto-assign all missing components by searching the GameObject and its children
    public AutoAssignmentResult AutoAssignComponents()
    {
        var result = new AutoAssignmentResult();

        DebugLog("Starting auto-assignment of PlayerStatusController components...");

        // Auto-assign core references
        AutoAssignCoreReferences(result);

        // Auto-assign status managers
        AutoAssignStatusManagers(result);

        // Auto-assign system managers
        AutoAssignSystemManagers(result);

        // Generate summary
        GenerateAutoAssignmentSummary(result);

        return result;
    }

    // Create missing components that don't exist on the GameObject
    public ComponentCreationResult CreateMissingComponents()
    {
        var result = new ComponentCreationResult();

        DebugLog("Creating missing components for PlayerStatusController...");

        // Create missing status managers
        CreateMissingStatusManagers(result);

        // Create missing system managers
        CreateMissingSystemManagers(result);

        // Create missing models
        CreateMissingModels(result);

        GenerateCreationSummary(result);

        return result;
    }

    // Validate that all status values are within reasonable ranges
    public StatusValueValidationResult ValidateStatusValues()
    {
        var result = new StatusValueValidationResult();

        if (statusController.CurrentPlayerClass == null)
        {
            result.AddWarning("No PlayerClass assigned - cannot validate status values");
            return result;
        }

        var playerClass = statusController.CurrentPlayerClass;

        // Validate each status value
        ValidateStatusValue("Health", playerClass.health, result);
        ValidateStatusValue("Stamina", playerClass.stamina, result);
        ValidateStatusValue("Mana", playerClass.mana, result);
        ValidateStatusValue("Speed", playerClass.speed, result);
        ValidateStatusValue("Hunger", playerClass.hunger, result);
        ValidateStatusValue("Thirst", playerClass.thirst, result);
        ValidateStatusValue("Weight", playerClass.weight, result);
        ValidateStatusValue("Sleep", playerClass.sleep, result);
        ValidateStatusValue("Sanity", playerClass.sanity, result);
        ValidateStatusValue("Body Heat", playerClass.bodyHeat, result);
        ValidateStatusValue("Oxygen", playerClass.oxygen, result);

        return result;
    }

    // Setup default status values based on a template or reasonable defaults
    public void SetupDefaultValues(PlayerClass templateClass = null)
    {
        if (templateClass == null)
        {
            DebugLog("Setting up default status values...");
            SetupReasonableDefaults();
        }
        else
        {
            DebugLog($"Setting up status values from template: {templateClass.className}");
            ApplyTemplateValues(templateClass);
        }
    }

    // Validate core references (movement, roll, dash models)
    private void ValidateCoreReferences(SetupValidationResult result)
    {
        if (statusController.MovementModel == null)
            result.AddError("PlayerMovementModel is not assigned!");
        else
            result.AddSuccess("PlayerMovementModel is assigned");

        if (statusController.RollModel == null)
            result.AddWarning("PlayerRollModel is not assigned (optional)");
        else
            result.AddSuccess("PlayerRollModel is assigned");

        if (statusController.DashModel == null)
            result.AddWarning("PlayerDashModel is not assigned (optional)");
        else
            result.AddSuccess("PlayerDashModel is assigned");
    }

    // Validate all status managers
    private void ValidateStatusManagers(SetupValidationResult result)
    {
        var statusManagers = new Dictionary<string, Component>
        {
            { "StaminaManager", statusController.StaminaManager },
            { "HealthManager", statusController.HpManager },
            { "HungerManager", statusController.HungerManager },
            { "ThirstManager", statusController.ThirstManager },
            { "WeightManager", statusController.WeightManager },
            { "SpeedManager", statusController.SpeedManager },
            { "SleepManager", statusController.SleepManager },
            { "SanityManager", statusController.SanityManager },
            { "ManaManager", statusController.ManaManager },
            { "BodyHeatManager", statusController.BodyHeatManager },
            { "OxygenManager", statusController.OxygenManager }
        };

        foreach (var manager in statusManagers)
        {
            if (manager.Value == null)
                result.AddError($"{manager.Key} is not assigned!");
            else
                result.AddSuccess($"{manager.Key} is assigned");
        }
    }

    // Validate system managers
    private void ValidateSystemManagers(SetupValidationResult result)
    {
        if (statusController.XPManager == null)
            result.AddError("ExperienceManager is not assigned!");
        else
            result.AddSuccess("ExperienceManager is assigned");

        if (statusController.TraitManager == null)
            result.AddError("TraitManager is not assigned!");
        else
            result.AddSuccess("TraitManager is assigned");
    }

    // Validate player class setup
    private void ValidatePlayerClassSetup(SetupValidationResult result)
    {
        if (statusController.CurrentPlayerClass == null)
        {
            result.AddWarning("No PlayerClass assigned - player will need one at runtime");
        }
        else
        {
            result.AddSuccess($"PlayerClass assigned: {statusController.CurrentPlayerClass.className}");

            // Validate class has reasonable values
            var playerClass = statusController.CurrentPlayerClass;
            if (playerClass.health <= 0) result.AddWarning("PlayerClass has zero or negative health");
            if (playerClass.stamina <= 0) result.AddWarning("PlayerClass has zero or negative stamina");
            if (playerClass.speed <= 0) result.AddWarning("PlayerClass has zero or negative speed");
        }
    }

    // Auto-assign core model references
    private void AutoAssignCoreReferences(AutoAssignmentResult result)
    {
        if (statusController.MovementModel == null)
        {
            var movementModel = statusController.GetComponent<PlayerMovementModel>();
            if (movementModel != null)
            {
                SetPrivateField("movementModel", movementModel);
                result.AddAssignment("PlayerMovementModel", "Found on same GameObject");
            }
            else
            {
                result.AddMissing("PlayerMovementModel", "Component not found");
            }
        }

        if (statusController.RollModel == null)
        {
            var rollModel = statusController.GetComponent<PlayerRollModel>();
            if (rollModel != null)
            {
                SetPrivateField("rollModel", rollModel);
                result.AddAssignment("PlayerRollModel", "Found on same GameObject");
            }
        }

        if (statusController.DashModel == null)
        {
            var dashModel = statusController.GetComponent<PlayerDashModel>();
            if (dashModel != null)
            {
                SetPrivateField("dashModel", dashModel);
                result.AddAssignment("PlayerDashModel", "Found on same GameObject");
            }
        }
    }

    // Auto-assign status managers
    private void AutoAssignStatusManagers(AutoAssignmentResult result)
    {
        var managerTypes = new Dictionary<string, System.Type>
        {
            { "staminaManager", typeof(StaminaManager) },
            { "hpManager", typeof(HealthManager) },
            { "hungerManager", typeof(HungerManager) },
            { "thirstManager", typeof(ThirstManager) },
            { "weightManager", typeof(WeightManager) },
            { "speedManager", typeof(SpeedManager) },
            { "sleepManager", typeof(SleepManager) },
            { "sanityManager", typeof(SanityManager) },
            { "manaManager", typeof(ManaManager) },
            { "bodyHeatManager", typeof(BodyHeatManager) },
            { "oxygenManager", typeof(OxygenManager) }
        };

        foreach (var managerInfo in managerTypes)
        {
            TryAutoAssignComponent(managerInfo.Key, managerInfo.Value, result);
        }
    }

    // Auto-assign system managers
    private void AutoAssignSystemManagers(AutoAssignmentResult result)
    {
        TryAutoAssignComponent("xpManager", typeof(ExperienceManager), result);
        TryAutoAssignComponent("traitManager", typeof(TraitManager), result);
    }

    // Try to auto-assign a component by searching for it
    private void TryAutoAssignComponent(string fieldName, System.Type componentType, AutoAssignmentResult result)
    {
        var component = statusController.GetComponent(componentType);
        if (component != null)
        {
            SetPrivateField(fieldName, component);
            result.AddAssignment(componentType.Name, "Found on same GameObject");
        }
        else
        {
            // Try children
            component = statusController.GetComponentInChildren(componentType);
            if (component != null)
            {
                SetPrivateField(fieldName, component);
                result.AddAssignment(componentType.Name, $"Found on child: {component.name}");
            }
            else
            {
                result.AddMissing(componentType.Name, "Component not found in GameObject or children");
            }
        }
    }

    // Create missing status managers
    private void CreateMissingStatusManagers(ComponentCreationResult result)
    {
        var managerTypes = new System.Type[]
        {
            typeof(StaminaManager), typeof(HealthManager), typeof(HungerManager),
            typeof(ThirstManager), typeof(WeightManager), typeof(SpeedManager),
            typeof(SleepManager), typeof(SanityManager), typeof(ManaManager),
            typeof(BodyHeatManager), typeof(OxygenManager)
        };

        foreach (var managerType in managerTypes)
        {
            if (statusController.GetComponent(managerType) == null)
            {
                var component = statusController.gameObject.AddComponent(managerType);
                result.AddCreated(managerType.Name, "Added to PlayerStatusController GameObject");
            }
        }
    }

    // Create missing system managers
    private void CreateMissingSystemManagers(ComponentCreationResult result)
    {
        if (statusController.GetComponent<ExperienceManager>() == null)
        {
            statusController.gameObject.AddComponent<ExperienceManager>();
            result.AddCreated("ExperienceManager", "Added to PlayerStatusController GameObject");
        }

        if (statusController.GetComponent<TraitManager>() == null)
        {
            statusController.gameObject.AddComponent<TraitManager>();
            result.AddCreated("TraitManager", "Added to PlayerStatusController GameObject");
        }
    }

    // Create missing models
    private void CreateMissingModels(ComponentCreationResult result)
    {
        if (statusController.GetComponent<PlayerMovementModel>() == null)
        {
            statusController.gameObject.AddComponent<PlayerMovementModel>();
            result.AddCreated("PlayerMovementModel", "Added to PlayerStatusController GameObject");
        }

        if (statusController.GetComponent<PlayerRollModel>() == null)
        {
            statusController.gameObject.AddComponent<PlayerRollModel>();
            result.AddCreated("PlayerRollModel", "Added to PlayerStatusController GameObject");
        }

        if (statusController.GetComponent<PlayerDashModel>() == null)
        {
            statusController.gameObject.AddComponent<PlayerDashModel>();
            result.AddCreated("PlayerDashModel", "Added to PlayerStatusController GameObject");
        }
    }

    // Validate individual status value
    private void ValidateStatusValue(string statusName, float value, StatusValueValidationResult result)
    {
        if (value <= 0)
        {
            result.AddError($"{statusName} is zero or negative ({value})");
        }
        else if (value > 10000)
        {
            result.AddWarning($"{statusName} seems unusually high ({value})");
        }
        else
        {
            result.AddValid($"{statusName} has reasonable value ({value})");
        }
    }

    // Setup reasonable default values
    private void SetupReasonableDefaults()
    {
        if (statusController.CurrentPlayerClass == null)
        {
            DebugLog("No PlayerClass assigned - cannot set default values");
            return;
        }

        var playerClass = statusController.CurrentPlayerClass;

        // Set reasonable defaults if values are zero or negative
        if (playerClass.health <= 0) playerClass.health = 100f;
        if (playerClass.stamina <= 0) playerClass.stamina = 100f;
        if (playerClass.mana <= 0) playerClass.mana = 100f;
        if (playerClass.speed <= 0) playerClass.speed = 5f;
        if (playerClass.hunger <= 0) playerClass.hunger = 100f;
        if (playerClass.thirst <= 0) playerClass.thirst = 100f;
        if (playerClass.weight <= 0) playerClass.weight = 50f;
        if (playerClass.sleep <= 0) playerClass.sleep = 100f;
        if (playerClass.sanity <= 0) playerClass.sanity = 100f;
        if (playerClass.bodyHeat <= 0) playerClass.bodyHeat = 100f;
        if (playerClass.oxygen <= 0) playerClass.oxygen = 100f;

        DebugLog("Applied reasonable default values to PlayerClass");
    }

    // Apply values from a template class
    private void ApplyTemplateValues(PlayerClass template)
    {
        if (statusController.CurrentPlayerClass == null)
        {
            DebugLog("No PlayerClass assigned - cannot apply template values");
            return;
        }

        var playerClass = statusController.CurrentPlayerClass;

        playerClass.health = template.health;
        playerClass.stamina = template.stamina;
        playerClass.mana = template.mana;
        playerClass.speed = template.speed;
        playerClass.hunger = template.hunger;
        playerClass.thirst = template.thirst;
        playerClass.weight = template.weight;
        playerClass.sleep = template.sleep;
        playerClass.sanity = template.sanity;
        playerClass.bodyHeat = template.bodyHeat;
        playerClass.oxygen = template.oxygen;

        DebugLog($"Applied template values from {template.className}");
    }

    // Use reflection to set private fields during auto-assignment
    private void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(PlayerStatusController).GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(statusController, value);
        }
    }

    // Generate validation summary
    private void GenerateValidationSummary(SetupValidationResult result)
    {
        DebugLog($"Validation complete: {result.SuccessCount} successes, {result.WarningCount} warnings, {result.ErrorCount} errors");

        if (result.ErrorCount > 0)
        {
            DebugLogError("Setup validation found critical errors that need to be addressed!");
        }
        else if (result.WarningCount > 0)
        {
            DebugLogWarning("Setup validation found warnings - setup may be incomplete");
        }
        else
        {
            DebugLog("Setup validation passed - all components properly configured!");
        }
    }

    // Generate auto-assignment summary
    private void GenerateAutoAssignmentSummary(AutoAssignmentResult result)
    {
        DebugLog($"Auto-assignment complete: {result.AssignedCount} components assigned, {result.MissingCount} still missing");

        if (result.MissingCount > 0)
        {
            DebugLogWarning("Some components could not be auto-assigned and may need manual setup");
        }
        else
        {
            DebugLog("All components successfully auto-assigned!");
        }
    }

    // Generate creation summary
    private void GenerateCreationSummary(ComponentCreationResult result)
    {
        DebugLog($"Component creation complete: {result.CreatedCount} components created");

        if (result.CreatedCount > 0)
        {
            DebugLog("New components added - run auto-assignment to link them to PlayerStatusController");
        }
    }

    // Debug logging methods
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerStatusSetupHelper] {message}");
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[PlayerStatusSetupHelper] {message}");
    }

    private void DebugLogError(string message)
    {
        if (enableDebugLogs)
            Debug.LogError($"[PlayerStatusSetupHelper] {message}");
    }
}

// Result classes for different operations
public class SetupValidationResult
{
    public List<string> Successes = new List<string>();
    public List<string> Warnings = new List<string>();
    public List<string> Errors = new List<string>();

    public int SuccessCount => Successes.Count;
    public int WarningCount => Warnings.Count;
    public int ErrorCount => Errors.Count;
    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;

    public void AddSuccess(string message) => Successes.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddError(string message) => Errors.Add(message);
}

public class AutoAssignmentResult
{
    public List<string> Assigned = new List<string>();
    public List<string> Missing = new List<string>();

    public int AssignedCount => Assigned.Count;
    public int MissingCount => Missing.Count;

    public void AddAssignment(string componentName, string location) =>
        Assigned.Add($"{componentName}: {location}");
    public void AddMissing(string componentName, string reason) =>
        Missing.Add($"{componentName}: {reason}");
}

public class ComponentCreationResult
{
    public List<string> Created = new List<string>();

    public int CreatedCount => Created.Count;

    public void AddCreated(string componentName, string location) =>
        Created.Add($"{componentName}: {location}");
}

public class StatusValueValidationResult
{
    public List<string> ValidValues = new List<string>();
    public List<string> Warnings = new List<string>();
    public List<string> Errors = new List<string>();

    public int ValidCount => ValidValues.Count;
    public int WarningCount => Warnings.Count;
    public int ErrorCount => Errors.Count;
    public bool IsValid => ErrorCount == 0;

    public void AddValid(string message) => ValidValues.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddError(string message) => Errors.Add(message);
}