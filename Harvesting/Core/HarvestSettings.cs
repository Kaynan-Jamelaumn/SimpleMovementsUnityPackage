using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a hit marks a hide laid flat (cells of the pelt).</summary>
public enum HarvestHideMark
{
    /// <summary>Nothing shows on the hide (poison, magic).</summary>
    None,
    /// <summary>A line of cells: a slash.</summary>
    Line,
    /// <summary>A hole (and an exit hole when it went through): arrows, spears.</summary>
    Hole,
    /// <summary>A cluster of torn cells: a bite.</summary>
    Tear,
    /// <summary>Parallel short lines: claws.</summary>
    Scratches,
    /// <summary>A light mark: blunt blows hardly break the hide.</summary>
    Bruise,
    /// <summary>A crushed patch.</summary>
    Crush,
    /// <summary>A round patch: fire, acid, frost.</summary>
    Patch,
    /// <summary>A jagged branching line: lightning.</summary>
    Branch,
}

/// <summary>
/// What one kind of hit (a damage form such as Slash, or an element such as Fire) does to each tissue of the region it
/// lands on, per unit of the hit's severity: a slash cuts the hide and tendons, a pierce reaches the organs, a blunt
/// blow breaks bones and cracks horns, fire ruins the hide.
/// </summary>
[Serializable]
public class HarvestDamageResponse
{
    [Tooltip("The damage form (for a form response).")]
    public DamageForm form = DamageForm.None;
    [Tooltip("The element (for an element response, added to the form's).")]
    public ElementType element = ElementType.None;
    [Min(0f)] public float hide = 0.4f;
    [Min(0f)] public float meat = 0.4f;
    [Min(0f)] public float bone = 0.2f;
    [Min(0f)] public float tendon = 0.2f;
    [Min(0f)] public float organs = 0.3f;
    [Tooltip("Claws, teeth, horns, scales, shell plates of the region.")]
    [Min(0f)] public float trophies = 0.2f;
    [Tooltip("How it marks the hide.")]
    public HarvestHideMark hideMark = HarvestHideMark.Bruise;
    [Tooltip("Size of that mark (cells at full severity).")]
    [Min(0f)] public float hideCells = 1f;
    [Tooltip("What it leaves on what it hurts.")]
    public HarvestMarks marks = HarvestMarks.Bruised;
    [Tooltip("Spoils the meat and organs of the region (poison): 0-1 per unit of severity.")]
    [Range(0f, 2f)] public float contaminate;

    public HarvestDamageResponse() { }

    public HarvestDamageResponse(float hide, float meat, float bone, float tendon, float organs, float trophies, HarvestHideMark mark, float cells, HarvestMarks marks, float contaminate = 0f)
    {
        this.hide = hide;
        this.meat = meat;
        this.bone = bone;
        this.tendon = tendon;
        this.organs = organs;
        this.trophies = trophies;
        hideMark = mark;
        hideCells = cells;
        this.marks = marks;
        this.contaminate = contaminate;
    }

    public float For(HarvestTissue t)
    {
        switch (t)
        {
            case HarvestTissue.Hide: return hide;
            case HarvestTissue.Meat: return meat;
            case HarvestTissue.Bone: return bone;
            case HarvestTissue.Tendon: return tendon;
            default: return organs;
        }
    }

    public HarvestDamageResponse Clone() => (HarvestDamageResponse)MemberwiseClone();
}

