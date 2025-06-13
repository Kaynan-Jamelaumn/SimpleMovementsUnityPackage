#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(ArmorSO))]
public class ArmorSOEditor : Editor
{
    // Serialized properties
    private SerializedProperty armorSlotTypeProp;
    private SerializedProperty defenseValueProp;
    private SerializedProperty magicDefenseValueProp;
    private SerializedProperty durabilityModifierProp;
    private SerializedProperty inherentTraitsProp;
    private SerializedProperty applyTraitsWhenEquippedProp;
    private SerializedProperty armorModelProp;
    private SerializedProperty equipArmorSoundProp;
    private SerializedProperty unequipArmorSoundProp;
    private SerializedProperty effectsProp;
    private SerializedProperty belongsToArmorSetProp;

    // Validation state
    private bool hasValidationErrors = false;
    private List<string> validationErrors = new List<string>();
    private List<string> validationWarnings = new List<string>();

    private void OnEnable()
    {
        // Find all serialized properties
        armorSlotTypeProp = serializedObject.FindProperty("armorSlotType");
        defenseValueProp = serializedObject.FindProperty("defenseValue");
        magicDefenseValueProp = serializedObject.FindProperty("magicDefenseValue");
        durabilityModifierProp = serializedObject.FindProperty("durabilityModifier");
        inherentTraitsProp = serializedObject.FindProperty("inherentTraits");
        applyTraitsWhenEquippedProp = serializedObject.FindProperty("applyTraitsWhenEquipped");
        armorModelProp = serializedObject.FindProperty("armorModel");
        equipArmorSoundProp = serializedObject.FindProperty("equipArmorSound");
        unequipArmorSoundProp = serializedObject.FindProperty("unequipArmorSound");

        // Find inherited properties from EquippableSO
        effectsProp = serializedObject.FindProperty("effects");
        belongsToArmorSetProp = serializedObject.FindProperty("belongsToArmorSet");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ArmorSO armor = (ArmorSO)target;

        // Validate the armor
        ValidateArmor(armor);

        // Draw validation UI
        DrawValidationSection();

        EditorGUILayout.Space();

        // Main content with sections
        DrawBasicInfoSection(armor);
        EditorGUILayout.Space();

        DrawArmorStatsSection();
        EditorGUILayout.Space();

        DrawEffectsSection(armor);
        EditorGUILayout.Space();

        DrawArmorSetSection(armor);
        EditorGUILayout.Space();

        DrawOptionalTraitsSection();
        EditorGUILayout.Space();

        DrawVisualsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void ValidateArmor(ArmorSO armor)
    {
        validationErrors.Clear();
        validationWarnings.Clear();

        // CRITICAL: Check for at least one effect
        if (effectsProp.arraySize == 0)
        {
            validationErrors.Add("REQUIRED: Armor must have at least ONE status effect! Click '+' next to 'Status Effects' to add one.");
        }
        else
        {
            // Check for null or zero-amount effects
            int nullEffects = 0;
            int zeroAmountEffects = 0;

            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                var effect = effectsProp.GetArrayElementAtIndex(i);
                if (effect == null)
                {
                    nullEffects++;
                }
                else
                {
                    var amount = effect.FindPropertyRelative("amount");
                    if (amount != null && amount.floatValue == 0)
                    {
                        zeroAmountEffects++;
                    }
                }
            }

            if (nullEffects > 0)
            {
                validationErrors.Add($"Found {nullEffects} empty effect slot(s). Remove empty slots or configure them.");
            }

            if (zeroAmountEffects > 0)
            {
                validationWarnings.Add($"Found {zeroAmountEffects} effect(s) with 0 amount. Effects with 0 amount have no gameplay impact.");
            }
        }

        // Check name uniqueness
        string armorName = armor.name;
        var allArmors = AssetDatabase.FindAssets("t:ArmorSO")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ArmorSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(a => a != null && a != armor && a.name == armorName)
            .ToList();

        if (allArmors.Count > 0)
        {
            validationErrors.Add($"REQUIRED: Armor name '{armorName}' is not unique! Another armor already uses this name.");
        }

        // Check armor set relationship
        if (belongsToArmorSetProp.objectReferenceValue != null)
        {
            ArmorSet armorSet = belongsToArmorSetProp.objectReferenceValue as ArmorSet;
            if (armorSet != null && !armorSet.ContainsPiece(armor))
            {
                validationWarnings.Add($"This armor references set '{armorSet.SetName}' but the set doesn't include this armor in its pieces list.");
            }
        }

        // Optional checks
        if (defenseValueProp.floatValue == 0 && magicDefenseValueProp.floatValue == 0)
        {
            validationWarnings.Add("OPTIONAL: Both defense values are 0. Consider adding some defense unless this is intentional.");
        }

        if (string.IsNullOrEmpty(armor.Description))
        {
            validationWarnings.Add("OPTIONAL: No description provided. Consider adding one for better player experience.");
        }

        hasValidationErrors = validationErrors.Count > 0;
    }

