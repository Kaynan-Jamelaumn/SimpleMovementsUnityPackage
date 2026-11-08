using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An inn: a building with rooms to let by the night (its <see cref="InnRoom"/> children, each with a bed and a chest).
/// An Innkeeper NPC lets them: a player pays for some nights and gets a free room; when the stay ends the room is
/// theirs no more and what they left in its chest is held for them to claim. The price follows how full the inn is.
/// <para>Its <see cref="area"/> (the building's footprint) is checked against the properties around it: areas are truthful.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Housing/Inn")]
public class Inn : MonoBehaviour
{
    [Tooltip("Identifies the inn in the housing save. Made automatically; keep it unique.")]
    [SerializeField] private string innId = "";
    [Tooltip("Its name (\"The Gilded Goose\").")]
    public string displayName = "Inn";
    [Tooltip("The part of the city it is in.")]
    public string district = "City";
    [Tooltip("What a night in an ordinary room costs when the inn is as full as usual (a room's Quality multiplies it).")]
    [Min(1)] public int nightlyPrice = 20;
    [Tooltip("The building's footprint, metres, in this object's space (X, Z).")]
    public List<Vector2> area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(20f, 14f));
    [Tooltip("Its rooms (children with an Inn Room are found by themselves).")]
    public List<InnRoom> rooms = new List<InnRoom>();

    public string InnId
    {
        get
        {
            if (string.IsNullOrEmpty(innId)) innId = Application.isPlaying ? HousingIds.Stable(this, "inn") : Property.NewId();
            return innId;
        }
    }

    /// <summary>Closed: another inn has the same id. Set at run time.</summary>
    public bool OutOfOrder { get; internal set; }

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Inn" : displayName;
    public string District => string.IsNullOrWhiteSpace(district) ? "City" : district.Trim();

    private void Reset()
    {
        // Reset keeps the id it had (Unity empties the fields first; the editor remembered it): stays are named by it.
        innId = HousingIds.Recall(this, "inn") ?? Property.NewId();
        Collect();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(innId)) innId = Property.NewId();
        HousingIds.Remember(this, "inn", innId);
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

    /// <summary>Adds the Inn Rooms among the children that are not listed.</summary>
    public void Collect()
    {
        foreach (InnRoom r in GetComponentsInChildren<InnRoom>(true))
            if (!rooms.Contains(r)) rooms.Add(r);
        // A room moved under another inn is that inn's (still listed here, it would be let by both).
        rooms.RemoveAll(r => r == null || !Owns(r));
    }

    /// <summary>Is the room this inn's: listed here and not under another inn (the inn above it lets it).</summary>
    public bool Owns(InnRoom r)
    {
        if (r == null) return false;
        Inn above = r.GetComponentInParent<Inn>(true);
        return above == null || above == this;
    }

    /// <summary>The room with this id (a closed room - its id is another room's - is never it).</summary>
    public InnRoom Room(string roomId)
    {
        foreach (InnRoom r in rooms)
            if (r != null && !r.OutOfOrder && r.RoomId == roomId) return r;
        return null;
    }

    public Vector2 Local(Vector3 world) => HousingGeometry.ToLocal(world, transform.position, transform.rotation);
    public Vector3 World(Vector2 local, float lift = 0f) => transform.position + transform.rotation * new Vector3(local.x, lift, local.y);
    public List<Vector2> AreaWorld() => HousingGeometry.ToWorld(area, transform.position, transform.rotation);
    public bool Contains(Vector3 world) => HousingGeometry.Contains(area, Local(world));

    /// <summary>The facts the rules price it by.</summary>
    public InnFacts Facts()
    {
        var f = new InnFacts { id = InnId, name = DisplayName, district = District, nightlyPrice = Mathf.Max(1, nightlyPrice) };
        foreach (InnRoom r in rooms)
            if (r != null && !r.OutOfOrder && Owns(r)) f.rooms.Add(new InnRoomFacts { id = r.RoomId, name = r.DisplayName, quality = Mathf.Max(0.1f, r.quality) });
        return f;
    }

    private void OnDrawGizmos()
    {
        if (area == null || area.Count < 2) return;
        Gizmos.color = new Color(0.8f, 0.55f, 0.95f, 0.9f);
        for (int i = 0, j = area.Count - 1; i < area.Count; j = i++)
            Gizmos.DrawLine(World(area[j], 0.05f), World(area[i], 0.05f));
    }
}
