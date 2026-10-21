using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Everything a running <see cref="CharacterPower"/> can reach on the character that has it.</summary>
public sealed class PowerContext
{
    public CombatEntity Entity { get; internal set; }
    public Transform Root { get; internal set; }
    public ActivePowers Runner { get; internal set; }
    /// <summary>Who gave the power (the caster of the ability).</summary>
    public CombatEntity Source { get; internal set; }
    /// <summary>Strength multiplier (the caster's Buff Strength).</summary>
    public float Strength { get; internal set; } = 1f;
    /// <summary>Seconds the power lasts (infinity = until it ends itself or is cast again).</summary>
    public float Duration { get; internal set; }
    /// <summary>Seconds since it started.</summary>
    public float Elapsed { get; internal set; }
    /// <summary>The name shown for the power.</summary>
    public string Label { get; internal set; }
    /// <summary>
    /// True on a network client's copy of the character: the server runs the power for real and this copy only shows
    /// it (size, hidden meshes, copies, gates, links). Gameplay (damage, health, stats, teleports) must be skipped.
    /// </summary>
    public bool IsPresentation { get; internal set; }

    private CharacterBody body;
    private TraitManager traits;
    private bool bodySearched, traitsSearched;

    public CombatStats Stats => Entity != null && Entity.Stats != null ? Entity.Stats : CombatStats.For(Root, addIfMissing: true);

    /// <summary>The character's Character Body (size), if it has one.</summary>
    public CharacterBody Body
    {
        get
        {
            if (body == null && !bodySearched)
            {
                bodySearched = true;
                body = Root.GetComponentInChildren<CharacterBody>(true);
                if (body == null) body = Root.GetComponentInParent<CharacterBody>();
            }
            return body;
        }
    }

    /// <summary>The character's Trait Manager (players), if it has one.</summary>
    public TraitManager Traits
    {
        get
        {
            if (traits == null && !traitsSearched)
            {
                traitsSearched = true;
                traits = Root.GetComponentInChildren<TraitManager>(true);
                if (traits == null) traits = Root.GetComponentInParent<TraitManager>();
            }
            return traits;
        }
    }

    /// <summary>Is this character the player playing on this machine?</summary>
    public bool IsLocalPlayer => Entity != null && Entity.Kind == CombatEntity.EntityKind.Player && PlayerLocator.Local == Entity;

    /// <summary>Tells the network layer the power's visible state changed (a copy vanished, a gate moved...).</summary>
    public void MarkDirty() => Runner?.MarkDirty();
}

/// <summary>Implemented by trait behaviours that can end a power by themselves (Sky Walk runs out of mana).</summary>
public interface IPowerEndsItself
{
    bool PowerFinished { get; }
}

/// <summary>
/// How powers refer to other characters over the network (a soul link's partner, a leech's victim). The network
/// layer sets both functions; offline they stay null and only local references are used.
/// </summary>
public static class PowerNet
{
    /// <summary>The network id of a character (-1 when it has none).</summary>
    public static Func<CombatEntity, int> IdOf;
    /// <summary>The character with a network id (null when unknown).</summary>
    public static Func<int, CombatEntity> Resolve;

    public static int Id(CombatEntity e) => e != null && IdOf != null ? IdOf(e) : -1;
    public static CombatEntity Find(int id) => id >= 0 && Resolve != null ? Resolve(id) : null;
}

/// <summary>
/// A temporary power on a character: a bigger or smaller body, invisibility, immunity to crowd control, illusory
/// copies, walking on air, a link to another character... Given by the <see cref="PowerEffect"/> of an ability (or
/// from code through <see cref="ActivePowers.Run"/>) and run by the character's <see cref="ActivePowers"/> until its
/// time runs out, it ends itself (<see cref="Finished"/>) or the ability is cast again (toggle). Every character gets
/// its own copy.
/// <para>Multiplayer: the server runs the power; every client's copy of the character runs it too with
/// <see cref="PowerContext.IsPresentation"/> set, to show it. What a client copy needs beyond the configuration (where
/// the copies stand, who the partner is) goes through <see cref="NetDynamic"/>, and what changes while it runs (copies
/// alive) through <see cref="NetState"/>.</para>
/// </summary>
[Serializable]
public abstract class CharacterPower
{
    /// <summary>Set it to end the whole power now (invisibility broken by a hit, out of mana...).</summary>
    [NonSerialized] public bool Finished;

