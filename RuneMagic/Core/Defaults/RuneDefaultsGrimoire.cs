namespace RuneMagic
{
    public static partial class RuneDefaults
    {
        /// <summary>
        /// The Grimoire's built-in numbers: ten pages, an imprint costs a quarter of the rune's maximum energy (250 for a rank 1 rune,
        /// about two normal kills of energy), an absorbed print holds no energy, and the page is free again. Not from the design spec.
        /// </summary>
        public static GrimoireBalance Grimoire() => new GrimoireBalance
        {
            giveOnSpawn = true,
            reissueIfMissing = true,
            pages = 10,
            imprintEnergyFraction = 0.25f,
            minImprintCost = 10f,
            allowComposite = true,
            onlyAuthorImprints = true,
            allowErase = true,
            absorbedEnergyFraction = 0f,
            pageFreeAfterAbsorb = true,
            imprintCooldownSeconds = 3f,
            absorbCooldownSeconds = 2f,
            maxImprintsPerHour = 40,
            maxAbsorbsPerHour = 40,
            logEntries = 200,
            logToConsole = false,
            bookName = "Grimoire",
            itemWeight = 1f,
            itemPrice = 0f,
            canBeSold = false,
        };
    }
}
