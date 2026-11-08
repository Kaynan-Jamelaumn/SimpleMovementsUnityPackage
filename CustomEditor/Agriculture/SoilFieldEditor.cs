#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Soil Field inspector and its Scene view tools: shape the field and say what its soil is, part by part.
/// <list type="bullet">
/// <item><b>Outline</b>: drag the points on the ground; Shift+click an edge adds a point; Ctrl/Cmd+click a point removes it.</item>
/// <item><b>Sections</b>: parts whose soil differs - rectangles, circles, polygons or painted cells, with soft edges. Pick
/// one in the list, then move, size and turn it in the Scene view.</item>
/// <item><b>Paint</b>: brush cells into a section (or back to the base soil, or erase) - for the irregular corners no
/// shape fits.</item>
/// <item><b>Preview</b>: the baked soil drawn over the field, by soil type, by section or by any property (pH, each
/// nutrient, organic matter, salinity, water, compaction, magic, stones, fertility...), with the value under the mouse.
/// It is exactly the soil the game starts with.</item>
/// </list>
/// </summary>
[CustomEditor(typeof(SoilField))]
public class SoilFieldEditor : Editor
{
    private enum Tool { Select, Outline, Sections, Paint }

    private static Tool tool = Tool.Select;
    private static int preview = 1;
    private static float opacity = 0.75f;
    private static float brush = 0.6f;
    // The selected section and the brush follow their sections by id (undo, redo and reordering cannot point them at
    // another section; a brush whose section is gone erases) and belong to the field they were chosen on.
    private static int selectedUid, brushUid, brushKind = 1; // brush kind: 0 erase, 1 a section, 2 the base soil
    private static SoilField selectionField, brushField;
    // Unity's move/rotate/scale gizmo is hidden while a field tool is on and a field is the selected object (it sits at
    // the field's middle, under the brush) - decided from shared state alone, so no inspector can leave it hidden.
    private static int liveEditors;
    private static bool gizmoHidden, toolsWereHidden;
    private static bool snap = true;
    private static float snapStep = 0.25f;
    private static bool outlineFold, baseFold = true, sectionsFold = true, paintFold, variationFold, settingsFold, runtimeFold = true;
    private static readonly HashSet<int> openSections = new HashSet<int>();

    private static readonly string[] PreviewNames =
    {
        "Off", "Soil type", "Sections", "pH", "Nitrogen (N)", "Phosphorus (P)", "Potassium (K)", "Calcium (Ca)", "Magnesium (Mg)", "Sulfur (S)",
        "Iron (Fe)", "Zinc (Zn)", "Copper (Cu)", "Manganese (Mn)", "Boron (B)", "Molybdenum (Mo)", "Organic matter", "Salinity", "Water retention",
        "Drainage", "Compaction", "Moisture", "Magical essence", "Pathogens", "Stones", "Tilth", "Fertility", "Irrigation",
    };

    private static readonly string[] ChannelFields =
    {
        "ph", "nitrogen", "phosphorus", "potassium", "calcium", "magnesium", "sulfur", "iron", "zinc", "copper", "manganese", "boron",
        "molybdenum", "organicMatter", "salinity", "waterRetention", "drainage", "compaction", "moisture", "magicEssence", "pathogens", "stones", "tilth",
    };

    private SoilField field;
    private SoilGrid grid;
    private int gridHash = int.MinValue, texHash = int.MinValue, texMode = -1;
    private Texture2D texture;
    private Material material;
    private Mesh quad;
    private int version;
    private double nextCheck;
    private bool painting;
    private int paintGroup;
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();

    private void OnEnable()
    {
        field = (SoilField)target;
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
        if (texture != null) DestroyImmediate(texture);
        if (material != null) DestroyImmediate(material);
        if (quad != null) DestroyImmediate(quad);
    }

    /// <summary>Hides Unity's transform gizmo while a field tool is on and a soil field is the selected object; else gives it back.</summary>
    private static void RefreshTransformTool()
    {
        GameObject go = Selection.activeGameObject;
        bool want = liveEditors > 0 && tool != Tool.Select && go != null && go.GetComponent<SoilField>() != null;
        if (want && !gizmoHidden)
        {
            toolsWereHidden = Tools.hidden;
            Tools.hidden = true;
            gizmoHidden = true;
        }
        else if (!want && gizmoHidden)
        {
            Tools.hidden = toolsWereHidden;
            gizmoHidden = false;
        }
    }

    private void OnUndo()
    {
        version++;
        Repaint();
        SceneView.RepaintAll();
    }

    private void Changed()
    {
        version++;
        EditorUtility.SetDirty(field);
        SceneView.RepaintAll();
    }

    private void Record(string what) => Undo.RecordObject(field, what);

    private int UidAt(int index)
    {
        List<SoilSection> list = field != null && field.layout != null ? field.layout.sections : null;
        return list != null && index >= 0 && index < list.Count && list[index] != null ? list[index].uid : 0;
    }

    /// <summary>The selected section's index (-1 = none, or a selection made on another field).</summary>
    private int selected
    {
        get => field != null && field == selectionField && field.layout != null ? field.layout.IndexOfUid(selectedUid) : -1;
        set
        {
            selectionField = field;
            selectedUid = UidAt(value);
        }
    }

    /// <summary>What the brush paints: 0 erase, section index + 1, or <see cref="SoilLayout.PaintBase"/>.</summary>
    private int brushValue
    {
        get
        {
            // A brush chosen on another field erases here.
            if (field == null || field != brushField || brushKind == 0) return 0;
            if (brushKind == 2) return SoilLayout.PaintBase;
            int i = field.layout != null ? field.layout.IndexOfUid(brushUid) : -1;
            return i >= 0 ? i + 1 : 0;
        }
        set
        {
            brushField = field;
            if (value == SoilLayout.PaintBase)
            {
                brushKind = 2;
                brushUid = 0;
            }
            else
            {
                brushUid = value > 0 ? UidAt(value - 1) : 0;
                brushKind = brushUid != 0 ? 1 : 0;
            }
        }
    }

