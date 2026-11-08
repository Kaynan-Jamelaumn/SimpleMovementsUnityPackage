using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Two species of one cross group that, crossed, sometimes give seeds of a third.</summary>
[Serializable]
public class HybridRule
{
    [Tooltip("One parent (species id).")]
    public string parentA = "";
    [Tooltip("The other parent (species id).")]
    public string parentB = "";
    [Tooltip("The species the seeds of the cross may be.")]
    public string child = "";
    [Tooltip("Chance a seed of the cross is the new species.")]
    [Range(0f, 1f)] public float chance = 0.2f;

    public bool Matches(string a, string b) =>
        (string.Equals(a, parentA, StringComparison.OrdinalIgnoreCase) && string.Equals(b, parentB, StringComparison.OrdinalIgnoreCase)) ||
        (string.Equals(a, parentB, StringComparison.OrdinalIgnoreCase) && string.Equals(b, parentA, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Where the rules find species, materials and hybrids (the Agriculture Database at runtime, the built-in content in tests).</summary>
public interface IAgriCatalog
{
    AgriSettings Settings { get; }
    PlantSpeciesData Species(string id);
    IReadOnlyList<PlantSpeciesData> AllSpecies { get; }
    int AmendmentIndex(string id);
    SoilAmendmentData Amendment(int index);
    SoilAmendmentData Amendment(string id);
    IReadOnlyList<HybridRule> Hybrids { get; }
}

/// <summary>A catalog held in memory (tests, tools, the built-in content).</summary>
public sealed class AgriCatalog : IAgriCatalog
{
    private readonly List<PlantSpeciesData> species = new List<PlantSpeciesData>();
    private readonly Dictionary<string, PlantSpeciesData> byId = new Dictionary<string, PlantSpeciesData>(StringComparer.OrdinalIgnoreCase);
    private readonly List<SoilAmendmentData> amendments = new List<SoilAmendmentData>();
    private readonly Dictionary<string, int> amendmentIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly List<HybridRule> hybrids = new List<HybridRule>();

    public AgriSettings Settings { get; set; } = AgriSettings.Defaults();
    public IReadOnlyList<PlantSpeciesData> AllSpecies => species;
    public IReadOnlyList<HybridRule> Hybrids => hybrids;
    public IReadOnlyList<SoilAmendmentData> AllAmendments => amendments;

    public void Add(PlantSpeciesData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id) || byId.ContainsKey(s.id)) return;
        species.Add(s);
        byId[s.id] = s;
    }

    public void Add(SoilAmendmentData a)
    {
        if (a == null || string.IsNullOrWhiteSpace(a.id) || amendmentIndex.ContainsKey(a.id)) return;
        amendmentIndex[a.id] = amendments.Count;
        amendments.Add(a);
    }

    public void Add(HybridRule h)
    {
        if (h != null) hybrids.Add(h);
    }

    public PlantSpeciesData Species(string id) => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out PlantSpeciesData s) ? s : null;
    public int AmendmentIndex(string id) => !string.IsNullOrEmpty(id) && amendmentIndex.TryGetValue(id, out int i) ? i : -1;
    public SoilAmendmentData Amendment(int index) => index >= 0 && index < amendments.Count ? amendments[index] : null;
    public SoilAmendmentData Amendment(string id) => Amendment(AmendmentIndex(id));

    /// <summary>The built-in species, materials and hybrids.</summary>
    public static AgriCatalog BuiltIn()
    {
        var c = new AgriCatalog();
        foreach (PlantSpeciesData s in AgriDefaults.Species()) c.Add(s);
        foreach (SoilAmendmentData a in AgriDefaults.Amendments()) c.Add(a);
        foreach (HybridRule h in AgriDefaults.Hybrids()) c.Add(h);
        return c;
    }
}

/// <summary>The weather over a field at a time.</summary>
public interface IAgriClimateSource
{
    AgriWeather WeatherAt(double time);
    /// <summary>It knows the weather of any hour (the city's climate); a live Weather System only knows now.</summary>
    bool Historic { get; }
}

/// <summary>The city's climate (<see cref="AgriEnvironment.Climate"/>).</summary>
public sealed class CityClimateSource : IAgriClimateSource
{
    private readonly AgriSettings settings;
    public CityClimateSource(AgriSettings s) { settings = s ?? AgriSettings.Defaults(); }
    public AgriWeather WeatherAt(double time) => AgriEnvironment.Climate(settings, time);
    public bool Historic => true;
}

