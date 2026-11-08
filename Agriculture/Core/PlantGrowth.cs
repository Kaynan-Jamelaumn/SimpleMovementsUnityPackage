using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scratch buffers of <see cref="PlantSim"/> (one per thread of work: no allocation while plants grow).</summary>
public sealed class PlantScratch
{
    public readonly List<int> cells = new List<int>();
    public readonly List<float> weights = new List<float>();
    public readonly List<PlantState> near = new List<PlantState>();
    public readonly List<PlantState> donors = new List<PlantState>();
    public readonly float[] conc = new float[SoilChannels.NutrientCount];
    public readonly float[] sufficiency = new float[SoilChannels.NutrientCount];
    public readonly float[] repel = new float[8];
    public readonly float[] suppression = new float[8];
    public readonly float[] pestPressure = new float[8];
    public readonly float[] diseasePressure = new float[6];
    public readonly float[] mix = new float[SoilTypes.Count];
    /// <summary>How well each need was met in the last growing step (0-1, by <see cref="GrowthNeed"/>) - for tools and tests.</summary>
    public readonly float[] needs = new float[(int)GrowthNeed.Health + 1];
    /// <summary>Set when a growing step filled <see cref="needs"/> (clear it before a step to know).</summary>
    public bool stepped;
    public float growth;
}

/// <summary>The needs a growing plant's growth is limited by (what <see cref="PlantScratch.needs"/> holds).</summary>
public enum GrowthNeed
{
    Temperature,
    Water,
    Light,
    Nutrients,
    Ph,
    SoilType,
    Salinity,
    Compaction,
    Humidity,
    Crowding,
    Neighbours,
    Damage,
    Magic,
    Health,
}

/// <summary>
/// How plants live, hour by hour. <see cref="Advance"/> brings a plant from the last time it was updated to now, in
/// steps of at most <see cref="AgriSettings.maxStepHours"/> (catching up after time away costs a few steps, not one per
/// hour). Each step:
/// <list type="number">
/// <item>its root zone is brought up to date and read: the water in reach, the pH and what it lets through of each
/// nutrient, salts, packing, magic, soil diseases, how like each soil type it is;</item>
/// <item>a seed waits for warmth and moisture in its season, then comes up - or not: depth, worked soil, packing,
/// stones, salt, the moon and the hour it was sown at decide;</item>
/// <item>a growing plant meets its neighbours (shade from taller plants, crowding, companions, pests and diseases that
/// spread), works out how well every need is met, and the most limiting need decides most of its growth (Liebig's
/// law); temperature drives its development, conditions its size;</item>
/// <item>it drinks and feeds from its root cells (crowded plants share them: competition for water and food is just
/// that), fixes nitrogen if it can;</item>
/// <item>its stages follow (dormancy and the cold it needs, day length and season for flowering, flushes of flowers,
/// each piece of produce on its own timer, pollination by insects, wind or hand, fruit that drops, rots or is
/// picked);</item>
/// <item>pests arrive, breed and spread, diseases take hold in the weather that suits them, damage adds up, health
/// falls or recovers, the marks of its troubles show (<see cref="PlantSymptom"/>), and what it went through is added to
/// its history (the quality of everything it gives).</item>
/// </list>
/// Pure C#, the same result for the same field, plant and time on every machine.
/// </summary>
public static class PlantSim
{
    // mg taken up per hour by a full-grown average plant (N, P, K, Ca, Mg, S, Fe, Zn, Cu, Mn, B, Mo).
    private static readonly float[] BaseUptake = { 2f, 0.35f, 2.5f, 1f, 0.25f, 0.2f, 0.02f, 0.0075f, 0.002f, 0.0125f, 0.004f, 0.0002f };
    // How many times the adequate level becomes toxic.
    private static readonly float[] ToxicAt = { 5f, 6f, 4f, 5f, 5f, 6f, 8f, 6f, 5f, 6f, 3.5f, 10f };
    private static readonly float[] PestGrowth = { 0.03f, 0.014f, 0.035f, 0.01f, 0.02f, 0.008f, 0.012f, 0.01f };
    private static readonly float[] PestDamage = { 0.0018f, 0.004f, 0.0025f, 0.003f, 0.0018f, 0.003f, 0.0035f, 0.003f };
    private static readonly float[] DiseaseGrowth = { 0.012f, 0.009f, 0.007f, 0.009f, 0.011f, 0.004f };
    private static readonly float[] DiseaseDamage = { 0.005f, 0.01f, 0.009f, 0.004f, 0.01f, 0.003f };

    /// <summary>Brings a plant up to <paramref name="now"/> (game hours). Bumps the field's plant version when it looks different.</summary>
    public static void Advance(FieldState f, PlantState p, double now, PlantScratch sc, IAgriClimateSource src)
    {
        if (f == null || p == null) return;
        if (p.last <= 0.0) p.last = p.planted;
        double gap = now - p.last;
        if (gap <= 1e-4) return;
        p.data ??= f.Catalog?.Species(p.species);
        if (p.data == null)
        {
            p.last = now;
            return;
        }
        AgriSettings s = f.Settings;
        int steps = Math.Max(1, Math.Min(64, (int)Math.Ceiling(gap / Math.Max(0.25, s.maxStepHours))));
        double dt = gap / steps;
        int before = VisualKey(p);
        for (int k = 0; k < steps; k++)
        {
            double t = p.last + dt;
            Step(f, p, t, (float)dt, s, sc ?? new PlantScratch(), src);
            p.last = t;
        }
        p.last = now;
        if (VisualKey(p) != before) f.PlantVersion++;
    }

    /// <summary>A signature of how a plant looks (stage, size, health, marks, pests, produce): it changes when the drawing must.</summary>
    public static int VisualKey(PlantState p)
    {
        unchecked
        {
            int h = (int)p.stage * 31 + Mathf.RoundToInt(p.size * 24f);
            h = h * 31 + Mathf.RoundToInt(p.health * 8f) + (p.dormant ? 1000 : 0) + (p.supported ? 2000 : 0) + (p.bolted ? 4000 : 0);
            for (int i = 0; i < PlantState.SymptomCount; i++) h = h * 7 + Mathf.RoundToInt(p.symptoms[i] * 4f);
            for (int i = 0; i < 8; i++) h = h * 5 + Mathf.RoundToInt(p.pests[i] * 4f);
            for (int i = 0; i < 6; i++) h = h * 5 + Mathf.RoundToInt(p.diseases[i] * 4f);
            foreach (FruitState fr in p.fruits) h = h * 13 + fr.id * 17 + (int)fr.stage * 3 + Mathf.RoundToInt(fr.size * 4f);
            return h;
        }
    }

    private static AgriWeather WeatherAt(FieldState f, AgriSettings s, IAgriClimateSource src, double t)
    {
        if (src == null) return AgriEnvironment.Climate(s, t);
        if (src.Historic) return src.WeatherAt(t);
        return f.EnvTime >= 0.0 && f.EnvTime - t <= 2.0 ? f.Weather : AgriEnvironment.Climate(s, t);
    }

    /// <summary>The root zone's cells and weights, rebuilt as the plant grows.</summary>
    public static void EnsureRoots(SoilGrid g, PlantState p, PlantScratch sc)
    {
        PlantSpeciesData d = p.data;
        float radius = p.IsSeed || d == null ? 0.05f : Mathf.Max(0.05f, d.rootRadius * (0.25f + 0.75f * p.size) * Mathf.Sqrt(p.genes.Factor(GeneTrait.MaxSize)));
        if (p.rootCells != null && p.rootRadius > 0f && Mathf.Abs(radius - p.rootRadius) < Mathf.Max(0.04f, p.rootRadius * 0.2f))
            return;
        g.CellsInRadius(p.position, radius, sc.cells, sc.weights);
        p.rootCells = sc.cells.ToArray();
        p.rootWeights = sc.weights.ToArray();
        p.rootRadius = radius;
    }

