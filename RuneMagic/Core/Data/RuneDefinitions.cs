using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Plain data definitions of the Rune Magic content. They live inside RuneMagicData (and so inside the Rune Magic
    // Database asset) and are compiled into a RuneCatalog. Every definition has a stable lowercase id; saves and network
    // messages always use ids, never positions in a list.

    /// <summary>
    /// One word of the vocabulary that describes what magic does (force, pressure, thermal, wave, spatial...). Concepts,
    /// alignments, intentions and desired effects are described with weighted aspects, so the synthesis evaluation can
    /// relate any of them to any other without a rule for every pair.
    /// </summary>
    [Serializable]
    public class AspectDef
    {
        [Tooltip("Stable id (lowercase): 'pressure', 'thermal'...")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";

        public AspectDef() { }
        public AspectDef(string id, string displayName, string description)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
        }

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>An aspect and how strongly something carries it (0-1, more is allowed for dominant aspects).</summary>
    [Serializable]
    public class AspectWeight
    {
        [Tooltip("Aspect id.")]
        public string aspect = "";
        [Range(0f, 2f)] public float weight = 1f;

        public AspectWeight() { }
        public AspectWeight(string aspect, float weight)
        {
            this.aspect = aspect;
            this.weight = weight;
        }

        public override string ToString() => $"{aspect}:{weight:0.##}";
    }

    /// <summary>
    /// A fundamental magical domain (Fire: flames and combustion; Wind: air movement and pressure...). Base runes have one;
    /// composite runes (made by synthesis) can have several.
    /// </summary>
    [Serializable]
    public class AlignmentDef
    {
        [Tooltip("Stable id (lowercase): 'fire', 'wind'...")]
        public string id = "";
        public string displayName = "";
        [Tooltip("The domain in a few words (design spec 7.2 / 7.3).")]
        [TextArea(1, 3)] public string domain = "";
        [Tooltip("What the domain is about, as aspects.")]
        public List<AspectWeight> aspects = new List<AspectWeight>();
        [Tooltip("Combat element used by spells of this alignment (None = magical damage without an element).")]
        public ElementType element = ElementType.None;
        public Color color = Color.white;

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>
    /// A deeper principle of a rune that the player discovers and can use in synthesis (Wind ▸ Pressure, Fire ▸ Ignition).
    /// Its id is the rune id, a dot and the concept's own key: 'wind.pressure'.
    /// </summary>
    [Serializable]
    public class ConceptDef
    {
        [Tooltip("Stable id: '<rune id>.<concept>' (e.g. 'wind.pressure').")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("What the concept does, as aspects: the synthesis evaluation reads these.")]
        public List<AspectWeight> aspects = new List<AspectWeight>();
        [Tooltip("How rare the knowledge is: rarer concepts are found in harder dungeons.")]
        public RuneRarity rarity = RuneRarity.Common;
        [Tooltip("How hard the principle is to manipulate (0-1): raises synthesis risk and spell complexity.")]
        [Range(0f, 1f)] public float complexity = 0.3f;
        [Tooltip("Explicit coupling (optional): the lowest rune rank that can use this concept. 0 = any rank. Rank never " +
                 "grants concepts; this only restricts a known one.")]
        [Min(0)] public int minimumRankToUse = 0;
        [Tooltip("Free keywords for rules and tools.")]
        public List<string> tags = new List<string>();

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;

        /// <summary>The part after the rune id ('pressure' for 'wind.pressure').</summary>
        public string Key
        {
            get
            {
                int dot = id != null ? id.IndexOf('.') : -1;
                return dot >= 0 ? id.Substring(dot + 1) : id ?? "";
            }
        }

        public float AspectWeightOf(string aspect)
        {
            float w = 0f;
            for (int i = 0; i < aspects.Count; i++)
                if (aspects[i] != null && aspects[i].aspect == aspect) w += aspects[i].weight;
            return w;
        }
    }

    /// <summary>
    /// A rune definition: the kind of rune (Wind, Fire, Pyroclastic Resonance...). Rank, quality and energy belong to
    /// each rune the player owns, not to the definition; the concepts belong to the definition, and which of them a player
    /// knows is the player's knowledge.
    /// </summary>
    [Serializable]
    public class RuneDef
    {
        [Tooltip("Stable id (lowercase): 'wind', 'pyroclastic_resonance'...")]
        public string id = "";
        [Tooltip("Name without the rank title ('Wind'; shown as 'Greater Wind' at rank 1).")]
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        public RuneKind kind = RuneKind.Base;
        [Tooltip("Alignment ids: one for base runes, several for composites.")]
        public List<string> alignments = new List<string>();
        [Tooltip("Soul slots it occupies once absorbed.")]
        [Min(1)] public int slotCost = 1;
        [Tooltip("Lowest and highest rank this rune can have.")]
        [Min(1)] public int minRank = 1;
        [Min(1)] public int maxRank = 5;
        [Tooltip("Show the rank title before the name ('Greater Wind'). Composite runes usually have names of their own.")]
        public bool useRankTitle = true;
        public RuneRarity rarity = RuneRarity.Common;
        [Tooltip("Inherent characteristics and limitations (shown to the player).")]
        [TextArea(1, 4)] public string magicalProperties = "";
        [Tooltip("Keywords of its properties ('volatile', 'stable', 'heavy'...).")]
        public List<string> tags = new List<string>();
        [Tooltip("The hidden principles of this rune, discovered one by one.")]
        public List<ConceptDef> concepts = new List<ConceptDef>();
        public Color color = Color.white;

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;

        public ConceptDef FindConcept(string conceptId)
        {
            for (int i = 0; i < concepts.Count; i++)
                if (concepts[i] != null && concepts[i].id == conceptId) return concepts[i];
            return null;
        }
    }

    /// <summary>Support of an intention for one desired effect (0 = not supported, 1 = fully).</summary>
    [Serializable]
    public class EffectSupport
    {
        [Tooltip("Desired effect id.")]
        public string effect = "";
        [Range(0f, 1f)] public float support = 1f;

        public EffectSupport() { }
        public EffectSupport(string effect, float support)
        {
            this.effect = effect;
            this.support = support;
        }
    }

    /// <summary>
    /// The general purpose of a synthesis ("What am I trying to accomplish?"), chosen before the desired effect. It
    /// supports some desired effects and favours some aspects; it never guarantees a result.
    /// </summary>
    [Serializable]
    public class IntentionDef
    {
        [Tooltip("Stable id (lowercase): 'attack', 'defense'...")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Aspects this purpose favours (concepts carrying them fit the intention better).")]
        public List<AspectWeight> aspectBias = new List<AspectWeight>();
        [Tooltip("Desired-effect categories this intention fully supports.")]
        public List<EffectCategory> primaryCategories = new List<EffectCategory>();
        [Tooltip("Desired-effect categories it partly supports (Secondary Support).")]
        public List<EffectCategory> secondaryCategories = new List<EffectCategory>();
        [Tooltip("Support of effects in the secondary categories.")]
        [Range(0f, 1f)] public float secondarySupport = 0.5f;
        [Tooltip("Specific effects with their own support (outside the categories, or overriding them; 0 = not supported).")]
        public List<EffectSupport> effectOverrides = new List<EffectSupport>();

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>
    /// One requirement of a desired effect: the selected concepts together must carry at least <see cref="minCoverage"/>
    /// of any of these aspects.
    /// </summary>
    [Serializable]
    public class AspectRequirement
    {
        [Tooltip("Aspect ids: any of them counts.")]
        public List<string> anyOf = new List<string>();
        [Tooltip("Total weight the selected concepts must reach on those aspects.")]
        [Min(0f)] public float minCoverage = 0.5f;

        public AspectRequirement() { }
        public AspectRequirement(float minCoverage, params string[] aspects)
        {
            this.minCoverage = minCoverage;
            anyOf = new List<string>(aspects);
        }

        public override string ToString() => string.Join("|", anyOf) + $" ≥{minCoverage:0.##}";
    }

    /// <summary>One stage of a desired effect's ideal sequence (the aspects that should act at that point).</summary>
    [Serializable]
    public class ChainStage
    {
        public List<string> aspects = new List<string>();

        public ChainStage() { }
        public ChainStage(params string[] aspects) { this.aspects = new List<string>(aspects); }

        public override string ToString() => string.Join("|", aspects);
    }

    /// <summary>
    /// The specific result of a synthesis ("What exactly should this magic do?"): Explosion, Pierce, Barrier, Teleport...
    /// Whether it can be produced depends on the selected runes, concepts, order and energy, never on a fixed recipe.
    /// </summary>
    [Serializable]
    public class DesiredEffectDef
    {
        [Tooltip("Stable id: '<category>.<effect>' (e.g. 'offense.explosion').")]
        public string id = "";
        public string displayName = "";
        public EffectCategory category = EffectCategory.Offensive;
        [TextArea(1, 3)] public string description = "";
        [Tooltip("What the selected concepts must provide (every requirement must be met).")]
        public List<AspectRequirement> required = new List<AspectRequirement>();
        [Tooltip("Aspects that improve the result when present.")]
        public List<AspectWeight> supporting = new List<AspectWeight>();
        [Tooltip("Ideal order of the aspects (the rune order is compared with it). Empty = the order of the requirements.")]
        public List<ChainStage> idealChain = new List<ChainStage>();
        [Tooltip("Energy the effect asks for, relative to a normal effect (1).")]
        [Min(0.05f)] public float powerDemand = 1f;
        [Tooltip("How hard the effect is to shape (0-1).")]
        [Range(0f, 1f)] public float complexity = 0.4f;
        [Tooltip("Free keywords for rules and tools.")]
        public List<string> tags = new List<string>();

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;

        /// <summary>The ideal sequence: <see cref="idealChain"/>, or one stage per requirement.</summary>
        public List<ChainStage> EffectiveChain()
        {
            if (idealChain != null && idealChain.Count > 0)
                return idealChain;
            var chain = new List<ChainStage>();
            foreach (AspectRequirement r in required)
                if (r != null) chain.Add(new ChainStage(r.anyOf.ToArray()));
            return chain;
        }
    }

    /// <summary>
    /// A relation between two things the synthesis evaluation should know about: a synergy (positive value) or a
    /// conflict (negative), optionally only in one order, or a hard incoherence (always a complete failure). For
    /// <see cref="CompatibilityScope.IntentionEffect"/> the value is the support (0-1).
    /// </summary>
    [Serializable]
    public class CompatibilityRule
    {
        [Tooltip("Stable id, e.g. 'aspect:pressure+combustion' (scope, the two ids, '>' when ordered).")]
        public string id = "";
        public CompatibilityScope scope = CompatibilityScope.AspectPair;
        [Tooltip("First id (aspect, concept, alignment or intention, by scope).")]
        public string a = "";
        [Tooltip("Second id (aspect, concept, alignment or desired effect, by scope).")]
        public string b = "";
        [Tooltip("Synergy (+) or conflict (-), -1 to 1. IntentionEffect: the support, 0 to 1.")]
        [Range(-1f, 1f)] public float value = 0f;
        [Tooltip("Only when A comes before B in the rune order.")]
        public bool ordered = false;
        [Tooltip("The combination can never be coherent: a synthesis with both always fails completely.")]
        public bool incoherent = false;
        [Tooltip("Why (for designers and the synthesis log).")]
        [TextArea(1, 2)] public string note = "";

        public CompatibilityRule() { }
        public CompatibilityRule(CompatibilityScope scope, string a, string b, float value, string note, bool ordered = false, bool incoherent = false)
        {
            this.scope = scope;
            this.a = a;
            this.b = b;
            this.value = value;
            this.note = note;
            this.ordered = ordered;
            this.incoherent = incoherent;
            id = MakeId(scope, a, b, ordered);
        }

        /// <summary>"aspect:pressure+combustion", "concept:time.haste+time.slowness", "aspect:pressure>combustion" (ordered).</summary>
        public static string MakeId(CompatibilityScope scope, string a, string b, bool ordered)
        {
            string prefix;
            switch (scope)
            {
                case CompatibilityScope.AspectPair: prefix = "aspect"; break;
                case CompatibilityScope.ConceptPair: prefix = "concept"; break;
                case CompatibilityScope.AlignmentPair: prefix = "alignment"; break;
                case CompatibilityScope.IntentionEffect: prefix = "intention-effect"; break;
                case CompatibilityScope.ConceptIntention: prefix = "concept-intention"; break;
                default: prefix = "concept-effect"; break;
            }
            return prefix + ":" + a + (ordered ? ">" : "+") + b;
        }

        /// <summary>Does this rule relate <paramref name="x"/> and <paramref name="y"/> (either way unless ordered)?</summary>
        public bool Relates(string x, string y) => (a == x && b == y) || (!ordered && a == y && b == x);
    }

    /// <summary>Concepts a content pack adds to (or replaces in) a rune defined elsewhere, without redefining the rune.</summary>
    [Serializable]
    public class RuneConceptAddition
    {
        [Tooltip("Id of the rune that receives the concepts.")]
        public string rune = "";
        [Tooltip("Concepts to add (same id = replaces the existing one).")]
        public List<ConceptDef> concepts = new List<ConceptDef>();
    }
}
