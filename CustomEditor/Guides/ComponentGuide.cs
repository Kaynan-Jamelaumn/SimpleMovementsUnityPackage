#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleMovements.Guides
{
    /// <summary>One live check shown in a component's guide: good or not, what was found, and an optional fix.</summary>
    public sealed class GuideCheck
    {
        public bool ok;
        /// <summary>Not a problem either way: a note (blue "i").</summary>
        public bool info;
        public string label;
        public string detail;
        public string fixLabel;
        public Action fix;
        public Object select;
    }

    /// <summary>
    /// What a component's inspector explains about it: what it does, where it belongs, how it works, the live state of
    /// its setup (with fixes), handy actions, and values to watch in Play mode. Drawn by <see cref="ComponentGuide.Draw"/>
    /// above the component's own fields.
    /// </summary>
    public sealed class Guide
    {
        public string id;
        public string title;
        /// <summary>One or two sentences: what it does.</summary>
        public string summary;
        /// <summary>Where it goes (which scene / object), and whether it is created automatically.</summary>
        public string placement;
        /// <summary>How it works, one point per line.</summary>
        public readonly List<string> howItWorks = new List<string>();
        /// <summary>Documentation/… page.</summary>
        public string doc;
        public readonly List<GuideCheck> checks = new List<GuideCheck>();
        public readonly List<(string label, string tooltip, Action action)> actions = new List<(string, string, Action)>();
        public readonly List<(string label, string value)> live = new List<(string, string)>();

        public Guide(string id, string title, string summary)
        {
            this.id = id;
            this.title = title;
            this.summary = summary;
        }

        public Guide Where(string text)
        {
            placement = text;
            return this;
        }

        public Guide How(params string[] points)
        {
            howItWorks.AddRange(points);
            return this;
        }

        public Guide Doc(string path)
        {
            doc = path;
            return this;
        }

        public Guide Check(bool ok, string label, string detail, string fixLabel = null, Action fix = null, Object select = null)
        {
            checks.Add(new GuideCheck { ok = ok, label = label, detail = detail, fixLabel = fixLabel, fix = fix, select = select });
            return this;
        }

        public Guide Note(string label, string detail, Object select = null)
        {
            checks.Add(new GuideCheck { ok = true, info = true, label = label, detail = detail, select = select });
            return this;
        }

        public Guide Action(string label, string tooltip, Action action)
        {
            actions.Add((label, tooltip, action));
            return this;
        }

        public Guide Live(string label, string value)
        {
            live.Add((label, value));
            return this;
        }
    }

    /// <summary>Draws a <see cref="Guide"/>: an "About" foldout, the setup status with fixes, actions and Play-mode values.</summary>
    public static class ComponentGuide
    {
        private const string Prefs = "SimpleMovements.Guide.";
        private static GUIStyle okStyle, warnStyle, infoStyle, mutedStyle, bulletStyle;

        private static void Styles()
        {
            if (okStyle != null)
                return;
            okStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.35f, 0.8f, 0.4f) } };
            warnStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.98f, 0.72f, 0.25f) } };
            infoStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.55f, 0.7f, 0.95f) } };
            mutedStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
            bulletStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true, fontSize = 11 };
        }

        private static readonly Dictionary<Object, (double until, Guide guide)> cache = new Dictionary<Object, (double, Guide)>();

        /// <summary>
        /// For inspectors that have their own layout: draws the guide of <paramref name="owner"/>, rebuilt at most twice a
        /// second (the checks look at the open scenes).
        /// </summary>
        public static void DrawCached(Object owner, Func<Guide> build)
        {
            if (owner == null)
                return;
            Object id = owner; // keyed by the object itself (instance ids are obsolete in Unity 6)
            if (Event.current.type == EventType.Layout && (!cache.TryGetValue(id, out var entry) || EditorApplication.timeSinceStartup >= entry.until))
            {
                Guide g;
                try { g = build(); }
                catch (Exception e) { g = new Guide("error", owner.GetType().Name, "The guide could not be built: " + e.Message); }
                if (cache.Count > 64)
                    cache.Clear();
                cache[id] = (EditorApplication.timeSinceStartup + 0.5, g);
            }
            if (cache.TryGetValue(id, out var shown))
                Draw(shown.guide);
        }

        public static void Draw(Guide g)
        {
            if (g == null)
                return;
            Styles();
            string aboutKey = Prefs + g.id + ".About";
            bool about = EditorGUILayout.BeginFoldoutHeaderGroup(EditorPrefs.GetBool(aboutKey, true), "About: " + g.title);
            EditorPrefs.SetBool(aboutKey, about);
            if (about)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(g.summary, bulletStyle);
                    if (!string.IsNullOrEmpty(g.placement))
                        GUILayout.Label("<b>Where it goes:</b> " + g.placement, mutedStyle);
                    if (g.howItWorks.Count > 0)
                    {
                        GUILayout.Label("<b>How it works</b>", mutedStyle);
                        foreach (string point in g.howItWorks)
                            GUILayout.Label("•  " + point, mutedStyle);
                    }
                    if (!string.IsNullOrEmpty(g.doc) || g.actions.Count > 0)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (!string.IsNullOrEmpty(g.doc) && GUILayout.Button(new GUIContent("Open Guide", "Documentation/" + g.doc), EditorStyles.miniButton, GUILayout.Width(90f)))
                                Setup.DocLinks.Open(g.doc);
                            foreach (var (label, tooltip, action) in g.actions)
                                if (GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton))
                                    EditorApplication.delayCall += () => action();
                            GUILayout.FlexibleSpace();
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (g.checks.Count > 0)
            {
                int problems = g.checks.FindAll(c => !c.ok && !c.info).Count;
                string statusKey = Prefs + g.id + ".Status";
                bool status = EditorGUILayout.BeginFoldoutHeaderGroup(EditorPrefs.GetBool(statusKey, true) || problems > 0,
                    problems == 0 ? "Setup: ✔ all good" : $"Setup: {problems} to look at");
                EditorPrefs.SetBool(statusKey, status);
                if (status)
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        foreach (GuideCheck c in g.checks)
                            DrawCheck(c);
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            if (Application.isPlaying && g.live.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Now (Play mode)", EditorStyles.miniBoldLabel);
                    foreach (var (label, value) in g.live)
                        EditorGUILayout.LabelField(label, value, EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUILayout.Space(4f);
        }

        private static void DrawCheck(GuideCheck c)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(c.info ? "i" : c.ok ? "✔" : "!", c.info ? infoStyle : c.ok ? okStyle : warnStyle, GUILayout.Width(14f));
                GUILayout.Label(c.label, EditorStyles.boldLabel, GUILayout.MinWidth(80f));
                GUILayout.FlexibleSpace();
                if (c.select != null && GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    Selection.activeObject = c.select;
                    EditorGUIUtility.PingObject(c.select);
                }
                if (!c.ok && c.fix != null && !string.IsNullOrEmpty(c.fixLabel))
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                        if (GUILayout.Button(c.fixLabel, EditorStyles.miniButton, GUILayout.MaxWidth(170f)))
                        {
                            Action fix = c.fix;
                            EditorApplication.delayCall += () => fix();
                        }
            }
            if (!string.IsNullOrEmpty(c.detail))
                GUILayout.Label(c.detail, mutedStyle);
        }
    }

    /// <summary>
    /// An inspector that draws a <see cref="Guide"/> above the default fields. The guide is rebuilt twice a second (its
    /// checks look at the scene), and the inspector repaints in Play mode for the live values.
    /// </summary>
    public abstract class GuidedEditor : Editor
    {
        private Guide guide;
        private double nextBuild;

        protected abstract Guide BuildGuide();

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            if (Event.current.type == EventType.Layout && (guide == null || EditorApplication.timeSinceStartup >= nextBuild))
            {
                nextBuild = EditorApplication.timeSinceStartup + 0.5;
                try
                {
                    guide = BuildGuide();
                }
                catch (Exception e)
                {
                    guide = new Guide("error", target.GetType().Name, "The guide could not be built: " + e.Message);
                }
            }
            ComponentGuide.Draw(guide);
            DrawFields();
        }

        /// <summary>The component's own fields (override for a custom layout).</summary>
        protected virtual void DrawFields() => DrawDefaultInspector();
    }

    /// <summary>Small helpers for the guides.</summary>
    public static class GuideUtil
    {
        public static List<T> InOpenScenes<T>() where T : Component =>
            new List<T>(Object.FindObjectsByType<T>(FindObjectsInactive.Include));

        /// <summary>A short "Scene ▸ Object" path.</summary>
        public static string Where(Component c) => c == null ? "" : $"{c.gameObject.scene.name} ▸ {c.name}";

        /// <summary>A check that there is one of <typeparamref name="T"/> in the open scenes.</summary>
        public static void One<T>(Guide g, string label, string why) where T : Component
        {
            List<T> all = InOpenScenes<T>();
            if (all.Count <= 1)
                g.Check(true, label, all.Count == 1 ? $"One, in {Where(all[0])}." : "None in the open scenes.");
            else
                g.Check(false, label, $"{all.Count} in the open scenes ({string.Join(", ", all.ConvertAll(Where))}). {why}", select: all[1]);
        }

        /// <summary>
        /// Multiplayer: is the global relay <paramref name="typeName"/> on the open Network World object? (A note when no
        /// Network World is open, nothing without Fish-Net.)
        /// </summary>
        public static void Relay(Guide g, string typeName, string what)
        {
#if FISHNET
            Type t = Setup.SetupUtil.TypeByName(typeName);
            if (t == null)
                return;
            string shortName = Setup.SetupUtil.ShortName(typeName);
            GameObject world = null;
            foreach (FishNet.Object.NetworkObject nob in Object.FindObjectsByType<FishNet.Object.NetworkObject>(FindObjectsInactive.Include))
                if (nob.name == "Network World")
                    world = nob.gameObject;
            if (world == null)
            {
                g.Note($"Multiplayer: {shortName}", $"{what} needs the {shortName} relay on the Bootstrap scene's Network World (open the Bootstrap scene to check it).");
                return;
            }
            bool has = world.GetComponent(t) != null;
            GameObject w = world;
            g.Check(has, $"Multiplayer: {shortName} relay", has ? "On the Network World." : $"Not on the Network World: {what} does not work online.",
                "Add Relay", () =>
                {
                    Undo.AddComponent(w, t);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(w.scene);
                }, w);
#endif
        }
    }
}
#endif
