using UnityEngine;

/// <summary>The shape of what a mob sees (Mob Profile ▸ Perception ▸ Vision Shape).</summary>
public enum MobVisionShape
{
    /// <summary>A cone in front of it: Sight Range far, Field Of View wide. The usual.</summary>
    Cone,
    /// <summary>All around it: everything within Sight Range, whichever way it faces.</summary>
    Sphere,
    /// <summary>A corridor straight ahead: Sight Range long, Vision Width wide. A guard watching a passage; tunnel vision.</summary>
    Box,
    /// <summary>Blind: it sees nothing. Only touch (Close Sense Radius), hearing and pain tell it where anyone is.</summary>
    None,
}

/// <summary>
/// The geometry of a mob's sight: is a character inside the shape the mob sees (before walls are checked)? Flat, like the
/// rest of the perception: height differences don't count, walls and floors do (line of sight).
/// </summary>
public static class MobVision
{
    /// <summary>
    /// Is a character at <paramref name="toTarget"/> (from the eyes) inside the shape? <paramref name="forward"/> is the
    /// way the mob faces, <paramref name="range"/> how far it sees (already shortened for a sneaking character),
    /// <paramref name="targetRadius"/> the character's body radius (its edge is enough to be seen).
    /// </summary>
    public static bool Contains(MobVisionShape shape, Vector3 forward, Vector3 toTarget, float range, float fieldOfView, float width, float targetRadius)
    {
        if (shape == MobVisionShape.None || range <= 0f)
            return false;
        var flat = new Vector2(toTarget.x, toTarget.z);
        var facing = new Vector2(forward.x, forward.z);
        float distance = flat.magnitude;
        float reach = range + Mathf.Max(0f, targetRadius);
        switch (shape)
        {
            case MobVisionShape.Sphere:
                return distance <= reach;

            case MobVisionShape.Box:
            {
                if (facing.sqrMagnitude < 1e-6f)
                    return distance <= reach;
                facing.Normalize();
                float ahead = Vector2.Dot(flat, facing);
                float aside = Mathf.Abs(flat.x * facing.y - flat.y * facing.x);
                return ahead >= 0f && ahead <= reach && aside <= Mathf.Max(0f, width) * 0.5f + Mathf.Max(0f, targetRadius);
            }

            default:   // Cone
            {
                if (distance > reach)
                    return false;
                if (fieldOfView >= 359f || distance < 1e-3f || facing.sqrMagnitude < 1e-6f)
                    return true;
                return Vector2.Angle(facing, flat) <= fieldOfView * 0.5f;
            }
        }
    }

    /// <summary>The farthest any point of the shape is from the mob (how far around it characters are worth checking).</summary>
    public static float Extent(MobVisionShape shape, float range, float width)
    {
        switch (shape)
        {
            case MobVisionShape.None: return 0f;
            case MobVisionShape.Box: return Mathf.Sqrt(range * range + width * width * 0.25f);
            default: return range;
        }
    }
}
