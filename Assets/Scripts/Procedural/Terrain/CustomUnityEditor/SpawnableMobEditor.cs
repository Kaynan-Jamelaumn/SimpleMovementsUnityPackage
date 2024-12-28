using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpawnableMob))]
public class SpawnableMobDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Calculate initial positions for fields
        Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // Draw mobPrefab field
        SerializedProperty mobPrefabProp = property.FindPropertyRelative("mobPrefab");
        EditorGUI.PropertyField(rect, mobPrefabProp, new GUIContent("Mob Prefab"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Draw numeric fields for spawn configuration
        EditorGUI.PropertyField(rect, property.FindPropertyRelative("maxInstances"), new GUIContent("Max Instances"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rect, property.FindPropertyRelative("spawnWeight"), new GUIContent("Spawn Weight"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rect, property.FindPropertyRelative("spawnTime"), new GUIContent("Spawn Time"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rect, property.FindPropertyRelative("minSpawnTime"), new GUIContent("Min Spawn Time"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rect, property.FindPropertyRelative("maxSpawnTime"), new GUIContent("Max Spawn Time"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rect, property.FindPropertyRelative("shouldHaveRandomSpawnTime"), new GUIContent("Random Spawn Time?"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rect, property.FindPropertyRelative("weightToSpawnFactor"), new GUIContent("Weight to Spawn Factor"));
        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Draw allowedBiomes as checkboxes
        TerrainGenerator terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
        if (terrainGenerator != null && terrainGenerator.Biomes != null)
        {
            Biome[] biomes = terrainGenerator.Biomes;

            SerializedProperty allowedBiomesProp = property.FindPropertyRelative("allowedBiomes");

            EditorGUI.LabelField(rect, "Allowed Biomes");
            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            for (int i = 0; i < biomes.Length; i++)
            {
                Biome biome = biomes[i];
                bool isSelected = false;

                // Check if the biome is already in the allowedBiomes list
                for (int j = 0; j < allowedBiomesProp.arraySize; j++)
                {
                    if (allowedBiomesProp.GetArrayElementAtIndex(j).objectReferenceValue == biome)
                    {
                        isSelected = true;
                        break;
                    }
                }

                Rect toggleRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                bool newIsSelected = EditorGUI.ToggleLeft(toggleRect, biome.name, isSelected);

                // Update allowedBiomes based on selection
                if (newIsSelected && !isSelected)
                {
                    allowedBiomesProp.arraySize++;
                    allowedBiomesProp.GetArrayElementAtIndex(allowedBiomesProp.arraySize - 1).objectReferenceValue = biome;
                }
                else if (!newIsSelected && isSelected)
                {
                    for (int j = 0; j < allowedBiomesProp.arraySize; j++)
                    {
                        if (allowedBiomesProp.GetArrayElementAtIndex(j).objectReferenceValue == biome)
                        {
                            allowedBiomesProp.DeleteArrayElementAtIndex(j);
                            break;
                        }
                    }
                }

                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        else
        {
            EditorGUI.LabelField(rect, "No TerrainGenerator or Biomes Found");
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight * 9; // Fixed fields count
        TerrainGenerator terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();

        if (terrainGenerator != null && terrainGenerator.Biomes != null)
        {
            height += (terrainGenerator.Biomes.Length + 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        }
        return height;
    }
}
