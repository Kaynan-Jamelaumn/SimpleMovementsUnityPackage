#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Scene view helpers for areas on the ground - a plot, a house's footprint, a room, a zone: polygons of points in
/// metres on an object's ground plane (its local X, Z), drawn and edited in the current <see cref="Handles.matrix"/>
/// (set it to the object's position and rotation without its scale: <c>Matrix4x4.TRS(position, rotation, Vector3.one)</c>).
/// <para>A tool owns the clicks on the bare ground through a default control: make one in every OnSceneGUI with
/// <c>GUIUtility.GetControlID(hash, FocusType.Passive)</c>, add it on Layout with <c>HandleUtility.AddDefaultControl</c>
/// while the tool is on (and not <see cref="Viewing"/>), take a click with <see cref="TakeClick"/> and end it with
/// <see cref="ReleaseClick"/> at the end of OnSceneGUI. The handles under the mouse still come first.</para>
/// <para>Drawing allocates nothing while the shapes stay the same (the outlines share one buffer, the fills keep their
/// triangles until their points change).</para>
/// </summary>
public static class GroundAreaHandles
{
    /// <summary>Height of the point handles above the ground plane, metres.</summary>
    public const float HandleLift = 0.06f;
    /// <summary>How near an edge (metres) a Shift+click must be to add a point there.</summary>
    public const float InsertDistance = 1.5f;

    private static Vector3[] line = new Vector3[64];
    private static readonly Vector3[] triangle = new Vector3[3];
    private static GUIStyle labelStyle;

    /// <summary>The Scene view's hand (panning, orbiting): the clicks are never an area tool's then.</summary>
    public static bool Viewing => Tools.viewToolActive || Tools.current == UnityEditor.Tool.View;

    // ------------------------------------------------------------------ Unity's transform gizmo
    private static readonly HashSet<object> gizmoHiders = new HashSet<object>();
    private static bool hiddenBefore;

    /// <summary>
    /// Hides Unity's move/rotate/scale gizmo for <paramref name="owner"/> (an editor's own key, such as its type) while
    /// <paramref name="hide"/>, else gives its share back. Every area tool goes through here: what Tools.hidden was before
    /// the first one hid it comes back when the last lets go, in whatever order (two inspectors at once, or two editors on
    /// one object, never leave the gizmo hidden).
    /// </summary>
    public static void HideTransformTool(object owner, bool hide)
    {
        if (owner == null) return;
        if (hide)
        {
            if (gizmoHiders.Count == 0) hiddenBefore = Tools.hidden;
            gizmoHiders.Add(owner);
            Tools.hidden = true;
        }
        else if (gizmoHiders.Remove(owner) && gizmoHiders.Count == 0) Tools.hidden = hiddenBefore;
    }

    // ------------------------------------------------------------------ clicks on the ground
    /// <summary>Is this a left click on the bare ground for the tool's default control <paramref name="ground"/>?</summary>
    public static bool GroundClick(Event e, int ground) =>
        e.GetTypeForControl(ground) == EventType.MouseDown && e.button == 0 && !e.alt && !Viewing && HandleUtility.nearestControl == ground;

    /// <summary>The click is the tool's: nothing else (the Scene view's selection) gets it.</summary>
    public static void TakeClick(Event e, int ground)
    {
        GUIUtility.hotControl = ground;
        e.Use();
    }

    /// <summary>Ends a click the tool took (the left button's MouseUp). Call it at the end of OnSceneGUI.</summary>
    public static void ReleaseClick(Event e, int ground)
    {
        if (GUIUtility.hotControl == ground && e.GetTypeForControl(ground) == EventType.MouseUp && e.button == 0)
        {
            GUIUtility.hotControl = 0;
            e.Use();
        }
    }

    /// <summary>Where the mouse meets <paramref name="t"/>'s ground plane, as its local X, Z.</summary>
    public static bool PlaneHit(Transform t, Event e, out Vector2 local) => PlaneHit(t, e, out local, out _);

    /// <summary>Where the mouse meets <paramref name="t"/>'s ground plane, as its local X, Z, and how far along the mouse ray.</summary>
    public static bool PlaneHit(Transform t, Event e, out Vector2 local, out float distance)
    {
        local = default;
        distance = 0f;
        if (t == null || e == null) return false;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var plane = new Plane(t.up, t.position);
        if (!plane.Raycast(ray, out distance)) return false;
        local = HousingGeometry.ToLocal(ray.GetPoint(distance), t.position, t.rotation);
        return true;
    }

