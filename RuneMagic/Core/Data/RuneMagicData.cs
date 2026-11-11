using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// All the content and settings of the Rune Magic System as plain data: what the Rune Magic Database asset holds and
    /// what <see cref="RuneCatalog"/> compiles. Being plain (no ScriptableObject) it can be built, merged, compared and
    /// tested anywhere, and saved as JSON.
    /// </summary>
    [Serializable]
    public class RuneMagicData
    {
        /// <summary>
        /// Format version of this data (raise it when a change needs converting older assets; <see cref="Upgrade"/> converts).
        /// 2: soul damage kinds, collapse and recovery settings (Soul Integrity).
        /// 3: rune drop profiles and the acquisition balance (rune drops, energy rewards, absorption, release).
        /// 4: concept discovery (tiers, knowledge sources, riddles).
        /// 5: synthesis engine (outcome archetypes, flaws, the engine's balance).
        /// 6: spellcasting (spell forms, the spellcasting balance).
        /// 7: the Spelling System (its balance and rules).
        /// 8: the Grimoire (its balance).
        /// 9: charging by holding the key (the spells' charge, the forms that can be charged).
        /// </summary>
        public const int CurrentVersion = 9;

        public int version = CurrentVersion;

        [Header("Settings")]
        public RuneMagicSettings settings = new RuneMagicSettings();
        public RuneBalance balance = new RuneBalance();

        [Header("Content")]
        [Tooltip("The vocabulary that describes what magic does.")]
        public List<AspectDef> aspects = new List<AspectDef>();
        [Tooltip("Fundamental magical domains.")]
        public List<AlignmentDef> alignments = new List<AlignmentDef>();
        [Tooltip("Rune definitions, each with its concepts.")]
        public List<RuneDef> runes = new List<RuneDef>();
        [Tooltip("Concepts added to runes defined elsewhere (content packs).")]
        public List<RuneConceptAddition> conceptAdditions = new List<RuneConceptAddition>();
        [Tooltip("General purposes of a synthesis.")]
        public List<IntentionDef> intentions = new List<IntentionDef>();
        [Tooltip("Specific results of a synthesis.")]
        public List<DesiredEffectDef> effects = new List<DesiredEffectDef>();
        [Tooltip("Synergies, conflicts, supports and incoherences.")]
        public List<CompatibilityRule> rules = new List<CompatibilityRule>();
        [Tooltip("Which monsters carry which runes, and the magical energy they give.")]
        public List<RuneDropProfile> dropProfiles = new List<RuneDropProfile>();
        [Tooltip("Kinds of knowledge source found in dungeons (inscriptions, artifacts, sealed chambers...).")]
        public List<DiscoverySourceDef> discoverySources = new List<DiscoverySourceDef>();
        [Tooltip("Riddles written for concepts. Concepts without one get generated riddles.")]
        public List<RiddleDef> riddles = new List<RiddleDef>();
        [Tooltip("Named reference formulas (Pyroclastic Resonance, Gravity Well...): they name a result and refine its score. Not recipes.")]
        public List<OutcomeArchetype> archetypes = new List<OutcomeArchetype>();
        [Tooltip("Ways a flawed synthesis can go wrong.")]
        public List<SynthesisFlawDef> synthesisFlaws = new List<SynthesisFlawDef>();
        [Tooltip("The executable shapes of spells (a projectile, an area, a wall...) and the desired effects each serves.")]
        public List<SpellFormDef> spellForms = new List<SpellFormDef>();
        [Tooltip("Which spells use the Spelling System (key-sequence casting) and how hard it is for them: rules by form, desired effect or archetype.")]
        public List<SpellingRule> spellingRules = new List<SpellingRule>();

        /// <summary>
        /// Brings data written by an older format up to date: what a newer format added is filled with the built-in values
        /// (nothing that was set is changed). Returns true when something changed.
        /// </summary>
        public bool Upgrade()
        {
            if (version >= CurrentVersion)
                return false;
            if (balance == null) balance = new RuneBalance();
            if (version < 2)
            {
                // Version 1 had no soul damage kinds: an empty list would make every soul damage ordinary.
                if (balance.soulDamageKinds == null || balance.soulDamageKinds.Count == 0)
                    balance.soulDamageKinds = RuneDefaults.SoulDamageKinds();
                if (balance.soulCollapse == null) balance.soulCollapse = RuneDefaults.SoulCollapse();
                if (balance.soulRecovery == null) balance.soulRecovery = RuneDefaults.SoulRecovery();
            }
            if (version < 3)
            {
                // Version 2 had no drop profiles: without them no monster would carry runes.
                if (dropProfiles == null || dropProfiles.Count == 0)
                    dropProfiles = RuneDefaults.DropProfiles();
                if (balance.acquisition == null) balance.acquisition = RuneDefaults.Acquisition();
                else if (balance.acquisition.rarityWeights == null || balance.acquisition.rarityWeights.Count == 0)
                    balance.acquisition.rarityWeights = RuneDefaults.RarityWeights();
            }
            if (version < 4)
            {
                if (discoverySources == null || discoverySources.Count == 0) discoverySources = RuneDefaults.DiscoverySources();
                if (riddles == null || riddles.Count == 0) riddles = RuneDefaults.Riddles();
                if (balance.discovery == null || balance.discovery.tiers == null || balance.discovery.tiers.Count == 0)
                    balance.discovery = RuneDefaults.Discovery();
            }
            if (version < 5)
            {
                if (archetypes == null || archetypes.Count == 0) archetypes = RuneDefaults.Archetypes();
                if (synthesisFlaws == null || synthesisFlaws.Count == 0) synthesisFlaws = RuneDefaults.SynthesisFlaws();
                if (balance.synthesisEngine == null || balance.synthesisEngine.powerCurve == null || balance.synthesisEngine.powerCurve.Count == 0)
                    balance.synthesisEngine = RuneDefaults.SynthesisEngine();
            }
            if (version < 6)
            {
                if (spellForms == null || spellForms.Count == 0) spellForms = RuneDefaults.SpellForms();
                if (balance.spells == null) balance.spells = RuneDefaults.Spells();
            }
            if (version < 7)
            {
                if (balance.spelling == null) balance.spelling = RuneDefaults.Spelling();
                if (spellingRules == null || spellingRules.Count == 0) spellingRules = RuneDefaults.SpellingRules();
                if (balance.soulConditions != null)
                    foreach (SoulCondition c in balance.soulConditions)
                        if (c != null && c.spellingExtraLetters == 0 && c.spellingWindowMultiplier == 1f && c.spellingPenaltyMultiplier == 1f)
                            RuneDefaults.SpellingOfCondition(c);
            }
            if (version < 8)
            {
                if (balance.grimoire == null) balance.grimoire = RuneDefaults.Grimoire();
            }
            if (version < 9)
            {
                if (balance.spells != null && balance.spells.charge == null) balance.spells.charge = new ChargeProfile();
                // Older data has no chargeable form: the attacks get it (a designer who turned it on for some form keeps their choice).
                if (spellForms != null && !spellForms.Exists(f => f != null && f.chargeable))
                    foreach (SpellFormDef f in spellForms)
                        if (f != null && System.Array.IndexOf(RuneDefaults.ChargeableForms, f.id) >= 0) f.chargeable = true;
            }
            version = CurrentVersion;
            return true;
        }

        /// <summary>A deep copy (JsonUtility round trip: what is saved is what is copied).</summary>
        public RuneMagicData Clone()
        {
            var copy = new RuneMagicData();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), copy);
            return copy;
        }

        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        public static RuneMagicData FromJson(string json)
        {
            var data = new RuneMagicData();
            if (!string.IsNullOrEmpty(json))
                JsonUtility.FromJsonOverwrite(json, data);
            return data;
        }

        /// <summary>
        /// Adds the content of <paramref name="extension"/> to this data (a content pack): entries with the same id replace
        /// the ones here, new ids are added, concept additions are applied. Settings and balance stay this data's.
        /// Works on copies of the extension's entries.
        /// </summary>
        public void Merge(RuneMagicData extension)
        {
            if (extension == null)
                return;
            RuneMagicData ext = extension.Clone();
            MergeById(aspects, ext.aspects, a => a.id);
            MergeById(alignments, ext.alignments, a => a.id);
            MergeById(runes, ext.runes, r => r.id);
            MergeById(intentions, ext.intentions, i => i.id);
            MergeById(effects, ext.effects, e => e.id);
            MergeById(rules, ext.rules, r => r.id);
            if (ext.discoverySources != null)
            {
                if (discoverySources == null) discoverySources = new List<DiscoverySourceDef>();
                MergeById(discoverySources, ext.discoverySources, s => s.id);
            }
            if (ext.archetypes != null)
            {
                if (archetypes == null) archetypes = new List<OutcomeArchetype>();
                MergeById(archetypes, ext.archetypes, a => a.id);
            }
            if (ext.synthesisFlaws != null)
            {
                if (synthesisFlaws == null) synthesisFlaws = new List<SynthesisFlawDef>();
                MergeById(synthesisFlaws, ext.synthesisFlaws, f => f.id);
            }
            if (ext.spellForms != null)
            {
                if (spellForms == null) spellForms = new List<SpellFormDef>();
                MergeById(spellForms, ext.spellForms, f => f.id);
            }
            if (ext.spellingRules != null)
            {
                if (spellingRules == null) spellingRules = new List<SpellingRule>();
                MergeById(spellingRules, ext.spellingRules, r => r.scope + ":" + r.id);
            }
            if (ext.riddles != null)
            {
                if (riddles == null) riddles = new List<RiddleDef>();
                MergeById(riddles, ext.riddles, r => r.id);
            }
            if (ext.dropProfiles != null)
            {
                if (dropProfiles == null) dropProfiles = new List<RuneDropProfile>();
                MergeById(dropProfiles, ext.dropProfiles, p => p.id);
            }
            conceptAdditions.AddRange(ext.conceptAdditions);
        }

        /// <summary>
        /// Applies <see cref="conceptAdditions"/> to the runes (same concept id replaces, new ones are added) and clears
        /// the list. Additions for runes that do not exist are kept in <paramref name="unmatched"/>.
        /// </summary>
        public void ApplyConceptAdditions(List<RuneConceptAddition> unmatched = null)
        {
            foreach (RuneConceptAddition add in conceptAdditions)
            {
                if (add == null) continue;
                RuneDef rune = runes.Find(r => r != null && r.id == add.rune);
                if (rune == null)
                {
                    unmatched?.Add(add);
                    continue;
                }
                MergeById(rune.concepts, add.concepts, c => c.id);
            }
            conceptAdditions.Clear();
        }

        private static void MergeById<T>(List<T> into, List<T> from, Func<T, string> idOf) where T : class
        {
            if (from == null)
                return;
            foreach (T item in from)
            {
                if (item == null) continue;
                string id = idOf(item);
                int at = into.FindIndex(x => x != null && idOf(x) == id);
                if (at >= 0) into[at] = item;
                else into.Add(item);
            }
        }
    }
}
