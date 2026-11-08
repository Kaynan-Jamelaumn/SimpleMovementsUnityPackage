using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A room of an <see cref="Inn"/> (a child of it): its floor (an area, for the checks and for "inside the room"), its
/// bed, its chest(s) and its door(s). While a guest's stay lasts, only the guest - and those they let in - sleep in the
/// bed, open the chest and the door; when it ends, what is left in the chest is held for the guest to claim.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Housing/Inn Room")]
public class InnRoom : MonoBehaviour
{
    [Tooltip("Identifies the room in the housing save. Made automatically; keep it unique within its inn.")]
    [SerializeField] private string roomId = "";
    [Tooltip("Its name (\"Room 3\", \"The Blue Room\").")]
    public string displayName = "Room";
    [Tooltip("How fine it is: multiplies the inn's nightly price (1 = an ordinary room).")]
    [Range(0.25f, 5f)] public float quality = 1f;
    [Tooltip("The room's floor, metres, in this object's space (X, Z).")]
    public List<Vector2> area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(4f, 4f));
    [Tooltip("Its bed (a Sleep Spot). Empty: one among the children.")]
    public SleepSpot bed;
    [Tooltip("Its chests. Children with a Storage are found by themselves.")]
    public List<HousingChest> chests = new List<HousingChest>();
    [Tooltip("Its doors. Children with a Lockable Door are found by themselves.")]
    public List<LockableDoor> doors = new List<LockableDoor>();

    public string RoomId
    {
        get
        {
            if (string.IsNullOrEmpty(roomId)) roomId = Application.isPlaying ? HousingIds.Stable(this, "room") : Property.NewId();
            return roomId;
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Room" : displayName;
    public Inn Inn => GetComponentInParent<Inn>(true);

    /// <summary>
    /// Metres of one storey: a point counts as in the room from just under its floor to this high above it (a room on the
    /// floor above, at least this far up, is another room).
    /// </summary>
    public const float StoreyHeight = 2.5f;

    /// <summary>Closed: another room of its inn has the same id (it would share its stay). Set at run time.</summary>
    public bool OutOfOrder { get; internal set; }

    private void Reset()
    {
        // Reset keeps the ids it had (Unity empties the fields first; the editor remembered them): stays and chest records
        // are named by them.
        roomId = HousingIds.Recall(this, "room") ?? Property.NewId();
        Collect();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(roomId)) roomId = Property.NewId();
        HousingIds.Remember(this, "room", roomId);
        foreach (HousingChest c in chests)
        {
            if (c == null) continue;
            if (string.IsNullOrEmpty(c.id)) c.id = Property.NewId();
            HousingIds.Remember(c.storage, "chest", c.id);
        }
    }

    private void Awake()
    {
        if (Application.isPlaying) Collect();
    }

    /// <summary>
    /// Finds the bed, chests and doors among the children that are not listed, and drops what is listed but lies under
    /// another room (that one's).
    /// </summary>
    public void Collect()
    {
        if (bed != null && UnderOther(bed)) bed = null;
        chests.RemoveAll(x => x == null || x.storage == null || UnderOther(x.storage));
        doors.RemoveAll(x => x == null || UnderOther(x));
        if (bed == null)
            foreach (SleepSpot b in GetComponentsInChildren<SleepSpot>(true))
                if (!UnderOther(b)) { bed = b; break; }
        foreach (Storage s in GetComponentsInChildren<Storage>(true))
        {
            if (UnderOther(s)) continue;
            bool listed = false;
            foreach (HousingChest c in chests) listed |= c != null && c.storage == s;
            if (!listed) chests.Add(new HousingChest { id = Application.isPlaying ? HousingIds.Stable(s, "chest") : HousingIds.Recall(s, "chest") ?? Property.NewId(), storage = s });
        }
        foreach (HousingChest c in chests)
            if (c != null && c.storage != null && string.IsNullOrEmpty(c.id)) c.id = Application.isPlaying ? HousingIds.Stable(c.storage, "chest") : Property.NewId();
        foreach (LockableDoor d in GetComponentsInChildren<LockableDoor>(true))
            if (!doors.Contains(d) && !UnderOther(d)) doors.Add(d);
        chests.RemoveAll(x => x == null || x.storage == null);
        doors.RemoveAll(x => x == null);
    }

    /// <summary>Does the thing lie under another room?</summary>
    private bool UnderOther(Component c)
    {
        InnRoom owner = c.GetComponentInParent<InnRoom>(true);
        return owner != null && owner != this;
    }

    public Vector2 Local(Vector3 world) => HousingGeometry.ToLocal(world, transform.position, transform.rotation);
    public Vector3 World(Vector2 local, float lift = 0f) => transform.position + transform.rotation * new Vector3(local.x, lift, local.y);
    public List<Vector2> AreaWorld() => HousingGeometry.ToWorld(area, transform.position, transform.rotation);

    /// <summary>Is the world point in the room (on its floor, within one storey: <see cref="StoreyHeight"/>)?</summary>
    public bool Contains(Vector3 world)
    {
        float y = world.y - transform.position.y;
        return y >= -0.5f && y < StoreyHeight && HousingGeometry.Contains(area, Local(world));
    }

    private void OnDrawGizmos()
    {
        if (area == null || area.Count < 2) return;
        Gizmos.color = new Color(0.95f, 0.8f, 0.4f, 0.9f);
        for (int i = 0, j = area.Count - 1; i < area.Count; j = i++)
            Gizmos.DrawLine(World(area[j], 0.05f), World(area[i], 0.05f));
    }
}