    public abstract string Describe();
    public virtual void Start(PowerContext ctx) { }
    public virtual void Tick(PowerContext ctx, float dt) { }
    /// <summary>Undo everything Start did. Always called once, whatever ended the power.</summary>
    public virtual void End(PowerContext ctx) { }
    /// <summary>The character took a hit (server / offline).</summary>
    public virtual void OnOwnerDamaged(PowerContext ctx, DamageInfo info) { }
    /// <summary>The character started a weapon attack or an ability (server / offline).</summary>
    public virtual void OnOwnerActed(PowerContext ctx) { }
    /// <summary>
    /// The ability that gave the power was cast again while it runs. Return true to keep the power (it handled the
    /// recast, e.g. Phantom Exchange swaps places); false lets the power restart (or end, with Toggle).
    /// </summary>
    public virtual bool OnRecast(PowerContext ctx) => false;
    /// <summary>What it is doing right now (the Active Powers inspector).</summary>
    public virtual string LiveStatus => null;
    public virtual void Validate(string owner, List<string> errors, List<string> warnings) { }
    public virtual CharacterPower Clone() => (CharacterPower)MemberwiseClone();
    public string MenuName => AbilityTypeNames.Nice(GetType());

    // ------------------------------------------------------------------ network
    /// <summary>
    /// The configuration sent to clients when they cannot find the power's asset (JSON of its fields; references to
    /// prefabs and other assets are left out).
    /// </summary>
    public virtual string NetConfig() => ScrubReferences(JsonUtility.ToJson(this));

    /// <summary>Reads <see cref="NetConfig"/> into a fresh power.</summary>
    public virtual void ReadNetConfig(string json)
    {
        if (!string.IsNullOrEmpty(json))
            JsonUtility.FromJsonOverwrite(json, this);
    }

    /// <summary>What a client copy needs that the server decided when it started (positions, partner id...). Server side.</summary>
    public virtual string NetDynamic() => null;
    /// <summary>Applies <see cref="NetDynamic"/> on a client copy, before <see cref="Start"/>.</summary>
    public virtual void ApplyNetDynamic(string data) { }
    /// <summary>
    /// A client copy: <see cref="NetDynamic"/> changed on the server while the power runs (a phantom swapped places).
    /// The power calls <see cref="PowerContext.MarkDirty"/> on the server when it changes it.
    /// </summary>
    public virtual void OnNetDynamicChanged(PowerContext ctx, string data) { }
    /// <summary>A small state that changes while the power runs (which copies are alive). Server side.</summary>
    public virtual int NetState => 0;
    /// <summary>Applies <see cref="NetState"/> on a client copy (after Start, and whenever it changes).</summary>
    public virtual void ApplyNetState(PowerContext ctx, int state) { }

    private static readonly Regex InstanceIds = new Regex("\"instanceID\":-?\\d+", RegexOptions.Compiled);

    /// <summary>Removes asset references from JSON made by JsonUtility (they mean nothing on another machine).</summary>
    public static string ScrubReferences(string json) => string.IsNullOrEmpty(json) ? json : InstanceIds.Replace(json, "\"instanceID\":0");
}

/// <summary>The state of every power of a character, as sent to clients (JSON).</summary>
[Serializable]
public class PowerNetSnapshot
{
    [Serializable]
    public class Run
    {
        public int id;
        public string label;
        public string source;
        public float remaining;
        public float strength = 1f;
        public List<string> types = new List<string>();
        public List<string> configs = new List<string>();
        public List<string> dynamic = new List<string>();
        public List<int> states = new List<int>();
    }

