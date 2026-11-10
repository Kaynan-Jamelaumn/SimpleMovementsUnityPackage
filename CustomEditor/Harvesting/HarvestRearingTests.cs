#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HarvestingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of butchering farm animals through Harvesting: how an animal was raised scales the quality of every
    /// piece it gives (a creature that names no rearing gives what it always gave); the six farm anatomies are as sound as
    /// the game's own (no validation errors or warnings, no mob types, pelts the size of their tannery hides, every material
    /// planned); a farm animal's remains - neglect, illness, hunger - show in its carcass, a neglected cow's tannery hide
    /// worse than a well-raised one's; the carcass lies outside the fence, away from the feeder and the trough; only its
    /// owner may work it; and the hours it lay dead spoil it as they would have. Plain rules: no scene, no assets.
    /// </summary>
    public class HarvestRearingTests
    {
        private static readonly string[] Farm = { "cow", "horse", "goat", "pig", "chicken", "duck" };

        private static HarvestSettings Settings() => HarvestSettings.Defaults();

        private static HarvestStageResult Stage(HarvestStageKind kind, float precision, float damage = 0f, float completeness = 1f, int unit = -1, float tool = 0.9f) =>
            new HarvestStageResult { kind = kind, precision = precision, damage = damage, completeness = completeness, unit = unit, toolFitness = tool, seconds = 5f };

        private static HarvestTaskResult Task(string material, params HarvestStageResult[] stages) =>
            new HarvestTaskResult { material = material, stages = stages.ToList() };

        /// <summary>A perfect peel of every hide cell.</summary>
        private static HarvestStageResult CleanPeel(HarvestCondition c)
        {
            HarvestStageResult s = Stage(HarvestStageKind.Peel, 0.95f);
            s.cellDamage = c.pelt.Select(_ => 0f).ToList();
            s.cellFlesh = c.pelt.Select(_ => 0.1f).ToList();
            s.lostCells = new List<int>();
            return s;
        }

        private static CraftRules TanneryRules()
        {
            CraftContent content = CraftDefaults.Content();
            content.hides.AddRange(HarvestCraftHides.All());
            return CraftRules.Compile(new CraftSimulationSettings(), content);
        }

        private static HarvestToolProfile Tool(string id, float skinning, float butchering, float fine, float saw, float scraper, float pliers, float chopping) =>
            new HarvestToolProfile
            {
                id = id, displayName = id, sharpness = 1f, quality = 1f, speed = 1f,
                fit = new HarvestToolFit(skinning, butchering, fine, saw, scraper, pliers, chopping),
            };

        /// <summary>A master with a full kit: a proper tool for every kind of work.</summary>
        private static HarvestWorker Master()
        {
            HarvestSettings s = Settings();
            var skills = new HarvestSkillData();
            foreach (HarvestSkill k in Enum.GetValues(typeof(HarvestSkill)))
                skills.AddXp(k, HarvestSkills.XpFor(s, s.maxLevel) + 1f);
            var kit = new List<HarvestToolProfile>
            {
                Tool("skinning_knife", 1f, 0.5f, 0.4f, 0f, 0.3f, 0f, 0f), Tool("butcher_knife", 0.4f, 1f, 0.2f, 0f, 0.2f, 0f, 0.5f),
                Tool("lancet", 0.3f, 0f, 1f, 0f, 0f, 0f, 0f), Tool("bone_saw", 0f, 0f, 0f, 1f, 0f, 0f, 0.3f),
                Tool("scraper", 0.2f, 0f, 0f, 0f, 1f, 0f, 0f), Tool("pliers", 0f, 0f, 0f, 0f, 0f, 1f, 0f),
            };
            return new HarvestWorker { settings = s, skills = skills, tools = new HarvestToolbox(kit), player = 1, seed = 5 };
        }

        private static HarvestCarcass Carcass(HarvestAnatomyData a, float rearing, bool hideOff = false)
        {
            HarvestCondition k = HarvestCondition.Create(a, 7);
            k.rearing = rearing;
            HarvestCarcass c = HarvestCarcass.From(a, k);
            if (hideOff) c.hideOff = true;
            return c;
        }

        /// <summary>The quality of a cow's hide, a haunch, its heart and a femur, taken by the same novice hands, from a cow raised so.</summary>
        private static float[] Qualities(float rearing)
        {
            HarvestAnatomyData a = HarvestDefaults.Cow();
            HarvestSettings s = Settings();
            HarvestCarcass hide = Carcass(a, rearing), meat = Carcass(a, rearing, true), heart = Carcass(a, rearing), bone = Carcass(a, rearing, true);
            HarvestOutcome[] o =
            {
                HarvestResolver.Resolve(a, s, hide, Task("hide", Stage(HarvestStageKind.Trace, 0.95f), CleanPeel(hide.condition)), new HarvestSkillData()),
                HarvestResolver.Resolve(a, s, meat, Task("haunch", Stage(HarvestStageKind.Trace, 0.9f)), new HarvestSkillData()),
                HarvestResolver.Resolve(a, s, heart, Task("heart", Stage(HarvestStageKind.Steady, 0.9f), Stage(HarvestStageKind.Pull, 0.9f)), new HarvestSkillData()),
                HarvestResolver.Resolve(a, s, bone, new HarvestTaskResult
                {
                    material = "femur", units = new List<int> { 0 },
                    stages = new List<HarvestStageResult> { Stage(HarvestStageKind.Saw, 0.9f, unit: 0), Stage(HarvestStageKind.Scrape, 0.9f, unit: 0) },
                }, new HarvestSkillData()),
            };
            var q = new float[o.Length];
            for (int i = 0; i < o.Length; i++)
            {
                Assert.IsTrue(o[i].accepted, o[i].refusal);
                Assert.IsNotEmpty(o[i].pieces, $"piece {i} at rearing {rearing}");
                q[i] = o[i].pieces[0].quality;
            }
            return q;
        }

        private static AnimalRemainsReport Remains(float rearing, float lean, float coat, float parasites, float taint = 0f, float contamination = 0f, string cause = "Slaughtered") =>
            new AnimalRemainsReport
            {
                animal = "cow-1", species = "cow", anatomy = "cow", name = "Daisy", owner = "farmer", cause = cause,
                rearing = rearing, lean = lean, coatDamage = coat, parasites = parasites, taint = taint, contamination = contamination,
                sizeScale = 1f, age = 1, seed = 4242,
            };

        // ------------------------------------------------------------------ rearing
        [Test]
        public void RearingScalesTheQualityOfEveryPiece()
        {
            float[] wild = Qualities(1f), poor = Qualities(0.6f), well = Qualities(1.15f), unset = Qualities(0f), below = Qualities(0.05f), lowest = Qualities(0.3f);
            for (int i = 0; i < wild.Length; i++)
            {
                Assert.AreEqual(wild[i] * 0.6f, poor[i], 0.01f, $"piece {i}: x 0.6 (novice hands add no bonus to it)");
                Assert.GreaterOrEqual(well[i], wild[i], $"piece {i}: a well-raised animal's is no worse");
                Assert.AreEqual(wild[i], unset[i], 1e-6f, $"piece {i}: 0 (an older save) counts as 1");
                Assert.AreEqual(lowest[i], below[i], 1e-5f, $"piece {i}: never below x 0.3");
            }
            Assert.Greater(well.Sum(), wild.Sum(), "a well-raised cow gives better pieces");
        }

        [Test]
        public void ABodyThatNamesNoRearingIsJudgedAsBefore()
        {
            HarvestCondition hunted = HarvestCondition.Create(HarvestDefaults.Bear(), 3);
            Assert.AreEqual(1f, hunted.rearing, "a hunted creature: as wild");
            Assert.AreEqual("", hunted.cause);
            Assert.AreEqual(1.0, HarvestCondition.RearingFactor(1f));
            Assert.AreEqual(1.0, HarvestCondition.RearingFactor(0f), "older saves have none");
            Assert.AreEqual(1.0, HarvestCondition.RearingFactor(-2f));
            Assert.AreEqual(0.3, HarvestCondition.RearingFactor(0.01f), 1e-6);
            Assert.AreEqual(1.5, HarvestCondition.RearingFactor(9f), 1e-6);
            Assert.AreEqual("", HarvestCondition.RearingWord(1f));
            Assert.AreEqual("", HarvestCondition.RearingWord(0f));
            Assert.AreEqual("Well raised", HarvestCondition.RearingWord(1.05f));
            Assert.AreEqual("Poorly kept", HarvestCondition.RearingWord(0.8f));

            hunted.rearing = 0.7f;
            hunted.cause = "Slaughtered";
            HarvestCondition copy = hunted.Clone();
            HarvestCondition sent = JsonUtility.FromJson<HarvestCondition>(JsonUtility.ToJson(hunted));
            Assert.AreEqual(0.7f, copy.rearing);
            Assert.AreEqual("Slaughtered", copy.cause);
            Assert.AreEqual(0.7f, sent.rearing, "it travels with the carcass");
            Assert.AreEqual("Slaughtered", sent.cause);
        }

        // ------------------------------------------------------------------ the farm anatomies
        [Test]
        public void TheFarmAnimalsAreSoundAnatomies()
        {
            List<HarvestAnatomyData> all = HarvestDefaults.Anatomies();
            CraftRules rules = TanneryRules();
            foreach (string id in Farm)
            {
                HarvestAnatomyData a = all.FirstOrDefault(x => x.id == id);
                Assert.IsNotNull(a, $"{id} is a built-in creature");
                var errors = new List<string>();
                var warnings = new List<string>();
                a.Validate(errors, warnings);
                Assert.IsEmpty(errors, id + ": " + string.Join(" | ", errors));
                Assert.IsEmpty(warnings, id + ": " + string.Join(" | ", warnings));
                Assert.IsEmpty(a.mobTypes, $"{id}: no mob is ever taken for a farm animal");
                Assert.IsFalse(a.MatchesMob("Pig") || a.MatchesMob("Goat") || a.MatchesMob("Bird"), id);
                Assert.IsTrue(HarvestCarcass.HasHide(a), $"{id}: something comes off before the meat");
                int hides = 0;
                foreach (HarvestMaterial m in a.materials)
                {
                    if (string.IsNullOrWhiteSpace(m.craftHide)) continue;
                    hides++;
                    CraftHideDefinition d = rules.Hide(m.craftHide);
                    Assert.IsNotNull(d, $"{id}/{m.id}: the Crafting Database has no hide '{m.craftHide}'");
                    Assert.AreEqual(d.height, a.peltSize.x, $"{id}: pelt width = the {d.id} hide's height");
                    Assert.AreEqual(d.width, a.peltSize.y, $"{id}: pelt length = the {d.id} hide's width");
                    Assert.IsNotNull(HarvestHides.Piece(m, HarvestCondition.Create(a, 9), 1.0, rules), $"{id}: a tannery hide");
                }
                bool bird = a.plan == HarvestBodyPlan.Avian;
                Assert.AreEqual(bird ? 0 : 1, hides, bird ? $"{id}: its feathers are its hide" : $"{id}: one hide for the tannery");
            }
            Assert.IsTrue(HarvestDefaults.Boar().MatchesMob("Pig"), "a wild pig is still a boar");
            Assert.IsTrue(HarvestDefaults.Sheep().MatchesMob("Goat"), "a wild goat is still the sheep's");
        }

        [Test]
        public void EveryFarmMaterialCanBePlanned()
        {
            HarvestWorker w = Master();
            int planned = 0;
            foreach (string id in Farm)
            {
                HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
                // Rare finds made likely (a cow's horns), so they are planned too.
                HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, 3, 1f, HarvestAge.Adult, true, 10f));
                c.hideOff = true;
                for (int m = 0; m < a.materials.Count; m++)
                {
                    string what = $"{id}/{a.materials[m].id}";
                    HarvestAvailability av = c.Availability(a, w.settings, m, w.skills, 1, out string why);
                    if (av == HarvestAvailability.Missing) continue;
                    Assert.AreEqual(HarvestAvailability.Available, av, $"{what}: {why}");
                    HarvestPlan p = HarvestPlanner.Plan(a, c, m, null, w);
                    Assert.IsTrue(p.Valid, $"{what}: {p.refusal}");
                    Assert.IsNotEmpty(p.stages, what);
                    planned++;
                }
            }
            Assert.Greater(planned, 100, "the farm animals give a lot to work");
        }

        // ------------------------------------------------------------------ a farm animal's remains
        [Test]
        public void ANeglectedCowsTanneryHideIsWorseThanAWellRaisedOnes()
        {
            HarvestAnatomyData cow = HarvestDefaults.Cow();
            CraftRules rules = TanneryRules();
            HarvestMaterial hide = cow.Material("hide");
            HarvestCondition good = AnimalCarcass.Body(cow, Remains(1.12f, 1f, 0f, 0f));
            HarvestCondition bad = AnimalCarcass.Body(cow, Remains(0.5f, 0.45f, 0.3f, 0.6f, 0.3f, 0.5f, "Died of parasites (Severe)"));
            CraftPiece goodHide = HarvestHides.Piece(hide, good, 1.0, rules), badHide = HarvestHides.Piece(hide, bad, 1.0, rules);
            Assert.IsNotNull(goodHide);
            Assert.IsNotNull(badHide);
            double Damage(CraftPiece p) => p.hide.cells.Where(c => c.present).Average(c => c.damage);
            double Grain(CraftPiece p) => p.hide.cells.Where(c => c.present).Average(c => c.grain);
            int Flaws(CraftPiece p) => p.hide.cells.Count(c => c.present && c.flaw);
            Assert.Greater(Damage(badHide), Damage(goodHide) + 0.1, "neglect wears the hide");
            Assert.Less(Grain(badHide), Grain(goodHide), "a poorer grain");
            Assert.Greater(Flaws(badHide), Flaws(goodHide), "parasites leave flaws");
        }

        [Test]
        public void AFarmAnimalsRemainsShowInItsCarcass()
        {
            HarvestAnatomyData cow = HarvestDefaults.Cow();
            HarvestSettings s = Settings();
            HarvestCondition good = AnimalCarcass.Body(cow, Remains(1.12f, 1f, 0f, 0f));
            HarvestCondition bad = AnimalCarcass.Body(cow, Remains(0.5f, 0.45f, 0.3f, 0.6f, 0.3f, 0.5f, "Died of parasites (Severe)"));
            Assert.AreEqual(1.12f, good.rearing, 1e-6f);
            Assert.AreEqual("Slaughtered", good.cause);
            Assert.AreEqual("Died of parasites (Severe)", bad.cause);
            int back = cow.RegionIndex("back"), belly = cow.RegionIndex("belly");
            Assert.AreEqual(1f, good.regions[back].meat, 1e-6f, "well fleshed");
            Assert.AreEqual(0.45f, bad.regions[back].meat, 1e-6f, "lean");
            Assert.Greater(bad.regions[belly].contamination, 0.3f, "a digestive illness soils the gut");
            Assert.AreEqual(0f, bad.regions[back].contamination, "and nothing far from it");
            Assert.IsTrue(bad.regions.All(r => r.taint >= 0.3f - 1e-6f), "the taint of an illness runs through it");
            Assert.AreEqual(JsonUtility.ToJson(bad), JsonUtility.ToJson(AnimalCarcass.Body(cow, Remains(0.5f, 0.45f, 0.3f, 0.6f, 0.3f, 0.5f, "Died of parasites (Severe)"))),
                "the same remains give the same body, on every machine");

            float Backstrap(HarvestCondition k, out HarvestMarks marks)
            {
                HarvestCarcass c = HarvestCarcass.From(cow, k);
                c.hideOff = true;
                HarvestOutcome o = HarvestResolver.Resolve(cow, s, c, Task("backstrap", Stage(HarvestStageKind.Trace, 0.9f)), new HarvestSkillData());
                Assert.IsTrue(o.accepted, o.refusal);
                Assert.IsNotEmpty(o.pieces);
                marks = o.pieces[0].marks;
                return o.pieces[0].quality;
            }
            float fine = Backstrap(good, out HarvestMarks clean), poor = Backstrap(bad, out HarvestMarks tainted);
            Assert.Greater(fine, poor * 1.5f, "a neglected, sick cow's meat is much the worse");
            Assert.AreEqual(HarvestMarks.None, clean & HarvestMarks.Tainted);
            Assert.AreNotEqual(HarvestMarks.None, tainted & HarvestMarks.Tainted, "not fit to eat");
        }

        [Test]
        public void TheCarcassLiesOutsideTheFenceAwayFromTheFeederAndTheTrough()
        {
            List<Vector2> outline = HousingGeometry.Rectangle(Vector2.zero, new Vector2(6f, 4f));
            Vector2 feeder = new Vector2(-2.2f, 1.4f), trough = new Vector2(2.2f, 1.4f);
            Vector2 p = AnimalCarcass.FencePoint(outline, feeder, trough, AnimalCarcass.FenceGap, out Vector2 along);
            Assert.IsFalse(HousingGeometry.Contains(outline, p), "outside the fence");
            Assert.AreEqual(AnimalCarcass.FenceGap, HousingGeometry.DistanceToEdges(outline, p), 0.05f, "just outside it");
            Assert.Less(p.y, -2f, "on the side away from the feeder and the trough");
            Assert.AreEqual(1f, along.magnitude, 1e-4f);
            Assert.AreEqual(p, AnimalCarcass.FencePoint(outline, feeder, trough, AnimalCarcass.FenceGap, out _), "the same spot every time");

            var reversed = new List<Vector2>(outline);
            reversed.Reverse();
            Vector2 q = AnimalCarcass.FencePoint(reversed, feeder, trough, AnimalCarcass.FenceGap, out _);
            Assert.IsFalse(HousingGeometry.Contains(reversed, q), "whichever way the outline runs");
            Assert.Less(q.y, -2f);
        }

        [Test]
        public void AFarmCarcassIsItsOwnersOnly()
        {
            Assert.IsNull(AnimalCarcass.Refusal("p1", "p1"), "its owner");
            Assert.AreEqual(AnimalCarcass.NotYours, AnimalCarcass.Refusal("p1", "p2"), "anyone else, whatever the land");
            Assert.AreEqual(AnimalCarcass.NotYours, AnimalCarcass.Refusal("p1", null), "nobody known");
            Assert.IsNull(AnimalCarcass.Refusal("", "p2"), "nobody's");

            Func<HarvestableCreature, CombatEntity, string> before = HarvestableCreature.AccessCheck;
            var go = new GameObject("Carcass");
            try
            {
                HarvestableCreature creature = go.AddComponent<HarvestableCreature>();
                HarvestableCreature.AccessCheck = AnimalCarcass.Access;
                Assert.IsNull(creature.Denied(null), "a hunted creature's carcass is anyone's");
                AnimalCarcassTag tag = go.AddComponent<AnimalCarcassTag>();
                tag.ownerKey = "farmer";
                Assert.AreEqual(AnimalCarcass.NotYours, creature.Denied(null), "a farm animal's is its owner's");
                HarvestableCreature.AccessCheck = (c, p) => "Closed for the night.";
                Assert.AreEqual("Closed for the night.", creature.Denied(null), "a game's own rule");
                HarvestableCreature.AccessCheck = null;
                Assert.IsNull(creature.Denied(null), "no rule: anyone");
            }
            finally
            {
                HarvestableCreature.AccessCheck = before;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HoursDeadSpoilTheCarcassAsTheyWould()
        {
            Assert.AreEqual(0f, AnimalCarcass.SpoiledMinutes(0.0, 20f), "fresh");
            float day = AnimalCarcass.SpoiledMinutes(24.0, 20f);
            Assert.AreEqual((float)(24.0 * AnimalCarcass.RealMinutesPerGameHour()), day, 1e-3f, "at 20 °C a game day dead is a day's real minutes");
            Assert.Greater(AnimalCarcass.SpoiledMinutes(24.0, 30f), day, "faster in the heat");
            Assert.AreEqual(0f, AnimalCarcass.SpoiledMinutes(24.0, -40f), "frozen, it keeps");
        }
    }
}
#endif