/// <summary>
/// The tunable rules of harvesting: how hits mark bodies, how carcasses spoil, how quality and yield are worked out,
/// how skills grow and how hard the work is. A <c>HarvestDatabase</c> asset holds one; <see cref="Defaults"/> gives the
/// built-in values.
/// </summary>
[Serializable]
public class HarvestSettings
{
    [Header("Combat damage")]
    [Tooltip("Severity of a hit that removes all of a creature's health (a hit removing 10% of it has a tenth of this). 1 severity ruins " +
             "the tissues that hit harms most in that region.")]
    [Min(0.1f)] public float severityPerHealth = 2.5f;
    [Tooltip("Critical hits mark this much more.")]
    [Min(1f)] public float criticalMultiplier = 1.25f;
    [Tooltip("Area damage (explosions, slams): the meat it destroys × this, and this share of its severity reaches every other region.")]
    [Min(1f)] public float areaMeatMultiplier = 1.6f;
    [Range(0f, 1f)] public float areaSpread = 0.4f;
    [Tooltip("Damage over time (burning, poison, bleeding) marks this much of a hit's severity per tick.")]
    [Range(0f, 1f)] public float periodicScale = 0.5f;
    [Tooltip("The killing blow counts in full (with its overkill): an explosion that kills ruins what it hits.")]
    public bool countOverkill = true;
    [Tooltip("Most severity one hit can have.")]
    [Min(0.1f)] public float maxSeverity = 2f;
    [Tooltip("Wounds remembered per creature (the oldest small ones are forgotten).")]
    [Range(4, 64)] public int maxWounds = 32;
    [Tooltip("What each damage form does to the tissues.")]
    public List<HarvestDamageResponse> forms = new List<HarvestDamageResponse>();
    [Tooltip("What each element adds (a flaming sword slashes AND burns).")]
    public List<HarvestDamageResponse> elements = new List<HarvestDamageResponse>();

    [Header("Carcass and spoiling")]
    [Tooltip("Minutes a carcass stays when nobody harvests it (then it is removed).")]
    [Min(0.5f)] public float carcassMinutes = 30f;
    [Tooltip("A carcass someone is working stays at least this long after the last work (minutes).")]
    [Min(0f)] public float keepAfterWorkMinutes = 5f;
    [Tooltip("Remove a carcass once nothing worth taking is left on it.")]
    public bool removeWhenEmpty = true;
    [Tooltip("Ambient temperature (°C) when the weather says nothing.")]
    public float defaultTemperature = 18f;
    [Tooltip("Spoiling speed doubles for every this many °C above 20 °C (and halves below).")]
    [Min(1f)] public float doublingDegrees = 10f;
    [Tooltip("At or below this temperature nothing spoils (frozen).")]
    public float freezingPoint = 0f;
    [Tooltip("A gutted carcass's meat and hide spoil this much slower (× the speed).")]
    [Range(0.1f, 1f)] public float guttedSpoilFactor = 0.6f;
    [Tooltip("A ruptured gut in the body makes the meat spoil this much faster.")]
    [Min(1f)] public float soiledSpoilFactor = 1.5f;
    [Tooltip("Freshness below which a piece is 'starting to turn'.")]
    [Range(0f, 1f)] public float spoilingBelow = 0.45f;

    [Header("Quality and yield")]
    [Tooltip("How much freshness counts: quality × (1 - this + this × freshness).")]
    [Range(0f, 1f)] public float freshnessWeight = 0.75f;
    [Tooltip("How much the tool itself counts in the result (the rest shows in how hard the work is).")]
    [Range(0f, 0.5f)] public float toolWeight = 0.12f;
    [Tooltip("Quality added at the top of a skill (more care, small refinements).")]
    [Range(0f, 0.2f)] public float skillQualityBonus = 0.06f;
    [Tooltip("Yield added at the top of Butchering (less waste).")]
    [Range(0f, 1f)] public float butcheringYieldBonus = 0.25f;
    [Tooltip("Yield added at the top of the other skills.")]
    [Range(0f, 1f)] public float skillYieldBonus = 0.1f;
    [Tooltip("Below this fitness a tool is the wrong tool: the piece comes out roughly cut.")]
    [Range(0f, 1f)] public float wrongToolBelow = 0.3f;
    [Tooltip("Pieces below this quality are thrown away instead of given (0 = keep everything).")]
    [Range(0f, 0.5f)] public float discardBelow = 0.02f;
    [Tooltip("Combat integrity below which a piece is destroyed (cannot be taken).")]
    [Range(0f, 0.5f)] public float destroyedBelow = 0.08f;

    [Header("Skills")]
    [Min(1)] public int maxLevel = 50;
    [Tooltip("Experience from level 0 to 1; each level needs Growth × the one before.")]
    [Min(1f)] public float xpFirstLevel = 20f;
    [Min(1f)] public float xpGrowth = 1.08f;
    [Tooltip("Anatomy learns this share of the experience of every harvest.")]
    [Range(0f, 1f)] public float anatomyShare = 0.3f;
    [Tooltip("Even a botched harvest teaches this share (mistakes teach too).")]
    [Range(0f, 1f)] public float failureShare = 0.35f;
    [Tooltip("Character experience (the player's level) per point of harvesting experience. 0 = none.")]
    [Min(0f)] public float characterXpShare = 0.25f;
    [Tooltip("Harvests of a species until its anatomy is fully known (names, values, rare finds).")]
    [Min(1)] public int harvestsToKnowSpecies = 6;

