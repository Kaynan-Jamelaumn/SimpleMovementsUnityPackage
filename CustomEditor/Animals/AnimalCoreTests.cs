#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AnimalHusbandrySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the animal husbandry rules (plain C#: no scene, no assets): diets (what a food is to a species),
    /// nutrition apart from hunger (a full animal can be badly fed; one meal does not undo a week), growth that follows how an
    /// animal lives, hunger, thirst, toxins and upsets with their consequences, the owner-away floor, pastures that feed
    /// grazers, catch-ups that end exactly as watched hour by hour (and spread over frames), validated commands that are all or
    /// nothing, rights and ownership, requests done once, saving whole, ids never reused, item codes, and a species added as
    /// data alone.
    /// </summary>
    public class AnimalCoreTests
    {
        // ---------------------------------------------------------------- helpers
        /// <summary>Day 2 of spring (a whole hour: steps fall on the hour; grass grows in spring).</summary>
        private const double Start = 48.0;

        private static AnimalRules Rules() => AnimalRules.Defaults();

        private static AnimalCatalog Catalog(AnimalRules r = null) => AnimalCatalog.BuiltIn(r ?? Rules());

        private static AnimalSimContext Ctx(IAnimalCatalog cat, bool present = true) => new AnimalSimContext
        {
            catalog = cat,
            climate = new CityClimateSource(AgriSettings.Defaults()),
            ownerPresent = (k, t) => present,
            notices = new List<AnimalNotice>(),
        };

        private static AnimalHerd Pen(AnimalBook book, string id, AnimalHabitatKind kind, float area, double now, float grass = 0f)
        {
            var spec = new AnimalPenSpec
            {
                id = id, name = id, kind = kind, area = area, feederCapacity = 5000f, waterCapacity = 2000f, troughSurface = 0f,
                grassGrowth = grass, grassCapacity = 3f,
            };
            return book.EnsurePen(spec, "Test", "", now);
        }

        private static AnimalActor Actor(string key, AnimalRights rights, Dictionary<string, int> carried = null) => new AnimalActor
        {
            key = key,
            name = key,
            rights = rights,
            count = id => carried != null && carried.TryGetValue(id, out int n) ? n : 0,
            waterLitres = 0f,
        };

        private static AnimalTaken Taken(int count, float quality = 0.8f, int code = 0)
        {
            var t = new AnimalTaken { count = count, qualities = new List<float>(), codes = new List<int>() };
            for (int i = 0; i < count; i++)
            {
                t.qualities.Add(quality);
                t.codes.Add(code);
            }
            return t;
        }

        /// <summary>Lets a young animal out into a pen the way a player does (plan, take the crate, commit).</summary>
        private static AnimalRecord Release(AnimalBook book, AnimalSimContext ctx, AnimalHerd herd, string owner, string species, string breed, double now,
            GrowthStage stage = GrowthStage.Baby, int seed = 12345)
        {
            string item = AnimalIds.Livestock(species, breed);
            var actor = Actor(owner, AnimalRights.All, new Dictionary<string, int> { [item] = 1 });
            var r = new AnimalRequest { kind = AnimalActionKind.Release, pen = herd.pen.id, item = item, seq = 1 };
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            int code = AnimalItemCode.Livestock(AnimalSex.Female, stage, seed);
            AnimalResult res = AnimalCommands.Commit(book, plan, Taken(1, 0.6f, code), now, ctx);
            Assert.IsTrue(res.ok, res.message);
            return book.Find(res.animal);
        }

        private static void Stock(IAnimalCatalog cat, AnimalHerd herd, string food, float portions, float quality, double now) =>
            AnimalCommands.Stock(herd, cat.Food(food), portions, quality, "test", now);

        /// <summary>Phase 2: the same care, welfare and health state (a catch-up must end as watched for these too).</summary>
        private static void AssertSameCare(AnimalRecord watched, AnimalRecord other)
        {
            Assert.AreEqual(watched.hygiene, other.hygiene, 1e-3, "hygiene");
            Assert.AreEqual(watched.comfort, other.comfort, 1e-3, "comfort");
            Assert.AreEqual(watched.stress, other.stress, 1e-3, "stress");
            Assert.AreEqual(watched.happiness, other.happiness, 1e-3, "happiness");
            Assert.AreEqual(watched.welfare, other.welfare, 1e-3, "welfare");
            Assert.AreEqual(watched.hooves, other.hooves, 1e-3, "hooves");
            Assert.AreEqual(watched.affinity, other.affinity, 1e-3, "affinity");
            Assert.AreEqual(watched.life.welfare, other.life.welfare, 1e-3, "life record");
            Assert.AreEqual(watched.conditions.Count, other.conditions.Count, "illnesses");
            for (int i = 0; i < watched.conditions.Count; i++)
            {
                Assert.AreEqual(watched.conditions[i].id, other.conditions[i].id, "illness");
                Assert.AreEqual(watched.conditions[i].severity, other.conditions[i].severity, 1e-3, "its severity");
            }
            Assert.AreEqual(watched.exposures.Count, other.exposures.Count, "exposures");
            for (int i = 0; i < watched.exposures.Count; i++) Assert.AreEqual(watched.exposures[i].value, other.exposures[i].value, 1e-3, "exposure");
        }

        /// <summary>Phase 2: the same floor, bedding, trough and ground.</summary>
        private static void AssertSamePen(AnimalPenRecord watched, AnimalPenRecord other)
        {
            Assert.AreEqual(watched.dirt, other.dirt, 1e-3, "dirt");
            Assert.AreEqual(watched.bedding, other.bedding, 1e-3, "bedding");
            Assert.AreEqual(watched.waterDirt, other.waterDirt, 1e-3, "water dirt");
            Assert.AreEqual(watched.parasites, other.parasites, 1e-3, "worms");
        }

        // ---------------------------------------------------------------- diets
        [Test]
        public void ADietTellsFavouritesFromPoisonsByIdAndByTag()
        {
            AnimalCatalog cat = Catalog();
            AnimalDiet cow = cat.Species("cow").diet;
            Assert.AreEqual(FoodAffinity.Favorite, cow.AffinityOf(cat.Food("fresh_grass")));
            Assert.AreEqual(FoodAffinity.Preferred, cow.AffinityOf(cat.Food("hay")));
            Assert.AreEqual(FoodAffinity.Normal, cow.AffinityOf(cat.Food("wheat")), "#grain names wheat by its tag");
            Assert.AreEqual(FoodAffinity.Poor, cow.AffinityOf(cat.Food("low_quality_feed")), "an id beats a tag (cheap feed is also #grain)");
            Assert.AreEqual(FoodAffinity.Harmful, cow.AffinityOf(cat.Food("spoiled_feed")));
            Assert.AreEqual(FoodAffinity.Toxic, cow.AffinityOf(cat.Food("poisonous_herbs")));
            Assert.AreEqual(FoodAffinity.Inedible, cow.AffinityOf(cat.Food("meat_scraps")));
            Assert.AreEqual(FoodAffinity.Normal, cat.Species("pig").diet.AffinityOf(cat.Food("meat_scraps")), "pigs are omnivores");
        }

        [Test]
        public void ItemsAreFoodsByTheirIdsAndHarvestedMeatByPrefix()
        {
            AnimalCatalog cat = Catalog();
            Assert.AreEqual("hay", cat.FoodOfItem(AnimalIds.Feed("hay"))?.id);
            Assert.AreEqual("carrot", cat.FoodOfItem("agri:produce/carrot")?.id);
            Assert.AreEqual("meat_scraps", cat.FoodOfItem("harvest:boar/pork")?.id);
            Assert.IsNull(cat.FoodOfItem("agri:tool/hoe"));
        }

        // ---------------------------------------------------------------- nutrition apart from hunger
        [Test]
        public void AFullAnimalCanStillBeBadlyNourished()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd cheap = Pen(book, "cheap", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd good = Pen(book, "good", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, cheap, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            AnimalRecord b = Release(book, ctx, good, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            Stock(cat, cheap, "low_quality_feed", 2000f, 0.6f, Start);
            Stock(cat, good, "hay", 1200f, 0.8f, Start);
            Stock(cat, good, "alfalfa", 600f, 0.8f, Start);
            AnimalSim.AdvanceHerd(cheap, Start + 24 * 7, ctx);
            AnimalSim.AdvanceHerd(good, Start + 24 * 7, ctx);
            Assert.Greater(a.satiety, 50f, "the cheap feed keeps it full");
            Assert.Less(a.nutrition, 40f, "but it does not nourish it");
            Assert.Greater(b.nutrition, 60f, "forage nourishes a cow");
            Assert.Greater(b.health, a.health, "poor nutrition costs health");
        }

        [Test]
        public void OneGoodMealDoesNotMakeUpForAWeekOfPoorOnes()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "angus", Start, GrowthStage.Adult);
            Stock(cat, h, "low_quality_feed", 2000f, 0.6f, Start);
            double t = Start + 24 * 7;
            AnimalSim.AdvanceHerd(h, t, ctx);
            float before = a.nutrition;
            // A favourite meal from the hand, as much as it will take.
            a.satiety = 40f;
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [AnimalIds.Feed("fresh_grass")] = 10 });
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = h.pen.id, animal = a.id, item = AnimalIds.Feed("fresh_grass"), count = 1 }, actor, t, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            AnimalResult res = AnimalCommands.Commit(book, plan, Taken(plan.takeCount, 0.9f), t, ctx);
            Assert.IsTrue(res.ok, res.message);
            Assert.Less(a.nutrition - before, 15f, "a week is not undone by one meal");
            Assert.Less(a.nutrition, 45f, "it is still badly nourished after it");
            Assert.AreEqual(1, a.feedings.Count, "the meal is in its feeding history");
        }

        // ---------------------------------------------------------------- growth
        [Test]
        public void AnimalsGrowThroughTheirStagesAndPoorFeedingSlowsThem()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd fed = Pen(book, "fed", AnimalHabitatKind.Coop, 20f, Start);
            AnimalHerd starved = Pen(book, "poor", AnimalHabitatKind.Coop, 20f, Start);
            AnimalRecord a = Release(book, ctx, fed, "p1", "chicken", "leghorn", Start, GrowthStage.Baby, 777);
            AnimalRecord b = Release(book, ctx, starved, "p1", "chicken", "leghorn", Start, GrowthStage.Baby, 777);
            Assert.AreEqual(GrowthStage.Baby, a.stage);
            Stock(cat, fed, "chicken_feed", 400f, 0.95f, Start);
            Stock(cat, fed, "mealworms", 60f, 0.9f, Start);
            Stock(cat, starved, "low_quality_feed", 400f, 0.5f, Start);
            AnimalSim.AdvanceHerd(fed, Start + 300.0, ctx);
            AnimalSim.AdvanceHerd(starved, Start + 300.0, ctx);
            Assert.AreEqual(GrowthStage.Adult, a.stage, $"a well fed chick is grown in 300 hours (it grew {a.development:0} h)");
            Assert.Less((int)b.stage, (int)GrowthStage.Adult, $"a poorly fed one is not (it grew {b.development:0} h)");
            Assert.AreEqual(a.AgeHours(Start + 300.0), b.AgeHours(Start + 300.0), 1e-6, "but both are as old");
            Assert.IsTrue(a.events.Exists(e => e.kind == AnimalEventKind.StageReached), "reaching a stage is in its history");
        }

        // ---------------------------------------------------------------- hardship
        [Test]
        public void HungerAndThirstHarmAndKill()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            h.pen.water = 0f;
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            AnimalSim.AdvanceHerd(h, Start + 200.0, ctx);
            Assert.AreEqual(AnimalStatus.Dead, a.status);
            Assert.IsTrue(a.deathCause == AnimalDeathCause.Dehydration || a.deathCause == AnimalDeathCause.Starvation, a.deathCause.ToString());
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.Died && n.animal == a.id), "the owner is told");
            Assert.AreEqual(0, book.LivingOwnedBy("p1"), "the dead are not counted as living");
        }

        [Test]
        public void WhileItsOwnerIsAwayAnAnimalNeverFallsBelowTheFloor()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext away = Ctx(cat, present: false);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            h.pen.water = 0f;
            AnimalRecord a = Release(book, away, h, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            AnimalSim.AdvanceHerd(h, Start + 24 * 20, away);
            Assert.AreEqual(AnimalStatus.Alive, a.status, "an evening offline does not kill a herd");
            Assert.GreaterOrEqual(a.health, rules.awayHealthFloor - 0.01f);
            Assert.Less(a.health, 60f, "but it suffers");
        }

        [Test]
        public void ToxicFoodPoisonsAndHarmfulFoodUpsets()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            Stock(cat, h, "hay", 600f, 0.8f, Start);
            a.satiety = b.satiety = 30f;
            string herbs = AnimalIds.Feed("poisonous_herbs"), spoiled = AnimalIds.Feed("spoiled_feed");
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [herbs] = 10, [spoiled] = 10 });
            AnimalPlan p1 = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = h.pen.id, animal = a.id, item = herbs }, actor, Start, ctx);
            Assert.IsFalse(p1.Refused, p1.refusal?.message);
            Assert.IsTrue(AnimalCommands.Commit(book, p1, Taken(p1.takeCount), Start, ctx).ok);
            AnimalPlan p2 = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = h.pen.id, animal = b.id, item = spoiled }, actor, Start, ctx);
            Assert.IsFalse(p2.Refused, p2.refusal?.message);
            Assert.IsTrue(AnimalCommands.Commit(book, p2, Taken(p2.takeCount), Start, ctx).ok);
            float tolerance = cat.Species("cow").needs.toxinTolerance;
            Assert.Greater(a.toxin, tolerance, "the herbs poison it");
            Assert.Greater(b.upset, 0f, "the spoiled feed upsets it");
            var signs = new List<string>();
            AnimalObservation.Signs(a, cat.Species("cow"), signs);
            Assert.IsTrue(signs.Exists(s => s.Contains("trembling")), "poisoning shows");
            AnimalSim.AdvanceHerd(h, Start + 24.0, ctx);
            Assert.Less(a.health, 90f, "poison costs health");
            Assert.Greater(b.health, a.health);
        }

        [Test]
        public void AFullAnimalRefusesFoodAndNobodyEatsWhatIsNotFood()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            a.satiety = 99f;
            string hay = AnimalIds.Feed("hay");
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [hay] = 5, ["harvest:boar/pork"] = 5 });
            AnimalPlan full = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = h.pen.id, animal = a.id, item = hay }, actor, Start, ctx);
            Assert.IsTrue(full.Refused);
            StringAssert.Contains("not hungry", full.refusal.message);
            a.satiety = 20f;
            AnimalPlan meat = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = h.pen.id, animal = a.id, item = "harvest:boar/pork" }, actor, Start, ctx);
            Assert.IsTrue(meat.Refused, "a cow does not eat meat");
        }

        // ---------------------------------------------------------------- habitat
        [Test]
        public void GrazersLiveOnAPasturesGrass()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pasture", AnimalHabitatKind.Pasture, 48f, Start, grass: 1f);
            float startGrass = h.pen.grass;
            Assert.AreEqual(144f, startGrass, 0.01f, "a new pasture starts lush");
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "jersey", Start, GrowthStage.Adult);
            AnimalSim.AdvanceHerd(h, Start + 24 * 10, ctx);
            Assert.AreEqual(AnimalStatus.Alive, a.status);
            Assert.Greater(a.health, 80f, "grass alone keeps one cow well");
            Assert.Greater(a.nutrition, 60f);
            Assert.IsEmpty(h.pen.feeder, "no feeder was needed");
        }

        [Test]
        public void ThePenAndTheRulesKeepCountOfRoomAndKind()
        {
            AnimalRules rules = Rules();
            rules.maxAnimalsPerPlayer = 4;
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd pasture = Pen(book, "pasture", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 12f, Start);
            Assert.AreEqual(0f, coop.pen.grass, "a coop floor grows no grass");
            for (int i = 0; i < 3; i++) Release(book, ctx, pasture, "p1", "cow", "holstein", Start, GrowthStage.Baby, 10 + i);
            string calf = AnimalIds.Livestock("cow", "holstein");
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [calf] = 3, [AnimalIds.Livestock("chicken", "leghorn")] = 3 });
            AnimalPlan fifth = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Release, pen = pasture.pen.id, item = calf }, actor, Start, ctx);
            Assert.IsTrue(fifth.Refused, "48 m² holds three cows (16 m² each, as adults - calves grow)");
            AnimalPlan cowInCoop = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Release, pen = coop.pen.id, item = calf }, actor, Start, ctx);
            Assert.IsTrue(cowInCoop.Refused, "a cow does not live in a coop");
            Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            AnimalPlan overLimit = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Release, pen = coop.pen.id, item = AnimalIds.Livestock("chicken", "leghorn") }, actor, Start, ctx);
            Assert.IsTrue(overLimit.Refused, "a player keeps no more animals than the rules allow");
        }

        [Test]
        public void HarmfulAndToxicFoodIsEatenOnlyAsFarAsHungerDrivesIt()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            Stock(cat, h, "poisonous_herbs", 200f, 0.8f, Start);
            a.satiety = 5f;
            AnimalSim.AdvanceHerd(h, Start + 1.0, ctx);
            Assert.AreEqual(AnimalStatus.Alive, a.status, "a starving cow nibbles poison, it does not gorge on it");
            Assert.Less(a.satiety, cat.Rules.toxicBelow + 5f, "it stops once its hunger is eased");
            Assert.Greater(a.toxin, 0f);

            // From a hand: a hen takes nightshade only as far as the hand threshold.
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 12f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start, GrowthStage.Adult);
            hen.satiety = 20f;
            string nightshade = "agri:produce/nightshade";
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [nightshade] = 5 });
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = "coop", animal = hen.id, item = nightshade }, actor, Start + 1.0, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.IsTrue(AnimalCommands.Commit(book, plan, Taken(plan.takeCount), Start + 1.0, ctx).ok);
            Assert.Less(hen.satiety, cat.Rules.toxicFromHandBelow + 1f, "a hand's poison is taken only while hungry");
        }

        [Test]
        public void AStockedPastureWithItsUsualTroughKeepsACowAWeek()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            // A pen as "+ Pasture" makes it: 8 x 6 m, the trough sized to it, grass growing as a pasture's does.
            var spec = new AnimalPenSpec { id = "pasture", kind = AnimalHabitatKind.Pasture, area = 48f, feederCapacity = 120f, waterCapacity = 192f, troughSurface = 1.2f, grassGrowth = 1.5f };
            AnimalHerd h = book.EnsurePen(spec, "Test", "", Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult);
            AnimalSim.AdvanceHerd(h, Start + 24 * 7, ctx);
            Assert.AreEqual(AnimalStatus.Alive, a.status);
            Assert.Greater(a.health, 70f, "grass and the trough keep one cow a week without a bucket");
            Assert.Greater(h.pen.water, 0f);
        }

        [Test]
        public void TheRemainsOfAnAnimalThatDiedBoardedCanBeCleared()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "sheep", "jacob", Start, GrowthStage.Adult);
            AnimalCommands.Board(book, a, Start, ctx, "the lease ended");
            AnimalSim.Die(a, AnimalDeathCause.OldAge, Start + 1.0, ctx, cat.Species("sheep"));
            AnimalPenView view = AnimalObservation.PenView(book, h, "p1", AnimalRights.Look, Start + 1.0);
            Assert.IsTrue(view.elsewhere.Exists(v => v.id == a.id), "its owner sees the remains to clear them");
            var owner = Actor("p1", AnimalRights.Look);
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.RemoveRemains, pen = "pen", animal = a.id }, owner, Start + 1.0, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.IsTrue(AnimalCommands.Commit(book, plan, default, Start + 1.0, ctx).ok);
            Assert.IsNull(book.Find(a.id));
            var stranger = Actor("p2", AnimalRights.All);
            AnimalRecord b = Release(book, ctx, h, "p1", "sheep", "jacob", Start, GrowthStage.Adult, 99);
            AnimalCommands.Board(book, b, Start, ctx, "the lease ended");
            AnimalSim.Die(b, AnimalDeathCause.OldAge, Start + 1.0, ctx, cat.Species("sheep"));
            Assert.IsTrue(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.RemoveRemains, pen = "pen", animal = b.id }, stranger, Start + 1.0, ctx).Refused,
                "only its owner clears boarded remains");
        }

        // ---------------------------------------------------------------- time
        [Test]
        public void AWeekNobodyWatchedEndsExactlyAsAWeekWatchedHourByHour()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start, grass: 1f);
            for (int i = 0; i < 3; i++) Release(book, ctx, h, "p" + i, "sheep", "merino", Start, GrowthStage.Baby, 100 + i);
            Stock(cat, h, "hay", 300f, 0.7f, Start);
            Stock(cat, h, "clover", 100f, 0.8f, Start);
            string json = JsonUtility.ToJson(book.Export(Start, 0));

            var slept = new AnimalBook(cat, "test");
            slept.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            slept.Herd("pen").spec = h.spec;
            for (int hour = 1; hour <= 24 * 7; hour++) AnimalSim.AdvanceHerd(h, Start + hour, ctx);
            AnimalSim.AdvanceHerd(slept.Herd("pen"), Start + 24 * 7, Ctx(cat));

            foreach (AnimalRecord watched in h.animals)
            {
                AnimalRecord other = slept.Find(watched.id);
                Assert.IsNotNull(other);
                Assert.AreEqual(watched.satiety, other.satiety, 1e-3, "satiety");
                Assert.AreEqual(watched.hydration, other.hydration, 1e-3, "hydration");
                Assert.AreEqual(watched.nutrition, other.nutrition, 1e-3, "nutrition");
                Assert.AreEqual(watched.health, other.health, 1e-3, "health");
                Assert.AreEqual(watched.development, other.development, 1e-3, "growth");
                Assert.AreEqual(watched.stage, other.stage);
                AssertSameCare(watched, other);
            }
            Assert.AreEqual(h.pen.grass, slept.Herd("pen").pen.grass, 1e-2, "grass");
            Assert.AreEqual(h.pen.FeederPortions, slept.Herd("pen").pen.FeederPortions, 1e-2, "feeder");
            AssertSamePen(h.pen, slept.Herd("pen").pen);
            Assert.Greater(h.pen.parasites, 0f, "a stocked pasture gathers worm eggs (so the comparison means something)");
            Assert.Less(h.animals[0].hooves, 100f, "and hooves wear");
        }

        [Test]
        public void ACatchUpSpreadOverFramesEndsTheSameAsOneDoneAtOnce()
        {
            AnimalCatalog cat = Catalog();
            var once = new AnimalBook(cat, "test");
            AnimalSimContext ctx = Ctx(cat);
            AnimalHerd h = Pen(once, "pen", AnimalHabitatKind.Coop, 20f, Start);
            for (int i = 0; i < 5; i++) Release(once, ctx, h, "p1", "chicken", "rhode_island_red", Start, GrowthStage.Baby, 50 + i);
            Stock(cat, h, "chicken_feed", 300f, 0.8f, Start);
            string json = JsonUtility.ToJson(once.Export(Start, 0));
            var spread = new AnimalBook(cat, "test");
            spread.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            spread.Herd("pen").spec = h.spec;

            double end = Start + 24 * 5;
            once.Advance(end, ctx, null, int.MaxValue);
            AnimalSimContext c2 = Ctx(cat);
            for (int frame = 0; frame < 10000 && spread.Herd("pen").pen.livedTo < end - 1e-6; frame++) spread.Advance(end, c2, null, 7);
            Assert.AreEqual(end, spread.Herd("pen").pen.livedTo, 1e-6, "it got there");
            foreach (AnimalRecord a in once.Herd("pen").animals)
            {
                AnimalRecord b = spread.Find(a.id);
                Assert.AreEqual(a.health, b.health, 1e-3);
                Assert.AreEqual(a.nutrition, b.nutrition, 1e-3);
                Assert.AreEqual(a.development, b.development, 1e-3);
                AssertSameCare(a, b);
            }
            AssertSamePen(once.Herd("pen").pen, spread.Herd("pen").pen);
            Assert.Greater(once.Herd("pen").pen.dirt, 0f, "the coop got dirty (so the comparison means something)");
        }

        [Test]
        public void BoardedAnimalsAreFedAPlainDiet()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "goat", "saanen", Start, GrowthStage.Juvenile);
            AnimalCommands.Board(book, a, Start, ctx, "the lease ended");
            Assert.AreEqual(AnimalStatus.Boarded, a.status);
            Assert.AreEqual("", a.pen);
            Assert.AreEqual(0, book.LivingInPen("pen"));
            book.Advance(Start + 24 * 10, ctx, null, int.MaxValue);
            Assert.AreEqual(AnimalStatus.Boarded, a.status, "it lives on at the stable");
            Assert.Greater(a.health, 80f);
            Assert.IsTrue(a.events.Exists(e => e.kind == AnimalEventKind.Boarded));
        }

        // ---------------------------------------------------------------- commands, rights, ownership
        [Test]
        public void ACommandThatGotNothingDoesNothing()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            string hay = AnimalIds.Feed("hay");
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [hay] = 4 });
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.StockFeeder, pen = h.pen.id, item = hay, count = 4 }, actor, Start, ctx);
            Assert.IsFalse(plan.Refused);
            Assert.AreEqual(4, plan.takeCount);
            AnimalResult res = AnimalCommands.Commit(book, plan, Taken(0), Start, ctx);
            Assert.IsFalse(res.ok, "the bag changed meanwhile: nothing taken, nothing done");
            Assert.AreEqual(0f, h.pen.FeederPortions);
            AnimalResult some = AnimalCommands.Commit(book, plan, Taken(2), Start, ctx);
            Assert.IsTrue(some.ok);
            Assert.AreEqual(2 * cat.Food("hay").portionsPerItem, h.pen.FeederPortions, 1e-3, "only what was really taken is put in");
        }

        [Test]
        public void AFeederTakesNoMoreThanItHolds()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = book.EnsurePen(new AnimalPenSpec { id = "small", kind = AnimalHabitatKind.Coop, area = 10f, feederCapacity = 13f, waterCapacity = 10f }, "Test", "", Start);
            string feed = AnimalIds.Feed("chicken_feed"); // 6 portions an item
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [feed] = 10 });
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.StockFeeder, pen = "small", item = feed, count = 10 }, actor, Start, ctx);
            Assert.AreEqual(2, plan.takeCount, "13 portions of room take two items of 6");
            AnimalCommands.Commit(book, plan, Taken(2), Start, ctx);
            AnimalPlan again = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.StockFeeder, pen = "small", item = feed, count = 1 }, actor, Start, ctx);
            Assert.IsTrue(again.Refused, "the feeder is full");
        }

        [Test]
        public void OnlyTheOwnerNamesOrMovesAnAnimalAndOnlyKeepersTendThePen()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd a = Pen(book, "a", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd b = Pen(book, "b", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord sheep = Release(book, ctx, a, "owner", "sheep", "suffolk", Start);
            string hay = AnimalIds.Feed("hay");
            var visitor = Actor("visitor", AnimalRights.Look, new Dictionary<string, int> { [hay] = 5 });
            var friend = Actor("friend", AnimalRights.Look | AnimalRights.Tend | AnimalRights.CareForOthers, new Dictionary<string, int> { [hay] = 5 });
            var stranger = Actor("stranger", AnimalHousing.Public, new Dictionary<string, int> { [hay] = 5 });
            var owner = Actor("owner", AnimalRights.All);
            sheep.satiety = 30f;
            Assert.IsTrue(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = "a", animal = sheep.id, item = hay }, visitor, Start, ctx).Refused, "a visitor only looks");
            Assert.IsTrue(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.StockFeeder, pen = "a", item = hay }, visitor, Start, ctx).Refused);
            Assert.IsFalse(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = "a", animal = sheep.id, item = hay }, friend, Start, ctx).Refused, "a friend let in tends it");
            Assert.IsTrue(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Feed, pen = "a", animal = sheep.id, item = hay }, stranger, Start, ctx).Refused,
                "on public land a stranger may stock the feeder, never feed someone else's animal by hand");
            Assert.IsFalse(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.StockFeeder, pen = "a", item = hay }, stranger, Start, ctx).Refused);
            Assert.IsTrue(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Rename, pen = "a", animal = sheep.id, text = "Dolly" }, friend, Start, ctx).Refused, "only its owner names it");
            Assert.IsTrue(AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Move, pen = "b", animal = sheep.id }, friend, Start, ctx).Refused, "only its owner moves it");
            AnimalPlan rename = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Rename, pen = "a", animal = sheep.id, text = "  <b>Dolly</b> " }, owner, Start, ctx);
            Assert.IsTrue(AnimalCommands.Commit(book, rename, default, Start, ctx).ok);
            Assert.AreEqual("bDolly/b", sheep.name, "markup is taken out of names");
            AnimalPlan move = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Move, pen = "b", animal = sheep.id }, owner, Start, ctx);
            Assert.IsTrue(AnimalCommands.Commit(book, move, default, Start, ctx).ok);
            Assert.AreEqual("b", sheep.pen);
            Assert.AreEqual(0, book.LivingInPen("a"));
            Assert.AreEqual(1, book.LivingInPen("b"));
        }

        [Test]
        public void ARequestDoneOnceIsAnsweredButNeverDoneTwice()
        {
            AnimalCatalog cat = Catalog();
            var book = new AnimalBook(cat, "test");
            Assert.IsFalse(book.WasDone("p1", "tok", out _));
            book.RememberDone("p1", "tok", 7, 100.0, true, "Done.", "a1");
            Assert.IsTrue(book.WasDone("p1", "tok", out AnimalDoneRequest d));
            Assert.AreEqual("Done.", d.message);
            Assert.IsFalse(book.WasDone("p2", "tok", out _), "tokens are per player");
            // Remembered across a save.
            var copy = new AnimalBook(cat, "test");
            copy.Import(JsonUtility.FromJson<AnimalWorldSave>(JsonUtility.ToJson(book.Export(0, 0))));
            Assert.IsTrue(copy.WasDone("p1", "tok", out _));
            copy.ForgetOldRequests(100.0 + cat.Rules.rememberRequestsHours + 1.0);
            Assert.IsFalse(copy.WasDone("p1", "tok", out _), "and forgotten after a while");
        }

        // ---------------------------------------------------------------- identity and saving
        [Test]
        public void TheWorldSavesAndLoadsWholeAndIdsAreNeverReused()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Hutch, 10f, Start);
            AnimalRecord r1 = Release(book, ctx, h, "p1", "rabbit", "rex", Start);
            AnimalRecord r2 = Release(book, ctx, h, "p1", "rabbit", "dutch", Start);
            Stock(cat, h, "hay", 50f, 0.7f, Start);
            AnimalSim.AdvanceHerd(h, Start + 30.0, ctx);
            AnimalSim.Die(r2, AnimalDeathCause.Illness, Start + 30.0, ctx, cat.Species("rabbit"));
            Assert.IsTrue(book.Remove(r2), "the remains of the dead can be cleared");
            Assert.IsFalse(book.Remove(r1), "the living never are");

            string json = JsonUtility.ToJson(book.Export(Start + 30.0, 0));
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            AnimalRecord back = loaded.Find(r1.id);
            Assert.IsNotNull(back);
            Assert.AreEqual(r1.satiety, back.satiety, 1e-4);
            Assert.AreEqual(r1.genes.growth, back.genes.growth, 1e-6, "genes are kept");
            Assert.AreEqual(r1.events.Count, back.events.Count);
            Assert.AreEqual(h.pen.FeederPortions, loaded.Pen("pen").FeederPortions, 1e-3);
            Assert.AreEqual(1, loaded.InPen("pen").Count);
            Assert.AreEqual(book.Serial, loaded.Serial);
            string fresh = loaded.NewId("rabbit");
            Assert.AreNotEqual(r1.id, fresh);
            Assert.AreNotEqual(r2.id, fresh, "a removed animal's id is never given again");

            var future = new AnimalWorldSave { version = AnimalWorldSave.CurrentVersion + 1 };
            Assert.Throws<InvalidOperationException>(() => new AnimalBook(cat).Import(future), "a newer save is never half read");
        }

        [Test]
        public void AnimalsMadeFromTheSameCrateAreTheSameAnimal()
        {
            AnimalBreedData breed = Catalog().Species("cow").Breed("jersey");
            AnimalGenes a = AnimalGenes.FromSeed(breed, 4242), b = AnimalGenes.FromSeed(breed, 4242), c = AnimalGenes.FromSeed(breed, 4243);
            Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b), "deterministic");
            Assert.AreNotEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(c));
        }

        [Test]
        public void ItemCodesRoundTrip()
        {
            int code = AnimalItemCode.Livestock(AnimalSex.Male, GrowthStage.Juvenile, 0xABCDEF);
            Assert.IsTrue(AnimalItemCode.TryLivestock(code, out AnimalSex sex, out GrowthStage stage, out int seed));
            Assert.AreEqual(AnimalSex.Male, sex);
            Assert.AreEqual(GrowthStage.Juvenile, stage);
            Assert.AreEqual(0xABCDEF, seed);
            Assert.Greater(code, 1, "never a 'no value' code");
            Assert.IsFalse(AnimalItemCode.TryLivestock(0, out _, out _, out _), "a bought crate carries no animal yet");
            Assert.IsTrue(AnimalItemCode.TryFeed(AnimalItemCode.Feed(0.734f), out float q));
            Assert.AreEqual(0.734f, q, 1e-3);
            Assert.IsTrue(AnimalIds.ParseLivestock(AnimalIds.Livestock("cow", "jersey"), out string sp, out string br));
            Assert.AreEqual("cow", sp);
            Assert.AreEqual("jersey", br);
        }

        [Test]
        public void ACrateWithoutACodeBecomesAnAnimalOfItsOwn()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "world-a");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Coop, 20f, Start);
            string item = AnimalIds.Livestock("chicken", "silkie");
            var actor = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [item] = 2 });
            AnimalPlan p = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Release, pen = "pen", item = item, text = "Pearl" }, actor, Start, ctx);
            AnimalResult r = AnimalCommands.Commit(book, p, Taken(1, 0.6f, 0), Start, ctx);
            Assert.IsTrue(r.ok, r.message);
            AnimalRecord a = book.Find(r.animal);
            Assert.AreEqual("Pearl", a.name);
            Assert.AreEqual("silkie", a.breed);
            Assert.AreEqual(GrowthStage.Baby, a.stage, "sold young");
            Assert.AreNotEqual(AnimalSex.None, a.sex);
            Assert.AreNotEqual(0, a.genes.seed);
            Assert.AreEqual("p1", a.owner);
            Assert.IsTrue(a.events.Exists(e => e.kind == AnimalEventKind.Acquired));
        }

        // ---------------------------------------------------------------- data, not code
        [Test]
        public void ANewSpeciesNeedsNoCode()
        {
            var llama = new AnimalSpeciesData
            {
                id = "llama", name = "Llama", plural = "Llamas",
                stages = new List<AnimalStageSpec>
                {
                    new AnimalStageSpec(GrowthStage.Baby, 40f, 0.4f, 0.4f),
                    new AnimalStageSpec(GrowthStage.Adult, 0f, 1f, 1f, true, true),
                },
                habitat = new AnimalHabitatNeeds { kinds = AnimalHabitatKind.Pasture, areaPerAnimal = 10f },
                diet = AnimalDiet.Of(favorite: new[] { "#forage" }),
                needs = new AnimalNeedsSpec { portionScale = 3f },
                breeds = new List<AnimalBreedData> { new AnimalBreedData("andean", "Andean") },
            };
            var errors = new List<string>();
            var warnings = new List<string>();
            llama.Validate(errors, warnings);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            AnimalCatalog cat = Catalog();
            cat.AddSpecies(llama);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 40f, Start, grass: 1f);
            AnimalRecord a = Release(book, ctx, h, "p1", "llama", "andean", Start);
            AnimalSim.AdvanceHerd(h, Start + 72.0, ctx);
            Assert.AreEqual(GrowthStage.Adult, a.stage, "it grows by its own stages");
            Assert.AreEqual(FoodAffinity.Favorite, llama.diet.AffinityOf(cat.Food("hay")), "and eats by its own diet");
        }

        [Test]
        public void TheBuiltInContentIsSound()
        {
            AnimalCatalog cat = Catalog();
            Assert.AreEqual(8, cat.AllSpecies.Count);
            foreach (AnimalSpeciesData s in cat.AllSpecies)
            {
                var errors = new List<string>();
                var warnings = new List<string>();
                s.Validate(errors, warnings);
                Assert.IsEmpty(errors, $"{s.id}: {string.Join("; ", errors)}");
                Assert.IsTrue(AnimalIds.ValidKey(s.id));
                foreach (AnimalBreedData b in s.breeds) Assert.IsTrue(AnimalIds.ValidKey(b.id), b.id);
                for (int a = (int)FoodAffinity.Favorite; a <= (int)FoodAffinity.Toxic; a++)
                    foreach (string e in s.diet.Entries((FoodAffinity)a))
                        if (!e.StartsWith("#")) Assert.IsNotNull(cat.Food(e), $"{s.id}'s diet names '{e}', which is no food");
            }
            foreach (AnimalFoodData f in cat.AllFoods)
            {
                var errors = new List<string>();
                f.Validate(errors, new List<string>());
                Assert.IsEmpty(errors, $"{f.id}: {string.Join("; ", errors)}");
            }
        }

        [Test]
        public void WhatIsShownIsCoarse()
        {
            Assert.AreEqual(4, AnimalObservation.Level(95f));
            Assert.AreEqual(2, AnimalObservation.Level(45f));
            Assert.AreEqual(0, AnimalObservation.Level(5f));
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "owner", "cow", "angus", Start);
            AnimalPenView mine = AnimalObservation.PenView(book, h, "owner", AnimalRights.All, Start);
            AnimalPenView theirs = AnimalObservation.PenView(book, h, "visitor", AnimalRights.Look, Start);
            Assert.IsTrue(mine.animals[0].mine);
            Assert.IsFalse(theirs.animals[0].mine);
            string json = theirs.ToJson();
            StringAssert.DoesNotContain("\"toxin\"", json, "toxin loads never leave the server");
            StringAssert.DoesNotContain("\"genes\"", json, "nor genes");
            StringAssert.DoesNotContain("\"owner\":", json, "nor owners' keys");
        }
    }
}
#endif
