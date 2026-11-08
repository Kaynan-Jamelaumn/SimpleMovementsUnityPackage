#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The Property inspector and its Scene view tools: shape the land, the house and the zones on the ground, see what the
/// sizes are worth, list what belongs to the property, and keep the areas truthful.
/// <list type="bullet">
/// <item><b>Land</b>: drag the plot's points on the ground; Shift+click near an edge adds a point, Ctrl/Cmd+click a point removes it.</item>
/// <item><b>House</b>: the same for the house's footprint; the square in its middle moves it.</item>
/// <item><b>Zones</b>: click a zone on the ground (or choose it in the list), then shape it like the land; the square moves it.</item>
/// </list>
/// The Scene view also shows the gardens (brown), the other properties and inns around (grey, red where they overlap this
/// land) and the sizes; the inspector shows the checks of <see cref="HousingValidation"/> every half second.
/// </summary>
[CustomEditor(typeof(Property))]
public class PropertyEditor : Editor
{
    private enum Tool { Select, Land, House, Zones }

    public static readonly Color LandLine = new Color(0.35f, 0.8f, 0.4f, 1f);
    public static readonly Color LandFill = new Color(0.35f, 0.8f, 0.4f, 0.08f);
    public static readonly Color HouseLine = new Color(0.95f, 0.6f, 0.25f, 1f);
    public static readonly Color HouseFill = new Color(0.95f, 0.6f, 0.25f, 0.12f);
    public static readonly Color ZoneLine = new Color(0.35f, 0.6f, 1f, 0.9f);
    public static readonly Color ZoneFill = new Color(0.35f, 0.6f, 1f, 0.14f);
    public static readonly Color GardenLine = new Color(0.62f, 0.42f, 0.2f, 1f);

    private static readonly GUIContent[] ToolNames =
    {
        new GUIContent("Select", "Unity's own tools: pick and move objects."),
        new GUIContent("Land", "Shape the plot in the Scene view."),
        new GUIContent("House", "Shape and move the house's footprint in the Scene view."),
        new GUIContent("Zones", "Shape and move the zones in the Scene view."),
    };
    private static readonly GUIContent SnapLabel = new GUIContent("Snap", "Points snap to a grid of this step (metres) in the property's space.");
    private static readonly GUIContent WidthLabel = new GUIContent("Width", "Its first side, metres.");
    private static readonly GUIContent DepthLabel = new GUIContent("Depth", "Its second side, metres.");
    private static readonly GUIContent AngleLabel = new GUIContent("Angle", "Degrees it is turned, counter-clockwise seen from above.");
    private static readonly GUIContent OffsetXLabel = new GUIContent("Offset X", "Its middle, metres from the property's origin along its X.");
    private static readonly GUIContent OffsetZLabel = new GUIContent("Offset Z", "Its middle, metres from the property's origin along its Z.");
    private static readonly int GroundHash = "PropertyGround".GetHashCode();
    private const int MaxMessages = 12;
    private const double CheckInterval = 0.5;

    private static Tool tool = Tool.Select;
    // Unity's move/rotate/scale gizmo is hidden while a property tool is on and a property is the selected object
    // (decided from shared state alone, so no inspector can leave it hidden).
    private static int liveEditors;
    private static bool snap = true;
    private static float snapStep = 0.25f;
    // The zone the Zones tool edits: by index, on the property it was chosen on.
    private static int zoneIndex = -1;
    private static Property zoneOwner;
    private static bool summaryFold = true, landFold = true, houseFold = true, zonesFold = true, rentFold = true, belongsFold = true;

    private Property property;
    private double nextCheck;
    private bool checkNow = true;
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();
    private string[] districts = Array.Empty<string>();
    private string rent = "", sizes = "";
    private readonly HousingNeighbours neighbours = new HousingNeighbours();
    private readonly List<SoilField> gardens = new List<SoilField>();
    private readonly List<string> gardenLabels = new List<string>();

