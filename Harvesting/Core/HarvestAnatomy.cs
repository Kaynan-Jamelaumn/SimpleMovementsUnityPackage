using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One region of a creature's body (head, neck, back, belly, a leg, a wing, the tail...): what it is made of (hide,
/// meat, bone, tendons, organs, natural armour), where it lies on the hide laid flat, and - for the body-part profile
/// generated from the anatomy - where hits land on it, how much they hurt (weak points) and what they do.
/// </summary>
[Serializable]
public class HarvestRegion
{
    [Tooltip("Stable id (materials name it): head, neck, back, belly, foreleg_l, hindleg_r, wing_l, tail...")]
    public string id = "body";
    [Tooltip("Name shown to players. Empty = from the id.")]
    public string displayName = "";
    [Tooltip("The combat body parts that are this region (names or aliases of the creature's Body Part Profile: Head, Snout, LeftForeleg...). " +
             "Hits on them mark this region. Empty = the id.")]
    public List<string> bodyParts = new List<string>();
    [Tooltip("The shape the anatomy drawing uses for it (a slot of the body plan: head, neck, chest, back, belly, haunch, foreleg_l...). Empty = the id.")]
    public string slot = "";

    [Header("Tissues")]
    [Tooltip("How much of it is covered by hide (0 = none: a beak, a shell).")]
    [Range(0f, 1f)] public float hide = 1f;
    [Tooltip("Meat (relative mass): where the cuts come from.")]
    [Min(0f)] public float meat = 1f;
    [Tooltip("Bone (0-1): how much of it is bone - blunt blows break it.")]
    [Range(0f, 1f)] public float bone = 0.5f;
    [Tooltip("Tendons and ligaments (0-1): limbs and joints have many - slashes cut them.")]
    [Range(0f, 1f)] public float tendon = 0f;
    [Tooltip("The body cavity (0-1): organs lie inside - deep hits reach them.")]
    [Range(0f, 1f)] public float organs = 0f;
    [Tooltip("Natural armour (0-1): scales, plates, a thick skull. It takes part of every hit before the tissues under it.")]
    [Range(0f, 1f)] public float armor = 0f;

    [Header("On the hide")]
    [Tooltip("Where it lies on the hide laid flat, flesh side up: x across (0 left .. 1 right), y along (0 the head end .. 1 the tail end).")]
    public Rect pelt = new Rect(0.3f, 0.3f, 0.4f, 0.4f);
    [Tooltip("Hard to skin (face, paws, tail, joints): the margins are narrow when the hide is separated here.")]
    public bool difficultSkin;

    [Header("Combat (for the generated body-part profile)")]
    [Tooltip("Height band of the body (0 feet .. 1 top) where hits land on it when there is no hitbox.")]
    public Vector2 height = new Vector2(0f, 1f);
    [Tooltip("Length band (0 tail end .. 1 nose) where hits land on it, for horizontal bodies. (0,1) = the whole length.")]
    public Vector2 length = new Vector2(0f, 1f);
    [Tooltip("Which side of the body it is on (legs, wings): hits that far out from the centre line.")]
    public BodySide side = BodySide.Any;
    [Tooltip("For side regions: how far out from the centre line a hit must be (fraction of the body radius).")]
    [Range(0f, 1f)] public float sideOffset = 0.25f;
    [Tooltip("Damage multiplier of hits here (a weak point deals more: 1.5 - 2.5).")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Tooltip("Natural defense added to hits here (a carapace, a thick skull); negative = soft.")]
    public float bonusDefense;
    [Tooltip("A weak point: shown as one on the anatomy (and its multiplier is what makes it one).")]
    public bool weakPoint;
    [Tooltip("What a hit here does to the creature.")]
    public HarvestRegionReaction reaction = HarvestRegionReaction.None;
    [Tooltip("Body part hits go to when this one is severed.")]
    public string redirectWhenSevered = "";

    [Header("Hitbox (optional)")]
    [Tooltip("A bone of the model to put a hitbox on when the mob plays (a trigger sphere: exact hit locations), on a mob that " +
             "uses its anatomy's body parts and has no hitboxes of its own. Empty = none (the height and length bands decide).")]
    public string hitboxBone = "";
    [Tooltip("Radius of that hitbox (metres; 0 = from the creature's height).")]
    [Min(0f)] public float hitboxRadius;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? HarvestNames.FromId(id) : displayName;
    public string Slot => string.IsNullOrWhiteSpace(slot) ? id : slot;

