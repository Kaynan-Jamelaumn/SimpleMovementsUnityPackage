using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Blocking for a character (player or mob): raise a shield - or a weapon's guard - to stop hits that come from the
/// front, within the shield's arc and on the body parts it covers. Each blocked hit costs stamina (heavy blows more);
/// running out breaks the guard and staggers the defender. Raising the block just before a melee hit parries it: no
/// damage, and the attacker is staggered. Attacking with the block up bashes with the shield.
/// <para>
/// What blocks comes from the hands: the off-hand shield (stowed while a two-handed weapon is held), else the main
/// weapon's guard, else the off-hand weapon's guard. Mobs without a Weapon Controller use the Shield Item or Innate
/// Defense set here, and their AI raises the guard when an enemy swings at them.
/// </para>
/// It plugs into the combat system (a <see cref="CombatEntity"/> damage interceptor, after body parts are resolved), so
/// weapons, projectiles and abilities of everyone respect it.
/// </summary>
[DisallowMultipleComponent]
public class BlockController : MonoBehaviour, IDamageInterceptor, IDisplacementModifier
{
    public enum BlockState { Idle, Raising, Blocking, GuardBroken }

    [Header("What blocks (characters without a Weapon Controller)")]
    [Tooltip("Mobs and NPCs: the shield they carry (its Shield settings are used).")]
    [SerializeField] private ArmorSO shieldItem;
    [Tooltip("Mobs and NPCs without a shield item: block with these settings (a guarding beast, a parrying duelist).")]
    [SerializeField] private bool useInnateDefense = false;
    [SerializeField] private ShieldDefense innateDefense = ShieldDefense.WeaponGuard();

    [Header("Player Input")]
    [Tooltip("The block input (optional). Empty = an action named below, else the right mouse button.")]
    [SerializeField] private InputActionReference blockInput;
    [Tooltip("Actions looked up by name in the player's input actions.")]
    [SerializeField] private string[] blockActionNames = { "Block", "Guard", "Defend", "Shield" };
    [Tooltip("Without a block action, the right mouse button blocks (not while dual wielding: then it attacks with the off hand).")]
    [SerializeField] private bool rightMouseFallback = true;
    [Tooltip("Hold the input to block (off = press to raise, press again to lower).")]
    [SerializeField] private bool holdToBlock = true;

    [Header("AI (mobs)")]
    [Tooltip("Chance to raise the guard when an enemy in front starts a weapon swing at it.")]
    [SerializeField, Range(0f, 1f)] private float aiBlockChance = 0.5f;
    [Tooltip("Seconds before the mob reacts to a swing.")]
    [SerializeField, Min(0f)] private float aiReactionTime = 0.12f;
    [Tooltip("Seconds the mob keeps its guard up after deciding to block.")]
    [SerializeField, Min(0.1f)] private float aiHoldTime = 1f;
    [Tooltip("Chance per second to raise the guard while an enemy aims a drawn bow / loaded weapon at it nearby... or simply stands close.")]
    [SerializeField, Range(0f, 1f)] private float aiGuardWhenCloseChance = 0.15f;
    [Tooltip("Distance under which an enemy counts as close for the guard above (metres).")]
    [SerializeField, Min(0.5f)] private float aiCloseDistance = 3f;

    [Header("Animation")]
    [Tooltip("Animator bool true while the block is up (players: through the animation model; others: their Animator). Missing parameters are ignored.")]
    [SerializeField] private string blockingBool = "IsBlocking";
    [Tooltip("Animator trigger when a hit is blocked.")]
    [SerializeField] private string blockHitTrigger = "BlockHitTrigger";
    [Tooltip("Animator trigger when a hit is parried.")]
    [SerializeField] private string parryTrigger = "ParryTrigger";
    [Tooltip("Animator trigger when the guard breaks.")]
    [SerializeField] private string guardBreakTrigger = "GuardBreakTrigger";
    [Tooltip("Animator trigger of the shield bash.")]
    [SerializeField] private string bashTrigger = "ShieldBashTrigger";

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    /// <summary>The block went up (true) or down (false).</summary>
    public event Action<bool> BlockChanged;
    /// <summary>A hit was blocked (possibly partly; the info's amount is what went through).</summary>
    public event Action<DamageInfo> HitBlocked;
    /// <summary>A hit was parried.</summary>
    public event Action<DamageInfo> Parried;
    /// <summary>The guard broke (the attacker, if known).</summary>
    public event Action<CombatEntity> GuardBroken;

