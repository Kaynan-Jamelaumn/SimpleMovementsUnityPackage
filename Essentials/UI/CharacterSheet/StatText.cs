using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describes what traits, races, classes and buffs change, one line per change, coloured for TextMeshPro rich text:
/// green when it helps the character, red when it hurts, grey when neutral. Used by the character creation screen
/// and the character sheet.
/// </summary>
public static class StatText
{
    public const string GoodColor = "#6FD08C";
    public const string BadColor = "#F07A6E";
    public const string NeutralColor = "#B4BAC4";
    public const string HeaderColor = "#7FB2FF";

    /// <summary>One change: its text and whether it helps (+1), hurts (-1) or neither (0).</summary>
    public struct Line
    {
        public string text;
        public int sign;

        public Line(string text, int sign)
        {
            this.text = text;
            this.sign = sign;
        }

        public string Rich => Paint(text, sign);
    }

    public static string Paint(string text, int sign) =>
        $"<color={(sign > 0 ? GoodColor : sign < 0 ? BadColor : NeutralColor)}>{text}</color>";

    public static int Sign(CombatStatModifier m)
    {
        if (m == null || Mathf.Approximately(m.value, 0f)) return 0;
        bool up = m.value > 0f;
        if (CombatStatInfo.LowerIsBetter(m.stat)) up = !up;
        return up ? 1 : -1;
    }

    public static int Sign(ElementalResistance r) => r == null || Mathf.Approximately(r.percent, 0f) ? 0 : r.percent > 0f ? 1 : -1;
    public static int Sign(TraitModifier m) => m == null || Mathf.Approximately(m.value, 0f) ? 0 : m.IsBeneficial ? 1 : -1;
    public static int Sign(StatScalingRule r) => r == null || Mathf.Approximately(r.perPoint, 0f) ? 0 : r.perPoint > 0f ? 1 : -1;

    public static void Add(List<Line> into, IList<CombatStatModifier> mods, float strength = 1f, string suffix = "")
    {
        if (mods == null) return;
        foreach (CombatStatModifier m in mods)
            if (m != null) into.Add(new Line(m.Describe(strength) + suffix, Sign(m)));
    }

    public static void Add(List<Line> into, IList<ElementalResistance> res, float strength = 1f)
    {
        if (res == null) return;
        foreach (ElementalResistance r in res)
            if (r != null) into.Add(new Line(r.Describe(strength), Sign(r)));
    }

    public static void Add(List<Line> into, IList<TraitModifier> mods, string suffix = "")
    {
        if (mods == null) return;
        foreach (TraitModifier m in mods)
            if (m != null) into.Add(new Line(m.Describe() + suffix, Sign(m)));
    }

    public static void Add(List<Line> into, IList<StatScalingRule> rules)
    {
        if (rules == null) return;
        foreach (StatScalingRule r in rules)
            if (r != null) into.Add(new Line(r.Describe(), Sign(r)));
    }

    /// <summary>Every change a trait makes (modifiers, combat stats, resistances, scaling, behaviours).</summary>
    public static List<Line> Of(Trait trait)
    {
        var lines = new List<Line>();
        if (trait == null) return lines;
        if (trait.HasActiveSkill)
            lines.Add(new Line("Active trait: only one per character", 0));
        Add(lines, trait.modifiers);
        Add(lines, trait.combatStats);
        Add(lines, trait.resistances);
        Add(lines, trait.scalingRules);
        foreach (TraitBehaviour b in trait.behaviours)
            if (b != null) lines.Add(new Line(b.Describe(), b.IsActive ? 0 : 1));
        return lines;
    }

    /// <summary>Every change a race / class archetype makes.</summary>
    public static List<Line> Of(CharacterArchetype a)
    {
        var lines = new List<Line>();
        if (a == null) return lines;
        Add(lines, a.combatStats);
        Add(lines, a.resistances);
        Add(lines, a.characterStats);
        Add(lines, a.scalingRules);
        Add(lines, a.combatStatsPerLevel, 1f, " per level");
        Add(lines, a.characterStatsPerLevel, " per level");
        foreach (EquipmentEffect e in a.effects)
            if (e != null) lines.Add(new Line(e.Describe(), 0));
        foreach (Trait t in a.innateTraits)
            if (t != null) lines.Add(new Line("Trait: " + t.Name, 1));
        return lines;
    }

    /// <summary>A trait's description followed by its coloured changes (TextMeshPro rich text).</summary>
    public static string RichDescription(Trait trait)
    {
        if (trait == null) return "";
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(trait.description))
            sb.Append(trait.description).Append("\n\n");
        foreach (Line l in Of(trait))
            sb.Append("• ").Append(l.Rich).Append('\n');
        if (trait.onlyFor != null && trait.onlyFor.Exists(x => x != null))
            sb.Append(Paint("Only for: " + string.Join(", ", trait.onlyFor.FindAll(x => x != null).ConvertAll(x => x.Name)), 0)).Append('\n');
        return sb.ToString().TrimEnd();
    }
}
