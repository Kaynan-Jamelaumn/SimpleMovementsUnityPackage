#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AnimalHusbandrySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of Phase 3 of animal husbandry, production (plain C#: no scene, no assets): what each built-in species
    /// gives; how fast it makes it (no one thing done well is enough) and when it stops (its stage, its sex, hunger, illness,
    /// a full nest, the boarding stable); the quality formula, its weights and limits, the tiers and Special; quality judged
    /// over the whole cycle, never the last meal; freshness; collecting (by hand, with a milking pail or shears, by its owner
    /// or a carer, from one animal or all of them) into copies whose codes carry their quality and their lot; the ledger that
    /// tells where a copy came from, saved and never reusing a serial; catch-ups that make what a watched stretch makes; the
    /// views, the history and the checks of the content.
    /// </summary>
    public class AnimalProductionTests
    {
        // ---------------------------------------------------------------- helpers
        /// <summary>Day 2 of spring (a whole hour: steps fall on the hour).</summary>
        private const double Start = 48.0;

        private const string Egg = "egg";
        private const string Milk = "milk";
        private const string Wool = "wool";

        private static AnimalCatalog Catalog(AnimalRules r = null) => AnimalCatalog.BuiltIn(r ?? AnimalRules.Defaults());

        private static AnimalSimContext Ctx(IAnimalCatalog cat, bool present = true) => new AnimalSimContext
        {
            catalog = cat,
            climate = new CityClimateSource(AgriSettings.Defaults()),
            ownerPresent = (k, t) => present,
            notices = new List<AnimalNotice>(),
        };

        private static AnimalHerd Pen(AnimalBook book, string id, AnimalHabitatKind kind, float area, double now)
        {
            var spec = new AnimalPenSpec
            {
                id = id, name = id, kind = kind, area = area, feederCapacity = 5000f, waterCapacity = 2000f, troughSurface = 0f, grassCapacity = 3f,
            };
            return book.EnsurePen(spec, "Test", "", now);
        }

        private static AnimalActor Actor(string key, AnimalRights rights) => new AnimalActor
        {
            key = key,
            name = key,
            rights = rights,
            count = id => 0,
            waterKind = (int)WaterSourceKind.Well,
        };

        /// <summary>A player who also carries these tools (by tool id; <paramref name="edge"/> 1: new, 0: worn out).</summary>
        private static AnimalActor WithTools(AnimalActor actor, IAnimalCatalog cat, float edge, params string[] tools)
        {
            actor.tool = tags =>
            {
                AnimalToolUse best = default;
                float score = -1f;
                foreach (string id in tools)
                {
                    AnimalToolData t = cat.Tool(id);
                    if (t == null || t.consumed || !t.HasAny(tags)) continue;
                    float s = t.quality * (0.5f + 0.5f * edge);
                    if (s <= score) continue;
                    score = s;
                    best = new AnimalToolUse(t.items.Count > 0 ? t.items[0] : AnimalIds.Tool(id), t.id, t.quality, edge);
                }
                return best;
            };
            return actor;
        }

        private static void Stock(IAnimalCatalog cat, AnimalHerd herd, string food, float portions, float quality, double now) =>
            AnimalCommands.Stock(herd, cat.Food(food), portions, quality, "test", now);

        private static AnimalTaken Nothing() => new AnimalTaken { count = 0, qualities = new List<float>(), codes = new List<int>() };

        /// <summary>Lets an adult (or another stage) out into a pen the way a player does: plan, take the crate, commit.</summary>
        private static AnimalRecord Release(AnimalBook book, AnimalSimContext ctx, AnimalHerd herd, string owner, string species, string breed, double now,
            AnimalSex sex = AnimalSex.Female, GrowthStage stage = GrowthStage.Adult, int seed = 12345)
        {
            string item = AnimalIds.Livestock(species, breed);
            var actor = Actor(owner, AnimalRights.All);
            actor.count = id => id == item ? 1 : 0;
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

        /// <summary>Middling genes for production, no rare trait (so tests do not hang on a seed).</summary>
        private static void Typical(AnimalRecord a)
        {
            a.genes ??= new AnimalGenes();
            a.genes.productionRate = 0.5f;
            a.genes.productionQuality = 0.5f;
            a.genes.temperament = 0.5f;
            a.genes.rare = 0;
        }

        /// <summary>A well kept animal, as the simulation would leave one after good days.</summary>
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

        /// <summary>Plans a Collect request and commits it (asserts it was done); the plan carries what the player is given.</summary>
        private static AnimalResult Collect(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now, out AnimalPlan plan)
        {
            plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsFalse(plan.Refused, $"Collect: {plan.refusal?.message}");
            AnimalResult res = AnimalCommands.Commit(book, plan, Nothing(), now, ctx);
            Assert.IsTrue(res.ok, $"Collect: {res.message}");
            return res;
        }

        private static string Refusal(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsTrue(plan.Refused, $"{r.kind} should be refused");
            return plan.refusal.message;
        }

        private static AnimalRequest CollectFrom(AnimalRecord a, string product = "") =>
            new AnimalRequest { kind = AnimalActionKind.Collect, pen = a.pen, animal = a.id, text = product };

        /// <summary>A step of production alone (no simulation around it), the way the simulation calls it.</summary>
        private static void Step(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double from, double to, float dirt = 0f, bool boarded = false,
            float bandProduction = 1f, float illness = 1f, float pace = 1f, List<AnimalProductSpec> filled = null)
        {
            var f = new AnimalStepFacts { boarded = boarded, dirt = dirt, kind = AnimalHabitatKind.Coop, area = 9f };
            var band = new AnimalWelfareBandEffects { production = bandProduction };
            AnimalStageEffects fx = AnimalStageEffects.Neutral;
            fx.production = illness;
            AnimalProduction.Step(a, sp, in f, rules, band, in fx, (float)(to - from), pace, from, to, 0f, filled);
        }

        private static int Made(AnimalRecord a, string product)
        {
            int n = 0;
            if (a.productLog != null)
                foreach (AnimalProductEntry e in a.productLog)
                    if (e != null && e.product == product && e.kind == AnimalProductEntryKind.Made) n += e.units;
            return n;
        }

        private static float MeanQuality(AnimalProductState st)
        {
            float q = 0f;
            int n = 0;
            foreach (AnimalProductLot l in st.waiting)
            {
                q += l.quality * l.units;
                n += l.units;
            }
            return n > 0 ? q / n : 0f;
        }

        /// <summary>Hens fed and watered, their coop kept clean, for some days (hour by hour).</summary>
        private static void KeepCoop(AnimalSimContext ctx, AnimalHerd h, double from, int hours)
        {
            for (int hr = 1; hr <= hours; hr++)
            {
                h.pen.water = 1000f;
                h.pen.dirt = 0f;
                AnimalSim.AdvanceHerd(h, from + hr, ctx);
            }
        }

        // ---------------------------------------------------------------- the content
        [Test]
        public void TheBuiltInSpeciesGiveWhatTheyShould()
        {
            AnimalCatalog cat = Catalog();
            AnimalProductSpec egg = AnimalProduction.Spec(cat.Species("chicken"), Egg);
            AnimalProductSpec duckEgg = AnimalProduction.Spec(cat.Species("duck"), "duck_egg");
            AnimalProductSpec milk = AnimalProduction.Spec(cat.Species("cow"), Milk);
            AnimalProductSpec goatMilk = AnimalProduction.Spec(cat.Species("goat"), "goat_milk");
            AnimalProductSpec wool = AnimalProduction.Spec(cat.Species("sheep"), Wool);
            foreach (AnimalProductSpec p in new[] { egg, duckEgg, milk, goatMilk, wool }) Assert.IsNotNull(p);

            Assert.IsTrue(egg.femaleOnly && egg.edible && !egg.NeedsTool && !egg.drink, "an egg: a hen's, food, gathered by hand");
            Assert.IsTrue(milk.femaleOnly && milk.edible && milk.drink && milk.tools.Contains("milking") && milk.yieldScales, "milk: a cow's, a drink, milked with a pail, more from a well kept cow");
            Assert.IsTrue(!wool.femaleOnly && !wool.edible && wool.tools.Contains("shears") && wool.tags.Contains("fiber"), "wool: any sheep's, a material, shorn");
            Assert.AreEqual("Wool", wool.DisplayName, "named Wool, so the tailor's bench takes it as wool fibre");
            Assert.AreEqual("animal:product/egg", egg.Item);
            Assert.AreEqual("1 egg", egg.Count(1));
            Assert.AreEqual("3 eggs", egg.Count(3));
            Assert.AreEqual("2 jugs of milk", milk.Count(2));
            Assert.AreEqual("3 bundles of wool", wool.Count(3));
            Assert.AreEqual("Egg Yolk", egg.alchemyIngredient);
            Assert.IsEmpty(cat.Species("pig").products, "a pig gives nothing while it lives");

            // Adults and seniors give; the young do not.
            AnimalSpeciesData hen = cat.Species("chicken");
            Assert.IsTrue(hen.StageOrNearest(GrowthStage.Adult).productionEnabled);
            Assert.IsTrue(hen.StageOrNearest(GrowthStage.Senior).productionEnabled);
            Assert.IsFalse(hen.StageOrNearest(GrowthStage.Juvenile).productionEnabled);

            // The tools collecting takes are animal husbandry's own items.
            AnimalToolData shears = cat.Tool("shears"), pail = cat.Tool("milking_pail");
            Assert.IsNotNull(shears);
            Assert.IsNotNull(pail);
            Assert.IsTrue(shears.HasTag("shears") && !shears.consumed && shears.IsItem(AnimalIds.Tool("shears")));
            Assert.IsTrue(pail.HasTag("milking") && !pail.consumed);

            // And every built-in species is sound.
            foreach (AnimalSpeciesData sp in cat.AllSpecies)
            {
                var errors = new List<string>();
                var warnings = new List<string>();
                sp.Validate(errors, warnings);
                Assert.IsEmpty(errors, sp.id + ": " + string.Join(" | ", errors));
                Assert.IsFalse(warnings.Exists(w => w.Contains("product")), sp.id + ": " + string.Join(" | ", warnings));
            }
            var ruleErrors = new List<string>();
            var ruleWarnings = new List<string>();
            cat.Rules.production.Validate(ruleErrors, ruleWarnings);
            Assert.IsEmpty(ruleErrors);
            Assert.IsEmpty(ruleWarnings);
        }

        [Test]
        public void ContentThatCanNeverBeMadeIsCaught()
        {
            AnimalSpeciesData sp = Catalog().Species("chicken");
            var bad = new AnimalProductSpec { id = "egg", perCycle = 4, capacity = 2, cycleHours = 0.1f };
            var errors = new List<string>();
            var warnings = new List<string>();
            bad.Validate("'x'", errors, warnings);
            Assert.IsTrue(errors.Exists(e => e.Contains("half an hour")), string.Join(" | ", errors));
            Assert.IsTrue(warnings.Exists(w => w.Contains("never make one")), string.Join(" | ", warnings));
            bad = new AnimalProductSpec { id = "an egg" };
            errors.Clear();
            bad.Validate("'x'", errors, warnings);
            Assert.IsTrue(errors.Exists(e => e.Contains("invalid id")));

            // Two products of one id, and products in no stage of its life.
            sp.products.Add(new AnimalProductSpec { id = Egg, name = "Egg", cycleHours = 24f });
            foreach (AnimalStageSpec st in sp.stages) st.productionEnabled = false;
            errors.Clear();
            warnings.Clear();
            sp.Validate(errors, warnings);
            Assert.IsTrue(errors.Exists(e => e.Contains("two products")), string.Join(" | ", errors));
            Assert.IsTrue(warnings.Exists(w => w.Contains("no stage")), string.Join(" | ", warnings));

            var rules = new AnimalProductionRules { nutritionWeight = 0f, welfareWeight = 0f, geneticsWeight = 0f, healthWeight = 0f, environmentWeight = 0f, qualityMin = 0.9f, qualityMax = 0.5f };
            errors.Clear();
            rules.Validate(errors, warnings);
            Assert.IsTrue(errors.Exists(e => e.Contains("every quality weight")));
            Assert.IsTrue(errors.Exists(e => e.Contains("Quality Min")));
        }

        // ---------------------------------------------------------------- quality (FR-07)
        [Test]
        public void QualityIsTheRulesFormulaOfItsCycleWithinTheLimits()
        {
            var r = new AnimalProductionRules();
            Assert.AreEqual(0.30f, r.nutritionWeight);
            Assert.AreEqual(0.25f, r.welfareWeight);
            Assert.AreEqual(0.20f, r.geneticsWeight);
            Assert.AreEqual(0.15f, r.healthWeight);
            Assert.AreEqual(0.10f, r.environmentWeight);
            var p = new AnimalProductSpec { id = Egg, hygieneWeight = 0f };
            var a = new AnimalRecord { id = "a", species = "chicken", stage = GrowthStage.Adult, genes = new AnimalGenes { productionQuality = 0.5f } };
            var lot = new AnimalProductLot { nutrition = 80f, welfare = 60f, health = 90f, environment = 50f, hygiene = 100f };
            float expected = 0.30f * 0.8f + 0.25f * 0.6f + 0.20f * 0.5f + 0.15f * 0.9f + 0.10f * 0.5f;
            Assert.AreEqual(expected, AnimalProduction.Quality(lot, a, p, r, 0f), 1e-5, "Q = 0.30N + 0.25W + 0.20G + 0.15H + 0.10E");
            Assert.AreEqual(expected + r.ownerCareBonus, AnimalProduction.Quality(lot, a, p, r, 1f), 1e-5, "its owner's own care adds a little");
            a.stage = GrowthStage.Senior;
            Assert.AreEqual(expected + r.seniorQuality, AnimalProduction.Quality(lot, a, p, r, 0f), 1e-5, "an old animal's are a little less good");
            a.stage = GrowthStage.Adult;

            // The weights are the rules': only nutrition, say.
            var fed = new AnimalProductionRules { nutritionWeight = 1f, welfareWeight = 0f, geneticsWeight = 0f, healthWeight = 0f, environmentWeight = 0f };
            Assert.AreEqual(0.8f, AnimalProduction.Quality(lot, a, p, fed, 0f), 1e-5);
            // Weights that do not add to 1 are normalised.
            var doubled = new AnimalProductionRules { nutritionWeight = 0.6f, welfareWeight = 0.5f, geneticsWeight = 0.4f, healthWeight = 0.3f, environmentWeight = 0.2f };
            Assert.AreEqual(expected, AnimalProduction.Quality(lot, a, p, doubled, 0f), 1e-5);

            // Within the limits.
            var limited = new AnimalProductionRules { qualityMin = 0.3f, qualityMax = 0.7f };
            var awful = new AnimalProductLot();
            a.genes.productionQuality = 0f;
            Assert.AreEqual(0.3f, AnimalProduction.Quality(awful, a, p, limited, 0f), 1e-5);
            var perfect = new AnimalProductLot { nutrition = 100f, welfare = 100f, health = 100f, environment = 100f, hygiene = 100f };
            a.genes.productionQuality = 1f;
            Assert.AreEqual(0.7f, AnimalProduction.Quality(perfect, a, p, limited, 1f), 1e-5);
            Assert.AreEqual(1f, AnimalProduction.Quality(perfect, a, p, r, 1f), 1e-5, "never above 1");
        }

        [Test]
        public void ADirtyAnimalGivesWorseByItsProductsHygieneWeight()
        {
            var r = new AnimalProductionRules();
            var a = new AnimalRecord { id = "a", species = "sheep", stage = GrowthStage.Adult, genes = new AnimalGenes { productionQuality = 0.5f } };
            var clean = new AnimalProductLot { nutrition = 80f, welfare = 80f, health = 80f, environment = 80f, hygiene = 100f };
            var dirty = new AnimalProductLot { nutrition = 80f, welfare = 80f, health = 80f, environment = 80f, hygiene = 0f };
            var fleece = new AnimalProductSpec { id = Wool, hygieneWeight = 0.5f };
            var egg = new AnimalProductSpec { id = Egg, hygieneWeight = 0f };
            float q = AnimalProduction.Quality(clean, a, fleece, r, 0f);
            Assert.AreEqual(q * (1f - 0.5f + 0.5f * 0.55f), AnimalProduction.Quality(dirty, a, fleece, r, 0f), 1e-5, "a matted fleece is worth less");
            Assert.AreEqual(AnimalProduction.Quality(clean, a, egg, r, 0f), AnimalProduction.Quality(dirty, a, egg, r, 0f), 1e-6, "what does not weigh it is not changed by it");
        }

        [Test]
        public void SixTiersAndOnlyARareTraitMakesSpecial()
        {
            var r = new AnimalProductionRules();
            Assert.AreEqual(AnimalProductTier.Poor, AnimalProduction.Tier(0.2f, false, r));
            Assert.AreEqual(AnimalProductTier.Common, AnimalProduction.Tier(r.commonFrom, false, r));
            Assert.AreEqual(AnimalProductTier.Good, AnimalProduction.Tier(r.goodFrom, false, r));
            Assert.AreEqual(AnimalProductTier.Excellent, AnimalProduction.Tier(r.excellentFrom, false, r));
            Assert.AreEqual(AnimalProductTier.Premium, AnimalProduction.Tier(r.premiumFrom, false, r));
            Assert.AreEqual(AnimalProductTier.Premium, AnimalProduction.Tier(0.99f, false, r), "no rare trait: at most Premium");
            Assert.AreEqual(AnimalProductTier.Special, AnimalProduction.Tier(r.specialFrom, true, r));
            Assert.AreEqual(AnimalProductTier.Premium, AnimalProduction.Tier(r.specialFrom - 0.01f, true, r), "a rare trait needs the quality too");
            Assert.IsTrue(AnimalProduction.Rare(new AnimalRecord { genes = new AnimalGenes { rare = 1 } }));
            Assert.AreEqual("Good", AnimalProduction.TierWord(AnimalProductTier.Good));
        }

        [Test]
        public void QualityIsJudgedOverTheWholeCycleNeverTheLastMeal()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData sp = cat.Species("chicken");
            var hens = new List<AnimalRecord>();
            foreach (bool hungry in new[] { false, true })
            {
                var a = new AnimalRecord { id = hungry ? "b" : "a", species = "chicken", breed = "leghorn", sex = AnimalSex.Female, stage = GrowthStage.Adult };
                Typical(a);
                Thriving(a);
                double t = Start;
                for (int hr = 0; hr < 200 && Made(a, Egg) == 0; hr++)
                {
                    // The hungry hen is poorly fed for most of the cycle, and fed well only at its end.
                    a.nutrition = hungry && hr < 20 ? 40f : 90f;
                    Step(a, sp, rules, t, t + 1.0);
                    t += 1.0;
                }
                Assert.AreEqual(1, Made(a, Egg), "it laid");
                Assert.AreEqual(90f, a.nutrition, "both were well fed when they laid");
                hens.Add(a);
            }
            AnimalProductLot fed = AnimalProduction.State(hens[0], Egg, false).waiting[0];
            AnimalProductLot hungryLot = AnimalProduction.State(hens[1], Egg, false).waiting[0];
            Assert.AreEqual(90f, fed.nutrition, 1e-3);
            Assert.Less(hungryLot.nutrition, 70f, $"the cycle's mean remembers the hungry days ({hungryLot.nutrition:0.0})");
            Assert.Less(hungryLot.quality, fed.quality - 0.05f, $"and so does its quality ({hungryLot.quality:0.000} against {fed.quality:0.000})");
            Assert.Greater(hungryLot.madeAt - hungryLot.cycleStart, fed.madeAt - fed.cycleStart, "a hungry hen also lays later");
        }

        // ---------------------------------------------------------------- how fast (FR-06)
        [Test]
        public void NoSingleThingDoneWellGivesTheMostProduction()
        {
            var r = new AnimalProductionRules();
            AnimalSpeciesData sp = Catalog().Species("cow");
            var good = new AnimalWelfareBandEffects { production = 1f };
            AnimalStageEffects well = AnimalStageEffects.Neutral;
            var a = new AnimalRecord { id = "a", species = "cow", stage = GrowthStage.Adult };
            Typical(a);
            Thriving(a);
            float best = AnimalProduction.Rate(a, sp, r, good, in well);
            Assert.AreEqual(1f, best, 1e-4, "a well kept, typical animal makes at the spec's pace");

            a.comfort = 20f;
            Assert.AreEqual(0.7f, AnimalProduction.Rate(a, sp, r, good, in well), 1e-4, "perfectly fed but miserable: less");
            a.comfort = 85f;
            a.nutrition = r.minNutrition;
            Assert.AreEqual(0.5f, AnimalProduction.Rate(a, sp, r, good, in well), 1e-4, "comfortable but barely fed: less");
            a.nutrition = 90f;
            Assert.AreEqual(0.5f, AnimalProduction.Rate(a, sp, r, new AnimalWelfareBandEffects { production = 0.5f }, in well), 1e-4, "a poor life: less");
            AnimalStageEffects ill = AnimalStageEffects.Neutral;
            ill.production = 0.6f;
            Assert.AreEqual(0.6f, AnimalProduction.Rate(a, sp, r, good, in ill), 1e-4, "an illness: less");
            a.genes.productionRate = 1f;
            Assert.AreEqual(1f + r.geneSpread, AnimalProduction.Rate(a, sp, r, good, in well), 1e-4, "a gifted animal: more");
            a.genes.productionRate = 0f;
            Assert.AreEqual(1f - r.geneSpread, AnimalProduction.Rate(a, sp, r, good, in well), 1e-4);
            a.genes.productionRate = 0.5f;
            a.stage = GrowthStage.Senior;
            Assert.AreEqual(r.seniorRate, AnimalProduction.Rate(a, sp, r, good, in well), 1e-4, "an old animal: less");

            // Everything at once multiplies.
            a.stage = GrowthStage.Adult;
            a.comfort = 20f;
            a.nutrition = r.minNutrition;
            Assert.AreEqual(0.7f * 0.5f * 0.6f, AnimalProduction.Rate(a, sp, r, good, in ill), 1e-4);
        }

        [Test]
        public void AWellKeptHenLaysAboutAnEggADayUntilHerNestIsFull()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Stock(cat, coop, "chicken_feed", 500f, 0.8f, Start);
            KeepCoop(ctx, coop, Start, 24 * 7);
            int week = Made(hen, Egg);
            Assert.That(week, Is.InRange(4, 8), $"about an egg a day in a week ({week}; welfare {hen.welfare:0}, nutrition {hen.nutrition:0})");
            AnimalProductState st = AnimalProduction.State(hen, Egg, false);
            Assert.AreEqual(week, st.Waiting, "nobody collected them: they wait");
            Assert.Greater(MeanQuality(st), 0.4f, "a well kept hen's eggs are decent");

            // Left a fortnight, her nest fills and she lays no more, and her owner is told.
            KeepCoop(ctx, coop, Start + 24 * 7, 24 * 7);
            Assert.AreEqual(sp(cat).capacity, st.Waiting, "the nest is full");
            Assert.AreEqual(AnimalProductStatus.Full, st.status);
            Assert.AreEqual(sp(cat).capacity, Made(hen, Egg), "and she laid no more");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.ProductsReady && n.text.Contains("6 eggs")), "her owner hears the nest is full");

            // Collected, she lays again.
            AnimalResult res = Collect(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start + 24 * 14, out _);
            StringAssert.Contains("You collect 6 eggs from", res.message);
            Assert.AreEqual(0, st.Waiting);
            Assert.AreEqual(AnimalProductStatus.Producing, st.status);
            KeepCoop(ctx, coop, Start + 24 * 14, 48);
            Assert.Greater(st.Waiting, 0, "she lays again");

            AnimalProductSpec sp(AnimalCatalog c) => AnimalProduction.Spec(c.Species("chicken"), Egg);
        }

        [Test]
        public void ARoosterAChickAHungryHenAndAnIllHenLayNothing()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData sp = cat.Species("chicken");
            AnimalProductSpec egg = AnimalProduction.Spec(sp, Egg);

            var rooster = new AnimalRecord { id = "r", species = "chicken", sex = AnimalSex.Male, stage = GrowthStage.Adult };
            var chick = new AnimalRecord { id = "c", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Juvenile };
            Assert.IsFalse(AnimalProduction.Makes(rooster, sp, egg), "a rooster lays no eggs");
            Assert.IsFalse(AnimalProduction.Makes(chick, sp, egg), "nor does a chick");
            foreach (AnimalRecord a in new[] { rooster, chick })
            {
                Typical(a);
                Thriving(a);
                for (int hr = 0; hr < 60; hr++) Step(a, sp, rules, Start + hr, Start + hr + 1);
                Assert.AreEqual(0, Made(a, Egg), a.id);
                Assert.IsNull(AnimalProduction.State(a, Egg, false), "nothing begun");
            }

            // Hungry, ill, starving: she stops, and the window says why.
            var hen = new AnimalRecord { id = "h", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(hen);
            Thriving(hen);
            Step(hen, sp, rules, Start, Start + 1);
            AnimalProductState st = AnimalProduction.State(hen, Egg, false);
            Assert.AreEqual(AnimalProductStatus.Producing, st.status);
            float progress = st.progress;
            hen.nutrition = rules.production.minNutrition - 1f;
            Step(hen, sp, rules, Start + 1, Start + 2);
            Assert.AreEqual(AnimalProductStatus.Underfed, st.status);
            Assert.AreEqual(progress, st.progress, 1e-6, "no progress while underfed");
            StringAssert.Contains("poorly fed", AnimalProduction.StatusText(st.status, egg));
            hen.nutrition = 90f;
            hen.health = rules.production.minHealth - 1f;
            Step(hen, sp, rules, Start + 2, Start + 3);
            Assert.AreEqual(AnimalProductStatus.Ill, st.status);
            hen.health = 100f;
            hen.satiety = 0f;
            Step(hen, sp, rules, Start + 3, Start + 4);
            Assert.AreEqual(AnimalProductStatus.Starving, st.status);
            hen.satiety = 90f;
            Step(hen, sp, rules, Start + 4, Start + 5, boarded: true);
            Assert.AreEqual(AnimalProductStatus.Boarded, st.status, "the boarding stable keeps it, it makes nothing there");
            Assert.AreEqual(progress, st.progress, 1e-6);
            Step(hen, sp, rules, Start + 5, Start + 6);
            Assert.Greater(st.progress, progress, "well again, she goes on");

            // Off in the rules.
            var off = AnimalRules.Defaults();
            off.production.enabled = false;
            Step(hen, sp, off, Start + 6, Start + 7);
            Assert.AreEqual(AnimalProductStatus.Off, st.status);
        }

        [Test]
        public void AnAnimalWhoseOwnerIsAwayMakesAtTheSlowerPaceItLives()
        {
            var made = new List<float>();
            foreach (bool present in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat, present);
                var book = new AnimalBook(cat, "test");
                AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
                AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
                Stock(cat, coop, "chicken_feed", 500f, 0.8f, Start);
                KeepCoop(ctx, coop, Start, 72);
                AnimalProductState st = AnimalProduction.State(hen, Egg, false);
                made.Add(st.cycle + st.progress);
            }
            Assert.Less(made[1], made[0] * 0.6f, $"away, it lives and makes slower ({made[1]:0.00} cycles against {made[0]:0.00})");
            Assert.Greater(made[1], 0f, "but it still makes");
        }

        [Test]
        public void TheYieldOfMilkFollowsHowTheCowLivedButAnEggIsAnEgg()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData cow = cat.Species("cow"), chicken = cat.Species("chicken");
            var jugs = new List<int>();
            foreach (float life in new[] { 1f, 0.5f })
            {
                var a = new AnimalRecord { id = "c" + life, species = "cow", sex = AnimalSex.Female, stage = GrowthStage.Adult };
                Typical(a);
                Thriving(a);
                for (int hr = 0; hr < 40 && Made(a, Milk) == 0; hr++) Step(a, cow, rules, Start + hr, Start + hr + 1, bandProduction: life);
                jugs.Add(AnimalProduction.State(a, Milk, false).Waiting);
            }
            Assert.AreEqual(2, jugs[0], "a well kept cow gives her two jugs a milking");
            Assert.AreEqual(1, jugs[1], "a poorly kept one less");

            var hen = new AnimalRecord { id = "h", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(hen);
            Thriving(hen);
            for (int hr = 0; hr < 100 && Made(hen, Egg) == 0; hr++) Step(hen, chicken, rules, Start + hr, Start + hr + 1, bandProduction: 0.5f);
            Assert.AreEqual(1, AnimalProduction.State(hen, Egg, false).Waiting, "a poor hen lays later, not smaller eggs");
        }

        [Test]
        public void WhatWaitsPastItsFreshnessLosesQuality()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData sp = cat.Species("chicken");
            AnimalProductSpec egg = AnimalProduction.Spec(sp, Egg);
            var hen = new AnimalRecord { id = "h", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(hen);
            Thriving(hen);
            Assert.AreEqual(1, AnimalProduction.Ripen(hen, sp, rules, Start));
            AnimalProductLot lot = AnimalProduction.State(hen, Egg, false).waiting[0];
            float fresh = lot.quality;
            // Within its freshness it keeps.
            for (int hr = 0; hr < (int)egg.freshHours; hr++) Step(hen, sp, rules, Start + hr, Start + hr + 1);
            Assert.AreEqual(fresh, lot.quality, 1e-5, "fresh for two days");
            // Two days more: it loses Stale Loss Per Day a day.
            for (int hr = (int)egg.freshHours; hr < (int)egg.freshHours + 48; hr++) Step(hen, sp, rules, Start + hr, Start + hr + 1);
            Assert.AreEqual(fresh - 2f * egg.staleLossPerDay, lot.quality, 0.01f, "then it goes stale");

            // Milk and wool keep (no freshness: they wait in the udder or on the sheep).
            Assert.AreEqual(0f, AnimalProduction.Spec(cat.Species("sheep"), Wool).freshHours);
        }

        // ---------------------------------------------------------------- collecting (FR-14)
        [Test]
        public void CollectedCopiesCarryTheirQualityAndTheirLotWhoseOriginTheLedgerKnows()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            hen.name = "Henrietta";
            Thriving(hen);
            AnimalSpeciesData sp = cat.Species("chicken");
            AnimalProduction.Ripen(hen, sp, cat.Rules, Start);
            AnimalProduction.Ripen(hen, sp, cat.Rules, Start + 1.0);
            AnimalProductState st = AnimalProduction.State(hen, Egg, false);
            Assert.AreEqual(2, st.Waiting);
            float q0 = st.waiting[0].quality, q1 = st.waiting[1].quality;
            int cycle0 = st.waiting[0].cycle;
            float fedThen = st.waiting[0].nutrition;
            Assert.AreEqual(90f, fedThen, 1e-3, "as it lived (Ripen judges a cycle not yet lived from how it is)");

            AnimalResult res = Collect(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start + 2.0, out AnimalPlan plan);
            StringAssert.Contains("You collect 2 eggs from Henrietta (", res.message);
            Assert.AreEqual(1, plan.give.Count, "one kind of item");
            AnimalItemChange g = plan.give[0];
            Assert.AreEqual("animal:product/egg", g.item);
            Assert.AreEqual(2, g.count);
            Assert.AreEqual(2, g.codes.Count, "a code per copy");
            Assert.IsTrue(AnimalItemCode.TryProduct(g.codes[0], out float cq0, out long s0));
            Assert.IsTrue(AnimalItemCode.TryProduct(g.codes[1], out float cq1, out long s1));
            Assert.AreEqual(q0, cq0, 0.006f, "the copy keeps its lot's quality");
            Assert.AreEqual(q1, cq1, 0.006f);
            Assert.AreNotEqual(s0, s1, "each lot has its own serial");
            Assert.AreEqual(0, st.Waiting, "taken");

            // Where it came from.
            AnimalProductRecord origin = book.Ledger.Find(s0, Egg);
            Assert.IsNotNull(origin);
            Assert.AreEqual(hen.id, origin.animal);
            Assert.AreEqual("Henrietta", origin.animalName);
            Assert.AreEqual("chicken", origin.species);
            Assert.AreEqual("leghorn", origin.breed);
            Assert.AreEqual("p1", origin.owner);
            Assert.AreEqual("p1", origin.by);
            Assert.AreEqual(cycle0, origin.cycle);
            Assert.AreEqual(Start + 2.0, origin.collectedAt, 1e-9);
            Assert.AreEqual(AnimalItemCode.Hundredth(q0), origin.quality, 1e-6, "kept to the hundredth, as the copies keep it");
            Assert.AreEqual(cq0, origin.quality, 1e-6);
            Assert.AreEqual(AnimalProduction.Tier(cq0, false, cat.Rules.production), origin.tier, "so its tier is the copy's wherever it is read");
            Assert.AreEqual(fedThen, origin.nutrition, 1e-3, "how it lived through the cycle");
            Assert.IsNull(book.Ledger.Find(s0, Milk), "a serial of another product is not its origin");
            Assert.IsTrue(hen.productLog.Exists(e => e.kind == AnimalProductEntryKind.Collected && e.units == 1 && e.by == "p1"));

            // Nothing left: refused, with when there will be.
            string why = Refusal(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start + 2.0);
            StringAssert.Contains("no egg ready yet", why);
            StringAssert.Contains("more", why);
        }

        [Test]
        public void ThePerCopyCodeKeepsQualityAndSerialWithinItsBounds()
        {
            int code = AnimalItemCode.Product(0.734f, 12345);
            Assert.IsTrue(AnimalItemCode.TryProduct(code, out float q, out long s));
            Assert.AreEqual(0.73f, q, 1e-6);
            Assert.AreEqual(12345L, s);
            int top = AnimalItemCode.Product(1f, AnimalItemCode.MaxProductSerial, true);
            Assert.Less(top, 1 << 30, "below 2^30, as every per-copy code");
            Assert.IsTrue(AnimalItemCode.TryProduct(top, out q, out s, out bool rare));
            Assert.AreEqual(1f, q);
            Assert.AreEqual(AnimalItemCode.MaxProductSerial, s);
            Assert.IsTrue(rare, "the rare trait travels with the copy");
            Assert.IsTrue(AnimalItemCode.TryProduct(AnimalItemCode.Product(0.95f, 77, false), out _, out s, out rare));
            Assert.IsFalse(rare);
            Assert.AreEqual(77L, s);
            Assert.AreEqual(AnimalItemCode.Product(0.734f, 12345), AnimalItemCode.Product(AnimalItemCode.Hundredth(0.734f), 12345));
            Assert.IsTrue(AnimalItemCode.TryProduct(AnimalItemCode.Product(0f, 0), out q, out s));
            Assert.AreEqual(0f, q);
            Assert.AreEqual(0L, s, "a copy without a lot");
            Assert.AreEqual(0L, (AnimalItemCode.TryProduct(AnimalItemCode.Product(0.5f, AnimalItemCode.MaxProductSerial + 5), out _, out s) ? s : -1L), "a serial out of range is none");
            Assert.IsFalse(AnimalItemCode.TryProduct(0, out q, out _), "no code");
            Assert.AreEqual(AnimalItemCode.UnknownQuality, q, "a copy without a code is of middling quality");
            Assert.IsFalse(AnimalItemCode.TryProduct(1, out _, out _));
            Assert.IsFalse(AnimalItemCode.TryProduct(2 + 101, out _, out _), "not a product's code");

            // The ledger's serials go round past the largest.
            var ledger = new AnimalProductLedger { serial = AnimalItemCode.MaxProductSerial };
            Assert.AreEqual(1L, ledger.NextSerial());
            Assert.AreEqual(2L, ledger.NextSerial());
        }

        [Test]
        public void MilkingNeedsAPailAndShearingShearsWhichWearAndSkittishAnimalsMind()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 200f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, seed: 1);
            AnimalRecord ewe = Release(book, ctx, field, "p1", "sheep", "merino", Start, seed: 2);
            cow.name = "Daisy";
            Thriving(cow);
            Thriving(ewe);
            AnimalProduction.Ripen(cow, cat.Species("cow"), cat.Rules, Start);
            AnimalProduction.Ripen(ewe, cat.Species("sheep"), cat.Rules, Start);

            AnimalActor bare = Actor("p1", AnimalRights.All);
            StringAssert.Contains("milking pail", Refusal(book, ctx, CollectFrom(cow), bare, Start));
            StringAssert.Contains("to milk", Refusal(book, ctx, CollectFrom(cow), bare, Start));
            StringAssert.Contains("shears", Refusal(book, ctx, CollectFrom(ewe), bare, Start));

            AnimalActor milker = WithTools(Actor("p1", AnimalRights.All), cat, 1f, "milking_pail");
            cow.genes.temperament = 0f;
            cow.stress = 10f;
            AnimalResult milked = Collect(book, ctx, CollectFrom(cow), milker, Start, out AnimalPlan plan);
            StringAssert.Contains("You milk Daisy: 2 jugs of milk (", milked.message);
            Assert.AreEqual(1, plan.wearUses);
            Assert.IsFalse(string.IsNullOrEmpty(plan.wearKey), "the pail wears");
            Assert.AreEqual("animal:product/milk", plan.give[0].item);
            Assert.AreEqual(10f + AnimalProduction.Spec(cat.Species("cow"), Milk).handlingStress, cow.stress, 1e-4, "a skittish cow minds being milked");

            AnimalActor shearer = WithTools(Actor("p1", AnimalRights.All), cat, 1f, "shears");
            AnimalResult shorn = Collect(book, ctx, CollectFrom(ewe, Wool), shearer, Start, out plan);
            StringAssert.Contains("You shear", shorn.message);
            StringAssert.Contains("bundles of wool", shorn.message);
            Assert.AreEqual("animal:product/wool", plan.give[0].item);
            StringAssert.Contains("gives no such thing", Refusal(book, ctx, CollectFrom(ewe, Milk), shearer, Start));
        }

        [Test]
        public void OnlyItsOwnerOrOneWhoMayCareForOthersAnimalsCollects()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Thriving(hen);
            AnimalProduction.Ripen(hen, cat.Species("chicken"), cat.Rules, Start);
            StringAssert.Contains("Not your animal", Refusal(book, ctx, CollectFrom(hen), Actor("p2", AnimalRights.Look | AnimalRights.Tend), Start));
            StringAssert.Contains("None of these animals are yours",
                Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "coop" }, Actor("p2", AnimalRights.Look | AnimalRights.Tend), Start));

            ctx.notices.Clear();
            AnimalResult res = Collect(book, ctx, CollectFrom(hen), Actor("hand", AnimalRights.All), Start, out AnimalPlan plan);
            Assert.AreEqual(1, plan.give[0].count, "a farm hand who may care for others' animals collects");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.Collected && n.text.Contains("hand collected 1 egg")), "and its owner is told");
            Assert.AreEqual("p1", book.Ledger.Find(AnimalItemCode.TryProduct(plan.give[0].codes[0], out _, out long s) ? s : 0, Egg).owner, "the lot is still of its owner's animal");
        }

        [Test]
        public void CollectingFromAllHereTakesFromEveryAnimalWithSomethingWaiting()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd barn = Pen(book, "barn", AnimalHabitatKind.Barn | AnimalHabitatKind.Coop, 200f, Start);
            AnimalRecord a = Release(book, ctx, barn, "p1", "chicken", "leghorn", Start, seed: 11);
            AnimalRecord b = Release(book, ctx, barn, "p1", "chicken", "leghorn", Start, seed: 12);
            AnimalRecord c = Release(book, ctx, barn, "p1", "chicken", "leghorn", Start, seed: 13);
            AnimalRecord theirs = Release(book, ctx, barn, "p2", "chicken", "leghorn", Start, seed: 14);
            AnimalRecord cow = Release(book, ctx, barn, "p1", "cow", "holstein", Start, seed: 15);
            foreach (AnimalRecord x in barn.animals)
            {
                Thriving(x);
                AnimalProduction.Ripen(x, cat.Species(x.species), cat.Rules, Start);
            }
            AnimalProduction.State(c, Egg, false).waiting.Clear(); // one has laid nothing

            AnimalPenView view = AnimalObservation.PenView(book, barn, "p1", AnimalRights.Look | AnimalRights.Tend, Start);
            Assert.IsTrue(view.collectReady);
            Assert.AreEqual("2 eggs, 2 jugs of milk", view.productsWaiting, "what of the viewer's own waits");

            // Without a pail: the eggs only.
            AnimalActor keeper = Actor("p1", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep);
            AnimalResult res = Collect(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "barn" }, keeper, Start, out AnimalPlan plan);
            StringAssert.Contains("You collect 2 from 2 animals", res.message);
            Assert.AreEqual(1, plan.give.Count);
            Assert.AreEqual(2, plan.give[0].count);
            Assert.AreEqual(1, AnimalProduction.State(theirs, Egg, false).Waiting, "another's hen is not touched");
            Assert.AreEqual(2, AnimalProduction.State(cow, Milk, false).Waiting, "the cow waits for a pail");

            // With one: the milk too; and only the product named, when one is.
            AnimalProduction.Ripen(a, cat.Species("chicken"), cat.Rules, Start + 1.0);
            AnimalActor milker = WithTools(keeper, cat, 1f, "milking_pail");
            res = Collect(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "barn", text = Milk }, milker, Start + 1.0, out plan);
            Assert.AreEqual(1, plan.give.Count);
            Assert.AreEqual("animal:product/milk", plan.give[0].item);
            Assert.AreEqual(1, AnimalProduction.State(a, Egg, false).Waiting, "the egg was not asked for");
            StringAssert.Contains("There is nothing to collect", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "barn", text = Milk }, milker, Start + 1.0));
            view = AnimalObservation.PenView(book, barn, "p1", AnimalRights.Look | AnimalRights.Tend, Start + 1.0);
            Assert.AreEqual("1 egg", view.productsWaiting);
        }

        [Test]
        public void ABoardedAnimalsProductsWaitForItsOwnerFromAnyPen()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalHerd other = Pen(book, "other", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Thriving(hen);
            AnimalProduction.Ripen(hen, cat.Species("chicken"), cat.Rules, Start);
            AnimalCommands.Board(book, hen, Start, ctx, "the lease ended");
            Assert.AreEqual(AnimalStatus.Boarded, hen.status);
            book.BringUpToDate(hen, Start + 72.0, ctx);
            AnimalProductState st = AnimalProduction.State(hen, Egg, false);
            Assert.AreEqual(1, st.Waiting, "she lays nothing at the boarding stable");
            Assert.AreEqual(AnimalProductStatus.Boarded, st.status);

            var r = new AnimalRequest { kind = AnimalActionKind.Collect, pen = "other", animal = hen.id };
            StringAssert.Contains("not here", Refusal(book, ctx, r, Actor("p2", AnimalRights.All), Start + 72.0));
            Collect(book, ctx, r, Actor("p1", AnimalRights.All), Start + 72.0, out AnimalPlan plan);
            Assert.AreEqual(1, plan.give[0].count, "her owner takes what she laid before she went, from any pen");
        }

        // ---------------------------------------------------------------- the review's cases
        [Test]
        public void AGiftedSheepInAnExcellentLifeStillGrowsItsFleece()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData sp = cat.Species("sheep");
            var ewe = new AnimalRecord { id = "e", species = "sheep", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(ewe);
            Thriving(ewe);
            ewe.genes.productionRate = 0.8f; // with an Excellent life its yield would be 4 bundles: more than a fleece holds
            var filled = new List<AnimalProductSpec>();
            for (int hr = 0; hr < 400 && Made(ewe, Wool) == 0; hr++) Step(ewe, sp, cat.Rules, Start + hr, Start + hr + 1, bandProduction: 1.15f, filled: filled);
            AnimalProductState st = AnimalProduction.State(ewe, Wool, false);
            Assert.AreEqual(3, st.Waiting, "a full fleece: never more than its store holds");
            Assert.AreEqual(AnimalProductStatus.Full, st.status);
            Assert.AreEqual(1, filled.Count, "its owner is told once");
        }

        [Test]
        public void AFullUdderPausesHerSoMilkingHerLateStillGivesAFullMilking()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData sp = cat.Species("cow");
            var cow = new AnimalRecord { id = "c", species = "cow", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(cow);
            Thriving(cow);
            double t = Start;
            for (int i = 0; i < 100 && AnimalProduction.State(cow, Milk, false)?.status != AnimalProductStatus.Full; i++, t += 1.0) Step(cow, sp, rules, t, t + 1.0);
            AnimalProductState st = AnimalProduction.State(cow, Milk, false);
            Assert.AreEqual(6, st.Waiting, "her udder is full");
            int made = Made(cow, Milk);
            // A day full: nothing goes on, nothing of the day is counted.
            for (int i = 0; i < 24; i++, t += 1.0) Step(cow, sp, rules, t, t + 1.0);
            Assert.AreEqual(made, Made(cow, Milk));
            Assert.AreEqual(0f, st.progress, 1e-6, "the cycle waits");
            Assert.AreEqual(0f, st.hours, 1e-6, "and takes in none of the hours she waited");
            var codes = new List<int>();
            Assert.AreEqual(6, AnimalProduction.Collect(cow, sp, AnimalProduction.Spec(sp, Milk), new AnimalProductLedger(), rules.production, "p1", t, codes, out _, out _));
            Assert.AreEqual(t, st.cycleStart, 1e-9, "her next cycle starts when she is milked");
            for (int i = 0; i < 40 && st.Waiting == 0; i++, t += 1.0) Step(cow, sp, rules, t, t + 1.0);
            Assert.AreEqual(2, st.Waiting, "a well kept cow milked late gives her full two jugs");
        }

        [Test]
        public void AFleeceShornLateIsNotFollowedByAnotherAtOnce()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData sp = cat.Species("sheep");
            var ewe = new AnimalRecord { id = "e", species = "sheep", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(ewe);
            Thriving(ewe);
            AnimalProduction.Ripen(ewe, sp, cat.Rules, Start);
            double t = Start;
            for (int i = 0; i < 336; i++, t += 1.0) Step(ewe, sp, cat.Rules, t, t + 1.0);
            AnimalProductState st = AnimalProduction.State(ewe, Wool, false);
            Assert.AreEqual(3, st.Waiting);
            Assert.AreEqual(1, st.cycle, "no second fleece grew under the first");
            AnimalProduction.Collect(ewe, sp, AnimalProduction.Spec(sp, Wool), new AnimalProductLedger(), cat.Rules.production, "p1", t, new List<int>(), out _, out _);
            Assert.Less(AnimalObservation.FleeceOf(ewe, sp), 0.01f, "shorn: its body slims");
            Step(ewe, sp, cat.Rules, t, t + 1.0);
            Assert.AreEqual(0, st.Waiting, "and it is not shorn again an hour later");
            Assert.Less(AnimalObservation.FleeceOf(ewe, sp), 0.01f);
        }

        [Test]
        public void ShortCyclesMakeSoundLotsHoweverLongTheSteps()
        {
            var lots = new List<List<AnimalProductLot>>();
            foreach (double step in new[] { 1.0, 0.25 })
            {
                AnimalCatalog cat = Catalog();
                AnimalSpeciesData sp = cat.Species("chicken");
                AnimalProductSpec quick = AnimalProduction.Spec(sp, Egg);
                quick.cycleHours = 0.5f;
                quick.capacity = 50;
                // A hen of no laying breed (the first breed, a Leghorn, lays more since Phase 5).
                sp.BreedOrDefault(null).productYield = 1f;
                var hen = new AnimalRecord { id = "h", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Adult };
                Typical(hen);
                Thriving(hen);
                for (double t = Start; t < Start + 6.0 - 1e-9; t += step) Step(hen, sp, cat.Rules, t, t + step);
                lots.Add(AnimalProduction.State(hen, Egg, false).waiting);
            }
            Assert.AreEqual(12, lots[0].Count, "two lots an hour");
            Assert.AreEqual(lots[1].Count, lots[0].Count, "long steps make what short ones do");
            for (int i = 0; i < lots[0].Count; i++)
            {
                Assert.AreEqual(90f, lots[0][i].nutrition, 1e-3, $"lot {i}: judged by how the hen lived, not left empty");
                Assert.AreEqual(lots[1][i].quality, lots[0][i].quality, 1e-4, $"lot {i}");
                Assert.AreEqual(lots[1][i].madeAt, lots[0][i].madeAt, 1e-4, $"lot {i}: made when its cycle ended, within the step");
            }
            Assert.AreEqual(Start + 0.5, lots[0][0].madeAt, 1e-4);
        }

        [Test]
        public void FreshnessIsLostOnlyForTheHoursPastIt()
        {
            var quality = new List<float>();
            foreach (bool hourly in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                AnimalSpeciesData sp = cat.Species("chicken");
                AnimalProductSpec egg = AnimalProduction.Spec(sp, Egg);
                egg.capacity = 1; // the nest holds only the one egg, which waits
                var hen = new AnimalRecord { id = "h", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Adult };
                Typical(hen);
                Thriving(hen);
                AnimalProduction.Ripen(hen, sp, cat.Rules, Start);
                if (hourly)
                    for (int hr = 0; hr < 50; hr++) Step(hen, sp, cat.Rules, Start + hr, Start + hr + 1);
                else
                {
                    Step(hen, sp, cat.Rules, Start, Start + 47.5);
                    Step(hen, sp, cat.Rules, Start + 47.5, Start + 50.0);
                }
                quality.Add(AnimalProduction.State(hen, Egg, false).waiting[0].quality);
            }
            Assert.AreEqual(quality[0], quality[1], 1e-5, "two hours past its freshness, however the hours were stepped");
        }

        [Test]
        public void ItsOwnerIsToldOnceWhenTheNestFillsNotAgainWhenSheComesBack()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData sp = cat.Species("chicken");
            var hen = new AnimalRecord { id = "h", species = "chicken", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(hen);
            Thriving(hen);
            var filled = new List<AnimalProductSpec>();
            double t = Start;
            for (int i = 0; i < 300 && AnimalProduction.State(hen, Egg, false)?.status != AnimalProductStatus.Full; i++, t += 1.0) Step(hen, sp, cat.Rules, t, t + 1.0, filled: filled);
            Assert.AreEqual(1, filled.Count);
            for (int i = 0; i < 3; i++, t += 1.0) Step(hen, sp, cat.Rules, t, t + 1.0, boarded: true, filled: filled);
            for (int i = 0; i < 3; i++, t += 1.0) Step(hen, sp, cat.Rules, t, t + 1.0, filled: filled);
            Assert.AreEqual(AnimalProductStatus.Full, AnimalProduction.State(hen, Egg, false).status);
            Assert.AreEqual(1, filled.Count, "a full nest is told of once, not again each time she is back");
        }

        [Test]
        public void CollectingAllWithAPailAndShearsTakesBothAndWearsEach()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
            AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start, seed: 31);
            AnimalRecord ewe = Release(book, ctx, field, "p1", "sheep", "merino", Start, seed: 32);
            AnimalRecord cow2 = Release(book, ctx, field, "p1", "cow", "jersey", Start, seed: 33);
            foreach (AnimalRecord x in field.animals)
            {
                Thriving(x);
                AnimalProduction.Ripen(x, cat.Species(x.species), cat.Rules, Start);
            }
            book.Urgent = false;
            AnimalActor both = WithTools(Actor("p1", AnimalRights.All), cat, 1f, "milking_pail", "shears");
            AnimalResult res = Collect(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "field" }, both, Start, out AnimalPlan plan);
            StringAssert.Contains("You collect 7 from 3 animals", res.message);
            Assert.AreEqual(2, plan.give.Count, "milk and wool");
            Assert.AreEqual(7, plan.plannedCount, "it gives what it planned");
            var uses = new Dictionary<string, int> { [plan.wearKey] = plan.wearUses };
            foreach (AnimalItemChange w in plan.moreWear) uses[w.item] = w.count;
            Assert.AreEqual(2, uses.Count, "both tools are worn");
            Assert.AreEqual(2, uses[AnimalIds.Tool("milking_pail")], "the pail once for each cow");
            Assert.AreEqual(1, uses[AnimalIds.Tool("shears")], "the shears once for the sheep");
            Assert.IsTrue(book.Urgent, "what was taken is saved soon");
            Assert.AreEqual(0, AnimalProduction.State(ewe, Wool, false).Waiting);
            Assert.AreEqual(0, AnimalProduction.State(cow2, Milk, false).Waiting);
        }

        [Test]
        public void ARareAnimalsSpecialCopiesAreSpecialWhereverTheyAreRead()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "silkie", Start);
            Thriving(hen);
            hen.genes.rare = 1;
            hen.genes.productionQuality = 1f;
            hen.lastOwnerCareAt = Start;
            AnimalProduction.Ripen(hen, cat.Species("chicken"), cat.Rules, Start);
            Collect(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start, out AnimalPlan plan);
            Assert.IsTrue(AnimalItemCode.TryProduct(plan.give[0].codes[0], out float q, out long serial, out bool rare));
            Assert.IsTrue(rare, "the copy knows its hen has a rare trait");
            Assert.GreaterOrEqual(q, cat.Rules.production.specialFrom, $"quality {q:0.00}");
            Assert.AreEqual(AnimalProductTier.Special, book.Ledger.Find(serial, Egg).tier);
            Assert.AreEqual(AnimalProductTier.Special, AnimalProduction.Tier(q, rare, cat.Rules.production), "a client, or a forgotten lot, reads the same tier from the copy alone");
        }

        [Test]
        public void WithProductionOffTheRefusalSaysSo()
        {
            AnimalRules rules = AnimalRules.Defaults();
            rules.production.enabled = false;
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Stock(cat, coop, "chicken_feed", 500f, 0.8f, Start);
            KeepCoop(ctx, coop, Start, 30);
            StringAssert.Contains("production is off", Refusal(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start + 30.0));
        }

        [Test]
        public void WhenOthersMayNotCollectOnlyItsOwnerDoes()
        {
            AnimalRules rules = AnimalRules.Defaults();
            rules.production.othersCollect = false;
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Thriving(hen);
            AnimalProduction.Ripen(hen, cat.Species("chicken"), cat.Rules, Start);
            StringAssert.Contains("Not your animal", Refusal(book, ctx, CollectFrom(hen), Actor("hand", AnimalRights.All), Start));
            StringAssert.Contains("None of these animals are yours",
                Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Collect, pen = "coop" }, Actor("hand", AnimalRights.All), Start));
            Assert.IsEmpty(AnimalObservation.View(hen, cat, Start, "hand", AnimalRights.All, null, false).products, "a farm hand is not shown what waits");
            Assert.IsFalse(AnimalObservation.PenView(book, coop, "hand", AnimalRights.All, Start).collectReady);
            Assert.IsTrue(AnimalObservation.PenView(book, coop, "p1", AnimalRights.All, Start).collectReady);
            Collect(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start, out _);
        }

        [Test]
        public void AWornOutToolIsRoughOnTheAnimal()
        {
            var stress = new List<float>();
            foreach (float edge in new[] { 1f, 0f })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat);
                var book = new AnimalBook(cat, "test");
                AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 200f, Start);
                AnimalRecord cow = Release(book, ctx, field, "p1", "cow", "holstein", Start);
                Thriving(cow);
                cow.genes.temperament = 0f;
                AnimalProduction.Ripen(cow, cat.Species("cow"), cat.Rules, Start);
                AnimalResult res = Collect(book, ctx, CollectFrom(cow), WithTools(Actor("p1", AnimalRights.All), cat, edge, "milking_pail"), Start, out _);
                stress.Add(cow.stress - 5f);
                Assert.AreEqual(edge <= 0f, res.lines.Exists(l => l.Contains("worn-out tool is rough")), string.Join(" | ", res.lines));
            }
            float handling = AnimalProduction.Spec(Catalog().Species("cow"), Milk).handlingStress;
            Assert.AreEqual(handling, stress[0], 1e-4);
            Assert.AreEqual(2f * handling, stress[1], 1e-4, "twice the stress with a worn-out pail");
        }

        // ---------------------------------------------------------------- determinism and saves
        [Test]
        public void ACatchUpMakesWhatAWatchedStretchMakes()
        {
            var states = new List<AnimalRecord>();
            foreach (bool watched in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat);
                var book = new AnimalBook(cat, "test");
                AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 300f, Start);
                Release(book, ctx, field, "p1", "cow", "holstein", Start, seed: 3);
                Release(book, ctx, field, "p1", "sheep", "merino", Start, seed: 4);
                Release(book, ctx, field, "p1", "goat", "saanen", Start, seed: 5);
                Stock(cat, field, "hay", 3000f, 0.8f, Start);
                field.pen.water = 2000f;
                if (watched)
                    for (int hr = 1; hr <= 96; hr++) AnimalSim.AdvanceHerd(field, Start + hr, ctx);
                else AnimalSim.AdvanceHerd(field, Start + 96.0, ctx);
                states.AddRange(field.animals);
            }
            int n = states.Count / 2;
            for (int i = 0; i < n; i++)
            {
                AnimalRecord w = states[i], c = states[i + n];
                Assert.AreEqual(w.products.Count, c.products.Count, w.species);
                for (int k = 0; k < w.products.Count; k++)
                {
                    AnimalProductState ws = w.products[k], cs = c.products[k];
                    Assert.AreEqual(ws.cycle, cs.cycle, $"{w.species} {ws.product}: lots made");
                    Assert.AreEqual(ws.progress, cs.progress, 1e-4, $"{w.species} {ws.product}: progress");
                    Assert.AreEqual(ws.Waiting, cs.Waiting, $"{w.species} {ws.product}: waiting");
                    Assert.AreEqual(ws.status, cs.status, $"{w.species} {ws.product}: status");
                    for (int l = 0; l < ws.waiting.Count; l++) Assert.AreEqual(ws.waiting[l].quality, cs.waiting[l].quality, 1e-4, $"{w.species} {ws.product}: quality of lot {l}");
                }
            }
            Assert.Greater(states[0].products[0].cycle, 0, "the cow gave milk in four days");
        }

        [Test]
        public void TheLedgerIsSavedAndASerialIsNeverGivenTwice()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Thriving(hen);
            AnimalSpeciesData sp = cat.Species("chicken");
            AnimalProduction.Ripen(hen, sp, cat.Rules, Start);
            Collect(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start, out AnimalPlan first);
            AnimalItemCode.TryProduct(first.give[0].codes[0], out _, out long s1);
            AnimalProduction.Ripen(hen, sp, cat.Rules, Start + 1.0);
            float waitingQuality = AnimalProduction.State(hen, Egg, false).waiting[0].quality;

            AnimalWorldSave save = book.Export(Start + 1.0, 0);
            Assert.AreEqual(AnimalWorldSave.CurrentVersion, save.version, "saved as the current version (3 brought the ledger)");
            string json = JsonUtility.ToJson(save);
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            AnimalRecord back = loaded.Find(hen.id);
            AnimalProductState st = AnimalProduction.State(back, Egg, false);
            Assert.AreEqual(1, st.Waiting, "what waits is saved");
            Assert.AreEqual(waitingQuality, st.waiting[0].quality, 1e-6);
            Assert.AreEqual(2, st.cycle);
            Assert.IsTrue(back.productLog.Count >= 3, "and its production history");
            AnimalProductRecord origin = loaded.Ledger.Find(s1, Egg);
            Assert.IsNotNull(origin, "the copies collected before still know where they came from");
            Assert.AreEqual(hen.id, origin.animal);

            Collect(loaded, ctx, CollectFrom(back), Actor("p1", AnimalRights.All), Start + 2.0, out AnimalPlan second);
            AnimalItemCode.TryProduct(second.give[0].codes[0], out _, out long s2);
            Assert.Greater(s2, s1, "a loaded world never gives a serial again");
            Assert.AreEqual(hen.id, loaded.Ledger.Find(s1, Egg).animal, "the first lot is still the first hen's");
        }

        [Test]
        public void AVersion2SaveLoadsWithNothingMadeYet()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            AnimalWorldSave save = book.Export(Start, 0);
            // As a Phase 2 game saved it: no production, no ledger.
            save.version = 2;
            save.ledger = null;
            foreach (AnimalRecord a in save.animals)
            {
                a.products = null;
                a.productLog = null;
            }
            string json = JsonUtility.ToJson(save).Replace("\"products\":[],", "").Replace("\"productLog\":[],", "");
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            AnimalRecord back = loaded.Find(hen.id);
            Assert.IsNotNull(back.products);
            Assert.IsNotNull(back.productLog);
            Assert.IsNotNull(loaded.Ledger);
            Assert.AreEqual(1L, loaded.Ledger.NextSerial(), "the ledger starts afresh");
            AnimalHerd h = loaded.Herd("coop");
            Stock(cat, h, "chicken_feed", 500f, 0.8f, Start);
            KeepCoop(ctx, h, Start, 48);
            Assert.Greater(AnimalProduction.State(back, Egg, false).cycle + AnimalProduction.State(back, Egg, false).progress, 0f, "and she starts laying");
        }

        [Test]
        public void TheLedgerLetsGoOfOldLotsButTheirCopiesKeepTheirQuality()
        {
            var rules = new AnimalProductionRules { keepLedgerDays = 10f, maxLedger = 100 };
            var ledger = new AnimalProductLedger();
            for (int i = 0; i < 150; i++)
            {
                long s = ledger.NextSerial();
                ledger.Add(new AnimalProductRecord { serial = s, product = Egg, collectedAt = i < 20 ? 0.0 : 24.0 * 20.0 });
            }
            ledger.Prune(24.0 * 20.0, rules);
            Assert.AreEqual(100, ledger.entries.Count, "the oldest go first, past Max Ledger");
            Assert.IsNull(ledger.Find(1, Egg), "collected 20 days ago: forgotten");
            Assert.IsNotNull(ledger.Find(150, Egg));
            Assert.IsTrue(AnimalItemCode.TryProduct(AnimalItemCode.Product(0.8f, 1), out float q, out _));
            Assert.AreEqual(0.8f, q, 1e-6, "a copy of a forgotten lot keeps its quality");
        }

        // ---------------------------------------------------------------- what is shown
        [Test]
        public void ItsKeeperSeesWhatItGivesAndOthersOnlyHowItLooks()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture | AnimalHabitatKind.Coop, 200f, Start);
            AnimalRecord hen = Release(book, ctx, field, "p1", "chicken", "leghorn", Start, seed: 21);
            AnimalRecord ewe = Release(book, ctx, field, "p1", "sheep", "merino", Start, seed: 22);
            Thriving(hen);
            Thriving(ewe);
            AnimalProduction.Ripen(hen, cat.Species("chicken"), cat.Rules, Start);
            AnimalProduction.State(ewe, Wool, true).progress = 0.5f;

            AnimalView mine = AnimalObservation.View(hen, cat, Start, "p1", AnimalRights.All, null, false);
            Assert.AreEqual(1, mine.products.Count);
            AnimalProductView pv = mine.products[0];
            Assert.AreEqual(Egg, pv.id);
            Assert.AreEqual("Collect", pv.verb);
            Assert.IsTrue(pv.ready);
            Assert.AreEqual(1, pv.waiting);
            Assert.AreEqual("1 egg", pv.waitingText);
            Assert.AreEqual(AnimalProduction.TierWord(AnimalProduction.Tier(AnimalItemCode.Hundredth(AnimalProduction.State(hen, Egg, false).waiting[0].quality), false, cat.Rules.production)), pv.tier);
            Assert.That(pv.progress, Is.InRange(0, 4), "how far the next lot is: coarse");
            Assert.IsEmpty(pv.tools, "by hand");

            AnimalView fleece = AnimalObservation.View(ewe, cat, Start, "p1", AnimalRights.All, null, false);
            Assert.AreEqual("Shear", fleece.products[0].verb);
            Assert.IsFalse(fleece.products[0].ready);
            StringAssert.Contains("shears", fleece.products[0].toolText);
            Assert.AreEqual(0.5f, fleece.fleece, 1e-5, "its fleece is half grown");

            // A stranger sees how it looks, not what it gives.
            AnimalView seen = AnimalObservation.View(ewe, cat, Start, "p9", AnimalRights.Look, null, false);
            Assert.IsEmpty(seen.products);
            Assert.AreEqual(0.5f, seen.fleece, 1e-5, "its body shows its fleece to all");
            Assert.AreEqual(-1f, AnimalObservation.View(hen, cat, Start, "p9", AnimalRights.Look, null, false).fleece, "a hen grows none");
            AnimalPenView stranger = AnimalObservation.PenView(book, field, "p9", AnimalRights.Look, Start);
            Assert.IsFalse(stranger.collectReady);
            Assert.AreEqual("", stranger.productsWaiting);
            AnimalPenView hand = AnimalObservation.PenView(book, field, "p9", AnimalRights.All, Start);
            Assert.IsTrue(hand.collectReady, "one who may care for others' animals may collect");

            // The view never carries a quality number, a serial or the ledger.
            string json = JsonUtility.ToJson(mine);
            StringAssert.DoesNotContain("\"quality\"", json, "no quality field");
            StringAssert.DoesNotContain("\"serial\"", json, "no serial field");
        }

        [Test]
        public void ItsHistoryTellsWhatItMadeAndWhatWasCollected()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Thriving(hen);
            AnimalProduction.Ripen(hen, cat.Species("chicken"), cat.Rules, Start);
            Collect(book, ctx, CollectFrom(hen), Actor("p1", AnimalRights.All), Start + 1.0, out _);
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.History, pen = "coop", animal = hen.id }, Actor("p1", AnimalRights.All), Start + 2.0, ctx);
            Assert.IsNotNull(plan.answer);
            string text = string.Join(" | ", plan.answer.lines);
            StringAssert.Contains("Production:", text);
            StringAssert.Contains("1 egg collected", text);
            StringAssert.Contains("1 egg ready", text);
            Assert.LessOrEqual(plan.answer.lines.Count, 30);

            AnimalHerd sty = Pen(book, "sty", AnimalHabitatKind.Sty, 20f, Start);
            AnimalRecord pig = Release(book, ctx, sty, "p1", "pig", "large_white", Start);
            var lines = new List<string>();
            AnimalObservation.History(pig, cat.Rules, Start, lines, cat.Species("pig"));
            Assert.IsFalse(lines.Contains("Production:"), "a pig gives nothing: no such part");
        }

        [Test]
        public void RipeningCompletesTheCycleUnderWayOnlyWhereThereIsRoom()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData sp = cat.Species("sheep");
            var ewe = new AnimalRecord { id = "e", species = "sheep", sex = AnimalSex.Female, stage = GrowthStage.Adult };
            Typical(ewe);
            Thriving(ewe);
            Assert.AreEqual(1, AnimalProduction.Ripen(ewe, sp, cat.Rules, Start));
            AnimalProductState st = AnimalProduction.State(ewe, Wool, false);
            Assert.AreEqual(3, st.Waiting, "a fleece: three bundles");
            Assert.AreEqual(AnimalProductStatus.Full, st.status, "it waits to be shorn");
            Assert.AreEqual("ready", AnimalProduction.StatusText(st.status, AnimalProduction.Spec(sp, Wool)));
            Assert.AreEqual(0, AnimalProduction.Ripen(ewe, sp, cat.Rules, Start + 1.0), "no room for another");
            Assert.AreEqual(3, st.Waiting);
            Assert.AreEqual(1f, AnimalObservation.FleeceOf(ewe, sp), "in full fleece");
        }
    }
}
#endif