    private BlockState state = BlockState.Idle;
    private float stateSince;
    private float guardBrokenUntil;
    private float nextBashTime;
    private bool toggled;
    private float aiFrom, aiUntil;
    private float aiCloseCheck;
    private CombatEntity entity;
    private WeaponController weapons;
    private StaminaManager stamina;
    private PlayerAnimationModel playerAnim;
    private Animator animator;
    private CombatInputBinding input;
    private ShieldDefense activeDefense;
    private ItemSO activeItem;
    private InventoryItem activeInventoryItem;
    private bool moveApplied;
    private float speedDelta;
    private bool isPlayer;

    public BlockState State => state;
    /// <summary>The block is up (raising or raised).</summary>
    public bool IsBlocking => state == BlockState.Blocking || state == BlockState.Raising;
    /// <summary>The block protects now (raised, not still coming up).</summary>
    public bool IsGuarding => state == BlockState.Blocking;
    public bool IsGuardBroken => state == BlockState.GuardBroken;
    /// <summary>The settings of what blocks right now (null when nothing can block).</summary>
    public ShieldDefense ActiveDefense => activeDefense;
    /// <summary>The item blocking (shield or weapon), or null.</summary>
    public ItemSO ActiveItem => activeItem;
    public int InterceptOrder => 100;
    /// <summary>The input behind blocking (for UI and logs).</summary>
    public string InputSource => input != null ? input.Source : "AI";

    /// <summary>The block controller of a character, or null.</summary>
    public static BlockController For(Component anyPart) => anyPart != null ? anyPart.GetComponentInParent<BlockController>() : null;

    /// <summary>Does the character currently guard against hits from <paramref name="attacker"/> (AI: flank or wait)?</summary>
    public bool IsBlockingAgainst(CombatEntity attacker)
    {
        if (state != BlockState.Blocking || activeDefense == null || attacker == null || entity == null)
            return false;
        return activeDefense.Covers(entity.Forward, entity.Position - attacker.Position);
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        entity = GetComponent<CombatEntity>();
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        weapons = GetComponentInChildren<WeaponController>();
        if (weapons == null)
            weapons = GetComponentInParent<WeaponController>();
        BaseStatusController status = GetComponentInParent<BaseStatusController>();
        if (status is PlayerStatusController ps)
            stamina = ps.StaminaManager;
        if (stamina == null && status != null)
            stamina = status.GetComponentInChildren<StaminaManager>();
        if (stamina == null)
            stamina = GetComponentInChildren<StaminaManager>();
        playerAnim = GetComponentInChildren<PlayerAnimationModel>();
        animator = GetComponentInChildren<Animator>();
        isPlayer = status is PlayerStatusController;
    }

    private void OnEnable()
    {
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        if (entity != null)
        {
            entity.AddDamageInterceptor(this);
            entity.AddDisplacementModifier(this);
        }
        CombatEvents.MeleeSwingStarted += OnMeleeSwing;
        CombatEvents.CastStarted += OnCastStarted;
    }

    private void Start()
    {
        if (isPlayer && CombatInputBinding.IsPlayer(this))
            input = CombatInputBinding.Create(this, blockInput, blockActionNames, Key.None, rightMouseFallback ? MouseFallback.RightButton : MouseFallback.None, "Block");
    }

    private void OnDisable()
    {
        if (entity != null)
        {
            entity.RemoveDamageInterceptor(this);
            entity.RemoveDisplacementModifier(this);
        }
        CombatEvents.MeleeSwingStarted -= OnMeleeSwing;
        CombatEvents.CastStarted -= OnCastStarted;
        Lower(false);
    }

    private void OnDestroy()
    {
        input?.Dispose();
    }

