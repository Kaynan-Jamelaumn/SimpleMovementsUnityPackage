using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shows the way to a property on this machine only: its land outlined just above the ground and a tall beam of light
/// at its centre, for <see cref="ShowSeconds"/> real seconds or until the player stands on the land. One marker at a
/// time (showing another replaces it); it has no colliders and changes nothing in the game.
/// </summary>
[DisallowMultipleComponent]
public class HousingMarker : MonoBehaviour
{
    /// <summary>Real seconds a marker stays.</summary>
    public const float ShowSeconds = 120f;
    private const float Lift = 0.15f;
    private const float BeamHeight = 45f;
    private static readonly Color Colour = new Color(1f, 0.8f, 0.38f, 1f);

    private static HousingMarker current;
    private static Material material;

    private Property property;
    private CombatEntity player;
    private float until, nextCheck;
    private LineRenderer outline, beam;

    /// <summary>The property marked now (null: none).</summary>
    public static Property Showing => current != null ? current.property : null;

    /// <summary>Marks <paramref name="p"/> for <paramref name="player"/> (null player: it stays its time).</summary>
    public static HousingMarker Show(Property p, CombatEntity player)
    {
        Hide();
        if (p == null) return null;
        var go = new GameObject("Housing Marker");
        current = go.AddComponent<HousingMarker>();
        current.Begin(p, player);
        return current;
    }

    /// <summary>Takes the marker away.</summary>
    public static void Hide()
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        material = null;
    }

    private static Material Material()
    {
        if (material != null) return material;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) return null;
        material = new Material(s) { name = "Housing Marker", hideFlags = HideFlags.DontSave };
        return material;
    }

    private void Begin(Property p, CombatEntity who)
    {
        property = p;
        player = who;
        until = Time.unscaledTime + ShowSeconds;
        List<Vector2> shape = p.land != null && p.land.Count >= 3 ? p.land : p.house;
        if (shape == null || shape.Count < 2) shape = HousingGeometry.Rectangle(Vector2.zero, new Vector2(4f, 4f));

        outline = Line("Outline", 0.14f);
        outline.loop = true;
        var points = new Vector3[shape.Count];
        for (int i = 0; i < shape.Count; i++) points[i] = p.World(shape[i], Lift);
        outline.positionCount = points.Length;
        outline.SetPositions(points);

        beam = Line("Beam", 0.6f);
        Vector3 foot = p.World(HousingGeometry.Bounds(shape).center, 0f);
        beam.positionCount = 2;
        beam.SetPosition(0, foot);
        beam.SetPosition(1, foot + Vector3.up * BeamHeight);
        Pulse();
    }

    private LineRenderer Line(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = LightProbeUsage.Off;
        lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Material m = Material();
        if (m != null) lr.sharedMaterial = m;
        return lr;
    }

    private void Pulse()
    {
        float a = 0.65f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f);
        if (outline != null)
        {
            Color c = Colour;
            c.a = a;
            outline.startColor = outline.endColor = c;
        }
        if (beam != null)
        {
            Color bottom = Colour, top = Colour;
            bottom.a = a * 0.9f;
            top.a = 0f;
            beam.startColor = bottom;
            beam.endColor = top;
        }
    }

    private void Update()
    {
        if (property == null || !property.isActiveAndEnabled || Time.unscaledTime > until)
        {
            Destroy(gameObject);
            return;
        }
        Pulse();
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        // Arrived: the player stands on the land.
        if (player != null && property.Contains(player.transform.position)) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