    // The rectangle fields are the editor's own, taken from the shapes whenever these change; the texts likewise.
    private int landShape = int.MinValue, houseShape = int.MinValue, zonesShape = int.MinValue;
    private Vector2 landCentre, houseOffset, landLabelAt, houseLabelAt;
    private float landWidth, landDepth, landAngle, houseWidth, houseDepth, houseAngle;
    private string landInfo = "", landLabel = "", houseInfo = "", houseLabel = "";
    private readonly List<string> zoneAreas = new List<string>(), zoneLabels = new List<string>();
    private readonly List<Vector2> zoneLabelsAt = new List<Vector2>();

    private void OnEnable()
    {
        property = (Property)target;
        liveEditors++;
        Undo.undoRedoPerformed += OnUndo;
        Selection.selectionChanged += RefreshTransformTool;
        RefreshTransformTool();
    }

    private void OnDisable()
    {
        liveEditors = Mathf.Max(0, liveEditors - 1);
        Selection.selectionChanged -= RefreshTransformTool;
        RefreshTransformTool();
        Undo.undoRedoPerformed -= OnUndo;
    }

    /// <summary>Hides Unity's transform gizmo while a property tool is on and a property is the selected object; else gives it back.</summary>
    private static void RefreshTransformTool()
    {
        GameObject go = Selection.activeGameObject;
        bool want = liveEditors > 0 && tool != Tool.Select && go != null && go.GetComponent<Property>() != null;
        GroundAreaHandles.HideTransformTool(typeof(PropertyEditor), want);
    }

    private static void SetTool(Tool t)
    {
        tool = t;
        RefreshTransformTool();
        SceneView.RepaintAll();
    }

    private void OnUndo()
    {
        checkNow = true;
        Repaint();
        SceneView.RepaintAll();
    }

    private void Changed(Property p)
    {
        if (p != null) EditorUtility.SetDirty(p);
        checkNow = true;
        SceneView.RepaintAll();
        Repaint();
    }

    /// <summary>A change made to the component itself (not through the serialized object): recorded, marked, shown.</summary>
    private void Modify(Property p, string undoName, Action change)
    {
        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(p, undoName);
        change();
        Changed(p);
        serializedObject.Update();
    }

    private static float SnapStep => snap ? snapStep : 0f;

    /// <summary>The zone the Zones tool edits on <paramref name="p"/>, or -1.</summary>
    private static int ZoneOf(Property p) =>
        p != null && p == zoneOwner && p.zones != null && zoneIndex >= 0 && zoneIndex < p.zones.Count && p.zones[zoneIndex] != null ? zoneIndex : -1;

    private static void ChooseZone(Property p, int index)
    {
        zoneOwner = p;
        zoneIndex = index;
        SceneView.RepaintAll();
    }

    // ================================================================== inspector
    public override void OnInspectorGUI()
    {
        Property p = property;
        if (p == null) return;
        serializedObject.Update();
        RefreshTransformTool();
        if (Event.current.type == EventType.Layout)
        {
            Check(false);
            Shapes(p);
        }
        ToolbarGUI();
        SummaryGUI();
        MessagesGUI();
        LandGUI(p);
        HouseGUI(p);
        ZonesGUI(p);
        RentGUI();
        BelongsGUI(p);
        if (serializedObject.ApplyModifiedProperties()) Changed(p);
    }

    private static void ToolbarGUI()
    {
        EditorGUI.BeginChangeCheck();
        var t = (Tool)GUILayout.Toolbar((int)tool, ToolNames, GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck()) SetTool(t);
        EditorGUILayout.HelpBox(ToolHelp(), MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            snap = EditorGUILayout.ToggleLeft(SnapLabel, snap, GUILayout.Width(60));
            using (new EditorGUI.DisabledScope(!snap))
                snapStep = Mathf.Max(0.01f, EditorGUILayout.FloatField(snapStep, GUILayout.Width(50)));
            GUILayout.Label("m", GUILayout.Width(20));
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.Space(4);
    }

    private static string ToolHelp()
    {
        switch (tool)
        {
            case Tool.Land:
                return "Drag the land's points on the ground. Shift+click near an edge to add a point; Ctrl/Cmd+click a point to remove it. " +
                       "(While a tool is on, clicks belong to the property: Select to pick other objects.)";
            case Tool.House:
                return "Drag the house's points; the square in its middle moves it. Shift+click near an edge to add a point; Ctrl/Cmd+click a point to remove it.";
            case Tool.Zones:
                return "Click a zone on the ground (or choose it in the list), then shape it like the land; the square in its middle moves it. + Zone adds one.";
            default:
                return "Pick a tool to shape the property in the Scene view: Land (the plot), House (its footprint), Zones (parts of the land with a purpose). " +
                       "The properties and inns around are grey - filled red when they overlap this land.";
        }
    }

    private static bool Header(bool open, string title) => EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);

