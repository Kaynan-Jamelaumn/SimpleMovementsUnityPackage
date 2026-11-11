namespace RuneMagic
{
    /// <summary>
    /// What an ability slot holding a rune spell asks of the soul (see <see cref="ICastable"/>): can it be cast, what a cast
    /// draws and strains, the tooltip. A thin adapter: the rules are in <see cref="SpellCasting"/>, applied by the
    /// <see cref="RuneSoul"/>.
    /// </summary>
    internal sealed class RuneSpellCastable : ICastable
    {
        private readonly RuneSoul soul;
        public string SpellId { get; }

        public RuneSpellCastable(RuneSoul soul, string spellId)
        {
            this.soul = soul;
            SpellId = spellId;
        }

        public bool HasResources(AbilityCaster caster) => soul != null && soul.CanCastSpell(SpellId, out _);

        public bool CanStart(AbilityCaster caster) => soul != null && soul.CanStartSpell(SpellId, out _);

        public void OnCastStarted(AbilityCastInstance cast)
        {
            if (soul != null) soul.SpellCastStarted(SpellId, cast);
        }

        public void OnCastReleased(AbilityCastInstance cast)
        {
            if (soul != null) soul.SpellCastReleased(SpellId, cast);
        }

        public void OnCastInterrupted(AbilityCastInstance cast, CastInterruptReason reason)
        {
            if (soul != null) soul.SpellCastInterrupted(SpellId, cast, reason);
        }

        public string Describe() => soul != null ? soul.DescribeSpell(SpellId) : "";

        /// <summary>The charge profile of this spell (null when it cannot be charged).</summary>
        internal ChargeProfile ChargeProfile() => soul != null ? soul.ChargeProfileOfSpell(SpellId) : null;

        /// <summary>Do the anchored runes hold the energy a charge of <paramref name="fraction"/> draws? (Where the rules run.)</summary>
        internal bool CanAffordCharge(float fraction) => soul == null || soul.CanAffordSpellCharge(SpellId, fraction);
    }
}
