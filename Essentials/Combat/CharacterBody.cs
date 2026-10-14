using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The character's physical size. It scales the visual model and resizes the CharacterController, NavMeshAgent and
/// capsule collider; body-part hitboxes and weapon sockets on the model's bones follow the model. Melee hit shapes,
/// ability strike heights and reach use the measured body, so a taller character is easier to hit in the head and
/// reaches a little further. The root transform is never scaled, so cameras and child rigs keep their size.
/// <para>
/// <b>How the size is computed</b> (when the game runs; nothing changes in Edit mode):
/// <code>scale = Height / Model Height × Height stat, clamped to Scale Limits</code>
/// <list type="bullet">
/// <item><b>Model Height</b>: how tall your model is at scale 1 (measure it once: 1.8 for a normal humanoid).</item>
/// <item><b>Height</b>: how tall this character should be. A <see cref="CharacterIdentity"/> on the character
/// <i>overwrites</i> it with its own height (or its race's default height, clamped to the race's height range).</item>
/// <item><b>Height stat</b> (CombatStats): races, traits, items and buffs (+10% = 10% taller).</item>
/// <item><b>Scale Limits</b>: the smallest and largest scale allowed, whatever the numbers above say (0.5-2 = from half
/// to double the model's size). It protects against a giant or tiny character from a typo or stacked buffs.</item>
/// </list>
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class CharacterBody : MonoBehaviour
{
    [Tooltip("Height of the model as it is authored (metres) - the height at scale 1.")]
    [Min(0.1f)] public float modelHeight = 1.8f;
    [Tooltip("The character's height before the Height stat (metres). A Character Identity on the character overwrites " +
             "it with its own height (or its race's default) when the game starts.")]
    [Min(0.1f)] public float height = 1.8f;
    [Tooltip("The visual model to scale (a CHILD of the character: never the root). Empty = found automatically: the " +
             "Movement Model's Player Shell Object, else the child holding the Animator or the skinned meshes.")]
    public Transform model;
    [Tooltip("Resize the CharacterController (and a NavMeshAgent / capsule collider on the character) with the height. " +
             "The feet stay where they are; the radius follows with Scale Width.")]
    public bool resizeColliders = true;
    [Tooltip("Width follows height (true) or only the height grows (false: tall thin characters).")]
    public bool scaleWidth = true;
    [Tooltip("Smallest (x) and largest (y) final scale, whatever the height and the Height stat ask. 0.5-2 = the character " +
             "is between half and double the model's size (0.9 m to 3.6 m for a 1.8 m model).")]
    public Vector2 scaleLimits = new Vector2(0.5f, 2f);
    [Tooltip("Write what was resized in the Console when the size changes.")]
    public bool logChanges;

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
    private bool warned;

    /// <summary>The final size multiplier (1 = the model as authored).</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>The final height in metres.</summary>
    public float FinalHeight => modelHeight * Scale;

    /// <summary>The model this body scales (assigned or found).</summary>
    public Transform Model => model;

    /// <summary>The CharacterController this body resizes (null when there is none).</summary>
    public CharacterController Controller => controller;

    private void Awake()
    {
        stats = CombatStats.For(this, addIfMissing: true);
        entity = GetComponentInParent<CombatEntity>();
        FindParts();
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

    /// <summary>
    /// The colliders on this object, else on the character's root (the CharacterController is on the prefab root), and
    /// the model when none is assigned.
    /// </summary>
    private void FindParts()
    {
        Transform root = CharacterRoot();
        controller = GetComponent<CharacterController>();
        if (controller == null) controller = root.GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = root.GetComponent<NavMeshAgent>();
        capsule = GetComponent<CapsuleCollider>();
        if (capsule == null && controller == null) capsule = root.GetComponent<CapsuleCollider>();
        if (model == null)
            model = FindModel(root);
    }

    /// <summary>The object the character is built on: the CharacterController's / the CombatEntity's, else this one.</summary>
    private Transform CharacterRoot()
    {
        CharacterController cc = GetComponentInParent<CharacterController>();
        if (cc != null) return cc.transform;
        CombatEntity e = GetComponentInParent<CombatEntity>();
        return e != null ? e.transform : transform;
    }

    /// <summary>
    /// The visual model under <paramref name="root"/>: the player's shell (PlayerMovementModel ▸ Player Shell Object), a
    /// child holding an Animator, else the child holding most skinned meshes. Never the root itself.
    /// </summary>
    public static Transform FindModel(Transform root)
    {
        if (root == null)
            return null;
        PlayerMovementModel m = root.GetComponentInChildren<PlayerMovementModel>(true);
        if (m != null && m.PlayerShellObject != null && m.PlayerShellObject.transform != root && m.PlayerShellObject.transform.IsChildOf(root))
            return m.PlayerShellObject.transform;
        foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
            if (a != null && a.transform != root && a.gameObject.activeInHierarchy)
                return a.transform;
        Transform best = null;
        int bestCount = 0;
        foreach (Transform child in root)
        {
            int count = child.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            if (count > bestCount)
            {
                bestCount = count;
                best = child;
            }
        }
        return best;
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

    /// <summary>Sets the base height (character creation, Character Identity) and applies it.</summary>
    public void SetHeight(float metres)
    {
        height = Mathf.Max(0.1f, metres);
        Apply();
    }

    /// <summary>The scale <paramref name="metres"/> gives with the current model height, Height stat and limits.</summary>
    public float ScaleFor(float metres)
    {
        float scale = metres / Mathf.Max(0.1f, modelHeight) * (stats != null ? stats.HeightScale : 1f);
        return Mathf.Clamp(scale, Mathf.Max(0.05f, scaleLimits.x), Mathf.Max(scaleLimits.x, scaleLimits.y));
    }

    /// <summary>Recomputes the size from the height and the Height stat (called when stats change).</summary>
    public void Apply()
    {
        if (!measured)
        {
            FindParts();
            Measure();
        }
        float scale = ScaleFor(height);
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
                bool was = controller.enabled;
                float bottom = ccCenter.y - ccHeight * 0.5f;
                controller.enabled = false;
                controller.radius = ccRadius * w;
                controller.height = Mathf.Max(ccHeight * scale, controller.radius * 2f);
                controller.center = new Vector3(ccCenter.x, bottom + controller.height * 0.5f, ccCenter.z);
                controller.stepOffset = Mathf.Min(ccStep * scale, controller.height);
                controller.enabled = was;
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

        if (!warned && model == null && (!resizeColliders || (controller == null && agent == null && capsule == null)))
        {
            warned = true;
            Debug.LogWarning($"[CharacterBody] '{name}': nothing to scale - assign the Model (the child holding the meshes) " +
                             "and keep the CharacterController on the character's root.", this);
        }
        if (logChanges)
            Debug.Log($"[CharacterBody] '{name}': {FinalHeight:0.00} m (scale {scale:0.###}) - model " +
                      $"{(model != null ? "'" + model.name + "'" : "none")}, controller {(controller != null ? $"height {controller.height:0.00} radius {controller.radius:0.00}" : "none")}" +
                      (scale <= scaleLimits.x + 0.0001f || scale >= scaleLimits.y - 0.0001f ? " - limited by Scale Limits" : ""), this);
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