    /// <summary>Is <paramref name="partName"/> this region (its id or one of its body parts, case-insensitive)?</summary>
    public bool Matches(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return false;
        string n = partName.Trim();
        if (string.Equals(id, n, StringComparison.OrdinalIgnoreCase))
            return true;
        if (bodyParts != null)
            for (int i = 0; i < bodyParts.Count; i++)
                if (string.Equals(bodyParts[i], n, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    /// <summary>How much of <paramref name="t"/> it has (meat as a 0-1 presence).</summary>
    public float Tissue(HarvestTissue t)
    {
        switch (t)
        {
            case HarvestTissue.Hide: return hide;
            case HarvestTissue.Meat: return meat > 0f ? Math.Min(1f, 0.35f + meat * 0.4f) : 0f;
            case HarvestTissue.Bone: return bone;
            case HarvestTissue.Tendon: return tendon;
            default: return organs;
        }
    }

    public HarvestRegion Clone()
    {
        var c = (HarvestRegion)MemberwiseClone();
        c.bodyParts = bodyParts != null ? new List<string>(bodyParts) : new List<string>();
        return c;
    }
}

/// <summary>Where a material comes from: a region, how many pieces it holds (claws on a paw) or its share of a yield (meat).</summary>
[Serializable]
public class HarvestSource
{
    [Tooltip("The region (its id).")]
    public string region = "";
    [Tooltip("Countable materials: how many pieces this region holds (5 claws on a forepaw, 1 heart).")]
    [Min(0)] public int units = 1;
    [Tooltip("Bulk materials: this region's weight in the yield and the condition (meat of a cut spread over two regions).")]
    [Min(0f)] public float share = 1f;

    public HarvestSource() { }
    public HarvestSource(string region, int units = 1, float share = 1f)
    {
        this.region = region;
        this.units = units;
        this.share = share;
    }

    public HarvestSource Clone() => new HarvestSource(region, units, share);
}

/// <summary>
/// What a material gives an alchemy vessel: a substance of the Alchemy Database, how much of it one piece holds and how
/// readily it comes out (organs and glands are alchemy ingredients).
/// </summary>
[Serializable]
public class HarvestAlchemyRelease
{
    [Tooltip("Alchemy Database substance id (venom, sanguine_sap, draconic_ichor...).")]
    public string substance = "";
    [Tooltip("Units of it in one piece.")]
    [Min(0f)] public float amount = 1f;
    [Tooltip("How readily it comes out (0.01 locked in - 1 at once).")]
    [Range(0.01f, 1f)] public float extractability = 0.8f;

    public HarvestAlchemyRelease Clone() => (HarvestAlchemyRelease)MemberwiseClone();
}

/// <summary>
/// One material a creature gives: a pelt, a cut of meat, a tendon, a bone, an organ, claws... - how it is taken (its
/// method, minigame, tools, skill and difficulty), where it comes from, how much there is, what it is worth, how fast it
/// spoils, what must be taken before it, and - for rare finds - how likely the creature has it and how much Anatomy it
/// takes to notice it.
/// </summary>
[Serializable]
public class HarvestMaterial
{
    [Tooltip("Stable id within the creature (pelt, backstrap, heart, claws, femur...).")]
    public string id = "material";
    [Tooltip("Name shown to players (\"Bear pelt\"). Empty = from the id.")]
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    public HarvestMaterialKind kind = HarvestMaterialKind.Other;
    [Tooltip("How it is taken (the minigame, the tools and the skill).")]
    public HarvestMethod method = HarvestMethod.Trophies;
    [Tooltip("How it is drawn and worked: heart, lungs, liver, kidney, stomach, intestines, bladder, gall, gland, longbone, skull, ribs, spine, " +
             "pelvis, jaw, claw, fang, tooth, horn, antler, tusk, beak, talon, scale, plate, tendon, sinew, cut, fat, feathers. Empty = from the kind.")]
    public string shape = "";
    [Tooltip("Where it comes from.")]
    public List<HarvestSource> sources = new List<HarvestSource>();
    [Tooltip("Countable: each piece is taken and graded on its own (claws, teeth, bones, organs, tendons). Off: a bulk yield (meat, fat, a hide).")]
    public bool countable;
    [Tooltip("Bulk: pieces from a creature of its reference size, unhurt and perfectly harvested (min - max).")]
    public Vector2 yield = new Vector2(1f, 1f);
    [Tooltip("What one piece is called (\"portion\", \"strip\").")]
    public string unit = "piece";

    [Header("Work")]
    [Tooltip("How hard it is to take (0 easy .. 1 very hard): narrower margins, faster mistakes.")]
    [Range(0f, 1f)] public float difficulty = 0.4f;
    [Tooltip("Level of the method's skill needed to try it at all.")]
    [Min(0)] public int minSkill;
    [Tooltip("Level of Anatomy needed to notice it (rare finds hidden in the body). 0 = always seen.")]
    [Min(0)] public int revealSkill;
    [Tooltip("Chance the creature has it at all (a stone in the stomach: 0.08). Rolled once per creature.")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("The tools it is worked with. None = the method's usual tools.")]
    public HarvestToolKind tools = HarvestToolKind.None;
    [Tooltip("What must be taken (or done) first: material ids, or 'hide' (the hide off), 'cavity' (the body opened), 'gutted'.")]
    public List<string> requires = new List<string>();

    [Header("Value")]
    [Tooltip("Real minutes until it spoils at 20 °C (cold slows it, frost stops it). 0 = it never spoils (bones, claws, horns).")]
    [Min(0f)] public float spoilMinutes;
    [Tooltip("Price of one perfect piece.")]
    [Min(0f)] public float value = 1f;
    [Tooltip("Weight of one piece (kg).")]
    [Min(0f)] public float weight = 0.2f;
    [Tooltip("Material tier (1 = common).")]
    [Min(1)] public int tier = 1;
    [Tooltip("Experience one piece gives (its method's skill; Anatomy a share).")]
    [Min(0f)] public float xp = 6f;
    [Tooltip("Hides: the Crafting Database hide it becomes (cow, deer, boar, wolf, bear, rabbit, drake, troll, snake): a raw hide piece for the tannery, " +
             "with every hole, burn and tear where it was. Empty = a plain hide item.")]
    public string craftHide = "";
    [Tooltip("Ruptured, it spoils what is around it (gut, bladder, gall bladder).")]
    public bool hazard;
    [Tooltip("One slip and it is lost (glands, egg sacs): narrow margins, heavy damage.")]
    public bool fragile;
    [Tooltip("It can be eaten (raw it restores Food Value of hunger; spoiled or poisoned it makes you sick).")]
    public bool edible;
    [Tooltip("Hunger one perfect piece restores.")]
    [Min(0f)] public float foodValue = 10f;
    [Tooltip("Colour of its icon and on the anatomy.")]
    public Color color = new Color(0.75f, 0.6f, 0.5f);

    [Header("Alchemy")]
    [Tooltip("What one piece gives an alchemy vessel (Alchemy Database substances). Empty = not an alchemy ingredient.")]
    public List<HarvestAlchemyRelease> alchemy = new List<HarvestAlchemyRelease>();

    public string Name => string.IsNullOrWhiteSpace(displayName) ? HarvestNames.FromId(id) : displayName;

    /// <summary>The shape, or the usual one for its kind.</summary>
    public string Shape
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(shape))
                return shape.Trim().ToLowerInvariant();
            switch (kind)
            {
                case HarvestMaterialKind.Hide:
                case HarvestMaterialKind.Feathers: return "hide";
                case HarvestMaterialKind.Scales: return "scale";
                case HarvestMaterialKind.Meat: return "cut";
                case HarvestMaterialKind.Fat: return "fat";
                case HarvestMaterialKind.Tendon: return "tendon";
                case HarvestMaterialKind.Bone: return "longbone";
                case HarvestMaterialKind.Organ: return "organ";
                case HarvestMaterialKind.Gland: return "gland";
                case HarvestMaterialKind.Claw: return "claw";
                case HarvestMaterialKind.Tooth: return "fang";
                case HarvestMaterialKind.Horn: return "horn";
                case HarvestMaterialKind.Chitin: return "plate";
                case HarvestMaterialKind.Essence: return "gland";
                default: return "cut";
            }
        }
    }

