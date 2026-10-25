using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

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
