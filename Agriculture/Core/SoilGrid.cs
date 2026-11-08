using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The soil of a field as a grid of cells (a quarter of a metre by default), each with every <see cref="SoilChannel"/>.
/// <para><b>Lazy and cheap.</b> A cell is only worked out when something touches it (a plant's roots, a tool, an
/// instrument, the renderer near the camera): <see cref="Advance"/> brings it from the last time it was touched to now
/// in one step, with closed-form decays that are stable over any span. The weather is kept as running totals over the
/// whole field (rain, drying power, warmth), so a cell untouched for a week still gets exactly the rain of that week.
/// Cells nobody touches cost nothing.</para>
/// Coordinates are the field's local XZ plane (metres).
/// </summary>
public sealed class SoilGrid
{
    public const int Stride = SoilChannels.Count;
    public const int PendingSlots = 2;

    public readonly int Width, Height;
    public readonly float CellSize;
    /// <summary>Local XZ position of the corner of cell (0, 0).</summary>
    public readonly Vector2 Origin;
    public readonly float[] Values;
    /// <summary>Game time (hours) each cell was last brought up to date.</summary>
    public readonly double[] Last;
    /// <summary>The field's rain, drying and warmth totals when each cell was last brought up to date (3 per cell).</summary>
    public readonly double[] Marks;
    public readonly byte[] Types;
    /// <summary>Depth of an open hole in each cell, cm (0 = none).</summary>
    public readonly byte[] Holes;
    public readonly bool[] Inside;
    public int InsideCount { get; private set; }

    // What covers each cell (greenhouses, shade, sections' mulch): multipliers of rain, drying, light; degrees warmer; frost kept off (0-1).
    public readonly float[] CoverRain, CoverEvap, CoverLight, CoverWarmth, CoverFrost;
    /// <summary>Irrigated cells: the moisture they are kept at (0-255 = 0-1; 0 = not irrigated) and the water used.</summary>
    public readonly byte[] IrrigationTarget, IrrigationWater;
    /// <summary>Materials lying on top waiting to be worked in: amendment index + 1 (0 = empty slot) and portions per m².</summary>
    public readonly ushort[] PendingItem;
    public readonly float[] PendingAmount;

    /// <summary>Running totals over the field: rain reaching the ground (mm), the air's drying power (mm), the soil's warmth (warm hours).</summary>
    public double RainTotal, EvapTotal, WarmTotal;
    /// <summary>Resolves the amendment indices of <see cref="PendingItem"/>.</summary>
    public Func<int, SoilAmendmentData> Amendments;
    /// <summary>The water qualities used by irrigation.</summary>
    public AgriSettings Settings;
    /// <summary>Changes with every change a viewer should see (actions, not the slow drift of time).</summary>
    public int Version;

    public int Count => Width * Height;
    public float CellArea => CellSize * CellSize;
    /// <summary>Kilograms of root-zone soil in one cell.</summary>
    public float CellSoilKg => CellArea * SoilChemistry.RootZoneKgPerM2;

