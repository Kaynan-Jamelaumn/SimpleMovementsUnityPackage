using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short-hands for describing creatures in code (the built-in ones use them; so can yours): regions and materials
/// with chained settings, e.g. <c>HarvestBuild.Material("heart", "Heart", HarvestMaterialKind.Organ, HarvestMethod.Organs)
/// .From("ribs").Count().Spoils(20).Worth(12)</c>.
/// </summary>
public static class HarvestBuild
{
    // ------------------------------------------------------------------ regions
    public static HarvestRegion Region(string id, float hide, float meat, float bone, float tendon, float organs, string slot = "") =>
        new HarvestRegion { id = id, hide = hide, meat = meat, bone = bone, tendon = tendon, organs = organs, slot = slot ?? "" };

    /// <summary>Where it lies on the hide laid flat (x across, y from the head end).</summary>
    public static HarvestRegion Pelt(this HarvestRegion r, float x, float y, float w, float h)
    {
        r.pelt = new Rect(x, y, w, h);
        return r;
    }

    /// <summary>Where hits land on it: height band (0 feet .. 1 top) and length band (0 tail .. 1 nose).</summary>
    public static HarvestRegion Band(this HarvestRegion r, float heightFrom, float heightTo, float lengthFrom = 0f, float lengthTo = 1f)
    {
        r.height = new Vector2(heightFrom, heightTo);
        r.length = new Vector2(lengthFrom, lengthTo);
        return r;
    }

    public static HarvestRegion Side(this HarvestRegion r, BodySide side, float offset = 0f)
    {
        r.side = side;
        r.sideOffset = offset;
        return r;
    }

    /// <summary>Hits here deal × <paramref name="multiplier"/>.</summary>
    public static HarvestRegion Mult(this HarvestRegion r, float multiplier)
    {
        r.damageMultiplier = multiplier;
        return r;
    }

    /// <summary>A weak point (hits here deal × <paramref name="multiplier"/>).</summary>
    public static HarvestRegion Weak(this HarvestRegion r, float multiplier)
    {
        r.damageMultiplier = multiplier;
        r.weakPoint = true;
        return r;
    }

    public static HarvestRegion Defense(this HarvestRegion r, float bonus)
    {
        r.bonusDefense = bonus;
        return r;
    }

    public static HarvestRegion Armor(this HarvestRegion r, float armor)
    {
        r.armor = armor;
        return r;
    }

    public static HarvestRegion React(this HarvestRegion r, HarvestRegionReaction reaction)
    {
        r.reaction = reaction;
        return r;
    }

    /// <summary>Hard to skin (face, paws, tail, joints).</summary>
    public static HarvestRegion Fiddly(this HarvestRegion r)
    {
        r.difficultSkin = true;
        return r;
    }

    public static HarvestRegion Parts(this HarvestRegion r, params string[] names)
    {
        r.bodyParts = new List<string>(names);
        return r;
    }

    public static HarvestRegion Named(this HarvestRegion r, string name)
    {
        r.displayName = name;
        return r;
    }

    public static HarvestRegion Redirect(this HarvestRegion r, string to)
    {
        r.redirectWhenSevered = to;
        return r;
    }

    // ------------------------------------------------------------------ materials
    public static HarvestMaterial Material(string id, string name, HarvestMaterialKind kind, HarvestMethod method, string shape = "") =>
        new HarvestMaterial { id = id, displayName = name, kind = kind, method = method, shape = shape ?? "" };

    /// <summary>Comes from <paramref name="region"/> (<paramref name="units"/> pieces of a countable material; a share of a bulk one).</summary>
    public static HarvestMaterial From(this HarvestMaterial m, string region, int units = 1, float share = 1f)
    {
        m.sources.Add(new HarvestSource(region, units, share));
        return m;
    }

    /// <summary>Countable: each piece taken and graded on its own.</summary>
    public static HarvestMaterial Count(this HarvestMaterial m)
    {
        m.countable = true;
        return m;
    }

    /// <summary>A bulk yield of <paramref name="min"/>-<paramref name="max"/> pieces called <paramref name="unit"/>.</summary>
    public static HarvestMaterial Yield(this HarvestMaterial m, float min, float max, string unit = "portion")
    {
        m.countable = false;
        m.yield = new Vector2(min, max);
        m.unit = unit;
        return m;
    }