    // ------------------------------------------------------------------ drawing
    /// <summary>The closed outline of the points, <paramref name="lift"/> metres above the ground plane (Repaint only).</summary>
    public static void DrawOutline(IList<Vector2> local, Color colour, float width, float lift)
    {
        if (local == null || local.Count < 2 || Event.current == null || Event.current.type != EventType.Repaint) return;
        int n = local.Count + 1;
        if (line.Length < n) line = new Vector3[Mathf.NextPowerOfTwo(n)];
        for (int i = 0; i < local.Count; i++) line[i] = new Vector3(local[i].x, lift, local[i].y);
        line[local.Count] = line[0];
        Color old = Handles.color;
        Handles.color = colour;
        Handles.DrawAAPolyLine(width, n, line);
        Handles.color = old;
    }

    /// <summary>The polygon filled (any simple polygon, convex or not), <paramref name="lift"/> metres above the ground plane (Repaint only).</summary>
    public static void DrawFill(IList<Vector2> local, Color colour, float lift)
    {
        if (local == null || local.Count < 3 || Event.current == null || Event.current.type != EventType.Repaint) return;
        Fill f = FillOf(local, lift);
        Color old = Handles.color;
        Handles.color = colour;
        if (f.convex != null) Handles.DrawAAConvexPolygon(f.convex);
        else
            foreach (Vector2[] t in f.triangles)
            {
                triangle[0] = new Vector3(t[0].x, lift, t[0].y);
                triangle[1] = new Vector3(t[1].x, lift, t[1].y);
                triangle[2] = new Vector3(t[2].x, lift, t[2].y);
                Handles.DrawAAConvexPolygon(triangle);
            }
        Handles.color = old;
    }

    /// <summary>A label at a local point of the ground plane, <paramref name="lift"/> metres up (Repaint only).</summary>
    public static void Label(Vector2 local, string text, float lift) => Label(local, text, lift, LabelStyle);

