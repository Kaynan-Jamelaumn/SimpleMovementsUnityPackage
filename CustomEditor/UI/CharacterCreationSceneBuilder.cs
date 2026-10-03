#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a complete character creation screen in the open scene, wired to <see cref="CharacterCreationUI"/>: name,
/// race (with description and height slider), class (with summary), traits (available / chosen lists, details, points)
/// and the Create Character button that spawns the player prefab at a spawn point with everything chosen. It finds
/// the player prefab, the classes, the races and the traits in the project. Tools ▸ SimpleMovements ▸ Scene UI ▸ Build Character Creation Screen,
/// or the button on the Character Creation UI.
/// </summary>
public static class CharacterCreationSceneBuilder
{
    private const string RootName = "CharacterCreation";

    [MenuItem("Tools/SimpleMovements/Scene UI/Build Character Creation Screen")]
    public static void BuildMenu()
    {
        CharacterCreationUI existing = Object.FindAnyObjectByType<CharacterCreationUI>();
        if (existing != null)
        {
            int choice = EditorUtility.DisplayDialogComplex("Character Creation Screen",
                $"The scene already has a Character Creation UI on '{existing.name}'.", "Rebuild It", "Cancel", "Select It");
            if (choice == 1) return;
            if (choice == 2) { Selection.activeObject = existing; return; }
            Rebuild(existing);
            return;
        }
        Build(null);
    }

    /// <summary>Replaces the screen of <paramref name="ui"/> with a new one; its prefab, spawn point and lists are kept.</summary>
    public static CharacterCreationUI Rebuild(CharacterCreationUI ui)
    {
        Saved keep = ui != null ? Saved.From(ui) : null;
        if (ui != null)
        {
            Canvas c = ui.GetComponentInParent<Canvas>();
            Undo.DestroyObjectImmediate(c != null ? c.rootCanvas.gameObject : ui.gameObject);
        }
        return Build(keep);
    }

    /// <summary>What a rebuild keeps from the old screen.</summary>
    private sealed class Saved
    {
        public Object playerPrefab;
        public Object spawnPoint;
        public List<Object> classes, races, traits;

        public static Saved From(CharacterCreationUI ui)
        {
            var so = new SerializedObject(ui);
            return new Saved
            {
                playerPrefab = so.FindProperty("playerPrefab").objectReferenceValue,
                spawnPoint = so.FindProperty("spawnPoint").objectReferenceValue,
                classes = ReadList(so.FindProperty("availableClasses")),
                races = ReadList(so.FindProperty("availableRaces")),
                traits = ReadList(so.FindProperty("allTraits")),
            };
        }
    }

