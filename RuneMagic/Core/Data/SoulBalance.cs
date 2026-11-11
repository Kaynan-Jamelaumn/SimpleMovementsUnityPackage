using System;
using UnityEngine;

namespace RuneMagic
{
    // The Soul Integrity and Soul Rune Capacity rules that are data (design spec 4, 14, 15). Held by RuneBalance; every
    // value is tuned on the Rune Magic Database. Soul Integrity is its own resource: nothing here reads or changes health,
    // mana, stamina or rune energy (design spec 20).

    /// <summary>How Soul Integrity recovers (design spec 15). The soul never heals by itself unless Passive Per Minute says so.</summary>
    [Serializable]
    public class SoulRecoveryBalance
    {
        [Header("Natural recovery")]
        [Tooltip("Soul Integrity recovered per minute without resting or meditating. 0 = none: the soul does not heal by itself " +
                 "and does not return to full after a fight (design spec 15.2).")]
        [Min(0f)] public float passivePerMinute = 0f;
        [Tooltip("Natural recovery stops while in combat.")]
        public bool passiveOnlyOutOfCombat = true;

        [Header("Rest (sleeping)")]
        [Tooltip("Recovered per minute while the character sleeps (gradual), before the sleep spot's quality.")]
        [Min(0f)] public float restPerMinute = 3f;
        [Tooltip("Rest is scaled by the sleep quality's Recovery Multiplier (the ground is worse than a bed).")]
        public bool restUsesSleepQuality = true;

        [Header("Meditation")]
        [Tooltip("Recovered per minute while meditating, once settled. Meditation is the efficient way to stabilize the soul.")]
        [Min(0f)] public float meditationPerMinute = 10f;
        [Tooltip("Seconds of meditation before recovery starts (settling in). Breaking off early recovers nothing.")]
        [Min(0f)] public float meditationSettleSeconds = 3f;
        [Tooltip("Meditation can be started while in combat.")]
        public bool meditationAllowedInCombat = false;
        [Tooltip("Meditation can be started when the soul has nothing ordinary to recover.")]
        public bool meditationAllowedWhenFull = false;
        [Tooltip("The character cannot move while meditating; the Meditate key ends it. Off: moving ends it.")]
        public bool meditationLocksMovement = false;
        [Tooltip("Metres the character may be moved (by itself or pushed) before meditation or a ritual breaks.")]
        [Min(0f)] public float channelMoveTolerance = 0.4f;
        [Tooltip("Taking damage breaks meditation and rituals.")]
        public bool channelBrokenByDamage = true;
        [Tooltip("Crowd control (stun, knockback, root...) breaks meditation and rituals.")]
        public bool channelBrokenByControl = true;
        [Tooltip("Hitting something breaks meditation and rituals.")]
        public bool channelBrokenByAttacking = true;

        [Header("Combat")]
        [Tooltip("Seconds after the last hit (taken or dealt) during which the character counts as in combat.")]
        [Min(0f)] public float combatWindowSeconds = 6f;

        [Header("Places")]
        [Tooltip("Recovery multiplier of a Soul Sanctuary that does not set its own (rest and meditation recover this many times faster).")]
        [Min(0f)] public float sanctuaryMultiplier = 2f;
        [Tooltip("Recovered per minute just by being in a sanctuary out of combat, even without resting or meditating.")]
        [Min(0f)] public float sanctuaryPassivePerMinute = 1f;
        [Tooltip("The hub world's city counts as a sanctuary with this multiplier (1 = it does not).")]
        [Min(0f)] public float cityMultiplier = 1f;

        [Header("Deep damage")]
        [Tooltip("Most of the maximum Soul Integrity that deep damage can scar (0-1). Deep damage lowers the ceiling that " +
                 "ordinary recovery (rest, meditation, sanctuaries) can reach; only specialized healing removes it: soul items " +
                 "that heal deep damage, healing rituals, sanctuaries that heal it.")]
        [Range(0f, 1f)] public float maxDeepDamageFraction = 0.6f;
        [Tooltip("Deep share of damage whose kind is not in Soul Damage Kinds.")]
        [Range(0f, 1f)] public float unknownKindDeepShare = 0f;

        [Header("Modifiers (abilities, traits, equipment)")]
        [Tooltip("Lowest total recovery multiplier the modifiers can make.")]
        [Min(0f)] public float minModifierMultiplier = 0f;
        [Tooltip("Highest total recovery multiplier the modifiers can make.")]
        [Min(0f)] public float maxModifierMultiplier = 10f;
    }

    /// <summary>
    /// A source of Soul Damage (design spec 14.1) and how its damage behaves. The systems that hurt the soul (synthesis,
    /// casting, the Spelling System, absorption...) name the kind; the damage itself scales with their complexity and risk.
    /// </summary>
    [Serializable]
    public class SoulDamageKind
    {
        [Tooltip("Stable id the systems use: synthesis, synthesis_failure, incompatible_absorption, overuse, spell_cast, " +
                 "spell_failure, spelling_mistake, concept_overreach, primordial, experiment (add your own).")]
        public string id = "";
        public string name = "";
        [Tooltip("Multiplies the damage of this kind.")]
        [Min(0f)] public float multiplier = 1f;
        [Tooltip("Share of the damage that becomes deep damage (0-1), which only specialized healing removes.")]
        [Range(0f, 1f)] public float deepShare = 0f;
        [Tooltip("The current soul condition's Soul Damage Multiplier applies (a weakened soul is hurt more).")]
        public bool scaledByCondition = true;
        [TextArea(1, 2)] public string description = "";

        public SoulDamageKind() { }

        public SoulDamageKind(string id, string name, float deepShare, string description, float multiplier = 1f)
        {
            this.id = id;
            this.name = name;
            this.deepShare = deepShare;
            this.description = description;
            this.multiplier = multiplier;
        }
    }

    /// <summary>What a Soul Collapse (Soul Integrity reaching 0) does (design spec 14.3, decision D8).</summary>
    [Serializable]
    public class SoulCollapseRules
    {
        [Tooltip("Seconds the character is incapacitated when the soul collapses (0 = not at all).")]
        [Min(0f)] public float incapacitateSeconds = 8f;
        [Tooltip("After a collapse, casting and synthesis stay locked until Soul Integrity is back to this percent of the maximum.")]
        [Range(0f, 100f)] public float lockUntilPercent = 20f;
        [Tooltip("Magical energy not yet given to a rune is lost.")]
        public bool losePendingEnergy = true;
        [Tooltip("Deep damage the collapse itself adds (% of the maximum Soul Integrity).")]
        [Range(0f, 100f)] public float deepDamagePercent = 5f;
    }
}
