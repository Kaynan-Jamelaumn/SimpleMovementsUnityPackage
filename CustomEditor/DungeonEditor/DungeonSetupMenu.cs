using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    /// <summary>Menu items that create a ready-to-use dungeon setup.</summary>
    public static class DungeonSetupMenu
    {
        /// <summary>Asks for a folder inside Assets (created if needed); returns its project path ("Assets/...") or null.</summary>
        public static string PickAssetFolder(string title)
        {
            string absolute = EditorUtility.SaveFolderPanel(title, Application.dataPath, "Dungeon");
            if (string.IsNullOrEmpty(absolute))
                return null;
            absolute = absolute.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            if (!absolute.StartsWith(data))
            {
                EditorUtility.DisplayDialog("Dungeon setup", "Pick a folder inside this project's Assets folder.", "OK");
                return null;
            }
            Directory.CreateDirectory(absolute);
            AssetDatabase.Refresh();
            return "Assets" + absolute.Substring(data.Length);
        }

        [MenuItem("Tools/SimpleMovements/Dungeon/Create Default Setup...")]
        public static void CreateDefaultSetup()
        {
            string folder = PickAssetFolder("Folder for the dungeon assets");
            if (folder == null)
                return;

            var theme = ScriptableObject.CreateInstance<DungeonTheme>();
            AssetDatabase.CreateAsset(theme, Unique(folder, "DungeonTheme"));

            var loot = ScriptableObject.CreateInstance<DungeonLootTable>();
            foreach (LootInfo l in DungeonDefaults.Loot())
            {
                loot.entries.Add(new LootEntry
                {
                    name = l.Name, placeholder = l.Placeholder, weight = l.Weight, tier = l.Tier, progress = l.Progress,
                    styles = l.Styles, placement = l.Placement, minFloor = l.MinFloor, maxFloor = l.MaxFloor,
                });
            }
            AssetDatabase.CreateAsset(loot, Unique(folder, "DungeonLoot"));

            var props = ScriptableObject.CreateInstance<DungeonPropTable>();
            foreach (PropInfo p in DungeonDefaults.Props(theme))
            {
                props.entries.Add(new PropEntry
                {
                    name = p.Name, kind = p.Kind, placeholder = p.Placeholder, placement = p.Placement, roles = new List<AreaRole>(p.Roles),
                    areaTag = p.AreaTag, styles = p.Styles, mainPath = p.MainPath, chance = p.Chance, perArea = p.PerArea,
                    perHundredCells = p.PerHundredCells, spacing = p.Spacing, awayFromMobs = p.AwayFromMobs, progress = p.Progress,
                    minFloor = p.MinFloor, maxFloor = p.MaxFloor, heightOffset = p.HeightOffset, scale = p.Scale,
                    lightColor = p.LightColor, lightRange = p.LightRange,
                });
            }
            AssetDatabase.CreateAsset(props, Unique(folder, "DungeonProps"));

            var encounters = ScriptableObject.CreateInstance<DungeonEncounterTable>();
            AssetDatabase.CreateAsset(encounters, Unique(folder, "DungeonEncounters"));

            var hall = ScriptableObject.CreateInstance<RoomTemplate>();
            hall.mode = RoomTemplate.TemplateMode.ShapeMask;
            hall.shapeMask =
                "#####D#####\n" +
                "#.........#\n" +
                "#..P...P..#\n" +
                "#.........#\n" +
                "D....P....D\n" +
                "#.........#\n" +
                "#..P...P..#\n" +
                "#.........#\n" +
                "#####D#####";
            hall.role = AreaRole.None;
            hall.ceilingHeight = 6f;
            AssetDatabase.CreateAsset(hall, Unique(folder, "Template_PillaredHall"));

            var legacy = ScriptableObject.CreateInstance<RoomTemplate>();
            legacy.mode = RoomTemplate.TemplateMode.Prefab;
            legacy.legacyRoomBehaviour = true;
            legacy.pivot = RoomTemplate.Pivot.NorthWestCorner;
            legacy.footprint = new Vector2Int(7, 7);
            AssetDatabase.CreateAsset(legacy, Unique(folder, "Template_LegacyRoom"));

            var profile = ScriptableObject.CreateInstance<DungeonProfile>();
            profile.roles = DungeonProfile.DefaultRoles();
            profile.roles[0].templates = new[] { hall };
            profile.theme = theme;
            profile.population.loot = loot;
            profile.population.props = props;
            profile.population.encounters = null;   // placeholders until you add mobs (then assign the Encounters table)
            AssetDatabase.CreateAsset(profile, Unique(folder, "DungeonProfile"));

            var go = new GameObject("Dungeon Manager");
            var manager = go.AddComponent<DungeonManager>();
            manager.profile = profile;
            var director = go.AddComponent<DungeonRespawnDirector>();
            director.manager = manager;
            director.enabled = false;
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Dungeon Manager.prefab");
            GameObject managerPrefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            // The world portal that leads into it.
            GameObject portalPrefab = PortalSetupMenu.CreatePortalPrefab(folder, managerPrefab.GetComponent<DungeonManager>(), null);

            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            EditorUtility.DisplayDialog("Dungeon setup",
                $"Created in {folder}:\n- DungeonProfile (open it and press Open Preview)\n- DungeonTheme (materials / tile kit / portals)\n" +
                "- Loot and Prop tables with the built-in primitives\n- an empty Encounter table (add mob prefabs or import the world's, then assign it on the profile)\n" +
                "- two Room Templates (a pillared boss hall, and one for the old RoomBehaviour prefabs)\n- a Dungeon Manager prefab\n" +
                "- a World Portal prefab already wired to that Dungeon Manager (add it to EndlessTerrain > Portal Settings > Prefabs)",
                "OK");
            PortalSetupMenu.OfferToAddToTerrain(portalPrefab);
        }

        [MenuItem("Tools/SimpleMovements/Dungeon/Add Dungeon Manager To Scene")]
        public static void AddManager()
        {
            var go = new GameObject("Dungeon Manager");
            Undo.RegisterCreatedObjectUndo(go, "Add Dungeon Manager");
            var manager = go.AddComponent<DungeonManager>();
            manager.generateOnStart = true;
            if (Selection.activeObject is DungeonProfile p)
                manager.profile = p;
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/SimpleMovements/Dungeon/Open Preview")]
        public static void OpenPreview() => DungeonPreviewWindow.Open();

        private static string Unique(string folder, string name) => AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
    }
}