/// <summary>
/// Everything one field is while it lives: its soil, its plants (with a one-metre grid of buckets to find neighbours),
/// the living helpers released on it, and the weather it has had. Pure C# (the <see cref="SoilField"/> component owns one;
/// tests make their own).
/// </summary>
public sealed class FieldState
{
    public const float BucketSize = 1f;

    public readonly SoilGrid Grid;
    public readonly List<PlantState> Plants = new List<PlantState>();
    public readonly List<BeneficialRelease> Beneficials = new List<BeneficialRelease>();
    public IAgriCatalog Catalog;
    /// <summary>Mixes the field's randomness (the same field, plants and time give the same results).</summary>
    public int Seed;
    public int NextId = 1;
    /// <summary>Changes when a plant is added, removed or changes visibly.</summary>
    public int PlantVersion;
    /// <summary>
    /// Changes when a plant is added or removed, comes up, dies, rests or wakes, or catches or loses pests or a disease:
    /// what its neighbours are to a plant. A plant goes on with what it last saw of them (a quiet stretch of a catch-up)
    /// only while this is unchanged.
    /// </summary>
    public int QuietEpoch;

    // The weather it has had.
    public double EnvTime = -1.0;
    public AgriWeather Weather;
    public float SoilTemperature = 12f;
    /// <summary>Hours the leaves stay wet after rain.</summary>
    public float WetLeaves;
    /// <summary>Light gathered today and yesterday (full-sun hours).</summary>
    public float TodayLight, YesterdayLight;
    public int LightDay = -1;

    private readonly List<PlantState>[] buckets;
    private readonly int bucketsX, bucketsY;
    private readonly Vector2 bucketOrigin;
    private readonly Dictionary<int, PlantState> byId = new Dictionary<int, PlantState>();

