using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's abilities. Each <see cref="AbilityStateMachine"/> on the player (one per input binding) registers a
/// slot here; pressing its key casts the slot's ability aimed with the camera crosshair (or the mouse when the cursor
/// is free), with optional auto-aim and click-to-confirm previews. Absorbed abilities arrive through
/// <see cref="IAbilityReceiver"/> and can be saved/restored with <see cref="GetAbsorbedAbilities"/> /
/// <see cref="RestoreAbsorbedAbilities"/>.
/// <para>While a click-to-confirm preview is open the mouse click belongs to it: item use and weapon attacks can ask
/// <see cref="IsPointerCapturedFor"/> and skip that click. An optional cancel input stops the wind-up of an ability.</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerAbilityController : AbilityCaster
{
    [Header("Player Aiming")]
    [Tooltip("Camera whose crosshair (screen centre, or the mouse when the cursor is free) aims the abilities. Empty = the camera tagged MainCamera.")]
    [SerializeField] private Camera aimCamera;

    [Tooltip("Layers the aim ray can hit (ground, walls, characters). Leave out the player's own layer and triggers so the ray does not stop on the player.")]
    [SerializeField] private LayerMask aimLayers = ~0;

    [Tooltip("Longest aim ray (metres). Beyond it the ability aims at the point this far along the crosshair.")]
    [SerializeField, Min(1f)] private float maxAimDistance = 150f;

    [Tooltip("Aim with the mouse position instead of the screen centre when the cursor is visible and unlocked.")]
    [SerializeField] private bool aimWithMouseWhenCursorFree = true;

    [Tooltip("Turn the character toward the aim when an ability starts.")]
    [SerializeField] private bool snapTurnToAim = true;

    [Header("Click-To-Confirm Preview")]
    [Tooltip("Colour of the area preview for abilities with 'Player Confirms Target'. Alpha 0 = Combat Settings' aim preview colour.")]
    [SerializeField] private Color previewColor = new Color(0f, 0f, 0f, 0f);

    [Header("Cancel")]
    [Tooltip("Optional input that cancels the ability being wound up (its Casting phase) and closes a click-to-confirm preview. A cancelled ability follows its own interrupt rules (cost refund, charge given back, cooldown on interrupt). Empty = no cancel key (right click / Escape still close previews).")]
    [SerializeField] private InputActionReference cancelCastInput;

    private readonly Dictionary<int, AbilityStateMachine> machineBySlot = new Dictionary<int, AbilityStateMachine>();
    private readonly List<AbilityTelegraph> previews = new List<AbilityTelegraph>(4);
    private readonly List<ResolvedShape> previewShapes = new List<ResolvedShape>(4);
    private readonly RaycastHit[] aimHits = new RaycastHit[16];
    private AbilityCastInstance previewCast;
    private int targetingSlot = -1;
    private float targetingStart;
    private int pointerClaimFrame = -1;

    private static readonly List<PlayerAbilityController> enabledControllers = new List<PlayerAbilityController>();

    /// <summary>Slot currently showing a click-to-confirm preview (-1 = none).</summary>
    public int TargetingSlot => targetingSlot;
    public bool IsTargeting => targetingSlot >= 0;

    /// <summary>A click-to-confirm preview started (slot index).</summary>
    public event Action<int> TargetingStarted;
    /// <summary>A click-to-confirm preview ended (slot index, confirmed).</summary>
    public event Action<int, bool> TargetingEnded;

    public Camera AimCamera => aimCamera != null ? aimCamera : Camera.main;

    /// <summary>
    /// True while a click-to-confirm preview is open, and during the frame a click confirmed or cancelled it: that
    /// mouse click belongs to the ability and should not also use an item or swing a weapon.
    /// </summary>
    public bool IsPointerCaptured => targetingSlot >= 0 || Time.frameCount == pointerClaimFrame;

    /// <summary>
    /// True if the player owning <paramref name="anyOnPlayer"/> is using the mouse click for an ability preview (see
    /// <see cref="IsPointerCaptured"/>). Null = any player. Item use and weapon attack code call this to skip the click.
    /// </summary>
    public static bool IsPointerCapturedFor(GameObject anyOnPlayer)
    {
        Transform root = anyOnPlayer != null ? anyOnPlayer.transform.root : null;
        for (int i = 0; i < enabledControllers.Count; i++)
        {
            PlayerAbilityController c = enabledControllers[i];
            if (c != null && c.IsPointerCaptured && (root == null || c.transform.root == root))
                return true;
        }
        return false;
    }

    /// <summary>The optional cancel input (null = none).</summary>
    public InputActionReference CancelCastInput { get => cancelCastInput; set { cancelCastInput = value; EnableCancelInput(); } }

    /// <summary>The camera assigned in the inspector (null = Camera.main at runtime).</summary>
    public Camera AssignedAimCamera => aimCamera;

    /// <summary>Fills empty references: the Animator (from the PlayerAnimationModel) and the aim camera (Camera.main).</summary>
    public void AutoAssignReferences()
    {
        if (animator == null)
        {
            PlayerAnimationModel model = GetComponentInChildren<PlayerAnimationModel>();
            if (model != null && model.Anim != null)
                animator = model.Anim;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
        if (aimCamera == null)
            aimCamera = Camera.main;
    }

    protected override void Awake()
    {
        base.Awake();
        if (animator == null)
        {
            PlayerAnimationModel model = GetComponentInChildren<PlayerAnimationModel>();
            if (model != null && model.Anim != null)
                animator = model.Anim;
        }
    }

    // ------------------------------------------------------------------ slots bound to input
    /// <summary>Called by each AbilityStateMachine: creates (once) the slot it drives and returns its index.</summary>
    public int RegisterStateMachine(AbilityStateMachine machine)
    {
        Initialize();
        foreach (KeyValuePair<int, AbilityStateMachine> kv in machineBySlot)
        {
            if (kv.Value == machine)
                return kv.Key;
        }
        PlayerAbilityHolder holder = machine.AbilityHolder;
        AbilityDefinition def = holder != null ? holder.ResolveAbility() : null;
        int index = AddSlot(def, holder != null ? holder.modifiers : null);
        machineBySlot[index] = machine;
        return index;
    }

    /// <summary>The ability state machine (input binding) that drives slot <paramref name="index"/>, or null.</summary>
    public AbilityStateMachine GetStateMachine(int index)
    {
        machineBySlot.TryGetValue(index, out AbilityStateMachine m);
        return m;
    }

    /// <summary>
    /// A slot for an ability without a key of its own (e.g. a trait's ability cast with its own input). Reuses an empty
    /// extra slot when there is one, so adding and removing such abilities never grows the slot list. Returns its index.
    /// </summary>
    public int AcquireExtraSlot(AbilityDefinition ability, AbilityModifierSet modifiers = null)
    {
        Initialize();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty && !machineBySlot.ContainsKey(i))
            {
                SetSlot(i, ability, modifiers);
                return i;
            }
        }
        return AddSlot(ability, modifiers);
    }

    /// <summary>Empties an extra slot from <see cref="AcquireExtraSlot"/> so it can be reused (key slots are left alone).</summary>
    public void ReleaseExtraSlot(int index)
    {
        if (index >= 0 && index < slots.Count && !machineBySlot.ContainsKey(index))
            ClearSlot(index);
    }

    protected override bool CanAddSlots => false;

    protected override bool CanUseSlotForGrant(int index) => machineBySlot.ContainsKey(index);

    // ------------------------------------------------------------------ input
    /// <summary>
    /// Casts slot <paramref name="slotIndex"/> from player input: aims with the camera, or starts/confirms the
    /// click-to-confirm preview. Returns true if something started.
    /// </summary>
    public bool TryCastFromInput(int slotIndex)
    {
        AbilitySlot s = GetSlot(slotIndex);
        if (s == null || s.ability == null)
            return false;
        if (targetingSlot == slotIndex)
            return ConfirmTargeting();
        if (s.ability.targeting.playerConfirmsTarget)
        {
            // Open the preview only when the ability could start now. Aim problems (no target, out of range, no line
            // of sight) can still change while aiming and are checked on confirm; anything else (cooldown, not enough
            // mana, silenced, another ability busy) is reported right away instead of after the click.
            CastFailReason ready = CheckSlotReady(slotIndex);
            if (ready != CastFailReason.None)
            {
                RaiseCastFailed(slotIndex, ready);
                return false;
            }
            BeginTargeting(slotIndex);
            return true;
        }
        return TryCast(slotIndex, BuildAimRequest(s.ability, s.Stats));
    }

    /// <summary>Builds a cast request from the crosshair: the point it is on, the character under it or auto-aim.</summary>
    public CastRequest BuildAimRequest(AbilityDefinition def, in AbilityStats stats)
    {
        Camera cam = AimCamera;
        Vector3 origin = Entity != null ? Entity.Position : transform.position;
        if (cam == null)
        {
            Vector3 f = CombatQuery.FlatDirection(Vector3.zero, transform.forward, Vector3.forward);
            return CastRequest.InDirection(f);
        }

        Ray ray = AimRay(cam);
        Vector3 point = ray.origin + ray.direction * maxAimDistance;
        CombatEntity hitEntity = null;
        int n = Physics.RaycastNonAlloc(ray, aimHits, maxAimDistance, aimLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = aimHits[i];
            if (h.collider == null || h.distance >= best || h.collider.transform.IsChildOf(transform))
                continue;
            CombatEntity e = CombatEntity.Resolve(h.collider);
            if (e != null && e == Entity)
                continue;
            best = h.distance;
            point = h.point;
            hitEntity = e;
        }

        CombatEntity target = null;
        if (hitEntity != null && hitEntity.IsAlive && CombatRelations.Passes(def.targeting.targetFilter, Entity, hitEntity))
            target = hitEntity;
        if (target == null && def.targeting.autoAimAngle > 0f)
        {
            float camToPlayer = Vector3.Distance(cam.transform.position, origin);
            target = CombatQuery.BestInCone(cam.transform.position, ray.direction, def.targeting.autoAimAngle,
                def.Range(stats) + camToPlayer + 1f, Entity, def.targeting.targetFilter);
            if (target != null && CombatQuery.FlatDistance(origin, target.Position) > def.Range(stats) + target.Radius + 1f && def.targeting.mode != AbilityTargetingMode.Direction)
                target = null;
        }

        Vector3 aimAt = target != null && def.targeting.mode != AbilityTargetingMode.Point ? target.Position : point;
        Vector3 dir = CombatQuery.FlatDirection(origin, aimAt, ray.direction);
        return new CastRequest
        {
            target = target,
            point = target != null && def.targeting.mode == AbilityTargetingMode.Unit ? target.Position : point,
            hasPoint = true,
            direction = dir,
            hasDirection = true,
        };
    }

    private Ray AimRay(Camera cam)
    {
        bool cursorFree = Cursor.visible && Cursor.lockState == CursorLockMode.None;
        if (aimWithMouseWhenCursorFree && cursorFree && Mouse.current != null)
            return cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    // ------------------------------------------------------------------ aim overrides
    protected override void InitializeAim(AbilityCastInstance c, in CastRequest r)
    {
        base.InitializeAim(c, r);
        c.HasAimTargetPoint = r.hasPoint;
        c.AimTargetPoint = r.point;
    }

    /// <summary>While casting, player abilities follow the crosshair (not a target) unless locked at cast start.</summary>
    protected override void UpdateAim(AbilityCastInstance c, float dt)
    {
        AbilityTargeting t = c.Definition.targeting;
        if (t.aimLock != AbilityAimLock.TrackUntilRelease || AimCamera == null)
        {
            base.UpdateAim(c, dt);
            return;
        }
        CastRequest r = BuildAimRequest(c.Definition, c.Stats);
        if (r.target != null && t.mode == AbilityTargetingMode.Unit)
            c.Target = r.target;
        Vector3 from = Entity.Position;
        Vector3 point = ClampAimPoint(c.Definition, c.Stats, from, r.point);
        TurnAimToward(c, point, r.direction, dt);
        c.AimTargetPoint = r.point;
        c.HasAimTargetPoint = true;
    }

    protected override void OnCastStarted(AbilityCastInstance cast)
    {
        // Another ability started while a preview was open: the player chose that one, close the preview.
        if (targetingSlot >= 0 && (cast.Slot == null || cast.Slot.Index != targetingSlot))
            CancelTargeting();
        if (!snapTurnToAim || cast.Definition.targeting.mode == AbilityTargetingMode.Self && !cast.Definition.targeting.faceAim)
            return;
        Vector3 d = cast.AimDirection;
        if (d.sqrMagnitude > 1e-4f && cast.Definition.targeting.faceAim)
            transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }

    // ------------------------------------------------------------------ click-to-confirm
    private void BeginTargeting(int slot)
    {
        CancelTargeting();
        AbilitySlot s = GetSlot(slot);
        targetingSlot = slot;
        targetingStart = Time.time;
        previewCast = new AbilityCastInstance(this, Entity, s, s.ability, s.modifiers);
        TargetingStarted?.Invoke(slot);
    }

    /// <summary>Casts the previewed ability at the current aim. Returns true if it started.</summary>
    public bool ConfirmTargeting()
    {
        if (targetingSlot < 0)
            return false;
        int slot = targetingSlot;
        AbilitySlot s = GetSlot(slot);
        EndTargeting(false);
        bool ok = s != null && s.ability != null && TryCast(slot, BuildAimRequest(s.ability, s.Stats));
        TargetingEnded?.Invoke(slot, ok);
        return ok;
    }

    /// <summary>Closes the preview without casting.</summary>
    public void CancelTargeting()
    {
        if (targetingSlot < 0)
            return;
        int slot = targetingSlot;
        EndTargeting(false);
        TargetingEnded?.Invoke(slot, false);
    }

    private void EndTargeting(bool unused)
    {
        targetingSlot = -1;
        previewCast = null;
        for (int i = 0; i < previews.Count; i++)
            previews[i]?.Release();
        previews.Clear();
    }

    protected override void Update()
    {
        base.Update();
        if (targetingSlot >= 0)
            UpdateTargeting();
        if (cancelCastInput != null && cancelCastInput.action != null && cancelCastInput.action.triggered)
            CancelCurrentCast();
    }

    /// <summary>
    /// Closes an open click-to-confirm preview, otherwise cancels the abilities still winding up (see
    /// <see cref="AbilityCaster.CancelCasting"/>). Returns true if anything was cancelled.
    /// </summary>
    public bool CancelCurrentCast()
    {
        if (targetingSlot >= 0)
        {
            CancelTargeting();
            return true;
        }
        return CancelCasting();
    }

    private void EnableCancelInput()
    {
        if (isActiveAndEnabled && cancelCastInput != null && cancelCastInput.action != null)
            cancelCastInput.action.Enable();
    }

    private void OnEnable()
    {
        if (!enabledControllers.Contains(this))
            enabledControllers.Add(this);
        EnableCancelInput();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => enabledControllers.Clear();

    private void UpdateTargeting()
    {
        AbilitySlot s = GetSlot(targetingSlot);
        if (s == null || s.ability == null || !s.IsReady || Entity == null || !Entity.CanCast)
        {
            CancelTargeting();
            return;
        }

        // Aim the preview like a real cast.
        CastRequest r = BuildAimRequest(s.ability, s.Stats);
        previewCast.Target = r.target;
        InitializeAim(previewCast, r);

        previewShapes.Clear();
        List<CastAction> actions = s.ability.actions;
        for (int i = 0; i < actions.Count; i++)
            actions[i]?.GetTelegraphShapes(previewCast, previewShapes);

        Color color = previewColor.a > 0.01f ? previewColor : CombatSettings.Instance.aimPreviewColor;
        while (previews.Count < previewShapes.Count)
            previews.Add(AbilityTelegraph.Show(previewShapes[previews.Count], color));
        for (int i = 0; i < previews.Count; i++)
        {
            if (i < previewShapes.Count)
                previews[i]?.SetShape(previewShapes[i]);
        }

        // Confirm with left click, cancel with right click / Escape.
        if (Time.time - targetingStart < 0.1f)
            return;
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            pointerClaimFrame = Time.frameCount;
            ConfirmTargeting();
        }
        else if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
        {
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                pointerClaimFrame = Time.frameCount;
            CancelTargeting();
        }
    }

    protected override void OnDisable()
    {
        enabledControllers.Remove(this);
        EndTargeting(false);
        base.OnDisable();
    }

    // ------------------------------------------------------------------ saving absorbed abilities
    /// <summary>Save data for every absorbed ability the player holds (store it in your save file).</summary>
    public List<AbilityGrantSaveData> GetAbsorbedAbilities()
    {
        var list = new List<AbilityGrantSaveData>();
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s?.Grant == null || s.ability == null)
                continue;
            AbilityGrantSaveData d = s.Grant.ToSaveData();
            d.slot = i;
            list.Add(d);
        }
        return list;
    }

    /// <summary>Puts saved absorbed abilities back into their slots (abilities are found in the Ability Database).</summary>
    public void RestoreAbsorbedAbilities(IList<AbilityGrantSaveData> data)
    {
        if (data == null)
            return;
        Initialize();
        for (int i = 0; i < data.Count; i++)
        {
            AbilityGrant grant = AbilityGrant.FromSaveData(data[i]);
            if (grant == null)
                continue;
            int slot = data[i].slot;
            if (slot >= 0 && slot < slots.Count && CanUseSlotForGrant(slot))
                GrantInto(slot, grant);
            else
                Receive(grant, out _);
        }
    }
}
