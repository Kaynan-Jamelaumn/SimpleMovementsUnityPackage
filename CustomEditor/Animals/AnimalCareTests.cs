#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AnimalHusbandrySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of Phase 2 of animal husbandry (plain C#: no scene, no assets): care actions named by id (brushing,
    /// washing, petting, hoof care) with their tools, water, cooldowns and the harm of too much or the wrong kind; welfare and
    /// its bands; illnesses that come from how an animal lives (a dirty pen, crowding, contagion, worms, bad food), take hold,
    /// worsen and mend; examining, medicine, overdoses and the veterinarian; death by illness, slaughter and the remains;
    /// mucking out and the trough; saves of version 1 and 2; catch-ups that end as watched, and the short steps requests make
    /// that fall ill as hourly ones do; and views that stay coarse.
    /// </summary>
    public class AnimalCareTests
    {
        // ---------------------------------------------------------------- helpers
        /// <summary>Day 2 of spring (a whole hour: steps fall on the hour).</summary>
        private const double Start = 48.0;

        private const string Straw = "agri:amend/straw_mulch";
        private const string Infection = "infection";

        private static AnimalRules Rules() => AnimalRules.Defaults();

        private static AnimalCatalog Catalog(AnimalRules r = null) => AnimalCatalog.BuiltIn(r ?? Rules());

        private static AnimalSimContext Ctx(IAnimalCatalog cat, bool present = true, IAgriClimateSource climate = null) => new AnimalSimContext
        {
            catalog = cat,
            climate = climate ?? new CityClimateSource(AgriSettings.Defaults()),
            ownerPresent = (k, t) => present,
            notices = new List<AnimalNotice>(),
        };

        /// <summary>The same weather every hour (tests that need the cold or the heat).</summary>
        private sealed class FixedClimate : IAgriClimateSource
        {
            private readonly AgriWeather weather;
            public FixedClimate(float temperature, float rain = 0f, float wind = 0f) =>
                weather = new AgriWeather { temperature = temperature, rain = rain, wind = wind, humidity = 0.6f, clouds = 0.4f, light = 0.5f, day = true };
            public AgriWeather WeatherAt(double time) => weather;
            public bool Historic => true;
        }

        private static AnimalHerd Pen(AnimalBook book, string id, AnimalHabitatKind kind, float area, double now, float grass = 0f)
        {
            var spec = new AnimalPenSpec
            {
                id = id, name = id, kind = kind, area = area, feederCapacity = 5000f, waterCapacity = 2000f, troughSurface = 0f,
                grassGrowth = grass, grassCapacity = 3f,
            };
            return book.EnsurePen(spec, "Test", "", now);
        }

        private static AnimalActor Actor(string key, AnimalRights rights, Dictionary<string, int> carried = null, float water = 0f) => new AnimalActor
        {
            key = key,
            name = key,
            rights = rights,
            count = id => carried != null && carried.TryGetValue(id, out int n) ? n : 0,
            waterLitres = water,
            waterKind = (int)WaterSourceKind.Well,
        };

        /// <summary>A player who also carries these care tools (by tool id; <paramref name="edge"/> 1: new, 0: worn out).</summary>
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

        /// <summary>A keeper carrying every care tool of animal husbandry, plenty of straw and a container of well water.</summary>
        private static AnimalActor Keeper(IAnimalCatalog cat, string key = "p1") =>
            WithTools(Actor(key, AnimalRights.All, new Dictionary<string, int> { [Straw] = 1000 }, 500f), cat, 1f, "soft_brush", "curry_comb", "hoof_pick", "pitchfork");

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

        /// <summary>What the caller takes for a plan, all of it (items at a fair quality, the water, the fee).</summary>
        private static AnimalTaken All(AnimalPlan plan, float quality = 0.8f)
        {
            AnimalTaken t = Taken(plan.takeCount, quality);
            t.litres = plan.takeWater;
            t.waterKind = plan.actor != null ? plan.actor.waterKind : 0;
            t.paid = plan.fee;
            return t;
        }

        /// <summary>Plans a request, takes what it asks for and commits it (the way the director performs it); asserts it was done.</summary>
        private static AnimalResult Do(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsFalse(plan.Refused, $"{r.kind}: {plan.refusal?.message}");
            AnimalResult res = AnimalCommands.Commit(book, plan, All(plan), now, ctx);
            Assert.IsTrue(res.ok, $"{r.kind}: {res.message}");
            return res;
        }

        /// <summary>As <see cref="Do"/>, but a request that is refused (not needed now) is simply skipped.</summary>
        private static AnimalResult TryDo(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            return plan.Refused ? plan.refusal : AnimalCommands.Commit(book, plan, All(plan), now, ctx);
        }

        /// <summary>Why a request is refused (asserts it is).</summary>
        private static string Refusal(AnimalBook book, AnimalSimContext ctx, AnimalRequest r, AnimalActor actor, double now)
        {
            AnimalPlan plan = AnimalCommands.Plan(book, r, actor, now, ctx);
            Assert.IsTrue(plan.Refused, $"{r.kind} should be refused");
            return plan.refusal.message;
        }

        private static AnimalRequest Care(AnimalRecord a, string action, string supply = null) =>
            new AnimalRequest { kind = AnimalActionKind.Care, pen = a.pen, animal = a.id, text = action, item = supply };

        /// <summary>Lets a young animal out into a pen the way a player does (plan, take the crate, commit).</summary>
        private static AnimalRecord Release(AnimalBook book, AnimalSimContext ctx, AnimalHerd herd, string owner, string species, string breed, double now,
            GrowthStage stage = GrowthStage.Adult, int seed = 12345)
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

        /// <summary>Keeps a food in the feeder at about this many portions.</summary>
        private static void TopUp(IAnimalCatalog cat, AnimalHerd herd, string food, float portions, double now)
        {
            float have = 0f;
            foreach (FeederLot l in herd.pen.feeder)
                if (l != null && l.food == food) have += l.portions;
            if (have < portions) Stock(cat, herd, food, portions - have, 0.8f, now);
        }

        /// <summary>Gives an animal an illness at a severity (as the editor's debug tool does: it takes hold, then is set where wanted).</summary>
        private static AnimalCondition Ill(AnimalRecord a, IAnimalCatalog cat, string condition, float severity, double now, AnimalCause cause = AnimalCause.DirtyHabitat,
            bool diagnosed = false)
        {
            AnimalConditionData c = cat.Condition(condition);
            Assert.IsNotNull(c, condition);
            AnimalCondition k = AnimalHealth.Begin(a, c, cause, now, cat.Rules, cat.Species(a.species));
            k.severity = severity;
            k.peak = Mathf.Max(k.peak, severity);
            k.stage = c.StageAt(severity);
            k.shown = c.Stage(k.stage).visible;
            k.diagnosed = diagnosed;
            return k;
        }

        /// <summary>A good keeper's day, as a player does it (each request planned and committed; one not needed now is skipped):
        /// water, mucking out (with straw in a bedded pen), then each animal brushed, petted and (every third day) its hooves seen to.</summary>
        private static void KeepWell(AnimalBook book, AnimalSimContext ctx, AnimalHerd h, AnimalActor keeper, double now, int day, int pets = 1)
        {
            TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = h.pen.id }, keeper, now);
            TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = h.pen.id, item = h.spec.UsesBedding ? Straw : null }, keeper, now);
            foreach (AnimalRecord a in new List<AnimalRecord>(h.animals))
            {
                if (!a.Alive) continue;
                TryDo(book, ctx, Care(a, "brush"), keeper, now);
                if (day % 3 == 0) TryDo(book, ctx, Care(a, "clean_hooves"), keeper, now);
                for (int i = 0; i < pets; i++) TryDo(book, ctx, Care(a, "pet"), keeper, now + i * 4.0);
            }
        }

        private static string Conditions(AnimalRecord a)
        {
            var parts = new List<string>();
            foreach (AnimalCondition k in a.conditions) parts.Add($"{k.id} {k.severity:0}{(k.Recovering ? " (recovering)" : "")} from {k.cause}");
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private static AnimalHealthEntry LastHealth(AnimalRecord a, AnimalHealthEventKind kind, string condition = null)
        {
            for (int i = a.healthLog.Count - 1; i >= 0; i--)
            {
                AnimalHealthEntry e = a.healthLog[i];
                if (e.kind == kind && (condition == null || e.condition == condition)) return e;
            }
            return null;
        }

        // ---------------------------------------------------------------- care
        [Test]
        public void BrushingCleansAndSoothesNeedsABrushWaitsItsTurnAndWearsTheBrush()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            cow.hygiene = 40f;
            cow.comfort = 40f;

            string why = Refusal(book, ctx, Care(cow, "brush"), Actor("p1", AnimalRights.All), Start);
            StringAssert.Contains("curry comb", why, "no brush, no brushing: the window says what would do");

            AnimalActor groom = WithTools(Actor("p1", AnimalRights.All), cat, 1f, "curry_comb");
            AnimalPlan plan = AnimalCommands.Plan(book, Care(cow, "brush"), groom, Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.AreEqual(AnimalIds.Tool("curry_comb"), plan.wearKey, "the comb is worn by the job");
            Assert.AreEqual(1, plan.wearUses);
            Assert.IsFalse(plan.TakesSomething, "brushing takes nothing from the bag");
            AnimalResult res = AnimalCommands.Commit(book, plan, default, Start, ctx);
            Assert.IsTrue(res.ok, res.message);
            float gain = cow.hygiene - 40f;
            Assert.Greater(cow.hygiene, 55f, "brushing cleans its coat");
            Assert.Greater(cow.comfort, 45f, "and soothes it");
            Assert.AreEqual("brush", cow.careLog[cow.careLog.Count - 1].what, "the care history keeps it");
            AnimalDay today = cow.days[cow.days.Count - 1];
            Assert.AreEqual(1, today.cares, "and the day counts it");
            Assert.AreEqual(1, today.ownerCares, "as its owner's care");
            Assert.AreEqual(Start, cow.lastOwnerCareAt, 1e-9);

            why = Refusal(book, ctx, Care(cow, "brush"), groom, Start + 1.0);
            StringAssert.Contains("brushed", why, "not again so soon");
            Assert.IsFalse(AnimalCommands.Plan(book, Care(cow, "brush"), groom, Start + 8.0, ctx).Refused, "its turn comes again");

            // Petting needs bare hands only: nothing is asked for, nothing is worn.
            AnimalPlan pet = AnimalCommands.Plan(book, Care(cow, "pet"), Actor("p1", AnimalRights.All), Start + 8.0, ctx);
            Assert.IsFalse(pet.Refused, pet.refusal?.message);
            Assert.IsNull(pet.wearKey);

            // A worn-out comb does a poor job, and the player is told.
            AnimalRecord other = Release(book, ctx, h, "p1", "cow", "holstein", Start + 8.0, GrowthStage.Adult, 777);
            other.hygiene = 40f;
            AnimalActor worn = WithTools(Actor("p1", AnimalRights.All), cat, 0f, "curry_comb");
            AnimalResult poor = Do(book, ctx, Care(other, "brush"), worn, Start + 8.0);
            Assert.Less(other.hygiene - 40f, gain * 0.6f, "a worn tool cleans less");
            Assert.IsTrue(poor.lines.Exists(l => l.Contains("worn out")), string.Join(" | ", poor.lines));

            // A species names its own care: a hen is not brushed.
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start + 8.0);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start + 8.0);
            StringAssert.Contains("do not need that", Refusal(book, ctx, Care(hen, "brush"), groom, Start + 8.0));
        }

        [Test]
        public void TooMuchBrushingGivesLessAndDoesHarm()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "angus", Start);
            AnimalActor groom = WithTools(Actor("p1", AnimalRights.All), cat, 1f, "curry_comb");
            var hygiene = new List<float>();
            var comfort = new List<float>();
            var stress = new List<float>();
            AnimalResult last = null;
            // Every time its cooldown allows: four times in a day (it takes two in 24 hours well).
            for (int i = 0; i < 4; i++)
            {
                double t = Start + 8.0 * i;
                AnimalPlan plan = AnimalCommands.Plan(book, Care(cow, "brush"), groom, t, ctx);
                Assert.IsFalse(plan.Refused, plan.refusal?.message);
                cow.hygiene = 40f;
                cow.comfort = 40f;
                cow.stress = 10f;
                last = AnimalCommands.Commit(book, plan, default, t, ctx);
                Assert.IsTrue(last.ok, last.message);
                hygiene.Add(cow.hygiene - 40f);
                comfort.Add(cow.comfort - 40f);
                stress.Add(cow.stress - 10f);
            }
            Assert.Less(hygiene[3], hygiene[0] * 0.8f, $"too much of it gives less ({hygiene[0]:0.0} then {hygiene[3]:0.0})");
            Assert.Less(comfort[3], comfort[0] - 5f, $"and chafes ({comfort[0]:0.0} then {comfort[3]:0.0})");
            Assert.Greater(stress[3], stress[0] + 1f, $"and annoys it ({stress[0]:0.0} then {stress[3]:0.0})");
            Assert.IsTrue(last.lines.Contains("It has had enough of that for now."), string.Join(" | ", last.lines));
            Assert.AreEqual(hygiene[0], hygiene[1], 1e-3, "twice a day is no harm");
        }

        [Test]
        public void WashingARabbitOrWashingInTheColdChillsIt()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext mild = Ctx(cat, climate: new FixedClimate(15f));
            var book = new AnimalBook(cat, "test");
            AnimalHerd hutch = Pen(book, "hutch", AnimalHabitatKind.Hutch, 6f, Start);
            AnimalRecord rabbit = Release(book, mild, hutch, "p1", "rabbit", "dutch", Start);
            AnimalActor keeper = Actor("p1", AnimalRights.All, water: 20f);
            float comfort = rabbit.comfort, stress = rabbit.stress;
            AnimalResult res = Do(book, mild, Care(rabbit, "wash"), keeper, Start);
            Assert.Less(rabbit.comfort, comfort - 15f, "a bathed rabbit is miserable");
            Assert.Greater(rabbit.stress, stress + 15f, "and frightened");
            Assert.IsTrue(res.lines.Contains("This does it no good."), string.Join(" | ", res.lines));
            Assert.IsTrue(res.lines.Contains("It shivers: it is too cold to be wet."), string.Join(" | ", res.lines));
            Assert.Greater(rabbit.Exposure(Infection)?.value ?? 0f, 0f, "a wet rabbit takes a chill (it builds toward an infection)");

            // A cow washed on a freezing day is chilled; on a warm day it is refreshed.
            AnimalSimContext cold = Ctx(cat, climate: new FixedClimate(1f));
            AnimalSimContext warm = Ctx(cat, climate: new FixedClimate(20f));
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, mild, field, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, mild, field, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            float ca = a.comfort, cb = b.comfort;
            AnimalResult chilled = Do(book, cold, Care(a, "wash"), keeper, Start);
            AnimalResult fresh = Do(book, warm, Care(b, "wash"), keeper, Start);
            Assert.Less(a.comfort, ca - 10f, "washed in the cold");
            Assert.Greater(b.comfort, cb, "washed in the warm");
            Assert.IsTrue(chilled.lines.Contains("It shivers: it is too cold to be wet."), string.Join(" | ", chilled.lines));
            Assert.IsFalse(fresh.lines.Contains("It shivers: it is too cold to be wet."));
            Assert.Greater(a.Exposure(Infection)?.value ?? 0f, 0f, "the cold wash builds toward an infection");
            Assert.IsNull(b.Exposure(Infection), "the warm one does not");
        }

        [Test]
        public void AWashNeedsItsWaterAndSoapIsOptional()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat, climate: new FixedClimate(20f));
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord c = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            float litres = AnimalCare.WaterFor(cat.Species("cow").CareAction("wash"), cat.Species("cow"), a);
            Assert.That(litres, Is.InRange(6f, 12f), "a cow takes a bucket of water");

            StringAssert.Contains("of water", Refusal(book, ctx, Care(a, "wash"), Actor("p1", AnimalRights.All, water: litres * 0.2f), Start), "too little water carried");
            AnimalPlan plan = AnimalCommands.Plan(book, Care(a, "wash"), Actor("p1", AnimalRights.All, water: 50f), Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.AreEqual(litres, plan.takeWater, 1e-3, "it takes its own water, no more");
            float hygiene = a.hygiene;
            AnimalResult dry = AnimalCommands.Commit(book, plan, new AnimalTaken { litres = litres * 0.2f }, Start, ctx);
            Assert.IsFalse(dry.ok, "the water was poured away meanwhile: nothing is done");
            Assert.AreEqual(hygiene, a.hygiene, 1e-4);

            // The same wash with soap cleans better; soap that did not come is not missed.
            string soap = "craftstock:Supply:soap";
            var soapy = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [soap] = 2 }, 50f);
            a.hygiene = b.hygiene = 30f;
            AnimalPlan withSoap = AnimalCommands.Plan(book, Care(a, "wash", soap), soapy, Start, ctx);
            Assert.IsFalse(withSoap.Refused, withSoap.refusal?.message);
            Assert.AreEqual(soap, withSoap.takeItem);
            Assert.AreEqual(1, withSoap.takeCount);
            AnimalTaken all = All(withSoap);
            Assert.IsTrue(AnimalCommands.Commit(book, withSoap, all, Start, ctx).ok);
            AnimalPlan noSoap = AnimalCommands.Plan(book, Care(b, "wash", soap), soapy, Start, ctx);
            AnimalTaken waterOnly = All(noSoap);
            waterOnly.count = 0;
            waterOnly.qualities.Clear();
            AnimalResult plain = AnimalCommands.Commit(book, noSoap, waterOnly, Start, ctx);
            Assert.IsTrue(plain.ok, "a wash whose soap did not come is done without it");
            Assert.Greater(a.hygiene, b.hygiene + 5f, "soap cleans better");
            // Half the water (more than the least it takes) washes half as well.
            c.hygiene = 30f;
            AnimalPlan half = AnimalCommands.Plan(book, Care(c, "wash"), Actor("p1", AnimalRights.All, water: 50f), Start, ctx);
            Assert.IsTrue(AnimalCommands.Commit(book, half, new AnimalTaken { litres = half.takeWater * 0.5f }, Start, ctx).ok, "a wash with half its water is still done");
            Assert.Less(c.hygiene, b.hygiene - 5f, $"but cleans less ({c.hygiene - 30f:0} against {b.hygiene - 30f:0})");
            StringAssert.Contains("not used for this", Refusal(book, ctx, Care(a, "brush", soap), WithTools(soapy, cat, 1f, "curry_comb"), Start + 9.0));
        }

        [Test]
        public void ABondIsNotBoughtByPettingAllDay()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "jersey", Start);
            AnimalActor owner = Actor("p1", AnimalRights.All);
            AnimalActor friend = Actor("friend", AnimalRights.All);
            Do(book, ctx, Care(cow, "pet"), friend, Start);
            Assert.AreEqual(0f, cow.affinity, 1e-4, "its bond is with its owner, not with whoever pets it");
            float before = cow.affinity;
            for (int i = 1; i <= 12; i++) TryDo(book, ctx, Care(cow, "pet"), owner, Start + i);
            Assert.LessOrEqual(cow.affinity - before, rules.affinityPerDay + 1e-3, "care gives at most the day's affinity");
            Assert.Greater(cow.affinity - before, rules.affinityPerDay * 0.5f, "but it does give some");
            Assert.AreEqual(cow.affinity - before, cow.days[cow.days.Count - 1].affinityGained, 1e-3, "the day keeps count");
        }

        [Test]
        public void ABondIsNotBoughtByHandFeedingAllDay()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            string hay = AnimalIds.Feed("hay");
            AnimalActor owner = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [hay] = 1000 });
            var meal = new AnimalRequest { kind = AnimalActionKind.Feed, pen = "barn", animal = cow.id, item = hay, count = 1 };
            float before = cow.affinity;
            int fed = 0;
            // A meal from its owner's hand every time it may have one, all day long (it eats each in a moment).
            for (double t = Start; t < Start + 24.0; t += rules.handFeedCooldownHours)
                if (TryDo(book, ctx, meal, owner, t).ok) fed++;
            Assert.Greater(fed, 24, "it was fed by hand all day");
            Assert.LessOrEqual(cow.affinity - before, rules.affinityPerDay + 1e-3, "meals from its hand count within the day's affinity, as care does");
            Assert.Greater(cow.affinity - before, 0f, "but they do give some");
            Assert.AreEqual(cow.affinity - before, cow.days[cow.days.Count - 1].affinityGained, 1e-3, "the day keeps count");
            Assert.Greater(cow.lastOwnerCareAt, Start + 20.0, "each meal is still its owner's care (its bond does not fade)");
        }

        [Test]
        public void CareForAWholePenIsDoneToEveryAnimalThatCanTakeIt()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 30f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "sheep", "merino", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, ctx, h, "p1", "sheep", "merino", Start, GrowthStage.Adult, 2);
            AnimalRecord c = Release(book, ctx, h, "p1", "sheep", "merino", Start, GrowthStage.Adult, 3);
            AnimalRecord theirs = Release(book, ctx, h, "p2", "sheep", "suffolk", Start, GrowthStage.Adult, 4);
            AnimalActor groom = WithTools(Actor("p1", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep), cat, 1f, "curry_comb");
            Do(book, ctx, Care(a, "brush"), groom, Start);

            var all = new AnimalRequest { kind = AnimalActionKind.Care, pen = "barn", text = "brush" };
            AnimalPlan plan = AnimalCommands.Plan(book, all, groom, Start + 1.0, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            AnimalResult res = AnimalCommands.Commit(book, plan, All(plan), Start + 1.0, ctx);
            Assert.IsTrue(res.ok, res.message);
            Assert.AreEqual("You brush 2 animals (1 was brushed lately).", res.message);
            Assert.AreEqual(2, plan.wearUses, "the comb wears once for each animal done");
            Assert.IsNotNull(AnimalCare.FindMark(b, "brush"));
            Assert.IsNotNull(AnimalCare.FindMark(c, "brush"));
            Assert.IsNull(AnimalCare.FindMark(theirs, "brush"), "another player's sheep is not theirs to care for");
            Assert.IsTrue(res.lines.Exists(l => l.Contains("not yours")), string.Join(" | ", res.lines));

            // A drench for the whole pen: the doses of every animal of theirs, and each gets it.
            string vermifuge = "potion:vet_vermifuge";
            AnimalActor medic = Actor("p1", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep, new Dictionary<string, int> { [vermifuge] = 10, ["potion:healing_salve"] = 10 });
            AnimalPlan dose = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", item = vermifuge }, medic, Start + 1.0, ctx);
            Assert.IsFalse(dose.Refused, dose.refusal?.message);
            AnimalTreatmentData tr = cat.TreatmentOfItem(vermifuge);
            float doses = AnimalHealth.DosesFor(cat.Species("sheep"), a) + AnimalHealth.DosesFor(cat.Species("sheep"), b) + AnimalHealth.DosesFor(cat.Species("sheep"), c);
            Assert.AreEqual(AnimalHealth.ItemsFor(tr, doses), dose.takeCount, "the pen's doses");
            Assert.IsTrue(AnimalCommands.Commit(book, dose, All(dose), Start + 1.0, ctx).ok);
            Assert.IsNotNull(a.LastDose(tr.id));
            Assert.IsNotNull(c.LastDose(tr.id));
            Assert.IsNull(theirs.LastDose(tr.id));
            StringAssert.Contains("one animal at a time",
                Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", item = "potion:healing_salve" }, medic, Start + 1.0), "a salve is not given to a herd");

            // Washing the pen: each its own water while it lasts; one it would not reach is left.
            AnimalCareActionSpec wash = cat.Species("sheep").CareAction("wash");
            float wa = AnimalCare.WaterFor(wash, cat.Species("sheep"), a), wb = AnimalCare.WaterFor(wash, cat.Species("sheep"), b), wc = AnimalCare.WaterFor(wash, cat.Species("sheep"), c);
            AnimalActor bucket = Actor("p1", AnimalRights.Look | AnimalRights.Tend | AnimalRights.Keep, water: wa + wb + wc * 0.2f);
            AnimalPlan washing = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Care, pen = "barn", text = "wash" }, bucket, Start + 2.0, ctx);
            Assert.IsFalse(washing.Refused, washing.refusal?.message);
            Assert.AreEqual(wa + wb, washing.takeWater, 1e-3, "the water of the two it reaches");
            AnimalResult washed = AnimalCommands.Commit(book, washing, All(washing), Start + 2.0, ctx);
            Assert.IsTrue(washed.ok, washed.message);
            StringAssert.StartsWith("You wash 2 animals", washed.message);
            Assert.IsTrue(washed.lines.Exists(l => l.Contains("not enough water")), string.Join(" | ", washed.lines));
            Assert.IsNull(AnimalCare.FindMark(c, "wash"), "the third was not washed");
        }

        [Test]
        public void AnAnimalWithNoNameIsTheCowInTheMiddleOfASentence()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat, climate: new FixedClimate(15f));
            ctx.vetAvailable = true;
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 400f, Start);
            AnimalHerd hutch = Pen(book, "hutch", AnimalHabitatKind.Hutch, 20f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord sheep = Release(book, ctx, h, "p1", "sheep", "merino", Start, GrowthStage.Adult, 2);
            AnimalRecord rabbit = Release(book, ctx, hutch, "p1", "rabbit", "dutch", Start, GrowthStage.Adult, 3);
            Assert.IsTrue(string.IsNullOrEmpty(cow.name), "let out with no name, as most animals are");
            string vinegar = "ingredient:vinegar";
            AnimalActor keeper = WithTools(Actor("p1", AnimalRights.All, new Dictionary<string, int> { [vinegar] = 5 }, 200f), cat, 1f, "soft_brush", "curry_comb");
            keeper.vetFee = (a, sp) => 40L;

            Assert.AreEqual("You look the cow over.", Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "pen", animal = cow.id }, keeper, Start).message);
            AnimalResult vet = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.CallVet, pen = "pen", animal = cow.id, expected = 40 }, keeper, Start);
            Assert.AreEqual("The veterinarian sees to the cow.", vet.message);
            Assert.AreEqual("The veterinarian examines the cow.", vet.lines[0]);
            Assert.AreEqual("You put the vinegar wash on the cow.", Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = vinegar }, keeper, Start).message,
                "a wash is put on it; a draught it takes");
            // A care message names it wherever it falls, and starts with a capital.
            Assert.AreEqual("The cow leans into the comb and sighs.", Do(book, ctx, Care(cow, "brush"), keeper, Start).message);
            Assert.AreEqual("You work the brush through the sheep's fleece.", Do(book, ctx, Care(sheep, "brush"), keeper, Start).message);
            Assert.AreEqual("Rabbits are not bathed: the rabbit panics.", Do(book, ctx, Care(rabbit, "wash"), keeper, Start).message);

            // Named, it goes by its name everywhere; its history says what it was called before, and its remains are cleared by it.
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Rename, pen = "pen", animal = cow.id, text = "Daisy" }, keeper, Start);
            Assert.AreEqual("Named Daisy (was the cow).", cow.events.FindLast(e => e.kind == AnimalEventKind.Renamed).text);
            AnimalSim.Die(cow, AnimalDeathCause.OldAge, Start, ctx, cat.Species("cow"));
            AnimalSim.Die(sheep, AnimalDeathCause.OldAge, Start, ctx, cat.Species("sheep"));
            Assert.AreEqual("The remains of Daisy are cleared away.", Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.RemoveRemains, pen = "pen", animal = cow.id }, keeper, Start).message);
            Assert.AreEqual("The remains of the sheep are cleared away.", Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.RemoveRemains, pen = "pen", animal = sheep.id }, keeper, Start).message);
        }

        /// <summary>Where a text has half of a two-part character (a surrogate) standing alone (-1: nowhere).</summary>
        private static int LoneHalf(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && (i + 1 == s.Length || !char.IsLowSurrogate(s[i + 1]))) return i;
                if (char.IsLowSurrogate(s[i]) && (i == 0 || !char.IsHighSurrogate(s[i - 1]))) return i;
            }
            return -1;
        }

        /// <summary>A text as the codes of its characters (half a character cannot be printed on its own).</summary>
        private static string Codes(string s)
        {
            var parts = new List<string>();
            foreach (char c in s ?? "") parts.Add(((int)c).ToString("X4"));
            return string.Join(" ", parts);
        }

        [Test]
        public void ANameCutAtItsLengthNeverEndsInHalfACharacter()
        {
            AnimalRules rules = Rules();
            int max = rules.maxNameLength;
            const string emoji = "\U0001F404"; // one character written in two halves (a surrogate pair)
            Assert.AreEqual(2, emoji.Length);

            // A name whose last character the cap would cut in two loses that character whole.
            string straddling = new string('A', max - 1) + emoji;
            string cut = AnimalCommands.CleanName(straddling, max);
            Assert.AreEqual(-1, LoneHalf(cut), $"cut at {max}, it ends in half a character: {Codes(cut)}");
            Assert.AreEqual(new string('A', max - 1), cut);
            // A name that fits is kept as it is, one whose last character ends right at the cap too.
            string fits = "Daisy " + emoji, atCap = new string('A', max - 2) + emoji;
            Assert.AreEqual(fits, AnimalCommands.CleanName(fits, max));
            Assert.AreEqual(atCap, AnimalCommands.CleanName(atCap, max));

            // Named so by its owner (planned and committed, as the server does it), it keeps a whole name, and so does its history.
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalActor owner = Actor("p1", AnimalRights.All);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Rename, pen = "pen", animal = cow.id, text = straddling }, owner, Start);
            Assert.AreEqual(-1, LoneHalf(cow.name), Codes(cow.name));
            Assert.AreEqual(cut, cow.name);
            string named = cow.events.FindLast(e => e.kind == AnimalEventKind.Renamed).text;
            Assert.AreEqual(-1, LoneHalf(named), Codes(named));
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Rename, pen = "pen", animal = cow.id, text = fits }, owner, Start);
            Assert.AreEqual(fits, cow.name, "a name with an emoji that fits keeps it");
        }

        /// <summary>A name as the codes of its characters (see <see cref="Codes"/>), or "null" for no name.</summary>
        private static string CodesOrNull(string s) => s == null ? "null" : Codes(s);

        [Test]
        public void ANameIsCutOnlyWhereAWholeCharacterEndsAndLoneHalvesAndLeadingSpacesAreDropped()
        {
            int max = Rules().maxNameLength;
            const string cow = "\U0001F404";
            const string family = "\U0001F468\u200D\U0001F469\u200D\U0001F467"; // a man, a woman and a girl joined by zero-width joiners
            const string france = "\U0001F1EB\U0001F1F7", germany = "\U0001F1E9\U0001F1EA"; // flags: two regional letters each
            const string thumbsUp = "\U0001F44D\U0001F3FD"; // with a skin tone
            const string heart = "\u2764\uFE0F"; // with a variation selector
            const string accented = "e\u0301"; // e and a combining acute accent
            const string england = "\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F"; // a flag written with tags
            var cases = new List<(string what, string text, string expected)>
            {
                ("a lone low half at the start", "\uDC04Daisy", "Daisy"),
                ("a lone high half in the middle", "Dai\uD83Dsy", "Daisy"),
                ("a lone low half after a whole character", cow + "\uDC04", cow),
                ("nothing but a lone half", "\uDC04", null),
                ("20 spaces before the name", new string(' ', 20) + "Daisy", "Daisy"),
            };
            // A character of several parts that the cap falls in is dropped whole (no part of it is left), wherever in it the cap
            // falls; one that ends at the cap or before is kept whole. Of two flags, the first is kept when it ends by the cap.
            var joined = new (string what, string[] parts)[]
            {
                ("a cow (two halves)", new[] { cow }),
                ("a family (joined by zero-width joiners)", new[] { family }),
                ("two flags", new[] { france, germany }),
                ("a thumbs-up with a skin tone", new[] { thumbsUp }),
                ("a heart with a variation selector", new[] { heart }),
                ("e with a combining accent", new[] { accented }),
                ("a flag written with tags", new[] { england }),
            };
            foreach (var j in joined)
            {
                string whole = string.Concat(j.parts);
                for (int before = Math.Max(0, max - whole.Length - 1); before < max; before++)
                {
                    string expected = new string('A', before);
                    foreach (string part in j.parts)
                    {
                        if (expected.Length + part.Length > max) break;
                        expected += part;
                    }
                    cases.Add(($"{before} letters, then {j.what}", new string('A', before) + whole, expected));
                }
            }
            // Printed as character codes: half a character is never printed on its own.
            var wrong = new List<string>();
            foreach (var c in cases)
            {
                string got = AnimalCommands.CleanName(c.text, max);
                if (got != c.expected) wrong.Add($"{c.what}: {CodesOrNull(got)}, not {CodesOrNull(c.expected)}");
            }
            Assert.IsEmpty(wrong, $"{wrong.Count} of {cases.Count} names cut at {max}: " + string.Join("; ", wrong));
            // A player's name as the animals keep it is cut the same way, at its own length.
            string player = new string('A', AnimalToolkit.MaxNameLength - 3) + family;
            Assert.AreEqual(Codes(new string('A', AnimalToolkit.MaxNameLength - 3)), Codes(AnimalToolkit.ShownName(player)));
        }

        [Test]
        public void ACoatSettlesWhereItsPenAllowsAndCareLiftsItAboveThat()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat, climate: new FixedClimate(15f));
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 2000f, 0.8f, Start);
            AnimalSim.AdvanceHerd(h, Start + 24 * 5, ctx);
            float rest = cow.hygiene;
            Assert.That(rest, Is.InRange(45f, 60f), "never groomed, in a clean pen, it keeps itself fairly clean");
            Assert.Greater(rest, 40f, "above where a dirty coat makes it ill");

            AnimalActor keeper = WithTools(Actor("p1", AnimalRights.All, water: 50f), cat, 1f, "curry_comb");
            double t = Start + 24 * 5;
            Do(book, ctx, Care(cow, "brush"), keeper, t);
            Do(book, ctx, Care(cow, "wash"), keeper, t);
            Assert.Greater(cow.hygiene, 85f, "care lifts it well above that");
            AnimalSim.AdvanceHerd(h, t + 24.0, ctx);
            Assert.Greater(cow.hygiene, rest + 15f, "it stays clean for a while");
            AnimalSim.AdvanceHerd(h, t + 24 * 6, ctx);
            Assert.Less(cow.hygiene, rest + 5f, "and settles back over days");

            // A filthy floor keeps it filthy.
            AnimalHerd barn = Pen(book, "barn", AnimalHabitatKind.Barn, 16f, t);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Move, pen = "barn", animal = cow.id }, Actor("p1", AnimalRights.All), t + 24 * 6);
            Stock(cat, barn, "hay", 2000f, 0.8f, t + 24 * 6);
            for (int hr = 1; hr <= 72; hr++)
            {
                barn.pen.dirt = 100f;
                AnimalSim.AdvanceHerd(barn, t + 24 * 6 + hr, ctx);
            }
            Assert.Less(cow.hygiene, 40f, "a filthy pen makes a filthy coat");
            float[] causes = AnimalHealth.NewCauses();
            AnimalStepFacts f = AnimalHabitat.Facts(barn, barn.spec, cat, cat.Rules, t + 24 * 9, null, null);
            AnimalHealth.Causes(cow, cat.Species("cow"), cat, cat.Rules, in f, ctx.climate.WeatherAt(t), causes);
            Assert.Greater(causes[(int)AnimalCause.LowHygiene], 0f, "and a dirty coat is a cause of illness");
            Assert.Greater(causes[(int)AnimalCause.DirtyHabitat], 0.9f);
        }

        [Test]
        public void ShelterKeepsAnimalsComfortableInColdRainAndAKeeperCanSeeWhatBothersThem()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat, climate: new FixedClimate(-10f, rain: 3f, wind: 8f));
            var book = new AnimalBook(cat, "test");
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd barn = Pen(book, "barn", AnimalHabitatKind.Barn, 48f, Start);
            AnimalRecord outside = Release(book, ctx, field, "p1", "goat", "nubian", Start, GrowthStage.Adult, 5);
            AnimalRecord inside = Release(book, ctx, barn, "p1", "goat", "nubian", Start, GrowthStage.Adult, 5);
            Stock(cat, field, "hay", 500f, 0.8f, Start);
            Stock(cat, barn, "hay", 500f, 0.8f, Start);
            AnimalSim.AdvanceHerd(field, Start + 12.0, ctx);
            AnimalSim.AdvanceHerd(barn, Start + 12.0, ctx);
            Assert.Less(outside.comfort, inside.comfort - 15f, $"out in the cold rain ({outside.comfort:0}) against in the barn ({inside.comfort:0})");
            Assert.AreEqual(AnimalDiscomfort.Cold, outside.discomfort, "what bothers it is the cold");
            var signs = new List<string>();
            AnimalObservation.Signs(outside, cat.Species("goat"), cat, signs);
            Assert.IsTrue(signs.Contains("It huddles against the cold."), string.Join(" | ", signs));
            AnimalObservation.Signs(inside, cat.Species("goat"), cat, signs);
            Assert.IsFalse(signs.Contains("It huddles against the cold."), string.Join(" | ", signs));
            Assert.Greater(outside.Exposure(Infection)?.value ?? 0f, inside.Exposure(Infection)?.value ?? 0f, "cold and wet builds toward an infection");

            AnimalPenView open = AnimalObservation.PenView(book, field, "p1", AnimalRights.All, Start + 12.0, null, false, AnimalObservation.WeatherWord(ctx, Start + 12.0));
            AnimalPenView roofed = AnimalObservation.PenView(book, barn, "p1", AnimalRights.All, Start + 12.0);
            Assert.AreEqual("Open to the weather.", open.shelter);
            Assert.AreEqual("Roofed and sheltered.", roofed.shelter);
            Assert.AreEqual("It is cold and wet.", open.weather);
        }

        [Test]
        public void AnAnimalKeptAloneMissesItsKind()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat, climate: new FixedClimate(15f));
            var book = new AnimalBook(cat, "test");
            AnimalHerd lone = Pen(book, "lone", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd flock = Pen(book, "flock", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord alone = Release(book, ctx, lone, "p1", "sheep", "merino", Start, GrowthStage.Adult, 1);
            AnimalRecord with = Release(book, ctx, flock, "p1", "sheep", "merino", Start, GrowthStage.Adult, 1);
            Release(book, ctx, flock, "p1", "sheep", "merino", Start, GrowthStage.Adult, 2);
            Release(book, ctx, flock, "p1", "sheep", "merino", Start, GrowthStage.Adult, 3);
            Stock(cat, lone, "hay", 500f, 0.8f, Start);
            Stock(cat, flock, "hay", 1500f, 0.8f, Start);
            AnimalSim.AdvanceHerd(lone, Start + 48.0, ctx);
            AnimalSim.AdvanceHerd(flock, Start + 48.0, ctx);
            Assert.AreEqual(AnimalDiscomfort.Loneliness, alone.stressor, "a lone sheep calls for its flock");
            Assert.AreNotEqual(AnimalDiscomfort.Loneliness, with.stressor);
            Assert.Greater(alone.stress, with.stress + 5f, $"and is the more stressed for it ({alone.stress:0} against {with.stress:0})");
            Assert.Greater(alone.Exposure("chronic_stress")?.value ?? 0f, 0f, "loneliness builds toward chronic stress");
            Assert.AreEqual(0f, AnimalCare.Loneliness(with, cat.Species("sheep"), AnimalHabitat.Facts(flock, flock.spec, cat, cat.Rules, Start + 48.0, null, new Dictionary<string, int>())), 1e-4);
        }

        [Test]
        public void ABoardedAnimalIsKeptWellAndItsOwnerSeesToItsHealthFromAnyPen()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 100f, Start);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            var boarded = new List<AnimalRecord>();
            foreach (string species in new[] { "cow", "sheep", "goat", "horse", "pig" })
            {
                AnimalRecord a = Release(book, ctx, h, "p1", species, "", Start);
                a.hooves = 40f;
                AnimalCommands.Board(book, a, Start, ctx, "the lease ended");
                Assert.AreEqual(AnimalStatus.Boarded, a.status);
                boarded.Add(a);
            }
            book.Advance(Start + 24 * 40, ctx, null, int.MaxValue);
            foreach (AnimalRecord a in boarded)
            {
                Assert.AreEqual(AnimalStatus.Boarded, a.status, a.species);
                Assert.AreEqual("none", Conditions(a), $"{a.species} at the boarding stable stays well");
                Assert.AreEqual(cat.Rules.boardingHooves, a.hooves, 5f, $"{a.species}: the stable trims its hooves");
                Assert.AreEqual(cat.Rules.boardingHygiene, a.hygiene, 5f, $"{a.species}: and keeps it clean");
                Assert.Greater(a.health, 80f, a.species);
            }

            // Its owner looks it over, doses it and reads its history from any pen's window, but does not groom it there.
            AnimalRecord cow = boarded[0];
            double now = Start + 24 * 40;
            AnimalActor owner = WithTools(Actor("p1", AnimalRights.Look, new Dictionary<string, int> { ["potion:healing_draught"] = 5 }), cat, 1f, "curry_comb");
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "coop", animal = cow.id }, owner, now);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "coop", animal = cow.id, item = "potion:healing_draught" }, owner, now);
            AnimalPlan history = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.History, pen = "coop", animal = cow.id }, owner, now, ctx);
            Assert.IsFalse(history.Refused, history.refusal?.message);
            Assert.IsNotNull(history.answer);
            StringAssert.Contains("boarding stable", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Care, pen = "coop", animal = cow.id, text = "brush" }, owner, now));
            StringAssert.Contains("not here", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "coop", animal = cow.id }, Actor("p2", AnimalRights.All), now),
                "another player's boarded animal is none of theirs");
            AnimalPenView view = AnimalObservation.PenView(book, coop, "p1", AnimalRights.Look, now);
            AnimalView seen = view.elsewhere.Find(v => v.id == cow.id);
            Assert.IsNotNull(seen, "its owner sees it from the pen's window");
            Assert.IsEmpty(seen.care, "with no grooming to do there");
        }

        [Test]
        public void ItsOwnerIsToldWhenAnAnimalIsInABadWayAtMostOnceADay()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            h.pen.water = 0f;
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalSim.AdvanceHerd(h, Start + 200.0, ctx);
            Assert.AreEqual(AnimalStatus.Dead, a.status);
            var told = a.events.FindAll(e => e.kind == AnimalEventKind.NeedsHelp);
            Assert.IsNotEmpty(told, "its owner is told it needs help before it dies");
            for (int i = 1; i < told.Count; i++) Assert.GreaterOrEqual(told[i].at - told[i - 1].at, cat.Rules.needsHelpEveryHours - 1e-6, "not every hour");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.NeedsHelp && n.animal == a.id && n.text.Contains("bad way")));
            AnimalNotice ill = ctx.notices.Find(n => n.kind == AnimalEventKind.FellIll && n.animal == a.id);
            Assert.IsNotNull(ill.text, "its thirst shows as an illness");
            StringAssert.DoesNotContain("ehydration", ill.text, "the owner is told a symptom, not a name");
        }

        [Test]
        public void AnOwnerWhoComesBackIsToldAtOnceThatTheirAnimalIsInABadWay()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            double back = Start + 72.3;
            ctx.ownerPresent = (key, t) => t >= back;
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 100f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 3);
            Stock(cat, h, "hay", 4000f, 0.8f, Start);
            Ill(cow, cat, Infection, 75f, Start);
            // Held at the away floor while its owner is logged out, then they log in: when are they told?
            var told = new List<double>();
            for (int hr = 1; hr <= 24 * 6 && cow.Alive; hr++)
            {
                ctx.notices.Clear();
                book.BringUpToDate(h, Start + hr, ctx);
                foreach (AnimalNotice n in ctx.notices)
                    if (n.kind == AnimalEventKind.NeedsHelp && n.animal == cow.id) told.Add(Start + hr);
            }
            Assert.IsNotEmpty(told, "its owner is told it is in a bad way");
            Assert.GreaterOrEqual(told[0], back, $"nothing is raised while they are away to hear it (it would hold back the next warning a day; raised at {told[0] - Start:0} h)");
            Assert.Less(told[0] - back, 1.0, $"the first step after they are back tells them (back at {back - Start:0.0} h, told at {told[0] - Start:0} h)");
        }

        [Test]
        public void ALongSpellInABadWayIsToldEveryDayButKeptOnceInItsHistory()
        {
            AnimalRules rules = Rules();
            // Every band from 100: its welfare always reads Critical, so it is in a bad way all along.
            rules.welfare.excellentFrom = rules.welfare.goodFrom = rules.welfare.averageFrom = rules.welfare.poorFrom = 100f;
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 60f, Start, grass: 1f);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 3000f, 0.8f, Start);
            var reminded = new List<double>();
            for (int hr = 1; hr <= 24 * 6; hr++)
            {
                ctx.notices.Clear();
                AnimalSim.AdvanceHerd(h, Start + hr, ctx);
                if (ctx.notices.Exists(n => n.kind == AnimalEventKind.NeedsHelp && n.animal == cow.id)) reminded.Add(Start + hr);
            }
            Assert.GreaterOrEqual(reminded.Count, 5, "its owner is reminded every day");
            for (int i = 1; i < reminded.Count; i++) Assert.GreaterOrEqual(reminded[i] - reminded[i - 1], rules.needsHelpEveryHours - 1e-6, "but not every hour");
            Assert.AreEqual(1, cow.events.FindAll(e => e.kind == AnimalEventKind.NeedsHelp).Count, "its history keeps the spell once, so the reminders do not push the rest out");
            Assert.IsTrue(cow.events.Exists(e => e.kind == AnimalEventKind.Acquired));
        }

        [Test]
        public void AnAnimalInABadWayFromLowHealthShowsItNeedsHelp()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalSpeciesData sp = cat.Species("cow");
            var signs = new List<string>();
            // Below Needs Help Below Health it is in a bad way (its owner is told so), though its welfare is only Poor.
            cow.health = rules.needsHelpBelowHealth - 5f;
            cow.welfare = 48f;
            Assert.AreEqual(AnimalWelfareBand.Poor, AnimalWelfare.BandOf(cow.welfare, rules.welfare));
            AnimalObservation.Signs(cow, sp, cat, signs);
            Assert.IsTrue(signs.Contains("It needs help."), string.Join(" | ", signs));
            cow.health = 80f;
            AnimalObservation.Signs(cow, sp, cat, signs);
            Assert.IsFalse(signs.Contains("It needs help."), "Poor welfare alone is not a bad way: " + string.Join(" | ", signs));
            cow.welfare = 20f;
            AnimalObservation.Signs(cow, sp, cat, signs);
            Assert.IsTrue(signs.Contains("It needs help."), "Critical welfare is: " + string.Join(" | ", signs));
            AnimalSim.Die(cow, AnimalDeathCause.Starvation, Start, ctx, sp);
            AnimalObservation.Signs(cow, sp, cat, signs);
            Assert.IsFalse(signs.Contains("It needs help."), "a dead animal is past help");
        }

        // ---------------------------------------------------------------- welfare
        [Test]
        public void WelfareFollowsTheWeightedFormulaAndItsBands()
        {
            var s = new AnimalWelfareSpec();
            var a = new AnimalRecord { health = 80f, nutrition = 70f, hygiene = 60f, comfort = 50f, happiness = 40f, affinity = 30f, stress = 20f };
            float reference = (0.25f * 80f + 0.2f * 70f + 0.15f * 60f + 0.15f * 50f + 0.15f * 40f + 0.1f * 30f) / 1f;
            Assert.AreEqual(reference, AnimalWelfare.Compute(a, s), 1e-3, "the reference formula, with the default weights");
            a.stress = 60f;
            Assert.AreEqual(reference - s.stressPenalty * (60f - s.calmStress), AnimalWelfare.Compute(a, s), 1e-3, "stress above calm costs welfare");
            a.stress = 20f;
            a.health = 10f;
            Assert.LessOrEqual(AnimalWelfare.Compute(a, s), 10f + s.healthCap + 1e-3, "a dying animal is never Average");
            a.health = 80f;
            s.affinity = 0.5f;
            Assert.AreNotEqual(reference, AnimalWelfare.Compute(a, s), "the weights are settings");
            Assert.AreEqual((0.25f * 80f + 0.2f * 70f + 0.15f * 60f + 0.15f * 50f + 0.15f * 40f + 0.5f * 30f) / 1.4f, AnimalWelfare.Compute(a, s), 1e-3, "and are normalised");

            s = new AnimalWelfareSpec();
            Assert.AreEqual(AnimalWelfareBand.Excellent, AnimalWelfare.BandOf(90f, s));
            Assert.AreEqual(AnimalWelfareBand.Good, AnimalWelfare.BandOf(89.9f, s));
            Assert.AreEqual(AnimalWelfareBand.Good, AnimalWelfare.BandOf(75f, s));
            Assert.AreEqual(AnimalWelfareBand.Average, AnimalWelfare.BandOf(74.9f, s));
            Assert.AreEqual(AnimalWelfareBand.Average, AnimalWelfare.BandOf(50f, s));
            Assert.AreEqual(AnimalWelfareBand.Poor, AnimalWelfare.BandOf(49.9f, s));
            Assert.AreEqual(AnimalWelfareBand.Poor, AnimalWelfare.BandOf(25f, s));
            Assert.AreEqual(AnimalWelfareBand.Critical, AnimalWelfare.BandOf(24.9f, s));
            Assert.AreEqual("Excellent", AnimalWelfare.Word(AnimalWelfareBand.Excellent));
            Assert.AreEqual("Critical", AnimalWelfare.Word(AnimalWelfareBand.Critical));
            Assert.Less(AnimalWelfare.Effects(AnimalWelfareBand.Excellent, s).susceptibility, AnimalWelfare.Effects(AnimalWelfareBand.Critical, s).susceptibility);
            Assert.Greater(AnimalWelfare.Effects(AnimalWelfareBand.Excellent, s).recovery, AnimalWelfare.Effects(AnimalWelfareBand.Critical, s).recovery);

            var errors = new List<string>();
            AnimalWelfare.Validate(s, errors, new List<string>());
            Assert.IsEmpty(errors, string.Join("; ", errors));
            s.goodFrom = 95f;
            AnimalWelfare.Validate(s, errors, new List<string>());
            Assert.IsNotEmpty(errors, "bands out of order are an error");
        }

        [Test]
        public void ACriticalAnimalFallsIllFasterThanAnExcellentOne()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            AnimalSpeciesData hen = cat.Species("chicken");
            var poor = new AnimalRecord { id = "a", species = "chicken", breed = "leghorn", stage = GrowthStage.Adult, welfare = 10f };
            AnimalRecord fine = poor.Clone();
            fine.id = "b";
            fine.welfare = 95f;
            Assert.AreEqual(AnimalWelfareBand.Critical, AnimalWelfare.BandOf(poor.welfare, cat.Rules.welfare));
            Assert.AreEqual(AnimalWelfareBand.Excellent, AnimalWelfare.BandOf(fine.welfare, cat.Rules.welfare));
            // The same cause (a dirty pen), lived through hour by hour.
            float[] causes = AnimalHealth.NewCauses();
            causes[(int)AnimalCause.DirtyHabitat] = 0.6f;
            var facts = new AnimalStepFacts();
            double ill = -1.0, well = -1.0;
            float poorAt12 = 0f, fineAt12 = 0f;
            for (int hr = 0; hr < 240; hr++)
            {
                double t = Start + hr;
                AnimalHealth.Step(poor, hen, ctx, in facts, causes, t, t + 1.0, 1f, AnimalWelfare.EffectsOf(poor, cat.Rules), null);
                AnimalHealth.Step(fine, hen, ctx, in facts, causes, t, t + 1.0, 1f, AnimalWelfare.EffectsOf(fine, cat.Rules), null);
                if (hr == 11)
                {
                    poorAt12 = poor.Exposure(Infection)?.value ?? 0f;
                    fineAt12 = fine.Exposure(Infection)?.value ?? 0f;
                }
                if (ill < 0.0 && poor.Condition(Infection) != null) ill = t + 1.0;
                if (well < 0.0 && fine.Condition(Infection) != null) well = t + 1.0;
            }
            Assert.Greater(poorAt12, fineAt12 * 2f, $"it builds up faster in a neglected animal ({poorAt12:0.0} against {fineAt12:0.0} in 12 hours)");
            Assert.Greater(ill, 0.0, "the neglected one falls ill");
            Assert.IsTrue(well < 0.0 || well > ill, $"the well kept one later, or not at all ({ill - Start:0} h against {well - Start:0} h)");
            Assert.AreEqual(AnimalCause.DirtyHabitat, poor.Condition(Infection).cause, "and the cause is known");
        }

        [Test]
        public void AMendingIllnessComesBackIfItsCauseReturnsElseItClearsAndLeavesImmunity()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            AnimalSpeciesData hen = cat.Species("chicken");
            AnimalConditionData data = cat.Condition(Infection);
            var facts = new AnimalStepFacts();
            AnimalWelfareBandEffects band = AnimalWelfare.Effects(AnimalWelfareBand.Average, cat.Rules.welfare);
            var outcome = new AnimalHealthOutcome();
            foreach (bool dirty in new[] { true, false })
            {
                var a = new AnimalRecord { id = dirty ? "a" : "b", species = "chicken", breed = "leghorn", stage = GrowthStage.Adult };
                AnimalCondition k = Ill(a, cat, Infection, 0f, Start);
                k.diagnosed = k.shown = true;
                k.recoveringUntil = Start + 10.0;
                float[] causes = AnimalHealth.NewCauses();
                if (dirty) causes[(int)AnimalCause.DirtyHabitat] = 1f;
                AnimalHealth.Step(a, hen, ctx, in facts, causes, Start, Start + 1.0, 1f, band, outcome);
                if (dirty)
                {
                    Assert.IsFalse(k.Recovering, "back in a filthy pen, it falls ill again");
                    Assert.AreEqual(data.startSeverity, k.severity, 1e-4, "from its start");
                    Assert.IsNotNull(LastHealth(a, AnimalHealthEventKind.Relapsed));
                    StringAssert.Contains("came back", LastHealth(a, AnimalHealthEventKind.Relapsed).text);
                    continue;
                }
                Assert.IsTrue(k.Recovering, "kept clean, it goes on mending");
                for (int hr = 1; hr < 12; hr++) AnimalHealth.Step(a, hen, ctx, in facts, causes, Start + hr, Start + hr + 1.0, 1f, band, outcome);
                Assert.IsNull(a.Condition(Infection), "and clears");
                Assert.IsNotNull(LastHealth(a, AnimalHealthEventKind.Cleared));
                Assert.AreEqual(Start + 10.0 + data.immunityHours, a.Exposure(Infection).immuneUntil, 1e-6, "cleared when its recovery ended, leaving immunity for a while");
            }
        }

        [Test]
        public void DailyCareMakesWelfareGoodAndAnOwnersAffectionExcellent()
        {
            var welfare = new List<float>();
            foreach (int pets in new[] { 0, 3 })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat);
                var book = new AnimalBook(cat, "test");
                AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start, grass: 1.5f);
                AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 700);
                Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 701);
                AnimalActor keeper = Keeper(cat);
                for (int hr = 1; hr <= 24 * 21; hr++)
                {
                    double t = Start + hr;
                    AnimalSim.AdvanceHerd(h, t, ctx);
                    if (hr % 24 != 8) continue;
                    TopUp(cat, h, "hay", 60f, t);
                    TopUp(cat, h, "alfalfa", 40f, t);
                    KeepWell(book, ctx, h, keeper, t, hr / 24, pets);
                }
                Assert.AreEqual("none", Conditions(a), "kept like this it stays well");
                welfare.Add(a.welfare);
            }
            Assert.AreEqual(AnimalWelfareBand.Good, AnimalWelfare.BandOf(welfare[0], Rules().welfare), $"daily care: welfare {welfare[0]:0.0}");
            Assert.AreEqual(AnimalWelfareBand.Excellent, AnimalWelfare.BandOf(welfare[1], Rules().welfare), $"and its owner's affection on top: welfare {welfare[1]:0.0}");
        }

        [Test]
        public void ACrowdedPenIsLessComfortableMoreStressedAndOfLowerWelfare()
        {
            var cows = new List<AnimalRecord>();
            foreach (float area in new[] { 200f, 48f })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat);
                var book = new AnimalBook(cat, "test");
                AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, area, Start);
                for (int i = 0; i < 3; i++) Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 900 + i);
                Stock(cat, h, "hay", 3000f, 0.8f, Start);
                for (int hr = 1; hr <= 72; hr++)
                {
                    // The same herd, fed, watered and kept clean: only the room differs.
                    h.pen.water = 1000f;
                    h.pen.dirt = 0f;
                    AnimalSim.AdvanceHerd(h, Start + hr, ctx);
                }
                cows.Add(h.animals[0]);
            }
            AnimalRecord roomy = cows[0], crowded = cows[1];
            Assert.AreEqual(AnimalDiscomfort.Crowding, crowded.discomfort, "what bothers it is the crowding");
            Assert.AreEqual(AnimalDiscomfort.Crowding, crowded.stressor);
            Assert.AreNotEqual(AnimalDiscomfort.Crowding, roomy.discomfort);
            Assert.Less(crowded.comfort, roomy.comfort, "it is less comfortable");
            Assert.Greater(crowded.stress, roomy.stress, "more stressed");
            Assert.Less(crowded.welfare, roomy.welfare, $"and of lower welfare ({crowded.welfare:0.0} against {roomy.welfare:0.0})");
        }

        // ---------------------------------------------------------------- illness from how it lives
        [Test]
        public void AFilthyCrowdedCoopMakesItsHensIllButOneMuckedOutEveryTwoDaysDoesNot()
        {
            foreach (bool mucked in new[] { false, true })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat);
                var book = new AnimalBook(cat, "test");
                AnimalHerd h = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
                for (int i = 0; i < 6; i++) Release(book, ctx, h, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 600 + i);
                Stock(cat, h, "chicken_feed", 2000f, 0.8f, Start);
                AnimalActor keeper = Keeper(cat);
                AnimalRecord first = null;
                int days = mucked ? 30 : 7;
                for (int hr = 1; hr <= days * 24 && first == null; hr++)
                {
                    double t = Start + hr;
                    AnimalSim.AdvanceHerd(h, t, ctx);
                    foreach (AnimalRecord a in h.animals)
                        if (first == null && a.conditions.Count > 0) first = a;
                    // Mucked out only: no fresh straw, the trough's water never changed.
                    if (mucked && hr % 48 == 0) Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "coop" }, keeper, t);
                }
                if (mucked)
                {
                    Assert.IsNull(first, $"mucked out every two days, no hen falls ill in a month ({(first != null ? Conditions(first) : "")})");
                    continue;
                }
                Assert.IsNotNull(first, "a filthy, crowded coop makes its hens ill within a week");
                AnimalCondition k = first.Condition(Infection);
                Assert.IsNotNull(k, Conditions(first));
                Assert.IsTrue(k.cause == AnimalCause.DirtyHabitat || k.cause == AnimalCause.LowHygiene, $"from what it lives in ({k.cause})");
            }
        }

        /// <summary>
        /// A crowded coop of six hens, never cleaned (fed and watered at the top of every hour), stepped by requests: each one
        /// brings the pen up to its time before anything else, a refused one too (here a passer-by's look at an animal that is
        /// not there, <paramref name="perHour"/> times a game hour). Returns the hour each illness first took hold, and for
        /// each illness the most it had built up by the end in a hen it had not taken hold of.
        /// </summary>
        private static (Dictionary<string, double> onset, Dictionary<string, float> building) CoopSteppedBy(int perHour, int hours)
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            for (int i = 0; i < 6; i++) Release(book, ctx, h, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 900 + i);
            var look = new AnimalRequest { kind = AnimalActionKind.Inspect, pen = "coop", animal = "nobody" };
            AnimalActor passerby = Actor("p2", AnimalRights.Look);
            var onset = new Dictionary<string, double>();
            for (int hr = 0; hr < hours; hr++)
            {
                TopUp(cat, h, "chicken_feed", 200f, Start + hr);
                h.pen.water = 1000f;
                for (int s = 1; s <= perHour; s++)
                {
                    double t = Start + hr + s / (double)perHour;
                    if (!AnimalCommands.Plan(book, look, passerby, t, ctx).Refused) Assert.Fail("there is no such animal to look at");
                    foreach (AnimalRecord a in h.animals)
                        foreach (AnimalCondition k in a.conditions)
                            if (!onset.ContainsKey(k.id)) onset[k.id] = t - Start;
                }
            }
            var building = new Dictionary<string, float>();
            foreach (AnimalRecord a in h.animals)
                foreach (AnimalExposure e in a.exposures)
                    building[e.condition] = Mathf.Max(building.TryGetValue(e.condition, out float v) ? v : 0f, e.value);
            return (onset, building);
        }

        [Test]
        public void ManyShortStepsAsRequestsMakeThemGiveTheSameIllnessesAtTheSameHoursAsHourlySteps()
        {
            // 200 requests a game hour (about 3 a second at the default day length, under the server's limit) step the pen
            // in slices of 1/200 h: an illness building slowly must build as it does hour by hour, not be dropped each slice.
            var hourly = CoopSteppedBy(1, 110);
            var spammed = CoopSteppedBy(200, 110);
            Assert.IsNotEmpty(hourly.onset, "the filthy, crowded coop makes its hens ill");
            CollectionAssert.AreEquivalent(hourly.onset.Keys, spammed.onset.Keys, "the same illnesses take hold");
            foreach (KeyValuePair<string, double> kv in hourly.onset)
                Assert.AreEqual(kv.Value, spammed.onset[kv.Key], 2.0, $"{kv.Key} at about the same hour");
            // The illnesses slower still, which took hold in neither run, are building up all the same.
            foreach (KeyValuePair<string, float> kv in hourly.building)
                if (kv.Value > 1f && !hourly.onset.ContainsKey(kv.Key))
                    Assert.AreEqual(kv.Value, spammed.building.TryGetValue(kv.Key, out float v) ? v : 0f, 1f, $"the build-up toward {kv.Key} is kept");
        }

        [Test]
        public void AWellKeptAnimalOfEverySpeciesStaysWellForAMonth()
        {
            var setups = new (string species, string breed, AnimalHabitatKind kind, float area, float grass, string[] foods)[]
            {
                ("chicken", "rhode_island_red", AnimalHabitatKind.Coop, 9f, 0f, new[] { "chicken_feed", "mealworms" }),
                ("duck", "pekin", AnimalHabitatKind.Coop, 9f, 0f, new[] { "grain_mix", "chicken_feed" }),
                ("rabbit", "dutch", AnimalHabitatKind.Hutch, 6f, 0f, new[] { "hay", "clover" }),
                ("sheep", "suffolk", AnimalHabitatKind.Pasture, 48f, 1.5f, new[] { "hay", "clover" }),
                ("goat", "saanen", AnimalHabitatKind.Pasture, 48f, 1.5f, new[] { "hay", "alfalfa" }),
                ("pig", "berkshire", AnimalHabitatKind.Sty, 12f, 0f, new[] { "grain_mix", "carrot" }),
                ("cow", "jersey", AnimalHabitatKind.Pasture, 48f, 1.5f, new[] { "hay", "alfalfa" }),
                ("horse", "arabian", AnimalHabitatKind.Stable, 30f, 0f, new[] { "hay", "carrot" }),
            };
            var bad = new List<string>();
            foreach (var s in setups)
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat);
                var book = new AnimalBook(cat, "test");
                AnimalHerd h = Pen(book, "pen", s.kind, s.area, Start, s.grass);
                AnimalRecord a = Release(book, ctx, h, "p1", s.species, s.breed, Start, GrowthStage.Adult, 4242);
                AnimalActor keeper = Keeper(cat);
                string fell = null;
                for (int hr = 1; hr <= 24 * 30 && fell == null; hr++)
                {
                    double t = Start + hr;
                    AnimalSim.AdvanceHerd(h, t, ctx);
                    if (a.conditions.Count > 0) fell = $"{s.species}: {Conditions(a)} on day {(t - Start) / 24.0:0.0}";
                    if (hr % 24 != 8) continue;
                    foreach (string food in s.foods) TopUp(cat, h, food, 30f, t);
                    KeepWell(book, ctx, h, keeper, t, hr / 24);
                }
                if (fell != null) bad.Add(fell);
                else if (a.health < 90f) bad.Add($"{s.species}: health {a.health:0}");
            }
            Assert.IsEmpty(bad, string.Join("; ", bad));
        }

        [Test]
        public void ALoneCowOnAPastureStaysWellAndAtEase()
        {
            // §12 S1: one cow on an 8 x 6 m pasture with grass, hay and water, never groomed.
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            var spec = new AnimalPenSpec { id = "pasture", kind = AnimalHabitatKind.Pasture, area = 48f, feederCapacity = 120f, waterCapacity = 192f, troughSurface = 1.2f, grassGrowth = 1.5f };
            AnimalHerd h = book.EnsurePen(spec, "Test", "", Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalActor keeper = Keeper(cat);
            for (int hr = 1; hr <= 24 * 14; hr++)
            {
                double t = Start + hr;
                AnimalSim.AdvanceHerd(h, t, ctx);
                Assert.AreEqual("none", Conditions(a), $"day {(t - Start) / 24.0:0.0}");
                if (hr % 24 != 8) continue;
                TopUp(cat, h, "hay", 60f, t);
                TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "pasture" }, keeper, t);
            }
            Assert.GreaterOrEqual(AnimalWelfare.BandOf(a.welfare, cat.Rules.welfare), AnimalWelfareBand.Average, $"welfare {a.welfare:0}");
            Assert.Greater(a.health, 90f);
        }

        [Test]
        public void AFullPastureNeverMuckedOutFallsIllBetweenDay5AndDay20()
        {
            // §12 S2: three cows on the same pasture, fed hay and watered every day, never mucked out nor groomed.
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            var spec = new AnimalPenSpec { id = "pasture", kind = AnimalHabitatKind.Pasture, area = 48f, feederCapacity = 120f, waterCapacity = 192f, troughSurface = 1.2f, grassGrowth = 1.5f };
            AnimalHerd h = book.EnsurePen(spec, "Test", "", Start);
            for (int i = 0; i < 3; i++) Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 7000 + i);
            AnimalActor keeper = Keeper(cat);
            double first = -1.0;
            string what = "";
            for (int hr = 1; hr <= 24 * 20 && first < 0.0; hr++)
            {
                double t = Start + hr;
                AnimalSim.AdvanceHerd(h, t, ctx);
                foreach (AnimalRecord a in h.animals)
                    if (first < 0.0 && a.conditions.Count > 0)
                    {
                        first = (t - Start) / 24.0;
                        what = Conditions(a);
                    }
                if (hr % 24 != 8) continue;
                TopUp(cat, h, "hay", 60f, t);
                TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "pasture" }, keeper, t);
            }
            Assert.That(first, Is.InRange(5.0, 20.0), $"a fully stocked pasture nobody mucks out makes its cows ill, not at once (first: {what} on day {first:0.0})");
        }

        [Test]
        public void AnInfectionSpreadsToPenMatesUntilTheSickOneIsTakenAway()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            Pen(book, "sickbay", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord sick = Release(book, ctx, h, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 900);
            for (int i = 1; i < 5; i++) Release(book, ctx, h, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 900 + i);
            Stock(cat, h, "chicken_feed", 1000f, 0.8f, Start);
            Ill(sick, cat, Infection, 75f, Start);
            string json = JsonUtility.ToJson(book.Export(Start, 0));

            // Run the same coop twice from the same save, and once with the sick hen moved out after half a day.
            var runs = new List<string>();
            for (int run = 0; run < 3; run++)
            {
                var b = new AnimalBook(cat, "test");
                b.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
                b.Herd("coop").spec = h.spec;
                b.Herd("sickbay").spec = book.Herd("sickbay").spec;
                AnimalSimContext c = Ctx(cat);
                var onsets = new List<string>();
                for (int hr = 1; hr <= 24 * 5; hr++)
                {
                    double t = Start + hr;
                    AnimalSim.AdvanceHerd(b.Herd("coop"), t, c);
                    AnimalSim.AdvanceHerd(b.Herd("sickbay"), t, c);
                    // A clean coop: nothing but the sick hen makes them ill.
                    if (hr % 24 == 0) Do(b, c, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "coop", item = Straw }, Keeper(cat), t);
                    if (run == 2 && hr == 12) Do(b, c, new AnimalRequest { kind = AnimalActionKind.Move, pen = "sickbay", animal = sick.id }, Actor("p1", AnimalRights.All), t);
                    foreach (AnimalRecord a in b.Herd("coop").animals)
                    {
                        AnimalCondition k = a.Condition(Infection);
                        if (a.id != sick.id && k != null && !onsets.Exists(o => o.StartsWith(a.id)))
                            onsets.Add($"{a.id} at {k.since:0.##} from {k.cause}");
                    }
                }
                runs.Add(string.Join("; ", onsets));
            }
            Assert.IsNotEmpty(runs[0], "its pen-mates catch it");
            StringAssert.Contains("Contagion", runs[0], "from it");
            Assert.AreEqual(runs[0], runs[1], "the same coop gives the same illnesses at the same hours");
            Assert.IsEmpty(runs[2], "taken away in time, it gives it to nobody");

            // Alone, a hen does not catch its own illness: a Moderate infection below Needs Treatment Above mends by itself.
            var lone = new AnimalBook(cat, "test");
            AnimalHerd alone = Pen(lone, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(lone, ctx, alone, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 950);
            Stock(cat, alone, "chicken_feed", 500f, 0.8f, Start);
            Ill(hen, cat, Infection, cat.Condition(Infection).needsTreatmentAbove - 1f, Start);
            AnimalSim.AdvanceHerd(alone, Start + 24 * 4, ctx);
            Assert.IsTrue(hen.Condition(Infection) == null || hen.Condition(Infection).Recovering, $"it mends by itself ({Conditions(hen)})");

            // The remains of a hen an infection killed go on spreading it for a while.
            AnimalHerd grave = Pen(lone, "grave", AnimalHabitatKind.Coop, 9f, Start + 24 * 4);
            AnimalRecord body = Release(lone, ctx, grave, "p1", "chicken", "leghorn", Start + 24 * 4, GrowthStage.Adult, 951);
            body.deathCondition = Infection;
            AnimalSim.Die(body, AnimalDeathCause.Illness, Start + 24 * 4, ctx, cat.Species("chicken"));
            var catching = new Dictionary<string, float>();
            AnimalHabitat.Facts(grave, grave.spec, cat, cat.Rules, Start + 24 * 4 + 10.0, catching, null);
            Assert.AreEqual(cat.Condition(Infection).contagion * 0.8f, catching.TryGetValue(Infection, out float fresh) ? fresh : 0f, 1e-4, "fresh remains spread it");
            AnimalHabitat.Facts(grave, grave.spec, cat, cat.Rules, Start + 24 * 4 + cat.Rules.remainsContagionHours + 1.0, catching, null);
            Assert.IsFalse(catching.ContainsKey(Infection), "old remains no longer do");
        }

        [Test]
        public void WormsNeedTheVermifugeAndLeaveImmunityBehind()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 30f, Start);
            AnimalRecord sheep = Release(book, ctx, h, "p1", "sheep", "jacob", Start);
            Stock(cat, h, "hay", 600f, 0.8f, Start);
            Ill(sheep, cat, "parasites", 30f, Start, AnimalCause.Worms);
            double t = Start + 24 * 5;
            AnimalSim.AdvanceHerd(h, t, ctx);
            AnimalCondition k = sheep.Condition("parasites");
            Assert.IsNotNull(k, "worms never clear by themselves");
            Assert.GreaterOrEqual(k.severity, 30f, "even in a clean barn");

            string vermifuge = "potion:vet_vermifuge";
            var keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [vermifuge] = 4 });
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = sheep.id, item = vermifuge }, keeper, t);
            double cleared = -1.0;
            for (int hr = 1; hr <= 96 && cleared < 0.0; hr++)
            {
                AnimalSim.AdvanceHerd(h, t + hr, ctx);
                if (sheep.Condition("parasites") == null) cleared = t + hr;
            }
            Assert.Greater(cleared, 0.0, "the vermifuge clears them");
            Assert.IsNotNull(LastHealth(sheep, AnimalHealthEventKind.Cleared, "parasites"));
            AnimalExposure e = sheep.Exposure("parasites");
            Assert.IsNotNull(e);
            Assert.Greater(e.immuneUntil, cleared, "it is immune for a while");

            // Back among worms and lice: nothing builds while it is immune; after, it does.
            h.pen.parasites = 100f;
            sheep.hygiene = 0f;
            AnimalSim.AdvanceHerd(h, e.immuneUntil - 1.0, ctx);
            Assert.IsNull(sheep.Condition("parasites"), "no worms while immune");
            Assert.AreEqual(0f, sheep.Exposure("parasites")?.value ?? 0f, 1e-4, "nothing builds up");
            AnimalSim.AdvanceHerd(h, e.immuneUntil + 24.0, ctx);
            Assert.IsTrue(sheep.Condition("parasites") != null || (sheep.Exposure("parasites")?.value ?? 0f) > 0f, "after it, worms take hold again");
        }

        [Test]
        public void AMildUpsetPassesByItselfButABadOneMustBeTreated()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            Stock(cat, h, "hay", 1000f, 0.8f, Start);

            // A hungry cow fed spoiled feed by hand, twice: its gut is upset, then settles by itself.
            string spoiled = AnimalIds.Feed("spoiled_feed");
            var keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [spoiled] = 8 });
            var meal = new AnimalRequest { kind = AnimalActionKind.Feed, pen = "pen", animal = a.id, item = spoiled, count = 4 };
            a.satiety = 20f;
            Do(book, ctx, meal, keeper, Start);
            Assert.Greater(a.upset, 60f, "it ate enough to upset its stomach badly");
            AnimalSim.AdvanceHerd(h, Start + 3.0, ctx);
            a.satiety = 20f;
            Do(book, ctx, meal, keeper, Start + 3.0);
            bool upset = false;
            float worst = 0f;
            for (int hr = 4; hr <= 24 * 4; hr++)
            {
                AnimalSim.AdvanceHerd(h, Start + hr, ctx);
                AnimalCondition d = a.Condition("digestive");
                if (d == null) continue;
                upset = true;
                worst = Mathf.Max(worst, d.severity);
            }
            Assert.IsTrue(upset, "the bad food upsets its gut");
            Assert.Less(worst, cat.Condition("digestive").needsTreatmentAbove, "a bout from a couple of bad meals stays mild enough to mend");
            Assert.IsNull(a.Condition("digestive"), "and it passes by itself");
            Assert.IsNotNull(LastHealth(a, AnimalHealthEventKind.Cleared, "digestive"));

            // A bad bout (past Needs Treatment Above) does not mend by itself...
            double t = Start + 24 * 4;
            AnimalConditionData data = cat.Condition("digestive");
            AnimalCondition k = Ill(b, cat, "digestive", 70f, t, AnimalCause.Upset);
            Assert.Greater(k.severity, data.needsTreatmentAbove);
            AnimalSim.AdvanceHerd(h, t + 24.0, ctx);
            Assert.GreaterOrEqual(k.severity, data.needsTreatmentAbove, "a bad bout does not mend by itself");
            Assert.IsFalse(k.Recovering);
            // ...but a ginger tonic settles it.
            string ginger = "potion:ginger_tonic";
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = b.id, item = ginger }, Actor("p1", AnimalRights.All, new Dictionary<string, int> { [ginger] = 5 }), t + 24.0);
            AnimalSim.AdvanceHerd(h, t + 48.0, ctx);
            AnimalCondition after = b.Condition("digestive");
            Assert.IsTrue(after == null || after.Recovering, $"treated, it mends ({Conditions(b)})");
        }

        [Test]
        public void PoisonousFoodShowsAsFoodPoisoningWithinHours()
        {
            // §12 S4, its toxic half: a hungry cow eats poisonous herbs from its keeper's hand.
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 1000f, 0.8f, Start);
            string herbs = AnimalIds.Feed("poisonous_herbs");
            cow.satiety = 10f;
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Feed, pen = "pen", animal = cow.id, item = herbs, count = 6 },
                Actor("p1", AnimalRights.All, new Dictionary<string, int> { [herbs] = 6 }), Start);
            double shown = -1.0;
            for (int hr = 1; hr <= 4 && shown < 0.0; hr++)
            {
                AnimalSim.AdvanceHerd(h, Start + hr, ctx);
                AnimalCondition k = cow.Condition("food_poisoning");
                if (k != null && k.shown) shown = hr;
            }
            Assert.Greater(shown, 0.0, $"food poisoning shows within 4 hours ({Conditions(cow)})");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.FellIll && n.animal == cow.id), "and its owner is told the symptom");
            Assert.AreEqual(AnimalCause.Toxins, cow.Condition("food_poisoning").cause, "from what it ate");
        }

        // ---------------------------------------------------------------- medicine and the veterinarian
        [Test]
        public void TheRightMedicineCuresTheWrongOneDoesNotABigAnimalNeedsMoreAndTooMuchPoisons()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Stock(cat, h, "hay", 1000f, 0.8f, Start);
            string elixir = "potion:restoration_elixir", calming = "potion:calming_tonic";
            var bag = new Dictionary<string, int> { [elixir] = 1, [calming] = 20 };
            AnimalActor keeper = Actor("p1", AnimalRights.All, bag);

            // A cow takes more bottles than a hen.
            string why = Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = elixir }, keeper, Start);
            StringAssert.Contains("you have 1", why);
            AnimalPlan forHen = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "coop", animal = hen.id, item = elixir }, keeper, Start, ctx);
            Assert.IsFalse(forHen.Refused, forHen.refusal?.message);
            bag[elixir] = 10;
            AnimalPlan forCow = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = elixir }, keeper, Start, ctx);
            Assert.Greater(forCow.takeCount, forHen.takeCount, $"a cow needs more ({forCow.takeCount} against {forHen.takeCount})");

            AnimalCondition k = Ill(cow, cat, Infection, 50f, Start, AnimalCause.DirtyWater);
            // The wrong medicine does nothing for it...
            AnimalResult wrong = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = calming }, keeper, Start);
            Assert.IsTrue(wrong.lines.Contains("It does not seem to help."), string.Join(" | ", wrong.lines));
            Assert.AreEqual(50f, k.severity, 1e-3);
            // ...and a second dose of it too soon is an overdose.
            float toxin = cow.toxin;
            AnimalResult again = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = calming }, keeper, Start + 1.0);
            Assert.Greater(cow.toxin, toxin + 1f, "too much, too soon");
            Assert.IsTrue(again.lines.Contains("Too much, too soon: the dose makes it unwell."), string.Join(" | ", again.lines));
            Assert.GreaterOrEqual(k.severity, cat.Condition(Infection).needsTreatmentAbove - 1e-3, "untreated, a Moderate infection does not mend");

            // The right one cures it.
            AnimalResult right = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = elixir }, keeper, Start + 2.0);
            Assert.IsFalse(right.lines.Contains("It does not seem to help."));
            Assert.Less(k.severity, 45f, "it takes off severity at once");
            Assert.IsTrue(k.Treated(Start + 3.0), "and goes on working");
            AnimalSim.AdvanceHerd(h, Start + 24 * 3, ctx);
            Assert.IsNull(cow.Condition(Infection), $"cured ({Conditions(cow)})");
            Assert.IsNotNull(LastHealth(cow, AnimalHealthEventKind.Treated));
            Assert.IsNotNull(cow.careLog.FindLast(e => e.what == "treated"), "and in its care history");
        }

        [Test]
        public void PastNeedsVetAboveKeeperMedicineOnlyHoldsItAndTheVeterinarianCuresIt()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            ctx.vetAvailable = true;
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 1000f, 0.8f, Start);
            AnimalConditionData data = cat.Condition(Infection);
            AnimalCondition k = Ill(cow, cat, Infection, 85f, Start, AnimalCause.DirtyHabitat, diagnosed: true);
            Assert.GreaterOrEqual(k.severity, data.needsVetAbove);

            string elixir = "potion:restoration_elixir";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [elixir] = 10 });
            AnimalResult held = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = elixir }, keeper, Start);
            Assert.IsTrue(held.lines.Contains("This is beyond your medicine: send for the veterinarian."), string.Join(" | ", held.lines));
            Assert.GreaterOrEqual(k.severity, data.needsVetAbove - 1e-3, "keeper medicine only holds it");
            AnimalSim.AdvanceHerd(h, Start + 12.0, ctx);
            Assert.GreaterOrEqual(k.severity, data.needsVetAbove - 1e-3, "for as long as it works");
            AnimalResult look = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "pen", animal = cow.id }, keeper, Start + 12.0);
            Assert.IsTrue(look.lines.Exists(l => l.Contains("It needs a veterinarian.")), string.Join(" | ", look.lines));

            // The veterinarian: by the fee the player saw, and only where there is one.
            var call = new AnimalRequest { kind = AnimalActionKind.CallVet, pen = "pen", animal = cow.id, expected = 40 };
            StringAssert.Contains("no veterinarian", Refusal(book, ctx, call, keeper, Start + 12.0));
            keeper.vetFee = (a, sp) => 40L;
            AnimalPlan changed = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.CallVet, pen = "pen", animal = cow.id, expected = 30 }, keeper, Start + 12.0, ctx);
            Assert.IsTrue(changed.Refused, "nobody pays a fee they did not see");
            StringAssert.Contains("40", changed.refusal.message);
            AnimalPlan plan = AnimalCommands.Plan(book, call, keeper, Start + 12.0, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.AreEqual(40L, plan.fee);
            Assert.IsTrue(plan.TakesSomething);
            float before = k.severity;
            AnimalResult unpaid = AnimalCommands.Commit(book, plan, new AnimalTaken { paid = 10 }, Start + 12.0, ctx);
            Assert.IsFalse(unpaid.ok, "the vet is paid first");
            Assert.AreEqual(before, k.severity, 1e-4);
            AnimalResult seen = AnimalCommands.Commit(book, plan, new AnimalTaken { paid = 40 }, Start + 12.0, ctx);
            Assert.IsTrue(seen.ok, seen.message);
            Assert.IsTrue(k.byVet);
            Assert.Less(k.severity, before, "the veterinarian turns it");
            Assert.IsTrue(cow.events.Exists(e => e.kind == AnimalEventKind.SeenByVet));
            StringAssert.Contains("veterinarian saw", Refusal(book, ctx, call, keeper, Start + 13.0), "not again so soon");
            AnimalSim.AdvanceHerd(h, Start + 24 * 4, ctx);
            AnimalCondition after = cow.Condition(Infection);
            Assert.IsTrue(after == null || after.Recovering, $"cured ({Conditions(cow)})");
        }

        [Test]
        public void WithNoVeterinarianInTheWorldKeeperMedicineCuresEverything()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            Assert.IsFalse(ctx.vetAvailable);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 1000f, 0.8f, Start);
            AnimalCondition k = Ill(cow, cat, "injury", 80f, Start, AnimalCause.Fighting, diagnosed: true);
            string salve = "potion:healing_salve";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [salve] = 20 });
            AnimalResult res = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = salve }, keeper, Start);
            Assert.IsFalse(res.lines.Exists(l => l.Contains("veterinarian")), string.Join(" | ", res.lines));
            Assert.Less(k.severity, cat.Condition("injury").needsVetAbove, "with no vet in the world, the salve turns it");
            for (int day = 1; day <= 4; day++) TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = cow.id, item = salve }, keeper, Start + 24.0 * day);
            AnimalSim.AdvanceHerd(h, Start + 24 * 7, ctx);
            AnimalCondition after = cow.Condition("injury");
            Assert.IsTrue(after == null || after.Recovering, $"a course of salve cures it ({Conditions(cow)})");
        }

        /// <summary>The severity the treatment working on an illness takes off from <paramref name="from"/> on (each step's power to its own end: see <see cref="TakenOff"/>).</summary>
        private static double TreatmentLeft(AnimalCondition k, double from) => TakenOff(k, from, double.PositiveInfinity);

        /// <summary>What two doses working from <paramref name="from"/> on take off when each hour gets the stronger of the two.</summary>
        private static double StrongerEachHour(float power, double end, float other, double otherEnd, double from)
        {
            if (other > power) return StrongerEachHour(other, otherEnd, power, end, from);
            double strong = Math.Max(0.0, end - from), weak = Math.Max(0.0, otherEnd - from);
            return power * strong + (weak > strong ? other * (weak - strong) : 0.0);
        }

        [Test]
        public void AWeakDoseDoesNotStretchAStrongOneNorACheapWashTheVeterinarians()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData sp = cat.Species("cow");
            AnimalTreatmentData draught = cat.Treatment("healing_draught"), wash = cat.Treatment("vinegar_wash"),
                salve = cat.Treatment("healing_salve"), antiseptic = cat.Treatment("antiseptic");
            AnimalTreatmentEffect washing = wash.EffectOn(Infection);

            // A strong draught, then a cheap wash an hour before it ends: the draught keeps its power to its own end, and the
            // wash adds only what it would do itself after that.
            var a = new AnimalRecord { id = "a", owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult };
            AnimalCondition k = Ill(a, cat, Infection, 60f, Start, diagnosed: true);
            AnimalHealth.Treat(a, sp, draught, cat, rules, 1.5f, true, "p1", "p1", Start, false, null);
            float strong = draught.EffectOn(Infection).perHour * 1.5f;
            double strongEnd = Start + draught.EffectOn(Infection).hours;
            Assert.AreEqual(strong, k.treatPower, 1e-4);
            Assert.AreEqual(strongEnd, k.treatedUntil, 1e-6);
            double t = strongEnd - 1.0;
            AnimalHealth.Treat(a, sp, wash, cat, rules, 1f, true, "p1", "p1", t, false, null);
            Assert.AreEqual(strong, k.treatPower, 1e-4, "the stronger dose works first, at its full power");
            Assert.AreEqual(StrongerEachHour(strong, strongEnd, washing.perHour, t + washing.hours, t), TreatmentLeft(k, t), 1e-3,
                "the weak one adds only what it would do after the strong one ends: it does not stretch it");

            // The veterinarian's treatment, then a cheap wash an hour before it ends: the vet's power is not stretched.
            var b = new AnimalRecord { id = "b", owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult };
            AnimalCondition kb = Ill(b, cat, Infection, 85f, Start, diagnosed: true);
            AnimalHealth.VetVisit(b, sp, cat, rules, Start, "p1", "p1", null);
            Assert.AreEqual(rules.vetPerHour, kb.treatPower, 1e-4);
            t = Start + rules.vetHours - 1.0;
            AnimalHealth.Treat(b, sp, wash, cat, rules, 1f, true, "p1", "p1", t, false, null);
            Assert.AreEqual(rules.vetPerHour, kb.treatPower, 1e-4);
            Assert.AreEqual(StrongerEachHour(rules.vetPerHour, Start + rules.vetHours, washing.perHour, t + washing.hours, t), TreatmentLeft(kb, t), 1e-3,
                "a wash does not buy more of the veterinarian's treatment");

            // A strong short dose given while a weak long one works: the strong one first, then what is left of the weak one.
            var c = new AnimalRecord { id = "c", owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult };
            AnimalCondition kc = Ill(c, cat, "hoof_rot", 60f, Start, AnimalCause.HoofNeglect, diagnosed: true);
            AnimalHealth.Treat(c, sp, salve, cat, rules, 1f, true, "p1", "p1", Start, false, null);
            AnimalTreatmentEffect weak = salve.EffectOn("hoof_rot"), strongOne = antiseptic.EffectOn("hoof_rot");
            t = Start + 1.0;
            AnimalHealth.Treat(c, sp, antiseptic, cat, rules, 1f, true, "p1", "p1", t, false, null);
            Assert.AreEqual(strongOne.perHour, kc.treatPower, 1e-4);
            Assert.AreEqual(StrongerEachHour(weak.perHour, Start + weak.hours, strongOne.perHour, t + strongOne.hours, t), TreatmentLeft(kc, t), 1e-3,
                "nor does a weak dose stretch a strong one that came after it");
        }

        /// <summary>A dose of a course of treatment on an illness: given at <c>from</c>, it works to its own end (<c>to</c>) at its power.</summary>
        private struct DoseSpan
        {
            public double from, to;
            public float power;
        }

        /// <summary>The power of the strongest dose working at a time (0: none).</summary>
        private static float StrongestAt(List<DoseSpan> doses, double t)
        {
            float best = 0f;
            foreach (DoseSpan d in doses)
                if (t >= d.from && t < d.to) best = Mathf.Max(best, d.power);
            return best;
        }

        /// <summary>What doses take off from one time to another when each hour gets the strongest of them working then (each to its own end).</summary>
        private static double EachHourTheStrongest(List<DoseSpan> doses, double from, double to)
        {
            var cuts = new List<double> { from, to };
            foreach (DoseSpan d in doses)
            {
                if (d.from > from && d.from < to) cuts.Add(d.from);
                if (d.to > from && d.to < to) cuts.Add(d.to);
            }
            cuts.Sort();
            double sum = 0.0;
            for (int i = 1; i < cuts.Count; i++) sum += StrongestAt(doses, 0.5 * (cuts[i - 1] + cuts[i])) * (cuts[i] - cuts[i - 1]);
            return sum;
        }

        /// <summary>What an illness's treatment, as its record holds it, takes off from one time to another: the strongest dose to its end, then each weaker one's to its own.</summary>
        private static double TakenOff(AnimalCondition k, double from, double to)
        {
            double sum = 0.0, start = from;
            foreach ((float power, double until) in Steps(k, from))
            {
                sum += power * Math.Max(0.0, Math.Min(to, until) - start);
                start = until;
            }
            return sum;
        }

        /// <summary>
        /// The steps of an illness's treatment as its record holds them from a time on, as <see cref="AnimalCondition.PowerAt"/>
        /// reads them: the first step's power to its end, then each later step's to its own (one ending before the step ahead of
        /// it is never reached). Empty when nothing works.
        /// </summary>
        private static List<(float power, double until)> Steps(AnimalCondition k, double from)
        {
            var steps = new List<(float power, double until)>();
            double end = from;
            void Step(float power, double until)
            {
                if (until <= end) return;
                steps.Add((power, until));
                end = until;
            }
            Step(k.treatPower, k.treatedUntil);
            Step(k.thenPower, k.thenUntil);
            if (k.laterSteps != null)
                foreach (AnimalDoseStep s in k.laterSteps)
                    if (s != null) Step(s.power, s.until);
            return steps;
        }

        /// <summary>Each hour's strongest dose from a time on (found by brute force), as steps: a power to where the strongest dose working changes.</summary>
        private static List<(float power, double until)> Envelope(List<DoseSpan> doses, double from)
        {
            var ends = new List<double>();
            foreach (DoseSpan d in doses)
                if (d.from <= from && d.to > from && !ends.Contains(d.to)) ends.Add(d.to);
            ends.Sort();
            var steps = new List<(float power, double until)>();
            double at = from;
            foreach (double end in ends)
            {
                float power = StrongestAt(doses, 0.5 * (at + end));
                if (steps.Count > 0 && steps[steps.Count - 1].power == power) steps[steps.Count - 1] = (power, end);
                else if (power > 0f) steps.Add((power, end));
                at = end;
            }
            return steps;
        }

        /// <summary>Are two lists of steps the same: each power, each end (both are the doses' own, so exactly)?</summary>
        private static bool SameSteps(List<(float power, double until)> a, List<(float power, double until)> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (Math.Abs(a[i].power - b[i].power) > 1e-6f || Math.Abs(a[i].until - b[i].until) > 1e-9) return false;
            return true;
        }

        private static string StepsText(List<(float power, double until)> steps)
        {
            var parts = new List<string>();
            foreach ((float power, double until) in steps) parts.Add($"{power:0.###}/h to {until - Start:0.###} h");
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        /// <summary>A fixed pseudo-random sequence (a 64-bit linear congruential generator): the same numbers on every runtime.</summary>
        private sealed class Lcg
        {
            private ulong state;

            public Lcg(ulong seed) => state = seed;

            /// <summary>The next number, from 0 up to (not including) 1.</summary>
            public double Next()
            {
                state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
                return (state >> 11) * (1.0 / 9007199254740992.0);
            }

            /// <summary>The next whole number from 0 to <paramref name="n"/> - 1.</summary>
            public int Below(int n) => Math.Min(n - 1, (int)(Next() * n));
        }

        /// <summary>A cow's record (no pen; its coat clean, so a wash works at full strength) with a named illness at a severity.</summary>
        private static AnimalRecord Treatable(AnimalCatalog cat, string condition, float severity, out AnimalCondition k)
        {
            var a = new AnimalRecord { id = "a", owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult, hygiene = 80f };
            k = Ill(a, cat, condition, severity, Start, AnimalCause.DirtyHabitat, diagnosed: true);
            return a;
        }

        /// <summary>Keeps an illness where it was (so only its treatment is measured).</summary>
        private static void Hold(AnimalCondition k, AnimalConditionData c, float severity)
        {
            k.severity = severity;
            k.stage = c.StageAt(severity);
            k.recoveringUntil = -1.0;
        }

        /// <summary>Gives a dose with the real rules: the veterinarian's visit (<paramref name="treatment"/> "vet"), or that medicine at that strength. Returns its span on the illness.</summary>
        private static DoseSpan Give(AnimalRecord a, AnimalCatalog cat, string condition, double at, string treatment, float strength)
        {
            AnimalRules rules = cat.Rules;
            AnimalSpeciesData sp = cat.Species(a.species);
            if (treatment == "vet")
            {
                AnimalHealth.VetVisit(a, sp, cat, rules, at, "p1", "p1", null);
                return new DoseSpan { from = at, to = at + rules.vetHours, power = rules.vetPerHour };
            }
            AnimalTreatmentData tr = cat.Treatment(treatment);
            AnimalHealth.Treat(a, sp, tr, cat, rules, strength, true, "p1", "p1", at, false, null);
            AnimalTreatmentEffect e = tr.EffectOn(condition);
            return new DoseSpan { from = at, to = at + e.hours, power = e.perHour * strength };
        }

        /// <summary>
        /// An infection kept at 70 given a course of doses (hours from the start), in order. Returns what its treatment record
        /// takes off over <paramref name="hours"/> (summed between the doses), what the doses take off when each hour gets the
        /// strongest of them working then, and each dose after which the record's first step was not the strongest dose working
        /// then, to that dose's own end ("" when none). <paramref name="afterDose"/> (may be null) sees the record after each dose.
        /// </summary>
        private static (double record, double strongest, string moved) Course(AnimalCatalog cat, double hours, List<(double at, string treatment, float strength)> course,
            Action<double, AnimalCondition> afterDose = null)
        {
            AnimalConditionData c = cat.Condition(Infection);
            AnimalRecord a = Treatable(cat, Infection, 70f, out AnimalCondition k);
            var doses = new List<DoseSpan>();
            var moved = new List<string>();
            double record = 0.0, last = Start;
            foreach (var d in course)
            {
                double t = Start + d.at;
                record += TakenOff(k, last, t);
                last = t;
                Hold(k, c, 70f);
                doses.Add(Give(a, cat, Infection, t, d.treatment, d.strength));
                DoseSpan best = default;
                foreach (DoseSpan s in doses)
                    if (t >= s.from && t < s.to && (s.power > best.power || (s.power == best.power && s.to > best.to))) best = s;
                if (Math.Abs(k.treatPower - best.power) > 1e-4f || Math.Abs(k.treatedUntil - best.to) > 1e-6)
                    moved.Add($"after {d.at:0} h: {k.treatPower:0.##}/h to {k.treatedUntil - Start:0.##} h, not {best.power:0.##}/h to {best.to - Start:0.##} h");
                afterDose?.Invoke(d.at, k);
            }
            record += TakenOff(k, last, Start + hours);
            return (record, EachHourTheStrongest(doses, Start, Start + hours), string.Join("; ", moved));
        }

        [Test]
        public void AWeakDoseGivenAgainAndAgainNeverKeepsAStrongOneWorkingPastItsOwnEnd()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash"), draught = cat.Treatment("healing_draught");
            AnimalTreatmentEffect washing = wash.EffectOn(Infection), healing = draught.EffectOn(Infection);
            float standard = AnimalHealth.Strength(draught, Taken(1, 0.5f), 1); // a Standard draught (quality 0.5)
            const double lastAfterVet = 44.0, lastAfterDraught = 42.0, horizon = 72.0;
            // The veterinarian, then a cheap wash every 4 hours (more often than its interval) from 8 h on...
            var afterVet = new List<(double at, string treatment, float strength)> { (0.0, "vet", 1f) };
            for (double t = 8.0; t <= lastAfterVet; t += 4.0) afterVet.Add((t, wash.id, 1f));
            // ...a Standard draught, then a wash at the wash's own interval; and the same washes alone.
            var washes = new List<(double at, string treatment, float strength)>();
            for (double t = wash.minIntervalHours; t <= lastAfterDraught; t += wash.minIntervalHours) washes.Add((t, wash.id, 1f));
            var afterDraught = new List<(double at, string treatment, float strength)> { (0.0, draught.id, standard) };
            afterDraught.AddRange(washes);

            var vet = Course(cat, horizon, afterVet);
            var drink = Course(cat, horizon, afterDraught);
            var alone = Course(cat, horizon, washes);
            string all = $"taken off in {horizon:0} h by the record against each hour's strongest dose: the vet then a wash every 4 h {vet.record:0.0} against {vet.strongest:0.0}; " +
                         $"a draught (x{standard:0.0}) then a wash every {wash.minIntervalHours:0} h {drink.record:0.0} against {drink.strongest:0.0}; the washes alone {alone.record:0.0} against {alone.strongest:0.0}";
            // Each hour's strongest dose: the vet's (or the draught's) to its end, then the washes' to the last one's end.
            Assert.AreEqual(rules.vetPerHour * rules.vetHours + washing.perHour * (lastAfterVet + washing.hours - rules.vetHours), vet.strongest, 1e-3, all);
            Assert.AreEqual(healing.perHour * standard * healing.hours + washing.perHour * (lastAfterDraught + washing.hours - healing.hours), drink.strongest, 1e-3, all);
            Assert.AreEqual(washing.perHour * (lastAfterDraught + washing.hours - wash.minIntervalHours), alone.strongest, 1e-3, all);

            // The record takes off just that (exactly: each step to its own end): no weak dose keeps a strong one working.
            Assert.AreEqual(vet.strongest, vet.record, 1e-3, "washes after the veterinarian do not stretch the visit's treatment: " + all);
            Assert.AreEqual(drink.strongest, drink.record, 1e-3, "nor washes after a draught the draught: " + all);
            Assert.AreEqual(alone.strongest, alone.record, 1e-3, all);
            Assert.IsEmpty(vet.moved, "the stronger dose keeps its own end: " + vet.moved);
            Assert.IsEmpty(drink.moved, "the stronger dose keeps its own end: " + drink.moved);
            Assert.IsEmpty(alone.moved, alone.moved);
        }

        /// <summary>
        /// An illness kept above its Needs Treatment Above (untreated, it does not mend), given a course of doses (hours from the
        /// start) and stepped hour by hour by <see cref="AnimalHealth.Step"/> with no cause at a Good welfare: each hour it must
        /// lose its own mending and the power of the strongest dose working then, or nothing when none is. Returns each hour that
        /// was not so ("" when none).
        /// </summary>
        private static string HourByHour(AnimalCatalog cat, string condition, float severity, int hours, List<(double at, string treatment, float strength)> course) =>
            HourByHour(cat, condition, severity, hours, course, out AnimalRecord _);

        /// <summary>As HourByHour above, and the record it stepped (<paramref name="a"/>).</summary>
        private static string HourByHour(AnimalCatalog cat, string condition, float severity, int hours, List<(double at, string treatment, float strength)> course,
            out AnimalRecord a)
        {
            AnimalConditionData c = cat.Condition(condition);
            Assert.Greater(severity, c.needsTreatmentAbove, condition);
            AnimalSimContext ctx = Ctx(cat);
            AnimalWelfareBandEffects band = AnimalWelfare.Effects(AnimalWelfareBand.Good, cat.Rules.welfare);
            float mending = -c.courseWithoutCause * band.recovery;
            var facts = new AnimalStepFacts();
            float[] causes = AnimalHealth.NewCauses();
            a = Treatable(cat, condition, severity, out AnimalCondition k);
            var doses = new List<DoseSpan>();
            var wrong = new List<string>();
            for (int hr = 0; hr < hours; hr++)
            {
                double t = Start + hr;
                foreach (var d in course)
                    if (d.at == hr) doses.Add(Give(a, cat, condition, t, d.treatment, d.strength));
                Hold(k, c, severity);
                AnimalHealth.Step(a, cat.Species(a.species), ctx, in facts, causes, t, t + 1.0, 1f, band, null);
                float strongest = StrongestAt(doses, t), expected = strongest > 0f ? mending + strongest : 0f, took = severity - k.severity;
                if (Math.Abs(took - expected) > 1e-3f) wrong.Add($"hour {hr}: {took:0.##} off, not {expected:0.##}");
            }
            return string.Join("; ", wrong);
        }

        [Test]
        public void HourByHourTheStrongerDoseWorksToItsEndThenTheWeakerOneToItsOwn()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash");
            // The veterinarian, then one wash 3 hours before the visit's treatment ends: after it the wash goes on at its own power, to its own end.
            var once = new List<(double at, string treatment, float strength)> { (0.0, "vet", 1f), (rules.vetHours - 3.0, wash.id, 1f) };
            // The veterinarian, then a wash every 4 hours: the visit's power to its own end, then the washes'.
            var often = new List<(double at, string treatment, float strength)> { (0.0, "vet", 1f) };
            for (double t = 8.0; t <= 44.0; t += 4.0) often.Add((t, wash.id, 1f));
            // A strong short dose while a weak long one works (an antiseptic an hour after a salve, on hoof rot): the strong one to its end, then the rest of the weak one.
            var salveFirst = new List<(double at, string treatment, float strength)> { (0.0, "healing_salve", 1f), (1.0, "antiseptic", 1f) };
            string afterOne = HourByHour(cat, Infection, 70f, 72, once), afterMany = HourByHour(cat, Infection, 70f, 72, often),
                afterSalve = HourByHour(cat, "hoof_rot", 60f, 48, salveFirst);
            string all = $"one wash after the veterinarian: {afterOne}. A wash every 4 hours after it: {afterMany}. An antiseptic an hour after a salve: {afterSalve}.";
            Assert.IsEmpty(afterOne, all);
            Assert.IsEmpty(afterMany, all);
            Assert.IsEmpty(afterSalve, all);
        }

        [Test]
        public void TheVeterinarianADraughtAndWashesTakeOffEachHoursStrongestDoseEachToItsOwnEnd()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash"), draught = cat.Treatment("healing_draught");
            AnimalTreatmentEffect washing = wash.EffectOn(Infection), healing = draught.EffectOn(Infection);
            float standard = AnimalHealth.Strength(draught, Taken(1, 0.5f), 1); // a Standard draught (quality 0.5)
            const double draughtAt = 14.0, firstWash = 16.0, lastWash = 44.0, horizon = 72.0;
            // Three powers at once: the veterinarian at 0 h, a Standard draught at 14 h, then a wash every 4 hours from 16 h to 44 h.
            var course = new List<(double at, string treatment, float strength)> { (0.0, "vet", 1f), (draughtAt, draught.id, standard) };
            for (double t = firstWash; t <= lastWash; t += 4.0) course.Add((t, wash.id, 1f));
            float vetPower = rules.vetPerHour, draughtPower = healing.perHour * standard, washPower = washing.perHour;
            double draughtEnd = draughtAt + healing.hours, washEnd = lastWash + washing.hours;
            Assert.IsTrue(vetPower > draughtPower && draughtPower > washPower, $"three powers: {vetPower}, {draughtPower:0.##}, {washPower}");
            Assert.IsTrue(rules.vetHours < draughtEnd && draughtEnd < washEnd, "each weaker dose outlasts the stronger one");

            var first = new Dictionary<double, (float power, double until)>();
            var r = Course(cat, horizon, course, (at, k) => first[at] = (k.treatPower, k.treatedUntil));
            string all = $"taken off in {horizon:0} h by the record {r.record:0.00} against each hour's strongest dose {r.strongest:0.00}; " +
                         $"the first step after the 24 h wash: {first[24.0].power:0.##}/h to {first[24.0].until - Start:0.##} h";
            // Each hour's strongest: the veterinarian's to its end, the draught's to its own, then the washes' to the last one's end.
            Assert.AreEqual(vetPower * rules.vetHours + draughtPower * (draughtEnd - rules.vetHours) + washPower * (washEnd - draughtEnd), r.strongest, 1e-3, all);
            // The record takes off just that: the washes after the draught never keep it working past its own end.
            Assert.AreEqual(r.strongest, r.record, 1e-3, "each dose works to its own end, each hour the strongest working then: " + all);
            Assert.IsEmpty(r.moved, "the strongest dose working keeps its own end: " + r.moved);
            // Once the veterinarian's treatment has ended (the wash at 24 h), the draught works first, to its own end.
            Assert.AreEqual(draughtPower, first[24.0].power, 1e-4, all);
            Assert.AreEqual(Start + draughtEnd, first[24.0].until, 1e-6, all);
        }

        [Test]
        public void AfterEveryDoseOfThreeOrFourPowersTheRecordHoldsEachHoursStrongestDoseExactly()
        {
            float[] hours = { 4f, 6f, 8f, 12f, 16f, 24f };
            var cases = new (string name, float[] powers)[]
            {
                ("three powers (5, 3, 2)", new[] { 5f, 3f, 2f }),
                ("three powers (the vet's 5, a wash's 2 on a clean coat and 1.2 on a filthy one)", new[] { 5f, 2f, 1.2f }),
                ("four powers (6, 5, 3.6, 2)", new[] { 6f, 5f, 3.6f, 2f }),
            };
            var rng = new Lcg(20261109UL);
            var wrong = new List<string>();
            int given = 0, onAnEnd = 0, notExact = 0, moved = 0;
            foreach (var c in cases)
                for (int course = 0; course < 200; course++)
                {
                    var k = new AnimalCondition { id = Infection };
                    var doses = new List<DoseSpan>();
                    double t = Start, last = Start, record = 0.0;
                    for (int i = 0; i < 25; i++)
                    {
                        // The next dose: at once, some hours on, or exactly where a step of the record ends.
                        List<(float power, double until)> held = Steps(k, t);
                        if (held.Count > 0 && rng.Below(4) == 0)
                        {
                            t = held[rng.Below(held.Count)].until;
                            onAnEnd++;
                        }
                        else if (rng.Below(5) > 0) t += rng.Below(801) / 100.0; // 0 to 8 hours, in hundredths
                        record += TakenOff(k, last, t);
                        last = t;
                        float power = c.powers[rng.Below(c.powers.Length)], h = hours[rng.Below(hours.Length)];
                        AnimalHealth.Dose(k, t, power, h);
                        doses.Add(new DoseSpan { from = t, to = t + h, power = power });
                        given++;
                        List<(float power, double until)> want = Envelope(doses, t), got = Steps(k, t);
                        if (want.Count > 0 && (got.Count == 0 || got[0].power != want[0].power || got[0].until != want[0].until)) moved++;
                        if (SameSteps(want, got)) continue;
                        notExact++;
                        if (wrong.Count < 3)
                            wrong.Add($"{c.name}, course {course}, dose {i + 1} ({power}/h for {h} h at {t - Start:0.##} h): the record holds [{StepsText(got)}], each hour's strongest is [{StepsText(want)}]");
                    }
                    // What it takes off in all is what the doses take off, each hour the strongest of them working then.
                    record += TakenOff(k, last, double.PositiveInfinity);
                    double end = t;
                    foreach (DoseSpan d in doses) end = Math.Max(end, d.to);
                    double strongest = EachHourTheStrongest(doses, Start, end);
                    if (Math.Abs(record - strongest) > 1e-6 * Math.Max(1.0, strongest) && wrong.Count < 6)
                        wrong.Add($"{c.name}, course {course}: the record takes off {record:0.###} in all, each hour's strongest {strongest:0.###}");
                }
            string what = $"{given} doses ({onAnEnd} exactly where a step ended): after {notExact} the record was not each hour's strongest dose, and its first step moved off the strongest dose's own end {moved} times";
            Assert.IsEmpty(wrong, what + ". " + string.Join("; ", wrong));
            Assert.Greater(onAnEnd, given / 10, "doses fell on a step's end: " + what);
        }

        [Test]
        public void ADoseRecordHoldsAtMostItsMostStepsLetsTheWeakestGoAndSavesThemAll()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalCondition k = Ill(cow, cat, Infection, 70f, Start, diagnosed: true);
            // One dose more than the record holds steps: each weaker than the one before and outlasting it, all working when the last is given.
            int n = AnimalHealth.MaxDoseSteps + 1;
            var doses = new List<DoseSpan>();
            double t = Start;
            for (int i = 0; i < n; i++)
            {
                t = Start + 0.5 * i;
                float hours = 10f + 4f * i;
                var d = new DoseSpan { from = t, to = t + hours, power = n - i };
                AnimalHealth.Dose(k, t, d.power, hours);
                doses.Add(d);
            }
            List<(float power, double until)> all = Envelope(doses, t), held = Steps(k, t);
            Assert.AreEqual(n, all.Count, "each hour's strongest dose is each of them in turn: " + StepsText(all));
            List<(float power, double until)> kept = all.GetRange(0, n - 1);
            Assert.IsTrue(SameSteps(kept, held), $"the record keeps every step but the weakest dose's, each at its power to its own end: [{StepsText(held)}], not [{StepsText(kept)}]");
            Assert.AreEqual(AnimalHealth.MaxDoseSteps, held.Count);
            Assert.AreEqual(AnimalHealth.MaxDoseSteps - 2, k.laterSteps.Count, "the steps after the first two are its later steps");
            // Each step works in turn (its power read to its end), and after the last one kept nothing works, though the weakest dose would have.
            foreach ((float power, double until) in held) Assert.AreEqual(power, k.PowerAt(until - 0.01), 1e-6, $"{power}/h to {until - Start} h");
            double lastEnd = held[held.Count - 1].until;
            Assert.IsTrue(k.Treated(lastEnd - 0.01));
            Assert.IsFalse(k.Treated(lastEnd + 0.01), "the weakest dose was let go");
            Assert.Less(lastEnd, doses[n - 1].to, "it would have worked on longer");

            // Saved and loaded, the record keeps every step.
            string json = JsonUtility.ToJson(book.Export(t, 0));
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            List<(float power, double until)> back = Steps(loaded.Find(cow.id).Condition(Infection), t);
            Assert.IsTrue(SameSteps(held, back), $"saved [{StepsText(held)}], loaded [{StepsText(back)}]");
        }

        /// <summary>A medicine made for a test, as sound as any: it brings an infection down at once and does nothing per hour after (its effect names hours, as any does).</summary>
        private static AnimalTreatmentData AtOnceOnly(AnimalCatalog cat)
        {
            var t = new AnimalTreatmentData
            {
                id = "fever_powder", name = "Fever powder", description = "Brings a fever down at once, and does nothing after.", route = AnimalTreatmentRoute.Drench,
                dosesPerItem = 2f, minIntervalHours = 6f, qualityMatters = false, items = new List<string> { "ingredient:fever_powder" },
                effects = new List<AnimalTreatmentEffect> { new AnimalTreatmentEffect { condition = Infection, immediate = 5f, perHour = 0f, hours = 12f } },
            };
            cat.AddTreatment(t);
            return t;
        }

        [Test]
        public void AMedicineThatOnlyActsAtOnceLeavesNoTreatmentWorkingSoNothingMendsByIt()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalConditionData c = cat.Condition(Infection);
            AnimalTreatmentData powder = AtOnceOnly(cat);
            AnimalTreatmentEffect e = powder.EffectOn(Infection);
            var errors = new List<string>();
            var warnings = new List<string>();
            powder.Validate(cat, errors, warnings);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.IsEmpty(warnings, "a medicine that only acts at once is sound: " + string.Join("; ", warnings));
            Assert.AreEqual(0f, e.perHour);
            Assert.Greater(e.hours, 0f);

            // Given alone (by its owner, to a cow in a barn), it takes its immediate off and leaves nothing working.
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalSpeciesData sp = cat.Species("cow");
            AnimalCondition k = Ill(cow, cat, Infection, 70f, Start, diagnosed: true);
            AnimalHealth.Treat(cow, sp, powder, cat, rules, 1f, true, "p1", "p1", Start, false, null);
            Assert.AreEqual(70f - e.immediate, k.severity, 1e-4, "it acts at once");
            double look = Start + 1.0;
            Assert.IsFalse(k.Treated(Start) || k.Treated(look), "nothing works on it after: " + StepsText(Steps(k, Start)));
            Assert.IsEmpty(Steps(k, Start), "it adds no step: " + StepsText(Steps(k, Start)));
            // What its owner sees says nothing is working on it.
            AnimalConditionView view = AnimalObservation.View(cow, cat, look, "p1").conditions.Find(v => v.id == Infection);
            Assert.IsNotNull(view, "its owner sees the named illness");
            Assert.IsFalse(view.treated, "and is not told it is treated");
            var lines = new List<string>();
            AnimalObservation.Describe(cow, sp, cat, look, true, lines);
            Assert.AreEqual($"Known illness: infection ({c.StageName(k)}).", lines.Find(l => l.StartsWith("Known illness: infection")), string.Join(" | ", lines));
            StringAssert.DoesNotContain("The treatment is working.", AnimalHealth.Diagnosis(c, k, cat, rules, look, false));

            // Stepped hour by hour with no cause, above Needs Treatment Above (untreated, an infection there does not mend): nothing
            // mends it after the powder alone, nor once the veterinarian's treatment ends (the powder given an hour into it, and
            // again 4 hours before it ends, outlasting it).
            var alone = new List<(double at, string treatment, float strength)> { (0.0, powder.id, 1f) };
            var withVet = new List<(double at, string treatment, float strength)> { (0.0, "vet", 1f), (1.0, powder.id, 1f), (rules.vetHours - 4.0, powder.id, 1f) };
            string byItself = HourByHour(cat, Infection, 70f, 24, alone), afterVet = HourByHour(cat, Infection, 70f, 40, withVet, out AnimalRecord b);
            Assert.IsEmpty(byItself, "the powder alone: " + byItself);
            Assert.IsEmpty(afterVet, "the powder while the veterinarian's treatment works: " + afterVet);
            AnimalCondition kb = b.Condition(Infection);
            double after = Start + rules.vetHours + 2.0;
            Assert.IsFalse(kb.Treated(after), "once the veterinarian's treatment ends, nothing works on: " + StepsText(Steps(kb, Start + rules.vetHours)));
            StringAssert.DoesNotContain("The treatment is working.", AnimalHealth.Diagnosis(c, kb, cat, rules, after, false));
        }

        /// <summary>
        /// Hens in a small coop nobody mucked out until the first could be named (its infection Moderate). From then the coop is
        /// mucked out with fresh straw and the trough scrubbed and refilled every day, and (<paramref name="wash"/>) every hen
        /// that shows it has the vinegar wash every 6 hours (its interval). Returns how many died within 20 days.
        /// </summary>
        private static int CoopOutbreak(int hens, bool wash)
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            var spec = new AnimalPenSpec { id = "coop", name = "coop", kind = AnimalHabitatKind.Coop, area = 9f, feederCapacity = 2000f, waterCapacity = 40f, troughSurface = 0.3f };
            AnimalHerd h = book.EnsurePen(spec, "Test", "", Start);
            var flock = new List<AnimalRecord>();
            for (int i = 0; i < hens; i++) flock.Add(Release(book, ctx, h, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 600 + i));
            Stock(cat, h, "chicken_feed", 1500f, 0.8f, Start);
            string vinegar = "ingredient:vinegar";
            AnimalActor keeper = WithTools(Actor("p1", AnimalRights.All, new Dictionary<string, int> { [Straw] = 1000, [vinegar] = 1000 }, 500f), cat, 1f, "pitchfork");
            int nameable = cat.Condition(Infection).diagnoseFrom;
            double acted = -1.0;
            for (int hr = 1; hr <= 24 * 20; hr++)
            {
                double t = Start + hr;
                AnimalSim.AdvanceHerd(h, t, ctx);
                if (acted < 0.0)
                {
                    if (flock.Exists(a => a.Alive && a.Condition(Infection) != null && a.Condition(Infection).stage >= nameable)) acted = t;
                    // A keeper who forgot only the floor: the hens are watered.
                    else if (hr % 24 == 8) TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "coop" }, keeper, t);
                    if (acted < 0.0) continue;
                }
                if ((t - acted) % 24 == 0)
                {
                    TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "coop", item = Straw }, keeper, t);
                    TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "coop", text = AnimalCommands.TroughText }, keeper, t);
                    TryDo(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "coop" }, keeper, t);
                }
                if (!wash || (t - acted) % 6 != 0) continue;
                foreach (AnimalRecord a in flock)
                {
                    AnimalCondition k = a.Condition(Infection);
                    if (a.Alive && k != null && !k.Recovering && k.shown)
                        Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "coop", animal = a.id, item = vinegar }, keeper, t);
                }
            }
            return flock.FindAll(a => !a.Alive).Count;
        }

        [Test]
        public void TheVinegarWashTurnsAnOutbreakInASmallCoopOnceItIsMuckedOut()
        {
            Assert.Greater(CoopOutbreak(4, false), 0, "once every hen has caught it, cleaning up alone does not save them");
            Assert.AreEqual(0, CoopOutbreak(4, true), "the plain remedy, washed into every hen that shows it, does");
        }

        private const string TooSoon = "Too soon for another dose: it does no more good yet.";
        private const string TooMuch = "Too much, too soon: the dose makes it unwell.";

        /// <summary>The refusal of a dose asked for too soon of a medicine that does no harm (<paramref name="who"/>: "the cow", "it", "the first of them").</summary>
        private static string TooSoonFor(string who, AnimalTreatmentData tr, double wait) =>
            $"Too soon for another dose: {who} can have the {tr.DisplayName.ToLowerInvariant()} again in {AnimalObservation.Wait(wait)}.";

        [Test]
        public void WashesAskedForOneAfterAnotherAreRefusedWithTheWaitUntilTheIntervalHasPassed()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 500f, 0.8f, Start);
            cow.hygiene = 80f; // a clean coat: the wash works at full strength
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash");
            AnimalTreatmentEffect washing = wash.EffectOn(Infection);
            Assert.AreEqual(0f, wash.overdoseShare, "vinegar does no harm: a wash too soon would do nothing at all");
            AnimalCondition k = Ill(cow, cat, Infection, 70f, Start, diagnosed: true);
            string vinegar = "ingredient:vinegar";
            var bag = new Dictionary<string, int> { [vinegar] = 1000 };
            AnimalActor keeper = Actor("p1", AnimalRights.All, bag);
            var request = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = vinegar };

            // The first wash is given (its vinegar taken) and takes its immediate off at once.
            AnimalPlan plan = AnimalCommands.Plan(book, request, keeper, Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.Greater(plan.takeCount, 0);
            bag[vinegar] -= plan.takeCount;
            AnimalResult given = AnimalCommands.Commit(book, plan, All(plan), Start, ctx);
            Assert.IsTrue(given.ok, given.message);
            Assert.IsFalse(given.lines.Contains(TooSoon), string.Join(" | ", given.lines));
            float strength = cow.LastDose(wash.id).strength, off = washing.immediate * strength, once = 70f - off;
            Assert.AreEqual(once, k.severity, 1e-3, "the first wash takes off its immediate at once");
            int left = bag[vinegar];

            // Thirteen more asked for one after another, a few game seconds apart (all within a game minute): each is refused
            // before anything is taken, saying when the cow may have it again.
            var wrong = new List<string>();
            var severities = new List<string>();
            double at = Start;
            for (int i = 1; i < 14; i++)
            {
                at = Start + i / 1000.0;
                float before = k.severity;
                plan = AnimalCommands.Plan(book, request, keeper, at, ctx);
                string expected = TooSoonFor("the cow", wash, wash.minIntervalHours - (at - Start));
                if (!plan.Refused)
                {
                    // Given, as the server would give it: its vinegar is taken.
                    wrong.Add($"wash {i + 1} was given, taking {plan.takeCount} vinegar");
                    bag[vinegar] -= plan.takeCount;
                    AnimalCommands.Commit(book, plan, All(plan), at, ctx);
                }
                else if (plan.refusal.message != expected) wrong.Add($"wash {i + 1}: \"{plan.refusal.message}\", not \"{expected}\"");
                else if (plan.TakesSomething) wrong.Add($"wash {i + 1} is refused, yet takes {plan.takeCount}");
                if (Math.Abs(k.severity - before) > off * 0.01f) wrong.Add($"wash {i + 1}: severity {before:0.###} -> {k.severity:0.###}");
                severities.Add(k.Recovering ? "recovering" : $"{k.severity:0.##}");
            }
            string trace = $"70 -> {once:0.##} -> " + string.Join(" -> ", severities);
            Assert.Less(at - Start, 1.0 / 60.0, "all within a game minute");
            Assert.IsEmpty(wrong, "a wash asked for again so soon is not given: " + string.Join("; ", wrong) + ". " + trace);
            Assert.AreEqual(left, bag[vinegar], "no more vinegar was taken");
            Assert.AreEqual(once, k.severity, off * 0.1f, "nothing more came off at once: " + trace);
            Assert.IsFalse(k.Recovering, "a Severe infection is not cured by washing it over and over: " + trace);
            Assert.AreEqual(0f, cow.toxin, 1e-4, "vinegar is no poison");
            // Its treatment is the first wash's: at the wash's own power, to that wash's end.
            Assert.AreEqual(washing.perHour * strength, k.treatPower, 1e-4);
            Assert.IsTrue(k.Treated(Start + washing.hours - 0.01), "it works to the first wash's end");
            Assert.IsFalse(k.Treated(Start + washing.hours + 0.01), "and no further: the washes refused added nothing");

            // Its interval after the first wash, a wash is given again and does its full good.
            double due = Start + wash.minIntervalHours;
            plan = AnimalCommands.Plan(book, request, keeper, due, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            float then = k.severity;
            AnimalResult again = AnimalCommands.Commit(book, plan, All(plan), due, ctx);
            Assert.IsTrue(again.ok, again.message);
            Assert.IsFalse(again.lines.Contains(TooSoon), string.Join(" | ", again.lines));
            Assert.AreEqual(then - washing.immediate * cow.LastDose(wash.id).strength, k.severity, 1e-3, "a wash at its interval takes its immediate off again");
            Assert.AreEqual(due, cow.LastDose(wash.id).at, 1e-9);
        }

        /// <summary>What a dose did to an animal: the change of each of its values and of its infection's severity, and what the keeper was told.</summary>
        private sealed class DoseDid
        {
            public float health, toxin, upset, comfort, stress, satiety, hydration, severity;
            public readonly List<string> lines = new List<string>();

            public override string ToString() =>
                $"health {health:+0.##;-0.##;0}, toxin {toxin:+0.##;-0.##;0}, upset {upset:+0.##;-0.##;0}, comfort {comfort:+0.##;-0.##;0}, " +
                $"stress {stress:+0.##;-0.##;0}, satiety {satiety:+0.##;-0.##;0}, hydration {hydration:+0.##;-0.##;0}, infection {severity:+0.##;-0.##;0}";
        }

        /// <summary>The toxin a <see cref="Patient"/> carries.</summary>
        private const float PatientToxin = 20f;

        /// <summary>A cow's record (no pen) to dose: half well, hungry and thirsty, stressed, a little poisoned and upset, its coat clean, with a named infection at 50.</summary>
        private static AnimalRecord Patient(AnimalCatalog cat, string id)
        {
            var a = new AnimalRecord
            {
                id = id, owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult, health = 50f, toxin = PatientToxin, upset = 50f, comfort = 50f,
                stress = 50f, satiety = 40f, hydration = 40f, hygiene = 80f,
            };
            Ill(a, cat, Infection, 50f, Start, diagnosed: true);
            return a;
        }

        /// <summary>A dose of a medicine at strength 1 by its owner, straight through <see cref="AnimalHealth.Treat"/>: what it did.</summary>
        private static DoseDid DoseOf(AnimalCatalog cat, AnimalRecord a, string treatment, double at)
        {
            AnimalCondition k = a.Condition(Infection);
            var d = new DoseDid
            {
                health = -a.health, toxin = -a.toxin, upset = -a.upset, comfort = -a.comfort, stress = -a.stress, satiety = -a.satiety, hydration = -a.hydration,
                severity = -(k?.severity ?? 0f),
            };
            AnimalHealth.Treat(a, cat.Species(a.species), cat.Treatment(treatment), cat, cat.Rules, 1f, true, "p1", "p1", at, false, d.lines);
            d.health += a.health;
            d.toxin += a.toxin;
            d.upset += a.upset;
            d.comfort += a.comfort;
            d.stress += a.stress;
            d.satiety += a.satiety;
            d.hydration += a.hydration;
            d.severity += k?.severity ?? 0f;
            return d;
        }

        [Test]
        public void AMedicineGivenAgainWithinItsIntervalDoesNoMoreGoodYetButStillDoesItsHarm()
        {
            AnimalCatalog cat = Catalog();
            AnimalSpeciesData sp = cat.Species("cow");
            AnimalTreatmentData draught = cat.Treatment("healing_draught"), broth = cat.Treatment("broth"), salve = cat.Treatment("healing_salve"),
                charcoal = cat.Treatment("charcoal"), calming = cat.Treatment("calming_tonic"), wash = cat.Treatment("vinegar_wash");
            // Each medicine twice, the second dose halfway through its interval.
            var patients = new Dictionary<string, AnimalRecord>();
            var first = new Dictionary<string, DoseDid>();
            var again = new Dictionary<string, DoseDid>();
            string said = "";
            foreach (AnimalTreatmentData tr in new[] { draught, broth, salve, charcoal, calming, wash })
            {
                AnimalRecord a = patients[tr.id] = Patient(cat, tr.id);
                first[tr.id] = DoseOf(cat, a, tr.id, Start);
                again[tr.id] = DoseOf(cat, a, tr.id, Start + tr.minIntervalHours / 2.0);
                said += $"{tr.id}: {first[tr.id]}; then {again[tr.id]}. ";
            }

            // Given again so soon it does no more good at once: no health, no food or water, no comfort...
            Assert.AreEqual(draught.healthNow, first[draught.id].health, 1e-3, said);
            Assert.AreEqual(0f, again[draught.id].health, 1e-3, "a second healing draught so soon adds no health: " + said);
            Assert.IsTrue(again[draught.id].lines.Contains(TooSoon), string.Join(" | ", again[draught.id].lines));
            Assert.AreEqual(broth.satiety, first[broth.id].satiety, 1e-3, said);
            Assert.AreEqual(broth.hydration, first[broth.id].hydration, 1e-3, said);
            Assert.AreEqual(0f, again[broth.id].satiety, 1e-3, "a second broth so soon neither feeds it: " + said);
            Assert.AreEqual(0f, again[broth.id].hydration, 1e-3, "nor waters it: " + said);
            Assert.AreEqual(salve.comfort, first[salve.id].comfort, 1e-3, said);
            Assert.AreEqual(0f, again[salve.id].comfort, 1e-3, "a second salve so soon soothes no more: " + said);
            // ...no toxin cleared, no upset eased...
            Assert.AreEqual(-PatientToxin * charcoal.toxinCleared, first[charcoal.id].toxin, 1e-3, said);
            Assert.AreEqual(-charcoal.upsetEased, first[charcoal.id].upset, 1e-3, said);
            Assert.AreEqual(0f, again[charcoal.id].toxin, 1e-3, "a second charcoal so soon clears no more toxin: " + said);
            Assert.AreEqual(0f, again[charcoal.id].upset, 1e-3, "nor settles its gut more: " + said);
            // ...and no calm: the handling of the dose is all that is left of the stress, with the overdose's harm.
            AnimalRecord calmed = patients[calming.id];
            float handled = calming.handlingStress * (1f - calmed.genes.temperament);
            Assert.AreEqual(calming.stress + handled, first[calming.id].stress, 1e-3, said);
            Assert.AreEqual(handled, again[calming.id].stress, 1e-3, "a second calming tonic so soon takes no stress off (only its handling adds some): " + said);
            Assert.AreEqual(calming.overdoseShare * AnimalSim.Tolerance(calmed, sp), again[calming.id].toxin, 1e-3, "and it is an overdose: " + said);
            Assert.IsTrue(again[calming.id].lines.Contains(TooMuch), string.Join(" | ", again[calming.id].lines));
            Assert.IsFalse(again[calming.id].lines.Contains(TooSoon), "an overdose says so: " + string.Join(" | ", again[calming.id].lines));
            // A wash again so soon takes nothing off the infection at once, but it still stings.
            Assert.AreEqual(-wash.EffectOn(Infection).immediate, first[wash.id].severity, 1e-3, said);
            Assert.AreEqual(0f, again[wash.id].severity, 1e-3, "a second wash so soon takes nothing off at once: " + said);
            Assert.AreEqual(wash.comfort, again[wash.id].comfort, 1e-3, "but it still stings: " + said);

            // Its interval after the last dose, a dose does its full good again.
            AnimalRecord washed = patients[wash.id], drank = patients[draught.id];
            DoseDid later = DoseOf(cat, washed, wash.id, washed.LastDose(wash.id).at + wash.minIntervalHours);
            Assert.AreEqual(-wash.EffectOn(Infection).immediate, later.severity, 1e-3, "a wash at its interval takes its immediate off again: " + later);
            Assert.AreEqual(wash.comfort, later.comfort, 1e-3, later.ToString());
            Assert.IsFalse(later.lines.Contains(TooSoon), string.Join(" | ", later.lines));
            DoseDid next = DoseOf(cat, drank, draught.id, drank.LastDose(draught.id).at + draught.minIntervalHours);
            Assert.AreEqual(draught.healthNow, next.health, 1e-3, "and a draught adds its health again: " + next);
        }

        [Test]
        public void AWashTooSoonDoesNotPutOffTheNextItsIntervalCountsFromTheLastWashThatDidItsGood()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalConditionData c = cat.Condition(Infection);
            AnimalSpeciesData sp = cat.Species("cow");
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash");
            float immediate = wash.EffectOn(Infection).immediate;
            double interval = wash.minIntervalHours, often = interval - 1.0;
            Assert.IsTrue(often < interval && 2.0 * often >= interval, "washed every 5 hours, every other wash is too soon");

            // Washes straight through Treat on an infection kept at 70: each takes its immediate off, or (too soon) nothing. Returns
            // each wash that did otherwise, or after which its last dose was not its last one that did its good ("" when none).
            string Washes(double[] times, bool[] good)
            {
                AnimalRecord a = Treatable(cat, Infection, 70f, out AnimalCondition k);
                var wrong = new List<string>();
                double lastGood = double.NaN;
                for (int i = 0; i < times.Length; i++)
                {
                    Hold(k, c, 70f);
                    var lines = new List<string>();
                    AnimalHealth.Treat(a, sp, wash, cat, rules, 1f, true, "p1", "p1", Start + times[i], false, lines);
                    float took = 70f - k.severity, expected = good[i] ? immediate : 0f;
                    bool tooSoon = lines.Contains(TooSoon);
                    if (good[i]) lastGood = times[i];
                    if (Math.Abs(took - expected) > 1e-4f || tooSoon == good[i])
                        wrong.Add($"at {times[i]:0.##} h it took {took:0.##} off{(tooSoon ? " (too soon)" : "")}, not {expected:0.##}");
                    else if (Math.Abs(a.LastDose(wash.id).at - (Start + lastGood)) > 1e-9)
                        wrong.Add($"after the wash at {times[i]:0.##} h its last dose is at {a.LastDose(wash.id).at - Start:0.##} h, not at its last one that did its good ({lastGood:0.##} h)");
                }
                return string.Join("; ", wrong);
            }

            // A wash a little early does no good, and puts nothing off: the one at its interval after the last good one does its good.
            string early = Washes(new[] { 0.0, interval - 0.1, interval }, new[] { true, false, true });
            Assert.IsEmpty(early, $"washes at 0, {interval - 0.1:0.#} and {interval:0} h: " + early);
            // Washed every 5 hours for a day, every other wash does its good (10 hours apart), not only the first.
            var schedule = new List<double>();
            var doesGood = new List<bool>();
            for (double t = 0.0; t < 24.0; t += often)
            {
                schedule.Add(t);
                doesGood.Add(schedule.Count % 2 == 1);
            }
            string every = Washes(schedule.ToArray(), doesGood.ToArray());
            Assert.IsEmpty(every, $"washed every {often:0} hours, a wash does its good every {2.0 * often:0} hours: " + every);
        }

        [Test]
        public void AnOverdoseDoesNotPutOffTheNextDoseOfAMedicineThatHarmsWhenGivenTooSoon()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 500f, 0.8f, Start);
            AnimalTreatmentData calming = cat.Treatment("calming_tonic");
            Assert.Greater(calming.overdoseShare, 0f, "a calming tonic too soon is an overdose");
            string tonic = "potion:calming_tonic";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [tonic] = 100 });
            var request = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = tonic };
            float handled = calming.handlingStress * (1f - cow.genes.temperament), tolerance = AnimalSim.Tolerance(cow, cat.Species("cow"));

            // A dose through the commands (planned at its hour, which brings the pen up to date; the cow's stress then set at 90;
            // committed): what the dose itself did, and how strong it was.
            (float stress, float toxin, List<string> lines, float strength) Tonic(double at)
            {
                AnimalPlan plan = AnimalCommands.Plan(book, request, keeper, at, ctx);
                Assert.IsFalse(plan.Refused, "a medicine that harms is given even too soon (an overdose): " + plan.refusal?.message);
                cow.stress = 90f;
                float toxin = cow.toxin;
                AnimalTaken taken = All(plan);
                AnimalResult res = AnimalCommands.Commit(book, plan, taken, at, ctx);
                Assert.IsTrue(res.ok, res.message);
                return (cow.stress - 90f, cow.toxin - toxin, res.lines, AnimalHealth.Strength(calming, taken, plan.plannedCount));
            }
            var first = Tonic(Start);
            var overdose = Tonic(Start + calming.minIntervalHours / 2.0);
            var due = Tonic(Start + calming.minIntervalHours);
            string said = $"at 0 h stress {first.stress:+0.##;-0.##;0}, toxin {first.toxin:+0.##;-0.##;0}; at {calming.minIntervalHours / 2.0:0} h stress {overdose.stress:+0.##;-0.##;0}, " +
                          $"toxin {overdose.toxin:+0.##;-0.##;0} [{string.Join(" | ", overdose.lines)}]; at {calming.minIntervalHours:0} h stress {due.stress:+0.##;-0.##;0}, " +
                          $"toxin {due.toxin:+0.##;-0.##;0} [{string.Join(" | ", due.lines)}]";
            Assert.AreEqual(calming.stress * first.strength + handled, first.stress, 1e-3, "the first tonic calms it: " + said);
            Assert.AreEqual(handled, overdose.stress, 1e-3, "the one too soon calms nothing: " + said);
            Assert.AreEqual(calming.overdoseShare * overdose.strength * tolerance, overdose.toxin, 1e-3, "and poisons: " + said);
            Assert.IsTrue(overdose.lines.Contains(TooMuch), said);
            // Its interval after the first dose, the tonic does its good again: the overdose between put nothing off.
            Assert.AreEqual(calming.stress * due.strength + handled, due.stress, 1e-3, "its interval after the first dose, the tonic calms it again: " + said);
            Assert.AreEqual(0f, due.toxin, 1e-4, "and is no overdose: " + said);
            Assert.IsFalse(due.lines.Contains(TooMuch), said);
        }

        [Test]
        public void DosesOfOtherMedicinesNeverMakeAMedicineForgetItsLastDose()
        {
            AnimalCatalog cat = Catalog();
            AnimalRules rules = cat.Rules;
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            AnimalRecord hen = Release(book, ctx, coop, "p1", "chicken", "leghorn", Start);
            Stock(cat, coop, "chicken_feed", 200f, 0.8f, Start);
            hen.hygiene = 80f; // a clean coat: the wash works at full strength
            AnimalCondition k = Ill(hen, cat, Infection, 70f, Start, diagnosed: true);
            AnimalSpeciesData sp = cat.Species("chicken");
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash");
            string vinegar = "ingredient:vinegar";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [vinegar] = 10 });
            var request = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "coop", animal = hen.id, item = vinegar };
            Do(book, ctx, request, keeper, Start);
            Assert.AreEqual(70f - wash.EffectOn(Infection).immediate, k.severity, 1e-3, "the wash does its good");

            // Doses of eight other medicines within a game minute, each doing its good (more medicines than the history of doses once
            // held, even counting only the doses that did their good), and two of them again too soon.
            string[] others = { "lavender_herb", "charcoal", "ginger_tonic", "broth", "healing_draught", "lavender", "antidote", "electrolytes", "lavender_herb", "charcoal" };
            double t = Start;
            foreach (string id in others)
            {
                t += 0.001;
                AnimalHealth.Treat(hen, sp, cat.Treatment(id), cat, rules, 1f, true, "p1", "p1", t, false, null);
            }
            t += 0.001;
            var held = new List<string>();
            foreach (AnimalDose d in hen.doses) held.Add($"{d.treatment} at {d.at - Start:0.###} h");
            string doses = "its doses: " + string.Join(", ", held);

            // The wash again is still too soon: given (straight through Treat) it takes nothing off at once...
            float before = k.severity;
            var lines = new List<string>();
            AnimalHealth.Treat(hen, sp, wash, cat, rules, 1f, true, "p1", "p1", t, false, lines);
            Assert.AreEqual(before, k.severity, 1e-4, "the doses of others after it never make the hen's last wash forgotten: " + doses);
            Assert.IsTrue(lines.Contains(TooSoon), string.Join(" | ", lines));
            // ...and asked for, it is refused with the wait.
            Assert.AreEqual(TooSoonFor(AnimalSim.NameIn(hen, sp), wash, wash.minIntervalHours - (t - Start)), Refusal(book, ctx, request, keeper, t));
            // It keeps the last dose that did its good of each medicine, once each.
            var expected = new List<string> { wash.id };
            foreach (string id in others)
                if (!expected.Contains(id)) expected.Add(id);
            Assert.AreEqual(expected.Count, hen.doses.Count, doses);
            foreach (string id in expected) Assert.AreEqual(1, hen.doses.FindAll(d => d != null && d.treatment == id).Count, $"{id}: {doses}");
            Assert.AreEqual(Start, hen.LastDose(wash.id).at, 1e-9, "the wash that did its good: " + doses);
            Assert.AreEqual(Start + 0.001, hen.LastDose("lavender_herb").at, 1e-9, "the lavender that did its good, not the one too soon after it: " + doses);

            // A record keeps one dose of each medicine, the last, and at least 16 medicines whatever it is asked to keep.
            var r = new AnimalRecord { id = "r" };
            for (int i = 0; i < 20; i++) r.Dosed(new AnimalDose { treatment = $"medicine_{i}", at = i, strength = 1f }, 1);
            r.Dosed(new AnimalDose { treatment = "MEDICINE_19", at = 30.0, strength = 1f }, 1);
            var names = new List<string>();
            foreach (AnimalDose d in r.doses) names.Add($"{d.treatment} at {d.at:0}");
            string kept = string.Join(", ", names);
            Assert.AreEqual(16, r.doses.Count, kept);
            Assert.IsNull(r.LastDose("medicine_3"), "the medicines dosed longest ago are let go: " + kept);
            Assert.AreEqual(4.0, r.LastDose("medicine_4").at, 1e-9, kept);
            Assert.AreEqual(30.0, r.LastDose("medicine_19").at, 1e-9, "a dose takes the place of the last of the same medicine: " + kept);
        }

        [Test]
        public void GivingToAllHereLeavesOutTheOnesThatHadItTooLatelyAndTakesItemsOnlyForTheRest()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "field", AnimalHabitatKind.Pasture, 400f, Start);
            var flock = new List<AnimalRecord>();
            for (int i = 0; i < 6; i++) flock.Add(Release(book, ctx, h, "p1", "sheep", "merino", Start, GrowthStage.Adult, 70 + i));
            AnimalSpeciesData sp = cat.Species("sheep");
            AnimalTreatmentData broth = cat.Treatment("broth"), calming = cat.Treatment("calming_tonic");
            Assert.AreEqual(0f, broth.overdoseShare, "a broth too soon would do nothing at all");
            string item = "potion:travellers_broth", tonic = "potion:calming_tonic";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [item] = 100, [tonic] = 100 });
            var all = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", item = item };

            // One ewe has the broth an hour before the pen is given it.
            AnimalRecord early = flock[0];
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", animal = early.id, item = item }, keeper, Start);
            double now = Start + 1.0;
            AnimalPlan plan = AnimalCommands.Plan(book, all, keeper, now, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            float rest = 0f, every = 0f;
            foreach (AnimalRecord s in flock)
            {
                every += AnimalHealth.DosesFor(sp, s);
                if (s != early) rest += AnimalHealth.DosesFor(sp, s);
            }
            Assert.AreEqual(AnimalHealth.ItemsFor(broth, rest), plan.takeCount,
                $"the items are for the five that may have it ({AnimalHealth.ItemsFor(broth, rest)}), not for all six ({AnimalHealth.ItemsFor(broth, every)})");
            foreach (AnimalRecord s in flock)
            {
                s.satiety = 30f;
                s.hydration = 30f;
            }
            AnimalTaken taken = All(plan);
            AnimalResult res = AnimalCommands.Commit(book, plan, taken, now, ctx);
            Assert.IsTrue(res.ok, res.message);
            string said = $"\"{res.message}\" [{string.Join(" | ", res.lines)}]";
            Assert.AreEqual("5 animals take the traveller's broth.", res.message, said);
            Assert.IsTrue(res.lines.Contains("One had it too lately and was left out."), said);
            Assert.IsFalse(res.lines.Contains(TooSoon), said);
            float strength = AnimalHealth.Strength(broth, taken, plan.plannedCount);
            foreach (AnimalRecord s in flock)
            {
                bool leftOut = s == early;
                string who = leftOut ? "the one left out" : "each of the rest";
                Assert.AreEqual(leftOut ? 30f : 30f + broth.satiety * strength, s.satiety, 1e-3, $"{who}: {said}");
                Assert.AreEqual(leftOut ? 30f : 30f + broth.hydration * strength, s.hydration, 1e-3, $"{who}: {said}");
                Assert.AreEqual(leftOut ? Start : now, s.LastDose(broth.id).at, 1e-9, $"{who}: {said}");
            }

            // Asked for again at once it is refused (nothing is taken), with the soonest wait; a pen of one says "it".
            AnimalPlan again = AnimalCommands.Plan(book, all, keeper, now, ctx);
            Assert.IsTrue(again.Refused, "the whole pen had it just now");
            Assert.AreEqual(TooSoonFor("the first of them", broth, broth.minIntervalHours - (now - Start)), again.refusal.message);
            Assert.IsFalse(again.TakesSomething, "nothing is taken");
            AnimalHerd paddock = Pen(book, "paddock", AnimalHabitatKind.Pasture, 200f, now);
            AnimalRecord one = Release(book, ctx, paddock, "p1", "sheep", "merino", now, GrowthStage.Adult, 90);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "paddock", animal = one.id, item = item }, keeper, now);
            Assert.AreEqual(TooSoonFor("it", broth, broth.minIntervalHours - 1.0), Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "paddock", item = item }, keeper, now + 1.0));

            // Planned for the whole pen, and every one of them given it meanwhile: nothing is given (the items go back).
            double later = now + broth.minIntervalHours;
            AnimalPlan stale = AnimalCommands.Plan(book, all, keeper, later, ctx);
            Assert.IsFalse(stale.Refused, stale.refusal?.message);
            foreach (AnimalRecord s in flock) AnimalHealth.Treat(s, sp, broth, cat, cat.Rules, 1f, true, "p1", "p1", later, false, null);
            AnimalResult none = AnimalCommands.Commit(book, stale, All(stale), later, ctx);
            Assert.IsFalse(none.ok, none.message);
            Assert.AreEqual("Too soon for another dose.", none.message);

            // Any medicine had too lately leaves that one out of the pen's dose, one that harms when too soon too...
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", animal = flock[1].id, item = tonic }, keeper, later);
            AnimalResult calmed = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", item = tonic }, keeper, later + 0.5);
            Assert.AreEqual("5 animals take the calming tonic.", calmed.message, string.Join(" | ", calmed.lines));
            Assert.IsTrue(calmed.lines.Contains("One had it too lately and was left out."), string.Join(" | ", calmed.lines));
            Assert.AreEqual(later, flock[1].LastDose(calming.id).at, 1e-9, "the one left out");
            // ...but one animal is still given it too soon, as before: an overdose.
            var calmOne = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", animal = flock[2].id, item = tonic };
            AnimalPlan overdose = AnimalCommands.Plan(book, calmOne, keeper, later + 1.0, ctx);
            Assert.IsFalse(overdose.Refused, "a dose that harms when too soon is not refused: " + overdose.refusal?.message);
            Assert.Greater(overdose.takeCount, 0);
            float toxin = flock[2].toxin;
            AnimalResult od = AnimalCommands.Commit(book, overdose, All(overdose), later + 1.0, ctx);
            Assert.IsTrue(od.ok, od.message);
            Assert.IsTrue(od.lines.Contains(TooMuch), string.Join(" | ", od.lines));
            Assert.Greater(flock[2].toxin, toxin, "too much, too soon");
        }

        [Test]
        public void ADoseRecordedAheadOfTheClockMakesNoWait()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 500f, 0.8f, Start);
            cow.hygiene = 80f; // a clean coat: the wash works at full strength
            AnimalCondition k = Ill(cow, cat, Infection, 70f, Start, diagnosed: true);
            AnimalTreatmentData wash = cat.Treatment("vinegar_wash");
            // A world whose clock was set back: the cow's last wash is recorded 500 game hours after now.
            cow.Dosed(new AnimalDose { treatment = wash.id, at = Start + 500.0, strength = 1f }, cat.Rules.keepDoses);
            string vinegar = "ingredient:vinegar";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [vinegar] = 10 });
            var request = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = vinegar };
            AnimalPlan plan = AnimalCommands.Plan(book, request, keeper, Start, ctx);
            Assert.IsFalse(plan.Refused, "a wash now is not too soon: " + plan.refusal?.message);
            float before = k.severity;
            AnimalResult res = AnimalCommands.Commit(book, plan, All(plan), Start, ctx);
            Assert.IsTrue(res.ok, res.message);
            Assert.AreEqual(before - wash.EffectOn(Infection).immediate * cow.LastDose(wash.id).strength, k.severity, 1e-3, "it does its good: " + string.Join(" | ", res.lines));
            Assert.IsFalse(res.lines.Contains(TooSoon), string.Join(" | ", res.lines));
            // It takes the place of the one ahead of the clock, and the next waits its interval from it.
            Assert.AreEqual(1, cow.doses.FindAll(d => d != null && d.treatment == wash.id).Count);
            Assert.AreEqual(Start, cow.LastDose(wash.id).at, 1e-9);
            Assert.AreEqual(wash.minIntervalHours - 1.0, AnimalHealth.WaitBefore(cow, wash, Start + 1.0), 1e-9);
        }

        [Test]
        public void AMedicineForAnIllnessStillHiddenDoesNotSeemToHelpThoughItWorks()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalCondition k = Ill(cow, cat, Infection, 12f, Start);
            Assert.IsFalse(k.shown, "its first stage hides");
            string draught = "potion:healing_draught";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [draught] = 50 });
            AnimalResult res = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = draught }, keeper, Start);
            Assert.IsTrue(res.lines.Contains("It does not seem to help."), "nothing the keeper is told gives away an illness nobody can see: " + string.Join(" | ", res.lines));
            Assert.IsTrue(k.Treated(Start + 1.0), "though the medicine works on it all the same");

            // A flock whose only illness hides: the same.
            AnimalHerd coop = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            for (int i = 0; i < 3; i++) Ill(Release(book, ctx, coop, "p1", "chicken", "leghorn", Start, GrowthStage.Adult, 70 + i), cat, Infection, 10f, Start);
            AnimalResult flock = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "coop", item = draught }, keeper, Start);
            Assert.AreEqual("It does not seem to help.", flock.lines.Count > 0 ? flock.lines[0] : "", string.Join(" | ", flock.lines));
            foreach (AnimalRecord hen in coop.animals) Assert.IsTrue(hen.Condition(Infection).Treated(Start + 1.0), "each hen is treated");
        }

        [Test]
        public void WhatTheVeterinarianOrADoseDidIsInTheHistoryAboveTheVisitOrTheDose()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            ctx.vetAvailable = true;
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord other = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            AnimalCondition k = Ill(cow, cat, Infection, 30f, Start, diagnosed: true);
            string elixir = "potion:restoration_elixir";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [elixir] = 10 });
            keeper.vetFee = (a, sp) => 40L;

            // The veterinarian's visit cures a light infection outright: it is recovering, and its history says so.
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.CallVet, pen = "pen", animal = cow.id, expected = 40 }, keeper, Start);
            Assert.IsTrue(k.Recovering, $"severity {k.severity:0}");
            Assert.IsNotNull(LastHealth(cow, AnimalHealthEventKind.Recovering, Infection), "as after a dose that cures it");
            var lines = new List<string>();
            AnimalObservation.History(cow, cat.Rules, Start + 0.5, lines);
            int visit = lines.IndexOf("- Less than an hour ago: Seen by the veterinarian."), mends = lines.IndexOf("- Less than an hour ago: Recovering from infection.");
            Assert.IsTrue(visit > 0 && mends > 0 && mends < visit, "newest first, what the visit did reads above the visit: " + string.Join(" | ", lines));

            // A keeper's dose: the same.
            Ill(other, cat, Infection, 45f, Start, diagnosed: true);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = other.id, item = elixir }, keeper, Start);
            AnimalObservation.History(other, cat.Rules, Start + 0.5, lines);
            int dose = lines.IndexOf("- Less than an hour ago: Given restoration elixir."), eases = lines.IndexOf("- Less than an hour ago: Infection is easing (Mild).");
            Assert.IsTrue(dose > 0 && eases > 0 && eases < dose, "what the dose did reads above the dose: " + string.Join(" | ", lines));
        }

        [Test]
        public void AFreeVeterinarianCanBeSentForButNotByAVisitor()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            ctx.vetAvailable = true;
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "owner", "cow", "angus", Start);
            AnimalCondition k = Ill(cow, cat, Infection, 85f, Start, AnimalCause.DirtyWater, diagnosed: true);
            Func<AnimalRecord, AnimalSpeciesData, long> free = (a, sp) => 0L;

            AnimalPenView view = AnimalObservation.PenView(book, h, "owner", AnimalRights.All, Start, free, true, "Mild weather.");
            AnimalView mine = view.animals[0];
            Assert.IsTrue(mine.vetCanCome, "a veterinarian who asks nothing can still be sent for");
            Assert.AreEqual(0L, mine.vetFee);
            Assert.IsTrue(AnimalPenView.FromJson(view.ToJson()).animals[0].vetCanCome, "and the window is told so");
            AnimalView paid = AnimalObservation.PenView(book, h, "owner", AnimalRights.All, Start, (a, sp) => 25L, true).animals[0];
            Assert.IsTrue(paid.vetCanCome);
            Assert.AreEqual(25L, paid.vetFee);
            Assert.IsFalse(AnimalObservation.PenView(book, h, "visitor", AnimalRights.Look, Start, free, true).animals[0].vetCanCome, "a visitor may not send for it");
            Assert.IsFalse(AnimalObservation.PenView(book, h, "owner", AnimalRights.All, Start, null, false).animals[0].vetCanCome, "nor may anyone where there is no veterinarian");

            // The free visit is made as any other.
            AnimalActor owner = Actor("owner", AnimalRights.All);
            owner.vetFee = free;
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.CallVet, pen = "pen", animal = cow.id, expected = mine.vetFee }, owner, Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.AreEqual(0L, plan.fee);
            Assert.IsTrue(AnimalCommands.Commit(book, plan, All(plan), Start, ctx).ok);
            Assert.IsTrue(k.byVet, "the veterinarian saw to it");
        }

        [Test]
        public void LavenderGrownOnTheFarmIsTheLavenderRemedy()
        {
            AnimalCatalog cat = Catalog();
            string farm = AgriIds.Produce("lavender");
            AnimalTreatmentData tr = cat.TreatmentOfItem(farm);
            Assert.IsNotNull(tr, $"{farm} is a medicine for animals");
            Assert.AreEqual("lavender_herb", tr.id);
            Assert.AreSame(tr, cat.TreatmentOfItem("ingredient:lavender"), "the same remedy as the bought herb");
            Assert.AreEqual("ingredient:lavender", tr.items[0], "the herb comes first: the keeper's goods and the care kit go on giving it");

            // A keeper who grows it gives it.
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord sheep = Release(book, ctx, h, "p1", "sheep", "merino", Start);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "pen", animal = sheep.id, item = farm },
                Actor("p1", AnimalRights.All, new Dictionary<string, int> { [farm] = 5 }), Start);
            Assert.IsNotNull(sheep.LastDose(tr.id), "it is given as the herb is");
        }

        [Test]
        public void ALookNamesAnIllnessOnlyFromItsDiagnoseFromStage()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalActor keeper = Actor("p1", AnimalRights.All);
            var look = new AnimalRequest { kind = AnimalActionKind.Examine, pen = "pen", animal = cow.id };
            AnimalCondition k = Ill(cow, cat, Infection, 5f, Start, AnimalCause.DirtyWater);
            Assert.IsFalse(k.shown, "an incubating infection does not show");

            AnimalResult res = Do(book, ctx, look, keeper, Start);
            Assert.IsTrue(res.lines.Contains("It seems healthy."), string.Join(" | ", res.lines));
            StringAssert.Contains("just looked", Refusal(book, ctx, look, keeper, Start + 0.1));

            k.severity = 20f;
            k.stage = cat.Condition(Infection).StageAt(20f);
            res = Do(book, ctx, look, keeper, Start + 0.5);
            Assert.IsTrue(res.lines.Exists(l => l.StartsWith("Something is wrong") && l.Contains("too early to say")), string.Join(" | ", res.lines));
            Assert.IsFalse(k.diagnosed, "a cough could be anything");
            Assert.IsNull(LastHealth(cow, AnimalHealthEventKind.Diagnosed));

            k.severity = 45f;
            k.stage = cat.Condition(Infection).StageAt(45f);
            res = Do(book, ctx, look, keeper, Start + 1.0);
            Assert.IsTrue(k.diagnosed, "from its Moderate stage it is named");
            string line = res.lines.Find(l => l.StartsWith("Infection ("));
            Assert.IsNotNull(line, string.Join(" | ", res.lines));
            StringAssert.Contains("foul water", line, "with what likely brought it on");
            StringAssert.Contains("Treat with:", line);
            AnimalHealthEntry e = LastHealth(cow, AnimalHealthEventKind.Diagnosed, Infection);
            Assert.IsNotNull(e, "the diagnosis is in its health history");
            Assert.IsTrue(e.named && e.shown);
            Assert.AreEqual("p1", e.by);
            int diagnosed = cow.healthLog.FindAll(x => x.kind == AnimalHealthEventKind.Diagnosed).Count;
            Do(book, ctx, look, keeper, Start + 2.0);
            Assert.AreEqual(diagnosed, cow.healthLog.FindAll(x => x.kind == AnimalHealthEventKind.Diagnosed).Count, "it is named once");
            Assert.AreEqual(0f, cow.affinity, 1e-4, "a look does not buy affinity");
        }

        [Test]
        public void ALookThatFindsAnIllnessShowingPutsItInItsHistory()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalConditionData c = cat.Condition(Infection);
            AnimalCondition k = Ill(cow, cat, Infection, 5f, Start);
            // Grown into a stage that shows with nobody seeing it yet (as the editor's debug tool can leave it).
            k.severity = 20f;
            k.stage = c.StageAt(20f);
            Assert.IsFalse(k.shown);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "pen", animal = cow.id }, Actor("p1", AnimalRights.All), Start);
            Assert.IsTrue(k.shown, "the look sees it");
            AnimalHealthEntry seen = LastHealth(cow, AnimalHealthEventKind.Showed, Infection);
            Assert.IsNotNull(seen, "and its health history has it from then on, as when it shows by itself");
            Assert.IsTrue(seen.shown);
            Assert.AreEqual("It coughs and its nose runs.", seen.text);
            var lines = new List<string>();
            AnimalObservation.History(cow, cat.Rules, Start + 1.0, lines);
            StringAssert.Contains("It coughs and its nose runs.", string.Join(" | ", lines));
        }

        [Test]
        public void ALookStillTellsANamedIllnessTreatedBelowTheStagesThatShow()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord unseen = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            AnimalConditionData c = cat.Condition(Infection);
            AnimalCondition k = Ill(cow, cat, Infection, 20f, Start, diagnosed: true);
            Ill(unseen, cat, Infection, 10f, Start);
            string elixir = "potion:restoration_elixir";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [elixir] = 10 });
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = elixir }, keeper, Start);
            Assert.IsFalse(c.StageOf(k).visible, $"the dose takes it below the stages that show (severity {k.severity:0.0})");
            Assert.IsFalse(k.Recovering, "though it has not mended yet");

            AnimalResult look = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "barn", animal = cow.id }, keeper, Start + 0.25);
            string line = look.lines.Find(l => l.StartsWith("Infection (Slight):"));
            Assert.IsNotNull(line, "a careful look still tells the illness its keeper knows of: " + string.Join(" | ", look.lines));
            StringAssert.EndsWith("The treatment is working.", line);
            Assert.IsFalse(look.lines.Contains("It seems healthy."));
            AnimalResult other = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "barn", animal = unseen.id }, keeper, Start + 0.25);
            Assert.IsTrue(other.lines.Contains("It seems healthy."), "one nobody has seen stays unsaid: " + string.Join(" | ", other.lines));
        }

        /// <summary>A filthy barn for an hour (its floor and trough fouled), its animals fed and watered.</summary>
        private static void FilthyHour(IAnimalCatalog cat, AnimalHerd h, double now)
        {
            h.pen.dirt = 100f;
            h.pen.waterDirt = 100f;
            h.pen.water = 1000f;
            TopUp(cat, h, "hay", 100f, now);
        }

        [Test]
        public void AnIllnessThatComesBackInAStageThatHidesIsHiddenUntilItShowsThenItsOwnerIsTold()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            cow.name = "Daisy";
            AnimalConditionData c = cat.Condition(Infection);
            // A named infection that showed and is mending...
            AnimalCondition k = Ill(cow, cat, Infection, 0f, Start, diagnosed: true);
            k.shown = true;
            k.recoveringUntil = Start + 24.0;
            // ...back in a filthy barn: it comes back from its start, in the stage that hides.
            double t = Start;
            while (k.Recovering && t < Start + 12.0)
            {
                FilthyHour(cat, h, t);
                t += 1.0;
                ctx.notices.Clear();
                book.BringUpToDate(h, t, ctx);
            }
            Assert.IsFalse(k.Recovering, "it came back");
            Assert.IsFalse(c.StageOf(k).visible);
            Assert.IsFalse(k.shown, "hidden again, as it was at first");
            Assert.IsTrue(k.diagnosed, "but still named, for when it shows");
            Assert.IsEmpty(AnimalObservation.PenView(book, h, "p1", AnimalRights.All, t).animals[0].conditions, "the Health tab names nothing nobody can see");
            var lines = new List<string>();
            AnimalObservation.Describe(cow, cat.Species("cow"), cat, t, true, lines);
            Assert.IsFalse(lines.Exists(l => l.StartsWith("Known illness")), string.Join(" | ", lines));
            AnimalObservation.History(cow, cat.Rules, t, lines);
            StringAssert.DoesNotContain("came back", string.Join(" | ", lines));
            Assert.IsTrue(Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "barn", animal = cow.id }, Actor("p1", AnimalRights.All), t).lines.Contains("It seems healthy."),
                "a look finds nothing yet");
            Assert.IsFalse(ctx.notices.Exists(n => n.kind == AnimalEventKind.FellIll));

            // It shows again: its owner is told the symptom, as at first, and its keepers see it by its name.
            while (!k.shown && t < Start + 24.0)
            {
                FilthyHour(cat, h, t);
                t += 1.0;
                ctx.notices.Clear();
                book.BringUpToDate(h, t, ctx);
            }
            Assert.IsTrue(k.shown && c.StageOf(k).visible, $"it showed (severity {k.severity:0})");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.FellIll && n.text == "Daisy: It coughs and its nose runs."), "its owner is told when it shows");
            Assert.AreEqual(t, LastHealth(cow, AnimalHealthEventKind.Showed, Infection).at, 1e-6, "and its history has it showing again");
            List<AnimalConditionView> known = AnimalObservation.PenView(book, h, "p1", AnimalRights.All, t).animals[0].conditions;
            Assert.AreEqual(1, known.Count);
            Assert.AreEqual("Infection", known[0].name);
        }

        [Test]
        public void AComebackThatHidesAndMendsUnseenStillEndsWithItsOwnerToldItIsOver()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            cow.name = "Daisy";
            AnimalConditionData c = cat.Condition(Infection);
            // A named infection that showed and is mending, back in a filthy barn: it comes back from its start, in the stage that hides.
            AnimalCondition k = Ill(cow, cat, Infection, 0f, Start, diagnosed: true);
            k.shown = true;
            k.recoveringUntil = Start + 24.0;
            double t = Start;
            while (k.Recovering && t < Start + 12.0)
            {
                FilthyHour(cat, h, t);
                t += 1.0;
                book.BringUpToDate(h, t, ctx);
            }
            Assert.IsFalse(k.Recovering, "it came back");
            Assert.IsFalse(k.shown || c.StageOf(k).visible, "hidden, in the stage that hides");
            Assert.IsTrue(k.diagnosed, "but still named");

            // From then on the barn is spotless and the cow kept clean: it mends without ever showing, and clears.
            var told = new List<AnimalNotice>();
            while (cow.Condition(Infection) != null && t < Start + 24.0 * 10)
            {
                h.pen.dirt = 0f;
                h.pen.waterDirt = 0f;
                h.pen.water = 1000f;
                cow.hygiene = 100f;
                TopUp(cat, h, "hay", 100f, t);
                t += 1.0;
                ctx.notices.Clear();
                book.BringUpToDate(h, t, ctx);
                told.AddRange(ctx.notices);
                if (cow.Condition(Infection) == null) break;
                Assert.IsFalse(k.shown, $"it mends unseen ({c.StageName(k)}, severity {k.severity:0.0}, at {t - Start:0} h)");
                Assert.IsEmpty(AnimalObservation.PenView(book, h, "p1", AnimalRights.All, t).animals[0].conditions, "the Health tab names nothing nobody can see");
            }
            string notices = told.Count == 0 ? "none" : string.Join(" | ", told.ConvertAll(n => $"{n.kind}: {n.text}"));
            Assert.IsNull(cow.Condition(Infection), $"it cleared ({Conditions(cow)} at {t - Start:0} h)");
            Assert.IsFalse(told.Exists(n => n.kind == AnimalEventKind.FellIll), "it never showed: " + notices);
            // Its owner knew of it: they are told it is over, and its history says so (the comeback nobody saw stays out of it).
            Assert.IsTrue(told.Exists(n => n.kind == AnimalEventKind.Healed && n.animal == cow.id && n.text == "Daisy has got over its infection."),
                "its owner, who knew of the illness, is told it is over (told: " + notices + ")");
            var lines = new List<string>();
            AnimalObservation.History(cow, cat.Rules, t, lines);
            Assert.IsTrue(lines.Contains("- Less than an hour ago: Over its infection."), string.Join(" | ", lines));
            StringAssert.DoesNotContain("came back", string.Join(" | ", lines));
        }

        [Test]
        public void TheHistoryTellsAnIllnessBySymptomsUntilItIsNamed()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 500f, 0.8f, Start);
            Ill(cow, cat, Infection, 5f, Start, AnimalCause.DirtyWater);
            h.pen.waterDirt = 100f; // it drinks foul water, so the infection grows
            AnimalSim.AdvanceHerd(h, Start + 8.0, ctx);
            AnimalCondition k = cow.Condition(Infection);
            Assert.IsTrue(k.shown, $"it showed (severity {k.severity:0})");
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.FellIll && n.text.Contains("coughs")), "its owner is told the symptom");
            var lines = new List<string>();
            AnimalObservation.History(cow, cat.Rules, Start + 8.0, lines);
            string before = string.Join(" | ", lines);
            StringAssert.Contains("It coughs and its nose runs.", before);
            StringAssert.DoesNotContain("nfection", before, "not named before a look names it");
            StringAssert.DoesNotContain("hidden", before, "nothing from before it showed");

            AnimalSim.AdvanceHerd(h, Start + 24.0, ctx);
            Assert.GreaterOrEqual(k.stage, cat.Condition(Infection).diagnoseFrom, $"severity {k.severity:0}");
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "pen", animal = cow.id }, Actor("p1", AnimalRights.All), Start + 24.0);
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.History, pen = "pen", animal = cow.id }, Actor("p1", AnimalRights.All), Start + 25.0, ctx);
            Assert.IsNotNull(plan.answer);
            string after = string.Join(" | ", plan.answer.lines);
            StringAssert.Contains("Found to have infection", after, "named from the look on");
            StringAssert.Contains("It coughs and its nose runs.", after, "the earlier entries keep their words");
            Assert.LessOrEqual(plan.answer.lines.Count, 30);
            StringAssert.Contains("Looked over", after, "the care history has the look");
            StringAssert.Contains("Days:", after);
            StringAssert.Contains("Not your animal", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.History, pen = "pen", animal = cow.id }, Actor("p2", AnimalRights.Look), Start + 25.0));
        }

        // ---------------------------------------------------------------- death, slaughter and the remains
        [Test]
        public void AnAnimalThatDiesOfAnInfectionIsRecordedSoAndItsRemainsShowIt()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd sick = Pen(book, "sick", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalHerd good = Pen(book, "good", AnimalHabitatKind.Pasture, 48f, Start, grass: 1.5f);
            AnimalHerd poor = Pen(book, "poor", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord dying = Release(book, ctx, sick, "p1", "cow", "angus", Start, GrowthStage.Adult, 1);
            AnimalRecord well = Release(book, ctx, good, "p1", "cow", "angus", Start, GrowthStage.Adult, 2);
            AnimalRecord neglected = Release(book, ctx, poor, "p1", "cow", "angus", Start, GrowthStage.Adult, 3);
            Stock(cat, sick, "hay", 1000f, 0.8f, Start);
            Stock(cat, good, "alfalfa", 1000f, 0.9f, Start);
            Stock(cat, poor, "low_quality_feed", 2000f, 0.5f, Start);
            Ill(dying, cat, Infection, 95f, Start, AnimalCause.DirtyWater, diagnosed: true);
            dying.health = 40f;
            AnimalActor keeper = Keeper(cat);
            for (int hr = 1; hr <= 24 * 12; hr++)
            {
                double t = Start + hr;
                AnimalSim.AdvanceHerd(sick, t, ctx);
                AnimalSim.AdvanceHerd(good, t, ctx);
                AnimalSim.AdvanceHerd(poor, t, ctx);
                if (hr % 24 == 8) KeepWell(book, ctx, good, keeper, t, hr / 24);
            }
            Assert.AreEqual(AnimalStatus.Dead, dying.status, "an untreated severe infection kills");
            Assert.AreEqual(AnimalDeathCause.Illness, dying.deathCause);
            Assert.AreEqual(Infection, dying.deathCondition);
            AnimalHealthEntry died = LastHealth(dying, AnimalHealthEventKind.Died);
            Assert.IsNotNull(died);
            StringAssert.StartsWith("Died of infection", died.text);
            Assert.IsTrue(ctx.notices.Exists(n => n.kind == AnimalEventKind.Died && n.animal == dying.id), "its owner is told");

            double now = Start + 24 * 12;
            AnimalActor owner = Actor("p1", AnimalRights.All);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Slaughter, pen = "good", animal = well.id }, owner, now);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Slaughter, pen = "poor", animal = neglected.id }, owner, now);
            AnimalRemainsReport ill = AnimalRemains.Report(dying, cat.Species("cow"), cat, now, cat.Rules);
            AnimalRemainsReport fine = AnimalRemains.Report(well, cat.Species("cow"), cat, now, cat.Rules);
            AnimalRemainsReport bad = AnimalRemains.Report(neglected, cat.Species("cow"), cat, now, cat.Rules);
            Assert.Greater(ill.taint, 0.15f, "an infection taints the carcass");
            Assert.AreEqual(0f, fine.taint, 1e-4);
            Assert.Less(ill.rearing, fine.rearing, "dying of illness lowers the quality of what it gives");
            Assert.Greater(fine.rearing, bad.rearing, $"a well raised animal gives better than a neglected one ({fine.rearing:0.00} against {bad.rearing:0.00})");
            Assert.Less(bad.lean, fine.lean, "a badly fed one is leaner");
            Assert.AreEqual("Slaughtered", fine.cause);
            StringAssert.StartsWith("Died of infection", ill.cause);
            Assert.AreEqual("cow", fine.anatomy);
        }

        [Test]
        public void ADeathNoIllnessCausedEndsItsHealthHistoryAndASlaughterIsRecordedOnce()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 2000f, 0.8f, Start);
            double t = Start;
            while (cow.Alive && t < Start + 24 * 10)
            {
                h.pen.water = 0f; // nobody fills the trough
                t += 1.0;
                AnimalSim.AdvanceHerd(h, t, ctx);
            }
            Assert.AreEqual(AnimalDeathCause.Dehydration, cow.deathCause, "it died of thirst, not of an illness");
            Assert.AreEqual("", cow.deathCondition);
            var lines = new List<string>();
            AnimalObservation.History(cow, cat.Rules, t, lines);
            Assert.AreEqual("- Less than an hour ago: Died (of thirst).", lines[1], "its health history ends with its death: " + string.Join(" | ", lines));

            // A slaughter is recorded once, as a slaughter.
            AnimalRecord sheep = Release(book, ctx, h, "p1", "sheep", "suffolk", t);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Slaughter, pen = "barn", animal = sheep.id }, Actor("p1", AnimalRights.All), t);
            List<AnimalHealthEntry> died = sheep.healthLog.FindAll(e => e.kind == AnimalHealthEventKind.Died);
            Assert.AreEqual(1, died.Count);
            Assert.AreEqual("Slaughtered", died[0].text);
        }

        [Test]
        public void AnOldIllAnimalWhoseOwnerIsAwayDiesOfOldAge()
        {
            foreach (bool away in new[] { true, false })
            {
                AnimalCatalog cat = Catalog();
                AnimalSimContext ctx = Ctx(cat, present: !away);
                var book = new AnimalBook(cat, "test");
                AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 100f, Start);
                AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 3);
                Stock(cat, h, "hay", 4000f, 0.8f, Start);
                AnimalSpeciesData sp = cat.Species("cow");
                cow.bornAt = Start - sp.lifespanHours + 24.0; // a day from the end of its life
                Ill(cow, cat, Infection, 75f, Start);
                cow.health = 30f;
                book.BringUpToDate(h, Start + 24 * 5, ctx);
                Assert.AreEqual(AnimalStatus.Dead, cow.status, away ? "away" : "here");
                if (away)
                {
                    // Hardship never takes it below the away floor: only old age can, so old age is what it dies of.
                    Assert.AreEqual(AnimalDeathCause.OldAge, cow.deathCause);
                    Assert.AreEqual("", cow.deathCondition);
                    Assert.AreEqual("Died of old age", AnimalRemains.Report(cow, sp, cat, cow.diedAt, cat.Rules).cause);
                    Assert.AreEqual("Died (of old age)", LastHealth(cow, AnimalHealthEventKind.Died).text);
                }
                else
                {
                    Assert.AreEqual(AnimalDeathCause.Illness, cow.deathCause, "with its owner here, the infection kills it sooner");
                    Assert.AreEqual(Infection, cow.deathCondition);
                }
            }
        }

        [Test]
        public void OnlyAnIllnessOfTheWholeBodyThatShowsTaintsTheCarcass()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalSpeciesData sp = cat.Species("cow");
            var cases = new (string condition, float severity, bool taints)[]
            {
                ("hoof_rot", 30f, false), // it stays in the hoof
                ("hoof_rot", 80f, false),
                ("parasites", 17f, false), // worms not yet showing
                ("parasites", 30f, true),
                (Infection, 12f, false), // a fever not yet showing
                (Infection, 20f, true),
            };
            foreach (var x in cases)
            {
                AnimalHerd h = Pen(book, x.condition + x.severity, AnimalHabitatKind.Pasture, 48f, Start);
                AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "angus", Start, GrowthStage.Adult, 7);
                AnimalCondition k = Ill(cow, cat, x.condition, x.severity, Start);
                AnimalRemainsReport r = AnimalRemains.Report(cow, sp, cat, Start, cat.Rules);
                string what = $"{x.condition} at {x.severity:0} ({cat.Condition(x.condition).StageName(k)})";
                if (x.taints) Assert.AreEqual(x.severity / 100f * 0.8f, r.taint, 1e-4, what + " taints its meat");
                else Assert.AreEqual(0f, r.taint, 1e-4, what + " leaves its meat fit to eat");
                if (x.condition == "parasites") Assert.AreEqual(x.severity / 100f, r.parasites, 1e-4, "the hide shows worms, hidden ones too");
            }

            // What killed it: hoof rot leaves the meat as it is; an infection taints it at least a little, however it ended.
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord lame = Release(book, ctx, field, "p1", "cow", "angus", Start, GrowthStage.Adult, 8);
            Ill(lame, cat, "hoof_rot", 90f, Start);
            lame.deathCondition = "hoof_rot";
            AnimalSim.Die(lame, AnimalDeathCause.Illness, Start, ctx, sp);
            Assert.AreEqual(0f, AnimalRemains.Report(lame, sp, cat, Start, cat.Rules).taint, 1e-4, "hoof rot that killed it stays in the hoof");
            AnimalRecord fevered = Release(book, ctx, field, "p1", "cow", "angus", Start, GrowthStage.Adult, 9);
            Ill(fevered, cat, Infection, 10f, Start);
            fevered.deathCondition = Infection;
            AnimalSim.Die(fevered, AnimalDeathCause.Illness, Start, ctx, sp);
            Assert.AreEqual(0.15f, AnimalRemains.Report(fevered, sp, cat, Start, cat.Rules).taint, 1e-4, "an infection that killed it taints it");
        }

        [Test]
        public void AnIllnessTheGameNoLongerHasDoesNotKeepAnAnimalFromHealing()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 100f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Stock(cat, h, "hay", 4000f, 0.8f, Start);
            AnimalSpeciesData sp = cat.Species("cow");
            // An illness a save still has after the content dropped it (renamed by a modder, or built-in illnesses turned off):
            // it is kept on the record, but nothing of it acts.
            var gone = new AnimalCondition { id = "old_mod_illness", severity = 60f, stage = 1, since = Start };
            Assert.IsNull(cat.Condition(gone.id));
            float rearing = AnimalRemains.Report(cow, sp, cat, Start, cat.Rules).rearing;
            cow.conditions.Add(gone);
            float marked = AnimalRemains.Report(cow, sp, cat, Start, cat.Rules).rearing;
            cow.health = 50f;
            book.BringUpToDate(h, Start + 24 * 10, ctx);
            Assert.Greater(cow.health, 90f, $"a well kept cow heals ({Conditions(cow)})");
            Assert.IsTrue(cow.conditions.Contains(gone), "it is kept (it acts again if its data comes back)");
            Assert.AreEqual(rearing, marked, 1e-5, "its remains are not marked down for it");
            var lines = new List<string>();
            AnimalHealth.VetVisit(cow, sp, cat, cat.Rules, Start + 24 * 10, "p1", "p1", lines);
            Assert.IsTrue(lines.Contains("Nothing wrong with it."), "the veterinarian finds nothing the game knows of: " + string.Join(" | ", lines));
        }

        [Test]
        public void SlaughterAndButcheringAreTheOwnersAndButcheringChangesNothingUntilTheCarcassIsLaidOut()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord sheep = Release(book, ctx, h, "p1", "sheep", "suffolk", Start);
            AnimalActor owner = Actor("p1", AnimalRights.All);
            AnimalActor friend = Actor("friend", AnimalRights.All);
            var slaughter = new AnimalRequest { kind = AnimalActionKind.Slaughter, pen = "pen", animal = sheep.id };
            var butcher = new AnimalRequest { kind = AnimalActionKind.Butcher, pen = "pen", animal = sheep.id };

            StringAssert.Contains("Only its owner", Refusal(book, ctx, slaughter, friend, Start), "a carer may feed it, never kill it");
            StringAssert.Contains("alive", Refusal(book, ctx, butcher, owner, Start));
            rules.allowSlaughter = false;
            Assert.IsTrue(AnimalCommands.Plan(book, slaughter, owner, Start, ctx).Refused, "the rules may forbid it");
            rules.allowSlaughter = true;

            AnimalResult killed = Do(book, ctx, slaughter, owner, Start);
            StringAssert.Contains("butchered", killed.message);
            Assert.AreEqual(AnimalStatus.Dead, sheep.status);
            Assert.AreEqual(AnimalDeathCause.Slaughtered, sheep.deathCause);
            Assert.IsTrue(sheep.events.Exists(e => e.kind == AnimalEventKind.Slaughtered), "its history says so");
            Assert.IsFalse(sheep.events.Exists(e => e.kind == AnimalEventKind.Died), "once, as slaughtered");
            Assert.IsFalse(ctx.notices.Exists(n => n.animal == sheep.id && n.kind == AnimalEventKind.Died), "its owner did it: nobody is told it died");

            StringAssert.Contains("Only its owner", Refusal(book, ctx, butcher, friend, Start), "the carcass is its owner's");
            AnimalPlan plan = AnimalCommands.Plan(book, butcher, owner, Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.IsNotNull(plan.remains, "the plan reports the remains to lay out");
            Assert.AreEqual("sheep", plan.remains.anatomy);
            Assert.AreEqual(sheep.id, plan.remains.animal);
            string record = JsonUtility.ToJson(sheep);
            string pen = JsonUtility.ToJson(h.pen);
            int revision = h.pen.revision;
            AnimalResult res = AnimalCommands.Commit(book, plan, default, Start, ctx);
            Assert.IsTrue(res.ok, res.message);
            Assert.AreEqual(record, JsonUtility.ToJson(sheep), "the commit changes nothing of the animal: the caller removes it once the carcass is laid out");
            Assert.AreSame(sheep, book.Find(sheep.id));
            h.pen.revision = revision; // only the picture's revision moves
            Assert.AreEqual(pen, JsonUtility.ToJson(h.pen), "nor of its pen");

            // A species with no anatomy is not butchered: its remains are cleared.
            AnimalSpeciesData goat = cat.Species("goat");
            goat.harvestAnatomy = "";
            AnimalRecord g = Release(book, ctx, h, "p1", "goat", "boer", Start);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Slaughter, pen = "pen", animal = g.id }, owner, Start);
            StringAssert.Contains("clear the remains", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.Butcher, pen = "pen", animal = g.id }, owner, Start));
        }

        // ---------------------------------------------------------------- the pen
        [Test]
        public void MuckingOutNeedsAForkCleansLaysStrawGivesManureAndDisturbsTheHerd()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, ctx, h, "p2", "cow", "holstein", Start, GrowthStage.Adult, 2);
            Assert.AreEqual(rules.freshBedding, h.pen.bedding, 1e-3, "a bedded pen the world has just come to know is laid with fresh bedding");
            h.pen.dirt = 80f;
            h.pen.bedding = 20f;
            var muck = new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "barn", item = Straw };
            var bag = new Dictionary<string, int> { [Straw] = 20 };
            StringAssert.Contains("pitchfork or a shovel", Refusal(book, ctx, muck, Actor("p1", AnimalRights.All, bag), Start));
            AnimalActor keeper = WithTools(Actor("p1", AnimalRights.All, bag), cat, 1f, "pitchfork");
            AnimalPlan plan = AnimalCommands.Plan(book, muck, keeper, Start, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            Assert.AreEqual(AnimalIds.Tool("pitchfork"), plan.wearKey);
            Assert.AreEqual(Straw, plan.takeItem);
            Assert.AreEqual(AnimalHabitat.StrawNeeded(h.pen, h.spec, rules), plan.takeCount, "as much straw as makes the bedding fresh");
            float stressA = a.stress, stressB = b.stress;
            AnimalResult res = AnimalCommands.Commit(book, plan, All(plan), Start, ctx);
            Assert.IsTrue(res.ok, res.message);
            Assert.Less(h.pen.dirt, 30f, "the floor is forked clean");
            Assert.Greater(h.pen.bedding, 80f, "fresh straw is laid");
            AnimalItemChange manure = plan.give.Find(c => c.item == rules.manureItem);
            Assert.Greater(manure.count, 0, "the muck is manure for the fields");
            Assert.Greater(a.stress, stressA, "the work disturbs the animals a little");
            Assert.Less(a.stress - stressA, 5f);
            Assert.Greater(b.stress, stressB);
            StringAssert.Contains("mucked out", a.careLog[a.careLog.Count - 1].text, "it is in their care history");
            Assert.AreEqual(1, a.days[a.days.Count - 1].ownerCares, "its owner's care");
            Assert.AreEqual(0, b.days[b.days.Count - 1].ownerCares, "another's animal is cared for, not by its owner");
            Assert.AreEqual("p1", h.pen.cleanedBy);
            StringAssert.Contains("just mucked", Refusal(book, ctx, muck, keeper, Start + 0.1));

            // A pasture takes no straw; a clean pen needs nothing.
            AnimalHerd field = Pen(book, "field", AnimalHabitatKind.Pasture, 48f, Start);
            StringAssert.Contains("takes no bedding", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "field", item = Straw }, keeper, Start));
            StringAssert.Contains("clean", Refusal(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "field" }, keeper, Start));
        }

        [Test]
        public void MuckingOutAFreshBarnWithNoStrawSaysStrawIsWhatIsMissingAndFreshStrawWaitsItsTurn()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            book.BringUpToDate(h, Start + 0.5, ctx);
            // A new barn: its floor clean, its bedding only flattened (the rules' Fresh Bedding). Mucking out is offered...
            AnimalPenView view = AnimalObservation.PenView(book, h, "p1", AnimalRights.All, Start + 0.5);
            Assert.IsTrue(view.cleanReady, $"{view.cleanliness} {view.beddingState}");
            // ...and with a fork but no straw, the answer says what is missing.
            var muck = new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "barn" };
            Assert.AreEqual("The floor is clean: carry straw and tick it beside Muck out to lay fresh bedding.",
                Refusal(book, ctx, muck, WithTools(Actor("p1", AnimalRights.All), cat, 1f, "pitchfork"), Start + 0.5));

            // With straw on a floor with nothing to fork out, straw is laid, and its care history says just that.
            AnimalActor straw = WithTools(Actor("p1", AnimalRights.All, new Dictionary<string, int> { [Straw] = 20 }), cat, 1f, "pitchfork");
            var lay = new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "barn", item = Straw };
            h.pen.dirt = 0f;
            Do(book, ctx, lay, straw, Start + 0.5);
            Assert.Greater(h.pen.bedding, 80f);
            Assert.AreEqual("Fresh straw was laid by p1.", cow.careLog[cow.careLog.Count - 1].text);

            // Some straw laid on worn bedding: more is not offered nor taken until the bedding's own wait is over.
            h.pen.bedding = 40f;
            h.pen.lastBeddedAt = -1.0;
            h.pen.lastCleanedAt = -1.0;
            Do(book, ctx, lay, WithTools(Actor("p1", AnimalRights.All, new Dictionary<string, int> { [Straw] = 2 }), cat, 1f, "pitchfork"), Start + 1.0);
            Assert.Less(h.pen.bedding, 90f, "not enough to make it fresh");
            Assert.IsFalse(AnimalObservation.PenView(book, h, "p1", AnimalRights.All, Start + 2.0).cleanReady, "mucking out is not offered while the straw just laid waits its turn");
            Assert.AreEqual("Fresh straw was laid 1 hour ago.", Refusal(book, ctx, lay, straw, Start + 2.0));
            double after = Start + 1.0 + rules.beddingCooldownHours;
            Assert.IsTrue(AnimalObservation.PenView(book, h, "p1", AnimalRights.All, after).cleanReady, "after it, it is offered again");
            Assert.IsFalse(AnimalCommands.Plan(book, lay, straw, after, ctx).Refused);
        }

        [Test]
        public void PouringWaterMixesAndScrubbingTheTroughChangesFoulWater()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            Release(book, ctx, h, "p1", "cow", "holstein", Start);
            h.pen.water = 1000f;
            h.pen.waterDirt = 60f;
            AnimalActor keeper = Actor("p1", AnimalRights.All, water: 500f);
            AnimalResult poured = Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "pen" }, keeper, Start);
            Assert.AreEqual(1500f, h.pen.water, 1e-2, "pouring never empties the trough");
            Assert.AreEqual(40f, h.pen.waterDirt, 1e-2, "clean water mixes the dirt down");
            Assert.IsTrue(poured.lines.Exists(l => l.Contains("scrub out the trough")), string.Join(" | ", poured.lines));

            var scrub = new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "pen", text = AnimalCommands.TroughText };
            StringAssert.Contains("Only a keeper", Refusal(book, ctx, scrub, Actor("stranger", AnimalHousing.Public), Start), "a stranger on public land may not empty someone's trough");
            AnimalResult scrubbed = Do(book, ctx, scrub, Actor("p1", AnimalHousing.Public), Start);
            Assert.AreEqual(0f, h.pen.water, 1e-4, "the foul water is tipped out");
            Assert.AreEqual(0f, h.pen.waterDirt, 1e-4, "and the trough scrubbed");
            StringAssert.Contains("Fill it", string.Join(" ", scrubbed.lines));
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "pen" }, keeper, Start);
            Assert.AreEqual(0f, h.pen.waterDirt, 1e-4, "fresh well water is clear");
            StringAssert.Contains("clear", Refusal(book, ctx, scrub, keeper, Start), "clear water needs no changing");

            // Water from a swamp brings its dirt.
            var swamp = Actor("p1", AnimalRights.All, water: 500f);
            swamp.waterKind = (int)WaterSourceKind.Swamp;
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "pen" }, swamp, Start);
            Assert.Greater(h.pen.waterDirt, 5f, "swamp water is murky");
        }

        /// <summary>A player carrying water of a kind (from a swamp, a river, a well).</summary>
        private static AnimalActor Carrying(string key, AnimalRights rights, WaterSourceKind kind, float litres)
        {
            AnimalActor actor = Actor(key, rights, water: litres);
            actor.waterKind = (int)kind;
            return actor;
        }

        [Test]
        public void AStrangerOnPublicLandMayNotMakeATroughMurkyButMayFillADryOne()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "field", AnimalHabitatKind.Pasture, 120f, Start);
            Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            var pour = new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "field" };
            AnimalActor stranger = Carrying("stranger", AnimalHousing.Public, WaterSourceKind.Swamp, 160f);

            // Clean water in other players' trough: a passer-by on land nobody rents may not make it murky...
            h.pen.water = 40f;
            h.pen.waterDirt = 2f;
            Assert.AreEqual("Only a keeper of an animal here may pour such water into it.", Refusal(book, ctx, pour, stranger, Start));
            Assert.AreEqual(2f, h.pen.waterDirt, 1e-4);
            // ...though water the game calls clear is theirs to pour.
            Do(book, ctx, pour, Carrying("stranger", AnimalHousing.Public, WaterSourceKind.River, 160f), Start);
            Assert.Less(h.pen.waterDirt, rules.tipOutAbove, $"river water ({h.pen.waterDirt:0.0})");

            // A dry trough takes any water (better than none), and the passer-by is not told to scrub what they may not.
            h.pen.water = 0f;
            h.pen.waterDirt = 0f;
            AnimalResult dry = Do(book, ctx, pour, stranger, Start);
            Assert.GreaterOrEqual(h.pen.waterDirt, rules.tipOutAbove, "swamp water is murky");
            Assert.IsTrue(dry.lines.Contains("The water is still murky."), string.Join(" | ", dry.lines));
            Assert.IsFalse(dry.lines.Exists(l => l.Contains("scrub")), string.Join(" | ", dry.lines));

            // A keeper of an animal here may pour it, and is told how to change it.
            h.pen.water = 40f;
            h.pen.waterDirt = 2f;
            AnimalResult own = Do(book, ctx, pour, Carrying("p1", AnimalHousing.Public, WaterSourceKind.Swamp, 160f), Start);
            Assert.GreaterOrEqual(h.pen.waterDirt, rules.tipOutAbove);
            Assert.IsTrue(own.lines.Exists(l => l.Contains("scrub out the trough")), string.Join(" | ", own.lines));
        }

        /// <summary>A pasture the size of S1's (8 x 6 m) whose trough holds <paramref name="litres"/>, two of p1's cows drinking from it.</summary>
        private static AnimalHerd Trough(AnimalBook book, AnimalSimContext ctx, string id, float litres)
        {
            var spec = new AnimalPenSpec { id = id, name = id, kind = AnimalHabitatKind.Pasture, area = 48f, feederCapacity = 120f, waterCapacity = litres, troughSurface = 1.2f, grassGrowth = 1.5f };
            AnimalHerd h = book.EnsurePen(spec, "Test", "", Start);
            Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            return h;
        }

        /// <summary>
        /// A player on public land pours water of a kind into a trough, <paramref name="litres"/> carried each time,
        /// <paramref name="times"/> times (each pour planned, and committed when allowed; <paramref name="before"/> runs before
        /// each). Returns the pours done, the refusals and the first one's words, and each pour done that left the trough at or
        /// above Tip Out Above with more dirt than before.
        /// </summary>
        private static (int done, int refused, string refusal, List<string> murkier) Pours(AnimalBook book, AnimalSimContext ctx, AnimalHerd h, string who,
            WaterSourceKind kind, float litres, int times, Action before = null)
        {
            var pour = new AnimalRequest { kind = AnimalActionKind.FillWater, pen = h.pen.id };
            int done = 0, refused = 0;
            string refusal = null;
            var murkier = new List<string>();
            for (int i = 0; i < times; i++)
            {
                before?.Invoke();
                float dirt = h.pen.waterDirt;
                AnimalPlan plan = AnimalCommands.Plan(book, pour, Carrying(who, AnimalHousing.Public, kind, litres), Start, ctx);
                if (plan.Refused)
                {
                    refused++;
                    refusal ??= plan.refusal.message;
                    continue;
                }
                AnimalResult res = AnimalCommands.Commit(book, plan, All(plan), Start, ctx);
                Assert.IsTrue(res.ok, res.message);
                done++;
                if (h.pen.waterDirt >= book.Rules.tipOutAbove && h.pen.waterDirt > dirt) murkier.Add($"{dirt:0.00} to {h.pen.waterDirt:0.00}");
            }
            return (done, refused, refusal, murkier);
        }

        [Test]
        public void AStrangerMayNotMakeATroughMurkyALittleAtATime()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            const string onlyAKeeper = "Only a keeper of an animal here may pour such water into it.";
            float swamp = AnimalHealth.KindDirt((int)WaterSourceKind.Swamp), nearlyMurky = rules.tipOutAbove - 1f;
            Assert.Greater(swamp, rules.tipOutAbove, "swamp water is murky");

            // (a) A big trough (the S1 pasture's, 192 L) holding 40 L nearly murky: a passer-by pours swamp water into it 0.4 L at a time...
            AnimalHerd field = Trough(book, ctx, "field", 192f);
            field.pen.water = 40f;
            field.pen.waterDirt = nearlyMurky;
            var small = Pours(book, ctx, field, "stranger", WaterSourceKind.Swamp, 0.4f, 300);
            // (b) ...or a small trough kept nearly full: its room makes each pour small, whatever the passer-by carries.
            AnimalHerd tub = Trough(book, ctx, "tub", 40f);
            tub.pen.waterDirt = nearlyMurky;
            var topped = Pours(book, ctx, tub, "stranger", WaterSourceKind.Swamp, 160f, 200, () => tub.pen.water = tub.spec.waterCapacity - 0.4f);
            string what = $"(a) {small.done} pours of 0.4 L took the trough from dirt {nearlyMurky:0} to {field.pen.waterDirt:0.0}, {small.refused} refused; " +
                          $"(b) {topped.done} pours into 0.4 L of room took it to {tub.pen.waterDirt:0.0}, {topped.refused} refused (Tip Out Above {rules.tipOutAbove:0})";
            Assert.Less(field.pen.waterDirt, rules.tipOutAbove, "a stranger's small pours never make the trough murky: " + what);
            Assert.Less(tub.pen.waterDirt, rules.tipOutAbove, "nor pours the trough's room makes small: " + what);
            Assert.IsEmpty(small.murkier, "no pour done leaves it murky and dirtier: " + what);
            Assert.IsEmpty(topped.murkier, "no pour done leaves it murky and dirtier: " + what);
            Assert.Greater(small.done, 0, "pours that leave it short of murky are let be: " + what);
            Assert.Greater(topped.done, 0, what);
            Assert.AreEqual(onlyAKeeper, small.refusal, what);
            Assert.AreEqual(onlyAKeeper, topped.refusal, what);

            // (c) Cleaner water into murky water is theirs to pour (it mixes the dirt down), and so is swamp water into fouler water.
            field.pen.water = 40f;
            field.pen.waterDirt = rules.tipOutAbove * 2f;
            var well = Pours(book, ctx, field, "stranger", WaterSourceKind.Well, 0.4f, 20);
            Assert.AreEqual(20, well.done, well.refusal);
            Assert.Less(field.pen.waterDirt, rules.tipOutAbove * 2f, "well water mixes the dirt down");
            float fouler = swamp + rules.tipOutAbove;
            field.pen.waterDirt = fouler;
            var intoFouler = Pours(book, ctx, field, "stranger", WaterSourceKind.Swamp, 0.4f, 20);
            Assert.AreEqual(20, intoFouler.done, intoFouler.refusal);
            Assert.Less(field.pen.waterDirt, fouler, "swamp water makes fouler water less foul");

            // (d) A keeper of an animal here pours what they like, a little at a time too.
            field.pen.water = 40f;
            field.pen.waterDirt = nearlyMurky;
            var own = Pours(book, ctx, field, "p1", WaterSourceKind.Swamp, 0.4f, 300);
            Assert.AreEqual(0, own.refused, own.refusal);
            Assert.GreaterOrEqual(field.pen.waterDirt, rules.tipOutAbove, "their own animals' trough is theirs to make murky");
        }

        [Test]
        public void AStrangerMayNotMakeABigTroughMurkyByPoursTooSmallToMeasure()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            const string onlyAKeeper = "Only a keeper of an animal here may pour such water into it.";
            // A trough of 3000 L holding 2000 L just short of murky: 0.05 L of swamp water raises it by less than a thousandth.
            AnimalHerd pond = Trough(book, ctx, "pond", 3000f);
            float swamp = AnimalHealth.KindDirt((int)WaterSourceKind.Swamp), nearlyMurky = rules.tipOutAbove - 0.01f;
            pond.pen.water = 2000f;
            pond.pen.waterDirt = nearlyMurky;
            Assert.Less(0.05f * (swamp - nearlyMurky) / 2000f, 0.001f, "each pour raises the dirt by less than a thousandth");

            // A passer-by on land nobody rents pours it again and again: never into murky water.
            var tiny = Pours(book, ctx, pond, "stranger", WaterSourceKind.Swamp, 0.05f, 400);
            string what = $"{tiny.done} pours of 0.05 L took the trough from dirt {nearlyMurky:0.00} to {pond.pen.waterDirt:0.0000}, {tiny.refused} refused (Tip Out Above {rules.tipOutAbove:0})";
            Assert.Less(pond.pen.waterDirt, rules.tipOutAbove, "a stranger's pours never make it murky, however small: " + what);
            Assert.IsEmpty(tiny.murkier, "no pour done leaves it murky and dirtier: " + what);
            Assert.Greater(tiny.done, 0, "pours that leave it short of murky are let be: " + what);
            Assert.AreEqual(onlyAKeeper, tiny.refusal, what);

            // Cleaner water into murky water stays theirs to pour (it mixes the dirt down)...
            float murky = rules.tipOutAbove + 5f;
            pond.pen.waterDirt = murky;
            var well = Pours(book, ctx, pond, "stranger", WaterSourceKind.Well, 0.05f, 20);
            Assert.AreEqual(20, well.done, well.refusal);
            Assert.Less(pond.pen.waterDirt, murky, "well water mixes the dirt down");
            // ...and a keeper of an animal here pours what they like, however little at a time.
            pond.pen.waterDirt = nearlyMurky;
            var own = Pours(book, ctx, pond, "p1", WaterSourceKind.Swamp, 0.05f, 400);
            Assert.AreEqual(0, own.refused, own.refusal);
            Assert.GreaterOrEqual(pond.pen.waterDirt, rules.tipOutAbove, "their own animals' trough is theirs to make murky");
        }

        // ---------------------------------------------------------------- saving
        /// <summary>A world as Phase 1 (save version 1) wrote it: a coop with a hen and a pasture with a sheep, a day and a half in.</summary>
        private const string Phase1Save = @"
            {""version"":1,""world"":""phase1-world"",""savedUtc"":1700000000,""now"":78,""serial"":2,""animals"":[
            {""id"":""a1-3f3107"",""owner"":""p1"",""ownerName"":""Ann"",""species"":""chicken"",""breed"":""leghorn"",""name"":""Henny"",""sex"":1,
             ""genes"":{""size"":0.6674228,""growth"":0.52360547,""efficiency"":0.5763347,""resistance"":0.6897479,""fertility"":0.45355788,""temperament"":0.68752635,""productionQuality"":0.46501237,""productionRate"":0.8471088,""longevity"":0.5158915,""coat"":0,""pattern"":0,""rare"":0,""seed"":4242},
             ""bornAt"":-144,""acquiredAt"":48,""origin"":""bought"",""pen"":""coop"",""status"":0,""deathCause"":0,""diedAt"":-1,""livedTo"":78,""development"":223.64072,""stage"":4,
             ""health"":100,""satiety"":76.80002,""hydration"":77.5,""nutrition"":80.440384,""happiness"":64.29042,""stress"":9.2496605,""affinity"":0,""hygiene"":80,""comfort"":60,""toxin"":0,""upset"":0,
             ""intake"":{""energy"":7.02578,""protein"":3.794493,""fiber"":1.3255161,""minerals"":0.3612439,""vitamins"":0.21048681},
             ""need"":{""energy"":8.180908,""protein"":4.331067,""fiber"":1.4436882,""minerals"":0.43310663,""vitamins"":0.28873795},""primed"":true,""lastFedAt"":-1,""lastWateredAt"":-1,""feedings"":[],
             ""days"":[{""day"":2,""hours"":24,""satiety"":80.57502,""hydration"":85.1875,""nutrition"":79.85163,""health"":100,""minHealth"":100,""happiness"":58.913578,""stress"":14.826271,""eaten"":97.799934,""favorite"":0,""bad"":0,""starvingHours"":0,""parchedHours"":0,""ownerAwayHours"":0},
                       {""day"":3,""hours"":6,""satiety"":84.80001,""hydration"":88.75,""nutrition"":86.15622,""health"":100,""minHealth"":100,""happiness"":63.980972,""stress"":9.570535,""eaten"":0,""favorite"":0,""bad"":0,""starvingHours"":0,""parchedHours"":0,""ownerAwayHours"":0}],
             ""events"":[{""at"":48,""kind"":0,""text"":""Let out into Coop."",""by"":""p1""}],""revision"":31},
            {""id"":""a2-6a7fb6"",""owner"":""p1"",""ownerName"":""Ann"",""species"":""sheep"",""breed"":""merino"",""name"":""Dolly"",""sex"":1,
             ""genes"":{""size"":0.60760283,""growth"":0.8111486,""efficiency"":0.16752592,""resistance"":0.73377717,""fertility"":0.5585387,""temperament"":0.41381294,""productionQuality"":0.93241256,""productionRate"":0.5359076,""longevity"":0.48207843,""coat"":0,""pattern"":0,""rare"":0,""seed"":777},
             ""bornAt"":0,""acquiredAt"":48,""origin"":""bought"",""pen"":""field"",""status"":0,""deathCause"":0,""diedAt"":-1,""livedTo"":78,""development"":79.873146,""stage"":2,
             ""health"":100,""satiety"":84,""hydration"":74.666664,""nutrition"":86.46865,""happiness"":71.50416,""stress"":9.2496605,""affinity"":0,""hygiene"":80,""comfort"":60,""toxin"":0,""upset"":0,
             ""intake"":{""energy"":22.113918,""protein"":6.041242,""fiber"":39.901905,""minerals"":1.1433594,""vitamins"":1.8869469},
             ""need"":{""energy"":26.139824,""protein"":7.319154,""fiber"":41.82372,""minerals"":1.3069912,""vitamins"":1.8297884},""primed"":true,""lastFedAt"":-1,""lastWateredAt"":-1,""feedings"":[],
             ""days"":[{""day"":2,""hours"":24,""satiety"":80.12501,""hydration"":83.79166,""nutrition"":79.992905,""health"":100,""minHealth"":100,""happiness"":63.64386,""stress"":14.826271,""eaten"":66,""favorite"":66,""bad"":0,""starvingHours"":0,""parchedHours"":0,""ownerAwayHours"":0},
                       {""day"":3,""hours"":6,""satiety"":86,""hydration"":82.22222,""nutrition"":86.94096,""health"":100,""minHealth"":100,""happiness"":71.637146,""stress"":9.570535,""eaten"":33,""favorite"":33,""bad"":0,""starvingHours"":0,""parchedHours"":0,""ownerAwayHours"":0}],
             ""events"":[{""at"":48,""kind"":0,""text"":""Let out into Field."",""by"":""p1""}],""revision"":31}],
            ""pens"":[
            {""id"":""coop"",""scene"":""Farm"",""property"":"""",""feeder"":[{""food"":""chicken_feed"",""portions"":58.606052,""quality"":0.8,""since"":48,""by"":""p1""}],""water"":39.49116,""grass"":0,""waterKind"":2,""livedTo"":78,""revision"":32,
             ""spec"":{""id"":""coop"",""name"":""Coop"",""kind"":2,""area"":9,""feederCapacity"":200,""waterCapacity"":40,""troughSurface"":0,""capacityOverride"":0,""grassGrowth"":0,""grassCapacity"":3,""grassFood"":""fresh_grass"",""refillLitresPerHour"":0}},
            {""id"":""field"",""scene"":""Farm"",""property"":"""",""feeder"":[{""food"":""hay"",""portions"":30,""quality"":0.7,""since"":48,""by"":""p1""}],""water"":186.27199,""grass"":144,""waterKind"":2,""livedTo"":78,""revision"":32,
             ""spec"":{""id"":""field"",""name"":""Field"",""kind"":1,""area"":48,""feederCapacity"":120,""waterCapacity"":192,""troughSurface"":1.2,""capacityOverride"":0,""grassGrowth"":1.5,""grassCapacity"":3,""grassFood"":""fresh_grass"",""refillLitresPerHour"":0}}],
            ""done"":[
            {""player"":""p1"",""seq"":1,""token"":""tok-1"",""at"":5,""ok"":true,""message"":""Henny (female, Leghorn) is yours now."",""animal"":""a1-3f3107""}]}";

        [Test]
        public void APhase1SaveLoadsWithTheNewFieldsAtTheirDefaults()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            var book = new AnimalBook(cat, "test");
            AnimalWorldSave save = JsonUtility.FromJson<AnimalWorldSave>(Phase1Save.Trim());
            Assert.AreEqual(1, save.version);
            book.Import(save);
            Assert.AreEqual(2, book.Serial);
            AnimalRecord hen = book.Find("a1-3f3107"), sheep = book.Find("a2-6a7fb6");
            Assert.IsNotNull(hen);
            Assert.IsNotNull(sheep);
            Assert.AreEqual("Henny", hen.name);
            Assert.AreEqual(76.80002f, hen.satiety, 1e-4, "what Phase 1 knew is kept");
            foreach (AnimalRecord a in new[] { hen, sheep })
            {
                Assert.AreEqual(100f, a.hooves, "sound hooves");
                Assert.AreEqual(60f, a.welfare, "a fair welfare until its first step");
                Assert.AreEqual(1f, a.absorbFactor);
                Assert.IsNotNull(a.conditions);
                Assert.IsEmpty(a.conditions, "no illnesses");
                Assert.IsNotNull(a.exposures);
                Assert.IsNotNull(a.care);
                Assert.IsNotNull(a.careLog);
                Assert.IsNotNull(a.healthLog);
                Assert.IsNotNull(a.doses);
                Assert.IsNotNull(a.life);
                Assert.IsFalse(a.life.primed, "its life record starts at its first step");
                Assert.AreEqual("", a.deathCondition);
                Assert.AreEqual(-1.0, a.lastExaminedAt);
                Assert.AreEqual(-1.0, a.lastVetAt);
                Assert.AreEqual(-1.0, a.lastOwnerCareAt);
                Assert.AreEqual(AnimalDiscomfort.None, a.discomfort);
            }
            AnimalPenRecord coop = book.Pen("coop"), field = book.Pen("field");
            Assert.AreEqual(rules.freshBedding, coop.bedding, 1e-3, "a bedded pen of an old save is laid with fresh bedding");
            Assert.AreEqual(0f, field.bedding, 1e-3, "a pasture takes none");
            Assert.AreEqual(0f, coop.dirt);
            Assert.AreEqual(0f, coop.waterDirt);
            Assert.AreEqual(0f, field.parasites);
            Assert.AreEqual(-1.0, coop.lastCleanedAt);
            Assert.AreEqual("", coop.cleanedBy);
            Assert.AreEqual(0.9f, coop.spec.Shelter, 1e-4, "an old pen spec takes its shelter from its kind");
            Assert.IsTrue(coop.spec.UsesBedding);
            Assert.IsTrue(book.WasDone("p1", "tok-1", out _), "requests done are remembered");

            // It lives on: its welfare and life record start from where it is.
            AnimalSimContext ctx = Ctx(cat);
            book.Advance(78.0 + 24.0, ctx, null, int.MaxValue);
            Assert.IsTrue(hen.life.primed);
            Assert.IsTrue(sheep.life.primed);
            Assert.AreNotEqual(60f, hen.welfare, "worked out at its first step");
            Assert.AreEqual(AnimalStatus.Alive, hen.status);
            Assert.AreEqual(AnimalStatus.Alive, sheep.status);
            Assert.Greater(coop.dirt, 0f, "the coop gets dirty as it is lived in");

            // Saved again, it is a save of the current version (5 since Phase 5 added crates, pen levels and machines).
            AnimalWorldSave again = book.Export(78.0 + 24.0, 0);
            Assert.AreEqual(AnimalWorldSave.CurrentVersion, again.version);
            Assert.AreEqual(5, AnimalWorldSave.CurrentVersion);
        }

        [Test]
        public void TheDayAPhase1SaveWasMadeInKeepsSensibleMeansAndItsEarlierDaysSayNothingOfTheCoat()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 100f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 3);
            Stock(cat, h, "hay", 4000f, 0.8f, Start);
            // A day and a half lived, then saved as Phase 1 saved: its days kept no hygiene, comfort nor welfare.
            double saved = Start + 36.0, end = Start + 48.0;
            book.BringUpToDate(h, saved, ctx);
            AnimalWorldSave save = JsonUtility.FromJson<AnimalWorldSave>(JsonUtility.ToJson(book.Export(saved, 0)));
            save.version = 1;
            foreach (AnimalRecord r in save.animals)
                foreach (AnimalDay old in r.days)
                {
                    old.hygiene = old.comfort = old.welfare = old.illness = 0f;
                    old.minWelfare = 100f;
                }
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(save);
            loaded.Herd("barn").spec = h.spec;
            AnimalRecord back = loaded.Find(cow.id);

            // The rest of that day, lived after the load, against the same day never saved.
            loaded.BringUpToDate(loaded.Herd("barn"), end, Ctx(cat));
            book.BringUpToDate(h, end, ctx);
            int day = (int)Math.Floor(saved / 24.0);
            AnimalDay truth = cow.days.Find(d => d.day == day), kept = back.days.Find(d => d.day == day);
            Assert.AreEqual(truth.welfare, kept.welfare, 3f, "its welfare that day starts from how it was when loaded, not from 0");
            Assert.AreEqual(truth.hygiene, kept.hygiene, 3f, "its coat");
            Assert.AreEqual(truth.comfort, kept.comfort, 3f, "its comfort");
            Assert.AreEqual(0f, back.days.Find(d => d.day == day - 1).hygiene, "an earlier day is left as Phase 1 kept it");
            var lines = new List<string>();
            AnimalObservation.History(back, cat.Rules, end, lines);
            string yesterday = lines.Find(l => l.StartsWith("- Yesterday:")), before = lines.Find(l => l.StartsWith("- 2 days ago:"));
            StringAssert.Contains("clean", yesterday, "the day it was loaded in reads as it was");
            Assert.IsFalse(before.Contains("clean") || before.Contains("dirty"), "nothing is made up of the coat on a day Phase 1 kept: " + before);
        }

        [Test]
        public void AVersion2SaveRoundTripsEveryNewField()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 48f, Start);
            AnimalRecord a = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 1);
            AnimalRecord b = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 2);
            Stock(cat, h, "hay", 500f, 0.8f, Start);
            AnimalActor keeper = Keeper(cat);
            keeper.count = id => 10;
            AnimalSim.AdvanceHerd(h, Start + 30.0, ctx);
            Do(book, ctx, Care(a, "brush"), keeper, Start + 30.0);
            Ill(b, cat, Infection, 45f, Start + 30.0, AnimalCause.DirtyHabitat);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Examine, pen = "barn", animal = b.id }, keeper, Start + 30.0);
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = b.id, item = "potion:healing_draught" }, keeper, Start + 30.0);
            h.pen.dirt = 40f;
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.CleanPen, pen = "barn", item = Straw }, keeper, Start + 30.0);
            h.pen.waterDirt = 33f;
            h.pen.parasites = 12f;
            AnimalSim.AdvanceHerd(h, Start + 40.0, ctx);
            Assert.IsNotEmpty(b.conditions);
            Assert.IsNotEmpty(b.healthLog);
            Assert.IsNotEmpty(b.doses);
            Assert.IsNotEmpty(a.care);
            Assert.IsNotEmpty(a.careLog);
            Assert.IsTrue(a.life.primed);

            string json = JsonUtility.ToJson(book.Export(Start + 40.0, 0));
            var loaded = new AnimalBook(cat, "test");
            loaded.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            string read = JsonUtility.ToJson(JsonUtility.FromJson<AnimalWorldSave>(json));
            Assert.AreEqual(read, JsonUtility.ToJson(loaded.Export(Start + 40.0, 0)), "every field comes back as it was: loading leaves a current record exactly as it is");
            AnimalRecord back = loaded.Find(b.id);
            Assert.AreEqual(b.Condition(Infection).severity, back.Condition(Infection).severity, 1e-4);
            Assert.AreEqual(b.Condition(Infection).treatedUntil, back.Condition(Infection).treatedUntil, 1e-6);
            Assert.AreEqual(h.pen.manureOwed, loaded.Pen("barn").manureOwed, 1e-5);
        }

        // ---------------------------------------------------------------- time
        [Test]
        public void IllnessAndDirtCaughtUpEndExactlyAsWatchedHourByHour()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "coop", AnimalHabitatKind.Coop, 9f, Start);
            for (int i = 0; i < 6; i++) Release(book, ctx, h, "p" + (i % 2), "chicken", "leghorn", Start, GrowthStage.Adult, 300 + i);
            Stock(cat, h, "chicken_feed", 1000f, 0.8f, Start);
            Ill(h.animals[0], cat, Infection, 60f, Start);
            string json = JsonUtility.ToJson(book.Export(Start, 0));
            var slept = new AnimalBook(cat, "test");
            slept.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            slept.Herd("coop").spec = h.spec;
            var spread = new AnimalBook(cat, "test");
            spread.Import(JsonUtility.FromJson<AnimalWorldSave>(json));
            spread.Herd("coop").spec = h.spec;

            double end = Start + 24 * 6;
            for (int hour = 1; hour <= 24 * 6; hour++) AnimalSim.AdvanceHerd(h, Start + hour, ctx);
            AnimalSim.AdvanceHerd(slept.Herd("coop"), end, Ctx(cat));
            AnimalSimContext c3 = Ctx(cat);
            for (int frame = 0; frame < 10000 && spread.Herd("coop").pen.livedTo < end - 1e-6; frame++) spread.Advance(end, c3, null, 5);

            int ill = 0;
            foreach (AnimalBook other in new[] { slept, spread })
            {
                foreach (AnimalRecord watched in h.animals)
                {
                    AnimalRecord o = other.Find(watched.id);
                    Assert.AreEqual(watched.status, o.status);
                    Assert.AreEqual(watched.health, o.health, 1e-3, "health");
                    Assert.AreEqual(watched.hygiene, o.hygiene, 1e-3, "hygiene");
                    Assert.AreEqual(watched.comfort, o.comfort, 1e-3, "comfort");
                    Assert.AreEqual(watched.stress, o.stress, 1e-3, "stress");
                    Assert.AreEqual(watched.welfare, o.welfare, 1e-3, "welfare");
                    Assert.AreEqual(watched.hooves, o.hooves, 1e-3, "hooves");
                    Assert.AreEqual(watched.life.welfare, o.life.welfare, 1e-3, "life");
                    Assert.AreEqual(watched.conditions.Count, o.conditions.Count, "illnesses");
                    for (int i = 0; i < watched.conditions.Count; i++)
                    {
                        Assert.AreEqual(watched.conditions[i].id, o.conditions[i].id);
                        Assert.AreEqual(watched.conditions[i].severity, o.conditions[i].severity, 1e-3, "severity");
                        Assert.AreEqual(watched.conditions[i].since, o.conditions[i].since, 1e-6, "onset");
                        Assert.AreEqual(watched.conditions[i].recoveringUntil, o.conditions[i].recoveringUntil, 1e-6);
                    }
                    Assert.AreEqual(watched.exposures.Count, o.exposures.Count, "exposures");
                    for (int i = 0; i < watched.exposures.Count; i++) Assert.AreEqual(watched.exposures[i].value, o.exposures[i].value, 1e-3, "exposure");
                    if (watched.conditions.Count > 0) ill++;
                }
                AnimalPenRecord p = other.Pen("coop");
                Assert.AreEqual(h.pen.dirt, p.dirt, 1e-3, "dirt");
                Assert.AreEqual(h.pen.bedding, p.bedding, 1e-3, "bedding");
                Assert.AreEqual(h.pen.waterDirt, p.waterDirt, 1e-3, "trough");
                Assert.AreEqual(h.pen.parasites, p.parasites, 1e-3, "worms");
            }
            Assert.Greater(ill, 2, "the comparison had illness in it (the sick hen and the ones it gave it to)");
        }

        // ---------------------------------------------------------------- what is shown
        [Test]
        public void WhatIsShownOfHealthStaysCoarse()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            ctx.vetAvailable = true;
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord known = Release(book, ctx, h, "owner", "cow", "angus", Start, GrowthStage.Adult, 1);
            AnimalRecord hidden = Release(book, ctx, h, "owner", "cow", "angus", Start, GrowthStage.Adult, 2);
            Ill(known, cat, Infection, 85f, Start, AnimalCause.DirtyWater, diagnosed: true);
            Ill(hidden, cat, "parasites", 10f, Start, AnimalCause.Worms);
            Func<AnimalRecord, AnimalSpeciesData, long> fee = (a, sp) => 25L;

            AnimalPenView mine = AnimalObservation.PenView(book, h, "owner", AnimalRights.All, Start, fee, true, "Mild weather.");
            AnimalPenView theirs = AnimalObservation.PenView(book, h, "visitor", AnimalRights.Look, Start, fee, true, "Mild weather.");
            AnimalView mk = mine.animals.Find(v => v.id == known.id), mh = mine.animals.Find(v => v.id == hidden.id);
            Assert.AreEqual(1, mk.conditions.Count, "its owner sees what a look named");
            Assert.AreEqual("Infection", mk.conditions[0].name);
            Assert.AreEqual("Severe", mk.conditions[0].stage);
            Assert.IsTrue(mk.conditions[0].needsVet);
            Assert.AreEqual(25L, mk.vetFee);
            Assert.IsNotEmpty(mk.care, "and the care it takes");
            Assert.IsEmpty(mh.conditions, "nothing of an illness nobody named");
            Assert.That(mk.welfare, Is.InRange(0, 4), "welfare as a level");
            Assert.That(mk.hygiene, Is.InRange(0, 4));
            Assert.IsTrue((mk.looks & (int)AnimalLooks.Sick) != 0, "it looks sick");
            AnimalView tk = theirs.animals.Find(v => v.id == known.id);
            Assert.IsEmpty(tk.conditions, "a visitor sees the body, not the diagnosis");
            Assert.IsEmpty(tk.care);
            Assert.AreEqual(0L, tk.vetFee);

            foreach (AnimalPenView view in new[] { mine, theirs })
            {
                string json = view.ToJson();
                StringAssert.DoesNotContain("severity", json, "severities never leave the server");
                StringAssert.DoesNotContain("exposure", json, "nor the build-up of an illness");
                StringAssert.DoesNotContain("\"toxin\"", json);
                StringAssert.DoesNotContain("\"genes\"", json);
                StringAssert.DoesNotContain("\"owner\":", json);
                StringAssert.DoesNotContain("parasites", json, "an illness that has not shown is not named, not even by its id");
                StringAssert.DoesNotContain("Worms", json);
            }

            // The history shows an illness only once it showed, and names it only once it was named.
            var lines = new List<string>();
            AnimalObservation.History(hidden, cat.Rules, Start + 1.0, lines);
            string history = string.Join(" | ", lines);
            StringAssert.DoesNotContain("orm", history, "the worms in a cow nobody looked at are not in its history");
            StringAssert.DoesNotContain("hidden", history);
        }

        [Test]
        public void AnHourAgoReadsOneHour()
        {
            Assert.AreEqual("less than an hour", AnimalObservation.Age(0.5));
            Assert.AreEqual("1 hour", AnimalObservation.Age(1.0));
            Assert.AreEqual("1 hour", AnimalObservation.Age(1.9));
            Assert.AreEqual("2 hours", AnimalObservation.Age(2.0));
            Assert.AreEqual("47 hours", AnimalObservation.Age(47.9));
            Assert.AreEqual("2 days", AnimalObservation.Age(48.0));
        }

        // ---------------------------------------------------------------- the built-in content
        [Test]
        public void TheBuiltInHealthAndCareContentIsSound()
        {
            AnimalCatalog cat = Catalog();
            Assert.AreEqual(9, cat.AllConditions.Count, "nine illnesses");
            var errors = new List<string>();
            var warnings = new List<string>();
            foreach (AnimalConditionData c in cat.AllConditions) c.Validate(errors, warnings);
            foreach (AnimalTreatmentData t in cat.AllTreatments) t.Validate(cat, errors, warnings);
            foreach (AnimalToolData t in cat.AllTools) t.Validate(errors, warnings);
            AnimalWelfare.Validate(cat.Rules.welfare, errors, warnings);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.IsEmpty(warnings, string.Join("; ", warnings));
            foreach (AnimalConditionData c in cat.AllConditions)
            {
                Assert.IsTrue(c.Stage(0).from <= 0f, c.id);
                Assert.IsNotEmpty(AnimalHealth.TreatmentsFor(cat, c), $"something treats {c.id}");
                bool plain = false;
                foreach (AnimalTreatmentData t in cat.AllTreatments)
                    if (t.EffectOn(c.id) != null && !t.items.Exists(i => i.StartsWith("potion:"))) plain = true;
                bool mends = c.courseWithoutCause < 0f && c.needsTreatmentAbove > 0f;
                Assert.IsTrue(plain || mends, $"{c.id}: a remedy that needs no brewing, or it mends by itself");
            }
            foreach (AnimalSpeciesData s in cat.AllSpecies)
            {
                Assert.AreEqual(AnimalDatabase.SpeciesDataVersion, s.dataVersion, s.id);
                Assert.IsNotEmpty(s.harvestAnatomy, s.id);
                Assert.IsNotNull(s.CareAction("pet"), $"{s.id} can be petted");
                foreach (AnimalCareActionSpec a in s.care.actions)
                    if (a.NeedsTool) Assert.AreNotEqual("a tool", AnimalCare.ToolNames(cat, a.tools), $"{s.id}'s {a.Id} names a tool that exists");
                foreach (AnimalSusceptibility x in s.susceptibility) Assert.IsNotNull(cat.Condition(x.condition), $"{s.id} is prone to '{x.condition}'");
                Assert.GreaterOrEqual(s.care.selfGroomPerHour, s.care.hygieneLossPerHour, $"{s.id} keeps itself clean at rest");
            }
            Assert.AreEqual(0f, cat.Species("chicken").SusceptibilityTo(cat.Condition("hoof_rot")), "a hen has no hooves to rot");
            Assert.AreEqual(1.4f, cat.Species("sheep").SusceptibilityTo(cat.Condition("parasites")), 1e-4);
            Assert.AreEqual("charcoal", cat.TreatmentOfItem("craftstock:Fuel:charcoal")?.id);
            Assert.AreEqual("soap", cat.ToolOfItem("craftstock:Supply:soap")?.id);
            Assert.AreEqual("panacea", cat.TreatmentOfItem("potion:panacea")?.id);
            Assert.IsNotNull(cat.Treatment("panacea").EffectOn("hoof_rot"), "the panacea acts on every illness");
        }

        // ---------------------------------------------------------------- the last review's gaps (round 4)

        [Test]
        public void AWaitIsToldRoundedUpSoAKeeperWhoComesBackThenMayGiveIt()
        {
            Assert.AreEqual("less than an hour", AnimalObservation.Wait(0.5));
            Assert.AreEqual("1 hour", AnimalObservation.Wait(1.0));
            Assert.AreEqual("2 hours", AnimalObservation.Wait(1.01));
            Assert.AreEqual("6 hours", AnimalObservation.Wait(5.99));
            Assert.AreEqual("6 hours", AnimalObservation.Wait(6.0));
            Assert.AreEqual("24 hours", AnimalObservation.Wait(23.5));
            Assert.AreEqual("3 days", AnimalObservation.Wait(49.0));
            // A wait a hair over a whole hour (the rounding of the clock) is that hour; from 48 hours it is told in days.
            Assert.AreEqual("1 hour", AnimalObservation.Wait(1.0 + 1e-12));
            Assert.AreEqual("6 hours", AnimalObservation.Wait(6.0 + 1e-12));
            Assert.AreEqual("47 hours", AnimalObservation.Wait(47.0));
            Assert.AreEqual("2 days", AnimalObservation.Wait(48.0));

            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            cow.hygiene = 80f;
            Ill(cow, cat, Infection, 70f, Start, diagnosed: true);
            const string vinegar = "ingredient:vinegar";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [vinegar] = 1000 });
            var wash = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = vinegar };
            AnimalTreatmentData tr = cat.Treatment("vinegar_wash");
            Do(book, ctx, wash, keeper, Start);
            // A moment later the whole interval is still to wait, and it says so (not an hour less).
            Assert.AreEqual($"Too soon for another dose: the cow can have the vinegar wash again in {tr.minIntervalHours:0} hours.",
                Refusal(book, ctx, wash, keeper, Start + 0.01));
            // With a little over an hour left it says two hours, and after those two the wash is given.
            double asked = Start + tr.minIntervalHours - 1.01;
            Assert.AreEqual("Too soon for another dose: the cow can have the vinegar wash again in 2 hours.", Refusal(book, ctx, wash, keeper, asked));
            Do(book, ctx, wash, keeper, asked + 2.0);

            // Nor is one who comes back exactly when the interval ends refused for the rounding of the clock, whenever the dose was.
            foreach (string id in new[] { "electrolytes", "vinegar_wash", "calming_tonic", "vermifuge" })
            {
                AnimalTreatmentData med = cat.Treatment(id);
                var r = new AnimalRecord { id = "r", owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult };
                int refused = 0;
                for (int k = 0; k < 10000; k++)
                {
                    double given = Start + k * 0.001;
                    r.Dosed(new AnimalDose { treatment = med.id, at = given, strength = 1f }, 64);
                    if (AnimalHealth.WaitBefore(r, med, given + med.minIntervalHours) > 0.0) refused++;
                }
                Assert.AreEqual(0, refused, $"{id}: asked again {med.minIntervalHours} h after each of 10000 doses");
            }
        }

        [Test]
        public void EveryMedicineKeepsItsLastDoseHoweverFewDosesTheRulesKeep()
        {
            foreach (int keep in new[] { 64, 16, 8, 1 })
            {
                AnimalRules rules = Rules();
                rules.keepDoses = keep;
                AnimalCatalog cat = Catalog(rules);
                AnimalSpeciesData sp = cat.Species("cow");
                var a = new AnimalRecord { id = "a", owner = "p1", species = "cow", breed = "holstein", stage = GrowthStage.Adult, hygiene = 80f };
                double t = Start;
                foreach (AnimalTreatmentData tr in cat.AllTreatments) AnimalHealth.Treat(a, sp, tr, cat, rules, 1f, true, "p1", "p1", t += 0.001, false, null);
                var forgotten = new List<string>();
                foreach (AnimalTreatmentData tr in cat.AllTreatments)
                    if (AnimalHealth.WaitBefore(a, tr, t + 0.001) <= 0.0) forgotten.Add(tr.id);
                Assert.IsEmpty(forgotten, $"Keep Doses {keep}: each of the {cat.AllTreatments.Count} medicines just given waits its interval");
                Assert.AreEqual(cat.AllTreatments.Count, a.doses.Count, $"Keep Doses {keep}: one last dose per medicine");
            }
        }

        [Test]
        public void TheOwnersMedicineAddsToTheBondAtMostOncePerInterval()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            AnimalTreatmentData tonic = cat.Treatment("calming_tonic");
            AnimalConditionData stress = cat.Condition("chronic_stress");
            AnimalCondition k = Ill(cow, cat, "chronic_stress", 70f, Start);
            k.shown = true;
            const string item = "potion:calming_tonic";
            AnimalActor owner = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [item] = 100 });
            var give = new AnimalRequest { kind = AnimalActionKind.Treat, pen = "barn", animal = cow.id, item = item };
            var gained = new List<float>();
            foreach (double at in new[] { 0.0, tonic.minIntervalHours / 2.0, tonic.minIntervalHours })
            {
                k.severity = 70f;
                k.stage = stress.StageAt(70f);
                k.recoveringUntil = -1.0;
                float before = cow.affinity;
                Do(book, ctx, give, owner, Start + at);
                gained.Add(cow.affinity - before);
            }
            Assert.AreEqual(1f, gained[0], 1e-3, "the owner's first dose adds to the bond");
            Assert.AreEqual(0f, gained[1], 1e-3, "one too soon (an overdose) adds nothing");
            Assert.AreEqual(1f, gained[2], 1e-3, "one at its interval adds again");
        }

        [Test]
        public void AWholePensDoseOfAMedicineThatHarmsTooSoonTakesItemsOnlyForThoseItIsGiven()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "field", AnimalHabitatKind.Pasture, 400f, Start, 1.5f);
            var flock = new List<AnimalRecord>();
            for (int i = 0; i < 6; i++) flock.Add(Release(book, ctx, h, "p1", "sheep", "merino", Start, GrowthStage.Adult, 70 + i));
            AnimalTreatmentData tonic = cat.Treatment("calming_tonic");
            Assert.Greater(tonic.overdoseShare, 0f, "a medicine that harms when given too soon");
            AnimalSpeciesData sp = cat.Species("sheep");
            const string item = "potion:calming_tonic";
            AnimalActor keeper = Actor("p1", AnimalRights.All, new Dictionary<string, int> { [item] = 100 });
            Do(book, ctx, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", animal = flock[0].id, item = item }, keeper, Start);
            AnimalPlan plan = AnimalCommands.Plan(book, new AnimalRequest { kind = AnimalActionKind.Treat, pen = "field", item = item }, keeper, Start + 1.0, ctx);
            Assert.IsFalse(plan.Refused, plan.refusal?.message);
            float rest = 0f;
            for (int i = 1; i < flock.Count; i++) rest += AnimalHealth.DosesFor(sp, flock[i]);
            Assert.AreEqual(AnimalHealth.ItemsFor(tonic, rest), plan.takeCount, "the items are for the five it is given to, not the one left out");
        }

        [Test]
        public void AStrangerMayPourWaterNoDirtierThanAMurkyTroughs()
        {
            AnimalRules rules = Rules();
            AnimalCatalog cat = Catalog(rules);
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Trough(book, ctx, "pond", 3000f);
            float lake = AnimalHealth.KindDirt((int)WaterSourceKind.Lake);
            Assert.Greater(lake, rules.tipOutAbove, "lake water is murky");
            var pour = new AnimalRequest { kind = AnimalActionKind.FillWater, pen = "pond" };
            // Water as dirty as a murky trough's makes it no dirtier: a passer-by may pour it...
            h.pen.water = 500f;
            h.pen.waterDirt = lake;
            Do(book, ctx, pour, Carrying("stranger", AnimalHousing.Public, WaterSourceKind.Lake, 5f), Start);
            Assert.AreEqual(lake, h.pen.waterDirt, 1e-3, "it stays as it was");
            // ...but not water a little dirtier than the trough's.
            h.pen.water = 500f;
            h.pen.waterDirt = lake - 0.01f;
            Assert.AreEqual("Only a keeper of an animal here may pour such water into it.",
                Refusal(book, ctx, pour, Carrying("stranger", AnimalHousing.Public, WaterSourceKind.Lake, 5f), Start));
        }

        [Test]
        public void ANameKeepsNothingInvisibleAndNoSyllableOfAnotherScriptIsCutInTwo()
        {
            int max = Rules().maxNameLength;
            Assert.AreEqual(24, max, "the cases below are written for a cap of 24");
            var wrong = new List<string>();
            void Expect(string what, string name, string expected)
            {
                string got = AnimalCommands.CleanName(name, max);
                if (got != expected) wrong.Add($"{what}: {CodesOrNull(got)}, not {CodesOrNull(expected)}");
            }
            string A(int n) => new string('A', n);
            // Invisible characters (zero-width spaces, direction marks and overrides, a byte-order mark) are dropped and take no room.
            Expect("zero-width spaces before it", new string('\u200B', 24) + "Daisy", "Daisy");
            Expect("direction marks before it", new string('\u200E', 30) + "Daisy", "Daisy");
            Expect("a right-to-left override inside it", "Daisy\u202Eevil", "Daisyevil");
            Expect("a byte-order mark alone", "\uFEFF", null);
            Expect("a joiner alone", "\u200D", null);
            Expect("word joiners alone", "\u2060\u2060", null);
            Expect("a joiner at the end", "Daisy\u200D", "Daisy");
            // What emoji and scripts are made of stays: a joined family, a tag flag (England), a non-joiner in Persian.
            Expect("a family", "Ann \U0001F468\u200D\U0001F469\u200D\U0001F467", "Ann \U0001F468\u200D\U0001F469\u200D\U0001F467");
            Expect("a tag flag", "Jo \U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F",
                "Jo \U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F");
            Expect("a non-joiner", "\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645", "\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645");
            // A Hangul syllable written in its letters, Thai or Lao SARA AM, and a sign written before its letter go whole at the cap.
            Expect("23 letters, then a syllable of three Hangul letters", A(23) + "\u1100\u1161\u11A8", A(23));
            Expect("22 letters, then a syllable of three Hangul letters", A(22) + "\u1100\u1161\u11A8", A(22));
            Expect("21 letters, then a syllable of three Hangul letters", A(21) + "\u1100\u1161\u11A8", A(21) + "\u1100\u1161\u11A8");
            Expect("23 letters, then a Hangul syllable and its last letter", A(23) + "\uAC00\u11A8", A(23));
            Expect("23 letters, then Thai KO KAI with SARA AM", A(23) + "\u0E01\u0E33", A(23));
            Expect("23 letters, then Lao KO with AM", A(23) + "\u0E81\u0EB3", A(23));
            Expect("23 letters, then a Malayalam dot reph and its letter", A(23) + "\u0D4E\u0D15", A(23));
            Expect("a Hangul name that fits", "\uAE40\uBBFC\uC900", "\uAE40\uBBFC\uC900");
            Expect("marks only", "\u0301\u20DD", null);
            Expect("a joiner before it", "\u200DDaisy", "Daisy");
            Expect("a tag before it", "\U000E0067Daisy", "Daisy");
            Expect("23 letters, then a syllable and the letter added to it", A(23) + "\uAC01\u11A8", A(23));
            // A joiner after a virama belongs to the letter (a Malayalam chillu, a Devanagari half form): it stays at the end.
            Expect("a Malayalam chillu written with a joiner", "\u0D15\u0D2E\u0D32\u0D4D\u200D", "\u0D15\u0D2E\u0D32\u0D4D\u200D");
            Expect("a Devanagari half form at the end", "\u0915\u094D\u200D", "\u0915\u094D\u200D");
            // Line and paragraph separators would start a line of their own in other players' windows.
            Expect("a line separator inside it", "Daisy\u2028Evil", "DaisyEvil");
            Expect("a paragraph separator inside it", "Daisy\u2029Evil", "DaisyEvil");
            // Letters that show as nothing are no name, and neither start nor end one.
            Expect("a Hangul filler", "\u3164", null);
            Expect("Hangul fillers and a space", "\u3164 \u3164", null);
            Expect("the Hangul choseong and jungseong fillers", "\u115F\u1160", null);
            Expect("a halfwidth Hangul filler", "\uFFA0", null);
            Expect("blank Braille", "\u2800\u2800", null);
            Expect("a Hangul filler before and after it", "\u3164Daisy\u3164", "Daisy");
            // No lone accent or variation mark starts a name.
            Expect("accents before it", new string('\u0301', 20) + "Daisy", "Daisy");
            Expect("a variation mark before it", "\uFE0FDaisy", "Daisy");
            // Tag characters only as the tags of a tag flag.
            Expect("tags after a name", "Daisy\U000E0041\U000E0042\U000E0043", "Daisy");
            Expect("a cancel tag after a letter", "A\U000E007F", "A");
            // A halfwidth katakana and its sound mark go whole at the cap.
            Expect("23 letters, then a halfwidth GA", A(23) + "\uFF76\uFF9E", A(23));
            Assert.IsEmpty(wrong, string.Join("; ", wrong));

            // A rename to nothing visible is refused, as an empty one is.
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "pen", AnimalHabitatKind.Pasture, 48f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            Assert.AreEqual("That is not a name.", Refusal(book, ctx,
                new AnimalRequest { kind = AnimalActionKind.Rename, pen = "pen", animal = cow.id, text = "\u200B\u200E\u2060" }, Actor("p1", AnimalRights.All), Start));
        }

        [Test]
        public void TheEditorsHealAllTellsOverEachIllnessItsKeepersKnewOfAndNoOther()
        {
            AnimalCatalog cat = Catalog();
            AnimalSimContext ctx = Ctx(cat);
            var book = new AnimalBook(cat, "test");
            AnimalHerd h = Pen(book, "barn", AnimalHabitatKind.Barn, 60f, Start);
            AnimalRecord cow = Release(book, ctx, h, "p1", "cow", "holstein", Start);
            // A named infection that came back in a stage that hides (named, not showing), and worms nobody knew of.
            AnimalCondition named = Ill(cow, cat, Infection, 5f, Start, diagnosed: true);
            named.shown = false;
            AnimalCondition unknown = Ill(cow, cat, "parasites", 5f, Start);
            unknown.shown = false;
            cow.health = 40f;
            cow.toxin = 3f;
            cow.upset = 30f;
            cow.absorbFactor = 0.5f;
            cow.exposures.Add(new AnimalExposure { condition = Infection, value = 5f });
            int revision = cow.revision;
            Assert.IsTrue(AnimalPenEditor.HealNow(cow, cat, cat.Rules, Start + 1.0));
            Assert.IsEmpty(cow.conditions, "every illness is gone");
            Assert.IsEmpty(cow.exposures, "and its build-up");
            Assert.AreEqual(100f, cow.health, 1e-4);
            Assert.AreEqual(0f, cow.toxin, 1e-4);
            Assert.AreEqual(0f, cow.upset, 1e-4);
            Assert.AreEqual(1f, cow.absorbFactor, 1e-4);
            Assert.Greater(cow.revision, revision, "the record is marked changed");
            AnimalHealthEntry over = LastHealth(cow, AnimalHealthEventKind.Cleared, Infection);
            Assert.IsNotNull(over, "the named infection is told over");
            Assert.IsTrue(over.shown, "and its keepers see that in its history");
            Assert.IsNull(LastHealth(cow, AnimalHealthEventKind.Cleared, "parasites"), "worms nobody knew of are not told of");
            Assert.IsFalse(AnimalPenEditor.HealNow(null, cat, cat.Rules, Start), "no animal, nothing done");

            // A dead animal is left as it was.
            AnimalRecord dead = Release(book, ctx, h, "p1", "cow", "holstein", Start, GrowthStage.Adult, 99);
            Ill(dead, cat, Infection, 40f, Start, diagnosed: true);
            AnimalSim.Die(dead, AnimalDeathCause.OldAge, Start + 1.0, ctx, cat.Species("cow"), false);
            string before = JsonUtility.ToJson(dead);
            Assert.IsFalse(AnimalPenEditor.HealNow(dead, cat, cat.Rules, Start + 2.0), "a dead animal is not healed");
            Assert.AreEqual(before, JsonUtility.ToJson(dead), "and nothing of it changes");
        }
    }
}
#endif
