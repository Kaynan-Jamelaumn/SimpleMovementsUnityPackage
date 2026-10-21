using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Small visuals the powers build when no prefab is given: a tether line and a glowing gate disc.</summary>
public static class PowerVisuals
{
    private static Material material;

    /// <summary>An unlit material that takes a colour (shared).</summary>
    public static Material Material
    {
        get
        {
            if (material == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Unlit/Color");
                material = s != null ? new Material(s) : null;
            }
            return material;
        }
    }

    public static LineRenderer Line(string name, Color color, float width)
    {
        var go = new GameObject(name);
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width * 0.6f;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (Material != null)
            line.sharedMaterial = Material;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, color.a * 0.5f);
        return line;
    }

    public static GameObject Gate(Vector3 groundPoint, Vector3 facing, float radius, Color color, GameObject prefab)
    {
        facing.y = 0f;
        if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
        Quaternion look = Quaternion.LookRotation(facing.normalized, Vector3.up);
        if (prefab != null)
            return UnityEngine.Object.Instantiate(prefab, groundPoint, look);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Gate";
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.Destroy(col);
        go.transform.SetPositionAndRotation(groundPoint + Vector3.up * radius * 1.1f, look * Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(radius * 1.6f, 0.02f, radius * 2.2f);
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            if (Material != null)
                r.material = new Material(Material);
            r.material.color = color;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return go;
    }

    [Serializable] private class V3 { public Vector3 a, b; }

    public static string Pack(Vector3 a, Vector3 b) => JsonUtility.ToJson(new V3 { a = a, b = b });

    public static bool Unpack(string data, out Vector3 a, out Vector3 b)
    {
        a = b = Vector3.zero;
        if (string.IsNullOrEmpty(data)) return false;
        try
        {
            V3 v = JsonUtility.FromJson<V3>(data);
            if (v == null) return false;
            a = v.a;
            b = v.b;
            return true;
        }
        catch (Exception) { return false; }
    }
}

// ====================================================================== links
/// <summary>
/// A power tied to another character (the partner), drawn as a line between them. Ends when the partner dies or goes
/// out of range. The partner travels to clients by its network id.
/// </summary>
[Serializable]
public abstract class TetherPower : CharacterPower
{
    [Tooltip("The link breaks beyond this distance (metres).")]
    [Min(1f)] public float maxDistance = 15f;
    public Color lineColor = new Color(0.7f, 0.3f, 1f, 0.9f);
    [Min(0.01f)] public float lineWidth = 0.08f;

    [NonSerialized] protected CombatEntity partner;
    [NonSerialized] protected CombatEntity owner;
    [NonSerialized] private int partnerId = -1;
    [NonSerialized] private LineRenderer line;

    public CombatEntity Partner => partner;

    public void SetPartner(CombatEntity e) => partner = e;

    public override string NetDynamic() => PowerNet.Id(partner).ToString();

    public override void ApplyNetDynamic(string data)
    {
        if (int.TryParse(data, out int id))
            partnerId = id;
    }

    public override void Start(PowerContext ctx)
    {
        owner = ctx.Entity;
        if (partner == null)
            partner = PowerNet.Find(partnerId);
        line = PowerVisuals.Line(ctx.Label, lineColor, lineWidth);
        UpdateLine();
        if (!ctx.IsPresentation)
            StartLink(ctx);
    }

    protected virtual void StartLink(PowerContext ctx) { }
    protected virtual void EndLink(PowerContext ctx) { }
    protected virtual void TickLink(PowerContext ctx, float dt) { }

    public override void Tick(PowerContext ctx, float dt)
    {
        if (partner == null && partnerId >= 0)
            partner = PowerNet.Find(partnerId);
        UpdateLine();
        if (ctx.IsPresentation)
            return;
        if (partner == null || !partner.IsAlive || owner == null ||
            (owner.Center - partner.Center).sqrMagnitude > maxDistance * maxDistance)
        {
            Finished = true;
            return;
        }
        TickLink(ctx, dt);
    }

    private void UpdateLine()
    {
        if (line == null)
            return;
        bool show = owner != null && partner != null;
        line.enabled = show;
        if (!show)
            return;
        line.SetPosition(0, owner.Center);
        line.SetPosition(1, partner.Center);
    }

