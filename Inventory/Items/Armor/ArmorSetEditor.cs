#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Text;

[CustomEditor(typeof(ArmorSet))]
public class ArmorSetEditor : Editor
{
    // Cached serialized properties
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

    // Validation state - using pre-allocated collections
    private Dictionary<int, List<string>> effectValidationErrors = new Dictionary<int, List<string>>(8);
    private List<string> setValidationErrors = new List<string>(8);
    private List<string> setValidationWarnings = new List<string>(8);
    private bool hasValidationErrors = false;

    // UI state
    private bool[] effectFoldouts;
    private bool showDetailedEffectValidation = true;

    // Cache for expensive operations
    private static Dictionary<string, ArmorSet[]> setCache = new Dictionary<string, ArmorSet[]>(10);
    private static Dictionary<string, ArmorSO[]> armorCache = new Dictionary<string, ArmorSO[]>(10);
    private static float lastCacheTime = 0f;
    private const float CACHE_DURATION = 2f;

    // String builder for performance
    private static readonly StringBuilder stringBuilder = new StringBuilder(512);

    // GUI content cache
    private static readonly GUIContent setNameContent = new GUIContent("Set Name (Required)");
    private static readonly GUIContent descriptionContent = new GUIContent("Description (Optional)");
    private static readonly GUIContent effectNameContent = new GUIContent("Effect Name");
    private static readonly GUIContent piecesReqContent = new GUIContent("Pieces Required");

    // Validation timing
    private float lastValidationTime = 0f;
    private const float VALIDATION_INTERVAL = 0.5f;

    private void OnEnable()
    {
        // Cache all properties once
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

        // Initialize UI state efficiently
        if (setEffectsProp != null)
        {
            effectFoldouts = new bool[setEffectsProp.arraySize];
        }
    }

    private void OnDisable()
    {
        // Clear instance-specific data
        effectValidationErrors.Clear();
        setValidationErrors.Clear();
        setValidationWarnings.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ArmorSet armorSet = (ArmorSet)target;

        // Throttled validation
        float currentTime = Time.realtimeSinceStartup;
        if (currentTime - lastValidationTime > VALIDATION_INTERVAL)
        {
            ValidateArmorSet(armorSet);
            lastValidationTime = currentTime;
        }

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

        // Clear existing effect errors
        foreach (var list in effectValidationErrors.Values)
        {
            list.Clear();
        }

        // Validate set name
        if (string.IsNullOrEmpty(setNameProp.stringValue))
        {
            setValidationErrors.Add("REQUIRED: Set must have a name!");
        }
        else
        {
            ValidateSetNameUniqueness(armorSet);
        }

        // Validate armor pieces
        if (setPiecesProp.arraySize == 0)
        {
            setValidationErrors.Add("REQUIRED: Set must contain at least ONE armor piece!");
        }
        else
        {
            ValidateArmorPieces(armorSet);
        }

        // Validate set effects
        if (setEffectsProp.arraySize == 0)
        {
            setValidationErrors.Add("REQUIRED: Set must have at least ONE set effect!");
        }
        else
        {
            ValidateSetEffects(armorSet);
        }

        // Optional validation
        if (string.IsNullOrEmpty(setDescriptionProp.stringValue))
        {
            setValidationWarnings.Add("OPTIONAL: Consider adding a description for better player experience.");
        }

        hasValidationErrors = setValidationErrors.Count > 0 || effectValidationErrors.Count > 0;
    }

