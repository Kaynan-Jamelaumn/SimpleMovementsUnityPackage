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

    // Validation state - reuse collections
    private bool hasValidationErrors = false;
    private readonly List<string> validationErrors = new List<string>(4);
    private readonly List<string> validationWarnings = new List<string>(4);

    // Cache with proper cleanup
    private static ArmorSO[] cachedArmors;
    private static double lastCacheTime;
    private const double CACHE_DURATION = 5.0; // Increased cache duration

    // Reusable GUI content
    private static readonly GUIContent slotTypeContent = new GUIContent("Armor Slot Type");
    private static readonly GUIContent defenseContent = new GUIContent("Physical Defense");
    private static readonly GUIContent magicDefenseContent = new GUIContent("Magic Defense");
    private static readonly GUIContent durabilityContent = new GUIContent("Durability Modifier");
    private static readonly GUIContent descriptionContent = new GUIContent("Description (Optional)");

    // Single shared StringBuilder instance
    private static readonly StringBuilder sharedStringBuilder = new StringBuilder(256);

    // Validation timing
    private double lastValidationTime;
    private const double VALIDATION_INTERVAL = 1.0; // Increased interval

    // Pre-allocated arrays for GUI
    private static readonly GUILayoutOption[] boxOptions = new GUILayoutOption[0];
    private static readonly GUILayoutOption[] buttonWidth50 = new GUILayoutOption[] { GUILayout.Width(50) };

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
        double currentTime = EditorApplication.timeSinceStartup;
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
        hasValidationErrors = false;

        // Check for effects
        if (effectsProp.arraySize == 0)
        {
            validationErrors.Add("REQUIRED: Armor must have at least ONE status effect! Click '+' next to 'Status Effects' to add one.");
            hasValidationErrors = true;
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
                    if (amount != null && Mathf.Approximately(amount.floatValue, 0f))
                    {
                        zeroAmountEffects++;
                    }
                }
            }

            if (nullEffects > 0)
            {
                sharedStringBuilder.Clear();
                sharedStringBuilder.Append("Found ").Append(nullEffects).Append(" empty effect slot(s). Remove empty slots or configure them.");
                validationErrors.Add(sharedStringBuilder.ToString());
                hasValidationErrors = true;
            }

            if (zeroAmountEffects > 0)
            {
                sharedStringBuilder.Clear();
                sharedStringBuilder.Append("Found ").Append(zeroAmountEffects).Append(" effect(s) with 0 amount. Effects with 0 amount have no gameplay impact.");
                validationWarnings.Add(sharedStringBuilder.ToString());
            }
        }

        // Check defense values
        if (Mathf.Approximately(defenseValueProp.floatValue, 0f) && Mathf.Approximately(magicDefenseValueProp.floatValue, 0f))
        {
            validationWarnings.Add("OPTIONAL: Both defense values are 0. Consider adding some defense unless this is intentional.");
        }

        // Check name uniqueness using cached armor list
        ValidateArmorNameUniqueness(armor);
    }

    private void ValidateArmorNameUniqueness(ArmorSO armor)
    {
        // Refresh cache if needed
        double currentTime = EditorApplication.timeSinceStartup;
        if (cachedArmors == null || currentTime - lastCacheTime > CACHE_DURATION)
        {
            RefreshArmorCache();
        }

        if (cachedArmors != null)
        {
            int duplicateCount = 0;
            string armorName = armor.name;

            for (int i = 0; i < cachedArmors.Length; i++)
            {
                if (cachedArmors[i] != null && cachedArmors[i] != armor && cachedArmors[i].name == armorName)
                {
                    duplicateCount++;
                }
            }

            if (duplicateCount > 0)
            {
                sharedStringBuilder.Clear();
                sharedStringBuilder.Append("REQUIRED: Armor name '").Append(armorName).Append("' is already used by ")
                    .Append(duplicateCount).Append(" other armor piece(s). Each armor must have a unique name!");
                validationErrors.Add(sharedStringBuilder.ToString());
                hasValidationErrors = true;
            }
        }
    }

    private static void RefreshArmorCache()
    {
        var guids = AssetDatabase.FindAssets("t:ArmorSO");
        cachedArmors = new ArmorSO[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            cachedArmors[i] = AssetDatabase.LoadAssetAtPath<ArmorSO>(path);
        }

        lastCacheTime = EditorApplication.timeSinceStartup;
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
        EditorGUILayout.BeginVertical(GUI.skin.box, boxOptions);

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
        EditorGUILayout.BeginVertical(GUI.skin.box, boxOptions);

        EditorGUILayout.PropertyField(defenseValueProp, defenseContent);
        EditorGUILayout.PropertyField(magicDefenseValueProp, magicDefenseContent);
        EditorGUILayout.PropertyField(durabilityModifierProp, durabilityContent);

        if (Mathf.Approximately(defenseValueProp.floatValue, 0f) && Mathf.Approximately(magicDefenseValueProp.floatValue, 0f))
        {
            EditorGUILayout.HelpBox("This armor provides no protection.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEffectsSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Status Effects (Required: At least 1)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box, boxOptions);

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

            sharedStringBuilder.Clear();
            for (int i = 0; i < effectsProp.arraySize; i++)
            {
                var effect = effectsProp.GetArrayElementAtIndex(i);
                var type = effect.FindPropertyRelative("effectType");
                var amount = effect.FindPropertyRelative("amount");

                if (type != null && amount != null)
                {
                    var effectType = (EquippableEffectType)type.enumValueIndex;
                    sharedStringBuilder.Append("• ").Append(effectType).Append(": ");
                    if (amount.floatValue > 0) sharedStringBuilder.Append("+");
                    sharedStringBuilder.Append(amount.floatValue).AppendLine();
                }
            }
            EditorGUILayout.HelpBox(sharedStringBuilder.ToString(), MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawArmorSetSection(ArmorSO armor)
    {
        EditorGUILayout.LabelField("Armor Set (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box, boxOptions);

        EditorGUILayout.PropertyField(belongsToArmorSetProp);

        if (belongsToArmorSetProp.objectReferenceValue != null)
        {
            ArmorSet armorSet = belongsToArmorSetProp.objectReferenceValue as ArmorSet;
            if (armorSet != null)
            {
                EditorGUILayout.Space();

                sharedStringBuilder.Clear();
                sharedStringBuilder.Append("Set: ").AppendLine(armorSet.SetName);
                sharedStringBuilder.Append("Pieces in set: ").AppendLine(armorSet.SetPieces.Count.ToString());
                sharedStringBuilder.Append("This armor included: ").Append(armorSet.ContainsPiece(armor) ? "✓ Yes" : "✗ No");
                EditorGUILayout.HelpBox(sharedStringBuilder.ToString(), MessageType.None);

                if (!armorSet.ContainsPiece(armor))
                {
                    EditorGUILayout.HelpBox("⚠️ This armor references the set but isn't in its piece list!", MessageType.Warning);

                    if (GUILayout.Button("Add to Set", buttonWidth50))
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
        EditorGUILayout.LabelField("Optional Armor Traits", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box, boxOptions);

        EditorGUILayout.PropertyField(applyTraitsWhenEquippedProp);

        if (applyTraitsWhenEquippedProp.boolValue)
        {
            EditorGUILayout.PropertyField(inherentTraitsProp, true);

            if (inherentTraitsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No traits. This is optional - traits can come from armor sets instead.", MessageType.Info);
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
        EditorGUILayout.BeginVertical(GUI.skin.box, boxOptions);

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

            sharedStringBuilder.Clear();
            sharedStringBuilder.Append("Added ").Append(armor.name).Append(" to ").Append(armorSet.SetName).Append(" set pieces");
            Debug.Log(sharedStringBuilder.ToString());
        }
    }

    // Clean up static cache when domain reloads
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        cachedArmors = null;
        lastCacheTime = 0;
    }
}
#endif