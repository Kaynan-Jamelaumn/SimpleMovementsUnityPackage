#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleMovements.Validation
{
    /// <summary>
    /// Checks of the game's setup that are not about one object: the Input Actions asset (every action the scripts
    /// read, keys bound twice) and the open scenes (Weather System, Player Weather Effects, Day Night Cycle, network
    /// relays, targeting and parties). Run by the Project Validator (Game Setup) and from Tools ▸ SimpleMovements ▸
    /// Validate ▸ Input Actions / Scene Setup; most issues have a Fix.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupChecks
    {
        /// <summary>One action the scripts look up by name in the Input Actions asset.</summary>
        public readonly struct RequiredAction
        {
            public readonly string name;
            public readonly string key;
            public readonly string usedBy;
            public readonly bool core;

            public RequiredAction(string name, string key, string usedBy, bool core = false)
            {
                this.name = name;
                this.key = key;
                this.usedBy = usedBy;
                this.core = core;
            }
        }

        /// <summary>Every action read by name (core ones are needed to play at all).</summary>
        public static readonly RequiredAction[] Required =
        {
            new RequiredAction("Movement", null, "movement", true),
            new RequiredAction("Look", null, "camera", true),
            new RequiredAction("Jump", "<Keyboard>/space", "movement, swimming up", true),
            new RequiredAction("Sprint", "<Keyboard>/ctrl", "movement", true),
            new RequiredAction("Crouch", "<Keyboard>/shift", "movement, diving", true),
            new RequiredAction("Use", "<Mouse>/leftButton", "attacks and using items", true),
            new RequiredAction("Interact", "<Keyboard>/e", "doors, NPCs, pickups", true),
            new RequiredAction("Inventory", "<Keyboard>/tab", "inventory", true),
            new RequiredAction("Pause", "<Keyboard>/escape", "pause menu", true),
            new RequiredAction("Dash", "<Keyboard>/q", "dash"),
            new RequiredAction("Roll", "<Keyboard>/alt", "roll"),
            new RequiredAction("Block", "<Mouse>/middleButton", "blocking"),
            new RequiredAction("Reload", "<Keyboard>/b", "ranged weapons"),
            new RequiredAction("OffHandAttack", "<Keyboard>/h", "off-hand weapon"),
            new RequiredAction("Sheathe", "<Keyboard>/n", "drawing / sheathing"),
            new RequiredAction("RotateItem", "<Keyboard>/r", "grid inventory"),
            new RequiredAction("QuickSlot1", "<Keyboard>/f1", "quick slots"),
            new RequiredAction("QuickSlot2", "<Keyboard>/f2", "quick slots"),
            new RequiredAction("QuickSlot3", "<Keyboard>/f3", "quick slots"),
            new RequiredAction("QuickSlot4", "<Keyboard>/f4", "quick slots"),
            new RequiredAction("ActiveTrait", "<Keyboard>/j", "active traits"),
            new RequiredAction("InteractClick", "<Mouse>/leftButton", "clicking on interactables"),
            new RequiredAction("SelectTarget", "<Mouse>/leftButton", "clicking a player to target them"),
            new RequiredAction("CycleTarget", "<Keyboard>/k", "next target"),
            new RequiredAction("InspectTarget", "<Keyboard>/u", "inspecting the target"),
            new RequiredAction("TargetMenu", "<Keyboard>/o", "the target's menu"),
            new RequiredAction("Trade", "<Keyboard>/l", "trading"),
            new RequiredAction("AcceptRequest", "<Keyboard>/y", "accepting party invites and duels"),
            new RequiredAction("DeclineRequest", "<Keyboard>/backspace", "declining party invites and duels"),
            new RequiredAction("Map", "<Keyboard>/m", "dungeon map"),
        };

        /// <summary>Keys that may be shared on purpose (a click that selects, interacts and attacks).</summary>
        private static readonly string[][] SharedOnPurpose =
        {
            new[] { "Use", "SelectTarget", "InteractClick" },
            new[] { "Ability1", "RotateItem" }, // rotating only happens in the inventory
        };

        static SetupChecks()
        {
            ProjectValidator.ValidateProject += OnValidateProject;
        }

        private static void OnValidateProject(ValidationChecks checks, List<ValidationIssue> issues)
        {
            if ((checks & ValidationChecks.GameSetup) == 0)
                return;
            CheckInputActions(issues);
            CheckOpenScenes(issues);
        }

        // ------------------------------------------------------------------ menus

        [MenuItem(SMMenu.Root + "Validate/Input Actions", false, SMMenu.Validate + 28)]
        [MenuItem(SMMenu.Root + "Input/Check Input Actions Asset", false, SMMenu.Input + 0)]
        private static void ValidateInputMenu() => Report("Input actions", CheckInputActions);

        [MenuItem(SMMenu.Root + "Validate/Scene Setup (open scenes)", false, SMMenu.Validate + 29)]
        private static void ValidateScenesMenu() => Report("Scene setup", CheckOpenScenes);

        [MenuItem(SMMenu.Root + "Auto-Assign/Add Missing Input Actions", false, SMMenu.AutoAssign + 62)]
        [MenuItem(SMMenu.Root + "Input/Add Missing Input Actions", false, SMMenu.Input + 1)]
        private static void AddMissingInputMenu()
        {
            var issues = new List<ValidationIssue>();
            CheckInputActions(issues);
            List<ValidationIssue> missing = issues.Where(i => i.CanFix && i.category == "Input" && i.fixLabel == "Add").ToList();
            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("Input Actions", issues.Any(i => i.message.Contains("No Input Actions asset"))
                    ? "No Input Actions asset with a Player map was found." : "Every action the scripts read is in your Input Actions asset.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Input Actions", $"Add {missing.Count} action(s) to your Input Actions asset?\n\n" +
                string.Join("\n", missing.Select(i => "• " + i.message)), "Add", "Cancel"))
                return;
            int n = missing.Count(i => i.fix());
            EditorUtility.DisplayDialog("Input Actions", $"Added {n} action(s). Change their keys in the Input Actions editor (double-click the asset) or in the game's Key Bindings.", "OK");
        }

        [MenuItem(SMMenu.Root + "Auto-Assign/Fix Scene Setup (open scenes)", false, SMMenu.AutoAssign + 63)]
        private static void FixScenesMenu()
        {
            var issues = new List<ValidationIssue>();
            CheckOpenScenes(issues);
            List<ValidationIssue> fixable = issues.Where(i => i.CanFix).ToList();
            if (fixable.Count == 0)
            {
                EditorUtility.DisplayDialog("Scene Setup", issues.Count == 0 ? "The open scenes are set up." :
                    $"{issues.Count} note(s), none fixable automatically - see Validate ▸ Scene Setup.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Scene Setup", $"Apply {fixable.Count} fix(es)?\n\n" + string.Join("\n", fixable.Select(i => "• " + i.fixLabel + ": " + i.message)), "Fix", "Cancel"))
                return;
            int n = fixable.Count(i => i.fix());
            EditorUtility.DisplayDialog("Scene Setup", $"Applied {n} fix(es). Save the scenes.", "OK");
        }

        private static void Report(string title, Action<List<ValidationIssue>> check)
        {
            var issues = new List<ValidationIssue>();
            check(issues);
            foreach (ValidationIssue i in issues)
                i.Log();
            int errors = issues.Count(i => i.severity == IssueSeverity.Error), warnings = issues.Count(i => i.severity == IssueSeverity.Warning);
            string summary = issues.Count == 0 ? "No problems found." : $"{errors} error(s), {warnings} warning(s), {issues.Count - errors - warnings} note(s) - see the Console.";
            Debug.Log($"[Validator] {title}: {summary}");
            if (issues.Count > 0 && EditorUtility.DisplayDialog(title, summary + "\n\nThe Project Validator window shows them with Fix buttons (check Game Setup).", "Open Project Validator", "Close"))
                ValidationWindow.Open(ValidationScope.OpenScenes, ValidationChecks.GameSetup);
        }

        // ------------------------------------------------------------------ input actions

        /// <summary>The project's Input Actions assets with a Player map.</summary>
        public static List<string> InputAssetPaths()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:InputActionAsset", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null && asset.FindActionMap("Player", false) != null)
                    paths.Add(path);
            }
            return paths;
        }

        public static void CheckInputActions(List<ValidationIssue> issues)
        {
            List<string> paths = InputAssetPaths();
            if (paths.Count == 0)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Input",
                    "No Input Actions asset with a Player map was found under Assets/: the scripts read their keys from one."));
                return;
            }
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                InputActionMap map = asset.FindActionMap("Player", false);
                foreach (RequiredAction r in Required)
                {
                    if (map.FindAction(r.name, false) != null || asset.FindAction(r.name, false) != null)
                        continue;
                    string key = FreeKey(map, r.key);
                    var issue = new ValidationIssue(r.core ? IssueSeverity.Error : IssueSeverity.Warning, "Input",
                        $"'{r.name}' is missing from {Path.GetFileName(path)} (used by {r.usedBy})" +
                        (key != null ? $" - it would be added on {Describe(key)}." : r.key != null ? $" - {Describe(r.key)} is taken, it would be added without a key." : "."),
                        asset, path);
                    RequiredAction captured = r;
                    issue.WithFix("Add", () => AddAction(path, captured.name, FreeKey(AssetDatabase.LoadAssetAtPath<InputActionAsset>(path).FindActionMap("Player"), captured.key)));
                    issues.Add(issue);
                }
                CheckDoubleKeys(path, map, issues);
            }
        }

        /// <summary><paramref name="wanted"/> when no action of <paramref name="map"/> uses it yet (clicks may be shared), else null.</summary>
        private static string FreeKey(InputActionMap map, string wanted)
        {
            if (string.IsNullOrEmpty(wanted) || map == null)
                return null;
            if (wanted == "<Mouse>/leftButton")
                return wanted;
            foreach (InputBinding b in map.bindings)
                if (!b.isComposite && string.Equals(b.effectivePath, wanted, StringComparison.OrdinalIgnoreCase))
                    return null;
            return wanted;
        }

        private static void CheckDoubleKeys(string path, InputActionMap map, List<ValidationIssue> issues)
        {
            var byKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (InputBinding b in map.bindings)
            {
                if (b.isComposite || string.IsNullOrEmpty(b.effectivePath) || string.IsNullOrEmpty(b.action))
                    continue;
                if (!byKey.TryGetValue(b.effectivePath, out HashSet<string> actions))
                    byKey[b.effectivePath] = actions = new HashSet<string>();
                actions.Add(b.action);
            }
            foreach (KeyValuePair<string, HashSet<string>> kv in byKey)
            {
                if (kv.Value.Count < 2)
                    continue;
                bool intended = SharedOnPurpose.Any(set => kv.Value.All(a => set.Contains(a)));
                issues.Add(new ValidationIssue(intended ? IssueSeverity.Info : IssueSeverity.Warning, "Input",
                    $"{Describe(kv.Key)} is bound to {string.Join(", ", kv.Value)} in {Path.GetFileName(path)}" +
                    (intended ? " (shared on purpose)." : ": pressing it triggers all of them. Rebind one in the Input Actions editor."),
                    AssetDatabase.LoadAssetAtPath<InputActionAsset>(path), path));
            }
        }

        /// <summary>Adds a button action (with <paramref name="key"/>, if any) to the Player map of the asset file.</summary>
        public static bool AddAction(string path, string name, string key)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null)
                return false;
            InputActionAsset copy = InputActionAsset.FromJson(asset.ToJson());
            try
            {
                InputActionMap map = copy.FindActionMap("Player", false);
                if (map == null || map.FindAction(name, false) != null)
                    return false;
                InputAction action = map.AddAction(name, InputActionType.Button);
                if (!string.IsNullOrEmpty(key))
                    action.AddBinding(key).WithGroup(key.StartsWith("<Gamepad>") ? "Gamepad" : "Keyboard&Mouse");
                File.WriteAllText(path, copy.ToJson());
                AssetDatabase.ImportAsset(path);
                Debug.Log($"[Input] Added '{name}'{(string.IsNullOrEmpty(key) ? " (no key - bind it)" : " on " + Describe(key))} to {path}.");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private static string Describe(string path) =>
            InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);

        // ------------------------------------------------------------------ scenes

        private static List<T> InOpenScenes<T>() where T : Component
        {
            var list = new List<T>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded)
                    continue;
                foreach (GameObject root in s.GetRootGameObjects())
                    list.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return list;
        }

        private static string Where(Component c) => c.gameObject.scene.name + " ▸ " + ValidationUtility.PathOf(c.transform);

        public static void CheckOpenScenes(List<ValidationIssue> issues)
        {
            CheckWeather(issues);
            CheckDayNight(issues);
            CheckSingletons<PlayerTargeting>(issues, "Player Targeting");
            CheckSingletons<PartySystem>(issues, "Party System");
#if FISHNET
            CheckNetworkWorld(issues);
#endif
        }

        private static void CheckWeather(List<ValidationIssue> issues)
        {
            List<WeatherSystem> weathers = InOpenScenes<WeatherSystem>();
            if (weathers.Count > 1)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Weather", $"{weathers.Count} Weather Systems are open: keep one, in the World scene.", weathers[1], Where(weathers[1])));
            foreach (WeatherSystem w in weathers)
            {
                bool worldScene = w.gameObject.scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<EndlessTerrain>(true) != null || r.GetComponentInChildren<TerrainGenerator>(true) != null);
                if (!worldScene)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, "Weather",
                        "The Weather System is not in a scene with the terrain (EndlessTerrain / TerrainGenerator): put it in the World scene - players in its scene are the ones who get weather.", w, Where(w)));
                if (w.GetComponentInParent<PlayerStatusController>() != null)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, "Weather", "The Weather System is on a player: it belongs in the World scene.", w, Where(w)));
            }

            List<PlayerWeatherEffects> effects = InOpenScenes<PlayerWeatherEffects>();
            if (weathers.Count > 0 && effects.Count == 0)
            {
                WeatherSystem w = weathers[0];
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Weather", "No Player Weather Effects: the weather is only visual (no Soaked, Chilled, Heatstroke...).", w, Where(w))
                    .WithFix("Add Player Weather Effects", () =>
                    {
                        Undo.AddComponent<PlayerWeatherEffects>(w.gameObject);
                        EditorUtility.SetDirty(w.gameObject);
                        return true;
                    }));
            }
            foreach (PlayerWeatherEffects fx in effects)
            {
                if (fx.GetComponentInParent<PlayerStatusController>() != null)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Error, "Weather",
                        "Player Weather Effects is on a player: it must be on the Weather System object (one for all players).", fx, Where(fx))
                        .WithFix("Remove from the player", () => { Undo.DestroyObjectImmediate(fx); return true; }));
                    continue;
                }
                // The same condition in both lists applies twice.
                var assetLabels = new HashSet<string>(fx.conditions.Where(c => c != null && c.rule != null).Select(c => c.rule.label));
                List<WeatherExposureRule> doubled = fx.rules.Where(r => r != null && assetLabels.Contains(r.label)).ToList();
                if (doubled.Count > 0)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, "Weather",
                        $"{string.Join(", ", doubled.Select(r => r.label))} is both in Conditions (asset) and in Rules: it applies twice.", fx, Where(fx))
                        .WithFix("Remove them from Rules", () =>
                        {
                            Undo.RecordObject(fx, "Remove doubled weather rules");
                            fx.rules.RemoveAll(r => r != null && assetLabels.Contains(r.label));
                            EditorUtility.SetDirty(fx);
                            return true;
                        }));
                foreach (WeatherCondition c in fx.conditions)
                    if (c == null)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Info, "Weather", "Player Weather Effects has an empty Conditions slot.", fx, Where(fx))
                            .WithFix("Remove empty slots", () => { Undo.RecordObject(fx, "Remove empty"); fx.conditions.RemoveAll(x => x == null); EditorUtility.SetDirty(fx); return true; }));
                        break;
                    }
                // Shelter must not hit the players themselves.
                int playerLayer = PlayerLayer();
                if (playerLayer >= 0 && (fx.shelterLayers.value & (1 << playerLayer)) != 0)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, "Weather",
                        $"Shelter Layers includes '{LayerMask.LayerToName(playerLayer)}', the players' layer: a player's own collider could count as a roof.", fx, Where(fx))
                        .WithFix("Exclude the player layer", () =>
                        {
                            Undo.RecordObject(fx, "Exclude player layer");
                            fx.shelterLayers &= ~(1 << playerLayer);
                            EditorUtility.SetDirty(fx);
                            return true;
                        }));
            }
        }

        /// <summary>The layer the players use (a player in the open scenes, else a layer named Player), or -1.</summary>
        private static int PlayerLayer()
        {
            PlayerStatusController p = InOpenScenes<PlayerStatusController>().FirstOrDefault();
            if (p != null && p.gameObject.layer != 0)
                return p.gameObject.layer;
            return LayerMask.NameToLayer("Player");
        }

        private static void CheckDayNight(List<ValidationIssue> issues)
        {
            List<DayNightCycle> cycles = InOpenScenes<DayNightCycle>();
            if (cycles.Count > 1)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Day/Night",
                    $"{cycles.Count} Day Night Cycles are open ({string.Join(", ", cycles.Select(Where))}): keep one, in the scene that loads first (Bootstrap or City). The extra ones remove themselves at runtime.",
                    cycles[1], Where(cycles[1])));
            foreach (DayNightCycle c in cycles)
                if (c.GetComponentInParent<PlayerStatusController>() != null)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, "Day/Night", "A Day Night Cycle is on a player: put one in the Bootstrap or City scene instead.", c, Where(c))
                        .WithFix("Remove from the player", () => { Undo.DestroyObjectImmediate(c); return true; }));
            if (cycles.Count == 0 && InOpenScenes<WeatherSystem>().Count > 0)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Day/Night",
                    "No Day Night Cycle in the open scenes: if no other scene has one there is no night, and Day / Night spawn rules let everything through. (Put it in the Bootstrap or City scene.)"));
        }

        private static void CheckSingletons<T>(List<ValidationIssue> issues, string label) where T : Component
        {
            List<T> list = InOpenScenes<T>();
            if (list.Count > 1)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Social",
                    $"{list.Count} {label} objects are open: keep one (in a scene that stays loaded).", list[1], Where(list[1])));
        }