    private void ValidateSetNameUniqueness(ArmorSet armorSet)
    {
        // Check cache validity
        float currentTime = Time.realtimeSinceStartup;
        if (currentTime - lastCacheTime > CACHE_DURATION)
        {
            setCache.Clear();
            armorCache.Clear();
            lastCacheTime = currentTime;
        }

        // Get or create cached set list
        if (!setCache.TryGetValue("all", out ArmorSet[] allSets))
        {
            var guids = AssetDatabase.FindAssets("t:ArmorSet");
            allSets = new ArmorSet[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                allSets[i] = AssetDatabase.LoadAssetAtPath<ArmorSet>(path);
            }

            setCache["all"] = allSets;
        }

        // Check for duplicates efficiently
        string setName = armorSet.SetName;
        for (int i = 0; i < allSets.Length; i++)
        {
            if (allSets[i] != null && allSets[i] != armorSet && allSets[i].SetName == setName)
            {
                stringBuilder.Clear();
                stringBuilder.Append("REQUIRED: Set name '").Append(setName).Append("' is not unique! Another set already uses this name.");
                setValidationErrors.Add(stringBuilder.ToString());
                break;
            }
        }
    }

    private void ValidateArmorPieces(ArmorSet armorSet)
    {
        int nullPieces = 0;
        for (int i = 0; i < setPiecesProp.arraySize; i++)
        {
            var piece = setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue as ArmorSO;
            if (piece == null)
            {
                nullPieces++;
            }
            else if (piece.BelongsToSet != armorSet)
            {
                stringBuilder.Clear();
                stringBuilder.Append("Armor piece '").Append(piece.name).Append("' doesn't reference this set. Update its 'Belongs to Armor Set' field.");
                setValidationWarnings.Add(stringBuilder.ToString());
            }
        }

        if (nullPieces > 0)
        {
            stringBuilder.Clear();
            stringBuilder.Append("Found ").Append(nullPieces).Append(" empty armor piece slot(s). Remove empty slots or assign armor pieces.");
            setValidationErrors.Add(stringBuilder.ToString());
        }
    }

    private void ValidateSetEffects(ArmorSet armorSet)
    {
        for (int i = 0; i < setEffectsProp.arraySize; i++)
        {
            var effectProp = setEffectsProp.GetArrayElementAtIndex(i);
            var errors = ValidateEffect(effectProp, i, armorSet);

            if (errors.Count > 0)
            {
                if (!effectValidationErrors.ContainsKey(i))
                {
                    effectValidationErrors[i] = new List<string>(4);
                }
                effectValidationErrors[i].Clear();
                effectValidationErrors[i].AddRange(errors);
            }
        }
    }

    private List<string> ValidateEffect(SerializedProperty effectProp, int index, ArmorSet armorSet)
    {
        var errors = new List<string>(4);

        var nameProp = effectProp.FindPropertyRelative("effectName");
        var piecesProp = effectProp.FindPropertyRelative("piecesRequired");
        var statusProp = effectProp.FindPropertyRelative("statusModifiers");
        var traitsProp = effectProp.FindPropertyRelative("grantedTraits");
        var mechanicsProp = effectProp.FindPropertyRelative("specialMechanics");

        // Name validation
        if (string.IsNullOrEmpty(nameProp.stringValue))
        {
            stringBuilder.Clear();
            stringBuilder.Append("Effect #").Append(index + 1).Append(" needs a name");
            errors.Add(stringBuilder.ToString());
        }

        // Pieces required validation
        int piecesReq = piecesProp.intValue;
        if (piecesReq < 1)
        {
            errors.Add("Pieces required must be at least 1");
        }
        else if (piecesReq > armorSet.SetPieces.Count)
        {
            stringBuilder.Clear();
            stringBuilder.Append("Requires ").Append(piecesReq).Append(" pieces but set only has ").Append(armorSet.SetPieces.Count);
            errors.Add(stringBuilder.ToString());
        }

        // Effect content validation
        bool hasContent = (statusProp?.arraySize > 0) ||
                         (traitsProp?.arraySize > 0) ||
                         (mechanicsProp?.arraySize > 0);

        if (!hasContent)
        {
            errors.Add("Effect has no modifiers, traits, or mechanics");
        }

        return errors;
    }

    private void DrawHeader(ArmorSet armorSet)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Armor Set: {armorSet.SetName}", EditorStyles.boldLabel);
        showDetailedEffectValidation = EditorGUILayout.Toggle("Detailed Validation", showDetailedEffectValidation);
        EditorGUILayout.EndHorizontal();

