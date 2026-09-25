using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quick setup utility for creating fully configured mobs with all necessary components.
/// Access via: Right-click GameObject → Mob Setup → Quick Setup Mob
/// Or: Tools → Mob Setup → Create New Mob
/// </summary>
public class MobQuickSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Mob Setup/Create New Mob")]
    private static void CreateNewMob()
    {
        GameObject mobObject = new GameObject("NewMob");
        SetupMob(mobObject);
        Selection.activeGameObject = mobObject;
        EditorGUIUtility.PingObject(mobObject);
    }

    [MenuItem("GameObject/Mob Setup/Quick Setup Mob", false, 0)]
    private static void QuickSetupSelectedMob(MenuCommand menuCommand)
    {
        GameObject mobObject = menuCommand.context as GameObject;
        if (mobObject != null)
        {
            SetupMob(mobObject);
        }
    }

    [MenuItem("GameObject/Mob Setup/Quick Setup Mob", true)]
    private static bool ValidateQuickSetupMob()
    {
        return Selection.activeGameObject != null;
    }

    /// <summary>
    /// Main setup method that adds and configures all necessary components.
    /// </summary>
    private static void SetupMob(GameObject mobObject)
    {
        Undo.RegisterCompleteObjectUndo(mobObject, "Setup Mob");

        Debug.Log($"Setting up mob: {mobObject.name}");

        // Step 1: Add Collider if missing
        Collider collider = mobObject.GetComponent<Collider>();
        if (collider == null)
        {
            CapsuleCollider capsule = mobObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = new Vector3(0, 1f, 0);
            Debug.Log("✓ Added CapsuleCollider");
        }

        // Step 2: Add Rigidbody for physics
        Rigidbody rb = mobObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = mobObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Prevent rotation for NavMeshAgent
            Debug.Log("✓ Added Rigidbody");
        }

        // Step 3: Add NavMeshAgent
        NavMeshAgent agent = mobObject.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = mobObject.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = true;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            Debug.Log("✓ Added NavMeshAgent");
        }

        // Step 4: Setup Model/Animator hierarchy
        SetupModelHierarchy(mobObject);

        // Step 5: Add MobStatusController
        MobStatusController statusController = mobObject.GetComponent<MobStatusController>();
        if (statusController == null)
        {
            statusController = mobObject.AddComponent<MobStatusController>();

            // Add HealthManager if it doesn't exist
            HealthManager healthManager = mobObject.GetComponent<HealthManager>();
            if (healthManager == null)
            {
                healthManager = mobObject.AddComponent<HealthManager>();
                // Set health manager via reflection
                SerializedObject so = new SerializedObject(statusController);
                so.FindProperty("healthManager").objectReferenceValue = healthManager;
                so.ApplyModifiedProperties();
            }

            // Add SpeedManager if it doesn't exist
            SpeedManager speedManager = mobObject.GetComponent<SpeedManager>();
            if (speedManager == null)
            {
                speedManager = mobObject.AddComponent<SpeedManager>();
                // Set speed manager via reflection
                SerializedObject so = new SerializedObject(statusController);
                so.FindProperty("speedManager").objectReferenceValue = speedManager;
                so.ApplyModifiedProperties();
            }

            Debug.Log("✓ Added MobStatusController");
        }

        // Step 6: Add MobActionsController (main mob script)
        MobActionsController actionsController = mobObject.GetComponent<MobActionsController>();
        if (actionsController == null)
        {
            actionsController = mobObject.AddComponent<MobActionsController>();

            // Setup detection cast
            SerializedObject so = new SerializedObject(actionsController);
            SerializedProperty castProp = so.FindProperty("detectionCast");

            if (castProp != null)
            {
                castProp.FindPropertyRelative("castType").enumValueIndex = (int)CastBase.CastType.Sphere;
                castProp.FindPropertyRelative("castSize").floatValue = 10f;
                castProp.FindPropertyRelative("targetLayers").intValue = -1; // Everything
            }

            // Setup detection distances
            so.FindProperty("detectionDistance").vector3Value = new Vector3(2f, 2f, 2f);
            so.FindProperty("offSetDetectionDistance").vector3Value = new Vector3(0f, 1f, 1f);
            so.FindProperty("mobTransform").objectReferenceValue = mobObject.transform;

            // Setup mob type
            so.FindProperty("type").stringValue = "Mob";

            // Setup prey list
            SerializedProperty preysProp = so.FindProperty("Preys");
            if (preysProp != null && preysProp.arraySize == 0)
            {
                preysProp.arraySize = 1;
                preysProp.GetArrayElementAtIndex(0).stringValue = "Player";
            }

            so.ApplyModifiedProperties();
            Debug.Log("✓ Added MobActionsController");
        }

        // Step 7: Add MobMovementStateMachine
        MobMovementStateMachine stateMachine = mobObject.GetComponent<MobMovementStateMachine>();
        if (stateMachine == null)
        {
            stateMachine = mobObject.AddComponent<MobMovementStateMachine>();

            // Get the animator from the hierarchy
            Transform modelParent = mobObject.transform.Find("Model");
            Animator animator = null;
            if (modelParent != null)
            {
                Transform visualModel = modelParent.Find("VisualModel");
                if (visualModel != null)
                {
                    animator = visualModel.GetComponent<Animator>();
                    if (animator != null)
                    {
                        Debug.Log($"✓ Found Animator on {visualModel.name}");
                    }
                    else
                    {
                        Debug.LogWarning("⚠ Animator component not found on VisualModel!");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ VisualModel not found in Model hierarchy!");
                }
            }
            else
            {
                Debug.LogWarning("⚠ Model parent not found!");
            }

            // Wire up references
            SerializedObject so = new SerializedObject(stateMachine);
            so.FindProperty("actionsController").objectReferenceValue = actionsController;
            so.FindProperty("mob").objectReferenceValue = actionsController;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("navMeshAgent").objectReferenceValue = agent;
            so.FindProperty("statusController").objectReferenceValue = statusController;
            so.ApplyModifiedProperties();

            if (animator != null)
            {
                Debug.Log("✓ Added MobMovementStateMachine with Animator assigned");
            }
            else
            {
                Debug.LogError("✗ MobMovementStateMachine added but Animator is NULL!");
            }
        }

        // Step 8: Add MobAbilityController (optional)
        MobAbilityController abilityController = mobObject.GetComponent<MobAbilityController>();
        if (abilityController == null)
        {
            abilityController = mobObject.AddComponent<MobAbilityController>();
            Debug.Log("✓ Added MobAbilityController");
        }

        // Step 9: Configure default values
        ConfigureDefaultValues(mobObject, actionsController);

        EditorUtility.SetDirty(mobObject);
        Debug.Log($"<color=green>✓ Mob setup complete for: {mobObject.name}</color>");
        Debug.Log("Next steps:\n" +
                  "1. Assign an Animator Controller to the Model child\n" +
                  "2. Configure mob type and prey list\n" +
                  "3. Adjust detection ranges\n" +
                  "4. Set up patrol points (optional)");
    }

    /// <summary>
    /// Sets up the Model/Animator hierarchy required by the mob system.
    /// Expected structure: Mob -> Model -> VisualModel (with Animator)
    /// </summary>
    private static void SetupModelHierarchy(GameObject mobObject)
    {
        Transform modelParent = mobObject.transform.Find("Model");

        if (modelParent == null)
        {
            GameObject modelParentObj = new GameObject("Model");
            modelParentObj.transform.SetParent(mobObject.transform);
            modelParentObj.transform.localPosition = Vector3.zero;
            modelParent = modelParentObj.transform;
            Debug.Log("✓ Created Model parent");
        }

        Transform visualModel = modelParent.Find("VisualModel");

        if (visualModel == null)
        {
            GameObject visualModelObj = new GameObject("VisualModel");
            visualModelObj.transform.SetParent(modelParent);
            visualModelObj.transform.localPosition = Vector3.zero;
            visualModel = visualModelObj.transform;
            Debug.Log("✓ Created VisualModel");
        }

        // ALWAYS ensure Animator exists
        Animator animator = visualModel.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visualModel.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            Debug.Log("✓ Added Animator component");
        }

        // Add placeholder mesh if not exists
        MeshFilter meshFilter = visualModel.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = visualModel.gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = visualModel.gameObject.AddComponent<MeshRenderer>();

            // Create a simple capsule mesh as placeholder
            GameObject tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshFilter.sharedMesh = tempCapsule.GetComponent<MeshFilter>().sharedMesh;
            meshRenderer.sharedMaterial = tempCapsule.GetComponent<MeshRenderer>().sharedMaterial;
            DestroyImmediate(tempCapsule);

            Debug.Log("✓ Added placeholder mesh");
        }
    }

    /// <summary>
    /// Configures sensible default values for the mob.
    /// </summary>
    private static void ConfigureDefaultValues(GameObject mobObject, MobActionsController actionsController)
    {
        SerializedObject so = new SerializedObject(actionsController);

        // Wander settings
        so.FindProperty("wanderDistance").floatValue = 20f;
        so.FindProperty("maxWalkTime").floatValue = 6f;
        so.FindProperty("idleTime").floatValue = 3f;

        // Detection settings
        so.FindProperty("detectionRange").floatValue = 10f;

        // Prey settings
        so.FindProperty("escapeMaxDistance").floatValue = 30f;

        // Predator settings
        so.FindProperty("maxChaseTime").floatValue = 10f;
        so.FindProperty("biteDamage").intValue = 10;
        so.FindProperty("biteCooldown").floatValue = 1.5f;
        so.FindProperty("attackDistance").floatValue = 2f;
        so.FindProperty("isPartialWait").boolValue = false;
        so.FindProperty("playerHasMaxChaseTime").boolValue = true;

        so.ApplyModifiedProperties();
    }

    [MenuItem("Tools/Mob Setup/Add Missing Components")]
    private static void AddMissingComponents()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject first.", "OK");
            return;
        }

        SetupMob(selected);
    }

    [MenuItem("Tools/Mob Setup/Help")]
    private static void ShowHelp()
    {
        string help = @"MOB QUICK SETUP GUIDE

AUTOMATIC SETUP:
1. Right-click in Hierarchy → Mob Setup → Quick Setup Mob
   OR
2. Tools → Mob Setup → Create New Mob

WHAT IT ADDS:
✓ CapsuleCollider (0.5 radius, 2 height)
✓ Rigidbody (with rotation freeze)
✓ NavMeshAgent (3.5 speed, 0.5 stopping distance)
✓ Model hierarchy (Model/VisualModel with Animator)
✓ MobStatusController
✓ HealthManager
✓ SpeedManager
✓ MobActionsController (main mob script)
✓ MobMovementStateMachine (AI states)
✓ MobAbilityController (for abilities)
✓ Detection cast configuration

AFTER SETUP:
1. Assign Animator Controller to VisualModel
2. Configure mob type (Sheep, Wolf, Fox, etc.)
3. Set prey list (what this mob hunts)
4. Adjust detection ranges as needed
5. Optionally set patrol points
6. Replace placeholder capsule mesh with your model

STATES:
- Idle: Resting, scanning environment
- Moving: Walking to destination
- Chasing: Pursuing prey or fleeing predator
- Patrol: Following patrol points

TIPS:
- Use Layer Masks to filter detection
- Higher detection range = sees threats earlier
- Lower bite cooldown = attacks faster
- Set playerHasMaxChaseTime = false for persistent chase
";

        EditorUtility.DisplayDialog("Mob Setup Help", help, "Got it!");
    }
#endif
}