    public SoilGrid(int width, int height, float cellSize, Vector2 origin)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        CellSize = Mathf.Max(0.05f, cellSize);
        Origin = origin;
        int n = Width * Height;
        Values = new float[n * Stride];
        Last = new double[n];
        Marks = new double[n * 3];
        Types = new byte[n];
        Holes = new byte[n];
        Inside = new bool[n];
        CoverRain = Fill(n, 1f);
        CoverEvap = Fill(n, 1f);
        CoverLight = Fill(n, 1f);
        CoverWarmth = new float[n];
        CoverFrost = new float[n];
        IrrigationTarget = new byte[n];
        IrrigationWater = new byte[n];
        PendingItem = new ushort[n * PendingSlots];
        PendingAmount = new float[n * PendingSlots];
    }

    private static float[] Fill(int n, float v)
    {
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = v;
        return a;
    }

    public void SetInside(int i, bool inside)
    {
        if (Inside[i] == inside) return;
        Inside[i] = inside;
        InsideCount += inside ? 1 : -1;
    }

    // ---------------------------------------------------------------- geometry
    public int Index(int x, int y) => y * Width + x;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public Vector2 Center(int i) => new Vector2(Origin.x + (i % Width + 0.5f) * CellSize, Origin.y + (i / Width + 0.5f) * CellSize);

    public void Coords(Vector2 local, out int x, out int y)
    {
        x = Mathf.FloorToInt((local.x - Origin.x) / CellSize);
        y = Mathf.FloorToInt((local.y - Origin.y) / CellSize);
    }

    /// <summary>The cell under a local point, when it is part of the field.</summary>
    public bool TryIndex(Vector2 local, out int i)
    {
        Coords(local, out int x, out int y);
        i = InBounds(x, y) ? Index(x, y) : -1;
        return i >= 0 && Inside[i];
    }

    /// <summary>
    /// The field's cells within <paramref name="radius"/> of a point, each weighted by closeness (1 at the centre). With
    /// a radius smaller than a cell, the cell under the point. Returns the sum of the weights.
    /// </summary>
    public float CellsInRadius(Vector2 center, float radius, List<int> cells, List<float> weights)
    {
        cells.Clear();
        weights?.Clear();
        float sum = 0f;
        int r = Mathf.CeilToInt(radius / CellSize) + 1;
        Coords(center, out int cx, out int cy);
        float r2 = radius * radius;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!InBounds(x, y)) continue;
                int i = Index(x, y);
                if (!Inside[i]) continue;
                float d2 = (Center(i) - center).sqrMagnitude;
                if (d2 > r2) continue;
                float w = Mathf.Max(0.15f, 1f - Mathf.Sqrt(d2) / Mathf.Max(0.01f, radius));
                cells.Add(i);
                weights?.Add(w);
                sum += w;
            }
        if (cells.Count == 0 && TryIndex(center, out int own))
        {
            cells.Add(own);
            weights?.Add(1f);
            sum = 1f;
        }
        return sum;
    }

    // ---------------------------------------------------------------- values
    public float Get(int i, SoilChannel c) => Values[i * Stride + (int)c];
    public void Set(int i, SoilChannel c, float v) => Values[i * Stride + (int)c] = SoilChannels.Clamp(c, v);
    public void Add(int i, SoilChannel c, float dv) => Set(i, c, Get(i, c) + dv);
    public SoilType TypeAt(int i) => (SoilType)Types[i];
    public float Fertility(int i) => SoilChemistry.Fertility(Values, i * Stride);
    public float AvailableWater(int i) => SoilChemistry.Available(Get(i, SoilChannel.Moisture), Get(i, SoilChannel.WaterRetention));

    // ---------------------------------------------------------------- what a cell can give (kept until it changes)
    // Every plant reaching a cell works out, each step, how much of each nutrient the cell's pH lets through and where its
    // water starts and ends; crowded plants share their cells, so the same sums were done again and again. Each cell
    // keeps them with the very value they were worked out from (its pH, its water retention): when that value is
    // different - whoever changed it, the weather, a plant drinking, a tool - they are worked out again. Keyed by the value
    // itself rather than by a change counter, they can never be stale: the field's counter only follows actions, and
    // nothing at all counts a plant drinking.
    private float[] sharePh, shares, waterKey, waterWilt, waterSpan, waterCapacity;

    /// <summary>
    /// The share of each nutrient plants can take up at cell <paramref name="i"/>'s pH (0-1, as
    /// <see cref="SoilChemistry.Availability"/>): read <see cref="Shares"/> from the returned offset, one per nutrient.
    /// </summary>
    public int NutrientShares(int i)
    {
        if (shares == null)
        {
            shares = new float[Count * SoilChannels.NutrientCount];
            sharePh = Fill(Count, float.NaN);
        }
        float ph = Values[i * Stride + (int)SoilChannel.Ph];
        int o = i * SoilChannels.NutrientCount;
        if (sharePh[i] != ph) // NaN (never worked out) is different from everything
        {
            for (int n = 0; n < SoilChannels.NutrientCount; n++) shares[o + n] = SoilChemistry.Availability((Nutrient)n, ph);
            sharePh[i] = ph;
        }
        return o;
    }

    /// <summary>What <see cref="NutrientShares"/> fills.</summary>
    public float[] Shares => shares;

    /// <summary>
    /// Cell <paramref name="i"/>'s water figures: the moisture at its wilting point, the moisture between wilting point and
    /// field capacity (at least 0.05, as <see cref="SoilChemistry.Available"/> divides by it) and the millimetres it holds
    /// when saturated.
    /// </summary>
    public void WaterBudget(int i, out float wilt, out float span, out float capacityMm)
    {
        if (waterKey == null)
        {
            waterKey = Fill(Count, float.NaN);
            waterWilt = new float[Count];
            waterSpan = new float[Count];
            waterCapacity = new float[Count];
        }
        float retention = Values[i * Stride + (int)SoilChannel.WaterRetention];
        if (waterKey[i] != retention)
        {
            float wp = SoilChemistry.WiltingPoint(retention);
            waterWilt[i] = wp;
            waterSpan[i] = Mathf.Max(0.05f, SoilChemistry.FieldCapacity(retention) - wp);
            waterCapacity[i] = SoilChemistry.CapacityMm(retention);
            waterKey[i] = retention;
        }
        wilt = waterWilt[i];
        span = waterSpan[i];
        capacityMm = waterCapacity[i];
    }

    // ---------------------------------------------------------------- time
    /// <summary>Adds the weather of a span to the field's totals (rain mm reaching open ground, drying mm, warm hours).</summary>
    public void Accumulate(float rainMm, float evapMm, float warmHours)
    {
        RainTotal += Math.Max(0f, rainMm);
        EvapTotal += Math.Max(0f, evapMm);
        WarmTotal += Math.Max(0f, warmHours);
    }

    /// <summary>Brings every cell up to date (saving, tests; normally cells are brought up to date when touched).</summary>
    public void AdvanceAll(double now, AgriSettings s)
    {
        for (int i = 0; i < Count; i++)
            if (Inside[i]) Advance(i, now, s);
    }

    /// <summary>
    /// Brings one cell from the last time it was touched to <paramref name="now"/>: rain soaks in, water above field
    /// capacity drains away (taking nitrogen, potassium, sulfur, boron and salts with it), the surface dries, slow
    /// reserves and organic matter release their nutrients as the soil is warm and moist, pending pH changes react, the
    /// worked soil settles, mulch rots, materials lying on top work in, irrigation tops it up, soil diseases grow in
    /// waterlogged soil and die back elsewhere, magical essence returns to the soil's own level.
    /// </summary>
    // ---------------------------------------------------------------- the organic cycle
    /// <summary>Share of the organic matter that rots per warm hour (moist, near-neutral soil).</summary>
    public const float RotRate = 0.00004f;
    /// <summary>ppm of nutrient each point of organic matter (%) gives as it rots, into the slow reserves (sulfur straight into the soil).</summary>
    public const float NitrogenPerOm = 200f, PhosphorusPerOm = 20f, PotassiumPerOm = 10f, SulfurPerOm = 16f, BoronPerOm = 1.2f;
    /// <summary>Share of a slow reserve released per warm hour.</summary>
    public const float ReleaseN = 0.004f, ReleaseP = 0.002f, ReleaseK = 0.003f;
    /// <summary>What a slow reserve holds when it is fed as fast as it releases: ppm per point of (rotting) organic matter.</summary>
    public const float SlowNitrogenPerOm = RotRate * NitrogenPerOm / ReleaseN, SlowPhosphorusPerOm = RotRate * PhosphorusPerOm / ReleaseP,
        SlowPotassiumPerOm = RotRate * PotassiumPerOm / ReleaseK;

    /// <summary>How readily organic matter rots at a pH (acid peat hardly at all).</summary>
    public static float RotFactor(float ph) => AgriMath.Smooth(4.2f, 6.2f, ph);

    /// <summary>The part of the organic matter (%) that rots at the usual pace: beyond a garden's worth, most of it is old and tough.</summary>
    public static float Labile(float om) => om <= 8f ? om : 8f + (om - 8f) * 0.12f;

    public void Advance(int i, double now, AgriSettings s)
    {
        double t0 = Last[i];
        if (now <= t0 + 1e-6)
            return;
        Last[i] = now;
        s ??= Settings ?? AgriSettings.Defaults();
        float dt = (float)Math.Min(now - t0, 24.0 * 400.0);
        int m3 = i * 3;
        float dR = (float)Math.Max(0.0, RainTotal - Marks[m3]) * CoverRain[i];
        float dE = (float)Math.Max(0.0, EvapTotal - Marks[m3 + 1]) * CoverEvap[i] * s.evaporation;
        float warm = (float)Math.Pow(2.0, (SoilTypes.Warmth((SoilType)Types[i]) + CoverWarmth[i]) / 10.0);
        float dW = (float)Math.Max(0.0, WarmTotal - Marks[m3 + 2]) * s.mineralization * warm;
        Marks[m3] = RainTotal;
        Marks[m3 + 1] = EvapTotal;
        Marks[m3 + 2] = WarmTotal;

        // A long gap is caught up in even steps (a cell nobody touched for weeks costs a few steps, once): all its rain at
        // once would soak the soil, drain it and only then dry it - far more leaching than the same rain spread out.
        int steps = dt <= CatchUpStepHours ? 1 : Math.Min(MaxCatchUpSteps, (int)Math.Ceiling(dt / CatchUpStepHours));
        float part = 1f / steps;
        for (int k = 0; k < steps; k++)
            Step(i, s, dR * part, dE * part, dW * part, dt * part);

        // Open holes slump back in (rain washes them in fastest); whole millimetres, the rest by chance.
        if (Holes[i] > 0)
        {
            float slump = dR * 0.15f + dt * 0.02f;
            int whole = (int)slump;
            if (AgriMath.Noise(i, (int)(now * 4.0)) < slump - whole) whole++;
            Holes[i] = (byte)Mathf.Clamp(Holes[i] - whole, 0, 255);
        }
    }

    /// <summary>Hours of one catch-up step of <see cref="Advance"/>, and the most steps one call takes.</summary>
    public const float CatchUpStepHours = 6f;
    public const int MaxCatchUpSteps = 32;

    /// <summary>One step of a cell's life: rain, irrigation, drainage, drying, leaching, rotting, weathering, pH, settling.</summary>
    private void Step(int i, AgriSettings s, float dR, float dE, float dW, float dt)
    {
        float[] v = Values;
        int o = i * Stride;
        float retention = v[o + (int)SoilChannel.WaterRetention];
        float drainage = v[o + (int)SoilChannel.Drainage];
        float compaction = v[o + (int)SoilChannel.Compaction];
        float capacity = SoilChemistry.CapacityMm(retention);
        float fc = SoilChemistry.FieldCapacity(retention);
        float m = v[o + (int)SoilChannel.Moisture];
        float mulch = v[o + (int)SoilChannel.Mulch];

        // 1. Rain soaks in; packed, wet soil sheds some of it.
        if (dR > 0f)
        {
            float infiltration = 1f - 0.55f * compaction * m;
            m = Mathf.Min(1f, m + dR * infiltration / capacity);
        }

        // 2. Irrigation keeps the cell at its moisture.
        if (IrrigationTarget[i] > 0)
        {
            float target = IrrigationTarget[i] / 255f;
            if (m < target)
            {
                float mm = (target - m) * capacity;
                m = target;
                v[o + (int)SoilChannel.Moisture] = m;
                WaterEffects(o, mm, s.Water((WaterSourceKind)IrrigationWater[i]));
            }
        }

        // 3. Water above field capacity drains away.
        float before = m;
        if (m > fc)
        {
            float kd = (0.02f + 0.3f * drainage) * (1f - 0.6f * compaction);
            m = fc + (m - fc) * AgriMath.Decay(kd, dt);
        }
        float drained = (before - m) * capacity;

        // 4. The surface dries (slower under mulch).
        const float floor = 0.03f;
        if (m > floor && dE > 0f)
        {
            float rate = dE * (1f - 0.6f * mulch) / (capacity * Mathf.Max(0.05f, fc - floor));
            m = floor + (m - floor) * AgriMath.Decay(rate, 1f);
        }
        v[o + (int)SoilChannel.Moisture] = m;

        // 5. What drained took soluble things with it.
        if (drained > 0f)
        {
            float l = s.leaching;
            Scale(o, SoilChannel.Nitrogen, AgriMath.Decay(0.012f * l, drained));
            Scale(o, SoilChannel.Potassium, AgriMath.Decay(0.0025f * l * (1.5f - retention), drained));
            Scale(o, SoilChannel.Sulfur, AgriMath.Decay(0.006f * l, drained));
            Scale(o, SoilChannel.Boron, AgriMath.Decay(0.004f * l, drained));
            Scale(o, SoilChannel.Salinity, AgriMath.Decay(0.01f * l, drained));
            Scale(o, SoilChannel.Calcium, AgriMath.Decay(0.0006f * l, drained));
            Scale(o, SoilChannel.Magnesium, AgriMath.Decay(0.0006f * l, drained));
        }

        // 6. Waterlogged soil loses nitrogen to the air and breeds root diseases; drier soil lets them die back.
        float pathogens = v[o + (int)SoilChannel.Pathogens];
        if (m > 0.92f)
        {
            Scale(o, SoilChannel.Nitrogen, AgriMath.Decay(0.004f, dt));
            pathogens += (0.4f - pathogens) * (1f - AgriMath.Decay(0.002f, dt));
        }
        else
        {
            pathogens *= AgriMath.Decay(0.0012f, dt);
        }
        v[o + (int)SoilChannel.Pathogens] = AgriMath.Clamp01(pathogens);

        // 7. Slow reserves and organic matter release nutrients while the soil is warm and moist.
        float moistF = AgriMath.Band(m, 0.08f, 0.35f, 0.8f, 1.02f);
        float ph = v[o + (int)SoilChannel.Ph];
        if (dW > 0f && moistF > 0f)
        {
            Release(o, SoilChannel.SlowNitrogen, SoilChannel.Nitrogen, ReleaseN * dW * moistF);
            Release(o, SoilChannel.SlowPhosphorus, SoilChannel.Phosphorus, ReleaseP * dW * moistF);
            Release(o, SoilChannel.SlowPotassium, SoilChannel.Potassium, ReleaseK * dW * moistF);
            float om = v[o + (int)SoilChannel.OrganicMatter];
            // Peat barely rots: acidic and waterlogged, and most of so much organic matter is old and tough.
            float decomp = Labile(om) * (1f - AgriMath.Decay(RotRate * moistF * RotFactor(ph), dW));
            if (decomp > 0f)
            {
                // Rotting organic matter is the soil's own fertilizer (about a twentieth of it is nitrogen).
                v[o + (int)SoilChannel.OrganicMatter] = om - decomp;
                Add(i, SoilChannel.SlowNitrogen, decomp * NitrogenPerOm);
                Add(i, SoilChannel.SlowPhosphorus, decomp * PhosphorusPerOm);
                Add(i, SoilChannel.SlowPotassium, decomp * PotassiumPerOm);
                Add(i, SoilChannel.Sulfur, decomp * SulfurPerOm);
                // Most of a soil's boron is held in its organic matter.
                Add(i, SoilChannel.Boron, decomp * BoronPerOm);
            }
        }
        // A little sulfur comes down with the rain.
        if (dR > 0f) Add(i, SoilChannel.Sulfur, dR * 0.008f);

        // 7b. Minerals weather slowly out of clay, stones and ash: a little potassium, calcium, magnesium and iron come back by themselves.
        if (dW > 0f)
        {
            float mineral = (0.25f + retention + 0.5f * v[o + (int)SoilChannel.Stones] + ((SoilType)Types[i] == SoilType.Volcanic ? 1f : 0f)) * dW;
            Add(i, SoilChannel.Potassium, 0.012f * mineral);
            Add(i, SoilChannel.Calcium, 0.03f * mineral);
            Add(i, SoilChannel.Magnesium, 0.006f * mineral);
            Add(i, SoilChannel.Iron, 0.0004f * mineral);
            Add(i, SoilChannel.Phosphorus, 0.0006f * mineral);
        }

        // 8. A pH change under way reacts (faster in moist soil); rain sours the soil very slowly.
        float pending = v[o + (int)SoilChannel.PendingPh];
        if (Mathf.Abs(pending) > 1e-4f)
        {
            float applied = pending * (1f - AgriMath.Decay(0.006f * (0.3f + 0.7f * moistF), dt));
            ph += applied;
            v[o + (int)SoilChannel.PendingPh] = pending - applied;
        }
        ph -= dR * 0.00002f;
        v[o + (int)SoilChannel.Ph] = SoilChannels.Clamp(SoilChannel.Ph, ph);

        // 9. Worked soil settles; rain packs bare soil; mulch rots into organic matter.
        float tilth = v[o + (int)SoilChannel.Tilth] * AgriMath.Decay(0.0015f, dt) * AgriMath.Decay(0.002f, dR);
        v[o + (int)SoilChannel.Tilth] = tilth;
        v[o + (int)SoilChannel.Compaction] = AgriMath.Clamp01(compaction + dR * 0.0003f * (1f - tilth) * (1f - compaction) * (1f - mulch));
        if (mulch > 0f)
        {
            float rotted = mulch * (1f - AgriMath.Decay(0.004f * (0.3f + moistF), dt));
            v[o + (int)SoilChannel.Mulch] = mulch - rotted;
            Add(i, SoilChannel.OrganicMatter, rotted * 0.6f);
        }

        // 10. Magical essence drifts back to the soil's own level.
        float eq = SoilTypes.MagicLevel((SoilType)Types[i]);
        float magic = v[o + (int)SoilChannel.MagicEssence];
        v[o + (int)SoilChannel.MagicEssence] = AgriMath.Clamp01(eq + (magic - eq) * AgriMath.Decay(0.0004f, dt));

        // 11. What lies on top works in by itself, slowly (rain, worms).
        if (PendingItem[i * PendingSlots] != 0 || PendingItem[i * PendingSlots + 1] != 0)
            Incorporate(i, 1f - AgriMath.Decay(0.0012f * (0.3f + moistF), dt), false);
    }

    private void Scale(int o, SoilChannel c, float f) => Values[o + (int)c] *= f;

    private void Release(int o, SoilChannel from, SoilChannel to, float rate)
    {
        float pool = Values[o + (int)from];
        if (pool <= 0f) return;
        float r = pool * (1f - (float)Math.Exp(-rate));
        Values[o + (int)from] = pool - r;
        Values[o + (int)to] = SoilChannels.Clamp(to, Values[o + (int)to] + r);
    }

    // ---------------------------------------------------------------- water and materials
    /// <summary>Pours <paramref name="mm"/> of water (litres per m²) into a cell; returns what it took (the rest runs off).</summary>
    public float AddWater(int i, float mm, WaterQuality q)
    {
        if (mm <= 0f) return 0f;
        int o = i * Stride;
        float capacity = SoilChemistry.CapacityMm(Values[o + (int)SoilChannel.WaterRetention]);
        float m = Values[o + (int)SoilChannel.Moisture];
        float room = Mathf.Max(0f, 1f - m) * capacity;
        float taken = Mathf.Min(mm, room + 0.25f * mm);
        Values[o + (int)SoilChannel.Moisture] = Mathf.Min(1f, m + taken / capacity);
        WaterEffects(o, taken, q);
        Version++;
        return taken;
    }

    /// <summary>What <paramref name="mm"/> of a water carries into the soil.</summary>
    private void WaterEffects(int o, float mm, WaterQuality q)
    {
        float[] v = Values;
        float buffer = SoilChemistry.PhBuffer(v[o + (int)SoilChannel.WaterRetention], v[o + (int)SoilChannel.OrganicMatter]);
        v[o + (int)SoilChannel.Salinity] = SoilChannels.Clamp(SoilChannel.Salinity, v[o + (int)SoilChannel.Salinity] + q.salinity * mm * 0.004f);
        v[o + (int)SoilChannel.Calcium] = SoilChannels.Clamp(SoilChannel.Calcium, v[o + (int)SoilChannel.Calcium] + q.minerals * mm * 0.6f);
        v[o + (int)SoilChannel.Magnesium] = SoilChannels.Clamp(SoilChannel.Magnesium, v[o + (int)SoilChannel.Magnesium] + q.minerals * mm * 0.15f);
        v[o + (int)SoilChannel.PendingPh] = SoilChannels.Clamp(SoilChannel.PendingPh, v[o + (int)SoilChannel.PendingPh] + (q.ph - v[o + (int)SoilChannel.Ph]) * mm * 0.0004f / buffer);
        float p = v[o + (int)SoilChannel.Pathogens];
        v[o + (int)SoilChannel.Pathogens] = AgriMath.Clamp01(p + q.impurity * mm * 0.0015f * (1f - p));
        v[o + (int)SoilChannel.MagicEssence] = AgriMath.Clamp01(v[o + (int)SoilChannel.MagicEssence] + q.magic * mm * 0.004f);
        v[o + (int)SoilChannel.Nitrogen] = SoilChannels.Clamp(SoilChannel.Nitrogen, v[o + (int)SoilChannel.Nitrogen] + q.nitrogen * mm);
    }

    /// <summary>
    /// Puts a material on a cell at <paramref name="portionsPerM2"/>: it acts at once when it is a liquid, when it does not
    /// need working in or when <paramref name="workedIn"/>; otherwise it lies on top until worked in.
    /// </summary>
    public void Apply(int i, int amendmentIndex, SoilAmendmentData a, float portionsPerM2, bool workedIn)
    {
        if (a == null || portionsPerM2 <= 0f) return;
        Version++;
        if (a.needsWorkingIn && !a.liquid && !workedIn)
        {
            AddPending(i, amendmentIndex, portionsPerM2);
            return;
        }
        ApplyEffects(i, a, portionsPerM2);
    }

    /// <summary>Applies a material's effects to a cell (<paramref name="density"/> = portions per m²).</summary>
    public void ApplyEffects(int i, SoilAmendmentData a, float density)
    {
        if (a == null || density <= 0f) return;
        int o = i * Stride;
        if (a.effects != null)
            foreach (SoilEffect e in a.effects)
            {
                SoilChannel c = e.channel;
                if (a.slowRelease)
                {
                    if (c == SoilChannel.Nitrogen) c = SoilChannel.SlowNitrogen;
                    else if (c == SoilChannel.Phosphorus) c = SoilChannel.SlowPhosphorus;
                    else if (c == SoilChannel.Potassium) c = SoilChannel.SlowPotassium;
                }
                if ((int)c < Stride)
                    Add(i, c, e.amount * density);
            }
        if (Mathf.Abs(a.phEffect) > 1e-6f)
        {
            float buffer = SoilChemistry.PhBuffer(Values[o + (int)SoilChannel.WaterRetention], Values[o + (int)SoilChannel.OrganicMatter]);
            Add(i, SoilChannel.PendingPh, a.phEffect * density / buffer);
        }
        if (a.mulch > 0f)
            Add(i, SoilChannel.Mulch, a.mulch * density);
        if (a.liquid)
            Add(i, SoilChannel.Moisture, 0.002f * density * a.portionKg / Mathf.Max(0.05f, CellArea));
    }

    private void AddPending(int i, int index, float amount)
    {
        int b = i * PendingSlots;
        ushort id = (ushort)(index + 1);
        for (int k = 0; k < PendingSlots; k++)
            if (PendingItem[b + k] == id)
            {
                PendingAmount[b + k] += amount;
                return;
            }
        for (int k = 0; k < PendingSlots; k++)
            if (PendingItem[b + k] == 0)
            {
                PendingItem[b + k] = id;
                PendingAmount[b + k] = amount;
                return;
            }
        // Both slots taken by other materials: the oldest is pressed in half-way, the rest of it is lost, and the new one takes its place.
        SoilAmendmentData old = Amendments?.Invoke(PendingItem[b] - 1);
        ApplyEffects(i, old, PendingAmount[b] * 0.5f);
        PendingItem[b] = PendingItem[b + 1];
        PendingAmount[b] = PendingAmount[b + 1];
        PendingItem[b + 1] = id;
        PendingAmount[b + 1] = amount;
    }

    /// <summary>Works a fraction of what lies on top into the soil.</summary>
    public void Incorporate(int i, float fraction, bool bumpVersion = true)
    {
        fraction = AgriMath.Clamp01(fraction);
        if (fraction <= 0f) return;
        int b = i * PendingSlots;
        for (int k = 0; k < PendingSlots; k++)
        {
            if (PendingItem[b + k] == 0) continue;
            SoilAmendmentData a = Amendments?.Invoke(PendingItem[b + k] - 1);
            float amount = PendingAmount[b + k] * fraction;
            ApplyEffects(i, a, amount);
            PendingAmount[b + k] -= amount;
            if (PendingAmount[b + k] < 0.001f || a == null)
            {
                PendingItem[b + k] = 0;
                PendingAmount[b + k] = 0f;
            }
        }
        if (bumpVersion) Version++;
    }

    public bool HasPending(int i) => PendingItem[i * PendingSlots] != 0 || PendingItem[i * PendingSlots + 1] != 0;

    /// <summary>The material lying on top of a cell (the larger portion), or -1.</summary>
    public int PendingTop(int i, out float amount)
    {
        int b = i * PendingSlots;
        int best = -1;
        amount = 0f;
        for (int k = 0; k < PendingSlots; k++)
            if (PendingItem[b + k] != 0 && PendingAmount[b + k] > amount)
            {
                amount = PendingAmount[b + k];
                best = PendingItem[b + k] - 1;
            }
        return best;
    }

    /// <summary>
    /// One stroke of a hoe (or spade) over a cell: loosens it, breaks up the crust, works in what lies on top and the mulch,
    /// fills holes. <paramref name="strength"/> 0-1 is how much one stroke does.
    /// </summary>
    public void Till(int i, float strength)
    {
        strength = AgriMath.Clamp01(strength);
        int o = i * Stride;
        float tilth = Values[o + (int)SoilChannel.Tilth];
        Values[o + (int)SoilChannel.Tilth] = AgriMath.Clamp01(tilth + strength * (1f - tilth));
        float c = Values[o + (int)SoilChannel.Compaction];
        Values[o + (int)SoilChannel.Compaction] = AgriMath.Clamp01(c - strength * 0.5f * c);
        float mulch = Values[o + (int)SoilChannel.Mulch];
        if (mulch > 0f)
        {
            float mixed = mulch * strength;
            Values[o + (int)SoilChannel.Mulch] = mulch - mixed;
            Add(i, SoilChannel.OrganicMatter, mixed * 0.4f);
        }
        Incorporate(i, strength * 3f, false);
        Holes[i] = 0;
        Version++;
    }

    // ---------------------------------------------------------------- saving
    /// <summary>
    /// 2: also what covers each cell (greenhouses, shade, cold frames), so a garden caught up from its save while its
    /// structures are away (hidden with their realm, not loaded yet) still lives under them. 1: the soil alone (still
    /// read). The irrigation is not saved: it is the layout's (made again by its bake).
    /// </summary>
    private const int SaveVersion = 2;

    /// <summary>A cell open to the sky (nothing covers it).</summary>
    public bool Open(int i) => CoverRain[i] == 1f && CoverEvap[i] == 1f && CoverLight[i] == 1f && CoverWarmth[i] == 0f && CoverFrost[i] == 0f;

    /// <summary>Uncovers every cell (open sky).</summary>
    public void ClearCovers()
    {
        for (int i = 0; i < Count; i++)
        {
            CoverRain[i] = 1f;
            CoverEvap[i] = 1f;
            CoverLight[i] = 1f;
            CoverWarmth[i] = 0f;
            CoverFrost[i] = 0f;
        }
    }

    public void Write(BinaryWriter w)
    {
        w.Write(SaveVersion);
        w.Write(Width);
        w.Write(Height);
        w.Write(Stride);
        w.Write(RainTotal);
        w.Write(EvapTotal);
        w.Write(WarmTotal);
        int n = Count, covered = 0;
        for (int i = 0; i < n; i++)
        {
            if (!Inside[i]) continue;
            int o = i * Stride;
            for (int c = 0; c < Stride; c++) w.Write(Values[o + c]);
            w.Write(Last[i]);
            w.Write(Marks[i * 3]);
            w.Write(Marks[i * 3 + 1]);
            w.Write(Marks[i * 3 + 2]);
            w.Write(Types[i]);
            w.Write(Holes[i]);
            for (int k = 0; k < PendingSlots; k++)
            {
                w.Write(PendingItem[i * PendingSlots + k]);
                w.Write(PendingAmount[i * PendingSlots + k]);
            }
            if (!Open(i)) covered++;
        }
        // The covered cells only (most are open to the sky).
        w.Write(covered);
        for (int i = 0; i < n; i++)
        {
            if (!Inside[i] || Open(i)) continue;
            w.Write(i);
            w.Write(CoverRain[i]);
            w.Write(CoverEvap[i]);
            w.Write(CoverLight[i]);
            w.Write(CoverWarmth[i]);
            w.Write(CoverFrost[i]);
        }
    }

    /// <summary>
    /// Reads what <see cref="Write"/> wrote into this grid; false when it does not fit (another shape). A save of version 1
    /// leaves the covers as they are (open sky until the structures are looked at).
    /// </summary>
    public bool Read(BinaryReader r)
    {
        int version = r.ReadInt32();
        if (version < 1 || version > SaveVersion) return false;
        int w = r.ReadInt32(), h = r.ReadInt32(), stride = r.ReadInt32();
        if (w != Width || h != Height || stride != Stride) return false;
        double rain = r.ReadDouble(), evap = r.ReadDouble(), warm = r.ReadDouble();
        var values = new float[Values.Length];
        Array.Copy(Values, values, values.Length);
        int n = Count;
        for (int i = 0; i < n; i++)
        {
            if (!Inside[i]) continue;
            int o = i * Stride;
            for (int c = 0; c < Stride; c++) values[o + c] = r.ReadSingle();
            Last[i] = r.ReadDouble();
            Marks[i * 3] = r.ReadDouble();
            Marks[i * 3 + 1] = r.ReadDouble();
            Marks[i * 3 + 2] = r.ReadDouble();
            Types[i] = r.ReadByte();
            Holes[i] = r.ReadByte();
            for (int k = 0; k < PendingSlots; k++)
            {
                PendingItem[i * PendingSlots + k] = r.ReadUInt16();
                PendingAmount[i * PendingSlots + k] = r.ReadSingle();
            }
        }
        if (version >= 2)
        {
            ClearCovers();
            int covered = r.ReadInt32();
            if (covered < 0 || covered > n) throw new InvalidDataException("broken covers");
            for (int k = 0; k < covered; k++)
            {
                int i = r.ReadInt32();
                if (i < 0 || i >= n) throw new InvalidDataException("broken covers");
                CoverRain[i] = r.ReadSingle();
                CoverEvap[i] = r.ReadSingle();
                CoverLight[i] = r.ReadSingle();
                CoverWarmth[i] = r.ReadSingle();
                CoverFrost[i] = r.ReadSingle();
            }
        }
        Array.Copy(values, Values, values.Length);
        RainTotal = rain;
        EvapTotal = evap;
        WarmTotal = warm;
        Version++;
        return true;
    }
}
