using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The Grimoire: a book every Mage starts with. The Mage <b>imprints</b> a copy of a rune of their soul on one of its pages
    /// (the rune keeps existing but loses part of its energy; it must have that energy), and a Mage who holds the book
    /// <b>absorbs</b> a print from a page: it becomes a rune of their soul with no energy, and the page is spent (a print can
    /// be absorbed once). The book is an item: whoever holds another Mage's grimoire can read it and use it.
    /// All numbers are data; nothing here is a spec number (the design spec does not have the grimoire).
    /// </summary>
    [Serializable]
    public class GrimoireBalance
    {
        [Header("Who has one")]
        [Tooltip("A new Mage begins with a grimoire in their inventory (the soul remembers it was given). Off: grimoires only come from items " +
                 "you give out yourself (id 'grimoire:<serial>:<author>', see the guide).")]
        public bool giveOnSpawn = true;
        [Tooltip("A Mage who enters the game holding none of their own grimoires gets it again, with its pages (the inventory may not be kept " +
                 "between sessions in your game). While another player holds it too, both copies show the same pages.")]
        public bool reissueIfMissing = true;

        [Header("Pages")]
        [Tooltip("Pages of a new grimoire: how many prints it can hold at once.")]
        [Min(1)] public int pages = 10;

        [Header("Imprinting (a rune of the soul is copied on a page)")]
        [Tooltip("What an imprint costs the rune of the soul, as a share of that rune's maximum energy (it must have that much energy to be imprinted).")]
        [Range(0.001f, 1f)] public float imprintEnergyFraction = 0.25f;
        [Tooltip("The least energy an imprint costs.")]
        [Min(0f)] public float minImprintCost = 10f;
        [Tooltip("Runes made by synthesis can be imprinted too (their profile is copied with them).")]
        public bool allowComposite = true;
        [Tooltip("Only the Mage who made the grimoire (its author) writes in it; a Mage who holds it only reads it and absorbs from it.")]
        public bool onlyAuthorImprints = true;
        [Tooltip("The author may erase a print from a page (nothing is given back).")]
        public bool allowErase = true;

        [Header("Absorbing a print")]
        [Tooltip("The energy an absorbed print holds, as a share of its maximum (0 = none: the spec of this feature).")]
        [Range(0f, 1f)] public float absorbedEnergyFraction = 0f;
        [Tooltip("A page whose print was absorbed is blank again and can be written once more. Off: the page is spent for good.")]
        public bool pageFreeAfterAbsorb = true;

        [Header("Against duplication and abuse (0 = no limit)")]
        [Tooltip("Seconds a Mage must wait between two imprints.")]
        [Min(0f)] public float imprintCooldownSeconds = 3f;
        [Tooltip("Seconds a Mage must wait between two absorbs.")]
        [Min(0f)] public float absorbCooldownSeconds = 2f;
        [Tooltip("Most imprints one Mage may make in an hour.")]
        [Min(0)] public int maxImprintsPerHour = 40;
        [Tooltip("Most prints one Mage may absorb in an hour.")]
        [Min(0)] public int maxAbsorbsPerHour = 40;
        [Tooltip("How many actions (imprints, absorbs, erasures and refusals) each book remembers; the audit log shows who did what.")]
        [Min(0)] public int logEntries = 200;
        [Tooltip("Every action is also written to the Console (the server's log).")]
        public bool logToConsole = false;

        [Header("The book as an item")]
        [Tooltip("The name of the item ('Grimoire of <author>').")]
        public string bookName = "Grimoire";
        [Min(0f)] public float itemWeight = 1f;
        [Min(0f)] public float itemPrice = 0f;
        [Tooltip("A merchant buys it (its prints would be sold with it).")]
        public bool canBeSold = false;

        /// <summary>Adds the problems of these settings.</summary>
        public void Validate(List<string> errors, List<string> warnings)
        {
            if (pages < 1) errors?.Add("Grimoire: Pages must be at least 1.");
            if (pages > 64) warnings?.Add("Grimoire: more than 64 pages make the book's window very long.");
            if (!(imprintEnergyFraction > 0f)) errors?.Add("Grimoire: Imprint Energy Fraction must be above 0 (an imprint always costs energy).");
            if (imprintEnergyFraction >= 1f) warnings?.Add("Grimoire: an imprint costs a rune all its energy.");
            if (absorbedEnergyFraction > 0f) warnings?.Add("Grimoire: absorbed prints hold energy: copying runes costs less than earning them.");
            if (string.IsNullOrWhiteSpace(bookName)) warnings?.Add("Grimoire: the book has no name.");
            if (maxImprintsPerHour == 0 && maxAbsorbsPerHour == 0 && imprintCooldownSeconds <= 0f && absorbCooldownSeconds <= 0f)
                warnings?.Add("Grimoire: nothing limits how fast runes can be copied (see Against duplication and abuse).");
            if (logEntries == 0) warnings?.Add("Grimoire: books keep no log: nobody can tell who copied what.");
        }
    }
}