    public override void End(PowerContext ctx)
    {
        if (!ctx.IsPresentation)
            EndLink(ctx);
        if (line != null)
            UnityEngine.Object.Destroy(line.gameObject);
        line = null;
    }

    public override CharacterPower Clone()
    {
        var c = (TetherPower)base.Clone();
        c.line = null;
        return c;
    }
}

/// <summary>Soul Link: whatever damage one of the two takes, the other takes too (both ways).</summary>
[Serializable, AbilityMenu("Link/Soul Link", "The damage the character takes is also dealt to the partner, and the other way round.", 0)]
public class SoulLinkPower : TetherPower
{
    [Tooltip("Percentage of the damage passed on (100 = the same amount).")]
    [Range(1f, 200f)] public float sharePercent = 100f;

    private static int passing;

    public override string Describe() => $"soul link: {sharePercent:0}% of the damage is shared both ways";

    protected override void StartLink(PowerContext ctx)
    {
        if (partner != null)
            partner.Damaged += OnPartnerDamaged;
    }

    protected override void EndLink(PowerContext ctx)
    {
        if (partner != null)
            partner.Damaged -= OnPartnerDamaged;
    }

    public override void OnOwnerDamaged(PowerContext ctx, DamageInfo info) => Pass(info, partner);

    private void OnPartnerDamaged(DamageInfo info) => Pass(info, owner);

    private void Pass(DamageInfo info, CombatEntity to)
    {
        if (to == null || !to.IsAlive || info.isReflected || info.amount <= 0f || passing > 0)
            return;
        passing++;
        try
        {
            to.ApplyDamage(new DamageInfo
            {
                amount = info.amount * sharePercent / 100f,
                source = info.source,
                target = to,
                point = to.Center,
                direction = info.direction,
                type = DamageType.True,
                element = info.element,
                isReflected = true, // never passed back again
            });
        }
        finally { passing--; }
    }
}

/// <summary>Leech: keeps draining the partner's health and healing the character.</summary>
[Serializable, AbilityMenu("Link/Leech Life", "Drains the partner's health every half second and heals the character.", 0)]
public class LeechPower : TetherPower
{
    [Min(0f)] public float damagePerSecond = 8f;
    [Range(0f, 200f)] public float healPercent = 100f;
    public ElementType element = ElementType.Dark;

    [NonSerialized] private float timer;

    public LeechPower() { lineColor = new Color(0.8f, 0.05f, 0.1f, 0.9f); maxDistance = 14f; }

    public override string Describe() => $"drains {damagePerSecond:0.#} health per second";

    protected override void TickLink(PowerContext ctx, float dt)
    {
        timer += dt;
        if (timer < 0.5f)
            return;
        timer -= 0.5f;
        float dealt = partner.ApplyDamage(new DamageInfo
        {
            amount = damagePerSecond * 0.5f,
            source = owner,
            target = partner,
            point = partner.Center,
            direction = (partner.Center - owner.Center).normalized,
            type = DamageType.Magical,
            element = element,
            isPeriodic = true,
        });
        if (dealt > 0f && healPercent > 0f)
            owner.ApplyHeal(dealt * healPercent / 100f, owner);
    }
}

// ====================================================================== debt
/// <summary>Incoming Debt on the character: its next ability costs more or recharges slower.</summary>
[Serializable, AbilityMenu("Debuff/Incoming Debt", "The character's next ability costs more or has a longer cooldown.", 0)]
public class DebtPower : CharacterPower
{
    public DebtKind kind = DebtKind.Cost;
    [Range(1f, 5f)] public float multiplier = 2f;
    public GameObject attachedVfx;

    private const string Layer = "Incoming Debt";
    [NonSerialized] private AbilityCaster caster;
    [NonSerialized] private GameObject vfx;

    public override string Describe() => kind == DebtKind.Cooldown ? $"next ability: cooldown x{multiplier:0.#}" : $"next ability: costs x{multiplier:0.#}";