    /// <summary>Sections made by code or copied get their ids (the brush and the selection need them).</summary>
    private void EnsureUids()
    {
        if (field != null && field.layout != null && field.layout.EnsureSectionUids()) EditorUtility.SetDirty(field);
    }

    /// <summary>The Scene view's hand: panning and orbiting are never the field's.</summary>
    private static bool Viewing => Tools.viewToolActive || Tools.current == UnityEditor.Tool.View;

    // ================================================================== inspector
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SoilLayout l = field.layout ??= new SoilLayout();
        EnsureUids();
        RefreshTransformTool();
        Summary(l);

        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        Tool t = (Tool)GUILayout.Toolbar((int)tool, new[] { "Select", "Outline", "Sections", "Paint" }, GUILayout.Height(24));
        if (EditorGUI.EndChangeCheck())
        {
            tool = t;
            RefreshTransformTool();
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox(ToolHelp(), MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            preview = EditorGUILayout.Popup("Preview", preview, PreviewNames);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        }
        EditorGUI.BeginChangeCheck();
        opacity = EditorGUILayout.Slider("Opacity", opacity, 0.1f, 1f);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        EditorGUILayout.Space(6);
        OutlineGUI(l);
        BaseGUI();
        SectionsGUI(l);
        PaintGUI(l);
        VariationGUI();
        SettingsGUI();
        RuntimeGUI();

        if (serializedObject.ApplyModifiedProperties()) Changed();
    }

