using System.Collections.Generic;

namespace RuneMagic
{
    // Built-in Soul Integrity data (design spec 14, 15): the sources of soul damage and how deep they cut, collapse and
    // recovery. The spec gives the rules, not the numbers: every value here is a proposal, tuned on the database.
    public static partial class RuneDefaults
    {
        /// <summary>
        /// The sources of Soul Damage of design spec 14.1. Ordinary synthesis damage heals with meditation (no deep share);
        /// Primordial runes scar the soul deeply and need rare materials or rituals (design spec 15.2).
        /// </summary>
        public static List<SoulDamageKind> SoulDamageKinds() => new List<SoulDamageKind>
        {
            new SoulDamageKind("synthesis", "Rune Synthesis", 0f, "The strain of any synthesis; recovers with rest and meditation."),
            new SoulDamageKind("synthesis_failure", "Failed combination", 0.1f, "A synthesis that failed; a small part needs specialized healing."),
            new SoulDamageKind("incompatible_absorption", "Incompatible rune", 0.15f, "Absorbing a rune the soul rejects."),
            new SoulDamageKind("overuse", "Beyond safe limits", 0.1f, "Using runes beyond what the soul can safely sustain."),
            new SoulDamageKind("spell_cast", "Powerful casting", 0f, "Casting a powerful effect (per spell; never automatic)."),
            new SoulDamageKind("spell_failure", "Failed cast", 0.05f, "A spell that failed while being cast (per spell)."),
            new SoulDamageKind("spelling_mistake", "Spelling mistake", 0f, "A wrong key in the Spelling System (per spell)."),
            new SoulDamageKind("concept_overreach", "Concept overreach", 0.25f, "Manipulating concepts beyond the caster's understanding."),
            new SoulDamageKind("primordial", "Primordial rune", 0.6f, "Using a Primordial rune: deep damage that needs rare materials or rituals."),
            new SoulDamageKind("experiment", "Dangerous experiment", 0.3f, "Unstable or dangerous experiments."),
        };

        /// <summary>Soul recovery (design spec 15.1-15.2): no natural recovery, gradual rest, efficient meditation.</summary>
        public static SoulRecoveryBalance SoulRecovery() => new SoulRecoveryBalance
        {
            passivePerMinute = 0f,
            passiveOnlyOutOfCombat = true,
            restPerMinute = 3f,
            restUsesSleepQuality = true,
            meditationPerMinute = 10f,
            meditationSettleSeconds = 3f,
            meditationAllowedInCombat = false,
            meditationAllowedWhenFull = false,
            meditationLocksMovement = false,
            channelMoveTolerance = 0.4f,
            channelBrokenByDamage = true,
            channelBrokenByControl = true,
            channelBrokenByAttacking = true,
            combatWindowSeconds = 6f,
            sanctuaryMultiplier = 2f,
            sanctuaryPassivePerMinute = 1f,
            cityMultiplier = 1f,
            maxDeepDamageFraction = 0.6f,
            unknownKindDeepShare = 0f,
            minModifierMultiplier = 0f,
            maxModifierMultiplier = 10f,
        };

        /// <summary>Soul Collapse consequences (decision D8).</summary>
        public static SoulCollapseRules SoulCollapse() => new SoulCollapseRules
        {
            incapacitateSeconds = 8f,
            lockUntilPercent = 20f,
            losePendingEnergy = true,
            deepDamagePercent = 5f,
        };
    }
}
