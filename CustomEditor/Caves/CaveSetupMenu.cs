#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProceduralDungeon;
using ProceduralDungeon.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProceduralCaves.EditorTools
{
    /// <summary>
    /// Tools > SimpleMovements > Caves: a complete cave setup in one click (profile, theme, tables, resources, mining stage,
    /// Dungeon Manager - and the world's Cave Entrances wired to it), a hand-placed entrance, a resource table from the
    /// Crafting Database, and a check of everything.
    /// </summary>
    public static class CaveSetupMenu
    {
        private const string Menu = SMMenu.Root + "Caves/";

        [MenuItem(Menu + "Create Cave Setup...", false, SMMenu.Caves + 1)]
        public static void CreateCaveSetup()
        {
            string folder = DungeonSetupMenu.PickAssetFolder("Folder for the cave assets", "Caves");
            if (folder == null)
                return;
            CaveAssets made = CreateAssets(folder);
            AssetDatabase.SaveAssets();
            Selection.activeObject = made.Profile;
            EditorGUIUtility.PingObject(made.Profile);

            var report = new StringBuilder();
            report.AppendLine($"Created in {folder}:");
            report.AppendLine("- Cave_DungeonProfile: natural caverns, tunnels, drops and climbs, guardian chambers (open it and press Open Preview)");
            report.AppendLine("- Cave_MiningStage (in the profile's Custom Stages): deposits, special chambers, rock formations, depth, persistence");
            report.AppendLine($"- Cave_Resources: {made.Resources.entries.Count} deposits from the Crafting Database (ores, coal, stone, clay, salt, gems)");
            report.AppendLine("- Cave_DungeonTheme, Cave_DungeonLoot, Cave_DungeonProps");
            report.AppendLine(made.ImportedMobs > 0
                ? $"- Cave_DungeonEncounters: {made.ImportedMobs} mob(s) from the world (rarer ones only from the second level down)"
                : "- Cave_DungeonEncounters: empty (no EndlessTerrain mobs in the open scene) - stand-in creatures until you add mob prefabs");
            report.AppendLine("- Cave Dungeon Manager prefab (its exit leads deeper)");
            EditorUtility.DisplayDialog("Cave setup", report.ToString(), "OK");
            OfferToAddToTerrain(made);
        }

        /// <summary>What <see cref="CreateAssets"/> made.</summary>
        public sealed class CaveAssets
        {
            public DungeonProfile Profile;
            public CaveMiningStage Stage;
            public CaveResourceTable Resources;
            public DungeonTheme Theme;
            public DungeonEncounterTable Encounters;
            public DungeonManager Manager;
            public int ImportedMobs;
        }

        /// <summary>Creates a whole cave setup in <paramref name="folder"/> (a project path, "Assets/...").</summary>
        public static CaveAssets CreateAssets(string folder)
        {
            var made = new CaveAssets();

            var theme = ScriptableObject.CreateInstance<DungeonTheme>();
            CaveDefaults.ApplyTheme(theme);
            AssetDatabase.CreateAsset(theme, Unique(folder, "Cave_DungeonTheme"));
            made.Theme = theme;

            var loot = ScriptableObject.CreateInstance<DungeonLootTable>();
            foreach (LootInfo l in DungeonDefaults.Loot())
            {
                loot.entries.Add(new LootEntry
                {
                    name = l.Name, placeholder = l.Placeholder, weight = l.Weight, tier = l.Tier, progress = l.Progress,
                    styles = l.Styles, placement = l.Placement, minFloor = l.MinFloor, maxFloor = l.MaxFloor,
                });
            }
            AssetDatabase.CreateAsset(loot, Unique(folder, "Cave_DungeonLoot"));

            var props = ScriptableObject.CreateInstance<DungeonPropTable>();
            DungeonSetupMenu.AddMissingProps(props, theme);
            AssetDatabase.CreateAsset(props, Unique(folder, "Cave_DungeonProps"));

            var encounters = ScriptableObject.CreateInstance<DungeonEncounterTable>();
            made.ImportedMobs = ImportWorldMobs(encounters);
            AssetDatabase.CreateAsset(encounters, Unique(folder, "Cave_DungeonEncounters"));
            made.Encounters = encounters;

            CaveResourceTable resources = CaveResourceTable.CreateDefault();
            AssetDatabase.CreateAsset(resources, Unique(folder, "Cave_Resources"));
            made.Resources = resources;

            var stage = ScriptableObject.CreateInstance<CaveMiningStage>();
            stage.resources = resources;
            AssetDatabase.CreateAsset(stage, Unique(folder, "Cave_MiningStage"));
            made.Stage = stage;

            var profile = ScriptableObject.CreateInstance<DungeonProfile>();
            CaveDefaults.ApplyProfile(profile);
            profile.theme = theme;
            profile.population.loot = loot;
            profile.population.props = props;
            profile.population.encounters = encounters.entries.Count > 0 ? encounters : null;
            if (profile.customStages == null)
                profile.customStages = new List<DungeonStageAsset>();
            profile.customStages.Add(stage);
            AssetDatabase.CreateAsset(profile, Unique(folder, "Cave_DungeonProfile"));
            made.Profile = profile;

            var go = new GameObject("Cave Dungeon Manager");
            var manager = go.AddComponent<DungeonManager>();
            manager.profile = profile;
            // The lowest level's way leads to a deeper cave (deeper, richer, more dangerous); the way out is always the entrance.
            manager.exitPortalAction = DungeonPortal.PortalAction.NextDungeon;
            var director = go.AddComponent<DungeonRespawnDirector>();
            director.manager = manager;
            director.enabled = false;
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Cave Dungeon Manager.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            made.Manager = prefab != null ? prefab.GetComponent<DungeonManager>() : null;
            return made;
        }

        /// <summary>
        /// The world's mobs (EndlessTerrain > Mobs in the open scene) as cave creatures: rarer ones (Rarity Level 4+) from
        /// the second level down, the rarest (7+) from the third.
        /// </summary>
        public static int ImportWorldMobs(DungeonEncounterTable table)
        {
            EndlessTerrain terrain = Object.FindAnyObjectByType<EndlessTerrain>();
            if (terrain == null || table == null)
                return 0;
            var so = new SerializedObject(terrain);
            SerializedProperty list = so.FindProperty("mobSettings.prefabs");
            if (list == null || !list.isArray)
                return 0;
            int added = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                var mob = new SpawnableMob { mobPrefab = e.FindPropertyRelative("mobPrefab")?.objectReferenceValue as GameObject };
                if (mob.mobPrefab == null)
                    continue;
                SerializedProperty prop;
                if ((prop = e.FindPropertyRelative("spawnWeight")) != null) mob.spawnWeight = prop.floatValue;
                if ((prop = e.FindPropertyRelative("rarityLevel")) != null) mob.rarityLevel = prop.intValue;
                if ((prop = e.FindPropertyRelative("isPackAnimal")) != null) mob.isPackAnimal = prop.boolValue;
                if ((prop = e.FindPropertyRelative("preferredPackSize")) != null) mob.preferredPackSize = prop.intValue;
                if ((prop = e.FindPropertyRelative("packSpreadRadius")) != null) mob.packSpreadRadius = prop.floatValue;
                EncounterEntry entry = SpawnableMobImport.ToEncounter(mob);
                if (entry == null)
                    continue;
                entry.minFloor = mob.rarityLevel >= 7 ? 2 : mob.rarityLevel >= 4 ? 1 : 0;
                table.entries.Add(entry);
                added++;
            }
            return added;
        }

        private static void OfferToAddToTerrain(CaveAssets made)
        {
            EndlessTerrain terrain = Object.FindAnyObjectByType<EndlessTerrain>();
            if (terrain == null || made.Profile == null)
                return;
            if (!EditorUtility.DisplayDialog("Cave setup", $"Put cave entrances leading to '{made.Profile.name}' in the world (EndlessTerrain '{terrain.name}' > Cave Entrances)?", "Yes", "No"))
                return;
            AddToTerrain(terrain, made.Profile, made.Manager);
        }

        /// <summary>Adds <paramref name="profile"/> (and the manager, when the settings have none) to the terrain's Cave Entrances.</summary>
        public static void AddToTerrain(EndlessTerrain terrain, DungeonProfile profile, DungeonManager manager)
        {
            var so = new SerializedObject(terrain);
            SerializedProperty settings = so.FindProperty("caveSettings");
            if (settings == null)
                return;
            settings.FindPropertyRelative("enabled").boolValue = true;
            SerializedProperty list = settings.FindPropertyRelative("caveProfiles");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                present |= list.GetArrayElementAtIndex(i).objectReferenceValue == profile;
            if (!present)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = profile;
            }
            SerializedProperty m = settings.FindPropertyRelative("dungeonManager");
            if (m.objectReferenceValue == null && manager != null)
                m.objectReferenceValue = manager;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            Selection.activeObject = terrain;
        }

        [MenuItem(Menu + "Add Cave Entrance Here", false, SMMenu.Caves + 2)]
        public static void AddEntrance()
        {
            DungeonProfile profile = SelectedOrFirstCaveProfile();
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Cave entrance", "No cave profile in the project yet: use Tools > SimpleMovements > Caves > Create Cave Setup first (or select a cave profile).", "OK");
                return;
            }
            Vector3 at = Vector3.zero;
            float yaw = 0f;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                Transform cam = view.camera.transform;
                at = view.pivot;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
                    at = hit.point;
                Vector3 toCamera = cam.position - at;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.01f)
                    yaw = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;   // the mouth opens towards the view
            }
            var go = new GameObject("Cave Entrance");
            Undo.RegisterCreatedObjectUndo(go, "Add Cave Entrance");
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var entrance = go.AddComponent<CaveEntrance>();
            entrance.profile = profile;
            entrance.manager = ManagerFor(profile);
            Selection.activeGameObject = go;
        }

        [MenuItem(Menu + "Create Resource Table...", false, SMMenu.Caves + 3)]
        public static void CreateResourceTable()
        {
            string folder = DungeonSetupMenu.PickAssetFolder("Folder for the cave resource table", "Caves");
            if (folder == null)
                return;
            CaveResourceTable table = CaveResourceTable.CreateDefault();
            AssetDatabase.CreateAsset(table, Unique(folder, "Cave_Resources"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
        }

        [MenuItem(Menu + "Validate Cave Setup", false, SMMenu.Caves + 20)]
        public static void ValidateMenu()
        {
            List<string> problems = Validate(out int profiles, out int entrances);
            var sb = new StringBuilder();
            sb.AppendLine($"Cave profiles: {profiles}, cave entrances in the open scenes: {entrances}.");
            if (problems.Count == 0)
            {
                sb.AppendLine("No problems found.");
                Debug.Log("[Caves] " + sb);
            }
            else
            {
                foreach (string p in problems)
                    sb.AppendLine("- " + p);
                Debug.LogWarning("[Caves] " + sb);
            }
            EditorUtility.DisplayDialog("Validate cave setup", sb.ToString(), "OK");
        }

        /// <summary>Everything that would stop caves from working, in the project and the open scenes.</summary>
        public static List<string> Validate(out int profileCount, out int entranceCount)
        {
            var problems = new List<string>();
            profileCount = 0;
            foreach (DungeonProfile p in CaveProfiles())
            {
                profileCount++;
                CaveMiningStage stage = StageOf(p);
                if (p.population == null || p.population.encounters == null || p.population.encounters.entries.Count == 0)
                    problems.Add($"{p.name}: no Encounter table with mobs - the caves get stand-in creatures (import the world's mobs on the table).");
                if (p.build != null && p.build.keepGenerated <= 0)
                    problems.Add($"{p.name}: Build > Keep Generated is 0 - every visit generates the cave again (it is the same cave, but slower).");
                if (p.theme == null)
                    problems.Add($"{p.name}: no theme (the caves use flat colours).");
                problems.AddRange(CheckRules(p.name, stage.MakeRules()));
            }
            entranceCount = 0;
            foreach (CaveEntrance e in Object.FindObjectsByType<CaveEntrance>(FindObjectsSortMode.None))
            {
                entranceCount++;
                foreach (string s in e.GetSetupProblems())
                    problems.Add($"Cave Entrance '{e.name}': {s}");
            }
            foreach (EndlessTerrain t in Object.FindObjectsByType<EndlessTerrain>(FindObjectsSortMode.None))
                if (t.Caves != null)
                    foreach (string s in t.Caves.GetProblems())
                        problems.Add($"EndlessTerrain '{t.name}' > Cave Entrances: {s}");
            return problems;
        }

        /// <summary>Resource entries that would give nothing, and depth bands without any resource.</summary>
        public static List<string> CheckRules(string owner, CaveRules rules)
        {
            var problems = new List<string>();
            CraftRules craft = null;
            try
            {
                craft = CraftDatabase.Instance != null ? CraftDatabase.Instance.Rules : null;
            }
            catch (System.Exception)
            {
                // no database: ids are not checked
            }
            for (int i = 0; i < rules.Resources.Count; i++)
            {
                CaveResourceEntry e = rules.Resources[i];
                if (e == null || !e.enabled)
                    continue;
                if (e.itemOverride == null && craft != null && CraftItems.Stock(craft, e.kind, e.craftId) == null)
                    problems.Add($"{owner}: resource '{e.name}' gives '{e.craftId}' ({e.kind}), which the Crafting Database does not know - it would give nothing.");
                if (e.weight <= 0f)
                    problems.Add($"{owner}: resource '{e.name}' has Weight 0 (never placed).");
            }
            foreach (CaveDepthBand band in System.Enum.GetValues(typeof(CaveDepthBand)))
            {
                float depth = BandMiddle(rules.Depth, band);
                bool any = false;
                foreach (CaveResourceEntry e in rules.Resources)
                    any |= e != null && e.enabled && !e.specialChambersOnly && e.weight > 0f && CaveDepth.Suitability(e, depth) > 0f;
                if (!any)
                    problems.Add($"{owner}: no resource for the {band} band (depth {depth:0.#}) outside special chambers.");
            }
            // The bottom: the deepest caves of a chain count this deep.
            float bottom = rules.Depth.maxDepth > 0f ? rules.Depth.maxDepth : 12f;
            int atBottom = 0;
            foreach (CaveResourceEntry e in rules.Resources)
                if (e != null && e.enabled && !e.specialChambersOnly && e.weight > 0f && CaveDepth.Suitability(e, bottom) > 0f)
                    atBottom++;
            if (atBottom < 3)
                problems.Add($"{owner}: only {atBottom} resource(s) reach the bottom (depth {bottom:0.#}, Depth > Max Depth): the deepest caves would hold little - lower Max Depth or widen the deepest resources' ranges.");
            return problems;
        }

        /// <summary>A depth in the middle of <paramref name="band"/>.</summary>
        public static float BandMiddle(CaveDepthSettings s, CaveDepthBand band)
        {
            Vector3 b = s.bandStarts;
            switch (band)
            {
                case CaveDepthBand.Shallow: return b.x * 0.5f;
                case CaveDepthBand.Medium: return (b.x + b.y) * 0.5f;
                case CaveDepthBand.Deep: return (b.y + b.z) * 0.5f;
                default: return b.z + 1f;
            }
        }

        [MenuItem(Menu + "Help", false, SMMenu.Caves + 40)]
        public static void Help()
        {
            string[] guids = AssetDatabase.FindAssets("t:TextAsset README", null);
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Replace('\\', '/').Contains("/Documentation/Caves/"))
                {
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                    return;
                }
            }
            EditorUtility.DisplayDialog("Caves", "See Documentation/Caves/README.md in the SimpleMovements folder.", "OK");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Every Dungeon Profile asset with a Cave Mining Stage.</summary>
        public static IEnumerable<DungeonProfile> CaveProfiles()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:DungeonProfile"))
            {
                var p = AssetDatabase.LoadAssetAtPath<DungeonProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && StageOf(p) != null)
                    yield return p;
            }
        }

        /// <summary>The profile's Cave Mining Stage, or null.</summary>
        public static CaveMiningStage StageOf(DungeonProfile p)
        {
            if (p == null || p.customStages == null)
                return null;
            foreach (DungeonStageAsset s in p.customStages)
                if (s is CaveMiningStage c)
                    return c;
            return null;
        }

        private static DungeonProfile SelectedOrFirstCaveProfile()
        {
            if (Selection.activeObject is DungeonProfile selected && StageOf(selected) != null)
                return selected;
            foreach (DungeonProfile p in CaveProfiles())
                return p;
            return null;
        }

        /// <summary>A Dungeon Manager prefab whose profile is <paramref name="profile"/>, or null.</summary>
        public static DungeonManager ManagerFor(DungeonProfile profile)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                DungeonManager m = go != null ? go.GetComponent<DungeonManager>() : null;
                if (m != null && m.profile == profile)
                    return m;
            }
            return null;
        }

        private static string Unique(string folder, string name) => AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
    }
}
#endif