    /// <summary>A label at a local point of the ground plane, <paramref name="lift"/> metres up, in <paramref name="style"/> (Repaint only).</summary>
    public static void Label(Vector2 local, string text, float lift, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text) || Event.current == null || Event.current.type != EventType.Repaint) return;
        Handles.Label(new Vector3(local.x, lift, local.y), text, style ?? LabelStyle);
    }

    /// <summary>The labels' style: a small box that reads on any ground.</summary>
    public static GUIStyle LabelStyle => labelStyle ??= new GUIStyle(EditorStyles.helpBox)
    {
        fontSize = 11,
        alignment = TextAnchor.MiddleCenter,
        wordWrap = false,
        richText = false,
    };

    // ------------------------------------------------------------------ editing
    /// <summary>
    /// Point handles on the ground plane: drag a point (snapped to <paramref name="snapStep"/> metres, 0 = no snap);
    /// Shift+click near an edge adds a point; Ctrl/Cmd+click a point removes it while there are more than
    /// <paramref name="min"/>. Every change is recorded on <paramref name="undoTarget"/> (Undo, dirty, Scene view repainted).
    /// <paramref name="t"/> is the object whose ground plane the points are on (where a click lands). True when the points changed.
    /// </summary>
    public static bool EditPoints(Object undoTarget, List<Vector2> pts, int min, string undoName, Color colour, int ground, Transform t, float snapStep)
    {
        if (pts == null) return false;
        Event e = Event.current;
        bool changed = false;
        Color old = Handles.color;
        Handles.color = colour;
        for (int i = 0; i < pts.Count; i++)
        {
            var at = new Vector3(pts[i].x, HandleLift, pts[i].y);
            float size = HandleUtility.GetHandleSize(at) * 0.08f;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && EditorGUI.actionKey && pts.Count > min &&
                HandleUtility.DistanceToCircle(at, size) < 1f)
            {
                Undo.RecordObject(undoTarget, undoName);
                pts.RemoveAt(i);
                TakeClick(e, ground);
                Changed(undoTarget);
                Handles.color = old;
                return true;
            }
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(at, Vector3.up, Vector3.right, Vector3.forward, size, Handles.SphereHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(undoTarget, undoName);
                pts[i] = Snap(new Vector2(moved.x, moved.z), snapStep);
                changed = true;
            }
        }
        if (e.shift && pts.Count >= 2 && t != null)
        {
            // Where a Shift+click would add a point, and the click itself.
            int edge = -1;
            Vector2 on = default;
            if ((e.type == EventType.Repaint || GroundClick(e, ground)) && PlaneHit(t, e, out Vector2 mouse))
                edge = NearestEdge(pts, mouse, out on);
            if (e.type == EventType.Repaint && edge >= 0)
                Handles.DrawWireDisc(new Vector3(on.x, HandleLift, on.y), Vector3.up, HandleUtility.GetHandleSize(new Vector3(on.x, 0f, on.y)) * 0.06f);
            if (GroundClick(e, ground))
            {
                if (edge >= 0)
                {
                    Undo.RecordObject(undoTarget, undoName);
                    pts.Insert(edge, Snap(on, snapStep));
                    changed = true;
                }
                TakeClick(e, ground);
            }
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
        }
        if (changed) Changed(undoTarget);
        Handles.color = old;
        return changed;
    }

    /// <summary>
    /// A square handle in the middle of the points that moves them all, by whole <paramref name="snapStep"/>s (points on
    /// the grid stay on it; 0 = no snap). Recorded on <paramref name="undoTarget"/>. True when they moved.
    /// </summary>
    public static bool MoveAll(Object undoTarget, List<Vector2> pts, string undoName, Color colour, float snapStep)
    {
        if (pts == null || pts.Count == 0) return false;
        Vector2 c = Centre(pts);
        var at = new Vector3(c.x, HandleLift, c.y);
        float size = HandleUtility.GetHandleSize(at) * 0.1f;
        Color old = Handles.color;
        Handles.color = colour;
        bool changed = false;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider2D(at, Vector3.up, Vector3.right, Vector3.forward, size, Handles.CubeHandleCap, Vector2.zero);
        if (EditorGUI.EndChangeCheck())
        {
            Vector2 delta = Snap(new Vector2(moved.x - at.x, moved.z - at.z), snapStep);
            if (delta.sqrMagnitude > 1e-10f)
            {
                Undo.RecordObject(undoTarget, undoName);
                for (int i = 0; i < pts.Count; i++) pts[i] += delta;
                Changed(undoTarget);
                changed = true;
            }
        }
        Handles.color = old;
        return changed;
    }

    /// <summary>
    /// A square handle at the world point <paramref name="at"/> that moves the object <paramref name="moved"/> over the
    /// ground plane of <paramref name="plane"/>, by whole <paramref name="snapStep"/>s of that plane (0 = no snap). Draw it
    /// with the identity <see cref="Handles.matrix"/>. True when it moved.
    /// </summary>
    public static bool MoveObject(Transform moved, Vector3 at, Transform plane, Color colour, float snapStep, string undoName)
    {
        if (moved == null || plane == null) return false;
        float size = HandleUtility.GetHandleSize(at) * 0.1f;
        Color old = Handles.color;
        Handles.color = colour;
        bool changed = false;
        EditorGUI.BeginChangeCheck();
        Vector3 to = Handles.Slider2D(at, plane.up, plane.right, plane.forward, size, Handles.CubeHandleCap, Vector2.zero);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 d = Quaternion.Inverse(plane.rotation) * (to - at);
            Vector2 step = Snap(new Vector2(d.x, d.z), snapStep);
            if (step.sqrMagnitude > 1e-10f)
            {
                Undo.RecordObject(moved, undoName);
                moved.position += plane.rotation * new Vector3(step.x, 0f, step.y);
                SceneView.RepaintAll();
                changed = true;
            }
        }
        Handles.color = old;
        return changed;
    }

    /// <summary>The point snapped to a grid of <paramref name="step"/> metres (0 or less: as it is).</summary>
    public static Vector2 Snap(Vector2 p, float step) =>
        step > 0f ? new Vector2(Mathf.Round(p.x / step) * step, Mathf.Round(p.y / step) * step) : p;

    // ------------------------------------------------------------------ shapes
    /// <summary>The middle of the box around the points (without allocating: it runs every repaint).</summary>
    public static Vector2 Centre(IList<Vector2> pts)
    {
        if (pts == null || pts.Count == 0) return Vector2.zero;
        Vector2 min = pts[0], max = pts[0];
        for (int i = 1; i < pts.Count; i++)
        {
            min = Vector2.Min(min, pts[i]);
            max = Vector2.Max(max, pts[i]);
        }
        return (min + max) * 0.5f;
    }

    /// <summary>A hash of the points (and how many): changes whenever the shape does.</summary>
    public static int Hash(IList<Vector2> pts)
    {
        if (pts == null) return 0;
        unchecked
        {
            int h = pts.Count * 397;
            for (int i = 0; i < pts.Count; i++) h = h * 31 + pts[i].GetHashCode();
            return h;
        }
    }

    /// <summary>
    /// Is it a rectangle (four points, square corners)? Then its centre, its size (first side, second side) and the angle of
    /// its first side (degrees, counter-clockwise seen from above, in (-90, 90]) - what <see cref="HousingGeometry.Rectangle"/> makes.
    /// </summary>
    public static bool IsRectangle(IList<Vector2> pts, out Vector2 centre, out Vector2 size, out float degrees)
    {
        centre = Centre(pts);
        size = Vector2.zero;
        degrees = 0f;
        if (pts == null || pts.Count != 4) return false;
        Vector2 a = pts[1] - pts[0], b = pts[2] - pts[1], c = pts[3] - pts[2], d = pts[0] - pts[3];
        float la = a.magnitude, lb = b.magnitude;
        if (la < 1e-4f || lb < 1e-4f) return false;
        float tolerance = 1e-3f * Mathf.Max(la, lb);
        if (Mathf.Abs(Vector2.Dot(a, b)) > 1e-3f * la * lb || (a + c).magnitude > tolerance || (b + d).magnitude > tolerance) return false;
        centre = (pts[0] + pts[1] + pts[2] + pts[3]) * 0.25f;
        size = new Vector2(Tidy(la), Tidy(lb));
        degrees = Mathf.Atan2(a.y, a.x) * Mathf.Rad2Deg;
        if (degrees > 90f + 1e-3f) degrees -= 180f;
        else if (degrees <= -90f + 1e-3f) degrees += 180f;
        degrees = Tidy(degrees);
        return true;
    }

    /// <summary>Rounded to a ten-thousandth (what a float field shows: 30, not 29.999996).</summary>
    private static float Tidy(float v) => Mathf.Round(v * 10000f) / 10000f;

    /// <summary>"16 × 12 m": a rectangle's sides, else the size of the box around the points.</summary>
    public static string SizeText(IList<Vector2> pts)
    {
        Vector2 s;
        if (!IsRectangle(pts, out _, out s, out _))
        {
            Rect b = HousingGeometry.Bounds(pts);
            s = b.size;
        }
        return $"{s.x:0.##} × {s.y:0.##} m";
    }

    /// <summary>The index of the edge (the point it ends at) nearest <paramref name="p"/> when it is within <see cref="InsertDistance"/>, else -1.</summary>
    private static int NearestEdge(List<Vector2> pts, Vector2 p, out Vector2 on)
    {
        int best = -1;
        float bestD = float.MaxValue;
        on = p;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            Vector2 a = pts[j], ab = pts[i] - a;
            float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            Vector2 q = a + ab * t;
            float d = Vector2.Distance(p, q);
            if (d < bestD)
            {
                bestD = d;
                best = i;
                on = q;
            }
        }
        return bestD < InsertDistance ? best : -1;
    }

    private static void Changed(Object target)
    {
        if (target != null) EditorUtility.SetDirty(target);
        SceneView.RepaintAll();
    }

    // ------------------------------------------------------------------ fills (kept until their points change)
    private sealed class Fill
    {
        public IList<Vector2> source;
        public int hash;
        public float lift;
        public int used;
        /// <summary>The points of a convex polygon (drawn at once, without seams), or null.</summary>
        public Vector3[] convex;
        public List<Vector2[]> triangles = NoTriangles;
    }

    private static readonly List<Vector2[]> NoTriangles = new List<Vector2[]>();

    private const int MaxFills = 48;
    private static readonly List<Fill> fills = new List<Fill>();
    private static int clock;

    private static Fill FillOf(IList<Vector2> local, float lift)
    {
        int hash = Hash(local);
        clock++;
        Fill oldest = null;
        for (int i = 0; i < fills.Count; i++)
        {
            Fill f = fills[i];
            if (ReferenceEquals(f.source, local))
            {
                if (f.hash != hash || !Mathf.Approximately(f.lift, lift)) Build(f, local, hash, lift);
                f.used = clock;
                return f;
            }
            if (oldest == null || f.used < oldest.used) oldest = f;
        }
        Fill made = fills.Count < MaxFills || oldest == null ? new Fill() : oldest;
        if (made != oldest) fills.Add(made);
        made.source = local;
        Build(made, local, hash, lift);
        made.used = clock;
        return made;
    }

    private static void Build(Fill f, IList<Vector2> local, int hash, float lift)
    {
        f.hash = hash;
        f.lift = lift;
        f.convex = null;
        f.triangles = NoTriangles;
        if (IsConvex(local))
        {
            f.convex = new Vector3[local.Count];
            for (int i = 0; i < local.Count; i++) f.convex[i] = new Vector3(local[i].x, lift, local[i].y);
        }
        else f.triangles = HousingGeometry.Triangulate(local);
    }

    /// <summary>Do all the corners turn the same way, without the outline crossing itself?</summary>
    private static bool IsConvex(IList<Vector2> pts)
    {
        int n = pts.Count, sign = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % n], c = pts[(i + 2) % n];
            float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            if (Mathf.Abs(cross) < 1e-9f) continue;
            int s = cross > 0f ? 1 : -1;
            if (sign == 0) sign = s;
            else if (s != sign) return false;
        }
        return sign != 0 && HousingGeometry.IsSimple(pts);
    }
}
#endif
