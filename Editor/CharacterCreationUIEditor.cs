#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CharacterCreationUI))]
public class CharacterCreationUIEditor : Editor
{
    private CharacterCreationUI characterUI;

    private void OnEnable()
    {
        characterUI = (CharacterCreationUI)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default inspector first
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Prefab Setup Tools
        EditorGUILayout.LabelField("Prefab Setup Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");

        // Check if player prefab is assigned
        SerializedProperty playerPrefabProp = serializedObject.FindProperty("playerPrefab");
        if (playerPrefabProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign a Player Prefab first to use the setup tools.", MessageType.Info);
        }
        else
        {
            GameObject playerPrefab = (GameObject)playerPrefabProp.objectReferenceValue;

            // Show prefab component status
            EditorGUILayout.LabelField("Prefab Component Status:", EditorStyles.miniLabel);

            var statusController = playerPrefab.GetComponent<PlayerStatusController>();
            var nameComponent = playerPrefab.GetComponent<PlayerNameComponent>();
            var traitManager = playerPrefab.GetComponent<TraitManager>();

            ShowComponentStatus("StatusController:", statusController != null);
            ShowComponentStatus("NameComponent:", nameComponent != null);
            ShowComponentStatus("TraitManager:", traitManager != null);

            EditorGUILayout.Space(5);

            // Auto-assign button
            if (GUILayout.Button("Auto-Assign Prefab References", GUILayout.Height(30)))
            {
                AutoAssignPrefabReferences();
            }

            EditorGUILayout.Space(5);

            // Manual assignment guide
            EditorGUILayout.LabelField("Manual Assignment Guide:", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "1. Drag your Player Prefab into the scene\n" +
                "2. Assign the components to the 'Prefab Component References' fields\n" +
                "3. Apply changes to the prefab\n" +
                "4. Delete the instance from the scene",
                MessageType.Info);

            // Show reference assignment status
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Reference Assignment Status:", EditorStyles.miniLabel);

            SerializedProperty statusControllerRef = serializedObject.FindProperty("prefabStatusController");
            SerializedProperty nameComponentRef = serializedObject.FindProperty("prefabNameComponent");
            SerializedProperty traitManagerRef = serializedObject.FindProperty("prefabTraitManager");

            ShowReferenceStatus("StatusController", statusControllerRef.objectReferenceValue != null);
            ShowReferenceStatus("NameComponent", nameComponentRef.objectReferenceValue != null);
            ShowReferenceStatus("TraitManager", traitManagerRef.objectReferenceValue != null);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // Validation tools
        EditorGUILayout.LabelField("Validation Tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Validate Setup", GUILayout.Height(25)))
        {
            characterUI.ValidatePrefabSetupManual();
        }

        if (GUILayout.Button("Test First Class", GUILayout.Height(25)))
        {
            characterUI.TestSelectFirstClass();
        }

        EditorGUILayout.EndHorizontal();

        // Apply changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(characterUI);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void AutoAssignPrefabReferences()
    {
        SerializedProperty playerPrefabProp = serializedObject.FindProperty("playerPrefab");
        GameObject playerPrefab = (GameObject)playerPrefabProp.objectReferenceValue;

        if (playerPrefab == null)
        {
            Debug.LogError("Cannot auto-assign - Player Prefab is not assigned!");
            return;
        }

        bool anyAssigned = false;

        // Auto-assign StatusController
        SerializedProperty statusControllerRef = serializedObject.FindProperty("prefabStatusController");
        if (statusControllerRef.objectReferenceValue == null)
        {
            var statusController = playerPrefab.GetComponent<PlayerStatusController>();
            if (statusController != null)
            {
                statusControllerRef.objectReferenceValue = statusController;
                anyAssigned = true;
            }
        }

        // Auto-assign NameComponent
        SerializedProperty nameComponentRef = serializedObject.FindProperty("prefabNameComponent");
        if (nameComponentRef.objectReferenceValue == null)
        {
            var nameComponent = playerPrefab.GetComponent<PlayerNameComponent>();
            if (nameComponent != null)
            {
                nameComponentRef.objectReferenceValue = nameComponent;
                anyAssigned = true;
            }
        }

        // Auto-assign TraitManager
        SerializedProperty traitManagerRef = serializedObject.FindProperty("prefabTraitManager");
        if (traitManagerRef.objectReferenceValue == null)
        {
            var traitManager = playerPrefab.GetComponent<TraitManager>();
            if (traitManager != null)
            {
                traitManagerRef.objectReferenceValue = traitManager;
                anyAssigned = true;
            }
        }

        if (anyAssigned)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(characterUI);
        }
    }

    private void ShowComponentStatus(string componentName, bool exists)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(componentName, GUILayout.Width(120));
        EditorGUILayout.LabelField(exists ? "✓ Found" : "✗ Missing",
            exists ? EditorStyles.label : EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();
    }

    private void ShowReferenceStatus(string componentName, bool isAssigned)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{componentName}:", GUILayout.Width(120));

        GUI.color = isAssigned ? Color.green : Color.red;
        EditorGUILayout.LabelField(isAssigned ? "✓ Assigned" : "✗ Not Assigned", EditorStyles.boldLabel);
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }
}
#endif