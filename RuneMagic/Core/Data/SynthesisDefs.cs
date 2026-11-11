using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Rune synthesis (design spec 6.1, 11-13, 18-19): the data that tunes the engine. Nothing here is a recipe: archetypes
    // are named reference formulas that only name a result and refine its score when a formula resembles them; every
    // combination, known or not, is judged by the same evaluation of aspects, order and energy.

    /// <summary>What a synthesis makes.</summary>
    public enum SynthesisMode : byte
    {
        /// <summary>A spell (design spec 11): any number of runes; the runes stay in the soul, anchored by the spell.</summary>
        Spell = 0,
        /// <summary>A new composite rune: the input runes are consumed (they free their slots), the rune takes one.</summary>
        CompositeRune = 1,
        /// <summary>Rank advancement (design spec 6.1): exactly seven runes of the same rank become one rune of the next rank.</summary>
        RankAdvancement = 2,
    }

    /// <summary>Which synthesis modes an archetype can name.</summary>
    public enum ArchetypeMode : byte
    {
        Any = 0,
        Spell = 1,
        Rune = 2,
    }

    /// <summary>What makes a flawed result flawed (design spec 13.1).</summary>
    public enum FlawCause : byte
    {
        /// <summary>The concepts only partly provide what the effect needs ("insufficient Concept understanding").</summary>
        Coverage = 0,
        /// <summary>The concepts work against each other ("weak Concept compatibility").</summary>
        Compatibility = 1,
        /// <summary>The intention only partly supports the effect.</summary>
        Intention = 2,
        /// <summary>The runes are in a poor order ("poor ordering").</summary>
        Order = 3,
        /// <summary>The energy was spread badly ("incorrect energy ratios", "inadequate stabilization").</summary>
        EnergyImbalance = 4,
        /// <summary>A rune was given too much energy ("excessive energy in one or more Runes").</summary>
        EnergyExcess = 5,
        /// <summary>Poor runes, concepts beyond the runes' rank, or poor understanding.</summary>
        Execution = 6,
    }

    /// <summary>One point of a piecewise-linear curve (score 0-100 on x).</summary>
    [Serializable]
    public class CurvePoint
    {
        public float x;
        public float y;

        public CurvePoint() { }
        public CurvePoint(float x, float y) { this.x = x; this.y = y; }

        /// <summary>The value of the curve at <paramref name="at"/>: linear between points, flat beyond the ends.</summary>
        public static float Evaluate(IList<CurvePoint> curve, float at)
        {
            if (curve == null || curve.Count == 0) return 1f;
            CurvePoint prev = null;
            float result = curve[0].y;
            bool any = false;
            foreach (CurvePoint p in curve)
            {
                if (p == null) continue;
                if (!any) { result = p.y; any = true; }
                if (at <= p.x)
                {
                    if (prev == null || p.x <= prev.x) return p.y;
                    return prev.y + (p.y - prev.y) * (at - prev.x) / (p.x - prev.x);
                }
                prev = p;
                result = p.y;
            }
            return result;
        }
    }

    /// <summary>
    /// One stage of an archetype: a rune (and optionally a concept) used a number of times, with the energy weight each of
    /// its runes ideally gets (design spec 18: 3 Wind, 1 Ash, 3 Vibration).
    /// </summary>
    [Serializable]
    public class ArchetypeStage
    {
        [Tooltip("Rune id (wind, ash...). For a composite result of an earlier synthesis, its rune id.")]
        public string runeId = "";
        [Tooltip("Concept id the rune should use (empty = any concept of the rune).")]
        public string conceptId = "";
        [Tooltip("How many runes of this kind the ideal formula has.")]
        [Min(1)] public int count = 1;
        [Tooltip("Relative energy each rune of this stage ideally gets (the ideal ratio between stages).")]
        [Min(0.05f)] public float energyWeight = 1f;

        public ArchetypeStage() { }
        public ArchetypeStage(string runeId, string conceptId, int count, float energyWeight = 1f)
        {
            this.runeId = runeId;
            this.conceptId = conceptId;
            this.count = count;
            this.energyWeight = energyWeight;
        }
    }

    /// <summary>
    /// A named reference formula (design spec 18, 19): the Pyroclastic Resonance adaptation, the original combinations. It is
    /// <b>not a recipe</b>: a synthesis that does not match is judged by the general evaluation, and one that does match is
    /// still judged by it. A matching formula (same effect and intention, mostly the same runes) additionally takes the
    /// archetype's name, its ideal energy ratios, and a fidelity factor: the ideal counts and order score best, near
    /// variants score less (3 Wind + 1 Ash + 3 Vibration flawless, 2 + 2 + 3 flawed).
    /// </summary>
    [Serializable]
    public class OutcomeArchetype
    {
        [Tooltip("Stable id (pyroclastic_resonance, gravity_well...).")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Intention of the formula (id).")]
        public string intention = "";
        [Tooltip("Desired effect of the formula (id).")]
        public string effect = "";
        [Tooltip("Which synthesis modes it names (Any, Spell or Rune).")]
        public ArchetypeMode mode = ArchetypeMode.Any;
        [Tooltip("The ideal formula, in order.")]
        public List<ArchetypeStage> stages = new List<ArchetypeStage>();
        [Tooltip("The order of the stages is part of the ideal formula (off: any order matches).")]
        public bool orderMatters = true;
        [Tooltip("Share of the score the fidelity to the ideal counts and order decides (0 = none, 1 = all).")]
        [Range(0f, 1f)] public float strictness = 0.8f;
        [Tooltip("Counts within this share of the ideal total still score (0.5 = a difference of half the ideal total scores 0).")]
        [Range(0.1f, 2f)] public float countTolerance = 0.5f;
        [Tooltip("Share of the ideal runes a formula needs to be recognised as this archetype.")]
        [Range(0.1f, 1f)] public float matchThreshold = 0.6f;

        [Header("Result")]
        [Tooltip("The name of a rune made from it (empty = the display name).")]
        public string resultName = "";
        [Tooltip("Rune id of the composite rune it makes (saves and the window use it; empty = a generated composite).")]
        public string resultRuneId = "";
        [TextArea(1, 2)] public string resultProperties = "";
        [Tooltip("Soul slots the composite rune occupies.")]
        [Min(1)] public int resultSlotCost = 1;
        [Tooltip("Free keywords for tools.")]
        public List<string> tags = new List<string>();

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
        public string RuneName => string.IsNullOrEmpty(resultName) ? Name : resultName;

        /// <summary>Total runes of the ideal formula.</summary>
        public int TotalCount
        {
            get
            {
                int n = 0;
                foreach (ArchetypeStage s in stages) if (s != null) n += Mathf.Max(1, s.count);
                return n;
            }
        }
    }

    /// <summary>
    /// A way a flawed synthesis can go wrong (design spec 13.1): reduced power, increased energy consumption, reduced
    /// stability, a result that is hard to control, unintended secondary effects. Picked by what was wrong with the
    /// formula; its strength scales with how low the score is inside the Flawed band.
    /// </summary>
    [Serializable]
    public class SynthesisFlawDef
    {
        public string id = "";
        public string displayName = "";
        [TextArea(1, 2)] public string description = "";
        [Tooltip("What makes a result carry this flaw.")]
        public FlawCause cause = FlawCause.Coverage;
        [Tooltip("Chance weight among the flaws of the same cause.")]
        [Min(0f)] public float weight = 1f;
        [Header("At full severity (the score at the bottom of the Flawed band)")]
        [Range(0.1f, 1.5f)] public float powerMultiplier = 1f;
        [Range(0.1f, 1.5f)] public float stabilityMultiplier = 1f;
        [Range(0.5f, 3f)] public float costMultiplier = 1f;
        [Tooltip("Added to the spell's control difficulty (harder to control; the Spelling System uses it).")]
        [Range(0f, 1f)] public float controlPenalty = 0f;
        [Tooltip("An unintended secondary effect (a tag the spell compiler understands; empty = none).")]
        public string sideEffect = "";

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>
    /// The numbers of the synthesis engine. Every value is a proposal (the spec gives rules, not numbers): the energy a
    /// formula asks for, how forgiving the optimum is, the weights of the evaluation factors, the risk, the outcome curves,
    /// the soul damage. Held by <see cref="RuneBalance"/>; tune it on the Rune Magic Database.
    /// </summary>
    [Serializable]
    public class SynthesisBalance
    {
        [Header("Runes")]
        [Tooltip("Fewest runes of a custom spell.")]
        [Min(1)] public int minRunes = 1;
        [Tooltip("Most runes of a custom spell or composite rune.")]
        [Min(1)] public int maxRunes = 12;
        [Tooltip("Fewest runes of a composite rune (combining runes is how slots are freed).")]
        [Min(2)] public int minRunesComposite = 2;
        [Tooltip("Most spells a soul can hold (dismiss one to make another).")]
        [Min(1)] public int maxSpells = 40;
        [Tooltip("The same rune can anchor several spells (they share its energy). Off: a rune anchors one spell.")]
        public bool runesMayAnchorSeveralSpells = true;
        [Tooltip("Highest rank a composite rune can reach (a rune of the database stops at its own highest rank).")]
        [Min(1)] public int maxRuneRank = 5;
        [Tooltip("How many synthesis attempts the saved research log keeps.")]
        [Range(0, 200)] public int logLength = 30;
        [Tooltip("A synthesis may be made in combat (off: like releasing a rune, it needs a moment of calm).")]
        public bool allowInCombat = false;

        [Header("Energy asked (a share of each rune's maximum energy, for a normal effect)")]
        [Range(0.01f, 1f)] public float energyFractionSpell = 0.15f;
        [Range(0.01f, 1f)] public float energyFractionComposite = 0.30f;
        [Range(0.01f, 1f)] public float energyFractionAdvancement = 0.40f;
        [Tooltip("A rune that gets less than this share of its ideal energy cannot take part: the synthesis cannot proceed (no penalty).")]
        [Range(0f, 1f)] public float minEnergyShare = 0.5f;
        [Tooltip("Energy within this share of the ideal is optimal (more or less is worse).")]
        [Range(0.02f, 0.9f)] public float optimalTolerance = 0.25f;
        [Tooltip("Energy above this multiple of the ideal is excessive: it interferes (overpowers the others, destabilizes).")]
        [Range(1.1f, 4f)] public float maxSafeShare = 1.75f;
        [Tooltip("Complex concepts need precise control: their optimal band is narrower and their safe maximum lower by up to this share.")]
        [Range(0f, 0.9f)] public float complexityNarrowing = 0.5f;
        [Tooltip("Complex concepts ask for more energy (the ideal is × (1 - this/2 + this × complexity)).")]
        [Range(0f, 1f)] public float complexityEnergy = 0.5f;
        [Tooltip("The least share of the energy a rune that adds nothing to the formula is ideally given.")]
        [Range(0f, 0.3f)] public float irrelevantShareFloor = 0.05f;
        [Tooltip("How much interference (excess energy) lowers the energy factor (0-1).")]
        [Range(0f, 1f)] public float interferenceWeight = 0.5f;

        [Header("Evaluation weights (design spec 12.1)")]
        [Min(0f)] public float weightCoverage = 0.28f;
        [Min(0f)] public float weightCompatibility = 0.14f;
        [Min(0f)] public float weightIntention = 0.10f;
        [Min(0f)] public float weightOrder = 0.16f;
        [Min(0f)] public float weightEnergy = 0.22f;
        [Min(0f)] public float weightExecution = 0.10f;
        [Tooltip("How much the weakest factor drags the score (0 = a plain weighted mean, 1 = the weakest factor decides).")]
        [Range(0f, 1f)] public float minBlend = 0.4f;
        [Tooltip("The desired effect is not producible (the synthesis fails completely) when a requirement is met by less than this share.")]
        [Range(0f, 0.9f)] public float hardCoverageFloor = 0.2f;
        [Tooltip("A fundamentally wrong sequence (order score at or below this) fails completely, when the order matters.")]
        [Range(0f, 0.5f)] public float orderIncoherentBelow = 0.08f;
        [Tooltip("The order only matters when the runes' places in the effect's chain are spread at least this far (in stages).")]
        [Min(0f)] public float orderMinSpread = 0.75f;
        [Tooltip("A rune adding less than this share of an average rune's relevance counts as dead weight.")]
        [Range(0f, 1f)] public float deadWeightRelevance = 0.15f;
        [Tooltip("A pair of concepts with this much of each aspect triggers an aspect rule marked incoherent.")]
        [Range(0f, 1f)] public float incoherentAspectMin = 0.5f;

        [Header("Execution (rank, quality and understanding)")]
        [Tooltip("Average rune quality (0-100) at which quality stops costing score.")]
        [Range(1f, 100f)] public float fullCreditQuality = 55f;
        [Tooltip("Share of a rune's concepts the player knows at which understanding stops costing score.")]
        [Range(0.05f, 1f)] public float fullCreditKnowledge = 0.5f;
        [Tooltip("Score of the execution factor lost per rank of difference between the highest and lowest rune.")]
        [Range(0f, 0.5f)] public float rankHarmonyLoss = 0.25f;
        [Tooltip("The concept complexity a rank-1 rune handles safely (more complex concepts overreach).")]
        [Range(0f, 1f)] public float conceptCapacityBase = 0.5f;
        [Tooltip("Complexity added to the safe capacity per rank above 1.")]
        [Range(0f, 0.5f)] public float conceptCapacityPerRank = 0.12f;

        [Header("Preview")]
        [Tooltip("The preview's estimate is this many points wide on each side (hides the exact score: the optimum is research).")]
        [Range(0f, 50f)] public float previewFuzz = 18f;
        [Tooltip("The preview tells whether each rune's energy is low, optimal or high (off: only insufficient and excessive are shown).")]
        public bool previewShowsEnergyStates = false;
        [Tooltip("Refuse a synthesis that could collapse the soul (the preview always warns).")]
        public bool refuseWhenSoulCouldCollapse = false;

        [Header("Risk (design spec 13.3)")]
        [Range(0f, 1f)] public float riskEffectComplexity = 0.30f;
        [Range(0f, 1f)] public float riskConceptComplexity = 0.20f;
        [Range(0f, 1f)] public float riskPrimordial = 0.25f;
        [Range(0f, 1f)] public float riskInterference = 0.15f;
        [Range(0f, 1f)] public float riskRuneCount = 0.10f;
        [Tooltip("Risk (0-1) at which the preview calls it High; Extreme is destruction territory.")]
        [Range(0f, 1f)] public float riskHigh = 0.5f;

        [Header("Instability")]
        [Tooltip("Chance a synthesis falters, per point of risk, on top of the soul condition's own chance.")]
        [Range(0f, 1f)] public float riskFailureChance = 0.10f;
        [Tooltip("Chance a synthesis falters per point of interference (energy beyond what the runes can hold): excessive energy " +
                 "can make a synthesis fail (design spec 11.5).")]
        [Range(0f, 1f)] public float interferenceFailureChance = 0.35f;
        [Tooltip("A faltering synthesis loses this many points of score (random between the two).")]
        [Min(0f)] public float falterMin = 8f;
        [Min(0f)] public float falterMax = 25f;

        [Header("Failure (design spec 13.3)")]
        [Tooltip("Catastrophic failure: each input rune is destroyed with this chance (at least one is).")]
        [Range(0f, 1f)] public float destroyChancePerRune = 0.6f;
        [Tooltip("A fundamentally incoherent formula fails whatever its score; the severity of its failure is judged by its score × " +
                 "this (at most just under the Flawed threshold): the worse the formula, the harsher the penalty.")]
        [Range(0f, 1f)] public float incoherentScoreFactor = 0.5f;

        [Header("Outcome curves (score 0-100)")]
        [Tooltip("Power of the result against the score.")]
        public List<CurvePoint> powerCurve = new List<CurvePoint>();
        [Tooltip("Stability of the result against the score (0-1).")]
        public List<CurvePoint> stabilityCurve = new List<CurvePoint>();
        [Tooltip("Energy cost multiplier of the result against the score (lower is better).")]
        public List<CurvePoint> costCurve = new List<CurvePoint>();
        [Tooltip("Power of a rank-1 spell at score 100 (the spell compiler scales its effect with it).")]
        [Min(1f)] public float baseSpellPower = 100f;
        [Tooltip("Power multiplier per rank above 1.")]
        [Min(1f)] public float powerPerRank = 1.6f;
        [Tooltip("Share of the power the synergy, supporting aspects and intention bias can add together.")]
        [Range(0f, 1f)] public float bonusPower = 0.5f;
        [Tooltip("Power added per rune beyond the first (a share of the base): more runes carry more raw magic, at the price of risk and Soul Damage.")]
        [Range(0f, 1f)] public float powerPerRune = 0.10f;
        [Tooltip("The ideal energy of a rune never exceeds this share of its maximum (the optimum must always be reachable).")]
        [Range(0.3f, 1f)] public float maxIdealShare = 0.9f;
        [Tooltip("Energy a new rune from a rank advancement or composite starts with, as a share of its maximum, at score 100.")]
        [Range(0f, 1f)] public float resultEnergyShare = 0.10f;

        [Header("Soul damage (design spec 14.1)")]
        [Min(0f)] public float soulDamageBase = 6f;
        [Tooltip("Added per extra rune (a share of the base).")]
        [Min(0f)] public float soulDamagePerRune = 0.08f;
        [Tooltip("Added per rank above 1 (a share of the base).")]
        [Min(0f)] public float soulDamagePerRank = 0.25f;
        [Tooltip("Rank advancement multiplies the damage.")]
        [Min(0f)] public float soulDamageAdvancement = 2f;
        [Min(0f)] public float soulDamagePerfect = 0.5f;
        [Min(0f)] public float soulDamageSuccessful = 0.9f;
        [Min(0f)] public float soulDamageFlawed = 1.3f;
        [Min(0f)] public float soulDamageFailure = 1.6f;
        [Tooltip("Risk raises the damage by up to this share.")]
        [Min(0f)] public float soulDamageRisk = 0.5f;
        [Tooltip("Soul Damage of kind 'primordial' per Primordial rune used.")]
        [Min(0f)] public float primordialDamage = 12f;
        [Tooltip("Soul Damage of kind 'concept_overreach' per point of complexity a concept exceeds its rune's rank.")]
        [Min(0f)] public float overreachDamage = 30f;

        /// <summary>Energy share asked of a rune for <paramref name="mode"/>.</summary>
        public float EnergyFraction(SynthesisMode mode) =>
            mode == SynthesisMode.RankAdvancement ? energyFractionAdvancement : mode == SynthesisMode.CompositeRune ? energyFractionComposite : energyFractionSpell;

        /// <summary>Setup problems of the engine's numbers.</summary>
        public void Validate(List<string> errors, List<string> warnings)
        {
            float sum = weightCoverage + weightCompatibility + weightIntention + weightOrder + weightEnergy + weightExecution;
            if (sum <= 0f) errors.Add("Synthesis: every evaluation weight is 0.");
            else if (Mathf.Abs(sum - 1f) > 0.2f) warnings.Add($"Synthesis: the evaluation weights add up to {sum:0.##} (they are used as shares of their total).");
            if (maxRunes < minRunes) errors.Add("Synthesis: Max Runes is below Min Runes.");
            if (maxRunes > RuneNetLimits.MaxSynthesisSlots)
                warnings.Add($"Synthesis: Max Runes ({maxRunes}) is above the {RuneNetLimits.MaxSynthesisSlots} runes a network request can carry: players on a client could not send such a synthesis.");
            if (minRunesComposite > maxRunes) errors.Add("Synthesis: Min Runes Composite is above Max Runes.");
            if (minEnergyShare >= 1f) errors.Add("Synthesis: Min Energy Share must be below 1 (the ideal energy would be the least).");
            if (1f - optimalTolerance < minEnergyShare) warnings.Add("Synthesis: the optimal band starts below the least energy a rune may have.");
            if (maxSafeShare < 1f + optimalTolerance) errors.Add("Synthesis: Max Safe Share must be above the optimal band (1 + Optimal Tolerance).");
            if (falterMax < falterMin) errors.Add("Synthesis: Falter Max is below Falter Min.");
            if (powerCurve == null || powerCurve.Count == 0) errors.Add("Synthesis: the power curve is empty.");
            if (stabilityCurve == null || stabilityCurve.Count == 0) errors.Add("Synthesis: the stability curve is empty.");
            if (costCurve == null || costCurve.Count == 0) errors.Add("Synthesis: the cost curve is empty.");
            CheckCurve(powerCurve, "power", errors);
            CheckCurve(stabilityCurve, "stability", errors);
            CheckCurve(costCurve, "cost", errors);
        }

        private static void CheckCurve(List<CurvePoint> curve, string name, List<string> errors)
        {
            if (curve == null) return;
            for (int i = 1; i < curve.Count; i++)
                if (curve[i] != null && curve[i - 1] != null && curve[i].x < curve[i - 1].x)
                    errors.Add($"Synthesis: the {name} curve's points are not in order of score.");
        }
    }
}
