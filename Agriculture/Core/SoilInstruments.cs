using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>What an instrument showed.</summary>
public class SoilReading
{
    public string title = "";
    public List<string> lines = new List<string>();

    /// <summary>The reading as one notebook line.</summary>
    public string Note()
    {
        var sb = new StringBuilder(title);
        foreach (string l in lines)
        {
            sb.Append(sb.Length > 0 ? " · " : "").Append(l);
            if (sb.Length > 500) break;
        }
        return sb.ToString();
    }
}

/// <summary>
/// The instruments: each reads what it reads, as well as it can. A crude instrument only sorts into rough classes and
/// can be wrong near their edges; a precise one gives numbers with a small error; every instrument has its own small
/// lean (two probes of the same kind disagree a little, always the same way); skill (Botany for the air, light and
/// water, Alchemy for the analyses, Agriculture for the soil's body) narrows the error. The same instrument at the same
/// spot within the same hour reads the same - waiting and coming back gives a fresh reading.
/// </summary>
public static class SoilInstruments
{
    public static SoilReading Read(FieldState f, int cell, PlantState plant, InstrumentProfile ins, FarmWorker w, AgriSettings s, double now, AgriDate date)
    {
        SoilGrid g = f.Grid;
        var r = new SoilReading();
        float q = Mathf.Clamp01(ins.quality);
        int idHash = AgriMath.Hash(ins.id ?? ins.kind.ToString());
        int salt = AgriMath.Hash($"{f.Seed}/{cell}/{(int)Math.Floor(now)}/{w.key}");
        float skill;
        switch (ins.kind)
        {
            case InstrumentKind.SoilAnalysisKit:
            case InstrumentKind.AdvancedAnalyzer: skill = AgriSkills.Factor(s, w.skills, AgriSkill.Alchemy); break;
            case InstrumentKind.CompactionTester:
            case InstrumentKind.DepthGauge: skill = AgriSkills.Factor(s, w.skills, AgriSkill.Agriculture); break;
            default: skill = AgriSkills.Factor(s, w.skills, AgriSkill.Botany); break;
        }
        float spread = (1.4f - q) * (1f - 0.45f * skill);
        bool crude = q < 0.35f;
        float Noise(int channel, float sigma) => AgriMath.Gauss(salt, idHash, channel) * sigma * spread + AgriMath.Signed(idHash, channel, 77) * sigma * (1f - q) * 0.8f;

        int o = cell * SoilGrid.Stride;
        float[] v = g.Values;
        switch (ins.kind)
        {
            case InstrumentKind.PhProbe:
            {
                r.title = $"{AgriText.Instrument(ins.kind)}:";
                float ph = v[o + (int)SoilChannel.Ph] + Noise(1, 0.28f);
                if (crude) r.lines.Add(PhWord(ph));
                else if (q < 0.7f) r.lines.Add($"pH about {Mathf.Round(ph * 2f) / 2f:0.0}");
                else r.lines.Add($"pH {ph:0.0}");
                break;
            }
            case InstrumentKind.SoilAnalysisKit:
            {
                r.title = "The reagents colour:";
                int count = q >= 0.5f ? 6 : 3;
                for (int n = 0; n < count; n++)
                {
                    var nut = (Nutrient)n;
                    float val = Mathf.Max(0f, v[o + 1 + n] * (1f + Noise(10 + n, 0.25f)));
                    r.lines.Add(q >= 0.8f ? $"{SoilChannels.Name(SoilChannels.Of(nut))}: {val:0} ppm" : $"{SoilChannels.Name(SoilChannels.Of(nut))}: {Level(val, SoilChemistry.Adequate(nut))}");
                }
                break;
            }
            case InstrumentKind.MoistureMeter:
            {
                r.title = "The needle swings to:";
                float m = Mathf.Clamp01(v[o + (int)SoilChannel.Moisture] + Noise(2, 0.08f));
                r.lines.Add(crude || q < 0.45f ? MoistureWord(m) : $"Moisture {m * 100f:0}%");
                break;
            }
            case InstrumentKind.Thermometer:
            {
                r.title = $"{AgriText.Instrument(ins.kind)}:";
                float air = f.Weather.temperature + g.CoverWarmth[cell] + Noise(3, 1.4f);
                float soil = f.SoilTemperature + SoilTypes.Warmth(g.TypeAt(cell)) + g.CoverWarmth[cell] * 0.7f + Noise(4, 1.4f);
                r.lines.Add(crude ? $"Air: {TempWord(air)}" : $"Air {air:0.0} °C");
                r.lines.Add(crude ? $"Soil: {TempWord(soil)}" : $"Soil {soil:0.0} °C");
                break;
            }
            case InstrumentKind.LightMeter:
            {
                r.title = "The crystal glows:";
                float shade = ShadeAt(f, g.Center(cell));
                float light = Mathf.Max(0f, f.Weather.light * g.CoverLight[cell] * (1f - shade) * (1f + Noise(5, 0.12f)));
                r.lines.Add(crude ? LightWord(light) : $"About {Mathf.Round(light * 100000f / 1000f) * 1000f:N0} lux");
                if (q >= 0.6f) r.lines.Add($"Sunlight so far today: about {Mathf.Max(0f, f.TodayLight * g.CoverLight[cell] * (1f - shade) * (1f + Noise(6, 0.1f))):0.0} hours of full sun");
                break;
            }
            case InstrumentKind.Hygrometer:
            {
                r.title = "The hair hygrometer reads:";
                float h = Mathf.Clamp01(f.Weather.humidity + (g.CoverRain[cell] < 0.5f ? 0.08f : 0f) + Noise(7, 0.07f));
                r.lines.Add(crude ? HumidityWord(h) : $"Air humidity {h * 100f:0}%");
                break;
            }
            case InstrumentKind.CompactionTester:
            {
                r.title = "The rod goes in:";
                float c = Mathf.Clamp01(v[o + (int)SoilChannel.Compaction] * (1f - 0.4f * v[o + (int)SoilChannel.Tilth]) + v[o + (int)SoilChannel.Stones] * 0.3f + Noise(8, 0.08f));
                float mpa = 0.3f + 3.7f * c;
                r.lines.Add(crude || q < 0.45f ? CompactionWord(c) : $"Resistance {mpa:0.0} MPa");
                break;
            }
            case InstrumentKind.BotanicalHandbook:
            {
                if (plant == null || plant.data == null)
                {
                    r.title = "The Botanical Handbook";
                    r.lines.Add("You leaf through it, but there is no plant here to look up.");
                    break;
                }
                r.title = $"The Botanical Handbook, on the {plant.data.name}:";
                string text = plant.data.handbook;
                r.lines.Add(string.IsNullOrWhiteSpace(text) ? "There is no entry for it." : text.Trim());
                if (!string.IsNullOrWhiteSpace(text)) r.lines.Add($"({AgriText.Category(plant.data.category)}.)");
                break;
            }
            case InstrumentKind.AdvancedAnalyzer:
            {
                r.title = "The analyzer hums and settles:";
                r.lines.Add($"pH {v[o + (int)SoilChannel.Ph] + Noise(1, 0.2f):0.0}");
                var sb = new StringBuilder();
                for (int n = 0; n < SoilChannels.NutrientCount; n++)
                {
                    float val = Mathf.Max(0f, v[o + 1 + n] * (1f + Noise(10 + n, 0.12f)));
                    if (sb.Length > 0) sb.Append(", ");
                    string sym = SoilChannels.Name(SoilChannels.Of((Nutrient)n));
                    int open = sym.IndexOf('('), close = sym.IndexOf(')');
                    sym = open >= 0 && close > open ? sym.Substring(open + 1, close - open - 1) : sym;
                    sb.Append(sym).Append(' ').Append(val < 10f ? val.ToString("0.00") : val.ToString("0"));
                }
                r.lines.Add(sb.Append(" ppm").ToString());
                r.lines.Add($"Organic matter {Mathf.Max(0f, v[o + (int)SoilChannel.OrganicMatter] * (1f + Noise(30, 0.1f))):0.0}% · Salinity {Mathf.Max(0f, v[o + (int)SoilChannel.Salinity] + Noise(31, 0.15f)):0.0} dS/m");
                r.lines.Add($"Water held {v[o + (int)SoilChannel.WaterRetention] * 100f:0}% · Drainage {v[o + (int)SoilChannel.Drainage] * 100f:0}% · Compaction {v[o + (int)SoilChannel.Compaction] * 100f:0}%");
                float magic = v[o + (int)SoilChannel.MagicEssence];
                float magicSkill = AgriSkills.Factor(s, w.skills, AgriSkill.MagicalCultivation);
                r.lines.Add(magic < 0.01f ? "Magical essence: none" : magicSkill >= 0.3f ? $"Magical essence {magic * 100f + Noise(32, 2f):0}%" : $"Magical essence: {(magic < 0.2f ? "faint" : magic < 0.5f ? "clear" : "strong")}");
                float path = v[o + (int)SoilChannel.Pathogens];
                r.lines.Add($"Disease organisms: {(path < 0.08f ? "few" : path < 0.25f ? "some" : path < 0.5f ? "many" : "teeming")}");
                r.lines.Add($"Soil: {TypeWords(g, cell)}");
                break;
            }
            case InstrumentKind.DepthGauge:
            {
                r.title = "The depth gauge:";
                PlantState seed = plant != null && plant.IsSeed && !plant.covered ? plant : null;
                float depth = seed != null ? seed.depthCm : g.Holes[cell];
                depth = Mathf.Max(0f, depth + Noise(9, 0.4f));
                if (seed != null) r.lines.Add(depth < 0.3f ? "The seed lies on the surface." : $"The seed lies {Mathf.Round(depth * 2f) / 2f:0.#} cm down.");
                else if (g.Holes[cell] == 0) r.lines.Add("There is no hole here.");
                else r.lines.Add($"The hole is {Mathf.Round(depth * 2f) / 2f:0.#} cm deep.");
                break;
            }
            case InstrumentKind.Magnifier:
            {
                r.title = "Through the lens:";
                if (plant == null || !plant.Emerged)
                {
                    r.lines.AddRange(PlantObservation.Soil(g, cell, f.Catalog));
                    break;
                }
                r.lines.AddRange(PlantObservation.Plant(plant, now, s, Mathf.Min(1f, AgriSkills.Factor(s, w.skills, AgriSkill.Botany) + 0.2f), true, q, true));
                break;
            }
        }
        return r;
    }

