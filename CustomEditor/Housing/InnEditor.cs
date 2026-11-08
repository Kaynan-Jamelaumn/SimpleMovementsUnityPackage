#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The Inn inspector and its Scene view tools: the building's area and its rooms.
/// <list type="bullet">
/// <item><b>Building</b>: drag the area's points on the ground; Shift+click near an edge adds a point, Ctrl/Cmd+click a point removes it.</item>
/// <item><b>Rooms</b>: click inside a room (or choose it in the list), then shape its floor the same way - in the room's own
/// space - and move the whole room (with its bed, chest and door) with the square in its middle.</item>
/// </list>
/// The rooms list says which have a bed and how many chests; + Room makes one on a free spot of the area. The checks of
/// <see cref="HousingValidation.CheckInn"/> and the properties and inns overlapping it show every half second.
/// </summary>
[CustomEditor(typeof(Inn))]
public class InnEditor : Editor
{
    private enum Tool { Select, Building, Rooms }

    public static readonly Color AreaLine = new Color(0.8f, 0.55f, 0.95f, 1f);
    public static readonly Color AreaFill = new Color(0.8f, 0.55f, 0.95f, 0.07f);
    public static readonly Color RoomLine = new Color(0.95f, 0.8f, 0.4f, 1f);
    public static readonly Color RoomFill = new Color(0.95f, 0.8f, 0.4f, 0.14f);

    private static readonly GUIContent[] ToolNames =
    {
        new GUIContent("Select", "Unity's own tools: pick and move objects."),
        new GUIContent("Building", "Shape the inn's area (the building's footprint) in the Scene view."),
        new GUIContent("Rooms", "Shape and move the rooms in the Scene view."),
    };
    private static readonly GUIContent SnapLabel = new GUIContent("Snap", "Points snap to a grid of this step (metres), in the space of what is edited.");
    private static readonly int GroundHash = "InnGround".GetHashCode();
    private const int MaxMessages = 12;
    private const double CheckInterval = 0.5;

    private static Tool tool = Tool.Select;
    private static int liveEditors;
    private static bool snap = true;
    private static float snapStep = 0.25f;
    // The room the Rooms tool edits, and the inn it was chosen on.
    private static InnRoom chosenRoom;
    private static Inn chosenInn;
    private static bool summaryFold = true, areaFold = true, roomsFold = true;

    /// <summary>A line of the rooms list (and its label in the Scene view), made every half second.</summary>
    private struct Row
    {
        public InnRoom room;
        public string info;
        public string label;
        public Vector2 labelAt;
    }

    private Inn inn;
    private double nextCheck;
    private bool checkNow = true;
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();
    private string[] districts = Array.Empty<string>();
    private readonly HousingNeighbours neighbours = new HousingNeighbours();
    private readonly List<Row> rows = new List<Row>();
    private int areaShape = int.MinValue;
    private Vector2 areaCentre, areaLabelAt;
    private float areaWidth, areaDepth, areaAngle;
    private string areaInfo = "", areaLabel = "";

