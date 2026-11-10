#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AnimalHusbandrySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of Phase 4 of animal husbandry (plain C#: no scene, no assets): breeding (who may be bred, the chance
    /// to conceive, gestation, births into the pen, cooldowns, losing the young, nothing born twice), inheritance (means,
    /// spread, configurable mutations, rare traits, coats, inbreeding), kinship, nursing, relationships between animals
    /// (companions, kin, mates, rivals; missing a companion), the bond with the owner (time together, perks, easier handling,
    /// and that a bond never stands in for welfare), catch-ups and saves, and what each viewer is shown.
    /// </summary>
    public class AnimalBreedingTests
    {
        // ---------------------------------------------------------------- helpers
        private const double Start = 48.0;

        private static AnimalCatalog Catalog(AnimalRules r = null) => AnimalCatalog.BuiltIn(r ?? AnimalRules.Defaults());

        private static AnimalSimContext Ctx(IAnimalCatalog cat, AnimalBook book, bool present = true) => new AnimalSimContext
        {
            catalog = cat,
            climate = new CityClimateSource(AgriSettings.Defaults()),
            ownerPresent = (k, t) => present,
            notices = new List<AnimalNotice>(),
            book = book,
        };

        private static AnimalHerd Pen(AnimalBook book, string id, AnimalHabitatKind kind, float area, double now)
        {
            var spec = new AnimalPenSpec { id = id, name = id, kind = kind, area = area, feederCapacity = 50000f, waterCapacity = 5000f, troughSurface = 0f, grassCapacity = 3f };
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

        private static AnimalRequest Breed(AnimalRecord a, AnimalRecord b) =>
            new AnimalRequest { kind = AnimalActionKind.Breed, pen = a.pen, animal = a.id, text = b.id };

        private static AnimalResult Do(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsFalse(plan.Refused, $"{r.kind}: {plan.refusal?.message}");
            var taken = new AnimalTaken { count = plan.takeCount, qualities = new List<float>(), codes = new List<int>() };
            for (int i = 0; i < plan.takeCount; i++)
            {
                taken.qualities.Add(0.8f);
                taken.codes.Add(0);
            }
            AnimalResult res = AnimalCommands.Commit(book, plan, taken, now, ctx);
            Assert.IsTrue(res.ok, $"{r.kind}: {res.message}");
            return res;
        }

        private static string Refusal(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsTrue(plan.Refused, $"{r.kind} should be refused");
            return plan.refusal.message;
        }

        /// <summary>The herd kept fed and watered (hay in the feeder, the trough full, the floor clean), stepped hour by hour by the book.</summary>
        private static void Keep(AnimalBook book, AnimalSimContext ctx, AnimalHerd h, double from, int hours, string food = "hay")
        {
            for (int hr = 1; hr <= hours; hr++)
            {
                h.pen.water = 5000f;
                h.pen.dirt = 0f;
                if (h.pen.FeederPortions < 200f) AnimalCommands.Stock(h, book.Catalog.Food(food), 1000f, 0.8f, "test", from + hr - 1);
                book.BringUpToDate(h, from + hr, ctx);
            }
        }

        /// <summary>Breeds a pair until she is expecting (a mating that does not take is tried again after its wait). Returns the hour.</summary>
        private static double BreedUntilExpecting(AnimalBook book, AnimalSimContext ctx, AnimalHerd h, AnimalRecord mother, AnimalRecord father, double t)
        {
            AnimalActor owner = Actor(mother.owner, AnimalRights.All);
            for (int i = 0; i < 12; i++)
            {
                Thriving(mother);
                Thriving(father);
                Do(book, ctx, Breed(mother, father), owner, t);
                if (AnimalBreeding.Expecting(mother)) return t;
                t += book.Rules.breeding.retryHours;
                Keep(book, ctx, h, t - book.Rules.breeding.retryHours, (int)book.Rules.breeding.retryHours);
            }
            Assert.Fail("she never conceived in twelve tries");
            return t;
        }

        private static List<AnimalRecord> YoungOf(AnimalHerd h, AnimalRecord mother)
        {
            var list = new List<AnimalRecord>();
            foreach (AnimalRecord a in h.animals)
                if (a != null && a.mother == mother.id) list.Add(a);
            return list;
        }

        // ---------------------------------------------------------------- the content
        [Test]
        public void TheBuiltInSpeciesBreedBehaveAndBondEachInTheirWay()
        {
            AnimalCatalog cat = Catalog();
            foreach (AnimalSpeciesData sp in cat.AllSpecies)
            {
                Assert.IsTrue(sp.breeding.enabled, sp.id);
                Assert.Greater(sp.breeding.gestationHours, 0f, sp.id);
                Assert.GreaterOrEqual(sp.breeding.litterMax, sp.breeding.litterMin, sp.id);
                Assert.IsNotNull(sp.behaviour, sp.id);
                Assert.IsNotEmpty(sp.perks, sp.id);
                Assert.AreEqual(5, sp.dataVersion);
                var errors = new List<string>();
                var warnings = new List<string>();
                sp.Validate(errors, warnings);
                Assert.IsEmpty(errors, sp.id + ": " + string.Join(" | ", errors));
            }
            Assert.IsFalse(cat.Species("chicken").breeding.nurses, "a chick feeds itself");
            Assert.IsTrue(cat.Species("cow").breeding.nurses, "a calf suckles");
            Assert.AreEqual(1, cat.Species("cow").breeding.LitterMax);
            Assert.Greater(cat.Species("pig").breeding.LitterMax, 1);
            Assert.IsTrue(cat.Species("cow").breeding.malesRival, "bulls are rivals");
            Assert.AreEqual("calved", cat.Species("cow").breeding.birthVerb);
            Assert.IsTrue(cat.Species("horse").perks.Exists(p => p.kind == AnimalPerkKind.ComeWhenCalled), "a horse comes when called");
            Assert.IsTrue(cat.Species("sheep").perks.Exists(p => p.kind == AnimalPerkKind.CalmHandling), "a sheep stands quiet for shearing");
            var ruleErrors = new List<string>();
            var ruleWarnings = new List<string>();
            cat.Rules.breeding.Validate(ruleErrors, ruleWarnings);
            cat.Rules.social.Validate(ruleErrors, ruleWarnings);
            Assert.IsEmpty(ruleErrors);
            Assert.IsEmpty(ruleWarnings);
        }

        // ---------------------------------------------------------------- breeding
        [Test]
        public void AWellKeptPairHaveACalfThatKnowsItsParents()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 101);
            AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 102);
            cow.name = "Daisy";
            bull.name = "Ferdinand";
            double t = BreedUntilExpecting(book, ctx, field, cow, bull, Start);
            AnimalPregnancy p = cow.pregnancy;
            Assert.AreEqual(bull.id, p.father);
            Assert.AreEqual(t + cat.Species("cow").breeding.gestationHours, p.dueAt, 1e-6);
            Assert.AreEqual(1, p.litter);
            Assert.AreEqual(p.dueAt, cow.breedReadyAt, 1e-6, "she cannot be bred again while she carries");
            Assert.Greater(bull.breedReadyAt, t, "the bull rests a while");
            Assert.IsNotNull(AnimalSocial.BondWith(cow, bull.id), "they are mates");
            StringAssert.Contains("in calf", Refusal(book, ctx, Breed(cow, bull), Actor("p1", AnimalRights.All), t + 1.0));

            int before = field.animals.Count;
            ctx.notices.Clear();
            Keep(book, ctx, field, t, (int)Math.Ceiling(p.dueAt - t) + 1);
            List<AnimalRecord> young = YoungOf(field, cow);
            Assert.AreEqual(1, young.Count, "a calf is born in her pen");
            Assert.AreEqual(before + 1, field.animals.Count);
            AnimalRecord calf = young[0];
            Assert.AreEqual(bull.id, calf.father);
            Assert.AreEqual("Daisy", calf.motherName);
            Assert.AreEqual("Ferdinand", calf.fatherName);
            Assert.AreEqual(1, calf.generation);
            Assert.AreEqual("p1", calf.owner, "the young are its mother's owner's");
            Assert.AreEqual("born", calf.origin);
            Assert.AreEqual(cat.Species("cow").stages[0].stage, calf.stage, "it starts at the first stage of its life");
            Assert.IsTrue(calf.breed == "holstein" || calf.breed == "angus", "one parent's breed");
            Assert.AreEqual(AnimalBondKind.Kin, AnimalSocial.BondWith(calf, cow.id).kind, "mother and calf are kin");
            Assert.AreEqual(AnimalBondKind.Kin, AnimalSocial.BondWith(cow, calf.id).kind);
            Assert.IsFalse(AnimalBreeding.Expecting(cow));
            Assert.AreEqual(1, cow.offspring);
            Assert.AreEqual(1, bull.offspring);
            Assert.Greater(cow.breedReadyAt, p.dueAt, "she rests after calving");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.GaveBirth && n.text.Contains("Daisy has calved")), "her owner is told");
            Assert.IsTrue(calf.events.Exists(e => e.kind == AnimalEventKind.Born));
            Assert.IsTrue(book.Urgent, "a birth is saved soon");
            Assert.AreEqual(0, AnimalBreeding.Deliver(book, cow, cat.Species("cow"), ctx, p.dueAt + 10.0), "nothing is born twice");
        }

        [Test]
        public void OnlyACompatibleReadyPairOfTheOwnersOwnMayBeBred()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 400f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 1);
            AnimalRecord cow2 = Release(book, ctx, field, "p1", "cow", "jersey", Start, AnimalSex.Female, seed: 2);
            AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 3);
            AnimalRecord theirs = Release(book, ctx, field, "p2", "cow", "angus", Start, AnimalSex.Male, seed: 4);
            AnimalRecord ram = Release(book, ctx, field, "p1", "sheep", "merino", Start, AnimalSex.Male, seed: 5);
            AnimalRecord calf = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, GrowthStage.Juvenile, 6);
            foreach (AnimalRecord x in field.animals) Thriving(x);
            AnimalActor p1 = Actor("p1", AnimalRights.All);
            StringAssert.Contains("both female", Refusal(book, ctx, Breed(cow, cow2), p1, Start));
            StringAssert.Contains("not of one kind", Refusal(book, ctx, Breed(cow, ram), p1, Start));
            StringAssert.Contains("both must be yours", Refusal(book, ctx, Breed(cow, theirs), p1, Start));
            StringAssert.Contains("too young", Refusal(book, ctx, Breed(cow, calf), p1, Start));
            StringAssert.Contains("Not your animals", Refusal(book, ctx, Breed(cow, bull), Actor("p1", AnimalRights.None), Start));
            StringAssert.Contains("cannot keep animals", Refusal(book, ctx, Breed(cow, bull), Actor("p1", AnimalRights.Look | AnimalRights.Tend), Start));
            cow.nutrition = 40f;
            StringAssert.Contains("too poorly fed", Refusal(book, ctx, Breed(cow, bull), p1, Start));
            Thriving(cow);
            cow.welfare = 30f;
            StringAssert.Contains("not kept well enough", Refusal(book, ctx, Breed(cow, bull), p1, Start));
            Thriving(cow);
            bull.health = 40f;
            StringAssert.Contains("not well enough", Refusal(book, ctx, Breed(cow, bull), p1, Start));
            Thriving(bull);
            bull.breedReadyAt = Start + 10.0;
            StringAssert.Contains("not ready again yet", Refusal(book, ctx, Breed(cow, bull), p1, Start));
            bull.breedReadyAt = 0.0;

            // Room for the young, and in the owner's keeping.
            AnimalRules tight = AnimalRules.Defaults();
            tight.maxAnimalsPerPlayer = 5;
            book.Catalog = Catalog(tight);
            ctx.catalog = book.Catalog;
            StringAssert.Contains("could not keep their young", Refusal(book, ctx, Breed(cow, bull), p1, Start));
            book.Catalog = cat;
            ctx.catalog = cat;
            AnimalHerd small = Pen(book, "small", AnimalHabitatKind.Pasture, 33f, Start);
            AnimalRecord c3 = Release(book, ctx, small, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 7);
            AnimalRecord b3 = Release(book, ctx, small, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 8);
            Thriving(c3);
            Thriving(b3);
            StringAssert.Contains("no room here for their young", Refusal(book, ctx, Breed(c3, b3), p1, Start));

            // What breeding takes from the player.
            AnimalRules costly = AnimalRules.Defaults();
            AnimalCatalog costlyCat = Catalog(costly);
            costlyCat.Species("cow").breeding.costItem = "animal:feed/grain_mix";
            costlyCat.Species("cow").breeding.costCount = 3;
            book.Catalog = costlyCat;
            ctx.catalog = costlyCat;
            StringAssert.Contains("takes 3 grain mix", Refusal(book, ctx, Breed(cow, bull), p1, Start));
            AnimalPlan plan = AnimalCommands.Plan(book, Breed(cow, bull), Actor("p1", AnimalRights.All, new Dictionary<string, int> { ["animal:feed/grain_mix"] = 3 }), Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.AreEqual("animal:feed/grain_mix", plan.takeItem);
            Assert.AreEqual(3, plan.takeCount, "it is taken from the player");
            AnimalResult nothingTaken = AnimalCommands.Commit(book, plan, new AnimalTaken { count = 0, qualities = new List<float>(), codes = new List<int>() }, Start, ctx);
            Assert.IsFalse(nothingTaken.ok, "without what it takes, nothing happens");
            Assert.IsFalse(AnimalBreeding.Expecting(cow));
        }

        [Test]
        public void CloseKinAreNotBredUnlessTheRulesAllowIt()
        {
            var mother = new AnimalRecord { id = "m", species = "cow", sex = AnimalSex.Female };
            var son = new AnimalRecord { id = "s", species = "cow", sex = AnimalSex.Male, mother = "m", father = "f", grandparents = new List<string> { "mm", "mf", "fm", "ff" } };
            var daughter = new AnimalRecord { id = "d", species = "cow", sex = AnimalSex.Female, mother = "m", father = "f", grandparents = new List<string> { "mm", "mf", "fm", "ff" } };
            var halfBrother = new AnimalRecord { id = "h", species = "cow", sex = AnimalSex.Male, mother = "m", father = "x", grandparents = new List<string> { "mm", "mf", "", "" } };
            var cousin = new AnimalRecord { id = "c", species = "cow", sex = AnimalSex.Male, mother = "a", father = "b", grandparents = new List<string> { "mm", "mf", "q", "r" } };
            var grandson = new AnimalRecord { id = "g", species = "cow", sex = AnimalSex.Male, mother = "d", father = "z", grandparents = new List<string> { "m", "f", "", "" } };
            var stranger = new AnimalRecord { id = "t", species = "cow", sex = AnimalSex.Male };
            Assert.AreEqual(0.25f, AnimalBreeding.Kinship(mother, son), "parent and young");
            Assert.AreEqual(0.25f, AnimalBreeding.Kinship(son, daughter), "full siblings");
            Assert.AreEqual(0.125f, AnimalBreeding.Kinship(halfBrother, daughter), "half siblings");
            Assert.AreEqual(0.125f, AnimalBreeding.Kinship(mother, grandson), "grandparent and grandchild");
            Assert.AreEqual(0.125f, AnimalBreeding.Kinship(son, grandson), "uncle and nephew");
            Assert.AreEqual(0.0625f, AnimalBreeding.Kinship(cousin, daughter), "cousins (two shared grandparents)");
            Assert.AreEqual(0f, AnimalBreeding.Kinship(stranger, daughter));

            AnimalSpeciesData sp = Catalog().Species("cow");
            AnimalRules rules = AnimalRules.Defaults();
            Assert.IsFalse(AnimalBreeding.Compatible(mother, son, sp, rules, out _, out _, out string why));
            StringAssert.Contains("too closely related", why);
            Assert.IsFalse(AnimalBreeding.Compatible(daughter, son, sp, rules, out _, out _, out _));
            Assert.IsTrue(AnimalBreeding.Compatible(daughter, halfBrother, sp, rules, out AnimalRecord dam, out AnimalRecord sire, out _), "half siblings may be bred");
            Assert.AreSame(daughter, dam);
            Assert.AreSame(halfBrother, sire);
            rules.breeding.allowCloseKin = true;
            Assert.IsTrue(AnimalBreeding.Compatible(daughter, son, sp, rules, out _, out _, out _), "unless the rules allow it");
        }

        [Test]
        public void TheChanceToConceiveFollowsFertilityWelfareAgeAndKin()
        {
            AnimalSpeciesData sp = Catalog().Species("cow");
            AnimalRules rules = AnimalRules.Defaults();
            AnimalRecord M() => new AnimalRecord { id = "m", species = "cow", sex = AnimalSex.Female, stage = GrowthStage.Adult, welfare = 60f, genes = new AnimalGenes { fertility = 0.5f } };
            AnimalRecord F() => new AnimalRecord { id = "f", species = "cow", sex = AnimalSex.Male, stage = GrowthStage.Adult, welfare = 60f, genes = new AnimalGenes { fertility = 0.5f } };
            float typical = AnimalBreeding.ConceptionChance(M(), F(), sp, rules);
            Assert.AreEqual(sp.breeding.conception, typical, 1e-5, "a typical pair: the species' chance");
            AnimalRecord fertile = M();
            fertile.genes.fertility = 1f;
            Assert.Greater(AnimalBreeding.ConceptionChance(fertile, F(), sp, rules), typical, "a fertile mother");
            AnimalRecord barren = M();
            barren.genes.fertility = 0f;
            Assert.Less(AnimalBreeding.ConceptionChance(barren, F(), sp, rules), typical * 0.6f);
            AnimalRecord happy = M();
            happy.welfare = 95f;
            Assert.Greater(AnimalBreeding.ConceptionChance(happy, F(), sp, rules), typical, "an excellent life");
            AnimalRecord old = M();
            old.stage = GrowthStage.Senior;
            Assert.AreEqual(typical * 0.6f, AnimalBreeding.ConceptionChance(old, F(), sp, rules), 1e-5, "an old mother");
            AnimalRecord half = F();
            AnimalRecord herM = M();
            herM.mother = "x";
            half.mother = "x";
            Assert.AreEqual(typical * (1f - 2f * 0.125f), AnimalBreeding.ConceptionChance(herM, half, sp, rules), 1e-5, "kin conceive less readily");
        }

        [Test]
        public void TheYoungInheritTheirParentsMeanWithConfigurableMutations()
        {
            var r = new AnimalBreedingRules { inheritSpread = 0f, mutationChance = 0f, rareInheritChance = 0f, rareMutationChance = 0f };
            var mother = new AnimalGenes { size = 0.2f, growth = 0.3f, efficiency = 0.4f, resistance = 0.6f, fertility = 0.7f, temperament = 0.8f, productionQuality = 0.9f, productionRate = 0.1f, longevity = 0.5f, coat = 1, pattern = 2, rare = 3 };
            var father = new AnimalGenes { size = 0.8f, growth = 0.7f, efficiency = 0.6f, resistance = 0.4f, fertility = 0.3f, temperament = 0.2f, productionQuality = 0.5f, productionRate = 0.9f, longevity = 0.5f, coat = 0, pattern = 0 };
            AnimalBreedData breed = Catalog().Species("cow").Breed("holstein");
            AnimalGenes g = AnimalBreeding.Inherit(mother, father, 777, r, breed, 0f);
            Assert.AreEqual(0.5f, g.size, 1e-5, "the parents' mean");
            Assert.AreEqual(0.5f, g.growth, 1e-5);
            Assert.AreEqual(0.5f, g.temperament, 1e-5);
            Assert.AreEqual(0.7f, g.productionQuality, 1e-5);
            Assert.AreEqual(0.5f, g.productionRate, 1e-5);
            Assert.AreEqual(0, g.rare, "no rare trait passed on at a chance of 0");
            Assert.IsTrue(g.pattern == 2 || g.pattern == 0, "one parent's pattern");
            Assert.Less(g.coat, Math.Max(1, breed.coats.Count), "a coat its breed has");

            // The same parents and seed give the same young; another seed another.
            r.inheritSpread = 0.06f;
            AnimalGenes a1 = AnimalBreeding.Inherit(mother, father, 777, r, breed, 0f), a2 = AnimalBreeding.Inherit(mother, father, 777, r, breed, 0f);
            AnimalGenes b1 = AnimalBreeding.Inherit(mother, father, 778, r, breed, 0f);
            Assert.AreEqual(a1.size, a2.size);
            Assert.AreNotEqual(a1.size, b1.size);
            Assert.AreEqual(0.5f, a1.size, 0.06f * 4f, "near the mean");

            // Mutations: configurable.
            var mutating = new AnimalBreedingRules { inheritSpread = 0f, mutationChance = 1f, mutationSize = 0.3f };
            int changed = 0;
            for (int seed = 1; seed <= 20; seed++)
                if (Math.Abs(AnimalBreeding.Inherit(mother, father, seed, mutating, breed, 0f).size - 0.5f) > 1e-4f) changed++;
            Assert.Greater(changed, 15, "at a chance of 1, nearly every young mutates");

            // A rare trait passes on, or (rarely) a new one appears.
            Assert.AreEqual(3, AnimalBreeding.Inherit(mother, father, 5, new AnimalBreedingRules { rareInheritChance = 1f }, breed, 0f).rare);
            Assert.AreNotEqual(0, AnimalBreeding.Inherit(father, father, 5, new AnimalBreedingRules { rareMutationChance = 1f }, breed, 0f).rare);

            // Close kin weaken their young.
            AnimalGenes fine = AnimalBreeding.Inherit(mother, father, 9, r, breed, 0f), inbred = AnimalBreeding.Inherit(mother, father, 9, r, breed, 0.25f);
            Assert.Less(inbred.resistance, fine.resistance);
            Assert.Less(inbred.fertility, fine.fertility);
            Assert.Less(inbred.longevity, fine.longevity);
            Assert.AreEqual(0.25f, inbred.inbreeding);
        }

        [Test]
        public void ALitterOfPigletsAndAClutchOfChicksAreEachTheirOwn()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 40f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start, AnimalSex.Female, seed: 11);
            AnimalRecord rooster = Release(book, ctx, coop, "p1", "chicken", "rhode_island_red", Start, AnimalSex.Male, seed: 12);
            hen.genes.fertility = 0.9f;
            double t = BreedUntilExpecting(book, ctx, coop, hen, rooster, Start);
            int litter = hen.pregnancy.litter;
            Assert.That(litter, Is.InRange(cat.Species("chicken").breeding.litterMin, cat.Species("chicken").breeding.litterMax));
            Keep(book, ctx, coop, t, (int)Math.Ceiling(hen.pregnancy.dueAt - t) + 1, "chicken_feed");
            List<AnimalRecord> chicks = YoungOf(coop, hen);
            Assert.AreEqual(litter, chicks.Count, "the whole clutch hatches");
            var ids = new HashSet<string>();
            foreach (AnimalRecord c in chicks)
            {
                Assert.IsTrue(ids.Add(c.id), "each its own id");
                foreach (AnimalRecord d in chicks)
                    if (d != c) Assert.AreEqual(AnimalBondKind.Kin, AnimalSocial.BondWith(c, d.id)?.kind, "litter-mates are kin");
            }
            if (chicks.Count > 1) Assert.AreNotEqual(chicks[0].genes.seed, chicks[1].genes.seed, "each its own genes");
        }

        [Test]
        public void ACalfSucklesFromItsMotherAndStarvesWithoutHerOrMilk()
        {
            var calves = new List<AnimalRecord>();
            foreach (bool withMother in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                var book = new AnimalBook(cat, "test");
                AnimalSimContext ctx = Ctx(cat, book);
                AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
                AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 21);
                AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 22);
                double t = BreedUntilExpecting(book, ctx, field, cow, bull, Start);
                double due = cow.pregnancy.dueAt;
                Keep(book, ctx, field, t, (int)Math.Ceiling(due - t) + 1);
                AnimalRecord calf = YoungOf(field, cow)[0];
                double born = Math.Ceiling(due - t) + 1 + t;
                if (!withMother)
                {
                    AnimalCommands.Board(book, cow, born, ctx, "taken away");
                    Assert.AreEqual(AnimalStatus.Boarded, cow.status);
                }
                float appetite = AnimalBreeding.AppetiteFactor(cow, cat.Rules, 1);
                Assert.Greater(appetite, 1f, "a nursing mother eats more");
                // Hay only (no milk replacer): a newborn calf takes none of it.
                Keep(book, ctx, field, born, 30);
                calves.Add(calf);
            }
            Assert.Greater(calves[0].satiety, 40f, $"with its mother it suckles (satiety {calves[0].satiety:0})");
            Assert.Greater(calves[0].nutrition, 50f, "and is well nourished");
            Assert.Less(calves[1].satiety, 20f, $"without her, and no milk replacer, it goes hungry (satiety {calves[1].satiety:0})");
        }

        [Test]
        public void AMotherInPoorHealthLosesHerYoung()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord ewe = Release(book, ctx, field, "p1", "sheep", "merino", Start, AnimalSex.Female, seed: 31);
            AnimalRecord ram = Release(book, ctx, field, "p1", "sheep", "suffolk", Start, AnimalSex.Male, seed: 32);
            double t = BreedUntilExpecting(book, ctx, field, ewe, ram, Start);
            ctx.notices.Clear();
            ewe.health = cat.Rules.breeding.miscarryHealthBelow - 5f;
            Keep(book, ctx, field, t, 1);
            Assert.IsFalse(AnimalBreeding.Expecting(ewe), "she lost them");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.Miscarried), "her owner is told");
            Keep(book, ctx, field, t + 1, (int)cat.Species("sheep").breeding.gestationHours + 2);
            Assert.IsEmpty(YoungOf(field, ewe), "none are born");
        }

        [Test]
        public void BirthsInACatchUpAreTheBirthsOfAWatchedStretch()
        {
            var runs = new List<List<AnimalRecord>>();
            var serials = new List<long>();
            foreach (bool watched in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                var book = new AnimalBook(cat, "test");
                AnimalSimContext ctx = Ctx(cat, book);
                AnimalHerd sty = Pen(book, "sty", AnimalHabitatKind.Sty, 200f, Start);
                AnimalRecord sow = Release(book, ctx, sty, "p1", "pig", "large_white", Start, AnimalSex.Female, seed: 41);
                AnimalRecord boar = Release(book, ctx, sty, "p1", "pig", "berkshire", Start, AnimalSex.Male, seed: 42);
                double t = BreedUntilExpecting(book, ctx, sty, sow, boar, Start);
                AnimalCommands.Stock(sty, cat.Food("grain_mix"), 5000f, 0.8f, "test", t);
                sty.pen.water = 5000f;
                double end = sow.pregnancy.dueAt + 12.0;
                if (watched)
                    for (double h = t + 1.0; h <= end + 1e-6; h += 1.0) book.BringUpToDate(sty, h, ctx);
                else book.BringUpToDate(sty, end, ctx);
                runs.Add(YoungOf(sty, sow));
                serials.Add(book.Serial);
            }
            Assert.Greater(runs[0].Count, 0, "piglets were born");
            Assert.AreEqual(runs[0].Count, runs[1].Count);
            Assert.AreEqual(serials[0], serials[1]);
            for (int i = 0; i < runs[0].Count; i++)
            {
                Assert.AreEqual(runs[0][i].id, runs[1][i].id, "the same ids");
                Assert.AreEqual(runs[0][i].genes.size, runs[1][i].genes.size, "the same genes");
                Assert.AreEqual(runs[0][i].bornAt, runs[1][i].bornAt, 1e-9);
                Assert.AreEqual(runs[0][i].satiety, runs[1][i].satiety, 1e-3, "and they lived the same hours since");
            }
        }

        [Test]
        public void APregnancyIsSavedAndTheYoungBornAfterALoadAreTheSame()
        {
            var young = new List<AnimalGenes>();
            foreach (bool reload in new[] { false, true })
            {
                AnimalCatalog cat = Catalog();
                var book = new AnimalBook(cat, "test");
                AnimalSimContext ctx = Ctx(cat, book);
                AnimalHerd hutch = Pen(book, "hutch", AnimalHabitatKind.Hutch, 60f, Start);
                AnimalRecord doe = Release(book, ctx, hutch, "p1", "rabbit", "new_zealand_white", Start, AnimalSex.Female, seed: 51);
                AnimalRecord buck = Release(book, ctx, hutch, "p1", "rabbit", "dutch", Start, AnimalSex.Male, seed: 52);
                double t = BreedUntilExpecting(book, ctx, hutch, doe, buck, Start);
                if (reload)
                {
                    string json = JsonUtility.ToJson(book.Export(t, 0));
                    book = new AnimalBook(cat, "test");
                    book.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
                    ctx = Ctx(cat, book);
                    hutch = book.Herd("hutch");
                    doe = book.Find(doe.id);
                    Assert.IsTrue(AnimalBreeding.Expecting(doe), "still expecting after a load");
                    Assert.IsNotNull(AnimalSocial.BondWith(doe, buck.id), "her bonds are saved");
                }
                Keep(book, ctx, hutch, t, (int)Math.Ceiling(doe.pregnancy.dueAt - t) + 1, "hay");
                foreach (AnimalRecord y in YoungOf(hutch, doe)) young.Add(y.genes);
            }
            Assert.Greater(young.Count, 0);
            Assert.AreEqual(0, young.Count % 2);
            int n = young.Count / 2;
            for (int i = 0; i < n; i++) Assert.AreEqual(young[i].size, young[i + n].size, "the same young, saved or not");
        }

        // ---------------------------------------------------------------- relationships
        [Test]
        public void PenMatesGrowCloseAndMissEachOtherWhenParted()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalHerd other = Pen(book, "other", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord bella = Release(book, ctx, field, "p1", "sheep", "merino", Start, AnimalSex.Female, seed: 61);
            AnimalRecord daisy = Release(book, ctx, field, "p1", "sheep", "jacob", Start, AnimalSex.Female, seed: 62);
            bella.name = "Bella";
            daisy.name = "Daisy";
            Keep(book, ctx, field, Start, 24 * 14, "fresh_grass");
            AnimalBond b = AnimalSocial.BondWith(bella, daisy.id);
            Assert.IsNotNull(b);
            Assert.GreaterOrEqual(b.level, cat.Rules.social.closeFrom, $"two weeks together, they are close ({b.level:0})");
            float together = bella.stress;

            ctx.notices.Clear();
            AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Inspect, pen = "field", animal = bella.id }, Actor("p1", AnimalRights.All), Start + 24 * 14, ctx);
            book.MoveTo(daisy, "other");
            Keep(book, ctx, field, Start + 24 * 14, 12, "fresh_grass");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.MissesCompanion && n.text.Contains("Bella seems to miss Daisy")), "her owner is told she misses her");
            Assert.Greater(bella.stress, together, $"parted, she frets ({together:0.0} then {bella.stress:0.0})");
            Assert.AreEqual(1, ctx.notices.FindAll(n => n.kind == AnimalEventKind.MissesCompanion && n.animal == bella.id).Count, "told once");
            float level = b.level;
            Keep(book, ctx, field, Start + 24 * 14 + 12, 24, "fresh_grass");
            Assert.Less(b.level, level, "apart, the bond fades");
        }

        [Test]
        public void TwoBullsInAPenAreRivalsAndFret()
        {
            var stress = new List<float>();
            foreach (bool rivals in new[] { false, true })
            {
                AnimalCatalog cat = Catalog();
                var book = new AnimalBook(cat, "test");
                AnimalSimContext ctx = Ctx(cat, book);
                AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
                AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 71);
                AnimalRecord other = Release(book, ctx, field, "p1", "cow", "holstein", Start, rivals ? AnimalSex.Male : AnimalSex.Female, seed: 72);
                Keep(book, ctx, field, Start, 24 * 7);
                AnimalBond b = AnimalSocial.BondWith(bull, other.id);
                Assert.IsNotNull(b);
                Assert.AreEqual(rivals ? AnimalBondKind.Rival : AnimalBondKind.Companion, b.kind);
                stress.Add(bull.stress);
            }
            Assert.Greater(stress[1], stress[0] + 3f, $"a rival in the pen stresses it ({stress[0]:0.0} against {stress[1]:0.0})");
        }

        // ---------------------------------------------------------------- the bond with the owner
        [Test]
        public void TimeTogetherDeepensTheBondALittleEachDayAndKeepsItFromFading()
        {
            AnimalRules rules = AnimalRules.Defaults();
            var a = new AnimalRecord { id = "a", species = "horse", stage = GrowthStage.Adult, affinity = 10f };
            float given = 0f;
            for (int i = 0; i < 24; i++) given += AnimalSocial.TimeTogether(a, 1f, Start + i, rules);
            Assert.AreEqual(rules.social.togetherPerDay, given, 1e-4, "at most Together Per Day in a day");
            Assert.AreEqual(10f + rules.social.togetherPerDay, a.affinity, 1e-4);
            Assert.AreEqual(Start + 23, a.lastOwnerNearAt, 1e-9);
            Assert.Greater(AnimalSocial.TimeTogether(a, 1f, Start + 24, rules), 0f, "a new day, more");

            // An animal whose owner spends time near it keeps its bond; one left alone loses it.
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord near = Release(book, ctx, field, "p1", "horse", "arabian", Start, AnimalSex.Female, seed: 81);
            AnimalRecord far = Release(book, ctx, field, "p1", "horse", "arabian", Start, AnimalSex.Female, seed: 82);
            near.affinity = far.affinity = 50f;
            for (int day = 0; day < 6; day++)
            {
                double t = Start + day * 24.0;
                AnimalSocial.TimeTogether(near, 0.5f, t + 1.0, cat.Rules);
                Keep(book, ctx, field, t, 24);
            }
            Assert.Greater(near.affinity, far.affinity, "time together keeps the bond");
            Assert.Less(far.affinity, 50f, "neglect lets it fade");
        }

        [Test]
        public void ADeepBondUnlocksPerksAndCalmsHandlingButNeverStandsInForWelfare()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData horse = cat.Species("horse"), cow = cat.Species("cow");
            var a = new AnimalRecord { id = "a", species = "horse", stage = GrowthStage.Adult, affinity = 20f };
            Assert.IsFalse(AnimalSocial.HasPerk(a, horse, AnimalPerkKind.ComeWhenCalled));
            a.affinity = 55f;
            Assert.IsTrue(AnimalSocial.HasPerk(a, horse, AnimalPerkKind.ComeWhenCalled), "a horse comes when called at 50");
            Assert.IsFalse(AnimalSocial.HasPerk(a, horse, AnimalPerkKind.Ribbon));
            a.affinity = 95f;
            var perks = new List<AnimalAffinityPerk>();
            AnimalSocial.Perks(a, horse, perks);
            Assert.AreEqual(horse.perks.Count, perks.Count, "everything at 95");
            Assert.AreEqual("devoted", AnimalSocial.BondWord(a.affinity));

            // Handling: a trusting animal is easier for anyone, its owner never harder than anyone else, and its owner not at
            // all once it is calm with them.
            var c = new AnimalRecord { id = "c", species = "cow", stage = GrowthStage.Adult, affinity = 30f };
            Assert.AreEqual(1f - 0.3f * cat.Rules.social.handlingRelief, AnimalSocial.HandlingFactor(c, cow, true, cat.Rules), 1e-5);
            Assert.AreEqual(AnimalSocial.HandlingFactor(c, cow, true, cat.Rules), AnimalSocial.HandlingFactor(c, cow, false, cat.Rules), 1e-6);
            c.affinity = 65f;
            Assert.AreEqual(0f, AnimalSocial.HandlingFactor(c, cow, true, cat.Rules), "calm with its owner");
            Assert.Greater(AnimalSocial.HandlingFactor(c, cow, false, cat.Rules), 0f, "but not with a stranger");

            // Milking a cow calm with its owner: no stress.
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord daisy = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 91);
            Thriving(daisy);
            daisy.affinity = 70f;
            daisy.genes.temperament = 0f;
            AnimalProduction.Ripen(daisy, cow, cat.Rules, Start);
            AnimalActor milker = Actor("p1", AnimalRights.All);
            milker.tool = tags => new AnimalToolUse(AnimalIds.Tool("milking_pail"), "milking_pail", 0.7f, 1f);
            float before = daisy.stress;
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "field", animal = daisy.id }, milker, Start);
            Assert.AreEqual(before, daisy.stress, 1e-4, "she lets down her milk for her owner without a fuss");

            // A devoted animal can still be starving and sick: affinity is one part of welfare.
            AnimalRecord starving = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 92);
            starving.affinity = 100f;
            starving.satiety = 0f;
            starving.hydration = 0f;
            starving.nutrition = 20f;
            starving.health = 30f;
            starving.hygiene = 20f;
            starving.comfort = 20f;
            starving.happiness = 10f;
            float w = AnimalWelfare.Compute(starving, cat.Rules.welfare);
            Assert.Less(w, 40f, $"devoted but neglected: its welfare stays poor ({w:0})");
        }

        // ---------------------------------------------------------------- what is shown
        [Test]
        public void ItsOwnerSeesItsFamilyBondAndPartnersAndOthersOnlyItsWays()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 400f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 101);
            AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 102);
            AnimalRecord young = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, GrowthStage.Juvenile, 103);
            foreach (AnimalRecord x in field.animals) Thriving(x);
            cow.affinity = 70f;

            AnimalPenView mine = AnimalObservation.PenView(book, field, "p1", AnimalRights.All, Start);
            AnimalView cv = mine.animals.Find(v => v.id == cow.id);
            Assert.AreEqual(3, cv.bond);
            Assert.AreEqual("friendly", cv.bondWord);
            Assert.IsTrue(cv.perks.Contains("Lets down her milk for you"));
            Assert.IsTrue(cv.traits.Exists(s => s.StartsWith("Fertility")), "her inheritance, in words");
            Assert.IsTrue(cv.breedReady);
            AnimalPartnerView pv = cv.partners.Find(p => p.id == bull.id);
            Assert.IsNotNull(pv, "the bull is offered as her partner");
            Assert.IsTrue(pv.ready);
            AnimalPartnerView yv = cv.partners.Find(p => p.id == young.id);
            Assert.IsNotNull(yv);
            Assert.IsFalse(yv.ready);
            StringAssert.Contains("too young", yv.hint);
            Assert.IsTrue(cv.greets, "she greets her owner (in her owner's picture)");

            double t = BreedUntilExpecting(book, ctx, field, cow, bull, Start);
            cv = AnimalObservation.PenView(book, field, "p1", AnimalRights.All, t + 1.0).animals.Find(v => v.id == cow.id);
            StringAssert.StartsWith("in calf: due in about", cv.expecting);
            Assert.IsFalse(cv.breedReady);
            Assert.IsTrue(cv.relations.Exists(r => r.Contains("paired with")), string.Join(" | ", cv.relations));

            // A stranger sees how it behaves, not its family, bond or breeding.
            AnimalView seen = AnimalObservation.PenView(book, field, "p9", AnimalRights.Look, t + 1.0).animals.Find(v => v.id == cow.id);
            Assert.AreEqual(-1, seen.bond);
            Assert.IsEmpty(seen.perks);
            Assert.IsEmpty(seen.traits);
            Assert.IsEmpty(seen.partners);
            Assert.AreEqual("", seen.expecting);
            Assert.IsEmpty(seen.relations);
            Assert.IsFalse(seen.greets, "she greets only her own owner");
            Assert.AreEqual(cow.genes.temperament < 0.38f, seen.shy, "only whether it is shy of people");
            string json = JsonUtility.ToJson(seen);
            StringAssert.DoesNotContain("\"affinity\"", json, "no number of the bond");
            StringAssert.DoesNotContain("\"fertility\"", json, "no gene numbers");

            // Late in carrying, every viewer sees it.
            double late = cow.pregnancy.conceivedAt + (cow.pregnancy.dueAt - cow.pregnancy.conceivedAt) * 0.7;
            Keep(book, ctx, field, t, (int)Math.Ceiling(late - t));
            AnimalView heavy = AnimalObservation.PenView(book, field, "p9", AnimalRights.Look, late + 1.0).animals.Find(v => v.id == cow.id);
            Assert.IsTrue(heavy.heavy);
            Assert.IsTrue(heavy.signs.Contains("She is heavy with young."));
        }

        // ---------------------------------------------------------------- the review's cases
        [Test]
        public void YoungOnTheWayCountInTheOwnersLimitAndThePensRoom()
        {
            AnimalRules rules = AnimalRules.Defaults();
            rules.maxAnimalsPerPlayer = 5;
            AnimalCatalog cat = Catalog(rules);
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 400f, Start);
            AnimalRecord c1 = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 201);
            AnimalRecord c2 = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 202);
            AnimalRecord b1 = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 203);
            AnimalRecord b2 = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 204);
            double t = BreedUntilExpecting(book, ctx, field, c1, b1, Start);
            Assert.AreEqual(1, AnimalBreeding.ExpectedFor(book, "p1"));
            Thriving(c2);
            Thriving(b2);
            StringAssert.Contains("young on the way counted", Refusal(book, ctx, Breed(c2, b2), Actor("p1", AnimalRights.All), t), "4 living + 1 on the way + 1 more > 5");
            string crate = AnimalIds.Livestock("cow", "jersey");
            StringAssert.Contains("young on the way counted",
                Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Release, pen = "field", item = crate }, Actor("p1", AnimalRights.All, new Dictionary<string, int> { [crate] = 1 }), t));

            // The pen's room counts the young on the way to its mothers.
            AnimalCatalog roomy = Catalog();
            var book2 = new AnimalBook(roomy, "test");
            AnimalSimContext ctx2 = Ctx(roomy, book2);
            AnimalHerd small = Pen(book2, "small", AnimalHabitatKind.Pasture, 90f, Start);
            AnimalRecord d1 = Release(book2, ctx2, small, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 211);
            AnimalRecord d2 = Release(book2, ctx2, small, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 212);
            AnimalRecord e1 = Release(book2, ctx2, small, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 213);
            AnimalRecord e2 = Release(book2, ctx2, small, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 214);
            double t2 = BreedUntilExpecting(book2, ctx2, small, d1, e1, Start);
            Thriving(d2);
            Thriving(e2);
            StringAssert.Contains("no room here for their young", Refusal(book2, ctx2, Breed(d2, e2), Actor("p1", AnimalRights.All), t2), "64 m2 of cows + 16 on the way + 16 more > 90");
        }

        [Test]
        public void TheYoungsIdsAreFixedAtTheMatingWhateverOrderTheyAreBornIn()
        {
            var ids = new List<List<string>>();
            foreach (bool watched in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                var book = new AnimalBook(cat, "test");
                AnimalSimContext ctx = Ctx(cat, book);
                AnimalHerd a = Pen(book, "a", AnimalHabitatKind.Hutch, 80f, Start);
                AnimalHerd b = Pen(book, "b", AnimalHabitatKind.Hutch, 80f, Start);
                AnimalRecord doeA = Release(book, ctx, a, "p1", "rabbit", "dutch", Start, AnimalSex.Female, seed: 221);
                AnimalRecord buckA = Release(book, ctx, a, "p1", "rabbit", "rex", Start, AnimalSex.Male, seed: 222);
                AnimalRecord doeB = Release(book, ctx, b, "p1", "rabbit", "dutch", Start, AnimalSex.Female, seed: 223);
                AnimalRecord buckB = Release(book, ctx, b, "p1", "rabbit", "rex", Start, AnimalSex.Male, seed: 224);
                double ta = BreedUntilExpecting(book, ctx, a, doeA, buckA, Start);
                Assert.AreEqual(doeA.pregnancy.litter, doeA.pregnancy.young.Count, "the litter's ids are taken at the mating");
                book.BringUpToDate(b, ta + 10.0, ctx);
                double tb = BreedUntilExpecting(book, ctx, b, doeB, buckB, ta + 10.0);
                double end = Math.Max(doeA.pregnancy.dueAt, doeB.pregnancy.dueAt) + 4.0;
                foreach (AnimalHerd h in new[] { a, b })
                {
                    AnimalCommands.Stock(h, cat.Food("hay"), 2000f, 0.8f, "test", tb);
                    h.pen.water = 5000f;
                }
                if (watched)
                    for (double hr = tb + 1.0; hr <= end + 1e-6; hr += 1.0)
                    {
                        book.BringUpToDate(a, hr, ctx);
                        book.BringUpToDate(b, hr, ctx);
                    }
                else
                {
                    // A catch-up that reaches the later pen first.
                    book.BringUpToDate(b, end, ctx);
                    book.BringUpToDate(a, end, ctx);
                }
                // Each litter's ids (who got which matters, not only which serials were used).
                var ofA = new List<string>();
                var ofB = new List<string>();
                foreach (AnimalRecord y in YoungOf(a, doeA)) ofA.Add(y.id);
                foreach (AnimalRecord y in YoungOf(b, doeB)) ofB.Add(y.id);
                ofA.Sort(string.CompareOrdinal);
                ofB.Sort(string.CompareOrdinal);
                var run = new List<string>(ofA) { "|" };
                run.AddRange(ofB);
                ids.Add(run);
            }
            Assert.Greater(ids[0].Count, 2, "both litters were born");
            CollectionAssert.AreEqual(ids[0], ids[1], "each mother's young have the same ids, whatever order the pens were brought up to date in");
        }

        [Test]
        public void TimeTogetherNeverMakesADayOfHistoryAheadOfTheSimulation()
        {
            AnimalRules rules = AnimalRules.Defaults();
            var a = new AnimalRecord { id = "a", species = "horse", stage = GrowthStage.Adult, affinity = 10f };
            a.days.Add(new AnimalDay { day = 3, hours = 23.5f });
            AnimalSocial.TimeTogether(a, 1f, 4 * 24.0 + 0.2, rules);
            Assert.AreEqual(1, a.days.Count, "the simulation, not time together, opens the next day");
            Assert.AreEqual(3, a.days[0].day);
            float second = AnimalSocial.TimeTogether(a, 10f, 4 * 24.0 + 5.0, rules);
            Assert.AreEqual(rules.social.togetherPerDay - rules.social.togetherPerHour, second, 1e-4, "the day's share is counted on the record");
            Assert.AreEqual(0f, AnimalSocial.TimeTogether(a, 1f, 4 * 24.0 + 6.0, rules));
        }

        [Test]
        public void ANewRivalIsNoticedInACrowdedPenAndOldCompanyDoesNotMakeItFierce()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 400f, Start);
            AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 231);
            for (int i = 0; i < 10; i++) Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 240 + i);
            Keep(book, ctx, field, Start, 48);
            int companions = bull.bonds.FindAll(b => b.kind == AnimalBondKind.Companion).Count;
            Assert.LessOrEqual(companions, cat.Rules.social.keepBonds, "at most Keep Bonds companions");
            var before = new HashSet<string>();
            foreach (AnimalBond b in bull.bonds) before.Add(b.other);
            Keep(book, ctx, field, Start + 48, 2);
            foreach (AnimalBond b in bull.bonds) Assert.IsTrue(before.Contains(b.other), "the bonds it has are kept, not made and dropped each step");

            AnimalRecord newcomer = Release(book, ctx, field, "p1", "cow", "angus", Start + 50, AnimalSex.Male, seed: 260);
            Keep(book, ctx, field, Start + 50, 24);
            AnimalBond rival = AnimalSocial.BondWith(bull, newcomer.id);
            Assert.IsNotNull(rival, "the established bull notices the new one, however many companions it has");
            Assert.AreEqual(AnimalBondKind.Rival, rival.kind);

            // Two males raised as companions: their rivalry starts afresh when they are grown.
            var x = new AnimalRecord { id = "x", species = "sheep", sex = AnimalSex.Male, stage = GrowthStage.Adult };
            var y = new AnimalRecord { id = "y", species = "sheep", sex = AnimalSex.Male, stage = GrowthStage.Adult };
            AnimalSocial.Bond(x, y, AnimalBondKind.Companion, 60f, Start);
            AnimalSocial.Bond(y, x, AnimalBondKind.Companion, 60f, Start);
            AnimalHerd pair = Pen(book, "pair", AnimalHabitatKind.Pasture, 100f, Start);
            pair.animals.Add(x);
            pair.animals.Add(y);
            AnimalSocial.StepHerd(pair, cat, cat.Rules, 1f, Start + 1.0);
            Assert.AreEqual(AnimalBondKind.Rival, AnimalSocial.BondWith(x, "y").kind);
            Assert.Less(AnimalSocial.BondWith(x, "y").level, 5f, "not fierce at once");
        }

        [Test]
        public void YoungBornAtTheBoardingStableAreWithTheirMother()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd hutch = Pen(book, "hutch", AnimalHabitatKind.Hutch, 80f, Start);
            AnimalRecord doe = Release(book, ctx, hutch, "p1", "rabbit", "dutch", Start, AnimalSex.Female, seed: 271);
            AnimalRecord buck = Release(book, ctx, hutch, "p1", "rabbit", "rex", Start, AnimalSex.Male, seed: 272);
            double t = BreedUntilExpecting(book, ctx, hutch, doe, buck, Start);
            AnimalCommands.Board(book, doe, t, ctx, "the lease ended");
            ctx.notices.Clear();
            double end = doe.pregnancy.dueAt + 24.0;
            for (double h = t + 1.0; h <= end; h += 1.0) book.BringUpToDate(doe, h, ctx);
            var young = new List<AnimalRecord>();
            foreach (AnimalRecord a in book.Boarded)
                if (a.mother == doe.id) young.Add(a);
            Assert.Greater(young.Count, 0, "born at the stable with her");
            foreach (AnimalRecord y in young) book.BringUpToDate(y, end, ctx);
            Assert.IsFalse(ctx.notices.Exists(n => n.kind == AnimalEventKind.MissesCompanion && (n.animal == doe.id || young.Exists(y => y.id == n.animal))),
                "mother and young are together there: nobody misses anybody");
        }

        [Test]
        public void ALookTellsAStrangerNothingOfAPregnancyAndBreedingTakesItsCostWhole()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 281);
            AnimalRecord bull = Release(book, ctx, field, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 282);
            double t = BreedUntilExpecting(book, ctx, field, cow, bull, Start);
            var lines = new List<string>();
            AnimalObservation.Describe(cow, cat.Species("cow"), cat, t, false, lines);
            Assert.IsFalse(lines.Exists(l => l.Contains("in calf")), string.Join(" | ", lines));
            AnimalObservation.Describe(cow, cat.Species("cow"), cat, t, true, lines);
            Assert.IsTrue(lines.Exists(l => l.Contains("in calf")), "its owner is told");

            AnimalCatalog costly = Catalog();
            costly.Species("cow").breeding.costItem = "animal:feed/grain_mix";
            costly.Species("cow").breeding.costCount = 3;
            var book2 = new AnimalBook(costly, "test");
            AnimalSimContext ctx2 = Ctx(costly, book2);
            AnimalHerd f2 = Pen(book2, "f2", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord c2 = Release(book2, ctx2, f2, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 283);
            AnimalRecord b2 = Release(book2, ctx2, f2, "p1", "cow", "angus", Start, AnimalSex.Male, seed: 284);
            Thriving(c2);
            Thriving(b2);
            AnimalPlan plan = AnimalCommands.Plan(book2, Breed(c2, b2), Actor("p1", AnimalRights.All, new Dictionary<string, int> { ["animal:feed/grain_mix"] = 3 }), Start, ctx2);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            AnimalResult part = AnimalCommands.Commit(book2, plan, new AnimalTaken { count = 2, qualities = new List<float> { 0.8f, 0.8f }, codes = new List<int> { 0, 0 } }, Start, ctx2);
            Assert.IsFalse(part.ok, "two of three is not what it takes");
            Assert.AreEqual(0, c2.breedAttempts, "nothing was done");
        }

        [Test]
        public void ARecordOfAnOlderSaveLoadsWithNoFamilyYet()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat, book);
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, AnimalSex.Female, seed: 111);
            AnimalWorldSave save = book.Export(Start, 0);
            save.version = 3;
            string json = JsonUtility.ToJson(save);
            foreach (string field4 in new[] { "\"mother\":\"\",", "\"father\":\"\",", "\"grandparents\":[],", "\"bonds\":[]," }) json = json.Replace(field4, "");
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            AnimalRecord back = loaded.Find(cow.id);
            Assert.IsNotNull(back.pregnancy);
            Assert.IsFalse(AnimalBreeding.Expecting(back));
            Assert.IsNotNull(back.bonds);
            Assert.IsNotNull(back.grandparents);
            Assert.AreEqual("", back.mother);
            Assert.AreEqual(0, back.generation);
            Assert.AreEqual(AnimalWorldSave.CurrentVersion, loaded.Export(Start, 0).version);
        }
    }
}
#endif