    public List<Run> runs = new List<Run>();
}

/// <summary>
/// Where powers come from, so a client can rebuild them from its own assets (with their prefabs and effects) instead of
/// the JSON configuration: "ability:{id}#{n}" is the n-th Power effect of an ability.
/// </summary>
public static class PowerSources
{
    private static readonly Dictionary<AbilityDefinition, List<PowerEffect>> cache = new Dictionary<AbilityDefinition, List<PowerEffect>>();

    /// <summary>The source key of <paramref name="effect"/> inside <paramref name="ability"/> (null when not found).</summary>
    public static string KeyOf(AbilityDefinition ability, PowerEffect effect)
    {
        if (ability == null || effect == null || string.IsNullOrEmpty(ability.Id))
            return null;
        int i = EffectsOf(ability).IndexOf(effect);
        return i >= 0 ? $"ability:{ability.Id}#{i}" : null;
    }

    /// <summary>The powers a source key names (null when the asset is not found).</summary>
    public static List<CharacterPower> Resolve(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith("ability:", StringComparison.Ordinal))
            return null;
        int hash = key.LastIndexOf('#');
        if (hash < 0 || !int.TryParse(key.Substring(hash + 1), out int index))
            return null;
        AbilityDefinition a = AbilityDatabase.Resolve(key.Substring(8, hash - 8));
        if (a == null)
            return null;
        List<PowerEffect> list = EffectsOf(a);
        return index >= 0 && index < list.Count ? list[index].powers : null;
    }

    /// <summary>Every Power effect of an ability, in a stable order (actions, then their fields, depth first).</summary>
    public static List<PowerEffect> EffectsOf(AbilityDefinition ability)
    {
        if (cache.TryGetValue(ability, out List<PowerEffect> list))
            return list;
        list = new List<PowerEffect>();
        var seen = new HashSet<object>(ReferenceComparer<object>.Instance);
        if (ability.actions != null)
            foreach (CastAction a in ability.actions)
                Collect(a, list, seen, 0);
        cache[ability] = list;
        return list;
    }

    private static void Collect(object o, List<PowerEffect> into, HashSet<object> seen, int depth)
    {
        if (o == null || depth > 6 || o is UnityEngine.Object || !seen.Add(o))
            return;
        if (o is PowerEffect p)
        {
            into.Add(p);
            return;
        }
        if (o is System.Collections.IList l)
        {
            foreach (object e in l)
                Collect(e, into, seen, depth + 1);
            return;
        }
        Type t = o.GetType();
        if (t.IsPrimitive || o is string || t.IsEnum)
            return;
        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            Type ft = f.FieldType;
            if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(ft) || ft.IsValueType)
                continue;
            Collect(f.GetValue(o), into, seen, depth + 1);
        }
    }
}

/// <summary>
/// Runs the <see cref="CharacterPower"/>s of a character (added automatically the first time one is given). Each run
/// lasts its duration, ends early when a power finishes, when the character dies or this component goes away, and is
/// switched off by casting a toggle ability again. In a network session the server's runs are mirrored on every
/// client's copy of the character (<see cref="CaptureNet"/> / <see cref="ApplyNet"/>), which only shows them.
/// </summary>
[DisallowMultipleComponent]
public class ActivePowers : MonoBehaviour
{
    /// <summary>One power given to the character.</summary>
    public sealed class Running
    {
        public object key;
        public string label;
        public string source;
        public int netId;
        public readonly List<CharacterPower> powers = new List<CharacterPower>();
        public readonly List<string> netDynamic = new List<string>();
        public PowerContext ctx;
        public float until;
        public float restartCooldown;
        public bool ended;

        public float Remaining => float.IsPositiveInfinity(until) ? float.PositiveInfinity : Mathf.Max(0f, until - Time.time);
    }