    [Header("Minigames")]
    [Tooltip("Scales every margin (tolerances, bands): 1.2 = 20% easier.")]
    [Range(0.3f, 3f)] public float forgiveness = 1f;
    [Tooltip("Scales how fast the work goes (paths, peeling, sawing).")]
    [Range(0.3f, 3f)] public float workSpeed = 1f;
    [Tooltip("How much the hands tremble (× the crafting hands' unsteadiness).")]
    [Range(0f, 3f)] public float tremble = 1f;
    [Tooltip("At the top of a skill margins are this much wider (and the hands steadier).")]
    [Range(0f, 2f)] public float skillMargin = 0.8f;

    [Header("World")]
    [Tooltip("How close a player must be to work a carcass (metres from its centre, plus its size).")]
    [Min(0.5f)] public float workRange = 2.4f;
    [Tooltip("Creatures with an anatomy drop no generic loot from the Item Drop Manager's Mob table: what they give is harvested " +
             "(their own Item Drop On Death still drops).")]
    public bool skipGenericMobDrops = true;
    [Tooltip("Mobs whose type matches an anatomy (Bear, Wolf...) become harvestable automatically.")]
    public bool autoAttach = true;
    [Tooltip("Mobs made harvestable automatically get body-part tracking (where hits land) from their anatomy when they have none.")]
    public bool addBodyParts = true;

    // ------------------------------------------------------------------ lookups
    /// <summary>The response of <paramref name="form"/> (the built-in one when the list has none for it).</summary>
    public HarvestDamageResponse Form(DamageForm form)
    {
        if (forms != null)
            foreach (HarvestDamageResponse r in forms)
                if (r != null && r.form == form)
                    return r;
        return DefaultForm(form);
    }

    /// <summary>What <paramref name="element"/> adds (null for None and elements without a response).</summary>
    public HarvestDamageResponse Element(ElementType element)
    {
        if (element == ElementType.None)
            return null;
        if (elements != null)
            foreach (HarvestDamageResponse r in elements)
                if (r != null && r.element == element)
                    return r;
        return null;
    }

    /// <summary>Experience from level <paramref name="level"/> to the next.</summary>
    public float XpToNext(int level) => xpFirstLevel * (float)Math.Pow(xpGrowth, Math.Max(0, level));

    // ------------------------------------------------------------------ defaults
    /// <summary>The built-in rules.</summary>
    public static HarvestSettings Defaults()
    {
        var s = new HarvestSettings();
        s.forms = DefaultForms();
        s.elements = DefaultElements();
        return s;
    }

    /// <summary>Adds the built-in form and element responses this one is missing (after an upgrade). Returns how many.</summary>
    public int AddMissingResponses()
    {
        int n = 0;
        forms ??= new List<HarvestDamageResponse>();
        elements ??= new List<HarvestDamageResponse>();
        foreach (HarvestDamageResponse d in DefaultForms())
            if (!forms.Exists(r => r != null && r.form == d.form)) { forms.Add(d); n++; }
        foreach (HarvestDamageResponse d in DefaultElements())
            if (!elements.Exists(r => r != null && r.element == d.element)) { elements.Add(d); n++; }
        return n;
    }

    private static HarvestDamageResponse F(DamageForm form, float hide, float meat, float bone, float tendon, float organs, float trophies, HarvestHideMark mark, float cells, HarvestMarks marks) =>
        new HarvestDamageResponse(hide, meat, bone, tendon, organs, trophies, mark, cells, marks) { form = form };

    private static HarvestDamageResponse E(ElementType element, float hide, float meat, float bone, float tendon, float organs, float trophies, HarvestHideMark mark, float cells, HarvestMarks marks, float contaminate = 0f) =>
        new HarvestDamageResponse(hide, meat, bone, tendon, organs, trophies, mark, cells, marks, contaminate) { element = element };

