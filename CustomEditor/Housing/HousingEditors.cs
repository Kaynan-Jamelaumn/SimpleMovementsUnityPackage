#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Menus of the housing system (Tools ▸ SimpleMovements ▸ Housing, and GameObject ▸ SimpleMovements ▸ Housing):
/// properties (with a placeholder house, front door, chest, bed and garden, or fitted over a selected house), inns and
/// their rooms, the Realtor and Innkeeper NPCs, the Housing Settings, networked gardens, validation and the
/// documentation - and the helpers the housing inspectors share (what belongs to a place, free spots, new parts).
/// <para>New places go to the Scene view's pivot, on the ground, into the City scene when it is open; every change is undoable.</para>
/// </summary>
public static class HousingEditors
{
    private const string Menu = SMMenu.Root + "Housing/";
    private const string Hierarchy = "GameObject/SimpleMovements/Housing/";
    /// <summary>Land around a house the menus fit a property over, metres on each side.</summary>
    public const float LandMargin = 2f;

    // ================================================================== menus
    [MenuItem(Menu + "Create Property", false, SMMenu.Housing)]
    private static void CreatePropertyMenu() => CreateProperty(null);

    [MenuItem(Menu + "Create Property Over Selected Object", false, SMMenu.Housing + 1)]
    private static void CreatePropertyOverMenu() => CreatePropertyOver(Selection.activeGameObject);