    public override void Start(PowerContext ctx)
    {
        if (attachedVfx != null)
            vfx = UnityEngine.Object.Instantiate(attachedVfx, ctx.Root);
        if (ctx.IsPresentation || ctx.Entity == null)
            return;
        caster = ctx.Entity.Caster;
        if (caster == null)
        {
            Finished = true;
            return;
        }
        var m = new AbilityModifierSet { label = "Debt" };
        if (kind == DebtKind.Cooldown) m.cooldownMultiplier = multiplier;
        else m.costMultiplier = multiplier;
        caster.SetModifierLayer(Layer, m);
        caster.CastStarted += OnCast;
    }

    // The cast has its numbers (cost paid, cooldown decided): the debt is paid.
    private void OnCast(AbilityCastInstance c) => Finished = true;

    public override void End(PowerContext ctx)
    {
        if (caster != null)
        {
            caster.CastStarted -= OnCast;
            caster.SetModifierLayer(Layer, null);
        }
        caster = null;
        if (vfx != null)
            UnityEngine.Object.Destroy(vfx);
    }
}

// ====================================================================== gates
/// <summary>Twin Gates: two linked gates; any character that walks into one comes out of the other.</summary>
[Serializable, AbilityMenu("Movement/Twin Gates", "Two linked gates; any creature that enters one comes out of the other.", 0)]
public class TwinGatesPower : CharacterPower
{
    [Min(0.3f)] public float gateRadius = 1.1f;
    [Min(0.2f)] public float reuseDelay = 1.5f;
    public GameObject gatePrefab;
    public Color gateColor = new Color(0.3f, 0.6f, 1f, 0.6f);

    [NonSerialized] private Vector3 a, b;
    [NonSerialized] private GameObject gateA, gateB;
    [NonSerialized] private Dictionary<CombatEntity, float> lastUse = new Dictionary<CombatEntity, float>();
    private static readonly List<CombatEntity> movers = new List<CombatEntity>();

    public void SetGates(Vector3 first, Vector3 second)
    {
        a = first;
        b = second;
    }

    public override string Describe() => "two linked gates";

    public override string NetDynamic() => PowerVisuals.Pack(a, b);

    public override void ApplyNetDynamic(string data) => PowerVisuals.Unpack(data, out a, out b);

    public override void Start(PowerContext ctx)
    {
        Vector3 ab = b - a;
        gateA = PowerVisuals.Gate(a, ab, gateRadius, gateColor, gatePrefab);
        gateB = PowerVisuals.Gate(b, -ab, gateRadius, gateColor, gatePrefab);
    }

    public override void Tick(PowerContext ctx, float dt)
    {
        if (ctx.IsPresentation)
            return;
        movers.Clear();
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e != null && e.IsAlive && !e.IsReplica)
                movers.Add(e);
        }
        foreach (CombatEntity e in movers)
        {
            if (lastUse.TryGetValue(e, out float t) && Time.time - t < reuseDelay)
                continue;
            if (Inside(e, a)) Through(e, a, b);
            else if (Inside(e, b)) Through(e, b, a);
        }
        movers.Clear();
    }

    private bool Inside(CombatEntity e, Vector3 gate)
    {
        Vector3 p = e.BasePosition;
        float dx = p.x - gate.x, dz = p.z - gate.z;
        return dx * dx + dz * dz <= (gateRadius + e.Radius) * (gateRadius + e.Radius) && Mathf.Abs(p.y - gate.y) < 2.5f;
    }

    private void Through(CombatEntity e, Vector3 from, Vector3 to)
    {
        // Out on the far side of the exit gate, so it is not entered again at once.
        Vector3 dir = to - from;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
        Vector3 exit = MirageCopy.Ground(to + dir * (gateRadius + e.Radius + 0.4f), e.transform);
        lastUse[e] = Time.time;
        MovementAction.Teleport(e, exit);
    }

    public override void End(PowerContext ctx)
    {
        if (gateA != null) UnityEngine.Object.Destroy(gateA);
        if (gateB != null) UnityEngine.Object.Destroy(gateB);
        gateA = gateB = null;
        lastUse.Clear();
    }

    public override CharacterPower Clone()
    {
        var c = (TwinGatesPower)base.Clone();
        c.lastUse = new Dictionary<CombatEntity, float>();
        c.gateA = c.gateB = null;
        return c;
    }
}