        if (hasValidationErrors)
        {
            EditorGUILayout.HelpBox("⚠️ This armor set has validation errors that need to be fixed!", MessageType.Error);
        }
    }

    private void DrawValidationSection()
    {
        if (setValidationErrors.Count == 0 && setValidationWarnings.Count == 0 && effectValidationErrors.Count == 0)
        {
            EditorGUILayout.HelpBox("✓ All validation checks passed!", MessageType.Info);
            return;
        }

        // Set-level errors
        if (setValidationErrors.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("❌ SET ERRORS (Must Fix)", EditorStyles.boldLabel);

            for (int i = 0; i < setValidationErrors.Count; i++)
            {
                EditorGUILayout.HelpBox(setValidationErrors[i], MessageType.Error);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // Effect errors
        if (showDetailedEffectValidation && effectValidationErrors.Count > 0)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("❌ EFFECT ERRORS", EditorStyles.boldLabel);

            foreach (var kvp in effectValidationErrors)
            {
                int effectIndex = kvp.Key;
                var errors = kvp.Value;

                if (errors.Count == 0) continue;

                var effectProp = setEffectsProp.GetArrayElementAtIndex(effectIndex);
                var nameProp = effectProp.FindPropertyRelative("effectName");
                var piecesReqProp = effectProp.FindPropertyRelative("piecesRequired");

                stringBuilder.Clear();
                stringBuilder.Append("Effect: ");
                stringBuilder.Append(string.IsNullOrEmpty(nameProp.stringValue) ? $"#{effectIndex + 1}" : nameProp.stringValue);
                stringBuilder.Append(" (").Append(piecesReqProp.intValue).Append(" pieces)");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(stringBuilder.ToString(), EditorStyles.miniBoldLabel);

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

            for (int i = 0; i < setValidationWarnings.Count; i++)
            {
                EditorGUILayout.HelpBox(setValidationWarnings[i], MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawBasicInfoSection()
    {
        EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(setNameProp, setNameContent);
        EditorGUILayout.PropertyField(setDescriptionProp, descriptionContent);
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

            stringBuilder.Clear();
            for (int i = 0; i < setPiecesProp.arraySize; i++)
            {
                var piece = setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue as ArmorSO;
                if (piece != null)
                {
                    string status = piece.BelongsToSet == armorSet ? "✓" : "⚠️ Not linked";
                    stringBuilder.Append("• ").Append(piece.name);
                    stringBuilder.Append(" (").Append(piece.ArmorSlotType).Append(") ");
                    stringBuilder.AppendLine(status);
                }
            }

            EditorGUILayout.HelpBox(stringBuilder.ToString(), MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSetEffectsSection(ArmorSet armorSet)
    {
        EditorGUILayout.LabelField("Set Effects (Required: At least 1)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (setEffectsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("⚠️ No set effects! Add at least ONE effect.", MessageType.Error);
        }



        // Ensure foldout array size matches
        if (effectFoldouts == null || effectFoldouts.Length != setEffectsProp.arraySize)
        {
            effectFoldouts = new bool[setEffectsProp.arraySize];
        }

        // Draw each effect
        for (int i = 0; i < setEffectsProp.arraySize; i++)
        {
            DrawSetEffect(i, armorSet);
        }

        // Summary
        if (setEffectsProp.arraySize > 0)
        {
            EditorGUILayout.Space();
            DrawEffectsSummary(armorSet);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSetEffect(int index, ArmorSet armorSet)
    {
        var effectProp = setEffectsProp.GetArrayElementAtIndex(index);
        var nameProp = effectProp.FindPropertyRelative("effectName");
        var piecesReqProp = effectProp.FindPropertyRelative("piecesRequired");

        // Header with validation indicator
        bool hasErrors = effectValidationErrors.ContainsKey(index) && effectValidationErrors[index].Count > 0;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        stringBuilder.Clear();
        stringBuilder.Append(hasErrors ? "❌ " : "✓ ");
        stringBuilder.Append(string.IsNullOrEmpty(nameProp.stringValue) ? $"Effect #{index + 1}" : nameProp.stringValue);
        stringBuilder.Append(" (").Append(piecesReqProp.intValue).Append(" pieces)");

        effectFoldouts[index] = EditorGUILayout.Foldout(effectFoldouts[index], stringBuilder.ToString(), true);

        // Remove button
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            setEffectsProp.DeleteArrayElementAtIndex(index);
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (effectFoldouts[index])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(nameProp, effectNameContent);
            EditorGUILayout.PropertyField(piecesReqProp, piecesReqContent);

            // Draw sub-properties
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("effectDescription"));
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("statusModifiers"), true);
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("grantedTraits"), true);
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("specialMechanics"), true);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEffectsSummary(ArmorSet armorSet)
    {
        EditorGUILayout.LabelField("Effects Summary:", EditorStyles.miniBoldLabel);

        stringBuilder.Clear();

        // Sort effects by pieces required
        var sortedEffects = new List<(int pieces, string name)>();
        for (int i = 0; i < setEffectsProp.arraySize; i++)
        {
            var effectProp = setEffectsProp.GetArrayElementAtIndex(i);
            var nameProp = effectProp.FindPropertyRelative("effectName");
            var piecesReqProp = effectProp.FindPropertyRelative("piecesRequired");

            sortedEffects.Add((piecesReqProp.intValue, nameProp.stringValue));
        }

        sortedEffects.Sort((a, b) => a.pieces.CompareTo(b.pieces));

        foreach (var effect in sortedEffects)
        {
            stringBuilder.Append("• ").Append(effect.pieces).Append(" pieces: ").AppendLine(effect.name);
        }

        EditorGUILayout.HelpBox(stringBuilder.ToString(), MessageType.None);
    }

    private void DrawVisualsSection()
    {
        EditorGUILayout.LabelField("Audio & Visual", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(setCompleteSoundProp);
        EditorGUILayout.PropertyField(setCompleteEffectProp);

        EditorGUILayout.EndVertical();
    }

    private void FindAndAssignArmorPieces(ArmorSet armorSet)
    {
        // Get cached armor list
        if (!armorCache.TryGetValue("all", out ArmorSO[] allArmor))
        {
            var guids = AssetDatabase.FindAssets("t:ArmorSO");
            allArmor = new ArmorSO[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                allArmor[i] = AssetDatabase.LoadAssetAtPath<ArmorSO>(path);
            }

            armorCache["all"] = allArmor;
        }

        int added = 0;

        // Find armor that references this set
        for (int i = 0; i < allArmor.Length; i++)
        {
            if (allArmor[i] != null && allArmor[i].BelongsToSet == armorSet)
            {
                bool alreadyInSet = false;

                // Check if already in set
                for (int j = 0; j < setPiecesProp.arraySize; j++)
                {
                    if (setPiecesProp.GetArrayElementAtIndex(j).objectReferenceValue == allArmor[i])
                    {
                        alreadyInSet = true;
                        break;
                    }
                }

                if (!alreadyInSet)
                {
                    int newIndex = setPiecesProp.arraySize;
                    setPiecesProp.InsertArrayElementAtIndex(newIndex);
                    setPiecesProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = allArmor[i];
                    added++;
                }
            }
        }

        if (added > 0)
        {
            serializedObject.ApplyModifiedProperties();

            stringBuilder.Clear();
            stringBuilder.Append("Added ").Append(added).Append(" armor piece(s) to the set");
            Debug.Log(stringBuilder.ToString());
        }
        else
        {
            Debug.Log("No new armor pieces found that reference this set");
        }
    }

    private void CleanNullReferences()
    {
        int removed = 0;

        for (int i = setPiecesProp.arraySize - 1; i >= 0; i--)
        {
            if (setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                setPiecesProp.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }

        if (removed > 0)
        {
            serializedObject.ApplyModifiedProperties();

            stringBuilder.Clear();
            stringBuilder.Append("Removed ").Append(removed).Append(" null reference(s)");
            Debug.Log(stringBuilder.ToString());
        }
    }


}
#endif