    public static List<HarvestDamageResponse> DefaultForms() => new List<HarvestDamageResponse>
    {
        // A hit without a form (magic, a scripted hit): a little of everything.
        F(DamageForm.None, 0.35f, 0.4f, 0.2f, 0.2f, 0.3f, 0.2f, HarvestHideMark.Bruise, 1f, HarvestMarks.Bruised),
        // Blades open the hide and cut tendons; they rarely reach far into the body.
        F(DamageForm.Slash, 1f, 0.55f, 0.15f, 0.8f, 0.3f, 0.25f, HarvestHideMark.Line, 5f, HarvestMarks.Cut),
        // Points make small holes and go deep: the organs suffer.
        F(DamageForm.Pierce, 0.35f, 0.35f, 0.15f, 0.25f, 1f, 0.15f, HarvestHideMark.Hole, 1f, HarvestMarks.Pierced),
        // Blunt blows leave the hide but break bones, crack horns and burst organs.
        F(DamageForm.Blunt, 0.12f, 0.5f, 1f, 0.15f, 0.45f, 0.8f, HarvestHideMark.Bruise, 2f, HarvestMarks.Bruised),
        // Bites tear.
        F(DamageForm.Bite, 0.85f, 0.6f, 0.35f, 0.5f, 0.35f, 0.35f, HarvestHideMark.Tear, 2f, HarvestMarks.Torn),
        // Claws shred the hide.
        F(DamageForm.Claw, 1f, 0.45f, 0.1f, 0.45f, 0.2f, 0.2f, HarvestHideMark.Scratches, 4f, HarvestMarks.Torn),
        // A great weight crushes everything.
        F(DamageForm.Crush, 0.3f, 0.85f, 1f, 0.35f, 0.8f, 1f, HarvestHideMark.Crush, 3f, HarvestMarks.Crushed),
    };

    public static List<HarvestDamageResponse> DefaultElements() => new List<HarvestDamageResponse>
    {
        // Fire ruins hides (hair singed, leather burned through) and chars the meat under them.
        E(ElementType.Fire, 1.1f, 0.5f, 0.05f, 0.25f, 0.15f, 0.15f, HarvestHideMark.Patch, 3f, HarvestMarks.Burned),
        // Frost barely harms, but makes the hide and horns brittle.
        E(ElementType.Ice, 0.25f, 0.15f, 0.1f, 0.1f, 0.05f, 0.3f, HarvestHideMark.Patch, 2f, HarvestMarks.Frozen),
        // Lightning scorches its path and stops hearts.
        E(ElementType.Lightning, 0.45f, 0.35f, 0.05f, 0.2f, 0.7f, 0.1f, HarvestHideMark.Branch, 4f, HarvestMarks.Burned),
        // Poison leaves no mark outside, but the meat and the liver are not fit to eat.
        E(ElementType.Poison, 0f, 0.3f, 0f, 0f, 0.45f, 0f, HarvestHideMark.None, 0f, HarvestMarks.Poisoned, 0.6f),
        // Holy light sears.
        E(ElementType.Holy, 0.3f, 0.2f, 0f, 0f, 0.1f, 0f, HarvestHideMark.Patch, 1f, HarvestMarks.Tainted),
        // Dark magic withers the inside.
        E(ElementType.Dark, 0.15f, 0.25f, 0.05f, 0.05f, 0.4f, 0f, HarvestHideMark.None, 0f, HarvestMarks.Tainted, 0.3f),
        // Wind cuts.
        E(ElementType.Wind, 0.3f, 0.15f, 0.05f, 0.15f, 0.05f, 0.05f, HarvestHideMark.Line, 3f, HarvestMarks.Cut),
        // Stone crushes.
        E(ElementType.Earth, 0.15f, 0.4f, 0.6f, 0.1f, 0.25f, 0.5f, HarvestHideMark.Bruise, 2f, HarvestMarks.Crushed),
        // Water does little.
        E(ElementType.Water, 0.05f, 0.1f, 0.1f, 0f, 0.1f, 0f, HarvestHideMark.None, 0f, HarvestMarks.None),
    };

    private static List<HarvestDamageResponse> builtInForms;

    private static HarvestDamageResponse DefaultForm(DamageForm form)
    {
        builtInForms ??= DefaultForms();
        foreach (HarvestDamageResponse r in builtInForms)
            if (r.form == form) return r;
        return builtInForms[0];
    }
}
