using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    /// <summary>
    /// Window > SimpleMovements > Dungeon Preview. Generates dungeons from a profile without entering play mode and
    /// shows each floor from above (by role, style, height, walking distance, width or zone), with the main path,
    /// doors and placements, hover details, the generation report, a batch tester over many seeds (success rate,
    /// retries, timings, room counts, loops, dead ends) and a button to build the preview into the open scene.
    /// </summary>
    public class DungeonPreviewWindow : EditorWindow
    {
        [SerializeField] private DungeonProfile profile;
        [SerializeField] private DungeonRequest request = new DungeonRequest { seed = 12345 };
        [SerializeField] private PreviewMode mode = PreviewMode.Roles;
        [SerializeField] private int floor;
        [SerializeField] private int pixelsPerCell = 5;
        [SerializeField] private bool showPath = true, showPlacements = true, showDoors = true;
        [SerializeField] private int batchCount = 50;

        private DungeonLayout layout;
        private string error;
        private Texture2D texture;
        private bool dirty = true;
        private Vector2 mapScroll, infoScroll;
        private string batchReport;
        private string hover = "";

        [MenuItem("Window/SimpleMovements/Dungeon Preview")]
        public static void Open()
        {
            GetWindow<DungeonPreviewWindow>("Dungeon Preview").Show();
        }

        public static void Open(DungeonProfile profile)
        {
            var w = GetWindow<DungeonPreviewWindow>("Dungeon Preview");
            w.profile = profile;
            w.Generate();
            w.Show();
        }

        private void OnDisable()
        {
            if (texture != null)
                DestroyImmediate(texture);
        }

        private void Generate()
        {
            error = null;
            layout = null;
            try
            {
                CompiledProfile compiled = CompiledProfile.Compile(profile);
                layout = DungeonPipeline.CreateDefault(compiled.CustomStages).Generate(request, compiled);
                floor = Mathf.Clamp(floor, 0, layout.Floors.Count - 1);
            }
            catch (Exception e)
            {
                error = e is DungeonGenerationException ? e.Message : e.ToString();
            }
            dirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (error != null)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            if (layout == null)
            {
                EditorGUILayout.HelpBox("Pick a profile (or leave it empty for defaults) and press Generate.", MessageType.Info);
                DrawBatch();
                return;
            }

            DrawFloorBar();
            EditorGUILayout.BeginHorizontal();
            DrawMap();
            DrawInfo();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            profile = (DungeonProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(DungeonProfile), false);
            EditorGUILayout.BeginHorizontal();
            request.seed = EditorGUILayout.IntField("Seed", request.seed);
            if (GUILayout.Button("Random", GUILayout.Width(70)))
            {
                request.seed = new System.Random().Next(1, int.MaxValue);
                Generate();
            }
            if (GUILayout.Button("Prev", GUILayout.Width(45)))
            {
                request.seed--;
                Generate();
            }
            if (GUILayout.Button("Next", GUILayout.Width(45)))
            {
                request.seed++;
                Generate();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            request.size = (SizeClass)EditorGUILayout.EnumPopup("Size", request.size);
            request.floorCount = EditorGUILayout.IntField("Floors (0 = profile)", request.floorCount);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            request.difficulty = EditorGUILayout.Slider("Difficulty", request.difficulty, 0.1f, 4f);
            request.overrideStyle = EditorGUILayout.ToggleLeft("Force style", request.overrideStyle, GUILayout.Width(90));
            using (new EditorGUI.DisabledScope(!request.overrideStyle))
                request.style = (FloorStyle)EditorGUILayout.EnumPopup(request.style, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate", GUILayout.Height(24)))
                Generate();
            using (new EditorGUI.DisabledScope(layout == null))
            {
                if (GUILayout.Button("Build In Scene", GUILayout.Height(24)))
                    BuildInScene();
            }
            if (GUILayout.Button("Clear Scene Build", GUILayout.Height(24)))
                ClearSceneBuild();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawFloorBar()
        {
            EditorGUILayout.BeginHorizontal();
            var labels = new string[layout.Floors.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = $"{i}: {layout.Floors[i].Spec.Style}";
            int f = GUILayout.Toolbar(floor, labels);
            if (f != floor)
            {
                floor = f;
                dirty = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var m = (PreviewMode)EditorGUILayout.EnumPopup(mode, GUILayout.Width(110));
            bool p = GUILayout.Toggle(showPath, "Main path", EditorStyles.miniButtonLeft);
            bool d = GUILayout.Toggle(showDoors, "Doors", EditorStyles.miniButtonMid);
            bool pl = GUILayout.Toggle(showPlacements, "Placements", EditorStyles.miniButtonRight);
            GUILayout.Label("Zoom", GUILayout.Width(36));
            int z = (int)GUILayout.HorizontalSlider(pixelsPerCell, 2, 12, GUILayout.Width(90));
            if (m != mode || p != showPath || d != showDoors || pl != showPlacements || z != pixelsPerCell)
            {
                mode = m;
                showPath = p;
                showDoors = d;
                showPlacements = pl;
                pixelsPerCell = z;
                dirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMap()
        {
            if (dirty || texture == null)
            {
                texture = DungeonPreviewTexture.Draw(layout, floor, mode, pixelsPerCell, showPath, showPlacements, showDoors, texture);
                dirty = false;
            }
            mapScroll = EditorGUILayout.BeginScrollView(mapScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Rect r = GUILayoutUtility.GetRect(texture.width, texture.height, GUILayout.Width(texture.width), GUILayout.Height(texture.height));
            GUI.DrawTexture(r, texture, ScaleMode.StretchToFill, false);

            Event e = Event.current;
            if (r.Contains(e.mousePosition))
            {
                int cx = Mathf.FloorToInt((e.mousePosition.x - r.x) / pixelsPerCell);
                int cy = Mathf.FloorToInt((r.yMax - e.mousePosition.y) / pixelsPerCell);
                hover = Describe(cx, cy);
                if (e.type == EventType.MouseMove)
                    Repaint();
            }
            EditorGUILayout.EndScrollView();
            wantsMouseMove = true;
        }

        private string Describe(int x, int y)
        {
            FloorLayout f = layout.Floors[floor];
            TileGrid g = f.Grid;
            if (!g.InBounds(x, y))
                return "";
            int i = g.Index(x, y);
            var sb = new StringBuilder();
            sb.AppendLine($"Cell ({x}, {y}): {g.Type[i]}  flags: {g.Flags[i]}");
            if (g.IsWalkable(i))
            {
                sb.AppendLine($"Floor {g.FloorHeight[i]:0.00} m, ceiling {g.CeilingHeight[i]:0.00} m, wall distance {f.WallDistance?[i]:0.0}");
                int d = f.DistanceFromArrival != null ? f.DistanceFromArrival[i] : -1;
                sb.AppendLine($"Walk from arrival: {d} cells (from entrance: {(d >= 0 ? f.GlobalDistanceOffset + d : -1)})");
            }
            Area a = f.AreaAt(i);
            if (a != null)
            {
                sb.AppendLine(a.ToString());
                sb.AppendLine($"Style {a.Style}, depth {a.Depth}, progress {a.Progress:0.00}, main path {a.OnMainPath}, leaf {a.IsLeaf}, hub {a.IsHub}");
                sb.AppendLine($"Connections {a.Connections.Count}, links {a.Links.Count}, openness {a.Openness:0.00}, difficulty {a.Difficulty:0.00}");
            }
            else if (g.Connection[i] >= 0)
            {
                sb.AppendLine(f.Connections[g.Connection[i]].ToString());
            }
            return sb.ToString();
        }

        private void DrawInfo()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            infoScroll = EditorGUILayout.BeginScrollView(infoScroll);
            EditorGUILayout.LabelField("Hover", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(hover) ? "Move the mouse over the map." : hover, MessageType.None);

            FloorLayout f = layout.Floors[floor];
            EditorGUILayout.LabelField("Floor", EditorStyles.boldLabel);
            int loops = f.Connections.Count(c => c.IsLoop && !c.Failed);
            int leaves = f.Areas.Count(a => a.IsLeaf);
            EditorGUILayout.HelpBox(
                $"{f.Spec.Style}, footprint {f.Spec.Footprint.width}x{f.Spec.Footprint.height} cells\n" +
                $"Openness {f.Spec.Openness:0.00}, complexity {f.Spec.Complexity:0.00}, difficulty {f.Spec.Difficulty:0.00}\n" +
                $"{f.Areas.Count} areas, {f.Connections.Count} connections ({loops} loops), {leaves} dead ends\n" +
                $"Main path on this floor: {f.MainPathCells.Count} cells", MessageType.None);

            EditorGUILayout.LabelField("Dungeon", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(layout.Describe() + "\n" + layout.Report, MessageType.None);

            EditorGUILayout.LabelField("Legend", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Green room: entrance - teal: exit - blue: arrival stairs - purple: stairs down - pink: drops\n" +
                "Red room: boss - gold: treasure - light green: rest - orange: arena - lavender: shrine - magenta: secret\n" +
                "Magenta cells: shafts - orange dots: doors (red: secret)\n" +
                "Dots: red mob, dark red boss, gold loot, pale light, green spawn, cyan portals, orange hazard, violet POI",
                MessageType.None);

            DrawBatch();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ batch

        private void DrawBatch()
        {
            EditorGUILayout.LabelField("Batch test", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            batchCount = Mathf.Clamp(EditorGUILayout.IntField("Seeds", batchCount), 1, 2000);
            if (GUILayout.Button("Run", GUILayout.Width(60)))
                RunBatch();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(batchReport))
                EditorGUILayout.HelpBox(batchReport, MessageType.None);
        }

        private void RunBatch()
        {
            CompiledProfile compiled = CompiledProfile.Compile(profile);
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault(compiled.CustomStages);
            int ok = 0, attempts = 0, retried = 0;
            var ms = new List<double>();
            var areas = new List<int>();
            var loops = new List<int>();
            var leafShare = new List<float>();
            var floors = new List<int>();
            var placements = new List<int>();
            var mainPath = new List<int>();
            var styles = new Dictionary<FloorStyle, int>();
            var failures = new List<string>();
            try
            {
                for (int i = 0; i < batchCount; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Dungeon batch test", $"Seed {i + 1} / {batchCount}", i / (float)batchCount))
                        break;
                    DungeonRequest r = request.Clone();
                    r.seed = request.seed + i * 7919;
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        DungeonLayout l = pipeline.Generate(r, compiled);
                        ms.Add(watch.Elapsed.TotalMilliseconds);
                        ok++;
                        attempts += l.Report.Attempts;
                        if (l.Report.Attempts > 1)
                            retried++;
                        floors.Add(l.Floors.Count);
                        placements.Add(l.Placements.Count);
                        mainPath.Add(l.MainPath.Count);
                        foreach (FloorLayout f in l.Floors)
                        {
                            areas.Add(f.Areas.Count);
                            loops.Add(f.Connections.Count(c => c.IsLoop && !c.Failed));
                            leafShare.Add(f.Areas.Count(a => a.IsLeaf) / (float)Mathf.Max(1, f.Areas.Count));
                            styles.TryGetValue(f.Spec.Style, out int n);
                            styles[f.Spec.Style] = n + 1;
                        }
                    }
                    catch (DungeonGenerationException e)
                    {
                        failures.Add($"seed {r.seed}: {e.Message.Split('\n')[0]}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{ok} / {ok + failures.Count} generated ({retried} needed a retry, {(ok > 0 ? attempts / (float)ok : 0f):0.00} attempts each)");
            if (ms.Count > 0)
                sb.AppendLine($"Time: avg {ms.Average():0.0} ms, max {ms.Max():0.0} ms (single thread; floors run in parallel in play mode)");
            if (areas.Count > 0)
            {
                sb.AppendLine($"Floors per dungeon: {floors.Average():0.0} [{floors.Min()}-{floors.Max()}]");
                sb.AppendLine($"Areas per floor: {areas.Average():0.0} [{areas.Min()}-{areas.Max()}]");
                sb.AppendLine($"Loops per floor: {loops.Average():0.0}, dead-end share {leafShare.Average():0.00}");
                sb.AppendLine($"Main path: {mainPath.Average():0.0} areas, placements: {placements.Average():0}");
                sb.AppendLine("Styles: " + string.Join(", ", styles.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            }
            foreach (string f in failures.Take(10))
                sb.AppendLine("FAILED " + f);
            batchReport = sb.ToString();
        }

        // ------------------------------------------------------------------ scene build

        private const string PreviewName = "Dungeon Preview (editor)";

        private void BuildInScene()
        {
            ClearSceneBuild();
            var go = new GameObject(PreviewName);
            Undo.RegisterCreatedObjectUndo(go, "Build dungeon preview");
            var manager = go.AddComponent<DungeonManager>();
            manager.profile = profile;
            try
            {
                DungeonInstance instance = manager.GenerateImmediate(request);
                Selection.activeGameObject = instance.gameObject;
                SceneView.lastActiveSceneView?.Frame(new Bounds(instance.PlayerSpawn.position, Vector3.one * 30f), false);
            }
            catch (Exception e)
            {
                error = e.Message;
            }
        }

        private static void ClearSceneBuild()
        {
            GameObject existing = GameObject.Find(PreviewName);
            if (existing == null)
                return;
            // OnDestroy doesn't run in edit mode: release the generated meshes and materials first.
            var manager = existing.GetComponent<DungeonManager>();
            if (manager != null)
                manager.Clear();
            Undo.DestroyObjectImmediate(existing);
        }
    }
}
