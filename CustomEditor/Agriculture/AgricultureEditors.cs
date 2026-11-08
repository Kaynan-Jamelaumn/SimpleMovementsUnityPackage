#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SimpleMovements.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Menus of the agriculture system (Tools ▸ SimpleMovements ▸ Agriculture): soil fields (in the city, or fitted over a
/// selected piece of ground), water sources and growing structures, the project's Agriculture Database, editable species
/// and soil material assets, item assets with generated icons and models, validation and the documentation.
/// </summary>
public static class AgricultureEditors
{
    private const string Menu = SMMenu.Root + "Agriculture/";
    private const string Water = Menu + "Create Water Source/";
    private const string Cover = Menu + "Create Growing Structure/";

    [MenuItem(SMMenu.Root + "Project Setup/Create Agriculture Database (Resources)", false, SMMenu.ProjectSetup + 11)]
    private static void CreateDatabaseSetupMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Soil Field", false, SMMenu.Agriculture)]
    private static void CreateFieldMenu() => CreateField(null);

    [MenuItem(Menu + "Create Soil Field Over Selected Object", false, SMMenu.Agriculture + 1)]
    private static void CreateFieldOverMenu() => CreateField(Selection.activeGameObject);

    [MenuItem(Menu + "Create Soil Field Over Selected Object", true)]
    private static bool CreateFieldOverValidate() => Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);

    [MenuItem(Water + "Well", false, SMMenu.Agriculture + 2)] private static void WellMenu() => CreateWaterSource(WaterSourceKind.Well);
    [MenuItem(Water + "Rain Barrel", false, SMMenu.Agriculture + 3)] private static void BarrelMenu() => CreateWaterSource(WaterSourceKind.Rain);
    [MenuItem(Water + "River Bank", false, SMMenu.Agriculture + 4)] private static void RiverMenu() => CreateWaterSource(WaterSourceKind.River);
    [MenuItem(Water + "Lake Shore", false, SMMenu.Agriculture + 5)] private static void LakeMenu() => CreateWaterSource(WaterSourceKind.Lake);
    [MenuItem(Water + "Spring", false, SMMenu.Agriculture + 6)] private static void SpringMenu() => CreateWaterSource(WaterSourceKind.Spring);
    [MenuItem(Water + "Swamp Pool", false, SMMenu.Agriculture + 7)] private static void SwampMenu() => CreateWaterSource(WaterSourceKind.Swamp);
    [MenuItem(Water + "Alchemical Font", false, SMMenu.Agriculture + 8)] private static void FontMenu() => CreateWaterSource(WaterSourceKind.Alchemical);
    [MenuItem(Water + "Magical Pool", false, SMMenu.Agriculture + 9)] private static void PoolMenu() => CreateWaterSource(WaterSourceKind.Magical);

    [MenuItem(Cover + "Greenhouse", false, SMMenu.Agriculture + 10)] private static void GreenhouseMenu() => CreateStructure(GrowingStructureKind.Greenhouse);
    [MenuItem(Cover + "Shade Shelter", false, SMMenu.Agriculture + 11)] private static void ShadeMenu() => CreateStructure(GrowingStructureKind.ShadeShelter);
    [MenuItem(Cover + "Cold Frame", false, SMMenu.Agriculture + 12)] private static void FrameMenu() => CreateStructure(GrowingStructureKind.ColdFrame);
    [MenuItem(Cover + "Magical Chamber", false, SMMenu.Agriculture + 13)] private static void ChamberMenu() => CreateStructure(GrowingStructureKind.MagicalChamber);

    [MenuItem(Menu + "Create Agriculture Database", false, SMMenu.Agriculture + 20)]
    private static void CreateDatabaseMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Species Assets", false, SMMenu.Agriculture + 21)]
    private static void SpeciesMenu()
    {
        List<PlantSpecies> made = CreateSpeciesAssets(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Agriculture", $"{made.Count} plant species asset(s) in {SMAssets.FolderPath(SMAssets.Plants)}, listed in the Agriculture " +
            "Database. Edit them (or copy one for a new plant): an asset replaces the built-in species with its id. Each has a Trial Plot in its inspector.", "OK");
    }

    [MenuItem(Menu + "Create Soil Material Assets", false, SMMenu.Agriculture + 22)]
    private static void MaterialsMenu()
    {
        List<SoilAmendmentSO> made = CreateMaterialAssets(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Agriculture", $"{made.Count} soil material item(s) (fertilizers, amendments, treatments) in " +
            $"{SMAssets.FolderPath(SMAssets.AgricultureItems)}/Soil Materials, listed in the Agriculture Database. Change what they do to the soil, their " +
            "price and look; a new asset with a new id is a new material.", "OK");
    }

    [MenuItem(Menu + "Create Items", false, SMMenu.Agriculture + 23)]
    private static void ItemsMenu()
    {
        int n = CreateItems(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Agriculture", $"{n} item asset(s) in {SMAssets.FolderPath(SMAssets.AgricultureItems)}, bound in the Agriculture Database: " +
            "seeds and produce of every species, the tools, instruments and water containers. Without them every item is still made at runtime; assets " +
            "let you give them your own icons, models and prices, and use them in recipes, merchants and quests.", "OK");
    }

    [MenuItem(Menu + "Make Selected Field Networked", false, SMMenu.Agriculture + 30)]
    private static void NetworkedMenu()
    {
        var report = new StringBuilder();
        foreach (GameObject go in Selection.gameObjects)
        {
            SoilField f = go.GetComponentInParent<SoilField>();
            if (f != null) report.AppendLine(MakeNetworked(f));
        }
        if (report.Length == 0) report.Append("Select soil fields in the scene first.");
        EditorUtility.DisplayDialog("Agriculture", report.ToString().TrimEnd(), "OK");
    }

    [MenuItem(Menu + "Make Selected Field Networked", true)]
    private static bool NetworkedValidate() => Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<SoilField>() != null;

    /// <summary>
    /// Gives a field what a network session needs: a NetworkObject (it is a scene object, spawned with its scene) and a
    /// NetSoilField (the server decides; clients get a picture of the field). Needs Fish-Net in the project.
    /// </summary>
    public static string MakeNetworked(SoilField field)
    {
        Type nob = SimpleMovements.Setup.SetupUtil.TypeByName("FishNet.Object.NetworkObject");
        Type net = SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetSoilField");
        if (nob == null || net == null) return $"{field.name}: Fish-Net is not in the project (the field works offline as it is).";
        var notes = new List<string>();
        if (field.GetComponentInParent(nob, true) == null)
        {
            Undo.AddComponent(field.gameObject, nob);
            notes.Add("added a NetworkObject");
        }
        if (field.GetComponent(net) == null)
        {
            Undo.AddComponent(field.gameObject, net);
            notes.Add("added a NetSoilField");
        }
        EditorUtility.SetDirty(field.gameObject);
        return notes.Count == 0 ? $"{field.name}: already networked." : $"{field.name}: {string.Join(", ", notes)}.";
    }

    [MenuItem(Menu + "Validate Agriculture", false, SMMenu.Agriculture + 40)]
    private static void ValidateMenu()
    {
        var issues = new List<ValidationIssue>();
        AgricultureValidation.CheckProject(issues);
        foreach (GameObject root in OpenSceneRoots())
            AgricultureValidation.CheckRoot(root, issues);
        foreach (ValidationIssue i in issues) i.Log();
        Debug.Log($"[Agriculture] {AgricultureDatabase.Instance.AllSpecies.Count} species, {Fields().Count} soil field(s) open: " +
                  $"{issues.Count(i => i.severity == IssueSeverity.Error)} error(s), {issues.Count(i => i.severity == IssueSeverity.Warning)} warning(s).");
    }

    [MenuItem(Menu + "Help", false, SMMenu.Agriculture + 60)]
    private static void HelpMenu()
    {
        foreach (string g in AssetDatabase.FindAssets("README"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Replace('\\', '/').Contains("Documentation/Agriculture/"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }
        }
        EditorUtility.DisplayDialog("Agriculture", "See Documentation/Agriculture/README.md.", "OK");
    }

    // ------------------------------------------------------------------ the database
    /// <summary>The project's Agriculture Database (Resources/SimpleMovements/Settings), with the default rules when made.</summary>
    public static AgricultureDatabase ProjectDatabase(bool select)
    {
        AgricultureDatabase db = SMAssetsEditor.FindOrCreate<AgricultureDatabase>(SMAssets.Settings, AgricultureDatabase.ResourcesName,
            d => d.settings = AgriSettings.Defaults(), select);
        AgricultureDatabase.Override(null);
        return db;
    }

    /// <summary>The name of the permanent City scene (Hub World settings), or "".</summary>
    public static string CitySceneName()
    {
        try
        {
            return HubWorld.HubWorldSettings.Instance != null ? HubWorld.HubWorldSettings.Instance.citySceneName ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Calls a (private) method of a component - the debug context menus of a field.</summary>
    public static void InvokeContext(Object target, string method)
    {
        if (target == null) return;
        MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null && m.GetParameters().Length == 0) m.Invoke(target, null);
        else Debug.LogWarning($"Agriculture: {target.GetType().Name} has no {method}().", target);
    }

    public static IEnumerable<GameObject> OpenSceneRoots()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (GameObject root in s.GetRootGameObjects()) yield return root;
        }
    }

    /// <summary>Every soil field of the open scenes (inactive ones too).</summary>
    public static List<SoilField> Fields()
    {
        var list = new List<SoilField>();
        foreach (GameObject root in OpenSceneRoots()) list.AddRange(root.GetComponentsInChildren<SoilField>(true));
        return list;
    }

    /// <summary>Another field of the open scenes with the same id as <paramref name="field"/> (a duplicated object), or null.</summary>
    public static SoilField SameId(SoilField field)
    {
        if (field == null) return null;
        foreach (SoilField f in Fields())
            if (f != field && f.FieldId == field.FieldId) return f;
        return null;
    }

    // ------------------------------------------------------------------ scene objects
    /// <summary>
    /// A soil field: fitted over <paramref name="over"/>'s footprint (a garden bed, a plot of ground), else an 8 × 6 m
    /// plot of organic-rich soil at the Scene view's pivot, on the ground. It goes into the City scene when that is open.
    /// </summary>
    public static SoilField CreateField(GameObject over)
    {
        var go = new GameObject("Soil Field");
        Undo.RegisterCreatedObjectUndo(go, "Create Soil Field");
        SoilField field = Undo.AddComponent<SoilField>(go);
        field.layout = new SoilLayout { seed = UnityEngine.Random.Range(1, int.MaxValue) };
        if (over != null)
        {
            // Over the object (or, when it has nothing to fit to, at its position): in its scene, beside it.
            bool fitted = FitOver(field, over, out string note);
            if (over.scene.IsValid() && go.scene != over.scene) Undo.MoveGameObjectToScene(go, over.scene, "Create Soil Field");
            if (over.transform.parent != null) Undo.SetTransformParent(go.transform, over.transform.parent, "Create Soil Field");
            go.name = $"Soil Field ({over.name})";
            if (fitted) Debug.Log($"[Agriculture] {note} Shape it in the Scene view (the Soil Field inspector: Outline, Sections, Paint) and say what its soil is.", go);
        }
        else
        {
            Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            if (Physics.Raycast(at + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            go.transform.position = at;
            InCity(go);
        }
        Selection.activeGameObject = go;
        return field;
    }

    /// <summary>Moves a new object into the City scene when that is open (and says so when it is not).</summary>
    private static void InCity(GameObject go)
    {
        string city = CitySceneName();
        if (string.IsNullOrEmpty(city)) return;
        Scene scene = SceneManager.GetSceneByName(city);
        if (scene.IsValid() && scene.isLoaded)
        {
            if (go.scene != scene) Undo.MoveGameObjectToScene(go, scene, "Move To The City");
            return;
        }
        Debug.Log($"[Agriculture] '{go.name}' is in '{go.scene.name}', not the City scene ('{city}'). Fields belong in the permanent city: " +
                  "the hub world's other scenes are made again every cycle.", go);
    }

    /// <summary>Lays the field over the top of <paramref name="over"/> (its meshes, else its renderers or colliders), turned with it.</summary>
    private static bool FitOver(SoilField field, GameObject over, out string note)
    {
        note = "";
        var corners = new List<Vector3>();
        foreach (MeshFilter mf in over.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
                corners.Add(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1))));
        }
        if (corners.Count == 0)
        {
            var boxes = new List<Bounds>();
            foreach (Renderer r in over.GetComponentsInChildren<Renderer>()) boxes.Add(r.bounds);
            if (boxes.Count == 0) foreach (Collider c in over.GetComponentsInChildren<Collider>()) boxes.Add(c.bounds);
            foreach (Bounds b in boxes)
                for (int i = 0; i < 8; i++)
                    corners.Add(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1)));
        }
        if (corners.Count == 0)
        {
            Debug.LogWarning($"[Agriculture] '{over.name}' has no mesh, renderer or collider to fit a field over: made a plot at its position.", over);
            field.transform.position = over.transform.position;
            return false;
        }
        Quaternion rot = Quaternion.Euler(0f, over.transform.eulerAngles.y, 0f);
        Quaternion inv = Quaternion.Inverse(rot);
        Vector3 origin = corners[0];
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, top = float.MinValue;
        foreach (Vector3 c in corners)
        {
            Vector3 l = inv * (c - origin);
            minX = Mathf.Min(minX, l.x);
            maxX = Mathf.Max(maxX, l.x);
            minZ = Mathf.Min(minZ, l.z);
            maxZ = Mathf.Max(maxZ, l.z);
            top = Mathf.Max(top, c.y);
        }
        float inset = 0.05f;
        float w = Mathf.Max(0.5f, maxX - minX - inset * 2f), h = Mathf.Max(0.5f, maxZ - minZ - inset * 2f);
        Vector3 centre = origin + rot * new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        centre.y = top;
        field.transform.SetPositionAndRotation(centre, rot);
        field.layout.outline = new List<Vector2> { new Vector2(-w * 0.5f, -h * 0.5f), new Vector2(w * 0.5f, -h * 0.5f), new Vector2(w * 0.5f, h * 0.5f), new Vector2(-w * 0.5f, h * 0.5f) };
        if (w * h > 400f) field.layout.cellSize = Mathf.Max(field.layout.cellSize, 0.25f);
        note = $"Made a {w:0.#} × {h:0.#} m field over '{over.name}'.";
        return true;
    }

    /// <summary>A water source of <paramref name="kind"/> at the Scene view's pivot (next to the selected field, when one is selected).</summary>
    public static WaterSource CreateWaterSource(WaterSourceKind kind)
    {
        string label = kind switch
        {
            WaterSourceKind.Rain => "Rain Barrel",
            WaterSourceKind.River => "River Bank",
            WaterSourceKind.Lake => "Lake Shore",
            WaterSourceKind.Swamp => "Swamp Pool",
            WaterSourceKind.Spring => "Spring",
            WaterSourceKind.Alchemical => "Alchemical Font",
            WaterSourceKind.Magical => "Magical Pool",
            _ => "Well",
        };
        var go = new GameObject(label);
        Undo.RegisterCreatedObjectUndo(go, "Create Water Source");
        go.transform.position = PlaceNearSelection(2.5f);
        WaterSource w = go.AddComponent<WaterSource>();
        w.kind = kind;
        w.displayName = label;
        Color water = kind switch
        {
            WaterSourceKind.Swamp => new Color(0.25f, 0.3f, 0.15f),
            WaterSourceKind.Alchemical => new Color(0.55f, 0.85f, 0.6f),
            WaterSourceKind.Magical => new Color(0.55f, 0.45f, 0.95f),
            WaterSourceKind.Spring => new Color(0.5f, 0.75f, 0.9f),
            _ => new Color(0.2f, 0.38f, 0.55f),
        };
        Material stone = SharedMaterial("Agriculture Stone", new Color(0.55f, 0.55f, 0.52f));
        Material wood = SharedMaterial("Agriculture Wood", new Color(0.45f, 0.3f, 0.17f));
        Material surface = SharedMaterial("Agriculture Water " + kind, water);
        switch (kind)
        {
            case WaterSourceKind.Well:
                Part(go.transform, PrimitiveType.Cylinder, "Wall", new Vector3(0f, 0.4f, 0f), new Vector3(1.3f, 0.4f, 1.3f), stone, true);
                Part(go.transform, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.81f, 0f), new Vector3(1.05f, 0.01f, 1.05f), surface, false);
                Part(go.transform, PrimitiveType.Cube, "Post", new Vector3(-0.6f, 1.2f, 0f), new Vector3(0.1f, 1.6f, 0.1f), wood, false);
                Part(go.transform, PrimitiveType.Cube, "Post", new Vector3(0.6f, 1.2f, 0f), new Vector3(0.1f, 1.6f, 0.1f), wood, false);
                Part(go.transform, PrimitiveType.Cube, "Beam", new Vector3(0f, 1.95f, 0f), new Vector3(1.4f, 0.1f, 0.1f), wood, false);
                break;
            case WaterSourceKind.Rain:
                w.capacity = 160f;
                w.startLitres = 40f;
                w.catchment = 6f;
                Part(go.transform, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.45f, 0f), new Vector3(0.65f, 0.45f, 0.65f), wood, true);
                Part(go.transform, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.86f, 0f), new Vector3(0.55f, 0.01f, 0.55f), surface, false);
                break;
            case WaterSourceKind.Alchemical:
                w.capacity = 60f;
                w.startLitres = 60f;
                Part(go.transform, PrimitiveType.Cylinder, "Basin", new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.45f, 0.9f), stone, true);
                Part(go.transform, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.91f, 0f), new Vector3(0.75f, 0.01f, 0.75f), surface, false);
                break;
            default:
                Part(go.transform, PrimitiveType.Cylinder, "Water", new Vector3(0f, 0.02f, 0f), new Vector3(2.4f, 0.02f, 2.4f), surface, false);
                Part(go.transform, PrimitiveType.Cylinder, "Edge", new Vector3(0f, 0.01f, 0f), new Vector3(2.8f, 0.01f, 2.8f), stone, false);
                break;
        }
        // Walk-through water still needs something the Player Interactor can find: a trigger (Ignore Raycast).
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var t = new GameObject("Water Source Trigger") { layer = 2 };
            t.transform.SetParent(go.transform, false);
            var box = t.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(2.4f, 1.4f, 2.4f);
        }
        InCity(go);
        Selection.activeGameObject = go;
        return w;
    }

    /// <summary>A growing structure over the selected field (else at the Scene view's pivot), with a simple frame to replace with art.</summary>
    public static GrowingStructure CreateStructure(GrowingStructureKind kind)
    {
        string label = kind switch
        {
            GrowingStructureKind.ShadeShelter => "Shade Shelter",
            GrowingStructureKind.ColdFrame => "Cold Frame",
            GrowingStructureKind.MagicalChamber => "Magical Chamber",
            _ => "Greenhouse",
        };
        var go = new GameObject(label);
        Undo.RegisterCreatedObjectUndo(go, "Create Growing Structure");
        GrowingStructure s = go.AddComponent<GrowingStructure>();
        s.ApplyPreset(kind);
        float height = kind == GrowingStructureKind.ColdFrame ? 0.6f : 2.6f;
        Vector3 size = new Vector3(4f, height, 6f);
        SoilField field = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<SoilField>() : null;
        if (field != null)
        {
            Rect b = field.layout.Bounds();
            go.transform.SetPositionAndRotation(field.World(b.center), field.transform.rotation);
            size = new Vector3(b.width + 0.4f, height, b.height + 0.4f);
        }
        else go.transform.position = PlaceNearSelection(0f);
        s.size = size;
        s.center = new Vector3(0f, height * 0.5f, 0f);

        Material frame = SharedMaterial(kind == GrowingStructureKind.MagicalChamber ? "Agriculture Dark Stone" : "Agriculture Wood",
            kind == GrowingStructureKind.MagicalChamber ? new Color(0.25f, 0.22f, 0.3f) : new Color(0.45f, 0.3f, 0.17f));
        float t = kind == GrowingStructureKind.ColdFrame ? 0.06f : 0.1f;
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(go.transform, PrimitiveType.Cube, "Post", new Vector3(x * size.x * 0.5f, height * 0.5f, z * size.z * 0.5f), new Vector3(t, height, t), frame, false);
        for (int z = -1; z <= 1; z += 2)
            Part(go.transform, PrimitiveType.Cube, "Beam", new Vector3(0f, height, z * size.z * 0.5f), new Vector3(size.x, t, t), frame, false);
        for (int x = -1; x <= 1; x += 2)
            Part(go.transform, PrimitiveType.Cube, "Beam", new Vector3(x * size.x * 0.5f, height, 0f), new Vector3(t, t, size.z), frame, false);
        InCity(go);
        Selection.activeGameObject = go;
        Debug.Log($"[Agriculture] {label}: the box (Center, Size) is what it covers - the frame is only a stand-in for your own model.", go);
        return s;
    }

    /// <summary>The Scene view's pivot on the ground, or beside the selected field.</summary>
    private static Vector3 PlaceNearSelection(float beside)
    {
        SoilField field = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<SoilField>() : null;
        Vector3 at;
        if (field != null)
        {
            Rect b = field.layout.Bounds();
            at = field.World(new Vector2(b.xMax + beside, b.center.y));
        }
        else at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        if (Physics.Raycast(at + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        return at;
    }

    private static void Part(Transform parent, PrimitiveType type, string partName, Vector3 at, Vector3 scale, Material m, bool solid)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localScale = scale;
        if (!solid)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && m != null) r.sharedMaterial = m;
    }

    /// <summary>A plain material asset (Agriculture/Materials), kept when it exists.</summary>
    private static Material SharedMaterial(string name, Color color)
    {
        string path = $"{SMAssetsEditor.Folder(SMAssets.Agriculture + "/Materials")}/{SMAssetsEditor.CleanFileName(name)}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ------------------------------------------------------------------ assets
    /// <summary>
    /// A Plant Species asset for every built-in species, listed in the database. A species the database (or the project)
    /// already has an asset of - by its id, whatever the file is called - keeps it.
    /// </summary>
    public static List<PlantSpecies> CreateSpeciesAssets(AgricultureDatabase db, bool select)
    {
        var made = new List<PlantSpecies>();
        Undo.RecordObject(db, "Create Plant Species Assets");
        db.species ??= new List<PlantSpecies>();
        foreach (PlantSpeciesData d in AgriDefaults.Species())
        {
            PlantSpecies asset = db.species.FirstOrDefault(s => s != null && string.Equals(s.Id, d.id, StringComparison.OrdinalIgnoreCase)) ?? ProjectAsset<PlantSpecies>(s => s.Id, d.id);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PlantSpecies>();
                asset.data = d;
                AssetDatabase.CreateAsset(asset, SMAssetsEditor.UniquePath(SMAssets.Plants, SMAssetsEditor.CleanFileName(d.name) + ".asset"));
            }
            if (!db.species.Contains(asset)) db.species.Add(asset);
            made.Add(asset);
        }
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Select(made, select);
        return made;
    }

    /// <summary>An item asset of every built-in soil material (with its icon and a sack to lie on the ground), listed in the database.</summary>
    public static List<SoilAmendmentSO> CreateMaterialAssets(AgricultureDatabase db, bool select)
    {
        var made = new List<SoilAmendmentSO>();
        Undo.RecordObject(db, "Create Soil Material Assets");
        db.amendments ??= new List<SoilAmendmentSO>();
        string folder = SMAssets.AgricultureItems + "/Soil Materials";
        foreach (SoilAmendmentData a in AgriDefaults.Amendments())
        {
            SoilAmendmentSO asset = db.amendments.FirstOrDefault(x => x != null && x.data != null && string.Equals(x.data.id, a.id, StringComparison.OrdinalIgnoreCase)) ??
                                    ProjectAsset<SoilAmendmentSO>(x => x.data != null ? x.data.id : "", a.id);
            if (asset == null)
            {
                string path = SMAssetsEditor.UniquePath(folder, SMAssetsEditor.CleanFileName(a.name) + ".asset");
                asset = ScriptableObject.CreateInstance<SoilAmendmentSO>();
                asset.Setup(a, SaveIcon(Path.GetFileNameWithoutExtension(path), PlantArt.AmendmentIcon(a)), null);
                ((Object)asset).name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(asset, path);
            }
            if (asset.Prefab == null)
                SetPrefab(asset, SaveModel(asset, PlantArt.Template("amend:" + a.id, PrimitiveType.Cube, a.color, new Vector3(0.25f, 0.3f, 0.18f))));
            if (!db.amendments.Contains(asset)) db.amendments.Add(asset);
            EditorUtility.SetDirty(asset);
            made.Add(asset);
        }
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Select(made, select);
        return made;
    }

    /// <summary>
    /// Item assets for every agricultural id the database does not bind yet - seeds and produce of every species, the
    /// tools, instruments and water containers - made like the runtime items (same names, prices, codes), with their
    /// icons saved as PNGs and a model to lie on the ground, and bound in the database. Returns how many are bound.
    /// </summary>
    public static int CreateItems(AgricultureDatabase db, bool select)
    {
        var made = new List<ItemSO>();
        Undo.RecordObject(db, "Create Agriculture Items");
        db.items ??= new List<AgriItemBinding>();
        db.MarkChanged();
        var ids = new List<(string id, string folder)>();
        foreach (PlantSpeciesData s in db.AllSpecies)
        {
            ids.Add((AgriIds.Seed(s.id), "Seeds"));
            ids.Add((AgriIds.Produce(s.id), "Produce"));
        }
        foreach (FarmToolPreset p in AgriDefaults.Tools()) ids.Add((AgriIds.Tool(p.id), "Equipment"));
        foreach (InstrumentPreset p in AgriDefaults.Instruments()) ids.Add((AgriIds.Instrument(p.id), "Equipment"));
        foreach (ContainerPreset p in AgriDefaults.Containers()) ids.Add((AgriIds.Container(p.id), "Equipment"));

        foreach ((string id, string sub) in ids)
        {
            if (db.BoundItem(id) != null) continue;
            ItemSO runtime = AgriItems.Resolve(id);
            if (runtime == null) continue;
            string label = runtime.name;
            string path = SMAssetsEditor.UniquePath(SMAssets.AgricultureItems + "/" + sub, SMAssetsEditor.CleanFileName(label) + ".asset");
            string file = Path.GetFileNameWithoutExtension(path);
            ItemSO asset = Object.Instantiate(runtime);
            asset.hideFlags = HideFlags.None;
            ((Object)asset).name = file;
            // The runtime drawing is not an asset: save it as a PNG sprite.
            var so0 = new SerializedObject(asset);
            SerializedProperty icon = so0.FindProperty("icon");
            if (icon != null)
            {
                icon.objectReferenceValue = SaveIcon(file, runtime.Icon);
                so0.ApplyModifiedPropertiesWithoutUndo();
            }
            SetPrefab(asset, null);
            AssetDatabase.CreateAsset(asset, path);
            GameObject template = runtime.Prefab;
            if (template != null) SetPrefab(asset, SaveModel(asset, template));
            db.items.Add(new AgriItemBinding { id = id, item = asset });
            EditorUtility.SetDirty(asset);
            made.Add(asset);
        }
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Select(made, select);
        return db.items.Count(b => b != null && b.item != null);
    }

    private static void Select<T>(List<T> made, bool select) where T : Object
    {
        if (!select || made.Count == 0) return;
        Selection.objects = made.Cast<Object>().ToArray();
        EditorGUIUtility.PingObject(made[0]);
    }

    /// <summary>The project's asset of type <typeparamref name="T"/> whose key is <paramref name="id"/>, wherever it is, or null.</summary>
    private static T ProjectAsset<T>(Func<T, string> key, string id) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && string.Equals(key(a), id, StringComparison.OrdinalIgnoreCase)) return a;
        }
        return null;
    }

    /// <summary>Saves a generated sprite as a PNG in Items/Agriculture/Icons (an existing one is kept) and returns the asset.</summary>
    public static Sprite SaveIcon(string name, Sprite generated)
    {
        if (generated == null || generated.texture == null) return null;
        string path = $"{SMAssetsEditor.Folder(SMAssets.AgricultureIcons)}/{SMAssetsEditor.CleanFileName(name)}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        File.WriteAllBytes(path, generated.texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// A model prefab for an item (Items/Agriculture/Models, named after it): a copy of the runtime model with its
    /// material saved as an asset and an Item Pickable giving <paramref name="item"/>. An existing prefab of that item is kept.
    /// </summary>
    public static GameObject SaveModel(ItemSO item, GameObject template)
    {
        if (item == null || template == null) return null;
        string file = SMAssetsEditor.CleanFileName(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(item)) is string f && f.Length > 0 ? f : item.name);
        string folder = SMAssetsEditor.Folder(SMAssets.AgricultureModels);
        string path = $"{folder}/{file}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            ItemPickable p = existing.GetComponent<ItemPickable>();
            if (p != null && p.itemScriptableObject == item) return existing;
            if (p == null || p.itemScriptableObject == null)
            {
                // A model of this item without its pickable (or one giving nothing): it gives this item.
                if (p == null) p = existing.AddComponent<ItemPickable>();
                p.itemScriptableObject = item;
                p.quantity = Mathf.Max(1, p.quantity);
                EditorUtility.SetDirty(existing);
                PrefabUtility.SavePrefabAsset(existing);
                return existing;
            }
            path = AssetDatabase.GenerateUniqueAssetPath(path);
        }
        GameObject copy = Object.Instantiate(template);
        copy.name = Path.GetFileNameWithoutExtension(path);
        copy.SetActive(true);
        copy.hideFlags = HideFlags.None;
        try
        {
            Renderer r = copy.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null && !EditorUtility.IsPersistent(r.sharedMaterial))
            {
                string matPath = Path.ChangeExtension(path, ".mat");
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(r.sharedMaterial);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                r.sharedMaterial = mat;
            }
            // Not '??': in the Editor a missing component is a fake null that '??' would keep.
            ItemPickable pick = copy.GetComponent<ItemPickable>();
            if (pick == null) pick = copy.AddComponent<ItemPickable>();
            pick.itemScriptableObject = item;
            pick.quantity = 1;
            return PrefabUtility.SaveAsPrefabAsset(copy, path);
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
    }

    private static void SetPrefab(ItemSO item, GameObject model)
    {
        if (item == null) return;
        var so = new SerializedObject(item);
        SerializedProperty p = so.FindProperty("prefab");
        if (p == null) return;
        p.objectReferenceValue = model;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }
}

