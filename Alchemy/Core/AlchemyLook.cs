using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// What a preparation looks like right now, read from its actual state - never from what it will become: how full the
/// vessel is, the liquid's colour and clarity, what floats on it and what lies at the bottom, layers, froth, how it
/// bubbles, steams, smokes and glows, streaks of a poor mix, how thick it is and what it smells of. The 3D vessel
/// (<see cref="AlchemyStationVisuals"/>) draws it; the station window (outside a test bench) describes it in words
/// (<see cref="Describe"/>) instead of showing amounts, reactions and quality. Presentation only: it changes nothing.
/// </summary>
public sealed class AlchemyLook
{
    /// <summary>One piece of an ingredient still in the vessel (drawn as a chunk).</summary>
    public struct Piece
    {
        public string Name;
        public Color Color;
        /// <summary>0 = just added, 1 = spent.</summary>
        public float Progress;
        /// <summary>Floats on the surface (fresh plant matter); else lies at the bottom (waterlogged, minerals).</summary>
        public bool Floats;
        /// <summary>How big it looks (ingredient units, a few per piece).</summary>
        public float Size;
    }

    public bool HasLiquid;
    /// <summary>0-1 of the vessel's capacity (contents and what still dissolves).</summary>
    public float Fill;
    public float LiquidUnits;
    public Color Color = new Color(0.5f, 0.55f, 0.6f, 0f);
    /// <summary>0 = clear, 1 = opaque with particles drifting in it.</summary>
    public float Cloudiness;
    /// <summary>0-1: how much of the surface is covered by floating matter.</summary>
    public float Floating;
    public Color FloatingColor = new Color(0.4f, 0.35f, 0.3f);
    /// <summary>0-1: how much has settled at the bottom.</summary>
    public float Sediment;
    public Color SedimentColor = new Color(0.35f, 0.3f, 0.25f);
    public bool Layered;
    /// <summary>Share of the liquid in the top layer.</summary>
    public float TopShare;
    public Color TopColor, BottomColor;
    /// <summary>0-1: how far stirring has blended the layers into one.</summary>
    public float Emulsion;
    /// <summary>0-1: froth on top (air beaten in, a boil with plant matter).</summary>
    public float Foam;
    /// <summary>0-1: small bubbles from the heat before it boils.</summary>
    public float Simmer;
    /// <summary>0-1: all the bubbling (heat, the boil, reactions that bubble).</summary>
    public float Bubbling;
    /// <summary>0-1: bubbles from reactions alone (it bubbles though it is not hot).</summary>
    public float ReactionBubbles;
    public float Steam;
    public float Smoke;
    /// <summary>0-1: something in it is burning or breaking down from the heat (the clue that it is too hot).</summary>
    public float Scorching;
    public float Glow;
    public Color GlowColor = Color.white;
    /// <summary>0-1: streaks of colour of a poor mix (ingredients still giving up their substances into a still liquid, half-blended layers).</summary>
    public float Streaks;
    /// <summary>0-1: how thick it is (thin as water - syrup - paste): the spoon drags in it.</summary>
    public float Thickness;
    /// <summary>0-1: grime in it.</summary>
    public float Grime;
    public bool Crystals, Precipitating, Boiling, Slopping, Sealed;
    public float Temperature, BoilingPoint = 100f;
    public float StirSpeed, Turbulence;
    /// <summary>The signs of the reactions running (their observations), strongest first.</summary>
    public readonly List<string> Signs = new List<string>();
    public readonly List<string> Smells = new List<string>();
    public readonly List<Piece> Pieces = new List<Piece>();
    /// <summary>What breaks down from the heat says so ("the rose turns a muddy brown").</summary>
    public readonly List<string> Breaking = new List<string>();

    /// <summary>Reads <paramref name="brew"/> into a new look (an empty vessel when null).</summary>
    public static AlchemyLook Of(AlchemyBrew brew)
    {
        var look = new AlchemyLook();
        look.Read(brew);
        return look;
    }

