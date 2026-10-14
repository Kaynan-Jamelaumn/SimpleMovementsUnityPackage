#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleMovements.Setup
{
    /// <summary>
    /// Tools ▸ SimpleMovements ▸ Scene &amp; Prefab Setup: one page per scene and prefab the game needs (Bootstrap, Hub City,
    /// World and Dungeon scenes; Player, Mob and Item prefabs). Each page says what the scene / prefab is for, finds yours
    /// (or lets you pick one, or creates it), and lists every requirement - required, recommended, optional - with what was
    /// found, why it matters, how to do it by hand, and a button that does it. An overview shows every page's state at once.
    /// </summary>
    public class SceneAndPrefabSetupWindow : EditorWindow
    {
        private const string Menu = SMMenu.Root + "Scene and Prefab Setup/";
        private const string PrefsPrefix = "SimpleMovements.Setup.Window.";

        private static List<SetupProfile> profiles;

        /// <summary>Every page, in menu order.</summary>
        public static IReadOnlyList<SetupProfile> Profiles
        {
            get
            {
                if (profiles == null)
                    profiles = new List<SetupProfile>
                    {
                        SceneProfiles.Bootstrap(), SceneProfiles.HubCity(), SceneProfiles.World(), SceneProfiles.Dungeon(),
                        PrefabProfiles.Player(), PrefabProfiles.MobPrefab(), PrefabProfiles.Item(),
                    };
                return profiles;
            }
        }

        private int selected = -1; // -1 = overview
        private readonly Dictionary<string, string> targets = new Dictionary<string, string>();
        private readonly Dictionary<string, List<SetupLine>> results = new Dictionary<string, List<SetupLine>>();
        private SetupReportView view;
        private Vector2 listScroll, pageScroll;
        private bool dirty = true;
        private bool showDescription = true;

        // ------------------------------------------------------------------ menus

        [MenuItem(Menu + "Open Setup Window (overview)...", false, SMMenu.Setup + 0)]
        [MenuItem("Window/SimpleMovements/Scene and Prefab Setup")]
        public static void ShowOverview() => Open(null);

        [MenuItem(Menu + "Bootstrap Scene", false, SMMenu.Setup + 11)] private static void OpenBootstrap() => Open("bootstrap");
        [MenuItem(Menu + "Hub City Scene", false, SMMenu.Setup + 12)] private static void OpenCity() => Open("city");
        [MenuItem(Menu + "World Scene", false, SMMenu.Setup + 13)] private static void OpenWorld() => Open("world");
        [MenuItem(Menu + "Dungeon Scene", false, SMMenu.Setup + 14)] private static void OpenDungeon() => Open("dungeon");
        [MenuItem(Menu + "Player Prefab", false, SMMenu.Setup + 25)] private static void OpenPlayer() => Open("player");
        [MenuItem(Menu + "Mob Prefab", false, SMMenu.Setup + 26)] private static void OpenMob() => Open("mob");
        [MenuItem(Menu + "Item Prefab", false, SMMenu.Setup + 27)] private static void OpenItem() => Open("item");

        /// <summary>Checks the selected prefab (or the active scene) against the page that fits it.</summary>
        [MenuItem(Menu + "Check Selection Or Active Scene", false, SMMenu.Setup + 40)]
        private static void CheckSelection()
        {
            GameObject go = Selection.activeGameObject;
            string path = go != null ? AssetDatabase.GetAssetPath(go) : null;
            if (!string.IsNullOrEmpty(path))
            {
                SetupProfile match = Profiles.FirstOrDefault(p => p.Kind == SetupTargetKind.Prefab && p.IsCandidate != null && p.IsCandidate(go));
                if (match == null)
                {
                    EditorUtility.DisplayDialog("Scene & Prefab Setup", $"'{go.name}' is not a player, mob or item prefab.", "OK");
                    return;
                }
                Open(match.Id, path);
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            SetupProfile sceneMatch = GuessSceneProfile(scene);
            if (sceneMatch == null)
            {
                EditorUtility.DisplayDialog("Scene & Prefab Setup", $"Cannot tell what '{scene.name}' is for. Open the window and pick the page (Bootstrap, Hub City, World or Dungeon).", "OK");
                ShowOverview();
                return;
            }
            Open(sceneMatch.Id, scene.path);
        }

        /// <summary>Opens the page <paramref name="profileId"/> checking <paramref name="targetPath"/> (a scene or prefab asset path).</summary>
        public static SceneAndPrefabSetupWindow Open(string profileId, string targetPath)
        {
            SceneAndPrefabSetupWindow w = Open(profileId);
            if (profileId != null && !string.IsNullOrEmpty(targetPath))
            {
                w.targets[profileId] = targetPath;
                w.dirty = true;
            }
            return w;
        }

        public static SceneAndPrefabSetupWindow Open(string profileId)
        {
            var w = GetWindow<SceneAndPrefabSetupWindow>("Scene & Prefab Setup");
            w.minSize = new Vector2(640f, 420f);
            w.selected = profileId == null ? -1 : Profiles.ToList().FindIndex(p => p.Id == profileId);
            w.dirty = true;
            w.Show();
            return w;
        }

        /// <summary>The page a scene looks like, from what is in it (or its name).</summary>
        private static SetupProfile GuessSceneProfile(Scene scene)
        {
            if (!scene.IsValid())
                return null;
            var ctx = SetupContext.ForScene(scene);
            string id = null;
            if (ctx.All<GameNet.DungeonInstanceSceneMarker>().Count > 0) id = "dungeon";
            else if (ctx.All<HubWorld.RealmScene>().Any(r => r.realm == HubWorld.RealmKind.World) || ctx.All<TerrainGenerator>().Count > 0) id = "world";
            else if (ctx.All<HubWorld.RealmScene>().Any(r => r.realm == HubWorld.RealmKind.City)) id = "city";
            else if (ctx.RootNamed("Network World") != null || ctx.RootNamed("NetworkManager") != null || scene.name.Contains("Bootstrap")) id = "bootstrap";
            else if (scene.name == SetupUtil.HubSettings().citySceneName) id = "city";
            else if (scene.name == SetupUtil.HubSettings().worldSceneName) id = "world";
            return id != null ? Profiles.First(p => p.Id == id) : null;
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            view = new SetupReportView("Window");
            view.Changed += () => { dirty = true; Repaint(); };
            showDescription = EditorPrefs.GetBool(PrefsPrefix + "Description", true);
            foreach (SetupProfile p in Profiles)
            {
                string saved = EditorPrefs.GetString(PrefsPrefix + "Target." + p.Id, "");
                if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
                    targets[p.Id] = saved;
            }
            EditorApplication.hierarchyChanged += MarkDirty;
            EditorApplication.projectChanged += MarkDirty;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkDirty;
            EditorApplication.projectChanged -= MarkDirty;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            foreach (KeyValuePair<string, string> kv in targets)
                EditorPrefs.SetString(PrefsPrefix + "Target." + kv.Key, kv.Value ?? "");
        }

        private void OnSceneOpened(Scene s, OpenSceneMode m) => MarkDirty();
        private void OnSceneClosed(Scene s) => MarkDirty();
        private void OnFocus() => MarkDirty();

        private void MarkDirty()
        {
            dirty = true;
            Repaint();
        }

        // ------------------------------------------------------------------ targets

        private string TargetPath(SetupProfile p)
        {
            if (targets.TryGetValue(p.Id, out string path) && !string.IsNullOrEmpty(path) && File.Exists(path))
                return path;
            string found = p.FindDefault?.Invoke();
            if (!string.IsNullOrEmpty(found))
                targets[p.Id] = found;
            return found;
        }

        /// <summary>The context to check, or null (no target, or a scene that is not open).</summary>
        private SetupContext ContextFor(SetupProfile p)
        {
            string path = TargetPath(p);
            if (string.IsNullOrEmpty(path))
                return null;
            if (p.Kind == SetupTargetKind.Scene)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                return scene.IsValid() && scene.isLoaded ? SetupContext.ForScene(scene) : null;
            }
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
                return SetupContext.ForObject(stage.prefabContentsRoot);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? SetupContext.ForPrefabAsset(path) : null;
        }

        private void Reevaluate()
        {
            dirty = false;
            results.Clear();
            foreach (SetupProfile p in Profiles)
            {
                // The overview checks every page; a page checks only itself (scene checks walk the whole scene).
                if (selected >= 0 && Profiles[selected] != p)
                    continue;
                SetupContext ctx = ContextFor(p);
                if (ctx != null && ctx.IsValid)
                    results[p.Id] = SetupRunner.Evaluate(p, ctx);
            }
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            if (dirty && Event.current.type == EventType.Layout)
                Reevaluate();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                using (new EditorGUILayout.VerticalScope())
                {
                    pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
                    if (selected < 0 || selected >= Profiles.Count)
                        DrawOverview();
                    else
                        DrawPage(Profiles[selected]);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(190f)))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, EditorStyles.helpBox);
                if (ListButton("Overview", selected < 0, null))
                    Select(-1);
                EditorGUILayout.Space(6f);
                GUILayout.Label("Scenes", EditorStyles.miniBoldLabel);
                for (int i = 0; i < Profiles.Count; i++)
                {
                    if (i == Profiles.ToList().FindIndex(p => p.Kind == SetupTargetKind.Prefab))
                    {
                        EditorGUILayout.Space(6f);
                        GUILayout.Label("Prefabs", EditorStyles.miniBoldLabel);
                    }
                    if (ListButton(Profiles[i].Name, selected == i, Profiles[i]))
                        Select(i);
                }
                EditorGUILayout.Space(10f);
                if (GUILayout.Button(new GUIContent("Refresh", "Check again.")))
                    MarkDirty();
                if (GUILayout.Button(new GUIContent("Project Validator…", "Missing scripts, broken references, network prefabs and project assets everywhere.")))
                    Validation.ValidationWindow.ShowWindow();
                EditorGUILayout.EndScrollView();
            }
        }

        private void Select(int index)
        {
            selected = index;
            pageScroll = Vector2.zero;
            dirty = true;
        }

        private bool ListButton(string label, bool active, SetupProfile p)
        {
            string badge = "";
            if (p != null && results.TryGetValue(p.Id, out List<SetupLine> lines))
            {
                var c = SetupReportView.Count(lines);
                badge = c.required > 0 ? "  ✖" : c.recommended > 0 ? "  !" : "  ✔";
            }
            var style = new GUIStyle(active ? EditorStyles.boldLabel : EditorStyles.label) { padding = new RectOffset(6, 4, 3, 3) };
            Rect r = GUILayoutUtility.GetRect(new GUIContent(label + badge), style, GUILayout.ExpandWidth(true));
            if (active)
                EditorGUI.DrawRect(r, new Color(0.24f, 0.48f, 0.9f, 0.35f));
            return GUI.Button(r, label + badge, style);
        }

        // ------------------------------------------------------------------ overview

        private void DrawOverview()
        {
            GUILayout.Label("Scene & Prefab Setup", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "Every scene and prefab the game needs, and whether each is set up. Pick a page on the left for the full list of " +
                "requirements with Fix buttons.\n\n" +
                "Scenes are checked while they are open (open several at once with the Open Additively buttons). Prefabs are " +
                "checked directly from the project.\n\n" +
                "Typical order: Player Prefab → Hub City Scene → World Scene → (multiplayer) Bootstrap Scene and Dungeon Scene → Mob and Item prefabs.",
                MessageType.Info);
            foreach (SetupProfile p in Profiles)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(p.Name, EditorStyles.boldLabel, GUILayout.Width(150f));
                        GUILayout.Label(p.Summary, EditorStyles.wordWrappedMiniLabel);
                        if (GUILayout.Button("Open Page", GUILayout.Width(90f)))
                            Select(Profiles.ToList().IndexOf(p));
                    }
                    string path = TargetPath(p);
                    if (string.IsNullOrEmpty(path))
                        EditorGUILayout.LabelField(p.Kind == SetupTargetKind.Scene ? "No scene found yet." : "No prefab found yet.", EditorStyles.miniLabel);
                    else if (results.TryGetValue(p.Id, out List<SetupLine> lines))
                        EditorGUILayout.HelpBox($"{Path.GetFileNameWithoutExtension(path)}: {SetupReportView.SummaryText(lines)}", SetupReportView.SummaryType(lines));
                    else
                        EditorGUILayout.LabelField($"{path} (not open: open it to check)", EditorStyles.miniLabel);
                }
            }
        }

        // ------------------------------------------------------------------ a page

        private void DrawPage(SetupProfile p)
        {
            GUILayout.Label(p.Name, EditorStyles.largeLabel);
            bool desc = EditorGUILayout.Foldout(showDescription, "What it is", true);
            if (desc != showDescription)
            {
                showDescription = desc;
                EditorPrefs.SetBool(PrefsPrefix + "Description", desc);
            }
            if (showDescription)
            {
                EditorGUILayout.HelpBox(p.Description, MessageType.None);
                if (!string.IsNullOrEmpty(p.DocPath) && GUILayout.Button(new GUIContent("Open Guide: " + p.DocPath, "Opens the documentation page."), EditorStyles.miniButton))
                    DocLinks.Open(p.DocPath);
            }
            EditorGUILayout.Space(4f);
            DrawTarget(p);
            EditorGUILayout.Space(6f);

            SetupContext ctx = ContextFor(p);
            if (ctx == null || !ctx.IsValid || !results.TryGetValue(p.Id, out List<SetupLine> lines))
                return;
            view.DrawSummary(p, ctx, lines);
            EditorGUILayout.Space(4f);
            view.DrawLines(ctx, lines);
        }

        private void DrawTarget(SetupProfile p)
        {
            string path = TargetPath(p);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (p.Kind == SetupTargetKind.Scene)
                {
                    SceneAsset current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    var picked = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent("Scene", "The scene checked by this page."), current, typeof(SceneAsset), false);
                    if (picked != current)
                    {
                        targets[p.Id] = picked != null ? AssetDatabase.GetAssetPath(picked) : "";
                        dirty = true;
                    }
                    if (!string.IsNullOrEmpty(path))
                    {
                        Scene scene = SceneManager.GetSceneByPath(path);
                        bool open = scene.IsValid() && scene.isLoaded;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(open ? "Open: checked live." : "Not open: open it to check it.", open ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
                            if (!open)
                            {
                                if (GUILayout.Button("Open", GUILayout.Width(70f)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                {
                                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                                    dirty = true;
                                }
                                if (GUILayout.Button(new GUIContent("Open Additively", "Keeps the open scenes (e.g. check the City and the Bootstrap together)."), GUILayout.Width(120f)))
                                {
                                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                                    dirty = true;
                                }
                            }
                            else if (scene.isDirty && GUILayout.Button("Save Scene", GUILayout.Width(90f)))
                                EditorSceneManager.SaveScene(scene);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"No {p.Name.ToLowerInvariant()} found in the project. Pick one above" +
                                                (p.Create != null ? $", or create it:" : ", or open the scene you use and press 'Use Active Scene'."), MessageType.Warning);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Scene active = SceneManager.GetActiveScene();
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(active.path) || active.path == path))
                            if (GUILayout.Button(new GUIContent($"Use Active Scene ({active.name})", "Check the scene that is active in the Hierarchy with this page.")))
                            {
                                targets[p.Id] = active.path;
                                dirty = true;
                            }
                        if (p.Create != null && GUILayout.Button(new GUIContent(p.CreateLabel, "Creates a new one with everything this page needs.")))
                            EditorApplication.delayCall += () =>
                            {
                                p.Create();
                                targets.Remove(p.Id);
                                MarkDirty();
                            };
                    }
                }
                else
                {
                    GameObject current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var picked = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab", "The prefab checked by this page."), current, typeof(GameObject), false);
                    if (picked != current)
                    {
                        string newPath = picked != null ? AssetDatabase.GetAssetPath(picked) : "";
                        if (picked != null && string.IsNullOrEmpty(newPath))
                            EditorUtility.DisplayDialog("Scene & Prefab Setup", "Pick a prefab asset from the Project window (not an object in the scene).", "OK");
                        else
                        {
                            targets[p.Id] = newPath;
                            dirty = true;
                        }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GameObject sel = Selection.activeGameObject;
                        string selPath = sel != null ? AssetDatabase.GetAssetPath(sel) : null;
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selPath) || selPath == path))
                            if (GUILayout.Button(new GUIContent("Use Selected Prefab", "The prefab selected in the Project window.")))
                            {
                                targets[p.Id] = selPath;
                                dirty = true;
                            }
                        if (GUILayout.Button(new GUIContent("Other Candidates ▾", "Prefabs of the project that look like this kind.")))
                            ShowCandidates(p);
                        using (new EditorGUI.DisabledScope(current == null))
                            if (GUILayout.Button("Open Prefab", GUILayout.Width(100f)))
                                AssetDatabase.OpenAsset(current);
                    }
                    if (current == null)
                        EditorGUILayout.HelpBox($"No {p.Name.ToLowerInvariant()} found in the project. Pick one above.", MessageType.Warning);
                    else if (PrefabStageUtility.GetCurrentPrefabStage() is PrefabStage stage && stage.assetPath == path)
                        EditorGUILayout.LabelField("Open in Prefab Mode: fixes change the open prefab (save it afterwards).", EditorStyles.miniBoldLabel);
                    else
                        EditorGUILayout.LabelField("Fixes are saved to the prefab asset at once (Undo does not cover them).", EditorStyles.miniLabel);
                }
            }
        }

        private void ShowCandidates(SetupProfile p)
        {
            var menu = new GenericMenu();
            int count = 0;
            foreach (string path in Validation.ValidationUtility.ProjectPrefabPaths())
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || p.IsCandidate == null || !p.IsCandidate(go))
                    continue;
                string captured = path;
                menu.AddItem(new GUIContent(path.Replace('/', '∕')), TargetPath(p) == path, () =>
                {
                    targets[p.Id] = captured;
                    MarkDirty();
                });
                if (++count >= 60)
                    break;
            }
            if (count == 0)
                menu.AddDisabledItem(new GUIContent("No matching prefab in Assets/"));
            menu.ShowAsContext();
        }
    }

    /// <summary>Opens a page of the Documentation folder.</summary>
    public static class DocLinks
    {
        /// <summary>Opens Documentation/<paramref name="relative"/> (e.g. "Player/01-Player-Setup.md"); false when not found.</summary>
        public static bool Open(string relative)
        {
            string path = Find(relative);
            if (path == null)
            {
                EditorUtility.DisplayDialog("Documentation", $"Documentation/{relative} was not found in the project.", "OK");
                return false;
            }
            EditorUtility.OpenWithDefaultApp(path);
            return true;
        }

        public static string Find(string relative)
        {
            if (string.IsNullOrEmpty(relative))
                return null;
            string file = Path.GetFileNameWithoutExtension(relative);
            foreach (string guid in AssetDatabase.FindAssets(file))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("Documentation/" + relative, StringComparison.Ordinal))
                    return p;
            }
            return null;
        }
    }
}
#endif
