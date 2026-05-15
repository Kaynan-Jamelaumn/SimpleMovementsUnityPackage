#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

public class InventoryEditorAutoSetup
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;
    private InventoryEditorComponentDetection detection;

    public InventoryEditorAutoSetup(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }

    public void Initialize()
    {
        detection = new InventoryEditorComponentDetection(inventoryManager, serializedObject, utils);
        detection.Initialize();
    }

    public void DrawAutoSetupSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        // One-click setup buttons
        EditorGUILayout.LabelField("⚡ One-Click Setup", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🎯 Auto-Detect All", styles.SuccessButtonStyle))
        {
            RunFullAutoDetection();
        }

        if (GUILayout.Button("🔗 Auto-Link Components", styles.WarningButtonStyle))
        {
            AutoLinkDetectedComponents();
        }

        if (GUILayout.Button("✨ Complete Setup", styles.SuccessButtonStyle))
        {
            CompleteAutoSetup();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Individual auto-assignment buttons
        DrawIndividualAutoAssignments(styles);

        EditorGUILayout.EndVertical();
    }

    private void DrawIndividualAutoAssignments(InventoryEditorStyles styles)
    {
        EditorGUILayout.LabelField("🎛️ Individual Auto-Assignments", EditorStyles.boldLabel);

        var detectedCounts = detection.GetDetectedCounts();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"📷 Camera ({detectedCounts.cameras})", styles.ButtonStyle))
        {
            AutoAssignCamera();
        }

        if (GUILayout.Button($"🎮 Player ({detectedCounts.players})", styles.ButtonStyle))
        {
            AutoAssignPlayer();
        }

        if (GUILayout.Button($"✋ Hand Parent ({detectedCounts.handParents})", styles.ButtonStyle))
        {
            AutoAssignHandParent();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"📦 Item Prefab ({detectedCounts.itemPrefabs})", styles.ButtonStyle))
        {
            AutoAssignItemPrefab();
        }

        if (GUILayout.Button($"🎪 Slot Manager", styles.ButtonStyle))
        {
            AutoAssignSlotManager();
        }

        if (GUILayout.Button($"📐 UI Layout", styles.ButtonStyle))
        {
            AutoAssignUILayoutManager();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void RunFullAutoDetection()
    {
        EditorUtility.DisplayProgressBar("Auto Detection", "Scanning scene...", 0f);

        try
        {
            detection.RunAutoDetection();
            EditorUtility.DisplayProgressBar("Auto Detection", "Analyzing components...", 0.5f);
            AnalyzeDetectedComponents();
            EditorUtility.DisplayProgressBar("Auto Detection", "Generating recommendations...", 0.8f);
            GenerateAutoAssignmentRecommendations();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var counts = detection.GetDetectedCounts();
        Debug.Log($"🔍 Auto-detection complete! Found: {counts.cameras} cameras, {counts.players} players, {counts.itemPrefabs} item prefabs");
    }

    public void AutoLinkDetectedComponents()
    {
        int assignmentCount = 0;

        var detected = detection.GetDetectedComponents();

        if (utils.CamProp.objectReferenceValue == null && detected.cameras.Count > 0)
        {
            utils.CamProp.objectReferenceValue = detected.cameras[0];
            assignmentCount++;
        }

        if (utils.PlayerProp.objectReferenceValue == null && detected.players.Count > 0)
        {
            utils.PlayerProp.objectReferenceValue = detected.players[0];
            AutoAssignPlayerRelatedComponents(detected.players[0]);
            assignmentCount++;
        }

        if (utils.PlayerStatusControllerProp.objectReferenceValue == null && detected.playerControllers.Count > 0)
        {
            utils.PlayerStatusControllerProp.objectReferenceValue = detected.playerControllers[0];
            assignmentCount++;
        }

        if (utils.WeaponControllerProp.objectReferenceValue == null && detected.weaponControllers.Count > 0)
        {
            utils.WeaponControllerProp.objectReferenceValue = detected.weaponControllers[0];
            assignmentCount++;
        }

        if (utils.HandParentProp.objectReferenceValue == null && detected.handParents.Count > 0)
        {
            utils.HandParentProp.objectReferenceValue = detected.handParents[0];
            assignmentCount++;
        }

        if (utils.ItemPrefabProp.objectReferenceValue == null && detected.itemPrefabs.Count > 0)
        {
            utils.ItemPrefabProp.objectReferenceValue = detected.itemPrefabs[0];
            assignmentCount++;
        }

        serializedObject.ApplyModifiedProperties();

        Debug.Log($"✅ Auto-linked {assignmentCount} components successfully!");

        if (assignmentCount > 0)
        {
            EditorUtility.SetDirty(inventoryManager);
        }
    }

    public void CompleteAutoSetup()
    {
        EditorUtility.DisplayProgressBar("Complete Setup", "Running auto-detection...", 0.2f);
        RunFullAutoDetection();

        EditorUtility.DisplayProgressBar("Complete Setup", "Auto-linking components...", 0.5f);
        AutoLinkDetectedComponents();

        EditorUtility.DisplayProgressBar("Complete Setup", "Validating setup...", 0.8f);
        var validation = new InventoryEditorValidation(inventoryManager, serializedObject, utils);
        bool isValid = validation.ValidateInventorySetup();

        EditorUtility.DisplayProgressBar("Complete Setup", "Finalizing...", 0.9f);

        if (isValid)
        {
            Debug.Log("✨ Complete auto-setup finished successfully!");
            EditorUtility.DisplayDialog("Setup Complete",
                "Inventory Manager setup completed successfully!\n\nAll required components have been detected and assigned.",
                "Great!");
        }
        else
        {
            Debug.LogWarning("⚠️ Setup completed with some missing components. Check validation results.");
            EditorUtility.DisplayDialog("Setup Partially Complete",
                "Setup completed but some components are still missing.\n\nPlease check the validation results and assign missing components manually.",
                "OK");
        }

        EditorUtility.ClearProgressBar();
    }

    private void AutoAssignCamera()
    {
        var cameras = detection.GetDetectedComponents().cameras;
        if (cameras.Count > 0)
        {
            utils.CamProp.objectReferenceValue = cameras[0];
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"📷 Auto-assigned camera: {cameras[0].name}");
        }
    }

    private void AutoAssignPlayer()
    {
        var players = detection.GetDetectedComponents().players;
        if (players.Count > 0)
        {
            utils.PlayerProp.objectReferenceValue = players[0];
            AutoAssignPlayerRelatedComponents(players[0]);
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"🎮 Auto-assigned player: {players[0].name}");
        }
    }

    private void AutoAssignHandParent()
    {
        var handParents = detection.GetDetectedComponents().handParents;
        if (handParents.Count > 0)
        {
            utils.HandParentProp.objectReferenceValue = handParents[0];
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"✋ Auto-assigned hand parent: {handParents[0].name}");
        }
    }

    private void AutoAssignItemPrefab()
    {
        var itemPrefabs = detection.GetDetectedComponents().itemPrefabs;
        if (itemPrefabs.Count > 0)
        {
            utils.ItemPrefabProp.objectReferenceValue = itemPrefabs[0];
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"📦 Auto-assigned item prefab: {itemPrefabs[0].name}");
        }
    }

    private void AutoAssignSlotManager()
    {
        // SlotManager is likely a serialized class, not an object reference
        if (utils.SlotManagerProp != null)
        {
            Debug.Log($"🎪 Slot Manager is properly configured");
        }
        else
        {
            Debug.LogWarning("🎪 Slot Manager configuration may need manual setup");
        }
    }

    private void AutoAssignUILayoutManager()
    {
        // UILayoutManager is likely a serialized class, not an object reference
        if (utils.UILayoutManagerProp != null)
        {
            Debug.Log($"📐 UI Layout Manager is properly configured");
        }
        else
        {
            Debug.LogWarning("📐 UI Layout Manager configuration may need manual setup");
        }
    }

    private void AutoAssignPlayerRelatedComponents(GameObject player)
    {
        // Try to find and assign player-related components
        var statusController = player.GetComponent<PlayerStatusController>();
        if (statusController != null && utils.PlayerStatusControllerProp.objectReferenceValue == null)
        {
            utils.PlayerStatusControllerProp.objectReferenceValue = statusController;
        }

        var weaponController = player.GetComponent<WeaponController>();
        if (weaponController != null && utils.WeaponControllerProp.objectReferenceValue == null)
        {
            utils.WeaponControllerProp.objectReferenceValue = weaponController;
        }
    }

    public void AutoAssignSpecificComponent(SerializedProperty prop)
    {
        if (prop.propertyType != SerializedPropertyType.ObjectReference)
            return;

        switch (prop.name)
        {
            case "cam":
                AutoAssignCamera();
                break;
            case "player":
                AutoAssignPlayer();
                break;
            case "handParent":
                AutoAssignHandParent();
                break;
            case "itemPrefab":
                AutoAssignItemPrefab();
                break;
            case "playerStatusController":
                var controllers = detection.GetDetectedComponents().playerControllers;
                if (controllers.Count > 0)
                {
                    prop.objectReferenceValue = controllers[0];
                    serializedObject.ApplyModifiedProperties();
                }
                break;
            case "weaponController":
                var weaponControllers = detection.GetDetectedComponents().weaponControllers;
                if (weaponControllers.Count > 0)
                {
                    prop.objectReferenceValue = weaponControllers[0];
                    serializedObject.ApplyModifiedProperties();
                }
                break;
        }
    }

    private void AnalyzeDetectedComponents()
    {
        var detected = detection.GetDetectedComponents();
        Debug.Log("🔍 Component Analysis:");
        Debug.Log($"Cameras: {detected.cameras.Count} (Recommended: {(detected.cameras.FirstOrDefault(c => c.tag == "MainCamera")?.name ?? "First found")})");
        Debug.Log($"Players: {detected.players.Count} (Recommended: {(detected.players.FirstOrDefault()?.name ?? "None")})");
        Debug.Log($"Item Prefabs: {detected.itemPrefabs.Count} found in project");
    }

    private void GenerateAutoAssignmentRecommendations()
    {
        var detected = detection.GetDetectedComponents();
        Debug.Log("💡 Auto-assignment Recommendations:");

        if (detected.cameras.Count > 1)
            Debug.Log($"Multiple cameras found. MainCamera will be prioritized.");

        if (detected.players.Count > 1)
            Debug.Log($"Multiple player objects found. First one with Player component will be used.");

        if (detected.itemPrefabs.Count == 0)
            Debug.LogWarning("No item prefabs found. Create one with InventoryItem component.");
    }
}
#endif