    [MenuItem(Menu + "Create Property Over Selected Object", true)]
    private static bool CreatePropertyOverValidate()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && !EditorUtility.IsPersistent(go) && go.GetComponentInParent<Property>(true) == null && go.GetComponentInParent<Inn>(true) == null;
    }

    [MenuItem(Menu + "Create Inn", false, SMMenu.Housing + 2)]
    private static void CreateInnMenu() => CreateInn(null);

    [MenuItem(Menu + "Add Room To Selected Inn", false, SMMenu.Housing + 3)]
    private static void AddRoomMenu()
    {
        Inn inn = SelectedInn();
        if (inn != null) AddRoom(inn, true);
    }

    [MenuItem(Menu + "Add Room To Selected Inn", true)]
    private static bool AddRoomValidate() => SelectedInn() != null;

    [MenuItem(Menu + "Create Realtor NPC", false, SMMenu.Housing + 10)]
    private static void CreateRealtorMenu() => CreateRealtor();

    [MenuItem(Menu + "Create Innkeeper NPC", false, SMMenu.Housing + 11)]
    private static void CreateInnkeeperMenu() => CreateInnkeeper(SelectedInn());

    [MenuItem(Menu + "Create Housing Settings", false, SMMenu.Housing + 20)]
    private static void CreateSettingsMenu() => CreateSettings(true);

    [MenuItem(SMMenu.Root + "Project Setup/Create Housing Settings (Resources)", false, SMMenu.ProjectSetup + 12)]
    private static void CreateSettingsSetupMenu() => CreateSettings(true);

    [MenuItem(Menu + "Make Selected Gardens Networked", false, SMMenu.Housing + 30)]
    private static void NetworkedMenu()
    {
        List<SoilField> fields = SelectedGardens();
        var report = new StringBuilder();
        foreach (SoilField f in fields) report.AppendLine(AgricultureEditors.MakeNetworked(f));
        if (report.Length == 0) report.Append("Select properties (or their gardens) in the scene first.");
        EditorUtility.DisplayDialog("Housing", report.ToString().TrimEnd(), "OK");
    }

    [MenuItem(Menu + "Make Selected Gardens Networked", true)]
    private static bool NetworkedValidate() => SelectedGardens().Count > 0;

    [MenuItem(Menu + "Validate Housing", false, SMMenu.Housing + 40)]
    private static void ValidateMenu() => HousingValidation.ValidateOpenScenes();

    [MenuItem(Menu + "Help", false, SMMenu.Housing + 60)]
    private static void HelpMenu()
    {
        foreach (string g in AssetDatabase.FindAssets("README"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g).Replace('\\', '/');
            if (!path.Contains("Documentation/Housing/")) continue;
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
            return;
        }
        EditorUtility.DisplayDialog("Housing",
            "1. Housing ▸ Create Property (or select a house model and Create Property Over Selected Object).\n" +
            "2. In its inspector, shape the Land, the House and the Zones in the Scene view, and add gardens, chests, beds and doors.\n" +
            "3. Housing ▸ Create Realtor NPC lets the properties. Create Inn, + Room and Create Innkeeper NPC let rooms by the night.\n" +
            "4. Housing ▸ Create Housing Settings: what a lease day is, what sizes are worth, the districts.\n" +
            "5. Housing ▸ Validate Housing checks that the areas are truthful (no overlaps, nothing standing on a land).\n\n" +
            "Full guide: Documentation/Housing/README.md", "OK");
    }

    [MenuItem(Hierarchy + "Property", false, 30)]
    private static void PropertyFromHierarchy(MenuCommand command)
    {
        if (FirstOfSelection(command)) CreateProperty(command.context as GameObject);
    }

    [MenuItem(Hierarchy + "Inn", false, 31)]
    private static void InnFromHierarchy(MenuCommand command)
    {
        if (FirstOfSelection(command)) CreateInn(command.context as GameObject);
    }

    /// <summary>A GameObject menu item runs once per selected object: make one place, not one per object.</summary>
    private static bool FirstOfSelection(MenuCommand command) =>
        command == null || command.context == null || Selection.objects.Length <= 1 || command.context == Selection.objects[0];

    /// <summary>The inn of the selected object (an inn, one of its rooms or anything under it), or null.</summary>
    public static Inn SelectedInn()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && !EditorUtility.IsPersistent(go) ? go.GetComponentInParent<Inn>(true) : null;
    }

    private static List<SoilField> SelectedGardens()
    {
        var fields = new List<SoilField>();
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue;
            Property p = go.GetComponentInParent<Property>(true);
            if (p != null)
                foreach (SoilField f in GardensOf(p))
                    if (!fields.Contains(f)) fields.Add(f);
            SoilField own = go.GetComponentInParent<SoilField>(true);
            if (own != null && !fields.Contains(own)) fields.Add(own);
        }
        return fields;
    }

    // ================================================================== settings and NPCs
    /// <summary>The project's Housing Settings (Resources/SimpleMovements/Settings), made with the default rules when there is none.</summary>
    public static HousingSettings CreateSettings(bool select)
    {
        HousingSettings s = SMAssetsEditor.FindOrCreate<HousingSettings>(SMAssets.Settings, HousingSettings.ResourcesName, x => x.Defaults(), select);
        HousingSettings.Override(null);
        return s;
    }

    /// <summary>A Realtor NPC (it lets the city's properties) at the Scene view's pivot, on the ground.</summary>
    public static Realtor CreateRealtor()
    {
        Vector3 at = GroundAt(Pivot(), null);
        GameObject go = MakeNPC();
        go.name = "Realtor";
        Name(go, "Realtor", "Houses to let");
        Realtor r = go.GetComponent<Realtor>();
        if (r == null) r = Undo.AddComponent<Realtor>(go);
        go.transform.position = at;
        InCity(go);
        Selection.activeGameObject = go;
        return r;
    }

    /// <summary>
    /// An Innkeeper NPC: for <paramref name="inn"/> when given - standing inside it by its front, on a spot no room covers
    /// (where + Room leaves the entrance free) - else at the Scene view's pivot.
    /// </summary>
    public static Innkeeper CreateInnkeeper(Inn inn)
    {
        Vector3 at = GroundAt(Pivot(), null);
        GameObject go = MakeNPC();
        go.name = "Innkeeper";
        Name(go, "Innkeeper", "Rooms by the night");
        Innkeeper k = go.GetComponent<Innkeeper>();
        if (k == null) k = Undo.AddComponent<Innkeeper>(go);
        if (inn != null)
        {
            k.inn = inn;
            Vector2 entrance = Entrance(inn);
            if (inn.area == null || inn.area.Count < 3 || !FreeSpot(inn.area, new Vector2(0.8f, 0.8f), GroundRooms(inn), 0.2f, entrance, out Vector2 spot)) spot = entrance;
            go.transform.SetPositionAndRotation(inn.World(spot), inn.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            if (inn.gameObject.scene.IsValid() && go.scene != inn.gameObject.scene) Undo.MoveGameObjectToScene(go, inn.gameObject.scene, "Create Innkeeper");
            EditorUtility.SetDirty(k);
        }
        else
        {
            go.transform.position = at;
            InCity(go);
        }
        Selection.activeGameObject = go;
        return k;
    }

    /// <summary>A new NPC with no option of its own (no Talk: the housing option is its only one).</summary>
    private static GameObject MakeNPC()
    {
        GameObject go = NPCSetupMenu.CreateNPC(null, false);
        foreach (NPCDialogue talk in go.GetComponents<NPCDialogue>()) Undo.DestroyObjectImmediate(talk);
        return go;
    }

    private static void Name(GameObject npcObject, string displayName, string title)
    {
        NPC npc = npcObject.GetComponent<NPC>();
        if (npc == null) return;
        var so = new SerializedObject(npc);
        SerializedProperty n = so.FindProperty("displayName"), t = so.FindProperty("title");
        if (n != null) n.stringValue = displayName;
        if (t != null) t.stringValue = title;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================================================================== properties
    /// <summary>
    /// A property at the Scene view's pivot, on the ground (under <paramref name="parent"/> when given, else in the City
    /// scene when it is open): a 16 × 12 m plot with an 8 × 6 m house at the back - a placeholder house (walls with a
    /// doorway, a roof), its front door, a chest and a bed inside, and a 6 × 4 m garden in the yard. Undoable; selected.
    /// </summary>
    public static Property CreateProperty(GameObject parent)
    {
        Vector3 at = GroundAt(Pivot(), null);
        var go = new GameObject("Property");
        go.transform.position = at;
        Property p = go.AddComponent<Property>();
        p.displayName = "House";
        p.land = HousingGeometry.Rectangle(Vector2.zero, new Vector2(16f, 12f));
        p.house = HousingGeometry.Rectangle(new Vector2(0f, 2f), new Vector2(8f, 6f));
        p.floors = 1;
        p.floorHeight = 3f;

        Rect h = HousingGeometry.Bounds(p.house);
        // The placeholder house: children, so they never count as standing on the land.
        PlaceholderHouse(go.transform, h, p.floors * p.floorHeight);
        MakeDoor(go.transform, "Front Door", new Vector3(h.center.x - 0.5f, 0f, h.yMin - 0.06f));
        MakeChest(go.transform, new Vector3(h.center.x + h.width * 0.3f, 0f, h.yMax - 0.8f));
        MakeBed(go.transform, new Vector3(h.center.x - h.width * 0.3f, 0f, h.yMax - 1.4f));
        MakeGarden(go.transform, new Vector2(-4f, -3.5f), new Vector2(6f, 4f));
        p.Collect();

        Finish(go, parent, "Create Property");
        Debug.Log("[Housing] Made a property: a 16 × 12 m plot with an 8 × 6 m house at the back, its front door, a chest, a bed and a garden " +
                  "(the cubes are placeholders for your own models). Shape it with the Property inspector's tools: Land, House, Zones.", go);
        return p;
    }

    /// <summary>
    /// A placeholder house on the footprint <paramref name="h"/>: four walls with a doorway behind the front door (in the
    /// middle of the front, its -Z side) and a roof - boxes with their colliders, so one walks in, and sees and reaches
    /// what is inside.
    /// </summary>
    private static void PlaceholderHouse(Transform parent, Rect h, float height)
    {
        var house = new GameObject("House (placeholder)");
        house.transform.SetParent(parent, false);
        Transform t = house.transform;
        const float wall = 0.2f, doorway = 1.2f, doorHeight = 2.3f;
        float mid = height * 0.5f, side = Mathf.Max(0.1f, (h.width - doorway) * 0.5f);
        Box(t, "Back Wall", new Vector3(h.center.x, mid, h.yMax - wall * 0.5f), new Vector3(h.width, height, wall));
        Box(t, "Left Wall", new Vector3(h.xMin + wall * 0.5f, mid, h.center.y), new Vector3(wall, height, h.height));
        Box(t, "Right Wall", new Vector3(h.xMax - wall * 0.5f, mid, h.center.y), new Vector3(wall, height, h.height));
        Box(t, "Front Wall (Left)", new Vector3(h.xMin + side * 0.5f, mid, h.yMin + wall * 0.5f), new Vector3(side, height, wall));
        Box(t, "Front Wall (Right)", new Vector3(h.xMax - side * 0.5f, mid, h.yMin + wall * 0.5f), new Vector3(side, height, wall));
        if (height > doorHeight + 0.05f)
            Box(t, "Over The Door", new Vector3(h.center.x, (doorHeight + height) * 0.5f, h.yMin + wall * 0.5f), new Vector3(doorway, height - doorHeight, wall));
        Box(t, "Roof", new Vector3(h.center.x, height + wall * 0.5f, h.center.y), new Vector3(h.width + 0.4f, wall, h.height + 0.4f));
    }

    /// <summary>A placeholder box (a cube with its collider) of <paramref name="size"/> centred at <paramref name="centre"/>.</summary>
    private static void Box(Transform parent, string label, Vector3 centre, Vector3 size)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = label;
        b.transform.SetParent(parent, false);
        b.transform.localPosition = centre;
        b.transform.localScale = size;
    }

    /// <summary>
    /// A property fitted over <paramref name="over"/> (a house model): its house is the model's footprint (turned with it,
    /// its floors from its height), its land the house with <see cref="LandMargin"/> m around it. The model becomes the
    /// property's child (so it is its house, not something standing on its land) - an inner part of a prefab instance once
    /// the prefab is unpacked, which it asks first - and its doors (each Door Controller given a Lockable Door), chests, beds
    /// and soil fields belong to the property. A model left inside its prefab stays where it is, with those listed (its
    /// walls then count as standing on the land). In the model's scene, where the model was.
    /// </summary>
    public static Property CreatePropertyOver(GameObject over)
    {
        if (over == null || EditorUtility.IsPersistent(over)) return CreateProperty(null);
        if (!Footprint(over, out Vector3 centre, out Quaternion rot, out Vector2 size, out float height))
        {
            Debug.LogWarning($"[Housing] '{over.name}' has no mesh, renderer or collider to fit a property over: made one at the Scene view's pivot.", over);
            return CreateProperty(null);
        }
        bool child = CanReparent(over) || Unpack(over);
        size = Vector2.Max(size, new Vector2(1.5f, 1.5f));
        var go = new GameObject($"Property ({over.name})");
        go.transform.SetPositionAndRotation(centre, rot);
        Property p = go.AddComponent<Property>();
        p.displayName = over.name;
        p.house = HousingGeometry.Rectangle(Vector2.zero, size);
        p.land = HousingGeometry.Rectangle(Vector2.zero, size + new Vector2(LandMargin * 2f, LandMargin * 2f));
        p.floorHeight = 3f;
        p.floors = Mathf.Clamp(Mathf.FloorToInt(height / p.floorHeight + 0.25f), 1, 12);

        Undo.RegisterCreatedObjectUndo(go, "Create Property");
        if (over.scene.IsValid() && go.scene != over.scene) Undo.MoveGameObjectToScene(go, over.scene, "Create Property");
        if (over.transform.parent != null) Undo.SetTransformParent(go.transform, over.transform.parent, "Create Property");
        if (child) Undo.SetTransformParent(over.transform, go.transform, "Create Property");
        int locked = 0;
        foreach (DoorController door in over.GetComponentsInChildren<DoorController>(true))
            if (door.GetComponent<LockableDoor>() == null && AddLock(door, null) != null) locked++;
        Undo.RecordObject(p, "Create Property");
        p.Collect();
        bool part = !child && AddPart(p, over);
        if (!child) ListParts(p, over);
        EditorUtility.SetDirty(p);
        Selection.activeGameObject = go;
        Debug.Log($"[Housing] Made a property over '{over.name}': a {size.x:0.#} × {size.y:0.#} m house ({p.floors} floor(s) by its height - check it) on " +
                  $"{size.x + LandMargin * 2f:0.#} × {size.y + LandMargin * 2f:0.#} m of land. " +
                  (child ? $"'{over.name}' is now its child." :
                      part ? $"'{over.name}' is part of a prefab and stays where it is, listed in the property's Parts (its own, not something standing on " +
                             "its land); its doors, chests, beds and gardens are listed too." :
                      $"'{over.name}' is part of a prefab and stays where it is: its doors, chests, beds and gardens are listed, but its walls count as " +
                      "standing on the land (unpack the prefab to make it the property's child).") +
                  (locked > 0 ? $" {locked} Door Controller(s) got a Lockable Door (only the tenant opens them)." : "") +
                  " Shape the land with the Property inspector's tools.", go);
        return p;
    }

    /// <summary>
    /// Asks to unpack the prefab instances <paramref name="go"/> is an inner part of (as far as needed: prefabs nested further
    /// stay prefabs), so it can move under the property. True when it can now.
    /// </summary>
    private static bool Unpack(GameObject go)
    {
        GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (outer == null || Application.isBatchMode) return false;
        if (!EditorUtility.DisplayDialog("Create Property Over Selected Object",
                $"'{go.name}' is a part of the prefab instance '{outer.name}', so it cannot become the property's child: the checks would see it " +
                "standing on its own land, and only what is listed by hand would belong to the property.\n\n" +
                $"Unpack '{outer.name}' (as far as needed - prefabs nested deeper stay prefabs) so '{go.name}' can move under the property?",
                "Unpack", "Keep The Prefab"))
            return false;
        for (int i = 0; i < 8 && !CanReparent(go); i++)
        {
            outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (outer == null) break;
            PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        }
        return CanReparent(go);
    }

    /// <summary>Lists the model in the property's Parts (its own, though not its child), when the property has such a list (a serialized "parts").</summary>
    private static bool AddPart(Property p, GameObject model)
    {
        var so = new SerializedObject(p);
        SerializedProperty parts = so.FindProperty("parts");
        if (parts == null || !parts.isArray || !parts.arrayElementType.StartsWith("PPtr<", StringComparison.Ordinal)) return false;
        parts.InsertArrayElementAtIndex(parts.arraySize);
        SerializedProperty e = parts.GetArrayElementAtIndex(parts.arraySize - 1);
        e.objectReferenceValue = model;
        if (e.objectReferenceValue == null) e.objectReferenceValue = model.transform; // a list of transforms
        if (e.objectReferenceValue == null) return false; // a list of something else: left as it was
        so.ApplyModifiedProperties();
        return true;
    }

    /// <summary>Lists the doors, chests (a new id each), beds and soil fields under <paramref name="model"/> in the property's lists (they are not its children).</summary>
    private static void ListParts(Property p, GameObject model)
    {
        foreach (LockableDoor d in model.GetComponentsInChildren<LockableDoor>(true))
            if (!p.doors.Contains(d)) p.doors.Add(d);
        foreach (SleepSpot b in model.GetComponentsInChildren<SleepSpot>(true))
            if (!p.beds.Contains(b)) p.beds.Add(b);
        foreach (SoilField f in model.GetComponentsInChildren<SoilField>(true))
            if (!p.gardens.Contains(f)) p.gardens.Add(f);
        foreach (Storage s in model.GetComponentsInChildren<Storage>(true))
            if (!p.chests.Exists(c => c != null && c.storage == s)) p.chests.Add(new HousingChest { id = Property.NewId(), storage = s });
    }

    /// <summary>
    /// Gives the Door Controller a Lockable Door that drives it - put before it on the object, so a player's interaction
    /// reaches the lock first - then lists it in <paramref name="owner"/> (a property or an inn room) when given. Undoable.
    /// </summary>
    public static LockableDoor AddLock(DoorController door, Component owner)
    {
        if (door == null) return null;
        LockableDoor d = door.GetComponent<LockableDoor>();
        if (d == null)
        {
            d = Undo.AddComponent<LockableDoor>(door.gameObject);
            Component[] all = door.GetComponents<Component>();
            int target = Array.IndexOf(all, door);
            for (int i = Array.IndexOf(all, d); i > target && target >= 0; i--) UnityEditorInternal.ComponentUtility.MoveComponentUp(d);
        }
        if (owner is Property p) Collect(p);
        else if (owner is InnRoom r) Collect(r);
        return d;
    }

    /// <summary>Can the object be moved under another (not an inner part of a prefab instance)?</summary>
    private static bool CanReparent(GameObject go) => !PrefabUtility.IsPartOfPrefabInstance(go) || PrefabUtility.IsOutermostPrefabInstanceRoot(go);

    /// <summary>The object's footprint, turned with it: its middle on the ground, its turn, its size (X, Z) and its height.</summary>
    private static bool Footprint(GameObject over, out Vector3 centre, out Quaternion rot, out Vector2 size, out float height)
    {
        centre = over.transform.position;
        rot = Quaternion.Euler(0f, over.transform.eulerAngles.y, 0f);
        size = Vector2.zero;
        height = 0f;
        var corners = new List<Vector3>();
        foreach (MeshFilter mf in over.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++) corners.Add(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, Corner(i))));
        }
        if (corners.Count == 0)
        {
            var boxes = new List<Bounds>();
            foreach (Renderer r in over.GetComponentsInChildren<Renderer>()) boxes.Add(r.bounds);
            if (boxes.Count == 0)
                foreach (Collider c in over.GetComponentsInChildren<Collider>())
                    boxes.Add(c.bounds);
            foreach (Bounds b in boxes)
                for (int i = 0; i < 8; i++)
                    corners.Add(b.center + Vector3.Scale(b.extents, Corner(i)));
        }
        if (corners.Count == 0) return false;
        Quaternion inv = Quaternion.Inverse(rot);
        Vector3 origin = corners[0];
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        foreach (Vector3 c in corners)
        {
            Vector3 l = inv * (c - origin);
            minX = Mathf.Min(minX, l.x);
            maxX = Mathf.Max(maxX, l.x);
            minZ = Mathf.Min(minZ, l.z);
            maxZ = Mathf.Max(maxZ, l.z);
            bottom = Mathf.Min(bottom, c.y);
            top = Mathf.Max(top, c.y);
        }
        size = new Vector2(maxX - minX, maxZ - minZ);
        centre = origin + rot * new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        centre.y = bottom;
        // On the ground under its middle (not on the model itself) when that is near its base.
        Vector3 ground = GroundAt(centre, over.transform);
        if (Mathf.Abs(ground.y - bottom) < 2f) centre.y = ground.y;
        height = Mathf.Max(0f, top - centre.y);
        return true;
    }

    private static Vector3 Corner(int i) => new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f);

    /// <summary>
    /// A garden for the property: a Soil Field child on a free spot of the land beside the house (clear of the house, its
    /// zones, its other gardens and its doors), 6 × 4 m when it fits, smaller when not. Undoable; listed in its Gardens.
    /// </summary>
    public static SoilField AddGarden(Property p)
    {
        if (p == null) return null;
        var taken = new List<List<Vector2>>();
        if (p.house != null && p.house.Count >= 3) taken.Add(p.house);
        if (p.zones != null)
            foreach (PropertyZone z in p.zones)
                if (z != null && z.outline != null && z.outline.Count >= 3) taken.Add(z.outline);
        foreach (SoilField f in GardensOf(p))
            if (f.layout != null && f.layout.outline != null && f.layout.outline.Count >= 3) taken.Add(Convert(f.layout.outline, f.transform, p.transform));
        foreach (AnimalPen pen in PensOf(p))
            if (pen.outline != null && pen.outline.Count >= 3) taken.Add(Convert(pen.outline, pen.transform, p.transform));
        foreach (LockableDoor d in p.GetComponentsInChildren<LockableDoor>(true)) taken.Add(HousingGeometry.Rectangle(p.Local(d.transform.position), new Vector2(1.5f, 1.5f)));
        if (p.doors != null)
            foreach (LockableDoor d in p.doors)
                if (d != null) taken.Add(HousingGeometry.Rectangle(p.Local(d.transform.position), new Vector2(1.5f, 1.5f)));

        Vector2 near = p.house != null && p.house.Count >= 3 ? GroundAreaHandles.Centre(p.house) : GroundAreaHandles.Centre(p.land);
        Vector2[] sizes = { new Vector2(6f, 4f), new Vector2(4f, 6f), new Vector2(4f, 3f), new Vector2(3f, 4f), new Vector2(2f, 2f) };
        Vector2 size = sizes[sizes.Length - 1], centre = GroundAreaHandles.Centre(p.land);
        bool found = false;
        foreach (Vector2 s in sizes)
        {
            if (!FreeSpot(p.land, s, taken, 0.5f, near, out Vector2 c)) continue;
            size = s;
            centre = c;
            found = true;
            break;
        }
        string label = UniqueChildName(p.transform, "Garden");
        SoilField field = MakeGarden(p.transform, centre, size);
        field.gameObject.name = label;
        Undo.RegisterCreatedObjectUndo(field.gameObject, "Add Garden");
        Undo.RecordObject(p, "Add Garden");
        if (!p.gardens.Contains(field)) p.gardens.Add(field);
        EditorUtility.SetDirty(p);
        if (found) Debug.Log($"[Housing] Made a {size.x:0.#} × {size.y:0.#} m garden on the land of '{p.DisplayName}'. Shape it with its Soil Field inspector.", field);
        else Debug.LogWarning($"[Housing] No free spot on the land of '{p.DisplayName}' for a garden: put a 2 × 2 m one in the middle of the land. Move it or make the land bigger.", field);
        return field;
    }

    private static SoilField MakeGarden(Transform parent, Vector2 centre, Vector2 size)
    {
        var go = new GameObject("Garden");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centre.x, 0f, centre.y);
        SoilField f = go.AddComponent<SoilField>();
        f.layout = new SoilLayout { seed = UnityEngine.Random.Range(1, int.MaxValue) };
        f.layout.outline = HousingGeometry.Rectangle(Vector2.zero, size);
        return f;
    }

    /// <summary>The property's animal pens: those listed, and (with Include Children) those among its children.</summary>
    public static List<AnimalPen> PensOf(Property p)
    {
        var list = new List<AnimalPen>();
        if (p == null) return list;
        if (p.pens != null)
            foreach (AnimalPen a in p.pens)
                if (a != null && !list.Contains(a)) list.Add(a);
        if (p.includeChildren)
            foreach (AnimalPen a in p.GetComponentsInChildren<AnimalPen>(true))
                if (!list.Contains(a) && a.GetComponentInParent<Property>(true) == p) list.Add(a);
        return list;
    }

    /// <summary>
    /// An animal pen for the property, placed like a garden: an Animal Pen child on a free spot of the land (clear of the
    /// house, its zones, its gardens, its other pens and its doors), as big as its kind usually is when that fits, smaller
    /// when not. Undoable; listed in its Pens.
    /// </summary>
    public static AnimalPen AddPen(Property p, AnimalHabitatKind kind)
    {
        if (p == null) return null;
        var taken = new List<List<Vector2>>();
        if (p.house != null && p.house.Count >= 3) taken.Add(p.house);
        if (p.zones != null)
            foreach (PropertyZone z in p.zones)
                if (z != null && z.outline != null && z.outline.Count >= 3) taken.Add(z.outline);
        foreach (SoilField f in GardensOf(p))
            if (f.layout != null && f.layout.outline != null && f.layout.outline.Count >= 3) taken.Add(Convert(f.layout.outline, f.transform, p.transform));
        foreach (AnimalPen other in PensOf(p))
            if (other.outline != null && other.outline.Count >= 3) taken.Add(Convert(other.outline, other.transform, p.transform));
        foreach (LockableDoor d in p.GetComponentsInChildren<LockableDoor>(true)) taken.Add(HousingGeometry.Rectangle(p.Local(d.transform.position), new Vector2(1.5f, 1.5f)));

        Vector2[] sizes = AnimalPenSizes(kind);
        Vector2 near = p.house != null && p.house.Count >= 3 ? GroundAreaHandles.Centre(p.house) : GroundAreaHandles.Centre(p.land);
        Vector2 size = sizes[sizes.Length - 1], centre = GroundAreaHandles.Centre(p.land);
        bool found = false;
        foreach (Vector2 s in sizes)
        {
            if (!FreeSpot(p.land, s, taken, 0.5f, near, out Vector2 c)) continue;
            size = s;
            centre = c;
            found = true;
            break;
        }
        string label = UniqueChildName(p.transform, kind == AnimalHabitatKind.Pasture ? "Pasture" : kind.ToString());
        var go = new GameObject(label);
        go.transform.SetParent(p.transform, false);
        go.transform.localPosition = new Vector3(centre.x, 0f, centre.y);
        AnimalPen pen = go.AddComponent<AnimalPen>();
        pen.kind = kind;
        pen.DisplayName = label;
        pen.outline = HousingGeometry.Rectangle(Vector2.zero, size);
        pen.feederPoint = new Vector2(-size.x * 0.5f + 0.7f, size.y * 0.5f - 0.5f);
        pen.troughPoint = new Vector2(size.x * 0.5f - 0.8f, size.y * 0.5f - 0.5f);
        // Stations sized to the pen: a pasture's herd drinks buckets a day, a coop's a few cups.
        float area = size.x * size.y;
        pen.feederCapacity = Mathf.Max(40f, Mathf.Round(area * 2.5f));
        pen.waterCapacity = Mathf.Max(20f, Mathf.Round(area * 4f));
        pen.troughSurface = Mathf.Clamp(area / 40f, 0.4f, 2f);
        Undo.RegisterCreatedObjectUndo(go, "Add Animal Pen");
        Undo.RecordObject(p, "Add Animal Pen");
        if (p.pens == null) p.pens = new List<AnimalPen>();
        if (!p.pens.Contains(pen)) p.pens.Add(pen);
        EditorUtility.SetDirty(p);
        if (found) Debug.Log($"[Housing] Made a {size.x:0.#} × {size.y:0.#} m {kind.ToString().ToLowerInvariant()} on the land of '{p.DisplayName}', clear of its house, garden and zones. Shape it with its Animal Pen inspector.", pen);
        else Debug.LogWarning($"[Housing] No free spot on the land of '{p.DisplayName}' for an animal pen: put a {size.x:0.#} × {size.y:0.#} m one in the middle of the land. Move it or make the land bigger.", pen);
        return pen;
    }

    /// <summary>The sizes tried for a new pen of a kind, the usual first (a pasture is roomy, a coop or a hutch small).</summary>
    public static Vector2[] AnimalPenSizes(AnimalHabitatKind kind)
    {
        switch (kind)
        {
            case AnimalHabitatKind.Pasture: return new[] { new Vector2(8f, 6f), new Vector2(6f, 6f), new Vector2(6f, 4f), new Vector2(4f, 4f), new Vector2(3f, 3f) };
            case AnimalHabitatKind.Coop:
            case AnimalHabitatKind.Hutch: return new[] { new Vector2(4f, 3f), new Vector2(3f, 3f), new Vector2(3f, 2f), new Vector2(2f, 2f) };
            default: return new[] { new Vector2(6f, 4f), new Vector2(4f, 4f), new Vector2(4f, 3f), new Vector2(3f, 3f), new Vector2(2f, 2f) };
        }
    }

    /// <summary>Gives the property's gardens what a network session needs (Agriculture's Make Networked); one line per garden.</summary>
    public static string MakeGardensNetworked(Property p)
    {
        var report = new StringBuilder();
        foreach (SoilField f in GardensOf(p)) report.AppendLine(AgricultureEditors.MakeNetworked(f));
        return report.Length == 0 ? "The property has no gardens." : report.ToString().TrimEnd();
    }

    // ================================================================== inns
    /// <summary>
    /// An inn at the Scene view's pivot, on the ground (under <paramref name="parent"/> when given, else in the City scene
    /// when it is open): a 20 × 14 m building's area with two rooms. Undoable; selected.
    /// </summary>
    public static Inn CreateInn(GameObject parent)
    {
        Vector3 at = GroundAt(Pivot(), null);
        var go = new GameObject("Inn");
        go.transform.position = at;
        Inn inn = go.AddComponent<Inn>();
        inn.displayName = "Inn";
        inn.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(20f, 14f));
        Finish(go, parent, "Create Inn");
        AddRoom(inn, false);
        AddRoom(inn, false);
        Selection.activeGameObject = go;
        Debug.Log("[Housing] Made an inn with two rooms (a bed, a chest and a door each - placeholders for your own models). Shape its area with the " +
                  "Inn inspector's Building tool, the rooms with its Rooms tool, and give it an Innkeeper (Housing ▸ Create Innkeeper NPC).", go);
        return inn;
    }

    /// <summary>
    /// A room for the inn: a child "Room N" with an Inn Room (a 4 × 4 m area), a Bed (Sleep Spot), a Chest (Storage, 16
    /// slots) and a Door (Lockable Door) at its front edge, on a free spot of the inn's area whose door opens onto free floor
    /// (inside the inn, in no room, the entrance left free) that can be walked to from the entrance - without cutting off
    /// another room's door. Undoable; selected when <paramref name="select"/>.
    /// </summary>
    public static InnRoom AddRoom(Inn inn, bool select)
    {
        if (inn == null) return null;
        var size = new Vector2(4f, 4f);
        bool hasArea = inn.area != null && inn.area.Count >= 3;
        Rect b = hasArea ? HousingGeometry.Bounds(inn.area) : new Rect(-2f, -2f, 4f, 4f);
        List<List<Vector2>> rooms = GroundRooms(inn);
        Vector2 entrance = Entrance(inn);
        // Not to be built over: the rooms, the floor in front of each room's door, the entrance.
        var doorways = new List<List<Vector2>>();
        foreach (InnRoom r in RoomsOf(inn))
        {
            if (r.area == null || r.area.Count < 3 || Mathf.Abs(r.transform.position.y - inn.transform.position.y) >= HousingValidation.FloorSeparation) continue;
            foreach (LockableDoor d in r.GetComponentsInChildren<LockableDoor>(true)) doorways.Add(Doorway(inn.Local(DoorFront(r.transform, r.area, d))));
        }
        var taken = new List<List<Vector2>>(rooms);
        taken.AddRange(doorways);
        taken.Add(Doorway(entrance));
        foreach (Innkeeper k in HousingSceneObjects.Cached().innkeepers) // where its innkeepers stand stays free too
            if (k != null && (k.inn == inn || (k.inn == null && k.GetComponentInParent<Inn>(true) == inn)))
                taken.Add(Doorway(inn.Local(k.transform.position)));
        // The doors that can be walked to from the entrance now must stay so (when the entrance itself is free).
        InnFloor floor = hasArea ? new InnFloor(inn.area, rooms) : null;
        var reached = new List<List<Vector2>>();
        if (floor != null && floor.Walk(entrance, null))
        {
            foreach (List<Vector2> w in doorways)
                if (floor.Reached(w)) reached.Add(w);
        }
        else floor = null;
        bool found = FreeSpot(inn.area, size, taken, 0f, new Vector2(b.xMin, b.yMin), out Vector2 centre, c =>
        {
            List<Vector2> way = Doorway(new Vector2(c.x, c.y - size.y * 0.5f - HousingValidation.DoorStep));
            if (!HousingGeometry.Holds(inn.area, way)) return false;
            foreach (List<Vector2> room in rooms)
                if (HousingGeometry.Overlap(way, room)) return false;
            if (floor == null) return true;
            floor.Walk(entrance, new Rect(c - size * 0.5f, size));
            if (!floor.Reached(way)) return false;
            foreach (List<Vector2> w in reached)
                if (!floor.Reached(w)) return false;
            return true;
        });

        int n = NextRoomNumber(inn);
        var go = new GameObject($"Room {n}");
        go.transform.SetParent(inn.transform, false);
        go.transform.localPosition = new Vector3(centre.x, 0f, centre.y);
        InnRoom room = go.AddComponent<InnRoom>();
        room.displayName = $"Room {n}";
        room.area = HousingGeometry.Rectangle(Vector2.zero, size);
        room.bed = MakeBed(go.transform, new Vector3(-1.2f, 0f, 0.8f));
        MakeChest(go.transform, new Vector3(1.2f, 0f, 1.5f));
        MakeDoor(go.transform, "Door", new Vector3(-0.5f, 0f, -size.y * 0.5f));
        room.Collect();
        Undo.RegisterCreatedObjectUndo(go, "Add Inn Room");
        Undo.RecordObject(inn, "Add Inn Room");
        inn.Collect();
        EditorUtility.SetDirty(inn);
        if (!found)
            Debug.LogWarning($"[Housing] No free 4 × 4 m spot left in the area of '{inn.DisplayName}' whose door opens onto a corridor reached from the " +
                             $"entrance: '{go.name}' is in its middle. Move it, or make the inn's area bigger.", go);
        if (select) Selection.activeGameObject = go;
        return room;
    }

    /// <summary>Where guests come into the inn (and its innkeeper stands): the middle of its front (its -Z side), a step inside.</summary>
    public static Vector2 Entrance(Inn inn)
    {
        Rect b = inn.area != null && inn.area.Count >= 3 ? HousingGeometry.Bounds(inn.area) : new Rect(-1f, -1f, 2f, 2f);
        return new Vector2(b.center.x, b.yMin + Mathf.Min(HousingValidation.DoorStep, b.height * 0.25f));
    }

    /// <summary>The inn's rooms on its ground floor, as outlines in the inn's space.</summary>
    private static List<List<Vector2>> GroundRooms(Inn inn)
    {
        var rooms = new List<List<Vector2>>();
        foreach (InnRoom r in RoomsOf(inn))
        {
            if (r.area == null || r.area.Count < 3) continue;
            if (Mathf.Abs(r.transform.position.y - inn.transform.position.y) >= HousingValidation.FloorSeparation) continue; // another floor
            rooms.Add(Convert(r.area, r.transform, inn.transform));
        }
        return rooms;
    }

    /// <summary>The floor kept free in front of a door (1.5 m square about the step out of it).</summary>
    private static List<Vector2> Doorway(Vector2 front) => HousingGeometry.Rectangle(front, new Vector2(1.5f, 1.5f));

    /// <summary>
    /// Where one steps out of a room through its door (world): <see cref="HousingValidation.DoorStep"/> m straight out from
    /// the room's edge nearest the door's leaf.
    /// </summary>
    public static Vector3 DoorFront(Transform room, IList<Vector2> area, LockableDoor door)
    {
        Collider c = door.GetComponent<Collider>();
        Vector3 leaf = c is BoxCollider box ? door.transform.TransformPoint(box.center) : c != null ? c.bounds.center : door.transform.position;
        Vector2 at = HousingGeometry.ToLocal(leaf, room.position, room.rotation);
        Vector2 step = Outside(area, at, HousingValidation.DoorStep);
        return room.position + room.rotation * new Vector3(step.x, 0f, step.y);
    }

    /// <summary>The point <paramref name="distance"/> m outside the outline, straight out from its edge nearest <paramref name="at"/>.</summary>
    public static Vector2 Outside(IList<Vector2> area, Vector2 at, float distance)
    {
        if (area == null || area.Count < 3) return at;
        float best = float.MaxValue;
        Vector2 on = at, normal = Vector2.zero;
        for (int i = 0, j = area.Count - 1; i < area.Count; j = i++)
        {
            Vector2 a = area[j], e = area[i] - a;
            float len2 = e.sqrMagnitude;
            if (len2 < 1e-8f) continue;
            Vector2 q = a + e * Mathf.Clamp01(Vector2.Dot(at - a, e) / len2);
            float d = (q - at).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            on = q;
            normal = new Vector2(e.y, -e.x).normalized;
        }
        if (HousingGeometry.Contains(area, on + normal * 0.01f)) normal = -normal;
        return on + normal * distance;
    }

    /// <summary>An inn's ground floor in 0.5 m cells: free inside its area and in no room; what can be walked to from the entrance.</summary>
    private sealed class InnFloor
    {
        private const float Cell = 0.5f;
        private readonly Rect box;
        private readonly int w, h;
        private readonly bool[] free, seen;
        private readonly Queue<int> next = new Queue<int>();

        public InnFloor(List<Vector2> area, List<List<Vector2>> rooms)
        {
            box = HousingGeometry.Bounds(area);
            w = Mathf.Max(1, Mathf.CeilToInt(box.width / Cell));
            h = Mathf.Max(1, Mathf.CeilToInt(box.height / Cell));
            free = new bool[w * h];
            seen = new bool[w * h];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                Vector2 c = Centre(x, z);
                bool ok = HousingGeometry.Contains(area, c);
                for (int i = 0; i < rooms.Count && ok; i++) ok = !HousingGeometry.Contains(rooms[i], c);
                free[z * w + x] = ok;
            }
        }

        private Vector2 Centre(int x, int z) => new Vector2(box.xMin + (x + 0.5f) * Cell, box.yMin + (z + 0.5f) * Cell);

        /// <summary>Walks from the entrance, a new room standing on <paramref name="room"/> when given. False when the entrance is not free.</summary>
        public bool Walk(Vector2 entrance, Rect? room)
        {
            Array.Clear(seen, 0, seen.Length);
            next.Clear();
            Visit(Mathf.FloorToInt((entrance.x - box.xMin) / Cell), Mathf.FloorToInt((entrance.y - box.yMin) / Cell), room);
            if (next.Count == 0) return false;
            while (next.Count > 0)
            {
                int i = next.Dequeue(), x = i % w, z = i / w;
                Visit(x + 1, z, room);
                Visit(x - 1, z, room);
                Visit(x, z + 1, room);
                Visit(x, z - 1, room);
            }
            return true;
        }

        private void Visit(int x, int z, Rect? room)
        {
            if (x < 0 || z < 0 || x >= w || z >= h) return;
            int i = z * w + x;
            if (seen[i] || !free[i] || (room.HasValue && room.Value.Contains(Centre(x, z)))) return;
            seen[i] = true;
            next.Enqueue(i);
        }

        /// <summary>Did the last <see cref="Walk"/> reach a cell of the outline?</summary>
        public bool Reached(List<Vector2> outline)
        {
            Rect r = HousingGeometry.Bounds(outline);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - box.xMin) / Cell)), x1 = Mathf.Min(w - 1, Mathf.FloorToInt((r.xMax - box.xMin) / Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - box.yMin) / Cell)), z1 = Mathf.Min(h - 1, Mathf.FloorToInt((r.yMax - box.yMin) / Cell));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                if (seen[z * w + x] && HousingGeometry.Contains(outline, Centre(x, z))) return true;
            return false;
        }
    }

    /// <summary>One more than the highest "Room N" of the inn (and than its number of rooms).</summary>
    private static int NextRoomNumber(Inn inn)
    {
        List<InnRoom> rooms = RoomsOf(inn);
        int n = rooms.Count;
        foreach (InnRoom r in rooms)
        {
            string name = r.displayName ?? "";
            if (name.StartsWith("Room ", StringComparison.Ordinal) && int.TryParse(name.Substring(5), out int k)) n = Mathf.Max(n, k);
        }
        return n + 1;
    }

    // ================================================================== what belongs to a place
    /// <summary>The property's gardens: those listed and (with Include Children) the Soil Fields among its children.</summary>
    public static List<SoilField> GardensOf(Property p)
    {
        var list = new List<SoilField>();
        if (p == null) return list;
        if (p.gardens != null)
            foreach (SoilField f in p.gardens)
                if (f != null && !list.Contains(f)) list.Add(f);
        if (p.includeChildren)
            foreach (SoilField f in p.GetComponentsInChildren<SoilField>(true))
                if (!list.Contains(f)) list.Add(f);
        return list;
    }

    /// <summary>The inn's rooms: those listed and the Inn Rooms among its children (the inn takes both).</summary>
    public static List<InnRoom> RoomsOf(Inn inn)
    {
        var list = new List<InnRoom>();
        if (inn == null) return list;
        if (inn.rooms != null)
            foreach (InnRoom r in inn.rooms)
                if (r != null && !list.Contains(r)) list.Add(r);
        foreach (InnRoom r in inn.GetComponentsInChildren<InnRoom>(true))
            if (!list.Contains(r)) list.Add(r);
        return list;
    }

    /// <summary>Lists the Soil Fields, chests, beds and doors among the property's children (even with Include Children off). Undoable.</summary>
    public static bool Collect(Property p)
    {
        if (p == null) return false;
        Undo.RecordObject(p, "Collect From Children");
        bool include = p.includeChildren;
        p.includeChildren = true;
        p.Collect();
        p.includeChildren = include;
        EditorUtility.SetDirty(p);
        return true;
    }

    /// <summary>Lists the Inn Rooms among the inn's children, and drops listed rooms that lie under another inn (that inn lets them). Undoable.</summary>
    public static bool Collect(Inn inn)
    {
        if (inn == null) return false;
        Undo.RecordObject(inn, "Collect Rooms");
        inn.Collect();
        inn.rooms.RemoveAll(r => r != null && UnderAnotherInn(r, inn));
        EditorUtility.SetDirty(inn);
        return true;
    }

    /// <summary>Takes a room out of the inn's Rooms (one that lies under another inn: that inn lets it). Undoable.</summary>
    public static bool DropRoom(Inn inn, InnRoom room)
    {
        if (inn == null || inn.rooms == null || !inn.rooms.Contains(room)) return false;
        Undo.RecordObject(inn, "Remove Room From List");
        inn.rooms.Remove(room);
        EditorUtility.SetDirty(inn);
        return true;
    }

    private static bool UnderAnotherInn(InnRoom r, Inn inn)
    {
        Inn under = r.GetComponentInParent<Inn>(true);
        return under != null && under != inn;
    }

    /// <summary>Finds the room's bed, and lists the chests and doors among its children. Undoable.</summary>
    public static bool Collect(InnRoom room)
    {
        if (room == null) return false;
        Undo.RecordObject(room, "Collect From Children");
        room.Collect();
        EditorUtility.SetDirty(room);
        return true;
    }

    // ================================================================== parts
    /// <summary>A bed: a child with a Sleep Spot and a 1 × 0.6 × 2 m box (its model a placeholder cube), standing at <paramref name="at"/>.</summary>
    public static SleepSpot MakeBed(Transform parent, Vector3 at) => Part(parent, "Bed", at, new Vector3(1f, 0.6f, 2f)).AddComponent<SleepSpot>();

    /// <summary>A chest: a child with a Storage ("Chest", 16 slots) and a 1 × 0.8 × 0.6 m box (its model a placeholder cube).</summary>
    public static Storage MakeChest(Transform parent, Vector3 at)
    {
        Storage s = Part(parent, "Chest", at, new Vector3(1f, 0.8f, 0.6f)).AddComponent<Storage>();
        s.name = "Chest";
        s.size = 16;
        return s;
    }

    /// <summary>
    /// A door: a child at its hinge (<paramref name="hinge"/>) with a Lockable Door and a 1 × 2.2 × 0.1 m box beside the
    /// hinge, so the leaf (a placeholder cube) swings about it.
    /// </summary>
    public static LockableDoor MakeDoor(Transform parent, string label, Vector3 hinge)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = hinge;
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0.5f, 1.1f, 0f);
        box.size = new Vector3(1f, 2.2f, 0.1f);
        Model(go.transform, "Leaf (placeholder)", box.center, box.size);
        LockableDoor d = go.AddComponent<LockableDoor>();
        var so = new SerializedObject(d);
        SerializedProperty n = so.FindProperty("displayName");
        if (n != null) n.stringValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        return d;
    }

    private static GameObject Part(Transform parent, string label, Vector3 at, Vector3 size)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        box.size = size;
        Model(go.transform, "Model (placeholder)", box.center, size);
        return go;
    }

    private static void Model(Transform parent, string label, Vector3 centre, Vector3 size)
    {
        GameObject m = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m.name = label;
        Collider c = m.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        m.transform.SetParent(parent, false);
        m.transform.localPosition = centre;
        m.transform.localScale = size;
    }

    private static string UniqueChildName(Transform parent, string label)
    {
        if (parent.Find(label) == null) return label;
        for (int i = 2; ; i++)
            if (parent.Find($"{label} {i}") == null) return $"{label} {i}";
    }

    // ================================================================== places and spaces
    /// <summary>
    /// The middle of a free <paramref name="size"/> rectangle (not turned) inside <paramref name="area"/> that overlaps none of
    /// <paramref name="taken"/> by less than <paramref name="gap"/> metres (and that <paramref name="accept"/>, when given,
    /// accepts), nearest to <paramref name="near"/> - all in one space. False (and the area's middle) when none fits.
    /// </summary>
    public static bool FreeSpot(IList<Vector2> area, Vector2 size, IList<List<Vector2>> taken, float gap, Vector2 near, out Vector2 centre,
        Func<Vector2, bool> accept = null)
    {
        centre = GroundAreaHandles.Centre(area);
        if (area == null || area.Count < 3) return false;
        Rect b = HousingGeometry.Bounds(area);
        float step = Mathf.Max(0.25f, Mathf.Ceil(Mathf.Max(b.width, b.height) / 48f / 0.25f) * 0.25f);
        var grown = new Vector2(size.x + gap * 2f, size.y + gap * 2f);
        float best = float.MaxValue;
        bool found = false;
        for (float y = b.yMin + size.y * 0.5f; y <= b.yMax - size.y * 0.5f + 1e-4f; y += step)
        for (float x = b.xMin + size.x * 0.5f; x <= b.xMax - size.x * 0.5f + 1e-4f; x += step)
        {
            var c = new Vector2(x, y);
            float d = (c - near).sqrMagnitude;
            if (d >= best) continue;
            if (!HousingGeometry.Holds(area, HousingGeometry.Rectangle(c, size))) continue;
            if (taken != null)
            {
                List<Vector2> probe = HousingGeometry.Rectangle(c, grown);
                bool clear = true;
                for (int i = 0; i < taken.Count && clear; i++)
                    if (taken[i] != null && taken[i].Count >= 3 && HousingGeometry.Overlap(probe, taken[i])) clear = false;
                if (!clear) continue;
            }
            if (accept != null && !accept(c)) continue;
            best = d;
            centre = c;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// Points of <paramref name="from"/>'s ground plane (its local X, Z) as points of <paramref name="to"/>'s - from the two
    /// objects' offset in double, so as precise kilometres from the world's origin as near it.
    /// </summary>
    public static List<Vector2> Convert(IList<Vector2> pts, Transform from, Transform to)
    {
        if (pts == null || from == null || to == null) return new List<Vector2>();
        return HousingGeometry.Relative(pts, from.position, from.rotation, to.position, to.rotation);
    }

    /// <summary>The Scene view's pivot (the middle of the view).</summary>
    private static Vector3 Pivot() => SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

    /// <summary>The ground under <paramref name="at"/> (the first solid collider below a point 50 m above it, not under <paramref name="ignore"/>), else <paramref name="at"/>.</summary>
    public static Vector3 GroundAt(Vector3 at, Transform ignore)
    {
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 50f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 ground = at;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || (ignore != null && h.collider.transform.IsChildOf(ignore))) continue;
            if (h.distance >= best) continue;
            best = h.distance;
            ground = h.point;
        }
        return ground;
    }

    /// <summary>Registers a new place with Undo, under <paramref name="parent"/> or in the City scene, and selects it.</summary>
    private static void Finish(GameObject go, GameObject parent, string undo)
    {
        if (parent != null && !EditorUtility.IsPersistent(parent)) go.transform.SetParent(parent.transform, true);
        Undo.RegisterCreatedObjectUndo(go, undo);
        if (go.transform.parent == null) InCity(go);
        Selection.activeGameObject = go;
    }

    /// <summary>Moves a new object into the City scene when that is open (and says so when it is not).</summary>
    private static void InCity(GameObject go)
    {
        if (go == null || go.transform.parent != null) return;
        string city = AgricultureEditors.CitySceneName();
        if (string.IsNullOrEmpty(city)) return;
        Scene scene = SceneManager.GetSceneByName(city);
        if (scene.IsValid() && scene.isLoaded)
        {
            if (go.scene != scene) Undo.MoveGameObjectToScene(go, scene, "Move To The City");
            return;
        }
        Debug.Log($"[Housing] '{go.name}' is in '{go.scene.name}', not the City scene ('{city}'). Places to let belong in the permanent city: " +
                  "the hub world's other scenes are made again every cycle.", go);
    }

    // ================================================================== inspector parts
    /// <summary>A district: its text field, and a small menu of the Housing Settings' districts beside it.</summary>
    public static void DistrictField(SerializedProperty district, string[] names)
    {
        if (district == null) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(district);
            using (new EditorGUI.DisabledScope(names == null || names.Length == 0))
            {
                int current = -1;
                string value = (district.stringValue ?? "").Trim();
                if (names != null)
                    for (int i = 0; i < names.Length; i++)
                        if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase)) current = i;
                int picked = EditorGUILayout.Popup(current, names ?? Array.Empty<string>(), GUILayout.Width(90));
                if (picked != current && picked >= 0 && names != null) district.stringValue = names[picked];
            }
        }
    }
}