// ====================================================================== phantom
/// <summary>
/// Phantom Exchange: leaves a still copy of the character where it stands. Casting the ability again while the copy
/// exists swaps places with it. The power ends when the copy is destroyed (it is a decoy enemies can hit) or the time
/// runs out.
/// </summary>
[Serializable, AbilityMenu("Illusion/Phantom Exchange", "Leaves a still copy; casting again swaps places with it while it exists.", 0)]
public class PhantomAnchorPower : CharacterPower
{
    [Tooltip("The copy vanishes when hit (and the power ends).")]
    public bool vanishWhenHit = true;
    [Tooltip("Enemies treat the copy as the caster and attack it.")]
    public bool decoy = true;
    [Tooltip("How many swaps (0 = as many as you like while it lasts).")]
    [Min(0)] public int maxSwaps = 0;
    [Tooltip("Played at both ends of a swap.")]
    public GameObject swapVfx;

    [NonSerialized] private MirageCopy copy;
    [NonSerialized] private Vector3 spot;
    [NonSerialized] private Quaternion facing = Quaternion.identity;
    [NonSerialized] private int swaps;

    public override string Describe() => "leaves a phantom; cast again to swap places with it";

    public override string LiveStatus => copy != null ? $"phantom ready ({swaps} swaps)" : "no phantom";

    public override string NetDynamic() => PowerVisuals.Pack(spot, facing.eulerAngles);

    public override void ApplyNetDynamic(string data)
    {
        if (PowerVisuals.Unpack(data, out Vector3 s, out Vector3 e))
        {
            spot = s;
            facing = Quaternion.Euler(e);
        }
    }

    public override int NetState => copy != null ? 1 : 0;

    public override void ApplyNetState(PowerContext ctx, int state)
    {
        if (state == 0 && copy != null)
            copy.Vanish();
    }

    public override void Start(PowerContext ctx)
    {
        Transform model = ctx.Body != null && ctx.Body.Model != null ? ctx.Body.Model : CharacterBody.FindModel(ctx.Root);
        if (model == null)
        {
            Finished = true;
            return;
        }
        if (!ctx.IsPresentation)
        {
            spot = ctx.Root.position;
            facing = ctx.Root.rotation;
        }
        bool real = !ctx.IsPresentation;
        copy = MirageCopy.Create(ctx.Entity, ctx.Root, model, spot, facing, float.IsPositiveInfinity(ctx.Duration) ? 3600f : ctx.Duration + 1f,
            MirageMotion.Idle, false, Vector2.zero, real && vanishWhenHit, real && decoy, null, 1, !real);
    }

    public override bool OnRecast(PowerContext ctx)
    {
        if (copy == null || ctx.Entity == null)
            return false; // no phantom left: a new one is made
        Vector3 me = ctx.Root.position;
        Quaternion myFacing = ctx.Root.rotation;
        MovementAction.Teleport(ctx.Entity, spot);
        copy.transform.SetPositionAndRotation(me, myFacing);
        if (swapVfx != null)
        {
            UnityEngine.Object.Instantiate(swapVfx, me, myFacing);
            UnityEngine.Object.Instantiate(swapVfx, spot, facing);
        }
        spot = me;
        facing = myFacing;
        swaps++;
        ctx.MarkDirty();
        if (maxSwaps > 0 && swaps >= maxSwaps)
            Finished = true;
        return true;
    }

    public override void OnNetDynamicChanged(PowerContext ctx, string data)
    {
        ApplyNetDynamic(data);
        if (copy != null)
            copy.transform.SetPositionAndRotation(spot, facing);
    }

    public override void Tick(PowerContext ctx, float dt)
    {
        if (!ctx.IsPresentation && copy == null)
            Finished = true; // destroyed or gone
    }

    public override void End(PowerContext ctx)
    {
        if (copy != null)
            copy.Vanish();
        copy = null;
    }
}

// ====================================================================== time
/// <summary>
/// Chronal Recall: remembers where the character stands (and its health). When the power ends, it returns there, and
/// gets back the health it had then if it lost some. A still ghost marks the spot meanwhile.
/// </summary>
[Serializable, AbilityMenu("Time/Chronal Recall", "When it ends, the character returns to where it was when it started, with the health it had then.", 0)]
public class ChronalRecallPower : CharacterPower
{
    [Tooltip("Get back the health lost since it started.")]
    public bool restoreHealth = true;
    [Tooltip("A still copy marks where the character will return.")]
    public bool showMarker = true;
    [Tooltip("Played at both ends of the return.")]
    public GameObject returnVfx;

