#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleMovements.Validation
{
    /// <summary>
    /// Window ▸ SimpleMovements ▸ Project Validator: runs the validators on the selection, the open scenes or every
    /// prefab, lists the issues (errors first) with a button to select each object and one to fix it, and gives access
    /// to the auto-assigner and to the package's other validators (players, abilities, traits, NPCs, portals, hub world,
    /// network).
    /// </summary>
    public class ValidationWindow : EditorWindow
    {
        private const string PrefsPrefix = "SimpleMovements.Validation.";

        private static readonly (string label, string menu)[] OtherValidators =
        {
            ("Players", "Tools/SimpleMovements/Validate/Players"),
            ("Abilities", "Tools/SimpleMovements/Validate/Abilities"),
            ("Traits", "Tools/SimpleMovements/Validate/Traits"),
            ("NPCs in scene", "Tools/SimpleMovements/Validate/NPCs In Scene"),
            ("Dungeon portals", "Tools/SimpleMovements/Validate/Dungeon Portals"),
            ("Hub world", "Tools/SimpleMovements/Validate/Hub World"),
            ("Network setup", "Tools/SimpleMovements/Validate/Network Setup"),
            ("Input actions", "Tools/SimpleMovements/Validate/Input Actions"),
            ("Scene setup", "Tools/SimpleMovements/Validate/Scene Setup (open scenes)"),
        };

        private ValidationScope scope = ValidationScope.OpenScenes;
        private ValidationChecks checks = ProjectValidator.DefaultChecks;
        private bool showErrors = true, showWarnings = true, showInfo = true;
        private string search = "";
        private Vector2 scroll;
        private bool showTools;
        private List<ValidationIssue> issues = new List<ValidationIssue>();
        private bool hasRun;
        private string status = "";

        [MenuItem("Window/SimpleMovements/Project Validator")]
        [MenuItem(SMMenu.Root + "Project Validator...", false, SMMenu.Top + 0)]
        public static void ShowWindow() => GetWindow<ValidationWindow>("Project Validator").Show();

        /// <summary>Everything at once: the open scenes, every prefab, the project's assets and the game setup.</summary>
        [MenuItem(SMMenu.Root + "Check Everything...", false, SMMenu.Top + 1)]
        private static void CheckEverything() => Open(ValidationScope.Everything, ProjectValidator.AllChecks);

        /// <summary>Opens the window and runs <paramref name="runScope"/> right away.</summary>
        public static void Open(ValidationScope runScope, ValidationChecks? runChecks = null)
        {
            var window = GetWindow<ValidationWindow>("Project Validator");
            window.scope = runScope;
            if (runChecks.HasValue)
                window.checks = runChecks.Value;
            window.Show();
            window.RunNow();
        }

        private void OnEnable()
        {
            scope = (ValidationScope)EditorPrefs.GetInt(PrefsPrefix + "Scope", (int)ValidationScope.OpenScenes);
            checks = (ValidationChecks)EditorPrefs.GetInt(PrefsPrefix + "Checks", (int)ProjectValidator.DefaultChecks);
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt(PrefsPrefix + "Scope", (int)scope);
            EditorPrefs.SetInt(PrefsPrefix + "Checks", (int)checks);
        }

        private void RunNow()
        {
            issues = ProjectValidator.Run(scope, checks);
            hasRun = true;
            status = issues.Count == 0 ? "No problems found." : $"{issues.Count} issue(s).";
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawFilters();
            DrawIssues();
            DrawTools();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            scope = (ValidationScope)EditorGUILayout.EnumPopup(scope, EditorStyles.toolbarPopup, GUILayout.Width(110));
            checks = (ValidationChecks)EditorGUILayout.EnumFlagsField(checks, EditorStyles.toolbarPopup, GUILayout.Width(170));
            if (GUILayout.Button(new GUIContent("Run", "Validate the chosen scope."), EditorStyles.toolbarButton, GUILayout.Width(50)))
                EditorApplication.delayCall += RunNow;
            GUILayout.FlexibleSpace();
            int fixable = issues.Count(i => i.CanFix && Visible(i));
            using (new EditorGUI.DisabledScope(fixable == 0))
            {
                if (GUILayout.Button(new GUIContent($"Fix All Shown ({fixable})", "Apply every available fix of the issues shown."), EditorStyles.toolbarButton))
                {
                    List<ValidationIssue> shown = issues.Where(i => i.CanFix && Visible(i)).ToList();
                    EditorApplication.delayCall += () => FixAll(shown);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void FixAll(List<ValidationIssue> shown)
        {
            if (!EditorUtility.DisplayDialog("Fix all", $"Apply {shown.Count} fix(es)?\n\nScene changes can be undone; prefab changes are saved to the assets.", "Fix", "Cancel"))
                return;
            int count = ProjectValidator.FixAll(shown);
            issues.RemoveAll(i => shown.Contains(i) && !i.CanFix);
            status = $"Fixed {count} of {shown.Count}. Run again to refresh the list.";
            Repaint();
        }

        private void DrawFilters()
        {
            int errors = issues.Count(i => i.severity == IssueSeverity.Error);
            int warnings = issues.Count(i => i.severity == IssueSeverity.Warning);
            int infos = issues.Count - errors - warnings;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            showErrors = GUILayout.Toggle(showErrors, new GUIContent(errors.ToString(), Icon(IssueSeverity.Error)), EditorStyles.toolbarButton, GUILayout.Width(50));
            showWarnings = GUILayout.Toggle(showWarnings, new GUIContent(warnings.ToString(), Icon(IssueSeverity.Warning)), EditorStyles.toolbarButton, GUILayout.Width(50));
            showInfo = GUILayout.Toggle(showInfo, new GUIContent(infos.ToString(), Icon(IssueSeverity.Info)), EditorStyles.toolbarButton, GUILayout.Width(50));
            GUILayout.Space(6);
            search = GUILayout.TextField(search ?? "", EditorStyles.toolbarTextField, GUILayout.MinWidth(80));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }

        private void DrawIssues()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!hasRun)
            {
                EditorGUILayout.HelpBox("Choose what to validate and which checks to make, then press Run.\n\n" +
                    "• Missing scripts, broken references and empty [RequiredReference] fields\n" +
                    "• Assignable references: empty fields the auto-assigner can fill (off by default)\n" +
                    "• Network: network prefabs and components (Fish-Net)\n" +
                    "• Project assets: settings and databases in Resources", MessageType.Info);
            }
            ValidationIssue fixedIssue = null;
            foreach (ValidationIssue issue in issues)
            {
                if (!Visible(issue))
                    continue;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(Icon(issue.severity), GUILayout.Width(20), GUILayout.Height(20));
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(issue.category, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(issue.message, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(issue.objectPath))
                    EditorGUILayout.LabelField(issue.objectPath, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical(GUILayout.Width(150));
                using (new EditorGUI.DisabledScope(issue.context == null))
                {
                    if (GUILayout.Button("Select"))
                    {
                        Selection.activeObject = issue.context;
                        EditorGUIUtility.PingObject(issue.context);
                    }
                }
                if (issue.CanFix && GUILayout.Button(new GUIContent(Shorten(issue.fixLabel ?? "Fix", 22), issue.fixLabel)))
                    fixedIssue = issue;
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (fixedIssue != null)
            {
                // Fixes can open dialogs or change the list: run them outside this GUI pass.
                ValidationIssue issue = fixedIssue;
                EditorApplication.delayCall += () =>
                {
                    if (ProjectValidator.TryFix(issue))
                    {
                        issues.Remove(issue);
                        status = "Fixed: " + issue.message;
                    }
                    Repaint();
                };
            }
        }

        private void DrawTools()
        {
            showTools = EditorGUILayout.Foldout(showTools, "Auto-assign and other validators", true);
            if (!showTools)
                return;
            EditorGUILayout.LabelField("Auto-assign empty references", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Selection"))
                ReferenceAutoAssigner.RunOnSelection(AutoAssignOptions.Preview);
            if (GUILayout.Button("Assign Selection"))
                ReferenceAutoAssigner.RunOnSelection(new AutoAssignOptions());
            if (GUILayout.Button("Preview Open Scenes"))
                ReferenceAutoAssigner.RunOnOpenScenes(AutoAssignOptions.Preview).Log("Open scenes (preview)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Other validators (results in the Console or a dialog)", EditorStyles.miniBoldLabel);
            int perRow = Math.Max(1, (int)(position.width / 130f));
            for (int i = 0; i < OtherValidators.Length; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + perRow, OtherValidators.Length); j++)
                {
                    (string label, string menu) = OtherValidators[j];
                    if (GUILayout.Button(label))
                    {
                        string path = menu;
                        EditorApplication.delayCall += () =>
                        {
                            if (!EditorApplication.ExecuteMenuItem(path))
                                Debug.LogWarning("[Validator] Menu not available: " + path);
                        };
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool Visible(ValidationIssue issue)
        {
            switch (issue.severity)
            {
                case IssueSeverity.Error when !showErrors:
                case IssueSeverity.Warning when !showWarnings:
                case IssueSeverity.Info when !showInfo:
                    return false;
            }
            if (string.IsNullOrEmpty(search))
                return true;
            return Contains(issue.message, search) || Contains(issue.category, search) || Contains(issue.objectPath, search);
        }

        private static bool Contains(string text, string part) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        private static Texture Icon(IssueSeverity severity)
        {
            string name = severity == IssueSeverity.Error ? "console.erroricon.sml"
                : severity == IssueSeverity.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
            return EditorGUIUtility.IconContent(name).image;
        }
    }
}
#endif
