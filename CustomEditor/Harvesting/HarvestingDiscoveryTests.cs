#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HarvestingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of what harvested parts are good for and how players find out: each part's uses (worked out, or
    /// its own - some none at all) and rarity; a player knows a part's worth roughly, then exactly, only by taking it,
    /// selling it or by skill, and finds its hidden uses by trying them; the world's discoveries make a part worth more and
    /// good for more; some parts must be taken soon after death or into a container; and the open body still shows every
    /// organ on its own. Plain rules: no scene, no assets.
    /// </summary>
    public class HarvestingDiscoveryTests
    {
        [SetUp]
        public void NoDiscoveries() => HarvestDiscoveries.Clear();

        [TearDown]
        public void Forget() => HarvestDiscoveries.Clear();

        private static HarvestMaterial M(string anatomy, string material) => HarvestDefaults.Anatomy(anatomy).Material(material);

        private static HarvestSkillData Skilled(HarvestSettings s, int anatomyLevel)
        {
            var d = new HarvestSkillData();
            if (anatomyLevel > 0)
                d.AddXp(HarvestSkill.Anatomy, HarvestSkills.XpFor(s, anatomyLevel) + 0.01f);
            return d;
        }

        [Test]
        public void EachPart_IsGoodForWhatItIs_AndSomeForNothing()
        {
            Assert.That(M("bear", "haunch").Uses, Is.EqualTo(HarvestUse.Food), "meat is food");
            Assert.That(M("bear", "pelt").Uses & HarvestUse.Crafting, Is.EqualTo(HarvestUse.Crafting), "a pelt is for crafting");
            Assert.That(M("bear", "skull").Uses, Is.EqualTo(HarvestUse.Crafting | HarvestUse.Trophy), "a skull: bone, and a trophy");
            Assert.That(M("bear", "gallbladder").Uses & (HarvestUse.Alchemy | HarvestUse.Trade), Is.EqualTo(HarvestUse.Alchemy | HarvestUse.Trade),
                "the prized gall: alchemy, and it sells");
            Assert.That(M("bear", "spleen").Uses, Is.EqualTo(HarvestUse.None), "nobody wants the spleen");
            Assert.That(M("giant_spider", "book_lungs").Uses, Is.EqualTo(HarvestUse.None));
            foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
                foreach (HarvestMaterial m in a.materials)
                    Assert.That(m.Uses & HarvestUse.Auto, Is.EqualTo(HarvestUse.None), $"{a.id}/{m.id}: Auto is worked out, never shown");
        }

        [Test]
        public void Rarity_ComesFromHowLikelyAndHowFineItIs()
        {
            Assert.That(M("bear", "haunch").Rarity, Is.EqualTo(HarvestRarity.Common));
            Assert.That(M("bear", "bezoar").Rarity, Is.EqualTo(HarvestRarity.Rare), "12 % of bears, tier 3");
            Assert.That(M("drake", "heartstone").Rarity, Is.EqualTo(HarvestRarity.Legendary), "its own");
            Assert.That(M("drake", "fire_gland").Rarity, Is.EqualTo(HarvestRarity.Exotic), "tier 4");
            Assert.That(new HarvestMaterial { chance = 0.01f }.Rarity, Is.EqualTo(HarvestRarity.Legendary));
        }

        [Test]
        public void APlayer_LearnsWhatAPartIsWorth_ByTakingIt_SellingIt_OrBySkill()
        {
            HarvestSettings s = HarvestSettings.Defaults();
            HarvestMaterial liver = M("bear", "liver");
            var d = new HarvestSkillData();
            Assert.That(HarvestSkills.ValueKnowledge(s, d, "bear", liver), Is.EqualTo(HarvestValueKnowledge.Unknown), "a novice has no idea");

            Assert.That(HarvestSkills.NotePieces(s, d, "bear", liver, 1), Is.False);
            Assert.That(HarvestSkills.ValueKnowledge(s, d, "bear", liver), Is.EqualTo(HarvestValueKnowledge.Estimate), "one taken: a rough idea");
            Assert.That(HarvestSkills.NotePieces(s, d, "bear", liver, s.piecesToKnowValue), Is.True, "enough taken: they know it now");
            Assert.That(HarvestSkills.ValueKnowledge(s, d, "bear", liver), Is.EqualTo(HarvestValueKnowledge.Known));

            var seller = new HarvestSkillData();
            Assert.That(HarvestSkills.Appraise(seller, "bear", liver), Is.True, "a merchant paid for one");
            Assert.That(HarvestSkills.Appraise(seller, "bear", liver), Is.False, "once is enough");
            Assert.That(HarvestSkills.ValueKnowledge(s, seller, "bear", liver), Is.EqualTo(HarvestValueKnowledge.Known));

            Assert.That(HarvestSkills.ValueKnowledge(s, Skilled(s, s.anatomyToEstimateValue), "bear", liver), Is.EqualTo(HarvestValueKnowledge.Estimate),
                "a trained eye guesses any part's worth");
            Assert.That(HarvestSkills.ValueWord(0.1f), Is.EqualTo("worth nothing"));
            Assert.That(HarvestSkills.ValueWord(40f), Is.EqualTo("very valuable"));
        }

        [Test]
        public void HiddenUses_AreFoundByTryingThem_ObviousOnesAreKnown()
        {
            HarvestSettings s = HarvestSettings.Defaults();
            var d = new HarvestSkillData();
            HarvestMaterial gall = M("bear", "gallbladder");
            Assert.That(HarvestSkills.KnownUses(s, d, "bear", M("bear", "haunch")), Is.EqualTo(HarvestUse.Food), "meat is obviously food");
            Assert.That(HarvestSkills.KnownUses(s, d, "bear", gall), Is.EqualTo(HarvestUse.None), "what a gall bladder is for is not obvious");
            Assert.That(HarvestSkills.HasUnknownUses(s, d, "bear", gall), Is.True);

            Assert.That(HarvestSkills.LearnUse(d, "bear", gall, HarvestUse.Food), Is.EqualTo(HarvestUse.None), "it is not food: nothing learnt");
            Assert.That(HarvestSkills.LearnUse(d, "bear", gall, HarvestUse.Alchemy), Is.EqualTo(HarvestUse.Alchemy), "put in a vessel: alchemy");
            Assert.That(HarvestSkills.LearnUse(d, "bear", gall, HarvestUse.Alchemy), Is.EqualTo(HarvestUse.None), "known already");
            Assert.That(HarvestSkills.KnownUses(s, d, "bear", gall), Is.EqualTo(HarvestUse.Alchemy));

            Assert.That(HarvestSkills.KnownUses(s, Skilled(s, s.anatomyToSenseUses), "bear", gall), Is.EqualTo(gall.Uses), "a master anatomist knows them all");
        }

        [Test]
        public void ADiscovery_MakesAPartWorthMore_AndGoodForMore()
        {
            HarvestMaterial blood = M("troll", "blood");
            Assert.That(blood.CurrentValue, Is.EqualTo(1f), "a curiosity");
            Assert.That(blood.Uses & HarvestUse.Alchemy, Is.EqualTo(HarvestUse.None));
            Assert.That(HarvestDiscoveries.MaterialsOf(HarvestDefaults.Discoveries.TrollRegeneration, HarvestDefaults.Anatomies()), Does.Contain("Troll blood"));

            Assert.That(HarvestDiscoveries.Unlock(HarvestDefaults.Discoveries.TrollRegeneration), Is.True);
            Assert.That(HarvestDiscoveries.Unlock(HarvestDefaults.Discoveries.TrollRegeneration), Is.False, "made once");
            Assert.That(blood.CurrentValue, Is.EqualTo(45f), "worth its discovered value now");
            Assert.That(blood.Uses & HarvestUse.Alchemy, Is.EqualTo(HarvestUse.Alchemy), "and an alchemy ingredient");
            Assert.That(HarvestSkills.KnownUses(HarvestSettings.Defaults(), new HarvestSkillData(), "troll", blood) & HarvestUse.Alchemy,
                Is.EqualTo(HarvestUse.Alchemy), "everyone knows a discovery");

            HarvestDiscoveries.SetAll(new[] { "something_else" });
            Assert.That(blood.CurrentValue, Is.EqualTo(1f), "forgotten with a new world");
        }

        [Test]
        public void SomeParts_MustBeTakenSoonAfterDeath_OrIntoAContainer()
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy("bear");
            HarvestSettings s = HarvestSettings.Defaults();
            var d = new HarvestSkillData();
            HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, 3));
            c.hideOff = true;
            int eyes = a.MaterialIndex("eyes"), blood = a.MaterialIndex("blood");
            Assert.That(c.Availability(a, s, eyes, d, 1, out _), Is.EqualTo(HarvestAvailability.Available), "fresh");
            c.clock = 25.0; // 25 minutes at 20 °C
            Assert.That(c.Availability(a, s, eyes, d, 1, out string why), Is.EqualTo(HarvestAvailability.Spoiled), "eyes cloud within 20 minutes");
            StringAssert.Contains("Too late", why);
            Assert.That(c.TooLate(a.materials[blood]), Is.True, "blood clots within 10");
            Assert.That(a.materials[blood].container, Is.EqualTo(HarvestDefaults.Containers.Vial), "and goes into a vial");
            foreach (HarvestAnatomyData x in HarvestDefaults.Anatomies())
                foreach (HarvestMaterial m in x.materials)
                {
                    if (!string.IsNullOrEmpty(m.container))
                        Assert.That(m.container, Is.EqualTo(HarvestDefaults.Containers.Vial), $"{x.id}/{m.id}: a container Create Tools makes");
                    if (m.takeWithin > 0f && m.spoilMinutes > 0f)
                        Assert.That(m.takeWithin, Is.LessThan(m.spoilMinutes), $"{x.id}/{m.id}: its time limit comes before it spoils, or it would never matter");
                }
        }

        [Test]
        public void HiddenGlands_NeedAnatomyToNotice()
        {
            HarvestSettings s = HarvestSettings.Defaults();
            HarvestMaterial adrenals = M("bear", "adrenals");
            Assert.That(HarvestSkills.Notices(s, new HarvestSkillData(), "bear", adrenals), Is.False, "easy to miss");
            Assert.That(HarvestSkills.Notices(s, Skilled(s, 12), "bear", adrenals), Is.True);
        }

        [Test]
        public void TheOpenBody_ShowsEveryOrganOnItsOwn_InsideTheDrawing()
        {
            HarvestSettings s = HarvestSettings.Defaults();
            var skills = new HarvestSkillData();
            foreach (HarvestSkill k in Enum.GetValues(typeof(HarvestSkill)))
                skills.AddXp(k, HarvestSkills.XpFor(s, s.maxLevel) + 1f);
            var w = new HarvestWorker { settings = s, skills = skills, tools = new HarvestToolbox(new List<HarvestToolProfile>()), player = 1, seed = 5 };
            foreach (string id in new[] { "bear", "wolf", "deer", "troll", "giant_spider" })
            {
                HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
                HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, 3, 1f, HarvestAge.Adult, true, 10f));
                c.hideOff = true;
                HarvestPlan p = HarvestPlanner.Plan(a, c, a.MaterialIndex("heart"), null, w);
                HarvestStageSpec open = p.stages.FirstOrDefault(st => st.title == "Open the body");
                Assert.That(open, Is.Not.Null, id);
                List<Rect> organs = open.scene.Where(sh => sh.unit >= 0 && sh.points.Count > 2)
                    .GroupBy(sh => sh.unit).Select(g => Bounds(g.SelectMany(sh => sh.points))).ToList();
                Assert.That(organs.Count, Is.GreaterThanOrEqualTo(5), $"{id}: its organs are drawn");
                for (int i = 0; i < organs.Count; i++)
                    for (int j = i + 1; j < organs.Count; j++)
                        if (Overlap(organs[i], organs[j]) > 0.5f)
                            Assert.Fail($"{id}: two organs drawn over each other ({organs[i]} and {organs[j]})");
            }
        }

        private static Rect Bounds(IEnumerable<Vector2> pts)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Vector2 p in pts) { x0 = Math.Min(x0, p.x); y0 = Math.Min(y0, p.y); x1 = Math.Max(x1, p.x); y1 = Math.Max(y1, p.y); }
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>The share of the smaller rectangle the two cover together.</summary>
        private static float Overlap(Rect a, Rect b)
        {
            float w = Math.Min(a.xMax, b.xMax) - Math.Max(a.xMin, b.xMin), h = Math.Min(a.yMax, b.yMax) - Math.Max(a.yMin, b.yMin);
            if (w <= 0f || h <= 0f) return 0f;
            return w * h / Math.Max(1e-4f, Math.Min(a.width * a.height, b.width * b.height));
        }
    }

    /// <summary>
    /// Edit Mode tests of unusual bodies: a slime, a turtle, a three-headed cerberus and a five-headed hydra are drawn with
    /// every region once, inside the drawing; extra heads stand apart from the first, each hydra head on its own neck; a
    /// region can be drawn with a shape of its own; and every one of them can be harvested.
    /// </summary>
    public class HarvestingShapeTests
    {
        private static Vector2 Mid(List<Vector2[]> polys) => HarvestDiagram.Middle(polys[0]);

        [Test]
        public void UnusualBodies_DrawEveryRegionOnce_InsideTheDrawing()
        {
            foreach (string id in new[] { "slime", "giant_turtle", "cerberus", "hydra" })
            {
                HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
                Assert.That(a, Is.Not.Null, id);
                List<KeyValuePair<int, List<Vector2[]>>> drawing = HarvestDiagram.Drawing(a);
                Assert.That(drawing.Select(d => d.Key).Distinct().Count(), Is.EqualTo(a.regions.Count), $"{id}: every region drawn once");
                foreach (KeyValuePair<int, List<Vector2[]>> d in drawing)
                    foreach (Vector2[] poly in d.Value)
                        foreach (Vector2 p in poly)
                            Assert.That(p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f, $"{id}/{a.regions[d.Key].id}: ({p.x:0.00}, {p.y:0.00}) off the drawing");
            }
        }

        [Test]
        public void ExtraHeads_StandApart_AndEachHydraHeadStaysOnItsNeck()
        {
            HarvestAnatomyData cerberus = HarvestDefaults.Anatomy("cerberus");
            Vector2 h1 = Mid(HarvestDiagram.RegionShape(cerberus, cerberus.Region("head")));
            Vector2 h2 = Mid(HarvestDiagram.RegionShape(cerberus, cerberus.Region("head_2")));
            Vector2 h3 = Mid(HarvestDiagram.RegionShape(cerberus, cerberus.Region("head_3")));
            Assert.That((h2 - h1).magnitude, Is.GreaterThan(0.03f), "the second head beside the first");
            Assert.That((h3 - h1).magnitude, Is.GreaterThan(0.03f), "the third too");
            Assert.That(Math.Sign(h2.y - h1.y), Is.Not.EqualTo(Math.Sign(h3.y - h1.y)), "one above, one below");

            HarvestAnatomyData hydra = HarvestDefaults.Anatomy("hydra");
            float neckToHead = (Mid(HarvestDiagram.RegionShape(hydra, hydra.Region("head"))) - Mid(HarvestDiagram.RegionShape(hydra, hydra.Region("neck")))).magnitude;
            for (int n = 2; n <= 5; n++)
            {
                Vector2 neck = Mid(HarvestDiagram.RegionShape(hydra, hydra.Region($"neck_{n}")));
                Vector2 head = Mid(HarvestDiagram.RegionShape(hydra, hydra.Region($"head_{n}")));
                Assert.That((head - neck).magnitude, Is.EqualTo(neckToHead).Within(0.04f), $"head {n} sits on neck {n}, as the first head on the first neck");
            }
            Assert.That(hydra.RegionOfPart("Head3"), Is.EqualTo(hydra.RegionIndex("head_3")), "hits on the third head mark the third head");
        }

        [Test]
        public void ARegion_CanBeDrawnWithAShapeOfItsOwn()
        {
            var a = new HarvestAnatomyData { id = "test", plan = HarvestBodyPlan.Quadruped };
            var ellipse = new HarvestRegion { id = "hump", drawShape = HarvestDrawShape.Ellipse, drawBox = new Rect(0.4f, 0.8f, 0.2f, 0.1f) };
            var box = new HarvestRegion { id = "plate", drawShape = HarvestDrawShape.Box, drawBox = new Rect(0.1f, 0.1f, 0.2f, 0.2f) };
            var outline = new HarvestRegion { id = "fin", drawShape = HarvestDrawShape.Outline, outline = new List<Vector2> { new Vector2(0.5f, 0.9f), new Vector2(0.6f, 1f), new Vector2(0.7f, 0.9f) } };
            var head = new HarvestRegion { id = "head" };
            a.regions.AddRange(new[] { head, ellipse, box, outline });
            Assert.That(HarvestDiagram.Middle(HarvestDiagram.RegionShape(a, ellipse)[0]).x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(HarvestDiagram.RegionShape(a, box)[0].Length, Is.EqualTo(4));
            Assert.That(HarvestDiagram.RegionShape(a, outline)[0].Length, Is.EqualTo(3));
            Assert.That(HarvestDiagram.Drawing(a).Count, Is.EqualTo(4), "the plan's head and the three shapes of their own");
            Assert.That(HarvestDiagram.SlotCopy("head#3", out string b, out int n) && b == "head" && n == 3);
            Assert.That(HarvestDiagram.SlotCopy("head", out _, out _), Is.False);
        }

        [Test]
        public void ATurtle_MustLoseItsPlastronBeforeItsMeat_AndASlimeItsMembraneBeforeItsGel()
        {
            HarvestSettings s = HarvestSettings.Defaults();
            var d = new HarvestSkillData();
            HarvestAnatomyData turtle = HarvestDefaults.Anatomy("giant_turtle");
            HarvestCarcass c = HarvestCarcass.From(turtle, HarvestCondition.Create(turtle, 3));
            c.hideOff = true;
            Assert.That(c.Availability(turtle, s, turtle.MaterialIndex("meat"), d, 1, out string why), Is.EqualTo(HarvestAvailability.Blocked));
            StringAssert.Contains("plastron", why);
            HarvestAnatomyData slime = HarvestDefaults.Anatomy("slime");
            HarvestCarcass k = HarvestCarcass.From(slime, HarvestCondition.Create(slime, 3));
            Assert.That(k.Availability(slime, s, slime.MaterialIndex("gel"), d, 1, out _), Is.EqualTo(HarvestAvailability.NeedsHide), "the membrane comes off first");
            Assert.That(HarvestCarcass.IsHide(slime.Material("membrane")), Is.True);
        }
    }
}
#endif
