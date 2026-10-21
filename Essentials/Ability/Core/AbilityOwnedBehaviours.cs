using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the <see cref="AbilityDefinition.whileOwned"/> behaviours of the abilities in a caster's slots: started when
/// an ability lands in a slot, stopped when it leaves the last slot that held it (replaced, cleared, forgotten,
/// expired, its asset destroyed) or when the caster goes away. They run through the character's
/// <see cref="TraitManager"/>, exactly like a trait's behaviours; a character without one (a mob) skips them.
/// </summary>
internal sealed class AbilityOwnedBehaviours
{
    private sealed class Entry
    {
        public readonly List<TraitBehaviour> runtimes = new List<TraitBehaviour>();
    }

    private readonly AbilityCaster caster;
    private readonly Dictionary<AbilityDefinition, Entry> running = new Dictionary<AbilityDefinition, Entry>(ReferenceComparer<AbilityDefinition>.Instance);
    private readonly HashSet<AbilityDefinition> held = new HashSet<AbilityDefinition>(ReferenceComparer<AbilityDefinition>.Instance);
    private readonly List<AbilityDefinition> stopBuffer = new List<AbilityDefinition>();
    private TraitManager traits;
    private bool searched, warned;

    public AbilityOwnedBehaviours(AbilityCaster caster) => this.caster = caster;

    /// <summary>Abilities whose While Owned behaviours are running.</summary>
    public int RunningCount => running.Count;

    private TraitManager Traits
    {
        get
        {
            if (traits == null && !searched)
            {
                searched = true;
                traits = caster.GetComponent<TraitManager>();
                if (traits == null) traits = caster.GetComponentInParent<TraitManager>();
                if (traits == null) traits = caster.transform.root.GetComponentInChildren<TraitManager>(true);
            }
            return traits;
        }
    }

    /// <summary>Starts what new abilities need and stops what is no longer held.</summary>
    public void Refresh(IReadOnlyList<AbilitySlot> slots)
    {
        if (!Application.isPlaying)
            return;
        held.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            AbilityDefinition a = slots[i]?.ability;
            if (a != null && a.HasWhileOwned)
                held.Add(a);
        }

        stopBuffer.Clear();
        foreach (AbilityDefinition a in running.Keys)
            if (a == null || !held.Contains(a))
                stopBuffer.Add(a);
        foreach (AbilityDefinition a in stopBuffer)
            Stop(a);

        foreach (AbilityDefinition a in held)
            if (!running.ContainsKey(a))
                Start(a);
    }

    /// <summary>Stops everything (the caster is destroyed).</summary>
    public void StopAll()
    {
        stopBuffer.Clear();
        stopBuffer.AddRange(running.Keys);
        foreach (AbilityDefinition a in stopBuffer)
            Stop(a);
    }

    /// <summary>Stops the behaviours of abilities whose asset was destroyed while held.</summary>
    public void CheckDestroyed(IReadOnlyList<AbilitySlot> slots)
    {
        foreach (AbilityDefinition a in running.Keys)
        {
            if (a == null)
            {
                Refresh(slots);
                return;
            }
        }
    }

    private void Start(AbilityDefinition a)
    {
        TraitManager tm = Traits;
        if (tm == null)
        {
            if (!warned)
                Debug.LogWarning($"[Abilities] {caster.name}: '{a.DisplayName}' has While Owned behaviours, which need a Trait Manager on the character; they were skipped.", caster);
            warned = true;
            return;
        }
        var e = new Entry();
        foreach (TraitBehaviour template in a.whileOwned)
        {
            if (template == null)
                continue;
            TraitBehaviour rt = template.CreateRuntimeCopy();
            e.runtimes.Add(rt);
            tm.AddExternalBehaviour(rt);
        }
        running[a] = e;
    }

    private void Stop(AbilityDefinition a)
    {
        if (!running.TryGetValue(a, out Entry e))
            return;
        running.Remove(a);
        TraitManager tm = traits;
        if (tm == null)
            return;
        for (int i = e.runtimes.Count - 1; i >= 0; i--)
            tm.RemoveExternalBehaviour(e.runtimes[i]);
    }
}
