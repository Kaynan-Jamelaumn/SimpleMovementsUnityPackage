using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a static or moving danger for the AI (lava, spike traps, fire pits). Mobs will not path through it and step
/// out of it. Does not deal damage by itself.
/// </summary>
[AddComponentMenu("Combat/AI Hazard Source")]
public class AIHazardSource : MonoBehaviour
{
    [Tooltip("Area of the hazard, relative to this object (forward = this object's forward).")]
    public HitShape shape = new HitShape { type = HitShapeType.Circle, radius = 2f };
    [Tooltip("How much mobs fear it (0-1). High values make them avoid it even when chasing.")]
    [Range(0f, 1f)] public float severity = 0.8f;
    [Tooltip("Update the hazard position every frame (for moving hazards).")]
    public bool moving = false;

    private HazardArea area;

    private void OnEnable() => Register();

    private void OnDisable()
    {
        HazardRegistry.Remove(area);
        area = null;
    }

    private void Register()
    {
        HazardRegistry.Remove(area);
        ResolvedShape r = ResolvedShape.Resolve(shape, transform.position, transform.rotation, 1f);
        area = HazardRegistry.AddShape(null, TargetFilter.All, r, float.MinValue, float.MaxValue, severity);
    }

    private void Update()
    {
        if (!moving || area == null)
            return;
        area.shape = ResolvedShape.Resolve(shape, transform.position, transform.rotation, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        var r = ResolvedShape.Resolve(shape, transform.position, transform.rotation, 1f);
        var outer = new List<Vector3>();
        var inner = new List<Vector3>();
        r.GetOutline(outer, inner);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
        for (int i = 1; i < outer.Count; i++)
            Gizmos.DrawLine(outer[i - 1], outer[i]);
        for (int i = 1; i < inner.Count; i++)
            Gizmos.DrawLine(inner[i - 1], inner[i]);
    }
}
