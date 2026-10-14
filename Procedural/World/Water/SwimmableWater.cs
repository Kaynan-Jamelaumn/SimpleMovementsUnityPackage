using UnityEngine;

/// <summary>
/// A hand-placed body of water characters can swim and drown in: a pool, a lake built into a city, a flooded dungeon
/// room. Put it on an object with a BoxCollider covering the water: the top of the box is the surface, its bottom the
/// floor. The collider is made a trigger. (The generated terrain's water needs nothing: it is known per cell.)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SwimmableWater : MonoBehaviour
{
    private BoxCollider box;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void OnEnable()
    {
        if (box == null)
            box = GetComponent<BoxCollider>();
        WaterQuery.Register(this);
    }

    private void OnDisable()
    {
        WaterQuery.Unregister(this);
        Submersion.ForgetSource(this);
    }

    /// <summary>The water at <paramref name="position"/> when it is inside this volume (or just above its surface).</summary>
    public bool TryGetWater(Vector3 position, out float surface, out float bottom)
    {
        surface = bottom = float.NaN;
        if (box == null || !box.enabled)
            return false;
        Bounds b = box.bounds;
        if (position.x < b.min.x || position.x > b.max.x || position.z < b.min.z || position.z > b.max.z)
            return false;
        if (position.y < b.min.y - 1f || position.y > b.max.y + 2.5f)
            return false;
        surface = b.max.y;
        bottom = b.min.y;
        return true;
    }

    private void OnTriggerEnter(Collider other) => Submersion.Enter(this, other);

    private void OnTriggerExit(Collider other) => Submersion.Exit(this, other);
}