/// <summary>
/// Agriculture's part of the Project Validator: the Agriculture Database (rules, species, materials, hybrids, item
/// bindings) and every soil field (its layout, a unique id, scale 1, where it is).
/// </summary>
[InitializeOnLoad]
public static class AgricultureValidation
{
    static AgricultureValidation()
    {
        ProjectValidator.ValidateRoot += CheckRoot;
        ProjectValidator.ValidateProject += (checks, issues) =>
        {
            if ((checks & ValidationChecks.ProjectAssets) != 0) CheckProject(issues);
        };
    }

    public static void CheckProject(List<ValidationIssue> issues)
    {
        AgricultureDatabase db = SMAssets.Load<AgricultureDatabase>(SMAssets.Settings, AgricultureDatabase.ResourcesName);
        if (db == null)
        {
            if (AgricultureEditors.Fields().Count > 0)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Agriculture", "No Agriculture Database: the soil fields use the built-in rules, species and materials.")
                    .WithFix("Create", () => AgricultureEditors.ProjectDatabase(false) != null));
            return;
        }
        db.MarkChanged();
        var errors = new List<string>();
        var warnings = new List<string>();
        db.Validate(errors, warnings);
        foreach (string e in errors) issues.Add(new ValidationIssue(IssueSeverity.Error, "Agriculture", e, db));
        foreach (string w in warnings) issues.Add(new ValidationIssue(IssueSeverity.Warning, "Agriculture", w, db));