    // ------------------------------------------------------------------ source
    /// <summary>What can block right now: the off-hand shield, the main weapon's guard, the off-hand weapon's guard.</summary>
    private bool ResolveSource(out ShieldDefense defense, out ItemSO item, out InventoryItem invItem)
    {
        defense = null;
        item = null;
        invItem = null;
        if (weapons != null)
        {
            ArmorSO shield = weapons.OffHandShield;
            if (shield != null && shield.ShieldDefense != null && !WeaponController.IsBroken(weapons.OffHandShieldItem, shield))
            {
                defense = shield.ShieldDefense;
                item = shield;
                invItem = weapons.OffHandShieldItem;
                return true;
            }
            WeaponSO main = weapons.EquippedWeapon;
            if (main != null && main.Guard != null && !WeaponController.IsBroken(weapons.HeldItem, main))
            {
                defense = main.Guard;
                item = main;
                invItem = weapons.HeldItem;
                return true;
            }
            WeaponSO off = weapons.OffHandWeapon;
            if (off != null && off.Guard != null && !WeaponController.IsBroken(weapons.OffHandItem, off))
            {
                defense = off.Guard;
                item = off;
                invItem = weapons.OffHandItem;
                return true;
            }
            return false;
        }
        if (shieldItem != null && shieldItem.ShieldDefense != null)
        {
            defense = shieldItem.ShieldDefense;
            item = shieldItem;
            return true;
        }
        if (useInnateDefense && innateDefense != null && innateDefense.enabled)
        {
            defense = innateDefense;
            return true;
        }
        return false;
    }

    /// <summary>Can this character block at all right now (something to block with)?</summary>
    public bool CanBlock => ResolveSource(out _, out _, out _);

    // ------------------------------------------------------------------ per frame
    private void Update()
    {
        bool want = WantsToBlock();
        float now = Time.time;
        switch (state)
        {
            case BlockState.Idle:
                if (want)
                    TryRaise();
                break;
            case BlockState.Raising:
                if (!want || !SourceStillValid())
                {
                    Lower(true);
                    break;
                }
                if (now - stateSince >= (activeDefense != null ? activeDefense.raiseTime : 0f))
                    SetState(BlockState.Blocking);
                break;
            case BlockState.Blocking:
                if (!want || !SourceStillValid() || !CanAct())
                {
                    Lower(true);
                    break;
                }
                if (activeDefense.staminaPerSecond > 0f)
                {
                    float drain = activeDefense.staminaPerSecond * Time.deltaTime;
                    if (!SpendStamina(drain))
                        Lower(true);
                }
                break;
            case BlockState.GuardBroken:
                if (now >= guardBrokenUntil)
                    SetState(BlockState.Idle);
                break;
        }
    }

    private bool WantsToBlock()
    {
        if (entity != null && (entity.IsDead || entity.IsStunned))
            return false;
        if (isPlayer)
        {
            if (input == null)
                return false;
            if (weapons != null && weapons.InputBlocked)
                return false;
            // The right mouse button fallback belongs to the off-hand weapon while dual wielding.
            if (input.UsesMouseFallback && weapons != null && weapons.IsDualWielding)
                return false;
            if (input.UsesMouseFallback && PlayerAbilityController.IsPointerCapturedFor(gameObject))
                return false;
            if (holdToBlock)
                return input.Held;
            if (input.Pressed)
                toggled = !toggled;
            return toggled;
        }
        UpdateAiGuard();
        float now = Time.time;
        return now >= aiFrom && now < aiUntil;
    }

    private bool SourceStillValid()
    {
        if (!ResolveSource(out ShieldDefense d, out ItemSO item, out InventoryItem inv))
            return false;
        if (d != activeDefense)
        {
            activeDefense = d;
            activeItem = item;
            activeInventoryItem = inv;
        }
        return true;
    }

    private bool CanAct() => entity == null || (!entity.IsDead && !entity.IsStunned);

    private void TryRaise()
    {
        if (!CanAct() || Time.time < guardBrokenUntil)
            return;
        if (!ResolveSource(out ShieldDefense d, out ItemSO item, out InventoryItem inv))
            return;
        if (weapons != null)
        {
            // An attack must be cancellable (recovery past its cancel point) to raise the block; a charge is dropped.
            if (weapons.IsCharging)
                weapons.CancelCharge();
            else if (weapons.IsAttacking)
            {
                if (!weapons.CanAttack())
                    return; // the block goes up as soon as the attack can be cancelled (the input is still held)
                weapons.InterruptAttack();
            }
            weapons.CancelReload(weapons.MainHand);
            weapons.CancelReload(weapons.OffHand);
            weapons.DrawWeapons();
        }
        activeDefense = d;
        activeItem = item;
        activeInventoryItem = inv;
        SetState(BlockState.Raising);
        ApplyMovement(d.moveSpeedWhileBlocking);
        SetAnimatorBool(true);
        BlockChanged?.Invoke(true);
        if (debugLog)
            Debug.Log($"[Block] {name} raises {(item != null ? item.Name : "its guard")}.", this);
    }

