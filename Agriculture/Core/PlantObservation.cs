using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// What a player sees, in words: the soil (its look, wetness, how worked it is, stones, what lies on it, a hole) and a
/// plant (its height, its stage as it looks, the colour and state of its leaves, pests and the marks of diseases, its
/// flowers and fruit). Only what the eye takes in - never a cause, a number or advice: pale old leaves are pale old
/// leaves, the player works out why. A Botany-trained eye notices earlier and tells ripeness more exactly; a lens shows
/// small things.
/// </summary>
public static class PlantObservation
{
    public static string NameOf(PlantState p) => p?.data != null ? p.data.name : "plant";

    // ---------------------------------------------------------------- the soil
    public static List<string> Soil(SoilGrid g, int cell, IAgriCatalog catalog)
    {
        var lines = new List<string>();
        if (g == null || cell < 0) return lines;
        int o = cell * SoilGrid.Stride;
        float[] v = g.Values;
        var mix = new float[SoilTypes.Count];
        SoilChemistry.TypeMix(v, o, g.TypeAt(cell), mix);
        int main = 0;
        for (int t = 1; t < mix.Length; t++) if (mix[t] > mix[main]) main = t;
        float m = v[o + (int)SoilChannel.Moisture], fc = SoilChemistry.FieldCapacity(v[o + (int)SoilChannel.WaterRetention]);
        string wet = m < 0.08f ? "dust-dry" : m < fc * 0.45f ? "dry" : m < fc * 0.75f ? "slightly damp" : m <= fc + 0.04f ? "moist" : m < 0.93f ? "wet" : "waterlogged, with water standing in the low spots";
        lines.Add(AgriText.Capital(SoilTypes.Look((SoilType)main)) + ", " + wet + ".");
        float tilth = v[o + (int)SoilChannel.Tilth], comp = v[o + (int)SoilChannel.Compaction];
        if (tilth > 0.7f) lines.Add("It has been freshly worked: loose and fine.");
        else if (tilth > 0.35f) lines.Add("It has been worked, and is settling.");
        else if (comp > 0.6f) lines.Add("The surface is crusted and hard-packed.");
        else if (comp > 0.4f) lines.Add("It is firm underfoot.");
        float stones = v[o + (int)SoilChannel.Stones];
        if (stones > 0.5f) lines.Add("Stones everywhere: the soil is more stone than earth.");
        else if (stones > 0.2f) lines.Add("There are a good many stones in it.");
        else if (stones > 0.08f) lines.Add("A few stones show at the surface.");
        if (v[o + (int)SoilChannel.Salinity] > 4f) lines.Add("A thin white crust lies on the surface.");
        if (v[o + (int)SoilChannel.Mulch] > 0.15f) lines.Add("Mulch lies over it.");
        int top = g.PendingTop(cell, out float amount);
        SoilAmendmentData a = top >= 0 ? catalog?.Amendment(top) : null;
        if (a != null && amount > 0.05f) lines.Add($"{AgriText.Capital(Lying(a, amount))} lies on top, not yet worked in.");
        byte hole = g.Holes[cell];
        if (hole > 0) lines.Add($"There is a hole here, {HoleWords(hole)}.");
        return lines;
    }

    private static string Lying(SoilAmendmentData a, float amount)
    {
        string much = amount > 3f ? "a thick layer of" : amount > 0.8f ? "a layer of" : "a dusting of";
        Color c = a.color;
        float light = (c.r + c.g + c.b) / 3f;
        string colour = light > 0.8f ? "white" : light > 0.6f ? (c.r > c.b + 0.15f ? "pale yellow" : "pale") : light > 0.4f ? (c.g > c.r ? "greenish" : "grey") : light > 0.25f ? "brown" : "dark";
        string stuff = a.kind == AmendmentKind.Texture ? (a.Effect(SoilChannel.Stones) > 0f ? "gravel" : "grit") : a.kind == AmendmentKind.Organic ? "crumbly matter" : "powder";
        if (a.kind == AmendmentKind.Fertilizer && a.portionKg >= 1f) stuff = "muck";
        return $"{much} {colour} {stuff}";
    }