    private static void Step(FieldState f, PlantState p, double t, float dt, AgriSettings s, PlantScratch sc, IAgriClimateSource src)
    {
        if (!p.Alive)
        {
            p.deadHours += dt;
            return;
        }
        PlantSpeciesData d = p.data;
        SoilGrid g = f.Grid;
        var date = new AgriDate(t, s);
        AgriWeather w = WeatherAt(f, s, src, t);
        int cell = g.TryIndex(p.position, out int ci) ? ci : -1;
        float coverWarm = cell >= 0 ? g.CoverWarmth[cell] : 0f, coverFrost = cell >= 0 ? g.CoverFrost[cell] : 0f;
        float coverLight = cell >= 0 ? g.CoverLight[cell] : 1f, coverRain = cell >= 0 ? g.CoverRain[cell] : 1f, coverEvap = cell >= 0 ? g.CoverEvap[cell] : 1f;
        float air = w.temperature + coverWarm;
        if (coverFrost > 0f && air < 1.5f) air = Mathf.Lerp(air, 1.5f, coverFrost);
        float soilT = f.SoilTemperature + (cell >= 0 ? SoilTypes.Warmth(g.TypeAt(cell)) : 0f) + coverWarm * 0.7f;
        float humidity = AgriMath.Clamp01(w.humidity + (coverRain < 0.5f ? 0.08f : 0f));
        bool leafWet = (f.WetLeaves > 0f && coverRain > 0.5f) || p.leafWet > 0f;
        p.leafWet = Mathf.Max(0f, p.leafWet - dt);
        var rng = new AgriRandom(f.Seed ^ (p.id * 7919), (int)(t * 4.0), 3);
        p.ageDays += dt / 24f;

        // ---------------------------------------------------------------- the root zone
        EnsureRoots(g, p, sc);
        float sumW = 0f, avail = 0f, ph = 0f, sal = 0f, comp = 0f, tilth = 0f, stones = 0f, magic = 0f, path = 0f;
        Array.Clear(sc.conc, 0, sc.conc.Length);
        float[] v = g.Values;
        for (int k = 0; k < p.rootCells.Length; k++)
        {
            int c = p.rootCells[k];
            float wgt = p.rootWeights[k];
            g.Advance(c, t, s);
            int o = c * SoilGrid.Stride;
            float cph = v[o + (int)SoilChannel.Ph];
            avail += wgt * SoilChemistry.Available(v[o + (int)SoilChannel.Moisture], v[o + (int)SoilChannel.WaterRetention]);
            ph += wgt * cph;
            sal += wgt * v[o + (int)SoilChannel.Salinity];
            comp += wgt * v[o + (int)SoilChannel.Compaction];
            tilth += wgt * v[o + (int)SoilChannel.Tilth];
            stones += wgt * v[o + (int)SoilChannel.Stones];
            magic += wgt * v[o + (int)SoilChannel.MagicEssence];
            path += wgt * v[o + (int)SoilChannel.Pathogens];
            // Plants feed well at the pH they are made for, whatever the chemistry says (acid lovers in sour soil).
            float adapt = 0.75f * AgriMath.Band(cph, d.phMin, d.phLow, d.phHigh, d.phMax);
            for (int n = 0; n < SoilChannels.NutrientCount; n++)
                sc.conc[n] += wgt * v[o + 1 + n] * Mathf.Max(SoilChemistry.Availability((Nutrient)n, cph), adapt);
            sumW += wgt;
        }
        if (sumW <= 0f)
        {
            p.last = t;
            return;
        }
        float inv = 1f / sumW;
        avail *= inv; ph *= inv; sal *= inv; comp *= inv; tilth *= inv; stones *= inv; magic *= inv; path *= inv;
        for (int n = 0; n < SoilChannels.NutrientCount; n++) sc.conc[n] *= inv;
        float affinity = 0.7f;
        if (cell >= 0)
        {
            SoilChemistry.TypeMix(v, cell * SoilGrid.Stride, g.TypeAt(cell), sc.mix);
            affinity = 0f;
            for (int k = 0; k < SoilTypes.Count; k++) affinity += sc.mix[k] * d.SoilAffinity((SoilType)k);
        }

        if (p.IsSeed)
        {
            StepSeed(p, d, s, date, dt, soilT, avail, tilth, comp, stones, sal, path, magic, ref rng);
            return;
        }

        // ---------------------------------------------------------------- years, seasons, dormancy
        if (date.Season != p.lastSeason)
        {
            if (date.Season == Season.Spring && p.lastSeason == Season.Winter)
            {
                p.years++;
                p.flushes = 0;
            }
            p.lastSeason = date.Season;
        }
        if (air < 7f && air > -12f) p.chill += dt;
        bool restSeason = d.Perennial && (d.dormancySeasons & AgriDate.Mask(date.Season)) != 0;
        if (restSeason && !p.dormant && !Developing(p)) p.dormant = true;
        else if (!restSeason && p.dormant)
        {
            p.dormant = false;
            if (p.stage > PlantStage.Vegetative && p.stage < PlantStage.Senescence)
            {
                p.stage = PlantStage.Vegetative;
                p.progress = 1f;
            }
        }
        p.flushTimer = Mathf.Max(0f, p.flushTimer - dt);

        // ---------------------------------------------------------------- neighbours and light
        Neighbourhood(f, p, d, t, sc, out float shade, out float crowd, out float growthMul, out float inhibit, out float pollinatorBoost);
        // Light relative to an open, clear sky at this hour (1 = full sun): clouds, cover and taller neighbours take from it.
        float lightNow = w.light * coverLight * (1f - Mathf.Min(0.9f, shade));
        float clearSky = AgriEnvironment.Sunlight(date.Hour, date.Sunrise, date.Sunset, 0f);
        if (w.day && clearSky > 0.08f)
            p.lightAvg += (Mathf.Clamp01(lightNow / clearSky) - p.lightAvg) * (1f - AgriMath.Decay(1f / 36f, dt));

        // ---------------------------------------------------------------- how well each need is met
        float tMin = d.tempMin + p.genes.ColdShift, tMax = d.tempMax + p.genes.HeatShift;
        float fT = AgriMath.Band(air, tMin, d.tempLow, d.tempHigh, tMax);
        float tDev = air <= d.tempLow ? AgriMath.Smooth(tMin + 1f, d.tempLow, air) : air <= d.tempHigh ? 1f : Mathf.Lerp(1f, 0.4f, AgriMath.InverseLerp(d.tempHigh, tMax, air));
        float drought = AgriMath.Clamp01(d.droughtTolerance + p.genes.DroughtShift);
        float fW;
        if (avail < d.waterLow) fW = Mathf.Pow(AgriMath.Smooth(-0.05f, d.waterLow, avail), 1f - 0.6f * drought);
        else if (avail > d.waterHigh) fW = 1f - (1f - d.waterlogTolerance) * 0.85f * AgriMath.Smooth(d.waterHigh, d.waterHigh + 0.55f, avail);
        else fW = 1f;
        float fL;
        if (p.lightAvg < d.lightLow) fL = Mathf.Pow(AgriMath.Smooth(0.02f, d.lightLow, p.lightAvg), 0.8f);
        else if (p.lightAvg > d.lightHigh) fL = 1f - d.lightExcess * 0.6f * AgriMath.InverseLerp(d.lightHigh, Mathf.Max(d.lightHigh + 0.05f, 1f), p.lightAvg);
        else fL = 1f;

        float macro = 1f, second = 1f, micro = 1f, toxicity = 0f;
        float eff = p.genes.Factor(GeneTrait.NutrientEfficiency);
        for (int n = 0; n < SoilChannels.NutrientCount; n++)
        {
            var nut = (Nutrient)n;
            float need = d.Need(nut) * Emphasis(nut, p.stage, d) * eff;
            float adequate = SoilChemistry.Adequate(nut) * Mathf.Pow(Mathf.Max(0.05f, need), 0.6f);
            float r = adequate <= 0f ? 2f : sc.conc[n] / adequate;
            float suff = need <= 0.01f || r >= 1f ? 1f : Mathf.Pow(Mathf.Max(0f, r), 0.8f);
            sc.sufficiency[n] = suff;
            // Excess is judged against what an average plant needs (light feeders are no more sensitive to plenty); heavy feeders bear more.
            float excess = sc.conc[n] / (SoilChemistry.Adequate(nut) * ToxicAt[n] * Mathf.Sqrt(Mathf.Max(1f, need)));
            if (excess > 1f) toxicity += excess - 1f;
            if (n < 3) macro = Mathf.Min(macro, suff);
            else if (n < 6) second = Mathf.Min(second, suff);
            else micro = Mathf.Min(micro, suff);
        }
        float fN = macro * Mathf.Pow(second, 0.5f) * Mathf.Pow(micro, 0.3f) * (1f - Mathf.Min(0.6f, toxicity * 0.3f));
        float fPh = Mathf.Lerp(0.15f, 1f, AgriMath.Band(ph, d.phMin, d.phLow, d.phHigh, d.phMax));
        float fSoil = Mathf.Lerp(0.5f, 1f, affinity);
        float fSalt = 1f - 0.9f * AgriMath.Smooth(d.salinityTolerance, d.salinityTolerance + 6f, sal);
        float fPack = 1f - 0.7f * AgriMath.Smooth(d.compactionTolerance, 1f, comp);
        float fHum = Mathf.Lerp(0.75f, 1f, AgriMath.Band(humidity, d.humidityLow - 0.25f, d.humidityLow, d.humidityHigh, d.humidityHigh + 0.2f));
        float fCrowd = 1f / (1f + crowd);
        float fNear = Mathf.Clamp(growthMul, 0.3f, 1.6f) * (1f - Mathf.Min(0.8f, inhibit));
        float pestHit = 0f, disHit = 0f;
        for (int k = 0; k < 8; k++) pestHit += p.pests[k] * PestDamage[k] * 60f;
        for (int k = 0; k < 6; k++) disHit += p.diseases[k] * DiseaseDamage[k] * 60f;
        float fDamage = 1f - Mathf.Min(0.9f, pestHit + disHit);
        float fMagic = d.magicNeed > 0f ? Mathf.Lerp(0.05f, 1f, AgriMath.Smooth(0f, d.magicNeed, magic)) : 1f;
        float fHealth = 0.3f + 0.7f * p.health;

        // The most limiting need decides most of it; the others still count a little.
        float fNearLimit = Mathf.Min(1f, fNear);
        float minF = Min(Min(Min(fT, fW), Min(fL, fN)), Min(Min(fPh, fSoil), Min(fSalt, fPack)));
        minF = Min(minF, Min(Min(fHum, fCrowd), Min(Min(fNearLimit, fDamage), Min(fMagic, fHealth))));
        float geo = (float)Math.Exp((Ln(fT) + Ln(fW) + Ln(fL) + Ln(fN) + Ln(fPh) + Ln(fSoil) + Ln(fSalt) + Ln(fPack) + Ln(fHum) + Ln(fCrowd) + Ln(fNearLimit) + Ln(fDamage) + Ln(fMagic) + Ln(fHealth)) / 14.0);
        float G = Mathf.Pow(Mathf.Max(0f, minF), 0.6f) * Mathf.Pow(geo, 0.4f) * Mathf.Max(1f, fNear);
        G *= 1f + (d.MoonGermination(date.Moon) - 1f) * d.lunarStrength * 0.3f;
        G = Mathf.Clamp(G, 0f, 1.4f);
        if (p.dormant) G *= 0.05f;
        float[] needs = sc.needs;
        needs[0] = fT; needs[1] = fW; needs[2] = fL; needs[3] = fN; needs[4] = fPh; needs[5] = fSoil; needs[6] = fSalt;
        needs[7] = fPack; needs[8] = fHum; needs[9] = fCrowd; needs[10] = fNearLimit; needs[11] = fDamage; needs[12] = fMagic; needs[13] = fHealth;
        sc.growth = G;
        sc.stepped = true;

        // ---------------------------------------------------------------- drinking and feeding
        float waterStress = Uptake(g, p, d, w, t, dt, s, coverEvap, sc);
        if (d.nitrogenFixer && !p.dormant)
        {
            // Legumes fix nitrogen while the soil lacks it, and stop when it has plenty.
            float want = 1f - AgriMath.Smooth(15f, 45f, sc.conc[(int)Nutrient.Nitrogen]);
            if (want > 0f) SpreadTotal(g, p, SoilChannel.Nitrogen, 2.5f * want * Mass(d) * p.size * Mathf.Min(1f, G) * dt / g.CellSoilKg);
        }
        if (d.magicNeed > 0f && !p.dormant)
            SpreadEach(g, p, SoilChannel.MagicEssence, -d.magicNeed * 0.0003f * p.size * dt);

        // ---------------------------------------------------------------- development
        float growthGene = p.genes.Factor(GeneTrait.GrowthSpeed) * (1f + p.pruneBoost * 0.25f);
        float stageHours = Mathf.Max(0.1f, d.StageDays(p.stage)) * 24f / Mathf.Max(0.01f, s.growthSpeed);
        float dev = p.dormant ? 0f : dt / stageHours * tDev * (0.35f + 0.65f * Mathf.Min(1f, G)) * growthGene;
        ProduceSpec pr = d.Produce;
        switch (p.stage)
        {
            case PlantStage.Seedling:
                p.progress += dev;
                if (p.progress >= 1f)
                {
                    p.stage = PlantStage.Juvenile;
                    p.progress = 0f;
                    if (pr.part == ProducePart.Root) NewPiece(p, ref rng, 0.5f);
                }
                break;
            case PlantStage.Juvenile:
                p.progress += dev;
                if (p.progress >= 1f)
                {
                    p.stage = PlantStage.Vegetative;
                    p.progress = 0f;
                    if (pr.part == ProducePart.Leaf && pr.wholePlant) NewPiece(p, ref rng, 0.5f);
                }
                break;
            case PlantStage.Vegetative:
                p.progress = Mathf.Min(1f, p.progress + dev);
                if (p.progress >= 1f && CanFlower(p, d, date)) StartFlush(p, d, ref rng);
                break;
            case PlantStage.Flowering:
                Flowering(p, d, s, dt, tDev, ref rng);
                break;
            case PlantStage.Senescence:
                p.progress += dt / (Mathf.Max(0.1f, d.senescenceDays) * 24f);
                p.health -= 0.4f * dt / (Mathf.Max(0.1f, d.senescenceDays) * 24f);
                if (p.progress >= 1f) Die(p, false);
                break;
        }
        if (!p.Alive) return;

        // Leaves keep coming on leaf crops.
        if (pr.part == ProducePart.Leaf && !pr.wholePlant && !p.dormant && p.stage >= PlantStage.Juvenile && p.stage <= PlantStage.Flowering)
        {
            int live = 0;
            foreach (FruitState fr in p.fruits) if (fr.stage < ProduceStage.Rotten) live++;
            int cap = Mathf.Max(1, Mathf.RoundToInt(pr.countMax * p.genes.Factor(GeneTrait.FruitQuantity) * (0.4f + 0.6f * p.size)));
            if (live < cap && rng.Chance(dt / (Mathf.Max(0.25f, pr.flushIntervalDays > 0f ? pr.flushIntervalDays : 1f) * 24f) * Mathf.Min(1f, G) * 2f))
                NewPiece(p, ref rng, 0f);
            if (!p.bolted && p.stage == PlantStage.Vegetative && p.progress >= 1f && Bolts(p, d, date, air))
            {
                p.bolted = true;
                p.stage = PlantStage.Flowering;
                p.progress = 0f;
            }
        }

        // Size: towards the size of its stage, smaller when it has lived badly.
        float target = TargetSize(p, d) * (0.45f + 0.55f * Mathf.Clamp01(p.Quality * 1.15f));
        if (!p.dormant && p.stage < PlantStage.Senescence && p.size < target)
            p.size += (target - p.size) * (1f - AgriMath.Decay(0.06f * Mathf.Min(1f, G) * Mathf.Max(0.2f, tDev) * growthGene * s.growthSpeed, dt));

        // ---------------------------------------------------------------- the produce
        StepProduce(f, p, d, s, w, dt, tDev, G, fW, sc.sufficiency[(int)Nutrient.Potassium], pollinatorBoost, ref rng);
        UpdateFruitingStage(p, d, date);

        // Annuals end with the year; perennials with their lifespan.
        if (p.stage < PlantStage.Senescence)
        {
            if (d.lifeCycle == PlantLifeCycle.Annual && date.Season == Season.Winter && !AgriDate.In(d.sowingSeasons, Season.Winter) && d.sowingSeasons != SeasonMask.None)
                StartSenescence(p);
            else if (d.Perennial && d.lifespanYears > 0f && p.years >= d.lifespanYears)
                StartSenescence(p);
        }

        // ---------------------------------------------------------------- pests and diseases
        Pests(p, d, s, date, w, air, soilT, avail, sc, ref rng, dt);
        Diseases(p, d, s, air, humidity, avail, path, leafWet, waterStress, sc, ref rng, dt);

        // ---------------------------------------------------------------- damage and health
        float wilt = 1f - AgriMath.Smooth(-0.05f, 0.25f * (1f - 0.7f * drought) + 0.02f, avail);
        wilt = Mathf.Max(wilt, waterStress * 0.8f);
        float logged = AgriMath.Smooth(1.15f, 1.5f, avail);
        float damage = 0f;
        if (wilt > 0.05f) damage += wilt * 0.02f * (1f - 0.8f * drought);
        damage += logged * 0.004f * (1f - d.waterlogTolerance);
        if (air < tMin) damage += (tMin - air) * 0.01f * (p.dormant ? 0.3f : 1f);
        float frost = 0f;
        if (air < 0f && d.frostSensitivity > 0f)
        {
            frost = d.frostSensitivity * d.frostSensitivity * (-air + 1f) * 0.02f * (p.dormant ? 0.25f : 1f) * Mathf.Max(0.2f, 1f - 0.15f * p.genes.Level(GeneTrait.ColdResistance));
            damage += frost;
        }
        // A resting plant (leaves shed, life drawn down into its roots) bears the heat far better.
        if (air > tMax) damage += (air - tMax) * 0.012f * Mathf.Max(0.2f, 1f - 0.15f * p.genes.Level(GeneTrait.HeatResistance)) * (p.dormant ? 0.08f : 1f);
        if (p.lightAvg < d.lightLow * 0.35f) damage += 0.0008f;
        float scald = d.lightExcess > 0f && lightNow > d.lightHigh + 0.05f ? d.lightExcess * (lightNow - d.lightHigh) : 0f;
        damage += scald * 0.006f;
        float saltBurn = Mathf.Max(0f, sal - d.salinityTolerance - 1f);
        damage += saltBurn * 0.0025f;
        damage += toxicity * 0.002f;
        float chronic = 0f;
        if (fN < 0.25f) chronic += (0.25f - fN) * 0.0015f;
        if (fMagic < 0.3f) chronic += 0.002f;
        for (int k = 0; k < 8; k++) chronic += p.pests[k] * PestDamage[k] * (k >= (int)PestKind.Slugs && p.stage <= PlantStage.Juvenile ? 4f : 1f);
        for (int k = 0; k < 6; k++) chronic += p.diseases[k] * DiseaseDamage[k];
        // An established perennial (woody, deep-rooted, with reserves) bears hunger, pests and disease far longer than a soft annual.
        if (d.Perennial) chronic /= 1f + 2f * p.size + 0.4f * Mathf.Min(5, p.years);
        damage += chronic;
        if (p.lastTemperature > -998f && dt > 0f)
        {
            float change = Mathf.Abs(air - p.lastTemperature) / Mathf.Max(1f, dt);
            if (change > 4f) damage += (change - 4f) * 0.004f * d.shockSensitivity;
        }
        p.lastTemperature = air;
        damage *= s.stressDamage;
        p.health -= damage * dt;
        if (damage < 0.002f) p.health += 0.003f * Mathf.Min(1f, G) * dt;
        p.health = Mathf.Min(1f, p.health);
        if (p.health <= 0f)
        {
            Die(p, false);
            return;
        }

        // ---------------------------------------------------------------- what can be seen
        float k12 = 1f - AgriMath.Decay(1f / 12f, dt), k3 = 1f - AgriMath.Decay(1f / 3f, dt), k72 = 1f - AgriMath.Decay(1f / 72f, dt);
        Ease(p, PlantSymptom.PaleOldLeaves, 1f - sc.sufficiency[(int)Nutrient.Nitrogen], k12);
        Ease(p, PlantSymptom.PurpleLeaves, Mathf.Max(1f - sc.sufficiency[(int)Nutrient.Phosphorus], air < 8f ? 0.3f : 0f), k12);
        Ease(p, PlantSymptom.ScorchedEdges, 1f - sc.sufficiency[(int)Nutrient.Potassium], k12);
        Ease(p, PlantSymptom.YellowBetweenVeins, 1f - sc.sufficiency[(int)Nutrient.Magnesium], k12);
        Ease(p, PlantSymptom.PaleNewLeaves, 1f - Mathf.Min(sc.sufficiency[(int)Nutrient.Iron], sc.sufficiency[(int)Nutrient.Manganese]), k12);
        Ease(p, PlantSymptom.DeadTips, 1f - Mathf.Min(sc.sufficiency[(int)Nutrient.Calcium], sc.sufficiency[(int)Nutrient.Boron]), k12);
        Ease(p, PlantSymptom.LeafBurn, AgriMath.Clamp01(toxicity * 0.6f + saltBurn * 0.25f), k12);
        Ease(p, PlantSymptom.Wilting, wilt, k3);
        Ease(p, PlantSymptom.Waterlogged, logged, k12);
        p.symptoms[(int)PlantSymptom.FrostBitten] = AgriMath.Clamp01(p.symptoms[(int)PlantSymptom.FrostBitten] * (1f - k72) + frost * dt * 8f);
        Ease(p, PlantSymptom.Sunscald, AgriMath.Clamp01(scald * 2f), k12);
        Ease(p, PlantSymptom.Leggy, 1f - AgriMath.Smooth(0.08f, Mathf.Max(0.1f, d.lightLow * 0.8f), p.lightAvg), k12);
        float expected = TargetSize(p, d);
        Ease(p, PlantSymptom.Stunted, expected > 0.05f ? AgriMath.Clamp01(1f - p.size / expected) * AgriMath.Smooth(2f, 6f, p.ageDays) : 0f, k12);

        // ---------------------------------------------------------------- history
        float stageWeight = p.stage <= PlantStage.Seedling ? 0.5f : p.stage == PlantStage.Juvenile ? 0.8f : p.stage == PlantStage.Vegetative ? 1f : p.stage == PlantStage.Senescence ? 0.2f : 1.5f;
        if (p.dormant) stageWeight *= 0.1f;
        float q = Mathf.Pow(Mathf.Min(1f, G / Mathf.Max(0.05f, p.dormant ? 0.05f : 1f)), 0.7f) * (0.5f + 0.5f * p.health) * (1f - 0.5f * p.stress);
        p.qualitySum += q * stageWeight * dt;
        p.qualityHours += stageWeight * dt;
        p.stress += (AgriMath.Clamp01(1f - Mathf.Min(1f, G / (p.dormant ? 0.05f : 1f))) - p.stress) * (1f - AgriMath.Decay(1f / 24f, dt));

        // Woody plants: dead wood builds, the crown thickens, pruning wears off, wounds close.
        if (d.Look.shape == PlantShape.Tree || d.Look.shape == PlantShape.Bush || d.Look.shape == PlantShape.Vine)
        {
            p.deadWood = AgriMath.Clamp01(p.deadWood + dt * 0.0003f * (1f + p.stress + disHit));
            p.canopy = AgriMath.Clamp01(p.canopy + (1f - p.canopy) * dt * 0.0008f * Mathf.Min(1f, G));
        }
        p.pruneBoost *= AgriMath.Decay(1f / 240f, dt);
        p.wounds *= AgriMath.Decay(0.02f, dt);
    }

