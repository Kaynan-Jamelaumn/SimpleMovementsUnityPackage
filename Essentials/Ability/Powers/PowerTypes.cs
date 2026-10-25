using System;
using System.Collections.Generic;
using UnityEngine;

// ====================================================================== body size
/// <summary>
/// Grows or shrinks the character for the power's duration: the body, its colliders and hitboxes follow the size
/// (through the Height stat and the Character Body), and its health and physical capabilities can follow it too.
/// Gigantification: +80% size, health and strength with it. Minimization: -50% size, faster, but takes more damage.
/// </summary>
[Serializable, AbilityMenu("Body/Change Size (Giant, Tiny)", "Grows or shrinks the character: body, colliders and hitboxes scale; health, strength, damage, knockback, speed and jump can follow the size.", 0)]
public class SizeChangePower : CharacterPower
{
    [Tooltip("How much bigger (+80 = 80% taller) or smaller (-50 = half the height). The body never goes below half or above double its normal size.")]
    [Range(-50f, 100f)] public float sizePercent = 60f;
    [Tooltip("Seconds to grow or shrink to the new size.")]
    [Min(0f)] public float growSeconds = 0.6f;

    [Header("Follows the size (0 = no, 1 = in proportion)")]
    [Tooltip("Max health follows the size: 1 = +80% size gives +80% max health (the current health keeps its fraction).")]
    [Range(0f, 3f)] public float healthPerSize = 1f;
    [Tooltip("Weapon damage follows the size: 0.5 = +80% size gives +40% weapon damage.")]
    [Range(-2f, 3f)] public float weaponDamagePerSize = 0.5f;
    [Tooltip("Knockback dealt follows the size: 1 = +80% size pushes 80% further.")]
    [Range(-2f, 3f)] public float knockbackPerSize = 1f;
    [Tooltip("Strength points for +100% size (scaled by the size change).")]
    public float strengthPerSize = 10f;
    [Tooltip("Jump force follows the size: 0.3 = +80% size jumps 24% higher.")]
    [Range(-2f, 3f)] public float jumpPerSize = 0.3f;

    [Header("Extra")]
    [Tooltip("Percent move speed (+40 = 40% faster). Players: through the Trait Manager.")]
    public float moveSpeedPercent = 0f;
    [Tooltip("Percent damage taken (+40 = takes 40% more).")]
    public float damageTakenPercent = 0f;
    [Tooltip("Percent attack speed.")]
    public float attackSpeedPercent = 0f;
    [Tooltip("Played on the character when the size changes.")]
    public GameObject vfx;

    [NonSerialized] private object heightToken, statsToken;
    [NonSerialized] private TraitManager.AppliedModifiers traitHandle;
    [NonSerialized] private float healthAdded;
    [NonSerialized] private float damageTakenFactor = 1f;
    [NonSerialized] private bool limitsChanged, growing;

    // Size powers running on each body, and its limits before the first one (restored when the last one ends).
    private static readonly Dictionary<CharacterBody, (int count, Vector2 limits)> widened = new Dictionary<CharacterBody, (int, Vector2)>();
    [NonSerialized] private float growTime;

    private float Factor(PowerContext ctx) => 1f + sizePercent / 100f * Mathf.Max(0f, ctx.Strength);

    public override string Describe()
    {
        var parts = new List<string> { sizePercent >= 0f ? $"{sizePercent:0}% bigger" : $"{-sizePercent:0}% smaller" };
        if (healthPerSize > 0f) parts.Add("health follows the size");
        if (moveSpeedPercent != 0f) parts.Add($"{moveSpeedPercent:+0;-0}% speed");
        if (damageTakenPercent != 0f) parts.Add($"{damageTakenPercent:+0;-0}% damage taken");
        return string.Join(", ", parts);
    }

    public override string LiveStatus => growing ? "changing size" : null;

