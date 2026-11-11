using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The Rune Magic System's content and settings: runes, alignments, concepts, intentions, desired effects,
    /// compatibility rules, the balancing tables and who uses Rune Magic. Optional: without one in
    /// Resources/SimpleMovements/Settings/RuneMagicDatabase the built-in content (<see cref="RuneDefaults"/>) is used.
    /// Create it with Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Rune Magic Database (Resources).
    /// <para>Content packs: put further databases in <see cref="extensions"/>; their content is added (same ids replace the
    /// base entries; their concept additions extend existing runes), so new runes, concepts, intentions and effects need
    /// no change to this asset or the code. Settings and balance always come from this database.</para>
    /// <para>In a network session the server's database decides; clients compare <see cref="RuneCatalog.ContentHash"/>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = ResourcesName, menuName = "SimpleMovements/Rune Magic/Rune Magic Database", order = 0)]
    public class RuneMagicDatabase : ScriptableObject
    {
        public const string ResourcesName = "RuneMagicDatabase";

        [Tooltip("Shown in the inspector only.")]
        [TextArea(1, 3)] public string notes = "";

        [Header("Who uses Rune Magic")]
        [Tooltip("Class assets whose players use Rune Magic, in addition to the class names in Settings ▸ Rune User Class Names.")]
        public List<PlayerClass> runeUserClasses = new List<PlayerClass>();
        [Tooltip("Class archetypes whose characters use Rune Magic (e.g. the Mage archetype preset).")]
        public List<CharacterArchetype> runeUserArchetypes = new List<CharacterArchetype>();

        [Header("Content and settings")]
        public RuneMagicData data = new RuneMagicData();

        [Header("Spell forms (design spec 16.4)")]
        [Tooltip("Ability assets used as the executable of a spell form instead of the form's built-in preset (a form is picked by its id, " +
                 "e.g. 'bolt', 'wall', 'ritual'). The template's mana, stamina and health costs are ignored: a spell costs Rune Energy. " +
                 "Give the 'ritual' form one to make the effects without a combat equivalent (reveal, grow, communicate...) do something.")]
        public List<SpellFormTemplate> spellFormTemplates = new List<SpellFormTemplate>();

        [Tooltip("More databases whose content is added to this one (content packs). Same ids replace this one's entries.")]
        public List<RuneMagicDatabase> extensions = new List<RuneMagicDatabase>();

        [NonSerialized] private RuneCatalog compiled;
        [NonSerialized] private int compiledVersion = -1;
        [NonSerialized] private int version;

        /// <summary>The compiled catalog changed (a database was edited or replaced).</summary>
        public static event Action CatalogChanged;

        // ------------------------------------------------------------------ instance
        private static RuneMagicDatabase instance;
        private static bool searched;

        /// <summary>The project's database (Resources/SimpleMovements/Settings/RuneMagicDatabase), or the built-in content.</summary>
        public static RuneMagicDatabase Instance
        {
            get
            {
                if (instance == null)
                {
                    if (!searched)
                    {
                        searched = true;
                        instance = SMAssets.Load<RuneMagicDatabase>(SMAssets.Settings, ResourcesName);
                    }
                    if (instance == null)
                    {
                        instance = CreateInstance<RuneMagicDatabase>();
                        instance.name = ResourcesName + " (built-in)";
                        instance.hideFlags = HideFlags.DontSave;
                        instance.data = RuneDefaults.Create();
                    }
                }
                return instance;
            }
        }

        /// <summary>Uses <paramref name="database"/> instead of the project's (tests, tools). Null = look it up again.</summary>
        public static void Override(RuneMagicDatabase database)
        {
            instance = database;
            searched = database != null;
            CatalogChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            searched = false;
            CatalogChanged = null;
        }

        // ------------------------------------------------------------------ catalog
        /// <summary>Changes whenever the content changes (inspector edits, <see cref="MarkChanged"/>).</summary>
        public int Version => version;

        /// <summary>This database's content with its extensions, compiled (recompiled after a change).</summary>
        public RuneCatalog Catalog
        {
            get
            {
                if (compiled == null || compiledVersion != version)
                {
                    compiled = RuneCatalog.Compile(data, ExtensionData());
                    compiledVersion = version;
                }
                return compiled;
            }
        }

        /// <summary>Call after changing the content from code (the inspector does it itself).</summary>
        public void MarkChanged()
        {
            version++;
            CatalogChanged?.Invoke();
        }

        /// <summary>Replaces the content and settings with the built-in ones.</summary>
        public void FillDefaults()
        {
            data = RuneDefaults.Create();
            MarkChanged();
        }

        private void OnValidate() => MarkChanged();

        // An asset of an older format is brought up to date when it loads (and saved so in the editor).
        private void OnEnable()
        {
            if (data == null || !data.Upgrade())
                return;
            MarkChanged();
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.EditorUtility.IsPersistent(this))
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>The extensions' data, each database once (an extension listing this one is ignored).</summary>
        private List<RuneMagicData> ExtensionData()
        {
            var result = new List<RuneMagicData>();
            var visited = new HashSet<RuneMagicDatabase> { this };
            void Add(RuneMagicDatabase db)
            {
                if (db == null || !visited.Add(db))
                    return;
                if (db.data != null)
                    result.Add(db.data);
                foreach (RuneMagicDatabase nested in db.extensions)
                    Add(nested);
            }
            foreach (RuneMagicDatabase ext in extensions)
                Add(ext);
            return result;
        }

        // ------------------------------------------------------------------ spell form templates
        /// <summary>The ability assigned to the spell form <paramref name="formId"/> on this database or one of its extensions (null = use the form's preset).</summary>
        public AbilityDefinition TemplateFor(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return null;
            AbilityDefinition own = TemplateIn(this, formId);
            if (own != null) return own;
            var visited = new HashSet<RuneMagicDatabase> { this };
            foreach (RuneMagicDatabase ext in extensions)
            {
                AbilityDefinition found = TemplateInTree(ext, formId, visited);
                if (found != null) return found;
            }
            return null;
        }

        private static AbilityDefinition TemplateIn(RuneMagicDatabase db, string formId)
        {
            if (db.spellFormTemplates == null) return null;
            foreach (SpellFormTemplate t in db.spellFormTemplates)
                if (t != null && t.ability != null && t.formId == formId) return t.ability;
            return null;
        }

        private static AbilityDefinition TemplateInTree(RuneMagicDatabase db, string formId, HashSet<RuneMagicDatabase> visited)
        {
            if (db == null || !visited.Add(db)) return null;
            AbilityDefinition found = TemplateIn(db, formId);
            if (found != null) return found;
            foreach (RuneMagicDatabase ext in db.extensions)
            {
                found = TemplateInTree(ext, formId, visited);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------ rune users
        public bool ListsClass(PlayerClass playerClass)
        {
            if (playerClass == null) return false;
            foreach (PlayerClass c in runeUserClasses)
                if (c == playerClass) return true;
            return false;
        }

        public bool ListsArchetype(CharacterArchetype archetype)
        {
            if (archetype == null) return false;
            foreach (CharacterArchetype a in runeUserArchetypes)
                if (a == archetype) return true;
            return false;
        }

        // ------------------------------------------------------------------ validation
        /// <summary>Setup problems: the catalog's, plus the asset's own lists.</summary>
        public void Validate(List<string> errors, List<string> warnings)
        {
            RuneCatalog c = Catalog;
            errors.AddRange(c.Errors);
            warnings.AddRange(c.Warnings);
            if (runeUserClasses.Exists(x => x == null)) warnings.Add("Rune User Classes has empty entries.");
            if (runeUserArchetypes.Exists(x => x == null)) warnings.Add("Rune User Archetypes has empty entries.");
            foreach (CharacterArchetype a in runeUserArchetypes)
                if (a != null && a.kind != ArchetypeKind.Class)
                    warnings.Add($"Rune User Archetypes: '{a.Name}' is a {a.kind}, not a Class archetype (only Class archetypes are matched).");
            foreach (SpellFormTemplate t in spellFormTemplates)
            {
                if (t == null) continue;
                if (string.IsNullOrEmpty(t.formId) || c.SpellForm(t.formId) == null)
                    warnings.Add($"Spell Form Templates: '{t.formId}' is not the id of a spell form.");
                else if (t.ability == null)
                    warnings.Add($"Spell Form Templates: the template of '{t.formId}' has no ability.");
            }
            foreach (SpellFormDef f in c.SpellForms)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.preset) || TemplateFor(f.id) != null) continue;
                bool found = false;
                foreach (AbilityPresets.Preset p in AbilityPresets.All)
                    if (string.Equals(p.name, f.preset, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                if (!found)
                    warnings.Add($"Spell Forms: '{f.id}' uses the ability preset '{f.preset}', which does not exist: its spells do nothing until you fix the name or assign a template ability.");
            }
            if (extensions.Contains(this)) warnings.Add("The database lists itself as an extension (ignored).");
            if (extensions.Exists(x => x == null)) warnings.Add("Extensions has empty entries.");
            if (c.Settings.enabled && runeUserClasses.Count == 0 && runeUserArchetypes.Count == 0 &&
                !c.Settings.runeUserClassNames.Exists(n => !string.IsNullOrWhiteSpace(n)))
                errors.Add("Rune Magic is enabled but no class can use it (no class names, classes or archetypes).");
        }
    }

    /// <summary>An ability asset used as the executable of one spell form (see <see cref="RuneMagicDatabase.spellFormTemplates"/>).</summary>
    [Serializable]
    public class SpellFormTemplate
    {
        [Tooltip("The id of the spell form ('bolt', 'blast', 'wall', 'ritual'...).")]
        public string formId = "";
        public AbilityDefinition ability;
    }
}