    private static float Ln(float x) => (float)Math.Log(Mathf.Max(0.001f, x));
    private static float Min(float a, float b) => a < b ? a : b;

    private static void Ease(PlantState p, PlantSymptom s, float value, float k)
    {
        int i = (int)s;
        p.symptoms[i] += (AgriMath.Clamp01(value) - p.symptoms[i]) * k;
    }

    /// <summary>How much more of a nutrient a stage needs: nitrogen while growing leaves, phosphorus for roots and flowers, potassium and calcium for fruit.</summary>
    private static float Emphasis(Nutrient n, PlantStage stage, PlantSpeciesData d)
    {
        switch (stage)
        {
            case PlantStage.Seedling: return n == Nutrient.Phosphorus ? 1.2f : n == Nutrient.Nitrogen ? 0.8f : 1f;
            case PlantStage.Juvenile:
            case PlantStage.Vegetative: return n == Nutrient.Nitrogen ? 1.25f : 1f;
            case PlantStage.Flowering: return n == Nutrient.Phosphorus ? 1.3f : n == Nutrient.Boron ? 1.2f : 1f;
            case PlantStage.FruitDevelopment:
            case PlantStage.FruitMaturation:
            case PlantStage.HarvestReady:
            case PlantStage.Overripe: return n == Nutrient.Potassium ? 1.4f : n == Nutrient.Calcium ? 1.2f : n == Nutrient.Nitrogen ? 0.85f : 1f;
            default: return 1f;
        }
    }