    private void OnEnable()
    {
        inn = (Inn)target;
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

    /// <summary>Hides Unity's transform gizmo while an inn tool is on and an inn is the selected object; else gives it back.</summary>
    private static void RefreshTransformTool()
    {
        GameObject go = Selection.activeGameObject;
        bool want = liveEditors > 0 && tool != Tool.Select && go != null && go.GetComponent<Inn>() != null;
        GroundAreaHandles.HideTransformTool(typeof(InnEditor), want);
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

    private void Changed(UnityEngine.Object o)
    {
        if (o != null) EditorUtility.SetDirty(o);
        checkNow = true;
        SceneView.RepaintAll();
        Repaint();
    }

    private static float SnapStep => snap ? snapStep : 0f;

    /// <summary>The room the Rooms tool edits on <paramref name="i"/>, or null.</summary>
    private static InnRoom RoomOf(Inn i) => chosenInn == i && chosenRoom != null ? chosenRoom : null;

    private static void Choose(Inn i, InnRoom room)
    {
        chosenInn = i;
        chosenRoom = room;
        SceneView.RepaintAll();
    }

    // ================================================================== inspector
    public override void OnInspectorGUI()
    {
        Inn i = inn;
        if (i == null) return;
        serializedObject.Update();
        RefreshTransformTool();
        if (Event.current.type == EventType.Layout)
        {
            Check(false);
            Shape(i);
        }
        ToolbarGUI();
        SummaryGUI();
        MessagesGUI();
        AreaGUI(i);
        RoomsGUI(i);
        if (serializedObject.ApplyModifiedProperties()) Changed(i);
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
            case Tool.Building:
                return "Drag the area's points on the ground. Shift+click near an edge to add a point; Ctrl/Cmd+click a point to remove it. " +
                       "(While a tool is on, clicks belong to the inn: Select to pick other objects.)";
            case Tool.Rooms:
                return "Click inside a room (or choose it in the list), then shape its floor like the area; the square in its middle moves the whole room " +
                       "with its bed, chest and door.";
            default:
                return "Pick a tool to shape the inn in the Scene view: Building (its footprint) or Rooms. The properties and inns around are grey - filled red when they overlap it.";
        }
    }

    internal static bool Header(bool open, string title) => EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);

    internal static float Field(string label, string tooltip, float value)
    {
        float old = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 42f;
        value = EditorGUILayout.FloatField(new GUIContent(label, tooltip), value, GUILayout.MinWidth(76f));
        EditorGUIUtility.labelWidth = old;
        return value;
    }

