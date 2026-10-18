#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HubWorld.EditorTools
{
    /// <summary>
    /// One-click setup of the hub world: the settings asset, a ready City scene (walls, gate, spawn, director), turning a
    /// terrain scene into the World scene (the exterior copy of the city, the return gate, the arrival point), gates and
    /// a check of the whole setup.
    /// </summary>
    public static class HubWorldSetupMenu
    {
        private const string Menu = "Tools/SimpleMovements/Hub World/";
        private const float WallHeight = 9f;
        private const float WallThickness = 2.5f;
        private const float GateWidth = 9f;

        // ------------------------------------------------------------------ settings

        [MenuItem(SMMenu.Root + "Project Setup/Create Hub World Settings (Resources)", false, SMMenu.ProjectSetup + 6)]
        public static HubWorldSettings CreateSettings()
        {
            // Cycle, zones, penalties, duels, dungeon scenes: Resources/SimpleMovements/Settings/HubWorldSettings.
            return SMAssetsEditor.FindOrCreate<HubWorldSettings>(SMAssets.Settings, "HubWorldSettings");
        }

        internal static HubWorldSettings Settings()
        {
            HubWorldSettings s = SMAssets.Load<HubWorldSettings>(SMAssets.Settings, "HubWorldSettings");
            return s != null ? s : HubWorldSettings.Instance;
        }

        // ------------------------------------------------------------------ the City scene

        [MenuItem(SMMenu.Root + "Hub World/Build City Scene...", false, SMMenu.HubWorld + 0)]
        public static void BuildCityScene()
        {
            HubWorldSettings settings = Settings();
            GameObject playerPrefab = Selection.activeObject as GameObject;
            if (playerPrefab != null && (!AssetDatabase.Contains(playerPrefab) || playerPrefab.GetComponentInChildren<PlayerStatusController>(true) == null))
                playerPrefab = null;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            string path = EditorUtility.SaveFilePanelInProject("City scene", string.IsNullOrEmpty(settings.citySceneName) ? "City" : settings.citySceneName,
                "unity", "Where to save the permanent hub city scene.");
            if (string.IsNullOrEmpty(path))
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ZoneSettings z = settings.zones;
            Vector3 center = new Vector3(z.cityCenter.x, z.cityGroundHeight, z.cityCenter.y);
            float radius = Mathf.Max(20f, z.cityWallRadius);

            var city = new GameObject("City");
            city.transform.position = center;
            Material stone = Mat("City Stone", new Color(0.55f, 0.52f, 0.47f));
            Material floor = Mat("City Floor", new Color(0.4f, 0.38f, 0.34f));
            // The floor follows the wall ring (same polygon), reaching a few metres past the walls outside the gate.
            GameObject ground = Prism("Ground", city.transform, radius + WallThickness * 0.5f + GroundRim, WallSegments(radius), -1f, 0f, floor);
            ground.isStatic = true;
            BuildWalls(city.transform, radius, stone, true);

            // The main gate, south side: walking through it leads out into the world.
            Vector3 gatePos = center + Vector3.back * radius;
            GameObject gate = BuildGate("City Gate", gatePos, Quaternion.identity, RealmGate.GateKind.CityGate, stone,
                new Color(0.35f, 1f, 0.6f, 0.55f));
            gate.transform.SetParent(city.transform, true);

            var realmGo = new GameObject("Realm: City");
            RealmScene realm = realmGo.AddComponent<RealmScene>();
            realm.realm = RealmKind.City;
            realm.arrivalPoint = Point("Arrival (inside the gate)", realmGo.transform, gatePos + Vector3.forward * 12f, Quaternion.identity);
            realm.spawnPoint = Point("Spawn (city square)", realmGo.transform, center + Vector3.forward * 4f, Quaternion.identity);

            var directorGo = new GameObject("Hub World Director");
            directorGo.AddComponent<HubWorldDirector>();

            Camera sceneCamera = Object.FindAnyObjectByType<Camera>();
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                player.transform.SetPositionAndRotation(realm.spawnPoint.position + Vector3.up * 0.1f, realm.spawnPoint.rotation);
                // The player brings its own camera: the scene's default one would be a second camera and audio listener.
                if (sceneCamera != null && player.GetComponentInChildren<Camera>(true) != null)
                {
                    Object.DestroyImmediate(sceneCamera.gameObject);
                    sceneCamera = null;
                }
            }
            if (sceneCamera != null)
                sceneCamera.transform.SetPositionAndRotation(center + new Vector3(0f, 25f, -radius - 25f), Quaternion.Euler(30f, 0f, 0f));

            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path, true);
            SetSceneName(settings, true, Path.GetFileNameWithoutExtension(path));
            Selection.activeObject = realmGo;
            EditorUtility.DisplayDialog("Hub World - City scene",
                $"Saved {path} and made it the first scene in Build Settings.\n\n" +
                "It has: the city ground and walls (placeholders - replace them with your city, keeping the gate), the City Gate, " +
                "the arrival and spawn points (Realm: City) and the Hub World Director.\n\n" +
                (playerPrefab != null ? "The selected player prefab was placed at the spawn.\n\n" : "Place your player at the spawn (select a player prefab before running this to have it placed).\n\n") +
                "Next: open your terrain scene and run Tools > SimpleMovements > Hub World > Make Open Scene The World Scene.", "OK");
        }

        // ------------------------------------------------------------------ the World scene

        [MenuItem(SMMenu.Root + "Hub World/Make Open Scene The World Scene", false, SMMenu.HubWorld + 1)]
        public static void MakeWorldScene()
        {
            HubWorldSettings settings = Settings();
            Scene scene = SceneManager.GetActiveScene();
            if (Object.FindAnyObjectByType<HubWorldDirector>() != null &&
                !EditorUtility.DisplayDialog("Hub World - World scene", "This scene has a Hub World Director, which belongs in the City scene. Continue anyway?", "Continue", "Cancel"))
                return;
            RealmScene existing = Object.FindObjectsByType<RealmScene>(FindObjectsInactive.Include).FirstOrDefault(r => r.realm == RealmKind.World);
            ZoneSettings z = settings.zones;
            Vector3 center = new Vector3(z.cityCenter.x, z.cityGroundHeight, z.cityCenter.y);
            float radius = Mathf.Max(20f, z.cityWallRadius);
            var report = new StringBuilder();

            Vector3 gatePos = center + Vector3.back * radius;
            if (existing == null)
            {
                Undo.IncrementCurrentGroup();
                BuildExterior(center, radius, gatePos);

                var realmGo = new GameObject("Realm: World");
                Undo.RegisterCreatedObjectUndo(realmGo, "Hub World");
                RealmScene realm = realmGo.AddComponent<RealmScene>();
                realm.realm = RealmKind.World;
                realm.hideWhileLocalPlayerAway = false;
                realm.arrivalPoint = Point("Arrival (outside the return gate)", realmGo.transform, gatePos + Vector3.back * 14f, Quaternion.Euler(0f, 180f, 0f));
                report.AppendLine("Added the exterior copy of the city (walls, sealed inside), the Return Gate and the arrival point (Realm: World).");
            }
            else if (EditorUtility.DisplayDialog("Hub World - World scene",
                         "This scene already has a Realm: World.\n\nRebuild the exterior copy of the city (walls, sealed inside, Return Gate) from the current Hub World Settings? " +
                         "The old 'City Exterior' object is replaced (Undo restores it); the arrival point is kept.", "Rebuild", "Keep"))
            {
                Undo.IncrementCurrentGroup();
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "City Exterior")
                        Undo.DestroyObjectImmediate(root);
                BuildExterior(center, radius, gatePos);
                report.AppendLine("Rebuilt the exterior copy of the city.");
            }
            else
            {
                report.AppendLine("The scene already has a Realm: World - nothing added.");
            }

            foreach (EndlessTerrain t in Object.FindObjectsByType<EndlessTerrain>(FindObjectsInactive.Include))
            {
                if (t.viewer != null)
                {
                    Undo.RecordObject(t, "Hub World");
                    t.viewer = null;
                    report.AppendLine($"Cleared the viewer of '{t.name}' (the director sets it to the player out in the world).");
                }
            }
            if (Object.FindAnyObjectByType<TerrainGenerator>() == null)
            {
                Block("Placeholder Ground (delete once the terrain is set up)", null, center + new Vector3(0f, -0.5f, 0f),
                    new Vector3(z.keepClearRadius * 2.5f, 1f, z.keepClearRadius * 2.5f), Quaternion.identity, Mat("Placeholder Ground", new Color(0.3f, 0.45f, 0.25f)));
                report.AppendLine("No Terrain Generator here: added a placeholder ground. Add your TerrainGenerator, EndlessTerrain (Viewer empty) and WeatherSystem to this scene.");
            }
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                report.AppendLine($"Note: '{c.name}' is a camera in the World scene - the player's camera usually lives with the player in the City scene; remove this one if it is not needed.");
            foreach (PlayerStatusController p in Object.FindObjectsByType<PlayerStatusController>(FindObjectsInactive.Include))
                report.AppendLine($"Note: '{p.name}' is a player in the World scene - players start in the City scene (move it there).");

            EditorSceneManager.MarkSceneDirty(scene);
            if (string.IsNullOrEmpty(scene.path))
            {
                string path = EditorUtility.SaveFilePanelInProject("World scene", "World", "unity", "Where to save the World scene.");
                if (string.IsNullOrEmpty(path))
                    return;
                EditorSceneManager.SaveScene(scene, path);
            }
            else
            {
                EditorSceneManager.SaveScene(scene);
            }
            AddToBuildSettings(scene.path, false);
            SetSceneName(settings, false, scene.name);
            report.AppendLine($"Saved and added '{scene.name}' to Build Settings.");
            EditorUtility.DisplayDialog("Hub World - World scene", report.ToString(), "OK");
        }

        /// <summary>The city as seen from the world: the wall ring, a solid core exactly inside it, and the Return Gate.</summary>
        private static GameObject BuildExterior(Vector3 center, float radius, Vector3 gatePos)
        {
            var exterior = new GameObject("City Exterior");
            Undo.RegisterCreatedObjectUndo(exterior, "Hub World");
            exterior.transform.position = center;
            Material stone = Mat("City Stone", new Color(0.55f, 0.52f, 0.47f));
            BuildWalls(exterior.transform, radius, stone, false);
            // A solid core exactly inside the wall ring (same polygon as the walls' inner faces, from below the ground to
            // just under the wall tops) so nobody wanders into the empty inside.
            Prism("Sealed Interior", exterior.transform, radius - WallThickness * 0.5f + 0.05f, WallSegments(radius), -2f, WallHeight - 0.6f,
                Mat("City Roofs", new Color(0.35f, 0.28f, 0.25f))).isStatic = true;
            GameObject gate = BuildGate("Return Gate", gatePos, Quaternion.identity, RealmGate.GateKind.ReturnGate, stone, new Color(0.4f, 0.7f, 1f, 0.6f));
            gate.transform.SetParent(exterior.transform, true);
            return exterior;
        }

        // ------------------------------------------------------------------ gates

        [MenuItem(SMMenu.Root + "Hub World/Add City Gate At Scene View Pivot", false, SMMenu.HubWorld + 20)]
        private static void AddCityGate() => AddGate(RealmGate.GateKind.CityGate);

        [MenuItem(SMMenu.Root + "Hub World/Add Return Gate At Scene View Pivot", false, SMMenu.HubWorld + 21)]
        private static void AddReturnGate() => AddGate(RealmGate.GateKind.ReturnGate);

        private static void AddGate(RealmGate.GateKind kind)
        {
            Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            Selection.activeObject = CreateGate(kind, pos);
        }

        /// <summary>A placeholder gate (pillars, lintel, glowing veil, the trigger and its <see cref="RealmGate"/>) at <paramref name="position"/>.</summary>
        internal static GameObject CreateGate(RealmGate.GateKind kind, Vector3 position)
        {
            GameObject gate = BuildGate(kind == RealmGate.GateKind.CityGate ? "City Gate" : "Return Gate", position, Quaternion.identity, kind,
                Mat("City Stone", new Color(0.55f, 0.52f, 0.47f)), kind == RealmGate.GateKind.CityGate ? new Color(0.35f, 1f, 0.6f, 0.55f) : new Color(0.4f, 0.7f, 1f, 0.6f));
            Undo.RegisterCreatedObjectUndo(gate, "Add gate");
            return gate;
        }

        /// <summary>
        /// The World scene's copy of the city as seen from outside (walls, sealed inside, Return Gate), at the city centre
        /// of the Hub World Settings, in the active scene.
        /// </summary>
        internal static GameObject CreateExterior()
        {
            ZoneSettings z = Settings().zones;
            Vector3 center = new Vector3(z.cityCenter.x, z.cityGroundHeight, z.cityCenter.y);
            float radius = Mathf.Max(20f, z.cityWallRadius);
            return BuildExterior(center, radius, center + Vector3.back * radius);
        }

        /// <summary>Where the city's gate stands (south side of the walls), from the Hub World Settings.</summary>
        internal static Vector3 CityGatePosition(out Vector3 center)
        {
            ZoneSettings z = Settings().zones;
            center = new Vector3(z.cityCenter.x, z.cityGroundHeight, z.cityCenter.y);
            return center + Vector3.back * Mathf.Max(20f, z.cityWallRadius);
        }

        // ------------------------------------------------------------------ validation

        [MenuItem(SMMenu.Root + "Validate/Hub World", false, SMMenu.Validate + 25)]
        public static void Validate()
        {
            HubWorldSettings settings = Settings();
            var errors = new List<string>();
            var warnings = new List<string>();
            var ok = new List<string>();

            if (SMAssets.Load<HubWorldSettings>(SMAssets.Settings, "HubWorldSettings") == null)
                warnings.Add("No Hub World Settings asset (Resources/SimpleMovements/Settings/HubWorldSettings): the defaults are used (Create Settings Asset).");
            else
                ok.Add("Settings asset found.");

            var build = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
            if (!build.Contains(settings.citySceneName))
                errors.Add($"The City scene '{settings.citySceneName}' is not in Build Settings.");
            else if (build.IndexOf(settings.citySceneName) != 0)
                warnings.Add($"The City scene '{settings.citySceneName}' is not the first scene in Build Settings (fine if a bootstrap scene loads it).");
            else
                ok.Add($"City scene '{settings.citySceneName}' is the first scene.");
            if (!build.Contains(settings.worldSceneName))
                errors.Add($"The World scene '{settings.worldSceneName}' is not in Build Settings.");
            else
                ok.Add($"World scene '{settings.worldSceneName}' is in Build Settings.");
            foreach (DimensionalModifier m in settings.modifiers)
                if (m != null && !string.IsNullOrEmpty(m.worldSceneOverride) && !build.Contains(m.worldSceneOverride))
                    errors.Add($"Modifier '{m.displayName}' uses the scene '{m.worldSceneOverride}', which is not in Build Settings.");

            // The open scenes.
            RealmScene[] realms = Object.FindObjectsByType<RealmScene>(FindObjectsInactive.Include);
            foreach (RealmScene r in realms)
            {
                if (r.realm == RealmKind.City)
                {
                    if (Object.FindObjectsByType<RealmGate>(FindObjectsInactive.Include).All(g => g.kind != RealmGate.GateKind.CityGate || g.gameObject.scene != r.gameObject.scene))
                        warnings.Add($"City scene '{r.gameObject.scene.name}': no City Gate - players cannot go out.");
                    if (Object.FindObjectsByType<HubWorldDirector>(FindObjectsInactive.Include).All(d => d.gameObject.scene != r.gameObject.scene))
                        warnings.Add($"City scene '{r.gameObject.scene.name}': no Hub World Director (put it in the city, or in a bootstrap scene).");
                    if (Object.FindObjectsByType<Mob>(FindObjectsInactive.Include).Any(m => m.gameObject.scene == r.gameObject.scene))
                        warnings.Add($"City scene '{r.gameObject.scene.name}': has mobs - the city should only have NPCs.");
                    ok.Add($"City scene '{r.gameObject.scene.name}' is marked (Realm: City).");
                }
                else if (r.realm == RealmKind.World)
                {
                    if (Object.FindObjectsByType<RealmGate>(FindObjectsInactive.Include).All(g => g.kind != RealmGate.GateKind.ReturnGate || g.gameObject.scene != r.gameObject.scene))
                        warnings.Add($"World scene '{r.gameObject.scene.name}': no Return Gate - players can only come back when the city jumps.");
                    foreach (TerrainGenerator tg in Object.FindObjectsByType<TerrainGenerator>(FindObjectsInactive.Include))
                        if (tg.gameObject.scene == r.gameObject.scene && settings.zones.flattenCityGround && tg.SeaLevel >= settings.zones.cityGroundHeight - 0.5f)
                            warnings.Add($"World scene '{r.gameObject.scene.name}': the terrain's water level ({tg.SeaLevel:0.#}) is not below City Ground Height ({settings.zones.cityGroundHeight:0.#}) - the city's levelled ground would be under the sea. Raise City Ground Height (Hub World Settings ▸ Zones) above the water level, then rebuild the city exterior.");
                    foreach (EndlessTerrain t in Object.FindObjectsByType<EndlessTerrain>(FindObjectsInactive.Include))
                        if (t.gameObject.scene == r.gameObject.scene && t.viewer != null)
                            warnings.Add($"World scene: '{t.name}' has a Viewer - leave it empty (the director sets it).");
                    ok.Add($"World scene '{r.gameObject.scene.name}' is marked (Realm: World).");
                }
            }
            if (realms.Length == 0)
                warnings.Add("No Realm Scene in the open scene(s): open the City or World scene to check it too.");
            foreach (Portal p in Object.FindObjectsByType<Portal>(FindObjectsInactive.Include))
                if (p.LeadsToDungeon && p.Manager == null && p.Profile == null)
                    errors.Add($"Portal '{p.name}': no Dungeon Manager or Dungeon Profile.");
            if (settings.dungeonsInOwnScenes && settings.dungeonScenes != null && settings.dungeonScenes.slotOrigin.y > -1500f && settings.unloadWorldWhenEmpty == false)
                warnings.Add("Dungeon slots are near the world's heights while the world stays loaded: move Dungeon Scenes ▸ Slot Origin below the terrain.");
            if (settings.CycleSeconds < 120f)
                warnings.Add($"The cycle is only {settings.cycleMinutes} minutes (fine for testing).");

            var sb = new StringBuilder();
            foreach (string e in errors) sb.AppendLine("✗ " + e);
            foreach (string w in warnings) sb.AppendLine("! " + w);
            foreach (string o in ok) sb.AppendLine("✓ " + o);
            if (errors.Count > 0) Debug.LogError("Hub World setup:\n" + sb);
            else if (warnings.Count > 0) Debug.LogWarning("Hub World setup:\n" + sb);
            else Debug.Log("Hub World setup:\n" + sb);
            EditorUtility.DisplayDialog("Hub World setup", sb.Length > 0 ? sb.ToString() : "Nothing to check.", "OK");
        }

        [MenuItem(SMMenu.Root + "Hub World/Help", false, SMMenu.HubWorld + 40)]
        private static void Help()
        {
            EditorUtility.DisplayDialog("Hub World",
                "1. Create Settings Asset (Resources): cycle length, warnings, zones, penalties, duels, dungeon scenes.\n" +
                "2. Build City Scene (select your player prefab first to have it placed): the permanent city with its gate and the director.\n" +
                "3. Open your terrain scene and Make Open Scene The World Scene: the city's exterior copy, the return gate, the arrival point.\n" +
                "4. Validate Setup, then press Play in the City scene.\n\n" +
                "The guide: Documentation/HubWorld/README.md.", "OK");
        }

        // ------------------------------------------------------------------ building blocks

        /// <summary>A ring of wall blocks around the parent, open at the south (-Z) for the gate.</summary>
        private static void BuildWalls(Transform parent, float radius, Material material, bool towers)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(parent, false);
            int segments = WallSegments(radius);
            float step = 360f / segments;
            float gateHalfAngle = Mathf.Rad2Deg * (GateWidth * 0.5f + 1f) / radius;
            float length = 2f * Mathf.PI * radius / segments + 0.4f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * step; // 0 = north (+Z), clockwise
                float fromSouth = Mathf.Abs(Mathf.DeltaAngle(angle, 180f));
                if (fromSouth < gateHalfAngle + step * 0.5f)
                    continue;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 pos = rot * Vector3.forward * radius + Vector3.up * (WallHeight * 0.5f);
                GameObject block = Block($"Wall {i}", walls, pos, new Vector3(length, WallHeight, WallThickness), rot, material);
                block.isStatic = true;
                if (towers && i % 6 == 0)
                    Block($"Tower {i}", walls, pos + Vector3.up * 2f, new Vector3(5f, WallHeight + 4f, 5f), rot, material).isStatic = true;
            }
        }

        /// <summary>Number of wall blocks around a ring of <paramref name="radius"/> (the floor and the sealed core use the same).</summary>
        private static int WallSegments(float radius) => Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radius / 6f), 16, 96);

        /// <summary>How far the City's floor reaches past the outer face of its walls.</summary>
        private const float GroundRim = 6f;

        /// <summary>
        /// A solid polygon prism lined up with the wall ring: <paramref name="segments"/> flat sides, each facing the
        /// middle of a wall block, <paramref name="apothem"/> metres from the centre, from <paramref name="bottom"/> to
        /// <paramref name="top"/> (local y). Saved as a mesh asset next to the hub world materials; has a mesh collider.
        /// </summary>
        private static GameObject Prism(string name, Transform parent, float apothem, int segments, float bottom, float top, Material material)
        {
            Mesh mesh = PrismMesh(apothem, segments, bottom, top);
            string folder = SMAssetsEditor.Folder(SMAssets.HubWorld);
            string path = $"{folder}/{name} {apothem:0.##}m {segments} sides.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            else
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
            }
            AssetDatabase.SaveAssets();

            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        internal static Mesh PrismMesh(float apothem, int segments, float bottom, float top)
        {
            float step = 2f * Mathf.PI / segments;
            // Corners between two wall blocks: wall i faces the angle i * step (0 = +Z, clockwise), so the corners sit
            // half a step either side and the sides are flat chords at the apothem.
            float cornerRadius = apothem / Mathf.Cos(step * 0.5f);
            var corners = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * step;
                corners[i] = new Vector3(Mathf.Sin(a) * cornerRadius, 0f, Mathf.Cos(a) * cornerRadius);
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Caps (own vertices so the edges stay sharp).
            for (int cap = 0; cap < 2; cap++)
            {
                bool isTop = cap == 0;
                float y = isTop ? top : bottom;
                int centre = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(isTop ? Vector3.up : Vector3.down);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < segments; i++)
                {
                    vertices.Add(corners[i] + Vector3.up * y);
                    normals.Add(isTop ? Vector3.up : Vector3.down);
                    uvs.Add(new Vector2(corners[i].x, corners[i].z) * 0.1f);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = centre + 1 + i, b = centre + 1 + (i + 1) % segments;
                    // Corners run clockwise seen from above: (centre, a, b) faces up.
                    if (isTop) triangles.AddRange(new[] { centre, a, b });
                    else triangles.AddRange(new[] { centre, b, a });
                }
            }
            // Sides.
            float height = top - bottom;
            for (int i = 0; i < segments; i++)
            {
                Vector3 c0 = corners[i], c1 = corners[(i + 1) % segments];
                Vector3 normal = ((c0 + c1) * 0.5f).normalized;
                int v = vertices.Count;
                vertices.Add(c0 + Vector3.up * bottom);
                vertices.Add(c1 + Vector3.up * bottom);
                vertices.Add(c1 + Vector3.up * top);
                vertices.Add(c0 + Vector3.up * top);
                float width = Vector3.Distance(c0, c1);
                for (int k = 0; k < 4; k++)
                    normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(width * 0.1f, 0f));
                uvs.Add(new Vector2(width * 0.1f, height * 0.1f));
                uvs.Add(new Vector2(0f, height * 0.1f));
                // Seen from outside c0 is on the right and c1 on the left: clockwise (Unity's front) is v, v+1, v+2.
                triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Two pillars, a lintel and a glowing veil with the gate's trigger.</summary>
        private static GameObject BuildGate(string name, Vector3 position, Quaternion rotation, RealmGate.GateKind kind, Material stone, Color glow)
        {
            var gate = new GameObject(name);
            gate.transform.SetPositionAndRotation(position, rotation);
            float half = GateWidth * 0.5f;
            Block("Pillar L", gate.transform, new Vector3(-half - 1f, WallHeight * 0.5f + 0.5f, 0f), new Vector3(2f, WallHeight + 1f, WallThickness + 1f), Quaternion.identity, stone);
            Block("Pillar R", gate.transform, new Vector3(half + 1f, WallHeight * 0.5f + 0.5f, 0f), new Vector3(2f, WallHeight + 1f, WallThickness + 1f), Quaternion.identity, stone);
            Block("Lintel", gate.transform, new Vector3(0f, WallHeight + 0.5f, 0f), new Vector3(GateWidth + 4f, 2f, WallThickness + 1f), Quaternion.identity, stone);
            GameObject veil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            veil.name = "Veil";
            veil.transform.SetParent(gate.transform, false);
            veil.transform.localPosition = new Vector3(0f, (WallHeight - 0.5f) * 0.5f, 0f);
            veil.transform.localScale = new Vector3(GateWidth, WallHeight - 0.5f, 1f);
            Object.DestroyImmediate(veil.GetComponent<Collider>());
            veil.GetComponent<Renderer>().sharedMaterial = Mat(kind == RealmGate.GateKind.CityGate ? "City Gate Veil" : "Return Gate Veil", glow, true);
            var trigger = gate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2.5f, 0f);
            trigger.size = new Vector3(GateWidth, 5f, 2.5f);
            RealmGate rg = gate.AddComponent<RealmGate>();
            rg.kind = kind;
            return gate;
        }

        private static GameObject Block(string name, Transform parent, Vector3 localPosition, Vector3 size, Quaternion localRotation, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = size;
            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        internal static Transform Point(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.transform;
        }

        /// <summary>A simple lit material saved in Resources/SimpleMovements/World/Hub World (so scene objects keep it), reused by name.</summary>
        private static Material Mat(string name, Color color, bool transparent = false)
        {
            string folder = SMAssetsEditor.Folder(SMAssets.HubWorld);
            string path = $"{folder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
                return m;
            Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find(transparent ? "Sprites/Default" : "Standard");
            m = new Material(shader) { color = color };
            if (transparent && shader != null && shader.name.Contains("Universal"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void AddToBuildSettings(string path, bool first)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == path);
            var entry = new EditorBuildSettingsScene(path, true);
            if (first)
                scenes.Insert(0, entry);
            else
                scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetSceneName(HubWorldSettings settings, bool city, string sceneName)
        {
            if (settings == null || !AssetDatabase.Contains(settings))
                return;
            Undo.RecordObject(settings, "Hub World scene name");
            if (city)
                settings.citySceneName = sceneName;
            else
                settings.worldSceneName = sceneName;
            EditorUtility.SetDirty(settings);
        }
    }
}
#endif