    /// <summary>Lowers the block (if up).</summary>
    public void Lower(bool feedback)
    {
        if (state != BlockState.Raising && state != BlockState.Blocking)
            return;
        SetState(BlockState.Idle);
        RestoreMovement();
        SetAnimatorBool(false);
        toggled = false;
        if (feedback)
            BlockChanged?.Invoke(false);
    }

    /// <summary>Raises the block for <paramref name="seconds"/> (AI, scripts).</summary>
    public void GuardFor(float seconds, float delay = 0f)
    {
        aiFrom = Time.time + Mathf.Max(0f, delay);
        aiUntil = aiFrom + Mathf.Max(0.05f, seconds);
    }

    private void SetState(BlockState s)
    {
        state = s;
        stateSince = Time.time;
    }

    // ------------------------------------------------------------------ AI
    private void OnMeleeSwing(CombatEntity attacker)
    {
        if (isPlayer || entity == null || attacker == null || attacker == entity || entity.IsDead || aiBlockChance <= 0f)
            return;
        if (!CanBlock || CombatRelations.Get(entity, attacker) != CombatRelation.Enemy)
            return;
        Vector3 toMe = entity.Position - attacker.Position;
        toMe.y = 0f;
        float d = toMe.magnitude;
        if (d > 4f + attacker.Radius + entity.Radius || d < 1e-3f)
            return;
        if (Vector3.Dot(attacker.Forward, toMe / d) < 0.4f || Vector3.Dot(entity.Forward, -toMe / d) < 0.2f)
            return; // not swinging at me, or I do not face it
        if (UnityEngine.Random.value >= aiBlockChance)
            return;
        GuardFor(aiHoldTime, aiReactionTime);
    }

    private void UpdateAiGuard()
    {
        if (aiGuardWhenCloseChance <= 0f || Time.time < aiCloseCheck || entity == null)
            return;
        aiCloseCheck = Time.time + 1f;
        if (entity.Caster != null && entity.Caster.IsCasting)
            return;
        CombatEntity target = entity.LastTarget;
        if (target == null)
            target = entity.TopThreat();
        if (target == null || !target.IsAlive)
            return;
        if ((target.Position - entity.Position).sqrMagnitude > aiCloseDistance * aiCloseDistance)
            return;
        if (UnityEngine.Random.value < aiGuardWhenCloseChance && CanBlock)
            GuardFor(aiHoldTime * 0.8f, aiReactionTime);
    }

    private void OnCastStarted(AbilityCastInstance cast)
    {
        // Mobs drop their guard to attack.
        if (!isPlayer && cast != null && cast.CasterEntity == entity)
        {
            aiUntil = 0f;
            Lower(true);
        }
    }

    // ------------------------------------------------------------------ hits
    bool IDamageInterceptor.InterceptDamage(ref DamageInfo info)
    {
        if (state != BlockState.Blocking || activeDefense == null || entity == null)
            return true;
        if (info.unblockable || info.isPeriodic || info.delivery == DamageDelivery.Periodic || info.type == DamageType.True || info.isReflected)
            return true;
        ShieldDefense d = activeDefense;
        if (info.delivery == DamageDelivery.Projectile && !d.blocksProjectiles)
            return true;
        if (info.delivery == DamageDelivery.Area && !d.blocksAreaDamage)
            return true;

        Vector3 travel = info.direction;
        if (info.source != null && (travel.sqrMagnitude < 1e-6f || info.delivery != DamageDelivery.Projectile))
            travel = entity.Position - info.source.Position;
        if (!d.Covers(entity.Forward, travel))
            return true; // from the side or behind
        if (!d.CoversPart(info.bodyPart))
            return true; // under (or over) the shield

        bool parryable = info.delivery == DamageDelivery.Melee || (info.delivery == DamageDelivery.Projectile && d.parryProjectiles) ||
                         (info.delivery == DamageDelivery.Unknown && info.source != null &&
                          (info.source.Position - entity.Position).sqrMagnitude < 9f);
        if (parryable && d.parryWindow > 0f && Time.time - stateSince <= d.parryWindow)
        {
            Parry(ref info, d);
            return false;
        }

        float reduction = d.Reduction(info.type, info.element);
        if (reduction <= 0f)
            return true;
        float before = info.amount;
        float stopped = before * reduction;
        float guard = info.guardDamage > 0f ? info.guardDamage : 1f;
        float cost = (d.staminaPerBlock + stopped * d.staminaPerDamage) * guard;
        bool breakGuard = d.guardBreakDamage > 0f && before >= d.guardBreakDamage;
        if (stamina != null && cost > 0f)
        {
            float have = Mathf.Max(0f, stamina.CurrentValue);
            if (cost > have && d.guardBreakOnNoStamina)
            {
                // The stamina pays for part of the hit; the rest goes through, and the guard breaks.
                stopped *= cost > 0f ? have / cost : 1f;
                breakGuard = true;
            }
            stamina.ConsumeStamina(Mathf.Min(cost, have));
        }

        info.amount = Mathf.Max(0f, before - stopped);
        info.blocked = true;
        info.blockedAmount = stopped;
        WearItem(d);

        Vector3 at = info.point != Vector3.zero ? info.point : entity.Center + entity.Forward * entity.Radius;
        if (d.blockVfx != null)
            AbilityPool.PlayVfx(d.blockVfx, at, Quaternion.LookRotation(travel.sqrMagnitude > 1e-6f ? -travel : entity.Forward), 0f);
        PlaySound(d.blockSound, at);
        SetAnimatorTrigger(blockHitTrigger);
        HitBlocked?.Invoke(info);
        CombatEvents.RaiseBlocked(info);
        if (debugLog)
            Debug.Log($"[Block] {name} blocked {stopped:0.#} of {before:0.#} ({cost:0.#} stamina).", this);

        if (breakGuard)
            BreakGuard(info.source, d);
        return info.amount > 0.0001f;
    }