    public override void Start(PowerContext ctx)
    {
        float f = Factor(ctx);
        float change = f - 1f;
        CharacterBody body = ctx.Body;
        if (body == null && ctx.Root != null && !ctx.IsPresentation)
            AbilityDiagnostics.WarnOnce(ctx.Root.gameObject, "no-body",
                $"[{ctx.Root.name}] has no Character Body: '{ctx.Label}' changes the Height stat (and its health and strength " +
                "follow), but nothing scales the model. Add a Character Body to the character (the Player Setup Validator lists it).");
        if (body != null)
        {
            // A big race doubled would pass the body's limit; the Height stat itself stays between half and double.
            if (widened.TryGetValue(body, out var w))
                widened[body] = (w.count + 1, w.limits);
            else
            {
                widened[body] = (1, body.scaleLimits);
                body.scaleLimits = new Vector2(Mathf.Min(body.scaleLimits.x, 0.25f), Mathf.Max(body.scaleLimits.y, 4f));
            }
            limitsChanged = true;
        }
        growTime = 0f;
        growing = true;
        SetHeight(ctx, growSeconds > 0f ? 0f : 1f);
        if (vfx != null)
            UnityEngine.Object.Instantiate(vfx, ctx.Root.position, ctx.Root.rotation);
        // A client copy only shows the size (body, colliders, hitboxes); the server owns health and stats.
        if (ctx.IsPresentation)
            return;

        // Physical capabilities
        var combat = new List<CombatStatModifier>();
        if (weaponDamagePerSize != 0f) combat.Add(new CombatStatModifier(CombatStatType.WeaponDamage, weaponDamagePerSize * change * 100f));
        if (knockbackPerSize != 0f) combat.Add(new CombatStatModifier(CombatStatType.Knockback, knockbackPerSize * change * 100f));
        if (strengthPerSize != 0f) combat.Add(new CombatStatModifier(CombatStatType.Strength, strengthPerSize * change));
        if (attackSpeedPercent != 0f) combat.Add(new CombatStatModifier(CombatStatType.AttackSpeed, attackSpeedPercent));
        if (combat.Count > 0)
            statsToken = ctx.Stats.AddModifiers(ctx.Label, combat);

        TraitManager traits = ctx.Traits;
        var character = new List<TraitModifier>();
        if (jumpPerSize != 0f) character.Add(TraitModifier.Percent(TraitStat.JumpForce, jumpPerSize * change * 100f));
        if (moveSpeedPercent != 0f) character.Add(TraitModifier.Percent(TraitStat.MoveSpeed, moveSpeedPercent));
        if (traits != null && damageTakenPercent != 0f) character.Add(TraitModifier.Percent(TraitStat.DamageTaken, damageTakenPercent));
        if (traits != null && character.Count > 0)
            traitHandle = traits.ApplyModifiers(character, 1f, ctx.Label);
        if (traits == null && damageTakenPercent != 0f && ctx.Entity != null)
        {
            damageTakenFactor = Mathf.Max(0f, 1f + damageTakenPercent / 100f);
            ctx.Entity.damageTakenMultiplier *= damageTakenFactor;
        }

        // Health in proportion to the size, keeping the current fraction
        HealthManager hp = ctx.Entity != null ? ctx.Entity.Health : null;
        if (hp != null && healthPerSize > 0f)
        {
            float fraction = hp.Fraction;
            healthAdded = hp.MaxValue * healthPerSize * change;
            hp.ModifyMaxValue(healthAdded);
            hp.SetCurrentValue(fraction * hp.MaxValue);
        }
    }

    public override void Tick(PowerContext ctx, float dt)
    {
        if (!growing)
            return;
        growTime += dt;
        float t = growSeconds > 0f ? Mathf.Clamp01(growTime / growSeconds) : 1f;
        SetHeight(ctx, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f)
            growing = false;
    }

    private void SetHeight(PowerContext ctx, float t)
    {
        if (heightToken != null)
            ctx.Stats.RemoveModifiers(heightToken);
        heightToken = ctx.Stats.AddModifiers(ctx.Label + " (size)",
            new[] { new CombatStatModifier(CombatStatType.Height, (Factor(ctx) - 1f) * 100f * t) });
    }

    public override void End(PowerContext ctx)
    {
        CombatStats stats = ctx.Stats;
        if (heightToken != null) stats.RemoveModifiers(heightToken);
        if (statsToken != null) stats.RemoveModifiers(statsToken);
        heightToken = statsToken = null;
        if (traitHandle != null && ctx.Traits != null)
            ctx.Traits.RevertModifiers(traitHandle);
        traitHandle = null;
        if (ctx.Entity != null && damageTakenFactor != 1f && damageTakenFactor > 0f)
            ctx.Entity.damageTakenMultiplier /= damageTakenFactor;
        damageTakenFactor = 1f;
        HealthManager hp = ctx.Entity != null ? ctx.Entity.Health : null;
        if (hp != null && healthAdded != 0f)
        {
            float fraction = hp.Fraction;
            hp.ModifyMaxValue(-healthAdded);
            hp.SetCurrentValue(Mathf.Max(fraction * hp.MaxValue, hp.CurrentValue > 0f ? 1f : 0f));
        }
        healthAdded = 0f;
        CharacterBody body = ctx.Body;
        if (limitsChanged && body != null && widened.TryGetValue(body, out var w))
        {
            if (w.count <= 1)
            {
                widened.Remove(body);
                body.scaleLimits = w.limits;
            }
            else
                widened[body] = (w.count - 1, w.limits);
        }
        limitsChanged = false;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (Mathf.Approximately(sizePercent, 0f))
            warnings.Add($"{owner}: Size Percent is 0, the size does not change.");
    }
}

