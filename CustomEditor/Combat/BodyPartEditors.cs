#if UNITY_EDITOR
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspectors of the body-part system: a front-view figure of the character's parts (each segment where it is on the
/// body, left and right, coloured by region, with its damage multiplier), one-click setup (an editable profile, hitboxes
/// on a Humanoid rig) and, while playing, test hits and the parts' state.
/// </summary>
public static class BodyPartDiagram
{
    /// <summary>The part picked on the figure (by profile), shown in detail under it.</summary>
    private static readonly Dictionary<BodyPartProfile, int> picked = new Dictionary<BodyPartProfile, int>();
    private static bool showTable;

    private struct Drawn
    {
        public int index;
        public Vector2[] points;
        public Vector2 anchor;
        public int column; // -1 viewer-left (the character's right), +1 viewer-right, 0 inside the shape
    }

    private struct Callout
    {
        public int index;
        public Vector2 anchor;
        public float y;
        public string text;
        public Color color;
    }

    /// <summary>The part picked on the figure of <paramref name="p"/> (-1 = none).</summary>
    public static int Picked(BodyPartProfile p) => p != null && picked.TryGetValue(p, out int i) ? i : -1;

    /// <summary>
    /// The figure, the picked part's details and a foldout with every part. Click a part on the figure to see it;
    /// <paramref name="state"/> (optional) adds a line per part while playing.
    /// </summary>
    public static void DrawAll(BodyPartProfile p, float height = 260f, System.Func<int, string> state = null)
    {
        Rect r = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
        Draw(r, p);
        int sel = Picked(p);
        if (p != null && p.parts != null && sel >= 0 && sel < p.parts.Count && p.parts[sel] != null)
        {
            BodyPartDefinition part = p.parts[sel];
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect sw = GUILayoutUtility.GetRect(12f, 14f, GUILayout.Width(12f));
                EditorGUI.DrawRect(new Rect(sw.x, sw.y + 2f, 10f, 10f), BodyPartController.PartColor(part, sel));
                string extra = state != null ? state(sel) : null;
                EditorGUILayout.LabelField(part.Describe() + (string.IsNullOrEmpty(extra) ? "" : "\n" + extra), EditorStyles.wordWrappedMiniLabel);
            }
        }
        else
            EditorGUILayout.LabelField("Click a part on the figure to see it.", EditorStyles.centeredGreyMiniLabel);
        if (p != null && p.parts != null)
        {
            showTable = EditorGUILayout.Foldout(showTable, $"Every part ({p.parts.Count})", true);
            if (showTable)
                DrawTable(p);
        }
    }

    /// <summary>
    /// Draws the parts of <paramref name="p"/> on a front-view figure (the character's right on the left, as when facing
    /// it): head, neck, torso segments, and each arm and leg segment by segment, coloured by region, labelled with their
    /// damage multiplier. Parts the figure has no place for (a tail) are bars of their height on the far right.
    /// </summary>
    public static void Draw(Rect r, BodyPartProfile p)
    {
        EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.18f));
        if (p == null || p.parts == null || p.parts.Count == 0)
        {
            GUI.Label(r, "No parts", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        if (p.UsesLength)
        {
            DrawSideView(r, p);
            return;
        }
        BodyPartProfile id = p; // keyed by the profile itself (GetInstanceID is obsolete in Unity 6.3+)
        int sel = Picked(p);
        // The figure is half as wide as it is tall, narrower when the inspector is (the label columns need room).
        float room = r.height - 22f;
        float figW = Mathf.Min(room * 0.5f, Mathf.Max(60f, r.width - 220f));
        float figH = figW * 2f;
        var fig = new Rect(r.center.x - figW * 0.5f, r.y + 16f + (room - figH) * 0.5f, figW, figH);
        Vector2 ToGui(Vector2 v) => new Vector2(fig.x + v.x * fig.width, fig.y + v.y * fig.height);

        var drawn = new List<Drawn>();
        var others = new List<int>();
        for (int i = 0; i < p.parts.Count; i++)
        {
            BodyPartDefinition part = p.parts[i];
            if (part == null)
                continue;
            BodyPartLayout.Region region = BodyPartLayout.Classify(part);
            Vector2[] shape = BodyPartLayout.Shape(region);
            if (shape == null)
            {
                others.Add(i);
                continue;
            }
            if (!BodyPartLayout.IsLimb(region))
            {
                drawn.Add(Make(i, shape, 0, ToGui));
                continue;
            }
            BodyPartLayout.Sides(part, region, out bool left, out bool right);
            if (right) drawn.Add(Make(i, shape, -1, ToGui));
            if (left) drawn.Add(Make(i, BodyPartLayout.Mirror(shape), 1, ToGui));
        }

        // Picking: the smallest shape under the mouse (a hand over a whole-arm part).
        Event e = Event.current;
        int hover = -1;
        float hoverArea = float.MaxValue;
        if (r.Contains(e.mousePosition))
            foreach (Drawn d in drawn)
                if (BodyPartLayout.Contains(d.points, e.mousePosition))
                {
                    Rect bb = BodyPartLayout.Bounds(d.points);
                    if (bb.width * bb.height < hoverArea) { hoverArea = bb.width * bb.height; hover = d.index; }
                }
        if (e.type == EventType.MouseDown && e.button == 0 && fig.Contains(e.mousePosition))
        {
            picked[id] = hover; // empty space: nothing picked
            e.Use();
            GUI.changed = true;
        }

        if (e.type == EventType.Repaint)
        {
            // Big shapes first, so segments drawn over a whole-limb part stay visible.
            drawn.Sort((a, b) => Area(b.points).CompareTo(Area(a.points)));
            foreach (Drawn d in drawn)
            {
                Color c = BodyPartController.PartColor(p.parts[d.index], d.index);
                bool hi = d.index == sel || d.index == hover;
                Fill(d.points, new Color(c.r, c.g, c.b, hi ? 0.95f : 0.62f));
                Outline(d.points, hi ? Color.white : new Color(0f, 0f, 0f, 0.55f), hi ? 2.5f : 1.5f);
            }
        }

        // Labels: inside the torso segments when they fit, else beside the figure with a line to the part. The
        // character's right side is on the left (a front view).
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontStyle = FontStyle.Bold };
        var callouts = new Dictionary<int, List<Callout>> { { -1, new List<Callout>() }, { 1, new List<Callout>() } };
        foreach (Drawn d in drawn)
        {
            BodyPartDefinition part = p.parts[d.index];
            string text = $"{BodyPartLayout.ShortName(part.name)} ×{part.damageMultiplier:0.##}";
            Rect bb = BodyPartLayout.Bounds(d.points);
            Vector2 size = style.CalcSize(new GUIContent(text));
            if (d.column == 0 && bb.width >= size.x + 2f && bb.height >= size.y)
            {
                if (e.type == EventType.Repaint)
                    GUI.Label(new Rect(bb.center.x - size.x * 0.5f, bb.center.y - size.y * 0.5f, size.x, size.y), text, style);
                continue;
            }
            // Centre parts too small for a label: the head on the left, the neck and groin on the right, so they do not pile up.
            int column = d.column;
            if (column == 0)
            {
                BodyPartLayout.Region reg = BodyPartLayout.Classify(part);
                column = reg == BodyPartLayout.Region.Neck || reg == BodyPartLayout.Region.Groin ? 1 : -1;
            }
            Color c = BodyPartController.PartColor(part, d.index);
            callouts[column].Add(new Callout { index = d.index, anchor = d.anchor, y = d.anchor.y, text = text, color = Color.Lerp(c, Color.white, 0.4f) });
        }
        foreach (int i in others)
        {
            BodyPartDefinition part = p.parts[i];
            float lo = Mathf.Clamp01(Mathf.Min(part.heightRange.x, part.heightRange.y)), top = Mathf.Clamp01(Mathf.Max(part.heightRange.x, part.heightRange.y));
            var bar = new Rect(fig.xMax + 4f + (others.IndexOf(i) % 3) * 7f, fig.y + (1f - top) * fig.height, 5f, Mathf.Max(3f, (top - lo) * fig.height));
            Color c = BodyPartController.PartColor(part, i);
            if (e.type == EventType.Repaint)
                EditorGUI.DrawRect(bar, new Color(c.r, c.g, c.b, i == sel ? 1f : 0.7f));
            if (e.type == EventType.MouseDown && bar.Contains(e.mousePosition))
            {
                picked[id] = i;
                e.Use();
                GUI.changed = true;
            }
            callouts[1].Add(new Callout { index = i, anchor = new Vector2(bar.xMax, bar.center.y), y = bar.center.y, text = $"{part.name} ×{part.damageMultiplier:0.##}",
                                          color = Color.Lerp(c, Color.white, 0.4f) });
        }
        float colW = Mathf.Max(60f, (r.width - fig.width) * 0.5f - 22f);
        Place(callouts[-1], new Rect(r.x + 4f, r.y + 14f, colW, r.height - 16f), false, style, e, sel, id);
        Place(callouts[1], new Rect(r.xMax - colW - 4f, r.y + 14f, colW, r.height - 16f), true, style, e, sel, id);

        if (e.type == EventType.Repaint)
        {
            GUI.Label(new Rect(r.x + 4f, r.y + 1f, colW, 14f), "Right side", EditorStyles.miniBoldLabel);
            var rs = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(r.xMax - colW - 4f, r.y + 1f, colW, 14f), "Left side", rs);
            GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 14f), "front view · click a part", EditorStyles.centeredGreyMiniLabel);
        }
    }

    /// <summary>
    /// A horizontal body from the side (the tail on the left, the nose on the right): each part's box over its length and
    /// height bands, the right side's limbs a little behind the left side's, labelled with the damage multiplier.
    /// </summary>
    private static void DrawSideView(Rect r, BodyPartProfile p)
    {
        int sel = Picked(p);
        var body = new Rect(r.x + 12f, r.y + 18f, r.width - 24f, r.height - 40f);
        Event e = Event.current;
        var rects = new List<(int index, Rect rect)>();
        for (int i = 0; i < p.parts.Count; i++)
        {
            BodyPartDefinition part = p.parts[i];
            if (part == null)
                continue;
            float l0 = part.HasLength ? Mathf.Clamp01(Mathf.Min(part.lengthRange.x, part.lengthRange.y)) : 0f;
            float l1 = part.HasLength ? Mathf.Clamp01(Mathf.Max(part.lengthRange.x, part.lengthRange.y)) : 1f;
            float h0 = Mathf.Clamp01(Mathf.Min(part.heightRange.x, part.heightRange.y)), h1 = Mathf.Clamp01(Mathf.Max(part.heightRange.x, part.heightRange.y));
            float shift = part.side == BodySide.Right ? 4f : part.side == BodySide.Left ? -4f : 0f;
            rects.Add((i, new Rect(body.x + l0 * body.width + shift, body.y + (1f - h1) * body.height - shift, Mathf.Max(3f, (l1 - l0) * body.width),
                Mathf.Max(3f, (h1 - h0) * body.height))));
        }
        // Picking: the smallest box under the mouse.
        int hover = -1;
        float hoverArea = float.MaxValue;
        foreach ((int index, Rect rect) in rects)
            if (rect.Contains(e.mousePosition) && rect.width * rect.height < hoverArea)
            {
                hoverArea = rect.width * rect.height;
                hover = index;
            }
        if (e.type == EventType.MouseDown && e.button == 0 && body.Contains(e.mousePosition))
        {
            picked[p] = hover;
            e.Use();
            GUI.changed = true;
        }
        if (e.type != EventType.Repaint)
            return;
        // Big boxes first, the right side's (far) limbs before the left side's.
        rects.Sort((a, b) =>
        {
            int fa = p.parts[a.index].side == BodySide.Right ? 0 : 1, fb = p.parts[b.index].side == BodySide.Right ? 0 : 1;
            return fa != fb ? fa.CompareTo(fb) : (b.rect.width * b.rect.height).CompareTo(a.rect.width * a.rect.height);
        });
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontStyle = FontStyle.Bold };
        foreach ((int index, Rect rect) in rects)
        {
            BodyPartDefinition part = p.parts[index];
            Color c = BodyPartController.PartColor(part, index);
            bool hi = index == sel || index == hover;
            float alpha = part.side == BodySide.Right ? 0.4f : 0.62f;
            EditorGUI.DrawRect(rect, new Color(c.r, c.g, c.b, hi ? 0.95f : alpha));
            Handles.color = hi ? Color.white : new Color(0f, 0f, 0f, 0.55f);
            Handles.DrawAAPolyLine(hi ? 2.5f : 1.2f, new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y), new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.x, rect.yMax), new Vector3(rect.x, rect.y));
            string text = $"{BodyPartLayout.ShortName(part.name)} ×{part.damageMultiplier:0.##}";
            Vector2 size = style.CalcSize(new GUIContent(text));
            if (rect.width >= size.x && rect.height >= size.y)
                GUI.Label(new Rect(rect.center.x - size.x * 0.5f, rect.center.y - size.y * 0.5f, size.x, size.y), text, style);
        }
        GUI.Label(new Rect(r.x + 4f, r.y + 1f, 120f, 14f), "Tail end", EditorStyles.miniBoldLabel);
        GUI.Label(new Rect(r.xMax - 124f, r.y + 1f, 120f, 14f), "Nose", new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.UpperRight });
        GUI.Label(new Rect(r.x, r.yMax - 16f, r.width, 14f), "side view (length bands) · right-side limbs behind · click a part", EditorStyles.centeredGreyMiniLabel);
    }

    private static Drawn Make(int index, Vector2[] shape, int column, System.Func<Vector2, Vector2> toGui)
    {
        var pts = new Vector2[shape.Length];
        for (int k = 0; k < shape.Length; k++)
            pts[k] = toGui(shape[k]);
        Rect bb = BodyPartLayout.Bounds(pts);
        // The line goes to the outer edge of a limb, to the middle of a centre part.
        Vector2 anchor = column < 0 ? new Vector2(bb.xMin + 2f, bb.center.y) : column > 0 ? new Vector2(bb.xMax - 2f, bb.center.y) : bb.center;
        return new Drawn { index = index, points = pts, anchor = anchor, column = column };
    }

    /// <summary>Stacks a column of labels by height (no overlaps) and draws each with a line to its part.</summary>
    private static void Place(List<Callout> list, Rect col, bool rightColumn, GUIStyle style, Event e, int sel, BodyPartProfile id)
    {
        if (list.Count == 0)
            return;
        list.Sort((a, b) => a.y.CompareTo(b.y));
        const float step = 13f;
        var ys = new float[list.Count];
        for (int i = 0; i < list.Count; i++)
            ys[i] = Mathf.Max(col.y + step * 0.5f, i > 0 ? Mathf.Max(list[i].y, ys[i - 1] + step) : list[i].y);
        // Pushed past the bottom: move the last ones back up.
        float bottom = col.yMax - step;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ys[i] > bottom) ys[i] = bottom;
            bottom = ys[i] - step;
        }
        var ls = new GUIStyle(style) { alignment = rightColumn ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, fontStyle = FontStyle.Normal };
        for (int i = 0; i < list.Count; i++)
        {
            Callout c = list[i];
            var lr = new Rect(col.x, ys[i] - step * 0.5f, col.width, step);
            if (e.type == EventType.MouseDown && e.button == 0 && lr.Contains(e.mousePosition))
            {
                picked[id] = c.index; // clicking a label picks its part too
                e.Use();
                GUI.changed = true;
            }
            if (e.type != EventType.Repaint)
                continue;
            ls.normal.textColor = c.index == sel ? Color.white : c.color;
            ls.fontStyle = c.index == sel ? FontStyle.Bold : FontStyle.Normal;
            GUI.Label(lr, c.text, ls);
            var from = new Vector2(rightColumn ? col.x - 2f : col.xMax + 2f, ys[i]);
            var elbow = new Vector2(rightColumn ? col.x - 8f : col.xMax + 8f, ys[i]);
            Handles.color = new Color(c.color.r, c.color.g, c.color.b, c.index == sel ? 1f : 0.55f);
            Handles.DrawAAPolyLine(1.5f, from, elbow, c.anchor);
        }
    }

    private static float Area(Vector2[] pts)
    {
        Rect b = BodyPartLayout.Bounds(pts);
        return b.width * b.height;
    }

    private static Material fillMaterial;

    /// <summary>Fills an outline (a fan from its centre).</summary>
    private static void Fill(Vector2[] pts, Color c)
    {
        if (pts == null || pts.Length < 3)
            return;
        Vector2 centre = Vector2.zero;
        foreach (Vector2 v in pts) centre += v;
        centre /= pts.Length;
        if (fillMaterial == null)
        {
            fillMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            fillMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fillMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fillMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            fillMaterial.SetInt("_ZWrite", 0);
        }
        fillMaterial.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.TRIANGLES);
        GL.Color(c);
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
            GL.Vertex3(centre.x, centre.y, 0f);
            GL.Vertex3(a.x, a.y, 0f);
            GL.Vertex3(b.x, b.y, 0f);
        }
        GL.End();
        GL.PopMatrix();
    }

    private static void Outline(Vector2[] pts, Color c, float width)
    {
        if (pts == null || pts.Length < 2)
            return;
        var line = new Vector3[pts.Length + 1];
        for (int i = 0; i < pts.Length; i++)
            line[i] = pts[i];
        line[pts.Length] = pts[0];
        Handles.color = c;
        Handles.DrawAAPolyLine(width, line);
    }

    /// <summary>One line per part: what protects it and what it does when hit.</summary>
    public static void DrawTable(BodyPartProfile p)
    {
        if (p == null || p.parts == null)
            return;
        for (int i = 0; i < p.parts.Count; i++)
        {
            BodyPartDefinition part = p.parts[i];
            if (part == null)
                continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect sw = GUILayoutUtility.GetRect(10f, 14f, GUILayout.Width(10f));
                EditorGUI.DrawRect(new Rect(sw.x, sw.y + 2f, 10f, 10f), BodyPartController.PartColor(part, i));
                EditorGUILayout.LabelField(part.Describe(), EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}

[CustomEditor(typeof(BodyPartProfile))]
public class BodyPartProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var p = (BodyPartProfile)target;
        var errors = new List<string>();
        var warnings = new List<string>();
        p.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Parts ({p.parts?.Count ?? 0})", EditorStyles.boldLabel);
        BodyPartDiagram.DrawAll(p);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Reset To Detailed Humanoid", "Head, neck, upper / lower torso, groin, and per side the upper arm, forearm, " +
                                                "hand, thigh, shin and foot, with the default multipliers, armour and effects (Undo restores).")))
            {
                Undo.RecordObject(p, "Reset body parts");
                p.FillHumanoid();
                EditorUtility.SetDirty(p);
            }
            if (GUILayout.Button(new GUIContent("Reset To Simple Humanoid", "Head, Torso, Arms, Legs (Undo restores).")))
            {
                Undo.RecordObject(p, "Reset body parts");
                p.FillSimpleHumanoid();
                EditorUtility.SetDirty(p);
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(BodyPartController))]
public class BodyPartControllerEditor : Editor
{
    private float testDamage = 10f;
    private static bool showLiveList;