    // ---------------------------------------------------------------- seeds
    private static void StepSeed(PlantState p, PlantSpeciesData d, AgriSettings s, AgriDate date, float dt, float soilT, float avail,
        float tilth, float comp, float stones, float sal, float path, float magic, ref AgriRandom rng)
    {
        float tempOk = AgriMath.Band(soilT, d.germinationMin - 3f, d.germinationMin, d.germinationMax, d.germinationMax + 4f);
        float moistOk = AgriMath.Band(avail, -0.05f, 0.3f, 1.1f, 1.6f);
        bool season = AgriDate.In(d.sowingSeasons, date.Season);
        float rate = tempOk * moistOk * (season ? 1f : 0.08f);
        p.progress += dt / (Mathf.Max(0.1f, d.germinationDays) * 24f / Mathf.Max(0.01f, s.growthSpeed)) * rate * p.genes.Factor(GeneTrait.GrowthSpeed);
        if (p.progress > 0.03f && p.stage == PlantStage.Seed) p.stage = PlantStage.Germination;

        // Seeds rot in cold, waterlogged soil and with soil diseases; one left uncovered dries out and is taken by birds.
        float rot = AgriMath.Smooth(1.1f, 1.5f, avail) * (1f - 0.5f * tempOk) * 0.004f + path * 0.0015f;
        if (!p.covered && !d.surfaceSown)
        {
            rot += 0.0035f * (1f - 0.5f * AgriMath.Clamp01(avail));
            p.exposure = AgriMath.Clamp01(p.exposure + dt * 0.0035f);
        }
        p.viability *= AgriMath.Decay(rot, dt);
        if (p.progress < 1f) return;

        float dev = (p.depthCm - d.depthCm) / Mathf.Max(0.1f, d.depthToleranceCm);
        float depthF = Mathf.Exp(-0.5f * dev * dev);
        if (p.depthCm > d.depthCm + d.depthToleranceCm * 3.5f + 1f) depthF = 0f; // too deep: it never reaches the light
        if (d.surfaceSown && p.covered) depthF *= 0.15f;
        float tilthF = 0.45f + 0.55f * AgriMath.Smooth(0f, Mathf.Max(0.05f, d.minTilth), tilth);
        float compF = 1f - 0.7f * AgriMath.Smooth(d.compactionTolerance, 1f, comp);
        float stoneF = 1f - 0.5f * stones;
        float saltF = 1f - 0.9f * AgriMath.Smooth(d.salinityTolerance, d.salinityTolerance + 4f, sal);
        float moonF = 1f + (d.MoonGermination(p.plantedMoon) - 1f) * d.lunarStrength;
        float timeF = 1f;
        if (d.plantingTime != PlantingTime.Any)
        {
            bool right = (d.plantingTime == PlantingTime.Night) == p.plantedAtNight;
            timeF = right ? 1f + 0.25f * d.plantingTimeStrength : 1f - 0.45f * d.plantingTimeStrength;
        }
        float magicF = d.magicNeed > 0f ? AgriMath.Smooth(0f, d.magicNeed, magic) : 1f;
        float chance = AgriMath.Clamp01(p.viability * p.genes.Viability * depthF * tilthF * compF * stoneF * saltF * moonF * timeF * (0.3f + 0.7f * magicF));
        if (rng.Next() < chance)
        {
            p.stage = PlantStage.Seedling;
            p.progress = 0f;
            p.size = 0.03f;
            p.health = 1f;
            p.rootRadius = -1f;
            p.lastSeason = date.Season;
        }
        else
        {
            Die(p, true);
        }
    }