    private void DrawValidationSection()
    {
        if (validationErrors.Count == 0 && validationWarnings.Count == 0)
        {
            EditorGUILayout.HelpBox("✓ All validation checks passed!", MessageType.Info);
            return;
        }

        // Draw errors
        if (validationErrors.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("❌ VALIDATION ERRORS (Must Fix)", EditorStyles.boldLabel);

            foreach (string error in validationErrors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // Draw warnings
        if (validationWarnings.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("⚠️ Warnings (Optional)", EditorStyles.boldLabel);

            foreach (string warning in validationWarnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawBasicInfoSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // Name (show current, can't edit directly)
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Armor Name", armor.name);
        EditorGUI.EndDisabledGroup();

        if (validationErrors.Any(e => e.Contains("name")))
        {
            EditorGUILayout.HelpBox("To rename: Right-click the asset in Project window → Rename", MessageType.Info);
        }

        // Armor slot type
        EditorGUILayout.PropertyField(armorSlotTypeProp, new GUIContent("Armor Slot Type"));

        // Description (from ItemSO)
        var descProp = serializedObject.FindProperty("description");
        if (descProp != null)
        {
            EditorGUILayout.PropertyField(descProp, new GUIContent("Description (Optional)"));
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawArmorStatsSection()
    {
        EditorGUILayout.LabelField("Armor Statistics", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(defenseValueProp, new GUIContent("Physical Defense"));
        EditorGUILayout.PropertyField(magicDefenseValueProp, new GUIContent("Magic Defense"));
        EditorGUILayout.PropertyField(durabilityModifierProp, new GUIContent("Durability Modifier"));

        if (defenseValueProp.floatValue == 0 && magicDefenseValueProp.floatValue == 0)
        {
            EditorGUILayout.HelpBox("Both defense values are 0. This armor provides no protection.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEffectsSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Status Effects (REQUIRED)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (hasValidationErrors && effectsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("⚠️ At least ONE effect is REQUIRED! Add effects like +Health, +Stamina, etc.", MessageType.Error);
        }

        // Add helpful button for common effects
        if (GUILayout.Button("Add Common Effect"))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("+50 Health"), false, () => AddEffect(EquippableEffectType.MaxHp, 50f, "+50 Maximum Health"));
            menu.AddItem(new GUIContent("+30 Stamina"), false, () => AddEffect(EquippableEffectType.MaxStamina, 30f, "+30 Maximum Stamina"));
            menu.AddItem(new GUIContent("+20 Mana"), false, () => AddEffect(EquippableEffectType.MaxMana, 20f, "+20 Maximum Mana"));
            menu.AddItem(new GUIContent("+10% Speed"), false, () => AddEffect(EquippableEffectType.Speed, 0.1f, "+10% Movement Speed"));
            menu.AddItem(new GUIContent("+5 Defense"), false, () => AddEffect(EquippableEffectType.Defense, 5f, "+5 Physical Defense"));
            menu.AddItem(new GUIContent("+15 Carry Weight"), false, () => AddEffect(EquippableEffectType.MaxWeight, 15f, "+15 Carry Weight"));
            menu.ShowAsContext();
        }

        EditorGUILayout.PropertyField(effectsProp, true);

        // Show summary of effects
        if (effectsProp.arraySize > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effect Summary:", EditorStyles.miniBoldLabel);

            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                var effect = effectsProp.GetArrayElementAtIndex(i);
                var typeProp = effect.FindPropertyRelative("effectType");
                var amountProp = effect.FindPropertyRelative("amount");
                var descProp = effect.FindPropertyRelative("effectDescription");

                if (typeProp != null && amountProp != null)
                {
                    string effectName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
                    float amount = amountProp.floatValue;
                    string desc = descProp?.stringValue ?? "";

                    if (amount == 0)
                    {
                        EditorGUILayout.LabelField($"  • {effectName}: {amount} ⚠️ (No effect)", EditorStyles.miniLabel);
                    }
                    else
                    {
                        string sign = amount > 0 ? "+" : "";
                        EditorGUILayout.LabelField($"  • {effectName}: {sign}{amount} {desc}", EditorStyles.miniLabel);
                    }
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawArmorSetSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Armor Set Configuration", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(belongsToArmorSetProp, new GUIContent("Belongs to Armor Set"));

        if (belongsToArmorSetProp.objectReferenceValue != null)
        {
            ArmorSet armorSet = belongsToArmorSetProp.objectReferenceValue as ArmorSet;
            if (armorSet != null)
            {
                if (!armorSet.ContainsPiece(armor))
                {
                    EditorGUILayout.HelpBox("⚠️ The armor set doesn't include this piece in its list! Add this armor to the set's pieces.", MessageType.Warning);

                    if (GUILayout.Button("Add to Set's Pieces"))
                    {
                        AddArmorToSet(armor, armorSet);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"✓ Part of '{armorSet.SetName}' set", MessageType.Info);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Not part of any armor set. This is okay - not all armor needs to be in a set.", MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawOptionalTraitsSection()
    {
        EditorGUILayout.LabelField("Inherent Traits (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.HelpBox("These are optional traits that apply when the armor is equipped, separate from armor set effects.", MessageType.Info);

        EditorGUILayout.PropertyField(applyTraitsWhenEquippedProp);
        EditorGUILayout.PropertyField(inherentTraitsProp, true);

        if (inherentTraitsProp.arraySize > 0)
        {
            int nullTraits = 0;
            for (int i = 0; i < inherentTraitsProp.arraySize; i++)
            {
                if (inherentTraitsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    nullTraits++;
            }

            if (nullTraits > 0)
            {
                EditorGUILayout.HelpBox($"Found {nullTraits} empty trait slot(s). Remove them or assign traits.", MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawVisualsSection()
    {
        EditorGUILayout.LabelField("Visual & Audio", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(armorModelProp);
        EditorGUILayout.PropertyField(equipArmorSoundProp);
        EditorGUILayout.PropertyField(unequipArmorSoundProp);

        EditorGUILayout.EndVertical();
    }

    private void AddEffect(EquippableEffectType effectType, float amount, string description)
    {
        int newIndex = effectsProp.arraySize;
        effectsProp.InsertArrayElementAtIndex(newIndex);

        var newEffect = effectsProp.GetArrayElementAtIndex(newIndex);
        newEffect.FindPropertyRelative("effectType").enumValueIndex = (int)effectType;
        newEffect.FindPropertyRelative("amount").floatValue = amount;
        newEffect.FindPropertyRelative("effectDescription").stringValue = description;

        serializedObject.ApplyModifiedProperties();
    }

    private void AddArmorToSet(ArmorSO armor, ArmorSet armorSet)
    {
        SerializedObject setObj = new SerializedObject(armorSet);
        var setPiecesProp = setObj.FindProperty("setPieces");

        // Check if already exists
        bool exists = false;
        for (int i = 0; i < setPiecesProp.arraySize; i++)
        {
            if (setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue == armor)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            int newIndex = setPiecesProp.arraySize;
            setPiecesProp.InsertArrayElementAtIndex(newIndex);
            setPiecesProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = armor;
            setObj.ApplyModifiedProperties();

            EditorUtility.SetDirty(armorSet);
            Debug.Log($"Added {armor.name} to {armorSet.SetName} set pieces");
        }
    }
}
#endif