    /// <summary>How much taller plants shade a spot (0-0.9).</summary>
    public static float ShadeAt(FieldState f, Vector2 spot)
    {
        var near = new List<PlantState>();
        f.Near(spot, 4f, near);
        float shade = 0f;
        foreach (PlantState q in near)
        {
            if (!q.Emerged || q.data == null) continue;
            float r = q.data.Look.width * 0.5f * q.size, h = q.data.Look.height * q.size * (q.dormant ? 0.3f : 1f);
            float d = (q.position - spot).magnitude;
            if (h > 0.25f && d < r) shade += (1f - d / Mathf.Max(0.01f, r)) * (0.3f + 0.7f * q.canopy) * 0.6f;
        }
        return Mathf.Min(0.9f, shade);
    }

    public static string Level(float value, float adequate)
    {
        float r = adequate <= 0f ? 1f : value / adequate;
        return r < 0.4f ? "very low" : r < 0.8f ? "low" : r < 1.6f ? "adequate" : r < 3f ? "high" : "very high";
    }

    public static string PhWord(float ph) => ph < 4.8f ? "Strongly acidic" : ph < 5.8f ? "Acidic" : ph < 6.6f ? "Slightly acidic" : ph < 7.4f ? "Neutral" : ph < 8.2f ? "Slightly alkaline" : "Alkaline";
    public static string MoistureWord(float m) => m < 0.15f ? "Dry" : m < 0.4f ? "Slightly moist" : m < 0.7f ? "Moist" : m < 0.9f ? "Wet" : "Saturated";
    public static string TempWord(float t) => t < 0f ? "freezing" : t < 8f ? "cold" : t < 15f ? "cool" : t < 24f ? "mild" : t < 30f ? "warm" : "hot";
    public static string LightWord(float l) => l < 0.02f ? "Dark" : l < 0.15f ? "Deep shade" : l < 0.4f ? "Shade" : l < 0.7f ? "Bright" : "Full sun";
    public static string HumidityWord(float h) => h < 0.3f ? "Dry air" : h < 0.6f ? "Fresh air" : h < 0.85f ? "Damp air" : "Saturated air";
    public static string CompactionWord(float c) => c < 0.25f ? "Easily: the soil is loose" : c < 0.5f ? "With some effort: firm" : c < 0.75f ? "Only with force: hard" : "Hardly at all: very hard";

    /// <summary>"like Organic-rich soil, with something of Sandy soil".</summary>
    public static string TypeWords(SoilGrid g, int cell)
    {
        var mix = new float[SoilTypes.Count];
        SoilChemistry.TypeMix(g.Values, cell * SoilGrid.Stride, g.TypeAt(cell), mix);
        int a = 0, b = -1;
        for (int t = 1; t < mix.Length; t++) if (mix[t] > mix[a]) a = t;
        for (int t = 0; t < mix.Length; t++) if (t != a && (b < 0 || mix[t] > mix[b])) b = t;
        string main = AgriText.Soil((SoilType)a);
        return b >= 0 && mix[b] > 0.15f ? $"{main}, with something of {AgriText.Soil((SoilType)b).ToLowerInvariant()}" : main;
    }
}
