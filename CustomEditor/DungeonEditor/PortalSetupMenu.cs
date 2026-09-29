using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    /// <summary>
    /// Menu items for the world portal: build a ready-to-use portal prefab (visuals, trigger, light, Portal component
    /// wired to a Dungeon Manager prefab), and check the whole terrain -> portal -> dungeon setup of the open scene.
    /// </summary>
    public static class PortalSetupMenu
    {
        [MenuItem("Tools/SimpleMovements/Dungeon/Create World Portal Prefab...")]
        public static void CreatePortalPrefabMenu()
        {
            string folder = DungeonSetupMenu.PickAssetFolder("Folder for the world portal prefab");
            if (folder == null)
                return;
            DungeonManager manager = FindManagerPrefab();
            if (manager == null)
            {
                bool proceed = EditorUtility.DisplayDialog("World portal",
                    "No Dungeon Manager prefab was found (select one in the Project window, or run Create Default Setup first).\n\n" +
                    "Create the portal anyway? You'll have to assign a Dungeon Manager or Dungeon Profile on its Portal component.", "Create", "Cancel");
                if (!proceed)
                    return;
            }
            GameObject prefab = CreatePortalPrefab(folder, manager, null);
            OfferToAddToTerrain(prefab);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        /// <summary>
        /// Builds the portal prefab (stone frame, glowing surface, light, trigger box, Portal component) with its two
        /// materials in <paramref name="folder"/>, and returns the prefab asset.
        /// </summary>
        public static GameObject CreatePortalPrefab(string folder, DungeonManager managerPrefab, DungeonProfile profile)
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material template = probe.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(probe);

            Material stone = CreateMaterial(template, folder, "Portal Stone", new Color(0.42f, 0.40f, 0.44f), 0f);
            Material glow = CreateMaterial(template, folder, "Portal Glow", new Color(0.55f, 0.25f, 1f), 2.5f);

            var root = new GameObject("World Portal");
            // The trigger the player walks into: the opening between the pillars, a little deeper than the frame.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.6f, 0f);
            trigger.size = new Vector3(2.2f, 3.0f, 1.6f);

            Part(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.075f, 0f), new Vector3(3.4f, 0.075f, 2.2f), stone, false);
            Part(root, "Pillar Left", PrimitiveType.Cube, new Vector3(-1.35f, 1.75f, 0f), new Vector3(0.45f, 3.5f, 0.55f), stone, true);
            Part(root, "Pillar Right", PrimitiveType.Cube, new Vector3(1.35f, 1.75f, 0f), new Vector3(0.45f, 3.5f, 0.55f), stone, true);
            Part(root, "Lintel", PrimitiveType.Cube, new Vector3(0f, 3.65f, 0f), new Vector3(3.2f, 0.4f, 0.65f), stone, true);
            Part(root, "Surface", PrimitiveType.Cube, new Vector3(0f, 1.75f, 0f), new Vector3(2.25f, 3.3f, 0.06f), glow, false);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.8f, 0.8f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.35f, 1f);
            light.range = 9f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;

            var portal = root.AddComponent<Portal>();
            var so = new SerializedObject(portal);
            so.FindProperty("dungeonManager").objectReferenceValue = managerPrefab;
            if (profile != null)
                so.FindProperty("dungeonProfile").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/World Portal.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created the world portal prefab at {path}" + (managerPrefab != null ? $" (Dungeon Manager: {managerPrefab.name})." : " - assign a Dungeon Manager or Dungeon Profile on its Portal component."), prefab);
            return prefab;
        }

        private static void Part(GameObject root, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider c = go.GetComponent<Collider>();
            if (!solid && c != null)
                Object.DestroyImmediate(c);
        }

        private static Material CreateMaterial(Material template, string folder, string name, Color color, float emission)
        {
            var m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = name;
            m.color = color;
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", color * emission);
            }
            AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.mat"));
            return m;
        }

        /// <summary>The selected Dungeon Manager prefab, else the first one in the project (null when there is none).</summary>
        public static DungeonManager FindManagerPrefab()
        {
            if (Selection.activeObject is GameObject selected && PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                DungeonManager m = selected.GetComponent<DungeonManager>();
                if (m != null)
                    return m;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                DungeonManager m = go != null ? go.GetComponent<DungeonManager>() : null;
                if (m != null)
                    return m;
            }
            return null;
        }

        /// <summary>Offers to add a portal prefab to the scene's EndlessTerrain portal list.</summary>
        public static void OfferToAddToTerrain(GameObject portalPrefab)
        {
            var terrain = Object.FindAnyObjectByType<EndlessTerrain>();
            if (portalPrefab == null || terrain == null)
                return;
            if (EditorUtility.DisplayDialog("World portal", $"Add '{portalPrefab.name}' to {terrain.name}'s Portal Settings, so it spawns on the terrain?", "Add", "Not now"))
            {
                EndlessTerrainEditor.AddPortalPrefab(terrain, portalPrefab);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }
        }

        // ------------------------------------------------------------------ validation

        [MenuItem("Tools/SimpleMovements/Dungeon/Validate Portal Setup")]
        public static void ValidateSetup()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var ok = new List<string>();

            // World.
            var generator = Object.FindAnyObjectByType<TerrainGenerator>();
            if (generator == null) errors.Add("No TerrainGenerator in the scene.");
            else ok.Add($"TerrainGenerator '{generator.name}' (world seed {generator.VoronoiSeed}).");

            var terrain = Object.FindAnyObjectByType<EndlessTerrain>();
            if (terrain == null)
            {
                errors.Add("No EndlessTerrain in the scene: portals are spawned by its chunks.");
            }
            else
            {
                if (terrain.viewer == null) errors.Add("EndlessTerrain > Viewer is empty.");
                else ok.Add($"EndlessTerrain viewer: {terrain.viewer.name}.");
                if (!terrain.bakeNavMesh) warnings.Add("EndlessTerrain > Bake NavMesh is off: mobs can't spawn, and portals ignore Require NavMesh.");
                foreach (string p in terrain.Portals.GetProblems()) errors.Add("Portal Settings: " + p);
                foreach (string p in terrain.Mobs.GetProblems(terrain.navMeshDistance, terrain.bakeNavMesh)) warnings.Add("Mob Settings: " + p);

                // Every portal prefab.
                if (terrain.Portals.prefabs != null)
                {
                    foreach (SpawnablePortal type in terrain.Portals.prefabs)
                    {
                        if (type == null || type.prefab == null)
                            continue;
                        Portal portal = type.prefab.GetComponentInChildren<Portal>(true);
                        if (portal == null)
                            continue;   // already reported
                        List<string> problems = portal.GetSetupProblems();
                        if (problems.Count == 0) ok.Add($"Portal prefab '{type.prefab.name}' is ready.");
                        foreach (string p in problems) errors.Add($"Portal prefab '{type.prefab.name}': {p}");
                        CheckDungeon(portal, errors, warnings, ok);
                    }
                }
                if (terrain.Portals.spawnChance < 0.3f)
                    warnings.Add("Portal Settings > Spawn Chance is low: portals will be rare. For testing, set it to 1 and Region Size to ~400.");
            }

            // Player.
            GameObject player = null;
            try { player = GameObject.FindGameObjectWithTag("Player"); }
            catch (UnityException) { errors.Add("The 'Player' tag doesn't exist (Project Settings > Tags and Layers)."); }
            if (player == null) warnings.Add("No object tagged 'Player' in the open scene (fine if it's spawned at runtime).");
            else
            {
                bool body = player.GetComponentInChildren<CharacterController>() != null || player.GetComponentInChildren<Rigidbody>() != null;
                if (!body) errors.Add($"Player '{player.name}' has no CharacterController or Rigidbody: trigger colliders (portals) never detect it.");
                else ok.Add($"Player '{player.name}' can trigger portals.");
            }

            var sb = new StringBuilder();
            foreach (string e in errors) sb.AppendLine("ERROR: " + e);
            foreach (string w in warnings) sb.AppendLine("Warning: " + w);
            foreach (string o in ok) sb.AppendLine("OK: " + o);
            string report = sb.ToString();
            if (errors.Count > 0) Debug.LogError("Portal setup check:\n" + report);
            else if (warnings.Count > 0) Debug.LogWarning("Portal setup check:\n" + report);
            else Debug.Log("Portal setup check:\n" + report);
            EditorUtility.DisplayDialog("Portal setup check",
                (errors.Count == 0 ? (warnings.Count == 0 ? "Everything needed is in place.\n\n" : "Usable, with warnings.\n\n") : $"{errors.Count} problem(s) to fix.\n\n") +
                Truncate(report, 1800) + "\n(Full report in the Console.)", "OK");
        }

        private static void CheckDungeon(Portal portal, List<string> errors, List<string> warnings, List<string> ok)
        {
            if (!portal.LeadsToDungeon)
                return;
            DungeonProfile profile = portal.Profile != null ? portal.Profile : (portal.Manager != null ? portal.Manager.profile : null);
            if (profile == null)
                return;   // already reported by GetSetupProblems
            if (profile.theme == null) warnings.Add($"Dungeon Profile '{profile.name}' has no Theme: dungeons use flat-colour placeholder materials.");
            if (profile.population == null || profile.population.encounters == null) warnings.Add($"Dungeon Profile '{profile.name}' has no Encounter table: dungeons use placeholder capsule mobs.");
            if (profile.roles == null || profile.roles.Count == 0) warnings.Add($"Dungeon Profile '{profile.name}' has no Roles (no boss, treasure or rest rooms).");
            ok.Add($"Portal '{portal.name}' leads to profile '{profile.name}'.");
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
