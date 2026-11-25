using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// One rune absorbed into a soul (design spec 2, 3, 5): which rune it is, its rank, quality and energy, where it came from,
    /// the syntheses it took part in, and its flaws. A composite rune (made by synthesis) also carries its own
    /// <see cref="composite"/> profile (name, alignments, essence, slot cost). Its maximum energy is derived from the tables
    /// (<see cref="EnergyRules.MaxEnergy"/>), never stored. The same type is a print on a grimoire page (no instance id, no energy).
    /// </summary>
    [Serializable]
    public class RuneInstance
    {
        [Tooltip("Unique in the soul (a GUID, or an id the synthesis engine makes). Empty for a print on a grimoire page.")]
        public string instanceId = "";
        [Tooltip("The rune definition (\"wind\"), or an archetype's result rune / \"composite\" for a rune made by synthesis.")]
        public string runeId = "";
        [Min(1)] public int rank = 1;
        [Range(0f, 100f)] public float quality;
        [Tooltip("Rune Energy it holds (its maximum comes from its rank).")]
        public float energy;
        public RuneOrigin origin = new RuneOrigin();
        [Tooltip("UTC ticks of the absorption.")]
        public long acquiredUtc;
        [Tooltip("The syntheses it took part in (their ids), oldest first.")]
        public List<string> history = new List<string>();
        [Tooltip("Made by a flawed synthesis (see flaws).")]
        public bool flawed;
        [Tooltip("Synthesis flaw ids.")]
        public List<string> flaws = new List<string>();
        [Tooltip("The profile of a rune made by synthesis; inactive for a rune of the database.")]
        public CompositeProfile composite = new CompositeProfile();

        /// <summary>
        /// Is it a rune made by synthesis, with a profile of its own? (An inactive profile, as the serializer writes for every rune,
        /// is not one.)
        /// </summary>
        public bool IsComposite => composite != null && composite.active;

        /// <summary>A deep copy (its origin, lists and composite profile are its own).</summary>
        public RuneInstance Clone()
        {
            var c = (RuneInstance)MemberwiseClone();
            c.origin = origin != null ? origin.Clone() : new RuneOrigin();
            c.history = history != null ? new List<string>(history) : new List<string>();
            c.flaws = flaws != null ? new List<string>(flaws) : new List<string>();
            c.composite = composite != null ? composite.Clone() : new CompositeProfile();
            return c;
        }

        /// <summary>Fills what an older save or a network copy lacks (never changes what is there).</summary>
        public void EnsureLists()
        {
            if (instanceId == null) instanceId = "";
            if (runeId == null) runeId = "";
            if (origin == null) origin = new RuneOrigin();
            origin.EnsureLists();
            if (history == null) history = new List<string>();
            if (flaws == null) flaws = new List<string>();
            if (composite == null) composite = new CompositeProfile();
            composite.EnsureLists();
        }
    }

    /// <summary>Where a rune came from: a drop, an item, a reward, a synthesis, a grimoire... and its source.</summary>
    [Serializable]
    public class RuneOrigin
    {
        [Tooltip("drop, item, reward, synthesis, grimoire... (free text).")]
        public string kind = "";
        [Tooltip("What gave it (an item's name, a synthesis id, a grimoire's author...).")]
        public string source = "";
        [Tooltip("UTC ticks.")]
        public long utc;

        public RuneOrigin Clone() => (RuneOrigin)MemberwiseClone();

        public void EnsureLists()
        {
            if (kind == null) kind = "";
            if (source == null) source = "";
        }
    }

    /// <summary>
    /// What a rune made by synthesis is (design spec 6.1, 13): its own name, alignments (those of its ingredients), essence (the
    /// aspects of the concepts used, usable as the concept "@essence" of a later synthesis), complexity, slot cost, and the formula
    /// that made it. <see cref="active"/> is false for every rune of the database.
    /// </summary>
    [Serializable]
    public class CompositeProfile
    {
        [Tooltip("This rune was made by synthesis and this profile is its nature.")]
        public bool active;
        public string name = "";
        public string description = "";
        public List<string> alignments = new List<string>();
        [Tooltip("Its essence: the aspects of the concepts it was made with.")]
        public List<AspectWeight> aspects = new List<AspectWeight>();
        public string archetypeId = "";
        public string intentionId = "";
        public string effectId = "";
        [Min(1)] public int slotCost = 1;
        [Range(0f, 1f)] public float complexity;
        [Tooltip("The Synthesis Quality Score it was made with.")]
        public float score;
        public string synthesisId = "";
        [Tooltip("The runes it was made from, in order.")]
        public List<SynthesisIngredient> ingredients = new List<SynthesisIngredient>();

        /// <summary>A deep copy (lists, aspects and ingredients are its own).</summary>
        public CompositeProfile Clone()
        {
            var c = (CompositeProfile)MemberwiseClone();
            c.alignments = alignments != null ? new List<string>(alignments) : new List<string>();
            c.aspects = new List<AspectWeight>();
            if (aspects != null)
                foreach (AspectWeight a in aspects)
                    if (a != null) c.aspects.Add(new AspectWeight(a.aspect, a.weight));
            c.ingredients = new List<SynthesisIngredient>();
            if (ingredients != null)
                foreach (SynthesisIngredient i in ingredients)
                    if (i != null) c.ingredients.Add(i.Clone());
            return c;
        }

        /// <summary>Fills what an older save lacks (never changes what is there).</summary>
        public void EnsureLists()
        {
            if (name == null) name = "";
            if (description == null) description = "";
            if (alignments == null) alignments = new List<string>();
            if (aspects == null) aspects = new List<AspectWeight>();
            if (archetypeId == null) archetypeId = "";
            if (intentionId == null) intentionId = "";
            if (effectId == null) effectId = "";
            if (synthesisId == null) synthesisId = "";
            if (ingredients == null) ingredients = new List<SynthesisIngredient>();
        }
    }

    /// <summary>One rune of a synthesis as its result remembers it: which rune, its rank, the concept it gave and the energy it was given.</summary>
    [Serializable]
    public class SynthesisIngredient
    {
        public string runeInstanceId = "";
        public string runeId = "";
        public int rank;
        public string conceptId = "";
        public float energy;

        public SynthesisIngredient Clone() => (SynthesisIngredient)MemberwiseClone();
    }

    /// <summary>The concepts a player has discovered of one rune definition (decision D13: knowledge belongs to the player, not to a rune).</summary>
    [Serializable]
    public class ConceptKnowledge
    {
        [Tooltip("The rune definition (\"wind\").")]
        public string runeId = "";
        [Tooltip("Discovered concept ids (\"wind.pressure\"): permanent.")]
        public List<string> concepts = new List<string>();

        public ConceptKnowledge Clone() => new ConceptKnowledge { runeId = runeId ?? "", concepts = new List<string>(concepts ?? new List<string>()) };

        public void EnsureLists()
        {
            if (runeId == null) runeId = "";
            if (concepts == null) concepts = new List<string>();
        }
    }
}
