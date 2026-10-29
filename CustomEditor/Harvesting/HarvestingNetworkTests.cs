#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HarvestingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of what a network session sends as JSON: the server's carcass reaches the clients whole (the same
    /// availability, condition and locks); the plan of a player's work reaches their client and what they play from it
    /// is accepted by the server's own plan; results, reports and skills come back intact, and broken text is refused.
    /// Plain rules: no scene, no assets.
    /// </summary>
    public class HarvestingNetworkTests
    {
        private static HarvestSettings Settings() => HarvestSettings.Defaults();

        private static HarvestToolProfile Tool(string id, float skinning, float butchering, float fine, float saw, float scraper, float pliers) =>
            new HarvestToolProfile { id = id, displayName = id, sharpness = 1f, quality = 0.7f, speed = 1f, fit = new HarvestToolFit(skinning, butchering, fine, saw, scraper, pliers, 0f) };

        private static HarvestWorker Worker(int player = 3) => new HarvestWorker
        {
            settings = Settings(),
            skills = new HarvestSkillData(),
            tools = new HarvestToolbox(new List<HarvestToolProfile>
            {
                Tool("skinning_knife", 1f, 0.4f, 0.4f, 0f, 0.3f, 0f), Tool("butcher_knife", 0.4f, 1f, 0.2f, 0f, 0.2f, 0f), Tool("lancet", 0.3f, 0f, 1f, 0f, 0f, 0f),
                Tool("bone_saw", 0f, 0f, 0f, 1f, 0f, 0f), Tool("scraper", 0.1f, 0f, 0f, 0f, 1f, 0f), Tool("pliers", 0f, 0f, 0.1f, 0f, 0f, 1f),
            }),
            player = player,
            seed = 41,
        };

        /// <summary>A bear after a fight: an arrow in the ribs, a mace on the head, a burn on the back; dead a while.</summary>
        private static (HarvestAnatomyData a, HarvestCarcass c) FoughtBear()
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy("bear");
            HarvestSettings s = Settings();
            HarvestCondition k = HarvestCondition.Create(a, 23);
            k.ApplyHit(a, s, new HarvestHit { region = a.RegionIndex("ribs"), form = DamageForm.Pierce, severity = 0.35f, u = 0.4f, v = 0.6f, source = "Iron arrow" });
            k.ApplyHit(a, s, new HarvestHit { region = a.RegionIndex("head"), form = DamageForm.Blunt, severity = 0.5f, u = -1f, v = -1f, source = "Mace", critical = true });
            k.ApplyHit(a, s, new HarvestHit { region = a.RegionIndex("back"), element = ElementType.Fire, severity = 0.3f, u = 0.5f, v = 0.5f, source = "Fireball" });
            HarvestCarcass c = HarvestCarcass.From(a, k);
            c.Tick(s, 300, 22f);
            Assert.IsTrue(c.Lock(a.MaterialIndex("claws"), 9, 600));
            return (a, c);
        }

        private static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        /// <summary>A careful result for every stage of <paramref name="plan"/> (one per piece for prying), as a client sends it.</summary>
        private static HarvestTaskResult Played(HarvestPlan plan, int peltCells)
        {
            var r = new HarvestTaskResult { material = plan.material, units = new List<int>(plan.units) };
            foreach (HarvestStageSpec st in plan.stages)
            {
                if (st.kind == HarvestStageKind.Pry)
                {
                    foreach (HarvestPieceSpot p in st.pieces)
                        r.stages.Add(new HarvestStageResult { kind = st.kind, unit = p.unit, precision = 0.8f, seconds = st.minSeconds / Math.Max(1, st.pieces.Count) + 1f });
                    continue;
                }
                var res = new HarvestStageResult { kind = st.kind, unit = st.unit, precision = 0.8f, seconds = st.minSeconds + 1f };
                if (st.kind == HarvestStageKind.Peel)
                {
                    res.cellDamage = Enumerable.Repeat(0.05f, peltCells).ToList();
                    res.cellFlesh = Enumerable.Repeat(0.1f, peltCells).ToList();
                    res.lostCells = new List<int>();
                }
                else
                {
                    // Unity's JSON has no null lists: they arrive empty.
                    res.cellDamage = new List<float>();
                    res.cellFlesh = new List<float>();
                    res.lostCells = new List<int>();
                }
                r.stages.Add(res);
            }
            return r;
        }

        [Test]
        public void TheCarcassReachesClientsWhole()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = FoughtBear();
            HarvestSettings s = Settings();
            string json = JsonUtility.ToJson(c);
            HarvestCarcass copy = JsonUtility.FromJson<HarvestCarcass>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(copy), "nothing lost or changed on the way");
            Assert.AreEqual(c.condition.wounds.Count, copy.condition.wounds.Count);
            Assert.AreEqual(c.condition.pelt.Count, copy.condition.pelt.Count);
            Assert.AreEqual(c.condition.units.Count, copy.condition.units.Count);
            Assert.AreEqual(c.locks.Count, copy.locks.Count);
            Assert.AreEqual(c.age, copy.age, 1e-9);
            var skills = new HarvestSkillData();
            for (int i = 0; i < a.materials.Count; i++)
            {
                Assert.AreEqual(c.Availability(a, s, i, skills, 5, out string why), copy.Availability(a, s, i, skills, 5, out string whyCopy), a.materials[i].id);
                Assert.AreEqual(why, whyCopy, a.materials[i].id);
                Assert.AreEqual(c.CombatIntegrity(a, i), copy.CombatIntegrity(a, i), 1e-6, a.materials[i].id);
            }
            // The clients' copies keep spoiling as the server's does.
            c.Tick(s, 600, 22f);
            copy.Tick(s, 600, 22f);
            Assert.AreEqual(JsonUtility.ToJson(c), JsonUtility.ToJson(copy));

            // The small hides that feathers grow on where there is no hide (a griffon's wings) go with it.
            var g = new HarvestAnatomyData { id = "griffon", displayName = "Griffon", plan = HarvestBodyPlan.Quadruped, peltSize = new Vector2Int(12, 16) };
            g.regions = HarvestDefaults.QuadrupedRegions(wings: true);
            g.materials.Add(HarvestBuild.Material("pelt", "Griffon pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt"));
            g.materials.Add(HarvestBuild.Material("feathers", "Flight feathers", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
                .From("wing_l").From("wing_r").Yield(20f, 40f, "feather"));
            HarvestCondition k = HarvestCondition.Create(g, 5);
            k.ApplyHit(g, s, new HarvestHit { region = g.RegionIndex("wing_l"), form = DamageForm.Claw, severity = 0.8f, u = -1f, v = -1f, source = "test" });
            HarvestCarcass griffon = HarvestCarcass.From(g, k), sent = JsonUtility.FromJson<HarvestCarcass>(JsonUtility.ToJson(griffon));
            int feathers = g.MaterialIndex("feathers");
            Assert.Less(griffon.CombatIntegrity(g, feathers), 1.0, "clawed");
            Assert.AreEqual(griffon.CombatIntegrity(g, feathers), sent.CombatIntegrity(g, feathers), 1e-6);
            Assert.AreEqual(griffon.CombatMarks(g, feathers), sent.CombatMarks(g, feathers));
        }

        [Test]
        public void TheServersPlanReachesTheClientAndHoldsWhatItPlays()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = FoughtBear();
            int cells = c.condition.pelt.Count;
            HarvestCarcass skinned = c.Clone();
            skinned.hideOff = true;
            foreach (string material in new[] { "pelt", "fangs", "fat", "femur", "leg_sinew", "heart" })
            {
                if (material == "fat")
                    c = skinned; // the rest waits for the hide to come off
                int mi = a.MaterialIndex(material);
                Assert.GreaterOrEqual(mi, 0, material);
                HarvestPlan server = HarvestPlanner.Plan(a, c, mi, null, Worker());
                Assert.IsTrue(server.Valid, $"{material}: {server.refusal}");
                HarvestPlan client = RoundTrip(server);
                Assert.AreEqual(JsonUtility.ToJson(server), JsonUtility.ToJson(client), material);
                Assert.AreEqual(server.stages.Count, client.stages.Count, material);
                for (int i = 0; i < server.stages.Count; i++)
                {
                    Assert.AreEqual(server.stages[i].kind, client.stages[i].kind);
                    Assert.AreEqual(server.stages[i].gridW * server.stages[i].gridH, client.stages[i].gridW * client.stages[i].gridH);
                    Assert.AreEqual(server.stages[i].pieces.Count, client.stages[i].pieces.Count);
                }
                // What the client plays from its copy comes back as JSON and passes the server's own plan.
                HarvestTaskResult sent = RoundTrip(Played(client, cells));
                Assert.IsTrue(HarvestRequestGuard.Check(server, sent, cells, out string why, out bool rushed), $"{material}: {why}");
                Assert.IsFalse(rushed, material);
                foreach (HarvestStageResult r in sent.stages)
                    if (r.kind != HarvestStageKind.Peel)
                        Assert.IsNull(r.cellDamage, "only a peel carries hide cells");
            }
        }

        [Test]
        public void TheSamePlanOnBothSides()
        {
            // The window shows the work before it starts from the client's copy: it must be the work the server plans.
            (HarvestAnatomyData a, HarvestCarcass c) = FoughtBear();
            HarvestCarcass copy = RoundTrip(c);
            for (int i = 0; i < a.materials.Count; i++)
            {
                HarvestPlan server = HarvestPlanner.Plan(a, c, i, null, Worker());
                HarvestPlan client = HarvestPlanner.Plan(a, copy, i, null, Worker());
                Assert.AreEqual(JsonUtility.ToJson(server), JsonUtility.ToJson(client), a.materials[i].id);
            }
        }

        [Test]
        public void ResultsReportsAndSkillsComeBackIntact()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = FoughtBear();
            HarvestPlan plan = HarvestPlanner.Plan(a, c, a.MaterialIndex("pelt"), null, Worker());
            HarvestTaskResult result = Played(plan, c.condition.pelt.Count);
            result.stages[0].marks = HarvestMarks.Nicked;
            HarvestTaskResult back = RoundTrip(result);
            Assert.AreEqual(JsonUtility.ToJson(result), JsonUtility.ToJson(back));
            Assert.AreEqual(HarvestMarks.Nicked, back.stages[0].marks);

            var report = new HarvestReport { material = "pelt", materialName = "Bear pelt", skill = (int)HarvestSkill.Skinning, xp = 42f, anatomyXp = 6f, performance = 0.8f, rushed = true };
            report.pieces.Add(new HarvestReportPiece { name = "Bear pelt", count = 1, quality = 0.74f, marks = (int)(HarvestMarks.Pierced | HarvestMarks.Burned), item = "x", tanneryHide = true });
            report.lines.Add("The bear pelt comes off: 92% of it whole.");
            report.learnt.Add("Skinning 3");
            HarvestReport r = HarvestReport.FromJson(report.ToJson());
            Assert.IsNotNull(r);
            Assert.AreEqual(report.ToJson(), r.ToJson());
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(HarvestMarks.Pierced | HarvestMarks.Burned, (HarvestMarks)r.pieces[0].marks);

            HarvestReport refused = HarvestReport.FromJson(HarvestReport.Refused("claws", "Claws", "Someone is working on it.").ToJson());
            Assert.IsFalse(refused.accepted);
            Assert.AreEqual("Someone is working on it.", refused.refusal);

            Assert.IsNull(HarvestReport.FromJson(""));
            Assert.IsNull(HarvestReport.FromJson("{ not json"));

            var skills = new HarvestSkillData();
            skills.AddXp(HarvestSkill.Butchering, 300f);
            HarvestSkillData skillsBack = HarvestSkillData.FromJson(skills.ToJson());
            Assert.AreEqual(skills.Xp(HarvestSkill.Butchering), skillsBack.Xp(HarvestSkill.Butchering), 1e-4);
        }

        [Test]
        public void AClientCannotClaimMoreTimeThanPassed()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = FoughtBear();
            int cells = c.condition.pelt.Count;
            HarvestPlan plan = HarvestPlanner.Plan(a, c, a.MaterialIndex("pelt"), null, Worker());
            float claimed = Played(plan, cells).Seconds;

            // Honest: the work took as long as it says (the server saw it start and end later still).
            HarvestTaskResult honest = Played(plan, cells);
            HarvestRequestGuard.LimitToElapsed(honest, claimed + 0.3);
            Assert.AreEqual(claimed, honest.Seconds, 1e-3, "an honest result keeps its times");
            Assert.IsTrue(HarvestRequestGuard.Check(plan, honest, cells, out string why, out bool rushed), why);
            Assert.IsFalse(rushed);

            // A forged result sent a moment after the work began, claiming careful times.
            HarvestTaskResult forged = Played(plan, cells);
            HarvestRequestGuard.LimitToElapsed(forged, 0.2);
            Assert.LessOrEqual(forged.Seconds, 0.7f + 1e-3f, "held to the time that passed");
            Assert.IsTrue(HarvestRequestGuard.Check(plan, forged, cells, out why, out rushed), why);
            Assert.IsTrue(rushed, "it is judged by the real time: rushed");
            Assert.IsTrue(forged.stages.All(s => (s.marks & HarvestMarks.Rough) != 0));

            // Absurd times are held to the time that passed as well: no claim is too big to scale down.
            HarvestTaskResult huge = Played(plan, cells);
            foreach (HarvestStageResult st in huge.stages) st.seconds = 1e30f;
            HarvestRequestGuard.LimitToElapsed(huge, 0.2);
            Assert.LessOrEqual(huge.Seconds, 0.7f + 1e-3f, "held to the time that passed");
            Assert.IsTrue(HarvestRequestGuard.Check(plan, huge, cells, out why, out rushed), why);
            Assert.IsTrue(rushed, "a huge claim is no way round the time check");

            // Nonsense times end up as no time at all.
            HarvestTaskResult silly = Played(plan, cells);
            silly.stages[0].seconds = float.PositiveInfinity;
            silly.stages[1].seconds = float.NaN;
            HarvestRequestGuard.LimitToElapsed(silly, 5);
            Assert.IsTrue(HarvestRequestGuard.Check(plan, silly, cells, out why, out rushed), why);
            Assert.IsTrue(rushed);
        }
    }
}
#endif
