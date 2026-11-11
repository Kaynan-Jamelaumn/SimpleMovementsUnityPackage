#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// The Grimoire without the engine: ids, the imprint (a copy of a rune of the soul on a page; the rune loses energy and must have it),
    /// the absorption (a print becomes a rune with no energy, once, if the soul has room), reading another Mage's book, erasing, the
    /// world's ledger, the views and the request texts, and the balance. The component, the item and the window are in
    /// <see cref="GrimoireRuntimeTests"/> (they need the engine).
    /// </summary>
    public class GrimoireCoreTests
    {
        private const string Author = "c0000000000000000000author";
        private const string Reader = "c00000000000000000000reader";

        private static RuneCatalog Catalog() => RuneDefaults.Catalog();
        private static RuneCatalog Custom(Action<RuneMagicData> edit) => SynthLab.Custom(edit);

        private static RuneSoulState Soul(RuneCatalog c, params (string rune, int rank, float energy)[] runes)
        {
            var s = new RuneSoulState();
            SoulRules.Normalize(c.Balance, s.soul, c.Balance.maxSoulIntegrity);
            foreach ((string rune, int rank, float energy) r in runes)
                RuneRules.Absorb(c, s, new RuneDrop { runeId = r.rune, rank = r.rank, quality = 50f, energy = r.energy }, new RuneOrigin { kind = "test" }, null, 1L);
            return s;
        }

        private static GrimoireData Book(RuneCatalog c, string authorKey = Author, string serial = "abc123") =>
            new GrimoireLedger().Create(serial, authorKey, "Aria", c.Grimoire.pages, 1L);

        private static RuneInstance CompositeRune(string name, int slots)
        {
            var r = new RuneInstance
            {
                instanceId = Guid.NewGuid().ToString("N"), runeId = "composite_test", rank = 2, quality = 70f, energy = 5000f,
                flawed = true, history = new List<string> { "syn1" },
            };
            r.composite = new CompositeProfile
            {
                active = true, name = name, alignments = new List<string> { "fire" }, slotCost = slots, effectId = "offense.explosion",
                aspects = new List<AspectWeight> { new AspectWeight("combustion", 1f) },
            };
            r.flaws.Add("unstable");
            return r;
        }

        // ================================================================== ids
        [Test]
        public void AGrimoireIdIsMadeFromASerialAndAnAuthorAndReadBack()
        {
            string id = GrimoireIds.Make("3FA9C1d2E4", "Aria the Wise!");
            Assert.AreEqual("grimoire:3fa9c1d2e4:AriatheWise", id);
            Assert.IsTrue(GrimoireIds.TryParse(id, out string serial, out string tag));
            Assert.AreEqual("3fa9c1d2e4", serial);
            Assert.AreEqual("AriatheWise", tag);
            Assert.AreEqual("3fa9c1d2e4", GrimoireIds.SerialOf(id));
            Assert.AreEqual("Mage", GrimoireIds.CleanTag("   !!! "), "an author with nothing usable in their name is still named");
            Assert.AreEqual(GrimoireIds.MaxTag, GrimoireIds.CleanTag(new string('a', 50)).Length);
        }

        [Test]
        public void MalformedGrimoireIdsAreRefused()
        {
            foreach (string bad in new[]
            {
                null, "", "grimoire:", "grimoire:abc", "grimoire:abc:", "grimoire::Aria", "grimoire:ABC:Aria", "grimoire:abc:Ari a",
                "grimoire:abc:Aria:extra", "grimoire:a-b:Aria", "rune:wind:1:50:0", "Grimoire:abc:Aria", "grimoire:" + new string('a', 40) + ":Aria",
                "grimoire:abc:" + new string('b', 40),
            })
            {
                Assert.IsFalse(GrimoireIds.TryParse(bad, out _, out _), "'" + bad + "' is not a grimoire id");
                Assert.AreEqual("", GrimoireIds.SerialOf(bad));
            }
            Assert.IsFalse(GrimoireIds.IsId("rune:wind:1:50:0"), "a rune item is not a grimoire");
        }

        // ================================================================== imprinting
        [Test]
        public void AnImprintCostsAShareOfTheRunesMaximumEnergyAndAtLeastTheMinimum()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 0f), ("wind", 3, 0f));
            Assert.AreEqual(250f, GrimoireRules.ImprintCost(c, s.runes[0]), 0.01f, "a quarter of the 1,000 a rank 1 rune holds");
            Assert.AreEqual(6250f, GrimoireRules.ImprintCost(c, s.runes[1]), 0.01f, "a quarter of the 25,000 of rank 3");
            RuneCatalog cheap = Custom(d => { d.balance.grimoire.imprintEnergyFraction = 0.001f; d.balance.grimoire.minImprintCost = 10f; });
            Assert.AreEqual(10f, GrimoireRules.ImprintCost(cheap, s.runes[0]), 0.01f, "never below the minimum");
        }

        [Test]
        public void ImprintingWritesACopyOnThePageAndTheRuneLosesEnergyButStays()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 600f));
            GrimoireData book = Book(c);
            string rune = s.runes[0].instanceId;
            ImprintPlan plan = GrimoireRules.Imprint(c, s, book, Author, "Aria", 2, rune, 100L);
            Assert.IsTrue(plan.ok, plan.reason);
            Assert.AreEqual(250f, plan.cost, 0.01f);
            Assert.AreEqual(1, s.runes.Count, "the rune stays in the soul: a copy was made");
            Assert.AreEqual(350f, s.runes[0].energy, 0.01f, "it lost part of its energy");
            GrimoirePage p = book.Page(2);
            Assert.IsTrue(p.filled);
            Assert.AreEqual("wind", p.print.runeId);
            Assert.AreEqual(1, p.print.rank);
            Assert.AreEqual(50f, p.print.quality, 0.01f);
            Assert.AreEqual("", p.print.instanceId, "a print is not a rune of any soul");
            Assert.AreEqual(0f, p.print.energy, "and holds no energy");
            Assert.AreEqual("Aria", p.imprintedBy);
            Assert.AreEqual("grimoire", p.print.origin.kind);
            Assert.AreEqual(1, book.FilledCount());
        }

        [Test]
        public void ARuneThatLacksTheEnergyCannotBeImprintedAndNothingChanges()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 200f));
            GrimoireData book = Book(c);
            ImprintPlan plan = GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, s.runes[0].instanceId, 1L);
            Assert.IsFalse(plan.ok);
            Assert.AreEqual(GrimoireRefusal.NotEnoughEnergy, plan.refusal);
            StringAssert.Contains("250", plan.reason, "it says what the imprint costs");
            StringAssert.Contains("200", plan.reason, "and what the rune holds");
            Assert.AreEqual(200f, s.runes[0].energy, "no energy was taken");
            Assert.IsFalse(book.Page(0).filled, "and nothing was written");
            Assert.AreEqual(0, book.FilledCount());

            // Exactly the cost is enough: the rune is left empty.
            s.runes[0].energy = 250f;
            Assert.IsTrue(GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, s.runes[0].instanceId, 1L).ok);
            Assert.AreEqual(0f, s.runes[0].energy, 0.001f);
        }

        [Test]
        public void EveryRefusedImprintLeavesTheSoulAndTheBookAsTheyWere()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 900f), ("fire", 1, 900f));
            GrimoireData book = Book(c);
            string wind = s.runes[0].instanceId, fire = s.runes[1].instanceId;
            Assert.IsTrue(GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, wind, 1L).ok);
            string before = s.ToJson() + JsonUtility.ToJson(book);

            Assert.AreEqual(GrimoireRefusal.PageFilled, GrimoireRules.PlanImprint(c, s, book, Author, 0, fire).refusal, "a page holds one print");
            Assert.AreEqual(GrimoireRefusal.NoSuchPage, GrimoireRules.PlanImprint(c, s, book, Author, -1, fire).refusal);
            Assert.AreEqual(GrimoireRefusal.NoSuchPage, GrimoireRules.PlanImprint(c, s, book, Author, 99, fire).refusal);
            Assert.AreEqual(GrimoireRefusal.NoSuchRune, GrimoireRules.PlanImprint(c, s, book, Author, 1, "nope").refusal, "a rune the soul does not hold");
            Assert.AreEqual(GrimoireRefusal.NoSuchRune, GrimoireRules.PlanImprint(c, s, book, Author, 1, null).refusal);
            Assert.AreEqual(GrimoireRefusal.NotAuthor, GrimoireRules.PlanImprint(c, s, book, Reader, 1, fire).refusal);
            Assert.AreEqual(GrimoireRefusal.NoGrimoire, GrimoireRules.PlanImprint(c, s, null, Author, 1, fire).refusal);
            book.Page(1).spent = true;
            Assert.AreEqual(GrimoireRefusal.PageSpent, GrimoireRules.PlanImprint(c, s, book, Author, 1, fire).refusal);
            book.Page(1).spent = false;
            Assert.IsFalse(GrimoireRules.Imprint(c, s, book, Reader, "Bram", 1, fire, 1L).ok);
            Assert.AreEqual(before, s.ToJson() + JsonUtility.ToJson(book), "nothing changed");
        }

        [Test]
        public void OnlyTheAuthorWritesWhenTheBalanceSaysSoAndAnyoneWhenItDoesNot()
        {
            RuneCatalog strict = Catalog();
            RuneSoulState s = Soul(strict, ("wind", 1, 900f));
            GrimoireData book = Book(strict);
            Assert.IsTrue(GrimoireRules.MayWrite(strict, book, Author));
            Assert.IsFalse(GrimoireRules.MayWrite(strict, book, Reader));
            Assert.IsFalse(GrimoireRules.MayWrite(strict, book, ""), "nobody's key is the author's");
            Assert.IsFalse(GrimoireRules.MayWrite(strict, book, null));

            RuneCatalog open = Custom(d => d.balance.grimoire.onlyAuthorImprints = false);
            Assert.IsTrue(GrimoireRules.MayWrite(open, book, Reader));
            Assert.IsTrue(GrimoireRules.Imprint(open, s, book, Reader, "Bram", 0, s.runes[0].instanceId, 1L).ok);
        }

        [Test]
        public void ACompositeRuneIsImprintedWithItsProfileOrRefusedWhenTheBalanceSaysSo()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c);
            RuneInstance comp = CompositeRune("Storm Lance", 2);
            s.runes.Add(comp);
            GrimoireData book = Book(c);
            ImprintPlan plan = GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, comp.instanceId, 1L);
            Assert.IsTrue(plan.ok, plan.reason);
            RuneInstance print = book.Page(0).print;
            Assert.IsTrue(print.IsComposite);
            Assert.AreEqual("Storm Lance", print.composite.name);
            Assert.AreEqual(2, print.composite.slotCost);
            Assert.IsTrue(print.flawed, "a flawed rune is copied flawed");
            Assert.AreEqual(0, print.history.Count, "a print is the rune, not the story of this copy of it");
            Assert.AreEqual(0f, print.energy);

            RuneCatalog no = Custom(d => d.balance.grimoire.allowComposite = false);
            ImprintPlan refused = GrimoireRules.PlanImprint(no, s, Book(no, Author, "other"), Author, 0, comp.instanceId);
            Assert.AreEqual(GrimoireRefusal.RuneNotAllowed, refused.refusal);
        }

        // ================================================================== absorbing
        [Test]
        public void AnAbsorbedPrintIsANewRuneWithNoEnergyAndThePageIsFreeAgain()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 600f));
            GrimoireData book = Book(c);
            string original = s.runes[0].instanceId;
            GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, original, 1L);
            int absorbedBefore = s.runesAbsorbed;

            Assert.IsTrue(GrimoireRules.CanAbsorb(c, s, 7, book, 0, out GrimoireRefusal why, out string reason), reason);
            RuneInstance r = GrimoireRules.Absorb(c, s, book, 0, null, 5L);
            Assert.IsNotNull(r);
            Assert.AreEqual(2, s.runes.Count, "a second rune of the same kind");
            Assert.AreNotEqual(original, r.instanceId, "a new rune, not the original");
            Assert.AreEqual("wind", r.runeId);
            Assert.AreEqual(1, r.rank);
            Assert.AreEqual(50f, r.quality, 0.01f);
            Assert.AreEqual(0f, r.energy, "it holds no energy: the player must give it some");
            Assert.AreEqual("grimoire", r.origin.kind);
            Assert.AreEqual(absorbedBefore + 1, s.runesAbsorbed);
            Assert.AreEqual(350f, s.runes[0].energy, 0.01f, "the original rune is untouched by the absorption");
            Assert.IsFalse(book.Page(0).filled);
            Assert.IsFalse(book.Page(0).spent, "the default balance makes the page usable again");
            Assert.AreEqual(0, book.FilledCount());
        }

        [Test]
        public void APrintCanBeAbsorbedOnlyOnce()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 600f));
            GrimoireData book = Book(c);
            GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, s.runes[0].instanceId, 1L);
            Assert.IsNotNull(GrimoireRules.Absorb(c, s, book, 0, null, 5L));
            Assert.IsFalse(GrimoireRules.CanAbsorb(c, s, 99, book, 0, out GrimoireRefusal why, out string reason));
            Assert.AreEqual(GrimoireRefusal.PageBlank, why);
            Assert.IsNull(GrimoireRules.Absorb(c, s, book, 0, null, 6L), "the print is gone: no second rune");
            Assert.AreEqual(2, s.runes.Count);

            // When pages are one-time, the page is spent for good.
            RuneCatalog once = Custom(d => d.balance.grimoire.pageFreeAfterAbsorb = false);
            RuneSoulState s2 = Soul(once, ("wind", 1, 600f));
            GrimoireData book2 = Book(once, Author, "once");
            GrimoireRules.Imprint(once, s2, book2, Author, "Aria", 0, s2.runes[0].instanceId, 1L);
            GrimoireRules.Absorb(once, s2, book2, 0, null, 5L);
            Assert.IsTrue(book2.Page(0).spent);
            Assert.AreEqual(GrimoireRefusal.PageSpent, GrimoireRules.PlanImprint(once, s2, book2, Author, 0, s2.runes[0].instanceId).refusal);
            Assert.AreEqual(9, book2.BlankCount(), "a spent page is no longer blank");
        }

        [Test]
        public void AbsorbingNeedsRoomInTheSoulAndATooFullSoulLeavesThePrintOnItsPage()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 600f));
            GrimoireData book = Book(c);
            GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, s.runes[0].instanceId, 1L);
            Assert.IsFalse(GrimoireRules.CanAbsorb(c, s, 1, book, 0, out GrimoireRefusal why, out string reason), "one slot, held by the rune itself");
            Assert.AreEqual(GrimoireRefusal.NoRoom, why);
            StringAssert.Contains("no room", reason);
            Assert.IsTrue(book.Page(0).filled, "the print is still there");
            Assert.IsTrue(GrimoireRules.CanAbsorb(c, s, 2, book, 0, out _, out _));

            // A composite print needs its own slot cost.
            RuneSoulState s2 = Soul(c);
            RuneInstance comp = CompositeRune("Gravity Well", 2);
            s2.runes.Add(comp);
            GrimoireData book2 = Book(c, Author, "comp");
            comp.energy = 9000f;
            GrimoireRules.Imprint(c, s2, book2, Author, "Aria", 0, comp.instanceId, 1L);
            Assert.IsFalse(GrimoireRules.CanAbsorb(c, s2, 3, book2, 0, out GrimoireRefusal why2, out _), "two slots held, a print of two more needs four");
            Assert.AreEqual(GrimoireRefusal.NoRoom, why2);
            Assert.IsTrue(GrimoireRules.CanAbsorb(c, s2, 4, book2, 0, out _, out _));
        }

        [Test]
        public void AbsorbedEnergyIsNoneByDefaultAndAShareOfTheMaximumWhenTheBalanceSaysSo()
        {
            RuneCatalog some = Custom(d => d.balance.grimoire.absorbedEnergyFraction = 0.1f);
            RuneSoulState s = Soul(some, ("wind", 1, 900f));
            GrimoireData book = Book(some);
            GrimoireRules.Imprint(some, s, book, Author, "Aria", 0, s.runes[0].instanceId, 1L);
            RuneInstance r = GrimoireRules.Absorb(some, s, book, 0, null, 2L);
            Assert.AreEqual(100f, r.energy, 0.01f, "a tenth of the 1,000 a rank 1 rune holds");
            AssertTheValidatorWarnsAboutFreeEnergy(some);
        }

        private static void AssertTheValidatorWarnsAboutFreeEnergy(RuneCatalog c)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            c.Grimoire.Validate(errors, warnings);
            Assert.IsEmpty(errors);
            Assert.IsNotEmpty(warnings, "absorbed prints that hold energy make copying cheaper than earning: the validator says so");
        }

        [Test]
        public void AnotherMageWhoHoldsTheBookReadsItAndAbsorbsFromItButDoesNotWriteInIt()
        {
            RuneCatalog c = Catalog();
            RuneSoulState author = Soul(c, ("fire", 1, 900f));
            RuneSoulState reader = Soul(c, ("wind", 1, 100f));
            GrimoireData book = Book(c);
            Assert.IsTrue(GrimoireRules.Imprint(c, author, book, Author, "Aria", 0, author.runes[0].instanceId, 1L).ok);

            // The reader (a different soul, a different player) absorbs the print.
            Assert.IsTrue(GrimoireRules.CanAbsorb(c, reader, 7, book, 0, out _, out string why), why);
            RuneInstance r = GrimoireRules.Absorb(c, reader, book, 0, null, 3L);
            Assert.AreEqual("fire", r.runeId);
            Assert.AreEqual(0f, r.energy);
            Assert.AreEqual("Aria", r.origin.source, "it remembers whose book it came from");
            Assert.AreEqual(2, reader.runes.Count);
            Assert.AreEqual(1, author.runes.Count, "the author's soul is unchanged");

            // But not write, and not erase.
            Assert.AreEqual(GrimoireRefusal.NotAuthor, GrimoireRules.PlanImprint(c, reader, book, Reader, 1, reader.runes[0].instanceId).refusal);
            GrimoireRules.Imprint(c, author, book, Author, "Aria", 1, author.runes[0].instanceId, 1L);
            Assert.IsFalse(GrimoireRules.Erase(c, book, Reader, 1, out GrimoireRefusal refusal, out _));
            Assert.AreEqual(GrimoireRefusal.NotAuthor, refusal);
            Assert.IsTrue(book.Page(1).filled);
        }

        // ================================================================== erasing
        [Test]
        public void TheAuthorErasesAPrintAndGetsNothingBack()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 1, 600f));
            GrimoireData book = Book(c);
            GrimoireRules.Imprint(c, s, book, Author, "Aria", 0, s.runes[0].instanceId, 1L);
            float after = s.runes[0].energy;
            Assert.IsTrue(GrimoireRules.Erase(c, book, Author, 0, out _, out string why), why);
            Assert.IsFalse(book.Page(0).filled);
            Assert.IsFalse(book.Page(0).spent, "an erased page can be written again");
            Assert.AreEqual(after, s.runes[0].energy, "the energy of the imprint is not given back");
            Assert.IsFalse(GrimoireRules.Erase(c, book, Author, 0, out GrimoireRefusal refusal, out _), "nothing left to erase");
            Assert.AreEqual(GrimoireRefusal.PageBlank, refusal);
            RuneCatalog off = Custom(d => d.balance.grimoire.allowErase = false);
            GrimoireData b2 = Book(off, Author, "off");
            RuneSoulState s2 = Soul(off, ("wind", 1, 600f));
            GrimoireRules.Imprint(off, s2, b2, Author, "Aria", 0, s2.runes[0].instanceId, 1L);
            Assert.IsFalse(GrimoireRules.Erase(off, b2, Author, 0, out refusal, out _));
            Assert.AreEqual(GrimoireRefusal.ErasingOff, refusal);
        }

        // ================================================================== the book and the world's ledger
        [Test]
        public void EveryBookHasItsOwnSerialInTheLedgerAndANewBookHasTheBalancesPages()
        {
            RuneCatalog c = Catalog();
            var ledger = new GrimoireLedger();
            GrimoireData a = ledger.Create("same", Author, "Aria", c.Grimoire.pages, 1L);
            GrimoireData b = ledger.Create("same", Reader, "Bram", c.Grimoire.pages, 2L);
            Assert.AreNotEqual(a.serial, b.serial, "a serial that is taken is never reused");
            Assert.AreSame(a, ledger.Find(a.serial));
            Assert.AreSame(b, ledger.Find(b.serial));
            Assert.IsNull(ledger.Find("none"));
            Assert.IsNull(ledger.Find(null));
            Assert.AreEqual(c.Grimoire.pages, a.pages.Count);
            for (int i = 0; i < a.pages.Count; i++) Assert.AreEqual(i, a.pages[i].index);
            Assert.AreEqual(2, ledger.Count);
            int version = ledger.Version;
            ledger.Touch(a);
            Assert.AreEqual(1, a.revision);
            Assert.Greater(ledger.Version, version);
        }

        [Test]
        public void TheLedgerSurvivesAJsonRoundTripWithItsPrints()
        {
            RuneCatalog c = Catalog();
            var ledger = new GrimoireLedger();
            GrimoireData book = ledger.Create("abc123", Author, "Aria", c.Grimoire.pages, 7L);
            RuneSoulState s = Soul(c, ("wind", 2, 3000f));
            s.runes.Add(CompositeRune("Storm Lance", 2));
            s.runes[1].energy = 9000f;
            Assert.IsTrue(GrimoireRules.Imprint(c, s, book, Author, "Aria", 3, s.runes[0].instanceId, 11L).ok);
            Assert.IsTrue(GrimoireRules.Imprint(c, s, book, Author, "Aria", 5, s.runes[1].instanceId, 12L).ok);
            ledger.Touch(book);

            GrimoireLedger back = GrimoireLedger.FromJson(ledger.ToJson(), c.Grimoire.pages);
            GrimoireData b = back.Find("abc123");
            Assert.IsNotNull(b);
            Assert.AreEqual(Author, b.authorKey);
            Assert.AreEqual("Aria", b.authorName);
            Assert.AreEqual(1, b.revision);
            Assert.AreEqual(2, b.FilledCount());
            Assert.AreEqual("wind", b.Page(3).print.runeId);
            Assert.AreEqual(2, b.Page(3).print.rank);
            Assert.AreEqual(11L, b.Page(3).imprintedUtc);
            Assert.IsTrue(b.Page(5).print.IsComposite);
            Assert.AreEqual("Storm Lance", b.Page(5).print.composite.name);
            Assert.IsFalse(b.Page(0).filled);
        }

        [Test]
        public void BrokenLedgerTextGivesAnEmptyLedgerAndABrokenBookIsSkipped()
        {
            Assert.AreEqual(0, GrimoireLedger.FromJson("", 10).Count);
            Assert.AreEqual(0, GrimoireLedger.FromJson(null, 10).Count);
            Assert.AreEqual(0, GrimoireLedger.FromJson("not json {", 10).Count);
            GrimoireLedger two = GrimoireLedger.FromJson("{\"books\":[{\"serial\":\"\"},{\"serial\":\"ok1\",\"authorKey\":\"k\"},{\"serial\":\"OK1\"}]}", 4);
            Assert.AreEqual(1, two.Count, "a book without a serial is skipped, and a serial is one book");
            Assert.AreEqual(4, two.Find("ok1").pages.Count, "its pages are made");
        }

        [Test]
        public void APageCountOnlyEverGrowsSoABalanceChangeNeverDestroysAPrint()
        {
            RuneCatalog c = Catalog();
            GrimoireData book = Book(c);
            RuneSoulState s = Soul(c, ("wind", 1, 900f));
            GrimoireRules.Imprint(c, s, book, Author, "Aria", 9, s.runes[0].instanceId, 1L);
            book.EnsureLists(3);
            Assert.AreEqual(10, book.pages.Count, "a smaller balance does not cut pages off");
            Assert.IsTrue(book.Page(9).filled);
            book.EnsureLists(12);
            Assert.AreEqual(12, book.pages.Count);
            Assert.IsFalse(book.Page(11).filled);
            Assert.IsTrue(book.Page(9).filled);
        }

        // ================================================================== the soul's part
        [Test]
        public void TheSoulRemembersItsOwnGrimoireAndOlderSavesStillLoad()
        {
            RuneSoulState s = new RuneSoulState { grimoireSerial = "abc123", grimoireGiven = true };
            RuneSoulState back = RuneSoulState.FromJson(s.ToJson());
            Assert.AreEqual("abc123", back.grimoireSerial);
            Assert.IsTrue(back.grimoireGiven);
            Assert.AreEqual(RuneSoulState.CurrentVersion, back.version);

            RuneSoulState old = RuneSoulState.FromJson("{\"version\":5}");
            Assert.AreEqual("", old.grimoireSerial, "a save from before the grimoire has none yet: the Mage is given one");
            Assert.IsFalse(old.grimoireGiven);
            Assert.AreEqual("", RuneSoulState.FromJson("{\"grimoireSerial\":null}").grimoireSerial);
            Assert.AreEqual(6, RuneSoulState.CurrentVersion);
        }

        // ================================================================== views and request texts
        [Test]
        public void TheViewShowsThePagesAndWhatTheReaderMayDo()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = Soul(c, ("wind", 2, 4000f));
            GrimoireData book = Book(c);
            GrimoireRules.Imprint(c, s, book, Author, "Aria", 1, s.runes[0].instanceId, 1L);

            GrimoireView mine = GrimoireView.Build(c, book, "grimoire:abc123:Aria", true, GrimoireRules.MayWrite(c, book, Author));
            Assert.AreEqual("Grimoire of Aria", mine.title);
            Assert.IsTrue(mine.isAuthor && mine.canWrite && mine.canErase);
            Assert.AreEqual(10, mine.pages.Count);
            Assert.AreEqual(1, mine.Filled);
            Assert.IsTrue(mine.Page(1).filled);
            StringAssert.Contains("Wind", mine.Page(1).name);
            Assert.AreEqual(2, mine.Page(1).rank);
            Assert.AreEqual("wind", mine.Page(1).runeId);
            Assert.AreEqual("Aria", mine.Page(1).by);
            Assert.IsFalse(mine.Page(0).filled);
            Assert.IsNull(mine.Page(99));

            GrimoireView theirs = GrimoireView.Build(c, book, "grimoire:abc123:Aria", false, GrimoireRules.MayWrite(c, book, Reader));
            Assert.IsFalse(theirs.isAuthor);
            Assert.IsFalse(theirs.canWrite, "a reader does not write");
            Assert.IsFalse(theirs.canErase);

            GrimoireView back = JsonUtility.FromJson<GrimoireView>(JsonUtility.ToJson(mine));
            back.EnsureLists();
            Assert.IsTrue(mine.SameAs(back));
            Assert.Less(JsonUtility.ToJson(mine).Length, RuneNetLimits.MaxGrimoireViewLength);
            back.revision++;
            Assert.IsFalse(mine.SameAs(back));
            Assert.AreEqual(0, GrimoireView.Build(c, null, "x", true, true).pages.Count);
        }

        [Test]
        public void TheLargestBookStillFitsTheNetworkLimit()
        {
            RuneCatalog big = Custom(d => d.balance.grimoire.pages = 64);
            GrimoireData book = Book(big);
            RuneInstance comp = CompositeRune("A Composite Rune With A Rather Long Name Indeed", 2);
            foreach (GrimoirePage p in book.pages)
            {
                p.filled = true;
                p.print = comp.Clone();
                p.imprintedBy = new string('n', 16);
            }
            GrimoireView v = GrimoireView.Build(big, book, GrimoireIds.Make("abc123", "Aria"), true, true);
            Assert.Less(JsonUtility.ToJson(v).Length, RuneNetLimits.MaxGrimoireViewLength, "64 full pages of composite runes");
        }

        [Test]
        public void TheRequestTextsAreSplitAndMalformedOnesRefused()
        {
            string id = GrimoireIds.Make("abc123", "Aria");
            string text = GrimoireRequestText.Imprint(id, "0123456789abcdef");
            Assert.IsTrue(GrimoireRequestText.TrySplitImprint(text, out string item, out string rune));
            Assert.AreEqual(id, item);
            Assert.AreEqual("0123456789abcdef", rune);
            Assert.Less(text.Length, RuneNetLimits.MaxRequestText, "it fits an ordinary request");
            foreach (string bad in new[] { null, "", "|", id, id + "|", "|rune", id + "||x", id + "|a|b", "rune:wind:1:5:0|x", id + "|" + new string('x', 65) })
                Assert.IsFalse(GrimoireRequestText.TrySplitImprint(bad, out _, out _), "'" + bad + "'");

            Assert.AreEqual(3, GrimoireRequestText.PageOf(3f, 10));
            Assert.AreEqual(0, GrimoireRequestText.PageOf(0f, 10));
            Assert.AreEqual(-1, GrimoireRequestText.PageOf(10f, 10), "past the last page");
            Assert.AreEqual(-1, GrimoireRequestText.PageOf(-1f, 10));
            Assert.AreEqual(-1, GrimoireRequestText.PageOf(2.5f, 10), "not a whole number");
            Assert.AreEqual(-1, GrimoireRequestText.PageOf(float.NaN, 10));
            Assert.AreEqual(-1, GrimoireRequestText.PageOf(float.PositiveInfinity, 10));
        }

        // ================================================================== energies on the network (the update's optimisation)
        [Test]
        public void WhenOnlyEnergyChangedJustTheEnergiesAreSentAndTheyApplyToTheListTheyWereMadeFor()
        {
            RuneCatalog c = Catalog();
            RuneSoulState server = Soul(c, ("wind", 1, 600f), ("fire", 2, 3000f));
            var list = new RuneListView { version = 7 };
            foreach (RuneInstance r in server.runes) list.runes.Add(r.Clone());

            server.runes[0].energy = 350f;
            server.runes[1].energy = 0f;
            RuneEnergyView delta = RuneEnergyView.Of(server.runes, 7);
            RuneEnergyView back = JsonUtility.FromJson<RuneEnergyView>(JsonUtility.ToJson(delta));
            Assert.Less(JsonUtility.ToJson(delta).Length, JsonUtility.ToJson(list).Length / 4, "a few numbers, not the whole list");
            Assert.IsTrue(back.ApplyTo(list));
            Assert.AreEqual(350f, list.runes[0].energy);
            Assert.AreEqual(0f, list.runes[1].energy);
            Assert.AreEqual(1, list.runes[0].rank, "nothing else changes");

            // Another version of the list: ignored, nothing changes.
            list.runes[0].energy = 111f;
            Assert.IsFalse(new RuneEnergyView { baseVersion = 6, ids = back.ids, energies = back.energies }.ApplyTo(list));
            Assert.AreEqual(111f, list.runes[0].energy);
            // Malformed or hostile numbers.
            Assert.IsFalse(new RuneEnergyView { baseVersion = 7, ids = new List<string> { "a" }, energies = new List<float>() }.ApplyTo(list), "ids and energies of different lengths");
            Assert.IsFalse(RuneEnergyView.Of(null, 7).ApplyTo(null));
            var odd = new RuneEnergyView { baseVersion = 7, ids = new List<string> { list.runes[0].instanceId, "unknown", list.runes[1].instanceId },
                                           energies = new List<float> { float.NaN, 5f, -50f } };
            Assert.IsTrue(odd.ApplyTo(list), "an unknown rune is skipped");
            Assert.AreEqual(111f, list.runes[0].energy, "a number that is not finite is ignored");
            Assert.AreEqual(0f, list.runes[1].energy, "energy never goes below 0");
            Assert.IsTrue(new RuneEnergyView { baseVersion = 7 }.ApplyTo(list), "an update with no runes is valid data");
        }

        // ================================================================== the balance and the data
        [Test]
        public void TheBuiltInGrimoireBalanceIsValidAndIsTheSpecOfTheFeature()
        {
            RuneCatalog c = Catalog();
            GrimoireBalance g = c.Grimoire;
            var errors = new List<string>();
            var warnings = new List<string>();
            g.Validate(errors, warnings);
            Assert.IsEmpty(errors);
            Assert.IsEmpty(warnings);
            Assert.IsTrue(g.giveOnSpawn, "a Mage begins with a grimoire");
            Assert.AreEqual(10, g.pages);
            Assert.Greater(g.imprintEnergyFraction, 0f, "imprinting always costs energy");
            Assert.AreEqual(0f, g.absorbedEnergyFraction, "an absorbed print has no energy");
            Assert.IsTrue(g.onlyAuthorImprints);
            Assert.IsFalse(c.Errors.Any(e => e.Contains("Grimoire")));
        }

        [Test]
        public void ABrokenGrimoireBalanceIsReported()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            new GrimoireBalance { pages = 0, imprintEnergyFraction = 0f, bookName = " " }.Validate(errors, warnings);
            Assert.AreEqual(2, errors.Count, string.Join("; ", errors));
            Assert.IsNotEmpty(warnings);
            errors.Clear();
            warnings.Clear();
            new GrimoireBalance { pages = 100, imprintEnergyFraction = 1f }.Validate(errors, warnings);
            Assert.IsEmpty(errors);
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void OlderDataIsUpgradedWithTheGrimoireAndTheBalanceSurvivesTheAssetsJson()
        {
            RuneMagicData d = RuneDefaults.Create();
            Assert.AreEqual(9, RuneMagicData.CurrentVersion, "charging is data version 9 (the grimoire was 8)");
            d.version = 7;
            d.balance.grimoire = null;
            Assert.IsTrue(d.Upgrade());
            Assert.IsNotNull(d.balance.grimoire);
            Assert.AreEqual(RuneMagicData.CurrentVersion, d.version);
            Assert.AreEqual(10, d.balance.grimoire.pages);

            d.balance.grimoire.pages = 21;
            d.balance.grimoire.imprintEnergyFraction = 0.4f;
            RuneMagicData back = d.Clone();
            Assert.AreEqual(21, back.balance.grimoire.pages);
            Assert.AreEqual(0.4f, back.balance.grimoire.imprintEnergyFraction, 1e-6f);
            Assert.IsFalse(back.Upgrade(), "a current asset is not upgraded again");
        }

        [Test]
        public void TheGrimoireActionIsAnInputActionNameAndNoKey()
        {
            var settings = new RuneMagicSettings();
            CollectionAssert.Contains(settings.InputActionNames().ToList(), "Grimoire");
            settings.grimoireAction = "  ";
            CollectionAssert.DoesNotContain(settings.InputActionNames().ToList(), "  ");
            Assert.AreEqual(1, settings.InputActionNames().Count(n => n == "Spellbook"), "the others are unchanged");
        }
    }
}
#endif