        var seen = new Dictionary<string, SoilField>();
        foreach (SoilField f in AgricultureEditors.Fields())
        {
            if (seen.TryGetValue(f.FieldId, out SoilField other))
            {
                SoilField copy = f;
                issues.Add(new ValidationIssue(IssueSeverity.Error, "Agriculture",
                        $"'{f.name}' has the same Field Id as '{other.name}' (a duplicated object?): they would share one save.", f, ValidationUtility.PathOf(f.transform))
                    .WithFix("New Id", () => NewId(copy)));
            }
            else seen[f.FieldId] = f;
        }
    }

    public static void CheckRoot(GameObject root, List<ValidationIssue> issues)
    {
        if (root == null) return;
        foreach (SoilField f in root.GetComponentsInChildren<SoilField>(true))
        {
            string path = ValidationUtility.PathOf(f.transform);
            if (f.layout == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, "Agriculture", "The soil field has no layout.", f, path));
                continue;
            }
            var errors = new List<string>();
            var warnings = new List<string>();
            f.layout.Validate(errors, warnings);
            foreach (string e in errors) issues.Add(new ValidationIssue(IssueSeverity.Error, "Agriculture", e, f, path));
            foreach (string w in warnings) issues.Add(new ValidationIssue(IssueSeverity.Warning, "Agriculture", w, f, path));
            Vector3 s = f.transform.lossyScale;
            if (Mathf.Abs(s.x - 1f) > 0.001f || Mathf.Abs(s.z - 1f) > 0.001f)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Agriculture", "The soil field is scaled: its outline is in metres and ignores the scale (keep it at 1).", f, path));
            if (EditorUtility.IsPersistent(f.gameObject))
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Agriculture",
                    "A soil field in a prefab: every copy has the prefab's Field Id unless it is given its own (fields save by id).", f, path));
            Type net = NetSoilFieldType;
            if (net != null && !EditorUtility.IsPersistent(f.gameObject) && f.GetComponent(net) == null)
            {
                SoilField field = f;
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Agriculture",
                        "The soil field has no NetSoilField: in a network session only the host can work it.", f, path)
                    .WithFix("Make Networked", () => { AgricultureEditors.MakeNetworked(field); return true; }));
            }
        }
    }

    private static Type netType;
    private static bool netSearched;

    /// <summary>GameNet.NetSoilField when Fish-Net is in the project, else null.</summary>
    private static Type NetSoilFieldType
    {
        get
        {
            if (!netSearched)
            {
                netSearched = true;
                netType = SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetSoilField");
            }
            return netType;
        }
    }

    private static bool NewId(SoilField f)
    {
        if (f == null) return false;
        var so = new SerializedObject(f);
        so.FindProperty("fieldId").stringValue = Guid.NewGuid().ToString("N").Substring(0, 12);
        so.ApplyModifiedProperties();
        return true;
    }
}