/// <summary>
/// The places around a selected property or inn, for its Scene view: the other properties' lands and inns' areas
/// within <see cref="Range"/> m in grey, filled red when they overlap it. <see cref="Refresh"/> it now and then;
/// <see cref="Draw"/> allocates nothing.
/// </summary>
public sealed class HousingNeighbours
{
    public static readonly Color Line = new Color(0.62f, 0.62f, 0.62f, 0.85f);
    public static readonly Color OverlapLine = new Color(1f, 0.25f, 0.2f, 1f);
    public static readonly Color OverlapFill = new Color(1f, 0.25f, 0.2f, 0.25f);
    /// <summary>Only places this near (metres, from one object to the other) are drawn.</summary>
    public const float Range = 250f;

    private readonly List<Component> places = new List<Component>();
    private readonly List<bool> overlapping = new List<bool>();

    /// <summary>Takes the places around <paramref name="self"/> from <paramref name="scene"/>; those in <paramref name="overlaps"/> are drawn red.</summary>
    public void Refresh(Component self, HousingSceneObjects scene, ICollection<Component> overlaps)
    {
        places.Clear();
        overlapping.Clear();
        if (self == null || scene == null) return;
        Vector3 at = self.transform.position;
        foreach (Property p in scene.properties)
            if (p != null && p != self && (p.transform.position - at).sqrMagnitude < Range * Range) Keep(p, overlaps);
        foreach (Inn inn in scene.inns)
            if (inn != null && inn != self && (inn.transform.position - at).sqrMagnitude < Range * Range) Keep(inn, overlaps);
    }

    private void Keep(Component c, ICollection<Component> overlaps)
    {
        places.Add(c);
        overlapping.Add(overlaps != null && overlaps.Contains(c));
    }

    /// <summary>Draws them (Repaint only), each in its own space.</summary>
    public void Draw()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        for (int i = 0; i < places.Count; i++)
        {
            Component c = places[i];
            if (c == null) continue;
            List<Vector2> area = c is Property p ? p.land : c is Inn inn ? inn.area : null;
            if (area == null || area.Count < 3) continue;
            Transform t = c.transform;
            using (new Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
            {
                if (overlapping[i]) GroundAreaHandles.DrawFill(area, OverlapFill, 0.025f);
                GroundAreaHandles.DrawOutline(area, overlapping[i] ? OverlapLine : Line, overlapping[i] ? 3f : 1.5f, 0.045f);
            }
        }
    }
}
#endif
