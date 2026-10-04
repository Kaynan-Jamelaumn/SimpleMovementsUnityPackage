using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>How the <see cref="PlayerInteractor"/> looks for what to interact with. Several can be combined.</summary>
[Flags]
public enum InteractionTargeting
{
    None = 0,
    /// <summary>A ray from the camera through the mouse pointer (the centre of the screen while the cursor is locked).</summary>
    PointerRay = 1,
    /// <summary>A ray from the centre of the screen (a crosshair), whatever the mouse does.</summary>
    ScreenCenterRay = 2,
    /// <summary>The best target around the player (gamepad-friendly): within range, in front of the player, in sight.</summary>
    Proximity = 4,
}

/// <summary>Where the interaction prompt is drawn.</summary>
public enum InteractionPromptPlacement
{
    /// <summary>Above the target, following it on screen.</summary>
    AboveTarget,
    /// <summary>At a fixed place near the bottom of the screen.</summary>
    ScreenBottom,
}

/// <summary>
/// The player's side of interactions: finds the <see cref="IInteractable"/> to use (under the mouse, at the centre of the
/// screen, or the best one nearby), shows a prompt, and interacts with the Interact input or a click on the target.
/// <list type="bullet">
/// <item><b>Input</b> (resolved like the other gameplay inputs, see <see cref="CombatInputBinding"/>): an assigned Input
/// Action, else an action named Interact / Interaction / Talk in the player's input actions, else one is created with
/// E (keyboard) and the north face button (gamepad), rebindable in Key Bindings. Clicking a target uses the left mouse
/// button (action InteractClick); while the pointer is over a target the click does not attack.</item>
/// <item><b>Several targets</b>: the one under the pointer wins, then the one at the centre of the screen, then the best
/// nearby target (highest priority, then nearest and most in front, with a little stickiness so the focus does not flicker).
/// Only targets in their own range, in sight and accepting the interaction (<see cref="IInteractable.CanInteract"/>) are used;
/// a target aimed at but out of reach shows why ("Too far").</item>
/// <item>While an interaction is open (a shop, a dialogue) targeting pauses and the Interact input closes it.</item>
/// </list>
/// Put it on the player (added by Tools ▸ SimpleMovements ▸ NPC ▸ Add Player Interactor To Players). The older
/// <c>Player.OnInteract</c> keeps working for doors, items and chests; turn on Handle Legacy Interactables to use this
/// component for them too (and then unwire Player.OnInteract so a key is not handled twice).
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("How targets are found. Pointer Ray: under the mouse (the screen centre while the cursor is locked). Screen Center Ray: " +
             "at a crosshair. Proximity: the best target around the player - works with a gamepad and without aiming.")]
    [SerializeField] private InteractionTargeting targeting = InteractionTargeting.PointerRay | InteractionTargeting.Proximity;
    [Tooltip("Camera of the rays (empty = the main camera).")]
    [SerializeField] private Camera cam;
    [Tooltip("Length of the camera rays (metres). A target hit by a ray must still be within its own Interaction Range of the player.")]
    [SerializeField, Min(0.5f)] private float rayDistance = 12f;
    [Tooltip("Radius searched around the player for Proximity targeting (metres).")]
    [SerializeField, Min(0.25f)] private float proximityRadius = 3f;
    [Tooltip("Proximity only takes targets within this angle in front of the player (degrees, 360 = all around). " +
             "'In front' follows the camera when there is one.")]
    [SerializeField, Range(10f, 360f)] private float proximityAngle = 160f;
    [Tooltip("Layers searched for targets (their colliders).")]
    [SerializeField] private LayerMask interactableLayers = ~0;
    [Tooltip("Whether trigger colliders count (an NPC's interaction volume can be a trigger).")]
    [SerializeField] private QueryTriggerInteraction triggerColliders = QueryTriggerInteraction.Collide;
    [Tooltip("Ignore targets behind walls (a ray from the player's eyes to the target).")]
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Height of the player's eyes for the line-of-sight check (metres above the player's position).")]
    [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
    [Tooltip("A target already in focus is kept unless another one is better by this much (metres) - stops flicker between two NPCs.")]
    [SerializeField, Min(0f)] private float focusStickiness = 0.5f;
    [Tooltip("Also use doors, dungeon objects, items on the ground and storage (Interactable / ItemPickable / Storage) like Player.OnInteract " +
             "does. Unwire Player.OnInteract when turning this on, or the key is handled twice.")]
    [SerializeField] private bool handleLegacyInteractables;
    [Tooltip("Reach of those older objects (Player.OnInteract uses 3.2 m).")]
    [SerializeField, Min(0.5f)] private float legacyRange = 3.2f;

    [Header("Input")]
    [Tooltip("The Interact action (optional). Empty = an action named below, found in (or added to) the player's input actions.")]
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("Actions looked up by name for Interact (the first found is used).")]
    [SerializeField] private string[] interactActionNames = { "Interact", "Interaction", "Talk" };
    [Tooltip("Controls of the Interact action created when none of the names exist (any device). Controls another action already uses are skipped.")]
    [SerializeField] private string[] interactDefaultControls = { "<Keyboard>/e", "<Gamepad>/buttonNorth" };
    [Tooltip("Clicking a target interacts with it (targets whose Allows Click is on, under the pointer, within range).")]
    [SerializeField] private bool clickToInteract = true;
    [Tooltip("The click action (optional). Empty = an action named below, else one created on the left mouse button.")]
    [SerializeField] private InputActionReference clickAction;
    [Tooltip("Actions looked up by name for clicking a target.")]
    [SerializeField] private string[] clickActionNames = { "InteractClick" };
    [Tooltip("Controls of the click action created when none of the names exist.")]
    [SerializeField] private string[] clickDefaultControls = { "<Mouse>/leftButton" };
    [Tooltip("While the pointer is over a target that a click would use, the click does not also attack or use the item in hand.")]
    [SerializeField] private bool clickBlocksAttack = true;
    [Tooltip("While an interaction is open (shop, dialogue), the Interact input closes it.")]
    [SerializeField] private bool interactKeyCloses = true;

    [Header("Prompt")]
    [Tooltip("Show what can be interacted with (\"[E] Trade  Bram the Smith\").")]
    [SerializeField] private bool showPrompt = true;
    [Tooltip("Your own prompt prefab (needs an InteractionPromptUI). Empty = a default prompt built at runtime.")]
    [SerializeField] private InteractionPromptUI promptPrefab;
    [Tooltip("Where the prompt goes.")]
    [SerializeField] private InteractionPromptPlacement promptPlacement = InteractionPromptPlacement.AboveTarget;
    [Tooltip("Distance above the target's top (its collider) where the prompt is drawn (metres).")]
    [SerializeField] private float promptHeightOffset = 0.35f;
    [Tooltip("Show targets that are aimed at but cannot be used, with the reason (\"Too far\", \"Busy\").")]
    [SerializeField] private bool showUnavailable = true;

    private static readonly List<PlayerInteractor> all = new List<PlayerInteractor>();
    private const float VerticalTolerance = 2.5f;

    private Transform root;
    private CombatEntity entity;
    private InventoryManager inventory;
    private CombatInputBinding interactInput;
    private CombatInputBinding clickInput;
    private bool inputBound;
    private readonly RaycastHit[] rayHits = new RaycastHit[24];
    private readonly Collider[] overlap = new Collider[48];
    private readonly HashSet<IInteractable> seen = new HashSet<IInteractable>(ReferenceComparer<IInteractable>.Instance);
    private readonly Dictionary<Component, LegacyInteractableTarget> legacyTargets = new Dictionary<Component, LegacyInteractableTarget>(ReferenceComparer<Component>.Instance);

    private IInteractable focused;
    private bool focusedAvailable;
    private string focusedReason = "";
    private IInteractable pointerTarget;
    private IInteractable holding;
    private bool holdFromClick;
    private float holdTime;
    private IInteractable current;
    private Action closeCurrent;
    private int currentSinceFrame = -1;
    private int lastEndedFrame = -1;
    private InteractionPromptUI prompt;
    private bool promptCreated;

    /// <summary>Raised when the target in focus changes (null = none).</summary>
    public event Action<IInteractable> FocusChanged;
    /// <summary>Raised after interacting with a target.</summary>
    public event Action<IInteractable> Interacted;

    public static IReadOnlyList<PlayerInteractor> All => all;
    /// <summary>The target that would be used now (may be unavailable, see <see cref="FocusedAvailable"/>).</summary>
    public IInteractable Focused => focused;
    public bool FocusedAvailable => focused != null && focusedAvailable;
    /// <summary>The interaction that is open (a shop, a dialogue), or null.</summary>
    public IInteractable Current => current;
    public bool IsBusy => current != null;
    /// <summary>The player object (the root with the Player Status Controller).</summary>
    public Transform PlayerRoot => root != null ? root : (root = AbilitiesStateMachine.PlayerRoot(this));
    public GameObject PlayerObject => PlayerRoot.gameObject;
    public Camera Camera => cam != null ? cam : Camera.main;
    public InventoryManager Inventory => inventory != null ? inventory : (inventory = InventoryManager.For(this));
    public string InputSource => interactInput != null ? interactInput.Source : "not bound yet";
    public string ClickSource => clickInput != null ? clickInput.Source : clickToInteract ? "not bound yet" : "off";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    /// <summary>The interactor of the player owning <paramref name="anyOnPlayer"/>, or null.</summary>
    public static PlayerInteractor For(Component anyOnPlayer)
    {
        if (anyOnPlayer == null)
            return null;
        Transform r = AbilitiesStateMachine.PlayerRoot(anyOnPlayer);
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].PlayerRoot == r)
                return all[i];
        return r.GetComponentInChildren<PlayerInteractor>(true);
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        root = AbilitiesStateMachine.PlayerRoot(this);
        entity = CombatEntity.Resolve(root.gameObject);
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
    }

    private void Start() => BindInput();

    private void OnDisable()
    {
        all.Remove(this);
        PointerCapture.Release(this);
        CancelHold();
        SetFocus(null, false, "");
        if (prompt != null)
            prompt.Hide();
    }

    private void OnDestroy()
    {
        interactInput?.Dispose();
        clickInput?.Dispose();
        interactInput = clickInput = null;
        if (promptCreated && prompt != null)
            Destroy(prompt.gameObject);
    }

    private void BindInput()
    {
        if (inputBound)
            return;
        inputBound = true;
        interactInput = CombatInputBinding.Create(this, interactAction, interactActionNames, interactDefaultControls, "Interact");
        if (clickToInteract)
            clickInput = CombatInputBinding.Create(this, clickAction, clickActionNames, clickDefaultControls, "Interact (click)", fallbackMayShare: true);
        if (!interactInput.HasAny)
            Debug.LogWarning("[Interaction] No Interact input could be found or created: only clicks interact. Add an 'Interact' action to the input actions.", this);
    }

    private void Update()
    {
        if (!inputBound)
            BindInput();

        // An interaction is open: nothing is targeted; the Interact input closes it.
        if (current != null)
        {
            PointerCapture.Release(this);
            SetFocus(null, false, "");
            HidePrompt();
            if (interactKeyCloses && Time.frameCount != currentSinceFrame && interactInput != null && interactInput.Pressed)
                CloseCurrent();
            return;
        }

        if (!CanAct())
        {
            PointerCapture.Release(this);
            CancelHold();
            SetFocus(null, false, "");
            HidePrompt();
            return;
        }

        Detect();
        UpdatePointerClaim();
        HandleInput();
        UpdatePrompt();
    }

    /// <summary>Alive, gameplay input running, and no inventory / other window taking the mouse.</summary>
    private bool CanAct()
    {
        if (entity != null && !entity.IsAlive)
            return false;
        if (InputBindingStore.GameplayInputPaused)
            return false;
        InventoryManager inv = Inventory;
        return inv == null || !(inv.IsInventoryOpened || inv.IsExternalPanelOpen);
    }

    // ------------------------------------------------------------------ sessions (shops, dialogue)
    /// <summary>
    /// Called by a target that opened something (a shop, a dialogue). Until <see cref="EndInteraction"/>, targeting pauses
    /// and the Interact input (when Interact Key Closes is on) calls <paramref name="close"/>.
    /// </summary>
    public void BeginInteraction(IInteractable owner, Action close)
    {
        current = owner;
        closeCurrent = close;
        currentSinceFrame = Time.frameCount;
        CancelHold();
        SetFocus(null, false, "");
        HidePrompt();
        PointerCapture.Release(this);
    }

    /// <summary>The interaction opened by <paramref name="owner"/> is over.</summary>
    public void EndInteraction(IInteractable owner)
    {
        if (current == null || !ReferenceEquals(current, owner))
            return;
        current = null;
        closeCurrent = null;
        lastEndedFrame = Time.frameCount;
    }

    private void CloseCurrent()
    {
        Action close = closeCurrent;
        current = null;
        closeCurrent = null;
        lastEndedFrame = Time.frameCount;
        try { close?.Invoke(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    // ------------------------------------------------------------------ detection
    private IInteractable pointerHit, centerHit;

    private void Detect()
    {
        pointerHit = centerHit = null;
        pointerTarget = null;
        Camera c = Camera;
        if (c != null && (targeting & InteractionTargeting.PointerRay) != 0 && Mouse.current != null && !PointerOverUI())
            pointerHit = RayTarget(c.ScreenPointToRay(Mouse.current.position.ReadValue()));
        if (c != null && (targeting & InteractionTargeting.ScreenCenterRay) != 0)
            centerHit = RayTarget(c.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));

        string reason;
        if (pointerHit != null && IsUsable(pointerHit, out _) && pointerHit.AllowsClick)
            pointerTarget = pointerHit;

        // 1. aimed with the pointer, 2. at the crosshair, 3. the best one nearby.
        if (pointerHit != null && IsUsable(pointerHit, out reason))
        {
            SetFocus(pointerHit, true, "");
            return;
        }
        if (centerHit != null && IsUsable(centerHit, out reason))
        {
            SetFocus(centerHit, true, "");
            return;
        }
        IInteractable near = (targeting & InteractionTargeting.Proximity) != 0 ? BestNearby() : null;
        if (near != null)
        {
            SetFocus(near, true, "");
            return;
        }
        // Aimed at something that cannot be used: say why.
        IInteractable aimed = pointerHit ?? centerHit;
        if (aimed != null && showUnavailable)
        {
            IsUsable(aimed, out reason);
            SetFocus(aimed, false, string.IsNullOrEmpty(reason) ? "Unavailable" : reason);
            return;
        }
        SetFocus(null, false, "");
    }

    /// <summary>The first target along the ray (solid objects in front of it block it).</summary>
    private IInteractable RayTarget(Ray ray)
    {
        int n = Physics.RaycastNonAlloc(ray, rayHits, rayDistance, interactableLayers, triggerColliders);
        if (n <= 0)
            return null;
        Array.Sort(rayHits, 0, n, HitDistanceComparer.Instance);
        for (int i = 0; i < n; i++)
        {
            Collider col = rayHits[i].collider;
            if (col == null || IsOwn(col))
                continue;
            IInteractable t = Resolve(col);
            if (t != null)
                return t;
            if (!col.isTrigger)
                return null; // a wall in front
        }
        return null;
    }

    private IInteractable BestNearby()
    {
        Vector3 origin = PlayerRoot.position;
        int n = Physics.OverlapSphereNonAlloc(origin, proximityRadius, overlap, interactableLayers, triggerColliders);
        Vector3 forward = Forward();
        IInteractable best = null;
        float bestScore = float.NegativeInfinity;
        seen.Clear();
        for (int i = 0; i < n; i++)
        {
            Collider col = overlap[i];
            if (col == null || IsOwn(col))
                continue;
            IInteractable t = Resolve(col);
            if (t == null || !seen.Add(t))
                continue;
            Transform p = t.InteractionPoint;
            if (p == null)
                continue;
            Vector3 to = p.position - origin;
            to.y = 0f;
            float distance = to.magnitude;
            float angle = distance > 0.05f ? Vector3.Angle(forward, to) : 0f;
            if (proximityAngle < 360f && angle > proximityAngle * 0.5f)
                continue;
            if (!IsUsable(t, out _))
                continue;
            float score = t.InteractionPriority * 100f - distance - angle / 180f * 1.5f + (ReferenceEquals(t, focused) ? focusStickiness : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }
        return best;
    }

    /// <summary>In range, in sight and accepting the interaction (<paramref name="reason"/> says why not).</summary>
    private bool IsUsable(IInteractable t, out string reason)
    {
        reason = "";
        if (t == null || (t is UnityEngine.Object o && o == null))
            return false;
        if (!IsInRange(t))
        {
            reason = "Too far";
            return false;
        }
        if (!t.CanInteract(this, out reason))
        {
            if (string.IsNullOrEmpty(reason)) reason = "Unavailable";
            return false;
        }
        if (requireLineOfSight && !HasLineOfSight(t))
        {
            reason = "Not in sight";
            return false;
        }
        reason = "";
        return true;
    }

    /// <summary>Within the target's Interaction Range on the ground plane (and not far above or below).</summary>
    public bool IsInRange(IInteractable t)
    {
        Transform p = t?.InteractionPoint;
        if (p == null)
            return false;
        Vector3 d = p.position - PlayerRoot.position;
        float vertical = Mathf.Abs(d.y);
        d.y = 0f;
        float range = Mathf.Max(0.1f, t.InteractionRange);
        return d.sqrMagnitude <= range * range && vertical <= range + VerticalTolerance;
    }

    private bool HasLineOfSight(IInteractable t)
    {
        Transform target = TargetTransform(t);
        Transform p = t.InteractionPoint;
        if (target == null || p == null)
            return false;
        Vector3 eye = PlayerRoot.position + Vector3.up * eyeHeight;
        Vector3 aim = TopOf(t, p.position) - Vector3.up * 0.4f;
        Vector3 dir = aim - eye;
        float dist = dir.magnitude;
        if (dist < 0.05f)
            return true;
        int n = Physics.RaycastNonAlloc(eye, dir / dist, rayHits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = rayHits[i].collider;
            if (col == null || IsOwn(col) || col.transform.IsChildOf(target))
                continue;
            return false;
        }
        return true;
    }

    private Vector3 Forward()
    {
        Camera c = Camera;
        Vector3 f = c != null ? c.transform.forward : PlayerRoot.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f)
            f = PlayerRoot.forward;
        return f.normalized;
    }

    private bool IsOwn(Collider col) => col.transform.IsChildOf(PlayerRoot);

    /// <summary>The interactable behind a collider (on it or a parent), or null.</summary>
    private IInteractable Resolve(Collider col)
    {
        IInteractable t = col.GetComponentInParent<IInteractable>();
        if (t != null)
            return t is Behaviour b && !b.isActiveAndEnabled ? null : t;
        if (!handleLegacyInteractables)
            return null;
        Component legacy = LegacyInteractableTarget.FindOwner(col);
        if (legacy == null || (legacy is Behaviour lb && !lb.isActiveAndEnabled))
            return null;
        if (!legacyTargets.TryGetValue(legacy, out LegacyInteractableTarget target))
        {
            if (legacyTargets.Count > 256)
                legacyTargets.Clear(); // old pickups that no longer exist
            target = new LegacyInteractableTarget(legacy, legacyRange);
            legacyTargets[legacy] = target;
        }
        return target;
    }

    private static Transform TargetTransform(IInteractable t)
    {
        if (t is Component c)
            return c != null ? c.transform : null;
        if (t is LegacyInteractableTarget l)
            return l.Owner != null ? l.Owner.transform : null;
        return t?.InteractionPoint;
    }

    /// <summary>The top of the target's collider (where the prompt goes), else its point.</summary>
    private static Vector3 TopOf(IInteractable t, Vector3 fallback)
    {
        Transform tr = TargetTransform(t);
        Collider col = tr != null ? tr.GetComponentInChildren<Collider>() : null;
        if (col == null || !col.enabled)
            return fallback + Vector3.up * 1.8f;
        Bounds b = col.bounds;
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    private static bool PointerOverUI()
    {
        // The cursor is locked during gameplay: the pointer is then the screen centre and never over a menu.
        if (Cursor.lockState == CursorLockMode.Locked)
            return false;
        EventSystem es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private void SetFocus(IInteractable target, bool available, string reason)
    {
        focusedAvailable = available;
        focusedReason = reason ?? "";
        if (ReferenceEquals(target, focused))
            return;
        if (holding != null && !ReferenceEquals(holding, target))
            CancelHold();
        IInteractable old = focused;
        focused = target;
        if (old != null && !(old is UnityEngine.Object oo && oo == null))
        {
            try { old.OnFocusChanged(this, false); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
        if (focused != null)
        {
            try { focused.OnFocusChanged(this, true); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
        FocusChanged?.Invoke(focused);
    }

    // ------------------------------------------------------------------ input
    private void UpdatePointerClaim()
    {
        if (clickToInteract && clickBlocksAttack && pointerTarget != null && clickInput != null && clickInput.HasAny)
            PointerCapture.Claim(this, this);
        else
            PointerCapture.Release(this);
    }

    private void HandleInput()
    {
        if (holding != null)
        {
            bool held = holdFromClick ? clickInput != null && clickInput.Held : interactInput != null && interactInput.Held;
            IInteractable expected = holdFromClick ? pointerTarget : (focusedAvailable ? focused : null);
            if (!held || !ReferenceEquals(holding, expected))
            {
                CancelHold();
                return;
            }
            holdTime += Time.deltaTime;
            if (holdTime >= holding.HoldDuration)
            {
                IInteractable t = holding;
                CancelHold();
                DoInteract(t);
            }
            return;
        }
        if (Time.frameCount == lastEndedFrame)
            return; // the press that just closed an interaction does not open another

        if (interactInput != null && interactInput.Pressed && focused != null && focusedAvailable)
            Begin(focused, false);
        else if (clickToInteract && clickInput != null && clickInput.Pressed && pointerTarget != null)
            Begin(pointerTarget, true);
    }

    private void Begin(IInteractable t, bool fromClick)
    {
        if (t.HoldDuration > 0f)
        {
            holding = t;
            holdFromClick = fromClick;
            holdTime = 0f;
            return;
        }
        DoInteract(t);
    }

    private void CancelHold()
    {
        holding = null;
        holdTime = 0f;
    }

    /// <summary>Interacts with <paramref name="target"/> now (range and availability are checked again). True if it happened.</summary>
    public bool DoInteract(IInteractable target)
    {
        if (!IsUsable(target, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
                Inventory?.ShowMessage($"{target?.InteractionName}: {reason}", InventoryFeedback.Kind.Info);
            return false;
        }
        try { target.Interact(this); }
        catch (Exception e)
        {
            Debug.LogException(e, target as UnityEngine.Object);
            return false;
        }
        Interacted?.Invoke(target);
        return true;
    }

    // ------------------------------------------------------------------ prompt
    private void UpdatePrompt()
    {
        if (!showPrompt || focused == null || (!focusedAvailable && !showUnavailable))
        {
            HidePrompt();
            return;
        }
        InteractionPromptUI p = EnsurePrompt();
        if (p == null)
            return;
        float hold = holding != null && ReferenceEquals(holding, focused) && holding.HoldDuration > 0f ? holdTime / holding.HoldDuration : 0f;
        p.Show(KeyLabel(), focused.InteractionVerb, focused.InteractionName, focusedAvailable ? "" : focusedReason, hold);
        Vector2? screen = null;
        Camera c = Camera;
        if (promptPlacement == InteractionPromptPlacement.AboveTarget && c != null && focused.InteractionPoint != null)
        {
            Vector3 world = TopOf(focused, focused.InteractionPoint.position) + Vector3.up * promptHeightOffset;
            Vector3 sp = c.WorldToScreenPoint(world);
            if (sp.z > 0f)
                screen = new Vector2(sp.x, sp.y);
        }
        p.Place(screen);
    }

    /// <summary>"E" / "Y" for the device in use; "Click" when only clicking works.</summary>
    private string KeyLabel()
    {
        if (interactInput != null && interactInput.ReadAction != null)
        {
            string s = InputDeviceTracker.DisplayString(interactInput.ReadAction);
            if (!string.IsNullOrEmpty(s))
                return s;
        }
        if (interactDefaultControls != null)
            foreach (string path in interactDefaultControls)
                if (InputDeviceTracker.IsFor(path, InputDeviceTracker.Current))
                    return InputActionResolver.Describe(new[] { path });
        return focused != null && focused.AllowsClick && clickToInteract ? "Click" : "";
    }

    private InteractionPromptUI EnsurePrompt()
    {
        if (prompt != null)
            return prompt;
        Canvas canvas = GameplayUIRoot.CanvasFor(this);
        if (canvas == null)
            return null;
        prompt = promptPrefab != null ? Instantiate(promptPrefab, canvas.transform, false) : InteractionPromptUI.Create(canvas.transform);
        prompt.name = "InteractionPrompt";
        promptCreated = true;
        return prompt;
    }

    private void HidePrompt()
    {
        if (prompt != null)
            prompt.Hide();
    }

    private sealed class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private void OnDrawGizmosSelected()
    {
        Transform r = Application.isPlaying ? PlayerRoot : AbilitiesStateMachine.PlayerRoot(this);
        if ((targeting & InteractionTargeting.Proximity) != 0)
        {
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.5f);
            Gizmos.DrawWireSphere(r.position, proximityRadius);
        }
        if (Application.isPlaying && focused?.InteractionPoint != null)
        {
            Gizmos.color = focusedAvailable ? Color.green : Color.red;
            Gizmos.DrawLine(r.position + Vector3.up * eyeHeight, focused.InteractionPoint.position + Vector3.up);
        }
    }
}