    /// <summary>The skill trained (and needed) by taking it.</summary>
    public HarvestSkill Skill => HarvestText.SkillOf(method);

    /// <summary>The tools it is worked with (its own, else its method's).</summary>
    public HarvestToolKind Tools => tools != HarvestToolKind.None ? tools : HarvestMethods.DefaultTools(method, Shape);

    /// <summary>Pieces in total (countable materials): the units of every source.</summary>
    public int TotalUnits
    {
        get
        {
            int n = 0;
            if (sources != null)
                foreach (HarvestSource s in sources)
                    if (s != null) n += Math.Max(0, s.units);
            return n;
        }
    }

    /// <summary>Does <paramref name="token"/> (a material id or 'hide', 'cavity', 'gutted') have to come first?</summary>
    public bool Requires(string token)
    {
        if (requires == null || string.IsNullOrEmpty(token))
            return false;
        foreach (string r in requires)
            if (string.Equals(r, token, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>Is it eaten (food) rather than used (a material)?</summary>
    public bool IsFood => edible && (kind == HarvestMaterialKind.Meat || kind == HarvestMaterialKind.Fat || kind == HarvestMaterialKind.Organ);

    public HarvestMaterial Clone()
    {
        var c = (HarvestMaterial)MemberwiseClone();
        c.sources = new List<HarvestSource>();
        if (sources != null)
            foreach (HarvestSource s in sources)
                if (s != null) c.sources.Add(s.Clone());
        c.requires = requires != null ? new List<string>(requires) : new List<string>();
        c.alchemy = new List<HarvestAlchemyRelease>();
        if (alchemy != null)
            foreach (HarvestAlchemyRelease r in alchemy)
                if (r != null) c.alchemy.Add(r.Clone());
        return c;
    }
}

/// <summary>Less tissue damage from one element (a drake's hide shrugs off fire).</summary>
[Serializable]
public class HarvestElementResistance
{
    public ElementType element = ElementType.Fire;
    [Tooltip("0 = no protection .. 1 = that element never harms its tissues.")]
    [Range(0f, 1f)] public float resist = 0.5f;
}

/// <summary>
/// Everything about the body of one kind of creature: its plan, size, regions and the materials it gives. A
/// <c>CreatureAnatomy</c> asset holds one; the built-in creatures are made in <see cref="HarvestDefaults"/>. New creatures
/// reuse every mechanic: they only describe their own body.
/// </summary>
[Serializable]
public class HarvestAnatomyData
{
    [Tooltip("Stable id (bear, wolf, giant_spider): items and saves name it.")]
    public string id = "creature";
    [Tooltip("Name shown to players.")]
    public string displayName = "Creature";
    [TextArea(1, 3)] public string description = "";
    [Tooltip("How its body is built.")]
    public HarvestBodyPlan plan = HarvestBodyPlan.Quadruped;
    [Tooltip("Mob types or words in a mob's name this anatomy is used for automatically (Bear, Grizzly). Matched whole-word, case-insensitive.")]
    public List<string> mobTypes = new List<string>();

    [Header("Size")]
    [Tooltip("Mass of a typical adult (kg): yields scale with it.")]
    [Min(0.05f)] public float mass = 80f;
    [Tooltip("Height of a typical adult (metres): a bigger or smaller one gives more or less (by its height against this).")]
    [Min(0.05f)] public float referenceHeight = 1f;

    [Header("Hide")]
    [Tooltip("Cells of its hide laid flat, head up: across (x) and along (y). With a Crafting hide, use that hide's Height and Width " +
             "(the tannery lays hides out with the spine running across: its width is the hide's length).")]
    public Vector2Int peltSize = new Vector2Int(12, 9);
    public Color hideColor = new Color(0.55f, 0.42f, 0.3f);
    public Color hairColor = new Color(0.35f, 0.25f, 0.18f);
    public Color meatColor = new Color(0.72f, 0.24f, 0.22f);

    [Header("Body")]
    [Tooltip("How hard its tissues are to damage (a troll's hide: 1.5). Hits on it mark the materials less.")]
    [Min(0.05f)] public float toughness = 1f;
    [Tooltip("While alive, wounds close by this fraction a minute (trolls: 0.5); burns and acid never do.")]
    [Range(0f, 2f)] public float regeneration;
    [Tooltip("Elements its tissues resist.")]
    public List<HarvestElementResistance> resistances = new List<HarvestElementResistance>();
    [Tooltip("The region hits without a location mark (area damage, damage over time). Empty = the region with the most meat that is not a weak point and takes no extra damage (multiplier 1 or less).")]
    public string defaultRegion = "";

    [Tooltip("The regions of the body.")]
    public List<HarvestRegion> regions = new List<HarvestRegion>();
    [Tooltip("What it gives.")]
    public List<HarvestMaterial> materials = new List<HarvestMaterial>();

    public string Name => string.IsNullOrWhiteSpace(displayName) ? HarvestNames.FromId(id) : displayName;

    // ------------------------------------------------------------------ lookups
    public int RegionIndex(string regionId)
    {
        if (regions == null || string.IsNullOrEmpty(regionId))
            return -1;
        for (int i = 0; i < regions.Count; i++)
            if (regions[i] != null && string.Equals(regions[i].id, regionId, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    public HarvestRegion Region(string regionId)
    {
        int i = RegionIndex(regionId);
        return i >= 0 ? regions[i] : null;
    }

    /// <summary>The region a combat body part belongs to (by the regions' body parts, then by id), or -1.</summary>
    public int RegionOfPart(string partName)
    {
        if (regions == null || string.IsNullOrWhiteSpace(partName))
            return -1;
        for (int i = 0; i < regions.Count; i++)
            if (regions[i] != null && regions[i].Matches(partName))
                return i;
        return -1;
    }

    /// <summary>
    /// The region a combat body part belongs to, for a hit at a known place: <paramref name="h01"/> (0 feet .. 1 top),
    /// <paramref name="lateral"/> (signed, + = right) and <paramref name="along"/> (0 tail .. 1 nose). A part several
    /// regions list ("Legs" on every leg) goes to the one whose side and bands fit the hit best; -1 when none lists it.
    /// </summary>
    public int RegionOfPart(string partName, float h01, float lateral, float along)
    {
        if (regions == null || string.IsNullOrWhiteSpace(partName))
            return -1;
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < regions.Count; i++)
        {
            HarvestRegion r = regions[i];
            if (r == null || !r.Matches(partName)) continue;
            float score = Outside(h01, r.height) + Outside(along, r.length);
            if (r.side == BodySide.Left && lateral > 0f || r.side == BodySide.Right && lateral < 0f) score += 10f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    /// <summary>How far <paramref name="x"/> lies outside a band (0 inside it).</summary>
    private static float Outside(float x, Vector2 band)
    {
        float lo = Math.Min(band.x, band.y), hi = Math.Max(band.x, band.y);
        return x < lo ? lo - x : x > hi ? x - hi : 0f;
    }

    public HarvestMaterial Material(string materialId)
    {
        if (materials == null || string.IsNullOrEmpty(materialId))
            return null;
        for (int i = 0; i < materials.Count; i++)
            if (materials[i] != null && string.Equals(materials[i].id, materialId, StringComparison.OrdinalIgnoreCase))
                return materials[i];
        return null;
    }

    public int MaterialIndex(string materialId)
    {
        if (materials == null || string.IsNullOrEmpty(materialId))
            return -1;
        for (int i = 0; i < materials.Count; i++)
            if (materials[i] != null && string.Equals(materials[i].id, materialId, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>
    /// The region hits without a location mark: the default, else the meatiest that is not a weak point and takes no extra
    /// damage (a blast is not a blow to the vitals), else the meatiest.
    /// </summary>
    public int DefaultRegionIndex
    {
        get
        {
            int i = RegionIndex(defaultRegion);
            if (i >= 0 || regions == null)
                return i;
            float best = -1f, bestAny = -1f;
            int any = -1;
            for (int r = 0; r < regions.Count; r++)
            {
                HarvestRegion x = regions[r];
                if (x == null) continue;
                if (x.meat > bestAny) { bestAny = x.meat; any = r; }
                if (!x.weakPoint && x.damageMultiplier <= 1f && x.meat > best) { best = x.meat; i = r; }
            }
            return i >= 0 ? i : any;
        }
    }

    /// <summary>How much this creature resists <paramref name="element"/> (0 none .. 1 immune).</summary>
    public float Resistance(ElementType element)
    {
        if (resistances == null || element == ElementType.None)
            return 0f;
        float r = 0f;
        foreach (HarvestElementResistance e in resistances)
            if (e != null && e.element == element) r = Math.Max(r, e.resist);
        return HarvestMathF.Clamp01(r);
    }

    /// <summary>Is <paramref name="mobType"/> (or a mob named so) one this anatomy is for? Whole words, case-insensitive.</summary>
    public bool MatchesMob(string mobType, string mobName = null)
    {
        if (mobTypes == null)
            return false;
        foreach (string t in mobTypes)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (HarvestNames.HasWord(mobType, t) || HarvestNames.HasWord(mobName, t))
                return true;
        }
        return false;
    }

    /// <summary>A deep copy (the defaults hand out copies, so nothing edits the built-in content).</summary>
    public HarvestAnatomyData Clone()
    {
        var c = (HarvestAnatomyData)MemberwiseClone();
        c.mobTypes = mobTypes != null ? new List<string>(mobTypes) : new List<string>();
        c.resistances = new List<HarvestElementResistance>();
        if (resistances != null)
            foreach (HarvestElementResistance r in resistances)
                if (r != null) c.resistances.Add(new HarvestElementResistance { element = r.element, resist = r.resist });
        c.regions = new List<HarvestRegion>();
        if (regions != null)
            foreach (HarvestRegion r in regions)
                if (r != null) c.regions.Add(r.Clone());
        c.materials = new List<HarvestMaterial>();
        if (materials != null)
            foreach (HarvestMaterial m in materials)
                if (m != null) c.materials.Add(m.Clone());
        return c;
    }

    // ------------------------------------------------------------------ checking
    /// <summary>Setup problems: missing or duplicate ids, sources naming no region, requirements naming nothing.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(id))
            errors.Add("The anatomy has no id.");
        if (regions == null || regions.Count == 0)
        {
            errors.Add($"{Name}: no regions.");
            return;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (HarvestRegion r in regions)
        {
            if (r == null) { warnings.Add($"{Name}: an empty region."); continue; }
            if (string.IsNullOrWhiteSpace(r.id)) errors.Add($"{Name}: a region has no id.");
            else if (!ids.Add(r.id)) errors.Add($"{Name}: two regions are called '{r.id}'.");
            if (r.hide > 0f && (r.pelt.width <= 0f || r.pelt.height <= 0f)) warnings.Add($"{Name}/{r.id}: it has hide but no place on the pelt.");
        }
        if (peltSize.x < 2 || peltSize.y < 2)
            warnings.Add($"{Name}: the pelt is smaller than 2×2 cells.");
        // A part whose place on the pelt holds no cell is marked through the cells nearest it; on a pelt with a cell for
        // every part (most), that is a layout to fix.
        int[] cells = HarvestCondition.PeltCells(this);
        int present = 0, covered = 0;
        for (int r = 0; r < regions.Count; r++)
        {
            present += cells[r];
            if (regions[r] != null && regions[r].hide > 0f) covered++;
        }
        if (present >= covered)
            for (int r = 0; r < regions.Count; r++)
                if (regions[r] != null && regions[r].hide > 0f && regions[r].pelt.width > 0f && regions[r].pelt.height > 0f && cells[r] == 0)
                    warnings.Add($"{Name}/{regions[r].id}: its place on the pelt holds no cell of the {peltSize.x}×{peltSize.y} hide (hits there mark the cells nearest it): widen it, or make the pelt finer.");
        int hides = 0;
        if (materials != null)
            foreach (HarvestMaterial m in materials)
                if (HarvestCarcass.TakesHideOff(this, m)) hides++;
        var mids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (materials == null || materials.Count == 0)
            warnings.Add($"{Name}: it gives no materials.");
        else
            foreach (HarvestMaterial m in materials)
            {
                if (m == null) { warnings.Add($"{Name}: an empty material."); continue; }
                string where = $"{Name}/{m.id}";
                if (string.IsNullOrWhiteSpace(m.id)) errors.Add($"{Name}: a material has no id.");
                else if (!mids.Add(m.id)) errors.Add($"{Name}: two materials are called '{m.id}'.");
                if (m.sources == null || m.sources.Count == 0) errors.Add($"{where}: it comes from no region.");
                else
                    foreach (HarvestSource s in m.sources)
                    {
                        if (s == null) continue;
                        if (RegionIndex(s.region) < 0) errors.Add($"{where}: unknown region '{s.region}'.");
                        if (m.countable && s.units <= 0) warnings.Add($"{where}: no pieces from '{s.region}'.");
                    }
                if (!m.countable && m.yield.y <= 0f) warnings.Add($"{where}: it yields nothing (Yield max 0).");
                if (m.kind == HarvestMaterialKind.Hide && HarvestCarcass.IsHide(m) && string.IsNullOrWhiteSpace(m.craftHide))
                    warnings.Add($"{where}: no Crafting hide: it becomes a plain hide item, not a hide for the tannery.");
                if (hides > 1 && HarvestCarcass.TakesHideOff(this, m))
                    warnings.Add($"{where}: more than one hide is skinned from this creature (taking any of them takes the hide off).");
                if (m.requires != null)
                    foreach (string q in m.requires)
                    {
                        if (string.IsNullOrWhiteSpace(q) || HarvestMethods.IsStateToken(q)) continue;
                        if (Material(q) == null) errors.Add($"{where}: requires '{q}', which is not one of its materials.");
                        else if (string.Equals(q, m.id, StringComparison.OrdinalIgnoreCase)) errors.Add($"{where}: requires itself.");
                    }
                if (m.chance <= 0f) warnings.Add($"{where}: its chance is 0: it never exists.");
            }
    }
}

/// <summary>Defaults that depend on the method (tools, words).</summary>
public static class HarvestMethods
{
    /// <summary>Requirement tokens that are states of the carcass, not materials.</summary>
    public const string Hide = "hide", Cavity = "cavity", Gutted = "gutted";

    public static bool IsStateToken(string token) =>
        string.Equals(token, Hide, StringComparison.OrdinalIgnoreCase) || string.Equals(token, Cavity, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, Gutted, StringComparison.OrdinalIgnoreCase);

    /// <summary>The tools a method uses (by the shape for trophies: saws for horns, pliers for teeth and claws).</summary>
    public static HarvestToolKind DefaultTools(HarvestMethod method, string shape)
    {
        switch (method)
        {
            case HarvestMethod.Skinning: return HarvestToolKind.Skinning | HarvestToolKind.Scraper;
            case HarvestMethod.Butchering: return HarvestToolKind.Butchering | HarvestToolKind.Chopping;
            case HarvestMethod.Tendons: return HarvestToolKind.Fine | HarvestToolKind.Pliers;
            case HarvestMethod.Bones: return HarvestToolKind.Saw | HarvestToolKind.Scraper;
            case HarvestMethod.Organs: return HarvestToolKind.Fine;
            default:
                return IsSawn(shape) ? HarvestToolKind.Saw : HarvestToolKind.Pliers | HarvestToolKind.Fine;
        }
    }

    /// <summary>Trophies that are sawn off rather than worked loose (horns, antlers, tusks, beaks, a rabbit's foot).</summary>
    public static bool IsSawn(string shape) => shape == "horn" || shape == "antler" || shape == "tusk" || shape == "beak" || shape == "paw";

    /// <summary>Trophies worked loose with pliers (claws, teeth, talons, scales, plates).</summary>
    public static bool IsPried(string shape) => !IsSawn(shape);
}

/// <summary>Names made from ids ("foreleg_l" → "Left foreleg").</summary>
public static class HarvestNames
{
    public static string FromId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";
        string s = id.Trim();
        string side = "";
        if (s.EndsWith("_l", StringComparison.OrdinalIgnoreCase)) { side = "Left "; s = s.Substring(0, s.Length - 2); }
        else if (s.EndsWith("_r", StringComparison.OrdinalIgnoreCase)) { side = "Right "; s = s.Substring(0, s.Length - 2); }
        s = s.Replace('_', ' ');
        return side.Length > 0 ? side + s.ToLowerInvariant() : HarvestText.Capital(s);
    }

    /// <summary>Does <paramref name="text"/> contain <paramref name="word"/> as a whole word (case-insensitive)?</summary>
    public static bool HasWord(string text, string word)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(word))
            return false;
        string t = text.ToLowerInvariant(), w = word.Trim().ToLowerInvariant();
        int at = 0;
        while ((at = t.IndexOf(w, at, StringComparison.Ordinal)) >= 0)
        {
            bool startOk = at == 0 || !char.IsLetter(t[at - 1]);
            int end = at + w.Length;
            bool endOk = end >= t.Length || !char.IsLetter(t[end]) || (t[end] == 's' && (end + 1 >= t.Length || !char.IsLetter(t[end + 1])));
            if (startOk && endOk)
                return true;
            at = end;
        }
        return false;
    }
}

/// <summary>Float clamps (the data classes use floats).</summary>
public static class HarvestMathF
{
    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
}