    /// <summary>"a shallow scrape", "a fingertip deep", "a finger deep", "a hand deep"...</summary>
    public static string HoleWords(float cm) =>
        cm < 1f ? "a shallow scrape" : cm < 2.5f ? "a fingertip deep" : cm < 6f ? "about a finger deep" : cm < 12f ? "about a hand deep" : cm < 25f ? "about a forearm deep" : "a deep pit";

    // ---------------------------------------------------------------- a plant
    public static List<string> Plant(PlantState p, double now, AgriSettings s, float botany, bool lens, float lensQuality, bool closeUp = false)
    {
        var lines = new List<string>();
        if (p == null || p.data == null) return lines;
        PlantSpeciesData d = p.data;
        ProduceSpec pr = d.Produce;
        double days = Math.Max(0.0, now - p.planted) / 24.0;
        lines.Add($"{AgriText.Capital(Article(d.name))} {d.name}, sown {AgeWords(days)}.");
        if (!string.IsNullOrEmpty(p.label)) lines.Add($"Its label reads: \"{p.label}\".");
        if (!p.Alive)
        {
            lines.Add(p.deadHours > 24f ? "It is dead, dry and brittle." : "It is dead: limp, brown and collapsing.");
            return lines;
        }
        if (p.IsSeed)
        {
            lines.Add(p.covered ? "Nothing has come up yet." : "The seed lies uncovered.");
            return lines;
        }
        // How it stands.
        float height = d.Look.height * p.size * (p.dormant ? 0.5f : 1f);
        string tall = height < 0.06f ? "barely above the soil" : height < 0.2f ? "ankle-high" : height < 0.55f ? "knee-high" : height < 1f ? "waist-high" : height < 1.5f ? "chest-high" : height < 2.2f ? "taller than a man" : "a small tree";
        string stage;
        switch (p.stage)
        {
            case PlantStage.Seedling: stage = "A seedling with its first leaves"; break;
            case PlantStage.Juvenile: stage = "A young plant"; break;
            case PlantStage.Flowering: stage = p.bolted ? "It has run up a tall flower stalk" : "It is in flower"; break;
            case PlantStage.Senescence: stage = "It is going over: its leaves yellow and drop"; break;
            default: stage = d.Look.shape == PlantShape.Tree ? "A tree" : "A grown plant"; break;
        }
        lines.Add(p.dormant ? $"It is bare and resting, {tall}." : $"{stage}, {tall}.");
        if (p.dormant) return lines;

        // The leaves. A trained eye sees the first signs; an untrained one only the obvious.
        float threshold = 0.32f - 0.17f * Mathf.Clamp01(botany) - (lens ? 0.04f : 0f);
        bool any = false;
        void See(PlantSymptom sym, string mild, string bad)
        {
            float x = p.Symptom(sym);
            if (x < threshold) return;
            lines.Add(x > 0.6f ? bad : mild);
            any = true;
        }
        See(PlantSymptom.PaleOldLeaves, "The lower, older leaves are pale.", "The older leaves are yellow, and some have dropped.");
        See(PlantSymptom.PurpleLeaves, "The leaves have a dull purple cast.", "The leaves are dark and purplish, the undersides reddish.");
        See(PlantSymptom.ScorchedEdges, "The leaf edges are turning brown.", "The leaf edges are brown and crisp, as if scorched.");
        See(PlantSymptom.YellowBetweenVeins, "The leaves are yellowish between the veins.", "The leaves are yellow between green veins.");
        See(PlantSymptom.PaleNewLeaves, "The newest leaves are pale.", "The newest leaves are pale yellow, almost white.");
        See(PlantSymptom.DeadTips, "Some growing tips look weak.", "Some growing tips are black and dead.");
        See(PlantSymptom.LeafBurn, "The leaf tips are brown.", "The leaf tips and edges look burnt.");
        See(PlantSymptom.Wilting, "The leaves droop.", "It is badly wilted, the leaves hanging limp.");
        See(PlantSymptom.Waterlogged, "The leaves are yellowing and limp.", "It is yellow, limp and soft at the base.");
        See(PlantSymptom.FrostBitten, "Some leaves are darkened at the edges.", "Leaves are blackened and glassy.");
        See(PlantSymptom.Sunscald, "There are pale patches on the leaves.", "There are bleached, papery patches on the leaves.");
        See(PlantSymptom.Leggy, "Its stems are long and thin.", "Its stems are long, thin and pale, leaning to the light.");
        See(PlantSymptom.Stunted, "It has not grown much.", "It has hardly grown at all.");
        if (!any && p.health > 0.85f && p.stress < 0.3f && botany >= 0.15f) lines.Add("Its leaves are lush and evenly green.");

        // Pests (visible on the leaves; roots do not show).
        float seePest = 0.2f - 0.13f * Mathf.Clamp01(botany) - (lens ? 0.06f + 0.04f * lensQuality : 0f);
        for (int k = 0; k < 8; k++)
        {
            float load = p.pests[k];
            if (load < seePest) continue;
            string many = load < 0.3f ? "a few" : load < 0.6f ? "many" : "swarms of";
            switch ((PestKind)k)
            {
                case PestKind.Aphids: lines.Add(lens ? $"{AgriText.Capital(many)} tiny soft green insects cluster under the leaves and on the tips." : $"{AgriText.Capital(many)} tiny green specks under the leaves; the tips curl."); break;
                case PestKind.Caterpillars: lines.Add(load < 0.3f ? "Some leaves have ragged holes chewed in them." : $"Ragged holes everywhere, and {many} fat green caterpillars."); break;
                case PestKind.Mites: lines.Add(lens ? "Fine webbing, and on it tiny moving red dots." : "The leaves are speckled and dusty; fine threads between them."); break;
                case PestKind.Beetles: lines.Add($"{AgriText.Capital(many)} shiny beetles on the leaves, and small round holes."); break;
                case PestKind.SapSuckers: lines.Add(load < 0.3f ? "The leaves are a little sticky." : "The leaves are sticky; a cloud of tiny white insects rises when it is touched."); break;
                case PestKind.RootParasites:
                    if (lens && load > 0.35f) lines.Add("At the soil line the roots look knotted and gnawed.");
                    break;
                case PestKind.Slugs: lines.Add(load < 0.3f ? "Silvery trails on the leaves." : "Silvery trails, and leaf edges eaten away."); break;
                case PestKind.Snails: lines.Add(load < 0.3f ? "A snail or two on the stems." : $"{AgriText.Capital(many)} snails, and chewed, ragged leaves."); break;
            }
        }
        float seeDisease = 0.16f - 0.1f * Mathf.Clamp01(botany) - (lens ? 0.04f : 0f);
        for (int k = 0; k < 6; k++)
        {
            float sev = p.diseases[k];
            if (sev < seeDisease) continue;
            bool bad = sev > 0.5f;
            switch ((DiseaseKind)k)
            {
                case DiseaseKind.Fungal: lines.Add(bad ? "A white, powdery coat covers many leaves; some are grey with fuzz." : "There are white, powdery patches on some leaves."); break;
                case DiseaseKind.RootRot: lines.Add(bad ? "The stem base is black and soft, and it smells of rot." : "The stem base is darkening."); break;
                case DiseaseKind.Wilt: lines.Add(bad ? "It wilts in the heat of the day, whatever the soil." : "Some shoots hang limp in the afternoon."); break;
                case DiseaseKind.LeafSpot: lines.Add(bad ? "Brown spots with yellow halos run together on the leaves." : "There are small brown spots on some leaves."); break;
                case DiseaseKind.Bacterial: lines.Add(bad ? "Dark, wet-looking patches with a foul smell." : "A few dark, water-soaked spots."); break;
                case DiseaseKind.Viral: lines.Add(bad ? "The leaves are mottled light and dark, puckered and twisted." : "Some leaves are faintly mottled."); break;
            }
        }

        // The crown and how it stands.
        bool woody = d.Look.shape == PlantShape.Tree || d.Look.shape == PlantShape.Bush || d.Look.shape == PlantShape.Vine;
        if (woody && p.deadWood > 0.3f) lines.Add("There is dead wood in the crown.");
        if (woody && p.canopy > 0.85f) lines.Add("The crown is dense and crowded.");
        if (p.wounds > 0.3f) lines.Add("It carries fresh wounds.");
        if (p.supported) lines.Add("It is tied to a stake.");
        else if (d.needsSupport && p.size > 0.45f) lines.Add("It sprawls over the ground.");
        if (p.genes.rare) lines.Add("There is a strange lustre to it.");

        // Flowers and produce.
        string produce = Produce(p, pr, botany, closeUp);
        if (!string.IsNullOrEmpty(produce)) lines.Add(produce);
        return lines;
    }

