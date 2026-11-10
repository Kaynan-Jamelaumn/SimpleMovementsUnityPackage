#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AnimalHusbandrySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of Phase 5 of animal husbandry (plain C#: no scene, no assets): trading (crating an animal, its crate's
    /// code, letting it out to a new owner - validated, once, with its history and lineage kept - crates kept through a save
    /// and dropped after their wait, what a crated animal is worth), habitat upgrades (room, comfort, cleanliness and
    /// production by level, prices and rights), upkeep (falling due, disrepair, paying it), the three machines (the feed
    /// hopper, the automatic waterer, egg trays), breed yields, catch-ups with machines at work, old saves, the pen's
    /// picture, and wool quality in Crafting.
    /// </summary>
    public class AnimalTradeTests
    {
        // ---------------------------------------------------------------- helpers
        private const double Start = 48.0;
        private static readonly string Feed = AnimalIds.Feed("chicken_feed");

        private static AnimalCatalog Catalog(AnimalRules r = null) => AnimalCatalog.BuiltIn(r ?? AnimalRules.Defaults());

        private static AnimalSimContext Ctx(IAnimalCatalog cat, AnimalBook book) => new AnimalSimContext
        {
            catalog = cat,
            climate = new CityClimateSource(AgriSettings.Defaults()),
            ownerPresent = (k, t) => true,
            notices = new List<AnimalNotice>(),
            book = book,
            realHours = 1000.0,
        };

        private static AnimalHerd Pen(AnimalBook book, string id, AnimalHabitatKind kind, float area, double now, float feeder = 50000f, float water = 5000f)
        {
            var spec = new AnimalPenSpec { id = id, name = id, kind = kind, area = area, feederCapacity = feeder, waterCapacity = water, troughSurface = 0f, grassCapacity = 3f };
            return book.EnsurePen(spec, "Test", "", now);
        }

        private static AnimalActor Actor(string key, AnimalRights rights, Dictionary<string, int> carried = null) => new AnimalActor
        {
            key = key,
            name = key,
            rights = rights,
            count = id => carried != null && carried.TryGetValue(id, out int n) ? n : 0,
            waterKind = (int)WaterSourceKind.Well,
        };

        private static AnimalRecord Release(AnimalBook book, AnimalSimContext ctx, AnimalHerd herd, string owner, string species, string breed, double now,
            AnimalSex sex, GrowthStage stage = GrowthStage.Adult, int seed = 12345)
        {
            string item = AnimalIds.Livestock(species, breed);
            var actor = Actor(owner, AnimalRights.All, new Dictionary<string, int> { [item] = 1 });
            var r = new AnimalRequest { kind = AnimalActionKind.Release, pen = herd.pen.id, item = item, seq = 1 };
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            var taken = new AnimalTaken { count = 1, qualities = new List<float> { 0.6f }, codes = new List<int> { AnimalItemCode.Livestock(sex, stage, seed) } };
            AnimalResult res = AnimalCommands.Commit(book, plan, taken, now, ctx);
            Assert.IsTrue(res.ok, res.message);
            AnimalRecord a = book.Find(res.animal);
            Typical(a);
            Thriving(a);
            return a;
        }

        private static void Typical(AnimalRecord a)
        {
            a.genes ??= new AnimalGenes();
            a.genes.fertility = 0.5f;
            a.genes.temperament = 0.5f;
            a.genes.productionRate = 0.5f;
            a.genes.productionQuality = 0.5f;
            a.genes.rare = 0;
        }

        private static void Thriving(AnimalRecord a)
        {
            a.satiety = 90f;
            a.hydration = 90f;
            a.nutrition = 90f;
            a.health = 100f;
            a.welfare = 85f;
            a.hygiene = 90f;
            a.comfort = 85f;
            a.stress = 5f;
        }

        /// <summary>Plans and commits a request, paying what it asks (its fee) and giving what it takes; asserts it went through.</summary>
        private static AnimalResult Do(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now, AnimalPlan into = null, List<int> codes = null)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsFalse(plan.Refused, $"{r.kind}: {plan.refusal?.message}");
            var taken = new AnimalTaken { count = plan.takeCount, qualities = new List<float>(), codes = new List<int>(), paid = plan.fee };
            for (int i = 0; i < plan.takeCount; i++)
            {
                taken.qualities.Add(0.8f);
                taken.codes.Add(codes != null && i < codes.Count ? codes[i] : 0);
            }
            AnimalResult res = AnimalCommands.Commit(book, plan, taken, now, ctx);
            Assert.IsTrue(res.ok, $"{r.kind}: {res.message}");
            if (into != null)
            {
                into.give.Clear();
                into.give.AddRange(plan.give);
                into.fee = plan.fee;
            }
            return res;
        }

        private static string Refusal(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsTrue(plan.Refused, $"{r.kind} should be refused");
            return plan.refusal.message;
        }

        /// <summary>Crates an animal for its owner; returns the crate's code.</summary>
        private static int Crate(AnimalBook book, AnimalSimContext ctx, AnimalRecord a, double now)
        {
            var got = new AnimalPlan();
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Crate, pen = a.pen, animal = a.id }, Actor(a.owner, AnimalRights.All), now, got);
            Assert.AreEqual(1, got.give.Count);
            Assert.AreEqual(1, got.give[0].count);
            return got.give[0].codes[0];
        }

        /// <summary>Lets a crate (by its code, chosen as the window chooses it) out into a pen for a player: the result (not asserted).</summary>
        private static AnimalResult LetOut(AnimalBook book, AnimalSimContext ctx, AnimalHerd herd, string who, string item, int code, double now, string name = "", int chosen = -1)
        {
            var actor = Actor(who, AnimalRights.All, new Dictionary<string, int> { [item] = 1 });
            var r = new AnimalRequest { kind = AnimalActionKind.Release, pen = herd.pen.id, item = item, text = name, code = chosen >= 0 ? chosen : code };
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            if (plan.Refused) return plan.refusal;
            var taken = new AnimalTaken { count = 1, qualities = new List<float> { 0.6f }, codes = new List<int> { code } };
            return AnimalCommands.Commit(book, plan, taken, now, ctx);
        }

        /// <summary>The herd kept fed (chicken feed) and watered, the floor left as it gets, stepped hour by hour by the book.</summary>
        private static void Keep(AnimalBook book, AnimalSimContext ctx, AnimalHerd h, double from, int hours, bool cleanFloor = true)
        {
            for (int hr = 1; hr <= hours; hr++)
            {
                h.pen.water = h.spec.waterCapacity;
                if (cleanFloor) h.pen.dirt = 0f;
                if (h.pen.FeederPortions < 200f) AnimalCommands.Stock(h, book.Catalog.Food("chicken_feed"), 1000f, 0.8f, "test", from + hr - 1);
                book.BringUpToDate(h, from + hr, ctx);
            }
        }

        private static int Made(AnimalRecord a, string product)
        {
            int n = 0;
            foreach (AnimalProductEntry e in a.productLog)
                if (e != null && e.product == product && e.kind == AnimalProductEntryKind.Made) n += e.units;
            return n;
        }

        // ---------------------------------------------------------------- crate codes
        [Test]
        public void ACratesCodeNamesItsAnimalItsStageAndGradeAndStaysUnderTwoToThe30()
        {
            int top = AnimalItemCode.Crated(AnimalSex.Male, GrowthStage.Senior, 15, AnimalItemCode.MaxCrateSerial);
            Assert.Greater(top, 1);
            Assert.Less((long)top, 1L << 30, "every per-copy code stays under 2^30");
            Assert.IsTrue(AnimalItemCode.TryCrated(top, out AnimalSex sex, out GrowthStage stage, out int grade, out long serial));
            Assert.AreEqual(AnimalSex.Male, sex);
            Assert.AreEqual(GrowthStage.Senior, stage);
            Assert.AreEqual(15, grade);
            Assert.AreEqual(AnimalItemCode.MaxCrateSerial, serial);
            Assert.IsFalse(AnimalItemCode.TryLivestock(top, out _, out _, out _), "a crated animal is not a fresh crate's seed");

            int low = AnimalItemCode.Crated(AnimalSex.Female, GrowthStage.Baby, 0, 1);
            Assert.IsTrue(AnimalItemCode.TryCrated(low, out sex, out stage, out grade, out serial));
            Assert.AreEqual(AnimalSex.Female, sex);
            Assert.AreEqual(GrowthStage.Baby, stage);
            Assert.AreEqual(0, grade);
            Assert.AreEqual(1L, serial);

            int plain = AnimalItemCode.Livestock(AnimalSex.Female, GrowthStage.Adult, 0xFFFFFF);
            Assert.IsFalse(AnimalItemCode.TryCrated(plain, out _, out _, out _, out _), "a plain crate holds no animal of the world");
            Assert.IsTrue(AnimalItemCode.TryLivestock(plain, out _, out _, out _));
            Assert.IsFalse(AnimalItemCode.TryCrated(0, out _, out _, out _, out _));
            Assert.IsFalse(AnimalItemCode.TryCrated(AnimalItemCode.Crated(AnimalSex.Female, GrowthStage.Adult, 7, 0), out _, out _, out _, out _), "no serial: no animal");
            Assert.IsFalse(AnimalItemCode.TryCrated(AnimalItemCode.Crated(AnimalSex.Female, GrowthStage.Adult, 7, AnimalItemCode.MaxCrateSerial + 1), out _, out _, out _, out _));
        }

        // ---------------------------------------------------------------- crating and changing hands
        [Test]
        public void ACratedAnimalLeavesTheWorldAndIsLetOutWholeByItsNewOwnerOnce()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalHerd other = Pen(book, "other", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female);
            hen.name = "Bella";
            hen.affinity = 80f;
            hen.genes.productionQuality = 0.77f;
            string id = hen.id;
            string item = AnimalIds.Livestock("chicken", "leghorn");

            // Only its owner crates it.
            StringAssert.Contains("owner", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Crate, pen = coop.pen.id, animal = id }, Actor("bob", AnimalRights.All), Start));

            int code = Crate(book, ctx, hen, Start + 1.0);
            float bond = hen.affinity; // as it was crated (a crate does not live: it stays so)
            Assert.IsTrue(AnimalItemCode.TryCrated(code, out AnimalSex sex, out GrowthStage stage, out int grade, out long serial));
            Assert.AreEqual(AnimalSex.Female, sex);
            Assert.AreEqual(GrowthStage.Adult, stage);
            Assert.AreEqual(AnimalTrade.Grade(hen, book.Rules), grade);
            Assert.IsNull(book.Find(id), "out of the world while crated");
            Assert.IsFalse(coop.animals.Contains(hen));
            Assert.AreEqual(0, book.LivingOwnedBy("ann"));
            Assert.AreEqual(1, book.CratedCount);
            Assert.AreEqual(AnimalStatus.Crated, book.Crated(serial).animal.status);
            Assert.IsTrue(hen.events.Exists(e => e.kind == AnimalEventKind.Crated), "its history keeps it");
            // A crate does not live: nothing steps it.
            double lived = hen.livedTo;
            book.BringUpToDate(coop, Start + 48.0, ctx);
            Assert.AreEqual(lived, hen.livedTo);

            // Bob lets it out (he bought it in a trade): the very animal, now his.
            AnimalResult res = LetOut(book, ctx, other, "bob", item, code, Start + 50.0);
            Assert.IsTrue(res.ok, res.message);
            Assert.AreEqual(id, res.animal);
            AnimalRecord back = book.Find(id);
            Assert.AreSame(hen, back);
            Assert.AreEqual("bob", back.owner);
            Assert.AreEqual("Bella", back.name, "its name, history and lineage come with it");
            Assert.AreEqual(0.77f, back.genes.productionQuality, 1e-6f);
            Assert.AreEqual(other.pen.id, back.pen);
            Assert.AreEqual(AnimalStatus.Alive, back.status);
            Assert.AreEqual(bond * book.Rules.trade.affinityKept, back.affinity, 1e-3f, "a new keeper starts with a share of its old bond");
            Assert.Greater(bond, 70f);
            Assert.IsTrue(back.events.Exists(e => e.kind == AnimalEventKind.ChangedHands));
            Assert.AreEqual(Start + 50.0, back.livedTo, 1e-6, "it lives again from when it is let out");
            Assert.AreEqual(1, book.LivingOwnedBy("bob"));
            Assert.AreEqual(0, book.CratedCount);

            // A crate is let out once: the same code again (a copy that should not exist) finds it empty.
            AnimalResult again = LetOut(book, ctx, other, "bob", item, code, Start + 51.0);
            Assert.IsFalse(again.ok);
            StringAssert.Contains("empty", again.message);
            Assert.AreEqual(1, book.LivingOwnedBy("bob"));
        }

        [Test]
        public void ACrateIsOnlyLetOutAsWhatItSaysAndItsCraterKeepsTheBond()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female);
            hen.affinity = 70f;
            int code = Crate(book, ctx, hen, Start);

            // Its code in another kind of crate (a hand-made copy) is refused, and nothing changes.
            AnimalResult wrong = LetOut(book, ctx, coop, "ann", AnimalIds.Livestock("chicken", "silkie"), code, Start + 1.0);
            Assert.IsFalse(wrong.ok);
            Assert.AreEqual(1, book.CratedCount);

            AnimalResult res = LetOut(book, ctx, coop, "ann", AnimalIds.Livestock("chicken", "leghorn"), code, Start + 2.0, "Hetty");
            Assert.IsTrue(res.ok, res.message);
            Assert.AreEqual("ann", hen.owner);
            Assert.AreEqual(70f, hen.affinity, 1e-4f, "its own keeper keeps all of the bond");
            Assert.AreEqual("Hetty", hen.name, "a name given as it is let out");
            Assert.IsFalse(hen.events.Exists(e => e.kind == AnimalEventKind.ChangedHands));
        }

        [Test]
        public void AnAnimalNotFitToTravelIsNotCrated()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd barn = Pen(book, "barn", AnimalHabitatKind.Barn | AnimalHabitatKind.Pasture, 200f, Start);
            AnimalRecord doe = Release(book, ctx, barn, "ann", "goat", "saanen", Start, AnimalSex.Female);
            AnimalActor ann = Actor("ann", AnimalRights.All);
            var crate = new AnimalRequest { kind = AnimalActionKind.Crate, pen = barn.pen.id, animal = doe.id };

            doe.health = 10f;
            StringAssert.Contains("too weak", Refusal(book, ctx, crate, ann, Start));
            doe.health = 100f;

            doe.pregnancy = new AnimalPregnancy { active = true, dueAt = Start + 100.0, litter = 1 };
            StringAssert.Contains("expecting", Refusal(book, ctx, crate, ann, Start));
            doe.pregnancy = new AnimalPregnancy();

            AnimalRecord kid = Release(book, ctx, barn, "ann", "goat", "saanen", Start, AnimalSex.Male, GrowthStage.Newborn, 777);
            kid.mother = doe.id;
            Assert.IsTrue(AnimalBreeding.Suckles(kid, cat.Species("goat")), "the test needs a kid at the teat");
            StringAssert.Contains("nursing", Refusal(book, ctx, crate, ann, Start));

            AnimalRules off = AnimalRules.Defaults();
            off.trade.allowCrating = false;
            Assert.IsNotNull(AnimalTrade.WhyNotCrate(kid, cat.Species("goat"), null, cat, off), "the rules may forbid it");
        }

        [Test]
        public void CratedAnimalsAreSavedAndGoneAfterTheirWaitInRealDays()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            ctx.realHours = 100.0;
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female);
            string id = hen.id;
            int code = Crate(book, ctx, hen, Start);
            AnimalItemCode.TryCrated(code, out _, out _, out _, out long serial);

            AnimalWorldSave save = book.Export(Start, 0);
            Assert.AreEqual(1, save.crated.Count);
            var loaded = new AnimalBook(cat, "w");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(JsonUtility.ToJson(save)));
            Assert.AreEqual(1, loaded.CratedCount);
            Assert.AreEqual(serial, loaded.CrateSerial);
            Assert.AreEqual(id, loaded.Crated(serial).animal.id);
            Assert.AreEqual(AnimalStatus.Crated, loaded.Crated(serial).animal.status);
            Assert.IsNull(loaded.Find(id), "still out of the world after loading");

            float days = cat.Rules.trade.crateKeepDays;
            Assert.AreEqual(0, loaded.PruneCrates(100.0 + days * 24.0 - 1.0), "within its wait");
            Assert.AreEqual(1, loaded.PruneCrates(100.0 + days * 24.0 + 1.0), "past its wait: the carrier sold it on");
            Assert.AreEqual(0, loaded.CratedCount);

            // One crated with no real hour known starts its wait when the world first counts it.
            var fresh = new AnimalBook(cat, "w2");
            AnimalSimContext ctx2 = Ctx(cat, fresh);
            ctx2.realHours = 0.0;
            AnimalHerd coop2 = Pen(fresh, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord other = Release(fresh, ctx2, coop2, "ann", "chicken", "leghorn", Start, AnimalSex.Female, GrowthStage.Adult, 555);
            Crate(fresh, ctx2, other, Start);
            Assert.AreEqual(0, fresh.PruneCrates(5000.0));
            Assert.AreEqual(0, fresh.PruneCrates(5000.0 + days * 24.0 - 1.0));
            Assert.AreEqual(1, fresh.PruneCrates(5000.0 + days * 24.0 + 1.0));
        }

        [Test]
        public void AFineAnimalIsWorthMoreAndAnAdultMoreThanAYoungOne()
        {
            AnimalRules rules = AnimalRules.Defaults();
            var poor = new AnimalRecord { health = 40f, nutrition = 35f, welfare = 30f, genes = new AnimalGenes() };
            var fine = new AnimalRecord { health = 100f, nutrition = 95f, welfare = 90f, genes = new AnimalGenes { productionQuality = 0.9f, productionRate = 0.9f, efficiency = 0.8f, resistance = 0.8f, fertility = 0.8f, longevity = 0.8f } };
            int gp = AnimalTrade.Grade(poor, rules), gf = AnimalTrade.Grade(fine, rules);
            Assert.Less(gp, gf);
            fine.genes.rare = 1;
            Assert.Greater(AnimalTrade.Grade(fine, rules), gf, "a rare trait is worth something");
            fine.genes.inbreeding = 0.8f;
            fine.genes.rare = 0;
            Assert.Less(AnimalTrade.Grade(fine, rules), gf, "close breeding is not");
            Assert.That(AnimalTrade.Grade(fine, rules), Is.InRange(0, 15));

            Assert.Greater(AnimalTrade.ValueFactor(GrowthStage.Adult, 8, rules), AnimalTrade.ValueFactor(GrowthStage.Baby, 8, rules));
            Assert.Greater(AnimalTrade.ValueFactor(GrowthStage.Adult, 15, rules), AnimalTrade.ValueFactor(GrowthStage.Adult, 0, rules));
            Assert.AreEqual(rules.trade.adultWorth * (1f + rules.trade.gradeSpread), AnimalTrade.ValueFactor(GrowthStage.Adult, 15, rules), 1e-4f);
            Assert.AreEqual(rules.trade.adultWorth * (1f - rules.trade.gradeSpread), AnimalTrade.ValueFactor(GrowthStage.Adult, 0, rules), 1e-4f);
        }

        // ---------------------------------------------------------------- habitat upgrades
        [Test]
        public void AnImprovedPenTakesMoreAnimalsForThePriceShownToThoseWhoKeepAnimalsThere()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            float per = cat.Species("chicken").habitat.areaPerAnimal;
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, per * 2f + 0.001f, Start);
            Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female, GrowthStage.Adult, 1);
            Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female, GrowthStage.Adult, 2);
            AnimalSpeciesData chicken = cat.Species("chicken");
            Assert.IsNotNull(AnimalCommands.RoomFor(book, coop, chicken, null), "two hens fill it as built");

            var tender = Actor("tom", AnimalRights.Look | AnimalRights.Tend);
            var keeper = Actor("ann", AnimalRights.All);
            long price = AnimalUpgrades.NextLevelPrice(coop.pen, coop.spec, book.Rules);
            Assert.Greater(price, 0L);
            var up = new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.LevelText, expected = price };
            StringAssert.Contains("keeps animals", Refusal(book, ctx, up, tender, Start));
            var cheaper = up;
            cheaper.expected = price - 1;
            StringAssert.Contains("costs", Refusal(book, ctx, cheaper, keeper, Start), "nobody pays what they did not agree to");

            // Paid short (the purse changed between the plan and the take): nothing is built.
            AnimalPlan plan = AnimalCommands.Plan(book, up, keeper, Start, ctx);
            Assert.AreEqual(price, plan.fee);
            AnimalResult shortPaid = AnimalCommands.Commit(book, plan, new AnimalTaken { paid = price - 1 }, Start, ctx);
            Assert.IsFalse(shortPaid.ok);
            Assert.AreEqual(1, AnimalUpgrades.Level(coop.pen, book.Rules));

            Do(book, ctx, up, keeper, Start);
            Assert.AreEqual(2, AnimalUpgrades.Level(coop.pen, book.Rules));
            Assert.AreEqual("ann", coop.pen.upgradedBy);
            Assert.IsNotNull(AnimalCommands.RoomFor(book, coop, chicken, null), "a quarter more room is not yet room for a third hen");
            long third = AnimalUpgrades.NextLevelPrice(coop.pen, coop.spec, book.Rules);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.LevelText, expected = third }, keeper, Start);
            Assert.AreEqual(3, AnimalUpgrades.Level(coop.pen, book.Rules));
            Assert.IsNull(AnimalCommands.RoomFor(book, coop, chicken, null), "half as much room again: a third hen fits");
            Assert.AreEqual(-1L, AnimalUpgrades.NextLevelPrice(coop.pen, coop.spec, book.Rules));
            StringAssert.Contains("as good as it gets", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.LevelText }, keeper, Start));

            // A set capacity grows with it too.
            AnimalHerd hutch = Pen(book, "hutch", AnimalHabitatKind.Hutch, 100f, Start);
            hutch.spec.capacityOverride = 4;
            hutch.pen.level = 3;
            Assert.AreEqual(6, AnimalUpgrades.Capacity(hutch.pen, hutch.spec, book.Rules));
        }

        [Test]
        public void ALevelsPriceAndUpkeepFollowThePensSize()
        {
            AnimalRules rules = AnimalRules.Defaults();
            var pen = new AnimalPenRecord();
            int cost = rules.upgrades.levels[0].cost;
            Assert.AreEqual((long)cost, AnimalUpgrades.NextLevelPrice(pen, new AnimalPenSpec { area = 24f }, rules));
            Assert.AreEqual((long)cost * 2, AnimalUpgrades.NextLevelPrice(pen, new AnimalPenSpec { area = 48f }, rules));
            Assert.AreEqual((long)Math.Round(cost * 0.5), AnimalUpgrades.NextLevelPrice(pen, new AnimalPenSpec { area = 4f }, rules), "never less than half");
            Assert.AreEqual((long)cost * 4, AnimalUpgrades.NextLevelPrice(pen, new AnimalPenSpec { area = 1000f }, rules), "never more than four times");
            Assert.AreEqual(0f, AnimalUpgrades.UpkeepPerDay(pen, new AnimalPenSpec { area = 24f }, rules), "a basic pen without machines costs nothing to keep");
            pen.level = 2;
            pen.hopper = true;
            Assert.AreEqual(rules.upgrades.levels[0].upkeepPerDay * 2f + rules.upgrades.hopperUpkeepPerDay, AnimalUpgrades.UpkeepPerDay(pen, new AnimalPenSpec { area = 48f }, rules), 1e-4f);
        }

        [Test]
        public void AnImprovedPenIsRoomierMoreComfortableCleanerAndMoreProductive()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd basic = Pen(book, "basic", AnimalHabitatKind.Coop, 12f, Start);
            AnimalHerd fine = Pen(book, "fine", AnimalHabitatKind.Coop, 12f, Start);
            fine.pen.level = 3;
            for (int i = 0; i < 8; i++)
            {
                Release(book, ctx, basic, "ann", "chicken", "silkie", Start, AnimalSex.Female, GrowthStage.Adult, 100 + i);
                Release(book, ctx, fine, "ann", "chicken", "silkie", Start, AnimalSex.Female, GrowthStage.Adult, 100 + i);
            }
            AnimalPenLevelSpec top = AnimalUpgrades.LevelSpec(3, book.Rules);
            AnimalStepFacts f1 = AnimalHabitat.Facts(basic, basic.spec, cat, book.Rules, Start, null, null);
            AnimalStepFacts f3 = AnimalHabitat.Facts(fine, fine.spec, cat, book.Rules, Start, null, null);
            Assert.AreEqual(f1.area * (1f + top.space), f3.area, 1e-3f, "room: crowding is judged against the usable area");
            Assert.Less(f3.density, f1.density);
            Assert.AreEqual(0f, f1.comfortBonus);
            Assert.AreEqual(top.comfort, f3.comfortBonus, 1e-4f);
            Assert.AreEqual(top.production, f3.productionBonus, 1e-4f);

            // Comfort: in the cold an improved pen's animals settle higher.
            AnimalSpeciesData sp = cat.Species("chicken");
            var cold = new AgriWeather { temperature = -8f, humidity = 0.6f, clouds = 0.4f, light = 0.5f, day = true };
            AnimalRecord h1 = basic.animals[0], h3 = fine.animals[0];
            float t1 = AnimalCare.ComfortTarget(h1, sp, in f1, in cold, book.Rules, AnimalStageEffects.Neutral, out _);
            float t3 = AnimalCare.ComfortTarget(h3, sp, in f3, in cold, book.Rules, AnimalStageEffects.Neutral, out _);
            Assert.Greater(t3, t1);

            // Cleanliness: two days of the same hens soil the improved floor less.
            basic.pen.dirt = fine.pen.dirt = 0f;
            Keep(book, ctx, basic, Start, 48, false);
            Keep(book, ctx, fine, Start, 48, false);
            Assert.Greater(basic.pen.dirt, 0.5f);
            Assert.Less(fine.pen.dirt, basic.pen.dirt);

            // Production: the same hen, the same hour, goes further toward her egg in the improved pen.
            var a = new AnimalRecord { id = "x", species = "chicken", breed = "silkie", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            var b = new AnimalRecord { id = "y", species = "chicken", breed = "silkie", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            foreach (AnimalRecord r in new[] { a, b })
            {
                Typical(r);
                Thriving(r);
            }
            var band = new AnimalWelfareBandEffects { production = 1f };
            var plain = new AnimalStepFacts { kind = AnimalHabitatKind.Coop, area = 9f };
            var improved = new AnimalStepFacts { kind = AnimalHabitatKind.Coop, area = 9f, productionBonus = 0.25f };
            for (double t = Start; t < Start + 10.0 - 1e-9; t += 1.0)
            {
                AnimalProduction.Step(a, sp, in plain, book.Rules, band, AnimalStageEffects.Neutral, 1f, 1f, t, t + 1.0, 0f);
                AnimalProduction.Step(b, sp, in improved, book.Rules, band, AnimalStageEffects.Neutral, 1f, 1f, t, t + 1.0, 0f);
            }
            float pa = AnimalProduction.State(a, "egg", false).progress, pb = AnimalProduction.State(b, "egg", false).progress;
            Assert.AreEqual(pa * 1.25f, pb, 1e-3f);
        }

        // ---------------------------------------------------------------- upkeep
        [Test]
        public void UpkeepFallsDueByTheDayAndAPenInDisrepairLosesItsMachinesAndComfortUntilPaid()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start, 50f, 80f);
            AnimalUpgradeRules u = book.Rules.upgrades;
            AnimalActor keeper = Actor("ann", AnimalRights.All);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.LevelText, expected = AnimalUpgrades.NextLevelPrice(coop.pen, coop.spec, book.Rules) }, keeper, Start);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.WatererText, expected = u.watererCost }, keeper, Start);
            float day = AnimalUpgrades.UpkeepPerDay(coop.pen, coop.spec, book.Rules);
            Assert.AreEqual(u.levels[0].upkeepPerDay + u.watererUpkeepPerDay, day, 1e-4f);
            StringAssert.Contains("owes nothing", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id }, keeper, Start));

            book.BringUpToDate(coop, Start + 24.0, ctx);
            Assert.AreEqual(day, coop.pen.upkeepOwed, 1e-3f, "a day's upkeep after a day");
            Assert.IsFalse(AnimalUpgrades.InDisrepair(coop.pen, coop.spec, book.Rules));
            Assert.AreEqual(u.levels[0].comfort, AnimalHabitat.Facts(coop, coop.spec, cat, book.Rules, Start + 24.0, null, null).comfortBonus, 1e-4f);

            double t = Start + 24.0 * (u.graceDays + 1.0);
            book.BringUpToDate(coop, t, ctx);
            Assert.IsTrue(AnimalUpgrades.InDisrepair(coop.pen, coop.spec, book.Rules), "past its grace days");
            Assert.AreEqual(0f, AnimalHabitat.Facts(coop, coop.spec, cat, book.Rules, t, null, null).comfortBonus, "its level's comfort does not count in disrepair");
            Assert.AreEqual(1f, AnimalUpgrades.DirtFactor(coop.pen, coop.spec, book.Rules));
            Assert.IsFalse(AnimalUpgrades.Working(coop.pen, coop.spec, book.Rules, AnimalDevice.Waterer));
            coop.pen.water = 0f;
            book.BringUpToDate(coop, t + 5.0, ctx);
            Assert.AreEqual(0f, coop.pen.water, 1e-4f, "the waterer stands still");
            Assert.Greater(AnimalUpgrades.Area(coop.pen, coop.spec, book.Rules), coop.spec.area, "but its room stays: nobody is turned out");
            StringAssert.Contains("pay it first", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.HopperText, expected = u.hopperCost }, keeper, t + 5.0));

            long owed = AnimalUpgrades.OwedPrice(coop.pen);
            StringAssert.Contains("comes to", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id, expected = owed + 1 }, keeper, t + 5.0));
            StringAssert.Contains("Not your pen", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id, expected = owed }, Actor("x", AnimalRights.Look), t + 5.0));
            var paid = new AnimalPlan();
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id, expected = owed }, keeper, t + 5.0, paid);
            Assert.AreEqual(owed, paid.fee);
            Assert.That(coop.pen.upkeepOwed, Is.InRange(-1f, 0f), "paid in whole coins: what is over is paid ahead");
            Assert.IsTrue(AnimalUpgrades.Working(coop.pen, coop.spec, book.Rules, AnimalDevice.Waterer));
            book.BringUpToDate(coop, t + 10.0, ctx);
            Assert.Greater(coop.pen.water, 30f, "working again once paid");

            // Paid ahead, it stays in good repair while nobody comes; what is paid ahead is spent first.
            long week = AnimalUpgrades.PayPrice(coop.pen, coop.spec, book.Rules, AnimalUpgrades.WeekAhead);
            Assert.AreEqual((long)Math.Ceiling(coop.pen.upkeepOwed + day * 7f - 1e-4), week);
            StringAssert.Contains("comes to", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id, count = 7, expected = week - 1 }, keeper, t + 10.0));
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id, count = 7, expected = week }, keeper, t + 10.0);
            Assert.AreEqual(7f, AnimalUpgrades.PaidAheadDays(coop.pen, coop.spec, book.Rules), 1f / day + 0.01f, "a week ahead (to the coin)");
            float credit = coop.pen.upkeepOwed;
            StringAssert.Contains("owes nothing", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = coop.pen.id }, keeper, t + 10.0));
            book.BringUpToDate(coop, t + 10.0 + 24.0 * 9.0, ctx);
            Assert.IsFalse(AnimalUpgrades.InDisrepair(coop.pen, coop.spec, book.Rules), "a week ahead and its grace days");
            Assert.AreEqual(credit + 9f * day, coop.pen.upkeepOwed, 0.05f);

            // What it can owe has a ceiling.
            book.BringUpToDate(coop, t + 24.0 * 200.0, ctx);
            Assert.AreEqual(day * u.maxOwedDays, coop.pen.upkeepOwed, 1e-2f);
        }

        // ---------------------------------------------------------------- the hopper
        [Test]
        public void TheHopperKeepsTheFeederStockedAndItsFeedKeepsBetter()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start, 50f, 80f);
            AnimalActor keeper = Actor("ann", AnimalRights.All, new Dictionary<string, int> { [Feed] = 30 });
            var toHopper = new AnimalRequest { kind = AnimalActionKind.StockFeeder, pen = coop.pen.id, item = Feed, count = 10, text = AnimalUpgrades.HopperText };
            StringAssert.Contains("no feed hopper", Refusal(book, ctx, toHopper, keeper, Start));

            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.HopperText, expected = book.Rules.upgrades.hopperCost }, keeper, Start);
            Assert.IsTrue(coop.pen.hopper);
            Do(book, ctx, toHopper, keeper, Start);
            int per = cat.Food("chicken_feed").portionsPerItem;
            Assert.AreEqual(10f * per, coop.pen.HopperPortions, 1e-3f);
            Assert.AreEqual(0f, coop.pen.FeederPortions, "it drops feed as time goes by, not at once");

            book.BringUpToDate(coop, Start + 1.0, ctx);
            float level = coop.spec.feederCapacity * book.Rules.upgrades.hopperKeepsFeeder;
            Assert.AreEqual(level, coop.pen.FeederPortions, 1e-3f, "the feeder kept at its share");
            Assert.AreEqual(10f * per - level, coop.pen.HopperPortions, 1e-3f);

            // Sealed: fresh food lain in it for two days is as good as a day in the open (at Hopper Age Pace 0.5).
            var pen = new AnimalPenRecord { hopper = true };
            var spec = new AnimalPenSpec { feederCapacity = 100f };
            AnimalFoodData grass = cat.Food("fresh_grass");
            AnimalUpgrades.StockHopper(pen, book.Rules, grass, 20f, 0.8f, "ann", 0.0);
            AnimalUpgrades.Dispense(pen, spec, book.Rules, 48.0);
            FeederLot moved = pen.feeder[0];
            float inHopper = grass.QualityAfter(moved.quality, 48.0 - moved.since);
            float inOpen = grass.QualityAfter(0.8f, 48.0);
            Assert.AreEqual(grass.QualityAfter(0.8f, 24.0), inHopper, 1e-4f);
            Assert.Greater(inHopper, inOpen);

            // Taken out: its feed goes into the feeder as far as it fits.
            coop.pen.feeder.Clear();
            float held = coop.pen.HopperPortions;
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.RemovePrefix + AnimalUpgrades.HopperText }, keeper, Start + 1.0);
            Assert.IsFalse(coop.pen.hopper);
            Assert.AreEqual(0f, coop.pen.HopperPortions);
            Assert.AreEqual(Mathf.Min(held, coop.spec.feederCapacity), coop.pen.FeederPortions, 1e-3f);
        }

        // ---------------------------------------------------------------- the waterer
        [Test]
        public void TheWatererKeepsTheTroughFullOfCleanWater()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start, 50f, 80f);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.WatererText, expected = book.Rules.upgrades.watererCost }, Actor("ann", AnimalRights.All), Start);
            coop.pen.water = 10f;
            coop.pen.waterDirt = 80f;
            coop.pen.waterKind = (int)WaterSourceKind.Swamp;
            book.BringUpToDate(coop, Start + 20.0, ctx);
            Assert.AreEqual(coop.spec.waterCapacity, coop.pen.water, 1e-3f);
            Assert.Less(coop.pen.waterDirt, 25f, "clean water coming in washes the dirt out");
            Assert.AreEqual((int)WaterSourceKind.Well, coop.pen.waterKind);
        }

        // ---------------------------------------------------------------- the egg trays
        [Test]
        public void EggsRollIntoTheTraysSoTheNestNeverFillsAndKeepTheirOrigin()
        {
            AnimalCatalog cat = Catalog();
            var withTrays = new AnimalBook(cat, "w");
            var without = new AnimalBook(cat, "w");
            AnimalSimContext c1 = Ctx(cat, withTrays), c2 = Ctx(cat, without);
            AnimalHerd t = Pen(withTrays, "coop", AnimalHabitatKind.Coop, 24f, Start);
            AnimalHerd n = Pen(without, "coop", AnimalHabitatKind.Coop, 24f, Start);
            t.pen.trays = true;
            AnimalRecord ht = Release(withTrays, c1, t, "ann", "chicken", "silkie", Start, AnimalSex.Female);
            // Its upkeep paid two weeks ahead (unpaid, the trays would stop after the rules' grace days).
            long ahead = AnimalUpgrades.PayPrice(t.pen, t.spec, withTrays.Rules, 14);
            Do(withTrays, c1, new AnimalRequest { kind = AnimalActionKind.PayUpkeep, pen = t.pen.id, count = 14, expected = ahead }, Actor("ann", AnimalRights.All), Start);
            Assert.Greater(AnimalUpgrades.PaidAheadDays(t.pen, t.spec, withTrays.Rules), 13.9f);
            AnimalRecord hn = Release(without, c2, n, "ann", "chicken", "silkie", Start, AnimalSex.Female);
            Keep(withTrays, c1, t, Start, 24 * 12);
            Keep(without, c2, n, Start, 24 * 12);
            AnimalProductSpec egg = AnimalProduction.Spec(cat.Species("chicken"), "egg");
            Assert.AreEqual(egg.capacity, AnimalProduction.State(hn, "egg", false).Waiting, "without trays her nest fills, and she stops");
            Assert.AreEqual(egg.capacity, Made(hn, "egg"));
            Assert.Greater(Made(ht, "egg"), egg.capacity, "with trays she goes on laying");
            Assert.AreEqual(0, AnimalProduction.State(ht, "egg", false).Waiting);
            Assert.AreEqual(Made(ht, "egg"), t.pen.TrayUnits);
            Assert.IsTrue(t.pen.tray.TrueForAll(x => x.animal == ht.id && x.owner == "ann" && x.product == "egg"));

            // Collected from the trays: each copy coded with its quality and a ledger lot of its whole origin.
            int units = t.pen.TrayUnits;
            var got = new AnimalPlan();
            Do(withTrays, c1, new AnimalRequest { kind = AnimalActionKind.Collect, pen = t.pen.id, text = AnimalUpgrades.TraysText }, Actor("ann", AnimalRights.All), Start + 24 * 12, got);
            Assert.AreEqual(1, got.give.Count);
            Assert.AreEqual(units, got.give[0].count);
            Assert.AreEqual(0, t.pen.TrayUnits);
            Assert.IsTrue(AnimalItemCode.TryProduct(got.give[0].codes[0], out _, out long serial));
            AnimalProductRecord origin = withTrays.Ledger.Find(serial);
            Assert.IsNotNull(origin);
            Assert.AreEqual(ht.id, origin.animal);
            Assert.AreEqual("ann", origin.owner);
            Assert.Greater(origin.cycle, 0);
            StringAssert.Contains("empty", Refusal(withTrays, c1, new AnimalRequest { kind = AnimalActionKind.Collect, pen = t.pen.id, text = AnimalUpgrades.TraysText }, Actor("ann", AnimalRights.All), Start + 24 * 12));
        }

        [Test]
        public void TheTraysHoldWhatTheyHoldAndOnlyWhoMayCollectTakesFromThem()
        {
            AnimalRules rules = AnimalRules.Defaults();
            rules.upgrades.trayCapacity = 2;
            AnimalCatalog cat = Catalog(rules);
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start);
            coop.pen.trays = true;
            AnimalRecord hen = Release(book, ctx, coop, "ann", "chicken", "silkie", Start, AnimalSex.Female);
            AnimalSpeciesData sp = cat.Species("chicken");
            AnimalProductState st = AnimalProduction.State(hen, "egg", true);
            for (int i = 0; i < 3; i++) st.waiting.Add(new AnimalProductLot { units = 1, quality = 0.7f, cycle = i + 1, madeAt = Start });
            Assert.AreEqual(2, AnimalUpgrades.RollToTrays(coop, cat, rules));
            Assert.AreEqual(2, coop.pen.TrayUnits);
            Assert.AreEqual(1, st.Waiting, "what does not fit waits in the nest");

            // Not the keeper's own, and no right to care for others' animals: nothing of it is taken.
            var stranger = Actor("bob", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep);
            StringAssert.Contains("yours", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = coop.pen.id, text = AnimalUpgrades.TraysText }, stranger, Start));
            // Nor are the trays taken out while they hold eggs.
            StringAssert.Contains("Collect what waits", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.RemovePrefix + AnimalUpgrades.TraysText }, Actor("ann", AnimalRights.All), Start));

            // Its keeper's Collect for the whole pen takes the trays' and the nest's together.
            var got = new AnimalPlan();
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = coop.pen.id }, Actor("ann", AnimalRights.All), Start, got);
            Assert.AreEqual(3, got.give[0].count);
            Assert.AreEqual(0, coop.pen.TrayUnits);
            Assert.AreEqual(0, st.Waiting);

            // Aging: past their Fresh Hours eggs in the trays lose quality as in the nest.
            AnimalProductSpec egg = AnimalProduction.Spec(sp, "egg");
            coop.pen.tray.Add(new AnimalTrayLot { product = "egg", animal = hen.id, species = "chicken", owner = "ann", lot = new AnimalProductLot { units = 1, quality = 0.8f, madeAt = 0.0 } });
            AnimalUpgrades.AgeTrays(coop.pen, cat, rules, 24f, egg.freshHours + 24.0);
            Assert.AreEqual(0.8f - egg.staleLossPerDay, coop.pen.tray[0].lot.quality, 1e-4f);
        }

        // ---------------------------------------------------------------- catch-ups
        [Test]
        public void AWeekOfMachinesAtWorkCaughtUpEndsExactlyAsAWeekWatchedHourByHour()
        {
            string Run(bool hourly)
            {
                AnimalCatalog cat = Catalog();
                var book = new AnimalBook(cat, "w");
                AnimalSimContext ctx = Ctx(cat, book);
                AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start, 40f, 30f);
                coop.pen.level = 3;
                coop.pen.hopper = coop.pen.waterer = coop.pen.trays = true;
                for (int i = 0; i < 4; i++) Release(book, ctx, coop, "ann", "chicken", i % 2 == 0 ? "leghorn" : "silkie", Start, AnimalSex.Female, GrowthStage.Adult, 300 + i);
                AnimalUpgrades.StockHopper(coop.pen, book.Rules, cat.Food("chicken_feed"), 230f, 0.8f, "ann", Start);
                double end = Start + 24.0 * 7.0;
                if (hourly)
                    for (double t = Start + 1.0; t <= end + 1e-9; t += 1.0) book.BringUpToDate(coop, t, ctx);
                else book.BringUpToDate(coop, end, ctx);
                var sb = new System.Text.StringBuilder(JsonUtility.ToJson(coop.pen));
                foreach (AnimalRecord a in coop.animals) sb.Append(JsonUtility.ToJson(a));
                return Strip(sb.ToString());
            }

            string Strip(string json) => System.Text.RegularExpressions.Regex.Replace(json, "\"revision\":-?[0-9]+", "");

            string watched = Run(true), caught = Run(false);
            Assert.AreEqual(watched, caught);
            StringAssert.Contains("\"trays\":true", caught);
        }

        // ---------------------------------------------------------------- breeds
        [Test]
        public void ALayingBreedLaysMoreButOnlyOfWhatItIsBredFor()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData chicken = cat.Species("chicken");
            AnimalProductSpec egg = AnimalProduction.Spec(chicken, "egg");
            AnimalBreedData leghorn = chicken.Breed("leghorn"), silkie = chicken.Breed("silkie");
            Assert.AreEqual(1.2f, AnimalProduction.BreedYield(leghorn, egg), 1e-4f);
            Assert.AreEqual(1f, AnimalProduction.BreedYield(silkie, egg), 1e-4f);
            Assert.AreEqual(1f, AnimalProduction.BreedYield(leghorn, new AnimalProductSpec { id = "feather" }), 1e-4f, "its yield is for its specialty only");
            Assert.AreEqual(1.25f, AnimalProduction.BreedYield(cat.Species("sheep").Breed("merino"), AnimalProduction.Spec(cat.Species("sheep"), "wool")), 1e-4f);
            Assert.AreEqual(1f, AnimalProduction.BreedYield(null, egg));

            var a = new AnimalRecord { id = "a", species = "chicken", breed = "leghorn", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            var b = new AnimalRecord { id = "b", species = "chicken", breed = "silkie", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            foreach (AnimalRecord r in new[] { a, b })
            {
                Typical(r);
                Thriving(r);
            }
            var f = new AnimalStepFacts { kind = AnimalHabitatKind.Coop, area = 9f };
            var band = new AnimalWelfareBandEffects { production = 1f };
            for (double t = Start; t < Start + 12.0 - 1e-9; t += 1.0)
            {
                AnimalProduction.Step(a, chicken, in f, cat.Rules, band, AnimalStageEffects.Neutral, 1f, 1f, t, t + 1.0, 0f);
                AnimalProduction.Step(b, chicken, in f, cat.Rules, band, AnimalStageEffects.Neutral, 1f, 1f, t, t + 1.0, 0f);
            }
            Assert.AreEqual(AnimalProduction.State(b, "egg", false).progress * 1.2f, AnimalProduction.State(a, "egg", false).progress, 1e-3f);
            Assert.AreEqual(5, chicken.dataVersion);
        }

        // ---------------------------------------------------------------- saves and pictures
        [Test]
        public void APhase4SaveLoadsWithPensAsBuiltAndNothingOwedOrCrated()
        {
            AnimalCatalog cat = Catalog();
            var save = new AnimalWorldSave { version = 4, world = "w", serial = 3 };
            save.pens.Add(new AnimalPenRecord { id = "p", level = 0, hopperFeed = null, tray = null, upkeepOwed = -3f, upgradedBy = null, spec = new AnimalPenSpec { id = "p", kind = AnimalHabitatKind.Coop } });
            save.crated = null;
            var book = new AnimalBook(cat, "w");
            book.Import(save);
            AnimalPenRecord p = book.Pen("p");
            Assert.AreEqual(1, p.level);
            Assert.IsNotNull(p.hopperFeed);
            Assert.IsNotNull(p.tray);
            Assert.AreEqual(0f, p.upkeepOwed);
            Assert.AreEqual("", p.upgradedBy);
            Assert.AreEqual(0, book.CratedCount);
            Assert.AreEqual(0L, book.CrateSerial);
            Assert.AreEqual(5, AnimalWorldSave.CurrentVersion);
            Assert.AreEqual(AnimalWorldSave.CurrentVersion, book.Export(0, 0).version);
        }

        [Test]
        public void ThePensPictureShowsItsLevelMachinesUpkeepAndWhatEachOwnerMayCrate()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start);
            AnimalRecord mine = Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female);
            AnimalRecord theirs = Release(book, ctx, coop, "bob", "chicken", "leghorn", Start, AnimalSex.Female, GrowthStage.Adult, 999);
            coop.pen.trays = true;
            coop.pen.tray.Add(new AnimalTrayLot { product = "egg", animal = theirs.id, species = "chicken", owner = "bob", lot = new AnimalProductLot { units = 2, quality = 0.7f } });

            AnimalPenView v = AnimalObservation.PenView(book, coop, "ann", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep, Start);
            Assert.AreEqual(1, v.level);
            Assert.AreEqual(AnimalUpgrades.MaxLevel(book.Rules), v.maxLevel);
            Assert.AreEqual(AnimalUpgrades.NextLevelPrice(coop.pen, coop.spec, book.Rules), v.nextLevelPrice);
            Assert.IsTrue(v.canBuild);
            Assert.IsTrue(v.canPay);
            Assert.IsTrue(v.trays);
            Assert.AreEqual(2, v.trayUnits);
            Assert.IsFalse(v.collectReady, "bob's eggs in the trays are not hers to collect");
            AnimalView a = v.animals.Find(x => x.id == mine.id), b = v.animals.Find(x => x.id == theirs.id);
            Assert.IsTrue(a.crateReady);
            Assert.That(a.grade, Is.InRange(0, 15));
            Assert.Greater(a.worth, 0f);
            Assert.IsFalse(b.crateReady, "nor is his hen hers to crate");
            Assert.AreEqual(-1, b.grade);

            AnimalPenView bobs = AnimalObservation.PenView(book, coop, "bob", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep, Start);
            Assert.IsTrue(bobs.collectReady);
            StringAssert.Contains("2", bobs.productsWaiting);

            coop.pen.level = 2;
            coop.pen.upkeepOwed = 100f;
            v = AnimalObservation.PenView(book, coop, "ann", AnimalRights.Look, Start);
            Assert.IsTrue(v.disrepair);
            Assert.AreEqual(100L, v.upkeepOwed);
            Assert.IsFalse(v.canBuild);
            Assert.Greater(v.area, v.baseArea);
            StringAssert.Contains("disrepair", v.upkeepText);
        }

        // ---------------------------------------------------------------- the review's cases
        [Test]
        public void ACratedAnimalIsLetOutOnlyWhenItsOwnCrateIsChosenAndOnlyAsItsCodeSays()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female);
            string item = AnimalIds.Livestock("chicken", "leghorn");
            int code = Crate(book, ctx, hen, Start);
            AnimalItemCode.TryCrated(code, out AnimalSex sex, out GrowthStage stage, out int grade, out long serial);

            // "Any crate" (the plain crate's line, or a script that names none) never lets a crated animal out.
            AnimalResult any = LetOut(book, ctx, coop, "ann", item, code, Start + 1.0, "", 0);
            Assert.IsFalse(any.ok);
            StringAssert.Contains("Choose which crate", any.message);
            AnimalResult plain = LetOut(book, ctx, coop, "ann", item, code, Start + 1.0, "", AnimalCommands.PlainCrate);
            Assert.IsFalse(plain.ok);
            Assert.AreEqual(1, book.CratedCount, "still in its crate");

            // A copy whose code disagrees with the animal (a stale serial given again, a forged grade) opens nothing.
            int forged = AnimalItemCode.Crated(sex, stage, (grade + 5) % 16, serial);
            Assert.IsFalse(LetOut(book, ctx, coop, "bob", item, forged, Start + 1.0).ok);
            int wrongSex = AnimalItemCode.Crated(AnimalSex.Male, stage, grade, serial);
            Assert.IsFalse(LetOut(book, ctx, coop, "bob", item, wrongSex, Start + 1.0).ok);
            Assert.AreEqual(1, book.CratedCount);

            Assert.IsTrue(LetOut(book, ctx, coop, "ann", item, code, Start + 1.0).ok, "its own crate, chosen");
        }

        [Test]
        public void AMachineIsTakenOutOnlyByWhoFittedItOrThePensKeepers()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd pub = Pen(book, "public", AnimalHabitatKind.Coop, 24f, Start, 50f, 80f);
            AnimalRights publicLand = AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep;
            AnimalActor ann = Actor("ann", publicLand), bob = Actor("bob", publicLand), tenant = Actor("tess", AnimalRights.All);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = pub.pen.id, text = AnimalUpgrades.HopperText, expected = book.Rules.upgrades.hopperCost }, ann, Start);
            Assert.AreEqual("ann", pub.pen.hopperBy);
            var remove = new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = pub.pen.id, text = AnimalUpgrades.RemovePrefix + AnimalUpgrades.HopperText };
            StringAssert.Contains("Only who fitted it", Refusal(book, ctx, remove, bob, Start), "a passer-by on public land does not rip it out");
            Assert.IsFalse(AnimalObservation.PenView(book, pub, "bob", publicLand, Start).removeHopper);
            Assert.IsTrue(AnimalObservation.PenView(book, pub, "ann", publicLand, Start).removeHopper);
            Do(book, ctx, remove, ann, Start);
            Assert.IsFalse(pub.pen.hopper);
            Assert.AreEqual("", pub.pen.hopperBy);

            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = pub.pen.id, text = AnimalUpgrades.WatererText, expected = book.Rules.upgrades.watererCost }, ann, Start);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = pub.pen.id, text = AnimalUpgrades.RemovePrefix + AnimalUpgrades.WatererText }, tenant, Start);
            Assert.IsFalse(pub.pen.waterer, "the pen's keepers (Care For Others) may");

            // A machine with nothing to work on is not fitted.
            AnimalHerd bare = Pen(book, "bare", AnimalHabitatKind.Coop, 24f, Start, 0f, 0f);
            StringAssert.Contains("no feeder", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = bare.pen.id, text = AnimalUpgrades.HopperText, expected = book.Rules.upgrades.hopperCost }, tenant, Start));
            StringAssert.Contains("no trough", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = bare.pen.id, text = AnimalUpgrades.WatererText, expected = book.Rules.upgrades.watererCost }, tenant, Start));
        }

        [Test]
        public void WithUpgradesOffALevelCountsForNothingAndAMachineCanStillBeTakenOut()
        {
            AnimalRules rules = AnimalRules.Defaults();
            AnimalCatalog cat = Catalog(rules);
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start, 50f, 80f);
            coop.pen.level = 3;
            AnimalUpgrades.Set(coop.pen, AnimalDevice.Hopper, true, "ann");
            AnimalUpgrades.StockHopper(coop.pen, rules, cat.Food("chicken_feed"), 30f, 0.8f, "ann", Start);
            rules.upgrades.enabled = false;
            Assert.AreEqual(1, AnimalUpgrades.Level(coop.pen, rules));
            Assert.AreEqual(coop.spec.area, AnimalUpgrades.Area(coop.pen, coop.spec, rules), 1e-4f);
            Assert.AreEqual(0f, AnimalUpgrades.ComfortBonus(coop.pen, coop.spec, rules));
            Assert.IsFalse(AnimalUpgrades.Working(coop.pen, coop.spec, rules, AnimalDevice.Hopper));
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Upgrade, pen = coop.pen.id, text = AnimalUpgrades.RemovePrefix + AnimalUpgrades.HopperText }, Actor("ann", AnimalRights.All), Start);
            Assert.IsFalse(coop.pen.hopper);
            Assert.AreEqual(30f, coop.pen.FeederPortions, 1e-3f, "its feed is not stuck in it");
        }

        [Test]
        public void AFailedCratingOrLettingOutIsUndoneOnTheBooksSideToo()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "ann", "chicken", "leghorn", Start, AnimalSex.Female);
            int code = Crate(book, ctx, hen, Start);
            AnimalItemCode.TryCrated(code, out _, out _, out _, out long serial);
            long last = book.CrateSerial;

            // Its crate could not be given: it lives in its pen again (and its serial is not given again).
            Assert.IsTrue(book.UndoCrate(hen, coop.pen.id));
            Assert.AreSame(hen, book.Find(hen.id));
            Assert.IsTrue(coop.animals.Contains(hen));
            Assert.AreEqual(AnimalStatus.Alive, hen.status);
            Assert.AreEqual(0, book.CratedCount);
            Assert.IsFalse(book.UndoCrate(hen, coop.pen.id), "once");
            int again = Crate(book, ctx, hen, Start + 1.0);
            AnimalItemCode.TryCrated(again, out _, out _, out _, out long serial2);
            Assert.Greater(serial2, last);

            // A letting out that failed after the animal left its crate: back in it, under its own serial, as it was.
            AnimalCrated entry = book.Crated(serial2);
            Assert.IsTrue(LetOut(book, ctx, coop, "bob", AnimalIds.Livestock("chicken", "leghorn"), again, Start + 2.0).ok);
            Assert.IsTrue(book.UndoUncrate(entry));
            Assert.IsNull(book.Find(hen.id));
            Assert.IsFalse(coop.animals.Contains(hen));
            Assert.AreSame(hen, book.Crated(serial2).animal);
            Assert.AreEqual(AnimalStatus.Crated, hen.status);
            Assert.IsFalse(book.UndoUncrate(entry), "once");
        }

        [Test]
        public void AYieldSpeedsTheCyclesButDoesNotAlsoFillEachLot()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData cow = cat.Species("cow");
            AnimalProductSpec milk = AnimalProduction.Spec(cow, "milk");
            var holstein = new AnimalRecord { id = "h", species = "cow", breed = "holstein", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            var plain = new AnimalRecord { id = "p", species = "cow", breed = "angus", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            cow.Breed("angus").productYield = 1f;
            foreach (AnimalRecord r in new[] { holstein, plain })
            {
                Typical(r);
                Thriving(r);
            }
            var f = new AnimalStepFacts { kind = AnimalHabitatKind.Barn, area = 50f, productionBonus = 0.1f };
            var band = new AnimalWelfareBandEffects { production = 1f };
            // Fifteen hours: neither udder is full yet (three lots fill it).
            for (double t = Start; t < Start + 15.0 - 1e-9; t += 1.0)
            {
                AnimalProduction.Step(holstein, cow, in f, cat.Rules, band, AnimalStageEffects.Neutral, 1f, 1f, t, t + 1.0, 0f);
                AnimalProduction.Step(plain, cow, in f, cat.Rules, band, AnimalStageEffects.Neutral, 1f, 1f, t, t + 1.0, 0f);
            }
            AnimalProductState sa = AnimalProduction.State(holstein, "milk", false), sb = AnimalProduction.State(plain, "milk", false);
            Assert.IsTrue(milk.yieldScales);
            Assert.Greater(sa.waiting.Count, 0);
            Assert.Greater(sb.waiting.Count, 0);
            Assert.AreEqual(sb.waiting[0].units, sa.waiting[0].units, "a lot holds what how she lives gives");
            Assert.AreEqual((sb.cycle + sb.progress) * 1.2f, sa.cycle + sa.progress, 1e-2f, "but she milks 20% more often");
        }

        [Test]
        public void TrayLotsOfAProductNoLongerGivenAreDroppedAndNeverCounted()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "w");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 24f, Start);
            coop.pen.trays = true;
            Release(book, ctx, coop, "ann", "chicken", "silkie", Start, AnimalSex.Female);
            coop.pen.tray.Add(new AnimalTrayLot { product = "gold_egg", animal = "x", species = "chicken", owner = "ann", lot = new AnimalProductLot { units = 3, quality = 0.7f } });
            StringAssert.Contains("yours", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = coop.pen.id, text = AnimalUpgrades.TraysText }, Actor("ann", AnimalRights.All), Start));
            AnimalUpgrades.AgeTrays(coop.pen, cat, book.Rules, 1f, Start + 1.0);
            Assert.AreEqual(0, coop.pen.TrayUnits, "it can never be collected: it goes, and the trays can be taken out");
        }

        // ---------------------------------------------------------------- crafting
        [Test]
        public void AFineFleeceMakesACleanerSounderBatchOfFibre()
        {
            CraftPiece Batch(float q)
            {
                var p = new CraftPiece { kind = CraftPieceKind.Fibre };
                p.fibre.dirt = 0.3;
                p.fibre.shortFibre = 0.12;
                CraftFibre.Grade(p, q);
                return p;
            }
            Assert.AreEqual(0.3, Batch(0.6f).fibre.dirt, 1e-6, "a typical fleece is the supply's typical batch");
            Assert.AreEqual(0.15, Batch(1f).fibre.dirt, 1e-6);
            Assert.AreEqual(0.06, Batch(1f).fibre.shortFibre, 1e-6);
            Assert.Greater(Batch(0.1f).fibre.dirt, 0.3);
            Assert.AreEqual(0.3, Batch(-1f).fibre.dirt, 1e-9, "a copy of unknown quality is left as it is");
            Assert.LessOrEqual(Batch(0f).fibre.dirt, 1.0);
        }
    }
}
#endif