// ====================================================================== invisibility
/// <summary>
/// Hides the character: its meshes disappear (the local player still sees itself unless told otherwise) and mobs cannot
/// see it, so they search where it was last seen. Optionally broken by taking a hit or by attacking / casting.
/// </summary>
[Serializable, AbilityMenu("Stealth/Invisibility", "Hides the character from sight and from mobs; broken by hits or attacks if set.", 0)]
public class InvisibilityPower : CharacterPower
{
    [Tooltip("Taking a hit ends it.")]
    public bool breakOnDamaged = true;
    [Tooltip("Starting a weapon attack or an ability ends it.")]
    public bool breakOnAttack = true;
    [Tooltip("Seconds after it starts during which attacking does not break it (the cast that made it invisible).")]
    [Min(0f)] public float graceSeconds = 0.3f;
    [Tooltip("Hide the meshes. Off = mobs cannot see it but it still looks the same (use your own shader effect).")]
    public bool hideMeshes = true;
    [Tooltip("The player playing on this machine still sees its own character.")]
    public bool localPlayerSeesItself = true;
    [Tooltip("Played where the character was when it vanishes and when it appears again.")]
    public GameObject vanishVfx;

    [NonSerialized] private List<Renderer> hidden = new List<Renderer>();
    [NonSerialized] private bool active;

    public override string Describe() =>
        "invisible" + (breakOnDamaged || breakOnAttack ? $" (ends when {(breakOnDamaged ? "hit" : "")}{(breakOnDamaged && breakOnAttack ? " or " : "")}{(breakOnAttack ? "attacking" : "")})" : "");

    public override void Start(PowerContext ctx)
    {
        if (ctx.Entity == null)
            return;
        ctx.Entity.AddInvisibility();
        active = true;
        if (hideMeshes && !(localPlayerSeesItself && ctx.IsLocalPlayer))
            Hide(ctx.Root, hidden);
        if (vanishVfx != null)
            UnityEngine.Object.Instantiate(vanishVfx, ctx.Root.position, ctx.Root.rotation);
    }

    public override void OnOwnerDamaged(PowerContext ctx, DamageInfo info)
    {
        if (breakOnDamaged && info.amount > 0f)
            Finished = true;
    }

    public override void OnOwnerActed(PowerContext ctx)
    {
        if (breakOnAttack && ctx.Elapsed >= graceSeconds)
            Finished = true;
    }

    public override CharacterPower Clone()
    {
        var c = (InvisibilityPower)base.Clone();
        c.hidden = new List<Renderer>();
        return c;
    }

    public override void End(PowerContext ctx)
    {
        if (active && ctx.Entity != null)
            ctx.Entity.RemoveInvisibility();
        active = false;
        Show(hidden);
        if (vanishVfx != null && ctx.Root != null)
            UnityEngine.Object.Instantiate(vanishVfx, ctx.Root.position, ctx.Root.rotation);
    }

    /// <summary>Turns off every visible renderer under <paramref name="root"/>, remembering them in <paramref name="into"/>.</summary>
    public static void Hide(Transform root, List<Renderer> into)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled)
                continue;
            r.enabled = false;
            into.Add(r);
        }
    }

    /// <summary>Turns the renderers hidden by <see cref="Hide"/> back on.</summary>
    public static void Show(List<Renderer> list)
    {
        foreach (Renderer r in list)
            if (r != null)
                r.enabled = true;
        list.Clear();
    }
}