/// <summary>The Agriculture Database: the default inspector with its checks and the asset tools at the top.</summary>
[CustomEditor(typeof(AgricultureDatabase))]
public class AgricultureDatabaseEditor : Editor
{
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();
    private double next;

    public override void OnInspectorGUI()
    {
        var db = (AgricultureDatabase)target;
        // Only on Layout: the number of messages must not change between Layout and Repaint.
        if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup > next)
        {
            next = EditorApplication.timeSinceStartup + 1.0;
            errors.Clear();
            warnings.Clear();
            db.MarkChanged();
            db.Validate(errors, warnings);
        }
        EditorGUILayout.LabelField($"{db.AllSpecies.Count} species · {db.AllAmendments.Count} soil materials · {db.Hybrids.Count} hybrid rule(s) · " +
                                   $"{db.items?.Count(b => b != null && b.item != null) ?? 0} bound item(s)", EditorStyles.miniLabel);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings.Take(12)) EditorGUILayout.HelpBox(w, MessageType.Warning);
        if (warnings.Count > 12) EditorGUILayout.HelpBox($"…and {warnings.Count - 12} more warning(s) (Agriculture ▸ Validate Agriculture lists them all).", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Species Assets")) AgricultureEditors.CreateSpeciesAssets(db, true);
            if (GUILayout.Button("Soil Material Assets")) AgricultureEditors.CreateMaterialAssets(db, true);
            if (GUILayout.Button("Item Assets")) AgricultureEditors.CreateItems(db, true);
        }
        EditorGUILayout.Space(4);
        DrawDefaultInspector();
    }
}

