#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AgricultureSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the agriculture rules (plain C#: no scene, no assets): soil fields baked from their layout
    /// (base soil, sections of every shape, soft edges, painted cells), the soil's own life (catching up, leaching, liming,
    /// the organic cycle), the calendar and the city's climate, seed genes, growth (every species tended and untended),
    /// instruments and what a player is told (symptoms, never causes), per-copy item codes and saving.
    /// </summary>
    public class AgricultureCoreTests
    {
        private static AgriCatalog Catalog() => AgriCatalog.BuiltIn();

        private static SoilLayout Plot(SoilType type, float width = 4f, float height = 4f) => new SoilLayout
        {
            outline = new List<Vector2>
            {
                new Vector2(-width * 0.5f, -height * 0.5f), new Vector2(width * 0.5f, -height * 0.5f),
                new Vector2(width * 0.5f, height * 0.5f), new Vector2(-width * 0.5f, height * 0.5f),
            },
            baseSoil = SoilProfile.Preset(type),
            variation = 0f,
            cellSize = 0.25f,
        };

        private static SoilGrid Bake(SoilLayout l, double now = 0.0)
        {
            SoilGrid g = l.CreateGrid();
            l.Bake(g, now);
            return g;
        }

        private static int Cell(SoilGrid g, Vector2 p)
        {
            Assert.IsTrue(g.TryIndex(p, out int i), $"{p} is not on the field");
            return i;
        }

        private static float At(SoilGrid g, Vector2 p, SoilChannel c) => g.Get(Cell(g, p), c);

        private static SoilSection Section(SoilSectionShape shape, float ph)
        {
            var s = new SoilSection { shape = shape, feather = 0f };
            s.soil.values.ph = ph;
            s.soil.SetOverride(SoilChannel.Ph, true);
            return s;
        }

        // ================================================================== the layout
        [Test]
        public void BaseSoilFillsTheField()
        {
            SoilGrid g = Bake(Plot(SoilType.Clay));
            SoilProfile clay = SoilProfile.Preset(SoilType.Clay);
            Assert.Greater(g.InsideCount, 200);
            for (int i = 0; i < g.Count; i++)
            {
                if (!g.Inside[i]) continue;
                Assert.AreEqual(clay.ph, g.Get(i, SoilChannel.Ph), 1e-4f);
                Assert.AreEqual(clay.nitrogen, g.Get(i, SoilChannel.Nitrogen), 1e-3f);
                Assert.AreEqual(SoilType.Clay, g.TypeAt(i));
            }
        }

        [Test]
        public void ASectionChangesOnlyItsPart()
        {
            SoilLayout l = Plot(SoilType.Clay);
            SoilSection s = Section(SoilSectionShape.Rectangle, 5f);
            s.center = new Vector2(1f, 0f);
            s.size = new Vector2(1.5f, 1.5f);
            s.soil.overrideType = true;
            s.soil.type = SoilType.Peat;
            l.sections.Add(s);
            SoilGrid g = Bake(l);
            Assert.AreEqual(5f, At(g, new Vector2(1f, 0f), SoilChannel.Ph), 1e-3f);
            Assert.AreEqual(SoilType.Peat, g.TypeAt(Cell(g, new Vector2(1f, 0f))));
            Assert.AreEqual(6.8f, At(g, new Vector2(-1.5f, 0f), SoilChannel.Ph), 1e-3f, "outside the section: the base soil");
            Assert.AreEqual(SoilType.Clay, g.TypeAt(Cell(g, new Vector2(-1.5f, 0f))));
            Assert.AreEqual(SoilProfile.Preset(SoilType.Clay).nitrogen, At(g, new Vector2(1f, 0f), SoilChannel.Nitrogen), 1e-3f,
                "a property the section does not tick is the base soil's");
        }

        [Test]
        public void ASoftEdgeBlendsIntoTheSoilAround()
        {
            SoilLayout l = Plot(SoilType.Clay, 6f, 4f);
            SoilSection s = Section(SoilSectionShape.Rectangle, 5f);
            s.size = new Vector2(2f, 2f);
            s.feather = 1.2f;
            l.sections.Add(s);
            SoilGrid g = Bake(l);
            float inside = At(g, new Vector2(0f, 0f), SoilChannel.Ph);
            float edge = At(g, new Vector2(1.05f, 0f), SoilChannel.Ph);
            float outside = At(g, new Vector2(2.4f, 0f), SoilChannel.Ph);
            Assert.AreEqual(5f, inside, 1e-3f);
            Assert.AreEqual(6.8f, outside, 1e-3f);
            Assert.Greater(edge, 5.05f);
            Assert.Less(edge, 6.75f);
        }

        [Test]
        public void LaterSectionsWinWhereSectionsOverlap()
        {
            SoilLayout l = Plot(SoilType.Clay);
            SoilSection a = Section(SoilSectionShape.Circle, 5f);
            a.radius = 1.5f;
            SoilSection b = Section(SoilSectionShape.Circle, 8f);
            b.radius = 0.6f;
            l.sections.Add(a);
            l.sections.Add(b);
            SoilGrid g = Bake(l);
            Assert.AreEqual(8f, At(g, Vector2.zero, SoilChannel.Ph), 1e-3f);
            Assert.AreEqual(5f, At(g, new Vector2(1.1f, 0f), SoilChannel.Ph), 1e-3f);
        }

        [Test]
        public void PolygonSectionsFollowTheirPoints()
        {
            SoilLayout l = Plot(SoilType.Sandy);
            SoilSection s = Section(SoilSectionShape.Polygon, 7.5f);
            s.points = new List<Vector2> { new Vector2(-1.8f, -1.8f), new Vector2(0f, -1.8f), new Vector2(-1.8f, 0f) };
            l.sections.Add(s);
            SoilGrid g = Bake(l);
            Assert.AreEqual(7.5f, At(g, new Vector2(-1.4f, -1.4f), SoilChannel.Ph), 1e-3f);
            Assert.AreEqual(6.2f, At(g, new Vector2(1.4f, 1.4f), SoilChannel.Ph), 1e-3f);
        }

        [Test]
        public void PaintedCellsTakeTheirSection()
        {
            SoilLayout l = Plot(SoilType.Clay);
            SoilSection painted = Section(SoilSectionShape.Painted, 8f);
            l.sections.Add(painted);
            l.EnsurePaintMap();
            Assert.IsTrue(l.Paint(new Vector2(1f, 1f), 0.3f, 1));
            SoilGrid g = Bake(l);
            Assert.AreEqual(8f, At(g, new Vector2(1f, 1f), SoilChannel.Ph), 1e-3f);
            Assert.AreEqual(6.8f, At(g, new Vector2(-1f, -1f), SoilChannel.Ph), 1e-3f, "unpainted cells keep the base soil");
        }

        [Test]
        public void PaintingTheBaseSoilCutsAHoleInASection()
        {
            SoilLayout l = Plot(SoilType.Clay);
            SoilSection s = Section(SoilSectionShape.Rectangle, 5f);
            s.size = new Vector2(3f, 3f);
            l.sections.Add(s);
            l.EnsurePaintMap();
            l.Paint(Vector2.zero, 0.3f, SoilLayout.PaintBase);
            SoilGrid g = Bake(l);
            Assert.AreEqual(6.8f, At(g, Vector2.zero, SoilChannel.Ph), 1e-3f);
            Assert.AreEqual(5f, At(g, new Vector2(1f, 1f), SoilChannel.Ph), 1e-3f);
            l.ClearPaint();
            Assert.AreEqual(5f, At(Bake(l), Vector2.zero, SoilChannel.Ph), 1e-3f, "cleared paint: the shape decides again");
        }

        [Test]
        public void RemovingASectionKeepsTheOtherSectionsPaint()
        {
            SoilLayout l = Plot(SoilType.Clay);
            l.sections.Add(Section(SoilSectionShape.Painted, 5f));
            l.sections.Add(Section(SoilSectionShape.Painted, 8f));
            l.EnsurePaintMap();
            l.Paint(new Vector2(1f, 1f), 0.3f, 2);
            l.sections.RemoveAt(0);
            l.SectionRemoved(0);
            Assert.AreEqual(8f, At(Bake(l), new Vector2(1f, 1f), SoilChannel.Ph), 1e-3f);
        }

        [Test]
        public void TryingAnotherCellSizeKeepsThePaint()
        {
            SoilLayout l = Plot(SoilType.Clay);
            l.sections.Add(Section(SoilSectionShape.Painted, 8f));
            // A disc around the middle of a 1 m cell: a coarse cell takes the paint at its centre.
            l.Paint(new Vector2(1.5f, 0.5f), 0.45f, 1);
            byte[] before = (byte[])l.painted.Clone();
            l.cellSize = 1f;
            SoilGrid coarse = Bake(l);
            Assert.AreEqual(8f, At(coarse, new Vector2(1.9f, 0.9f), SoilChannel.Ph), 1e-3f, "the paint shows at the coarse cells too");
            l.cellSize = 0.25f;
            CollectionAssert.AreEqual(before, l.painted, "baking (and previews) never rewrite the painted map");
            SoilGrid fine = Bake(l);
            Assert.AreEqual(8f, At(fine, new Vector2(1.5f, 0.5f), SoilChannel.Ph), 1e-3f);
            Assert.AreEqual(6.8f, At(fine, new Vector2(1.9f, 0.9f), SoilChannel.Ph), 1e-3f, "...and none of it grew coarser");
        }

        [Test]
        public void CentringTheLayoutLeavesTheSoilWhereItIs()
        {
            SoilLayout l = Plot(SoilType.Clay);
            l.variation = 0.5f;
            l.outline = new List<Vector2> { new Vector2(0.3f, 0.6f), new Vector2(5.1f, 0.6f), new Vector2(5.1f, 3.9f), new Vector2(0.3f, 3.9f) };
            SoilSection circle = Section(SoilSectionShape.Circle, 5f);
            circle.center = new Vector2(2f, 2f);
            circle.radius = 0.8f;
            circle.feather = 0.4f;
            l.sections.Add(circle);
            l.sections.Add(Section(SoilSectionShape.Painted, 8f));
            l.Paint(new Vector2(4f, 3f), 0.4f, 2);
            SoilGrid before = Bake(l);
            // What the inspector's Centre does: the object moves by the offset, the layout the other way.
            Vector2 c = l.CentreOffset();
            Assert.AreNotEqual(Vector2.zero, c);
            l.Shift(c);
            SoilGrid after = Bake(l);
            int compared = 0;
            for (int i = 0; i < before.Count; i++)
            {
                if (!before.Inside[i]) continue;
                Vector2 p = before.Center(i);
                Assert.IsTrue(after.TryIndex(p - c, out int j) && after.Inside[j], $"{p} left the field");
                for (int ch = 0; ch < SoilChannels.Authored; ch++)
                    Assert.AreEqual(before.Get(i, (SoilChannel)ch), after.Get(j, (SoilChannel)ch), 1e-3f, $"{(SoilChannel)ch} at {p}");
                compared++;
            }
            Assert.Greater(compared, 200);
        }

        [Test]
        public void SectionsKeepTheirIdsThroughReordering()
        {
            SoilLayout l = Plot(SoilType.Clay);
            l.sections.Add(Section(SoilSectionShape.Circle, 5f));
            l.sections.Add(Section(SoilSectionShape.Circle, 6f));
            Assert.IsTrue(l.EnsureSectionUids());
            l.sections.Add(l.sections[0].Clone());
            Assert.IsTrue(l.EnsureSectionUids(), "a copy gets an id of its own");
            Assert.AreEqual(3, l.sections.Select(x => x.uid).Distinct().Count());
            Assert.IsFalse(l.EnsureSectionUids(), "ids are kept once given");
            int second = l.sections[1].uid;
            (l.sections[0], l.sections[1]) = (l.sections[1], l.sections[0]);
            Assert.AreEqual(0, l.IndexOfUid(second));
            Assert.AreEqual(-1, l.IndexOfUid(0));
        }

        [Test]
        public void ARemovedSectionsIdIsNotGivenAgain()
        {
            SoilLayout l = Plot(SoilType.Clay);
            l.sections.Add(Section(SoilSectionShape.Circle, 5f));
            l.sections.Add(Section(SoilSectionShape.Circle, 6f));
            l.EnsureSectionUids();
            int gone = l.sections[1].uid;
            l.sections.RemoveAt(1);
            // A brush or selection still holding the removed section's id must not wake up on a new section.
            for (int k = 0; k < 1000; k++)
            {
                int id = l.NewSectionUid();
                Assert.AreNotEqual(gone, id);
                Assert.Greater(id, 0);
                Assert.AreEqual(-1, l.IndexOfUid(id), "a new id is no section's");
            }
        }

        [Test]
        public void TheLayoutHashFollowsEveryChange()
        {
            SoilLayout l = Plot(SoilType.Clay);
            int h0 = l.Hash();
            Assert.AreEqual(h0, Plot(SoilType.Clay).Hash(), "the same layout, the same hash");
            l.baseSoil.ph = 6f;
            int h1 = l.Hash();
            Assert.AreNotEqual(h0, h1);
            l.sections.Add(Section(SoilSectionShape.Circle, 5f));
            Assert.AreNotEqual(h1, l.Hash());
        }

        [Test]
        public void ValidationFindsABrokenOutline()
        {
            SoilLayout l = Plot(SoilType.Clay);
            l.outline = new List<Vector2> { Vector2.zero, Vector2.one };
            var errors = new List<string>();
            var warnings = new List<string>();
            l.Validate(errors, warnings);
            Assert.IsNotEmpty(errors);
        }

        // ================================================================== the soil's own life
        [Test]
        public void ACellCaughtUpAtOnceMatchesOneUpdatedEveryHour()
        {
            AgriCatalog cat = Catalog();
            AgriSettings s = cat.Settings;
            foreach (SoilType type in new[] { SoilType.Sandy, SoilType.Clay, SoilType.OrganicRich })
            {
                SoilGrid hourly = Bake(Plot(type)), lazy = Bake(Plot(type));
                var fh = new FieldState(hourly, cat, 1);
                var fl = new FieldState(lazy, cat, 1);
                var climate = new CityClimateSource(s);
                int c = Cell(hourly, Vector2.zero);
                for (int h = 1; h <= 24 * 20; h++)
                {
                    fh.TickEnvironment(h, climate);
                    fl.TickEnvironment(h, climate);
                    hourly.Advance(c, h, s);
                }
                lazy.Advance(c, 24 * 20, s);
                float n = hourly.Get(c, SoilChannel.Nitrogen);
                Assert.AreEqual(n, lazy.Get(c, SoilChannel.Nitrogen), Mathf.Max(1f, n * 0.08f), $"{type}: nitrogen");
                Assert.AreEqual(hourly.Get(c, SoilChannel.Moisture), lazy.Get(c, SoilChannel.Moisture), 0.08f, $"{type}: moisture");
                Assert.AreEqual(hourly.Get(c, SoilChannel.OrganicMatter), lazy.Get(c, SoilChannel.OrganicMatter), 0.02f, $"{type}: organic matter");
            }
        }

        [Test]
        public void WaterWashesNitrogenOutOfSandFasterThanClay()
        {
            AgriSettings s = AgriSettings.Defaults();
            float Left(SoilType t)
            {
                SoilGrid g = Bake(Plot(t));
                int c = Cell(g, Vector2.zero);
                float n0 = g.Get(c, SoilChannel.Nitrogen);
                for (int day = 1; day <= 5; day++)
                {
                    g.Advance(c, day * 24.0 - 23.0, s);
                    g.AddWater(c, 40f, s.Water(WaterSourceKind.Well));
                    g.Advance(c, day * 24.0, s);
                }
                return g.Get(c, SoilChannel.Nitrogen) / n0;
            }
            float sand = Left(SoilType.Sandy), clay = Left(SoilType.Clay);
            Assert.Less(sand, 0.5f);
            Assert.Less(sand, clay);
        }

        [Test]
        public void LimestoneRaisesThePhOverDays()
        {
            AgriCatalog cat = Catalog();
            AgriSettings s = cat.Settings;
            SoilLayout l = Plot(SoilType.Clay);
            l.baseSoil.ph = 5f;
            SoilGrid g = Bake(l);
            int c = Cell(g, Vector2.zero);
            g.Apply(c, cat.AmendmentIndex("limestone"), cat.Amendment("limestone"), 4f, true);
            float first = g.Get(c, SoilChannel.Ph);
            for (int day = 1; day <= 10; day++)
            {
                g.Advance(c, day * 24.0 - 12.0, s);
                g.AddWater(c, 5f, s.Water(WaterSourceKind.Well));
                g.Advance(c, day * 24.0, s);
            }
            Assert.Less(first, 5.2f, "lime works slowly, not at once");
            Assert.Greater(g.Get(c, SoilChannel.Ph), 5.2f);
        }

        [Test]
        public void FallowSoilKeepsItsFertilityWithinReason()
        {
            AgriCatalog cat = Catalog();
            AgriSettings s = cat.Settings;
            float After(SoilType t)
            {
                SoilGrid g = Bake(Plot(t, 2f, 2f));
                var f = new FieldState(g, cat, 1);
                var climate = new CityClimateSource(s);
                int c = Cell(g, Vector2.zero);
                int years = 3 * 4 * s.daysPerSeason * 24;
                for (int h = 1; h <= years; h++) f.TickEnvironment(h, climate);
                g.Advance(c, years, s);
                return g.Get(c, SoilChannel.Nitrogen);
            }
            float rich = After(SoilType.OrganicRich), sand = After(SoilType.Sandy);
            Assert.Greater(rich, 15f, "rotting organic matter keeps feeding a rich soil");
            Assert.Less(rich, 120f, "...without piling up");
            Assert.Less(sand, rich);
        }

        // ================================================================== time and weather
        [Test]
        public void TheCalendarCountsSeasonsYearsAndTheMoon()
        {
            AgriSettings s = AgriSettings.Defaults();
            int dps = s.daysPerSeason;
            var first = new AgriDate(1.0, s);
            Assert.AreEqual(Season.Spring, first.Season);
            Assert.AreEqual(0, first.DayOfSeason);
            Assert.AreEqual(1, first.Year);
            Assert.AreEqual(Season.Summer, new AgriDate(dps * 24.0 + 1.0, s).Season);
            Assert.AreEqual(Season.Winter, new AgriDate(3 * dps * 24.0 + 1.0, s).Season);
            var nextYear = new AgriDate(4 * dps * 24.0 + 1.0, s);
            Assert.AreEqual(Season.Spring, nextYear.Season);
            Assert.AreEqual(2, nextYear.Year);
            Assert.AreEqual(first.Moon, new AgriDate(s.lunarCycleDays * 24.0 + 1.0, s).Moon, "the moon comes round every cycle");
            float summer = new AgriDate((dps + dps / 2) * 24.0, s).DayLength, winter = new AgriDate((3 * dps + dps / 2) * 24.0, s).DayLength;
            Assert.Greater(summer, 14f);
            Assert.Less(winter, 10f);
        }

        [Test]
        public void TheCityClimateIsTheSameEverywhereAndFollowsTheSeasons()
        {
            AgriSettings s = AgriSettings.Defaults();
            Assert.AreEqual(AgriEnvironment.Climate(s, 1234.5).temperature, AgriEnvironment.Climate(s, 1234.5).temperature, 1e-6f,
                "every machine reads the same weather from the same time");
            float Mean(Season season)
            {
                double start = (int)season * s.daysPerSeason * 24.0;
                float sum = 0f;
                int n = 0;
                for (double t = start; t < start + s.daysPerSeason * 24.0; t += 3.0, n++) sum += AgriEnvironment.Climate(s, t).temperature;
                return sum / n;
            }
            Assert.Greater(Mean(Season.Summer) - Mean(Season.Winter), 10f);
        }

        // ================================================================== seeds and genes
        [Test]
        public void SeedCodesCarryEveryGene()
        {
            var rng = new AgriRandom(7);
            for (int k = 0; k < 200; k++)
            {
                var levels = new int[PlantGenes.TraitCount];
                for (int t = 0; t < levels.Length; t++) levels[t] = Mathf.FloorToInt(rng.Range(-2f, 2.999f));
                PlantGenes g = PlantGenes.FromLevels(levels, rng.Chance(0.2f), Mathf.FloorToInt(rng.Range(0f, 3.999f)));
                int code = g.ToCode();
                Assert.GreaterOrEqual(code, 2, "codes 0 and 1 mean a wild seed");
                PlantGenes back = PlantGenes.FromCode(code);
                Assert.IsTrue(back.SameAs(g));
                Assert.AreEqual(g.rare, back.rare);
                for (int t = 0; t < levels.Length; t++) Assert.AreEqual(levels[t], back.Level((GeneTrait)t));
            }
            Assert.IsTrue(PlantGenes.FromCode(0).SameAs(PlantGenes.Wild));
            Assert.IsTrue(PlantGenes.FromCode(1).SameAs(PlantGenes.Wild));
        }

        [Test]
        public void CrossedSeedsStayWithinTheirKind()
        {
            var rng = new AgriRandom(11);
            PlantGenes mother = PlantGenes.Wild, father = PlantGenes.Wild;
            father.SetLevel(GeneTrait.FruitQuality, 2);
            int better = 0;
            for (int k = 0; k < 400; k++)
            {
                PlantGenes child = PlantGenes.Cross(mother, father, ref rng, 0.1f, 0.01f, 1f, 0.5f, 2);
                for (int t = 0; t < PlantGenes.TraitCount; t++)
                {
                    int level = child.Level((GeneTrait)t);
                    Assert.GreaterOrEqual(level, PlantGenes.MinLevel);
                    Assert.LessOrEqual(level, PlantGenes.MaxLevel);
                }
                if (child.Level(GeneTrait.FruitQuality) > 0) better++;
                Assert.IsTrue(PlantGenes.FromCode(child.ToCode()).SameAs(child));
            }
            Assert.Greater(better, 100, "a fine father passes his fruit on to many of his seeds");
        }

        // ================================================================== growing
        [Test]
        public void EveryBuiltInSpeciesGivesWhenTended()
        {
            AgriCatalog cat = Catalog();
            foreach (PlantSpeciesData sp in cat.AllSpecies)
            {
                AgriTrial.Options o = AgriTrial.Options.For(sp, cat.Settings);
                o.feed = true;
                o.protect = true;
                AgriTrial.Report r = AgriTrial.Grow(sp, cat, o);
                Assert.Greater(r.cameUp, 0, $"{sp.id}: nothing came up\n{r}");
                Assert.Greater(r.ripened, 0, $"{sp.id}: nothing ripened\n{r}");
            }
        }

        [Test]
        public void PerennialsOutliveAYearWithoutCare()
        {
            AgriCatalog cat = Catalog();
            foreach (PlantSpeciesData sp in cat.AllSpecies.Where(x => x.Perennial))
            {
                AgriTrial.Report r = AgriTrial.Grow(sp, cat, AgriTrial.Options.For(sp, cat.Settings));
                Assert.Greater(r.alive, 0, $"{sp.id}: no plant outlived the trial with only water\n{r}");
            }
        }

        [Test]
        public void TheSameTrialGrowsTheSameWay()
        {
            AgriCatalog cat = Catalog();
            PlantSpeciesData tomato = cat.Species("tomato");
            string a = AgriTrial.Grow(tomato, cat).ToString(), b = AgriTrial.Grow(tomato, cat).ToString();
            Assert.AreEqual(a, b);
        }

        [Test]
        public void RichSoilGrowsBetterThanPoorSoil()
        {
            AgriCatalog cat = Catalog();
            PlantSpeciesData cabbage = cat.Species("cabbage");
            AgriTrial.Options rich = AgriTrial.Options.For(cabbage, cat.Settings), poor = AgriTrial.Options.For(cabbage, cat.Settings);
            rich.soil = SoilType.OrganicRich;
            poor.soil = SoilType.Rocky;
            rich.protect = poor.protect = true;
            AgriTrial.Report r = AgriTrial.Grow(cabbage, cat, rich), p = AgriTrial.Grow(cabbage, cat, poor);
            Assert.Greater(r.growthSum / Mathf.Max(1f, r.growingHours), p.growthSum / Mathf.Max(1f, p.growingHours));
        }

        // ================================================================== what the player is told
        private static FarmWorker Worker()
        {
            var w = new FarmWorker { key = 1, name = "Tester" };
            foreach (InstrumentPreset ip in AgriDefaults.Instruments()) w.instruments.Add(new InstrumentProfile { id = ip.id, kind = ip.kind, quality = ip.quality });
            return w;
        }

        [Test]
        public void ACrudeProbeGivesWordsAPreciseOneGivesNumbers()
        {
            AgriCatalog cat = Catalog();
            SoilGrid g = Bake(Plot(SoilType.Sandy), 300.0);
            var f = new FieldState(g, cat, 3);
            var climate = new CityClimateSource(cat.Settings);
            f.TickEnvironment(300.0, climate);
            var spot = new Vector2(0.1f, 0.1f);
            FarmResult crude = FarmActions.Perform(f, new FarmRequest { kind = FarmActionKind.Measure, spot = spot, item = "ph_strip" }, Worker(), 300.0, climate);
            FarmResult precise = FarmActions.Perform(f, new FarmRequest { kind = FarmActionKind.Measure, spot = spot, item = "ph_probe" }, Worker(), 300.0, climate);
            Assert.IsTrue(crude.ok && precise.ok);
            Assert.IsFalse(crude.lines.Any(l => Regex.IsMatch(l, "[0-9]")), "a crude probe tells sour from sweet, no more: " + string.Join(" | ", crude.lines));
            Match m = Regex.Match(string.Join(" ", precise.lines), @"pH ([0-9]+\.[0-9])");
            Assert.IsTrue(m.Success, string.Join(" | ", precise.lines));
            Assert.AreEqual(At(g, spot, SoilChannel.Ph), float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 0.4f);
            FarmResult again = FarmActions.Perform(f, new FarmRequest { kind = FarmActionKind.Measure, spot = spot, item = "ph_probe" }, Worker(), 300.0, climate);
            Assert.AreEqual(string.Join("|", precise.lines), string.Join("|", again.lines), "the same spot at the same time reads the same");
        }

        private static readonly Regex Causes = new Regex(
            @"nitrogen|phosph|potass|calcium|magnes|sulfur|sulphur|\biron\b|\bzinc\b|boron|deficien|fertili|\blime\b|\bacid|alkal|\bph\b|\bneeds?\b|\blacks?\b|\blacking\b|too much|too little|should|\badd\b|because|at their best",
            RegexOptions.IgnoreCase);

        [Test]
        public void LookingAtPlantsTellsSymptomsNeverCauses()
        {
            AgriCatalog cat = Catalog();
            AgriSettings s = cat.Settings;
            var seen = new HashSet<string>();
            foreach (string id in new[] { "tomato", "cabbage", "apple", "nightshade", "strawberry", "wheat" })
            foreach (SoilType soil in new[] { SoilType.Sandy, SoilType.Peat, SoilType.Swamp })
            {
                PlantSpeciesData sp = cat.Species(id);
                SoilLayout l = Plot(soil);
                l.baseSoil.tilth = 0.8f;
                AgriTrial.Options o = AgriTrial.Options.For(sp, s);
                double start = 24.0 * (((int)o.startSeason - (int)s.startSeason + 4) % 4 * s.daysPerSeason + o.startDay) + 8.0;
                SoilGrid g = Bake(l, start);
                var f = new FieldState(g, cat, 5);
                var climate = new CityClimateSource(s);
                f.TickEnvironment(start, climate);
                PlantState p = f.Add(new PlantState
                {
                    species = id, position = Vector2.zero, depthCm = sp.depthCm, covered = !sp.surfaceSown, planted = start, last = start,
                    genes = PlantGenes.Wild, viability = 1f,
                });
                var sc = new PlantScratch();
                for (int h = 1; h <= 24 * 30; h++)
                {
                    double now = start + h;
                    f.TickEnvironment(now, climate);
                    PlantSim.Advance(f, p, now, sc, climate);
                    if (h % 72 == 0)
                        foreach (string line in PlantObservation.Plant(p, now, s, 0f, false, 0f, true)) seen.Add(line);
                }
                foreach (string line in PlantObservation.Soil(g, Cell(g, Vector2.zero), cat)) seen.Add(line);
            }
            Assert.Greater(seen.Count, 20);
            foreach (string line in seen)
            {
                Assert.IsFalse(Causes.IsMatch(line), $"'{line}' tells a cause or a remedy");
                bool age = Regex.IsMatch(line, @"sown .*ago\.$");
                bool count = Regex.IsMatch(line, @"^On it: ");
                if (!age && !count) Assert.IsFalse(Regex.IsMatch(line, "[0-9]"), $"'{line}' gives a number");
            }
        }

        // ================================================================== items and saving
        [Test]
        public void ProduceAndWaterCodesRoundTrip()
        {
            foreach (float q in new[] { 0f, 0.25f, 0.5f, 0.9f, 1f })
            foreach (bool rare in new[] { false, true })
            {
                int code = AgriItemCode.Produce(q, 0.4f, ProduceStage.Ripe, rare);
                Assert.GreaterOrEqual(code, 2);
                Assert.IsTrue(AgriItemCode.TryProduce(code, out float quality, out float potency, out ProduceStage stage, out bool r));
                Assert.AreEqual(q, quality, 0.02f);
                Assert.AreEqual(0.4f, potency, 0.04f);
                Assert.AreEqual(ProduceStage.Ripe, stage);
                Assert.AreEqual(rare, r);
            }
            int w = AgriItemCode.Water(7.5f, WaterSourceKind.Spring);
            Assert.IsTrue(AgriItemCode.TryWater(w, out float litres, out WaterSourceKind kind));
            Assert.AreEqual(7.5f, litres, 0.15f);
            Assert.AreEqual(WaterSourceKind.Spring, kind);
            Assert.IsFalse(AgriItemCode.TryProduce(0, out _, out _, out _, out _), "a copy without a code is unknown, not perfect");
        }

        [Test]
        public void AgriculturalIdsReadBack()
        {
            Assert.IsTrue(AgriIds.Parse(AgriIds.Seed("tomato"), out string kind, out string key));
            Assert.AreEqual("seed", kind);
            Assert.AreEqual("tomato", key);
            Assert.IsTrue(AgriIds.Parse(AgriIds.Amendment("bone_meal"), out kind, out key));
            Assert.AreEqual("amend", kind);
            Assert.AreEqual("bone_meal", key);
            Assert.IsFalse(AgriIds.Parse("sword", out _, out _));
        }

        [Test]
        public void ASoilGridSavesAndLoadsWhole()
        {
            AgriCatalog cat = Catalog();
            SoilLayout l = Plot(SoilType.OrganicRich);
            SoilGrid g = Bake(l);
            int c = Cell(g, Vector2.zero);
            g.Apply(c, cat.AmendmentIndex("compost"), cat.Amendment("compost"), 2f, false);
            g.AddWater(c, 8f, cat.Settings.Water(WaterSourceKind.Rain));
            // Under glass (what a greenhouse gave it): kept with the soil, so a garden caught up from its save still lives under it.
            g.CoverRain[c] = 0f;
            g.CoverEvap[c] = 0.4f;
            g.CoverLight[c] = 0.8f;
            g.CoverWarmth[c] = 6f;
            g.CoverFrost[c] = 0.7f;
            int open = Cell(g, new Vector2(1.5f, 1.5f));
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms)) g.Write(w);
                bytes = ms.ToArray();
            }
            SoilGrid back = l.CreateGrid();
            using (var r = new BinaryReader(new MemoryStream(bytes))) Assert.IsTrue(back.Read(r));
            for (int ch = 0; ch < SoilGrid.Stride; ch++) Assert.AreEqual(g.Get(c, (SoilChannel)ch), back.Get(c, (SoilChannel)ch), 1e-5f, ((SoilChannel)ch).ToString());
            Assert.AreEqual(g.PendingTop(c, out float a), back.PendingTop(c, out float b));
            Assert.AreEqual(a, b, 1e-5f);
            Assert.AreEqual(0f, back.CoverRain[c], "no rain under the glass");
            Assert.AreEqual(0.4f, back.CoverEvap[c], 1e-6f);
            Assert.AreEqual(0.8f, back.CoverLight[c], 1e-6f);
            Assert.AreEqual(6f, back.CoverWarmth[c], 1e-6f, "its warmth");
            Assert.AreEqual(0.7f, back.CoverFrost[c], 1e-6f, "its frost protection");
            Assert.IsTrue(back.Open(open), "a cell nothing covers stays open to the sky");
            SoilGrid other = Plot(SoilType.OrganicRich, 6f, 6f).CreateGrid();
            using (var r = new BinaryReader(new MemoryStream(bytes))) Assert.IsFalse(other.Read(r), "a save of another shape is refused");
        }

        // ------------------------------------------------------------------ sleeping gardens
        /// <summary>A sown plot of a species (as the trial plot sows it) with its weather started.</summary>
        private static FieldState Sown(AgriCatalog cat, string species, int count, double start, int seed = 4242)
        {
            PlantSpeciesData sp = cat.Species(species);
            float gap = Mathf.Max(0.5f, sp.spacing);
            SoilLayout layout = Plot(SoilType.OrganicRich, Mathf.Max(6f, count * gap + 2f), Mathf.Max(4f, gap * 2f + 2f));
            layout.baseSoil.tilth = 0.8f;
            layout.baseSoil.ph = (sp.phLow + sp.phHigh) * 0.5f;
            SoilGrid g = layout.CreateGrid();
            layout.Bake(g, start);
            var f = new FieldState(g, cat, seed);
            f.TickEnvironment(start, new CityClimateSource(cat.Settings));
            var date = new AgriDate(start, cat.Settings);
            for (int i = 0; i < count; i++)
                f.Add(new PlantState
                {
                    species = sp.id, position = new Vector2(-(count - 1) * gap * 0.5f + i * gap, 0f), depthCm = sp.depthCm, covered = !sp.surfaceSown,
                    planted = start, last = start, genes = PlantGenes.Wild, viability = 0.95f, plantedMoon = date.Moon, lastSeason = date.Season,
                });
            return f;
        }

        private static (float nitrogen, float moisture, float potassium) SoilAverage(FieldState f, AgriSettings s)
        {
            double n = 0, m = 0, k = 0;
            int cells = 0;
            for (int i = 0; i < f.Grid.Count; i++)
            {
                if (!f.Grid.Inside[i]) continue;
                f.Grid.Advance(i, f.EnvTime, s);
                n += f.Grid.Get(i, SoilChannel.Nitrogen);
                m += f.Grid.Get(i, SoilChannel.Moisture);
                k += f.Grid.Get(i, SoilChannel.Potassium);
                cells++;
            }
            return ((float)(n / cells), (float)(m / cells), (float)(k / cells));
        }

        [Test]
        public void AGardenAsleepForWeeksWakesAsIfWatchedHourByHour()
        {
            AgriCatalog cat = Catalog();
            AgriSettings s = cat.Settings;
            double start = 8.0 + 24.0 * 2.0;
            foreach ((string species, int days) in new[] { ("lavender", 45), ("carrot", 20), ("wheat", 20) })
            {
                double end = start + days * 24.0;
                // Plants come up and flower by chance: several gardens of each, compared together.
                var wa = new List<PlantState>();
                var sa = new List<PlantState>();
                foreach (int seed in new[] { 4242, 1717, 9001 })
                {
                    (FieldState watched, FieldState slept) = WatchedAndSlept(cat, species, 10, start, end, seed);
                    Assert.AreEqual(end, slept.EnvTime, 1e-6, $"{species}: its weather reached the present");
                    foreach (PlantState p in slept.Plants) Assert.AreEqual(end, p.last, 1e-6, $"{species}: every plant reached the present");
                    var a = SoilAverage(watched, s);
                    var b = SoilAverage(slept, s);
                    Assert.AreEqual(a.nitrogen, b.nitrogen, Mathf.Max(1f, a.nitrogen * 0.05f), $"{species}: nitrogen");
                    Assert.AreEqual(a.potassium, b.potassium, Mathf.Max(1f, a.potassium * 0.05f), $"{species}: potassium");
                    Assert.AreEqual(a.moisture, b.moisture, 0.03f, $"{species}: moisture");
                    wa.AddRange(watched.Plants.Where(p => p.Alive));
                    sa.AddRange(slept.Plants.Where(p => p.Alive));
                }
                Assert.AreEqual(wa.Count, sa.Count, 8, $"{species}: about as many alive (seeds come up by chance)");
                if (wa.Count > 0 && sa.Count > 0)
                {
                    Assert.AreEqual(wa.Average(p => p.size), sa.Average(p => p.size), Mathf.Max(0.05f, wa.Average(p => p.size) * 0.1f), $"{species}: size");
                    Assert.AreEqual(wa.Average(p => (int)p.stage), sa.Average(p => (int)p.stage), 0.75, $"{species}: stage");
                    Assert.AreEqual(wa.Average(p => p.health), sa.Average(p => p.health), 0.15, $"{species}: health");
                }
            }
        }

        /// <summary>
        /// The same garden watched hour by hour (the weather and every plant, the trial plot's way) and asleep the whole
        /// time, then woken (brought up to date in one go).
        /// </summary>
        private static (FieldState watched, FieldState slept) WatchedAndSlept(AgriCatalog cat, string species, int count, double start, double end, int seed)
        {
            AgriSettings s = cat.Settings;
            FieldState watched = Sown(cat, species, count, start, seed);
            var climate = new CityClimateSource(s);
            var scratch = new PlantScratch();
            for (double t = start + 1.0; t <= end + 1e-9; t += 1.0)
            {
                watched.TickEnvironment(t, climate);
                foreach (PlantState p in watched.Plants.ToArray()) PlantSim.Advance(watched, p, t, scratch, climate);
                watched.SweepDead(s.deadPlantDays * 24f);
            }
            FieldState slept = Sown(cat, species, count, start, seed);
            Assert.IsTrue(new FieldCatchUp(slept, climate, null, end).Run(), $"{species}: done");
            return (watched, slept);
        }

        [Test]
        public void AGardenAsleepRipensItsProduceAsIfWatched()
        {
            // Flowers open by how far the flowering has come, not one a step: an hour-by-hour garden and one caught up in
            // longer steps make as many pieces, and they ripen, overripen and rot alike.
            AgriCatalog cat = Catalog();
            int summer = cat.Settings.daysPerSeason;
            foreach ((string species, int sowDay, int days) in new[] { ("tomato", summer, 20), ("chamomile", 2, 16) })
            {
                double start = 8.0 + 24.0 * sowDay, end = start + days * 24.0;
                int[] w = new int[3], z = new int[3];
                foreach (int seed in new[] { 4242, 12161, 20080, 27999 })
                {
                    (FieldState watched, FieldState slept) = WatchedAndSlept(cat, species, 10, start, end, seed);
                    Pieces(watched, w);
                    Pieces(slept, z);
                }
                Assert.Greater(w[1], 10, $"{species}: the watched gardens have ripe produce to compare");
                string[] what = { "pieces made", "ripe pieces", "overripe or rotten pieces" };
                for (int k = 0; k < 3; k++)
                    Assert.AreEqual(w[k], z[k], Mathf.Max(6f, w[k] * 0.2f), $"{species}: {what[k]}");
            }
        }

        /// <summary>Adds up a garden's produce: pieces made so far, pieces ripe (mature, not yet overripe), pieces overripe or rotten.</summary>
        private static void Pieces(FieldState f, int[] into)
        {
            foreach (PlantState p in f.Plants)
            {
                into[0] += Mathf.Max(0, p.nextFruitId - 1);
                foreach (FruitState fr in p.fruits)
                {
                    if (fr.stage >= ProduceStage.Overripe) into[2]++;
                    else if (fr.stage >= ProduceStage.Mature) into[1]++;
                }
            }
        }

        [Test]
        public void ACatchUpSpreadOverFramesEndsTheSameAsAllAtOnce()
        {
            AgriCatalog cat = Catalog();
            double start = 8.0 + 24.0 * 2.0, end = start + 24.0 * 10.0;
            var climate = new CityClimateSource(cat.Settings);
            FieldState once = Sown(cat, "carrot", 6, start), spread = Sown(cat, "carrot", 6, start);
            Assert.IsTrue(new FieldCatchUp(once, climate, null, end).Run());
            var job = new FieldCatchUp(spread, climate, null, end);
            int slices = 0, steps = 0;
            while (!job.Run(() => ++steps % 5 == 0)) slices++;
            Assert.Greater(slices, 10, "it stopped and went on many times");
            Assert.AreEqual(once.EnvTime, spread.EnvTime, 1e-9);
            Assert.AreEqual(once.Plants.Count, spread.Plants.Count);
            for (int i = 0; i < once.Plants.Count; i++)
            {
                Assert.AreEqual(once.Plants[i].stage, spread.Plants[i].stage);
                Assert.AreEqual(once.Plants[i].size, spread.Plants[i].size, 1e-5f);
                Assert.AreEqual(once.Plants[i].health, spread.Plants[i].health, 1e-5f);
            }
        }

        [Test]
        public void ACatchUpFollowsTheSeasons()
        {
            AgriCatalog cat = Catalog();
            AgriSettings s = cat.Settings;
            double start = 8.0 + 24.0 * 2.0;
            double end = start + 24.0 * (s.YearDays + 4); // a whole year asleep
            FieldState f = Sown(cat, "lavender", 4, start);
            var job = new FieldCatchUp(f, new CityClimateSource(s), null, end);
            Assert.IsTrue(job.Run());
            Assert.AreEqual(new AgriDate(end, s).Season, new AgriDate(f.EnvTime, s).Season);
            Assert.GreaterOrEqual(job.WeatherSteps, (int)((end - start) / s.maxStepHours) - 1, "the weather of every step");
            PlantState anyAlive = f.Plants.FirstOrDefault(p => p.Alive);
            Assert.IsNotNull(anyAlive, "a perennial lives through a year of rain alone");
            Assert.GreaterOrEqual(anyAlive.years, 1, "and counts the year");
        }

        // ------------------------------------------------------------------ catching up lightly
        /// <summary>The built-in content with the catch-up's shortcuts on or off (each is a switch in the settings).</summary>
        private static AgriCatalog Shortcuts(bool bare, bool quiet, bool cells)
        {
            AgriCatalog cat = Catalog();
            cat.Settings.fastForwardBareGardens = bare;
            cat.Settings.quietSteps = quiet;
            cat.Settings.cacheCellValues = cells;
            return cat;
        }

        /// <summary>A 10 x 7 m plot sown in rows, one species a row (a row goes on below when it is full).</summary>
        private static FieldState Rows(AgriCatalog cat, double start, int seed, params (string species, int count)[] rows)
        {
            SoilLayout layout = Plot(SoilType.OrganicRich, 10f, 7f);
            layout.baseSoil.tilth = 0.8f;
            var f = new FieldState(Bake(layout, start), cat, seed);
            f.TickEnvironment(start, new CityClimateSource(cat.Settings));
            var date = new AgriDate(start, cat.Settings);
            float x = -4.6f, y = -3.2f, rowGap = 0f;
            foreach ((string species, int count) in rows)
            {
                PlantSpeciesData sp = cat.Species(species);
                float gap = Mathf.Max(0.5f, sp.spacing);
                x = -4.6f;
                y += rowGap * 0.5f + gap * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    if (x > 4.6f)
                    {
                        x = -4.6f;
                        y += gap;
                    }
                    Assert.IsTrue(f.Grid.TryIndex(new Vector2(x, y), out _), $"{species} {i} is on the plot");
                    f.Add(new PlantState
                    {
                        species = sp.id, position = new Vector2(x, y), depthCm = sp.depthCm, covered = !sp.surfaceSown, planted = start, last = start,
                        genes = PlantGenes.Wild, viability = 0.95f, plantedMoon = date.Moon, lastSeason = date.Season,
                    });
                    x += gap;
                }
                rowGap = gap;
            }
            return f;
        }

        /// <summary>
        /// The same garden caught up from <paramref name="from"/> to <paramref name="to"/> with the shortcuts of
        /// <paramref name="on"/>, after living until <paramref name="from"/> without them.
        /// </summary>
        private static (FieldState field, FieldCatchUp job) CaughtUp(AgriCatalog on, double sown, double from, double to, int seed, params (string, int)[] rows)
        {
            AgriSettings s = on.Settings;
            bool bare = s.fastForwardBareGardens, quiet = s.quietSteps, cells = s.cacheCellValues;
            s.fastForwardBareGardens = s.quietSteps = s.cacheCellValues = false;
            FieldState f = Rows(on, sown, seed, rows);
            if (from > sown) Assert.IsTrue(new FieldCatchUp(f, new CityClimateSource(s), null, from).Run());
            s.fastForwardBareGardens = bare;
            s.quietSteps = quiet;
            s.cacheCellValues = cells;
            var job = new FieldCatchUp(f, new CityClimateSource(s), null, to);
            Assert.IsTrue(job.Run());
            f.Grid.AdvanceAll(f.EnvTime, s);
            return (f, job);
        }

        /// <summary>The soil's averages over the plot (every cell brought to the end first): moisture, N, P, K, organic matter, pH.</summary>
        private static float[] SoilTotals(FieldState f)
        {
            var channels = new[] { SoilChannel.Moisture, SoilChannel.Nitrogen, SoilChannel.Phosphorus, SoilChannel.Potassium, SoilChannel.OrganicMatter, SoilChannel.Ph };
            var sums = new double[channels.Length];
            int cells = 0;
            for (int i = 0; i < f.Grid.Count; i++)
            {
                if (!f.Grid.Inside[i]) continue;
                cells++;
                for (int c = 0; c < channels.Length; c++) sums[c] += f.Grid.Get(i, channels[c]);
            }
            return sums.Select(v => (float)(v / cells)).ToArray();
        }

        [Test]
        public void ABareGardenCatchesUpInLongStepsToTheSameSoil()
        {
            // Nothing living on it: the soil is lazy, so long steps replay the same weather sample by sample - and with
            // dead plants waiting to be gone, they go at the same time.
            double start = 8.0 + 24.0 * 2.0, end = start + 24.0 * 40.0;
            foreach (bool withDead in new[] { false, true })
            {
                var r = new FieldState[2];
                var jobs = new FieldCatchUp[2];
                for (int k = 0; k < 2; k++)
                {
                    AgriCatalog cat = Shortcuts(k == 1, false, false);
                    FieldState f = Rows(cat, start, 99, ("tomato", withDead ? 4 : 0));
                    foreach (PlantState p in f.Plants) PlantSim.Die(p, false);
                    jobs[k] = new FieldCatchUp(f, new CityClimateSource(cat.Settings), null, end);
                    Assert.IsTrue(jobs[k].Run());
                    r[k] = f;
                }
                Assert.Less(jobs[1].WeatherSteps * 10, jobs[0].WeatherSteps, "far fewer steps");
                Assert.Greater(jobs[1].BareHours, 24.0 * 39.0, "nearly all of it in long steps");
                Assert.AreEqual(r[0].Grid.RainTotal, r[1].Grid.RainTotal, 1e-9, "the same rain");
                Assert.AreEqual(r[0].Grid.EvapTotal, r[1].Grid.EvapTotal, 1e-9, "the same drying");
                Assert.AreEqual(r[0].Grid.WarmTotal, r[1].Grid.WarmTotal, 1e-9, "the same warmth");
                Assert.AreEqual(r[0].SoilTemperature, r[1].SoilTemperature, 1e-6f);
                Assert.AreEqual(r[0].EnvTime, r[1].EnvTime, 1e-9);
                Assert.AreEqual(r[0].Plants.Count, r[1].Plants.Count, "the dead are gone alike");
                Assert.AreEqual(0, r[1].Plants.Count, "and gone by now");
                for (int k = 0; k < 2; k++) r[k].Grid.AdvanceAll(end, r[k].Settings);
                float[] a = SoilTotals(r[0]), b = SoilTotals(r[1]);
                for (int c = 0; c < a.Length; c++) Assert.AreEqual(a[c], b[c], Mathf.Abs(a[c]) * 1e-5f + 1e-6f, $"soil {c}");
            }
        }

        [Test]
        public void CachedCellValuesChangeNothing()
        {
            // What a cell can feed at its pH and its water figures, kept until they change: the garden grows exactly the
            // same, to the last digit.
            double start = 8.0 + 24.0 * 14.0, end = start + 24.0 * 12.0;
            FieldState a = CaughtUp(Shortcuts(false, false, false), start, start, end, 31, ("tomato", 8), ("bean", 8), ("cabbage", 6)).field;
            FieldState b = CaughtUp(Shortcuts(false, false, true), start, start, end, 31, ("tomato", 8), ("bean", 8), ("cabbage", 6)).field;
            CollectionAssert.AreEqual(SoilTotals(a), SoilTotals(b), "the soil");
            Assert.AreEqual(a.Plants.Count, b.Plants.Count);
            for (int i = 0; i < a.Plants.Count; i++)
            {
                PlantState p = a.Plants[i], q = b.Plants[i];
                Assert.AreEqual(p.stage, q.stage);
                Assert.AreEqual(p.health, q.health);
                Assert.AreEqual(p.size, q.size);
                Assert.AreEqual(p.Quality, q.Quality);
                Assert.AreEqual(p.nextFruitId, q.nextFruitId);
            }
        }

        [Test]
        public void QuietStepsEndAsTheEveryStepLook()
        {
            // In a quiet stretch a plant goes on with what it last saw of its soil and neighbours; the weather - frost,
            // warmth, ripening - still comes every step. Through a winter's rest, a summer crop, a frost spell over ripe
            // fruit and a year of perennials, it ends as the garden that looked every step: the same stages and produce,
            // the same plants alive, and its numbers within noise.
            int season = Catalog().Settings.daysPerSeason;
            double day = 24.0;
            var gardens = new (string name, double sown, double from, double to, (string, int)[] rows)[]
            {
                ("resting through winter", 8.0 + day * 2, 8.0 + day * (3 * season + 1), 8.0 + day * (4 * season + 4), new[] { ("apple", 3), ("grape", 3), ("strawberry", 8), ("lavender", 8) }),
                ("a summer crop", 8.0 + day * season, 8.0 + day * season, 8.0 + day * (season + 20), new[] { ("tomato", 10), ("bean", 10), ("carrot", 10) }),
                ("frost over ripe fruit", 8.0 + day * (season + 4), 8.0 + day * (season + 4), 8.0 + day * (3 * season + 6), new[] { ("tomato", 10), ("cabbage", 8), ("potato", 6) }),
                ("perennials over a year", 8.0 + day * 2, 8.0 + day * 2, 8.0 + day * (4 * season + 6), new[] { ("lavender", 8), ("comfrey", 4), ("strawberry", 8), ("apple", 2) }),
            };
            int quietSteps = 0;
            foreach (var g in gardens)
                foreach (int seed in new[] { 4242, 1717 })
                {
                    (FieldState a, FieldCatchUp ja) = CaughtUp(Shortcuts(false, false, false), g.sown, g.from, g.to, seed, g.rows);
                    (FieldState b, FieldCatchUp jb) = CaughtUp(Shortcuts(false, true, true), g.sown, g.from, g.to, seed, g.rows);
                    string what = $"{g.name} ({seed})";
                    Assert.AreEqual(ja.PlantSteps, jb.PlantSteps, $"{what}: the same steps");
                    quietSteps += jb.QuietPlantSteps;
                    float[] sa = SoilTotals(a), sb = SoilTotals(b);
                    for (int c = 0; c < sa.Length; c++) Assert.AreEqual(sa[c], sb[c], Mathf.Abs(sa[c]) * 0.001f + 1e-4f, $"{what}: soil {c}");
                    Assert.AreEqual(a.Plants.Count, b.Plants.Count, $"{what}: the same plants");
                    for (int i = 0; i < a.Plants.Count; i++)
                    {
                        PlantState p = a.Plants[i], q = b.Plants[i];
                        string who = $"{what}: {p.species} {p.id}";
                        Assert.AreEqual(p.id, q.id, who);
                        Assert.AreEqual(p.stage, q.stage, $"{who}: stage");
                        Assert.AreEqual(p.dormant, q.dormant, $"{who}: rest");
                        Assert.AreEqual(p.years, q.years, $"{who}: years");
                        Assert.AreEqual(p.nextFruitId, q.nextFruitId, $"{who}: produce made");
                        Assert.AreEqual(p.fruits.Count, q.fruits.Count, $"{who}: produce on it");
                        for (int k = 0; k < p.fruits.Count; k++) Assert.AreEqual(p.fruits[k].stage, q.fruits[k].stage, $"{who}: produce {k} ripeness");
                        Assert.AreEqual(p.health, q.health, 0.005f, $"{who}: health");
                        Assert.AreEqual(p.size, q.size, 0.005f, $"{who}: size");
                        Assert.AreEqual(p.Quality, q.Quality, 0.005f, $"{who}: quality");
                        Assert.AreEqual(p.chill, q.chill, 0.01f, $"{who}: cold gathered");
                        Assert.AreEqual(p.Symptom(PlantSymptom.FrostBitten), q.Symptom(PlantSymptom.FrostBitten), 0.005f, $"{who}: frost");
                    }
                }
            Assert.Greater(quietSteps, 200, "quiet stretches were found");
        }
    }

    /// <summary>Edit Mode tests of the Soil Field component: what a network client is sent (a picture, never the hidden numbers).</summary>
    public class AgricultureFieldTests
    {
        private readonly List<GameObject> made = new List<GameObject>();

        [SetUp]
        public void SetUp() => AgricultureDatabase.Override(ScriptableObject.CreateInstance<AgricultureDatabase>());

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in made) if (go != null) Object.DestroyImmediate(go);
            made.Clear();
            AgricultureDatabase.Override(null);
        }

        private SoilField Field()
        {
            var go = new GameObject("Test Soil Field");
            made.Add(go);
            var f = go.AddComponent<SoilField>();
            f.layout = new SoilLayout { variation = 0f, cellSize = 0.25f };
            f.saveToDisk = false;
            return f;
        }

        [Test]
        public void AClientSeesThePlantsButNotTheSoilsChemistryOrTheGenes()
        {
            SoilField server = Field(), client = Field();
            SoilGrid g = server.State.Grid;
            Assert.IsTrue(g.TryIndex(Vector2.zero, out int c));
            float baked = g.Get(c, SoilChannel.Nitrogen);
            g.Set(c, SoilChannel.Nitrogen, baked + 50f);
            g.Set(c, SoilChannel.Moisture, 0.9f);
            PlantGenes genes = PlantGenes.Wild;
            genes.SetLevel(GeneTrait.FruitQuality, 2);
            server.State.Add(new PlantState { species = "tomato", position = Vector2.zero, stage = PlantStage.Vegetative, size = 0.5f, genes = genes, planted = 1.0, last = 1.0 });

            Assert.IsTrue(client.ImportVisual(server.ExportVisual()));
            Assert.IsTrue(client.Mirror);
            Assert.AreEqual(1, client.State.Plants.Count);
            PlantState seen = client.State.Plants[0];
            Assert.AreEqual("tomato", seen.species);
            Assert.AreEqual(PlantStage.Vegetative, seen.stage);
            Assert.AreEqual(0.5f, seen.size, 0.01f);
            Assert.AreEqual(0, seen.genes.Level(GeneTrait.FruitQuality), "genes never leave the server");
            Assert.AreEqual(0.9f, client.State.Grid.Get(c, SoilChannel.Moisture), 0.01f, "how wet it looks is sent");
            Assert.AreEqual(baked, client.State.Grid.Get(c, SoilChannel.Nitrogen), 1e-3f, "the chemistry is not");
        }

        [Test]
        public void APictureOfAnotherFieldIsRefused()
        {
            SoilField server = Field(), client = Field();
            client.layout.outline = new List<Vector2> { new Vector2(-6f, -6f), new Vector2(6f, -6f), new Vector2(6f, 6f), new Vector2(-6f, 6f) };
            Assert.IsFalse(client.ImportVisual(server.ExportVisual()));
            Assert.IsFalse(client.ImportVisual(new byte[] { 1, 2, 3 }));
        }

        private CombatEntity Player(Vector3 at)
        {
            var go = new GameObject("Test Player");
            made.Add(go);
            go.transform.position = at;
            return go.AddComponent<CombatEntity>();
        }

        [Test]
        public void AFieldAcceptsWorkOnlyWithinReach()
        {
            SoilField f = Field();
            CombatEntity player = Player(new Vector3(30f, 0f, 30f));
            FarmResult r = f.PerformHere(new FarmRequest { kind = FarmActionKind.Inspect, spot = Vector2.zero }, player);
            Assert.IsNotNull(r);
            Assert.IsFalse(r.ok, "a player across the city cannot work the field");
        }

        [Test]
        public void APlantNamedByIdMustBeWithinReachToo()
        {
            SoilField f = Field();
            PlantState far = f.State.Add(new PlantState { species = "tomato", position = new Vector2(3.5f, 2.5f), stage = PlantStage.Vegetative, size = 0.3f, planted = 1.0, last = 1.0 });
            PlantState near = f.State.Add(new PlantState { species = "tomato", position = new Vector2(-3.2f, -2.2f), stage = PlantStage.Vegetative, size = 0.3f, planted = 1.0, last = 1.0 });
            CombatEntity player = Player(new Vector3(-3.5f, 0f, -2.5f));
            var spot = new Vector2(-3f, -2f);
            FarmResult r = f.PerformHere(new FarmRequest { kind = FarmActionKind.Inspect, spot = spot, plant = far.id }, player);
            Assert.IsFalse(r.ok, "a spot at one's feet does not reach a plant across the field");
            r = f.PerformHere(new FarmRequest { kind = FarmActionKind.Inspect, spot = spot, plant = near.id, seq = 7 }, player);
            Assert.IsTrue(r.ok, r.message);
            Assert.AreEqual(7, r.seq, "an answer names the request it answers");
        }

        [Test]
        public void APictureMayCarryOnlyThePlants()
        {
            SoilField server = Field(), client = Field();
            Assert.IsTrue(server.State.Grid.TryIndex(Vector2.zero, out int c));
            float own = client.State.Grid.Get(c, SoilChannel.Moisture);
            server.State.Grid.Set(c, SoilChannel.Moisture, own > 0.5f ? 0.1f : 0.9f);
            server.State.Add(new PlantState { species = "bean", position = Vector2.zero, stage = PlantStage.Seedling, size = 0.1f, planted = 1.0, last = 1.0 });
            Assert.IsTrue(client.ImportVisual(server.ExportVisual(false, true)));
            Assert.AreEqual(1, client.State.Plants.Count);
            Assert.AreEqual(own, client.State.Grid.Get(c, SoilChannel.Moisture), 1e-4f, "a plants-only picture leaves the soil as it was");
            Assert.IsTrue(client.ImportVisual(server.ExportVisual(true, false)));
            Assert.AreEqual(server.State.Grid.Get(c, SoilChannel.Moisture), client.State.Grid.Get(c, SoilChannel.Moisture), 0.01f);
            Assert.AreEqual(1, client.State.Plants.Count, "a soil-only picture leaves the plants as they were");
        }

        [Test]
        public void ASaveIsWrittenWholeAndKeepsTheCopyBeforeIt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AgricultureSaveTest_" + System.Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "Fields", "garden.bin");
            try
            {
                FarmClock.WriteWhole(path, new byte[] { 1, 2, 3 });
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path), "the first save");
                FarmClock.WriteWhole(path, new byte[] { 4, 5 });
                CollectionAssert.AreEqual(new byte[] { 4, 5 }, File.ReadAllBytes(path), "the new save");
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path + ".bak"), "the copy before it is kept");
                Assert.IsFalse(File.Exists(path + ".tmp"), "nothing is left aside");
                CollectionAssert.AreEqual(new[] { path, path + ".tmp", path + ".bak" }, FarmClock.CopiesOf(path), "read newest first");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (System.Exception) { }
            }
        }

        [Test]
        public void AClientCopyIsDroppedWhenTheSessionEnds()
        {
            SoilField server = Field(), client = Field();
            server.State.Add(new PlantState { species = "bean", position = Vector2.zero, stage = PlantStage.Seedling, size = 0.1f, planted = 1.0, last = 1.0 });
            Assert.IsTrue(client.ImportVisual(server.ExportVisual()));
            Assert.IsTrue(client.Mirror);
            client.ClearMirror();
            Assert.IsFalse(client.Mirror);
            Assert.AreEqual(0, client.State.Plants.Count, "the field is its own again: not what the server showed");
        }
    }
}
#endif