    public FieldState(SoilGrid grid, IAgriCatalog catalog, int seed)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Catalog = catalog;
        Seed = seed;
        bucketOrigin = grid.Origin;
        bucketsX = Mathf.Max(1, Mathf.CeilToInt(grid.Width * grid.CellSize / BucketSize));
        bucketsY = Mathf.Max(1, Mathf.CeilToInt(grid.Height * grid.CellSize / BucketSize));
        buckets = new List<PlantState>[bucketsX * bucketsY];
        if (catalog != null)
        {
            grid.Amendments = catalog.Amendment;
            grid.Settings = catalog.Settings;
        }
    }

    public AgriSettings Settings => Catalog?.Settings ?? AgriSettings.Defaults();

    // ---------------------------------------------------------------- plants
    private int BucketOf(Vector2 p)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((p.x - bucketOrigin.x) / BucketSize), 0, bucketsX - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((p.y - bucketOrigin.y) / BucketSize), 0, bucketsY - 1);
        return y * bucketsX + x;
    }

    /// <summary>Adds a plant (giving it an id when it has none) and finds its species.</summary>
    public PlantState Add(PlantState p)
    {
        if (p == null) return null;
        p.Repair();
        if (p.id <= 0 || byId.ContainsKey(p.id)) p.id = NextId;
        NextId = Math.Max(NextId, p.id + 1);
        p.data = Catalog?.Species(p.species);
        Plants.Add(p);
        byId[p.id] = p;
        int b = BucketOf(p.position);
        p.bucket = b;
        (buckets[b] ??= new List<PlantState>()).Add(p);
        p.rootRadius = -1f;
        p.surroundings = null;
        PlantVersion++;
        QuietEpoch++;
        return p;
    }

    public void Remove(PlantState p)
    {
        if (p == null || !byId.Remove(p.id)) return;
        Plants.Remove(p);
        if (p.bucket >= 0 && p.bucket < buckets.Length) buckets[p.bucket]?.Remove(p);
        p.bucket = -1;
        PlantVersion++;
        QuietEpoch++;
    }

    public PlantState Find(int id) => byId.TryGetValue(id, out PlantState p) ? p : null;

    /// <summary>The dead that have lain longer than <paramref name="limitHours"/> are gone (how many).</summary>
    public int SweepDead(float limitHours)
    {
        int removed = 0;
        for (int i = Plants.Count - 1; i >= 0; i--)
        {
            PlantState p = Plants[i];
            if (p != null && !p.Alive && p.deadHours > limitHours)
            {
                Remove(p);
                removed++;
            }
        }
        return removed;
    }

    /// <summary>The plants within <paramref name="radius"/> of a point (dead ones too; not <paramref name="except"/>).</summary>
    public void Near(Vector2 p, float radius, List<PlantState> into, PlantState except = null)
    {
        into.Clear();
        int r = Mathf.CeilToInt(radius / BucketSize);
        int cx = Mathf.FloorToInt((p.x - bucketOrigin.x) / BucketSize), cy = Mathf.FloorToInt((p.y - bucketOrigin.y) / BucketSize);
        float r2 = radius * radius;
        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(bucketsY - 1, cy + r); y++)
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(bucketsX - 1, cx + r); x++)
            {
                List<PlantState> list = buckets[y * bucketsX + x];
                if (list == null) continue;
                foreach (PlantState q in list)
                    if (!ReferenceEquals(q, except) && (q.position - p).sqrMagnitude <= r2)
                        into.Add(q);
            }
    }

    /// <summary>The nearest living plant (or seed) within <paramref name="radius"/>, or null.</summary>
    public PlantState Nearest(Vector2 p, float radius, bool seeds = true)
    {
        var tmp = scratch;
        Near(p, radius, tmp);
        PlantState best = null;
        float bestD = float.MaxValue;
        foreach (PlantState q in tmp)
        {
            if (!q.Alive || (!seeds && q.IsSeed)) continue;
            float d = (q.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = q; }
        }
        return best;
    }

    private readonly List<PlantState> scratch = new List<PlantState>();

    // ---------------------------------------------------------------- the weather
    /// <summary>
    /// Adds the weather from the last sample to <paramref name="now"/> to the soil's totals, in steps of
    /// <see cref="AgriSettings.environmentStepHours"/> (a live Weather System that only knows now stands in for the last two
    /// hours; the city's climate fills in anything older).
    /// </summary>
    public void TickEnvironment(double now, IAgriClimateSource source)
    {
        AgriSettings s = Settings;
        if (EnvTime < 0.0)
        {
            EnvTime = now;
            Weather = source != null ? source.WeatherAt(now) : AgriEnvironment.Climate(s, now);
            SoilTemperature = Weather.temperature;
            return;
        }
        double step = Math.Max(0.05, s.environmentStepHours);
        double gap = now - EnvTime;
        if (gap <= 1e-6) return;
        if (gap > step * 4000) step = gap / 4000.0;
        while (EnvTime + 1e-9 < now)
        {
            double dt = Math.Min(step, now - EnvTime);
            double mid = EnvTime + dt * 0.5;
            AgriWeather w = source == null ? AgriEnvironment.Climate(s, mid)
                : source.Historic || now - mid <= 2.0 ? source.WeatherAt(source.Historic ? mid : now)
                : AgriEnvironment.Climate(s, mid);
            float hours = (float)dt;
            Grid.Accumulate(w.rain * hours, AgriEnvironment.Evaporation(w) * hours, AgriEnvironment.Warmth(SoilTemperature) * hours);
            SoilTemperature += (w.temperature - SoilTemperature) * (1f - AgriMath.Decay(1f / 14f, hours));
            WetLeaves = w.rain > 0.1f ? 3f : Mathf.Max(0f, WetLeaves - hours * (0.5f + w.light + w.wind * 0.1f));
            int day = (int)Math.Floor(mid / 24.0);
            if (day != LightDay)
            {
                YesterdayLight = LightDay == day - 1 ? TodayLight : 0f;
                TodayLight = 0f;
                LightDay = day;
            }
            if (w.day) TodayLight += w.light * hours;
            Weather = w;
            EnvTime += dt;
        }
    }

    // ---------------------------------------------------------------- saving
    public PlantStateList Export()
    {
        var l = new PlantStateList { nextId = NextId };
        l.plants.AddRange(Plants);
        l.beneficials.AddRange(Beneficials);
        return l;
    }

    public void Import(PlantStateList list)
    {
        foreach (PlantState p in Plants.ToArray()) Remove(p);
        Beneficials.Clear();
        if (list == null) return;
        NextId = Math.Max(1, list.nextId);
        if (list.plants != null)
            foreach (PlantState p in list.plants)
                if (p != null && Grid.TryIndex(p.position, out _))
                    Add(p);
        if (list.beneficials != null) Beneficials.AddRange(list.beneficials);
        PlantVersion++;
    }
}
