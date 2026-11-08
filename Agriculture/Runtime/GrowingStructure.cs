using UnityEngine;

/// <summary>What a structure over the soil is.</summary>
public enum GrowingStructureKind
{
    /// <summary>Glass: warm, sheltered from rain and frost, a little less light.</summary>
    Greenhouse,
    /// <summary>Cloth or lattice: half the light, cooler, some rain still comes through.</summary>
    ShadeShelter,
    /// <summary>A low glass box: warmer and frost-free nights for seedlings.</summary>
    ColdFrame,
    /// <summary>An enclosed, dim, magical chamber: no weather at all.</summary>
    MagicalChamber,
    /// <summary>Your own numbers.</summary>
    Custom,
}

/// <summary>
/// A greenhouse, a shade shelter, a cold frame or a magical chamber over (part of) a soil field: a box that changes the
/// weather of the cells inside it - how much rain reaches them, how fast they dry, how much light and warmth they get,
/// how well they are kept from frost. Fields work out their covers again when one appears, moves or goes.
/// </summary>
[AddComponentMenu("SimpleMovements/Agriculture/Growing Structure")]
public class GrowingStructure : MonoBehaviour
{
    public GrowingStructureKind kind = GrowingStructureKind.Greenhouse;
    [Tooltip("The covered volume, in the object's local space.")]
    public Vector3 center = new Vector3(0f, 1.5f, 0f);
    public Vector3 size = new Vector3(4f, 3f, 6f);

    [Header("What it does")]
    [Tooltip("Share of the rain that reaches the soil inside (0 = none).")]
    [Range(0f, 1f)] public float rain;
    [Tooltip("How fast the soil inside dries compared with outside.")]
    [Range(0f, 2f)] public float evaporation = 0.6f;
    [Tooltip("Share of the light that comes through.")]
    [Range(0f, 1f)] public float light = 0.85f;
    [Tooltip("Degrees warmer inside.")]
    [Range(-10f, 20f)] public float warmth = 6f;
    [Tooltip("How well it keeps frost off (1 = never freezes inside).")]
    [Range(0f, 1f)] public float frostProtection = 0.85f;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float checkTimer;

    public Bounds WorldBounds
    {
        get
        {
            var b = new Bounds(transform.TransformPoint(center), Vector3.zero);
            Vector3 h = size * 0.5f;
            for (int i = 0; i < 8; i++)
                b.Encapsulate(transform.TransformPoint(center + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z)));
            return b;
        }
    }

    public bool Contains(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world) - center;
        Vector3 h = size * 0.5f;
        return Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.y) <= h.y && Mathf.Abs(l.z) <= h.z;
    }

    private void Reset() => ApplyPreset(kind);

    /// <summary>Sets the numbers of a kind.</summary>
    public void ApplyPreset(GrowingStructureKind k)
    {
        kind = k;
        switch (k)
        {
            case GrowingStructureKind.Greenhouse: rain = 0f; evaporation = 0.6f; light = 0.85f; warmth = 6f; frostProtection = 0.85f; break;
            case GrowingStructureKind.ShadeShelter: rain = 0.6f; evaporation = 0.75f; light = 0.45f; warmth = -1.5f; frostProtection = 0.2f; break;
            case GrowingStructureKind.ColdFrame: rain = 0.15f; evaporation = 0.7f; light = 0.8f; warmth = 3.5f; frostProtection = 0.65f; break;
            case GrowingStructureKind.MagicalChamber: rain = 0f; evaporation = 0.5f; light = 0.3f; warmth = 2f; frostProtection = 1f; break;
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        FarmDirector.Register(this);
    }

    private void OnDisable() => FarmDirector.Unregister(this);

    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled) FarmDirector.CoversChanged(this);
    }

    private void Update()
    {
        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer < 1f) return;
        checkTimer = 0f;
        if ((transform.position - lastPosition).sqrMagnitude > 0.0001f || Quaternion.Angle(transform.rotation, lastRotation) > 0.1f)
        {
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            FarmDirector.CoversChanged(null);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.5f);
        Gizmos.DrawWireCube(center, size);
    }
}
