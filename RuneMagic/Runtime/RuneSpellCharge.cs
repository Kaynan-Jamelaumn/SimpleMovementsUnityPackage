using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Makes rune spells chargeable by holding their key: tells the ability system's charge rules which slots hold a chargeable spell (its form
    /// allows it and it does not use the Spelling System) and with what profile (Spells ▸ Charge), and refuses a charge the anchored runes cannot
    /// pay for (the cast then goes off uncharged). The holding itself is the player's <see cref="AbilityCharger"/>.
    /// </summary>
    internal sealed class RuneSpellCharge : IChargeSource
    {
        private static readonly RuneSpellCharge instance = new RuneSpellCharge();

        internal static void Register() => AbilityCharging.Register(instance);

        public bool TryGetProfile(AbilityCaster caster, AbilitySlot slot, out ChargeProfile profile)
        {
            profile = null;
            if (!(slot?.Castable is RuneSpellCastable spell)) return false;
            profile = spell.ChargeProfile();
            return profile != null;
        }

        public bool CanAfford(AbilityCaster caster, AbilitySlot slot, ChargeProfile profile, float fraction) =>
            !(slot?.Castable is RuneSpellCastable spell) || spell.CanAffordCharge(fraction);
    }
}
