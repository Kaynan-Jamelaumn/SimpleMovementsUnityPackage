using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Whether a pen's floor is bedded with straw. Saved in scenes as a number: add new values at the END.</summary>
public enum PenBedding
{
    /// <summary>As its kind is: coops, barns, stables, sties and hutches are bedded; pastures and ponds are not.</summary>
    FromKind,
    /// <summary>No bedding (its animals do not miss any).</summary>
    None,
    /// <summary>A bedded floor: straw is laid in it, and its animals miss it when it is spent.</summary>
    Bedded,
}

/// <summary>
/// A habitat for animals - a pasture, a coop, a sty, a hutch, a stable - placed on the land of a house like its garden:
/// a child of the <see cref="Property"/> with an outline on the ground (metres, in this object's space, X and Z), kept clear
/// of the house, the gardens, the zones and the other pens (the Housing checks say so). Its <b>kind</b> decides which
/// species can live in it, its <b>area</b> how many (each species needs its own space), and it has a <b>feeder</b> and a
/// <b>water trough</b>; a pasture also grows grass that grazers eat.
/// <list type="bullet">
/// <item>The pen is the scene's; what is in it - its animals, the feed in its feeder, the water, the grass - is the
/// world's: kept by the <see cref="AnimalDirector"/> with the world's save, living on while nobody is near (and while its
/// scene is not loaded).</item>
/// <item>Its shelter (roof and walls, the warmth they keep, shade), whether its floor is bedded with straw and how fast
/// open ground clears itself of dung come from its kind unless set here (Shelter and Bedding): its animals' comfort and
/// cleanliness depend on them.</item>
/// <item>On a rented plot only the tenant and those they let work the garden may tend it and keep animals in it;
/// anyone may look over the fence. A pen on no property is public.</item>
/// <item>Interactable: a trigger over the pen (Ignore Raycast layer) opens the pen's window.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Animals/Animal Pen")]
public class AnimalPen : MonoBehaviour, IInteractable
{
    [Tooltip("Identifies the pen in the world's save and network messages. Made automatically; keep it unique.")]
    [SerializeField] private string penId = "";
    [Tooltip("Its name (the window's title).")]
    [SerializeField] private string displayName = "Pen";
    [Tooltip("What kind of habitat it is: it decides which species can live in it.")]
    public AnimalHabitatKind kind = AnimalHabitatKind.Pasture;
    [Tooltip("Its outline on the ground: metres, in this object's space (X, Z). Keep the object's scale at 1.")]
    public List<Vector2> outline = HousingGeometry.Rectangle(Vector2.zero, new Vector2(6f, 4f));

    [Header("Stations")]
    [Tooltip("Portions of food its feeder holds.")]
    [Min(0f)] public float feederCapacity = 120f;
    [Tooltip("Litres its water trough holds.")]
    [Min(0f)] public float waterCapacity = 80f;
    [Tooltip("Square metres of open water in its trough (rain falls in, the sun dries it).")]
    [Min(0f)] public float troughSurface = 0.6f;
    [Tooltip("Litres a spring, a pipe or a stream brings into its trough per game hour (0 = only rain and buckets).")]
    [Min(0f)] public float refillLitresPerHour;
    [Tooltip("Where the feeder stands (metres, in this object's space).")]
    public Vector2 feederPoint = new Vector2(-2.2f, 1.4f);
    [Tooltip("Where the trough stands.")]
    public Vector2 troughPoint = new Vector2(2.2f, 1.4f);

    [Header("Ground")]
    [Tooltip("Portions of grass a square metre grows per game day in good weather (-1 = from its kind: a pasture grows 1.5, a coop or a barn floor none).")]
    public float grassGrowth = -1f;
    [Tooltip("Most portions of standing grass a square metre holds.")]
    [Min(0f)] public float grassCapacity = 3f;

    [Header("Capacity")]
    [Tooltip("Most animals it takes (0 = from its area and each species' space).")]
    [Min(0)] public int capacityOverride;