    private readonly List<Running> running = new List<Running>();
    private readonly List<Running> buffer = new List<Running>();
    private readonly Dictionary<object, float> restartAt = new Dictionary<object, float>();
    private readonly List<int> lastStates = new List<int>();
    private CombatEntity entity;
    private WeaponController weapons;
    private AbilityCaster caster;
    private int nextNetId = 1;

    /// <summary>The powers running now.</summary>
    public IReadOnlyList<Running> All => running;

    /// <summary>Raised when a power starts or ends.</summary>
    public event Action Changed;

    /// <summary>Goes up whenever what clients must see changes (a power starts or ends, a copy vanishes...).</summary>
    public int Version { get; private set; }

    /// <summary>True on a network client's copy: powers only come from the server (<see cref="ApplyNet"/>).</summary>
    public bool IsPresentation => entity != null && entity.IsReplica;

    /// <summary>The runner of <paramref name="e"/> (added when missing).</summary>
    public static ActivePowers For(CombatEntity e)
    {
        if (e == null)
            return null;
        ActivePowers p = e.GetComponent<ActivePowers>();
        if (p == null)
            p = e.gameObject.AddComponent<ActivePowers>();
        p.Init();
        return p;
    }

    private void Init()
    {
        if (entity != null)
            return;
        entity = GetComponent<CombatEntity>();
        if (entity == null)
            return;
        entity.Damaged += OnDamaged;
        entity.Died += OnDied;
        weapons = GetComponentInChildren<WeaponController>(true);
        if (weapons == null) weapons = GetComponentInParent<WeaponController>();
        if (weapons != null)
            weapons.AttackStarted += OnAttackStarted;
        caster = entity.Caster;
        if (caster != null)
            caster.CastStarted += OnCastStarted;
    }

    private void OnDestroy()
    {
        StopAll();
        if (entity != null)
        {
            entity.Damaged -= OnDamaged;
            entity.Died -= OnDied;
        }
        if (weapons != null)
            weapons.AttackStarted -= OnAttackStarted;
        if (caster != null)
            caster.CastStarted -= OnCastStarted;
    }

    private void OnDisable() => StopAll();

    /// <summary>Clients must be told something changed (see <see cref="Version"/>).</summary>
    public void MarkDirty() => Version++;

    /// <summary>Is a power with this key running?</summary>
    public bool IsRunning(object key) => Find(key) != null;

