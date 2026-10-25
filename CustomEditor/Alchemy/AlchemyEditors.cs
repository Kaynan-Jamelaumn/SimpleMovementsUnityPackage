#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menus of the alchemy system: the project's Alchemy Database, a ready station in the scene, the vessel presets, the
/// ingredient and potion item assets (with generated icons and models), the Alchemy Lab and the documentation.
/// </summary>
public static class AlchemyEditorTools
{
    private const string Menu = SMMenu.Root + "Alchemy/";

    [MenuItem(SMMenu.Root + "Project Setup/Create Alchemy Database (Resources)", false, SMMenu.ProjectSetup + 8)]
    public static AlchemyDatabase CreateDatabaseMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Alchemy Station", false, SMMenu.Alchemy)]
    private static void CreateStationMenu() => CreateStation(false);

    [MenuItem(Menu + "Create Test Bench (free ingredients)", false, SMMenu.Alchemy + 1)]
    private static void CreateBenchMenu() => CreateStation(true);

    [MenuItem(Menu + "Create Equipment Presets", false, SMMenu.Alchemy + 20)]
    private static void CreateEquipmentMenu() => CreateEquipmentPresets(true);

    [MenuItem(Menu + "Create Ingredient Items", false, SMMenu.Alchemy + 21)]
    private static void CreateIngredientsMenu()
    {
        List<AlchemyIngredientSO> made = CreateIngredientItems(true);
        EditorUtility.DisplayDialog("Alchemy", $"{made.Count} ingredient item(s) in {SMAssets.FolderPath(SMAssets.AlchemyIngredients)}. " +
            "Add them to loot tables, merchants or starting items.", "OK");
    }

    [MenuItem(Menu + "Create Potion Items", false, SMMenu.Alchemy + 22)]
    private static void CreatePotionsMenu()
    {
        AlchemyDatabase db = ProjectDatabase(false);
        int n = CreatePotionItems(db, true);
        EditorUtility.DisplayDialog("Alchemy", $"{n} potion item(s) created or updated in {SMAssets.FolderPath(SMAssets.AlchemyPotions)} and " +
            "assigned to the outcomes' tiers of the Alchemy Database.", "OK");
    }

    [MenuItem(Menu + "Open Alchemy Lab", false, SMMenu.Alchemy + 40)]
    private static void LabMenu() => AlchemyLabWindow.Open();

    [MenuItem(Menu + "Validate Alchemy Database", false, SMMenu.Alchemy + 41)]
    private static void ValidateMenu()
    {
        AlchemyDatabase db = AlchemyDatabase.Instance;
        var errors = new List<string>();
        var warnings = new List<string>();
        db.MarkChanged();
        db.Validate(errors, warnings);
        foreach (string e in errors) Debug.LogError("[Alchemy] " + e, db);
        foreach (string w in warnings) Debug.LogWarning("[Alchemy] " + w, db);
        Debug.Log($"[Alchemy] {db.name}: {errors.Count} error(s), {warnings.Count} warning(s).", db);
    }

