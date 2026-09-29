#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Trait Database inspector: counts by kind and category, a searchable/filterable list with problems flagged,
/// collect-all / remove-empty / validate buttons, and creation of new traits (empty or from a preset).
/// </summary>
[CustomEditor(typeof(TraitDatabase))]
public class TraitDatabaseEditor : Editor
{
    private string search = "";
    private int typeFilter = -1;
    private int kindFilter = -1;
    private static bool showRaw;
    // Per-trait problem counts, refreshed twice per second (validating every trait on every redraw was slow).
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private readonly Dictionary<Trait, string> problems = new Dictionary<Trait, string>();
    private readonly Dictionary<Trait, string> problemDetails = new Dictionary<Trait, string>();

    public override void OnInspectorGUI()
    {
        var database = (TraitDatabase)target;
        List<Trait> all = database.GetAllTraits().Where(t => t != null).ToList();
        if (checks.Due)
            RefreshProblems(all);

        EditorGUILayout.HelpBox(
            "Every trait of the game. The character creation screen lists the traits with 'Available At Creation' on. " +
            "Keep this asset in a Resources folder named exactly 'TraitDatabase' so it is found at runtime.", MessageType.Info);
        string path = AssetDatabase.GetAssetPath(database);
        if (!path.Contains("/Resources/") || System.IO.Path.GetFileNameWithoutExtension(path) != "TraitDatabase")
            EditorGUILayout.HelpBox("This database is not at Resources/TraitDatabase: the Trait Manager only finds it at runtime if it is assigned directly.", MessageType.Warning);

        // Counts.
        int passive = all.Count(t => t.Kind == TraitKind.Passive), active = all.Count(t => t.Kind == TraitKind.Active), hybrid = all.Count(t => t.Kind == TraitKind.Hybrid);
        EditorGUILayout.LabelField($"{all.Count} traits · {passive} passive · {active} active · {hybrid} hybrid · " +
                                   $"{all.Count(t => t.IsPositive)} cost points · {all.Count(t => t.IsNegative)} drawbacks · {all.Count(t => t.availableAtCreation)} at creation",
            EditorStyles.wordWrappedMiniLabel);

        // Tools.
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Collect All Traits", "Add every Trait asset in the project that is not in the database yet.")))
            CollectAll(database);
        if (GUILayout.Button(new GUIContent("Remove Empty", "Remove missing/deleted entries.")))
            database.RemoveNullTraits();
        if (GUILayout.Button(new GUIContent("Validate All", "Check every trait and log problems to the Console.")))
            ValidateAll(all);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("+ New Empty Trait", "Create an empty Trait asset next to this database and add it.")))
            CreateEmpty(database);
        if (GUILayout.Button(new GUIContent("+ New Trait From Preset ▾", "Create a ready-made trait (passive, drawback, double jump, wall climb, barrier...) next to this database.")))
            PresetMenu(database);
        EditorGUILayout.EndHorizontal();

        // Filters.
        EditorGUILayout.Space(4);
        search = EditorGUILayout.TextField(new GUIContent("Search", "Filter by name or description."), search);
        EditorGUILayout.BeginHorizontal();
        var typeNames = new List<string> { "All categories" };
        typeNames.AddRange(System.Enum.GetNames(typeof(TraitType)));
        typeFilter = EditorGUILayout.Popup(typeFilter + 1, typeNames.ToArray()) - 1;
        kindFilter = EditorGUILayout.Popup(kindFilter + 1, new[] { "All kinds", "Passive", "Active", "Hybrid" }) - 1;
        EditorGUILayout.EndHorizontal();

        // List.
        var shown = all.Where(t =>
            (string.IsNullOrEmpty(search) || t.Name.ToLowerInvariant().Contains(search.ToLowerInvariant()) || (t.description ?? "").ToLowerInvariant().Contains(search.ToLowerInvariant())) &&
            (typeFilter < 0 || (int)t.type == typeFilter) && (kindFilter < 0 || (int)t.Kind == kindFilter))
            .OrderBy(t => t.type).ThenBy(t => t.Name).ToList();
        foreach (Trait t in shown)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            Rect sw = GUILayoutUtility.GetRect(8, 16, GUILayout.Width(8));
            EditorGUI.DrawRect(new Rect(sw.x, sw.y + 2, 6, 12), t.traitColor);
            EditorGUILayout.LabelField(new GUIContent(t.Name, t.GetFormattedDescription()), EditorStyles.boldLabel, GUILayout.MinWidth(90));
            string cost = t.cost > 0 ? $"cost {t.cost}" : t.cost < 0 ? $"gives {-t.cost}" : "free";
            problems.TryGetValue(t, out string flags);
            problemDetails.TryGetValue(t, out string details);
            EditorGUILayout.LabelField(new GUIContent($"{t.Kind} · {t.type} · {cost}{(t.availableAtCreation ? "" : " · hidden")}{flags}", details),
                EditorStyles.miniLabel, GUILayout.MinWidth(150));
            if (GUILayout.Button(new GUIContent("Select", "Select the trait asset."), EditorStyles.miniButton, GUILayout.Width(50)))
                Selection.activeObject = t;
            if (GUILayout.Button(new GUIContent("✕", "Remove from the database (the asset is kept)."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                Undo.RecordObject(database, "Remove Trait From Database");
                database.RemoveTrait(t);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (shown.Count == 0)
            EditorGUILayout.LabelField(all.Count == 0 ? "No traits yet. Use 'New Trait From Preset' or 'Collect All Traits'." : "No trait matches the filters.", EditorStyles.miniLabel);

        showRaw = EditorGUILayout.Foldout(showRaw, "Raw list", true);
        if (showRaw)
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void RefreshProblems(List<Trait> all)
    {
        problems.Clear();
        problemDetails.Clear();
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (Trait t in all)
        {
            errors.Clear();
            warnings.Clear();
            t.Validate(errors, warnings);
            problems[t] = errors.Count > 0 ? $"  ✗ {errors.Count}" : warnings.Count > 0 ? $"  ⚠ {warnings.Count}" : "";
            problemDetails[t] = string.Join("\n", errors.Concat(warnings));
        }
    }

    private static string Folder(TraitDatabase db)
    {
        string p = AssetDatabase.GetAssetPath(db);
        return string.IsNullOrEmpty(p) ? "Assets" : System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
    }

    private static void CollectAll(TraitDatabase database)
    {
        var existing = new HashSet<Trait>(database.GetAllTraits());
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Trait"))
        {
            var trait = AssetDatabase.LoadAssetAtPath<Trait>(AssetDatabase.GUIDToAssetPath(guid));
            if (trait != null && !existing.Contains(trait))
            {
                database.AddTrait(trait);
                added++;
            }
        }
        EditorUtility.SetDirty(database);
        Debug.Log(added > 0 ? $"Added {added} traits to the database." : "No new traits found.", database);
    }

    private static void ValidateAll(List<Trait> all)
    {
        int e = 0, w = 0;
        foreach (Trait t in all)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            t.Validate(errors, warnings);
            foreach (string s in errors) Debug.LogError(s, t);
            foreach (string s in warnings) Debug.LogWarning(s, t);
            e += errors.Count;
            w += warnings.Count;
        }
        Debug.Log($"Validated {all.Count} traits: {e} errors, {w} warnings.");
    }

    private static void CreateEmpty(TraitDatabase database)
    {
        Trait t = CreateInstance<Trait>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder(database)}/New Trait.asset");
        AssetDatabase.CreateAsset(t, path);
        AssetDatabase.SaveAssets();
        database.AddTrait(t);
        Selection.activeObject = t;
    }

    private static void PresetMenu(TraitDatabase database)
    {
        var menu = new GenericMenu();
        foreach (TraitPresets.Preset p in TraitPresets.All)
        {
            TraitPresets.Preset captured = p;
            menu.AddItem(new GUIContent($"{p.category}/{p.name}", p.description), false, () =>
            {
                Trait t = TraitPresets.CreateAsset(captured, Folder(database));
                database.AddTrait(t);
                EditorGUIUtility.PingObject(t);
            });
        }
        menu.ShowAsContext();
    }
}
#endif