// ====================================================================== crowd control immunity
/// <summary>Immovable Force: the character ignores stuns, roots, silences, slows, taunts and knockbacks.</summary>
[Serializable, AbilityMenu("Defense/Immune To Control", "The character ignores crowd control (stun, root, silence, slow, taunt) and displacement (knockback, pull).", 0)]
public class ControlImmunityPower : CharacterPower
{
    [Tooltip("What the character ignores.")]
    public ControlImmunity immuneTo = ControlImmunity.Everything;
    [Tooltip("Removes the crowd control already on the character when it starts.")]
    public bool cleanseOnStart = true;
    [Tooltip("Percent damage taken while it lasts (-30 = 30% less).")]
    public float damageTakenPercent = 0f;
    [Tooltip("Shown on the character while it lasts.")]
    public GameObject attachedVfx;

    [NonSerialized] private bool applied;
    [NonSerialized] private float factor = 1f;
    [NonSerialized] private GameObject vfxInstance;

    public override string Describe() =>
        (immuneTo == ControlImmunity.Everything ? "immune to crowd control and knockback" : $"immune to {immuneTo}") +
        (damageTakenPercent != 0f ? $", {damageTakenPercent:+0;-0}% damage taken" : "");

    public override void Start(PowerContext ctx)
    {
        if (attachedVfx != null)
            vfxInstance = UnityEngine.Object.Instantiate(attachedVfx, ctx.Root);
        if (ctx.Entity == null || ctx.IsPresentation)
            return; // client copies only show the effect
        ctx.Entity.AddImmunity(immuneTo);
        applied = true;
        if (cleanseOnStart)
            ctx.Entity.ClearControl();
        if (damageTakenPercent != 0f)
        {
            factor = Mathf.Max(0f, 1f + damageTakenPercent / 100f);
            ctx.Entity.damageTakenMultiplier *= factor;
        }
    }

    public override void End(PowerContext ctx)
    {
        if (applied && ctx.Entity != null)
        {
            ctx.Entity.RemoveImmunity(immuneTo);
            if (factor > 0f && factor != 1f)
                ctx.Entity.damageTakenMultiplier /= factor;
        }
        applied = false;
        factor = 1f;
        if (vfxInstance != null)
            UnityEngine.Object.Destroy(vfxInstance);
    }
}

// ====================================================================== movement behaviours
/// <summary>
/// Runs a movement behaviour (Sky Walk, Glide, Double Jump, Wall Climb...) for the power's duration, exactly like a
/// trait's (stepped by the movement simulation). Players only: they need a Trait Manager.
/// </summary>
[Serializable, AbilityMenu("Movement/Movement Behaviour (Sky Walk, Glide...)", "Gives a movement behaviour while the power lasts: walk on air, glide, double jump, wall climb... Players only.", 0)]
public class MovementPower : CharacterPower
{
    [Tooltip("The behaviour (the same list traits use). Sky Walk ends the power when its mana or stamina runs out.")]
    [SerializeReference, SubclassSelector] public TraitBehaviour behaviour = new SkyWalkTrait();

    [NonSerialized] private TraitBehaviour runtime;
    [NonSerialized] private TraitManager traits;

    public override string Describe() => behaviour != null ? behaviour.Describe() : "";

    public override string LiveStatus => runtime?.LiveStatus;

    public override void Start(PowerContext ctx)
    {
        traits = ctx.Traits;
        if (behaviour == null)
            return;
        if (traits == null)
        {
            Debug.LogWarning($"[Powers] {ctx.Root.name}: '{ctx.Label}' needs a Trait Manager to run {behaviour.MenuName}; skipped.", ctx.Root);
            return;
        }
        runtime = behaviour.CreateRuntimeCopy();
        traits.AddExternalBehaviour(runtime, ctx.Strength);
    }

    public override void Tick(PowerContext ctx, float dt)
    {
        if (runtime is IPowerEndsItself e && e.PowerFinished)
            Finished = true;
    }

    public override void End(PowerContext ctx)
    {
        if (runtime != null && traits != null)
            traits.RemoveExternalBehaviour(runtime);
        runtime = null;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (behaviour == null)
            errors.Add($"{owner}: no behaviour selected.");
        else
        {
            var e = new List<string>();
            var w = new List<string>();
            behaviour.Validate(e, w);
            foreach (string m in e) errors.Add($"{owner}: {m}");
            foreach (string m in w) warnings.Add($"{owner}: {m}");
        }
    }

    [Serializable]
    private class NetWrap { public string type; public string json; }

    // The behaviour is a [SerializeReference]: sent as its type and its own JSON.
    public override string NetConfig() =>
        behaviour == null ? "" : JsonUtility.ToJson(new NetWrap { type = behaviour.GetType().FullName, json = ScrubReferences(JsonUtility.ToJson(behaviour)) });

