#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Character Catalog inspector (Fill From Project, setup checks) and the menu that creates the catalog in a Resources
/// folder, where the creation screen and the server find it in builds.
/// </summary>
[CustomEditor(typeof(CharacterCatalog))]
public class CharacterCatalogEditor : Editor
{
    private const string AssetPath = "Assets/Resources/CharacterCatalog.asset";

    [MenuItem(SMMenu.Root + "Project Setup/Create Character Catalog (Resources)", false, SMMenu.ProjectSetup + 3)]
    public static void CreateCatalog()
    {
        CharacterCatalog existing = Resources.Load<CharacterCatalog>("CharacterCatalog");
        if (existing != null)
        {
            if (EditorUtility.DisplayDialog("Character Catalog", $"A catalog already exists at {AssetDatabase.GetAssetPath(existing)}.", "Fill From Project", "Select It"))
                Fill(existing, true);
            Selection.activeObject = existing;
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        var catalog = CreateInstance<CharacterCatalog>();
        Fill(catalog, false);
        AssetDatabase.CreateAsset(catalog, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
        Debug.Log($"[Characters] Character Catalog created at {AssetPath} with {catalog.races.Count} race(s) and {catalog.classes.Count} class(es).", catalog);
    }

    private const string ExampleFolder = "Assets/Characters/Races";

    /// <summary>
    /// Creates four example races with their look (height, build, ears, beards) and a few stats, and adds them to the
    /// catalog: Human, Dwarf (short, broad, always bearded), Elf (tall, slender, pointed ears), Barbarian (tall, broad).
    /// Existing assets with those names are kept.
    /// </summary>
    [MenuItem(SMMenu.Root + "Project Setup/Create Example Races (Human, Dwarf, Elf, Barbarian)", false, SMMenu.ProjectSetup + 4)]
    public static void CreateExampleRaces()
    {
        Directory.CreateDirectory(ExampleFolder);
        var made = new List<CharacterArchetype>();
        made.Add(Race("Human", "Adaptable and ambitious: humans learn a little of everything.", new Vector2(1.6f, 1.95f), 1.78f, a =>
        {
            a.bonusTraitPoints = 2;
        }));
        made.Add(Race("Dwarf", "Short, broad and stubborn as the mountains they carve. Every dwarf wears a beard with pride.", new Vector2(1.2f, 1.45f), 1.35f, a =>
        {
            a.appearance.bodyWidth = 1.22f;
            a.appearance.alwaysBeard = true;
            a.appearance.beardOnAnyBody = true;
            a.appearance.defaultBeard = 5; // Long Beard (placeholder list)
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Endurance, 3f));
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Strength, 2f));
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Agility, -1f));
        }));
        made.Add(Race("Elf", "Tall, graceful and long-lived, with sharp senses and pointed ears.", new Vector2(1.75f, 2.0f), 1.86f, a =>
        {
            a.appearance.bodyWidth = 0.92f;
            a.appearance.ears = EarShape.Pointed;
            a.appearance.skinTones = new[]
            {
                new Color(1f, 0.9f, 0.82f), new Color(0.96f, 0.84f, 0.74f), new Color(0.85f, 0.7f, 0.58f), new Color(0.62f, 0.5f, 0.6f), new Color(0.45f, 0.42f, 0.55f),
            };
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Agility, 3f));
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Intelligence, 2f));
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Strength, -1f));
        }));
        made.Add(Race("Barbarian", "Humans of the wild north: bigger, heavier and harder to kill.", new Vector2(1.85f, 2.1f), 1.95f, a =>
        {
            a.appearance.bodyWidth = 1.16f;
            a.appearance.blendShapes.Add(new NamedBlendShape { shape = "Muscular", weight = 100f });
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Strength, 4f));
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Endurance, 2f));
            a.combatStats.Add(new CombatStatModifier(CombatStatType.Intelligence, -2f));
        }));
        AssetDatabase.SaveAssets();

        CharacterCatalog catalog = Resources.Load<CharacterCatalog>("CharacterCatalog");
        if (catalog == null)
        {
            CreateCatalog();
            catalog = Resources.Load<CharacterCatalog>("CharacterCatalog");
        }
        if (catalog != null)
        {
            Undo.RecordObject(catalog, "Add Example Races");
            foreach (CharacterArchetype a in made)
                if (a != null && !catalog.races.Contains(a)) catalog.races.Add(a);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = made[1];
        Debug.Log($"[Characters] Example races in {ExampleFolder}: Human, Dwarf, Elf, Barbarian (added to the Character Catalog). " +
                  "Their look is under Appearance; height under Body.");
    }

    private static CharacterArchetype Race(string name, string description, Vector2 heights, float height, System.Action<CharacterArchetype> setup)
    {
        string path = $"{ExampleFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CharacterArchetype>(path);
        if (existing != null)
            return existing;
        var a = CreateInstance<CharacterArchetype>();
        a.kind = ArchetypeKind.Race;
        a.displayName = name;
        a.description = description;
        a.setsHeight = true;
        a.heightRange = heights;
        a.defaultHeight = height;
        setup(a);
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    /// <summary>Adds every race (archetype of kind Race) and class of the project that is not listed yet.</summary>
    public static void Fill(CharacterCatalog catalog, bool undo)
    {
        if (undo) Undo.RecordObject(catalog, "Fill Character Catalog");
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterArchetype"))
        {
            var a = AssetDatabase.LoadAssetAtPath<CharacterArchetype>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && a.kind == ArchetypeKind.Race && !catalog.races.Contains(a))
                catalog.races.Add(a);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerClass"))
        {
            var c = AssetDatabase.LoadAssetAtPath<PlayerClass>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && !catalog.classes.Contains(c))
                catalog.classes.Add(c);
        }
        catalog.races.RemoveAll(r => r == null);
        catalog.classes.RemoveAll(c => c == null);
        EditorUtility.SetDirty(catalog);
    }

    public override void OnInspectorGUI()
    {
        var catalog = (CharacterCatalog)target;
        EditorGUILayout.HelpBox("What the character creation screen offers, and the rules the server checks every new character against. " +
                                "Races are archetypes of kind Race; traits come from each class's trait list (or the list below, " +
                                "else the Trait Database's creation traits).", MessageType.None);
        if (!AssetDatabase.GetAssetPath(catalog).Contains("/Resources/") || catalog.name != "CharacterCatalog")
            EditorGUILayout.HelpBox("Name it 'CharacterCatalog' and keep it in a Resources folder, or builds cannot find it.", MessageType.Warning);
        var problems = new List<string>();
        if (catalog.classes.FindAll(c => c != null).Count == 0)
            problems.Add("No class: the creation screen cannot create a character.");
        foreach (CharacterArchetype r in catalog.races)
            if (r != null && r.kind != ArchetypeKind.Race)
                problems.Add($"'{r.Name}' is a {r.kind} archetype, not a Race.");
        foreach (CharacterArchetype r in catalog.races)
        {
            if (r == null) continue;
            bool any = catalog.classes.Exists(c => c != null && (c.archetype == null || r.IsCompatibleWith(c.archetype)));
            if (!any && catalog.classes.Count > 0) problems.Add($"{r.Name} cannot be combined with any class.");
        }
        foreach (string p in problems)
            EditorGUILayout.HelpBox(p, MessageType.Warning);
        if (GUILayout.Button(new GUIContent("Fill From Project", "Adds every race and class asset of the project that is not listed yet."), GUILayout.Height(24f)))
            Fill(catalog, true);
        EditorGUILayout.Space();
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Saved characters", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(CharacterSaveStore.Folder, EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Folder"))
            {
                Directory.CreateDirectory(CharacterSaveStore.Folder);
                EditorUtility.RevealInFinder(CharacterSaveStore.Folder);
            }
            if (GUILayout.Button("Delete All Saved Characters") &&
                EditorUtility.DisplayDialog("Saved characters", "Delete every character saved on this computer?", "Delete", "Cancel"))
            {
                foreach (CharacterSave s in CharacterSaveStore.LoadAll())
                    CharacterSaveStore.Delete(s);
            }
        }
    }
}

