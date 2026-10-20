using UnityEngine;

/// <summary>
/// Marks a world item that belongs to a severed limb: the limb itself, or the armour and held item that fell with it.
/// The health panel and tooltips use it, and it removes the object after a while (Injury Settings ▸ Limbs ▸ Cleanup) so
/// limbs and dropped gear do not pile up in the world. Picked up, the object is gone and so is this.
/// </summary>
public class SeveredLimbLink : MonoBehaviour
{
    [Tooltip("Id of the limb's record (see SeveredLimbRegistry).")]
    public int recordId;
    [Tooltip("This object is the limb (off = something that fell with it).")]
    public bool isLimb;
    [Tooltip("Time (Time.time) this object is removed from the world. 0 = never.")]
    public float despawnAt;

    public SeveredLimbRecord Record => InjuryController.FindRecord(recordId);

    /// <summary>
    /// Links <paramref name="go"/> to <paramref name="record"/>; it is removed after <paramref name="despawnSeconds"/>
    /// (0 = never).
    /// </summary>
    public static void Attach(GameObject go, SeveredLimbRecord record, bool limb, float despawnSeconds = 0f)
    {
        if (go == null || record == null)
            return;
        SeveredLimbLink l = go.GetComponent<SeveredLimbLink>();
        if (l == null)
            l = go.AddComponent<SeveredLimbLink>();
        l.recordId = record.Id;
        l.isLimb = limb;
        l.despawnAt = despawnSeconds > 0f ? Time.time + despawnSeconds : 0f;
        if (limb)
            record.DespawnAt = l.despawnAt;
    }

    private void Update()
    {
        if (despawnAt <= 0f || Time.time < despawnAt)
            return;
        despawnAt = 0f;
        // Removed where the game rules run (offline, the server); clients see the network despawn.
        if (!GameNet.NetAuthority.RunsRules(gameObject))
            return;
        ItemPickable p = GetComponent<ItemPickable>();
        if (p != null)
        {
            if (p.Taken)
                return;
            p.Taken = true;
        }
        if (isLimb)
        {
            SeveredLimbRecord r = Record;
            if (r != null && !r.Consumed)
            {
                r.Gone = true;
                r.DespawnAt = 0f;
                SeveredLimbRegistry.NotifyChanged(r);
                if (r.Owner != null)
                    r.Owner.LimbRecordChanged(r);
            }
        }
        WorldItems.Remove(gameObject);
    }
}
