using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A test plot for designers (the species inspector, the tests): a few plants of a species sown in a chosen soil, grown
/// hour by hour in the city's climate and watered when dry, and what happened to them in numbers - when they came up,
/// flowered and ripened, how much they gave, how healthy they were. It is the same simulation the game runs; the
/// numbers are for whoever tunes the species, never for players.
/// </summary>
public static class AgriTrial
{
    public sealed class Options
    {
        public SoilType soil = SoilType.OrganicRich;
        /// <summary>Use the species' best pH instead of the soil type's own.</summary>
        public bool idealPh = true;
        public Season startSeason = Season.Spring;
        /// <summary>0-based day of the season to sow on.</summary>
        public int startDay = 1;
        public int days = 45;
        public int plants = 4;
        /// <summary>Water the plants whenever their soil gets dry (twice a day).</summary>
        public bool water = true;
        /// <summary>Keep every nutrient in reach of the roots at what the species needs (once a day).</summary>
        public bool feed;
        /// <summary>Clear pests and diseases (twice a day).</summary>
        public bool protect;
        public int seed = 7;

        /// <summary>
        /// A sensible trial of a species: its favourite soil, sown on the first day of its sowing seasons that is warm
        /// enough for it (the next days' average in the city's climate), long enough to give.
        /// </summary>
        public static Options For(PlantSpeciesData d, AgriSettings settings = null)
        {
            var o = new Options();
            if (d == null) return o;
            settings ??= AgriSettings.Defaults();
            // The soil it likes best that also feeds it (at its best pH) - a liking for peat is no use when peat starves it.
            float best = -1f, ph = (d.phLow + d.phHigh) * 0.5f;
            for (int t = 0; t < (int)SoilType.Magical + 1; t++)
            {
                SoilProfile p = SoilProfile.Preset((SoilType)t);
                float feeds = Mathf.Min(Mathf.Min(Feeds(p.nitrogen, Nutrient.Nitrogen, ph, d), Feeds(p.phosphorus, Nutrient.Phosphorus, ph, d)),
                    Mathf.Min(Feeds(p.potassium, Nutrient.Potassium, ph, d), Feeds(p.calcium, Nutrient.Calcium, ph, d)));
                float score = d.SoilAffinity((SoilType)t) * (0.35f + 0.65f * Mathf.Sqrt(feeds));
                if (score > best + 0.001f)
                {
                    best = score;
                    o.soil = (SoilType)t;
                }
            }
            bool found = false;
            int dps = Mathf.Max(1, settings.daysPerSeason);
            float needs = Mathf.Max(d.germinationMin + 3f, d.tempMin + 4f);
            for (int day = 0; day < dps * 4 && !found; day++)
            {
                var date = new AgriDate(day * 24.0 + 12.0, settings);
                if (!AgriDate.In(d.sowingSeasons, date.Season)) continue;
                float sum = 0f, coldest = float.MaxValue;
                for (int h = 0; h < 6 * 24; h += 3)
                {
                    float t = AgriEnvironment.Climate(settings, day * 24.0 + h).temperature;
                    sum += t;
                    coldest = Mathf.Min(coldest, t);
                }
                if (sum / 48f < needs || (d.frostSensitivity > 0.6f && coldest < 0.5f)) continue;
                o.startSeason = date.Season;
                o.startDay = date.DayOfSeason;
                found = true;
            }
            if (!found)
                for (int s = 0; s < 4; s++)
                    if (AgriDate.In(d.sowingSeasons, (Season)s))
                    {
                        o.startSeason = (Season)s;
                        o.startDay = 1;
                        break;
                    }
            o.days = d.Perennial ? 140 : 50;
            return o;
        }
    }

    private static float Feeds(float ppm, Nutrient n, float ph, PlantSpeciesData d) =>
        Mathf.Min(1f, SoilChemistry.Sufficiency(ppm * SoilChemistry.Availability(n, ph), SoilChemistry.Adequate(n) * Mathf.Pow(Mathf.Max(0.05f, d.Need(n)), 0.6f)));