    [Header("Shelter and bedding")]
    [Tooltip("Roof and walls, 0-1: how well it keeps out rain and wind (-1 = from its kind: a pasture or a pond is open, a sty 0.7, a hutch 0.8, a coop 0.9, a barn or a stable 1).")]
    public float shelter = -1f;
    [Tooltip("°C its shelter adds in the cold, at full shelter (-1 = from its kind: a barn or a stable 5, a coop 4, a sty or a hutch 3, open ground 0).")]
    public float warmth = -1f;
    [Tooltip("Shade in the heat, 0-1 (-1 = from its kind: as much as its kind's roof gives).")]
    public float shade = -1f;
    [Tooltip("Whether straw is laid on its floor (From Kind: coops, barns, stables, sties and hutches are bedded; pastures and ponds are not).")]
    public PenBedding bedding = PenBedding.FromKind;
    [Tooltip("Game hours in which its ground clears itself of dung: rain and time wash open ground (0 = never; -1 = from its kind: a pasture or a pond does, at the rules' Pasture Clean Hours).")]
    public float selfCleanHours = -1f;

    [Header("Look")]
    [Tooltip("Show a simple fence along the outline, with its feeder and trough (no colliders: it shows the pen, it does not wall players in).")]
    public bool showFence = true;
    [Tooltip("Follow the ground below when placing the fence and the animals (raycast); off = flat at the pen's height.")]
    public bool followGround = true;
    [Tooltip("What counts as ground.")]
    public LayerMask groundLayers = ~0;

    [Header("Interaction")]
    public bool interactable = true;
    public int interactionPriority = 3;
    [Tooltip("Metres beyond the outline a player can stand and still tend the pen.")]
    [Min(0f)] public float extraReach = 0.8f;

    private Transform interactionPoint;
    private float radius = -1f;
    private AnimalPenView view;

    /// <summary>
    /// Asked before every piece of work on any pen and for its prompt (where the rules run): the pen, the player and the
    /// work - a refusal or null when they may. Null hook: rights decide alone. A game's own rule keeps the previous
    /// delegate and calls it first.
    /// </summary>
    public static Func<AnimalPen, CombatEntity, AnimalActionKind, string> AccessCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AccessCheck = null;

    /// <summary>Raised when the picture of the pen changed (the window and the bodies follow).</summary>
    public event Action Changed;
    /// <summary>A player's request was done (or refused); on a client: the server's answer arrived.</summary>
    public event Action<CombatEntity, AnimalResult> Answered;

    public string PenId
    {
        get
        {
            if (string.IsNullOrEmpty(penId)) penId = Application.isPlaying ? HousingIds.Stable(this, "pen") : NewId();
            return penId;
        }
    }

    public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

    public string DisplayName { get => string.IsNullOrWhiteSpace(displayName) ? "Pen" : displayName; set => displayName = value; }
    public AnimalRules Rules => AnimalDatabase.Instance.Rules;
    /// <summary>Where the rules run (offline, the server): the pen's animals are decided here.</summary>
    public static bool RulesHere => !GameNet.NetAuthority.IsNetworked || GameNet.NetAuthority.IsServer;

    /// <summary>The last picture of the pen this machine holds for its own player (null: none yet).</summary>
    public AnimalPenView View => view;

    /// <summary>The pen's area, square metres.</summary>
    public float Area => HousingGeometry.Area(outline);

