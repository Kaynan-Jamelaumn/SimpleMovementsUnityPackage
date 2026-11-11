using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Spellcasting data (design spec 16.1-16.4): which executable a spell becomes, and the numbers that turn what a synthesis
    // earned (power, stability, complexity, rank) into what a cast costs and does. Every value is a proposal, held as data.

    /// <summary>
    /// The executable shape of a spell (design spec 16.4: spells use the existing ability pipeline): a projectile, an area, a
    /// wall, a dash... A form names an ability preset (or a template asset a designer assigns on the database component) and
    /// the desired effects it serves. The spell's own numbers (power, cast time, range...) are applied on top as the slot's
    /// modifiers; the form itself is shared.
    /// </summary>
    [Serializable]
    public class SpellFormDef
    {
        [Tooltip("Stable id (lowercase): 'bolt', 'wall'... It becomes part of the spell's ability id (rune:<form>:<spell>).")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("The ability preset it is built from (a name of Assets > Create > SimpleMovements > Abilities > Ability From " +
                 "Preset, e.g. 'Firebolt (Projectile)'). Empty = an empty self cast (no game effect: costs and strain only). " +
                 "A designer can assign a template ability to the form on the Rune Magic Database component instead.")]
        public string preset = "";
        [Tooltip("The desired effects (ids) this form serves.")]
        public List<string> effects = new List<string>();
        [Tooltip("Effects not listed by any form use the first form of their category listed here.")]
        public List<EffectCategory> fallbackFor = new List<EffectCategory>();
        [Tooltip("The caster may move (slowly) while casting (spec 16.1 'canMoveWhileCasting').")]
        public bool canMoveWhileCasting = false;
        [Tooltip("Multiplies the Rune Energy cost of spells of this form.")]
        [Min(0f)] public float energyMultiplier = 1f;
        [Tooltip("Multiplies the Soul Strain of spells of this form (0 = this form never strains the soul).")]
        [Min(0f)] public float strainMultiplier = 1f;
        [Min(0.1f)] public float rangeMultiplier = 1f;
        [Min(0.1f)] public float areaMultiplier = 1f;
        [Tooltip("Spells of this form can be charged by holding their key (see Spells ▸ Charge), unless they use the Spelling System.")]
        public bool chargeable = false;
        [Tooltip("Free keywords for tools and rules.")]
        public List<string> tags = new List<string>();

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>
    /// The numbers of spellcasting (design spec 16.1, 16.2, 14.1): what a cast costs in Rune Energy and Soul Strain, how the
    /// spell's stats become cast time, power and reach, how stability becomes the chance of a misfire. Held by
    /// <see cref="RuneBalance"/>; tune it on the Rune Magic Database.
    /// </summary>
    [Serializable]
    public class SpellBalance
    {
        [Header("Spell slots (spec 16.4)")]
        [Tooltip("A spell may be put on an ability slot that holds another (non-spell) ability and replaces it (it is not given back when the " +
                 "spell is taken off). Off: only empty slots and slots that hold spells take spells.")]
        public bool replaceOtherAbilities = false;

        [Header("Rune Energy cost (spec 16.1)")]
        [Tooltip("A cast draws this share of the maximum energy of a rune of the spell's rank (× (0.5 + complexity) × the " +
                 "spell's cost multiplier and the soul condition's): the spec's worked example, a powerful rank 3 spell of complexity 0.5, draws " +
                 "2% of 25,000 = 500.")]
        [Range(0.001f, 1f)] public float castEnergyFraction = 0.02f;
        [Tooltip("The least energy one cast draws.")]
        [Min(0f)] public float minEnergyCost = 1f;

        [Header("Soul Strain (spec 14.1, 16.3)")]
        [Tooltip("Soul Damage a successful cast causes at rank 1 and complexity 0.5 (0 = casting never strains the soul). With the per-rank " +
                 "share below, the spec's worked example holds: a powerful rank 3 spell (complexity 0.5) causes 12 Soul Damage, and two of them in a row " +
                 "leave the soul Strained.")]
        [Min(0f)] public float strainBase = 4f;
        [Tooltip("Added per rank above 1 (a share of the base): 1 = the strain grows by the base for each rank (4, 8, 12, 16, 20).")]
        [Min(0f)] public float strainPerRank = 1f;
        [Tooltip("Casting strains the soul (off: spells are free of Soul Damage; only synthesis and failures strain).")]
        public bool strainOnSuccess = true;
        [Tooltip("The Soul Damage Kind of a successful cast (see Soul Damage Kinds: its multiplier, deep share and whether the soul's " +
                 "condition scales it apply). Set the kind's Multiplier to 0 to make casting free of Soul Damage.")]
        public string strainKind = "spell_cast";
        [Tooltip("The Soul Damage Kind of a failed cast (the built-in 'spell_failure' kind sends a small share of the damage to deep damage).")]
        public string failureKind = "spell_failure";
        [Tooltip("A failed cast (a misfire) causes this multiple of the cast's strain (0 = none).")]
        [Min(0f)] public float failureDamageFactor = 1.5f;
        [Tooltip("A cast interrupted by an enemy also causes the failure damage (off: an interrupted cast is only refunded).")]
        public bool damageOnInterrupt = false;

        [Header("Casting time, cooldown and power")]
        [Tooltip("Cast time multiplier at complexity 0 (the form's own time is multiplied by this + complexity × the next value).")]
        [Min(0.1f)] public float castTimeBase = 0.8f;
        [Min(0f)] public float castTimeComplexity = 0.6f;
        [Tooltip("An instant form still takes at least this long to cast (a spell is shaped, not clicked).")]
        [Min(0f)] public float minCastTime = 0.3f;
        [Tooltip("Cooldown multiplier at complexity 0 (+ complexity × the next value).")]
        [Min(0.1f)] public float cooldownBase = 0.6f;
        [Min(0f)] public float cooldownComplexity = 0.8f;
        [Tooltip("The power (spell stat) at which the form's own damage is dealt: damage × power / this. Synthesis power is " +
                 "about 100 × 1.3 for a perfect rank 1 spell, before bonuses.")]
        [Min(1f)] public float referencePower = 100f;
        [Tooltip("Speed multiplier while casting for a form that lets the caster move.")]
        [Range(0f, 1f)] public float castMoveSpeed = 0.5f;

        [Header("Reach (spec 16.1 range, area, duration)")]
        [Min(0f)] public float rangePerRank = 0.05f;
        [Min(0f)] public float areaPerRank = 0.08f;
        [Tooltip("Duration multiplier at stability 0 (+ stability/100 × the next value).")]
        [Min(0.1f)] public float durationBase = 0.8f;
        [Min(0f)] public float durationStability = 0.4f;

        [Header("Stability (spec 16.1) and misfires")]
        [Tooltip("The chance a cast misfires is (1 - stability/100) × this, plus the soul condition's own.")]
        [Range(0f, 1f)] public float instabilityScale = 0.5f;
        [Tooltip("A misfire cuts the cast off between these shares of its cast time.")]
        [Range(0.05f, 1f)] public float fizzleMin = 0.5f;
        [Range(0.05f, 1f)] public float fizzleMax = 0.9f;
        [Tooltip("A misfired cast keeps the energy it drew (off: it is refunded).")]
        public bool misfireKeepsEnergy = true;

        [Header("Charging (hold the key)")]
        [Tooltip("How a chargeable spell (a form with Chargeable on) is charged by holding its key: the longest hold, what a full charge multiplies, and what a " +
                 "short tap does. Its Cost At Full also scales the Rune Energy a cast draws. Spells that use the Spelling System are not charged.")]
        public ChargeProfile charge = new ChargeProfile();
        [Tooltip("A full charge multiplies the Soul Strain of the cast by this (a partial charge in between).")]
        [Min(0f)] public float chargeStrainAtFull = 1.5f;
        [Tooltip("A full charge adds this to the chance the cast misfires (a partial charge in between): a held spell is harder to keep stable.")]
        [Range(0f, 1f)] public float chargeInstabilityAtFull = 0.12f;

        public void Validate(List<string> errors, List<string> warnings)
        {
            if (fizzleMax < fizzleMin) errors.Add("Spells: Fizzle Max is below Fizzle Min.");
            if (string.IsNullOrWhiteSpace(strainKind)) errors.Add("Spells: the Strain Kind is empty.");
            if (string.IsNullOrWhiteSpace(failureKind)) errors.Add("Spells: the Failure Kind is empty.");
            if (castEnergyFraction * 4f > 1f) warnings.Add("Spells: a cast draws a quarter or more of a rune's maximum energy: spells will be very expensive.");
            if (charge == null) errors.Add("Spells: the charge settings are missing.");
            else charge.Validate("Spells ▸ Charge", errors, warnings);
        }
    }
}