    [MenuItem(Menu + "Help", false, SMMenu.Alchemy + 60)]
    private static void HelpMenu()
    {
        string[] guids = AssetDatabase.FindAssets("01-Overview-and-Setup");
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("Alchemy"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }
        }
        EditorUtility.DisplayDialog("Alchemy", "See Documentation/Alchemy/README.md.", "OK");
    }

    // ------------------------------------------------------------------ the database
    /// <summary>The project's Alchemy Database (Resources/SimpleMovements/Settings), made from the built-in alchemy when missing.</summary>
    public static AlchemyDatabase ProjectDatabase(bool select)
    {
        AlchemyDatabase db = SMAssetsEditor.FindOrCreate<AlchemyDatabase>(SMAssets.Settings, AlchemyDatabase.ResourcesName, d => AlchemyDefaults.Fill(d), select);
        AlchemyDatabase.Override(null);
        return db;
    }

    // ------------------------------------------------------------------ the station
    /// <summary>
    /// A cauldron on a fire in the open scene at the Scene view's pivot: the station, a liquid surface, a fire light,
    /// bubbles, steam, smoke and sparks wired to <see cref="AlchemyStationVisuals"/>. <paramref name="bench"/> makes it a
    /// test bench with every built-in ingredient free.
    /// </summary>
    public static AlchemyStation CreateStation(bool bench)
    {
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var root = new GameObject(bench ? "Alchemy Test Bench" : "Alchemy Station");
        Undo.RegisterCreatedObjectUndo(root, "Create Alchemy Station");
        root.transform.position = at;

        Color iron = new Color(0.18f, 0.18f, 0.2f), stone = new Color(0.4f, 0.38f, 0.36f);
        GameObject body = Shape(PrimitiveType.Cylinder, "Cauldron", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1f, 0.4f, 1f), iron);
        Shape(PrimitiveType.Cylinder, "Fire Pit", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(1.3f, 0.06f, 1.3f), stone);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            GameObject leg = Shape(PrimitiveType.Cube, "Leg", root.transform, new Vector3(Mathf.Cos(a) * 0.42f, 0.2f, Mathf.Sin(a) * 0.42f), new Vector3(0.06f, 0.4f, 0.06f), iron);
            Object.DestroyImmediate(leg.GetComponent<Collider>());
        }
        GameObject liquid = Shape(PrimitiveType.Cylinder, "Liquid", body.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.02f, 0.9f), new Color(0.5f, 0.6f, 0.7f));
        Object.DestroyImmediate(liquid.GetComponent<Collider>());

        var fire = new GameObject("Fire Light");
        fire.transform.SetParent(root.transform, false);
        fire.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        Light fl = fire.AddComponent<Light>();
        fl.type = LightType.Point;
        fl.color = new Color(1f, 0.55f, 0.2f);
        fl.range = 4f;
        fl.intensity = 0f;
        var glow = new GameObject("Glow Light");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        Light gl = glow.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.range = 3f;
        gl.intensity = 0f;

        ParticleSystem bubbles = Particles("Bubbles", root.transform, new Vector3(0f, 0.75f, 0f), 0.35f, 0.4f, 0.06f, new Color(1f, 1f, 1f, 0.6f), 0.4f, false);
        ParticleSystem steam = Particles("Steam", root.transform, new Vector3(0f, 0.8f, 0f), 0.3f, 3f, 0.35f, new Color(1f, 1f, 1f, 0.25f), 0.7f, false);
        ParticleSystem smoke = Particles("Smoke", root.transform, new Vector3(0f, 0.85f, 0f), 0.35f, 3f, 0.5f, new Color(0.2f, 0.2f, 0.2f, 0.6f), 1f, true);
        ParticleSystem sparks = Particles("Sparks", root.transform, new Vector3(0f, 0.8f, 0f), 0.2f, 0.8f, 0.04f, new Color(1f, 0.8f, 0.3f, 1f), 3f, true);

        var station = root.AddComponent<AlchemyStation>();
        var so = new SerializedObject(station);
        so.FindProperty("freeDefaultIngredients").boolValue = bench;
        so.FindProperty("stationName").stringValue = bench ? "Alchemy Test Bench" : "";
        so.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<AlchemySurroundings>();

        var visuals = root.AddComponent<AlchemyStationVisuals>();
        var vso = new SerializedObject(visuals);
        vso.FindProperty("station").objectReferenceValue = station;
        vso.FindProperty("liquid").objectReferenceValue = liquid.GetComponent<Renderer>();
        vso.FindProperty("emptyHeight").floatValue = -0.8f;
        vso.FindProperty("fullHeight").floatValue = 0.85f;
        vso.FindProperty("bubbles").objectReferenceValue = bubbles;
        vso.FindProperty("steam").objectReferenceValue = steam;
        vso.FindProperty("smoke").objectReferenceValue = smoke;
        vso.FindProperty("sparks").objectReferenceValue = sparks;
        vso.FindProperty("fireLight").objectReferenceValue = fl;
        vso.FindProperty("glowLight").objectReferenceValue = gl;
        vso.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log(bench
            ? "[Alchemy] Created a test bench: every built-in ingredient is free there. Walk up to it and press Interact."
            : "[Alchemy] Created an alchemy station. Give the player ingredients (Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Ingredient Items) and press Interact at it.", root);
        return station;
    }

    private static GameObject Shape(PrimitiveType type, string n, Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = n;
        go.transform.SetParent(parent, false);
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

    private static ParticleSystem Particles(string n, Transform parent, Vector3 localPos, float radius, float lifetime, float size, Color color, float speed, bool burstsOnly)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = lifetime;
        main.startSize = size;
        main.startSpeed = speed;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = !burstsOnly;
        ParticleSystem.EmissionModule e = ps.emission;
        e.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        var r = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (r != null && shader != null)
            r.sharedMaterial = new Material(shader) { color = Color.white };
        return ps;
    }

    // ------------------------------------------------------------------ equipment
    /// <summary>The built-in vessels as assets in Resources/SimpleMovements/Alchemy/Equipment.</summary>
    public static List<AlchemyEquipment> CreateEquipmentPresets(bool select)
    {
        var list = new List<AlchemyEquipment>();
        foreach (AlchemyEquipmentProfile p in AlchemyDefaults.Equipment())
        {
            AlchemyEquipmentProfile profile = p;
            AlchemyEquipment e = SMAssetsEditor.FindOrCreate<AlchemyEquipment>(SMAssets.AlchemyEquipment, profile.displayName, a => a.SetProfile(profile), false);
            list.Add(e);
        }
        AssetDatabase.SaveAssets();
        if (select && list.Count > 0)
        {
            Selection.objects = list.ToArray();
            EditorGUIUtility.PingObject(list[0]);
        }
        return list;
    }

    // ------------------------------------------------------------------ ingredients
    /// <summary>
    /// Ingredient item assets for the built-in presets (Spring Water, Sunpetal, Frostmint...) with generated icons
    /// and models, in Resources/SimpleMovements/Items/Alchemy/Ingredients. Existing ones are kept (their profile is not
    /// overwritten). Substances that share a preset's name (Moon Crystal, Fire Ash), or that a preset is made of (brine:
    /// Rock Salt), get it as Harvest Item - what settles out of a mixture is collected as that item.
    /// </summary>
    public static List<AlchemyIngredientSO> CreateIngredientItems(bool select)
    {
        var made = new List<AlchemyIngredientSO>();
        foreach (AlchemyDefaults.IngredientPreset p in AlchemyDefaults.Ingredients())
        {
            AlchemyDefaults.IngredientPreset preset = p;
            AlchemyArt.Shape shape = AlchemyArt.ShapeOf(preset.kind);
            AlchemyIngredientSO item = SMAssetsEditor.FindOrCreate<AlchemyIngredientSO>(SMAssets.AlchemyIngredients, preset.name, a =>
                a.Setup(preset.name, preset.description, preset.kind, preset.price, preset.weight, preset.profile, null, null), false);
            EnsureArt(item, preset.name, shape, preset.color);
            made.Add(item);
        }
        // Ingredients made by hand (Create ▸ SimpleMovements ▸ Alchemy ▸ Ingredient): an icon and a pickable prefab too,
        // in the colour of what they are mainly made of.
        AlchemyRules artRules = AlchemyDatabase.Instance.Rules;
        foreach (string guid in AssetDatabase.FindAssets("t:AlchemyIngredientSO"))
        {
            var own = AssetDatabase.LoadAssetAtPath<AlchemyIngredientSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (own == null || made.Contains(own))
                continue;
            EnsureArt(own, own.Name, AlchemyArt.ShapeOf(own.Kind), MainColor(artRules, own.AlchemyProfile));
        }
        AlchemyDatabase db = SMAssetsEditor.Find<AlchemyDatabase>(SMAssets.Settings, AlchemyDatabase.ResourcesName);
        if (db != null)
        {
            Undo.RecordObject(db, "Assign Harvest Items");
            foreach (AlchemySubstance s in db.substances)
            {
                if (s == null || s.harvestItem != null) continue;
                // Same name (Moon Crystal), else the ingredient it mainly comes from (settled brine is Rock Salt).
                AlchemyDefaults.IngredientPreset source = AlchemyDefaults.MainSourceOf(s.id);
                AlchemyIngredientSO match = made.Find(i => i.Name == s.Name) ?? (source != null ? made.Find(i => i.Name == source.name) : null);
                if (match != null) s.harvestItem = match;
            }
            db.MarkChanged();
            EditorUtility.SetDirty(db);
        }
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return made;
    }

    // ------------------------------------------------------------------ potions
    /// <summary>
    /// A <see cref="BrewedPotionSO"/> asset for every tier of every outcome that bottles items, assigned to the tier
    /// (existing tier items are updated only when they are brewed potions of that tier). Returns how many were made or updated.
    /// </summary>
    public static int CreatePotionItems(AlchemyDatabase db, bool select)
    {
        if (db == null)
            return 0;
        AlchemyRules rules = db.Rules;
        int count = 0;
        Undo.RecordObject(db, "Create Potion Items");
        var made = new List<Object>();
        foreach (AlchemyOutcome o in db.outcomes)
        {
            if (o == null || !o.producesItems || o.tiers == null) continue;
            Color color = AlchemyItems.Typical(rules, o, new Color(0.55f, 0.55f, 0.6f));
            for (int t = 0; t < o.tiers.Count; t++)
            {
                AlchemyOutcomeTier tier = o.tiers[t];
                if (tier == null) continue;
                if (tier.item != null && !(tier.item is BrewedPotionSO))
                    continue; // a hand-made item: leave it
                if (tier.item is BrewedPotionSO other && (other.OutcomeId != o.id || other.Tier != t))
                    tier.item = null; // a generated potion of another result (an older tool run shared one): make its own
                string fileName = SMAssetsEditor.CleanFileName(o.TierName(t));
                int tierIndex = t;
                Sprite icon = SaveIcon(fileName, AlchemyArt.Icon(AlchemyArt.Shape.Vial, color));
                GameObject model = SaveModel("Vial " + ColorUtility.ToHtmlStringRGB(color), AlchemyArt.Shape.Vial, color);
                BrewedPotionSO item = tier.item as BrewedPotionSO;
                if (item == null)
                    item = SMAssetsEditor.FindOrCreate<BrewedPotionSO>(SMAssets.AlchemyPotions, fileName, null, false);
                item.Setup(o, tierIndex, rules, color, icon, SavePickable(item, model));
                EditorUtility.SetDirty(item);
                tier.item = item;
                made.Add(item);
                count++;
            }
        }
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return count;
    }

    // ------------------------------------------------------------------ generated art as assets
    /// <summary>
    /// Gives <paramref name="item"/> a generated icon when it has none, and a pickable prefab (its model with a collider
    /// and an Item Pickable) when it has no prefab or only a generated model: placed in a scene, it can be picked up.
    /// </summary>
    private static void EnsureArt(AlchemyIngredientSO item, string fileName, AlchemyArt.Shape shape, Color color)
    {
        Sprite icon = item.Icon == null ? SaveIcon(fileName, AlchemyArt.Icon(shape, color)) : null;
        GameObject prefab = null;
        if (item.Prefab == null)
            prefab = SavePickable(item, SaveModel(fileName, shape, color));
        else if (item.Prefab.GetComponent<ItemPickable>() == null && IsGeneratedModel(item.Prefab))
            prefab = SavePickable(item, item.Prefab);
        if (icon == null && prefab == null)
            return;
        item.SetArt(icon, prefab);
        EditorUtility.SetDirty(item);
    }

    private static bool IsGeneratedModel(GameObject go) =>
        AssetDatabase.GetAssetPath(go).StartsWith(SMAssetsEditor.Folder(SMAssets.AlchemyModels) + "/", System.StringComparison.Ordinal);

    /// <summary>The colour of what an ingredient is mostly made of (grey when unknown).</summary>
    private static Color MainColor(AlchemyRules rules, AlchemyIngredientProfile profile)
    {
        IngredientRelease main = null;
        if (profile != null && profile.releases != null)
            foreach (IngredientRelease r in profile.releases)
                if (r != null && (main == null || r.amount > main.amount)) main = r;
        AlchemySubstance s = main != null && rules != null ? rules.Substance(main.substance) : null;
        return s != null ? s.color : new Color(0.6f, 0.6f, 0.6f);
    }

    /// <summary>
    /// A prefab for one item (Items/Alchemy/Pickables): <paramref name="model"/> with a collider and an Item Pickable
    /// giving that item - what Item ▸ Prefab should be so a copy placed in a scene can be picked up (dropped items get
    /// the same at runtime). Kept and re-pointed when it exists.
    /// </summary>
    public static GameObject SavePickable(ItemSO item, GameObject model)
    {
        if (item == null || model == null)
            return model;
        string folder = SMAssetsEditor.Folder(SMAssets.AlchemyItems + "/Pickables");
        string path = $"{folder}/{SMAssetsEditor.CleanFileName(item.name)}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            ItemPickable p = existing.GetComponent<ItemPickable>();
            if (p != null && p.itemScriptableObject != item)
            {
                p.itemScriptableObject = item;
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }
        GameObject copy = Object.Instantiate(model);
        copy.name = item.name;
        copy.SetActive(true);
        copy.hideFlags = HideFlags.None;
        if (copy.GetComponentInChildren<Collider>(true) == null)
        {
            var box = copy.AddComponent<BoxCollider>();
            Renderer[] renderers = copy.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                box.center = copy.transform.InverseTransformPoint(b.center);
                Vector3 scale = copy.transform.lossyScale;
                box.size = new Vector3(b.size.x / Mathf.Max(1e-4f, scale.x), b.size.y / Mathf.Max(1e-4f, scale.y), b.size.z / Mathf.Max(1e-4f, scale.z));
            }
        }
        ItemPickable pick = copy.GetComponent<ItemPickable>();
        if (pick == null)
            pick = copy.AddComponent<ItemPickable>();
        pick.itemScriptableObject = item;
        pick.quantity = 1;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        Object.DestroyImmediate(copy);
        return prefab;
    }

    /// <summary>Saves a generated sprite as a PNG sprite asset (Items/Alchemy/Icons) and returns it.</summary>
    public static Sprite SaveIcon(string name, Sprite generated)
    {
        if (generated == null || generated.texture == null)
            return null;
        string folder = SMAssetsEditor.Folder(SMAssets.AlchemyIcons);
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

    /// <summary>Saves a small model (vial, leaf, powder, crystal, flask) as a prefab (Items/Alchemy/Models) and returns it.</summary>
    public static GameObject SaveModel(string name, AlchemyArt.Shape shape, Color color)
    {
        string folder = SMAssetsEditor.Folder(SMAssets.AlchemyModels);
        string path = $"{folder}/{SMAssetsEditor.CleanFileName(name)}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;
        GameObject template = AlchemyArt.Template(shape, color);
        GameObject copy = Object.Instantiate(template);
        copy.SetActive(true);
        copy.name = name;
        copy.hideFlags = HideFlags.None;
        Renderer r = copy.GetComponent<Renderer>();
        if (r != null)
        {
            string matPath = $"{folder}/{SMAssetsEditor.CleanFileName(name)}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = NewMaterial(color);
                if (mat != null) AssetDatabase.CreateAsset(mat, matPath);
            }
            if (mat != null) r.sharedMaterial = mat;
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        Object.DestroyImmediate(copy);
        return prefab;
    }
}

