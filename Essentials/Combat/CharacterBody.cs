using System.Collections.Generic;
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
    [Tooltip("Height of the model as it is authored (metres) - the height at scale 1. Measured from the body when Fit To Model is on.")]
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
    [Tooltip("Build: width and depth of the body on top of the height (1 = as modelled, 1.15 = broad and muscular, " +
             "0.92 = slender). Set by the race's appearance (Character Archetype ▸ Appearance ▸ Body Width).")]
    [Range(0.6f, 1.6f)] public float bodyWidth = 1f;
    [Tooltip("Smallest (x) and largest (y) final scale, whatever the height and the Height stat ask. 0.5-2 = the character " +
             "is between half and double the model's size (0.9 m to 3.6 m for a 1.8 m model).")]
    public Vector2 scaleLimits = new Vector2(0.5f, 2f);
    [Tooltip("Write what was resized in the Console when the size changes.")]
    public bool logChanges;

    [Header("Fit To The Body")]
    [Tooltip("Measure the real body (feet to the top of the head, shoulder width) and size the CharacterController, capsule " +
             "and NavMeshAgent to it, instead of their authored size. Model Height is measured too. A taller or smaller " +
             "character (race, traits, Height stat) then scales body and colliders together. Measured again when the " +
             "character's body changes (a placeholder body, another body model).")]
    public bool fitToModel = true;
    [Tooltip("Points outside the model that follow the body's height (camera targets...): their height above the feet " +
             "scales with the body. Empty = found automatically (objects named \"...Target...\" and their children).")]
    public Transform[] heightAnchors = new Transform[0];
    [Tooltip("Feet on the ground: when the character stands still on the ground (at the start and after each size " +
             "change) the soles of its feet are measured against the ground under it and the body is moved to touch it - " +
             "whatever the controller's skin width, the avatar's foot height or the body's proportions.")]
    public bool groundFeet = true;

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
    private float appliedWidth = -1f;
    private bool warned;
    private float authoredColliderHeight;   // world height of the collider as authored (anchors were placed for it)
    private float anchorBottom;             // the feet, in the root's space
    private readonly List<(Transform t, Vector3 position, bool rootLevel)> anchors = new List<(Transform, Vector3, bool)>();
    private float ccSkin;
    private Vector3 modelPosition;
    private float groundOffset;      // metres the body is lowered, at scale 1 (scaled with the body)
    private bool groundPending = true;
    private float groundStill;
    private Vector3 lastRootPosition;
    private Transform hips;
    private Animator bodyAnimator;
    private Vector3 hipsWritten;
    private bool hipsShifted;
    private float postureHeight = 1f;   // share of the colliders' height kept (no legs: from the waist up)
    private float postureDrop;          // metres the body is lowered for the posture, at scale 1

    /// <summary>The final size multiplier (1 = the model as authored).</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>The final height in metres.</summary>
    public float FinalHeight => modelHeight * Scale;

    /// <summary>The model this body scales (assigned or found).</summary>
    public Transform Model => model;

    /// <summary>The CharacterController this body resizes (null when there is none).</summary>
    public CharacterController Controller => controller;

    /// <summary>Share of the standing height the colliders keep for the posture (1 = standing).</summary>
    public float PostureHeight => postureHeight;

    /// <summary>
    /// The body's posture (the injury system, without legs): the colliders keep <paramref name="heightFactor"/> of their
    /// height with their bottom where it was, the model (its hips) is lowered by <paramref name="dropMetres"/> at the current
    /// size so the waist rests on the ground, and the height anchors (camera targets) come down with it. (1, 0) = standing.
    /// </summary>
    public void SetPosture(float heightFactor, float dropMetres)
    {
        heightFactor = Mathf.Clamp(heightFactor, 0.1f, 1f);
        float drop = Mathf.Max(0f, dropMetres) / Mathf.Max(0.01f, Scale);
        if (Mathf.Abs(heightFactor - postureHeight) < 0.001f && Mathf.Abs(drop - postureDrop) < 0.001f)
            return;
        postureHeight = heightFactor;
        postureDrop = drop;
        applied = -1f; // apply again even at the same size
        Apply();
        // Standing again with Ground Feet off: LateUpdate no longer moves a generic model, so put it back here.
        if (postureDrop <= 0f && !groundFeet && model != null && Application.isPlaying && Hips() == null)
            model.localPosition = modelPosition;
    }

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
        if (model != null) { modelScale = model.localScale; modelPosition = model.localPosition; }
        if (controller != null) { ccHeight = controller.height; ccRadius = controller.radius; ccCenter = controller.center; ccStep = controller.stepOffset; ccSkin = controller.skinWidth; }
        if (agent != null) { agentHeight = agent.height; agentRadius = agent.radius; }
        if (capsule != null) { capHeight = capsule.height; capRadius = capsule.radius; capCenter = capsule.center; }

        // What the anchors were placed for: the authored collider (or the model).
        Transform root = CharacterRoot();
        float rs = Mathf.Abs(root.lossyScale.y);
        if (controller != null) { authoredColliderHeight = ccHeight * rs; anchorBottom = ccCenter.y - ccHeight * 0.5f; }
        else if (capsule != null) { authoredColliderHeight = capHeight * rs; anchorBottom = capCenter.y - capHeight * 0.5f; }
        else { authoredColliderHeight = modelHeight; anchorBottom = 0f; }
        FindAnchors(root);
        if (fitToModel)
            FitToModel();
    }

    /// <summary>
    /// Measures the body again and fits the colliders to it (the body changed: a placeholder body was built, another
    /// body model was chosen). Keeps the current height setting.
    /// </summary>
    public void Remeasure()
    {
        if (!measured)
        {
            FindParts();
            Measure();
        }
        else if (fitToModel)
            FitToModel();
        hips = null; // the skeleton may be new
        applied = -1f;
        Apply();
    }

    /// <summary>
    /// Model Height and the colliders' size at scale 1 from the body as it is: feet on the colliders' bottom, height to
    /// the top of the head, radius from the shoulders.
    /// </summary>
    private void FitToModel()
    {
        Transform root = CharacterRoot();
        if (!MeasureBody(root, model, out float h, out float shoulders))
            return;
        // The model may already be shown scaled: back to scale 1.
        if (applied > 0f)
        {
            h /= Mathf.Max(0.01f, Scale);
            shoulders /= Mathf.Max(0.01f, (scaleWidth ? Scale : 1f) * Mathf.Clamp(bodyWidth, 0.6f, 1.6f));
        }
        if (h < 0.2f || h > 20f)
            return;
        modelHeight = h;
        float radius = Mathf.Clamp(shoulders * 0.75f, h * 0.12f, h * 0.22f);
        float rs = Mathf.Max(1e-4f, Mathf.Abs(root.lossyScale.y));
        if (controller != null)
        {
            float bottom = ccCenter.y - ccHeight * 0.5f;
            ccHeight = h / rs;
            ccRadius = radius / rs;
            ccCenter = new Vector3(ccCenter.x, bottom + ccHeight * 0.5f, ccCenter.z);
            ccStep = Mathf.Min(ccStep, ccHeight * 0.25f);
        }
        if (agent != null)
        {
            agentHeight = h / rs;
            agentRadius = radius / rs;
        }
        if (capsule != null)
        {
            float bottom = capCenter.y - capHeight * 0.5f;
            float cs = Mathf.Max(1e-4f, Mathf.Abs(capsule.transform.lossyScale.y));
            capHeight = h / cs;
            capRadius = radius / cs;
            capCenter = new Vector3(capCenter.x, bottom + capHeight * 0.5f, capCenter.z);
        }
        if (logChanges)
            Debug.Log($"[CharacterBody] '{name}': body measured {h:0.00} m tall, shoulders {shoulders:0.00} m - colliders fitted (radius {radius:0.00} m).", this);
    }

    /// <summary>
    /// The body's height (feet to the top of the head) and shoulder width in metres, as shown now: the placeholder
    /// body, else the humanoid skeleton, else the visible meshes. False when there is nothing to measure.
    /// </summary>
    public static bool MeasureBody(Transform root, Transform model, out float height, out float shoulders)
    {
        height = shoulders = 0f;
        if (root == null)
            return false;
        ProceduralBody pb = root.GetComponentInChildren<ProceduralBody>(false);
        if (pb != null && !pb.IsShown)
            pb = null; // replaced by a body model: measured from the skeleton / meshes below
        if (pb != null)
        {
            height = pb.WorldHeight;
            shoulders = pb.WorldShoulderWidth;
            return height > 0f;
        }
        Animator a = model != null ? model.GetComponentInParent<Animator>() : null;
        if (a == null || !a.isHuman) a = root.GetComponentInChildren<Animator>(false);
        if (a != null && a.isHuman && a.avatar != null)
        {
            Transform head = a.GetBoneTransform(HumanBodyBones.Head);
            Transform lf = a.GetBoneTransform(HumanBodyBones.LeftFoot), rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
            if (head != null && (lf != null || rf != null))
            {
                Vector3 up = root.up;
                float foot = Mathf.Min(lf != null ? Vector3.Dot(lf.position, up) : float.MaxValue, rf != null ? Vector3.Dot(rf.position, up) : float.MaxValue);
                float headAt = Vector3.Dot(head.position, up);
                // Human proportions: the ankle is 4.7 % of the height above the sole, the head bone 88 %.
                float span = headAt - foot;
                if (span > 0.05f)
                {
                    height = span * (ProceduralBody.ReferenceHeight / 1.5f);
                    Transform top = FindHeadTop(head);
                    if (top != null)
                        height = Mathf.Max(height * 0.85f, Vector3.Dot(top.position, up) - (foot - span * (0.085f / 1.5f)));
                    Transform l = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), r = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    shoulders = l != null && r != null ? Vector3.Distance(l.position, r.position) : height * 0.2f;
                    return true;
                }
            }
        }
        bool any = false;
        var b = new Bounds();
        foreach (Renderer r in (model != null ? model : root).GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.GetComponentInParent<CharacterAppearancePartsMarker>() != null || r.GetComponentInParent<Canvas>() != null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any)
            return false;
        height = b.size.y;
        shoulders = Mathf.Min(b.size.x, b.size.z) * 0.8f;
        return height > 0f;
    }

    /// <summary>A "head top" end bone under the head (Mixamo's HeadTop_End...), or null.</summary>
    private static Transform FindHeadTop(Transform head)
    {
        foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            if (n.Contains("headtop") || n.EndsWith("headend"))
                return t;
        }
        return null;
    }

    /// <summary>The height anchors: assigned, else objects named "...Target..." outside the model (and their children).</summary>
    private void FindAnchors(Transform root)
    {
        anchors.Clear();
        var list = new List<Transform>();
        if (heightAnchors != null && heightAnchors.Length > 0)
        {
            foreach (Transform t in heightAnchors)
                if (t != null && t != root) list.Add(t);
        }
        else
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || t is RectTransform || (model != null && t.IsChildOf(model))) continue;
                if (t.GetComponentInParent<Camera>(true) != null || t.GetComponent<ProceduralBody>() != null) continue;
                bool named = t.name.ToLowerInvariant().Contains("target") || (t.parent != null && t.parent != root && t.parent.name.ToLowerInvariant().Contains("target"));
                if (named) list.Add(t);
            }
        }
        foreach (Transform t in list)
            anchors.Add((t, t.localPosition, t.parent == root || !list.Contains(t.parent)));
    }

    /// <summary>Anchors keep their place relative to the body: their height above the feet follows its height.</summary>
    private void ApplyAnchors()
    {
        if (anchors.Count == 0 || authoredColliderHeight <= 0f)
            return;
        float ratio = FinalHeight * postureHeight / authoredColliderHeight;
        Transform root = CharacterRoot();
        foreach (var a in anchors)
        {
            if (a.t == null) continue;
            Vector3 p = a.position;
            if (a.rootLevel && a.t.parent == root)
                p.y = anchorBottom + (p.y - anchorBottom) * ratio;
            else
                p.y *= ratio;
            a.t.localPosition = p;
        }
    }

    /// <summary>Sets the base height (character creation, Character Identity) and applies it.</summary>
    public void SetHeight(float metres)
    {
        height = Mathf.Max(0.1f, metres);
        Apply();
    }

    /// <summary>Sets the build (width and depth on top of the height: a race's bulk) and applies it.</summary>
    public void SetBodyWidth(float multiplier)
    {
        bodyWidth = Mathf.Clamp(multiplier, 0.6f, 1.6f);
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
        if (Mathf.Abs(scale - applied) < 0.0005f && Mathf.Abs(bodyWidth - appliedWidth) < 0.0005f)
            return;
        applied = scale;
        appliedWidth = bodyWidth;
        Scale = scale;
        float w = (scaleWidth ? scale : 1f) * Mathf.Clamp(bodyWidth, 0.6f, 1.6f);

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
                controller.height = Mathf.Max(ccHeight * scale * postureHeight, controller.radius * 2f);
                controller.center = new Vector3(ccCenter.x, bottom + controller.height * 0.5f, ccCenter.z);
                controller.stepOffset = Mathf.Min(ccStep * scale * Mathf.Max(0.5f, postureHeight), controller.height);
                // Unity's advice: skin width about 10 % of the radius (a thick skin keeps the capsule, and the feet, off the ground).
                if (fitToModel)
                    controller.skinWidth = Mathf.Clamp(controller.radius * 0.1f, 0.005f, Mathf.Max(0.005f, ccSkin));
                controller.enabled = was;
            }
            if (agent != null)
            {
                agent.height = agentHeight * scale * postureHeight;
                agent.radius = agentRadius * w;
            }
            if (capsule != null)
            {
                float bottom = capCenter.y - capHeight * 0.5f;
                capsule.radius = capRadius * w;
                capsule.height = Mathf.Max(capHeight * scale * postureHeight, capsule.radius * 2f);
                capsule.center = new Vector3(capCenter.x, bottom + capsule.height * 0.5f, capCenter.z);
            }
        }
        ApplyAnchors();
        groundPending = true; // the feet are measured again at the new size
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

    // ------------------------------------------------------------------ feet on the ground
    private void LateUpdate()
    {
        if ((!groundFeet && postureDrop <= 0f) || !Application.isPlaying)
            return;
        if (groundPending && postureDrop <= 0f)
            TryGround(); // without feet (no legs) there is nothing to measure
        ApplyGroundOffset();
    }

    /// <summary>Measures how far the soles are above the ground once the character has stood still on it for a moment.</summary>
    private void TryGround()
    {
        Transform root = CharacterRoot();
        Vector3 moved = root.position - lastRootPosition;
        lastRootPosition = root.position;
        if (moved.sqrMagnitude > 0.0004f) { groundStill = 0f; return; } // moving (also seen on other machines' copies)
        groundStill += Time.deltaTime;
        if (groundStill < 0.4f)
            return;
        if (!GroundBelow(root, out float ground) || !LowestSole(root, out float sole))
            return;
        Vector3 up = root.up;
        float gap = sole - ground;
        float limit = Mathf.Max(0.05f, FinalHeight * 0.3f);
        if (Mathf.Abs(gap) > limit) { groundPending = false; return; } // not standing on this ground (a ledge, a step)
        // The hips are measured as the animation left them (not lowered yet this frame); a moved model already is.
        Transform h = Hips();
        bool alreadyLowered = h == null || (hipsShifted && (h.position - hipsWritten).sqrMagnitude < 1e-10f);
        groundOffset = (alreadyLowered ? groundOffset : 0f) + gap / Mathf.Max(0.01f, Scale);
        groundOffset = Mathf.Clamp(groundOffset, -modelHeight * 0.3f, modelHeight * 0.3f);
        groundPending = false;
        if (logChanges)
            Debug.Log($"[CharacterBody] '{name}': feet were {gap * 100f:0.0} cm above the ground - body moved {groundOffset * Scale * 100f:0.0} cm down.", this);
    }

    /// <summary>Lowers the body by the measured gap: the hips (after the animation wrote them) or the model.</summary>
    private void ApplyGroundOffset()
    {
        Transform root = CharacterRoot();
        float offset = (groundOffset + postureDrop) * Scale;
        Transform h = Hips();
        if (h != null)
        {
            // The animation writes the hips every frame; a frame it did not (culled) keeps the shift already made.
            if (hipsShifted && (h.position - hipsWritten).sqrMagnitude < 1e-10f)
                return;
            if (Mathf.Abs(offset) < 1e-4f) { hipsShifted = false; return; }
            h.position -= root.up * offset;
            hipsWritten = h.position;
            hipsShifted = true;
        }
        else if (model != null && model.parent != null)
        {
            model.localPosition = modelPosition - model.parent.InverseTransformVector(root.up * offset);
        }
    }

    /// <summary>The humanoid hips the animation drives, or null (generic models are moved as a whole).</summary>
    private Transform Hips()
    {
        if (hips != null && bodyAnimator != null && bodyAnimator.isHuman)
            return hips;
        Transform root = CharacterRoot();
        bodyAnimator = model != null ? model.GetComponentInParent<Animator>() : null;
        if (bodyAnimator == null) bodyAnimator = root.GetComponentInChildren<Animator>();
        hips = bodyAnimator != null && bodyAnimator.isHuman && bodyAnimator.isActiveAndEnabled ? bodyAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
        return hips;
    }

    private static readonly RaycastHit[] groundHits = new RaycastHit[16];

    /// <summary>The ground under the character (its own colliders and triggers left out).</summary>
    private bool GroundBelow(Transform root, out float ground)
    {
        ground = 0f;
        Vector3 up = root.up;
        float reach = Mathf.Max(0.5f, FinalHeight * 0.5f);
        Vector3 from = root.position + up * reach;
        if (controller != null)
            from = controller.transform.TransformPoint(controller.center);
        int n = ScenePhysics.RaycastNonAlloc(from, -up, groundHits, reach * 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = groundHits[i].collider;
            if (c == null || c.transform.IsChildOf(root)) continue;
            if (groundHits[i].distance < best) { best = groundHits[i].distance; ground = Vector3.Dot(groundHits[i].point, up); found = true; }
        }
        return found;
    }

    /// <summary>The lowest point of the feet as they are drawn now (skinned meshes baked around the foot bones).</summary>
    private bool LowestSole(Transform root, out float sole)
    {
        sole = float.MaxValue;
        Vector3 up = root.up;
        Animator a = bodyAnimator != null ? bodyAnimator : root.GetComponentInChildren<Animator>();
        Transform lf = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
        Transform rf = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.RightFoot) : null;
        float near = Mathf.Max(0.1f, FinalHeight * 0.16f);
        Transform scope = model != null ? model : root;
        var baked = new Mesh();
        bool any = false;
        foreach (SkinnedMeshRenderer smr in scope.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr == null || !smr.enabled || smr.sharedMesh == null) continue;
            if (smr.GetComponentInParent<CharacterAppearancePartsMarker>() != null) continue;
            if (!smr.sharedMesh.isReadable && !Application.isEditor) continue;
            smr.BakeMesh(baked, true);
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Vector3[] v = baked.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(v[i]);
                if (lf != null || rf != null)
                {
                    // Only around the feet: a cape or a weapon hanging low is not a sole.
                    bool atFoot = (lf != null && Flat(w - lf.position, up).sqrMagnitude < near * near) ||
                                  (rf != null && Flat(w - rf.position, up).sqrMagnitude < near * near);
                    if (!atFoot) continue;
                }
                float y = Vector3.Dot(w, up);
                if (y < sole) { sole = y; any = true; }
            }
        }
        Destroy(baked);
        return any;
    }

    private static Vector3 Flat(Vector3 v, Vector3 up) => v - up * Vector3.Dot(v, up);

    /// <summary>Measures the feet against the ground again (another body, a teleport onto other ground...).</summary>
    public void RegroundFeet() => groundPending = true;

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