    // ------------------------------------------------------------------ lifecycle
    private void Reset()
    {
        penId = HousingIds.Recall(this, "pen") ?? NewId();
        outline = HousingGeometry.Rectangle(Vector2.zero, new Vector2(6f, 4f));
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(penId)) penId = NewId();
        HousingIds.Remember(this, "pen", penId);
        radius = -1f;
    }

    private void Awake()
    {
        if (Application.isPlaying) AnimalDirector.Exists(this);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying) AnimalDirector.Gone(this);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        AnimalDirector.Exists(this);
        EnsureTrigger();
        AnimalDirector.Register(this);
        if (showFence) AnimalPenLook.Build(this);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        AnimalDirector.Unregister(this);
        AnimalBodies.Clear(this);
    }

    /// <summary>What the rules know of the pen from the scene.</summary>
    public AnimalPenSpec Spec()
    {
        float growth = grassGrowth >= 0f ? grassGrowth : kind == AnimalHabitatKind.Pasture ? 1.5f : 0f;
        return new AnimalPenSpec
        {
            id = PenId,
            name = DisplayName,
            kind = kind,
            area = Area,
            feederCapacity = feederCapacity,
            waterCapacity = waterCapacity,
            troughSurface = troughSurface,
            refillLitresPerHour = refillLitresPerHour,
            capacityOverride = capacityOverride,
            grassGrowth = growth,
            grassCapacity = grassCapacity,
            shelter = shelter,
            warmth = warmth,
            shade = shade,
            bedding = BeddingOf(bedding),
            selfCleanHours = selfCleanHours,
        };
    }

    /// <summary>The rules' bedding setting of a pen (-1 from its kind, 0 none, 1 bedded).</summary>
    public static int BeddingOf(PenBedding b) => b == PenBedding.Bedded ? 1 : b == PenBedding.None ? 0 : -1;

    // ------------------------------------------------------------------ geometry
    public Vector2 Local(Vector3 world) => HousingGeometry.ToLocal(world, transform.position, transform.rotation);

    public Vector3 World(Vector2 local, float lift = 0f) => transform.position + transform.rotation * new Vector3(local.x, lift, local.y);

    public bool Contains(Vector3 world) => outline != null && outline.Count >= 3 && HousingGeometry.Contains(outline, Local(world));

    /// <summary>Metres from a world point to the pen (0 inside it).</summary>
    public float DistanceFromOutline(Vector3 world) => outline != null && outline.Count >= 3 ? HousingGeometry.Distance(outline, Local(world)) : float.MaxValue;

    /// <summary>The middle of the pen's box in the world, on its plane.</summary>
    public Vector3 CentreWorld
    {
        get
        {
            Rect r = outline != null && outline.Count > 0 ? HousingGeometry.Bounds(outline) : default;
            return World(r.center);
        }
    }

    /// <summary>Half the size of the pen's box, metres.</summary>
    public float Radius
    {
        get
        {
            if (radius < 0f)
            {
                Rect b = outline != null && outline.Count > 0 ? HousingGeometry.Bounds(outline) : default;
                radius = Mathf.Max(b.width, b.height) * 0.5f + 0.5f;
            }
            return radius;
        }
    }

    private static readonly RaycastHit[] groundHits = new RaycastHit[8];

    /// <summary>
    /// The ground height below a local point (with Follow Ground), else the pen's height. It asks the pen's own scene's
    /// physics (scenes may have their own) and looks past players, mobs and animals standing there, and past a roof or a
    /// loft over the pen: the ground is the nearest surface no more than a metre above the pen's plane, and only where
    /// there is none (a pen set into a slope) the nearest surface at all.
    /// </summary>
    public float GroundY(Vector2 local)
    {
        Vector3 p = World(local);
        if (!followGround) return p.y;
        PhysicsScene physics = gameObject.scene.IsValid() ? gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
        int n = physics.Raycast(p + Vector3.up * 4f, Vector3.down, groundHits, 12f, groundLayers, QueryTriggerInteraction.Ignore);
        // Straight down, the nearest hit is the highest: the nearest of all, and the nearest a step or less above the plane.
        float best = float.MaxValue, y = p.y;
        float bestLow = float.MaxValue, yLow = p.y;
        bool low = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = groundHits[i].collider;
            if (c == null || c is CharacterController || c.GetComponentInParent<CombatEntity>() != null || c.GetComponentInParent<AnimalBody>() != null) continue;
            float d = groundHits[i].distance, at = groundHits[i].point.y;
            if (d < best)
            {
                best = d;
                y = at;
            }
            if (at <= p.y + 1f && d < bestLow)
            {
                bestLow = d;
                yLow = at;
                low = true;
            }
        }
        return low ? yLow : y;
    }

    /// <summary>The property the pen belongs to (null: public land).</summary>
    public Property Property => AnimalHousing.PropertyOf(this);

    // ------------------------------------------------------------------ work
    /// <summary>
    /// A player asks to do something. Where the rules run it is done now (the items are taken) and the result returned;
    /// on a network client the request goes to the server and null is returned - the answer comes through <see cref="Answered"/>.
    /// </summary>
    public AnimalResult Perform(AnimalRequest request, CombatEntity player)
    {
        if (player == null) return AnimalResult.Fail("Nobody to do it.");
        request.pen = PenId;
        if (!RulesHere)
        {
            if (!AnimalNet.SendRequest(request)) return AnimalResult.Fail("The animals are not set up for multiplayer (the Network World needs a NetAnimals).");
            return null;
        }
        return PerformHere(request, player);
    }

    /// <summary>Does a request where the rules run (offline, or the server for any player).</summary>
    public AnimalResult PerformHere(AnimalRequest request, CombatEntity player)
    {
        AnimalResult result;
        string denied;
        if (player == null) result = AnimalResult.Fail("Nobody to do it.");
        else if (!interactable) result = AnimalResult.Fail("This pen cannot be used.");
        else if (DistanceFromOutline(player.transform.position) > Rules.workRange + extraReach + 0.75f) result = AnimalResult.Fail("That is out of reach.");
        else if ((denied = Denied(player, request.kind)) != null) result = AnimalResult.Fail(denied);
        else result = AnimalDirector.Perform(this, request, player);
        result ??= AnimalResult.Fail("That cannot be done.");
        result.seq = request.seq;
        Answered?.Invoke(player, result);
        if (!CardTable.IsLocalPlayer(player)) AnimalNet.SendResult(player, PenId, result);
        return result;
    }

    /// <summary>The access hook's refusal for this player and work, or null.</summary>
    public string Denied(CombatEntity player, AnimalActionKind kind)
    {
        Func<AnimalPen, CombatEntity, AnimalActionKind, string> check = AccessCheck;
        if (check == null) return null;
        try
        {
            return check(this, player, kind);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return "You cannot tend this pen now.";
        }
    }

    /// <summary>A client: the server's answer arrived.</summary>
    public void ReceiveAnswer(CombatEntity player, AnimalResult result) => Answered?.Invoke(player, result);

    /// <summary>The picture of the pen for this machine's player arrived (or was made here).</summary>
    public void ReceiveView(AnimalPenView v)
    {
        if (v == null || v.id != PenId) return;
        if (view != null && v.now < view.now - 1e-6) return; // an older picture than the one held
        view = v;
        Changed?.Invoke();
    }

    /// <summary>Forget the picture (a client walked away, or the session ended).</summary>
    public void ForgetView()
    {
        view = null;
        AnimalBodies.Clear(this);
        Changed?.Invoke();
    }

    public void Tell(CombatEntity player, string text)
    {
        if (player == null || string.IsNullOrEmpty(text)) return;
        if (CardTable.IsLocalPlayer(player)) InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info);
        else AnimalNet.Tell(player, text);
    }

    // ------------------------------------------------------------------ interaction
    private void EnsureTrigger()
    {
        if (!interactable || transform.Find("Animal Pen Trigger") != null) return;
        Rect r = outline != null && outline.Count > 0 ? HousingGeometry.Bounds(outline) : new Rect(-1f, -1f, 2f, 2f);
        var go = new GameObject("Animal Pen Trigger") { layer = 2 }; // Ignore Raycast: only the interaction prompt sees it
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(r.center.x, 0.6f, r.center.y);
        box.size = new Vector3(Mathf.Max(0.5f, r.width), 1.6f, Mathf.Max(0.5f, r.height));
        var point = new GameObject("Interaction Point");
        point.transform.SetParent(transform, false);
        point.transform.localPosition = new Vector3(r.center.x, 1f, r.center.y);
        interactionPoint = point.transform;
    }

    public string InteractionName => DisplayName;
    public string InteractionVerb => "Tend";
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public float InteractionRange => Radius + Rules.workRange + extraReach;
    public int InteractionPriority => interactionPriority;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (!interactable) return false;
        CombatEntity player = PlayerOf(interactor);
        if (player == null) return false;
        if (DistanceFromOutline(player.transform.position) > Rules.workRange + extraReach)
        {
            reason = "Too far from the pen";
            return false;
        }
        if (RulesHere)
        {
            string denied = Denied(player, AnimalActionKind.Inspect);
            if (denied != null)
            {
                reason = denied;
                return false;
            }
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        CombatEntity player = PlayerOf(interactor);
        if (player == null) return;
        if (CardTable.IsLocalPlayer(player))
        {
            AnimalPenWindow.Show(this, player);
            return;
        }
        // The server, for a remote player: their window opens on their screen with the pen's picture.
        if (RulesHere) AnimalNet.Open(player, this);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static CombatEntity PlayerOf(PlayerInteractor interactor) =>
        interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;

    private void OnDrawGizmos()
    {
        if (outline == null || outline.Count < 2) return;
        Gizmos.color = new Color(0.85f, 0.7f, 0.35f, 0.9f);
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            Gizmos.DrawLine(World(outline[j], 0.05f), World(outline[i], 0.05f));
        Gizmos.color = new Color(0.9f, 0.8f, 0.4f, 1f);
        Gizmos.DrawWireCube(World(feederPoint, 0.3f), new Vector3(0.8f, 0.6f, 0.5f));
        Gizmos.color = new Color(0.4f, 0.65f, 0.95f, 1f);
        Gizmos.DrawWireCube(World(troughPoint, 0.25f), new Vector3(1f, 0.5f, 0.5f));
    }

    /// <summary>What is wrong with the pen itself (its outline, stations, kind): the editor and the validator list it.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (outline == null || outline.Count < 3) errors.Add("Its outline has fewer than 3 points.");
        else
        {
            if (!HousingGeometry.IsSimple(outline)) errors.Add("Its outline crosses itself.");
            if (Area < 1f) errors.Add($"It is too small ({Area:0.##} m²; at least 1 m²).");
            if (!HousingGeometry.Contains(outline, feederPoint)) warnings.Add("Its feeder stands outside it.");
            if (!HousingGeometry.Contains(outline, troughPoint)) warnings.Add("Its trough stands outside it.");
        }
        int bits = 0;
        foreach (AnimalHabitatKind k in Enum.GetValues(typeof(AnimalHabitatKind)))
            if (k != AnimalHabitatKind.None && (kind & k) != 0) bits++;
        if (bits != 1) errors.Add("Its Kind must be exactly one kind of habitat.");
        if (feederCapacity <= 0f) warnings.Add("It has no feeder (Feeder Capacity 0): only grass can feed its animals.");
        if (waterCapacity <= 0f) warnings.Add("It has no trough (Water Capacity 0): its animals will die of thirst.");
        if (shelter > 1f) warnings.Add($"Its Shelter is {shelter:0.##}: it counts as 1 (shelter is 0-1; -1 = from its kind).");
        else if (shelter < 0f && shelter > -0.999f) warnings.Add($"Its Shelter is {shelter:0.##}: below 0 it is taken from its kind (use -1 for that, or 0-1).");
        if (shade > 1f) warnings.Add($"Its Shade is {shade:0.##}: it counts as 1 (shade is 0-1; -1 = from its kind).");
        else if (shade < 0f && shade > -0.999f) warnings.Add($"Its Shade is {shade:0.##}: below 0 it is taken from its kind (use -1 for that, or 0-1).");
        if (Mathf.Abs(transform.lossyScale.x - 1f) > 0.001f || Mathf.Abs(transform.lossyScale.z - 1f) > 0.001f)
            warnings.Add("It is scaled: its outline is in metres and ignores the scale.");
        bool any = false;
        foreach (AnimalSpeciesData s in AnimalDatabase.Instance.AllSpecies)
            any |= s.habitat != null && s.habitat.Accepts(kind);
        if (!any) warnings.Add($"No species lives in a {kind.ToString().ToLowerInvariant()}.");
    }
}
