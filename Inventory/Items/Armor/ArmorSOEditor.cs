#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Text;

[CustomEditor(typeof(ArmorSO))]
public class ArmorSOEditor : Editor
{
    // Cached serialized properties
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
    private SerializedProperty descProp;

    // Validation state
    private bool hasValidationErrors = false;
    private List<string> validationErrors = new List<string>(4);
    private List<string> validationWarnings = new List<string>(4);

    // Cache for expensive operations
    private static Dictionary<string, ArmorSO[]> armorCache = new Dictionary<string, ArmorSO[]>(10);
    private static float lastCacheTime = 0f;
    private const float CACHE_DURATION = 2f;

    // GUI content cache
    private static readonly GUIContent slotTypeContent = new GUIContent("Armor Slot Type");
    private static readonly GUIContent defenseContent = new GUIContent("Physical Defense");
    private static readonly GUIContent magicDefenseContent = new GUIContent("Magic Defense");
    private static readonly GUIContent durabilityContent = new GUIContent("Durability Modifier");
    private static readonly GUIContent descriptionContent = new GUIContent("Description (Optional)");

    // String builder for efficiency
    private static readonly StringBuilder stringBuilder = new StringBuilder(256);

    // Validation timing
    private float lastValidationTime = 0f;
    private const float VALIDATION_INTERVAL = 0.5f;

    private void OnEnable()
    {
        // Cache all properties once
        armorSlotTypeProp = serializedObject.FindProperty("armorSlotType");
        defenseValueProp = serializedObject.FindProperty("defenseValue");
        magicDefenseValueProp = serializedObject.FindProperty("magicDefenseValue");
        durabilityModifierProp = serializedObject.FindProperty("durabilityModifier");
        inherentTraitsProp = serializedObject.FindProperty("inherentTraits");
        applyTraitsWhenEquippedProp = serializedObject.FindProperty("applyTraitsWhenEquipped");
        armorModelProp = serializedObject.FindProperty("armorModel");
        equipArmorSoundProp = serializedObject.FindProperty("equipArmorSound");
        unequipArmorSoundProp = serializedObject.FindProperty("unequipArmorSound");
        effectsProp = serializedObject.FindProperty("effects");
        belongsToArmorSetProp = serializedObject.FindProperty("belongsToArmorSet");
        descProp = serializedObject.FindProperty("description");
    }

    private void OnDisable()
    {
        // Clear instance-specific data
        validationErrors.Clear();
        validationWarnings.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ArmorSO armor = (ArmorSO)target;

        // Throttled validation
        float currentTime = Time.realtimeSinceStartup;
        if (currentTime - lastValidationTime > VALIDATION_INTERVAL)
        {
            ValidateArmor(armor);
            lastValidationTime = currentTime;
        }

        // Draw UI sections
        DrawValidationSection();
        EditorGUILayout.Space();
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

        // Check for effects
        if (effectsProp.arraySize == 0)
        {
            validationErrors.Add("REQUIRED: Armor must have at least ONE status effect! Click '+' next to 'Status Effects' to add one.");
        }
        else
        {
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
                stringBuilder.Clear();
                stringBuilder.Append("Found ").Append(nullEffects).Append(" empty effect slot(s). Remove empty slots or configure them.");
                validationErrors.Add(stringBuilder.ToString());
            }

            if (zeroAmountEffects > 0)
            {
                stringBuilder.Clear();
                stringBuilder.Append("Found ").Append(zeroAmountEffects).Append(" effect(s) with 0 amount. Effects with 0 amount have no gameplay impact.");
                validationWarnings.Add(stringBuilder.ToString());
            }
        }

        // Check name uniqueness with caching
        ValidateNameUniqueness(armor);

