#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Menus of the smithing, smelting and leatherworking system: the project's Crafting Database, ready stations in the
/// scene (a smithy, a smeltery, a tannery, a leatherworks, a complete workshop, and a test station with everything),
/// ore deposits, stock item assets (bars, ores, hides, blanks, fuel, supplies, agents, fasteners) with generated
/// icons, the design templates (a weapon or armour asset per design: the attacks and models crafted items copy),
/// validation and the documentation.
/// </summary>
public static class CraftingEditorTools
{
    private const string Menu = SMMenu.Root + "Crafting/";

    /// <summary>What a station preset has.</summary>
    public enum StationPreset { Smithy, Smeltery, Tannery, Leatherworks, Complete, Test }

    [MenuItem(SMMenu.Root + "Project Setup/Create Crafting Database (Resources)", false, SMMenu.ProjectSetup + 9)]
    public static CraftDatabase CreateDatabaseMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Smithy (forge, anvil, quench, grinder)", false, SMMenu.Crafting)]
    private static void SmithyMenu() => CreateStation(StationPreset.Smithy);

    [MenuItem(Menu + "Create Smeltery (ore bench, furnace, crucible)", false, SMMenu.Crafting + 1)]
    private static void SmelteryMenu() => CreateStation(StationPreset.Smeltery);

    [MenuItem(Menu + "Create Tannery (beam, vats, frame, rack)", false, SMMenu.Crafting + 2)]
    private static void TanneryMenu() => CreateStation(StationPreset.Tannery);

    [MenuItem(Menu + "Create Leatherworks (cutting and assembly)", false, SMMenu.Crafting + 3)]
    private static void LeatherworksMenu() => CreateStation(StationPreset.Leatherworks);

    [MenuItem(Menu + "Create Complete Workshop", false, SMMenu.Crafting + 4)]
    private static void CompleteMenu() => CreateStation(StationPreset.Complete);

    [MenuItem(Menu + "Create Test Station (everything, free materials, numbers shown)", false, SMMenu.Crafting + 5)]
    private static void TestMenu() => CreateStation(StationPreset.Test);

    [MenuItem(Menu + "Create Ore Deposit", false, SMMenu.Crafting + 6)]
    private static void DepositMenu() => CreateDeposit("hematite");

    [MenuItem(Menu + "Create Stock Items", false, SMMenu.Crafting + 20)]
    private static void StockMenu()
    {
        List<CraftStockItemSO> made = CreateStockItems(true);
        EditorUtility.DisplayDialog("Crafting", $"{made.Count} stock item(s) in {SMAssets.FolderPath(SMAssets.CraftingStock)}. " +
            "Add them to merchants, loot tables or starting items; ore deposits give their ore by themselves.", "OK");
    }

    [MenuItem(Menu + "Create Design Templates", false, SMMenu.Crafting + 21)]
    private static void TemplatesMenu()
    {
        int n = CreateDesignTemplates(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Crafting", $"{n} design template(s) in {SMAssets.FolderPath(SMAssets.CraftingTemplates)}, assigned in the Crafting " +
            "Database. Crafted items copy their template's attacks, models and sounds; their numbers come from how they were made.", "OK");
    }

    [MenuItem(Menu + "Validate Crafting Database", false, SMMenu.Crafting + 40)]
    private static void ValidateMenu()
    {
        CraftDatabase db = CraftDatabase.Instance;
        var errors = new List<string>();
        var warnings = new List<string>();
        db.MarkChanged();
        db.Validate(errors, warnings);
        foreach (string e in errors) Debug.LogError("[Crafting] " + e, db);
        foreach (string w in warnings) Debug.LogWarning("[Crafting] " + w, db);
        Debug.Log($"[Crafting] {db.name}: {errors.Count} error(s), {warnings.Count} warning(s).", db);
    }

