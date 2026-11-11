using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>Why a synthesis could not even be attempted: nothing is spent, nothing is risked (design spec 13.4).</summary>
    public enum SynthesisRefusal : byte
    {
        None = 0,
        NoRequest,
        UnknownIntention,
        UnknownEffect,
        TooFewRunes,
        TooManyRunes,
        /// <summary>Rank advancement needs exactly the number of runes the balance says (seven).</summary>
        WrongRuneCount,
        DuplicateRune,
        RuneNotOwned,
        /// <summary>A spell anchors the rune: dismiss the spell first (decision D3).</summary>
        RuneAnchored,
        ConceptRequired,
        UnknownConcept,
        ConceptNotOfRune,
        /// <summary>The concept has not been discovered (design spec 11.2).</summary>
        ConceptNotKnown,
        ConceptRankTooLow,
        /// <summary>Rank advancement needs runes of one rank.</summary>
        MixedRanks,
        /// <summary>The rune cannot rise above its highest rank.</summary>
        RankTooHigh,
        /// <summary>The soul's condition forbids it (design spec 14.4).</summary>
        SoulRestriction,
        NoCapacity,
        SpellbookFull,
        InvalidEnergy,
        /// <summary>The formula may be valid, but a rune lacks the energy it needs: it cannot proceed, with no penalty (design spec 13.4).</summary>
        InsufficientEnergy,
        /// <summary>The synthesis could collapse the soul and the balance forbids that.</summary>
        SoulWouldCollapse,
        /// <summary>Only rune users (Mages) synthesize (class gating).</summary>
        NotRuneUser,
        /// <summary>Not now: in combat, asleep, downed, busy with meditation or a ritual, or not where the rules run.</summary>
        NotNow,
    }

    /// <summary>Where a rune's energy stands against what its place in the formula asks (design spec 11.5).</summary>
    public enum EnergyState : byte
    {
        /// <summary>Below the least it may have: the synthesis cannot proceed.</summary>
        Insufficient = 0,
        Low = 1,
        Optimal = 2,
        High = 3,
        /// <summary>Above what it can safely hold: it interferes.</summary>
        Excessive = 4,
    }

    public enum RiskLevel : byte
    {
        Low = 0,
        Moderate = 1,
        High = 2,
        /// <summary>Destruction of runes is possible.</summary>
        Extreme = 3,
    }

    /// <summary>What a performed synthesis made.</summary>
    public enum SynthesisOutput : byte
    {
        None = 0,
        Spell = 1,
        Rune = 2,
    }

    /// <summary>One rune of a synthesis, in its place: the rune, the concept it contributes and the energy given.</summary>
    [Serializable]
    public class SynthesisSlot
    {
        [UnityEngine.Tooltip("Instance id of the rune in the soul.")]
        public string runeInstanceId = "";
        [UnityEngine.Tooltip("A concept the player has discovered of that rune; \"@essence\" for a composite rune's own nature.")]
        public string conceptId = "";
        [UnityEngine.Tooltip("Energy given from the rune (consumed).")]
        public float energy;

        public SynthesisSlot() { }
        public SynthesisSlot(string runeInstanceId, string conceptId, float energy)
        {
            this.runeInstanceId = runeInstanceId;
            this.conceptId = conceptId;
            this.energy = energy;
        }
    }

    /// <summary>
    /// What the player chooses (design spec 11.1): the purpose and the result, the runes in order with a concept and an
    /// energy each. Plain data; the network layer sends it as JSON.
    /// </summary>
    [Serializable]
    public class SynthesisRequest
    {
        public SynthesisMode mode;
        public string intentionId = "";
        public string effectId = "";
        public List<SynthesisSlot> slots = new List<SynthesisSlot>();
        [UnityEngine.Tooltip("Seeds the chance in the outcome (the server chooses it; a client's is ignored).")]
        public int seed;
        [UnityEngine.Tooltip("A name for the result (empty = the archetype's or a generated one).")]
        public string name = "";

        public SynthesisRequest Clone()
        {
            var c = new SynthesisRequest { mode = mode, intentionId = intentionId, effectId = effectId, seed = seed, name = name };
            if (slots != null)
                foreach (SynthesisSlot s in slots)
                    if (s != null) c.slots.Add(new SynthesisSlot(s.runeInstanceId, s.conceptId, s.energy));
            return c;
        }
    }

    /// <summary>What the soul and the character offer a synthesis: capacity and what the soul's condition allows.</summary>
    public sealed class SynthesisContext
    {
        /// <summary>Soul Rune Capacity (design spec 4).</summary>
        public int capacity = 7;
        public SoulRestrictions soul = SoulRestrictions.None;
        public float integrity = 100f;
        public float maxIntegrity = 100f;
    }

    /// <summary>A rune's place in an evaluated synthesis.</summary>
    public sealed class SlotEvaluation
    {
        public int index;
        public string runeInstanceId = "";
        public string runeId = "";
        public string runeName = "";
        public string conceptId = "";
        public string conceptName = "";
        public int rank;
        public float quality;
        public float available;
        public float energy;
        /// <summary>How much the concept contributes to what the effect needs (0-1).</summary>
        public float relevance;
        /// <summary>The share of the energy this rune ideally gets (sums to 1 over the runes).</summary>
        public float idealShare;
        public float minEnergy;
        public float idealEnergy;
        public float maxSafeEnergy;
        public EnergyState state;
        /// <summary>The credit its energy earns (0-1).</summary>
        public float energyScore;
        public float interference;
        public bool overpowering;
        public bool deadWeight;
        /// <summary>Where in the effect's chain the concept acts (1 = the first stage; 0 = off the chain).</summary>
        public float stage;
        public float overreach;
        public float understanding;
    }

    /// <summary>The factors of an evaluation (design spec 12.1), each 0-1.</summary>
    public struct FactorScores
    {
        public float coverage;
        public float compatibility;
        public float intention;
        public float order;
        public float energy;
        public float execution;

        public float Min => Math.Min(Math.Min(Math.Min(coverage, compatibility), Math.Min(intention, order)), energy);
    }

    /// <summary>What a result is worth: the stats the synthesis earned (the spell compiler of the casting phase uses them).</summary>
    public struct SynthesisStats
    {
        public float power;
        /// <summary>0-100.</summary>
        public float stability;
        public float energyCostMultiplier;
        /// <summary>0-100.</summary>
        public float quality;
        public float controlPenalty;
        public int rank;
    }

    /// <summary>
    /// The full, deterministic judgement of a formula: nothing in it depends on chance. The score is the Synthesis Quality
    /// Score (design spec 12.2) before the roll for faltering; an incoherent formula fails whatever its score.
    /// </summary>
    public sealed class SynthesisEvaluation
    {
        public bool evaluated;
        public SynthesisMode mode;
        public bool pure;
        public int rank;
        public float score;
        public SynthesisTier tier;
        public FactorScores factors;
        /// <summary>Why the formula is fundamentally incoherent (design spec 13.2); empty when it is coherent.</summary>
        public List<string> incoherence = new List<string>();
        public List<SlotEvaluation> slots = new List<SlotEvaluation>();
        public string archetypeId = "";
        public float archetypeFidelity;
        public float synergy;
        public float conflict;
        public float supportCoverage;
        public float intentionBias;
        public float interference;
        public float risk;
        public RiskLevel riskLevel;
        public float failureChance;
        public float averageQuality;
        public float averageComplexity;
        public float effectComplexity;
        /// <summary>The energy the desired effect asks for, relative to a normal effect (1).</summary>
        public float powerDemand = 1f;
        public int primordialRunes;
        public float totalEnergy;
        public float overreach;
        public SynthesisStats stats;
        public List<string> notes = new List<string>();

        public bool Incoherent => incoherence.Count > 0;

        public bool AnyInsufficient
        {
            get
            {
                foreach (SlotEvaluation s in slots) if (s.state == EnergyState.Insufficient) return true;
                return false;
            }
        }
    }

    /// <summary>What a rune needs and has, as the preview shows it (design spec 11.1 step 7).</summary>
    public sealed class SlotRequirement
    {
        public int index;
        public string runeInstanceId = "";
        public string runeName = "";
        public string conceptName = "";
        public float minEnergy;
        public float available;
        public float allocated;
        public float shortfall;
        /// <summary>Insufficient or Excessive always; the others only when the balance lets the preview show them (else Optimal).</summary>
        public EnergyState shown;
    }

    /// <summary>
    /// What the player may know before confirming (design spec 11.1 step 7): whether it can proceed, the energy each rune
    /// needs at least, the soul's requirement, an estimate of the score (a range: the optimum is research), the risk and
    /// whether runes could be destroyed. Never the exact score, the ideal order or the ideal energy.
    /// </summary>
    public sealed class SynthesisPreview
    {
        public SynthesisRefusal refusal;
        public string message = "";
        public SynthesisMode mode;
        public SynthesisOutput output;
        public int resultRank;
        public List<SlotRequirement> requirements = new List<SlotRequirement>();
        public float estimateLow;
        public float estimateHigh;
        public SynthesisTier tierLow;
        public SynthesisTier tierHigh;
        public RiskLevel riskLevel;
        public bool destructionPossible;
        public bool soulCouldCollapse;
        public float failureChance;
        public float soulDamageLow;
        public float soulDamageHigh;
        public string soulCondition = "";
        public List<string> warnings = new List<string>();

        public bool CanAttempt => refusal == SynthesisRefusal.None;
    }

    /// <summary>Soul Damage a synthesis causes (design spec 14.1): the kind (a Soul Damage Kind id) and the amount before the soul's multipliers.</summary>
    public struct SoulDamageEntry
    {
        public string kind;
        public float amount;
        public string source;
    }

    /// <summary>What a performed (or refused) synthesis did.</summary>
    public sealed class SynthesisResult
    {
        public bool performed;
        public SynthesisRefusal refusal;
        public string message = "";
        public SynthesisEvaluation evaluation;
        public string synthesisId = "";
        /// <summary>The score after the roll for faltering.</summary>
        public float score;
        public SynthesisTier tier;
        public bool faltered;
        public bool incoherent;
        public SynthesisOutput output;
        public SpellRecord spell;
        public RuneInstance rune;
        public string name = "";
        public List<string> flaws = new List<string>();
        public string severity = "";
        public bool runesDestroyed;
        public float energyConsumed;
        public float energyLost;
        public List<string> destroyedRunes = new List<string>();
        public List<string> lostSpells = new List<string>();
        public List<SoulDamageEntry> soulDamage = new List<SoulDamageEntry>();
        public SynthesisStats stats;

        public bool Succeeded => performed && (tier == SynthesisTier.Perfect || tier == SynthesisTier.Successful || tier == SynthesisTier.Flawed);
    }
}