    // ---------------------------------------------------------------- stages
    private static bool Developing(PlantState p)
    {
        foreach (FruitState f in p.fruits)
            if (f.stage < ProduceStage.Mature) return true;
        return p.stage == PlantStage.Flowering;
    }

    private static float TargetSize(PlantState p, PlantSpeciesData d)
    {
        float max = p.genes.Factor(GeneTrait.MaxSize);
        float t;
        switch (p.stage)
        {
            case PlantStage.Seedling: t = Mathf.Lerp(0.03f, 0.1f, p.progress); break;
            case PlantStage.Juvenile: t = Mathf.Lerp(0.1f, 0.4f, p.progress); break;
            case PlantStage.Vegetative: t = Mathf.Lerp(0.4f, 1f, p.progress); break;
            default: t = 1f; break;
        }
        if (d.Perennial && d.yearsToFullSize > 0f && p.stage >= PlantStage.Vegetative)
            t = Mathf.Min(t, Mathf.Clamp((p.years + 0.5f) / d.yearsToFullSize, 0.4f, 1f));
        return t * max;
    }

    private static bool CanFlower(PlantState p, PlantSpeciesData d, AgriDate date)
    {
        ProduceSpec pr = d.Produce;
        if (pr.part == ProducePart.Root || (pr.part == ProducePart.Leaf)) return false;
        if (p.dormant || p.flushTimer > 0f) return false;
        if (!AgriDate.In(d.floweringSeasons, date.Season)) return false;
        if (d.photoperiod == PhotoperiodType.LongDay && date.DayLength < d.criticalDayLength) return false;
        if (d.photoperiod == PhotoperiodType.ShortDay && date.DayLength > d.criticalDayLength) return false;
        if (d.chillHours > 0f && p.chill < d.chillHours) return false;
        if (d.Perennial && p.years < d.yearsToFruit) return false;
        return p.flushes < Mathf.Max(1, pr.maxFlushes);
    }

    /// <summary>Leaf crops run to flower (bolt) in long days or heat: their leaves turn bitter.</summary>
    private static bool Bolts(PlantState p, PlantSpeciesData d, AgriDate date, float air)
    {
        if (p.flushTimer > 0f || p.dormant) return false;
        if (!AgriDate.In(d.floweringSeasons, date.Season) || d.floweringSeasons == SeasonMask.None) return false;
        if (d.photoperiod == PhotoperiodType.LongDay && date.DayLength >= d.criticalDayLength) return p.ageDays > d.juvenileDays + d.vegetativeDays;
        return air > d.tempHigh + 3f && p.ageDays > d.juvenileDays + d.vegetativeDays;
    }

    private static void StartFlush(PlantState p, PlantSpeciesData d, ref AgriRandom rng)
    {
        ProduceSpec pr = d.Produce;
        p.stage = PlantStage.Flowering;
        p.progress = 0f;
        p.flushes++;
        if (d.chillHours > 0f) p.chill = 0f;
        float quality = AgriMath.InverseLerp(0.35f, 0.9f, p.Quality);
        float support = d.needsSupport && !p.supported ? 0.7f : 1f;
        float n = Mathf.Lerp(pr.countMin, pr.countMax, quality) * p.genes.Factor(GeneTrait.FruitQuantity) * (0.45f + 0.55f * Mathf.Clamp01(p.size)) *
                  support * (1f + p.pruneBoost * 0.2f) * (1f - p.deadWood * 0.4f) * (0.85f + 0.3f * rng.Next());
        p.flowersLeft = Mathf.Clamp(Mathf.RoundToInt(n), 0, pr.countMax * 2);
        p.flowersTotal = p.flowersLeft;
    }

    /// <summary>
    /// How many flowers of the flush should be open at <paramref name="progress"/> through the flowering: the first at once,
    /// the rest evenly until the end. It depends on the progress alone, never on how long the steps are, so a garden
    /// stepped hour by hour and one caught up in longer steps open the same flowers by the same time.
    /// </summary>
    public static int FlowersOpenBy(int total, float progress) =>
        progress >= 1f ? total : Mathf.Clamp(Mathf.CeilToInt(total * progress - 1e-4f), 0, total);

    private static void Flowering(PlantState p, PlantSpeciesData d, AgriSettings s, float dt, float tDev, ref AgriRandom rng)
    {
        float hours = Mathf.Max(0.1f, d.floweringDays) * 24f / Mathf.Max(0.01f, s.growthSpeed);
        float before = p.progress;
        p.progress = Mathf.Min(1f, p.progress + dt / hours * Mathf.Max(0.3f, tDev));
        if (p.flowersLeft > 0 && !p.bolted)
        {
            // A flush saved before its size was kept: as many as opened so far at this pace, plus those still to open.
            if (p.flowersTotal < p.flowersLeft)
                p.flowersTotal = Mathf.Min(100000, Mathf.RoundToInt(p.flowersLeft / Mathf.Max(0.001f, 1f - before)));
            int opened = p.flowersTotal - p.flowersLeft;
            int open = p.progress >= 1f ? p.flowersLeft : Mathf.Clamp(FlowersOpenBy(p.flowersTotal, p.progress) - opened, 0, p.flowersLeft);
            for (int i = 0; i < open; i++) NewPiece(p, ref rng, 0f);
            p.flowersLeft -= open;
        }
        if (p.progress >= 1f && p.bolted)
        {
            if (d.Perennial)
            {
                // A perennial leaf crop flowers and goes on: leaves again, the bitterness gone after a while.
                p.stage = PlantStage.Vegetative;
                p.progress = 1f;
                p.bolted = false;
                p.flushTimer = 24f * 7f;
            }
            else
            {
                StartSenescence(p); // an annual leaf crop that bolted has gone to seed
            }
        }
    }