    public sealed class Report
    {
        public string species = "";
        public string start = "";
        public int planted, cameUp, alive;
        /// <summary>The first day (since sowing) any plant reached each stage; -1 = never.</summary>
        public readonly float[] firstStageDay = new float[(int)PlantStage.Dead + 1];
        /// <summary>Fruits (or roots, heads, bunches) that became ripe over the trial (none were picked).</summary>
        public int ripened;
        /// <summary>The most ripe at one time on one plant.</summary>
        public int mostRipe;
        public float firstRipeDay = -1f;
        public float quality, health, size;
        public float worstPests, worstDisease;
        public float waterGiven;
        public readonly List<string> timeline = new List<string>();
        /// <summary>Growing hours each need was the one holding growth back most (by <see cref="GrowthNeed"/>), and how well it was met then (summed).</summary>
        public readonly float[] limitHours = new float[(int)GrowthNeed.Health + 1], limitSum = new float[(int)GrowthNeed.Health + 1];
        /// <summary>Hours each nutrient was the scarcest while nutrients held growth back.</summary>
        public readonly float[] nutrientHours = new float[SoilChannels.NutrientCount];
        public float growingHours, growthSum;

        /// <summary>"nutrients (phosphorus) 46% of its growing hours, met at 55%; temperature 30%, met at 41%".</summary>
        public string Limits(int top = 3)
        {
            if (growingHours <= 0f) return "";
            var order = new List<int>();
            for (int i = 0; i < limitHours.Length; i++) if (limitHours[i] > 0f) order.Add(i);
            order.Sort((a, b) => limitHours[b].CompareTo(limitHours[a]));
            var parts = new List<string>();
            for (int k = 0; k < order.Count && k < top; k++)
            {
                int i = order[k];
                string name = ((GrowthNeed)i).ToString().ToLowerInvariant();
                if (i == (int)GrowthNeed.Nutrients)
                {
                    int worst = 0;
                    for (int n = 1; n < nutrientHours.Length; n++) if (nutrientHours[n] > nutrientHours[worst]) worst = n;
                    name += $" ({((Nutrient)worst).ToString().ToLowerInvariant()})";
                }
                parts.Add($"{name} {limitHours[i] / growingHours * 100f:0}% of its growing hours, met at {limitSum[i] / limitHours[i] * 100f:0}%");
            }
            return string.Join("; ", parts);
        }

        public float Day(PlantStage s) => firstStageDay[(int)s];

        public override string ToString()
        {
            var b = new StringBuilder();
            b.AppendLine($"{species}: {planted} sown {start}.");
            b.AppendLine($"Came up: {cameUp}/{planted}, alive at the end: {alive}/{planted}.");
            void Stage(string name, PlantStage s)
            {
                float d = Day(s);
                if (d >= 0f) b.AppendLine($"  {name}: day {d:0.#}");
            }
            Stage("Germinating", PlantStage.Germination);
            Stage("Seedling", PlantStage.Seedling);
            Stage("Vegetative", PlantStage.Vegetative);
            Stage("Flowering", PlantStage.Flowering);
            Stage("Fruit developing", PlantStage.FruitDevelopment);
            Stage("Ready to harvest", PlantStage.HarvestReady);
            Stage("Senescence", PlantStage.Senescence);
            Stage("First death", PlantStage.Dead);
            if (firstRipeDay >= 0f) b.AppendLine($"  First ripe produce: day {firstRipeDay:0.#}");
            b.AppendLine($"Ripened: {ripened} (at most {mostRipe} ripe at once on a plant; none picked).");
            b.AppendLine($"End: quality {quality:0.00}, health {health:0.00}, size {size:0.00} (averages of the living).");
            b.AppendLine($"Worst pests {worstPests:0.00}, worst disease {worstDisease:0.00}, water given {waterGiven:0} mm.");
            if (growingHours > 0f)
            {
                b.AppendLine($"Growth: {growthSum / growingHours * 100f:0}% of its best on average while growing.");
                b.AppendLine($"Held back most by: {Limits()}.");
            }
            return b.ToString().TrimEnd();
        }
    }

