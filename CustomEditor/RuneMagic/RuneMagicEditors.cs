#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuneMagic.EditorTools
{
    /// <summary>Tools ▸ SimpleMovements ▸ Rune Magic, and the Project Setup entry that creates the database.</summary>
    public static class RuneMagicMenus
    {
        private const string Menu = SMMenu.Root + "Rune Magic/";
        private const int Section = SMMenu.Agriculture + 1;

        [MenuItem(SMMenu.Root + "Project Setup/Create Rune Magic Database (Resources)", false, SMMenu.ProjectSetup + 13)]
        public static RuneMagicDatabase CreateDatabaseMenu() => ProjectDatabase(true);

        [MenuItem(Menu + "Select Rune Magic Database", false, Section)]
        private static void SelectMenu() => ProjectDatabase(true);

        [MenuItem(Menu + "Validate Rune Magic Database", false, Section + 1)]
        private static void ValidateMenu()
        {
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            db.MarkChanged();
            var errors = new List<string>();
            var warnings = new List<string>();
            db.Validate(errors, warnings);
            foreach (string e in errors) Debug.LogError("[Rune Magic] " + e, db);
            foreach (string w in warnings) Debug.LogWarning("[Rune Magic] " + w, db);
            EditorUtility.DisplayDialog("Rune Magic", errors.Count == 0 && warnings.Count == 0
                ? "The Rune Magic content has no problems."
                : $"{errors.Count} error(s) and {warnings.Count} warning(s); see the Console.", "OK");
        }

        [MenuItem(Menu + "Log Content Summary", false, Section + 2)]
        private static void SummaryMenu() => Debug.Log(Summary(RuneMagicDatabase.Instance.Catalog, true), RuneMagicDatabase.Instance);

        [MenuItem(Menu + "Balance Report", false, Section + 3)]
        private static void BalanceMenu()
        {
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            Debug.Log("[Rune Magic] Balance report of the current tables (the pace of the whole loop)\n" + BalanceReport.ToText(BalanceReport.Build(db.Catalog)), db);
            EditorUtility.DisplayDialog("Rune Magic", "The balance report is in the Console.", "OK");
        }

        [MenuItem(Menu + "Spell Simulator", false, Section + 4)]
        private static void SimulatorMenu() => RunSimulator(0f);

        [MenuItem(Menu + "Spell Simulator (fully charged)", false, Section + 5)]
        private static void SimulatorChargedMenu() => RunSimulator(1f);

        [MenuItem(Menu + "Log Grimoire Audit (Play Mode)", false, Section + 6)]
        private static void GrimoireAuditMenu()
        {
            var sb = new StringBuilder();
            foreach (GrimoireData book in GrimoireStore.Ledger.All) sb.Append(GrimoireAudit.Text(book, 100)).Append('\n');
            Debug.Log(sb.Length > 0 ? "[Rune Magic] " + sb : "[Rune Magic] No grimoire in this world yet (the ledger is kept where the rules run).");
        }

        [MenuItem(Menu + "Log Grimoire Audit (Play Mode)", true)]
        private static bool GrimoireAuditValidate() => Application.isPlaying;

        private static void RunSimulator(float charge)
        {
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            SimulationReport report = SpellSimulator.Run(db.Catalog, new SimulationOptions { samples = 3000, seed = (int)(System.DateTime.UtcNow.Ticks % 100000), charge = charge });
            Debug.Log("[Rune Magic] " + report.ToText(), db);
            EditorUtility.DisplayDialog("Rune Magic", $"{report.builds.Count} random spells were built; {report.outliers.Count} outlier(s) stand out. The report is in the Console.", "OK");
        }

        /// <summary>The project's database, created in Resources/SimpleMovements/Settings with the built-in content when missing.</summary>
        public static RuneMagicDatabase ProjectDatabase(bool select)
        {
            RuneMagicDatabase db = SMAssetsEditor.FindOrCreate<RuneMagicDatabase>(SMAssets.Settings, RuneMagicDatabase.ResourcesName, d => d.FillDefaults(), select);
            RuneMagicDatabase.Override(null);
            return db;
        }

        /// <summary>Counts of the content (and, with <paramref name="detailed"/>, every rune with its concepts).</summary>
        public static string Summary(RuneCatalog c, bool detailed)
        {
            var sb = new StringBuilder();
            sb.Append($"Rune Magic content {c.ContentHash}: {c.Aspects.Count} aspects, {c.Alignments.Count} alignments, {c.Runes.Count} runes, " +
                      $"{c.ConceptCount} concepts, {c.Intentions.Count} intentions, {c.Effects.Count} desired effects, {c.Rules.Count} rules, " +
                      $"{c.Archetypes.Count} synthesis archetypes, {c.Flaws.Count} flaws; " +
                      $"{c.Errors.Count} error(s), {c.Warnings.Count} warning(s).");
            if (!detailed)
                return sb.ToString();
            sb.Append("\nRune users: ").Append(c.Settings.enabled ? string.Join(", ", c.Settings.runeUserClassNames) : "(Rune Magic disabled)");
            foreach (RuneDef r in c.Runes)
            {
                sb.Append($"\n• {c.RuneName(r, r.minRank)} [{r.id}] ({r.rarity}, {r.slotCost} slot{(r.slotCost == 1 ? "" : "s")}): ");
                var names = new List<string>();
                foreach (ConceptDef k in r.concepts) if (k != null) names.Add(k.Name);
                sb.Append(string.Join(", ", names));
            }
            return sb.ToString();
        }
    }

    [CustomEditor(typeof(RuneMagicDatabase))]
    public class RuneMagicDatabaseEditor : Editor
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private int validatedVersion = -1;
        private bool showAllProblems;

        public override void OnInspectorGUI()
        {
            var db = (RuneMagicDatabase)target;
            if (validatedVersion != db.Version)
            {
                errors.Clear();
                warnings.Clear();
                db.Validate(errors, warnings);
                validatedVersion = db.Version;
            }

            EditorGUILayout.HelpBox(RuneMagicMenus.Summary(db.Catalog, false) +
                "\n\nRune users (Mage class by default) use runes instead of absorbing abilities; every other class is unchanged. " +
                "Content packs go in Extensions.", MessageType.Info);
            DrawProblems(errors, MessageType.Error);
            DrawProblems(warnings, MessageType.Warning);
            if (errors.Count + warnings.Count > 8)
                showAllProblems = EditorGUILayout.Toggle("Show All Problems", showAllProblems);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate"))
                {
                    db.MarkChanged();
                    validatedVersion = -1;
                }
                if (GUILayout.Button("Log Summary"))
                    Debug.Log(RuneMagicMenus.Summary(db.Catalog, true), db);
                if (GUILayout.Button("Reset to Built-in Content") &&
                    EditorUtility.DisplayDialog("Rune Magic", "Replace this database's content and settings with the built-in ones? " +
                        "The class lists and extensions are kept.", "Replace", "Cancel"))
                {
                    Undo.RecordObject(db, "Reset Rune Magic Content");
                    db.FillDefaults();
                    EditorUtility.SetDirty(db);
                }
            }
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
                db.MarkChanged();
        }

        private void DrawProblems(List<string> list, MessageType type)
        {
            int shown = showAllProblems ? list.Count : Mathf.Min(list.Count, 4);
            for (int i = 0; i < shown; i++)
                EditorGUILayout.HelpBox(list[i], type);
            if (shown < list.Count)
                EditorGUILayout.HelpBox($"… and {list.Count - shown} more ({type}).", type);
        }
    }
}
#endif