    private void Summary(SoilLayout l)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Name"));
        using (new EditorGUILayout.HorizontalScope())
        {
            SerializedProperty id = serializedObject.FindProperty("fieldId");
            EditorGUILayout.LabelField("Field Id", id.stringValue);
            if (GUILayout.Button(new GUIContent("New Id", "A new id: the field no longer finds its old save."), GUILayout.Width(70)))
                id.stringValue = Guid.NewGuid().ToString("N").Substring(0, 12);
        }
        l.Geometry(out int w, out int h, out float cell, out _);
        SoilGrid g = Grid();
        EditorGUILayout.LabelField($"{l.Area():0.#} m² · {w}×{h} cells of {cell:0.##} m · {(g != null ? g.InsideCount : 0)} cells of soil · {l.sections?.Count ?? 0} section(s)", EditorStyles.miniLabel);
        if (Math.Abs(field.transform.lossyScale.x - 1f) > 0.001f || Math.Abs(field.transform.lossyScale.z - 1f) > 0.001f)
            EditorGUILayout.HelpBox("Keep the field's scale at 1: its outline is in metres.", MessageType.Warning);
        if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup > nextCheck)
        {
            nextCheck = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            l.Validate(errors, warnings);
            SoilField twin = AgricultureEditors.SameId(field);
            if (twin != null) errors.Add($"'{twin.name}' has the same Field Id (a duplicated object?): they would share one save. Press New Id.");
        }
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string x in warnings) EditorGUILayout.HelpBox(x, MessageType.Warning);
        string city = AgricultureEditors.CitySceneName();
        if (!string.IsNullOrEmpty(city) && field.gameObject.scene.IsValid() && field.gameObject.scene.name != city)
            EditorGUILayout.HelpBox($"This is not the City scene ('{city}'): fields outside the permanent city are rebuilt with their scene (the world is regenerated every cycle).", MessageType.Info);
    }

    private static string ToolHelp()
    {
        switch (tool)
        {
            case Tool.Outline: return "Drag the points on the ground. Shift+click near an edge to add a point; Ctrl/Cmd+click a point to remove it. (While a tool is on, clicks belong to the field: Select to pick other objects.)";
            case Tool.Sections: return "Click a section on the ground (or in the list), then move it (its centre), size it (its corner or edge), turn it (the disc) and, for polygons, edit its points like the outline.";
            case Tool.Paint: return "Paint cells with the brush (below) - drag over the field. Hold Alt to orbit the camera.";
            default: return "Pick a tool to shape the field in the Scene view. The preview shows the soil the game starts with.";
        }
    }

    // ------------------------------------------------------------------ outline
    private void OutlineGUI(SoilLayout l)
    {
        outlineFold = EditorGUILayout.BeginFoldoutHeaderGroup(outlineFold, $"Outline ({l.outline.Count} points)");
        if (outlineFold)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                snap = EditorGUILayout.ToggleLeft("Snap", snap, GUILayout.Width(60));
                snapStep = EditorGUILayout.FloatField(snapStep, GUILayout.Width(50));
                EditorGUILayout.LabelField("m", GUILayout.Width(20));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rectangle"))
                {
                    Rect b = l.Bounds();
                    Record("Soil Field Outline");
                    l.outline = new List<Vector2> { new Vector2(b.xMin, b.yMin), new Vector2(b.xMax, b.yMin), new Vector2(b.xMax, b.yMax), new Vector2(b.xMin, b.yMax) };
                    Changed();
                }
                if (GUILayout.Button("Circle"))
                {
                    Rect b = l.Bounds();
                    float r = Mathf.Max(b.width, b.height) * 0.5f;
                    Record("Soil Field Outline");
                    l.outline = new List<Vector2>();
                    for (int i = 0; i < 20; i++)
                    {
                        float a = i * Mathf.PI * 2f / 20f;
                        l.outline.Add(b.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                    }
                    Changed();
                }
                if (GUILayout.Button(new GUIContent("Centre", "Moves the object to the middle of the outline (the outline stays where it is in the world).")))
                {
                    // By whole cells: the soil, its sections, its paint and its patchiness stay exactly where they are.
                    Vector2 c = l.CentreOffset();
                    Undo.RecordObject(field.transform, "Centre Soil Field");
                    Record("Centre Soil Field");
                    field.transform.position += field.transform.rotation * new Vector3(c.x, 0f, c.y);
                    l.Shift(c);
                    Changed();
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layout.cellSize"));
            for (int i = 0; i < l.outline.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    Vector2 p = EditorGUILayout.Vector2Field($"#{i + 1}", l.outline[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Record("Soil Field Outline");
                        l.outline[i] = p;
                        Changed();
                    }
                    using (new EditorGUI.DisabledScope(l.outline.Count <= 3))
                        if (GUILayout.Button("x", GUILayout.Width(22)))
                        {
                            Record("Soil Field Outline");
                            l.outline.RemoveAt(i);
                            Changed();
                            break;
                        }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ------------------------------------------------------------------ base soil
    private void BaseGUI()
    {
        baseFold = EditorGUILayout.BeginFoldoutHeaderGroup(baseFold, "Base soil");
        if (baseFold)
        {
            SerializedProperty p = serializedObject.FindProperty("layout.baseSoil");
            SerializedProperty type = p.FindPropertyRelative("type");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(type);
                if (GUILayout.Button(new GUIContent("Use its soil", "Sets every property to the type's typical soil."), GUILayout.Width(90)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Record("Base Soil Preset");
                    field.layout.baseSoil = SoilProfile.Preset(field.layout.baseSoil.type);
                    Changed();
                    serializedObject.Update();
                    p = serializedObject.FindProperty("layout.baseSoil");
                }
            }
            EditorGUI.indentLevel++;
            for (int c = 0; c < SoilChannels.Authored; c++)
                EditorGUILayout.PropertyField(p.FindPropertyRelative(ChannelFields[c]), new GUIContent(SoilChannels.Name((SoilChannel)c)));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ------------------------------------------------------------------ sections
    private void SectionsGUI(SoilLayout l)
    {
        l.sections ??= new List<SoilSection>();
        sectionsFold = EditorGUILayout.BeginFoldoutHeaderGroup(sectionsFold, $"Sections ({l.sections.Count})");
        if (sectionsFold)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Rectangle")) AddSection(l, SoilSectionShape.Rectangle);
                if (GUILayout.Button("+ Circle")) AddSection(l, SoilSectionShape.Circle);
                if (GUILayout.Button("+ Polygon")) AddSection(l, SoilSectionShape.Polygon);
                if (GUILayout.Button("+ Painted")) AddSection(l, SoilSectionShape.Painted);
            }
            if (l.sections.Count == 0)
                EditorGUILayout.HelpBox("No sections: the whole field is the base soil. Add one for every part whose soil differs (a sour corner, a stony strip, a bed dug with compost...).", MessageType.None);
            SerializedProperty list = serializedObject.FindProperty("layout.sections");
            for (int i = 0; i < l.sections.Count && i < list.arraySize; i++)
                if (SectionGUI(l, list.GetArrayElementAtIndex(i), i)) break;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void AddSection(SoilLayout l, SoilSectionShape shape)
    {
        serializedObject.ApplyModifiedProperties();
        Record("Add Soil Section");
        Rect b = l.Bounds();
        var s = new SoilSection
        {
            name = $"{shape} {l.sections.Count + 1}",
            shape = shape,
            center = b.center,
            size = new Vector2(Mathf.Max(0.5f, b.width * 0.35f), Mathf.Max(0.5f, b.height * 0.35f)),
            radius = Mathf.Max(0.4f, Mathf.Min(b.width, b.height) * 0.2f),
            color = Color.HSVToRGB((l.sections.Count * 0.17f + 0.08f) % 1f, 0.75f, 0.95f),
        };
        if (shape == SoilSectionShape.Polygon)
        {
            Vector2 c = b.center;
            float r = Mathf.Min(b.width, b.height) * 0.2f;
            s.points = new List<Vector2> { c + new Vector2(-r, -r), c + new Vector2(r, -r * 0.7f), c + new Vector2(r * 0.8f, r), c + new Vector2(-r * 0.9f, r * 0.8f) };
        }
        s.soil.values = field.layout.baseSoil.Clone();
        s.uid = l.NewSectionUid();
        l.sections.Add(s);
        selected = l.sections.Count - 1;
        openSections.Add(selected);
        if (shape == SoilSectionShape.Painted)
        {
            tool = Tool.Paint;
            brushValue = selected + 1;
        }
        else tool = Tool.Sections;
        Changed();
        serializedObject.Update();
    }

    /// <summary>One section's block. Returns true when the list changed (stop drawing it this frame).</summary>
    private bool SectionGUI(SoilLayout l, SerializedProperty sp, int i)
    {
        SoilSection s = l.sections[i];
        if (s == null) return false;
        bool open = openSections.Contains(i);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool nowOpen = EditorGUILayout.Foldout(open, "", true);
                if (nowOpen != open)
                {
                    if (nowOpen) openSections.Add(i);
                    else openSections.Remove(i);
                    open = nowOpen;
                }
                EditorGUILayout.PropertyField(sp.FindPropertyRelative("enabled"), GUIContent.none, GUILayout.Width(16));
                EditorGUILayout.PropertyField(sp.FindPropertyRelative("color"), GUIContent.none, GUILayout.Width(36));
                GUIStyle style = selected == i ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button($"{s.name}  ({s.shape})", style))
                {
                    selected = i;
                    if (tool == Tool.Select) tool = Tool.Sections;
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(22))) { Move(l, i, i - 1); return true; }
                using (new EditorGUI.DisabledScope(i == l.sections.Count - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(22))) { Move(l, i, i + 1); return true; }
                if (GUILayout.Button(new GUIContent("⧉", "Duplicate"), GUILayout.Width(24)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Record("Duplicate Soil Section");
                    SoilSection copy = s.Clone();
                    copy.uid = l.NewSectionUid();
                    copy.name = s.name + " copy";
                    copy.center += new Vector2(0.5f, 0.5f);
                    for (int k = 0; k < copy.points.Count; k++) copy.points[k] += new Vector2(0.5f, 0.5f);
                    l.sections.Add(copy);
                    selected = l.sections.Count - 1;
                    Changed();
                    serializedObject.Update();
                    return true;
                }
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Record("Remove Soil Section");
                    l.sections.RemoveAt(i);
                    l.SectionRemoved(i);
                    openSections.Clear();
                    // The brush and the selection follow their sections by id (the removed one's become erase / none).
                    Changed();
                    serializedObject.Update();
                    return true;
                }
            }
            if (!open) return false;
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("name"));
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("shape"));
            switch (s.shape)
            {
                case SoilSectionShape.Rectangle:
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("center"));
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("size"));
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("angle"));
                    break;
                case SoilSectionShape.Circle:
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("center"));
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("radius"));
                    break;
                case SoilSectionShape.Polygon:
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("points"), true);
                    break;
                default:
                    EditorGUILayout.HelpBox("Only painted cells: use Paint with this section as the brush.", MessageType.None);
                    break;
            }
            if (s.shape != SoilSectionShape.Painted) EditorGUILayout.PropertyField(sp.FindPropertyRelative("feather"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Its soil (tick what differs here)", EditorStyles.boldLabel);
            SerializedProperty soil = sp.FindPropertyRelative("soil");
            SerializedProperty values = soil.FindPropertyRelative("values");
            using (new EditorGUILayout.HorizontalScope())
            {
                SerializedProperty ot = soil.FindPropertyRelative("overrideType");
                ot.boolValue = EditorGUILayout.ToggleLeft("Soil type", ot.boolValue, GUILayout.Width(110));
                using (new EditorGUI.DisabledScope(!ot.boolValue))
                    EditorGUILayout.PropertyField(soil.FindPropertyRelative("type"), GUIContent.none);
                if (GUILayout.Button(new GUIContent("Use its soil", "Ticks every property and sets it to the type's typical soil."), GUILayout.Width(90)))
                {
                    serializedObject.ApplyModifiedProperties();
                    Record("Section Soil Preset");
                    s.soil.UsePreset(s.soil.type);
                    Changed();
                    serializedObject.Update();
                    return true;
                }
            }
            SerializedProperty mask = soil.FindPropertyRelative("mask");
            for (int c = 0; c < SoilChannels.Authored; c++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool on = (mask.intValue & (1 << c)) != 0;
                    bool now = EditorGUILayout.ToggleLeft(SoilChannels.Name((SoilChannel)c), on, GUILayout.Width(150));
                    if (now != on) mask.intValue = now ? mask.intValue | (1 << c) : mask.intValue & ~(1 << c);
                    using (new EditorGUI.DisabledScope(!now))
                        EditorGUILayout.PropertyField(values.FindPropertyRelative(ChannelFields[c]), GUIContent.none);
                }
            }
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("irrigated"));
            if (s.irrigated)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp.FindPropertyRelative("irrigationMoisture"), new GUIContent("Kept at moisture"));
                EditorGUILayout.PropertyField(sp.FindPropertyRelative("irrigationWater"), new GUIContent("Water"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("mulch"));
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("note"));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Paint this section"))
                {
                    tool = Tool.Paint;
                    brushValue = i + 1;
                    selected = i;
                    paintFold = true;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Clear its paint"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Record("Clear Section Paint");
                    l.ClearPaint(i);
                    Changed();
                    serializedObject.Update();
                }
            }
        }
        return false;
    }

    private void Move(SoilLayout l, int from, int to)
    {
        serializedObject.ApplyModifiedProperties();
        Record("Reorder Soil Sections");
        SoilSection s = l.sections[from];
        l.sections[from] = l.sections[to];
        l.sections[to] = s;
        l.SectionsSwapped(from, to);
        openSections.Clear();
        Changed();
        serializedObject.Update();
    }

    // ------------------------------------------------------------------ painting, patchiness, the rest
    private void PaintGUI(SoilLayout l)
    {
        paintFold = EditorGUILayout.BeginFoldoutHeaderGroup(paintFold, "Painting");
        if (paintFold)
        {
            brush = EditorGUILayout.Slider("Brush (m)", brush, 0.1f, 5f);
            var options = new List<string> { "Erase (shapes decide)" };
            for (int i = 0; i < l.sections.Count; i++) options.Add($"{i + 1}. {l.sections[i]?.name}");
            options.Add("Base soil (no section)");
            int index = brushValue == SoilLayout.PaintBase ? options.Count - 1 : Mathf.Clamp(brushValue, 0, l.sections.Count);
            // Only a choice made here changes the brush (another field's inspector drawing this never takes it over).
            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup("Paints", index, options.ToArray());
            if (EditorGUI.EndChangeCheck()) brushValue = index == options.Count - 1 ? SoilLayout.PaintBase : index;
            if (GUILayout.Button("Clear all paint"))
            {
                Record("Clear Soil Paint");
                l.ClearPaint();
                Changed();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void VariationGUI()
    {
        variationFold = EditorGUILayout.BeginFoldoutHeaderGroup(variationFold, "Natural patchiness");
        if (variationFold)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layout.variation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layout.variationScale"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("layout.seed"));
                if (GUILayout.Button("New", GUILayout.Width(50)))
                    serializedObject.FindProperty("layout.seed").intValue = UnityEngine.Random.Range(1, int.MaxValue);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void SettingsGUI()
    {
        settingsFold = EditorGUILayout.BeginFoldoutHeaderGroup(settingsFold, "Weather, look, interaction, saving");
        if (settingsFold)
        {
            string[] names = { "useWorldWeather", "showSoil", "drawDistance", "detailDistance", "groundOffset", "followGround", "groundLayers", "plantShadows", "interactable", "interactionPriority", "extraReach", "saveToDisk" };
            foreach (string n in names) EditorGUILayout.PropertyField(serializedObject.FindProperty(n));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void RuntimeGUI()
    {
        if (!Application.isPlaying) return;
        runtimeFold = EditorGUILayout.BeginFoldoutHeaderGroup(runtimeFold, "Play mode");
        if (runtimeFold)
        {
            FieldState st = field.State;
            int alive = 0, seeds = 0;
            foreach (PlantState p in st.Plants)
            {
                if (!p.Alive) continue;
                if (p.IsSeed) seeds++;
                else alive++;
            }
            EditorGUILayout.LabelField(FarmClock.Date.Full());
            EditorGUILayout.LabelField($"{alive} plants growing, {seeds} seeds in the ground · {st.Weather.temperature:0.0} °C, rain {st.Weather.rain:0.0} mm/h · rules {(field.RulesHere ? "here" : "on the server")}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Starter kit")) AgricultureEditors.InvokeContext(field, "GiveStarterKit");
                if (GUILayout.Button("+6 h")) FarmClock.Skip(6.0);
                if (GUILayout.Button("+1 day")) FarmClock.Skip(24.0);
                if (GUILayout.Button("+1 week")) FarmClock.Skip(24.0 * 7.0);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Rebuild from layout", "Starts the field afresh from the layout (plants are lost).")))
                    field.Rebuild();
                if (GUILayout.Button(new GUIContent("Forget its save", "Deletes the saved field and starts afresh.")))
                    AgricultureEditors.InvokeContext(field, "ForgetSave");
            }
            Repaint();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ================================================================== the baked preview
    private int Hash()
    {
        unchecked { return field.layout.Hash() * 31 + version; }
    }

    private double lastBake;
    private byte[] previewPaint;

    private SoilGrid Grid()
    {
        if (field == null || field.layout == null) return null;
        int h = Hash();
        if (grid != null && h == gridHash) return grid;
        // While a handle is dragged over a big field, bake at most a dozen times a second.
        double t = EditorApplication.timeSinceStartup;
        if (grid != null && grid.Count > 20000 && t - lastBake < 0.08)
        {
            SceneView.RepaintAll();
            return grid;
        }
        lastBake = t;
        try
        {
            // Baked as the game bakes it; the stored paint is only read (painting alone reshapes it).
            grid = field.layout.CreateGrid();
            field.layout.Bake(grid, 0.0);
            previewPaint = field.layout.PaintFor(grid);
            gridHash = h;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Soil Field preview: {e.Message}");
            grid = null;
        }
        return grid;
    }

    private Texture2D Texture(SoilGrid g)
    {
        if (texture != null && texHash == gridHash && texMode == preview && texture.width == g.Width && texture.height == g.Height)
            return texture;
        if (texture == null || texture.width != g.Width || texture.height != g.Height)
        {
            if (texture != null) DestroyImmediate(texture);
            texture = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        }
        var px = new Color32[g.Count];
        for (int i = 0; i < g.Count; i++) px[i] = g.Inside[i] ? (Color32)Colour(g, i) : new Color32(0, 0, 0, 0);
        texture.SetPixels32(px);
        texture.Apply(false, false);
        texHash = gridHash;
        texMode = preview;
        return texture;
    }

    private Color Colour(SoilGrid g, int i)
    {
        switch (preview)
        {
            case 1:
            {
                Color c = SoilTypes.DryColor(g.TypeAt(i));
                return Color.Lerp(c, Color.white, 0.15f);
            }
            case 2:
            {
                int s = SectionAt(g, i, out float w);
                return s < 0 ? new Color(0.45f, 0.45f, 0.45f) : Color.Lerp(new Color(0.45f, 0.45f, 0.45f), field.layout.sections[s].color, Mathf.Max(0.35f, w));
            }
            case 26: return Gradient(g.Fertility(i));
            case 27: return g.IrrigationTarget[i] > 0 ? Color.Lerp(new Color(0.2f, 0.4f, 0.9f), new Color(0.4f, 0.9f, 1f), g.IrrigationTarget[i] / 255f) : new Color(0.3f, 0.3f, 0.3f);
            default:
            {
                var ch = (SoilChannel)(preview - 3);
                Vector2 r = DisplayRange(ch);
                return Gradient(Mathf.InverseLerp(r.x, r.y, g.Get(i, ch)));
            }
        }
    }

    /// <summary>The section that decides a cell (the last one with the most weight), and its weight.</summary>
    private int SectionAt(SoilGrid g, int i, out float weight)
    {
        SoilLayout l = field.layout;
        weight = 0f;
        int best = -1;
        byte paint = previewPaint != null && i < previewPaint.Length ? previewPaint[i] : (byte)0;
        if (paint == SoilLayout.PaintBase) return -1;
        Vector2 p = g.Center(i);
        for (int s = 0; s < l.sections.Count; s++)
        {
            SoilSection sec = l.sections[s];
            if (sec == null || !sec.enabled) continue;
            float w = Mathf.Max(sec.Weight(p), paint == s + 1 ? 1f : 0f);
            if (w > 0.01f && w >= weight * 0.999f) { weight = w; best = s; }
        }
        return best;
    }

    public static Vector2 DisplayRange(SoilChannel c)
    {
        switch (c)
        {
            case SoilChannel.Ph: return new Vector2(3.5f, 9.5f);
            case SoilChannel.Nitrogen: return new Vector2(0f, 120f);
            case SoilChannel.Phosphorus: return new Vector2(0f, 80f);
            case SoilChannel.Potassium: return new Vector2(0f, 500f);
            case SoilChannel.Calcium: return new Vector2(0f, 4000f);
            case SoilChannel.Magnesium: return new Vector2(0f, 600f);
            case SoilChannel.Sulfur: return new Vector2(0f, 80f);
            case SoilChannel.Iron: return new Vector2(0f, 60f);
            case SoilChannel.Zinc: return new Vector2(0f, 8f);
            case SoilChannel.Copper: return new Vector2(0f, 4f);
            case SoilChannel.Manganese: return new Vector2(0f, 50f);
            case SoilChannel.Boron: return new Vector2(0f, 3f);
            case SoilChannel.Molybdenum: return new Vector2(0f, 0.5f);
            case SoilChannel.OrganicMatter: return new Vector2(0f, 30f);
            case SoilChannel.Salinity: return new Vector2(0f, 8f);
            default: return new Vector2(0f, 1f);
        }
    }

    /// <summary>Dark blue → teal → green → yellow.</summary>
    public static Color Gradient(float t)
    {
        t = Mathf.Clamp01(t);
        Color a = new Color(0.16f, 0.1f, 0.42f), b = new Color(0.12f, 0.5f, 0.6f), c = new Color(0.3f, 0.75f, 0.35f), d = new Color(0.98f, 0.9f, 0.2f);
        return t < 0.33f ? Color.Lerp(a, b, t / 0.33f) : t < 0.66f ? Color.Lerp(b, c, (t - 0.33f) / 0.33f) : Color.Lerp(c, d, (t - 0.66f) / 0.34f);
    }

    private string ValueText(SoilGrid g, int i)
    {
        switch (preview)
        {
            case 1: return AgriText.Soil(g.TypeAt(i));
            case 2:
            {
                int s = SectionAt(g, i, out float w);
                return s < 0 ? "Base soil" : $"{field.layout.sections[s].name}{(w < 0.99f ? $" ({w * 100f:0}%)" : "")}";
            }
            case 26: return $"Fertility {g.Fertility(i) * 100f:0}%";
            case 27: return g.IrrigationTarget[i] > 0 ? $"Irrigated, kept at {g.IrrigationTarget[i] / 2.55f:0}% moisture with {AgriText.Water((WaterSourceKind)g.IrrigationWater[i]).ToLowerInvariant()}" : "Not irrigated";
            default:
            {
                var ch = (SoilChannel)(preview - 3);
                float v = g.Get(i, ch);
                string unit = SoilChannels.Unit(ch);
                string value = ch == SoilChannel.Ph ? v.ToString("0.00") : v < 10f ? v.ToString("0.00") : v.ToString("0");
                return ch >= SoilChannel.WaterRetention && ch != SoilChannel.Salinity && unit == "" ? $"{SoilChannels.Name(ch)} {v * 100f:0}%" : $"{SoilChannels.Name(ch)} {value} {unit}";
            }
        }
    }

    // ================================================================== Scene view
    private static readonly int GroundHash = "SoilFieldGround".GetHashCode();

    private void OnSceneGUI()
    {
        if (field == null || field.layout == null) return;
        SoilLayout l = field.layout;
        Transform t = field.transform;
        Matrix4x4 m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        Event e = Event.current;

        // While a tool edits the field, a click on the bare ground is the tool's (adding a point, picking a section,
        // painting), not the Scene view's selection; the handles under the mouse still come first. (Select: as usual.)
        int ground = GUIUtility.GetControlID(GroundHash, FocusType.Passive);
        if (e.type == EventType.Layout) EnsureUids();
        RefreshTransformTool();
        if (tool != Tool.Select && !Viewing && e.type == EventType.Layout) HandleUtility.AddDefaultControl(ground);

        SoilGrid g = Grid();
        if (g != null && preview > 0 && e.type == EventType.Repaint) DrawPreview(g, m, l);

        using (new Handles.DrawingScope(m))
        {
            DrawOutline(l.outline, new Color(1f, 0.8f, 0.4f, 1f), 3f);
            for (int i = 0; i < l.sections.Count; i++)
            {
                SoilSection s = l.sections[i];
                if (s == null || s.shape == SoilSectionShape.Painted) continue;
                Color c = s.color;
                c.a = s.enabled ? 1f : 0.35f;
                DrawOutline(s.Outline(), c, i == selected ? 3.5f : 1.5f);
                if (i == selected) Handles.Label(new Vector3(s.center.x, 0.1f, s.center.y), s.name, EditorStyles.whiteBoldLabel);
            }
            switch (tool)
            {
                case Tool.Outline: EditPoints(l.outline, 3, "Soil Field Outline", new Color(1f, 0.85f, 0.4f), ground); break;
                case Tool.Sections: EditSection(l, ground); break;
                case Tool.Paint: Paint(l, e, ground); break;
            }
        }
        // A click the tools took on the ground ends here (with the left button that made it).
        if (GUIUtility.hotControl == ground && e.GetTypeForControl(ground) == EventType.MouseUp && e.button == 0)
        {
            GUIUtility.hotControl = 0;
            e.Use();
        }
        Hover(g, e);
    }

    private void DrawPreview(SoilGrid g, Matrix4x4 m, SoilLayout l)
    {
        Texture2D tex = Texture(g);
        if (material == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) return;
            material = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (quad == null)
        {
            quad = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            quad.SetVertices(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) });
            quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            quad.RecalculateBounds();
        }
        material.mainTexture = tex;
        material.color = new Color(1f, 1f, 1f, opacity);
        Matrix4x4 local = Matrix4x4.TRS(new Vector3(g.Origin.x, 0.04f, g.Origin.y), Quaternion.identity, new Vector3(g.Width * g.CellSize, 1f, g.Height * g.CellSize));
        if (material.SetPass(0)) Graphics.DrawMeshNow(quad, m * local);
    }

    private static void DrawOutline(List<Vector2> pts, Color c, float width)
    {
        if (pts == null || pts.Count < 2) return;
        var line = new Vector3[pts.Count + 1];
        for (int i = 0; i < pts.Count; i++) line[i] = new Vector3(pts[i].x, 0.06f, pts[i].y);
        line[pts.Count] = line[0];
        Handles.color = c;
        Handles.DrawAAPolyLine(width, line);
    }

    private bool Snapping => snap && snapStep > 0.01f;

    private Vector2 Snap(Vector2 p) => Snapping ? new Vector2(Mathf.Round(p.x / snapStep) * snapStep, Mathf.Round(p.y / snapStep) * snapStep) : p;

    private float SnapLength(float v, float min) => Mathf.Max(min, Snapping ? Mathf.Max(snapStep, Mathf.Round(v / snapStep) * snapStep) : v);

    /// <summary>A click on the bare ground for the tool <paramref name="ground"/>: true (and the click is the tool's) when it is one.</summary>
    private static bool GroundClick(Event e, int ground) =>
        e.GetTypeForControl(ground) == EventType.MouseDown && e.button == 0 && !e.alt && !Viewing && HandleUtility.nearestControl == ground;

    private static void TakeClick(Event e, int ground)
    {
        GUIUtility.hotControl = ground;
        e.Use();
    }

    /// <summary>Point handles on the ground plane: drag; Shift+click near an edge adds; Ctrl/Cmd+click a point removes.</summary>
    private void EditPoints(List<Vector2> pts, int min, string undo, Color colour, int ground)
    {
        if (pts == null) return;
        Event e = Event.current;
        Handles.color = colour;
        for (int i = 0; i < pts.Count; i++)
        {
            var at = new Vector3(pts[i].x, 0.06f, pts[i].y);
            float size = HandleUtility.GetHandleSize(at) * 0.08f;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && EditorGUI.actionKey && pts.Count > min &&
                HandleUtility.DistanceToCircle(at, size) < 1f)
            {
                Record(undo);
                pts.RemoveAt(i);
                TakeClick(e, ground);
                Changed();
                return;
            }
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(at, Vector3.up, Vector3.right, Vector3.forward, size, Handles.SphereHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck())
            {
                Record(undo);
                pts[i] = Snap(new Vector2(moved.x, moved.z));
                Changed();
            }
        }
        if (e.shift && pts.Count >= 2 && GroundClick(e, ground) && Plane(e, out Vector2 click))
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                float d = AgriMath.DistanceToSegment(click, pts[j], pts[i]);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0 && bestD < 1.5f)
            {
                Record(undo);
                pts.Insert(best, Snap(click));
                Changed();
            }
            TakeClick(e, ground);
        }
    }

    /// <summary>The section deciding a spot (the last that covers it), or -1.</summary>
    private int SectionUnder(SoilLayout l, Vector2 p)
    {
        int cell = -1;
        if (grid != null && grid.TryIndex(p, out int ci)) cell = ci;
        byte paint = cell >= 0 && previewPaint != null && cell < previewPaint.Length ? previewPaint[cell] : (byte)0;
        if (paint == SoilLayout.PaintBase) return -1;
        for (int s = l.sections.Count - 1; s >= 0; s--)
        {
            SoilSection sec = l.sections[s];
            if (sec == null || !sec.enabled) continue;
            if (paint == s + 1 || (sec.shape != SoilSectionShape.Painted && sec.Weight(p) >= 0.5f)) return s;
        }
        return -1;
    }

    private void EditSection(SoilLayout l, int ground)
    {
        Event e = Event.current;
        // A click on the ground picks the section there (Shift+click on a polygon's edge adds a point instead).
        bool polygon = selected >= 0 && selected < l.sections.Count && l.sections[selected] != null && l.sections[selected].shape == SoilSectionShape.Polygon;
        if (!(polygon && e.shift) && !EditorGUI.actionKey && GroundClick(e, ground) && Plane(e, out Vector2 at))
        {
            selected = SectionUnder(l, at);
            if (selected >= 0) openSections.Add(selected);
            TakeClick(e, ground);
            Repaint();
            return;
        }
        if (selected < 0 || selected >= l.sections.Count) return;
        SoilSection s = l.sections[selected];
        if (s == null) return;
        Handles.color = s.color;
        var c3 = new Vector3(s.center.x, 0.06f, s.center.y);
        float size = HandleUtility.GetHandleSize(c3) * 0.1f;
        switch (s.shape)
        {
            case SoilSectionShape.Polygon:
                EditPoints(s.points, 3, "Soil Section Points", s.color, ground);
                break;
            case SoilSectionShape.Circle:
            {
                // One handle at a time: the centre moves it, the edge sizes it.
                EditorGUI.BeginChangeCheck();
                Vector3 c = Handles.Slider2D(c3, Vector3.up, Vector3.right, Vector3.forward, size, Handles.CubeHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Move Soil Section");
                    s.center = Snap(new Vector2(c.x, c.z));
                    Changed();
                    c3 = new Vector3(s.center.x, 0.06f, s.center.y);
                }
                EditorGUI.BeginChangeCheck();
                Vector3 edge = Handles.Slider2D(c3 + Vector3.right * s.radius, Vector3.up, Vector3.right, Vector3.forward, size * 0.8f, Handles.SphereHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Size Soil Section");
                    s.radius = SnapLength((new Vector2(edge.x, edge.z) - s.center).magnitude, 0.05f);
                    Changed();
                }
                break;
            }
            case SoilSectionShape.Rectangle:
            {
                // One handle at a time: the centre moves it, the corner sizes it, the disc turns it.
                EditorGUI.BeginChangeCheck();
                Vector3 c = Handles.Slider2D(c3, Vector3.up, Vector3.right, Vector3.forward, size, Handles.CubeHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Move Soil Section");
                    s.center = Snap(new Vector2(c.x, c.z));
                    Changed();
                    c3 = new Vector3(s.center.x, 0.06f, s.center.y);
                }
                Quaternion rot = Quaternion.Euler(0f, -s.angle, 0f);
                Vector3 ax = rot * Vector3.right, az = rot * Vector3.forward;
                EditorGUI.BeginChangeCheck();
                Vector3 corner = Handles.Slider2D(c3 + ax * s.size.x * 0.5f + az * s.size.y * 0.5f, Vector3.up, ax, az, size * 0.8f, Handles.SphereHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Size Soil Section");
                    Vector3 d = corner - c3;
                    s.size = new Vector2(SnapLength(Mathf.Abs(Vector3.Dot(d, ax)) * 2f, 0.1f), SnapLength(Mathf.Abs(Vector3.Dot(d, az)) * 2f, 0.1f));
                    Changed();
                }
                EditorGUI.BeginChangeCheck();
                Quaternion turned = Handles.Disc(rot, c3, Vector3.up, Mathf.Max(0.3f, Mathf.Min(s.size.x, s.size.y) * 0.35f), false, 15f);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Turn Soil Section");
                    s.angle = Mathf.Repeat(-turned.eulerAngles.y + 180f, 360f) - 180f;
                    Changed();
                }
                break;
            }
        }
    }

    private void Paint(SoilLayout l, Event e, int ground)
    {
        // (A brush whose section is gone reads as erase: nothing hidden is ever painted.)
        if (Plane(e, out Vector2 at))
        {
            Color c = brushValue == 0 ? new Color(1f, 1f, 1f, 0.8f) : brushValue == SoilLayout.PaintBase ? new Color(0.6f, 0.6f, 0.6f, 0.9f) :
                l.sections[brushValue - 1] != null ? l.sections[brushValue - 1].color : Color.white;
            Handles.color = c;
            Handles.DrawWireDisc(new Vector3(at.x, 0.07f, at.y), Vector3.up, brush);
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
        }
        switch (e.GetTypeForControl(ground))
        {
            case EventType.MouseDown:
                if (!GroundClick(e, ground) || !Plane(e, out Vector2 down)) break;
                Undo.IncrementCurrentGroup();
                paintGroup = Undo.GetCurrentGroup();
                painting = true;
                PaintAt(l, down);
                TakeClick(e, ground);
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl != ground || !painting) break;
                if (Plane(e, out Vector2 drag)) PaintAt(l, drag);
                e.Use();
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl != ground || e.button != 0) break;
                GUIUtility.hotControl = 0;
                if (painting)
                {
                    painting = false;
                    Undo.CollapseUndoOperations(paintGroup);
                }
                e.Use();
                break;
        }
    }

    private void PaintAt(SoilLayout l, Vector2 p)
    {
        Record("Paint Soil");
        if (l.Paint(p, brush, (byte)Mathf.Clamp(brushValue, 0, 255))) Changed();
    }

    /// <summary>Where the mouse meets the field's ground plane, field-local.</summary>
    private bool Plane(Event e, out Vector2 local)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var plane = new Plane(field.transform.up, field.transform.position);
        if (plane.Raycast(ray, out float d))
        {
            local = field.Local(ray.GetPoint(d));
            return true;
        }
        local = default;
        return false;
    }

    private void Hover(SoilGrid g, Event e)
    {
        if (g == null || preview <= 0) return;
        if (!Plane(e, out Vector2 at) || !g.TryIndex(at, out int i)) return;
        string text = ValueText(g, i);
        string extra = preview != 2 ? SectionAt(g, i, out _) is int s && s >= 0 ? $" · {field.layout.sections[s].name}" : " · base soil" : "";
        Handles.BeginGUI();
        Vector2 mp = e.mousePosition + new Vector2(16f, 8f);
        var content = new GUIContent($"{text}{extra}  ({at.x:0.0}, {at.y:0.0})");
        Vector2 sz = EditorStyles.helpBox.CalcSize(content);
        GUI.Box(new Rect(mp.x, mp.y, sz.x + 8f, sz.y + 4f), content, EditorStyles.helpBox);
        if (preview >= 3 && preview <= 26)
        {
            var r = new Rect(10f, 10f, 160f, 14f);
            for (int k = 0; k < 32; k++)
                EditorGUI.DrawRect(new Rect(r.x + k * r.width / 32f, r.y, r.width / 32f + 1f, r.height), Gradient(k / 31f));
            string lo, hi;
            if (preview == 26) { lo = "poor"; hi = "rich"; }
            else
            {
                Vector2 range = DisplayRange((SoilChannel)(preview - 3));
                lo = range.x.ToString("0.#");
                hi = range.y.ToString("0.#");
            }
            GUI.Label(new Rect(r.x, r.yMax + 2f, 220f, 18f), $"{PreviewNames[preview]}: {lo} → {hi}", EditorStyles.whiteMiniLabel);
        }
        Handles.EndGUI();
        if (e.type == EventType.MouseMove) SceneView.RepaintAll();
    }
}
#endif
