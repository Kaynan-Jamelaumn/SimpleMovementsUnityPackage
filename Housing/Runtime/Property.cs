using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a part of a property's land is for (not all land is garden).</summary>
public enum LandUse
{
    Garden,
    Yard,
    Orchard,
    Pasture,
    Workshop,
    Storage,
    Courtyard,
    Other,
}

/// <summary>A named part of a property's land with a purpose (a yard, a stable yard, a workshop corner).</summary>
[Serializable]
public class PropertyZone
{
    [Tooltip("Its name in the listing (\"Back yard\").")]
    public string name = "Yard";
    public LandUse use = LandUse.Yard;
    [Tooltip("Its outline in metres, in the property's space (X, Z).")]
    public List<Vector2> outline = new List<Vector2>();
}

/// <summary>A chest that belongs to a rented place, with the id its contents are saved under.</summary>
[Serializable]
public class HousingChest
{
    [Tooltip("Identifies the chest's contents in the save. Made automatically; keep it unique.")]
    public string id = "";
    public Storage storage;
}

/// <summary>
/// A house to rent, with its land: put it in the city and shape it in the Scene view - the <see cref="land"/> (the plot,
/// a polygon in metres) and the <see cref="house"/> on it (its footprint and floors), and parts of the land with a
/// purpose (<see cref="zones"/>; gardens are the Soil Fields on it). The sizes decide its base rent (Housing Settings),
/// the market decides what it rents for. Its chests, beds, doors and gardens open only for its tenant and those they let in.
/// <para>Areas are truthful: two properties may not overlap, a property may not overlap an inn's rooms, the house stays
/// on its land, and the Housing validator reports any other object of the city standing on the land.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Housing/Property")]
public class Property : MonoBehaviour
{
    [Tooltip("Identifies the property in the housing save and network messages. Made automatically; keep it unique.")]
    [SerializeField] private string propertyId = "";
    [Tooltip("Its name in the listing (\"Rose Cottage\").")]
    public string displayName = "House";
    [Tooltip("What the realtor says about it.")]
    [TextArea(2, 5)] public string description = "";
    [Tooltip("The part of the city it is in: prices move by district (Housing Settings has the districts and how wanted they are).")]
    public string district = "City";

    [Header("Land and house")]
    [Tooltip("The plot: its outline in metres, in this object's space (X, Z). Without a garden or a yard it can be just the house.")]
    public List<Vector2> land = HousingGeometry.Rectangle(Vector2.zero, new Vector2(16f, 12f));
    [Tooltip("The house: its footprint on the land, in metres, in this object's space (X, Z).")]
    public List<Vector2> house = HousingGeometry.Rectangle(new Vector2(0f, 2.5f), new Vector2(8f, 6f));
    [Tooltip("Floors of the house (its floor area is the footprint times the floors).")]
    [Range(1, 12)] public int floors = 1;
    [Tooltip("Metres from one floor to the next (the house's height, for the checks of what stands on the land).")]
    [Min(1.5f)] public float floorHeight = 3f;
    [Tooltip("Parts of the land with a purpose: a yard, an orchard, a workshop... (the gardens are its Soil Fields).")]
    public List<PropertyZone> zones = new List<PropertyZone>();

    [Header("Renting")]
    [Tooltip("It may be rented by the week.")]
    public bool weekly = true;
    [Tooltip("It may be rented by the month.")]
    public bool monthly = true;
    [Tooltip("How fine it is (multiplies the base rent its sizes give: 1 = ordinary).")]
    [Range(0.25f, 4f)] public float quality = 1f;
    [Tooltip("Base rent per day set by hand (0 = from its sizes and Housing Settings). The market moves the actual rent around it.")]
    [Min(0)] public int baseRentPerDay;

    [Header("What belongs to it")]
    [Tooltip("Its gardens (Soil Fields on the land). Children inside the land are found by themselves.")]
    public List<SoilField> gardens = new List<SoilField>();
    [Tooltip("Its chests: kept for the tenant; when a lease ends, what is left in them is held for its tenant to claim.")]
    public List<HousingChest> chests = new List<HousingChest>();
    [Tooltip("Its beds (only the tenant and those they let in sleep there).")]
    public List<SleepSpot> beds = new List<SleepSpot>();
    [Tooltip("Its doors (locked to everyone else).")]
    public List<LockableDoor> doors = new List<LockableDoor>();
    [Tooltip("Its animal pens (Animal Pens on the land, next to the gardens: a pasture, a coop, a sty...). Children inside the land are found by themselves.")]
    public List<AnimalPen> pens = new List<AnimalPen>();
    [Tooltip("Also take the Soil Fields, animal pens, chests, beds and doors among its children that are not listed.")]
    public bool includeChildren = true;
    [Tooltip("Its house models kept inside a prefab (not its children): their colliders are its own, not something standing on its land.")]
    public List<GameObject> parts = new List<GameObject>();

