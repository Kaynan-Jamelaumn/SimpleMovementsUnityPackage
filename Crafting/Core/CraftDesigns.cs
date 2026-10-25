using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One point of a shape's profile along its length: at <see cref="t"/> (0 = the start, 1 = the end) it is this wide and thick (mm).</summary>
[Serializable]
public class CraftProfilePoint
{
    [Range(0f, 1f)] public float t;
    [Min(0f)] public float width = 30f;
    [Min(0f)] public float thickness = 6f;

    public CraftProfilePoint() { }

    public CraftProfilePoint(float t, float width, float thickness)
    {
        this.t = t;
        this.width = width;
        this.thickness = thickness;
    }
}

/// <summary>
/// The shape a metal (or wooden) component should have: its length, a profile of widths and thicknesses along it, where
/// its edge is, a tang, an eye, a curve, a dish, the mass range. It is a target, not a recipe: how close the piece comes
/// is part of its workmanship, and the piece's actual geometry decides how it performs.
/// </summary>
[Serializable]
public class CraftShapeTarget
{
    [Tooltip("Total length (mm).")]
    public float lengthMin = 200f;
    public float lengthMax = 300f;
    [Tooltip("Width and thickness along the length (t 0-1). At least two points.")]
    public List<CraftProfilePoint> profile = new List<CraftProfilePoint>();
    public CraftEdgeKind edge = CraftEdgeKind.None;
    [Tooltip("Where along the length the edge runs (t).")]
    [Range(0f, 1f)] public float edgeFrom = 0.15f;
    [Range(0f, 1f)] public float edgeTo = 1f;
    [Tooltip("The edge's best inclusive angle (degrees): sharper cuts, wider lasts.")]
    [Range(5f, 90f)] public float edgeAngle = 30f;
    [Tooltip("The first part of the length (t) is a tang that goes into a handle (narrow, unhardened is fine).")]
    [Range(0f, 0.6f)] public float tangTo;
    [Tooltip("The bend it should have along its length (degrees in total): a sabre's curve.")]
    public float curvature;
    [Tooltip("How deep a plate should be dished (mm): a helmet's dome, a shield boss.")]
    [Min(0f)] public float dish;
    [Tooltip("It needs an eye (a hole for a haft, a tang slot, a socket) at Eye At (t) of Eye Diameter (mm).")]
    public bool eye;
    [Range(0f, 1f)] public float eyeAt = 0.2f;
    [Min(0f)] public float eyeDiameter = 25f;
    [Tooltip("Mass range (g).")]
    public float massMin = 100f;
    public float massMax = 2000f;
    [Tooltip("It is meant to be hardened (and tempered): a soft blade dulls and bends.")]
    public bool hardened;

    public CraftShapeTarget Point(float t, float width, float thickness)
    {
        profile.Add(new CraftProfilePoint(t, width, thickness));
        return this;
    }

    /// <summary>The target width and thickness at <paramref name="t"/> (between the profile's points).</summary>
    public void At(float t, out float width, out float thickness)
    {
        width = thickness = 0f;
        if (profile == null || profile.Count == 0)
            return;
        if (t <= profile[0].t)
        {
            width = profile[0].width;
            thickness = profile[0].thickness;
            return;
        }
        for (int i = 1; i < profile.Count; i++)
        {
            CraftProfilePoint a = profile[i - 1], b = profile[i];
            if (t <= b.t)
            {
                float k = b.t - a.t > 1e-5f ? (t - a.t) / (b.t - a.t) : 1f;
                width = Mathf.Lerp(a.width, b.width, k);
                thickness = Mathf.Lerp(a.thickness, b.thickness, k);
                return;
            }
        }
        CraftProfilePoint last = profile[profile.Count - 1];
        width = last.width;
        thickness = last.thickness;
    }
}

/// <summary>
/// A leather pattern: its footprint in hide cells (a mask of rows: 'X' is part of it, '.' is not). Cutting it from a
/// hide takes those cells - flaws included - into a component.
/// </summary>
[Serializable]
public class CraftPattern
{
    public string id = "";
    public string displayName = "";
    [Tooltip("Rows of the footprint, top first: 'X' = part of the pattern, '.' = not.")]
    public List<string> rows = new List<string>();
    [Tooltip("Stitch holes along its seams (for stitching joints).")]
    [Min(0)] public int seamHoles = 20;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    public int Width
    {
        get
        {
            int w = 0;
            if (rows != null)
                foreach (string r in rows)
                    if (r != null && r.Length > w) w = r.Length;
            return w;
        }
    }
    public int Height => rows != null ? rows.Count : 0;