    public static CharacterCreationUI Build(object keepData)
    {
        var keep = keepData as Saved;
        Undo.SetCurrentGroupName("Build Character Creation Screen");
        int group = Undo.GetCurrentGroup();
        UIBuildKit.EnsureEventSystem();

        Canvas canvas = UIBuildKit.Canvas(RootName, 100);
        GameObject root = canvas.gameObject;
        var ui = root.AddComponent<CharacterCreationUI>();

        Image bg = InventoryUIFactory.Panel("Background", root.transform, new Color(0.04f, 0.05f, 0.07f, 1f));
        InventoryUIFactory.Stretch(bg.rectTransform);

        RectTransform page = UIBuildKit.Column("Page", root.transform, 40, 18f);
        InventoryUIFactory.Stretch(page);
        UIBuildKit.Title(page, "Create Your Character", 42f);

        RectTransform columns = InventoryUIFactory.Rect("Columns", page);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(columns.gameObject, 28f);
        h.childAlignment = TextAnchor.UpperLeft;
        InventoryUIFactory.Size(columns, -1f, 200f).flexibleHeight = 1f;

        // ---------------------------------------------------------------- column 1: name, race, height, class
        RectTransform col1 = Column(columns, "Identity", 420f);
        UIBuildKit.Header(col1, "Name");
        TMP_InputField nameInput = UIBuildKit.InputField("NameInput", col1, "Enter a name…", 24);
        UIBuildKit.Header(col1, "Race");
        RectTransform raceList = UIBuildKit.ScrollList("RaceList", col1, 170f, out _);
        TextMeshProUGUI raceDescription = UIBuildKit.ScrollText("RaceDescription", col1, 150f, "Pick a race.", 15f, InventoryUIFactory.MutedColor);
        RectTransform heightRow = UIBuildKit.Row("HeightRow", col1, 34f);
        TextMeshProUGUI heightLabel = InventoryUIFactory.Text("Label", heightRow, "Height", 16f, InventoryUIFactory.TextColor);
        InventoryUIFactory.Size(heightLabel, 70f, 34f, 0f);
        Slider heightSlider = InventoryUIFactory.Slider("HeightSlider", heightRow);
        heightSlider.wholeNumbers = false;
        heightSlider.minValue = 1.5f;
        heightSlider.maxValue = 2f;
        heightSlider.value = 1.78f;
        TextMeshProUGUI heightText = InventoryUIFactory.Text("HeightText", heightRow, "Height", 16f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(heightText, 140f, 34f, 0f);
        UIBuildKit.Header(col1, "Class");
        RectTransform classList = UIBuildKit.ScrollList("ClassList", col1, 0f, out _);

        // ---------------------------------------------------------------- column 2: class summary
        RectTransform col2 = Column(columns, "Summary", -1f);
        Image summary = InventoryUIFactory.Panel("ClassSummaryPanel", col2, UIBuildKit.Section);
        InventoryUIFactory.Size(summary, -1f, 200f).flexibleHeight = 1f;
        RectTransform sumCol = UIBuildKit.Column("Content", summary.transform, 20, 12f);
        InventoryUIFactory.Stretch(sumCol);
        RectTransform sumHead = UIBuildKit.Row("Head", sumCol, 72f, 16f);
        Image classIcon = InventoryUIFactory.Panel("ClassIcon", sumHead, Color.white);
        classIcon.preserveAspect = true;
        InventoryUIFactory.Size(classIcon, 72f, 72f, 0f);
        TextMeshProUGUI className = InventoryUIFactory.Text("ClassName", sumHead, "Class", 30f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(className, -1f, 72f, 1f);
        TextMeshProUGUI classDescription = UIBuildKit.ScrollText("ClassDescription", sumCol, 0f, "", 16f, InventoryUIFactory.TextColor);
        TextMeshProUGUI classPoints = UIBuildKit.Body("ClassTraitPoints", sumCol, "Trait Points: 0", 17f);
        InventoryUIFactory.Size(classPoints, -1f, 26f);
        TextMeshProUGUI uniqueTraits = UIBuildKit.ScrollText("UniqueTraits", sumCol, 130f, "", 15f, InventoryUIFactory.MutedColor);

        // ---------------------------------------------------------------- column 3: traits
        RectTransform col3 = Column(columns, "Traits", 560f);
        Image traitPanel = InventoryUIFactory.Panel("TraitSelectionPanel", col3, new Color(0f, 0f, 0f, 0f));
        InventoryUIFactory.Size(traitPanel, -1f, 200f).flexibleHeight = 1f;
        RectTransform traitCol = UIBuildKit.Column("Content", traitPanel.transform, 0, 10f);
        InventoryUIFactory.Stretch(traitCol);
        TextMeshProUGUI pointsText = InventoryUIFactory.Text("TraitPoints", traitCol, "Available Points: 0", 20f, InventoryUIFactory.AccentColor, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(pointsText, -1f, 30f);
        UIBuildKit.Header(traitCol, "Available traits");
        RectTransform availableList = UIBuildKit.ScrollList("AvailableTraits", traitCol, 0f, out _);
        UIBuildKit.Header(traitCol, "Chosen traits");
        RectTransform chosenList = UIBuildKit.ScrollList("ChosenTraits", traitCol, 150f, out _);

        Image detail = InventoryUIFactory.Panel("TraitDetailPanel", traitCol, UIBuildKit.Section);
        InventoryUIFactory.Size(detail, -1f, 230f);
        RectTransform detCol = UIBuildKit.Column("Content", detail.transform, 14, 8f);
        InventoryUIFactory.Stretch(detCol);
        RectTransform detHead = UIBuildKit.Row("Head", detCol, 44f, 12f);
        Image traitIcon = InventoryUIFactory.Panel("TraitIcon", detHead, Color.white);
        traitIcon.preserveAspect = true;
        InventoryUIFactory.Size(traitIcon, 44f, 44f, 0f);
        TextMeshProUGUI traitName = InventoryUIFactory.Text("TraitName", detHead, "Trait", 22f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(traitName, -1f, 44f, 1f);
        TextMeshProUGUI traitCost = InventoryUIFactory.Text("TraitCost", detHead, "Cost", 17f, InventoryUIFactory.AccentColor, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(traitCost, 170f, 44f, 0f);
        TextMeshProUGUI traitDescription = UIBuildKit.ScrollText("TraitDescription", detCol, 0f, "", 15f, InventoryUIFactory.TextColor);
        RectTransform detButtons = UIBuildKit.Row("Buttons", detCol, 40f);
        Button addTrait = UIBuildKit.Button("AddTrait", detButtons, "Add Trait", null, 0f, 40f);
        Button removeTrait = UIBuildKit.Button("RemoveTrait", detButtons, "Remove Trait", null, 0f, 40f);

        // ---------------------------------------------------------------- bottom bar
        RectTransform bottom = UIBuildKit.Row("Bottom", page, 60f);
        TextMeshProUGUI tip = InventoryUIFactory.Text("Tip", bottom, "Pick a race and a class, spend your trait points, name your character.", 16f, InventoryUIFactory.MutedColor);
        InventoryUIFactory.Size(tip, -1f, 60f, 1f);
        Button create = UIBuildKit.Button("CreateCharacter", bottom, "Create Character", null, 300f, 60f);
        ((Image)create.targetGraphic).color = InventoryUIFactory.AccentColor;

        // ---------------------------------------------------------------- templates (inactive, outside every list)
        RectTransform templates = InventoryUIFactory.Rect("Templates", root.transform);
        GameObject classButton = UIBuildKit.ItemTemplate("ClassButton", templates, "Class", 44f);
        GameObject traitButton = UIBuildKit.ItemTemplate("TraitButton", templates, "Trait (0)", 38f);
        GameObject chosenButton = UIBuildKit.ItemTemplate("ChosenTraitButton", templates, "Trait (0)", 38f);
        ((Image)chosenButton.GetComponent<Button>().targetGraphic).color = new Color(0.35f, 0.6f, 1f, 0.35f);

        // ---------------------------------------------------------------- spawn point, camera
        Transform spawn = keep?.spawnPoint as Transform;
        if (spawn == null)
        {
            GameObject sp = GameObject.Find("PlayerSpawnPoint");
            if (sp == null)
            {
                sp = new GameObject("PlayerSpawnPoint");
                Undo.RegisterCreatedObjectUndo(sp, "Create Spawn Point");
            }
            spawn = sp.transform;
        }
        var hideOnCreate = new List<Object>();
        if (Object.FindAnyObjectByType<Camera>() == null)
        {
            var camGo = new GameObject("Creation Camera", typeof(Camera), typeof(AudioListener));
            Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            camGo.transform.position = spawn.position + new Vector3(0f, 1.6f, -4f);
            hideOnCreate.Add(camGo);
        }

        // ---------------------------------------------------------------- wiring
        GameObject prefab = keep?.playerPrefab as GameObject;
        if (prefab == null)
            prefab = FindPlayerPrefab();
        UIBuildKit.Assign(ui,
            ("characterNameInput", nameInput), ("classListContainer", classList), ("classButtonPrefab", classButton),
            ("classSummaryPanel", summary.gameObject), ("classIcon", classIcon), ("className", className),
            ("classDescription", classDescription), ("classTraitPoints", classPoints), ("uniqueTraitsText", uniqueTraits),
            ("traitSelectionPanel", traitPanel.gameObject), ("currentTraitPointsText", pointsText),
            ("availableTraitsContainer", availableList), ("selectedTraitsContainer", chosenList),
            ("traitButtonPrefab", traitButton), ("selectedTraitPrefab", chosenButton),
            ("traitDetailPanel", detail.gameObject), ("traitDetailIcon", traitIcon), ("traitDetailName", traitName),
            ("traitDetailDescription", traitDescription), ("traitDetailCost", traitCost),
            ("addTraitButton", addTrait), ("removeTraitButton", removeTrait), ("createPlayerButton", create),
            ("playerPrefab", prefab), ("spawnPoint", spawn),
            ("raceListContainer", raceList), ("raceDescription", raceDescription), ("heightSlider", heightSlider), ("heightText", heightText));
        AssignPrefabComponents(ui, prefab);

        var so = new SerializedObject(ui);
        WriteList(so.FindProperty("availableClasses"), keep != null && keep.classes.Count > 0 ? keep.classes : FindAssets<PlayerClass>().Cast<Object>().ToList());
        WriteList(so.FindProperty("availableRaces"), keep != null && keep.races.Count > 0 ? keep.races : FindAssets<CharacterArchetype>()
            .Where(a => a.kind == ArchetypeKind.Race && a.availableAtCreation).Cast<Object>().ToList());
        WriteList(so.FindProperty("allTraits"), keep != null && keep.traits.Count > 0 ? keep.traits : FindCreationTraits());
        WriteList(so.FindProperty("hideOnCreate"), hideOnCreate);
        SerializedProperty raceRequired = so.FindProperty("raceRequired");
        if (raceRequired != null)
            raceRequired.boolValue = so.FindProperty("availableRaces").arraySize > 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeObject = root;
        Report(ui, prefab);
        return ui;
    }

    private static RectTransform Column(Transform parent, string name, float width)
    {
        RectTransform col = UIBuildKit.Column(name, parent, 0, 10f);
        LayoutElement le = InventoryUIFactory.Size(col, width > 0f ? width : -1f, -1f, width > 0f ? 0f : 1f);
        le.flexibleHeight = 1f;
        return col;
    }

    // ------------------------------------------------------------------ project lookups
    /// <summary>The selected prefab if it is a player, else the first prefab in the project with a Player Status Controller.</summary>
    public static GameObject FindPlayerPrefab()
    {
        if (Selection.activeObject is GameObject sel && PrefabUtility.IsPartOfPrefabAsset(sel) && sel.GetComponentInChildren<PlayerStatusController>(true) != null)
            return sel;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && go.GetComponentInChildren<PlayerStatusController>(true) != null)
                return go;
        }
        return null;
    }

    /// <summary>
    /// Every asset of type <typeparamref name="T"/> in the project. Falls back to checking every ScriptableObject when
    /// the type search finds nothing (it misses assets whose script file name differs from the class name).
    /// </summary>
    public static List<T> FindAssets<T>() where T : Object
    {
        List<T> found = AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null).Distinct().OrderBy(a => a.name).ToList();
        if (found.Count > 0)
            return found;
        return AssetDatabase.FindAssets("t:ScriptableObject")
            .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)) as T)
            .Where(a => a != null).Distinct().OrderBy(a => a.name).ToList();
    }

    public static List<Object> FindCreationTraits()
    {
        TraitDatabase db = TraitDatabase.Instance;
        List<Trait> traits = db != null ? db.GetCreationTraits() : FindAssets<Trait>().Where(t => t.availableAtCreation).ToList();
        return traits.Where(t => t != null).Cast<Object>().ToList();
    }

    public static void AssignPrefabComponents(CharacterCreationUI ui, GameObject prefab)
    {
        if (prefab == null)
            return;
        UIBuildKit.Assign(ui,
            ("prefabStatusController", prefab.GetComponentInChildren<PlayerStatusController>(true)),
            ("prefabNameComponent", prefab.GetComponentInChildren<PlayerNameComponent>(true)),
            ("prefabTraitManager", prefab.GetComponentInChildren<TraitManager>(true)));
    }

    public static List<Object> ReadList(SerializedProperty list)
    {
        var result = new List<Object>();
        if (list == null) return result;
        for (int i = 0; i < list.arraySize; i++)
            result.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
        return result;
    }

    public static void WriteList(SerializedProperty list, IList<Object> values)
    {
        if (list == null) return;
        list.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void Report(CharacterCreationUI ui, GameObject prefab)
    {
        var so = new SerializedObject(ui);
        int classes = so.FindProperty("availableClasses").arraySize, races = so.FindProperty("availableRaces").arraySize;
        int traits = so.FindProperty("allTraits").arraySize;
        string msg = $"[UI Builder] Character creation screen built: {classes} classes, {races} races, {traits} traits" +
                     (prefab != null ? $", player prefab '{prefab.name}'." : ". No player prefab found - assign one on the Character Creation UI.");
        if (classes == 0)
            msg += " No Player Class assets found: create one (Create ▸ SimpleMovements ▸ Character ▸ Player Class).";
        if (prefab != null && prefab.GetComponentInChildren<PlayerNameComponent>(true) == null)
            msg += " The player prefab has no Player Name Component (the name is only used as the object's name).";
        Debug.Log(msg + " Move 'PlayerSpawnPoint' to where the player should appear.", ui);
    }
}