    private static float Field(GUIContent label, float value, float labelWidth)
    {
        float old = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = labelWidth;
        value = EditorGUILayout.FloatField(label, value, GUILayout.MinWidth(labelWidth + 34f));
        EditorGUIUtility.labelWidth = old;
        return value;
    }

    private void SummaryGUI()
    {
        summaryFold = Header(summaryFold, "Summary");
        if (!summaryFold) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            SerializedProperty id = serializedObject.FindProperty("propertyId");
            EditorGUILayout.LabelField("Property Id", id.stringValue);
            if (GUILayout.Button(new GUIContent("New Id", "A new id: the property no longer finds its old lease and save (for a duplicated property)."), GUILayout.Width(70)) &&
                EditorUtility.DisplayDialog("New Property Id", "With a new id the property no longer finds its current lease, rent account and saved chests " +
                                                               "(its chests and doors that share their ids with another place's get new ones too). " +
                                                               "Do it for the copy of a duplicated property - the one that is not in the save.", "New Id", "Cancel"))
            {
                serializedObject.ApplyModifiedProperties();
                HousingValidation.RenewIds(property);
                serializedObject.Update();
                Changed(property);
            }
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Name", "Its name in the listing (\"Rose Cottage\")."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        HousingEditors.DistrictField(serializedObject.FindProperty("district"), districts);
        EditorGUILayout.LabelField(sizes, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4);
    }

    private void MessagesGUI()
    {
        int shown = Mathf.Min(errors.Count, MaxMessages);
        for (int i = 0; i < shown; i++) EditorGUILayout.HelpBox(errors[i], MessageType.Error);
        int warned = Mathf.Min(warnings.Count, Mathf.Max(0, MaxMessages - shown));
        for (int i = 0; i < warned; i++) EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        int more = errors.Count - shown + warnings.Count - warned;
        if (more > 0) EditorGUILayout.HelpBox($"…and {more} more (Housing ▸ Validate Housing lists them all).", MessageType.None);
    }

    // ------------------------------------------------------------------ land
    private void LandGUI(Property p)
    {
        landFold = Header(landFold, "Land");
        if (!landFold) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            landWidth = Field(WidthLabel, landWidth, 42f);
            landDepth = Field(DepthLabel, landDepth, 42f);
            landAngle = Field(AngleLabel, landAngle, 42f);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Make Rectangle", "The land becomes a rectangle of this width, depth and angle, where it is.")))
            {
                Vector2 size = new Vector2(Mathf.Max(0.5f, landWidth), Mathf.Max(0.5f, landDepth));
                Vector2 centre = landCentre;
                float angle = landAngle;
                Modify(p, "Land Rectangle", () => p.land = HousingGeometry.Rectangle(centre, size, angle));
            }
            using (new EditorGUI.DisabledScope(p.house == null || p.house.Count < 3))
                if (GUILayout.Button(new GUIContent("Fit Around House", $"The land becomes the house with {HousingEditors.LandMargin:0.#} m around it.")))
                    FitLandAroundHouse(p);
        }
        EditorGUILayout.LabelField(landInfo, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
    }

    private void FitLandAroundHouse(Property p)
    {
        var margin = new Vector2(HousingEditors.LandMargin * 2f, HousingEditors.LandMargin * 2f);
        List<Vector2> land;
        if (GroundAreaHandles.IsRectangle(p.house, out Vector2 c, out Vector2 s, out float a)) land = HousingGeometry.Rectangle(c, s + margin, a);
        else
        {
            Rect b = HousingGeometry.Bounds(p.house);
            land = HousingGeometry.Rectangle(b.center, b.size + margin);
        }
        Modify(p, "Fit Land Around House", () => p.land = land);
    }

    // ------------------------------------------------------------------ house
    private void HouseGUI(Property p)
    {
        houseFold = Header(houseFold, "House");
        if (!houseFold) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            houseWidth = Field(WidthLabel, houseWidth, 42f);
            houseDepth = Field(DepthLabel, houseDepth, 42f);
            houseAngle = Field(AngleLabel, houseAngle, 42f);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            houseOffset.x = Field(OffsetXLabel, houseOffset.x, 56f);
            houseOffset.y = Field(OffsetZLabel, houseOffset.y, 56f);
            if (GUILayout.Button(new GUIContent("Make Rectangle", "The house becomes a rectangle of this width, depth and angle, its middle at the offset."), GUILayout.Width(110)))
            {
                Vector2 size = new Vector2(Mathf.Max(0.5f, houseWidth), Mathf.Max(0.5f, houseDepth));
                Vector2 centre = houseOffset;
                float angle = houseAngle;
                Modify(p, "House Rectangle", () => p.house = HousingGeometry.Rectangle(centre, size, angle));
            }
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("floors"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("floorHeight"));
        EditorGUILayout.LabelField(houseInfo, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
    }

    // ------------------------------------------------------------------ zones
    private void ZonesGUI(Property p)
    {
        int count = p.zones != null ? p.zones.Count : 0;
        zonesFold = Header(zonesFold, $"Zones ({count})");
        if (!zonesFold) return;
        if (count == 0)
            EditorGUILayout.HelpBox("No zones: the land that is neither house nor garden is priced as plain land. Add one for a yard, an orchard, a workshop corner...",
                MessageType.None);
        SerializedProperty list = serializedObject.FindProperty("zones");
        int chosen = ZoneOf(p);
        int removed = -1;
        for (int i = 0; i < count && i < list.arraySize && removed < 0; i++)
        {
            if (p.zones[i] == null) continue;
            SerializedProperty zone = list.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(i == chosen ? "●" : "○", "Edit this zone with the Zones tool."), EditorStyles.label, GUILayout.Width(16)))
                {
                    ChooseZone(p, i);
                    if (tool != Tool.Zones) SetTool(Tool.Zones);
                }
                EditorGUILayout.PropertyField(zone.FindPropertyRelative("name"), GUIContent.none, GUILayout.MinWidth(60));
                EditorGUILayout.PropertyField(zone.FindPropertyRelative("use"), GUIContent.none, GUILayout.Width(88));
                GUILayout.Label(i < zoneAreas.Count ? zoneAreas[i] : "", EditorStyles.miniLabel, GUILayout.Width(56));
                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(56))) removed = i;
            }
        }
        if (removed >= 0)
        {
            Modify(p, "Remove Zone", () => p.zones.RemoveAt(removed));
            if (chosen == removed) ChooseZone(p, -1);
            else if (chosen > removed) ChooseZone(p, chosen - 1);
            GUIUtility.ExitGUI(); // the list changed under the layout: draw it again from the start
        }
        if (GUILayout.Button(new GUIContent("+ Zone", "A 4 × 4 m yard on a free spot of the land (clear of the house, the gardens and the other zones), to shape with the Zones tool."))) AddZone(p);
        EditorGUILayout.Space(4);
    }

    /// <summary>A 4 × 4 m yard (smaller when that does not fit) on a free spot of the land, clear of the house, the gardens and the other zones.</summary>
    private void AddZone(Property p)
    {
        var taken = new List<List<Vector2>>();
        if (p.house != null && p.house.Count >= 3) taken.Add(p.house);
        if (p.zones != null)
            foreach (PropertyZone z in p.zones)
                if (z != null && z.outline != null && z.outline.Count >= 3) taken.Add(z.outline);
        foreach (SoilField f in HousingEditors.GardensOf(p))
            if (f.layout != null && f.layout.outline != null && f.layout.outline.Count >= 3) taken.Add(HousingEditors.Convert(f.layout.outline, f.transform, p.transform));
        Vector2 near = p.house != null && p.house.Count >= 3 ? GroundAreaHandles.Centre(p.house) : GroundAreaHandles.Centre(p.land);
        Vector2 centre = GroundAreaHandles.Centre(p.land), size = new Vector2(2f, 2f);
        bool found = false;
        foreach (Vector2 s in new[] { new Vector2(4f, 4f), new Vector2(3f, 3f), new Vector2(2f, 2f) })
        {
            if (!HousingEditors.FreeSpot(p.land, s, taken, 0.25f, near, out Vector2 c)) continue;
            centre = c;
            size = s;
            found = true;
            break;
        }
        if (!found) Debug.LogWarning($"[Housing] No free spot on the land of '{p.DisplayName}' for a yard: put a 2 × 2 m one in the middle of the land. Move it or make the land bigger.", p);
        string name = "Yard";
        if (p.zones != null)
            for (int n = 2; p.zones.Exists(z => z != null && z.name == name); n++)
                name = $"Yard {n}";
        var zone = new PropertyZone { name = name, use = LandUse.Yard, outline = HousingGeometry.Rectangle(centre, size) };
        Modify(p, "Add Zone", () =>
        {
            if (p.zones == null) p.zones = new List<PropertyZone>();
            p.zones.Add(zone);
        });
        ChooseZone(p, p.zones.Count - 1);
        SetTool(Tool.Zones);
    }

    // ------------------------------------------------------------------ renting
    private void RentGUI()
    {
        rentFold = Header(rentFold, "Renting");
        if (!rentFold) return;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weekly"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("monthly"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("quality"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseRentPerDay"), new GUIContent("Base Rent Per Day",
            "Base rent per day set by hand (0 = from its sizes and Housing Settings). The market moves the actual rent around it."));
        EditorGUILayout.HelpBox(rent, MessageType.None);
        EditorGUILayout.Space(4);
    }

    /// <summary>"Base rent: 12 a day · 84 a week (7 days) · 336 a month (28 days) - from its sizes..." (rounded up).</summary>
    private static string RentText(Property p)
    {
        HousingRules rules = HousingSettings.Instance.Rules;
        double daily = HousingPricing.BaseDaily(p.Facts(), rules);
        int week = rules.Days(LeaseTerm.Week), month = rules.Days(LeaseTerm.Month);
        var sb = new StringBuilder();
        sb.Append("Base rent: ").Append(Up(daily)).Append(" a day");
        if (p.weekly) sb.Append(" · ").Append(Up(daily * week)).Append(" a week (").Append(week).Append(" days)");
        if (p.monthly || !p.weekly) sb.Append(" · ").Append(Up(daily * month)).Append(" a month (").Append(month).Append(" days)");
        sb.Append(p.baseRentPerDay > 0 ? " - set by hand." : $" - from its sizes, its quality and its district ({p.District}).");
        sb.Append(" The market moves what players pay around it.");
        return sb.ToString();
    }

    private static long Up(double v) => (long)Math.Ceiling(v - 1e-6);

    // ------------------------------------------------------------------ what belongs to it
    private void BelongsGUI(Property p)
    {
        belongsFold = Header(belongsFold, "What Belongs To It");
        if (!belongsFold) return;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gardens"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chests"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("beds"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("doors"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("includeChildren"));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Collect From Children", "Lists the Soil Fields, chests, beds and doors among its children (a new id for each new chest).")))
            {
                serializedObject.ApplyModifiedProperties();
                HousingEditors.Collect(p);
                Changed(p);
                serializedObject.Update();
            }
            if (GUILayout.Button(new GUIContent("+ Garden", "A Soil Field child on a free spot of the land beside the house.")))
            {
                serializedObject.ApplyModifiedProperties();
                HousingEditors.AddGarden(p);
                Changed(p);
                serializedObject.Update();
            }
        }
        if (GUILayout.Button(new GUIContent("Make Gardens Networked", "Gives each garden a NetworkObject and a NetSoilField (needs Fish-Net).")))
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.DisplayDialog("Housing", HousingEditors.MakeGardensNetworked(p), "OK");
            GUIUtility.ExitGUI();
        }
    }

    // ================================================================== checks and shapes
    /// <summary>
    /// The checks, the overlaps, the neighbours, the rent, the sizes and the district names: every half second (on Layout),
    /// at once after a change - but while a handle is dragged at most every half second (and once more when it is let go).
    /// </summary>
    private void Check(bool force)
    {
        if (!force && (!checkNow || GUIUtility.hotControl != 0) && EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + CheckInterval;
        checkNow = false;
        Property p = property;
        if (p == null) return;
        errors.Clear();
        warnings.Clear();
        HousingValidation.CheckProperty(p, errors, warnings);
        // Lands overlap only in the open scenes (not in a prefab being edited).
        if (!EditorUtility.IsPersistent(p) && !EditorSceneManager.IsPreviewScene(p.gameObject.scene))
        {
            HousingSceneObjects scene = HousingSceneObjects.Cached();
            HousingOverlaps o = HousingValidation.OverlapsOf(p, scene);
            errors.AddRange(o.errors);
            warnings.AddRange(o.warnings);
            neighbours.Refresh(p, scene, o.places);
        }
        else neighbours.Refresh(null, null, null);
        districts = HousingSettings.Instance.DistrictNames().ToArray();
        rent = RentText(p);
        gardens.Clear();
        gardens.AddRange(HousingEditors.GardensOf(p));
        gardenLabels.Clear();
        foreach (SoilField f in gardens)
            gardenLabels.Add(f.layout != null ? $"{f.DisplayName} · {f.layout.Area():0.#} m²" : f.DisplayName);
        sizes = SizesText(p, gardens);
    }

    /// <summary>"Land 192 m² · house 48 m² footprint, 96 m² of floor area (2 floors) · garden 24 m² · zones: Yard 16 m²".</summary>
    private static string SizesText(Property p, List<SoilField> gardens)
    {
        int floors = Mathf.Max(1, p.floors);
        var sb = new StringBuilder();
        sb.Append($"Land {HousingGeometry.Area(p.land):0.#} m² · house {p.HouseFootprint:0.#} m² footprint, {p.HouseFootprint * floors:0.#} m² of floor area (");
        sb.Append(floors == 1 ? "1 floor)" : $"{floors} floors)");
        float garden = 0f;
        foreach (SoilField f in gardens)
            if (f != null && f.layout != null) garden += f.layout.Area();
        sb.Append(gardens.Count == 0 ? " · no garden" : $" · garden{(gardens.Count == 1 ? "" : "s")} {garden:0.#} m²");
        if (p.zones != null && p.zones.Count > 0)
        {
            sb.Append(" · zones:");
            for (int i = 0; i < p.zones.Count; i++)
            {
                PropertyZone z = p.zones[i];
                if (z == null) continue;
                sb.Append(i == 0 ? " " : ", ").Append(string.IsNullOrWhiteSpace(z.name) ? $"Zone {i + 1}" : z.name.Trim()).Append($" {HousingGeometry.Area(z.outline):0.#} m²");
            }
        }
        return sb.ToString();
    }

    /// <summary>The rectangle fields and the texts of the shapes, made again when a shape changes.</summary>
    private void Shapes(Property p)
    {
        int land = GroundAreaHandles.Hash(p.land);
        if (land != landShape)
        {
            landShape = land;
            Rect b = HousingGeometry.Bounds(p.land);
            if (GroundAreaHandles.IsRectangle(p.land, out Vector2 c, out Vector2 s, out float a))
            {
                landCentre = c;
                landWidth = s.x;
                landDepth = s.y;
                landAngle = a;
            }
            else
            {
                landCentre = b.center;
                landWidth = b.width;
                landDepth = b.height;
                landAngle = 0f;
            }
            float area = HousingGeometry.Area(p.land);
            int points = p.land != null ? p.land.Count : 0;
            landInfo = $"{points} points · {area:0.#} m²";
            landLabel = $"Land {GroundAreaHandles.SizeText(p.land)} · {area:0.#} m²";
            landLabelAt = new Vector2(b.center.x, b.yMin);
        }

        int floors = Mathf.Max(1, p.floors);
        int house = unchecked(GroundAreaHandles.Hash(p.house) * 31 + floors);
        if (house != houseShape)
        {
            houseShape = house;
            Rect b = HousingGeometry.Bounds(p.house);
            if (GroundAreaHandles.IsRectangle(p.house, out Vector2 c, out Vector2 s, out float a))
            {
                houseOffset = c;
                houseWidth = s.x;
                houseDepth = s.y;
                houseAngle = a;
            }
            else
            {
                houseOffset = b.center;
                houseWidth = b.width;
                houseDepth = b.height;
                houseAngle = 0f;
            }
            float footprint = HousingGeometry.Area(p.house);
            string storeys = floors == 1 ? "1 floor" : $"{floors} floors";
            houseInfo = $"{footprint:0.#} m² footprint · {footprint * floors:0.#} m² of floor area ({storeys})";
            houseLabel = $"House {GroundAreaHandles.SizeText(p.house)} · {storeys} · {footprint * floors:0.#} m²";
            houseLabelAt = b.center;
        }

        int zones = 17;
        if (p.zones != null)
            unchecked
            {
                foreach (PropertyZone z in p.zones)
                    zones = zones * 31 + (z == null ? 0 : GroundAreaHandles.Hash(z.outline) * 7 + (z.name ?? "").GetHashCode() * 3 + (int)z.use);
                zones = zones * 31 + p.zones.Count;
            }
        if (zones != zonesShape)
        {
            zonesShape = zones;
            zoneAreas.Clear();
            zoneLabels.Clear();
            zoneLabelsAt.Clear();
            if (p.zones != null)
                for (int i = 0; i < p.zones.Count; i++)
                {
                    PropertyZone z = p.zones[i];
                    float area = z != null ? HousingGeometry.Area(z.outline) : 0f;
                    string name = z == null || string.IsNullOrWhiteSpace(z.name) ? $"Zone {i + 1}" : z.name;
                    zoneAreas.Add($"{area:0.#} m²");
                    zoneLabels.Add(z != null ? $"{name} · {z.use} · {area:0.#} m²" : "");
                    zoneLabelsAt.Add(z != null ? GroundAreaHandles.Centre(z.outline) : Vector2.zero);
                }
        }
    }

    // ================================================================== Scene view
    private void OnSceneGUI()
    {
        var p = target as Property;
        if (p == null) return;
        Event e = Event.current;
        Transform t = p.transform;
        bool primary = p == property;

        // While a tool edits the property, a click on the bare ground is the tool's (adding a point, choosing a zone),
        // not the Scene view's selection; the handles under the mouse still come first. (Select: as usual.)
        int ground = GUIUtility.GetControlID(GroundHash, FocusType.Passive);
        RefreshTransformTool();
        if (tool != Tool.Select && !GroundAreaHandles.Viewing && e.type == EventType.Layout) HandleUtility.AddDefaultControl(ground);
        if (primary && e.type == EventType.Layout)
        {
            Check(false);
            Shapes(p);
        }

        if (e.type == EventType.Repaint)
        {
            if (primary) neighbours.Draw();
            DrawGardens(p, primary);
        }
        float step = SnapStep;
        using (new Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
        {
            if (e.type == EventType.Repaint) DrawShapes(p, primary);
            switch (tool)
            {
                case Tool.Land:
                    if (GroundAreaHandles.EditPoints(p, p.land, 3, "Property Land", LandLine, ground, t, step)) Changed(p);
                    break;
                case Tool.House:
                    if (GroundAreaHandles.EditPoints(p, p.house, 3, "Property House", HouseLine, ground, t, step) |
                        GroundAreaHandles.MoveAll(p, p.house, "Move House", HouseLine, step))
                        Changed(p);
                    break;
                case Tool.Zones:
                    EditZones(p, e, ground, t, step);
                    break;
            }
        }
        // A click the tool took on the ground ends here (with the left button that made it).
        GroundAreaHandles.ReleaseClick(e, ground);
    }

    private void DrawShapes(Property p, bool labels)
    {
        GroundAreaHandles.DrawFill(p.land, LandFill, 0.02f);
        GroundAreaHandles.DrawOutline(p.land, LandLine, tool == Tool.Land ? 4f : 2.5f, 0.04f);
        GroundAreaHandles.DrawFill(p.house, HouseFill, 0.03f);
        GroundAreaHandles.DrawOutline(p.house, HouseLine, tool == Tool.House ? 4f : 2.5f, 0.05f);
        int chosen = ZoneOf(p);
        if (p.zones != null)
            for (int i = 0; i < p.zones.Count; i++)
            {
                PropertyZone z = p.zones[i];
                if (z == null) continue;
                if (i == chosen || tool == Tool.Zones) GroundAreaHandles.DrawFill(z.outline, ZoneFill, 0.035f);
                GroundAreaHandles.DrawOutline(z.outline, ZoneLine, i == chosen ? 3.5f : 1.5f, 0.055f);
            }
        if (!labels) return;
        GroundAreaHandles.Label(landLabelAt, landLabel, 0.1f);
        GroundAreaHandles.Label(houseLabelAt, houseLabel, 0.1f);
        for (int i = 0; i < zoneLabels.Count && i < zoneLabelsAt.Count; i++)
            if (tool == Tool.Zones || i == chosen) GroundAreaHandles.Label(zoneLabelsAt[i], zoneLabels[i], 0.1f);
    }

    /// <summary>The gardens' outlines (brown), each in its own field's space.</summary>
    private void DrawGardens(Property p, bool primary)
    {
        List<SoilField> list = primary ? gardens : p.gardens;
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            SoilField f = list[i];
            if (f == null || f.layout == null) continue;
            Transform ft = f.transform;
            using (new Handles.DrawingScope(Matrix4x4.TRS(ft.position, ft.rotation, Vector3.one)))
            {
                GroundAreaHandles.DrawOutline(f.layout.outline, GardenLine, 2.5f, 0.08f);
                if (primary && i < gardenLabels.Count) GroundAreaHandles.Label(GroundAreaHandles.Centre(f.layout.outline), gardenLabels[i], 0.12f);
            }
        }
    }

    private void EditZones(Property p, Event e, int ground, Transform t, float step)
    {
        // A click on the ground chooses the zone there (Shift+click near the chosen zone's edge adds a point instead,
        // Ctrl/Cmd+click on one of its points removes it).
        if (!e.shift && !EditorGUI.actionKey && GroundAreaHandles.GroundClick(e, ground) && GroundAreaHandles.PlaneHit(t, e, out Vector2 at))
        {
            ChooseZone(p, ZoneAt(p, at));
            GroundAreaHandles.TakeClick(e, ground);
            Repaint();
            return;
        }
        int chosen = ZoneOf(p);
        if (chosen < 0) return;
        PropertyZone zone = p.zones[chosen];
        if (zone.outline == null) return;
        if (GroundAreaHandles.EditPoints(p, zone.outline, 3, "Property Zone", ZoneLine, ground, t, step) |
            GroundAreaHandles.MoveAll(p, zone.outline, "Move Zone", ZoneLine, step))
            Changed(p);
    }

    /// <summary>The smallest zone under a point of the land, or -1.</summary>
    private static int ZoneAt(Property p, Vector2 at)
    {
        int best = -1;
        float bestArea = float.MaxValue;
        if (p.zones == null) return best;
        for (int i = 0; i < p.zones.Count; i++)
        {
            PropertyZone z = p.zones[i];
            if (z == null || z.outline == null || !HousingGeometry.Contains(z.outline, at)) continue;
            float area = HousingGeometry.Area(z.outline);
            if (area < bestArea)
            {
                bestArea = area;
                best = i;
            }
        }
        return best;
    }
}
#endif
