using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something a piece of equipment (armor, weapon, trinket) or an armor-set bonus does while it is active: stats,
/// resistances, traits, trait enhancements, passive behaviours (double jump, life steal...), abilities on keys,
/// reactions to hits... Items and set bonuses list them with a type dropdown; write a new one by deriving from this
/// class (it appears in the dropdown automatically, grouped by its <see cref="AbilityMenuAttribute"/> path).
/// <para>
/// Effects never add and subtract values by hand: <see cref="Apply"/> returns a handle that undoes exactly what it
/// did. Equipping, unequipping, swapping and set tiers changing can therefore happen in any order without values
/// drifting or anything being applied twice. Effects are shared configuration (they live on assets): keep runtime
/// state in the handle, never in the effect.
/// </para>
/// </summary>
[Serializable]
public abstract class EquipmentEffect
{
    /// <summary>Name shown in the dropdown and in the inspector.</summary>
    public virtual string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>One line for tooltips ("+12 Defense, +10% Fire Resistance").</summary>
    public abstract string Describe(float strength = 1f);

    /// <summary>
    /// Applies the effect to a character and returns the handle that removes it again (null when there was nothing
    /// to apply, e.g. an empty list). <paramref name="strength"/> scales numeric values (1 = as configured).
    /// </summary>
    public abstract EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f);

    /// <summary>Reports configuration problems (inspectors and the validation window).</summary>
    public virtual void Validate(string owner, List<string> errors, List<string> warnings) { }

    /// <summary>A copy of the configuration (templates, wizards). Lists are shared: override to copy them.</summary>
    public virtual EquipmentEffect Clone() => (EquipmentEffect)MemberwiseClone();

    /// <summary>Applies a list of effects and collects their handles (null entries and failures are skipped).</summary>
    public static void ApplyAll(IList<EquipmentEffect> effects, EquipmentContext ctx, float strength, string source, List<EquipmentEffectHandle> into)
    {
        if (effects == null || ctx == null)
            return;
        string previous = ctx.CurrentSource;
        ctx.CurrentSource = source;
        for (int i = 0; i < effects.Count; i++)
        {
            EquipmentEffect e = effects[i];
            if (e == null)
                continue;
            EquipmentEffectHandle h = null;
            try { h = e.Apply(ctx, strength); }
            catch (Exception ex) { Debug.LogException(ex, ctx.Host); }
            if (h == null)
                continue;
            h.Effect = e;
            h.Source = source;
            into.Add(h);
        }
        ctx.CurrentSource = previous;
    }

    /// <summary>Reverts handles in reverse order (the last applied is removed first) and clears the list.</summary>
    public static void RevertAll(List<EquipmentEffectHandle> handles)
    {
        if (handles == null)
            return;
        for (int i = handles.Count - 1; i >= 0; i--)
            handles[i]?.Revert();
        handles.Clear();
    }

    /// <summary>Descriptions of a list of effects, one line each (empty descriptions are skipped).</summary>
    public static void DescribeAll(IList<EquipmentEffect> effects, float strength, List<string> into)
    {
        if (effects == null)
            return;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
                continue;
            string d = effects[i].Describe(strength);
            if (!string.IsNullOrEmpty(d))
                into.Add(d);
        }
    }

    /// <summary>Validates a list of effects ("owner" names the asset/section in the messages).</summary>
    public static void ValidateAll(IList<EquipmentEffect> effects, string owner, List<string> errors, List<string> warnings)
    {
        if (effects == null)
            return;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
            {
                warnings.Add($"{owner}: effect {i + 1} has no type (pick one in the dropdown or remove it).");
                continue;
            }
            effects[i].Validate($"{owner} ▸ {effects[i].MenuName}", errors, warnings);
        }
    }
}

/// <summary>
/// Undoes one applied <see cref="EquipmentEffect"/>. Reverting twice is harmless. Handles that need per-frame work
/// (conditions, input, timers) return true from <see cref="NeedsTick"/> and are ticked by the equipment manager.
/// </summary>
public abstract class EquipmentEffectHandle
{
    /// <summary>The effect that created this handle.</summary>
    public EquipmentEffect Effect { get; internal set; }
    /// <summary>What applied it (item or set bonus name), for debugging.</summary>
    public string Source { get; internal set; }
    public bool Reverted { get; private set; }

    public virtual bool NeedsTick => false;
    public virtual void Tick(float dt) { }