/// <summary>A plant species: the default inspector with its checks and a trial plot that grows it in the city's climate.</summary>
[CustomEditor(typeof(PlantSpecies))]
public class PlantSpeciesEditor : Editor
{
    private static bool trialFold = true;
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();
    private double next;
    private AgriTrial.Options options;
    private AgricultureDatabase projectDb;
    private string report = "";
    private Vector2 scroll;

    public override void OnInspectorGUI()
    {
        var sp = (PlantSpecies)target;
        if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup > next)
        {
            next = EditorApplication.timeSinceStartup + 0.75;
            errors.Clear();
            warnings.Clear();
            sp.Validate(errors, warnings);
            projectDb = SMAssets.Load<AgricultureDatabase>(SMAssets.Settings, AgricultureDatabase.ResourcesName);
        }
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        AgricultureDatabase db = projectDb;
        if (db != null)
        {
            if (db.species == null || !db.species.Contains(sp))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox("Not in the Agriculture Database: the game does not know this species yet.", MessageType.Warning);
                    if (GUILayout.Button("Add", GUILayout.Width(50), GUILayout.Height(38)))
                    {
                        Undo.RecordObject(db, "Add Plant Species");
                        db.species ??= new List<PlantSpecies>();
                        db.species.Add(sp);
                        db.MarkChanged();
                        EditorUtility.SetDirty(db);
                    }
                }
        }
        TrialGUI(sp);
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && AgricultureDatabase.HasInstance) AgricultureDatabase.Instance.MarkChanged();
    }

    private void TrialGUI(PlantSpecies sp)
    {
        trialFold = EditorGUILayout.BeginFoldoutHeaderGroup(trialFold, "Trial plot");
        if (trialFold && sp.data != null)
        {
            options ??= AgriTrial.Options.For(sp.data, AgricultureDatabase.Instance.Settings);
            EditorGUILayout.HelpBox("Sows a few seeds in worked soil and grows them hour by hour in the city's climate - the game's own simulation - " +
                                    "to see when it comes up, flowers and ripens. For tuning only: players never see these numbers.", MessageType.None);
            options.soil = (SoilType)EditorGUILayout.EnumPopup("Soil", options.soil);
            options.idealPh = EditorGUILayout.Toggle(new GUIContent("Its best pH", "Off: the soil type's own pH."), options.idealPh);
            using (new EditorGUILayout.HorizontalScope())
            {
                options.startSeason = (Season)EditorGUILayout.EnumPopup("Sown in", options.startSeason);
                options.startDay = EditorGUILayout.IntSlider(options.startDay + 1, 1, Mathf.Max(1, AgricultureDatabase.Instance.Settings.daysPerSeason)) - 1;
            }
            options.days = EditorGUILayout.IntSlider("Days", options.days, 5, 400);
            options.plants = EditorGUILayout.IntSlider("Plants", options.plants, 1, 12);
            options.water = EditorGUILayout.Toggle(new GUIContent("Water when dry", "Waters each plant whenever its soil gets dry (checked twice a day)."), options.water);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Grow"))
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    AgriTrial.Report r = AgriTrial.Grow(sp.data, AgricultureDatabase.Instance, options);
                    report = r + $"\n({watch.ElapsedMilliseconds} ms to simulate {options.days} days)";
                }
                if (GUILayout.Button("Its own season", GUILayout.Width(110)))
                {
                    options = AgriTrial.Options.For(sp.data, AgricultureDatabase.Instance.Settings);
                    report = "";
                }
            }
            if (!string.IsNullOrEmpty(report))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(220));
                EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(report), EditorGUIUtility.currentViewWidth - 40f)));
                EditorGUILayout.EndScrollView();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif
