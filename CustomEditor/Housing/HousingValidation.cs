#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SimpleMovements.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The housing objects of the open scenes, gathered for a run of checks (<see cref="Open"/>), or kept between the
/// inspectors' checks and gathered again only when objects are made, destroyed or moved under others (<see cref="Cached"/>).
/// The colliders standing among them are not gathered: the check of what stands on a land asks the physics scene for
/// those near that land (a big city is never walked through for one plot), unless <see cref="colliders"/> names the only
/// ones to look at (<see cref="Of"/>: a test's own objects).
/// </summary>
public sealed class HousingSceneObjects
{
    public readonly List<Property> properties = new List<Property>();
    public readonly List<Inn> inns = new List<Inn>();
    public readonly List<InnRoom> rooms = new List<InnRoom>();
    public readonly List<SoilField> fields = new List<SoilField>();
    public readonly List<LockableDoor> doors = new List<LockableDoor>();
    public readonly List<Realtor> realtors = new List<Realtor>();
    public readonly List<Innkeeper> innkeepers = new List<Innkeeper>();
    /// <summary>
    /// The only colliders the check of what stands on a land measures (<see cref="Of"/>: those of the given objects).
    /// Null (the open scenes): the physics scene is asked for the colliders near each land.
    /// </summary>
    public List<Collider> colliders;

    private static HousingSceneObjects cached;
    private static bool watching;

    /// <summary>Everything of the open scenes (inactive objects too), gathered now.</summary>
    public static HousingSceneObjects Open()
    {
        var s = new HousingSceneObjects();
        foreach (GameObject root in AgricultureEditors.OpenSceneRoots()) s.Gather(root);
        return s;
    }

    /// <summary>
    /// Only what is under <paramref name="roots"/> (inactive objects too), with the colliders of its active objects: the
    /// checks see nothing else of the open scenes.
    /// </summary>
    public static HousingSceneObjects Of(params GameObject[] roots)
    {
        var s = new HousingSceneObjects { colliders = new List<Collider>() };
        if (roots == null) return s;
        foreach (GameObject root in roots)
        {
            if (root == null || Inside(root.transform.parent, roots)) continue; // gathered with an object above it
            s.Gather(root);
            s.colliders.AddRange(root.GetComponentsInChildren<Collider>(false));
        }
        return s;
    }

    private static bool Inside(Transform t, GameObject[] roots)
    {
        for (; t != null; t = t.parent)
            if (Array.IndexOf(roots, t.gameObject) >= 0) return true;
        return false;
    }

    /// <summary>
    /// <see cref="Open"/>, kept until objects are made, destroyed, moved under others, their components added or removed,
    /// a scene opened or closed, or an undo (the inspectors check twice a second and never walk a big city again for
    /// nothing). A change is seen from the editor's next update on.
    /// </summary>
    public static HousingSceneObjects Cached()
    {
        if (!watching)
        {
            watching = true;
            EditorApplication.hierarchyChanged += Forget;
            Undo.undoRedoPerformed += Forget;
            ObjectChangeEvents.changesPublished += Changes;
            EditorSceneManager.sceneOpened += (scene, mode) => Forget();
            EditorSceneManager.sceneClosed += scene => Forget();
            EditorApplication.playModeStateChanged += state => Forget();
        }
        return cached ??= Open();
    }

    /// <summary>Makes <see cref="Cached"/> gather the open scenes again.</summary>
    public static void Forget() => cached = null;

    private static void Changes(ref ObjectChangeEventStream stream)
    {
        for (int i = 0; i < stream.length; i++)
        {
            ObjectChangeKind kind = stream.GetEventType(i);
            if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties || kind == ObjectChangeKind.ChangeAssetObjectProperties) continue;
            cached = null; // made, destroyed, moved under another, components added or removed, a scene changed
            return;
        }
    }

    private void Gather(GameObject root)
    {
        if (root == null) return;
        properties.AddRange(root.GetComponentsInChildren<Property>(true));
        inns.AddRange(root.GetComponentsInChildren<Inn>(true));
        rooms.AddRange(root.GetComponentsInChildren<InnRoom>(true));
        fields.AddRange(root.GetComponentsInChildren<SoilField>(true));
        doors.AddRange(root.GetComponentsInChildren<LockableDoor>(true));
        realtors.AddRange(root.GetComponentsInChildren<Realtor>(true));
        innkeepers.AddRange(root.GetComponentsInChildren<Innkeeper>(true));
    }
}

/// <summary>
/// What overlaps a property's land or an inn's area (<see cref="HousingValidation.OverlapsOf(Property)"/>,
/// <see cref="HousingValidation.OverlapsOf(Inn)"/>): one message per problem - errors (another place's area, a soil
/// field that is not its garden or is also another's, an object standing on it by 1 m² or more) and warnings (an object
/// standing on it by less, from Housing Settings' Intrusion Warn Area) - with the object each message is about.
/// </summary>
public sealed class HousingOverlaps
{
    public readonly List<string> errors = new List<string>();
    public readonly List<string> warnings = new List<string>();
    /// <summary>What each error is about: <c>errors[i]</c> is about <c>errorObjects[i]</c> (the other place, the soil field, the object).</summary>
    public readonly List<Object> errorObjects = new List<Object>();
    /// <summary>What each warning is about: <c>warnings[i]</c> is about <c>warningObjects[i]</c>.</summary>
    public readonly List<Object> warningObjects = new List<Object>();
    /// <summary>The other properties and inns whose areas overlap it (the Scene view fills them red).</summary>
    public readonly List<Component> places = new List<Component>();

    /// <summary>How many messages.</summary>
    public int Count => errors.Count + warnings.Count;

    internal void Error(string message, Object about)
    {
        errors.Add(message);
        errorObjects.Add(about);
    }

    internal void Warning(string message, Object about)
    {
        warnings.Add(message);
        warningObjects.Add(about);
    }
}

/// <summary>
/// Housing's checks, and its part of the Project Validator: areas are truthful.
/// <list type="bullet">
/// <item>Each property: its land and house (simple outlines, big enough, the house on its land), its zones and gardens on
/// the land and apart, what belongs to it on the land, a Lockable Door, scale 1, its lease terms, chests that would roll
/// random items or lose their contents (<see cref="CheckProperty"/>).</item>
/// <item>Each inn: its area, its rooms inside it and apart (and not lying under another inn), each with a bed, a chest
/// and a Lockable Door whose way out is not another room, its price, the rooms' ids (<see cref="CheckInn"/>,
/// <see cref="CheckRoom"/>).</item>
/// <item>The open scenes: lands overlapping other lands, inns or soil fields that are not their gardens, gardens two
/// properties hold, and objects of the city standing on them (<see cref="OverlapsOf(Property)"/>); duplicated ids;
/// the Housing Settings, the Realtors and Innkeepers (an Innkeeper for every inn, none without an inn), and the City
/// scene (<see cref="CheckProject"/>, <see cref="CheckPlaces"/>).</item>
/// </list>
/// Places are compared in one place's own space, so plots kilometres from the world's origin that only share a fence
/// never overlap. In the Project Validator the per-object checks run with Game Setup, the open scenes' checks once per
/// run with Project Assets or Game Setup. Tools ▸ SimpleMovements ▸ Housing ▸ Validate Housing logs everything to the
/// Console (the Project Validator has the fix buttons).
/// </summary>
[InitializeOnLoad]
public static class HousingValidation
{
    private const string Category = "Housing";

    /// <summary>The least area of a property's land, square metres.</summary>
    public const float MinLandArea = 4f;
    /// <summary>The least footprint of a house, square metres.</summary>
    public const float MinHouseArea = 2f;
    /// <summary>The share of an area that may lie outside the area holding it (snapping, rounding) before it is reported.</summary>
    public const float OutsideSlack = 0.02f;
    /// <summary>How far (metres) a chest, bed or door may stand off its land before it is reported.</summary>
    public const float OffLandSlack = 0.5f;
    /// <summary>An object standing on a land by this many square metres or more is an error (below it, a warning).</summary>
    public const float IntrusionErrorArea = 1f;
    /// <summary>An object whose top is at most this many metres above the property is flat ground (a road, a pavement).</summary>
    public const float FlatGround = 0.2f;
    /// <summary>
    /// What lies wholly higher than this many metres above the house's roof (Floors × Floor Height) passes over the land
    /// (a bridge, an arch, a neighbour's balcony): it does not stand on it.
    /// </summary>
    public const float RoofClearance = 0.5f;
    /// <summary>Rooms this many metres or more apart in height are on different floors: they never overlap.</summary>
    public const float FloorSeparation = InnRoom.StoreyHeight; // the same storey the runtime uses for "inside the room"
    /// <summary>How far (metres) in front of a room's door one steps out of the room (where the corridor must be).</summary>
    public const float DoorStep = 0.75f;

    static HousingValidation()
    {
        ProjectValidator.ValidateRootWithChecks += (root, checks, issues) =>
        {
            if ((checks & ValidationChecks.GameSetup) != 0) CheckRoot(root, issues);
        };
        ProjectValidator.ValidateProject += (checks, issues) =>
        {
            if ((checks & (ValidationChecks.ProjectAssets | ValidationChecks.GameSetup)) != 0) CheckProject(issues);
        };
    }

    // ================================================================== findings
    /// <summary>One problem: what, about which object, and how to fix it (when it can be).</summary>
    private sealed class Finding
    {
        public IssueSeverity severity;
        public string message;
        public Object context;
        public string fixLabel;
        public Func<bool> fix;
    }

    private static void Add(List<Finding> into, IssueSeverity severity, string message, Object context, string fixLabel = null, Func<bool> fix = null) =>
        into.Add(new Finding { severity = severity, message = message, context = context, fixLabel = fixLabel, fix = fix });

    private static void Split(List<Finding> findings, List<string> errors, List<string> warnings)
    {
        foreach (Finding f in findings)
        {
            if (f.severity == IssueSeverity.Error) errors?.Add(f.message);
            else warnings?.Add(f.message);
        }
    }

    // ================================================================== properties
    /// <summary>
    /// The property's own problems (no other object of the scene is looked at - see <see cref="OverlapsOf(Property)"/>):
    /// its land (3 points or more, not crossing itself, at least <see cref="MinLandArea"/> m²), its house (the same, at
    /// least <see cref="MinHouseArea"/> m², on its land), its zones (on the land, off the house, apart), its gardens, chests,
    /// beds and doors on the land, scale 1, at least one lease term, its chests (an id each, none rolling random items, none
    /// missing from the list), its district known to the Housing Settings.
    /// </summary>
    public static void CheckProperty(Property p, List<string> errors, List<string> warnings)
    {
        var findings = new List<Finding>();
        PropertyFindings(p, findings);
        Split(findings, errors, warnings);
    }

    private static void PropertyFindings(Property p, List<Finding> into)
    {
        if (p == null) return;
        const IssueSeverity E = IssueSeverity.Error, W = IssueSeverity.Warning;
        List<Vector2> land = p.land, house = p.house;
        bool landSimple = land != null && land.Count >= 3 && HousingGeometry.IsSimple(land);
        bool houseSimple = house != null && house.Count >= 3 && HousingGeometry.IsSimple(house);

        if (land == null || land.Count < 3) Add(into, E, "The land needs at least 3 points (its outline): use Make Rectangle or the Land tool.", p);
        else if (!landSimple) Add(into, E, "The land's outline crosses itself: drag its points apart (Land tool).", p);
        else if (HousingGeometry.Area(land) < MinLandArea)
            Add(into, E, $"The land is too small ({HousingGeometry.Area(land):0.##} m²; at least {MinLandArea:0} m²).", p);

        if (house == null || house.Count < 3) Add(into, E, "The house needs at least 3 points (its footprint): use Make Rectangle or the House tool.", p);
        else if (!houseSimple) Add(into, E, "The house's footprint crosses itself: drag its points apart (House tool).", p);
        else if (HousingGeometry.Area(house) < MinHouseArea)
            Add(into, E, $"The house is too small ({HousingGeometry.Area(house):0.##} m²; at least {MinHouseArea:0} m²).", p);

        if (landSimple && houseSimple && !HousingGeometry.Holds(land, house))
        {
            float outside = Mathf.Max(0f, HousingGeometry.Area(house) - HousingGeometry.SharedArea(land, house));
            Add(into, E, $"The house is not on its land ({outside:0.#} m² of its footprint is outside it).", p);
        }

        // Zones: inside the land, off the house, apart (their areas price the land).
        if (p.zones != null)
            for (int i = 0; i < p.zones.Count; i++)
            {
                PropertyZone z = p.zones[i];
                if (z == null) continue;
                string zn = ZoneName(z, i);
                if (z.outline == null || z.outline.Count < 3)
                {
                    Add(into, E, $"The zone '{zn}' needs at least 3 points.", p);
                    continue;
                }
                if (!HousingGeometry.IsSimple(z.outline))
                {
                    Add(into, E, $"The zone '{zn}' crosses itself: drag its points apart (Zones tool).", p);
                    continue;
                }
                if (landSimple)
                {
                    float outside = HousingGeometry.OutsideShare(land, z.outline);
                    if (outside > OutsideSlack) Add(into, W, $"The zone '{zn}' reaches outside the land ({outside * 100f:0}% of it).", p);
                }
                if (houseSimple)
                {
                    float over = HousingGeometry.SharedArea(house, z.outline);
                    if (over > HousingGeometry.DefaultOverlapArea) Add(into, W, $"The zone '{zn}' lies over the house ({over:0.#} m²): that ground counts twice in the rent.", p);
                }
                for (int j = i + 1; j < p.zones.Count; j++)
                {
                    PropertyZone y = p.zones[j];
                    if (y == null || y.outline == null || y.outline.Count < 3 || !HousingGeometry.IsSimple(y.outline)) continue;
                    float shared = HousingGeometry.SharedArea(z.outline, y.outline);
                    if (shared > HousingGeometry.DefaultOverlapArea)
                        Add(into, W, $"The zones '{zn}' and '{ZoneName(y, j)}' overlap by {shared:0.#} m² (that ground counts twice in the rent).", p);
                }
            }

        // Gardens: their outlines (converted into the property's space) on the land, off the house, off the zones, apart.
        List<SoilField> gardenFields = HousingEditors.GardensOf(p);
        var gardenOutlines = new List<List<Vector2>>(gardenFields.Count);
        foreach (SoilField f in gardenFields)
        {
            if (f.layout == null || f.layout.outline == null || f.layout.outline.Count < 3)
            {
                gardenOutlines.Add(null);
                continue;
            }
            List<Vector2> local = HousingEditors.Convert(f.layout.outline, f.transform, p.transform);
            gardenOutlines.Add(HousingGeometry.IsSimple(local) ? local : null);
            if (landSimple)
            {
                float outside = HousingGeometry.OutsideShare(land, local);
                if (outside > OutsideSlack) Add(into, W, $"The garden '{f.name}' reaches outside the land ({outside * 100f:0}% of it).", f);
            }
            if (houseSimple)
            {
                float under = HousingGeometry.SharedArea(house, local);
                if (under > HousingGeometry.DefaultOverlapArea) Add(into, W, $"The garden '{f.name}' lies under the house ({under:0.#} m²).", f);
            }
        }
        // A zone over a garden, or two gardens on the same ground: that ground would be priced (and listed) twice.
        for (int i = 0; i < gardenFields.Count; i++)
        {
            List<Vector2> g = gardenOutlines[i];
            if (g == null) continue;
            if (p.zones != null)
                for (int j = 0; j < p.zones.Count; j++)
                {
                    PropertyZone z = p.zones[j];
                    if (z == null || z.outline == null || z.outline.Count < 3 || !HousingGeometry.IsSimple(z.outline)) continue;
                    float shared = HousingGeometry.SharedArea(z.outline, g);
                    if (shared > HousingGeometry.DefaultOverlapArea)
                        Add(into, W, $"The zone '{ZoneName(z, j)}' lies over the garden '{gardenFields[i].name}' ({shared:0.#} m²): that ground counts twice in the rent.", p);
                }
            for (int j = i + 1; j < gardenFields.Count; j++)
            {
                if (gardenOutlines[j] == null) continue;
                float shared = HousingGeometry.SharedArea(g, gardenOutlines[j]);
                if (shared > HousingGeometry.DefaultOverlapArea)
                    Add(into, W, $"The gardens '{gardenFields[i].name}' and '{gardenFields[j].name}' overlap by {shared:0.#} m² (that ground counts twice in the rent).", gardenFields[j]);
            }
        }

        // Animal pens: like gardens - on the land, off the house, clear of the zones, the gardens and each other (an animal
        // pen next to a garden, never over it).
        List<AnimalPen> penList = HousingEditors.PensOf(p);
        var penOutlines = new List<List<Vector2>>(penList.Count);
        foreach (AnimalPen pen in penList)
        {
            if (pen.outline == null || pen.outline.Count < 3)
            {
                penOutlines.Add(null);
                Add(into, W, $"The animal pen '{pen.name}' has no proper outline.", pen);
                continue;
            }
            List<Vector2> local = HousingEditors.Convert(pen.outline, pen.transform, p.transform);
            bool simple = HousingGeometry.IsSimple(local);
            penOutlines.Add(simple ? local : null);
            if (!simple)
            {
                Add(into, W, $"The animal pen '{pen.name}' has an outline that crosses itself.", pen);
                continue;
            }
            if (landSimple)
            {
                float outside = HousingGeometry.OutsideShare(land, local);
                if (outside > OutsideSlack) Add(into, W, $"The animal pen '{pen.name}' reaches outside the land ({outside * 100f:0}% of it).", pen);
            }
            if (houseSimple)
            {
                float under = HousingGeometry.SharedArea(house, local);
                if (under > HousingGeometry.DefaultOverlapArea) Add(into, W, $"The animal pen '{pen.name}' lies under the house ({under:0.#} m²).", pen);
            }
            if (p.zones != null)
                for (int j = 0; j < p.zones.Count; j++)
                {
                    PropertyZone z = p.zones[j];
                    if (z == null || z.outline == null || z.outline.Count < 3 || !HousingGeometry.IsSimple(z.outline)) continue;
                    float shared = HousingGeometry.SharedArea(z.outline, local);
                    if (shared > HousingGeometry.DefaultOverlapArea)
                        Add(into, W, $"The zone '{ZoneName(z, j)}' lies over the animal pen '{pen.name}' ({shared:0.#} m²): that ground counts twice in the rent.", pen);
                }
            for (int g = 0; g < gardenFields.Count; g++)
            {
                if (gardenOutlines[g] == null) continue;
                float shared = HousingGeometry.SharedArea(gardenOutlines[g], local);
                if (shared > HousingGeometry.DefaultOverlapArea)
                    Add(into, W, $"The animal pen '{pen.name}' lies over the garden '{gardenFields[g].name}' ({shared:0.#} m²): animals and plants may not share ground.", pen);
            }
        }
        for (int i = 0; i < penList.Count; i++)
            for (int j = i + 1; j < penList.Count; j++)
            {
                if (penOutlines[i] == null || penOutlines[j] == null) continue;
                float shared = HousingGeometry.SharedArea(penOutlines[i], penOutlines[j]);
                if (shared > HousingGeometry.DefaultOverlapArea)
                    Add(into, W, $"The animal pens '{penList[i].name}' and '{penList[j].name}' overlap by {shared:0.#} m².", penList[j]);
            }

        // Chests: an id each, listed once, none rolling random items, none missing from the list; all on the land.
        var listed = new HashSet<Storage>();
        if (p.chests != null)
            foreach (HousingChest c in p.chests)
            {
                if (c == null || c.storage == null) continue;
                if (!listed.Add(c.storage))
                {
                    Add(into, E, $"The chest '{c.storage.gameObject.name}' is listed twice in Chests.", c.storage);
                    continue;
                }
                ChestFindings(c, p, into);
                if (landSimple && OffLand(p, c.storage.transform.position))
                    Add(into, W, $"The chest '{c.storage.gameObject.name}' is not on the land.", c.storage);
            }
        if (p.includeChildren)
        {
            Property owner = p;
            foreach (Storage s in p.GetComponentsInChildren<Storage>(true))
            {
                if (listed.Contains(s)) continue;
                Add(into, E, $"The chest '{s.gameObject.name}' is a child but not in Chests: it would get a new id every session and lose what " +
                             "tenants keep in it. Press Collect From Children.", s, "Collect", () => HousingEditors.Collect(owner));
                if (SpawnsItems(s))
                    Add(into, E, $"The chest '{s.gameObject.name}' spawns random items (Spawn Items): it would roll them into a tenant's chest. Turn it off.",
                        s, "Turn Off", () => StopSpawning(s));
                if (landSimple && OffLand(p, s.transform.position)) Add(into, W, $"The chest '{s.gameObject.name}' is not on the land.", s);
            }
        }
        foreach (SleepSpot b in Belongings(p, p.beds))
            if (landSimple && OffLand(p, b.transform.position)) Add(into, W, $"The bed '{b.gameObject.name}' is not on the land.", b);
        List<LockableDoor> doors = Belongings(p, p.doors);
        foreach (LockableDoor d in doors)
            if (landSimple && OffLand(p, d.transform.position)) Add(into, W, $"The door '{d.gameObject.name}' is not on the land.", d);
        Unlocked(p, p.transform, doors.Count, "It", into);

        Scale(p.transform, "property", "its land and house are", p, into);
        if (!p.weekly && !p.monthly) Add(into, E, "It can be let neither by the week nor by the month: tick Weekly or Monthly.", p);

        List<string> districts = HousingSettings.Instance.DistrictNames();
        if (districts.Count > 0 && !HasName(districts, p.District))
            Add(into, W, $"Its district '{p.District}' is not one of the Housing Settings' districts: it counts as an ordinary district.", p);
    }

    private static string ZoneName(PropertyZone z, int index) => string.IsNullOrWhiteSpace(z.name) ? $"Zone {index + 1}" : z.name.Trim();

    private static bool OffLand(Property p, Vector3 world) => HousingGeometry.Distance(p.land, p.Local(world)) > OffLandSlack;

    /// <summary>The listed ones and (with Include Children) the children: what the property holds at runtime.</summary>
    private static List<T> Belongings<T>(Property p, List<T> list) where T : Component
    {
        var all = new List<T>();
        if (list != null)
            foreach (T x in list)
                if (x != null && !all.Contains(x)) all.Add(x);
        if (p.includeChildren)
            foreach (T x in p.GetComponentsInChildren<T>(true))
                if (!all.Contains(x)) all.Add(x);
        return all;
    }

    private static void ChestFindings(HousingChest c, Object owner, List<Finding> into)
    {
        Storage s = c.storage;
        if (string.IsNullOrEmpty(c.id))
            Add(into, IssueSeverity.Error, $"The chest '{s.gameObject.name}' has no id: what tenants keep in it could not be saved.", s, "New Id", () => NewChestId(owner, s));
        if (SpawnsItems(s))
            Add(into, IssueSeverity.Error, $"The chest '{s.gameObject.name}' spawns random items (Spawn Items): it would roll them into a tenant's chest. Turn it off.",
                s, "Turn Off", () => StopSpawning(s));
    }

    private static void Scale(Transform t, string what, string areas, Object context, List<Finding> into)
    {
        Vector3 s = t.lossyScale;
        if (Mathf.Abs(s.x - 1f) > 0.001f || Mathf.Abs(s.z - 1f) > 0.001f)
            Add(into, IssueSeverity.Warning, $"The {what} is scaled: {areas} in metres and ignore the scale (keep it at 1).", context);
    }

    private static bool HasName(List<string> names, string name)
    {
        foreach (string n in names)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ================================================================== inns
    /// <summary>
    /// The inn's own problems: its area (3 points or more, not crossing itself), its nightly price (at least 1), its rooms
    /// (each inside the area, none overlapping another on the same floor, each with a bed and a chest, chests not rolling
    /// random items, a unique room id each, each a child of the inn).
    /// </summary>
    public static void CheckInn(Inn inn, List<string> errors, List<string> warnings)
    {
        var findings = new List<Finding>();
        InnFindings(inn, findings);
        Split(findings, errors, warnings);
    }

    /// <summary>The room's part of <see cref="CheckInn"/> (its area, its place among its inn's rooms, its bed and chests, its id).</summary>
    public static void CheckRoom(InnRoom room, List<string> errors, List<string> warnings)
    {
        var findings = new List<Finding>();
        RoomAlone(room, findings);
        Split(findings, errors, warnings);
    }

    private static void InnFindings(Inn inn, List<Finding> into)
    {
        if (inn == null) return;
        const IssueSeverity E = IssueSeverity.Error, W = IssueSeverity.Warning;
        List<Vector2> area = inn.area;
        bool areaSimple = area != null && area.Count >= 3 && HousingGeometry.IsSimple(area);
        if (area == null || area.Count < 3) Add(into, E, "The inn's area needs at least 3 points (the building's footprint).", inn);
        else if (!areaSimple) Add(into, E, "The inn's area crosses itself: drag its points apart (Building tool).", inn);
        if (inn.nightlyPrice < 1) Add(into, E, "The nightly price must be at least 1.", inn);
        Scale(inn.transform, "inn", "its area and rooms are", inn, into);
        List<string> districts = HousingSettings.Instance.DistrictNames();
        if (districts.Count > 0 && !HasName(districts, inn.District))
            Add(into, W, $"Its district '{inn.District}' is not one of the Housing Settings' districts: it counts as an ordinary district.", inn);

        // Its rooms: those listed and its children - but one that lies under another inn is that inn's (both would let it).
        var rooms = new List<InnRoom>();
        foreach (InnRoom r in HousingEditors.RoomsOf(inn))
        {
            Inn under = r.GetComponentInParent<Inn>(true);
            if (under != null && under != inn)
            {
                Inn self = inn;
                InnRoom moved = r;
                if (inn.rooms != null && inn.rooms.Contains(r))
                    Add(into, E, $"The room {Quote(r)} is in this inn's Rooms but lies under the inn {Quote(under)}: both would let it (two guests " +
                                 "for one room). Remove it from this inn's Rooms.", r, "Remove From List", () => HousingEditors.DropRoom(self, moved));
                else
                    Add(into, E, $"The inn {Quote(under)} lies under this inn: both would let its room {Quote(r)} (two guests for one room). " +
                                 "Move that inn out of this one.", r);
                continue;
            }
            if (under == null) Add(into, W, $"The room {Quote(r)} is in the inn's Rooms but under no inn: make it a child of the inn.", r);
            rooms.Add(r);
        }
        if (rooms.Count == 0) Add(into, W, "The inn has no rooms: + Room adds one (a child with an Inn Room, a bed, a chest and a door).", inn);
        List<Vector2> innWorld = areaSimple ? inn.AreaWorld() : null;
        var worlds = new List<List<Vector2>>(rooms.Count);
        foreach (InnRoom r in rooms) worlds.Add(RoomWorld(r));
        var ids = new Dictionary<string, InnRoom>();
        for (int i = 0; i < rooms.Count; i++)
        {
            InnRoom r = rooms[i];
            RoomFindings(r, worlds[i], innWorld, into);
            for (int j = i + 1; j < rooms.Count; j++) RoomPair(r, worlds[i], rooms[j], worlds[j], r, into);
            DoorsInto(r, rooms, into);
            string id = r.RoomId;
            if (ids.TryGetValue(id, out InnRoom first))
            {
                InnRoom copy = r, original = first;
                Add(into, E, $"The rooms {Quote(first)} and {Quote(r)} have the same Room Id (a duplicated object?): their stays and chests would be mixed up.",
                    r, "New Id…", AskWhichToRenew("Two rooms share an id", "Room Id", original, copy, () => original.RoomId == copy.RoomId, o => RenewRoom((InnRoom)o)));
            }
            else ids[id] = r;
        }
    }

    private static void RoomAlone(InnRoom room, List<Finding> into)
    {
        if (room == null) return;
        Inn inn = room.GetComponentInParent<Inn>(true);
        List<Vector2> world = RoomWorld(room);
        if (inn == null)
        {
            Add(into, IssueSeverity.Warning, "The room is not under an Inn: make it a child of one (the inn lets it).", room);
            RoomFindings(room, world, null, into);
            return;
        }
        List<Vector2> innWorld = inn.area != null && inn.area.Count >= 3 && HousingGeometry.IsSimple(inn.area) ? inn.AreaWorld() : null;
        RoomFindings(room, world, innWorld, into);
        var rooms = new List<InnRoom>();
        foreach (InnRoom other in HousingEditors.RoomsOf(inn))
            if (other.GetComponentInParent<Inn>(true) == inn) rooms.Add(other);
        DoorsInto(room, rooms, into);
        string id = room.RoomId;
        foreach (InnRoom other in rooms)
        {
            if (other == room) continue;
            RoomPair(room, world, other, RoomWorld(other), room, into);
            if (other.RoomId == id)
            {
                InnRoom copy = room, original = other;
                Add(into, IssueSeverity.Error, $"The room {Quote(other)} has the same Room Id (a duplicated object?): their stays and chests would be mixed up.",
                    room, "New Id…", AskWhichToRenew("Two rooms share an id", "Room Id", original, copy, () => original.RoomId == copy.RoomId, o => RenewRoom((InnRoom)o)));
            }
        }
    }

    /// <summary>The room's area in the world, or null when it is not a proper area.</summary>
    private static List<Vector2> RoomWorld(InnRoom r) =>
        r != null && r.area != null && r.area.Count >= 3 && HousingGeometry.IsSimple(r.area) ? r.AreaWorld() : null;

    private static void RoomFindings(InnRoom r, List<Vector2> world, List<Vector2> innWorld, List<Finding> into)
    {
        const IssueSeverity E = IssueSeverity.Error, W = IssueSeverity.Warning;
        string rn = Quote(r);
        if (world == null) Add(into, E, $"The room {rn} has no proper area (at least 3 points, its outline not crossing itself).", r);
        else if (innWorld != null)
        {
            float outside = HousingGeometry.OutsideShare(innWorld, world);
            if (outside > OutsideSlack) Add(into, W, $"The room {rn} reaches outside the inn's area ({outside * 100f:0}% of it).", r);
        }
        SleepSpot bed = r.bed != null ? r.bed : r.GetComponentInChildren<SleepSpot>(true);
        if (bed == null) Add(into, E, $"The room {rn} has no bed: give it a child with a Sleep Spot.", r);

        int chests = 0;
        var listed = new HashSet<Storage>();
        if (r.chests != null)
            foreach (HousingChest c in r.chests)
            {
                if (c == null || c.storage == null) continue;
                if (!listed.Add(c.storage))
                {
                    Add(into, E, $"The chest '{c.storage.gameObject.name}' is listed twice in the Chests of {rn}.", c.storage);
                    continue;
                }
                chests++;
                ChestFindings(c, r, into);
            }
        InnRoom owner = r;
        foreach (Storage s in r.GetComponentsInChildren<Storage>(true))
        {
            if (listed.Contains(s)) continue;
            chests++;
            Add(into, E, $"The chest '{s.gameObject.name}' of {rn} is not in its Chests: it would get a new id every session and lose what guests " +
                         "keep in it. Press Collect From Children.", s, "Collect", () => HousingEditors.Collect(owner));
            if (SpawnsItems(s))
                Add(into, E, $"The chest '{s.gameObject.name}' spawns random items (Spawn Items): it would roll them into a guest's chest. Turn it off.",
                    s, "Turn Off", () => StopSpawning(s));
        }
        if (chests == 0) Add(into, E, $"The room {rn} has no chest: give it a child with a Storage.", r);
        Unlocked(r, r.transform, RoomDoors(r).Count, $"The room {rn}", into);
        Scale(r.transform, $"room {rn}", "its area is", r, into);
    }

    private static void RoomPair(InnRoom a, List<Vector2> wa, InnRoom b, List<Vector2> wb, Object context, List<Finding> into)
    {
        if (a == null || b == null || wa == null || wb == null) return;
        if (Mathf.Abs(a.transform.position.y - b.transform.position.y) >= FloorSeparation) return; // another floor
        if (Overlapping(a.area, a.transform, b.area, b.transform, out float shared))
            Add(into, IssueSeverity.Error, $"The rooms {Quote(a)} and {Quote(b)} overlap by {shared:0.#} m².", context);
    }

    /// <summary>The room's doors whose way out (a step in front of them) lies in another of <paramref name="rooms"/> on its floor.</summary>
    private static void DoorsInto(InnRoom r, List<InnRoom> rooms, List<Finding> into)
    {
        if (r == null || r.area == null || r.area.Count < 3) return;
        foreach (LockableDoor d in RoomDoors(r))
        {
            Vector3 front = HousingEditors.DoorFront(r.transform, r.area, d);
            foreach (InnRoom o in rooms)
            {
                if (o == null || o == r || o.area == null || o.area.Count < 3) continue;
                if (Mathf.Abs(o.transform.position.y - r.transform.position.y) >= FloorSeparation) continue; // another floor
                if (!HousingGeometry.Contains(o.area, o.Local(front))) continue;
                Add(into, IssueSeverity.Warning, $"The door '{d.gameObject.name}' of the room {Quote(r)} opens into the room {Quote(o)}: its guests would have " +
                                                 "to walk through another guest's room. Leave a corridor in front of it.", d);
                break;
            }
        }
    }

    /// <summary>The room's doors: those listed and the Lockable Doors among its children.</summary>
    private static List<LockableDoor> RoomDoors(InnRoom r)
    {
        var list = new List<LockableDoor>();
        if (r == null) return list;
        if (r.doors != null)
            foreach (LockableDoor d in r.doors)
                if (d != null && !list.Contains(d)) list.Add(d);
        foreach (LockableDoor d in r.GetComponentsInChildren<LockableDoor>(true))
            if (!list.Contains(d)) list.Add(d);
        return list;
    }

    /// <summary>
    /// A place whose doors anyone may open: none is a Lockable Door (only the sweep shows strangers out, after a while), or
    /// a Door Controller among its children has no Lockable Door beside it (a fix adds one).
    /// </summary>
    private static void Unlocked(Component place, Transform root, int lockable, string who, List<Finding> into)
    {
        if (lockable == 0)
            Add(into, IssueSeverity.Warning, $"{who} has no Lockable Door: anyone may walk in (only the sweep shows them out after a while). " +
                                             "Put a Lockable Door on its door, then Collect From Children.", place);
        foreach (DoorController dc in root.GetComponentsInChildren<DoorController>(true))
        {
            if (dc.GetComponent<LockableDoor>() != null) continue;
            DoorController door = dc;
            Component owner = place;
            Add(into, IssueSeverity.Warning, $"The door '{dc.gameObject.name}' is a Door Controller without a Lockable Door: anyone may open it.", dc,
                "Add Lock", () => HousingEditors.AddLock(door, owner) != null);
        }
    }

    // ================================================================== overlaps (the open scenes)
    /// <summary>What overlaps the property's land in the open scenes (see <see cref="OverlapsOf(Property, HousingSceneObjects)"/>).</summary>
    public static HousingOverlaps OverlapsOf(Property p) => OverlapsOf(p, HousingSceneObjects.Open());

    /// <summary>
    /// What overlaps the property's land in <paramref name="scene"/> (each pair measured in the property's own space): the
    /// other properties' lands and the inns' areas (errors: "'A' overlaps 'B' by 3.2 m²"), soil fields that are not its
    /// gardens and gardens another property holds too (errors), and the objects of the city standing on it - the enabled,
    /// solid colliders whose shapes reach the land between <see cref="FlatGround"/> m above the property and
    /// <see cref="RoofClearance"/> m above its roof (Floors × Floor Height), not under the property or what it holds (its
    /// gardens, chests, beds and doors), not a terrain, not on the Housing Settings' Ground Layers, not under an NPC, a
    /// Combat Entity or a moving rigidbody. Each is measured by the ground it covers seen from above (boxes, spheres,
    /// capsules and convex meshes by their own outline, other meshes from above), the colliders of one object (its
    /// rigidbody, its prefab) together: covering <see cref="IntrusionErrorArea"/> m² of the land or more is an error, from
    /// Intrusion Warn Area a warning.
    /// </summary>
    public static HousingOverlaps OverlapsOf(Property p, HousingSceneObjects scene)
    {
        var r = new HousingOverlaps();
        PropertyOverlaps(p, scene, r, false, null);
        return r;
    }

    /// <summary>What overlaps the inn's area in the open scenes: the properties' lands and the other inns' areas (errors).</summary>
    public static HousingOverlaps OverlapsOf(Inn inn) => OverlapsOf(inn, HousingSceneObjects.Open());

    /// <summary>What overlaps the inn's area in <paramref name="scene"/>: the properties' lands and the other inns' areas (errors).</summary>
    public static HousingOverlaps OverlapsOf(Inn inn, HousingSceneObjects scene)
    {
        var r = new HousingOverlaps();
        InnOverlaps(inn, scene, r, true, false, null);
        return r;
    }

    /// <summary>The world boxes of a run's places, taken once (each pair is first tested by its boxes).</summary>
    private sealed class Boxes
    {
        public readonly Rect[] lands, inns, fields;

        public Boxes(HousingSceneObjects s)
        {
            lands = new Rect[s.properties.Count];
            for (int i = 0; i < lands.Length; i++) lands[i] = s.properties[i] != null ? WorldRect(s.properties[i].land, s.properties[i].transform) : Nowhere;
            inns = new Rect[s.inns.Count];
            for (int i = 0; i < inns.Length; i++) inns[i] = s.inns[i] != null ? WorldRect(s.inns[i].area, s.inns[i].transform) : Nowhere;
            fields = new Rect[s.fields.Count];
            for (int i = 0; i < fields.Length; i++)
            {
                SoilField f = s.fields[i];
                fields[i] = f != null && f.layout != null ? WorldRect(f.layout.outline, f.transform) : Nowhere;
            }
        }
    }

    private static readonly Rect Nowhere = new Rect(float.MaxValue, float.MaxValue, 0f, 0f);

    /// <summary>The world box (X, Z) of an outline placed by <paramref name="t"/>, grown a little for rounding (nothing allocated).</summary>
    private static Rect WorldRect(List<Vector2> local, Transform t)
    {
        if (local == null || local.Count == 0 || t == null) return Nowhere;
        Vector3 o = t.position;
        Quaternion q = t.rotation;
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
        foreach (Vector2 v in local)
        {
            Vector3 w = o + q * new Vector3(v.x, 0f, v.y);
            x0 = Mathf.Min(x0, w.x);
            x1 = Mathf.Max(x1, w.x);
            z0 = Mathf.Min(z0, w.z);
            z1 = Mathf.Max(z1, w.z);
        }
        float pad = 0.01f + 1e-6f * Mathf.Max(Mathf.Abs(o.x), Mathf.Abs(o.z));
        return Rect.MinMaxRect(x0 - pad, z0 - pad, x1 + pad, z1 + pad);
    }

    /// <summary>
    /// Do two placed outlines overlap - share more than <see cref="HousingGeometry.TouchingSlack"/> (places that only share a
    /// fence touch)? Measured in <paramref name="ta"/>'s own space, as precisely kilometres from the world's origin as at it.
    /// </summary>
    private static bool Overlapping(List<Vector2> a, Transform ta, List<Vector2> b, Transform tb, out float shared)
    {
        shared = HousingGeometry.SharedArea(a, HousingGeometry.Relative(b, tb.position, tb.rotation, ta.position, ta.rotation));
        return shared > HousingGeometry.TouchingSlack(a, b, ta.position, tb.position);
    }

    /// <param name="pairsOnce">Report a pair of places once (from the first of the two in the scene's list).</param>
    /// <param name="boxes">The places' world boxes taken for the whole run (null: measured here).</param>
    private static void PropertyOverlaps(Property p, HousingSceneObjects s, HousingOverlaps r, bool pairsOnce, Boxes boxes)
    {
        if (p == null || s == null || p.land == null || p.land.Count < 3 || !HousingGeometry.IsSimple(p.land)) return;
        Transform pt = p.transform;
        int self = s.properties.IndexOf(p);
        Rect lr = boxes != null && self >= 0 ? boxes.lands[self] : WorldRect(p.land, pt);

        for (int i = 0; i < s.properties.Count; i++)
        {
            Property q = s.properties[i];
            if (q == null || q == p || (pairsOnce && self >= 0 && i < self)) continue;
            if (q.land == null || q.land.Count < 3) continue;
            if (!lr.Overlaps(boxes != null ? boxes.lands[i] : WorldRect(q.land, q.transform))) continue;
            if (!Overlapping(p.land, pt, q.land, q.transform, out float shared)) continue;
            r.places.Add(q);
            r.Error($"{Quote(p)} overlaps {Quote(q)} by {shared:0.0} m².", q);
        }

        for (int i = 0; i < s.inns.Count; i++)
        {
            Inn inn = s.inns[i];
            if (inn == null || inn.area == null || inn.area.Count < 3) continue;
            if (!lr.Overlaps(boxes != null ? boxes.inns[i] : WorldRect(inn.area, inn.transform))) continue;
            if (!Overlapping(p.land, pt, inn.area, inn.transform, out float shared)) continue;
            r.places.Add(inn);
            r.Error($"{Quote(p)} overlaps the inn {Quote(inn)} by {shared:0.0} m².", inn);
        }

        // Its gardens: none another property holds too (both would be priced and let with it; only one tenant could garden it).
        foreach (SoilField f in HousingEditors.GardensOf(p))
            for (int i = 0; i < s.properties.Count; i++)
            {
                Property q = s.properties[i];
                if (q == null || q == p || (pairsOnce && self >= 0 && i < self) || !Owns(q, f)) continue;
                r.Error($"The garden '{f.name}' belongs to both {Quote(p)} and {Quote(q)}: two tenants would pay for it and only one could garden it. " +
                        "Keep it in the Gardens of one (Collect From Children drops a listed garden that lies under another property).", f);
            }

        for (int i = 0; i < s.fields.Count; i++)
        {
            SoilField f = s.fields[i];
            if (f == null || f.layout == null || f.layout.outline == null || f.layout.outline.Count < 3) continue;
            if (Owns(p, f)) continue;
            if (!lr.Overlaps(boxes != null ? boxes.fields[i] : WorldRect(f.layout.outline, f.transform))) continue;
            if (!Overlapping(p.land, pt, f.layout.outline, f.transform, out float shared)) continue;
            Property owner = null;
            foreach (Property q in s.properties)
                if (q != null && q != p && Owns(q, f)) owner = q;
            r.Error(owner != null
                ? $"The garden '{f.name}' of {Quote(owner)} lies on the land of {Quote(p)} ({shared:0.#} m²)."
                : $"The soil field '{f.name}' lies on the land of {Quote(p)} ({shared:0.#} m²) but is not one of its gardens: add it to its Gardens " +
                  "(or make it a child), or move it.", f);
        }

        Intrusions(p, s, r);
    }

    /// <summary>Does the property hold this garden (listed, or a child with Include Children)?</summary>
    private static bool Owns(Property p, SoilField f) =>
        (p.gardens != null && p.gardens.Contains(f)) || (p.includeChildren && f.transform.IsChildOf(p.transform));

    // ------------------------------------------------------------------ what stands on a land
    /// <summary>The most points the ground an object covers is sampled at (its parts overlapping, or a concave mesh).</summary>
    private const int MaxSamples = 4096;
    /// <summary>The finest sampling step, metres.</summary>
    private const float MinSampleStep = 0.05f;
    /// <summary>Meshes with more vertices are outlined by their bounds' corners.</summary>
    private const int MaxHullVertices = 65536;

    /// <summary>A collider seen from above: its outline (world X, Z), or none (a concave mesh: sampled from above); its box.</summary>
    private struct Shape
    {
        public Collider collider;
        public List<Vector2> outline;
        public Rect box;
        public float top;
    }

    private static Collider[] near = new Collider[256];
    private static readonly List<Collider> candidates = new List<Collider>();
    private static readonly Dictionary<long, KeyValuePair<Matrix4x4, List<Vector2>>> hulls = new Dictionary<long, KeyValuePair<Matrix4x4, List<Vector2>>>();

    /// <summary>The objects of the city standing on the land (the ground their colliders cover of it seen from above), summed per object.</summary>
    private static void Intrusions(Property p, HousingSceneObjects s, HousingOverlaps r)
    {
        HousingSettings settings = HousingSettings.Instance;
        int groundLayers = settings.groundLayers.value;
        float warnArea = Mathf.Max(0.01f, settings.intrusionWarnArea);
        Transform pt = p.transform;
        float bottom = pt.position.y + FlatGround;
        float top = pt.position.y + Mathf.Max(1, p.floors) * Mathf.Max(0.1f, p.floorHeight) + RoofClearance;
        List<Vector2> land = p.LandWorld();
        Rect lr = HousingGeometry.Bounds(land);
        if (lr.width <= 0f || lr.height <= 0f) return;
        Physics.SyncTransforms(); // in Edit Mode the physics scene follows moved objects only when asked to

        // What the property holds is its own wherever it lies: its children, its gardens, chests, beds and doors.
        var own = new List<Transform> { pt };
        foreach (SoilField f in HousingEditors.GardensOf(p)) own.Add(f.transform);
        if (p.chests != null)
            foreach (HousingChest c in p.chests)
                if (c != null && c.storage != null) own.Add(c.storage.transform);
        foreach (SleepSpot b in Belongings(p, p.beds)) own.Add(b.transform);
        foreach (LockableDoor d in Belongings(p, p.doors)) own.Add(d.transform);
        foreach (Object part in Parts(p))
            if (part is GameObject g) own.Add(g.transform);
            else if (part is Component c) own.Add(c.transform);

        var owners = new List<GameObject>();
        var parts = new List<List<Shape>>();
        foreach (Collider c in Near(p, lr, bottom, top, s, ~groundLayers))
        {
            if (c == null || !c.enabled || c.isTrigger || c is TerrainCollider) continue;
            GameObject go = c.gameObject;
            if (!go.activeInHierarchy || (groundLayers & (1 << go.layer)) != 0) continue;
            if ((go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor)) != 0) continue;
            if (Under(c.transform, own)) continue; // the property's own house, fences, sheds, beds...
            Rigidbody body = c.GetComponentInParent<Rigidbody>(); // the body it moves with (attachedRigidbody, read from the hierarchy in Edit Mode)
            if (body != null && !body.isKinematic) continue; // it moves: a crate, a cart
            if (c.GetComponentInParent<NPC>() != null || c.GetComponentInParent<CombatEntity>() != null) continue; // people and creatures
            Shape shape = ShapeOf(c);
            if (!shape.box.Overlaps(lr)) continue;
            GameObject owner = OwnerOf(c, p);
            int at = owners.IndexOf(owner);
            if (at < 0)
            {
                owners.Add(owner);
                parts.Add(new List<Shape>());
                at = owners.Count - 1;
            }
            parts[at].Add(shape);
        }
        float landArea = HousingGeometry.Area(land);
        for (int i = 0; i < owners.Count; i++)
        {
            float a = Mathf.Min(Covered(land, lr, parts[i], bottom), landArea);
            if (a < warnArea) continue;
            GameObject owner = owners[i];
            bool inPrefab = PrefabUtility.IsPartOfPrefabInstance(owner) && !PrefabUtility.IsOutermostPrefabInstanceRoot(owner);
            string text = $"'{owner.name}' stands on the land of {Quote(p)} ({a:0.##} m²{PartNames(owner, parts[i])}): move it, make it a child of the " +
                          (inPrefab ? "property if it is part of it (it is inside a prefab: unpack the prefab first)" : "property if it is part of it") +
                          ", or put it on a Ground Layer of the Housing Settings if it is ground.";
            if (a >= IntrusionErrorArea) r.Error(text, owner);
            else r.Warning(text, owner);
        }
    }