    [MenuItem(Menu + "Help", false, SMMenu.Crafting + 60)]
    private static void HelpMenu()
    {
        foreach (string g in AssetDatabase.FindAssets("README"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Replace('\\', '/').Contains("Documentation/Crafting/"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }
        }
        EditorUtility.DisplayDialog("Crafting", "See Documentation/Crafting/README.md.", "OK");
    }

    // ------------------------------------------------------------------ the database
    /// <summary>The project's Crafting Database (Resources/SimpleMovements/Settings), made from the built-in content when missing.</summary>
    public static CraftDatabase ProjectDatabase(bool select)
    {
        CraftDatabase db = SMAssetsEditor.FindOrCreate<CraftDatabase>(SMAssets.Settings, CraftDatabase.ResourcesName, d => CraftDefaults.Fill(d), select);
        CraftDatabase.Override(null);
        return db;
    }

    // ------------------------------------------------------------------ stations
    public static CraftTools ToolsOf(StationPreset p)
    {
        switch (p)
        {
            case StationPreset.Smithy: return CraftTools.Smithy;
            case StationPreset.Smeltery: return CraftTools.Smeltery;
            case StationPreset.Tannery: return CraftTools.Tannery;
            case StationPreset.Leatherworks: return CraftTools.Leatherworks;
            default: return CraftTools.Everything;
        }
    }

    /// <summary>
    /// A station in the open scene at the Scene view's pivot: the work places for its tools laid out in a row (built from
    /// primitives - replace them with your own art), a collider, the station, its visuals (fire glow, sparks, steam) and
    /// its surroundings. A test station has everything, free materials and shows the numbers.
    /// </summary>
    public static CraftStation CreateStation(StationPreset preset)
    {
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        bool test = preset == StationPreset.Test;
        string title = test ? "Crafting Test Station" : preset == StationPreset.Complete ? "Workshop" : preset.ToString();
        var root = new GameObject(title);
        Undo.RegisterCreatedObjectUndo(root, "Create Crafting Station");
        root.transform.position = at;
        CraftTools tools = ToolsOf(preset);

        Color iron = new Color(0.2f, 0.2f, 0.22f), stone = new Color(0.42f, 0.4f, 0.38f), wood = new Color(0.42f, 0.29f, 0.17f), water = new Color(0.25f, 0.4f, 0.55f);
        var places = new List<(string name, Transform point)>();
        float x = 0f;
        Transform Place(string name, float width)
        {
            var p = new GameObject(name);
            p.transform.SetParent(root.transform, false);
            p.transform.localPosition = new Vector3(x + width * 0.5f, 0f, 0f);
            x += width + 0.3f;
            places.Add((name, p.transform));
            return p.transform;
        }

        Transform forge = null, anvil = null, quench = null, furnace = null, grinder = null, vats = null;
        if ((tools & CraftTools.Forge) != 0)
        {
            forge = Place("Forge", 1.2f);
            Shape(PrimitiveType.Cube, "Hearth", forge, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.9f), stone);
            Shape(PrimitiveType.Cube, "Hood", forge, new Vector3(0f, 1.8f, 0.2f), new Vector3(1f, 0.6f, 0.6f), stone);
        }
        if ((tools & CraftTools.Anvil) != 0)
        {
            anvil = Place("Anvil", 0.8f);
            Shape(PrimitiveType.Cylinder, "Stump", anvil, new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.3f, 0.45f), wood);
            Shape(PrimitiveType.Cube, "Anvil Body", anvil, new Vector3(0f, 0.72f, 0f), new Vector3(0.55f, 0.2f, 0.2f), iron);
            Shape(PrimitiveType.Cube, "Horn", anvil, new Vector3(-0.38f, 0.75f, 0f), new Vector3(0.22f, 0.1f, 0.1f), iron);
        }
        if ((tools & CraftTools.QuenchTank) != 0)
        {
            quench = Place("Quench Tank", 0.8f);
            Shape(PrimitiveType.Cylinder, "Tank", quench, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.7f), wood);
            Shape(PrimitiveType.Cylinder, "Water", quench, new Vector3(0f, 0.58f, 0f), new Vector3(0.62f, 0.02f, 0.62f), water);
        }
        if ((tools & CraftTools.Oven) != 0)
        {
            Transform oven = Place("Tempering Oven", 0.7f);
            Shape(PrimitiveType.Cube, "Oven", oven, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0.6f), iron);
        }
        if ((tools & CraftTools.Grinder) != 0)
        {
            grinder = Place("Grinding Wheel", 0.8f);
            GameObject wheel = Shape(PrimitiveType.Cylinder, "Wheel", grinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.06f, 0.6f), stone);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shape(PrimitiveType.Cube, "Frame", grinder, new Vector3(0f, 0.35f, 0f), new Vector3(0.5f, 0.7f, 0.3f), wood);
        }
        if ((tools & (CraftTools.Files | CraftTools.Whetstone | CraftTools.Polishing)) != 0)
        {
            Transform bench = Place("Finishing Bench", 1f);
            Shape(PrimitiveType.Cube, "Bench", bench, new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 0.6f), wood);
        }
        if ((tools & CraftTools.OreBench) != 0)
        {
            Transform ore = Place("Ore Bench", 1f);
            Shape(PrimitiveType.Cube, "Bench", ore, new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 0.7f), stone);
        }
        if ((tools & CraftTools.Furnace) != 0)
        {
            furnace = Place("Bloomery", 1f);
            Shape(PrimitiveType.Cylinder, "Shaft", furnace, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.8f, 0.9f), new Color(0.5f, 0.33f, 0.24f));
        }
        if ((tools & CraftTools.Crucible) != 0)
        {
            Transform crucible = Place("Crucible", 0.6f);
            Shape(PrimitiveType.Cylinder, "Crucible", crucible, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.15f, 0.3f), new Color(0.45f, 0.4f, 0.36f));
        }
        if ((tools & (CraftTools.FleshingBeam | CraftTools.Frame | CraftTools.Rack)) != 0)
        {
            Transform beam = Place("Fleshing Beam and Frame", 1.4f);
            GameObject log = Shape(PrimitiveType.Cylinder, "Beam", beam, new Vector3(-0.3f, 0.5f, 0f), new Vector3(0.25f, 0.6f, 0.25f), wood);
            log.transform.localRotation = Quaternion.Euler(0f, 0f, 60f);
            Shape(PrimitiveType.Cube, "Frame", beam, new Vector3(0.4f, 0.9f, 0f), new Vector3(0.8f, 1.6f, 0.06f), wood);
        }
        if ((tools & CraftTools.Vats) != 0)
        {
            vats = Place("Vats", 2f);
            for (int i = 0; i < 3; i++)
                Shape(PrimitiveType.Cylinder, $"Vat {i + 1}", vats, new Vector3(-0.65f + i * 0.65f, 0.35f, 0f), new Vector3(0.55f, 0.35f, 0.55f), new Color(0.35f, 0.25f, 0.15f));
        }
        if ((tools & (CraftTools.LeatherBench | CraftTools.AssemblyBench)) != 0)
        {
            Transform bench = Place("Assembly Bench", 1.4f);
            Shape(PrimitiveType.Cube, "Bench", bench, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 0.7f), wood);
        }
        // Centre the row on the root and cover it with one collider (the interaction point is its middle).
        foreach ((string _, Transform t) in places)
            t.localPosition -= new Vector3(x * 0.5f, 0f, 0f);
        foreach (Collider c in root.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.6f, 0f);
        box.size = new Vector3(Mathf.Max(1f, x), 1.2f, 1f);

        var station = root.AddComponent<CraftStation>();
        var so = new SerializedObject(station);
        so.FindProperty("stationName").stringValue = title;
        so.FindProperty("tools").intValue = (int)tools;
        so.FindProperty("testStation").boolValue = test;
        so.FindProperty("instruments").intValue = (int)(test ? CraftInstruments.All : CraftInstruments.Magnet | CraftInstruments.TestFile);
        so.FindProperty("range").floatValue = Mathf.Max(3f, x * 0.5f + 1.5f);
        so.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<AlchemySurroundings>();

        var visuals = root.AddComponent<CraftStationVisuals>();
        var vso = new SerializedObject(visuals);
        vso.FindProperty("station").objectReferenceValue = station;
        if (forge != null) vso.FindProperty("forgePoint").objectReferenceValue = Point(forge, 0.85f);
        if (anvil != null) vso.FindProperty("anvilPoint").objectReferenceValue = Point(anvil, 0.85f);
        if (quench != null) vso.FindProperty("quenchPoint").objectReferenceValue = Point(quench, 0.6f);
        if (furnace != null) vso.FindProperty("furnacePoint").objectReferenceValue = Point(furnace, 1.65f);
        if (grinder != null) vso.FindProperty("grinderPoint").objectReferenceValue = Point(grinder, 0.75f);
        if (vats != null) vso.FindProperty("vatPoint").objectReferenceValue = Point(vats, 0.7f);
        vso.ApplyModifiedPropertiesWithoutUndo();
        MakeNetworked(root); // with Fish-Net in the project: the server runs it, every client follows

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log(test
            ? "[Crafting] Created a test station: every tool and instrument, every material free, and its window shows the numbers. Walk up to it and press Interact."
            : $"[Crafting] Created a {title.ToLowerInvariant()}. Give the player stock (Tools ▸ SimpleMovements ▸ Crafting ▸ Create Stock Items) and press Interact at it.", root);
        return station;
    }

    /// <summary>
    /// Adds a NetworkObject and a NetCraftStation (Fish-Net), so the station works in a network session with the server
    /// deciding everything. False when Fish-Net is not in the project.
    /// </summary>
    public static bool MakeNetworked(GameObject root)
    {
        Type nob = SimpleMovements.Setup.SetupUtil.TypeByName("FishNet.Object.NetworkObject");
        Type net = SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetCraftStation");
        if (root == null || nob == null || net == null)
            return false;
        if (root.GetComponent(nob) == null)
            Undo.AddComponent(root, nob);
        if (root.GetComponent(net) == null)
            Undo.AddComponent(root, net);
        return true;
    }

    private static Transform Point(Transform place, float height)
    {
        var p = new GameObject("Effects Point");
        p.transform.SetParent(place, false);
        p.transform.localPosition = new Vector3(0f, height, 0f);
        return p.transform;
    }

    /// <summary>A seam of <paramref name="ore"/> at the Scene view's pivot (a rough rock; replace it with your own).</summary>
    public static OreDeposit CreateDeposit(string ore)
    {
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        CraftOreDefinition o = CraftDatabase.Instance.Rules.Ore(ore);
        GameObject rock = Shape(PrimitiveType.Sphere, $"{o?.Name ?? "Ore"} Deposit", null, at + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 1.1f), o != null ? Color.Lerp(o.gangueColor, o.color, 0.5f) : Color.gray);
        Undo.RegisterCreatedObjectUndo(rock, "Create Ore Deposit");
        var d = rock.AddComponent<OreDeposit>();
        var so = new SerializedObject(d);
        so.FindProperty("ore").stringValue = ore;
        so.ApplyModifiedPropertiesWithoutUndo();
        Selection.activeGameObject = rock;
        return d;
    }

    private static GameObject Shape(PrimitiveType type, string n, Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = n;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = NewMaterial(color);
            if (m != null) r.sharedMaterial = m;
        }
        return go;
    }

    private static Material NewMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        var m = new Material(shader) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        return m;
    }

    // ------------------------------------------------------------------ stock
    /// <summary>
    /// Stock item assets (Resources/SimpleMovements/Items/Crafting/Stock) with generated icons: the stock metals, every
    /// ore, hide, wood, fuel, supply and agent of the database, and ready-made fasteners. Existing ones are kept.
    /// </summary>
    public static List<CraftStockItemSO> CreateStockItems(bool select)
    {
        CraftRules rules = CraftDatabase.Instance.Rules;
        var specs = new List<(CraftStockKind kind, string id)>();
        foreach (var m in CraftDefaults.StockMetals()) specs.Add((CraftStockKind.Metal, m.name));
        foreach (CraftOreDefinition o in rules.Ores) specs.Add((CraftStockKind.Ore, o.id));
        foreach (CraftHideDefinition h in rules.Hides) specs.Add((CraftStockKind.Hide, h.id));
        foreach (CraftWoodDefinition w in rules.Woods) specs.Add((CraftStockKind.Wood, w.id));
        foreach (CraftFuel f in rules.Fuels) specs.Add((CraftStockKind.Fuel, f.id));
        foreach (CraftSupplyDefinition s in rules.Supplies) specs.Add((CraftStockKind.Supply, s.id));
        foreach (CraftTanningAgent a in rules.Agents)
            if (!string.IsNullOrEmpty(a.item)) specs.Add((CraftStockKind.Agent, a.id));
        foreach (CraftFastenerKind k in Enum.GetValues(typeof(CraftFastenerKind))) specs.Add((CraftStockKind.Fasteners, k.ToString()));

        var made = new List<CraftStockItemSO>();
        foreach ((CraftStockKind kind, string id) in specs)
        {
            CraftStockItemSO runtime = CraftItems.Stock(rules, kind, id);
            if (runtime == null) continue;
            string file = SMAssetsEditor.CleanFileName(runtime.Name);
            CraftStockItemSO item = SMAssetsEditor.FindOrCreate<CraftStockItemSO>(SMAssets.CraftingStock, file, a =>
            {
                EditorUtility.CopySerialized(runtime, a);
                a.hideFlags = HideFlags.None;
                a.name = file;
                // The runtime item's model is a generated object, not an asset: an asset keeps no model until you give it one.
                var so = new SerializedObject(a);
                SerializedProperty model = so.FindProperty("prefab");
                if (model != null) model.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }, false);
            if (item.Icon == null || AssetDatabase.GetAssetPath(item.Icon) == "")
                item.SetArt(SaveIcon(file, runtime.Icon), null);
            EditorUtility.SetDirty(item);
            made.Add(item);
        }
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return made;
    }

    /// <summary>Saves a generated sprite as a PNG sprite asset (Items/Crafting/Icons) and returns it.</summary>
    public static Sprite SaveIcon(string name, Sprite generated)
    {
        if (generated == null || generated.texture == null)
            return null;
        string folder = SMAssetsEditor.Folder(SMAssets.CraftingIcons);
        string path = $"{folder}/{SMAssetsEditor.CleanFileName(name)}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;
        File.WriteAllBytes(path, generated.texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ------------------------------------------------------------------ design templates
    /// <summary>The weapon template a design's weapons copy (attacks, chains, hit shapes).</summary>
    public static WeaponTemplates.Template TemplateFor(CraftDesign d)
    {
        switch (d.weaponCategory)
        {
            case WeaponCategory.Dagger: return WeaponTemplates.Template.Dagger;
            case WeaponCategory.Greatsword: return WeaponTemplates.Template.Greatsword;
            case WeaponCategory.Spear: return WeaponTemplates.Template.Spear;
            case WeaponCategory.Axe: return WeaponTemplates.Template.Axe;
            case WeaponCategory.Mace:
            case WeaponCategory.Hammer: return WeaponTemplates.Template.Hammer;
            case WeaponCategory.Tool: return d.toolType == ToolType.Axe ? WeaponTemplates.Template.Axe : WeaponTemplates.Template.Hammer;
            default: return WeaponTemplates.Template.Sword;
        }
    }

    /// <summary>
    /// A template asset per design (Items/Crafting/Templates): a weapon with the moveset of its kind, or an armour piece
    /// of its slot; assigned in <paramref name="db"/>. Templates already assigned are kept. Returns how many were made.
    /// </summary>
    public static int CreateDesignTemplates(CraftDatabase db, bool select)
    {
        if (db == null)
            return 0;
        CraftRules rules = db.Rules;
        Undo.RecordObject(db, "Create Design Templates");
        var made = new List<Object>();
        foreach (CraftDesign d in rules.Designs)
        {
            if (rules.TemplateOf(d) != null) continue;
            string file = SMAssetsEditor.CleanFileName(d.Name + " Template");
            ItemSO item;
            if (d.IsWeaponLike)
            {
                WeaponSO w = SMAssetsEditor.FindOrCreate<WeaponSO>(SMAssets.CraftingTemplates, file, null, false);
                WeaponTemplates.Apply(w, TemplateFor(d), false);
                w.SetStats(d.weaponCategory, d.damageMin, d.damageMax, d.critChance, d.critMultiplier, d.knockback, d.scaling);
                item = w;
            }
            else
            {
                ArmorSO a = SMAssetsEditor.FindOrCreate<ArmorSO>(SMAssets.CraftingTemplates, file, null, false);
                a.SetArmorStats(d.armorSlot, d.defense, d.magicDefense, null);
                item = a;
            }
            item.SetGridSize(d.gridSize);
            EditorUtility.SetDirty(item);
            db.SetTemplate(d.id, item);
            made.Add(item);
        }
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return made.Count;
    }
}

[CustomEditor(typeof(CraftDatabase))]
public class CraftDatabaseEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private bool showWarnings;

    public override void OnInspectorGUI()
    {
        var db = (CraftDatabase)target;
        errors.Clear();
        warnings.Clear();
        db.Validate(errors, warnings);
        CraftRules rules = db.Rules;
        EditorGUILayout.HelpBox($"{rules.Elements.Length} elements · {rules.Classes.Length} metal classes · {rules.Ores.Length} ores · {rules.Hides.Length} hides · " +
                                $"{rules.Woods.Length} woods · {rules.Designs.Length} designs{(db.extensions != null && db.extensions.Count > 0 ? $" (with {db.extensions.Count} extension(s))" : "")}.\n" +
                                "What a metal is follows from what it is made of (its classes); what an item is worth follows from how it was made.", MessageType.None);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        if (warnings.Count > 0)
        {
            showWarnings = EditorGUILayout.Foldout(showWarnings, $"{warnings.Count} warning(s)", true);
            if (showWarnings)
                foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Create Design Templates", "A weapon or armour asset per design: the attacks and models crafted items copy.")))
                CraftingEditorTools.CreateDesignTemplates(db, true);
            if (GUILayout.Button(new GUIContent("Create Stock Items", "Item assets for the stock metals, ores, hides, woods, fuels, supplies and agents.")))
                CraftingEditorTools.CreateStockItems(true);
            if (GUILayout.Button(new GUIContent("Reset To Built-in", "Replace the content with the built-in metals, ores, hides and designs.")) &&
                EditorUtility.DisplayDialog("Crafting", "Replace every element, class, ore, hide, wood, supply and design with the built-in content?", "Replace", "Cancel"))
            {
                Undo.RecordObject(db, "Reset Crafting Database");
                CraftDefaults.Fill(db);
                EditorUtility.SetDirty(db);
            }
        }
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            db.MarkChanged();
    }
}

[CustomEditor(typeof(CraftStation))]
public class CraftStationEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public override void OnInspectorGUI()
    {
        var s = (CraftStation)target;
        errors.Clear();
        warnings.Clear();
        s.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string warning in warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        if (SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetCraftStation") is Type net && s.GetComponent(net) == null)
        {
            EditorGUILayout.HelpBox("Multiplayer: the station has no NetCraftStation (and NetworkObject); in a network session only the host's player could work it.", MessageType.Info);
            if (GUILayout.Button("Make Networked"))
                CraftingEditorTools.MakeNetworked(s.gameObject);
        }
        DrawDefaultInspector();
        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
        CraftWorkshop w = s.Workshop;
        if (w == null)
        {
            EditorGUILayout.LabelField("No workshop.");
            Repaint();
            return;
        }
        CraftRules rules = s.Rules;
        EditorGUILayout.LabelField("Time", $"{CraftText.Clock(w.Elapsed)} (step {w.Step}){(w.IsSuspended ? " · paused" : "")} · ambient {w.Ambient:0} °C");
        if (s.Has(CraftTools.Forge))
            EditorGUILayout.LabelField("Forge", $"{w.Forge.temperature:0} °C · fuel {w.Forge.fuel:0.0} · level {w.Forge.level:0.00} · air {w.Forge.air:0.00} · vent {w.Forge.damper:0.00}");
        if (s.Has(CraftTools.Furnace))
            EditorGUILayout.LabelField("Furnace", $"{w.Furnace.fire.temperature:0} °C · slag {w.Furnace.slag:0} g · bloom {w.Furnace.BloomMass:0} g · pool {w.Furnace.PoolMass:0} g");
        if (s.Has(CraftTools.Crucible))
            EditorGUILayout.LabelField("Crucible", $"{w.Crucible.temperature:0} °C · {w.Crucible.Mass:0} g · dross {w.Crucible.dross:0.0} · cover {w.Crucible.cover:0.00}");
        foreach (CraftPiece p in w.Pieces)
        {
            CraftPieceReport r = CraftEvaluator.Piece(rules, p);
            string what = p.kind == CraftPieceKind.Metal ? $"{p.Profile(rules).ClassName} · {p.MaxTemperature:0} °C · {r.BodyHardness:0}/{r.EdgeHardness:0} HV · {r.Mass:0} g"
                : p.kind == CraftPieceKind.Hide ? $"tannage {r.Tannage:0.00} · moisture {r.Moisture:0.00} · stiffness {r.Stiffness:0.00}"
                : p.kind == CraftPieceKind.Ore ? $"{p.ore.fragments.Count} fragments · {p.ore.Mass:0} g"
                : p.kind == CraftPieceKind.Assembly ? (p.assembly.finished ? "finished" : "being assembled") : p.kind.ToString();
            EditorGUILayout.LabelField($"  {p.id} ({p.location})", what);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(w.IsSuspended ? "Resume" : "Pause"))
            {
                if (w.IsSuspended) w.Resume();
                else w.Suspend();
            }
            if (GUILayout.Button(new GUIContent("Copy State JSON", "The whole workshop as JSON (bug reports, tests).")))
                EditorGUIUtility.systemCopyBuffer = w.Capture().ToJson(true);
            if (GUILayout.Button("Clear"))
                s.ClearWorkshop();
        }
        Repaint();
    }
}
#endif