/// <summary>The build tools shown at the top of the Character Creation UI inspector.</summary>
public static class CharacterCreationBuildTools
{
    public static void Draw(CharacterCreationUI ui, SerializedObject so)
    {
        EditorGUILayout.HelpBox("The character creation screen: name, race and height, class, traits. Create Character spawns the Player " +
                                "Prefab at the Spawn Point with the choices applied (class, race, height, traits, name).", MessageType.None);
        var missing = new List<string>();
        if (so.FindProperty("playerPrefab").objectReferenceValue == null) missing.Add("Player Prefab");
        if (so.FindProperty("characterNameInput").objectReferenceValue == null) missing.Add("the UI (press Rebuild Screen)");
        if (so.FindProperty("availableClasses").arraySize == 0) missing.Add("Player Classes");
        if (missing.Count > 0)
            EditorGUILayout.HelpBox("Missing: " + string.Join(", ", missing), MessageType.Warning);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Rebuild Screen", "Replaces this screen with a freshly generated one (prefab, spawn point and lists are kept)."), GUILayout.Height(26f)))
            {
                CharacterCreationSceneBuilder.Rebuild(ui);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(new GUIContent("Find Classes, Races & Traits", "Fills the lists with every Player Class, every Race archetype offered at creation and every creation trait in the project."), GUILayout.Height(26f)))
            {
                Undo.RecordObject(ui, "Find Classes, Races & Traits");
                CharacterCreationSceneBuilder.WriteList(so.FindProperty("availableClasses"), CharacterCreationSceneBuilder.FindAssets<PlayerClass>().Cast<Object>().ToList());
                CharacterCreationSceneBuilder.WriteList(so.FindProperty("availableRaces"), CharacterCreationSceneBuilder.FindAssets<CharacterArchetype>()
                    .Where(a => a.kind == ArchetypeKind.Race && a.availableAtCreation).Cast<Object>().ToList());
                CharacterCreationSceneBuilder.WriteList(so.FindProperty("allTraits"), CharacterCreationSceneBuilder.FindCreationTraits());
                so.ApplyModifiedProperties();
            }
        }
        EditorGUILayout.Space();
    }
}
#endif