    private static void StartSenescence(PlantState p)
    {
        if (p.stage >= PlantStage.Senescence) return;
        p.stage = PlantStage.Senescence;
        p.progress = 0f;
        p.flowersLeft = 0;
    }

    /// <summary>A fruiting plant's stage follows its most advanced piece; with nothing left, the flush (or the plant) is over.</summary>
    private static void UpdateFruitingStage(PlantState p, PlantSpeciesData d, AgriDate date)
    {
        ProduceSpec pr = d.Produce;
        if (OnePiece(pr))
        {
            if (p.stage < PlantStage.Vegetative || p.stage >= PlantStage.Senescence || p.fruits.Count == 0) return;
            ProduceStage st = p.fruits[0].stage;
            p.stage = st >= ProduceStage.Rotten ? PlantStage.Overripe : st >= ProduceStage.Overripe ? PlantStage.Overripe
                : st >= ProduceStage.Ripe ? PlantStage.HarvestReady : st >= ProduceStage.Mature ? PlantStage.FruitMaturation : PlantStage.Vegetative;
            if (st == ProduceStage.Rotten && p.fruits[0].rotHours > 24f) StartSenescence(p);
            return;
        }
        if (pr.part == ProducePart.Leaf) return;
        bool fruiting = p.stage >= PlantStage.FruitDevelopment && p.stage <= PlantStage.Overripe;
        if (!fruiting && !(p.stage == PlantStage.Flowering && p.progress >= 1f)) return;
        int best = -1;
        bool buds = false;
        foreach (FruitState f in p.fruits)
        {
            if (f.stage == ProduceStage.Bud) { buds = true; continue; }
            if (f.stage < ProduceStage.Rotten && (int)f.stage > best) best = (int)f.stage;
        }
        if (best < 0)
        {
            if (buds || p.flowersLeft > 0)
            {
                if (fruiting) p.stage = PlantStage.FruitDevelopment;
                return;
            }
            EndFlush(p, d, date);
            return;
        }
        var b = (ProduceStage)best;
        p.stage = b >= ProduceStage.Overripe ? PlantStage.Overripe : b >= ProduceStage.Ripe ? PlantStage.HarvestReady
            : b >= ProduceStage.Mature ? PlantStage.FruitMaturation : PlantStage.FruitDevelopment;
    }

    /// <summary>The plant itself is the produce: one root, one head of leaves (it grows with the plant, no flowers).</summary>
    public static bool OnePiece(ProduceSpec pr) => pr.part == ProducePart.Root || (pr.part == ProducePart.Leaf && pr.wholePlant);

    private static void EndFlush(PlantState p, PlantSpeciesData d, AgriDate date)
    {
        ProduceSpec pr = d.Produce;
        if (p.flushes < Mathf.Max(1, pr.maxFlushes) && pr.flushIntervalDays > 0f && AgriDate.In(d.floweringSeasons, date.Season))
        {
            p.stage = PlantStage.Vegetative;
            p.progress = 1f;
            p.flushTimer = pr.flushIntervalDays * 24f;
        }
        else if (d.Perennial)
        {
            p.stage = PlantStage.Vegetative;
            p.progress = 1f;
            p.flushTimer = 0f;
        }
        else
        {
            StartSenescence(p);
        }
    }

    public static void Die(PlantState p, bool failedSeed)
    {
        p.stage = PlantStage.Dead;
        p.health = 0f;
        p.fruits.Clear();
        p.flowersLeft = 0;
        p.deadHours = failedSeed ? 1e6f : 0f;
        p.rootCells = null;
    }

    private static void NewPiece(PlantState p, ref AgriRandom rng, float slot)
    {
        var f = new FruitState
        {
            id = p.nextFruitId++,
            stage = ProduceStage.Bud,
            progress = 0f,
            pace = rng.Range(0.85f, 1.15f),
            slot = slot > 0f ? slot : rng.Next(),
            size = 0f,
        };
        ProducePart part = p.data != null ? p.data.Produce.part : ProducePart.Fruit;
        Pollination mode = p.data != null ? p.data.Produce.pollination : Pollination.Self;
        if (part != ProducePart.Fruit && part != ProducePart.Grain) f.pollinated = true;
        else if (mode == Pollination.None) f.pollinated = true;
        p.fruits.Add(f);
    }

    // ---------------------------------------------------------------- produce
    private static void StepProduce(FieldState field, PlantState p, PlantSpeciesData d, AgriSettings s, AgriWeather w, float dt, float tDev, float G,
        float fW, float suffK, float pollinatorBoost, ref AgriRandom rng)
    {
        if (p.fruits.Count == 0) return;
        ProduceSpec pr = d.Produce;
        bool whole = OnePiece(pr);
        float matGene = p.genes.Factor(GeneTrait.MaturationSpeed);
        float calcium = 1f - p.Symptom(PlantSymptom.DeadTips);
        float bitter = p.bolted && pr.part == ProducePart.Leaf ? 0.7f : 1f;
        float support = d.needsSupport && !p.supported ? 0.8f : 1f;
        float diseaseOnFruit = Mathf.Min(0.8f, p.Disease(DiseaseKind.Fungal) + p.Disease(DiseaseKind.Bacterial) + p.Disease(DiseaseKind.LeafSpot) * 0.5f);
        for (int i = p.fruits.Count - 1; i >= 0; i--)
        {
            FruitState f = p.fruits[i];
            if (f.stage == ProduceStage.Rotten)
            {
                f.rotHours += dt;
                if (f.rotHours > (pr.part == ProducePart.Leaf ? 12f : 24f) && !whole)
                {
                    p.fruits.RemoveAt(i);
                    if (field.Grid.TryIndex(p.position, out int c)) field.Grid.Add(c, SoilChannel.Pathogens, 0.01f);
                }
                continue;
            }
            float hours = pr.StageDays(f.stage) * 24f * f.pace / Mathf.Max(0.01f, s.growthSpeed) / Mathf.Max(0.2f, f.stage >= ProduceStage.Set ? matGene : 1f);
            float speed = f.stage >= ProduceStage.Mature ? Mathf.Lerp(0.4f, 1f, tDev) : tDev * (0.4f + 0.6f * Mathf.Min(1f, G));
            if (p.dormant) speed *= 0.05f;
            if (whole) speed *= Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, G));

            if (f.stage == ProduceStage.Bud && !f.pollinated)
            {
                TryPollinate(field, p, d, f, s, w, dt, pollinatorBoost, ref rng);
                f.progress += dt / hours * speed;
                if (f.progress >= 1.6f && !f.pollinated)
                {
                    p.fruits.RemoveAt(i); // the flower drops, never pollinated
                    continue;
                }
                if (f.progress >= 1f && !f.pollinated) continue;
            }
            else
            {
                f.progress += dt / hours * speed;
            }

            if (f.stage >= ProduceStage.Set && f.stage <= ProduceStage.Mature)
            {
                f.size += (1f - f.size) * (1f - AgriMath.Decay(0.06f * Mathf.Min(1f, G) * Mathf.Min(fW, 1f), dt));
                float q = Mathf.Pow(Mathf.Min(1f, G), 0.6f) * (0.5f + 0.5f * p.health) * (0.6f + 0.4f * Mathf.Min(fW, suffK)) *
                          (0.6f + 0.4f * calcium) * bitter * support * (1f - 0.5f * diseaseOnFruit);
                f.qualitySum += q * dt;
                f.qualityHours += dt;
            }
            if (f.stage >= ProduceStage.Ripe && f.size < 1f)
                f.size = Mathf.Min(1f, f.size + dt * 0.01f);