        // Check armor set relationship
        if (belongsToArmorSetProp.objectReferenceValue != null)
        {
            ArmorSet armorSet = belongsToArmorSetProp.objectReferenceValue as ArmorSet;
            if (armorSet != null && !armorSet.ContainsPiece(armor))
            {
                stringBuilder.Clear();
                stringBuilder.Append("This armor references set '").Append(armorSet.SetName).Append("' but the set doesn't include this armor in its pieces list.");
                validationWarnings.Add(stringBuilder.ToString());
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

    private void ValidateNameUniqueness(ArmorSO armor)
    {
        string armorName = armor.name;

        // Check cache validity
        float currentTime = Time.realtimeSinceStartup;
        if (currentTime - lastCacheTime > CACHE_DURATION)
        {
            armorCache.Clear();
            lastCacheTime = currentTime;
        }

        // Get or create cached armor list
        if (!armorCache.TryGetValue("all", out ArmorSO[] allArmors))
        {
            var guids = AssetDatabase.FindAssets("t:ArmorSO");
            allArmors = new ArmorSO[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                allArmors[i] = AssetDatabase.LoadAssetAtPath<ArmorSO>(path);
            }

            armorCache["all"] = allArmors;
        }

        // Check for duplicates
        for (int i = 0; i < allArmors.Length; i++)
        {
            if (allArmors[i] != null && allArmors[i] != armor && allArmors[i].name == armorName)
            {
                stringBuilder.Clear();
                stringBuilder.Append("REQUIRED: Armor name '").Append(armorName).Append("' is not unique! Another armor already uses this name.");
                validationErrors.Add(stringBuilder.ToString());
                break;
            }
        }
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

            for (int i = 0; i < validationErrors.Count; i++)
            {
                EditorGUILayout.HelpBox(validationErrors[i], MessageType.Error);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // Draw warnings
        if (validationWarnings.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("⚠️ Warnings (Optional)", EditorStyles.boldLabel);

            for (int i = 0; i < validationWarnings.Count; i++)
            {
                EditorGUILayout.HelpBox(validationWarnings[i], MessageType.Warning);
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

        if (hasValidationErrors && validationErrors.Count > 0)
        {
            for (int i = 0; i < validationErrors.Count; i++)
            {
                if (validationErrors[i].Contains("name"))
                {
                    EditorGUILayout.HelpBox("To rename: Right-click the asset in Project window → Rename", MessageType.Info);
                    break;
                }
            }
        }

        // Armor slot type
        EditorGUILayout.PropertyField(armorSlotTypeProp, slotTypeContent);

        // Description
        if (descProp != null)
        {
            EditorGUILayout.PropertyField(descProp, descriptionContent);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawArmorStatsSection()
    {
        EditorGUILayout.LabelField("Armor Statistics", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(defenseValueProp, defenseContent);
        EditorGUILayout.PropertyField(magicDefenseValueProp, magicDefenseContent);
        EditorGUILayout.PropertyField(durabilityModifierProp, durabilityContent);

        if (defenseValueProp.floatValue == 0 && magicDefenseValueProp.floatValue == 0)
        {
            EditorGUILayout.HelpBox("Both defense values are 0. This armor provides no protection.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEffectsSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Status Effects (Required: At least 1)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (effectsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("⚠️ No effects! Add at least ONE status effect to this armor.", MessageType.Error);
        }



        EditorGUILayout.PropertyField(effectsProp, true);

        // Summary
        if (effectsProp.arraySize > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effect Summary:", EditorStyles.miniBoldLabel);

            stringBuilder.Clear();
            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                var effect = effectsProp.GetArrayElementAtIndex(i);
                var type = effect.FindPropertyRelative("effectType");
                var amount = effect.FindPropertyRelative("amount");

                if (type != null && amount != null)
                {
                    var effectType = (EquippableEffectType)type.enumValueIndex;
                    stringBuilder.Append("• ").Append(effectType).Append(": ");
                    if (amount.floatValue > 0) stringBuilder.Append("+");
                    stringBuilder.Append(amount.floatValue).AppendLine();
                }
            }
            EditorGUILayout.HelpBox(stringBuilder.ToString(), MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawArmorSetSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Armor Set (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(belongsToArmorSetProp);

        if (belongsToArmorSetProp.objectReferenceValue != null)
        {
            ArmorSet armorSet = belongsToArmorSetProp.objectReferenceValue as ArmorSet;
            if (armorSet != null)
            {
                EditorGUILayout.Space();

                stringBuilder.Clear();
                stringBuilder.Append("Set: ").AppendLine(armorSet.SetName);
                stringBuilder.Append("Pieces in set: ").AppendLine(armorSet.SetPieces.Count.ToString());
                stringBuilder.Append("This armor included: ").Append(armorSet.ContainsPiece(armor) ? "Yes" : "No ⚠️");

                EditorGUILayout.HelpBox(stringBuilder.ToString(), MessageType.Info);

                if (!armorSet.ContainsPiece(armor))
                {
                    if (GUILayout.Button("Add This Armor to Set"))
                    {
                        AddArmorToSet(armor, armorSet);
                    }
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawOptionalTraitsSection()
    {
        EditorGUILayout.LabelField("Inherent Traits (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(applyTraitsWhenEquippedProp);

        if (applyTraitsWhenEquippedProp.boolValue)
        {
            EditorGUILayout.PropertyField(inherentTraitsProp, true);

            if (inherentTraitsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No traits assigned. This is optional - traits can come from armor sets instead.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Trait application is disabled. Enable to apply traits when equipped.", MessageType.Info);

            if (inherentTraitsProp.arraySize > 0)
            {
                EditorGUILayout.HelpBox("⚠️ This armor has traits but won't apply them! Remove them or enable application.", MessageType.Warning);
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

            stringBuilder.Clear();
            stringBuilder.Append("Added ").Append(armor.name).Append(" to ").Append(armorSet.SetName).Append(" set pieces");
            Debug.Log(stringBuilder.ToString());
        }
    }
}
#endif