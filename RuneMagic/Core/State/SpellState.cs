using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A spell made by synthesis (design spec 11, 16), as the soul keeps it: what it was made for, the stats the synthesis earned,
    /// its flaws, the formula (<see cref="ingredients"/>) and the runes it anchors (decision D3: they stay in the soul, its casts draw
    /// their energy, and they cannot be used up or released until the spell is dismissed). <see cref="SpellCompiler"/> turns it into
    /// something to cast. Nothing here is a player setting (the Spelling System is the developer's, design spec 16.5).
    /// </summary>
    [Serializable]
    public class SpellRecord
    {
        [Tooltip("Unique in the soul (the synthesis id, made unique).")]
        public string spellId = "";
        public string name = "";
        public string intentionId = "";
        public string effectId = "";
        [Tooltip("The archetype the formula matched (empty: a custom formula).")]
        public string archetypeId = "";
        [Min(1)] public int rank = 1;
        [Tooltip("The synthesis tier it was made with (SynthesisTier as a number: 1 flawed, 2 successful, 3 perfect).")]
        public int tier;
        [Tooltip("The Synthesis Quality Score (0-100).")]
        public float score;
        [Tooltip("0-100.")]
        public float quality;
        public float power;
        [Tooltip("0-100: the lower, the likelier a cast misfires.")]
        public float stability;
        [Tooltip("Scales the Rune Energy a cast costs (flaws raise it).")]
        public float energyCostMultiplier = 1f;
        [Tooltip("0-1: longer casts, harder to aim (flaws).")]
        public float controlPenalty;
        [Tooltip("0-1: the effect's complexity (cost, strain, cast time).")]
        public float complexity;
        [Tooltip("The energy the synthesis consumed.")]
        public float investedEnergy;
        public bool flawed;
        [Tooltip("Synthesis flaw ids.")]
        public List<string> flaws = new List<string>();
        public List<string> sideEffects = new List<string>();
        [Tooltip("Instance ids of the runes it anchors (decision D3).")]
        public List<string> anchors = new List<string>();
        [Tooltip("The runes it was made from, in order.")]
        public List<SynthesisIngredient> ingredients = new List<SynthesisIngredient>();
        public string synthesisId = "";
        [Tooltip("UTC ticks.")]
        public long createdUtc;

        /// <summary>A deep copy (its lists and ingredients are its own).</summary>
        public SpellRecord Clone()
        {
            var c = (SpellRecord)MemberwiseClone();
            c.flaws = flaws != null ? new List<string>(flaws) : new List<string>();
            c.sideEffects = sideEffects != null ? new List<string>(sideEffects) : new List<string>();
            c.anchors = anchors != null ? new List<string>(anchors) : new List<string>();
            c.ingredients = new List<SynthesisIngredient>();
            if (ingredients != null)
                foreach (SynthesisIngredient i in ingredients)
                    if (i != null) c.ingredients.Add(i.Clone());
            return c;
        }

        /// <summary>Fills what an older save or a network copy lacks (never changes what is there).</summary>
        public void EnsureLists()
        {
            if (spellId == null) spellId = "";
            if (name == null) name = "";
            if (intentionId == null) intentionId = "";
            if (effectId == null) effectId = "";
            if (archetypeId == null) archetypeId = "";
            if (synthesisId == null) synthesisId = "";
            if (flaws == null) flaws = new List<string>();
            if (sideEffects == null) sideEffects = new List<string>();
            if (anchors == null) anchors = new List<string>();
            if (ingredients == null) ingredients = new List<SynthesisIngredient>();
        }
    }

    /// <summary>A spell on an ability key slot (design spec 16.4). A slot holds at most one spell, a spell sits in at most one slot (<see cref="SpellSlotRules"/>).</summary>
    [Serializable]
    public class SpellSlotAssignment
    {
        [Tooltip("The ability key slot index (0 = the first key).")]
        public int slot;
        public string spellId = "";

        public SpellSlotAssignment() { }

        public SpellSlotAssignment(int slot, string spellId)
        {
            this.slot = slot;
            this.spellId = spellId ?? "";
        }

        public SpellSlotAssignment Clone() => new SpellSlotAssignment(slot, spellId);

        public void EnsureLists()
        {
            if (spellId == null) spellId = "";
        }
    }

    /// <summary>One synthesis attempt in the soul's research log (<see cref="SynthesisEngine"/> keeps the last ones): what was tried and how it went.</summary>
    [Serializable]
    public class SynthesisLogEntry
    {
        [Tooltip("The synthesis id.")]
        public string id = "";
        [Tooltip("UTC ticks.")]
        public long utc;
        [Tooltip("SynthesisMode as a number.")]
        public int mode;
        public string intentionId = "";
        public string effectId = "";
        public int runeCount;
        public float score;
        [Tooltip("perfect, successful, flawed, failed[:severity].")]
        public string outcome = "";
        public string resultName = "";
        [Tooltip("The hint the attempt gave (never numbers).")]
        public string note = "";

        public SynthesisLogEntry Clone() => (SynthesisLogEntry)MemberwiseClone();

        public void EnsureLists()
        {
            if (id == null) id = "";
            if (intentionId == null) intentionId = "";
            if (effectId == null) effectId = "";
            if (outcome == null) outcome = "";
            if (resultName == null) resultName = "";
            if (note == null) note = "";
        }
    }
}