    public override void ReadNetConfig(string json)
    {
        behaviour = null;
        if (string.IsNullOrEmpty(json))
            return;
        NetWrap w = JsonUtility.FromJson<NetWrap>(json);
        Type t = w != null ? ActivePowers.FindType(w.type) : null;
        if (t == null || !typeof(TraitBehaviour).IsAssignableFrom(t) || t.IsAbstract)
            return;
        behaviour = (TraitBehaviour)Activator.CreateInstance(t);
        JsonUtility.FromJsonOverwrite(w.json, behaviour);
    }

    public override CharacterPower Clone()
    {
        var c = (MovementPower)base.Clone();
        c.behaviour = behaviour != null ? behaviour.CreateRuntimeCopy() : null;
        c.runtime = null;
        return c;
    }
}

// ====================================================================== illusions
/// <summary>How the copies of a <see cref="MiragePower"/> move.</summary>
public enum MirageMotion
{
    /// <summary>They stand still, playing the idle animation.</summary>
    Idle,
    /// <summary>They walk around.</summary>
    Walk,
    /// <summary>They run around.</summary>
    Run,
    /// <summary>Each copy picks idle, walk or run, and changes its mind from time to time.</summary>
    Mixed,
}

/// <summary>
/// Mirage: copies of the character (same model, same equipment, its animations) appear around it. Each one vanishes
/// after its lifetime or when it takes a hit; enemies treat them as the caster's side, so they attack them. The caster
/// can turn invisible for a moment as they appear. Multiplayer: the server decides where the copies stand and how they
/// move (a seed); every client shows the same copies and removes each one when the server's vanishes.
/// </summary>
[Serializable, AbilityMenu("Illusion/Mirage Copies", "Creates copies of the character around it that idle, walk or run, and vanish after a while or when hit. The caster can turn invisible for a moment.", 0)]
public class MiragePower : CharacterPower
{
    [Tooltip("How many copies.")]
    [Range(1, 12)] public int copies = 4;
    [Tooltip("Copies appear this far around the character (metres).")]
    [Min(0.5f)] public float radius = 3f;
    [Tooltip("Idle: they stand still. Walk / Run: they move around. Mixed: each copy picks and changes.")]
    public MirageMotion motion = MirageMotion.Idle;
    [Tooltip("Seconds a copy lasts (0 = as long as the power).")]
    [Min(0f)] public float copyLifetime = 0f;
    [Tooltip("A copy vanishes when it takes a hit.")]
    public bool vanishWhenHit = true;
    [Tooltip("Enemies see the copies as the caster's side and attack them (decoys).")]
    public bool decoys = true;
    [Tooltip("Seconds the caster is invisible as the copies appear (0 = not at all).")]
    [Min(0f)] public float casterInvisibleSeconds = 1.5f;
    [Tooltip("The caster swaps places with one of the copies, so enemies lose track of which one is real.")]
    public bool swapWithACopy = false;
    [Tooltip("Walk and run speeds of moving copies (metres per second).")]
    public Vector2 walkRunSpeed = new Vector2(1.6f, 4.5f);
    [Tooltip("Played where each copy appears and vanishes.")]
    public GameObject vfx;

    [NonSerialized] private List<MirageCopy> spawned = new List<MirageCopy>();
    [NonSerialized] private List<Renderer> hidden = new List<Renderer>();
    [NonSerialized] private bool invisible;
    [NonSerialized] private Plan plan;

    /// <summary>Where the copies stand and how they move, decided by the server.</summary>
    [Serializable]
    private class Plan
    {
        public List<Vector3> spots = new List<Vector3>();
        public List<int> motions = new List<int>();
        public Quaternion rotation = Quaternion.identity;
        public int seed;
        public float lifetime;
    }

    public override string Describe() =>
        $"{copies} {(motion == MirageMotion.Idle ? "idle" : motion == MirageMotion.Mixed ? "wandering" : motion.ToString().ToLowerInvariant() + "ing")} copies" +
        (casterInvisibleSeconds > 0f ? $", invisible for {casterInvisibleSeconds:0.#} s" : "");

    public override string LiveStatus => $"{CountAlive()} copies";

    private int CountAlive()
    {
        int alive = 0;
        foreach (MirageCopy c in spawned) if (c != null) alive++;
        return alive;
    }

