using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The character's physical size. Height = the chosen height (character creation, race range) × the Height stat
/// (races, traits, items, buffs: +10% = 10% taller). It scales the visual model and resizes the CharacterController /
/// NavMeshAgent / capsule (not the root transform, so cameras and child rigs are untouched); body-part hitboxes and
/// weapon sockets on the model's bones follow. Melee hit shapes, ability strike heights and reach use the measured
/// body, so a taller character is easier to hit in the head and reaches a little further.
/// </summary>
[DisallowMultipleComponent]
public class CharacterBody : MonoBehaviour
{
    [Tooltip("Height of the model as it is authored (metres) - the height at scale 1.")]
    [Min(0.1f)] public float modelHeight = 1.8f;
    [Tooltip("The character's height before the Height stat (metres). Set by character creation / the race.")]
    [Min(0.1f)] public float height = 1.8f;
    [Tooltip("The visual model to scale (a child). Empty = only the colliders are resized.")]
    public Transform model;
    [Tooltip("Resize the CharacterController, NavMeshAgent or capsule collider with the height.")]
    public bool resizeColliders = true;
    [Tooltip("Width follows height (true) or only the height grows (false: tall thin characters).")]
    public bool scaleWidth = true;
    [Tooltip("Smallest / largest final scale.")]
    public Vector2 scaleLimits = new Vector2(0.5f, 2f);

    private CombatStats stats;
    private CombatEntity entity;
    private CharacterController controller;
    private NavMeshAgent agent;
    private CapsuleCollider capsule;
    private bool measured;
    private Vector3 modelScale = Vector3.one;
    private float ccHeight, ccRadius, ccStep;
    private Vector3 ccCenter;
    private float agentHeight, agentRadius;
    private float capHeight, capRadius;
    private Vector3 capCenter;
    private float applied = -1f;

    /// <summary>The final size multiplier (1 = the model as authored).</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>The final height in metres.</summary>
    public float FinalHeight => modelHeight * Scale;

    private void Awake()
    {
        stats = CombatStats.For(this, addIfMissing: true);
        entity = GetComponentInParent<CombatEntity>();
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        capsule = GetComponent<CapsuleCollider>();
        Measure();
    }

    private void OnEnable()
    {
        if (stats != null)
            stats.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.Changed -= Apply;
    }

    private void Measure()
    {
        if (measured)
            return;
        measured = true;
        if (model != null) modelScale = model.localScale;
        if (controller != null) { ccHeight = controller.height; ccRadius = controller.radius; ccCenter = controller.center; ccStep = controller.stepOffset; }
        if (agent != null) { agentHeight = agent.height; agentRadius = agent.radius; }
        if (capsule != null) { capHeight = capsule.height; capRadius = capsule.radius; capCenter = capsule.center; }
    }

    /// <summary>Sets the base height (character creation) and applies it.</summary>
    public void SetHeight(float metres)
    {
        height = Mathf.Max(0.1f, metres);
        Apply();
    }

    /// <summary>Recomputes the size from the height and the Height stat (called when stats change).</summary>
    public void Apply()
    {
        Measure();
        float scale = height / Mathf.Max(0.1f, modelHeight) * (stats != null ? stats.HeightScale : 1f);
        scale = Mathf.Clamp(scale, Mathf.Max(0.05f, scaleLimits.x), Mathf.Max(scaleLimits.x, scaleLimits.y));
        if (Mathf.Abs(scale - applied) < 0.0005f)
            return;
        applied = scale;
        Scale = scale;
        float w = scaleWidth ? scale : 1f;

        if (model != null)
            model.localScale = new Vector3(modelScale.x * w, modelScale.y * scale, modelScale.z * w);
        if (resizeColliders)
        {
            if (controller != null)
            {
                // Keep the feet on the ground: the bottom of the capsule stays where it was.
                float bottom = ccCenter.y - ccHeight * 0.5f;
                controller.radius = ccRadius * w;
                controller.height = Mathf.Max(ccHeight * scale, controller.radius * 2f);
                controller.center = new Vector3(ccCenter.x, bottom + controller.height * 0.5f, ccCenter.z);
                controller.stepOffset = Mathf.Min(ccStep * scale, controller.height);
            }
            if (agent != null)
            {
                agent.height = agentHeight * scale;
                agent.radius = agentRadius * w;
            }
            if (capsule != null)
            {
                float bottom = capCenter.y - capHeight * 0.5f;
                capsule.radius = capRadius * w;
                capsule.height = Mathf.Max(capHeight * scale, capsule.radius * 2f);
                capsule.center = new Vector3(capCenter.x, bottom + capsule.height * 0.5f, capCenter.z);
            }
        }
        if (entity != null)
            entity.RemeasureBody();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
        {
            applied = -1f;
            Apply();
        }
    }
#endif
}
