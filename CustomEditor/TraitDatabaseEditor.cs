
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TraitDatabase))]
public class TraitDatabaseEditor : Editor
{
    private TraitDatabase database;
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        database = (TraitDatabase)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Trait Database Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Show statistics
        var stats = database.GetTraitStatistics();
        EditorGUILayout.LabelField($"Total Traits: {stats["Total Traits"]}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Combat: {stats["Combat Traits"]}", EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField($"Survival: {stats["Survival Traits"]}", EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField($"Magic: {stats["Magic Traits"]}", EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Management buttons
        EditorGUILayout.LabelField("Management Tools:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-Find All Traits", GUILayout.Height(30)))
        {
            AutoFindAllTraits();
        }

        if (GUILayout.Button("Remove Null Traits", GUILayout.Height(30)))
        {
            database.RemoveNullTraits();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Create New Trait", GUILayout.Height(25)))
        {
            CreateNewTrait();
        }

        EditorGUILayout.Space(10);

        // Draw default inspector
        DrawDefaultInspector();

        // Apply changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(database);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void AutoFindAllTraits()
    {
        // Find all Trait assets in the project
        string[] guids = AssetDatabase.FindAssets("t:Trait");
        var allTraits = database.GetAllTraits();
        int addedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Trait trait = AssetDatabase.LoadAssetAtPath<Trait>(path);

            if (trait != null && !allTraits.Contains(trait))
            {
                database.AddTrait(trait);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            Debug.Log($"Added {addedCount} traits to the database.");
            EditorUtility.SetDirty(database);
        }
        else
        {
            Debug.Log("No new traits found to add.");
        }
    }

    private void CreateNewTrait()
    {
        // Create a new trait asset
        Trait newTrait = CreateInstance<Trait>();

        // Generate a unique name
        string assetPath = "Assets/NewTrait.asset";
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        // Create the asset
        AssetDatabase.CreateAsset(newTrait, uniquePath);
        AssetDatabase.SaveAssets();

        // Add to database
        database.AddTrait(newTrait);

        // Select it in the project
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newTrait;

        Debug.Log($"Created new trait: {uniquePath}");
    }
}