    /// <summary>Reads <paramref name="brew"/> into this look (reused every frame by the 3D vessel).</summary>
    public void Read(AlchemyBrew brew)
    {
        Signs.Clear();
        Smells.Clear();
        Pieces.Clear();
        Breaking.Clear();
        HasLiquid = Layered = Crystals = Precipitating = Boiling = Slopping = Sealed = false;
        Fill = LiquidUnits = Cloudiness = Floating = Sediment = TopShare = Emulsion = Foam = Simmer = Bubbling = ReactionBubbles = 0f;
        Steam = Smoke = Scorching = Glow = Streaks = Thickness = Grime = StirSpeed = Turbulence = 0f;
        AlchemyMixture m = brew?.Mixture;
        if (m == null)
            return;
        AlchemyRules rules = m.Rules;
        AlchemySimulationSettings settings = rules.Settings;
        int n = rules.SubstanceCount;
        double liquid = m.LiquidVolume;
        double capacity = Math.Max(1.0, m.Equipment.capacity);
        Temperature = (float)m.Temperature;
        StirSpeed = (float)m.StirSpeed;
        Turbulence = (float)m.Turbulence;
        Fill = (float)Clamp01(m.FilledVolume / capacity);
        LiquidUnits = (float)liquid;
        HasLiquid = liquid > settings.traceAmount;
        Color = HasLiquid ? m.LiquidColor : new Color(0.5f, 0.55f, 0.6f, 0f);
        Sealed = m.VaporMode == VaporHandling.Sealed;
        Boiling = brew.IsBoiling;
        Slopping = m.Agitation > settings.splashAgitation && !Sealed && HasLiquid;

        // Lowest boiling point among the solvents present: where the first bubbles come.
        double bp = 1000;
        foreach (int k in rules.Solvents)
            if (m.Amount(k) > settings.noticeableAmount)
                bp = Math.Min(bp, rules.Substances[k].boilingPoint);
        if (bp >= 1000) bp = 100;
        BoilingPoint = (float)bp;

        // What is not dissolved: floating, drifting in it, or lying at the bottom.
        double floating = 0, suspended = 0, settled = 0, nonSolvent = 0, oily = 0, degrading = 0, organic = 0;
        double fr = 0, fg = 0, fb = 0, sr = 0, sg = 0, sb = 0;
        double density = m.LiquidDensity;
        int organicIndex = rules.PropertyIndex("organic");
        for (int i = 0; i < n; i++)
        {
            AlchemySubstance s = rules.Substances[i];
            double a = m.Amount(i), u = m.UndissolvedAmount(i);
            bool lighter = rules.Density[i] < density - 0.02;
            if (u > 0)
            {
                double share = m.SuspendedShare(i);
                suspended += u * share;
                double rest = u * (1 - share);
                if (lighter) { floating += rest; Tint(ref fr, ref fg, ref fb, s.color, rest); }
                else { settled += rest; Tint(ref sr, ref sg, ref sb, s.color, rest); }
            }
            if (a <= 0)
                continue;
            if (s.phase == SubstancePhase.Solid)
            {
                if (lighter) { floating += a; Tint(ref fr, ref fg, ref fb, s.color, a); }
                else { settled += a; Tint(ref sr, ref sg, ref sb, s.color, a); }
                continue;
            }
            if (s.role != SubstanceRole.Solvent) nonSolvent += a;
            else if (rules.Layer[i] == LiquidLayer.Oily) oily += a;
            if (organicIndex >= 0) organic += a * Math.Max(0.0, rules.PropertyMatrix[i, organicIndex]);
            if (a > settings.noticeableAmount && m.Temperature > s.stableMax)
            {
                degrading += a * Math.Min(1.0, (m.Temperature - s.stableMax) / 20.0);
                if (!string.IsNullOrEmpty(s.degradeObservation) && !Breaking.Contains(s.degradeObservation))
                    Breaking.Add(s.degradeObservation);
            }
        }
        Floating = (float)Clamp01(floating / (capacity * 0.06));
        Sediment = (float)Clamp01(settled / (capacity * 0.08));
        Cloudiness = HasLiquid ? (float)Clamp01(suspended / Math.Max(1.0, liquid) * 6.0) : 0f;
        if (floating > 1e-9) FloatingColor = Mean(fr, fg, fb, floating);
        if (settled > 1e-9) SedimentColor = Mean(sr, sg, sb, settled);
        double contamination = m.ContaminantLevel();
        Grime = (float)Clamp01(contamination * 8.0);
        Cloudiness = Mathf.Max(Cloudiness, Grime * 0.6f);

        // Layers.
        if (HasLiquid && m.LayerSummary(out double top, out _, out double bottom, out _, out bool oilOnTop))
        {
            Layered = true;
            TopShare = (float)(top / Math.Max(1e-9, top + bottom));
            Emulsion = (float)m.Emulsion;
            m.Layers(out double aq, out double oil);
            var oilPart = new double[n];
            var waterPart = new double[n];
            for (int i = 0; i < n; i++)
            {
                double a = m.Amount(i);
                if (a <= 0 || rules.Substances[i].phase != SubstancePhase.Liquid) continue;
                double p = m.OilShare(i, aq, oil);
                oilPart[i] = a * p;
                waterPart[i] = a * (1 - p);
            }
            Color oilColor = AlchemyComposition.Color(rules, oilPart, true), waterColor = AlchemyComposition.Color(rules, waterPart, true);
            TopColor = oilOnTop ? oilColor : waterColor;
            BottomColor = oilOnTop ? waterColor : oilColor;
        }

        // Ingredients still in it.
        double pendingRemaining = 0;
        foreach (PendingIngredient p in m.Pending)
        {
            double r = 0, g = 0, b = 0, w = 0, heavy = 0;
            for (int k = 0; k < p.Substances.Length; k++)
            {
                int idx = p.Substances[k];
                if (idx < 0 || idx >= n) continue;
                double t = p.Totals[k];
                Tint(ref r, ref g, ref b, rules.Substances[idx].color, t);
                w += t;
                heavy += t * rules.Density[idx];
            }
            double progress = p.Progress;
            pendingRemaining += p.Remaining;
            bool mineral = w > 1e-9 && heavy / w > density + 0.3;
            // Herbs, roots, crystals are pieces; a poured liquid or a paste only streaks the brew while it mixes in.
            bool lumpy = p.Form == "whole" || p.Form == "coarse" || string.IsNullOrEmpty(p.Form);
            if (!lumpy || p.DissolveSeconds < 18 && !mineral)
                continue;
            Pieces.Add(new Piece
            {
                Name = p.Name,
                Color = w > 1e-9 ? Color.Lerp(Mean(r, g, b, w), new Color(0.35f, 0.3f, 0.22f), (float)progress * 0.6f) : new Color(0.4f, 0.5f, 0.3f),
                Progress = (float)progress,
                Floats = !mineral && progress < 0.55,
                Size = (float)Math.Max(0.5, Math.Min(4.0, p.Remaining + 1.0)),
            });
        }

        // Thickness: dissolved matter makes a syrup, oil is heavier, particles make it a paste, cold thickens it.
        if (HasLiquid)
        {
            double t = 0.05 + 1.3 * Math.Max(0.0, nonSolvent / liquid - 0.25) + 0.3 * oily / liquid + 0.4 * Math.Min(1.0, (suspended + settled * 0.3) / liquid * 3.0);
            if (m.Temperature < 10) t += 0.15 * (10 - m.Temperature) / 10.0;
            Thickness = (float)Clamp01(t);
        }

        // Heat: shimmer, then small bubbles, then the boil.
        Simmer = HasLiquid ? (float)Clamp01((m.Temperature - (bp - 45)) / 40.0) : 0f;
        double reacting = 0, smoky = 0, glowing = 0;
        var signs = new List<(string text, double rate)>();
        for (int j = 0; j < rules.Reactions.Length; j++)
        {
            if (!brew.IsReacting(j)) continue;
            double rate = brew.RateOf(j);
            AlchemyReaction def = rules.Reactions[j].Definition;
            switch (def.visual)
            {
                case ReactionVisual.Bubbles: reacting += rate * 4; break;
                case ReactionVisual.Smoke:
                case ReactionVisual.Hiss: smoky += rate * 5; reacting += rate; break;
                case ReactionVisual.Glow: glowing += rate * 6; break;
                default: reacting += rate; break;
            }
            string seen = def.observation;
            if (!string.IsNullOrWhiteSpace(seen))
            {
                int at = signs.FindIndex(x => x.text == seen);
                if (at < 0) signs.Add((seen, rate));
                else signs[at] = (seen, signs[at].rate + rate);
            }
        }
        signs.Sort((a, b) => b.rate.CompareTo(a.rate));
        foreach ((string text, double rate) s in signs)
            Signs.Add(s.text);
        double boil = brew.Tracker.BoilRate;
        ReactionBubbles = HasLiquid ? (float)Clamp01(reacting) : 0f;
        Bubbling = HasLiquid ? (float)Clamp01(boil * 2.0 + reacting + Simmer * 0.3 + (Boiling ? 0.35 : 0)) : 0f;
        Steam = HasLiquid && !Sealed ? (float)Clamp01(boil * 3.0 + Math.Max(0.0, (m.Temperature - 55) / (bp - 55 + 1e-6)) * 0.35) : 0f;

        // Too hot: plant matter scorches past ~140 °C or on a near-dry hot bottom; what breaks down from heat darkens and smokes.
        double dry = m.EffectiveHeat > 0.2 && liquid < Math.Max(1.0, capacity * 0.04) && m.Temperature > 90 ? 0.6 : 0;
        double scorch = (organic > 0.05 && m.Temperature > 140 ? Math.Min(1.0, (m.Temperature - 140) / 60.0) : 0) + dry +
                        (HasLiquid ? Math.Min(1.0, degrading / Math.Max(1.0, liquid) * 6.0) : 0);
        Scorching = (float)Clamp01(scorch);
        Smoke = (float)Clamp01(smoky + Scorching * 0.8);
        Glow = HasLiquid ? (float)Clamp01(glowing) : 0f;
        GlowColor = Color;

        // Froth: air beaten in, a hard boil with plant matter or an emulsifier in it.
        double emulsifier = m.PropertyValue("emulsifier");
        Foam = HasLiquid ? (float)Clamp01(m.Aeration * 1.1 + (Boiling ? Math.Min(0.6, organic / Math.Max(1.0, liquid) * 4.0) : 0) + emulsifier * m.Agitation) : 0f;

        // A poor mix: what still comes out of the ingredients curls through a still liquid; half-blended layers swirl.
        double still = 1.0 - Math.Min(1.0, m.Agitation * 2.0);
        double fresh = HasLiquid ? Math.Min(1.0, pendingRemaining / Math.Max(1.0, liquid) * 4.0) : 0;
        double halfBlended = Layered && Emulsion > 0.05 && Emulsion < 0.85 ? 0.6 : 0;
        Streaks = (float)Clamp01(fresh * still + halfBlended + (m.Turbulence > 0.5 && m.StirSpeed > 0.05 ? 0.25 : 0));

        // Only what can be seen: a few grains of a trace are not.
        double grains = Math.Max(settings.noticeableAmount * 4, liquid * 0.01);
        for (int i = 0; i < n; i++)
        {
            bool visible = m.UndissolvedAmount(i) + (rules.Substances[i].phase == SubstancePhase.Solid ? m.Amount(i) : 0) >= grains;
            Precipitating |= brew.Tracker.Precipitating[i] && visible;
            Crystals |= brew.Tracker.Crystallizing[i] && visible;
        }

        // Smells.
        if (HasLiquid)
        {
            double ph = m.Ph;
            if (ph < 5.5) Smells.Add("It smells sharp and sour.");
            else if (ph > 8.5) Smells.Add("It has a soapy, bitter smell.");
            if (Scorching > 0.15) Smells.Add("It smells scorched.");
            if (Grime > 0.2) Smells.Add("It smells of mud.");
            if (m.Temperature > 45)
            {
                // The most volatile liquid in it (lowest boiling point) carries its smell.
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    AlchemySubstance s = rules.Substances[i];
                    if (s.phase != SubstancePhase.Liquid || m.Amount(i) < Math.Max(settings.noticeableAmount, liquid * 0.03) || s.boilingPoint >= 100 || string.IsNullOrEmpty(s.observation)) continue;
                    if (best < 0 || s.boilingPoint < rules.Substances[best].boilingPoint) best = i;
                }
                if (best >= 0) Smells.Add($"The vapours carry {rules.Substances[best].observation}.");
            }
        }
    }

    // ------------------------------------------------------------------ words
    /// <summary>"pale amber", "deep green", "murky brown" (a liquid's colour in words).</summary>
    public static string ColorName(Color c) => ColorName(c, false);

    /// <summary><see cref="ColorName(Color)"/>; a <paramref name="solid"/> that is pale is white, not colourless.</summary>
    public static string ColorName(Color c, bool solid)
    {
        Hsv(c, out float h, out float s, out float v);
        if (v < 0.12f) return "black";
        if (s < 0.12f) return v > 0.8f ? (solid ? "white" : "colourless") : v > 0.5f ? "grey" : "dark grey";
        string hue = h < 0.03f || h >= 0.95f ? "red" : h < 0.07f ? "red-orange" : h < 0.11f ? "orange" : h < 0.15f ? "amber"
                   : h < 0.19f ? "yellow" : h < 0.25f ? "yellow-green" : h < 0.42f ? "green" : h < 0.5f ? "teal" : h < 0.56f ? "turquoise"
                   : h < 0.7f ? "blue" : h < 0.79f ? "violet" : h < 0.88f ? "purple" : "pink";
        if (s < 0.3f && v < 0.6f) return hue == "orange" || hue == "amber" || hue == "red-orange" ? "muddy brown" : "murky " + hue;
        if ((hue == "orange" || hue == "amber" || hue == "red-orange") && v < 0.55f) return v < 0.35f ? "dark brown" : "brown";
        string shade = v < 0.4f ? "dark " : s < 0.35f ? "pale " : s > 0.75f && v > 0.7f ? "vivid " : v < 0.6f ? "deep " : "";
        return shade + hue;
    }

    /// <summary>"a clear, pale amber liquid" (colour, clarity, thickness).</summary>
    public string LiquidWords()
    {
        if (!HasLiquid)
            return "";
        string clarity = Grime > 0.4f || Cloudiness > 0.75f ? "murky" : Cloudiness > 0.35f ? "cloudy" : Cloudiness > 0.12f ? "slightly hazy" : "clear";
        string body = Thickness > 0.7f ? "paste-thick " : Thickness > 0.45f ? "syrupy " : Thickness > 0.3f ? "thickish " : "";
        string colour = ColorName(Color);
        return $"{A(clarity)} {clarity}, {body}{colour} liquid";
    }

    /// <summary>
    /// What a player sees in the vessel, in sentences (the station window outside a test bench): the liquid, layers,
    /// what floats or has settled, pieces still soaking, streaks, froth, bubbling, smoke and glow, crystals, smells.
    /// <paramref name="vesselName"/> is the vessel ("cauldron"); <paramref name="thermometer"/> = the station reads the
    /// temperature (the words then leave it out).
    /// </summary>
    public void Describe(List<string> into, string vesselName, bool thermometer)
    {
        if (!HasLiquid && Pieces.Count == 0 && Sediment <= 0.01f && Floating <= 0.01f)
        {
            into.Add("The " + vesselName + " is empty.");
            return;
        }
        into.Add($"The {vesselName} is {FillWords(Fill)}.");
        if (HasLiquid)
            into.Add(Capital(LiquidWords()) + ".");
        else
            into.Add("There is no liquid in it.");
        if (Layered)
            into.Add(Emulsion > 0.7f ? "Stirred hard, its two parts have blended into one milky swirl - for now."
                : Emulsion > 0.1f ? $"It is in two layers, {ColorName(TopColor)} over {ColorName(BottomColor)}, swirling into each other where the spoon passed."
                : $"It has separated into two layers: {A(ColorName(TopColor))} {ColorName(TopColor)} one floats on {A(ColorName(BottomColor))} {ColorName(BottomColor)} one.");
        if (Floating > 0.02f)
            into.Add(Floating > 0.5f ? $"{Capital(A(ColorName(FloatingColor, true)))} {ColorName(FloatingColor, true)} crust covers much of the surface." : Floating > 0.15f ? $"Bits of something {ColorName(FloatingColor, true)} float on the surface." : "It appears there is something afloat.");
        if (Cloudiness > 0.12f && Grime < 0.4f)
            into.Add(Cloudiness > 0.5f ? "Fine specks drift all through it." : "Fine specks seem to drift in it.");
        if (Sediment > 0.02f)
            into.Add(Sediment > 0.4f ? $"A thick, {ColorName(SedimentColor, true)} layer has settled at the bottom." : "Something seems to have settled at the bottom.");
        foreach (Piece p in Pieces)
        {
            string state = p.Progress < 0.25f ? "look barely touched" : p.Progress < 0.6f ? "are softening" : p.Progress < 0.9f ? "look mostly spent" : "are almost spent";
            into.Add($"Pieces of {p.Name} {(p.Floats ? "bob on the surface" : "lie at the bottom")} and {state}.");
        }
        if (Streaks > 0.3f)
            into.Add(Streaks > 0.65f ? "Streaks of colour curl through it: it is far from evenly mixed." : "Faint streaks of colour show it is not evenly mixed.");
        if (Foam > 0.15f)
            into.Add(Foam > 0.55f ? "A thick froth rises on top." : "A thin froth sits on top.");
        if (Crystals) into.Add("Crystals are growing in it.");
        else if (Precipitating && Sediment <= 0.02f) into.Add("Grains seem to be forming in it and sinking.");
        if (Glow > 0.1f) into.Add(Glow > 0.5f ? "It glows brightly from within." : "It glows faintly.");
        foreach (string s in Smells)
            into.Add(s);
    }

    /// <summary>What is going on, in sentences: the heat's signs (shimmer, bubbles, the boil, steam, smoke), the reactions' signs, slopping.</summary>
    public void DescribeActivity(List<string> into, bool thermometer)
    {
        if (!HasLiquid)
            return;
        if (Boiling) into.Add(Bubbling > 0.75f ? "It is boiling hard, bubbles bursting all over." : "It is boiling.");
        else if (Simmer > 0.6f) into.Add("Small bubbles rise from the bottom: it is close to boiling.");
        else if (Simmer > 0.25f) into.Add("Wisps of vapour rise; it shimmers with the heat.");
        else if (!thermometer && Temperature < 8f) into.Add("It is cold to the touch.");
        else if (!thermometer && Temperature > 30f) into.Add("It is warm.");
        if (ReactionBubbles > 0.15f && !Boiling) into.Add("It bubbles, though not from the heat.");
        if (Steam > 0.5f) into.Add("Clouds of steam pour off it.");
        if (Scorching > 0.15f)
            into.Add(Scorching > 0.6f ? "It is burning! Acrid smoke rises and the colour darkens." : "A burnt smell rises and the colour seems to darken at the edges.");
        else if (Smoke > 0.2f) into.Add("Smoke curls up from it.");
        foreach (string b in Breaking)
            into.Add(Capital(b) + ".");
        foreach (string s in Signs)
            into.Add(Capital(s) + ".");
        if (Slopping) into.Add("It slops over the rim!");
    }

    public static string FillWords(float fill) =>
        fill < 0.02f ? "empty" : fill < 0.12f ? "barely wet at the bottom" : fill < 0.3f ? "about a quarter full" : fill < 0.42f ? "a third full"
        : fill < 0.6f ? "about half full" : fill < 0.8f ? "three quarters full" : fill < 0.95f ? "nearly full" : "full to the brim";

    /// <summary>How hot it is without a thermometer ("cold", "warm", "simmering"...).</summary>
    public string HeatWords()
    {
        if (Boiling) return "boiling";
        if (Simmer > 0.6f) return "about to boil";
        if (Temperature > BoilingPoint - 30f) return "steaming hot";
        if (Temperature > 45f) return "hot";
        if (Temperature > 28f) return "warm";
        if (Temperature > 8f) return "cool";
        return Temperature > 0.5f ? "cold" : "freezing";
    }

    /// <summary>All the sentences of <see cref="Describe"/> and <see cref="DescribeActivity"/> as one text.</summary>
    public string Text(string vesselName, bool thermometer)
    {
        var lines = new List<string>();
        Describe(lines, vesselName, thermometer);
        DescribeActivity(lines, thermometer);
        var sb = new StringBuilder();
        foreach (string l in lines) sb.Append(l).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }

    private static void Hsv(Color c, out float h, out float s, out float v)
    {
        float max = Math.Max(c.r, Math.Max(c.g, c.b)), min = Math.Min(c.r, Math.Min(c.g, c.b)), d = max - min;
        v = max;
        s = max > 1e-6f ? d / max : 0f;
        if (d < 1e-6f) { h = 0f; return; }
        if (max == c.r) h = (c.g - c.b) / d % 6f;
        else if (max == c.g) h = (c.b - c.r) / d + 2f;
        else h = (c.r - c.g) / d + 4f;
        h /= 6f;
        if (h < 0f) h += 1f;
    }

    private static string A(string word) => word.Length > 0 && "aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an" : "a";

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private static void Tint(ref double r, ref double g, ref double b, Color c, double w)
    {
        r += c.r * w;
        g += c.g * w;
        b += c.b * w;
    }

    private static Color Mean(double r, double g, double b, double w) => new Color((float)(r / w), (float)(g / w), (float)(b / w), 1f);

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
