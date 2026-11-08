using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A line a player wrote in their field notebook (by hand, or a reading they chose to write down).</summary>
[Serializable]
public class AgriNote
{
    /// <summary>Game time it was written (hours).</summary>
    public double time;
    /// <summary>The field it is about (its name), or empty.</summary>
    public string field = "";
    public string text = "";
}

/// <summary>
/// A player's field notebook: only what the player wrote or chose to write down - the game never writes conclusions
/// into it. Kept on the player's own machine.
/// </summary>
[Serializable]
public class AgriNotebook
{
    public List<AgriNote> notes = new List<AgriNote>();

    public void Note(double time, string field, string text, int keep = 400)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        notes ??= new List<AgriNote>();
        notes.Add(new AgriNote { time = time, field = field ?? "", text = text.Length > 600 ? text.Substring(0, 600) : text });
        if (notes.Count > keep) notes.RemoveRange(0, notes.Count - keep);
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    public static AgriNotebook FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return new AgriNotebook();
        try
        {
            AgriNotebook n = JsonUtility.FromJson<AgriNotebook>(json) ?? new AgriNotebook();
            n.notes ??= new List<AgriNote>();
            return n;
        }
        catch (Exception)
        {
            return new AgriNotebook();
        }
    }
}

/// <summary>One player's agriculture: experience in each <see cref="AgriSkill"/> and what they have done.</summary>
[Serializable]
public class AgriSkillData
{
    public List<float> xp = new List<float>();
    public int sown, harvested, measured;

    public float Xp(AgriSkill s)
    {
        int i = (int)s;
        return xp != null && i < xp.Count ? xp[i] : 0f;
    }

    public void AddXp(AgriSkill s, float amount)
    {
        xp ??= new List<float>();
        int i = (int)s;
        while (xp.Count <= i) xp.Add(0f);
        xp[i] = Mathf.Max(0f, xp[i] + amount);
    }

    public string ToJson() => JsonUtility.ToJson(this);

    public static AgriSkillData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return new AgriSkillData();
        try
        {
            AgriSkillData d = JsonUtility.FromJson<AgriSkillData>(json) ?? new AgriSkillData();
            d.xp ??= new List<float>();
            return d;
        }
        catch (Exception)
        {
            return new AgriSkillData();
        }
    }

    public AgriSkillData Clone() => FromJson(ToJson());
}

/// <summary>Levels and what they do. Skills sharpen the hands and the eye; they never do the work or give the answers.</summary>
public static class AgriSkills
{
    public const int Count = 7;

    public static int Level(AgriSettings s, AgriSkillData d, AgriSkill k) => (s ?? AgriSettings.Defaults()).Level(d != null ? d.Xp(k) : 0f);

    /// <summary>The level as 0-1 of the highest.</summary>
    public static float Factor(AgriSettings s, AgriSkillData d, AgriSkill k)
    {
        s ??= AgriSettings.Defaults();
        return s.maxLevel <= 0 ? 0f : Mathf.Clamp01(Level(s, d, k) / (float)s.maxLevel);
    }

    /// <summary>Adds experience; returns "Botany rises to 3." for each level gained.</summary>
    public static List<string> Gain(AgriSettings s, AgriSkillData d, AgriSkill k, float amount)
    {
        var lines = new List<string>();
        if (d == null || amount <= 0f) return lines;
        s ??= AgriSettings.Defaults();
        int before = Level(s, d, k);
        d.AddXp(k, amount * s.xpMultiplier);
        int after = Level(s, d, k);
        if (after > before) lines.Add($"{AgriText.Skill(k)} rises to {after}.");
        return lines;
    }
}