    public static HarvestMaterial Hard(this HarvestMaterial m, float difficulty, int minSkill = 0)
    {
        m.difficulty = difficulty;
        m.minSkill = minSkill;
        return m;
    }

    /// <summary>Spoils in <paramref name="minutes"/> real minutes at 20 °C.</summary>
    public static HarvestMaterial Spoils(this HarvestMaterial m, float minutes)
    {
        m.spoilMinutes = minutes;
        return m;
    }

    /// <summary>Price, weight (kg) and experience of one piece.</summary>
    public static HarvestMaterial Worth(this HarvestMaterial m, float value, float weight = 0.2f, float xp = 6f, int tier = 1)
    {
        m.value = value;
        m.weight = weight;
        m.xp = xp;
        m.tier = tier;
        return m;
    }

    public static HarvestMaterial Needs(this HarvestMaterial m, params string[] requirements)
    {
        m.requires.AddRange(requirements);
        return m;
    }

    /// <summary>A rare find: the creature has it <paramref name="chance"/> of the time; <paramref name="reveal"/> Anatomy notices it.</summary>
    public static HarvestMaterial Rare(this HarvestMaterial m, float chance, int reveal)
    {
        m.chance = chance;
        m.revealSkill = reveal;
        return m;
    }

    public static HarvestMaterial Food(this HarvestMaterial m, float hunger)
    {
        m.edible = true;
        m.foodValue = hunger;
        return m;
    }

    /// <summary>Ruptured, it soils what is around it.</summary>
    public static HarvestMaterial Hazard(this HarvestMaterial m)
    {
        m.hazard = true;
        return m;
    }

    public static HarvestMaterial Fragile(this HarvestMaterial m)
    {
        m.fragile = true;
        return m;
    }

    /// <summary>A hide that becomes the Crafting Database's <paramref name="craftHide"/> for the tannery.</summary>
    public static HarvestMaterial Tannery(this HarvestMaterial m, string craftHide)
    {
        m.craftHide = craftHide;
        return m;
    }

    public static HarvestMaterial Tools(this HarvestMaterial m, HarvestToolKind tools)
    {
        m.tools = tools;
        return m;
    }

    public static HarvestMaterial Tint(this HarvestMaterial m, Color c)
    {
        m.color = c;
        return m;
    }

    public static HarvestMaterial Describe(this HarvestMaterial m, string description)
    {
        m.description = description;
        return m;
    }

    /// <summary>A hide that is not leather: it comes off first like a hide, as a plain item (never the tannery's).</summary>
    public static HarvestMaterial PlainHide(this HarvestMaterial m)
    {
        m.plainHide = true;
        return m;
    }

    /// <summary>What it is good for (instead of the uses worked out from the material). <see cref="HarvestUse.None"/> = nothing.</summary>
    public static HarvestMaterial Uses(this HarvestMaterial m, HarvestUse uses)
    {
        m.uses = uses & ~HarvestUse.Auto;
        return m;
    }

    /// <summary>How rare it is (instead of the rarity worked out from its chance and tier).</summary>
    public static HarvestMaterial Rarity(this HarvestMaterial m, HarvestRarity rarity)
    {
        m.rarity = rarity;
        return m;
    }

    /// <summary>The bag must hold a <paramref name="item"/> (by name or id) for each piece taken, used up as it is filled.</summary>
    public static HarvestMaterial Container(this HarvestMaterial m, string item)
    {
        m.container = item ?? "";
        return m;
    }

    /// <summary>Only within <paramref name="minutes"/> minutes of death (at 20 °C).</summary>
    public static HarvestMaterial TakeWithin(this HarvestMaterial m, float minutes)
    {
        m.takeWithin = minutes;
        return m;
    }

    /// <summary>The discovery <paramref name="key"/> makes it worth <paramref name="value"/> and gives it <paramref name="uses"/>.</summary>
    public static HarvestMaterial Discovery(this HarvestMaterial m, string key, float value, HarvestUse uses)
    {
        m.discovery = key ?? "";
        m.discoveredValue = value;
        m.discoveredUses = uses & ~HarvestUse.Auto;
        return m;
    }

    /// <summary>One piece gives an alchemy vessel <paramref name="amount"/> units of <paramref name="substance"/>.</summary>
    public static HarvestMaterial Alchemy(this HarvestMaterial m, string substance, float amount, float extractability = 0.8f)
    {
        m.alchemy.Add(new HarvestAlchemyRelease { substance = substance, amount = amount, extractability = extractability });
        return m;
    }
}