    /// <summary>The produce in words: by look for an untrained eye, by stage for a trained one.</summary>
    public static string Produce(PlantState p, ProduceSpec pr, float botany, bool closeUp)
    {
        if (p.fruits.Count == 0) return "";
        var counts = new int[9];
        int flowersOpen = 0, set = 0;
        foreach (FruitState f in p.fruits)
        {
            counts[(int)f.stage]++;
            if (f.stage == ProduceStage.Bud && (pr.part == ProducePart.Fruit || pr.part == ProducePart.Grain))
            {
                if (f.pollinated) set++;
                else flowersOpen++;
            }
        }
        var sb = new StringBuilder();
        void Add(int n, string what)
        {
            if (n <= 0) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(AgriText.Count(n)).Append(' ').Append(what);
        }
        bool fruit = pr.part == ProducePart.Fruit || pr.part == ProducePart.Grain;
        if (fruit)
        {
            if (botany >= 0.45f && set > 0) { Add(flowersOpen, flowersOpen == 1 ? "open flower" : "open flowers"); Add(set, set == 1 ? "flower that has set" : "flowers that have set"); }
            else Add(flowersOpen + set, flowersOpen + set == 1 ? "flower" : "flowers");
        }
        string part = AgriText.Part(pr.part, false), parts = AgriText.Part(pr.part, true);
        if (botany >= 0.6f)
        {
            for (int st = fruit ? 1 : 0; st < 9; st++)
                if (counts[st] > 0)
                {
                    string name = AgriText.Stage(pr.part, (ProduceStage)st).ToLowerInvariant();
                    if (counts[st] > 1 && !name.EndsWith("s")) name = Plural(name);
                    Add(counts[st], (ProduceStage)st == ProduceStage.Ripe && botany >= 0.8f ? name + " (at their best)" : name);
                }
        }
        else
        {
            // By look: size, colour, softness.
            int young = counts[(int)ProduceStage.Set] + counts[(int)ProduceStage.Early] + counts[(int)ProduceStage.Immature] + (fruit ? 0 : counts[(int)ProduceStage.Bud]);
            Add(young, young == 1 ? $"small, young {part}" : $"small, young {parts}");
            Add(counts[(int)ProduceStage.Developing], counts[(int)ProduceStage.Developing] == 1 ? $"swelling {part}" : $"swelling {parts}");
            int colouring = counts[(int)ProduceStage.Mature] + (botany >= 0.3f ? 0 : counts[(int)ProduceStage.Ripe]);
            Add(colouring, colouring == 1 ? $"{part} that looks nearly ready" : $"{parts} that look nearly ready");
            if (botany >= 0.3f) Add(counts[(int)ProduceStage.Ripe], counts[(int)ProduceStage.Ripe] == 1 ? $"{part} full and well coloured" : $"{parts} full and well coloured");
            Add(counts[(int)ProduceStage.Overripe], counts[(int)ProduceStage.Overripe] == 1 ? $"soft, darkening {part}" : $"soft, darkening {parts}");
            Add(counts[(int)ProduceStage.Rotten], counts[(int)ProduceStage.Rotten] == 1 ? $"rotting {part}" : $"rotting {parts}");
        }
        if (sb.Length == 0) return "";
        return "On it: " + sb + ".";
    }

    private static string Plural(string s) => s.EndsWith("f") ? s.Substring(0, s.Length - 1) + "ves" : s.EndsWith("y") ? s.Substring(0, s.Length - 1) + "ies" : s + "s";

    private static string Article(string name) => !string.IsNullOrEmpty(name) && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an" : "a";

    public static string AgeWords(double days) =>
        days < 0.08 ? "just now" : days < 1 ? "today" : days < 2 ? "yesterday" : days < 14 ? $"{Math.Floor(days)} days ago" : days < 60 ? $"some {Math.Round(days / 7.0)} weeks ago" : "long ago";
}