    [NonSerialized] private Vector3 spot;
    [NonSerialized] private Quaternion facing = Quaternion.identity;
    [NonSerialized] private float health;
    [NonSerialized] private MirageCopy marker;

    public override string Describe() => "returns to where it started" + (restoreHealth ? ", with the health it had" : "");

    public override string NetDynamic() => PowerVisuals.Pack(spot, facing.eulerAngles);

    public override void ApplyNetDynamic(string data)
    {
        if (PowerVisuals.Unpack(data, out Vector3 s, out Vector3 e))
        {
            spot = s;
            facing = Quaternion.Euler(e);
        }
    }

    public override void Start(PowerContext ctx)
    {
        if (!ctx.IsPresentation)
        {
            spot = ctx.Root.position;
            facing = ctx.Root.rotation;
            health = ctx.Entity != null && ctx.Entity.Health != null ? ctx.Entity.Health.CurrentValue : 0f;
        }
        if (showMarker)
        {
            Transform model = ctx.Body != null && ctx.Body.Model != null ? ctx.Body.Model : CharacterBody.FindModel(ctx.Root);
            if (model != null)
                marker = MirageCopy.Create(ctx.Entity, ctx.Root, model, spot, facing, 3600f, MirageMotion.Idle, false, Vector2.zero, false, false, null, 1, true);
        }
    }

    public override void End(PowerContext ctx)
    {
        if (marker != null)
            marker.Vanish();
        marker = null;
        CombatEntity e = ctx.Entity;
        if (ctx.IsPresentation || e == null || !e.IsAlive)
            return;
        if (returnVfx != null)
        {
            UnityEngine.Object.Instantiate(returnVfx, ctx.Root.position, ctx.Root.rotation);
            UnityEngine.Object.Instantiate(returnVfx, spot, facing);
        }
        MovementAction.Teleport(e, spot);
        if (restoreHealth && e.Health != null && health > e.Health.CurrentValue)
            e.ApplyHeal(health - e.Health.CurrentValue, e);
    }
}

// ====================================================================== reflection
/// <summary>Mirror Ward: part of the damage the character takes goes back to whoever dealt it.</summary>
[Serializable, AbilityMenu("Defense/Reflect Damage (Mirror Ward)", "Part of every hit taken is dealt back to the attacker.", 0)]
public class ReflectDamagePower : CharacterPower
{
    [Range(1f, 200f)] public float reflectPercent = 50f;
    [Tooltip("Hits reflected before it ends (0 = no limit).")]
    [Min(0)] public int charges = 0;
    [Tooltip("Damage over time ticks are reflected too.")]
    public bool includeDamageOverTime = false;
    [Tooltip("Shown on the character while it lasts.")]
    public GameObject attachedVfx;

    [NonSerialized] private int used;
    [NonSerialized] private GameObject vfx;

    public override string Describe() => $"reflects {reflectPercent:0}% of the damage taken" + (charges > 0 ? $" ({charges} hits)" : "");

    public override void Start(PowerContext ctx)
    {
        used = 0;
        if (attachedVfx != null)
            vfx = UnityEngine.Object.Instantiate(attachedVfx, ctx.Root);
    }

    public override void OnOwnerDamaged(PowerContext ctx, DamageInfo info)
    {
        CombatEntity src = info.source;
        if (src == null || src == ctx.Entity || info.isReflected || info.amount <= 0f || (info.isPeriodic && !includeDamageOverTime) || !src.IsAlive)
            return;
        src.ApplyDamage(new DamageInfo
        {
            amount = info.amount * reflectPercent / 100f * ctx.Strength,
            source = ctx.Entity,
            target = src,
            point = src.Center,
            direction = (src.Center - ctx.Entity.Center).normalized,
            type = info.type,
            element = info.element,
            isReflected = true,
        });
        used++;
        if (charges > 0 && used >= charges)
            Finished = true;
    }

    public override void End(PowerContext ctx)
    {
        if (vfx != null)
            UnityEngine.Object.Destroy(vfx);
    }
}
