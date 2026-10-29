#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HarvestingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the harvesting work: every built-in material of every built-in creature can be planned, inside
    /// the work surface and the same way every time; skill and tools widen the margins; no blade, no cut. Then the
    /// minigames played by scripted hands - careful hands do clean work, careless ones tear, spill, snap, splinter and
    /// crack - and the server's check of a result. Plain rules: no scene, no assets.
    /// </summary>
    public class HarvestingMinigameTests
    {
        private const double Frame = 1.0 / 60.0;

        private static HarvestToolProfile Tool(string id, float skinning, float butchering, float fine, float saw, float scraper, float pliers, float chopping, float sharp = 1f) =>
            new HarvestToolProfile
            {
                id = id, displayName = id, sharpness = sharp, quality = 1f, speed = 1f,
                fit = new HarvestToolFit(skinning, butchering, fine, saw, scraper, pliers, chopping),
            };

        /// <summary>A full kit: a proper tool for every kind of work.</summary>
        private static List<HarvestToolProfile> Kit() => new List<HarvestToolProfile>
        {
            Tool("skinning_knife", 1f, 0.5f, 0.4f, 0f, 0.3f, 0f, 0f),
            Tool("butcher_knife", 0.4f, 1f, 0.2f, 0f, 0.2f, 0f, 0.5f),
            Tool("lancet", 0.3f, 0f, 1f, 0f, 0f, 0f, 0f),
            Tool("bone_saw", 0f, 0f, 0f, 1f, 0f, 0f, 0.3f),
            Tool("scraper", 0.2f, 0f, 0f, 0f, 1f, 0f, 0f),
            Tool("pliers", 0f, 0f, 0f, 0f, 0f, 1f, 0f),
        };

        private static HarvestWorker Worker(bool master, IEnumerable<HarvestToolProfile> tools = null, int seed = 5)
        {
            HarvestSettings s = HarvestSettings.Defaults();
            var skills = new HarvestSkillData();
            if (master)
                foreach (HarvestSkill k in Enum.GetValues(typeof(HarvestSkill)))
                    skills.AddXp(k, HarvestSkills.XpFor(s, s.maxLevel) + 1f);
            return new HarvestWorker { settings = s, skills = skills, tools = new HarvestToolbox(tools ?? Kit()), player = 1, seed = seed };
        }

        private static (HarvestAnatomyData a, HarvestCarcass c) Fresh(string id, int seed = 7, bool hideOff = false)
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
            HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, seed));
            if (hideOff) c.hideOff = true;
            return (a, c);
        }

        private static HarvestPlan Plan(HarvestAnatomyData a, HarvestCarcass c, string material, HarvestWorker w, List<int> units = null) =>
            HarvestPlanner.Plan(a, c, a.MaterialIndex(material), units, w);

        private static HarvestStageSpec Only(HarvestPlan p, HarvestStageKind kind) => p.stages.First(s => s.kind == kind);

        // ------------------------------------------------------------------ plans
        [Test]
        public void EveryBuiltInMaterialCanBePlanned()
        {
            var surface = new Rect(-1f, -1f, HarvestPlanner.Width + 2f, HarvestPlanner.Height + 2f);
            HarvestWorker w = Worker(true);
            int planned = 0;
            foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
            {
                // Rare finds made likely, so they are planned too.
                HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, 3, 1f, HarvestAge.Adult, true, 10f));
                c.hideOff = true;
                for (int m = 0; m < a.materials.Count; m++)
                {
                    string what = $"{a.id}/{a.materials[m].id}";
                    HarvestAvailability av = c.Availability(a, w.settings, m, w.skills, 1, out string why);
                    if (av == HarvestAvailability.Missing) continue;
                    Assert.AreEqual(HarvestAvailability.Available, av, $"{what}: {why}");
                    HarvestPlan p = HarvestPlanner.Plan(a, c, m, null, w);
                    Assert.IsTrue(p.Valid, $"{what}: {p.refusal}");
                    Assert.Greater(p.minSeconds, 0f, what);
                    foreach (HarvestStageSpec st in p.stages)
                    {
                        string where = $"{what} {st.kind} '{st.title}'";
                        Assert.IsNotEmpty(st.title, where);
                        Assert.IsNotEmpty(st.instruction, where);
                        Assert.Greater(st.toolFitness, 0f, where);
                        IEnumerable<Vector2> points = st.scene.SelectMany(s => s.points)
                            .Concat(st.paths.SelectMany(x => x.points)).Concat(st.targets.Select(t => t.at))
                            .Concat(st.hazards.SelectMany(z => z.shape)).Concat(st.pieces.Select(x => x.at));
                        foreach (Vector2 pt in points)
                            Assert.IsTrue(surface.Contains(pt), $"{where}: ({pt.x:0}, {pt.y:0}) is off the work surface");
                        switch (st.kind)
                        {
                            case HarvestStageKind.Trace:
                                Assert.IsNotEmpty(st.paths, where);
                                Assert.IsTrue(st.paths.All(x => x.points.Count >= 2 && x.Length > 20f), where);
                                break;
                            case HarvestStageKind.Saw:
                                Assert.IsNotEmpty(st.paths, where);
                                Assert.Greater(st.paths[0].Length, 40f, $"{where}: room for a stroke");
                                break;
                            case HarvestStageKind.Peel:
                                Assert.AreEqual(st.gridW * st.gridH, st.cells.Count, where);
                                Assert.IsTrue(st.cells.Any(b => b > 0), where);
                                Assert.Less(st.bandLow, st.bandHigh, where);
                                Assert.Less(st.bandHigh * HarvestPeelModel.PullPixels, st.area.x, $"{where}: room to pull the left edge out");
                                break;
                            case HarvestStageKind.Scrape:
                                Assert.AreEqual(st.gridW * st.gridH, st.cells.Count, where);
                                Assert.IsTrue(st.cells.Any(b => b != 255 && b > 0), $"{where}: something to scrape");
                                break;
                            case HarvestStageKind.Steady:
                                Assert.IsNotEmpty(st.targets, where);
                                Assert.IsTrue(st.hazards.Any(z => z.self), $"{where}: the piece itself must not be cut");
                                break;
                            case HarvestStageKind.Pull:
                                Assert.Greater((st.pullTo - st.pullFrom).magnitude, 50f, where);
                                // The hand needs room past the band's top (and the catches) in the direction of the pull.
                                Vector2 far = st.pullFrom + (st.pullTo - st.pullFrom).normalized * ((st.bandHigh + 0.28f) * 120f);
                                Assert.IsTrue(surface.Contains(far), $"{where}: no room to pull");
                                break;
                            case HarvestStageKind.Pry:
                                Assert.AreEqual(p.units.Count, st.pieces.Count, where);
                                Assert.Less(st.tolerance, st.speedLimit, $"{where}: the inner marks inside the outer ones");
                                break;
                        }
                    }
                    Assert.AreEqual(UnityEngine.JsonUtility.ToJson(p), UnityEngine.JsonUtility.ToJson(HarvestPlanner.Plan(a, c, m, null, w)), $"{what}: the same plan every time");
                    planned++;
                }
            }
            Assert.Greater(planned, 100);
        }

        [Test]
        public void SkillAndToolsWidenTheMargins()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer");
            var dull = new List<HarvestToolProfile> { Tool("old_knife", 0.6f, 0.6f, 0.3f, 0f, 0.2f, 0f, 0f, 0.2f) };
            HarvestPlan novice = Plan(a, c, "hide", Worker(false, dull)), master = Plan(a, c, "hide", Worker(true));
            Assert.IsTrue(novice.Valid && master.Valid);
            HarvestStageSpec nt = Only(novice, HarvestStageKind.Trace), mt = Only(master, HarvestStageKind.Trace);
            Assert.Greater(mt.tolerance, nt.tolerance * 1.5f, "a wider line");
            Assert.Greater(mt.speedLimit, nt.speedLimit, "a faster safe speed");
            HarvestStageSpec np = Only(novice, HarvestStageKind.Peel), mp = Only(master, HarvestStageKind.Peel);
            Assert.Greater(mp.bandHigh - mp.bandLow, (np.bandHigh - np.bandLow) * 1.5f, "a wider band");
            Assert.Greater(np.bandLow + np.bandHigh, mp.bandLow + mp.bandHigh, "a dull knife needs more pull");
            Assert.Greater(mp.fiddlyHigh - mp.fiddlyLow, np.fiddlyHigh - np.fiddlyLow, "fine work");
            Assert.AreEqual("skinning_knife", mt.toolId, "the right tool is picked");
        }

        [Test]
        public void NoBladeNoCut()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("wolf");
            HarvestWorker bare = Worker(false, new HarvestToolProfile[0]);
            HarvestPlan pelt = Plan(a, c, "pelt", bare);
            Assert.IsFalse(pelt.Valid);
            StringAssert.Contains("tool", pelt.refusal);
            HarvestPlan claws = Plan(a, c, "claws", bare);
            Assert.IsTrue(claws.Valid, "claws can be worked loose by hand: " + claws.refusal);
            Assert.Less(Only(claws, HarvestStageKind.Pry).toolFitness, 0.3f, "but it is the wrong tool");
            HarvestPlan hidden = Plan(a, c, "haunch", Worker(true));
            Assert.IsFalse(hidden.Valid, "the hide comes first");
        }

        [Test]
        public void PlansFollowTheChosenPiecesAndTheBody()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear", 4, true);
            HarvestWorker w = Worker(true);
            HarvestPlan two = Plan(a, c, "femur", w, new List<int> { 1 });
            Assert.AreEqual(new List<int> { 1 }, two.units);
            Assert.IsTrue(two.stages.All(s => s.unit == 1));
            Assert.AreEqual(new[] { HarvestStageKind.Saw, HarvestStageKind.Scrape }, two.stages.Select(s => s.kind).ToArray());

            HarvestPlan heart = Plan(a, c, "heart", w);
            Assert.AreEqual(HarvestStageKind.Trace, heart.stages[0].kind, "the body is opened first");
            Assert.IsTrue(Only(heart, HarvestStageKind.Steady).hazards.Any(z => z.gut), "the guts are in the way");
            c.cavityOpen = true;
            Assert.AreNotEqual(HarvestStageKind.Trace, Plan(a, c, "heart", w).stages[0].kind, "once open, it stays open");

            HarvestPlan shanks = Plan(a, c, "shank", w);
            Assert.AreEqual(4, Only(shanks, HarvestStageKind.Trace).paths.Count, "a shank from each leg");
            HarvestPlan haunch = Plan(a, c, "haunch", w);
            Assert.IsTrue(Only(haunch, HarvestStageKind.Trace).paths[0].zones.Any(z => z.kind == HarvestZoneKind.Joint), "the hip");

            HarvestPlan claws = Plan(a, c, "claws", w);
            Assert.AreEqual(20, Only(claws, HarvestStageKind.Pry).pieces.Count);
            Assert.AreEqual(5, Plan(a, c, "claws", w, new List<int> { 0, 1, 2, 3, 4 }).units.Count);
        }

        [Test]
        public void HiddenFindsAreNotDrawn()
        {
            HarvestAnatomyData a = HarvestDefaults.Bear();
            int bezoar = a.MaterialIndex("bezoar");
            int seed = Enumerable.Range(0, 2000).First(x => HarvestCondition.Create(a, x).Has(bezoar));
            HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, seed));
            HarvestPlan heart = Plan(a, c, "heart", Worker(false));
            Assert.IsTrue(heart.Valid, heart.refusal);
            Assert.IsFalse(heart.stages[0].scene.Any(s => s.unit / 1000 == bezoar && s.unit >= 0), "a novice does not see the bezoar");
            HarvestPlan expert = Plan(a, c, "heart", Worker(true));
            Assert.IsTrue(expert.stages[0].scene.Any(s => s.unit >= 0 && s.unit / 1000 == bezoar), "an expert does");
        }

        [Test]
        public void OrganCutMarksStayClearOfTheGut()
        {
            // A mark inside (or touching) a gut, bladder or gall could not be cut without spilling it.
            foreach (bool master in new[] { true, false })
            {
                HarvestWorker w = Worker(master);
                foreach (HarvestAnatomyData built in HarvestDefaults.Anatomies())
                    for (int seed = 1; seed <= 200; seed++)
                    {
                        HarvestAnatomyData a = built.Clone();
                        foreach (HarvestMaterial m in a.materials) m.chance = 1f;
                        HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, seed));
                        c.hideOff = true;
                        for (int mi = 0; mi < a.materials.Count; mi++)
                        {
                            if (a.materials[mi].method != HarvestMethod.Organs) continue;
                            foreach (HarvestStageSpec st in HarvestPlanner.Plan(a, c, mi, null, w).stages.Where(x => x.kind == HarvestStageKind.Steady))
                                foreach (HarvestTarget t in st.targets)
                                    foreach (HarvestHazard h in st.hazards.Where(z => z.gut))
                                    {
                                        Vector2[] poly = h.shape.ToArray();
                                        float d = float.MaxValue;
                                        for (int i = 0; i < poly.Length; i++)
                                            d = Math.Min(d, HarvestDiagram.SegmentDistance(t.at, poly[i], poly[(i + 1) % poly.Length], out _));
                                        string where = $"{a.id}/{a.materials[mi].id} seed {seed}: '{st.title}' mark at ({t.at.x:0}, {t.at.y:0})";
                                        Assert.IsFalse(HarvestDiagram.Contains(poly, t.at), $"{where} is inside the {h.name}");
                                        Assert.GreaterOrEqual(d, t.radius, $"{where} touches the {h.name}");
                                    }
                        }
                    }
            }
        }

        // ------------------------------------------------------------------ scripted hands
        private static HarvestStageModel Model(HarvestStageSpec spec, int seed = 3) => HarvestStageModel.Create(spec, new CraftHands(seed, 0.5));

        /// <summary>Follows every path of a Trace stage at <paramref name="speed"/> px/s (<paramref name="offset"/> px beside the line).</summary>
        private static HarvestTraceModel Trace(HarvestStageSpec spec, float speed, float offset = 0f)
        {
            var m = (HarvestTraceModel)Model(spec);
            for (int guard = 0; guard < spec.paths.Count + 2 && !m.Done; guard++)
            {
                HarvestPath path = m.CurrentPath;
                int index = m.PathIndex;
                Vector2 Side(float s)
                {
                    Vector2 d = path.At(s + 1f) - path.At(Math.Max(0f, s - 1f));
                    Vector2 n = new Vector2(-d.y, d.x).normalized;
                    return path.At(s) + n * offset;
                }
                m.OnPointer(HarvestPointer.Down, Side(m.Reached));
                float s = m.Reached;
                for (int f = 0; f < 60 * 120 && !m.Done && m.PathIndex == index; f++)
                {
                    s = Math.Min(path.Length, s + (float)(speed * Frame));
                    m.OnPointer(HarvestPointer.Drag, Side(s));
                    m.Tick(Frame);
                }
                m.OnPointer(HarvestPointer.Up, Side(s));
            }
            return m;
        }

        [Test]
        public void CarefulCutsAreCleanRushedOnesOpenTheGut()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer");
            HarvestStageSpec open = Only(Plan(a, c, "hide", Worker(false)), HarvestStageKind.Trace);
            HarvestStageResult careful = Trace(open, open.speedLimit * 0.35f).Finish()[0];
            Assert.Greater(careful.precision, 0.8f);
            Assert.Less(careful.damage, 0.1f);
            Assert.AreEqual(0f, careful.spill, 1e-6);
            Assert.AreEqual(HarvestMarks.None, careful.marks & HarvestMarks.Contaminated);

            HarvestTraceModel rushedModel = Trace(open, open.speedLimit * 2.5f);
            HarvestStageResult rushed = rushedModel.Finish()[0];
            Assert.Greater(rushed.spill, 0f, "rushing over the gut opens it");
            Assert.IsTrue((rushed.marks & HarvestMarks.Contaminated) != 0);
            Assert.Greater(rushed.damage, careful.damage);
            Assert.Greater(rushedModel.Mistakes, 0);

            HarvestStageResult ragged = Trace(open, open.speedLimit * 0.35f, open.tolerance * 2f).Finish()[0];
            Assert.Less(ragged.precision, careful.precision, "off the line is ragged");
            Assert.Less(ragged.completeness, careful.completeness, "and wasteful");
        }

        [Test]
        public void TheSameHandsGiveTheSameResult()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("wolf");
            HarvestStageSpec open = Only(Plan(a, c, "pelt", Worker(false)), HarvestStageKind.Trace);
            string one = UnityEngine.JsonUtility.ToJson(Trace(open, 120f).Finish()[0]);
            string two = UnityEngine.JsonUtility.ToJson(Trace(open, 120f).Finish()[0]);
            Assert.AreEqual(one, two);
        }

        /// <summary>Peels a hide holding the pull at <paramref name="pull"/> (a fraction of the band: 0 its bottom, 1 its top).</summary>
        private static HarvestPeelModel Peel(HarvestStageSpec spec, float pull, double seconds = 600)
        {
            var m = (HarvestPeelModel)Model(spec);
            int w = spec.gridW, h = spec.gridH;
            float cw = spec.area.width / w, ch = spec.area.height / h;
            bool down = false;
            for (int f = 0; f < seconds * 60 && !m.Done; f++)
            {
                int row = -1, side = 0, cell = -1;
                for (int y = 0; y < h && row < 0; y++)
                {
                    int left = m.FrontierCell(y, -1);
                    if (left < 0) continue;
                    row = y;
                    // From the left while the next cell is in the left half, else from the right.
                    side = left % w < w / 2 ? -1 : 1;
                    cell = side < 0 ? left : m.FrontierCell(y, 1);
                }
                if (row < 0) break;
                int cx = cell % w;
                bool fiddly = spec.cells[cell] == 2;
                float low = fiddly ? spec.fiddlyLow : spec.bandLow, high = fiddly ? spec.fiddlyHigh : spec.bandHigh;
                float t = low + (high - low) * pull;
                float y0 = spec.area.yMax - (row + 0.5f) * ch;
                float x0 = side < 0 ? spec.area.x + cx * cw - t * HarvestPeelModel.PullPixels : spec.area.x + (cx + 1) * cw + t * HarvestPeelModel.PullPixels;
                var p = new Vector2(x0, y0);
                m.OnPointer(down ? HarvestPointer.Drag : HarvestPointer.Down, p);
                down = true;
                m.Tick(Frame);
            }
            return m;
        }

        [Test]
        public void APullInTheBandPeelsCleanlyTooHardTears()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("wolf");
            HarvestStageSpec peel = Only(Plan(a, c, "pelt", Worker(true)), HarvestStageKind.Peel);
            HarvestPeelModel good = Peel(peel, 0.5f);
            Assert.IsTrue(good.Done, "the hide comes off");
            HarvestStageResult clean = good.Finish()[0];
            Assert.AreEqual(1f, clean.completeness, 1e-4);
            Assert.AreEqual(HarvestMarks.None, clean.marks & HarvestMarks.Ripped, "no tears");
            Assert.Greater(clean.precision, 0.6f);
            Assert.AreEqual(c.condition.pelt.Count, clean.cellDamage.Count, "a value for every cell of the hide");

            HarvestStageResult torn = Peel(peel, 2.2f, 60).Finish()[0];
            Assert.IsTrue((torn.marks & HarvestMarks.Ripped) != 0, "pulling too hard rips it");
            Assert.Greater(torn.damage, clean.damage);
            Assert.Less(torn.precision, clean.precision);

            // What the hands did reaches the pelt.
            HarvestOutcome o = HarvestResolver.Resolve(a, HarvestSettings.Defaults(), c, new HarvestTaskResult
            {
                material = "pelt",
                stages = new List<HarvestStageResult> { Trace(Only(Plan(a, c, "pelt", Worker(true)), HarvestStageKind.Trace), 80f).Finish()[0], torn },
            }, new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsTrue((o.pieces[0].marks & HarvestMarks.Ripped) != 0);
        }

        /// <summary>Holds the knife on each mark in turn (<paramref name="offset"/> px off it).</summary>
        private static HarvestSteadyModel Steady(HarvestStageSpec spec, Vector2 offset)
        {
            var m = (HarvestSteadyModel)Model(spec);
            for (int f = 0; f < 60 * 120 && !m.Done; f++)
            {
                int next = Enumerable.Range(0, spec.targets.Count).First(i => m.CutOf(i) < 1f);
                Vector2 p = spec.targets[next].at + offset;
                if (!m.Holding) m.OnPointer(HarvestPointer.Down, p);
                // Correct for the hand's wander, as a player watching the knife would.
                m.OnPointer(HarvestPointer.Drag, p - m.Wander * 0.8f);
                m.Tick(Frame);
            }
            return m;
        }

        [Test]
        public void AHeldKnifeFreesTheTendonAWanderingOneCutsIt()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 2, true);
            HarvestPlan plan = Plan(a, c, "leg_sinew", Worker(true), new List<int> { 0 });
            HarvestStageSpec free = Only(plan, HarvestStageKind.Steady);
            HarvestSteadyModel held = Steady(free, Vector2.zero);
            Assert.IsTrue(held.Done);
            HarvestStageResult good = held.Finish()[0];
            Assert.AreEqual(0f, good.damage, 1e-4, "the tendon is not touched");
            Assert.Greater(good.precision, 0.8f);

            // Pressing toward the tendon's middle: the knife cuts into it.
            Vector2 a0 = free.targets[0].at, b0 = free.targets[1].at;
            HarvestSteadyModel sloppy = (HarvestSteadyModel)Model(free);
            Vector2 mid = (a0 + b0) * 0.5f;
            sloppy.OnPointer(HarvestPointer.Down, a0);
            for (int f = 0; f < 60 * 2; f++)
            {
                sloppy.OnPointer(HarvestPointer.Drag, mid);
                sloppy.Tick(Frame);
            }
            HarvestStageResult bad = sloppy.Finish()[0];
            Assert.Greater(bad.damage, 0.1f);
            Assert.IsTrue((bad.marks & HarvestMarks.Nicked) != 0);
        }

        /// <summary>Pulls, keeping the pull at <paramref name="pull"/> of the current band (0 bottom, 1 top).</summary>
        private static HarvestPullModel Pull(HarvestStageSpec spec, float pull, double seconds = 120)
        {
            var m = (HarvestPullModel)Model(spec);
            Vector2 grip = spec.pullFrom, dir = (spec.pullTo - spec.pullFrom).normalized;
            m.OnPointer(HarvestPointer.Down, grip);
            for (int f = 0; f < seconds * 60 && !m.Done; f++)
            {
                m.Band(out float low, out float high);
                m.OnPointer(HarvestPointer.Drag, grip + dir * ((low + (high - low) * pull) * 120f));
                m.Tick(Frame);
            }
            return m;
        }

        [Test]
        public void AnEvenDrawComesOutWholeAYankSnaps()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 2, true);
            HarvestStageSpec draw = Only(Plan(a, c, "back_sinew", Worker(false)), HarvestStageKind.Pull);
            HarvestPullModel even = Pull(draw, 0.5f);
            Assert.IsTrue(even.Done);
            HarvestStageResult whole = even.Finish()[0];
            Assert.AreEqual(HarvestMarks.None, whole.marks & HarvestMarks.Ripped);
            Assert.AreEqual(1f, whole.completeness, 1e-4);

            HarvestStageResult snapped = Pull(draw, 3f).Finish()[0];
            Assert.IsTrue((snapped.marks & HarvestMarks.Ripped) != 0, "it snaps");
            Assert.Greater(snapped.damage, whole.damage);
        }

        /// <summary>Saws back and forth along the line, each stroke taking <paramref name="stroke"/> seconds.</summary>
        private static HarvestSawModel Saw(HarvestStageSpec spec, double stroke, float offset = 0f)
        {
            var m = (HarvestSawModel)Model(spec);
            Vector2 a = spec.paths[0].points[0], b = spec.paths[0].points[spec.paths[0].points.Count - 1];
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
            float len = (b - a).magnitude;
            Vector2 At(double t)
            {
                double phase = t / stroke % 2.0;
                double u = phase < 1 ? phase : 2 - phase;
                return a + d * (float)(len * (0.1 + 0.8 * u)) + n * offset;
            }
            m.OnPointer(HarvestPointer.Down, At(0));
            double time = 0;
            for (int f = 0; f < 60 * 120 && !m.Done; f++)
            {
                time += Frame;
                m.OnPointer(HarvestPointer.Drag, At(time));
                m.Tick(Frame);
            }
            return m;
        }

        [Test]
        public void EvenStrokesSawCleanSnatchedOnesSplinter()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear", 2, true);
            HarvestStageSpec saw = Only(Plan(a, c, "femur", Worker(false), new List<int> { 0 }), HarvestStageKind.Saw);
            HarvestSawModel even = Saw(saw, saw.seconds);
            Assert.IsTrue(even.Done);
            HarvestStageResult clean = even.Finish()[0];
            Assert.Less(clean.damage, 0.05f);
            Assert.Greater(clean.precision, 0.8f);

            HarvestStageResult snatched = Saw(saw, saw.seconds * 0.3).Finish()[0];
            Assert.Greater(snatched.damage, clean.damage);
            Assert.IsTrue((snatched.marks & HarvestMarks.Splintered) != 0);

            HarvestStageResult crooked = Saw(saw, saw.seconds, saw.tolerance * 2.5f).Finish()[0];
            Assert.Less(crooked.precision, clean.precision);
        }

        /// <summary>Rocks each piece in turn, swinging <paramref name="reach"/> px each way every <paramref name="period"/> seconds.</summary>
        private static HarvestPryModel Pry(HarvestStageSpec spec, float reach, double period)
        {
            var m = (HarvestPryModel)Model(spec);
            int current = -1;
            double t = 0;
            for (int f = 0; f < 60 * 300 && !m.Done; f++)
            {
                if (m.Current != current)
                {
                    current = m.Current;
                    m.OnPointer(HarvestPointer.Up, m.Piece.at);
                    m.OnPointer(HarvestPointer.Down, m.Piece.at);
                    t = 0;
                }
                t += Frame;
                float side = (int)(t / period) % 2 == 0 ? 1f : -1f;
                m.OnPointer(HarvestPointer.Drag, m.Piece.at + new Vector2(side * reach, 0f));
                m.Tick(Frame);
            }
            return m;
        }

        [Test]
        public void GentleRockingFreesClawsWildSwingsCrackThem()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("wolf");
            HarvestStageSpec pry = Only(Plan(a, c, "claws", Worker(false), new List<int> { 0, 1, 2 }), HarvestStageKind.Pry);
            float inner = pry.tolerance, outer = pry.speedLimit;
            HarvestPryModel gentle = Pry(pry, (inner + outer) * 0.5f, 0.35);
            Assert.IsTrue(gentle.Done);
            List<HarvestStageResult> good = gentle.Finish();
            Assert.AreEqual(3, good.Count, "one result a claw");
            Assert.IsTrue(good.All(r => (r.marks & HarvestMarks.Cracked) == 0));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, good.Select(r => r.unit).ToArray());

            List<HarvestStageResult> wild = Pry(pry, outer * 2.2f, 0.35).Finish();
            Assert.IsTrue(wild.Any(r => (r.marks & HarvestMarks.Cracked) != 0), "too far cracks them");
        }

        [Test]
        public void BriskStrokesCleanABoneDawdlingGougesIt()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 2, true);
            HarvestStageSpec scrape = Only(Plan(a, c, "femur", Worker(false), new List<int> { 0 }), HarvestStageKind.Scrape);
            var brisk = (HarvestScrapeModel)Model(scrape);
            // A raster over the piece at a brisk pace.
            Rect r = scrape.area;
            float x = r.x, y = r.yMax - 6f, dir = 1f;
            brisk.OnPointer(HarvestPointer.Down, new Vector2(x, y));
            for (int f = 0; f < 60 * 240 && !brisk.Done; f++)
            {
                x += dir * 220f * (float)Frame;
                if (x > r.xMax || x < r.x)
                {
                    dir = -dir;
                    x = Math.Max(r.x, Math.Min(r.xMax, x));
                    y -= 10f;
                    if (y < r.y) y = r.yMax - 6f;
                }
                brisk.OnPointer(HarvestPointer.Drag, new Vector2(x, y));
                brisk.Tick(Frame);
            }
            Assert.IsTrue(brisk.Done, "brisk strokes finish it ('Clean.') without Accept");
            brisk.Accept();
            HarvestStageResult clean = brisk.Finish()[0];
            Assert.Greater(clean.completeness, 0.95f);

            var lazy = (HarvestScrapeModel)Model(scrape);
            Vector2 spot = Enumerable.Range(0, scrape.cells.Count).Where(i => scrape.cells[i] != 255)
                .Select(i => new Vector2(r.x + (i % scrape.gridW + 0.5f) * r.width / scrape.gridW, r.yMax - (i / scrape.gridW + 0.5f) * r.height / scrape.gridH)).First();
            lazy.OnPointer(HarvestPointer.Down, spot);
            for (int f = 0; f < 60 * 20; f++)
            {
                lazy.OnPointer(HarvestPointer.Drag, spot);
                lazy.Tick(Frame);
            }
            lazy.Accept();
            HarvestStageResult gouged = lazy.Finish()[0];
            Assert.Greater(gouged.damage, clean.damage, "dawdling on bare bone gouges it");
        }

        // ------------------------------------------------------------------ the server's check
        private static HarvestTaskResult Played(HarvestPlan plan)
        {
            var r = new HarvestTaskResult { material = plan.material, units = new List<int>(plan.units) };
            foreach (HarvestStageSpec st in plan.stages)
            {
                if (st.kind == HarvestStageKind.Pry)
                {
                    foreach (HarvestPieceSpot p in st.pieces)
                        r.stages.Add(new HarvestStageResult { kind = st.kind, unit = p.unit, precision = 0.9f, completeness = 1f, seconds = st.minSeconds + 2f });
                    continue;
                }
                var res = new HarvestStageResult { kind = st.kind, unit = st.unit, precision = 0.9f, completeness = 1f, seconds = st.minSeconds + 2f };
                if (st.kind == HarvestStageKind.Peel)
                {
                    res.cellDamage = Enumerable.Repeat(0f, st.gridW * st.gridH).ToList();
                    res.cellFlesh = Enumerable.Repeat(0.1f, st.gridW * st.gridH).ToList();
                    res.lostCells = new List<int>();
                }
                r.stages.Add(res);
            }
            return r;
        }

        [Test]
        public void TheServerHoldsResultsToItsPlan()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear");
            HarvestWorker w = Worker(false);
            HarvestPlan pelt = Plan(a, c, "pelt", w);
            int cells = c.condition.pelt.Count;

            HarvestTaskResult honest = Played(pelt);
            honest.stages[0].toolFitness = 1f;
            honest.stages[0].tool = "masterwork";
            honest.stages[0].precision = 7f;
            honest.stages[0].marks = HarvestMarks.Burned | HarvestMarks.Nicked;
            Assert.IsTrue(HarvestRequestGuard.Check(pelt, honest, cells, out string why, out bool rushed), why);
            Assert.IsFalse(rushed);
            Assert.AreEqual(1f, honest.stages[0].precision, "bounded");
            Assert.AreEqual(pelt.stages[0].toolFitness, honest.stages[0].toolFitness, "the server's tool");
            Assert.AreEqual(pelt.stages[0].toolId, honest.stages[0].tool);
            Assert.AreEqual(HarvestMarks.Nicked, honest.stages[0].marks, "only harvest marks");

            HarvestTaskResult fast = Played(pelt);
            foreach (HarvestStageResult s in fast.stages) s.seconds = 0.01f;
            Assert.IsTrue(HarvestRequestGuard.Check(pelt, fast, cells, out why, out rushed), why);
            Assert.IsTrue(rushed, "too fast to be careful");
            Assert.IsTrue(fast.stages.All(s => s.precision < 0.1f && (s.marks & HarvestMarks.Rough) != 0), "rushed work is rough work");

            HarvestTaskResult skipped = Played(pelt);
            skipped.stages.RemoveAt(1);
            Assert.IsFalse(HarvestRequestGuard.Check(pelt, skipped, cells, out _, out _));

            HarvestTaskResult wrongHide = Played(pelt);
            wrongHide.stages[1].cellDamage.RemoveAt(0);
            Assert.IsFalse(HarvestRequestGuard.Check(pelt, wrongHide, cells, out _, out _));

            HarvestTaskResult other = Played(pelt);
            other.material = "claws";
            Assert.IsFalse(HarvestRequestGuard.Check(pelt, other, cells, out _, out _));

            HarvestPlan claws = Plan(a, c, "claws", w, new List<int> { 0, 1 });
            HarvestTaskResult more = Played(claws);
            more.units = new List<int> { 0, 1, 2, 3, 4 };
            Assert.IsTrue(HarvestRequestGuard.Check(claws, more, cells, out why, out _), why);
            CollectionAssert.AreEqual(new[] { 0, 1 }, more.units, "only the planned pieces");
            HarvestTaskResult swapped = Played(claws);
            swapped.stages[0].unit = 7;
            Assert.IsFalse(HarvestRequestGuard.Check(claws, swapped, cells, out _, out _));
        }

        [Test]
        public void AScrapeIsNoCleanerThanItIsComplete()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 2, true);
            HarvestPlan femur = Plan(a, c, "femur", Worker(false), new List<int> { 0 });
            HarvestTaskResult r = Played(femur);
            HarvestStageResult scrape = r.stages.First(s => s.kind == HarvestStageKind.Scrape);
            scrape.completeness = 0.5f;
            scrape.precision = 1f;
            Assert.IsTrue(HarvestRequestGuard.Check(femur, r, c.condition.pelt.Count, out string why, out _), why);
            Assert.LessOrEqual(scrape.precision, 0.5f, "half the work cannot claim a clean bone");
        }

        [Test]
        public void CarefulPlayIsNeverTakenForRushing()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer");
            foreach (bool master in new[] { false, true })
            {
                HarvestPlan plan = Plan(a, c, "hide", Worker(master));
                HarvestStageSpec open = Only(plan, HarvestStageKind.Trace), peel = Only(plan, HarvestStageKind.Peel);
                var r = new HarvestTaskResult
                {
                    material = plan.material,
                    stages = new List<HarvestStageResult> { Trace(open, open.speedLimit * 0.9f).Finish()[0], Peel(peel, 0.9f).Finish()[0] },
                };
                Assert.IsTrue(HarvestRequestGuard.Check(plan, r, c.condition.pelt.Count, out string why, out bool rushed), why);
                Assert.IsFalse(rushed, $"{(master ? "a master" : "a novice")} working briskly but carefully: {r.stages[0].seconds:0.0}s / {open.minSeconds:0.0}s, {r.stages[1].seconds:0.0}s / {peel.minSeconds:0.0}s");
            }
        }
    }
}
#endif
