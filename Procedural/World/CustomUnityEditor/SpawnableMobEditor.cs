using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Alternative approach: Custom Editor for objects containing SpawnableMob.
/// Use this if the PropertyDrawer continues to have issues.
/// This would need to be attached to the container class (e.g., MobSettings).
/// </summary>
public class SpawnableMobEditor : EditorWindow
{
    [MenuItem("Tools/Spawnable Mob Helper")]
    public static void ShowWindow()
    {
        GetWindow<SpawnableMobEditor>("Mob Biome Helper");
    }

    private SerializedObject serializedObject;
    private SerializedProperty mobsProperty;
    private Vector2 scrollPosition;
    private TerrainGenerator terrainGenerator;
    private bool showHelp = true;

    private void OnEnable()
    {
        FindTerrainGenerator();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Spawnable Mob Biome Helper", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This tool helps you assign allowed biomes to your spawnable mobs. " +
            "Select a GameObject with a component that has a 'prefabs' field (List<SpawnableMob>) to get started.",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // Object field to select the container
        EditorGUI.BeginChangeCheck();
        GameObject selectedObject = (GameObject)EditorGUILayout.ObjectField(
            "Mob Container",
            serializedObject?.targetObject as GameObject,
            typeof(GameObject),
            true);

        if (EditorGUI.EndChangeCheck() && selectedObject != null)
        {
            // Try to find MobSettings or any component with prefabs field
            var components = selectedObject.GetComponents<Component>();
            foreach (var component in components)
            {
                SerializedObject so = new SerializedObject(component);
                SerializedProperty prop = so.FindProperty("prefabs");
                if (prop != null && prop.isArray)
                {
                    serializedObject = so;
                    mobsProperty = prop;
                    break;
                }
            }
        }

        if (serializedObject == null || mobsProperty == null)
        {
            EditorGUILayout.HelpBox("No valid mob container selected. Please select a GameObject with a component containing a 'prefabs' field (List<SpawnableMob>).",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(10);

        // Refresh TerrainGenerator
        if (GUILayout.Button("Refresh Terrain Generator", GUILayout.Height(30)))
        {
            FindTerrainGenerator();
        }

        if (terrainGenerator == null || terrainGenerator.BiomeDefinitions == null || terrainGenerator.BiomeDefinitions.Length == 0)
        {
            EditorGUILayout.HelpBox("No TerrainGenerator found in scene or no biomes defined!",
                MessageType.Error);
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Found {terrainGenerator.BiomeDefinitions.Length} biomes", EditorStyles.miniLabel);
        EditorGUILayout.Space(5);

        // Scroll view for mobs
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        serializedObject.Update();

        for (int mobIndex = 0; mobIndex < mobsProperty.arraySize; mobIndex++)
        {
            SerializedProperty mobProp = mobsProperty.GetArrayElementAtIndex(mobIndex);
            SerializedProperty mobPrefabProp = mobProp.FindPropertyRelative("mobPrefab");
            SerializedProperty allowedBiomesProp = mobProp.FindPropertyRelative("allowedBiomes");

            if (mobPrefabProp.objectReferenceValue == null)
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(mobPrefabProp.objectReferenceValue.name, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            // Draw biome checkboxes
            EditorGUILayout.LabelField("Allowed Biomes:", EditorStyles.miniBoldLabel);

            foreach (var biomeInstance in terrainGenerator.BiomeDefinitions)
            {
                if (biomeInstance == null || biomeInstance.BiomePrefab == null)
                    continue;

                Biome biome = biomeInstance.BiomePrefab;
                bool isSelected = IsBiomeInList(allowedBiomesProp, biome);

                EditorGUI.BeginChangeCheck();
                bool newIsSelected = EditorGUILayout.ToggleLeft(biome.name, isSelected);

                if (EditorGUI.EndChangeCheck())
                {
                    if (newIsSelected && !isSelected)
                    {
                        // Add biome
                        int newIndex = allowedBiomesProp.arraySize;
                        allowedBiomesProp.InsertArrayElementAtIndex(newIndex);
                        allowedBiomesProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = biome;
                    }
                    else if (!newIsSelected && isSelected)
                    {
                        // Remove biome
                        RemoveBiomeFromList(allowedBiomesProp, biome);
                    }
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();
    }

    private void FindTerrainGenerator()
    {
#if UNITY_2023_1_OR_NEWER
        terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
#else
            terrainGenerator = Object.FindObjectOfType<TerrainGenerator>();
#endif
    }

    private bool IsBiomeInList(SerializedProperty listProp, Biome biome)
    {
        for (int i = 0; i < listProp.arraySize; i++)
        {
            if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == biome)
            {
                return true;
            }
        }
        return false;
    }

    private void RemoveBiomeFromList(SerializedProperty listProp, Biome biome)
    {
        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == biome)
            {
                listProp.DeleteArrayElementAtIndex(i);
                if (i < listProp.arraySize && listProp.GetArrayElementAtIndex(i).objectReferenceValue == biome)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
                break;
            }
        }
    }
}