    private static string LiveLine(BodyPartController c, BodyPartProfile used, int i)
    {
        if (used == null || used.parts == null || i < 0 || i >= used.parts.Count || used.parts[i] == null)
            return null;
        BodyPartController.PartState st = c.GetState(used.parts[i]);
        return $"Live: {st.hits} hit(s), {st.damageTaken:0.#} damage{(st.severed ? ", SEVERED" : "")}";
    }

    public override void OnInspectorGUI()
    {
        var c = (BodyPartController)target;
        serializedObject.Update();
        SerializedProperty assigned = serializedObject.FindProperty("profile");
        BodyPartProfile used = c.Profile;
        string source = assigned.objectReferenceValue != null ? "the assigned profile"
            : CombatSettings.Instance.defaultBodyPartProfile != null ? "Combat Settings ▸ Default Body Part Profile"
            : "the built-in humanoid (not editable: create a profile to change it)";

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Body parts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Using {source}.", EditorStyles.wordWrappedMiniLabel);
        BodyPartDiagram.DrawAll(used, 260f, Application.isPlaying ? (System.Func<int, string>)(i => LiveLine(c, used, i)) : null);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (assigned.objectReferenceValue == null)
            {
                if (GUILayout.Button(new GUIContent("Create Editable Profile", "Saves a copy of the profile in use as an asset and assigns it, so you can change parts, multipliers, armour and effects.")))
                    CreateProfile(c, used);
            }
            else if (GUILayout.Button(new GUIContent("Edit Profile", "Selects the assigned profile.")))
            {
                Selection.activeObject = assigned.objectReferenceValue;
            }
        }
        EditorGUILayout.EndVertical();

