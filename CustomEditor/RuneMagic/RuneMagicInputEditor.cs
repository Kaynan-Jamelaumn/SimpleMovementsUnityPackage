#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using SimpleMovements.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneMagic.EditorTools
{
    /// <summary>
    /// The Rune Magic input actions in the project's Input Actions assets: Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune
    /// Magic Input Actions adds the missing ones (no key: bind them in the Input Actions editor), and the Project Validator
    /// (Game Setup) reports missing or unbound ones. Keys are never chosen in code.
    /// </summary>
    [InitializeOnLoad]
    public static class RuneMagicInputEditor
    {
        private const string Menu = SMMenu.Root + "Rune Magic/";
        private const int Section = SMMenu.Agriculture + 1;

        static RuneMagicInputEditor()
        {
            ProjectValidator.ValidateProject -= OnValidateProject;
            ProjectValidator.ValidateProject += OnValidateProject;
        }

        /// <summary>The action names Rune Magic reads (Rune Magic Database ▸ Settings).</summary>
        public static List<string> ActionNames() => new List<string>(RuneMagicDatabase.Instance.Catalog.Settings.InputActionNames());

        [MenuItem(Menu + "Add Rune Magic Input Actions", false, Section + 21)]
        private static void AddMenu()
        {
            List<string> assets = SetupChecks.InputAssetPaths();
            if (assets.Count == 0)
            {
                EditorUtility.DisplayDialog("Rune Magic", "No Input Actions asset with a Player map was found under Assets/.", "OK");
                return;
            }
            var added = new List<string>();
            foreach (string path in assets)
                foreach (string name in Missing(path))
                    if (SetupChecks.AddAction(path, name, null))
                        added.Add($"{name} ({Path.GetFileName(path)})");
            List<string> unbound = Unbound();
            string message = added.Count > 0 ? "Added without a key:\n• " + string.Join("\n• ", added) : "Every Rune Magic action is already in your Input Actions asset.";
            if (unbound.Count > 0)
                message += "\n\nBind these in the Input Actions editor (double-click the asset); players can rebind them in Key Bindings:\n• " + string.Join("\n• ", unbound);
            EditorUtility.DisplayDialog("Rune Magic Input Actions", message, "OK");
        }

        [MenuItem(Menu + "Toggle Debug Panel (Play Mode)", false, Section + 22)]
        private static void ToggleDebugMenu() => RuneMagicDebugPanel.Toggle();

        [MenuItem(Menu + "Toggle Debug Panel (Play Mode)", true)]
        private static bool ToggleDebugValidate() => Application.isPlaying;

        [MenuItem(Menu + "Toggle Rune Soul Window (Play Mode)", false, Section + 23)]
        private static void ToggleWindowMenu() => RuneSoulWindow.Toggle();

        [MenuItem(Menu + "Toggle Rune Soul Window (Play Mode)", true)]
        private static bool ToggleWindowValidate() => Application.isPlaying;

        [MenuItem(Menu + "Toggle Synthesis Window (Play Mode)", false, Section + 24)]
        private static void ToggleSynthesisMenu() => SynthesisWindow.Toggle();

        [MenuItem(Menu + "Toggle Synthesis Window (Play Mode)", true)]
        private static bool ToggleSynthesisValidate() => Application.isPlaying;

        [MenuItem(Menu + "Toggle Grimoire Window (Play Mode)", false, Section + 25)]
        private static void ToggleGrimoireMenu() => GrimoireWindow.Toggle();

        [MenuItem(Menu + "Toggle Grimoire Window (Play Mode)", true)]
        private static bool ToggleGrimoireValidate() => Application.isPlaying;

        /// <summary>The Rune Magic actions missing from the asset at <paramref name="path"/>.</summary>
        public static List<string> Missing(string path)
        {
            var missing = new List<string>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null) return missing;
            foreach (string name in ActionNames())
                if (asset.FindAction(name, false) == null && !missing.Contains(name))
                    missing.Add(name);
            return missing;
        }

        /// <summary>"Meditate (PlayerInput.inputactions)" for every Rune Magic action that exists but has no key.</summary>
        public static List<string> Unbound()
        {
            var list = new List<string>();
            foreach (string path in SetupChecks.InputAssetPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset == null) continue;
                foreach (string name in ActionNames())
                {
                    InputAction a = asset.FindAction(name, false);
                    if (a != null && !HasBinding(a)) list.Add($"{name} ({Path.GetFileName(path)})");
                }
            }
            return list;
        }

        private static bool HasBinding(InputAction a)
        {
            foreach (InputBinding b in a.bindings)
                if (!string.IsNullOrEmpty(b.path)) return true;
            return false;
        }

        private static void OnValidateProject(ValidationChecks checks, List<ValidationIssue> issues)
        {
            if ((checks & ValidationChecks.GameSetup) == 0)
                return;
            foreach (string path in SetupChecks.InputAssetPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset == null) continue;
                foreach (string name in ActionNames())
                {
                    InputAction a = asset.FindAction(name, false);
                    if (a == null)
                    {
                        string captured = name, at = path;
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, "Input",
                                $"Rune Magic: '{name}' is missing from {Path.GetFileName(path)} (Mages cannot use that input). It is added without a key: bind it in the Input Actions editor.",
                                asset, path)
                            .WithFix("Add", () => SetupChecks.AddAction(at, captured, null)));
                    }
                    else if (!HasBinding(a))
                        issues.Add(new ValidationIssue(IssueSeverity.Info, "Input",
                            $"Rune Magic: '{name}' in {Path.GetFileName(path)} has no key yet: bind it in the Input Actions editor (players can rebind it in Key Bindings).", asset, path));
                }
            }
        }
    }
}
#endif
