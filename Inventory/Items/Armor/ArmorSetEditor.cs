#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(ArmorSet))]
public class ArmorSetEditor : Editor
{
    // Serialized properties
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

    // Validation state
    private Dictionary<int, List<string>> effectValidationErrors = new Dictionary<int, List<string>>();
    private List<string> setValidationErrors = new List<string>();
    private List<string> setValidationWarnings = new List<string>();
    private bool hasValidationErrors = false;

    // UI state
    private bool[] effectFoldouts;
    private bool showDetailedEffectValidation = true;

    private void OnEnable()
    {
        // Find all properties
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

        // Initialize UI state
        if (setEffectsProp != null)
        {
            effectFoldouts = new bool[setEffectsProp.arraySize];
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ArmorSet armorSet = (ArmorSet)target;

        // Perform validation
        ValidateArmorSet(armorSet);

        // Header
        DrawHeader(armorSet);

        // Validation section
        DrawValidationSection();

        EditorGUILayout.Space();

        // Main sections
        DrawBasicInfoSection();
        EditorGUILayout.Space();

        DrawSetPiecesSection(armorSet);
        EditorGUILayout.Space();

        DrawSetEffectsSection(armorSet);
        EditorGUILayout.Space();

        DrawVisualsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void ValidateArmorSet(ArmorSet armorSet)
    {
        setValidationErrors.Clear();
        setValidationWarnings.Clear();
        effectValidationErrors.Clear();

        // Validate set name
        if (string.IsNullOrEmpty(setNameProp.stringValue))
        {
            setValidationErrors.Add("REQUIRED: Set must have a name!");
        }
        else
        {
            // Check name uniqueness
            var otherSets = AssetDatabase.FindAssets("t:ArmorSet")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ArmorSet>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(set => set != null && set != armorSet && set.SetName == armorSet.SetName)
                .ToList();

            if (otherSets.Count > 0)
            {
                setValidationErrors.Add($"REQUIRED: Set name '{armorSet.SetName}' is not unique! Another set already uses this name.");
            }
        }

        // Validate armor pieces
        if (setPiecesProp.arraySize == 0)
        {
            setValidationErrors.Add("REQUIRED: Set must contain at least ONE armor piece!");
        }
        else
        {
            // Check for null pieces
            int nullPieces = 0;
            for (int i = 0; i < setPiecesProp.arraySize; i++)
            {
                if (setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    nullPieces++;
            }

            if (nullPieces > 0)
            {
                setValidationErrors.Add($"Found {nullPieces} empty armor piece slot(s). Remove empty slots or assign armor pieces.");
            }

            // Validate that pieces reference this set
            for (int i = 0; i < setPiecesProp.arraySize; i++)
            {
                var piece = setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue as ArmorSO;
                if (piece != null && piece.BelongsToSet != armorSet)
                {
                    setValidationWarnings.Add($"Armor piece '{piece.name}' doesn't reference this set. Update its 'Belongs to Armor Set' field.");
                }
            }
        }

        // Validate set effects
        if (setEffectsProp.arraySize == 0)
        {
            setValidationErrors.Add("REQUIRED: Set must have at least ONE set effect!");
        }
        else
        {
            // Validate each effect
            for (int i = 0; i < setEffectsProp.arraySize; i++)
            {
                ValidateSetEffect(i);
            }
        }

        // Check description (optional)
        if (string.IsNullOrEmpty(setDescriptionProp.stringValue))
        {
            setValidationWarnings.Add("OPTIONAL: No description provided. Consider adding one for better player experience.");
        }

        hasValidationErrors = setValidationErrors.Count > 0 || effectValidationErrors.Count > 0;
    }

    private void ValidateSetEffect(int effectIndex)
    {
        var effectErrors = new List<string>();
        var effectProp = setEffectsProp.GetArrayElementAtIndex(effectIndex);

        var piecesRequiredProp = effectProp.FindPropertyRelative("piecesRequired");
        var effectNameProp = effectProp.FindPropertyRelative("effectName");
        var traitsToApplyProp = effectProp.FindPropertyRelative("traitsToApply");
        var traitEnhancementsProp = effectProp.FindPropertyRelative("traitEnhancements");
        var statBonusesProp = effectProp.FindPropertyRelative("statBonuses");
        var specialMechanicsProp = effectProp.FindPropertyRelative("specialMechanics");

        // Check pieces required
        if (piecesRequiredProp.intValue < 1)
        {
            effectErrors.Add($"Pieces required is {piecesRequiredProp.intValue}. Must be at least 1.");
        }

        // Check effect name
        if (string.IsNullOrEmpty(effectNameProp.stringValue) ||
            effectNameProp.stringValue == "New Set Bonus" ||
            effectNameProp.stringValue == "Set Bonus")
        {
            effectErrors.Add("Effect needs a descriptive name (e.g., 'Warrior's Vigor', 'Mage's Focus')");
        }

        // Check if effect has any actual effects
        bool hasTraits = traitsToApplyProp.arraySize > 0;
        bool hasEnhancements = traitEnhancementsProp.arraySize > 0;
        bool hasStats = statBonusesProp.arraySize > 0;
        bool hasSpecialMechanics = specialMechanicsProp.arraySize > 0;

        if (!hasTraits && !hasEnhancements && !hasStats && !hasSpecialMechanics)
        {
            effectErrors.Add("Effect must have at least ONE of: Stat Bonuses, Traits to Apply, Trait Enhancements, or Special Mechanics");
        }
        else
        {
            // Validate individual components
            // Check traits
            if (hasTraits)
            {
                int nullTraits = 0;
                for (int i = 0; i < traitsToApplyProp.arraySize; i++)
                {
                    if (traitsToApplyProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        nullTraits++;
                }
                if (nullTraits > 0)
                {
                    effectErrors.Add($"{nullTraits} empty trait slot(s) in 'Traits to Apply'");
                }
            }

            // Check stat bonuses
            if (hasStats)
            {
                int zeroStats = 0;
                for (int i = 0; i < statBonusesProp.arraySize; i++)
                {
                    var statBonus = statBonusesProp.GetArrayElementAtIndex(i);
                    var amountProp = statBonus.FindPropertyRelative("amount");
                    if (amountProp != null && amountProp.floatValue == 0)
                        zeroStats++;
                }
                if (zeroStats > 0)
                {
                    effectErrors.Add($"{zeroStats} stat bonus(es) have 0 amount and won't affect gameplay");
                }
            }

            // Check trait enhancements
            if (hasEnhancements)
            {
                for (int i = 0; i < traitEnhancementsProp.arraySize; i++)
                {
                    var enhancement = traitEnhancementsProp.GetArrayElementAtIndex(i);
                    var originalTraitProp = enhancement.FindPropertyRelative("originalTrait");
                    var enhancedTraitProp = enhancement.FindPropertyRelative("enhancedTrait");
                    var enhancementTypeProp = enhancement.FindPropertyRelative("enhancementType");

                    if (originalTraitProp.objectReferenceValue == null)
                    {
                        effectErrors.Add("Trait enhancement missing original trait");
                    }
                    else if ((TraitEnhancementType)enhancementTypeProp.enumValueIndex == TraitEnhancementType.Upgrade &&
                             enhancedTraitProp.objectReferenceValue == null)
                    {
                        effectErrors.Add("Trait enhancement set to 'Upgrade' but no enhanced trait selected");
                    }
                }
            }

            // Check special mechanics
            if (hasSpecialMechanics)
            {
                for (int i = 0; i < specialMechanicsProp.arraySize; i++)
                {
                    var mechanic = specialMechanicsProp.GetArrayElementAtIndex(i);
                    var mechanicIdProp = mechanic.FindPropertyRelative("mechanicId");
                    var mechanicNameProp = mechanic.FindPropertyRelative("mechanicName");

                    if (string.IsNullOrEmpty(mechanicIdProp.stringValue))
                    {
                        effectErrors.Add("Special mechanic missing ID (e.g., 'double_jump', 'water_walking')");
                    }
                    if (string.IsNullOrEmpty(mechanicNameProp.stringValue))
                    {
                        effectErrors.Add("Special mechanic missing display name");
                    }
                }
            }
        }

        if (effectErrors.Count > 0)
        {
            effectValidationErrors[effectIndex] = effectErrors;
        }
    }

    private void DrawHeader(ArmorSet armorSet)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Armor Set: {armorSet.SetName}", EditorStyles.boldLabel);

        if (GUILayout.Button("Validate", GUILayout.Width(80)))
        {
            ValidateArmorSet(armorSet);
            showDetailedEffectValidation = true;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawValidationSection()
    {
        bool hasErrors = setValidationErrors.Count > 0 || effectValidationErrors.Count > 0;
        bool hasWarnings = setValidationWarnings.Count > 0;

        if (!hasErrors && !hasWarnings)
        {
            EditorGUILayout.HelpBox("✓ All validation checks passed!", MessageType.Info);
            return;
        }

        // Set-level errors
        if (setValidationErrors.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("❌ SET VALIDATION ERRORS (Must Fix)", EditorStyles.boldLabel);

            foreach (string error in setValidationErrors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // Effect-level errors
        if (effectValidationErrors.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("❌ EFFECT VALIDATION ERRORS", EditorStyles.boldLabel);

            foreach (var kvp in effectValidationErrors)
            {
                int effectIndex = kvp.Key;
                var errors = kvp.Value;

                var effectProp = setEffectsProp.GetArrayElementAtIndex(effectIndex);
                var nameProp = effectProp.FindPropertyRelative("effectName");
                var piecesReqProp = effectProp.FindPropertyRelative("piecesRequired");

                string effectName = string.IsNullOrEmpty(nameProp.stringValue) ? $"Effect #{effectIndex + 1}" : nameProp.stringValue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Effect: {effectName} ({piecesReqProp.intValue} pieces)", EditorStyles.miniBoldLabel);

                foreach (string error in errors)
                {
                    EditorGUILayout.LabelField($"  • {error}", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // Warnings
        if (setValidationWarnings.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("⚠️ Warnings (Optional)", EditorStyles.boldLabel);

            foreach (string warning in setValidationWarnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawBasicInfoSection()
    {
        EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(setNameProp, new GUIContent("Set Name (Required)"));
        EditorGUILayout.PropertyField(setDescriptionProp, new GUIContent("Description (Optional)"));
        EditorGUILayout.PropertyField(setIconProp);
        EditorGUILayout.PropertyField(setColorProp);

        EditorGUILayout.EndVertical();
    }

    private void DrawSetPiecesSection(ArmorSet armorSet)
    {
        EditorGUILayout.LabelField("Set Pieces (Required: At least 1)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (setPiecesProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("⚠️ No armor pieces! Add at least ONE armor piece to this set.", MessageType.Error);
        }

        // Buttons
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Find Armor Referencing This Set"))
        {
            FindAndAssignArmorPieces(armorSet);
        }

        if (GUILayout.Button("Clean Null References"))
        {
            CleanNullReferences();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(setPiecesProp, true);

        // Show piece summary
        if (setPiecesProp.arraySize > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Piece Summary:", EditorStyles.miniBoldLabel);

            for (int i = 0; i < setPiecesProp.arraySize; i++)
            {
                var piece = setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue as ArmorSO;
                if (piece != null)
                {
                    string status = piece.BelongsToSet == armorSet ? "✓" : "⚠️";
                    EditorGUILayout.LabelField($"  {status} {piece.name} ({piece.ArmorSlotType})", EditorStyles.miniLabel);
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSetEffectsSection(ArmorSet armorSet)
    {
        EditorGUILayout.LabelField("Set Effects (Required: At least 1)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (setEffectsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("⚠️ No set effects! Add at least ONE effect that provides bonuses when pieces are equipped.", MessageType.Error);
        }

        // Buttons
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add New Effect"))
        {
            AddNewEffect();
        }

        if (GUILayout.Button("Add Sample Effects"))
        {
            CreateSampleEffects();
        }

        EditorGUILayout.EndHorizontal();

        // Draw each effect with enhanced UI
        for (int i = 0; i < setEffectsProp.arraySize; i++)
        {
            DrawSetEffect(i);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSetEffect(int index)
    {
        var effectProp = setEffectsProp.GetArrayElementAtIndex(index);
        var effectNameProp = effectProp.FindPropertyRelative("effectName");
        var piecesReqProp = effectProp.FindPropertyRelative("piecesRequired");

        string effectName = string.IsNullOrEmpty(effectNameProp.stringValue) ? $"Effect #{index + 1}" : effectNameProp.stringValue;
        bool hasErrors = effectValidationErrors.ContainsKey(index);

        // Header with validation indicator
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        string headerText = $"{(hasErrors ? "❌" : "✓")} {effectName} ({piecesReqProp.intValue} pieces)";
        effectFoldouts[index] = EditorGUILayout.Foldout(effectFoldouts[index], headerText, true);

        if (GUILayout.Button("Remove", GUILayout.Width(60)))
        {
            setEffectsProp.DeleteArrayElementAtIndex(index);
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (effectFoldouts[index])
        {
            EditorGUI.indentLevel++;

            // Show validation errors for this effect
            if (hasErrors && showDetailedEffectValidation)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("Fix these issues:", EditorStyles.miniBoldLabel);

                foreach (string error in effectValidationErrors[index])
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            // Basic properties
            EditorGUILayout.PropertyField(effectNameProp, new GUIContent("Effect Name"));
            EditorGUILayout.PropertyField(piecesReqProp, new GUIContent("Pieces Required"));
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("effectDescription"));

            EditorGUILayout.Space();

            // Effect components with helper buttons
            DrawEffectComponent(effectProp, "Stat Bonuses", "statBonuses", () => {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("+50 Health"), false, () => AddStatBonus(effectProp, EquippableEffectType.MaxHp, 50f));
                menu.AddItem(new GUIContent("+30 Stamina"), false, () => AddStatBonus(effectProp, EquippableEffectType.MaxStamina, 30f));
                menu.AddItem(new GUIContent("+5 Defense"), false, () => AddStatBonus(effectProp, EquippableEffectType.Defense, 5f));
                menu.AddItem(new GUIContent("+10% Speed"), false, () => AddStatBonus(effectProp, EquippableEffectType.Speed, 0.1f));
                menu.ShowAsContext();
            });

            DrawEffectComponent(effectProp, "Traits to Apply", "traitsToApply", null);
            DrawEffectComponent(effectProp, "Trait Enhancements", "traitEnhancements", null);
            DrawEffectComponent(effectProp, "Special Mechanics", "specialMechanics", () => {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Double Jump"), false, () => AddSpecialMechanic(effectProp, "double_jump", "Double Jump"));
                menu.AddItem(new GUIContent("Water Walking"), false, () => AddSpecialMechanic(effectProp, "water_walking", "Water Walking"));
                menu.AddItem(new GUIContent("Gravity Reduction"), false, () => AddSpecialMechanic(effectProp, "gravity_reduction", "Reduced Gravity"));
                menu.ShowAsContext();
            });

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEffectComponent(SerializedProperty effectProp, string label, string propertyName, System.Action addButtonAction)
    {
        var prop = effectProp.FindPropertyRelative(propertyName);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (addButtonAction != null && GUILayout.Button("Add Common", GUILayout.Width(100)))
        {
            addButtonAction();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(prop, GUIContent.none, true);
        EditorGUILayout.Space();
    }

    private void DrawVisualsSection()
    {
        EditorGUILayout.LabelField("Visual & Audio", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(setCompleteSoundProp);
        EditorGUILayout.PropertyField(setCompleteEffectProp);

        EditorGUILayout.EndVertical();
    }

    // Helper methods
    private void AddNewEffect()
    {
        int newIndex = setEffectsProp.arraySize;
        setEffectsProp.InsertArrayElementAtIndex(newIndex);

        var newEffect = setEffectsProp.GetArrayElementAtIndex(newIndex);
        newEffect.FindPropertyRelative("piecesRequired").intValue = 2;
        newEffect.FindPropertyRelative("effectName").stringValue = $"New Bonus ({2} pieces)";
        newEffect.FindPropertyRelative("effectDescription").stringValue = "Enter effect description";

        // Expand the new effect
        if (effectFoldouts.Length <= newIndex)
        {
            System.Array.Resize(ref effectFoldouts, newIndex + 1);
        }
        effectFoldouts[newIndex] = true;

        serializedObject.ApplyModifiedProperties();
    }

    private void CreateSampleEffects()
    {
        setEffectsProp.ClearArray();

        // 2-piece effect
        setEffectsProp.InsertArrayElementAtIndex(0);
        var effect2 = setEffectsProp.GetArrayElementAtIndex(0);
        effect2.FindPropertyRelative("piecesRequired").intValue = 2;
        effect2.FindPropertyRelative("effectName").stringValue = "Partial Set Bonus";
        effect2.FindPropertyRelative("effectDescription").stringValue = "Basic bonuses for wearing 2 pieces";

        var statBonuses2 = effect2.FindPropertyRelative("statBonuses");
        statBonuses2.InsertArrayElementAtIndex(0);
        var stat1 = statBonuses2.GetArrayElementAtIndex(0);
        stat1.FindPropertyRelative("effectType").enumValueIndex = (int)EquippableEffectType.MaxHp;
        stat1.FindPropertyRelative("amount").floatValue = 50f;
        stat1.FindPropertyRelative("effectDescription").stringValue = "+50 Maximum Health";

        // 4-piece effect
        setEffectsProp.InsertArrayElementAtIndex(1);
        var effect4 = setEffectsProp.GetArrayElementAtIndex(1);
        effect4.FindPropertyRelative("piecesRequired").intValue = 4;
        effect4.FindPropertyRelative("effectName").stringValue = "Full Set Bonus";
        effect4.FindPropertyRelative("effectDescription").stringValue = "Complete set bonuses";

        var statBonuses4 = effect4.FindPropertyRelative("statBonuses");
        statBonuses4.InsertArrayElementAtIndex(0);
        var stat2 = statBonuses4.GetArrayElementAtIndex(0);
        stat2.FindPropertyRelative("effectType").enumValueIndex = (int)EquippableEffectType.MaxHp;
        stat2.FindPropertyRelative("amount").floatValue = 150f;
        stat2.FindPropertyRelative("effectDescription").stringValue = "+150 Maximum Health";

        statBonuses4.InsertArrayElementAtIndex(1);
        var stat3 = statBonuses4.GetArrayElementAtIndex(1);
        stat3.FindPropertyRelative("effectType").enumValueIndex = (int)EquippableEffectType.Defense;
        stat3.FindPropertyRelative("amount").floatValue = 10f;
        stat3.FindPropertyRelative("effectDescription").stringValue = "+10 Defense";

        // Update foldouts
        System.Array.Resize(ref effectFoldouts, 2);
        effectFoldouts[0] = true;
        effectFoldouts[1] = true;

        serializedObject.ApplyModifiedProperties();
    }

    private void AddStatBonus(SerializedProperty effectProp, EquippableEffectType type, float amount)
    {
        var statBonuses = effectProp.FindPropertyRelative("statBonuses");
        int newIndex = statBonuses.arraySize;
        statBonuses.InsertArrayElementAtIndex(newIndex);

        var newBonus = statBonuses.GetArrayElementAtIndex(newIndex);
        newBonus.FindPropertyRelative("effectType").enumValueIndex = (int)type;
        newBonus.FindPropertyRelative("amount").floatValue = amount;

        serializedObject.ApplyModifiedProperties();
    }

    private void AddSpecialMechanic(SerializedProperty effectProp, string mechanicId, string mechanicName)
    {
        var mechanics = effectProp.FindPropertyRelative("specialMechanics");
        int newIndex = mechanics.arraySize;
        mechanics.InsertArrayElementAtIndex(newIndex);

        var newMechanic = mechanics.GetArrayElementAtIndex(newIndex);
        newMechanic.FindPropertyRelative("mechanicId").stringValue = mechanicId;
        newMechanic.FindPropertyRelative("mechanicName").stringValue = mechanicName;
        newMechanic.FindPropertyRelative("mechanicDescription").stringValue = $"Enables {mechanicName}";

        serializedObject.ApplyModifiedProperties();
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

    private void CleanNullReferences()
    {
        int originalCount = setPiecesProp.arraySize;

        for (int i = setPiecesProp.arraySize - 1; i >= 0; i--)
        {
            if (setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                setPiecesProp.DeleteArrayElementAtIndex(i);
            }
        }

        int removed = originalCount - setPiecesProp.arraySize;
        if (removed > 0)
        {
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Removed {removed} null references");
        }
    }
}
#endif