    /// <summary>What the effect is doing right now (shown while playing), or null.</summary>
    public virtual string LiveStatus => null;

    public void Revert()
    {
        if (Reverted)
            return;
        Reverted = true;
        try { OnRevert(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    protected abstract void OnRevert();
}

/// <summary>A handle that runs an action when reverted.</summary>
public sealed class ActionHandle : EquipmentEffectHandle
{
    private Action revert;
    private readonly string status;

    public ActionHandle(Action revert, string status = null)
    {
        this.revert = revert;
        this.status = status;
    }

    public override string LiveStatus => status;

    protected override void OnRevert()
    {
        Action a = revert;
        revert = null;
        a?.Invoke();
    }
}

/// <summary>A handle made of other handles (nested effects): ticks and reverts them all.</summary>
public class CompositeHandle : EquipmentEffectHandle
{
    protected readonly List<EquipmentEffectHandle> children = new List<EquipmentEffectHandle>();

    public IReadOnlyList<EquipmentEffectHandle> Children => children;
    public int Count => children.Count;

    public void Add(EquipmentEffectHandle h)
    {
        if (h != null)
            children.Add(h);
    }

    public override bool NeedsTick
    {
        get
        {
            for (int i = 0; i < children.Count; i++)
                if (children[i].NeedsTick) return true;
            return false;
        }
    }

    public override void Tick(float dt)
    {
        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].NeedsTick)
                continue;
            try { children[i].Tick(dt); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    protected override void OnRevert() => EquipmentEffect.RevertAll(children);
}

/// <summary>
/// The character equipment is applied to: its components (looked up once, any of them may be missing - mobs have no
/// trait manager) and the shared bookkeeping that keeps effects from different items and sets from fighting each
/// other (one armor set's trait multiplier no longer resets another's, a special mechanic stays on while any item
/// still provides it, a replaced trait comes back only when the last replacement is gone).
/// </summary>
public sealed class EquipmentContext
{
    public GameObject Owner { get; }
    /// <summary>The component that owns this context (logs, coroutines).</summary>
    public MonoBehaviour Host { get; }
    public PlayerStatusController Status { get; private set; }
    public TraitManager Traits { get; private set; }
    public PlayerAbilityController Abilities { get; private set; }
    public AudioSource Audio { get; private set; }

    /// <summary>Name of the item or set bonus being applied right now (labels for debug output).</summary>
    public string CurrentSource { get; set; } = "Equipment";

    public TraitMultiplierRegistry TraitMultipliers { get; }
    public TraitReplacementRegistry TraitReplacements { get; }
    public MechanicRegistry Mechanics { get; }
    public LegacyStatPool LegacyStats { get; }

    private CombatEntity entity;
    private CombatStats stats;
    private readonly HashSet<string> warnedOnce = new HashSet<string>();

    public EquipmentContext(MonoBehaviour host)
    {
        Host = host;
        Owner = host != null ? host.gameObject : null;
        Refresh();
        TraitMultipliers = new TraitMultiplierRegistry(this);
        TraitReplacements = new TraitReplacementRegistry(this);
        Mechanics = new MechanicRegistry(this);
        LegacyStats = new LegacyStatPool(this);
    }

    /// <summary>Looks the character's components up (again).</summary>
    public void Refresh()
    {
        if (Owner == null)
            return;
        Status = Find<PlayerStatusController>();
        Traits = Status != null && Status.TraitManager != null ? Status.TraitManager : Find<TraitManager>();
        Abilities = Find<PlayerAbilityController>();
        Audio = Find<AudioSource>();
    }

    private T Find<T>() where T : Component
    {
        T c = Owner.GetComponent<T>();
        if (c == null) c = Owner.GetComponentInParent<T>();
        if (c == null) c = Owner.GetComponentInChildren<T>(true);
        return c;
    }

    /// <summary>The character's combat entity (created by the combat system; looked up until found).</summary>
    public CombatEntity Entity
    {
        get
        {
            if (entity == null && Owner != null)
            {
                entity = Abilities != null && Abilities.Entity != null ? Abilities.Entity : CombatEntity.Resolve(Owner);
                if (entity == null)
                    entity = Owner.GetComponentInChildren<CombatEntity>(true);
            }
            return entity;
        }
    }

    /// <summary>The character's combat stats (added to the character when missing).</summary>
    public CombatStats Stats
    {
        get
        {
            if (stats == null && Owner != null)
            {
                GameObject host = Status != null ? Status.gameObject : Owner;
                stats = host.GetComponentInChildren<CombatStats>(true);
                if (stats == null)
                    stats = host.AddComponent<CombatStats>();
            }
            return stats;
        }
    }

    /// <summary>Where attached visuals go (the character's body).</summary>
    public Transform Body
    {
        get
        {
            if (Status != null && Status.MovementModel != null && Status.MovementModel.PlayerTransform != null)
                return Status.MovementModel.PlayerTransform;
            return Status != null ? Status.transform : Owner != null ? Owner.transform : null;
        }
    }

    /// <summary>Logs a warning only the first time for a given key (missing components are reported once, not per item).</summary>
    public void WarnOnce(string key, string message)
    {
        if (warnedOnce.Add(key))
            Debug.LogWarning(message, Host);
    }

    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;
        if (Audio != null)
            Audio.PlayOneShot(clip, volume);
        else if (Owner != null)
            AbilityPool.PlaySound(clip, Owner.transform.position, volume);
    }
}

// ====================================================================== registries
/// <summary>
/// Trait strength multipliers from several sources (armor sets, items). A trait's strength is the product of every
/// stacking multiplier times the strongest non-stacking one (highest priority first), so two sets that both
/// strengthen a trait no longer overwrite each other, and removing one restores exactly the other's value.
/// </summary>
public sealed class TraitMultiplierRegistry
{
    private sealed class Entry
    {
        public Trait trait;
        public float multiplier;
        public bool stacks;
        public int priority;
    }

    private readonly EquipmentContext ctx;
    private readonly Dictionary<Trait, List<Entry>> byTrait = new Dictionary<Trait, List<Entry>>(ReferenceComparer<Trait>.Instance);

    public TraitMultiplierRegistry(EquipmentContext ctx) => this.ctx = ctx;

    public object Push(Trait trait, float multiplier, bool stacks, int priority)
    {
        if (trait == null || ctx.Traits == null)
            return null;
        if (!byTrait.TryGetValue(trait, out List<Entry> list))
            byTrait[trait] = list = new List<Entry>(2);
        var e = new Entry { trait = trait, multiplier = Mathf.Max(0f, multiplier), stacks = stacks, priority = priority };
        list.Add(e);
        Publish(trait, list);
        return e;
    }

    public void Pop(object token)
    {
        if (!(token is Entry e) || !byTrait.TryGetValue(e.trait, out List<Entry> list) || !list.Remove(e))
            return;
        Publish(e.trait, list);
        if (list.Count == 0)
            byTrait.Remove(e.trait);
    }

    /// <summary>The combined multiplier equipment gives a trait (1 = none).</summary>
    public float Get(Trait trait) => trait != null && byTrait.TryGetValue(trait, out List<Entry> list) ? Combine(list) : 1f;

    private static float Combine(List<Entry> list)
    {
        float product = 1f;
        Entry best = null;
        for (int i = 0; i < list.Count; i++)
        {
            Entry e = list[i];
            if (e.stacks)
            {
                product *= e.multiplier;
                continue;
            }
            if (best == null || e.priority > best.priority ||
                (e.priority == best.priority && Mathf.Abs(e.multiplier - 1f) > Mathf.Abs(best.multiplier - 1f)))
                best = e;
        }
        return best != null ? product * best.multiplier : product;
    }

    private void Publish(Trait trait, List<Entry> list)
    {
        if (ctx.Traits != null)
            ctx.Traits.NotifyTraitMultiplierChanged(trait, Combine(list));
    }
}

/// <summary>
/// Temporary trait replacements (armor sets that swap a trait for an upgraded one). The original trait is suspended
/// (its points and grants are kept) while at least one replacement is active; when several sources replace the same
/// trait, only the highest-priority replacement is granted. Taking the last one off restores the original exactly.
/// </summary>
public sealed class TraitReplacementRegistry
{
    private sealed class Entry
    {
        public Trait original;
        public Trait replacement;
        public int priority;
        public int order;
    }

    private sealed class State
    {
        public readonly List<Entry> entries = new List<Entry>(2);
        public bool suspended;       // we suspended the original (it was active)
        public Trait granted;        // replacement currently granted
    }

    private readonly EquipmentContext ctx;
    private readonly Dictionary<Trait, State> byOriginal = new Dictionary<Trait, State>(ReferenceComparer<Trait>.Instance);
    private int nextOrder;

    public TraitReplacementRegistry(EquipmentContext ctx) => this.ctx = ctx;

    /// <summary>Is <paramref name="trait"/> currently replaced by equipment?</summary>
    public bool IsReplaced(Trait trait) => trait != null && byOriginal.ContainsKey(trait);

    public object Push(Trait original, Trait replacement, int priority)
    {
        TraitManager tm = ctx.Traits;
        if (original == null || tm == null)
            return null;
        if (!byOriginal.TryGetValue(original, out State st))
            byOriginal[original] = st = new State();
        var e = new Entry { original = original, replacement = replacement, priority = priority, order = nextOrder++ };
        st.entries.Add(e);
        if (!st.suspended && tm.SuspendTrait(original))
            st.suspended = true;
        Publish(tm, st);
        return e;
    }

    public void Pop(object token)
    {
        if (!(token is Entry e) || !byOriginal.TryGetValue(e.original, out State st) || !st.entries.Remove(e))
            return;
        TraitManager tm = ctx.Traits;
        if (tm == null)
        {
            if (st.entries.Count == 0)
                byOriginal.Remove(e.original);
            return;
        }
        Publish(tm, st);
        if (st.entries.Count > 0)
            return;
        byOriginal.Remove(e.original);
        if (st.suspended)
            tm.ResumeTrait(e.original);
    }

    private static void Publish(TraitManager tm, State st)
    {
        Entry best = null;
        for (int i = 0; i < st.entries.Count; i++)
        {
            Entry e = st.entries[i];
            if (e.replacement == null)
                continue;
            if (best == null || e.priority > best.priority || (e.priority == best.priority && e.order > best.order))
                best = e;
        }
        Trait wanted = best != null ? best.replacement : null;
        if (wanted == st.granted)
            return;
        if (st.granted != null)
            tm.RemoveTrait(st.granted, true);
        st.granted = wanted;
        if (wanted != null)
            tm.AddTrait(wanted, true);
    }
}

/// <summary>
/// Special mechanics (string-based handlers such as "double_jump") provided by several items or sets: turned on when
/// the first source needs it and off only when the last one lets go (two sets with the same mechanic used to turn it
/// off for both when one was removed).
/// </summary>
public sealed class MechanicRegistry
{
    private sealed class Entry
    {
        public string id;
        public SpecialMechanic mechanic;
    }

    private readonly EquipmentContext ctx;
    private readonly Dictionary<string, List<Entry>> byId = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when a mechanic turns on (true) or off (false).</summary>
    public event Action<SpecialMechanic, bool> Toggled;

    public MechanicRegistry(EquipmentContext ctx) => this.ctx = ctx;

    public bool IsActive(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id.Trim());

    public IEnumerable<string> ActiveIds => byId.Keys;

    public object Push(SpecialMechanic mechanic)
    {
        if (mechanic == null || string.IsNullOrWhiteSpace(mechanic.mechanicId))
            return null;
        string id = mechanic.mechanicId.Trim();
        if (!byId.TryGetValue(id, out List<Entry> list))
            byId[id] = list = new List<Entry>(1);
        var e = new Entry { id = id, mechanic = mechanic };
        list.Add(e);
        if (list.Count == 1 && mechanic.activateOnEquip)
            Set(mechanic, true);
        return e;
    }

    public void Pop(object token)
    {
        if (!(token is Entry e) || !byId.TryGetValue(e.id, out List<Entry> list) || !list.Remove(e))
            return;
        if (list.Count > 0)
            return;
        byId.Remove(e.id);
        if (e.mechanic.deactivateOnUnequip)
            Set(e.mechanic, false);
    }

    private void Set(SpecialMechanic mechanic, bool on)
    {
        try
        {
            EffectRegistry registry = EffectRegistry.Instance;
            if (registry != null)
            {
                // A mechanic can bring its own handler: spawned on the character the first time it is needed.
                if (on && mechanic.handlerPrefab != null && !registry.HasMechanicHandler(mechanic.mechanicId) && ctx.Owner != null)
                {
                    SpecialMechanicHandlerBase handler = UnityEngine.Object.Instantiate(mechanic.handlerPrefab, ctx.Owner.transform);
                    registry.RegisterMechanicHandler(mechanic.mechanicId, handler);
                }
                registry.ApplySpecialMechanic(mechanic, on);
            }
        }
        catch (Exception e) { Debug.LogException(e, ctx.Host); }
        Toggled?.Invoke(mechanic, on);
    }
}
