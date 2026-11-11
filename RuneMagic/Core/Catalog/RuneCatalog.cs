using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The Rune Magic content compiled for use: the database's data with its content packs merged in, indexed by id,
    /// validated, and identified by a content hash (two machines with the same content have the same hash). Immutable
    /// once compiled: the rules read it, nothing writes to it. Pure C#: no Unity objects.
    /// </summary>
    public sealed class RuneCatalog
    {
        private readonly Dictionary<string, AspectDef> aspects = new Dictionary<string, AspectDef>();
        private readonly Dictionary<string, AlignmentDef> alignments = new Dictionary<string, AlignmentDef>();
        private readonly Dictionary<string, RuneDef> runes = new Dictionary<string, RuneDef>();
        private readonly Dictionary<string, ConceptDef> concepts = new Dictionary<string, ConceptDef>();
        private readonly Dictionary<string, RuneDef> runeOfConcept = new Dictionary<string, RuneDef>();
        private readonly Dictionary<string, IntentionDef> intentions = new Dictionary<string, IntentionDef>();
        private readonly Dictionary<string, DesiredEffectDef> effects = new Dictionary<string, DesiredEffectDef>();
        private readonly Dictionary<string, RuneDropProfile> dropProfiles = new Dictionary<string, RuneDropProfile>();
        private readonly Dictionary<string, DiscoverySourceDef> sources = new Dictionary<string, DiscoverySourceDef>();
        private readonly Dictionary<string, RiddleDef> riddles = new Dictionary<string, RiddleDef>();
        private readonly Dictionary<string, List<RiddleDef>> riddlesOfConcept = new Dictionary<string, List<RiddleDef>>();
        private readonly Dictionary<string, OutcomeArchetype> archetypes = new Dictionary<string, OutcomeArchetype>();
        private readonly Dictionary<string, SynthesisFlawDef> flaws = new Dictionary<string, SynthesisFlawDef>();
        private readonly Dictionary<string, SpellFormDef> spellForms = new Dictionary<string, SpellFormDef>();
        private readonly Dictionary<string, SpellFormDef> formOfEffect = new Dictionary<string, SpellFormDef>();
        private readonly Dictionary<string, List<CompatibilityRule>> ruleIndex = new Dictionary<string, List<CompatibilityRule>>(StringComparer.Ordinal);
        private static readonly IReadOnlyList<CompatibilityRule> NoRules = Array.Empty<CompatibilityRule>();
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        /// <summary>The merged data this catalog was compiled from (a private copy: changing it changes nothing).</summary>
        public RuneMagicData Data { get; }
        public RuneMagicSettings Settings => Data.settings;
        public RuneBalance Balance => Data.balance;

        public IReadOnlyList<AspectDef> Aspects => Data.aspects;
        public IReadOnlyList<AlignmentDef> Alignments => Data.alignments;
        public IReadOnlyList<RuneDef> Runes => Data.runes;
        public IReadOnlyList<IntentionDef> Intentions => Data.intentions;
        public IReadOnlyList<DesiredEffectDef> Effects => Data.effects;
        public IReadOnlyList<CompatibilityRule> Rules => Data.rules;
        public IReadOnlyList<RuneDropProfile> DropProfiles => Data.dropProfiles;
        public IReadOnlyList<DiscoverySourceDef> DiscoverySources => Data.discoverySources;
        public IReadOnlyList<RiddleDef> Riddles => Data.riddles;
        public IReadOnlyList<OutcomeArchetype> Archetypes => Data.archetypes;
        public IReadOnlyList<SynthesisFlawDef> Flaws => Data.synthesisFlaws;
        public SynthesisBalance Synthesis => Data.balance.synthesisEngine;
        public IReadOnlyList<SpellFormDef> SpellForms => Data.spellForms;
        public SpellBalance Spells => Data.balance.spells;
        public SpellingBalance Spelling => Data.balance.spelling;
        public GrimoireBalance Grimoire => Data.balance.grimoire;
        public IReadOnlyList<SpellingRule> SpellingRules => Data.spellingRules;
        public DiscoveryBalance Discovery => Data.balance.discovery;
        public int ConceptCount => concepts.Count;

        /// <summary>Setup errors: content with them should not be used (the editor's validator shows them).</summary>
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool IsValid => errors.Count == 0;

        /// <summary>Identifies the content (FNV-1a of its JSON): the server sends it so clients can tell when theirs differs.</summary>
        public string ContentHash { get; private set; } = "";

        private RuneCatalog(RuneMagicData merged)
        {
            Data = merged;
        }

        // ------------------------------------------------------------------ compiling
        /// <summary>
        /// Compiles <paramref name="data"/> with <paramref name="extensions"/> (content packs, merged in order: same ids
        /// replace). The inputs are not modified.
        /// </summary>
        public static RuneCatalog Compile(RuneMagicData data, IList<RuneMagicData> extensions = null)
        {
            RuneMagicData merged = data != null ? data.Clone() : new RuneMagicData();
            merged.Upgrade(); // data of an older format (an asset made before an update) gets what was added since
            if (extensions != null)
                foreach (RuneMagicData ext in extensions)
                    merged.Merge(ext);
            var unmatched = new List<RuneConceptAddition>();
            merged.ApplyConceptAdditions(unmatched);

            var catalog = new RuneCatalog(merged);
            foreach (RuneConceptAddition u in unmatched)
                catalog.warnings.Add($"Concept addition for unknown rune '{u.rune}' ({u.concepts.Count} concept(s)) was ignored.");
            catalog.Index();
            catalog.Validate();
            catalog.ContentHash = Hash(merged.ToJson());
            return catalog;
        }

        private void Index()
        {
            AddAll(Data.aspects, a => a?.id, aspects, "aspect");
            AddAll(Data.alignments, a => a?.id, alignments, "alignment");
            AddAll(Data.runes, r => r?.id, runes, "rune");
            AddAll(Data.intentions, i => i?.id, intentions, "intention");
            AddAll(Data.effects, e => e?.id, effects, "desired effect");
            if (Data.dropProfiles == null) Data.dropProfiles = new List<RuneDropProfile>();
            AddAll(Data.dropProfiles, p => p?.id, dropProfiles, "drop profile");
            if (Data.discoverySources == null) Data.discoverySources = new List<DiscoverySourceDef>();
            if (Data.riddles == null) Data.riddles = new List<RiddleDef>();
            AddAll(Data.discoverySources, s => s?.id, sources, "discovery source");
            AddAll(Data.riddles, r => r?.id, riddles, "riddle");
            if (Data.archetypes == null) Data.archetypes = new List<OutcomeArchetype>();
            if (Data.synthesisFlaws == null) Data.synthesisFlaws = new List<SynthesisFlawDef>();
            AddAll(Data.archetypes, a => a?.id, archetypes, "archetype");
            AddAll(Data.synthesisFlaws, f => f?.id, flaws, "synthesis flaw");
            if (Data.spellForms == null) Data.spellForms = new List<SpellFormDef>();
            if (Data.spellingRules == null) Data.spellingRules = new List<SpellingRule>();
            if (Data.balance.grimoire == null) Data.balance.grimoire = RuneDefaults.Grimoire();
            AddAll(Data.spellForms, f => f?.id, spellForms, "spell form");
            IndexSpellForms();
            foreach (CompatibilityRule r in Data.rules)
            {
                if (r == null) continue;
                IndexRule(r.scope, r.a, r.b, r);
                if (!r.ordered && r.a != r.b) IndexRule(r.scope, r.b, r.a, r);
            }
            foreach (RiddleDef r in Data.riddles)
            {
                if (r == null || string.IsNullOrEmpty(r.concept)) continue;
                if (!riddlesOfConcept.TryGetValue(r.concept, out List<RiddleDef> list)) riddlesOfConcept[r.concept] = list = new List<RiddleDef>();
                list.Add(r);
            }
            foreach (RuneDef r in Data.runes)
            {
                if (r == null || r.concepts == null) continue;
                foreach (ConceptDef c in r.concepts)
                {
                    if (c == null || string.IsNullOrEmpty(c.id)) continue;
                    if (concepts.ContainsKey(c.id))
                    {
                        errors.Add($"Concept id '{c.id}' is used twice.");
                        continue;
                    }
                    concepts[c.id] = c;
                    runeOfConcept[c.id] = r;
                }
            }
        }

        private void AddAll<T>(List<T> list, Func<T, string> idOf, Dictionary<string, T> into, string what) where T : class
        {
            foreach (T item in list)
            {
                if (item == null) { errors.Add($"An empty {what} entry."); continue; }
                string id = idOf(item);
                if (string.IsNullOrEmpty(id)) { errors.Add($"A {what} has no id."); continue; }
                if (into.ContainsKey(id)) { errors.Add($"The {what} id '{id}' is used twice."); continue; }
                into[id] = item;
            }
        }

        // ------------------------------------------------------------------ lookups
        public AspectDef Aspect(string id) => id != null && aspects.TryGetValue(id, out AspectDef a) ? a : null;
        public AlignmentDef Alignment(string id) => id != null && alignments.TryGetValue(id, out AlignmentDef a) ? a : null;
        public RuneDef Rune(string id) => id != null && runes.TryGetValue(id, out RuneDef r) ? r : null;
        public ConceptDef Concept(string id) => id != null && concepts.TryGetValue(id, out ConceptDef c) ? c : null;
        /// <summary>The rune a concept belongs to.</summary>
        public RuneDef RuneOfConcept(string conceptId) => conceptId != null && runeOfConcept.TryGetValue(conceptId, out RuneDef r) ? r : null;
        public IntentionDef Intention(string id) => id != null && intentions.TryGetValue(id, out IntentionDef i) ? i : null;
        public DesiredEffectDef Effect(string id) => id != null && effects.TryGetValue(id, out DesiredEffectDef e) ? e : null;
        public DiscoverySourceDef DiscoverySource(string id) => id != null && sources.TryGetValue(id, out DiscoverySourceDef s) ? s : null;
        public RiddleDef Riddle(string id) => id != null && riddles.TryGetValue(id, out RiddleDef r) ? r : null;
        /// <summary>The riddles written for a concept (empty when it has none: it gets generated ones).</summary>
        public IReadOnlyList<RiddleDef> RiddlesOf(string conceptId) =>
            conceptId != null && riddlesOfConcept.TryGetValue(conceptId, out List<RiddleDef> l) ? (IReadOnlyList<RiddleDef>)l : Array.Empty<RiddleDef>();
        public OutcomeArchetype Archetype(string id) => id != null && archetypes.TryGetValue(id, out OutcomeArchetype a) ? a : null;
        public SynthesisFlawDef Flaw(string id) => id != null && flaws.TryGetValue(id, out SynthesisFlawDef f) ? f : null;
        public SpellFormDef SpellForm(string id) => id != null && spellForms.TryGetValue(id, out SpellFormDef f) ? f : null;

        /// <summary>The spell form of a desired effect: the form that lists it, else the first form that is the fallback of its category (null when none).</summary>
        public SpellFormDef FormOfEffect(string effectId) => effectId != null && formOfEffect.TryGetValue(effectId, out SpellFormDef f) ? f : null;

        private void IndexSpellForms()
        {
            foreach (SpellFormDef f in Data.spellForms)
            {
                if (f?.effects == null) continue;
                foreach (string effect in f.effects)
                    if (!string.IsNullOrEmpty(effect) && !formOfEffect.ContainsKey(effect)) formOfEffect[effect] = f;
            }
            foreach (DesiredEffectDef fx in Data.effects)
            {
                if (fx == null || formOfEffect.ContainsKey(fx.id)) continue;
                foreach (SpellFormDef f in Data.spellForms)
                    if (f?.fallbackFor != null && f.fallbackFor.Contains(fx.category)) { formOfEffect[fx.id] = f; break; }
            }
        }

        private void IndexRule(CompatibilityScope scope, string first, string second, CompatibilityRule rule)
        {
            string key = RuleKey(scope, first, second);
            if (!ruleIndex.TryGetValue(key, out List<CompatibilityRule> list)) ruleIndex[key] = list = new List<CompatibilityRule>(1);
            list.Add(rule);
        }

        private static string RuleKey(CompatibilityScope scope, string first, string second) => (int)scope + "|" + first + "|" + second;

        /// <summary>
        /// The rules of <paramref name="scope"/> that apply when <paramref name="first"/> comes before <paramref name="second"/>
        /// (an unordered rule either way, an ordered rule only in its own order). Indexed: cheap enough for the synthesis
        /// evaluation to ask for every pair of aspects.
        /// </summary>
        public IReadOnlyList<CompatibilityRule> RulesBetween(CompatibilityScope scope, string first, string second) =>
            first != null && second != null && ruleIndex.TryGetValue(RuleKey(scope, first, second), out List<CompatibilityRule> l) ? l : NoRules;

        public RuneDropProfile DropProfile(string id) => id != null && dropProfiles.TryGetValue(id, out RuneDropProfile p) ? p : null;

        public List<DesiredEffectDef> EffectsIn(EffectCategory category)
        {
            var list = new List<DesiredEffectDef>();
            foreach (DesiredEffectDef e in Data.effects)
                if (e != null && e.category == category) list.Add(e);
            return list;
        }

        /// <summary>The rules of <paramref name="scope"/> that relate <paramref name="x"/> and <paramref name="y"/>.</summary>
        public List<CompatibilityRule> RulesFor(CompatibilityScope scope, string x, string y)
        {
            var list = new List<CompatibilityRule>();
            foreach (CompatibilityRule r in Data.rules)
                if (r != null && r.scope == scope && r.Relates(x, y)) list.Add(r);
            return list;
        }

        /// <summary>
        /// How much an intention supports a desired effect (0-1). Order of precedence: an IntentionEffect rule (an
        /// incoherent one is 0), the intention's own override for the effect, its primary categories (1), its secondary
        /// categories (Secondary Support), otherwise 0.
        /// </summary>
        public float IntentionSupport(string intentionId, string effectId)
        {
            IntentionDef intention = Intention(intentionId);
            DesiredEffectDef effect = Effect(effectId);
            if (intention == null || effect == null)
                return 0f;
            foreach (CompatibilityRule r in Data.rules)
                if (r != null && r.scope == CompatibilityScope.IntentionEffect && r.a == intentionId && r.b == effectId)
                    return r.incoherent ? 0f : Mathf.Clamp01(r.value);
            foreach (EffectSupport s in intention.effectOverrides)
                if (s != null && s.effect == effectId) return Mathf.Clamp01(s.support);
            if (intention.primaryCategories.Contains(effect.category)) return 1f;
            if (intention.secondaryCategories.Contains(effect.category)) return Mathf.Clamp01(intention.secondarySupport);
            return 0f;
        }

        /// <summary>The desired effects an intention supports at all, best supported first.</summary>
        public List<DesiredEffectDef> EffectsSupportedBy(string intentionId)
        {
            var list = new List<DesiredEffectDef>();
            foreach (DesiredEffectDef e in Data.effects)
                if (e != null && IntentionSupport(intentionId, e.id) > 0f) list.Add(e);
            list.Sort((x, y) => IntentionSupport(intentionId, y.id).CompareTo(IntentionSupport(intentionId, x.id)));
            return list;
        }

        /// <summary>"Greater Wind" (rank title + name) or the rune's own name when it uses no title.</summary>
        public string RuneName(RuneDef rune, int rank)
        {
            if (rune == null) return "";
            string title = rune.useRankTitle ? Balance.TitleOf(rank) : "";
            return string.IsNullOrEmpty(title) ? rune.Name : title + " " + rune.Name;
        }

        // ------------------------------------------------------------------ validation
        private void ValidateSynthesis()
        {
            var resultRunes = new HashSet<string>(StringComparer.Ordinal);
            foreach (OutcomeArchetype a in Data.archetypes)
                if (a != null && !string.IsNullOrEmpty(a.resultRuneId)) resultRunes.Add(a.resultRuneId);
            foreach (OutcomeArchetype a in Data.archetypes)
            {
                if (a == null) continue;
                if (!IsValidId(a.id)) errors.Add($"Archetype id '{a.id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
                if (!string.IsNullOrEmpty(a.resultRuneId) && !IsValidId(a.resultRuneId)) errors.Add($"Archetype '{a.id}': the result rune id '{a.resultRuneId}' is not valid.");
                if (!string.IsNullOrEmpty(a.resultRuneId) && Rune(a.resultRuneId) != null) errors.Add($"Archetype '{a.id}': the result rune id '{a.resultRuneId}' is already a rune of the database.");
                if (Intention(a.intention) == null) errors.Add($"Archetype '{a.id}' names the unknown intention '{a.intention}'.");
                if (Effect(a.effect) == null) errors.Add($"Archetype '{a.id}' names the unknown desired effect '{a.effect}'.");
                else if (Intention(a.intention) != null && IntentionSupport(a.intention, a.effect) <= 0f)
                    errors.Add($"Archetype '{a.id}': the intention '{a.intention}' does not support '{a.effect}', so its ideal formula would always fail.");
                if (a.stages == null || a.stages.Count == 0) { errors.Add($"Archetype '{a.id}' has no stages."); continue; }
                foreach (ArchetypeStage s in a.stages)
                {
                    if (s == null) { errors.Add($"Archetype '{a.id}' has an empty stage."); continue; }
                    bool composite = resultRunes.Contains(s.runeId);
                    if (Rune(s.runeId) == null && !composite) errors.Add($"Archetype '{a.id}' uses the unknown rune '{s.runeId}'.");
                    if (!string.IsNullOrEmpty(s.conceptId))
                    {
                        ConceptDef k = Concept(s.conceptId);
                        if (k == null) errors.Add($"Archetype '{a.id}' uses the unknown concept '{s.conceptId}'.");
                        else if (RuneOfConcept(s.conceptId)?.id != s.runeId) errors.Add($"Archetype '{a.id}': concept '{s.conceptId}' does not belong to rune '{s.runeId}'.");
                    }
                    if (s.count < 1) errors.Add($"Archetype '{a.id}': a stage has a count below 1.");
                }
            }
            var causes = new HashSet<FlawCause>();
            foreach (SynthesisFlawDef f in Data.synthesisFlaws)
            {
                if (f == null) continue;
                if (!IsValidId(f.id)) errors.Add($"Synthesis flaw id '{f.id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
                causes.Add(f.cause);
            }
            if (Data.synthesisFlaws.Count > 0)
                foreach (FlawCause cause in Enum.GetValues(typeof(FlawCause)))
                    if (!causes.Contains(cause)) warnings.Add($"No synthesis flaw has the cause '{cause}': a flawed result with only that fault gets no flaw.");
        }

        private void ValidateSpells()
        {
            var claimed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (SpellFormDef f in Data.spellForms)
            {
                if (f == null) continue;
                if (!IsValidId(f.id)) errors.Add($"Spell form id '{f.id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
                else if (f.id.IndexOf(':') >= 0) errors.Add($"Spell form id '{f.id}' must not contain ':' (it is part of an ability id).");
                if (f.effects == null) continue;
                foreach (string e in f.effects)
                {
                    if (Effect(e) == null) { errors.Add($"Spell form '{f.id}' names the unknown desired effect '{e}'."); continue; }
                    if (claimed.TryGetValue(e, out string other) && other != f.id) warnings.Add($"Desired effect '{e}' is listed by the spell forms '{other}' and '{f.id}': the first is used.");
                    else claimed[e] = f.id;
                }
            }
            foreach (DesiredEffectDef fx in Data.effects)
                if (fx != null && FormOfEffect(fx.id) == null)
                    errors.Add($"Desired effect '{fx.id}' has no spell form (list it in a form, or make a form the fallback of its category): its spells could not be cast.");
        }

        private void ValidateSpelling()
        {
            SpellingBalance b = Data.balance.spelling;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpellingRule r in Data.spellingRules)
            {
                if (r == null) { warnings.Add("Spelling rules: an entry is empty."); continue; }
                string what = r.scope + " '" + r.id + "'";
                bool known = r.scope == SpellingScope.Form ? SpellForm(r.id) != null
                    : r.scope == SpellingScope.Effect ? Effect(r.id) != null
                    : Archetype(r.id) != null;
                if (!known) errors.Add($"Spelling rule for {what}: there is no such {r.scope.ToString().ToLowerInvariant()}.");
                if (!seen.Add(r.scope + ":" + r.id)) warnings.Add($"Spelling rules: {what} is listed twice (the first is used).");
                if (r.keyWindows != null && r.keyWindows.Exists(w => w <= 0f)) errors.Add($"Spelling rule for {what}: a key window is not above 0.");
                if (r.letters == 0 || r.letters > 40) errors.Add($"Spelling rule for {what}: Letters must be −1 (generated) or between 1 and 40.");
            }
            if (b != null && b.weights != null)
                foreach (SpellingWeight w in b.weights)
                    if (w != null && !SpellingEvaluator.HasTerm(w.term)) warnings.Add($"Spelling: the evaluation term '{w.term}' is not known (it counts for nothing).");
            if (Data.balance.spells != null && b != null)
            {
                SoulDamageKind kind = Data.balance.DamageKind(b.damageKind);
                if (kind == null) warnings.Add($"Spelling: the Damage Kind '{b.damageKind}' is not in Soul Damage Kinds (it is ordinary damage).");
            }
        }

        private void ValidateDiscovery()
        {
            DiscoveryBalance d = Data.balance.discovery;
            int tiers = d != null && d.tiers != null ? d.tiers.Count : 0;
            if (Data.discoverySources.Count == 0) warnings.Add("No discovery sources: concepts cannot be discovered.");
            foreach (DiscoverySourceDef s in Data.discoverySources)
            {
                if (s == null) continue;
                if (!IsValidId(s.id)) errors.Add($"Discovery source id '{s.id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
                if (s.id != null && s.id.IndexOf('|') >= 0) errors.Add($"Discovery source id '{s.id}' may not contain '|'.");
                foreach (string r in s.runes)
                    if (Rune(r) == null) errors.Add($"Discovery source '{s.id}' names the unknown rune '{r}'.");
                foreach (string k in s.concepts)
                    if (Concept(k) == null) errors.Add($"Discovery source '{s.id}' names the unknown concept '{k}'.");
                if (s.maxTier >= 0 && s.maxTier < s.minTier) errors.Add($"Discovery source '{s.id}': Max Tier is below Min Tier.");
                if (tiers > 0 && s.minTier >= tiers) warnings.Add($"Discovery source '{s.id}' starts at band {s.minTier}, above the last band ({tiers - 1}): it never works.");
            }
            foreach (RiddleDef r in Data.riddles)
            {
                if (r == null) continue;
                if (!IsValidId(r.id)) errors.Add($"Riddle id '{r.id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
                if (Concept(r.concept) == null) errors.Add($"Riddle '{r.id}' is about the unknown concept '{r.concept}'.");
                if (string.IsNullOrWhiteSpace(r.text)) errors.Add($"Riddle '{r.id}' has no text.");
                foreach (string k in r.distractors)
                {
                    if (Concept(k) == null) errors.Add($"Riddle '{r.id}' offers the unknown concept '{k}' as a wrong answer.");
                    else if (k == r.concept) errors.Add($"Riddle '{r.id}' offers its own answer as a wrong answer.");
                }
                if (r.maxTier >= 0 && r.maxTier < r.minTier) errors.Add($"Riddle '{r.id}': Max Tier is below Min Tier.");
            }
            if (tiers > 0 && Data.discoverySources.Count > 0)
            {
                var unreachable = new List<ConceptDef>();
                var fragile = new List<ConceptDef>();
                DiscoveryCoverage.Find(this, unreachable, fragile);
                if (unreachable.Count > 0)
                    warnings.Add($"{unreachable.Count} concept(s) can never be discovered: no source teaches them at any band (e.g. '{unreachable[0].id}').");
                if (fragile.Count > 0)
                    warnings.Add($"{fragile.Count} concept(s) have a single source: one wrong answer would lock them for good (e.g. '{fragile[0].id}'). Add another source.");
            }
        }

        private void ValidateDrops()
        {
            RuneAcquisitionBalance a = Data.balance.acquisition;
            if (a != null && !string.IsNullOrEmpty(a.defaultProfile) && DropProfile(a.defaultProfile) == null)
                errors.Add($"Acquisition: the default drop profile '{a.defaultProfile}' does not exist.");
            if (Data.dropProfiles.Count == 0)
                warnings.Add("No drop profiles: monsters carry no runes.");
            foreach (RuneDropProfile p in Data.dropProfiles)
            {
                if (p == null) continue;
                if (!IsValidId(p.id)) errors.Add($"Drop profile id '{p.id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
                if (p.runes == null || p.runes.Count == 0) warnings.Add($"Drop profile '{p.id}' lists no runes (it can only drop runes of the monster's own alignment).");
                else
                    foreach (RuneDropEntry e in p.runes)
                    {
                        if (e == null) { errors.Add($"Drop profile '{p.id}' has an empty rune entry."); continue; }
                        if (Rune(e.runeId) == null) errors.Add($"Drop profile '{p.id}' drops the unknown rune '{e.runeId}'.");
                        if (e.minRank > 0 && e.maxRank > 0 && e.minRank > e.maxRank) errors.Add($"Drop profile '{p.id}': rune '{e.runeId}' has Min Rank above Max Rank.");
                    }
                if (p.qualityRange.x > p.qualityRange.y) errors.Add($"Drop profile '{p.id}': the quality range is reversed.");
                if (p.startingEnergy.x > p.startingEnergy.y) errors.Add($"Drop profile '{p.id}': the starting energy range is reversed.");
                if ((p.matchWords == null || p.matchWords.Count == 0) && (p.matchTags == null || p.matchTags.Count == 0) &&
                    (a == null || a.defaultProfile != p.id))
                    warnings.Add($"Drop profile '{p.id}' matches no monster by itself (no words or tags): only a Rune Source naming it uses it.");
            }
        }

        private void Validate()
        {
            if (Data.version > RuneMagicData.CurrentVersion)
                warnings.Add($"The data is version {Data.version}, newer than this code understands ({RuneMagicData.CurrentVersion}).");
            Data.balance.Validate(errors, warnings);
            ValidateDrops();
            ValidateDiscovery();
            ValidateSynthesis();
            ValidateSpells();
            ValidateSpelling();
            if (Data.settings.enabled && (Data.settings.runeUserClassNames == null || !Data.settings.runeUserClassNames.Exists(n => !string.IsNullOrWhiteSpace(n))))
                warnings.Add("Settings: no rune-user class names (only class assets listed on the database can use Rune Magic).");

            var usedAspects = new HashSet<string>();
            void CheckId(string id, string what)
            {
                if (!IsValidId(id)) errors.Add($"{what} id '{id}' is not valid (lowercase letters, digits, '.', '_' and '-' only).");
            }
            void CheckAspects(List<AspectWeight> list, string owner, bool warnIfEmpty)
            {
                if (list == null || list.Count == 0)
                {
                    if (warnIfEmpty) warnings.Add($"{owner} has no aspects (the synthesis cannot relate it to anything).");
                    return;
                }
                foreach (AspectWeight w in list)
                {
                    if (w == null) { errors.Add($"{owner} has an empty aspect entry."); continue; }
                    if (Aspect(w.aspect) == null) errors.Add($"{owner} uses the unknown aspect '{w.aspect}'.");
                    else usedAspects.Add(w.aspect);
                    if (w.weight < 0f) errors.Add($"{owner}: aspect '{w.aspect}' has a negative weight.");
                }
            }

            foreach (AspectDef a in Data.aspects) if (a != null) CheckId(a.id, "Aspect");
            foreach (AlignmentDef al in Data.alignments)
            {
                if (al == null) continue;
                CheckId(al.id, "Alignment");
                CheckAspects(al.aspects, $"Alignment '{al.id}'", true);
            }
            foreach (RuneDef r in Data.runes)
            {
                if (r == null) continue;
                CheckId(r.id, "Rune");
                if (r.alignments == null || r.alignments.Count == 0) errors.Add($"Rune '{r.id}' has no alignment.");
                else foreach (string al in r.alignments) if (Alignment(al) == null) errors.Add($"Rune '{r.id}' uses the unknown alignment '{al}'.");
                if (r.kind == RuneKind.Base && r.alignments != null && r.alignments.Count > 1) warnings.Add($"Base rune '{r.id}' has several alignments (composites usually do).");
                if (r.minRank > r.maxRank) errors.Add($"Rune '{r.id}': Min Rank is above Max Rank.");
                if (r.slotCost < 1) errors.Add($"Rune '{r.id}': Slot Cost must be at least 1.");
                if (r.concepts == null || r.concepts.Count == 0) warnings.Add($"Rune '{r.id}' has no concepts.");
                else
                    foreach (ConceptDef c in r.concepts)
                    {
                        if (c == null) { errors.Add($"Rune '{r.id}' has an empty concept entry."); continue; }
                        CheckId(c.id, "Concept");
                        if (c.id == null || !c.id.StartsWith(r.id + ".", StringComparison.Ordinal))
                            errors.Add($"Concept '{c.id}' of rune '{r.id}' must be named '{r.id}.<concept>'.");
                        CheckAspects(c.aspects, $"Concept '{c.id}'", true);
                        if (c.minimumRankToUse > r.maxRank) warnings.Add($"Concept '{c.id}' needs rank {c.minimumRankToUse}, above the rune's highest rank.");
                    }
            }
            foreach (IntentionDef i in Data.intentions)
            {
                if (i == null) continue;
                CheckId(i.id, "Intention");
                CheckAspects(i.aspectBias, $"Intention '{i.id}'", false);
                foreach (EffectSupport s in i.effectOverrides)
                    if (s != null && Effect(s.effect) == null) errors.Add($"Intention '{i.id}' refers to the unknown desired effect '{s.effect}'.");
                if (EffectsSupportedBy(i.id).Count == 0) warnings.Add($"Intention '{i.id}' supports no desired effect.");
            }
            foreach (DesiredEffectDef e in Data.effects)
            {
                if (e == null) continue;
                CheckId(e.id, "Desired effect");
                if (e.required == null || e.required.Count == 0) warnings.Add($"Desired effect '{e.id}' has no requirement (any concepts would produce it).");
                else
                    foreach (AspectRequirement req in e.required)
                    {
                        if (req == null || req.anyOf.Count == 0) { errors.Add($"Desired effect '{e.id}' has an empty requirement."); continue; }
                        bool reachable = false;
                        foreach (string a in req.anyOf)
                        {
                            if (Aspect(a) == null) { errors.Add($"Desired effect '{e.id}' requires the unknown aspect '{a}'."); continue; }
                            usedAspects.Add(a);
                            if (!reachable && AnyConceptCarries(a)) reachable = true;
                        }
                        if (!reachable) warnings.Add($"Desired effect '{e.id}': no concept provides any of {req} (it can never be produced).");
                    }
                CheckAspects(e.supporting, $"Desired effect '{e.id}' (supporting)", false);
                foreach (ChainStage st in e.idealChain)
                    if (st != null)
                        foreach (string a in st.aspects)
                            if (Aspect(a) == null) errors.Add($"Desired effect '{e.id}': its ideal chain uses the unknown aspect '{a}'.");
                bool supported = false;
                foreach (IntentionDef i in Data.intentions)
                    if (i != null && IntentionSupport(i.id, e.id) > 0f) { supported = true; break; }
                if (!supported) warnings.Add($"Desired effect '{e.id}' is supported by no intention.");
            }
            var ruleIds = new HashSet<string>();
            foreach (CompatibilityRule r in Data.rules)
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(r.id)) errors.Add("A compatibility rule has no id.");
                else if (!ruleIds.Add(r.id)) errors.Add($"The compatibility rule id '{r.id}' is used twice.");
                if (r.scope == CompatibilityScope.IntentionEffect && (r.value < 0f || r.value > 1f) && !r.incoherent)
                    warnings.Add($"Compatibility rule '{r.id}': an intention's support should be 0 to 1.");
                if (!Known(r.scope, r.a, true) || !Known(r.scope, r.b, false))
                    errors.Add($"Compatibility rule '{r.id}' ({r.scope}) refers to an unknown id ('{r.a}' / '{r.b}').");
                if (r.scope == CompatibilityScope.AspectPair) { usedAspects.Add(r.a); usedAspects.Add(r.b); }
            }
            foreach (AspectDef a in Data.aspects)
                if (a != null && !usedAspects.Contains(a.id)) warnings.Add($"Aspect '{a.id}' is not used by anything.");
        }

        private bool AnyConceptCarries(string aspect)
        {
            foreach (ConceptDef c in concepts.Values)
                if (c.AspectWeightOf(aspect) > 0f) return true;
            return false;
        }

        private bool Known(CompatibilityScope scope, string id, bool first)
        {
            switch (scope)
            {
                case CompatibilityScope.AspectPair: return Aspect(id) != null;
                case CompatibilityScope.ConceptPair: return Concept(id) != null;
                case CompatibilityScope.AlignmentPair: return Alignment(id) != null;
                case CompatibilityScope.IntentionEffect: return first ? Intention(id) != null : Effect(id) != null;
                case CompatibilityScope.ConceptIntention: return first ? Concept(id) != null : Intention(id) != null;
                case CompatibilityScope.ConceptEffect: return first ? Concept(id) != null : Effect(id) != null;
                default: return false;
            }
        }

        /// <summary>Lowercase letters, digits, '.', '_' and '-', starting with a letter or digit.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || (i > 0 && (c == '.' || c == '_' || c == '-'));
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>FNV-1a (64 bits) of <paramref name="text"/> as 16 hexadecimal digits.</summary>
        public static string Hash(string text)
        {
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            ulong h = offset;
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            for (int i = 0; i < bytes.Length; i++)
            {
                h ^= bytes[i];
                h *= prime;
            }
            return h.ToString("x16");
        }
    }
}