            while (f.progress >= 1f && f.stage < ProduceStage.Rotten)
            {
                f.progress -= 1f;
                f.stage++;
            }
            if (f.stage == ProduceStage.Overripe && !whole && rng.Chance(dt * 0.01f * (1f + w.wind / 10f)))
                p.fruits.RemoveAt(i); // fell from the plant
        }
    }

    private static void TryPollinate(FieldState field, PlantState p, PlantSpeciesData d, FruitState f, AgriSettings s, AgriWeather w, float dt, float boost, ref AgriRandom rng)
    {
        Pollination mode = d.Produce.pollination;
        float chance;
        switch (mode)
        {
            case Pollination.Self: chance = 0.08f; break;
            case Pollination.Insect:
            {
                var date = new AgriDate(p.last, s);
                float season = date.Season == Season.Winter ? 0.05f : date.Season == Season.Autumn ? 0.6f : 1f;
                bool weather = w.day && w.rain < 0.2f && w.temperature > 11f && w.temperature < 33f && w.wind < 9f;
                chance = weather ? 0.07f * season * s.pollinators * (1f + boost) : 0f;
                break;
            }
            case Pollination.Wind:
                chance = w.rain < 0.2f && w.wind > 0.8f && w.wind < 13f ? 0.06f * Mathf.Min(1f, w.wind / 3f) : 0f;
                break;
            case Pollination.None: chance = 1f; break;
            default: chance = 0f; break; // by hand only
        }
        if (!rng.Chance(chance * dt)) return;
        f.pollinated = true;
        if (mode != Pollination.Insect && mode != Pollination.Wind) return;
        // Insects and wind bring pollen from other flowering plants of the group now and then.
        PlantScratch tmp = donorScratch;
        field.Near(p.position, 5f, tmp.near, p);
        tmp.donors.Clear();
        foreach (PlantState q in tmp.near)
        {
            if (!q.Alive || q.data == null || q.stage != PlantStage.Flowering && !HasFlower(q)) continue;
            if (q.data.id == d.id || (!string.IsNullOrEmpty(d.crossGroup) && q.data.crossGroup == d.crossGroup))
                tmp.donors.Add(q);
        }
        if (tmp.donors.Count > 0 && rng.Chance(0.6f))
        {
            PlantState donor = tmp.donors[rng.Range(0, tmp.donors.Count)];
            f.crossed = true;
            f.donor = donor.genes;
            f.donorSpecies = donor.species;
        }
    }

    private static readonly PlantScratch donorScratch = new PlantScratch();

    private static bool HasFlower(PlantState q)
    {
        foreach (FruitState f in q.fruits)
            if (f.stage == ProduceStage.Bud) return true;
        return false;
    }

    // ---------------------------------------------------------------- water and food
    /// <summary>Drinks and feeds from the root cells; returns the water stress (0 = it got all it wanted).</summary>
    private static float Uptake(SoilGrid g, PlantState p, PlantSpeciesData d, AgriWeather w, double t, float dt, AgriSettings s, float coverEvap, PlantScratch sc)
    {
        int[] cells = p.rootCells;
        float[] weights = p.rootWeights;
        if (cells == null || cells.Length == 0) return 0f;
        float sumW = 0f;
        foreach (float x in weights) sumW += x;
        if (sumW <= 0f) return 0f;
        float active = p.dormant ? 0.1f : 1f;
        float rootHarm = 1f - 0.6f * Mathf.Min(1f, p.Disease(DiseaseKind.RootRot) + p.Disease(DiseaseKind.Wilt) * 0.5f + p.Pest(PestKind.RootParasites) * 0.6f);

        // Water: transpiration from the leaf area, as far as the root zone gives it.
        float radius = d.Look.width * 0.5f * Mathf.Max(0.05f, p.size);
        float leafArea = Mathf.PI * radius * radius * active;
        float demand = AgriEnvironment.Evaporation(w) * coverEvap * 1.1f * leafArea * d.waterUse * s.waterUse *
                       Mathf.Max(0.3f, 1f - 0.05f * p.genes.Level(GeneTrait.DroughtResistance)) * dt;
        float supplied = 0f;
        float[] v = g.Values;
        float cellArea = g.CellArea;
        if (demand > 0f)
            for (int k = 0; k < cells.Length; k++)
            {
                int o = cells[k] * SoilGrid.Stride;
                float ret = v[o + (int)SoilChannel.WaterRetention];
                float cap = SoilChemistry.CapacityMm(ret);
                float wp = SoilChemistry.WiltingPoint(ret);
                float m = v[o + (int)SoilChannel.Moisture];
                float litres = Mathf.Max(0f, m - wp) * cap * cellArea;
                float want = demand * weights[k] / sumW;
                float take = Mathf.Min(want, litres * 0.5f) * rootHarm;
                v[o + (int)SoilChannel.Moisture] = m - take / Mathf.Max(0.001f, cap * cellArea);
                supplied += take;
            }
        float stress = demand > 1e-6f ? AgriMath.Clamp01(1f - supplied / demand) : 0f;

        // Nutrients: what the stage needs, from where there is most of it.
        float grow = Mathf.Pow(Mathf.Max(0.02f, p.size), 0.9f) * Mass(d) * active * s.nutrientUse * p.genes.Factor(GeneTrait.NutrientEfficiency) * rootHarm;
        float kg = g.CellSoilKg;
        for (int n = 0; n < SoilChannels.NutrientCount; n++)
        {
            float mg = BaseUptake[n] * d.Need((Nutrient)n) * Emphasis((Nutrient)n, p.stage, d) * grow * dt;
            if (mg <= 0f) continue;
            float total = 0f;
            for (int k = 0; k < cells.Length; k++) total += weights[k] * v[cells[k] * SoilGrid.Stride + 1 + n];
            if (total <= 1e-6f) continue;
            // Roots take less of what is scarce: a hungry soil leaves a plant hungry, not stripped to nothing.
            float adequate = SoilChemistry.Adequate((Nutrient)n) * Mathf.Pow(Mathf.Max(0.05f, d.Need((Nutrient)n)), 0.6f);
            if (adequate > 0f) mg *= Mathf.Clamp01(total / sumW / adequate);
            for (int k = 0; k < cells.Length; k++)
            {
                int idx = cells[k] * SoilGrid.Stride + 1 + n;
                float share = mg * weights[k] * v[idx] / total;
                v[idx] = Mathf.Max(0f, v[idx] - share / kg);
            }
        }
        return stress;
    }

    /// <summary>How big a feeder the species is next to an average plant (by the reach of its roots): a tree drinks and eats far more than a carrot.</summary>
    public static float Mass(PlantSpeciesData d) => Mathf.Clamp(d.rootRadius * d.rootRadius / 0.04f, 0.2f, 10f);

    /// <summary>Shares <paramref name="total"/> (in the channel's unit, summed over cells) between the root cells, by weight.</summary>
    private static void SpreadTotal(SoilGrid g, PlantState p, SoilChannel c, float total)
    {
        if (p.rootCells == null || p.rootCells.Length == 0) return;
        float sumW = 0f;
        foreach (float x in p.rootWeights) sumW += x;
        if (sumW <= 0f) return;
        for (int k = 0; k < p.rootCells.Length; k++)
            g.Add(p.rootCells[k], c, total * p.rootWeights[k] / sumW);
    }

    /// <summary>Adds (or takes) <paramref name="perCell"/> to every root cell, more where the roots are thickest.</summary>
    private static void SpreadEach(SoilGrid g, PlantState p, SoilChannel c, float perCell)
    {
        if (p.rootCells == null || p.rootCells.Length == 0) return;
        float sumW = 0f;
        foreach (float x in p.rootWeights) sumW += x;
        if (sumW <= 0f) return;
        float scale = p.rootCells.Length / sumW;
        for (int k = 0; k < p.rootCells.Length; k++)
            g.Add(p.rootCells[k], c, perCell * p.rootWeights[k] * scale);
    }

    // ---------------------------------------------------------------- neighbours
    private static void Neighbourhood(FieldState f, PlantState p, PlantSpeciesData d, double t, PlantScratch sc,
        out float shade, out float crowd, out float growthMul, out float inhibit, out float pollinatorBoost)
    {
        shade = 0f; crowd = 0f; growthMul = 1f; inhibit = 0f; pollinatorBoost = 0f;
        Array.Clear(sc.repel, 0, 8);
        Array.Clear(sc.pestPressure, 0, 8);
        Array.Clear(sc.diseasePressure, 0, 6);
        Array.Clear(sc.suppression, 0, 8);
        f.Near(p.position, 5f, sc.near, p);
        float myH = d.Look.height * p.size;
        float myR = d.Look.width * 0.5f * p.size;
        foreach (PlantState q in sc.near)
        {
            if (!q.Emerged || q.data == null) continue;
            PlantSpeciesData qd = q.data;
            float dist = (q.position - p.position).magnitude;
            float sep = (d.spacing + qd.spacing) * 0.45f;
            if (dist < sep) crowd += (1f - dist / sep) * (0.4f + 0.6f * q.size);
            float qH = qd.Look.height * q.size * (q.dormant ? 0.3f : 1f), qR = qd.Look.width * 0.5f * q.size;
            if (qH > myH * 1.15f && dist < qR + myR)
                shade += (1f - dist / Mathf.Max(0.01f, qR + myR)) * Mathf.Min(1f, (qH - myH) / Mathf.Max(0.05f, qH)) * (0.3f + 0.7f * q.canopy) * 0.6f;
            if (qd.companions != null)
                foreach (CompanionRule r in qd.companions)
                {
                    if (r == null || dist > r.radius || !r.Applies(qd, d)) continue;
                    float prox = (1f - dist / r.radius) * Mathf.Clamp01(q.size * 1.5f);
                    switch (r.effect)
                    {
                        case CompanionEffect.Growth: growthMul *= Mathf.Lerp(1f, r.strength, prox); break;
                        case CompanionEffect.RepelPests:
                            if (r.pests != null)
                                foreach (PestKind k in r.pests) sc.repel[(int)k] = Mathf.Max(sc.repel[(int)k], AgriMath.Clamp01(r.strength * prox));
                            break;
                        case CompanionEffect.AttractPollinators: pollinatorBoost += r.strength * prox; break;
                        case CompanionEffect.Inhibit: inhibit += r.strength * prox; break;
                    }
                }
            float spreadP = Mathf.Max(0f, 1f - dist / 2f);
            if (spreadP > 0f)
                for (int k = 0; k < 8; k++) sc.pestPressure[k] += q.pests[k] * spreadP;
            float spreadD = Mathf.Max(0f, 1f - dist / 1.5f);
            if (spreadD > 0f)
                for (int k = 0; k < 6; k++)
                    if (q.diseases[k] > 0.05f) sc.diseasePressure[k] += q.diseases[k] * spreadD;
        }
        // Living helpers released nearby.
        foreach (BeneficialRelease b in f.Beneficials)
        {
            if (b.until < t || (b.center - p.position).sqrMagnitude > b.radius * b.radius) continue;
            SoilAmendmentData a = f.Catalog?.Amendment(b.amendment);
            if (a == null) continue;
            for (int k = 0; k < 8; k++) sc.suppression[k] = Mathf.Max(sc.suppression[k], a.Suppression((PestKind)k));
        }
    }

    // ---------------------------------------------------------------- pests
    private static void Pests(PlantState p, PlantSpeciesData d, AgriSettings s, AgriDate date, AgriWeather w, float air, float soilT, float avail, PlantScratch sc, ref AgriRandom rng, float dt)
    {
        float season = date.Season == Season.Summer ? 1.2f : date.Season == Season.Spring ? 0.8f : date.Season == Season.Autumn ? 0.7f : 0.05f;
        float active = AgriMath.Band(air, 6f, 16f, 30f, 38f);
        float lush = 1f + Mathf.Max(0f, 1f - p.Symptom(PlantSymptom.PaleOldLeaves)) * 0.5f;
        bool wet = w.rain > 0.1f || avail > 0.95f || w.humidity > 0.85f;
        float resist = Mathf.Max(0.3f, 1f - 0.08f * p.genes.Level(GeneTrait.DiseaseResistance));
        for (int k = 0; k < 8; k++)
        {
            var kind = (PestKind)k;
            float sus = d.PestSusceptibility(kind) * resist;
            float load = p.pests[k];
            if (sus <= 0f)
            {
                p.pests[k] = load * AgriMath.Decay(0.05f, dt);
                continue;
            }
            float likes;
            switch (kind)
            {
                case PestKind.Aphids: likes = AgriMath.Band(air, 8f, 14f, 26f, 32f) * lush; break;
                case PestKind.Caterpillars: likes = date.Season == Season.Summer ? 1.2f : 0.6f; break;
                case PestKind.Mites: likes = AgriMath.Smooth(22f, 30f, air) * (1f - AgriMath.Smooth(0.4f, 0.65f, w.humidity)) * 1.5f + 0.1f; break;
                case PestKind.Beetles: likes = AgriMath.Smooth(14f, 24f, air); break;
                case PestKind.SapSuckers: likes = AgriMath.Smooth(16f, 26f, air) * (1f - 0.5f * AgriMath.Smooth(0.6f, 0.85f, w.humidity)); break;
                case PestKind.RootParasites: likes = AgriMath.Smooth(12f, 20f, soilT) * AgriMath.Band(avail, 0.2f, 0.4f, 1f, 1.3f); break;
                case PestKind.Slugs: likes = (wet ? 1.5f : 0.15f) * AgriMath.Band(air, 3f, 8f, 20f, 26f) * (w.day ? 0.4f : 1.2f); break;
                default: likes = (wet ? 1.2f : 0.3f) * AgriMath.Band(air, 4f, 9f, 22f, 28f) * (w.day ? 0.5f : 1.1f); break;
            }
            float keepDown = (1f - sc.repel[k]) * (1f - sc.suppression[k]);
            if (load < 0.01f)
            {
                float arrival = (0.0005f * s.pestPressure * season * active * likes * sus * (1f + 2f * p.stress) * keepDown + sc.pestPressure[k] * 0.006f * s.spread * sus) * (p.dormant ? 0.1f : 1f);
                if (rng.Chance(arrival * dt)) p.pests[k] = 0.04f;
                else p.pests[k] = load * AgriMath.Decay(0.05f, dt);
                continue;
            }
            // A colony grows towards what the plant can feed (more on a weak plant); natural enemies keep a healthy garden in check.
            float r = PestGrowth[k] * active * Mathf.Min(1.6f, likes) * (1f - sc.suppression[k]) * (1f - 0.5f * sc.repel[k]) * (p.dormant ? 0.1f : 1f) * season;
            float carrying = Mathf.Clamp01((0.35f + 0.65f * p.stress) * Mathf.Min(1.5f, sus));
            load += r * load * (1f - load / Mathf.Max(0.05f, carrying)) * dt;
            float decline = 0.004f * p.health * (1f - p.stress) + 0.01f * (1f - active) +
                            (w.rain > 2f && (kind == PestKind.Aphids || kind == PestKind.Mites) ? 0.02f * w.rain : 0f) + sc.suppression[k] * 0.03f;
            load -= load * decline * dt;
            p.pests[k] = AgriMath.Clamp01(load);
        }
        p.wounds = AgriMath.Clamp01(p.wounds + (p.pests[(int)PestKind.Caterpillars] + p.pests[(int)PestKind.Beetles] + p.pests[(int)PestKind.Slugs] + p.pests[(int)PestKind.Snails]) * 0.01f * dt);
    }

    // ---------------------------------------------------------------- diseases
    private static void Diseases(PlantState p, PlantSpeciesData d, AgriSettings s, float air, float humidity, float avail, float soilPathogens, bool leafWet, float waterStress, PlantScratch sc, ref AgriRandom rng, float dt)
    {
        float res = Mathf.Max(0.1f, p.genes.Factor(GeneTrait.DiseaseResistance));
        for (int k = 0; k < 6; k++)
        {
            var kind = (DiseaseKind)k;
            float sus = d.DiseaseSusceptibility(kind) * res * s.diseasePressure;
            float favour;
            switch (kind)
            {
                case DiseaseKind.Fungal:
                    favour = AgriMath.Smooth(0.68f, 0.9f, humidity) * AgriMath.Band(air, 8f, 15f, 26f, 32f) * (0.5f + p.canopy) * (leafWet ? 1.5f : 1f);
                    break;
                case DiseaseKind.RootRot: favour = AgriMath.Smooth(1.05f, 1.4f, avail) * (0.3f + 2f * soilPathogens); break;
                case DiseaseKind.Wilt: favour = AgriMath.Smooth(20f, 30f, air) * (0.2f + 2f * soilPathogens) * (0.5f + waterStress); break;
                case DiseaseKind.LeafSpot: favour = (leafWet ? 1f : 0.15f) * AgriMath.Band(air, 10f, 18f, 28f, 34f); break;
                case DiseaseKind.Bacterial: favour = (0.1f + p.wounds) * (leafWet ? 1.5f : 0.3f) * AgriMath.Band(air, 12f, 22f, 32f, 38f); break;
                default: favour = (p.Pest(PestKind.Aphids) + p.Pest(PestKind.SapSuckers)) * 1.5f; break;
            }
            float sev = p.diseases[k];
            if (sev < 0.01f)
            {
                float infection = 0.0012f * sus * favour + sc.diseasePressure[k] * 0.006f * s.spread * sus;
                p.diseases[k] = rng.Chance(infection * dt) ? 0.05f : 0f;
                continue;
            }
            sev += DiseaseGrowth[k] * Mathf.Max(0.15f, favour) * Mathf.Min(1.5f, sus) * (0.2f + sev) * dt * (p.dormant ? 0.2f : 1f);
            if (kind != DiseaseKind.Viral && favour < 0.3f)
                sev -= 0.002f * p.health * (1f - favour) * dt;
            p.diseases[k] = AgriMath.Clamp01(sev);
        }
    }
}
