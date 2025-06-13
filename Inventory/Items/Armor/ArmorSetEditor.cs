#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(ArmorSet))]
public class ArmorSetEditor : Editor
{
    private SerializedProperty setNameProp;
    private SerializedProperty setDescriptionProp;
    private SerializedProperty setIconProp;
    private SerializedProperty setColorProp;
    private SerializedProperty setPiecesProp;
    private SerializedProperty requiredSlotTypesProp;
    private SerializedProperty setEffectsProp;
    private SerializedProperty minimumPiecesProp;
    private SerializedProperty maximumPiecesProp;
    private SerializedProperty setCompleteSoundProp;
    private SerializedProperty setCompleteEffectProp;

    private void OnEnable()
    {
        setNameProp = serializedObject.FindProperty("setName");
        setDescriptionProp = serializedObject.FindProperty("setDescription");
        setIconProp = serializedObject.FindProperty("setIcon");
        setColorProp = serializedObject.FindProperty("setColor");
        setPiecesProp = serializedObject.FindProperty("setPieces");
        requiredSlotTypesProp = serializedObject.FindProperty("requiredSlotTypes");
        setEffectsProp = serializedObject.FindProperty("setEffects");
        minimumPiecesProp = serializedObject.FindProperty("minimumPiecesForSet");
        maximumPiecesProp = serializedObject.FindProperty("maximumSetPieces");
        setCompleteSoundProp = serializedObject.FindProperty("setCompleteSound");
        setCompleteEffectProp = serializedObject.FindProperty("setCompleteEffect");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ArmorSet armorSet = (ArmorSet)target;

        // Header
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Armor Set Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Basic Info
        EditorGUILayout.PropertyField(setNameProp);
        EditorGUILayout.PropertyField(setDescriptionProp);
        EditorGUILayout.PropertyField(setIconProp);
        EditorGUILayout.PropertyField(setColorProp);

        EditorGUILayout.Space();

        // Set Pieces Section
        EditorGUILayout.LabelField("Set Pieces", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(setPiecesProp, true);

        if (GUILayout.Button("Find and Assign Armor Pieces"))
        {
            FindAndAssignArmorPieces(armorSet);
        }

        EditorGUILayout.Space();

        // Effects Section
        EditorGUILayout.LabelField("Set Effects", EditorStyles.boldLabel);

        // Add button for creating new effects
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add New Effect"))
        {
            AddNewArmorSetEffectProperly();
        }
        if (GUILayout.Button("Create Sample Effects"))
        {
            CreateSampleEffects(armorSet);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(setEffectsProp, true);

        EditorGUILayout.Space();

        // Configuration
        EditorGUILayout.LabelField("Set Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(minimumPiecesProp);
        EditorGUILayout.PropertyField(maximumPiecesProp);

        EditorGUILayout.Space();

        // Audio & Visual
        EditorGUILayout.LabelField("Audio & Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(setCompleteSoundProp);
        EditorGUILayout.PropertyField(setCompleteEffectProp);

        EditorGUILayout.Space();

        // Effects Preview
        DrawEffectsPreview(armorSet);

        // Validation Section
        DrawValidationSection(armorSet);

        // Utility Buttons
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Validate Set"))
        {
            ValidateArmorSetDetailed(armorSet);
        }

        if (GUILayout.Button("Auto-populate Slot Types"))
        {
            AutoPopulateSlotTypes(armorSet);
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void AddNewArmorSetEffectProperly()
    {
        // Add new element to array
        int newIndex = setEffectsProp.arraySize;
        setEffectsProp.InsertArrayElementAtIndex(newIndex);

        var newEffect = setEffectsProp.GetArrayElementAtIndex(newIndex);

        // PROPERLY initialize all properties with correct default values
        var piecesRequired = newEffect.FindPropertyRelative("piecesRequired");
        var effectName = newEffect.FindPropertyRelative("effectName");
        var effectDescription = newEffect.FindPropertyRelative("effectDescription");
        var traitsToApply = newEffect.FindPropertyRelative("traitsToApply");
        var traitEnhancements = newEffect.FindPropertyRelative("traitEnhancements");
        var statBonuses = newEffect.FindPropertyRelative("statBonuses");
        var specialMechanics = newEffect.FindPropertyRelative("specialMechanics");
        var canStack = newEffect.FindPropertyRelative("canStack");
        var priority = newEffect.FindPropertyRelative("priority");
        var persistDuration = newEffect.FindPropertyRelative("persistDuration");

        // Set proper default values (matching the class defaults)
        piecesRequired.intValue = 2;
        effectName.stringValue = "New Set Bonus";
        effectDescription.stringValue = "Enter effect description here";
        canStack.boolValue = false;
        priority.intValue = 0;
        persistDuration.floatValue = 0f;

        // Clear any existing arrays to start fresh
        traitsToApply.ClearArray();
        traitEnhancements.ClearArray();
        statBonuses.ClearArray();
        specialMechanics.ClearArray();

        // Apply changes immediately
        serializedObject.ApplyModifiedProperties();

        // Mark as dirty to ensure Unity saves the changes
        EditorUtility.SetDirty(target);

        Debug.Log($"Added new set effect '{effectName.stringValue}' requiring {piecesRequired.intValue} pieces. Remember to configure the effect by adding stat bonuses, traits, or special mechanics!");
    }

    private void DrawEffectsPreview(ArmorSet armorSet)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Effects Preview", EditorStyles.boldLabel);

        if (armorSet.SetEffects.Count == 0)
        {
            EditorGUILayout.LabelField("No set effects defined.");
        }
        else
        {
            foreach (var effect in armorSet.SetEffects.OrderBy(e => e.piecesRequired))
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);

                string effectTitle = !string.IsNullOrEmpty(effect.effectName) ? effect.effectName : "Unnamed Effect";
                EditorGUILayout.LabelField($"{effect.piecesRequired} Pieces: {effectTitle}", EditorStyles.boldLabel);

                if (!string.IsNullOrEmpty(effect.effectDescription) && effect.effectDescription != "Enter effect description here")
                {
                    EditorGUILayout.LabelField(effect.effectDescription, EditorStyles.wordWrappedLabel);
                }

                // Show validation warnings for this effect
                var effectIssues = effect.ValidateConfiguration();
                if (effectIssues.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Issues: {string.Join("\n", effectIssues)}", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("✓ Effect properly configured", MessageType.Info);
                }

                // Show effect details
                if (effect.statBonuses.Count > 0)
                {
                    EditorGUILayout.LabelField("Stat Bonuses:", EditorStyles.miniBoldLabel);
                    foreach (var bonus in effect.statBonuses)
                    {
                        EditorGUILayout.LabelField($"  • {bonus.effectType}: {bonus.amount:+0.0;-0.0}");
                    }
                }

                if (effect.traitsToApply.Count > 0)
                {
                    EditorGUILayout.LabelField("Traits Applied:", EditorStyles.miniBoldLabel);
                    foreach (var trait in effect.traitsToApply.Where(t => t != null))
                    {
                        EditorGUILayout.LabelField($"  • {trait.Name}");
                    }
                }

                if (effect.specialMechanics.Count > 0)
                {
                    EditorGUILayout.LabelField("Special Mechanics:", EditorStyles.miniBoldLabel);
                    foreach (var mechanic in effect.specialMechanics)
                    {
                        EditorGUILayout.LabelField($"  • {mechanic.mechanicName}");
                    }
                }

                if (effect.traitEnhancements.Count > 0)
                {
                    EditorGUILayout.LabelField("Trait Enhancements:", EditorStyles.miniBoldLabel);
                    foreach (var enhancement in effect.traitEnhancements)
                    {
                        if (enhancement.originalTrait != null)
                        {
                            string enhanceType = enhancement.enhancementType.ToString();
                            EditorGUILayout.LabelField($"  • {enhancement.originalTrait.Name} ({enhanceType})");
                        }
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawValidationSection(ArmorSet armorSet)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        var issues = ArmorSetUtils.ValidateArmorSet(armorSet);
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("✓ Armor set validation passed!", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"⚠️ Found {issues.Count} validation issues:", MessageType.Warning);
            foreach (var issue in issues)
            {
                EditorGUILayout.LabelField($"  • {issue}");
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void CreateSampleEffects(ArmorSet armorSet)
    {
        // Clear existing effects first
        setEffectsProp.ClearArray();

        // Add sample 2-piece effect
        setEffectsProp.InsertArrayElementAtIndex(0);
        var effect2 = setEffectsProp.GetArrayElementAtIndex(0);
        InitializeEffectProperty(effect2, 2, "Warrior's Endurance (2 pieces)", "Increases health and provides basic protection.",
            new (EquippableEffectType, float)[] { (EquippableEffectType.MaxHp, 50f) });

        // Add sample 4-piece effect
        setEffectsProp.InsertArrayElementAtIndex(1);
        var effect4 = setEffectsProp.GetArrayElementAtIndex(1);
        InitializeEffectProperty(effect4, 4, "Warrior's Mastery (4 pieces)", "Complete set bonus providing enhanced combat capabilities.",
            new (EquippableEffectType, float)[] { (EquippableEffectType.MaxHp, 150f), (EquippableEffectType.MaxStamina, 100f) });

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(armorSet);
        Debug.Log("Sample effects created for " + armorSet.SetName);
    }

    private void InitializeEffectProperty(SerializedProperty effectProp, int pieces, string name, string description,
        (EquippableEffectType type, float amount)[] bonuses)
    {
        effectProp.FindPropertyRelative("piecesRequired").intValue = pieces;
        effectProp.FindPropertyRelative("effectName").stringValue = name;
        effectProp.FindPropertyRelative("effectDescription").stringValue = description;

        var statBonuses = effectProp.FindPropertyRelative("statBonuses");
        statBonuses.ClearArray();

        for (int i = 0; i < bonuses.Length; i++)
        {
            statBonuses.InsertArrayElementAtIndex(i);
            var bonus = statBonuses.GetArrayElementAtIndex(i);
            bonus.FindPropertyRelative("effectType").enumValueIndex = (int)bonuses[i].type;
            bonus.FindPropertyRelative("amount").floatValue = bonuses[i].amount;
        }
    }

    private void FindAndAssignArmorPieces(ArmorSet armorSet)
    {
        var armorPieces = AssetDatabase.FindAssets("t:ArmorSO")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmorSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(armor => armor != null && armor.BelongsToSet == armorSet)
            .ToList();

        if (armorPieces.Count > 0)
        {
            setPiecesProp.ClearArray();
            for (int i = 0; i < armorPieces.Count; i++)
            {
                setPiecesProp.InsertArrayElementAtIndex(i);
                setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue = armorPieces[i];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(armorSet);
            Debug.Log($"Found and assigned {armorPieces.Count} armor pieces to {armorSet.SetName}");
        }
        else
        {
            Debug.LogWarning($"No armor pieces found that reference {armorSet.SetName}");
        }
    }

    private void AutoPopulateSlotTypes(ArmorSet armorSet)
    {
        var slotTypes = armorSet.SetPieces.Where(piece => piece != null)
                                         .Select(piece => piece.ArmorSlotType)
                                         .Distinct()
                                         .ToList();

        requiredSlotTypesProp.ClearArray();
        for (int i = 0; i < slotTypes.Count; i++)
        {
            requiredSlotTypesProp.InsertArrayElementAtIndex(i);
            requiredSlotTypesProp.GetArrayElementAtIndex(i).enumValueIndex = (int)slotTypes[i];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(armorSet);
        Debug.Log($"Auto-populated {slotTypes.Count} slot types for {armorSet.SetName}");
    }

    private void ValidateArmorSetDetailed(ArmorSet armorSet)
    {
        var issues = ArmorSetUtils.ValidateArmorSet(armorSet);
        if (issues.Count == 0)
        {
            EditorUtility.DisplayDialog("Validation Success",
                $"Armor set '{armorSet.SetName}' passed all validation checks!", "OK");
        }
        else
        {
            string issueList = string.Join("\n", issues.Select(issue => $"• {issue}"));
            EditorUtility.DisplayDialog("Validation Issues",
                $"Found {issues.Count} issues in '{armorSet.SetName}':\n\n{issueList}", "OK");
        }
    }

    // Menu items for creating armor sets
    [MenuItem("Assets/Create/Armor System/Armor Set Only", false, 2)]
    public static void CreateArmorSetOnly()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Armor Set",
            "New_Armor_Set",
            "asset",
            "Choose location for armor set"
        );

        if (string.IsNullOrEmpty(path)) return;

        var armorSet = ScriptableObject.CreateInstance<ArmorSet>();
        AssetDatabase.CreateAsset(armorSet, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = armorSet;
        EditorGUIUtility.PingObject(armorSet);
    }

    [MenuItem("Tools/Armor System/Validate All Armor Sets")]
    public static void ValidateAllArmorSets()
    {
        var armorSets = AssetDatabase.FindAssets("t:ArmorSet")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmorSet>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(set => set != null)
            .ToList();

        int totalIssues = 0;
        foreach (var set in armorSets)
        {
            var issues = ArmorSetUtils.ValidateArmorSet(set);
            if (issues.Count > 0)
            {
                Debug.LogWarning($"Armor set '{set.SetName}' has {issues.Count} issues:");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"  • {issue}");
                }
                totalIssues += issues.Count;
            }
        }

        if (totalIssues == 0)
        {
            EditorUtility.DisplayDialog("Validation Complete",
                $"All {armorSets.Count} armor sets passed validation!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation Complete",
                $"Found {totalIssues} total issues across {armorSets.Count} armor sets. Check console for details.", "OK");
        }
    }
}

#endif