[CustomEditor(typeof(AlchemyDatabase))]
public class AlchemyDatabaseEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private bool showWarnings;

    public override void OnInspectorGUI()
    {
        var db = (AlchemyDatabase)target;
        errors.Clear();
        warnings.Clear();
        db.Validate(errors, warnings);
        AlchemyRules rules = db.Rules;
        EditorGUILayout.HelpBox($"{rules.Properties.Length} properties · {rules.SubstanceCount} substances · {rules.Reactions.Length} reactions · " +
                                $"{rules.Outcomes.Length} outcomes{(db.extensions != null && db.extensions.Count > 0 ? $" (with {db.extensions.Count} extension(s))" : "")}.\n" +
                                "Outcomes classify the state of a mixture; reactions change it. No recipe lists the ingredients of a potion.", MessageType.None);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        if (warnings.Count > 0)
        {
            showWarnings = EditorGUILayout.Foldout(showWarnings, $"{warnings.Count} warning(s)", true);
            if (showWarnings)
                foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Open Alchemy Lab", "Simulate preparations and map outcomes over heat and time.")))
                AlchemyLabWindow.Open(db);
            if (GUILayout.Button(new GUIContent("Create Potion Items", "An item asset for every tier of every outcome (needed for network sessions and saves).")))
                AlchemyEditorTools.CreatePotionItems(db, true);
            if (GUILayout.Button(new GUIContent("Reset To Built-in", "Replace the content with the built-in alchemy.")) &&
                EditorUtility.DisplayDialog("Alchemy", "Replace every property, substance, reaction and outcome with the built-in alchemy?", "Replace", "Cancel"))
            {
                Undo.RecordObject(db, "Reset Alchemy Database");
                AlchemyDefaults.Fill(db);
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

[CustomEditor(typeof(AlchemyStation))]
public class AlchemyStationEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public override void OnInspectorGUI()
    {
        var s = (AlchemyStation)target;
        errors.Clear();
        warnings.Clear();
        s.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        DrawDefaultInspector();
        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
        AlchemyBrew brew = s.Brew;
        if (brew == null)
        {
            EditorGUILayout.LabelField("Empty.");
            Repaint();
            return;
        }
        AlchemyRules rules = brew.Rules;
        AlchemyMixture m = brew.Mixture;
        EditorGUILayout.LabelField("Time", $"{AlchemyText.Clock(m.Elapsed)} (step {m.Step}){(brew.IsSuspended ? " · paused" : "")}");
        EditorGUILayout.LabelField("Temperature", $"{m.Temperature:0.0} °C (heat {m.Heat:0.00}, ambient {m.Ambient:0} °C){(brew.IsBoiling ? " · boiling" : "")}");
        EditorGUILayout.LabelField("Volume", $"{m.FilledVolume:0.0} / {m.Equipment.capacity:0} u · agitation {m.Agitation:0.00}{(m.VaporMode == VaporHandling.Sealed ? $" · pressure {m.Pressure:0.00}" : "")}");
        if (m.FireManual)
            EditorGUILayout.LabelField("Fire", $"flames {m.FireLevel:0.00} · fuel {m.Fuel:0.00} · bellows air {m.Air:0.00} · vent {m.Damper:0.00}");
        EditorGUILayout.LabelField("Stirring", $"speed {m.StirSpeed:0.00} · turbulence {m.Turbulence:0.00} · aeration {m.Aeration:0.00}{(m.IsLayered ? $" · layered, emulsion {m.Emulsion:0.00}" : "")}");
        EditorGUILayout.LabelField("Conditions", $"pH {m.Ph:0.0} · swing ±{m.Swing:0.0} °C · cooling {m.CoolingRate:0.00} °C/s · bath {m.Bath}");
        for (int i = 0; i < rules.SubstanceCount; i++)
            if (m.Amount(i) > 0.01 || m.UndissolvedAmount(i) > 0.01)
                EditorGUILayout.LabelField("  " + rules.Substances[i].Name,
                    $"{m.Amount(i):0.00} u{(m.UndissolvedAmount(i) > 0.01 ? $" + {m.UndissolvedAmount(i):0.00} u undissolved (holds {m.Capacity(i):0.00})" : "")}");
        foreach (PendingIngredient p in m.Pending)
            EditorGUILayout.LabelField("  ◌ " + p.Name, $"{p.Form} · {p.Progress * 100:0}% given up");
        for (int j = 0; j < rules.Reactions.Length; j++)
            if (brew.IsReacting(j))
                EditorGUILayout.LabelField("  ⟳ " + rules.Reactions[j].Definition.Name, $"{brew.RateOf(j):0.000} u/s");
        AlchemyResult r = brew.Evaluate();
        EditorGUILayout.LabelField("If bottled now", $"{r.DisplayName} · quality {r.Quality:0.00} · {r.Bottles} bottle(s)");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Stir")) brew.Stir();
            if (GUILayout.Button(brew.IsSuspended ? "Resume" : "Pause"))
            {
                if (brew.IsSuspended) brew.Resume();
                else brew.Suspend();
            }
            if (GUILayout.Button(new GUIContent("Copy State JSON", "The whole preparation as JSON (bug reports, the Alchemy Lab).")))
                EditorGUIUtility.systemCopyBuffer = brew.Capture().ToJson(true);
            if (GUILayout.Button(new GUIContent("Open In Lab", "Replays this preparation's history in the Alchemy Lab.")))
                AlchemyLabWindow.Open(s.Database, brew);
        }
        Repaint();
    }
}
#endif
