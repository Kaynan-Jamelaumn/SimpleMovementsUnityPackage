using System;
using System.Collections.Generic;

/// <summary>
/// The tools a player can work with right now (the harvesting tools and knives they carry, their weapon in hand, their
/// bare hands), and which one suits a piece of work best.
/// </summary>
public sealed class HarvestToolbox
{
    public readonly List<HarvestToolProfile> tools = new List<HarvestToolProfile>();
    private readonly HarvestToolProfile hands = HarvestTools.BareHands();

    public HarvestToolbox() { }

    public HarvestToolbox(IEnumerable<HarvestToolProfile> profiles)
    {
        if (profiles != null)
            foreach (HarvestToolProfile p in profiles)
                if (p != null) tools.Add(p);
    }

    public HarvestToolProfile Hands => hands;

    /// <summary>
    /// The best tool for work done with any of <paramref name="kinds"/>, how fit it is and as what it is used.
    /// <paramref name="handsFitness"/> &gt; 0: the bare hands do this work that well (drawing out an organ, plucking).
    /// </summary>
    public HarvestToolProfile Best(HarvestToolKind kinds, out HarvestToolKind usedAs, out float fitness, float handsFitness = 0f)
    {
        HarvestToolProfile best = null;
        usedAs = HarvestToolKind.None;
        fitness = 0f;
        foreach (HarvestToolProfile p in tools)
        {
            float f = p.Fitness(kinds, out HarvestToolKind k);
            if (f > fitness) { fitness = f; best = p; usedAs = k; }
        }
        float hf = Math.Max(handsFitness, hands.Fitness(kinds, out HarvestToolKind hk));
        if (hf > fitness)
        {
            fitness = hf;
            usedAs = hk;
            best = hands;
        }
        return best ?? hands;
    }

    /// <summary>The tools that are any good for <paramref name="kinds"/>, best first.</summary>
    public List<HarvestToolProfile> For(HarvestToolKind kinds)
    {
        var list = new List<HarvestToolProfile>();
        foreach (HarvestToolProfile p in tools)
            if (p.Fitness(kinds, out _) > 0.01f) list.Add(p);
        list.Sort((x, y) => y.Fitness(kinds, out _).CompareTo(x.Fitness(kinds, out _)));
        return list;
    }
}

/// <summary>Who is working and with what: the rules, their skills, their tools, their hands.</summary>
public sealed class HarvestWorker
{
    public HarvestSettings settings;
    public HarvestSkillData skills = new HarvestSkillData();
    public HarvestToolbox tools = new HarvestToolbox();
    public int player;
    /// <summary>Seed of the work (the shapes' small irregularities).</summary>
    public int seed;
}