    private void Parry(ref DamageInfo info, ShieldDefense d)
    {
        info.parried = true;
        info.blocked = true;
        info.blockedAmount = info.amount;
        info.amount = 0f;
        if (info.source != null && info.source != entity && d.parryStun > 0f)
        {
            info.source.ApplyControl(ControlType.Stun, d.parryStun, entity);
            WeaponController attackerWeapons = info.source.GetComponentInChildren<WeaponController>();
            if (attackerWeapons != null)
                attackerWeapons.InterruptAttack();
        }
        Vector3 at = info.point != Vector3.zero ? info.point : entity.Center + entity.Forward * entity.Radius;
        if (d.parryVfx != null)
            AbilityPool.PlayVfx(d.parryVfx, at, Quaternion.LookRotation(entity.Forward), 0f);
        PlaySound(d.parrySound != null ? d.parrySound : d.blockSound, at);
        SetAnimatorTrigger(parryTrigger);
        Parried?.Invoke(info);
        CombatEvents.RaiseBlocked(info);
        if (debugLog)
            Debug.Log($"[Block] {name} parried {(info.source != null ? info.source.name : "a hit")}.", this);
    }

    private void BreakGuard(CombatEntity attacker, ShieldDefense d)
    {
        RestoreMovement();
        SetAnimatorBool(false);
        state = BlockState.GuardBroken;
        stateSince = Time.time;
        guardBrokenUntil = Time.time + d.guardBreakCooldown;
        toggled = false;
        aiUntil = 0f;
        if (d.guardBreakStun > 0f && entity != null)
            entity.ApplyControl(ControlType.Stun, d.guardBreakStun, attacker);
        PlaySound(d.guardBreakSound, entity != null ? entity.Center : transform.position);
        SetAnimatorTrigger(guardBreakTrigger);
        BlockChanged?.Invoke(false);
        GuardBroken?.Invoke(attacker);
        if (debugLog)
            Debug.Log($"[Block] {name}: guard broken.", this);
    }

    private void WearItem(ShieldDefense d)
    {
        if (d.durabilityPerBlock <= 0 || activeInventoryItem == null || activeItem == null)
            return;
        if (weapons != null)
            weapons.ConsumeDurability(activeInventoryItem, activeItem, d.durabilityPerBlock);
        else
            activeInventoryItem.durability = Mathf.Max(0f, activeInventoryItem.durability - d.durabilityPerBlock);
    }

    Vector3 IDisplacementModifier.ModifyDisplacement(Vector3 displacement, CombatEntity source, bool isPull)
    {
        if (state != BlockState.Blocking || activeDefense == null || isPull || entity == null)
            return displacement;
        Vector3 travel = source != null ? entity.Position - source.Position : displacement;
        return activeDefense.Covers(entity.Forward, travel) ? displacement * activeDefense.knockbackWhileBlocking : displacement;
    }