        // Hitboxes
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        BodyPartHitbox[] boxes = c.GetComponentsInChildren<BodyPartHitbox>(true);
        EditorGUILayout.LabelField($"Hitboxes ({boxes.Length})", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(boxes.Length == 0
                ? "None: parts come from where a hit lands (height and side). Hitboxes on the bones make hits exact and follow the animation."
                : "Hits that touch a hitbox count for its part; other hits use the height and side.",
            EditorStyles.wordWrappedMiniLabel);
        Animator animator = c.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.isHuman && animator.avatar != null;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!human || Application.isPlaying))
            {
                if (GUILayout.Button(new GUIContent(boxes.Length == 0 ? "Add Hitboxes (Humanoid)" : "Rebuild Hitboxes (Humanoid)",
                        human ? "A hitbox per segment, named after the profile's parts: head, neck, upper and lower torso, groin, and each upper arm, " +
                                "forearm, hand, thigh, shin and foot (a simple profile gets Head / Torso / Arms / Legs)."
                              : "Needs a Humanoid Animator with an Avatar. On other rigs add BodyPartHitbox components to colliders yourself.")))
                    HumanoidHitboxes.Build(c, used, animator);
            }
            using (new EditorGUI.DisabledScope(boxes.Length == 0 || Application.isPlaying))
            {
                if (GUILayout.Button("Remove Hitboxes"))
                    HumanoidHitboxes.Remove(c);
            }
        }
        if (!human)
            EditorGUILayout.LabelField("Not a Humanoid rig: add a collider and a Body Part Hitbox to each bone yourself (Part = a part's name).", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        if (Application.isPlaying)
            DrawLive(c, used);
        else
            EditorGUILayout.HelpBox("Select the character to see its parts in the Scene view (coloured bands, hitboxes). While playing, hits show where they " +
                                    "landed with the part and the damage, and you can test each part below.", MessageType.None);

        EditorGUILayout.Space(4);
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying)
            Repaint();
    }

    private void DrawLive(BodyPartController c, BodyPartProfile used)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
        testDamage = EditorGUILayout.Slider(new GUIContent("Test Damage", "Damage of a test hit (before the part's multiplier and armour)."), testDamage, 1f, 200f);
        CombatEntity e = c.Entity;
        int sel = BodyPartDiagram.Picked(used);
        if (used != null && used.parts != null && sel >= 0 && sel < used.parts.Count && used.parts[sel] != null)
        {
            using (new EditorGUI.DisabledScope(e == null || e.IsDead))
                if (GUILayout.Button($"Hit the {BodyPartLayout.ShortName(used.parts[sel].name)} ({testDamage:0} damage)"))
                    TestHit(c, e, used.parts[sel]);
        }
        else
            EditorGUILayout.LabelField("Pick a part on the figure to test-hit it, or use the list.", EditorStyles.miniLabel);
        showLiveList = EditorGUILayout.Foldout(showLiveList, "Every part: hits and damage", true);
        for (int i = 0; showLiveList && used != null && used.parts != null && i < used.parts.Count; i++)
        {
            BodyPartDefinition part = used.parts[i];
            if (part == null)
                continue;
            BodyPartController.PartState st = c.GetState(part);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect sw = GUILayoutUtility.GetRect(10f, 16f, GUILayout.Width(10f));
                EditorGUI.DrawRect(new Rect(sw.x, sw.y + 3f, 10f, 10f), BodyPartController.PartColor(part, i));
                EditorGUILayout.LabelField(part.name, $"{st.hits} hit(s), {st.damageTaken:0.#} damage{(st.severed ? ", SEVERED" : "")}");
                using (new EditorGUI.DisabledScope(e == null || e.IsDead))
                {
                    if (GUILayout.Button(new GUIContent("Hit", $"Deals {testDamage:0} damage aimed at the {part.name} (unblockable), to check multipliers, armour and effects."), GUILayout.Width(40f)))
                        TestHit(c, e, part);
                }
            }
        }
        IReadOnlyList<BodyPartController.RecentHit> hits = c.RecentHits;
        if (hits.Count > 0)
        {
            BodyPartController.RecentHit last = hits[hits.Count - 1];
            EditorGUILayout.LabelField($"Last hit: {last.part}, {last.damage:0.#} damage, {Time.time - last.time:0.0}s ago", EditorStyles.miniLabel);
        }
        if (GUILayout.Button("Reset Parts"))
            c.ResetParts();
        EditorGUILayout.EndVertical();
    }

    private void TestHit(BodyPartController c, CombatEntity e, BodyPartDefinition part)
    {
        float mid = (part.heightRange.x + part.heightRange.y) * 0.5f;
        Vector3 point = e.BasePosition + Vector3.up * mid * e.Height;
        if (part.side == BodySide.Left) point -= c.transform.right * e.Radius;
        else if (part.side != BodySide.Any) point += c.transform.right * e.Radius;
        if (part.HasLength)
        {
            c.BodyExtent(out float back, out float front);
            float along = (part.lengthRange.x + part.lengthRange.y) * 0.5f;
            point += c.transform.forward * (back + along * (front - back));
        }
        e.ApplyDamage(new DamageInfo
        {
            amount = testDamage,
            target = e,
            point = point,
            direction = -c.transform.forward,
            type = DamageType.Physical,
            delivery = DamageDelivery.Melee,
            aimedBodyPart = part.name,
            unblockable = true,
        });
    }

    private void CreateProfile(BodyPartController c, BodyPartProfile from)
    {
        var copy = from != null ? Object.Instantiate(from) : BodyPartProfile.CreateHumanoid();
        copy.name = c.gameObject.name + " Body Parts";
        // A mob's go with its other assets (Mobs/<Type>), anything else in Combat/Body Parts.
        string folder = c.GetComponentInParent<Mob>() != null ? MobSetupUtility.FolderFor(c.gameObject) : SMAssetsEditor.Folder(SMAssets.BodyParts);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{copy.name}.asset");
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        serializedObject.FindProperty("profile").objectReferenceValue = copy;
        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(copy);
        Debug.Log($"[Body Parts] Created '{path}' and assigned it to '{c.name}'.", copy);
    }
}