    private void SummaryGUI()
    {
        summaryFold = Header(summaryFold, "Summary");
        if (!summaryFold) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            SerializedProperty id = serializedObject.FindProperty("innId");
            EditorGUILayout.LabelField("Inn Id", id.stringValue);
            if (GUILayout.Button(new GUIContent("New Id", "A new id: the inn no longer finds its guests' stays and save (for a duplicated inn)."), GUILayout.Width(70)) &&
                EditorUtility.DisplayDialog("New Inn Id", "With a new id the inn no longer finds its guests' stays and saved chests. Do it for a duplicated inn " +
                                                          "that shares another's id.", "New Id", "Cancel"))
                id.stringValue = Property.NewId();
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Name", "Its name (\"The Gilded Goose\")."));
        HousingEditors.DistrictField(serializedObject.FindProperty("district"), districts);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nightlyPrice"));
        EditorGUILayout.Space(4);
    }

    private void MessagesGUI()
    {
        int shown = Mathf.Min(errors.Count, MaxMessages);
        for (int k = 0; k < shown; k++) EditorGUILayout.HelpBox(errors[k], MessageType.Error);
        int warned = Mathf.Min(warnings.Count, Mathf.Max(0, MaxMessages - shown));
        for (int k = 0; k < warned; k++) EditorGUILayout.HelpBox(warnings[k], MessageType.Warning);
        int more = errors.Count - shown + warnings.Count - warned;
        if (more > 0) EditorGUILayout.HelpBox($"…and {more} more (Housing ▸ Validate Housing lists them all).", MessageType.None);
    }

    private void AreaGUI(Inn i)
    {
        areaFold = Header(areaFold, "Area (the building)");
        if (!areaFold) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            areaWidth = Field("Width", "Its first side, metres.", areaWidth);
            areaDepth = Field("Depth", "Its second side, metres.", areaDepth);
            areaAngle = Field("Angle", "Degrees it is turned, counter-clockwise seen from above.", areaAngle);
        }
        if (GUILayout.Button(new GUIContent("Make Rectangle", "The area becomes a rectangle of this width, depth and angle, where it is.")))
        {
            var size = new Vector2(Mathf.Max(1f, areaWidth), Mathf.Max(1f, areaDepth));
            Vector2 centre = areaCentre;
            float angle = areaAngle;
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(i, "Inn Area Rectangle");
            i.area = HousingGeometry.Rectangle(centre, size, angle);
            Changed(i);
            serializedObject.Update();
        }
        EditorGUILayout.LabelField(areaInfo, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
    }

    private void RoomsGUI(Inn i)
    {
        roomsFold = Header(roomsFold, $"Rooms ({rows.Count})");
        if (!roomsFold) return;
        if (rows.Count == 0)
            EditorGUILayout.HelpBox("No rooms yet: + Room adds one (a child with an Inn Room, a bed, a chest and a door).", MessageType.None);
        else
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20f);
                GUILayout.Label("Name", EditorStyles.miniLabel, GUILayout.MinWidth(60));
                GUILayout.Label(new GUIContent("Quality", "Multiplies the nightly price (1 = an ordinary room)."), EditorStyles.miniLabel, GUILayout.Width(42));
                GUILayout.Label("", EditorStyles.miniLabel, GUILayout.Width(96));
                GUILayout.Space(52f);
            }
        InnRoom chosen = RoomOf(i);
        InnRoom select = null;
        foreach (Row row in rows)
        {
            InnRoom r = row.room;
            if (r == null) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(r == chosen ? "●" : "○", "Edit this room with the Rooms tool."), EditorStyles.label, GUILayout.Width(16)))
                {
                    Choose(i, r);
                    if (tool != Tool.Rooms) SetTool(Tool.Rooms);
                }
                EditorGUI.BeginChangeCheck();
                string n = EditorGUILayout.DelayedTextField(r.displayName, GUILayout.MinWidth(60));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(r, "Rename Inn Room");
                    r.displayName = n;
                    Changed(r);
                }
                EditorGUI.BeginChangeCheck();
                float q = EditorGUILayout.FloatField(r.quality, GUILayout.Width(42));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(r, "Inn Room Quality");
                    r.quality = Mathf.Clamp(q, 0.25f, 5f);
                    Changed(r);
                }
                GUILayout.Label(row.info, EditorStyles.miniLabel, GUILayout.Width(96));
                if (GUILayout.Button(new GUIContent("Select", "Selects the room's object (its own inspector)."), EditorStyles.miniButton, GUILayout.Width(50))) select = r;
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("+ Room", "A 4 × 4 m room with a bed, a chest and a door, on a free spot of the area.")))
            {
                serializedObject.ApplyModifiedProperties();
                InnRoom made = HousingEditors.AddRoom(i, false);
                Choose(i, made);
                SetTool(Tool.Rooms);
                Changed(i);
                serializedObject.Update();
            }
            if (GUILayout.Button(new GUIContent("Collect From Children", "Lists the Inn Rooms among its children.")))
            {
                serializedObject.ApplyModifiedProperties();
                HousingEditors.Collect(i);
                Changed(i);
                serializedObject.Update();
            }
        }
        if (select != null)
        {
            Selection.activeGameObject = select.gameObject;
            EditorGUIUtility.PingObject(select.gameObject);
            GUIUtility.ExitGUI(); // another inspector takes over
        }
    }

    // ================================================================== checks and shapes
    private void Check(bool force)
    {
        // At once after a change, but while a handle is dragged at most every half second (and once more when it is let go).
        if (!force && (!checkNow || GUIUtility.hotControl != 0) && EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + CheckInterval;
        checkNow = false;
        Inn i = inn;
        if (i == null) return;
        errors.Clear();
        warnings.Clear();
        HousingValidation.CheckInn(i, errors, warnings);
        if (!EditorUtility.IsPersistent(i) && !EditorSceneManager.IsPreviewScene(i.gameObject.scene))
        {
            HousingSceneObjects scene = HousingSceneObjects.Cached();
            HousingOverlaps o = HousingValidation.OverlapsOf(i, scene);
            errors.AddRange(o.errors);
            warnings.AddRange(o.warnings);
            neighbours.Refresh(i, scene, o.places);
        }
        else neighbours.Refresh(null, null, null);
        districts = HousingSettings.Instance.DistrictNames().ToArray();
        rows.Clear();
        foreach (InnRoom r in HousingEditors.RoomsOf(i)) rows.Add(MakeRow(r));
    }

    private static Row MakeRow(InnRoom r)
    {
        bool bed = r.bed != null || r.GetComponentInChildren<SleepSpot>(true) != null;
        var chests = new HashSet<Storage>();
        if (r.chests != null)
            foreach (HousingChest c in r.chests)
                if (c != null && c.storage != null) chests.Add(c.storage);
        foreach (Storage s in r.GetComponentsInChildren<Storage>(true)) chests.Add(s);
        return new Row
        {
            room = r,
            info = $"{(bed ? "Bed" : "No bed")} · {chests.Count} chest{(chests.Count == 1 ? "" : "s")}",
            label = $"{r.DisplayName} · {HousingGeometry.Area(r.area):0.#} m²",
            labelAt = GroundAreaHandles.Centre(r.area),
        };
    }

    private void Shape(Inn i)
    {
        int h = GroundAreaHandles.Hash(i.area);
        if (h == areaShape) return;
        areaShape = h;
        Rect b = HousingGeometry.Bounds(i.area);
        if (GroundAreaHandles.IsRectangle(i.area, out Vector2 c, out Vector2 s, out float a))
        {
            areaCentre = c;
            areaWidth = s.x;
            areaDepth = s.y;
            areaAngle = a;
        }
        else
        {
            areaCentre = b.center;
            areaWidth = b.width;
            areaDepth = b.height;
            areaAngle = 0f;
        }
        float area = HousingGeometry.Area(i.area);
        areaInfo = $"{(i.area != null ? i.area.Count : 0)} points · {area:0.#} m²";
        areaLabel = $"{i.DisplayName} {GroundAreaHandles.SizeText(i.area)} · {area:0.#} m²";
        areaLabelAt = new Vector2(b.center.x, b.yMin);
    }

    // ================================================================== Scene view
    private void OnSceneGUI()
    {
        var i = target as Inn;
        if (i == null) return;
        Event e = Event.current;
        Transform t = i.transform;
        bool primary = i == inn;
        int ground = GUIUtility.GetControlID(GroundHash, FocusType.Passive);
        RefreshTransformTool();
        if (tool != Tool.Select && !GroundAreaHandles.Viewing && e.type == EventType.Layout) HandleUtility.AddDefaultControl(ground);
        if (primary && e.type == EventType.Layout)
        {
            Check(false);
            Shape(i);
        }
        if (primary && e.type == EventType.Repaint) neighbours.Draw();

        float step = SnapStep;
        using (new Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
        {
            if (e.type == EventType.Repaint)
            {
                GroundAreaHandles.DrawFill(i.area, AreaFill, 0.02f);
                GroundAreaHandles.DrawOutline(i.area, AreaLine, tool == Tool.Building ? 4f : 2.5f, 0.04f);
                if (primary) GroundAreaHandles.Label(areaLabelAt, areaLabel, 0.1f);
            }
            if (tool == Tool.Building && GroundAreaHandles.EditPoints(i, i.area, 3, "Inn Area", AreaLine, ground, t, step)) Changed(i);
        }

        InnRoom chosen = RoomOf(i);
        if (e.type == EventType.Repaint)
        {
            if (primary)
                foreach (Row row in rows) DrawRoom(row.room, row.room == chosen, row.label, row.labelAt);
            else if (i.rooms != null)
                foreach (InnRoom r in i.rooms) DrawRoom(r, r == chosen, null, default);
        }
        if (tool == Tool.Rooms) EditRooms(i, e, ground, chosen, step);
        GroundAreaHandles.ReleaseClick(e, ground);
    }

    /// <summary>A room's floor in its own space: an outline, filled when it is the chosen one, with its label.</summary>
    internal static void DrawRoom(InnRoom r, bool chosen, string label, Vector2 labelAt)
    {
        if (r == null || r.area == null) return;
        Transform rt = r.transform;
        using (new Handles.DrawingScope(Matrix4x4.TRS(rt.position, rt.rotation, Vector3.one)))
        {
            if (chosen) GroundAreaHandles.DrawFill(r.area, RoomFill, 0.03f);
            GroundAreaHandles.DrawOutline(r.area, RoomLine, chosen ? 3.5f : 1.5f, 0.06f);
            if (label != null) GroundAreaHandles.Label(labelAt, label, 0.12f);
        }
    }

    private void EditRooms(Inn i, Event e, int ground, InnRoom chosen, float step)
    {
        // A click on the ground chooses the room there (the nearest floor under the mouse: rooms on upper floors first).
        if (!e.shift && !EditorGUI.actionKey && GroundAreaHandles.GroundClick(e, ground))
        {
            Choose(i, RoomUnderMouse(i, e));
            GroundAreaHandles.TakeClick(e, ground);
            Repaint();
            return;
        }
        if (chosen == null || chosen.area == null) return;
        Transform rt = chosen.transform;
        using (new Handles.DrawingScope(Matrix4x4.TRS(rt.position, rt.rotation, Vector3.one)))
            if (GroundAreaHandles.EditPoints(chosen, chosen.area, 3, "Inn Room Area", RoomLine, ground, rt, step)) Changed(chosen);
        Vector3 at = chosen.World(GroundAreaHandles.Centre(chosen.area), GroundAreaHandles.HandleLift);
        if (GroundAreaHandles.MoveObject(rt, at, i.transform, RoomLine, step, "Move Inn Room")) checkNow = true;
    }

    /// <summary>The room whose floor is under the mouse (the nearest along the mouse ray), or null.</summary>
    internal static InnRoom RoomUnderMouse(Inn i, Event e)
    {
        InnRoom best = null;
        float bestD = float.MaxValue;
        foreach (InnRoom r in HousingEditors.RoomsOf(i))
        {
            if (r.area == null || r.area.Count < 3) continue;
            if (!GroundAreaHandles.PlaneHit(r.transform, e, out Vector2 local, out float d) || d >= bestD) continue;
            if (!HousingGeometry.Contains(r.area, local)) continue;
            best = r;
            bestD = d;
        }
        return best;
    }
}

/// <summary>
/// The Inn Room inspector and its Scene view tool: <b>Area</b> shapes the room's floor (in its own space: drag its points;
/// Shift+click near an edge adds one, Ctrl/Cmd+click one removes it) and the square in its middle moves the room with its
/// bed, chest and door. Shows its inn (and the inn's other rooms in the Scene view) and its part of the inn's checks.
/// </summary>
[CustomEditor(typeof(InnRoom))]
public class InnRoomEditor : Editor
{
    private enum Tool { Select, Area }

    private static readonly GUIContent[] ToolNames =
    {
        new GUIContent("Select", "Unity's own tools: pick and move objects."),
        new GUIContent("Area", "Shape and move the room's floor in the Scene view."),
    };
    private static readonly GUIContent SnapLabel = new GUIContent("Snap", "Points snap to a grid of this step (metres), in the room's space.");
    private static readonly Color OtherRoom = new Color(0.95f, 0.8f, 0.4f, 0.45f);
    private static readonly int GroundHash = "InnRoomGround".GetHashCode();
    private const double CheckInterval = 0.5;

    private static Tool tool = Tool.Select;
    private static int liveEditors;
    private static bool snap = true;
    private static float snapStep = 0.25f;

    private InnRoom room;
    private Inn inn;
    private double nextCheck;
    private bool checkNow = true;
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();
    private readonly List<InnRoom> others = new List<InnRoom>();
    private int areaShape = int.MinValue;
    private Vector2 areaCentre, labelAt;
    private float areaWidth, areaDepth, areaAngle;
    private string areaInfo = "", label = "";

    private void OnEnable()
    {
        room = (InnRoom)target;
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

    /// <summary>Hides Unity's transform gizmo while the Area tool is on and a room is the selected object; else gives it back.</summary>
    private static void RefreshTransformTool()
    {
        GameObject go = Selection.activeGameObject;
        bool want = liveEditors > 0 && tool != Tool.Select && go != null && go.GetComponent<InnRoom>() != null;
        GroundAreaHandles.HideTransformTool(typeof(InnRoomEditor), want);
    }

    private void OnUndo()
    {
        checkNow = true;
        Repaint();
        SceneView.RepaintAll();
    }

    private void Changed(UnityEngine.Object o)
    {
        if (o != null) EditorUtility.SetDirty(o);
        checkNow = true;
        SceneView.RepaintAll();
        Repaint();
    }

    // ================================================================== inspector
    public override void OnInspectorGUI()
    {
        InnRoom r = room;
        if (r == null) return;
        serializedObject.Update();
        RefreshTransformTool();
        if (Event.current.type == EventType.Layout)
        {
            Check(false);
            Shape(r);
        }

        EditorGUI.BeginChangeCheck();
        var t = (Tool)GUILayout.Toolbar((int)tool, ToolNames, GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck())
        {
            tool = t;
            RefreshTransformTool();
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox(tool == Tool.Area
            ? "Drag the floor's points on the ground; the square in its middle moves the room with its bed, chest and door. Shift+click near an edge to " +
              "add a point; Ctrl/Cmd+click a point to remove it."
            : "Pick Area to shape the room's floor in the Scene view (its inn's area is purple, its other rooms pale).", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            snap = EditorGUILayout.ToggleLeft(SnapLabel, snap, GUILayout.Width(60));
            using (new EditorGUI.DisabledScope(!snap))
                snapStep = Mathf.Max(0.01f, EditorGUILayout.FloatField(snapStep, GUILayout.Width(50)));
            GUILayout.Label("m", GUILayout.Width(20));
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.Space(4);

        bool selectInn = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(new GUIContent("Inn", "The inn that lets this room (the Inn above it)."), inn, typeof(Inn), true);
            using (new EditorGUI.DisabledScope(inn == null))
                selectInn = GUILayout.Button(new GUIContent("Select", "Selects the inn (its own inspector)."), GUILayout.Width(60));
        }
        if (selectInn && inn != null)
        {
            Selection.activeGameObject = inn.gameObject;
            GUIUtility.ExitGUI(); // another inspector takes over
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            SerializedProperty id = serializedObject.FindProperty("roomId");
            EditorGUILayout.LabelField("Room Id", id.stringValue);
            if (GUILayout.Button(new GUIContent("New Id", "A new id: the room no longer finds its guest's stay and save (for a duplicated room)."), GUILayout.Width(70)) &&
                EditorUtility.DisplayDialog("New Room Id", "With a new id the room no longer finds its guest's stay and saved chest. Do it for a duplicated room " +
                                                           "that shares another's id.", "New Id", "Cancel"))
                id.stringValue = Property.NewId();
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Name", "Its name (\"Room 3\", \"The Blue Room\")."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("quality"));

        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Floor", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            areaWidth = InnEditor.Field("Width", "Its first side, metres.", areaWidth);
            areaDepth = InnEditor.Field("Depth", "Its second side, metres.", areaDepth);
            areaAngle = InnEditor.Field("Angle", "Degrees it is turned, counter-clockwise seen from above.", areaAngle);
        }
        if (GUILayout.Button(new GUIContent("Make Rectangle", "The floor becomes a rectangle of this width, depth and angle, where it is.")))
        {
            var size = new Vector2(Mathf.Max(0.5f, areaWidth), Mathf.Max(0.5f, areaDepth));
            Vector2 centre = areaCentre;
            float angle = areaAngle;
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(r, "Inn Room Rectangle");
            r.area = HousingGeometry.Rectangle(centre, size, angle);
            Changed(r);
            serializedObject.Update();
        }
        EditorGUILayout.LabelField(areaInfo, EditorStyles.miniLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("What Belongs To It", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chests"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("doors"), true);
        if (GUILayout.Button(new GUIContent("Collect From Children", "Finds its bed and lists the chests and doors among its children (a new id for each new chest).")))
        {
            serializedObject.ApplyModifiedProperties();
            HousingEditors.Collect(r);
            Changed(r);
            serializedObject.Update();
        }
        if (serializedObject.ApplyModifiedProperties()) Changed(r);
    }

    private void Check(bool force)
    {
        // At once after a change, but while a handle is dragged at most every half second (and once more when it is let go).
        if (!force && (!checkNow || GUIUtility.hotControl != 0) && EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + CheckInterval;
        checkNow = false;
        InnRoom r = room;
        if (r == null) return;
        errors.Clear();
        warnings.Clear();
        HousingValidation.CheckRoom(r, errors, warnings);
        inn = r.GetComponentInParent<Inn>(true);
        others.Clear();
        if (inn != null)
            foreach (InnRoom o in HousingEditors.RoomsOf(inn))
                if (o != r) others.Add(o);
    }

    private void Shape(InnRoom r)
    {
        int h = unchecked(GroundAreaHandles.Hash(r.area) * 31 + (r.displayName ?? "").GetHashCode());
        if (h == areaShape) return;
        areaShape = h;
        Rect b = HousingGeometry.Bounds(r.area);
        if (GroundAreaHandles.IsRectangle(r.area, out Vector2 c, out Vector2 s, out float a))
        {
            areaCentre = c;
            areaWidth = s.x;
            areaDepth = s.y;
            areaAngle = a;
        }
        else
        {
            areaCentre = b.center;
            areaWidth = b.width;
            areaDepth = b.height;
            areaAngle = 0f;
        }
        float area = HousingGeometry.Area(r.area);
        areaInfo = $"{(r.area != null ? r.area.Count : 0)} points · {area:0.#} m²";
        label = $"{r.DisplayName} {GroundAreaHandles.SizeText(r.area)} · {area:0.#} m²";
        labelAt = b.center;
    }

    // ================================================================== Scene view
    private void OnSceneGUI()
    {
        var r = target as InnRoom;
        if (r == null) return;
        Event e = Event.current;
        bool primary = r == room;
        int ground = GUIUtility.GetControlID(GroundHash, FocusType.Passive);
        RefreshTransformTool();
        if (tool != Tool.Select && !GroundAreaHandles.Viewing && e.type == EventType.Layout) HandleUtility.AddDefaultControl(ground);
        if (primary && e.type == EventType.Layout)
        {
            Check(false);
            Shape(r);
        }

        if (e.type == EventType.Repaint && primary)
        {
            if (inn != null && inn.area != null)
                using (new Handles.DrawingScope(Matrix4x4.TRS(inn.transform.position, inn.transform.rotation, Vector3.one)))
                    GroundAreaHandles.DrawOutline(inn.area, InnEditor.AreaLine, 2f, 0.04f);
            foreach (InnRoom o in others)
                if (o != null && o.area != null)
                    using (new Handles.DrawingScope(Matrix4x4.TRS(o.transform.position, o.transform.rotation, Vector3.one)))
                        GroundAreaHandles.DrawOutline(o.area, OtherRoom, 1.5f, 0.05f);
        }

        Transform t = r.transform;
        using (new Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
        {
            if (e.type == EventType.Repaint)
            {
                GroundAreaHandles.DrawFill(r.area, InnEditor.RoomFill, 0.03f);
                GroundAreaHandles.DrawOutline(r.area, InnEditor.RoomLine, tool == Tool.Area ? 4f : 2.5f, 0.06f);
                if (primary) GroundAreaHandles.Label(labelAt, label, 0.12f);
            }
            if (tool == Tool.Area && GroundAreaHandles.EditPoints(r, r.area, 3, "Inn Room Area", InnEditor.RoomLine, ground, t, snap ? snapStep : 0f)) Changed(r);
        }
        if (tool == Tool.Area && r.area != null && r.area.Count > 0)
        {
            Transform plane = inn != null && primary ? inn.transform : t;
            Vector3 at = r.World(GroundAreaHandles.Centre(r.area), GroundAreaHandles.HandleLift);
            if (GroundAreaHandles.MoveObject(t, at, plane, InnEditor.RoomLine, snap ? snapStep : 0f, "Move Inn Room")) checkNow = true;
        }
        GroundAreaHandles.ReleaseClick(e, ground);
    }
}
#endif