    /// <summary>Is cell (x, y) of the footprint part of the pattern (rotated by <paramref name="quarterTurns"/>)?</summary>
    public bool Covers(int x, int y, int quarterTurns = 0)
    {
        int w = Width, h = Height;
        int q = ((quarterTurns % 4) + 4) % 4;
        int sx, sy;
        switch (q)
        {
            case 1: sx = y; sy = h - 1 - x; break;
            case 2: sx = w - 1 - x; sy = h - 1 - y; break;
            case 3: sx = w - 1 - y; sy = x; break;
            default: sx = x; sy = y; break;
        }
        if (sy < 0 || sy >= h || sx < 0)
            return false;
        string row = rows[sy];
        return row != null && sx < row.Length && (row[sx] == 'X' || row[sx] == 'x');
    }

    /// <summary>Footprint size after <paramref name="quarterTurns"/>.</summary>
    public void Size(int quarterTurns, out int w, out int h)
    {
        bool sideways = (((quarterTurns % 4) + 4) % 4) % 2 == 1;
        w = sideways ? Height : Width;
        h = sideways ? Width : Height;
    }

    /// <summary>Cells in the footprint.</summary>
    public int Area
    {
        get
        {
            int n = 0;
            if (rows != null)
                foreach (string r in rows)
                    if (r != null)
                        foreach (char c in r)
                            if (c == 'X' || c == 'x') n++;
            return n;
        }
    }
}

/// <summary>What a leather component of a design should be like.</summary>
[Serializable]
public class CraftLeatherSpec
{
    [Tooltip("The pattern it is cut with.")]
    public string pattern = "";
    [Tooltip("Thickness range (mm).")]
    public float thicknessMin = 2f;
    public float thicknessMax = 5f;
    [Tooltip("At least this supple (0 rigid - 1 limp). Gloves need suppleness; armour plates of leather do not.")]
    [Range(0f, 1f)] public float suppleMin;
    [Tooltip("At most this supple (hardened leather armour wants rigidity).")]
    [Range(0f, 1f)] public float suppleMax = 1f;
}

/// <summary>What a wooden component should be like.</summary>
[Serializable]
public class CraftWoodSpec
{
    public float lengthMin = 90f;
    public float lengthMax = 130f;
    [Tooltip("Thickness of the grip or haft (mm).")]
    public float diameterMin = 24f;
    public float diameterMax = 36f;
    [Tooltip("The least toughness (shock resistance) the wood should have.")]
    [Range(0f, 1f)] public float toughnessMin = 0.3f;
}

/// <summary>What a component does in the finished item (how the evaluator reads it). Serialized as numbers: add at the END.</summary>
public enum CraftComponentFunction
{
    /// <summary>The cutting part (blade, axe head, spear head).</summary>
    Edge,
    /// <summary>The striking part of a blunt weapon or tool (mace head, hammer, pick).</summary>
    Striking,
    /// <summary>A crossguard.</summary>
    Guard,
    /// <summary>The grip or haft.</summary>
    Grip,
    /// <summary>A counterweight (pommel).</summary>
    Counterweight,
    /// <summary>A protective plate or panel.</summary>
    Protection,
    /// <summary>What a plate sits on (leather backing, a liner).</summary>
    Backing,
    /// <summary>A strap.</summary>
    Strap,
    /// <summary>Rivets, studs, buckles.</summary>
    Fastener,
    /// <summary>Padding.</summary>
    Padding,
    /// <summary>A wrap on a grip.</summary>
    Wrap,
    /// <summary>Thread for the seams.</summary>
    Thread,
}

/// <summary>One part of a design: its role, what it is made of, the shape or pattern it should have.</summary>
[Serializable]
public class CraftComponentSpec
{
    [Tooltip("Stable role id (\"blade\", \"guard\", \"grip\", \"pommel\", \"plate\", \"straps\"...).")]
    public string role = "";
    public string displayName = "";
    [Tooltip("What it is made of: Metal, Hide (leather), Wood, Fasteners. Supplies (padding, thread) use Kind = Metal with Supply set.")]
    public CraftPieceKind kind = CraftPieceKind.Metal;
    public CraftComponentFunction function = CraftComponentFunction.Edge;
    [Tooltip("How many of this part (two bracers, two plates).")]
    [Min(1)] public int count = 1;
    [Tooltip("The item can be finished without it.")]
    public bool optional;
    [Tooltip("Metal and wood parts: their target shape.")]
    public CraftShapeTarget shape = new CraftShapeTarget();
    [Tooltip("Leather parts.")]
    public CraftLeatherSpec leather = new CraftLeatherSpec();
    [Tooltip("Wooden parts.")]
    public CraftWoodSpec wood = new CraftWoodSpec();
    [Tooltip("Fasteners: what kind and how many.")]
    public CraftFastenerKind fastener = CraftFastenerKind.Rivet;
    [Min(0)] public int fastenerCount;
    [Tooltip("A supply used instead of a piece (padding, thread): its kind; the supply is taken from the inventory.")]
    public bool supply;
    public CraftSupplyKind supplyKind = CraftSupplyKind.Padding;
    [Min(0)] public int supplyCount = 1;
    [Tooltip("How much this part counts in the item's quality.")]
    [Min(0f)] public float importance = 1f;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? role : displayName;
}