    /// <summary>Its id (made in the editor; a property saved without one gets one from its place in the scene, the same everywhere).</summary>
    public string PropertyId
    {
        get
        {
            if (string.IsNullOrEmpty(propertyId)) propertyId = Application.isPlaying ? HousingIds.Stable(this, "property") : NewId();
            return propertyId;
        }
    }

    /// <summary>Closed: another property has the same id (it would share its lease). Set at run time.</summary>
    public bool OutOfOrder { get; internal set; }

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "House" : displayName;
    public string District => string.IsNullOrWhiteSpace(district) ? "City" : district.Trim();

    public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

    private void Reset()
    {
        // Reset keeps the ids it had (Unity empties the fields first; the editor remembered them): a tenant's lease, orders
        // and chests are named by them.
        propertyId = HousingIds.Recall(this, "property") ?? NewId();
        Collect();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(propertyId)) propertyId = NewId();
        HousingIds.Remember(this, "property", propertyId);
        foreach (HousingChest c in chests)
        {
            if (c == null) continue;
            if (string.IsNullOrEmpty(c.id)) c.id = NewId();
            HousingIds.Remember(c.storage, "chest", c.id);
        }
        if (!weekly && !monthly) weekly = true;
    }

    private void Awake()
    {
        if (!Application.isPlaying) return;
        Collect();
        HousingDirector.Register(this);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying) HousingDirector.Unregister(this);
    }

    /// <summary>
    /// Adds the Soil Fields, chests, beds and doors among the children that are not listed (with Include Children), and
    /// drops what is listed but lies under another property (a garden moved there, a copy still pointing outside itself):
    /// that one is the other's.
    /// </summary>
    public void Collect()
    {
        gardens.RemoveAll(x => x == null || UnderOther(x));
        chests.RemoveAll(x => x == null || x.storage == null || UnderOther(x.storage));
        beds.RemoveAll(x => x == null || UnderOther(x));
        doors.RemoveAll(x => x == null || UnderOther(x));
        pens.RemoveAll(x => x == null || UnderOther(x));
        if (!includeChildren) return;
        foreach (SoilField f in GetComponentsInChildren<SoilField>(true))
            if (!gardens.Contains(f)) gardens.Add(f);
        foreach (AnimalPen a in GetComponentsInChildren<AnimalPen>(true))
            if (!pens.Contains(a)) pens.Add(a);
        foreach (Storage s in GetComponentsInChildren<Storage>(true))
        {
            bool listed = false;
            foreach (HousingChest c in chests) listed |= c != null && c.storage == s;
            // In play a chest not listed gets an id from its place (the same next session), never a random one.
            if (!listed) chests.Add(new HousingChest { id = Application.isPlaying ? HousingIds.Stable(s, "chest") : HousingIds.Recall(s, "chest") ?? NewId(), storage = s });
        }
        foreach (HousingChest c in chests)
            if (c != null && c.storage != null && string.IsNullOrEmpty(c.id)) c.id = Application.isPlaying ? HousingIds.Stable(c.storage, "chest") : NewId();
        foreach (SleepSpot b in GetComponentsInChildren<SleepSpot>(true))
            if (!beds.Contains(b)) beds.Add(b);
        foreach (LockableDoor d in GetComponentsInChildren<LockableDoor>(true))
            if (!doors.Contains(d)) doors.Add(d);
        gardens.RemoveAll(x => x == null || UnderOther(x));
        chests.RemoveAll(x => x == null || x.storage == null || UnderOther(x.storage));
        beds.RemoveAll(x => x == null || UnderOther(x));
        doors.RemoveAll(x => x == null || UnderOther(x));
        pens.RemoveAll(x => x == null || UnderOther(x));
    }

    /// <summary>Does the thing lie under another property (nested in it, or moved there)?</summary>
    private bool UnderOther(Component c)
    {
        Property owner = c.GetComponentInParent<Property>(true);
        return owner != null && owner != this;
    }

    // ------------------------------------------------------------------ geometry
    /// <summary>Property-local ground-plane coordinates (X, Z) of a world point.</summary>
    public Vector2 Local(Vector3 world) => HousingGeometry.ToLocal(world, transform.position, transform.rotation);

    /// <summary>The world point at local (X, Z), at the property's height.</summary>
    public Vector3 World(Vector2 local, float lift = 0f) => transform.position + transform.rotation * new Vector3(local.x, lift, local.y);

    /// <summary>The land's outline as world X, Z.</summary>
    public List<Vector2> LandWorld() => HousingGeometry.ToWorld(land, transform.position, transform.rotation);

    /// <summary>The house's footprint as world X, Z.</summary>
    public List<Vector2> HouseWorld() => HousingGeometry.ToWorld(house, transform.position, transform.rotation);

    /// <summary>Is the world point on the land?</summary>
    public bool Contains(Vector3 world) => HousingGeometry.Contains(land, Local(world));

    /// <summary>Is the world point inside the house (its footprint, between the ground and its roof)?</summary>
    public bool InHouse(Vector3 world)
    {
        float y = world.y - transform.position.y;
        return y > -1f && y < floors * floorHeight + 1f && HousingGeometry.Contains(house, Local(world));
    }

    /// <summary>Metres from a world point to the land (0 on it).</summary>
    public float DistanceFromLand(Vector3 world) => HousingGeometry.Distance(land, Local(world));

    /// <summary>The land's box in the world (from the ground to the roof).</summary>
    public Bounds WorldBounds()
    {
        Rect r = HousingGeometry.Bounds(land.Count >= 3 ? land : house);
        var b = new Bounds(World(r.center), Vector3.zero);
        float top = floors * floorHeight + 1f;
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector2((i & 1) == 0 ? r.xMin : r.xMax, (i & 2) == 0 ? r.yMin : r.yMax);
            b.Encapsulate(World(p, -0.5f));
            b.Encapsulate(World(p, top));
        }
        return b;
    }

    /// <summary>The land's area, square metres.</summary>
    public float LandArea => HousingGeometry.Area(land);

    /// <summary>The house's footprint, square metres.</summary>
    public float HouseFootprint => HousingGeometry.Area(house);

    /// <summary>The house's floor area (footprint times floors), square metres.</summary>
    public float HouseFloorArea => HouseFootprint * Mathf.Max(1, floors);

    /// <summary>The gardens' area (their Soil Fields' outlines), square metres.</summary>
    public float GardenArea
    {
        get
        {
            float a = 0f;
            foreach (SoilField f in gardens)
                if (f != null && f.layout != null) a += f.layout.Area();
            return a;
        }
    }

    /// <summary>The animal pens' area (their outlines), square metres: pens are pasture land.</summary>
    public float PenArea
    {
        get
        {
            float a = 0f;
            foreach (AnimalPen pen in pens)
                if (pen != null) a += pen.Area;
            return a;
        }
    }

    /// <summary>The area of the zones of one use, square metres.</summary>
    public float ZoneArea(LandUse use)
    {
        float a = use == LandUse.Garden ? GardenArea : use == LandUse.Pasture ? PenArea : 0f;
        foreach (PropertyZone z in zones)
            if (z != null && z.use == use) a += HousingGeometry.Area(z.outline);
        return a;
    }

    /// <summary>Does the property have a garden?</summary>
    public bool HasGarden => GardenArea > 0.01f;

    /// <summary>The facts the rules price it by.</summary>
    public PropertyFacts Facts()
    {
        var f = new PropertyFacts
        {
            id = PropertyId,
            name = DisplayName,
            district = District,
            landArea = LandArea,
            houseFootprint = HouseFootprint,
            floors = Mathf.Max(1, floors),
            gardenArea = GardenArea, // its Soil Fields (garden zones without one are priced as zones: zoneArea[Garden])
            quality = quality,
            baseRentPerDay = baseRentPerDay,
            weekly = weekly,
            monthly = monthly || !weekly,
        };
        foreach (PropertyZone z in zones)
            if (z != null) f.zoneArea[(int)z.use] += HousingGeometry.Area(z.outline);
        // Its animal pens are pasture land (priced and listed as such).
        f.zoneArea[(int)LandUse.Pasture] += PenArea;
        return f;
    }

    private void OnDrawGizmos()
    {
        DrawOutline(land, new Color(0.35f, 0.75f, 0.4f, 0.9f), 0.04f);
        DrawOutline(house, new Color(0.9f, 0.65f, 0.3f, 0.9f), 0.06f);
        foreach (PropertyZone z in zones)
            if (z != null) DrawOutline(z.outline, new Color(0.5f, 0.6f, 0.9f, 0.7f), 0.05f);
    }

    private void DrawOutline(List<Vector2> pts, Color c, float lift)
    {
        if (pts == null || pts.Count < 2) return;
        Gizmos.color = c;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            Gizmos.DrawLine(World(pts[j], lift), World(pts[i], lift));
    }
}