    /// <summary>Seconds before the power with this key can start again (its Restart Cooldown).</summary>
    public float RestartCooldownLeft(object key) => key != null && restartAt.TryGetValue(key, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

    /// <summary>
    /// Gives the character copies of <paramref name="templates"/> for <paramref name="duration"/> seconds (0 or less =
    /// until a power ends itself or the power is stopped). The same key again lets the running powers handle the
    /// recast (<see cref="CharacterPower.OnRecast"/>), else restarts it, or with <paramref name="toggle"/> stops it.
    /// Returns the run, or null when it was stopped or cannot start yet (<paramref name="restartCooldown"/>).
    /// </summary>
    public Running Run(object key, string label, IList<CharacterPower> templates, float duration, float strength, CombatEntity source,
        bool toggle = false, float restartCooldown = 0f, string sourceKey = null)
    {
        Init();
        if (entity == null || templates == null || IsPresentation)
            return null;
        Running existing = Find(key);
        if (existing != null)
        {
            bool handled = false;
            foreach (CharacterPower p in existing.powers)
                handled |= p.OnRecast(existing.ctx);
            if (handled)
            {
                MarkDirty();
                EndFinished();
                return existing;
            }
            Finish(existing);
            if (toggle)
                return null;
        }
        else if (RestartCooldownLeft(key) > 0f)
            return null;
        var r = new Running
        {
            key = key,
            label = string.IsNullOrEmpty(label) ? "Power" : label,
            source = sourceKey,
            netId = nextNetId++,
            until = duration > 0f ? Time.time + duration : float.PositiveInfinity,
            restartCooldown = restartCooldown,
        };
        r.ctx = NewContext(r, source, strength, duration, false);
        foreach (CharacterPower t in templates)
            if (t != null)
                r.powers.Add(t.Clone());
        Begin(r);
        return r;
    }

    private PowerContext NewContext(Running r, CombatEntity source, float strength, float duration, bool presentation) => new PowerContext
    {
        Entity = entity,
        Root = entity.transform,
        Runner = this,
        Source = source,
        Strength = strength,
        Duration = duration > 0f ? duration : float.PositiveInfinity,
        Label = r.label,
        IsPresentation = presentation,
    };

    private void Begin(Running r)
    {
        running.Add(r);
        foreach (CharacterPower p in r.powers)
        {
            try { p.Start(r.ctx); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
        MarkDirty();
        Changed?.Invoke();
        EndFinished();
    }

    /// <summary>Ends the power with this key now. Returns true if one was running.</summary>
    public bool Stop(object key)
    {
        Running r = Find(key);
        if (r == null)
            return false;
        Finish(r);
        return true;
    }

    /// <summary>Ends every power now.</summary>
    public void StopAll()
    {
        buffer.Clear();
        buffer.AddRange(running);
        foreach (Running r in buffer)
            Finish(r);
        buffer.Clear();
    }

    private Running Find(object key)
    {
        for (int i = 0; i < running.Count; i++)
            if (Equals(running[i].key, key))
                return running[i];
        return null;
    }

    private void Update()
    {
        if (running.Count == 0)
            return;
        float dt = Time.deltaTime;
        buffer.Clear();
        buffer.AddRange(running);
        foreach (Running r in buffer)
        {
            if (r.ended)
                continue;
            r.ctx.Elapsed += dt;
            foreach (CharacterPower p in r.powers)
            {
                try { p.Tick(r.ctx, dt); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            if (!r.ctx.IsPresentation && Time.time >= r.until)
                Finish(r);
        }
        buffer.Clear();
        EndFinished();
        // Server: a power's small state changed (a copy vanished): clients must hear it.
        if (!IsPresentation)
        {
            int n = 0;
            bool changed = false;
            foreach (Running r in running)
                foreach (CharacterPower p in r.powers)
                {
                    int s = p.NetState;
                    if (n >= lastStates.Count) { lastStates.Add(s); changed = true; }
                    else if (lastStates[n] != s) { lastStates[n] = s; changed = true; }
                    n++;
                }
            if (changed)
                MarkDirty();
        }
    }

    private void EndFinished()
    {
        if (IsPresentation)
            return; // a client copy ends its powers when the server says so
        for (int i = running.Count - 1; i >= 0; i--)
        {
            if (i >= running.Count) continue;
            Running r = running[i];
            foreach (CharacterPower p in r.powers)
            {
                if (p.Finished)
                {
                    Finish(r);
                    break;
                }
            }
        }
    }

    private void Finish(Running r)
    {
        if (r.ended)
            return;
        r.ended = true;
        running.Remove(r);
        if (r.restartCooldown > 0f && r.key != null)
            restartAt[r.key] = Time.time + r.restartCooldown;
        for (int i = r.powers.Count - 1; i >= 0; i--)
        {
            try { r.powers[i].End(r.ctx); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
        MarkDirty();
        Changed?.Invoke();
    }

    private void OnDamaged(DamageInfo info)
    {
        if (running.Count == 0 || IsPresentation)
            return;
        buffer.Clear();
        buffer.AddRange(running);
        foreach (Running r in buffer)
            foreach (CharacterPower p in r.powers)
                p.OnOwnerDamaged(r.ctx, info);
        buffer.Clear();
        EndFinished();
    }

    private void OnActed()
    {
        if (running.Count == 0 || IsPresentation)
            return;
        buffer.Clear();
        buffer.AddRange(running);
        foreach (Running r in buffer)
            foreach (CharacterPower p in r.powers)
                p.OnOwnerActed(r.ctx);
        buffer.Clear();
        EndFinished();
    }

    private void OnAttackStarted(AttackType input, AttackComponent attack) => OnActed();
    private void OnCastStarted(AbilityCastInstance cast) => OnActed();
    private void OnDied(CombatEntity killer) => StopAll();

    // ------------------------------------------------------------------ network
    /// <summary>The server's powers as sent to clients (JSON).</summary>
    public string CaptureNet()
    {
        var snap = new PowerNetSnapshot();
        foreach (Running r in running)
        {
            var nr = new PowerNetSnapshot.Run
            {
                id = r.netId, label = r.label, source = r.source ?? "", strength = r.ctx.Strength,
                remaining = float.IsPositiveInfinity(r.until) ? -1f : r.Remaining,
            };
            foreach (CharacterPower p in r.powers)
            {
                nr.types.Add(p.GetType().FullName);
                nr.configs.Add(r.source != null ? "" : p.NetConfig() ?? "");
                nr.dynamic.Add(p.NetDynamic() ?? "");
                nr.states.Add(p.NetState);
            }
            snap.runs.Add(nr);
        }
        return JsonUtility.ToJson(snap);
    }

    /// <summary>A client copy: shows the server's powers (starts the new ones, ends the gone ones, updates states).</summary>
    public void ApplyNet(string json)
    {
        Init();
        if (entity == null)
            return;
        PowerNetSnapshot snap;
        try { snap = string.IsNullOrEmpty(json) ? new PowerNetSnapshot() : JsonUtility.FromJson<PowerNetSnapshot>(json); }
        catch (Exception) { return; }
        if (snap == null || snap.runs == null)
            return;

        // Ended on the server
        buffer.Clear();
        foreach (Running r in running)
        {
            bool still = false;
            foreach (PowerNetSnapshot.Run nr in snap.runs)
                if (nr.id == r.netId) { still = true; break; }
            if (!still) buffer.Add(r);
        }
        foreach (Running r in buffer)
            Finish(r);
        buffer.Clear();

        foreach (PowerNetSnapshot.Run nr in snap.runs)
        {
            Running r = null;
            foreach (Running x in running)
                if (x.netId == nr.id) { r = x; break; }
            if (r == null)
            {
                r = new Running { key = "net:" + nr.id, label = nr.label, source = nr.source, netId = nr.id, until = float.PositiveInfinity };
                r.ctx = NewContext(r, null, Mathf.Clamp(nr.strength, 0f, 10f), nr.remaining, true);
                if (!Rebuild(nr, r.powers))
                    continue;
                r.netDynamic.AddRange(nr.dynamic);
                running.Add(r);
                foreach (CharacterPower p in r.powers)
                {
                    try { p.Start(r.ctx); }
                    catch (Exception e) { Debug.LogException(e, this); }
                }
                Changed?.Invoke();
            }
            for (int i = 0; i < r.powers.Count && i < nr.dynamic.Count; i++)
            {
                string d = nr.dynamic[i] ?? "";
                if (i < r.netDynamic.Count && r.netDynamic[i] == d)
                    continue;
                while (r.netDynamic.Count <= i) r.netDynamic.Add("");
                r.netDynamic[i] = d;
                try { r.powers[i].OnNetDynamicChanged(r.ctx, d); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            for (int i = 0; i < r.powers.Count && i < nr.states.Count; i++)
            {
                try { r.powers[i].ApplyNetState(r.ctx, nr.states[i]); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }
    }

    /// <summary>Builds a client copy's powers: from the source asset when it is found, else from the JSON configuration.</summary>
    private static bool Rebuild(PowerNetSnapshot.Run nr, List<CharacterPower> into)
    {
        into.Clear();
        List<CharacterPower> templates = PowerSources.Resolve(nr.source);
        bool fromAsset = templates != null && templates.Count == nr.types.Count;
        for (int i = 0; i < nr.types.Count; i++)
        {
            CharacterPower p;
            if (fromAsset && templates[i] != null && templates[i].GetType().FullName == nr.types[i])
                p = templates[i].Clone();
            else
            {
                Type t = FindType(nr.types[i]);
                if (t == null || !typeof(CharacterPower).IsAssignableFrom(t) || t.IsAbstract)
                    return false;
                p = (CharacterPower)Activator.CreateInstance(t);
                p.ReadNetConfig(i < nr.configs.Count ? nr.configs[i] : null);
            }
            if (i < nr.dynamic.Count && !string.IsNullOrEmpty(nr.dynamic[i]))
                p.ApplyNetDynamic(nr.dynamic[i]);
            into.Add(p);
        }
        return into.Count > 0;
    }

    private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>();

    /// <summary>A power or trait behaviour type by its full name (only those two families are ever created).</summary>
    public static Type FindType(string fullName)
    {
        if (string.IsNullOrEmpty(fullName) || fullName.Length > 256)
            return null;
        if (types.TryGetValue(fullName, out Type t))
            return t;
        t = typeof(CharacterPower).Assembly.GetType(fullName);
        if (t == null)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) break;
            }
        if (t != null && !typeof(CharacterPower).IsAssignableFrom(t) && !typeof(TraitBehaviour).IsAssignableFrom(t))
            t = null;
        types[fullName] = t;
        return t;
    }
}

/// <summary>
/// Gives the hit character (usually the caster: Hit ▸ Direct Effect on the caster) one or more powers for a while:
/// Change Size, Invisibility, Immune To Control, Mirage Copies, a movement behaviour such as Sky Walk...
/// </summary>
[Serializable, AbilityMenu("Buff/Power (Size, Invisibility, Mirage, Sky Walk...)", "Gives the target temporary powers: a giant or tiny body, invisibility, immunity to crowd control, illusory copies, walking on air... Toggle: casting the ability again ends it.", 1)]
public class PowerEffect : AbilityEffect
{
    [Tooltip("Name shown for the power (empty = the ability's name).")]
    public string label = "";
    [Tooltip("Seconds it lasts (scaled by the ability's Duration and the caster's Buff Duration). 0 = until a power ends itself (Sky Walk out of mana) or the ability is cast again.")]
    [Min(0f)] public float duration = 10f;
    [Tooltip("Casting the ability again while the power runs ends it (Sky Walk on and off).")]
    public bool toggle = false;
    [Tooltip("Seconds after the power ends before it can be given again (for abilities with a short cooldown whose recast does something, like Phantom Exchange).")]
    [Min(0f)] public float restartCooldown = 0f;
    [Tooltip("What the character gets for that time.")]
    [SerializeReference, SubclassSelector] public List<CharacterPower> powers = new List<CharacterPower>();

    public PowerEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public PowerEffect(float duration, params CharacterPower[] powers) : this()
    {
        this.duration = duration;
        this.powers = new List<CharacterPower>(powers);
    }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null || powers == null || powers.Count == 0)
            return;
        Give(ctx.target, ctx.caster, ctx.Ability, ctx.stats.duration, powers);
    }

    /// <summary>Gives <paramref name="list"/> (this effect's powers, or copies set up for one target) to <paramref name="target"/>.</summary>
    public ActivePowers.Running Give(CombatEntity target, CombatEntity caster, AbilityDefinition ability, float durationScale, IList<CharacterPower> list)
    {
        float time = duration;
        float strength = 1f;
        CombatStats cs = caster != null ? caster.Stats : null;
        if (time > 0f)
        {
            time *= durationScale > 0f ? durationScale : 1f;
            if (cs != null)
                time *= cs.BuffDurationMultiplier;
            time = Mathf.Max(0.1f, time);
        }
        if (cs != null)
            strength *= cs.BuffStrengthMultiplier;
        string name = !string.IsNullOrEmpty(label) ? label : ability != null ? ability.DisplayName : "Power";
        // One run per power name: casting again lets it handle the recast, restarts it, or ends it with Toggle.
        return ActivePowers.For(target).Run("power:" + name, name, list, time, strength, caster, toggle, restartCooldown,
            ReferenceEquals(list, powers) ? PowerSources.KeyOf(ability, this) : null);
    }

    public override string Describe(in AbilityStats s)
    {
        var parts = new List<string>();
        if (powers != null)
            foreach (CharacterPower p in powers)
                if (p != null)
                {
                    string d = p.Describe();
                    if (!string.IsNullOrEmpty(d)) parts.Add(d);
                }
        string what = parts.Count > 0 ? string.Join("; ", parts) : "nothing";
        string time = duration > 0f ? $" for {duration * s.duration:0.#} s" : " until it ends";
        return what + time + (toggle ? " (cast again to end)" : "");
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (powers == null || powers.Count == 0)
        {
            errors.Add($"{owner}: Power has no powers. Add one (Change Size, Invisibility...).");
            return;
        }
        for (int i = 0; i < powers.Count; i++)
        {
            if (powers[i] == null)
                warnings.Add($"{owner}: power #{i + 1} is empty.");
            else
                powers[i].Validate($"{owner} › {powers[i].MenuName}", errors, warnings);
        }
        if (duration <= 0f && !toggle)
        {
            bool endsItself = false;
            foreach (CharacterPower p in powers)
                if (p is MovementPower m && m.behaviour is IPowerEndsItself)
                    endsItself = true;
            if (!endsItself)
                warnings.Add($"{owner}: Duration 0 without Toggle: the power never ends.");
        }
    }

    public override AbilityEffect Clone()
    {
        var c = (PowerEffect)base.Clone();
        c.powers = new List<CharacterPower>();
        if (powers != null)
            foreach (CharacterPower p in powers)
                c.powers.Add(p?.Clone());
        return c;
    }
}

/// <summary>
/// Runs powers for as long as the item is worn (or the set bonus is active): a giant's belt, boots that walk on air,
/// a ring that makes the wearer immovable...
/// </summary>
[Serializable, AbilityMenu("Special/Power While Worn", "Gives powers (size, immunity to control, Sky Walk...) for as long as the item is worn.", 1)]
public class PowerWhileWornEffect : EquipmentEffect
{
    [Tooltip("Name shown for the power.")]
    public string label = "Item Power";
    [SerializeReference, SubclassSelector] public List<CharacterPower> powers = new List<CharacterPower>();

    public PowerWhileWornEffect() { }
    public PowerWhileWornEffect(string label, params CharacterPower[] powers)
    {
        this.label = label;
        this.powers = new List<CharacterPower>(powers);
    }

    public override string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        if (powers != null)
            foreach (CharacterPower p in powers)
                if (p != null && !string.IsNullOrEmpty(p.Describe()))
                    parts.Add(p.Describe());
        return string.Join("; ", parts);
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (powers == null || powers.Count == 0 || ctx.Owner == null)
            return null;
        CombatEntity e = CombatEntity.Resolve(ctx.Owner);
        if (e == null || e.IsReplica)
            return null; // client copies get the power from the server
        ActivePowers runner = ActivePowers.For(e);
        // A key per application, so two items with the same power do not end each other.
        object key = new object();
        runner.Run(key, label, powers, 0f, strength, e);
        return new ActionHandle(() => { if (runner != null) runner.Stop(key); });
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (powers == null || powers.Count == 0)
            errors.Add($"{owner}: no powers.");
        else
            foreach (CharacterPower p in powers)
                if (p != null)
                    p.Validate(owner, errors, warnings);
    }

    public override EquipmentEffect Clone()
    {
        var c = (PowerWhileWornEffect)base.Clone();
        c.powers = new List<CharacterPower>();
        if (powers != null)
            foreach (CharacterPower p in powers)
                c.powers.Add(p?.Clone());
        return c;
    }
}
