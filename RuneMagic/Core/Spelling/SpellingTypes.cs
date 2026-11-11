using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// What one spell's key sequence is, resolved: how many letters, how long each key's window is, the rhythm, the failure model
    /// and what mistakes cost (design spec 16.5). Generated from the spell's rank, power and complexity, the soul's condition and
    /// the player's knowledge, and overridden by the rules of the database; plain data, so it travels to the owner's client.
    /// </summary>
    [Serializable]
    public class SpellingParams
    {
        public bool uses;
        public int letters;
        /// <summary>The time window of each key, in seconds: a different one for every key when the spell says so.</summary>
        public List<float> windows = new List<float>();
        public float totalLimit;
        public bool rhythm;
        public float tempo;
        public float tolerance;
        public SpellingFailureModel model;
        /// <summary>Lenient: the spell fails with more mistakes than this.</summary>
        public int maxMistakes;

        public SpellingDamageMode damageMode;
        public float damageAmount;
        public float damageScaling;
        public string damageKind = "";

        public List<SpellingWeight> weights = new List<SpellingWeight>();
        public float speedBonusThreshold;
        public int speedBonusMaxMistakes;
        public float speedBonusPower, speedBonusCost, speedBonusStrain, speedBonusStability;
        public float powerSwing;
        /// <summary>Per mistake, already scaled by the soul's condition.</summary>
        public float powerPerMistake, castTimePerMistake, stabilityPerMistake;

        public float energyLossOnFailure;
        public float failureLockSeconds;
        public float failureDamageFactor;
        /// <summary>How much knowledge and rune quality made it easier (0-1).</summary>
        public float forgiveness;

        public void EnsureLists()
        {
            if (windows == null) windows = new List<float>();
            if (weights == null) weights = new List<SpellingWeight>();
            if (damageKind == null) damageKind = "";
        }

        /// <summary>The window of key <paramref name="i"/> (the last one repeats when there are fewer windows than letters).</summary>
        public float WindowOf(int i)
        {
            if (windows == null || windows.Count == 0) return 1f;
            return windows[Math.Max(0, Math.Min(i, windows.Count - 1))];
        }

        /// <summary>The time the sequence may take at most: the limit, or (0 = none) the windows added up.</summary>
        public float EffectiveLimit
        {
            get
            {
                if (totalLimit > 0f) return totalLimit;
                float sum = 0f;
                if (windows != null) foreach (float w in windows) sum += w;
                return sum;
            }
        }

        /// <summary>The moment (seconds after the first prompt) key <paramref name="i"/> should be pressed to be on the beat.</summary>
        public float BeatOf(int i) => (i + 1) * Math.Max(0.05f, tempo);

        public SpellingParams Clone()
        {
            var c = (SpellingParams)MemberwiseClone();
            c.windows = new List<float>(windows ?? new List<float>());
            c.weights = new List<SpellingWeight>();
            if (weights != null) foreach (SpellingWeight w in weights) if (w != null) c.weights.Add(new SpellingWeight(w.term, w.weight));
            return c;
        }
    }

    /// <summary>What the difficulty of a sequence depends on besides the spell: the player's knowledge and the soul.</summary>
    public struct SpellingContext
    {
        /// <summary>Share (0-1) of the concepts of the spell's runes the player knows.</summary>
        public float knowledge;
        /// <summary>The spell's synthesis quality (0-100).</summary>
        public float quality;
        public SoulRestrictions soul;

        public static SpellingContext Plain => new SpellingContext { soul = SoulRestrictions.None };
    }

    /// <summary>A key pressed during a sequence and when (seconds after the first prompt).</summary>
    public struct SpellingPress
    {
        public string key;
        public float t;
        public SpellingPress(string key, float t) { this.key = key; this.t = t; }
    }

    public enum SpellingStepState
    {
        Pending = 0,
        Hit = 1,
        Missed = 2,
    }

    /// <summary>How one letter of the sequence went.</summary>
    public struct SpellingStep
    {
        public SpellingStepState state;
        /// <summary>Hit: when the key was pressed. Missed: when its window ended.</summary>
        public float t;
        /// <summary>Hit: pressed off the beat (rhythm only).</summary>
        public bool offBeat;
        /// <summary>Wrong keys pressed while this letter was asked for.</summary>
        public int wrongKeys;
    }

    public enum SpellingFailure
    {
        None = 0,
        /// <summary>Strict model: a mistake.</summary>
        Mistake = 1,
        /// <summary>Lenient model: more mistakes than the spell allows.</summary>
        TooManyMistakes = 2,
    }

    /// <summary>
    /// What a sequence earned (design spec 16.5 "performance result"): the score, the mistakes, and what they and the speed do to
    /// the spell and to the soul. Computed by the server from the key presses; the client's overlay only shows it.
    /// </summary>
    public sealed class SpellingOutcome
    {
        public bool success;
        public SpellingFailure failure;
        public int hits, wrongKeys, missed, offBeat;
        public int Mistakes => wrongKeys + missed + offBeat;
        /// <summary>Seconds from the first prompt to the last key (or the end of the last window).</summary>
        public float duration;
        /// <summary>0-1: the weighted accuracy, speed, precision and rhythm.</summary>
        public float performance;
        public float accuracy, speed, precision, rhythm;
        public bool speedBonus;
        public List<SpellingStep> steps = new List<SpellingStep>();

        // What it does to the cast (a success; a failure does not cast).
        public float powerMultiplier = 1f;
        public float castTimeMultiplier = 1f;
        public float costMultiplier = 1f;
        public float strainMultiplier = 1f;
        public float stabilityDelta;

        /// <summary>Soul Damage the mistakes cost (before the kind's and the condition's multipliers) and its kind.</summary>
        public float mistakeDamage;
        public string damageKind = "";

        public string Summary()
        {
            if (!success)
                return failure == SpellingFailure.Mistake ? "A wrong key: the spell fails." : $"Too many mistakes ({Mistakes}): the spell fails.";
            string text = Mistakes == 0 ? "Clean." : Mistakes == 1 ? "1 mistake." : $"{Mistakes} mistakes.";
            if (speedBonus) text += " Fast: bonus!";
            return text + $" Performance {performance * 100f:0}%.";
        }
    }
}