    // ------------------------------------------------------------------ bash
    /// <summary>Attacking with the block up: a shield bash (push, stagger). Returns true if it happened.</summary>
    public bool TryBash()
    {
        ShieldDefense d = activeDefense;
        if (!IsBlocking || d == null || !d.bashEnabled || Time.time < nextBashTime || entity == null)
            return false;
        if (stamina != null && d.bashStamina > 0f)
        {
            if (stamina.CurrentValue < d.bashStamina)
                return false;
            stamina.ConsumeStamina(d.bashStamina);
        }
        nextBashTime = Time.time + d.bashCooldown;
        var shape = HitShape.ConeShape(d.bashRange, d.bashAngle);
        ResolvedShape r = ResolvedShape.Resolve(shape, entity.BasePosition, Quaternion.LookRotation(entity.Forward, Vector3.up), 1f);
        var targets = new System.Collections.Generic.List<CombatEntity>(4);
        CombatQuery.Overlap(r, targets);
        CombatEvents.RaiseMeleeSwing(entity);
        foreach (CombatEntity t in targets)
        {
            if (t == null || t == entity || !t.IsAlive)
                continue;
            CombatRelation rel = CombatRelations.Get(entity, t);
            if (!CombatTargeting.CanHit(TargetFilter.Enemies | TargetFilter.Neutral, null, rel, t, true))
                continue;
            Vector3 dir = CombatQuery.FlatDirection(entity.Position, t.Position, entity.Forward);
            if (d.bashDamage > 0f)
                t.ApplyDamage(new DamageInfo
                {
                    amount = d.bashDamage,
                    source = entity,
                    target = t,
                    point = t.Center - dir * t.Radius,
                    direction = dir,
                    type = DamageType.Physical,
                    delivery = DamageDelivery.Melee,
                    guardDamage = 2f,
                });
            if (t.IsAlive && d.bashKnockback > 0f)
                t.ApplyDisplacement(dir * d.bashKnockback, 0.2f, 0f, false, entity);
            if (t.IsAlive && d.bashStun > 0f)
                t.ApplyControl(ControlType.Stun, d.bashStun, entity);
        }
        PlaySound(d.bashSound, entity.Center + entity.Forward);
        SetAnimatorTrigger(bashTrigger);
        return true;
    }

    // ------------------------------------------------------------------ helpers
    private bool SpendStamina(float amount)
    {
        if (stamina == null || amount <= 0f)
            return true;
        if (stamina.CurrentValue < amount)
            return false;
        stamina.ConsumeStamina(amount);
        return true;
    }

    private void ApplyMovement(float multiplier)
    {
        RestoreMovement();
        multiplier = Mathf.Clamp01(multiplier);
        if (Mathf.Approximately(multiplier, 1f) || entity == null)
            return;
        SpeedManager sm = isPlayer ? entity.Speed : null;
        if (sm != null)
        {
            float before = sm.Speed;
            sm.ModifySpeed(before * (multiplier - 1f));
            speedDelta = sm.Speed - before;
        }
        else
        {
            entity.GuardMoveMultiplier = multiplier;
        }
        moveApplied = true;
    }

    private void RestoreMovement()
    {
        if (!moveApplied)
            return;
        moveApplied = false;
        if (entity == null)
            return;
        SpeedManager sm = isPlayer ? entity.Speed : null;
        if (sm != null)
        {
            if (!Mathf.Approximately(speedDelta, 0f))
                sm.ModifySpeed(-speedDelta);
            speedDelta = 0f;
        }
        else
        {
            entity.GuardMoveMultiplier = 1f;
        }
    }

    private void SetAnimatorBool(bool value)
    {
        if (string.IsNullOrEmpty(blockingBool))
            return;
        if (playerAnim != null)
            playerAnim.SetParameterSafe(blockingBool, value);
        else if (animator != null)
            AnimatorParameterCache.SetBool(animator, blockingBool, value);
    }

    private void SetAnimatorTrigger(string trigger)
    {
        if (string.IsNullOrEmpty(trigger))
            return;
        if (playerAnim != null)
        {
            if (playerAnim.HasAnimationParameter(trigger) && playerAnim.Anim != null)
                playerAnim.Anim.SetTrigger(trigger);
        }
        else if (animator != null)
        {
            AnimatorParameterCache.SetTrigger(animator, trigger);
        }
    }

    private void PlaySound(AudioClip clip, Vector3 at)
    {
        if (clip == null)
            return;
        if (weapons != null)
            weapons.PlaySound(clip);
        else
            AbilityPool.PlaySound(clip, at, 1f);
    }
}
