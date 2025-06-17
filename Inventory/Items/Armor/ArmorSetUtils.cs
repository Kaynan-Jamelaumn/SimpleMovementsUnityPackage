#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Text;

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

    private Dictionary<int, List<string>> effectValidationErrors = new Dictionary<int, List<string>>(8);
    private List<string> setValidationErrors = new List<string>(8);
    private List<string> setValidationWarnings = new List<string>(8);
    private bool hasValidationErrors = false;

    private bool[] effectFoldouts;
    private bool showDetailedEffectValidation = true;

    private static Dictionary<string, ArmorSet[]> setCache;
    private static Dictionary<string, ArmorSO[]> armorCache;
    private static double lastCacheTime = 0;
    private const double CACHE_DURATION = 5.0;

    private static readonly StringBuilder stringBuilder = new StringBuilder(512);

    private static readonly GUIContent setNameContent = new GUIContent("Set Name (Required)");
    private static readonly GUIContent descriptionContent = new GUIContent("Description (Optional)");
    private static readonly GUIContent effectNameContent = new GUIContent("Effect Name");
    private static readonly GUIContent piecesReqContent = new GUIContent("Pieces Required");

    private double lastValidationTime = 0;
    private const double VALIDATION_INTERVAL = 1.0;

    static ArmorSetEditor()
    {
        setCache = new Dictionary<string, ArmorSet[]>(16);
        armorCache = new Dictionary<string, ArmorSO[]>(16);
    }

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

        if (setEffectsProp != null)
        {
            int effectCount = setEffectsProp.arraySize;
            if (effectFoldouts == null || effectFoldouts.Length != effectCount)
            {
                effectFoldouts = new bool[effectCount];
            }
        }
    }

    private void OnDisable()
    {
        effectValidationErrors.Clear();
        setValidationErrors.Clear();
        setValidationWarnings.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ArmorSet armorSet = (ArmorSet)target;

        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - lastValidationTime > VALIDATION_INTERVAL)
        {
            PerformValidation(armorSet);
            lastValidationTime = currentTime;
        }

        DrawHeader(armorSet);
        DrawValidationSection();

        EditorGUILayout.Space();

        DrawBasicInfoSection();
        EditorGUILayout.Space();
        DrawSetPiecesSection(armorSet);
        EditorGUILayout.Space();
        DrawSetEffectsSection(armorSet);
        EditorGUILayout.Space();
        DrawAdvancedProperties();

        if (serializedObject.ApplyModifiedProperties())
        {
            InvalidateCache();
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
        EditorGUILayout.PropertyField(requiredSlotTypesProp, true);

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

        if (effectFoldouts == null || effectFoldouts.Length != setEffectsProp.arraySize)
        {
            effectFoldouts = new bool[setEffectsProp.arraySize];
        }

        for (int i = 0; i < setEffectsProp.arraySize; i++)
        {
            DrawSetEffect(i, armorSet);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Effect"))
        {
            setEffectsProp.arraySize++;
            System.Array.Resize(ref effectFoldouts, setEffectsProp.arraySize);
            effectFoldouts[setEffectsProp.arraySize - 1] = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawSetEffect(int index, ArmorSet armorSet)
    {
        SerializedProperty effectProp = setEffectsProp.GetArrayElementAtIndex(index);
        SerializedProperty nameProp = effectProp.FindPropertyRelative("effectName");
        SerializedProperty piecesReqProp = effectProp.FindPropertyRelative("piecesRequired");

        bool hasErrors = effectValidationErrors.ContainsKey(index) && effectValidationErrors[index].Count > 0;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        stringBuilder.Clear();
        stringBuilder.Append(hasErrors ? "❌ " : "✓ ");
        stringBuilder.Append(string.IsNullOrEmpty(nameProp.stringValue) ? $"Effect #{index + 1}" : nameProp.stringValue);
        stringBuilder.Append(" (").Append(piecesReqProp.intValue).Append(" pieces)");

        effectFoldouts[index] = EditorGUILayout.Foldout(effectFoldouts[index], stringBuilder.ToString(), true);

        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            setEffectsProp.DeleteArrayElementAtIndex(index);
            if (effectFoldouts.Length > setEffectsProp.arraySize)
            {
                System.Array.Resize(ref effectFoldouts, setEffectsProp.arraySize);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (effectFoldouts[index])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(nameProp, effectNameContent);
            EditorGUILayout.PropertyField(piecesReqProp, piecesReqContent);
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("effectDescription"));
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("statusModifiers"), true);
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("grantedTraits"), true);
            EditorGUILayout.PropertyField(effectProp.FindPropertyRelative("specialMechanics"), true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
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

    private void DrawAdvancedProperties()
    {
        EditorGUILayout.LabelField("Advanced Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(minimumPiecesProp);
        EditorGUILayout.PropertyField(maximumPiecesProp);
        EditorGUILayout.PropertyField(setCompleteSoundProp);
        EditorGUILayout.PropertyField(setCompleteEffectProp);
        EditorGUILayout.Space();
    }

    private void DrawValidationSection()
    {
        if (hasValidationErrors || setValidationWarnings.Count > 0)
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (hasValidationErrors)
            {
                EditorGUILayout.HelpBox($"Found {setValidationErrors.Count} validation errors", MessageType.Error);
                foreach (string error in setValidationErrors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (setValidationWarnings.Count > 0)
            {
                foreach (string warning in setValidationWarnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }

            showDetailedEffectValidation = EditorGUILayout.Toggle("Show Detailed Effect Validation", showDetailedEffectValidation);

            if (showDetailedEffectValidation && effectValidationErrors.Count > 0)
            {
                foreach (var kvp in effectValidationErrors)
                {
                    foreach (string error in kvp.Value)
                    {
                        EditorGUILayout.HelpBox($"Effect {kvp.Key + 1}: {error}", MessageType.Warning);
                    }
                }
            }
        }
    }

    private void PerformValidation(ArmorSet armorSet)
    {
        setValidationErrors.Clear();
        setValidationWarnings.Clear();
        effectValidationErrors.Clear();
        hasValidationErrors = false;

        if (string.IsNullOrEmpty(setNameProp.stringValue))
        {
            setValidationErrors.Add("REQUIRED: Set must have a name!");
            hasValidationErrors = true;
        }

        if (setPiecesProp.arraySize == 0)
        {
            setValidationErrors.Add("REQUIRED: Set must contain at least ONE armor piece!");
            hasValidationErrors = true;
        }

        if (setEffectsProp.arraySize == 0)
        {
            setValidationErrors.Add("REQUIRED: Set must have at least ONE set effect!");
            hasValidationErrors = true;
        }
        else
        {
            ValidateSetEffects(armorSet);
        }

        if (string.IsNullOrEmpty(setDescriptionProp.stringValue))
        {
            setValidationWarnings.Add("OPTIONAL: Consider adding a description for better player experience.");
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

        if (string.IsNullOrEmpty(nameProp.stringValue))
        {
            errors.Add($"Effect #{index + 1} needs a name");
        }

        int piecesReq = piecesProp.intValue;
        if (piecesReq < 1)
        {
            errors.Add("Pieces required must be at least 1");
        }
        else if (piecesReq > setPiecesProp.arraySize)
        {
            errors.Add($"Requires {piecesReq} pieces but set only has {setPiecesProp.arraySize}");
        }

        return errors;
    }

    private void FindAndAssignArmorPieces(ArmorSet armorSet)
    {
        ArmorSO[] allArmors = GetAllArmors();
        int foundCount = 0;

        foreach (ArmorSO armor in allArmors)
        {
            if (armor.BelongsToArmorSet == armorSet && !armorSet.SetPieces.Contains(armor))
            {
                int newIndex = setPiecesProp.arraySize;
                setPiecesProp.InsertArrayElementAtIndex(newIndex);
                setPiecesProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = armor;
                foundCount++;
            }
        }

        if (foundCount > 0)
        {
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Found and assigned {foundCount} armor pieces to {armorSet.SetName}");
        }
        else
        {
            Debug.Log($"No unassigned armor pieces found referencing {armorSet.SetName}");
        }
    }

    private void CleanNullReferences()
    {
        for (int i = setPiecesProp.arraySize - 1; i >= 0; i--)
        {
            if (setPiecesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                setPiecesProp.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private static void InvalidateCache()
    {
        setCache.Clear();
        armorCache.Clear();
        lastCacheTime = 0;
    }

    private static ArmorSet[] GetAllArmorSets()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        string cacheKey = "all_sets";

        if (currentTime - lastCacheTime > CACHE_DURATION || !setCache.ContainsKey(cacheKey))
        {
            setCache[cacheKey] = Resources.FindObjectsOfTypeAll<ArmorSet>();
            lastCacheTime = currentTime;
        }

        return setCache[cacheKey];
    }

    private static ArmorSO[] GetAllArmors()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        string cacheKey = "all_armors";

        if (currentTime - lastCacheTime > CACHE_DURATION || !armorCache.ContainsKey(cacheKey))
        {
            armorCache[cacheKey] = Resources.FindObjectsOfTypeAll<ArmorSO>();
            lastCacheTime = currentTime;
        }

        return armorCache[cacheKey];
    }
}
#endif