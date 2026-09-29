#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for Ability Definitions: a readable summary, live validation (errors and warnings), buttons to add
/// actions, apply a preset and copy the id, then the fields.
/// </summary>
[CustomEditor(typeof(AbilityDefinition))]
[CanEditMultipleObjects]
public class AbilityDefinitionEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private double nextValidate;
    private static bool showSummary = true;
    private static bool showIssues = true;

    private void OnEnable() => Validate();

    private void Validate()
    {
        errors.Clear();
        warnings.Clear();
        if (target is AbilityDefinition def)
        {
            try { def.Validate(errors, warnings); }
            catch (Exception e) { errors.Add("Validation failed: " + e.Message); }
        }
        nextValidate = EditorApplication.timeSinceStartup + 0.5;
    }

    public override void OnInspectorGUI()
    {
        var def = (AbilityDefinition)target;
        serializedObject.Update();

        // Only on Layout events, so the number of messages never changes between Layout and Repaint.
        if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup > nextValidate)
            Validate();

        // Summary.
        showSummary = EditorGUILayout.BeginFoldoutHeaderGroup(showSummary, "Summary");
        if (showSummary)
        {
            string text;
            try { text = def.Describe(); }
            catch (Exception e) { text = "(cannot describe: " + e.Message + ")"; }
            EditorGUILayout.HelpBox(text, MessageType.None);
            AbilityStats s = AbilityStats.Identity;
            EditorGUILayout.LabelField("Reach / damage / control", $"{def.MaxReach(s):0.#} m   ·   ~{def.EstimateDamage(s):0.#} dmg   ·   {def.EstimateControl(s):0.#} s control", EditorStyles.miniLabel);
            if (def.LegacySource != null)
                EditorGUILayout.ObjectField("Converted From", def.LegacySource, typeof(AbilityEffectSO), false);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Validation.
        if (errors.Count + warnings.Count > 0)
        {
            showIssues = EditorGUILayout.BeginFoldoutHeaderGroup(showIssues, $"Setup issues ({errors.Count} errors, {warnings.Count} warnings)");
            if (showIssues)
            {
                foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
                foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        else
        {
            EditorGUILayout.HelpBox("No setup issues found.", MessageType.Info);
        }

        // Tools.
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Add Action ▾", "Add an area hit, projectile, ground surge, zone, beam, wall/cage, summon or movement.")))
            ShowAddActionMenu();
        if (GUILayout.Button(new GUIContent("Apply Preset ▾", "Replace this ability's settings with a ready-made setup (keeps its id and name).")))
            ShowPresetMenu(def);
        if (GUILayout.Button(new GUIContent("Copy Id", def.Id)))
            EditorGUIUtility.systemCopyBuffer = def.Id;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();
        if (changed)
            nextValidate = 0;
    }

    private void ShowAddActionMenu()
    {
        var menu = new GenericMenu();
        foreach (Type t in SubclassSelectorDrawer.GetTypes(typeof(CastAction)))
        {
            var attr = (AbilityMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AbilityMenuAttribute), false);
            Type captured = t;
            menu.AddItem(new GUIContent(attr != null ? attr.path : AbilityTypeNames.Nice(t)), false, () => AddAction(captured));
        }
        menu.ShowAsContext();
    }

    private void AddAction(Type t)
    {
        serializedObject.Update();
        SerializedProperty list = serializedObject.FindProperty("actions");
        Undo.RecordObjects(targets, "Add Action");
        int i = list.arraySize;
        list.arraySize++;
        SerializedProperty element = list.GetArrayElementAtIndex(i);
        element.managedReferenceValue = Activator.CreateInstance(t);
        element.isExpanded = true;
        serializedObject.ApplyModifiedProperties();
        nextValidate = 0;
    }

    private static void ShowPresetMenu(AbilityDefinition def)
    {
        var menu = new GenericMenu();
        foreach (AbilityPresets.Preset p in AbilityPresets.All)
        {
            AbilityPresets.Preset captured = p;
            menu.AddItem(new GUIContent(p.name), false, () => ApplyPreset(def, captured));
        }
        menu.ShowAsContext();
    }

    private static void ApplyPreset(AbilityDefinition def, AbilityPresets.Preset preset)
    {
        if (!EditorUtility.DisplayDialog("Apply preset", $"Replace every setting of '{def.DisplayName}' with the '{preset.name}' preset?\n\n{preset.description}", "Replace", "Cancel"))
            return;
        string id = def.Id;
        string assetName = def.name;
        string display = def.DisplayName;
        AbilityDefinition source = preset.create();
        Undo.RecordObject(def, "Apply Ability Preset");
        EditorUtility.CopySerialized(source, def);
        def.name = assetName;
        def.SetId(id);
        def.SetDisplayName(display);
        UnityEngine.Object.DestroyImmediate(source);
        EditorUtility.SetDirty(def);
    }
}

/// <summary>Inspector for the Ability Database: collect every ability in the project and validate them.</summary>
[CustomEditor(typeof(AbilityDatabase))]
public class AbilityDatabaseEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private bool validated;

    public override void OnInspectorGUI()
    {
        var db = (AbilityDatabase)target;
        EditorGUILayout.HelpBox("Saves store ability ids; the database turns them back into abilities (absorbed abilities after loading). Put it in a Resources folder named 'AbilityDatabase'.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Collect All Abilities In Project"))
        {
            Undo.RecordObject(db, "Collect Abilities");
            db.abilities.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
            {
                var a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && !db.abilities.Contains(a))
                    db.abilities.Add(a);
            }
            EditorUtility.SetDirty(db);
            validated = false;
        }
        if (GUILayout.Button("Validate"))
        {
            errors.Clear();
            warnings.Clear();
            db.Validate(errors, warnings);
            validated = true;
        }
        EditorGUILayout.EndHorizontal();
        if (validated)
        {
            if (errors.Count + warnings.Count == 0)
                EditorGUILayout.HelpBox($"{db.abilities.Count} abilities, no issues.", MessageType.Info);
            foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
            foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
#endif