/// <summary>Creates body-part hitboxes on a Humanoid rig's bones.</summary>
public static class HumanoidHitboxes
{
    private const string Prefix = "Hitbox_";

    public static void Build(BodyPartController c, BodyPartProfile profile, Animator a)
    {
        Remove(c);
        CombatEntity e = c.GetComponent<CombatEntity>();
        float h = e != null && e.Height > 0.1f ? e.Height : 1.8f;
        int layer = c.gameObject.layer;
        int made = 0;

        // Each segment gets the most precise part the profile has: "LeftForearm", else "Forearms", else "Arms"...
        string head = Pick(profile, "Head");
        Transform headBone = a.GetBoneTransform(HumanBodyBones.Head);
        if (headBone != null)
        {
            var go = Make(headBone, Prefix + head, layer);
            var sc = Undo.AddComponent<SphereCollider>(go);
            sc.radius = 0.065f * h / Mathf.Max(0.0001f, headBone.lossyScale.y);
            sc.center = headBone.InverseTransformVector(Vector3.up * 0.05f * h);
            Tag(go, head);
            made++;
        }
        made += Capsule(a, HumanBodyBones.Neck, HumanBodyBones.Head, Pick(profile, "Neck", "Head"), 0.045f * h, layer);
        HumanBodyBones chest = a.GetBoneTransform(HumanBodyBones.Chest) != null ? HumanBodyBones.Chest : HumanBodyBones.Spine;
        made += Capsule(a, chest, HumanBodyBones.Neck, Pick(profile, "UpperTorso", "Chest", "Torso"), 0.12f * h, layer);
        made += Capsule(a, HumanBodyBones.Hips, chest, Pick(profile, "LowerTorso", "Abdomen", "Torso"), 0.11f * h, layer);
        made += Groin(a, Pick(profile, "Groin", "Hips", "Torso"), 0.07f * h, layer);
        foreach (bool left in new[] { true, false })
        {
            string sn = left ? "Left" : "Right";
            HumanBodyBones upperArm = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            HumanBodyBones lowerArm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            HumanBodyBones hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            HumanBodyBones upperLeg = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            HumanBodyBones lowerLeg = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
            HumanBodyBones foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            HumanBodyBones toes = left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            made += Capsule(a, upperArm, lowerArm, Pick(profile, sn + "UpperArm", "UpperArms", sn + "Arm", "Arms"), 0.04f * h, layer);
            made += Capsule(a, lowerArm, hand, Pick(profile, sn + "Forearm", "Forearms", sn + "Arm", "Arms"), 0.035f * h, layer);
            made += Extremity(a, hand, lowerArm, HumanBodyBones.LastBone, Pick(profile, sn + "Hand", "Hands", sn + "Arm", "Arms"), 0.04f * h, 0.09f * h, layer);
            made += Capsule(a, upperLeg, lowerLeg, Pick(profile, sn + "Thigh", "Thighs", sn + "Leg", "Legs"), 0.06f * h, layer);
            made += Capsule(a, lowerLeg, foot, Pick(profile, sn + "Shin", "Shins", sn + "Leg", "Legs"), 0.045f * h, layer);
            made += Extremity(a, foot, lowerLeg, toes, Pick(profile, sn + "Foot", "Feet", sn + "Leg", "Legs"), 0.04f * h, 0.14f * h, layer);
        }
        EditorUtility.SetDirty(c.gameObject);
        Debug.Log($"[Body Parts] Added {made} hitbox(es) to '{c.name}' (triggers on layer '{LayerMask.LayerToName(layer)}'). Adjust their radius on each Hitbox_ object.", c);
    }