    /// <summary>Grows a trial plot of <paramref name="species"/>; <paramref name="catalog"/> gives the rules and the neighbours.</summary>
    public static Report Grow(PlantSpeciesData species, IAgriCatalog catalog, Options o = null)
    {
        o ??= Options.For(species, catalog?.Settings);
        var report = new Report { species = species != null ? species.name : "?" };
        if (species == null) return report;

        // The species itself (an asset not yet in the database works too), then the catalog's.
        var cat = new AgriCatalog { Settings = catalog?.Settings ?? AgriSettings.Defaults() };
        cat.Add(species);
        if (catalog != null)
        {
            foreach (PlantSpeciesData s in catalog.AllSpecies) cat.Add(s);
            for (int i = 0; catalog.Amendment(i) != null; i++) cat.Add(catalog.Amendment(i));
            foreach (HybridRule rule in catalog.Hybrids) cat.Add(rule);
        }
        AgriSettings st = cat.Settings;

        int n = Mathf.Clamp(o.plants, 1, 32);
        float gap = Mathf.Max(0.5f, species.spacing);
        float w = Mathf.Max(6f, n * gap + 2f), h = Mathf.Max(4f, gap * 2f + 2f);
        var layout = new SoilLayout
        {
            outline = new List<Vector2> { new Vector2(-w * 0.5f, -h * 0.5f), new Vector2(w * 0.5f, -h * 0.5f), new Vector2(w * 0.5f, h * 0.5f), new Vector2(-w * 0.5f, h * 0.5f) },
            baseSoil = SoilProfile.Preset(o.soil),
            variation = 0f,
            seed = o.seed,
        };
        layout.baseSoil.tilth = 0.8f;
        if (o.idealPh) layout.baseSoil.ph = (species.phLow + species.phHigh) * 0.5f;
        if (species.magicNeed > 0f) layout.baseSoil.magicEssence = Mathf.Max(layout.baseSoil.magicEssence, Mathf.Min(1f, species.magicNeed + 0.2f));
        SoilGrid g = layout.CreateGrid();

        int dps = Mathf.Max(1, st.daysPerSeason);
        int seasonFromStart = ((int)o.startSeason - (int)st.startSeason + 4) % 4;
        double start = 24.0 * (seasonFromStart * dps + Mathf.Clamp(o.startDay, 0, dps - 1)) + (species.plantingTime == PlantingTime.Night ? 22.0 : 8.0);
        layout.Bake(g, start);
        var date = new AgriDate(start, st);
        report.start = date.Describe();

        var field = new FieldState(g, cat, o.seed);
        var climate = new CityClimateSource(st);
        field.TickEnvironment(start, climate);
        var plants = new List<PlantState>();
        for (int i = 0; i < n; i++)
        {
            var p = new PlantState
            {
                species = species.id,
                position = new Vector2(-(n - 1) * gap * 0.5f + i * gap, 0f),
                depthCm = species.depthCm,
                covered = !species.surfaceSown,
                planted = start,
                last = start,
                genes = PlantGenes.Wild,
                viability = 0.95f,
                plantedAtNight = species.plantingTime == PlantingTime.Night,
                plantedMoon = date.Moon,
                lastSeason = date.Season,
            };
            plants.Add(field.Add(p));
        }
        report.planted = n;
        for (int s = 0; s < report.firstStageDay.Length; s++) report.firstStageDay[s] = -1f;

        var scratch = new PlantScratch();
        var cells = new List<int>();
        var weights = new List<float>();
        var ripe = new HashSet<long>();
        var cameUp = new HashSet<int>();
        int hours = Mathf.Clamp(o.days, 1, 2000) * 24;
        WaterQuality well = st.Water(WaterSourceKind.Well);
        PlantStage lastNoted = PlantStage.Seed;
        for (int hour = 1; hour <= hours; hour++)
        {
            double now = start + hour;
            float day = hour / 24f;
            field.TickEnvironment(now, climate);
            foreach (PlantState p in plants)
            {
                scratch.stepped = false;
                PlantSim.Advance(field, p, now, scratch, climate);
                if (scratch.stepped && p.Emerged && !p.dormant)
                {
                    int worst = 0;
                    for (int k = 1; k < scratch.needs.Length; k++) if (scratch.needs[k] < scratch.needs[worst]) worst = k;
                    report.limitHours[worst] += 1f;
                    report.limitSum[worst] += scratch.needs[worst];
                    if (worst == (int)GrowthNeed.Nutrients)
                    {
                        int scarce = 0;
                        for (int nu = 1; nu < scratch.sufficiency.Length; nu++) if (scratch.sufficiency[nu] < scratch.sufficiency[scarce]) scarce = nu;
                        report.nutrientHours[scarce] += 1f;
                    }
                    report.growingHours += 1f;
                    report.growthSum += Mathf.Min(1f, scratch.growth);
                }
                if (report.firstStageDay[(int)p.stage] < 0f) report.firstStageDay[(int)p.stage] = day;
                if (p.Emerged) cameUp.Add(p.id);
                int ripeNow = 0;
                foreach (FruitState f in p.fruits)
                    if (f.stage == ProduceStage.Ripe)
                    {
                        ripeNow++;
                        if (ripe.Add(((long)p.id << 20) | (uint)f.id) && report.firstRipeDay < 0f) report.firstRipeDay = day;
                    }
                report.mostRipe = Mathf.Max(report.mostRipe, ripeNow);
                report.worstPests = Mathf.Max(report.worstPests, p.PestLoad());
                report.worstDisease = Mathf.Max(report.worstDisease, p.DiseaseLoad());
            }
            PlantState first = plants[0];
            if (first.stage != lastNoted)
            {
                report.timeline.Add($"day {day:0.0}: {first.stage}");
                lastNoted = first.stage;
            }
            if (o.protect && hour % 12 == 0)
                foreach (PlantState p in plants)
                {
                    if (p.pests != null) System.Array.Clear(p.pests, 0, p.pests.Length);
                    if (p.diseases != null) System.Array.Clear(p.diseases, 0, p.diseases.Length);
                    p.wounds = 0f;
                }
            if (o.feed && hour % 24 == 0)
                foreach (PlantState p in plants)
                {
                    if (!p.Alive) continue;
                    g.CellsInRadius(p.position, Mathf.Max(0.3f, species.rootRadius), cells, weights);
                    foreach (int ci in cells)
                    {
                        g.Advance(ci, now, st);
                        for (int nu = 0; nu < SoilChannels.NutrientCount; nu++)
                        {
                            var ch = (SoilChannel)(1 + nu);
                            float want = SoilChemistry.Adequate((Nutrient)nu) * Mathf.Pow(Mathf.Max(0.05f, species.Need((Nutrient)nu)), 0.6f) * 1.2f /
                                         Mathf.Max(0.2f, SoilChemistry.Availability((Nutrient)nu, g.Get(ci, SoilChannel.Ph)));
                            float have = g.Get(ci, ch);
                            if (have < want) g.Add(ci, ch, want - have);
                        }
                    }
                }
            if (o.water && hour % 12 == 0)
                foreach (PlantState p in plants)
                {
                    if (!p.Alive || !g.TryIndex(p.position, out int c)) continue;
                    g.Advance(c, now, st);
                    if (g.AvailableWater(c) >= 0.6f) continue;
                    g.CellsInRadius(p.position, 0.35f, cells, weights);
                    foreach (int ci in cells)
                    {
                        g.Advance(ci, now, st);
                        g.AddWater(ci, 6f, well);
                    }
                    report.waterGiven += 6f;
                }
        }

        report.ripened = ripe.Count;
        report.cameUp = cameUp.Count;
        int living = 0;
        foreach (PlantState p in plants)
        {
            if (!p.Alive) continue;
            living++;
            report.quality += p.Quality;
            report.health += p.health;
            report.size += p.size;
        }
        report.alive = living;
        if (living > 0)
        {
            report.quality /= living;
            report.health /= living;
            report.size /= living;
        }
        return report;
    }
}