/// <summary>Character Appearance inspector: what is placeholder and what is set up, and a random preview.</summary>
[CustomEditor(typeof(CharacterAppearance))]
public class CharacterAppearanceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var a = (CharacterAppearance)target;
        EditorGUILayout.HelpBox("Body type, hair, beard, eyes and nose chosen on the creation screen are shown here. Parts without options " +
                                "use placeholder shapes on the head; list real options (objects of the model, prefabs on a bone, or blend " +
                                "shapes) to replace them. Male / Female objects or blend shapes switch the body.", MessageType.None);
        var lines = new List<string>();
        foreach (AppearancePart part in (AppearancePart[])System.Enum.GetValues(typeof(AppearancePart)))
        {
            int own = a.Options(part) != null ? a.Options(part).Count : 0;
            if (part == AppearancePart.Body)
                lines.Add(own > 0 ? $"Body: default + {own} body model(s)" : "Body: default only (add Body Models for other bodies)");
            else
                lines.Add($"{part}: " + (a.UsesPlaceholder(part) ? "placeholder shapes"
                    : a.PlaceholderCount(part) > 0 ? $"placeholder shapes + {own} of yours" : $"{own} of yours"));
        }
        bool body = a.maleObjects.Length > 0 || a.femaleObjects.Length > 0 || a.femaleBlendShapes.Length > 0 || a.maleBlendShapes.Length > 0;
        lines.Add("Body type: " + (body ? "set up" : a.placeholderBody != PlaceholderBodyMode.Off ? "placeholder body (male / female shapes)" : "not set up (male and female look the same)"));
        if (a.placeholderBody != PlaceholderBodyMode.Off)
            lines.Add(a.placeholderBody == PlaceholderBodyMode.Always
                ? "Placeholder body: always built in Play Mode"
                : "Placeholder body: built in Play Mode when the model has no skinned mesh (torso, arms, legs, hands, feet, head skin)");
        EditorGUILayout.HelpBox(string.Join("\n", lines), body ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.HelpBox("Modular character (one mesh per hairstyle, beard, eyebrows, head, and male / female body parts)? " +
                                "Scan Model lists them as choices by their names and fills the Body Regions that armour hides.", MessageType.None);
        if (GUILayout.Button(new GUIContent("Scan Model for Parts", "Hair / beard / eyebrow / head meshes become choices; male and female body " +
                                                                    "parts become the Male / Female Objects; every part goes into its body region."), GUILayout.Height(24f)))
        {
            Undo.RecordObject(a, "Scan Model for Parts");
            string report = a.ScanModel();
            EditorUtility.SetDirty(a);
            if (PrefabUtility.IsPartOfPrefabInstance(a))
                PrefabUtility.RecordPrefabInstancePropertyModifications(a);
            EditorUtility.DisplayDialog("Scan Model", report, "OK");
        }
        if (Application.isPlaying && GUILayout.Button("Preview Random Appearance"))
            a.Apply(CharacterAppearanceData.Random(a));
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
#endif