    public static void Remove(BodyPartController c)
    {
        foreach (BodyPartHitbox hb in c.GetComponentsInChildren<BodyPartHitbox>(true))
            if (hb != null && hb.gameObject.name.StartsWith(Prefix))
                Undo.DestroyObjectImmediate(hb.gameObject);
    }

    /// <summary>
    /// A part name of the profile: the first of <paramref name="wanted"/> it has (by name, then by alias), else the first
    /// wanted name.
    /// </summary>
    private static string Pick(BodyPartProfile p, params string[] wanted)
    {
        if (p != null)
        {
            foreach (string w in wanted)
                foreach (BodyPartDefinition d in p.parts)
                    if (d != null && string.Equals(d.name, w, System.StringComparison.OrdinalIgnoreCase))
                        return d.name;
            foreach (string w in wanted)
            {
                BodyPartDefinition d = p.Find(w);
                if (d != null)
                    return d.name;
            }
        }
        return wanted[0];
    }

    /// <summary>A sphere between the thighs, a little under the hips (the groin).</summary>
    private static int Groin(Animator a, string part, float radius, int layer)
    {
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        Transform l = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), r = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        if (hips == null || l == null || r == null)
            return 0;
        var go = Make(hips, $"{Prefix}{part}_Groin", layer);
        go.transform.position = (l.position + r.position) * 0.5f - Vector3.up * radius * 0.6f;
        var sc = Undo.AddComponent<SphereCollider>(go);
        sc.radius = radius / Mathf.Max(0.0001f, go.transform.lossyScale.y);
        Tag(go, part);
        return 1;
    }

    /// <summary>
    /// A hand or a foot: a capsule from the bone toward <paramref name="tip"/> (the toes), else along the direction from
    /// <paramref name="parent"/> (the forearm), <paramref name="length"/> long.
    /// </summary>
    private static int Extremity(Animator a, HumanBodyBones bone, HumanBodyBones parent, HumanBodyBones tip, string part, float radius, float length, int layer)
    {
        Transform b = a.GetBoneTransform(bone), p = a.GetBoneTransform(parent);
        if (b == null)
            return 0;
        Transform t = tip != HumanBodyBones.LastBone ? a.GetBoneTransform(tip) : null;
        Vector3 axis = t != null ? t.position - b.position : p != null ? b.position - p.position : Vector3.down;
        if (axis.sqrMagnitude < 1e-6f)
            axis = Vector3.down;
        axis = axis.normalized * length;
        var go = Make(b, $"{Prefix}{part}_{bone}", layer);
        go.transform.position = b.position + axis * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        var cc = Undo.AddComponent<CapsuleCollider>(go);
        float s = Mathf.Max(0.0001f, go.transform.lossyScale.y);
        cc.direction = 1;
        cc.radius = radius / s;
        cc.height = (length + radius) / s;
        Tag(go, part);
        return 1;
    }

    private static int Capsule(Animator a, HumanBodyBones from, HumanBodyBones to, string part, float radius, int layer)
    {
        Transform f = a.GetBoneTransform(from), t = a.GetBoneTransform(to);
        if (f == null || t == null)
            return 0;
        Vector3 axis = t.position - f.position;
        float length = axis.magnitude;
        if (length < 0.01f)
            return 0;
        var go = Make(f, $"{Prefix}{part}_{from}", layer);
        go.transform.position = f.position + axis * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis / length);
        var cc = Undo.AddComponent<CapsuleCollider>(go);
        float s = Mathf.Max(0.0001f, go.transform.lossyScale.y);
        cc.direction = 1;
        cc.radius = radius / s;
        cc.height = (length + radius) / s;
        Tag(go, part);
        return 1;
    }

    private static GameObject Make(Transform bone, string name, int layer)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Add body part hitbox");
        go.transform.SetParent(bone, false);
        go.layer = layer;
        return go;
    }

    private static void Tag(GameObject go, string part)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        var hb = Undo.AddComponent<BodyPartHitbox>(go);
        hb.part = part;
    }
}
#endif
#endif
