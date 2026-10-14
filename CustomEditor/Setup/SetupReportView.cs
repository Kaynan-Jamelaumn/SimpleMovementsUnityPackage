#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleMovements.Setup
{
    /// <summary>
    /// Draws the results of a <see cref="SetupProfile"/>: a summary with progress and Fix All buttons, then every
    /// requirement grouped (Network Manager, Realm, Movement...), each with its status, what was found, why it matters,
    /// how to do it by hand, and Select / Fix buttons. Used by the Scene &amp; Prefab Setup window and the Player Setup
    /// Validator's inspector. Fixes run after the GUI pass; <see cref="Changed"/> is raised afterwards so the owner checks
    /// again.
    /// </summary>
    public sealed class SetupReportView
    {
        private const string Prefs = "SimpleMovements.Setup.";

        public bool showPassed = true;
        public bool showExplanations = true;
        private readonly Dictionary<string, bool> folded = new Dictionary<string, bool>();
        private static GUIStyle okStyle, missingStyle, wrongStyle, noteStyle, titleStyle, mutedStyle, badgeStyle, detailStyle;

        /// <summary>A fix changed something: check again.</summary>
        public event Action Changed;

        public SetupReportView(string prefsKey = "View")
        {
            showPassed = EditorPrefs.GetBool(Prefs + prefsKey + ".Passed", true);
            showExplanations = EditorPrefs.GetBool(Prefs + prefsKey + ".Explain", true);
            this.prefsKey = prefsKey;
        }

        private readonly string prefsKey;

        private static void Styles()
        {
            if (okStyle != null)
                return;
            okStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.35f, 0.8f, 0.4f) }, alignment = TextAnchor.MiddleCenter };
            missingStyle = new GUIStyle(okStyle) { normal = { textColor = new Color(0.95f, 0.4f, 0.35f) } };
            wrongStyle = new GUIStyle(okStyle) { normal = { textColor = new Color(0.98f, 0.72f, 0.25f) } };
            noteStyle = new GUIStyle(okStyle) { normal = { textColor = new Color(0.55f, 0.7f, 0.95f) } };
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            mutedStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
            detailStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };
            badgeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        }

        // ------------------------------------------------------------------ summary

        /// <summary>Counts of what is missing, by level (problems only, not notes).</summary>
        public static (int required, int recommended, int optional, int ok, int total) Count(IEnumerable<SetupLine> lines)
        {
            int req = 0, rec = 0, opt = 0, ok = 0, total = 0;
            foreach (SetupLine l in lines)
            {
                if (l.result.status == SetupStatus.Note || l.result.status == SetupStatus.NotApplicable)
                    continue;
                total++;
                if (!l.IsProblem)
                {
                    ok++;
                    continue;
                }
                switch (l.requirement.Level)
                {
                    case SetupLevel.Required: req++; break;
                    case SetupLevel.Recommended: rec++; break;
                    default: opt++; break;
                }
            }
            return (req, rec, opt, ok, total);
        }

        /// <summary>One sentence: "Ready", "2 required and 1 recommended missing"...</summary>
        public static string SummaryText(IEnumerable<SetupLine> lines)
        {
            var c = Count(lines);
            if (c.required == 0 && c.recommended == 0)
                return c.optional == 0 ? "Ready: everything is set up." : $"Ready. {c.optional} optional item(s) not used.";
            var parts = new List<string>();
            if (c.required > 0) parts.Add($"{c.required} required");
            if (c.recommended > 0) parts.Add($"{c.recommended} recommended");
            if (c.optional > 0) parts.Add($"{c.optional} optional");
            return string.Join(", ", parts) + " item(s) to fix.";
        }

        public static MessageType SummaryType(IEnumerable<SetupLine> lines)
        {
            var c = Count(lines);
            return c.required > 0 ? MessageType.Error : c.recommended > 0 ? MessageType.Warning : MessageType.Info;
        }

        /// <summary>The summary box, progress bar and the Fix All buttons.</summary>
        public void DrawSummary(SetupProfile profile, SetupContext context, List<SetupLine> lines)
        {
            Styles();
            var c = Count(lines);
            EditorGUILayout.HelpBox(SummaryText(lines), SummaryType(lines));
            Rect bar = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(bar, c.total == 0 ? 1f : (float)c.ok / c.total, $"{c.ok} / {c.total} set up");

            int fixRequired = lines.Count(l => l.IsProblem && l.requirement.CanFix && l.requirement.InFixAll && l.requirement.Level == SetupLevel.Required);
            int fixRecommended = lines.Count(l => l.IsProblem && l.requirement.CanFix && l.requirement.InFixAll && l.requirement.Level <= SetupLevel.Recommended);
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUI.DisabledScope(fixRequired == 0))
                    if (GUILayout.Button(new GUIContent($"Fix All Required ({fixRequired})", "Applies every automatic fix of the missing REQUIRED items."), GUILayout.Height(24f)))
                        Defer(() => FixAll(profile, context, lines, SetupLevel.Required));
                using (new EditorGUI.DisabledScope(fixRecommended == 0))
                    if (GUILayout.Button(new GUIContent($"Fix Required + Recommended ({fixRecommended})", "Applies every automatic fix of the missing required and recommended items (optional ones are left to you)."), GUILayout.Height(24f)))
                        Defer(() => FixAll(profile, context, lines, SetupLevel.Recommended));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool passed = GUILayout.Toggle(showPassed, new GUIContent(" Show what is already set up", "Off = only the items that need attention."), GUILayout.ExpandWidth(false));
                GUILayout.Space(12f);
                bool explain = GUILayout.Toggle(showExplanations, new GUIContent(" Show explanations", "What each item is for and how to set it up by hand."), GUILayout.ExpandWidth(false));
                if (passed != showPassed || explain != showExplanations)
                {
                    showPassed = passed;
                    showExplanations = explain;
                    EditorPrefs.SetBool(Prefs + prefsKey + ".Passed", showPassed);
                    EditorPrefs.SetBool(Prefs + prefsKey + ".Explain", showExplanations);
                }
            }
        }

        private void FixAll(SetupProfile profile, SetupContext context, List<SetupLine> lines, SetupLevel level)
        {
            int n = SetupRunner.FixAll(profile, context, lines, level);
            Debug.Log($"[Setup] {profile.Name} '{context.Label}': applied {n} fix(es).");
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ requirement list

        /// <summary>Every requirement, grouped, with its status and buttons.</summary>
        public void DrawLines(SetupContext context, List<SetupLine> lines)
        {
            Styles();
            foreach (IGrouping<string, SetupLine> group in lines.GroupBy(l => l.requirement.Group ?? ""))
            {
                List<SetupLine> shown = group.Where(l => showPassed || l.IsProblem || l.result.status == SetupStatus.Note).ToList();
                if (shown.Count == 0)
                    continue;
                int problems = group.Count(l => l.IsProblem);
                int required = group.Count(l => l.IsProblem && l.requirement.Level == SetupLevel.Required);
                string key = group.Key;
                if (!folded.TryGetValue(key, out bool isFolded))
                    isFolded = false;
                string state = problems == 0 ? "✔" : required > 0 ? $"✖ {problems}" : $"! {problems}";
                bool open = EditorGUILayout.Foldout(!isFolded, $"{(string.IsNullOrEmpty(key) ? "General" : key)}   {state}", true, EditorStyles.foldoutHeader);
                folded[key] = !open;
                if (!open)
                    continue;
                foreach (SetupLine line in shown)
                    DrawLine(context, line);
                EditorGUILayout.Space(4f);
            }
        }

        private void DrawLine(SetupContext context, SetupLine line)
        {
            SetupRequirement r = line.requirement;
            SetupResult res = line.result;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(Icon(res.status, r.Level), StyleOf(res.status, r.Level), GUILayout.Width(18f));
                    GUILayout.Label(r.Title, titleStyle, GUILayout.MinWidth(100f));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(LevelText(r.Level), badgeStyle, GUILayout.Width(84f));
                    using (new EditorGUI.DisabledScope(res.target == null))
                        if (GUILayout.Button(new GUIContent("Select", "Selects the object this is about."), EditorStyles.miniButton, GUILayout.Width(48f)))
                        {
                            Selection.activeObject = res.target;
                            EditorGUIUtility.PingObject(res.target);
                        }
                    if (res.IsProblem && r.CanFix)
                    {
                        using (new EditorGUI.DisabledScope(Application.isPlaying))
                            if (GUILayout.Button(new GUIContent(Shorten(r.FixLabel ?? "Fix", 26), r.FixLabel), EditorStyles.miniButton, GUILayout.Width(150f)))
                                Defer(() =>
                                {
                                    if (SetupRunner.ApplyFix(r, context))
                                        Debug.Log($"[Setup] {r.Title}: {r.FixLabel} done on '{context.Label}'.");
                                    Changed?.Invoke();
                                });
                    }
                }
                if (!string.IsNullOrEmpty(res.detail))
                    GUILayout.Label(Colored(res.detail, res.status, r.Level), detailStyle);
                if (showExplanations)
                {
                    if (!string.IsNullOrEmpty(r.Why))
                        GUILayout.Label("<b>Why:</b> " + r.Why, mutedStyle);
                    if (!string.IsNullOrEmpty(r.How) && (res.IsProblem || res.status == SetupStatus.Note))
                        GUILayout.Label("<b>By hand:</b> " + r.How, mutedStyle);
                }
            }
        }

        private static void Defer(Action action)
        {
            // Fixes may open dialogs, change the hierarchy or load prefabs: never inside the GUI pass.
            EditorApplication.delayCall += () => action();
        }

        // ------------------------------------------------------------------ looks

        public static string Icon(SetupStatus status, SetupLevel level)
        {
            switch (status)
            {
                case SetupStatus.Ok: return "✔";
                case SetupStatus.Wrong: return "!";
                case SetupStatus.Note: return "i";
                case SetupStatus.NotApplicable: return "–";
                default: return level == SetupLevel.Optional ? "○" : "✖";
            }
        }

        private static GUIStyle StyleOf(SetupStatus status, SetupLevel level)
        {
            switch (status)
            {
                case SetupStatus.Ok: return okStyle;
                case SetupStatus.Wrong: return wrongStyle;
                case SetupStatus.Note:
                case SetupStatus.NotApplicable: return noteStyle;
                default: return level == SetupLevel.Required ? missingStyle : level == SetupLevel.Recommended ? wrongStyle : noteStyle;
            }
        }

        private static string Colored(string text, SetupStatus status, SetupLevel level)
        {
            string color = status == SetupStatus.Ok ? "#7fcf85" : status == SetupStatus.Note || status == SetupStatus.NotApplicable ? "#9db7e8"
                : status == SetupStatus.Wrong || level != SetupLevel.Required ? "#f0c060" : "#f07a6a";
            return $"<color={color}>{Escape(text)}</color>";
        }

        /// <summary>Text shown in a rich-text label as it is (angle brackets would be read as tags).</summary>
        private static string Escape(string text) => text.Replace("<", "‹").Replace(">", "›");

        public static string LevelText(SetupLevel level) => level == SetupLevel.Required ? "REQUIRED" : level == SetupLevel.Recommended ? "recommended" : "optional";

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";
    }
}
#endif