/// <summary>A joint of a design: what holds two of its parts together, and how it is made.</summary>
[Serializable]
public class CraftJointSpec
{
    public string id = "";
    public string displayName = "";
    public CraftJointKind kind = CraftJointKind.Rivet;
    [Tooltip("The parts it joins (roles).")]
    public string roleA = "";
    public string roleB = "";
    [Tooltip("Rivets, stitches or turns it needs.")]
    [Min(0)] public int count = 4;
    [Tooltip("How much it counts in the item's integrity.")]
    [Min(0f)] public float importance = 1f;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>
/// An item design: what it is (a sword, a breastplate), its parts and the joints between them, and the reference
/// numbers its performance is measured against. A design gives target shapes and what fits together; how the finished
/// item performs comes from the materials and the work - two swords of one design can be very different.
/// </summary>
[Serializable]
public class CraftDesign
{
    public string id = "";
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    public CraftDesignKind kind = CraftDesignKind.Weapon;
    public WeaponCategory weaponCategory = WeaponCategory.Sword;
    public ToolType toolType = ToolType.None;
    [Tooltip("What its hits do (None = from the category).")]
    public DamageForm damageForm = DamageForm.None;
    public WeaponScaling scaling = WeaponScaling.Strength;
    public ArmorSlotType armorSlot = ArmorSlotType.Chestplate;
    [Tooltip("An item asset the finished item copies its moves, model and animations from (a weapon with its attacks, an armour " +
             "piece with its model). Its numbers are replaced by the crafted item's. Empty = a plain item (no attacks).")]
    public ItemSO template;
    public List<CraftComponentSpec> components = new List<CraftComponentSpec>();
    public List<CraftJointSpec> joints = new List<CraftJointSpec>();

    [Header("Weapons and tools")]
    [Tooltip("Damage of a well-made one of reference mass (min and max).")]
    public float damageMin = 10f;
    public float damageMax = 14f;
    public float critChance = 0.06f;
    public float critMultiplier = 1.6f;
    public float knockback = 0.5f;
    [Tooltip("Mass the reference damage and speed are for (g).")]
    public float referenceMass = 1100f;
    [Tooltip("Where its balance should be: mm in front of the grip's guard end (swords ~120), or along a haft.")]
    public float idealBalance = 120f;
    [Tooltip("Tool damage (harvesting).")]
    public float toolDamage = 10f;

    [Header("Armour and shields")]
    [Tooltip("Defense of a well-made one with reference protection.")]
    public float defense = 10f;
    public float magicDefense;

    [Header("Both")]
    [Tooltip("Uses before it breaks, well made.")]
    public int durability = 200;
    [Tooltip("Value of a well-made one.")]
    public float price = 50f;
    [Tooltip("Cells in the grid inventory.")]
    public Vector2Int gridSize = new Vector2Int(1, 3);

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    public CraftComponentSpec Component(string role)
    {
        if (components != null)
            foreach (CraftComponentSpec c in components)
                if (c != null && string.Equals(c.role, role, StringComparison.OrdinalIgnoreCase))
                    return c;
        return null;
    }

    public CraftJointSpec Joint(string jointId)
    {
        if (joints != null)
            foreach (CraftJointSpec j in joints)
                if (j != null && string.Equals(j.id, jointId, StringComparison.OrdinalIgnoreCase))
                    return j;
        return null;
    }

    /// <summary>The part that does the work (the blade, the head, the main plate).</summary>
    public CraftComponentSpec MainComponent
    {
        get
        {
            if (components == null || components.Count == 0)
                return null;
            foreach (CraftComponentSpec c in components)
                if (c != null && (c.function == CraftComponentFunction.Edge || c.function == CraftComponentFunction.Striking))
                    return c;
            foreach (CraftComponentSpec c in components)
                if (c != null && c.function == CraftComponentFunction.Protection)
                    return c;
            return components[0];
        }
    }

    public bool IsWeaponLike => kind == CraftDesignKind.Weapon || kind == CraftDesignKind.Tool;
}
