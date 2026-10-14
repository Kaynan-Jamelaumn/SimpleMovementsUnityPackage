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

        /// <summary>A prop table entry from a built-in prop.</summary>
        public static PropEntry ToEntry(PropInfo p)
        {
            return new PropEntry
            {
                name = p.Name, kind = p.Kind, placeholder = p.Placeholder, placement = p.Placement, roles = new List<AreaRole>(p.Roles),
                areaTag = p.AreaTag, styles = p.Styles, mainPath = p.MainPath, modifiers = p.Modifiers, chance = p.Chance, perArea = p.PerArea,
                perHundredCells = p.PerHundredCells, spacing = p.Spacing, awayFromMobs = p.AwayFromMobs, progress = p.Progress,
                minFloor = p.MinFloor, maxFloor = p.MaxFloor, heightOffset = p.HeightOffset, scale = p.Scale,
                lightColor = p.LightColor, lightRange = p.LightRange,
            };
        }

        /// <summary>Appends the built-in props a table doesn't have yet (by name). Returns how many were added.</summary>
        public static int AddMissingProps(DungeonPropTable table, DungeonTheme theme)
        {
            int added = 0;
            foreach (PropInfo p in DungeonDefaults.Props(theme))
            {
                if (table.entries.Exists(e => e != null && e.name == p.Name))
                    continue;
                table.entries.Add(ToEntry(p));
                added++;
            }
            return added;
        }

        [MenuItem(SMMenu.Root + "Dungeon/Create Default Setup...", false, SMMenu.Dungeon + 1)]
        public static void CreateDefaultSetup() => CreateSetup(DungeonType.Classic);

        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Crypt...", false, SMMenu.Dungeon + 20)] private static void CreateCrypt() => CreateSetup(DungeonType.Crypt);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Deep Caves...", false, SMMenu.Dungeon + 20)] private static void CreateCaves() => CreateSetup(DungeonType.DeepCaves);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Fortress...", false, SMMenu.Dungeon + 20)] private static void CreateFortress() => CreateSetup(DungeonType.Fortress);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Labyrinth...", false, SMMenu.Dungeon + 20)] private static void CreateLabyrinth() => CreateSetup(DungeonType.Labyrinth);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Sunken Temple...", false, SMMenu.Dungeon + 20)] private static void CreateTemple() => CreateSetup(DungeonType.SunkenTemple);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Volcanic Forge...", false, SMMenu.Dungeon + 20)] private static void CreateForge() => CreateSetup(DungeonType.VolcanicForge);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Cathedral...", false, SMMenu.Dungeon + 20)] private static void CreateCathedral() => CreateSetup(DungeonType.Cathedral);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Prison...", false, SMMenu.Dungeon + 20)] private static void CreatePrison() => CreateSetup(DungeonType.Prison);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Frozen Depths...", false, SMMenu.Dungeon + 20)] private static void CreateFrozen() => CreateSetup(DungeonType.FrozenDepths);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Overgrown Ruins...", false, SMMenu.Dungeon + 20)] private static void CreateOvergrown() => CreateSetup(DungeonType.OvergrownRuins);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Tower...", false, SMMenu.Dungeon + 20)] private static void CreateTower() => CreateSetup(DungeonType.Tower);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Undercity...", false, SMMenu.Dungeon + 20)] private static void CreateUndercity() => CreateSetup(DungeonType.Undercity);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Fungal Hive...", false, SMMenu.Dungeon + 20)] private static void CreateHive() => CreateSetup(DungeonType.FungalHive);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Chasm Islands...", false, SMMenu.Dungeon + 20)] private static void CreateIslands() => CreateSetup(DungeonType.ChasmIslands);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Dragon's Den...", false, SMMenu.Dungeon + 20)] private static void CreateDen() => CreateSetup(DungeonType.DragonsDen);
        [MenuItem(SMMenu.Root + "Dungeon/Create Dungeon Type/Astral Void...", false, SMMenu.Dungeon + 20)] private static void CreateAstral() => CreateSetup(DungeonType.AstralVoid);

        /// <summary>A complete setup (profile, theme, tables, templates, manager and world portal) for a kind of dungeon.</summary>
        public static void CreateSetup(DungeonType dungeonType)
        {
            string folder = PickAssetFolder($"Folder for the {ObjectNames.NicifyVariableName(dungeonType.ToString())} dungeon assets");
            if (folder == null)
                return;
            string prefix = dungeonType == DungeonType.Classic ? "" : dungeonType + "_";

            var theme = ScriptableObject.CreateInstance<DungeonTheme>();
            if (dungeonType != DungeonType.Classic)
                DungeonTypes.ApplyTheme(theme, dungeonType);
            AssetDatabase.CreateAsset(theme, Unique(folder, prefix + "DungeonTheme"));

            var loot = ScriptableObject.CreateInstance<DungeonLootTable>();
            foreach (LootInfo l in DungeonDefaults.Loot())
            {
                loot.entries.Add(new LootEntry
                {
                    name = l.Name, placeholder = l.Placeholder, weight = l.Weight, tier = l.Tier, progress = l.Progress,
                    styles = l.Styles, placement = l.Placement, minFloor = l.MinFloor, maxFloor = l.MaxFloor,
                });
            }
            AssetDatabase.CreateAsset(loot, Unique(folder, prefix + "DungeonLoot"));

            var props = ScriptableObject.CreateInstance<DungeonPropTable>();
            AddMissingProps(props, theme);
            AssetDatabase.CreateAsset(props, Unique(folder, prefix + "DungeonProps"));

            var encounters = ScriptableObject.CreateInstance<DungeonEncounterTable>();
            AssetDatabase.CreateAsset(encounters, Unique(folder, prefix + "DungeonEncounters"));

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
            hall.ceilingHeight = 9f;
            AssetDatabase.CreateAsset(hall, Unique(folder, prefix + "Template_PillaredHall"));

            var legacy = ScriptableObject.CreateInstance<RoomTemplate>();
            legacy.mode = RoomTemplate.TemplateMode.Prefab;
            legacy.legacyRoomBehaviour = true;
            legacy.pivot = RoomTemplate.Pivot.NorthWestCorner;
            legacy.footprint = new Vector2Int(7, 7);
            AssetDatabase.CreateAsset(legacy, Unique(folder, prefix + "Template_LegacyRoom"));

            var profile = ScriptableObject.CreateInstance<DungeonProfile>();
            if (dungeonType != DungeonType.Classic)
                DungeonTypes.Apply(profile, dungeonType);
            else
                profile.roles = DungeonProfile.DefaultRoles();
            profile.roles[0].templates = new[] { hall };
            profile.theme = theme;
            profile.population.loot = loot;
            profile.population.props = props;
            profile.population.encounters = null;   // placeholders until you add mobs (then assign the Encounters table)
            AssetDatabase.CreateAsset(profile, Unique(folder, prefix + "DungeonProfile"));

            var go = new GameObject("Dungeon Manager");
            var manager = go.AddComponent<DungeonManager>();
            manager.profile = profile;
            var director = go.AddComponent<DungeonRespawnDirector>();
            director.manager = manager;
            director.enabled = false;
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + prefix + "Dungeon Manager.prefab");
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

        [MenuItem(SMMenu.Root + "Dungeon/Add Dungeon Manager To Scene", false, SMMenu.Dungeon + 0)]
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

        [MenuItem(SMMenu.Root + "Dungeon/Open Preview", false, SMMenu.Dungeon + 40)]
        public static void OpenPreview() => DungeonPreviewWindow.Open();

        private static string Unique(string folder, string name) => AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
    }
}
