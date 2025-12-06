using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Custom inspector for MobActionsController with quick setup button.
/// </summary>
#if UNITY_EDITOR
[CustomEditor(typeof(MobActionsController))]
public class MobActionsControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MobActionsController mob = (MobActionsController)target;

        // Setup button at the top
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);

        if (GUILayout.Button("⚡ Auto-Configure All Components", GUILayout.Height(30)))
        {
            SetupMobComponents(mob.gameObject);
        }

        if (GUILayout.Button("📋 Check Missing Components", GUILayout.Height(25)))
        {
            CheckMissingComponents(mob.gameObject);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // Status indicators
        ShowComponentStatus(mob.gameObject);

        EditorGUILayout.Space(10);

        // Default inspector
        DrawDefaultInspector();
    }

    private void SetupMobComponents(GameObject mobObject)
    {
        Undo.RegisterCompleteObjectUndo(mobObject, "Setup Mob Components");

        int componentsAdded = 0;

        // Add Collider
        if (mobObject.GetComponent<Collider>() == null)
        {
            CapsuleCollider capsule = mobObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = new Vector3(0, 1f, 0);
            componentsAdded++;
        }

        // Add Rigidbody
        if (mobObject.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = mobObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
            componentsAdded++;
        }

        // Add NavMeshAgent
        if (mobObject.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            UnityEngine.AI.NavMeshAgent agent = mobObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = 3.5f;
            agent.stoppingDistance = 0.5f;
            componentsAdded++;
        }

        // Setup hierarchy
        Transform modelParent = mobObject.transform.Find("Model");
        if (modelParent == null)
        {
            GameObject model = new GameObject("Model");
            model.transform.SetParent(mobObject.transform);
            model.transform.localPosition = Vector3.zero;
            modelParent = model.transform;
            componentsAdded++;
        }

        // Ensure VisualModel exists
        Transform visual = modelParent.Find("VisualModel");
        if (visual == null)
        {
            GameObject visualObj = new GameObject("VisualModel");
            visualObj.transform.SetParent(modelParent);
            visualObj.transform.localPosition = Vector3.zero;
            visual = visualObj.transform;
            componentsAdded++;
        }

        // ALWAYS ensure Animator exists
        Animator existingAnimator = visual.GetComponent<Animator>();
        if (existingAnimator == null)
        {
            existingAnimator = visual.gameObject.AddComponent<Animator>();
            existingAnimator.applyRootMotion = false;
            componentsAdded++;
        }

        // Add StatusController
        if (mobObject.GetComponent<MobStatusController>() == null)
        {
            mobObject.AddComponent<MobStatusController>();
            componentsAdded++;
        }

        // Add StateMachine
        if (mobObject.GetComponent<MobMovementStateMachine>() == null)
        {
            MobMovementStateMachine stateMachine = mobObject.AddComponent<MobMovementStateMachine>();

            // Reuse existing modelParent and visual variables from above
            // They're already in scope and pointing to the correct objects
            Animator animator = null;
            if (modelParent != null && visual != null)
            {
                animator = visual.GetComponent<Animator>();
            }

            // Wire up all references using SerializedObject
            SerializedObject so = new SerializedObject(stateMachine);
            so.FindProperty("actionsController").objectReferenceValue = mobObject.GetComponent<MobActionsController>();
            so.FindProperty("mob").objectReferenceValue = mobObject.GetComponent<MobActionsController>();
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("navMeshAgent").objectReferenceValue = mobObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
            so.FindProperty("statusController").objectReferenceValue = mobObject.GetComponent<MobStatusController>();
            so.ApplyModifiedProperties();

            componentsAdded++;
        }

        EditorUtility.SetDirty(mobObject);

        if (componentsAdded > 0)
        {
            Debug.Log($"<color=green>✓ Added {componentsAdded} missing components to {mobObject.name}</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>All components already present on {mobObject.name}</color>");
        }
    }

    private void CheckMissingComponents(GameObject mobObject)
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine($"Component Check for: {mobObject.name}\n");

        bool allGood = true;

        // Check required components
        if (mobObject.GetComponent<Collider>() == null)
        {
            report.AppendLine("❌ Missing: Collider");
            allGood = false;
        }
        else
        {
            report.AppendLine("✓ Collider");
        }

        if (mobObject.GetComponent<Rigidbody>() == null)
        {
            report.AppendLine("❌ Missing: Rigidbody");
            allGood = false;
        }
        else
        {
            report.AppendLine("✓ Rigidbody");
        }

        if (mobObject.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            report.AppendLine("❌ Missing: NavMeshAgent");
            allGood = false;
        }
        else
        {
            report.AppendLine("✓ NavMeshAgent");
        }

        if (mobObject.GetComponent<MobStatusController>() == null)
        {
            report.AppendLine("❌ Missing: MobStatusController");
            allGood = false;
        }
        else
        {
            report.AppendLine("✓ MobStatusController");
        }

        if (mobObject.GetComponent<MobMovementStateMachine>() == null)
        {
            report.AppendLine("❌ Missing: MobMovementStateMachine");
            allGood = false;
        }
        else
        {
            report.AppendLine("✓ MobMovementStateMachine");
        }

        // Check hierarchy
        Transform modelParent = mobObject.transform.Find("Model");
        if (modelParent == null)
        {
            report.AppendLine("❌ Missing: Model/VisualModel hierarchy");
            allGood = false;
        }
        else
        {
            Transform visual = modelParent.Find("VisualModel");
            if (visual == null)
            {
                report.AppendLine("❌ Missing: VisualModel child");
                allGood = false;
            }
            else
            {
                Animator animator = visual.GetComponent<Animator>();
                if (animator == null)
                {
                    report.AppendLine("❌ Missing: Animator on VisualModel");
                    allGood = false;
                }
                else
                {
                    report.AppendLine("✓ Model/VisualModel/Animator hierarchy");
                    if (animator.runtimeAnimatorController == null)
                    {
                        report.AppendLine("⚠ Warning: Animator Controller not assigned (optional)");
                    }
                }
            }
        }

        if (allGood)
        {
            EditorUtility.DisplayDialog("Component Check",
                "✓ All required components are present!\n\n" + report.ToString(),
                "Great!");
        }
        else
        {
            EditorUtility.DisplayDialog("Component Check",
                report.ToString() + "\nUse 'Auto-Configure All Components' to fix.",
                "OK");
        }
    }

    private void ShowComponentStatus(GameObject mobObject)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Component Status", EditorStyles.boldLabel);

        ShowStatus("Collider", mobObject.GetComponent<Collider>() != null);
        ShowStatus("Rigidbody", mobObject.GetComponent<Rigidbody>() != null);
        ShowStatus("NavMeshAgent", mobObject.GetComponent<UnityEngine.AI.NavMeshAgent>() != null);
        ShowStatus("MobStatusController", mobObject.GetComponent<MobStatusController>() != null);
        ShowStatus("MobMovementStateMachine", mobObject.GetComponent<MobMovementStateMachine>() != null);

        // Check hierarchy and animator
        Transform modelParent = mobObject.transform.Find("Model");
        bool hierarchyGood = false;
        Animator foundAnimator = null;

        if (modelParent != null)
        {
            Transform visualModel = modelParent.Find("VisualModel");
            if (visualModel != null)
            {
                foundAnimator = visualModel.GetComponent<Animator>();
                hierarchyGood = foundAnimator != null;
            }
        }

        ShowStatus("Model Hierarchy", hierarchyGood);

        if (hierarchyGood && foundAnimator != null)
        {
            // Check if animator is assigned in state machine
            MobMovementStateMachine sm = mobObject.GetComponent<MobMovementStateMachine>();
            if (sm != null)
            {
                SerializedObject so = new SerializedObject(sm);
                SerializedProperty animProp = so.FindProperty("animator");
                bool animatorAssigned = animProp.objectReferenceValue != null;
                ShowStatus("Animator Assigned to StateMachine", animatorAssigned);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void ShowStatus(string label, bool isPresent)
    {
        EditorGUILayout.BeginHorizontal();
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = isPresent ? Color.green : Color.red;
        EditorGUILayout.LabelField(isPresent ? "✓" : "✗", style, GUILayout.Width(20));
        EditorGUILayout.LabelField(label);
        EditorGUILayout.EndHorizontal();
    }
}
#endif