    /// <summary>
    /// The property's Parts - house models kept inside a prefab, its own though not its children - when the property has
    /// such a list (a serialized "parts" list of objects); else none.
    /// </summary>
    public static List<Object> Parts(Property p)
    {
        var list = new List<Object>();
        if (p == null) return list;
        using (var so = new SerializedObject(p))
        {
            SerializedProperty parts = so.FindProperty("parts");
            if (parts == null || !parts.isArray) return list;
            for (int i = 0; i < parts.arraySize; i++)
            {
                SerializedProperty e = parts.GetArrayElementAtIndex(i);
                if (e.propertyType == SerializedPropertyType.ObjectReference && e.objectReferenceValue != null) list.Add(e.objectReferenceValue);
            }
        }
        return list;
    }

    private static bool Under(Transform t, List<Transform> roots)
    {
        foreach (Transform root in roots)
            if (root != null && t.IsChildOf(root)) return true;
        return false;
    }

    /// <summary>
    /// The colliders whose shapes reach into the box over the land between <paramref name="bottom"/> and <paramref name="top"/>:
    /// asked of the property's physics scene (its own index - only what is near is looked at), or found among
    /// <see cref="HousingSceneObjects.colliders"/> by their bounds when the run names them.
    /// </summary>
    private static List<Collider> Near(Property p, Rect lr, float bottom, float top, HousingSceneObjects s, int mask)
    {
        candidates.Clear();
        if (s.colliders != null)
        {
            foreach (Collider c in s.colliders)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                if (b.max.y <= bottom || b.min.y >= top) continue; // flat ground, or passing over the roof
                if (b.max.x < lr.xMin || b.min.x > lr.xMax || b.max.z < lr.yMin || b.min.z > lr.yMax) continue;
                candidates.Add(c);
            }
            return candidates;
        }
        PhysicsScene physics = p.gameObject.scene.IsValid() ? p.gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
        if (!physics.IsValid()) physics = Physics.defaultPhysicsScene;
        var centre = new Vector3(lr.center.x, (bottom + top) * 0.5f, lr.center.y);
        var half = new Vector3(lr.width * 0.5f, (top - bottom) * 0.5f, lr.height * 0.5f);
        int n;
        while ((n = physics.OverlapBox(centre, half, near, Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) >= near.Length)
            near = new Collider[near.Length * 2];
        for (int i = 0; i < n; i++) candidates.Add(near[i]);
        return candidates;
    }

    /// <summary>
    /// The object a collider is part of, whose parts are summed: its rigidbody's object, else the prefab instance it belongs
    /// to (the nearest), else its own object - never one that holds the property itself.
    /// </summary>
    private static GameObject OwnerOf(Collider c, Property p)
    {
        Rigidbody body = c.GetComponentInParent<Rigidbody>();
        GameObject owner = body != null ? body.gameObject : PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject);
        return owner == null || p.transform.IsChildOf(owner.transform) ? c.gameObject : owner;
    }

    /// <summary>"" for an object of one part; "; its parts 'Counter', 'Crate (2)' and 3 more" for one of several.</summary>
    private static string PartNames(GameObject owner, List<Shape> shapes)
    {
        var names = new List<string>();
        foreach (Shape x in shapes)
        {
            GameObject go = x.collider.gameObject;
            if (go != owner && !names.Contains(go.name)) names.Add(go.name);
        }
        if (names.Count == 0) return "";
        string shown = names.Count == 1 ? $"'{names[0]}'" : $"'{names[0]}', '{names[1]}'";
        return names.Count > 2 ? $"; its parts {shown} and {names.Count - 2} more" : $"; its part{(names.Count == 1 ? "" : "s")} {shown}";
    }

    /// <summary>
    /// A collider seen from above (world X, Z): a box, a sphere, a capsule and a convex mesh by their own outlines; a concave
    /// mesh sampled from above (its outline bounds it); anything else by its bounds.
    /// </summary>
    private static Shape ShapeOf(Collider c)
    {
        Bounds b = c.bounds;
        var shape = new Shape { collider = c, box = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z), top = b.max.y };
        Transform t = c.transform;
        bool sampled = false;
        switch (c)
        {
            case BoxCollider box:
                shape.outline = HousingGeometry.BoxFootprint(t.TransformPoint(box.center), Vector3.Scale(box.size, t.lossyScale), t.rotation);
                break;
            case SphereCollider sphere:
            {
                Vector3 s = Abs(t.lossyScale);
                shape.outline = Disc(t.TransformPoint(sphere.center), sphere.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z)), null);
                break;
            }
            case CapsuleCollider capsule:
                shape.outline = Capsule(t, capsule.center, capsule.radius, capsule.height, capsule.direction);
                break;
            case CharacterController character:
                shape.outline = Capsule(t, character.center, character.radius, character.height, 1);
                break;
            case MeshCollider mesh when mesh.sharedMesh != null:
                List<Vector2> hull = MeshHull(mesh);
                if (mesh.convex) shape.outline = hull;
                else sampled = true;
                if (hull.Count >= 3)
                {
                    Rect h = HousingGeometry.Bounds(hull);
                    shape.box = Rect.MinMaxRect(Mathf.Max(h.xMin, shape.box.xMin), Mathf.Max(h.yMin, shape.box.yMin),
                        Mathf.Min(h.xMax, shape.box.xMax), Mathf.Min(h.yMax, shape.box.yMax));
                }
                break;
        }
        if (shape.outline == null && !sampled)
            shape.outline = new List<Vector2> { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.max.x, b.max.z), new Vector2(b.min.x, b.max.z) };
        return shape;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    /// <summary>A capsule seen from above: the hull of the discs at its two ends.</summary>
    private static List<Vector2> Capsule(Transform t, Vector3 centre, float radius, float height, int direction)
    {
        Vector3 s = Abs(t.lossyScale);
        Vector3 axis = direction == 0 ? Vector3.right : direction == 2 ? Vector3.forward : Vector3.up;
        float along = direction == 0 ? s.x : direction == 2 ? s.z : s.y;
        float r = radius * (direction == 0 ? Mathf.Max(s.y, s.z) : direction == 2 ? Mathf.Max(s.x, s.y) : Mathf.Max(s.x, s.z));
        Vector3 c = t.TransformPoint(centre), end = t.rotation * axis * Mathf.Max(0f, height * along * 0.5f - r);
        var points = new List<Vector2>(48);
        Disc(c + end, r, points);
        Disc(c - end, r, points);
        return HousingGeometry.ConvexHull(points);
    }

    /// <summary>A disc seen from above (24 points), added to <paramref name="into"/> (a new list when null).</summary>
    private static List<Vector2> Disc(Vector3 centre, float radius, List<Vector2> into)
    {
        into ??= new List<Vector2>(24);
        for (int i = 0; i < 24; i++)
        {
            float a = i * (Mathf.PI * 2f / 24f);
            into.Add(new Vector2(centre.x + Mathf.Cos(a) * radius, centre.z + Mathf.Sin(a) * radius));
        }
        return into;
    }

    /// <summary>
    /// A mesh collider's outline seen from above: the hull of its vertices (of its bounds' corners when the mesh cannot be
    /// read or is very big), kept while the collider does not move.
    /// </summary>
    private static List<Vector2> MeshHull(MeshCollider c)
    {
        Mesh mesh = c.sharedMesh;
        Matrix4x4 m = c.transform.localToWorldMatrix;
        long key = ((long)c.GetInstanceID() << 32) ^ (uint)mesh.GetInstanceID();
        if (hulls.TryGetValue(key, out KeyValuePair<Matrix4x4, List<Vector2>> kept) && kept.Key == m) return kept.Value;
        var points = new List<Vector2>();
        if (mesh.isReadable && mesh.vertexCount <= MaxHullVertices)
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                points.Add(new Vector2(w.x, w.z));
            }
        else
        {
            Bounds b = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 w = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                points.Add(new Vector2(w.x, w.z));
            }
        }
        List<Vector2> hull = HousingGeometry.ConvexHull(points);
        if (hulls.Count > 4096) hulls.Clear();
        hulls[key] = new KeyValuePair<Matrix4x4, List<Vector2>>(m, hull);
        return hull;
    }

    /// <summary>
    /// The ground of the land an object's parts cover (square metres), the parts together: exact for one outline or for
    /// outlines apart; else sampled on a grid of at most <see cref="MaxSamples"/> points as the union of the parts (overlapping
    /// parts count once), a concave mesh covering a point when a ray from above meets it higher than <paramref name="bottom"/>.
    /// </summary>
    private static float Covered(List<Vector2> land, Rect lr, List<Shape> shapes, float bottom)
    {
        bool exact = true;
        for (int i = 0; i < shapes.Count && exact; i++)
        {
            if (shapes[i].outline == null) exact = false;
            for (int j = i + 1; j < shapes.Count && exact; j++)
                if (shapes[i].box.Overlaps(shapes[j].box)) exact = false;
        }
        if (exact)
        {
            float sum = 0f;
            foreach (Shape x in shapes) sum += HousingGeometry.SharedArea(land, x.outline);
            return sum;
        }
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
        foreach (Shape x in shapes)
        {
            x0 = Mathf.Min(x0, x.box.xMin);
            z0 = Mathf.Min(z0, x.box.yMin);
            x1 = Mathf.Max(x1, x.box.xMax);
            z1 = Mathf.Max(z1, x.box.yMax);
        }
        x0 = Mathf.Max(x0, lr.xMin);
        z0 = Mathf.Max(z0, lr.yMin);
        x1 = Mathf.Min(x1, lr.xMax);
        z1 = Mathf.Min(z1, lr.yMax);
        if (x1 <= x0 || z1 <= z0) return 0f;
        float step = Mathf.Max(MinSampleStep, Mathf.Sqrt((x1 - x0) * (z1 - z0) / MaxSamples));
        int nx = Mathf.CeilToInt((x1 - x0) / step), nz = Mathf.CeilToInt((z1 - z0) / step);
        int covered = 0;
        for (int iz = 0; iz < nz; iz++)
        for (int ix = 0; ix < nx; ix++)
        {
            var at = new Vector2(x0 + (ix + 0.5f) * step, z0 + (iz + 0.5f) * step);
            if (!HousingGeometry.Contains(land, at)) continue;
            foreach (Shape x in shapes)
            {
                if (!x.box.Contains(at)) continue;
                if (x.outline != null ? HousingGeometry.Contains(x.outline, at) : FromAbove(x, at, bottom))
                {
                    covered++;
                    break;
                }
            }
        }
        return covered * step * step;
    }

    /// <summary>Does a ray from above the shape meet its collider at <paramref name="at"/> higher than <paramref name="bottom"/>?</summary>
    private static bool FromAbove(Shape x, Vector2 at, float bottom)
    {
        float y = x.top + 0.1f;
        return y > bottom && x.collider.Raycast(new Ray(new Vector3(at.x, y, at.y), Vector3.down), out RaycastHit _, y - bottom);
    }

    /// <param name="withProperties">Also report the properties' lands (the property's own check reports a property and an inn already).</param>
    /// <param name="pairsOnce">Report a pair of inns once (from the first of the two in the scene's list).</param>
    /// <param name="boxes">The places' world boxes taken for the whole run (null: measured here).</param>
    private static void InnOverlaps(Inn inn, HousingSceneObjects s, HousingOverlaps r, bool withProperties, bool pairsOnce, Boxes boxes)
    {
        if (inn == null || s == null || inn.area == null || inn.area.Count < 3 || !HousingGeometry.IsSimple(inn.area)) return;
        Transform it = inn.transform;
        int self = s.inns.IndexOf(inn);
        Rect ar = boxes != null && self >= 0 ? boxes.inns[self] : WorldRect(inn.area, it);
        if (withProperties)
            for (int i = 0; i < s.properties.Count; i++)
            {
                Property p = s.properties[i];
                if (p == null || p.land == null || p.land.Count < 3) continue;
                if (!ar.Overlaps(boxes != null ? boxes.lands[i] : WorldRect(p.land, p.transform))) continue;
                if (!Overlapping(inn.area, it, p.land, p.transform, out float shared)) continue;
                r.places.Add(p);
                r.Error($"{Quote(p)} overlaps the inn {Quote(inn)} by {shared:0.0} m².", p);
            }
        for (int i = 0; i < s.inns.Count; i++)
        {
            Inn other = s.inns[i];
            if (other == null || other == inn || (pairsOnce && self >= 0 && i < self) || other.area == null || other.area.Count < 3) continue;
            if (!ar.Overlaps(boxes != null ? boxes.inns[i] : WorldRect(other.area, other.transform))) continue;
            if (!Overlapping(inn.area, it, other.area, other.transform, out float shared)) continue;
            r.places.Add(other);
            r.Error($"The inn {Quote(inn)} overlaps the inn {Quote(other)} by {shared:0.0} m².", other);
        }
    }

    // ================================================================== the Project Validator
    /// <summary>Every property, inn and room under <paramref name="root"/> (a scene root or a prefab): their own checks.</summary>
    public static void CheckRoot(GameObject root, List<ValidationIssue> issues)
    {
        if (root == null) return;
        bool asset = EditorUtility.IsPersistent(root);
        var findings = new List<Finding>();
        foreach (Property p in root.GetComponentsInChildren<Property>(true))
        {
            findings.Clear();
            PropertyFindings(p, findings);
            if (asset)
                Add(findings, IssueSeverity.Warning, "A property in a prefab: every copy has the prefab's Property Id unless it is given its own (leases and saves go by id).", p);
            AddIssues(findings, p, issues);
        }
        foreach (Inn inn in root.GetComponentsInChildren<Inn>(true))
        {
            findings.Clear();
            InnFindings(inn, findings);
            if (asset)
                Add(findings, IssueSeverity.Warning, "An inn in a prefab: every copy has the prefab's Inn Id unless it is given its own (stays and saves go by id).", inn);
            AddIssues(findings, inn, issues);
        }
        foreach (InnRoom room in root.GetComponentsInChildren<InnRoom>(true))
        {
            if (room.GetComponentInParent<Inn>(true) != null) continue; // checked with its inn
            findings.Clear();
            RoomAlone(room, findings);
            AddIssues(findings, room, issues);
        }
    }

    /// <summary>
    /// The open scenes as a whole, once per run: the Housing Settings (missing while there are properties or inns, or
    /// their own problems), their places together (<see cref="CheckPlaces"/>), and properties and inns outside the City scene.
    /// </summary>
    public static void CheckProject(List<ValidationIssue> issues)
    {
        HousingSceneObjects s = HousingSceneObjects.Open();
        bool any = s.properties.Count > 0 || s.inns.Count > 0;

        HousingSettings settings = SMAssets.Load<HousingSettings>(SMAssets.Settings, HousingSettings.ResourcesName);
        if (settings == null)
        {
            if (any)
                issues.Add(new ValidationIssue(IssueSeverity.Info, Category,
                        "No Housing Settings: the built-in rules are used (what a lease day is, what sizes are worth, the districts).")
                    .WithFix("Create", () => HousingEditors.CreateSettings(false) != null));
        }
        else
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            settings.Validate(errors, warnings);
            foreach (string e in errors) issues.Add(new ValidationIssue(IssueSeverity.Error, Category, e, settings));
            foreach (string w in warnings) issues.Add(new ValidationIssue(IssueSeverity.Warning, Category, w, settings));
        }

        CheckPlaces(s, issues);

        string city = AgricultureEditors.CitySceneName();
        if (!string.IsNullOrEmpty(city))
        {
            foreach (Property p in s.properties) OutsideCity(p, city, issues);
            foreach (Inn inn in s.inns) OutsideCity(inn, city, issues);
        }
    }

    /// <summary>
    /// The places of <paramref name="s"/> together: what overlaps each land and each inn (and what stands on the lands),
    /// duplicated ids (properties, inns, doors, chests - each New Id fix asks which of the two keeps its id), gardens,
    /// chests, beds, doors and rooms that belong to two places, a Realtor when there are properties (one that lets each;
    /// none whose Listings are all missing), an Innkeeper for each inn and an inn for each Innkeeper, and realtors and
    /// innkeepers that serve everyone at once.
    /// </summary>
    public static void CheckPlaces(HousingSceneObjects s, List<ValidationIssue> issues)
    {
        if (s == null) return;
        var boxes = new Boxes(s);
        foreach (Property p in s.properties)
        {
            var r = new HousingOverlaps();
            PropertyOverlaps(p, s, r, true, boxes);
            AddIssues(r, issues);
        }
        foreach (Inn inn in s.inns)
        {
            var r = new HousingOverlaps();
            InnOverlaps(inn, s, r, false, true, boxes);
            AddIssues(r, issues);
        }
        Duplicates(s, issues);
        Realtors(s, issues);
        Innkeepers(s, issues);
    }

    private static void OutsideCity(Component c, string city, List<ValidationIssue> issues)
    {
        if (c == null || !c.gameObject.scene.IsValid() || c.gameObject.scene.name == city) return;
        issues.Add(new ValidationIssue(IssueSeverity.Info, Category,
            $"{Quote(c)} is in '{c.gameObject.scene.name}', not the City scene ('{city}'): places to let belong in the permanent city " +
            "(the hub world's other scenes are made again every cycle).", c, Where(c)));
    }

    // ------------------------------------------------------------------ realtors and innkeepers
    private static void Realtors(HousingSceneObjects s, List<ValidationIssue> issues)
    {
        if (s.properties.Count > 0 && s.realtors.Count == 0)
            issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"{s.properties.Count} propert{(s.properties.Count == 1 ? "y" : "ies")} but no Realtor in the open scenes: nobody lets them " +
                    "(Housing ▸ Create Realtor NPC).")
                .WithFix("Create Realtor", () => HousingEditors.CreateRealtor() != null));
        else
            foreach (Property p in s.properties)
                if (p != null && !Let(p, s))
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                        $"No Realtor of the open scenes lets {Quote(p)} (their Listings or District leave it out).", p, Where(p)));
        foreach (Realtor rt in s.realtors)
        {
            if (rt == null) continue;
            int slots = rt.listings != null ? rt.listings.Count : 0, missing = 0;
            for (int i = 0; i < slots; i++)
                if (rt.listings[i] == null) missing++;
            Realtor target = rt;
            if (missing > 0 && missing == slots)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"The realtor '{rt.name}' lists only missing properties ({missing} empty slot{(missing == 1 ? "" : "s")} in Listings): it lets none. " +
                    "Fill the slots, or empty Listings to let every property.", rt, Where(rt)));
            else if (missing > 0)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                        $"The realtor '{rt.name}' has {missing} empty slot{(missing == 1 ? "" : "s")} in Listings (a listed property is missing).", rt, Where(rt))
                    .WithFix("Remove Empty Slots", () => DropEmptySlots(target)));
            ServesOneAtATime(rt, "A realtor", "one player could keep others from bidding", issues);
        }
    }

    /// <summary>
    /// Does a realtor let this property - by the rule it follows in play: Listings with any slot (even missing ones) name
    /// the only properties it lets; its District, when set, the only district?
    /// </summary>
    private static bool Let(Property p, HousingSceneObjects s)
    {
        foreach (Realtor r in s.realtors)
        {
            if (r == null) continue;
            if (r.listings != null && r.listings.Count > 0 && !r.listings.Contains(p)) continue;
            if (string.IsNullOrWhiteSpace(r.district) || string.Equals(r.district.Trim(), p.District, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool DropEmptySlots(Realtor r)
    {
        if (r == null || r.listings == null || !r.listings.Exists(x => x == null) || !r.listings.Exists(x => x != null)) return false;
        Undo.RecordObject(r, "Remove Empty Slots");
        r.listings.RemoveAll(x => x == null);
        EditorUtility.SetDirty(r);
        return true;
    }

    private static void Innkeepers(HousingSceneObjects s, List<ValidationIssue> issues)
    {
        foreach (Inn inn in s.inns)
        {
            if (inn == null || HasInnkeeper(inn, s)) continue;
            Inn target = inn;
            issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"The inn {Quote(inn)} has no Innkeeper: give an NPC an Innkeeper whose Inn is it (or that stands under it).", inn, Where(inn))
                .WithFix("Create Innkeeper", () => HousingEditors.CreateInnkeeper(target) != null));
        }
        foreach (Innkeeper k in s.innkeepers)
        {
            if (k == null) continue;
            if (ServedInn(k, s) == null)
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                    $"The innkeeper '{k.name}' has no inn to let: set its Inn, or put the NPC under an inn.", k, Where(k)));
            ServesOneAtATime(k, "An innkeeper", "one player could keep others waiting", issues);
        }
    }

    private static bool HasInnkeeper(Inn inn, HousingSceneObjects s)
    {
        foreach (Innkeeper k in s.innkeepers)
            if (k != null && ServedInn(k, s) == inn) return true;
        return false;
    }

    /// <summary>
    /// The inn an innkeeper lets, by the rule it follows in play (Innkeeper.InnServed): its Inn, else the inn it stands
    /// under, else the inn of its own scene whose origin is nearest (of the active, enabled inns).
    /// </summary>
    private static Inn ServedInn(Innkeeper k, HousingSceneObjects s)
    {
        if (k.inn != null) return k.inn;
        Inn parent = k.GetComponentInParent<Inn>(true);
        if (parent != null) return parent;
        Inn best = null;
        float bestD = float.MaxValue;
        foreach (Inn i in s.inns)
        {
            if (i == null || !i.enabled || !i.gameObject.activeInHierarchy || i.gameObject.scene != k.gameObject.scene) continue;
            float d = (i.transform.position - k.transform.position).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>A realtor or innkeeper that serves one player at a time (its NPC's Max Players At Once): a warning, with a fix.</summary>
    private static void ServesOneAtATime(Component c, string who, string why, List<ValidationIssue> issues)
    {
        NPC npc = c.GetComponent<NPC>();
        if (npc == null || npc.MaxPlayersAtOnce <= 0) return;
        issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                $"'{c.name}': {who} should serve everyone at once (Max Players At Once 0): {why}.", c, Where(c))
            .WithFix("Serve Everyone", () =>
            {
                if (npc == null) return false;
                var so = new SerializedObject(npc);
                SerializedProperty p = so.FindProperty("maxPlayersAtOnce");
                if (p == null || p.intValue == 0) return false;
                p.intValue = 0;
                so.ApplyModifiedProperties();
                return true;
            }));
    }

    // ------------------------------------------------------------------ ids and claims
    /// <summary>
    /// Ids two objects share (each with a New Id fix that asks which of the two keeps it), and gardens, chests, beds, doors
    /// and rooms two places claim.
    /// </summary>
    private static void Duplicates(HousingSceneObjects s, List<ValidationIssue> issues)
    {
        var properties = new Dictionary<string, Property>();
        foreach (Property p in s.properties)
        {
            if (p == null) continue;
            if (properties.TryGetValue(p.PropertyId, out Property first))
            {
                Property copy = p, original = first;
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                        $"{Quote(p)} has the same Property Id as {Quote(first)} (a duplicated object?): they would share one lease and save.", p, Where(p))
                    .WithFix("New Id…", AskWhichToRenew("Two properties share an id", "Property Id", original, copy,
                        () => original.PropertyId == copy.PropertyId, o => RenewIds((Property)o))));
            }
            else properties[p.PropertyId] = p;
        }

        var inns = new Dictionary<string, Inn>();
        foreach (Inn inn in s.inns)
        {
            if (inn == null) continue;
            if (inns.TryGetValue(inn.InnId, out Inn first))
            {
                Inn copy = inn, original = first;
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                        $"The inn {Quote(inn)} has the same Inn Id as {Quote(first)} (a duplicated object?): they would share their guests and save.", inn, Where(inn))
                    .WithFix("New Id…", AskWhichToRenew("Two inns share an id", "Inn Id", original, copy, () => original.InnId == copy.InnId, o => RenewIds((Inn)o))));
            }
            else inns[inn.InnId] = inn;
        }

        var doors = new Dictionary<string, LockableDoor>();
        foreach (LockableDoor d in s.doors)
        {
            if (d == null) continue;
            if (doors.TryGetValue(d.DoorId, out LockableDoor first))
            {
                LockableDoor copy = d, original = first;
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                        $"The door '{d.name}' has the same Door Id as '{first.name}' (a duplicated object?): opening one would open the other.", d, Where(d))
                    .WithFix("New Id…", AskWhichToRenew("Two doors share an id", "Door Id", original, copy, () => original.DoorId == copy.DoorId, o => NewId(o, "doorId"))));
            }
            else doors[d.DoorId] = d;
        }

        // Chests: an id each across every place; one place each. Beds and doors: one place each.
        var chestIds = new Dictionary<string, Storage>();
        var chestOwners = new Dictionary<Storage, Component>();
        var bedOwners = new Dictionary<SleepSpot, Component>();
        var doorOwners = new Dictionary<LockableDoor, Component>();
        foreach (Property p in s.properties)
        {
            if (p == null) continue;
            Chests(p, p.chests, chestIds, chestOwners, issues);
            foreach (SleepSpot b in Belongings(p, p.beds)) Claim(b, p, bedOwners, "bed", issues);
            foreach (LockableDoor d in Belongings(p, p.doors)) Claim(d, p, doorOwners, "door", issues);
        }
        foreach (InnRoom r in s.rooms)
        {
            if (r == null) continue;
            Chests(r, r.chests, chestIds, chestOwners, issues);
            if (r.bed != null) Claim(r.bed, r, bedOwners, "bed", issues);
            if (r.doors != null)
                foreach (LockableDoor d in r.doors)
                    if (d != null) Claim(d, r, doorOwners, "door", issues);
        }

        // Rooms in the Rooms of two inns while under neither (one under another inn is reported by the inn that lists it).
        var roomInns = new Dictionary<InnRoom, Inn>();
        foreach (Inn inn in s.inns)
        {
            if (inn == null || inn.rooms == null) continue;
            foreach (InnRoom room in inn.rooms)
            {
                if (room == null || room.GetComponentInParent<Inn>(true) != null) continue;
                if (roomInns.TryGetValue(room, out Inn other))
                {
                    if (other != inn)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                            $"The room {Quote(room)} is in the Rooms of both {Quote(other)} and {Quote(inn)}: both would let it (two guests for one room).", room, Where(room)));
                    continue;
                }
                roomInns[room] = inn;
            }
        }
    }

    private static void Chests(Component owner, List<HousingChest> list, Dictionary<string, Storage> ids, Dictionary<Storage, Component> owners, List<ValidationIssue> issues)
    {
        if (list == null) return;
        foreach (HousingChest c in list)
        {
            if (c == null || c.storage == null) continue;
            if (owners.TryGetValue(c.storage, out Component other))
            {
                if (other != owner)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                        $"The chest '{c.storage.gameObject.name}' belongs to both {Quote(other)} and {Quote(owner)}: two tenants would share it.", c.storage, Where(c.storage)));
                continue;
            }
            owners[c.storage] = owner;
            if (string.IsNullOrEmpty(c.id)) continue; // reported by the place's own check
            if (ids.TryGetValue(c.id, out Storage first) && first != c.storage)
            {
                Component place = owner, firstPlace = owners[first];
                Storage storage = c.storage, original = first;
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                        $"The chest '{storage.gameObject.name}' of {Quote(owner)} has the same id as the chest '{first.gameObject.name}' of {Quote(firstPlace)}: " +
                        "their contents would be saved as one.", storage, Where(storage))
                    .WithFix("New Id…", AskWhichToRenew("Two chests share an id", "chest id", original, storage,
                        () => ChestId(firstPlace, original) != null && ChestId(firstPlace, original) == ChestId(place, storage),
                        o => o == (Object)storage ? NewChestId(place, storage) : NewChestId(firstPlace, original))));
            }
            else ids[c.id] = c.storage;
        }
    }

    /// <summary>The id of the chest entry of <paramref name="storage"/> in the place's Chests (null when it has none).</summary>
    private static string ChestId(Component place, Storage storage)
    {
        List<HousingChest> list = place is Property p ? p.chests : place is InnRoom r ? r.chests : null;
        if (list == null) return null;
        foreach (HousingChest c in list)
            if (c != null && c.storage == storage) return c.id;
        return null;
    }

    private static void Claim<T>(T item, Component owner, Dictionary<T, Component> owners, string what, List<ValidationIssue> issues) where T : Component
    {
        if (owners.TryGetValue(item, out Component other))
        {
            if (other != owner)
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                    $"The {what} '{item.gameObject.name}' belongs to both {Quote(other)} and {Quote(owner)}: only one tenant can hold it (in play the place " +
                    "registered first keeps it).", item, Where(item)));
            return;
        }
        owners[item] = owner;
    }

    // ------------------------------------------------------------------ new ids for copies (they ask)
    /// <summary>
    /// The fix for two objects that share an id: it asks which of the two gets a new one, naming both and where they are.
    /// In a world that is already running, the one that keeps the id keeps what is saved under it (a lease or a stay, a
    /// rent account, a chest's contents) and the other starts afresh - so the new id goes to the copy that is not in the
    /// save, which only the designer knows. Nothing changes when the question is cancelled, when the two no longer share
    /// the id, or in batch mode (nobody to ask).
    /// </summary>
    private static Func<bool> AskWhichToRenew(string title, string what, Object a, Object b, Func<bool> stillShared, Func<Object, bool> renew) => () =>
    {
        if (a == null || b == null || !stillShared()) return false;
        if (Application.isBatchMode)
        {
            Debug.LogWarning($"[Housing] {Named(a)} and {Named(b)} share a {what}: choose in the editor which one gets a new id (nothing was changed).", b);
            return false;
        }
        int choice = EditorUtility.DisplayDialogComplex(title,
            $"1. {Named(a)}\n    {Where(a)}\n2. {Named(b)}\n    {Where(b)}\n\nThey have the same {what}. In a world that is already running, the one " +
            "that keeps the id keeps what is saved under it (its lease or stay, its rent account, its chests' contents) and the other starts " +
            "afresh: give the new id to the copy - the one that is not in the save.",
            $"New Id For 2 ({Short(b)})", "Cancel", $"New Id For 1 ({Short(a)})");
        if (choice == 1) return false;
        return renew(choice == 0 ? b : a);
    };

    private static string Named(Object o) =>
        o is Storage st ? $"The chest '{st.gameObject.name}'" : o is Component c ? Quote(c) : o != null ? $"'{o.name}'" : "''";

    private static string Short(Object o)
    {
        string n = o is Component c ? c.gameObject.name : o != null ? o.name : "";
        return n.Length > 20 ? n.Substring(0, 19) + "…" : n;
    }

    /// <summary>
    /// A new Property Id for the property, and new ids for its chests and doors that share theirs with another place's: one
    /// choice moves the whole place (in a world already running, it starts afresh). Undoable.
    /// </summary>
    public static bool RenewIds(Property p)
    {
        if (p == null) return false;
        bool done = NewId(p, "propertyId");
        done |= RenewShared(p, p.chests, Belongings(p, p.doors), HousingSceneObjects.Open());
        return done;
    }

    /// <summary>A new Inn Id for the inn, and new ids for its rooms' chests and doors that share theirs with another place's. Undoable.</summary>
    public static bool RenewIds(Inn inn)
    {
        if (inn == null) return false;
        bool done = NewId(inn, "innId");
        HousingSceneObjects s = HousingSceneObjects.Open();
        foreach (InnRoom r in HousingEditors.RoomsOf(inn)) done |= RenewShared(r, r.chests, RoomDoors(r), s);
        return done;
    }

    private static bool RenewRoom(InnRoom r)
    {
        if (r == null) return false;
        bool done = NewId(r, "roomId");
        done |= RenewShared(r, r.chests, RoomDoors(r), HousingSceneObjects.Open());
        return done;
    }

    /// <summary>New ids for the place's chests and doors whose ids other places' chests and doors have too.</summary>
    private static bool RenewShared(Component place, List<HousingChest> chests, List<LockableDoor> doors, HousingSceneObjects s)
    {
        var taken = new HashSet<string>();
        foreach (Property p in s.properties)
            if (p != null && p != place && p.chests != null)
                foreach (HousingChest c in p.chests)
                    if (c != null && !string.IsNullOrEmpty(c.id)) taken.Add(c.id);
        foreach (InnRoom r in s.rooms)
            if (r != null && r != place && r.chests != null)
                foreach (HousingChest c in r.chests)
                    if (c != null && !string.IsNullOrEmpty(c.id)) taken.Add(c.id);
        var renew = new List<Storage>();
        if (chests != null)
            foreach (HousingChest c in chests)
                if (c != null && c.storage != null && !string.IsNullOrEmpty(c.id) && taken.Contains(c.id)) renew.Add(c.storage);
        bool done = false;
        foreach (Storage st in renew) done |= NewChestId(place, st);
        var doorIds = new HashSet<string>();
        foreach (LockableDoor d in s.doors)
            if (d != null && !doors.Contains(d)) doorIds.Add(d.DoorId);
        foreach (LockableDoor d in doors)
            if (d != null && doorIds.Contains(d.DoorId)) done |= NewId(d, "doorId");
        return done;
    }

    // ================================================================== the menu
    /// <summary>Every housing check of the open scenes (and the settings), written to the Console.</summary>
    public static void ValidateOpenScenes()
    {
        var issues = new List<ValidationIssue>();
        foreach (GameObject root in AgricultureEditors.OpenSceneRoots()) CheckRoot(root, issues);
        CheckProject(issues);
        issues.Sort((a, b) => b.severity.CompareTo(a.severity));
        int errors = 0, warnings = 0;
        foreach (ValidationIssue i in issues)
        {
            i.Log();
            if (i.severity == IssueSeverity.Error) errors++;
            else if (i.severity == IssueSeverity.Warning) warnings++;
        }
        HousingSceneObjects s = HousingSceneObjects.Open();
        Debug.Log($"[Housing] {s.properties.Count} propert{(s.properties.Count == 1 ? "y" : "ies")}, {s.inns.Count} inn(s) and {s.rooms.Count} room(s) in the open scenes: " +
                  (issues.Count == 0 ? "no problems." : $"{errors} error(s), {warnings} warning(s), {issues.Count - errors - warnings} note(s) - " +
                                                         "click a line to select its object; the Project Validator (Window ▸ SimpleMovements ▸ Project Validator) " +
                                                         "has their fix buttons."));
    }

    // ================================================================== helpers
    private static void AddIssues(List<Finding> findings, Component owner, List<ValidationIssue> issues)
    {
        foreach (Finding f in findings)
        {
            Object context = f.context != null ? f.context : owner;
            var issue = new ValidationIssue(f.severity, Category, $"{Quote(owner)}: {f.message}", context, Where(context));
            if (f.fix != null) issue.WithFix(f.fixLabel, f.fix);
            issues.Add(issue);
        }
    }

    private static void AddIssues(HousingOverlaps r, List<ValidationIssue> issues)
    {
        for (int i = 0; i < r.errors.Count; i++)
            issues.Add(new ValidationIssue(IssueSeverity.Error, Category, r.errors[i], r.errorObjects[i], Where(r.errorObjects[i])));
        for (int i = 0; i < r.warnings.Count; i++)
            issues.Add(new ValidationIssue(IssueSeverity.Warning, Category, r.warnings[i], r.warningObjects[i], Where(r.warningObjects[i])));
    }

    /// <summary>"'Rose Cottage'", or "'Rose Cottage' (Property 3)" when its object has another name.</summary>
    private static string Quote(Component c)
    {
        if (c == null) return "''";
        string shown = c is Property p ? p.DisplayName : c is Inn inn ? inn.DisplayName : c is InnRoom room ? room.DisplayName : c.gameObject.name;
        return shown == c.gameObject.name ? $"'{shown}'" : $"'{shown}' ({c.gameObject.name})";
    }

    /// <summary>"Scene ▸ Root/Child" (or "Asset path ▸ Root/Child" in a prefab), for the Project Validator.</summary>
    private static string Where(Object o)
    {
        Transform t = o is Component c ? c.transform : o is GameObject g ? g.transform : null;
        if (t == null) return null;
        string path = ValidationUtility.PathOf(t);
        if (EditorUtility.IsPersistent(t.gameObject))
        {
            string asset = AssetDatabase.GetAssetPath(t.gameObject);
            return string.IsNullOrEmpty(asset) ? path : asset + " ▸ " + path;
        }
        return t.gameObject.scene.IsValid() ? t.gameObject.scene.name + " ▸ " + path : path;
    }

    /// <summary>Does the storage roll random items into itself (its private Spawn Items)?</summary>
    public static bool SpawnsItems(Storage s)
    {
        if (s == null) return false;
        using (var so = new SerializedObject(s))
        {
            SerializedProperty p = so.FindProperty("spawnItems");
            return p != null && p.boolValue;
        }
    }

    private static bool StopSpawning(Storage s)
    {
        if (s == null) return false;
        var so = new SerializedObject(s);
        SerializedProperty p = so.FindProperty("spawnItems");
        if (p == null || !p.boolValue) return false;
        p.boolValue = false;
        so.ApplyModifiedProperties();
        return true;
    }

    /// <summary>A new id in a string field of a component (Undo).</summary>
    public static bool NewId(Object o, string field)
    {
        if (o == null) return false;
        var so = new SerializedObject(o);
        SerializedProperty p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.String) return false;
        p.stringValue = Property.NewId();
        so.ApplyModifiedProperties();
        return true;
    }

    /// <summary>A new id for the chest entry of <paramref name="storage"/> in the Chests of <paramref name="owner"/> (a property or a room; Undo).</summary>
    public static bool NewChestId(Object owner, Storage storage)
    {
        if (owner == null || storage == null) return false;
        var so = new SerializedObject(owner);
        SerializedProperty list = so.FindProperty("chests");
        if (list == null || !list.isArray) return false;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("storage").objectReferenceValue != storage) continue;
            entry.FindPropertyRelative("id").stringValue = Property.NewId();
            so.ApplyModifiedProperties();
            return true;
        }
        return false;
    }
}
#endif
