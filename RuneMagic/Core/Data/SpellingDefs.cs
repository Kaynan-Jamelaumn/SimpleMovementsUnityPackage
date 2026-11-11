using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // The Spelling System's data (design spec 16.5, directive 9): the key-sequence mechanic some spells use. It is configured by the
    // developer / server in the database, never by the player: a global mode (All / Selected / None), rules that mark which spells
    // use it (by form, desired effect or archetype) and how hard it is, and the numbers every spell's difficulty is generated from.
    // Every value is a proposal, held as data.

    /// <summary>Which spells use the Spelling System (the developer's / server's choice, never the player's).</summary>
    public enum SpellingMode
    {
        /// <summary>No spell uses it: casting works like a normal ability.</summary>
        None = 0,
        /// <summary>Only spells marked by a rule use it.</summary>
        Selected = 1,
        /// <summary>Every spell must use it.</summary>
        All = 2,
    }

    /// <summary>What a wrong, missed or late key does (design spec 16.5 failure models).</summary>
    public enum SpellingFailureModel
    {
        /// <summary>One mistake fails the spell.</summary>
        Strict = 0,
        /// <summary>Each mistake costs power, stability and time; too many still fail it.</summary>
        Lenient = 1,
    }

    /// <summary>When mistakes cost Soul Damage (design spec 16.5: configurable per spell, never automatic).</summary>
    public enum SpellingDamageMode
    {
        None = 0,
        /// <summary>The same amount for every mistake.</summary>
        FlatPerMistake = 1,
        /// <summary>Each further mistake costs more than the one before.</summary>
        ScalingPerMistake = 2,
        /// <summary>One amount, only when the sequence fails.</summary>
        OnceOnFailure = 3,
    }

    /// <summary>A rule's choice about using the Spelling System.</summary>
    public enum SpellingUse
    {
        Inherit = 0,
        Yes = 1,
        No = 2,
    }

    /// <summary>A rule's choice of failure model.</summary>
    public enum SpellingFailureChoice
    {
        Inherit = 0,
        Strict = 1,
        Lenient = 2,
    }

    /// <summary>A rule's choice of when mistakes cost Soul Damage.</summary>
    public enum SpellingDamageChoice
    {
        Inherit = 0,
        None = 1,
        FlatPerMistake = 2,
        ScalingPerMistake = 3,
        OnceOnFailure = 4,
    }

    /// <summary>What a spelling rule applies to.</summary>
    public enum SpellingScope
    {
        /// <summary>A spell form (<see cref="SpellFormDef"/>).</summary>
        Form = 0,
        /// <summary>A desired effect.</summary>
        Effect = 1,
        /// <summary>An outcome archetype (a recognised formula such as Pyroclastic Resonance).</summary>
        Archetype = 2,
    }

    /// <summary>How much one evaluation term counts in the performance score (see <c>SpellingEvaluator.Terms</c>).</summary>
    [Serializable]
    public class SpellingWeight
    {
        [Tooltip("The term's id: accuracy, speed, precision, rhythm (more can be registered in code).")]
        public string term = "";
        [Min(0f)] public float weight = 1f;

        public SpellingWeight() { }
        public SpellingWeight(string term, float weight) { this.term = term; this.weight = weight; }
    }

    /// <summary>
    /// Marks spells that use the Spelling System and tunes how hard it is for them: the data-table form of the spec's per-spell
    /// flag and parameters (spells are synthesized, so they are marked by what they are: their form, their desired effect or the
    /// archetype they were recognised as). The most specific rule wins field by field: archetype, then effect, then form.
    /// A negative number (or Inherit) leaves the value to the generated one.
    /// </summary>
    [Serializable]
    public class SpellingRule
    {
        public SpellingScope scope = SpellingScope.Form;
        [Tooltip("The id of the form, desired effect or archetype this rule applies to.")]
        public string id = "";
        [Tooltip("Selected mode: Yes makes these spells use the Spelling System (All mode: every spell does, None mode: none).")]
        public SpellingUse use = SpellingUse.Inherit;

        [Header("Difficulty (−1 = generated from the spell's rank, power and complexity)")]
        [Tooltip("Exactly this many letters (before the soul's condition and the pool allow fewer or more).")]
        public int letters = -1;
        [Tooltip("Added to the generated number of letters.")]
        public int lettersDelta = 0;
        [Tooltip("The time window of every key, in seconds.")]
        public float keyWindow = -1f;
        [Tooltip("One time window per key, in seconds (key 1, key 2...): different windows for different keys. Empty = generated.")]
        public List<float> keyWindows = new List<float>();
        [Tooltip("Multiplies every window (a rule can make its spells easier or harder than their numbers say).")]
        [Min(0.1f)] public float windowMultiplier = 1f;
        [Tooltip("Total time limit for the sequence, in seconds (−1 = a share of the windows' sum).")]
        public float totalLimit = -1f;

        [Header("Rhythm")]
        public SpellingUse rhythm = SpellingUse.Inherit;
        [Tooltip("Seconds between beats (−1 = the balance's).")]
        public float rhythmTempo = -1f;
        [Tooltip("How far from a beat a key may be pressed, in seconds (−1 = the balance's).")]
        public float rhythmTolerance = -1f;

        [Header("Evaluation")]
        public SpellingFailureChoice failureModel = SpellingFailureChoice.Inherit;
        [Tooltip("Lenient: mistakes beyond this many fail the spell (−1 = generated).")]
        public int maxMistakes = -1;
        [Tooltip("Finishing within this share of the time limit earns the speed bonus (−1 = the balance's).")]
        public float speedBonusThreshold = -1f;

        [Header("Soul Damage on mistakes (never automatic)")]
        public SpellingDamageChoice damageMode = SpellingDamageChoice.Inherit;
        [Tooltip("Soul Damage per mistake (flat or scaling) or once (on failure); −1 = the balance's.")]
        public float damageAmount = -1f;

        [Tooltip("Free keywords for tools and rules.")]
        public List<string> tags = new List<string>();
    }

    /// <summary>
    /// The numbers of the Spelling System (design spec 16.5): the global mode, which keys may be used, how a spell's difficulty is
    /// generated from its rank, power and complexity, how the sequence is scored, what mistakes cost and what the server accepts.
    /// Held by <see cref="RuneBalance"/>; tune it on the Rune Magic Database.
    /// </summary>
    [Serializable]
    public class SpellingBalance
    {
        [Header("Who uses it (set by the developer / server, never by the player)")]
        [Tooltip("None: no spell uses the Spelling System. Selected: only spells a rule marks. All: every spell does.")]
        public SpellingMode mode = SpellingMode.Selected;
        [Tooltip("The default failure model (a rule can choose its own).")]
        public SpellingFailureModel failureModel = SpellingFailureModel.Lenient;
        [Tooltip("A player without a keyboard (a gamepad only) can still cast spelled spells: the Spelling System needs letter keys, so the cast is made as if the sequence had been played with the most mistakes the spell allows (weaker and slower, no bonus, no Soul Damage for the mistakes). Off: the server refuses spelled spells to such players.")]
        public bool skipWithoutKeyboard = true;

        [Header("Keys (names of the Input System's Key enum: 'A', 'Digit1', 'Space'...)")]
        [Tooltip("The keys a sequence may ask for.")]
        public List<string> candidateKeys = new List<string>();
        [Tooltip("Keys the game reads outside the Input System's actions (digits, quick slots, Y/N prompts, T, F-keys, Esc, Tab, Enter, Space): never asked for.")]
        public List<string> reservedKeys = new List<string>();
        [Tooltip("Actions whose keys are the movement keys (their keys, as the player has bound them now, are never used while the caster can move). " +
                 "Any action with a keyboard composite (2D Vector) counts as well.")]
        public List<string> movementActions = new List<string>();
        [Tooltip("Never ask for a key that any gameplay action uses (ability keys, interact, inventory...): pressing it would do that too.")]
        public bool excludeGameplayKeys = true;
        [Tooltip("A spell that roots the caster may ask for movement keys (never for ability keys).")]
        public bool allowMovementKeysWhenRooted = true;
        [Tooltip("With fewer keys than this the Spelling System is skipped (a warning says why).")]
        [Min(2)] public int minPoolSize = 3;

        [Header("Letters (spec: scaled by rank, power and concept complexity)")]
        [Min(1)] public int baseLetters = 3;
        [Tooltip("Added per spell rank above 1.")]
        [Min(0f)] public float lettersPerRank = 0.5f;
        [Tooltip("Added per +100% of power above the reference power of the Spell Balance.")]
        [Min(0f)] public float lettersPerPower = 1f;
        [Tooltip("Added at complexity 1 (× the spell's complexity 0-1).")]
        [Min(0f)] public float lettersPerComplexity = 2f;
        [Min(1)] public int minLetters = 2;
        [Min(1)] public int maxLetters = 10;

        [Header("Time (seconds)")]
        [Min(0.2f)] public float baseKeyWindow = 1.6f;
        [Tooltip("Taken off each window at complexity 1 (× the spell's complexity).")]
        [Min(0f)] public float windowPerComplexity = 0.6f;
        [Min(0.1f)] public float minKeyWindow = 0.5f;
        [Tooltip("Added to the first key's window (reading the letters takes a moment).")]
        [Min(0f)] public float firstKeyExtra = 0.5f;
        [Tooltip("How much the generated windows differ from key to key (0 = all alike, 0.25 = ±25%).")]
        [Range(0f, 0.9f)] public float windowVariation = 0.25f;
        [Tooltip("The total time limit as a share of the sum of the windows (1 = every key may take all of its window).")]
        [Range(0.2f, 3f)] public float totalLimitFactor = 0.9f;

        [Header("Rhythm (when a rule asks for it)")]
        [Min(0.1f)] public float rhythmTempo = 0.6f;
        [Min(0.02f)] public float rhythmTolerance = 0.25f;

        [Header("Score (accuracy, speed, precision, rhythm)")]
        public List<SpellingWeight> weights = new List<SpellingWeight>();
        [Tooltip("Finishing within this share of the time limit (and with few enough mistakes) earns the speed bonus.")]
        [Range(0.05f, 1f)] public float speedBonusThreshold = 0.55f;
        [Tooltip("Most mistakes a sequence may have and still earn the speed bonus.")]
        [Min(0)] public int speedBonusMaxMistakes = 0;
        [Tooltip("Speed bonus: more power (0.1 = +10%), lower energy cost (0.1 = −10%), less Soul Strain (0.2 = −20%) and more stability (points).")]
        [Min(0f)] public float speedBonusPower = 0.1f;
        [Range(0f, 0.9f)] public float speedBonusCost = 0.1f;
        [Range(0f, 1f)] public float speedBonusStrain = 0.2f;
        [Min(0f)] public float speedBonusStability = 5f;
        [Tooltip("The performance score moves the spell's power between 1 − this and 1 + this.")]
        [Range(0f, 0.9f)] public float powerSwing = 0.1f;

        [Header("Mistakes (a missed, wrong or late key; with rhythm also a key off the beat)")]
        [Tooltip("Lenient: power lost per mistake (0.06 = −6%).")]
        [Range(0f, 0.5f)] public float powerPerMistake = 0.06f;
        [Tooltip("Lenient: cast time added per mistake (0.1 = +10%).")]
        [Min(0f)] public float castTimePerMistake = 0.1f;
        [Tooltip("Lenient: stability lost per mistake (points; stability decides the misfire chance).")]
        [Min(0f)] public float stabilityPerMistake = 5f;
        [Tooltip("Lenient: mistakes beyond this fail the spell: 1 + this share of the letters (rounded down), unless a rule says.")]
        [Min(0f)] public float mistakesPerLetterShare = 0.34f;

        [Header("Failure")]
        [Tooltip("A spell that fails its sequence loses this share of the Rune Energy a cast would have drawn (0 = none).")]
        [Range(0f, 1f)] public float energyLossOnFailure = 0.5f;
        [Tooltip("Seconds before the same spell may be spelled again after a failed sequence.")]
        [Min(0f)] public float failureLockSeconds = 1f;
        [Tooltip("A failed sequence also causes the spell's misfire damage of the Spell Balance times this (0 = none).")]
        [Min(0f)] public float failureDamageFactor = 1f;

        [Header("Soul Damage on mistakes (the default: none; a rule may set it per spell)")]
        public SpellingDamageMode damageMode = SpellingDamageMode.None;
        [Min(0f)] public float damageAmount = 2f;
        [Tooltip("Scaling mode: each further mistake costs this much more than the one before (0.5 = +50%).")]
        [Min(0f)] public float damageScaling = 0.5f;
        [Tooltip("The Soul Damage Kind of Soul Damage from mistakes.")]
        public string damageKind = "spelling_mistake";

        [Header("Forgiveness (concept knowledge and rune quality make it easier)")]
        [Tooltip("Easier by this much (0-1) when every concept of the spell's runes is known.")]
        [Range(0f, 1f)] public float knowledgeForgiveness = 0.15f;
        [Tooltip("Easier by this much (0-1) at synthesis quality 100.")]
        [Range(0f, 1f)] public float qualityForgiveness = 0.10f;
        [Tooltip("Forgiveness 1 widens every window by this share and allows up to this many more mistakes.")]
        [Min(0f)] public float forgivenessWindow = 1f;
        [Min(0)] public int forgivenessMistakes = 2;

        [Header("What the server accepts")]
        [Tooltip("Keys pressed closer together than this are not human (seconds).")]
        [Min(0f)] public float minPressInterval = 0.07f;
        [Tooltip("Seconds of latency and clock error the server forgives when it checks that a sequence fits the real time that passed.")]
        [Min(0f)] public float latencyAllowance = 1f;
        [Tooltip("The most seconds between the end of a sequence (as its machine says) and the server receiving its result: the result is sent at once, so " +
                 "a result that waits can only be hiding the times it reports. Covers the network and the update interval.")]
        [Min(1f)] public float maxReportLag = 3f;
        [Tooltip("Seconds after its time limit a sequence may still be answered.")]
        [Min(0f)] public float answerGraceSeconds = 4f;
        [Tooltip("A successful sequence stays good for this long: the cast must start within it.")]
        [Min(1f)] public float readySeconds = 8f;
        [Tooltip("Most key presses (right and wrong) a sequence may report, per letter.")]
        [Min(1)] public int maxPressesPerLetter = 4;

        [Header("Presentation")]
        [Tooltip("Short tones for right and wrong keys (in addition to the shapes and text).")]
        public bool soundCues = true;

        public float WeightOf(string term)
        {
            if (weights == null || term == null) return 0f;
            foreach (SpellingWeight w in weights)
                if (w != null && string.Equals(w.term, term, StringComparison.OrdinalIgnoreCase)) return Math.Max(0f, w.weight);
            return 0f;
        }

        public void Validate(List<string> errors, List<string> warnings)
        {
            if (candidateKeys == null || candidateKeys.Count < minPoolSize) errors.Add("Spelling: fewer candidate keys than the minimum pool size.");
            if (maxLetters < minLetters) errors.Add("Spelling: Max Letters is below Min Letters.");
            if (weights == null || weights.TrueForAll(w => w == null || w.weight <= 0f)) errors.Add("Spelling: no evaluation weight is above 0.");
            if (string.IsNullOrWhiteSpace(damageKind)) errors.Add("Spelling: the Damage Kind is empty.");
            if (candidateKeys != null)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string k in candidateKeys)
                    if (string.IsNullOrWhiteSpace(k) || !seen.Add(k.Trim())) warnings.Add($"Spelling: the candidate key '{k}' is empty or listed twice.");
            }
            if (mode == SpellingMode.All && failureModel == SpellingFailureModel.Strict)
                warnings.Add("Spelling: every spell uses the Strict model: one mistake fails any spell.");
        }
    }
}