    public override int NetState
    {
        get
        {
            int mask = 0;
            for (int i = 0; i < spawned.Count && i < 31; i++)
                if (spawned[i] != null) mask |= 1 << i;
            return mask;
        }
    }

    public override string NetDynamic() => plan != null ? JsonUtility.ToJson(plan) : null;

    public override void ApplyNetDynamic(string data)
    {
        try { plan = JsonUtility.FromJson<Plan>(data); }
        catch (Exception) { plan = null; }
    }

    public override void ApplyNetState(PowerContext ctx, int state)
    {
        for (int i = 0; i < spawned.Count && i < 31; i++)
            if (spawned[i] != null && (state & (1 << i)) == 0)
                spawned[i].Vanish();
    }

    public override void Start(PowerContext ctx)
    {
        Transform model = ctx.Body != null && ctx.Body.Model != null ? ctx.Body.Model : CharacterBody.FindModel(ctx.Root);
        if (model == null)
        {
            Debug.LogWarning($"[Powers] {ctx.Root.name}: Mirage found no model to copy.", ctx.Root);
            return;
        }
        if (!ctx.IsPresentation || plan == null)
            plan = MakePlan(ctx);
        bool real = !ctx.IsPresentation;
        for (int i = 0; i < plan.spots.Count; i++)
        {
            MirageMotion m = i < plan.motions.Count ? (MirageMotion)plan.motions[i] : MirageMotion.Idle;
            MirageCopy c = MirageCopy.Create(ctx.Entity, ctx.Root, model, plan.spots[i], plan.rotation, plan.lifetime, m, motion == MirageMotion.Mixed,
                walkRunSpeed, real && vanishWhenHit, real && decoys, vfx, plan.seed + i * 7919, !real);
            spawned.Add(c);
        }
        if (casterInvisibleSeconds > 0f && ctx.Entity != null)
        {
            ctx.Entity.AddInvisibility();
            invisible = true;
            if (!ctx.IsLocalPlayer)
                InvisibilityPower.Hide(ctx.Root, hidden);
        }
    }

    private Plan MakePlan(PowerContext ctx)
    {
        var p = new Plan
        {
            seed = UnityEngine.Random.Range(1, int.MaxValue / 2),
            rotation = ctx.Root.rotation,
            lifetime = copyLifetime > 0f ? copyLifetime : float.IsPositiveInfinity(ctx.Duration) ? 8f : ctx.Duration,
        };
        var rng = new System.Random(p.seed);
        Vector3 centre = ctx.Root.position;
        float startAngle = (float)rng.NextDouble() * 360f;
        for (int i = 0; i < copies; i++)
        {
            float a = (startAngle + 360f * i / copies) * Mathf.Deg2Rad;
            float d = radius * (0.6f + 0.4f * (float)rng.NextDouble());
            p.spots.Add(MirageCopy.Ground(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d, ctx.Root));
            p.motions.Add(motion == MirageMotion.Mixed ? rng.Next(0, 3) : (int)motion);
        }
        if (swapWithACopy && p.spots.Count > 0 && !ctx.IsPresentation)
        {
            // The caster takes one copy's place and a copy stands where the caster was.
            int k = rng.Next(0, p.spots.Count);
            Vector3 to = p.spots[k];
            p.spots[k] = centre;
            if (ctx.Entity != null)
                MovementAction.Teleport(ctx.Entity, to);
        }
        return p;
    }

    public override void Tick(PowerContext ctx, float dt)
    {
        if (invisible && ctx.Elapsed >= casterInvisibleSeconds)
            Reveal(ctx);
    }

    public override void OnOwnerActed(PowerContext ctx)
    {
        if (invisible && ctx.Elapsed > 0.3f)
            Reveal(ctx);
    }

    private void Reveal(PowerContext ctx)
    {
        if (!invisible)
            return;
        invisible = false;
        if (ctx.Entity != null)
            ctx.Entity.RemoveInvisibility();
        InvisibilityPower.Show(hidden);
    }

    public override void End(PowerContext ctx)
    {
        Reveal(ctx);
        foreach (MirageCopy c in spawned)
            if (c != null)
                c.Vanish();
        spawned.Clear();
    }

    public override CharacterPower Clone()
    {
        var c = (MiragePower)base.Clone();
        c.spawned = new List<MirageCopy>();
        c.hidden = new List<Renderer>();
        c.plan = null;
        return c;
    }
}