#if FISHNET
        private static void CheckNetworkWorld(List<ValidationIssue> issues)
        {
            foreach (FishNet.Object.NetworkObject nob in InOpenScenes<FishNet.Object.NetworkObject>())
            {
                if (nob.name != "Network World")
                    continue;
                var missing = new List<string>();
                foreach (string typeName in GameNet.EditorTools.NetworkSetupMenu.GlobalRelayComponents)
                {
                    Type t = GameNet.EditorTools.NetworkSetupMenu.FindType(typeName);
                    if (t != null && nob.GetComponent(t) == null)
                        missing.Add(t.Name);
                }
                if (missing.Count == 0)
                    continue;
                GameObject go = nob.gameObject;
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Network",
                    $"The Network World is missing {string.Join(", ", missing)}: those systems do not work online (e.g. NetSocial = parties and inspection).", go, Where(nob))
                    .WithFix("Add the relays", () =>
                    {
                        Undo.RegisterFullObjectHierarchyUndo(go, "Add relays");
                        GameNet.EditorTools.NetworkSetupMenu.AddByName(go, GameNet.EditorTools.NetworkSetupMenu.GlobalRelayComponents, null);
                        EditorUtility.SetDirty(go);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
                        return true;
                    }));
            }
        }
#endif
    }
}
#endif
