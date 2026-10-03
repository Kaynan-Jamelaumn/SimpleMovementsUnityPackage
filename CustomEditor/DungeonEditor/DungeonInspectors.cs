using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProceduralDungeon.EditorTools
{
    [CustomEditor(typeof(DungeonProfile))]
    public class DungeonProfileEditor : Editor
    {
        private string quickReport;
        private MessageType quickType = MessageType.Info;
        private DungeonType type = DungeonType.Classic;

        public override void OnInspectorGUI()
        {
            var p = (DungeonProfile)target;
            float spacing = p.EffectiveFloorSpacing();
            int stair = p.StairLengthCells();
            EditorGUILayout.HelpBox(
                $"Cells are {p.cellSize} m. Floors are up to {spacing:0.#} m apart" + (spacing > p.floorSpacing + 0.01f ? $" (raised from {p.floorSpacing} m to fit the ceilings)" : "") +
                $"; stair wells are {stair} cells ({stair * p.cellSize:0.#} m) long at up to {p.maxStairSlope}°.\n" +
                $"Ceilings: rooms {Scaled(p, p.rooms.roomCeiling)} m, halls {Scaled(p, p.rooms.hallCeiling)} m, corridors {Scaled(p, p.rooms.corridorCeiling)} m, caves up to {Scaled(p, p.caves.ceilingLimits.max)} m; tallest {p.TallestCeiling():0.#} m.\n" +
                "Leave tables and theme empty to use the built-in primitives.", MessageType.None);

            DrawHeights(p);
            DrawDungeonType(p);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Preview"))
                DungeonPreviewWindow.Open(p);
            if (GUILayout.Button("Quick Test (20 seeds)"))
                QuickTest(p);
            if (GUILayout.Button(new GUIContent("Default Roles", "Replace the role list with the default set (boss, treasure, rest, arena, shrine, secret and every special room).")))
            {
                Undo.RecordObject(p, "Reset dungeon roles");
                p.roles = DungeonProfile.DefaultRoles();
                EditorUtility.SetDirty(p);
            }
            if (GUILayout.Button(new GUIContent("Add Special Rooms", "Add the special rooms this profile's role list doesn't have yet (guardian, vault, trap gauntlet, puzzle, ambush, library, armory, prison, crypt, laboratory, garden, throne, nest, cursed altars, kitchen, gallery, barracks, pit fight, greenhouse, wine cellar, map room, gas chamber). Your existing rules are kept.")))
                AddSpecialRooms(p);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(quickReport))
                EditorGUILayout.HelpBox(quickReport, quickType);
            DrawChecklist(p);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static string Scaled(DungeonProfile p, float meters) => (meters * (p.ceilings != null ? p.ceilings.heightScale : 1f)).ToString("0.#");

        /// <summary>One click sets every ceiling height (rooms, halls, corridors, caves, vaults) to a ready-made set.</summary>
        private static void DrawHeights(DungeonProfile p)
        {
            EditorGUILayout.LabelField(new GUIContent("Ceiling Heights", "Sets Rooms > Heights, Caverns > Heights and Heights (size bonus, vaults, door height) together. Floors move apart to make room (Auto Floor Spacing)."), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (CeilingPreset preset in System.Enum.GetValues(typeof(CeilingPreset)))
            {
                string tip = preset switch
                {
                    CeilingPreset.Classic => "The original low ceilings: rooms 4 m, halls 6.5 m, corridors 3.2 m, no vaults.",
                    CeilingPreset.Tall => "Rooms 6.5 m, halls 10 m, corridors 4.5 m, tall caves, most halls vaulted.",
                    CeilingPreset.Cathedral => "Rooms 8 m, halls 14 m, corridors 5.5 m, huge vaults and caverns.",
                    _ => "Rooms 5 m, halls 7.5 m, corridors 3.6 m, bigger rooms taller, half the halls vaulted.",
                };
                if (GUILayout.Button(new GUIContent(ObjectNames.NicifyVariableName(preset.ToString()), tip)))
                {
                    Undo.RecordObject(p, "Ceiling heights");
                    if (p.ceilings == null)
                        p.ceilings = new CeilingSettings();
                    CeilingSettings.ApplyPreset(preset, p.rooms, p.caves, p.ceilings);
                    EditorUtility.SetDirty(p);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (p.ceilings != null)
            {
                EditorGUI.BeginChangeCheck();
                float scale = EditorGUILayout.Slider(new GUIContent("Height Scale", "Multiplies every ceiling height (Heights > Height Scale)."), p.ceilings.heightScale, 0.5f, 3f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(p, "Ceiling height scale");
                    p.ceilings.heightScale = scale;
                    EditorUtility.SetDirty(p);
                }
            }
        }

        /// <summary>Reshapes the profile into a ready-made kind of dungeon (crypt, caves, fortress...).</summary>
        private void DrawDungeonType(DungeonProfile p)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            type = (DungeonType)EditorGUILayout.EnumPopup(new GUIContent("Dungeon Type", "A ready-made kind of dungeon: floor styles, room shapes, ceilings, special rooms, mechanics and floor modifiers. Tables, theme and build settings are kept."), type);
            if (GUILayout.Button("Apply", GUILayout.Width(60)) &&
                EditorUtility.DisplayDialog("Apply dungeon type", $"Reshape '{p.name}' into a {ObjectNames.NicifyVariableName(type.ToString())} dungeon?\n\n{DungeonTypes.Describe(type)}\n\nFloor styles, rooms, caves, ceilings, connections, roles, mechanics and floor modifiers are replaced (Undo works). Tables, theme and build settings are kept.", "Apply", "Cancel"))
            {
                Undo.RecordObject(p, "Apply dungeon type");
                DungeonTypes.Apply(p, type);
                if (p.theme != null && EditorUtility.DisplayDialog("Theme colours", $"Also set the colours and atmosphere of the theme '{p.theme.name}' for this type? (Its materials and prefabs are kept.)", "Yes", "No"))
                {
                    Undo.RecordObject(p.theme, "Dungeon type colours");
                    DungeonTypes.ApplyTheme(p.theme, type);
                    EditorUtility.SetDirty(p.theme);
                }
                EditorUtility.SetDirty(p);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(DungeonTypes.Describe(type), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
        }

        private static void AddSpecialRooms(DungeonProfile p)
        {
            Undo.RecordObject(p, "Add special rooms");
            if (p.roles == null)
                p.roles = new List<RoleRule>();
            int added = 0;
            foreach (RoleRule rule in DungeonProfile.SpecialRoomRules())
            {
                if (p.roles.Exists(r => r != null && r.role == rule.role))
                    continue;
                p.roles.Add(rule);
                added++;
            }
            EditorUtility.SetDirty(p);
            EditorUtility.DisplayDialog("Special rooms", added > 0 ? $"Added {added} special room rule(s). Tune their chances in Roles." : "Every special room is already in the role list.", "OK");
        }

        /// <summary>What is required, what is optional and what the profile falls back to.</summary>
        private static void DrawChecklist(DungeonProfile p)
        {
            var errors = new List<string>();
            var notes = new List<string>();
            StyleWeights w = p.styles;
            if (w == null || w.Total <= 0f)
                errors.Add("Styles: every weight is 0 - set at least one above 0.");
            if (p.sizeClasses == null || p.sizeClasses.Length == 0)
                errors.Add("Size Classes is empty: add at least a Medium entry.");
            if (p.rooms == null || p.rooms.sizes == null || p.rooms.sizes.Length == 0)
                errors.Add("Rooms > Sizes is empty: add at least one room size.");
            float tallest = p.TallestCeiling() + (p.caves != null ? p.caves.floorHeightAmplitude : 0f) + (p.ceilings != null ? p.ceilings.rockBetweenFloors : 0.8f);
            if (p.ceilings != null && !p.ceilings.autoFloorSpacing && p.floorSpacing < tallest)
                errors.Add($"Floor Spacing ({p.floorSpacing}) < tallest ceiling + cave floor variation ({tallest:0.0}): ceilings will be lowered. Turn on Heights > Auto Floor Spacing.");
            if (p.roles != null && !p.roles.Exists(r => r != null && r.role > AreaRole.Custom))
                notes.Add("Roles: no special rooms (guardian, vault, traps, puzzle, ambush, library...) - press Add Special Rooms.");
            if (p.roles == null || !p.roles.Exists(r => r != null && r.role == AreaRole.Boss))
                notes.Add("Roles: no Boss rule (press Default Roles for the standard set).");
            notes.Add(p.theme != null ? $"Theme: {p.theme.name}." : "Theme: none (optional) - placeholder colours, generated walls, built-in portals.");
            notes.Add(p.population != null && p.population.encounters != null ? $"Encounters: {p.population.encounters.name}." : "Encounters: none (optional) - placeholder capsule mobs.");
            notes.Add(p.population != null && p.population.loot != null ? $"Loot: {p.population.loot.name}." : "Loot: none (optional) - built-in chests.");
            notes.Add(p.population != null && p.population.props != null ? $"Props: {p.population.props.name}." : "Props: none (optional) - built-in torches, barrels, altars, traps.");
            if (errors.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            EditorGUILayout.HelpBox(string.Join("\n", notes), MessageType.None);
        }

        private void QuickTest(DungeonProfile p)
        {
            CompiledProfile compiled = CompiledProfile.Compile(p);
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault(compiled.CustomStages);
            int ok = 0, retries = 0;
            var failures = new List<string>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    DungeonLayout l = pipeline.Generate(new DungeonRequest { seed = 1000 + i * 7919, size = (SizeClass)(i % 3) }, compiled);
                    ok++;
                    retries += l.Report.Attempts - 1;
                }
                catch (DungeonGenerationException e)
                {
                    failures.Add(e.Message.Split('\n')[0]);
                }
            }
            quickReport = $"{ok}/20 generated in {watch.Elapsed.TotalMilliseconds:0} ms ({retries} retries)." +
                          (compiled.Warnings.Count > 0 ? "\nWarnings: " + string.Join("; ", compiled.Warnings) : "") +
                          (failures.Count > 0 ? "\nFailures: " + string.Join("; ", failures.Distinct().Take(3)) : "");
            quickType = failures.Count > 0 ? MessageType.Error : (compiled.Warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
        }
    }

    [CustomEditor(typeof(DungeonPropTable))]
    public class DungeonPropTableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var table = (DungeonPropTable)target;
            EditorGUILayout.HelpBox("Props, lights, traps and furniture. Special rooms and floor modifiers this table has nothing for get the built-in props anyway (Profile > Population > Fill Missing Role Props).", MessageType.None);
            if (GUILayout.Button(new GUIContent("Add Missing Built-in Props", "Append the built-in entries this table doesn't have yet (by name): special-room furniture, traps, floor-modifier hazards. Then give them your prefabs.")))
            {
                Undo.RecordObject(table, "Add built-in props");
                int added = DungeonSetupMenu.AddMissingProps(table, null);
                EditorUtility.SetDirty(table);
                EditorUtility.DisplayDialog("Props", added > 0 ? $"Added {added} prop(s)." : "The table already has every built-in prop.", "OK");
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(DungeonEncounterTable))]
    public class DungeonEncounterTableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var table = (DungeonEncounterTable)target;
            EditorGUILayout.HelpBox("Mobs with a NavMeshAgent are placed on the dungeon's NavMesh. Tick Boss for mobs that should only appear in boss rooms.", MessageType.None);
            if (GUILayout.Button("Import mobs from the world (EndlessTerrain's mob settings in the open scene)"))
                Import(table);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        /// <summary>Reads the world's SpawnableMob list through serialization (no World code changes needed).</summary>
        private static void Import(DungeonEncounterTable table)
        {
            EndlessTerrain terrain = Object.FindAnyObjectByType<EndlessTerrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Import mobs", "No EndlessTerrain in the open scene.", "OK");
                return;
            }
            var so = new SerializedObject(terrain);
            SerializedProperty list = so.FindProperty("mobSettings.prefabs");
            if (list == null || !list.isArray)
            {
                EditorUtility.DisplayDialog("Import mobs", "EndlessTerrain has no mob list (mobSettings.prefabs).", "OK");
                return;
            }
            Undo.RecordObject(table, "Import dungeon mobs");
            var mobs = new List<SpawnableMob>();
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                var mob = new SpawnableMob();
                mob.mobPrefab = e.FindPropertyRelative("mobPrefab")?.objectReferenceValue as GameObject;
                if (mob.mobPrefab == null)
                    continue;
                SerializedProperty prop;
                if ((prop = e.FindPropertyRelative("spawnWeight")) != null) mob.spawnWeight = prop.floatValue;
                if ((prop = e.FindPropertyRelative("rarityLevel")) != null) mob.rarityLevel = prop.intValue;
                if ((prop = e.FindPropertyRelative("isPackAnimal")) != null) mob.isPackAnimal = prop.boolValue;
                if ((prop = e.FindPropertyRelative("preferredPackSize")) != null) mob.preferredPackSize = prop.intValue;
                if ((prop = e.FindPropertyRelative("packSpreadRadius")) != null) mob.packSpreadRadius = prop.floatValue;
                mobs.Add(mob);
            }
            int added = SpawnableMobImport.AddAll(table, mobs);
            EditorUtility.SetDirty(table);
            EditorUtility.DisplayDialog("Import mobs", $"Added {added} mob(s).", "OK");
        }
    }

    /// <summary>Inspector for <see cref="DungeonManager"/>: what is required, how it is used by portals, and (in Play mode) its status and test buttons.</summary>
    [CustomEditor(typeof(DungeonManager))]
    public class DungeonManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var m = (DungeonManager)target;
            if (m.profile == null)
                EditorGUILayout.HelpBox("No Profile: assign a Dungeon Profile here (or on every Portal that uses this manager). Create one with Tools > SimpleMovements > Dungeon > Create Default Setup.", MessageType.Warning);
            bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(m);
            if (isPrefab && m.generateOnStart)
                EditorGUILayout.HelpBox("Generate On Start is on: a portal instantiating this prefab would start a second, random dungeon. Turn it off for the portal's prefab.", MessageType.Warning);
            EditorGUILayout.HelpBox(isPrefab
                ? "Portal use: assign this prefab to a Portal's Dungeon Manager. Each visit instantiates it at the Portal's Dungeon Origin, builds the dungeon, and destroys it when the player leaves."
                : "In the scene: call Generate() from code, tick Generate On Start, or let a Portal that references this object use it (it is reused, not destroyed).", MessageType.None);

            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", $"{m.Status} ({m.Progress:P0})");
            if (m.Current != null && m.Current.Layout != null)
                EditorGUILayout.LabelField("Dungeon", $"seed {m.Current.Layout.Seed}, {m.Current.FloorCount} floor(s)");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(m.profile == null || m.IsGenerating))
                {
                    if (GUILayout.Button("Generate"))
                        m.Generate();
                }
                if (GUILayout.Button("Clear"))
                {
                    m.Cancel();
                    m.Clear();
                }
            }
            Repaint();
        }
    }
}
